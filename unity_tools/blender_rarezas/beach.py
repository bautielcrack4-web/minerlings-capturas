"""Playa de los Naufragios (DIRECCION_CREATIVA 3): arena, barcos encallados, cangrejos, palmeras, muelle y faro, mas los
objetos de la playa del catalogo. Base en y=0 (Unity), frente hacia -Z. Ver town.py para el formato.

Uso: blender -b -P beach.py -- [--only palm,wreck,...] [--preview carpeta]
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, torus, tube, blob, lathe
from shack import U, UB, UC, US, text
from town import UL, UBL

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
PROPS = {}
ITEMS = {}


def prop(fn):
    PROPS[fn.__name__] = fn
    return fn


def item(fn):
    ITEMS[fn.__name__] = fn
    return fn


SAND, SANDD = "f0d9a0", "d9bd80"


@prop
def palm():
    rnd = random.Random(3)
    o = []
    pts = [U(math.sin(k * 0.35) * 0.18 * k / 6.0, k * 0.42, 0) for k in range(7)]
    for k in range(6):
        o.append(UC("tronco", pts[k][0] * -1, k * 0.42, 0, 0.16 - k * 0.012, 0.44, "a8703f" if k % 2 else "8a5a30", r2=0.15 - k * 0.012, seg=10, bev=0.02))
    top = (-pts[6][0], 2.5, 0)
    for k in range(7):
        a = k * 2 * math.pi / 7
        dx, dz = math.cos(a), math.sin(a)
        leaf = [U(top[0], top[1], top[2]), U(top[0] + dx * 0.6, top[1] + 0.25, top[2] + dz * 0.6), U(top[0] + dx * 1.15, top[1] - 0.05, top[2] + dz * 1.15),
                U(top[0] + dx * 1.4, top[1] - 0.45, top[2] + dz * 1.4)]
        o.append(tube("hoja", leaf, 0.14, "4aa840" if k % 2 else "5cbf63"))
    for k in range(3):
        o.append(US("coco", top[0] + (k - 1) * 0.12, top[1] - 0.1, 0.1, 0.1, "7a4a2a"))
    return o


@prop
def beach_rock():
    return [UBL("roca", 0, 0.2, 0, 0.5, "b8aa98", seed=5, amp=0.3, scale=(1.4, 1, 0.75)),
            UBL("roca2", 0.45, 0.12, 0.2, 0.3, "a89a88", seed=6, amp=0.3),
            US("estrella", -0.2, 0.5, -0.25, 0.06, "f59a32", scale=(1, 0.3, 1))]


@prop
def wreck():
    """Barco encallado grande (hito de la playa), partido y escorado."""
    o = []
    hull = [(0, 0), (0.9, 0.0), (1.25, 0.35), (1.35, 0.9), (1.3, 1.3), (0, 1.3)]
    for i in range(9):
        z = -2.4 + i * 0.6
        w = 1.0 - abs(i - 4) / 6.0
        o.append(UB("cuaderna", 0, 0.0, z, 2.4 * w + 0.4, 1.2 * w + 0.4, 0.58, rz=-14, r=0.12, seg=2, col=["8a5a30", "7a4a2a", "a8703f"][i % 3]))
    o.append(UB("cubierta", 0.1, 1.15, 0, 2.2, 0.08, 4.8, rz=-14, r=0.03, col="c98d55"))
    o.append(UC("mastil", 0.3, 1.2, 0.6, 0.1, 2.6, "7a4a2a", rz=-24, seg=10))
    o.append(UB("vela_rota", 0.9, 2.6, 0.6, 1.3, 0.9, 0.05, rz=-24, r=0.02, col="f3ead8"))
    o.append(UB("vela_rota2", 1.0, 2.0, 0.62, 0.8, 0.5, 0.05, rz=-10, r=0.02, col="e8dcc8"))
    for k in range(4):
        o.append(UC("hueco", -0.95, 0.5 + (k % 2) * 0.3, -1.5 + k * 0.9, 0.12, 0.1, "2b2026", rz=90 - 14, seg=12))
    o.append(UB("proa", 0, 0.5, 2.7, 0.4, 0.9, 0.6, rx=-30, rz=-14, r=0.1, col="7a4a2a"))
    o.append(UBL("arena", 0, 0.05, 0, 1.6, SANDD, seed=2, amp=0.2, scale=(1.4, 2.0, 0.18)))
    for k in range(5):
        o.append(UBL("alga", -1.2 + k * 0.6, 0.1, -2 + k * 0.9, 0.18, "4a8a5a", seed=k, amp=0.4, scale=(1, 0.5, 1)))
    return o


@prop
def small_wreck():
    o = [UB("casco", 0, 0, 0, 0.9, 0.45, 2.0, rz=20, r=0.2, col="a8703f")]
    o.append(UB("interior", 0, 0.12, 0, 0.7, 0.35, 1.7, rz=20, r=0.15, col="5a3a2a"))
    o.append(UB("tabla", 0.1, 0.42, 0.2, 0.75, 0.06, 0.2, rz=20, r=0.02, col="c98d55"))
    o.append(UBL("arena", 0, 0.02, 0, 0.8, SANDD, seed=4, amp=0.2, scale=(1.3, 1.7, 0.2)))
    return o


@prop
def driftwood():
    return [UC("tronco", -0.7, 0.12, 0, 0.13, 1.4, "c9b49a", rz=-90, seg=10, bev=0.03),
            UC("rama", 0.2, 0.2, 0.05, 0.05, 0.6, "c9b49a", rz=-60, rx=20, seg=8)]


@prop
def barrel():
    o = [UL("barril", 0, 0, 0, [(0, 0), (0.22, 0), (0.27, 0.22), (0.22, 0.46), (0, 0.46)], "a8703f", seg=18)]
    for y in (0.08, 0.38):
        o.append(torus("zuncho", U(0, y, 0), 0.245, 0.018, "6d747e", seg=18, metal=0.4, rough=0.5))
    return o


@prop
def seaweed():
    rnd = random.Random(8)
    o = []
    for k in range(6):
        x, z = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3)
        o.append(tube("alga", [U(x, 0, z), U(x + 0.05, 0.2, z), U(x - 0.05, 0.4, z + 0.05)], 0.03, "3f8a5a" if k % 2 else "4aa86a"))
    return o


@prop
def pier():
    """Muelle de tablas que entra al agua (10 m hacia +X)."""
    o = []
    for i in range(20):
        o.append(UB("tabla", 0.25 + i * 0.5, 0.42, 0, 0.46, 0.06, 1.6, r=0.015, seg=1, col=["c98d55", "b87a45"][i % 2]))
    for i in range(6):
        for s_ in (-1, 1):
            o.append(UC("pilote", 0.3 + i * 1.9, -0.6, s_ * 0.75, 0.1, 1.1, "7a4a2a", seg=10))
    o.append(UB("baranda", 5.0, 0.8, 0.78, 10.0, 0.06, 0.06, r=0.02, col="8a5a30"))
    return o


@prop
def lighthouse():
    o = [UL("base", 0, 0, 0, [(0, 0), (1.3, 0), (1.25, 0.6), (0, 0.6)], "b8aa98", seg=24)]
    for k in range(5):
        o.append(UL("torre", 0, 0.6 + k * 1.0, 0, [(0, 0), (0.9 - k * 0.08, 0), (0.85 - k * 0.08, 1.0), (0, 1.0)], "e0594a" if k % 2 == 0 else "fbf6ee", seg=24))
    o.append(UC("balcon", 0, 5.6, 0, 0.75, 0.12, "2b3040", seg=24))
    o.append(UC("luz", 0, 5.72, 0, 0.45, 0.6, "fff1b0", seg=12, emis=1.4, rough=0.1))
    o.append(UC("techo", 0, 6.32, 0, 0.55, 0.45, "2b3040", r2=0.05, seg=12))
    o.append(UC("puerta", 0, 0.6, -0.88, 0.25, 0.7, "7a4a2a", rx=90, seg=12))
    return o


@prop
def crab():
    o = [US("cuerpo", 0, 0.08, 0, 0.1, "e0594a", scale=(1.3, 0.6, 1))]
    for s_ in (-1, 1):
        o.append(US("pinza", s_ * 0.15, 0.1, 0.1, 0.05, "e0594a", scale=(1.2, 0.8, 0.8)))
        o.append(US("ojo", s_ * 0.04, 0.17, 0.07, 0.02, "fbf6ee"))
        for k in range(3):
            o.append(tube("pata", [U(s_ * 0.08, 0.06, -0.03 + k * 0.04), U(s_ * 0.17, 0.0, -0.05 + k * 0.05)], 0.012, "c4483b"))
    return o


@prop
def sand_mound():
    return [UBL("arena", 0, 0.02, 0, 0.34, SAND, seed=3, amp=0.3, scale=(1.3, 1.3, 0.35)),
            US("concha", 0.18, 0.08, 0.1, 0.04, "ffd0dc", scale=(1, 0.5, 1)),
            US("concha2", -0.15, 0.06, -0.12, 0.03, "fbf6ee", scale=(1, 0.5, 1))]


def sea_chest():
    """Cofre cerrado de la playa (forzar girando el dedo): body + lid (bisagra atras)."""
    body = [UB("caja", 0, 0, 0, 0.62, 0.36, 0.44, r=0.04, col="7a4a2a")]
    for x in (-0.22, 0.22):
        body.append(UB("fleje", x, 0, 0, 0.06, 0.37, 0.45, r=0.015, col="6d747e", metal=0.5, rough=0.4))
    body.append(UB("candado", 0, 0.22, -0.24, 0.12, 0.14, 0.05, r=0.02, col="ffd23a", metal=0.6, rough=0.3))
    body.append(UBL("arena", 0, 0.0, 0, 0.45, SAND, seed=9, amp=0.25, scale=(1.4, 1.2, 0.2)))
    lid = [UB("tapa", 0, 0.36, 0, 0.66, 0.14, 0.48, r=0.06, col="8a5a36")]
    for x in (-0.22, 0.22):
        lid.append(UB("fleje_t", x, 0.36, 0, 0.065, 0.15, 0.49, r=0.02, col="6d747e", metal=0.5, rough=0.4))
    return [("body", body, (0, 0, 0)), ("lid", lid, U(0, 0.36, 0.22))]


# ================================================================ objetos de la playa
@item
def pink_shell():
    o = []
    prof = [(0, 0), (0.09, 0.0), (0.12, 0.05), (0.1, 0.12), (0.06, 0.18), (0.02, 0.22), (0, 0.24)]
    o.append(lathe("espiral", (0, 0, 0.0), prof, "ff9ec0", seg=20, rot=(0, -70, 0)))
    o.append(blob("boca", (0.0, -0.07, 0.06), 0.08, "ffe0ea", amp=0.2, scale=(0.9, 0.6, 1.1)))
    for k in range(5):
        o.append(torus("anillo", (0.03 * k, 0, 0.06), 0.1 - k * 0.018, 0.012, "e87aa0", rot=(0, 90 - 70, 0)))
    return o


@item
def spyglass():
    o = []
    m = dict(metal=0.6, rough=0.3)
    o.append(cyl("tubo1", (-0.18, 0, 0.06), 0.05, 0.16, "d9a441", rot=(0, 90, 0), seg=20, **m))
    o.append(cyl("tubo2", (-0.03, 0, 0.06), 0.042, 0.14, "c98a2a", rot=(0, 90, 0), seg=20, **m))
    o.append(cyl("tubo3", (0.1, 0, 0.06), 0.034, 0.12, "d9a441", rot=(0, 90, 0), seg=20, **m))
    o.append(cyl("cuero", (-0.12, 0, 0.06), 0.053, 0.07, "7a4a2a", rot=(0, 90, 0), seg=20))
    o.append(cyl("lente", (-0.19, 0, 0.06), 0.045, 0.01, "8fd8f0", rot=(0, 90, 0), seg=20, rough=0.1))
    o.append(rbox("soporte", (0, 0, 0.01), (0.3, 0.1, 0.02), r=0.008, col="a8703f"))
    return o


@item
def gold_coin():
    o = []
    for k in range(4):
        o.append(cyl("moneda", (0.04 * (k % 2) - 0.02, 0.03 * (k // 2), k * 0.022), 0.09, 0.02, "ffd23a", seg=28, bev=0.006, metal=0.7, rough=0.25))
    o.append(cyl("cara", (0, 0.03, 0.09), 0.065, 0.006, "e0a22a", seg=20, metal=0.7, rough=0.3))
    o.append(sphere("perfil", (0, 0.03, 0.098), 0.035, "e8b030", scale=(0.8, 1, 0.3)))
    return o


@item
def anchor():
    o = []
    m = dict(metal=0.5, rough=0.45)
    o.append(rbox("caña", (0, 0, 0.45), (0.08, 0.08, 0.78), r=0.03, col="4a4f5a", **m))
    o.append(torus("argolla", (0, 0, 0.9), 0.08, 0.025, "4a4f5a", rot=(90, 0, 0), **m))
    o.append(rbox("cepo", (0, 0, 0.72), (0.5, 0.07, 0.07), r=0.03, col="7a4a2a"))
    pts = [(-0.38, 0, 0.28), (-0.3, 0, 0.12), (-0.15, 0, 0.06), (0, 0, 0.05), (0.15, 0, 0.06), (0.3, 0, 0.12), (0.38, 0, 0.28)]
    o.append(tube("brazos", pts, 0.045, "4a4f5a", **m))
    for s_ in (-1, 1):
        o.append(cyl("una", (s_ * 0.4, 0, 0.27), 0.08, 0.1, "4a4f5a", r2=0.0, rot=(0, s_ * -35, 0), seg=4, **m))
    o.append(tube("cadena", [(0, 0, 0.95), (0.1, 0, 1.0), (0.25, 0, 0.9), (0.3, 0, 0.6)], 0.018, "6d747e", **m))
    for k in range(4):
        o.append(sphere("oxido", (0.03 * k - 0.04, -0.04, 0.2 + k * 0.15), 0.025, "c4683a", scale=(1, 0.4, 1)))
    o.append(blob("algas", (0.25, 0, 0.12), 0.08, "4a8a5a", amp=0.3))
    return o


@item
def figurehead():
    """Mascaron de sirena (enorme): busto tallado con cola, pintado y gastado."""
    o = []
    o.append(rbox("base", (0, 0.1, 0.1), (0.6, 0.5, 0.2), r=0.05, col="7a4a2a"))
    o.append(tube("cola", [(0, 0.15, 0.2), (0.05, 0.05, 0.5), (-0.05, -0.05, 0.85), (0, -0.05, 1.1)], 0.16, "3fbfbf"))
    o.append(sphere("aleta", (0, 0.1, 0.2), 0.18, "3fbfbf", scale=(1.6, 0.4, 0.6)))
    o.append(sphere("torso", (0, -0.08, 1.25), 0.2, "f2c49b", scale=(1, 0.8, 1.2)))
    o.append(sphere("cabeza", (0, -0.12, 1.55), 0.16, "f2c49b"))
    o.append(sphere("pelo", (0, -0.06, 1.6), 0.19, "e0594a", scale=(1.05, 1.1, 1.0)))
    o.append(tube("melena", [(0, 0.05, 1.6), (0, 0.15, 1.35), (0, 0.18, 1.1)], 0.11, "e0594a"))
    for s_ in (-1, 1):
        o.append(tube("brazo", [(s_ * 0.18, -0.08, 1.35), (s_ * 0.2, -0.2, 1.2), (s_ * 0.08, -0.22, 1.12)], 0.045, "f2c49b"))
        o.append(sphere("ojo", (s_ * 0.06, -0.27, 1.57), 0.02, "2b2440"))
    o.append(rbox("conchas", (0, -0.24, 1.3), (0.2, 0.04, 0.06), r=0.02, col="ffd0dc"))
    return o


@item
def rowboat():
    """Bote a remo (gigante)."""
    o = []
    o.append(rbox("casco", (0, 0, 0.25), (1.2, 2.6, 0.5), r=0.24, col="4a8fe0"))
    o.append(sphere("proa", (0, -1.25, 0.3), 0.45, "4a8fe0", scale=(1.25, 1.0, 0.75)))
    o.append(rbox("adentro", (0, 0.05, 0.42), (1.0, 2.4, 0.2), r=0.15, col="c98d55"))
    for s_ in (-1, 1):
        o.append(rbox("borda", (s_ * 0.6, 0, 0.5), (0.08, 2.6, 0.07), r=0.03, col="fbf6ee"))
    o.append(rbox("popa", (0, 1.28, 0.5), (1.2, 0.08, 0.07), r=0.03, col="fbf6ee"))
    for y in (-0.5, 0.45):
        o.append(rbox("banco", (0, y, 0.48), (1.05, 0.2, 0.05), r=0.02, col="a8703f"))
    for s_ in (-1, 1):
        o.append(tube("remo", [(s_ * 0.5, 0.0, 0.55), (s_ * 1.25, 0.4, 0.25)], 0.03, "c98d55"))
        o.append(rbox("pala", (s_ * 1.35, 0.48, 0.2), (0.12, 0.32, 0.02), r=0.02, col="c98d55", rot=(0, 0, s_ * 30)))
    o.append(torus("salvavidas", (0.45, 1.05, 0.62), 0.14, 0.05, "e0594a", rot=(0, 0, 0)))
    return o


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(PROPS) + list(ITEMS) + ["sea_chest"]
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if prev:
        os.makedirs(prev, exist_ok=True)
    for n in only:
        lib.reset()
        if n == "sea_chest":
            parts, t = lib.finish_parts(sea_chest(), "prop_sea_chest", OUT, ao=0.5, ao_dist=0.2)
            print("BEACH", n, t)
            if prev:
                lib.preview(parts, os.path.join(prev, "prop_sea_chest.png"), size=320, elev=30, azim=196)
            continue
        if n in PROPS:
            objs = [x for x in PROPS[n]() if x is not None]
            o, t = lib.finish(objs, "prop_" + n, OUT, ao=0.5, ao_dist=0.3)
            print("BEACH", n, t)
            if prev:
                lib.preview([o], os.path.join(prev, "prop_" + n + ".png"), size=320, elev=30, azim=196)
        else:
            objs = [x for x in ITEMS[n]() if x is not None]
            o, t = lib.finish(objs, "item_" + n, OUT)
            print("BEACH item", n, t)
            if prev:
                lib.preview([o], os.path.join(prev, n + ".png"), size=320)


if __name__ == "__main__":
    main()
