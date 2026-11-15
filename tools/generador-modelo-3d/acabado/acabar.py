"""Acabado realista del modelo con Blender (bpy): materiales con textura,
aristas suavizadas y oclusión ambiental horneada (sombras de contacto).

Uso (con el entorno de Blender, ver README):
    python acabado/acabar.py --glb salida/clinica-piso1.glb \
        --ambientes salida/clinica-piso1-ambientes.json \
        --salida salida/clinica-piso1-final.glb

La geometría, los nombres de los objetos (ambiente_XX, puerta_XX, ...) y las
posiciones no cambian: solo se agregan materiales, coordenadas UV y la
textura de oclusión. Los acabados (vinílico, porcelanato, madera...) no
vienen en el plano: son acabados estándar de clínica, configurables en
ACABADOS y PISOS_PORCELANATO.
"""
import argparse
import json
import os
import sys
import time

import bpy  # noqa: I001  (bpy debe importarse antes que bmesh)
import bmesh
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from texturas import generar as generar_texturas  # noqa: E402

# material del generador -> (textura, color base RGB, rugosidad, metalicidad)
ACABADOS = {
    "muro": ("muro", (.86, .85, .82), .9, 0),
    "ducto": ("muro", (.80, .80, .79), .9, 0),
    "corte": (None, (.18, .19, .21), .9, 0),
    "zocalo": (None, (.52, .52, .50), .5, 0),
    "piso": ("vinilico", (.70, .68, .64), .55, 0),
    "zona": ("vinilico", (.70, .68, .64), .55, 0),
    "base": ("concreto", (.66, .66, .64), .95, 0),
    "escalera": ("concreto", (.74, .74, .72), .85, 0),
    "puerta": ("madera", (.62, .45, .30), .55, 0),
    "mueble": ("madera", (.70, .56, .41), .6, 0),
    "cubierta": ("laminado", (.93, .92, .89), .45, 0),
    "tapiz": ("tapiz", (.20, .42, .50), .85, 0),
    "colchon": ("tapiz", (.85, .89, .91), .9, 0),
    "acero": ("acero", (.72, .73, .75), .35, .8),
    "base_sillon": (None, (.28, .29, .31), .5, .3),
    "equipo_blanco": (None, (.92, .93, .92), .35, 0),
    "ceramica": (None, (.96, .96, .95), .12, 0),
}
TRANSPARENTES = {"vidrio", "cortina", "mampara"}
# Ambientes con piso de porcelanato (por su rótulo); el resto es vinílico.
PISOS_PORCELANATO = ("SSHH", "SH", "Cto. de limpieza", "Cuarto séptico")
COLOR_PORCELANATO = (.80, .80, .78)
BISEL = {"muros": .008, "mobiliario_mueble": .006}


def material_base(obj):
    m = obj.active_material
    return m.name.split(".")[0].replace("_original", "") if m else None


