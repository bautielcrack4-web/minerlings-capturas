"""Objetos de las zonas profundas de Curio Barn: Mazmorra del Castillo, Ruinas de la Jungla y Pico Helado/Volcan.

Mismas convenciones que items.py (base en z=0, frente hacia -Y, export "item_<id>").
Tamaños por peso: Small ~0.2-0.35 m, Medium ~0.35-0.6, Large ~0.8-1.1, Huge ~1.3-1.8, Giant ~2-3 m de largo.

Uso: blender -b -P items_deep.py -- --only chalice,throne --out ../../unity_rarezas/Assets/Resources/Models --preview /tmp/prev
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, Euler
import lib
from lib import rbox, sphere, cyl, lathe, torus, tube, blob

ITEMS = {}


def item(fn):
    ITEMS[fn.__name__] = fn
    return fn


# ================================================================ ayudas locales (no tocan lib.py)
def _catmull(pts, n=4):
    """Suaviza una polilinea de control con Catmull-Rom (n puntos por tramo)."""
    P = [Vector(p) for p in pts]
    if len(P) < 3:
        return [tuple(p) for p in P]
    ext = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(n):
            t = k / float(n)
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(P[-1])
    return [tuple(p) for p in out]


def loft(name, pts, radii, col, seg=12, n=4, wave=0.0, freq=1.0, flat=1.0, **kw):
    """Tubo con radio variable por un camino suave (cuernos, colmillos, trompas, colas, plumas).
    radii: lista por punto de control (se interpola). Radio 0 en un extremo = punta cerrada.
    wave/freq: ondula el radio (anillos, costillas). flat: aplasta la seccion (<1)."""
    ctrl = [Vector(p) for p in pts]
    P = [Vector(p) for p in _catmull(pts, n)] if len(pts) > 2 else ctrl
    if len(P) == 2:
        P = [P[0].lerp(P[1], t / 4.0) for t in range(5)]
    # radio por longitud acumulada, interpolando la lista de control
    L = [0.0]
    for a, b in zip(P, P[1:]):
        L.append(L[-1] + (b - a).length)
    tot = L[-1] or 1.0
    R = []
    for i, l in enumerate(L):
        f = l / tot * (len(radii) - 1)
        j = min(int(f), len(radii) - 2)
        r = radii[j] + (radii[j + 1] - radii[j]) * (f - j)
        if wave:
            r *= 1.0 + wave * math.sin(l / tot * freq * 2 * math.pi)
        R.append(r)
    T = []
    for i in range(len(P)):
        a = P[max(i - 1, 0)]
        b = P[min(i + 1, len(P) - 1)]
        T.append((b - a).normalized())
    up = Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((1, 0, 0))
    N = T[0].cross(up).normalized()
    bm = bmesh.new()
    rings = []
    for i in range(len(P)):
        if i > 0:
            ax = T[i - 1].cross(T[i])
            if ax.length > 1e-6:
                N = Matrix.Rotation(T[i - 1].angle(T[i]), 3, ax.normalized()) @ N
        B = T[i].cross(N).normalized()
        N = B.cross(T[i]).normalized()
        if R[i] <= 1e-5:
            rings.append([bm.verts.new(P[i])])
        else:
            rings.append([bm.verts.new(P[i] + (N * math.cos(2 * math.pi * k / seg) * flat + B * math.sin(2 * math.pi * k / seg)) * R[i])
                          for k in range(seg)])
    for a, b in zip(rings, rings[1:]):
        for k in range(seg):
            j = (k + 1) % seg
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                bm.faces.new((a[0], b[k], b[j]))
            elif len(b) == 1:
                bm.faces.new((a[k], a[j], b[0]))
            else:
                bm.faces.new((a[k], a[j], b[j], b[k]))
    for ring in (rings[0], rings[-1]):
        if len(ring) > 2:
            bm.faces.new(ring)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = lib._obj_from_bm(name, bm)
    lib.setmat(o, col, **kw)
    lib.smooth(o, 70)
    return o


def slab(name, loc, pts, th, col, rot=(0, 0, 0), bev=0.01, **kw):
    """Placa: contorno 2D [(x, z), ...] en el plano XZ (mirando a -Y), extruida con espesor th en Y."""
    bm = bmesh.new()
    f = [bm.verts.new((x, -th * 0.5, z)) for x, z in pts]
    b = [bm.verts.new((x, th * 0.5, z)) for x, z in pts]
    bm.faces.new(f)
    bm.faces.new(list(reversed(b)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((f[i], f[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = lib._obj_from_bm(name, bm)
    lib.setmat(o, col, **kw)
    if bev > 0:
        lib.bevel(o, bev, 2)
    lib.smooth(o, 40)
    return lib.place(o, loc, rot)


def xform(objs, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    """Mueve/rota/escala un sub-conjunto armado alrededor del origen."""
    bpy.context.view_layer.update()
    M = Matrix.Translation(Vector(loc)) @ Euler(tuple(math.radians(a) for a in rot)).to_matrix().to_4x4() @ Matrix.Scale(scale, 4)
    for o in objs:
        o.matrix_world = M @ o.matrix_world
    bpy.context.view_layer.update()
    return objs


def mound(name, loc, rx, ry, h, col, seg=24, **kw):
    """Monticulo / base de piedra o tierra con la base plana en loc.z."""
    prof = [(0, 0), (1.0, 0), (0.97, 0.3), (0.85, 0.7), (0.55, 0.95), (0, 1.0)]
    o = lathe(name, loc, [(r, z * h) for r, z in prof], col, seg=seg, **kw)
    o.scale = (rx, ry, 1.0)
    return o


def crystal(name, base, rot, r, h, col, tip=0.38, **kw):
    """Prisma hexagonal con punta (cristales de hielo, gemas grandes)."""
    d = Euler(tuple(math.radians(a) for a in rot)).to_matrix() @ Vector((0, 0, 1))
    b = Vector(base)
    return [cyl(name, b, r, h * (1 - tip), col, seg=6, rot=rot, **kw),
            cyl(name + "_p", b + d * h * (1 - tip), r, h * tip, col, seg=6, r2=0.0, rot=rot, **kw)]


def ring_pts(c, R, n, z=None, a0=0.0):
    return [(c[0] + math.sin(a0 + k * 2 * math.pi / n) * R, c[1] - math.cos(a0 + k * 2 * math.pi / n) * R, c[2] if z is None else z)
            for k in range(n)]


# piezas compartidas -------------------------------------------------------
def helmet_parts(M="c3ccd6", D="1e1a24", G="gold", plume="d81e2e", plume2="f25050", rivets=True, holes=True):
    """Yelmo cerrado de radio 0.13 con base en el origen (alto 0.35 sin penacho)."""
    m = dict(metal=0.6, rough=0.3)
    o = []
    o.append(cyl("gola", (0, 0, 0), 0.152, 0.05, M, r2=0.132, bev=0.012, seg=16, **m))
    o.append(cyl("casco", (0, 0, 0.045), 0.13, 0.2, M, bev=0.015, seg=16, **m))
    o.append(sphere("cupula", (0, 0, 0.24), 0.13, M, seg=16, scale=(1, 1, 0.85), **m))
    o.append(torus("filete", (0, 0, 0.245), 0.132, 0.012, G, seg=18, mseg=5, **m))
    o.append(torus("filete2", (0, 0, 0.05), 0.143, 0.012, G, seg=18, mseg=5, **m))
    arc = [(0, -math.sin(math.radians(a)) * 0.132, 0.24 + math.cos(math.radians(a)) * 0.114) for a in range(80, -81, -20)]
    o.append(tube("cresta", arc, 0.015, G, **m))
    for s in (-1, 1):
        o.append(rbox("rendija", (s * 0.05, -0.121, 0.205), (0.078, 0.024, 0.022), r=0.009, col=D, seg=2))
        for k in range(3 if holes else 0):
            x = s * (0.045 + 0.022 * (k % 2))
            o.append(sphere("respiro", (x, -math.sqrt(0.13 ** 2 - x * x) + 0.002, 0.1 + k * 0.028), 0.0095, D, seg=6, rings=4))
    for p in ring_pts((0, 0, 0), 0.131, 8 if rivets else 0, z=0.088, a0=math.pi / 8):
        o.append(sphere("remache", p, 0.011, G, seg=6, rings=4))
    if plume:
        o.append(cyl("portapluma", (0, 0.02, 0.33), 0.024, 0.06, G, seg=14))
        # penacho: plumas gorditas que suben y caen hacia atras en arco
        for dx, c, ln in ((0.0, plume, 1.0), (-0.04, plume2, 0.86), (0.04, plume2, 0.86)):
            pp = [(dx * 0.2, 0.02, 0.38), (dx * 0.6, 0.04, 0.5 * ln + 0.06), (dx, 0.14, 0.58 * ln + 0.04), (dx * 1.2, 0.25 * ln, 0.52 * ln + 0.05),
                  (dx * 1.4, 0.32 * ln, 0.36 * ln + 0.08), (dx * 1.5, 0.33 * ln, 0.22 + (1 - ln) * 0.2)]
            o.append(loft("penacho", pp, [0.03, 0.055, 0.062, 0.055, 0.04, 0.0], c, seg=9, n=2, rough=0.8))
    return o


def knight_parts(M, D, G, cloth, pose="hang", plume="e0394a", cape=None, belt="7a4a2a"):
    """Armadura completa de pie, base en z=0, ~1.1 de alto con penacho."""
    m = dict(metal=0.6, rough=0.3)
    o = []
    for s in (-1, 1):
        o.append(rbox("escarpe", (s * 0.08, -0.035, 0.03), (0.1, 0.17, 0.06), r=0.028, col=M, seg=2, **m))
        o.append(cyl("greba", (s * 0.08, 0, 0.05), 0.046, 0.23, M, r2=0.054, seg=14, **m))
        o.append(sphere("rodilla", (s * 0.08, -0.012, 0.29), 0.056, M, seg=10, **m))
        o.append(sphere("rodilla_g", (s * 0.08, -0.064, 0.29), 0.02, G, seg=8, rings=5))
        o.append(cyl("muslo", (s * 0.08, 0, 0.3), 0.056, 0.17, M, r2=0.068, seg=14, **m))
    o.append(cyl("faldar", (0, 0, 0.42), 0.165, 0.1, M, r2=0.13, bev=0.015, seg=16, **m))
    o.append(torus("faldar_g", (0, 0, 0.425), 0.163, 0.011, G, seg=18, mseg=5))
    o.append(cyl("cintura", (0, 0, 0.5), 0.12, 0.12, D if cape else M, seg=16))
    o.append(cyl("cinto", (0, 0, 0.5), 0.135, 0.04, belt, seg=18))
    o.append(rbox("hebilla", (0, -0.137, 0.52), (0.05, 0.02, 0.045), r=0.008, col=G, seg=2))
    if cloth:
        o.append(rbox("tabardo", (0, -0.155, 0.42), (0.13, 0.025, 0.17), r=0.012, col=cloth, rough=0.8, seg=2))
    o.append(sphere("peto", (0, 0, 0.66), 0.16, M, seg=16, scale=(1.05, 0.82, 1.05), **m))
    o.append(tube("arista", [(0, -0.126, 0.57), (0, -0.133, 0.66), (0, -0.1, 0.77)], 0.011, G))
    o.append(cyl("gorguera", (0, 0, 0.79), 0.078, 0.06, M, seg=14, **m))
    for s in (-1, 1):
        o.append(sphere("hombrera", (s * 0.17, 0, 0.775), 0.085, M, seg=12, scale=(1.1, 1.05, 0.8), **m))
        o.append(sphere("hombrera2", (s * 0.19, 0, 0.725), 0.075, M, seg=10, scale=(1.0, 1.0, 0.6), **m))
        o.append(sphere("tachon", (s * 0.2, -0.02, 0.83), 0.016, G, seg=6, rings=4))
        if pose == "hang":
            sh, el, wr, hd = (s * 0.2, 0, 0.74), (s * 0.225, -0.01, 0.6), (s * 0.215, -0.05, 0.47), (s * 0.215, -0.055, 0.44)
        else:
            sh, el, wr, hd = (s * 0.2, 0, 0.74), (s * 0.215, -0.07, 0.6), (s * 0.07, -0.19, 0.53), (s * 0.035, -0.205, 0.51)
        o.append(tube("brazo", [sh, el], 0.043, M, **m))
        o.append(sphere("codo", el, 0.05, M, seg=10, **m))
        o.append(tube("antebrazo", [el, wr], 0.044, M, **m))
        o.append(sphere("guantelete", hd, 0.048, D, seg=10, scale=(0.95, 1, 1.1)))
    if pose == "sword":
        sy = -0.205
        o.append(rbox("hoja", (0, sy, 0.235), (0.065, 0.018, 0.4), r=0.008, col=M, seg=2, **m))
        o.append(cyl("punta", (0, sy, 0.035), 0.033, 0.035, M, seg=4, r2=0.0, rot=(180, 0, 0), **m))
        o.append(rbox("canal", (0, sy - 0.009, 0.25), (0.014, 0.004, 0.32), r=0.002, col=D, seg=1))
        o.append(rbox("guarda", (0, sy, 0.445), (0.22, 0.035, 0.035), r=0.012, col=G, seg=2))
        o.append(cyl("puno", (0, sy, 0.46), 0.017, 0.1, belt))
        o.append(sphere("pomo", (0, sy, 0.575), 0.03, G, seg=12))
    if cape:
        o.append(rbox("capa", (0, 0.14, 0.47), (0.36, 0.045, 0.68), r=0.02, col=cape, rot=(-5, 0, 0), rough=0.85))
        o.append(sphere("capa_cuello", (0, 0.07, 0.8), 0.1, cape, seg=10, scale=(1.6, 0.8, 0.5)))
    h = helmet_parts(M, "1e1a24" if pose == "hang" else D, G, plume=plume, rivets=False, holes=(pose == "hang"))
    o += xform(h, (0, 0, 0.84), scale=0.62)
    return o


# ================================================================ MAZMORRA DEL CASTILLO
@item
def chalice():
    o = []
    g = dict(metal=0.8, rough=0.22)
    prof = [(0, 0), (0.085, 0), (0.09, 0.014), (0.078, 0.028), (0.034, 0.05), (0.022, 0.09), (0.02, 0.125), (0.032, 0.14),
            (0.05, 0.158), (0.078, 0.185), (0.097, 0.23), (0.104, 0.282), (0.09, 0.282), (0.083, 0.235), (0.065, 0.195), (0, 0.185)]
    o.append(lathe("caliz", (0, 0, 0), prof, "gold", seg=36, **g))
    o.append(cyl("vino", (0, 0, 0.245), 0.082, 0.012, "8a1f3a", seg=28, rough=0.15))
    o.append(torus("borde", (0, 0, 0.28), 0.097, 0.009, "gold", seg=32, mseg=8, **g))
    o.append(torus("banda", (0, 0, 0.2), 0.084, 0.008, "e09a1c", seg=32, mseg=8, **g))
    o.append(torus("pie_aro", (0, 0, 0.014), 0.088, 0.01, "e09a1c", seg=32, mseg=8, **g))
    o.append(sphere("nudo", (0, 0, 0.1), 0.036, "gold", seg=16, scale=(1, 1, 0.72), **g))
    cols = ["e0394a", "3a7fe0", "3fbf6a"]
    for k, p in enumerate(ring_pts((0, 0, 0), 0.093, 6, z=0.23)):
        a = k * math.pi / 3
        o.append(torus("engaste", p, 0.016, 0.005, "e09a1c", seg=12, mseg=6, rot=(90, 0, math.degrees(a))))
        o.append(sphere("gema", p, 0.016, cols[k % 3], seg=12, scale=(1, 0.6, 1.15), rot=(0, 0, math.degrees(a)), rough=0.08, emis=0.15))
    for k, p in enumerate(ring_pts((0, 0, 0), 0.035, 4, z=0.1, a0=math.pi / 4)):
        o.append(sphere("gemita", p, 0.011, cols[k % 2], seg=10, rough=0.1))
    for k, p in enumerate(ring_pts((0, 0, 0), 0.075, 6, z=0.03, a0=math.pi / 6)):
        o.append(sphere("perla", p, 0.01, "fbf6ee", seg=8))
    return o


@item
def plumed_helmet():
    o = helmet_parts()
    # cojin bajo el yelmo para que se lea como objeto de exhibicion
    o.append(rbox("cojin", (0, 0, 0.03), (0.38, 0.38, 0.06), r=0.028, col="2f4fa8", rough=0.8))
    for s in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        o.append(sphere("borla", (s[0] * 0.19, s[1] * 0.19, 0.03), 0.02, "gold", seg=10))
    xform(o[:-5], (0, 0, 0.058))
    return o


@item
def crest_shield():
    o = []
    # atril de madera
    o.append(rbox("base", (0, 0.03, 0.03), (0.38, 0.2, 0.06), r=0.02, col="wood"))
    o.append(rbox("tope", (0, -0.06, 0.075), (0.26, 0.035, 0.04), r=0.012, col="woodd"))
    o.append(tube("puntal", [(0, 0.12, 0.06), (0, 0.065, 0.33)], 0.016, "woodd"))
    # escudo (armado en local: punta inferior en el origen)
    W, H = 0.2, 0.48
    outline = [(0, 0)]
    for k in range(1, 7):
        t = k / 7.0
        outline.append((W * math.sin(t * math.pi / 2), H * 0.62 * (1 - math.cos(t * math.pi / 2)) ** 0.9))
    outline += [(W, H * 0.62), (W, H), (-W, H), (-W, H * 0.62)]
    outline += [(-x, z) for x, z in reversed(outline[1:7])]
    sh = []
    sh.append(slab("escudo", (0, 0, 0), outline, 0.045, "gold", bev=0.012, metal=0.6, rough=0.3))
    cz = 0.28
    inner = [(x * 0.86, cz + (z - cz) * 0.86 + 0.008) for x, z in outline]
    left = [p for p in inner if p[0] <= 0.0001]
    right = [p for p in inner if p[0] >= -0.0001]
    sh.append(slab("campo_r", (0, -0.026, 0), [(0, inner[0][1])] + [p for p in right if p[0] > 0] + [(0, H * 0.86 + cz * 0.14 + 0.008)],
                   0.014, "d8343a", bev=0.004))
    sh.append(slab("campo_a", (0, -0.026, 0), [(0, H * 0.86 + cz * 0.14 + 0.008)] + [p for p in left if p[0] < 0] + [(0, inner[0][1])],
                   0.014, "2f5fc8", bev=0.004))
    # cruz blanca detras del leon
    sh.append(rbox("cruz_v", (0, -0.036, 0.25), (0.05, 0.01, 0.34), r=0.006, col="fbf6ee"))
    sh.append(rbox("cruz_h", (0, -0.036, 0.33), (0.3, 0.01, 0.05), r=0.006, col="fbf6ee"))
    # cabeza de leon
    lc = (0, -0.045, 0.3)
    for k in range(12):
        a = k * 2 * math.pi / 12
        sh.append(sphere("melena", (lc[0] + math.cos(a) * 0.072, lc[1], lc[2] + math.sin(a) * 0.072), 0.032, "e09a1c", seg=12, scale=(1, 0.5, 1)))
    sh.append(sphere("cara", (lc[0], lc[1] - 0.008, lc[2]), 0.068, "ffcf3d", seg=18, scale=(1, 0.45, 1)))
    for s in (-1, 1):
        sh.append(sphere("oreja", (s * 0.05, lc[1] - 0.01, lc[2] + 0.055), 0.02, "ffcf3d", seg=10, scale=(1, 0.5, 1)))
        sh.append(sphere("ojo", (s * 0.025, lc[1] - 0.04, lc[2] + 0.018), 0.011, "2b2440", seg=10))
        sh.append(sphere("bigote", (s * 0.017, lc[1] - 0.036, lc[2] - 0.02), 0.022, "fff3d6", seg=10, scale=(1, 0.6, 0.8)))
    sh.append(sphere("nariz", (0, lc[1] - 0.047, lc[2] - 0.004), 0.014, "c4483b", seg=10, scale=(1.3, 0.8, 0.9)))
    sh.append(sphere("lengua", (0, lc[1] - 0.034, lc[2] - 0.045), 0.012, "e0594a", seg=10, scale=(1, 0.6, 1.3)))
    for p in [(-0.15, 0.44), (0.15, 0.44), (-0.16, 0.12), (0.16, 0.12)]:
        sh.append(sphere("remache", (p[0], -0.025, p[1]), 0.013, "e09a1c", seg=8))
    o += xform(sh, (0, -0.045, 0.06), rot=(-11, 0, 0))
    return o


@item
def candelabra():
    o = []
    I, B = "3a3640", "d9a441"
    m = dict(metal=0.6, rough=0.35)
    o.append(lathe("base", (0, 0, 0), [(0, 0), (0.13, 0), (0.13, 0.022), (0.095, 0.045), (0.05, 0.062), (0.03, 0.085), (0, 0.085)], I, seg=24, **m))
    o.append(torus("base_aro", (0, 0, 0.024), 0.122, 0.008, B, seg=20, mseg=6))
    for k in range(3):
        a = k * 2 * math.pi / 3 + math.pi / 2
        o.append(sphere("pata", (math.cos(a) * 0.12, math.sin(a) * 0.12, 0.02), 0.028, B, seg=10, scale=(1, 1, 0.7)))
    o.append(cyl("tallo", (0, 0, 0.08), 0.02, 0.36, I, seg=12, **m))
    for z in (0.16, 0.3):
        o.append(sphere("nudo", (0, 0, z), 0.034, B, seg=12, scale=(1, 1, 0.75), **m))
    tops = [(0, 0, 0.44)]
    for s in (-1, 1):
        pts = [(0, 0, 0.3), (s * 0.07, 0, 0.27), (s * 0.13, 0, 0.29), (s * 0.155, 0, 0.34), (s * 0.155, 0, 0.37)]
        o.append(loft("brazo", pts, [0.017, 0.016, 0.015, 0.015, 0.016], I, seg=8, n=3, **m))
        o.append(sphere("gota_brazo", (s * 0.075, 0, 0.245), 0.016, B, seg=8, scale=(1, 1, 1.4)))
        tops.append((s * 0.155, 0, 0.37))
    for i, (x, y, z) in enumerate(tops):
        o.append(cyl("platillo", (x, y, z), 0.045, 0.014, B, seg=14, bev=0.005, **m))
        o.append(cyl("copa", (x, y, z + 0.01), 0.026, 0.03, B, r2=0.034, seg=12, **m))
        hc = 0.13 if i == 0 else 0.1 - 0.01 * i
        cz = z + 0.035
        o.append(cyl("vela", (x, y, cz), 0.023, hc, "fbf1d8", seg=12, bev=0.004))
        o.append(sphere("cera_top", (x, y, cz + hc - 0.004), 0.022, "fff8e8", seg=10, scale=(1, 1, 0.3)))
        for k in range(3):
            a = k * 2.2 + i
            dz = 0.02 + 0.012 * ((k + i) % 3)
            o.append(sphere("goteo", (x + math.cos(a) * 0.022, y + math.sin(a) * 0.022, cz + hc - dz), 0.009, "fff8e8", seg=6, rings=5, scale=(1, 1, 2.4)))
        o.append(sphere("charco", (x + 0.02, y - 0.02, z + 0.014), 0.014, "fff8e8", seg=8, rings=4, scale=(1, 1, 0.5)))
        o.append(cyl("mecha", (x, y, cz + hc), 0.003, 0.02, "2b2440", seg=5))
        o.append(loft("llama", [(x, y, cz + hc + 0.004), (x, y, cz + hc + 0.025), (x, y, cz + hc + 0.05), (x, y, cz + hc + 0.08)],
                      [0.006, 0.02, 0.014, 0.0], "ff8c10", seg=10, n=3, emis=1.5))
        o.append(sphere("llama_in", (x, y - 0.014, cz + hc + 0.026), 0.01, "ffe85a", seg=8, scale=(1, 0.6, 1.5), emis=2.0))
    return o


@item
def armor():
    o = []
    o.append(rbox("tarima", (0, 0, 0.035), (0.46, 0.36, 0.07), r=0.025, col="wood"))
    o.append(rbox("tarima_b", (0, 0, 0.004), (0.48, 0.38, 0.012), r=0.006, col="woodd"))
    o.append(rbox("placa", (0, -0.181, 0.035), (0.12, 0.008, 0.035), r=0.004, col="brass"))
    k = knight_parts("c3ccd6", "6d747e", "gold", "c0283a", pose="hang")
    o += xform(k, (0, 0, 0.07))
    return o


@item
def knight_statue():
    o = []
    st, sd, sl = "b6b3a8", "8f8c84", "cfccc2"
    o.append(rbox("plinto", (0, 0, 0.14), (0.66, 0.56, 0.28), r=0.03, col=sd, seg=2))
    o.append(rbox("cornisa", (0, 0, 0.29), (0.6, 0.5, 0.06), r=0.02, col=st, seg=2))
    o.append(rbox("zocalo", (0, 0, 0.02), (0.72, 0.62, 0.04), r=0.015, col=st, seg=2))
    o.append(rbox("placa", (0, -0.281, 0.15), (0.3, 0.01, 0.1), r=0.006, col=sl, seg=1))
    for k in range(3):
        o.append(rbox("letra", (-0.08 + k * 0.08, -0.287, 0.15), (0.045, 0.004, 0.012), r=0.002, col=sd, seg=1))
    fig = knight_parts(st, sd, sl, None, pose="sword", plume=None, cape=st, belt=sd)
    xform(fig, (0, 0.01, 0.32), scale=1.18)
    o += fig
    # musgo y grietas
    moss = "6fa040"
    for (x, y, z, r) in ((-0.27, -0.23, 0.31, 0.075), (0.33, -0.27, 0.05, 0.065)):
        o.append(blob("musgo", (x, y, z), r, moss, amp=0.35, scale=(1.2, 1, 0.45)))
    o.append(blob("musgo_h", (-0.21, 0.0, 1.3), 0.05, "7fb04a", amp=0.3, scale=(1.2, 1.1, 0.45)))
    cr = "4a4640"
    o.append(tube("grieta", [(0.18, -0.281, 0.27), (0.2, -0.281, 0.21), (0.17, -0.281, 0.16), (0.21, -0.281, 0.08)], 0.006, cr, seg=6))
    o.append(tube("grieta", [(-0.33, -0.18, 0.25), (-0.331, -0.12, 0.19), (-0.331, -0.15, 0.12)], 0.006, cr, seg=6))
    o.append(tube("grieta", [(0.04, -0.145, 1.0), (0.07, -0.14, 0.95), (0.05, -0.15, 0.9)], 0.005, cr, seg=6))
    o.append(sphere("cascote", (-0.3, -0.36, 0.03), 0.035, sd, seg=8, scale=(1.2, 1, 0.7)))
    return o


@item
def throne():
    o = []
    W, G, V = "6a3424", "gold", "c0283a"
    g = dict(metal=0.7, rough=0.25)
    for sx in (-1, 1):
        for sy in (-1, 1):
            o.append(rbox("pata", (sx * 0.31, sy * 0.24, 0.21), (0.09, 0.09, 0.34), r=0.02, col=W, seg=2))
            o.append(sphere("garra", (sx * 0.31, sy * 0.24 - 0.01, 0.05), 0.062, G, seg=12, scale=(1, 1, 0.85), **g))
    o.append(rbox("asiento", (0, 0, 0.4), (0.74, 0.6, 0.1), r=0.025, col=W))
    o.append(rbox("ribete", (0, -0.302, 0.4), (0.76, 0.025, 0.045), r=0.01, col=G, seg=2, **g))
    o.append(slab("faldon", (0, -0.31, 0.35), [(-0.3, 0), (0.3, 0), (0.3, -0.04), (0.18, -0.06), (0.1, -0.1), (0, -0.12), (-0.1, -0.1), (-0.18, -0.06), (-0.3, -0.04)],
                  0.02, G, bev=0.005, **g))
    o.append(rbox("cojin", (0, -0.03, 0.49), (0.62, 0.52, 0.09), r=0.04, col=V, rough=0.85))
    # respaldo
    o.append(rbox("respaldo", (0, 0.27, 0.95), (0.62, 0.08, 1.0), r=0.025, col=W))
    o.append(rbox("tapizado", (0, 0.215, 0.95), (0.48, 0.05, 0.8), r=0.035, col=V, rough=0.85))
    rect = [(-0.255, 0.19, 0.54), (0.255, 0.19, 0.54), (0.255, 0.19, 1.36), (-0.255, 0.19, 1.36), (-0.255, 0.19, 0.54)]
    o.append(tube("marco", rect, 0.014, G, seg=8, **g))
    for i in range(4):
        for j in range(3 if i % 2 == 0 else 2):
            x = (j - (1 if i % 2 == 0 else 0.5)) * 0.14
            o.append(sphere("boton", (x, 0.19, 0.7 + i * 0.17), 0.016, G, seg=6, rings=4, scale=(1, 0.6, 1)))
    for s in (-1, 1):
        o.append(rbox("poste", (s * 0.34, 0.27, 0.92), (0.09, 0.11, 1.04), r=0.02, col=W, seg=2))
        o.append(torus("anillo", (s * 0.34, 0.27, 1.25), 0.06, 0.012, G, seg=12, mseg=6, scale=(1, 1.1, 1), **g))
        o.append(sphere("remate", (s * 0.34, 0.27, 1.49), 0.055, G, seg=12, **g))
        o.append(cyl("remate_p", (s * 0.34, 0.27, 1.53), 0.025, 0.06, G, r2=0.0, seg=8, **g))
        o.append(rbox("brazo", (s * 0.37, -0.04, 0.705), (0.1, 0.58, 0.065), r=0.02, col=W, seg=2))
        o.append(rbox("almohadilla", (s * 0.37, -0.01, 0.745), (0.075, 0.4, 0.03), r=0.012, col=V, rough=0.85, seg=2))
        o.append(rbox("soporte", (s * 0.37, -0.27, 0.57), (0.07, 0.07, 0.22), r=0.015, col=W, seg=2))
        o.append(sphere("voluta", (s * 0.37, -0.33, 0.705), 0.052, G, seg=12, **g))
        o.append(sphere("gema_v", (s * 0.37, -0.38, 0.705), 0.018, "3a7fe0", seg=8, rings=5, rough=0.1, emis=0.2))
    # cresta dorada con gema
    arch = [(-0.32, 0.0), (0.32, 0.0)] + [(math.cos(math.radians(a)) * 0.3, 0.06 + math.sin(math.radians(a)) * 0.2) for a in range(10, 171, 20)]
    o.append(slab("cresta", (0, 0.27, 1.43), arch, 0.07, G, bev=0.012, **g))
    for k in range(5):
        a = math.radians(30 + k * 30)
        o.append(sphere("perla", (math.cos(a) * 0.32, 0.27, 1.49 + math.sin(a) * 0.21), 0.02, "fbf6ee", seg=6, rings=5))
    o.append(sphere("rubi", (0, 0.228, 1.56), 0.055, "e0203a", seg=14, scale=(1, 0.5, 1.1), rough=0.08, emis=0.25))
    o.append(torus("engaste", (0, 0.233, 1.56), 0.058, 0.012, G, seg=16, mseg=6, rot=(90, 0, 0), scale=(1, 1.1, 1), **g))
    for s in (-1, 1):
        o.append(sphere("zafiro", (s * 0.15, 0.232, 1.5), 0.026, "3a7fe0", seg=8, scale=(1, 0.5, 1), rough=0.1))
    return o


@item
def stone_dragon():
    o = []
    st, sd, sl, mem = "8fa08c", "6f7f6c", "aab8a4", "748672"
    base, based = "a39280", "857563"
    moss = "6fa040"
    o.append(rbox("plinto", (0.06, -0.02, 0.05), (1.6, 2.8, 0.1), r=0.04, col=base, seg=2))
    o.append(rbox("plinto_b", (0.06, -0.02, 0.006), (1.68, 2.88, 0.02), r=0.008, col=based, seg=1))
    S = 16
    z0 = 0.1
    # cuerpo echado (esfinge) con el cuello erguido
    o.append(sphere("cuerpo", (0, 0.3, z0 + 0.4), 0.45, st, seg=S, scale=(1.0, 1.45, 0.85)))
    o.append(sphere("pecho", (0, -0.22, z0 + 0.42), 0.36, st, seg=S))
    o.append(sphere("panza", (0, -0.25, z0 + 0.3), 0.27, sl, seg=12, scale=(0.95, 1.0, 0.95)))
    o.append(loft("cuello", [(0, -0.3, z0 + 0.6), (0, -0.52, z0 + 0.85), (0, -0.66, z0 + 1.05)], [0.27, 0.2, 0.16], st, seg=12, n=3))
    o.append(loft("cuello_v", [(0, -0.48, z0 + 0.55), (0, -0.62, z0 + 0.8), (0, -0.74, z0 + 0.98)], [0.17, 0.13, 0.1], sl, seg=10, n=3))
    hc = (0, -0.76, z0 + 1.1)
    o.append(sphere("cabeza", hc, 0.19, st, seg=S, scale=(1.05, 1.15, 0.85)))
    o.append(sphere("hocico", (0, hc[1] - 0.22, hc[2] - 0.05), 0.13, st, seg=14, scale=(0.95, 1.25, 0.72)))
    o.append(sphere("mandibula", (0, hc[1] - 0.15, hc[2] - 0.12), 0.12, sl, seg=12, scale=(0.95, 1.5, 0.45)))
    for s in (-1, 1):
        o.append(sphere("nariz", (s * 0.05, hc[1] - 0.37, hc[2] - 0.01), 0.022, "3e423c", seg=6, rings=5))
        o.append(sphere("ceja", (s * 0.1, hc[1] - 0.07, hc[2] + 0.12), 0.06, sd, seg=10, scale=(1.1, 1.0, 0.5)))
        o.append(sphere("ojo", (s * 0.12, hc[1] - 0.13, hc[2] + 0.05), 0.034, "ffb030", seg=10, scale=(0.7, 0.8, 1.0), emis=0.8))
        o.append(sphere("pupila", (s * 0.138, hc[1] - 0.145, hc[2] + 0.05), 0.012, "2b2018", seg=6, rings=5, scale=(0.5, 0.8, 1.6)))
        o.append(loft("cuerno", [(s * 0.09, hc[1] + 0.05, hc[2] + 0.12), (s * 0.15, hc[1] + 0.2, hc[2] + 0.22), (s * 0.17, hc[1] + 0.38, hc[2] + 0.25)],
                      [0.05, 0.035, 0.0], "e2dcc8", seg=8, n=3))
        o.append(sphere("oreja", (s * 0.18, hc[1] + 0.05, hc[2] + 0.04), 0.06, sd, seg=8, scale=(0.4, 1.4, 0.8), rot=(0, 0, s * 25)))
        for k in range(2):
            o.append(cyl("diente", (s * (0.05 + k * 0.04), hc[1] - 0.32 + k * 0.06, hc[2] - 0.09), 0.012, 0.035, "f0ead8", seg=5, r2=0.0, rot=(180, 0, 0)))
        # patas delanteras estiradas
        o.append(loft("pata_d", [(s * 0.27, -0.3, z0 + 0.32), (s * 0.31, -0.6, z0 + 0.13), (s * 0.3, -0.9, z0 + 0.09)], [0.14, 0.11, 0.1], st, seg=10, n=3))
        o.append(sphere("mano", (s * 0.3, -0.98, z0 + 0.08), 0.11, st, seg=12, scale=(1.15, 1.25, 0.62)))
        for k in range(3):
            o.append(cyl("garra", (s * 0.3 + (k - 1) * 0.055, -1.09, z0 + 0.06), 0.022, 0.07, "e2dcc8", seg=5, r2=0.0, rot=(100, 0, 0)))
        # patas traseras
        o.append(sphere("anca", (s * 0.37, 0.58, z0 + 0.32), 0.26, st, seg=14, scale=(0.62, 1.05, 0.95)))
        o.append(sphere("pie", (s * 0.42, 0.28, z0 + 0.07), 0.12, st, seg=10, scale=(1.0, 1.5, 0.5)))
        for k in range(3):
            o.append(cyl("garra_t", (s * 0.42 + (k - 1) * 0.05, 0.11, z0 + 0.06), 0.02, 0.06, "e2dcc8", seg=5, r2=0.0, rot=(100, 0, 0)))
    # alas plegadas sobre el lomo
    wing = [(0, 0.08), (0.2, 0.2), (0.55, 0.24), (0.95, 0.12), (1.1, 0.0), (0.97, -0.1), (0.83, -0.28), (0.69, -0.18),
            (0.53, -0.4), (0.39, -0.28), (0.23, -0.44), (0.09, -0.28), (-0.03, -0.1)]
    for s in (-1, 1):
        w = [slab("ala", (0, 0, 0), wing, 0.05, mem, bev=0.012)]
        w.append(loft("hueso", [(0, 0, 0.08), (0.2, 0, 0.2), (0.55, 0, 0.24), (0.95, 0, 0.12), (1.12, 0, -0.01)],
                      [0.055, 0.05, 0.042, 0.03, 0.0], sl, seg=8, n=3))
        for (x, zz) in ((0.83, -0.28), (0.53, -0.4), (0.23, -0.44)):
            w.append(tube("vara", [(x * 0.95, 0.0, zz * 0.9), (x * 0.6 + 0.2, 0.0, 0.18)], 0.018, sl, seg=6))
        w.append(sphere("pulgar", (0.0, 0, 0.09), 0.065, sl, seg=10))
        o += xform(w, (s * 0.22, -0.2, z0 + 0.8), rot=(-s * 32, 0, 90))
    # puas por el lomo y el cuello
    for k in range(8):
        y = -0.1 + k * 0.17
        yy = (y - 0.3) / (0.45 * 1.45)
        z = z0 + 0.4 + 0.3825 * math.sqrt(max(0.0, 1 - yy * yy)) - 0.03
        h = 0.13 - abs(k - 3) * 0.012
        o.append(cyl("pua", (0, y, z), 0.05, h, sl, seg=5, r2=0.0, rot=(-25, 0, 0)))
    for k, (y, z) in enumerate(((-0.42, z0 + 0.83), (-0.52, z0 + 0.98), (-0.62, z0 + 1.14))):
        o.append(cyl("pua_c", (0, y + 0.05, z), 0.045, 0.1, sl, seg=5, r2=0.0, rot=(-50, 0, 0)))
    # cola enroscada hacia adelante
    tail = [(0, 0.95, z0 + 0.3), (0.08, 1.2, z0 + 0.17), (0.42, 1.28, z0 + 0.12), (0.7, 1.02, z0 + 0.1), (0.76, 0.55, z0 + 0.09),
            (0.68, 0.05, z0 + 0.08), (0.55, -0.32, z0 + 0.07)]
    o.append(loft("cola", tail, [0.2, 0.16, 0.13, 0.11, 0.09, 0.07, 0.05], st, seg=10, n=3))
    o.append(slab("punta_cola", (0.52, -0.39, z0 + 0.07), [(0, 0.14), (0.09, 0.0), (0, -0.05), (-0.09, 0.0)], 0.045, sd, rot=(90, 0, 160), bev=0.012))
    # musgo y grietas
    for (x, y, z, r) in ((0.18, 0.45, z0 + 0.76, 0.11), (-0.6, -1.22, 0.1, 0.11)):
        o.append(blob("musgo", (x, y, z), r, moss, amp=0.35, scale=(1.2, 1, 0.42)))
    cr = "4e5248"
    o.append(tube("grieta", [(-0.4, -1.422, 0.09), (-0.35, -1.422, 0.06), (-0.38, -1.422, 0.02)], 0.007, cr, seg=6))
    o.append(tube("grieta", [(0.42, 0.0, z0 + 0.52), (0.44, 0.15, z0 + 0.46), (0.45, 0.1, z0 + 0.38)], 0.008, cr, seg=6))
    return o


def _scale_pts(pts, k=1.0):
    return [tuple(c * k for c in p) for p in pts]


# ================================================================ RUINAS DE LA JUNGLA
@item
def jade_mask():
    o = []
    J, Jl, Jd, G = "3fae7a", "7fd8a8", "1f6a48", "f2b425"
    o.append(rbox("base", (0, 0.02, 0.025), (0.2, 0.14, 0.05), r=0.015, col="woodd"))
    o.append(rbox("base2", (0, 0.02, 0.058), (0.15, 0.1, 0.02), r=0.008, col="wood", seg=2))
    o.append(cyl("vara", (0, 0.035, 0.06), 0.012, 0.09, "woodd", seg=10))
    m = []
    m.append(sphere("cara", (0, 0, 0), 0.1, J, seg=20, scale=(0.9, 0.42, 1.15), rough=0.25))
    rays = [(-0.1, -0.02)]
    for k, a in enumerate(range(0, 181, 15)):
        rr = 0.12 if k % 2 == 0 else 0.165
        rays.append((math.cos(math.radians(a)) * rr, 0.03 + math.sin(math.radians(a)) * rr))
    rays = rays[1:]
    m.append(slab("tocado", (0, 0.022, 0.0), [(0.1, -0.02)] + rays + [(-0.1, -0.02)], 0.025, G, bev=0.005, metal=0.6, rough=0.3))
    m.append(slab("tocado_in", (0, 0.006, 0.0), [(0.085, 0.0)] + [(x * 0.8, 0.03 + (z - 0.03) * 0.8) for x, z in rays] + [(-0.085, 0.0)],
                  0.012, "c0503a", bev=0.003))
    for k, a in enumerate(range(15, 166, 30)):
        r = 0.112
        m.append(sphere("gema", (math.cos(math.radians(a)) * r, -0.006, 0.03 + math.sin(math.radians(a)) * r), 0.014,
                        ["3fbfbf", "fbf6ee"][k % 2], seg=8, rings=5, scale=(1, 0.6, 1), emis=0.15))
    m.append(rbox("diadema", (0, -0.025, 0.075), (0.15, 0.03, 0.03), r=0.012, col=G, rot=(-15, 0, 0), seg=2))
    for s in (-1, 1):
        m.append(torus("ojo_aro", (s * 0.036, -0.036, 0.03), 0.024, 0.008, Jl, seg=14, mseg=6, rot=(80, 0, 0), scale=(1.25, 1, 0.8)))
        m.append(sphere("ojo", (s * 0.036, -0.034, 0.03), 0.022, "123426", seg=10, scale=(1.25, 0.4, 0.75)))
        m.append(sphere("pupila", (s * 0.036, -0.043, 0.03), 0.008, "f0e6b0", seg=6, rings=5, emis=0.3))
        m.append(sphere("ceja", (s * 0.04, -0.034, 0.064), 0.03, Jd, seg=8, scale=(1.3, 0.35, 0.35), rot=(0, s * -10, 0)))
        m.append(cyl("orejera", (s * 0.088, 0, 0.01), 0.028, 0.02, G, seg=12, rot=(0, s * 90, 0), bev=0.005))
        m.append(sphere("orejera_g", (s * 0.108, 0, 0.01), 0.013, "e0394a", seg=6, rings=5))
        m.append(sphere("mejilla", (s * 0.055, -0.03, -0.035), 0.018, Jl, seg=6, rings=5, scale=(1, 0.4, 1)))
    m.append(sphere("nariz", (0, -0.048, -0.0), 0.022, J, seg=10, scale=(0.85, 0.75, 1.4)))
    m.append(rbox("boca", (0, -0.03, -0.065), (0.06, 0.02, 0.022), r=0.008, col="123426", seg=2))
    for k in range(4):
        m.append(rbox("diente", (-0.018 + k * 0.012, -0.04, -0.06), (0.008, 0.006, 0.01), r=0.002, col="fbf6ee", seg=1))
    m.append(sphere("barbilla", (0, -0.022, -0.1), 0.02, Jd, seg=6, rings=5, scale=(1.5, 0.5, 0.6)))
    o += xform(m, (0, 0, 0.18), rot=(-8, 0, 0))
    return o


@item
def gold_idol():
    o = []
    st, sd = "a8957a", "86735a"
    o.append(lathe("pedestal", (0, 0, 0), [(0, 0), (0.1, 0), (0.1, 0.03), (0.078, 0.042), (0.07, 0.1), (0.088, 0.11), (0.088, 0.135), (0, 0.135)], st, seg=8))
    o.append(torus("talla", (0, 0, 0.07), 0.073, 0.006, sd, seg=8, mseg=4))
    o.append(tube("enredadera", [(0.08, -0.06, 0.02), (0.06, -0.07, 0.06), (0.02, -0.075, 0.08), (-0.03, -0.073, 0.1), (-0.07, -0.06, 0.12)], 0.006, "4a8a3a", seg=6))
    for (x, y, z) in ((0.06, -0.072, 0.06), (-0.03, -0.077, 0.1), (-0.075, -0.06, 0.125)):
        o.append(sphere("hoja", (x, y, z), 0.018, "5cae47", seg=6, rings=5, scale=(1.2, 0.4, 0.7)))
    g = dict(metal=0.8, rough=0.2)
    G, Gd, Gl = "f7b81e", "d48a10", "ffe070"
    z0 = 0.135
    o.append(cyl("peana", (0, 0, z0), 0.06, 0.015, Gd, seg=14, bev=0.004, **g))
    z0 += 0.015
    for s in (-1, 1):
        o.append(sphere("pie", (s * 0.03, -0.035, z0 + 0.015), 0.022, G, seg=10, scale=(1, 1.3, 0.7), **g))
        o.append(sphere("rodilla", (s * 0.04, -0.02, z0 + 0.035), 0.03, G, seg=10, **g))
        o.append(tube("brazo", [(s * 0.05, 0.0, z0 + 0.1), (s * 0.055, -0.025, z0 + 0.07), (s * 0.02, -0.045, z0 + 0.065)], 0.013, G, seg=8, **g))
        o.append(sphere("oreja", (s * 0.058, 0.0, z0 + 0.155), 0.017, Gd, seg=8, scale=(0.6, 1, 1.2), **g))
        o.append(sphere("ojo", (s * 0.022, -0.05, z0 + 0.163), 0.011, "2b2440", seg=8, scale=(1.2, 0.5, 0.9)))
        o.append(sphere("ojo_b", (s * 0.019, -0.054, z0 + 0.166), 0.0035, "ffffff", seg=5, rings=4, emis=0.5))
        o.append(sphere("ceja", (s * 0.022, -0.047, z0 + 0.18), 0.014, Gd, seg=6, rings=4, scale=(1.3, 0.5, 0.4)))
        o.append(sphere("cachete", (s * 0.033, -0.042, z0 + 0.14), 0.012, "f59a52", seg=6, rings=4, scale=(1, 0.5, 0.8)))
    o.append(sphere("cuerpo", (0, 0, z0 + 0.065), 0.052, G, seg=14, scale=(1, 0.85, 1.05), **g))
    o.append(sphere("manos", (0, -0.048, z0 + 0.065), 0.016, G, seg=8, scale=(1.4, 0.8, 0.9), **g))
    o.append(sphere("cabeza", (0, -0.005, z0 + 0.155), 0.056, G, seg=16, scale=(1, 0.88, 1.0), **g))
    o.append(sphere("nariz", (0, -0.055, z0 + 0.145), 0.012, G, seg=8, scale=(1.2, 0.8, 1), **g))
    o.append(rbox("boca", (0, -0.05, z0 + 0.125), (0.025, 0.006, 0.006), r=0.002, col="8a5a1a", seg=1))
    o.append(cyl("tocado", (0, 0, z0 + 0.19), 0.05, 0.035, Gd, r2=0.062, seg=14, bev=0.006, **g))
    o.append(cyl("tocado2", (0, 0, z0 + 0.225), 0.062, 0.012, G, seg=14, bev=0.004, **g))
    for p in ring_pts((0, 0, 0), 0.06, 8, z=z0 + 0.237):
        o.append(cyl("punta", p, 0.013, 0.028, G, seg=5, r2=0.0, **g))
    o.append(sphere("gema", (0, -0.055, z0 + 0.208), 0.013, "3fbf6a", seg=8, scale=(1, 0.5, 1), emis=0.25))
    o.append(sphere("brillo", (-0.025, -0.045, z0 + 0.19), 0.007, "ffffff", seg=5, rings=4, emis=1.2))
    o.append(sphere("brillo2", (0.03, -0.04, z0 + 0.085), 0.006, "ffffff", seg=5, rings=4, emis=1.2))
    return o


@item
def totem():
    o = []
    W = "a8703f"
    o.append(mound("tierra", (0, 0, 0), 0.24, 0.24, 0.05, "b98a55", seg=14))
    for (x, y) in ((0.17, -0.1), (-0.15, 0.12)):
        o.append(blob("piedra", (x, y, 0.035), 0.04, "9aa1aa", amp=0.3, scale=(1.2, 1, 0.7)))
    secs = [(0.03, 0.3, "c0503a", "nariz"), (0.33, 0.27, "3a8fb0", "lengua"), (0.6, 0.22, "e8b040", "dientes")]
    R = 0.14
    for i, (z, h, c, kind) in enumerate(secs):
        o.append(cyl("seccion", (0, 0, z), R, h, c, seg=14, bev=0.01, rough=0.7))
        o.append(cyl("junta", (0, 0, z + h - 0.012), R + 0.008, 0.024, W, seg=14))
        ez = z + h * 0.66
        for s in (-1, 1):
            o.append(sphere("ojo", (s * 0.052, -0.128, ez), 0.037, "fbf6ee", seg=10, scale=(1.1, 0.4, 0.85)))
            o.append(sphere("pupila", (s * 0.05, -0.143, ez - 0.003), 0.017, "2b2440", seg=8, rings=5, scale=(1, 0.5, 1)))
            o.append(rbox("ceja", (s * 0.055, -0.13, ez + 0.045), (0.075, 0.025, 0.02), r=0.008, col="2b2440", seg=1,
                          rot=(0, s * (12 if i != 1 else -12), 0)))
            o.append(sphere("oreja", (s * 0.143, -0.01, ez - 0.02), 0.035, W, seg=8, scale=(0.5, 1, 1.3)))
        if kind == "nariz":
            o.append(cyl("pico", (0, -0.13, z + 0.12), 0.04, 0.12, "e09a1c", seg=10, r2=0.012, rot=(100, 0, 0)))
            o.append(rbox("boca", (0, -0.13, z + 0.06), (0.11, 0.03, 0.025), r=0.01, col="2b2440", seg=2))
        elif kind == "lengua":
            o.append(sphere("nariz", (0, -0.14, z + 0.13), 0.03, "2a6a8a", seg=8, scale=(0.9, 0.7, 1.2)))
            o.append(sphere("boca", (0, -0.128, z + 0.065), 0.045, "2b2440", seg=10, scale=(1.3, 0.35, 0.7)))
            o.append(sphere("lengua", (0, -0.14, z + 0.04), 0.025, "e0594a", seg=8, scale=(1, 0.6, 1.6)))
        else:
            o.append(sphere("nariz", (0, -0.14, z + 0.105), 0.025, "c08a20", seg=8, scale=(1, 0.8, 1.1)))
            o.append(rbox("boca", (0, -0.13, z + 0.045), (0.12, 0.03, 0.04), r=0.01, col="2b2440", seg=2))
            for k in range(5):
                o.append(rbox("diente", (-0.04 + k * 0.02, -0.145, z + 0.05), (0.012, 0.008, 0.016), r=0.003, col="fbf6ee", seg=1))
    # aguila en la cima
    z = 0.82
    o.append(sphere("cabeza_ag", (0, 0, z + 0.07), 0.13, "4a9a50", seg=16, scale=(1, 1, 0.9)))
    o.append(loft("pico_ag", [(0, -0.1, z + 0.09), (0, -0.2, z + 0.08), (0, -0.23, z + 0.02)], [0.05, 0.035, 0.0], "f0c040", seg=10))
    for s in (-1, 1):
        o.append(sphere("ojo_ag", (s * 0.06, -0.1, z + 0.11), 0.028, "fbf6ee", seg=8, scale=(1, 0.5, 1)))
        o.append(sphere("pupila_ag", (s * 0.06, -0.113, z + 0.11), 0.013, "2b2440", seg=6, rings=5))
        wing = [(0, 0.0), (0.12, 0.05), (0.26, 0.12), (0.36, 0.2), (0.33, 0.12), (0.37, 0.1), (0.31, 0.03), (0.34, 0.0), (0.26, -0.04),
                (0.27, -0.07), (0.16, -0.07), (0.05, -0.08)]
        mw = [(s * x, zz) for x, zz in wing]
        wg = [slab("ala", (0, 0, 0), mw, 0.04, "c0503a", bev=0.006)]
        wg.append(slab("ala_in", (s * 0.02, -0.02, 0.0), [(x * 0.7, zz * 0.6) for x, zz in mw], 0.01, "fbf6ee", bev=0.0))
        wg.append(slab("ala_neg", (s * 0.16, -0.026, -0.02), [(s * x, zz) for x, zz in ((0, 0.0), (0.12, 0.05), (0.15, 0.0), (0.06, -0.04))],
                       0.008, "2b2440", bev=0.0))
        o += xform(wg, (s * 0.12, 0.0, z + 0.0), rot=(0, 0, s * -10))
    return o


@item
def painted_vase():
    o = []
    T, Td, K, C = "c9683a", "a8502a", "2b2420", "f0dcb0"
    prof = [(0, 0), (0.08, 0), (0.09, 0.015), (0.075, 0.03), (0.12, 0.09), (0.158, 0.18), (0.163, 0.23), (0.14, 0.3), (0.085, 0.355),
            (0.062, 0.39), (0.07, 0.425), (0.088, 0.435), (0.088, 0.448), (0.068, 0.448), (0.055, 0.43), (0, 0.42)]
    o.append(lathe("jarra", (0, 0, 0), prof, T, seg=32, rough=0.7))
    o.append(torus("banda1", (0, 0, 0.13), 0.14, 0.01, K, seg=32, mseg=6))
    o.append(torus("banda2", (0, 0, 0.28), 0.149, 0.01, K, seg=32, mseg=6))
    o.append(torus("banda_cuello", (0, 0, 0.4), 0.064, 0.008, C, seg=24, mseg=6))
    o.append(torus("labio", (0, 0, 0.44), 0.087, 0.009, K, seg=28, mseg=6))
    o.append(torus("pie", (0, 0, 0.012), 0.085, 0.01, K, seg=28, mseg=6))
    # friso: rombos crema (octaedros aplastados contra la panza) y puntos negros
    n = 12
    for k in range(n):
        a = k * 2 * math.pi / n
        rad = 0.159
        o.append(sphere("rombo", (math.sin(a) * rad, -math.cos(a) * rad, 0.205), 0.032, C, seg=4, rings=3, scale=(0.75, 0.13, 1.35), rot=(0, 0, math.degrees(a))))
        b = a + math.pi / n
        for dz in (0.17, 0.24):
            rr = 0.156 if dz < 0.2 else 0.157
            o.append(sphere("punto", (math.sin(b) * rr, -math.cos(b) * rr, dz), 0.011, K, seg=8, rings=5, scale=(1, 0.35, 1), rot=(0, 0, math.degrees(b))))
    # zigzag crema en el hombro (sigue la superficie)
    zz = []
    for k in range(33):
        a = k * 2 * math.pi / 32
        z = 0.308 if k % 2 == 0 else 0.338
        rr = 0.14 + (0.085 - 0.14) * (z - 0.3) / 0.055 + 0.004
        zz.append((math.sin(a) * rr, -math.cos(a) * rr, z))
    o.append(tube("zigzag", zz, 0.0065, C, seg=6))
    for s in (-1, 1):
        o.append(tube("asa", [(s * 0.065, 0, 0.39), (s * 0.13, 0, 0.395), (s * 0.165, 0, 0.36), (s * 0.15, 0, 0.29)], 0.013, Td))
    o.append(sphere("desportillado", (0.06, -0.06, 0.448), 0.018, "8a3e20", seg=8))
    return o


@item
def ammonite():
    o = []
    o.append(mound("roca", (0, 0, 0), 0.15, 0.11, 0.075, "a89e90", seg=14))
    for (x, y) in ((0.12, -0.05), (-0.13, 0.04)):
        o.append(blob("piedrita", (x, y, 0.02), 0.025, "8a8378", amp=0.3, scale=(1.2, 1, 0.7)))
    b = math.log(2.2) / (2 * math.pi)
    Rend = 0.1
    K, FL = 0.43, 0.72

    def P(t):
        R = Rend * math.exp(b * t)
        return (R * math.cos(t), 0.0, R * math.sin(t)), R
    pts, rad = [], []
    th = -4.2 * math.pi
    while th <= 0.001:
        p, R = P(th)
        pts.append(p)
        rad.append(K * R)
        th += 0.2
    parts = [loft("concha", pts, rad, "e8b878", seg=12, n=1, flat=FL, rough=0.35)]
    # costillas: anillos mas oscuros sobre la vuelta exterior
    t = -2.3 * math.pi
    while t < -0.12:
        p0, _ = P(t - 0.035)
        p1, R = P(t)
        p2, _ = P(t + 0.035)
        parts.append(loft("costilla", [p0, p1, p2], [K * R * 1.0, K * R * 1.065, K * R * 1.0], "d49a62", seg=12, n=1, flat=FL))
        t += 0.4 + 0.06 * (t / math.pi)
    # abertura con labio y hueco oscuro
    pe, R = P(0.0)
    parts.append(loft("labio", [P(-0.06)[0], pe, P(0.02)[0]], [K * R * 1.05, K * R * 1.16, K * R * 1.1], "f4d6b0", seg=12, n=1, flat=FL))
    parts.append(sphere("hueco", (pe[0] + 0.0, 0.0, pe[2] + 0.012), K * R * 0.8, "6a4a30", seg=10, scale=(1, FL * 0.95, 0.35)))
    parts.append(sphere("ombligo", (0, 0, 0), 0.014, "a8784a", seg=8))
    o += xform(parts, (0.0, 0.0, 0.195), rot=(-8, 70, 0))
    return o


@item
def trike_skull():
    o = []
    B, Bd, H, Hd = "ecdcb8", "c9b48e", "dccaa0", "8a7a5a"
    o.append(mound("tierra", (0, 0.05, 0), 0.82, 1.08, 0.1, "b98a55", seg=18))
    for (x, y, r) in ((0.68, -0.6, 0.09), (-0.72, 0.4, 0.11), (0.6, 0.75, 0.08)):
        o.append(blob("piedra", (x, y, 0.06), r, "9aa1aa", amp=0.3, scale=(1.2, 1, 0.75)))
    o.append(sphere("craneo", (0, 0.02, 0.5), 0.4, B, seg=18, scale=(0.95, 1.25, 0.85)))
    o.append(loft("hocico", [(0, -0.25, 0.52), (0, -0.62, 0.47), (0, -0.88, 0.38), (0, -1.0, 0.28)], [0.28, 0.2, 0.13, 0.06], B, seg=14, flat=0.85))
    o.append(loft("pico", [(0, -0.86, 0.42), (0, -0.99, 0.34), (0, -1.07, 0.22), (0, -1.04, 0.13)], [0.085, 0.075, 0.045, 0.0], Hd, seg=10, flat=0.8))
    o.append(sphere("mandibula", (0, -0.45, 0.22), 0.22, Bd, seg=14, scale=(0.95, 1.8, 0.48)))
    o.append(loft("cuerno_n", [(0, -0.72, 0.55), (0, -0.8, 0.66), (0, -0.84, 0.76)], [0.08, 0.05, 0.0], H, seg=10))
    for s in (-1, 1):
        o.append(loft("cuerno", [(s * 0.18, -0.12, 0.78), (s * 0.24, -0.42, 0.98), (s * 0.27, -0.75, 1.1), (s * 0.26, -1.02, 1.13)],
                      [0.1, 0.075, 0.045, 0.0], H, seg=12))
        o.append(loft("cuerno_p", [(s * 0.262, -0.82, 1.11), (s * 0.26, -1.02, 1.13)], [0.04, 0.0], Hd, seg=10))
        o.append(sphere("cuenca", (s * 0.26, -0.24, 0.64), 0.085, "4a3a2a", seg=12, scale=(0.6, 1.1, 0.85)))
        o.append(sphere("ceja", (s * 0.21, -0.2, 0.73), 0.085, B, seg=12, scale=(1, 1.2, 0.5)))
        o.append(loft("pomulo", [(s * 0.3, -0.16, 0.4), (s * 0.47, -0.2, 0.28)], [0.075, 0.0], H, seg=10))
        o.append(sphere("nasal", (s * 0.075, -0.74, 0.49), 0.03, "5a4a38", seg=8, scale=(0.6, 1.4, 0.8)))
        for k in range(2):
            o.append(sphere("diente", (s * 0.12, -0.58 + k * 0.1, 0.3), 0.022, "fbf6ee", seg=6, rings=5, scale=(0.8, 0.8, 1.3)))
    # gola (frill) inclinada hacia atras con festones
    fr = []
    pts = [(0, -0.05)]
    for a in range(-10, 191, 15):
        pts.append((math.cos(math.radians(a)) * 0.68, 0.05 + math.sin(math.radians(a)) * 0.6))
    fr.append(slab("gola", (0, 0, 0), list(reversed(pts)), 0.08, B, bev=0.025))
    for a in range(-5, 186, 16):
        fr.append(cyl("feston", (math.cos(math.radians(a)) * 0.68, 0, 0.05 + math.sin(math.radians(a)) * 0.6), 0.055, 0.11, Bd, seg=6, r2=0.0,
                      rot=(0, -(90 - a), 0)))
    for (x, z, r) in ((-0.36, 0.38, 0.08), (0.32, 0.42, 0.07), (0.05, 0.55, 0.06), (0.47, 0.2, 0.06), (-0.5, 0.15, 0.05)):
        fr.append(sphere("mancha", (x, -0.042, z), r, Bd, seg=10, scale=(1, 0.2, 0.8)))
    fr.append(tube("grieta", [(-0.12, -0.044, 0.62), (-0.08, -0.044, 0.5), (-0.15, -0.044, 0.42), (-0.1, -0.044, 0.33)], 0.008, "6a5a48", seg=6))
    fr.append(tube("grieta", [(0.48, -0.044, 0.42), (0.4, -0.044, 0.35), (0.43, -0.044, 0.26)], 0.007, "6a5a48", seg=6))
    o += xform(fr, (0, 0.38, 0.55), rot=(-24, 0, 0))
    # enredadera de la jungla
    o.append(tube("liana", [(0.6, 0.62, 0.7), (0.48, 0.5, 0.74), (0.34, 0.4, 0.86), (0.18, 0.3, 0.9), (0.03, 0.26, 0.9)], 0.014, "4a8a3a", seg=6))
    for (x, y, z) in ((0.54, 0.56, 0.72), (0.34, 0.39, 0.88), (0.12, 0.28, 0.92), (0.42, 0.46, 0.8)):
        o.append(sphere("hoja", (x, y, z), 0.055, "5cae47", seg=8, scale=(1.3, 0.8, 0.3), rot=(0, 0, x * 90)))
    return o


# ================================================================ PICO HELADO / VOLCAN
@item
def meteorite():
    from mathutils.bvhtree import BVHTree
    o = []
    o.append(torus("crater", (0, 0, 0.012), 0.16, 0.032, "5a4a40", seg=18, mseg=8, scale=(1, 1, 0.5)))
    o.append(cyl("suelo", (0, 0, 0), 0.16, 0.014, "5e4c40", seg=18))
    rc = Vector((0, 0, 0.13))
    rock = blob("roca", tuple(rc), 0.12, "3e3846", amp=0.4, scale=(1.12, 1.0, 0.92), rough=0.8)
    o.append(rock)
    o.append(blob("bulto", (0.07, 0.04, 0.2), 0.06, "3e3846", amp=0.4))
    o.append(blob("bulto2", (-0.08, 0.05, 0.08), 0.07, "3e3846", amp=0.4))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(rock, dg)
    Mw = rock.matrix_world
    Mi = Mw.inverted()

    def on(u, v):
        d = Vector((math.cos(v) * math.cos(u), math.cos(v) * math.sin(u), math.sin(v)))
        org = rc + d * 0.5
        loc, nrm, _, _ = bvh.ray_cast(Mi @ org, (Mi.to_3x3() @ -d).normalized())
        if loc is None:
            return tuple(rc + d * 0.12), d
        return tuple(Mw @ loc), (Mw.to_3x3() @ nrm).normalized()

    def path(c, steps=4):
        out = []
        for (u0, v0), (u1, v1) in zip(c, c[1:]):
            for k in range(steps):
                t = k / float(steps)
                out.append(on(u0 + (u1 - u0) * t, v0 + (v1 - v0) * t))
        out.append(on(*c[-1]))
        return out
    cracks = [[(-1.95, 0.55), (-1.65, 0.3), (-1.8, 0.05), (-1.5, -0.25)],
              [(-1.65, 0.3), (-1.3, 0.45), (-1.05, 0.3)],
              [(-0.75, 1.0), (-0.45, 0.65), (-0.6, 0.4), (-0.3, 0.12)],
              [(0.3, 1.25), (0.75, 0.95), (0.6, 0.6)],
              [(-2.6, 0.7), (-2.9, 0.35), (-2.7, -0.05)],
              [(-1.3, 1.3), (-0.75, 1.0)]]
    for c in cracks:
        P = path(c)
        o.append(tube("grieta", [tuple(Vector(p) - n * 0.006) for p, n in P], 0.0095, "e0501a", seg=6, emis=1.0))
        o.append(tube("grieta_luz", [tuple(Vector(p) + n * 0.0) for p, n in P], 0.0055, "ffd050", seg=6, emis=2.0))
    p, n = on(-1.2, 1.35)
    o.append(sphere("boca_lava", p, 0.03, "ff8a1a", seg=10, scale=(1, 1, 0.5), emis=2.0))
    o.append(sphere("boca_luz", tuple(Vector(p) + n * 0.01), 0.016, "fff0a0", seg=8, scale=(1, 1, 0.5), emis=2.5))
    for (u, v, r) in ((-0.9, 0.0, 0.02), (0.3, 0.3, 0.018), (-2.2, 0.2, 0.02), (1.3, 0.6, 0.016), (-1.2, -0.3, 0.015), (2.8, 0.2, 0.02)):
        p, n = on(u, v)
        o.append(sphere("poro", tuple(Vector(p) - n * r * 0.4), r, "1e1a22", seg=8, rings=5))
    for k in range(5):
        a = k * 1.3 + 0.4
        o.append(sphere("brasa", (math.cos(a) * 0.16, math.sin(a) * 0.16, 0.03), 0.011, "ff8a2a", seg=6, rings=4, emis=1.4))
    for (x, y) in ((0.17, 0.08), (-0.13, -0.14)):
        o.append(blob("lasca", (x, y, 0.025), 0.025, "3e3846", amp=0.3, scale=(1.2, 1, 0.7)))
    return o


@item
def mammoth():
    o = []
    F, Fl, Fd, T = "7a4a2a", "9a6238", "4e2c1a", "f4ead0"
    I, Id, Il = "a8dcf5", "86c6ec", "d8f2ff"
    ice = dict(rough=0.1, emis=0.12)
    S = 16
    o.append(rbox("losa", (0, 0.1, 0.12), (1.6, 2.7, 0.24), r=0.06, col=Id, **ice))
    o.append(sphere("cuerpo", (0, 0.3, 0.95), 0.58, F, seg=S, scale=(0.85, 1.25, 0.85)))
    o.append(sphere("joroba", (0, -0.35, 1.2), 0.42, F, seg=S, scale=(0.85, 0.95, 1.0)))
    o.append(sphere("cara", (0, -0.7, 1.02), 0.27, Fl, seg=S, scale=(0.9, 0.9, 1.05)))
    o.append(blob("mechon", (0, -0.55, 1.6), 0.13, Fd, amp=0.4, scale=(1.1, 1.2, 0.7)))
    o.append(loft("trompa", [(0, -0.9, 0.98), (0, -1.02, 0.78), (0, -1.05, 0.52), (0, -1.0, 0.36), (0, -0.9, 0.33), (0, -0.86, 0.42)],
                  [0.13, 0.11, 0.085, 0.07, 0.06, 0.05], Fl, seg=10, n=3, wave=0.06, freq=9))
    for s in (-1, 1):
        o.append(loft("colmillo", [(s * 0.17, -0.85, 0.82), (s * 0.28, -1.08, 0.58), (s * 0.42, -1.3, 0.62), (s * 0.4, -1.42, 0.86), (s * 0.24, -1.38, 1.0)],
                      [0.075, 0.07, 0.06, 0.045, 0.0], T, seg=10, n=3))
        o.append(cyl("vaina", (s * 0.17, -0.82, 0.84), 0.09, 0.08, Fd, seg=10, rot=(120, s * -20, 0)))
        o.append(loft("ojo", [(s * 0.2, -0.84, 1.15), (s * 0.14, -0.9, 1.13), (s * 0.08, -0.9, 1.16)], [0.014, 0.017, 0.014], "2b2018", seg=6, n=3))
        o.append(sphere("ceja", (s * 0.15, -0.86, 1.22), 0.06, Fd, seg=8, scale=(1.2, 0.6, 0.45)))
        o.append(sphere("oreja", (s * 0.33, -0.55, 1.1), 0.13, Fd, seg=10, scale=(0.35, 0.9, 1.1)))
        o.append(sphere("mejilla", (s * 0.15, -0.88, 1.03), 0.04, "e09a8a", seg=6, rings=5, scale=(1, 0.5, 0.7)))
        for y in (-0.3, 0.75):
            o.append(cyl("pata", (s * 0.3, y, 0.24), 0.17, 0.5, F, seg=14, bev=0.02))
            o.append(cyl("pezuna", (s * 0.3, y - 0.02, 0.24), 0.18, 0.06, "d8c8a8", seg=14, bev=0.012))
        for k in range(4):
            y = -0.55 + k * 0.4
            o.append(sphere("fleco", (s * 0.43, y, 0.62 + 0.02 * (k % 2)), 0.13, Fd, seg=8, scale=(0.6, 1.0, 1.4)))
    # bloque de hielo que atrapa el cuarto trasero
    o.append(rbox("bloque", (0, 0.82, 0.86), (1.4, 1.05, 1.3), r=0.07, rot=(0, 0, 4), col=I, **ice))
    o.append(rbox("bloque2", (-0.1, 0.62, 1.55), (0.95, 0.75, 0.35), r=0.06, rot=(0, 6, -8), col=Il, **ice))
    o.append(rbox("nieve", (0, 0.82, 1.53), (1.35, 1.0, 0.07), r=0.03, rot=(0, 0, 4), col="fbfdff", rough=0.6, seg=2))
    for s in (-1, 1):
        o.append(rbox("hielo_pata", (s * 0.32, -0.3, 0.42), (0.5, 0.52, 0.36), r=0.05, rot=(0, 0, s * 12), col=Il, **ice))
        o.append(rbox("veta", (s * 0.703, 0.6, 0.9), (0.01, 0.5, 0.06), r=0.004, rot=(35 * s, 0, 4), col="ffffff", seg=1))
    for (b, rot, r, h, c) in (((0.62, 0.3, 0.2), (0, 30, 10), 0.09, 0.55, Il), ((0.6, 0.6, 0.2), (0, 18, -15), 0.07, 0.4, I),
                               ((-0.62, 0.05, 0.2), (0, -32, 0), 0.08, 0.5, Il), ((-0.6, -0.55, 0.2), (10, -25, 0), 0.06, 0.35, I),
                               ((0.55, -0.95, 0.2), (-20, 25, 0), 0.07, 0.4, Il), ((-0.3, 1.25, 1.4), (20, -15, 0), 0.08, 0.45, Il),
                               ((0.35, 1.2, 1.4), (25, 20, 0), 0.07, 0.38, I)):
        o += crystal("pico_hielo", b, rot, r, h, c, **ice)
    return o


@item
def ice_crystal():
    o = []
    o.append(mound("roca", (0, 0, 0), 0.14, 0.12, 0.07, "7d8898", seg=14))
    for (x, y, r) in ((0.11, -0.07, 0.04), (-0.12, 0.05, 0.045), (0.02, 0.1, 0.035)):
        o.append(blob("piedra", (x, y, 0.035), r, "6d7888", amp=0.3, scale=(1.2, 1, 0.8)))
    E = dict(rough=0.1, emis=0.35)
    cr = [((0, 0, 0.05), (0, 0, 0), 0.042, 0.26, "9fdcff"),
          ((0.05, -0.03, 0.05), (18, 28, 0), 0.03, 0.17, "6cc4f2"),
          ((-0.05, -0.02, 0.05), (20, -30, 0), 0.032, 0.19, "c4ecff"),
          ((0.02, 0.05, 0.05), (-30, 12, 0), 0.028, 0.16, "86d0f8"),
          ((-0.06, 0.04, 0.04), (-18, -35, 0), 0.022, 0.12, "6cc4f2"),
          ((0.08, 0.03, 0.04), (-10, 45, 0), 0.02, 0.1, "c4ecff"),
          ((0.0, -0.07, 0.04), (40, 0, 0), 0.02, 0.1, "9fdcff")]
    for (b, rot, r, h, c) in cr:
        o += crystal("cristal", b, rot, r, h, c, **E)
    o.append(sphere("brillo", (-0.012, -0.04, 0.2), 0.008, "ffffff", seg=6, emis=1.5))
    for (x, y) in ((0.09, -0.06), (-0.1, -0.05), (0.04, 0.09)):
        o.append(sphere("nieve", (x, y, 0.06), 0.03, "fbfdff", seg=10, scale=(1.3, 1, 0.35)))
    return o


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
    prev = None
    for i, a in enumerate(argv):
        if a == "--only":
            only = argv[i + 1].split(",")
        if a == "--out":
            out = argv[i + 1]
        if a == "--preview":
            prev = argv[i + 1]
    names = only or list(ITEMS)
    report = []
    for n in names:
        lib.reset()
        objs = ITEMS[n]()
        objs = [x for x in objs if x is not None]
        o, tris = lib.finish(objs, "item_" + n, out)
        report.append((n, tris))
        if prev:
            os.makedirs(prev, exist_ok=True)
            lib.preview([o], os.path.join(prev, n + ".png"), size=420)
    for n, t in report:
        print("ITEM", n, t)


if __name__ == "__main__":
    main()
