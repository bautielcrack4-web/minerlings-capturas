"""El personal de Curio Barn (DIRECCION_CREATIVA 8), con las mismas piezas y pivotes que Pipo (pipo.py) para usar su
esqueleto y animaciones (Rarezas.View.Hero). Cada uno con cara, uniforme y la herramienta de su puesto en "tool"
(colgada de la mano derecha).

Piezas: body, legL, legR, armL, armR, acc (pelo, sombrero, anteojos), hair (vacio salvo el color), tool.
  staff_receptionist  Martina: saco celeste, rodete y auricular
  staff_cleaner       Don Ramon: mameluco gris, escoba y balde, bigote canoso
  staff_seeker        Tito: sombrero de explorador, camisa caqui y bermudas, binoculares
  staff_driver        chofer: gorra con visera, saco oscuro con botones dorados, guantes
  staff_restorer      Profe Bruno: delantal, lupa en la frente, pincel
  staff_appraiser     Sra. Olga: lentes con cadena, cardigan lila, libreta
  staff_guard         Toro: uniforme azul, gorra con escudo y silbato, ancho
  staff_star          Lucho: traje a rayas, moño, pelo con gomina

Uso: blender -b -P staff.py -- [--only cleaner,...] [--preview carpeta]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import sphere, cyl, torus, tube
from people import U, S, C, B, L, H, HAIR, R, HC, SKIN, ears

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
GOLD = "d9a441"


def torso(shirt, pants, wide=1.0, plaid=None):
    o = [L("cadera", 0, 0.17, 0, [(0, 0), (0.152 * wide, 0), (0.168 * wide, 0.05), (0.162 * wide, 0.09), (0, 0.09)], pants)]
    t = L("torso", 0, 0.24, 0, [(0, 0), (0.166 * wide, 0), (0.175 * wide, 0.06), (0.156 * wide, 0.15), (0.105 * wide, 0.21), (0.05, 0.235), (0, 0.24)], shirt)
    if plaid:
        lib.dense(t, 1)
        lib.setmat(t, shirt, plaid=plaid)
    o.append(t)
    o.append(C("cuello", 0, 0.44, 0, 0.05, 0.05, SKIN))
    return o


def leg(side, pants, shoe, shorts=False, wide=1.0):
    x = side * 0.075 * wide
    if shorts:
        o = [C("bermuda", x, 0.1, 0, 0.068, 0.1, pants, bev=0.02), C("pierna", x, 0.03, 0, 0.05, 0.1, SKIN, bev=0.015)]
        o.append(C("media", x, 0.0, 0, 0.054, 0.06, "f4efe2", bev=0.01))
    else:
        o = [C("pierna", x, 0.03, 0, 0.064, 0.17, pants, bev=0.02)]
    o.append(S("zapato", x, 0.01, 0.03, 0.073, shoe, scale=(1, 1.4, 0.8)))
    o.append(B("suela", x, -0.035, 0.03, 0.14, 0.03, 0.19, r=0.012, col="3a2a22"))
    return o


def arm(side, sleeve, cuff=None, hand=SKIN, plaid=None, wide=1.0):
    x = side * 0.17 * wide
    a = C("brazo", x, 0.26, 0, 0.047 * wide, 0.18, sleeve, bev=0.02)
    h = S("hombro", x, 0.43, 0, 0.054 * wide, sleeve)
    if plaid:
        for o_ in (a, h):
            lib.dense(o_, 1)
            lib.setmat(o_, sleeve, plaid=plaid)
    o = [a, h]
    if cuff:
        o.append(C("puño", x, 0.25, 0, 0.05 * wide, 0.03, cuff, bev=0.008))
    o.append(S("mano", x, 0.215, 0, 0.056, hand))
    o.append(S("pulgar", x - side * 0.035, 0.235, 0.03, 0.022, hand))
    return o


def hair_cap(col, y_cut=0.42, z_cut=-0.3, scale=(1, 1, 1)):
    return HAIR("pelo", 0, 0.0, -0.02, R * 1.05, col, scale=scale, y_cut=y_cut, z_cut=z_cut)


def eye_glasses(col, r=0.14, chain=None):
    o = []
    for s_ in (-1, 1):
        o.append(torus("anteojo", U(HC[0] + s_ * R * 0.34, HC[1] + R * 0.05, HC[2] + R * 0.95), R * r, 0.01, col, rot=(90, 0, 0)))
    o.append(tube("puente", [U(-R * 0.18, HC[1] + R * 0.08, HC[2] + R * 0.99), U(0, HC[1] + R * 0.11, HC[2] + R * 1.01), U(R * 0.18, HC[1] + R * 0.08, HC[2] + R * 0.99)], 0.007, col))
    for s_ in (-1, 1):
        o.append(tube("patilla", [U(s_ * R * 0.55, HC[1] + R * 0.07, HC[2] + R * 0.86), U(s_ * R * 0.92, HC[1] + R * 0.07, HC[2] + R * 0.2)], 0.007, col))
        if chain:
            # cadenita que cuelga de las patillas hasta el cuello
            o.append(tube("cadena", [U(s_ * R * 0.92, HC[1] + R * 0.05, HC[2] + R * 0.2), U(s_ * R * 0.86, HC[1] - R * 0.55, HC[2] + R * 0.3),
                                      U(s_ * R * 0.4, HC[1] - R * 1.0, HC[2] + R * 0.55)], 0.005, chain))
    return o


# ================================================================ puestos
def receptionist():
    blazer = "7ec8f0"
    body = torso("fbf6ee", "2e3f66")
    body.append(L("saco", 0, 0.235, 0.004, [(0, 0), (0.176, 0), (0.18, 0.06), (0.16, 0.15), (0.11, 0.205), (0, 0.21)], blazer))
    body.append(B("solapa", 0, 0.37, 0.16, 0.07, 0.15, 0.02, r=0.008, col="fbf6ee", rx=-8))
    for s_ in (-1, 1):
        body.append(B("solapa_s", s_ * 0.05, 0.37, 0.158, 0.035, 0.12, 0.016, r=0.006, col="5aa8d8", rz=s_ * 12, rx=-8))
    body.append(S("prendedor", -0.08, 0.36, 0.158, 0.02, GOLD, metal=0.6, rough=0.3))
    body.append(B("credencial", 0.075, 0.31, 0.162, 0.06, 0.04, 0.008, r=0.004, col="fbf6ee"))
    hair = "4a2a1e"
    acc = [hair_cap(hair, y_cut=0.32, z_cut=-0.25)]
    acc.append(H("rodete", 0, R * 0.95, -R * 0.35, R * 0.42, hair, scale=(1, 0.9, 1)))
    acc.append(torus("hebilla", U(0, HC[1] + R * 0.82, HC[2] - R * 0.35), R * 0.3, 0.012, "e0594a", rot=(-30, 0, 0)))
    # auricular con microfono
    acc.append(torus("vincha", U(*HC), R * 1.04, 0.012, "3a3a44", rot=(0, 90, 0), scale=(1, 1, 1)))
    acc.append(C("auricular", R * 1.0, HC[1], HC[2], R * 0.22, 0.05, "3a3a44", rz=90, bev=0.01))
    acc.append(tube("micro", [U(R * 1.02, HC[1] - R * 0.1, HC[2] + R * 0.1), U(R * 0.85, HC[1] - R * 0.45, HC[2] + R * 0.65), U(R * 0.45, HC[1] - R * 0.55, HC[2] + R * 0.9)], 0.008, "3a3a44"))
    acc.append(S("bocina", R * 0.42, HC[1] - R * 0.55, HC[2] + R * 0.92, 0.018, "2b2a30"))
    acc += ears()
    return dict(body=body, legs=("2e3f66", "2b2026"), arms=(blazer, "fbf6ee"), acc=acc, tool=[])


def cleaner():
    grey, grey_d = "8d929e", "6a6f7a"
    body = torso(grey, grey)
    body.append(B("cierre", 0, 0.33, 0.168, 0.015, 0.2, 0.01, r=0.004, col="c8ccd4", metal=0.5))
    body.append(B("bolsillo", -0.07, 0.36, 0.165, 0.06, 0.05, 0.012, r=0.008, col=grey_d))
    body.append(B("trapo", 0.11, 0.2, 0.12, 0.05, 0.12, 0.02, r=0.01, col="f0cf6a", rz=8))
    body.append(torus("cinto", U(0, 0.25, 0), 0.172, 0.012, grey_d))
    acc = [hair_cap("c8c4c0", y_cut=0.55, z_cut=-0.45)]
    acc.append(H("pelado", 0, R * 0.9, -R * 0.05, R * 0.55, SKIN, scale=(1, 0.35, 1)))
    acc.append(H("bigote", 0, -R * 0.12, R * 0.97, R * 0.14, "e8e4de", scale=(1.7, 0.5, 0.5)))
    for s_ in (-1, 1):
        acc.append(H("ceja", s_ * R * 0.32, R * 0.36, R * 0.9, R * 0.1, "e8e4de", scale=(1.6, 0.6, 0.6)))
    acc += ears()
    # escoba: palo largo y cepillo (cuelga de la mano derecha)
    tool = [C("palo", 0.17, -0.25, 0.06, 0.016, 0.6, "c98d55", bev=0.005)]
    tool.append(B("cepillo", 0.17, -0.28, 0.06, 0.2, 0.05, 0.06, r=0.015, col="b0382a"))
    tool.append(B("cerdas", 0.17, -0.33, 0.06, 0.19, 0.06, 0.05, r=0.01, col="e8c97a"))
    # balde en la otra mano: va en el cuerpo, al costado
    body.append(C("balde", -0.26, 0.0, 0.06, 0.085, 0.13, "4a8fe0", r2=0.07, bev=0.01))
    body.append(C("agua", -0.26, 0.12, 0.06, 0.078, 0.012, "a8dff0"))
    body.append(torus("asa", U(-0.26, 0.16, 0.06), 0.08, 0.006, "c8ccd4", rot=(0, 0, 90)))
    for k in range(3):
        body.append(S("espuma", -0.26 + (k - 1) * 0.04, 0.135, 0.06 + (k % 2) * 0.03, 0.03, "ffffff"))
    return dict(body=body, legs=(grey, "3a3a44"), arms=(grey, grey_d), acc=acc, tool=tool)


def seeker():
    khaki, khaki_d = "d9c08a", "b39a62"
    body = torso("c9b27a", khaki_d)
    for s_ in (-1, 1):
        body.append(B("bolsillo", s_ * 0.075, 0.36, 0.163, 0.06, 0.06, 0.014, r=0.008, col=khaki_d))
    body.append(tube("correa", [U(-0.12, 0.43, 0.08), U(0.0, 0.32, 0.17), U(0.13, 0.2, 0.12)], 0.012, "7a4626"))
    body.append(B("binoculares", 0.04, 0.27, 0.19, 0.11, 0.06, 0.06, r=0.02, col="2b2a30"))
    body.append(B("bolso", 0.15, 0.2, 0.06, 0.09, 0.1, 0.12, r=0.02, col="a8673a"))
    acc = [hair_cap("2b2026", y_cut=0.55, z_cut=-0.4)]
    acc.append(H("casco", 0, R * 0.38, -0.01, R * 1.08, "e8d8a8", scale=(1, 0.75, 1.05)))
    acc.append(C("ala", 0, HC[1] + R * 0.32, HC[2] - 0.01, R * 1.42, 0.022, "e8d8a8", seg=32, bev=0.01, rx=-6))
    acc.append(C("banda", 0, HC[1] + R * 0.36, HC[2] - 0.01, R * 1.03, 0.045, "7a4626", r2=R * 1.0, rx=-6))
    acc.append(H("pluma", R * 0.8, R * 0.62, -R * 0.2, R * 0.1, "e0594a", scale=(0.4, 2.0, 0.4)))
    acc += ears()
    return dict(body=body, legs=(khaki, "8a5530"), shorts=True, arms=("c9b27a", khaki_d), acc=acc, tool=[])


def driver():
    coat = "2e3a56"
    body = torso(coat, "2e3a56")
    for k in range(3):
        for s_ in (-1, 1):
            body.append(S("boton", s_ * 0.045, 0.39 - k * 0.07, 0.17, 0.014, GOLD, metal=0.6, rough=0.3))
    body.append(B("cuello_c", 0, 0.445, 0.07, 0.15, 0.04, 0.06, r=0.015, col="243048"))
    acc = [hair_cap("6a4026", y_cut=0.5, z_cut=-0.4)]
    acc.append(C("gorra", 0, HC[1] + R * 0.45, HC[2] - 0.01, R * 1.0, R * 0.32, coat, r2=R * 1.12, seg=32, bev=0.02))
    acc.append(C("banda", 0, HC[1] + R * 0.44, HC[2] - 0.01, R * 1.01, 0.04, "1a2236"))
    acc.append(C("visera", 0, HC[1] + R * 0.46, HC[2] + R * 0.74, R * 0.5, 0.02, "1a1a20", rx=-14, seg=20, rough=0.25))
    acc.append(S("escudo", 0, HC[1] + R * 0.62, HC[2] + R * 1.02, 0.03, GOLD, metal=0.7, rough=0.25, scale=(1, 1, 0.4)))
    acc += ears()
    return dict(body=body, legs=(coat, "1a1a20"), arms=(coat, "243048"), hand="f4efe2", acc=acc, tool=[])


def restorer():
    apron = "a8673a"
    body = torso("e8e0d0", "5a4a3e")
    body.append(B("delantal", 0, 0.27, 0.165, 0.26, 0.3, 0.025, r=0.02, col=apron, rx=-4))
    body.append(B("bolsillo", 0, 0.22, 0.18, 0.15, 0.07, 0.012, r=0.01, col="7a4626"))
    for k, c in enumerate(("e0594a", "4a8fe0", "f0cf6a")):
        body.append(C("pincel", -0.04 + k * 0.035, 0.24, 0.185, 0.008, 0.08, c))
    for s_ in (-1, 1):
        body.append(tube("tira", [U(s_ * 0.1, 0.4, 0.16), U(s_ * 0.07, 0.45, 0.04)], 0.012, apron))
    for k in range(5):
        body.append(S("mancha", -0.09 + k * 0.05, 0.16 + (k % 2) * 0.12, 0.185, 0.014, ("e0594a", "4a8fe0", "5cc84a", "f0cf6a", "b06ef0")[k], scale=(1, 1, 0.3)))
    acc = [hair_cap("9a9490", y_cut=0.6, z_cut=-0.45)]
    acc.append(H("pelado", 0, R * 0.85, R * 0.1, R * 0.62, SKIN, scale=(1, 0.38, 1)))
    acc.append(H("barba", 0, -R * 0.5, R * 0.75, R * 0.42, "9a9490", scale=(1.2, 0.8, 0.6)))
    # lupa de joyero en la frente
    acc.append(C("lupa", R * 0.1, HC[1] + R * 0.6, HC[2] + R * 0.85, R * 0.18, 0.06, "3a3a44", rx=70, metal=0.5, rough=0.3))
    acc.append(C("vidrio", R * 0.1, HC[1] + R * 0.63, HC[2] + R * 0.91, R * 0.15, 0.01, "bfe8f6", rx=70, rough=0.05, metal=0.2))
    acc.append(torus("vincha", U(HC[0], HC[1] + R * 0.25, HC[2]), R * 1.0, 0.012, "3a3a44", rot=(18, 0, 0)))
    acc += ears()
    tool = [C("mango", 0.17, 0.1, 0.06, 0.012, 0.13, "c98d55", rx=90), C("pelo", 0.17, 0.1, 0.19, 0.016, 0.04, "6a4026", rx=90, r2=0.004)]
    return dict(body=body, legs=("5a4a3e", "5a3420"), arms=("e8e0d0", "c8c0b0"), acc=acc, tool=tool)


def appraiser():
    lilac = "a98fd0"
    body = torso(lilac, "5a4a6a")
    body.append(B("blusa", 0, 0.38, 0.16, 0.08, 0.12, 0.02, r=0.01, col="fbf6ee", rx=-8))
    for k in range(4):
        body.append(S("boton", 0.05, 0.4 - k * 0.055, 0.168, 0.012, "fbf6ee"))
    body.append(torus("collar", U(0, 0.43, 0.01), 0.085, 0.009, "fff3d6", rot=(14, 0, 0)))
    acc = [hair_cap("b8b4c4", y_cut=0.32, z_cut=-0.2)]
    acc.append(H("rodete", 0, R * 1.0, -R * 0.15, R * 0.4, "b8b4c4", scale=(1, 0.8, 1)))
    acc.append(tube("palito", [U(-R * 0.4, HC[1] + R * 1.05, HC[2] - R * 0.2), U(R * 0.45, HC[1] + R * 1.15, HC[2] - R * 0.1)], 0.008, "e0594a"))
    acc += eye_glasses("3a2a4a", r=0.17, chain=GOLD)
    acc += ears()
    # libreta y lapiz
    tool = [B("libreta", 0.17, 0.17, 0.09, 0.11, 0.015, 0.14, r=0.006, col="e0594a"),
            B("hojas", 0.17, 0.18, 0.09, 0.1, 0.01, 0.13, r=0.004, col="fbf6ee"),
            C("lapiz", 0.2, 0.19, 0.06, 0.007, 0.11, "f0cf6a", rx=90)]
    return dict(body=body, legs=("5a4a6a", "2b2026"), arms=(lilac, "8a72b4"), acc=acc, tool=tool)


def guard():
    blue, blue_d = "3e5aa8", "2e4380"
    W = 1.18
    body = torso("6f8fd8", blue_d, wide=W)
    body.append(B("chaleco", 0, 0.33, 0.17, 0.3, 0.22, 0.03, r=0.025, col=blue, rx=-6))
    body.append(S("placa", -0.08, 0.38, 0.19, 0.03, GOLD, metal=0.7, rough=0.25, scale=(1, 1.15, 0.35)))
    body.append(torus("cinto", U(0, 0.25, 0), 0.172 * W, 0.016, "2b2a30"))
    body.append(B("hebilla", 0, 0.25, 0.2, 0.05, 0.035, 0.012, r=0.004, col=GOLD, metal=0.7))
    body.append(tube("cordon", [U(0.07, 0.42, 0.17), U(0.1, 0.32, 0.19), U(0.12, 0.3, 0.19)], 0.006, "e0594a"))
    body.append(C("silbato", 0.13, 0.28, 0.19, 0.016, 0.05, "c8ccd4", rz=90, metal=0.7, rough=0.25))
    acc = [hair_cap("2b2026", y_cut=0.6, z_cut=-0.4)]
    acc.append(C("gorra", 0, HC[1] + R * 0.45, HC[2] - 0.01, R * 1.0, R * 0.3, blue, r2=R * 1.15, seg=32, bev=0.02))
    acc.append(C("visera", 0, HC[1] + R * 0.46, HC[2] + R * 0.74, R * 0.52, 0.02, "1a1a20", rx=-14, seg=20, rough=0.25))
    acc.append(S("escudo", 0, HC[1] + R * 0.64, HC[2] + R * 1.04, 0.035, GOLD, metal=0.7, rough=0.25, scale=(1, 1.15, 0.4)))
    acc.append(H("bigote", 0, -R * 0.12, R * 0.97, R * 0.13, "2b2026", scale=(1.8, 0.45, 0.5)))
    acc += ears()
    return dict(body=body, legs=(blue_d, "1a1a20"), arms=("6f8fd8", blue), acc=acc, tool=[], wide=W)


def star():
    suit = "4a3a6a"
    body = torso(suit, suit, plaid=None)
    # rayas del traje: tiritas finas verticales
    for k in range(9):
        a = -1.1 + k * 0.275
        body.append(tube("raya", [U(math.sin(a) * 0.174, 0.25, math.cos(a) * 0.174), U(math.sin(a) * 0.15, 0.42, math.cos(a) * 0.15)], 0.0035, "d8cce8"))
    body.append(B("camisa", 0, 0.38, 0.16, 0.08, 0.13, 0.02, r=0.01, col="fbf6ee", rx=-8))
    for s_ in (-1, 1):
        body.append(S("moño", s_ * 0.028, 0.44, 0.13, 0.026, "e53935", scale=(1.4, 0.8, 0.6)))
        body.append(B("solapa", s_ * 0.06, 0.37, 0.158, 0.04, 0.14, 0.018, r=0.006, col="3a2c56", rz=s_ * 14, rx=-8))
    body.append(S("nudo", 0, 0.44, 0.15, 0.016, "b0282a"))
    body.append(S("flor", -0.1, 0.385, 0.16, 0.024, "ff5d8f"))
    acc = [HAIR("pelo", 0, 0.02, -0.02, R * 1.05, "1a1418", y_cut=0.42, z_cut=-0.35)]
    acc.append(H("jopo", R * 0.15, R * 0.78, R * 0.42, R * 0.4, "1a1418", scale=(1.5, 0.55, 1.1)))
    for k in range(3):
        acc.append(H("brillo", -R * 0.2 + k * R * 0.2, R * 0.92, R * 0.3, R * 0.06, "ffffff", scale=(1.8, 0.4, 0.8), emis=0.6))
    acc += ears()
    return dict(body=body, legs=(suit, "1a1a20"), arms=(suit, "fbf6ee"), acc=acc, tool=[])


ROLES = [("receptionist", receptionist), ("cleaner", cleaner), ("seeker", seeker), ("driver", driver),
         ("restorer", restorer), ("appraiser", appraiser), ("guard", guard), ("star", star)]


def build(fn):
    d = fn()
    w = d.get("wide", 1.0)
    pants, shoe = d["legs"]
    sleeve, cuff = d["arms"]
    hand = d.get("hand", SKIN)
    groups = [
        ("body", d["body"], U(0, 0, 0)),
        ("legL", leg(-1, pants, shoe, d.get("shorts", False), w), U(-0.075 * w, 0.2, 0)),
        ("legR", leg(1, pants, shoe, d.get("shorts", False), w), U(0.075 * w, 0.2, 0)),
        ("armL", arm(-1, sleeve, cuff, hand, wide=w), U(-0.17 * w, 0.43, 0)),
        ("armR", arm(1, sleeve, cuff, hand, wide=w), U(0.17 * w, 0.43, 0)),
        ("acc", d["acc"], U(*HC)),
    ]
    if d["tool"]:
        groups.append(("tool", d["tool"], U(0.17 * w, 0.43, 0)))
    return groups


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    for name, fn in ROLES:
        if only and name not in only:
            continue
        lib.reset()
        parts, t = lib.finish_parts(build(fn), "staff_" + name, OUT, ao=0.45, ao_dist=0.12, budget=8000)
        print("STAFF", name, t)
        if prev:
            os.makedirs(prev, exist_ok=True)
            h = sphere("cabeza", U(*HC), R, SKIN, scale=(1, 0.96, 0.94))
            lib.preview(parts + [h], os.path.join(prev, "staff_" + name + ".png"), size=420, azim=-25, elev=12)


if __name__ == "__main__":
    main()
