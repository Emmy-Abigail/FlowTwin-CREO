# Contrato de integración: ESP32 ↔ FlowTwin (MQTT)

**Versión 2.0 · 15/11/2026** · Software: Emmy López · Hardware: equipo de nodos

Este documento es el acuerdo entre el firmware de los ESP32 y el servidor. Si el firmware cumple lo que
dice aquí, el sistema funciona; no hace falta conocer el backend. **Cualquier cambio se conversa entre
ambos equipos y se hace con un pull request a este archivo**, junto con el código que lo implementa.

```
 Emisor (ESP32 iBeacon) ──BLE──► Nodo (ESP32 receptor) ──Wi-Fi / MQTT──► Broker Mosquitto ──► Backend FlowTwin ──► Gemelo
 lo lleva el paciente            fijo en una zona                         (red de CREO+)      suscrito a los nodos
```

**Qué cambió desde la v1.0** (ADR-24): los nodos ya no envían por HTTP, publican en un broker MQTT.
El contenido de los mensajes es el mismo JSON, sin el campo `mac` (ahora va en el tema). La respuesta del
servidor llega por un tema de respuestas en vez de un código HTTP.

## 1. Emisor (lo lleva el paciente)

Un ESP32 que **solo anuncia** un iBeacon. No se conecta a Wi-Fi ni al servidor.

| Campo | Valor | Notas |
|---|---|---|
| Formato | iBeacon (Apple), anuncio **no conectable** | |
| UUID | `b14c70f2-314c-41eb-b11a-93fa9c7ace90` | Propio de CREO+. **Igual en todos los emisores** |
| Major | Número de sede: `1` = Sede Principal | 1 a 65535 |
| Minor | Número del emisor: T-07 → `7` | 1 a 65535, único dentro de la sede |
| Intervalo de anuncio | **300 ms** (máximo 500 ms) | Más lento = el sistema tarda más en ubicarlo |
| Measured power | RSSI medido a 1 m del emisor | Calibrar con un nodo; típico −59 dBm |
| Dirección BLE | Puede ser aleatoria | El sistema identifica por UUID + major + minor, nunca por la MAC del emisor |

UUID, major y minor **no cambian nunca** en un emisor. Cada uno lleva su QR impreso con el texto `CREO-T07`.

## 2. Nodo (receptor fijo en una zona)

1. Escanea BLE de forma continua y **descarta todo anuncio cuyo UUID no sea el de la clínica**.
2. Cada **2 segundos** arma un lote: por cada emisor visto, el **RSSI promedio** del período y **hace
   cuántos milisegundos lo vio por última vez** (`edadMs`).
3. Publica el lote en su tema de lecturas (sección 5) y espera la respuesta (sección 7). Si no vio a
   nadie, puede no publicar nada.
4. Cada **30 segundos** publica un latido (sección 6), aunque no haya visto emisores.
5. **No necesita la hora real** (no hace falta NTP): el servidor calcula la hora de cada lectura con su
   propio reloj, como `hora de llegada − edadMs`.

**Configuración guardada en NVS** (se carga por puerto serie o con un portal la primera vez, nunca en el
código fuente ni en el repositorio): SSID y contraseña del Wi-Fi, servidor y puerto del broker, usuario
y clave del nodo, y el certificado raíz de la clínica (sección 9).

## 3. Identificación, usuario y clave

| Dato | Formato | Ejemplo |
|---|---|---|
| MAC del nodo | MAC **Wi-Fi STA** del ESP32 (`esp_read_mac(…, ESP_MAC_WIFI_STA)`) | `A4:CF:12:9B:3E:03` |
| `{mac}` en los temas | La MAC en **minúsculas y sin separadores** | `a4cf129b3e03` |
| Usuario MQTT | `{prefijo}-{mac}`. En la clínica el prefijo es `flowtwin` | `flowtwin-a4cf129b3e03` |
| Clave del nodo | Texto que empieza con `nk_`; es la contraseña MQTT | `nk_Zt81…` |
| Identificador de emisor (`uuidBle`) | `{uuid}-{major}-{minor}` en minúsculas, **números sin ceros a la izquierda** | `b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7` |

En C: `snprintf(id, sizeof id, "%s-%u-%u", UUID_CLINICA, major, minor);`

**Alta de un nodo:** el Administrador lo registra en el sistema con su código (`N-04`), su MAC y su zona.
El sistema muestra **una sola vez** la clave, el usuario y los temas; se cargan en el NVS del nodo. Si la
clave se pierde o se filtra, el Administrador genera otra: **la anterior deja de funcionar al instante** y
el broker cierra la conexión que la usaba. Si el nodo se da de baja, su cuenta se borra.

