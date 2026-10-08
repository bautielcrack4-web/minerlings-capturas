"""Minero de Mineros Idle en Blender, construido por codigo a partir de la hoja de referencia (ref/miner_ref.webp).

Uso:  blender -b -P build_miner.py -- --out out/ [--views front,left,back,right,face] [--samples 64] [--blend]
Unidades: altura total = 1.0 (suela a z=0). El minero mira hacia -Y (la camara frontal esta en -Y).
Las medidas salen de la vista frontal de la referencia: 614 px de alto, centro en x=355 px, suela en y=622 px.
"""
import bpy, bmesh, math, sys, os, argparse
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--out", default="out")
ap.add_argument("--views", default="front")
ap.add_argument("--samples", type=int, default=48)
ap.add_argument("--res", type=int, default=710)
ap.add_argument("--blend", action="store_true")
ap.add_argument("--fbx", action="store_true")
ap.add_argument("--export", default="")
args = ap.parse_args(argv)
OUT = os.path.abspath(args.out)
os.makedirs(OUT, exist_ok=True)

PX = 1.0 / 614.0


def P(x, y):
    """Pixel de la vista frontal de la referencia -> (x, z) en metros del modelo."""
    return ((x - 355.0) * PX, (622.0 - y) * PX)


# ------------------------------------------------------------------ escena limpia
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = bpy.data.collections.new("Minero")
scene.collection.children.link(col)


def hexcol(h, a=1.0):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (lin[0], lin[1], lin[2], a)


MATS = {}


def mat(name, color, rough=0.5, sss=0.0, metal=0.0, coat=0.0, emis=None, emis_str=0.0, sheen=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = hexcol(color)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if sss > 0:
        b.inputs["Subsurface Weight"].default_value = sss
        b.inputs["Subsurface Radius"].default_value = (0.9, 0.35, 0.2)
        b.inputs["Subsurface Scale"].default_value = 0.02
    if coat > 0:
        b.inputs["Coat Weight"].default_value = coat
        b.inputs["Coat Roughness"].default_value = 0.25
    if sheen > 0:
        b.inputs["Sheen Weight"].default_value = sheen
    if emis:
        b.inputs["Emission Color"].default_value = hexcol(emis)
        b.inputs["Emission Strength"].default_value = emis_str
    MATS[name] = m
    return m


# paleta (muestras de la referencia)
M = {
    "yellow": mat("Amarillo", "#f5a823", 0.42, coat=0.15),
    "yellow_shirt": mat("Camisa", "#f4ab2a", 0.62, sheen=0.3),
    "band": mat("Banda", "#4a3a2c", 0.55),
    "brown": mat("Cuero", "#74513a", 0.5),
    "brown_d": mat("CueroOscuro", "#5a3d2b", 0.55),
    "boot": mat("Bota", "#6e4528", 0.48),
    "boot_d": mat("BotaOscura", "#5c3c26", 0.5),
    "dark": mat("Gris", "#3c3c40", 0.6, sheen=0.2),
    "glove": mat("Guante", "#38383c", 0.55),
    "grey": mat("GrisClaro", "#6b6e74", 0.5),
    "sole": mat("Suela", "#4b4b50", 0.65),
    "metal": mat("Metal", "#b9bec4", 0.28, metal=0.9),
    "lamp_rim": mat("Lampara", "#4d5157", 0.35, metal=0.4),
    "lens": mat("Lente", "#ffffff", 0.1, emis="#f4f8ff", emis_str=2.2),
    "skin": mat("Piel", "#efae86", 0.48, sss=0.25),
    "hair": mat("Pelo", "#2e2019", 0.55),
    "brow": mat("Ceja", "#2a1c15", 0.6),
    "eye_w": mat("Esclera", "#fbfbf8", 0.2),
    "iris": mat("Iris", "#6b4126", 0.25),
    "pupil": mat("Pupila", "#140b07", 0.2),
    "shine": mat("Brillo", "#ffffff", 0.1, emis="#ffffff", emis_str=1.5),
    "mouth": mat("Boca", "#b5705b", 0.5),
    "under": mat("Remera", "#3d3d40", 0.7),
    "wood": mat("Madera", "#7a4a2a", 0.55),
}

OBJS = []


def finish(o, m, sub=2, smooth=True, bevel=0.0):
    col.objects.link(o)
    if o.name in scene.collection.objects:
        scene.collection.objects.unlink(o)
    o.data.materials.clear()
    o.data.materials.append(m)
    if bevel > 0:
        bv = o.modifiers.new("Bisel", "BEVEL")
        bv.width = bevel
        bv.segments = 3
        bv.limit_method = "ANGLE"
    if sub:
        s = o.modifiers.new("Sub", "SUBSURF")
        s.levels = 1
        s.render_levels = sub
    if smooth:
        for p in o.data.polygons:
            p.use_smooth = True
    OBJS.append(o)
    return o


def new_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return bpy.data.objects.new(name, me)


def ellipsoid(name, c, r, m, seg=24, ring=16, sub=1, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(r), verts=bm.verts)
    o = new_obj(name, bm)
    o.location = c
    o.rotation_euler = rot
    return finish(o, m, sub)


def rbox(name, c, size, m, bevel=0.3, rot=(0, 0, 0), sub=2, taper=None):
    """Caja redondeada: cubo con bisel + subdivision. taper=(sx, sy) escala la cara de arriba."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        if taper and v.co.z > 0:
            v.co.x *= taper[0]
            v.co.y *= taper[1]
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    o = new_obj(name, bm)
    o.location = c
    o.rotation_euler = rot
    bw = min(size) * bevel
    return finish(o, m, sub, bevel=bw)


def tube(name, p0, p1, r0, r1, m, seg=24, sub=1, cap=True, ry=None):
    """Cilindro (cono truncado) entre dos puntos. ry: radio en el otro eje (seccion eliptica)."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    L = d.length
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, segments=seg, radius1=1.0, radius2=1.0, depth=1.0)
    for v in bm.verts:
        t = v.co.z + 0.5
        r = r0 + (r1 - r0) * t
        v.co.x *= r
        v.co.y *= r * ((ry / r0) if ry else 1.0)
        v.co.z *= L
    o = new_obj(name, bm)
    o.location = (p0 + p1) / 2
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return finish(o, m, sub, bevel=min(r0, r1) * 0.25 if cap else 0.0)


