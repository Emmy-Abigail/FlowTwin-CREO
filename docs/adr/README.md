# Decisiones de arquitectura (ADR)

Cada decisión importante queda en un archivo corto: **contexto, decisión, alternativas y consecuencias**.
Se escriben en el mismo pull request que el código que las aplica, así la documentación nunca queda
atrás. Los ADR-01 a ADR-20 están en el documento de arquitectura (v2); desde el ADR-21 viven aquí.

| N.º | Decisión | Estado |
|---|---|---|
| [ADR-21](0021-base-de-datos-como-cola-del-motor.md) | La base de datos es la cola del motor de presencia | Aceptada |
| [ADR-22](0022-simulacion-temporal-en-el-frontend.md) | Simulación en el frontend: temporal, separada y siempre a la vista | Aceptada (temporal) |
| [ADR-23](0023-aforo-por-definir.md) | El aforo de una zona puede quedar «por definir» | Aceptada |
| [ADR-24](0024-nodos-por-mqtt-con-mosquitto.md) | Los nodos envían sus datos por MQTT a un broker Mosquitto | Aceptada |
| [ADR-25](0025-alertas-y-zona-fuera-de-servicio.md) | Alertas del Operador, cierre de jornada y zona «fuera de servicio» en vez de P7 | Aceptada |
| [ADR-26](0026-triaje-no-detiene-la-espera.md) | El triaje no detiene la espera | Aceptada |
| [ADR-27](0027-configuracion-sin-editor-de-planos.md) | Configuración por datos en vez de «Construcción del gemelo»; aforo solo donde la gente se acumula | Aceptada |