## 4. Conexión al broker

| Parámetro | Valor |
|---|---|
| Protocolo | **MQTT 3.1.1** (el de la librería PubSubClient o `esp-mqtt`) |
| Servidor | En la clínica: el servidor de CREO+, **puerto 8883 con TLS** (sección 9). En pruebas: puerto 1883 |
| Client ID | El usuario MQTT (`flowtwin-a4cf129b3e03`) |
| Usuario / contraseña | Los de la sección 3 |
| Clean session | `true` |
| Keep alive | 30 s |
| QoS de todo lo que publica | **1** (al menos una vez) |
| Suscripción | `{prefijo}/v1/nodos/{mac}/respuestas`, QoS 1 |

Si el broker rechaza la conexión (**«not authorised»**), la clave o el usuario no son válidos o el nodo
está de baja: **dejar de intentar seguido** y señalarlo (LED). Reintentar cada 60 s por si se corrigió.

El nodo **solo** puede publicar en sus dos temas y leer el suyo de respuestas: el broker descarta en
silencio cualquier otra cosa.

## 5. Publicar lecturas

Tema: `flowtwin/v1/nodos/a4cf129b3e03/lecturas` · QoS 1

```json
{
  "secuencia": 18342,
  "lecturas": [
    { "uuidBle": "b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7",  "rssi": -61, "edadMs": 350 },
    { "uuidBle": "b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-11", "rssi": -78, "edadMs": 1200 }
  ]
}
```

| Campo | Tipo | Rango | Qué es |
|---|---|---|---|
| `secuencia` | entero ≥ 0 | 0 a 2³²−1 | Contador del nodo: empieza en 0 al encender y sube 1 por lote. **Un reenvío usa la misma secuencia** |
| `lecturas` | lista | 0 a 200 elementos | Una por emisor visto en el período |
| `uuidBle` | texto | hasta 64 caracteres | Identificador del emisor (sección 3) |
| `rssi` | entero | −127 a 0 dBm | Promedio del período, **sin corregir** (la calibración la aplica el servidor) |
| `edadMs` | entero | 0 a 600 000 | Milisegundos desde que lo vio por última vez **hasta el momento del envío** |

## 6. Publicar latidos

Tema: `flowtwin/v1/nodos/a4cf129b3e03/latidos` · QoS 1

```json
{ "versionFirmware": "1.0.0", "rssiWifi": -58, "memoriaLibre": 142000 }
```

| Campo | Obligatorio | Rango |
|---|---|---|
| `versionFirmware` | No | Hasta 20 caracteres |
| `rssiWifi` | No | −127 a 0 dBm |
| `memoriaLibre` | No | Bytes, ≥ 0 |

Con el primer latido el nodo aparece **En línea**; si pasan **120 s** sin latidos, pasa a **Sin señal** y su
zona se ve gris en el gemelo.

## 7. Respuestas del servidor

Tema: `flowtwin/v1/nodos/a4cf129b3e03/respuestas`. Por cada mensaje, el servidor contesta:

```json
{ "tipo": "lecturas", "estado": "GUARDADO", "secuencia": 18342 }
{ "tipo": "lecturas", "estado": "INVALIDO", "secuencia": 18343, "errores": { "lecturas[0].Rssi": ["El RSSI va de -127 a 0 dBm."] } }
{ "tipo": "latidos",  "estado": "GUARDADO" }
```

| `estado` | Significa | El nodo debe |
|---|---|---|
| `GUARDADO` | Se guardó | Borrar el lote de esa `secuencia` del búfer |
| `DUPLICADO` | Ese lote ya había llegado (un reenvío) | Igual que `GUARDADO`: es un éxito |
| `INVALIDO` | El JSON no cumple este contrato | **No reintentar** (fallaría igual). Registrar los `errores` y descartar el lote. Es un error de firmware que hay que corregir |
| `RECHAZADO` | El servidor no reconoce al nodo (de baja) | Dejar de enviar y señalarlo (LED) |
| `LIMITE` | Envía demasiados mensajes | Esperar 10 s y reintentar |
| `ERROR` | Falló el servidor | Conservar el lote y reintentar (sección 8) |

**Importante:** el PUBACK del broker (QoS 1) solo dice que el broker recibió el mensaje, **no que se
guardó**. Un lote se borra del búfer solo con `GUARDADO` o `DUPLICADO`. Si no llega respuesta en **5 s**,
se trata como `ERROR`: se reintenta con la misma `secuencia` y el servidor no lo guarda dos veces.

## 8. Búfer y reenvío sin Wi-Fi o sin broker