def torus(name, c, R, r, m, rot=(0, 0, 0), scale=(1, 1, 1), sub=1):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=48, minor_segments=12,
                                     location=c, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    o.name = name
    return finish(o, m, sub)


def mirror(fn, *a, **k):
    """Llama fn para el lado izquierdo (x<0) y su espejo."""
    fn(*a, side=-1, **k)
    fn(*a, side=1, **k)


# ================================================================== CABEZA Y CASCO
def head():
    hx, hz = P(355, 152)
    # craneo + cara: elipsoide con mandibula afinada
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=24, radius=1.0)
    for v in bm.verts:
        x, y, z = v.co
        if z < 0:  # mandibula: mas angosta abajo, menton redondeado
            k = 1.0 - 0.32 * (-z) ** 1.6
            x *= k
            y *= 1.0 - 0.10 * (-z)
        v.co = Vector((x * 0.106, y * 0.122, z * 0.126))
    o = new_obj("Cabeza", bm)
    o.location = (hx, 0.0, hz)
    finish(o, M["skin"], 2)
    # orejas
    for s in (-1, 1):
        ex, ez = P(355 + s * 70, 150)
        ellipsoid("Oreja", (ex, 0.012, ez), (0.019, 0.03, 0.04), M["skin"], rot=(0, s * 0.25, 0))
        sx_, sz_ = P(355 + s * 60, 138)
        ellipsoid("Patilla", (sx_, -0.03, sz_ + 0.012), (0.014, 0.032, 0.04), M["hair"])
    # pelo: casquete detras y a los costados (patillas), bajo el ala del casco
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=20, radius=1.0)
    kill = [f for f in bm.faces if f.calc_center_median().y < 0.25 and (abs(f.calc_center_median().x) < 0.90 or f.calc_center_median().z < -0.05)]
    kill += [f for f in bm.faces if f.calc_center_median().z < -0.35 and f not in kill]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.scale(bm, vec=Vector((0.113, 0.13, 0.128)), verts=bm.verts)
    o = new_obj("Pelo", bm)
    o.location = (hx, 0.004, hz + 0.004)
    finish(o, M["hair"], 2)
    so = o.modifiers.new("Grosor", "SOLIDIFY")
    so.thickness = 0.006
    o.modifiers.move(len(o.modifiers) - 1, 0)
    # flequillo bajo el ala
    # ojos
    for s in (-1, 1):
        ex, ez = P(355 + s * 31, 132)
        y0 = -0.116
        ellipsoid("Ojo", (ex, y0, ez), (0.030, 0.012, 0.027), M["eye_w"], sub=1)
        ellipsoid("Iris", (ex + s * 0.001, y0 - 0.010, ez - 0.002), (0.0235, 0.006, 0.0245), M["iris"], sub=1)
        ellipsoid("Pupila", (ex + s * 0.001, y0 - 0.0145, ez - 0.003), (0.012, 0.003, 0.013), M["pupil"], sub=1)
        ellipsoid("Brillo", (ex - 0.006, y0 - 0.017, ez + 0.008), (0.0048, 0.002, 0.0048), M["shine"], sub=1)
        # parpado superior oscuro (linea de pestañas)
        ellipsoid("Parpado", (ex, y0 - 0.002, ez + 0.019), (0.028, 0.010, 0.0045), M["brow"], sub=1)
        # ceja gruesa
        bx, bz = P(355 + s * 32, 113)
        ellipsoid("Ceja", (bx, -0.120, bz), (0.034, 0.010, 0.0105), M["brow"], rot=(0.15, 0, s * -0.10))
    # nariz y boca
    nx, nz = P(355, 152)
    ellipsoid("Nariz", (nx, -0.124, nz), (0.0095, 0.010, 0.010), M["skin"])
    mx, mz = P(355, 176)
    cu = bpy.data.curves.new("Boca", "CURVE")
    cu.dimensions = "3D"
    sp = cu.splines.new("BEZIER")
    sp.bezier_points.add(2)
    for bp, p in zip(sp.bezier_points, [(-0.025, 0.006, 0.006), (0.0, 0.0, -0.002), (0.023, 0.006, 0.007)]):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    cu.bevel_depth = 0.0022
    cu.bevel_resolution = 3
    o = bpy.data.objects.new("Boca", cu)
    o.location = (mx, -0.1125, mz)
    col.objects.link(o)
    o.data.materials.append(M["mouth"])
    # cuello
    tube("Cuello", (0, 0.004, P(0, 236)[1]), (0, 0.004, P(0, 192)[1]), 0.042, 0.040, M["skin"])


