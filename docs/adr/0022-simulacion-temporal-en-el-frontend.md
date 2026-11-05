# ADR-22. Simulación en el frontend: temporal, separada y siempre a la vista

**Estado:** Aceptada · **Fecha:** 05/11/2026 · **Retiro previsto:** cuando los nodos reales envíen datos estables en la clínica

## Contexto

El Gemelo y el Historial del prototipo mostraban pacientes inventados (SimuladorService) sin ningún
aviso visible. Un gerente podía tomar una decisión con números falsos. Aun así, antes de que lleguen los
nodos hace falta mostrarle al patrocinador cómo se verá el sistema con datos.

## Decisión

- **Por defecto, solo datos reales.** Si una pantalla todavía no tiene datos, lo dice (estado vacío).
- **Un modo «Simulación»** que muestra datos ficticios generados en el navegador:
  - solo existe si el servidor lo permite: `Demo:SimulacionDisponible` (falso por defecto; verdadero solo
    en desarrollo y en la instalación de la demostración), expuesto en `GET /api/v1/configuracion`;
  - solo lo ven Administrador y Gerencia; nunca el Operador, cuya pantalla es operativa;
  - mientras está activo, una franja violeta fija y un marco violeta lo dejan claro en toda la pantalla;
  - no escribe nada en la base ni aparece en reportes;
  - no se recuerda: al recargar se vuelve a los datos reales.
- **El simulador de nodos del backend** (`tools/FlowTwin.Simulador`) es otra cosa: prueba la ingesta y el
  motor con datos reales de desarrollo, nunca contra la base de la clínica.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Seguir mostrando la simulación sin aviso | Datos falsos presentados como reales |
| Una versión aparte del frontend para la demo | Dos versiones que mantener y que se desalinean |
| Simular en el backend escribiendo en la base | Los datos ficticios se mezclarían con los reales en los reportes |

## Consecuencias

- ✅ Nadie confunde datos ficticios con reales.
- ✅ La demo al patrocinador es posible sin hardware.
- ⚠️ Deuda técnica con fecha: el retiro es un solo PR que borra el interruptor, el botón y
  `SimuladorService`/`datos-semilla.ts`. Mientras exista, cada pantalla nueva debe respetar el modo.
