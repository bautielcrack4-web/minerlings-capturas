"""Edificio de Tripo -> Unity: normaliza (huella de `--size` metros, suelo en y=0, frente hacia la camara del juego),
hunde la base de tierra, renderiza una vista previa y exporta malla (pos, normal, uv) en JSON + textura PNG.

Uso: blender -b -P tripo_building.py -- out/tripo_b/casa.glb --name house --size 3.2 [--yaw 0] [--sink 0.08]
         [--outdir ../../unity_miner/Assets/Resources/Island] [--preview out/tripo_b/house_r.png]
"""
import bpy, sys, os, math, json, argparse
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
ap = argparse.ArgumentParser()
ap.add_argument("glb")
ap.add_argument("--name", required=True)
ap.add_argument("--size", type=float, default=3.2)
ap.add_argument("--yaw", type=float, default=0.0)
ap.add_argument("--sink", type=float, default=0.08, help="fraccion de la altura que se hunde (base de tierra)")
ap.add_argument("--outdir", default="../../unity_miner/Assets/Resources/Island")
ap.add_argument("--preview", default="")
ap.add_argument("--tex", type=int, default=1024)
a = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=os.path.abspath(a.glb))
meshes = [o for o in scene.objects if o.type == "MESH"]
for o in scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
mw = obj.matrix_world.copy(); obj.parent = None; obj.matrix_world = mw
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o)
me = obj.data
if abs(a.yaw) > 0.01:
    me.transform(Matrix.Rotation(math.radians(a.yaw), 4, "Z"))
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
foot = max(max(xs) - min(xs), max(ys) - min(ys))
s = a.size / foot
me.transform(Matrix.Translation((-(max(xs) + min(xs)) / 2, -(max(ys) + min(ys)) / 2, -min(zs))))
me.transform(Matrix.Scale(s, 4))
h = (max(zs) - min(zs)) * s
me.transform(Matrix.Translation((0, 0, -a.sink * h)))
me.update()
for p in me.polygons:
    p.use_smooth = False   # low-poly: caras planas (se ve mejor que suavizado en P1)
print("NORM", a.name, "alto %.2f m" % (h * (1 - a.sink)), "caras", len(me.polygons))

if a.preview:
    world = bpy.data.worlds.new("F"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    l = bpy.data.lights.new("Sol", "SUN"); l.energy = 3.0
    lo = bpy.data.objects.new("Sol", l); lo.rotation_euler = (math.radians(50), 0, math.radians(35)); scene.collection.objects.link(lo)
    cd = bpy.data.cameras.new("C"); cd.type = "ORTHO"; cd.ortho_scale = a.size * 1.6
    cam = bpy.data.objects.new("C", cd); scene.collection.objects.link(cam); scene.camera = cam
    # misma orientacion que la camara del juego (pitch 48, yaw 35); Unity Z+ = Blender -Y
    d = Vector((math.sin(math.radians(35)) * math.cos(math.radians(48)), -math.cos(math.radians(35)) * math.cos(math.radians(48)), math.sin(math.radians(48))))
    cam.location = d * 20 + Vector((0, 0, h * 0.4))
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "CYCLES"; scene.cycles.samples = 24; scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.film_transparent = True
    scene.render.filepath = os.path.abspath(a.preview)
    bpy.ops.render.render(write_still=True)

# ------------------------------------------------------------ export
img = None
mat = me.materials[0] if me.materials else None
if mat and mat.use_nodes:
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            img = n.image
            break
outdir = os.path.abspath(a.outdir)
os.makedirs(outdir, exist_ok=True)
if img:
    img.scale(a.tex, a.tex)
    img.filepath_raw = os.path.join(outdir, a.name + ".png")
    img.file_format = "PNG"
    img.save()
me.calc_loop_triangles()
uvl = me.uv_layers.active.data if me.uv_layers.active else None
pos, nrm, uv, idx = [], [], [], []
weld = {}
for tri in me.loop_triangles:
    n = tri.normal
    for li in reversed(tri.loops):   # el cambio de ejes refleja: invertir el orden
        vi = me.loops[li].vertex_index
        u = tuple(uvl[li].uv) if uvl else (0.0, 0.0)
        key = (vi, round(u[0], 4), round(u[1], 4), round(n.x, 3), round(n.y, 3), round(n.z, 3))
        j = weld.get(key)
        if j is None:
            j = len(pos) // 3
            weld[key] = j
            c = me.vertices[vi].co
            # Blender (x, y, z) -> Unity (-x, z, -y): el frente del modelo (-Y en Blender) queda mirando a -Z
            pos += [round(-c.x, 4), round(c.z, 4), round(-c.y, 4)]
            nrm += [round(-n.x, 3), round(n.z, 3), round(-n.y, 3)]
            uv += [round(u[0], 4), round(u[1], 4)]
        idx.append(j)
with open(os.path.join(outdir, a.name + ".json"), "w") as f:
    json.dump({"pos": pos, "nrm": nrm, "uv": uv, "idx": idx, "height": h * (1 - a.sink)}, f, separators=(",", ":"))
print("EXPORT", a.name, "tris", len(idx) // 3, "verts", len(pos) // 3, "tex", bool(img))
