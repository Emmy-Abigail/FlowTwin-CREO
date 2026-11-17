"""Genera el modelo 3D del gemelo digital a partir de un plano PDF exportado de AutoCAD.

Uso:
    python generar.py --pdf "../../PLANOS_COMPLETOS/A07_PISO 01.pdf" --salida salida
    python generar.py --pdf ... --referencia     # solo la imagen de referencia con cuadrícula
"""
import argparse
import importlib
import sys
from pathlib import Path

from generador.coords import Coords
from generador.extraer import leer_pdf
from generador.muros import detectar_muros
from generador.ambientes import puertas_de_arcos, mamparas_y_cortinas, dividir_ambientes
from generador.mobiliario import detectar_mobiliario
from generador.muebles import simbolos_sillon
from generador.exportar import construir, guardar_json, guardar_mapa
from generador.referencia import guardar_referencia
from generador.visor import guardar_visor
from shapely.geometry import box


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--pdf", required=True, help="Plano PDF de AutoCAD (con capas)")
    ap.add_argument("--config", default="config_piso1", help="Módulo de configuración del piso")
    ap.add_argument("--salida", default="salida", help="Carpeta de salida")
    ap.add_argument("--nombre", default="clinica-piso1", help="Nombre base de los archivos")
    ap.add_argument("--referencia", action="store_true", help="Generar solo la imagen de referencia")
    ap.add_argument("--sin-visor", action="store_true", help="No generar el visor HTML")
    a = ap.parse_args()

    cfg = importlib.import_module(a.config)
    co = Coords(cfg)
    salida = Path(a.salida)
    salida.mkdir(parents=True, exist_ok=True)

    print("1/6 Leyendo el PDF…")
    pdf = leer_pdf(a.pdf, cfg.PDF_PAGINA)
    if a.referencia:
        guardar_referencia(pdf["pagina"], cfg, salida / "referencia.png")
        print(f"Listo: {salida / 'referencia.png'}")
        return

    print("2/6 Detectando muros…")
    W = detectar_muros(pdf["trazos"], cfg, co)
    b = W.bounds
    co.centro = ((b[0] + b[2]) / 2, (b[1] + b[3]) / 2)

    print("3/6 Detectando puertas, vidrios y mamparas…")
    puertas = puertas_de_arcos(pdf["arcos"], W, co)
    vidrios = [box(*co.ref(*r[:2]), *co.ref(*r[2:])) for r in cfg.VIDRIOS]
    mamparas, cortinas, borde = mamparas_y_cortinas(pdf["trazos"], cfg, co)

    print("4/6 Dividiendo ambientes…")
    contorno, ambientes, sin_zona = dividir_ambientes(W, puertas, vidrios, mamparas, cortinas, borde, cfg, co)
    if sin_zona:
        print("  AVISO: rótulos que no caen en ningún ambiente:", ", ".join(sin_zona))

    print("5/6 Detectando mobiliario (tarda un poco)…")
    mobiliario = detectar_mobiliario(pdf["trazos"], pdf["arcos"], puertas, W, contorno, cfg, co)

    print("6/6 Exportando…")
    d = dict(W=W, contorno=contorno, ambientes=ambientes, puertas=puertas, vidrios=vidrios,
             mamparas=mamparas, cortinas=cortinas, mobiliario=mobiliario,
             sillones=simbolos_sillon(pdf["trazos"], co))
    escena, lista, cuenta = construir(d, cfg, co)
    glb = salida / f"{a.nombre}.glb"
    escena.export(glb)
    guardar_json(lista, salida / f"{a.nombre}-ambientes.json", Path(a.pdf).name, cfg)
    guardar_mapa(d, salida / f"{a.nombre}-mapa.png", cfg)
    if not a.sin_visor:
        guardar_visor(glb, lista, salida / f"{a.nombre}-visor.html")

    print(f"\nListo en {salida}/")
    print(f"  {len(ambientes)} ambientes, {len(puertas)} puertas")
    print("  mobiliario:", ", ".join(f"{v} {k}" for k, v in sorted(cuenta.items())))
    print(f"  {glb.name}: {glb.stat().st_size / 1e6:.1f} MB, {len(escena.geometry)} objetos")


if __name__ == "__main__":
    sys.exit(main())
