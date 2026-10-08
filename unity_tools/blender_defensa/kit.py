"""Fortin de Juguete: kit comun de Blender (todo el arte del juego sale de aca, por codigo).

- Primitivas gorditas: esferas, capsulas, cajas redondeadas (bisel), tubos, toros, conos, estrellas, mallas a mano.
- Paleta UNICA de 256x256 (franjas de color con degrade + un bloque de vetas de madera) compartida por todo el juego:
  casi todo se dibuja con el mismo material (pocas llamadas). Ver PALETTE / MATS.
- Esqueleto simple de "huesos" (Empties en jerarquia: raiz, cadera, pecho, cabeza, brazos, manos, piernas). Cada pieza
  queda pegada al 100% a un hueso (son juguetes: las articulaciones son literales).
- Animaciones por fotogramas clave en el script (clave(hueso, cuadro, pos/rot/esc)) muestreadas a 30 cps.
- Exportacion al formato JSON propio que lee el juego (Fortin.View.ToyModel):
    modelo:  {"bones":[{n,p,t,r,s}], "meshes":[{b,pos,nrm,uv,col,idx}], "height", "tris"}
    anims:   {"fps":30, "bones":[...], "clips":[{n,len,loop,ev,k:[...]}]}
  Ejes: Blender (x, y, z) -> Unity (x, z, y). El frente del modelo (-Y en Blender) mira a -Z en Unity (hacia la camara).
- Color de vertice = banderas del material para el sombreado toon del juego:
    R = brillo (plastico 0.3, metal 1.0)   G = cristal arcoiris   B = emision   A = 1
Uso: se importa desde heroes.py, enemigos.py, tableros.py, utileria.py e iconos.py.
"""
import bpy, bmesh, math, os, json, random
from contextlib import contextmanager
from mathutils import Vector, Matrix, Euler, Quaternion

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
UNITY = os.path.join(ROOT, "unity_defensa")
OUT_MODELS = os.path.join(UNITY, "Assets", "Resources", "Models")
OUT_DOCS = os.path.join(UNITY, "docs")
FPS = 30

# ------------------------------------------------------------------ paleta (orden FIJO: cambia la textura de todos)
# Paleta base del juego: noche 2a2350, violeta 7b5cff, magenta ff5ab4, turquesa 3fc7b4, dorado ffc93a, crema fff4dc,
# lima a8ff3e, contorno 241a3d. Las sombras son violetas: ningun color baja de ~0x18 en todos los canales.
PALETTE = [
    ("noche", "2a2350"), ("violeta", "7b5cff"), ("magenta", "ff5ab4"), ("turquesa", "3fc7b4"),
    ("dorado", "ffc93a"), ("crema", "fff4dc"), ("lima", "a8ff3e"), ("contorno", "241a3d"),
    ("piel", "ffd9b8"), ("mejilla", "ff8fa8"), ("blanco", "fbf7ff"), ("ojo", "2a1f45"),
    ("rojo", "ee3b4a"), ("rojo_osc", "a51e3a"), ("azul", "3a8dff"), ("azul_osc", "2a4fb8"),
    ("verde", "4cc35a"), ("verde_osc", "2c8a4a"), ("amarillo", "ffd83a"), ("naranja", "ff9a3a"),
    ("violeta_osc", "4a2fa8"), ("lila", "b9a2ff"), ("rosa", "ffb3d6"), ("rosa_chicle", "ff6fc0"),
    ("gris", "9aa3c0"), ("gris_osc", "5a6080"), ("marron", "9a5a35"), ("marron_osc", "5e3420"),
    ("bronce", "d08a4a"), ("plata", "dfe6f5"), ("oro", "ffcf3a"), ("cristal", "e9f4ff"),
    ("carton", "d6a36a"), ("carton_osc", "a8743e"), ("turquesa_osc", "23907f"), ("turquesa_cl", "8ae8d6"),
    ("cafe", "6a3e2a"), ("taza", "f2ecf7"), ("lapiz_am", "ffc23a"), ("grafito", "4a4a5a"),
    ("papel", "f5eedd"), ("lavanda", "8f86d9"), ("luz", "ffe7b0"), ("negro_suave", "2e2440"),
    ("lima_osc", "6ac21e"), ("frutilla", "ff3d5e"), ("mermelada", "e8304a"), ("menta", "7ff0c8"),
    # ---- mazmorra de fantasia (colores naturales; ver docs/DIRECCION_MAZMORRA.md)
    ("piedra", "9a9aa3"), ("piedra_osc", "686a78"), ("piedra_cl", "c9c6c6"), ("piedra_tibia", "a4968a"),
    ("losa", "8a8792"), ("musgo", "6f9a4a"), ("tierra", "7d5f46"), ("madera_mesa", "8a5a36"),
    ("hierro", "7c8490"), ("hierro_osc", "4b515d"), ("acero", "bcc4cf"), ("tela_roja", "c8463e"),
    ("tela_azul", "3f6fc2"), ("tela_verde", "4f9a52"), ("tela_violeta", "7a54b8"), ("tela_amarilla", "e6b53c"),
    ("tela_blanca", "eee6d8"), ("cuero", "8f5c3a"), ("cuero_osc", "5e3b26"), ("piel_c", "f3cfa8"),
    ("piel_m", "d6a078"), ("piel_o", "9a684a"), ("pelo_rubio", "e6be6a"), ("pelo_castano", "7a4a2e"),
    ("pelo_negro", "3a2e2c"), ("pelo_rojo", "c45530"), ("barba_gris", "c9c4bd"), ("fuego", "ff8a2a"),
    ("fuego_cl", "ffd25a"), ("lava", "ff5a1e"), ("hielo", "a8dcf0"), ("cristal_azul", "66b6ff"),
    ("cristal_magico", "8e7cff"), ("antorcha_luz", "ffc96a"), ("slime", "72cf62"), ("slime_osc", "3f9446"),
    ("hueso", "e6dec6"), ("goblin", "8fb84a"), ("ogro", "98aa66"), ("murcielago", "5c4c6c"),
    ("espectro", "cfe4ee"), ("arana", "4b3d5c"), ("dragon", "c64a3c"), ("liche", "6c5c9c"),
    ("ojo_monstruo", "ffd84a"), ("tela_naranja", "e0782e"), ("tela_negra", "3b3442"), ("bronce_cl", "c9874a"),
]
PIDX = {k: i for i, (k, _) in enumerate(PALETTE)}
TEX = 256
CELL_W, CELL_H = 64, 8             # 4 columnas x 24 filas de franjas = 96 colores
WOOD_Y0 = 192                      # bloque de vetas: filas 192..255 (64 px de alto, 256 de ancho)


