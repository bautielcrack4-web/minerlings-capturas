"""Fortin de Juguete: utileria (objetos del mueble, peanas, estrellitas, proyectiles, velador y placard).

Uso: blender -b -P utileria.py            (exporta todo a Resources/Models/u_*.json)
Los objetos gigantes del borde (taza, lapices, regla, goma, libros, tijera, lampara) se modelan a escala real del
juguete: el tablero mide ~7 x 6 unidades y un juguete ~0.8, asi que una taza mide ~3.2 de alto.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import ball, rbox, tube, capsule, torus, cone, star, prism, lathe, on, ring, boolean
from mathutils import Vector

RAREZA = {"comun": "gris_osc_b", "raro": "azul_b", "epico": "violeta_b", "legendario": "oro_m"}


# ------------------------------------------------------------------ peana y estrellas (bajo cada juguete)
def peana(rareza):
    col = RAREZA[rareza]
    # baja y chica: del color de la rareza con un filete claro (el juguete es lo que tiene que dominar)
    lathe((0, 0, 0), [(0.0, 0.0), (0.25, 0.0), (0.27, 0.014), (0.27, 0.03), (0.255, 0.044), (0.225, 0.048), (0.0, 0.048)], col, seg=28, angle=45)
    torus((0, 0, 0.047), 0.215, 0.007, "crema", seg=28, mseg=4)


def flechita():
    """Flechita dorada que apunta hacia abajo sobre los juguetes con los que se puede fusionar."""
    cone((0, 0, 0.085), 0.0, 0.16, 0.17, "oro_m", seg=14)
    tube((0, 0, 0.16), (0, 0, 0.3), 0.065, 0.065, "oro_m", seg=12)


def estrellita():
    star((0, 0, 0), 0.05, 0.024, 0.02, "brillo_oro", bevel=0.004)


# ------------------------------------------------------------------ velador de cristal y placard
def velador():
    """Velador de Cristal: esfera de luz tibia sobre base de madera torneada."""
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.42, 0.0), (0.44, 0.05), (0.4, 0.12), (0.3, 0.16), (0.24, 0.24), (0.26, 0.3),
                          (0.2, 0.34), (0.0, 0.34)], "madera", seg=28, angle=40)
        torus((0, 0, 0.12), 0.39, 0.02, "dorado", seg=28, mseg=5)
        torus((0, 0, 0.33), 0.2, 0.03, "oro_m", seg=24, mseg=6)
    with on("luz"):
        ball((0, 0, 0.7), 0.38, "cristal_vidrio", seg=28, ring=18)
        ball((0, 0, 0.7), 0.2, "luz_velador", seg=16, ring=10)
        for i in range(3):
            a = 2 * math.pi * i / 3
            torus((0, 0, 0.7), 0.385, 0.012, "oro_m", rot=(90, 0, math.degrees(a)), seg=28, mseg=4)
        cone((0, 0, 1.1), 0.05, 0.0, 0.08, "oro_m", seg=8)


def placard():
    """Placard de juguete: ropero de carton pintado con la puerta entreabierta y luz violeta adentro."""
    with on("raiz"):
        rbox((0, 0, 0.9), (1.3, 0.8, 1.8), "lavanda_m", r=0.06)
        rbox((0, 0, 1.86), (1.42, 0.9, 0.14), "violeta_osc", r=0.3)
        lathe((0, 0, 1.93), [(0.0, 0.0), (0.12, 0.0), (0.1, 0.09), (0.0, 0.1)], "dorado", seg=12)
        for x in (-0.5, 0.5):
            rbox((x, 0.0, 0.04), (0.16, 0.6, 0.12), "violeta_osc", r=0.3)
        # hueco oscuro con brillo violeta (entre las puertas)
        rbox((0.0, -0.395, 0.95), (0.2, 0.02, 1.55), "violeta", r=0.2)
        p = kit.PARTS["raiz"][-1]; p["mk"] = "aura_lila"; p.data.materials[0] = kit.blmat("aura_lila")
        # puerta izquierda cerrada, derecha entreabierta
        rbox((-0.33, -0.42, 0.95), (0.6, 0.06, 1.6), "lila", r=0.25)
        ball((-0.1, -0.47, 0.95), 0.04, "dorado", seg=10, ring=6)
    with on("puerta"):
        rbox((0.33, -0.42, 0.95), (0.6, 0.06, 1.6), "lila", r=0.25)
        ball((0.1, -0.47, 0.95), 0.04, "dorado", seg=10, ring=6)
        star((0.33, -0.455, 1.35), 0.12, 0.05, 0.02, "dorado")
        torus((0.33, -0.455, 0.6), 0.1, 0.018, "dorado", rot=(90, 0, 0), seg=14, mseg=4)


# ------------------------------------------------------------------ proyectiles y efectos
def lanza():
    tube((0, 0, -0.2), (0, 0, 0.18), 0.012, 0.012, "marron", seg=6)
    cone((0, 0, 0.23), 0.035, 0.0, 0.09, "plata_m", seg=6)


def flecha():
    tube((0, 0, -0.18), (0, 0, 0.16), 0.008, 0.008, "crema", seg=5)
    cone((0, 0, 0.19), 0.022, 0.0, 0.05, "gris", seg=5)
    for a in (0, 120, 240):
        prism((0, 0, -0.15), [(0.0, -0.05), (0.035, -0.07), (0.035, 0.0), (0.0, 0.02)], 0.004, "rojo", rot=(0, 0, a))


def bola_fuego():
    ball((0, 0, 0), 0.11, "naranja", seg=12, ring=8)
    p = kit.PARTS["raiz"][-1]; p["mk"] = "aura"; p.data.materials[0] = kit.blmat("aura")
    ball((0, 0, 0), 0.07, "luz_velador", seg=8, ring=6)


def bala_canon():
    ball((0, 0, 0), 0.1, "grafito_m", seg=12, ring=8)


def nota():
    ball((0, 0, 0), (0.06, 0.04, 0.05), "rosa_chicle", seg=10, ring=6)
    tube((0.05, 0, 0.0), (0.05, 0, 0.17), 0.01, 0.01, "rosa_chicle", seg=5)
    prism((0.05, 0, 0.17), [(0.0, 0.0), (0.07, -0.03), (0.07, -0.06), (0.0, -0.03)], 0.012, "rosa_chicle")


def estrella_ninja():
    for a in (0, 90):
        prism((0, 0, 0), [(0.0, 0.1), (0.025, 0.025), (0.1, 0.0), (0.025, -0.025), (0.0, -0.1), (-0.025, -0.025), (-0.1, 0.0), (-0.025, 0.025)],
              0.015, "gris_osc", rot=(0, a, 0))
    torus((0, 0, 0), 0.02, 0.008, "lima", rot=(90, 0, 0), seg=8, mseg=4)


def daga():
    """Daga de la picara con veneno verde (proyectil)."""
    prism((0, 0, 0.06), [(-0.025, 0.0), (0.025, 0.0), (0.0, 0.16)], 0.012, "acero_m")
    rbox((0, 0, 0.0), (0.06, 0.02, 0.02), "cuero_osc_m", r=0.3, seg=1)
    tube((0, 0, -0.08), (0, 0, 0.0), 0.012, 0.012, "cuero_osc_m", seg=5)
    ball((0, 0, 0.14), 0.02, "veneno_m", seg=6, ring=4)


def palma():
    """Onda de palma de la monja: disco de energia dorada."""
    torus((0, 0, 0), 0.09, 0.03, "llama_cl", rot=(90, 0, 0), seg=14, mseg=5)
    ball((0, 0, 0), 0.055, "llama", seg=8, ring=6)


def chispa():
    """Chispa de moneda de batalla (icono 3D)."""
    prism((0, 0, 0), [(0.02, 0.12), (-0.07, -0.01), (-0.01, -0.01), (-0.03, -0.12), (0.07, 0.02), (0.01, 0.02)], 0.04, "amarillo", bevel=0.01)


# ------------------------------------------------------------------ objetos gigantes del escritorio
def taza():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (1.05, 0.0), (1.12, 0.1), (1.2, 2.4), (1.24, 2.6), (1.16, 2.62), (1.1, 2.45), (1.02, 0.25), (0.0, 0.25)],
              "taza_m", seg=36, angle=40)
        lathe((0, 0, 0), [(0.0, 2.2), (1.13, 2.2), (0.0, 2.2001)], "cafe_m", seg=36)
        torus((1.3, 0, 1.3), 0.6, 0.12, "taza_m", rot=(90, 0, 0), seg=24, mseg=8, arc=0.6)
        kit.PARTS["raiz"][-1].rotation_euler = (math.radians(90), 0, math.radians(-108))
        torus((0, 0, 1.6), 1.205, 0.06, "magenta", seg=36, mseg=4)
        for i in range(5):
            a = 2 * math.pi * i / 5 + 0.3
            star((math.cos(a) * 1.19, math.sin(a) * 1.19, 1.0), 0.16, 0.07, 0.03, "turquesa", rot=(0, 0, math.degrees(a) + 90))


def lapiz(col="lapiz_am", L=6.0):
    with on("raiz"):
        tube((0, 0, 0.6), (0, 0, L), 0.22, 0.22, col, seg=6, bevel=0.03)
        cone((0, 0, 0.35), 0.22, 0.05, 0.5, "carton", seg=6, rot=(180, 0, 0))
        cone((0, 0, 0.06), 0.06, 0.0, 0.12, "grafito_m", seg=6, rot=(180, 0, 0))
        tube((0, 0, L), (0, 0, L + 0.35), 0.225, 0.225, "plata_m", seg=12)
        tube((0, 0, L + 0.35), (0, 0, L + 0.75), 0.21, 0.21, "rosa", seg=12)


def regla(L=9.0):
    with on("raiz"):
        rbox((0, 0, 0.05), (L, 1.2, 0.1), "turquesa_cl", r=0.2)
        for i in range(int(L * 4)):
            x = -L / 2 + 0.2 + i * 0.25
            h = 0.35 if i % 4 == 0 else 0.18
            prism((x, -0.6 + h / 2 + 0.04, 0.105), [(-0.015, -h / 2), (0.015, -h / 2), (0.015, h / 2), (-0.015, h / 2)], 0.012, "noche", axis="Z")


def goma():
    with on("raiz"):
        rbox((0, 0, 0.35), (1.6, 0.9, 0.7), "rosa", r=0.25)
        rbox((0.45, 0, 0.36), (0.7, 0.92, 0.72), "azul", r=0.2)


def libro(col="violeta", w=3.2, d=4.2, h=0.7):
    with on("raiz"):
        rbox((0, 0, h / 2), (w, d, h), col, r=0.12)
        rbox((0.06, 0, h / 2), (w - 0.06, d - 0.18, h - 0.16), "papel", r=0.05)
        rbox((-w / 2 + 0.06, 0, h / 2), (0.14, d + 0.02, h + 0.02), col, r=0.4)
        rbox((0, -d / 2 + 0.6, h + 0.005), (w * 0.7, 0.18, 0.01), "dorado", r=0.1)


def tijera():
    with on("raiz"):
        for s in (-1, 1):
            torus((-1.6, s * 0.7, 0.12), 0.48, 0.13, "magenta", seg=20, mseg=6)
            prism((0.9, s * 0.18, 0.12), [(-1.2, -0.15), (2.0, -0.02), (2.0, 0.04), (-1.2, 0.15)], 0.08, "plata_m", rot=(0, 0, -s * 8), axis="Z")
        ball((-0.4, 0, 0.2), 0.12, "plata_m", seg=10, ring=6)


def lampara():
    """Lampara de escritorio apagada (el velador es la luz): base, brazo y pantalla."""
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (1.4, 0.0), (1.35, 0.25), (0.3, 0.4), (0.0, 0.4)], "turquesa_osc", seg=28)
        tube((0, 0, 0.3), (1.0, 0, 4.2), 0.12, 0.12, "turquesa_osc", seg=10)
        tube((1.0, 0, 4.2), (2.6, 0, 3.6), 0.12, 0.12, "turquesa_osc", seg=10)
        lathe((2.9, 0, 2.8), [(0.0, 0.0), (1.1, 0.0), (0.45, 0.9), (0.0, 0.95)], "turquesa", seg=24, rot=(0, -25, 0))


# ------------------------------------------------------------------ capitulos 2-8
def cereales():
    with on("raiz"):
        rbox((0, 0, 1.9), (2.4, 0.9, 3.8), "amarillo", r=0.04)
        rbox((0, -0.46, 2.3), (1.9, 0.02, 1.6), "rojo", r=0.2)
        for i in range(5): ball((-0.6 + i * 0.3, -0.48, 1.1 + (i % 2) * 0.2), (0.14, 0.04, 0.14), "naranja", seg=10, ring=6)
        star((0, -0.48, 3.3), 0.3, 0.13, 0.04, "blanco")


def leche():
    with on("raiz"):
        rbox((0, 0, 1.5), (1.3, 1.3, 3.0), "blanco", r=0.05)
        prism((0, 0, 3.0), [(-0.65, 0.0), (0.65, 0.0), (0.0, 0.6)], 1.3, "blanco", bevel=0.03)
        rbox((0, -0.66, 1.6), (1.1, 0.02, 1.2), "azul", r=0.2)
        ball((0.0, -0.68, 1.6), (0.3, 0.03, 0.22), "blanco", seg=12, ring=6)


def charco():
    """Leche derramada (se pone sobre la pista: frena a las pesadillas)."""
    with on("raiz"):
        for (x, y, r) in ((0, 0, 0.55), (0.35, 0.3, 0.35), (-0.3, -0.35, 0.4), (0.2, -0.45, 0.25)):
            tube((x, y, 0.0), (x, y, 0.02), r, r, "blanco", seg=18, bevel=0.0001)


def cuchara():
    with on("raiz"):
        ball((0, 0, 0.15), (0.55, 0.85, 0.15), "plata_m", seg=16, ring=8)
        tube((0, 0.75, 0.2), (0, 3.2, 0.35), 0.13, 0.15, "plata_m", seg=10)


def bowl():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.8, 0.0), (1.5, 0.9), (1.6, 1.0), (1.45, 1.0), (0.75, 0.15), (0.0, 0.15)], "turquesa", seg=28)
        lathe((0, 0, 0.75), [(0.0, 0.0), (1.38, 0.0), (0.0, 0.001)], "crema", seg=28)
        for i in range(7):
            a = i * 0.9
            torus((math.cos(a) * 0.7, math.sin(a) * 0.7, 0.8), 0.18, 0.07, "naranja", seg=10, mseg=4)


def patito():
    with on("raiz"):
        ball((0, 0, 0.7), (1.0, 1.2, 0.75), "plastico_am", seg=18, ring=10)
        ball((0, -0.75, 1.55), 0.6, "plastico_am", seg=16, ring=10)
        ball((0, -1.35, 1.45), (0.3, 0.3, 0.12), "naranja", seg=12, ring=6)
        for s_ in (-1, 1): ball((0.3 * s_, -1.2, 1.75), 0.1, "ojo", seg=8, ring=5)
        prism((0, 1.1, 1.2), [(-0.3, 0.0), (0.3, 0.0), (0.0, 0.5)], 0.4, "plastico_am", rot=(-40, 0, 0))


def burbujas():
    with on("raiz"):
        for (x, y, z, r) in ((0, 0, 0.5, 0.5), (0.7, 0.2, 0.35, 0.35), (-0.5, 0.4, 0.3, 0.3), (0.2, -0.6, 0.25, 0.25), (-0.2, 0.1, 1.0, 0.25)):
            ball((x, y, z), r, "cristal_vidrio", seg=14, ring=10)


def espuma():
    with on("raiz"):
        for i in range(12):
            a = i * 2.4
            ball((math.cos(a) * (0.3 + 0.08 * i), math.sin(a) * (0.3 + 0.08 * i), 0.2 + 0.05 * (i % 3)), 0.35 - 0.015 * i, "blanco", seg=10, ring=6)


def shampoo():
    with on("raiz"):
        rbox((0, 0, 1.4), (1.2, 0.7, 2.8), "rosa_chicle", r=0.25)
        tube((0, 0, 2.8), (0, 0, 3.3), 0.3, 0.3, "magenta", seg=12)
        rbox((0, -0.36, 1.4), (0.8, 0.02, 1.0), "blanco", r=0.2)


def sandia():
    with on("raiz"):
        prism((0, 0, 0.0), [(-1.6, 0.0), (1.6, 0.0), (1.1, 1.1), (0.0, 1.45), (-1.1, 1.1)], 0.9, "rojo", bevel=0.08)
        torus((0, 0, 0.0), 1.55, 0.18, "verde_osc", rot=(90, 0, 0), seg=20, mseg=6, arc=0.5)
        for (x, z) in ((-0.6, 0.6), (0.0, 0.9), (0.6, 0.6), (-0.3, 0.3), (0.35, 0.3)):
            ball((x, -0.47, z), (0.06, 0.03, 0.1), "contorno", seg=6, ring=4)


def hormiga():
    with on("raiz"):
        for k, r in enumerate((0.12, 0.09, 0.15)):
            ball((0, -0.25 + k * 0.22, 0.15), r, "contorno", seg=10, ring=6)
        for k in range(3):
            for s_ in (-1, 1):
                tube((0, -0.03 + k * 0.08, 0.13), (0.22 * s_, -0.08 + k * 0.12, 0.0), 0.015, 0.012, "contorno", seg=4)


def cesta():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (1.1, 0.0), (1.3, 1.0), (1.2, 1.0), (0.0, 0.1)], "carton", seg=24)
        torus((0, 0, 1.0), 1.25, 0.08, "carton_osc", seg=24, mseg=5)
        torus((0, 0, 1.0), 1.15, 0.08, "carton_osc", rot=(90, 0, 0), seg=20, mseg=5, arc=0.5)


def limonada():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.55, 0.0), (0.65, 1.6), (0.0, 1.6)], "cristal_vidrio", seg=20)
        lathe((0, 0, 0.05), [(0.0, 0.0), (0.53, 0.0), (0.6, 1.3), (0.0, 1.3)], "amarillo", seg=20)
        tube((0.2, 0, 1.0), (0.45, 0.1, 2.2), 0.05, 0.05, "magenta", seg=6)


def torta():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (1.8, 0.0), (1.8, 1.0), (1.2, 1.05), (1.2, 1.9), (0.0, 1.95)], "rosa", seg=28, angle=40)
        for z, r in ((1.0, 1.8), (1.9, 1.2)):
            for i in range(14 if r > 1.5 else 10):
                a = 2 * math.pi * i / (14 if r > 1.5 else 10)
                ball((math.cos(a) * r, math.sin(a) * r, z), 0.16, "crema", seg=8, ring=5)
        for i in range(5):
            a = 2 * math.pi * i / 5
            tube((math.cos(a) * 0.7, math.sin(a) * 0.7, 1.95), (math.cos(a) * 0.7, math.sin(a) * 0.7, 2.5), 0.05, 0.05, "turquesa" if i % 2 else "magenta", seg=6)
            ball((math.cos(a) * 0.7, math.sin(a) * 0.7, 2.6), (0.07, 0.07, 0.12), "luz_velador", seg=6, ring=4)


def globo(col="magenta"):
    with on("raiz"):
        ball((0, 0, 3.0), (0.9, 0.9, 1.1), col, seg=16, ring=10)
        cone((0, 0, 1.85), 0.12, 0.0, 0.15, col, seg=6, rot=(180, 0, 0))
        tube((0, 0, 1.8), (0.2, 0, 0.0), 0.012, 0.012, "blanco", seg=4)


def regalo(col="rojo", lazo="dorado", s_=1.0):
    with on("raiz"):
        rbox((0, 0, 0.6 * s_), (1.2 * s_, 1.2 * s_, 1.2 * s_), col, r=0.08)
        rbox((0, 0, 0.6 * s_), (1.24 * s_, 0.2 * s_, 1.24 * s_), lazo, r=0.2)
        rbox((0, 0, 0.6 * s_), (0.2 * s_, 1.24 * s_, 1.24 * s_), lazo, r=0.2)
        for a in (40, 140):
            torus((0, 0, 1.3 * s_), 0.25 * s_, 0.07 * s_, lazo, rot=(90, 0, a), seg=12, mseg=4)


def baul():
    with on("raiz"):
        rbox((0, 0, 0.9), (3.4, 2.0, 1.8), "marron", r=0.06)
        tube((-1.72, 0, 1.8), (1.72, 0, 1.8), 1.0, 1.0, "marron", seg=20, bevel=0.0001)
        for x in (-1.2, 0, 1.2): rbox((x, 0, 1.3), (0.2, 2.06, 2.6), "bronce_m", r=0.2)
        rbox((0, -1.02, 1.7), (0.4, 0.06, 0.5), "dorado", r=0.3)


def telarana():
    with on("raiz"):
        for k in range(1, 4):
            torus((0, 0, 0), 0.6 * k, 0.012, "blanco", seg=10, mseg=3, arc=0.25)
        for i in range(5):
            a = math.radians(i * 22.5)
            tube((0, 0, 0), (math.cos(a) * 2.0, math.sin(a) * 2.0, 0), 0.012, 0.012, "blanco", seg=3)


def linterna():
    with on("raiz"):
        tube((0, 0, 0.6), (0, 3.2, 0.6), 0.5, 0.5, "rojo", seg=16)
        tube((0, 3.2, 0.6), (0, 3.8, 0.6), 0.5, 0.75, "rojo", seg=16)
        tube((0, 3.8, 0.6), (0, 3.84, 0.6), 0.72, 0.72, "luz_velador", seg=16, bevel=0.0001)


def herramientas():
    with on("raiz"):
        rbox((0, 0, 0.7), (3.6, 1.6, 1.4), "rojo", r=0.06)
        rbox((0, 0, 1.45), (3.4, 1.4, 0.1), "rojo_osc", r=0.3)
        tube((-0.8, 0, 1.6), (0.8, 0, 1.6), 0.1, 0.1, "grafito_m", seg=8)
        rbox((0.6, 0.0, 1.9), (0.3, 0.3, 1.2), "plata_m", r=0.2, rot=(0, 30, 0))


def gotera():
    with on("raiz"):
        ball((0, 0, 0.0), (0.5, 0.5, 0.05), "azul", seg=14, ring=6)
        ball((0, 0, 0.6), (0.12, 0.12, 0.18), "cristal_vidrio", seg=10, ring=6)


def lata_pintura():
    with on("raiz"):
        tube((0, 0, 0), (0, 0, 1.4), 0.8, 0.8, "plata_m", seg=20)
        tube((0, 0, 1.4), (0, 0, 1.45), 0.75, 0.75, "turquesa", seg=20, bevel=0.0001)
        rbox((0, -0.8, 0.7), (0.9, 0.02, 0.8), "magenta", r=0.2)


def arbolito():
    with on("raiz"):
        tube((0, 0, 0), (0, 0, 0.8), 0.35, 0.35, "marron", seg=10)
        for k, (z, r) in enumerate(((0.8, 2.0), (2.2, 1.5), (3.4, 1.0))):
            cone((0, 0, z + 0.8), r, 0.15, 1.7, "verde_osc", seg=12)
            for i in range(6):
                a = 2 * math.pi * i / 6 + k
                ball((math.cos(a) * r * 0.7, math.sin(a) * r * 0.7, z + 0.4), 0.18, ["rojo", "dorado", "azul", "magenta"][i % 4], seg=8, ring=5)
        star((0, 0, 5.6), 0.5, 0.22, 0.15, "brillo_oro")


def nieve():
    with on("raiz"):
        for i in range(10):
            a = i * 2.3
            ball((math.cos(a) * 0.12 * i, math.sin(a) * 0.12 * i, 0.05), (0.4 - 0.02 * i, 0.4 - 0.02 * i, 0.12), "blanco", seg=10, ring=5)


EXPORTS = dict(peana_comun=lambda: peana("comun"), peana_raro=lambda: peana("raro"), peana_epico=lambda: peana("epico"),
               peana_legendario=lambda: peana("legendario"), estrellita=estrellita, flechita=flechita, lanza=lanza, flecha=flecha,
               bola_fuego=bola_fuego, bala_canon=bala_canon, daga=daga, palma=palma, nota=nota, estrella_ninja=estrella_ninja, chispa=chispa,
               taza=taza, lapiz=lapiz, lapiz_azul=lambda: lapiz("azul", 5.2), lapiz_rosa=lambda: lapiz("magenta", 6.6),
               regla=regla, goma=goma, libro=libro, libro_turquesa=lambda: libro("turquesa_osc", 3.0, 4.0, 0.6),
               libro_rojo=lambda: libro("rojo", 3.4, 4.4, 0.8), tijera=tijera, lampara=lampara,
               cereales=cereales, leche=leche, charco=charco, cuchara=cuchara, bowl=bowl,
               patito=patito, burbujas=burbujas, espuma=espuma, shampoo=shampoo,
               sandia=sandia, hormiga=hormiga, cesta=cesta, limonada=limonada,
               torta=torta, globo=globo, globo_turquesa=lambda: globo("turquesa"), globo_amarillo=lambda: globo("amarillo"),
               regalo=regalo, regalo_azul=lambda: regalo("azul", "plata_m", 0.8), regalo_verde=lambda: regalo("verde", "rojo", 1.2),
               baul=baul, telarana=telarana, linterna=linterna, herramientas=herramientas, gotera=gotera, lata_pintura=lata_pintura,
               arbolito=arbolito, nieve=nieve)


def produce_one(name):
    kit.reset()
    kit.bone("raiz", None, (0, 0, 0))
    if name == "velador":
        kit.bone("luz", "raiz", (0, 0, 0.34)); velador()
    elif name == "placard":
        kit.bone("puerta", "raiz", (0.63, -0.42, 0.0)); placard()
    else:
        with on("raiz"): EXPORTS[name]()
    return kit.export_model("u_" + name)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [x for x in (argv[0].split(",") if argv else []) if x] or (list(EXPORTS) + ["velador", "placard"])
    tot = 0
    for n in names:
        tot += produce_one(n)
    print("TRIS utileria total", tot)
