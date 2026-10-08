"""Guardianes del Cristal: monstruos de la mazmorra (tiernos de lejos) y jefes.

Uso: blender -b -P enemigos.py -- [--only pelusa,babosa]
Exporta Resources/Models/e_<id>.json + e_<id>_anim.json (caminar, golpe, morir, aparecer[, especial]).
Ojos grandes, colores acidos (lima, violeta, rosa chicle) y contorno oscuro suave (lo pone el sombreado del juego).
Presupuesto: <= 1.500 triangulos (jefes <= 6.000). Frente hacia -Y.
"""
import sys, os, math, random, argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import ball, rbox, tube, capsule, torus, cone, star, prism, lathe, on, key, pose, rest_all, clip, boolean, ring
from mathutils import Vector


def eyes(c, dx, r, look=(0, 0), lids=False, k_white="blanco", k_pupil="ojo", squint=1.0):
    """Ojos grandes: blanco + pupila + brillo. c = centro entre los ojos (frente -Y)."""
    c = Vector(c)
    for s in (-1, 1):
        p = c + Vector((dx * s, 0, 0))
        ball(p, (r, r * 0.55, r * 1.1 * squint), k_white, seg=12, ring=8)
        pp = p + Vector((look[0] * r * 0.3 + 0.08 * r * s, -r * 0.42, look[1] * r * 0.3 - 0.05 * r))
        ball(pp, (r * 0.58, r * 0.3, r * 0.68 * squint), k_pupil, seg=10, ring=7)
        ball(pp + Vector((-r * 0.2, -r * 0.24, r * 0.25)), r * 0.2, "ojo_brillo", seg=6, ring=4)
        if lids:
            ball(p + Vector((0, -0.01, r * 0.55)), (r * 1.08, r * 0.62, r * 0.55), lids, seg=10, ring=5)


def mouth(c, w, open_=False, k="contorno"):
    if open_:
        ball(c, (w, w * 0.4, w * 0.6), k, seg=10, ring=6)
        ball(Vector(c) + Vector((0, -0.005, -w * 0.2)), (w * 0.6, w * 0.35, w * 0.3), "rosa_chicle", seg=8, ring=4)
    else:
        torus(c, w, w * 0.25, k, seg=10, mseg=4, arc=0.5)
        kit.PARTS[kit.CUR[-1]][-1].rotation_euler = (math.radians(90), math.radians(180), 0)


def puffs(c, r, key_, n=28, seed=3, size=0.32, skip_face=True):
    """Pelusa: bolitas de pelo repartidas sobre la esfera (nube suave, nada filoso)."""
    rnd = random.Random(seed)
    gold = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        y = 1 - (i + 0.5) / n * 2
        rr = math.sqrt(1 - y * y); th = gold * i
        d = Vector((math.cos(th) * rr, math.sin(th) * rr, y))
        if skip_face and d.y < -0.6 and abs(d.z) < 0.55 and abs(d.x) < 0.6: continue
        R = r * size * (0.75 + 0.5 * rnd.random())
        ball(Vector(c) + d * (r * 0.92), (R, R, R * 0.9), key_, seg=7, ring=4)


def fluff(c, r, key_, n=26, seed=3, length=0.35):
    """Mechones de pelusa: conitos gorditos repartidos sobre la esfera."""
    rnd = random.Random(seed)
    gold = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        y = 1 - (i + 0.5) / n * 2
        rr = math.sqrt(1 - y * y); th = gold * i
        d = Vector((math.cos(th) * rr, math.sin(th) * rr, y))
        if d.y < -0.55 and abs(d.z) < 0.5: continue          # la cara queda despejada
        L = r * length * (0.8 + 0.4 * rnd.random())
        p = Vector(c) + d * (r * 0.9 + L * 0.4)
        cone(p, r * 0.22, 0.0, L, key_, seg=5, rot=(0, 0, 0))
        o = kit.PARTS[kit.CUR[-1]][-1]
        o.rotation_mode = "QUATERNION"; o.rotation_quaternion = d.to_track_quat("Z", "Y")


ENEMIES = {}


def enemy(fn):
    ENEMIES[fn.__name__] = fn
    return fn


def bones_basic(feet=True, extra=()):
    kit.bone("raiz", None, (0, 0, 0))
    kit.bone("cuerpo", "raiz", (0, 0, 0.05))
    if feet:
        for s, n in ((1, "L"), (-1, "R")):
            kit.bone("pata_" + n, "raiz", (0.1 * s, 0, 0.05))
    for name, parent, head in extra:
        kit.bone(name, parent, head)


