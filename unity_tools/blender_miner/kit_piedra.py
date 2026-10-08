"""Kit de habitacion PIEDRA (Sala central, Trituradora, Deposito, Descarga, Pasillos, Escaleras): sillares grises en
relieve de tres tamanos dentro de un entramado de vigas de madera (con la cruz en Y de la lamina), esquineros de hierro
con clavos, farol encendido y placa hexagonal con el martillo; ventana de arco de dovelas con vidrio celeste; puerta de
tablas con herrajes negros y aldaba; techo de pizarra gris-azul con aleros y limas de madera y remate de hierro.
Calcado de D:/rtmp/trellis_job/in/habitaciones/piedra_*.png, paleta oscurecida ~25 % (el toon del juego aclara).
uso: blender -b -P kit_piedra.py -- outdir [--preview png] [--sheet png]
"""
import os, sys, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

PAL = {
    "piedra_a": hexc("#8d8984"), "piedra_b": hexc("#6e6a66"), "junta": hexc("#3c3936"), "hierro": hexc("#2a2a2c", 1.0),
    "madera": hexc("#7a4a26"), "madera_b": hexc("#6b4325"), "clavo": hexc("#6d6d70"), "farol": hexc("#ffc451", 1.0),
    "pizarra_a": hexc(SLATE["pizarra_a"]), "pizarra_b": hexc(SLATE["pizarra_b"]), "vidrio": hexc("#3fa9f5", 0.85), "piedra_c": hexc("#a29d96"),
}
setup("piedra", PAL, emissive={"vidrio": 0.5}, rivet="clavo", glow={"farol": "titila"})


def stones(x0, x1, z0, z1, seed, skip=None, rows=None):
    """Sillares en relieve (chaflan al frente) en hiladas de alto parejo y largos al azar; skip(x, z) los saltea."""
    rnd = random.Random(seed)
    n = rows or max(2, int(round((z1 - z0) / 0.17)))
    hh = (z1 - z0) / n
    for j in range(n):
        z = z0 + hh * (j + 0.5)
        x = x0
        while x < x1 - 0.02:
            L = min(rnd.choice((0.16, 0.22, 0.28, 0.34)), x1 - x)
            if x1 - (x + L) < 0.08: L = x1 - x
            cx = x + L / 2
            if not (skip and skip(cx, z, L)):
                d = rnd.uniform(0.022, 0.034)
                beam((x + 0.012, FY - d / 2 + 0.004, z), (x + L - 0.012, FY - d / 2 + 0.004, z), hh - 0.022, d,
                     rnd.choice(("piedra_a", "piedra_b", "piedra_a", "piedra_c")), up=(0, -1, 0), top=0.82)
            x += L


def plate(c, s=0.13):
    """Esquinero de hierro con un clavo."""
    box(c, (s, 0.03, s), "hierro", 0)
    cyl((c[0], c[1] - 0.02, c[2]), 0.022, 0.02, "clavo", "Y", 6)


def post(x, w=0.12):
    box((x, FY - 0.035, H / 2), (w, 0.07, H), "madera", 0.012)
    box((x - w * 0.18, FY - 0.071, H / 2), (0.008, 0.004, H - 0.1), "madera_b", 0)    # veta


def timber(x0, x1, z, h=0.11, m="madera"):
    box(((x0 + x1) / 2, FY - 0.035, z), (x1 - x0, 0.07, h), m, 0.012)


def lantern(x, z, side=1):
    """Farol colgado de una mensula de hierro: techito, vidrio encendido con barrotes y base."""
    box((x - side * 0.02, FY - 0.02, z + 0.16), (0.05, 0.03, 0.12), "hierro", 0)
    beam((x - side * 0.02, FY - 0.03, z + 0.2), (x, FY - 0.1, z + 0.2), 0.025, 0.025, "hierro")
    cyl((x, FY - 0.1, z + 0.155), 0.016, 0.05, "hierro", "Z", 6)
    cone((x, FY - 0.1, z + 0.115), 0.075, 0.02, 0.05, "hierro", v=6)
    cyl((x, FY - 0.1, z + 0.02), 0.055, 0.14, "farol", "Z", 6)
    for k in range(3):
        a = k * 2 * math.pi / 3 + math.pi / 6
        box((x + math.cos(a) * 0.058, FY - 0.1 + math.sin(a) * 0.058, z + 0.02), (0.014, 0.014, 0.15), "hierro", 0)
    cyl((x, FY - 0.1, z - 0.06), 0.065, 0.025, "hierro", "Z", 6)
    cone((x, FY - 0.1, z - 0.09), 0.03, 0.0, 0.04, "hierro", v=6)


