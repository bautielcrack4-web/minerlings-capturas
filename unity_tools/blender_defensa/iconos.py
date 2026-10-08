"""Fortin de Juguete: retratos e iconos renderizados en Blender (nada dibujado a mano ni generado con IA).

Uso: blender -b -P iconos.py -- [retratos|iconos|todo] [--only soldadito,...] [--samples 32]
- Retratos: busto del juguete en nivel 3 (el pintado y detallado), camara de 3/4, luz de estudio calida, borde de luz
  del color de su rareza y fondo transparente, 512x512 -> Resources/UI/retratos/<id>.png
- Iconos (256x256, transparentes): chispa, vida (bombillita), gema, moneda, energia, trofeo, cuerno, candado, pausa,
  tachito, estrella, carta, cofres y pestanas -> Resources/UI/iconos/<nombre>.png
"""
import sys, os, math, argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, heroes, mazmorra, enemigos, utileria
from kit import ball, rbox, tube, capsule, torus, cone, star, prism, lathe, on, ring, boolean
from mathutils import Vector, Matrix

UI = os.path.join(kit.UNITY, "Assets", "Resources", "UI")
RAR_COL = {0: "b9c4dd", 1: "4aa3ff", 2: "b06ef0", 3: "ffc93a"}

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("what", nargs="?", default="todo")
ap.add_argument("--only", default="")
ap.add_argument("--samples", type=int, default=32)
a = ap.parse_args(argv)

RARITY = {"lancero": 0, "arquera": 0, "mago": 0, "enano": 0, "chaman": 1, "bardo": 1, "barbaro": 1, "picara": 1, "mimico": 1,
          "artillero": 2, "monja": 2, "paladin": 2, "hada": 3}


def portrait_lights(rim_hex):
    kit.light("llave", "AREA", (1.6, -2.2, 2.4), 260, "ffe2b0", size=1.6, target=(0, 0, 0.6))
    kit.light("relleno", "AREA", (-2.2, -1.4, 1.0), 70, "c9bcff", size=2.0, target=(0, 0, 0.6))
    kit.light("borde1", "AREA", (-1.4, 1.8, 1.6), 380, rim_hex, size=1.0, target=(0, 0, 0.6))
    kit.light("borde2", "AREA", (1.5, 1.6, 1.2), 260, rim_hex, size=1.0, target=(0, 0, 0.6))
    w = kit.night_world(0.6, "6f7690")


def clear_scene():
    import bpy
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    kit.BONES.clear(); kit.BONE_ORDER.clear(); kit.PARTS.clear(); kit.CLIPS.clear()


def portrait(hid, lv=3):
    kit.reset()
    heroes.skeleton(); heroes.build(hid, lv)
    kit.prep_render()
    # pose de retrato: cabeza un poco inclinada (sin esqueleto animado: se rota el grupo entero)
    portrait_lights("#" + RAR_COL[RARITY.get(hid, 0)] if False else RAR_COL[RARITY.get(hid, 0)])
    tgt = Vector((0, 0, 0.7 if hid != "mimico" else 0.5))
    d = Vector((math.sin(math.radians(28)) * math.cos(math.radians(12)), -math.cos(math.radians(28)) * math.cos(math.radians(12)), math.sin(math.radians(12))))
    kit.camera(tgt + d * 2.7, tgt, lens=85)
    os.makedirs(os.path.join(UI, "retratos"), exist_ok=True)
    kit.render(os.path.join(UI, "retratos", hid + ".png"), 512, 512, a.samples, transparent=True)


def portrait_enemy(eid):
    kit.reset()
    enemigos.ENEMIES[eid]()
    kit.prep_render()
    portrait_lights("b6ff5a")
    zmin, zmax, c = kit.bounds()
    tgt = Vector((c.x, c.y, (zmin + zmax) / 2))
    d = Vector((math.sin(math.radians(25)), -math.cos(math.radians(25)), math.sin(math.radians(16)))).normalized()
    kit.camera(tgt + d * (zmax - zmin) * 4.2, tgt, lens=85)
    os.makedirs(os.path.join(UI, "retratos"), exist_ok=True)
    kit.render(os.path.join(UI, "retratos", "e_" + eid + ".png"), 512, 512, a.samples, transparent=True)


