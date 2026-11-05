# Contrato de integración: ESP32 ↔ API de FlowTwin

**Versión 1.0 · 05/11/2026** · Software: Emmy López · Hardware: equipo de nodos

Este documento es el acuerdo entre el firmware de los ESP32 y el servidor. Si el firmware cumple lo que
dice aquí, el sistema funciona; no hace falta conocer el backend. **Cualquier cambio se conversa entre
ambos equipos y se hace con un pull request a este archivo**, junto con el código que lo implementa.

```
 Emisor (ESP32 iBeacon)  ──BLE──►  Nodo (ESP32 receptor)  ──Wi-Fi / HTTP──►  API FlowTwin  ──►  Gemelo
 lo lleva el paciente              fijo en una zona                          servidor de la clínica
```

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
3. Envía el lote a la API (sección 4). Si no vio a nadie, puede no enviar nada.
4. Cada **30 segundos** envía un latido (sección 5), aunque no haya visto emisores.
5. **No necesita la hora real** (no hace falta NTP): el servidor calcula la hora de cada lectura con su
   propio reloj, como `hora de llegada − edadMs`.

**Configuración guardada en NVS** (se carga por puerto serie o con un portal la primera vez, nunca en el
código fuente ni en el repositorio): SSID y contraseña del Wi-Fi, URL base del servidor y clave del nodo.

## 3. Identificación y clave

| Dato | Formato | Ejemplo |
|---|---|---|
| MAC del nodo | MAC **Wi-Fi STA** del ESP32 (`esp_read_mac(…, ESP_MAC_WIFI_STA)`), mayúsculas con `:` | `A4:CF:12:9B:3E:03` |
| Identificador de emisor (`uuidBle`) | `{uuid}-{major}-{minor}` en minúsculas, **números sin ceros a la izquierda** | `b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7` |
| Clave del nodo | Texto que empieza con `nk_`; se envía en la cabecera `X-Node-Key` | `nk_Zt81…` |

En C: `snprintf(id, sizeof id, "%s-%u-%u", UUID_CLINICA, major, minor);`

**Alta de un nodo:** el Administrador lo registra en el sistema con su código (`N-04`), su MAC y su zona.
El sistema muestra la clave **una sola vez**; se carga en el NVS del nodo. Si se pierde o se filtra, el
Administrador genera otra y la anterior deja de funcionar al instante.

## 4. Enviar lecturas

```http
POST {URL_BASE}/api/v1/ingesta/lecturas
X-Node-Key: nk_…
Content-Type: application/json

{
  "mac": "A4:CF:12:9B:3E:03",
  "secuencia": 18342,
  "lecturas": [
    { "uuidBle": "b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7",  "rssi": -61, "edadMs": 350 },
    { "uuidBle": "b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-11", "rssi": -78, "edadMs": 1200 }
  ]
}
```

| Campo | Tipo | Rango | Qué es |
|---|---|---|---|
| `mac` | texto | MAC del nodo | Debe coincidir con la clave |
| `secuencia` | entero ≥ 0 | 0 a 2³²−1 | Contador del nodo: empieza en 0 al encender y sube 1 por lote. **Un reenvío usa la misma secuencia** |
| `lecturas` | lista | 0 a 200 elementos | Una por emisor visto en el período |
| `uuidBle` | texto | hasta 64 caracteres | Identificador del emisor (sección 3) |
| `rssi` | entero | −127 a 0 dBm | Promedio del período, **sin corregir** (la calibración la aplica el servidor) |
| `edadMs` | entero | 0 a 600 000 | Milisegundos desde que lo vio por última vez **hasta el momento del envío** |

Respuesta correcta: **`202 Accepted`** con `{"recibidas": 2, "duplicado": false}`. Si el lote ya había
llegado antes, responde `202` con `"duplicado": true` y no lo guarda otra vez: para el nodo es un éxito.

## 5. Enviar latidos

```http
POST {URL_BASE}/api/v1/ingesta/latidos
X-Node-Key: nk_…
Content-Type: application/json

{ "mac": "A4:CF:12:9B:3E:03", "versionFirmware": "1.0.0", "rssiWifi": -58, "memoriaLibre": 142000 }
```

| Campo | Obligatorio | Rango |
|---|---|---|
| `mac` | Sí | MAC del nodo |
| `versionFirmware` | No | Hasta 20 caracteres |
| `rssiWifi` | No | −127 a 0 dBm |
| `memoriaLibre` | No | Bytes, ≥ 0 |

