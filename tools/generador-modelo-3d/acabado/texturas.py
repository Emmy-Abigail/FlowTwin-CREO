"""Texturas procedurales (sin imágenes externas) para el acabado del modelo.

Son mapas de detalle en escala de grises (media ~1) que se multiplican por el
color base de cada material, más el patrón propio de porcelanato y madera.
Se generan con numpy y se guardan como JPG; son repetibles (semilla fija).
"""
import numpy as np
from PIL import Image, ImageFilter

N = 1024


def _ruido(rng, escala, n=N):
    """Ruido suave periódico (se repite sin costuras): ruido blanco filtrado
    en frecuencia; 'escala' es el tamaño aproximado de las manchas en píxeles."""
    f = np.fft.fftfreq(n)
    r = np.sqrt(f[:, None] ** 2 + f[None, :] ** 2)
    filtro = np.exp(-(r * escala) ** 2)
    a = np.real(np.fft.ifft2(np.fft.fft2(rng.standard_normal((n, n))) * filtro))
    return (a - a.mean()) / (a.std() + 1e-9)


def _guardar(a, ruta, rgb=None):
    a = np.clip(a, 0, 1)
    if rgb is None:
        img = np.stack([a, a, a], -1)
    else:
        img = a
    Image.fromarray((img * 255).astype(np.uint8)).save(ruta, quality=88)


def generar(carpeta):
    rng = np.random.default_rng(7)
    t = {}
    # Piso vinílico: moteado fino y variación suave. Repetición: 2 m.
    a = .93 + .02 * _ruido(rng, 120) + .015 * _ruido(rng, 4) + .015 * rng.standard_normal((N, N))
    _guardar(a, f"{carpeta}/vinilico.jpg"); t["vinilico"] = 2.0
    # Porcelanato 60 x 60 cm con fragua de 3 mm. Repetición: 1.2 m (2 x 2 piezas).
    a = .95 + .015 * _ruido(rng, 40) + .01 * rng.standard_normal((N, N))
    f = int(N * .003 / 1.2) + 1
    for p in (0, N // 2):
        a[p:p + f, :] = .62
        a[:, p:p + f] = .62
    _guardar(a, f"{carpeta}/porcelanato.jpg"); t["porcelanato"] = 1.2
    # Madera: vetas rectas a lo largo de un eje, con fibras finas. Repetición: 1 m.
    y = np.linspace(0, 1, N, endpoint=False)[None, :]
    w = _ruido(rng, 300)
    vetas = np.sin((y * 24 + .25 * w) * 2 * np.pi)
    fibras = np.repeat(rng.standard_normal((1, N)), N, 0)
    fibras = np.asarray(Image.fromarray((np.clip(.5 + .15 * fibras, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(1)), dtype=np.float32) / 255 - .5
    a = .9 + .035 * vetas + .12 * fibras + .02 * _ruido(rng, 200) + .01 * rng.standard_normal((N, N))
    _guardar(a, f"{carpeta}/madera.jpg"); t["madera"] = 1.0
    # Laminado de cubiertas: casi liso. Repetición: 1 m.
    a = .96 + .012 * _ruido(rng, 80) + .008 * rng.standard_normal((N, N))
    _guardar(a, f"{carpeta}/laminado.jpg"); t["laminado"] = 1.0
    # Tapiz: tejido fino. Repetición: 0.25 m.
    i, j = np.meshgrid(np.arange(N), np.arange(N), indexing="ij")
    a = .9 + .05 * np.sign(np.sin(i * np.pi / 4)) * np.sign(np.sin(j * np.pi / 4)) + .03 * rng.standard_normal((N, N))
    a = np.asarray(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(.8)), dtype=np.float32) / 255
    _guardar(a, f"{carpeta}/tapiz.jpg"); t["tapiz"] = .25
    # Muro tarrajeado y pintado: textura muy leve. Repetición: 3 m.
    a = .96 + .012 * _ruido(rng, 150) + .008 * _ruido(rng, 6) + .006 * rng.standard_normal((N, N))
    _guardar(a, f"{carpeta}/muro.jpg"); t["muro"] = 3.0
    # Concreto (vereda, escalinata, losa). Repetición: 2 m.
    a = .9 + .035 * _ruido(rng, 70) + .025 * _ruido(rng, 4) + .03 * rng.standard_normal((N, N))
    _guardar(a, f"{carpeta}/concreto.jpg"); t["concreto"] = 2.0
    # Acero cepillado: rayas finas. Repetición: 0.5 m.
    a = .92 + .04 * np.repeat(rng.standard_normal((N, 1)), N, 1) + .02 * rng.standard_normal((N, N))
    a = np.asarray(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.BoxBlur(1)), dtype=np.float32) / 255
    _guardar(a, f"{carpeta}/acero.jpg"); t["acero"] = .5
    return t


if __name__ == "__main__":
    import sys
    print(generar(sys.argv[1]))