def nuevo_material(nombre, tex, color, rug, met, imagenes, ao_img):
    m = bpy.data.materials.new(nombre)
    m.use_nodes = True
    m.use_backface_culling = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = rug
    bsdf.inputs["Metallic"].default_value = met
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if tex:
        uv = nt.nodes.new("ShaderNodeUVMap")
        uv.uv_map = "UVMap"
        img = nt.nodes.new("ShaderNodeTexImage")
        img.image = imagenes[tex]
        nt.links.new(uv.outputs["UV"], img.inputs["Vector"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = (*color, 1)
        nt.links.new(img.outputs["Color"], mix.inputs[6])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*color, 1)
    # Oclusión horneada (salida glTF: occlusionTexture en TEXCOORD_1)
    uv2 = nt.nodes.new("ShaderNodeUVMap")
    uv2.uv_map = "lightmap"
    ao = nt.nodes.new("ShaderNodeTexImage")
    ao.image = ao_img
    ao.name = "AO"
    nt.links.new(uv2.outputs["UV"], ao.inputs["Vector"])
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(ao.outputs["Color"], sep.inputs["Color"])
    grupo = nt.nodes.new("ShaderNodeGroup")
    grupo.node_tree = grupo_gltf()
    nt.links.new(sep.outputs[0], grupo.inputs["Occlusion"])
    nt.nodes.active = ao
    return m


def grupo_gltf():
    g = bpy.data.node_groups.get("glTF Material Output")
    if g:
        return g
    g = bpy.data.node_groups.new("glTF Material Output", "ShaderNodeTree")
    g.interface.new_socket("Occlusion", in_out="INPUT", socket_type="NodeSocketFloat")
    g.nodes.new("NodeGroupInput")
    return g


def quitar_caras_ocultas(obj):
    """Elimina las caras que miran hacia abajo a nivel de piso o más abajo
    (bases de losas, muros y muebles): nunca se ven y ocuparían espacio en la
    textura de oclusión."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    mw = obj.matrix_world
    rot = mw.to_3x3()
    borrar = [f for f in bm.faces if (rot @ f.normal).normalized().z < -.9 and (mw @ f.calc_center_median()).z < .02]
    bmesh.ops.delete(bm, geom=borrar, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()


def uv_caja(obj, escala):
    """UV0 en metros (proyección por caras), dividida por el tamaño de repetición."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    capa = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    mw = obj.matrix_world
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        i, j = [(1, 2), (0, 2), (0, 1)][ax]
        for lp in f.loops:
            co = mw @ lp.vert.co
            lp[capa].uv = (co[i] / escala, co[j] / escala)
    bm.to_mesh(me)
    bm.free()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--glb", required=True)
    ap.add_argument("--ambientes", required=True)
    ap.add_argument("--salida", required=True)
    ap.add_argument("--resolucion", type=int, default=2048, help="Tamaño de la textura de oclusión")
    ap.add_argument("--muestras", type=int, default=48)
    a = ap.parse_args()
    t0 = time.time()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(a.glb))
    for m_ in bpy.data.materials:
        m_.name = m_.name + "_original"
    rotulos = {x["id"]: x["rotulo_en_plano"] or "" for x in json.load(open(a.ambientes, encoding="utf-8"))["ambientes"]}

    carpeta = os.path.join(os.path.dirname(os.path.abspath(a.salida)), "texturas")
    os.makedirs(carpeta, exist_ok=True)
    reps = generar_texturas(carpeta)
    imagenes = {k: bpy.data.images.load(os.path.join(carpeta, f"{k}.jpg")) for k in reps}
    ao_img = bpy.data.images.new("oclusion", a.resolucion, a.resolucion)
    ao_img.generated_color = (1, 1, 1, 1)

    mats = {}
    def mat(clave, tex, color, rug, met):
        if clave not in mats:
            mats[clave] = nuevo_material(clave, tex, color, rug, met, imagenes, ao_img)
        return mats[clave]

    opacos = []
    for obj in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        base = material_base(obj)
        if base in TRANSPARENTES or base is None:
            continue
        if base not in ACABADOS:
            print("  sin acabado:", obj.name, base)
            continue
        tex, color, rug, met = ACABADOS[base]
        clave = base
        if obj.name.startswith("ambiente_") and rotulos.get(obj.name, "").startswith(PISOS_PORCELANATO):
            tex, color, clave = "porcelanato", COLOR_PORCELANATO, "zona_porcelanato"
        nombre = {"zona": "piso_vinilico", "zona_porcelanato": "piso_porcelanato"}.get(clave, clave)
        obj.data.materials.clear()
        obj.data.materials.append(mat(nombre, tex, color, rug, met))
        if obj.name in BISEL:
            bv = obj.modifiers.new("bisel", "BEVEL")
            bv.width = BISEL[obj.name]
            bv.segments = 1
            bv.limit_method = "ANGLE"
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier="bisel")
        quitar_caras_ocultas(obj)
        uv_caja(obj, reps[tex] if tex else 1.0)
        if "lightmap" not in obj.data.uv_layers:
            obj.data.uv_layers.new(name="lightmap")
        opacos.append(obj)
    print(f"Materiales listos ({len(opacos)} objetos) en {time.time() - t0:.0f} s")

    # UV de oclusión: todas las piezas en un solo atlas
    bpy.ops.object.select_all(action="DESELECT")
    for o in opacos:
        o.select_set(True)
        o.data.uv_layers.active = o.data.uv_layers["lightmap"]
    bpy.context.view_layer.objects.active = opacos[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.0015, rotate=True, scale=True, shape_method="AABB")
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in opacos:
        o.data.uv_layers.active = o.data.uv_layers["UVMap"]
        o.data.uv_layers["UVMap"].active_render = True
    print(f"Atlas de oclusión listo en {time.time() - t0:.0f} s")

    # Horneado de oclusión ambiental (Cycles, CPU)
    esc = bpy.context.scene
    esc.render.engine = "CYCLES"
    esc.cycles.device = "CPU"
    esc.cycles.samples = a.muestras
    if esc.world is None:
        esc.world = bpy.data.worlds.new("mundo")
    esc.world.light_settings.distance = 1.2
    for o in bpy.context.scene.objects:
        if o.type == "MESH" and o not in opacos:
            o.hide_render = True
    esc.render.bake.margin = 6
    for o in opacos:
        o.data.uv_layers.active = o.data.uv_layers["lightmap"]
    bpy.ops.object.bake(type="AO", margin=6, use_clear=True)
    for o in opacos:
        o.data.uv_layers.active = o.data.uv_layers["UVMap"]
    for o in bpy.context.scene.objects:
        o.hide_render = False
    print(f"Oclusión horneada en {time.time() - t0:.0f} s")

    # Suavizado y aclarado: sombra de contacto suave, sin oscurecer en exceso
    px = np.array(ao_img.pixels[:], dtype=np.float32).reshape(a.resolucion, a.resolucion, 4)
    ao = px[..., 0]
    from PIL import Image, ImageFilter
    im = Image.fromarray((np.clip(ao, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.4))
    ao = np.asarray(im, dtype=np.float32) / 255
    ao = 1 - .8 * (1 - ao)
    px[..., 0] = px[..., 1] = px[..., 2] = ao
    ao_img.pixels[:] = px.ravel()
    ruta_ao = os.path.join(carpeta, "oclusion.png")
    ao_img.filepath_raw = ruta_ao
    ao_img.file_format = "PNG"
    ao_img.save()

    bpy.ops.export_scene.gltf(filepath=os.path.abspath(a.salida), export_format="GLB",
                              export_image_format="JPEG", export_jpeg_quality=88,
                              export_yup=True, export_texcoords=True, export_normals=True,
                              export_materials="EXPORT")
    print(f"Listo: {a.salida} ({os.path.getsize(a.salida) / 1e6:.1f} MB) en {time.time() - t0:.0f} s")


if __name__ == "__main__":
    main()
