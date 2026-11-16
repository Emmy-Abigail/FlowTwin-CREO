#!/usr/bin/env bash
# Escaneo OWASP ZAP de la API con sesión de Administrador (informe de seguridad, docs/seguridad).
# ÚSALO SOLO contra una base desechable: el escáner envía ataques reales que crean, editan y desactivan datos.
#
#   API=http://127.0.0.1:5091 CORREO=admin@creo.pe CLAVE=demo123 ./tools/seguridad/zap.sh salida/
#
# La API debe estar en Development (publica /openapi/v1.json) y con límites altos para que el escáner llegue a
# todo (ver docs/seguridad/informe-seguridad.md, «Cómo repetir»).
set -euo pipefail
API="${API:?Falta API, por ejemplo http://127.0.0.1:5091}"
CORREO="${CORREO:?Falta CORREO}"
CLAVE="${CLAVE:?Falta CLAVE}"
SALIDA="${1:-zap-salida}"
mkdir -p "$SALIDA" && chmod 777 "$SALIDA"

TOKEN=$(curl -sf -X POST "$API/api/v1/auth/login" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"clave\":\"$CLAVE\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')

docker run --rm --network host -v "$(realpath "$SALIDA")":/zap/wrk:rw ghcr.io/zaproxy/zaproxy:stable \
  zap-api-scan.py -t "$API/openapi/v1.json" -f openapi -r zap-informe.html -J zap-informe.json -I \
  -z "-config scanner.maxScanDurationInMins=12 \
      -config replacer.full_list(0).description=sesion -config replacer.full_list(0).enabled=true \
      -config replacer.full_list(0).matchtype=REQ_HEADER -config replacer.full_list(0).matchstr=Authorization \
      -config replacer.full_list(0).regex=false -config replacer.full_list(0).replacement=Bearer\ $TOKEN" \
  | tee "$SALIDA/salida.txt" | grep -E "^(WARN|FAIL)|FAIL-NEW|Total of"
echo "Informe: $SALIDA/zap-informe.html"
