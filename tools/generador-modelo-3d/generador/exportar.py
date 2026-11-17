"""Construye las mallas 3D y exporta el .glb, el JSON de ambientes y el mapa de IDs."""
import json
import math
import random
import numpy as np
import shapely as sh
import trimesh
from PIL import Image, ImageDraw, ImageFont
from shapely.geometry import LineString, MultiPolygon, Point, Polygon, box
from shapely.ops import unary_union
from trimesh.visual import TextureVisuals
from trimesh.visual.material import PBRMaterial

from . import muebles

# color base RGBA, rugosidad, metalicidad, ¿transparente?
MATERIALES = {
    "muro": ((.86, .85, .82, 1), .85, 0, False), "corte": ((.16, .18, .20, 1), .9, 0, False),
    "piso": ((.62, .56, .48, 1), .75, 0, False), "zona": ((.70, .64, .54, 1), .65, 0, False),
    "ducto": ((.62, .63, .64, 1), .9, 0, False), "puerta": ((.60, .42, .27, 1), .6, 0, False),
    "vidrio": ((.62, .82, .86, .35), .1, 0, True), "cortina": ((.70, .82, .82, .55), .9, 0, True),
    "mampara": ((.80, .86, .86, .75), .3, 0, True), "mueble": ((.72, .56, .40, 1), .6, 0, False),
    "silla": ((.55, .16, .22, 1), .7, 0, False), "equipo": ((.80, .83, .86, 1), .4, .1, False),
    "escalera": ((.74, .74, .72, 1), .8, 0, False), "zocalo": ((.55, .55, .53, 1), .6, 0, False),
    "tapiz": ((.20, .42, .50, 1), .7, 0, False), "base_sillon": ((.30, .31, .33, 1), .5, .3, False),
    "equipo_blanco": ((.90, .91, .90, 1), .4, 0, False), "ceramica": ((.95, .95, .94, 1), .15, 0, False),
    "colchon": ((.86, .90, .92, 1), .8, 0, False), "cubierta": ((.92, .90, .86, 1), .5, 0, False), "acero": ((.72, .74, .76, 1), .35, .7, False), "base": ((.70, .70, .68, 1), .95, 0, False),
}
MAT = {k: PBRMaterial(name=k, baseColorFactor=v[0], roughnessFactor=v[1], metallicFactor=v[2],
                      alphaMode="BLEND" if v[3] else "OPAQUE", doubleSided=v[3])
       for k, v in MATERIALES.items()}


class Modelo:
    def __init__(self, co):
        self.co = co
        self.escena = trimesh.Scene()

    def extruir(self, geom_pt, y0, y1, mat):
        """Extruye polígonos (en pt) entre las alturas y0 e y1 (en m)."""
        mallas = []
        g = self.co.a_mundo(geom_pt)
        for p in getattr(g, "geoms", [g]):
            if p.is_empty or p.geom_type != "Polygon" or p.area < 1e-5:
                continue
            p = p.buffer(0)
            for q in getattr(p, "geoms", [p]):
                m = trimesh.creation.extrude_polygon(q, y1 - y0)
                m.apply_translation([0, 0, y0])
                mallas.append(m)
        m = trimesh.util.concatenate(mallas)
        # De Z-arriba (extrusión) a Y-arriba (glTF / Three.js).
        m.apply_transform(trimesh.transformations.rotation_matrix(-math.pi / 2, [1, 0, 0]))
        m.visual = TextureVisuals(material=MAT[mat])
        return m

    def agregar(self, nombre, malla, mat=None):
        if mat:
            malla.visual = TextureVisuals(material=MAT[mat])
        self.escena.add_geometry(malla, node_name=nombre, geom_name=nombre)


