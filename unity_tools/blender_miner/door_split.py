"""Puerta en pedazos (PartCrafter) -> dos GLB: el marco (pared con el hueco) y la hoja.

La hoja es el objeto que se llama hoja/leaf/door, o si no el mas chico de los dos mas grandes. Uso:
blender -b -P door_split.py -- <tema>.glb <carpeta_salida>   (escribe <tema>_marco.glb y <tema>_hoja.glb)
"""
import bpy, sys, os

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = os.path.abspath(argv[0]), os.path.abspath(argv[1])
name = os.path.splitext(os.path.basename(src))[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if len(meshes) < 2: sys.exit("ERROR: %s tiene una sola pieza (no esta separada la hoja)" % name)


def vol(o):
    d = o.dimensions
    return d.x * d.y * d.z


named = [o for o in meshes if any(k in o.name.lower() for k in ("hoja", "leaf", "door"))]
leaf = named[0] if named else sorted(sorted(meshes, key=vol)[-2:], key=vol)[0]
os.makedirs(out, exist_ok=True)
for part, objs in (("hoja", [leaf]), ("marco", [o for o in meshes if o is not leaf])):
    for o in bpy.context.scene.objects: o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, "%s_%s.glb" % (name, part)), export_format="GLB", use_selection=True)
    print("EXPORT", name, part, len(objs), "objetos")