def anim_walk_hop(h=0.12, length=16, squash=0.2, tilt=6):
    rest_all(0)
    pose(0, raiz=dict(scale=(1 + squash * 0.6, 1 + squash * 0.6, 1 - squash)))
    pose(length // 4, raiz=dict(loc=(0, 0, h * 0.8), scale=(0.9, 0.9, 1.14), rot=(-tilt, 0, 0)))
    pose(length // 2, raiz=dict(loc=(0, 0, h), scale=(0.96, 0.96, 1.05), rot=(0, 0, 0)))
    pose(3 * length // 4, raiz=dict(loc=(0, 0, h * 0.5), scale=(0.94, 0.94, 1.1), rot=(tilt, 0, 0)))
    pose(length, raiz=dict(loc=(0, 0, 0), scale=(1 + squash * 0.6, 1 + squash * 0.6, 1 - squash), rot=(0, 0, 0)))
    clip("caminar", 0, length, loop=True)


def anim_common(walk=None, die_spin=True):
    """golpe 30..38, morir 50..70 (se encoge girando: el confeti lo pone el juego), aparecer 80..95."""
    if walk: walk()
    else: anim_walk_hop()
    rest_all(30)
    pose(32, cuerpo=dict(scale=(1.18, 1.18, 0.84), rot=(10, 0, 0)))
    pose(35, cuerpo=dict(scale=(0.92, 0.92, 1.1), rot=(-6, 0, 0)))
    rest_all(38)
    clip("golpe", 30, 8)
    # morir: se infla un instante y se desinfla aplastandose ("puf"; el polvo del color lo pone el juego)
    rest_all(50)
    pose(53, raiz=dict(scale=(1.22, 1.22, 1.22)))
    pose(58, raiz=dict(scale=(1.35, 1.35, 0.45)))
    pose(64, raiz=dict(scale=(0.6, 0.6, 0.2)))
    pose(70, raiz=dict(scale=(0.01, 0.01, 0.01)))
    clip("morir", 50, 20)
    rest_all(80)
    pose(80, raiz=dict(scale=(0.2, 0.2, 0.2), loc=(0, 0, 0.1)))
    pose(86, raiz=dict(scale=(1.2, 1.2, 0.8), loc=(0, 0, 0)))
    pose(90, raiz=dict(scale=(0.92, 0.92, 1.1)))
    rest_all(95)
    clip("aparecer", 80, 15)


# ================================================================== MONSTRUOS DE LA MAZMORRA
kit.matdef("slime_m", "slime", shine=0.9, glow=0.12)
kit.matdef("slime_osc_m", "slime_osc", shine=0.9, glow=0.1)
kit.matdef("hueso_m", "hueso", shine=0.25)
kit.matdef("ojo_brasa", "ojo_monstruo", glow=1.0)
kit.matdef("ojo_magico", "cristal_magico", glow=1.0)
kit.matdef("espectro_m", "espectro", shine=0.4, glow=0.25)
kit.matdef("murcielago_m", "murcielago", shine=0.2)
kit.matdef("dragon_m", "dragon", shine=0.45)
kit.matdef("orbe_liche", "cristal_magico", shine=0.9, glow=0.8)
kit.matdef("fuego_dragon", "fuego", glow=1.0)


def horns(c, dx, h, key_="hueso_m", tilt=25):
    for s in (-1, 1):
        cone(Vector(c) + Vector((dx * s, 0, 0)), h * 0.32, 0.0, h, key_, seg=8, rot=(0, -tilt * s, 0))


@enemy
def slime():
    """Slime: gota verde de gelatina que salta en manada (rapido y debil)."""
    bones_basic(feet=False)
    with on("cuerpo"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.2, 0.0), (0.23, 0.05), (0.21, 0.15), (0.14, 0.27), (0.05, 0.33), (0.0, 0.34)], "slime_m", seg=16, angle=70)
        ball((-0.07, -0.1, 0.24), (0.05, 0.03, 0.035), "ojo_brillo", seg=8, ring=5)                  # reflejo
        ball((0.06, 0.04, 0.1), 0.035, "slime_osc_m", seg=8, ring=5)                                   # burbuja adentro
        eyes((0, -0.165, 0.17), 0.07, 0.065)
        mouth((0, -0.2, 0.07), 0.028)
        for s in (-1, 1): ball((0.12 * s, -0.16, 0.1), (0.03, 0.01, 0.018), "rosa", seg=8, ring=4)
    anim_common(lambda: anim_walk_hop(h=0.14, length=12, squash=0.25))


@enemy
def slime_grande():
    """Slime Grande: gelatina gorda y lenta con un hueso adentro; al morir se parte en dos chiquitos."""
    kit.bone("raiz", None, (0, 0, 0)); kit.bone("cuerpo", "raiz", (0, 0, 0.0)); kit.bone("ojos", "cuerpo", (0, -0.05, 0.36))
    with on("cuerpo"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.32, 0.0), (0.35, 0.06), (0.33, 0.18), (0.25, 0.32), (0.12, 0.4), (0.0, 0.42)], "slime_m", seg=20, angle=70)
        tube((-0.12, 0.08, 0.2), (0.1, 0.12, 0.27), 0.022, 0.022, "hueso_m", seg=6)                       # hueso tragado
        for (x, y, z) in ((-0.13, 0.08, 0.2), (0.11, 0.12, 0.27)):
            ball((x, y, z), 0.035, "hueso_m", seg=8, ring=5)
        ball((-0.11, -0.2, 0.32), (0.08, 0.035, 0.05), "ojo_brillo", seg=10, ring=6, rot=(0, 20, 0))
        mouth((0, -0.33, 0.13), 0.05, open_=True)
        for s in (-1, 1): ball((0.2 * s, -0.27, 0.14), (0.045, 0.02, 0.028), "rosa", seg=8, ring=5)
    with on("ojos"):
        eyes((0, -0.25, 0.3), 0.1, 0.075)

    def walk():
        rest_all(0)
        pose(0, cuerpo=dict(scale=(1.08, 0.92, 0.95)), ojos=dict(rot=(0, 0, 0)))
        pose(10, cuerpo=dict(scale=(0.94, 1.12, 1.04)), ojos=dict(rot=(-8, 0, 6)))
        pose(20, cuerpo=dict(scale=(1.08, 0.92, 0.95)), ojos=dict(rot=(0, 0, 0)))
        pose(30, cuerpo=dict(scale=(0.94, 1.12, 1.04)), ojos=dict(rot=(-8, 0, -6)))
        pose(40, cuerpo=dict(scale=(1.08, 0.92, 0.95)), ojos=dict(rot=(0, 0, 0)))
        clip("caminar", 0, 40, loop=True)
    anim_common(walk, die_spin=False)


