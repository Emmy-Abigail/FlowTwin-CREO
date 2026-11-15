"""Mobiliario con forma real.

Cada pieza detectada en el plano se clasifica según su bloque (capa del CAD,
tamaño, forma y ambiente) y se reemplaza por un modelo paramétrico que ocupa
exactamente la posición, orientación y contorno del plano. Las alturas no
vienen en el plano: son medidas estándar (ver ALTURA_MUEBLE en la config).

Tipos:
  unidad_dental   bloque con símbolo verde: sillón, cabezal, brazo y bandeja
  escupidera      pieza pequeña junto al sillón (n.º 5 del bloque)
  taburete        asiento redondo (operador / asistente)
  silla           silla con respaldo (asiento + arco de respaldo en el plano)
  asiento_espera  asientos y respaldos de las bancas de la sala de espera
  camilla         camillas de observación, tópico y área de camillas
  inodoro, lavatorio
  mueble          mostradores, escritorios, muebles bajos y otros contornos
"""
import math
from collections import defaultdict

import numpy as np
import shapely as sh
import trimesh
from shapely import affinity
from shapely.geometry import LineString, MultiPoint, Point, Polygon, box
from shapely.ops import nearest_points, polygonize, unary_union

BANOS = ("SH", "SSHH")


# ---------------------------------------------------------------- símbolos
def simbolos_sillon(trazos, co):
    """Centros y ángulo de los cuadrados verdes (símbolo del bloque de sillón)."""
    segs = []
    for s in trazos:
        c = s["color"]
        if c and c[1] > .6 and c[0] < .4 and c[2] < .4:
            segs += [x for x in s["segs"] if x[0] != x[1]]
    grupos = []
    for a, b in segs:
        m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        for g in grupos:
            if math.dist(m, g["c"]) < 12:
                g["segs"].append((a, b))
                break
        else:
            grupos.append(dict(c=m, segs=[(a, b)]))
    out = []
    for g in grupos:
        pts = [p for ab in g["segs"] for p in ab]
        hull = MultiPoint(pts).convex_hull
        if hull.area * co.k ** 2 < .02:
            continue
        L = min(math.dist(a, b) for a, b in g["segs"])
        lados = [(a, b) for a, b in g["segs"] if math.dist(a, b) < L * 1.2]
        a, b = lados[0]
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        out.append(dict(c=(hull.centroid.x, hull.centroid.y), ang=ang))
    return out


# ---------------------------------------------------------------- utilidades
def _rect(p):
    """Rectángulo mínimo: centro, eje largo (unitario), largo y ancho (pt)."""
    mr = p.minimum_rotated_rectangle
    c = list(mr.exterior.coords)
    e1 = np.subtract(c[1], c[0])
    e2 = np.subtract(c[2], c[1])
    l1, l2 = np.linalg.norm(e1), np.linalg.norm(e2)
    if l1 >= l2:
        return np.array(mr.centroid.coords[0]), e1 / l1, l1, l2
    return np.array(mr.centroid.coords[0]), e2 / l2, l2, l1


def _caja_local(o, u, v, u0, u1, v0, v1):
    """Polígono en pt de un rectángulo en el marco local (o, u, v), medidas en pt."""
    P = [o + u * a + v * b for a, b in ((u0, v0), (u1, v0), (u1, v1), (u0, v1))]
    return Polygon(P)


def _trap_local(o, u, v, u0, u1, w0, w1, c=0.0):
    P = [o + u * u0 + v * (c - w0), o + u * u1 + v * (c - w1),
         o + u * u1 + v * (c + w1), o + u * u0 + v * (c + w0)]
    return Polygon(P)


def _circulo(c, r):
    return Point(c).buffer(r, quad_segs=6)


def _circularidad(p):
    r = sh.minimum_bounding_radius(p)
    return p.area / (math.pi * r * r) if r > 0 else 0


