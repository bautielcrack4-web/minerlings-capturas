"""Clientes de Curio Barn por piezas (Blender por scripts). Coordenadas en Unity con U() (frente hacia +Z).

Piezas y pivotes (deben coincidir con Rarezas.View.Chibi):
  body  (0, 0, 0)            torso, cadera y detalles del pecho
  legL  (-0.075, 0.2, 0)     pierna con zapato (cuelga hacia abajo desde la cadera)
  legR  ( 0.075, 0.2, 0)
  armL  (-0.17, 0.43, 0)     brazo con mano (y lo que lleva en la mano)
  armR  ( 0.17, 0.43, 0)
  acc   (0, 0.70, 0.01)      pelo, sombrero, anteojos: centrado en la cabeza (radio 0.25)
La cabeza (esfera con la cara de expresiones) la pone Unity.

Uso: blender -b -P people.py -- [--only tourist_a,...] [--preview carpeta]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, lathe, torus, tube, blob

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
R = 0.25
HC = (0, 0.70, 0.01)
SKIN = "f2c49b"


def U(x, y, z):
    return (-x, -z, y)


def S(name, x, y, z, r, col, **kw):
    return sphere(name, U(x, y, z), r, col, **kw)


def C(name, x, y, z, r, h, col, rx=0, ry=0, rz=0, **kw):
    """Cilindro con base en Unity (x,y,z), eje +Y de Unity rotado (grados, ejes de Unity)."""
    return cyl(name, U(x, y, z), r, h, col, rot=(-rx, -rz, -ry), **kw)


def B(name, x, y, z, w, h, d, r=0.02, col="wood", rx=0, ry=0, rz=0, **kw):
    """Caja con centro en Unity (x,y,z), tamaño w (x) h (y) d (z)."""
    return rbox(name, U(x, y, z), (w, d, h), r=r, col=col, rot=(-rx, -rz, -ry), **kw)


def L(name, x, y, z, prof, col, **kw):
    """Revolucion alrededor de +Y de Unity con base en (x,y,z): prof = [(r, alto)]."""
    return lathe(name, U(x, y, z), prof, col, seg=24, **kw)


def H(name, dx, dy, dz, r, col, **kw):
    """Esfera relativa al centro de la cabeza."""
    return S(name, HC[0] + dx, HC[1] + dy, HC[2] + dz, r, col, **kw)


def HAIR(name, dx, dy, dz, rr, col, scale=(1, 1, 1), y_cut=0.45, z_cut=-0.05):
    """Pelo o gorra como casquete (deja la cara libre)."""
    o = H(name, dx, dy, dz, rr, col, scale=scale)
    bpy.context.view_layer.update()
    return lib.cap(o, U(*HC), R, y_cut, z_cut)


def torso(shirt, pants):
    o = []
    o.append(L("cadera", 0, 0.17, 0, [(0, 0), (0.15, 0), (0.165, 0.05), (0.16, 0.09), (0, 0.09)], pants))
    o.append(L("torso", 0, 0.24, 0, [(0, 0), (0.165, 0), (0.172, 0.06), (0.152, 0.15), (0.1, 0.21), (0.05, 0.235), (0, 0.24)], shirt))
    o.append(C("cuello", 0, 0.44, 0, 0.05, 0.05, SKIN))
    return o


def leg(side, pants, shoe):
    x = side * 0.075
    o = [C("pierna", x, 0.0, 0, 0.06, 0.2, pants, bev=0.02)]
    o.append(S("zapato", x, 0.005, 0.03, 0.07, shoe, scale=(1, 1.35, 0.75)))
    o.append(B("suela", x, -0.03, 0.03, 0.13, 0.025, 0.18, r=0.01, col="3a2a22"))
    return o


def arm(side, sleeve, skin=SKIN):
    x = side * 0.17
    o = [C("brazo", x, 0.24, 0, 0.045, 0.2, sleeve, bev=0.02)]
    o.append(S("hombro", x, 0.43, 0, 0.05, sleeve))
    o.append(S("mano", x, 0.225, 0, 0.055, skin))
    return o


def ears(skin=SKIN):
    return [H("oreja", s * R * 0.97, -R * 0.05, 0, R * 0.2, skin, scale=(0.8, 0.5, 1)) for s in (-1, 1)]


# ================================================================ tipos
def tourist(v):
    shirt = "3fbfbf" if v == "a" else "ff8a5c"
    body = torso(shirt, "d9c08a")
    for k in range(9):
        a = -1.2 + k * 0.3
        h = 0.27 + (k % 3) * 0.05
        rr = 0.17 - (h - 0.24) * 0.15
        body.append(S("flor", math.sin(a) * rr, h, math.cos(a) * rr, 0.022, "fff3d6" if k % 2 else "ff5d8f", scale=(1, 0.4, 1)))
    body.append(B("camara", 0.02, 0.31, 0.175, 0.13, 0.085, 0.06, r=0.015, col="2b2a30"))
    body.append(C("lente", 0.02, 0.31, 0.2, 0.026, 0.035, "7ec8e3", rx=90))
    body.append(torus("correa", U(0, 0.44, 0.0), 0.11, 0.008, "2b2a30", rot=(0, 0, 0), scale=(1, 1.1, 1)))
    acc = [HAIR("pelo", 0, 0.02, -0.02, R * 1.04, "5a3a28")]
    acc.append(C("ala", 0, HC[1] + R * 0.6, HC[2] - 0.01, R * 1.75, 0.025, "f0cf6a", seg=32, bev=0.01, rx=-8))
    acc.append(C("copa", 0, HC[1] + R * 0.6, HC[2] - 0.01, R * 0.88, R * 0.5, "f0cf6a", r2=R * 0.78, bev=0.04, rx=-8))
    acc.append(C("cinta", 0, HC[1] + R * 0.63, HC[2] - 0.01, R * 0.89, 0.05, "e0594a", r2=R * 0.87, rx=-8))
    acc += ears()
    return body, acc, [], []


def collector(v):
    body = torso("fbf6ee", "6b5a4a")
    body.append(L("chaleco", 0, 0.25, 0.005, [(0, 0), (0.172, 0), (0.176, 0.06), (0.156, 0.15), (0.11, 0.2), (0, 0.205)], "2f6a52"))
    body.append(B("abertura", 0, 0.36, 0.16, 0.06, 0.18, 0.02, r=0.008, col="fbf6ee"))
    for s_ in (-1, 1):
        body.append(S("moño", s_ * 0.03, 0.445, 0.07, 0.026, "e0594a", scale=(1.3, 0.8, 0.6)))
        body.append(B("bolsillo", s_ * 0.08, 0.3, 0.155, 0.05, 0.035, 0.012, r=0.006, col="245a44"))
    body.append(tube("cadena", [U(0.03, 0.31, 0.165), U(0.07, 0.29, 0.162), U(0.1, 0.31, 0.155)], 0.004, "gold"))
    acc = [HAIR("pelo", 0, 0.0, -0.025, R * 1.05, "8a6a4a", y_cut=0.4)]
    acc.append(H("jopo", -R * 0.5, R * 0.65, R * 0.2, R * 0.33, "8a6a4a", scale=(1.3, 0.6, 1)))
    acc.append(torus("anteojo", U(HC[0] + R * 0.26, HC[1] + R * 0.05, HC[2] + R * 0.98), R * 0.16, 0.009, "gold", rot=(90, 0, 0)))
    acc += ears()
    lupa = [tube("mango", [U(0.17, 0.22, 0.03), U(0.17, 0.2, 0.12)], 0.012, "woodd"),
            torus("aro", U(0.17, 0.21, 0.19), 0.05, 0.01, "gold", rot=(20, 0, 0)),
            cyl("vidrio", U(0.17, 0.21, 0.19), 0.045, 0.008, "bfe4ff", rot=(110, 0, 0), rough=0.05)]
    return body, acc, [], lupa


def grandma(v):
    top = "9a6ac8" if v == "a" else "d96a8a"
    body = torso(top, "6a5a8a")
    body.append(L("pollera", 0, 0.12, 0, [(0, 0), (0.2, 0), (0.17, 0.08), (0.15, 0.14), (0, 0.14)], "6a4a8a" if v == "a" else "a84a6a"))
    for k in range(11):
        a = -1.1 + k * 0.22
        body.append(S("perla", math.sin(a) * 0.08, 0.43 - math.cos(a * 1.3) * 0.02, math.cos(a) * 0.08 + 0.02, 0.012, "fbf6ee"))
    body.append(B("delantal", 0, 0.3, 0.16, 0.16, 0.14, 0.012, r=0.02, col="fff3d6"))
    acc = [HAIR("pelo", 0, 0.0, -0.03, R * 1.06, "d8d8e0", y_cut=0.42)]
    acc.append(H("rodete", 0, R * 0.95, -R * 0.35, R * 0.42, "d8d8e0"))
    acc.append(torus("hebilla", U(HC[0], HC[1] + R * 0.92, HC[2] - R * 0.33), R * 0.36, 0.012, "ff5d8f", rot=(-30, 0, 0)))
    for s_ in (-1, 1):
        acc.append(torus("lente", U(HC[0] + s_ * R * 0.26, HC[1] + R * 0.05, HC[2] + R * 0.98), R * 0.15, 0.007, "b8c4d0", rot=(90, 0, 0)))
    acc += ears()
    purse = [B("cartera", -0.17, 0.17, 0.03, 0.11, 0.09, 0.06, r=0.02, col="e0594a"),
             torus("asa", U(-0.17, 0.23, 0.03), 0.035, 0.008, "e0594a", rot=(0, 90, 0)),
             S("broche", -0.17, 0.2, 0.065, 0.01, "gold")]
    return body, acc, purse, []


def kid(v):
    shirt = "ffd34d" if v == "a" else "ef5a4a"
    body = torso(shirt, "4a8fe0")
    body.append(cyl("estrella", U(0, 0.34, 0.165), 0.04, 0.02, "fbf6ee", seg=5, rot=(90, 0, 0)))
    acc = [HAIR("gorra", 0, R * 0.08, -0.01, R * 1.06, "4a8fe0", y_cut=0.42, z_cut=-1.5)]
    acc.append(C("visera", 0, HC[1] + R * 0.42, HC[2] + R * 0.85, R * 0.5, 0.022, "3a72b8", rx=14))
    acc.append(H("boton", 0, R * 1.08, 0, 0.025, "3a72b8"))
    acc += ears()
    balloon = [tube("hilo", [U(0.17, 0.22, 0.0), U(0.19, 0.95, 0.02)], 0.004, "fbf6ee"),
               S("globo", 0.19, 1.08, 0.02, 0.12, "e0594a", scale=(1, 1, 1.15), rough=0.25),
               cyl("nudo", U(0.19, 0.95, 0.02), 0.02, 0.03, "e0594a", r2=0.005)]
    return body, acc, [], balloon


def rich(v):
    body = torso("8a2b3d", "2b2440")
    body.append(L("saco", 0, 0.1, 0, [(0, 0), (0.2, 0), (0.18, 0.1), (0.17, 0.16), (0, 0.16)], "6e2232"))
    for i in range(3):
        body.append(S("boton", 0, 0.27 + i * 0.055, 0.165 - i * 0.006, 0.014, "gold"))
    body.append(cyl("pañuelo", U(0.08, 0.38, 0.14), 0.025, 0.04, "fbf6ee", r2=0.0, rot=(90, 0, 0)))
    acc = [HAIR("pelo", 0, -0.01, -0.03, R * 1.04, "3a2a22", y_cut=0.5)]
    acc.append(C("ala", 0, HC[1] + R * 0.68, HC[2], R * 1.25, 0.022, "24202c", seg=32, bev=0.008))
    acc.append(C("galera", 0, HC[1] + R * 0.68, HC[2], R * 0.72, R * 1.0, "24202c", r2=R * 0.76, bev=0.02))
    acc.append(C("cinta", 0, HC[1] + R * 0.7, HC[2], R * 0.735, 0.05, "b06ef0"))
    acc.append(torus("monoculo", U(HC[0] + R * 0.27, HC[1] + R * 0.06, HC[2] + R * 0.99), R * 0.17, 0.008, "gold", rot=(90, 0, 0)))
    acc.append(tube("cadenita", [U(HC[0] + R * 0.43, HC[1] - R * 0.05, HC[2] + R * 0.9), U(HC[0] + R * 0.6, HC[1] - R * 0.55, HC[2] + R * 0.7)], 0.003, "gold"))
    for s_ in (-1, 1):
        acc.append(H("bigote", s_ * R * 0.16, -R * 0.09, R * 0.95, R * 0.12, "3a2a22", scale=(1.6, 0.6, 0.6)))
    acc += ears()
    cane = [tube("baston", [U(0.17, 0.22, 0.04), U(0.2, -0.02, 0.12)], 0.012, "24202c"),
            S("pomo", 0.17, 0.24, 0.04, 0.025, "gold")]
    return body, acc, [], cane


def reseller(v):
    body = torso("3a3440", "5a6070")
    body.append(B("cierre", 0, 0.34, 0.165, 0.014, 0.18, 0.012, r=0.004, col="9aa1aa"))
    body.append(torus("cadena", U(0, 0.43, 0.03), 0.075, 0.009, "gold", rot=(-30, 0, 0)))
    body.append(B("cuello", 0, 0.45, 0.0, 0.2, 0.05, 0.14, r=0.02, col="2b2630"))
    acc = [HAIR("gorra", 0, R * 0.08, -0.01, R * 1.06, "2b2a30", y_cut=0.42, z_cut=-1.5)]
    acc.append(C("visera", 0, HC[1] + R * 0.4, HC[2] - R * 0.85, R * 0.5, 0.022, "2b2a30", rx=-14))
    acc += ears()
    return body, acc, [], []


def critic(v):
    body = torso("ff9ec0", "2b2440")
    body.append(L("chal", 0, 0.38, 0, [(0, 0), (0.12, 0.0), (0.13, 0.04), (0.08, 0.07), (0, 0.07)], "fbf6ee"))
    acc = [HAIR("pelo", 0, -0.02, -0.03, R * 1.07, "f0cf6a", y_cut=0.42, z_cut=0.1)]
    acc.append(H("boina", 0, R * 0.8, -0.02, R * 0.8, "e0594a", scale=(1.15, 0.35, 1.15)))
    acc.append(H("pompon", 0, R * 1.1, -0.02, 0.03, "e0594a"))
    for s_ in (-1, 1):
        acc.append(B("lente", HC[0] + s_ * R * 0.27, HC[1] + R * 0.05, HC[2] + R * 0.97, R * 0.3, R * 0.22, 0.012, r=0.015, col="ff5d8f"))
    acc += ears()
    phone = [B("celular", 0.17, 0.25, 0.1, 0.07, 0.13, 0.015, r=0.012, col="2b2a30", rx=-60),
             S("flash", 0.17, 0.3, 0.08, 0.012, "fff1d6", emis=1.0)]
    return body, acc, [], phone


KINDS = {"tourist": tourist, "collector": collector, "grandma": grandma, "kid": kid, "rich": rich, "reseller": reseller, "critic": critic}
VARIANTS = {"tourist": "ab", "grandma": "ab", "kid": "ab"}

SHIRT = {"tourist": ("3fbfbf", "ff8a5c"), "collector": ("fbf6ee",), "grandma": ("9a6ac8", "d96a8a"), "kid": ("ffd34d", "ef5a4a"),
         "rich": ("8a2b3d",), "reseller": ("3a3440",), "critic": ("ff9ec0",)}
PANTS = {"tourist": "d9c08a", "collector": "6b5a4a", "grandma": "6a5a8a", "kid": "4a8fe0", "rich": "2b2440", "reseller": "5a6070", "critic": "2b2440"}
SHOES = {"rich": "1e1a24", "reseller": "1e1a24"}


def build(kind, v):
    body, acc, handL, handR = KINDS[kind](v)
    i = 0 if v == "a" else 1
    sh = SHIRT[kind][min(i, len(SHIRT[kind]) - 1)]
    if kind == "collector":
        sh = "2f6a52"
    if kind == "rich":
        sh = "6e2232"
    pants = PANTS[kind]
    shoe = SHOES.get(kind, "4a3426")
    groups = [
        ("body", body, U(0, 0, 0)),
        ("legL", leg(-1, pants, shoe), U(-0.075, 0.2, 0)),
        ("legR", leg(1, pants, shoe), U(0.075, 0.2, 0)),
        ("armL", arm(-1, sh) + handL, U(-0.17, 0.43, 0)),
        ("armR", arm(1, sh) + handR, U(0.17, 0.43, 0)),
        ("acc", acc, U(*HC)),
    ]
    return groups


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only, prev = None, None
    for i, a in enumerate(argv):
        if a == "--only":
            only = argv[i + 1].split(",")
        if a == "--preview":
            prev = argv[i + 1]
    names = []
    for k in KINDS:
        for v in VARIANTS.get(k, "a"):
            names.append(k + "_" + v)
    for n in (only or names):
        k, v = n.split("_")
        lib.reset()
        groups = build(k, v)
        parts, t = lib.finish_parts(groups, "cust_" + n, OUT, ao=0.45, ao_dist=0.12)
        print("PERSON", n, t)
        if prev:
            # cabeza de vista previa (en el juego la pone Unity con la cara)
            h = sphere("cabeza", U(*HC), R, SKIN, scale=(1, 0.96, 0.94))
            lib.preview(parts + [h], os.path.join(prev, "cust_" + n + ".png"), size=360, azim=-25, elev=12)


if __name__ == "__main__":
    main()
