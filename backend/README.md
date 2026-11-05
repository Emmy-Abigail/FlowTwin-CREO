# CREO FlowTwin · backend

ASP.NET Core 10 (monolito modular) + EF Core 10 + SQL Server. Arquitectura: ver el *Documento de arquitectura*.

## Puesta en marcha (desarrollo)

Todo se ejecuta dentro de `backend/`.

```bash
# 1. SQL Server Developer en Docker (la primera vez: cp .env.example .env y pon una contraseña)
docker-compose up -d
docker-compose ps                       # espera "(healthy)"

# 2. Secretos (solo la primera vez; se guardan fuera del repositorio, en ~/.microsoft/usersecrets)
SA=$(grep MSSQL_SA_PASSWORD .env | cut -d= -f2)
dotnet user-secrets set "ConnectionStrings:FlowTwin" \
  "Server=localhost,1433;Database=FlowTwin;User Id=sa;Password=$SA;TrustServerCertificate=True;Encrypt=True" \
  --project src/FlowTwin.Api
dotnet user-secrets set "Jwt:Clave" "$(openssl rand -base64 64 | tr -d '\n')" --project src/FlowTwin.Api

# 3. Herramientas locales (dotnet-ef)
dotnet tool restore

# 4. API en http://localhost:5080 (en desarrollo aplica las migraciones y siembra los datos de demo)
dotnet run --project src/FlowTwin.Api
```

- Documentación interactiva de la API: <http://localhost:5080/scalar>
- Peticiones de ejemplo: `src/FlowTwin.Api/FlowTwin.Api.http`
- Usuarios de demo, **solo en desarrollo** (contraseña `demo123`): `admin@creo.pe`, `operador@creo.pe`,
  `gerencia@creo.pe`, `nuevo@creo.pe`. Tarjetas `T-01` a `T-15`; `T-11` por desinfectar y `T-14` sin devolver.
- Nodos de demo, **solo en desarrollo**: `N-01` (Admisión), `N-02` (Sala de espera) y `N-03` (Módulo 2), con
  MAC `02:00:00:00:00:0n` y clave `nk_dev_N-0n`. En producción cada nodo se registra con `POST /api/v1/nodos`
  y su clave aleatoria se muestra una sola vez.

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

## Pruebas

```bash
dotnet test     # unitarias, de arquitectura y de integración (estas usan la base FlowTwin_Pruebas)
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
