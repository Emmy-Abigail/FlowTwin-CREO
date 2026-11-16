# Generador del modelo 3D (FlowTwin CREO+)

Convierte un plano PDF exportado de AutoCAD (con capas) en el modelo `.glb` que
usa el frontend del gemelo digital. Hoy está configurado para el primer piso
(lámina A-07).

Lee los vectores del PDF, separa muros, puertas, vidrios, mamparas, cortinas y
mobiliario por capa del CAD, divide el piso en ambientes y exporta:

| Archivo | Para qué sirve |
|---|---|
| `clinica-piso1.glb` | Modelo 3D que carga Three.js |
| `clinica-piso1-ambientes.json` | ID, rótulo del plano, área y centro de cada ambiente |
| `clinica-piso1-mapa.png` | Mapa 2D con el número de cada ambiente |
| `clinica-piso1-visor.html` | Visor autocontenido para revisar el modelo sin Angular |
| `clinica-piso1-final.glb` | Modelo con acabado realista (paso opcional, ver abajo) |

## Instalación

Requiere Python 3.11 o superior. Se verificó con Python 3.13.

```bash
cd tools/generador-modelo-3d
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

## Uso

```bash
python generar.py --pdf "../../PLANOS_COMPLETOS/A07_PISO 01.pdf" --salida salida
```

Tarda unos 15 segundos. El mapa de ambientes sirve para revisar los rótulos:

```bash
cp salida/clinica-piso1-mapa.png ../../docs/mapa-ambientes-piso1.png
```

## Instalar la maqueta de un piso (ADR-28)

La API entrega la maqueta con sesión desde la carpeta `Maquetas:Ruta` del servidor (en desarrollo,
`maquetas/` en la raíz del proyecto, que está en `.gitignore`). **Nunca en `public/` del frontend:** ahí
la descarga cualquiera sin iniciar sesión. Una subcarpeta por piso, con el **id** del piso (el de
Configuración):

```
maquetas/piso-1/
  maqueta.glb       ← salida/clinica-piso1-final.glb (con el acabado de abajo)
  ambientes.json    ← salida/clinica-piso1-ambientes.json
  zonas.json        ← qué ambientes pinta cada zona del sistema (lo escribe el equipo técnico)
```

`zonas.json` enlaza cada zona (por su id) con los rótulos del plano, tal como están en `ambientes.json`:

```json
{ "zonas": { "3": ["Sala de esperas"], "7": ["Módulo 2"] } }
```

Una zona puede pintarse sobre varios ambientes (basta listar varios rótulos). Solo se rotulan en el 3D las zonas; el resto de ambientes se ve sin nombre. Si un rótulo no existe en la maqueta, la consola del navegador lo avisa. Para cambiar la maqueta basta con
reemplazar los archivos: el navegador detecta la versión nueva (ETag) sin reiniciar nada.

## Acabado realista (materiales, texturas y sombras horneadas)

Es un segundo paso, con Blender como módulo de Python (necesita Python 3.11):

```bash
python3.11 -m venv .venv-blender
.venv-blender/bin/pip install -r acabado/requirements-acabado.txt
.venv-blender/bin/python acabado/acabar.py --glb salida/clinica-piso1.glb \
    --ambientes salida/clinica-piso1-ambientes.json --salida salida/clinica-piso1-acabado.glb
