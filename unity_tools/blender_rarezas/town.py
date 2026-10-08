"""Pueblo y bosque (DIRECCION_CREATIVA 3) en Blender por scripts: piezas sueltas que Unity reparte por el mapa
(Rarezas.View.WorldMap) y junta por zonas para dibujarlas en pocas llamadas.

Pueblo: fuente, farol, banco, puestito de limonada, casas (3), cerco, cartel de camino con icono, tranquera con candado.
Bosque: pino (2), Viejo Roble, arbusto, roca (2), tocon, tronco hueco, hongos gigantes, flores, pasto.
Base en y=0 (Unity), frente hacia -Z (la camara).

Uso: blender -b -P town.py -- [--only fountain,pine_a,...] [--preview carpeta]
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, torus, tube, blob, lathe
from shack import U, UB, UC, US, text

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
PROPS = {}


def prop(fn):
    PROPS[fn.__name__] = fn
    return fn


def UL(name, x, y, z, prof, col, seg=28, **kw):
    return lathe(name, U(x, y, z), prof, col, seg=seg, **kw)


def UBL(name, x, y, z, r, col, **kw):
    return blob(name, U(x, y, z), r, col, **kw)


# ================================================================ PUEBLO
@prop
def fountain():
    o = []
    o.append(UL("pileta", 0, 0, 0, [(0, 0), (1.25, 0), (1.3, 0.05), (1.3, 0.38), (1.15, 0.42), (1.08, 0.38), (1.08, 0.12), (0, 0.12)], "e8dcc8", seg=40))
    o.append(UC("agua", 0, 0.3, 0, 1.09, 0.04, "6cc4e8", seg=40, rough=0.1))
    o.append(UL("columna", 0, 0.12, 0, [(0, 0), (0.22, 0), (0.18, 0.1), (0.12, 0.6), (0.16, 0.7), (0, 0.7)], "e8dcc8", seg=24))
    o.append(UL("plato", 0, 0.8, 0, [(0, 0), (0.08, 0), (0.55, 0.08), (0.6, 0.16), (0.5, 0.16), (0, 0.1)], "f3ead8", seg=32))
    o.append(UC("agua2", 0, 0.92, 0, 0.5, 0.03, "8fd8f0", seg=32, rough=0.1))
    o.append(UL("remate", 0, 0.9, 0, [(0, 0), (0.1, 0), (0.07, 0.25), (0.12, 0.32), (0, 0.45)], "e8dcc8", seg=20))
    for k in range(8):
        a = k * math.pi / 4
        o.append(US("chorro", math.cos(a) * 0.62, 0.62 - 0.0, math.sin(a) * 0.62, 0.06, "bfeaff", scale=(1, 1.8, 1), emis=0.2, rough=0.1))
    for k in range(10):
        a = k * 0.63
        o.append(US("borde_flor", math.cos(a) * 1.22, 0.44, math.sin(a) * 1.22, 0.05, "ff5d8f" if k % 2 else "ffd34d"))
    return o


@prop
def lamp():
    o = []
    o.append(UC("base", 0, 0, 0, 0.14, 0.12, "2b3040", seg=16, bev=0.02))
    o.append(UC("poste", 0, 0.1, 0, 0.045, 1.7, "2b3040", seg=12))
    o.append(torus("anillo", U(0, 0.9, 0), 0.06, 0.015, "d9a441", seg=16))
    o.append(UC("farol", 0, 1.78, 0, 0.12, 0.28, "fff1b0", seg=8, emis=1.0, rough=0.2))
    o.append(UC("techo", 0, 2.05, 0, 0.17, 0.12, "2b3040", r2=0.03, seg=8, bev=0.01))
    o.append(US("punta", 0, 2.2, 0, 0.035, "d9a441"))
    o.append(UC("base_f", 0, 1.74, 0, 0.14, 0.05, "2b3040", seg=8))
    return o


@prop
def bench():
    o = []
    for x in (-0.55, 0.55):
        o.append(UB("pata", x, 0, 0, 0.08, 0.32, 0.36, r=0.02, col="2b3040"))
        o.append(UB("apoyo", x, 0.3, 0.16, 0.08, 0.42, 0.06, r=0.02, col="2b3040", rx=10))
    for k in range(3):
        o.append(UB("tabla", 0, 0.32, -0.12 + k * 0.12, 1.3, 0.05, 0.1, r=0.02, col="c98d55"))
    for k in range(2):
        o.append(UB("respaldo", 0, 0.5 + k * 0.14, 0.2 + k * 0.025, 1.3, 0.09, 0.04, r=0.02, col="c98d55", rx=10))
    return o


@prop
def lemonade():
    o = []
    o.append(UB("mostrador", 0, 0, 0, 1.2, 0.7, 0.55, r=0.05, col="ffd34d"))
    for k in range(5):
        o.append(UB("franja", -0.48 + k * 0.24, 0.05, -0.28, 0.1, 0.6, 0.01, r=0.01, col="fbf6ee"))
    o.append(UB("tapa", 0, 0.7, 0, 1.3, 0.05, 0.62, r=0.02, col="c98d55"))
    for x in (-0.58, 0.58):
        o.append(UB("palo", x, 0.7, 0.2, 0.06, 0.9, 0.06, r=0.02, col="c98d55"))
    for k in range(6):
        o.append(UB("toldo", -0.55 + k * 0.22, 1.6, 0.0, 0.22, 0.06, 0.8, rx=-12, r=0.015, col="e0594a" if k % 2 == 0 else "fbf6ee"))
    o.append(UL("jarra", -0.3, 0.75, 0.0, [(0, 0), (0.1, 0), (0.12, 0.15), (0.08, 0.22), (0, 0.22)], "fff3b0", seg=16, rough=0.1))
    for k in range(3):
        o.append(UC("vaso", 0.1 + k * 0.14, 0.75, -0.05, 0.04, 0.1, "fff3b0", seg=12, rough=0.1))
    o.append(US("limon", 0.45, 0.8, 0.1, 0.06, "ffd34d", scale=(1.2, 1, 1)))
    o.append(US("limon", 0.38, 0.79, 0.18, 0.06, "ffd34d", scale=(1.2, 1, 1)))
    o.append(UB("cartel", 0, 1.0, -0.32, 0.5, 0.25, 0.04, r=0.03, col="fbf6ee"))
    o.append(US("limon_logo", 0, 1.12, -0.35, 0.07, "ffd34d", scale=(1.3, 1, 0.4)))
    return o


def house(wall, roof, trim, door, seed, w=3.2, d=2.6, h=1.9, chimney=True):
    rnd = random.Random(seed)
    o = []
    o.append(UB("base", 0, 0, 0, w + 0.15, 0.18, d + 0.15, r=0.05, col="b8aa98"))
    o.append(UB("paredes", 0, 0.15, 0, w, h, d, r=0.08, col=wall))
    # techo a dos aguas: la cumbrera corre a lo largo de X y cada faldon baja hacia adelante o hacia atras
    pitch = 34.0
    run = d * 0.5 + 0.25
    rise = run * math.tan(math.radians(pitch))
    top = h + 0.15
    slab = run / math.cos(math.radians(pitch))
    for s_ in (-1, 1):
        o.append(UB("techo", 0, top + rise * 0.5 - 0.07, s_ * run * 0.5, w + 0.4, 0.14, slab + 0.1, rx=s_ * pitch, r=0.06, col=roof))
    o.append(UB("cumbrera", 0, top + rise - 0.06, 0, w + 0.45, 0.12, 0.18, r=0.05, col=trim))
    # tapas de los costados del techo (el triangulo de la pared queda debajo del faldon)
    for x in (-w * 0.5 + 0.03, w * 0.5 - 0.03):
        for k in range(5):
            frac = 1.0 - (k + 0.5) / 5
            o.append(UB("hastial", x, top + k * (rise - 0.12) / 5, 0, 0.06, (rise - 0.12) / 5 + 0.01, d * frac * 0.98, r=0.02, col=wall))
    o.append(UB("puerta", -w * 0.18, 0.15, -d * 0.5 - 0.03, 0.62, 1.05, 0.08, r=0.05, col=door))
    o.append(US("pomo", -w * 0.18 + 0.2, 0.68, -d * 0.5 - 0.08, 0.035, "d9a441"))
    o.append(UB("escalon", -w * 0.18, 0.0, -d * 0.5 - 0.22, 0.9, 0.12, 0.3, r=0.03, col="b8aa98"))
    for x in (w * 0.22,):
        o.append(UB("ventana", x, 0.75, -d * 0.5 - 0.02, 0.62, 0.58, 0.06, r=0.04, col=trim))
        o.append(UB("vidrio", x, 0.8, -d * 0.5 - 0.05, 0.48, 0.44, 0.04, r=0.03, col="8fd0f0", rough=0.1, emis=0.15))
        o.append(UB("cruz_v", x, 0.8, -d * 0.5 - 0.075, 0.04, 0.44, 0.02, r=0.01, col=trim))
        o.append(UB("cruz_h", x, 1.0, -d * 0.5 - 0.075, 0.48, 0.04, 0.02, r=0.01, col=trim))
        o.append(UB("maceta", x, 0.62, -d * 0.5 - 0.15, 0.62, 0.14, 0.18, r=0.03, col="c4683a"))
        for k in range(4):
            o.append(US("flor", x - 0.22 + k * 0.15, 0.82, -d * 0.5 - 0.16, 0.06, ["ff5d8f", "ffd34d", "fbf6ee", "e0594a"][k]))
    if chimney:
        o.append(UB("chimenea", w * 0.3, h + 0.4, d * 0.15, 0.32, 0.8, 0.32, r=0.04, col="c4683a"))
    o.append(UBL("arbusto", w * 0.5 + 0.05, 0.25, -d * 0.4, 0.35, "5aa64e", amp=0.25))
    return o


@prop
def house_a():
    return house("fff3d6", "e0594a", "fbf6ee", "4a8fe0", 1)


@prop
def house_b():
    return house("bfe4d8", "4a8fe0", "fbf6ee", "e0594a", 2, w=2.8, d=2.4, h=1.7)


@prop
def house_c():
    return house("ffd9b0", "6a8a4a", "fff3d6", "8a5530", 3, w=3.4, d=2.6, h=2.0, chimney=False)


@prop
def fence():
    """Tramo de cerco de 2 m (a lo largo de X)."""
    o = []
    for x in (-0.95, 0.0, 0.95):
        o.append(UB("poste", x, 0, 0, 0.1, 0.62, 0.1, r=0.03, col="fbf6ee"))
        o.append(UB("punta", x, 0.6, 0, 0.07, 0.07, 0.07, r=0.03, col="fbf6ee", rz=45))
    for y in (0.22, 0.45):
        o.append(UB("travesano", 0, y, 0.0, 2.0, 0.07, 0.05, r=0.02, col="f3ead8"))
    return o


@prop
def signpost():
    """Cartel de camino: el icono lo pone Unity en la tabla (sprite), aca va la madera."""
    o = []
    o.append(UB("poste", 0, 0, 0, 0.1, 1.4, 0.1, r=0.03, col="8a5530"))
    o.append(UB("flecha", 0.15, 1.05, -0.06, 0.75, 0.32, 0.05, r=0.05, col="c98d55"))
    o.append(UB("punta", 0.55, 1.07, -0.06, 0.22, 0.22, 0.05, rz=45, r=0.03, col="c98d55"))
    o.append(UB("cara", 0.12, 1.06, -0.09, 0.62, 0.24, 0.01, r=0.04, col="fff3d6"))
    return o


@prop
def gate():
    """Tranquera cerrada con candado (zona bloqueada). 3 m de ancho."""
    o = []
    for x in (-1.5, 1.5):
        o.append(UB("poste", x, 0, 0, 0.18, 1.1, 0.18, r=0.05, col="7a4a2a"))
        o.append(US("bocha", x, 1.15, 0, 0.1, "7a4a2a"))
    for y in (0.3, 0.6, 0.9):
        o.append(UB("tabla", 0, y, 0, 2.9, 0.1, 0.06, r=0.03, col="a8703f"))
    o.append(UB("diagonal", 0, 0.32, -0.02, 3.1, 0.09, 0.05, rz=-11, r=0.03, col="8a5530"))
    o.append(UB("candado", 0, 0.5, -0.08, 0.26, 0.24, 0.12, r=0.05, col="ffd23a", metal=0.6, rough=0.3))
    o.append(torus("arco", U(0, 0.68, -0.08), 0.08, 0.025, "b8c0c8", rot=(90, 0, 0), metal=0.6, rough=0.3))
    o.append(US("ojo", 0, 0.48, -0.15, 0.03, "2b2440"))
    return o


@prop
def well():
    o = []
    o.append(UL("aro", 0, 0, 0, [(0, 0), (0.6, 0), (0.62, 0.55), (0.5, 0.58), (0.48, 0.1), (0, 0.1)], "b8aa98", seg=24))
    o.append(UC("agua", 0, 0.3, 0, 0.49, 0.02, "4a8fe0", seg=24))
    for x in (-0.55, 0.55):
        o.append(UB("poste", x, 0.5, 0, 0.09, 0.9, 0.09, r=0.03, col="8a5530"))
    o.append(UB("techito", 0, 1.45, -0.25, 1.4, 0.07, 0.6, rx=-28, r=0.03, col="e0594a"))
    o.append(UB("techito", 0, 1.45, 0.25, 1.4, 0.07, 0.6, rx=28, r=0.03, col="e0594a"))
    o.append(UC("rodillo", -0.55, 1.05, 0, 0.06, 1.1, "c98d55", rz=-90))
    o.append(UC("balde", 0.1, 0.62, 0, 0.11, 0.17, "a8703f", seg=14))
    return o


# ================================================================ BOSQUE
def pine(seed, h=2.6, col="3f8a4a", cold="2f6b3a"):
    rnd = random.Random(seed)
    o = [UC("tronco", 0, 0, 0, 0.13, h * 0.35, "8a5530", r2=0.1, seg=10, bev=0.02)]
    tiers = 3
    for k in range(tiers):
        y = h * (0.22 + k * 0.22)
        r = 0.85 * (1 - k * 0.24)
        hh = h * 0.42
        o.append(UL("copa", rnd.uniform(-0.03, 0.03), y, rnd.uniform(-0.03, 0.03),
                    [(0, 0), (r, 0.0), (r * 1.02, 0.08), (r * 0.55, hh * 0.55), (0.06, hh), (0, hh)], col if k % 2 == 0 else cold, seg=14))
    o.append(US("punta", 0, h * 0.22 + 2 * h * 0.22 + h * 0.42 - 0.02, 0, 0.07, col))
    return o


@prop
def pine_a():
    return pine(1)


@prop
def pine_b():
    return pine(2, h=3.2, col="4a9a52", cold="35784a")


@prop
def oak():
    """El Viejo Roble: arbol enorme y redondo, hito del bosque."""
    rnd = random.Random(9)
    o = [UL("tronco", 0, 0, 0, [(0, 0), (0.75, 0), (0.55, 0.3), (0.42, 1.2), (0.5, 1.8), (0, 1.9)], "7a4a2a", seg=14)]
    for k in range(5):
        a = k * 1.256
        o.append(tube("raiz", [U(math.cos(a) * 0.3, 0.3, math.sin(a) * 0.3), U(math.cos(a) * 0.95, 0.03, math.sin(a) * 0.95)], 0.12, "6a4026"))
    for k in range(3):
        a = k * 2.1 + 0.4
        o.append(tube("rama", [U(0, 1.5, 0), U(math.cos(a) * 1.0, 2.3, math.sin(a) * 1.0)], 0.13, "7a4a2a"))
    copa = [(0, 3.0, 0, 1.6), (1.1, 2.6, 0.3, 1.15), (-1.1, 2.7, -0.2, 1.2), (0.3, 2.6, -1.0, 1.1), (-0.2, 2.5, 1.0, 1.1), (0.2, 3.7, 0.1, 1.0)]
    for i, (x, y, z, r) in enumerate(copa):
        o.append(UBL("copa", x, y, z, r, "5aa64e" if i % 2 == 0 else "4a9a42", seed=i + 3, amp=0.18))
    o.append(UB("hueco", 0.0, 0.7, -0.45, 0.32, 0.42, 0.1, r=0.12, col="2b1e18"))
    return o


@prop
def bush():
    return [UBL("arbusto", 0, 0.3, 0, 0.45, "5aa64e", seed=4, amp=0.22, scale=(1.3, 1.3, 1)),
            UBL("arbusto2", 0.35, 0.22, 0.1, 0.3, "6ab45a", seed=5, amp=0.22),
            US("baya", -0.2, 0.55, -0.3, 0.05, "e0594a"), US("baya", 0.1, 0.62, -0.35, 0.05, "e0594a")]


@prop
def rock_a():
    return [UBL("roca", 0, 0.18, 0, 0.42, "9aa1aa", seed=6, amp=0.3, scale=(1.3, 1.0, 0.7)),
            UBL("musgo", -0.1, 0.38, 0.05, 0.22, "76ab3a", seed=2, amp=0.3, scale=(1.2, 1.2, 0.4))]


@prop
def rock_b():
    return [UBL("roca", 0, 0.12, 0, 0.3, "8a929c", seed=8, amp=0.32, scale=(1.2, 1.0, 0.8)),
            UBL("roca2", 0.35, 0.08, 0.1, 0.18, "a9b0b8", seed=9, amp=0.3)]


@prop
def stump():
    o = [UC("tocon", 0, 0, 0, 0.32, 0.32, "8a5530", r2=0.28, seg=16, bev=0.04),
         UC("corte", 0, 0.32, 0, 0.27, 0.012, "e8c89a", seg=16),
         torus("anillo", U(0, 0.33, 0), 0.15, 0.008, "c49a5a", seg=16)]
    for k in range(4):
        a = k * 1.57 + 0.3
        o.append(tube("raiz", [U(math.cos(a) * 0.2, 0.12, math.sin(a) * 0.2), U(math.cos(a) * 0.48, 0.0, math.sin(a) * 0.48)], 0.07, "7a4a2a"))
    o.append(US("hongo", 0.3, 0.12, -0.2, 0.07, "e0594a", scale=(1, 0.5, 1)))
    return o


@prop
def hollow_log():
    o = [UC("tronco", -0.9, 0.35, 0, 0.36, 1.8, "8a5530", rz=-90, seg=18, bev=0.04),
         UC("hueco", -0.95, 0.35, 0, 0.27, 1.9, "3a2a20", rz=-90, seg=16)]
    for x in (-0.9, 0.9):
        o.append(torus("borde", U(x, 0.35, 0), 0.31, 0.05, "c49a5a", rot=(0, 90, 0), seg=18))
    o.append(UBL("musgo", 0.2, 0.68, 0.0, 0.3, "76ab3a", seed=3, amp=0.3, scale=(2.2, 1, 0.4)))
    for k in range(3):
        o.append(US("hongo", -0.4 + k * 0.4, 0.72, -0.25, 0.06, "f0cf6a", scale=(1, 0.45, 1)))
    return o


@prop
def mushroom_big():
    o = [UC("pie", 0, 0, 0, 0.16, 0.7, "f3ead8", r2=0.12, seg=16, bev=0.04)]
    o.append(UL("sombrero", 0, 0.65, 0, [(0, 0), (0.55, 0), (0.56, 0.05), (0.45, 0.22), (0.2, 0.34), (0, 0.36)], "e0594a", seg=24))
    for k in range(7):
        a = k * 0.9
        r = 0.15 + (k % 3) * 0.12
        o.append(US("lunar", math.cos(a) * r, 0.65 + 0.36 - r * 0.45, math.sin(a) * r, 0.05, "fbf6ee", scale=(1, 0.45, 1)))
    o.append(UC("pie2", 0.45, 0, -0.2, 0.07, 0.3, "f3ead8", seg=12))
    o.append(UL("sombrero2", 0.45, 0.28, -0.2, [(0, 0), (0.22, 0), (0.18, 0.1), (0, 0.15)], "e0594a", seg=16))
    return o


@prop
def flowers():
    rnd = random.Random(12)
    o = []
    for k in range(7):
        x, z = rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4)
        col = ["ff5d8f", "ffd34d", "fbf6ee", "b06ef0"][k % 4]
        o.append(tube("tallo", [U(x, 0, z), U(x, 0.18, z)], 0.01, "4a8a3a"))
        o.append(US("flor", x, 0.2, z, 0.045, col, scale=(1, 0.6, 1)))
        o.append(US("centro", x, 0.225, z, 0.02, "ffd23a"))
    return o


@prop
def grass():
    rnd = random.Random(13)
    o = []
    for k in range(9):
        x, z = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3)
        o.append(UC("hoja", x, 0, z, 0.03, rnd.uniform(0.15, 0.3), "76ab3a" if k % 2 else "8cc247", r2=0.0, seg=5,
                    rx=rnd.uniform(-15, 15), rz=rnd.uniform(-15, 15)))
    return o


@prop
def find_mound():
    """Montoncito de tierra / hojas donde hay algo (punto de hallazgo): el objeto asoma encima."""
    return [UBL("tierra", 0, 0.02, 0, 0.32, "a8804a", seed=2, amp=0.3, scale=(1.3, 1.3, 0.35)),
            UBL("hojas", 0.15, 0.06, 0.1, 0.16, "c98d55", seed=3, amp=0.3, scale=(1.3, 1.3, 0.4)),
            UBL("hojas2", -0.18, 0.05, -0.08, 0.13, "e0a24a", seed=4, amp=0.3, scale=(1.3, 1.3, 0.4))]


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else list(PROPS)
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if prev:
        os.makedirs(prev, exist_ok=True)
    for n in only:
        lib.reset()
        objs = [x for x in PROPS[n]() if x is not None]
        o, t = lib.finish(objs, "prop_" + n, OUT, ao=0.5, ao_dist=0.3)
        print("PROP", n, t)
        if prev:
            lib.preview([o], os.path.join(prev, "prop_" + n + ".png"), size=320, elev=35, azim=196)


if __name__ == "__main__":
    main()
