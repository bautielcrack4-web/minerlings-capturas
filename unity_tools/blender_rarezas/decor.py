"""Decoracion, mugre y exhibidores de las etapas altas (DIRECCION_CREATIVA 2): planta, alfombra, cuadro, lampara,
gato que duerme, pecera y la estatua de Pipo; barro, papelitos, chicle y huellas; vitrina de vidrio, pedestal con foco,
tarima con cordon rojo y vitrina de lujo con terciopelo. Base en y=0 (Unity), frente hacia -Z.

Uso: blender -b -P decor.py -- [--only plant,...] [--preview carpeta]
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, torus, tube, blob, lathe
from shack import U, UB, UC, US, text
from town import UL, UBL

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
MODELS = {}


def model(fn):
    MODELS[fn.__name__] = fn
    return fn


# ================================================================ decoracion
@model
def decor_plant():
    o = [UL("maceta", 0, 0, 0, [(0, 0), (0.14, 0), (0.18, 0.26), (0.2, 0.28), (0, 0.28)], "c4683a", seg=20)]
    o.append(UC("tierra", 0, 0.25, 0, 0.17, 0.02, "5a3a28", seg=20))
    for k in range(7):
        a = k * 0.9
        o.append(US("hoja", math.cos(a) * 0.12, 0.42 + (k % 3) * 0.08, math.sin(a) * 0.12, 0.11, "4aa840" if k % 2 else "5cbf63", scale=(0.6, 0.6, 1.5)))
    o.append(US("flor", 0.05, 0.62, -0.05, 0.05, "ff5d8f"))
    return o


@model
def decor_rug():
    o = [UB("alfombra", 0, 0, 0, 1.4, 0.02, 0.95, r=0.01, seg=1, col="b8443a", plaid=("8a2b30", "ffd9a0", 0.12))]
    o.append(UB("borde", 0, 0.002, 0, 1.46, 0.016, 1.0, r=0.01, seg=1, col="ffd23a"))
    for s_ in (-1, 1):
        for k in range(8):
            o.append(UB("fleco", s_ * 0.76, 0, -0.42 + k * 0.12, 0.08, 0.01, 0.02, r=0.004, seg=1, col="fff3d6"))
    return o


@model
def decor_painting():
    o = [UB("caballete_pata", 0, 0, 0.1, 0.06, 1.2, 0.06, rx=-10, r=0.02, col="7a4a2a")]
    for s_ in (-1, 1):
        o.append(UB("pata", s_ * 0.3, 0, -0.08, 0.05, 1.05, 0.05, rx=8, rz=s_ * 6, r=0.02, col="8a5a30"))
    o.append(UB("marco", 0, 0.55, -0.02, 0.82, 0.62, 0.06, rx=-10, r=0.03, col="ffd23a", metal=0.5, rough=0.35))
    o.append(UB("tela", 0, 0.6, -0.055, 0.68, 0.48, 0.02, rx=-10, r=0.01, col="8fd0f0"))
    o.append(UB("cerro", -0.1, 0.62, -0.07, 0.4, 0.2, 0.02, rx=-10, rz=20, r=0.01, col="5cbf63"))
    o.append(US("sol", 0.18, 0.84, -0.08, 0.05, "ffd23a"))
    return o


@model
def decor_lamp():
    o = [UC("base", 0, 0, 0, 0.14, 0.05, "ffd23a", seg=16, metal=0.5, rough=0.35)]
    o.append(UC("pie", 0, 0.05, 0, 0.025, 0.9, "ffd23a", seg=10, metal=0.5, rough=0.35))
    o.append(UL("pantalla", 0, 0.85, 0, [(0, 0), (0.26, 0), (0.14, 0.26), (0, 0.26)], "4aa86a", seg=16, emis=0.25))
    for k in range(8):
        a = k * math.pi / 4
        o.append(US("vidrio", math.cos(a) * 0.2, 0.92, math.sin(a) * 0.2, 0.045, ["e0594a", "ffd23a", "4a8fe0", "b06ef0"][k % 4], emis=0.5, scale=(1, 1.4, 0.4)))
    return o


@model
def decor_cat():
    """Gato naranja que duerme hecho un rollito en un almohadon."""
    o = [UC("almohadon", 0, 0, 0, 0.3, 0.1, "4a8fe0", seg=20, bev=0.04)]
    o.append(US("cuerpo", 0, 0.18, 0, 0.18, "f59a32", scale=(1.2, 0.7, 1.0)))
    o.append(US("cabeza", 0.12, 0.2, -0.1, 0.11, "f59a32"))
    for s_ in (-1, 1):
        o.append(UC("oreja", 0.12 + s_ * 0.06, 0.28, -0.1, 0.035, 0.07, "f59a32", r2=0.003, rz=s_ * 20, seg=8))
        o.append(UB("ojo", 0.12 + s_ * 0.04, 0.21, -0.2, 0.035, 0.008, 0.01, r=0.003, col="2b2440"))
    o.append(tube("cola", [U(-0.18, 0.15, 0.02), U(-0.12, 0.13, -0.15), U(0.02, 0.12, -0.2)], 0.04, "e0802a"))
    for k in range(3):
        o.append(UB("raya", -0.05 + k * 0.06, 0.29, 0.0, 0.025, 0.01, 0.2, r=0.005, col="d06a20"))
    return o


@model
def decor_fishtank():
    o = [UB("mueble", 0, 0, 0, 0.9, 0.5, 0.45, r=0.03, col="7a4a2a")]
    o.append(UB("pecera", 0, 0.5, 0, 0.86, 0.45, 0.4, r=0.03, col="8fd8f0", rough=0.1, emis=0.12))
    o.append(UB("arena", 0, 0.5, 0, 0.82, 0.06, 0.36, r=0.02, col="f0d9a0"))
    for k in range(3):
        o.append(US("pez", -0.2 + k * 0.2, 0.7 + (k % 2) * 0.08, -0.15, 0.05, ["f59a32", "ffd23a", "e0594a"][k], scale=(1.4, 0.8, 0.5)))
    o.append(tube("alga", [U(0.3, 0.56, 0.05), U(0.32, 0.8, 0.05)], 0.02, "4aa86a"))
    o.append(UB("tapa", 0, 0.95, 0, 0.9, 0.04, 0.44, r=0.02, col="2b3040"))
    return o


@model
def decor_self_statue():
    """Estatua de Pipo (museo de lujo): bronce sobre un pedestal."""
    o = [UB("pedestal", 0, 0, 0, 0.9, 0.6, 0.9, r=0.05, col="f3ead8")]
    o.append(UB("placa", 0, 0.25, -0.46, 0.4, 0.14, 0.02, r=0.01, col="ffd23a", metal=0.6, rough=0.3))
    m = dict(metal=0.6, rough=0.35)
    o.append(US("cuerpo", 0, 0.95, 0, 0.24, "c9a14a", scale=(0.9, 1.1, 0.8), **m))
    o.append(US("cabeza", 0, 1.38, 0, 0.26, "c9a14a", **m))
    o.append(US("gorra", 0, 1.52, -0.02, 0.26, "b8903a", scale=(1.05, 0.6, 1.05), **m))
    for s_ in (-1, 1):
        o.append(UC("pierna", s_ * 0.09, 0.6, 0, 0.07, 0.25, "c9a14a", seg=10, **m))
    o.append(tube("brazo", [U(0.2, 1.05, 0), U(0.32, 1.3, -0.05), U(0.3, 1.55, -0.05)], 0.06, "c9a14a", **m))
    o.append(tube("brazo2", [U(-0.2, 1.05, 0), U(-0.25, 0.85, -0.05)], 0.06, "c9a14a", **m))
    o.append(US("puño", 0.3, 1.6, -0.05, 0.07, "c9a14a", **m))
    return o


# ================================================================ mugre
@model
def stain_mud():
    o = []
    for k in range(4):
        z = -0.35 + k * 0.22
        x = 0.06 if k % 2 else -0.06
        o.append(US("pisada", x, 0.0, z, 0.06, "7a5a3a", scale=(0.8, 1.4, 0.05)))
        o.append(US("talon", x, 0.0, z + 0.09, 0.04, "7a5a3a", scale=(1, 1, 0.05)))
    return o


@model
def stain_paper():
    rnd = random.Random(4)
    o = []
    for k in range(3):
        x, z = rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15)
        o.append(UB("papel", x, 0.0, z, 0.1, 0.02, 0.08, ry=rnd.uniform(0, 90), rx=rnd.uniform(-10, 10), r=0.01, col=["fbf6ee", "ffd23a", "8fd0f0"][k]))
    return o


@model
def stain_gum():
    return [US("chicle", 0, 0.0, 0, 0.08, "ff9ec0", scale=(1.2, 1.0, 0.18)), US("chicle2", 0.08, 0.0, 0.05, 0.04, "ff9ec0", scale=(1, 1, 0.2))]


# ================================================================ exhibidores de las etapas altas
@model
def disp_glasscase():
    """Vitrina de vidrio (chico y mediano, +20%). 2x2; tope a y=0.62."""
    o = [UB("base", 0, 0, 0, 0.92, 0.6, 0.92, r=0.04, col="7a4a2a")]
    o.append(UB("tapa", 0, 0.6, 0, 0.94, 0.04, 0.94, r=0.02, col="a8703f"))
    for (x, z) in ((-0.44, -0.44), (0.44, -0.44), (-0.44, 0.44), (0.44, 0.44)):
        o.append(UB("parante", x, 0.62, z, 0.04, 0.55, 0.04, r=0.01, col="ffd23a", metal=0.5, rough=0.35))
    # el vidrio se sugiere con reflejos (el objeto de adentro se tiene que ver)
    for s_ in (-1, 1):
        o.append(UB("reflejo", -0.18, 0.75, s_ * 0.445, 0.035, 0.32, 0.008, rz=35, r=0.004, col="ffffff", emis=0.6))
        o.append(UB("reflejo2", -0.08, 0.7, s_ * 0.445, 0.02, 0.22, 0.008, rz=35, r=0.004, col="ffffff", emis=0.6))
    o.append(UB("techo", 0, 1.17, 0, 0.94, 0.05, 0.94, r=0.02, col="a8703f"))
    return o


@model
def disp_spotpedestal():
    """Pedestal con foco (mediano y grande, +25%). 2x2; tope a y=0.9."""
    o = [UB("pedestal", 0, 0, 0, 0.8, 0.88, 0.8, r=0.04, col="fbf6ee")]
    o.append(UB("moldura", 0, 0.86, 0, 0.88, 0.05, 0.88, r=0.02, col="e8dcc8"))
    o.append(UB("zocalo", 0, 0, 0, 0.9, 0.08, 0.9, r=0.02, col="e8dcc8"))
    o.append(UC("poste_foco", 0.38, 0, 0.38, 0.025, 1.9, "2b3040", seg=10))
    o.append(UC("foco", 0.3, 1.85, 0.3, 0.08, 0.16, "2b3040", rx=40, rz=-30, seg=12))
    o.append(UC("luz", 0.24, 1.79, 0.24, 0.06, 0.02, "fff1b0", rx=40, rz=-30, seg=12, emis=2.0))
    return o


@model
def disp_ropeplatform():
    """Tarima con cordon rojo (enorme, +30%). 4x4; tope a y=0.25."""
    o = [UB("tarima", 0, 0, 0, 1.9, 0.22, 1.9, r=0.04, col="e8dcc8")]
    o.append(UB("alfombra", 0, 0.22, 0, 1.6, 0.02, 1.6, r=0.01, seg=1, col="c0392b"))
    for (x, z) in ((-0.9, -0.9), (0.9, -0.9), (-0.9, 0.9), (0.9, 0.9)):
        o.append(UC("poste", x, 0.22, z, 0.035, 0.6, "ffd23a", seg=10, metal=0.6, rough=0.3))
        o.append(US("bocha", x, 0.84, z, 0.05, "ffd23a", metal=0.6, rough=0.3))
    for (a, b) in (((-0.9, -0.9), (0.9, -0.9)), ((0.9, -0.9), (0.9, 0.9)), ((0.9, 0.9), (-0.9, 0.9)), ((-0.9, 0.9), (-0.9, -0.9))):
        o.append(tube("cordon", [U(a[0], 0.76, a[1]), U((a[0] + b[0]) / 2, 0.6, (a[1] + b[1]) / 2), U(b[0], 0.76, b[1])], 0.02, "c0392b"))
    return o


@model
def disp_luxurycase():
    """Vitrina de lujo con terciopelo (cualquiera, +45%). 3x3; tope a y=0.7."""
    o = [UB("base", 0, 0, 0, 1.4, 0.66, 1.4, r=0.05, col="2b2440")]
    o.append(UB("terciopelo", 0, 0.66, 0, 1.3, 0.05, 1.3, r=0.03, col="8a2b4a"))
    for (x, z) in ((-0.66, -0.66), (0.66, -0.66), (-0.66, 0.66), (0.66, 0.66)):
        o.append(UB("parante", x, 0.66, z, 0.06, 1.0, 0.06, r=0.02, col="ffd23a", metal=0.6, rough=0.3))
        o.append(US("adorno", x, 1.7, z, 0.06, "ffd23a", metal=0.6, rough=0.3))
    for s_ in (-1, 1):
        o.append(UB("reflejo", -0.3, 0.9, s_ * 0.665, 0.05, 0.55, 0.008, rz=35, r=0.004, col="ffffff", emis=0.6))
        o.append(UB("reflejo2", -0.16, 0.85, s_ * 0.665, 0.03, 0.38, 0.008, rz=35, r=0.004, col="ffffff", emis=0.6))
    o.append(UB("techo", 0, 1.64, 0, 1.44, 0.07, 1.44, r=0.03, col="ffd23a", metal=0.6, rough=0.3))
    return o


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(MODELS)
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if prev:
        os.makedirs(prev, exist_ok=True)
    for n in only:
        lib.reset()
        objs = [x for x in MODELS[n]() if x is not None]
        o, t = lib.finish(objs, n if n.startswith("disp_") else "prop_" + n, OUT, ao=0.5, ao_dist=0.25)
        print("DECOR", n, t)
        if prev:
            lib.preview([o], os.path.join(prev, n + ".png"), size=300, elev=30, azim=196)


if __name__ == "__main__":
    main()
