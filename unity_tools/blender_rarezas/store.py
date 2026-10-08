"""Icono de la tienda de Curio Barn (Blender por scripts): la caja dorada abierta con rarezas asomando.
Uso: blender -b -P store.py -- --out carpeta     (deja icono_render.png con fondo transparente, 1024x1024)
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Euler
import lib
import world
import items

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = argv[argv.index("--out") + 1] if "--out" in argv else "/tmp"

lib.reset()
body, lid = world.crate(2)
objs = list(body)
# tapa abierta hacia atras
for o in lid:
    o.location = o.location + Vector((0, 0.02, 0))
lj = lib.join(lid, "tapa")
lj.location = (0, 0, 0)
bpy.context.view_layer.update()
pivot = Vector((0, 0.24, 0.41))
lj.data.transform(__import__("mathutils").Matrix.Translation(-pivot))
lj.location = pivot
lj.rotation_euler = Euler((math.radians(110), 0, 0))
objs.append(lj)


def put(fn, loc, rot, s):
    parts = [x for x in fn() if x is not None]
    o = lib.join(parts, fn.__name__)
    o.scale = (s, s, s)
    o.rotation_euler = Euler(tuple(math.radians(a) for a in rot))
    o.location = loc
    return o


objs.append(put(items.crown, (0.0, 0.02, 0.36), (8, 0, 10), 1.25))
objs.append(put(items.dragon_egg, (-0.2, 0.06, 0.3), (0, -18, 0), 0.9))
objs.append(put(items.compass, (0.22, -0.05, 0.36), (40, 25, -20), 0.9))
for k in range(9):
    a = k * 0.7
    objs.append(lib.cyl("moneda", (math.cos(a) * 0.36, -0.28 + math.sin(a) * 0.08, 0.004 + (k % 3) * 0.02), 0.045, 0.015, "coin", seg=20, bev=0.005, rot=(0, 0, 0)))

sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = 96
sc.cycles.use_denoising = True
sc.render.resolution_x = sc.render.resolution_y = 1024
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
w = bpy.data.worlds.new("W")
sc.world = w
w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (1.0, 0.92, 0.8, 1)
w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
cd = bpy.data.cameras.new("cam")
cd.lens = 60
cam = bpy.data.objects.new("cam", cd)
sc.collection.objects.link(cam)
ctr = Vector((0, 0, 0.32))
cam.location = ctr + Vector((0.55, -1.75, 1.05))
cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam
ld = bpy.data.lights.new("sol", "SUN")
ld.energy = 3.6
ld.color = lib.hexcol("fff1d6")
ld.angle = math.radians(6)
sun = bpy.data.objects.new("sol", ld)
sc.collection.objects.link(sun)
sun.rotation_euler = Euler((math.radians(48), 0, math.radians(-30)))
rim = bpy.data.lights.new("borde", "AREA")
rim.energy = 120
rim.size = 1.5
rimo = bpy.data.objects.new("borde", rim)
sc.collection.objects.link(rimo)
rimo.location = (-0.8, 1.2, 1.4)
rimo.rotation_euler = (Vector((0, 0, 0.3)) - rimo.location).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = os.path.join(out, "icono_render.png")
bpy.ops.render.render(write_still=True)
print("ICONO", sc.render.filepath)
