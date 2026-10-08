"""Exporta el minero construido por build_miner.py a Unity, por piezas del esqueleto del juego (MinerModel).

Se ejecuta dentro de build_miner.py con --export <ruta.json>. Cada grupo (hips, torso, head, armL, armR, legL, legR)
se guarda relativo a su pivote y con el color de cada material en los vertices; los canales separados
("skin", "hair", "helmet", "lens") permiten variantes de piel/pelo, cascos comprables y la lente emisiva.
Ejes: Blender (X der, Y atras, Z arriba; el minero mira a -Y) -> Unity (X, Y arriba, Z adelante): (x, y, z) -> (-x, z, -y).
Los brazos se exportan colgando (rotados 90 grados sobre el hombro), que es la pose de reposo del juego.
"""
import bpy, bmesh, json, math
from mathutils import Vector, Matrix

PX = 1.0 / 614.0


def zpx(y):
    return (622.0 - y) * PX


PIVOTS = {
    "hips": Vector((0, 0, zpx(400))),
    "torso": Vector((0, 0, zpx(395))),
    "head": Vector((0, 0, zpx(200))),
    "armL": Vector((78 * PX, 0, zpx(252))),
    "armR": Vector((-78 * PX, 0, zpx(252))),
    "legL": Vector((50 * PX, 0, zpx(408))),
    "legR": Vector((-50 * PX, 0, zpx(408))),
}
PARENT = {"hips": None, "torso": "hips", "head": "torso", "armL": "torso", "armR": "torso", "legL": "hips", "legR": "hips"}
# presupuesto de triangulos por grupo (un minero ~5.5k)
BUDGET = {"head": 3600, "torso": 1500, "hips": 200, "armL": 650, "armR": 650, "legL": 600, "legR": 600}

HEAD = ("Cabeza", "Oreja", "Patilla", "Pelo", "Ojo", "Iris", "Pupila", "Brillo", "Parpado", "Ceja", "Nariz", "Boca",
        "Casco", "Ala", "Banda", "Lampara")
TORSO = ("Torso", "Escote", "Solapa", "Tapeta", "Tirador", "Cinturon", "Hebilla", "Bolsa", "Boton", "Mochila", "Cuello")
ARM = ("Manga", "Puño", "Brazo", "Guante", "Palma", "Dedo", "Pulgar")
LEG = ("Muslo", "RodillaTela", "Canilla", "Rodillera", "Bota", "Suela")


def group_of(o):
    n = o.name.split(".")[0]
    side = "L" if o.matrix_world.translation.x > 0 else "R"
    if n.startswith(HEAD):
        return "head"
    if n.startswith(ARM):
        return "arm" + side
    if n.startswith(LEG):
        return "leg" + side
    if n.startswith("Cadera"):
        return "hips"
    if n.startswith(TORSO):
        return "torso"
    return None


def channel_of(o, mat):
    mn = mat.name if mat else ""
    g = group_of(o)
    if mn == "Piel":
        return "skin"
    if mn == "Pelo":
        return "hair"
    if mn == "Lente":
        return "lens"
    if mn == "Amarillo" and g == "head":
        return "helmet"
    return "vc"


def base_color(mat):
    if not mat or not mat.use_nodes:
        return (1, 1, 1)
    b = mat.node_tree.nodes.get("Principled BSDF")
    c = b.inputs["Base Color"].default_value
    return (c[0], c[1], c[2])  # ya lineal


