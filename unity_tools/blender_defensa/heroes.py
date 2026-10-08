"""Fortin de Juguete: heroes (juguetes del escritorio) en sus 7 niveles de fusion, con esqueleto y animaciones.

Uso:
  blender -b -P heroes.py -- [--only soldadito,arquera] [--no-export] [--sheet ruta.png] [--samples 48]
Cada heroe exporta:
  Resources/Models/h_<id>_L1..L7.json   (mismo esqueleto, piezas y pintura del nivel)
  Resources/Models/h_<id>_anim.json     (idle, aparecer, atacar, efecto, fusionar, festejar, alzado)
Niveles (siempre el mismo modelo; el color y la silueta de cada juguete se leen desde el nivel 1):
  1 pintado mate | 2 pintado con brillo y ojos mas grandes | 3 detalles (cinturon, escudito, plumita)
  4 ribetes de bronce y capa corta | 5 plata en armas y bordes (+ aura en el juego) | 6 oro, corona o alas
  7 cristal arcoiris + halo
La cabeza es el 50 % del alto (se agranda 15 % sobre el cuello al final del armado). Frente hacia -Y.
"""
import sys, os, math, argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import ball, rbox, tube, capsule, torus, cone, star, prism, lathe, on, key, pose, rest_all, clip, boolean
from mathutils import Vector

# ------------------------------------------------------------------ medidas del chibi
HEAD_C = Vector((0, 0, 0.665)); HEAD_R = Vector((0.205, 0.19, 0.195))
SHOULDER = 0.415; HAND_Z = 0.255


def head_pt(x, z, out=0.0):
    """Punto sobre la cara (frente -Y) de la cabeza en (x, z), un poco afuera."""
    u = x / HEAD_R.x; w = (z - HEAD_C.z) / HEAD_R.z
    y = -HEAD_R.y * math.sqrt(max(0.0, 1 - u * u - w * w))
    n = Vector((x / HEAD_R.x ** 2, y / HEAD_R.y ** 2, (z - HEAD_C.z) / HEAD_R.z ** 2)).normalized()
    return Vector((x, y, z)) + n * out, n


# ------------------------------------------------------------------ pintura por nivel
FACE_SLOTS = ("ojo", "boca", "mejilla", "brillo")


def paint(base, lv):
    """base: colores de identidad del heroe. Devuelve slot -> clave de material del nivel lv.
    Nivel 1: los mismos colores en pintura mate. Nivel 2+: los colores lisos pasan a pintura con brillo."""
    p = dict(base)
    p.setdefault("ojo", "ojo"); p.setdefault("boca", "ojo"); p.setdefault("mejilla", "mejilla"); p.setdefault("brillo", "ojo_brillo")
    for k, v in list(p.items()):
        if k in FACE_SLOTS or not isinstance(v, str) or v not in kit.MATS: continue
        m = kit.MATS[v]
        if lv == 1:
            if not m["glow"] and not m["rainbow"]: p[k] = m["col"]                 # mate
        elif m["shine"] == 0 and not m["glow"] and not m["wood"] and (v + "_b") in kit.MATS:
            p[k] = v + "_b"                                                        # brillo de pintura
    if lv == 1:
        p["trim"] = kit.MATS[base.get("trim", "amarillo")]["col"]
        return p
    p["trim"] = {2: base.get("trim", "amarillo"), 3: base.get("trim", "amarillo"), 4: "bronce_m", 5: "plata_m", 6: "oro_m", 7: "oro_m"}[lv]
    if lv >= 5 and "metal" in base:            # piezas de armadura que pasan a plata / oro
        p[base["metal"]] = {5: "plata_m", 6: "oro_m", 7: "arcoiris"}[lv]
    if lv == 7:
        for s in base.get("cristal", ("ropa", "ropa2", "sombrero")):
            if s in p: p[s] = "arcoiris"
    return p


# ------------------------------------------------------------------ esqueleto comun
HEAD_K, NECK_Z = 1.15, 0.47       # la cabeza se agranda 15 % sobre el cuello (queda en el 50 % del alto)
EXTRA_BONES = {
    # huesos propios para el reposo de cada heroe
    "mago": [("sombrero", "cabeza", (0, 0, NECK_Z + (0.8 - NECK_Z) * HEAD_K))],
    "mimico": [("tapa", "cabeza", (0, 0.2, 0.62))],
    "hada": [("alas", "pecho", (0, 0.1, 0.38))],
    "bardo": [("laud", "pecho", (0, -0.1, 0.3))],
}


def bn(name, fallback):
    """Hueso propio si el esqueleto lo tiene (las laminas arman sin extras)."""
    return name if name in kit.BONES else fallback


def skeleton(hid=None):
    kit.bone("raiz", None, (0, 0, 0))
    kit.bone("cadera", "raiz", (0, 0, 0.19))
    kit.bone("pecho", "cadera", (0, 0, 0.28))
    kit.bone("cabeza", "pecho", (0, 0, 0.46))
    for s, n in ((1, "L"), (-1, "R")):
        kit.bone("brazo_" + n, "pecho", (0.13 * s, 0, SHOULDER))
        kit.bone("mano_" + n, "brazo_" + n, (0.165 * s, -0.005, HAND_Z + 0.03))
        kit.bone("pierna_" + n, "cadera", (0.062 * s, 0, 0.17))
    for name, parent, head in EXTRA_BONES.get(hid, []):
        kit.bone(name, parent, head)


def body(P, lv, robe=False, legs=True, sleeve_r=0.044, torso_w=1.0):
    """Cuerpo chibi: piernas y botas, cadera, torso, brazos, manos, cabeza y cara segun el nivel."""
    if legs and not robe:
        for s, n in ((1, "L"), (-1, "R")):
            with on("pierna_" + n):
                capsule((0.062 * s, 0, 0.16), (0.064 * s, 0, 0.085), 0.047, P["pantalon"], seg=10, ring=2)
                rbox((0.066 * s, -0.018, 0.045), (0.088, 0.128, 0.07), P["botas"], r=0.42)
                rbox((0.066 * s, -0.022, 0.012), (0.094, 0.138, 0.024), P.get("suela", "cuero_osc_m"), r=0.4, seg=1)   # suela
    with on("cadera"):
        if robe:
            lathe((0, 0, 0), [(0.0, 0.012), (0.155 * torso_w, 0.012), (0.17 * torso_w, 0.03), (0.15 * torso_w, 0.12),
                              (0.125 * torso_w, 0.24), (0.0, 0.24)], P["ropa"], seg=18)
        else:
            lathe((0, 0, 0), [(0.0, 0.13), (0.112 * torso_w, 0.13), (0.124 * torso_w, 0.16), (0.12 * torso_w, 0.225), (0.0, 0.225)], P["pantalon"], seg=16)
    with on("pecho"):
        lathe((0, 0, 0), [(0.0, 0.2), (0.118 * torso_w, 0.2), (0.128 * torso_w, 0.27), (0.118 * torso_w, 0.37),
                          (0.085 * torso_w, 0.445), (0.0, 0.47)], P["ropa"], seg=16)
    for s, n in ((1, "L"), (-1, "R")):
        with on("brazo_" + n):
            capsule((0.128 * s, 0, SHOULDER - 0.005), (0.158 * s, -0.005, HAND_Z + 0.05), sleeve_r, P.get("manga", P["ropa"]), seg=9, ring=2)
        with on("mano_" + n):
            ball((0.165 * s, -0.005, HAND_Z + 0.012), 0.046, P.get("guante", P["piel"]), seg=10, ring=7)
            if "guante" in P: torus((0.163 * s, -0.004, HAND_Z + 0.05), 0.04, 0.012, P["guante"], seg=10, mseg=3)
    with on("cabeza"):
        ball(HEAD_C, tuple(HEAD_R), P["piel"], seg=20, ring=12)
        face(P, lv)


def face(P, lv):
    """Cara tierna en todos los niveles: ojos grandes con brillo blanco, cachetes rosados y boquita.
    Desde el nivel 2 los ojos crecen un poco mas."""
    k = 1.0 if lv < 2 else 1.12
    for s in (-1, 1):
        ex = 0.08 * s; ez = 0.665
        p, n = head_pt(ex, ez, -0.006)
        ball(p, (0.036 * k, 0.017, 0.046 * k), P["ojo"], seg=10, ring=7)
        q, _ = head_pt(ex - 0.012 * k, ez + 0.019 * k, 0.008)
        ball(q, 0.013 * k, P["brillo"], seg=8, ring=6)
        q2, _ = head_pt(ex + 0.011 * k, ez - 0.016 * k, 0.006)
        ball(q2, 0.006 * k, P["brillo"], seg=6, ring=4)
        m, _ = head_pt(0.128 * s, 0.6, -0.008)
        ball(m, (0.034, 0.01, 0.02), P["mejilla"], seg=10, ring=6)
    p, n = head_pt(0.0, 0.594, -0.002)
    torus(p, 0.022, 0.0065, P["boca"], seg=10, mseg=5, arc=0.5)
    kit.PARTS["cabeza"][-1].rotation_euler = (math.radians(90), math.radians(180), 0)


# ------------------------------------------------------------------ piezas de nivel (comunes)
def cape(P, lv, col):
    """Capa (nivel 4+): curva detras del pecho, con borde del metal del nivel."""
    if lv < 4: return
    with on("pecho"):
        pts = []
        for i in range(9):
            a = math.pi * (0.15 + 0.7 * i / 8)
            pts.append((math.cos(a) * 0.15, math.sin(a) * 0.065 + 0.045))
        bm = kit.bmesh.new()
        top, bot = [], []
        for x, y in pts:
            top.append(bm.verts.new((x * 0.95, y - 0.01, 0.44)))
            bot.append(bm.verts.new((x * 1.7, y + 0.06, 0.1)))
        for i in range(len(pts) - 1):
            bm.faces.new([top[i], top[i + 1], bot[i + 1], bot[i]])
        kit.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        o = kit._obj("capa", bm); kit._finish(o, col, True, angle=50)
        sm = o.modifiers.new("grosor", "SOLIDIFY"); sm.thickness = 0.016
        tube((-0.15, 0.05, 0.43), (0.15, 0.05, 0.43), 0.022, 0.022, P["trim"], seg=8)


