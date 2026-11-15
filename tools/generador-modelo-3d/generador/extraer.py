"""Lee los vectores del PDF agrupados por capa del CAD."""
import math
import numpy as np
import pymupdf


def _girar(pagina):
    """Lleva los vectores (en coordenadas sin girar) a la orientación de lectura de la lámina."""
    if pagina.rotation == 0:
        return lambda q: (q[0], q[1])
    if pagina.rotation != 270:
        raise ValueError(f"Rotación de página {pagina.rotation}° no soportada (esta lámina usa 270°)")
    ancho = pagina.mediabox.width
    return lambda q: (q[1], ancho - q[0])


def _bezier(P, t):
    w = ((1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3)
    return (sum(a * b[0] for a, b in zip(w, P)), sum(a * b[1] for a, b in zip(w, P)))


def leer_pdf(ruta, n_pagina=0):
    doc = pymupdf.open(ruta)
    pag = doc[n_pagina]
    R = _girar(pag)
    dibujos = pag.get_drawings()

    trazos = []
    for x in dibujos:
        c = []
        for it in x["items"]:
            if it[0] == "l":
                c.append((R(it[1]), R(it[2])))
            elif it[0] == "c":
                P = [(v.x, v.y) for v in it[1:5]]
                prev = P[0]
                for k in range(1, 9):
                    q = _bezier(P, k / 8)
                    c.append((R(prev), R(q)))
                    prev = q
            elif it[0] in ("qu", "re"):
                Q = it[1] if it[0] == "qu" else it[1].quad
                v = [Q.ul, Q.ur, Q.lr, Q.ll]
                c += [(R(v[i]), R(v[(i + 1) % 4])) for i in range(4)]
        trazos.append(dict(capa=x.get("layer"), ancho=x.get("width"),
                           color=x.get("color"), segs=c))

    arcos = _arcos(dibujos, R)
    return dict(doc=doc, pagina=pag, trazos=trazos, arcos=arcos)


def _arcos(dibujos, R):
    """Cadenas de curvas Bézier: candidatas a arcos de giro de puertas."""
    arcos = []
    for x in dibujos:
        cadena = []
        for it in x["items"] + [("fin",)]:
            if it[0] == "c":
                cadena.append(it)
                continue
            if cadena:
                poly = [np.array(R(cadena[0][1]))]
                for c in cadena:
                    P = [np.array(R(v)) for v in c[1:5]]
                    for t in (.25, .5, .75, 1):
                        poly.append((1 - t) ** 3 * P[0] + 3 * (1 - t) ** 2 * t * P[1]
                                    + 3 * (1 - t) * t * t * P[2] + t ** 3 * P[3])
                cuerda = np.linalg.norm(poly[0] - poly[-1])
                largo = sum(np.linalg.norm(poly[i + 1] - poly[i]) for i in range(len(poly) - 1))
                if cuerda > 0:
                    arcos.append(dict(capa=x.get("layer"), razon=largo / cuerda,
                                      cuerda_pt=cuerda, pts=[tuple(v) for v in poly]))
                cadena = []
    return arcos
