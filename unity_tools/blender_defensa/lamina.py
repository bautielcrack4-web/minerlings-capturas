"""Lamina de estilo (control de calidad antes de producir todo).

Uso: blender -b -P lamina.py -- heroes|pesadillas|tablero [--samples 48] [--out ruta.png] [--heroes a,b,c]
- heroes:     3 heroes x 7 niveles de fusion, cada uno sobre su peana con sus estrellitas.
- pesadillas: las pesadillas en fila (caminando).
- tablero:    el tablero del escritorio con utileria, velador y placard (luz de noche).
"""
import sys, os, math, argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, heroes, utileria, enemigos, tableros, mazmorra
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("what")
ap.add_argument("--samples", type=int, default=48)
ap.add_argument("--out", default="")
ap.add_argument("--heroes", default="lancero,arquera,mago")
ap.add_argument("--rareza", default="comun,comun,comun")
ap.add_argument("--scale", type=float, default=1.0)
ap.add_argument("--ids", default="")
ap.add_argument("--zona", default="calabozo")
a = ap.parse_args(argv)


def group_from(fn, offset, rot_z=0.0, scale=1.0, bones=("raiz",)):
    """Arma algo con fn (sobre huesos nuevos), lo copia horneado en offset y borra las piezas."""
    kit.BONES.clear(); kit.BONE_ORDER.clear(); kit.PARTS.clear()
    for b in bones: kit.bone(b, None, (0, 0, 0))
    with kit.on(bones[0]): fn()
    out = kit.duplicate_group(kit.all_parts(), offset, rot_z, scale)
    kit.clear_parts()
    return out


def stars_row(n, center, r=0.3, z=0.09):
    """n estrellitas en arco sobre el frente de la peana."""
    for i in range(n):
        t = (i - (n - 1) / 2) * 0.3
        x = center.x + math.sin(t) * r; y = center.y - math.cos(t) * r
        group_from(utileria.estrellita, (x, y, center.z + z), rot_z=math.degrees(t), scale=1.25)


def floor(col_hex="2f2860", size=60, z=0.0):
    import bpy
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, z))
    p = bpy.context.active_object
    m = bpy.data.materials.new("piso"); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (*kit.lin(kit.srgb(col_hex)), 1)
    b.inputs["Roughness"].default_value = 0.8
    p.data.materials.append(m)
    return p


def sheet_heroes():
    kit.reset()
    heroes.AURA_MESH = True     # en la lamina se dibuja el aura (en el juego es un efecto)
    ids = a.heroes.split(","); rar = a.rareza.split(",")
    DX, DY = 0.95, 1.45
    for row, hid in enumerate(ids):
        for lv in range(1, 8):
            kit.BONES.clear(); kit.BONE_ORDER.clear(); kit.PARTS.clear()
            heroes.skeleton()
            heroes.build(hid, lv)
            k = len(ids) - 1 - row
            x = (lv - 4) * DX; y = k * 1.1; z = k * 1.05
            c = Vector((x, y, z))
            kit.prep_render()
            kit.duplicate_group(kit.all_parts(), c + Vector((0, 0, 0.066)), rot_z=-12, scale=0.8)
            kit.clear_parts()
            group_from(lambda: utileria.peana(rar[row]), c)
            stars_row(lv, c)
    import bpy
    for row in range(len(ids)):        # estante escalonado (vidriera de jugueteria)
        k = len(ids) - 1 - row
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, k * 1.1 + 0.2, k * 1.05 - 0.5 - 0.0))
        st = bpy.context.active_object; st.scale = (8.2, 1.5, 1.0)
        bv = st.modifiers.new("b", "BEVEL"); bv.width = 0.06; bv.segments = 3
        m = bpy.data.materials.new("estante%d" % row); m.use_nodes = True
        bb = m.node_tree.nodes["Principled BSDF"]; bb.inputs["Base Color"].default_value = (*kit.lin(kit.srgb(["8a8792", "7d7a86", "6f6c78"][row % 3])), 1)
        bb.inputs["Roughness"].default_value = 0.7
        st.data.materials.append(m)
    floor("2b2a33", z=-1.0)
    kit.night_world(0.5, "6f7690")
    kit.studio_lights(2.2)
    kit.light("velador", "POINT", (3.5, -3.0, 2.5), 900, "ffd89a", size=1.5)
    cam_t = Vector((0, (len(ids) - 1) * 0.55, (len(ids) - 1) * 0.525 + 0.42))
    d = Vector((0, -math.cos(math.radians(14)), math.sin(math.radians(14))))
    kit.camera(cam_t + d * 40, cam_t, ortho=6.9)
    out = a.out or os.path.join(kit.OUT_DOCS, "lamina_heroes.png")
    kit.render(out, 2100, 1450, a.samples)


