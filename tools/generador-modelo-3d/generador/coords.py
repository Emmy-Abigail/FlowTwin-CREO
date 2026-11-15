"""Conversión entre coordenadas de referencia, puntos PDF y metros."""
import numpy as np
import shapely as sh


class Coords:
    def __init__(self, cfg):
        self.cfg = cfg
        self.k = cfg.M_POR_PT
        self.centro = None  # se fija cuando se conocen los muros

    def ref(self, x, y):
        """Coordenada de la imagen de referencia -> punto PDF."""
        ox, oy = self.cfg.ORIGEN_PT
        return (ox + x * self.cfg.PT_POR_REF, oy + y * self.cfg.PT_POR_REF)

    def m2(self, area_pt):
        return area_pt * self.k * self.k

    def a_mundo(self, geom):
        """Punto PDF -> metros, centrado en el origen. Al extruir, este plano pasa a ser el XZ de Three.js."""
        cx, cy = self.centro
        k = self.k
        return sh.transform(geom, lambda c: np.c_[(c[:, 0] - cx) * k, -(c[:, 1] - cy) * k])
