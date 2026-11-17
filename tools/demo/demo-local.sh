#!/usr/bin/env bash
# Demo temporal desde ESTA computadora (Linux): FlowTwin en un contenedor, en modo producción, más un túnel HTTPS de
# Cloudflare que da una dirección pública (https://….trycloudflare.com). Para mostrar el sistema mientras no hay nube.
# Guía: docs/despliegue.md, sección C.
#
#   ./tools/demo/demo-local.sh iniciar     # compila, arranca y muestra la dirección pública
#   ./tools/demo/demo-local.sh estado      # ¿está corriendo? ¿cuál es la dirección?
#   ./tools/demo/demo-local.sh detener     # apaga la app y el túnel (la dirección deja de funcionar)
#   ./tools/demo/demo-local.sh clave       # cambia la contraseña temporal del Administrador inicial
#
# Mientras está iniciado, cualquiera con la dirección llega a la pantalla de inicio de sesión: deténlo al terminar.
set -euo pipefail

RAIZ=$(cd "$(dirname "$0")/../.." && pwd)
CONFIG="$RAIZ/tools/demo/demo.env"          # secretos de la demo: está en .gitignore
IMAGEN=flowtwin:demo
PUERTO=8090
CLOUDFLARED=cloudflare/cloudflared:2026.10.7

# Pide la contraseña temporal hasta que tenga el largo que exige la API (12 a 128). Las demás reglas (no filtrada,
# sin el correo…) las revisa la API al arrancar; si alguna falla, el error lo dice y se cambia con «clave».
pedir_clave() {
  local c1 c2
  while true; do
    read -rsp "Contraseña temporal del Administrador (12 caracteres o más; puede ser una frase): " c1; echo >&2
    if [ ${#c1} -lt 12 ] || [ ${#c1} -gt 128 ]; then echo "Tiene ${#c1} caracteres: debe tener entre 12 y 128." >&2; continue; fi
    read -rsp "Repítela: " c2; echo >&2
    [ "$c1" = "$c2" ] && break
    echo "No coinciden. Otra vez." >&2
  done
  printf '%s' "$c1"
}

# Cambia solo la contraseña temporal en demo.env (sirve mientras el Administrador aún no se haya creado)
cambiar_clave() {
  [ -f "$CONFIG" ] || { echo "Aún no existe tools/demo/demo.env: usa «iniciar»."; exit 1; }
  local clave
  clave=$(pedir_clave)
  umask 077
  { grep -v '^AdminInicial__ClaveTemporal=' "$CONFIG"; printf 'AdminInicial__ClaveTemporal=%s\n' "$clave"; } > "$CONFIG.tmp"
  mv "$CONFIG.tmp" "$CONFIG"
  echo "Contraseña temporal actualizada. Ahora: $0 iniciar"
}

crear_config() {
  local sa
  sa=$(grep '^MSSQL_SA_PASSWORD=' "$RAIZ/backend/.env" | cut -d= -f2-) || { echo "Falta backend/.env (ver README)"; exit 1; }
  echo "Primera vez: se crea tools/demo/demo.env con claves nuevas."
  read -rp "Correo del Administrador de la demo: " correo
  clave=$(pedir_clave)
  umask 077
  cat > "$CONFIG" <<CONF
ConnectionStrings__FlowTwin=Server=127.0.0.1,1433;Database=FlowTwin_Demo;User Id=sa;Password=$sa;TrustServerCertificate=True;Encrypt=True
Jwt__Clave=$(openssl rand -base64 48 | tr -d '\n')
Auditoria__ClaveSello=$(openssl rand -base64 48 | tr -d '\n')
AdminInicial__Correo=$correo
AdminInicial__ClaveTemporal=$clave
BaseDatos__MigrarAlIniciar=true
Contrasenas__ExigirCambioPrimerIngreso=true
Demo__SimulacionDisponible=true
Mqtt__Activo=false
CONF
  echo "Listo. Guarda ese correo y esa contraseña: con ellos entras la primera vez."
}

# Si la app no arranca, muestra el motivo (la línea del error) en vez de mandar a buscarlo
no_arranco() {
  echo "FlowTwin no arrancó. Motivo:"
  docker logs flowtwin-demo 2>&1 | grep -E 'Exception|Faltan secretos' | tail -3 | sed 's/^/  /'
  if docker logs flowtwin-demo 2>&1 | grep -q 'AdminInicial:ClaveTemporal'; then
    echo; echo "La contraseña temporal no cumple la política. Cámbiala con:  $0 clave"
  fi
  docker rm -f flowtwin-demo >/dev/null 2>&1 || true
  exit 1
}

direccion() { docker logs flowtwin-tunel 2>&1 | grep -oE 'https://[a-z0-9-]+\.trycloudflare\.com' | tail -1; }

iniciar() {
  [ -f "$CONFIG" ] || crear_config
  docker inspect flowtwin-sqlserver --format '{{.State.Running}}' 2>/dev/null | grep -q true \
    || { echo "SQL Server no está encendido: cd backend && docker compose up -d"; exit 1; }

  echo "1/3 Compilando la imagen (la primera vez tarda unos minutos)…"
  docker build -q -t "$IMAGEN" "$RAIZ" >/dev/null

  echo "2/3 Arrancando FlowTwin en el puerto $PUERTO…"
  docker rm -f flowtwin-demo flowtwin-tunel >/dev/null 2>&1 || true
  docker run -d --name flowtwin-demo --network host --env-file "$CONFIG" -e PORT="$PUERTO" "$IMAGEN" >/dev/null
  for _ in $(seq 1 60); do curl -sf -o /dev/null "http://127.0.0.1:$PUERTO/api/v1/salud" && break; sleep 2; done
  curl -sf -o /dev/null "http://127.0.0.1:$PUERTO/api/v1/salud" || no_arranco

  echo "3/3 Abriendo el túnel HTTPS…"
  docker run -d --name flowtwin-tunel --network host "$CLOUDFLARED" tunnel --no-autoupdate --url "http://127.0.0.1:$PUERTO" >/dev/null
  for _ in $(seq 1 30); do [ -n "$(direccion)" ] && break; sleep 2; done
  [ -n "$(direccion)" ] || { echo "El túnel no dio una dirección. Mira: docker logs flowtwin-tunel"; exit 1; }

  echo
  echo "  FlowTwin está publicado en:  $(direccion)"
  echo
  echo "  Envía esa dirección a quien va a probarlo. Cambia cada vez que inicias el túnel."
  echo "  Al terminar:  ./tools/demo/demo-local.sh detener"
}

estado() {
  if docker inspect flowtwin-tunel >/dev/null 2>&1; then echo "Publicado en: $(direccion)"; else echo "No está iniciado."; fi
}

detener() {
  docker rm -f flowtwin-tunel flowtwin-demo >/dev/null 2>&1 || true
  echo "Detenido: la dirección pública ya no funciona. Los datos de la demo quedan en la base FlowTwin_Demo."
}

case "${1:-}" in
  iniciar) iniciar ;;
  estado) estado ;;
  detener) detener ;;
  clave) cambiar_clave ;;
  *) echo "Uso: $0 iniciar | estado | detener | clave"; exit 1 ;;
esac
