# CREO FlowTwin · backend

ASP.NET Core 10 (monolito modular) + EF Core 10 + SQL Server + broker MQTT Mosquitto para los nodos
(ADR-24). Arquitectura: ver el *Documento de arquitectura* y `docs/adr/`.

## Puesta en marcha (desarrollo)

Los pasos (Docker, secretos, API y web) están en el [README principal](../README.md#-cómo-correrlo). Aquí va el
detalle técnico que solo necesita quien trabaja en el backend.

- Documentación interactiva de la API: <http://localhost:5080/scalar>
- Peticiones de ejemplo: `src/FlowTwin.Api/FlowTwin.Api.http`
- Usuarios de demo, **solo en desarrollo** (contraseña `demo123`): `admin@creo.pe`, `operador@creo.pe`,
  `gerencia@creo.pe`, `nuevo@creo.pe`. Tarjetas `T-01` a `T-15`; `T-11` por desinfectar y `T-14` sin devolver.
- Nodos de demo, **solo en desarrollo**: `N-01` (Admisión), `N-02` (Sala de espera) y `N-03` (Módulo 2), con
  MAC `02:00:00:00:00:0n` y clave `nk_dev_N-0n`. Al arrancar, la API les crea su cuenta en el broker. En
  producción cada nodo se registra con `POST /api/v1/nodos`: la respuesta trae su clave (una sola vez) y su
  usuario y temas MQTT.

## Nodos por MQTT (ADR-24)

Los ESP32 publican en Mosquitto (`localhost:1883` en desarrollo); la API está suscrita y guarda lo que
llega. El contrato completo está en `docs/contrato-integracion-nodos.md`.

- **Cuentas:** cada nodo tiene la suya y solo puede publicar en sus temas. La API las crea, cambia y borra
  al registrar, regenerar la clave o dar de baja un nodo, con la cuenta de administrador de Mosquitto
  (`Mqtt:ClaveAdmin`). Por eso registrar un nodo exige que Mosquitto esté encendido (si no, responde 503).
- **Si Mosquitto se recrea desde cero** (se borró el volumen `flowtwin-mqttdata`): los nodos de demo se
  recrean solos; los reales no, porque su clave no se guarda en ningún lado. La API avisa en el log
  «regenera su clave en Dispositivos».
- **Prefijos por entorno:** la API de trabajo usa `flowtwin`, la del simulador `flowtwin-sim` y las pruebas
  `flowtwin-pruebas`: comparten el broker sin verse entre sí.
- **En la clínica:** listener TLS en el 8883 con el certificado interno, `Mqtt__UsarTls=true`,
  `Mqtt__Puerto=8883` y `Mqtt__ClaveAdmin` como variable de entorno. El firmware valida el certificado.

## Administrador inicial (instalación en la clínica)

Fuera de desarrollo no se siembran usuarios de demo. Al arrancar, si no hay ningún Administrador activo,
la API crea uno con la sección `AdminInicial` (si falta, no arranca y explica qué configurar).
El correo y la contraseña nunca van en el código: en el servidor se definen como variables de entorno.

```bash
AdminInicial__Correo=ti@creo.pe
AdminInicial__ClaveTemporal='...'     # debe cumplir la política de contraseñas
```

Ese administrador crea luego a los demás usuarios desde la pantalla de Usuarios.

## Cambio de contraseña en el primer ingreso (F-02)

Por ahora está **apagado**: `Contrasenas:ExigirCambioPrimerIngreso = false` en `appsettings.json`.
Al ponerlo en `true`, quien entra con una contraseña temporal debe cambiarla antes de usar el sistema.
Las pruebas de integración verifican la regla encendida y apagada.

## Simulador de nodos

Hace de recepción y de los 3 ESP32 de desarrollo: entrega tarjetas por la API, mueve pacientes por
Admisión → Sala de espera → Módulo 2 y publica lecturas y latidos en Mosquitto, igual que el firmware real.

Lo que escribe parece real, así que **nunca va a la base de trabajo** (`FlowTwin`, API en el 5080):
usa su propia API en el puerto 5090 con la base `FlowTwin_Simulador`, que se crea sola la primera vez.
Cada paso en su terminal:

```bash
dotnet run --project src/FlowTwin.Api --launch-profile simulador                 # 1. API del simulador (5090)
dotnet run --project tools/FlowTwin.Simulador                                  # 2. día normal (escenarios/piloto.json)
dotnet run --project tools/FlowTwin.Simulador -- --escenario escenarios/demo.json   #    o tiempos cortos para una demo
cd ../frontend/flowtwin-web && npm run start:simulador                          # 3. verlo en http://127.0.0.1:4300
```

- La pantalla del simulador va en `127.0.0.1` y no en `localhost` a propósito: el navegador separa las
  cookies por nombre de sitio, no por puerto, y así cada ventana conserva su propia sesión.
- Si el simulador apunta al 5080 o a los temas MQTT de trabajo (prefijo `flowtwin`) se niega a arrancar;
  para escribir en la base de trabajo a propósito hay que agregar `--en-base-de-trabajo`.
- `Ctrl+C` lo detiene y devuelve las tarjetas de los pacientes simulados. Los tiempos, nodos y ruido de la
  señal se cambian en `tools/FlowTwin.Simulador/escenarios/*.json`.
- Para volver una base de desarrollo al estado de la semilla (sin atenciones, lecturas ni movimientos):
  `tools/limpiar-datos-desarrollo.sql` (ver el encabezado del archivo).

## Pruebas

```bash
dotnet test     # unitarias, de arquitectura y de integración (base FlowTwin_Pruebas y Mosquitto encendido)
```

## Migraciones

```bash
dotnet ef migrations add NombreDelCambio -p src/FlowTwin.Persistence -s src/FlowTwin.Api -o Migraciones
dotnet ef database update -p src/FlowTwin.Persistence -s src/FlowTwin.Api
```

## Estructura

```
src/
  FlowTwin.Api/            Program.cs, seguridad, errores, datos de demo
  FlowTwin.SharedKernel/   columnas de registro (ADR-18), errores, catálogos, IAuditoria
  FlowTwin.Persistence/    DbContext, interceptor de registro, configuración de tablas, migraciones
  Modules/                 Identidad, Gemelo, Dispositivos, Atenciones, Monitoreo, Reportes, Auditoria
                           cada uno con Domain/ Application/ Endpoints/ Contracts/
tests/                     UnitTests, IntegrationTests, ArchitectureTests
```