- Guardar hasta **10 minutos** de lotes. Si se llena, se descarta **el más antiguo**.
- Reconectar con espera creciente: 2, 4, 8, 16 y luego cada 30 s.
- Al reenviar, **mantener la `secuencia` original** (así el servidor detecta lo que ya recibió) y
  **recalcular `edadMs`** como `ahora − momento en que se vio`. Si supera 600 000, esa lectura se descarta:
  si se enviara la edad original, el servidor la fecharía en el momento del reenvío y el tiempo de espera
  del paciente saldría mal.
- Enviar los lotes pendientes **en orden**, del más antiguo al más nuevo.

## 9. Seguridad

- La clave va **solo** en el NVS y como contraseña de la conexión MQTT. Nunca en el código fuente, en el
  repositorio ni en el log serie.
- **En la clínica la conexión es TLS (puerto 8883)** con un certificado interno: el firmware debe llevar el
  certificado raíz de la clínica y validarlo (`cert_pem` en `esp-mqtt`, `setCACert` en `WiFiClientSecure`).
  **No desactivar la validación**: con ella apagada, cualquiera en la red podría hacerse pasar por el
  broker y robar las claves.
- En desarrollo (red local de pruebas) se usa el puerto 1883 sin TLS.

## 10. Límites del servidor

| Límite | Valor |
|---|---|
| Mensajes por nodo | 300 por minuto (un nodo normal publica ~32) |
| Tamaño de un mensaje | 64 KB |
| Lecturas por lote | 200 |
| Antigüedad de una lectura (`edadMs`) | 10 minutos |
| Latido máximo antes de Sin señal | 120 s |

## 11. Probar contra la laptop de desarrollo

1. En la laptop: `docker-compose up -d` dentro de `backend/` (SQL Server y Mosquitto) y la API
   (`dotnet run --project src/FlowTwin.Api`). El broker escucha en el puerto **1883** de la laptop.
2. Averiguar la IP de la laptop en la red (`hostname -I`): el servidor del broker es esa IP.
3. Registrar el ESP32 real (su MAC) como nodo desde `http://localhost:5080/scalar` → `POST /api/v1/nodos`
   con la sesión del Administrador. La respuesta trae la **clave** y el bloque **`mqtt`** con el usuario y
   los temas: cargarlos en el NVS del nodo.
4. Ver en la consola de la API «Nodo N-0x en línea» con el primer latido.

Probar sin ESP32, desde la misma laptop, como el nodo de demostración N-01 (usuario y clave solo existen en
desarrollo). En una terminal, escuchar las respuestas:

```bash
docker exec flowtwin-mosquitto mosquitto_sub -h localhost -u flowtwin-020000000001 -P nk_dev_N-01 -q 1 \
  -t flowtwin/v1/nodos/020000000001/respuestas -v
```

En otra, publicar un latido y un lote:

```bash
docker exec flowtwin-mosquitto mosquitto_pub -h localhost -u flowtwin-020000000001 -P nk_dev_N-01 -q 1 \
  -t flowtwin/v1/nodos/020000000001/latidos -m '{"versionFirmware":"1.0.0"}'
docker exec flowtwin-mosquitto mosquitto_pub -h localhost -u flowtwin-020000000001 -P nk_dev_N-01 -q 1 \
  -t flowtwin/v1/nodos/020000000001/lecturas \
  -m '{"secuencia":1,"lecturas":[{"uuidBle":"b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7","rssi":-61,"edadMs":350}]}'
```

La primera terminal muestra `{"tipo":"latidos","estado":"GUARDADO"}` y
`{"tipo":"lecturas","estado":"GUARDADO","secuencia":1}`. Con una clave equivocada, `mosquitto_pub` responde
`Connection Refused: not authorised`.

## 12. Lista de verificación de la primera prueba

- [ ] El emisor aparece en una app como *nRF Connect* con el UUID, major y minor correctos.
- [ ] El nodo ignora un beacon con otro UUID (por ejemplo, un celular emitiendo otro iBeacon).
- [ ] El nodo se conecta al broker y el primer latido recibe `GUARDADO`; el nodo aparece **En línea**.
- [ ] Un lote recibe `GUARDADO` con su `secuencia`, y el nodo lo borra de su búfer.
- [ ] Con una clave equivocada el broker responde «not authorised» y el nodo deja de insistir cada 2 s.
- [ ] Regenerar la clave desde el sistema desconecta al nodo; con la clave nueva vuelve a entrar.
- [ ] Desenchufar el router 3 minutos: al volver, los lotes guardados llegan, sin duplicados y con la
      `edadMs` recalculada.
- [ ] Apagar el nodo: a los 120 s aparece **Sin señal**.