@enemy
def diablillo():
    """Diablillo: duende rojo con cuernitos, alitas y cola de flecha; salta de costado (esquiva el 20 %)."""
    bones_basic(extra=(("cola", "cuerpo", (0, 0.12, 0.15)),))
    with on("cuerpo"):
        C = Vector((0, 0, 0.32))
        ball(C, (0.17, 0.16, 0.16), "tela_roja", seg=14, ring=9)
        ball((0, 0, 0.14), (0.11, 0.1, 0.1), "tela_roja", seg=12, ring=7)
        ball((0, -0.07, 0.14), (0.07, 0.04, 0.07), "piel_m", seg=10, ring=6)
        horns(C + Vector((0, 0.0, 0.12)), 0.09, 0.1)
        eyes(C + Vector((0, -0.13, 0.02)), 0.06, 0.055)
        mouth(C + Vector((0, -0.155, -0.07)), 0.025)
        cone(C + Vector((0.025, -0.15, -0.085)), 0.01, 0.0, 0.02, "blanco", seg=4, rot=(180, 0, 0))
        for s in (-1, 1):
            prism((0.1 * s, 0.1, 0.36), [(0.0, 0.0), (0.16 * s, 0.1), (0.19 * s, 0.0), (0.14 * s, -0.04), (0.1 * s, -0.08)], 0.01, "tela_violeta",
                  rot=(0, 0, -20 * s))
            ball((0.13 * s, -0.02, 0.18), 0.035, "tela_roja", seg=8, ring=5)
    with on("cola"):
        tube((0, 0.1, 0.12), (0, 0.24, 0.16), 0.016, 0.012, "tela_roja", seg=6)
        tube((0, 0.24, 0.16), (0, 0.3, 0.26), 0.012, 0.01, "tela_roja", seg=6)
        cone((0, 0.31, 0.29), 0.035, 0.0, 0.06, "tela_violeta", seg=4)
    for s, n in ((1, "L"), (-1, "R")):
        with on("pata_" + n):
            ball((0.07 * s, -0.03, 0.03), (0.04, 0.06, 0.03), "pelo_negro", seg=8, ring=5)

    def walk():
        rest_all(0)
        pose(0, raiz=dict(scale=(1.15, 1.15, 0.8)), cola=dict(rot=(0, 0, 20)))
        pose(4, raiz=dict(loc=(0, 0, 0.18), scale=(0.88, 0.88, 1.2), rot=(-12, 0, 0)), cola=dict(rot=(0, 0, -20)))
        pose(8, raiz=dict(loc=(0, 0, 0.24), scale=(1, 1, 1), rot=(0, 0, 0)), cola=dict(rot=(0, 0, 20)))
        pose(12, raiz=dict(loc=(0, 0, 0.14), scale=(0.92, 0.92, 1.12), rot=(10, 0, 0)), cola=dict(rot=(0, 0, -20)))
        pose(16, raiz=dict(loc=(0, 0, 0), scale=(1.15, 1.15, 0.8), rot=(0, 0, 0)), cola=dict(rot=(0, 0, 20)))
        clip("caminar", 0, 16, loop=True)
    anim_common(walk)
    rest_all(100)
    pose(104, raiz=dict(loc=(0.18, 0, 0.12), rot=(0, -25, 0)))
    pose(110, raiz=dict(loc=(0, 0, 0), rot=(0, 0, 0)))
    clip("especial", 100, 10)


@enemy
def murcielago():
    """Murcielago: bolita peluda con alas de membrana y colmillos; vuela (solo le pegan los de alcance)."""
    kit.bone("raiz", None, (0, 0, 0)); kit.bone("cuerpo", "raiz", (0, 0, 0.0))
    kit.bone("ala_L", "cuerpo", (0.1, 0, 0.3)); kit.bone("ala_R", "cuerpo", (-0.1, 0, 0.3))
    with on("cuerpo"):
        ball((0, 0, 0.3), (0.14, 0.13, 0.14), "murcielago_m", seg=14, ring=9)
        for s in (-1, 1):
            cone((0.07 * s, 0.0, 0.45), 0.045, 0.0, 0.1, "murcielago_m", seg=6, rot=(0, -15 * s, 0))
            cone((0.07 * s, -0.01, 0.45), 0.025, 0.0, 0.07, "rosa", seg=5, rot=(0, -15 * s, 0))
        eyes((0, -0.11, 0.32), 0.055, 0.05)
        mouth((0, -0.13, 0.25), 0.02, open_=True)
        for s in (-1, 1): cone((0.014 * s, -0.135, 0.235), 0.01, 0.0, 0.025, "blanco", seg=4, rot=(180, 0, 0))
        ball((0, -0.06, 0.2), (0.06, 0.04, 0.05), "piel_o", seg=8, ring=5)
    for s, n in ((1, "L"), (-1, "R")):
        with on("ala_" + n):
            pts = [(0.0, 0.06), (0.14, 0.13), (0.3, 0.12), (0.36, 0.04), (0.3, 0.0), (0.24, -0.06), (0.18, 0.0), (0.12, -0.07), (0.06, -0.01), (0.0, -0.05)]
            prism((0.1 * s, 0.01, 0.3), [(x * s, z) for x, z in pts], 0.012, "liche")
            tube((0.1 * s, 0.0, 0.36), (0.42 * s, 0.0, 0.42), 0.008, 0.005, "murcielago_m", seg=4)

    def walk():
        rest_all(0)
        pose(0, ala_L=dict(rot=(0, -35, 0)), ala_R=dict(rot=(0, 35, 0)), raiz=dict(loc=(0, 0, 0.04)))
        pose(5, ala_L=dict(rot=(0, 30, 0)), ala_R=dict(rot=(0, -30, 0)), raiz=dict(loc=(0, 0, -0.02)))
        pose(10, ala_L=dict(rot=(0, -35, 0)), ala_R=dict(rot=(0, 35, 0)), raiz=dict(loc=(0, 0, 0.04)))
        clip("caminar", 0, 10, loop=True)
    anim_common(walk)


