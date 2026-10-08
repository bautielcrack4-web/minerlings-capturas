"""Humanoide de prueba en pose T con esqueleto de nombres genericos (como sale de UniRig) y textura, para validar
rig_export.py y el minero con esqueleto del juego sin esperar a los modelos reales.

Uso: blender -b -P rig_test_model.py -- out/rig_test.glb
"""
import bpy, sys, os, math, bmesh
from mathutils import Vector

out = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- malla (frente hacia -Y, alto 1.8)
parts = [  # (tipo, posicion, escala, color)
    ("sphere", (0, 0, 1.52), (0.26, 0.24, 0.26), (0.96, 0.78, 0.62)),   # cabeza
    ("sphere", (0, -0.03, 1.66), (0.29, 0.27, 0.16), (0.95, 0.72, 0.15)),  # casco
    ("sphere", (0, -0.22, 1.52), (0.05, 0.05, 0.05), (0.85, 0.55, 0.45)),  # nariz (frente)
    ("cube", (0, 0, 1.08), (0.22, 0.14, 0.24), (0.25, 0.55, 0.85)),     # pecho
    ("cube", (0, 0, 0.82), (0.2, 0.13, 0.08), (0.45, 0.32, 0.2)),       # cadera
]
for s in (-1, 1):
    parts += [("cyl_x", (s * 0.37, 0, 1.25), (0.16, 0.06, 0.06), (0.25, 0.55, 0.85)),    # brazo
              ("cyl_x", (s * 0.66, 0, 1.25), (0.14, 0.05, 0.05), (0.96, 0.78, 0.62)),    # antebrazo
              ("sphere", (s * 0.84, 0, 1.25), (0.07, 0.07, 0.07), (0.7, 0.45, 0.25)),    # mano
              ("cyl_z", (s * 0.11, 0, 0.55), (0.075, 0.075, 0.2), (0.45, 0.32, 0.2)),    # muslo
              ("cyl_z", (s * 0.11, 0, 0.2), (0.065, 0.065, 0.17), (0.45, 0.32, 0.2)),    # pierna
              ("cube", (s * 0.11, -0.05, 0.03), (0.07, 0.12, 0.03), (0.3, 0.2, 0.12))]   # bota
objs = []
for kind, loc, sc, col in parts:
    if kind == "sphere": bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, location=loc)
    elif kind == "cube": bpy.ops.mesh.primitive_cube_add(location=loc)
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, location=loc,
                                            rotation=(0, math.pi / 2, 0) if kind == "cyl_x" else (0, 0, 0))
        if kind == "cyl_x": sc = (sc[2], sc[1], sc[0])
    o = bpy.context.active_object
    o.scale = sc
    # subdividir a lo largo para que doble bien
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.subdivide(number_cuts=1); bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o["col"] = col
    objs.append(o)

# textura: una franja de color por pieza en un atlas de 64x64 (cada pieza mapea a su celda)
N = len(objs); W = 8
img = bpy.data.images.new("atlas", 64, 64)
px = [0.0] * (64 * 64 * 4)
for i, o in enumerate(objs):
    cx, cy = i % W, i // W
    me = o.data
    for l in list(me.uv_layers): me.uv_layers.remove(l)
    uvl = me.uv_layers.new(name="UV")
    for l in uvl.data: l.uv = ((cx + 0.5) / W, (cy + 0.5) / W)
    for y in range(cy * 8, cy * 8 + 8):
        for x in range(cx * 8, cx * 8 + 8):
            k = (y * 64 + x) * 4
            px[k:k + 4] = [*o["col"], 1.0]
img.pixels = px
# una imagen generada se exporta negra: se escribe a disco y se carga de nuevo
atlas = os.path.splitext(out)[0] + "_atlas.png"
os.makedirs(os.path.dirname(atlas), exist_ok=True)
img.save_render(atlas)
img = bpy.data.images.load(atlas); img.pack()
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.object.join()
body = bpy.context.active_object
body.name = "body"
mat = bpy.data.materials.new("mat"); mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.interpolation = "Closest"
mat.node_tree.links.new(tex.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
body.data.materials.append(mat)

# ---------------------------------------------------------------- esqueleto con nombres genericos
bpy.ops.object.armature_add(location=(0, 0, 0))
arm = bpy.context.active_object
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.data.edit_bones
for b in list(eb): eb.remove(b)
n = [0]


def bone(head, tail, parent=None):
    b = eb.new("bone_%d" % n[0]); n[0] += 1
    b.head = Vector(head); b.tail = Vector(tail)
    if parent: b.parent = parent; b.use_connect = (parent.tail - b.head).length < 1e-4
    return b


hips = bone((0, 0, 0.85), (0, 0, 0.98))
spine = bone((0, 0, 0.98), (0, 0, 1.2), hips)
neck = bone((0, 0, 1.2), (0, 0, 1.33), spine)
head = bone((0, 0, 1.33), (0, 0, 1.82), neck)
for s in (-1, 1):
    sh = bone((s * 0.05, 0, 1.22), (s * 0.2, 0, 1.25), spine)
    up = bone((s * 0.2, 0, 1.25), (s * 0.52, 0, 1.25), sh)
    lo = bone((s * 0.52, 0, 1.25), (s * 0.79, 0, 1.25), up)
    bone((s * 0.79, 0, 1.25), (s * 0.92, 0, 1.25), lo)
    th = bone((s * 0.11, 0, 0.85), (s * 0.11, 0, 0.38), hips)
    sn = bone((s * 0.11, 0, 0.38), (s * 0.11, 0, 0.05), th)
    bone((s * 0.11, 0, 0.05), (s * 0.11, -0.16, 0.02), sn)
bpy.ops.object.mode_set(mode="OBJECT")

for o in scene.objects: o.select_set(False)
body.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB")
print("OK", out, "huesos", len(arm.data.bones))
