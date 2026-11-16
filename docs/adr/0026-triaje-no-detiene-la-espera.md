# ADR-26. El triaje no detiene la espera

**Estado:** Aceptada · **Fecha:** 15/11/2026 · **Precisa:** RN-06 (documento de arquitectura v2)

## Contexto

RN-06 define la espera como el tiempo desde la entrega de la tarjeta hasta la primera entrada a una zona
**Servicio**. Triaje estaba registrado como Servicio, así que la espera se «detenía» en el triaje: si el
paciente volvía luego a la sala de espera a esperar al médico, ese tiempo no contaba como espera en ninguna
parte. Además, la simulación usaba una lista propia de zonas de consulta sin Triaje: la misma pantalla
mostraba dos reglas distintas.

La métrica de referencia en salud es el tiempo **desde la llegada hasta la atención del profesional**
(«door-to-provider»), sumando todos los tramos. El triaje es una evaluación previa, no esa atención.

## Decisión

- Nuevo tipo de zona **TRIAJE** (catálogo `cat.TipoZona`). Tiene aforo y semáforo (RN-13), pero **no
  detiene la espera ni suma a la consulta**. La zona Triaje del Piso 1 pasa a ese tipo.
- RN-06 no cambia de fórmula: espera hasta la primera zona Servicio; consulta = tiempo en zonas Servicio.
- En el detalle (P6), Triaje va en el carril «Admisión y triaje».

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Triaje como Servicio | Esconde la espera posterior al triaje |
| Contar solo el último tramo en la sala | Pierde los tramos anteriores: la espera saldría más corta de lo que fue |
| Triaje como Espera | Mezclaría «estar esperando» con «ser evaluado» en el carril y en la ocupación |

## Consecuencias

- ✅ Una sola regla en el sistema real, la simulación, el gemelo, P5 y P6.
- ⚠️ Las esperas que ya se midieron con Triaje como Servicio no se recalculan solas en un reporte viejo
  exportado; en el sistema se recalculan al consultar, porque los tiempos no se guardan.