def helmet():
    cx, cz = P(353, 78)
    # domo: media esfera algo alta
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=24, radius=1.0)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.32], context="VERTS")
    bmesh.ops.scale(bm, vec=Vector((0.137, 0.146, 0.124)), verts=bm.verts)
    o = new_obj("Casco", bm)
    o.location = (cx, 0.004, cz)
    finish(o, M["yellow"], 2)
    so = o.modifiers.new("Grosor", "SOLIDIFY")
    so.thickness = 0.012
    o.modifiers.move(len(o.modifiers) - 1, 0)
    # cresta central
    # ala: anillo inclinado hacia abajo, con visera adelante mas larga (como un casco de obra)
    bx, bz = P(353, 93)
    bm = bmesh.new()
    N = 64
    inner, outer = [], []
    for i in range(N):
        a = 2 * math.pi * i / N
        ca, sa = math.cos(a), math.sin(a)   # sa<0 = adelante (-Y)
        front = max(0.0, -sa)
        back = max(0.0, sa)
        ri = 0.132
        ro = 0.158 + 0.022 * front ** 2 + 0.022 * back ** 2
        zi = 0.0
        zo = -0.032 - 0.040 * abs(ca) ** 1.5 + 0.040 * front ** 2
        inner.append(bm.verts.new((ca * ri * 1.02, sa * ri * 1.06, zi)))
        outer.append(bm.verts.new((ca * ro, sa * ro * 1.06, zo)))
    for i in range(N):
        j = (i + 1) % N
        bm.faces.new((inner[i], inner[j], outer[j], outer[i]))
    o = new_obj("Ala", bm)
    o.location = (bx, 0.004, bz)
    finish(o, M["yellow"], 2)
    so = o.modifiers.new("Grosor", "SOLIDIFY")
    so.thickness = 0.024
    so.offset = 0
    o.modifiers.move(len(o.modifiers) - 1, 0)
    # borde redondeado del ala
    for v in o.data.vertices:
        pass
    # banda marron
    # banda: franja del mismo domo, apenas mas grande (sigue la curva del casco)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=32, radius=1.0)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.16 or v.co.z > 0.40], context="VERTS")
    bmesh.ops.scale(bm, vec=Vector((0.137 * 1.045, 0.146 * 1.045, 0.124 * 1.045)), verts=bm.verts)
    o = new_obj("Banda", bm)
    o.location = (cx, 0.004, cz)
    finish(o, M["band"], 2)
    so = o.modifiers.new("Grosor", "SOLIDIFY")
    so.thickness = 0.006
    o.modifiers.move(len(o.modifiers) - 1, 0)
    # lampara al frente, sobre la banda
    lx, lz = P(352, 45)
    tube("LamparaCuerpo", (lx, -0.11, lz), (lx, -0.155, lz), 0.043, 0.045, M["lamp_rim"], seg=36)
    torus("LamparaAro", (lx, -0.157, lz), 0.039, 0.008, M["lamp_rim"], rot=(math.radians(90), 0, 0))
    ellipsoid("LamparaLente", (lx, -0.157, lz), (0.033, 0.006, 0.033), M["lens"], sub=1)
    rbox("LamparaSoporte", (lx, -0.128, lz - 0.03), (0.05, 0.03, 0.03), M["lamp_rim"], 0.3)


