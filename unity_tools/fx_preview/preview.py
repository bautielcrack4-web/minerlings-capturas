#!/usr/bin/env python3
"""
Vista previa de los efectos de Mineros Idle SIN Unity.

Lee lo que exporta FxExport (effects.json + texturas .rgba, generadas por el mismo codigo C# que usa el juego) y
reproduce los parametros principales de cada emisor: rafagas y tasa, forma de emision, velocidad, gravedad, arrastre,
colision con el suelo (y=0) con rebote, sub-emisores (choque/muerte), tamano y color sobre la vida, giro, estiramiento,
mezcla aditiva/alfa en espacio lineal (el proyecto usa Linear). Renderiza con una camara ortografica isometrica
(pitch 30, yaw 45) y escribe hojas de contacto PNG.

Uso:
  dotnet run --project FxExport -- /tmp/fx_preview/data
  python3 preview.py --data /tmp/fx_preview/data --out /tmp/fx_preview [--only rock_break,crit] [--tint rock_break=d0905a]
"""
import argparse
import json
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFont

PITCH = math.radians(30.0)
YAW = math.radians(45.0)
FWD = np.array([math.cos(PITCH) * math.sin(YAW), -math.sin(PITCH), math.cos(PITCH) * math.cos(YAW)])
RIGHT = np.array([FWD[2], 0.0, -FWD[0]])
RIGHT /= np.linalg.norm(RIGHT)
CAMUP = np.cross(FWD, RIGHT)
GROUND_Y = 0.0
DT = 1.0 / 120.0
CELL = 300


def srgb_to_lin(c):
    c = np.asarray(c, dtype=np.float32)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * (c ** (1 / 2.4)) - 0.055)


def hex_rgba(v):
    return np.array([(v >> 24) & 255, (v >> 16) & 255, (v >> 8) & 255, v & 255], dtype=np.float32) / 255.0


def curve(keys, t, default=1.0):
    if not keys:
        return default
    n = len(keys) // 2
    if t <= keys[0]:
        return keys[1]
    for i in range(n - 1):
        t0, v0, t1, v1 = keys[2 * i], keys[2 * i + 1], keys[2 * i + 2], keys[2 * i + 3]
        if t <= t1:
            k = (t - t0) / max(1e-6, t1 - t0)
            return v0 + (v1 - v0) * k
    return keys[-1]


def gradient(keys, t):
    if not keys:
        return np.array([1, 1, 1, 1], dtype=np.float32)
    n = len(keys) // 5
    rows = [keys[5 * i:5 * i + 5] for i in range(n)]
    if t <= rows[0][0]:
        return np.array(rows[0][1:], dtype=np.float32)
    for i in range(n - 1):
        a, b = rows[i], rows[i + 1]
        if t <= b[0]:
            k = (t - a[0]) / max(1e-6, b[0] - a[0])
            return np.array(a[1:], dtype=np.float32) * (1 - k) + np.array(b[1:], dtype=np.float32) * k
    return np.array(rows[-1][1:], dtype=np.float32)


def shade(c, s):
    c = np.array(c[:3], dtype=np.float32)
    if s > 0:
        return c + (1 - c) * s
    return c * (1 + s)


class Textures:
    def __init__(self, data_dir):
        self.base = {}
        self.cache = {}
        for fn in os.listdir(data_dir):
            if fn.startswith("tex_") and fn.endswith(".rgba"):
                name, size = fn[4:-5].rsplit("_", 1)
                size = int(size)
                a = np.frombuffer(open(os.path.join(data_dir, fn), "rb").read(), dtype=np.uint8).reshape(size, size, 4)
                a = a[::-1].astype(np.float32) / 255.0   # fila 0 = arriba
                self.base[name] = a

    def get(self, name, px):
        """Version pre-reducida (mip) de la textura para ~px pixeles."""
        a = self.base[name]
        size = a.shape[0]
        lvl = size
        while lvl // 2 >= max(4, px):
            lvl //= 2
        key = (name, lvl)
        if key not in self.cache:
            if lvl == size:
                self.cache[key] = a
            else:
                # promedio por bloques, ponderado por alpha
                f = size // lvl
                w = a[..., 3:4]
                rgb = (a[..., :3] * w).reshape(lvl, f, lvl, f, 3).sum((1, 3))
                ws = w.reshape(lvl, f, lvl, f, 1).sum((1, 3))
                out = np.zeros((lvl, lvl, 4), dtype=np.float32)
                out[..., :3] = rgb / np.maximum(ws, 1e-6)
                out[..., 3:4] = ws / (f * f)
                self.cache[key] = out
        return self.cache[key]


