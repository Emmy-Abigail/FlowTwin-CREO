# CREO FlowTwin · frontend (flowtwin-web)

Gemelo digital del flujo de pacientes de la Clínica CREO+. Angular 22 (standalone, Signals, SCSS) y Three.js.
**Todos los datos son simulados en el navegador**: aún no hay backend.

## Cómo correrlo

```bash
nvm use 24.15.0     # Angular 22 pide Node 24.15 o superior
npm install         # solo la primera vez
npx ng serve        # abre http://localhost:4200
```

Usuarios demo (contraseña `demo123`): `operador@creo.pe`, `gerencia@creo.pe`, `admin@creo.pe`
y `nuevo@creo.pe` (primer ingreso: obliga a cambiar la contraseña).

## Estructura

```
src/app/
  core/            auth (login simulado, guards por rol), layout (barra lateral y superior),
                   datos (ClinicaStore, SimuladorService, datos semilla, reglas de cálculo)
  shared/          componentes reutilizables: KPI, chip, tabla, modal, botón, ícono, logo
  features/        una carpeta por pantalla: auth (P1-P2), admision (P3), gemelo (P4), historial (P5)
  gemelo-render/   render-3d: escena Three.js de solo presentación · geometria.ts: tipos del JSON
public/pisos/      geometría real del Piso 1 (AutoCAD A-07) y la imagen del plano
```

Para conectar el backend: reemplazar `SimuladorService` por un servicio que escuche SignalR y
llame a los mismos métodos de `ClinicaStore`, y cambiar la URL de `GeometriaService` por la API.
