"""Biblioteca comun de los kits de habitacion (RoomKit) modelados a mano: primitivas, paleta, exportacion al formato
del juego (IslandArt.TripoModel) y vista previa del modulo armado. Cada tema (kit_<tema>.py) define la paleta y
build(pieza). Medidas reales del juego (IslandArtComplex): pared 1.7 x 1.25, hueco de puerta 0.9 x 0.93 centrado abajo,
techo para 2.04 x 2.04. En Blender el frente mira a -Y (RoomKit.KitFront). Nada puede pasar del alto de la pared (se
estira a 1.25): lo que sobresale arriba (cupulas, almenas) va en el techo.
uso desde un tema: blender -b -P kit_<tema>.py -- outdir [--preview png]
"""
import bpy, bmesh, math, sys, os, json
CELL = 8; NCOL = 4
SLATE = {"pizarra_a": "#5d6673", "pizarra_b": "#4f5866"}   # techo comun de todo el Complejo (= IslandArt.RoofBase)
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUTDIR = argv[0] if argv else "."
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None
SHEET = argv[argv.index("--sheet") + 1] if "--sheet" in argv else None      # las 5 piezas de frente, para comparar con la lamina
THEME = "tema"
PAL = {}
NAMES = []
MATS = {}
RIVET = None        # material de los remaches y canos (lo fija setup)
sc = None
parts = []

W, H, T = 1.7, 1.25, 0.08          # pared: ancho, alto, grosor (el frente es la cara -Y)
FY = -T / 2                        # cara de afuera
DW, DH = 0.9, 0.93                 # hueco de la puerta
SIDES = [(Vector((0, -1, 0)), Vector((1, 0, 0))), (Vector((1, 0, 0)), Vector((0, 1, 0))),
         (Vector((0, 1, 0)), Vector((-1, 0, 0))), (Vector((-1, 0, 0)), Vector((0, -1, 0)))]


def hexc(h, k=0.75):
    """Color de la lamina (#rrggbb, sRGB) oscurecido por k (el toon del juego aclara ~25 %), en lineal para Blender."""
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 * k for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


GLOW_KINDS = ("late", "titila", "respira", "led")   # columna de la fila emisiva = tipo de latido (MinerToonTex)
EMIS_ROWS = 0
ANCHORS = []          # anclas de la pieza que se esta armando: (nombre, punto, direccion)
PIECE_ANCHORS = {}


def setup(theme, pal, emissive=None, rivet=None, glow=None):
    """Escena vacia y un material por color de la paleta (emissive: {nombre: fuerza}, solo para la vista previa).
    glow: {nombre: tipo} con tipo en GLOW_KINDS. Esos colores van a las filas de ABAJO de la paleta, en la columna de su
    tipo, y el shader del juego los hace latir (0 costo de CPU). Las demas filas se completan con relleno."""
    global THEME, PAL, NAMES, sc, RIVET, EMIS_ROWS
    THEME, PAL = theme, dict(pal)
    glow = glow or {}
    plain = [k for k in pal if k not in glow]
    while len(plain) % NCOL: plain.append(None)
    rows = []
    for k, kind in glow.items():
        col = GLOW_KINDS.index(kind)
        for r in rows:
            if r[col] is None: r[col] = k; break
        else:
            r = [None] * NCOL; r[col] = k; rows.append(r)
    EMIS_ROWS = len(rows)
    NAMES = plain + [k for r in reversed(rows) for k in r]     # la ultima fila de la textura es la de mas abajo
    RIVET = rivet or plain[0]
    ANCHORS.clear(); PIECE_ANCHORS.clear()
    os.makedirs(OUTDIR, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    MATS.clear()
    emissive = dict(emissive or {})
    for k in glow: emissive.setdefault(k, 2.0)
    for k, rgb in PAL.items():
        m = bpy.data.materials.new(k); m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = 0.45
        if k in emissive:
            b.inputs["Emission Color"].default_value = (*rgb, 1); b.inputs["Emission Strength"].default_value = emissive[k]
        MATS[k] = m
    for k, hx in list(SLATE.items()) + [("madera", "#7a4a26")]:   # para la vista previa con el techo comun (no van a la paleta)
        if k in MATS: continue
        m = bpy.data.materials.new(k); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*hexc(hx), 1)
        MATS[k] = m


def anchor(name, p, d=(0, -1, 0)):
    """Ancla para animar en el juego (vapor, aguja, cristal...): punto y direccion en la pieza (se exportan en el JSON)."""
    ANCHORS.append((name, Vector(p), Vector(d).normalized()))


def add(o, m, bevel=0.0):
    o.data.materials.append(MATS[m])
    if bevel > 0:
        b = o.modifiers.new("bv", "BEVEL"); b.width = bevel; b.segments = 1; b.limit_method = "ANGLE"
    parts.append(o); return o
