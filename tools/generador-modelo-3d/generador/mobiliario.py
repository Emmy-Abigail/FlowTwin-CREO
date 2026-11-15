"""Mobiliario: contornos de muebles a partir de las capas de equipamiento."""
import math
from collections import defaultdict
import numpy as np
import shapely as sh
from shapely.geometry import LineString, Polygon, box
from shapely.ops import polygonize, unary_union

from .ambientes import es_arco_de_puerta


def detectar_mobiliario(trazos, arcos, puertas, W, contorno, cfg, co):
    lineas_puerta = [LineString(a["pts"]) for a in arcos
                     if es_arco_de_puerta(a, co)]
    lineas_puerta += [LineString([d["bisagra"], d["abierta"]]) for d in puertas]
    excluir = unary_union([box(*co.ref(*r[:2]), *co.ref(*r[2:])) for r in cfg.SIN_MOBILIARIO])
    zona = contorno.difference(excluir)

    L, meta = [], []
    for s in trazos:
        if s["capa"] not in cfg.CAPAS_MOBILIARIO:
            continue
        # Las cortinas se modelan aparte.
        if s["capa"] == cfg.CAPA_CORTINAS and s["segs"] and len(s["segs"]) > 8 and \
                LineString([s["segs"][0][0]] + [z[1] for z in s["segs"]]).length > 25:
            continue
        for a, b in s["segs"]:
            largo = math.dist(a, b)
            # 4.0-4.95 pt: radios de los círculos de maniobra de los sillones dentales.
            if a == b or 4.0 < largo < 4.95:
                continue
            L.append(LineString([a, b]))
            c = s["color"] or (0, 0, 0)
            meta.append((s["capa"], c[1] > .6 and c[0] < .4 and c[2] < .4))

    A = np.array(L)
    meta = np.array(meta, dtype=object)
    sh.prepare(zona)
    dentro = sh.within(A, zona)
    A, meta = A[dentro], meta[dentro]
    arbol = sh.STRtree(A)
    cerca = set()
    for z in lineas_puerta:
        cerca |= set(arbol.query(z, predicate="dwithin", distance=1.5).tolist())
    A = np.delete(A, list(cerca))
    meta = np.delete(meta, list(cerca), axis=0)
    arbol_lineas = sh.STRtree(A)

    # Agrupar trazos que se tocan (union-find) y tomar el contorno de cada grupo.
    buf = sh.buffer(A, .7, quad_segs=1, cap_style="square")
    arbol = sh.STRtree(buf)
    padre = list(range(len(buf)))

    def raiz(i):
        while padre[i] != i:
            padre[i] = padre[padre[i]]
            i = padre[i]
        return i

    ii, jj = arbol.query(buf, predicate="intersects")
    for x, y in zip(ii, jj):
        if x < y:
            padre[raiz(x)] = raiz(y)
    grupos = defaultdict(list)
    for i in range(len(buf)):
        grupos[raiz(i)].append(i)

    piezas = []
    for idx in grupos.values():
        g = sh.union_all(buf[idx])
        fp = g.buffer(.7, join_style="mitre").buffer(-.7, join_style="mitre")
        for q in getattr(fp, "geoms", [fp]):
            q = Polygon(q.exterior).simplify(.3)
            ext = max(q.bounds[2] - q.bounds[0], q.bounds[3] - q.bounds[1])
            if ext * co.k < .14 or q.buffer(-1.6).is_empty or co.m2(q.area) > 7:
                continue
            piezas.append(q)
    piezas = [p.difference(W.buffer(.5)) for p in piezas]
    piezas = [q for p in piezas for q in getattr(p, "geoms", [p])
              if q.geom_type == "Polygon" and co.m2(q.area) > .025]

    # Bancas de la sala de espera: se separan en asientos según las líneas del plano.
    arbol = sh.STRtree(A)
    salida = []
    for p in piezas:
        mr = p.minimum_rotated_rectangle
        c = list(mr.exterior.coords)
        d1, d2 = math.dist(c[0], c[1]) * co.k, math.dist(c[1], c[2]) * co.k
        if 1.2 < co.m2(p.area) < 4 and max(d1, d2) / max(min(d1, d2), .01) > 2.4 \
                and p.area / mr.area > .9 and min(d1, d2) < .8:
            celdas = [f for f in polygonize(sh.union_all(A[arbol.query(p)]))
                      if co.m2(f.area) > .1 and p.contains(f.representative_point())]
            if len(celdas) >= 3:
                salida += [("asiento", f.buffer(-.6)) for f in celdas]
                continue
        salida.append(("pieza", p))

    # Metadatos de cada pieza: capas del CAD y si contiene el símbolo verde de sillón.
    resultado = []
    for tipo, p in salida:
        idx = arbol_lineas.query(p.buffer(.5), predicate="intersects")
        capas = {}
        verde = False
        for i in idx:
            capas[meta[i][0]] = capas.get(meta[i][0], 0) + 1
            verde = verde or bool(meta[i][1])
        resultado.append(dict(tipo=tipo, geom=p, capas=capas, verde=verde,
                              lineas=list(A[idx])))
    return resultado
