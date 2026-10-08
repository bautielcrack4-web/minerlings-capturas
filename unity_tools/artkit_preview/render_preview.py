#!/usr/bin/env python3
"""Rasterizador de prueba del kit de arte (sin Unity): lee los .obj/.mtl que exporta ArtKitPreview y los dibuja con la
misma luz envolvente del shader Mineros/MinerToon (MinerCommon.cginc) y camara isometrica ortografica.

Uso:
  dotnet run --project unity_tools/artkit_preview -- /tmp/artkit_preview/obj
  python3 unity_tools/artkit_preview/render_preview.py /tmp/artkit_preview/obj /tmp/artkit_preview/img [opciones]
Opciones: --biomes 0,1,2,3  --kinds tree,bush  --hero (una imagen grande por kind/bioma)  --scale (hoja de escala)
Las imagenes se dejan FUERA del repo.
"""
import os, sys, math, argparse
import numpy as np
from PIL import Image, ImageDraw, ImageFont

# ------------------------------------------------------------------ paleta (suelo por bioma) y escena
GROUND = {0: "a9c97a", 1: "ecd09a", 2: "6d6385", 3: "7d5d52"}
DARK = {0: 0.0, 1: 0.0, 2: 0.38, 3: 0.22}
KINDS = ["tree", "bush", "flower", "grass", "cactus", "shrub", "crystal_cluster", "mushroom", "deadtree", "vent",
         "pebble", "rock_small", "minecart", "rail", "lantern", "beam", "barrel", "crate", "ore_pile", "ingot",
         "gem", "pickaxe_stand", "sign"]
VARS = {"tree": 4, "bush": 4, "flower": 3, "grass": 4, "cactus": 4, "shrub": 3, "crystal_cluster": 4, "mushroom": 4,
        "deadtree": 3, "vent": 3, "pebble": 3, "rock_small": 4, "minecart": 3, "rail": 3, "lantern": 3, "beam": 3,
        "barrel": 4, "crate": 4, "ore_pile": 4, "ingot": 3, "gem": 4, "pickaxe_stand": 3, "sign": 3}