@enemy
def esqueleto():
    """Esqueleto con Escudo: calavera grande, huesitos y un escudo de madera (los primeros golpes rebotan)."""
    bones_basic(extra=(("escudo", "cuerpo", (0.18, -0.05, 0.3)),))
    with on("cuerpo"):
        C = Vector((0, 0, 0.5))
        ball(C, (0.17, 0.16, 0.15), "hueso_m", seg=14, ring=9)                          # calavera
        rbox(C + Vector((0, -0.04, -0.12)), (0.17, 0.12, 0.08), "hueso_m", r=0.4)       # mandibula
        for k in range(4): rbox(C + Vector((-0.045 + k * 0.03, -0.1, -0.1)), (0.018, 0.01, 0.03), "blanco", r=0.3, seg=1)
        for s in (-1, 1):   # cuencas con brasas
            ball(C + Vector((0.06 * s, -0.12, 0.02)), (0.045, 0.03, 0.05), "negro_suave", seg=10, ring=6)
            ball(C + Vector((0.06 * s, -0.145, 0.025)), 0.02, "ojo_brasa", seg=8, ring=5)
        ball(C + Vector((0, -0.145, -0.04)), (0.015, 0.01, 0.02), "negro_suave", seg=6, ring=4)
        tube((0, 0, 0.36), (0, 0, 0.12), 0.025, 0.025, "hueso_m", seg=6)               # columna
        for z in (0.32, 0.26, 0.2):                                                       # costillas
            torus((0, 0, z), 0.075 - (0.32 - z) * 0.2, 0.012, "hueso_m", seg=10, mseg=3, scale=(1, 0.8, 1), arc=0.75)
        ball((0, 0, 0.12), (0.08, 0.06, 0.04), "hueso_m", seg=8, ring=5)                  # cadera
        for s in (-1, 1): tube((0.09 * s, 0, 0.34), (0.15 * s, -0.04, 0.2), 0.016, 0.014, "hueso_m", seg=5)
        # espada oxidada
        tube((-0.15, -0.05, 0.2), (-0.15, -0.2, 0.36), 0.012, 0.012, "cuero_osc", seg=5)
        rbox((-0.15, -0.28, 0.45), (0.025, 0.012, 0.22), "hierro_m", r=0.3, seg=1, rot=(-40, 0, 0))
    with on("escudo"):
        lathe((0.2, -0.06, 0.3), [(0.0, -0.014), (0.13, -0.014), (0.135, 0.0), (0.11, 0.02), (0.0, 0.024)], "madera_mesa", seg=14,
              rot=(0, 90, 0), angle=50)
        torus((0.205, -0.06, 0.3), 0.13, 0.014, "hierro_m", rot=(0, 90, 0), seg=14, mseg=4)
        ball((0.23, -0.06, 0.3), 0.035, "hierro_m", seg=8, ring=5)
    for s, n in ((1, "L"), (-1, "R")):
        with on("pata_" + n):
            tube((0.06 * s, 0, 0.12), (0.065 * s, 0, 0.03), 0.016, 0.014, "hueso_m", seg=5)
            ball((0.065 * s, -0.03, 0.02), (0.035, 0.05, 0.02), "hueso_m", seg=8, ring=4)

    def walk():
        rest_all(0)
        for f, k in ((0, 1), (6, 0), (12, -1), (18, 0), (24, 1)):
            pose(f, pata_L=dict(loc=(0, 0, 0.04 * max(0, k))), pata_R=dict(loc=(0, 0, 0.04 * max(0, -k))), raiz=dict(rot=(0, 6 * k, 0)),
                 escudo=dict(rot=(0, 0, 5 * k)))
        clip("caminar", 0, 24, loop=True)
    anim_common(walk, die_spin=False)


@enemy
def espectro():
    """Espectro: capa con capucha flotante, sin cara: solo dos ojos que brillan (invisible a ratos)."""
    kit.bone("raiz", None, (0, 0, 0)); kit.bone("cuerpo", "raiz", (0, 0, 0.0))
    with on("cuerpo"):
        prof = [(0.0, 0.62), (0.12, 0.6), (0.2, 0.52), (0.22, 0.38), (0.25, 0.22), (0.29, 0.08), (0.27, 0.05), (0.0, 0.05)]
        lathe((0, 0, 0), prof, "espectro_m", seg=16, angle=70)
        for i in range(7):    # borde deshilachado
            a = 2 * math.pi * i / 7
            cone((math.cos(a) * 0.26, math.sin(a) * 0.26, 0.04), 0.06, 0.0, 0.1, "espectro_m", seg=6, rot=(180, 0, 0))
        h = ball((0, 0.0, 0.55), (0.18, 0.17, 0.16), "espectro_m", seg=14, ring=8)          # capucha
        ball((0, -0.12, 0.5), (0.13, 0.08, 0.11), "negro_suave", seg=12, ring=7)            # hueco oscuro
        for sx in (-1, 1):
            ball((0.05 * sx, -0.19, 0.52), (0.028, 0.012, 0.035), "ojo_magico", seg=8, ring=5)
        for s in (-1, 1): capsule((0.2 * s, -0.02, 0.4), (0.25 * s, -0.12, 0.3), 0.04, "espectro_m", seg=8, ring=2)

    def walk():
        rest_all(0)
        pose(0, raiz=dict(loc=(0, 0, 0.06), rot=(0, 5, 0)), cuerpo=dict(scale=(1, 1, 1)))
        pose(15, raiz=dict(loc=(0, 0, 0.14), rot=(0, -5, 0)), cuerpo=dict(scale=(0.96, 0.96, 1.05)))
        pose(30, raiz=dict(loc=(0, 0, 0.06), rot=(0, 5, 0)), cuerpo=dict(scale=(1, 1, 1)))
        clip("caminar", 0, 30, loop=True)
    anim_common(walk)


