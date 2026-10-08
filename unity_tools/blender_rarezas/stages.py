"""El local en sus etapas 2 a 5 (despues del rancho y el galpon de shack.py): tienda de antiguedades, galeria, museo y
museo de lujo. Mismas convenciones que shack.py: coordenadas de Unity con U() (x derecha, y arriba, z hacia el fondo =
norte), centro del interior en el origen, piso a y = FY, frente a -Z con la pared de adelante BAJA (corte de maqueta)
para que la camara (orto, 48 grados, mirando al norte) vea adentro. Paredes del fondo y costados altas.

Interior (medias x, z): shack2 (3, 2.25), shack3 (4, 2.75), shack4 (5.5, 3.5), shack5 (7, 4.5). El piso queda libre
(los exhibidores los pone el codigo en una grilla de 0.5 m) salvo el rincon del taller atras a la izquierda y los
muebles fijos contra las paredes que lista FIXED. Puerta adelante a la derecha: x de hx-0.95 a hx-0.25.

Piezas de cada "shackN": shell (piso, paredes, columnas, decoracion de pared), roof (todo lo de arriba de las paredes y la
fachada alta: se esconde cuando entras), sign (cartel RAREZAS adelante a la izquierda), open (cartel OPEN/CLOSED que
gira; pivote en el clavo).

Uso: blender -b -P stages.py -- [--only stage2,stage3,stage4,stage5] [--preview carpeta]
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Euler
import lib
from lib import rbox, sphere, cyl, torus, tube, lathe, blob
import shack
from shack import U, UB, UC, FY, OUT


def US(name, x, y, z, r, col, **kw):
    """Esfera (como shack.US) con menos gajos: aca hay cientos de esferitas."""
    k = r * (max(kw["scale"]) if kw.get("scale") is not None else 1.0)
    kw.setdefault("seg", max(8, min(18, int(8 + k * 60))))
    return sphere(name, U(x, y, z), r, col, **kw)


def text(name, s, x, y, z, size, col, depth=0.02, ry=0, rx=0, res=2, **kw):
    """Como shack.text (fuente del juego, de frente a -Z) pero con menos resolucion de curva (menos triangulos)."""
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = s
    try:
        cu.font = bpy.data.fonts.load(shack.FONT)
    except Exception:
        pass
    cu.size = size
    cu.extrude = depth
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    cu.resolution_u = res
    o = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    lib.setmat(o, col, **kw)
    o.rotation_euler = (math.radians(90 + rx), 0, math.radians(180 + ry))
    o.location = U(x, y, z)
    return o

T = 0.14            # espesor de pared
GOLD = "e6b23a"
GOLDD = "b8862f"
INK = "2b2440"

# Muebles fijos dentro del rectangulo interior (x0, x1, z0, z1) por etapa, para que el codigo no ponga exhibidores ahi.
def bench_rect(hx, hz):
    return (-hx + 0.1, -hx + 1.1, hz - 0.75, hz)


# ================================================================ ayudas geometricas
def B(o, name, x0, x1, y0, y1, z0, z1, col, r=0.02, seg=2, **kw):
    """Caja por extremos (coordenadas de Unity)."""
    o.append(UB(name, (x0 + x1) * 0.5, y0, (z0 + z1) * 0.5, x1 - x0, y1 - y0, z1 - z0, r=r, seg=seg, col=col, **kw))
    return o[-1]


def _mesh_multi(name, bm, cols, rough=0.55):
    idx = [f.material_index for f in bm.faces]
    o = lib._obj_from_bm(name, bm)
    o.data.materials.clear()
    for c in cols:
        o.data.materials.append(lib.mat(c, rough=rough))
    o.data.polygons.foreach_set("material_index", idx)   # to_mesh sin materiales los dejaba en 0
    o.data.update()
    return o


def hull(name, pts, col, r=0.0, seg=2, **kw):
    """Cuerpo convexo de puntos en coordenadas de Unity (frontones, techos, diamantes, triangulos)."""
    bm = bmesh.new()
    for p in pts:
        bm.verts.new(Vector(U(*p)))
    res = bmesh.ops.convex_hull(bm, input=list(bm.verts))
    extra = list({g for g in res.get("geom_interior", []) + res.get("geom_unused", []) if isinstance(g, bmesh.types.BMVert)})
    if extra:
        bmesh.ops.delete(bm, geom=extra, context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    o = lib._obj_from_bm(name, bm)
    lib.setmat(o, col, **kw)
    if r > 0:
        lib.bevel(o, r, seg)
    lib.smooth(o, 30)
    return o


def flat(name, polys, y):
    """Poligonos planos horizontales (pisos) en un solo objeto, cada uno con su color: polys = [([(x, z)...], col)]."""
    bm = bmesh.new()
    order = {}
    for pts, col in polys:
        if len(pts) < 3:
            continue
        vs = [bm.verts.new(Vector(U(x, y, z))) for (x, z) in pts]
        try:
            f = bm.faces.new(vs)
        except ValueError:
            continue
        f.material_index = order.setdefault(col, len(order))
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    return _mesh_multi(name, bm, list(order.keys()), rough=0.45)


def clip(pts, x0, x1, z0, z1):
    """Recorta un poligono convexo (x, z) al rectangulo (Sutherland-Hodgman)."""
    for ax, val, ge in ((0, x0, True), (0, x1, False), (1, z0, True), (1, z1, False)):
        if not pts:
            return pts
        out = []
        for i in range(len(pts)):
            a, b = pts[i - 1], pts[i]
            ia = a[ax] >= val if ge else a[ax] <= val
            ib = b[ax] >= val if ge else b[ax] <= val
            if ia != ib:
                t = (val - a[ax]) / (b[ax] - a[ax])
                out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            if ib:
                out.append(b)
        pts = out
    return pts


def rect(x0, x1, z0, z1, g=0.0):
    return [(x0 + g, z0 + g), (x1 - g, z0 + g), (x1 - g, z1 - g), (x0 + g, z1 - g)]


class Face:
    """Una cara de pared. n = normal ("-z", "+z", "+x", "-x"), c = coordenada del plano. u = a lo largo (a la derecha del
    que la mira de frente), v = altura, d = cuanto sale de la cara."""

    def __init__(self, n, c):
        self.n, self.c = n, c
        self.ry = {"-z": 0, "+z": 180, "+x": 90, "-x": -90}[n]

    def P(self, u, v, d=0.0):
        n, c = self.n, self.c
        if n == "-z":
            return (u, v, c - d)
        if n == "+z":
            return (-u, v, c + d)
        if n == "+x":
            return (c + d, v, u)
        return (c - d, v, -u)

    def box(self, o, name, u, v, d, w, h, dep, col, r=0.015, seg=2, **kw):
        x, y, z = self.P(u, v, d + dep * 0.5)
        o.append(UB(name, x, y, z, w, h, dep, ry=self.ry, r=r, seg=seg, col=col, **kw))
        return o[-1]

    def shape(self, o, name, pts, d, dep, col, r=0.0, **kw):
        P = [self.P(u, v, d) for (u, v) in pts] + [self.P(u, v, d + dep) for (u, v) in pts]
        o.append(hull(name, P, col, r=r, **kw))
        return o[-1]

    def disc(self, o, name, u, v, d, rad, dep, col, n=18, sy=1.0, **kw):
        pts = [(u + rad * math.cos(2 * math.pi * i / n), v + rad * sy * math.sin(2 * math.pi * i / n)) for i in range(n)]
        return self.shape(o, name, pts, d, dep, col, **kw)

    def word(self, o, name, s, u, v, d, size, col, depth=0.012):
        x, y, z = self.P(u, v, d + depth)
        o.append(text(name, s, x, y, z, size, col, depth=depth, ry=self.ry))
        return o[-1]


def LT(x0, z0, ry):
    """Transformacion local -> mundo con giro ry (el frente -Z local gira hacia +X con ry > 0)."""
    a = math.radians(ry)
    ca, sa = math.cos(a), math.sin(a)

    def f(lx, ly, lz):
        return (x0 + lx * ca - lz * sa, ly, z0 + lx * sa + lz * ca)
    return f


def slope_rows(o, name, x0, x1, A, Bp, n, cols, th=0.07, over=0.06, rnd=None, **kw):
    """Faldon de techo con la cumbrera a lo largo de X: filas de tejas de A=(z, y) a B=(z, y)."""
    (za, ya), (zb, yb) = A, Bp
    L = math.hypot(zb - za, yb - ya)
    ang = math.degrees(math.atan2(yb - ya, zb - za))      # >0 si sube hacia +z
    for k in range(n):
        t = (k + 0.5) / n
        zc, yc = za + (zb - za) * t, ya + (yb - ya) * t
        jit = rnd.uniform(-0.03, 0.03) if rnd else 0.0
        o.append(UB(name, (x0 + x1) * 0.5 + jit, yc - th * 0.5, zc, x1 - x0, th, L / n + over, rx=-ang, r=0.02, seg=1,
                    col=cols[k % len(cols)], **kw))


def walls(o, hx, hz, H, ext, inn):
    """Pared del fondo y costados (altas) con su piel interior. El frente lo arma cada etapa."""
    B(o, "pared_fondo", -hx - T, hx + T, 0, H, hz, hz + T, ext, r=0.02, seg=1)
    B(o, "pared_izq", -hx - T, -hx, 0, H, -hz - T, hz, ext, r=0.02, seg=1)
    B(o, "pared_der", hx, hx + T, 0, H, -hz - T, hz, ext, r=0.02, seg=1)
    for sk in (B(o, "piel_fondo", -hx, hx, FY, H, hz - 0.012, hz + 0.004, inn, r=0.003, seg=1),
               B(o, "piel_izq", -hx - 0.004, -hx + 0.012, FY, H, -hz - 0.004, hz, inn, r=0.003, seg=1),
               B(o, "piel_der", hx - 0.012, hx + 0.004, FY, H, -hz - 0.004, hz, inn, r=0.003, seg=1)):
        lib.dense(sk, 2)


def wainscot(o, hx, hz, h, col, rail, panel=None, base=None, panels_every=0.7):
    d0 = 0.012
    B(o, "zocalo_f", -hx, hx, FY, h, hz - d0 - 0.025, hz - d0, col, r=0.006, seg=1)
    B(o, "zocalo_i", -hx + d0, -hx + d0 + 0.025, FY, h, -hz, hz - d0, col, r=0.006, seg=1)
    B(o, "zocalo_d", hx - d0 - 0.025, hx - d0, FY, h, -hz, hz - d0, col, r=0.006, seg=1)
    B(o, "riel_f", -hx, hx, h - 0.01, h + 0.045, hz - 0.075, hz - d0, rail, r=0.015, seg=1)
    B(o, "riel_i", -hx + d0, -hx + 0.06, h - 0.01, h + 0.045, -hz, hz - d0, rail, r=0.015, seg=1)
    B(o, "riel_d", hx - 0.06, hx - d0, h - 0.01, h + 0.045, -hz, hz - d0, rail, r=0.015, seg=1)
    bc = base or rail
    B(o, "base_f", -hx, hx, FY, FY + 0.09, hz - 0.06, hz - d0, bc, r=0.01, seg=1)
    if panel:
        n = max(2, int(2 * hx / panels_every))
        w = 2 * hx / n
        for i in range(n):
            x = -hx + (i + 0.5) * w
            B(o, "panel", x - w * 0.36, x + w * 0.36, FY + 0.15, h - 0.08, hz - d0 - 0.04, hz - d0 - 0.02, panel, r=0.012, seg=1)


def crown(o, hx, hz, H, col, d=0.07, h=0.09):
    B(o, "cornisa_f", -hx, hx, H - h, H, hz - d, hz, col, r=0.02, seg=1)
    B(o, "cornisa_i", -hx, -hx + d, H - h, H, -hz, hz, col, r=0.02, seg=1)
    B(o, "cornisa_d", hx - d, hx, H - h, H, -hz, hz, col, r=0.02, seg=1)


def sparse_blocks(o, rnd, f, u0, u1, v0, v1, bw, bh, cols, density, dep=0.018, stag=True):
    rows = int((v1 - v0) / bh)
    for r_ in range(rows):
        off = (r_ % 2) * bw * 0.5 if stag else 0.0
        u = u0 - off
        while u < u1 - 0.04:
            a, b = max(u, u0), min(u + bw, u1)
            if b - a > bw * 0.3 and rnd.random() < density:
                va, vb = v0 + r_ * bh + 0.007, v0 + (r_ + 1) * bh - 0.007
                f.shape(o, "bloque", [(a + 0.008, va), (b - 0.008, va), (b - 0.008, vb), (a + 0.008, vb)], 0.0, dep,
                        cols[rnd.randrange(len(cols))])
            u += bw


def painting(o, f, u, v, w, h, art, frame=GOLD, back="4e2e1a"):
    """Cuadro colgado en una cara (v = borde de abajo). art: paisaje, retrato, abstracto, flores, mar, noche."""
    b = min(0.08, w * 0.12)
    f.box(o, "cuadro_fondo", u, v, 0.0, w, h, 0.03, back, r=0.008, seg=1)
    u0, u1, v0, v1 = u - w / 2 + b, u + w / 2 - b, v + b, v + h - b
    W, Hh = u1 - u0, v1 - v0
    bg = {"paisaje": "a9d4ee", "retrato": "3d3b52", "abstracto": "f3ead8", "flores": "2f4e5a", "mar": "bfe3f2",
          "noche": "26305a"}[art]
    f.box(o, "lienzo", u, v0, 0.03, W, Hh, 0.006, bg, r=0.002, seg=1)
    d1, d2, d3, th = 0.036, 0.04, 0.044, 0.004
    if art == "paisaje":
        f.disc(o, "sol", u1 - W * 0.22, v1 - Hh * 0.25, d1, min(W, Hh) * 0.11, th, "ffd23a", n=14)
        f.shape(o, "monte", [(u0, v0), (u0 + W * 0.42, v0 + Hh * 0.62), (u0 + W * 0.85, v0)], d1, th, "7d8fb3")
        f.shape(o, "monte2", [(u0 + W * 0.35, v0), (u0 + W * 0.72, v0 + Hh * 0.45), (u1, v0)], d2, th, "5f9a5c")
        f.box(o, "pasto", u, v0, d3 - 0.002, W, Hh * 0.14, th, "79b94f", r=0.001, seg=1)
    elif art == "retrato":
        f.disc(o, "pelo", u, v0 + Hh * 0.6, d1, W * 0.22, th, "5a3520", n=16, sy=1.15)
        f.shape(o, "hombros", [(u - W * 0.38, v0), (u - W * 0.3, v0 + Hh * 0.28), (u, v0 + Hh * 0.36),
                               (u + W * 0.3, v0 + Hh * 0.28), (u + W * 0.38, v0)], d1, th, "8e2f3a")
        f.disc(o, "cara", u, v0 + Hh * 0.55, d2, W * 0.16, th, "f1c9a5", n=16, sy=1.25)
        f.box(o, "cuello", u, v0 + Hh * 0.3, d1, W * 0.12, Hh * 0.12, th, "e8bc96", r=0.001, seg=1)
    elif art == "abstracto":
        f.box(o, "rojo", u0 + W * 0.25, v0 + Hh * 0.45, d1, W * 0.5, Hh * 0.55, th, "e0594a", r=0.001, seg=1)
        f.box(o, "azul", u0 + W * 0.78, v0, d1, W * 0.44, Hh * 0.4, th, "4a6fd0", r=0.001, seg=1)
        f.box(o, "amarillo", u0 + W * 0.85, v0 + Hh * 0.7, d1, W * 0.3, Hh * 0.3, th, "ffd23a", r=0.001, seg=1)
        f.box(o, "linea", u0 + W * 0.52, v0, d2, 0.02, Hh, th, INK, r=0.001, seg=1)
        f.box(o, "linea", u, v0 + Hh * 0.42, d2, W, 0.02, th, INK, r=0.001, seg=1)
        f.disc(o, "circulo", u0 + W * 0.22, v0 + Hh * 0.2, d2, min(W, Hh) * 0.13, th, "5cbf63", n=14)
    elif art == "flores":
        f.shape(o, "florero", [(u - W * 0.12, v0 + Hh * 0.05), (u + W * 0.12, v0 + Hh * 0.05), (u + W * 0.16, v0 + Hh * 0.35),
                               (u - W * 0.16, v0 + Hh * 0.35)], d1, th, "d9a441")
        for k, (du, dv, c) in enumerate(((-0.18, 0.55, "e0594a"), (0.0, 0.68, "ff8fb0"), (0.18, 0.55, "ffd23a"),
                                         (-0.08, 0.8, "f3ead8"), (0.12, 0.78, "e0594a"))):
            f.disc(o, "flor", u + W * du, v0 + Hh * dv, d2, min(W, Hh) * 0.1, th, c, n=10)
        f.box(o, "mesa_c", u, v0, d1, W, Hh * 0.06, th, "8a5a36", r=0.001, seg=1)
    elif art == "mar":
        f.box(o, "agua", u, v0, d1, W, Hh * 0.42, th, "3f7fc0", r=0.001, seg=1)
        f.shape(o, "casco", [(u - W * 0.25, v0 + Hh * 0.42), (u + W * 0.25, v0 + Hh * 0.42), (u + W * 0.18, v0 + Hh * 0.32),
                             (u - W * 0.18, v0 + Hh * 0.32)], d2, th, "7a4a2a")
        f.shape(o, "vela", [(u - W * 0.02, v0 + Hh * 0.45), (u - W * 0.02, v0 + Hh * 0.9), (u + W * 0.22, v0 + Hh * 0.45)], d2, th, "fbf6ee")
    elif art == "noche":
        f.disc(o, "luna", u1 - W * 0.25, v1 - Hh * 0.28, d1, min(W, Hh) * 0.14, th, "fff1b0", n=14)
        for k in range(5):
            f.disc(o, "estrella", u0 + W * (0.12 + 0.17 * k), v1 - Hh * (0.15 + 0.12 * (k % 2)), d1, 0.018, th, "fff6e4", n=6)
        f.shape(o, "colina", [(u0, v0), (u0, v0 + Hh * 0.25), (u0 + W * 0.5, v0 + Hh * 0.38), (u1, v0 + Hh * 0.2), (u1, v0)], d2, th, "1f3f4a")
    # marco
    for (cu, cv, cw, chh) in ((u, v, w, b), (u, v + h - b, w, b), (u - w / 2 + b / 2, v + b, b, h - 2 * b), (u + w / 2 - b / 2, v + b, b, h - 2 * b)):
        f.box(o, "marco", cu, cv, 0.0, cw, chh, 0.065, frame, r=0.014, seg=1)


def tiffany(o, x, y, z, s=1.0):
    """Pantalla Tiffany (vitral de colores) con el borde de bronce; y = arriba de la pantalla."""
    prof = [(0.025 * s, 0.0), (0.08 * s, -0.025 * s), (0.135 * s, -0.075 * s), (0.175 * s, -0.14 * s), (0.19 * s, -0.19 * s)]
    cols = ["3f8a5c", "e8a33a", "c0392b", "e8a33a"]
    for k in range(len(prof) - 1):
        o.append(lathe("tiffany", U(x, y, z), [prof[k], prof[k + 1]], cols[k], seg=16))
    o.append(torus("borde_t", U(x, y - 0.19 * s, z), 0.19 * s, 0.012 * s, "8a6a3a", seg=16, mseg=6))
    o.append(US("foco", x, y - 0.13 * s, z, 0.05 * s, "fff1b0", emis=2.0))
    o.append(US("remate", x, y + 0.01, z, 0.025 * s, "8a6a3a"))


def column(o, x, z, h, r, col, cap=None, base=None, y0=0.0, fluted=False):
    cap = cap or col
    base = base or col
    o.append(UB("plinto", x, y0, z, r * 2.6, 0.12, r * 2.6, r=0.02, seg=2, col=base))
    o.append(torus("toro", U(x, y0 + 0.14, z), r * 1.05, r * 0.2, base, seg=16, mseg=5))
    o.append(UC("fuste", x, y0 + 0.12, z, r, h - 0.32 - y0, col, r2=r * 0.86, seg=14, cap=False))
    lib.dense(o[-1], 1)
    o.append(torus("collar", U(x, h - 0.2, z), r * 0.95, r * 0.18, cap, seg=16, mseg=5))
    o.append(UC("equino", x, h - 0.2, z, r * 0.95, 0.1, cap, r2=r * 1.35, seg=16))
    o.append(UB("abaco", x, h - 0.1, z, r * 2.8, 0.1, r * 2.8, r=0.02, seg=1, col=cap))


def window(o, f, u, v, w, h, frame="fbf6ee", glass="bfe0ea", arch=False, sill=True):
    f.box(o, "ventana_marco", u, v, 0.0, w, h, 0.03, frame, r=0.01, seg=1)
    f.box(o, "vidrio", u, v + 0.06, 0.03, w - 0.12, h - 0.12, 0.004, glass, r=0.002, seg=1)
    f.box(o, "parteluz", u, v + 0.06, 0.03, 0.035, h - 0.12, 0.02, frame, r=0.006, seg=1)
    f.box(o, "travesano", u, v + h * 0.62, 0.03, w - 0.12, 0.035, 0.02, frame, r=0.006, seg=1)
    f.shape(o, "brillo", [(u - w * 0.32, v + h * 0.3), (u - w * 0.22, v + h * 0.3), (u - w * 0.36, v + h * 0.5), (u - w * 0.4, v + h * 0.5)],
            0.035, 0.003, "f4fbff")
    if arch:
        f.disc(o, "arco", u, v + h, 0.0, w * 0.5, 0.03, frame, n=16)
        f.disc(o, "arco_v", u, v + h, 0.03, w * 0.5 - 0.06, 0.004, glass, n=16)
    if sill:
        f.box(o, "alfeizar", u, v - 0.05, 0.0, w + 0.12, 0.06, 0.08, frame, r=0.012, seg=1)


def rope_posts(o, pts, h=0.55, post="d9a441", rope="b8262f"):
    for (x, z) in pts:
        o.append(UC("base_poste", x, FY, z, 0.085, 0.03, post, r2=0.07, seg=14))
        o.append(UC("poste", x, FY + 0.03, z, 0.022, h - 0.06, post, seg=10))
        o.append(US("bocha", x, FY + h - 0.01, z, 0.04, post))
    for (a, b) in zip(pts[:-1], pts[1:]):
        P = []
        for k in range(7):
            t = k / 6.0
            P.append(U(a[0] + (b[0] - a[0]) * t, FY + h - 0.07 - 0.12 * math.sin(math.pi * t), a[1] + (b[1] - a[1]) * t))
        o.append(tube("soga", P, 0.018, rope, seg=8))


def banner(o, f, u, vtop, w, h, col, trim=GOLD, emblem="ffd23a", rod=GOLDD):
    f.box(o, "tela", u, vtop - h, 0.02, w, h, 0.012, col, r=0.004, seg=1)
    f.shape(o, "punta", [(u - w / 2, vtop - h + 0.002), (u + w / 2, vtop - h + 0.002), (u, vtop - h - w * 0.4)], 0.02, 0.012, col)
    for s_ in (-1, 1):
        f.box(o, "ribete", u + s_ * (w / 2 - 0.03), vtop - h, 0.032, 0.025, h - 0.03, 0.004, trim, r=0.002, seg=1)
    f.disc(o, "emblema", u, vtop - h * 0.45, 0.034, w * 0.27, 0.008, emblem, n=16)
    f.disc(o, "emblema_c", u, vtop - h * 0.45, 0.042, w * 0.15, 0.008, col, n=12)
    x0, y0, z0 = f.P(u - w / 2 - 0.08, vtop + 0.02, 0.026)
    x1, y1, z1 = f.P(u + w / 2 + 0.08, vtop + 0.02, 0.026)
    o.append(tube("barra", [U(x0, y0, z0), U(x1, y1, z1)], 0.018, rod, seg=8))
    o.append(US("pomo", x0, y0, z0, 0.03, rod))
    o.append(US("pomo", x1, y1, z1, 0.03, rod))


def workbench(o, hx, hz, top="8a5a36", leg="6a4026", board="c98d55", lamp="3f8a5c", deluxe=False):
    """Rincon del taller (como el banco del galpon): banco, estante, tablero de herramientas, morsa, lampara."""
    cx, cz = -hx + 0.6, hz - 0.45
    o.append(UB("banco", cx, FY + 0.48, cz, 1.0, 0.08, 0.6, r=0.02, col=top))
    for dx in (-0.43, 0.43):
        for dz in (-0.25, 0.25):
            if deluxe:
                o.append(UC("pata", cx + dx, FY, cz + dz, 0.04, 0.48, leg, r2=0.028, seg=12))
            else:
                o.append(UB("pata", cx + dx, FY, cz + dz, 0.08, 0.48, 0.08, r=0.02, col=leg))
    o.append(UB("estante", cx, FY + 0.12, cz, 0.92, 0.04, 0.52, r=0.01, col=leg))
    o.append(UB("caja", cx - 0.2, FY + 0.16, cz, 0.36, 0.18, 0.3, r=0.03, col="c0392b"))
    o.append(UB("trapo", cx + 0.22, FY + 0.16, cz + 0.02, 0.3, 0.05, 0.26, r=0.025, col="e8d6ae"))
    o.append(UB("tabla_herr", cx, 0.85, hz - 0.045, 1.0, 0.6, 0.035, r=0.02, col=board))
    for k in range(4):
        x = cx - 0.35 + k * 0.22
        o.append(tube("herramienta", [U(x, 1.3, hz - 0.07), U(x, 0.95, hz - 0.07)], 0.018, ["9aa1aa", "a8703f", "e0594a", "6d747e"][k]))
        o.append(US("clavo", x, 1.33, hz - 0.07, 0.015, "6d747e"))
    o.append(UB("martillo", cx + 0.38, 1.12, hz - 0.08, 0.16, 0.05, 0.04, r=0.015, col="6d747e"))
    # morsa
    o.append(UB("morsa", cx + 0.32, FY + 0.56, cz - 0.24, 0.16, 0.1, 0.14, r=0.02, col="4a8fe0"))
    o.append(UB("morsa_m", cx + 0.32, FY + 0.66, cz - 0.24, 0.18, 0.05, 0.06, r=0.015, col="6d747e"))
    o.append(UC("lata_pintura", cx - 0.3, FY + 0.56, cz - 0.05, 0.07, 0.12, "e0594a"))
    o.append(UC("pincel", cx - 0.18, FY + 0.6, cz - 0.05, 0.012, 0.16, "c98d55", rz=40))
    o.append(UB("libro", cx + 0.02, FY + 0.56, cz + 0.08, 0.22, 0.04, 0.16, r=0.01, col="5a8fd0", ry=12))
    # lupa
    o.append(torus("lupa", U(cx + 0.05, FY + 0.6, cz - 0.15), 0.06, 0.012, "d9a441", seg=16, mseg=6))
    o.append(UC("lupa_v", cx + 0.05, FY + 0.6, cz - 0.15, 0.05, 0.004, "cfeaf5", seg=14))
    # lampara articulada
    lx, lz = cx - 0.38, cz + 0.18
    o.append(UC("lamp_base", lx, FY + 0.56, lz, 0.07, 0.03, lamp, seg=14))
    o.append(tube("lamp_brazo", [U(lx, FY + 0.58, lz), U(lx + 0.05, FY + 0.88, lz - 0.05), U(lx + 0.22, FY + 0.95, lz - 0.15)], 0.012, "2b2440"))
    o.append(UC("lamp_pantalla", lx + 0.24, FY + 0.86, lz - 0.16, 0.09, 0.1, lamp, r2=0.035, seg=14))
    o.append(US("lamp_foco", lx + 0.24, FY + 0.87, lz - 0.16, 0.035, "fff1b0", emis=2.0))
    if deluxe:
        o.append(UB("banco_ribete", cx, FY + 0.47, cz - 0.3, 1.02, 0.03, 0.02, r=0.008, seg=1, col=GOLD))


def open_sign(opn, shell, px, py, pz, wall_z, arm="2b2440"):
    """Cartel OPEN/CLOSED colgado de un brazo que sale de la fachada (pivote en el clavo, gira sobre Y)."""
    shell.append(tube("brazo_cartel", [U(px, py + 0.03, wall_z), U(px, py + 0.03, pz), U(px, py - 0.0, pz)], 0.012, arm))
    shell.append(US("brazo_punta", px, py + 0.03, wall_z, 0.03, arm))
    opn.append(UB("tabla", px, py - 0.34, pz, 0.46, 0.24, 0.04, r=0.03, col="fbf6ee"))
    opn.append(UB("borde_v", px, py - 0.34, pz - 0.005, 0.42, 0.2, 0.04, r=0.03, col="5cbf63"))
    opn.append(text("open", "OPEN", px, py - 0.22, pz - 0.035, 0.13, "fbf6ee", depth=0.008))
    opn.append(UB("borde_r", px, py - 0.34, pz + 0.005, 0.42, 0.2, 0.04, r=0.03, col="e0594a"))
    opn.append(text("closed", "CLOSED", px, py - 0.22, pz + 0.035, 0.1, "fbf6ee", depth=0.008, ry=180))
    for s_ in (-1, 1):
        opn.append(tube("hilo", [U(px + s_ * 0.17, py - 0.1, pz), U(px, py, pz)], 0.006, "6a4026"))
    opn.append(US("clavo", px, py, pz, 0.018, "9aa1aa"))
    return U(px, py, pz)


def plank_floor(name, hx, hz, rnd, cols, w=0.2, gap=0.012):
    polys = []
    n = max(1, int(round(2 * hz / w)))
    w = 2 * hz / n
    for i in range(n):
        z0, z1 = -hz + i * w, -hz + (i + 1) * w
        x = -hx - rnd.uniform(0, 1.0)
        while x < hx:
            L = rnd.uniform(0.8, 1.6)
            a, b = max(x, -hx), min(x + L, hx)
            if b - a > 0.05:
                polys.append((rect(a, b, z0, z1, gap * 0.5), cols[rnd.randrange(len(cols))]))
            x += L
    return flat(name, polys, FY)


def herringbone(name, hx, hz, rnd, cols_a, cols_b, w=0.14, k=4, gap=0.008):
    """Parquet en espiga: tablitas de w x k*w a +-45 grados, recortadas al interior."""
    s = w / math.sqrt(2)
    g = gap / w
    polys = []
    R = int((hx + hz) * 1.6 / w) + 2 * k
    for m in range(-R // k - 2, R // k + 3):
        for i in range(-R, R):
            ou, ov = i + m * k, i - m * k
            for kind in (0, 1):
                if kind == 0:
                    q = [(ou + g, ov + g), (ou + k - g, ov + g), (ou + k - g, ov + 1 - g), (ou + g, ov + 1 - g)]
                else:
                    q = [(ou + g, ov + 1 + g), (ou + 1 - g, ov + 1 + g), (ou + 1 - g, ov + 1 + k - g), (ou + g, ov + 1 + k - g)]
                pts = [((uu - vv) * s, (uu + vv) * s) for (uu, vv) in q]
                xs = [p[0] for p in pts]
                zs = [p[1] for p in pts]
                if max(xs) < -hx or min(xs) > hx or max(zs) < -hz or min(zs) > hz:
                    continue
                c = clip(pts, -hx, hx, -hz, hz)
                if len(c) >= 3:
                    cols = cols_a if kind == 0 else cols_b
                    polys.append((c, cols[rnd.randrange(len(cols))]))
    return flat(name, polys, FY)


def dome(name, cx, cy, cz, R, Hd, nseg, nring, cols):
    """Cupula de gajos (cada pano con su color, alternados como vidrios)."""
    bm = bmesh.new()
    rings = []
    for j in range(nring):
        t = j / nring * (math.pi / 2)
        r = R * math.cos(t)
        y = cy + Hd * math.sin(t)
        rings.append([bm.verts.new(Vector(U(cx + r * math.cos(2 * math.pi * i / nseg), y, cz + r * math.sin(2 * math.pi * i / nseg))))
                      for i in range(nseg)])
    top = bm.verts.new(Vector(U(cx, cy + Hd, cz)))
    for j in range(nring):
        for i in range(nseg):
            i2 = (i + 1) % nseg
            if j < nring - 1:
                f = bm.faces.new((rings[j][i], rings[j][i2], rings[j + 1][i2], rings[j + 1][i]))
            else:
                f = bm.faces.new((rings[j][i], rings[j][i2], top))
            f.material_index = (i + j) % len(cols)
    bm.normal_update()
    c = Vector(U(cx, cy, cz))
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - c) < 0:
            f.normal_flip()
    o = _mesh_multi(name, bm, cols, rough=0.25)
    return o


# ================================================================ etapa 2: tienda de antiguedades (6 x 4.5 m)
def rug(o, cx, cz, w, d, rnd):
    """Alfombra persa gastada, pintada en el piso (chatita)."""
    y = FY
    B(o, "alf_borde", cx - w / 2, cx + w / 2, y - 0.004, y + 0.006, cz - d / 2, cz + d / 2, "8c3a33", r=0.003, seg=1)
    B(o, "alf_banda", cx - w / 2 + 0.1, cx + w / 2 - 0.1, y - 0.004, y + 0.008, cz - d / 2 + 0.1, cz + d / 2 - 0.1, "2f3d5c", r=0.003, seg=1)
    B(o, "alf_campo", cx - w / 2 + 0.2, cx + w / 2 - 0.2, y - 0.004, y + 0.01, cz - d / 2 + 0.2, cz + d / 2 - 0.2, "a8473c", r=0.003, seg=1)

    def diamond(name, x, z, a, b, yy, col):
        o.append(hull(name, [(x - a, yy, z), (x + a, yy, z), (x, yy, z - b), (x, yy, z + b),
                             (x - a, yy + 0.002, z), (x + a, yy + 0.002, z), (x, yy + 0.002, z - b), (x, yy + 0.002, z + b)], col))
    diamond("medallon", cx, cz, w * 0.26, d * 0.3, y + 0.01, "2f3d5c")
    diamond("medallon2", cx, cz, w * 0.18, d * 0.21, y + 0.0115, "e8d6ae")
    diamond("medallon3", cx, cz, w * 0.09, d * 0.11, y + 0.013, "c0392b")
    for sx in (-1, 1):
        for sz in (-1, 1):
            x0, z0 = cx + sx * (w / 2 - 0.2), cz + sz * (d / 2 - 0.2)
            o.append(hull("esquina", [(x0, y + 0.01, z0), (x0 - sx * 0.38, y + 0.01, z0), (x0, y + 0.01, z0 - sz * 0.3),
                                      (x0, y + 0.012, z0), (x0 - sx * 0.38, y + 0.012, z0), (x0, y + 0.012, z0 - sz * 0.3)], "2f3d5c"))
    # dibujitos en la banda
    for i in range(int(w / 0.22)):
        x = cx - w / 2 + 0.16 + i * 0.22
        for z in (cz - d / 2 + 0.15, cz + d / 2 - 0.15):
            diamond("motivo", x, z, 0.04, 0.03, y + 0.008, "e8d6ae")
    # gastado: manchas claras
    for k in range(5):
        x, z = cx + rnd.uniform(-w * 0.38, w * 0.38), cz + rnd.uniform(-d * 0.32, d * 0.32)
        a, b = rnd.uniform(0.08, 0.16), rnd.uniform(0.05, 0.09)
        o.append(hull("gastado", [(x + a * math.cos(t), yy, z + b * math.sin(t)) for t in (0, 1.2, 2.3, 3.3, 4.4, 5.4) for yy in (y + 0.0102, y + 0.0108)], "ae4f42"))
    # flecos
    for sx in (-1, 1):
        for i in range(int(d / 0.09)):
            z = cz - d / 2 + 0.06 + i * 0.09
            o.append(tube("fleco", [U(cx + sx * w / 2, y + 0.004, z), U(cx + sx * (w / 2 + 0.07), y + 0.003, z + rnd.uniform(-0.01, 0.01))],
                          0.009, "efe2c4", seg=4))


def cash_register(o, x, y, z):
    br, brd = "b07a3a", "8a5a2a"
    o.append(UB("caja_base", x, y, z, 0.46, 0.08, 0.36, r=0.02, col=brd))
    o.append(hull("caja_cuerpo", [(x - 0.21, y + 0.08, z - 0.16), (x + 0.21, y + 0.08, z - 0.16), (x - 0.21, y + 0.08, z + 0.17), (x + 0.21, y + 0.08, z + 0.17),
                                  (x - 0.21, y + 0.22, z - 0.04), (x + 0.21, y + 0.22, z - 0.04), (x - 0.21, y + 0.3, z + 0.17), (x + 0.21, y + 0.3, z + 0.17)],
                  br, r=0.02))
    for r_ in range(3):
        for c in range(5):
            kx = x - 0.16 + c * 0.08
            ky = y + 0.11 + r_ * 0.045
            kz = z - 0.135 + r_ * 0.05
            o.append(UC("tecla", kx, ky, kz, 0.022, 0.03, "fbf1dc" if (r_ + c) % 3 else "e0594a", seg=10))
    o.append(UB("marquesina", x, y + 0.3, z + 0.1, 0.3, 0.13, 0.08, r=0.03, col=br))
    o.append(UB("visor", x, y + 0.33, z + 0.055, 0.22, 0.07, 0.01, r=0.01, col="fbf1dc"))
    o.append(text("precio", "$1", x, y + 0.365, z + 0.045, 0.06, INK, depth=0.004))
    o.append(UB("cajon_reg", x, y + 0.005, z - 0.19, 0.4, 0.07, 0.03, r=0.01, col=brd))
    o.append(US("tirador", x, y + 0.04, z - 0.21, 0.018, GOLD))
    o.append(tube("manija", [U(x + 0.23, y + 0.18, z + 0.05), U(x + 0.29, y + 0.18, z + 0.05), U(x + 0.29, y + 0.08, z - 0.05)], 0.012, brd))
    o.append(US("manija_p", x + 0.29, y + 0.08, z - 0.05, 0.025, "e0594a"))
    for s_ in (-1, 1):
        o.append(torus("voluta", U(x + s_ * 0.215, y + 0.18, z + 0.08), 0.05, 0.012, GOLD, seg=14, mseg=6, rot=(0, 90, 0)))


def stage2():
    """Tienda de antiguedades: fachada de ladrillo con vidriera y toldo a rayas verde y crema, campanita en la puerta,
    mostrador de madera con caja registradora de bronce (atras a la derecha), alfombra persa gastada, lamparas Tiffany,
    cartel de madera tallada con letras doradas."""
    rnd = random.Random(21)
    hx, hz, H = 3.0, 2.25, 2.05
    door = (hx - 0.95, hx - 0.25)
    shell, roof, sign, opn = [], [], [], []
    brick, brickd, brickl, mortar = "b35a3e", "9c4a33", "c46a4a", "d9c9ae"
    stone = "d8cbb5"
    green, greend = "2f6b4f", "24553f"
    paper, paper2 = "3f6b55", "4c7c63"
    wood, woodd, woodl = "6e4228", "4e2e1a", "8a5a36"

    # piso
    B(shell, "base", -hx - T - 0.2, hx + T + 0.2, -0.05, FY - 0.004, -hz - T - 0.3, hz + T + 0.2, "8f8172", r=0.05)
    B(shell, "bajo_piso", -hx, hx, FY - 0.03, FY - 0.002, -hz, hz, "3a2416", r=0.003, seg=1)
    shell.append(plank_floor("piso", hx, hz, rnd, ("7a4a2a", "8a5634", "6c4126", "94603a"), w=0.2))
    rug(shell, -0.45, 0.05, 3.0, 2.0, rnd)

    # paredes (ladrillo afuera, empapelado verde adentro)
    walls(shell, hx, hz, H, brick, paper)
    for i in range(int(2 * hx / 0.3)):
        x = -hx + 0.15 + i * 0.3
        B(shell, "raya", x - 0.04, x + 0.04, 0.8, H - 0.08, hz - 0.017, hz - 0.012, paper2, r=0.002, seg=1)
    wainscot(shell, hx, hz, 0.78, wood, woodd, panel=woodl, panels_every=0.75)
    crown(shell, hx, hz, H, "e8d6ae")
    # ladrillos sueltos en los costados y el fondo (afuera)
    for f in (Face("+x", hx + T), Face("-x", -hx - T)):
        sparse_blocks(shell, rnd, f, -hz - T, hz + T, 0.0, H, 0.24, 0.085, (brickd, brickl), 0.14)

    # fachada: pilar izquierdo, pilar derecho (hasta la puerta), pared baja con vidriera
    ff = Face("-z", -hz - T)
    B(shell, "pilar_i", -hx - T, -hx + 0.2, 0, H, -hz - T, -hz, mortar, r=0.02, seg=1)
    B(shell, "pilar_d", door[1] + 0.02, hx + T, 0, H, -hz - T, -hz, mortar, r=0.02, seg=1)
    B(shell, "pared_baja", -hx + 0.2, door[0] - 0.12, 0, 0.45, -hz - T, -hz, mortar, r=0.02, seg=1)
    sparse_blocks(shell, rnd, ff, -hx - T, -hx + 0.2, 0.0, H, 0.24, 0.085, (brick, brickd, brickl, brick), 1.0)
    sparse_blocks(shell, rnd, ff, door[1] + 0.02, hx + T, 0.0, H, 0.24, 0.085, (brick, brickd, brickl, brick), 1.0)
    sparse_blocks(shell, rnd, ff, -hx + 0.2, door[0] - 0.12, 0.0, 0.43, 0.24, 0.085, (brick, brickd, brickl, brick), 1.0)
    B(shell, "pared_baja_tapa", -hx - T, door[0] - 0.12, 0.43, 0.5, -hz - T - 0.06, -hz + 0.06, green, r=0.02, seg=2)
    B(shell, "zocalo_piedra", -hx - T - 0.02, door[0] - 0.12, 0, 0.1, -hz - T - 0.04, -hz - T + 0.02, stone, r=0.02, seg=1)
    B(shell, "zocalo_piedra", door[1] + 0.02, hx + T + 0.02, 0, 0.1, -hz - T - 0.04, -hz - T + 0.02, stone, r=0.02, seg=1)
    # marco de la puerta, umbral y puerta abierta hacia afuera
    B(shell, "jamba_i", door[0] - 0.12, door[0], 0, 1.66, -hz - T - 0.03, -hz + 0.02, green, r=0.02)
    B(shell, "jamba_d", door[1], door[1] + 0.1, 0, 1.66, -hz - T - 0.03, -hz + 0.02, green, r=0.02)
    B(shell, "dintel", door[0] - 0.14, door[1] + 0.12, 1.64, 1.76, -hz - T - 0.04, -hz + 0.03, green, r=0.025)
    B(shell, "umbral", door[0] - 0.1, door[1] + 0.12, 0, FY + 0.01, -hz - T - 0.3, -hz, stone, r=0.02)
    B(shell, "felpudo", door[0] + 0.05, door[1] - 0.05, FY + 0.005, FY + 0.03, -hz - T - 0.62, -hz - T - 0.32, "a8703f", r=0.015)
    B(shell, "puerta", door[1] - 0.045, door[1] + 0.005, FY, FY + 1.5, -hz - T - 0.7, -hz - T - 0.04, green, r=0.015)
    for s_ in (-1, 1):
        B(shell, "puerta_vidrio", door[1] - 0.02 + s_ * 0.027 - 0.003, door[1] - 0.02 + s_ * 0.027 + 0.003, FY + 0.75, FY + 1.38,
          -hz - T - 0.6, -hz - T - 0.14, "cfe6ea", r=0.002, seg=1)
        shell.append(US("pomo", door[1] - 0.02 + s_ * 0.04, FY + 0.78, -hz - T - 0.62, 0.025, GOLD))
    # campanita sobre la puerta (adentro)
    bx = (door[0] + door[1]) * 0.5
    shell.append(tube("soporte_campana", [U(bx, 1.66, -hz + 0.02), U(bx, 1.66, -hz + 0.16), U(bx, 1.6, -hz + 0.2)], 0.01, INK))
    shell.append(lathe("campana", U(bx, 1.6, -hz + 0.2), [(0.0, 0.0), (0.03, -0.01), (0.05, -0.06), (0.075, -0.12), (0.08, -0.13), (0.0, -0.13)], GOLD, seg=16))
    shell.append(US("badajo", bx, 1.45, -hz + 0.2, 0.018, "8a6a3a"))
    # cositas en la vidriera (sobre la pared baja)
    gx = -2.2
    shell.append(UC("globo_pie", gx, 0.5, -hz - 0.07, 0.07, 0.03, GOLDD, seg=14))
    shell.append(tube("globo_eje", [U(gx, 0.52, -hz - 0.07), U(gx, 0.78, -hz - 0.07)], 0.01, GOLDD))
    shell.append(US("globo", gx, 0.68, -hz - 0.07, 0.11, "4a8fe0"))
    for k in range(3):
        shell.append(blob("tierra", U(gx + [0.04, -0.05, 0.02][k], 0.68 + [0.05, -0.02, -0.06][k], -hz - 0.12), 0.05, "79b94f", amp=0.3, scale=(1, 0.5, 0.8)))
    shell.append(lathe("jarron", U(-1.3, 0.5, -hz - 0.07), [(0.0, 0.0), (0.06, 0.0), (0.09, 0.08), (0.06, 0.2), (0.04, 0.24), (0.06, 0.28), (0.0, 0.28)], "3f7fc0", seg=16))
    shell.append(torus("jarron_ar", U(-1.3, 0.62, -hz - 0.07), 0.075, 0.012, "fbf1dc", seg=16, mseg=6))
    shell.append(UC("candelabro", -0.5, 0.5, -hz - 0.07, 0.06, 0.03, GOLDD, seg=12))
    shell.append(UC("cand_pie", -0.5, 0.52, -hz - 0.07, 0.015, 0.22, GOLDD, seg=8))
    shell.append(UC("vela", -0.5, 0.74, -hz - 0.07, 0.022, 0.1, "fbf6ee", seg=10))
    shell.append(US("llama", -0.5, 0.87, -hz - 0.07, 0.02, "ffd23a", scale=(1, 1, 1.6), emis=2.0))
    shell.append(UB("reloj_mesa", 0.45, 0.5, -hz - 0.07, 0.2, 0.22, 0.1, r=0.04, col=woodl))
    shell.append(UC("reloj_mesa_c", 0.45, 0.62, -hz - 0.125, 0.07, 0.01, "fbf1dc", rx=90, seg=16))
    shell.append(UB("libros_v", 1.2, 0.5, -hz - 0.07, 0.3, 0.14, 0.12, r=0.015, col="8e2f3a"))
    shell.append(UB("libros_v2", 1.2, 0.64, -hz - 0.07, 0.26, 0.05, 0.13, r=0.012, col="2f4e8a", ry=8))

    # pared del fondo: espejo, cuadro, reloj, estantes sobre el mostrador, apliques Tiffany
    fb = Face("-z", hz - 0.012)
    painting(shell, fb, -0.75, 0.92, 0.72, 0.86, "retrato")
    painting(shell, fb, 0.35, 1.0, 0.56, 0.44, "paisaje", frame="c9a55a")
    fb.disc(shell, "reloj_aro", 0.35, 1.72, 0.0, 0.17, 0.04, woodd, n=20)
    fb.disc(shell, "reloj_cara", 0.35, 1.72, 0.04, 0.135, 0.01, "fbf1dc", n=20)
    fb.box(shell, "aguja", 0.35, 1.71, 0.05, 0.018, 0.11, 0.008, INK, r=0.003, seg=1)
    fb.box(shell, "aguja", 0.39, 1.71, 0.05, 0.09, 0.016, 0.008, INK, r=0.003, seg=1)
    for (x, ylamp) in ((-1.55, 1.42), (0.95, 1.42)):
        x0, y0, z0 = fb.P(x, ylamp, 0.0)
        shell.append(UB("aplique", x, ylamp - 0.12, z0 - 0.02, 0.1, 0.16, 0.04, r=0.02, col=GOLDD))
        shell.append(tube("aplique_brazo", [U(x, ylamp - 0.04, z0 - 0.03), U(x, ylamp + 0.02, z0 - 0.16), U(x, ylamp, z0 - 0.2)], 0.012, GOLDD))
        tiffany(shell, x, ylamp, z0 - 0.2, s=0.75)
    for y in (1.18, 1.52):
        B(shell, "estante", 1.35, hx - 0.02, y, y + 0.04, hz - 0.3, hz - 0.012, woodl, r=0.01, seg=1)
        for x in (1.45, hx - 0.1):
            B(shell, "mensula", x - 0.02, x + 0.02, y - 0.12, y, hz - 0.2, hz - 0.012, woodd, r=0.008, seg=1)
    jar_cols = ["8fd8c8", "e8a33a", "c0392b", "3f8a5c", "9a6ac8"]
    for k in range(6):
        x = 1.5 + k * 0.24
        if k % 2 == 0:
            shell.append(UC("frasco", x, 1.22, hz - 0.15, 0.055, 0.15 + 0.03 * (k % 3), jar_cols[k % 5], seg=12))
            shell.append(UC("tapa_frasco", x, 1.37 + 0.03 * (k % 3), hz - 0.15, 0.045, 0.03, GOLDD, seg=12))
        else:
            for j in range(3):
                shell.append(UB("libro", x - 0.06 + j * 0.05, 1.22, hz - 0.15, 0.045, 0.2 - 0.02 * j, 0.15, r=0.008, seg=1,
                                col=["2f4e8a", "8e2f3a", "3f6b55"][j]))
    shell.append(lathe("tetera", U(1.65, 1.56, hz - 0.15), [(0.0, 0.0), (0.07, 0.0), (0.09, 0.05), (0.07, 0.11), (0.03, 0.13), (0.0, 0.14)], "e8e0d0", seg=16))
    shell.append(US("busto_mini", 2.3, 1.68, hz - 0.15, 0.06, "e8e0d0"))
    shell.append(UB("busto_mini_pie", 2.3, 1.56, hz - 0.15, 0.1, 0.07, 0.1, r=0.015, col="e8e0d0"))
    shell.append(UC("gramofono_caja", 2.75, 1.56, hz - 0.16, 0.1, 0.08, woodl, seg=4))
    shell.append(UC("gramofono_bocina", 2.72, 1.66, hz - 0.2, 0.02, 0.24, GOLD, r2=0.11, rx=40, seg=16))

    # mostrador contra el fondo a la derecha, con caja registradora de bronce
    cx0, cx1, cz0, cz1 = 1.3, hx, hz - 0.56, hz - 0.012
    B(shell, "mostrador", cx0, cx1, FY, FY + 0.8, cz0, cz1, wood, r=0.02)
    B(shell, "mostrador_tapa", cx0 - 0.05, cx1, FY + 0.8, FY + 0.86, cz0 - 0.06, cz1, woodd, r=0.025)
    B(shell, "mostrador_pie", cx0 - 0.02, cx1, FY, FY + 0.08, cz0 - 0.02, cz1, woodd, r=0.015)
    fm = Face("-z", cz0)
    for k in range(3):
        u = cx0 + 0.29 + k * 0.56
        fm.box(shell, "panel_m", u, FY + 0.16, 0.0, 0.44, 0.52, 0.025, woodl, r=0.012, seg=1)
        fm.box(shell, "panel_m2", u, FY + 0.23, 0.02, 0.32, 0.38, 0.012, wood, r=0.01, seg=1)
    fs = Face("-x", cx0)
    fs.box(shell, "panel_lado", -(cz0 + cz1) * 0.5, FY + 0.16, 0.0, 0.4, 0.52, 0.02, woodl, r=0.012, seg=1)
    shell.append(tube("barra_pies", [U(cx0 - 0.02, FY + 0.12, cz0 - 0.1), U(cx1 - 0.02, FY + 0.12, cz0 - 0.1)], 0.018, GOLD))
    cash_register(shell, 2.4, FY + 0.86, hz - 0.3)
    # lampara de banquero, libro mayor y timbre
    shell.append(UC("banq_base", 1.6, FY + 0.86, hz - 0.25, 0.07, 0.03, GOLDD, seg=14))
    shell.append(UC("banq_pie", 1.6, FY + 0.89, hz - 0.25, 0.014, 0.2, GOLDD, seg=8))
    shell.append(UB("banq_pantalla", 1.6, FY + 1.06, hz - 0.28, 0.26, 0.06, 0.12, r=0.04, col="2f8a5c", rx=-12))
    shell.append(UB("libro_mayor", 1.95, FY + 0.86, hz - 0.33, 0.3, 0.04, 0.22, r=0.012, col="8e2f3a", ry=-10))
    shell.append(UB("libro_hojas", 1.95, FY + 0.9, hz - 0.33, 0.28, 0.008, 0.2, r=0.004, seg=1, col="fbf1dc", ry=-10))
    shell.append(UC("timbre", 2.88, FY + 0.86, hz - 0.42, 0.045, 0.02, "6d747e", seg=12))
    shell.append(US("timbre_c", 2.88, FY + 0.89, hz - 0.42, 0.04, GOLD, scale=(1, 1, 0.7)))

    # lamparas Tiffany colgantes
    for (x, z) in ((-1.15, -0.15), (0.35, 0.35)):
        shell.append(tube("cadena", [U(x, H, z), U(x, 1.62, z)], 0.008, INK))
        tiffany(shell, x, 1.62, z, s=1.0)

    # taller atras a la izquierda
    workbench(shell, hx, hz, board="b98a55")

    # ---------------- techo y fachada alta (pieza roof)
    # vidriera: parantes, travesano, vidrios con brillos y vitrales arriba
    vx0, vx1 = -hx + 0.2, door[0] - 0.12
    zf = -hz - T * 0.5
    n = 4
    for i in range(n + 1):
        x = vx0 + (vx1 - vx0) * i / n
        B(roof, "parante", x - 0.04, x + 0.04, 0.5, 1.64, zf - 0.05, zf + 0.05, green, r=0.015)
    B(roof, "travesano_v", vx0, vx1, 1.3, 1.36, zf - 0.05, zf + 0.05, green, r=0.015)
    B(roof, "cabezal", -hx - T, door[1] + 0.12, 1.64, 1.76, -hz - T - 0.04, -hz + 0.02, green, r=0.02)
    vit = ["e8a33a", "3f8a5c", "c0392b", "e8a33a", "4a6fd0"]
    for i in range(n):
        a = vx0 + (vx1 - vx0) * i / n + 0.04
        b = vx0 + (vx1 - vx0) * (i + 1) / n - 0.04
        B(roof, "vidrio", a, b, 0.5, 1.3, zf - 0.006, zf + 0.006, "cfe6ea", r=0.003, seg=1)
        for k in range(2):
            gx = a + (b - a) * (0.25 + 0.3 * k)
            roof.append(UB("brillo", gx, 0.75 + 0.1 * k, zf - 0.012, 0.05, 0.36 - 0.12 * k, 0.006, rz=-35, r=0.002, seg=1, col="f6fdff"))
        for k in range(3):
            c = a + (b - a) * (k + 0.5) / 3
            B(roof, "vitral", c - (b - a) / 6 + 0.01, c + (b - a) / 6 - 0.01, 1.37, 1.63, zf - 0.008, zf + 0.008, vit[(i + k) % 5], r=0.004, seg=1)
    # ladrillo arriba de la puerta y fachada falsa (con cartelera y frontis curvo)
    B(roof, "fachada_alta", -hx - T, hx + T, 1.76, H + 0.75, -hz - T, -hz, mortar, r=0.02, seg=1)
    B(roof, "fachada_ladrillo", -hx - T, hx + T, 1.76, H + 0.75, -hz - T - 0.01, -hz - T + 0.02, brick, r=0.01, seg=1)
    sparse_blocks(roof, rnd, ff, -hx - T, hx + T, 1.76, H + 0.7, 0.24, 0.085, (brickd, brickl), 0.22, dep=0.03)
    B(roof, "cartelera", -hx + 0.3, hx - 0.3, 2.02, 2.46, -hz - T - 0.05, -hz - T + 0.01, green, r=0.03)
    B(roof, "cartelera_borde", -hx + 0.24, hx - 0.24, 1.98, 2.5, -hz - T - 0.035, -hz - T + 0.01, GOLDD, r=0.03)
    roof.append(text("antiguedades", "ANTIGUEDADES", 0, 2.24, -hz - T - 0.075, 0.27, GOLD, depth=0.014))
    B(roof, "coronamiento", -hx - T - 0.06, hx + T + 0.06, H + 0.75, H + 0.83, -hz - T - 0.08, -hz + 0.06, stone, r=0.025)
    ff2 = Face("-z", -hz - T)
    half = lambda R_, sy: [(R_ * math.cos(math.pi * k / 16), H + 0.79 + R_ * sy * math.sin(math.pi * k / 16)) for k in range(17)]
    ff2.shape(roof, "frontis_borde", half(0.78, 0.62), -0.03, T + 0.1, stone, r=0.02)
    ff2.shape(roof, "frontis", half(0.68, 0.6), -0.05, 0.03, brick)
    ff2.disc(roof, "frontis_c", 0, H + 0.98, -0.08, 0.17, 0.03, GOLD, n=16)
    ff2.word(roof, "anio", "1897", 0, H + 0.98, -0.11, 0.1, INK, depth=0.006)
    # toldo a rayas sobre la vidriera
    ax0, ax1 = -hx - 0.06, door[0] - 0.1
    nst = int((ax1 - ax0) / 0.26)
    sw = (ax1 - ax0) / nst
    zt0, yt0, zt1, yt1 = -hz - T, 1.92, -hz - T - 0.78, 1.6
    L = math.hypot(zt1 - zt0, yt1 - yt0)
    ang = math.degrees(math.atan2(yt0 - yt1, zt0 - zt1))
    for i in range(nst):
        x = ax0 + (i + 0.5) * sw
        col = "3f8a5c" if i % 2 == 0 else "f3ead2"
        roof.append(UB("toldo", x, (yt0 + yt1) * 0.5 - 0.02, (zt0 + zt1) * 0.5, sw + 0.003, 0.04, L, rx=-ang, r=0.012, seg=1, col=col))
        roof.append(UB("faldon_t", x, yt1 - 0.17, zt1 - 0.01, sw + 0.003, 0.17, 0.025, r=0.008, seg=1, col=col))
        Face("-z", zt1 - 0.022).shape(roof, "feston", [(x + sw * 0.5 * math.cos(-math.pi * k / 8), yt1 - 0.17 + sw * 0.45 * math.sin(-math.pi * k / 8))
                                                       for k in range(9)], 0.0, 0.025, col)
    for x in (ax0 + 0.08, ax1 - 0.08):
        roof.append(tube("brazo_toldo", [U(x, 1.2, -hz - T), U(x, yt1 - 0.02, zt1 + 0.05)], 0.012, INK))
    B(roof, "toldo_cano", ax0, ax1, yt0 - 0.04, yt0 + 0.06, -hz - T - 0.07, -hz - T, greend, r=0.03)
    # techo a dos aguas (cumbrera a lo largo de X) de pizarra verde gris, con hastiales de ladrillo y chimenea
    pitch = 30.0
    zr = 0.15
    yr = H + (zr + hz + T) * math.tan(math.radians(pitch))
    over = 0.18
    zb = hz + T + 0.3
    yb = yr - (zb - zr) * math.tan(math.radians(pitch))
    slope_rows(roof, "teja", -hx - T - over, hx + T + over, (-hz - T, H), (zr, yr), 9, ("5f6f6a", "6f817a", "566661"), th=0.07, rnd=rnd)
    slope_rows(roof, "teja", -hx - T - over, hx + T + over, (zr, yr), (zb, yb), 9, ("566661", "6f817a", "5f6f6a"), th=0.07, rnd=rnd)
    B(roof, "cumbrera", -hx - T - over - 0.05, hx + T + over + 0.05, yr - 0.04, yr + 0.07, zr - 0.09, zr + 0.09, "4a5753", r=0.04)
    for sx in (-1, 1):
        xw = sx * (hx + T * 0.5)
        roof.append(hull("hastial", [(xw - T / 2, H - 0.01, -hz - T), (xw + T / 2, H - 0.01, -hz - T), (xw - T / 2, H - 0.01, hz + T), (xw + T / 2, H - 0.01, hz + T),
                                     (xw - T / 2, yr - 0.05, zr), (xw + T / 2, yr - 0.05, zr)], brick))
    B(roof, "chimenea", 1.5, 1.95, yr - 0.6, yr + 0.55, zr + 0.35, zr + 0.75, brick, r=0.025)
    B(roof, "chimenea_tapa", 1.45, 2.0, yr + 0.55, yr + 0.63, zr + 0.3, zr + 0.8, stone, r=0.02)
    for k in range(2):
        roof.append(UC("caño", 1.63 + k * 0.18, yr + 0.63, zr + 0.55, 0.05, 0.12, "c4683a", seg=12))

    # ---------------- cartel: tabla tallada con letras doradas colgada de un poste de hierro con farol
    cx, cz = -hx + 0.95, -hz - T - 0.62
    px = cx - 0.85
    sign.append(UC("poste_base", px, 0, cz, 0.08, 0.12, INK, r2=0.06, seg=12))
    sign.append(UC("poste", px, 0.1, cz, 0.035, 1.55, INK, seg=10))
    sign.append(tube("brazo", [U(px, 1.4, cz), U(cx + 0.72, 1.4, cz)], 0.022, INK))
    sign.append(tube("voluta", [U(px, 1.15, cz), U(px + 0.18, 1.18, cz), U(px + 0.3, 1.3, cz), U(px + 0.36, 1.4, cz)], 0.014, INK))
    sign.append(torus("rulo", U(px + 0.2, 1.3, cz), 0.07, 0.012, INK, seg=14, mseg=5, rot=(90, 0, 0)))
    sign.append(UC("farol_base", px, 1.65, cz, 0.06, 0.04, INK, seg=8))
    sign.append(UC("farol", px, 1.69, cz, 0.07, 0.18, "ffe2a0", r2=0.085, seg=8, emis=1.5))
    sign.append(UC("farol_techo", px, 1.87, cz, 0.11, 0.08, INK, r2=0.02, seg=8))
    sign.append(US("farol_punta", px, 1.97, cz, 0.025, INK))
    for x in (cx - 0.55, cx + 0.55):
        sign.append(tube("cadena", [U(x, 1.4, cz), U(x, 1.2, cz)], 0.008, "9aa1aa"))
    sign.append(UB("tabla_borde", cx, 0.76, cz, 1.36, 0.46, 0.06, r=0.06, col=woodd))
    sign.append(UB("tabla", cx, 0.79, cz - 0.012, 1.26, 0.4, 0.06, r=0.06, col=woodl))
    sign.append(text("letras", "RAREZAS", cx, 0.99, cz - 0.055, 0.25, GOLD, depth=0.016))
    for s_ in (-1, 1):
        sign.append(US("adorno", cx + s_ * 0.56, 0.99, cz - 0.045, 0.03, GOLD))
        sign.append(UB("filete", cx + s_ * 0.35, 0.86, cz - 0.045, 0.28, 0.018, 0.01, r=0.004, seg=1, col=GOLD))
    sign.append(text("sub", "antiguedades", cx, 0.86, cz - 0.05, 0.09, "fbf1dc", depth=0.006))

    # ---------------- OPEN/CLOSED colgado de un brazo en la jamba izquierda
    pv = open_sign(opn, shell, door[0] - 0.06, 1.22, -hz - T - 0.36, -hz - T - 0.03)
    register_fixed("shack2", [("taller", bench_rect(hx, hz)), ("mostrador", (1.25, hx, hz - 0.62, hz))])
    return [("shell", shell, (0, 0, 0)), ("roof", roof, (0, 0, 0)), ("sign", sign, (0, 0, 0)), ("open", opn, pv)]


FIXED = {}


def register_fixed(stage, items):
    FIXED[stage] = items


# ================================================================ etapa 3: galeria (8 x 5.5 m)
def stage3():
    """Galeria: dos pisos con columnas blancas, ventanales, parquet en espiga, paredes verde salvia con cuadros y
    spots, escalera contra la pared izquierda que sube a un entrepiso (balcon) a lo largo del fondo."""
    rnd = random.Random(33)
    hx, hz, H = 4.0, 2.75, 3.0
    door = (hx - 0.95, hx - 0.25)
    shell, roof, sign, opn = [], [], [], []
    stucco, stuccod = "efe6d2", "e2d6bd"
    white = "fbf6ee"
    sage, saged = "a3b896", "8fa682"
    YM = 1.78          # entrepiso
    B(shell, "base", -hx - T - 0.2, hx + T + 0.2, -0.05, FY - 0.004, -hz - T - 0.35, hz + T + 0.2, "d9d2c4", r=0.05)
    B(shell, "bajo_piso", -hx, hx, FY - 0.03, FY - 0.002, -hz, hz, "6c4126", r=0.003, seg=1)
    shell.append(herringbone("parquet", hx, hz, rnd, ("c98d55", "d39a62", "c48850"), ("a8703f", "b07a48", "9c6838"), w=0.15, k=4))
    walls(shell, hx, hz, H, stucco, sage)
    wainscot(shell, hx, hz, 0.55, white, white, base=white)
    crown(shell, hx, hz, H, white, d=0.08, h=0.12)
    # banda del entrepiso afuera (dos pisos) y ventanales
    for f, L in ((Face("+x", hx + T), hz + T), (Face("-x", -hx - T), hz + T), (Face("+z", hz + T), hx + T)):
        f.box(shell, "banda", 0, YM, 0, 2 * L, 0.14, 0.05, white, r=0.02, seg=1)
        f.box(shell, "zocalo_ext", 0, 0, 0, 2 * L, 0.3, 0.03, stuccod, r=0.01, seg=1)
        f.box(shell, "cornisa_ext", 0, H - 0.14, 0, 2 * L + 0.05, 0.14, 0.06, white, r=0.02, seg=1)
    fr = Face("+x", hx + T)
    for u in (-1.6, 0.0, 1.6):
        window(shell, fr, u, 0.55, 0.62, 1.0, arch=True)
        window(shell, fr, u, 2.05, 0.5, 0.6)
    fl = Face("-x", -hx - T)
    for u in (-1.6, 0.0, 1.6):
        window(shell, fl, u, 2.05, 0.5, 0.6)
        window(shell, fl, u, 0.55, 0.62, 1.0, arch=True)
    fri = Face("-x", hx - 0.012)     # ventanales vistos desde adentro (pared derecha)
    for u in (1.6, 0.0, -1.6):
        window(shell, fri, u, 0.6, 0.62, 1.0, arch=True, sill=False)
        window(shell, fri, u, 2.05, 0.5, 0.6, sill=False)
    fli = Face("+x", -hx + 0.012)    # pared izquierda adentro: solo arriba (abajo va la escalera)
    for u in (-1.2, 0.4):
        window(shell, fli, u, 2.12, 0.5, 0.58, sill=False)

    # frente: columnas blancas y baranda baja de balaustres
    zf = -hz - T * 0.5
    B(shell, "estilobato", -hx - T - 0.05, hx + T + 0.05, 0, 0.12, -hz - T - 0.12, -hz + 0.02, stuccod, r=0.02)
    cols_x = [-hx - T * 0.4, -1.4, 1.2, door[0] - 0.15, hx + T * 0.4]
    for x in cols_x:
        column(shell, x, zf, H, 0.14, white, y0=0.12)
    segs = [(cols_x[0] + 0.2, cols_x[1] - 0.2), (cols_x[1] + 0.2, cols_x[2] - 0.2), (cols_x[2] + 0.2, cols_x[3] - 0.2)]
    for (a, b) in segs:
        B(shell, "baranda_base", a, b, 0.12, 0.2, -hz - T + 0.01, -hz - 0.01, white, r=0.02)
        B(shell, "baranda_pasamanos", a - 0.02, b + 0.02, 0.42, 0.5, -hz - T - 0.01, -hz + 0.01, white, r=0.025)
        nb = int((b - a) / 0.17)
        for i in range(nb):
            x = a + (b - a) * (i + 0.5) / nb
            shell.append(lathe("balaustre", U(x, 0.2, zf), [(0.035, 0.0), (0.05, 0.03), (0.03, 0.08), (0.055, 0.14), (0.045, 0.19), (0.025, 0.21), (0.035, 0.22)], white, seg=10))
    # puerta vidriada abierta hacia afuera (bisagra a la derecha)
    B(shell, "umbral", door[0] - 0.05, door[1] + 0.05, 0, FY + 0.01, -hz - T - 0.4, -hz, "d9d2c4", r=0.02)
    B(shell, "puerta", door[1] - 0.04, door[1] + 0.01, FY, FY + 1.9, -hz - T - 0.72, -hz - T - 0.05, INK, r=0.012)
    for s_ in (-1, 1):
        B(shell, "puerta_vidrio", door[1] - 0.015 + s_ * 0.028 - 0.003, door[1] - 0.015 + s_ * 0.028 + 0.003, FY + 0.1, FY + 1.8,
          -hz - T - 0.65, -hz - T - 0.12, "cfe6ea", r=0.002, seg=1)
    B(shell, "manija", door[1] - 0.07, door[1] + 0.04, FY + 0.6, FY + 1.2, -hz - T - 0.66, -hz - T - 0.64, GOLD, r=0.008, seg=1)

    # escalera contra la pared izquierda: sube hacia el fondo hasta el entrepiso
    sx0, sx1 = -hx, -hx + 0.6
    zs0, zs1 = -hz + 0.5, hz - 1.0
    nst = 10
    run = (zs1 - zs0) / nst
    rise = (YM - FY) / nst
    for k in range(nst):
        z0 = zs0 + k * run
        top = FY + (k + 1) * rise
        B(shell, "escalon", sx0 + 0.012, sx1, FY - 0.002, top - 0.03, z0, z0 + run, "f1ead8" if k % 2 else "e9e0cc", r=0.01, seg=1)
        B(shell, "huella", sx0 + 0.012, sx1 + 0.02, top - 0.035, top, z0 - 0.025, z0 + run + 0.005, "a8703f", r=0.012, seg=1)
    fst = Face("+x", sx1)
    # zanca blanca y baranda
    for k in range(0, nst, 1):
        z = zs0 + (k + 0.5) * run
        y = FY + (k + 1) * rise
        shell.append(UC("balaustre_e", sx1 - 0.05, y, z, 0.016, 0.52, white, seg=8))
    shell.append(UC("pilaron", sx1 - 0.05, FY, zs0 + 0.15, 0.05, 0.75, "a8703f", seg=10))
    shell.append(US("pilaron_bola", sx1 - 0.05, FY + 0.8, zs0 + 0.15, 0.06, "a8703f"))
    shell.append(tube("pasamanos_e", [U(sx1 - 0.05, FY + 0.76, zs0 + 0.15), U(sx1 - 0.05, YM + 0.55, zs1)], 0.025, "8a5a36"))
    # entrepiso: descanso sobre la escalera + balcon a lo largo del fondo
    ledge_x1 = 0.6
    B(shell, "descanso", -hx + 0.012, sx1, YM - 0.14, YM, zs1, hz - 0.012, "e9e0cc", r=0.015)
    B(shell, "balcon", sx1, ledge_x1, YM - 0.14, YM, hz - 0.55, hz - 0.012, "e9e0cc", r=0.015)
    B(shell, "balcon_piso", -hx + 0.012, ledge_x1, YM - 0.01, YM + 0.01, hz - 0.55, hz - 0.012, "a8703f", r=0.006, seg=1)
    B(shell, "descanso_piso", -hx + 0.012, sx1, YM - 0.01, YM + 0.01, zs1, hz - 0.55, "a8703f", r=0.006, seg=1)
    B(shell, "balcon_moldura", sx1, ledge_x1 + 0.03, YM - 0.18, YM - 0.1, hz - 0.58, hz - 0.012, white, r=0.02)
    for x in (-2.6, -1.4, -0.2):
        shell.append(hull("mensula", [(x - 0.04, YM - 0.14, hz - 0.012), (x + 0.04, YM - 0.14, hz - 0.012), (x - 0.04, YM - 0.14, hz - 0.45), (x + 0.04, YM - 0.14, hz - 0.45),
                                      (x - 0.04, YM - 0.6, hz - 0.012), (x + 0.04, YM - 0.6, hz - 0.012)], white, r=0.01))
    # baranda del balcon
    zrail = hz - 0.53
    B(shell, "baranda_bal", sx1, ledge_x1, YM + 0.5, YM + 0.56, zrail - 0.03, zrail + 0.03, "8a5a36", r=0.02)
    B(shell, "baranda_bal_l", ledge_x1 - 0.03, ledge_x1 + 0.03, YM + 0.5, YM + 0.56, zrail, hz - 0.012, "8a5a36", r=0.02)
    B(shell, "baranda_des", sx1 - 0.03, sx1 + 0.03, YM + 0.5, YM + 0.56, zs1 + 0.25, zrail, "8a5a36", r=0.02)
    xb = sx1 + 0.12
    while xb < ledge_x1 - 0.05:
        shell.append(UC("balaustre_b", xb, YM, zrail, 0.016, 0.5, white, seg=8))
        xb += 0.16
    for z in (zrail + 0.16, zrail + 0.32, zrail + 0.48):
        shell.append(UC("balaustre_b", ledge_x1, YM, z, 0.016, 0.5, white, seg=8))
    # en el entrepiso: busto y planta
    shell.append(UB("pedestal_b", -0.15, YM, hz - 0.28, 0.24, 0.42, 0.24, r=0.02, col=white))
    shell.append(lathe("busto", U(-0.15, YM + 0.42, hz - 0.28), [(0.0, 0.0), (0.1, 0.0), (0.12, 0.08), (0.06, 0.13), (0.04, 0.16), (0.0, 0.16)], "e8e0d0", seg=16))
    shell.append(US("busto_cabeza", -0.15, YM + 0.65, hz - 0.28, 0.075, "e8e0d0", scale=(0.9, 0.95, 1.1)))
    shell.append(UC("maceta", -1.9, YM, hz - 0.3, 0.13, 0.22, "c9773f", r2=0.1, seg=14))
    for k in range(5):
        a = k * 72
        shell.append(blob("hoja", U(-1.9 + 0.1 * math.cos(math.radians(a)), YM + 0.38, hz - 0.3 + 0.1 * math.sin(math.radians(a))), 0.11, "5c9a4a", amp=0.3, scale=(1, 1, 1.3)))

    # cuadros con spots en el fondo (abajo a la derecha del balcon y arriba sobre el balcon)
    fb = Face("-z", hz - 0.012)
    painting(shell, fb, 1.55, 0.6, 1.05, 1.2, "paisaje")
    painting(shell, fb, 2.95, 0.75, 0.8, 0.95, "retrato", frame=INK)
    painting(shell, fb, 2.25, 2.0, 1.4, 0.65, "abstracto", frame=white)
    painting(shell, fb, -3.0, 2.08, 0.62, 0.6, "flores")
    painting(shell, fb, -1.05, 2.1, 0.8, 0.56, "noche", frame=INK)
    painting(shell, fb, -2.2, 0.75, 0.5, 0.4, "mar", frame="c9a55a")
    painting(shell, fb, -0.5, 0.65, 0.9, 0.7, "flores", frame=GOLD)
    # riel de spots
    B(shell, "riel_spots", 0.9, 3.7, 2.82, 2.86, hz - 0.3, hz - 0.26, INK, r=0.01, seg=1)
    for x in (1.2, 1.9, 2.95):
        shell.append(UC("spot_v", x, 2.7, hz - 0.28, 0.01, 0.12, INK, seg=6))
        shell.append(UC("spot", x, 2.72, hz - 0.36, 0.05, 0.16, INK, r2=0.065, rx=-60, seg=12))
        shell.append(US("spot_luz", x, 2.66, hz - 0.45, 0.04, "fff1b0", emis=2.0))
    for x in (-3.0, -1.05):
        shell.append(tube("spot_brazo", [U(x, 2.86, hz - 0.012), U(x, 2.86, hz - 0.25)], 0.012, INK))
        shell.append(UC("spot", x, 2.8, hz - 0.3, 0.045, 0.14, INK, r2=0.06, rx=-60, seg=12))
    # taller (atras a la izquierda, bajo el descanso)
    workbench(shell, hx, hz, top="c98d55", leg="fbf6ee", board="e9e0cc", lamp=INK)

    # ---------------- techo: fachada alta (2do piso), mansarda de pizarra con buhardillas y claraboya
    B(roof, "entablamento", -hx - T, hx + T, YM - 0.1, YM + 0.22, -hz - T - 0.06, -hz, white, r=0.02)
    roof.append(text("galeria", "GALERIA DE ARTE", (cols_x[1] + cols_x[2]) * 0.5, YM + 0.06, -hz - T - 0.075, 0.18, GOLDD, depth=0.01))
    B(roof, "fachada_2", -hx - T, hx + T, YM + 0.22, H, -hz - T, -hz, stucco, r=0.02, seg=1)
    ff = Face("-z", -hz - T)
    for u in (-2.6, -0.1, 2.1):
        window(roof, ff, u, 2.18, 0.62, 0.62, sill=True)
    B(roof, "cornisa_frente", -hx - T - 0.05, hx + T + 0.05, H - 0.14, H, -hz - T - 0.08, -hz, white, r=0.02)
    # mansarda (tronco de piramide) + techito de zinc + claraboya
    a0, b0 = hx + T + 0.18, hz + T + 0.18
    a1, b1 = hx - 0.75, hz - 0.75
    y0, y1 = H + 0.1, H + 1.15
    B(roof, "cornisa_techo", -a0 - 0.04, a0 + 0.04, H - 0.02, H + 0.12, -b0 - 0.04, b0 + 0.04, white, r=0.04)
    roof.append(hull("mansarda", [(sx * a0, y0, sz * b0) for sx in (-1, 1) for sz in (-1, 1)] +
                     [(sx * a1, y1, sz * b1) for sx in (-1, 1) for sz in (-1, 1)], "5b6b80", r=0.04))
    # hiladas de pizarra (lineas) en el faldon de adelante
    for k in range(1, 5):
        t = k / 5.0
        yy = y0 + (y1 - y0) * t
        zz = -(b0 + (b1 - b0) * t)
        aa = a0 + (a1 - a0) * t
        B(roof, "hilada", -aa + 0.1, aa - 0.1, yy - 0.02, yy + 0.02, zz - 0.02, zz + 0.02, "4d5c70", r=0.012, seg=1)
    B(roof, "techo_plano", -a1, a1, y1 - 0.02, y1 + 0.06, -b1, b1, "8a96a5", r=0.03)
    roof.append(hull("claraboya", [(sx * 1.3, y1 + 0.06, sz * 0.7) for sx in (-1, 1) for sz in (-1, 1)] + [(sx * 0.8, y1 + 0.5, 0) for sx in (-1, 1)],
                     "bfe0ea", r=0.02))
    for sx in (-1, 1):
        roof.append(tube("claraboya_c", [U(sx * 1.3, y1 + 0.07, -0.7), U(sx * 0.8, y1 + 0.5, 0), U(sx * 1.3, y1 + 0.07, 0.7)], 0.02, white))
    roof.append(tube("claraboya_r", [U(-0.8, y1 + 0.5, 0), U(0.8, y1 + 0.5, 0)], 0.025, white))
    # buhardillas en el faldon de adelante
    for x in (-2.2, 0.0, 2.2):
        zb_ = -b0 + 0.35
        B(roof, "buhardilla", x - 0.38, x + 0.38, y0 + 0.05, y0 + 0.65, zb_ - 0.02, zb_ + 0.55, stucco, r=0.02)
        window(roof, Face("-z", zb_ - 0.02), x, y0 + 0.13, 0.42, 0.42, sill=False)
        roof.append(hull("buh_techo", [(x - 0.46, y0 + 0.63, zb_ - 0.08), (x + 0.46, y0 + 0.63, zb_ - 0.08), (x - 0.46, y0 + 0.63, zb_ + 0.6),
                                       (x + 0.46, y0 + 0.63, zb_ + 0.6), (x, y0 + 0.92, zb_ - 0.08), (x, y0 + 0.92, zb_ + 0.6)], "4d5c70", r=0.02))
    for sx in (-1, 1):
        B(roof, "chimenea", sx * 2.8 - 0.2, sx * 2.8 + 0.2, y1 - 0.3, y1 + 0.5, 0.6, 1.0, stucco, r=0.02)
        B(roof, "chimenea_t", sx * 2.8 - 0.25, sx * 2.8 + 0.25, y1 + 0.5, y1 + 0.57, 0.55, 1.05, white, r=0.02)

    # ---------------- cartel: atril de pintor con un cuadro negro y letras doradas
    cx, cz = -hx + 1.0, -hz - T - 0.75
    for (dx, dz) in ((-0.42, 0.0), (0.42, 0.0)):
        sign.append(tube("pata_atril", [U(cx + dx, 0.0, cz + 0.12), U(cx + dx * 0.55, 1.45, cz - 0.02)], 0.022, "a8703f"))
    sign.append(tube("pata_atras", [U(cx, 0.0, cz + 0.55), U(cx, 1.4, cz + 0.05)], 0.02, "a8703f"))
    sign.append(UB("repisa", cx, 0.5, cz - 0.03, 1.1, 0.05, 0.12, r=0.015, col="8a5a36"))
    sign.append(UB("lienzo_marco", cx, 0.55, cz, 1.3, 0.62, 0.06, r=0.03, col=GOLD))
    sign.append(UB("lienzo", cx, 0.6, cz - 0.02, 1.18, 0.52, 0.05, r=0.02, col=INK))
    sign.append(text("letras", "RAREZAS", cx, 0.92, cz - 0.06, 0.26, GOLD, depth=0.014))
    sign.append(text("sub", "galeria", cx, 0.72, cz - 0.055, 0.1, "fbf6ee", depth=0.006))
    for k, c in enumerate(("e0594a", "4a8fe0", "ffd23a")):
        sign.append(US("pintura", cx + 0.38 + k * 0.08, 0.56, cz - 0.07, 0.025, c, scale=(1, 1, 0.5)))
    sign.append(tube("pincel_c", [U(cx - 0.45, 0.56, cz - 0.08), U(cx - 0.15, 0.58, cz - 0.08)], 0.01, "c98d55"))

    pv = open_sign(opn, shell, door[0] - 0.15, 1.3, -hz - T - 0.42, -hz - T - 0.1)
    register_fixed("shack3", [("taller", bench_rect(hx, hz)), ("escalera", (-hx, -hx + 0.62, zs0 - 0.05, hz))])
    return [("shell", shell, (0, 0, 0)), ("roof", roof, (0, 0, 0)), ("sign", sign, (0, 0, 0)), ("open", opn, pv)]


# ================================================================ etapa 4: museo (11 x 7 m)
def stage4():
    """Museo: piedra clara, escalinata en la puerta, fronton con relieve, estandartes, detalles dorados, boleteria
    afuera a la derecha de la puerta y postes con cordon rojo."""
    rnd = random.Random(44)
    hx, hz, H = 5.5, 3.5, 3.0
    door = (hx - 0.95, hx - 0.25)
    shell, roof, sign, opn = [], [], [], []
    stone, stoned, stonel = "e9e0cf", "d6cab4", "f3ece0"
    wallin, wallind = "8e3b33", "7a3029"
    B(shell, "base", -hx - T - 0.3, hx + T + 0.3, -0.05, FY - 0.004, -hz - T - 0.3, hz + T + 0.3, "c9bfae", r=0.05)
    B(shell, "bajo_piso", -hx, hx, FY - 0.03, FY - 0.002, -hz, hz, "8a7c66", r=0.003, seg=1)
    # piso de losas claras con guarda y estrella central
    polys = []
    bw = 0.3
    for (x0, x1, z0, z1) in ((-hx, hx, -hz, -hz + bw), (-hx, hx, hz - bw, hz), (-hx, -hx + bw, -hz + bw, hz - bw), (hx - bw, hx, -hz + bw, hz - bw)):
        polys.append((rect(x0, x1, z0, z1, 0.006), "b5a68c"))
    rowh = 0.6
    nrow = int(round((2 * hz - 2 * bw) / rowh))
    rowh = (2 * hz - 2 * bw) / nrow
    for i in range(nrow):
        z0 = -hz + bw + i * rowh
        x = -hx + bw - (0.6 if i % 2 else 0.0)
        while x < hx - bw:
            a, b = max(x, -hx + bw), min(x + 1.2, hx - bw)
            if b - a > 0.05:
                polys.append((rect(a, b, z0, z0 + rowh, 0.008), ("ebe3d3", "e0d6c3", "e6ddcb")[rnd.randrange(3)]))
            x += 1.2
    shell.append(flat("losas", polys, FY))
    star = []
    for k in range(8):
        a0 = math.radians(k * 45)
        a1 = math.radians(k * 45 + 22.5)
        a2 = math.radians(k * 45 - 22.5)
        tip = (1.05 * math.cos(a0), 1.05 * math.sin(a0))
        star.append(([(0, 0), (0.42 * math.cos(a2), 0.42 * math.sin(a2)), tip], "c49a3a"))
        star.append(([(0, 0), tip, (0.42 * math.cos(a1), 0.42 * math.sin(a1))], "7a6a52"))
    shell.append(flat("estrella", star, FY + 0.003))
    shell.append(UC("estrella_c", 0, FY, 0, 0.2, 0.006, "c0392b", seg=20))
    shell.append(torus("estrella_aro", U(0, FY + 0.002, 0), 1.18, 0.022, "b5a68c", seg=40, mseg=4, scale=(1, 1, 0.2)))

    # paredes de piedra clara (afuera) y bordo con dorado (adentro)
    walls(shell, hx, hz, H, stone, wallin)
    wainscot(shell, hx, hz, 0.7, stonel, GOLD, panel=stone, base=stoned, panels_every=1.1)
    crown(shell, hx, hz, H, stonel, d=0.09, h=0.16)
    B(shell, "filete_oro", -hx, hx, H - 0.24, H - 0.2, hz - 0.03, hz - 0.012, GOLD, r=0.008, seg=1)
    for f, L in ((Face("+x", hx + T), hz + T), (Face("-x", -hx - T), hz + T), (Face("+z", hz + T), hx + T)):
        f.box(shell, "zocalo_rust", 0, 0, 0, 2 * L + 0.02, 0.55, 0.05, stoned, r=0.02, seg=1)
        f.box(shell, "filete_ext", 0, 0.55, 0, 2 * L + 0.02, 0.05, 0.06, GOLD, r=0.01, seg=1)
        sparse_blocks(shell, rnd, f, -L, L, 0.62, H - 0.1, 0.6, 0.3, (stonel, stoned), 0.25, dep=0.012)
    # pilastras adentro (fondo)
    fb = Face("-z", hz - 0.012)
    for u in (-3.9, -1.75, 1.75, 3.9):
        fb.box(shell, "pilastra", u, FY, 0.0, 0.3, H - FY - 0.16, 0.06, stonel, r=0.015, seg=1)
        fb.box(shell, "pilastra_cap", u, H - 0.38, 0.0, 0.38, 0.12, 0.09, GOLD, r=0.015, seg=1)
        fb.box(shell, "pilastra_base", u, FY, 0.0, 0.38, 0.14, 0.09, stoned, r=0.015, seg=1)
    # cuadro central grande, dos medianos, estandartes
    painting(shell, fb, 0, 1.0, 2.0, 1.35, "paisaje", frame=GOLD)
    fb.box(shell, "placa", 0, 0.78, 0.0, 0.44, 0.14, 0.02, GOLD, r=0.008, seg=1)
    painting(shell, fb, -2.85, 1.05, 1.0, 1.15, "retrato", frame=GOLD)
    painting(shell, fb, 2.85, 1.05, 1.0, 1.15, "mar", frame=GOLD)
    banner(shell, fb, -4.85, H - 0.3, 0.5, 1.0, "f3e2b0", trim=GOLD, emblem="c0392b")
    banner(shell, fb, 4.85, H - 0.3, 0.5, 1.0, "f3e2b0", trim=GOLD, emblem="c0392b")
    # postes con cordon rojo frente al cuadro central
    rope_posts(shell, [(-1.15, hz - 0.2), (-0.4, hz - 0.22), (0.4, hz - 0.22), (1.15, hz - 0.2)])

    # frente: columnas de piedra sobre zocalo, pared baja con filete dorado
    zf = -hz - T * 0.5
    B(shell, "estilobato", -hx - T - 0.1, hx + T + 0.1, 0, 0.1, -hz - T - 0.14, -hz + 0.02, stoned, r=0.02)
    cols_x = [-hx - T * 0.3, -3.3, -1.1, 1.1, 3.3, door[0] - 0.2, hx + T * 0.3]
    for x in cols_x:
        column(shell, x, zf, H, 0.17, stonel, cap=GOLD, base=stone, y0=0.1)
    for (a, b) in zip(cols_x[:-2], cols_x[1:-1]):
        B(shell, "pared_baja", a + 0.2, b - 0.2, 0.1, 0.45, -hz - T, -hz, stone, r=0.02)
        B(shell, "pared_baja_tapa", a + 0.18, b - 0.18, 0.45, 0.5, -hz - T - 0.03, -hz + 0.02, stonel, r=0.02)
        B(shell, "filete", a + 0.2, b - 0.2, 0.32, 0.35, -hz - T - 0.012, -hz - T, GOLD, r=0.006, seg=1)
    # escalinata en la puerta (tres escalones) con macetones
    sx0, sx1 = door[0] - 0.42, door[1] + 0.3
    for k in range(3):
        y1 = FY * (3 - k) / 3.0 + 0.01
        B(shell, "escalon", sx0 - 0.12 * k, sx1 + 0.12 * k, 0, y1, -hz - T - 0.28 - 0.26 * k, -hz + 0.0, stonel if k % 2 == 0 else stone, r=0.02)
        B(shell, "nariz", sx0 - 0.12 * k, sx1 + 0.12 * k, y1 - 0.02, y1 + 0.004, -hz - T - 0.3 - 0.26 * k, -hz - T - 0.24 - 0.26 * k, GOLD, r=0.008, seg=1)
    for x in (sx0 - 0.5, ):
        B(shell, "pedestal_urna", x - 0.18, x + 0.18, 0, 0.42, -hz - T - 0.95, -hz - T - 0.59, stone, r=0.02)
        shell.append(lathe("urna", U(x, 0.42, -hz - T - 0.77), [(0.0, 0.0), (0.08, 0.0), (0.06, 0.05), (0.15, 0.16), (0.12, 0.3), (0.15, 0.33), (0.0, 0.33)], stonel, seg=18))
        shell.append(blob("planta", U(x, 0.82, -hz - T - 0.77), 0.17, "5c9a4a", amp=0.35))
    # puertas de bronce dobles abiertas hacia afuera
    for (x, s_) in ((door[0] + 0.02, -1), (door[1] - 0.02, 1)):
        B(shell, "hoja", x - 0.025, x + 0.025, FY, FY + 2.0, -hz - T - 0.37, -hz - T - 0.02, "8a5a2a", r=0.012)
        for k in range(2):
            B(shell, "hoja_panel", x - 0.035, x + 0.035, FY + 0.15 + k * 0.95, FY + 0.95 + k * 0.95, -hz - T - 0.32, -hz - T - 0.07, "b07a3a", r=0.01, seg=1)
        shell.append(US("tirador", x - s_ * 0.04, FY + 1.0, -hz - T - 0.32, 0.03, GOLD))

    # boleteria afuera, a la derecha de la puerta
    bx, bz = hx + 0.95, -hz - 0.45
    B(shell, "bol_base", bx - 0.5, bx + 0.5, 0, 0.12, bz - 0.45, bz + 0.45, stoned, r=0.03)
    B(shell, "bol_cuerpo", bx - 0.45, bx + 0.45, 0.12, 1.0, bz - 0.4, bz + 0.4, "a8423a", r=0.03)
    fbt = Face("-z", bz - 0.4)
    for u in (bx - 0.22, bx + 0.22):
        fbt.box(shell, "bol_panel", u, 0.25, 0.0, 0.32, 0.6, 0.02, "b8574a", r=0.01, seg=1)
    fbt.box(shell, "bol_filete", bx, 0.95, 0.0, 0.92, 0.05, 0.03, GOLD, r=0.01, seg=1)
    B(shell, "bol_mostrador", bx - 0.47, bx + 0.47, 1.0, 1.05, bz - 0.5, bz + 0.42, GOLD, r=0.015)
    for (x, z) in ((bx - 0.42, bz - 0.37), (bx + 0.42, bz - 0.37), (bx - 0.42, bz + 0.37), (bx + 0.42, bz + 0.37)):
        shell.append(UC("bol_poste", x, 1.05, z, 0.03, 0.75, GOLD, seg=8))
    B(shell, "bol_vidrio_l", bx - 0.43, bx - 0.41, 1.05, 1.8, bz - 0.35, bz + 0.35, "cfe6ea", r=0.003, seg=1)
    B(shell, "bol_vidrio_r", bx + 0.41, bx + 0.43, 1.05, 1.8, bz - 0.35, bz + 0.35, "cfe6ea", r=0.003, seg=1)
    B(shell, "bol_vidrio_f", bx - 0.38, bx + 0.38, 1.35, 1.8, bz - 0.38, bz - 0.36, "cfe6ea", r=0.003, seg=1)
    B(shell, "bol_fondo", bx - 0.4, bx + 0.4, 1.05, 1.8, bz + 0.36, bz + 0.4, "6a2a24", r=0.003, seg=1)
    shell.append(US("bol_lampara", bx, 1.62, bz, 0.06, "fff1b0", emis=2.0))
    B(shell, "bol_techo", bx - 0.55, bx + 0.55, 1.8, 1.9, bz - 0.5, bz + 0.5, "a8423a", r=0.03)
    shell.append(hull("bol_cupula", [(bx + sx * 0.5, 1.9, bz + sz * 0.45) for sx in (-1, 1) for sz in (-1, 1)] + [(bx, 2.25, bz)], "8e3b33", r=0.02))
    shell.append(US("bol_remate", bx, 2.27, bz, 0.05, GOLD))
    fbt2 = Face("-z", bz - 0.5)
    fbt2.box(shell, "bol_cartel", bx, 1.82, 0.0, 0.86, 0.17, 0.03, INK, r=0.02, seg=1)
    fbt2.word(shell, "tickets", "TICKETS", bx, 1.905, 0.03, 0.12, GOLD, depth=0.006)
    rope_posts(shell, [(bx - 0.45, bz - 0.85), (bx + 0.1, bz - 1.0), (bx + 0.65, bz - 0.85)])

    # taller (restauracion)
    workbench(shell, hx, hz, top="7a4a2a", leg="5a3520", board=stonel, lamp="c0392b")

    # ---------------- techo: entablamento con friso y MUSEO, fronton con relieve, techo de cobre, estandartes
    YE = H + 0.5
    B(roof, "arquitrabe", -hx - T - 0.15, hx + T + 0.15, H - 0.02, H + 0.2, -hz - T - 0.2, hz + T + 0.15, stonel, r=0.03)
    B(roof, "friso", -hx - T - 0.1, hx + T + 0.1, H + 0.2, YE - 0.06, -hz - T - 0.15, hz + T + 0.1, stone, r=0.02)
    B(roof, "cornisa", -hx - T - 0.25, hx + T + 0.25, YE - 0.06, YE + 0.04, -hz - T - 0.3, hz + T + 0.25, stonel, r=0.03)
    B(roof, "filete_oro_t", -hx - T - 0.16, hx + T + 0.16, H + 0.19, H + 0.22, -hz - T - 0.21, -hz - T - 0.17, GOLD, r=0.008, seg=1)
    ffr = Face("-z", -hz - T - 0.15)
    for k in range(13):
        u = -hx + 0.1 + k * (2 * hx - 0.2) / 12
        if abs(u) < 1.3:
            continue
        ffr.box(roof, "triglifo", u, H + 0.22, 0.0, 0.18, 0.2, 0.03, stoned, r=0.008, seg=1)
    ffr.box(roof, "museo_placa", 0, H + 0.2, 0.05, 1.5, 0.26, 0.02, "8e3b33", r=0.02, seg=1)
    ffr.word(roof, "museo", "MUSEO", 0, H + 0.33, 0.07, 0.24, GOLD, depth=0.014)
    # estandartes colgados del arquitrabe, entre columnas
    fbn = Face("-z", -hz - T - 0.22)
    for u in (-4.4, -2.2, 2.2):
        banner(roof, fbn, u, H - 0.05, 0.55, 1.15, "b0302a", trim=GOLD, emblem=GOLD, rod=GOLDD)
    # techo a dos aguas (cumbrera de adelante hacia atras) de cobre verde con juntas
    pitch = 15.0
    W = hx + T + 0.3
    rise = W * math.tan(math.radians(pitch))
    zt0, zt1 = -hz - T - 0.3, hz + T + 0.3
    for sx in (-1, 1):
        roof.append(hull("faldon", [(0, YE + rise, zt0), (0, YE + rise, zt1), (sx * W, YE, zt0), (sx * W, YE, zt1),
                                    (0, YE + rise - 0.1, zt0), (0, YE + rise - 0.1, zt1), (sx * W, YE - 0.1, zt0), (sx * W, YE - 0.1, zt1)],
                         "6fa58c", r=0.02))
        n = 14
        for k in range(1, n):
            z = zt0 + 0.25 + (zt1 - zt0 - 0.5) * k / n
            roof.append(tube("junta", [U(sx * 0.12, YE + rise - 0.02, z), U(sx * (W - 0.02), YE + 0.02 + 0.02, z)], 0.02, "8cc0a8", seg=6))
    B(roof, "cumbrera", -0.1, 0.1, YE + rise - 0.05, YE + rise + 0.07, zt0, zt1, "5d9078", r=0.04)
    # fronton: triangulo de piedra con cornisas inclinadas, relieve dorado y acroteras
    zp = -hz - T - 0.2
    roof.append(hull("timpano", [(-W + 0.3, YE + 0.04, zp + 0.02), (W - 0.3, YE + 0.04, zp + 0.02), (0, YE + rise - 0.12, zp + 0.02),
                                 (-W + 0.3, YE + 0.04, zp + 0.3), (W - 0.3, YE + 0.04, zp + 0.3), (0, YE + rise - 0.12, zp + 0.3)], stoned))
    ang = math.degrees(math.atan2(rise, W))
    for sx in (-1, 1):
        L = math.hypot(W, rise)
        roof.append(UB("cornisa_incl", sx * W * 0.5, YE + rise * 0.5 - 0.06, zp - 0.06, L + 0.2, 0.14, 0.16, rz=-sx * ang, r=0.03, col=stonel))
    fp = Face("-z", zp + 0.02)
    fp.disc(roof, "medallon", 0, YE + 0.55, 0.0, 0.36, 0.06, GOLD, n=24)
    fp.disc(roof, "medallon_i", 0, YE + 0.55, 0.06, 0.27, 0.03, "c49a3a", n=24)
    fp.shape(roof, "estrella_rel", [(0.22 * math.cos(math.radians(90 + k * 72 + (36 if j else 0))) * (1 if not j else 0.45),
                                     YE + 0.55 + 0.22 * math.sin(math.radians(90 + k * 72 + (36 if j else 0))) * (1 if not j else 0.45))
                                    for k in range(5) for j in (0,)], 0.09, 0.025, GOLD)
    for sx in (-1, 1):
        for k in range(7):
            t = 0.45 + k * 0.33
            fp.disc(roof, "laurel", sx * t, YE + 0.28 + 0.16 * math.sin(k * 0.45) + 0.04 * k * 0.3, 0.0, 0.085, 0.035, GOLD, n=10, sy=0.55)
        fp.shape(roof, "figura", [(sx * 2.6, YE + 0.08), (sx * 3.6, YE + 0.08), (sx * 3.4, YE + 0.24), (sx * 2.8, YE + 0.3)], 0.0, 0.05, stonel)
        fp.disc(roof, "figura_cab", sx * 2.75, YE + 0.33, 0.0, 0.09, 0.05, stonel, n=12)
    roof.append(lathe("acrotera", U(0, YE + rise + 0.03, zp), [(0.0, 0.0), (0.12, 0.0), (0.08, 0.08), (0.14, 0.2), (0.0, 0.34)], GOLD, seg=14))
    for sx in (-1, 1):
        roof.append(lathe("acrotera", U(sx * (W - 0.15), YE + 0.04, zp), [(0.0, 0.0), (0.1, 0.0), (0.07, 0.07), (0.12, 0.17), (0.0, 0.28)], GOLD, seg=14))
    roof.append(hull("timpano_atras", [(-W + 0.1, YE, zt1 - 0.25), (W - 0.1, YE, zt1 - 0.25), (0, YE + rise - 0.1, zt1 - 0.25),
                                       (-W + 0.1, YE, zt1 - 0.05), (W - 0.1, YE, zt1 - 0.05), (0, YE + rise - 0.1, zt1 - 0.05)], stone))

    # ---------------- cartel: monumento de piedra con letras doradas
    cx, cz = -hx + 1.3, -hz - T - 0.85
    sign.append(UB("mon_base", cx, 0, cz, 1.7, 0.16, 0.5, r=0.03, col=stoned))
    sign.append(UB("mon_cuerpo", cx, 0.16, cz, 1.5, 0.66, 0.3, r=0.04, col=stonel))
    sign.append(UB("mon_tapa", cx, 0.82, cz, 1.62, 0.1, 0.38, r=0.03, col=stone))
    sign.append(UB("mon_panel", cx, 0.26, cz - 0.15, 1.3, 0.48, 0.02, r=0.02, col="8e3b33"))
    sign.append(UB("mon_filete", cx, 0.24, cz - 0.155, 1.36, 0.52, 0.012, r=0.02, col=GOLD))
    sign.append(text("letras", "RAREZAS", cx, 0.57, cz - 0.17, 0.27, GOLD, depth=0.014))
    sign.append(text("sub", "MUSEO", cx, 0.37, cz - 0.17, 0.1, "f3e2b0", depth=0.006))
    for s_ in (-1, 1):
        sign.append(UC("mon_maceta", cx + s_ * 0.98, 0, cz, 0.15, 0.3, stoned, r2=0.12, seg=14))
        sign.append(blob("mon_bola", U(cx + s_ * 0.98, 0.5, cz), 0.2, "5c9a4a", amp=0.25))

    pv = open_sign(opn, shell, door[0] - 0.2, 1.35, -hz - T - 0.48, -hz - T - 0.16, arm=GOLDD)
    register_fixed("shack4", [("taller", bench_rect(hx, hz)), ("cordon", (-1.25, 1.25, hz - 0.32, hz))])
    return [("shell", shell, (0, 0, 0)), ("roof", roof, (0, 0, 0)), ("sign", sign, (0, 0, 0)), ("open", opn, pv)]


# ================================================================ etapa 5: museo de lujo (14 x 9 m)
def statue(o, x, z, pose=0):
    """Estatua de marmol sobre pedestal, mirando a -Z."""
    m, md = "f6f3ee", "e4ddd0"
    B(o, "pedestal", x - 0.24, x + 0.24, FY, FY + 0.55, z - 0.22, z + 0.22, "e8e0d0", r=0.02)
    B(o, "pedestal_cap", x - 0.28, x + 0.28, FY + 0.55, FY + 0.62, z - 0.26, z + 0.26, md, r=0.02)
    B(o, "pedestal_pie", x - 0.28, x + 0.28, FY, FY + 0.08, z - 0.26, z + 0.26, md, r=0.02)
    B(o, "pedestal_oro", x - 0.245, x + 0.245, FY + 0.47, FY + 0.5, z - 0.225, z + 0.225, GOLD, r=0.006, seg=1)
    y0 = FY + 0.62
    o.append(lathe("tunica", U(x, y0, z), [(0.0, 0.0), (0.17, 0.0), (0.16, 0.06), (0.12, 0.4), (0.1, 0.62), (0.13, 0.72), (0.09, 0.8), (0.0, 0.82)], m, seg=18))
    o.append(US("cabeza", x, y0 + 0.9, z, 0.075, m))
    o.append(US("pelo", x, y0 + 0.93, z + 0.02, 0.07, md, scale=(1, 1, 0.9)))
    if pose == 0:
        o.append(tube("brazo_alto", [U(x + 0.12, y0 + 0.74, z), U(x + 0.2, y0 + 0.95, z - 0.02), U(x + 0.2, y0 + 1.12, z - 0.02)], 0.03, m))
        o.append(UC("antorcha", x + 0.2, y0 + 1.08, z - 0.02, 0.025, 0.12, m, r2=0.04, seg=10))
        o.append(US("llama", x + 0.2, y0 + 1.24, z - 0.02, 0.045, GOLD, scale=(1, 1, 1.4)))
        o.append(tube("brazo_bajo", [U(x - 0.12, y0 + 0.74, z), U(x - 0.16, y0 + 0.5, z - 0.05), U(x - 0.08, y0 + 0.4, z - 0.12)], 0.028, m))
        o.append(UB("libro", x - 0.06, y0 + 0.36, z - 0.13, 0.12, 0.16, 0.05, r=0.01, col=md))
    else:
        o.append(tube("brazo", [U(x - 0.12, y0 + 0.74, z), U(x - 0.2, y0 + 0.5, z - 0.04), U(x - 0.18, y0 + 0.32, z - 0.06)], 0.028, m))
        o.append(tube("brazo2", [U(x + 0.12, y0 + 0.74, z), U(x + 0.08, y0 + 0.62, z - 0.12), U(x - 0.02, y0 + 0.62, z - 0.14)], 0.028, m))
        o.append(torus("laurel", U(x, y0 + 0.97, z), 0.07, 0.014, GOLD, seg=14, mseg=5))
        o.append(lathe("anfora", U(x - 0.18, y0 + 0.2, z - 0.08), [(0.0, 0.0), (0.04, 0.0), (0.07, 0.07), (0.04, 0.14), (0.0, 0.15)], md, seg=12))


def niche(o, f, u, w=0.9, h=2.2, back="5a6f86"):
    """Hornacina con arco: marco de pilastras con capitel dorado y fondo azul oscuro."""
    f.box(o, "nicho_fondo", u, FY + 0.05, 0.0, w, h - w * 0.5, 0.02, back, r=0.006, seg=1)
    f.disc(o, "nicho_arco", u, FY + 0.05 + h - w * 0.5, 0.0, w * 0.5, 0.02, back, n=20)
    f.disc(o, "nicho_concha", u, FY + 0.05 + h - w * 0.5, 0.02, w * 0.36, 0.01, "6d84a0", n=16)
    for s_ in (-1, 1):
        f.box(o, "nicho_pil", u + s_ * (w * 0.5 + 0.07), FY, 0.0, 0.14, h - w * 0.5 + 0.05, 0.07, "efe7d8", r=0.015, seg=1)
        f.box(o, "nicho_cap", u + s_ * (w * 0.5 + 0.07), FY + h - w * 0.5, 0.0, 0.2, 0.08, 0.1, GOLD, r=0.015, seg=1)
    n = 9
    for k in range(n + 1):
        a = math.pi * k / n
        f.box(o, "nicho_dov", u + (w * 0.5 + 0.06) * math.cos(a), FY + 0.05 + h - w * 0.5 + (w * 0.5 + 0.06) * math.sin(a) - 0.05, 0.0,
              0.12, 0.1, 0.08, "efe7d8" if k % 2 else GOLD, r=0.015, seg=1)


def fountain(o, x, z):
    mar = "efe9df"
    o.append(lathe("pileta", U(x, FY, z), [(0.0, 0.0), (0.66, 0.0), (0.68, 0.04), (0.64, 0.3), (0.7, 0.33), (0.7, 0.38), (0.58, 0.38), (0.57, 0.12), (0.0, 0.12)], mar, seg=28))
    o.append(UC("agua", x, FY + 0.27, z, 0.575, 0.02, "7cc4e0", seg=28))
    o.append(torus("agua_onda", U(x, FY + 0.29, z), 0.32, 0.012, "b8e4f2", seg=24, mseg=4))
    o.append(torus("pileta_oro", U(x, FY + 0.36, z), 0.69, 0.018, GOLD, seg=28, mseg=5))
    o.append(lathe("pie", U(x, FY + 0.12, z), [(0.0, 0.0), (0.14, 0.0), (0.09, 0.08), (0.06, 0.4), (0.09, 0.5), (0.0, 0.5)], mar, seg=16))
    o.append(lathe("taza", U(x, FY + 0.6, z), [(0.0, 0.0), (0.08, 0.0), (0.26, 0.1), (0.28, 0.14), (0.24, 0.14), (0.0, 0.07)], mar, seg=22))
    o.append(UC("agua_t", x, FY + 0.69, z, 0.24, 0.02, "7cc4e0", seg=20))
    o.append(lathe("remate", U(x, FY + 0.68, z), [(0.0, 0.0), (0.05, 0.0), (0.03, 0.12), (0.06, 0.18), (0.0, 0.26)], GOLD, seg=12))
    for k in range(8):
        a = 2 * math.pi * k / 8
        ca, sa = math.cos(a), math.sin(a)
        o.append(tube("chorro", [U(x + 0.27 * ca, FY + 0.73, z + 0.27 * sa), U(x + 0.36 * ca, FY + 0.62, z + 0.36 * sa),
                                 U(x + 0.42 * ca, FY + 0.45, z + 0.42 * sa), U(x + 0.45 * ca, FY + 0.29, z + 0.45 * sa)], 0.016, "a8dcef", seg=6))
    o.append(tube("chorro_c", [U(x, FY + 0.92, z), U(x, FY + 1.1, z)], 0.02, "a8dcef", seg=6))
    o.append(US("gota", x, FY + 1.12, z, 0.035, "c8ecf8"))
    for k in range(5):
        a = 1.3 * k
        o.append(UC("moneda", x + 0.42 * math.cos(a), FY + 0.285, z + 0.42 * math.sin(a), 0.03, 0.006, "coin", seg=10))


def armchair(o, x, z, ry, col="8e1f2f", cold="741726"):
    f = LT(x, z, ry)

    def bx(name, lx, ly, lz, w, h, d, c, r=0.04, seg=2):
        px, py, pz = f(lx, ly, lz)
        o.append(UB(name, px, py, pz, w, h, d, ry=ry, r=r, seg=seg, col=c))
    bx("asiento", 0, FY + 0.14, 0, 0.62, 0.22, 0.56, col, r=0.06)
    bx("almohadon", 0, FY + 0.36, -0.02, 0.46, 0.08, 0.48, cold, r=0.04)
    bx("respaldo", 0, FY + 0.14, 0.24, 0.62, 0.72, 0.16, col, r=0.07)
    for s_ in (-1, 1):
        bx("brazo", s_ * 0.27, FY + 0.14, 0, 0.12, 0.4, 0.54, col, r=0.055)
        for k in range(2):
            px, py, pz = f(-0.12 + 0.24 * k, FY + 0.62, 0.155)
        px, py, pz = f(s_ * 0.27, FY + 0.54, -0.26)
        o.append(US("voluta", px, py, pz, 0.045, GOLD))
    for (lx, lz) in ((-0.26, -0.22), (0.26, -0.22), (-0.26, 0.24), (0.26, 0.24)):
        px, py, pz = f(lx, 0, lz)
        o.append(UC("pata", px, FY, pz, 0.03, 0.14, GOLD, r2=0.02, seg=10))
    for k in range(3):
        px, py, pz = f(-0.18 + 0.18 * k, FY + 0.62, 0.155)
        o.append(US("boton", px, py, pz, 0.02, GOLD))
    bx("ribete", 0, FY + 0.12, -0.285, 0.64, 0.035, 0.02, GOLD, r=0.01, seg=1)


def chandelier(o, x, z, ytop, ybot):
    """Arana de cristal: cadena, columna dorada, aro con velas y caireles."""
    y0 = ybot + 0.55
    o.append(tube("cadena", [U(x, ytop, z), U(x, y0 + 0.2, z)], 0.012, GOLDD, seg=6))
    o.append(lathe("columna_a", U(x, ybot + 0.05, z), [(0.0, 0.0), (0.06, 0.02), (0.04, 0.2), (0.08, 0.32), (0.03, 0.5), (0.05, 0.62), (0.0, 0.72)], GOLD, seg=14))
    yr = ybot + 0.3
    o.append(torus("aro", U(x, yr, z), 0.42, 0.022, GOLD, seg=20, mseg=4))
    o.append(torus("aro2", U(x, yr + 0.3, z), 0.22, 0.016, GOLD, seg=14, mseg=4))
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        ca, sa = math.cos(a), math.sin(a)
        o.append(tube("brazo", [U(x, yr + 0.08, z), U(x + 0.25 * ca, yr - 0.02, z + 0.25 * sa), U(x + 0.42 * ca, yr + 0.02, z + 0.42 * sa)], 0.012, GOLD, seg=6))
        o.append(UC("cazoleta", x + 0.42 * ca, yr + 0.01, z + 0.42 * sa, 0.04, 0.03, GOLD, r2=0.05, seg=6))
        o.append(UC("vela", x + 0.42 * ca, yr + 0.04, z + 0.42 * sa, 0.016, 0.1, "fbf6ee", seg=6))
        o.append(UC("llama", x + 0.42 * ca, yr + 0.14, z + 0.42 * sa, 0.018, 0.05, "ffd23a", r2=0.0, seg=6, emis=2.5))
    for k in range(14):
        a = 2 * math.pi * k / 14
        ca, sa = math.cos(a), math.sin(a)
        o.append(US("cairel", x + 0.4 * ca, yr - 0.09, z + 0.4 * sa, 0.026, "dff4ff", scale=(1, 1, 1.8), seg=6, rings=4))
    o.append(US("bola", x, ybot - 0.02, z, 0.07, "dff4ff"))


def stage5():
    """Museo de lujo: gran salon con cupula de vidrio (nervios dorados), piso de marmol en damero, alfombra roja desde
    la puerta, aranas de cristal, estatuas en hornacinas, fuente en una esquina y sillones VIP de terciopelo."""
    rnd = random.Random(55)
    hx, hz, H = 7.0, 4.5, 3.4
    door = (hx - 0.95, hx - 0.25)
    shell, roof, sign, opn = [], [], [], []
    marble, marbled, marblel = "f1ebe0", "ddd3c2", "fbf8f2"
    wallin = "efe3cc"
    B(shell, "base", -hx - T - 0.3, hx + T + 0.3, -0.05, FY - 0.004, -hz - T - 0.3, hz + T + 0.3, "cfc6b6", r=0.05)
    B(shell, "bajo_piso", -hx, hx, FY - 0.03, FY - 0.002, -hz, hz, "5a5560", r=0.003, seg=1)
    polys = []
    nx, nz = int(2 * hx / 0.5), int(2 * hz / 0.5)
    for i in range(nx):
        for j in range(nz):
            x0, z0 = -hx + i * 0.5, -hz + j * 0.5
            polys.append((rect(x0, x0 + 0.5, z0, z0 + 0.5, 0.005), "f4efe4" if (i + j) % 2 == 0 else "34303a"))
    shell.append(flat("damero", polys, FY))
    # alfombra roja: desde la puerta (y afuera por la escalinata), cruza y sube por el centro
    red, redd = "b8262f", "9c1f28"

    def carpet(x0, x1, z0, z1, y=FY):
        B(shell, "alfombra_oro", x0, x1, y - 0.004, y + 0.01, z0, z1, GOLD, r=0.004, seg=1)
        B(shell, "alfombra", x0 + 0.05, x1 - 0.05, y - 0.004, y + 0.014, z0 + (0.05 if z0 > -hz - 0.5 else 0), z1 - 0.05, red, r=0.004, seg=1)
    carpet(door[0] + 0.05, door[1] - 0.05, -hz, -2.75)
    carpet(-0.4, door[1] - 0.05, -3.45, -2.75, y=FY + 0.003)
    carpet(-0.4, 0.4, -3.45, hz - 0.75, y=FY + 0.006)
    # paredes
    walls(shell, hx, hz, H, marble, wallin)
    wainscot(shell, hx, hz, 0.75, "d9b8a8", GOLD, panel="e6cbbd", base="8a7a6a", panels_every=1.2)
    crown(shell, hx, hz, H, marblel, d=0.1, h=0.18)
    B(shell, "filete_oro", -hx, hx, H - 0.26, H - 0.22, hz - 0.035, hz - 0.012, GOLD, r=0.008, seg=1)
    for f, L in ((Face("+x", hx + T), hz + T), (Face("-x", -hx - T), hz + T), (Face("+z", hz + T), hx + T)):
        f.box(shell, "zocalo_rust", 0, 0, 0, 2 * L + 0.02, 0.6, 0.05, marbled, r=0.02, seg=1)
        f.box(shell, "filete_ext", 0, 0.6, 0, 2 * L + 0.02, 0.05, 0.06, GOLD, r=0.01, seg=1)
        f.box(shell, "cornisa_ext", 0, H - 0.2, 0, 2 * L + 0.05, 0.2, 0.07, marblel, r=0.02, seg=1)
        for k in range(int(L / 1.4)):
            for s_ in (-1, 1):
                f.box(shell, "pilastra_ext", s_ * (0.7 + k * 1.4), 0.65, 0, 0.26, H - 0.85, 0.05, marblel, r=0.015, seg=1)
    fb = Face("-z", hz - 0.012)
    # hornacinas con estatuas en el fondo
    for (u, pose) in ((-3.4, 0), (0.0, 1), (3.4, 0)):
        niche(shell, fb, u)
        statue(shell, u, hz - 0.27, pose)
    # pilastras de marmol con capitel dorado entre hornacinas + cuadros
    for u in (-5.15, -2.55, 2.55, 4.85):
        fb.box(shell, "pilastra", u, FY, 0.0, 0.3, H - FY - 0.2, 0.06, marblel, r=0.015, seg=1)
        fb.box(shell, "pilastra_cap", u, H - 0.44, 0.0, 0.4, 0.14, 0.1, GOLD, r=0.015, seg=1)
    # VIP: sillones de terciopelo atras a la derecha, mesita, palmera, cordon y placa
    B(shell, "vip_alfombra", 5.0, hx, FY - 0.004, FY + 0.012, 2.9, hz, "5a2a5a", r=0.004, seg=1)
    B(shell, "vip_alf_borde", 5.08, hx - 0.08, FY - 0.004, FY + 0.016, 2.98, hz - 0.08, "7a3a7a", r=0.004, seg=1)
    armchair(shell, 5.55, 3.85, -150)
    armchair(shell, 6.45, 3.25, -120)
    shell.append(UC("mesita_pie", 6.35, FY, 4.05, 0.05, 0.42, GOLD, seg=12))
    shell.append(UC("mesita_base", 6.35, FY, 4.05, 0.16, 0.03, GOLD, seg=16))
    shell.append(UC("mesita", 6.35, FY + 0.42, 4.05, 0.24, 0.04, "34303a", seg=20))
    shell.append(UC("balde", 6.3, FY + 0.46, 4.05, 0.08, 0.14, "c9ced6", r2=0.09, seg=14))
    shell.append(UC("botella", 6.3, FY + 0.5, 4.05, 0.03, 0.22, "2f5d3a", seg=10))
    shell.append(UC("botella_c", 6.3, FY + 0.72, 4.05, 0.015, 0.05, GOLD, seg=8))
    for k in range(2):
        shell.append(UC("copa", 6.48 - k * 0.3, FY + 0.46, 4.18 - k * 0.2, 0.025, 0.14, "e8f6fb", r2=0.04, seg=10))
    shell.append(UC("maceta_palma", 6.7, FY, 2.75, 0.17, 0.32, GOLD, r2=0.13, seg=16))
    for k in range(6):
        a = math.radians(k * 60)
        shell.append(tube("palma", [U(6.7, FY + 0.3, 2.75), U(6.7 + 0.15 * math.cos(a), FY + 0.95, 2.75 + 0.15 * math.sin(a)),
                                    U(6.7 + 0.45 * math.cos(a), FY + 0.8, 2.75 + 0.45 * math.sin(a))], 0.03, "4f9a4a", seg=6))
        shell.append(US("hoja", 6.7 + 0.32 * math.cos(a), FY + 0.9, 2.75 + 0.32 * math.sin(a), 0.1, "5cae55", scale=(1.6, 1.6, 0.35), seg=10, rings=6))
    rope_posts(shell, [(5.0, 2.95), (5.0, 3.75), (5.0, 4.4)], post=GOLD, rope=red)
    fb.box(shell, "vip_placa", 5.95, 2.2, 0.0, 0.9, 0.36, 0.03, "34303a", r=0.02, seg=1)
    fb.box(shell, "vip_placa_b", 5.95, 2.18, -0.005, 0.96, 0.4, 0.03, GOLD, r=0.02, seg=1)
    fb.word(shell, "vip", "VIP", 5.95, 2.38, 0.03, 0.22, GOLD, depth=0.012)
    for u in (5.3, 6.6):
        fb.box(shell, "aplique", u, 1.9, 0.0, 0.08, 0.2, 0.04, GOLD, r=0.015, seg=1)
        x0, y0, z0 = fb.P(u, 2.1, 0.12)
        shell.append(US("aplique_globo", x0, y0, z0, 0.07, "fff1b0", emis=2.0))
    # fuente en la esquina de adelante a la izquierda
    fountain(shell, -hx + 0.8, -hz + 0.8)
    # cuadros grandes en el fondo entre hornacinas
    painting(shell, fb, -1.75, 1.0, 0.9, 1.15, "retrato", frame=GOLD)
    painting(shell, fb, 1.75, 1.0, 0.9, 1.15, "paisaje", frame=GOLD)
    painting(shell, fb, -4.5, 1.6, 0.8, 0.6, "noche", frame=GOLD)
    # taller de lujo
    workbench(shell, hx, hz, top="5a3520", leg=GOLD, board="d9b8a8", lamp="2f8a5c", deluxe=True)

    # frente: columnas corintias (capitel dorado) y balaustrada de marmol con remates dorados
    zf = -hz - T * 0.5
    B(shell, "estilobato", -hx - T - 0.1, hx + T + 0.1, 0, 0.1, -hz - T - 0.16, -hz + 0.02, marbled, r=0.02)
    cols_x = [-hx - T * 0.3, -4.6, -2.3, 0.0, 2.3, 4.6, door[0] - 0.2, hx + T * 0.3]
    for x in cols_x:
        column(shell, x, zf, H, 0.18, marblel, cap=GOLD, base=marble, y0=0.1)
        shell.append(torus("corintio", U(x, H - 0.33, zf), 0.2, 0.035, GOLD, seg=12, mseg=4))
    for (a, b) in zip(cols_x[:-2], cols_x[1:-1]):
        B(shell, "bal_base", a + 0.22, b - 0.22, 0.1, 0.18, -hz - T + 0.01, -hz - 0.01, marble, r=0.02)
        B(shell, "bal_pasa", a + 0.2, b - 0.2, 0.42, 0.5, -hz - T - 0.02, -hz + 0.02, marblel, r=0.025)
        B(shell, "bal_oro", a + 0.22, b - 0.22, 0.5, 0.52, -hz - T - 0.0, -hz + 0.0, GOLD, r=0.006, seg=1)
        nb = int((b - a - 0.44) / 0.24)
        for i in range(nb):
            x = a + 0.22 + (b - a - 0.44) * (i + 0.5) / nb
            shell.append(lathe("balaustre", U(x, 0.18, zf), [(0.035, 0.0), (0.055, 0.03), (0.032, 0.08), (0.06, 0.14), (0.045, 0.2), (0.03, 0.24)], marblel, seg=8))
    # escalinata de marmol con la alfombra roja por encima
    sx0, sx1 = door[0] - 0.45, door[1] + 0.32
    for k in range(3):
        y1 = FY * (3 - k) / 3.0 + 0.01
        B(shell, "escalon", sx0 - 0.15 * k, sx1 + 0.15 * k, 0, y1, -hz - T - 0.3 - 0.28 * k, -hz, marblel if k % 2 == 0 else marble, r=0.02)
        B(shell, "nariz", sx0 - 0.15 * k, sx1 + 0.15 * k, y1 - 0.02, y1 + 0.004, -hz - T - 0.32 - 0.28 * k, -hz - T - 0.26 - 0.28 * k, GOLD, r=0.008, seg=1)
    B(shell, "alfombra_esc", door[0] + 0.05, door[1] - 0.05, 0.0, FY + 0.016, -hz - T - 0.95, -hz, red, r=0.01)
    # puertas doradas vidriadas abiertas hacia afuera
    for (x, s_) in ((door[0] + 0.02, -1), (door[1] - 0.02, 1)):
        B(shell, "hoja", x - 0.025, x + 0.025, FY + 0.016, FY + 2.2, -hz - T - 0.37, -hz - T - 0.02, GOLD, r=0.012)
        B(shell, "hoja_vidrio", x - 0.032, x + 0.032, FY + 0.25, FY + 2.05, -hz - T - 0.31, -hz - T - 0.08, "cfe6ea", r=0.006, seg=1)
    # faroles a los lados de la escalinata
    for x in (sx0 - 0.6, sx1 + 0.55):
        shell.append(UC("farol_base", x, 0, -hz - T - 0.85, 0.12, 0.15, marbled, seg=12))
        shell.append(UC("farol_poste", x, 0.15, -hz - T - 0.85, 0.035, 1.35, GOLDD, seg=10))
        shell.append(US("farol_globo", x, 1.6, -hz - T - 0.85, 0.14, "fff1b0", emis=2.0))
        shell.append(UC("farol_corona", x, 1.72, -hz - T - 0.85, 0.07, 0.08, GOLD, r2=0.02, seg=10))

    # ---------------- techo: entablamento, atico con balaustrada y urnas, terraza, tambor y cupula de vidrio, aranas
    YE = H + 0.55
    B(roof, "arquitrabe", -hx - T - 0.15, hx + T + 0.15, H - 0.02, H + 0.22, -hz - T - 0.2, hz + T + 0.15, marblel, r=0.03)
    B(roof, "friso", -hx - T - 0.1, hx + T + 0.1, H + 0.22, YE - 0.08, -hz - T - 0.15, hz + T + 0.1, marble, r=0.02)
    B(roof, "friso_oro", -hx - T - 0.11, hx + T + 0.11, H + 0.22, H + 0.26, -hz - T - 0.16, hz + T + 0.11, GOLD, r=0.008, seg=1)
    B(roof, "cornisa", -hx - T - 0.25, hx + T + 0.25, YE - 0.08, YE + 0.04, -hz - T - 0.3, hz + T + 0.25, marblel, r=0.03)
    ffr = Face("-z", -hz - T - 0.15)
    ffr.word(roof, "museo", "GRAN MUSEO", 0, H + 0.39, 0.0, 0.22, GOLD, depth=0.014)
    for u in (-5.6, -3.4, 3.4, 5.6):
        ffr.disc(roof, "roseta", u, H + 0.39, 0.0, 0.1, 0.03, GOLD, n=14)
    B(roof, "terraza", -hx - T - 0.1, hx + T + 0.1, YE + 0.04, YE + 0.1, -hz - T - 0.15, hz + T + 0.1, "d8d0c2", r=0.02)
    tp = []
    for i in range(int(2 * hx / 1.0) + 1):
        for j in range(int(2 * hz / 1.0) + 1):
            x0, z0 = -hx + i * 1.0 - 0.0, -hz + j * 1.0
            c = clip(rect(x0, x0 + 1.0, z0, z0 + 1.0, 0.02), -hx, hx, -hz, hz)
            if len(c) >= 3:
                tp.append((c, "cdc4b3" if (i + j) % 2 else "e0d8ca"))
    roof.append(flat("losas_terraza", tp, YE + 0.102))
    # atico: muro bajo perimetral con paneles y urnas
    AX, AZ = hx + T + 0.05, hz + T + 0.05
    ya0, ya1 = YE + 0.1, YE + 0.55
    for (x0, x1, z0, z1) in ((-AX, AX, -AZ, -AZ + 0.16), (-AX, AX, AZ - 0.16, AZ), (-AX, -AX + 0.16, -AZ, AZ), (AX - 0.16, AX, -AZ, AZ)):
        B(roof, "atico", x0, x1, ya0, ya1, z0, z1, marble, r=0.02)
        B(roof, "atico_tapa", x0 - 0.04, x1 + 0.04, ya1, ya1 + 0.06, z0 - 0.04, z1 + 0.04, marblel, r=0.02)
    fa = Face("-z", -AZ)
    for k in range(9):
        u = -AX + 0.85 + k * (2 * AX - 1.7) / 8
        if abs(u) < 1.2:
            continue
        fa.box(roof, "atico_panel", u, ya0 + 0.08, 0.0, 1.0, 0.28, 0.02, marbled, r=0.01, seg=1)
    # escudo dorado sobre la entrada principal del atico
    fa.disc(roof, "escudo", 0, ya1 + 0.15, 0.0, 0.42, 0.08, GOLD, n=24, sy=0.85)
    fa.disc(roof, "escudo_i", 0, ya1 + 0.15, 0.08, 0.32, 0.03, red, n=24, sy=0.85)
    fa.shape(roof, "corona", [(-0.2, ya1 + 0.1), (0.2, ya1 + 0.1), (0.24, ya1 + 0.3), (0.0, ya1 + 0.22), (-0.24, ya1 + 0.3)], 0.11, 0.03, GOLD)
    for (x, z) in ((-AX, -AZ), (AX, -AZ), (-AX, AZ), (AX, AZ), (-2.3, -AZ), (2.3, -AZ)):
        roof.append(lathe("urna", U(x, ya1 + 0.06, z), [(0.0, 0.0), (0.09, 0.0), (0.06, 0.06), (0.15, 0.18), (0.12, 0.3), (0.05, 0.36), (0.08, 0.4), (0.0, 0.46)], GOLD, seg=14))
    # tambor y cupula
    R = 2.6
    yd0 = YE + 0.1
    roof.append(UC("tambor", 0, yd0, 0, R + 0.1, 0.75, marble, seg=40))
    roof.append(UC("tambor_cornisa", 0, yd0 + 0.75, 0, R + 0.2, 0.1, marblel, seg=40))
    roof.append(torus("tambor_oro", U(0, yd0 + 0.7, 0), R + 0.11, 0.025, GOLD, seg=48, mseg=4))
    for k in range(16):
        a = 2 * math.pi * k / 16
        th = math.degrees(a)
        x, z = (R + 0.1) * math.cos(a), (R + 0.1) * math.sin(a)
        if k % 2 == 0:
            roof.append(UB("tambor_ventana", x, yd0 + 0.18, z, 0.32, 0.42, 0.06, ry=90 - th, r=0.03, seg=1, col="3d5a7a"))
        else:
            roof.append(UB("tambor_pil", x, yd0 + 0.05, z, 0.16, 0.65, 0.08, ry=90 - th, r=0.02, seg=1, col=marblel))
    yc = yd0 + 0.85
    Hd = 2.3
    roof.append(dome("cupula", 0, yc, 0, R, Hd, 24, 7, ["a9d8ea", "c4e6f2", "94cbe0"]))
    for k in range(12):
        a = 2 * math.pi * k / 12
        pts = []
        for j in range(9):
            t = j / 8 * (math.pi / 2) * 0.97
            r = (R + 0.03) * math.cos(t)
            pts.append(U(r * math.cos(a), yc + (Hd + 0.03) * math.sin(t), r * math.sin(a)))
        roof.append(tube("nervio", pts, 0.045, GOLD, seg=6))
    for t in (0.0, 0.42, 0.85):
        r = (R + 0.03) * math.cos(t)
        roof.append(torus("anillo", U(0, yc + (Hd + 0.03) * math.sin(t), 0), r, 0.04, GOLD, seg=36, mseg=4))
    # brillos en el vidrio
    for (a, t) in ((200, 0.35), (215, 0.6), (250, 0.3)):
        ar = math.radians(a)
        r = (R + 0.04) * math.cos(t)
        roof.append(US("brillo", r * math.cos(ar), yc + (Hd + 0.04) * math.sin(t), r * math.sin(ar), 0.16, "f4fbff", scale=(0.5, 0.5, 1.6), seg=10, rings=6))
    # linterna en la punta
    yl = yc + Hd - 0.05
    roof.append(UC("linterna_base", 0, yl, 0, 0.42, 0.08, marblel, seg=20))
    for k in range(8):
        a = 2 * math.pi * k / 8
        roof.append(UC("linterna_col", 0.34 * math.cos(a), yl + 0.08, 0.34 * math.sin(a), 0.035, 0.4, marblel, seg=8))
    roof.append(UC("linterna_vidrio", 0, yl + 0.08, 0, 0.3, 0.4, "c4e6f2", seg=16))
    roof.append(UC("linterna_tapa", 0, yl + 0.48, 0, 0.42, 0.08, GOLD, seg=20))
    roof.append(lathe("linterna_cup", U(0, yl + 0.56, 0), [(0.36, 0.0), (0.3, 0.12), (0.18, 0.22), (0.0, 0.28)], GOLD, seg=20))
    roof.append(UC("aguja", 0, yl + 0.8, 0, 0.03, 0.4, GOLD, r2=0.005, seg=8))
    roof.append(US("aguja_bola", 0, yl + 0.9, 0, 0.07, GOLD))
    # aranas de cristal (cuelgan del cielorraso; pieza del techo)
    for (x, z) in ((-3.6, 0.2), (0.0, -0.4), (3.6, 0.2)):
        chandelier(roof, x, z, H, H - 1.0)
    B(roof, "cielorraso", -hx, hx, H - 0.04, H, -hz, hz, marblel, r=0.01, seg=1)

    # ---------------- cartel: placa de marmol verde con marco dorado y corona sobre pedestal, con dos faroles
    cx, cz = -hx + 1.7, -hz - T - 0.95
    sign.append(UB("pedestal", cx, 0, cz, 1.9, 0.22, 0.5, r=0.03, col=marbled))
    sign.append(UB("placa_marco", cx, 0.22, cz, 1.8, 0.82, 0.16, r=0.05, col=GOLD))
    sign.append(UB("placa", cx, 0.27, cz - 0.02, 1.64, 0.72, 0.16, r=0.04, col="1f4d3f"))
    sign.append(text("letras", "RAREZAS", cx, 0.7, cz - 0.11, 0.32, GOLD, depth=0.018))
    sign.append(text("sub", "MUSEO DE LUJO", cx, 0.44, cz - 0.11, 0.1, "f3e2b0", depth=0.006))
    sign.append(hull("corona", [(cx - 0.32, 1.04, cz), (cx + 0.32, 1.04, cz), (cx - 0.32, 1.04, cz + 0.08), (cx + 0.32, 1.04, cz + 0.08),
                                (cx - 0.4, 1.36, cz + 0.04), (cx + 0.4, 1.36, cz + 0.04), (cx, 1.3, cz + 0.04)], GOLD, r=0.02))
    for k in range(5):
        sign.append(US("corona_perla", cx - 0.36 + k * 0.18, 1.38 if k % 2 == 0 else 1.32, cz + 0.04, 0.04, "fbf6ee" if k != 2 else "c0392b"))
    for s_ in (-1, 1):
        x = cx + s_ * 1.15
        sign.append(UC("farolito_poste", x, 0, cz, 0.04, 1.05, GOLDD, seg=10))
        sign.append(US("farolito", x, 1.15, cz, 0.12, "fff1b0", emis=2.0))
        sign.append(UC("farolito_c", x, 1.25, cz, 0.06, 0.06, GOLD, r2=0.015, seg=10))

    pv = open_sign(opn, shell, door[0] - 0.2, 1.4, -hz - T - 0.5, -hz - T - 0.17, arm=GOLDD)
    register_fixed("shack5", [("taller", bench_rect(hx, hz)), ("estatua", (-3.7, -3.1, hz - 0.55, hz)), ("estatua", (-0.3, 0.3, hz - 0.55, hz)),
                              ("estatua", (3.1, 3.7, hz - 0.55, hz)), ("fuente", (-hx, -hx + 1.55, -hz, -hz + 1.55)),
                              ("vip", (4.9, hx, 2.6, hz))])
    return [("shell", shell, (0, 0, 0)), ("roof", roof, (0, 0, 0)), ("sign", sign, (0, 0, 0)), ("open", opn, pv)]


# ================================================================ render de control
def game_view(objs, path, scale, size=720, pitch=48.0, target=(0, 0.6, 0)):
    """Vista del juego: camara ortografica mirando al norte (+Z de Unity) con 48 grados de inclinacion."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = size
    sc.render.resolution_y = int(size * 0.8)
    w = sc.world or bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (*lib.srgb_to_lin(lib.hexcol("e8f4ff")), 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    ctr = Vector(U(*target))
    el = math.radians(pitch)
    loc = ctr + Vector((0, math.cos(el), math.sin(el))) * 40.0
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cd.ortho_scale = scale
    cd.clip_end = 200
    cam = bpy.data.objects.new("cam", cd)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (ctr - loc).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    ld = bpy.data.lights.new("sol", "SUN")
    ld.energy = 3.2
    ld.color = lib.hexcol("fff1d6")
    ld.angle = math.radians(8)
    sun = bpy.data.objects.new("sol", ld)
    bpy.context.scene.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(50), 0, math.radians(-35)))
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.002))
    fl = bpy.context.active_object
    lib.setmat(fl, "a8c97a")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for x in (cam, sun, fl):
        bpy.data.objects.remove(x, do_unlink=True)