AURA_MESH = False     # el aura la dibuja el juego (anillo aditivo que late); el modelo no la necesita


def armor(P, lv, plate=True, pads=True, plate_z=0.33):
    """Hombreras del metal del nivel (4+) y pechera (5+): la evolucion se lee de lejos."""
    if lv < 4: return
    with on("pecho"):
        if pads:
            for s in (-1, 1):
                ball((0.118 * s, 0.0, 0.438), (0.058, 0.058, 0.03), P["trim"], seg=12, ring=6)
        if plate and lv >= 5:
            ball((0, -0.088, plate_z), (0.085, 0.04, 0.075), P["trim"], seg=12, ring=7)
            star((0, -0.128, plate_z), 0.032, 0.014, 0.012, "rojo" if lv < 7 else "brillo_oro")


def aura(P, lv):
    """Aura suave a los pies (nivel 5+): disco emisivo (el juego le suma un brillo que late)."""
    if lv < 5 or not AURA_MESH: return
    with on("raiz"):
        kit.ring((0, 0, 0.008), 0.15, 0.215, "aura" if lv < 7 else "aura_lila", seg=28)


def crown(P, lv, z, r=0.085):
    """Corona dorada (nivel 6) o de cristal (7)."""
    k = "oro_m" if lv == 6 else "arcoiris"
    lathe((0, 0, z), [(0.0, 0.0), (r, 0.0), (r * 1.05, 0.045), (0.0, 0.045)], k, seg=12, angle=50)
    for i in range(5):
        a = 2 * math.pi * i / 5 - math.pi / 2
        cone((math.cos(a) * r, math.sin(a) * r, z + 0.065), 0.026, 0.0, 0.05, k, seg=5)
        if i % 2 == 0 and lv == 6: ball((math.cos(a) * r, math.sin(a) * r, z + 0.093), 0.012, "rojo" if i == 0 else "azul", seg=6, ring=3)


def wings(P, lv):
    """Alas de papel dorado (6) o cristal (7) en la espalda."""
    k = "oro_m" if lv == 6 else "arcoiris"
    with on("pecho"):
        for s in (-1, 1):
            pts = [(0.0, 0.0), (0.12, 0.09), (0.24, 0.14), (0.27, 0.08), (0.22, 0.02), (0.25, -0.04), (0.17, -0.06), (0.1, -0.05)]
            o = prism((0.05 * s, 0.12, 0.36), [(x * s, z) for x, z in pts], 0.016, k, rot=(0, 0, -25 * s), bevel=0.0)


def halo(P, lv, z):
    if lv < 7: return
    with on("cabeza"):
        torus((0, 0.02, z), 0.12, 0.012, "brillo_oro", seg=14, mseg=4)


def badge(c, P, lv, shape="escudo", key_=None):
    """Escudito del nivel 3+."""
    k = key_ or P["trim"]
    if shape == "escudo":
        pts = [(-0.04, 0.035), (0.04, 0.035), (0.04, -0.005), (0.0, -0.045), (-0.04, -0.005)]
        prism(c, pts, 0.014, k, bevel=0.006)
    else:
        star(c, 0.035, 0.016, 0.014, k)


def base_stars(lv):
    """Nada: la peana y las estrellas las pone el juego (peana_<rareza>.json + estrellita.json)."""
    return


# ================================================================== HEROES (mazmorra de fantasia)
HEROES = {}


def hero(fn):
    HEROES[fn.__name__] = fn
    return fn


def belt(P, z=0.215, r=0.122, key_=None):
    """Cinturon de cuero con hebilla (de metal del nivel)."""
    with on("cadera"):
        torus((0, 0, z), r, 0.016, key_ or P.get("cinto", "cuero_osc"), seg=14, mseg=4, scale=(1, 0.92, 1))
        rbox((0, -r * 0.93, z), (0.05, 0.02, 0.042), P["trim"], r=0.3, seg=1)
        rbox((0, -r * 0.93 - 0.004, z), (0.028, 0.02, 0.022), P.get("cinto", "cuero_osc"), r=0.3, seg=1)


def pouch(P, x, z=0.2):
    with on("cadera"):
        rbox((x, -0.08, z - 0.02), (0.06, 0.04, 0.07), P.get("bolsa", "cuero"), r=0.35)


def bent_hat(base_z, r0, h, key_, bend=0.12, seg=16, rings=9, lump=0.0):
    """Sombrero blando: cono que se dobla hacia atras en la punta."""
    bm = kit.bmesh.new()
    rows = []
    for j in range(rings + 1):
        t = j / rings
        r = r0 * (1 - t) ** 0.9 + 0.006 * (1 - t)
        cz = base_z + h * t
        cy = bend * (t ** 2.2)
        cz -= bend * 0.6 * (t ** 3)
        if j == rings: rows.append([bm.verts.new((0, cy, cz))]); break
        rows.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * r, cy + math.sin(2 * math.pi * i / seg) * r, cz)) for i in range(seg)])
    for j in range(len(rows) - 1):
        a, b = rows[j], rows[j + 1]
        for i in range(seg):
            i2 = (i + 1) % seg
            if len(b) == 1: bm.faces.new([a[i], a[i2], b[0]])
            else: bm.faces.new([a[i], a[i2], b[i2], b[i]])
    bm.faces.new(list(reversed(rows[0])))
    kit.bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    o = kit._obj("sombrero", bm)
    return kit._finish(o, key_, True, angle=70)


def hair_cap(P, z=0.74, key_="pelo"):
    """Pelo corto: casquete que cubre la parte de atras y arriba de la cabeza."""
    h = ball((0, 0.03, z - 0.01), (0.212, 0.205, 0.17), P[key_], seg=16, ring=9)
    cut = kit.ball((0, -0.2, z - 0.1), (0.2, 0.2, 0.16), P[key_], seg=12, ring=8)
    boolean(h, cut)
    return h


