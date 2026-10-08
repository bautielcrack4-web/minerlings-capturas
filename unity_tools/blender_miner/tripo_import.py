"""Importa el GLB de Tripo, lo normaliza (alto 1.0, suela en z=0, mirando a -Y), renderiza las mismas vistas que
build_miner.py y opcionalmente lo exporta a Unity con el mismo formato (Resources/Miner/miner.json).

Uso: blender -b -P tripo_import.py -- out/tripo/minero.glb --out out/tripo [--views front,left,back,face]
         [--export ../../unity_miner/Assets/Resources/Miner/miner.json] [--yaw 0] [--tris 12000]
Segmentacion para el esqueleto del juego (pose T): brazos = |x| > hombro y por encima de la cintura; cabeza = arriba
del cuello; piernas = abajo de la entrepierna (por lado); cadera = banda bajo el cinturon; el resto es torso.
El color sale de la textura base (muestreada por esquina) y queda en los vertices (shader MinerToonVC).
El amarillo del casco va en su canal ("helmet") para que los cascos comprables lo sigan tiñendo.
"""
import bpy, bmesh, sys, os, math, json, argparse, colorsys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
ap = argparse.ArgumentParser()
ap.add_argument("glb")
ap.add_argument("--out", default="out/tripo")
ap.add_argument("--views", default="front,left,back,face")
ap.add_argument("--samples", type=int, default=48)
ap.add_argument("--export", default="")
ap.add_argument("--yaw", type=float, default=0.0, help="giro extra en grados si el modelo no mira a la camara")
ap.add_argument("--tris", type=int, default=12000)
args = ap.parse_args(argv)
OUT = os.path.abspath(args.out)
os.makedirs(OUT, exist_ok=True)
PX = 1.0 / 614.0


def zpx(y):
    return (622.0 - y) * PX


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=os.path.abspath(args.glb))
meshes = [o for o in scene.objects if o.type == "MESH"]
for o in scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
# quitar padres/empties de glTF y aplicar transformaciones
mw = obj.matrix_world.copy()
obj.parent = None
obj.matrix_world = mw
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o)

# normalizar: glTF trae Y arriba -> Blender ya lo convierte a Z arriba. Alto 1.0, centrado en X/Y, suela en z=0.
me = obj.data
if abs(args.yaw) > 0.01:
    me.transform(Matrix.Rotation(math.radians(args.yaw), 4, "Z"))
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
h = max(zs) - min(zs)
s = 1.0 / h
me.transform(Matrix.Translation((-(max(xs) + min(xs)) / 2, -(max(ys) + min(ys)) / 2, -min(zs))))
me.transform(Matrix.Scale(s, 4))
# brazos en T: el eje mas ancho debe ser X; si es Y, girar 90 grados
xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
if (max(ys) - min(ys)) > (max(xs) - min(xs)):
    me.transform(Matrix.Rotation(math.radians(90), 4, "Z"))
me.update()
for p in me.polygons:
    p.use_smooth = True
print("NORM alto=1 ancho=%.3f fondo=%.3f tris=%d" % (max(v.co.x for v in me.vertices) - min(v.co.x for v in me.vertices),
      max(v.co.y for v in me.vertices) - min(v.co.y for v in me.vertices), sum(len(p.vertices) - 2 for p in me.polygons)))

# ------------------------------------------------------------------ luces y camara (igual que build_miner.py)
world = bpy.data.worlds.new("Fondo"); scene.world = world; world.use_nodes = True
bg = world.node_tree.nodes["Background"]; bg.inputs[0].default_value = (1, 1, 1, 1); bg.inputs[1].default_value = 0.35
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0)); bpy.context.active_object.is_shadow_catcher = True


def area(name, loc, size, energy, color=(1, 1, 1), target=(0, 0, 0.5)):
    l = bpy.data.lights.new(name, "AREA"); l.size = size; l.energy = energy; l.color = color
    o = bpy.data.objects.new(name, l); o.location = loc; scene.collection.objects.link(o)
    o.rotation_euler = (Vector(target) - o.location).to_track_quat("-Z", "Y").to_euler()


area("Clave", (-1.3, -2.2, 2.4), 4.0, 95, (1.0, 0.97, 0.93))
area("Relleno", (2.0, -1.8, 1.2), 3.0, 38, (0.95, 0.97, 1.0))
area("Contra", (0.5, 2.2, 2.0), 2.0, 55, target=(0, 0, 0.7))
area("Arriba", (0, -0.3, 3.0), 2.5, 25, target=(0, 0, 0.6))
cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); scene.collection.objects.link(cam); scene.camera = cam
scene.render.engine = "CYCLES"; scene.cycles.device = "CPU"; scene.cycles.samples = args.samples; scene.cycles.use_denoising = True
scene.render.film_transparent = True
scene.view_settings.view_transform = "Standard"; scene.view_settings.look = "None"


def aim(o, t):
    o.rotation_euler = (Vector(t) - o.location).to_track_quat("-Z", "Y").to_euler()


VIEWS = {"front": ((0, -1), (710, 635)), "left": ((-1, 0), (300, 635)), "back": ((0, 1), (710, 635)),
         "right": ((1, 0), (300, 635)), "face": (None, (420, 420))}
for name in [v for v in args.views.split(",") if v]:
    d, res = VIEWS[name]
    cd.type = "PERSP"
    if name == "face":
        cd.lens = 85; cd.sensor_fit = "AUTO"; cam.location = (0, -1.35, 0.83); aim(cam, (0, 0, 0.80))
    else:
        cd.lens = 85
        cd.sensor_fit = "HORIZONTAL" if res[0] >= res[1] else "VERTICAL"
        if cd.sensor_fit == "VERTICAL":
            cd.sensor_height = 36.0 * 635 / 710
        cam.location = (d[0] * 2.84, d[1] * 2.84, 0.56)
        aim(cam, (0, 0, (622 - 317.5) * PX))
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("render", name)


