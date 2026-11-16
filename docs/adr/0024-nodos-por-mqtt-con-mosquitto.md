# ADR-24. Los nodos envían sus datos por MQTT a un broker Mosquitto

**Estado:** Aceptada · **Fecha:** 15/11/2026 · **Reemplaza:** la ingesta HTTP (`POST /ingesta/lecturas` y `/ingesta/latidos`) del contrato v1.0

## Contexto

El alcance entregado al sponsor (Gestión del alcance v1.3, «Arquitectura Software») y su diagrama de
bloques comprometen un **«Backend modular desacoplado con broker MQTT pub/sub»** y un bloque **«Broker MQTT:
Mosquitto sobre red Wi-Fi dedicada (hosting de CREO+)»**. Hasta ahora los ESP32 enviaban sus lecturas por
HTTP a la API. Funcionaba, pero no era lo comprometido.

Cada nodo tiene su propia clave y en la base solo se guarda su hash (la clave no existe en ningún otro
lado). Cualquier broker debe autenticar a cada nodo **sin** que aparezca una segunda copia de las claves
que se pueda desincronizar.

## Decisión

1. **Broker Mosquitto 2.0** (versión fija en `docker-compose.yml`), sin acceso anónimo y con el plugin
   oficial **Dynamic Security**: las cuentas y permisos se cambian en caliente, sin reiniciar el broker.
2. **El backend administra las cuentas** con la cuenta de administrador del plugin (`Mqtt:ClaveAdmin`,
   secreto). La clave de un nodo solo se conoce al **registrarlo** o **regenerarla**: en ese momento se
   entrega al broker, que guarda su propio hash (PBKDF2 con sal). Al **dar de baja** se borra la cuenta.
   La base sigue siendo la fuente de verdad: al arrancar, la API borra las cuentas que no corresponden a un
   nodo activo y avisa de los nodos activos sin cuenta.
3. **Una cuenta y un rol por nodo**: solo puede publicar en `{prefijo}/v1/nodos/{mac}/lecturas` y
   `/latidos`, y solo puede suscribirse a `/respuestas`. Por defecto todo está cerrado. Un nodo no puede
   hacerse pasar por otro ni leer lo que otros envían.
4. **El backend es un suscriptor más** (módulo `FlowTwin.Mensajeria`): cuenta propia con clave aleatoria
   nueva en cada arranque, **sesión persistente** y **QoS 1**. Si la API se reinicia, el broker guarda los
   mensajes y los entrega al volver (verificado: 3 lotes publicados con la API apagada llegaron a la base).
5. **Confirmación de aplicación**: el backend contesta en `/respuestas` (`GUARDADO`, `DUPLICADO`,
   `INVALIDO`, `RECHAZADO`, `LIMITE`, `ERROR`). El nodo borra un lote de su búfer **solo** con `GUARDADO` o
   `DUPLICADO`, igual que antes con el `202`: el PUBACK del broker no significa que el lote se guardó.
6. **Mensajeria es transporte**: no sabe qué es una lectura. Atenciones (lecturas) y Dispositivos (latidos)
   registran su manejador; los servicios de ingesta y el motor de presencia no cambian. El ADR-21 sigue
   vigente: el broker transporta y `ope.LecturaBle` sigue siendo la cola del motor.
7. **Se quitan los endpoints HTTP de ingesta**: un solo camino, menos superficie de ataque.
8. **Prefijo por entorno** (`Mqtt:Prefijo`): `flowtwin` (trabajo), `flowtwin-sim` (simulador) y
   `flowtwin-pruebas` (pruebas de integración) comparten el broker de desarrollo sin verse entre sí.

## Alternativas

| Alternativa | Por qué no |
|---|---|
| Seguir con HTTP | No es lo comprometido con el sponsor |
| Broker dentro del backend (MQTTnet Server) | Una sola fuente de claves y menos piezas, pero el sponsor ya vio Mosquitto como bloque aparte; además, si la API se reinicia, el broker se reinicia con ella y no guarda mensajes |
| Mosquitto con archivo de contraseñas | Hay que reiniciar o recargar el broker en cada alta, y el archivo es otra copia de las claves que mantener a mano |
| Mosquitto con un plugin de autenticación por HTTP (mosquitto-go-auth) | Consultaría la base en cada conexión sin duplicar claves, pero es una imagen de terceros que hay que mantener; Dynamic Security viene en la imagen oficial |
| Un usuario del broker compartido por todos los nodos | Un nodo robado compromete a todos y no se puede revocar uno solo |

## Consecuencias

- ✅ Cumple el alcance: Mosquitto como broker pub/sub y nodos desacoplados del backend.
- ✅ Revocación inmediata: regenerar la clave o dar de baja desconecta al nodo en ese instante.
- ✅ No se pierden lecturas si la API se reinicia (sesión persistente, QoS 1) ni si el nodo pierde Wi-Fi
  (búfer de 10 minutos del contrato); la `secuencia` evita guardar dos veces.
- ✅ Más eficiente: una conexión abierta por nodo en vez de una petición HTTP cada 2 s.
- ⚠️ Una pieza más que operar: el contenedor de Mosquitto, con su volumen (`flowtwin-mqttdata`, que
  guarda las cuentas y los mensajes pendientes) y su respaldo.
- ⚠️ Si el broker se borra o se cambia (volumen perdido), las cuentas de los nodos reales no se pueden
  recrear solas porque sus claves no se guardan: el Administrador regenera la clave de cada nodo. La API
  lo avisa en el log al arrancar.
- ⚠️ Registrar o regenerar un nodo exige que el broker esté encendido (si no, 503 y no se cambia nada).
- ⚠️ Un solo proceso de API por broker: la cuenta de ingesta es única. Para varias instancias habría que
  usar suscripciones compartidas (`$share/…`). No hace falta en el piloto.
- ⚠️ En la clínica, el listener debe ser **TLS (8883)** con certificado y el firmware debe validarlo
  (contrato, sección 8). Pendiente del paso de producción.
