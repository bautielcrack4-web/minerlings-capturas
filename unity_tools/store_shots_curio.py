"""Capturas de tienda de Curio Barn: 1080x1920 (9:16, Play acepta hasta 2:1) con una frase corta arriba.
Uso: python3 unity_tools/store_shots_curio.py <carpeta_capturas_2x> <carpeta_salida>
Toma las capturas del ShotRunner (1440x3088) y las pone en un marco redondeado sobre un fondo calido.
"""
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SRC, OUT = sys.argv[1], sys.argv[2]
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../unity_rarezas/Assets/Resources/Fonts/LilitaOne-Regular.ttf")
SHOTS = [
    ("08_cargando", "Encontrá rarezas por todo el mundo", "Find curiosities all over the world"),
    ("17_tarjeta", "Los clientes te hacen ofertas", "Customers make you offers"),
    ("23_perfecto", "Mirá su cara y cerrá el trato PERFECTO", "Read their face, land the PERFECT deal"),
    ("93_manejando", "Salí con Tostada a buscar cosas grandes", "Haul big finds with Tostada"),
    ("a8_subasta", "Subastas relámpago", "Flash auctions!"),
    ("c1_patio", "Castillos, playas, selvas y un pico helado", "Castles, beaches, jungles and an icy peak"),
    ("ca_sala_exhibicion", "Del rancho al museo de lujo", "From shack to grand museum"),
    ("ce_grua", "Traé gigantes con la grúa", "Bring giants home with the crane"),
]
W, H = 1080, 1920


def frame(shot, text, top=(255, 186, 92), bot=(224, 89, 74)):
    bg = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / H
        d.line([(0, y), (W, y)], fill=tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)))
    im = Image.open(shot).convert("RGB")
    sh = 1500
    sw = int(im.width * sh / im.height)
    im = im.resize((sw, sh), Image.LANCZOS)
    x, y = (W - sw) // 2, H - sh - 70
    mask = Image.new("L", (sw, sh), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, sw - 1, sh - 1], 48, fill=255)
    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle([x - 6, y + 14, x + sw + 6, y + sh + 24], 54, fill=150)
    bg.paste((90, 30, 20), (0, 0), shadow.filter(ImageFilter.GaussianBlur(22)))
    border = Image.new("L", (W, H), 0)
    ImageDraw.Draw(border).rounded_rectangle([x - 12, y - 12, x + sw + 12, y + sh + 12], 58, fill=255)
    bg.paste((255, 246, 228), (0, 0), border)
    bg.paste(im, (x, y), mask)
    f = ImageFont.truetype(FONT, 92)
    while d.textlength(text, font=f) > W - 90:
        f = ImageFont.truetype(FONT, f.size - 4)
    tw = d.textlength(text, font=f)
    tx, ty = (W - tw) / 2, 120
    for dx in range(-7, 8, 2):
        for dy in range(-7, 8, 2):
            if dx * dx + dy * dy <= 49:
                d.text((tx + dx, ty + dy + 4), text, font=f, fill=(43, 36, 64))
    d.text((tx, ty), text, font=f, fill=(255, 255, 255))
    return bg


os.makedirs(OUT, exist_ok=True)
n = 0
for name, es, en in SHOTS:
    p = os.path.join(SRC, name + ".png")
    if not os.path.exists(p):
        print("falta", p)
        continue
    n += 1
    frame(p, es).save(os.path.join(OUT, "es_%02d_%s.png" % (n, name)))
    frame(p, en).save(os.path.join(OUT, "en_%02d_%s.png" % (n, name)))
print("capturas", n)