# ------------------------------------------------------------------ iconos
def i_chispa():
    prism((0, 0, 0), [(0.08, 0.5), (-0.28, -0.04), (-0.04, -0.04), (-0.12, -0.5), (0.28, 0.08), (0.04, 0.08)], 0.14, "amarillo", bevel=0.035)
    p = kit.PARTS["raiz"][-1]; p["mk"] = "brillo_oro"; p.data.materials[0] = kit.blmat("brillo_oro")


def i_vida():
    # el Cristal del Corazon (la vida de la partida)
    lathe((0, 0, -0.45), [(0.0, 0.0), (0.26, 0.0), (0.27, 0.05), (0.2, 0.1), (0.16, 0.2), (0.0, 0.2)], "piedra_cl", seg=8, smooth=False)
    torus((0, 0, -0.26), 0.16, 0.025, "oro_m", seg=18, mseg=4)
    lathe((0, 0, -0.25), [(0.0, 0.0), (0.2, 0.25), (0.2, 0.42), (0.0, 0.78)], "cristal_corazon", seg=6, smooth=False)
    lathe((0, 0, -0.2), [(0.0, 0.08), (0.1, 0.25), (0.1, 0.38), (0.0, 0.6)], "ojo_brillo", seg=6, smooth=False)


def i_gema():
    lathe((0, 0, -0.35), [(0.0, 0.0), (0.38, 0.45), (0.3, 0.6), (0.0, 0.63)], "magenta", seg=8, smooth=False)
    p = kit.PARTS["raiz"][-1]; p["mk"] = "arcoiris_suave"; p.data.materials[0] = kit.blmat("arcoiris_suave")


def i_moneda():
    tube((0, -0.06, 0), (0, 0.06, 0), 0.42, 0.42, "oro_m", seg=32, bevel=0.03)
    star((0, -0.075, 0), 0.22, 0.1, 0.03, "dorado")


def i_energia():
    rbox((0, 0, -0.03), (0.42, 0.24, 0.7), "turquesa", r=0.25)
    rbox((0, 0, 0.36), (0.18, 0.14, 0.08), "plata_m", r=0.3)
    prism((0, -0.13, -0.03), [(0.04, 0.24), (-0.12, -0.02), (-0.01, -0.02), (-0.05, -0.24), (0.12, 0.04), (0.01, 0.04)], 0.04, "amarillo", bevel=0.01)


def i_trofeo():
    lathe((0, 0, -0.45), [(0.0, 0.0), (0.26, 0.0), (0.26, 0.1), (0.1, 0.14), (0.06, 0.3), (0.06, 0.34), (0.3, 0.42), (0.36, 0.8),
                          (0.32, 0.82), (0.26, 0.48), (0.0, 0.46)], "oro_m", seg=24)
    for s in (-1, 1): torus((0.36 * s, 0, 0.12), 0.12, 0.03, "oro_m", rot=(90, 0, 0), seg=14, mseg=5, arc=0.6)
    for s in (-1, 1): kit.PARTS["raiz"][-1 if s == 1 else -2].rotation_euler = (math.radians(90), 0, math.radians(-110 if s > 0 else 70))
    star((0, -0.2, 0.12), 0.1, 0.045, 0.03, "rojo")


def i_cuerno():
    pts = []
    for i in range(9):
        t = i / 8
        pts.append(((0.4 - 0.75 * t) , 0.35 * math.sin(t * 2.4) - 0.1, 0.03 + 0.17 * t))
    for i in range(len(pts) - 1):
        (x0, z0, r0), (x1, z1, r1) = pts[i], pts[i + 1]
        tube((x0, 0, z0), (x1, 0, z1), r0, r1, "oro_m", seg=14, cap=(i == len(pts) - 2))
    for (x, z, r) in ((0.15, 0.1, 0.08), (-0.18, 0.15, 0.12)):
        torus((x, 0, z), r, 0.025, "rojo", rot=(0, 75, 0), seg=14, mseg=4)


def i_candado():
    rbox((0, 0, -0.15), (0.56, 0.24, 0.46), "dorado", r=0.25)
    torus((0, 0, 0.12), 0.18, 0.05, "plata_m", rot=(90, 0, 0), seg=18, mseg=6, arc=0.5)
    ball((0, -0.13, -0.1), (0.06, 0.03, 0.06), "contorno", seg=10, ring=6)
    rbox((0, -0.13, -0.2), (0.04, 0.02, 0.1), "contorno", r=0.4)