def hexrgb(h):
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def s2l(c):
    c = np.asarray(c, dtype=np.float32)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def l2s(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def norm(v):
    v = np.asarray(v, dtype=np.float32)
    return v / max(1e-9, float(np.linalg.norm(v)))


YAW, PITCH = 45.0, 32.0
_c = np.array([math.sin(math.radians(YAW)) * math.cos(math.radians(PITCH)), math.sin(math.radians(PITCH)),
               -math.cos(math.radians(YAW)) * math.cos(math.radians(PITCH))], dtype=np.float32)
F = -_c                                  # hacia adelante de la camara
RIGHT = norm(np.cross([0, 1, 0], F))     # mano izquierda de Unity: right = cross(up, forward)
UPC = norm(np.cross(F, RIGHT))
_ch = norm([_c[0], 0, _c[2]])
LDIR = norm(-0.5 * RIGHT + 0.9 * np.array([0, 1, 0]) + 0.25 * _ch)   # hacia la luz: arriba-izquierda de la camara
LIGHT = np.array([1.0, 0.95, 0.86], dtype=np.float32) * 0.78
AMB = np.array([0.46, 0.48, 0.54], dtype=np.float32)
RIM, FLOOR, GLOSS = 0.22, 0.42, 40.0


# ------------------------------------------------------------------ carga de .obj
class Mesh:
    pass


def load_obj(path):
    base = path[:-4]
    mats = {}
    anchor = None
    cur = None
    for ln in open(base + ".mtl"):
        p = ln.split()
        if not p:
            continue
        if p[0] == "#" and len(p) > 2 and p[1] == "anchor":
            anchor = [float(x) for x in p[2].split(",")]
        elif p[0] == "newmtl":
            cur = p[1]
            mats[cur] = {}
        elif p[0] in ("Kd", "Ks", "Ke"):
            mats[cur][p[0]] = np.array([float(p[1]), float(p[2]), float(p[3])], dtype=np.float32)
    V, N, T, M = [], [], [], []
    cm = None
    for ln in open(path):
        p = ln.split()
        if not p:
            continue
        if p[0] == "v":
            V.append([-float(p[1]), float(p[2]), float(p[3])])
        elif p[0] == "vn":
            N.append([-float(p[1]), float(p[2]), float(p[3])])
        elif p[0] == "usemtl":
            cm = p[1]
        elif p[0] == "f":
            idx = [int(q.split("/")[0]) - 1 for q in p[1:4]]
            T.append([idx[0], idx[2], idx[1]])  # deshacer el espejo
            M.append(cm)
    m = Mesh()
    m.V = np.array(V, dtype=np.float32)
    m.N = np.array(N, dtype=np.float32)
    m.T = np.array(T, dtype=np.int32)
    names = sorted(mats.keys(), key=lambda s: int(s[1:]))
    tid = {n: i for i, n in enumerate(names)}
    m.mat_of_tri = np.array([tid[x] for x in M], dtype=np.int32)
    m.kd = np.array([mats[n]["Kd"] for n in names], dtype=np.float32)
    m.ks = np.array([mats[n]["Ks"][0] for n in names], dtype=np.float32)
    m.ke = np.array([mats[n]["Ke"] for n in names], dtype=np.float32)
    m.anchor = anchor
    return m


def make_sphere(r, c, col, seg=20, rings=12, sy=1.0):
    V, N, T = [], [], []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        for j in range(seg):
            th = 2 * math.pi * j / seg
            d = np.array([-math.cos(a) * math.sin(th), math.sin(a), math.cos(a) * math.cos(th)])
            V.append([c[0] + d[0] * r, c[1] + d[1] * r * sy, c[2] + d[2] * r])
            N.append(d.tolist())
    for i in range(rings):
        for j in range(seg):
            j1 = (j + 1) % seg
            a, b, c2, d = i * seg + j, (i + 1) * seg + j, (i + 1) * seg + j1, i * seg + j1
            T.append([a, b, c2])
            T.append([a, c2, d])
    m = Mesh()
    m.V = np.array(V, dtype=np.float32)
    m.N = np.array(N, dtype=np.float32)
    m.T = np.array(T, dtype=np.int32)
    m.mat_of_tri = np.zeros(len(T), dtype=np.int32)
    m.kd = np.array([col], dtype=np.float32)
    m.ks = np.zeros(1, dtype=np.float32)
    m.ke = np.zeros((1, 3), dtype=np.float32)
    m.anchor = None
    return m


def make_miner():
    """Referencia de escala: el minero (1.2 de alto, cabeza ~45%). Solo siluetas simples."""
    parts = [
        make_sphere(0.27, (0, 0.93, 0), hexrgb("f2c49b")),
        make_sphere(0.24, (0, 0.5, 0), hexrgb("3f6fb5"), sy=1.25),
        make_sphere(0.17, (-0.1, 0.12, 0), hexrgb("4a3426"), sy=0.8),
        make_sphere(0.17, (0.1, 0.12, 0), hexrgb("4a3426"), sy=0.8),
        make_sphere(0.29, (0, 1.06, 0), hexrgb("ffcc33"), sy=0.5),
    ]
    return parts


# ------------------------------------------------------------------ render
def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def project(P, scale, cx, cy, W, H):
    sx = P @ RIGHT
    sy = P @ UPC
    dz = P @ F
    return (sx * scale + cx), (cy - sy * scale), dz


def render_scene(items, W, H, scale, ground_hex, dark=0.0, ss=3, label=None, focus=None, shadow=True):
    """items: lista de (Mesh, Xf offset (x,z), rotY grados, escala). Devuelve imagen RGB uint8 WxH."""
    Ws, Hs = W * ss, H * ss
    sc = scale * ss
    # centro de la camara: foco (x,y,z) en el mundo -> centro de la imagen
    f = np.array(focus if focus is not None else [0, 0.5, 0], dtype=np.float32)
    cx0 = (f @ RIGHT) * sc
    cy0 = (f @ UPC) * sc
    cxs, cys = Ws / 2 - cx0, Hs / 2 + cy0
    zbuf = np.full((Hs, Ws), 1e9, dtype=np.float32)
    idbuf = np.full((Hs, Ws), -1, dtype=np.int32)
    bary = np.zeros((Hs, Ws, 3), dtype=np.float32)
    # acumulo todos los vertices en espacio mundo
    allV, allN, allT, allM, kd, ks, ke = [], [], [], [], [], [], []
    voff = 0
    moff = 0
    for (m, off, rot, s) in items:
        a = math.radians(rot)
        R = np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]], dtype=np.float32)
        V = (m.V * s) @ R.T + np.array([off[0], 0, off[1]], dtype=np.float32)
        Nn = m.N @ R.T
        allV.append(V)
        allN.append(Nn)
        allT.append(m.T + voff)
        allM.append(m.mat_of_tri + moff)
        kd.append(m.kd)
        ks.append(m.ks)
        ke.append(m.ke)
        voff += len(V)
        moff += len(m.kd)
    V = np.concatenate(allV)
    N = np.concatenate(allN)
    T = np.concatenate(allT)
    Mt = np.concatenate(allM)
    KD = s2l(np.concatenate(kd))
    KS = np.concatenate(ks)
    KE = s2l(np.concatenate(ke))

    gcol = s2l(hexrgb(ground_hex))
    # sombra proyectada en el suelo (mascara)
    shadow_mask = np.zeros((Hs, Ws), dtype=np.uint8)
    if shadow:
        Ly = max(LDIR[1], 0.2)
        S = V - LDIR[None, :] * (V[:, 1:2] / Ly)
        S[:, 1] = 0
        ssx, ssy, _ = project(S, sc, cxs, cys, Ws, Hs)
        img = Image.new("L", (Ws, Hs), 0)
        dr = ImageDraw.Draw(img)
        for t in T:
            dr.polygon([(float(ssx[t[0]]), float(ssy[t[0]])), (float(ssx[t[1]]), float(ssy[t[1]])), (float(ssx[t[2]]), float(ssy[t[2]]))], fill=255)
        shadow_mask = np.array(img)

    px, py, pz = project(V, sc, cxs, cys, Ws, Hs)
    # normales geometricas y culling (cara frontal = normal hacia la camara)
    A, B, C = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    gn = np.cross(B - A, C - A)
    facing = (gn @ F) < 0
    for ti in np.nonzero(facing)[0]:
        i0, i1, i2 = T[ti]
        x0, y0, x1, y1, x2, y2 = px[i0], py[i0], px[i1], py[i1], px[i2], py[i2]
        minx = max(int(math.floor(min(x0, x1, x2))), 0)
        maxx = min(int(math.ceil(max(x0, x1, x2))), Ws - 1)
        miny = max(int(math.floor(min(y0, y1, y2))), 0)
        maxy = min(int(math.ceil(max(y0, y1, y2))), Hs - 1)
        if minx > maxx or miny > maxy:
            continue
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-9:
            continue
        xs = np.arange(minx, maxx + 1, dtype=np.float32) + 0.5
        ys = np.arange(miny, maxy + 1, dtype=np.float32) + 0.5
        gx, gy = np.meshgrid(xs, ys)
        w0 = ((y1 - y2) * (gx - x2) + (x2 - x1) * (gy - y2)) / den
        w1 = ((y2 - y0) * (gx - x2) + (x0 - x2) * (gy - y2)) / den
        w2 = 1 - w0 - w1
        eps = -1e-4
        inside = (w0 >= eps) & (w1 >= eps) & (w2 >= eps)
        if not inside.any():
            continue
        z = w0 * pz[i0] + w1 * pz[i1] + w2 * pz[i2]
        zb = zbuf[miny:maxy + 1, minx:maxx + 1]
        upd = inside & (z < zb)
        if not upd.any():
            continue
        zb[upd] = z[upd]
        idb = idbuf[miny:maxy + 1, minx:maxx + 1]
        idb[upd] = ti
        bb = bary[miny:maxy + 1, minx:maxx + 1]
        bb[upd, 0] = w0[upd]
        bb[upd, 1] = w1[upd]
        bb[upd, 2] = w2[upd]

    # fondo: suelo plano iluminado por el mismo shader, con sombra
    n_up = np.array([0, 1, 0], dtype=np.float32)
    d = float(n_up @ LDIR)
    t = float(smoothstep(-0.3, 0.55, d))
    lit = (FLOOR + (1 - FLOOR) * t) * LIGHT
    base = gcol * (AMB * 0.6 + lit)
    col = np.tile(base, (Hs, Ws, 1)).astype(np.float32)
    # oscuridad ambiente del bioma (cueva/volcan): oscurece todo el cuadro como el resto del juego
    sm = shadow_mask.astype(np.float32) / 255.0
    if ss > 1:
        sm = np.array(Image.fromarray((sm * 255).astype(np.uint8)).resize((Ws, Hs), Image.BILINEAR)).astype(np.float32) / 255.0
    col = col * (1 - 0.32 * sm[..., None])

    hit = idbuf >= 0
    if hit.any():
        ids = idbuf[hit]
        w = bary[hit]
        tri = T[ids]
        n = (N[tri[:, 0]] * w[:, 0:1] + N[tri[:, 1]] * w[:, 1:2] + N[tri[:, 2]] * w[:, 2:3])
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-9)
        mi = Mt[ids]
        alb = KD[mi]
        dd = n @ LDIR
        tt = smoothstep(-0.3, 0.55, dd)
        lit = (FLOOR + (1 - FLOOR) * tt)[:, None] * LIGHT[None, :]
        amb = np.maximum(AMB, 0.3)
        c = alb * (amb[None, :] * 0.6 + lit)
        v = -F
        r = 1 - np.clip(n @ v, 0, 1)
        c = c + alb * (r ** 3 * RIM)[:, None]
        spec = KS[mi]
        if (spec > 0).any():
            hv = norm(LDIR + v)
            sp = np.clip(n @ hv, 0, 1) ** GLOSS * spec * smoothstep(0.0, 0.3, dd)
            c = c + LIGHT[None, :] * sp[:, None]
        c = c + KE[mi]
        col[hit] = c

    if dark > 0:
        col = col * (1 - dark * 0.55)
    out = (l2s(col) * 255).astype(np.uint8)
    im = Image.fromarray(out)
    if ss > 1:
        im = im.resize((W, H), Image.LANCZOS)
    return im


