"""Guardianes del Cristal: utileria de la mazmorra (todo por codigo, sin modelos generados).

Uso: blender -b -P mazmorra.py -- [cristal,porton,muro,...]
Exporta Resources/Models/u_<nombre>.bytes. Medidas en metros del tablero (casillero = 1.15, heroe ~1.5 de alto).
Direccion de arte: docs/DIRECCION_MAZMORRA.md (piedra tibia, hierro, madera, luz de antorchas; formas gorditas con
bisel, nada de neon).
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import ball, rbox, tube, capsule, torus, cone, star, prism, lathe, on, ring

kit.matdef("antorcha_llama", "fuego", glow=1.0)
kit.matdef("antorcha_nucleo", "fuego_cl", glow=1.0)
kit.matdef("cristal_corazon", "cristal_azul", shine=0.9, glow=0.55)
kit.matdef("cristal_lila", "cristal_magico", shine=0.9, glow=0.5)
kit.matdef("hierro_m", "hierro", shine=0.7)
kit.matdef("hierro_osc_m", "hierro_osc", shine=0.6)
kit.matdef("oro_moneda", "oro", shine=1.0, glow=0.15)
kit.matdef("lava_m", "lava", glow=0.9)
kit.matdef("hielo_m", "hielo", shine=0.85, glow=0.12)
kit.matdef("vela_luz", "fuego_cl", glow=1.0)
kit.matdef("hongo_luz", "cristal_azul", glow=0.8)
kit.matdef("agua_sucia", "musgo", shine=0.9)


def stones(rnd, cx, cy, w, h, z, key_pool, size=(0.5, 0.42), gap=0.04, height=0.12):
    """Adoquines / bloques irregulares que llenan un rectangulo (bisel grande: cantos gorditos)."""
    y = cy - h / 2
    row = 0
    while y < cy + h / 2 - 0.01:
        sh = size[1] * rnd.uniform(0.85, 1.15)
        x = cx - w / 2 - (size[0] * 0.5 if row % 2 else 0)
        while x < cx + w / 2 - 0.01:
            sw = size[0] * rnd.uniform(0.75, 1.3)
            x0 = max(x, cx - w / 2); x1 = min(x + sw, cx + w / 2)
            y1 = min(y + sh, cy + h / 2)
            if x1 - x0 > 0.06:
                rbox(((x0 + x1) / 2, (y + y1) / 2, z + rnd.uniform(-0.01, 0.01)), (x1 - x0 - gap, y1 - y - gap, height),
                     rnd.choice(key_pool), r=0.22, seg=1)
            x += sw
        y += sh; row += 1


# ------------------------------------------------------------------ lo que se defiende y por donde entran
def cristal():
    """Cristal del Corazon: gema grande facetada sobre un pedestal de piedra tallada con aro de oro."""
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.5, 0.0), (0.52, 0.06), (0.44, 0.12), (0.34, 0.16), (0.28, 0.4), (0.36, 0.46),
                          (0.38, 0.52), (0.0, 0.52)], "piedra_cl", seg=8, smooth=False)
        torus((0, 0, 0.5), 0.36, 0.03, "oro_m", seg=24, mseg=5)
        for i in range(4):          # garras de oro que sostienen la gema
            a = math.pi / 4 + i * math.pi / 2
            tube((math.cos(a) * 0.3, math.sin(a) * 0.3, 0.5), (math.cos(a) * 0.17, math.sin(a) * 0.17, 0.82), 0.03, 0.02, "oro_m", seg=6)
    with on("luz"):
        # gema: doble piramide de 6 caras (se lee como cristal, no como globo)
        lathe((0, 0, 0.55), [(0.0, 0.0), (0.24, 0.26), (0.24, 0.5), (0.0, 0.92)], "cristal_corazon", seg=6, smooth=False)
        lathe((0, 0, 0.62), [(0.0, 0.08), (0.13, 0.27), (0.13, 0.45), (0.0, 0.72)], "ojo_brillo", seg=6, smooth=False)
        for i, (a, z, r) in enumerate(((0.3, 1.0, 0.07), (2.4, 0.86, 0.06), (4.4, 1.12, 0.05))):   # esquirlas que flotan
            lathe((math.cos(a) * 0.42, math.sin(a) * 0.42, z), [(0.0, 0.0), (r, r * 1.2), (0.0, r * 2.6)], "cristal_corazon", seg=4, smooth=False)


def porton():
    """Porton de la mazmorra: arco de piedra con reja de hierro levantada a medias y oscuridad adentro."""
    with on("raiz"):
        # hueco oscuro
        rbox((0, 0.12, 0.7), (1.0, 0.1, 1.35), "negro_suave", r=0.1)
        # jambas y arco de bloques
        for sx in (-1, 1):
            for k in range(4):
                rbox((0.62 * sx, 0, 0.18 + k * 0.33), (0.34, 0.5, 0.31), "piedra" if k % 2 else "piedra_tibia", r=0.18, seg=1)
        for k in range(7):
            a = math.pi * k / 6
            rbox((math.cos(a) * 0.62, 0, 1.38 + math.sin(a) * 0.48), (0.3, 0.52, 0.26), "piedra_tibia" if k % 2 else "piedra",
                 r=0.2, seg=1, rot=(0, -math.degrees(a) + 90, 0))
        rbox((0, 0, 1.92), (0.3, 0.54, 0.3), "piedra_cl", r=0.2, seg=1)          # clave del arco
        ball((0, -0.28, 1.93), (0.09, 0.04, 0.1), "hueso", seg=10, ring=6)       # calavera tallada
        # reja de hierro levantada (los monstruos pasan por abajo)
        for x in (-0.36, -0.18, 0.0, 0.18, 0.36):
            tube((x, -0.08, 0.62), (x, -0.08, 1.55), 0.025, 0.025, "hierro_osc_m", seg=6)
            cone((x, -0.08, 0.57), 0.0, 0.035, 0.09, "hierro_m", seg=6)
        for z in (0.82, 1.3):
            tube((-0.45, -0.08, z), (0.45, -0.08, z), 0.022, 0.022, "hierro_osc_m", seg=6)
        # escalon
        rbox((0, -0.18, 0.04), (1.1, 0.5, 0.08), "piedra_osc", r=0.3, seg=1)


# ------------------------------------------------------------------ mazmorra general
def muro(seed=1, w=2.6, h=3.4, keys=("piedra", "piedra_tibia", "losa")):
    """Muro de bloques de piedra con zocalo y mortero oscuro."""
    rnd = random.Random(seed)
    with on("raiz"):
        rbox((0, 0.06, h / 2), (w, 0.2, h), "piedra_osc", r=0.05, seg=1)            # mortero
        y = 0.0; row = 0
        while y < h - 0.05:
            bh = 0.36
            x = -w / 2 - (0.3 if row % 2 else 0)
            while x < w / 2:
                bw = rnd.uniform(0.5, 0.75)
                x0 = max(x, -w / 2); x1 = min(x + bw, w / 2)
                if x1 - x0 > 0.1:
                    rbox(((x0 + x1) / 2, -0.04, y + bh / 2), (x1 - x0 - 0.05, 0.16, bh - 0.05), rnd.choice(keys), r=0.2, seg=1)
                x += bw
            y += bh; row += 1
        rbox((0, -0.12, 0.1), (w, 0.16, 0.2), "piedra_osc", r=0.3, seg=1)            # zocalo
        rbox((0, -0.02, h + 0.08), (w, 0.36, 0.18), "piedra_osc", r=0.3, seg=1)      # cornisa


def antorcha():
    """Antorcha de pared: soporte de hierro, mango de madera envuelto y llama (el juego le suma un brillo)."""
    with on("raiz"):
        rbox((0, 0.0, 1.0), (0.16, 0.06, 0.3), "hierro_osc_m", r=0.3)
        tube((0, -0.03, 0.95), (0, -0.16, 1.08), 0.018, 0.018, "hierro_m", seg=6)
        torus((0, -0.16, 1.1), 0.06, 0.016, "hierro_m", seg=10, mseg=4)
        tube((0, -0.16, 0.92), (0, -0.16, 1.24), 0.035, 0.045, "madera_mesa", seg=8)
        tube((0, -0.16, 1.16), (0, -0.16, 1.26), 0.052, 0.052, "cuero_osc", seg=8)
        cone((0, -0.16, 1.38), 0.075, 0.0, 0.26, "antorcha_llama", seg=8)
        cone((0, -0.16, 1.35), 0.042, 0.0, 0.16, "antorcha_nucleo", seg=6)


def barril():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.3, 0.0), (0.36, 0.25), (0.37, 0.4), (0.36, 0.55), (0.3, 0.8), (0.0, 0.8)], "madera_mesa", seg=14)
        for z in (0.12, 0.68):
            torus((0, 0, z), 0.335, 0.022, "hierro_osc_m", seg=16, mseg=4)
        lathe((0, 0, 0.8), [(0.0, -0.01), (0.27, -0.01), (0.27, 0.0), (0.0, 0.01)], "cuero", seg=14)


def caja():
    with on("raiz"):
        rbox((0, 0, 0.3), (0.62, 0.62, 0.6), "madera_mesa", r=0.08)
        for z in (0.06, 0.54):
            rbox((0, 0, z), (0.66, 0.66, 0.08), "cuero_osc", r=0.3, seg=1)
        rbox((0, -0.315, 0.3), (0.08, 0.02, 0.5), "cuero_osc", r=0.3, seg=1, rot=(0, 45, 0))


def estandarte(col="tela_roja"):
    """Estandarte colgado: barra de hierro, tela con emblema dorado y borde en punta."""
    def f():
        with on("raiz"):
            tube((-0.42, -0.05, 2.3), (0.42, -0.05, 2.3), 0.025, 0.025, "hierro_m", seg=6)
            for sx in (-1, 1): ball((0.44 * sx, -0.05, 2.3), 0.04, "oro_m", seg=8, ring=5)
            prism((0, -0.08, 0), [(-0.34, 2.27), (0.34, 2.27), (0.34, 1.2), (0.0, 0.98), (-0.34, 1.2)], 0.03, col, bevel=0.01)
            star((0, -0.1, 1.7), 0.14, 0.06, 0.02, "oro_m")
    return f


def cadenas():
    with on("raiz"):
        for sx in (-1, 1):
            for k in range(7):
                torus((0.25 * sx, -0.06, 2.2 - k * 0.11), 0.045, 0.012, "hierro_osc_m", rot=(90 if k % 2 else 0, 0, 90), seg=8, mseg=3)
        tube((-0.32, -0.04, 2.3), (0.32, -0.04, 2.3), 0.03, 0.03, "hierro_osc_m", seg=6)


def calaveras():
    with on("raiz"):
        for (x, y, s) in ((0, 0, 1.0), (0.22, 0.08, 0.85), (-0.2, 0.1, 0.8), (0.05, 0.12, 0.8)):
            z = 0.12 * s + (0.16 if (x, y) == (0.05, 0.12) else 0)
            ball((x, y, z), (0.12 * s, 0.11 * s, 0.11 * s), "hueso", seg=10, ring=7)
            for sx in (-1, 1): ball((x + 0.04 * s * sx, y - 0.09 * s, z + 0.01), 0.025 * s, "negro_suave", seg=6, ring=4)
        tube((-0.3, -0.05, 0.04), (0.3, 0.12, 0.04), 0.025, 0.025, "hueso", seg=6)


def pilar():
    with on("raiz"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.5, 0.0), (0.5, 0.2), (0.4, 0.28), (0.36, 2.4), (0.46, 2.5), (0.46, 2.65), (0.0, 2.65)],
              "piedra", seg=10, smooth=False)


# ------------------------------------------------------------------ zonas
def charco_lodo():
    with on("raiz"):
        for (x, y, r) in ((0, 0, 0.42), (0.3, 0.15, 0.25), (-0.28, -0.12, 0.22)):
            ball((x, y, 0.0), (r, r * 0.9, 0.025), "agua_sucia", seg=14, ring=4)


def rejilla():
    """Rejilla de cloaca en el piso con un hilo de agua."""
    with on("raiz"):
        rbox((0, 0, 0.03), (0.9, 0.9, 0.06), "piedra_osc", r=0.3, seg=1)
        for k in range(-3, 4):
            rbox((k * 0.11, 0, 0.065), (0.04, 0.7, 0.03), "hierro_osc_m", r=0.3, seg=1)


def tubo():
    with on("raiz"):
        tube((-0.9, 0, 1.6), (0.9, 0, 1.6), 0.16, 0.16, "hierro_m", seg=12)
        for x in (-0.6, 0.6): torus((x, 0, 1.6), 0.17, 0.03, "hierro_osc_m", rot=(0, 90, 0), seg=12, mseg=4)
        tube((0.9, 0, 1.6), (0.9, 0, 0.3), 0.16, 0.16, "hierro_m", seg=12)
        ball((0.9, -0.05, 0.25), (0.2, 0.2, 0.04), "agua_sucia", seg=12, ring=4)


def cristales(col="cristal_corazon"):
    def f():
        rnd = random.Random(3)
        with on("raiz"):
            ball((0, 0, 0.05), (0.5, 0.45, 0.12), "piedra_osc", seg=10, ring=5)
            for i in range(6):
                a = i * 1.05 + rnd.uniform(-0.2, 0.2); d = rnd.uniform(0.0, 0.28); h = rnd.uniform(0.4, 0.9)
                lathe((math.cos(a) * d, math.sin(a) * d, 0.05), [(0.0, 0.0), (0.09, 0.05), (0.09, h * 0.75), (0.0, h)], col, seg=5,
                      smooth=False, rot=(rnd.uniform(-20, 20), rnd.uniform(-20, 20), 0))
    return f


def hongos():
    rnd = random.Random(5)
    with on("raiz"):
        for i in range(5):
            a = i * 1.3; d = 0.12 + 0.12 * (i % 2); h = rnd.uniform(0.18, 0.42)
            x, y = math.cos(a) * d, math.sin(a) * d
            tube((x, y, 0.0), (x, y, h), 0.035, 0.03, "tela_blanca", seg=6)
            ball((x, y, h), (0.1 + h * 0.15, 0.1 + h * 0.15, 0.06), "hongo_luz", seg=10, ring=5)


def raices():
    rnd = random.Random(7)
    with on("raiz"):
        for i in range(5):
            a0 = rnd.uniform(0, math.pi); L = rnd.uniform(0.9, 1.5)
            p0 = (0, 0, 0.05); p1 = (math.cos(a0) * L * 0.5, math.sin(a0) * L * 0.4, 0.12); p2 = (math.cos(a0) * L, math.sin(a0) * L * 0.8, 0.0)
            tube(p0, p1, 0.09, 0.06, "tierra", seg=7); tube(p1, p2, 0.06, 0.02, "tierra", seg=6)
        ball((0, 0, 0.05), (0.2, 0.2, 0.12), "tierra", seg=10, ring=6)


def oro():
    """Monton de monedas de oro con un cofre abierto."""
    rnd = random.Random(9)
    with on("raiz"):
        ball((0, 0, 0.0), (0.55, 0.5, 0.22), "oro_moneda", seg=14, ring=6)
        for i in range(10):
            a = rnd.uniform(0, 6.28); d = rnd.uniform(0.2, 0.6)
            tube((math.cos(a) * d, math.sin(a) * d, 0.05 + rnd.uniform(0, 0.12)), (math.cos(a) * d, math.sin(a) * d, 0.075 + rnd.uniform(0, 0.12)),
                 0.07, 0.07, "oro_moneda", seg=10)
        rbox((0.3, 0.25, 0.25), (0.5, 0.36, 0.3), "madera_mesa", r=0.12)
        rbox((0.3, 0.42, 0.52), (0.5, 0.08, 0.34), "madera_mesa", r=0.2, rot=(-60, 0, 0))
        for x in (0.1, 0.5): rbox((x, 0.25, 0.25), (0.05, 0.38, 0.32), "oro_m", r=0.3, seg=1)
        ball((0.3, 0.25, 0.42), (0.2, 0.14, 0.07), "oro_moneda", seg=10, ring=4)


def ataud():
    with on("raiz"):
        prism((0, 0, 0.18), [(-0.3, -0.7), (0.3, -0.7), (0.38, 0.3), (0.2, 0.75), (-0.2, 0.75), (-0.38, 0.3)], 0.36, "madera_mesa",
              axis="Z", bevel=0.04)
        prism((0, 0, 0.38), [(-0.26, -0.62), (0.26, -0.62), (0.33, 0.28), (0.17, 0.68), (-0.17, 0.68), (-0.33, 0.28)], 0.06, "cuero_osc",
              axis="Z", bevel=0.02)
        rbox((0, 0.0, 0.43), (0.06, 0.6, 0.03), "hueso", r=0.3, seg=1)
        rbox((0, 0.12, 0.43), (0.34, 0.06, 0.03), "hueso", r=0.3, seg=1)


def velas():
    with on("raiz"):
        for (x, y, h) in ((0, 0, 0.42), (0.16, 0.08, 0.3), (-0.14, 0.1, 0.24), (0.06, -0.14, 0.18)):
            tube((x, y, 0), (x, y, h), 0.05, 0.05, "tela_blanca", seg=8)
            ball((x + 0.03, y - 0.04, h - 0.05), (0.025, 0.02, 0.06), "tela_blanca", seg=6, ring=4)
            cone((x, y, h + 0.05), 0.022, 0.0, 0.08, "vela_luz", seg=6)


def yunque():
    with on("raiz"):
        rbox((0, 0, 0.18), (0.36, 0.36, 0.36), "piedra_osc", r=0.15)
        rbox((0, 0, 0.42), (0.26, 0.18, 0.14), "hierro_osc_m", r=0.2)
        rbox((0, 0, 0.55), (0.62, 0.26, 0.14), "hierro_m", r=0.25)
        cone((0.38, 0, 0.55), 0.12, 0.0, 0.26, "hierro_m", seg=8, rot=(0, 90, 0))


def forja():
    with on("raiz"):
        rbox((0, 0, 0.45), (1.3, 0.9, 0.9), "piedra_osc", r=0.12)
        rbox((0, -0.3, 0.55), (0.8, 0.4, 0.5), "negro_suave", r=0.3)
        ball((0, -0.3, 0.4), (0.38, 0.2, 0.12), "lava_m", seg=12, ring=6)
        lathe((0, 0.1, 0.9), [(0.0, 0.0), (0.55, 0.0), (0.3, 0.9), (0.2, 1.8), (0.0, 1.8)], "piedra", seg=8, smooth=False)


def hielo_bloque():
    rnd = random.Random(11)
    with on("raiz"):
        for i in range(4):
            a = i * 1.6; d = 0.15 * i
            s = rnd.uniform(0.35, 0.6)
            rbox((math.cos(a) * d, math.sin(a) * d, s / 2), (s, s * 0.9, s), "hielo_m", r=0.15, seg=1, rot=(0, 0, rnd.uniform(0, 40)))


def carambanos():
    """Muro de hielo con carambanos colgando (para el castillo de hielo)."""
    with on("raiz"):
        rbox((0, 0.06, 1.3), (2.6, 0.2, 2.6), "hielo_m", r=0.05, seg=1)
        for k in range(9):
            x = -1.15 + k * 0.29
            cone((x, -0.06, 2.3 - (k % 3) * 0.05), 0.07, 0.0, 0.35 + (k % 3) * 0.12, "hielo_m", seg=6, rot=(180, 0, 0))


EXPORTS = dict(muro=muro, muro_b=lambda: muro(seed=2), antorcha=antorcha, barril=barril, caja=caja,
               estandarte=estandarte("tela_roja"), estandarte_azul=estandarte("tela_azul"), estandarte_verde=estandarte("tela_verde"),
               estandarte_violeta=estandarte("tela_violeta"), cadenas=cadenas, calaveras=calaveras, pilar=pilar,
               charco_lodo=charco_lodo, rejilla=rejilla, tubo=tubo, cristales=cristales("cristal_corazon"),
               cristales_lila=cristales("cristal_lila"), hongos=hongos, raices=raices, oro=oro, ataud=ataud, velas=velas,
               yunque=yunque, forja=forja, hielo_bloque=hielo_bloque, carambanos=carambanos,
               muro_cripta=lambda: muro(seed=3, keys=("piedra_osc", "losa", "piedra")),
               muro_hielo=lambda: muro(seed=4, keys=("hielo", "piedra_cl", "hielo")),
               muro_musgo=lambda: muro(seed=5, keys=("piedra", "piedra_tibia", "piedra", "losa", "musgo")))


def produce_one(name):
    kit.reset()
    kit.bone("raiz", None, (0, 0, 0))
    if name == "cristal":
        kit.bone("luz", "raiz", (0, 0, 0.52)); cristal()
    elif name == "porton":
        porton()
    else:
        EXPORTS[name]()
    return kit.export_model("u_" + name)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [x for x in (argv[0].split(",") if argv else []) if x] or (["cristal", "porton"] + list(EXPORTS))
    tot = 0
    for n in names:
        t = produce_one(n); tot += t
    print("TRIS mazmorra total", tot)
