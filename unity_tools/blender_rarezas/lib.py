"""Biblioteca de modelado de Curio Barn (Rarezas) para Blender 4.2 por scripts.

Estilo: "diorama de juguete al sol" (unity_miner/docs/BIBLIA_VISUAL.md): formas gorditas con biseles generosos,
colores de la paleta, nada filoso. Cada modelo se arma con primitivas + bevel + subdivision, se le hornea la oclusion
ambiental a los vertices (Cycles) y se exporta a .bytes (formato propio, ver export()) que Unity carga directo
(Rarezas.View.MeshPack) con el shader toon de color por vertice de Minerlings.

Uso tipico:
    import lib
    lib.reset()
    o = lib.rbox("cuerpo", (0, 0, 0.2), (0.4, 0.3, 0.4), r=0.08, col="a8703f")
    lib.finish([o], "item_x", out_dir)
"""
import bpy, bmesh, math, os, struct
from mathutils import Vector, Matrix, Euler

PAL = {
    "grass": "8cc247", "grassd": "76ab3a", "dirt": "d9b07a", "dirtd": "b98a55",
    "wall": "f6ead2", "walld": "dcc8a4", "wood": "a8703f", "woodd": "7a4a2a", "woodl": "c98d55",
    "red": "e0594a", "redd": "c4483b", "blue": "4a8fe0", "green": "5cbf63", "stone": "9aa1aa", "stoned": "6d747e",
    "gold": "ffd23a", "gem": "b06ef0", "ink": "2b2440", "coin": "ffcf3d", "coind": "e09a1c", "cream": "fff6e4",
    "hay": "f0cf6a", "hayd": "d9ad45", "white": "fbf6ee", "black": "2a2630", "teal": "3fbfbf", "pink": "ff5d8f",
    "orange": "f59a32", "purple": "9a6ac8", "glass": "8fd8c8", "brass": "d9a441", "copper": "c9773f",
}


def hexcol(h):
    h = PAL.get(h, h).lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
    return (r, g, b)


