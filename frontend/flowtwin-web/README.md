# CREO FlowTwin · frontend (flowtwin-web)

Gemelo digital del flujo de pacientes de la Clínica CREO+. Angular 22 (standalone, Signals, SCSS) y Three.js.
Login, usuarios y Admisión usan la API real (`backend/`). El Gemelo y el Historial siguen simulados hasta el Avance 2.

## Cómo correrlo

```bash
nvm use 24.15.0     # Angular 22 pide Node 24.15 o superior
npm install         # solo la primera vez
npx ng serve        # abre http://localhost:4200
```

Necesita la API corriendo en http://localhost:5080 (ver `backend/README.md`): `ng serve` le reenvía
todo lo que empieza con `/api` mediante `proxy.conf.json`.

En desarrollo, la API siembra usuarios de prueba (contraseña `demo123`): `operador@creo.pe`,
`gerencia@creo.pe` y `admin@creo.pe`. El login no tiene accesos rápidos: se escribe el correo y la
contraseña igual que en producción.

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