def sheet_enemies():
    kit.reset()
    ids = a.ids.split(",") if a.ids else ["slime", "slime_grande", "diablillo", "murcielago", "esqueleto", "espectro", "goblin", "arana"]
    gap = 0.95 if not a.ids else 1.9
    for i, eid in enumerate(ids):
        x = (i - (len(ids) - 1) / 2) * gap
        fr = {"murcielago": 3, "diablillo": 6}.get(eid, 4)
        kit.bake(lambda: enemigos.ENEMIES[eid](), kit.Matrix.Translation((x, 0, 0.0 + (0.35 if eid in ("murcielago", "espectro") else 0))), frame=fr, bones_raiz=False)
    import bpy
    p = floor("8a8792", z=0.0, size=40)
    kit.night_world(0.5, "6f7690")
    kit.studio_lights(2.2)
    kit.light("velador", "POINT", (3.0, -3.0, 2.5), 700, "ffd89a", size=1.5)
    cam_t = Vector((0, 0, 0.35))
    d = Vector((0, -math.cos(math.radians(22)), math.sin(math.radians(22))))
    kit.camera(cam_t + d * 40, cam_t, ortho=8.0 if not a.ids else 6.6)
    out = a.out or os.path.join(kit.OUT_DOCS, "lamina_pesadillas.png")
    kit.render(out, 2100, 700, a.samples)


def sheet_board():
    """El tablero del escritorio de noche, con la camara del juego (vertical), juguetes y pesadillas de muestra."""
    kit.reset()
    cap = a.zona
    import bpy, json
    I = kit.Matrix.Identity(4)
    kit.bake(lambda: (tableros.surface(tableros.CAPS[cap]), tableros.board(tableros.CAPS[cap])), I)
    for d in tableros.DRESS[cap]:
        name = d["m"][2:]
        p = d["p"]
        M = kit.unity_trs_to_blender((p[0], p[2], p[1]), d.get("rx", 0), d.get("r", 0), d.get("rz", 0), d.get("s", 1))
        kit.bake(mazmorra.EXPORTS[name] if name in mazmorra.EXPORTS else utileria.EXPORTS[name], M)
    PX, PT = tableros.PX, tableros.PT
    kit.bake(lambda: (kit.bone("luz", "raiz", (0, 0, 0.52)), mazmorra.cristal()), kit.Matrix.Translation((PX, PT + 0.15, 0.2)))
    kit.bake(lambda: mazmorra.porton(), kit.Matrix.Translation((-PX, PT + 0.35, 0.2)) @ kit.Matrix.Scale(0.85, 4))
    # juguetes en algunos casilleros
    C = tableros.CELL
    for (col, row, hid, lv) in ((0, 0, "soldadito", 2), (1, 1, "arquera", 3), (2, 0, "mago", 1), (0, 3, "soldadito", 5), (2, 2, "mago", 4), (1, 4, "arquera", 6)):
        if hid not in heroes.HEROES: continue
        x = (col - 1) * C; y = (2 - row) * C
        kit.bake(lambda: utileria.peana("comun"), kit.Matrix.Translation((x, y, 0.215)) @ kit.Matrix.Scale(1.45, 4))
        def mk(hid=hid, lv=lv):
            heroes.skeleton(); heroes.build(hid, lv)
        kit.bake(mk, kit.Matrix.Translation((x, y, 0.215 + 0.095)) @ kit.Matrix.Scale(1.05, 4), bones_raiz=False)
    # pesadillas sobre la pista
    for (eid, x, y, ang) in (("slime", -PX, 2.0, 0), ("slime", -PX, 1.2, 0), ("slime_grande", -PX, -0.6, 0), ("diablillo", -1.0, tableros.PB, 90),
                             ("murcielago", 1.0, tableros.PB, 90), ("esqueleto", PX, -1.0, 180)):
        M = kit.Matrix.Translation((x, y, 0.21 + (0.5 if eid == "murcielago" else 0))) @ kit.Matrix.Rotation(math.radians(ang), 4, "Z") @ kit.Matrix.Scale(1.3, 4)
        kit.bake(lambda eid=eid: enemigos.ENEMIES[eid](), M, frame=4, bones_raiz=False)
    # luz: velador dorado (circulo calido sobre el tablero) + luna azul violeta desde la ventana + ambiente violeta
    kit.dungeon_lights(PX, PT)
    # la camara del juego: lente de diorama (28 grados vertical), 57 grados de inclinacion, tablero al 92% del ancho
    tgt = Vector((0, 0.1, 0.2))
    d = Vector((0, -math.cos(math.radians(57)), math.sin(math.radians(57))))
    dist = 3.5 / (math.tan(math.radians(14)) * 1080 / 2340)
    cam = kit.camera(tgt + d * dist, tgt, lens=18 / math.tan(math.radians(14)))
    cam.data.sensor_fit = "VERTICAL"; cam.data.sensor_height = 36
    cam.data.dof.use_dof = False      # sin desenfoque: el arte tiene que leerse nitido
    out = a.out or os.path.join(kit.OUT_DOCS, "lamina_tablero.png")
    kit.render(out, 1080, 2340, a.samples)


if __name__ == "__main__":
    if a.what == "heroes": sheet_heroes()
    if a.what == "pesadillas": sheet_enemies()
    if a.what == "tablero": sheet_board()