@enemy
def goblin():
    """Goblin Ladron: verde, orejas enormes, capucha marron y una bolsa de botin (si llega, roba chispas)."""
    bones_basic(extra=(("bolsa", "cuerpo", (0, 0.14, 0.3)),))
    with on("cuerpo"):
        C = Vector((0, 0, 0.36))
        ball(C, (0.15, 0.14, 0.14), "goblin", seg=14, ring=9)
        for s in (-1, 1): cone(C + Vector((0.16 * s, 0.0, 0.02)), 0.06, 0.0, 0.17, "goblin", seg=6, rot=(0, -80 * s, 0))
        ball(C + Vector((0, -0.14, -0.02)), (0.035, 0.03, 0.03), "goblin", seg=8, ring=5)           # nariz
        h = ball(C + Vector((0, 0.03, 0.04)), (0.16, 0.15, 0.13), "cuero", seg=12, ring=7)          # capucha
        cut = kit.ball(C + Vector((0, -0.14, -0.02)), (0.13, 0.14, 0.12), "cuero", seg=10, ring=6)
        boolean(h, cut)
        eyes(C + Vector((0, -0.12, 0.03)), 0.055, 0.045)
        mouth(C + Vector((0, -0.135, -0.07)), 0.025)
        lathe((0, 0, 0.05), [(0.0, 0.0), (0.1, 0.0), (0.12, 0.08), (0.1, 0.2), (0.0, 0.24)], "tela_verde", seg=12)
        torus((0, 0, 0.12), 0.11, 0.012, "cuero_osc", seg=12, mseg=3)
    with on("bolsa"):
        ball((0, 0.16, 0.3), (0.12, 0.1, 0.13), "tela_blanca", seg=12, ring=7)
        ball((0, 0.16, 0.44), 0.035, "cuero_osc", seg=8, ring=5)
        ball((0.05, 0.08, 0.4), 0.03, "oro_m", seg=8, ring=5)
    for s, n in ((1, "L"), (-1, "R")):
        with on("pata_" + n):
            ball((0.06 * s, -0.04, 0.03), (0.04, 0.07, 0.03), "goblin", seg=8, ring=5)

    def walk():
        rest_all(0)
        for f, k in ((0, 1), (4, 0), (8, -1), (12, 0), (16, 1)):
            pose(f, pata_L=dict(loc=(0, -0.03 * k, 0.04 * max(0, k))), pata_R=dict(loc=(0, 0.03 * k, 0.04 * max(0, -k))),
                 raiz=dict(loc=(0, 0, 0.03 * abs(k)), rot=(8, 0, 4 * k)), bolsa=dict(rot=(0, 0, -8 * k)))
        clip("caminar", 0, 16, loop=True)
    anim_common(walk)


@enemy
def arana():
    """Arana Tejedora: panza redonda violeta con 8 patitas y 4 ojos; al morir deja una telarana en un casillero."""
    bones_basic(feet=True)
    with on("cuerpo"):
        ball((0, 0.1, 0.2), (0.17, 0.2, 0.15), "arana", seg=14, ring=9)                     # panza
        for i in range(3): ball((0, 0.12 + i * 0.05 - 0.05, 0.345), (0.03, 0.02, 0.008), "tela_roja", seg=8, ring=4)
        ball((0, -0.12, 0.2), (0.12, 0.1, 0.1), "arana", seg=12, ring=8)                  # cabeza
        eyes((0, -0.21, 0.22), 0.05, 0.045)
        for s in (-1, 1): ball((0.03 * s, -0.215, 0.29), 0.018, "ojo", seg=6, ring=4)
        for s in (-1, 1): cone((0.03 * s, -0.22, 0.14), 0.012, 0.0, 0.035, "tela_blanca", seg=4, rot=(180, 0, 0))
    for s, n in ((1, "L"), (-1, "R")):
        with on("pata_" + n):
            for k in range(4):
                y = -0.12 + k * 0.08
                tube((0.08 * s, y, 0.2), (0.22 * s, y - 0.02, 0.26), 0.014, 0.012, "arana", seg=5)
                tube((0.22 * s, y - 0.02, 0.26), (0.3 * s, y - 0.04, 0.02), 0.012, 0.008, "arana", seg=5)

    def walk():
        rest_all(0)
        for f, k in ((0, 1), (4, -1), (8, 1)):
            pose(f, pata_L=dict(rot=(0, 0, 8 * k)), pata_R=dict(rot=(0, 0, 8 * k)), raiz=dict(loc=(0, 0, 0.015 * (k + 1))))
        clip("caminar", 0, 8, loop=True)
    anim_common(walk, die_spin=False)


