"""Kit de habitacion MAESTRO (Sala central en su etapa final, Aula, Trofeos, Reloj): piedra oscura con molduras doradas,
dos estandartes rojos con el rombo dorado y medallon central con la corona; ventana de arco con vitral azul; puerta de
madera con herrajes dorados. Techo comun; el remate es la corona dorada (ancla "corona" para el destello).
Simplificado respecto de la lamina (sin filigranas: a la distancia del juego son ruido).
Calcado de D:/rtmp/trellis_job/in/habitaciones/maestro_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_maestro.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "piedra_a": hexc("#36363e"), "piedra_b": hexc("#2a2a30"), "junta": hexc("#1c1c21"), "oro": hexc("#e8b020"),
    "oro_b": hexc("#a67712"), "rojo": hexc("#b3202a"), "rojo_b": hexc("#7d141c"), "madera": hexc("#6b4325"),
    "madera_b": hexc("#4e2c14"), "vidrio": hexc("#4fb3ff", 0.85),
}
setup("maestro", PAL, emissive={"vidrio": 0.5}, rivet="oro_b")
ST = ("piedra_a", "piedra_b", "piedra_a")


def frame(door=False):
    box((0, FY - 0.035, H - 0.07), (W, 0.07, 0.12), "piedra_a", 0.012)
    box((0, FY - 0.055, H - 0.13), (W, 0.03, 0.03), "oro", 0)
    box((0, FY - 0.03, 0.06), (W, 0.07, 0.11), "piedra_b", 0.012)
    box((0, FY - 0.05, 0.12), (W, 0.03, 0.025), "oro_b", 0)
    for s in (-1, 1):
        x = s * (W / 2 - 0.07)
        box((x, FY - 0.035, H / 2), (0.13, 0.07, H), "piedra_a", 0.012)
        for z in (0.12, H - 0.13): box((x, FY - 0.06, z), (0.15, 0.04, 0.05), "oro", 0)
        diamond((x, FY - 0.075, H / 2), 0.07, 0.02, "oro", 0)


def banner(cx, z1, w=0.2, h=0.5):
    """Estandarte rojo colgado de una barra dorada, con la punta en V y el rombo dorado."""
    box((cx, FY - 0.05, z1 + 0.02), (w + 0.08, 0.025, 0.025), "oro", 0)
    bm = bmesh.new(); y0, y1 = FY - 0.035, FY - 0.02
    pts = [(cx - w / 2, z1), (cx + w / 2, z1), (cx + w / 2, z1 - h), (cx, z1 - h + 0.09), (cx - w / 2, z1 - h)]
    f0 = [bm.verts.new(Vector((x, y0, z))) for x, z in pts]; f1 = [bm.verts.new(Vector((x, y1, z))) for x, z in pts]
    bm.faces.new(f0); bm.faces.new(list(reversed(f1)))
    for i in range(len(pts)):
        j = (i + 1) % len(pts); bm.faces.new([f0[i], f1[i], f1[j], f0[j]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("estandarte"); bm.to_mesh(me); bm.free(); link(me, "estandarte", "rojo")
    box((cx, FY - 0.038, z1 - 0.03), (w, 0.008, 0.04), "rojo_b", 0)
    diamond((cx, FY - 0.045, z1 - h * 0.42), 0.08, 0.015, "oro", 0)


def crown(c, s, m="oro"):
    """Corona: aro y cinco puntas."""
    x, y, z = c
    box((x, y, z), (0.3 * s, 0.03, 0.06 * s), m, 0)
    for k in range(5):
        px = x + (-0.12 + k * 0.06) * s
        cone((px, y, z + 0.075 * s), 0.025 * s, 0.0, (0.1 if k % 2 == 0 else 0.07) * s, m, v=4)


def build(piece):
    if piece in ("pared", "ventana"):
        box((0, 0, H / 2), (W, T, H), "junta", 0)
        masonry(-W / 2 + 0.14, W / 2 - 0.14, 0.14, H - 0.15, 71 if piece == "pared" else 73, ST, course=0.14, lens=(0.24, 0.32, 0.4),
                depth=(0.012, 0.018), skip=(lambda x, z, L: abs(x) - L / 2 < 0.27 and 0.36 < z < 1.0) if piece == "ventana" else None)
        frame()
        for s in (-1, 1): banner(s * 0.5, 0.98)
        if piece == "pared":
            cyl((0, FY - 0.03, 0.62), 0.2, 0.05, "piedra_a", "Y", 16)
            cyl((0, FY - 0.05, 0.62), 0.16, 0.02, "junta", "Y", 16)
            ring((0, FY - 0.06, 0.62), 0.165, 0.012, "oro")
            crown((0, FY - 0.07, 0.56), 0.9)
        else:
            box((0, FY - 0.004, 0.69), (0.56, 0.008, 0.64), "piedra_b", 0)
            arch_window(0, 0.46, 0.34, 0.26, FY - 0.01, "vidrio", "piedra_a", fw=0.07, depth=0.07, mull="oro")
            box((0, FY - 0.06, 0.4), (0.56, 0.08, 0.06), "piedra_a", 0.01)
            crown((0, FY - 0.08, 0.98), 0.5)
    elif piece == "marco":
        door_cut_walls("junta")
        masonry(-W / 2 + 0.14, -DW / 2 - 0.08, 0.14, H - 0.15, 75, ST, course=0.14, lens=(0.16,), depth=(0.012, 0.018))
        masonry(DW / 2 + 0.08, W / 2 - 0.14, 0.14, H - 0.15, 77, ST, course=0.14, lens=(0.16,), depth=(0.012, 0.018))
        box((0, FY - 0.004, (DH + H) / 2), (DW + 0.16, 0.01, H - DH), "piedra_b", 0)
        frame(door=True)
        for s in (-1, 1): box((s * (DW / 2 + 0.04), FY - 0.04, DH / 2), (0.08, 0.07, DH), "piedra_a", 0.01)
        arch_band(0, DH, DW / 2 - 0.02, DW / 2 + 0.08, FY - 0.07, FY, "piedra_a", seg=8, squash=0.33)
        box((0, FY - 0.08, DH + 0.15), (0.14, 0.04, 0.12), "oro_b", 0.01)
        crown((0, FY - 0.1, DH + 0.12), 0.35)
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.14, 0.28, 0.42, 0.56, 0.7): box((x, -0.032, h / 2), (0.012, 0.01, h - 0.04), "madera_b", 0)
        for z in (0.2, 0.72):
            box((0.3, -0.04, z), (0.56, 0.025, 0.05), "oro", 0.006)
            diamond((0.58, -0.045, z), 0.08, 0.02, "oro", 0)
        ring((w * 0.72, -0.055, 0.44), 0.05, 0.014, "oro")
    elif piece == "remate":
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.12, 2 * RB + 0.12, 0.07), "oro_b", 0.015)
        cyl((0, 0, Rz1 + 0.11), 0.13, 0.07, "rojo", "Z", 10)
        for k in range(6):
            a = k * math.pi / 3
            cone((math.cos(a) * 0.12, math.sin(a) * 0.12, Rz1 + 0.19), 0.035, 0.0, 0.12, "oro", v=4)
        cyl((0, 0, Rz1 + 0.15), 0.135, 0.03, "oro", "Z", 10)
        sphere((0, 0, Rz1 + 0.2), 0.04, "oro", seg=8, rings=4)
        anchor("corona", (0, 0, Rz1 + 0.24), (0, 0, 1))


run(build, preview_mats=("junta", "oro_b"))