# ---------------------------------------------------------------- clasificación
def clasificar(F, sillones, ambientes, cfg, co):
    m = 1 / co.k  # pt por metro
    def ambiente(p):
        q = p.representative_point()
        for r in ambientes:
            if r["geom"].contains(q):
                return r["rotulo"] or ""
        return ""

    piezas = []
    for f in F:
        p = f["geom"]
        if p.is_empty:
            continue
        capas = {k: v for k, v in f["capas"].items() if k not in cfg.CAPAS_NO_MUEBLE}
        if not capas and f["tipo"] != "asiento":
            continue
        capa = max(capas, key=capas.get) if capas else None
        c, eje, largo, ancho = _rect(p)
        piezas.append(dict(geom=p, tipo=f["tipo"], capa=capa, c=c, eje=eje,
                           L=largo / m, A=ancho / m, area=co.m2(p.area),
                           amb=ambiente(p), lineas=f["lineas"]))

    # Sillas M-39: el bloque llega en fragmentos (asiento, ruedas y el doble
    # arco del respaldo). Se agrupan; el centro del grupo es el asiento y el
    # respaldo va hacia los arcos (fragmentos alargados).
    frag = [q for q in piezas if q["capa"] == "ARQ-Mobiliarios" and q["area"] < .5]
    usados, grupos = set(), []
    for i, q in enumerate(frag):
        if i in usados:
            continue
        g, pend = [], [i]
        usados.add(i)
        while pend:
            k = pend.pop()
            g.append(frag[k])
            for j, r in enumerate(frag):
                if j not in usados and frag[k]["geom"].distance(r["geom"]) < .12 * m:
                    usados.add(j)
                    pend.append(j)
        grupos.append(g)
    piezas = [q for q in piezas if not any(q is r for r in frag)]
    for g in grupos:
        h = unary_union([q["geom"] for q in g]).convex_hull
        c, eje, largo, ancho = _rect(h)
        centro = np.array(h.centroid.coords[0])
        arcos = [q for q in g if q["L"] / max(q["A"], .01) > 2.5]
        respaldo = None
        if arcos:
            pa = np.mean([q["c"] for q in arcos], axis=0)
            if np.linalg.norm(pa - centro) > .03 * m:
                respaldo = (pa - centro) / np.linalg.norm(pa - centro)
        piezas.append(dict(geom=h, tipo="pieza", capa="ARQ-Mobiliarios", c=centro, eje=eje,
                           L=largo / m, A=ancho / m, area=co.m2(h.area),
                           amb=ambiente(h), lineas=[l for q in g for l in q["lineas"]],
                           forzar="silla", respaldo=respaldo))

    # Piezas pequeñas dibujadas sobre otro mueble (equipos sobre escritorios o
    # mostradores, pozas dentro de muebles): se modelan sobre la cubierta.
    grandes = [q for q in piezas if q["area"] > .6 and q["capa"] not in ("SANITARIOS", "ARQ-Sanitarios.") and q["tipo"] != "asiento"]
    for q in piezas:
        if q["tipo"] == "asiento" or q["area"] > .6 or (q.get("forzar") and q["forzar"] != "silla"):
            continue
        for gq in grandes:
            if gq is not q and q["geom"].intersection(gq["geom"]).area > .6 * q["geom"].area:
                q["forzar"] = "lavatorio_empotrado" if q["capa"] in ("SANITARIOS", "ARQ-Sanitarios.") else "sobre_mueble"
                break

    for q in piezas:
        q["clase"] = q.pop("forzar", None) or _clase(q, sillones, m)
    # Lavatorio dibujado dentro de un mostrador con poza (B-11): va empotrado.
    mostr = [q for q in piezas if q["clase"] == "mueble"]
    for q in piezas:
        if q["clase"] == "lavatorio" and any(q["geom"].intersection(t["geom"]).area > .5 * q["geom"].area for t in mostr):
            q["clase"] = "lavatorio_empotrado"
    # Símbolos duplicados (contorno exterior e interior del mismo lavatorio).
    lav = [q for q in piezas if q["clase"].startswith("lavatorio")]
    for q in lav:
        for r in lav:
            if r is not q and r["geom"].area >= q["geom"].area and q["geom"].intersection(r["geom"]).area > .5 * q["geom"].area and r["clase"] != "omitir":
                q["clase"] = "omitir"
                break
    return [q for q in piezas if q["clase"] != "omitir"]