# ------------------------------------------------------------------ export a Unity
def base_image(mat):
    """Imagen conectada al Base Color del material (glTF de Tripo: textura PBR)."""
    if not mat or not mat.use_nodes:
        return None
    b = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if not b:
        return None
    stack = [b.inputs["Base Color"]]
    while stack:
        sock = stack.pop()
        for l in sock.links:
            n = l.from_node
            if n.type == "TEX_IMAGE":
                return n.image
            stack += [i for i in n.inputs if i.is_linked]
    return None


def export(path):
    PIV = {
        "hips": Vector((0, 0, zpx(400))), "torso": Vector((0, 0, zpx(395))), "head": Vector((0, 0, zpx(200))),
        "armL": Vector((78 * PX, 0, zpx(252))), "armR": Vector((-78 * PX, 0, zpx(252))),
        "legL": Vector((50 * PX, 0, zpx(408))), "legR": Vector((-50 * PX, 0, zpx(408))),
    }
    PAR = {"hips": None, "torso": "hips", "head": "torso", "armL": "torso", "armR": "torso", "legL": "hips", "legR": "hips"}
    # diezmar a presupuesto de juego (la geometria de Tripo es densa)
    ntri = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if ntri > args.tris:
        dm = obj.modifiers.new("D", "DECIMATE"); dm.ratio = args.tris / ntri; dm.use_collapse_triangulate = True
    obj.modifiers.new("T", "TRIANGULATE")
    dg = bpy.context.evaluated_depsgraph_get()
    m = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), depsgraph=dg)
    img = base_image(m.materials[0] if m.materials else None)
    px = list(img.pixels) if img else None
    W, H = (img.size[0], img.size[1]) if img else (1, 1)
    uv = m.uv_layers.active.data if m.uv_layers.active else None

    def sample(u, v):
        if px is None:
            return (0.8, 0.8, 0.8)
        x = min(W - 1, max(0, int((u % 1.0) * W))); y = min(H - 1, max(0, int((v % 1.0) * H)))
        i = (y * W + x) * 4
        # Image.pixels devuelve los valores tal como estan guardados (sRGB): se pasan a lineal como espera MinerToonVC
        return tuple((c / 12.92) if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in (px[i], px[i + 1], px[i + 2]))

    m.calc_loop_triangles()
    y_neck, y_crotch, y_belt = zpx(205), zpx(415), zpx(392)
    out = {}
    for tri in m.loop_triangles:
        c = sum((m.vertices[v].co for v in tri.vertices), Vector()) / 3
        if abs(c.x) > 0.135 and 0.50 < c.z < y_neck:
            g = "armL" if c.x > 0 else "armR"
        elif c.z > y_neck:
            g = "head"
        elif c.z < y_crotch:
            g = "legL" if c.x > 0 else "legR"
        elif c.z < y_belt:
            g = "hips"
        else:
            g = "torso"
        cols = []
        for li in tri.loops:
            if uv:
                u, v = uv[li].uv
                cols.append(sample(u, v))
            else:
                cols.append((0.8, 0.8, 0.8))
        avg = [sum(cc[k] for cc in cols) / 3 for k in range(3)]
        # casco: amarillo saturado por encima de los ojos
        hh, ss, vv = colorsys.rgb_to_hsv(*[min(1, max(0, a)) ** (1 / 2.2) for a in avg])
        ch = "vc"   # un solo canal: separar el casco por color dejaba bordes dentados
        out.setdefault(g, {}).setdefault(ch, []).append((tri, cols))
    data = {"groups": []}
    total = 0
    for g, chans in out.items():
        piv = PIV[g]
        R = Matrix.Identity(3)
        if g.startswith("arm"):
            R = Matrix.Rotation(math.radians(90 if g == "armL" else -90), 3, "Y")
        subs = []
        for ch, tris in chans.items():
            pos, nrm, col, idx, weld = [], [], [], [], {}
            for tri, cols in tris:
                for k in (2, 1, 0):  # orden invertido por el cambio de ejes
                    vi = tri.vertices[k]
                    cc = cols[k]
                    key = (vi, round(cc[0], 2), round(cc[1], 2), round(cc[2], 2))
                    j = weld.get(key)
                    if j is None:
                        j = len(pos) // 3; weld[key] = j
                        p = R @ (m.vertices[vi].co - piv); n = R @ m.vertices[vi].normal
                        pos += [round(-p.x, 5), round(p.z, 5), round(-p.y, 5)]
                        nrm += [round(-n.x, 4), round(n.z, 4), round(-n.y, 4)]
                        col += [round(cc[0], 4), round(cc[1], 4), round(cc[2], 4)] if ch == "vc" else [1, 1, 1]
                    idx.append(j)
            total += len(idx) // 3
            subs.append({"channel": ch, "pos": pos, "nrm": nrm, "col": col, "idx": idx})
        par = PAR[g]
        rel = piv - (PIV[par] if par else Vector())
        data["groups"].append({"name": g, "parent": par or "", "pivot": [-rel.x, rel.z, -rel.y], "subs": subs})
    for g in PIV:  # el esqueleto necesita todos los nodos aunque una pieza quede vacia
        if g not in out:
            par = PAR[g]; rel = PIV[g] - (PIV[par] if par else Vector())
            data["groups"].append({"name": g, "parent": par or "", "pivot": [-rel.x, rel.z, -rel.y], "subs": []})
    data["handLen"] = (276 - 78) * PX
    data["height"] = 1.0
    with open(path, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    print("EXPORT", path, "tris", total, "textura", (W, H) if img else None)


if args.export:
    export(os.path.abspath(args.export))