# ------------------------------------------------------------------ jefes
@enemy
def rey_slime():
    """Jefe: Rey Slime (llama slimes a su lado). Gelatina enorme con corona, capa y cetro."""
    bones_basic(feet=False, extra=(("corona", "cuerpo", (0, 0, 0.95)),))
    with on("cuerpo"):
        lathe((0, 0, 0), [(0.0, 0.0), (0.5, 0.0), (0.55, 0.1), (0.52, 0.32), (0.42, 0.62), (0.22, 0.86), (0.0, 0.9)], "slime_m", seg=24, angle=70)
        ball((-0.2, -0.35, 0.6), (0.14, 0.06, 0.08), "ojo_brillo", seg=10, ring=6, rot=(0, 25, 0))
        for (x, y, z) in ((0.15, 0.1, 0.3), (-0.2, 0.2, 0.25), (0.05, -0.05, 0.15)):
            ball((x, y, z), 0.07, "slime_osc_m", seg=8, ring=5)
        eyes((0, -0.42, 0.5), 0.15, 0.12, lids="slime_m")
        mouth((0, -0.48, 0.28), 0.08, open_=True)
        for s in (-1, 1): ball((0.3 * s, -0.4, 0.3), (0.06, 0.02, 0.035), "rosa", seg=8, ring=5)
        cap = lathe((0, 0.25, 0.05), [(0.0, 0.0), (0.55, 0.0), (0.5, 0.3), (0.32, 0.66), (0.0, 0.7)], "tela_violeta", seg=20, angle=60)
        cap.scale = (1.0, 0.5, 1.0)
        torus((0, 0.25, 0.06), 0.55, 0.05, "tela_blanca", seg=20, mseg=6, scale=(1.0, 0.5, 1.0))
        tube((0.55, -0.1, 0.1), (0.62, -0.12, 0.85), 0.025, 0.025, "oro_m", seg=8)
        star((0.625, -0.12, 0.93), 0.09, 0.04, 0.04, "brillo_oro")
    with on("corona"):
        lathe((0, 0, 0.88), [(0.0, 0.0), (0.22, 0.0), (0.23, 0.1), (0.0, 0.1)], "oro_m", seg=16)
        for i in range(6):
            a = 2 * math.pi * i / 6
            cone((math.cos(a) * 0.22, math.sin(a) * 0.22, 1.01), 0.055, 0.0, 0.11, "oro_m", seg=5)
            ball((math.cos(a) * 0.22, math.sin(a) * 0.22, 1.08), 0.024, "tela_roja" if i % 2 else "cristal_azul", seg=6, ring=4)

    def walk():
        rest_all(0)
        pose(0, raiz=dict(scale=(1.1, 1.1, 0.9)), corona=dict(rot=(0, 0, 0)))
        pose(10, raiz=dict(loc=(0, 0, 0.12), scale=(0.95, 0.95, 1.06), rot=(0, 4, 0)), corona=dict(rot=(0, -8, 0)))
        pose(20, raiz=dict(loc=(0, 0, 0), scale=(1.1, 1.1, 0.9), rot=(0, 0, 0)), corona=dict(rot=(0, 0, 0)))
        pose(30, raiz=dict(loc=(0, 0, 0.12), scale=(0.95, 0.95, 1.06), rot=(0, -4, 0)), corona=dict(rot=(0, 8, 0)))
        pose(40, raiz=dict(loc=(0, 0, 0), scale=(1.1, 1.1, 0.9), rot=(0, 0, 0)), corona=dict(rot=(0, 0, 0)))
        clip("caminar", 0, 40, loop=True)
    anim_common(walk, die_spin=False)
    rest_all(100)
    pose(108, raiz=dict(scale=(1.15, 1.15, 0.85)), corona=dict(loc=(0, 0, -0.03)))
    pose(116, raiz=dict(loc=(0, 0, 0.25), scale=(0.9, 0.9, 1.15)), corona=dict(loc=(0, 0, 0.08), rot=(0, 15, 0)))
    pose(124, raiz=dict(loc=(0, 0, 0), scale=(1.2, 1.2, 0.8)), corona=dict(loc=(0, 0, 0), rot=(0, 0, 0)))
    rest_all(130)
    clip("especial", 100, 30, ev=[16])


@enemy
def liche():
    """Jefe: Liche (maldice casilleros: ese heroe no ataca 4 s). Calavera con capucha, tunica violeta y baston con orbe."""
    bones_basic(extra=(("cabeza", "cuerpo", (0, 0, 0.82)), ("brazo_L", "cuerpo", (0.24, 0, 0.72)), ("brazo_R", "cuerpo", (-0.24, 0, 0.72))))
    with on("cuerpo"):
        lathe((0, 0, 0.0), [(0.0, 0.05), (0.36, 0.05), (0.38, 0.1), (0.3, 0.4), (0.2, 0.7), (0.12, 0.82), (0.0, 0.84)], "liche", seg=20, angle=60)
        for i in range(9):
            a = 2 * math.pi * i / 9
            cone((math.cos(a) * 0.36, math.sin(a) * 0.36, 0.06), 0.07, 0.0, 0.1, "liche", seg=6, rot=(180, 0, 0))
        rbox((0, -0.24, 0.45), (0.16, 0.04, 0.42), "tela_negra", r=0.3)                       # faja
        star((0, -0.27, 0.6), 0.05, 0.024, 0.015, "oro_m")
        torus((0, 0, 0.78), 0.2, 0.04, "oro_m", seg=20, mseg=5)                               # cuello dorado
    with on("cabeza"):
        C = Vector((0, 0, 1.0))
        h = ball(C, (0.25, 0.24, 0.24), "liche", seg=16, ring=9)
        cut = kit.ball(C + Vector((0, -0.2, -0.04)), (0.19, 0.2, 0.19), "liche", seg=12, ring=7)
        boolean(h, cut)
        ball(C + Vector((0, -0.04, -0.03)), (0.17, 0.16, 0.16), "hueso_m", seg=14, ring=9)
        for s in (-1, 1):
            ball(C + Vector((0.065 * s, -0.17, 0.0)), (0.05, 0.03, 0.055), "negro_suave", seg=10, ring=6)
            ball(C + Vector((0.065 * s, -0.195, 0.005)), 0.025, "ojo_magico", seg=8, ring=5)
        for k in range(5): rbox(C + Vector((-0.06 + k * 0.03, -0.17, -0.14)), (0.02, 0.01, 0.035), "hueso_m", r=0.3, seg=1)
        lathe(C + Vector((0, 0, 0.2)), [(0.0, 0.0), (0.14, 0.0), (0.15, 0.06), (0.0, 0.06)], "oro_m", seg=12)
        for i in range(5):
            a = 2 * math.pi * i / 5
            cone(C + Vector((math.cos(a) * 0.14, math.sin(a) * 0.14, 0.31)), 0.035, 0.0, 0.1, "oro_m", seg=4)
    with on("brazo_L"):
        capsule((0.24, 0, 0.72), (0.3, -0.12, 0.5), 0.06, "liche", seg=8, ring=2)
        ball((0.31, -0.14, 0.47), 0.045, "hueso_m", seg=8, ring=5)
    with on("brazo_R"):
        capsule((-0.24, 0, 0.72), (-0.3, -0.12, 0.52), 0.06, "liche", seg=8, ring=2)
        tube((-0.32, -0.15, 0.1), (-0.32, -0.15, 1.15), 0.025, 0.02, "madera_mesa", seg=8)
        torus((-0.32, -0.15, 1.16), 0.08, 0.016, "oro_m", seg=12, mseg=4)
        ball((-0.32, -0.15, 1.24), 0.08, "orbe_liche", seg=12, ring=8)

    def walk():
        rest_all(0)
        pose(0, raiz=dict(loc=(0, 0, 0.04), rot=(0, 4, 0)), brazo_R=dict(rot=(0, 0, 0)))
        pose(24, raiz=dict(loc=(0, 0, 0.1), rot=(0, -4, 0)), brazo_R=dict(rot=(-8, 0, 0)))
        pose(48, raiz=dict(loc=(0, 0, 0.04), rot=(0, 4, 0)), brazo_R=dict(rot=(0, 0, 0)))
        clip("caminar", 0, 48, loop=True)
    anim_common(walk, die_spin=False)
    rest_all(100)
    pose(108, brazo_R=dict(rot=(-50, 0, 0)), cabeza=dict(rot=(-10, 0, 0)), raiz=dict(scale=(0.95, 0.95, 1.08)))
    pose(114, brazo_R=dict(rot=(-90, 0, 0)), raiz=dict(scale=(1.05, 1.05, 0.95)))
    rest_all(124)
    clip("especial", 100, 24, ev=[14])