def construir(d, cfg, co):
    """d: diccionario con W, contorno, ambientes, puertas, vidrios, mamparas, cortinas, mobiliario."""
    M = Modelo(co)
    R = co.ref
    W, contorno = d["W"], d["contorno"]
    baja = Polygon([R(*p) for p in cfg.ZONA_BAJA])
    explanada = Polygon([R(*p) for p in cfg.EXPLANADA])
    BASE, BAJO = cfg.NIVEL_BASE, cfg.NIVEL_BAJO
    nivel = lambda punto: BAJO if baja.contains(punto) else 0.0

    # Losa y pisos
    todo = unary_union([contorno, W.buffer(.5)] + d["vidrios"])
    suelo = unary_union([todo.convex_hull, explanada.buffer(3), box(*R(*cfg.SUELO_EXTRA[:2]), *R(*cfg.SUELO_EXTRA[2:]))])
    suelo = suelo.difference(box(*R(*cfg.SUELO_RECORTE[:2]), *R(*cfg.SUELO_RECORTE[2:])))
    EB = cfg.ESCALERA_BAJADA
    foso = box(*R(*EB["caja"][:2]), *R(*EB["caja"][2:]))
    suelo = suelo.difference(foso)
    contorno_piso = contorno.difference(foso)
    M.agregar("losa_base", M.extruir(suelo, BASE - .12, BASE, "base"))
    M.agregar("piso_principal", M.extruir(contorno_piso.difference(baja), BASE, -.01, "piso"))
    M.agregar("piso_nivel_0_10", M.extruir(baja.intersection(contorno), BASE, BAJO - .01, "piso"))
    M.agregar("piso_ingreso", M.extruir(explanada, BASE, -.01, "piso"))

    # Muros con tapa oscura (efecto de corte de maqueta)
    M.agregar("muros", M.extruir(W, BASE, cfg.ALTURA_MUROS, "muro"))
    # Zócalo de 10 cm al pie de los muros (acabado estándar; el plano no lo detalla)
    zocalo = W.buffer(.012 / co.k, join_style="mitre").difference(W).intersection(contorno_piso)
    M.agregar("zocalo", M.extruir(zocalo, 0, cfg.ALTURA_ZOCALO, "zocalo"))
    M.agregar("muros_corte", M.extruir(W, cfg.ALTURA_MUROS, cfg.ALTURA_MUROS + .006, "corte"))

    # Ambientes: una losa delgada por ambiente, con su propio nombre
    lista = []
    for r in d["ambientes"]:
        g = r["geom"]
        y = nivel(g.representative_point())
        M.agregar(r["id"], M.extruir(g.difference(foso), y - .01, y + .012, "zona"))
        c = co.a_mundo(g.representative_point())
        lista.append(dict(id=r["id"], rotulo_en_plano=r["rotulo"], nota=r["nota"], area_m2=r["area_m2"],
                          nivel_y=y, centro=[round(c.x, 2), round(y, 2), round(-c.y, 2)]))

    # Ductos: huecos cerrados por muros que no forman parte de ningún ambiente
    libre = contorno.difference(W.buffer(.1))
    usados = unary_union([r["geom"].buffer(3) for r in d["ambientes"]])
    ductos = [g for g in getattr(libre.difference(usados), "geoms", [])
              if .25 < co.m2(g.area) < 7 and not g.buffer(-4).is_empty]
    if ductos:
        M.agregar("ductos", M.extruir(MultiPolygon(ductos), 0, cfg.ALTURA_MUROS, "ducto"))

    # Puertas: hoja abierta, tal como dibuja el plano el arco de giro
    for i, p in enumerate(d["puertas"], 1):
        hoja = LineString([p["bisagra"], p["abierta"]]).buffer(1.2, cap_style="flat")
        y = nivel(Point(p["bisagra"]))
        M.agregar(f"puerta_{i:02d}", M.extruir(hoja, y, min(y + cfg.ALTURA_PUERTAS, cfg.ALTURA_MUROS), "puerta"))

    M.agregar("vidrios", M.extruir(MultiPolygon(d["vidrios"]), 0, cfg.ALTURA_MUROS, "vidrio"))
    M.agregar("mamparas", M.extruir(unary_union([l.buffer(1.3, cap_style="flat") for l in d["mamparas"]]),
                                    0, cfg.ALTURA_MAMPARAS, "mampara"))
    M.agregar("cortinas", M.extruir(unary_union([l.simplify(.4).buffer(.7, cap_style="flat", join_style="mitre") for l in d["cortinas"]]),
                                    0, cfg.ALTURA_CORTINAS, "cortina"))

    # Mobiliario con forma real (ver muebles.py)
    cuenta = muebles.construir(M, d["mobiliario"], d["sillones"], d["ambientes"], nivel, cfg, co, W)

    # Escaleras: primer tramo; lo que pasa el corte se ve como sección (tapa oscura)
    for nombre, e in cfg.ESCALERAS.items():
        pasos, tapas = [], []
        for i in range(e["n"]):
            t0 = e["ini"] + (e["fin"] - e["ini"]) * i / e["n"]
            t1 = e["ini"] + (e["fin"] - e["ini"]) * (i + 1) / e["n"]
            lo, hi = sorted((t0, t1))
            r = (e["a0"], lo, e["a1"], hi) if e["eje"] == "y" else (lo, e["a0"], hi, e["a1"])
            g = box(*R(r[0], r[1]), *R(r[2], r[3]))
            h = e["arranque"] + e["subida"] * (i + 1)
            if h > cfg.ALTURA_MUROS:
                pasos.append(M.extruir(g, e["arranque"], cfg.ALTURA_MUROS, "escalera"))
                tapas.append(M.extruir(g, cfg.ALTURA_MUROS, cfg.ALTURA_MUROS + .006, "corte"))
            else:
                pasos.append(M.extruir(g, e["arranque"], h, "escalera"))
        M.agregar(nombre, trimesh.util.concatenate(pasos), "escalera")
        if tapas:
            M.agregar(nombre + "_corte", trimesh.util.concatenate(tapas), "corte")

    # Escalera N°4 (baja): foso con dos tramos y descanso
    xs, h = EB["contrapasos_x"], EB["contrapaso"]
    fondo = EB["descanso"] - h * (len(xs) - 1) - .3
    pasos = []
    t0, t1 = EB["tramo_superior"]
    for i in range(len(xs) - 1):          # tramo superior: baja hacia la derecha
        pasos.append(M.extruir(box(*R(xs[i], t0), *R(xs[i + 1], t1)), fondo, -h * (i + 1), "escalera"))
    pasos.append(M.extruir(box(*R(xs[-1], EB["caja"][1]), *R(EB["caja"][2], EB["caja"][3])), fondo, EB["descanso"], "escalera"))
    b0, b1 = EB["tramo_inferior"]
    for i in range(len(xs) - 1):          # tramo inferior: sigue bajando hacia la izquierda
        j = len(xs) - 2 - i
        pasos.append(M.extruir(box(*R(xs[j], b0), *R(xs[j + 1], b1)), fondo, EB["descanso"] - h * (i + 1), "escalera"))
    M.agregar(EB["nombre"], trimesh.util.concatenate(pasos), "escalera")
    d0, d1 = EB["divisor"]
    M.agregar(EB["nombre"] + "_divisor", M.extruir(box(*R(xs[0], d0), *R(xs[-1], d1)), fondo, .9, "muro"))
    M.agregar(EB["nombre"] + "_foso", M.extruir(foso.buffer(.15 / co.k, join_style="mitre").difference(foso), fondo, BASE, "muro"))

    # Ascensores: cabinas (paredes delgadas, cortadas a la altura de corte) y puertas
    A = cfg.ASCENSORES
    for i, (x0, y0, x1, y1) in enumerate(A["cabinas"], 1):
        cab = box(*R(x0, y0), *R(x1, y1))
        M.agregar(f"ascensor_{i}_cabina", M.extruir(cab.difference(cab.buffer(-2.4)), 0, cfg.ALTURA_MUROS, "acero"))
        M.agregar(f"ascensor_{i}_piso", M.extruir(cab, 0, .02, "acero"))
    for i, (p0, p1) in enumerate(A["puertas"], 1):
        hoja = LineString([R(*p0), R(*p1)]).buffer(1.2, cap_style="flat")
        M.agregar(f"ascensor_{i}_puerta", M.extruir(hoja, 0, min(cfg.ALTURA_PUERTAS, cfg.ALTURA_MUROS), "acero"))

    # Ingreso: escalinata y rampa del 10 %
    E = cfg.ESCALINATA
    pasos = [M.extruir(box(*R(E["x_ini"], E["y_tope"]), *R(E["x"][k], E["y"][k])), BASE, BASE + E["subida"] * (k + 1), "escalera")
             for k in range(len(E["x"]))]
    M.agregar("escalinata_ingreso", trimesh.util.concatenate(pasos), "escalera")
    a, c = R(*cfg.RAMPA_CUNA[:2]), R(*cfg.RAMPA_CUNA[2:])
    cx, cy = co.centro
    P = np.array([[a[0], a[1], 0], [c[0], a[1], BASE], [c[0], c[1], BASE], [a[0], c[1], 0],
                  [a[0], a[1], BASE], [a[0], c[1], BASE]], float)
    V = np.c_[(P[:, 0] - cx) * co.k, P[:, 2], (P[:, 1] - cy) * co.k]
    cuna = trimesh.Trimesh(V, [[0, 3, 2], [0, 2, 1], [0, 1, 4], [3, 5, 2], [0, 4, 5], [0, 5, 3]], process=True)
    trimesh.repair.fix_normals(cuna)
    M.agregar("rampa_ingreso", cuna, "escalera")
    return M.escena, lista, cuenta