def srgb_to_lin(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


# ---------------------------------------------------------------- escena
def reset():
    _mats.clear()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 24
    sc.unit_settings.scale_length = 1.0


_mats = {}


def mat(col, rough=0.6, metal=0.0, emis=0.0, plaid=None):
    """plaid = (oscuro, claro, paso): cuadrille (camisa a cuadros) pintado por esquina al hornear."""
    key = (col, rough, metal, emis, plaid)
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new("M_" + str(col))
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    c = srgb_to_lin(hexcol(col))
    p.inputs["Base Color"].default_value = (*c, 1.0)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emis > 0:
        p.inputs["Emission Color"].default_value = (*c, 1.0)
        p.inputs["Emission Strength"].default_value = emis
    m["base_srgb"] = hexcol(col)
    m["spec"] = 1.0 - rough
    m["emis"] = emis
    if plaid:
        m["plaid_dark"] = hexcol(plaid[0])
        m["plaid_light"] = hexcol(plaid[1])
        m["plaid_step"] = plaid[2]
    _mats[key] = m
    return m


def _link(o, coll=None):
    (coll or bpy.context.scene.collection).objects.link(o)
    return o


def _obj_from_bm(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    return _link(o)


def setmat(o, col, **kw):
    o.data.materials.clear()
    o.data.materials.append(mat(col, **kw))
    return o


def place(o, loc=(0, 0, 0), rot=(0, 0, 0), scale=None):
    o.location = Vector(loc)
    o.rotation_euler = Euler(tuple(math.radians(a) for a in rot))
    if scale is not None:
        o.scale = Vector(scale if hasattr(scale, "__len__") else (scale, scale, scale))
    return o


def bevel(o, w, seg=3, limit=None):
    m = o.modifiers.new("bisel", "BEVEL")
    m.width = w
    m.segments = seg
    m.limit_method = "ANGLE" if limit is None else limit
    m.angle_limit = math.radians(30)
    m.harden_normals = False
    return o


def subsurf(o, lv=2):
    m = o.modifiers.new("sub", "SUBSURF")
    m.levels = lv
    m.render_levels = lv
    return o


def dense(o, cuts=2):
    """Subdivision simple (no cambia la forma): mas vertices para pintar dibujos por esquina (cuadrille)."""
    m = o.modifiers.new("denso", "SUBSURF")
    m.subdivision_type = "SIMPLE"
    m.levels = cuts
    m.render_levels = cuts
    return o


def smooth(o, angle=40):
    for p in o.data.polygons:
        p.use_smooth = True
    m = o.modifiers.new("arista", "EDGE_SPLIT")
    m.split_angle = math.radians(angle)
    return o


# ---------------------------------------------------------------- primitivas (z hacia arriba, frente hacia -Y)
def rbox(name, loc, size, r=0.05, col="wood", seg=3, rot=(0, 0, 0), **kw):
    """Caja redondeada: size = (ancho x, fondo y, alto z) total; loc = centro."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    o = _obj_from_bm(name, bm)
    setmat(o, col, **kw)
    bevel(o, min(r, min(size) * 0.49), seg)
    smooth(o)
    return place(o, loc, rot)


def sphere(name, loc, r, col, seg=None, rings=None, scale=None, rot=(0, 0, 0), **kw):
    # segmentos segun el tamaño (presupuesto movil: 2-4k triangulos por objeto suelto)
    if seg is None:
        k = r * (max(scale) if scale is not None and hasattr(scale, "__len__") else 1.0)
        seg = max(10, min(24, int(9 + k * 90)))
    if rings is None:
        rings = max(6, seg // 2 + 1)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    o = _obj_from_bm(name, bm)
    setmat(o, col, **kw)
    smooth(o, 80)
    return place(o, loc, rot, scale)


def cyl(name, loc, r, h, col, seg=24, r2=None, bev=0.0, rot=(0, 0, 0), cap=True, **kw):
    """Cilindro (o cono truncado) de base en loc (z) y altura h."""
    bm = bmesh.new()
    r2 = r if r2 is None else r2
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r, radius2=max(r2, 1e-4), depth=h)
    for v in bm.verts:
        v.co.z += h * 0.5
    o = _obj_from_bm(name, bm)
    setmat(o, col, **kw)
    if bev > 0:
        bevel(o, bev, 3)
    smooth(o, 50)
    return place(o, loc, rot)


def lathe(name, loc, prof, col, seg=32, rot=(0, 0, 0), **kw):
    """Revolucion de un perfil [(r, z), ...] de abajo hacia arriba alrededor de Z. Cierra con tapas si r=0 en los
    extremos."""
    bm = bmesh.new()
    rings = []
    for (r, z) in prof:
        ring = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            try:
                bm.faces.new((a[i], a[j], b[j], b[i]))
            except ValueError:
                pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _obj_from_bm(name, bm)
    setmat(o, col, **kw)
    for p in o.data.polygons:
        p.use_smooth = True
    m = o.modifiers.new("arista", "EDGE_SPLIT")
    m.split_angle = math.radians(55)
    return place(o, loc, rot)


def torus(name, loc, R, r, col, seg=32, mseg=12, rot=(0, 0, 0), scale=None, **kw):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=mseg)
    o = bpy.context.active_object
    o.name = name
    setmat(o, col, **kw)
    smooth(o, 80)
    return place(o, loc, rot, scale)


def tube(name, pts, r, col, seg=10, **kw):
    """Tubo por una polilinea (cuerdas, asas, mangos curvos)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    sp = cu.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        sp.points[i].co = (*p, 1.0)
    cu.bevel_depth = r
    cu.bevel_resolution = max(2, seg // 4)
    cu.use_fill_caps = True
    o = bpy.data.objects.new(name, cu)
    _link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    setmat(o, col, **kw)
    smooth(o, 70)
    return o


def blob(name, loc, r, col, seed=1, amp=0.12, scale=None, **kw):
    """Esfera con ruido suave (copas de arbol, rocas, paja)."""
    k = r * (max(scale) if scale is not None and hasattr(scale, "__len__") else 1.0)
    seg = max(12, min(22, int(10 + k * 16)))
    o = sphere(name, loc, r, col, seg=seg, rings=seg // 2 + 2, scale=scale, **kw)
    tex = bpy.data.textures.new(name + "_n", "CLOUDS")
    tex.noise_scale = 0.6 * r
    d = o.modifiers.new("ruido", "DISPLACE")
    d.texture = tex
    d.strength = amp * r
    d.texture_coords = "GLOBAL"
    tex.noise_basis = "BLENDER_ORIGINAL"
    o.modifiers.move(len(o.modifiers) - 1, 0)
    return o


def cap(o, center, r, y_cut=0.45, z_cut=-0.05):
    """Casquete de pelo o gorra: borra la parte de adelante y abajo de una esfera para dejar la cara libre.
    Trabaja en coordenadas de Unity: frente +Z (= Blender -Y), arriba +Y (= Blender +Z)."""
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)
    mw = o.matrix_world
    kill = []
    for f in bm.faces:
        c = mw @ f.calc_center_median()
        uy = (c.z - center[2]) / r      # arriba
        uz = (-(c.y - center[1])) / r   # frente
        if uy < y_cut and uz > z_cut:
            kill.append(f)
        elif uy < -0.35:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(me)
    bm.free()
    m = o.modifiers.new("grosor", "SOLIDIFY")
    m.thickness = 0.015
    m.offset = 1.0
    return o


def join(objs, name):
    """Aplica modificadores y une en un solo objeto (conserva materiales)."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers):
            try:
                bpy.ops.object.modifier_apply(modifier=m.name)
            except RuntimeError:
                o.modifiers.remove(m)
    bpy.context.view_layer.objects.active = objs[0]
    for o in objs:
        o.select_set(True)
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    # tambien la posicion: el exportador escribe coordenadas locales, y el origen de la union quedaba donde estaba la
    # primera pieza (todo salia corrido esa distancia respecto de su pivote: gorras dentro del cuerpo, techos bajos)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


# ---------------------------------------------------------------- colores por vertice + oclusion horneada
def bake_colors(o, ao_strength=0.55, ao_dist=0.25, samples=24):
    """Color de cada esquina = color del material x oclusion ambiental horneada (sRGB, atributo 'Col')."""
    me = o.data
    if "AO" in me.color_attributes:
        me.color_attributes.remove(me.color_attributes["AO"])
    ao = me.color_attributes.new("AO", "BYTE_COLOR", "CORNER")
    me.color_attributes.active_color = ao
    sc = bpy.context.scene
    sc.cycles.samples = samples
    sc.render.bake.target = "VERTEX_COLORS"
    sc.world = sc.world or bpy.data.worlds.new("W")
    sc.world.light_settings.distance = ao_dist
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    try:
        bpy.ops.object.bake(type="AO")
        ok = True
    except RuntimeError as e:
        print("bake fallo:", e)
        ok = False
    cols = []
    mats = [m for m in me.materials]
    aod = ao.data
    verts = me.vertices
    for poly in me.polygons:
        m = mats[poly.material_index] if mats else None
        base = m["base_srgb"] if m is not None and "base_srgb" in m else (0.8, 0.8, 0.8)
        plaid = m is not None and "plaid_step" in m
        for li in poly.loop_indices:
            a = aod[li].color[0] if ok else 1.0
            k = 1.0 - ao_strength * (1.0 - a)
            c = base
            if plaid:
                c = _plaid(verts[me.loops[li].vertex_index].co, base, m["plaid_dark"], m["plaid_light"], m["plaid_step"])
            cols.append((c[0] * k, c[1] * k, c[2] * k, 1.0))
    return cols


def _plaid(p, base, dark, light, step):
    """Cuadrille: franjas oscuras verticales y horizontales que se cruzan, con una linea clara fina."""
    fx = (p.x / step) % 1.0
    fy = (p.y / step) % 1.0
    fz = (p.z / step) % 1.0
    v = 1 if (fx < 0.32 or fy < 0.32) else 0
    h = 1 if fz < 0.32 else 0
    c = tuple(base)
    if v and h:
        c = tuple(d * 0.85 for d in dark)
    elif v or h:
        c = tuple((b + d) * 0.5 for b, d in zip(base, dark))
    if 0.62 < fz < 0.7:
        c = tuple(l for l in light)
    return c


# ---------------------------------------------------------------- exportacion .bytes
def _part_data(o, cols):
    """Triangulos con vertices por esquina deduplicados. Blender (x, y, z) -> Unity (-x, z, -y): el frente de Blender
    (-Y) queda hacia +Z en Unity. Es un espejo, asi que se invierte el orden de los triangulos."""
    me = o.data
    me.calc_loop_triangles()
    try:
        me.calc_normals_split()
    except AttributeError:
        pass
    loops = me.loops
    verts = me.vertices
    corner_n = [l.normal for l in loops] if hasattr(loops[0], "normal") else None
    if hasattr(me, "corner_normals") and len(me.corner_normals):
        corner_n = [cn.vector for cn in me.corner_normals]
    idx = {}
    V, N, C, I = [], [], [], []
    for tri in me.loop_triangles:
        out = []
        for li in tri.loops:
            p = verts[loops[li].vertex_index].co
            n = corner_n[li]
            c = cols[li]
            key = (round(p.x, 5), round(p.y, 5), round(p.z, 5), round(n.x, 3), round(n.y, 3), round(n.z, 3),
                   int(c[0] * 255), int(c[1] * 255), int(c[2] * 255))
            k = idx.get(key)
            if k is None:
                k = len(V)
                idx[key] = k
                V.append((-p.x, p.z, -p.y))
                N.append((-n.x, n.z, -n.y))
                C.append(tuple(max(0, min(255, int(round(x * 255)))) for x in c))
            out.append(k)
        I.extend((out[0], out[2], out[1]))
    return V, N, C, I


def export(parts, path):
    """parts: lista de (nombre, objeto, pivote_blender, colores). Formato little-endian:
    'CBM1', int n; por parte: int len + nombre utf8, float3 pivote (Unity), int nv, nv*float3 pos, nv*float3 normal,
    nv*byte4 color, int ni, ni*int indices."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"CBM1")
        f.write(struct.pack("<i", len(parts)))
        tot = 0
        for name, o, pivot, cols in parts:
            V, N, C, I = _part_data(o, cols)
            px, py, pz = pivot
            pu = (-px, pz, -py)
            nb = name.encode("utf-8")
            f.write(struct.pack("<i", len(nb)))
            f.write(nb)
            f.write(struct.pack("<3f", *pu))
            f.write(struct.pack("<i", len(V)))
            for v in V:
                f.write(struct.pack("<3f", v[0] - pu[0], v[1] - pu[1], v[2] - pu[2]))
            for n in N:
                f.write(struct.pack("<3f", *n))
            for c in C:
                f.write(struct.pack("<4B", c[0], c[1], c[2], 255))
            f.write(struct.pack("<i", len(I)))
            f.write(struct.pack("<%di" % len(I), *I))
            tot += len(I) // 3
    print("export", path, "partes", len(parts), "tris", tot)
    return tot


def finish(objs, name, out_dir, ao=0.55, ao_dist=0.25):
    """Une, hornea y exporta un modelo de una sola pieza (pivote en el origen)."""
    o = join(objs, name)
    cols = bake_colors(o, ao, ao_dist)
    tris = export([(name, o, (0, 0, 0), cols)], os.path.join(out_dir, name + ".bytes"))
    o["vcols"] = 1
    _apply_preview_colors(o, cols)
    return o, tris


def tri_count(o):
    o.data.calc_loop_triangles()
    return len(o.data.loop_triangles)


def decimate_to(o, limit):
    """Baja la malla a 'limit' triangulos (colapso de aristas) si se pasa del presupuesto movil."""
    n = tri_count(o)
    if limit is None or n <= limit:
        return n
    m = o.modifiers.new("dec", "DECIMATE")
    m.ratio = max(0.05, limit / float(n))
    m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)
    return tri_count(o)


def finish_parts(groups, name, out_dir, ao=0.5, ao_dist=0.2, budget=None):
    """Modelo por piezas animables: groups = [(nombre_pieza, [objetos], pivote_blender)].
    budget = triangulos totales maximos (se reparte en proporcion a cada pieza)."""
    parts = []
    tris = 0
    out = []
    joined = [(pname, join(objs, name + "_" + pname), pivot) for pname, objs, pivot in groups]
    if budget:
        total = sum(tri_count(o) for _, o, _ in joined)
        if total > budget:
            k = budget / float(total)
            for _, o, _ in joined:
                decimate_to(o, int(tri_count(o) * k))
    for pname, o, pivot in joined:
        cols = bake_colors(o, ao, ao_dist)
        _apply_preview_colors(o, cols)
        parts.append((pname, o, pivot, cols))
        out.append(o)
    tris = export(parts, os.path.join(out_dir, name + ".bytes"))
    return out, tris


def _apply_preview_colors(o, cols):
    """Material de vista previa que muestra el color horneado (para los renders de control)."""
    me = o.data
    ca = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for i, c in enumerate(cols):
        ca.data[i].color = (*srgb_to_lin(c[:3]), 1.0)
    m = bpy.data.materials.new("Preview_" + o.name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes.get("Principled BSDF")
    a = nt.nodes.new("ShaderNodeVertexColor")
    a.layer_name = "Col"
    nt.links.new(a.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.65
    me.materials.clear()
    me.materials.append(m)


# ---------------------------------------------------------------- render de control
def preview(objs, path, size=640, dist=None, elev=24, azim=-30, target=None):
    """Render de control: camara de 3/4, sol calido de arriba a la izquierda, cielo celeste (BIBLIA 1.2)."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = size
    sc.render.resolution_y = size
    sc.render.film_transparent = False
    w = sc.world or bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (*srgb_to_lin(hexcol("e8f4ff")), 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            p = o.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, p))
            mx = Vector(map(max, mx, p))
    ctr = target or (mn + mx) * 0.5
    rad = (mx - mn).length * 0.5
    d = dist or rad * 3.4
    el, az = math.radians(elev), math.radians(azim)
    cam_loc = ctr + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))) * d
    cd = bpy.data.cameras.new("cam")
    cd.lens = 70
    cam = bpy.data.objects.new("cam", cd)
    _link(cam)
    cam.location = cam_loc
    cam.rotation_euler = (ctr - cam_loc).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    ld = bpy.data.lights.new("sol", "SUN")
    ld.energy = 3.2
    ld.color = hexcol("fff1d6")
    ld.angle = math.radians(8)
    sun = bpy.data.objects.new("sol", ld)
    _link(sun)
    sun.rotation_euler = Euler((math.radians(50), 0, math.radians(-35)))
    # piso suave para la sombra
    bpy.ops.mesh.primitive_plane_add(size=rad * 8, location=(ctr.x, ctr.y, mn.z - 0.001))
    fl = bpy.context.active_object
    setmat(fl, "f3ead8")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for x in (cam, sun, fl):
        bpy.data.objects.remove(x, do_unlink=True)