@enemy
def ogro():
    """Jefe: Ogro del Porton (rompe el porton y salen 5). Grandote verde con colmillos, panza y garrote."""
    bones_basic(extra=(("brazo_L", "cuerpo", (0.32, 0, 0.8)), ("brazo_R", "cuerpo", (-0.32, 0, 0.8)), ("cabeza", "cuerpo", (0, 0, 0.92))))
    with on("cuerpo"):
        ball((0, 0, 0.55), (0.38, 0.32, 0.36), "ogro", seg=18, ring=11)                          # cuerpo
        ball((0, -0.17, 0.48), (0.26, 0.18, 0.24), "piel_m", seg=14, ring=9)                     # panza
        lathe((0, 0, 0.22), [(0.0, 0.0), (0.36, 0.0), (0.38, 0.12), (0.33, 0.24), (0.0, 0.24)], "cuero", seg=16)   # taparrabos
        torus((0, 0, 0.42), 0.36, 0.03, "cuero_osc", seg=18, mseg=4)
        rbox((0, -0.35, 0.42), (0.12, 0.04, 0.08), "hierro_m", r=0.3)
    with on("cabeza"):
        C = Vector((0, -0.06, 1.0))
        ball(C, (0.24, 0.22, 0.2), "ogro", seg=16, ring=10)
        eyes(C + Vector((0, -0.18, 0.04)), 0.09, 0.07, lids="ogro")
        mouth(C + Vector((0, -0.21, -0.09)), 0.08)
        for s in (-1, 1):
            cone(C + Vector((0.08 * s, -0.2, -0.08)), 0.025, 0.0, 0.07, "hueso_m", seg=6)
            ball(C + Vector((0.23 * s, 0.0, 0.02)), (0.05, 0.03, 0.07), "ogro", seg=8, ring=5)
        ball(C + Vector((0, -0.21, 0.0)), (0.05, 0.04, 0.04), "ogro", seg=8, ring=5)
    with on("brazo_L"):
        capsule((0.34, 0, 0.8), (0.42, -0.06, 0.45), 0.1, "ogro", seg=10, ring=3)
        ball((0.43, -0.08, 0.4), 0.1, "ogro", seg=10, ring=6)
    with on("brazo_R"):
        capsule((-0.34, 0, 0.8), (-0.42, -0.06, 0.45), 0.1, "ogro", seg=10, ring=3)
        ball((-0.43, -0.08, 0.4), 0.1, "ogro", seg=10, ring=6)
        tube((-0.45, -0.12, 0.35), (-0.45, -0.1, 1.15), 0.05, 0.11, "madera_mesa", seg=10)    # garrote
        for k in range(3): cone((-0.45 + 0.1 * math.cos(k * 2.1), -0.1 + 0.1 * math.sin(k * 2.1), 1.0 + k * 0.05), 0.025, 0.0, 0.06, "hierro_m", seg=5,
                                rot=(0, 90, math.degrees(k * 2.1)))
    for s, n in ((1, "L"), (-1, "R")):
        with on("pata_" + n):
            capsule((0.16 * s, 0, 0.22), (0.17 * s, -0.02, 0.08), 0.1, "ogro", seg=10, ring=2)
            ball((0.17 * s, -0.08, 0.05), (0.11, 0.15, 0.06), "cuero_osc", seg=10, ring=6)

    def walk():
        rest_all(0)
        for f, k in ((0, 1), (12, 0), (24, -1), (36, 0), (48, 1)):
            pose(f, pata_L=dict(loc=(0, 0, 0.06 * max(0, k))), pata_R=dict(loc=(0, 0, 0.06 * max(0, -k))), raiz=dict(rot=(0, 0, 4 * k)),
                 brazo_R=dict(rot=(-10 * k, 0, 0)), brazo_L=dict(rot=(10 * k, 0, 0)))
        clip("caminar", 0, 48, loop=True)
    anim_common(walk, die_spin=False)
    rest_all(100)
    pose(106, brazo_R=dict(rot=(0, 140, 0)), raiz=dict(scale=(0.95, 0.95, 1.1)))
    pose(112, brazo_R=dict(rot=(-80, 0, 0)), raiz=dict(scale=(1.15, 1.15, 0.85)))
    rest_all(134)
    clip("especial", 100, 34, ev=[12])


