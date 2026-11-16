# ADR-25. Alertas del Operador, cierre de jornada y zona «fuera de servicio» en vez de P7

**Estado:** Aceptada · **Fecha:** 15/11/2026 · **Cambia:** P7 Seguimiento, RN-07 y el efecto de RN-18 en las métricas (documento de arquitectura v2) · **Requiere:** solicitud de cambio de RF09 en el alcance (v1.4)

## Contexto

La pantalla P7 (switches para pausar zonas y lista de nodos) se revisó contra la práctica de sistemas RTLS
de flujo de pacientes en clínicas ambulatorias (CenTrak, Midmark, casos publicados). En esos sistemas la
recepción: ve el estado de cada sala **sobre el mismo plano** (disponible, ocupada, fuera de servicio),
recibe **alertas de espera** (umbrales típicos de 20, 40 y 60 min) y controla las **tarjetas no devueltas**.
No usan una pantalla aparte para «pausar el seguimiento». Además, P7 repetía lo que ya muestran el gemelo
y la pantalla de Dispositivos. La clínica ya no responde consultas, así que se decide con esta evidencia.

Al revisar RN-07 (cierre automático a los 60 min sin lecturas) apareció un riesgo: en el piloto solo hay
3 nodos y varias zonas sin cobertura (Triaje, Tópicos…). Un paciente que sigue en la clínica, en una zona
sin nodo, no se detecta: un cierre automático cerraría atenciones de pacientes que no se fueron.

## Decisión

1. **Sin P7.** El Operador tiene Admisión, Gemelo e Historial.
2. **RF09 como «Fuera de servicio»**: un interruptor en la ventana de cada zona del gemelo (Operador y
   Administrador), con motivo (catálogo MotivoPausa). La zona se ve gris con «Fuera de servicio». Tabla
   `ope.PausaZona`, una abierta por zona (RN-18, índice único filtrado). El switch «por piso» no hace
   falta: cuando la clínica cierra, lo resuelve el cierre de jornada.
3. **No se descuentan minutos de las métricas** (cambia RN-18/RN-15): los pacientes se siguen detectando y
   esas detecciones son reales. El historial **marca** las horas fuera de servicio para explicar por qué
   hubo menos gente.
4. **Alertas calculadas del estado actual** (no se guardan en `ope.Alerta`): espera larga (20/40/60 min),
   sin lecturas por 30 min (`TARJETA_SIN_DEVOLVER`) y nodo sin señal. Desaparecen solas al resolverse; no
   hay que «marcarlas como leídas». Las ven Operador y Administrador. Suenan al aparecer (Web Audio, con
   opción de silenciar) y animan la campana.
5. **RN-07 cambia**: a los 30 min sin lecturas hay una alerta y **el Operador decide** «No devolvió la
   tarjeta». No hay cierre automático por falta de lecturas mientras haya zonas sin nodo.
6. **Cierre de jornada (RN-08)** a la hora `Jornada:HoraCierre` (por defecto 22:00): atenciones abiertas →
   `CIERRE_JORNADA`, sus tarjetas → Sin devolver, zonas → en servicio. Idempotente; recupera un cierre
   perdido si el servidor estuvo apagado.
7. Una atención cerrada sin devolución **termina cuando se vio al paciente por última vez**, no a la hora
   del cierre, para no inflar el tiempo total. El historial ya muestra estos cierres en ámbar (aproximados).
8. Umbrales y hora de cierre son **valores por defecto configurables** (`Alertas`, `Jornada`), marcados
   como tales: la clínica no definió los suyos.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| P7 tal como estaba | Repite el gemelo y Dispositivos; pausar es una tarea rara y un olvido esconde datos |
| Descontar de las métricas el tiempo fuera de servicio (RN-18 original) | Esconde detecciones reales; durante una limpieza no hay pacientes, así que casi no corrige nada |
| Mantener RN-07 automático a los 60 min | Cerraría atenciones de pacientes en zonas sin nodo |
| Guardar las alertas en `ope.Alerta` con «leída» | Trabajo extra que nadie hace y alertas que quedan después de resolverse |

## Consecuencias

- ✅ Cada pantalla del Operador tiene un propósito distinto; las alertas llegan a cualquier pantalla.
- ✅ Ningún cierre automático sobre información incompleta durante la jornada.
- ⚠️ Hay que registrar el cambio de RF09 en el alcance (v1.4) con esta investigación como justificación.
- ⚠️ Cuando todas las zonas tengan nodo, se puede reactivar el cierre automático por falta de lecturas.
- ⚠️ El historial de alertas no se guarda: si se necesita para auditoría, se agrega después.
