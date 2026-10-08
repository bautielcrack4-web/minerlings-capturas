"""Kit de habitacion COBRE para el Cuartel (RoomKit): pared, ventana, marco (pared con hueco de puerta), hoja y techo,
calcado del estilo de la lamina cobre_techo.jpg. Medidas reales del juego (IslandArtComplex): pared 1.7 x 1.25, hueco de
puerta 0.9 x 0.93 centrado abajo, techo para 2.04 x 2.04. En Blender el frente mira a -Y (RoomKit.KitFront).
Exporta cada pieza como Resources/RoomKit/cobre_<pieza>.json (+ .png de paleta) con el formato de IslandArt.TripoModel.
uso: blender -b -P kit_cobre.py -- outdir [--preview png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

# ------------------------------------------------------------------ paleta (un color por celda de la textura)
PAL = {
    "techo": (0.42, 0.085, 0.014), "borde": (0.20, 0.04, 0.008), "borde_claro": (0.45, 0.12, 0.022),
    "pared": (0.10, 0.03, 0.01), "placa_a": (0.11, 0.03, 0.009), "placa_b": (0.15, 0.04, 0.012),
    "bronce": (0.50, 0.26, 0.035), "madera": (0.28, 0.085, 0.025), "brasa": (1.0, 0.62, 0.12),
    "dial": (0.88, 0.82, 0.68), "oscuro": (0.03, 0.015, 0.008), "rojo": (0.7, 0.08, 0.03),
    "pizarra_a": hexc(SLATE["pizarra_a"]), "pizarra_b": hexc(SLATE["pizarra_b"]),
}
# la brasa late (fila emisiva); las agujas de los manometros las dibuja y mueve el juego (anclas "aguja")
setup("cobre", PAL, glow={"brasa": "late"}, rivet="borde_claro")

def gauge(c, r=0.11):
    c = Vector(c)
    cyl(c, r, 0.05, "bronce", "Y", 12)
    cyl(c + Vector((0, -0.028, 0)), r * 0.78, 0.008, "dial", "Y", 12)
    cyl(c + Vector((0, -0.034, 0)), 0.014, 0.006, "oscuro", "Y", 6)
    beam(c + Vector((0, -0.033, r * 0.5)), c + Vector((r * 0.45, -0.033, r * 0.3)), 0.02, 0.004, "rojo", up=(0, -1, 0))   # zona roja
    anchor("aguja", c + Vector((0, -0.036, 0)), (0, -1, 0))


def wall_frame(skip_center=False):
    """Bandas de cobre de la pared: zocalo, viga de arriba, costura del medio y remaches."""
    box((0, FY - 0.02, 0.06), (W, 0.05, 0.12), "borde_claro")
    box((0, FY - 0.02, H - 0.06), (W, 0.05, 0.12), "borde")
    for x in (-W / 2 + 0.05, W / 2 - 0.05): box((x, FY - 0.02, H / 2), (0.1, 0.05, H), "borde")
    for x in (-0.8, -0.55, -0.25, 0.25, 0.55, 0.8): rivet((x, FY - 0.05, H - 0.06)); rivet((x, FY - 0.05, 0.06))
    for z in (0.3, 0.62, 0.95): rivet((-W / 2 + 0.05, FY - 0.05, z)); rivet((W / 2 - 0.05, FY - 0.05, z))

def plates(x0, x1, z0=0.12, z1=H - 0.12, cols=2, rows=2):
    """Placas oscuras en relieve en el rectangulo [x0,x1]x[z0,z1] con sus remaches."""
    w = (x1 - x0) / cols; hh = (z1 - z0) / rows
    for i in range(cols):
        for j in range(rows):
            c = (x0 + w * (i + 0.5), FY - 0.012, z0 + hh * (j + 0.5))
            box(c, (w - 0.04, 0.025, hh - 0.04), "placa_a" if (i + j) % 2 else "placa_b", 0.01)
            for sx in (-1, 1):
                for sz in (-1, 1): rivet((c[0] + sx * (w / 2 - 0.06), FY - 0.03, c[2] + sz * (hh / 2 - 0.06)), 0.018)

def build(piece):
    if piece in ("pared", "ventana"):
        box((0, 0, H / 2), (W, T, H), "pared", 0)
        plates(-W / 2 + 0.1, W / 2 - 0.1)
        wall_frame()
        if piece == "pared":
            # caño que recorre la pared con un manometro (el sello del cobre)
            pipe([(-W / 2 + 0.12, FY - 0.08, H - 0.2), (0.25, FY - 0.08, H - 0.2), (0.25, FY - 0.08, 0.45), (0.6, FY - 0.08, 0.45), (0.6, FY - 0.08, 0.12)])
            gauge((-0.3, FY - 0.09, 0.8))
            cyl((0.25, FY - 0.13, H - 0.2), 0.03, 0.06, "bronce", "Y", 8)            # boquilla del vapor
            anchor("vapor", (0.25, FY - 0.17, H - 0.2), (0, -0.6, 0.8))
            pipe([(-0.3, FY - 0.08, H - 0.2), (-0.3, FY - 0.08, 0.92)])
        else:
            # ventana = rejilla incandescente con marco de bronce y barrotes (la cara izquierda de la lamina)
            box((0, FY - 0.04, 0.62), (0.62, 0.06, 0.5), "bronce", 0.02)
            box((0, FY - 0.06, 0.62), (0.52, 0.02, 0.4), "brasa", 0)
            for k in range(5): box((-0.2 + k * 0.1, FY - 0.085, 0.62), (0.035, 0.03, 0.42), "bronce", 0)
            for sx in (-1, 1):
                for sz in (-1, 1): rivet((sx * 0.27, FY - 0.08, 0.62 + sz * 0.2), 0.02)
            pipe([(-W / 2 + 0.12, FY - 0.08, H - 0.2), (W / 2 - 0.12, FY - 0.08, H - 0.2)])
            gauge((0.55, FY - 0.09, H - 0.38), 0.09)
            cyl((-0.55, FY - 0.13, H - 0.2), 0.03, 0.06, "bronce", "Y", 8)
            anchor("vapor", (-0.55, FY - 0.17, H - 0.2), (0, -0.6, 0.8))
    elif piece == "marco":
        DW, DH = 0.9, 0.93
        side = (W - DW) / 2
        for s in (-1, 1): box((s * (DW / 2 + side / 2), 0, H / 2), (side, T, H), "pared", 0)
        box((0, 0, DH + (H - DH) / 2), (DW, T, H - DH), "pared", 0)
        for s in (-1, 1): plates(s * (DW / 2 + 0.1) if s > 0 else -W / 2 + 0.1, W / 2 - 0.1 if s > 0 else -DW / 2 - 0.1, cols=1)
        wall_frame()
        # marco de bronce en arco alrededor del hueco
        for s in (-1, 1): box((s * (DW / 2 + 0.04), FY - 0.03, DH / 2), (0.08, 0.06, DH), "bronce", 0.015)
        box((0, FY - 0.03, DH + 0.04), (DW + 0.16, 0.06, 0.08), "bronce", 0.015)
        bm = bmesh.new()   # media corona del arco sobre el dintel
        ri, ro, seg = DW / 2, DW / 2 + 0.08, 12
        for k in range(seg):
            a0, a1 = math.pi * k / seg, math.pi * (k + 1) / seg
            q = [Vector((math.cos(a) * r, 0, DH + 0.08 + math.sin(a) * r * 0.42)) for a, r in ((a0, ri), (a1, ri), (a1, ro), (a0, ro))]
            vs = [bm.verts.new(p + Vector((0, FY - 0.06, 0))) for p in q] + [bm.verts.new(p + Vector((0, FY, 0))) for p in q]
            for f in [(0, 1, 2, 3), (7, 6, 5, 4), (3, 2, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (0, 3, 7, 4)]: bm.faces.new([vs[i] for i in f])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        me = bpy.data.meshes.new("arco"); bm.to_mesh(me); bm.free()
        link(me, "arco", "bronce")
        # rejilla encendida sobre la puerta
        box((0, FY - 0.03, H - 0.2), (0.4, 0.05, 0.14), "bronce", 0.01)
        box((0, FY - 0.05, H - 0.2), (0.32, 0.02, 0.08), "brasa", 0)
        for k in range(2): box((0, FY - 0.07, H - 0.225 + k * 0.05), (0.32, 0.015, 0.018), "bronce", 0)
        gauge((DW / 2 + 0.2, FY - 0.09, 0.72), 0.09)
        pipe([(DW / 2 + 0.2, FY - 0.08, H - 0.15), (DW / 2 + 0.2, FY - 0.08, 0.82)])
    elif piece == "hoja":
        w, h = 0.84, 0.93      # bisagra en x = 0, la hoja se extiende hacia +X
        box((w / 2, 0, h / 2), (w, 0.06, h), "madera", 0.012)
        for z in (0.18, 0.58): box((w / 2, -0.04, z), (w, 0.025, 0.07), "borde_claro", 0.008)
        for x in (0.2, 0.42, 0.64): box((x, -0.035, h / 2), (0.012, 0.012, h - 0.04), "oscuro", 0)
        ring((w / 2, -0.045, 0.74), 0.1, 0.025, "bronce")
        cyl((w / 2, -0.04, 0.74), 0.085, 0.02, "brasa", "Y", 12)
        box((w / 2, -0.055, 0.74), (0.17, 0.015, 0.02), "bronce", 0); box((w / 2, -0.055, 0.74), (0.02, 0.015, 0.17), "bronce", 0)
        box((w - 0.1, -0.05, 0.45), (0.05, 0.03, 0.14), "bronce", 0.006)
        for z in (0.18, 0.58): box((0.07, -0.05, z), (0.12, 0.02, 0.09), "bronce", 0.006)
    elif piece == "remate":
        # el techo es el comun (comun_techo_<mascara>); el cobre pone la chimenea de calderas con remaches
        z1 = Rz1 - 0.03
        box((0, 0, Rz1 + 0.04), (2 * RB + 0.1, 2 * RB + 0.1, 0.07), "borde_claro", 0.015)
        cyl((0, 0, z1 + 0.05), 0.38, 0.12, "borde_claro", v=16)
        cyl((0, 0, z1 + 0.14), 0.29, 0.07, "borde", v=16)
        for k in range(8):
            ang = k * math.pi / 4; rivet((math.cos(ang) * 0.35, math.sin(ang) * 0.35, z1 + 0.11), 0.024)
        cyl((0, 0, z1 + 0.26), 0.14, 0.18, "borde_claro", v=12)
        cyl((0, 0, z1 + 0.355), 0.09, 0.015, "oscuro", v=10)


run(build)