@enemy
def dragon():
    """Jefe: Dragon (escupe fuego que frena a un heroe). Rojo regordete, alas, cuernos y cola larga."""
    names = [("cola%d" % i, "cuerpo" if i == 0 else "cola%d" % (i - 1), (0, 0.32 + i * 0.16, 0.25 - i * 0.03)) for i in range(4)]
    bones_basic(extra=[("cabeza", "cuerpo", (0, -0.2, 0.45)), ("ala_L", "cuerpo", (0.2, 0.05, 0.55)), ("ala_R", "cuerpo", (-0.2, 0.05, 0.55))] + names)
    with on("cuerpo"):
        ball((0, 0.05, 0.38), (0.3, 0.34, 0.3), "dragon_m", seg=16, ring=10)
        ball((0, -0.12, 0.33), (0.2, 0.14, 0.22), "tela_amarilla", seg=12, ring=8)              # panza
        for k in range(4): torus((0, -0.2, 0.2 + k * 0.07), 0.14 - abs(k - 1.5) * 0.02, 0.008, "bronce_cl", rot=(90, 0, 0), seg=10, mseg=3, arc=0.5)
        for i in range(4): cone((0, 0.05 + i * 0.1, 0.68 - i * 0.04), 0.05, 0.0, 0.09, "tela_amarilla", seg=5, rot=(-30, 0, 0))   # cresta
    with on("cabeza"):
        C = Vector((0, -0.28, 0.64))
        ball(C, (0.22, 0.2, 0.19), "dragon_m", seg=14, ring=9)
        # hocico largo de reptil (no redondo: si no parece un chancho), fosas arriba y colmillos
        ball(C + Vector((0, -0.21, -0.06)), (0.115, 0.17, 0.085), "dragon_m", seg=12, ring=7)
        ball(C + Vector((0, -0.17, -0.115)), (0.1, 0.13, 0.04), "tela_amarilla", seg=10, ring=5)  # mandibula clara
        for s in (-1, 1):
            ball(C + Vector((0.042 * s, -0.33, 0.0)), (0.018, 0.012, 0.008), "negro_suave", seg=6, ring=4)
            cone(C + Vector((0.055 * s, -0.31, -0.12)), 0.016, 0.0, 0.045, "hueso_m", seg=5, rot=(180, 0, 0))
            ball(C + Vector((0.085 * s, -0.05, 0.13)), (0.06, 0.05, 0.025), "dragon_m", seg=8, ring=5)  # ceja
        eyes(C + Vector((0, -0.12, 0.09)), 0.09, 0.07)
        horns(C + Vector((0, 0.05, 0.14)), 0.1, 0.16, "hueso_m", tilt=30)
        mouth(C + Vector((0, -0.36, -0.09)), 0.05)
    for s, n in ((1, "L"), (-1, "R")):
        with on("ala_" + n):
            pts = [(0.0, 0.0), (0.25, 0.3), (0.5, 0.35), (0.45, 0.15), (0.38, 0.05), (0.3, 0.1), (0.2, -0.02), (0.1, 0.0)]
            prism((0.2 * s, 0.1, 0.55), [(x * s, z) for x, z in pts], 0.02, "tela_roja")
            tube((0.2 * s, 0.1, 0.55), (0.7 * s, 0.1, 0.9), 0.02, 0.012, "dragon_m", seg=5)
    for i in range(4):
        with on("cola%d" % i):
            y0 = 0.32 + i * 0.16; z0 = 0.25 - i * 0.03; r = 0.12 - i * 0.025
            tube((0, y0 - 0.04, z0 + 0.02), (0, y0 + 0.16, z0 - 0.01), r, r * 0.8, "dragon_m", seg=8)
            if i == 3: cone((0, y0 + 0.2, z0), 0.06, 0.0, 0.1, "tela_amarilla", seg=4, rot=(-90, 0, 0))
    for s, n in ((1, "L"), (-1, "R")):
        with on("pata_" + n):
            ball((0.16 * s, -0.06, 0.08), (0.09, 0.12, 0.08), "dragon_m", seg=10, ring=6)

    def walk():
        rest_all(0)
        for f, k in ((0, 1), (12, -1), (24, 1), (36, -1), (48, 1)):
            pose(f, ala_L=dict(rot=(0, -15 * k, 0)), ala_R=dict(rot=(0, 15 * k, 0)), raiz=dict(loc=(0, 0, 0.04 + 0.04 * k)),
                 **{"cola%d" % i: dict(rot=(0, 0, 10 * k * (i + 1) / 2)) for i in range(4)})
        clip("caminar", 0, 48, loop=True)
    anim_common(walk, die_spin=False)
    rest_all(100)
    pose(106, cabeza=dict(rot=(-25, 0, 0)), raiz=dict(scale=(0.95, 0.95, 1.08)))
    pose(112, cabeza=dict(rot=(20, 0, 0)), raiz=dict(scale=(1.08, 1.08, 0.94)))
    rest_all(122)
    clip("especial", 100, 22, ev=[12])


def produce(eid, export=True):
    kit.reset()
    ENEMIES[eid]()
    if export:
        t = kit.export_model("e_" + eid)
        kit.export_anims("e_%s_anim" % eid)
        return t
    return 0


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    a = ap.parse_args(argv)
    for e in [x for x in a.only.split(",") if x] or list(ENEMIES):
        print("TRIS", e, produce(e))