Respuesta: **`202 Accepted`** sin cuerpo. Con el primer latido el nodo aparece **En línea**; si pasan
**120 s** sin latidos, pasa a **Sin señal** y su zona se ve gris en el gemelo.

## 6. Qué hacer con cada respuesta

| Respuesta | Significa | El nodo debe |
|---|---|---|
| `202` | Recibido (o duplicado ya recibido) | Borrar el lote del búfer |
| `400` | El JSON no cumple este contrato | **No reintentar** (fallaría igual). Registrar el cuerpo de la respuesta (trae `errores`) y descartar el lote. Es un error de firmware que hay que corregir |
| `401` | Clave o MAC incorrectas, o nodo dado de baja | **Dejar de enviar** y señalarlo (LED). Reintentar cada 60 s por si se corrigió en el servidor |
| `429` | Demasiadas peticiones | Esperar los segundos de la cabecera `Retry-After` y reintentar |
| `5xx`, timeout o sin red | Problema del servidor o de la red | Guardar en el búfer y reintentar (sección 7) |

Timeout recomendado por petición: **5 s**.

## 7. Búfer y reenvío sin Wi-Fi

- Guardar hasta **10 minutos** de lotes. Si se llena, se descarta **el más antiguo**.
- Reintentar con espera creciente: 2, 4, 8, 16 y luego cada 30 s.
- Al reenviar, **mantener la `secuencia` original** (así el servidor detecta lo que ya recibió) y
  **recalcular `edadMs`** como `ahora − momento en que se vio`. Si supera 600 000, esa lectura se descarta:
  si se enviara la edad original, el servidor la fecharía en el momento del reenvío y el tiempo de espera
  del paciente saldría mal.
- Enviar los lotes pendientes **en orden**, del más antiguo al más nuevo.

## 8. Seguridad

- La clave va **solo** en el NVS y en la cabecera `X-Node-Key`. Nunca en el código fuente, en el
  repositorio ni en el log serie.
- **En la clínica la URL es HTTPS** con un certificado interno: el firmware debe llevar el certificado
  raíz de la clínica (`cert_pem` de `esp_http_client`) y validarlo. **No desactivar la validación**: con
  ella apagada, cualquiera en la red podría hacerse pasar por el servidor y robar las claves.
- En desarrollo (red local de pruebas) se usa HTTP.

## 9. Límites del servidor

| Límite | Valor |
|---|---|
| Peticiones por IP | 600 por minuto (un nodo normal hace ~32) |
| Lecturas por lote | 200 |
| Antigüedad de una lectura (`edadMs`) | 10 minutos |
| Latido máximo antes de Sin señal | 120 s |

## 10. Probar contra la laptop de desarrollo

1. En la laptop con la API, arrancarla escuchando en la red y no solo en localhost:
   `dotnet run --project src/FlowTwin.Api -- --urls http://0.0.0.0:5080` (el `--` hace que `--urls` llegue a la API)
2. Averiguar la IP de la laptop en la red (`hostname -I`): la URL base es `http://<IP>:5080`.
3. Registrar el ESP32 real (su MAC) como nodo desde `http://localhost:5080/scalar` →
   `POST /api/v1/nodos` con la sesión del Administrador, y cargar en el nodo la clave que devuelve.
4. Ver en la consola de la API los mensajes «Nodo N-0x en línea» y las peticiones a `/ingesta`.

Ejemplo con curl, simulando al nodo de pruebas N-01 (MAC y clave solo existen en desarrollo):

```bash
curl -i -X POST http://<IP>:5080/api/v1/ingesta/lecturas \
  -H "X-Node-Key: nk_dev_N-01" -H "Content-Type: application/json" \
  -d '{"mac":"02:00:00:00:00:01","secuencia":1,"lecturas":[{"uuidBle":"b14c70f2-314c-41eb-b11a-93fa9c7ace90-1-7","rssi":-61,"edadMs":350}]}'
```

## 11. Lista de verificación de la primera prueba

- [ ] El emisor aparece en una app como *nRF Connect* con el UUID, major y minor correctos.
- [ ] El nodo ignora un beacon con otro UUID (por ejemplo, un celular emitiendo otro iBeacon).
- [ ] El primer latido devuelve `202` y el nodo aparece **En línea** en el sistema.
- [ ] Un lote devuelve `202` con `"recibidas"` igual al número de emisores cerca.
- [ ] Con una clave equivocada el servidor responde `401` y el nodo deja de insistir cada 2 s.
- [ ] Desenchufar el router 3 minutos: al volver, los lotes guardados llegan, sin duplicados y con la
      `edadMs` recalculada.
- [ ] Apagar el nodo: a los 120 s aparece **Sin señal**.
