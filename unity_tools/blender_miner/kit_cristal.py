"""Kit de habitacion CRISTAL (Laboratorio, Laboratorio portatil, Observatorio): adoquin irregular azul-gris con vetas
violetas que respiran entre las juntas (el fondo de la pared es la celda emisiva "veta") y racimos de cristal; ventana de
arco de piedra con vidrio celeste y racimos a los costados; puerta de madera con argolla bajo un arco con cristal en la
clave. El techo es el comun y el cristal pone de remate un racimo grande (ancla "cristal" para hacerlo flotar).
Calcado de D:/rtmp/trellis_job/in/habitaciones/cristal_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_cristal.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "piedra_a": hexc("#5d6378"), "piedra_b": hexc("#4e5368"), "piedra_c": hexc("#6a7086"), "madera": hexc("#4a3a2e", 0.9),
    "hierro": hexc("#3a3d48"), "cristal_b": hexc("#c8b6ff", 0.8), "vidrio": hexc("#7fd0ff", 0.85),
    "veta": hexc("#7a5cff", 1.0), "cristal": hexc("#8e6bff", 1.0),
}
setup("cristal", PAL, rivet="hierro", glow={"veta": "respira", "cristal": "respira"})
ST = ("piedra_a", "piedra_b", "piedra_c", "piedra_a")


def frame(door=False):
    """Coronamiento y zocalo de sillares grandes (sin vetas: la luz queda en el paño)."""
    for z, hh in ((H - 0.07, 0.13), (0.06, 0.12)):
        x = -W / 2
        for L in (0.3, 0.4, 0.3, 0.38, 0.32):
            if not (door and z < 0.5 and abs(x + L / 2) < DW / 2 + 0.1):
                beam((x + 0.01, FY - 0.03, z), (x + L - 0.01, FY - 0.03, z), hh - 0.015, 0.07, "piedra_c", up=(0, -1, 0), top=0.85)
            x += L


def field(x0, x1, z0, z1, seed, skip=None):
    """Adoquin irregular: hiladas de alto variable sobre el fondo que brilla (las juntas son las vetas)."""
    masonry(x0, x1, z0, z1, seed, ST, skip=skip, course=0.16, lens=(0.14, 0.2, 0.26), depth=(0.03, 0.045))


def build(piece):
    if piece == "pared":
        box((0, 0, H / 2), (W, T, H), "veta", 0)
        field(-W / 2 + 0.02, W / 2 - 0.02, 0.12, H - 0.14, 41)
        frame()
        cluster((-0.48, FY - 0.06, 0.42), 1.25, "cristal", "cristal_b")
        cluster((0.52, FY - 0.06, 0.78), 0.85, "cristal", "cristal_b")
        cluster((0.05, FY - 0.05, 0.12), 0.6, "cristal", "cristal_b", lean=0.5)
    elif piece == "ventana":
        box((0, 0, H / 2), (W, T, H), "veta", 0)
        field(-W / 2 + 0.02, W / 2 - 0.02, 0.12, H - 0.14, 43, skip=lambda x, z, L: abs(x) - L / 2 < 0.27 and 0.36 < z < 1.0)
        frame()
        box((0, FY - 0.004, 0.69), (0.56, 0.008, 0.64), "piedra_b", 0)        # sin veta detras de la ventana
        arch_window(0, 0.46, 0.34, 0.26, FY - 0.01, "vidrio", "piedra_c", fw=0.07, depth=0.07, mull="madera")
        box((0, FY - 0.06, 0.4), (0.56, 0.08, 0.06), "piedra_c", 0.01)
        for s in (-1, 1): cluster((s * 0.5, FY - 0.06, 0.34), 0.9, "cristal", "cristal_b")
    elif piece == "marco":
        door_cut_walls("veta")
        field(-W / 2 + 0.02, -DW / 2 - 0.08, 0.12, H - 0.14, 45)
        field(DW / 2 + 0.08, W / 2 - 0.02, 0.12, H - 0.14, 47)
        field(-DW / 2 - 0.08, DW / 2 + 0.08, DH + 0.1, H - 0.14, 49)
        frame(door=True)
        for s in (-1, 1): box((s * (DW / 2 + 0.04), FY - 0.04, DH / 2), (0.08, 0.07, DH), "madera", 0.01)
        box((0, FY - 0.04, DH + 0.04), (DW + 0.16, 0.08, 0.08), "madera", 0.01)
        crystal((0, FY - 0.06, DH + 0.06), 0.04, 0.17, "cristal", 0.0, 0.2)
        for s in (-1, 1): cluster((s * 0.66, FY - 0.06, 0.12), 0.9, "cristal", "cristal_b")
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.14, 0.28, 0.42, 0.56, 0.7): box((x, -0.032, h / 2), (0.012, 0.01, h - 0.04), "hierro", 0)
        for z in (0.2, 0.72): box((w / 2, -0.04, z), (w - 0.06, 0.025, 0.06), "hierro", 0.006)
        ring((w * 0.7, -0.055, 0.46), 0.055, 0.014, "hierro")
        gem((w * 0.7, -0.05, 0.56), 0.035, "cristal")
    elif piece == "remate":
        # techo comun; el cristal pone un racimo grande en una base de hierro (ancla para que flote y gire)
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.12, 2 * RB + 0.12, 0.07), "hierro", 0.015)
        cluster((0, 0, Rz1 + 0.07), 1.6, "cristal", "cristal_b", lean=0.0)
        anchor("cristal", (0, 0, Rz1 + 0.3), (0, 0, 1))


run(build, preview_mats=("piedra_b", "madera"))
