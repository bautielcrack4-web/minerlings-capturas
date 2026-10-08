"""Guardianes del Cristal: icono de la app y arte de la tienda, renderizados en Blender (escena armada por codigo).

Uso: blender -b -P tienda.py -- [icono|portada|todo] [--samples 64]
- icono:   1024x1024 -> unity_defensa/Assets/Icon/app_icon.png (el Lancero nivel 6 frente al Cristal del Corazon, un
           slime espiando, piso de piedra y luz de antorcha).
- portada: 1024x500 -> unity_defensa/docs/tienda/portada.png (la sala del calabozo con el mazo inicial).
"""
import sys, os, math, argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, heroes, enemigos, utileria, tableros, mazmorra
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("what", nargs="?", default="todo")
ap.add_argument("--samples", type=int, default=64)
a = ap.parse_args(argv)
T = Matrix.Translation
S = lambda k: Matrix.Scale(k, 4)
R = lambda deg: Matrix.Rotation(math.radians(deg), 4, "Z")


def hero_at(hid, lv, M, frame=0):
    def mk():
        heroes.skeleton(hid); heroes.anims(hid); heroes.build(hid, lv)
    kit.bake(mk, M, frame=frame, bones_raiz=False)


def background(col_top="2a2350", col_bot="5b3fa8"):
    import bpy
    w = bpy.data.worlds.new("fondo"); kit.sc.world = w; w.use_nodes = True
    nt = w.node_tree; bg = nt.nodes["Background"]
    grad = nt.nodes.new("ShaderNodeTexGradient"); tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Rotation"].default_value = (0, math.radians(90), 0)
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*kit.lin(kit.srgb(col_bot)), 1); ramp.color_ramp.elements[1].color = (*kit.lin(kit.srgb(col_top)), 1)
    nt.links.new(tc.outputs["Window"], mp.inputs["Vector"]); nt.links.new(mp.outputs["Vector"], grad.inputs["Vector"])
    nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.0


def icon():
    kit.reset()
    # piso: losas de piedra en un disco
    kit.bake(lambda: kit.lathe((0, 0, -0.06), [(0.0, 0.0), (1.6, 0.0), (1.62, 0.06), (0.0, 0.06)], "piedra", seg=48), Matrix.Identity(4))
    kit.bake(lambda: (kit.bone("luz", "raiz", (0, 0, 0.52)), mazmorra.cristal()), T((0.55, 0.4, 0.0)) @ S(1.0))
    kit.bake(lambda: utileria.peana("raro"), T((-0.32, -0.25, 0.0)) @ S(1.3))
    hero_at("lancero", 6, T((-0.32, -0.25, 0.07)) @ R(18) @ S(1.25), frame=15)
    kit.bake(lambda: enemigos.ENEMIES["slime"](), T((0.95, -0.55, 0.0)) @ R(-35) @ S(1.4), frame=4, bones_raiz=False)
    background("15131c", "3d4a70")
    # resplandor azul del cristal detras (disco emisivo grande, suave)
    import bpy
    bpy.ops.mesh.primitive_circle_add(vertices=48, radius=2.2, fill_type="NGON", location=(0.6, 2.4, 1.0), rotation=(math.radians(90), 0, 0))
    halo = bpy.context.active_object
    hm = bpy.data.materials.new("halo"); hm.use_nodes = True
    nt = hm.node_tree; em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*kit.lin(kit.srgb("6ab6ff")), 1)
    tc = nt.nodes.new("ShaderNodeTexCoord"); ln = nt.nodes.new("ShaderNodeVectorMath"); ln.operation = "LENGTH"
    rp = nt.nodes.new("ShaderNodeMapRange"); rp.inputs["From Min"].default_value = 0.0; rp.inputs["From Max"].default_value = 2.2
    rp.inputs["To Min"].default_value = 0.9; rp.inputs["To Max"].default_value = 0.0
    tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(tc.outputs["Object"], ln.inputs[0]); nt.links.new(ln.outputs["Value"], rp.inputs["Value"])
    nt.links.new(rp.outputs["Result"], mx.inputs["Fac"]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2])
    em.inputs["Strength"].default_value = 1.2
    nt.links.new(mx.outputs[0], nt.nodes["Material Output"].inputs["Surface"])
    halo.data.materials.append(hm)
    kit.light("cristal", "POINT", (0.55, 0.4, 1.0), 220, "6ab6ff", size=0.3)
    kit.light("antorcha", "AREA", (-1.8, -2.2, 2.6), 300, "ffd9a0", size=2.0, target=(0, 0, 0.4))
    kit.light("borde", "AREA", (2.4, 1.6, 2.0), 380, "9cc0ff", size=2.0, target=(0, 0, 0.4))
    tgt = Vector((0.12, -0.05, 0.62))
    d = Vector((math.sin(math.radians(12)), -math.cos(math.radians(12)), math.sin(math.radians(20)))).normalized()
    kit.camera(tgt + d * 4.3, tgt, lens=50)
    out = os.path.join(kit.UNITY, "Assets", "Icon", "app_icon.png")
    kit.render(out, 1024, 1024, a.samples)


def portada():
    kit.reset()
    cap = "calabozo"
    kit.bake(lambda: (tableros.surface(tableros.ZONAS[cap]), tableros.board(tableros.ZONAS[cap])), Matrix.Identity(4))
    for d in tableros.DRESS[cap]:
        p = d["p"]
        M = kit.unity_trs_to_blender((p[0], p[2], p[1]), d.get("rx", 0), d.get("r", 0), d.get("rz", 0), d.get("s", 1))
        kit.bake(mazmorra.EXPORTS[d["m"][2:]], M)
    PX, PT = tableros.PX, tableros.PT
    kit.bake(lambda: (kit.bone("luz", "raiz", (0, 0, 0.52)), mazmorra.cristal()), T((PX, PT + 0.15, 0.2)))
    kit.bake(lambda: mazmorra.porton(), T((-PX, PT + 0.35, 0.2)) @ S(0.85))
    C = tableros.CELL
    for (col, row, hid, lv) in ((0, 1, "lancero", 5), (1, 1, "arquera", 6), (2, 1, "mago", 7), (0, 2, "enano", 3), (2, 2, "barbaro", 4)):
        x = (col - 1) * C; y = (2 - row) * C
        kit.bake(lambda: utileria.peana("comun" if hid != "barbaro" else "raro"), T((x, y, 0.2)) @ S(1.2))
        hero_at(hid, lv, T((x, y, 0.26)) @ S(1.5), frame=10)
    for (eid, x, y, ang) in (("slime", -PX, 1.8, 0), ("slime", -PX, 1.0, 0), ("slime_grande", -PX, -0.6, 0), ("diablillo", -1.0, tableros.PB, 90),
                             ("murcielago", 1.2, tableros.PB, 90)):
        kit.bake(lambda eid=eid: enemigos.ENEMIES[eid](), T((x, y, 0.16 + (0.5 if eid == "murcielago" else 0))) @ R(ang) @ S(1.3), frame=4, bones_raiz=False)
    kit.dungeon_lights(PX, PT)
    tgt = Vector((0.0, 1.2, 0.4))
    d = Vector((0, -math.cos(math.radians(40)), math.sin(math.radians(40))))
    kit.camera(tgt + d * 11.5, tgt, lens=38)
    out = os.path.join(kit.OUT_DOCS, "tienda", "portada.png")
    kit.render(out, 1024, 500, a.samples)


if __name__ == "__main__":
    if a.what in ("icono", "todo"): icon()
    if a.what in ("portada", "todo"): portada()