def srgb(hx):
    hx = hx.lstrip("#")
    return [int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def lin(c):
    return [((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c]


def cell_rect(name):
    """(u0, v0, u1, v1) de la franja del color en la textura (v hacia arriba, como Unity/Blender)."""
    i = PIDX[name]
    cx, cy = i % 4, i // 4
    x0 = cx * CELL_W; y0 = cy * CELL_H
    return (x0 / TEX, 1 - (y0 + CELL_H) / TEX, (x0 + CELL_W) / TEX, 1 - y0 / TEX)


def wood_pixel(x, y):
    """Bloque de vetas: mitad izquierda pino crudo (juguetes de madera), mitad derecha nogal oscuro (muebles).
    Vetas finas onduladas a lo largo de y (columna) y nudos suaves."""
    dark_wood = x >= 128
    x = (x % 128) * 2
    if dark_wood:
        base = srgb("7c4a36"); dark = srgb("4e2a24")
    else:
        base = srgb("e9c48c"); dark = srgb("c08a52")
    w = x / 256 * 46.0 + math.sin(y / 64 * 5.0 + x * 0.045) * 1.1 + math.sin(x * 0.27 + y * 0.02) * 0.3
    g = 0.5 + 0.5 * math.sin(w * math.pi)
    g = g ** 8
    k = 0.5 + 0.5 * math.sin(x * 0.11 + math.sin(y * 0.05) * 2.0)
    t = min(1.0, g * (0.38 if dark_wood else 0.55) + k * 0.12)
    return [base[i] * (1 - t) + dark[i] * t for i in range(3)]


def build_palette_pixels():
    px = [[0.0, 0.0, 0.0, 1.0] for _ in range(TEX * TEX)]
    for name, hx in PALETTE:
        i = PIDX[name]; cx, cy = i % 4, i // 4
        c = srgb(hx)
        for yy in range(CELL_H):
            for xx in range(CELL_W):
                t = xx / (CELL_W - 1)
                # degrade de franja suave: apenas mas claro arriba del objeto y apenas mas oscuro abajo (sin teñir)
                k = 1.04 - 0.1 * t
                col = [min(1.0, c[0] * k), min(1.0, c[1] * k), min(1.0, c[2] * k)]
                X = cx * CELL_W + xx; Y = cy * CELL_H + yy      # Y desde arriba de la imagen
                px[(TEX - 1 - Y) * TEX + X] = col + [1.0]
    for Y in range(WOOD_Y0, TEX):
        for X in range(TEX):
            px[(TEX - 1 - Y) * TEX + X] = wood_pixel(X, Y - WOOD_Y0) + [1.0]
    return px


def save_palette(path=None):
    path = path or os.path.join(OUT_MODELS, "paleta.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.get("paleta") or bpy.data.images.new("paleta", TEX, TEX, alpha=False)
    flat = [v for p in build_palette_pixels() for v in p]
    img.pixels.foreach_set(flat)
    img.filepath_raw = path; img.file_format = "PNG"; img.save()
    return path


# ------------------------------------------------------------------ materiales (clave -> color de paleta + banderas)
# shine: brillo (0 mate, 0.3 plastico, 0.55 plastilina, 1 metal) | rainbow: cristal arcoiris | glow: emision
# wood: usa el bloque de vetas (madera cruda)
MATS = {}


def matdef(key, col, shine=0.0, rainbow=0.0, glow=0.0, wood=False):
    MATS[key] = dict(col=col, shine=shine, rainbow=rainbow, glow=glow, wood=wood)


for _k, _ in PALETTE:
    matdef(_k, _k)
    matdef(_k + "_b", _k, shine=0.42)       # pintura con brillo (juguetes de nivel 2+)
matdef("madera", "carton", wood=True)
matdef("nogal", "marron_osc", wood=2)          # madera oscura de los muebles (vetas finas)
# mazmorra: metales y cuero (heroes y utileria)
matdef("hierro_m", "hierro", shine=0.7)
matdef("hierro_osc_m", "hierro_osc", shine=0.6)
matdef("acero_m", "acero", shine=0.85)
matdef("cuero_m", "cuero", shine=0.15)
matdef("cuero_osc_m", "cuero_osc", shine=0.15)
matdef("llama", "fuego", glow=1.0)
matdef("llama_cl", "fuego_cl", glow=1.0)
matdef("rayo_m", "cristal_azul", shine=0.8, glow=0.7)
matdef("veneno_m", "slime", shine=0.8, glow=0.4)
matdef("luz_espada", "hielo", shine=0.9, glow=0.6)
matdef("bronce_cl_m", "bronce_cl", shine=0.9)
matdef("madera_osc", "marron_osc")
matdef("plastico_blanco", "blanco", shine=0.45)
matdef("hojalata", "rojo", shine=0.75)
matdef("hojalata_azul", "azul_osc", shine=0.75)
matdef("bronce_m", "bronce", shine=0.9)
matdef("plata_m", "plata", shine=1.0)
matdef("oro_m", "oro", shine=1.0)
matdef("arcoiris", "cristal", shine=0.8, rainbow=1.0)
matdef("arcoiris_suave", "lila", shine=0.6, rainbow=0.7)
matdef("brillo_oro", "oro", shine=1.0, glow=0.6)
matdef("aura", "luz", glow=1.0)
matdef("aura_lila", "lila", glow=1.0)
matdef("luz_velador", "luz", glow=1.0)
matdef("plastilina_vio", "violeta", shine=0.5)
matdef("plastilina_lila", "lila", shine=0.5)
matdef("plastilina_am", "amarillo", shine=0.5)
matdef("gelatina", "mermelada", shine=0.9)
matdef("gelatina_luz", "mermelada", shine=0.9, glow=0.22)     # gelatina con luz adentro (parece translucida)
matdef("frutilla_m", "frutilla", shine=0.6)
matdef("chicle", "rosa_chicle", shine=0.7)
matdef("ojo_brillo", "blanco", glow=0.4)
matdef("cristal_vidrio", "cristal", shine=1.0, glow=0.25)
matdef("taza_m", "taza", shine=0.5)
matdef("cafe_m", "cafe", shine=0.8)
matdef("grafito_m", "grafito", shine=0.6)
matdef("lavanda_m", "lavanda", shine=0.2)
matdef("lata", "gris", shine=0.75)
matdef("lata_turq", "turquesa", shine=0.7)
matdef("porcelana", "rosa", shine=0.65)
matdef("porcelana_bl", "blanco", shine=0.65)
matdef("plastico_rojo", "rojo", shine=0.5)
matdef("plastico_am", "amarillo", shine=0.5)
matdef("plastico_az", "azul", shine=0.5)
matdef("plastico_verde", "verde", shine=0.5)
matdef("peluche", "crema", shine=0.0)
matdef("luz_hada", "amarillo", glow=0.8)
matdef("papel_m", "papel", shine=0.1)

# ------------------------------------------------------------------ escena, huesos y piezas
sc = None
BONES = {}          # nombre -> Empty
BONE_ORDER = []
PARTS = {}          # nombre de hueso -> [objetos]
CUR = ["raiz"]
BLMATS = {}


def reset():
    global sc
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    BONES.clear(); BONE_ORDER.clear(); PARTS.clear(); BLMATS.clear(); CLIPS.clear(); CUR[:] = ["raiz"]


def bone(name, parent=None, head=(0, 0, 0)):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.05
    sc.collection.objects.link(e)
    e.location = Vector(head) - (BONES[parent].matrix_world.translation if parent else Vector())
    if parent:
        e.parent = BONES[parent]
    e.rotation_mode = "XYZ"
    BONES[name] = e; BONE_ORDER.append(name); PARTS.setdefault(name, [])
    bpy.context.view_layer.update()
    return e


@contextmanager
def on(name):
    CUR.append(name)
    try:
        yield
    finally:
        CUR.pop()


def blmat(key):
    """Material de Blender para las vistas previas (Cycles). El juego usa la paleta + banderas, no estos."""
    if key in BLMATS: return BLMATS[key]
    d = MATS[key]
    m = bpy.data.materials.new(key); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    c = lin(srgb(dict(PALETTE)[d["col"]]))
    b.inputs["Base Color"].default_value = (*c, 1)
    b.inputs["Roughness"].default_value = 0.55 - 0.35 * d["shine"]
    if d["shine"] >= 0.9 and not d["rainbow"]:
        b.inputs["Metallic"].default_value = 0.85; b.inputs["Roughness"].default_value = 0.22
    if d["wood"]:
        wv = nt.nodes.new("ShaderNodeTexWave"); wv.wave_type = "BANDS"; wv.bands_direction = "X"; wv.inputs["Scale"].default_value = 9.0
        wv.inputs["Distortion"].default_value = 3.0; wv.inputs["Detail"].default_value = 1.5; wv.inputs["Detail Scale"].default_value = 0.6
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (*lin(srgb("e9c48c")), 1); ramp.color_ramp.elements[1].color = (*lin(srgb("b97f48")), 1)
        ramp.color_ramp.elements[0].position = 0.55; ramp.color_ramp.elements[1].position = 0.95
        ramp.color_ramp.elements[1].color = (*lin(srgb("cf9a62")), 1)
        if d["wood"] == 2:
            ramp.color_ramp.elements[0].color = (*lin(srgb("7c4a36")), 1); ramp.color_ramp.elements[1].color = (*lin(srgb("5a3226")), 1)
            wv.inputs["Scale"].default_value = 30.0
        nt.links.new(wv.outputs["Fac"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = 0.7
    if d["rainbow"]:
        lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.6
        ramp = nt.nodes.new("ShaderNodeValToRGB"); cr = ramp.color_ramp
        cols = ["ff7fd0", "a98bff", "5fd0ff", "6dffb0", "ffe96a", "ff8fc8"]
        while len(cr.elements) < len(cols): cr.elements.new(0.5)
        for i, h in enumerate(cols):
            cr.elements[i].position = i / (len(cols) - 1); cr.elements[i].color = (*lin(srgb(h)), 1)
        nt.links.new(lw.outputs["Facing"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = 0.12
        b.inputs["Coat Weight"].default_value = 1.0
        nt.links.new(ramp.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 0.35
    if d["glow"]:
        b.inputs["Emission Color"].default_value = (*c, 1); b.inputs["Emission Strength"].default_value = 2.5 * d["glow"]
    if key == "cristal_vidrio":      # vidrio de verdad en los renders (en el juego: brillo + emision suave)
        b.inputs["Transmission Weight"].default_value = 1.0; b.inputs["Roughness"].default_value = 0.04; b.inputs["IOR"].default_value = 1.3
        b.inputs["Emission Strength"].default_value = 0.0
    if key == "luz_velador":
        b.inputs["Emission Strength"].default_value = 12.0
    BLMATS[key] = m
    return m


def _finish(o, key, smooth=True, bevel=0.0, bev_seg=2, sub=0, angle=40):
    if o.name not in sc.collection.objects:
        sc.collection.objects.link(o)
    o.data.materials.clear(); o.data.materials.append(blmat(key))
    o["mk"] = key
    if bevel > 0:
        bv = o.modifiers.new("bisel", "BEVEL"); bv.width = bevel; bv.segments = bev_seg; bv.limit_method = "ANGLE"
        bv.harden_normals = False
    if sub:
        s = o.modifiers.new("sub", "SUBSURF"); s.levels = sub; s.render_levels = sub
    if smooth:
        for p in o.data.polygons: p.use_smooth = True
    o["smooth_angle"] = (angle if bevel <= 0 else 75) if smooth else 0.0
    b = CUR[-1]
    PARTS.setdefault(b, []).append(o)
    return o


def _obj(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return bpy.data.objects.new(name, me)


def place(o, c=(0, 0, 0), rot=(0, 0, 0)):
    o.location = Vector(c); o.rotation_euler = Euler([math.radians(a) for a in rot]); return o


LOD = [0.7]


def lod_seg(r, req, k, lo=6):
    """Segmentos segun el tamano (las piezas chicas no necesitan resolucion): presupuesto de triangulos."""
    return max(lo, min(req, int(round((6 + r * k) * LOD[0]))))


def ball(c, r, key, seg=16, ring=10, rot=(0, 0, 0), smooth=True, squash=None):
    """Esfera / elipsoide. r puede ser numero o (rx, ry, rz)."""
    bm = bmesh.new()
    rr = (r, r, r) if isinstance(r, (int, float)) else r
    seg = lod_seg(max(rr), seg, 70); ring = max(3 if max(rr) < 0.02 else 4, min(ring, int(round(seg * 0.6))))
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(rr), verts=bm.verts)
    o = _obj("bola", bm); place(o, c, rot)
    return _finish(o, key, smooth, angle=85)


def rbox(c, size, key, r=0.25, rot=(0, 0, 0), seg=2, taper=None):
    """Caja redondeada: r = fraccion del lado menor que se bisela. taper=(sx, sy) achica la cara de arriba."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    if taper:
        for v in bm.verts:
            if v.co.z > 0: v.co.x *= taper[0]; v.co.y *= taper[1]
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    o = _obj("caja", bm); place(o, c, rot)
    return _finish(o, key, True, bevel=min(size) * r, bev_seg=seg)


def tube(p0, p1, r0, r1, key, seg=12, cap=True, bevel=0.0, ry=None):
    """Cilindro / cono truncado entre dos puntos. ry = factor de seccion eliptica."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0; L = d.length
    seg = lod_seg(max(r0, r1), seg, 80)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=1.0, radius2=1.0, depth=1.0)
    for v in bm.verts:
        t = v.co.z + 0.5; r = r0 + (r1 - r0) * t
        v.co.x *= r; v.co.y *= r * (ry or 1.0); v.co.z *= L
    o = _obj("tubo", bm)
    o.location = (p0 + p1) / 2
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = d.to_track_quat("Z", "Y")
    auto = min(r0, r1) * 0.3 if cap and min(r0, r1) > 0.03 else 0
    return _finish(o, key, True, bevel=bevel if bevel else auto, bev_seg=2 if seg > 10 else 1)


def capsule(p0, p1, r, key, seg=12, ring=4):
    """Capsula (salchicha) entre dos puntos: tubo + 2 medias esferas, una sola malla."""
    seg = lod_seg(r, seg, 80)
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0; L = d.length
    bm = bmesh.new()
    rows = []
    for j in range(ring + 1):           # media esfera de abajo
        a = -math.pi / 2 + (math.pi / 2) * j / ring
        rows.append((math.cos(a) * r, -L / 2 + math.sin(a) * r))
    for j in range(ring + 1):
        a = (math.pi / 2) * j / ring
        rows.append((math.cos(a) * r, L / 2 + math.sin(a) * r))
    vs = []
    for rr, zz in rows:
        if rr < 1e-5: vs.append([bm.verts.new((0, 0, zz))]); continue
        vs.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * rr, math.sin(2 * math.pi * i / seg) * rr, zz)) for i in range(seg)])
    for j in range(len(vs) - 1):
        a, b = vs[j], vs[j + 1]
        for i in range(seg):
            i2 = (i + 1) % seg
            if len(a) == 1: bm.faces.new([a[0], b[i2], b[i]])
            elif len(b) == 1: bm.faces.new([a[i], a[i2], b[0]])
            else: bm.faces.new([a[i], a[i2], b[i2], b[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = _obj("capsula", bm)
    o.location = (p0 + p1) / 2
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = (d if L > 1e-6 else Vector((0, 0, 1))).to_track_quat("Z", "Y")
    return _finish(o, key, True, angle=85)


def torus(c, R, r, key, rot=(0, 0, 0), seg=18, mseg=6, scale=(1, 1, 1), arc=1.0):
    """Toro (o arco si arc < 1). Plano XY antes de rotar."""
    seg = lod_seg(R * max(arc, 0.3), seg, 80, lo=8)
    mseg = min(mseg, 4 if r < 0.013 else 5 if r < 0.02 else mseg)
    bm = bmesh.new()
    n = seg if arc >= 1 else seg + 1
    rings = []
    for i in range(n):
        a = 2 * math.pi * arc * i / seg
        cen = Vector((math.cos(a) * R, math.sin(a) * R, 0))
        out = Vector((math.cos(a), math.sin(a), 0))
        rings.append([bm.verts.new(cen + out * math.cos(2 * math.pi * k / mseg) * r + Vector((0, 0, math.sin(2 * math.pi * k / mseg) * r))) for k in range(mseg)])
    m = len(rings) if arc >= 1 else len(rings) - 1
    for i in range(m):
        a, b = rings[i], rings[(i + 1) % len(rings)]
        for k in range(mseg):
            k2 = (k + 1) % mseg
            bm.faces.new([a[k], b[k], b[k2], a[k2]])
    if arc < 1:
        bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    o = _obj("toro", bm); place(o, c, rot)
    return _finish(o, key, True, angle=85 if arc >= 1 else 60)


def cone(c, r1, r2, h, key, seg=12, rot=(0, 0, 0), smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h)
    o = _obj("cono", bm); place(o, c, rot)
    return _finish(o, key, smooth, angle=35)


def star(c, R, r, th, key, points=5, rot=(0, 0, 0), bevel=0.0):
    """Estrella gordita (prisma con bisel) en el plano XZ, mirando a -Y."""
    bm = bmesh.new()
    pts = []
    for i in range(points * 2):
        a = math.pi / 2 + math.pi * i / points
        rr = R if i % 2 == 0 else r
        pts.append((math.cos(a) * rr, math.sin(a) * rr))
    f = [bm.verts.new((x, -th / 2, z)) for x, z in pts]
    b = [bm.verts.new((x, th / 2, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(f); bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([f[i], b[i], b[j], f[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = _obj("estrella", bm); place(o, c, rot)
    return _finish(o, key, False, bevel=bevel or th * 0.35, bev_seg=1)


def prism(c, pts2d, th, key, rot=(0, 0, 0), bevel=0.0, smooth=False, axis="Y"):
    """Prisma de un poligono (x, z) con grosor th en Y (o en Z con axis='Z': poligono en x, y)."""
    bm = bmesh.new()
    if axis == "Y":
        f = [bm.verts.new((x, -th / 2, z)) for x, z in pts2d]; b = [bm.verts.new((x, th / 2, z)) for x, z in pts2d]
    else:
        f = [bm.verts.new((x, y, -th / 2)) for x, y in pts2d]; b = [bm.verts.new((x, y, th / 2)) for x, y in pts2d]
    n = len(pts2d)
    bm.faces.new(f); bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([f[i], b[i], b[j], f[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = _obj("prisma", bm); place(o, c, rot)
    return _finish(o, key, smooth, bevel=bevel, bev_seg=2)


def lathe(c, prof, key, seg=16, rot=(0, 0, 0), smooth=True, angle=60):
    """Solido de revolucion: prof = [(radio, z)] de abajo hacia arriba (radio 0 cierra)."""
    seg = lod_seg(max(p[0] for p in prof), seg, 80, lo=8)
    bm = bmesh.new()
    rows = []
    for rr, zz in prof:
        if rr < 1e-6: rows.append([bm.verts.new((0, 0, zz))])
        else: rows.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * rr, math.sin(2 * math.pi * i / seg) * rr, zz)) for i in range(seg)])
    for j in range(len(rows) - 1):
        a, b = rows[j], rows[j + 1]
        for i in range(seg):
            i2 = (i + 1) % seg
            if len(a) == 1 and len(b) == 1: continue
            if len(a) == 1: bm.faces.new([a[0], b[i2], b[i]])
            elif len(b) == 1: bm.faces.new([a[i], a[i2], b[0]])
            else: bm.faces.new([a[i], a[i2], b[i2], b[i]])
    if len(rows[0]) > 1: bm.faces.new(list(reversed(rows[0])))
    if len(rows[-1]) > 1: bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = _obj("torno", bm); place(o, c, rot)
    return _finish(o, key, smooth, angle=angle)


def boolean(target, cutter, op="DIFFERENCE"):
    """Booleana aplicada (el cortador se borra)."""
    m = target.modifiers.new("bool", "BOOLEAN"); m.object = cutter; m.operation = op; m.solver = "EXACT"
    bpy.context.view_layer.objects.active = target
    for o in sc.objects: o.select_set(False)
    target.select_set(True)
    bpy.ops.object.modifier_apply(modifier=m.name)
    for lst in PARTS.values():
        if cutter in lst: lst.remove(cutter)
    bpy.data.objects.remove(cutter)
    return target


def mirror_x(fn):
    """Llama fn(s) con s = -1 y s = +1 (lado derecho e izquierdo del personaje)."""
    for s in (-1, 1): fn(s)


# ------------------------------------------------------------------ animacion
CLIPS = []          # (nombre, cuadro inicial, largo, loop, eventos)


def clip(name, start, length, loop=False, ev=()):
    CLIPS.append(dict(n=name, start=start, len=length, loop=loop, ev=list(ev)))


def key(bname, frame, loc=None, rot=None, scale=None, interp="BEZIER"):
    """Fotograma clave del hueso: loc relativo a su reposo, rot en grados (XYZ del mundo), escala."""
    e = BONES[bname]
    if "rest" not in e: e["rest"] = list(e.location)
    rest = Vector(e["rest"])
    if loc is not None:
        e.location = rest + Vector(loc); e.keyframe_insert("location", frame=frame)
    if rot is not None:
        e.rotation_euler = Euler([math.radians(a) for a in rot]); e.keyframe_insert("rotation_euler", frame=frame)
    if scale is not None:
        e.scale = Vector(scale) if not isinstance(scale, (int, float)) else Vector((scale, scale, scale))
        e.keyframe_insert("scale", frame=frame)
    if e.animation_data and e.animation_data.action and interp != "BEZIER":
        for fc in e.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                if abs(kp.co.x - frame) < 0.01: kp.interpolation = interp


def pose(frame, **bones):
    """Atajo: pose(10, cabeza=dict(rot=(0,0,10)), raiz=dict(loc=(0,0,.1)))."""
    for b, kw in bones.items(): key(b, frame, **kw)


def rest_all(frame):
    """Clave de reposo en todos los huesos (cierra y abre cada clip limpio)."""
    for b in BONE_ORDER:
        key(b, frame, loc=(0, 0, 0), rot=(0, 0, 0), scale=1)


# ------------------------------------------------------------------ exportacion
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))     # Blender -> Unity (x, z, y): refleja


def to_u(v):
    return Vector((v.x, v.z, v.y))


def m_to_u(m):
    return C @ m @ C


def trs(m):
    t, q, s = m_to_u(m).decompose()
    return [round(t.x, 5), round(t.y, 5), round(t.z, 5)], [round(q.x, 5), round(q.y, 5), round(q.z, 5), round(q.w, 5)], [round(s.x, 4), round(s.y, 4), round(s.z, 4)]


def _uv(key, wpos, zmin, zmax, center):
    d = MATS[key]
    if d["wood"]:
        u = 0.5 + ((wpos.x - center.x) * 0.9 + (wpos.y - center.y) * 0.6) * 0.55
        v = (wpos.z - zmin) / max(1e-4, zmax - zmin)
        u = min(0.985, max(0.015, u)); v = min(0.985, max(0.015, v))
        u = u * 0.49 + (0.505 if d["wood"] == 2 else 0.0)      # media textura: pino | nogal
        return (u, (TEX - WOOD_Y0) / TEX * v)
    u0, v0, u1, v1 = cell_rect(d["col"])
    g = (wpos.z - zmin) / max(1e-4, zmax - zmin)
    t = 0.08 + 0.84 * (1 - g)       # arriba claro, abajo un poco mas oscuro
    return (u0 + (u1 - u0) * t, (v0 + v1) / 2)


def _col(key):
    d = MATS[key]
    return (round(d["shine"], 3), round(d["rainbow"], 3), round(d["glow"], 3), 1.0)


def _corner_normals(me, angle_deg):
    """Normales por esquina suavizadas solo entre caras con menos de angle_deg de diferencia (0 = facetado)."""
    me.calc_loop_triangles()
    tris = me.loop_triangles
    fn = [t.normal.copy() for t in tris]
    ar = [t.area for t in tris]
    if angle_deg <= 0:
        return {(ti, k): fn[ti] for ti in range(len(tris)) for k in range(3)}
    cosl = math.cos(math.radians(angle_deg))
    # agrupar por posicion (las esferas y capsulas comparten vertices; los biseles tambien)
    pk = {}
    for ti, t in enumerate(tris):
        for k, li in enumerate(t.loops):
            co = me.vertices[me.loops[li].vertex_index].co
            pk.setdefault((round(co.x, 5), round(co.y, 5), round(co.z, 5)), []).append(ti)
    out = {}
    for ti, t in enumerate(tris):
        for k, li in enumerate(t.loops):
            co = me.vertices[me.loops[li].vertex_index].co
            acc = Vector()
            for tj in pk[(round(co.x, 5), round(co.y, 5), round(co.z, 5))]:
                if fn[tj].dot(fn[ti]) >= cosl: acc += fn[tj] * max(ar[tj], 1e-9)
            out[(ti, k)] = acc.normalized() if acc.length > 1e-12 else fn[ti]
    return out


def bake_mesh_data(objs, inv, zmin, zmax, center):
    """Une las piezas de un hueso en arrays (espacio del hueso, ejes de Unity)."""
    dg = bpy.context.evaluated_depsgraph_get()
    pos, nrm, uv, col, idx = [], [], [], [], []
    weld = {}
    for o in objs:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        key_ = o.get("mk", "gris")
        mw = o.matrix_world
        loc_m = inv @ mw
        loc_n = loc_m.to_3x3().inverted().transposed()
        ang = o.get("smooth_angle", 0.0) or 0.0
        cn = _corner_normals(me, ang)
        for ti, tri in enumerate(me.loop_triangles):
            for k in (2, 1, 0):                      # el cambio de ejes refleja: invertir el orden
                li = tri.loops[k]
                vi = me.loops[li].vertex_index
                wp = mw @ me.vertices[vi].co
                lp = loc_m @ me.vertices[vi].co
                ln = (loc_n @ cn[(ti, k)]).normalized()
                u = _uv(key_, wp, zmin, zmax, center)
                kk = (round(lp.x, 4), round(lp.y, 4), round(lp.z, 4), round(ln.x, 2), round(ln.y, 2), round(ln.z, 2), round(u[0], 3), round(u[1], 3), key_)
                j = weld.get(kk)
                if j is None:
                    j = len(pos) // 3; weld[kk] = j
                    pu = to_u(lp); nu = to_u(ln)
                    pos += [round(pu.x, 4), round(pu.y, 4), round(pu.z, 4)]
                    nrm += [round(nu.x, 3), round(nu.y, 3), round(nu.z, 3)]
                    uv += [round(u[0], 4), round(u[1], 4)]
                    col += list(_col(key_))
                idx.append(j)
        ev.to_mesh_clear()
    return pos, nrm, uv, col, idx


def prep_render():
    """Para Cycles: suave por angulo en las piezas sin bisel (tubos, conos); las biseladas quedan suaves enteras."""
    for o in all_parts():
        a = o.get("smooth_angle", 0.0) or 0.0
        if a <= 0 or "bisel" in [m.name for m in o.modifiers]: continue
        if hasattr(o.data, "set_sharp_from_angle"): o.data.set_sharp_from_angle(angle=math.radians(a))


def all_parts():
    return [o for b in BONE_ORDER for o in PARTS.get(b, [])]


def bounds():
    dg = bpy.context.evaluated_depsgraph_get()
    zs, xs, ys = [], [], []
    for o in all_parts():
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        for v in me.vertices:
            w = o.matrix_world @ v.co; zs.append(w.z); xs.append(w.x); ys.append(w.y)
        ev.to_mesh_clear()
    return min(zs), max(zs), Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, 0))


import struct


def _str(b, s):
    e = s.encode("utf-8"); b += struct.pack("<B", len(e)) + e
    return b


def write_model_bin(path, bones, meshes, height, tris):
    """Formato TOY1 (lo lee Fortin.View.ToyModel): huesos en reposo + una malla por hueso, todo little endian."""
    b = bytearray(b"TOY1") + struct.pack("<i", 1)
    b += struct.pack("<i", len(bones))
    for bo in bones:
        b = _str(b, bo["n"]); b += struct.pack("<h", bo["p"]) + struct.pack("<10f", *(bo["t"] + bo["r"] + bo["s"]))
    b += struct.pack("<i", len(meshes))
    for m in meshes:
        nv = len(m["pos"]) // 3
        b += struct.pack("<hi", m["b"], nv)
        b += struct.pack("<%df" % (nv * 3), *m["pos"])
        b += struct.pack("<%dh" % (nv * 3), *[max(-32767, min(32767, int(round(x * 32767)))) for x in m["nrm"]])
        b += struct.pack("<%dH" % (nv * 2), *[max(0, min(65535, int(round(x * 65535)))) for x in m["uv"]])
        b += struct.pack("<%dB" % (nv * 4), *[max(0, min(255, int(round(x * 255)))) for x in m["col"]])
        b += struct.pack("<i", len(m["idx"])) + struct.pack("<%dH" % len(m["idx"]), *m["idx"])
    b += struct.pack("<fi", height, tris)
    with open(path, "wb") as f: f.write(b)


def write_anim_bin(path, names, clips):
    """Formato TAN1: por clip, (largo+1) cuadros x huesos x 10 floats (t xyz, r xyzw, s xyz) a 30 cps."""
    b = bytearray(b"TAN1") + struct.pack("<ii", FPS, len(names))
    for n in names: b = _str(b, n)
    b += struct.pack("<i", len(clips))
    for c in clips:
        b = _str(b, c["n"]); b += struct.pack("<iBi", c["len"], 1 if c["loop"] else 0, len(c["ev"]))
        b += struct.pack("<%di" % len(c["ev"]), *c["ev"])
        flat = [x for row in c["k"] for x in row]
        b += struct.pack("<%df" % len(flat), *flat)
    with open(path, "wb") as f: f.write(b)


def export_model(name, out_dir=None, extra=None):
    """Exporta huesos (reposo) y una malla por hueso. Devuelve los triangulos."""
    out_dir = out_dir or OUT_MODELS
    os.makedirs(out_dir, exist_ok=True)
    sc.frame_set(0)
    bpy.context.view_layer.update()
    zmin, zmax, center = bounds()
    bones, meshes, tris = [], [], 0
    for i, bn in enumerate(BONE_ORDER):
        e = BONES[bn]
        p = BONE_ORDER.index(e.parent.name) if e.parent else -1
        local = (e.parent.matrix_world.inverted() @ e.matrix_world) if e.parent else e.matrix_world.copy()
        t, r, s = trs(local)
        bones.append(dict(n=bn, p=p, t=t, r=r, s=s))
        objs = PARTS.get(bn, [])
        if not objs: continue
        pos, nrm, uv, col, idx = bake_mesh_data(objs, e.matrix_world.inverted(), zmin, zmax, center)
        if not idx: continue
        tris += len(idx) // 3
        meshes.append(dict(b=i, pos=pos, nrm=nrm, uv=uv, col=col, idx=idx))
    write_model_bin(os.path.join(out_dir, name + ".bytes"), bones, meshes, zmax - zmin, tris)
    print("MODELO", name, "tris", tris, "huesos", len(bones), "alto", round(zmax - zmin, 3))
    return tris


def export_anims(name, out_dir=None):
    out_dir = out_dir or OUT_MODELS
    clips = []
    for c in CLIPS:
        frames = []
        n = c["len"] + 1
        for f in range(n):
            sc.frame_set(c["start"] + f)
            row = []
            for bn in BONE_ORDER:
                e = BONES[bn]
                local = (e.parent.matrix_world.inverted() @ e.matrix_world) if e.parent else e.matrix_world.copy()
                t, r, s = trs(local)
                row += t + r + s
            frames.append([round(x, 4) for x in row])
        clips.append(dict(n=c["n"], len=c["len"], loop=c["loop"], ev=c["ev"], k=frames))
    sc.frame_set(0)
    write_anim_bin(os.path.join(out_dir, name + ".bytes"), list(BONE_ORDER), clips)
    print("ANIMS", name, [c["n"] for c in clips])


def clear_parts():
    """Borra las piezas (no los huesos ni las animaciones): para armar el siguiente nivel sobre el mismo esqueleto."""
    for lst in PARTS.values():
        for o in lst:
            bpy.data.objects.remove(o)
        lst.clear()


def attach_parts():
    """Emparenta las piezas a sus huesos (para renderizar poses en Blender)."""
    bpy.context.view_layer.update()
    for bn, lst in PARTS.items():
        e = BONES[bn]
        for o in lst:
            if o.parent is e: continue
            mw = o.matrix_world.copy()
            o.parent = e; o.matrix_parent_inverse = e.matrix_world.inverted()
            o.matrix_world = mw


def duplicate_group(objs, offset, rot_z=0.0, scale=1.0, frame=None):
    """Copia horneada (con modificadores y pose actual) de un grupo de objetos, para armar laminas."""
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    R = Matrix.Translation(Vector(offset)) @ Matrix.Rotation(math.radians(rot_z), 4, "Z") @ Matrix.Scale(scale, 4)
    for o in objs:
        ev = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        n = bpy.data.objects.new(o.name + "_copia", me)
        sc.collection.objects.link(n)
        n.matrix_world = R @ o.matrix_world
        out.append(n)
    return out


# ------------------------------------------------------------------ luz y render de laminas (Cycles)
def night_world(strength=0.55, col="4a3f8c"):
    w = bpy.data.worlds.new("noche"); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*lin(srgb(col)), 1); bg.inputs[1].default_value = strength
    return w


def dungeon_lights(PX=2.5, PT=3.55):
    """Luz de la mazmorra (igual que en el juego): antorcha calida alta desde arriba a la izquierda, ambiente frio de
    piedra, borde frio desde atras, antorchas del muro y el cristal azul."""
    night_world(0.55, "6f7690")
    light("antorcha_principal", "SUN", (-6, 4, 12), 3.2, "ffe4b8", target=(0, 0, 0))
    bpy.data.lights["antorcha_principal"].angle = math.radians(6)
    light("borde", "AREA", (6, 9, 4), 900, "9cc0ff", size=6.0, target=(0, 0, 0.5))
    for x in (-1.25, 1.25):
        light("antorcha_%d" % int(x * 10), "POINT", (x, 4.95, 1.8), 120, "ffb060", size=0.15)
    light("cristal", "POINT", (PX, PT + 0.15, 1.3), 160, "6ab6ff", size=0.2)


def light(name, kind, loc, energy, color_hex, size=1.0, target=(0, 0, 0.4), spot=None):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = lin(srgb(color_hex))
    if kind == "AREA": l.size = size
    if kind in ("POINT", "SPOT"): l.shadow_soft_size = size
    if kind == "SPOT" and spot: l.spot_size = math.radians(spot); l.spot_blend = 0.6
    o = bpy.data.objects.new(name, l); sc.collection.objects.link(o)
    o.location = Vector(loc)
    d = Vector(target) - o.location
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return o


def studio_lights(scale=1.0):
    """Luz de estudio calida (retratos y laminas): llave dorada, relleno violeta, borde azul luna."""
    light("llave", "AREA", (2.2 * scale, -2.6 * scale, 3.0 * scale), 420 * scale * scale, "ffe2b0", size=2.2 * scale)
    light("relleno", "AREA", (-3.0 * scale, -1.6 * scale, 1.4 * scale), 120 * scale * scale, "b7a8ff", size=3.0 * scale)
    light("borde", "AREA", (-1.2 * scale, 3.0 * scale, 2.6 * scale), 360 * scale * scale, "8f8cff", size=1.6 * scale)


def camera(loc, target, ortho=None, lens=50, name="cam"):
    cd = bpy.data.cameras.new(name)
    if ortho: cd.type = "ORTHO"; cd.ortho_scale = ortho
    else: cd.lens = lens
    cam = bpy.data.objects.new(name, cd); sc.collection.objects.link(cam)
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam


def render(path, rx, ry, samples=48, transparent=False):
    sc.render.engine = "CYCLES"; sc.cycles.samples = samples; sc.cycles.use_denoising = True
    sc.cycles.device = "CPU"
    sc.render.resolution_x, sc.render.resolution_y = rx, ry; sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDER", path)


def tri_report(top=15):
    """Triangulos por pieza (para recortar el presupuesto)."""
    dg = bpy.context.evaluated_depsgraph_get()
    rows = []
    for b in BONE_ORDER:
        for o in PARTS.get(b, []):
            me = o.evaluated_get(dg).to_mesh(); me.calc_loop_triangles()
            rows.append((len(me.loop_triangles), b, o.name, o.get("mk")))
            o.evaluated_get(dg).to_mesh_clear()
    rows.sort(reverse=True)
    for r in rows[:top]: print("  PIEZA", r)


def ring(c, r0, r1, key, seg=24, z=0.0):
    """Anillo plano (aro de luz, aura): una sola cara de ancho r1-r0, mirando arriba."""
    bm = bmesh.new()
    a = [bm.verts.new((math.cos(2 * math.pi * i / seg) * r0, math.sin(2 * math.pi * i / seg) * r0, 0)) for i in range(seg)]
    b = [bm.verts.new((math.cos(2 * math.pi * i / seg) * r1, math.sin(2 * math.pi * i / seg) * r1, 0)) for i in range(seg)]
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new([a[i], b[i], b[j], a[j]])
    o = _obj("anillo", bm); place(o, c)
    return _finish(o, key, False)


def unity_trs_to_blender(pos_u, rx=0.0, ry=0.0, rz=0.0, s=1.0):
    """Matriz de Blender equivalente a un transform de Unity (posicion y Quaternion.Euler(rx, ry, rz) de Unity)."""
    Rx = Matrix.Rotation(math.radians(rx), 4, "X"); Ry = Matrix.Rotation(math.radians(ry), 4, "Y"); Rz = Matrix.Rotation(math.radians(rz), 4, "Z")
    Mu = Matrix.Translation(Vector(pos_u)) @ Ry @ Rx @ Rz @ Matrix.Scale(s, 4)
    return C @ Mu @ C


def bake(fn, M, frame=0, bones_raiz=True):
    """Arma algo nuevo con fn() (crea sus huesos si quiere), lo copia horneado con la matriz M y borra las piezas."""
    BONES.clear(); BONE_ORDER.clear(); PARTS.clear(); CLIPS.clear()
    if bones_raiz:
        bone("raiz", None, (0, 0, 0)); CUR[:] = ["raiz"]
    fn()
    sc.frame_set(frame)
    bpy.context.view_layer.update()
    attach_parts()
    sc.frame_set(frame)
    prep_render()
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    for o in all_parts():
        ev = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev)
        n = bpy.data.objects.new(o.name + "_h", me); sc.collection.objects.link(n)
        n.matrix_world = M @ o.matrix_world
        out.append(n)
    for o in all_parts(): bpy.data.objects.remove(o)
    for b in list(BONES.values()):
        if b.animation_data: b.animation_data_clear()
    PARTS.clear()
    sc.frame_set(0)
    return out
