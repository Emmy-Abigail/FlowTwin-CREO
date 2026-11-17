"""Puertas, vidrios, mamparas, cortinas y división en ambientes."""
import math
import numpy as np
from shapely.geometry import LineString, Point, Polygon, box
from shapely.ops import unary_union


def es_arco_de_puerta(a, co):
    """Arco casi circular (largo/cuerda ~ 1.11 para 90°) con cuerda de 0.75 a 2 m."""
    return 1.08 < round(a["razon"], 3) < 1.15 and .75 < round(a["cuerda_pt"] * co.k, 2) < 2.0


def puertas_de_arcos(arcos, W, co):
    """Un arco de ~90° apoyado en un muro es el giro de una puerta."""
    puertas = []
    for a in arcos:
        if a["capa"] == "trama" or not (es_arco_de_puerta(a, co)):
            continue
        poly = a["pts"]
        p0, pm, p1 = [np.array(poly[i]) for i in (0, len(poly) // 2, -1)]
        M = np.array([p1 - p0, pm - p0])
        rhs = np.array([(p1 @ p1 - p0 @ p0) / 2, (pm @ pm - p0 @ p0) / 2])
        try:
            c = np.linalg.solve(M, rhs)
        except np.linalg.LinAlgError:
            continue
        r = np.linalg.norm(p0 - c)
        giro = math.degrees(math.acos(np.clip((p0 - c) @ (p1 - c) / r / r, -1, 1)))
        if not 75 < giro < 105:
            continue
        d0, d1 = W.distance(Point(p0)), W.distance(Point(p1))
        abierta, cerrada = (p0, p1) if d0 > d1 else (p1, p0)
        if W.distance(Point(c)) < 14 and min(d0, d1) < 16:
            puertas.append(dict(bisagra=c.tolist(), abierta=abierta.tolist(),
                                cerrada=cerrada.tolist(), ancho_m=round(r * co.k, 2)))
    unicas = []
    for d in puertas:
        if not any(math.dist(d["bisagra"], e["bisagra"]) < 3 and math.dist(d["abierta"], e["abierta"]) < 3
                   for e in unicas):
            unicas.append(d)
    return unicas


def _en_zona(a, zona, co):
    x = (a[0] - co.cfg.ORIGEN_PT[0]) / co.cfg.PT_POR_REF
    y = (a[1] - co.cfg.ORIGEN_PT[1]) / co.cfg.PT_POR_REF
    return zona[0] < x < zona[2] and zona[1] < y < zona[3]


def mamparas_y_cortinas(trazos, cfg, co):
    mamparas, cortinas, borde = [], [], []
    for s in trazos:
        if s["capa"] == cfg.CAPA_MAMPARAS:
            for a, b in s["segs"]:
                if _en_zona(a, cfg.ZONA_MAMPARAS, co) and math.dist(a, b) > 30:
                    mamparas.append(LineString([a, b]))
                if _en_zona(a, cfg.ZONA_BORDE_ENTREVISTAS, co):
                    borde.append(LineString([a, b]))
        if s["capa"] == cfg.CAPA_CORTINAS and s["color"] and s["segs"]:
            linea = LineString([s["segs"][0][0]] + [z[1] for z in s["segs"]])
            if linea.length > 25:
                cortinas.append(linea)
    return mamparas, cortinas, borde


def _alargar(linea, e=9):
    c = list(linea.coords)
    a, b = np.array(c[0]), np.array(c[-1])
    v = (b - a) / max(np.linalg.norm(b - a), 1e-6)
    return LineString([tuple(a - e * v)] + c[1:-1] + [tuple(b + e * v)])


def dividir_ambientes(W, puertas, vidrios, mamparas, cortinas, borde, cfg, co):
    contorno = Polygon([co.ref(*p) for p in cfg.CONTORNO])
    limites = [LineString([co.ref(*a), co.ref(*b)]) for a, b in cfg.LIMITES_SIN_MURO]
    hojas = [_alargar(LineString([d["bisagra"], d["cerrada"]])) for d in puertas]
    Wc = W.buffer(6, join_style="mitre", mitre_limit=2.5).buffer(-6, join_style="mitre", mitre_limit=2.5)
    red = unary_union([Wc.buffer(.2)] + [g.buffer(.6) for g in vidrios]
                      + [_alargar(l).buffer(.6) for l in limites + mamparas]
                      + [l.buffer(.6) for l in hojas + borde]
                      + [_alargar(l, 14).buffer(.6) for l in cortinas])
    caras = contorno.difference(red)
    limpias = []
    for f in getattr(caras, "geoms", [caras]):
        # Quita astillas delgadas pegadas a los muros.
        g = f.buffer(-1.5, join_style="mitre").buffer(1.5, join_style="mitre")
        # Si la limpieza separa la cara en varias partes, cada parte con tamaño
        # de ambiente se conserva; las astillas se descartan.
        limpias += [q for q in getattr(g, "geoms", [g]) if q.geom_type == "Polygon" and co.m2(q.area) > .25]
    caras = [f for f in limpias if co.m2(f.area) > .25 and not f.buffer(-6).is_empty]
    for grupo in cfg.UNIR:
        pts = [Point(co.ref(*q)) for q in grupo]
        partes = [f for f in caras if any(f.contains(q) for q in pts)]
        if len(partes) > 1:
            caras = [f for f in caras if f not in partes]
            # Se une incluyendo el muro entre ellas para que quede un solo polígono.
            u = unary_union([f.buffer(6, join_style="mitre") for f in partes]).buffer(-6, join_style="mitre")
            caras.append(max(getattr(u, "geoms", [u]), key=lambda q: q.area))

    rotulos = {k: Point(co.ref(*v)) for k, v in cfg.ROTULOS.items()}
    ambientes = []
    for f in sorted(caras, key=lambda g: (g.centroid.y, g.centroid.x)):
        nombres = [k for k, p in rotulos.items() if f.contains(p)]
        nombres = [n for n in nombres if n not in [cfg.REEMPLAZA.get(m) for m in nombres]]
        notas = [t for pt, t in cfg.NOTAS.items() if f.contains(Point(co.ref(*pt)))]
        ambientes.append(dict(geom=f, rotulo=" + ".join(nombres) or None, nota=" ".join(notas) or None,
                              area_m2=round(co.m2(f.area), 1)))
    ambientes = [r for r in ambientes if r["rotulo"] or r["area_m2"] >= cfg.DESCARTAR_SIN_ROTULO_M2]
    for i, r in enumerate(ambientes, 1):
        r["id"] = f"ambiente_{i:02d}"
    sin_zona = [k for k, p in rotulos.items() if not any(r["geom"].contains(p) for r in ambientes)]
    return contorno, ambientes, sin_zona
