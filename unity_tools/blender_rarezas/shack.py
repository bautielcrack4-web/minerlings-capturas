"""El local en su etapa 1: el RANCHO (DIRECCION_CREATIVA 2), los exhibidores de la etapa 1, el cartel OPEN/CLOSED y la
gallina. Medidas en coordenadas de Unity con U() (x derecha, y arriba, z hacia el fondo = norte). El frente del rancho
mira a -Z (hacia la camara) y la pared de adelante es baja (corte de maqueta) para ver adentro.

Interior: grilla de 6x5 celdas de 0.5 m -> x -1.5..1.5, z -1.25..1.25, piso a y = FY. La puerta esta adelante a la
derecha (x 0.55..1.25). Unity: Rarezas.View.ShackArt usa estas mismas medidas.

Piezas de "stage0": shell (piso, paredes, mostrador, lamparita), roof (techo: se esconde cuando entras), sign (cartel
RARESAS sobre la puerta), open (cartel OPEN/CLOSED que gira; pivote en el clavo).

Uso: blender -b -P shack.py -- [--only stage0,displays,chicken] [--preview carpeta]
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, torus, tube, blob

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Fonts/LilitaOne-Regular.ttf")
FY = 0.08
X0, X1, Z0, Z1 = -1.5, 1.5, -1.25, 1.25
DOOR = (0.55, 1.25)


def U(x, y, z):
    return (-x, -z, y)


def UB(name, x, y, z, w, h, d, rx=0, ry=0, rz=0, **kw):
    """Caja con centro de BASE en Unity (x, y, z); giros en grados sobre los ejes de Unity (como Quaternion.Euler:
    rx > 0 baja el borde +Z, rz > 0 levanta el borde +X, ry > 0 gira el frente hacia +X)."""
    return rbox(name, U(x, y + h * 0.5, z), (w, d, h), rot=(rx, rz, -ry), **kw)


def UC(name, x, y, z, r, h, col, rx=0, ry=0, rz=0, **kw):
    return cyl(name, U(x, y, z), r, h, col, rot=(rx, rz, -ry), **kw)


def US(name, x, y, z, r, col, **kw):
    return sphere(name, U(x, y, z), r, col, **kw)


def text(name, s, x, y, z, size, col, depth=0.02, ry=0, rx=0, **kw):
    """Palabra en 3D (fuente del juego), parada, mirando a -Z de Unity, centrada en (x, y, z)."""
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = s
    try:
        cu.font = bpy.data.fonts.load(FONT)
    except Exception:
        pass
    cu.size = size
    cu.extrude = depth
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    cu.resolution_u = 4
    o = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    lib.setmat(o, col, **kw)
    # de pie (X 90) y de frente a -Z de Unity (= +Y de Blender... el frente de Blender es -Y)
    o.rotation_euler = (math.radians(90 + rx), 0, math.radians(180 + ry))
    o.location = U(x, y, z)
    return o


# ================================================================ rancho
def planks_wall(o, rnd, x0, x1, z, h0, h1, axis="x", col=("a8703f", "8a5a36", "c98d55"), gable=0.0):
    """Pared de tablas verticales torcidas (cada una con su giro y su altura). gable = cuanto sube al centro (x=0)."""
    n = max(2, int(abs(x1 - x0) / 0.24))
    w = (x1 - x0) / n
    for i in range(n):
        c = x0 + w * (i + 0.5)
        h = h0 + (h1 - h0) * (i + 0.5) / n + rnd.uniform(-0.06, 0.06) + gable * max(0.0, 1.0 - abs(c) / 1.55)
        tilt = rnd.uniform(-3.5, 3.5)
        cc = col[rnd.randrange(len(col))]
        if axis == "x":
            o.append(UB("tabla", c, 0, z, abs(w) - 0.025, h, 0.07, rz=tilt, r=0.02, seg=2, col=cc))
        else:
            o.append(UB("tabla", z, 0, c, 0.07, h, abs(w) - 0.025, rx=tilt, r=0.02, seg=2, col=cc))


def stage0():
    rnd = random.Random(5)
    shell, roof, sign, opn = [], [], [], []
    # piso de tierra apisonada con tablones sueltos
    shell.append(UB("piso", 0, -0.05, 0, 3.3, FY + 0.05, 2.8, r=0.06, col="c9a06a"))
    for i in range(7):
        x, z = rnd.uniform(-1.2, 1.2), rnd.uniform(-1.0, 1.0)
        shell.append(UC("mancha", x, FY - 0.004, z, rnd.uniform(0.15, 0.35), 0.01, "b88d58", seg=14))
    # postes de esquina (torcidos)
    for (x, z) in ((X0 - 0.06, Z1 + 0.06), (X1 + 0.06, Z1 + 0.06), (X0 - 0.06, Z0 - 0.06), (X1 + 0.06, Z0 - 0.06)):
        hh = 1.18 if z > 0 else 1.15
        shell.append(UB("poste", x, 0, z, 0.13, hh, 0.13, rz=rnd.uniform(-3, 3), r=0.04, col="6a4026"))
    # pared del fondo (alta), costados (bajan hacia adelante) y adelante baja con la puerta
    planks_wall(shell, rnd, X0, X1, Z1 + 0.06, 1.2, 1.2, gable=0.42)
    planks_wall(shell, rnd, Z0, Z1, X0 - 0.06, 0.55, 1.05, axis="z")
    planks_wall(shell, rnd, Z0, Z1, X1 + 0.06, 0.55, 1.05, axis="z")
    planks_wall(shell, rnd, X0, DOOR[0] - 0.05, Z0 - 0.06, 0.42, 0.42)
    planks_wall(shell, rnd, DOOR[1] + 0.05, X1, Z0 - 0.06, 0.42, 0.42)
    # marco de la puerta y dintel
    for x in DOOR:
        shell.append(UB("marco", x, 0, Z0 - 0.06, 0.1, 1.3, 0.1, r=0.03, col="6a4026"))
    shell.append(UB("dintel", (DOOR[0] + DOOR[1]) / 2, 1.25, Z0 - 0.06, DOOR[1] - DOOR[0] + 0.2, 0.09, 0.11, r=0.03, col="6a4026"))
    # parches y travesaños
    shell.append(UB("traviesa", 0, 0.95, Z1 + 0.11, 3.2, 0.08, 0.05, r=0.02, col="7a4a2a"))
    shell.append(UB("parche", -0.7, 0.6, Z1 + 0.01, 0.35, 0.28, 0.03, rz=8, r=0.02, col="c9773f"))
    for k in range(4):
        shell.append(US("clavo", -0.82 + (k % 2) * 0.24, 0.5 + (k // 2) * 0.2, Z1 + 0.0, 0.014, "9aa1aa"))
    # mostrador: un cajon dado vuelta con una tabla
    shell.append(UB("cajon", -0.95, FY, -0.75, 0.6, 0.42, 0.42, r=0.03, col="c98d55"))
    for y in (0.12, 0.3):
        shell.append(UB("listón", -0.95, FY + y, -0.965, 0.62, 0.06, 0.02, r=0.01, col="a8703f"))
    shell.append(UB("tapa", -0.95, FY + 0.42, -0.75, 0.75, 0.05, 0.5, r=0.02, col="8a5a36"))
    shell.append(UB("lata", -1.1, FY + 0.47, -0.75, 0.12, 0.12, 0.12, r=0.03, col="9aa1aa", metal=0.5, rough=0.4))
    shell.append(UC("monedas", -0.8, FY + 0.47, -0.72, 0.05, 0.03, "coin"))
    # lamparita colgando
    shell.append(tube("cable", [U(0.0, 1.62, 0.1), U(0.0, 1.25, 0.1)], 0.008, "2b2440"))
    shell.append(UC("portalampara", 0, 1.17, 0.1, 0.03, 0.08, "2b2440"))
    shell.append(US("foco", 0, 1.12, 0.1, 0.065, "fff1b0", emis=2.0))
    # cositas: balde, escoba, bolsa
    shell.append(UC("balde", 1.3, FY, 1.0, 0.13, 0.22, "9aa1aa", r2=0.15, seg=18, metal=0.4, rough=0.5))
    shell.append(tube("escoba", [U(-1.38, FY, 1.05), U(-1.3, 1.1, 1.12)], 0.016, "c98d55"))
    shell.append(blob("paja", U(-1.38, FY + 0.06, 1.03), 0.1, "f0cf6a", amp=0.3, scale=(1, 1, 1.2)))

    # techo de chapa a dos aguas con parches de colores (pieza aparte)
    for sx in (-1, 1):
        for k in range(6):
            z = Z0 - 0.18 + k * ((Z1 - Z0 + 0.45) / 6)
            col = ["9aa8b4", "8a98a4", "b0bcc6"][k % 3]
            roof.append(UB("chapa", sx * 0.85, 1.72 - 0.3 + 0.05, z + 0.25, 1.95, 0.045, 0.52, rz=-sx * 17 + rnd.uniform(-2, 2), r=0.012, seg=1,
                           col=col, metal=0.45, rough=0.5))
        roof.append(UB("parche", sx * 0.9, 1.72 - 0.25, rnd.uniform(-0.6, 0.6), 0.45, 0.06, 0.4, rz=-sx * 17, r=0.012, seg=1, col="c9773f" if sx < 0 else "e0594a", metal=0.3, rough=0.6))
    roof.append(UB("cumbrera", 0, 1.7, 0.05, 0.16, 0.08, Z1 - Z0 + 0.5, r=0.03, col="7a8894", metal=0.4, rough=0.5))
    roof.append(blob("piedra", U(0.6, 1.62, 0.5), 0.09, "9aa1aa", amp=0.3))

    # cartel RARESAS (con falta de ortografia, pintado a mano) clavado en dos palos, adelante a la izquierda
    cx, cz = -0.75, Z0 - 0.62
    for s_ in (-1, 1):
        sign.append(UB("palo", cx + s_ * 0.52, 0, cz + 0.03, 0.07, 1.05, 0.07, rz=s_ * 2, r=0.02, col="6a4026"))
    sign.append(UB("tabla", cx, 0.62, cz, 1.38, 0.34, 0.05, rz=-3, r=0.04, col="e8d6ae"))
    sign.append(text("letras", "RARESAS", cx, 0.79, cz - 0.04, 0.25, "c0392b", depth=0.012))
    sign.append(UB("clavo", cx - 0.52, 0.74, cz - 0.03, 0.04, 0.04, 0.02, r=0.01, col="9aa1aa"))
    sign.append(UB("clavo", cx + 0.52, 0.71, cz - 0.03, 0.04, 0.04, 0.02, r=0.01, col="9aa1aa"))
    # cartel OPEN/CLOSED colgado del marco (pivote en el clavo, gira sobre Y)
    px, py, pz = DOOR[1] + 0.2, 1.0, Z0 - 0.14
    opn.append(UB("tabla", px, py - 0.34, pz, 0.46, 0.24, 0.04, r=0.03, col="fbf6ee"))
    opn.append(UB("borde_v", px, py - 0.34, pz - 0.005, 0.42, 0.2, 0.04, r=0.03, col="5cbf63"))
    opn.append(text("open", "OPEN", px, py - 0.22, pz - 0.035, 0.13, "fbf6ee", depth=0.008))
    opn.append(UB("borde_r", px, py - 0.34, pz + 0.005, 0.42, 0.2, 0.04, r=0.03, col="e0594a"))
    opn.append(text("closed", "CLOSED", px, py - 0.22, pz + 0.035, 0.1, "fbf6ee", depth=0.008, ry=180))
    for s_ in (-1, 1):
        opn.append(tube("hilo", [U(px + s_ * 0.17, py - 0.1, pz), U(px, py, pz)], 0.006, "6a4026"))
    opn.append(US("clavo", px, py, pz, 0.018, "9aa1aa"))
    return [("shell", shell, (0, 0, 0)), ("roof", roof, (0, 0, 0)), ("sign", sign, (0, 0, 0)), ("open", opn, U(px, py, pz))]


# ================================================================ exhibidores (cada uno con su "tope": donde va el objeto)
def apple_crate():
    """Cajon de manzanas parado (chico, +0%). Ocupa 2x2 celdas; tope a y=0.46."""
    o = []
    o.append(UB("cajon", 0, 0, 0, 0.62, 0.44, 0.5, r=0.03, col="c98d55"))
    for y in (0.08, 0.22, 0.36):
        o.append(UB("liston", 0, y, -0.255, 0.64, 0.07, 0.02, r=0.01, col="a8703f"))
    o.append(UB("rotulo", 0, 0.2, -0.27, 0.26, 0.12, 0.01, r=0.01, col="fff3d6"))
    o.append(US("manzana_logo", 0, 0.2, -0.28, 0.035, "e0594a", scale=(1, 0.4, 1)))
    o.append(UB("tapa", 0, 0.42, 0, 0.66, 0.04, 0.54, r=0.015, col="a8703f"))
    for k in range(3):
        o.append(US("manzana", 0.32 - k * 0.07, 0.05, -0.3 - (k % 2) * 0.06, 0.045, "e0594a"))
    return o


def plank_shelf():
    """Estante de tablas sobre ladrillos (chico y mediano, +5%). Ocupa 2x1 celdas; tope a y=0.56."""
    o = []
    for x in (-0.38, 0.38):
        for y in (0.0, 0.13):
            o.append(UB("ladrillo", x, y, 0, 0.2, 0.12, 0.3, r=0.02, col="c4683a"))
        for y in (0.3, 0.43):
            o.append(UB("ladrillo", x, y, 0, 0.2, 0.12, 0.3, r=0.02, col="b85a32"))
    o.append(UB("tabla1", 0, 0.255, 0, 0.98, 0.045, 0.36, rz=1.5, r=0.015, col="c98d55"))
    o.append(UB("tabla2", 0, 0.555, 0, 1.0, 0.05, 0.38, rz=-1, r=0.015, col="a8703f"))
    for k in range(3):
        o.append(UC("frasco", -0.2 + k * 0.17, 0.3, 0.05, 0.04, 0.09, "8fd8c8", rough=0.15))
    return o


def chicken():
    body = [US("cuerpo", 0, 0.22, 0, 0.13, "fbf6ee", scale=(0.8, 0.85, 1.1))]
    body.append(US("cola", 0, 0.3, -0.13, 0.07, "f3ead8", scale=(0.6, 1.1, 0.8)))
    for s_ in (-1, 1):
        body.append(US("ala", s_ * 0.1, 0.22, -0.01, 0.07, "f3ead8", scale=(0.4, 0.8, 1.2)))
    head = [US("cabeza", 0, 0.36, 0.1, 0.075, "fbf6ee")]
    head.append(US("cresta", 0, 0.44, 0.1, 0.035, "e0594a", scale=(0.5, 1, 1.4)))
    head.append(UC("pico", 0, 0.36, 0.17, 0.022, 0.05, "f59a32", r2=0.0, rx=90, seg=10))
    head.append(US("barbilla", 0, 0.32, 0.15, 0.022, "e0594a"))
    for s_ in (-1, 1):
        head.append(US("ojo", s_ * 0.05, 0.38, 0.15, 0.013, "2b2440"))
    legs = []
    for s_ in (-1, 1):
        legs.append(UC("pata", s_ * 0.04, 0.0, 0, 0.012, 0.13, "f59a32"))
        legs.append(UB("pie", s_ * 0.04, 0.0, 0.03, 0.05, 0.015, 0.07, r=0.006, col="f59a32"))
    return [("body", body, (0, 0, 0)), ("head", head, U(0, 0.3, 0.06)), ("legs", legs, (0, 0, 0))]


# ================================================================ galpon (etapa 2): 9x7 celdas -> 4.5 x 3.5 m
def stage1():
    """Galpon de madera pintado de rojo con puerta corrediza grande, piso de tablas, estantes de cajones apilados,
    faroles, cartel de madera tallada y el rincon del taller (banco de trabajo) atras a la izquierda."""
    rnd = random.Random(7)
    hx, hz = 2.25, 1.75
    door = (hx - 0.95, hx - 0.25)
    shell, roof, sign, opn = [], [], [], []
    # piso de tablas
    shell.append(UB("base", 0, -0.06, 0, hx * 2 + 0.4, FY + 0.06, hz * 2 + 0.4, r=0.06, col="b8aa98"))
    n = 15
    for i in range(n):
        z = -hz + (i + 0.5) * (hz * 2 / n)
        shell.append(UB("tabla_piso", rnd.uniform(-0.03, 0.03), FY - 0.03, z, hx * 2, 0.03, hz * 2 / n - 0.012, r=0.008, seg=1,
                        col=["c98d55", "b87a45", "d39a62"][i % 3]))
    red, redd, trim = "c84a3c", "a83c30", "fbf1dc"
    # pared del fondo (con hastial) y costados, tablas verticales pintadas de rojo
    planks_wall(shell, rnd, -hx, hx, hz + 0.06, 1.4, 1.4, col=(red, redd, red))
    planks_wall(shell, rnd, -hz, hz, -hx - 0.06, 0.6, 1.4, axis="z", col=(red, redd, red))
    planks_wall(shell, rnd, -hz, hz, hx + 0.06, 0.6, 1.4, axis="z", col=(red, redd, red))
    planks_wall(shell, rnd, -hx, door[0] - 0.05, -hz - 0.06, 0.45, 0.45, col=(red, redd, red))
    planks_wall(shell, rnd, door[1] + 0.05, hx, -hz - 0.06, 0.45, 0.45, col=(red, redd, red))
    # esquinas y vigas blancas
    for (x, z) in ((-hx - 0.07, hz + 0.07), (hx + 0.07, hz + 0.07), (-hx - 0.07, -hz - 0.07), (hx + 0.07, -hz - 0.07)):
        shell.append(UB("esquina", x, 0, z, 0.16, 1.45 if z > 0 else 1.2, 0.16, r=0.04, col=trim))
    shell.append(UB("viga", 0, 1.5, hz + 0.12, hx * 2 + 0.3, 0.1, 0.06, r=0.03, col=trim))
    # X del granero en la pared del fondo
    for s_ in (-1, 1):
        shell.append(UB("cruz", -1.2, 0.75, hz + 0.0, 1.2, 0.09, 0.04, rz=s_ * 38, r=0.02, col=trim))
    shell.append(UB("marco_x", -1.2, 0.2, hz + 0.0, 1.0, 1.1, 0.03, r=0.02, col=redd))
    # puerta corrediza abierta (corrida a la derecha, por afuera) con su riel
    for x in door:
        shell.append(UB("marco", x, 0, -hz - 0.06, 0.12, 1.35, 0.12, r=0.03, col=trim))
    shell.append(UB("riel", hx - 0.3, 1.36, -hz - 0.16, 1.9, 0.06, 0.06, r=0.02, col="6a6a74", metal=0.5, rough=0.4))
    shell.append(UB("puerta", hx + 0.35, 0.05, -hz - 0.2, 0.75, 1.28, 0.06, r=0.03, col=red))
    for s_ in (-1, 1):
        shell.append(UB("cruz_p", hx + 0.35, 0.68, -hz - 0.24, 0.85, 0.07, 0.03, rz=s_ * 55, r=0.02, col=trim))
    # estantes de cajones apilados contra el fondo (decoracion)
    for i in range(3):
        for j in range(2 - (i % 2)):
            shell.append(UB("cajon", 0.6 + i * 0.5, FY + j * 0.36, hz - 0.25, 0.46, 0.34, 0.36, r=0.03, col=["c98d55", "a8703f"][(i + j) % 2]))
    # taller: banco de trabajo con herramientas (atras a la izquierda)
    shell.append(UB("banco", -1.65, FY + 0.48, hz - 0.45, 1.0, 0.08, 0.6, r=0.02, col="8a5a36"))
    for (x, z) in ((-2.08, hz - 0.2), (-1.22, hz - 0.2), (-2.08, hz - 0.7), (-1.22, hz - 0.7)):
        shell.append(UB("pata", x, FY, z, 0.08, 0.48, 0.08, r=0.02, col="6a4026"))
    shell.append(UB("tabla_herr", -1.65, 0.85, hz - 0.02, 1.0, 0.6, 0.04, r=0.02, col="c98d55"))
    for k in range(4):
        shell.append(tube("herramienta", [U(-2.0 + k * 0.22, 1.05, hz - 0.05), U(-2.0 + k * 0.22, 0.7, hz - 0.05)], 0.018, ["9aa1aa", "a8703f", "e0594a", "6d747e"][k]))
    shell.append(US("tornillo", -1.4, FY + 0.6, hz - 0.45, 0.06, "6d747e", metal=0.5, rough=0.4))
    shell.append(UC("lata_pintura", -1.95, FY + 0.52, hz - 0.5, 0.07, 0.12, "4a8fe0"))
    shell.append(UC("pincel", -1.8, FY + 0.6, hz - 0.5, 0.012, 0.16, "c98d55", rz=40))
    # faroles colgados
    for x in (-0.8, 1.0):
        shell.append(tube("cadena", [U(x, 1.75, 0.2), U(x, 1.35, 0.2)], 0.008, "2b2440"))
        shell.append(UC("farol", x, 1.15, 0.2, 0.07, 0.2, "ffe2a0", seg=8, emis=1.2))
        shell.append(UC("techo_farol", x, 1.35, 0.2, 0.1, 0.06, "2b3040", r2=0.03, seg=8))
    # paja en el piso
    for i in range(12):
        x, z = rnd.uniform(-hx + 0.2, hx - 0.2), rnd.uniform(-hz + 0.2, hz - 0.4)
        shell.append(tube("paja", [U(x, FY + 0.005, z), U(x + rnd.uniform(-0.15, 0.15), FY + 0.005, z + rnd.uniform(-0.1, 0.1))], 0.012, "hayd"))

    # techo a dos aguas (cumbrera a lo largo de X), tejas de madera rojas oscuras
    pitch = 24.0
    run = hz + 0.35
    import math as _m
    rise = run * _m.tan(_m.radians(pitch))
    top = 1.42
    for s_ in (-1, 1):
        for k in range(5):
            zc = s_ * (k + 0.5) * run / 5
            y = top + rise - (k + 0.5) * rise / 5
            roof.append(UB("faldon", 0, y - 0.04, zc, hx * 2 + 0.6, 0.07, run / 5 + 0.06, rx=s_ * pitch, r=0.02, seg=1,
                           col=["8a3a30", "7a3028", "943f33"][k % 3]))
    roof.append(UB("cumbrera", 0, top + rise - 0.02, 0, hx * 2 + 0.7, 0.1, 0.16, r=0.04, col=trim))
    roof.append(UB("veleta_poste", hx - 0.4, top + rise, 0, 0.04, 0.4, 0.04, r=0.01, col="2b2440"))
    roof.append(UB("veleta", hx - 0.4, top + rise + 0.38, 0, 0.32, 0.12, 0.02, r=0.01, col="2b2440"))

    # cartel de madera tallada (bien escrito ahora) colgado de un brazo, adelante a la izquierda
    cx, cz = -1.0, -hz - 0.62
    sign.append(UB("poste", cx - 0.82, 0, cz + 0.05, 0.1, 1.55, 0.1, r=0.03, col="6a4026"))
    sign.append(UB("brazo", cx - 0.4, 1.45, cz + 0.05, 0.9, 0.08, 0.08, r=0.02, col="6a4026"))
    for x in (cx - 0.65, cx - 0.15):
        sign.append(tube("cadena", [U(x, 1.43, cz + 0.05), U(x, 1.22, cz + 0.05)], 0.008, "9aa1aa"))
    sign.append(UB("tabla", cx - 0.4, 0.86, cz, 1.25, 0.38, 0.07, r=0.06, col="a8703f"))
    sign.append(UB("borde", cx - 0.4, 0.84, cz - 0.01, 1.32, 0.42, 0.05, r=0.07, col="7a4a2a"))
    sign.append(text("letras", "RAREZAS", cx - 0.4, 1.05, cz - 0.05, 0.25, "ffd23a", depth=0.015))

    # cartel OPEN/CLOSED en el marco de la puerta
    px, py, pz = door[0] - 0.22, 1.0, -hz - 0.16
    opn.append(UB("tabla", px, py - 0.34, pz, 0.46, 0.24, 0.04, r=0.03, col="fbf6ee"))
    opn.append(UB("borde_v", px, py - 0.34, pz - 0.005, 0.42, 0.2, 0.04, r=0.03, col="5cbf63"))
    opn.append(text("open", "OPEN", px, py - 0.22, pz - 0.035, 0.13, "fbf6ee", depth=0.008))
    opn.append(UB("borde_r", px, py - 0.34, pz + 0.005, 0.42, 0.2, 0.04, r=0.03, col="e0594a"))
    opn.append(text("closed", "CLOSED", px, py - 0.22, pz + 0.035, 0.1, "fbf6ee", depth=0.008, ry=180))
    for s_ in (-1, 1):
        opn.append(tube("hilo", [U(px + s_ * 0.17, py - 0.1, pz), U(px, py, pz)], 0.006, "6a4026"))
    opn.append(US("clavo", px, py, pz, 0.018, "9aa1aa"))
    return [("shell", shell, (0, 0, 0)), ("roof", roof, (0, 0, 0)), ("sign", sign, (0, 0, 0)), ("open", opn, U(px, py, pz))]


def table():
    """Mesa con mantel (mediano, +8%). 2x2 celdas; tope a y=0.6."""
    o = []
    for (x, z) in ((-0.36, -0.36), (0.36, -0.36), (-0.36, 0.36), (0.36, 0.36)):
        o.append(UB("pata", x, 0, z, 0.07, 0.56, 0.07, r=0.02, col="7a4a2a"))
    o.append(UB("tapa", 0, 0.54, 0, 0.92, 0.05, 0.92, r=0.02, col="a8703f"))
    o.append(UB("mantel", 0, 0.555, 0, 0.88, 0.045, 0.88, r=0.03, col="fbf1dc", plaid=("e0594a", "fff6e4", 0.1)))
    for s_ in (-1, 1):
        o.append(UB("caida", 0, 0.38, s_ * 0.45, 0.88, 0.2, 0.02, r=0.01, col="fbf1dc", plaid=("e0594a", "fff6e4", 0.1)))
    return o


def platform():
    """Tarima de madera (grande, +10%). 3x3 celdas; tope a y=0.2."""
    o = [UB("base", 0, 0, 0, 1.4, 0.12, 1.4, r=0.03, col="7a4a2a")]
    for i in range(7):
        o.append(UB("tabla", -0.6 + i * 0.2, 0.12, 0, 0.19, 0.06, 1.42, r=0.015, seg=1, col=["c98d55", "b87a45"][i % 2]))
    for (x, z) in ((-0.68, -0.68), (0.68, -0.68), (-0.68, 0.68), (0.68, 0.68)):
        o.append(US("clavo", x, 0.19, z, 0.025, "9aa1aa"))
    return o


def workbench_icon():
    return []


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else ["stage0", "stage1", "displays", "chicken"]
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if prev:
        os.makedirs(prev, exist_ok=True)
    if "stage0" in only:
        lib.reset()
        parts, t = lib.finish_parts(stage0(), "shack0", OUT, ao=0.55, ao_dist=0.35)
        print("SHACK shack0", t)
        if prev:
            lib.preview(parts, os.path.join(prev, "shack0.png"), size=720, elev=36, azim=196)
    if "stage1" in only:
        lib.reset()
        parts, t = lib.finish_parts(stage1(), "shack1", OUT, ao=0.55, ao_dist=0.35)
        print("SHACK shack1", t)
        if prev:
            lib.preview(parts, os.path.join(prev, "shack1.png"), size=720, elev=36, azim=196)
    if "displays" in only:
        for name, fn in (("disp_apple_crate", apple_crate), ("disp_plank_shelf", plank_shelf), ("disp_table", table), ("disp_platform", platform)):
            lib.reset()
            o, t = lib.finish(fn(), name, OUT, ao=0.5, ao_dist=0.2)
            print("SHACK", name, t)
            if prev:
                lib.preview([o], os.path.join(prev, name + ".png"), size=360, elev=25, azim=192)
    if "chicken" in only:
        lib.reset()
        parts, t = lib.finish_parts(chicken(), "chicken", OUT, ao=0.4, ao_dist=0.1, budget=2500)
        print("SHACK chicken", t)
        if prev:
            lib.preview(parts, os.path.join(prev, "chicken.png"), size=300, elev=20, azim=150)


if __name__ == "__main__":
    main()