def link(me, name, m):
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o); return add(o, m)
def box(c, s, m, bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=c); o = bpy.context.active_object
    o.scale = s; bpy.ops.object.transform_apply(scale=True); return add(o, m, bevel)
def cyl(c, r, d, m, axis="Z", v=14, bevel=0.0):
    rot = {"Z": (0, 0, 0), "X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0)}[axis]
    bpy.ops.mesh.primitive_cylinder_add(vertices=v, radius=r, depth=d, location=c, rotation=rot)
    return add(bpy.context.active_object, m, bevel)
def cone(c, r1, r2, d, m, axis="Z", v=12):
    rot = {"Z": (0, 0, 0), "X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0)}[axis]
    bpy.ops.mesh.primitive_cone_add(vertices=v, radius1=r1, radius2=r2, depth=d, location=c, rotation=rot)
    return add(bpy.context.active_object, m)
def sphere(c, r, m, seg=10, rings=6, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r, location=c)
    o = bpy.context.active_object; o.scale = scale; bpy.ops.object.transform_apply(scale=True); return add(o, m)
def dome(c, r, m, seg=12, rings=4, h=1.0):
    """Media esfera (cupula) apoyada en c, alto r*h."""
    bm = bmesh.new()
    pts = []
    for j in range(rings + 1):
        a = (math.pi / 2) * j / rings
        pts.append((math.cos(a) * r, math.sin(a) * r * h))
    rows = []
    for j, (rr, zz) in enumerate(pts):
        if j == rings: rows.append([bm.verts.new(Vector(c) + Vector((0, 0, zz)))]); break
        rows.append([bm.verts.new(Vector(c) + Vector((math.cos(2 * math.pi * i / seg) * rr, math.sin(2 * math.pi * i / seg) * rr, zz))) for i in range(seg)])
    for j in range(len(rows) - 1):
        a, b = rows[j], rows[j + 1]
        for i in range(seg):
            if len(b) == 1: bm.faces.new([a[i], a[(i + 1) % seg], b[0]])
            else: bm.faces.new([a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]])
    bm.faces.new(list(reversed(rows[0])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("cupula"); bm.to_mesh(me); bm.free()
    return link(me, "cupula", m)
def gem(c, r, m, flat=0.45, tall=1.25):
    """Rombo facetado (octaedro aplastado) que sobresale hacia -Y."""
    bm = bmesh.new()
    p = [Vector((r, 0, 0)), Vector((-r, 0, 0)), Vector((0, 0, r * tall)), Vector((0, 0, -r * tall)), Vector((0, -r * flat, 0)), Vector((0, r * 0.1, 0))]
    vs = [bm.verts.new(q + Vector(c)) for q in p]
    for f in [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]: bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("gema"); bm.to_mesh(me); bm.free()
    return link(me, "gema", m)
def diamond(c, s, d, m, bevel=0.01):
    """Placa en rombo (cuadrado girado 45 grados) de lado s y grosor d, de frente a -Y."""
    o = box(c, (s, d, s), m, bevel); c = Vector(c)   # box deja la malla en el mundo: girar alrededor del centro
    o.data.transform(Matrix.Translation(c) @ Matrix.Rotation(math.pi / 4, 4, "Y") @ Matrix.Translation(-c)); return o
def coin(c, r, m, standing=False, th=0.022):
    """Moneda: acostada (eje Z) o parada de frente (eje Y)."""
    return cyl(c, r, th, m, "Y" if standing else "Z", 8)
def ingot(c, l, w, h, m, rotz=0.0):
    """Lingote: tronco de piramide (base l x w, tapa mas chica)."""
    bm = bmesh.new(); k = 0.72
    q = [(-l / 2, -w / 2, 0), (l / 2, -w / 2, 0), (l / 2, w / 2, 0), (-l / 2, w / 2, 0)]
    rz = Matrix.Rotation(rotz, 3, "Z")
    vs = [bm.verts.new(rz @ Vector(p) + Vector(c)) for p in q] + [bm.verts.new(rz @ Vector((p[0] * k, p[1] * k, h)) + Vector(c)) for p in q]
    for f in [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]: bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("lingote"); bm.to_mesh(me); bm.free()
    return link(me, "lingote", m)
def crystal(c, r, h, m, tilt=0.0, lean=0.0, sides=6):
    """Cristal: prisma de `sides` caras con punta, apoyado en c, alto h; tilt gira hacia los costados (X), lean hacia
    el frente (-Y)."""
    bm = bmesh.new()
    rot = Matrix.Rotation(lean, 3, "X") @ Matrix.Rotation(tilt, 3, "Y")
    ring0 = [bm.verts.new(Vector(c) + rot @ Vector((math.cos(2 * math.pi * i / sides) * r, math.sin(2 * math.pi * i / sides) * r, 0))) for i in range(sides)]
    ring1 = [bm.verts.new(Vector(c) + rot @ Vector((math.cos(2 * math.pi * i / sides) * r, math.sin(2 * math.pi * i / sides) * r, h * 0.72))) for i in range(sides)]
    tip = bm.verts.new(Vector(c) + rot @ Vector((0, 0, h)))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new([ring0[i], ring0[j], ring1[j], ring1[i]]); bm.faces.new([ring1[i], ring1[j], tip])
    bm.faces.new(list(reversed(ring0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("cristal"); bm.to_mesh(me); bm.free()
    return link(me, "cristal", m)
def cluster(c, s, m_a, m_b, lean=0.35):
    """Racimo de cristales: uno alto al centro y dos o tres chicos inclinados."""
    x, y, z = c
    crystal((x, y, z), 0.045 * s, 0.32 * s, m_a, 0.0, lean * 0.5)
    crystal((x - 0.07 * s, y, z), 0.032 * s, 0.2 * s, m_b, -0.5, lean)
    crystal((x + 0.07 * s, y, z), 0.03 * s, 0.17 * s, m_b, 0.55, lean)
def rivet(c, r=0.022, m=None):
    bpy.ops.mesh.primitive_cube_add(size=r * 1.8, location=c, rotation=(0, 0, math.pi / 4))
    return add(bpy.context.active_object, m or RIVET)
def ring(c, R, r, m):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=14, minor_segments=5, location=c, rotation=(math.pi / 2, 0, 0))
    return add(bpy.context.active_object, m)
def beam(p0, p1, w, h, m, up=(0, 0, 1), top=1.0):
    """Viga de p0 a p1, ancho w y alto h; top < 1 angosta la cara de arriba (teja redondeada, con el mismo costo)."""
    p0, p1 = Vector(p0), Vector(p1); d = (p1 - p0).normalized()
    side = d.cross(Vector(up)).normalized(); nrm = side.cross(d).normalized()
    bm = bmesh.new(); vv = []
    for p in (p0, p1):
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vv.append(bm.verts.new(p + side * (sx * w / 2 * (top if sy > 0 else 1.0)) + nrm * (sy * h / 2)))
    for f in [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]: bm.faces.new([vv[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("viga"); bm.to_mesh(me); bm.free()
    return link(me, "viga", m)
def pipe(pts, r=0.045, m=None):
    pts = [Vector(p) for p in pts]
    for a, b in zip(pts, pts[1:]):
        d = b - a; o = cyl((a + b) / 2, r, d.length, m or RIVET, v=8)
        o.rotation_euler = d.normalized().to_track_quat("Z", "Y").to_euler()
    for p in pts[1:-1]:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=r * 1.2, location=p); add(bpy.context.active_object, m or RIVET)
def arch_band(cx, zb, ri, ro, y0, y1, m, seg=12, squash=1.0):
    """Media corona (arco) en el plano XZ, centro (cx, zb), radios ri..ro, entre y0 (afuera) e y1."""
    bm = bmesh.new()
    for k in range(seg):
        a0, a1 = math.pi * k / seg, math.pi * (k + 1) / seg
        q = [Vector((cx + math.cos(a) * r, 0, zb + math.sin(a) * r * squash)) for a, r in ((a0, ri), (a1, ri), (a1, ro), (a0, ro))]
        vs = [bm.verts.new(p + Vector((0, y0, 0))) for p in q] + [bm.verts.new(p + Vector((0, y1, 0))) for p in q]
        for f in [(0, 1, 2, 3), (7, 6, 5, 4), (3, 2, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (0, 3, 7, 4)]: bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("arco"); bm.to_mesh(me); bm.free()
    return link(me, "arco", m)
def half_disc(cx, zb, r, y0, y1, m, seg=12, squash=1.0):
    """Medio disco macizo (el relleno de un arco: vidrio, tablas) entre y0 e y1."""
    bm = bmesh.new()
    pts = [Vector((cx + math.cos(math.pi * k / seg) * r, 0, zb + math.sin(math.pi * k / seg) * r * squash)) for k in range(seg + 1)]
    f0 = [bm.verts.new(p + Vector((0, y0, 0))) for p in pts]; f1 = [bm.verts.new(p + Vector((0, y1, 0))) for p in pts]
    bm.faces.new(f0); bm.faces.new(list(reversed(f1)))
    for i in range(seg): bm.faces.new([f0[i], f1[i], f1[i + 1], f0[i + 1]])
    bm.faces.new([f0[-1], f1[-1], f1[0], f0[0]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("medio"); bm.to_mesh(me); bm.free()
    return link(me, "medio", m)
def arch_window(cx, z0, w, hs, y, glass, frame, fw=0.07, depth=0.06, mull=None):
    """Ventana de arco de medio punto: vidrio (rect + medio disco), marco alrededor y parteluces opcionales en cruz."""
    r = w / 2
    box((cx, y + 0.005, z0 + hs / 2), (w, 0.02, hs), glass, 0)
    half_disc(cx, z0 + hs, r, y - 0.005, y + 0.015, glass)
    for s in (-1, 1): box((cx + s * (r + fw / 2), y - depth / 2, z0 + hs / 2), (fw, depth, hs + 0.01), frame, 0.01)
    box((cx, y - depth / 2, z0 - fw / 2), (w + fw * 2 + 0.04, depth + 0.01, fw), frame, 0.01)
    arch_band(cx, z0 + hs, r, r + fw, y - depth, y, frame)
    if mull:
        box((cx, y - 0.02, z0 + (hs + r) / 2), (0.03, 0.025, hs + r - 0.02), mull, 0)
        box((cx, y - 0.02, z0 + hs * 0.62), (w, 0.025, 0.03), mull, 0)
def hip_roof(a, B, z0, z1, m):
    """Faldones a cuatro aguas: base cuadrada de medio lado a en z0, cumbre de medio lado B en z1."""
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in [(-a, -a, z0), (a, -a, z0), (a, a, z0), (-a, a, z0), (-B, -B, z1), (B, -B, z1), (B, B, z1), (-B, B, z1)]]
    for f in [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)]: bm.faces.new([vs[i] for i in f])
    me = bpy.data.meshes.new("faldones"); bm.to_mesh(me); bm.free()
    return link(me, "faldones", m)
def on_roof(a, B, z0, z1, n, t, u, v, lift):
    """Punto sobre el faldon de normal horizontal n: u 0 (alero) .. 1 (cumbre), v -1..1 a lo ancho."""
    r = a + (B - a) * u
    return n * r + t * (v * r) + Vector((0, 0, z0 + (z1 - z0) * u + lift))
def roof_up(a, B, z0, z1, n):
    return (n * (z1 - z0) / (a - B) + Vector((0, 0, 1))).normalized()
def tiles(a, B, z0, z1, m_a, m_b, rows=5, cols=6, lift=0.02, th=0.035, top=0.55):
    """Tejas sobre los 4 faldones: filas trabadas (media teja corrida una si y otra no), cada una un poco encima de la de
    abajo, tono alternado por fila; perfil de trapecio (top) para que el toon las lea redondeadas."""
    for n, t in SIDES:
        up = roof_up(a, B, z0, z1, n)
        for j in range(rows):
            u0, u1 = j / rows, (j + 1) / rows
            rr = a + (B - a) * (u0 + (u1 - u0) * 0.7)          # ancho de la fila cerca de su borde de arriba: no asoma del lima
            nn = max(2, int(round(cols * rr / a)))
            tw = 2 * rr / nn
            off = 0.5 * tw if j % 2 else 0.0
            for i in range(nn + (1 if j % 2 else 0)):
                x = -rr + tw * (i + 0.5) - off
                w = tw
                lo, hi = max(x - tw / 2, -rr), min(x + tw / 2, rr)
                if hi - lo < tw * 0.3: continue
                x, w = (lo + hi) / 2, hi - lo
                lj = lift + th * 0.5 * (j % 2)
                p0 = on_roof(a, B, z0, z1, n, t, u0, 0, lj) + t * x
                p1 = on_roof(a, B, z0, z1, n, t, min(u1 + 0.05, 1.0), 0, lj) + t * x
                beam(p0, p1, w * 0.9, th, m_a if j % 2 else m_b, up=up, top=top)


# ------------------------------------------------------------------ techo comun por variantes (techo continuo)
RA, RE, Ra, RB, Rz0, Rz1 = 1.02, 1.0, 0.95, 0.08, 0.14, 0.62   # alero, borde de la celda, base, meseta, alturas
SIDE_BIT = {(0, 1): 1, (1, 0): 2, (0, -1): 4, (-1, 0): 8}      # lados del juego: N=+Y, E=+X, S=-Y, O=-X


def _open(mask, v):
    return bool(mask & SIDE_BIT[(int(round(v.x)), int(round(v.y)))])


def face_tiles(n, t, lo_b, hi_b, lo_p, hi_p, ma, mb, rows=5, tw=0.4, th=0.045, lift=0.02):
    """Tejas de un faldon: filas trabadas sobre una grilla GLOBAL de ancho tw (2.0 / 5): las de la habitacion vecina
    siguen en la misma linea. lo/hi: limites a lo largo de t en la base (b) y en la meseta (p)."""
    up = roof_up(Ra, RB, Rz0, Rz1, n)
    for j in range(rows):
        u0, u1 = j / rows, (j + 1) / rows
        uc = u0 + (u1 - u0) * 0.7
        lo, hi = lo_b + (lo_p - lo_b) * uc, hi_b + (hi_p - hi_b) * uc
        off = tw / 2 if j % 2 else 0.0
        k0 = int(math.floor((-RE - tw - off) / tw))
        for k in range(k0, k0 + int(2 * RE / tw) + 4):
            x = k * tw + off
            a_, b_ = max(x - tw / 2, lo), min(x + tw / 2, hi)
            if b_ - a_ < tw * 0.3: continue
            xm = (a_ + b_) / 2
            lj = lift + th * 0.5 * (j % 2)
            def P(u):
                return n * (Ra + (RB - Ra) * u) + t * xm + Vector((0, 0, Rz0 + (Rz1 - Rz0) * u + lj))
            beam(P(u0), P(min(u1 + 0.05, 1.0)), (b_ - a_) * 0.9, th, ma if j % 2 else mb, up=up, top=0.85)


VALLEY_BIT = {(1, 1): 16, (1, -1): 32, (-1, -1): 64, (-1, 1): 128}   # limahoyas: NE, SE, SO, NO


def _valley(mask, sx, sy):
    """Esquina interior de una L: los dos lados techados y la diagonal no (bit 16/32/64/128)."""
    return bool(mask & VALLEY_BIT[(sx, sy)])


def roof_valid(mask):
    """Una limahoya solo existe si los dos lados de esa esquina estan techados."""
    for (sx, sy), b in VALLEY_BIT.items():
        if mask & b and not (_open(mask, Vector((sx, 0, 0))) and _open(mask, Vector((0, sy, 0)))): return False
    return True


def roof_variant(mask, cap="hierro", wood="madera", wood_b=None):
    """Techo comun (pizarra gris-azul, aleros y limas de madera) de una habitacion segun sus vecinas techadas: hacia
    cada lado de `mask` (bits N=1, E=2, S=4, O=8) el techo NO baja: sigue a la altura de la cumbrera hasta el borde de
    la celda y empalma con el de la vecina. Asi varias habitaciones se leen como UN techo, no cubos pegados.
    Bits 16..128 (NE, SE, SO, NO): esquina interior de una L sin habitacion en la diagonal. Ahi la meseta no sigue
    plana (quedaba un escalon contra los faldones de las vecinas): baja en LIMAHOYA, la altura es el maximo de los dos
    faldones vecinos, con una viga de madera en el valle.
    Origen = centro de la celda a la altura del borde de la pared (el juego lo ubica sin estirar: escala 1)."""
    wood_b = wood_b or wood
    X, Y = Vector((1, 0, 0)), Vector((0, 1, 0))
    def ext(v, closed_val):
        return RE if _open(mask, v) else closed_val
    def fz(s):   # altura del faldon a distancia s del centro (s >= RB): baja de Rz1 en RB a Rz0 en Ra, y sigue
        return Rz1 - (s - RB) / (Ra - RB) * (Rz1 - Rz0)
    # meseta: hasta el borde de la celda del lado abierto, +-B del cerrado; partida en tramos para las limahoyas
    x0, x1, y0, y1 = -ext(-X, RB), ext(X, RB), -ext(-Y, RB), ext(Y, RB)
    xs = [v for v in (x0, -RB, RB, x1) if x0 - 1e-6 <= v <= x1 + 1e-6]
    ys = [v for v in (y0, -RB, RB, y1) if y0 - 1e-6 <= v <= y1 + 1e-6]
    xs = sorted(set(round(v, 6) for v in xs)); ys = sorted(set(round(v, 6) for v in ys))
    bm = bmesh.new()
    valleys = []
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            a0, a1, b0, b1 = xs[i], xs[i + 1], ys[j], ys[j + 1]
            sx = 1 if a0 >= RB - 1e-6 else (-1 if a1 <= -RB + 1e-6 else 0)
            sy = 1 if b0 >= RB - 1e-6 else (-1 if b1 <= -RB + 1e-6 else 0)
            if sx and sy and _valley(mask, sx, sy):
                # cuadrado de la esquina: dos triangulos con la diagonal como valle
                cx, cy = sx * RB, sy * RB          # esquina de la meseta (alto)
                fx, fy = sx * RE, sy * RE          # esquina de la celda (bajo)
                pc = Vector((cx, cy, Rz1))
                pxe = Vector((fx, cy, Rz1))        # sobre el borde de la vecina del lado x: meseta de ella
                pye = Vector((cx, fy, Rz1))
                pf = Vector((fx, fy, fz(RE)))
                for tri in ((pc, pxe, pf), (pc, pf, pye)):
                    bm.faces.new([bm.verts.new(v) for v in tri])
                valleys.append((pc, pf, sx, sy))
                continue
            q = [bm.verts.new(Vector((x, y, Rz1))) for x, y in ((a0, b0), (a1, b0), (a1, b1), (a0, b1))]
            bm.faces.new(q)
    for n, t in SIDES:
        lo_b, hi_b = -ext(-t, Ra), ext(t, Ra)
        lo_p, hi_p = -ext(-t, RB), ext(t, RB)
        if _open(mask, n):
            # lado abierto: tapa vertical (queda escondida contra la de la vecina; sella si no calzan). Hacia una
            # limahoya la tapa baja con el faldon de la vecina (si no, asomaba un triangulo sobre el valle)
            def vxy(v): return (int(round(v.x)), int(round(v.y)))
            nn = vxy(n)
            def corner(tt):
                tv = vxy(tt); return _valley(mask, nn[0] + tv[0], nn[1] + tv[1]) if (nn[0] + tv[0]) and (nn[1] + tv[1]) else False
            hp = RB if corner(t) else hi_p
            lp = RB if corner(-t) else -lo_p
            vs = [bm.verts.new(n * RE + t * lo_b + Vector((0, 0, Rz0))), bm.verts.new(n * RE + t * hi_b + Vector((0, 0, Rz0))),
                  bm.verts.new(n * RE + t * hp + Vector((0, 0, Rz1))), bm.verts.new(n * RE + t * -lp + Vector((0, 0, Rz1)))]
            bm.faces.new(vs)
            continue
        vs = [bm.verts.new(n * Ra + t * lo_b + Vector((0, 0, Rz0))), bm.verts.new(n * Ra + t * hi_b + Vector((0, 0, Rz0))),
              bm.verts.new(n * RB + t * hi_p + Vector((0, 0, Rz1))), bm.verts.new(n * RB + t * lo_p + Vector((0, 0, Rz1)))]
        bm.faces.new(vs)
        face_tiles(n, t, lo_b, hi_b, lo_p, hi_p, "pizarra_a", "pizarra_b")
        # alero de madera de este lado (llega al borde de la celda donde sigue la vecina)
        bl, bh = (-RE if _open(mask, -t) else -RA), (RE if _open(mask, t) else RA)
        c = n * (RA - 0.06) + t * ((bl + bh) / 2) + Vector((0, 0, 0.1))
        L = bh - bl
        box(c, (abs(t.x) * L + 0.12 * abs(n.x), abs(t.y) * L + 0.12 * abs(n.y), 0.12), wood, 0.012)
        box(c + n * 0.062 + Vector((0, 0, -0.01)), (abs(t.x) * L * 0.98 + 0.004, abs(t.y) * L * 0.98 + 0.004, 0.006), wood_b, 0)
        # cumbrera de madera sobre el borde de la meseta
        beam(n * RB + t * lo_p + Vector((0, 0, Rz1 + 0.02)), n * RB + t * hi_p + Vector((0, 0, Rz1 + 0.02)), 0.08, 0.05, wood, up=n * 0.3 + Vector((0, 0, 1)))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for f in bm.faces:   # que miren para afuera/arriba (la meseta hacia +Z)
        if f.normal.z < -0.5: f.normal_flip()
    me = bpy.data.meshes.new("faldones"); bm.to_mesh(me); bm.free()
    link(me, "faldones", "pizarra_b")
    # limas y esquineros solo en las esquinas con los dos lados cerrados
    for sx in (-1, 1):
        for sy in (-1, 1):
            if _open(mask, X * sx) or _open(mask, Y * sy): continue
            beam((sx * (Ra + 0.02), sy * (Ra + 0.02), Rz0 + 0.03), (sx * RB, sy * RB, Rz1 + 0.03), 0.13, 0.08, wood)
            box((sx * (RA - 0.06), sy * (RA - 0.06), 0.1), (0.16, 0.16, 0.15), cap, 0)
    # limahoyas: tejas en los dos triangulos (cada uno sigue el faldon de su vecina, misma grilla global) y viga en el valle
    for pc, pf, sx, sy in valleys:
        for n, other in ((Vector((0, sy, 0)), Vector((sx, 0, 0))), (Vector((sx, 0, 0)), Vector((0, sy, 0)))):
            t = next(tt for nn, tt in SIDES if (nn - n).length < 1e-6)
            def tc(d): return (other * d).dot(t)
            lo_p, hi_p = sorted((tc(RB), tc(RE)))
            lo_b, hi_b = sorted((tc(Ra), tc(RE)))
            face_tiles(n, t, lo_b, hi_b, lo_p, hi_p, "pizarra_a", "pizarra_b")
        beam(pc + Vector((0, 0, 0.025)), pf + Vector((0, 0, 0.025)), 0.1, 0.05, wood)


def masonry(x0, x1, z0, z1, seed, mats, skip=None, course=0.17, lens=(0.16, 0.22, 0.28, 0.34), depth=(0.022, 0.034)):
    """Sillares o ladrillos en relieve (chaflan al frente) en hiladas parejas y largos al azar; skip(x, z, L) saltea."""
    import random
    rnd = random.Random(seed)
    n = max(2, int(round((z1 - z0) / course)))
    hh = (z1 - z0) / n
    for j in range(n):
        z = z0 + hh * (j + 0.5)
        x = x0
        while x < x1 - 0.02:
            L = min(rnd.choice(lens), x1 - x)
            if x1 - (x + L) < 0.08: L = x1 - x
            if not (skip and skip(x + L / 2, z, L)):
                d = rnd.uniform(*depth)
                beam((x + 0.012, FY - d / 2 + 0.004, z), (x + L - 0.012, FY - d / 2 + 0.004, z), hh - 0.022, d, rnd.choice(mats), up=(0, -1, 0), top=0.82)
            x += L


def door_cut_walls(m):
    """Pared con el hueco de la puerta (dos laterales y el dintel)."""
    side = (W - DW) / 2
    for s in (-1, 1): box((s * (DW / 2 + side / 2), 0, H / 2), (side, T, H), m, 0)
    box((0, 0, DH + (H - DH) / 2), (DW, T, H - DH), m, 0)


def join(piece):
    global parts
    for o in parts:
        bpy.context.view_layer.objects.active = o
        for mm in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=mm.name)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.active_object; ob.name = THEME + "_" + piece
    PIECE_ANCHORS[ob.name] = list(ANCHORS); ANCHORS.clear()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # vertices en el mundo (pivote = origen)
    parts = []
    return ob

# ------------------------------------------------------------------ textura de paleta y exportacion al formato del juego
def tex_size():
    """Ancho y alto de la paleta (se calcula con la paleta del tema, despues de setup)."""
    return CELL * NCOL, CELL * ((len(NAMES) + NCOL - 1) // NCOL)
def cell_uv(k):
    TEXW, TEXH = tex_size()
    i = NAMES.index(k); cx, cy = i % NCOL, i // NCOL
    return ((cx + 0.5) * CELL / TEXW, 1 - (cy + 0.5) * CELL / TEXH)
def save_palette(path):
    TEXW, TEXH = tex_size()
    img = bpy.data.images.new("pal", TEXW, TEXH, alpha=False)
    px = [0.0] * (TEXW * TEXH * 4)
    for i, k in enumerate(NAMES):
        cx, cy = i % NCOL, i // NCOL
        r, g, b = PAL[k] if k is not None else PAL[NAMES[0]]
        srgb = [((1.055 * c ** (1 / 2.4) - 0.055) if c > 0.0031308 else c * 12.92) for c in (r, g, b)]
        for y in range(CELL):
            for x in range(CELL):
                X = cx * CELL + x; Y = TEXH - 1 - (cy * CELL + y)
                j = (Y * TEXW + X) * 4; px[j:j + 4] = [*srgb, 1.0]
    img.pixels = px; img.filepath_raw = path; img.file_format = "PNG"; img.save()

def export(ob, name):
    me = ob.data
    me.calc_loop_triangles()
    pos, nrm, uv, idx, weld = [], [], [], [], {}
    for tri in me.loop_triangles:
        n = tri.normal; u = cell_uv(me.materials[tri.material_index].name.split(".")[0])
        for li in reversed(tri.loops):   # el cambio de ejes refleja: invertir el orden (como tripo_building.py)
            vi = me.loops[li].vertex_index
            key = (vi, tri.material_index, round(n.x, 3), round(n.y, 3), round(n.z, 3))
            j = weld.get(key)
            if j is None:
                j = len(pos) // 3; weld[key] = j
                c = me.vertices[vi].co
                pos += [round(-c.x, 4), round(c.z, 4), round(-c.y, 4)]
                nrm += [round(-n.x, 3), round(n.z, 3), round(-n.y, 3)]
                uv += [round(u[0], 4), round(u[1], 4)]
            idx.append(j)
    zs = [v.co.z for v in me.vertices]
    with open(os.path.join(OUTDIR, name + ".json"), "w") as f:
        anc = [{"n": n, "p": [round(-q.x, 4), round(q.z, 4), round(-q.y, 4)], "d": [round(-d.x, 3), round(d.z, 3), round(-d.y, 3)]}
               for n, q, d in PIECE_ANCHORS.get(ob.name, [])]
        json.dump({"pos": pos, "nrm": nrm, "uv": uv, "idx": idx, "height": max(zs) - min(zs), "emis": EMIS_ROWS, "anchors": anc}, f, separators=(",", ":"))
    save_palette(os.path.join(OUTDIR, name + ".png"))
    print("PIEZA", name, "tris", len(idx) // 3, "anclas", len(PIECE_ANCHORS.get(ob.name, [])))


def run(build, preview_mats=("borde", "borde")):
    """Arma y exporta las 5 piezas; con --preview, renderiza el modulo armado (piso y columnas con preview_mats)."""
    pieces = {}
    for p in ("pared", "ventana", "marco", "hoja", "remate"):
        build(p); ob = join(p); export(ob, THEME + "_" + p); pieces[p] = ob
    # para las vistas previas: el techo comun cerrado (el juego lo pone de Resources/RoomKit/comun_techo_<mascara>)
    roof_variant(0, cap=RIVET); pieces["techo"] = join("techo_comun")
    if SHEET: sheet(pieces)
    if PREVIEW: preview(pieces, preview_mats)
    return pieces


def lights_and_render(path, ortho, loc, rot, rx, ry):
    if sc.world is None:
        world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.93, 0.85, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 0.22
        l = bpy.data.lights.new("Sol", "SUN"); l.energy = 3.0; lo = bpy.data.objects.new("Sol", l); sc.collection.objects.link(lo)
        lo.rotation_euler = (math.radians(50), 0, math.radians(35))
    cd = bpy.data.cameras.new("C"); cd.type = "ORTHO"; cd.ortho_scale = ortho
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = loc; cam.rotation_euler = rot
    sc.render.engine = "CYCLES"; sc.cycles.samples = 32; sc.cycles.use_denoising = True
    try:
        pr = bpy.context.preferences.addons["cycles"].preferences; pr.compute_device_type = "CUDA"; pr.get_devices()
        for dv in pr.devices: dv.use = True
        sc.cycles.device = "GPU"
    except Exception: pass
    sc.render.resolution_x, sc.render.resolution_y = rx, ry; sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)


def sheet(pieces):
    """Las piezas de frente en fila (pared, ventana, marco con la hoja, techo en tres cuartos), para la lamina."""
    made = []
    def place(src, loc, rot=(0, 0, 0)):
        o = src.copy(); o.data = src.data.copy(); sc.collection.objects.link(o); o.location = loc; o.rotation_euler = rot
        o.hide_render = False; made.append(o); return o
    for p in pieces.values(): p.hide_render = True
    place(pieces["pared"], (-3.9, 0, 0)); place(pieces["ventana"], (-1.95, 0, 0)); place(pieces["marco"], (0, 0, 0))
    place(pieces["hoja"], (-0.43, -0.03, 0)); place(pieces["techo"], (2.3, 0.6, 0.15), (0, 0, math.radians(30))); place(pieces["remate"], (2.3, 0.6, 0.15), (0, 0, math.radians(30)))
    lights_and_render(SHEET, 8.4, Vector((-0.5, -20, 0.8 + 20 * math.tan(math.radians(12)))), (math.radians(78), 0, 0), 1800, 560)
    for o in made: bpy.data.objects.remove(o)


def preview(pieces, mats):
    MW = 1.8; SL = 0.16
    def place(src, loc, rotz):
        o = src.copy(); o.data = src.data.copy(); sc.collection.objects.link(o)
        o.location = loc; o.rotation_euler = (0, 0, rotz); o.hide_render = False; return o
    for p in pieces.values(): p.hide_render = True
    place(pieces["marco"], (0, -MW / 2 + 0.06, SL), 0)
    place(pieces["hoja"], (-0.43, -MW / 2 + 0.06 - 0.05, SL), 0)
    place(pieces["ventana"], (MW / 2 - 0.06, 0, SL), math.pi / 2)
    place(pieces["pared"], (0, MW / 2 - 0.06, SL), math.pi)
    place(pieces["pared"], (-MW / 2 + 0.06, 0, SL), -math.pi / 2)
    place(pieces["techo"], (0, 0, SL + H), 0); place(pieces["remate"], (0, 0, SL + H), 0)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, SL / 2)); fl = bpy.context.active_object; fl.scale = (MW + 0.1, MW + 0.1, SL)
    fl.data.materials.append(MATS[mats[0]])
    for sx in (-1, 1):
        for sy in (-1, 1):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(sx * (MW / 2 - 0.06), sy * (MW / 2 - 0.06), SL + H / 2)); c = bpy.context.active_object
            c.scale = (0.14, 0.14, H); c.data.materials.append(MATS[mats[1]])
    world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 0.93, 0.85, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 0.22
    l = bpy.data.lights.new("Sol", "SUN"); l.energy = 3.0; lo = bpy.data.objects.new("Sol", l); sc.collection.objects.link(lo)
    lo.rotation_euler = (math.radians(50), 0, math.radians(35))
    cd = bpy.data.cameras.new("C"); cd.type = "ORTHO"; cd.ortho_scale = 3.6
    cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
    d = Vector((math.sin(math.radians(35)) * math.cos(math.radians(48)), -math.cos(math.radians(35)) * math.cos(math.radians(48)), math.sin(math.radians(48))))
    cam.location = Vector((0, 0, 1.0)) + d * 20; cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.render.engine = "CYCLES"; sc.cycles.samples = 48; sc.cycles.use_denoising = True
    try:
        pr = bpy.context.preferences.addons["cycles"].preferences; pr.compute_device_type = "CUDA"; pr.get_devices()
        for dv in pr.devices: dv.use = True
        sc.cycles.device = "GPU"
    except Exception: pass
    sc.render.resolution_x = sc.render.resolution_y = 800; sc.render.film_transparent = True
    sc.view_settings.view_transform = "Standard"
    sc.render.filepath = PREVIEW; bpy.ops.render.render(write_still=True)
