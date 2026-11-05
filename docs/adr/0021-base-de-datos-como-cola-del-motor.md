# ADR-21. La base de datos es la cola del motor de presencia

**Estado:** Aceptada · **Fecha:** 05/11/2026 · **Reemplaza:** la «cola en memoria» del flujo F-07 (documento v2)

## Contexto

El documento v2 dice que la ingesta guarda las lecturas en `ope.LecturaBle`, las deja en una cola en
memoria y el motor de presencia las toma de ahí. Si el servidor se reinicia con lecturas en la cola,
esas lecturas nunca llegan al motor: los eventos de zona de esos segundos se pierden. El ADR-08
reconstruye los eventos abiertos al arrancar, pero no las lecturas pendientes.

## Decisión

El motor lee directamente de `ope.LecturaBle`: cada `Presencia:IntervaloMs` (1 s) toma las lecturas con
`Id` mayor a la última procesada. No hay cola en memoria.

Al arrancar, o si una vuelta falla al guardar, el motor se reconstruye:

1. Carga los eventos de zona abiertos (dónde está cada paciente, ADR-08).
2. Reproduce las lecturas de los últimos `SinLecturaSalidaSegundos` (120 s) para recuperar las
   histéresis que estaban a medio camino.

Funciona porque el motor es **determinista**: usa la hora de las lecturas (`LeidaEn`), no la del reloj,
así que reproducir las mismas lecturas da las mismas decisiones.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Cola en memoria (v2) | Pierde lecturas al reiniciar |
| Cola en memoria + punto de control guardado | Dos piezas que mantener sincronizadas, para el mismo resultado |
| Broker de mensajes (RabbitMQ, Service Bus) | Otro servicio que instalar y operar en la clínica; desproporcionado para ~22 lecturas/s |

## Consecuencias

- ✅ Ninguna lectura se pierde: si el servidor se cae, siguen en la tabla.
- ✅ Una sola forma de recuperarse, para reinicios y para errores al guardar.
- ✅ Menos piezas: ingesta → tabla → motor.
- ⚠️ Hasta 1 s de retraso extra; la meta del documento es 5 s. Se mide con la métrica
  `flowtwin.presencia.retraso`.
- ⚠️ Una consulta por segundo a `ope.LecturaBle`, por clave primaria: costo despreciable.
- ⚠️ Un solo motor a la vez: con dos instancias de la API procesarían las mismas lecturas (el índice
  único `UX_EventoZona_AtencionAbierta` evita estadías duplicadas, pero sobrarían errores). Para el
  piloto hay un solo servidor; si algún día hay más, se agrega un candado en la base.