# ================================================================== TORSO
def torso():
    # camisa: tronco algo trapecial
    cx, cz = P(355, 287)
    rbox("Torso", (cx, 0.0, cz), (0.255, 0.19, 0.24), M["yellow_shirt"], bevel=0.45, taper=(1.15, 1.0))
    # remera oscura en el escote (V) y solapas puntiagudas
    vx, vz = P(355, 234)
    bm = bmesh.new()
    v1 = bm.verts.new((-0.04, 0, 0.022)); v2 = bm.verts.new((0.04, 0, 0.022)); v3 = bm.verts.new((0, 0, -0.07))
    bm.faces.new((v1, v3, v2))
    o = new_obj("Escote", bm)
    o.location = (vx, -0.098, vz)
    finish(o, M["under"], 0)
    so = o.modifiers.new("Grosor", "SOLIDIFY"); so.thickness = 0.012; o.modifiers.move(len(o.modifiers) - 1, 0)
    for s in (-1, 1):
        bm = bmesh.new()
        a1 = bm.verts.new((s * 0.040, 0, 0.024)); a2 = bm.verts.new((s * 0.006, -0.004, -0.062))
        a3 = bm.verts.new((s * 0.090, 0.006, -0.046)); a4 = bm.verts.new((s * 0.088, 0.004, 0.022))
        f = bm.faces.new((a1, a2, a3, a4) if s > 0 else (a4, a3, a2, a1))
        o = new_obj("Solapa", bm)
        o.location = (vx, -0.103, vz)
        finish(o, M["yellow_shirt"], 0)
        so = o.modifiers.new("Grosor", "SOLIDIFY"); so.thickness = 0.012; o.modifiers.move(len(o.modifiers) - 1, 0)
    # tapeta y botones
    rbox("Tapeta", (cx, -0.093, cz - 0.03), (0.012, 0.006, 0.17), M["yellow_shirt"], 0.4)
    # mangas cortas arremangadas + brazo + guante (medidas de la vista frontal)
    for s in (-1, 1):
        sx0, sz = P(355 + s * 78, 252)
        sx1, _ = P(355 + s * 150, 252)
        tube("Manga", (sx0, 0, sz + 0.004), (sx1, 0, sz), 0.060, 0.056, M["yellow_shirt"])
        cx0, _ = P(355 + s * 142, 252)
        cx1, _ = P(355 + s * 168, 252)
        tube("Puño", (cx0, 0, sz), (cx1, 0, sz), 0.062, 0.062, M["yellow_shirt"])
        ax0, az = P(355 + s * 165, 250)
        ax1, _ = P(355 + s * 214, 250)
        tube("Brazo", (ax0, 0, az), (ax1, 0, az), 0.036, 0.034, M["skin"])
        glove(s)


