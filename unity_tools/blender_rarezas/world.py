"""Local, terreno y cajas de Curio Barn en Blender por scripts.

Las medidas se escriben en coordenadas de UNITY (x derecha, y arriba, z hacia el fondo) con U(): asi coinciden 1 a 1
con Rarezas.View.Stage (piso de x -2.4..2.4, z -1.7..2.0, cara de arriba a y 0.16; mesa del fondo en z 1.5; pedestales
de los costados en x +-1.8, z 0.35 y -0.55).

Uso: blender -b -P world.py -- [--only terrain,stage0,stage1,stage2,crates] [--preview carpeta]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, lathe, torus, tube, blob

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
FY = 0.16          # cara de arriba del piso
X0, X1, Z0, Z1 = -2.4, 2.4, -1.7, 2.0
BACK_X = (-1.35, -0.45, 0.45, 1.35)
SIDE = ((-1.8, 0.35), (-1.8, -0.55), (1.8, 0.35), (1.8, -0.55))


def U(x, y, z):
    """Unity (x, y, z) -> Blender (-x, -z, y) (lib.export hace la vuelta)."""
    return (-x, -z, y)


def UB(name, x, y, z, w, h, d, **kw):
    """Caja redondeada con centro de BASE en Unity (x, y, z), ancho w (x), alto h (y), fondo d (z)."""
    return rbox(name, U(x, y + h * 0.5, z), (w, d, h), **kw)


def UC(name, x, y, z, r, h, col, **kw):
    """Cilindro vertical con base en Unity (x, y, z)."""
    return cyl(name, U(x, y, z), r, h, col, **kw)


def US(name, x, y, z, r, col, **kw):
    return sphere(name, U(x, y, z), r, col, **kw)


def UBL(name, x, y, z, r, col, **kw):
    return blob(name, U(x, y, z), r, col, **kw)


# ================================================================ terreno (fijo para todas las etapas)
def terrain():
    o = []
    o.append(UC("meseta", 0, -0.6, 1.5, 16, 0.6, "grass", seg=64, bev=0.25))
    import random
    rnd = random.Random(11)
    for i in range(30):
        a, r = rnd.uniform(0, 6.283), rnd.uniform(3.4, 11)
        x, z = math.cos(a) * r, 1.5 + math.sin(a) * r
        if abs(x) < 1.4 and z < -1:
            continue
        o.append(UC("mancha", x, 0.004, z, rnd.uniform(0.4, 0.9), 0.012, "grassd", seg=16))
    for i in range(10):
        z = -2.05 - i * 0.72
        w = 0.62 + 0.06 * math.sin(i * 1.7)
        c = UC("camino", 0.08 * math.sin(i * 0.9), 0.006 + i * 0.0005, z, w, 0.02, "dirt", seg=20)
        c.scale = (1, 0.75, 1)
        o.append(c)
    for i in range(12):
        z = -2.2 - i * 0.55
        x = (-0.78 if i % 2 == 0 else 0.8) + rnd.uniform(-0.1, 0.1)
        o.append(US("piedrita", x, 0.01, z, rnd.uniform(0.05, 0.09), "dirtd", scale=(1, 1, 0.5)))
    # arboles de copa redonda alrededor
    spots = [(-3.6, 4.2), (3.3, 4.6), (-1.2, 5.6), (1.6, 6.3), (-4.6, 1.6), (4.5, 1.9), (-4.3, 7.4), (4.6, 7.9), (0.2, 8.6),
             (-2.7, 9.6), (2.8, 10.1), (-5.6, 4.6), (5.6, 5.0), (-3.4, -4.6), (3.6, -5.0)]
    for i, (x, z) in enumerate(spots):
        s = 0.85 + 0.25 * math.sin(i * 2.3)
        o.append(UC("tronco", x, 0, z, 0.14 * s, 0.9 * s, "8a5a3a", r2=0.1 * s, seg=10, bev=0.03))
        o.append(UBL("copa", x, 1.2 * s, z, 0.78 * s, "5cae47" if i % 2 else "6cbd52", seed=i, amp=0.14))
        o.append(UBL("copa2", x + 0.3 * s, 1.62 * s, z - 0.2 * s, 0.45 * s, "7cc85a", seed=i + 40, amp=0.14))
    for i, (x, z) in enumerate([(-2.5, -3.2), (2.6, -2.9), (-3.3, -0.6), (3.4, -0.9), (-2.1, -5.7), (2.3, -6.3), (-3.0, 2.8), (3.1, 3.0)]):
        o.append(UBL("arbusto", x, 0.2, z, 0.44, "4f9e3f", seed=60 + i, amp=0.2))
        o.append(UBL("arbusto2", x + 0.32, 0.16, z - 0.2, 0.28, "7cc85a", seed=70 + i, amp=0.2))
        for k in range(3):
            o.append(US("flor", x + math.cos(k * 2.1) * 0.35, 0.36, z + math.sin(k * 2.1) * 0.3, 0.05, ["ff5d8f", "ffd34d", "fbf6ee"][k]))
    for s_ in (-1, 1):
        for j in range(7):
            z = -2.4 - j * 0.9
            o.append(UB("poste", s_ * 1.45, 0, z, 0.09, 0.44, 0.09, r=0.03, col="e9dcc0"))
            if j < 6:
                o.append(UB("tabla", s_ * 1.45, 0.25, z - 0.45, 0.05, 0.06, 0.9, r=0.02, col="e9dcc0"))
    o.append(US("roca", 1.3, 0.06, -4.4, 0.22, "stone", scale=(1.2, 1, 0.7)))
    o.append(US("roca2", -1.4, 0.05, -5.0, 0.16, "stone", scale=(1.2, 1, 0.7)))
    return o


# ================================================================ piezas comunes del local
def floor(top="woodl", alt="wood", base="stone", tiles=False):
    o = []
    o.append(UB("base", 0, -0.04, 0.15, 4.95, 0.12, 4.05, r=0.05, col=base))
    if tiles:
        n = 8
        for i in range(n):
            for j in range(7):
                x = X0 + (X1 - X0) * (i + 0.5) / n
                z = Z0 + (Z1 - Z0) * (j + 0.5) / 7
                o.append(UB("baldosa", x, 0.06, z, (X1 - X0) / n - 0.03, FY - 0.06, (Z1 - Z0) / 7 - 0.03, r=0.02,
                            col=top if (i + j) % 2 == 0 else alt))
    else:
        n = 9
        for i in range(n):
            x = X0 + (X1 - X0) * (i + 0.5) / n
            o.append(UB("tablon", x, 0.06, 0.15, (X1 - X0) / n - 0.025, FY - 0.06, Z1 - Z0, r=0.035, col=top if i % 2 == 0 else alt))
            for z in (-1.2, 0.3, 1.6):
                o.append(US("clavo", x, FY, z, 0.012, "woodd"))
    o.append(UB("escalon", 0, 0, Z0 - 0.22, 1.3, 0.09, 0.42, r=0.04, col="woodd"))
    return o


def back_table(stage):
    """Mesa del fondo con 4 lugares (y respaldo con estante chico)."""
    o = []
    z, top = 1.5, FY + 0.52
    if stage == 0:
        wood, woodd = "woodl", "wood"
        o.append(UB("tapa", 0, top - 0.08, z, 3.7, 0.08, 0.62, r=0.035, col=wood))
        for sx in (-1, 1):
            for f in (-1, 1):
                o.append(UB("pata", sx * 1.72, FY, z + f * 0.22, 0.1, top - 0.08 - FY, 0.1, r=0.03, col=woodd))
        o.append(UB("respaldo", 0, top, z + 0.33, 3.7, 0.62, 0.07, r=0.03, col=woodd))
        o.append(UB("estantito", 0, top + 0.62, z + 0.25, 3.8, 0.07, 0.24, r=0.03, col=wood))
        cloth = "4a8fe0"
    elif stage == 1:
        o.append(UB("mostrador", 0, FY, z, 3.8, top - FY, 0.66, r=0.05, col="3f7f6a"))
        o.append(UB("tapa", 0, top - 0.04, z, 3.9, 0.05, 0.72, r=0.025, col="c98d55"))
        for k in range(5):
            o.append(UB("panel", -1.52 + k * 0.76, FY + 0.08, z - 0.335, 0.6, 0.3, 0.02, r=0.02, col="4f9a80"))
        o.append(UB("respaldo", 0, top, z + 0.36, 3.9, 0.9, 0.08, r=0.03, col="c98d55"))
        for y in (0.35, 0.68):
            o.append(UB("estante", 0, top + y, z + 0.27, 3.9, 0.05, 0.2, r=0.02, col="a8703f"))
        cloth = "c0392b"
    else:
        for x in BACK_X:
            o.append(UB("plinto", x, FY, z, 0.62, top - FY - 0.05, 0.62, r=0.04, col="f2ede4"))
            o.append(UB("cornisa", x, top - 0.06, z, 0.7, 0.06, 0.7, r=0.03, col="gold", metal=0.6, rough=0.3))
            o.append(UB("zocalo", x, FY, z, 0.7, 0.08, 0.7, r=0.03, col="dcd4c6"))
        cloth = "7a2a4a"
    for x in BACK_X:
        o.append(UC("mantel", x, top - 0.005, z, 0.27, 0.02, cloth, seg=24, bev=0.008))
    return o


def pedestal(stage, i):
    x, z = SIDE[i]
    top = FY + 0.52
    o = []
    if stage == 0:
        o.append(UB("barril", x, FY, z, 0.5, top - FY - 0.04, 0.5, r=0.12, col="wood"))
        for y in (0.12, 0.38):
            o.append(UC("aro", x, FY + y, z, 0.255, 0.035, "stoned", seg=24))
        o.append(UB("tabla", x, top - 0.05, z, 0.58, 0.05, 0.58, r=0.02, col="woodl"))
    elif stage == 1:
        o.append(UC("columna", x, FY, z, 0.2, top - FY - 0.06, "3f7f6a", seg=20, bev=0.03))
        o.append(UC("tapa", x, top - 0.07, z, 0.28, 0.07, "c98d55", seg=24, bev=0.02))
    else:
        o.append(UC("pedestal", x, FY, z, 0.22, top - FY - 0.06, "f2ede4", seg=20, bev=0.03))
        o.append(UC("anillo", x, top - 0.08, z, 0.29, 0.08, "gold", seg=24, bev=0.02, metal=0.6, rough=0.3))
    o.append(UC("mantel", x, top - 0.005, z, 0.22, 0.02, "4a8fe0" if stage == 0 else "c0392b" if stage == 1 else "7a2a4a", seg=20, bev=0.006))
    return o


def hay_bale(x, z, rot, y=FY):
    o = [UB("fardo", x, y, z, 0.76, 0.4, 0.5, r=0.08, col="hay")]
    for d in (-0.18, 0.18):
        o.append(UB("hilo", x + d, y, z, 0.03, 0.41, 0.51, r=0.01, col="hayd"))
    return o


def lantern_post(x, z, h):
    o = [UB("poste", x, FY, z, 0.18, h, 0.18, r=0.05, col="woodd")]
    o.append(US("bola", x, FY + h + 0.07, z, 0.11, "wall"))
    o.append(UB("brazo", x - math.copysign(0.13, x), FY + h - 0.35, z - 0.06, 0.24, 0.04, 0.05, r=0.015, col="woodd"))
    lx = x - math.copysign(0.24, x)
    o.append(UC("farol", lx, FY + h - 0.58, z - 0.06, 0.07, 0.16, "ffe7a0", emis=0.8))
    o.append(UC("techito", lx, FY + h - 0.42, z - 0.06, 0.1, 0.08, "2b3040", r2=0.02))
    return o


# ================================================================ etapa 0: establo de madera con paja
def stage0():
    o = floor()
    zb = Z1 + 0.1
    planks = 12
    pw = 4.9 / planks
    for i in range(planks):
        x = -2.45 + pw * (i + 0.5)
        h = 1.8 + (1.15 - abs(x) / 2.45 * 1.15)
        o.append(UB("tablon", x, 0, zb, pw - 0.02, h, 0.16, r=0.03, col="red" if i % 2 == 0 else "redd"))
    for sx in (-1, 1):
        o.append(UB("marco", sx * 2.45, 0, zb - 0.04, 0.14, 1.85, 0.2, r=0.04, col="wall"))
    o.append(UB("viga", 0, 1.74, zb - 0.06, 5.0, 0.12, 0.2, r=0.04, col="wall"))
    # aleros inclinados: se rotan sobre el eje de la pared
    for sx in (-1, 1):
        r = rbox("techo", U(sx * 1.33, 2.43, zb - 0.05), (2.98, 0.86, 0.18), r=0.05, col="8a5a3a", rot=(0, -sx * 25.6, 0))
        o.append(r)
    # X del granero sobre la puerta del pajar
    o.append(UC("ventana", 0, 2.25, zb - 0.06, 0.3, 0.05, "wall", seg=28, rot=(90, 0, 0), bev=0.02))
    o.append(UC("hueco", 0, 2.25, zb - 0.1, 0.22, 0.04, "5a3a2a", seg=28, rot=(90, 0, 0)))
    for sx in (-1, 1):
        x = sx * 2.4
        h = 1.05 if sx < 0 else 0.8
        for i in range(4):
            o.append(UB("pared", x, 0.12 + i * h / 4, 0.2, 0.1, h / 4 - 0.02, 3.65, r=0.03, col="wood" if i % 2 == 0 else "woodd"))
        o.extend(lantern_post(x, Z0 + 0.02, h + 0.55))
        o.append(UB("esquina", x, 0, Z1 - 0.05, 0.18, h + 0.3, 0.18, r=0.05, col="woodd"))
    o.extend(back_table(0))
    o.extend(hay_bale(-1.95, 1.5, 0))
    o.extend(hay_bale(-1.95, 1.55, 0, FY + 0.4))
    o.extend(hay_bale(2.0, 1.4, 0))
    import random
    rnd = random.Random(3)
    for i in range(16):
        x, z = rnd.uniform(-2.1, 2.1), rnd.uniform(-1.4, 1.9)
        if abs(z - 0.72) < 0.35:
            continue
        o.append(tube("paja", [U(x, FY + 0.005, z), U(x + rnd.uniform(-0.15, 0.15), FY + 0.005, z + rnd.uniform(-0.1, 0.1))], 0.012, "hayd"))
    o.extend(register(1.75, -1.3, 0))
    o.append(UC("barril", -1.95, FY, -1.3, 0.24, 0.42, "wood", seg=18, bev=0.06))
    for s_ in (-1, 1):
        o.extend(planter(s_ * 1.05, Z0 - 0.35, "ff5d8f" if s_ < 0 else "ffd34d"))
    return o


def register(x, z, stage):
    o = []
    col = "wood" if stage == 0 else "3f7f6a" if stage == 1 else "f2ede4"
    o.append(UB("mueble", x, FY, z, 0.8, 0.55, 0.5, r=0.04, col=col))
    o.append(UB("tapa", x, FY + 0.55, z, 0.86, 0.06, 0.56, r=0.03, col="woodd" if stage == 0 else "c98d55" if stage == 1 else "gold"))
    o.append(UB("caja", x, FY + 0.61, z, 0.44, 0.2, 0.32, r=0.05, col="4aa3a8", metal=0.3, rough=0.4))
    o.append(rbox("visor", U(x, FY + 0.86, z + 0.08), (0.36, 0.04, 0.12), r=0.02, col="3f8a8f", rot=(-25, 0, 0)))
    o.append(UB("tecla", x + 0.12, FY + 0.81, z - 0.06, 0.08, 0.04, 0.08, r=0.02, col="gold"))
    for i in range(4):
        o.append(UC("moneda", x - 0.25, FY + 0.61 + i * 0.025, z - 0.1, 0.06, 0.022, "coin", seg=16, bev=0.006))
    return o


def planter(x, z, flower):
    o = [UC("maceta", x, 0, z, 0.16, 0.24, "d9774a", r2=0.2, seg=16, bev=0.03)]
    o.append(UBL("planta", x, 0.36, z, 0.22, "5cbf63", seed=int(x * 10) + 5, amp=0.18))
    for k in range(4):
        o.append(US("flor", x + math.cos(k * 1.7) * 0.14, 0.45 + (k % 2) * 0.06, z + math.sin(k * 1.7) * 0.12, 0.05, flower))
    return o


# ================================================================ etapa 1: tienda de rarezas (piedra, revoque y toldo)
def stage1():
    o = floor(top="c98d55", alt="b07a48")
    zb = Z1 + 0.1
    o.append(UB("zocalo", 0, 0, zb, 4.9, 0.55, 0.22, r=0.05, col="stone"))
    o.append(UB("pared", 0, 0.55, zb + 0.02, 4.9, 1.75, 0.16, r=0.04, col="wall"))
    for x in (-2.35, -0.8, 0.8, 2.35):
        o.append(UB("viga", x, 0.55, zb - 0.07, 0.14, 1.75, 0.08, r=0.03, col="woodd"))
    o.append(UB("vigah", 0, 2.25, zb - 0.07, 4.95, 0.14, 0.1, r=0.03, col="woodd"))
    o.append(UB("vigam", 0, 1.35, zb - 0.07, 4.95, 0.1, 0.08, r=0.03, col="woodd"))
    # techo a dos aguas de tejas azules
    for sx in (-1, 1):
        o.append(rbox("techo", U(sx * 1.35, 2.62, zb + 0.15), (3.05, 1.0, 0.16), r=0.06, col="RoofBlue" if False else "4a8fe0", rot=(0, -sx * 24, 0)))
        for k in range(5):
            o.append(rbox("teja", U(sx * (0.35 + k * 0.48), 2.45 + (4 - k) * 0.21 - 0.03, zb - 0.38), (0.42, 0.08, 0.06), r=0.03, col="3a72b8", rot=(0, -sx * 24, 0)))
    # toldo a rayas sobre el mostrador
    for k in range(10):
        x = -2.25 + k * 0.5
        o.append(rbox("toldo", U(x, 2.0, zb - 0.45), (0.5, 0.75, 0.05), r=0.02, col="e0594a" if k % 2 == 0 else "fbf6ee", rot=(-28, 0, 0)))
        o.append(US("borla", x, 1.8, zb - 0.83, 0.045, "e0594a" if k % 2 == 0 else "fbf6ee", scale=(1.6, 1, 0.6)))
    # cartel con un frasco dibujado (sin texto)
    o.append(UB("cartel", 0, 2.55, zb - 0.12, 1.1, 0.42, 0.06, r=0.05, col="c98d55"))
    o.append(UC("dibujo", 0, 2.76, zb - 0.16, 0.13, 0.03, "gold", seg=24, rot=(90, 0, 0)))
    # paredes laterales de piedra baja con barandas
    for sx in (-1, 1):
        x = sx * 2.4
        o.append(UB("muro", x, 0.0, 0.2, 0.2, 0.75, 3.7, r=0.06, col="stone"))
        o.append(UB("baranda", x, 0.75, 0.2, 0.26, 0.08, 3.75, r=0.03, col="woodd"))
        for z in (-1.4, -0.4, 0.6, 1.6):
            o.append(US("maceton", x, 0.86, z, 0.11, "5cbf63"))
            o.append(US("flor", x, 0.95, z - 0.05, 0.05, "ff5d8f" if z > 0 else "ffd34d"))
        o.extend(lantern_post(x, Z0 + 0.02, 1.35))
    # alfombra redonda
    o.append(UC("alfombra", 0, FY, 0.0, 1.1, 0.015, "c0392b", seg=40))
    o.append(UC("alfombra2", 0, FY + 0.004, 0.0, 0.85, 0.015, "e0a24a", seg=40))
    o.extend(back_table(1))
    for k in range(9):
        x = -1.7 + k * 0.42
        c = ["7ec8e3", "f2a65a", "b06ef0", "5cbf63"][k % 4]
        if k % 2:
            o.append(UC("frasco", x, FY + 0.52 + 0.4, 1.77, 0.06, 0.16, c, r2=0.05, seg=12, bev=0.02))
        else:
            o.append(US("bola", x, FY + 0.52 + 0.47, 1.77, 0.07, c))
    o.extend(register(1.75, -1.3, 1))
    o.extend(planter(-1.95, -1.3, "b06ef0"))
    for s_ in (-1, 1):
        o.extend(planter(s_ * 1.05, Z0 - 0.35, "ff5d8f" if s_ < 0 else "ffd34d"))
    return o


# ================================================================ etapa 2: galeria (marmol, columnas, oro y banderines)
def stage2():
    o = floor(top="f2ede4", alt="c9c2b6", base="dcd4c6", tiles=True)
    zb = Z1 + 0.1
    o.append(UB("pared", 0, 0, zb + 0.02, 4.95, 2.4, 0.2, r=0.05, col="e8e0d0"))
    o.append(UB("friso", 0, 2.3, zb - 0.06, 5.05, 0.18, 0.12, r=0.04, col="gold", metal=0.6, rough=0.3))
    o.append(UB("zocalo", 0, 0, zb - 0.08, 5.0, 0.22, 0.08, r=0.03, col="7a2a4a"))
    # fronton
    for sx in (-1, 1):
        o.append(rbox("fronton", U(sx * 1.25, 2.85, zb - 0.02), (2.75, 0.2, 0.2), r=0.06, col="f2ede4", rot=(0, -sx * 20, 0)))
    o.append(UC("medallon", 0, 2.85, zb - 0.1, 0.24, 0.05, "gold", seg=32, rot=(90, 0, 0), bev=0.02, metal=0.6, rough=0.3))
    # columnas con capitel
    for x in (-2.35, -0.9, 0.9, 2.35):
        o.append(UB("basa", x, FY, zb - 0.25, 0.36, 0.1, 0.36, r=0.03, col="dcd4c6"))
        o.append(UC("fuste", x, FY + 0.1, zb - 0.25, 0.13, 1.95, "f2ede4", seg=20, bev=0.02))
        o.append(UB("capitel", x, FY + 2.05, zb - 0.25, 0.4, 0.12, 0.4, r=0.04, col="gold", metal=0.6, rough=0.3))
    # banderines de colores
    pts = []
    for k in range(13):
        x = -2.3 + k * 0.38
        pts.append(U(x, 2.0 - 0.18 * math.sin(k / 12 * math.pi), zb - 0.3))
    o.append(tube("cuerda", pts, 0.008, "woodd"))
    for k in range(12):
        x = -2.11 + k * 0.38
        y = 2.0 - 0.18 * math.sin((k + 0.5) / 12 * math.pi)
        o.append(cyl("banderin", U(x, y - 0.03, zb - 0.3), 0.09, 0.16, ["e0594a", "ffd23a", "4a8fe0", "5cbf63", "b06ef0"][k % 5], r2=0.0, seg=3, rot=(180, 0, 0)))
    # vallas de terciopelo a los costados
    for sx in (-1, 1):
        x = sx * 2.35
        for z in (-1.5, -0.25, 1.0):
            o.append(UC("poste", x, FY, z, 0.04, 0.6, "gold", seg=12, metal=0.6, rough=0.3))
            o.append(US("remate", x, FY + 0.62, z, 0.06, "gold", metal=0.6, rough=0.3))
        for z0, z1 in ((-1.5, -0.25), (-0.25, 1.0)):
            pts = [U(x, FY + 0.5 - 0.12 * math.sin(t / 8 * math.pi), z0 + (z1 - z0) * t / 8) for t in range(9)]
            o.append(tube("soga", pts, 0.025, "a8203a"))
        o.append(UB("muro", x, 0, 0.2, 0.16, 0.24, 3.7, r=0.04, col="dcd4c6"))
        o.extend(lantern_post(x, Z0 + 0.02, 1.7))
    o.append(UC("alfombra", 0, FY, 0.2, 1.2, 0.015, "7a2a4a", seg=40))
    o.append(UC("alfombra2", 0, FY + 0.004, 0.2, 1.0, 0.015, "a8203a", seg=40))
    o.extend(back_table(2))
    o.extend(register(1.75, -1.3, 2))
    o.append(UC("planta", -1.95, FY, -1.3, 0.18, 0.3, "f2ede4", seg=16, bev=0.03))
    o.append(UBL("palmera", -1.95, FY + 0.65, -1.3, 0.32, "4f9e3f", seed=7, amp=0.25))
    for s_ in (-1, 1):
        o.extend(planter(s_ * 1.05, Z0 - 0.35, "ffd34d"))
    return o


# ================================================================ cajas de la carreta (tapa aparte para abrirla)
def crate(tier):
    body, lid = [], []
    if tier == 0:
        c, cd, band = "c98d55", "a8703f", "7a4a2a"
    elif tier == 1:
        c, cd, band = "b8c4d0", "8a96a4", "4aa3f0"
    else:
        c, cd, band = "ffd23a", "e0a22a", "c0392b"
    kw = dict(metal=0.5, rough=0.35) if tier > 0 else {}
    body.append(rbox("caja", (0, 0, 0.2), (0.62, 0.48, 0.4), r=0.05, col=c, **kw))
    for z in (0.08, 0.32):
        body.append(rbox("tabla", (0, 0, z), (0.64, 0.5, 0.05), r=0.02, col=cd, **kw))
    for x in (-0.25, 0.25):
        body.append(rbox("fleje", (x, 0, 0.2), (0.06, 0.5, 0.41), r=0.02, col=band, **kw))
    body.append(rbox("cerradura", (0, -0.25, 0.33), (0.1, 0.03, 0.1), r=0.02, col="gold" if tier < 2 else "fbf6ee", metal=0.6, rough=0.3))
    lid.append(rbox("tapa", (0, 0, 0.47), (0.66, 0.52, 0.12), r=0.05, col=cd, **kw))
    for x in (-0.25, 0.25):
        lid.append(rbox("fleje_t", (x, 0, 0.47), (0.065, 0.53, 0.13), r=0.02, col=band, **kw))
    if tier == 2:
        lid.append(sphere("gema", (0, 0, 0.55), 0.06, "b06ef0", rough=0.1, emis=0.4))
    return body, lid


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only, prev = None, None
    for i, a in enumerate(argv):
        if a == "--only":
            only = argv[i + 1].split(",")
        if a == "--preview":
            prev = argv[i + 1]
    jobs = only or ["terrain", "stage0", "stage1", "stage2", "crates"]
    for j in jobs:
        lib.reset()
        if j == "terrain":
            objs = terrain()
            o, t = lib.finish(objs, "terrain", OUT, ao=0.45, ao_dist=0.5)
            shown = [o]
        elif j.startswith("stage"):
            st = int(j[-1])
            objs = {"stage0": stage0, "stage1": stage1, "stage2": stage2}[j]()
            groups = [("shell", objs, (0, 0, 0))]
            for i in range(4):
                bx, bz = SIDE[i]
                groups.append(("ped%d" % i, pedestal(st, i), U(bx, FY, bz)))
            shown, t = lib.finish_parts(groups, j, OUT, ao=0.55, ao_dist=0.35)
        else:
            shown = []
            for tier in range(3):
                lib.reset()
                b, l = crate(tier)
                parts, t = lib.finish_parts([("body", b, (0, 0, 0)), ("lid", l, (0, 0.24, 0.41))], "crate%d" % tier, OUT, ao=0.5, ao_dist=0.15)
                if prev:
                    lib.preview(parts, os.path.join(prev, "crate%d.png" % tier), size=360)
            continue
        print("WORLD", j, t)
        if prev:
            os.makedirs(prev, exist_ok=True)
            lib.preview(shown, os.path.join(prev, j + ".png"), size=720, elev=42, azim=192)


if __name__ == "__main__":
    main()
