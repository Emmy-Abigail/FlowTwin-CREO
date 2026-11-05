# CREO FlowTwin

[![CI](https://github.com/Emmy-Abigail/FlowTwin-CREO/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/Emmy-Abigail/FlowTwin-CREO/actions/workflows/ci.yml)

Gemelo digital del flujo de pacientes de la Clínica CREO+: ocupación, esperas y recorridos en tiempo
real a partir de emisores BLE y nodos ESP32, sin datos personales.

| Carpeta | Qué contiene | Cómo correrlo |
|---|---|---|
| [`backend/`](backend/README.md) | API .NET 10 (monolito modular) y SQL Server | `backend/README.md` |
| [`frontend/flowtwin-web/`](frontend/flowtwin-web/README.md) | SPA Angular con el gemelo 2D/3D | `frontend/flowtwin-web/README.md` |
| [`docs/contrato-integracion-nodos.md`](docs/contrato-integracion-nodos.md) | Contrato entre el firmware de los ESP32 y la API | Para el equipo de hardware |
| `.github/workflows/` | Integración continua | Corre sola en cada pull request |

## Cómo se trabaja

- **Ramas (GitFlow):** `main` solo recibe versiones entregadas desde `develop`; cada cambio se hace en
  `feature/…`, `fix/…` o `chore/…` y entra a `develop` por pull request.
- **Commits:** `tipo(modulo): descripción`, por ejemplo `feat(dispositivos): registro de nodos`.
- **Calidad:** un pull request se une solo si la CI está en verde (compilación, pruebas unitarias,
  de arquitectura y de integración contra SQL Server, y compilación de producción del frontend).
- **Secretos:** nunca en el repositorio. En desarrollo, `dotnet user-secrets` y `backend/.env`; en el
  servidor, variables de entorno.