def export(path):
    # sin subdivision: la geometria base ya es suave; luego se diezma al presupuesto
    for o in bpy.data.objects:
        for m in getattr(o, "modifiers", []):
            if m.type == "SUBSURF":
                m.levels = 0 if o.name.split(".")[0] in ("Cabeza", "Oreja", "Nariz", "Cuello") else 1
    dg = bpy.context.evaluated_depsgraph_get()
    # bmesh por (grupo, canal)
    acc = {}
    for o in bpy.data.objects:
        if o.type not in ("MESH", "CURVE"):
            continue
        g = group_of(o)
        if g is None:
            continue
        oe = o.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(oe, depsgraph=dg)
        me.transform(o.matrix_world)
        mats = list(me.materials) or [None]
        bm = bmesh.new()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        piv = PIVOTS[g]
        bm.verts.index_update()
        bmesh.ops.translate(bm, vec=-piv, verts=bm.verts)
        if g.startswith("arm"):
            s = 1 if g == "armL" else -1
            R = Matrix.Rotation(math.radians(90 * s), 4, "Y")   # brazo hacia abajo
            bmesh.ops.transform(bm, matrix=R, verts=bm.verts)
        for f in bm.faces:
            mat = mats[f.material_index] if f.material_index < len(mats) else mats[0]
            key = (g, channel_of(o, mat))
            col = base_color(mat)
            acc.setdefault(key, []).append((f, col, bm))
        acc.setdefault("_keep", []).append(bm)

    groups = {}
    for key, faces in acc.items():
        if key == "_keep":
            continue
        g, ch = key
        out = bmesh.new()
        lay = out.verts.layers.float_color.new("col")
        vmap = {}
        for f, col, src in faces:
            vs = []
            for v in f.verts:
                k = (id(src), v.index)
                nv = vmap.get(k)
                if nv is None:
                    nv = out.verts.new(v.co)
                    nv[lay] = (col[0], col[1], col[2], 1.0)
                    vmap[k] = nv
                vs.append(nv)
            try:
                out.faces.new(vs)
            except ValueError:
                pass
        groups.setdefault(g, {})[ch] = out

    data = {"groups": []}
    total = 0
    for g, chans in groups.items():
        # diezmado al presupuesto del grupo, repartido segun el tamaño de cada canal
        # la piel de la cabeza no se diezma (malla base suave, sin pliegues); el resto se reparte el presupuesto
        keep = {ch for ch in chans if (g, ch) in (("head", "skin"),)}
        kept = sum(sum(len(f.verts) - 2 for f in chans[ch].faces) for ch in keep)
        ntri = sum(sum(len(f.verts) - 2 for f in b.faces) for ch, b in chans.items() if ch not in keep)
        ratio = min(1.0, (3400 if g == "head" else BUDGET[g]) / max(ntri, 1))
        subs = []
        for ch, b in chans.items():
            me = bpy.data.meshes.new("tmp")
            b.to_mesh(me)
            ob = bpy.data.objects.new("tmp", me)
            bpy.context.scene.collection.objects.link(ob)
            if ratio < 0.999 and ch not in keep:
                dm = ob.modifiers.new("D", "DECIMATE")
                dm.ratio = ratio
                dm.use_collapse_triangulate = True
            tm = ob.modifiers.new("T", "TRIANGULATE")
            dge = bpy.context.evaluated_depsgraph_get()
            me2 = bpy.data.meshes.new_from_object(ob.evaluated_get(dge), depsgraph=dge)
            # normales suaves por vertice con angulo (para no redondear bordes duros)
            me2.calc_loop_triangles()
            colattr = me2.color_attributes.get("col")
            pos, nrm, cols, idx = [], [], [], []
            weld = {}
            cn = me2.corner_normals
            for tri in me2.loop_triangles:
                for li in reversed(tri.loops):  # orden invertido: el cambio de ejes refleja (mano derecha -> izquierda)
                    vi = me2.loops[li].vertex_index
                    n = me2.vertices[vi].normal   # normal suave: el diezmado deja caras planas
                    k = vi
                    j = weld.get(k)
                    if j is None:
                        j = len(pos) // 3
                        weld[k] = j
                        co = me2.vertices[vi].co
                        pos += [round(-co.x, 5), round(co.z, 5), round(-co.y, 5)]
                        nrm += [round(-n.x, 4), round(n.z, 4), round(-n.y, 4)]
                        c = colattr.data[vi].color if colattr and colattr.domain == "POINT" else (1, 1, 1, 1)
                        cols += [round(c[0], 4), round(c[1], 4), round(c[2], 4)]
                    idx.append(j)
            total += len(idx) // 3
            subs.append({"channel": ch, "pos": pos, "nrm": nrm, "col": cols, "idx": idx})
            bpy.data.objects.remove(ob)
            bpy.data.meshes.remove(me2)
        p = PIVOTS[g]
        par = PARENT[g]
        rel = p - (PIVOTS[par] if par else Vector((0, 0, 0)))
        data["groups"].append({"name": g, "parent": par or "", "pivot": [-rel.x, rel.z, -rel.y], "subs": subs})
    # mano (para colgar la herramienta): distancia hombro -> centro del guante
    data["handLen"] = (276 - 78) * PX
    data["height"] = 1.0
    with open(path, "w") as f:
        json.dump(data, f, separators=(",", ":"))
    print("EXPORT", path, "tris", total)