@hero
def lancero(lv):
    """Lancero: yelmo de acero con cresta roja, tunica azul, escudo redondo y lanza larga."""
    P = paint(dict(piel="piel_c", ropa="tela_azul", ropa2="tela_roja", pantalon="cuero", botas="cuero_osc", guante="cuero",
                   casco="acero_m", cresta="tela_roja", escudo="tela_azul", asta="madera_mesa", arma="acero_m", trim="tela_amarilla",
                   metal="casco", cristal=("ropa", "ropa2", "escudo", "cresta")), lv)
    body(P, lv)
    belt(P)
    with on("cabeza"):
        # yelmo redondo con ala corta, protector de nariz y cresta de pelo rojo
        h = ball((0, 0.01, 0.73), (0.222, 0.215, 0.19), P["casco"], seg=14, ring=8)
        cut = kit.ball((0, -0.2, 0.62), (0.19, 0.2, 0.17), P["casco"], seg=12, ring=7)
        boolean(h, cut)
        torus((0, 0.01, 0.72), 0.218, 0.016, P["trim"] if lv >= 4 else P["casco"], seg=22, mseg=5, scale=(1, 0.98, 1))
        for i in range(5):
            t = (i - 2) / 2
            ball((0, 0.02 + t * 0.15, 0.92 - abs(t) ** 2 * 0.07), (0.04, 0.075, 0.065), P["cresta"], seg=7, ring=4)
        if lv >= 3: badge(Vector((0, -0.2, 0.84)), P, lv, "estrella")
    with on("pecho"):
        if lv >= 2:   # cuello de malla y correa en diagonal
            torus((0, 0, 0.44), 0.085, 0.022, "hierro_m", seg=14, mseg=4)
            tube((0.1, -0.09, 0.42), (-0.08, -0.11, 0.24), 0.013, 0.013, "cuero_osc", seg=6, ry=0.5)
    with on("mano_R"):
        # lanza larga: asta de madera, punta de hoja y banderin
        tube((-0.165, -0.01, 0.0), (-0.165, -0.01, 0.86), 0.014, 0.014, P["asta"], seg=8)
        cone((-0.165, -0.01, 0.92), 0.04, 0.0, 0.12, P["arma"], seg=6)
        torus((-0.165, -0.01, 0.855), 0.022, 0.009, P["trim"] if lv >= 3 else "hierro_m", seg=10, mseg=4)
        if lv >= 2: prism((-0.165, -0.01, 0.78), [(0.0, 0.0), (0.13, -0.03), (0.0, -0.08)], 0.008, P["ropa2"])
    with on("mano_L"):   # escudo redondo con umbo
        lathe((0.205, -0.03, HAND_Z + 0.02), [(0.0, -0.014), (0.105, -0.014), (0.11, 0.0), (0.09, 0.02), (0.0, 0.024)],
              P["escudo"], seg=18, rot=(0, 90, 0), angle=50)
        torus((0.21, -0.03, HAND_Z + 0.02), 0.106, 0.011, P["trim"] if lv >= 4 else "hierro_m", rot=(0, 90, 0), seg=18, mseg=4)
        ball((0.232, -0.03, HAND_Z + 0.02), 0.03, "hierro_m" if lv < 5 else P["trim"], seg=10, ring=6)
    armor(P, lv, plate_z=0.31)
    cape(P, lv, "tela_roja" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6:
        with on("cabeza"): crown(P, lv, 0.9, 0.12)
    halo(P, lv, 1.1)


@hero
def arquera(lv):
    """Arquera Elfica: capucha verde, orejas en punta, trenza rubia, arco largo de madera y carcaj."""
    P = paint(dict(piel="piel_c", ropa="tela_verde", ropa2="cuero", pantalon="cuero_osc", botas="cuero", guante="cuero",
                   capucha="tela_verde", pelo="pelo_rubio", arco="madera", cuerda="tela_blanca", trim="tela_amarilla", carcaj="cuero",
                   flecha="tela_blanca", pluma="tela_roja", metal="carcaj", cristal=("ropa", "capucha", "pantalon")), lv)
    P["arco"] = "madera"
    body(P, lv)
    belt(P)
    with on("cabeza"):
        h = ball((0, 0.02, 0.68), (0.228, 0.222, 0.222), P["capucha"], seg=14, ring=8)
        cut = kit.ball((0, -0.2, 0.635), (0.168, 0.2, 0.17), P["capucha"], seg=12, ring=7)
        boolean(h, cut)
        cone((0, 0.2, 0.8), 0.07, 0.0, 0.2, P["capucha"], seg=12, rot=(-60, 0, 0))
        for s_ in (-1, 1):    # orejas de elfo asomando por los costados de la capucha
            cone((0.2 * s_, -0.03, 0.67), 0.035, 0.0, 0.12, P["piel"], seg=8, rot=(0, 75 * s_, 0))
        for x in (-0.09, -0.03, 0.04, 0.1):    # flequillo rubio
            ball(head_pt(x, 0.79, -0.02)[0], (0.05, 0.03, 0.035), P["pelo"], seg=10, ring=6)
        if lv >= 3: badge(Vector((0.0, -0.205, 0.83)), P, lv, "escudo")
    with on("pecho"):
        rbox((0, -0.07, 0.32), (0.2, 0.08, 0.16), P["ropa2"], r=0.35)          # chaleco de cuero
        tube((0, 0.15, 0.48), (0.02, 0.17, 0.28), 0.028, 0.012, P["pelo"], seg=8)  # trenza
        if lv >= 3:   # carcaj con flechas
            tube((0.11, -0.09, 0.43), (-0.1, -0.1, 0.22), 0.012, 0.012, P["carcaj"], seg=6, ry=0.5)
            tube((-0.06, 0.13, 0.22), (0.07, 0.15, 0.5), 0.04, 0.045, P["carcaj"], seg=12)
            for i in range(3):
                x = 0.03 + 0.025 * i
                tube((x, 0.15, 0.45), (x + 0.03, 0.17, 0.6), 0.006, 0.006, P["flecha"], seg=6)
                cone((x + 0.032, 0.172, 0.62), 0.018, 0.0, 0.04, P["pluma"], seg=4)
    with on("mano_L"):   # arco largo
        torus((0.175, -0.04, HAND_Z + 0.02), 0.25, 0.015, P["arco"], rot=(0, 90, 0), seg=22, mseg=6, arc=0.45)
        kit.PARTS["mano_L"][-1].rotation_euler = (math.radians(-81), math.radians(90), 0)
        tube((0.175, 0.0, HAND_Z + 0.02 + 0.24), (0.175, 0.0, HAND_Z + 0.02 - 0.24), 0.004, 0.004, P["cuerda"], seg=5)
        rbox((0.175, -0.05, HAND_Z + 0.02), (0.03, 0.03, 0.06), P["ropa2"], r=0.3, seg=1)     # empunadura
        if lv >= 4:
            for z in (0.23, -0.23): ball((0.175, -0.07, HAND_Z + 0.02 + z), 0.02, P["trim"], seg=8, ring=6)
    armor(P, lv)
    cape(P, lv, "tela_verde" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6: wings(P, lv)
    halo(P, lv, 1.0)


@hero
def mago(lv):
    """Mago de Fuego: sombrero conico violeta, barba blanca, tunica con estrellas y baston con llama."""
    P = paint(dict(piel="piel_c", ropa="tela_violeta", ropa2="tela_azul", pantalon="tela_violeta", botas="cuero_osc",
                   sombrero="tela_violeta", barba="barba_gris", baston="madera_mesa", trim="tela_amarilla",
                   metal="ropa2", cristal=("ropa", "sombrero")), lv)
    body(P, lv, robe=True, torso_w=1.0)
    for s_, n in ((1, "L"), (-1, "R")):
        with on("pierna_" + n):
            ball((0.07 * s_, -0.08, 0.03), (0.05, 0.07, 0.035), P["botas"], seg=12, ring=8)
    with on(bn("sombrero", "cabeza")):
        lathe((0, 0, 0.79), [(0.0, -0.01), (0.255, -0.01), (0.27, 0.0), (0.25, 0.018), (0.0, 0.02)], P["sombrero"], seg=24, angle=50)
        bent_hat(0.8, 0.165, 0.38, P["sombrero"], bend=0.16)
        torus((0, 0, 0.82), 0.155, 0.016, P["ropa2"] if lv < 4 else P["trim"], seg=24, mseg=6)
        if lv >= 3:
            for (x, z) in ((-0.07, 0.92), (0.06, 1.0), (0.0, 0.88)):
                star((x, -0.13 + abs(x) * 0.3 + (z - 0.88) * 0.35, z), 0.022, 0.01, 0.01, P["trim"], rot=(-25, 0, 0))
        if lv >= 6: crown(P, lv, 0.835, 0.17)
    with on("cabeza"):
        p, _ = head_pt(0.0, 0.53, -0.04)
        ball(p + Vector((0, -0.005, -0.03)), (0.11, 0.07, 0.1), P["barba"], seg=14, ring=9)
        cone(p + Vector((0, -0.01, -0.13)), 0.055, 0.0, 0.08, P["barba"], seg=10, rot=(180 - 15, 0, 0))
        for s_ in (-1, 1):
            ball(head_pt(0.03 * s_, 0.592, 0.0)[0], (0.032, 0.016, 0.014), P["barba"], seg=8, ring=5)
    with on("pecho"):
        if lv >= 2: torus((0, 0, 0.245), 0.13, 0.016, P["ropa2"] if lv < 3 else P["trim"], seg=20, mseg=6)
        if lv >= 3: badge(Vector((0, -0.13, 0.33)), P, lv, "estrella")
    with on("mano_R"):   # baston con una llama arriba
        tube((-0.17, -0.01, 0.0), (-0.17, -0.01, 0.62), 0.015, 0.012, P["baston"], seg=8)
        torus((-0.17, -0.01, 0.6), 0.03, 0.01, P["trim"] if lv >= 2 else "hierro_m", seg=10, mseg=4)
        cone((-0.17, -0.01, 0.7), 0.05, 0.0, 0.14, "llama", seg=8)
        cone((-0.17, -0.02, 0.68), 0.028, 0.0, 0.08, "llama_cl", seg=6)
    armor(P, lv, plate=False)
    cape(P, lv, "tela_azul" if lv < 7 else "arcoiris")
    aura(P, lv)
    halo(P, lv, 1.26)


@hero
def enano(lv):
    """Enano Minero: casco con vela, barba roja trenzada, overol azul y pico."""
    P = paint(dict(piel="piel_m", ropa="tela_azul", ropa2="cuero", pantalon="tela_azul", botas="cuero_osc", guante="cuero",
                   casco="hierro_m", barba="pelo_rojo", pico="hierro_m", mango="madera_mesa", trim="tela_amarilla",
                   metal="casco", cristal=("ropa", "pantalon", "barba")), lv)
    body(P, lv, torso_w=1.12)
    belt(P, r=0.135)
    with on("cabeza"):
        h = ball((0, 0.01, 0.74), (0.222, 0.215, 0.17), P["casco"], seg=16, ring=9)
        cut = kit.ball((0, -0.18, 0.64), (0.19, 0.2, 0.15), P["casco"], seg=12, ring=8)
        boolean(h, cut)
        torus((0, 0.01, 0.74), 0.22, 0.018, P["trim"] if lv >= 4 else "hierro_osc_m", seg=20, mseg=4)
        # vela encendida en el casco
        tube((0, -0.12, 0.84), (0, -0.12, 0.92), 0.026, 0.026, "tela_blanca", seg=8)
        cone((0, -0.12, 0.96), 0.016, 0.0, 0.05, "llama_cl", seg=6)
        # barba roja grande con dos trenzas
        p, _ = head_pt(0.0, 0.55, -0.035)
        ball(p + Vector((0, -0.01, -0.04)), (0.15, 0.08, 0.12), P["barba"], seg=14, ring=9)
        for s_ in (-1, 1):
            tube(p + Vector((0.06 * s_, -0.02, -0.12)), p + Vector((0.07 * s_, -0.03, -0.24)), 0.025, 0.015, P["barba"], seg=8)
            ball(p + Vector((0.07 * s_, -0.03, -0.25)), 0.018, P["trim"], seg=6, ring=4)
        for s_ in (-1, 1): ball(head_pt(0.035 * s_, 0.6, 0.0)[0], (0.035, 0.016, 0.016), P["barba"], seg=8, ring=5)
        if lv >= 3: badge(Vector((0.13, -0.16, 0.8)), P, lv, "estrella")
    with on("pecho"):
        rbox((0, -0.105, 0.3), (0.17, 0.035, 0.13), P["ropa"], r=0.3)            # peto del overol
        for sx in (-1, 1): ball((0.07 * sx, -0.128, 0.355), 0.014, P["trim"], seg=8, ring=5)
    with on("mano_R"):   # pico
        tube((-0.17, -0.01, 0.05), (-0.17, -0.01, 0.5), 0.015, 0.015, P["mango"], seg=8)
        tube((-0.17, -0.12, 0.5), (-0.17, 0.12, 0.5), 0.03, 0.012, P["pico"], seg=8)
        cone((-0.17, 0.15, 0.5), 0.012, 0.0, 0.05, P["pico"], seg=6, rot=(-90, 0, 0))
    if lv >= 3:
        with on("mano_L"):   # bolsita de oro
            ball((0.2, -0.01, HAND_Z - 0.03), (0.05, 0.045, 0.06), "cuero", seg=10, ring=6)
            ball((0.2, -0.01, HAND_Z + 0.03), 0.02, "oro_m", seg=8, ring=5)
    armor(P, lv)
    cape(P, lv, "tela_roja" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6:
        with on("cabeza"): crown(P, lv, 0.92, 0.11)
    halo(P, lv, 1.12)


@hero
def barbaro(lv):
    """Barbaro: melena castana, pieles en los hombros, brazos al aire y un martillo enorme."""
    P = paint(dict(piel="piel_m", ropa="cuero", ropa2="tela_roja", pantalon="cuero_osc", botas="cuero", pelo="pelo_castano",
                   piel_animal="barba_gris", martillo="hierro_m", mango="madera_mesa", trim="tela_amarilla",
                   metal="martillo", cristal=("ropa", "ropa2", "pantalon")), lv)
    P["manga"] = P["piel"]
    body(P, lv, torso_w=1.1, sleeve_r=0.05)
    belt(P, r=0.13, key_=P["ropa2"])
    with on("cabeza"):
        hair_cap(P, 0.76)
        for i in range(6):    # melena hacia atras
            a = -1.0 + i * 0.4
            ball((math.sin(a) * 0.17, 0.12 + 0.03 * abs(math.sin(a)), 0.62 + math.cos(a) * 0.06), (0.07, 0.06, 0.09), P["pelo"], seg=8, ring=5)
        torus((0, 0.0, 0.79), 0.2, 0.014, P["ropa2"], seg=18, mseg=4)            # vincha
        for s_ in (-1, 1): ball(head_pt(0.045 * s_, 0.735, 0.005)[0], (0.04, 0.012, 0.012), P["pelo"], seg=8, ring=4)   # cejas
    with on("pecho"):
        for s_ in (-1, 1):    # pieles en los hombros
            ball((0.11 * s_, 0.0, 0.43), (0.085, 0.08, 0.05), P["piel_animal"], seg=10, ring=6)
        tube((0.1, -0.1, 0.43), (-0.09, -0.11, 0.22), 0.016, 0.016, P["ropa"], seg=6, ry=0.5)
        if lv >= 3: badge(Vector((0, -0.125, 0.34)), P, lv, "escudo")
    with on("mano_R"):   # martillo enorme
        tube((-0.17, -0.01, 0.0), (-0.17, -0.01, 0.56), 0.02, 0.02, P["mango"], seg=8)
        rbox((-0.17, -0.01, 0.62), (0.34, 0.19, 0.19), P["martillo"], r=0.22)
        for s_ in (-1, 1): rbox((-0.17 + 0.165 * s_, -0.01, 0.62), (0.03, 0.2, 0.2), P["trim"] if lv >= 4 else "hierro_osc_m", r=0.3, seg=1)
    armor(P, lv, plate=False)
    cape(P, lv, "barba_gris" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6:
        with on("cabeza"): crown(P, lv, 0.88, 0.1)
    halo(P, lv, 1.08)


@hero
def chaman(lv):
    """Chaman del Rayo: mascara de madera levantada sobre la frente, plumas azules, tunica y baston con un rayo."""
    P = paint(dict(piel="piel_o", ropa="tela_azul", ropa2="cuero", pantalon="cuero_osc", botas="cuero", pelo="pelo_negro",
                   mascara="madera_mesa", pluma="cristal_azul", baston="madera_mesa", trim="tela_amarilla",
                   metal="ropa2", cristal=("ropa", "pantalon")), lv)
    body(P, lv, robe=True)
    for s_, n in ((1, "L"), (-1, "R")):
        with on("pierna_" + n):
            ball((0.07 * s_, -0.08, 0.03), (0.05, 0.07, 0.035), P["botas"], seg=12, ring=8)
    belt(P, z=0.2, r=0.15, key_=P["ropa2"])
    with on("cabeza"):
        hair_cap(P, 0.75)
        # vincha tejida con una piedra de rayo al frente y pintura azul en los cachetes
        torus((0, 0.0, 0.8), 0.205, 0.018, P["mascara"], seg=16, mseg=4)
        lathe((0, -0.2, 0.8), [(0.0, 0.0), (0.035, 0.012), (0.0, 0.03)], "rayo_m", seg=6, rot=(90, 0, 0), smooth=False)
        for s_ in (-1, 1):
            for k in range(2):
                rbox(head_pt(0.14 * s_, 0.63 - k * 0.03, 0.0)[0], (0.045, 0.006, 0.01), "cristal_azul", r=0.4, seg=1)
        for i, a in enumerate((-30, -10, 10, 30)):     # plumas azules
            cone((math.sin(math.radians(a)) * 0.1, 0.05, 0.92), 0.025, 0.0, 0.18, P["pluma"] if i % 2 else "tela_blanca", seg=6,
                 rot=(-20, a, 0))
    with on("pecho"):
        for i in range(3):   # collar de dientes
            cone((-0.05 + i * 0.05, -0.11, 0.41), 0.012, 0.0, 0.03, "hueso", seg=5, rot=(180, 0, 0))
        torus((0, 0, 0.43), 0.09, 0.008, P["ropa2"], seg=12, mseg=3)
        if lv >= 3: badge(Vector((0, -0.13, 0.33)), P, lv, "estrella")
    with on("mano_R"):   # baston con rayo de cristal
        tube((-0.17, -0.01, 0.0), (-0.17, -0.01, 0.6), 0.015, 0.012, P["baston"], seg=8)
        prism((-0.17, -0.01, 0.66), [(0.02, 0.12), (-0.05, -0.0), (0.0, -0.0), (-0.03, -0.12), (0.05, 0.02), (0.0, 0.02)], 0.02, "rayo_m")
    armor(P, lv, plate=False)
    cape(P, lv, "tela_azul" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6:
        with on("cabeza"): crown(P, lv, 0.92, 0.1)
    halo(P, lv, 1.14)


@hero
def bardo(lv):
    """Bardo: boina con pluma, capa amarilla corta y laud (acelera a los vecinos con musica)."""
    P = paint(dict(piel="piel_c", ropa="tela_roja", ropa2="tela_amarilla", pantalon="cuero_osc", botas="cuero", guante="cuero",
                   pelo="pelo_castano", boina="tela_roja", pluma="tela_blanca", laud="madera_mesa", trim="tela_amarilla",
                   metal="ropa2", cristal=("ropa", "ropa2", "boina")), lv)
    body(P, lv)
    belt(P)
    with on("cabeza"):
        hair_cap(P, 0.75)
        ball((0.03, 0.0, 0.84), (0.2, 0.19, 0.06), P["boina"], seg=16, ring=7, rot=(0, 12, 0))
        cone((0.12, 0.08, 0.9), 0.02, 0.0, 0.24, P["pluma"], seg=6, rot=(-35, 30, 0))
    with on("pecho"):
        lathe((0, 0, 0.38), [(0.0, 0.0), (0.16, 0.0), (0.13, 0.06), (0.0, 0.07)], P["ropa2"], seg=16)      # cuello de capa
        if lv >= 3: badge(Vector((0, -0.13, 0.32)), P, lv, "estrella")
    with on(bn("laud", "pecho")):   # laud cruzado al frente
        ball((0.03, -0.14, 0.26), (0.08, 0.035, 0.09), P["laud"], seg=12, ring=7, rot=(0, -35, 0))
        tube((0.06, -0.15, 0.31), (0.15, -0.16, 0.43), 0.013, 0.011, P["laud"], seg=6)
        ball((0.035, -0.172, 0.27), (0.025, 0.006, 0.025), "cuero_osc", seg=8, ring=4)
    armor(P, lv, plate=False)
    cape(P, lv, "tela_amarilla" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6: wings(P, lv)
    halo(P, lv, 1.04)


@hero
def artillero(lv):
    """Artillero Gnomo: gorro rojo alto, antiparras, barba blanca corta y canoncito de bronce al hombro."""
    P = paint(dict(piel="piel_c", ropa="tela_verde", ropa2="cuero", pantalon="cuero_osc", botas="cuero_osc", guante="cuero",
                   gorro="tela_roja", barba="barba_gris", lente="cristal_azul", canon="bronce_cl_m", trim="tela_amarilla",
                   metal="canon", cristal=("ropa", "gorro", "pantalon")), lv)
    body(P, lv, torso_w=1.05)
    belt(P)
    pouch(P, 0.08)
    with on("cabeza"):
        cone((0, 0.02, 0.78), 0.2, 0.0, 0.42, P["gorro"], seg=16, rot=(-8, 0, 0))
        torus((0, 0.0, 0.78), 0.19, 0.022, P["ropa2"], seg=18, mseg=5)
        for s_ in (-1, 1):   # antiparras sobre el gorro
            tube((0.065 * s_, -0.17, 0.81), (0.065 * s_, -0.2, 0.81), 0.045, 0.045, "cuero_osc", seg=10)
            ball((0.065 * s_, -0.2, 0.81), (0.035, 0.012, 0.035), P["lente"], seg=10, ring=5)
        p, _ = head_pt(0.0, 0.55, -0.03)
        ball(p + Vector((0, -0.01, -0.02)), (0.12, 0.07, 0.07), P["barba"], seg=12, ring=7)
    with on("mano_R"):   # canoncito al hombro
        tube((-0.16, -0.06, HAND_Z + 0.05), (-0.16, 0.12, 0.54), 0.05, 0.055, P["canon"], seg=12)
        torus((-0.16, -0.065, HAND_Z + 0.045), 0.055, 0.013, P["trim"] if lv >= 3 else "hierro_osc_m", seg=12, mseg=4, rot=(60, 0, 0))
        ball((-0.16, 0.13, 0.55), 0.045, P["canon"], seg=10, ring=6)
    armor(P, lv, plate=False)
    cape(P, lv, "tela_roja" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6:
        with on("cabeza"): crown(P, lv, 0.95, 0.1)
    halo(P, lv, 1.28)


@hero
def monja(lv):
    """Monja de Puno: tunica naranja, coleta, vendas en las manos y collar de cuentas; empuja con la palma."""
    P = paint(dict(piel="piel_m", ropa="tela_naranja", ropa2="tela_amarilla", pantalon="tela_naranja", botas="cuero",
                   pelo="pelo_negro", guante="tela_blanca", cuentas="madera_mesa", trim="tela_amarilla",
                   metal="ropa2", cristal=("ropa", "ropa2", "pantalon")), lv)
    body(P, lv)
    belt(P, key_=P["ropa2"])
    with on("cabeza"):
        hair_cap(P, 0.76)
        ball((0, 0.13, 0.86), (0.06, 0.06, 0.06), P["pelo"], seg=10, ring=6)
        tube((0, 0.16, 0.86), (0.0, 0.25, 0.7), 0.03, 0.015, P["pelo"], seg=8)       # coleta
        torus((0, 0.13, 0.86), 0.05, 0.012, P["ropa2"], seg=10, mseg=3, rot=(70, 0, 0))
    with on("pecho"):
        for i in range(9):   # collar de cuentas
            a = math.pi * (0.15 + 0.7 * i / 8)
            ball((math.cos(a) * 0.1, -math.sin(a) * 0.1 + 0.0, 0.42 - math.sin(a) * 0.03), 0.014, P["cuentas"], seg=6, ring=4)
        tube((0.11, -0.08, 0.44), (-0.1, -0.1, 0.22), 0.02, 0.02, P["ropa2"], seg=6, ry=0.5)     # banda cruzada
        if lv >= 3: badge(Vector((0, -0.13, 0.33)), P, lv, "estrella")
    armor(P, lv, plate=False, pads=lv >= 5)
    cape(P, lv, "tela_amarilla" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6: wings(P, lv)
    halo(P, lv, 1.04)


@hero
def picara(lv):
    """Picara: capucha negra con panuelo sobre la boca, dos dagas con veneno verde."""
    P = paint(dict(piel="piel_c", ropa="tela_negra", ropa2="tela_violeta", pantalon="tela_negra", botas="cuero_osc", guante="cuero_osc",
                   capucha="tela_negra", panuelo="tela_negra", daga="acero_m", trim="tela_amarilla",
                   metal="daga", cristal=("ropa", "capucha", "pantalon")), lv)
    body(P, lv)
    belt(P)
    pouch(P, -0.08)
    with on("cabeza"):
        h = ball((0, 0.02, 0.69), (0.228, 0.222, 0.215), P["capucha"], seg=14, ring=8)
        cut = kit.ball((0, -0.2, 0.66), (0.17, 0.2, 0.15), P["capucha"], seg=12, ring=7)
        boolean(h, cut)
        # panuelo que tapa la boca (los ojos quedan a la vista)
        ball((0, -0.075, 0.585), (0.175, 0.15, 0.06), P["panuelo"], seg=14, ring=7)
        prism((0.0, 0.18, 0.6), [(0.0, 0.0), (0.08, -0.03), (0.14, -0.12), (0.08, -0.1)], 0.02, P["panuelo"])
        if lv >= 3: badge(Vector((0, -0.21, 0.82)), P, lv, "estrella")
    for s_, n in ((-1, "R"), (1, "L")):
        with on("mano_" + n):   # dagas
            rbox((0.17 * s_, -0.05, HAND_Z + 0.01), (0.025, 0.07, 0.03), "cuero_osc", r=0.3, seg=1)
            prism((0.17 * s_, -0.15, HAND_Z + 0.01), [(-0.02, 0.0), (0.02, 0.0), (0.0, -0.12)], 0.012, P["daga"], rot=(90, 0, 0))
            if lv >= 2: ball((0.17 * s_, -0.22, HAND_Z + 0.01), 0.014, "veneno_m", seg=6, ring=4)
    armor(P, lv)
    cape(P, lv, "tela_violeta" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6: wings(P, lv)
    halo(P, lv, 1.0)


@hero
def paladin(lv):
    """Paladin: armadura blanca, yelmo con visor levantado, espada que brilla y escudo con un sol."""
    P = paint(dict(piel="piel_c", ropa="tela_blanca", ropa2="tela_amarilla", pantalon="acero_m", botas="acero_m", guante="acero_m",
                   casco="acero_m", espada="luz_espada", escudo="tela_blanca", trim="tela_amarilla",
                   metal="casco", cristal=("ropa", "escudo", "ropa2")), lv)
    P["manga"] = "acero_m" if lv < 5 else P["casco"]
    body(P, lv)
    belt(P, key_=P["ropa2"])
    with on("cabeza"):
        h = ball((0, 0.01, 0.71), (0.225, 0.218, 0.205), P["casco"], seg=14, ring=8)
        cut = kit.ball((0, -0.2, 0.64), (0.175, 0.2, 0.15), P["casco"], seg=12, ring=7)
        boolean(h, cut)
        rbox((0, -0.16, 0.82), (0.3, 0.06, 0.06), P["casco"], r=0.4, rot=(-30, 0, 0))     # visor levantado
        lathe((0, 0.0, 0.9), [(0.0, 0.0), (0.03, 0.0), (0.02, 0.1), (0.0, 0.11)], P["trim"], seg=8)
    with on("pecho"):
        rbox((0, -0.09, 0.33), (0.2, 0.05, 0.14), P["ropa"], r=0.35)        # sobreveste blanco
        star((0, -0.118, 0.34), 0.04, 0.02, 0.012, P["ropa2"])
    with on("mano_R"):   # espada luminosa
        rbox((-0.17, -0.01, HAND_Z + 0.03), (0.09, 0.03, 0.02), P["trim"], r=0.3)
        rbox((-0.17, -0.01, HAND_Z + 0.24), (0.04, 0.012, 0.38), P["espada"], r=0.25, seg=1)
        cone((-0.17, -0.01, HAND_Z + 0.46), 0.028, 0.0, 0.06, P["espada"], seg=4)
    with on("mano_L"):
        prism((0.21, -0.03, HAND_Z + 0.02), [(-0.1, 0.1), (0.1, 0.1), (0.1, -0.02), (0.0, -0.13), (-0.1, -0.02)], 0.03, P["escudo"],
              rot=(0, 0, 90), bevel=0.01)
        star((0.235, -0.03, HAND_Z + 0.03), 0.05, 0.025, 0.012, P["ropa2"], rot=(0, 0, 90))
    armor(P, lv, plate_z=0.3)
    cape(P, lv, "tela_azul" if lv < 7 else "arcoiris")
    aura(P, lv)
    if lv >= 6: wings(P, lv)
    halo(P, lv, 1.1)


@hero
def hada(lv):
    """Hada de Luz: lucecita con alitas, vestido de luz en campana y varita con estrella; revela espectros."""
    P = paint(dict(piel="piel_c", ropa="luz_hada", ropa2="tela_blanca", pantalon="tela_blanca", botas="tela_amarilla", alas="papel_m",
                   pelo="pelo_rubio", varita="oro_m", trim="tela_amarilla", metal="varita", cristal=("ropa2", "alas")), lv)
    with on("cadera"):
        lathe((0, 0, 0.06), [(0.0, 0.0), (0.13, 0.02), (0.16, 0.06), (0.11, 0.16), (0.0, 0.18)], P["ropa"], seg=16)
    with on("pecho"):
        lathe((0, 0, 0.0), [(0.0, 0.22), (0.09, 0.22), (0.1, 0.32), (0.07, 0.43), (0.0, 0.46)], P["ropa"], seg=14)
    for s_, n in ((1, "L"), (-1, "R")):
        with on("brazo_" + n):
            capsule((0.1 * s_, 0, SHOULDER - 0.01), (0.13 * s_, 0, HAND_Z + 0.06), 0.025, P["piel"], seg=8, ring=2)
        with on("mano_" + n):
            ball((0.135 * s_, 0, HAND_Z + 0.04), 0.03, P["piel"], seg=8, ring=5)
    with on("cabeza"):
        ball(HEAD_C, tuple(HEAD_R), P["piel"], seg=20, ring=12)
        ball((0, 0.04, 0.74), (0.21, 0.19, 0.16), P["pelo"], seg=16, ring=8)
        ball((0, 0.12, 0.86), (0.07, 0.06, 0.07), P["pelo"], seg=10, ring=6)
        face(P, lv)
    with on(bn("alas", "pecho")):
        for s_ in (-1, 1):
            prism((0.04 * s_, 0.11, 0.38), [(0.0, 0.0), (0.16 * s_, 0.12), (0.22 * s_, 0.04), (0.14 * s_, -0.03), (0.2 * s_, -0.1), (0.08 * s_, -0.08)],
                  0.01, P["alas"] if lv < 6 else ("oro_m" if lv == 6 else "arcoiris"), rot=(0, 0, -20 * s_))
    with on("cabeza"):
        if lv >= 3: star((0, -0.1, 0.86), 0.04, 0.018, 0.014, P["trim"], rot=(-20, 0, 0))
        if lv >= 5: torus((0, 0, 0.83), 0.11, 0.012, P["trim"], seg=14, mseg=4, rot=(15, 0, 0))
    with on("pecho"):
        if lv >= 4: torus((0, 0, 0.43), 0.075, 0.014, P["trim"], seg=12, mseg=4)
        if lv >= 3: badge(Vector((0, -0.1, 0.33)), P, lv, "estrella")
    if lv >= 4: cape(P, lv, "tela_blanca" if lv < 7 else "arcoiris")
    with on("mano_R"):
        tube((-0.135, 0, HAND_Z + 0.02), (-0.135, -0.02, HAND_Z + 0.22), 0.007, 0.007, P["varita"], seg=5)
        star((-0.135, -0.02, HAND_Z + 0.25), 0.04, 0.018, 0.016, "brillo_oro")
    aura(P, lv)
    halo(P, lv, 1.0)


@hero
def mimico(lv):
    """Mimico (comodin): un cofre con patitas, tapa que es boca con dientes y lengua; copia al que se fusiona."""
    P = paint(dict(piel="madera_mesa", ropa="madera_mesa", ropa2="hierro_m", pantalon="cuero_osc", botas="cuero_osc",
                   lengua="tela_roja", diente="hueso", trim="tela_amarilla", metal="ropa2", cristal=("ropa", "piel")), lv)
    for s_, n in ((1, "L"), (-1, "R")):
        with on("pierna_" + n):
            capsule((0.08 * s_, 0, 0.12), (0.085 * s_, -0.01, 0.05), 0.04, P["pantalon"], seg=8, ring=2)
            ball((0.085 * s_, -0.04, 0.03), (0.05, 0.07, 0.03), P["botas"], seg=10, ring=6)
    with on("cadera"):
        rbox((0, 0, 0.32), (0.42, 0.32, 0.3), P["ropa"], r=0.12)                        # caja del cofre
        for x in (-0.15, 0.15): rbox((x, 0, 0.32), (0.035, 0.33, 0.31), P["ropa2"] if lv < 4 else P["trim"], r=0.3, seg=1)
        rbox((0, -0.16, 0.4), (0.07, 0.02, 0.08), P["trim"], r=0.3)                       # cerradura
        for i in range(5):
            cone((-0.14 + i * 0.07, -0.13, 0.475), 0.02, 0.0, 0.045, P["diente"], seg=4)  # dientes de abajo
        ball((0, -0.03, 0.47), (0.12, 0.1, 0.025), P["lengua"], seg=10, ring=5)
    with on(bn("tapa", "cabeza")):
        rbox((0, 0.02, 0.6), (0.43, 0.33, 0.12), P["piel"], r=0.3, rot=(-35, 0, 0))     # tapa abierta
        for x in (-0.15, 0.15): rbox((x, 0.02, 0.6), (0.036, 0.34, 0.13), P["ropa2"] if lv < 4 else P["trim"], r=0.3, seg=1, rot=(-35, 0, 0))
        for i in range(5):
            cone((-0.14 + i * 0.07, -0.09, 0.53), 0.02, 0.0, 0.045, P["diente"], seg=4, rot=(180, 0, 0))
        for s_ in (-1, 1):   # ojos sobre la tapa
            e = Vector((0.08 * s_, -0.13, 0.7))
            ball(e, (0.05, 0.03, 0.055), "blanco", seg=10, ring=7)
            ball(e + Vector((0, -0.025, -0.005)), (0.03, 0.012, 0.035), "ojo", seg=8, ring=6)
            ball(e + Vector((-0.01, -0.035, 0.015)), 0.011, "ojo_brillo", seg=6, ring=4)
    if lv >= 3:
        with on("cadera"): badge(Vector((0, -0.165, 0.27)), P, lv, "estrella")
    aura(P, lv)
    if lv >= 6:
        with on(bn("tapa", "cabeza")): crown(P, lv, 0.74, 0.1)
    halo(P, lv, 0.95)


# ------------------------------------------------------------------ animaciones
ATTACK = {}


def anims(hid):
    f = 0
    # idle 0..60: propio de cada juguete (nunca quietos)
    rest_all(0)
    IDLE.get(hid, idle_default)()
    rest_all(60)
    IDLE_AFTER.get(hid, lambda: None)()
    clip("idle", 0, 60, loop=True)
    # aparecer 70..100: llega estirado, aplasta, rebota y se asienta
    rest_all(70)
    pose(70, raiz=dict(scale=(0.8, 0.8, 1.3)), brazo_L=dict(rot=(0, -150, 0)), brazo_R=dict(rot=(0, 150, 0)))
    pose(74, raiz=dict(scale=(1.3, 1.3, 0.65)), brazo_L=dict(rot=(0, -40, 0)), brazo_R=dict(rot=(0, 40, 0)))
    pose(80, raiz=dict(scale=(0.92, 0.92, 1.12), loc=(0, 0, 0.08)), brazo_L=dict(rot=(0, -70, 0)), brazo_R=dict(rot=(0, 70, 0)))
    pose(86, raiz=dict(scale=(1.08, 1.08, 0.93), loc=(0, 0, 0)), brazo_L=dict(rot=(0, -10, 0)), brazo_R=dict(rot=(0, 10, 0)))
    pose(92, raiz=dict(scale=(0.98, 0.98, 1.02)))
    rest_all(100)
    clip("aparecer", 70, 30)
    # atacar 110..130 (propio de cada heroe)
    rest_all(110)
    ev = ATTACK.get(hid, attack_default)(110)
    # vuelta con rebote (comun a todos): se pasa un poco y se asienta
    pose(127, raiz=dict(loc=(0, 0, 0), scale=(0.96, 0.96, 1.05)))
    rest_all(130)
    clip("atacar", 110, 20, ev=[ev])
    # efecto 140..152: se encoge y tiembla (recibe un efecto, p. ej. pegote de chicle)
    rest_all(140)
    pose(143, raiz=dict(scale=(1.12, 1.12, 0.85)), cabeza=dict(rot=(0, 0, 8)))
    pose(146, raiz=dict(scale=(0.95, 0.95, 1.06)), cabeza=dict(rot=(0, 0, -8)))
    pose(149, raiz=dict(scale=(1.03, 1.03, 0.97)), cabeza=dict(rot=(0, 0, 4)))
    rest_all(152)
    clip("efecto", 140, 12)
    # fusionar 160..180: salta girando
    rest_all(160)
    pose(163, raiz=dict(scale=(1.15, 1.15, 0.82)))
    pose(170, raiz=dict(loc=(0, 0, 0.25), rot=(0, 0, 200), scale=(0.9, 0.9, 1.15)), brazo_L=dict(rot=(0, -120, 0)), brazo_R=dict(rot=(0, 120, 0)))
    pose(176, raiz=dict(loc=(0, 0, 0), rot=(0, 0, 360), scale=(1.2, 1.2, 0.8)), brazo_L=dict(rot=(0, -20, 0)), brazo_R=dict(rot=(0, 20, 0)))
    pose(180, raiz=dict(rot=(0, 0, 360), scale=1), brazo_L=dict(rot=(0, 0, 0)), brazo_R=dict(rot=(0, 0, 0)))
    for b in kit.BONE_ORDER:   # ultimo cuadro = reposo (360 = 0)
        e = kit.BONES[b]
    clip("fusionar", 160, 20)
    # festejar 190..214 (loop): salto con brazos arriba
    rest_all(190)
    pose(190, raiz=dict(rot=(0, 0, 0)))
    pose(194, raiz=dict(scale=(1.15, 1.15, 0.85)), brazo_L=dict(rot=(0, -40, 0)), brazo_R=dict(rot=(0, 40, 0)))
    pose(202, raiz=dict(loc=(0, 0, 0.22), scale=(0.92, 0.92, 1.12)), brazo_L=dict(rot=(0, -160, 0)), brazo_R=dict(rot=(0, 160, 0)),
         cabeza=dict(rot=(-10, 0, 0)))
    pose(209, raiz=dict(loc=(0, 0, 0), scale=(1.12, 1.12, 0.88)), brazo_L=dict(rot=(0, -60, 0)), brazo_R=dict(rot=(0, 60, 0)), cabeza=dict(rot=(0, 0, 0)))
    rest_all(214)
    clip("festejar", 190, 24, loop=True)
    # alzado 220..250 (loop): colgando del dedo, patalea
    rest_all(220)
    pose(220, pierna_L=dict(rot=(-25, 0, 0)), pierna_R=dict(rot=(20, 0, 0)), brazo_L=dict(rot=(0, -35, 0)), brazo_R=dict(rot=(0, 35, 0)), raiz=dict(rot=(8, 0, 0)))
    pose(235, pierna_L=dict(rot=(20, 0, 0)), pierna_R=dict(rot=(-25, 0, 0)), brazo_L=dict(rot=(0, -55, 0)), brazo_R=dict(rot=(0, 55, 0)), raiz=dict(rot=(8, 0, 0)))
    pose(250, pierna_L=dict(rot=(-25, 0, 0)), pierna_R=dict(rot=(20, 0, 0)), brazo_L=dict(rot=(0, -35, 0)), brazo_R=dict(rot=(0, 35, 0)), raiz=dict(rot=(8, 0, 0)))
    clip("alzado", 220, 30, loop=True)
    # caminar 260..284 (loop): pasitos de juguete con balanceo
    rest_all(260)
    for f, k in ((260, 1), (266, 0), (272, -1), (278, 0), (284, 1)):
        pose(f, pierna_L=dict(rot=(-28 * k, 0, 0)), pierna_R=dict(rot=(28 * k, 0, 0)),
             brazo_L=dict(rot=(24 * k, 0, 0)), brazo_R=dict(rot=(-24 * k, 0, 0)),
             raiz=dict(loc=(0, 0, 0.035 * (1 - abs(k))), rot=(0, 5 * k, 0)), cabeza=dict(rot=(0, -3 * k, 0)))
    clip("caminar", 260, 24, loop=True)


def breathe(f0=0, f1=60):
    """Respiracion de fondo (se suma a cualquier reposo)."""
    m = (f0 + f1) // 2
    pose(f0 + (m - f0) // 2, pecho=dict(scale=(1.02, 1.02, 1.035)))
    pose(m, pecho=dict(scale=1))
    pose(m + (f1 - m) // 2, pecho=dict(scale=(1.02, 1.02, 1.035)))


def idle_default():
    pose(15, pecho=dict(scale=(1.02, 1.02, 1.035)), cabeza=dict(rot=(0, 3, 4)), raiz=dict(rot=(0, 0, 3)))
    pose(30, pecho=dict(scale=1), cabeza=dict(rot=(0, 0, 0)), raiz=dict(rot=(0, 0, 0)))
    pose(45, pecho=dict(scale=(1.02, 1.02, 1.035)), cabeza=dict(rot=(0, -3, -4)), raiz=dict(rot=(0, 0, -3)))


def idle_soldadito():
    # marcha en el lugar: rodilla arriba, brazo contrario, saltito
    for f, k in ((0, 0), (8, 1), (15, 0), (23, -1), (30, 0), (38, 1), (45, 0), (53, -1), (60, 0)):
        pose(f, pierna_L=dict(rot=(-38 * max(0, k), 0, 0)), pierna_R=dict(rot=(-38 * max(0, -k), 0, 0)),
             brazo_L=dict(rot=(-22 * k, 0, 0)), brazo_R=dict(rot=(22 * k, 0, 0)),
             raiz=dict(loc=(0, 0, 0.03 * abs(k)), scale=(1.0, 1.0, 1.0 + 0.03 * abs(k))))


def idle_arquera():
    # mira a un lado y al otro, atenta
    pose(10, cabeza=dict(rot=(0, 0, 28)), pecho=dict(rot=(0, 0, 8)))
    pose(24, cabeza=dict(rot=(-4, 0, 30)), pecho=dict(rot=(0, 0, 9)))
    pose(34, cabeza=dict(rot=(0, 0, -28)), pecho=dict(rot=(0, 0, -8)))
    pose(50, cabeza=dict(rot=(-4, 0, -30)), pecho=dict(rot=(0, 0, -9)))
    breathe()


def idle_mago():
    # hace girar el sombrero (vuelta entera, constante) y se acaricia la barba con la mano libre
    breathe()
    pose(20, brazo_L=dict(rot=(-40, 0, -25)), cabeza=dict(rot=(4, 0, 6)))
    pose(40, brazo_L=dict(rot=(-35, 0, -20)), cabeza=dict(rot=(4, 0, -6)))


def idle_mago_after():
    key("sombrero", 0, rot=(0, 0, 0), interp="LINEAR")
    key("sombrero", 30, rot=(0, 0, 180), interp="LINEAR")
    key("sombrero", 60, rot=(0, 0, 360), interp="LINEAR")


def idle_granjero():
    # golpea el pie y asiente, mascando la paja
    for f, k in ((0, 0), (7, 1), (14, 0), (21, 1), (28, 0), (45, 0)):
        pose(f, pierna_R=dict(rot=(-14 * k, 0, 0), loc=(0, 0, 0.015 * k)), cabeza=dict(rot=(6 * k, 0, 0)))
    pose(38, pecho=dict(rot=(0, 0, 10)), cabeza=dict(rot=(0, 0, 12)))
    pose(52, pecho=dict(rot=(0, 0, 0)), cabeza=dict(rot=(0, 0, 0)))


def idle_dino():
    # mueve la cola de un lado al otro y se balancea
    for f, k in ((0, 0), (8, 1), (23, -1), (38, 1), (53, -1), (60, 0)):
        pose(f, cola=dict(rot=(0, 0, 28 * k)), raiz=dict(rot=(0, 0, -5 * k)), cabeza=dict(rot=(0, 4 * k, 6 * k)))


def idle_robot():
    # tiembla de a ratitos (la cuerda), la llave gira sin parar
    for f, k in ((0, 0), (5, 1), (10, -1), (15, 1), (20, 0), (40, 0), (44, 1), (48, -1), (52, 0)):
        pose(f, raiz=dict(rot=(0, 2 * k, 0)), cabeza=dict(rot=(0, 0, 6 * k)))


def idle_robot_after():
    key("llave", 0, rot=(0, 0, 0), interp="LINEAR")
    key("llave", 30, rot=(0, 180, 0), interp="LINEAR")
    key("llave", 60, rot=(0, 360, 0), interp="LINEAR")


def idle_osito():
    # se mece de lado a lado abrazandose
    for f, k in ((0, 0), (15, 1), (45, -1), (60, 0)):
        pose(f, raiz=dict(rot=(0, 7 * k, 0)), cabeza=dict(rot=(0, 8 * k, 0)),
             brazo_L=dict(rot=(-30, 0, -30)), brazo_R=dict(rot=(-30, 0, 30)))


def idle_capitan():
    # se balancea de talon a punta mirando el horizonte
    for f, k in ((0, 0), (15, 1), (45, -1), (60, 0)):
        pose(f, raiz=dict(rot=(5 * k, 0, 0)), cabeza=dict(rot=(-4 * k, 0, 10 * k)))
    breathe()


def idle_bailarina():
    # gira sobre el pedestal como en la cajita de musica, con los brazos arriba
    for f, k in ((0, 0), (15, 1), (45, -1), (60, 0)):
        pose(f, raiz=dict(rot=(0, 0, 38 * k)), brazo_L=dict(rot=(0, -140 - 10 * k, 0)), brazo_R=dict(rot=(0, 140 - 10 * k, 0)),
             cabeza=dict(rot=(0, 6 * k, 0)))


def idle_ninja():
    # agachado y saltando en el lugar, listo
    for f, k in ((0, 0), (7, 1), (15, 0), (22, 1), (30, 0), (37, 1), (45, 0), (52, 1), (60, 0)):
        pose(f, raiz=dict(loc=(0, 0, 0.035 * k), scale=(1.0 + 0.04 * (1 - k), 1.0 + 0.04 * (1 - k), 1.0 - 0.05 * (1 - k))),
             brazo_L=dict(rot=(-30, 0, -15)), brazo_R=dict(rot=(-30, 0, 15)))


def idle_astronauta():
    # flota despacito como sin gravedad
    for f, k in ((0, 0), (15, 1), (45, -1), (60, 0)):
        pose(f, raiz=dict(loc=(0, 0, 0.03 + 0.03 * k), rot=(4 * k, 0, 0)), brazo_L=dict(rot=(0, -20 - 8 * k, 0)), brazo_R=dict(rot=(0, 20 - 8 * k, 0)))


def idle_hada():
    # flota arriba y abajo; las alas aletean rapido
    for f, k in ((0, 0), (15, 1), (45, -1), (60, 0)):
        pose(f, raiz=dict(loc=(0, 0, 0.06 + 0.04 * k)))
    for f in range(0, 61, 5):
        pose(f, alas=dict(rot=(0, 0, 0), scale=(1.0 if (f // 5) % 2 == 0 else 0.7, 1, 1)))


def idle_muneco():
    # cabeza floja de trapo que da vueltitas
    for f, (x, y) in ((0, (0, 0)), (15, (8, 0)), (30, (0, 8)), (45, (-8, 0)), (60, (0, 0))):
        pose(f, cabeza=dict(rot=(x, y, 0)), raiz=dict(rot=(0, y * 0.4, 0)))


def idle_barbaro():
    # se balancea pesado y apoya el martillo en el hombro
    for f, k in ((0, 0), (15, 1), (45, -1), (60, 0)):
        pose(f, raiz=dict(rot=(0, 0, -5 * k)), pecho=dict(rot=(0, 0, 6 * k), scale=(1.0 + 0.02 * abs(k), 1.0 + 0.02 * abs(k), 1.0)),
             brazo_R=dict(rot=(-25 - 8 * k, 0, 0)), cabeza=dict(rot=(0, 3 * k, 5 * k)))


def idle_chaman():
    # chispas en la mano: tiembla de a ratitos y levanta el baston
    for f, k in ((0, 0), (5, 1), (10, -1), (15, 1), (20, 0), (40, 0), (44, 1), (48, -1), (52, 0)):
        pose(f, raiz=dict(rot=(0, 2 * k, 0)), cabeza=dict(rot=(0, 0, 6 * k)))
    pose(30, brazo_R=dict(rot=(-35, 0, 0)))
    pose(55, brazo_R=dict(rot=(0, 0, 0)))


def idle_bardo():
    # rasguea el laud y lleva el ritmo con la cabeza
    for f, k in ((0, 0), (8, 1), (15, 0), (23, 1), (30, 0), (38, 1), (45, 0), (53, 1), (60, 0)):
        pose(f, brazo_R=dict(rot=(-40 - 18 * k, 0, 15)), brazo_L=dict(rot=(-50, 0, -20)), cabeza=dict(rot=(5 * k, 0, 4 * k)),
             laud=dict(rot=(0, 0, 3 * k)))


def idle_mimico():
    # abre y cierra la tapa como si masticara, con saltitos
    for f, k in ((0, 0), (8, 1), (15, 0), (30, 0), (38, 1), (45, 0), (60, 0)):
        pose(f, tapa=dict(rot=(-25 * k, 0, 0)), raiz=dict(loc=(0, 0, 0.03 * k), scale=(1.0 + 0.04 * k, 1.0 + 0.04 * k, 1.0 - 0.05 * k)))


IDLE = dict(lancero=idle_soldadito, arquera=idle_arquera, mago=idle_mago, enano=idle_granjero, barbaro=idle_barbaro,
            chaman=idle_chaman, bardo=idle_bardo, artillero=idle_capitan, monja=idle_ninja, picara=idle_arquera,
            paladin=idle_capitan, hada=idle_hada, mimico=idle_mimico)
IDLE_AFTER = dict(mago=idle_mago_after)


def attack_default(f0):
    # anticipacion: se echa para atras y se aplasta; golpe: se estira hacia adelante; vuelta
    pose(f0 + 4, raiz=dict(loc=(0, 0.05, 0), rot=(8, 0, 0), scale=(1.12, 1.12, 0.88)), brazo_R=dict(rot=(45, 0, 0)), brazo_L=dict(rot=(30, 0, 0)))
    pose(f0 + 8, raiz=dict(loc=(0, -0.09, 0), rot=(-12, 0, 0), scale=(0.9, 0.9, 1.14)), brazo_R=dict(rot=(-100, 0, 0)), brazo_L=dict(rot=(-60, 0, 0)))
    pose(f0 + 14, raiz=dict(loc=(0, -0.02, 0), rot=(0, 0, 0), scale=(1.04, 1.04, 0.97)), brazo_R=dict(rot=(-30, 0, 0)), brazo_L=dict(rot=(-10, 0, 0)))
    return 8


def _att_soldadito(f0):
    # lanzazo rapido: se echa atras (anticipacion), estocada hacia adelante, rebote
    pose(f0 + 4, raiz=dict(loc=(0, 0.04, 0), rot=(6, 0, 0), scale=(1.06, 1.06, 0.94)), brazo_R=dict(rot=(35, 0, 0)), pecho=dict(rot=(0, 0, 12)))
    pose(f0 + 7, raiz=dict(loc=(0, -0.08, 0), rot=(-10, 0, 0), scale=(0.94, 0.94, 1.06)), brazo_R=dict(rot=(-85, 0, 0)), pecho=dict(rot=(0, 0, -10)))
    pose(f0 + 12, raiz=dict(loc=(0, -0.02, 0), rot=(-3, 0, 0), scale=(1.03, 1.03, 0.97)), brazo_R=dict(rot=(-40, 0, 0)), pecho=dict(rot=(0, 0, 0)))
    return 7


def _att_arquera(f0):
    # tensa el arco (brazo izquierdo al frente, derecho tira hacia atras), suelta
    pose(f0 + 5, brazo_L=dict(rot=(-80, 0, 0)), brazo_R=dict(rot=(-70, 0, 25)), pecho=dict(rot=(0, 0, 15)), raiz=dict(scale=(1.04, 1.04, 0.96)))
    pose(f0 + 9, brazo_L=dict(rot=(-85, 0, 0)), brazo_R=dict(rot=(-40, 0, 55)), pecho=dict(rot=(0, 0, 18)), raiz=dict(scale=(0.97, 0.97, 1.03)))
    pose(f0 + 11, brazo_L=dict(rot=(-88, 0, 0)), brazo_R=dict(rot=(-20, 0, -20)), pecho=dict(rot=(-4, 0, 10)), raiz=dict(loc=(0, 0.03, 0)))
    pose(f0 + 16, brazo_L=dict(rot=(-30, 0, 0)), brazo_R=dict(rot=(0, 0, 0)), pecho=dict(rot=(0, 0, 0)), raiz=dict(loc=(0, 0, 0), scale=1))
    return 10


def _att_mago(f0):
    # levanta la varita por encima de la cabeza (anticipacion) y la baja al frente lanzando la bola
    pose(f0 + 5, brazo_R=dict(rot=(0, 150, 0)), raiz=dict(scale=(0.95, 0.95, 1.08)), cabeza=dict(rot=(-10, 0, 0)))
    pose(f0 + 9, brazo_R=dict(rot=(-95, 0, 0)), raiz=dict(loc=(0, -0.04, 0), scale=(1.08, 1.08, 0.92)), cabeza=dict(rot=(8, 0, 0)))
    pose(f0 + 14, brazo_R=dict(rot=(-50, 0, 0)), raiz=dict(loc=(0, 0, 0), scale=(0.98, 0.98, 1.02)), cabeza=dict(rot=(0, 0, 0)))
    return 9


def _att_granjero(f0):
    # estira la honda hacia atras y suelta la piedrita
    pose(f0 + 5, brazo_R=dict(rot=(30, 0, -20)), pecho=dict(rot=(0, 0, 18)), raiz=dict(scale=(1.04, 1.04, 0.96)))
    pose(f0 + 9, brazo_R=dict(rot=(-95, 0, 0)), pecho=dict(rot=(0, 0, -8)), raiz=dict(loc=(0, -0.03, 0), scale=(0.96, 0.96, 1.05)))
    pose(f0 + 14, brazo_R=dict(rot=(-40, 0, 0)), pecho=dict(rot=(0, 0, 0)), raiz=dict(loc=(0, 0, 0), scale=1))
    return 9


def _att_dino(f0):
    # salta y cae con un pisoton (aplastado fuerte al aterrizar)
    pose(f0 + 3, raiz=dict(scale=(1.2, 1.2, 0.8)), brazo_L=dict(rot=(0, -40, 0)), brazo_R=dict(rot=(0, 40, 0)))
    pose(f0 + 8, raiz=dict(loc=(0, 0, 0.22), scale=(0.9, 0.9, 1.15)), cabeza=dict(rot=(-15, 0, 0)))
    pose(f0 + 11, raiz=dict(loc=(0, 0, 0), scale=(1.35, 1.35, 0.68)), cabeza=dict(rot=(10, 0, 0)))
    pose(f0 + 15, raiz=dict(scale=(0.95, 0.95, 1.06)), cabeza=dict(rot=(0, 0, 0)), brazo_L=dict(rot=(0, 0, 0)), brazo_R=dict(rot=(0, 0, 0)))
    return 11


def _att_robot(f0):
    # se le traba la cuerda y suelta el rayo con los brazos adelante
    pose(f0 + 4, pecho=dict(rot=(0, 0, 20)), raiz=dict(scale=(1.05, 1.05, 0.95)))
    pose(f0 + 6, pecho=dict(rot=(0, 0, -20)))
    pose(f0 + 9, brazo_L=dict(rot=(-90, 0, 0)), brazo_R=dict(rot=(-90, 0, 0)), pecho=dict(rot=(0, 0, 0)), raiz=dict(scale=(0.96, 0.96, 1.06)))
    pose(f0 + 15, brazo_L=dict(rot=(-30, 0, 0)), brazo_R=dict(rot=(-30, 0, 0)), raiz=dict(scale=1))
    return 9


def _att_osito(f0):
    pose(f0 + 6, brazo_L=dict(rot=(0, -120, 0)), brazo_R=dict(rot=(0, 120, 0)), raiz=dict(loc=(0, 0, 0.08)))
    pose(f0 + 12, brazo_L=dict(rot=(0, -20, 0)), brazo_R=dict(rot=(0, 20, 0)), raiz=dict(loc=(0, 0, 0)))
    return 6


def _att_capitan(f0):
    # apunta el canon, retrocede con el disparo
    pose(f0 + 5, brazo_R=dict(rot=(-40, 0, 0)), raiz=dict(scale=(1.04, 1.04, 0.96)))
    pose(f0 + 8, brazo_R=dict(rot=(-55, 0, 0)), raiz=dict(loc=(0, 0.07, 0), scale=(0.95, 0.95, 1.06), rot=(8, 0, 0)))
    pose(f0 + 15, brazo_R=dict(rot=(-10, 0, 0)), raiz=dict(loc=(0, 0, 0), scale=1, rot=(0, 0, 0)))
    return 8


def _att_bailarina(f0):
    # giro de cajita de musica
    pose(f0 + 3, raiz=dict(scale=(1.05, 1.05, 0.95)), brazo_L=dict(rot=(0, -150, 0)), brazo_R=dict(rot=(0, 150, 0)))
    pose(f0 + 12, raiz=dict(rot=(0, 0, 360), scale=(0.97, 0.97, 1.04)), brazo_L=dict(rot=(0, -160, 0)), brazo_R=dict(rot=(0, 160, 0)))
    pose(f0 + 16, raiz=dict(rot=(0, 0, 360), scale=1), brazo_L=dict(rot=(0, -60, 0)), brazo_R=dict(rot=(0, 60, 0)))
    return 10


def _att_ninja(f0):
    pose(f0 + 4, brazo_R=dict(rot=(0, 130, 0)), pecho=dict(rot=(0, 0, 20)), raiz=dict(scale=(1.05, 1.05, 0.95)))
    pose(f0 + 7, brazo_R=dict(rot=(-95, 0, -30)), pecho=dict(rot=(0, 0, -15)), raiz=dict(loc=(0, -0.04, 0.04)))
    pose(f0 + 13, brazo_R=dict(rot=(-20, 0, 0)), pecho=dict(rot=(0, 0, 0)), raiz=dict(loc=(0, 0, 0), scale=1))
    return 7


def _att_astronauta(f0):
    pose(f0 + 5, brazo_R=dict(rot=(-90, 0, 0)), raiz=dict(scale=(1.03, 1.03, 0.97)))
    pose(f0 + 8, brazo_R=dict(rot=(-100, 0, 0)), raiz=dict(loc=(0, 0.04, 0), scale=(0.97, 0.97, 1.04)))
    pose(f0 + 15, brazo_R=dict(rot=(-30, 0, 0)), raiz=dict(loc=(0, 0, 0), scale=1))
    return 8


def _att_hada(f0):
    pose(f0 + 5, brazo_R=dict(rot=(0, 140, 0)), raiz=dict(loc=(0, 0, 0.1)))
    pose(f0 + 9, brazo_R=dict(rot=(-90, 0, 0)), raiz=dict(loc=(0, -0.03, 0.04)))
    pose(f0 + 15, brazo_R=dict(rot=(-20, 0, 0)), raiz=dict(loc=(0, 0, 0)))
    return 9


ATTACK.update(lancero=_att_soldadito, arquera=_att_arquera, mago=_att_mago, enano=_att_granjero, barbaro=_att_dino,
              chaman=_att_robot, bardo=_att_osito, artillero=_att_capitan, monja=_att_ninja, picara=_att_ninja,
              paladin=_att_astronauta, hada=_att_hada, mimico=attack_default)


# ------------------------------------------------------------------ armado / exportacion
def grow_head():
    """Cabeza (y lo que lleva encima) 15 % mas grande, desde el cuello: proporcion chibi de 50 %."""
    kit.bpy.context.view_layer.update()          # matrix_world al dia (si no, vale la identidad de recien creado)
    M = kit.Matrix.Translation((0, 0, NECK_Z)) @ kit.Matrix.Scale(HEAD_K, 4) @ kit.Matrix.Translation((0, 0, -NECK_Z))
    for b in ("cabeza", "sombrero"):
        for o in kit.PARTS.get(b, []):
            o.matrix_world = M @ o.matrix_world
    kit.bpy.context.view_layer.update()


def build(hid, lv):
    kit.clear_parts()
    HEROES[hid](lv)
    grow_head()


def produce(hid, export=True):
    kit.reset()
    kit.LOD[0] = 0.56         # presupuesto: <= 3000 triangulos por heroe en el nivel 7
    skeleton(hid)
    anims(hid)
    kit.save_palette()
    tris = {}
    for lv in range(1, 8):
        kit.sc.frame_set(0)
        build(hid, lv)
        if export:
            tris[lv] = kit.export_model("h_%s_L%d" % (hid, lv))
    if export:
        kit.export_anims("h_%s_anim" % hid)
    return tris


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--no-export", action="store_true")
    a = ap.parse_args(argv)
    ids = [x for x in a.only.split(",") if x] or list(HEROES)
    report = {}
    for h in ids:
        report[h] = produce(h, not a.no_export)
    for h, t in report.items():
        print("TRIS", h, t, "max", max(t.values()) if t else 0)