def _clase(q, sillones, m):
    L, A, a, amb, capa = q["L"], q["A"], q["area"], q["amb"], q["capa"]
    if q["tipo"] == "asiento":
        return "asiento_espera"
    if any(q["geom"].distance(Point(s["c"])) < .05 * m for s in sillones) and a > 1:
        return "unidad_dental"
    en_bano = amb.startswith(BANOS)
    if capa in ("SANITARIOS", "ARQ-Sanitarios."):
        return "lavatorio" if L <= .75 else "mueble"
    if en_bano and capa == "EQUIPAMIENTO" and .45 <= L <= .75 and .3 <= A <= .62:
        return "inodoro"
    if ("sillón" in amb or "Módulo" in amb) and .28 <= A <= .34 and L <= .4:
        return "escupidera"
    if .3 <= A and L <= .7 and .08 <= a <= .32 and capa in ("EQUIPAMIENTO", "ARQ-Mobiliarios") and not en_bano:
        return "taburete" if _circularidad(q["geom"]) > .78 else "silla"
    if any(k in amb for k in ("Observación", "camillas", "Tópico")) and 1.6 <= L <= 2.3 and A >= .6:
        return "camilla" if A <= 1.0 else "camilla_con_otros"
    return "mueble"


def separar_camillas(piezas, co):
    """Piezas donde la camilla del plano toca otro elemento (silla, mesa de
    noche): se busca el rectángulo de la camilla entre las caras que forman
    sus líneas y el resto se clasifica aparte."""
    m = 1 / co.k
    salida = []
    for q in piezas:
        if q["clase"] != "camilla_con_otros":
            salida.append(q)
            continue
        caras = [f for f in polygonize(sh.union_all(np.array(q["lineas"]))) if co.m2(f.area) > .02]
        mejor = None
        for f in caras:
            c, eje, L, A = _rect(f)
            if L / m < 1.2 or not .6 <= A / m <= 1.0 or f.area / f.minimum_rotated_rectangle.area < .9:
                continue
            # La cabecera puede no cerrar como cara: se prolonga la franja de la
            # camilla a lo largo de su eje dentro de la pieza.
            n = np.array([-eje[1], eje[0]])
            franja = _caja_local(c, eje, n, -2.5 * m, 2.5 * m, -A / 2, A / 2)
            g = q["geom"].intersection(franja)
            g = max(getattr(g, "geoms", [g]), key=lambda x: x.area).minimum_rotated_rectangle
            _, _, L2, A2 = _rect(g)
            if 1.6 <= L2 / m <= 2.45 and (mejor is None or g.area > mejor.area):
                mejor = g
        if mejor is None:
            q["clase"] = "mueble"
            salida.append(q)
            continue
        c, eje, L, A = _rect(mejor)
        salida.append(dict(q, geom=mejor, c=c, eje=eje, L=L / m, A=A / m, clase="camilla"))
        resto = q["geom"].difference(mejor.buffer(.02 * m))
        for r in getattr(resto, "geoms", [resto]):
            if r.geom_type != "Polygon" or co.m2(r.area) < .04:
                continue
            c, eje, L, A = _rect(r)
            L, A = L / m, A / m
            clase = "silla" if (.3 <= A and L <= .7) else "mueble"
            salida.append(dict(q, geom=r, c=c, eje=eje, L=L, A=A, area=co.m2(r.area), clase=clase))
    return salida


