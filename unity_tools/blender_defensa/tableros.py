"""Guardianes del Cristal: tableros (una sala de la mazmorra por zona) y su utileria alrededor.

Uso: blender -b -P tableros.py -- [--only calabozo,cloacas]
Exporta por zona:
  t_<zona>_tablero.bytes  piso de losas, estrado de piedra con 15 casilleros tallados (3 columnas x 5 filas,
                          vertical para el celular) y el pasillo empedrado en U por donde bajan los monstruos
  t_<zona>_escena.json    utileria alrededor: [{m: modelo, p: [x, y, z], r: grados en Y, s: escala}]
MEDIDAS COMPARTIDAS CON EL JUEGO (Fortin.Core.Layout): casillero cada 1.15 (3 col x 5 filas centradas en 0),
pasillo: izquierda x = -2.5, abajo z = -3.6, derecha x = +2.5, arriba z = +3.55 (porton arriba a la izquierda,
Cristal del Corazon arriba a la derecha). Unity (x, z) = Blender (x, y). Arte: docs/DIRECCION_MAZMORRA.md.
"""
import sys, os, math, json, argparse, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import ball, rbox, tube, torus, cone, star, prism, lathe, on, ring, boolean
from mathutils import Vector

CELL = 1.15
COLS, ROWS = 3, 5
PX, PB, PT = 2.5, -3.6, 3.55         # pasillo: x a los costados, z abajo, z arriba
PW = 0.92                            # ancho del pasillo
WAY = [(-PX, PT), (-PX, PB), (PX, PB), (PX, PT)]
MAT_HW, MAT_HH = COLS * CELL / 2 + 0.17, ROWS * CELL / 2 + 0.17      # estrado de los casilleros
BX0, BX1 = -PX - PW / 2 - 0.3, PX + PW / 2 + 0.3                       # borde de piedra (x)
BY0, BY1 = PB - PW / 2 - 0.3, PT + 0.95                                # borde de piedra (y)

ZONAS = {
    # zona: piso de la sala, estrado, casilleros (2 tonos), pasillo (adoquines), runa del casillero vacio
    "calabozo": dict(piso=("piedra", "piedra_tibia", "losa"), estrado="piedra_osc", cas=("piedra_tibia", "piedra"), pas=("piedra", "piedra_tibia", "losa"), runa="piedra"),
    "cloacas": dict(piso=("losa", "piedra_osc", "losa", "musgo"), estrado="piedra_osc", cas=("piedra", "losa"), pas=("piedra", "losa", "piedra", "musgo"), runa="musgo"),
    "cristales": dict(piso=("losa", "piedra", "piedra_osc"), estrado="piedra_osc", cas=("piedra", "losa"), pas=("piedra", "losa", "piedra_osc"), runa="cristal_azul"),
    "raices": dict(piso=("piedra_tibia", "piedra", "tierra", "piedra_tibia", "musgo"), estrado="piedra_osc", cas=("piedra_tibia", "piedra"), pas=("piedra_tibia", "tierra", "piedra", "piedra_tibia"), runa="musgo"),
    "tesoro": dict(piso=("piedra_tibia", "piedra", "piedra_cl"), estrado="piedra_osc", cas=("piedra_tibia", "piedra"), pas=("piedra_tibia", "piedra", "piedra_cl"), runa="oro"),
    "cripta": dict(piso=("piedra_osc", "losa", "piedra"), estrado="hierro_osc", cas=("losa", "piedra"), pas=("piedra_osc", "losa", "piedra"), runa="cristal_magico"),
    "forja": dict(piso=("piedra_osc", "hierro_osc", "losa"), estrado="hierro_osc", cas=("piedra", "losa"), pas=("piedra_osc", "losa", "hierro_osc"), runa="fuego"),
    "hielo": dict(piso=("piedra_cl", "hielo", "piedra"), estrado="piedra", cas=("hielo", "piedra_cl"), pas=("piedra_cl", "hielo", "piedra"), runa="cristal_azul"),
}