class P:
    __slots__ = ("pos", "vel", "age", "life", "size", "col", "rot", "spin", "em", "fl", "alive", "cflip")


class Sim:
    def __init__(self, effect, tint, scale, rng, loop_mode, eco=False, intensity=1.0):
        self.fx = effect
        self.ems = effect["Emitters"]
        self.tint = tint
        self.scale = scale
        self.rng = rng
        self.parts = []
        self.t = 0.0
        self.loop = loop_mode
        self.eco = eco
        self.acc = [0.0] * len(self.ems)
        self.bursts_done = [[False] * (len(e["Bursts"] or []) // 2) for e in self.ems]
        self.intensity = intensity
        self.end_emit = effect["PlayDuration"] if effect["Loop"] and not loop_mode else None

    # ---- emision ----
    def pick_color(self, e):
        rng = self.rng
        pal = e["Palette"]
        if pal:
            a = hex_rgba(pal[rng.randrange(len(pal))])
            b = a
        else:
            a, b = hex_rgba(e["ColA"]), hex_rgba(e["ColB"])
        u = rng.random() if not pal else 0.0
        base = a * (1 - u) + b * u
        if self.tint is not None and e["TintK"] > 0 and not pal:   # la paleta al azar ignora el tinte (igual que en Unity)
            ta = np.concatenate([shade(self.tint, e["ShadeA"]), [a[3]]])
            tb = np.concatenate([shade(self.tint, e["ShadeB"]), [b[3]]])
            tc = ta * (1 - u) + tb * u
            k = e["TintK"]
            base = base * (1 - k) + tc * k
        return base

    def spawn(self, ei, origin, parent=None, count=1):
        e = self.ems[ei]
        rng = self.rng
        s = self.scale
        for _ in range(count):
            p = P()
            R = e["Radius"] * s
            shape = e["Shape"]
            d = np.array([0.0, 1.0, 0.0])
            pos = np.zeros(3)
            if shape == "Point":
                v = np.array([rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)])
                d = v / (np.linalg.norm(v) + 1e-9)
            elif shape in ("Sphere", "Hemisphere"):
                v = np.array([rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)])
                v /= (np.linalg.norm(v) + 1e-9)
                if shape == "Hemisphere":
                    v[1] = abs(v[1])
                pos = v * R * (rng.random() ** (1 / 3))
                d = v
            elif shape == "Cone":
                r = math.sqrt(rng.random())
                phi = rng.random() * math.tau
                pos = np.array([math.cos(phi) * R * r, 0.0, math.sin(phi) * R * r])
                al = math.radians(e["Angle"]) * (r if R > 1e-6 else rng.random())
                if R <= 1e-6:
                    phi = rng.random() * math.tau
                d = np.array([math.cos(phi) * math.sin(al), math.cos(al), math.sin(phi) * math.sin(al)])
            elif shape == "Disc":
                r = math.sqrt(rng.random())
                phi = rng.random() * math.tau
                pos = np.array([math.cos(phi) * R * r, 0.0, math.sin(phi) * R * r])
                d = np.array([math.cos(phi), 0.0, math.sin(phi)])
            speed = (e["SpeedMin"] + (e["SpeedMax"] - e["SpeedMin"]) * rng.random()) * s
            org = np.array(origin, dtype=np.float64)
            if parent is None:
                if e["Ground"]:
                    org[1] = GROUND_Y + e["Y"] * s
                else:
                    org[1] += e["Y"] * s
            p.pos = org + pos
            p.vel = d * speed
            p.age = 0.0
            p.life = e["LifeMin"] + (e["LifeMax"] - e["LifeMin"]) * rng.random()
            p.size = (e["SizeMin"] + (e["SizeMax"] - e["SizeMin"]) * rng.random()) * s
            p.col = self.pick_color(e)
            p.rot = math.radians(e["RotMin"] + (e["RotMax"] - e["RotMin"]) * rng.random())
            p.spin = math.radians(e["SpinMin"] + (e["SpinMax"] - e["SpinMin"]) * rng.random())
            p.em = ei
            p.fl = rng.random()
            p.cflip = rng.random()
            self.parts.append(p)

    def count(self, n, e):
        n = n * self.intensity
        if self.eco:
            n *= 0.5
        k = int(n)
        if self.rng.random() < n - k:
            k += 1
        return k

    def emit(self, dt, origin):
        for ei, e in enumerate(self.ems):
            if e["Sub"]:
                continue
            if self.eco and e["Eco0"]:
                continue
            t0 = e["Delay"]
            b = e["Bursts"] or []
            for bi in range(len(b) // 2):
                if not self.bursts_done[ei][bi] and self.t >= t0 + b[2 * bi]:
                    self.bursts_done[ei][bi] = True
                    self.spawn(ei, origin, None, self.count(b[2 * bi + 1], e))
            if e["Rate"] > 0:
                dur = e["Duration"]
                active = self.t >= t0 and (self.loop or (dur > 0 and self.t < t0 + dur)
                                           or (self.end_emit is not None and self.t < self.end_emit and dur == 0))
                if self.fx["Loop"] and not self.loop and self.end_emit is not None:
                    active = self.t >= t0 and self.t < self.end_emit
                if active:
                    rate = e["Rate"] * self.intensity * (0.5 if self.eco else 1.0)
                    self.acc[ei] += rate * dt
                    while self.acc[ei] >= 1.0:
                        self.acc[ei] -= 1.0
                        self.spawn(ei, origin)

    def trigger_sub(self, p, ids):
        for si in ids:
            se = self.ems[si]
            b = se["Bursts"] or [0, 1]
            n = self.count(b[1], se)
            if self.eco and se["Eco0"]:
                continue
            self.spawn(si, p.pos, parent=p, count=n)

    # ---- paso ----
    def step(self, dt, origin):
        self.emit(dt, origin)
        rng = self.rng
        s = self.scale
        new = []
        births = []
        for p in self.parts:
            e = self.ems[p.em]
            p.age += dt
            if p.age >= p.life:
                if e["SubDeath"]:
                    births.append((p, e["SubDeath"]))
                continue
            p.vel[1] -= e["Gravity"] * s * dt
            if e["Noise"] > 0:
                p.vel += np.array([rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)]) * e["Noise"] * s * dt * 6
            if e["Drag"] > 0:
                p.vel *= max(0.0, 1.0 - e["Drag"] * dt)
            p.pos = p.pos + p.vel * dt
            p.rot += p.spin * dt
            if e["Bounce"] > 0:
                t = p.age / p.life
                rad = p.size * curve(e["SizeCurve"], t) * 0.5 * 0.5
                floor = GROUND_Y + rad
                if p.pos[1] < floor and p.vel[1] < 0:
                    p.pos[1] = floor
                    p.vel[1] = -p.vel[1] * e["Bounce"]
                    p.vel[0] *= (1 - e["Dampen"])
                    p.vel[2] *= (1 - e["Dampen"])
                    if abs(p.vel[1]) < 0.35:
                        p.vel[1] = 0.0
                    if e["SubCollision"] and rng.random() < e["SubProb"]:
                        births.append((p, e["SubCollision"]))
            new.append(p)
        self.parts = new
        for p, ids in births:
            self.trigger_sub(p, ids)
        self.t += dt