def i_pausa():
    for x in (-0.15, 0.15): rbox((x, 0, 0), (0.18, 0.18, 0.62), "crema", r=0.45)


def i_tachito():
    lathe((0, 0, -0.42), [(0.0, 0.0), (0.26, 0.0), (0.33, 0.66), (0.36, 0.7), (0.0, 0.7)], "violeta", seg=20)
    lathe((0, 0, 0.3), [(0.0, 0.0), (0.39, 0.0), (0.38, 0.06), (0.0, 0.08)], "violeta_osc", seg=20)
    tube((0, 0, 0.38), (0, 0, 0.46), 0.03, 0.03, "lila", seg=8)
    torus((0, 0, 0.47), 0.08, 0.025, "lila", rot=(90, 0, 0), seg=12, mseg=4, arc=0.5)
    for a in (-0.2, 0, 0.2):
        rbox((math.sin(a) * 0.3, -math.cos(a) * 0.3, -0.1), (0.04, 0.02, 0.45), "lila", r=0.4, rot=(0, 0, math.degrees(-a)))


def i_estrella():
    star((0, 0, 0), 0.48, 0.21, 0.16, "brillo_oro", bevel=0.05)


def i_carta():
    rbox((0, 0, 0), (0.5, 0.06, 0.7), "violeta", r=0.2, rot=(0, 10, 0))
    rbox((0, -0.035, 0.02), (0.4, 0.01, 0.58), "lila", r=0.3, rot=(0, 10, 0))
    star((0.01, -0.05, 0.02), 0.13, 0.06, 0.02, "dorado", rot=(0, 10, 0))


def chest(kind):
    """Cofre de juguete (madera, plata, oro, legendario): cuerpo + tapa abovedada (la tapa es otro hueso en el juego)."""
    body = {"madera": "madera", "plata": "lavanda_m", "oro": "rojo", "legendario": "violeta"}[kind]
    band = {"madera": "bronce_m", "plata": "plata_m", "oro": "oro_m", "legendario": "arcoiris"}[kind]
    with on("raiz"):
        rbox((0, 0, 0.22), (0.8, 0.56, 0.44), body, r=0.12)
        for x in (-0.28, 0.28): rbox((x, 0, 0.22), (0.08, 0.6, 0.46), band, r=0.3)
        rbox((0, -0.29, 0.38), (0.16, 0.06, 0.18), band, r=0.3)
        ball((0, -0.32, 0.36), 0.03, "contorno", seg=8, ring=5)
    with on("tapa"):
        tube((-0.415, 0, 0.44), (0.415, 0, 0.44), 0.285, 0.285, body, seg=20, bevel=0.0001)
        for x in (-0.28, 0.28):
            torus((x, 0.0, 0.44), 0.29, 0.04, band, rot=(90, 0, 90), seg=16, mseg=4, arc=0.5)
        if kind == "legendario":
            star((0, -0.27, 0.6), 0.1, 0.045, 0.03, "brillo_oro")


def i_cofre(kind):
    def f():
        chest(kind)
    return f


def i_tienda():
    rbox((0, 0, -0.2), (0.62, 0.4, 0.5), "crema", r=0.12)
    for i in range(5):
        x = -0.3 + i * 0.15
        rbox((x, -0.16, 0.18), (0.15, 0.2, 0.08), "rojo" if i % 2 == 0 else "blanco", r=0.3, rot=(-20, 0, 0))
    rbox((0, -0.21, -0.25), (0.2, 0.02, 0.3), "turquesa", r=0.3)


def i_juguetes():
    # heroes: yelmo con cresta y una espada cruzada detras
    rbox((0.0, 0.1, 0.0), (0.06, 0.03, 0.8), "acero_m", r=0.3, rot=(0, 40, 0))
    rbox((-0.2, 0.08, -0.2), (0.22, 0.05, 0.05), "oro_m", r=0.3, rot=(0, 40, 0))
    h = ball((0, 0, 0.0), (0.32, 0.3, 0.3), "acero_m", seg=18, ring=10)
    cut = kit.ball((0, -0.26, -0.12), (0.26, 0.2, 0.22), "acero_m", seg=14, ring=8)
    boolean(h, cut)
    for i in range(5):
        t = (i - 2) / 2
        ball((0, 0.02 + t * 0.2, 0.3 - abs(t) ** 2 * 0.1), (0.06, 0.1, 0.09), "tela_roja", seg=8, ring=5)
    torus((0, 0, -0.02), 0.31, 0.03, "oro_m", seg=22, mseg=5)