# Compresión de la geometría (de ~12 MB a ~5 MB); requiere Node.js
npx @gltf-transform/cli@4.1.1 meshopt salida/clinica-piso1-acabado.glb salida/clinica-piso1-final.glb --level medium
```

El horneado tarda unos 5 minutos. Los nombres de los objetos no cambian. Los
acabados (vinílico, porcelanato en baños, madera, laminado, tapiz, acero) no
vienen en el plano: son acabados estándar de clínica, configurables en
`ACABADOS` dentro de `acabado/acabar.py`. Para cargarlo en Angular, ver
`GUIA-THREEJS.md`.

## Corregir el modelo

Casi todo se deduce del PDF. Lo que no, está en `config_piso1.py`: contorno,
vidrios, límites entre ambientes sin muro, rótulos, escaleras y alturas.

Esas coordenadas se leen sobre una imagen de referencia con cuadrícula:

```bash
python generar.py --pdf "../../PLANOS_COMPLETOS/A07_PISO 01.pdf" --salida salida --referencia
```

Abre `salida/referencia.png`: la cuadrícula azul va cada 50 px y la roja cada
250 px, con sus valores. Por ejemplo, para separar dos ambientes que salieron
unidos, agrega un segmento `((x0, y0), (x1, y1))` a `LIMITES_SIN_MURO` y vuelve
a generar.

## Datos del plano y valores aproximados

**Del plano:**

- La escala está calibrada con la cota de 40.12 m y comprobada con las cotas de 2.55 m y 1.80 m.
- Las posiciones y contornos de muros, puertas, vidrios y mobiliario.
- Los rótulos de los ambientes.

**Aproximado (decisión visual):** todas las alturas. Están en la sección de
alturas de `config_piso1.py`. En el modelo, `y = 0` es el N.P.T. +0.75.

## Importante: los ID de los ambientes

Los ambientes se numeran por posición, de arriba abajo y de izquierda a
derecha. Si agregas o quitas un límite, la numeración puede cambiar. Revisa el
mapa nuevo y actualiza el mapeo de nombres del frontend antes de reemplazar el
`.glb`.

## Limitaciones conocidas

- `Área de recepción` y `Farmacia` siguen unidas en un solo ambiente.
- Las escaleras N.° 1 y N.° 2 muestran solo el primer tramo (el segundo queda sobre el corte del plano).
- La escalera N.° 4 baja en un foso; su contrapaso (0.145 m) es una suposición: el plano solo da el descanso a N.P.T. -0.70.
- La detección de puertas es automática; las que no se reconocen se cierran como vanos en `config_piso1.py`.
- Solo admite láminas con rotación de página de 0° o 270°.

## Mobiliario

Cada pieza del plano se clasifica por su bloque (capa del CAD, tamaño, forma y
ambiente) y se reemplaza por un modelo con forma real en la misma posición:

| Tipo | Cómo se reconoce en el plano |
|---|---|
| Unidad con sillón | bloque con el cuadrado verde (sillón, cabezal, poste y bandeja) |
| Escupidera | pieza pequeña junto al sillón |
| Silla / taburete | asiento con o sin arco de respaldo; el respaldo va donde el plano dibuja el arco |
| Asiento de espera | bancas divididas en asiento y respaldo según sus líneas |
| Camilla | rectángulo de camilla en observación, tópicos y área de camillas |
| Inodoro / lavatorio | capas de sanitarios y equipamiento en los baños |
| Mueble | mostradores, escritorios y el resto de contornos (altura 0.75 o 0.90 m) |

Los textos y códigos de equipo (capa `ARQ-CLAVE`) no se modelan. Las alturas
son medidas estándar porque el plano no las indica.

## Estructura

```
generar.py              Punto de entrada (CLI)
config_piso1.py         Parámetros del primer piso
generador/
  extraer.py            Lectura de vectores y arcos del PDF
  muros.py              Detección y limpieza de muros
  ambientes.py          Puertas, vidrios, mamparas, cortinas y ambientes
  mobiliario.py         Contornos de muebles
  muebles.py            Clasificación y modelos de mobiliario
  exportar.py           Mallas 3D, materiales, .glb, JSON y mapa
  referencia.py         Imagen de referencia con cuadrícula
  visor.py              Visor HTML de revisión
acabado/
  texturas.py           Texturas procedurales (sin imágenes externas)
  acabar.py             Materiales, bisel y oclusión horneada con Blender
```