# ------------------------------------------------------------------ render
def project(pos, ppu, cx, cy):
    sx = float(np.dot(pos, RIGHT))
    sy = float(np.dot(pos, CAMUP))
    return cx + sx * ppu, cy - sy * ppu


def splat(canvas, tex, cx, cy, U, V, rgba, additive):
    """Dibuja la textura (h,w,4) sobre canvas lineal (H,W,3) con base U,V (pixeles, y hacia arriba)."""
    H, W, _ = canvas.shape
    corners = [(-0.5, -0.5), (0.5, -0.5), (-0.5, 0.5), (0.5, 0.5)]
    xs = [cx + a * U[0] + b * V[0] for a, b in corners]
    ys = [cy - (a * U[1] + b * V[1]) for a, b in corners]
    x0, x1 = int(max(0, math.floor(min(xs)))), int(min(W, math.ceil(max(xs)) + 1))
    y0, y1 = int(max(0, math.floor(min(ys)))), int(min(H, math.ceil(max(ys)) + 1))
    if x1 <= x0 or y1 <= y0:
        return
    det = U[0] * V[1] - U[1] * V[0]
    if abs(det) < 1e-6:
        return
    gx, gy = np.meshgrid(np.arange(x0, x1) + 0.5 - cx, -(np.arange(y0, y1) + 0.5 - cy))
    a = (gx * V[1] - gy * V[0]) / det
    b = (-gx * U[1] + gy * U[0]) / det
    th, tw = tex.shape[0], tex.shape[1]
    fu = (a + 0.5) * tw - 0.5
    fv = (0.5 - b) * th - 0.5
    inside = (a >= -0.5) & (a <= 0.5) & (b >= -0.5) & (b <= 0.5)
    iu0 = np.clip(np.floor(fu).astype(int), 0, tw - 1)
    iv0 = np.clip(np.floor(fv).astype(int), 0, th - 1)
    iu1 = np.clip(iu0 + 1, 0, tw - 1)
    iv1 = np.clip(iv0 + 1, 0, th - 1)
    wu = np.clip(fu - np.floor(fu), 0, 1)[..., None]
    wv = np.clip(fv - np.floor(fv), 0, 1)[..., None]
    t00, t10, t01, t11 = tex[iv0, iu0], tex[iv0, iu1], tex[iv1, iu0], tex[iv1, iu1]
    # interpolacion ponderada por alpha para el color
    def lerp2(t00, t10, t01, t11):
        a0 = t00[..., 3:4] * (1 - wu) + t10[..., 3:4] * wu
        a1 = t01[..., 3:4] * (1 - wu) + t11[..., 3:4] * wu
        al = a0 * (1 - wv) + a1 * wv
        c0 = (t00[..., :3] * t00[..., 3:4]) * (1 - wu) + (t10[..., :3] * t10[..., 3:4]) * wu
        c1 = (t01[..., :3] * t01[..., 3:4]) * (1 - wu) + (t11[..., :3] * t11[..., 3:4]) * wu
        cc = (c0 * (1 - wv) + c1 * wv) / np.maximum(al, 1e-6)
        return cc, al
    tc, ta = lerp2(t00, t10, t01, t11)
    tc = srgb_to_lin(tc)
    col = srgb_to_lin(rgba[:3])
    alpha = ta * rgba[3] * inside[..., None]
    src = tc * col
    region = canvas[y0:y1, x0:x1]
    if additive:
        region += src * alpha
    else:
        region *= (1 - alpha)
        region += src * alpha