def stones(rnd, cx, cy, w, h, z, keys, size=(0.55, 0.45), gap=0.045, height=0.1, r=0.25):
    """Adoquines / losas irregulares que llenan un rectangulo (cantos redondeados, mortero entre medio)."""
    y = cy - h / 2; row = 0
    while y < cy + h / 2 - 0.02:
        sh = size[1] * rnd.uniform(0.85, 1.15)
        x = cx - w / 2 - (size[0] * 0.45 if row % 2 else 0)
        while x < cx + w / 2 - 0.02:
            sw = size[0] * rnd.uniform(0.75, 1.3)
            x0 = max(x, cx - w / 2); x1 = min(x + sw, cx + w / 2); y1 = min(y + sh, cy + h / 2)
            if x1 - x0 > 0.08 and y1 - y > 0.08:
                rbox(((x0 + x1) / 2, (y + y1) / 2, z + rnd.uniform(-0.008, 0.008)), (x1 - x0 - gap, y1 - y - gap, height),
                     rnd.choice(keys), r=r, seg=1)
            x += sw
        y += sh; row += 1


def surface(c):
    """El piso de la sala: losas grandes sobre mortero oscuro."""
    rnd = random.Random(1)
    # la sala termina en el muro del fondo (y = 5.3): detras, oscuridad
    rbox((0, -2.1, -0.12), (12, 14.8, 0.2), "piedra_osc", r=0.01, seg=1)
    stones(rnd, 0, -2.1, 11.5, 14.6, -0.03, c["piso"], size=(1.25, 1.05), gap=0.05, height=0.08, r=0.18)


def board(c):
    rnd = random.Random(2)
    cx, cy = (BX0 + BX1) / 2, (BY0 + BY1) / 2
    W, H = BX1 - BX0, BY1 - BY0
    # base de piedra elevada (borde del estrado y del pasillo)
    rbox((cx, cy, 0.05), (W + 0.1, H + 0.1, 0.14), c["estrado"], r=0.25, seg=2)
    stones(rnd, cx, BY1 - 0.32, W - 0.1, 0.5, 0.135, c["pas"], size=(0.5, 0.5), height=0.05)
    for sx in (-1, 1):                                # bordes laterales y de abajo del pasillo
        stones(rnd, sx * (PX + PW / 2 + 0.15), cy, 0.26, H - 0.1, 0.135, c["pas"], size=(0.26, 0.45), height=0.05)
    stones(rnd, cx, BY0 + 0.15, W - 0.1, 0.26, 0.135, c["pas"], size=(0.45, 0.26), height=0.05)
    # pasillo empedrado en U (un poco mas bajo que los bordes: se lee como camino)
    tramos = [((-PX, PB + PW / 2), (-PX, PT)), ((-PX - PW / 2, PB), (PX + PW / 2, PB)), ((PX, PB + PW / 2), (PX, PT))]
    for (x0, y0), (x1, y1) in tramos:
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        w = abs(x1 - x0) or PW; h = abs(y1 - y0) or PW
        rbox((mx, my, 0.1), (w, h, 0.04), c["estrado"], r=0.2, seg=1)
        stones(rnd, mx, my, w - 0.06, h - 0.06, 0.125, c["pas"], size=(0.46, 0.38), gap=0.022, height=0.04, r=0.35)
    # flechas talladas en el pasillo (la direccion del recorrido)
    arrow = [(-0.16, -0.12), (0.0, 0.02), (0.16, -0.12), (0.16, 0.02), (0.0, 0.17), (-0.16, 0.02)]
    for i in range(len(WAY) - 1):
        (x0, y0), (x1, y1) = WAY[i], WAY[i + 1]
        L = math.hypot(x1 - x0, y1 - y0); n = int(L / 1.3)
        ang = math.degrees(math.atan2(y1 - y0, x1 - x0)) - 90
        for kk in range(1, n):
            t = kk / n
            if i == 0 and t < 0.12: continue
            prism((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, 0.152), arrow, 0.006, "piedra_osc", axis="Z", rot=(0, 0, ang))
    # estrado: 15 casilleros de piedra tallada, hundidos en un marco con bisel
    slab = rbox((0, 0, 0.15), (2 * MAT_HW, 2 * MAT_HH, 0.12), c["estrado"], r=0.2, seg=2)
    for col in range(COLS):
        for row in range(ROWS):
            x = (col - 1) * CELL; y = (2 - row) * CELL
            cut = rbox((x, y, 0.235), (1.03, 1.03, 0.12), c["estrado"], r=0.18, seg=3)
            boolean(slab, cut)
            # losa del casillero: 2x2 piedras talladas sobre mortero (se lee piedra, no papel)
            rbox((x, y, 0.11), (1.02, 1.02, 0.1), c["estrado"], r=0.2, seg=1)
            k0, k1 = c["cas"][(col + row) % 2], c["cas"][(col + row + 1) % 2]
            for qx in (-1, 1):
                for qy in (-1, 1):
                    k = k0 if rnd.random() < 0.9 else k1
                    hz = rnd.uniform(-0.006, 0.004)
                    rbox((x + qx * 0.252, y + qy * 0.252, 0.13 + hz), (0.475, 0.475, 0.12), k, r=0.09, seg=1)
            ring((x, y, 0.197), 0.3, 0.335, c["runa"], seg=24)               # circulo tallado (casillero vacio)
            for a in range(4):
                aa = math.pi / 4 + a * math.pi / 2
                ball((x + math.cos(aa) * 0.43, y + math.sin(aa) * 0.43, 0.19), (0.03, 0.03, 0.008), c["runa"], seg=6, ring=3)
    # base del porton y del cristal (circulo de runas)
    rbox((-PX, PT + 0.2, 0.13), (1.25, 1.1, 0.08), c["estrado"], r=0.3, seg=1)
    lathe((PX, PT + 0.15, 0.1), [(0.0, 0.0), (0.66, 0.0), (0.66, 0.07), (0.0, 0.07)], c["estrado"], seg=24)
    ring((PX, PT + 0.15, 0.172), 0.5, 0.56, "cristal_azul", seg=32)