def glove(s):
    """Guante grande y cerrado: puño ancho, dorso abultado, cuatro dedos juntos curvados hacia abajo y pulgar arriba."""
    gx0, gz = P(355 + s * 205, 255)
    gx1, _ = P(355 + s * 246, 255)
    tube("GuantePuño", (gx0, 0, gz), (gx1, 0, gz), 0.046, 0.052, M["glove"])
    px, pz = P(355 + s * 276, 254)
    ellipsoid("Palma", (px, 0.0, pz), (0.052, 0.030, 0.040), M["glove"])
    for i in range(4):
        fz = pz + 0.024 - i * 0.0158
        x0 = px + s * 0.03
        L = 0.086 - abs(i - 1.0) * 0.009
        mid = (x0 + s * L * 0.6, -0.002, fz - 0.004 - i * 0.002)
        end = (x0 + s * L, -0.004, fz - 0.016 - i * 0.004)
        tube("Dedo", (x0, 0.0, fz), mid, 0.0165, 0.0160, M["glove"], seg=16)
        tube("DedoPunta", mid, end, 0.0160, 0.0140, M["glove"], seg=16)
    tube("Pulgar", (px - s * 0.012, -0.010, pz + 0.026), (px + s * 0.034, -0.014, pz + 0.044), 0.0150, 0.0130,
         M["glove"], seg=16)


def straps_belt():
    # tiradores de la mochila
    for s in (-1, 1):
        x, _ = P(355 + s * 60, 0)
        ztop = P(0, 215)[1]
        zbot = P(0, 345)[1]
        rbox("Tirador", (x, -0.094, (ztop + zbot) / 2), (0.032, 0.012, ztop - zbot), M["dark"], 0.3,
             rot=(0.05, 0, 0))
        rbox("TiradorHombro", (x, 0.0, ztop + 0.004), (0.034, 0.19, 0.014), M["dark"], 0.3)
    # cinturon
    bz = P(0, 367)[1]
    tube("Cinturon", (0, 0, bz - 0.022), (0, 0, bz + 0.022), 0.112, 0.112, M["brown"], seg=40, cap=False,
         ry=0.081)
    # hebilla: marco metalico
    hx, hz = P(355, 367)
    rbox("Hebilla", (hx, -0.083, hz), (0.075, 0.012, 0.05), M["metal"], 0.3)
    rbox("HebillaHueco", (hx, -0.088, hz), (0.05, 0.006, 0.026), M["brown_d"], 0.3)
    # bolsas en las caderas
    for s in (-1, 1):
        px, pz = P(355 + s * 77, 380)
        rbox("Bolsa", (px, -0.04, pz), (0.072, 0.05, 0.105), M["brown"], 0.3, rot=(0, 0, s * -0.12))
        rbox("BolsaTapa", (px, -0.067, pz + 0.025), (0.074, 0.012, 0.05), M["brown_d"], 0.3, rot=(0, 0, s * -0.12))
        ellipsoid("Boton", (px + s * 0.012, -0.075, pz + 0.012), (0.006, 0.004, 0.006), M["grey"])


