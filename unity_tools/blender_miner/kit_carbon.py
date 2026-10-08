"""Kit de habitacion CARBON (Generador): ladrillo negro de hollin con postes de madera y esquineros de hierro, hogar con
reja encendida abajo (celda que titila), nicho con trozos de carbon; ventana de arco con vidrio naranja encendido
(celda que late) y reja de horno; puerta de tablas oscuras con mirilla encendida. El techo es el comun y el carbon pone
de remate la chimenea grande de ladrillo con la boca encendida y el ancla "humo" (RoomLife le pone humo).
Calcado de D:/rtmp/trellis_job/in/habitaciones/carbon_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_carbon.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "ladrillo_a": hexc("#2b2827"), "ladrillo_b": hexc("#1e1c1c"), "junta": hexc("#121111"), "carbon": hexc("#0d0d0e"),
    "brillo": hexc("#4a4a52"), "madera": hexc("#4a2e1b", 0.9), "hierro": hexc("#2a2a2c", 1.0), "cobre": hexc("#8a4a2a"),
    "brasa": hexc("#ff6a1a", 1.0), "vidrio": hexc("#ffb347", 1.0),
}
setup("carbon", PAL, rivet="hierro", glow={"brasa": "titila", "vidrio": "late"})
BR = ("ladrillo_a", "ladrillo_b", "ladrillo_a")


def frame():
    box((0, FY - 0.03, H - 0.055), (W, 0.07, 0.11), "ladrillo_a", 0.012)      # coronamiento de ladrillo
    box((0, FY - 0.03, 0.05), (W, 0.07, 0.1), "ladrillo_b", 0.012)
    for s in (-1, 1):
        box((s * (W / 2 - 0.07), FY - 0.035, H / 2), (0.11, 0.06, H - 0.2), "madera", 0.01)
        for z in (0.3, H - 0.2):
            box((s * (W / 2 - 0.07), FY - 0.06, z), (0.14, 0.03, 0.12), "hierro", 0)
            cyl((s * (W / 2 - 0.07), FY - 0.08, z), 0.02, 0.02, "brillo", "Y", 6)


def coal(c, r):
    """Trozo de carbon: esfera achatada de pocas caras."""
    sphere(c, r, "carbon", seg=6, rings=3, scale=(1.0, 0.8, 0.85))


def hearth(cx, z0, w=0.44, h=0.3):
    """Hogar: boca con la brasa encendida, barrotes, marco de hierro y arco de ladrillo."""
    box((cx, FY - 0.004, z0 + h / 2), (w, 0.01, h), "brasa", 0)
    for k in range(5): box((cx - w * 0.36 + k * w * 0.18, FY - 0.03, z0 + h / 2), (0.03, 0.025, h), "hierro", 0)
    for s in (-1, 1): box((cx + s * (w / 2 + 0.03), FY - 0.03, z0 + h / 2), (0.06, 0.05, h + 0.04), "hierro", 0.008)
    box((cx, FY - 0.03, z0 - 0.02), (w + 0.12, 0.05, 0.05), "hierro", 0.008)
    arch_band(cx, z0 + h, w / 2, w / 2 + 0.07, FY - 0.05, FY, "ladrillo_a", seg=6, squash=0.5)
    for k in range(3): coal((cx - 0.1 + k * 0.1, FY - 0.03, z0 + 0.04), 0.045)


def build(piece):
    if piece == "pared":
        box((0, 0, H / 2), (W, T, H), "junta", 0)
        masonry(-W / 2 + 0.12, W / 2 - 0.12, 0.1, H - 0.11, 21, BR, course=0.12, lens=(0.2, 0.26, 0.32),
                skip=lambda x, z, L: (abs(x) - L / 2 < 0.3 and z < 0.55) or (abs(x + 0.38) - L / 2 < 0.17 and 0.62 < z < 0.98))
        frame()
        hearth(0, 0.13)
        # nicho con carbon (marco de hierro)
        box((-0.38, FY - 0.004, 0.8), (0.3, 0.01, 0.3), "junta", 0)
        for s in (-1, 1):
            box((-0.38 + s * 0.165, FY - 0.03, 0.8), (0.04, 0.05, 0.36), "hierro", 0)
            box((-0.38, FY - 0.03, 0.8 + s * 0.165), (0.36, 0.05, 0.04), "hierro", 0)
        for dx, dz, r in ((-0.06, -0.06, 0.07), (0.06, -0.05, 0.065), (0.0, 0.05, 0.06)): coal((-0.38 + dx, FY - 0.04, 0.8 + dz), r)
        # columna de chimenea en relieve (la chimenea grande va en el techo)
        box((0.4, FY - 0.04, 0.85), (0.24, 0.06, 0.5), "ladrillo_b", 0.012)
        box((0.4, FY - 0.07, 1.04), (0.28, 0.03, 0.06), "hierro", 0)
    elif piece == "ventana":
        box((0, 0, H / 2), (W, T, H), "junta", 0)
        masonry(-W / 2 + 0.12, W / 2 - 0.12, 0.1, H - 0.11, 33, BR, course=0.12, lens=(0.2, 0.26, 0.32),
                skip=lambda x, z, L: (abs(x) - L / 2 < 0.27 and 0.36 < z < 1.0) or (abs(x) - L / 2 < 0.25 and z < 0.3))
        frame()
        arch_window(0, 0.46, 0.34, 0.26, FY - 0.01, "vidrio", "ladrillo_a", fw=0.07, depth=0.06, mull="cobre")
        box((0, FY - 0.06, 0.4), (0.56, 0.08, 0.06), "ladrillo_a", 0.01)
        # reja de horno abajo, con aro de cobre
        box((0, FY - 0.004, 0.18), (0.4, 0.01, 0.16), "brasa", 0)
        for k in range(6): box((-0.15 + k * 0.06, FY - 0.03, 0.18), (0.022, 0.02, 0.17), "hierro", 0)
        arch_band(0, 0.26, 0.2, 0.245, FY - 0.04, FY, "cobre", seg=6, squash=0.35)
        for s in (-1, 1): box((s * 0.22, FY - 0.03, 0.18), (0.045, 0.04, 0.18), "cobre", 0)
        for dx, dz, r in ((-0.55, 0.16, 0.06), (-0.47, 0.13, 0.05), (0.55, 0.8, 0.055), (-0.52, 0.85, 0.05)): coal((dx, FY - 0.03, dz), r)
    elif piece == "marco":
        door_cut_walls("junta")
        masonry(-W / 2 + 0.12, -DW / 2 - 0.06, 0.1, H - 0.11, 5, BR, course=0.12, lens=(0.12, 0.18))
        masonry(DW / 2 + 0.06, W / 2 - 0.12, 0.1, H - 0.11, 7, BR, course=0.12, lens=(0.12, 0.18))
        masonry(-DW / 2 - 0.06, DW / 2 + 0.06, DH + 0.12, H - 0.11, 9, BR, course=0.12, lens=(0.2, 0.26))
        frame()
        for s in (-1, 1): box((s * (DW / 2 + 0.035), FY - 0.035, DH / 2), (0.07, 0.06, DH), "ladrillo_a", 0.01)
        arch_band(0, DH, DW / 2 - 0.02, DW / 2 + 0.07, FY - 0.06, FY, "ladrillo_a", seg=8, squash=0.32)
        for dx, r in ((-0.62, 0.065), (-0.55, 0.05), (0.6, 0.06)): coal((dx, FY - 0.03, 0.14), r)
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.14, 0.28, 0.42, 0.56, 0.7): box((x, -0.032, h / 2), (0.012, 0.01, h - 0.04), "carbon", 0)
        for z in (0.18, 0.74): box((w / 2, -0.04, z), (w - 0.06, 0.025, 0.07), "hierro", 0.006)
        # mirilla encendida
        box((w / 2, -0.035, 0.52), (0.18, 0.02, 0.12), "brasa", 0)
        box((w / 2, -0.05, 0.52), (0.22, 0.015, 0.03), "hierro", 0)
        for k in range(3): box((w / 2 - 0.06 + k * 0.06, -0.05, 0.52), (0.015, 0.015, 0.14), "hierro", 0)
        ring((w * 0.78, -0.055, 0.44), 0.05, 0.014, "hierro")
    elif piece == "remate":
        # techo comun; el carbon pone la chimenea grande de ladrillo con la boca encendida
        cx, cy = 0.35, 0.3
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.1, 2 * RB + 0.1, 0.07), "hierro", 0.015)
        box((cx, cy, 0.62), (0.3, 0.3, 0.7), "ladrillo_a", 0.02)
        for z in (0.45, 0.75): box((cx, cy, z), (0.32, 0.32, 0.03), "ladrillo_b", 0)
        box((cx, cy, 0.98), (0.36, 0.36, 0.06), "hierro", 0.01)
        box((cx, cy, 1.0), (0.2, 0.2, 0.03), "brasa", 0)
        anchor("humo", (cx, cy, 1.04), (0, 0, 1))


run(build, preview_mats=("junta", "madera"))
