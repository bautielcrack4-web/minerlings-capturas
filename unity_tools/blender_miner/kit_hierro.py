"""Kit de habitacion HIERRO (Herramientas, Taladro, Iman): placas blindadas con bisel dentro de un marco remachado
grueso, bulones en las esquinas; ventana de arco con vidrio azul en cruz; puerta de madera con herrajes y argolla bajo
un arco de placas. El techo es el comun (kit_comun.py) y el hierro pone su remate: tapa remachada con un bulon grande.
Calcado de D:/rtmp/trellis_job/in/habitaciones/hierro_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_hierro.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "hierro": hexc("#4b4f55"), "hierro_b": hexc("#3a3d42"), "borde": hexc("#25272b"), "remache": hexc("#6c7178"),
    "madera": hexc("#7a4a26"), "madera_b": hexc("#5e3a1e"), "vidrio": hexc("#3fb7ff", 0.85), "fondo": hexc("#2c2e33"),
}
setup("hierro", PAL, emissive={"vidrio": 0.5}, rivet="remache")


def frame(door=False):
    """Marco remachado grueso: soleras arriba y abajo, postes en las puntas y bloques de esquina con bulon."""
    box((0, FY - 0.035, H - 0.07), (W, 0.07, 0.13), "hierro", 0.012)
    box((0, FY - 0.035, 0.065), (W, 0.07, 0.12), "hierro", 0.012)
    for s in (-1, 1):
        box((s * (W / 2 - 0.07), FY - 0.035, H / 2), (0.13, 0.07, H), "hierro", 0.012)
        for z in (0.07, H - 0.07):
            box((s * (W / 2 - 0.07), FY - 0.06, z), (0.17, 0.05, 0.17), "hierro_b", 0.015)
            rivet((s * (W / 2 - 0.07), FY - 0.09, z), 0.035)
        for z in (0.45, 0.8): rivet((s * (W / 2 - 0.07), FY - 0.075, z), 0.022)
    for x in (-0.55, -0.27, 0.0, 0.27, 0.55):
        if door and abs(x) < 0.4: continue
        rivet((x, FY - 0.075, H - 0.07), 0.022); rivet((x, FY - 0.075, 0.065), 0.022)


def plate(cx, cz, w, h, m="hierro_b"):
    box((cx, FY - 0.012, cz), (w - 0.03, 0.025, h - 0.03), m, 0)
    for sx in (-1, 1):
        for sz in (-1, 1): rivet((cx + sx * (w / 2 - 0.05), FY - 0.03, cz + sz * (h / 2 - 0.05)), 0.012)


def strut(x, z0=0.13, z1=H - 0.14):
    """Montante remachado entre las placas."""
    box((x, FY - 0.03, (z0 + z1) / 2), (0.08, 0.05, z1 - z0), "hierro", 0)
    for k in range(4): rivet((x, FY - 0.06, z0 + 0.12 + k * (z1 - z0 - 0.24) / 3), 0.02)


def build(piece):
    if piece in ("pared", "ventana"):
        box((0, 0, H / 2), (W, T, H), "fondo", 0)
        frame()
        z0, z1 = 0.13, H - 0.14
        for s in (-1, 1):
            strut(s * 0.36, z0, z1)
            for k in range(3): plate(s * 0.6, z0 + (z1 - z0) * (k + 0.5) / 3, 0.3, (z1 - z0) / 3)
        if piece == "pared":
            plate(0, z0 + (z1 - z0) * 0.15, 0.62, (z1 - z0) * 0.3)
            plate(0, z0 + (z1 - z0) * 0.6, 0.62, (z1 - z0) * 0.5, "hierro")
            plate(0, z1 - (z1 - z0) * 0.1, 0.62, (z1 - z0) * 0.2)
        else:
            plate(0, z0 + 0.12, 0.62, 0.22)
            # ventana de arco chica con vidrio azul en cruz y marco de hierro remachado
            arch_window(0, 0.42, 0.34, 0.24, FY - 0.01, "vidrio", "hierro", fw=0.06, depth=0.06, mull="borde")
            for k in range(5):
                a = math.pi * (k + 0.5) / 5
                rivet((math.cos(a) * 0.2, FY - 0.07, 0.66 + math.sin(a) * 0.2), 0.014)
            box((0, FY - 0.05, 0.37), (0.5, 0.06, 0.05), "hierro", 0.008)
    elif piece == "marco":
        door_cut_walls("fondo")
        frame(door=True)
        for s in (-1, 1):
            strut(s * (DW / 2 + 0.17), 0.13, H - 0.14)
            box((s * (DW / 2 + 0.04), FY - 0.04, DH / 2), (0.08, 0.07, DH), "hierro", 0.01)
            box((s * 0.73, FY - 0.03, 0.55), (0.11, 0.04, 0.11), "hierro_b", 0.01)
            rivet((s * 0.73, FY - 0.06, 0.55), 0.03)
        # arco de placas sobre el hueco con la clave remachada
        arch_band(0, DH, DW / 2 - 0.02, DW / 2 + 0.08, FY - 0.07, FY, "hierro", seg=8, squash=0.35)
        box((0, FY - 0.08, DH + 0.17), (0.12, 0.05, 0.1), "hierro_b", 0.01)
        rivet((0, FY - 0.11, DH + 0.17), 0.025)
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.14, 0.28, 0.42, 0.56, 0.7): box((x, -0.032, h / 2), (0.012, 0.01, h - 0.04), "madera_b", 0)
        for z in (0.2, 0.72):
            box((w / 2, -0.04, z), (w - 0.06, 0.025, 0.07), "hierro", 0.006)
            for x in (0.12, 0.27, 0.42, 0.57, 0.72): rivet((x, -0.055, z), 0.014)
        box((w * 0.5, -0.045, 0.5), (0.08, 0.02, 0.08), "hierro", 0)
        ring((w * 0.5, -0.06, 0.42), 0.065, 0.016, "hierro_b")
    elif piece == "remate":
        # techo comun; el hierro pone una tapa remachada con un bulon grande (la punta de la lamina)
        box((0, 0, Rz1 + 0.05), (0.42, 0.42, 0.09), "hierro", 0.02)
        for sx in (-1, 1):
            for sy in (-1, 1): rivet((sx * 0.15, sy * 0.15, Rz1 + 0.1), 0.022)
        cyl((0, 0, Rz1 + 0.13), 0.07, 0.06, "remache", "Z", 6)


run(build, preview_mats=("borde", "hierro"))
