"""Muros: une las capas de muro del CAD en polígonos limpios."""
import shapely as sh
from shapely.geometry import MultiLineString, MultiPolygon, LineString, box
from shapely.ops import unary_union


def detectar_muros(trazos, cfg, co):
    caja = box(*cfg.RECORTE_PT)
    seguros, gruesos = [], []
    for s in trazos:
        segs = [x for x in s["segs"] if x[0] != x[1]]
        if s["capa"] in cfg.CAPAS_MURO:
            seguros += segs
        elif s["capa"] in cfg.CAPAS_MURO_GRUESO and round(s["ancho"] or 0, 2) >= .72:
            gruesos += segs

    def engrosar(segs):
        return sh.clip_by_rect(MultiLineString(segs), *caja.bounds).buffer(.45, cap_style="square")

    A = engrosar(seguros)
    B = engrosar(gruesos)
    # De las capas genéricas solo se aceptan trazos largos (muros, no detalles).
    B = unary_union([g for g in B.geoms if max(g.bounds[2] - g.bounds[0], g.bounds[3] - g.bounds[1]) > 70])
    red = unary_union([A, B])

    # Cierre morfológico: rellena el espacio entre las dos caras de cada muro.
    R = 7.0
    W = red.buffer(R, join_style="mitre", mitre_limit=2.5).buffer(-R, join_style="mitre", mitre_limit=2.5)
    W = W.buffer(-1.2, join_style="mitre").buffer(1.2, join_style="mitre").simplify(.25)
    W = MultiPolygon([g for g in W.geoms if co.m2(g.area) > .015])

    W = W.difference(box(*co.ref(*cfg.RAMPA[:2]), *co.ref(*cfg.RAMPA[2:])))
    extra = [LineString([co.ref(*a), co.ref(*b)]).buffer(2.5, cap_style="flat") for a, b in cfg.MUROS_EXTRA]
    W = unary_union([W] + extra)
    return MultiPolygon([g for g in getattr(W, "geoms", [W])
                         if g.geom_type == "Polygon" and co.m2(g.area) > .015])