def guardar_json(lista, ruta, pdf, cfg):
    datos = dict(fuente=pdf, escala_m_por_pt=cfg.M_POR_PT, altura_corte_muros_m=cfg.ALTURA_MUROS,
                 nivel_y0="N.P.T. +0.75", ambientes=lista)
    with open(ruta, "w", encoding="utf-8") as f:
        json.dump(datos, f, ensure_ascii=False, indent=1)


def guardar_mapa(d, ruta, cfg):
    ox, oy = cfg.ORIGEN_PT
    x1, y1 = cfg.RECORTE_PT[2:]
    esc = 2.0
    im = Image.new("RGB", (int((x1 - ox) * esc), int((y1 - oy) * esc)), "white")
    dib = ImageDraw.Draw(im)
    T = lambda cs: [((x - ox) * esc, (y - oy) * esc) for x, y in cs]
    random.seed(3)
    try:
        fuente = ImageFont.truetype("DejaVuSans-Bold.ttf", 17)
    except OSError:
        fuente = ImageFont.load_default()
    for r in d["ambientes"]:
        g = r["geom"]
        dib.polygon(T(g.exterior.coords), fill=tuple(random.randint(170, 240) for _ in range(3)))
        for i in g.interiors:
            dib.polygon(T(i.coords), fill="white")
    for g in d["W"].geoms:
        dib.polygon(T(g.exterior.coords), fill=(40, 40, 40))
        for i in g.interiors:
            dib.polygon(T(i.coords), fill="white")
    for p in d["puertas"]:
        dib.line(T([p["bisagra"], p["abierta"]]), fill=(200, 0, 0), width=3)
    for r in d["ambientes"]:
        p = r["geom"].representative_point()
        dib.text(((p.x - ox) * esc - 12, (p.y - oy) * esc - 8), r["id"][-2:], fill="black", font=fuente)
    im.save(ruta)
