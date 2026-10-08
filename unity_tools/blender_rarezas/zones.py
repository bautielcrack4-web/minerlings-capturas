"""Zonas nuevas de Curio Barn en Blender por scripts: Mazmorra del Castillo, Ruinas de la Jungla, Pico Helado / Volcan,
y dos vehiculos de evento (grua y globo). Piezas sueltas que Unity reparte por el mapa (como town.py / beach.py).
Base en y=0 (Unity), frente hacia -Z (la camara). Medidas en metros (el heroe mide ~1 m).

Mazmorra: castle_wall (tramo de 4 m a lo largo de X), castle_tower, bridge (4 m a lo largo de Z, tablas a y 0.15),
          dungeon_arch, torch, cobweb (esquina de arriba a la izquierda, plano XY), bat (colgado: patas arriba a y ~0.3),
          slab (baldosa trampa 1x1), dungeon_floor_rock, chains.
Ruinas:   temple, jungle_tree, fern, vine_pillar, stone_head, monkey.
Pico:     snow_pine, ice_rock, lava_rock, snowman, steam_vent, balloon_dock (tablas a y 0.2).
Vehiculos (frente hacia +Z, hacia donde avanza, como vehicles.py):
  crane:   body (0,0,0) camion; boom (pivote en la bisagra (0, 1.45, -1.55)): pluma + cable + gancho. Girar en X para subirla.
  balloon: una sola pieza "body" (canasta en y=0).

Uso: blender -b -P zones.py -- [--only castle_wall,crane,...] [--preview carpeta]
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix
import lib
from lib import rbox, sphere, cyl, torus, tube, blob, lathe
from shack import U, UB, UC, US
from town import UL, UBL

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
PROPS = {}
BUDGET = {}


def prop(budget=3000):
    def deco(fn):
        PROPS[fn.__name__] = fn
        BUDGET[fn.__name__] = budget
        return fn
    return deco


# ---------------------------------------------------------------- paleta
STONE, STONE2, STONE3, STONED = "9a98a6", "8a8896", "aaa7b2", "6d6b7a"
MOSS, MOSS2 = "6fae3e", "86c04a"
IRON, IRONL = "4a4f5a", "6d747e"
WOOD, WOODD, WOODL = "a8703f", "7a4a2a", "c98d55"
INK = "2b2026"
FLAME, FLAME2 = "ff8a2a", "ffd84a"
RSTONE, RSTONE2, RSTONED = "c2b996", "b0a886", "958f72"
LEAF, LEAF2, LEAF3 = "4aa840", "5cbf63", "3f8f3a"
SNOW, SNOWD, ICE, ICED = "f4f8ff", "d8e6f6", "bfe6fa", "8fc8ec"
BASALT, BASALT2 = "3e3842", "514a56"


# ---------------------------------------------------------------- helpers (coordenadas de Unity)
def _basis(primary, hint):
    """Base ortonormal: p = primary, q lo mas parecido a hint, r = p x q."""
    p = Vector(primary).normalized()
    q = Vector(hint)
    q = q - q.dot(p) * p
    if q.length < 1e-5:
        q = Vector((1, 0, 0)) if abs(p.x) < 0.9 else Vector((0, 1, 0))
        q = q - q.dot(p) * p
    q.normalize()
    return p, q, p.cross(q)


def _set_frame(o, loc, X, Y, Z):
    M = Matrix((X, Y, Z)).transposed().to_4x4()
    S = Matrix.Diagonal(tuple(o.scale) + (1.0,))
    o.matrix_world = Matrix.Translation(Vector(loc)) @ M @ S


def UBeam(name, a, b, w, h, col, up=(0, 1, 0), r=0.03, seg=2, **kw):
    """Viga de a hasta b (Unity); w = ancho de costado, h = alto hacia 'up'."""
    A, B = Vector(U(*a)), Vector(U(*b))
    d = B - A
    o = rbox(name, (0, 0, 0), (w, h, d.length), r=r, col=col, seg=seg, **kw)
    Z, Y, _ = _basis(d, U(*up))
    _set_frame(o, (A + B) * 0.5, Y.cross(Z), Y, Z)
    return o


def UCyl(name, a, b, r, col, r2=None, seg=12, **kw):
    """Cilindro (o cono) de a hasta b (Unity)."""
    A, B = Vector(U(*a)), Vector(U(*b))
    d = B - A
    o = cyl(name, (0, 0, 0), r, d.length, col, seg=seg, r2=r2, **kw)
    Z, Y, _ = _basis(d, (0, 0, 1))
    _set_frame(o, A, Y.cross(Z), Y, Z)
    return o


def USq(name, center, r, col, along, scale=(1, 1, 1), hint=(0, 1, 0), seg=10, **kw):
    """Esfera estirada orientada: scale[2] va a lo largo de 'along' (Unity), scale[1] hacia 'hint'."""
    o = sphere(name, (0, 0, 0), r, col, seg=seg, rings=max(5, seg // 2 + 1), scale=scale, **kw)
    Z, Y, _ = _basis(U(*along), U(*hint))
    _set_frame(o, U(*center), Y.cross(Z), Y, Z)
    return o


def ULeaf(name, pts, widths, col, up=(0, 1, 0), fold=0.25, thick=0.014, **kw):
    """Hoja / cinta por una polilinea (Unity) con ancho por estacion; los bordes caen (nervio central levantado)."""
    P = [Vector(U(*p)) for p in pts]
    upv = Vector(U(*up))
    bm = bmesh.new()
    rows = []
    for i, p in enumerate(P):
        t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
        side = t.cross(upv)
        if side.length < 1e-5:
            side = t.cross(Vector((1, 0, 0)))
        side.normalize()
        n = side.cross(t).normalized()
        w = widths[i]
        rows.append((bm.verts.new(p + side * w - n * w * fold), bm.verts.new(p), bm.verts.new(p - side * w - n * w * fold)))
    for i in range(len(rows) - 1):
        a, b = rows[i], rows[i + 1]
        for k in range(2):
            try:
                bm.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
            except ValueError:
                pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    lib.setmat(o, col, **kw)
    m = o.modifiers.new("grosor", "SOLIDIFY")
    m.thickness = thick
    m.offset = 0.0
    lib.smooth(o, 70)
    return o


def ULink(name, center, along, twist, col, R=0.04, r=0.012, **kw):
    """Eslabon de cadena centrado en 'center', estirado a lo largo de 'along'; twist alterna el plano."""
    o = torus(name, (0, 0, 0), R, r, col, seg=10, mseg=5, scale=(1.55, 1, 1), **kw)
    X, Y, Z = _basis(U(*along), U(0, 0, 1) if not twist else U(1, 0, 0))
    if twist:
        Y, Z = Z, -Y
    _set_frame(o, U(*center), X, Y, Z)
    return o


def chain(pts, col=IRON, link=0.1, R=0.04, r=0.012, **kw):
    """Cadena de eslabones por una polilinea (Unity)."""
    P = [Vector(p) for p in pts]
    out = []
    step = link * 0.72
    k = 0
    for i in range(len(P) - 1):
        a, b = P[i], P[i + 1]
        L = (b - a).length
        n = max(1, int(round(L / step)))
        for j in range(n):
            c = a + (b - a) * ((j + 0.5) / n)
            out.append(ULink("eslabon", tuple(c), tuple(b - a), k % 2 == 1, col, R=R, r=r, **kw))
            k += 1
    return out


def sag(a, b, drop, n=6):
    """Puntos de una cuerda colgando entre a y b (Unity) con flecha 'drop'."""
    return [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - drop * 4 * t * (1 - t), a[2] + (b[2] - a[2]) * t)
            for t in [i / float(n) for i in range(n + 1)]]


def UBc(name, x, y, z, w, h, d, **kw):
    """Caja con CENTRO en Unity (x, y, z)."""
    return UB(name, x, y - h * 0.5, z, w, h, d, **kw)


def tilt(objs, pivot, rx=0.0, rz=0.0):
    """Gira un grupo de objetos alrededor de un pivote (Unity), rx/rz en grados sobre ejes de Unity."""
    p = Vector(U(*pivot))
    R = Matrix.Rotation(math.radians(rz), 4, Vector(U(0, 0, 1))) @ Matrix.Rotation(math.radians(rx), 4, Vector(U(1, 0, 0)))
    M = Matrix.Translation(p) @ R @ Matrix.Translation(-p)
    bpy.context.view_layer.update()
    for o in objs:
        o.matrix_world = M @ o.matrix_world
    return objs


def gores(name, prof, cols, n=12, per=3, y0=0.0):
    """Envoltura de globo: revolucion de prof [(r, y)] partida en n gajos de colores alternados."""
    out = []
    for g in range(n):
        bm = bmesh.new()
        rings = []
        for (r, y) in prof:
            ring = []
            for i in range(per + 1):
                a = 2 * math.pi * (g * per + i) / (n * per)
                ring.append(bm.verts.new(U(math.cos(a) * r, y0 + y, math.sin(a) * r)))
            rings.append(ring)
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for i in range(per):
                try:
                    bm.faces.new((a[i], b[i], b[i + 1], a[i + 1]))
                except ValueError:
                    pass
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(o)
        lib.setmat(o, cols[g % len(cols)])
        for p in o.data.polygons:
            p.use_smooth = True
        out.append(o)
    return out


def flame(x, y, z, s=1.0):
    """Llama de juguete: gota naranja con nucleo amarillo adelante y dos lenguas (emisiva)."""
    prof = [(0, 0), (0.06, 0.02), (0.1, 0.08), (0.1, 0.15), (0.07, 0.24), (0.03, 0.32), (0, 0.38)]
    sp = [(r * s, h * s) for r, h in prof]
    o = [UL("llama", x, y, z, sp, FLAME, seg=12, emis=2.5, rough=0.3)]
    o.append(UL("nucleo", x, y + 0.01 * s, z - 0.05 * s, [(r * 0.65, h * 0.62) for r, h in sp], FLAME2, seg=10, emis=3.0, rough=0.3))
    for sx in (-1, 1):
        o.append(USq("lengua", (x + sx * 0.07 * s, y + 0.2 * s, z), 0.045 * s, FLAME, (sx * 0.4, 1, 0), scale=(1, 1, 2.0), seg=8, emis=2.5))
    return o


def moss(x, y, z, r, seed=1, col=None, flat=0.5):
    """Mata de musgo: racimo de 3 bultitos irregulares (no una tortilla chata)."""
    rnd = random.Random(seed)
    o = []
    for k in range(3):
        a = rnd.uniform(0, 6.28)
        d = r * 0.55 if k else 0.0
        rr = r * (0.62 if k == 0 else rnd.uniform(0.38, 0.5))
        o.append(UBL("musgo", x + math.cos(a) * d, y + (0.0 if k == 0 else -rr * 0.2), z + math.sin(a) * d * 0.6, rr,
                     col or (MOSS if (seed + k) % 2 else MOSS2), seed=seed * 7 + k, amp=0.45, scale=(1.25, 1.1, flat)))
    return o


def surf(o, dirs, center, inset=0.008):
    """Puntos (Blender) sobre la superficie de 'o' en las direcciones 'dirs' (Unity) desde 'center' (Unity)."""
    bpy.context.view_layer.update()
    ev = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    M = o.matrix_world
    mi = M.inverted()
    C = Vector(U(*center))
    out = []
    for d in dirs:
        D = Vector(U(*d)).normalized()
        org = C + D * 5.0
        ok, loc, n, i = ev.ray_cast(mi @ org, (mi.to_3x3() @ -D).normalized())
        out.append((M @ loc) - D * inset if ok else C + D * 0.3)
    return out


def crack_dirs(poly, sub=3):
    """Subdivide una polilinea de direcciones para que la grieta siga la superficie."""
    out = []
    for i in range(len(poly) - 1):
        a, b = Vector(poly[i]), Vector(poly[i + 1])
        for j in range(sub):
            out.append(tuple(a.lerp(b, j / float(sub))))
    out.append(tuple(poly[-1]))
    return out


# ================================================================ MAZMORRA DEL CASTILLO
@prop(3500)
def castle_wall():
    """Tramo de muralla de 4 m (x -2..2), 2.2 m de alto, almenas al frente. Se repite punta con punta."""
    rnd = random.Random(21)
    o = [UB("zocalo", 0, 0, 0, 4.0, 0.25, 0.95, r=0.05, seg=2, col=STONED)]
    o.append(UB("muro", 0, 0.2, 0, 3.98, 1.6, 0.8, r=0.06, seg=2, col=STONE))
    o.append(UB("adarve", 0, 1.74, 0, 4.0, 0.12, 0.9, r=0.04, seg=2, col=STONE3))
    for x in (-1.5, -0.5, 0.5, 1.5):
        o.append(UB("almena", x, 1.82, -0.24, 0.66, 0.4, 0.38, r=0.06, seg=2, col=STONE3 if x in (-0.5, 1.5) else STONE))
    # piedras salientes en la cara de adelante (hileras corridas)
    for row in range(5):
        y = 0.3 + row * 0.29
        x = -2.0 + (0.0 if row % 2 == 0 else 0.33)
        while x < 1.95:
            w = rnd.uniform(0.5, 0.75)
            if rnd.random() < 0.72 and x + w * 0.5 < 1.95 and x > -1.95:
                o.append(UB("piedra", x + w * 0.5, y, -0.42, w - 0.05, 0.25, 0.06, r=0.03, seg=1,
                            col=rnd.choice([STONE, STONE2, STONE3, "a39fa8"])))
            x += w
    o.append(UB("tronera", 0.05, 0.95, -0.44, 0.13, 0.42, 0.04, r=0.04, seg=1, col=INK))
    # musgo y humedad
    for k, x in enumerate((-1.45, -0.3, 0.95)):
        o += moss(x, 1.8, -0.4, 0.32, seed=k + 1)
        o.append(UB("chorreado", x + 0.05, 1.05, -0.405, 0.16, 0.7, 0.02, r=0.02, seg=1, col="7e7c8a"))
    for k, x in enumerate((-1.15, 0.5, 1.45)):
        o += moss(x, 0.1, -0.47, 0.24, seed=k + 7)
    return o


@prop(5000)
def castle_tower():
    """Torre redonda de 2.5 m de diametro, techo conico rojo con banderin (tope del techo ~3.4 m)."""
    rnd = random.Random(4)
    o = [UL("torre", 0, 0, 0, [(0, 0), (1.3, 0), (1.3, 0.22), (1.21, 0.28), (1.18, 1.95), (0, 1.95)], STONE, seg=28)]
    o.append(UL("cornisa", 0, 1.8, 0, [(0, 0), (1.18, 0), (1.34, 0.2), (1.36, 0.36), (0, 0.36)], STONE3, seg=28))
    o.append(UL("techo", 0, 2.1, 0, [(0, 0), (1.5, 0), (1.46, 0.1), (1.12, 0.45), (0.72, 0.85), (0.32, 1.2), (0.05, 1.42), (0, 1.45)], "e0594a", seg=24))
    for k, (rr, yy) in enumerate(((1.22, 2.36), (0.86, 2.74), (0.5, 3.08))):
        o.append(torus("teja", U(0, yy, 0), rr + 0.02, 0.035, "c4483b", seg=24, mseg=6))
    o.append(US("bocha", 0, 3.56, 0, 0.07, "ffd23a", metal=0.5, rough=0.3))
    o.append(UC("asta", 0, 3.58, 0, 0.025, 0.5, IRON, seg=8))
    o.append(ULeaf("bandera", [(0.02, 3.98, 0), (0.18, 3.96, -0.04), (0.34, 3.95, 0.02), (0.5, 3.92, -0.03)], [0.12, 0.12, 0.11, 0.1], "ffd34d",
                   up=(0, 0, 1), fold=0.0, thick=0.02))
    # piedras salientes, ventanas y puerta (mitad de adelante)
    for k in range(26):
        t = rnd.uniform(-1.4, 1.4)
        y = rnd.uniform(0.35, 1.65)
        o.append(UB("piedra", math.sin(t) * 1.18, y, -math.cos(t) * 1.18, rnd.uniform(0.3, 0.45), 0.2, 0.08, ry=math.degrees(t), r=0.03, seg=1,
                    col=rnd.choice([STONE2, STONE3, "a39fa8"])))
    for t in (-0.7, 0.7):
        o.append(UB("ventana", math.sin(t) * 1.17, 1.2, -math.cos(t) * 1.17, 0.18, 0.5, 0.08, ry=math.degrees(t), r=0.07, seg=2, col=INK))
    o.append(UB("marco", 0, 0.15, -1.18, 0.82, 1.18, 0.08, r=0.2, seg=2, col=STONE3))
    o.append(UB("puerta", 0, 0.15, -1.22, 0.62, 1.02, 0.06, r=0.18, seg=2, col=WOODD))
    for x in (-0.15, 0.15):
        o.append(UB("tabla", x, 0.17, -1.26, 0.04, 0.9, 0.02, r=0.01, seg=1, col="6a4026"))
    o.append(torus("aldaba", U(0.16, 0.62, -1.27), 0.045, 0.012, IRONL, seg=12, mseg=5, rot=(90, 0, 0), metal=0.5, rough=0.4))
    for k, t in enumerate((-1.1, -0.45, 0.6, 1.25)):
        o += moss(math.sin(t) * 1.3, 0.12, -math.cos(t) * 1.3, 0.24, seed=k + 2)
    return o


@prop(4000)
def bridge():
    """Puente de tablas sobre el foso: 4 m a lo largo de Z (z -2..2), 1.6 m de ancho, tablas arriba en y 0.15."""
    rnd = random.Random(8)
    o = []
    for x in (-0.55, 0.55):
        o.append(UB("larguero", x, -0.06, 0, 0.14, 0.15, 4.1, r=0.03, seg=1, col=WOODD))
    n = 13
    for i in range(n):
        z = -2.0 + (i + 0.5) * 4.0 / n
        o.append(UB("tabla", rnd.uniform(-0.03, 0.03), 0.09, z, 1.45 + rnd.uniform(-0.06, 0.06), 0.06, 4.0 / n - 0.03, ry=rnd.uniform(-2.5, 2.5),
                    r=0.02, seg=1, col=[WOODL, WOOD, "b87a45"][i % 3]))
        if i % 4 == 1:
            for x in (-0.55, 0.55):
                o.append(US("clavo", x, 0.155, z, 0.018, IRONL, seg=8))
    tops = {}
    for z in (-1.95, 0.0, 1.95):
        for x in (-0.78, 0.78):
            o.append(UB("poste", x, -0.4, z, 0.13, 1.35, 0.13, r=0.04, seg=1, col=WOODD))
            o.append(US("bocha", x, 0.98, z, 0.08, WOOD, seg=10))
            o.append(torus("amarre", U(x, 0.82, z), 0.085, 0.022, "d9b98a", seg=10, mseg=5))
            tops[(x, z)] = (x, 0.84, z)
    for x in (-0.78, 0.78):
        for z0, z1 in ((-1.95, 0.0), (0.0, 1.95)):
            o.append(tube("soga", [U(*p) for p in sag((x, 0.84, z0), (x, 0.84, z1), 0.14)], 0.022, "d9b98a"))
            o.append(tube("soga_b", [U(*p) for p in sag((x, 0.45, z0), (x, 0.45, z1), 0.07)], 0.018, "d9b98a"))
            for t in (0.25, 0.5, 0.75):
                z = z0 + (z1 - z0) * t
                yt = 0.84 - 0.14 * 4 * t * (1 - t)
                o.append(tube("atadura", [(U(x, yt, z)), U(x * 0.97, 0.15, z)], 0.012, "c9a774"))
    return o


@prop(4500)
def dungeon_arch():
    """Arco de piedra de 3 m de ancho y 2.6 m de alto con el rastrillo de hierro levantado (se puede pasar)."""
    o = []
    for x in (-1.2, 1.2):
        o.append(UB("base", x, 0, 0, 0.72, 0.25, 0.88, r=0.05, seg=2, col=STONED))
        o.append(UB("pilar", x, 0.2, 0, 0.6, 2.1, 0.78, r=0.06, seg=2, col=STONE))
        for k in range(4):
            o.append(UB("sillar", x + (0.03 if k % 2 else -0.03), 0.35 + k * 0.45, -0.4, 0.62, 0.36, 0.05, r=0.03, seg=1, col=[STONE2, STONE3][k % 2]))
    cy, ri, ro = 1.3, 0.9, 1.3
    nb = 9
    for k in range(nb):
        f = math.pi * (k + 0.5) / nb
        key = k == nb // 2
        ca, sa = math.cos(f), math.sin(f)
        r_out = ro + (0.08 if key else 0.0)
        o.append(UBeam("dovela", (ca * (ri - 0.02), cy + sa * (ri - 0.02), 0), (ca * r_out, cy + sa * r_out, 0),
                       0.36 if not key else 0.42, 0.82 if not key else 0.9, "c4683a" if False else (STONE3 if key else [STONE, STONE2][k % 2]),
                       up=(0, 0, 1), r=0.05, seg=2))
    o.append(US("emblema", 0, 2.38, -0.47, 0.09, "ffd23a", scale=(1, 1, 0.4), metal=0.5, rough=0.3))
    # rastrillo levantado: queda arriba, metido en el arco
    for x in (-0.72, -0.48, -0.24, 0.0, 0.24, 0.48, 0.72):
        top = cy + math.sqrt(max(0.0, ri * ri - x * x)) + 0.05
        o.append(UC("barra", x, 1.85, 0.05, 0.028, top - 1.85, IRON, seg=8, metal=0.5, rough=0.45))
        o.append(UC("punta", x, 1.87, 0.05, 0.045, 0.16, IRON, r2=0.0, rx=180, seg=8, metal=0.5, rough=0.45))
    for y in (1.95, 2.15):
        w = 2 * math.sqrt(ri * ri - (y - cy) ** 2)
        o.append(UB("travesano", 0, y, 0.05, w, 0.05, 0.05, r=0.015, seg=1, col=IRON, metal=0.5, rough=0.45))
    o.append(UB("umbral", 0, 0, 0, 1.85, 0.05, 0.85, r=0.02, seg=1, col=STONE2))
    for x in (-0.9, 0.9):
        o.append(torus("argolla", U(x * 1.05, 1.35, -0.42), 0.07, 0.016, IRONL, seg=12, mseg=5, rot=(90, 0, 0), metal=0.5, rough=0.4))
    for k, (x, y) in enumerate(((-1.0, 2.25), (0.8, 2.42))):
        o += moss(x, y, -0.25, 0.26, seed=k + 3, flat=0.6)
    o.append(tube("enredadera", [U(-1.0, 2.3, -0.43), U(-1.05, 1.9, -0.44), U(-0.98, 1.5, -0.44)], 0.02, LEAF3))
    for k in range(2):
        o += moss((-1, 1)[k] * 1.35, 0.08, -0.47, 0.22, seed=k + 11)
    return o


@prop(1800)
def torch():
    """Antorcha de pie: base de piedra, palo de hierro, canasta y llama emisiva (~1.75 m)."""
    o = [UL("base", 0, 0, 0, [(0, 0), (0.22, 0), (0.22, 0.08), (0.15, 0.14), (0.1, 0.18), (0, 0.18)], STONE2, seg=12)]
    o.append(UC("palo", 0, 0.16, 0, 0.032, 1.12, IRON, seg=8, metal=0.4, rough=0.5))
    for y in (0.5, 0.9):
        o.append(torus("anillo", U(0, y, 0), 0.045, 0.014, IRONL, seg=10, mseg=5, metal=0.4, rough=0.5))
    o.append(UL("canasta", 0, 1.22, 0, [(0, 0), (0.05, 0), (0.15, 0.12), (0.17, 0.2), (0.14, 0.2), (0, 0.06)], IRON, seg=12, metal=0.4, rough=0.5))
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        o.append(tube("garra", [U(math.cos(a) * 0.15, 1.38, math.sin(a) * 0.15), U(math.cos(a) * 0.19, 1.47, math.sin(a) * 0.19)], 0.014, IRON, metal=0.4))
    o.append(UC("lena", 0, 1.3, 0, 0.1, 0.12, "6a4026", seg=10))
    o += flame(0, 1.36, 0, 1.3)
    return o


@prop(2000)
def cobweb():
    """Telarana de esquina: la esquina es arriba a la izquierda (x -0.5, y 1.0), plano XY de frente a la camara."""
    cx, cy = -0.5, 1.0
    W = "f4f2fa"
    o = []
    angs = [0, 16, 34, 54, 73, 90]
    L = 0.95
    for k, a in enumerate(angs):
        ll = L * (1.0 if k in (0, len(angs) - 1) else 0.92)
        o.append(tube("radio", [U(cx, cy, 0), U(cx + math.cos(math.radians(a)) * ll, cy - math.sin(math.radians(a)) * ll, 0)], 0.009, W))
    for rr in (0.22, 0.4, 0.58, 0.76):
        pts = []
        for k in range(len(angs) - 1):
            for t in (0.0, 0.5):
                a = math.radians(angs[k] + (angs[k + 1] - angs[k]) * t)
                r = rr * (0.9 if t == 0.5 else 1.0)
                pts.append(U(cx + math.cos(a) * r, cy - math.sin(a) * r, 0))
        a = math.radians(angs[-1])
        pts.append(U(cx + math.cos(a) * rr, cy - math.sin(a) * rr, 0))
        o.append(tube("espiral", pts, 0.008, W))
    # hilo roto colgando con una aranita
    o.append(tube("hilo", [U(0.05, 0.62, 0), U(0.07, 0.4, 0)], 0.005, W))
    o.append(US("arana", 0.07, 0.36, -0.01, 0.045, "4a3a5a", seg=10))
    o.append(US("cabeza", 0.07, 0.31, -0.02, 0.03, "4a3a5a", seg=10))
    for s_ in (-1, 1):
        o.append(US("ojo", 0.07 + s_ * 0.013, 0.31, -0.045, 0.011, "fbf6ee", seg=8))
        for k in range(3):
            o.append(tube("pata", [U(0.07 + s_ * 0.03, 0.36 - k * 0.012, -0.01), U(0.07 + s_ * 0.075, 0.38 - k * 0.04, -0.01),
                                   U(0.07 + s_ * 0.09, 0.33 - k * 0.04, -0.01)], 0.006, "4a3a5a"))
    o.append(tube("hilo_suelto", [U(cx + 0.55, cy - 0.32, 0), U(cx + 0.6, cy - 0.5, 0.01)], 0.006, W))
    return o


@prop(1500)
def bat():
    """Murcielago colgado cabeza abajo (~0.3 m): cabeza abajo (orejas en y 0), patitas agarradas arriba en y ~0.32."""
    B, BD, P = "8a6ab0", "5a3f7a", "ff9ec0"
    o = [US("cuerpo", 0, 0.19, 0.01, 0.06, B, scale=(1, 1, 1.3))]
    for s_ in (-1, 1):
        # ala plegada como capa, con los dedos marcados y la garrita del pulgar abajo
        o.append(USq("ala", (s_ * 0.045, 0.19, -0.01), 0.06, BD, (0, 1, 0), scale=(0.8, 0.9, 1.7), hint=(0, 0, -1)))
        o.append(UC("garra", s_ * 0.07, 0.11, -0.04, 0.012, 0.03, "3a2a4a", r2=0.0, rx=180, seg=6))
        o.append(tube("pata", [U(s_ * 0.02, 0.26, 0.01), U(s_ * 0.022, 0.285, 0.01), U(s_ * 0.014, 0.297, -0.004)], 0.008, "3a2a4a", seg=6))
        o.append(UCyl("oreja", (s_ * 0.035, 0.06, 0.0), (s_ * 0.075, -0.0, 0.0), 0.03, B, r2=0.003, seg=8))
        o.append(UCyl("oreja_in", (s_ * 0.036, 0.055, -0.012), (s_ * 0.07, 0.005, -0.012), 0.017, P, r2=0.002, seg=6))
        o.append(US("ojo", s_ * 0.025, 0.088, -0.05, 0.02, "fbf6ee", seg=10))
        o.append(US("pupila", s_ * 0.025, 0.083, -0.066, 0.009, INK, seg=8))
        o.append(UC("colmillo", s_ * 0.012, 0.045, -0.055, 0.007, 0.02, "fbf6ee", r2=0.0, seg=6))
    o.append(US("cabeza", 0, 0.075, 0, 0.062, B))
    o.append(US("naricita", 0, 0.058, -0.06, 0.012, P, seg=8))
    o.append(US("panza", 0, 0.19, -0.04, 0.035, "b8a0d0", scale=(1, 1, 1.5), seg=10))
    return o


@prop(1500)
def slab():
    """Baldosa trampa 1x1 m que se hunde: piedra tibia con grietas (arriba a y 0.08)."""
    o = [UB("junta", 0, 0, 0, 1.0, 0.05, 1.0, r=0.02, seg=1, col="4e4a58")]
    o.append(UB("losa", 0, 0.0, 0, 0.94, 0.08, 0.94, r=0.035, seg=2, col="a89c8a"))
    o.append(UB("esquina", 0.38, 0.0, -0.38, 0.17, 0.065, 0.17, ry=12, r=0.025, seg=1, col="958a7a"))
    for pts in ([(0, 0), (-0.12, -0.08), (-0.18, -0.24), (-0.32, -0.3), (-0.44, -0.42)],
                [(0, 0), (0.14, 0.05), (0.2, 0.2), (0.36, 0.26), (0.45, 0.42)],
                [(0, 0), (-0.06, 0.16), (-0.2, 0.22), (-0.24, 0.38)],
                [(0.14, 0.05), (0.3, -0.04), (0.44, -0.02)],
                [(-0.18, -0.24), (-0.04, -0.34), (0.0, -0.45)]):
        o.append(tube("grieta", [U(x, 0.079, z) for x, z in pts], 0.013, "4a4250"))
    o.append(US("piedrita", -0.35, 0.09, 0.3, 0.035, "958a7a", scale=(1, 0.6, 1), seg=8))
    o.append(US("piedrita2", 0.25, 0.085, -0.15, 0.025, "958a7a", scale=(1, 0.6, 1), seg=8))
    return o


@prop(2800)
def dungeon_floor_rock():
    """Columna rota con escombros y un tambor caido."""
    rnd = random.Random(31)
    o = [UB("plinto", 0, 0, 0, 0.78, 0.18, 0.78, r=0.04, seg=2, col=STONED)]
    o.append(UL("fuste", 0, 0.15, 0, [(0, 0), (0.32, 0), (0.3, 0.08), (0.27, 0.12), (0.26, 0.82), (0, 0.82)], STONE, seg=16))
    o.append(UBL("rotura", 0, 0.96, 0, 0.27, STONE3, seed=4, amp=0.55, scale=(1, 1, 0.55)))
    o.append(UC("tambor", -0.95, 0.25, -0.45, 0.25, 0.42, STONE2, rz=-90, ry=-30, seg=16, bev=0.03))
    for k in range(6):
        a = rnd.uniform(0, 6.28)
        r = rnd.uniform(0.45, 0.75)
        o.append(UBL("cascote", math.cos(a) * r + 0.1, 0.05, math.sin(a) * r, rnd.uniform(0.07, 0.14), rnd.choice([STONE2, STONE3, STONED]),
                     seed=k + 5, amp=0.4, scale=(1.2, 0.8, 1.0)))
    o += moss(0.15, 0.18, -0.33, 0.18, seed=3)
    o += moss(-0.05, 1.0, 0.05, 0.16, seed=6, flat=0.8)
    return o


@prop(3000)
def chains():
    """Poste corto con argolla de la que cuelgan cadenas: una cae al piso con un grillete, otra va a una estaca."""
    o = [UB("bloque", 0, 0, 0, 0.42, 0.18, 0.42, r=0.04, seg=2, col=STONED)]
    o.append(UB("poste", 0, 0.15, 0, 0.22, 1.0, 0.22, r=0.04, seg=2, col="6a4026"))
    for y in (0.4, 0.95):
        o.append(UB("fleje", 0, y, 0, 0.24, 0.06, 0.24, r=0.015, seg=1, col=IRON, metal=0.4, rough=0.5))
    o.append(UB("tapa", 0, 1.13, 0, 0.27, 0.06, 0.27, r=0.02, seg=1, col=IRON, metal=0.4, rough=0.5))
    o.append(torus("argolla", U(0, 0.92, -0.15), 0.07, 0.018, IRONL, seg=14, mseg=6, rot=(90, 0, 0), metal=0.5, rough=0.4))
    m = dict(metal=0.45, rough=0.45)
    o += chain([(-0.03, 0.85, -0.16), (-0.08, 0.5, -0.2), (-0.1, 0.06, -0.24), (-0.1, 0.04, -0.4), (0.05, 0.04, -0.55), (0.22, 0.04, -0.58)], **m)
    o.append(torus("grillete", U(0.36, 0.05, -0.58), 0.09, 0.025, IRON, seg=14, mseg=6, scale=(1, 1, 1), **m))
    o += chain(sag((0.05, 0.86, -0.16), (0.72, 0.12, -0.2), 0.12, n=6), **m)
    o.append(UC("estaca", 0.75, 0, -0.2, 0.04, 0.16, IRON, seg=8, **m))
    o.append(torus("aro_estaca", U(0.75, 0.16, -0.2), 0.05, 0.014, IRONL, seg=12, mseg=5, rot=(90, 0, 0), **m))
    return o


# ================================================================ RUINAS DE LA JUNGLA
@prop(9000)
def temple():
    """Templo escalonado tipo maya: base 4x4 m, 3 m de alto, escalinata al frente, puerta oscura, musgo y lianas."""
    rnd = random.Random(41)
    o = []
    tiers = [(4.0, 0.0, RSTONE), (3.2, 0.7, RSTONE2), (2.4, 1.4, RSTONE)]
    for w, y, c in tiers:
        o.append(UB("grada", 0, y, 0, w, 0.72, w, r=0.06, seg=2, col=c))
        o.append(UB("friso", 0, y + 0.5, 0, w + 0.06, 0.1, w + 0.06, r=0.03, seg=1, col=RSTONED))
    # santuario arriba
    o.append(UB("santuario", 0, 2.1, 0.15, 1.7, 0.75, 1.4, r=0.06, seg=2, col="cfc6a4"))
    o.append(UB("techo", 0, 2.82, 0.15, 1.9, 0.14, 1.6, r=0.04, seg=2, col=RSTONED))
    o.append(UB("cresta", 0, 2.94, 0.35, 1.1, 0.12, 0.3, r=0.04, seg=2, col=RSTONE2))
    o.append(UB("cresta2", 0, 3.04, 0.35, 0.6, 0.12, 0.25, r=0.04, seg=2, col=RSTONE))
    o.append(UB("puerta", 0, 2.1, -0.57, 0.6, 0.58, 0.06, r=0.05, seg=2, col=INK))
    o.append(UB("dintel", 0, 2.66, -0.58, 0.82, 0.1, 0.06, r=0.02, seg=1, col=RSTONED))
    o.append(UB("mascara", 0, 2.88, -0.67, 0.34, 0.24, 0.06, r=0.05, seg=2, col="3fbfbf"))
    for s_ in (-1, 1):
        o.append(US("ojo_jade", s_ * 0.08, 2.98, -0.71, 0.03, "fbf6ee", seg=8))
    # escalinata
    z0, z1, n = -2.35, -1.22, 7
    run = (z1 - z0) / n
    for k in range(n):
        o.append(UB("escalon", 0, 0, (z0 + k * run + z1 + 0.1) * 0.5, 1.1, (k + 1) * 0.3, z1 + 0.1 - (z0 + k * run), r=0.03, seg=1,
                    col=["d2caa8", "c8c09c"][k % 2]))
    for s_ in (-1, 1):
        o.append(UBeam("alfarda", (s_ * 0.66, 0.12, z0 - 0.02), (s_ * 0.66, 2.18, z1), 0.22, 0.26, RSTONED, r=0.04))
    # glifos en las caras
    for w, y, c in tiers:
        for x in (-w * 0.32, w * 0.32):
            o.append(UB("glifo", x, y + 0.15, -w * 0.5 - 0.02, 0.32, 0.3, 0.04, r=0.04, seg=1, col=RSTONED))
            o.append(US("glifo_c", x, y + 0.3, -w * 0.5 - 0.05, 0.06, "3fbfbf" if w < 3.5 else RSTONE2, scale=(1, 1, 0.4), seg=8))
    # musgo en los bordes y lianas colgando
    for k in range(9):
        w, y, _ = tiers[k % 3]
        x = rnd.uniform(-w * 0.45, w * 0.45)
        if abs(x) < 0.7:
            x = 0.7 * (1 if x >= 0 else -1) + x * 0.5
        o += moss(x, y + 0.68, -w * 0.5 + 0.08, rnd.uniform(0.2, 0.28), seed=k + 1, flat=0.7)
    for k, (x, w, y) in enumerate(((-1.5, 4.0, 0.7), (1.3, 3.2, 1.4), (-0.95, 3.2, 1.4), (1.65, 4.0, 0.7), (-0.9, 2.4, 2.1))):
        zf = -w * 0.5 - 0.04
        pts = [U(x, y + 0.02, zf), U(x + 0.06, y - 0.2, zf - 0.01), U(x - 0.04, y - 0.42, zf - 0.01), U(x + 0.03, y - 0.6, zf)]
        o.append(tube("liana", pts, 0.025, LEAF3))
        for j in range(3):
            o.append(ULeaf("hojita", [(x + 0.02, y - 0.15 - j * 0.17, zf - 0.03), (x + 0.1, y - 0.2 - j * 0.17, zf - 0.05), (x + 0.16, y - 0.24 - j * 0.17, zf - 0.04)],
                           [0.0, 0.045, 0.0], LEAF if j % 2 else LEAF2, up=(0, 0, -1), fold=0.2, thick=0.01))
    for s_ in (-1, 1):
        o.append(UBL("arbusto", s_ * 2.0, 0.18, -2.05, 0.36, LEAF3, seed=s_ + 5, amp=0.25, scale=(1.3, 0.8, 1.0)))
    return o


@prop(4500)
def jungle_tree():
    """Arbol tropical (~4.2 m): tronco compacto (raices dentro de r 0.5), copa con hojas anchas y lianas colgando."""
    rnd = random.Random(52)
    o = [UL("tronco", 0, 0, 0, [(0, 0), (0.42, 0), (0.3, 0.2), (0.23, 0.7), (0.2, 2.0), (0.22, 2.7), (0, 2.85)], "8a5a36", seg=12)]
    for k in range(5):
        a = k * 1.256 + 0.3
        o.append(tube("raiz", [U(math.cos(a) * 0.12, 0.55, math.sin(a) * 0.12), U(math.cos(a) * 0.32, 0.2, math.sin(a) * 0.32),
                               U(math.cos(a) * 0.46, 0.02, math.sin(a) * 0.46)], 0.06, "7a4a2a", seg=8))
    branches = []
    for k in range(4):
        a = k * 1.57 + 0.6
        tip = (math.cos(a) * 1.15, 3.2, math.sin(a) * 1.0)
        branches.append(tip)
        o.append(tube("rama", [U(0, 2.3, 0), U(tip[0] * 0.5, 2.85, tip[2] * 0.5), U(*tip)], 0.08, "7a4a2a", seg=8))
    copa = [(0, 3.55, 0, 1.15, LEAF3), (0.85, 3.3, -0.3, 0.8, LEAF), (-0.85, 3.35, 0.2, 0.85, LEAF), (0.1, 3.3, 0.85, 0.8, LEAF3),
            (-0.2, 3.25, -0.8, 0.78, LEAF2)]
    for i, (x, y, z, r, c) in enumerate(copa):
        o.append(UBL("copa", x, y, z, r, c, seed=i + 2, amp=0.16, scale=(1, 1, 0.62)))
    # hojas anchas que caen por el borde
    for k in range(12):
        a = k * 2 * math.pi / 12 + rnd.uniform(-0.1, 0.1)
        r0 = 1.05
        dx, dz = math.cos(a), math.sin(a)
        y0 = 3.3 + rnd.uniform(-0.1, 0.15)
        L = rnd.uniform(0.75, 0.95)
        pts = [(dx * r0, y0, dz * r0), (dx * (r0 + L * 0.35), y0 + 0.08, dz * (r0 + L * 0.35)), (dx * (r0 + L * 0.7), y0 - 0.08, dz * (r0 + L * 0.7)),
               (dx * (r0 + L), y0 - 0.35, dz * (r0 + L))]
        o.append(ULeaf("hoja", pts, [0.05, 0.26, 0.22, 0.0], [LEAF, LEAF2, "6ccf5a"][k % 3], fold=0.3, thick=0.02))
    # lianas
    for k, tip in enumerate(branches):
        x, y, z = tip
        bot = 0.9 + (k % 2) * 0.6
        pts = [U(x * 0.8, 3.0, z * 0.8), U(x * 0.85 + 0.05, (3.0 + bot) * 0.5, z * 0.85), U(x * 0.8, bot, z * 0.8 - 0.05)]
        o.append(tube("liana", pts, 0.025, LEAF3, seg=8))
        for j in range(2):
            yy = bot + 0.25 + j * 0.45
            o.append(ULeaf("hojita", [(x * 0.82, yy, z * 0.82 - 0.03), (x * 0.82 + 0.08, yy - 0.04, z * 0.82 - 0.06), (x * 0.82 + 0.15, yy - 0.08, z * 0.82 - 0.05)],
                           [0.0, 0.05, 0.0], LEAF2, up=(0, 0, -1), fold=0.2, thick=0.01))
        if k % 2 == 0:
            o.append(US("flor", x * 0.8, bot, z * 0.8 - 0.07, 0.06, "ff5d8f", seg=10))
    for k in range(3):
        o.append(US("fruta", 0.4 * math.cos(k * 2.1), 2.98, 0.4 * math.sin(k * 2.1) - 0.2, 0.08, "ffd34d", seg=10))
    return o


@prop(2000)
def fern():
    """Helecho: frondas serradas que salen del centro y caen (~1.3 m de ancho, 0.6 m de alto)."""
    rnd = random.Random(63)
    o = [UBL("mata", 0, 0.05, 0, 0.16, LEAF3, seed=2, amp=0.3, scale=(1, 1, 0.7))]
    n = 9
    for k in range(n):
        a = k * 2 * math.pi / n + rnd.uniform(-0.15, 0.15)
        dx, dz = math.cos(a), math.sin(a)
        L = rnd.uniform(0.55, 0.7)
        hh = rnd.uniform(0.45, 0.6)
        pts, ws = [], []
        m = 12
        for i in range(m + 1):
            t = i / float(m)
            r = 0.05 + L * t
            y = 0.08 + hh * math.sin(t * 2.4) * (1.0 - 0.15 * t)
            pts.append((dx * r, y, dz * r))
            env = math.sin(min(1.0, t * 1.15) * math.pi) * 0.11 + 0.01
            ws.append(env * (1.0 if i % 2 == 0 else 0.45))
        ws[-1] = 0.0
        o.append(ULeaf("fronda", pts, ws, [LEAF, LEAF2, "6ccf5a"][k % 3], fold=0.15, thick=0.012))
    return o


@prop(3000)
def vine_pillar():
    """Columna rota envuelta en lianas (~1.65 m)."""
    o = [UB("plinto", 0, 0, 0, 0.8, 0.22, 0.8, r=0.05, seg=2, col=RSTONED)]
    o.append(UL("fuste", 0, 0.2, 0, [(0, 0), (0.36, 0), (0.33, 0.1), (0.3, 0.14), (0.28, 1.25), (0, 1.25)], RSTONE, seg=16))
    o.append(torus("junta", U(0, 0.8, 0), 0.29, 0.012, RSTONED, seg=16, mseg=4))
    o.append(UBL("rotura", 0, 1.43, 0, 0.29, "cfc6a4", seed=7, amp=0.6, scale=(1, 1, 0.6)))
    o.append(UBL("trozo", 0.65, 0.08, -0.25, 0.17, RSTONE2, seed=3, amp=0.4, scale=(1.3, 0.8, 1)))
    pts = []
    for i in range(23):
        t = i / 22.0
        a = t * 2 * math.pi * 1.6 + 0.8
        pts.append((math.cos(a) * 0.32, 0.15 + t * 1.3, math.sin(a) * 0.32))
    o.append(tube("liana", [U(*p) for p in pts], 0.03, LEAF3, seg=8))
    for i in range(1, 22, 2):
        x, y, z = pts[i]
        nx, nz = x / 0.32, z / 0.32
        o.append(ULeaf("hoja", [(x, y, z), (x + nx * 0.08, y + 0.03, z + nz * 0.08), (x + nx * 0.16, y - 0.02, z + nz * 0.16)],
                       [0.0, 0.06, 0.0], [LEAF, LEAF2][i % 2], fold=0.25, thick=0.012))
    o += moss(0.05, 1.5, 0.02, 0.2, seed=5, flat=0.9)
    o += moss(-0.3, 0.2, -0.32, 0.18, seed=9)
    return o


@prop(4500)
def stone_head():
    """Cabeza gigante de piedra (tipo olmeca) medio enterrada, con musgo (~1.8 m de ancho, 1.5 m de alto)."""
    C, CD = "bdb497", "958f72"
    o = [US("cabeza", 0, 0.58, 0, 0.8, C, scale=(1.05, 1.12, 0.95))]
    o.append(UL("casco", 0, 1.02, 0, [(0, 0), (0.86, 0), (0.88, 0.1), (0.78, 0.34), (0.52, 0.52), (0.22, 0.6), (0, 0.62)], CD, seg=24))
    o.append(torus("banda", U(0, 1.04, 0), 0.84, 0.06, RSTONED, seg=28, mseg=6))
    for s_ in (-1, 1):
        o.append(UBc("ceja", s_ * 0.24, 0.94, -0.71, 0.34, 0.08, 0.12, rz=-s_ * 8, r=0.04, seg=2, col=CD))
        o.append(US("ojo", s_ * 0.25, 0.81, -0.72, 0.12, "f4efe0", scale=(1.25, 0.8, 0.5)))
        o.append(US("pupila", s_ * 0.22, 0.8, -0.78, 0.065, "2b2620", scale=(1, 1.1, 0.5), seg=12))
        o.append(US("chapeta", s_ * 0.45, 0.57, -0.6, 0.1, "d8a890", scale=(1.2, 0.8, 0.4), seg=10))
        o.append(UBc("oreja", s_ * 0.82, 0.70, 0.0, 0.16, 0.42, 0.3, r=0.07, seg=2, col=CD))
        o.append(US("arete", s_ * 0.88, 0.65, 0.0, 0.07, "3fbfbf", scale=(0.6, 1, 1), seg=10))
    o.append(UBL("nariz", 0, 0.62, -0.79, 0.15, C, seed=4, amp=0.15, scale=(1.4, 1.0, 0.8)))
    for s_ in (-1, 1):
        o.append(US("narina", s_ * 0.09, 0.56, -0.86, 0.03, "6d6650", scale=(1, 0.7, 0.6), seg=8))
    o.append(US("labio", 0, 0.40, -0.73, 0.1, CD, scale=(2.4, 0.7, 0.8)))
    o.append(US("labio2", 0, 0.29, -0.7, 0.09, CD, scale=(2.2, 0.65, 0.75)))
    head = list(o)
    tilt(head, (0, 0, 0), rx=-14, rz=4)
    o.append(UBL("tierra", 0, 0.0, 0.0, 0.95, "a8904a", seed=3, amp=0.25, scale=(1.15, 1.05, 0.18)))
    o += moss(-0.25, 1.52, 0.3, 0.4, seed=5, flat=0.6)
    o += moss(0.6, 1.25, 0.0, 0.2, seed=6, flat=0.7)
    o.append(tube("liana", [U(0.6, 1.5, -0.2), U(0.74, 1.3, -0.4), U(0.8, 1.05, -0.45), U(0.8, 0.85, -0.42)], 0.025, LEAF3, seg=8))
    for k in range(5):
        a = k * 1.25 + 0.3
        x, z = math.cos(a) * 1.0, math.sin(a) * 0.9
        o.append(ULeaf("pasto", [(x, 0.0, z), (x * 1.05, 0.16, z * 1.05), (x * 1.12, 0.28, z * 1.1)], [0.05, 0.04, 0.0], LEAF2, up=(0, 0, -1), fold=0.1))
    return o


@prop(3000)
def monkey():
    """Monito sentado (~0.5 m), marron con cara color crema, sosteniendo una banana."""
    B, T = "8a5428", "f0c890"
    o = [US("cuerpo", 0, 0.15, 0.02, 0.12, B, scale=(1, 0.95, 1.15))]
    o.append(US("panza", 0, 0.15, -0.055, 0.085, T, scale=(1, 0.6, 1.15)))
    o.append(US("cabeza", 0, 0.37, -0.01, 0.12, B))
    for s_ in (-1, 1):
        o.append(US("cara", s_ * 0.042, 0.39, -0.08, 0.055, T, scale=(1, 0.6, 1.1)))
        o.append(US("ojo", s_ * 0.042, 0.395, -0.118, 0.022, INK, seg=10))
        o.append(US("brillo", s_ * 0.036, 0.405, -0.137, 0.008, "ffffff", seg=6))
        o.append(USq("oreja", (s_ * 0.125, 0.38, 0.0), 0.05, B, (s_, 0, 0), scale=(1, 1, 0.45), seg=10))
        o.append(USq("oreja_in", (s_ * 0.14, 0.38, -0.004), 0.034, T, (s_, 0, 0), scale=(1, 1, 0.4), seg=8))
        o.append(US("muslo", s_ * 0.07, 0.08, -0.09, 0.06, B, scale=(1, 1.4, 0.8)))
        o.append(US("pie", s_ * 0.085, 0.03, -0.17, 0.045, T, scale=(1.2, 1.5, 0.6), seg=10))
    o.append(US("hocico", 0, 0.34, -0.095, 0.065, T, scale=(1.25, 0.65, 0.8)))
    for s_ in (-1, 1):
        o.append(US("narina", s_ * 0.015, 0.355, -0.145, 0.008, "6a4026", seg=6))
    o.append(tube("sonrisa", [U(-0.03, 0.33, -0.142), U(0, 0.318, -0.148), U(0.03, 0.33, -0.142)], 0.006, "6a4026", seg=6))
    for k in range(3):
        o.append(US("mechon", -0.02 + k * 0.02, 0.48, -0.02, 0.025, B, seg=8))
    # brazos: el izquierdo en la rodilla, el derecho sube la banana
    o.append(tube("brazo_i", [U(-0.1, 0.25, -0.02), U(-0.12, 0.17, -0.08), U(-0.08, 0.12, -0.16)], 0.028, B, seg=8))
    o.append(US("mano_i", -0.08, 0.12, -0.17, 0.032, T, seg=10))
    o.append(tube("brazo_d", [U(0.1, 0.25, -0.02), U(0.14, 0.2, -0.1), U(0.1, 0.24, -0.17)], 0.028, B, seg=8))
    o.append(US("mano_d", 0.1, 0.24, -0.18, 0.032, T, seg=10))
    o.append(tube("banana", [U(0.06, 0.2, -0.2), U(0.11, 0.25, -0.21), U(0.13, 0.31, -0.2), U(0.12, 0.36, -0.18)], 0.022, "ffd34d", seg=8))
    o.append(US("punta", 0.12, 0.365, -0.178, 0.012, "6a4026", seg=6))
    o.append(tube("cola", [U(0, 0.06, 0.12), U(0.12, 0.03, 0.24), U(0.26, 0.08, 0.24), U(0.3, 0.2, 0.16), U(0.24, 0.27, 0.12), U(0.2, 0.22, 0.12)],
                  0.022, B, seg=8))
    return o


# ================================================================ PICO HELADO / VOLCAN
@prop(2800)
def snow_pine():
    """Pino nevado (~2.6 m) con una capa de nieve sobre cada piso."""
    h = 2.6
    o = [UC("tronco", 0, 0, 0, 0.13, h * 0.35, "7a4a2a", r2=0.1, seg=10, bev=0.02)]
    for k in range(3):
        y = h * (0.2 + k * 0.22)
        r = 0.85 * (1 - k * 0.24)
        hh = h * 0.42
        o.append(UL("copa", 0, y, 0, [(0, 0), (r, 0.0), (r * 1.02, 0.08), (r * 0.55, hh * 0.55), (0.06, hh), (0, hh)], "2f7a4a" if k % 2 == 0 else "3a8a55", seg=14))
        # capa de nieve pegada a la pendiente desde el 25% de alto hasta la punta
        def rad(yy):
            if yy <= 0.08:
                return r * 1.02
            if yy <= hh * 0.55:
                return r * 1.02 + (r * 0.55 - r * 1.02) * (yy - 0.08) / (hh * 0.55 - 0.08)
            return r * 0.55 + (0.06 - r * 0.55) * (yy - hh * 0.55) / (hh * 0.45)
        y1 = hh * 0.46
        o.append(UL("nieve", 0, y, 0, [(0, y1 - 0.02), (rad(y1) + 0.03, y1 - 0.02), (rad(y1) + 0.045, y1 + 0.03), (rad(hh * 0.55) + 0.04, hh * 0.55 + 0.02),
                                        (0.09, hh + 0.02), (0, hh + 0.04)], SNOW, seg=14))
        for j in range(6):
            a = j * 1.047 + k * 0.5
            o.append(US("gota", math.cos(a) * (rad(y1) + 0.03), y + y1 - 0.03, math.sin(a) * (rad(y1) + 0.03), 0.055, SNOW, scale=(1, 1, 1.3), seg=8))
    o.append(US("punta", 0, h * 0.2 + 2 * h * 0.22 + h * 0.42 + 0.02, 0, 0.08, SNOW, seg=10))
    o.append(UBL("monton", 0.0, 0.0, 0.0, 0.45, SNOW, seed=4, amp=0.25, scale=(1.3, 1.3, 0.25)))
    return o


@prop(2000)
def ice_rock():
    """Roca de hielo celeste (~1 m) con cristales que salen."""
    o = [UBL("roca", 0, 0.28, 0, 0.45, "a8dcf5", seed=5, amp=0.25, scale=(1.2, 1.0, 0.8), rough=0.15)]
    o.append(UBL("roca2", 0.45, 0.15, 0.15, 0.25, ICED, seed=6, amp=0.3, rough=0.15))
    for k, (x, z, ax, az, L, r) in enumerate(((-0.25, -0.1, -25, -10, 0.75, 0.11), (0.05, 0.05, 6, 8, 0.95, 0.13), (0.28, -0.12, 30, -12, 0.6, 0.1),
                                              (-0.05, -0.25, -5, -35, 0.5, 0.08), (0.5, 0.1, 35, 0, 0.45, 0.07))):
        dx, dz = math.sin(math.radians(ax)), math.sin(math.radians(az))
        dy = math.sqrt(max(0.1, 1 - dx * dx - dz * dz))
        b = (x, 0.15, z)
        o.append(UCyl("cristal", b, (x + dx * L * 0.8, 0.15 + dy * L * 0.8, z + dz * L * 0.8), r, ["dff4ff", "c4ecff"][k % 2], seg=6, rough=0.1, emis=0.2))
        o.append(UCyl("punta", (x + dx * L * 0.8, 0.15 + dy * L * 0.8, z + dz * L * 0.8), (x + dx * L, 0.15 + dy * L, z + dz * L), r, "f0faff", r2=0.0, seg=6,
                      rough=0.1, emis=0.3))
    o.append(UBL("nieve", -0.35, 0.42, 0.15, 0.2, SNOW, seed=2, amp=0.3, scale=(1.3, 1.2, 0.4)))
    o.append(UBL("monton", 0, 0.0, 0, 0.6, SNOW, seed=7, amp=0.2, scale=(1.3, 1.1, 0.15)))
    return o


@prop(2500)
def lava_rock():
    """Roca de basalto (~1 m) con grietas naranjas que brillan (emisivas), pegadas a la superficie."""
    rock = UBL("roca", 0, 0.3, 0, 0.48, BASALT, seed=7, amp=0.3, scale=(1.2, 1.0, 0.85))
    o = [rock, UBL("roca2", -0.5, 0.12, -0.1, 0.22, BASALT2, seed=8, amp=0.3)]
    c = (0, 0.3, 0)
    for poly in ([(-0.8, 0.3, -0.6), (-0.3, 0.0, -1), (0.0, 0.25, -1), (0.35, -0.05, -1), (0.8, 0.1, -0.6)],
                 [(0.0, 0.25, -1), (0.05, 0.7, -0.7), (-0.1, 1, -0.2), (0.2, 1, 0.3)],
                 [(0.6, 0.6, -0.5), (0.9, 0.3, -0.3), (1, -0.1, -0.1)],
                 [(-0.3, 0.0, -1), (-0.5, -0.4, -0.8)],
                 [(-0.1, 1, -0.2), (-0.6, 0.8, -0.1), (-0.9, 0.5, 0.0)]):
        pts = surf(rock, crack_dirs(poly), c, inset=0.012)
        o.append(tube("grieta", pts, 0.028, "ff7a1a", seg=6, emis=3.0))
        o.append(tube("brillo", pts, 0.012, FLAME2, seg=4, emis=4.0))
    return o


@prop(1800)
def snowman():
    """Munequito de nieve (~1.1 m) con bufanda, nariz de zanahoria y sombrero."""
    o = [US("base", 0, 0.28, 0, 0.32, SNOW, scale=(1, 1, 0.9))]
    o.append(US("medio", 0, 0.68, 0, 0.23, SNOW))
    o.append(US("cabeza", 0, 0.98, 0, 0.16, SNOW))
    o.append(torus("bufanda", U(0, 0.84, 0), 0.15, 0.05, "e0594a", seg=16, mseg=6))
    o.append(UB("punta_buf", 0.1, 0.62, -0.16, 0.09, 0.22, 0.04, rz=-12, r=0.02, seg=1, col="e0594a"))
    o.append(UC("nariz", 0, 0.97, -0.15, 0.03, 0.14, "f59a32", r2=0.0, rx=-90, seg=8))
    for s_ in (-1, 1):
        o.append(US("ojo", s_ * 0.055, 1.03, -0.14, 0.022, INK, seg=8))
        o.append(tube("brazo", [U(s_ * 0.2, 0.72, 0), U(s_ * 0.38, 0.86, -0.02), U(s_ * 0.46, 0.95, -0.02)], 0.018, "7a4a2a", seg=6))
    for k in range(3):
        o.append(US("boton", 0, 0.58 + k * 0.1, -0.21 + k * 0.01, 0.025, INK, seg=8))
    o.append(UC("ala", 0, 1.1, 0, 0.17, 0.03, "2b3040", seg=16))
    o.append(UC("copa", 0, 1.12, 0, 0.11, 0.18, "2b3040", seg=16))
    o.append(UC("cinta", 0, 1.13, 0, 0.115, 0.04, "4a8fe0", seg=16))
    return o


@prop(2500)
def steam_vent():
    """Fumarola: anillo de piedras (~1 m) con el centro naranja emisivo y un penacho de vapor."""
    rnd = random.Random(81)
    o = [UC("fondo", 0, 0.0, 0, 0.34, 0.04, "ff8a2a", seg=16, emis=3.0)]
    o.append(UBL("brasa", 0, 0.03, 0, 0.2, FLAME2, seed=2, amp=0.3, scale=(1, 1, 0.25), emis=4.0))
    for k in range(9):
        a = k * 2 * math.pi / 9 + rnd.uniform(-0.15, 0.15)
        r = 0.42 + rnd.uniform(-0.04, 0.04)
        o.append(UBL("piedra", math.cos(a) * r, 0.03, math.sin(a) * r, rnd.uniform(0.1, 0.16), rnd.choice([BASALT, BASALT2, "5e5662"]),
                     seed=k + 3, amp=0.45, scale=(1.3, 1.0, 0.65)))
    for k, (x, y, z, r) in enumerate(((0.0, 0.2, 0.0, 0.08), (0.09, 0.42, 0.02, 0.11), (-0.06, 0.7, 0.04, 0.14), (0.05, 0.74, 0.0, 0.1))):
        o.append(UBL("vapor", x, y, z, r, ["eef0f6", "e2e4ee", "f6f4fa"][k % 3], seed=k + 20, amp=0.3, scale=(1.2, 1.2, 0.9)))
    return o


@prop(2500)
def balloon_dock():
    """Plataforma de aterrizaje del globo: 2.5 x 2.5 m, tablas arriba en y 0.2, poste con banderin."""
    rnd = random.Random(91)
    o = []
    for x in (-1.1, 1.1):
        for z in (-1.1, 1.1):
            o.append(UB("pata", x, 0, z, 0.16, 0.16, 0.16, r=0.04, seg=1, col=WOODD))
    for z in (-0.8, 0.0, 0.8):
        o.append(UB("viga", 0, 0.05, z, 2.45, 0.09, 0.14, r=0.03, seg=1, col=WOODD))
    n = 9
    for i in range(n):
        x = -1.25 + (i + 0.5) * 2.5 / n
        o.append(UB("tabla", x, 0.14, rnd.uniform(-0.03, 0.03), 2.5 / n - 0.03, 0.06, 2.48 + rnd.uniform(-0.05, 0.05), r=0.02, seg=1,
                    col=[WOODL, WOOD, "b87a45"][i % 3]))
    o.append(torus("circulo", U(0, 0.2, 0), 0.6, 0.04, "fbf6ee", seg=32, mseg=6, scale=(1, 1, 0.25)))
    o.append(US("centro", 0, 0.2, 0, 0.12, "e0594a", scale=(1, 1, 0.12), seg=14))
    o.append(UB("poste", -1.1, 0.2, -1.1, 0.12, 1.5, 0.12, r=0.03, seg=1, col=WOODD))
    o.append(US("bocha", -1.1, 1.72, -1.1, 0.06, "ffd23a", seg=10))
    o.append(ULeaf("banderin", [(-1.06, 1.62, -1.1), (-0.85, 1.55, -1.13), (-0.62, 1.5, -1.09)], [0.13, 0.08, 0.0], "e0594a", up=(0, 0, 1), fold=0.0, thick=0.02))
    o.append(torus("soga", U(-1.1, 0.35, -1.1), 0.09, 0.025, "d9b98a", seg=10, mseg=5))
    o.append(UB("cajon", 0.85, 0.2, 0.85, 0.4, 0.3, 0.4, r=0.03, seg=1, col=WOODL))
    o.append(US("bolsa", 0.85, 0.62, 0.85, 0.12, "e8dcc8", scale=(1, 1, 0.9), seg=10))
    return o


# ================================================================ VEHICULOS DE EVENTO
HINGE = (0, 1.45, -1.55)


def crane():
    """Grua movil amarilla (frente +Z). body: camion; boom: pluma + gancho (pivote en la bisagra)."""
    Y, YD, BLK = "ffc53a", "e8a92a", "2b2a30"
    body = [UB("chasis", 0, 0.42, 0.05, 1.5, 0.3, 4.0, r=0.06, col="3a3a44")]
    body.append(UB("plataforma", 0, 0.7, -0.55, 1.7, 0.16, 2.9, r=0.05, col=Y))
    for k in range(6):
        body.append(UB("franja", -0.75 + k * 0.3, 0.72, -2.02, 0.14, 0.12, 0.03, r=0.01, seg=1, col=BLK))
    body.append(UB("cabina", 0, 0.7, 1.45, 1.6, 1.12, 1.05, r=0.16, col=Y))
    body.append(UB("vidrio", 0, 1.22, 1.97, 1.36, 0.46, 0.04, rx=-8, r=0.06, col="bfe4ff", rough=0.1, emis=0.1))
    for s_ in (-1, 1):
        body.append(UB("ventana", s_ * 0.8, 1.22, 1.5, 0.03, 0.42, 0.7, r=0.05, col="bfe4ff", rough=0.1))
        body.append(US("faro", s_ * 0.55, 0.85, 1.98, 0.1, "fff1b0", emis=1.0, scale=(1, 1, 0.5)))
        body.append(UB("espejo", s_ * 0.9, 1.3, 1.85, 0.06, 0.18, 0.1, r=0.02, seg=1, col=BLK))
    body.append(UB("paragolpe", 0, 0.42, 2.02, 1.66, 0.16, 0.12, r=0.05, col="c9d2da", metal=0.6, rough=0.3))
    body.append(UB("techo", 0, 1.8, 1.45, 1.4, 0.05, 0.8, r=0.03, seg=1, col=YD))
    body.append(UC("baliza", 0, 1.84, 1.45, 0.08, 0.1, "f59a32", seg=10, emis=1.5))
    # ruedas (3 ejes)
    for z in (1.35, -0.6, -1.45):
        for s_ in (-1, 1):
            x = s_ * 0.78
            body.append(cyl("goma", U(x, 0.36, z), 0.36, 0.28, BLK, seg=20, rot=(0, 90, 0), bev=0.04))
            body.append(cyl("llanta", U(x + s_ * 0.06, 0.36, z), 0.2, 0.18, "c9d2da", seg=14, rot=(0, 90, 0), metal=0.5, rough=0.3))
            body.append(US("tuerca", x + s_ * 0.16, 0.36, z, 0.05, YD, seg=8))
        body.append(UB("guardabarro", 0, 0.78, z, 1.82, 0.06, 0.82, r=0.03, seg=1, col=BLK) if z != 1.35 else None)
    # estabilizadores
    for z in (0.75, -2.0):
        for s_ in (-1, 1):
            body.append(UB("brazo", s_ * 1.0, 0.5, z, 0.62, 0.14, 0.18, r=0.03, seg=1, col=Y))
            body.append(UB("raya", s_ * 1.08, 0.5, z - 0.095, 0.12, 0.14, 0.02, r=0.005, seg=1, col=BLK))
            body.append(UC("gato", s_ * 1.28, 0.07, z, 0.05, 0.5, "c9d2da", seg=8, metal=0.5, rough=0.3))
            body.append(UC("zapata", s_ * 1.28, 0.0, z, 0.17, 0.07, "3a3a44", seg=12, bev=0.02))
    # torreta
    body.append(UC("corona", 0, 0.86, -1.05, 0.62, 0.14, "3a3a44", seg=20))
    body.append(UB("torreta", 0, 0.98, -1.25, 1.3, 0.5, 1.25, r=0.1, col=Y))
    body.append(UB("contrapeso", 0, 0.98, -1.95, 1.3, 0.55, 0.35, r=0.08, col=BLK))
    body.append(UB("cabinita", -0.45, 1.46, -0.85, 0.5, 0.62, 0.55, r=0.1, col=Y))
    body.append(UB("vidrio2", -0.45, 1.6, -0.57, 0.38, 0.32, 0.03, r=0.04, seg=1, col="bfe4ff", rough=0.1))
    for s_ in (-1, 1):
        body.append(UB("oreja", s_ * 0.3, 1.45, HINGE[2], 0.1, 0.24, 0.32, r=0.03, seg=1, col=YD))
    body.append(cyl("perno", U(-0.38, HINGE[1], HINGE[2]), 0.07, 0.76, "c9d2da", seg=10, rot=(0, -90, 0), metal=0.5, rough=0.3))
    # pluma a 45 grados hacia adelante
    ang = math.radians(45)
    d = (0, math.sin(ang), math.cos(ang))
    def P(t):
        return (HINGE[0], HINGE[1] + d[1] * t, HINGE[2] + d[2] * t)
    boom = [UBeam("pluma", P(-0.1), P(2.1), 0.42, 0.46, Y, r=0.07)]
    boom.append(UBeam("pluma2", P(1.9), P(3.45), 0.32, 0.34, YD, r=0.06))
    for t in (0.35, 0.75, 1.15):
        boom.append(UBeam("raya", P(t), P(t + 0.16), 0.43, 0.47, BLK, r=0.03, seg=1))
    tip = P(3.5)
    boom.append(cyl("polea", U(-0.12, tip[1], tip[2]), 0.13, 0.24, "3a3a44", seg=14, rot=(0, -90, 0)))
    boom.append(UCyl("piston", (0, 1.25, -0.75), P(1.25), 0.07, "c9d2da", seg=10, metal=0.5, rough=0.3))
    boom.append(UCyl("camisa", (0, 1.25, -0.75), (0, 1.25 + (P(1.25)[1] - 1.25) * 0.55, -0.75 + (P(1.25)[2] + 0.75) * 0.55), 0.1, "3a3a44", seg=10))
    hz = tip[2] + 0.13
    boom.append(tube("cable", [U(0, tip[1] - 0.05, hz), U(0, 2.62, hz)], 0.015, BLK, seg=6))
    boom.append(UB("aparejo", 0, 2.38, hz, 0.24, 0.26, 0.14, r=0.04, seg=1, col=Y))
    boom.append(UB("aparejo_r", 0, 2.44, hz - 0.075, 0.25, 0.06, 0.01, r=0.005, seg=1, col=BLK))
    boom.append(tube("gancho", [U(0, 2.38, hz), U(0, 2.2, hz), U(0.0, 2.08, hz - 0.08), U(0, 2.08, hz - 0.18), U(0, 2.18, hz - 0.2)], 0.028,
                     "6d747e", seg=8, metal=0.5, rough=0.35))
    return [("body", [x for x in body if x is not None], (0, 0, 0)), ("boom", boom, U(*HINGE))]


def balloon():
    """Globo aerostatico (~3.7 m): gajos rojo/amarillo, canasta de mimbre, sogas y quemador."""
    o = []
    prof = [(0.28, 1.6), (0.45, 1.8), (0.78, 2.08), (1.02, 2.42), (1.12, 2.78), (1.1, 3.1), (0.96, 3.36), (0.7, 3.56), (0.38, 3.68), (0, 3.72)]
    o += gores("gajo", prof, ["e0594a", "ffd34d"], n=12, per=3)
    o.append(torus("faja", U(0, 2.42, 0), 1.03, 0.045, "4a8fe0", seg=36, mseg=6))
    o.append(torus("boca", U(0, 1.6, 0), 0.28, 0.035, WOODD, seg=16, mseg=6))
    o.append(UB("canasta", 0, 0, 0, 0.78, 0.52, 0.78, r=0.08, col=WOODL))
    for y in (0.14, 0.3):
        o.append(UB("trenza", 0, y, 0, 0.8, 0.05, 0.8, r=0.025, seg=1, col=WOOD))
    o.append(UB("borde", 0, 0.48, 0, 0.84, 0.08, 0.84, r=0.04, seg=2, col=WOODD))
    for sx in (-1, 1):
        for sz in (-1, 1):
            o.append(tube("soga", [U(sx * 0.38, 0.55, sz * 0.38), U(sx * 0.2, 1.6, sz * 0.2)], 0.014, "d9b98a", seg=6))
            o.append(tube("vara", [U(sx * 0.36, 0.55, sz * 0.36), U(sx * 0.1, 1.08, sz * 0.1)], 0.014, IRONL, seg=6, metal=0.4))
    o.append(UC("quemador", 0, 1.06, 0, 0.12, 0.14, "9aa1aa", seg=12, metal=0.5, rough=0.3))
    o += flame(0, 1.2, 0, 0.75)
    for s_ in (-1, 1):
        o.append(tube("cuerda_bolsa", [U(s_ * 0.4, 0.5, -0.2), U(s_ * 0.43, 0.32, -0.2)], 0.01, "d9b98a", seg=6))
        o.append(UBL("bolsa", s_ * 0.44, 0.24, -0.2, 0.08, "e8dcc8", seed=3 + s_, amp=0.2, scale=(0.9, 0.9, 1.2)))
    return [("body", o, (0, 0, 0))]


VEH = {"crane": crane, "balloon": balloon}
VBUDGET = {"crane": 9000, "balloon": 6000}


def fin(objs, name, budget):
    """lib.finish con presupuesto de triangulos (decimate si se pasa)."""
    o = lib.join(objs, name)
    lib.decimate_to(o, budget)
    cols = lib.bake_colors(o, 0.5, 0.3)
    tris = lib.export([(name, o, (0, 0, 0), cols)], os.path.join(OUT, name + ".bytes"))
    lib._apply_preview_colors(o, cols)
    return o, tris


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(PROPS) + list(VEH)
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if prev:
        os.makedirs(prev, exist_ok=True)
    for n in only:
        lib.reset()
        if n in VEH:
            parts, t = lib.finish_parts(VEH[n](), "veh_" + n, OUT, ao=0.45, ao_dist=0.2, budget=VBUDGET[n])
            print("ZONE veh", n, t)
            if prev:
                lib.preview(parts, os.path.join(prev, "veh_" + n + ".png"), size=420, elev=28, azim=-40)
            continue
        objs = [x for x in PROPS[n]() if x is not None]
        o, t = fin(objs, "prop_" + n, BUDGET[n])
        bpy.context.view_layer.update()
        xs = [(o.matrix_world @ Vector(c)) for c in o.bound_box]
        print("ZONE prop", n, t, "size x %.2f z %.2f h %.2f" % (max(v.x for v in xs) - min(v.x for v in xs), max(v.y for v in xs) - min(v.y for v in xs),
                                                               max(v.z for v in xs)))
        if prev:
            lib.preview([o], os.path.join(prev, "prop_" + n + ".png"), size=360, elev=40, azim=196)


if __name__ == "__main__":
    main()
