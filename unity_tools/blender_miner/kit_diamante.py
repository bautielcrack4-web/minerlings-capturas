"""Kit de habitacion DIAMANTE (Pulidora): marmol blanco con columnas en las puntas (incrustaciones celestes), rombo grande
de diamante con marco dorado al centro y cuatro chicos; ventana de arco blanco con vidrio celeste; puerta de madera con
herraje dorado bajo un arco con el diamante en la clave. Techo comun; el remate es un diamante grande sobre base dorada
(ancla "diamante" para el destello). Los diamantes laten suave (celda emisiva).
Calcado de D:/rtmp/trellis_job/in/habitaciones/diamante_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_diamante.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "marmol_a": hexc("#f2f2f0"), "marmol_b": hexc("#dcdedf"), "veta": hexc("#c7cbd0"), "oro": hexc("#e0a91a"),
    "madera": hexc("#7a4a26"), "madera_b": hexc("#5e3a1e"), "vidrio": hexc("#8fd8ff", 0.85), "diamante": hexc("#2fa8ff", 1.0),
}
setup("diamante", PAL, rivet="oro", glow={"diamante": "late"})
MB = ("marmol_a", "marmol_b", "marmol_a")


def frame(door=False):
    box((0, FY - 0.035, H - 0.06), (W, 0.08, 0.12), "marmol_a", 0.015)
    box((0, FY - 0.045, H - 0.15), (W - 0.3, 0.03, 0.03), "oro", 0)
    box((0, FY - 0.03, 0.05), (W, 0.07, 0.1), "marmol_b", 0.012)
    for s in (-1, 1):
        x = s * (W / 2 - 0.08)
        box((x, FY - 0.035, H / 2), (0.15, 0.07, H - 0.2), "marmol_a", 0.012)
        for z in (0.14, H - 0.2): box((x, FY - 0.06, z), (0.17, 0.04, 0.05), "oro", 0)
        for z in (0.36, 0.78): box((x, FY - 0.072, z), (0.06, 0.01, 0.26), "diamante", 0)


def jewel(c, s):
    """Rombo de diamante con marco dorado."""
    diamond(c, s * 1.25, 0.025, "oro", 0)
    gem((c[0], c[1] - 0.02, c[2]), s * 0.52, "diamante", flat=0.5)


def build(piece):
    if piece in ("pared", "ventana"):
        box((0, 0, H / 2), (W, T, H), "veta", 0)
        masonry(-W / 2 + 0.16, W / 2 - 0.16, 0.1, H - 0.12, 61 if piece == "pared" else 63, MB, course=0.26, lens=(0.3, 0.42),
                depth=(0.012, 0.018), skip=(lambda x, z, L: abs(x) - L / 2 < 0.27 and 0.36 < z < 1.0) if piece == "ventana" else None)
        frame()
        if piece == "pared":
            jewel((0, FY - 0.03, 0.6), 0.2)
            for sx in (-1, 1):
                for z in (0.32, 0.88): jewel((sx * 0.42, FY - 0.025, z), 0.075)
        else:
            box((0, FY - 0.004, 0.69), (0.56, 0.008, 0.64), "marmol_b", 0)
            arch_window(0, 0.46, 0.34, 0.26, FY - 0.01, "vidrio", "marmol_a", fw=0.07, depth=0.07, mull="oro")
            jewel((0, FY - 0.08, 0.99), 0.06)
            box((0, FY - 0.06, 0.4), (0.56, 0.08, 0.06), "marmol_a", 0.01)
            for s in (-1, 1): jewel((s * 0.45, FY - 0.025, 0.66), 0.07)
    elif piece == "marco":
        door_cut_walls("veta")
        masonry(-W / 2 + 0.16, -DW / 2 - 0.08, 0.1, H - 0.12, 65, MB, course=0.26, lens=(0.2,), depth=(0.012, 0.018))
        masonry(DW / 2 + 0.08, W / 2 - 0.16, 0.1, H - 0.12, 67, MB, course=0.26, lens=(0.2,), depth=(0.012, 0.018))
        box((0, FY - 0.004, (DH + H) / 2), (DW + 0.16, 0.01, H - DH), "marmol_b", 0)
        frame(door=True)
        for s in (-1, 1): box((s * (DW / 2 + 0.04), FY - 0.04, DH / 2), (0.08, 0.07, DH), "marmol_a", 0.01)
        arch_band(0, DH, DW / 2 - 0.02, DW / 2 + 0.08, FY - 0.07, FY, "marmol_a", seg=8, squash=0.33)
        jewel((0, FY - 0.08, DH + 0.15), 0.07)
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.14, 0.28, 0.42, 0.56, 0.7): box((x, -0.032, h / 2), (0.012, 0.01, h - 0.04), "madera_b", 0)
        for z in (0.2, 0.72): box((w / 2, -0.04, z), (w - 0.06, 0.025, 0.05), "oro", 0.006)
        jewel((w * 0.5, -0.045, 0.48), 0.06)
    elif piece == "remate":
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.12, 2 * RB + 0.12, 0.07), "oro", 0.015)
        cyl((0, 0, Rz1 + 0.1), 0.1, 0.05, "oro", "Z", 8)
        # diamante: octaedro alto sobre su base
        bm = bmesh.new(); r, zc = 0.12, Rz1 + 0.27
        vs = [bm.verts.new(Vector((math.cos(i * math.pi / 2) * r, math.sin(i * math.pi / 2) * r, zc))) for i in range(4)]
        top, bot = bm.verts.new(Vector((0, 0, zc + 0.15))), bm.verts.new(Vector((0, 0, zc - 0.14)))
        for i in range(4):
            j = (i + 1) % 4; bm.faces.new([vs[i], vs[j], top]); bm.faces.new([vs[j], vs[i], bot])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        me = bpy.data.meshes.new("diamante"); bm.to_mesh(me); bm.free(); link(me, "diamante", "diamante")
        anchor("diamante", (0, 0, zc), (0, 0, 1))


run(build, preview_mats=("marmol_b", "oro"))