HALF = {"shack2": (3.0, 2.25), "shack3": (4.0, 2.75), "shack4": (5.5, 3.5), "shack5": (7.0, 4.5)}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else ["stage2", "stage3", "stage4", "stage5"]
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    views = argv[argv.index("--views") + 1].split(",") if "--views" in argv else ["persp", "open", "game", "gameroof"]
    if prev:
        os.makedirs(prev, exist_ok=True)
    for st, fn in (("stage2", stage2), ("stage3", stage3), ("stage4", stage4), ("stage5", stage5)):
        if st not in only:
            continue
        name = "shack" + st[-1]
        lib.reset()
        parts, t = lib.finish_parts(fn(), name, OUT, ao=0.55, ao_dist=0.35, budget=60000)
        print("SHACK", name, t, "fixed:", FIXED.get(name))
        if not prev:
            continue
        roof = parts[1]
        hx, hz = HALF[name]
        if "persp" in views:
            lib.preview(parts, os.path.join(prev, name + ".png"), size=720, elev=36, azim=196)
        roof.hide_render = True
        if "open" in views:
            lib.preview([p for p in parts if p is not roof], os.path.join(prev, name + "_open.png"), size=720, elev=44, azim=196)
        if "game" in views:
            game_view(parts, os.path.join(prev, name + "_game.png"), scale=2 * hx + 2.4, target=(0, 0.5, -0.3))
        roof.hide_render = False
        if "gameroof" in views:
            game_view(parts, os.path.join(prev, name + "_gameroof.png"), scale=2 * hx + 2.4, target=(0, 1.2, -0.3))


if __name__ == "__main__":
    main()
