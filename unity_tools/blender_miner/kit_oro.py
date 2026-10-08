"""Kit de habitacion ORO (Tesoreria, Boveda, Comedor, Campana de fiesta): muro crema de sillar liso con columnas,
cornisa dorada con dentellones, nicho de monedas y lingotes, rombos dorados; ventana de arco con vidrio azul en cruz;
puerta de madera con herrajes dorados; techo de tejas doradas con cupula central y cupulitas en las esquinas (las
cupulas de la lamina de la pared van en el techo: la pared no pasa de 1.25). Calcado de
D:/rtmp/trellis_job/in/habitaciones/oro_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_oro.py -- outdir [--preview png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *
import kit_lib as K

PAL = {
    "crema": hexc("#efe2c2"), "crema_b": hexc("#e1d1a8"), "junta": hexc("#c9b789"), "oscuro": hexc("#4a3208"),
    "oro": hexc("#f2b51d"), "oro_b": hexc("#c98b0b"), "oro_osc": hexc("#8a5a06"), "madera": hexc("#7a4a26"),
    "vidrio": hexc("#45b6ff", 0.85), "madera_osc": hexc("#4e2c14"),
    "pizarra_a": hexc(SLATE["pizarra_a"]), "pizarra_b": hexc(SLATE["pizarra_b"]),
}
setup("oro", PAL, emissive={"vidrio": 0.6}, rivet="oro_b")


def frame(door=False):
    """Lo comun de las paredes: zocalo, cornisa con dentellones y las dos columnas de las puntas."""
    box((0, FY - 0.03, 0.045), (W, 0.07, 0.09), "junta", 0.012)
    box((0, FY - 0.035, H - 0.055), (W, 0.08, 0.11), "crema_b", 0.015)
    box((0, FY - 0.015, H - 0.13), (W, 0.04, 0.04), "oro_b", 0)
    for k in range(9):
        x = -0.72 + k * 0.18
        if door and abs(x) < 0.5: continue
        box((x, FY - 0.022, H - 0.175), (0.09, 0.045, 0.05), "crema_b", 0)
    for s in (-1, 1):
        x = s * (W / 2 - 0.06)
        box((x, FY - 0.03, H / 2), (0.12, 0.06, H - 0.2), "crema_b", 0.012)
        for z in (0.12, 0.47, 0.8): box((x, FY - 0.05, z), (0.145, 0.05, 0.05), "oro", 0)


def field(x0, x1, z0, z1):
    """Juntas del sillar: dos lineas horizontales y unas verticales alternadas, apenas hundidas."""
    for z in (z0 + (z1 - z0) / 3, z0 + 2 * (z1 - z0) / 3):
        box(((x0 + x1) / 2, FY - 0.002, z), (x1 - x0, 0.006, 0.012), "junta", 0)


def niche(cx, z0, w, hs, coins=True, small=False):
    """Nicho de arco con fondo oscuro, marco dorado y su pila de monedas y lingotes."""
    r = w / 2
    box((cx, FY - 0.004, z0 + hs / 2), (w, 0.01, hs), "oscuro", 0)
    half_disc(cx, z0 + hs, r, FY - 0.009, FY + 0.001, "oscuro", seg=8)
    fw = 0.05 if small else 0.065
    for s in (-1, 1): box((cx + s * (r + fw / 2), FY - 0.03, z0 + hs / 2), (fw, 0.06, hs), "oro", 0)
    box((cx, FY - 0.035, z0 - fw / 2), (w + 2 * fw + 0.03, 0.07, fw), "oro", 0)
    arch_band(cx, z0 + hs, r, r + fw, FY - 0.06, FY, "oro", seg=8)
    gem((cx, FY - 0.06, z0 + hs + r + fw * 0.6), 0.04 if small else 0.05, "oro_b")
    if not coins: return
    if small:
        for k in range(4): coin((cx - 0.02, FY - 0.04, z0 + 0.012 + k * 0.024), 0.035, "oro" if k % 2 else "oro_b")
        coin((cx + 0.035, FY - 0.055, z0 + 0.05), 0.035, "oro", standing=True)
        return
    for k in range(3): coin((cx + 0.08, FY - 0.035, z0 + 0.012 + k * 0.024), 0.045, "oro_b" if k % 2 else "oro")
    ingot((cx + 0.075, FY - 0.04, z0 + 0.072), 0.13, 0.065, 0.05, "oro")
    coin((cx - 0.09, FY - 0.06, z0 + 0.055), 0.055, "oro", standing=True)
    coin((cx - 0.02, FY - 0.07, z0 + 0.06), 0.055, "oro_b", standing=True)
    ingot((cx - 0.04, FY - 0.035, z0 + 0.0), 0.14, 0.07, 0.055, "oro")


def medallion(c, r=0.075):
    """Rombo dorado con su gema (los de los costados de la lamina)."""
    diamond(c, r * 1.6, 0.03, "oro_b", 0)
    gem((c[0], c[1] - 0.02, c[2]), r * 0.62, "oro")


def build(piece):
    if piece in ("pared", "ventana"):
        box((0, 0, H / 2), (W, T, H), "crema", 0)
        field(-W / 2 + 0.12, W / 2 - 0.12, 0.36, H - 0.2)
        frame()
        # banda baja con dos paneles y el medallon redondo del centro
        box((0, FY - 0.025, 0.33), (W - 0.24, 0.05, 0.04), "oro", 0)
        for s in (-1, 1):
            box((s * 0.42, FY - 0.006, 0.21), (0.5, 0.012, 0.16), "crema_b", 0)
            box((s * 0.42, FY - 0.004, 0.21), (0.56, 0.008, 0.22), "junta", 0)
        cyl((0, FY - 0.04, 0.21), 0.11, 0.05, "oro", "Y", 14)
        cyl((0, FY - 0.06, 0.21), 0.085, 0.02, "oro_b", "Y", 14)
        gem((0, FY - 0.07, 0.21), 0.055, "oro")
        if piece == "pared":
            niche(0, 0.46, 0.4, 0.3)
            for s in (-1, 1): medallion((s * 0.52, FY - 0.03, 0.82))
        else:
            arch_window(0, 0.46, 0.4, 0.3, FY - 0.01, "vidrio", "oro", fw=0.07, depth=0.07, mull="oro_b")
            gem((0, FY - 0.05, 0.825), 0.04, "oro")
            box((0, FY - 0.07, 1.03), (0.12, 0.05, 0.1), "oro", 0.015)          # escudo de la clave
            gem((0, FY - 0.1, 1.03), 0.04, "oro_b")
            for s in (-1, 1): gem((s * 0.58, FY - 0.03, 0.74), 0.05, "oro")
            # alfeizar con monedas y lingotes al pie
            box((0, FY - 0.07, 0.385), (0.66, 0.08, 0.04), "oro_b", 0.01)
            for k in range(3): coin((-0.4, FY - 0.05, 0.37 + 0.012 + k * 0.024), 0.05, "oro" if k % 2 else "oro_b")
            coin((-0.28, FY - 0.07, 0.42), 0.045, "oro", standing=True)
            ingot((0.36, FY - 0.06, 0.37), 0.13, 0.065, 0.05, "oro")
            ingot((0.44, FY - 0.06, 0.42), 0.12, 0.06, 0.045, "oro_b")
    elif piece == "marco":
        door_cut_walls("crema")
        frame(door=True)
        field(-W / 2 + 0.12, -DW / 2 - 0.08, 0.1, H - 0.2); field(DW / 2 + 0.08, W / 2 - 0.12, 0.1, H - 0.2)
        # arco dorado de dovelas alrededor del hueco, con la gema en la clave
        for s in (-1, 1):
            box((s * (DW / 2 + 0.04), FY - 0.04, DH / 2), (0.08, 0.08, DH), "oro", 0.012)
            for z in (0.05, 0.5): box((s * (DW / 2 + 0.04), FY - 0.06, z), (0.1, 0.07, 0.08), "oro_b", 0)
        box((0, FY - 0.04, DH + 0.03), (DW + 0.16, 0.08, 0.06), "oro_b", 0.012)
        arch_band(0, DH + 0.06, DW / 2 - 0.05, DW / 2 + 0.08, FY - 0.075, FY, "oro", seg=10, squash=0.3)
        box((0, FY - 0.09, DH + 0.17), (0.13, 0.06, 0.12), "oro_b", 0.015)
        gem((0, FY - 0.12, DH + 0.17), 0.045, "oro")
        # nichos chicos de monedas a los costados, con un rombo encima
        for s in (-1, 1):
            niche(s * 0.62, 0.14, 0.14, 0.22, small=True)
            medallion((s * 0.62, FY - 0.03, 0.82), 0.06)
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.17, 0.34, 0.5, 0.67): box((x, -0.033, h / 2), (0.012, 0.01, h - 0.04), "madera_osc", 0)
        for z in (0.2, 0.7):
            box((w / 2, -0.04, z), (w - 0.08, 0.025, 0.06), "oro_b", 0.008)
            for x in (0.1, w - 0.1): diamond((x, -0.05, z), 0.085, 0.02, "oro", 0)
            for x in (0.3, 0.42, 0.54): rivet((x, -0.055, z), 0.016, "oro")
        diamond((w * 0.62, -0.045, 0.47), 0.09, 0.02, "oro_b", 0)
        ring((w * 0.62, -0.06, 0.4), 0.065, 0.016, "oro")
    elif piece == "remate":
        # el techo es el comun (comun_techo_<mascara>); el oro queda de acento: la cupula con su remate
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.1, 2 * RB + 0.1, 0.07), "oro_b", 0.015)
        cyl((0, 0, Rz1 + 0.1), 0.2, 0.05, "oro_b", v=12)
        dome((0, 0, Rz1 + 0.12), 0.17, "oro", seg=10, rings=4, h=1.05)
        cone((0, 0, Rz1 + 0.34), 0.045, 0.0, 0.1, "oro_b", v=4)


run(build, preview_mats=("oro_b", "oro"))