# ---------------------------------------------------------------- modelos
class Constructor:
    def __init__(self, M, co, nivel, W=None):
        self.M, self.co, self.nivel, self.W = M, co, nivel, W
        self.cubiertas = []
        self.m = 1 / co.k
        self.partes = defaultdict(list)
        self.huellas = []      # (clase, ambiente, polígono en pt) para revisar choques
        self._actual = None

    def perfil(self, o, u, v, puntos_uh, v0, v1, mat, base=0.0):
        """Sólido con perfil (u, altura) en metros, extruido entre v0 y v1 (m).
        Permite superficies inclinadas (respaldos, apoyapiernas)."""
        cx, cy = self.co.centro
        k = self.co.k
        V = []
        for a, h in puntos_uh:
            for b in (v0, v1):
                p = o + (u * a + v * b) * self.m
                V.append(((p[0] - cx) * k, base + h, (p[1] - cy) * k))
        malla = trimesh.convex.convex_hull(np.array(V))
        if self._actual is not None:
            pts = [o + (u * a + v * b) * self.m for a, _ in puntos_uh for b in (v0, v1)]
            self._actual["polys"].append(MultiPoint(pts).convex_hull)
        self.partes[mat].append(malla)

    def empezar(self, clase, amb):
        self._actual = dict(clase=clase, amb=amb, polys=[])
        self.huellas.append(self._actual)

    def prisma(self, poly, y0, y1, mat, base=0.0):
        if self.W is not None and poly.intersects(self.W):
            poly = poly.difference(self.W)
        if poly.is_empty or poly.area <= 0:
            return
        if self._actual is not None:
            self._actual["polys"].append(poly)
        self.partes[mat].append(self.M.extruir(poly, base + y0, base + y1, mat))

    # Unidad dental -----------------------------------------------------
    def unidad_dental(self, q, s, piezas):
        m, g = self.m, np.array(s["c"])
        y = self.nivel(Point(g))
        dirs = [np.array([math.cos(s["ang"] + k * math.pi / 2), math.sin(s["ang"] + k * math.pi / 2)]) for k in range(4)]
        # El cuerpo del sillón está en la dirección donde el contorno del plano es más largo.
        def puntaje(d):
            seg = LineString([g + d * .3 * m, g + d * 1.5 * m])
            return seg.intersection(q["geom"]).length
        u = max(dirs, key=puntaje)
        v = np.array([-u[1], u[0]])
        # La escupidera / taburete del operador definen el lado del bloque.
        cerca = [p for p in piezas if p["clase"] in ("escupidera", "taburete") and np.linalg.norm(p["c"] - g) < 1.6 * m]
        if cerca:
            ref = min(cerca, key=lambda p: np.linalg.norm(p["c"] - g))
            if np.dot(ref["c"] - g, v) < 0:
                v = -v
        L = lambda a: a * m
        # Poste de la unidad (cuadrado verde)
        self.prisma(_caja_local(g, u, v, L(-.15), L(.15), L(-.15), L(.15)), 0, .75, "equipo_blanco", y)
        # Base y cuerpo del sillón (perfil lateral; respaldo reclinado)
        self.prisma(_caja_local(g, u, v, L(.62), L(1.12), L(-.17), L(.17)), 0, .32, "base_sillon", y)
        P = lambda pts, w, mat="tapiz": self.perfil(g, u, v, pts, -w, w, mat, y)
        P([(.22, .30), (.83, .36), (.83, .50), (.22, .42)], .23)               # apoyapiernas
        P([(.83, .34), (1.07, .34), (1.07, .53), (.83, .51)], .26)             # asiento
        P([(1.03, .42), (1.13, .38), (1.44, .84), (1.36, .92)], .24)           # respaldo
        P([(1.40, .86), (1.64, .90), (1.62, .99), (1.38, .96)], .12)           # cabezal
        for lado in (-1, 1):  # apoyabrazos
            self.prisma(_caja_local(g, u, v, L(.72), L(1.04), L(lado * .27), L(lado * .33)), .50, .58, "base_sillon", y)
        # Brazo y bandeja de instrumental (lado opuesto a la escupidera)
        self.prisma(_caja_local(g, u, v, L(-.05), L(.05), L(-.62), L(-.15)), .78, .82, "acero", y)
        self.prisma(_caja_local(g, u, v, L(-.05), L(.35), L(-.88), L(-.55)), .80, .84, "equipo_blanco", y)

    def escupidera(self, q, y):
        c = q["c"]
        self.prisma(_circulo(c, .06 * self.m), 0, .72, "equipo_blanco", y)
        self.prisma(_circulo(c, .15 * self.m), .72, .80, "ceramica", y)

    def _despejar(self, c, r):
        """Aleja de los muros un elemento circular de radio r (m) si los toca."""
        m = self.m
        for _ in range(8):
            circ = _circulo(c, r * m)
            if not circ.intersects(self.W):
                break
            pm = np.array(nearest_points(self.W, Point(c))[0].coords[0])
            d = c - pm
            if np.linalg.norm(d) < 1e-6:
                break
            c = c + d / np.linalg.norm(d) * .02 * m
        return c

    def taburete(self, q, y, r=None):
        c, m = np.array(q["c"]), self.m
        if r is None:
            r = float(np.clip(sh.minimum_bounding_radius(q["geom"]) / m * .85, .18, .28))
        c = self._despejar(c, r)
        for k in range(5):
            a = 2 * math.pi * k / 5
            d = np.array([math.cos(a), math.sin(a)])
            self.prisma(LineString([c, c + d * r * .95 * m]).buffer(.022 * m, cap_style="flat"), .03, .07, "base_sillon", y)
            self.prisma(_circulo(c + d * r * .92 * m, .025 * m), 0, .03, "base_sillon", y)
        self.prisma(_circulo(c, .028 * m), .07, .44, "acero", y)
        self.prisma(_circulo(c, min(r * .85, .23) * m), .44, .51, "tapiz", y)
        return c

    def silla(self, q, y, mesas):
        """Silla con respaldo. Para el bloque M-39 el respaldo va hacia el doble
        arco del plano; para las demás, hacia el lado donde el arco desplaza el
        centroide del símbolo; si es simétrico, mira a la mesa más cercana."""
        p, m = q["geom"], self.m
        r = float(np.clip(sh.minimum_bounding_radius(p) / m * .8, .2, .28))
        b = q.get("respaldo")
        cc = np.array(q["c"]) if b is not None else np.array(sh.minimum_bounding_circle(p).centroid.coords[0])
        if b is None:
            off = np.array(p.centroid.coords[0]) - cc
            if np.linalg.norm(off) > .015 * m:
                b = off / np.linalg.norm(off)
            else:
                cerca = [t for t in mesas if t.distance(Point(cc)) < 1.2 * m]
                if cerca:
                    t = min(cerca, key=lambda t: t.distance(Point(cc)))
                    n = np.array(nearest_points(t, Point(cc))[0].coords[0])
                    b = (cc - n) / max(np.linalg.norm(cc - n), 1e-6)
                else:
                    b = q["eje"]
        n = np.array([-b[1], b[0]])
        respaldo = lambda c: _caja_local(c + b * (r * .85) * m, b, n, 0, .05 * m, -.21 * m, .21 * m)
        # El respaldo no puede quedar dentro de un mueble: si pasa, se gira.
        if any(respaldo(cc).intersects(t) for t in mesas):
            b, n = -b, -n
        cc = self._despejar(cc, r + .05)
        c = self.taburete(dict(c=cc, geom=p), y, r)
        self.prisma(respaldo(c), .55, .92, "tapiz", y)
        self.prisma(_caja_local(c + b * (r * .55) * m, b, n, 0, .3 * r * m, -.02 * m, .02 * m), .48, .60, "base_sillon", y)

    def asiento_espera(self, q, y, asientos):
        p, m = q["geom"], self.m
        if q["A"] < .25:
            # Celda de respaldo: panel delgado inclinado hacia afuera del asiento.
            vec = [r for r in asientos if r["A"] >= .25 and r["geom"].distance(p) < .03 * m]
            if not vec:
                self.prisma(p, .46, .86, "tapiz", y)
                return
            s_ = min(vec, key=lambda r: r["geom"].distance(p))
            d = q["c"] - s_["c"]
            d = d / np.linalg.norm(d)
            e = np.array([-d[1], d[0]])
            t = q["A"] / 2
            w = q["L"] / 2 - .02
            self.perfil(q["c"], d, e, [(-t + .02, .44), (-t + .07, .44), (t, .86), (t - .05, .87)], -w, w, "tapiz", y)
            # Soportes del respaldo
            for sv in (-1, 1):
                self.perfil(q["c"], d, e, [(-t - .05, .40), (-t - .01, .40), (t - .02, .80), (t - .06, .80)], sv * w - .015, sv * w + .015, "acero", y)
            return
        self.prisma(p.buffer(-.015 * m, join_style="mitre"), .41, .47, "tapiz", y)

    def filas_tandem(self, asientos):
        """Sillas tándem: una viga bajo cada fila de asientos y patas en los
        extremos y cada dos asientos (estándar de salas de espera; el plano no
        dibuja la estructura)."""
        m = self.m
        sillas = [q for q in asientos if q["A"] >= .25]
        usados, filas = set(), []
        for i, q in enumerate(sillas):
            if i in usados:
                continue
            fila, pend = [], [i]
            usados.add(i)
            while pend:
                k = pend.pop()
                fila.append(sillas[k])
                for j, r in enumerate(sillas):
                    if j not in usados and sillas[k]["geom"].distance(r["geom"]) < .05 * m \
                            and abs(np.dot(sillas[k]["eje"], r["eje"])) > .95:
                        usados.add(j)
                        pend.append(j)
            filas.append(fila)
        for fila in filas:
            self.empezar("estructura_tandem", fila[0]["amb"])
            y = self.nivel(Point(fila[0]["c"]))
            e = fila[0]["eje"]
            n = np.array([-e[1], e[0]])
            c0 = np.mean([q["c"] for q in fila], axis=0)
            t = sorted(np.dot(q["c"] - c0, e) for q in fila)
            w = fila[0]["L"] * m
            ini, fin = t[0] - w / 2 + .03 * m, t[-1] + w / 2 - .03 * m
            self.prisma(_caja_local(c0, e, n, ini, fin, -.03 * m, .03 * m), .35, .40, "acero", y)
            k = len(fila)
            pos = [ini] + [t[0] - w / 2 + w * j for j in range(2, k, 2)] + [fin]
            for x in pos:
                x = min(max(x, ini + .03 * m), fin - .03 * m)
                self.prisma(_caja_local(c0, e, n, x - .025 * m, x + .025 * m, -.025 * m, .025 * m), .03, .35, "acero", y)
                self.prisma(_caja_local(c0, e, n, x - .03 * m, x + .03 * m, -.24 * m, .24 * m), 0, .03, "acero", y)

    def camilla(self, q, y):
        c, u, Lg, An = q["c"], q["eje"], q["L"] * self.m, q["A"] * self.m
        v = np.array([-u[1], u[0]])
        a, b = Lg / 2, An / 2
        g = .04 * self.m
        self.prisma(_caja_local(c, u, v, -a + g, a - g, -b + g, b - g), .45, .58, "acero", y)
        self.prisma(_caja_local(c, u, v, -a + g, a - g, -b + g, b - g), .58, .70, "colchon", y)
        for su in (-1, 1):
            for sv in (-1, 1):
                self.prisma(_circulo(c + u * su * (a - 3 * g) + v * sv * (b - 3 * g), .025 * self.m), 0, .45, "acero", y)

    def inodoro(self, q, y):
        p = q["geom"]
        self.prisma(p.buffer(-.01 * self.m, join_style="mitre"), 0, .38, "ceramica", y)
        self.prisma(p, .38, .42, "ceramica", y)

    def lavatorio(self, q, y):
        self.prisma(q["geom"], .74, .86, "ceramica", y)

    def lavatorio_empotrado(self, q, y):
        h = self.altura_en(q["geom"])
        self.prisma(q["geom"].buffer(-.01 * self.m, join_style="mitre"), h, h + .006, "ceramica", y)

    def sobre_mueble(self, q, y):
        """Equipo dibujado sobre un mueble: bloque bajo apoyado en la cubierta.
        El plano no indica qué equipo es ni su altura."""
        h = self.altura_en(q["geom"])
        self.prisma(q["geom"].buffer(-.01 * self.m, join_style="mitre"), h, h + .06, "equipo_blanco", y)

    def altura_en(self, g):
        for poly, h in self.cubiertas:
            if poly.intersection(g).area > .5 * g.area:
                return h
        return .75

    @staticmethod
    def altura_mueble(q):
        return .90 if (q["A"] <= .7 and q["L"] >= 1.0) or q["capa"] in ("SANITARIOS", "ARQ-Sanitarios.") else .75

    def mueble(self, q, y):
        p = q["geom"]
        if q["A"] < .12:      # barras, rieles y piezas muy delgadas
            self.prisma(p, .80, .92, "acero", y)
            return
        h = self.altura_mueble(q)
        partes = [p]
        if q["area"] > 1.2 and q["A"] > .9:
            # Agrupación de varios elementos: cada cara cerrada del plano por
            # separado y con una pequeña separación entre ellas.
            caras = [f for f in polygonize(sh.union_all(np.array(q["lineas"]))) if f.area * self.co.k ** 2 > .03]
            caras = [f for f in caras if not any(g is not f and g.contains(f.representative_point()) and g.area > f.area for g in caras)]
            if caras and sum(f.area for f in caras) > .5 * p.area:
                partes = [f.buffer(-.01 * self.m, join_style="mitre") for f in caras]
        for p in partes:
            if p.is_empty:
                continue
            interior = p.buffer(-.03 * self.m, join_style="mitre")
            self.prisma(interior if not interior.is_empty else p, 0, h - .04, "mueble", y)
            self.prisma(p, h - .04, h, "cubierta", y)

    def exportar(self):
        for mat, mallas in self.partes.items():
            self.M.agregar("mobiliario_" + mat, trimesh.util.concatenate(mallas), mat)