def i_campana():
    tube((-0.25, 0, -0.45), (-0.25, 0, 0.45), 0.035, 0.035, "marron", seg=8)
    prism((0.03, 0, 0.27), [(-0.25, -0.18), (0.32, -0.04), (-0.25, 0.18)], 0.03, "magenta", bevel=0.01)
    ball((-0.25, 0, 0.47), 0.05, "dorado", seg=10, ring=6)
    lathe((-0.25, 0, -0.5), [(0.0, 0.0), (0.2, 0.0), (0.18, 0.06), (0.0, 0.08)], "verde", seg=16)


def i_eventos():
    ball((0, 0, 0), 0.4, "dorado", seg=20, ring=14)
    ball((0.16, -0.12, 0.1), 0.34, "noche", seg=18, ring=12)
    for (x, z) in ((-0.42, 0.35), (0.36, 0.36), (-0.36, -0.36)):
        star((x, -0.1, z), 0.09, 0.04, 0.03, "brillo_oro")


def i_club():
    lathe((0, 0, -0.4), [(0.0, 0.0), (0.32, 0.25), (0.36, 0.7), (0.0, 0.8)], "azul", seg=4, smooth=False, rot=(0, 0, 45))
    p = kit.PARTS["raiz"][-1]; p.scale = (1.0, 0.25, 1.0)
    star((0, -0.12, 0.0), 0.16, 0.07, 0.04, "dorado")


def i_diana():
    for i, (r, k) in enumerate(((0.46, "rojo"), (0.34, "blanco"), (0.22, "rojo"), (0.1, "blanco"))):
        tube((0, -0.02 * i, 0), (0, -0.02 * i - 0.06, 0), r, r, k, seg=28, bevel=0.0001)
    tube((0.6, -0.5, 0.3), (0.02, -0.12, 0.02), 0.02, 0.02, "marron", seg=6)
    cone((0.02, -0.11, 0.02), 0.05, 0.0, 0.1, "plata_m", seg=6, rot=(0, 0, 0))


def i_corona():
    lathe((0, 0, -0.25), [(0.0, 0.0), (0.4, 0.0), (0.42, 0.2), (0.0, 0.2)], "oro_m", seg=20)
    for i in range(5):
        a = 2 * math.pi * i / 5 - math.pi / 2
        cone((math.cos(a) * 0.38, math.sin(a) * 0.38, 0.08), 0.12, 0.0, 0.3, "oro_m", seg=6)
        ball((math.cos(a) * 0.38, math.sin(a) * 0.38, 0.25), 0.05, "rojo" if i % 2 else "turquesa", seg=8, ring=5)


def i_espada():
    prism((0, 0, 0.12), [(-0.06, -0.25), (0.06, -0.25), (0.06, 0.4), (0.0, 0.52), (-0.06, 0.4)], 0.05, "plata_m", bevel=0.015, rot=(0, -35, 0))
    rbox((0.14, 0, -0.08), (0.36, 0.08, 0.07), "dorado", r=0.3, rot=(0, -35, 0))
    tube((0.2, 0, -0.17), (0.32, 0, -0.34), 0.035, 0.035, "marron", seg=8)
    ball((0.34, 0, -0.37), 0.05, "dorado", seg=8, ring=5)


def i_reloj():
    tube((0, 0.04, 0), (0, -0.06, 0), 0.38, 0.38, "rojo", seg=28, bevel=0.0001)
    tube((0, -0.06, 0), (0, -0.08, 0), 0.32, 0.32, "crema", seg=28, bevel=0.0001)
    for s_ in (-1, 1):
        ball((0.26 * s_, 0, 0.36), (0.13, 0.08, 0.1), "dorado", seg=10, ring=6)
    rbox((0.0, -0.09, 0.1), (0.03, 0.01, 0.22), "contorno", r=0.4)
    rbox((0.08, -0.09, 0.0), (0.16, 0.01, 0.03), "contorno", r=0.4)