def hammer_plaque(c, r=0.11):
    """Placa hexagonal de hierro con el martillo cruzado."""
    o = cyl(c, r, 0.03, "hierro", "Y", 6)
    cyl((c[0], c[1] - 0.012, c[2]), r * 0.84, 0.012, "junta", "Y", 6)
    beam((c[0] - r * 0.35, c[1] - 0.03, c[2] - r * 0.45), (c[0] + r * 0.1, c[1] - 0.03, c[2] + r * 0.15), 0.025, 0.015, "madera", up=(0, -1, 0))
    beam((c[0] - r * 0.05, c[1] - 0.04, c[2] + r * 0.45), (c[0] + r * 0.45, c[1] - 0.04, c[2] - 0.05 * r), 0.07, 0.03, "clavo", up=(0, -1, 0))


def stone_arch(cx, zb, ri, ro, squash, seg, key=True):
    """Arco de dovelas de piedra (tonos alternados) con la clave saliente."""
    for k in range(seg):
        a0, a1 = math.pi * k / seg, math.pi * (k + 1) / seg
        am = (a0 + a1) / 2
        m = "piedra_c" if k % 2 else "piedra_a"
        arc_piece(cx, zb, ri, ro, a0 + 0.02, a1 - 0.02, squash, FY - (0.06 if key and k == seg // 2 else 0.045), FY, m)


def arc_piece(cx, zb, ri, ro, a0, a1, squash, y0, y1, m):
    bm = bmesh.new()
    q = [Vector((cx + math.cos(a) * r, 0, zb + math.sin(a) * r * squash)) for a, r in ((a0, ri), (a1, ri), (a1, ro), (a0, ro))]
    vs = [bm.verts.new(p + Vector((0, y0, 0))) for p in q] + [bm.verts.new(p + Vector((0, y1, 0))) for p in q]
    for f in [(0, 1, 2, 3), (7, 6, 5, 4), (3, 2, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (0, 3, 7, 4)]: bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new("dovela"); bm.to_mesh(me); bm.free()
    return link(me, "dovela", m)


def frame_walls():
    """Entramado de madera de la lamina: soleras arriba y abajo, postes en las puntas, esquineros de hierro."""
    timber(-W / 2, W / 2, H - 0.06, 0.12)
    timber(-W / 2, W / 2, 0.055, 0.11)
    for s in (-1, 1): post(s * (W / 2 - 0.06))
    for x in (-(W / 2 - 0.07), W / 2 - 0.07):
        for z in (H - 0.06, 0.055): plate((x, FY - 0.075, z))


def build(piece):
    if piece == "pared":
        box((0, 0, H / 2), (W, T, H), "junta", 0)
        stones(-W / 2 + 0.12, W / 2 - 0.12, 0.11, H - 0.12, 7, skip=lambda x, z, L: abs(x) < 0.06 + L / 2 - 0.03 and False)
        frame_walls()
        post(0, 0.12)
        for z in (H - 0.06, 0.055): plate((0, FY - 0.075, z))
        # la cruz en Y: dos jabalcones del poste del medio a la solera de arriba
        for s in (-1, 1): beam((s * 0.05, FY - 0.04, 0.76), (s * 0.4, FY - 0.04, H - 0.11), 0.085, 0.06, "madera_b", up=(0, -1, 0))
        hammer_plaque((0, FY - 0.09, 0.56))
        lantern(-0.6, 0.6)
    elif piece == "ventana":
        box((0, 0, H / 2), (W, T, H), "junta", 0)
        wz, ww, wh = 0.5, 0.3, 0.26          # vidrio: base, ancho, alto del rectangulo
        stones(-W / 2 + 0.12, W / 2 - 0.12, 0.11, H - 0.12, 11,
               skip=lambda x, z, L: abs(x) - L / 2 < 0.26 and 0.36 < z < 1.06 or abs(x) > 0.22 and 0.6 < z < 0.74)
        frame_walls()
        # vigas cruzadas a media altura, a los dos lados de la ventana
        for s in (-1, 1): timber(s * 0.5 - 0.24, s * 0.5 + 0.24, 0.67, 0.12, "madera_b")
        # jambas de piedra y arco de dovelas
        for s in (-1, 1):
            for k in range(3): box((s * 0.215, FY - 0.03, wz + 0.04 + k * 0.09), (0.09, 0.05, 0.085), "piedra_c" if k % 2 else "piedra_a", 0)
        stone_arch(0, wz + wh, 0.17, 0.26, 1.0, 7)
        arch_window(0, wz, ww, wh, FY - 0.01, "vidrio", "madera", fw=0.035, depth=0.05, mull="madera")
        # alfeizar de madera con dos mensulas de hierro
        box((0, FY - 0.06, wz - 0.03), (0.52, 0.09, 0.06), "madera", 0.01)
        for s in (-1, 1): box((s * 0.15, FY - 0.08, wz - 0.07), (0.05, 0.05, 0.08), "hierro", 0)
        lantern(-0.62, 0.6)
        hammer_plaque((0.62, FY - 0.08, 0.5), 0.09)
    elif piece == "marco":
        door_cut_walls("junta")
        stones(-W / 2 + 0.12, -DW / 2, 0.11, H - 0.12, 3)
        stones(DW / 2, W / 2 - 0.12, 0.11, H - 0.12, 5)
        stones(-DW / 2, DW / 2, DH, H - 0.12, 9, skip=lambda x, z, L: True)
        frame_walls()
        # jambas de sillares claros y arco de dovelas sobre el hueco
        for s in (-1, 1):
            for k in range(5): box((s * (DW / 2 + 0.045), FY - 0.035, 0.1 + k * 0.17), (0.09 + 0.02 * (k % 2), 0.06, 0.16), "piedra_c" if k % 2 else "piedra_a", 0)
        box((0, FY - 0.002, (DH + H) / 2 - 0.03), (DW, 0.01, H - DH - 0.06), "piedra_b", 0)
        stone_arch(0, DH, DW / 2 - 0.02, DW / 2 + 0.09, 0.36, 7)
        # escudo de hierro con el martillo en la solera de arriba
        box((0, FY - 0.08, H - 0.08), (0.24, 0.03, 0.14), "hierro", 0)
        cone((0, FY - 0.08, H - 0.17), 0.12, 0.0, 0.04, "hierro", v=4).data.transform(Matrix.Rotation(math.pi, 4, "X") @ Matrix.Identity(4))
        hammer_plaque((0, FY - 0.1, H - 0.1), 0.07)
        lantern(-0.66, 0.6)
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for x in (0.14, 0.28, 0.42, 0.56, 0.7): box((x, -0.032, h / 2), (0.012, 0.01, h - 0.04), "madera_b", 0)
        for z in (0.2, 0.72):
            box((0.3, -0.04, z), (0.56, 0.025, 0.06), "hierro", 0.006)
            for x in (0.08, 0.3, 0.52): cyl((x, -0.055, z), 0.016, 0.015, "clavo", "Y", 6)
        box((w * 0.7, -0.045, 0.5), (0.06, 0.02, 0.07), "hierro", 0)
        ring((w * 0.7, -0.06, 0.43), 0.06, 0.014, "hierro")
    elif piece == "remate":
        # el techo es el comun (comun_techo_<mascara>); la piedra solo pone su remate de hierro
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.1, 2 * RB + 0.1, 0.07), "hierro", 0.015)
        cone((0, 0, Rz1 + 0.12), 0.18, 0.03, 0.1, "hierro", v=4).data.transform(Matrix.Rotation(math.pi / 4, 4, "Z"))


run(build, preview_mats=("junta", "madera"))