# ================================================================== PIERNAS
def legs():
    # cadera
    cx, cz = P(355, 408)
    rbox("Cadera", (cx, 0.0, cz), (0.225, 0.145, 0.11), M["dark"], 0.45)
    for s in (-1, 1):
        xh, _ = P(355 + s * 48, 0)
        xa, _ = P(355 + s * 62, 0)
        z0 = P(0, 410)[1]
        z1 = P(0, 540)[1]
        zk = P(0, 480)[1]
        xk = xh + (xa - xh) * 0.55
        tube("Muslo", (xh, 0, z0), (xk, -0.004, zk), 0.061, 0.064, M["dark"])
        ellipsoid("RodillaTela", (xk, -0.006, zk), (0.069, 0.068, 0.05), M["dark"])
        tube("Canilla", (xk, -0.004, zk), (xa, 0, z1), 0.066, 0.068, M["dark"])
        kx, kz = P(355 + s * 58, 482)
        ellipsoid("Rodillera", (kx, -0.068, kz), (0.042, 0.016, 0.048), M["grey"])
        boot(s, P(355 + s * 68, 0)[0])


def boot(s, x):
    z0 = P(0, 527)[1]
    z1 = P(0, 585)[1]
    tube("BotaCaña", (x, 0, z1), (x, 0, z0 - 0.01), 0.064, 0.060, M["boot"])
    tube("BotaCuello", (x, 0, z0 - 0.022), (x, 0, z0 + 0.012), 0.068, 0.065, M["boot_d"])
    fz = P(0, 594)[1]
    rbox("BotaPie", (x + s * 0.004, -0.03, fz), (0.15, 0.225, 0.062), M["boot"], 0.5)
    ellipsoid("BotaPunta", (x + s * 0.004, -0.105, fz + 0.004), (0.078, 0.065, 0.044), M["boot"])
    sz = P(0, 611)[1]
    rbox("Suela", (x + s * 0.004, -0.025, sz), (0.176, 0.22, 0.026), M["sole"], 0.35)


# ================================================================== MOCHILA Y PICO
def backpack():
    bx, bz = P(355, 300)
    rbox("Mochila", (bx, 0.135, bz), (0.17, 0.09, 0.2), M["dark"], 0.35)
    rbox("MochilaFrente", (bx, 0.18, bz - 0.01), (0.12, 0.03, 0.15), M["yellow"], 0.35)
    rbox("MochilaBolsillo", (bx, 0.198, bz - 0.03), (0.04, 0.012, 0.09), M["dark"], 0.35)
    rbox("MochilaTapa", (bx, 0.135, bz + 0.1), (0.16, 0.085, 0.03), M["dark"], 0.4)


def pickaxe():
    # pico aparte, al costado (no se ve en la pose T de frente)
    root = (0.55, 0.3, 0.0)
    tube("PicoMango", (root[0], root[1], 0.02), (root[0], root[1], 0.42), 0.014, 0.013, M["wood"])
    tube("PicoGrip", (root[0], root[1], 0.02), (root[0], root[1], 0.12), 0.019, 0.019, M["dark"])
    head = bpy.data.curves.new("PicoCabeza", "CURVE")
    head.dimensions = "3D"
    sp = head.splines.new("BEZIER")
    sp.bezier_points.add(2)
    pts = [(-0.16, 0, -0.05), (0, 0, 0.02), (0.16, 0, -0.05)]
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    head.bevel_depth = 0.016
    head.bevel_resolution = 4
    o = bpy.data.objects.new("PicoCabeza", head)
    o.location = (root[0], root[1], 0.41)
    col.objects.link(o)
    o.data.materials.append(M["grey"])
    tp = o.data.splines[0]
    for i, bp in enumerate(tp.bezier_points):
        bp.radius = 0.25 if i != 1 else 1.4
    rbox("PicoCollar", (root[0], root[1], 0.42), (0.04, 0.04, 0.05), M["grey"], 0.3)