def construir(M, F, sillones, ambientes, nivel, cfg, co, W=None):
    piezas = clasificar(F, sillones, ambientes, cfg, co)
    K = Constructor(M, co, nivel, W)
    piezas = separar_camillas(piezas, co)
    K.cubiertas = [(q["geom"], K.altura_mueble(q)) for q in piezas if q["clase"] == "mueble"]
    mesas = [q["geom"] for q in piezas if q["clase"] == "mueble"]
    asientos = [q for q in piezas if q["clase"] == "asiento_espera"]
    cuenta = defaultdict(int)
    for s in sillones:
        cand = [q for q in piezas if q["clase"] == "unidad_dental" and q["geom"].distance(Point(s["c"])) < .05 / co.k]
        if cand:
            K.empezar("unidad_dental", cand[0]["amb"])
            K.unidad_dental(cand[0], s, piezas)
            cuenta["unidad_dental"] += 1
    for q in piezas:
        if q["clase"] == "unidad_dental":
            continue
        y = nivel(q["geom"].representative_point())
        c = q["clase"]
        K.empezar(c, q["amb"])
        if c == "silla":
            K.silla(q, y, mesas)
        elif c == "asiento_espera":
            K.asiento_espera(q, y, asientos)
        else:
            getattr(K, c)(q, y)
        cuenta[c] += 1
    K.filas_tandem(asientos)
    K.exportar()
    construir.huellas = [dict(clase=h["clase"], amb=h["amb"], geom=unary_union(h["polys"]))
                         for h in K.huellas if h["polys"]]
    return dict(cuenta)
