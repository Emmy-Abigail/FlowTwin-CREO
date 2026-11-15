"""Visor HTML autocontenido para revisar el modelo sin levantar Angular."""
import base64
import json
from pathlib import Path


def guardar_visor(ruta_glb, lista, ruta):
    plantilla = (Path(__file__).parent / "visor_plantilla.html").read_text(encoding="utf-8")
    glb = base64.b64encode(Path(ruta_glb).read_bytes()).decode()
    html = plantilla.replace("__AMB__", json.dumps(lista, ensure_ascii=False)).replace("__GLB__", glb)
    Path(ruta).write_text("<!doctype html><meta charset=\"utf-8\">\n" + html, encoding="utf-8")
