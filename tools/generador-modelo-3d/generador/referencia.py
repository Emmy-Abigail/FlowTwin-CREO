"""Imagen de referencia con cuadrícula para leer coordenadas "ref" y editar config_piso1.py."""
import pymupdf
from PIL import Image, ImageDraw


def guardar_referencia(pagina, cfg, ruta):
    x0, y0, x1, y1 = cfg.RECORTE_PT
    ancho_ref = round((x1 - x0) / cfg.PT_POR_REF)            # 2000 px
    esc = 1 / cfg.PT_POR_REF
    # Rectángulo de recorte en las coordenadas sin girar de la página.
    clip = pymupdf.Rect(x0, y0, x1, y1)  # en páginas giradas, get_pixmap usa coordenadas giradas
    pm = pagina.get_pixmap(matrix=pymupdf.Matrix(esc, esc), clip=clip)
    im = Image.frombytes("RGB", (pm.width, pm.height), pm.samples).convert("RGB")
    if im.width != ancho_ref:
        im = im.resize((ancho_ref, round(im.height * ancho_ref / im.width)))
    d = ImageDraw.Draw(im)
    for x in range(0, im.width, 50):
        d.line([(x, 0), (x, im.height)], fill=(0, 140, 255) if x % 250 else (220, 0, 0), width=1)
        if x % 250 == 0:
            d.text((x + 3, 3), str(x), fill=(220, 0, 0))
    for y in range(0, im.height, 50):
        d.line([(0, y), (im.width, y)], fill=(0, 140, 255) if y % 250 else (220, 0, 0), width=1)
        if y % 250 == 0:
            d.text((3, y + 3), str(y), fill=(220, 0, 0))
    im.save(ruta)
