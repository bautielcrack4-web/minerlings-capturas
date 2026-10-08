"""Modelo con esqueleto (TRELLIS.2 + UniRig, o cualquier GLB/FBX con armadura) -> formato del juego.

Normaliza (alto 1, suela en 0, centrado, frente hacia la camara del juego), deja 4 pesos por vertice, exporta malla,
huesos y una textura, y detecta solo por geometria que hueso es cadera, pecho, cabeza, brazos y piernas (UniRig no
usa nombres fijos). El juego reusa su animacion de siempre sobre esos huesos.

Uso: blender -b -P rig_export.py -- <modelo.glb|fbx> --name mr_oro_3 [--outdir ../../unity_miner/Assets/Resources/MinerRig]
     [--yaw 0] [--tex 1024]
"""
import bpy, sys, os, math, json, argparse
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
ap = argparse.ArgumentParser()
ap.add_argument("model")
ap.add_argument("--name", required=True)
ap.add_argument("--outdir", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_miner/Assets/Resources/MinerRig"))
ap.add_argument("--yaw", type=float, default=0.0)
ap.add_argument("--tex", type=int, default=1024)
a = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
path = os.path.abspath(a.model)
if path.lower().endswith(".fbx"): bpy.ops.import_scene.fbx(filepath=path)
else: bpy.ops.import_scene.gltf(filepath=path)

arms = [o for o in scene.objects if o.type == "ARMATURE"]
if not arms: sys.exit("ERROR: el modelo no tiene esqueleto")
arm = arms[0]
meshes = [o for o in scene.objects if o.type == "MESH"]
# unir las mallas
for o in scene.objects: o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active

# aplicar transformaciones (armadura y malla) para trabajar en coordenadas del mundo
for o in (arm, obj):
    for s in scene.objects: s.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

me = obj.data
bones = list(arm.data.bones)
heads = [arm.matrix_world @ b.head_local for b in bones]
tails = [arm.matrix_world @ b.tail_local for b in bones]
vw = [obj.matrix_world @ v.co for v in me.vertices]
zs = [v.z for v in vw]; xs = [v.x for v in vw]; ys = [v.y for v in vw]
h = max(zs) - min(zs)
cx = (max(xs) + min(xs)) / 2; cy = (max(ys) + min(ys)) / 2; z0 = min(zs)
rot = Matrix.Rotation(math.radians(a.yaw), 3, "Z")


def norm(p):
    q = rot @ Vector((p.x - cx, p.y - cy, p.z - z0))
    return q / h


def to_unity(p):  # Blender (x, y, z) -> Unity (-x, z, -y): el frente (-Y) queda mirando a +Z (como el minero)
    return [round(-p.x, 5), round(p.z, 5), round(-p.y, 5)]


# ---------------------------------------------------------------- roles por geometria
bi = {b.name: i for i, b in enumerate(bones)}
parent = [bi[b.parent.name] if b.parent else -1 for b in bones]
children = [[] for _ in bones]
for i, p in enumerate(parent):
    if p >= 0: children[p].append(i)
H = [norm(x) for x in heads]; T = [norm(x) for x in tails]
leaves = [i for i in range(len(bones)) if not children[i]]
root = next(i for i in range(len(bones)) if parent[i] < 0)


def chain_to(i):
    c = []
    while i >= 0: c.append(i); i = parent[i]
    return list(reversed(c))


def end_of(i):  # punta de la cadena (cola del ultimo hueso)
    return T[i]


# piernas: las dos hojas mas bajas; cabeza: la hoja mas alta; brazos: hojas mas abiertas a los costados por encima de 0.45
legs = sorted(leaves, key=lambda i: end_of(i).z)[:2]
headLeaf = max(leaves, key=lambda i: end_of(i).z)
others = [i for i in leaves if i not in legs and i != headLeaf and end_of(i).z > 0.4]
armLeaves = sorted(others, key=lambda i: -abs(end_of(i).x))[:2]
if len(armLeaves) < 2: sys.exit("ERROR: no encontre los dos brazos")


def limb(leaf):
    """Primer hueso de la cadena que se separa del tronco (hombro/cadera) y el siguiente (codo/rodilla)."""
    c = chain_to(leaf)
    # el tronco es lo que comparte con la cabeza
    hc = set(chain_to(headLeaf))
    k = 0
    while k < len(c) and c[k] in hc: k += 1
    branch = c[k - 1] if k > 0 else root
    # clavicula: hueso corto al principio del brazo (UniRig suele ponerla); el pivote del brazo es el siguiente
    total = sum((T[i] - H[i]).length for i in c[k:]) or 1.0
    if len(c) - k >= 3 and (T[c[k]] - H[c[k]]).length < 0.25 * total: k += 1
    upper = c[k] if k < len(c) else c[-1]
    lower = c[k + 1] if k + 1 < len(c) else upper
    hand = c[-1]
    return upper, lower, hand, branch


# Unity: el personaje mira a +Z, su derecha es +X -> en Blender (x -> -x) la derecha es -x
armR, armL = sorted(armLeaves, key=lambda i: end_of(i).x)   # x mas negativa en Blender = +X en Unity = derecha
legR, legL = sorted(legs, key=lambda i: end_of(i).x)
uL, lL, hL, chestL = limb(armL); uR, lR, hR, chestR = limb(armR)
ulL, llL, _, hipsL = limb(legL); ulR, llR, _, hipsR = limb(legR)
hc = chain_to(headLeaf)
headBone = next((i for i in hc if H[i].z > 0.72), hc[-1])
chest = chestR
hips = hipsR if hipsR >= 0 else root
roles = [hips, chest, headBone, uL, lL, hL, uR, lR, hR, ulL, llL, ulR, llR]
print("ROLES", [bones[r].name for r in roles])

# ---------------------------------------------------------------- pesos (4 por vertice)
gnames = {g.index: g.name for g in obj.vertex_groups}
bw, wt = [], []
per_vertex = []
for v in me.vertices:
    ws = [(bi[gnames[g.group]], g.weight) for g in v.groups if gnames.get(g.group) in bi and g.weight > 1e-4]
    ws.sort(key=lambda t: -t[1]); ws = ws[:4]
    s = sum(w for _, w in ws) or 1.0
    ws = [(i, w / s) for i, w in ws] + [(0, 0.0)] * (4 - len(ws))
    if all(w == 0 for _, w in ws): ws[0] = (headBone if vw[v.index].z > z0 + h * 0.7 else hips, 1.0)
    per_vertex.append(ws)

# ---------------------------------------------------------------- textura
img = None
mat = me.materials[0] if me.materials else None
if mat and mat.use_nodes:
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image: img = n.image; break
os.makedirs(os.path.abspath(a.outdir), exist_ok=True)
if img:
    img.scale(a.tex, a.tex)
    img.filepath_raw = os.path.join(os.path.abspath(a.outdir), a.name + ".png")
    img.file_format = "PNG"; img.save()

# ---------------------------------------------------------------- malla (triangulos, soldada por vertice+uv+normal)
me.calc_loop_triangles()
uvl = me.uv_layers.active.data if me.uv_layers.active else None
pos, nrm, uv, idx, BW, WT, weld = [], [], [], [], [], [], {}
for tri in me.loop_triangles:
    n = rot @ tri.normal
    for li in reversed(tri.loops):
        vi = me.loops[li].vertex_index
        u = tuple(uvl[li].uv) if uvl else (0.0, 0.0)
        key = (vi, round(u[0], 4), round(u[1], 4))
        j = weld.get(key)
        if j is None:
            j = len(pos) // 3; weld[key] = j
            pos += to_unity(norm(vw[vi])); nrm += [round(-n.x, 3), round(n.z, 3), round(-n.y, 3)]
            uv += [round(u[0], 4), round(u[1], 4)]
            for i, w in per_vertex[vi]: BW.append(i); WT.append(round(w, 4))
        idx.append(j)

out = {"pos": pos, "nrm": nrm, "uv": uv, "idx": idx, "bw": BW, "wt": WT,
       "bones": [b.name for b in bones], "parent": parent, "head": sum((to_unity(x) for x in H), []),
       "roles": roles, "tex": bool(img)}
with open(os.path.join(os.path.abspath(a.outdir), a.name + ".json"), "w") as f:
    json.dump(out, f, separators=(",", ":"))
print("EXPORT", a.name, "tris", len(idx) // 3, "verts", len(pos) // 3, "huesos", len(bones), "tex", bool(img))