def i_mano():
    """Manito fantasma del tutorial: guante blanco de juguete senalando hacia arriba."""
    ball((0, 0, -0.1), (0.24, 0.14, 0.24), "blanco", seg=16, ring=10)                     # palma
    ball((-0.07, -0.02, 0.22), (0.13, 0.12, 0.3), "blanco", seg=14, ring=10)              # indice largo
    for x, z in ((0.07, 0.07), (0.18, 0.02)):
        ball((x, -0.05, z), (0.08, 0.08, 0.11), "blanco", seg=12, ring=8)                 # dedos doblados
    ball((-0.25, -0.02, -0.05), (0.08, 0.08, 0.13), "blanco", seg=12, ring=8, rot=(0, -35, 0))   # pulgar
    torus((0, 0, -0.32), 0.2, 0.06, "lila", seg=16, mseg=6)                              # puno del guante


def i_escudo():
    # escudito (fondo del numero de nivel de experiencia)
    pts = [(-0.4, 0.42), (0.4, 0.42), (0.4, 0.02), (0.0, -0.48), (-0.4, 0.02)]
    prism((0, 0, 0), pts, 0.14, "violeta", bevel=0.05)
    prism((0, -0.075, 0.0), [(x * 0.82, z * 0.82 + 0.0) for x, z in pts], 0.02, "violeta_osc", bevel=0.01)


def i_subir():
    # flecha hacia arriba (mejorar el tipo)
    prism((0, 0, 0), [(0.0, 0.5), (0.36, 0.08), (0.14, 0.08), (0.14, -0.42), (-0.14, -0.42), (-0.14, 0.08), (-0.36, 0.08)], 0.14, "lima", bevel=0.045)


def i_jugar():
    prism((0, 0, 0), [(-0.25, -0.35), (0.38, 0.0), (-0.25, 0.35)], 0.12, "crema", bevel=0.05)


ICONS = dict(chispa=i_chispa, vida=i_vida, gema=i_gema, moneda=i_moneda, energia=i_energia, trofeo=i_trofeo, cuerno=i_cuerno,
             candado=i_candado, pausa=i_pausa, tachito=i_tachito, estrella=i_estrella, carta=i_carta,
             cofre_madera=i_cofre("madera"), cofre_plata=i_cofre("plata"), cofre_oro=i_cofre("oro"), cofre_legendario=i_cofre("legendario"),
             tienda=i_tienda, juguetes=i_juguetes, campana=i_campana, eventos=i_eventos, club=i_club, jugar=i_jugar,
             diana=i_diana, corona=i_corona, espada=i_espada, reloj=i_reloj, mano=i_mano, escudo=i_escudo, subir=i_subir)


def icon(name):
    kit.reset()
    kit.bone("raiz", None, (0, 0, 0))
    if name.startswith("cofre"): kit.bone("tapa", "raiz", (0, 0.28, 0.44))
    with on("raiz"): ICONS[name]()
    kit.prep_render()
    import bpy
    portrait_lights("b9a8ff")
    zmin, zmax, c = kit.bounds()
    ctr = Vector((c.x, c.y, (zmin + zmax) / 2))
    flat = name in ("pausa", "jugar", "chispa", "estrella", "carta", "diana", "reloj", "espada", "mano", "escudo", "subir")
    el = 6 if flat else 22
    d = Vector((math.sin(math.radians(0 if flat else 24)) * math.cos(math.radians(el)), -math.cos(math.radians(0 if flat else 24)) * math.cos(math.radians(el)), math.sin(math.radians(el))))
    size = max(zmax - zmin, 0.6) * 1.35
    kit.camera(ctr + d * 20, ctr, ortho=max(1.05, size))
    os.makedirs(os.path.join(UI, "iconos"), exist_ok=True)
    kit.render(os.path.join(UI, "iconos", name + ".png"), 256, 256, a.samples, transparent=True)


def export_chests():
    for k in ("madera", "plata", "oro", "legendario"):
        kit.reset()
        kit.bone("raiz", None, (0, 0, 0)); kit.bone("tapa", "raiz", (0, 0.28, 0.44))
        chest(k)
        kit.export_model("u_cofre_" + k)


if __name__ == "__main__":
    only = [x for x in a.only.split(",") if x]
    if a.what in ("retratos", "todo"):
        for h in (only or list(heroes.HEROES)):
            if h in heroes.HEROES: portrait(h)
        for e in (only or ["slime", "slime_grande", "diablillo", "murcielago", "esqueleto", "espectro", "goblin", "arana", "rey_slime", "liche", "ogro", "dragon"]):
            if e in enemigos.ENEMIES: portrait_enemy(e)
    if a.what in ("iconos", "todo"):
        for n in (only or list(ICONS)):
            if n in ICONS: icon(n)
        export_chests()