BIOMES = {  # suelo, manchas, oscuridad ambiente (portado de Art.BIOMES)
    "grass": ((0xA9, 0xC9, 0x7A), (0x9D, 0xBF, 0x6C), 0.0),
    "desert": ((0xEC, 0xD0, 0x9A), (0xE2, 0xC4, 0x87), 0.0),
    "cave": ((0x6D, 0x63, 0x85), (0x64, 0x5A, 0x7C), 0.38),
    "volcano": ((0x7D, 0x5D, 0x52), (0x73, 0x54, 0x48), 0.22),
}
BG_OF = {"crystal_glow": "cave", "lava_bubble": "volcano", "meteor_impact": "desert", "meteor_trail": "desert",
         "boss_phase": "cave", "boss_break": "cave", "explosion": "desert", "frenzy_trail": "desert"}


def ground_bg(w, h, cx, cy, ppu, biome="grass", focus=(0.0, 0.0, 0.0)):
    """Suelo del bioma: losas isometricas suaves (referencia de escala)."""
    img = np.zeros((h, w, 3), dtype=np.float32)
    g, bl, dk = BIOMES[biome]
    base = srgb_to_lin(np.array(g) / 255.0)
    dark = srgb_to_lin(np.array(bl) / 255.0)
    img[:] = base
    ys, xs = np.mgrid[0:h, 0:w]
    sx = (xs + 0.5 - cx) / ppu
    sy = -(ys + 0.5 - cy) / ppu
    # inversa de la proyeccion al plano y=0: p = a*RIGHT + b*(CAMUP) con y=0
    # resolver x,z a partir de sx,sy
    m = np.array([[RIGHT[0], RIGHT[2]], [CAMUP[0], CAMUP[2]]])
    inv = np.linalg.inv(m)
    gx = inv[0, 0] * sx + inv[0, 1] * sy + focus[0]
    gz = inv[1, 0] * sx + inv[1, 1] * sy + focus[2]
    chk = ((np.floor(gx / 0.9) + np.floor(gz / 0.9)) % 2 == 0)
    img[chk] = dark
    img *= (1.0 - dk)
    return img


