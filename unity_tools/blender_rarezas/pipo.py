"""Pipo, el protagonista (DIRECCION_CREATIVA 1), por piezas con pivote, mismas medidas que los clientes (people.py):
overol mostaza, camisa a cuadros verde, gorra de explorador con linterna, mochila de cuero y botas marrones.
La cabeza (esfera con la cara de expresiones) la pone Unity; el pelo va en "hair" para poder cambiarle el color.

Piezas (Unity, frente +Z):
  body  (0, 0, 0)          cadera, torso, pechera del overol, tiradores y mochila
  pack  (0, 0.36, -0.17)   tapa de la mochila (se infla cuando esta llena)
  legL  (-0.075, 0.2, 0)   pierna con bota
  legR  ( 0.075, 0.2, 0)
  armL  (-0.17, 0.43, 0)   manga a cuadros y mano
  armR  ( 0.17, 0.43, 0)
  acc   (0, 0.70, 0.01)    gorra de explorador con linterna y orejas
  hair  (0, 0.70, 0.01)    pelo (color elegible)

Uso: blender -b -P pipo.py -- [--preview carpeta]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import sphere, cyl, torus, tube
from people import U, S, C, B, L, H, HAIR, R, HC, SKIN, ears

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
MUSTARD, MUSTARD_D = "e8a93a", "c98a2a"
SHIRT = "5aa64e"
PLAID = ("2f6b3a", "c8e68a", 0.045)
BOOT, BOOT_D = "8a5530", "5a3420"
LEATHER, LEATHER_D = "a8673a", "7a4626"
KHAKI, KHAKI_D = "d9c08a", "b39a62"
HAIR_COL = "6a4026"


def shirt(o):
    lib.dense(o, 1)
    lib.setmat(o, SHIRT, plaid=PLAID)
    return o


def body():
    o = []
    # cadera y piernas del overol
    o.append(L("cadera", 0, 0.17, 0, [(0, 0), (0.152, 0), (0.168, 0.05), (0.162, 0.09), (0, 0.09)], MUSTARD))
    # torso con la camisa a cuadros
    o.append(shirt(L("torso", 0, 0.24, 0, [(0, 0), (0.166, 0), (0.173, 0.06), (0.153, 0.15), (0.1, 0.21), (0.05, 0.235), (0, 0.24)], SHIRT)))
    o.append(C("cuello", 0, 0.44, 0, 0.05, 0.05, SKIN))
    # pechera del overol (adelante) y la espalda
    o.append(L("pechera_baja", 0, 0.22, 0.004, [(0, 0), (0.172, 0), (0.176, 0.06), (0.165, 0.09), (0, 0.09)], MUSTARD))
    o.append(B("pechera", 0, 0.345, 0.145, 0.19, 0.16, 0.05, r=0.025, col=MUSTARD, rx=-8))
    o.append(B("bolsillo", 0, 0.35, 0.172, 0.09, 0.07, 0.012, r=0.01, col=MUSTARD_D, rx=-8))
    for s_ in (-1, 1):
        # tiradores que pasan por los hombros hasta la espalda
        o.append(tube("tirador", [U(s_ * 0.075, 0.41, 0.15), U(s_ * 0.09, 0.455, 0.06), U(s_ * 0.09, 0.45, -0.07), U(s_ * 0.08, 0.3, -0.16)], 0.016, MUSTARD))
        o.append(S("boton", s_ * 0.07, 0.405, 0.168, 0.016, "d9a441", metal=0.6, rough=0.3))
    # costuras del overol
    o.append(torus("costura", U(0, 0.22, 0), 0.172, 0.006, MUSTARD_D, rot=(0, 0, 0)))
    # mochila de cuero en la espalda
    o.append(B("mochila", 0, 0.31, -0.205, 0.25, 0.22, 0.12, r=0.045, col=LEATHER))
    o.append(B("bolsillo_m", 0, 0.255, -0.27, 0.15, 0.08, 0.03, r=0.02, col=LEATHER_D))
    for s_ in (-1, 1):
        o.append(B("correa", s_ * 0.08, 0.31, -0.27, 0.025, 0.21, 0.014, r=0.006, col=LEATHER_D))
        o.append(B("hebilla", s_ * 0.08, 0.27, -0.28, 0.035, 0.03, 0.01, r=0.005, col="d9a441", metal=0.6, rough=0.3))
    # manta enrollada arriba de la mochila
    o.append(C("manta", -0.14, 0.45, -0.2, 0.045, 0.28, "e0594a", rz=90, bev=0.02))
    o.append(torus("atadura", U(-0.06, 0.45, -0.2), 0.047, 0.007, LEATHER_D, rot=(0, 90, 0)))
    o.append(torus("atadura", U(0.06, 0.45, -0.2), 0.047, 0.007, LEATHER_D, rot=(0, 90, 0)))
    return o


def pack_flap():
    # tapa de la mochila: pieza aparte (pivote arriba atras) para que se infle o se abra
    o = [B("tapa", 0, 0.36, -0.215, 0.255, 0.04, 0.13, r=0.018, col=LEATHER_D, rx=-12)]
    o.append(B("solapa", 0, 0.34, -0.282, 0.12, 0.08, 0.018, r=0.012, col=LEATHER_D))
    o.append(S("broche", 0, 0.31, -0.29, 0.016, "d9a441", metal=0.6, rough=0.3))
    return o


def leg(side):
    x = side * 0.075
    o = [C("pierna", x, 0.03, 0, 0.064, 0.17, MUSTARD, bev=0.02)]
    o.append(C("ruedo", x, 0.06, 0, 0.068, 0.025, MUSTARD_D, bev=0.008))
    o.append(S("bota", x, 0.01, 0.03, 0.075, BOOT, scale=(1, 1.4, 0.8)))
    o.append(C("cana", x, 0.0, 0, 0.066, 0.075, BOOT, bev=0.02))
    o.append(B("suela", x, -0.035, 0.03, 0.14, 0.03, 0.19, r=0.012, col=BOOT_D))
    o.append(B("cordon", x, 0.05, 0.075, 0.05, 0.012, 0.012, r=0.004, col="f0cf6a"))
    return o


def arm(side):
    x = side * 0.17
    o = [shirt(C("brazo", x, 0.26, 0, 0.047, 0.18, SHIRT, bev=0.02))]
    o.append(shirt(S("hombro", x, 0.43, 0, 0.054, SHIRT)))
    o.append(C("puño", x, 0.25, 0, 0.05, 0.03, "2f6b3a", bev=0.008))
    o.append(S("mano", x, 0.215, 0, 0.056, SKIN))
    o.append(S("pulgar", x - side * 0.035, 0.235, 0.03, 0.022, SKIN))
    return o


def cap():
    o = []
    # gorra de explorador: copa redondeada, banda, visera y linterna al frente
    o.append(HAIR("copa", 0, R * 0.2, -0.01, R * 1.05, KHAKI, scale=(1, 0.82, 1), y_cut=0.42, z_cut=-1.5))
    o.append(C("banda", 0, HC[1] + R * 0.36, HC[2] - 0.005, R * 0.98, 0.05, KHAKI_D, r2=R * 0.94, seg=28))
    o.append(C("visera", 0, HC[1] + R * 0.4, HC[2] + R * 0.78, R * 0.5, 0.022, KHAKI_D, rx=-14, seg=20))
    o.append(H("boton", 0, R * 1.07, -0.01, 0.024, KHAKI_D))
    # linterna (frontal) con su lente que brilla
    o.append(B("soporte", 0, HC[1] + R * 0.5, HC[2] + R * 0.86, 0.075, 0.06, 0.03, r=0.012, col="3a3a44", metal=0.5, rough=0.4))
    o.append(C("linterna", 0, HC[1] + R * 0.6, HC[2] + R * 0.86, 0.042, 0.075, "4a4a56", rx=90, bev=0.012, seg=16, metal=0.5, rough=0.35))
    o.append(C("lente", 0, HC[1] + R * 0.6, HC[2] + R * 0.86 + 0.072, 0.033, 0.012, "fff1b0", rx=90, seg=16, emis=1.5))
    o += ears()
    return o


def hair():
    o = []
    # mechones que asoman abajo de la gorra (atras y a los costados) y un flequillo
    for s_ in (-1, 1):
        o.append(H("patilla", s_ * R * 0.86, R * 0.1, -R * 0.12, R * 0.26, HAIR_COL, scale=(0.55, 0.9, 0.9)))
    o.append(HAIR("nuca", 0, -R * 0.02, -0.02, R * 1.04, HAIR_COL, y_cut=0.7, z_cut=-0.35))
    for k in range(3):
        o.append(H("flequillo", (k - 1) * R * 0.28, R * 0.3, R * 0.86, R * 0.16, HAIR_COL, scale=(1.0, 0.7, 0.6)))
    return o


def build():
    return [
        ("body", body(), U(0, 0, 0)),
        ("pack", pack_flap(), U(0, 0.36, -0.17)),
        ("legL", leg(-1), U(-0.075, 0.2, 0)),
        ("legR", leg(1), U(0.075, 0.2, 0)),
        ("armL", arm(-1), U(-0.17, 0.43, 0)),
        ("armR", arm(1), U(0.17, 0.43, 0)),
        ("acc", cap(), U(*HC)),
        ("hair", hair(), U(*HC)),
    ]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    lib.reset()
    parts, t = lib.finish_parts(build(), "hero_pipo", OUT, ao=0.45, ao_dist=0.12, budget=9000)
    print("PIPO", t)
    if prev:
        os.makedirs(prev, exist_ok=True)
        h = sphere("cabeza", U(*HC), R, SKIN, scale=(1, 0.96, 0.94))
        lib.preview(parts + [h], os.path.join(prev, "pipo_front.png"), size=520, azim=-25, elev=12)
        lib.preview(parts + [h], os.path.join(prev, "pipo_back.png"), size=520, azim=160, elev=18)


if __name__ == "__main__":
    main()
