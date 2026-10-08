"""Kit de habitacion RAROS (Cuarto secreto, Experimental, Palomar): paneles de metal violeta con estante de frascos que
brillan (celda que respira), rejilla de ventilacion y panel con 3 LEDs (celda de parpadeo); ventana con frascos detras
del vidrio; puerta de metal y madera bajo un arco con luz violeta en la clave. Techo comun; el remate es una antena con
la bolita encendida (ancla "antena" para el pulso de anillo).
Calcado de D:/rtmp/trellis_job/in/habitaciones/raros_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_raros.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "metal_a": hexc("#5b3a85"), "metal_b": hexc("#4a2f6e"), "borde": hexc("#2c1c45"), "vidrio": hexc("#b9a6d6", 0.7),
    "madera": hexc("#4a3a2e", 0.9), "hierro": hexc("#3a3046"),
    "frasco": hexc("#c04dff", 1.0), "led": hexc("#ff5cf0", 1.0), "led_b": hexc("#5cffd6", 1.0),
}
setup("raros", PAL, rivet="borde", glow={"frasco": "respira", "led": "led", "led_b": "led"})


def frame(door=False):
    box((0, FY - 0.035, H - 0.065), (W, 0.07, 0.12), "metal_b", 0.012)
    box((0, FY - 0.035, 0.06), (W, 0.07, 0.11), "metal_b", 0.012)
    for s in (-1, 1):
        box((s * (W / 2 - 0.07), FY - 0.035, H / 2), (0.12, 0.07, H), "metal_b", 0.012)
        for z in (0.06, H - 0.065): rivet((s * (W / 2 - 0.07), FY - 0.08, z), 0.03)


def jar(c, h=0.17):
    """Frasco: vidrio con la piedra encendida adentro y tapa."""
    cyl((c[0], c[1], c[2] + h / 2), 0.055, h, "vidrio", "Z", 8)
    sphere((c[0], c[1] - 0.01, c[2] + h * 0.42), 0.04, "frasco", seg=6, rings=3, scale=(1, 0.8, 1.2))
    cyl((c[0], c[1], c[2] + h + 0.015), 0.062, 0.03, "metal_a", "Z", 8)


def build(piece):
    if piece in ("pared", "ventana"):
        box((0, 0, H / 2), (W, T, H), "metal_b", 0)
        frame()
        # panel de la izquierda: rejilla y 3 LEDs; a la derecha el estante de frascos
        box((-0.45, FY - 0.02, 0.8), (0.42, 0.04, 0.42), "metal_a", 0.012)
        for k in range(4): box((-0.55, FY - 0.045, 0.66 + k * 0.08), (0.14, 0.02, 0.035), "borde", 0)
        for k, m in enumerate(("led", "led_b", "led")): box((-0.4 + k * 0.06, FY - 0.045, 0.85), (0.03, 0.02, 0.1), m, 0)
        box((-0.45, FY - 0.02, 0.33), (0.42, 0.04, 0.4), "metal_a", 0.012)
        for s in (-1, 1): beam((-0.45 - 0.17, FY - 0.045, 0.33 + s * 0.17), (-0.45 + 0.17, FY - 0.045, 0.33 - s * 0.17), 0.03, 0.02, "borde", up=(0, -1, 0))
        gem((-0.45, FY - 0.06, 0.33), 0.045, "frasco")
        if piece == "pared":
            box((0.25, FY - 0.004, 0.6), (0.66, 0.01, 0.82), "borde", 0)
            for s in (-1, 1): box((0.25 + s * 0.34, FY - 0.04, 0.6), (0.06, 0.07, 0.86), "metal_a", 0.01)
            for z in (0.2, 0.6, 1.0): box((0.25, FY - 0.045, z), (0.66, 0.08, 0.03), "metal_a", 0)
            for z in (0.215, 0.615):
                for k in range(3): jar((0.25 - 0.2 + k * 0.2, FY - 0.06, z))
        else:
            box((0.25, FY - 0.004, 0.62), (0.6, 0.01, 0.6), "borde", 0)
            for k in range(3): jar((0.25 - 0.17 + k * 0.17, FY - 0.03, 0.4))
            box((0.25, FY - 0.035, 0.62), (0.56, 0.01, 0.52), "vidrio", 0)          # vidrio delante de los frascos
            for s in (-1, 1): box((0.25 + s * 0.3, FY - 0.04, 0.62), (0.05, 0.07, 0.6), "metal_a", 0.01)
            for z in (0.33, 0.91): box((0.25, FY - 0.04, z), (0.65, 0.07, 0.05), "metal_a", 0.01)
    elif piece == "marco":
        door_cut_walls("metal_b")
        frame(door=True)
        for s in (-1, 1):
            box((s * (DW / 2 + 0.05), FY - 0.04, DH / 2), (0.1, 0.07, DH), "metal_a", 0.01)
            box((s * (DW / 2 + 0.05), FY - 0.08, DH * 0.6), (0.03, 0.01, 0.3), "frasco", 0)
            jar((s * 0.66, FY - 0.06, 0.13))
        box((0, FY - 0.04, DH + 0.06), (DW + 0.2, 0.07, 0.1), "metal_a", 0.01)
        gem((0, FY - 0.09, DH + 0.06), 0.05, "frasco")
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "metal_b", 0.012)
        box((w / 2, -0.035, h / 2), (w - 0.14, 0.012, h - 0.14), "madera", 0)
        for z in (0.22, 0.7): box((w / 2, -0.045, z), (w - 0.06, 0.025, 0.05), "metal_a", 0.006)
        box((w * 0.75, -0.05, 0.47), (0.04, 0.02, 0.12), "led", 0)
    elif piece == "remate":
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.12, 2 * RB + 0.12, 0.07), "metal_a", 0.015)
        cyl((0, 0, Rz1 + 0.3), 0.012, 0.5, "hierro", "Z", 6)
        for k in range(3): cyl((0, 0, Rz1 + 0.12 + k * 0.04), 0.035, 0.012, "hierro", "Z", 8)
        sphere((0, 0, Rz1 + 0.57), 0.045, "led", seg=8, rings=4)
        # parabolica chica
        dome((0.22, -0.18, Rz1 + 0.08), 0.1, "metal_a", seg=8, rings=2, h=0.45)
        anchor("antena", (0, 0, Rz1 + 0.57), (0, 0, 1))


run(build, preview_mats=("borde", "metal_a"))