def draw_prop(canvas, kind, cx, cy, ppu):
    """Siluetas de referencia (roca ~0.9, minero 1.2) para juzgar escala. Estilo juguete plano."""
    if kind is None:
        return
    H, W, _ = canvas.shape
    ys, xs = np.mgrid[0:H, 0:W]

    def blob(wx, wy, wz, rx, ry, col_hi, col_lo):
        x, y = project(np.array([wx, wy, wz]), ppu, cx, cy)
        dx = (xs + 0.5 - x) / (rx * ppu)
        dy = (ys + 0.5 - y) / (ry * ppu)
        d = dx * dx + dy * dy
        m = d < 1.0
        lit = np.clip(0.5 - dy * 0.5 - dx * 0.25, 0, 1)[..., None]
        col = srgb_to_lin(np.array(col_lo)) * (1 - lit) + srgb_to_lin(np.array(col_hi)) * lit
        canvas[m] = col[m]

    def shadow(wx, wz, r):
        x, y = project(np.array([wx, 0.0, wz]), ppu, cx, cy)
        dx = (xs + 0.5 - x) / (r * ppu)
        dy = (ys + 0.5 - y) / (r * 0.5 * ppu)
        d = np.sqrt(dx * dx + dy * dy)
        k = (np.clip(1 - d, 0, 1) * 0.35)[..., None]
        canvas[:] = canvas * (1 - k)

    hx = lambda s: [int(s[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    if kind == "rock":
        shadow(0, 0, 0.65)
        blob(0, 0.38, 0, 0.5, 0.42, hx("c9cdd2"), hx("6f757c"))
    elif kind == "boss":
        shadow(0, 0, 1.3)
        blob(0, 0.9, 0, 1.0, 0.95, hx("e8a8e8"), hx("4b6fb8"))
    elif kind == "chest":
        shadow(0, 0, 0.7)
        blob(0, 0.3, 0, 0.5, 0.34, hx("c68a4a"), hx("5e3b22"))
    elif kind == "miner":
        shadow(0, 0, 0.45)
        blob(0, 0.35, 0, 0.3, 0.38, hx("ff8a28"), hx("b85a10"))     # cuerpo
        blob(0, 0.85, 0, 0.3, 0.3, hx("f2c9a0"), hx("c99a70"))       # cabeza
        blob(0, 1.05, 0, 0.33, 0.2, hx("ffcc33"), hx("d9961c"))      # casco
    elif kind == "crystal":
        shadow(0, 0, 0.5)
        blob(0, 0.45, 0, 0.26, 0.5, hx("c8ffff"), hx("2aa8c8"))


PROPS = {
    "hit_spark": "rock", "crit": "rock", "dust": "rock", "debris": "rock", "rock_break": None, "coin_burst": None,
    "gem_sparkle": None, "glint": "rock", "ring": None, "explosion": None, "confetti": "miner", "levelup_aura": "miner",
    "milestone": "miner", "meteor_trail": None, "meteor_impact": None, "chest_open": None, "boss_phase": "boss",
    "boss_break": None, "gold_rush": "miner", "frenzy_trail": "miner", "smoke": "rock", "lava_bubble": None,
    "crystal_glow": "crystal", "unlock_burst": None,
}
# antes de romperse (para ver el contexto en el primer cuadro) y que desaparezca al estallar
PROP_FIRST = {"rock_break": "rock", "coin_burst": "rock", "gem_sparkle": "rock", "chest_open": "chest", "boss_break": "boss",
              "explosion": "rock", "meteor_impact": "rock"}


def effect_duration(fx):
    d = 0.0
    for e in fx["Emitters"]:
        if e["Sub"]:
            continue
        last = 0.0
        b = e["Bursts"] or []
        for i in range(len(b) // 2):
            last = max(last, b[2 * i])
        if e["Rate"] > 0:
            last = max(last, e["Duration"] if e["Duration"] > 0 else fx["PlayDuration"])
        d = max(d, e["Delay"] + last + e["LifeMax"])
    return d


def render_frame(sim, tex, ppu, t_idx_prop, prop_kind, show_prop, origin, bg_cache, biome="grass", focus=(0.0, 0.0, 0.0)):
    W = H = CELL
    cx, cy = W / 2, H * 0.66
    key = (ppu, biome, tuple(round(f, 3) for f in focus))
    if key not in bg_cache:
        bg_cache[key] = ground_bg(W, H, cx, cy, ppu, biome, focus)
    canvas = bg_cache[key].copy()
    fo = np.array(focus, dtype=np.float64)
    if show_prop and prop_kind:
        pcx, pcy = project(np.array(origin, dtype=np.float64) - fo, ppu, cx, cy)
        draw_prop(canvas, prop_kind, pcx, pcy, ppu)
    ems = sim.ems
    order = sorted(range(len(ems)), key=lambda i: (1 if ems[i]["Overlay"] else 0, i))
    by_em = {i: [] for i in range(len(ems))}
    for p in sim.parts:
        by_em[p.em].append(p)
    for ei in order:
        e = ems[ei]
        lst = sorted(by_em[ei], key=lambda p: -p.age)
        name = e["Tex"]
        for p in lst:
            t = p.age / p.life
            sz = p.size * curve(e["SizeCurve"], t)
            if sz <= 1e-4:
                continue
            g = gradient(e["ColorCurve"], t)
            rgba = np.array([p.col[0] * g[0], p.col[1] * g[1], p.col[2] * g[2], p.col[3] * g[3]], dtype=np.float32)
            if rgba[3] <= 0.003:
                continue
            sx, sy = project(p.pos - fo, ppu, cx, cy)
            px_size = sz * ppu
            tx = sim_tex(tex, name, px_size)
            fx_ = 1.0
            if e["Flip"] > 0:
                f1 = abs(math.cos(math.tau * e["Flip"] * t))
                f2 = abs(math.cos(math.tau * e["Flip"] * 1.4 * t))
                fx_ = max(0.14, f1 * (1 - p.cflip) + f2 * p.cflip)
            if e["Render"] == "Stretch":
                sp = float(np.linalg.norm(p.vel))
                length = sz * e["StretchLen"] + sp * e["StretchSpeed"] * sim.scale
                vs = np.array([np.dot(p.vel, RIGHT), np.dot(p.vel, CAMUP)])
                n = np.linalg.norm(vs)
                dirv = vs / n if n > 1e-6 else np.array([0.0, 1.0])
                # el eje largo de la textura (v) va a lo largo de la velocidad
                V = dirv * length * ppu
                U = np.array([dirv[1], -dirv[0]]) * sz * ppu
                # el centro se adelanta media longitud hacia atras (cola detras de la cabeza)
                splat(canvas, tx[1], sx, sy, U, V, rgba, e["Additive"])
            elif e["Render"] == "Flat":
                c, s = math.cos(p.rot), math.sin(p.rot)
                ux = np.array([c, 0, s]) * sz
                vz = np.array([-s, 0, c]) * sz
                U = np.array([np.dot(ux, RIGHT), np.dot(ux, CAMUP)]) * ppu
                V = np.array([np.dot(vz, RIGHT), np.dot(vz, CAMUP)]) * ppu
                splat(canvas, tx[1], sx, sy, U, V, rgba, e["Additive"])
            else:
                c, s = math.cos(p.rot), math.sin(p.rot)
                U = np.array([c, s]) * px_size * fx_
                V = np.array([-s, c]) * px_size
                splat(canvas, tx[1], sx, sy, U, V, rgba, e["Additive"])
    return canvas


def sim_tex(tex, name, px):
    return (name, tex.get(name, int(px)))


def to_image(canvas):
    return Image.fromarray((lin_to_srgb(canvas) * 255 + 0.5).astype(np.uint8))


def run_effect(fx, tex, tint, scale, seed, nframes=6, eco=False, intensity=1.0, bg_cache=None):
    rng = random.Random(seed)
    ppu = 70.0 * fx["Zoom"]
    loop_mode = False
    dur = effect_duration(fx)
    if fx["Loop"]:
        times = [0.1, 0.3, 0.5, 0.7, 0.9, 1.15][:nframes]
        # para el continuo se simula como Attach (emite todo el tiempo)
        loop_mode = True
    else:
        fr = [0.05, 0.12, 0.22, 0.36, 0.56, 0.82]
        times = [max(0.02, dur * f) for f in fr][:nframes]
    sim = Sim(fx, tint, scale, rng, loop_mode, eco, intensity)
    frames = []
    k = 0
    t = 0.0
    kind = fx["Kind"]
    prop = PROPS.get(kind)
    first = PROP_FIRST.get(kind)
    biome = BG_OF.get(kind, "grass")
    focus = (0.0, 0.0, 0.0)
    path = None
    if kind == "meteor_trail":      # cae en diagonal a ~8 u/s y toca el suelo en 0.5 s
        times = [0.06, 0.14, 0.22, 0.30, 0.38, 0.46]
        path = lambda tt: np.array([1.6 - 3.2 * tt, max(0.0, 3.4 - 6.8 * tt), 1.6 - 3.2 * tt])
        focus = (0.4, 1.2, 0.4)
    elif kind == "frenzy_trail":    # el minero corre a 3.5 u/s hacia la derecha de la pantalla
        times = [0.15, 0.35, 0.55, 0.75, 0.95, 1.15]
        path = lambda tt: RIGHT * (3.5 * tt - 2.0)
        focus = (0.0, 0.0, 0.0)
    while k < len(times):
        org = path(t) if path else np.zeros(3)
        sim.step(DT, org)
        t += DT
        if t >= times[k]:
            pk = prop or first
            show = (prop is not None) or (first is not None and k == 0)
            if kind == "meteor_trail":
                pk, show = None, False
            cv = render_frame(sim, tex, ppu, k, pk, show, org, bg_cache, biome, focus)
            frames.append((times[k], to_image(cv), len(sim.parts)))
            k += 1
    return frames


def load_font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/dejavu/DejaVuSans.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def contact_sheet(rows, path, nframes=6):
    font = load_font(15)
    small = load_font(12)
    lab = 150
    w = lab + CELL * nframes
    h = CELL * len(rows)
    sheet = Image.new("RGB", (w, h), (30, 30, 36))
    d = ImageDraw.Draw(sheet)
    for r, (label, frames, info) in enumerate(rows):
        y = r * CELL
        d.text((8, y + 8), label, fill=(255, 255, 255), font=font)
        d.text((8, y + 30), info, fill=(190, 190, 200), font=small)
        for c, (tm, im, n) in enumerate(frames):
            sheet.paste(im, (lab + c * CELL, y))
            d.text((lab + c * CELL + 5, y + 4), "%.2fs  n=%d" % (tm, n), fill=(20, 20, 20), font=small)
            d.rectangle([lab + c * CELL, y, lab + (c + 1) * CELL - 1, y + CELL - 1], outline=(30, 30, 36))
    sheet.save(path)


def texture_sheet(tex, path):
    names = sorted(tex.base.keys())
    cols = 7
    cell = 150
    rows = (len(names) + cols - 1) // cols
    img = Image.new("RGB", (cols * cell, rows * cell), (60, 62, 70))
    font = load_font(12)
    d = ImageDraw.Draw(img)
    for i, nm in enumerate(names):
        a = tex.base[nm]
        pil = Image.fromarray((a * 255).astype(np.uint8), "RGBA").resize((cell - 20, cell - 28), Image.BILINEAR)
        bg = Image.new("RGBA", pil.size, (110, 112, 125, 255))
        # tablero para ver alpha
        bgd = ImageDraw.Draw(bg)
        for yy in range(0, pil.size[1], 12):
            for xx in range(0, pil.size[0], 12):
                if (xx // 12 + yy // 12) % 2:
                    bgd.rectangle([xx, yy, xx + 11, yy + 11], fill=(90, 92, 105, 255))
        bg.alpha_composite(pil)
        x, y = (i % cols) * cell + 10, (i // cols) * cell + 6
        img.paste(bg.convert("RGB"), (x, y))
        d.text((x, y + cell - 24), nm, fill=(255, 255, 255), font=font)
    img.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default="/tmp/fx_preview/data")
    ap.add_argument("--out", default="/tmp/fx_preview")
    ap.add_argument("--only", default="")
    ap.add_argument("--tint", default="", help="kind=rrggbb[,kind=rrggbb]")
    ap.add_argument("--scale", default="", help="kind=escala[,...]")
    ap.add_argument("--eco", action="store_true")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--per-sheet", type=int, default=6)
    ap.add_argument("--prefix", default="sheet")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    fxs = json.load(open(os.path.join(a.data, "effects.json")))
    tex = Textures(a.data)
    texture_sheet(tex, os.path.join(a.out, "textures.png"))
    tints = {}
    for kv in filter(None, a.tint.split(",")):
        k, v = kv.split("=")
        tints[k] = np.array([int(v[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)
    scales = {}
    for kv in filter(None, a.scale.split(",")):
        k, v = kv.split("=")
        scales[k] = float(v)
    only = set(filter(None, a.only.split(",")))
    sel = [f for f in fxs if not only or f["Kind"] in only]
    bg_cache = {}
    rows = []
    sheet_i = 0
    for i, fx in enumerate(sel):
        frames = run_effect(fx, tex, tints.get(fx["Kind"]), scales.get(fx["Kind"], 1.0), a.seed, eco=a.eco, bg_cache=bg_cache)
        info = "prio %d%s\nzoom x%.1f  %d emisores" % (fx["Priority"], " loop" if fx["Loop"] else "", fx["Zoom"], len(fx["Emitters"]))
        rows.append((fx["Kind"], frames, info))
        print("ok", fx["Kind"], flush=True)
        if len(rows) == a.per_sheet or i == len(sel) - 1:
            sheet_i += 1
            contact_sheet(rows, os.path.join(a.out, "%s_%d.png" % (a.prefix, sheet_i)))
            rows = []


if __name__ == "__main__":
    main()