head()
helmet()
torso()
straps_belt()
legs()
backpack()
pickaxe()

# ================================================================== LUCES, PISO, CAMARA
world = bpy.data.worlds.new("Fondo")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (1, 1, 1, 1)
bg.inputs[1].default_value = 0.35

bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
floor = bpy.context.active_object
floor.name = "Piso"
floor.is_shadow_catcher = True


def area(name, loc, rot, size, energy, color=(1, 1, 1)):
    l = bpy.data.lights.new(name, "AREA")
    l.size = size
    l.energy = energy
    l.color = color
    o = bpy.data.objects.new(name, l)
    o.location = loc
    o.rotation_euler = rot
    scene.collection.objects.link(o)
    return o


def aim(o, target=(0, 0, 0.5)):
    d = Vector(target) - o.location
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


k = area("Clave", (-1.3, -2.2, 2.4), (0, 0, 0), 4.0, 95, (1.0, 0.97, 0.93)); aim(k)
f = area("Relleno", (2.0, -1.8, 1.2), (0, 0, 0), 3.0, 38, (0.95, 0.97, 1.0)); aim(f)
r = area("Contra", (0.5, 2.2, 2.0), (0, 0, 0), 2.0, 55); aim(r, (0, 0, 0.7))
t = area("Arriba", (0, -0.3, 3.0), (0, 0, 0), 2.5, 25); aim(t, (0, 0, 0.6))

cam_data = bpy.data.cameras.new("Cam")
cam_data.lens = 85
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = args.samples
scene.cycles.use_denoising = True
scene.render.film_transparent = True
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
scene.view_settings.exposure = 0.0
scene.render.threads_mode = "AUTO"

# ocultar el pico en las vistas del cuerpo
pick = [o for o in col.objects if o.name.startswith("Pico")]

VIEWS = {
    # nombre: (posicion de camara, objetivo, resolucion x, y, distancia ortografica)
    "front": ((0, -6.0, 0.62), (0, 0, 0.5), (710, 635)),
    "left": ((-6.0, 0, 0.62), (0, 0, 0.5), (300, 635)),
    "back": ((0, 6.0, 0.62), (0, 0, 0.5), (710, 635)),
    "right": ((6.0, 0, 0.62), (0, 0, 0.5), (300, 635)),
    "face": ((0.0, -2.0, 0.84), (0, 0, 0.8), (420, 420)),
}
FOV_LENS = 85.0
CAM_Z = 0.56      # altura de la camara (pecho)
CAM_D = 2.84      # distancia
for name in args.views.split(","):
    pos, tgt, res = VIEWS[name]
    cam_data.type = "PERSP"
    cam_data.sensor_fit = "HORIZONTAL" if res[0] >= res[1] else "VERTICAL"
    if name == "face":
        cam_data.lens = 85
        cam.location = (0.0, -1.35, 0.83)
        aim(cam, (0, 0, 0.80))
    else:
        cam_data.lens = FOV_LENS
        d = CAM_D
        dirs = {"front": (0, -1), "back": (0, 1), "left": (-1, 0), "right": (1, 0)}[name]
        cam.location = (dirs[0] * d, dirs[1] * d, CAM_Z)
        # apuntar de modo que la vertical quede centrada como en la referencia (centro de imagen = y 317.5 px)
        aim(cam, (0, 0, (622 - 317.5) * PX))
        if name in ("left", "right"):
            cam_data.sensor_fit = "VERTICAL"
            cam_data.sensor_height = 36.0 * 635 / 710
    for o in pick:
        o.hide_render = True
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("render", name)

# depuracion: BBOX=script.py ejecuta un script extra con la escena armada
exec(open(os.environ["BBOX"]).read()) if os.environ.get("BBOX") else None
if args.export:
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import export_miner
    export_miner.export(os.path.abspath(args.export))
if args.blend:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "minero.blend"))