def font(sz=14):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()


def bounds(m):
    return m.V.min(axis=0), m.V.max(axis=0)


# ------------------------------------------------------------------ hojas
def sheet(objdir, outdir, biome, kinds, name, cell=230, scale=88, ss=2):
    cols = max(VARS[k] for k in kinds)
    rows = len(kinds)
    img = Image.new("RGB", (cols * cell + 120, rows * cell), (30, 30, 34))
    dr = ImageDraw.Draw(img)
    f = font(15)
    for r, k in enumerate(kinds):
        dr.text((8, r * cell + cell // 2 - 8), k, fill=(235, 235, 235), font=f)
        for v in range(VARS[k]):
            m = load_obj(os.path.join(objdir, "%s_%d_%d.obj" % (k, biome, v)))
            lo, hi = bounds(m)
            # escala por pieza: que entre en la celda, con tope para respetar tamanos relativos
            ext = max(hi[1], hi[0] - lo[0], hi[2] - lo[2]) * 1.0
            sc = (cell * 0.72) / max(ext, 0.2) * 0.9
            focus = [0, hi[1] * 0.5, 0]
            im = render_scene([(m, (0, 0), 0, 1.0)], cell, cell, sc, GROUND[biome], DARK[biome], ss, focus=focus)
            img.paste(im, (120 + v * cell, r * cell))
    path = os.path.join(outdir, "%s_b%d.png" % (name, biome))
    img.save(path)
    return path


def hero(objdir, outdir, biome, kind, size=420, ss=3):
    n = VARS[kind]
    img = Image.new("RGB", (n * size, size), (30, 30, 34))
    for v in range(n):
        m = load_obj(os.path.join(objdir, "%s_%d_%d.obj" % (kind, biome, v)))
        lo, hi = bounds(m)
        ext = max(hi[1], hi[0] - lo[0], hi[2] - lo[2])
        sc = size * 0.62 / max(ext, 0.2)
        im = render_scene([(m, (0, 0), 0, 1.0)], size, size, sc, GROUND[biome], DARK[biome], ss, focus=[0, hi[1] * 0.45, 0])
        img.paste(im, (v * size, 0))
    path = os.path.join(outdir, "hero_%s_b%d.png" % (kind, biome))
    img.save(path)
    return path


def scale_sheet(objdir, outdir, biome, scale=78, ss=2):
    """Todo a la misma escala, en fila, junto al minero de referencia (1.2). Dos filas: naturaleza y utileria."""
    miner = make_miner()
    row1 = ["tree", "deadtree", "cactus", "bush", "flower", "grass", "shrub", "crystal_cluster", "mushroom", "vent", "rock_small", "pebble"]
    row2 = ["minecart", "rail", "lantern", "beam", "barrel", "crate", "ore_pile", "ingot", "gem", "pickaxe_stand", "sign"]
    W, H = 2000, 520
    outs = []
    for row in (row1, row2):
        items = []
        x = 0.0
        for mm in miner:
            items.append((mm, x, 1.0))
        x = 0.8
        for k in row:
            m = load_obj(os.path.join(objdir, "%s_%d_0.obj" % (k, biome)))
            lo, hi = bounds(m)
            w = max(hi[0] - lo[0], hi[2] - lo[2], 0.3)
            x += w * 0.5 + 0.12
            items.append((m, x, 1.0))
            x += w * 0.5 + 0.12
        its = [(m, (RIGHT[0] * px, RIGHT[2] * px), 0, s) for (m, px, s) in items]
        cx = x / 2
        focus = [RIGHT[0] * cx, 0.9, RIGHT[2] * cx]
        outs.append(render_scene(its, W, H, scale, GROUND[biome], DARK[biome], ss, focus=focus))
    img = Image.new("RGB", (W, H * 2))
    for i, im in enumerate(outs):
        img.paste(im, (0, i * H))
    path = os.path.join(outdir, "scale_b%d.png" % biome)
    img.save(path)
    return path


def overview(objdir, outdir, biome, cell=250, cols=6, ss=2, variant=0):
    """Hoja de contacto compacta: los 23 kinds (una variante) en una sola imagen, con la misma escala relativa por celda."""
    rows = (len(KINDS) + cols - 1) // cols
    img = Image.new("RGB", (cols * cell, rows * cell), (30, 30, 34))
    dr = ImageDraw.Draw(img)
    f = font(14)
    for i, k in enumerate(KINDS):
        m = load_obj(os.path.join(objdir, "%s_%d_%d.obj" % (k, biome, variant % VARS[k])))
        lo, hi = bounds(m)
        ext = max(hi[1], hi[0] - lo[0], hi[2] - lo[2])
        sc = cell * 0.62 / max(ext, 0.2)
        im = render_scene([(m, (0, 0), 0, 1.0)], cell, cell, sc, GROUND[biome], DARK[biome], ss, focus=[0, hi[1] * 0.45, 0])
        x, y = (i % cols) * cell, (i // cols) * cell
        img.paste(im, (x, y))
        dr.text((x + 8, y + 6), k, fill=(255, 255, 255), font=f)
    path = os.path.join(outdir, "overview_b%d.png" % biome)
    img.save(path)
    return path


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("objdir")
    ap.add_argument("outdir")
    ap.add_argument("--biomes", default="0,1,2,3")
    ap.add_argument("--kinds", default="")
    ap.add_argument("--hero", action="store_true")
    ap.add_argument("--scale", action="store_true")
    ap.add_argument("--overview", action="store_true")
    a = ap.parse_args()
    os.makedirs(a.outdir, exist_ok=True)
    bms = [int(x) for x in a.biomes.split(",")]
    kinds = [k for k in a.kinds.split(",") if k] or KINDS
    for b in bms:
        if a.overview:
            print(overview(a.objdir, a.outdir, b))
        elif a.scale:
            print(scale_sheet(a.objdir, a.outdir, b))
        elif a.hero:
            for k in kinds:
                print(hero(a.objdir, a.outdir, b, k))
        else:
            pages = [kinds[i:i + 6] for i in range(0, len(kinds), 6)]
            for pi, pk in enumerate(pages):
                print(sheet(a.objdir, a.outdir, b, pk, "sheet%d" % (pi + 1)))