# utileria alrededor (Blender x, y; y = profundidad). Arriba: el muro del fondo con antorchas y estandartes;
# abajo y a los costados, cosas cortadas por el borde de la pantalla.
def wall(kind="muro"):
    return [dict(m="u_" + kind, p=(-3.9, 5.25, 0.0), r=0), dict(m="u_" + kind + ("_b" if kind == "muro" else ""), p=(-1.3, 5.25, 0.0), r=0),
            dict(m="u_" + kind, p=(1.3, 5.25, 0.0), r=0), dict(m="u_" + kind + ("_b" if kind == "muro" else ""), p=(3.9, 5.25, 0.0), r=0)]


TORCHES = [dict(m="u_antorcha", p=(-1.25, 5.13, 0.4), r=0), dict(m="u_antorcha", p=(1.25, 5.13, 0.4), r=0)]
DRESS = {
    "calabozo": wall() + TORCHES + [
        dict(m="u_estandarte", p=(0.0, 5.12, 0.15), r=0), dict(m="u_cadenas", p=(-2.6, 5.12, 0.1), r=0),
        dict(m="u_barril", p=(-3.45, -5.0, 0.0), r=20), dict(m="u_caja", p=(3.35, -5.2, 0.0), r=15), dict(m="u_calaveras", p=(3.4, 4.6, 0.0), r=-20),
    ],
    "cloacas": wall("muro_musgo") + TORCHES + [
        dict(m="u_tubo", p=(0.0, 5.0, 0.0), r=0), dict(m="u_rejilla", p=(-3.45, -5.0, 0.0), r=0),
        dict(m="u_charco_lodo", p=(-2.5, -1.5, 0.155), r=0, s=1.1), dict(m="u_charco_lodo", p=(3.3, -5.3, 0.0), r=40, s=1.4),
        dict(m="u_barril", p=(3.4, 4.5, 0.0), r=0),
    ],
    "cristales": wall() + TORCHES + [
        dict(m="u_cristales", p=(0.0, 4.75, 0.0), r=0, s=1.1), dict(m="u_cristales_lila", p=(-3.45, -5.0, 0.0), r=40),
        dict(m="u_cristales", p=(3.4, -5.1, 0.0), r=-20, s=0.9), dict(m="u_cristales_lila", p=(3.55, 4.4, 0.0), r=10, s=0.7),
    ],
    "raices": wall("muro_musgo") + TORCHES + [
        dict(m="u_raices", p=(0.0, 4.75, 0.0), r=0, s=1.3), dict(m="u_hongos", p=(-3.4, -4.9, 0.0), r=0, s=1.2),
        dict(m="u_hongos", p=(3.4, -5.0, 0.0), r=60), dict(m="u_raices", p=(-3.5, 4.4, 0.0), r=70),
    ],
    "tesoro": wall() + TORCHES + [
        dict(m="u_estandarte", p=(0.0, 5.12, 0.15), r=0), dict(m="u_oro", p=(-3.3, -5.0, 0.0), r=20, s=1.1),
        dict(m="u_oro", p=(3.4, -5.2, 0.0), r=-30, s=0.9), dict(m="u_caja", p=(3.5, 4.5, 0.0), r=10),
    ],
    "cripta": wall("muro_cripta") + TORCHES + [
        dict(m="u_estandarte_violeta", p=(0.0, 5.12, 0.15), r=0), dict(m="u_ataud", p=(-3.45, -5.0, 0.0), r=20),
        dict(m="u_velas", p=(3.35, -5.0, 0.0), r=0, s=1.2), dict(m="u_calaveras", p=(3.4, 4.55, 0.0), r=10), dict(m="u_cadenas", p=(-2.6, 5.12, 0.1), r=0),
    ],
    "forja": wall() + TORCHES + [
        dict(m="u_forja", p=(0.0, 4.95, 0.0), r=0, s=0.8), dict(m="u_yunque", p=(-3.4, -5.0, 0.0), r=20),
        dict(m="u_barril", p=(3.4, -5.1, 0.0), r=0), dict(m="u_caja", p=(3.5, 4.5, 0.0), r=-10),
    ],
    "hielo": wall("muro_hielo") + TORCHES + [
        dict(m="u_carambanos", p=(0.0, 5.0, 0.0), r=0), dict(m="u_hielo_bloque", p=(-3.4, -5.0, 0.0), r=0),
        dict(m="u_hielo_bloque", p=(3.45, -5.1, 0.0), r=45, s=0.8), dict(m="u_estandarte_azul", p=(2.6, 5.12, 0.15), r=0),
    ],
}
CAPS = ZONAS   # nombre viejo (lamina.py)


def produce(cap):
    kit.reset()
    kit.bone("raiz", None, (0, 0, 0))
    c = ZONAS[cap]
    with on("raiz"):
        surface(c)
        board(c)
    t = kit.export_model("t_%s_tablero" % cap)
    esc = []
    for d in DRESS.get(cap, []):
        p = d["p"]
        s = d.get("s", 1)
        esc.append(dict(m=d["m"], p=[p[0], p[2], p[1]], r=d.get("r", 0), s=s, rx=d.get("rx", 0), rz=d.get("rz", 0)))
    with open(os.path.join(kit.OUT_MODELS, "t_%s_escena.json" % cap), "w") as f:  # TextAsset (JsonUtility)
        json.dump(dict(items=esc, way=[[x, y] for x, y in WAY], cell=CELL), f)
    return t


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser(); ap.add_argument("--only", default="calabozo")
    a = ap.parse_args(argv)
    for cap in a.only.split(","):
        print("TRIS", cap, produce(cap))
