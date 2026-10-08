"""Compara un render con la referencia: silueta (IoU), diferencia de color dentro de la silueta y una hoja lado a lado.
Uso: python3 compare.py ref/front.png out/front.png out/cmp_front.png
"""
import sys
from PIL import Image, ImageChops, ImageDraw
import numpy as np


def mask(im):
    a = np.asarray(im.convert("RGB")).astype(np.float32)
    mn = a.min(axis=2)
    mx = a.max(axis=2)
    sat = (mx - mn) / np.maximum(mx, 1)
    # fondo blanco/gris claro y sombra suave del piso quedan afuera
    return (mn < 165) | (sat > 0.18)


def main(ref_p, ren_p, out_p):
    ref = Image.open(ref_p).convert("RGB")
    rgba = Image.open(ren_p).convert("RGBA").resize(ref.size)
    white = Image.new("RGBA", rgba.size, (250, 250, 250, 255))
    ren = Image.alpha_composite(white, rgba).convert("RGB")
    mr, mn = mask(ref), mask(ren)
    inter = (mr & mn).sum()
    union = (mr | mn).sum()
    iou = inter / max(union, 1)
    a = np.asarray(ref).astype(np.float32)
    b = np.asarray(ren).astype(np.float32)
    both = mr & mn
    dcol = np.abs(a - b)[both].mean() if both.any() else 255
    # mapa de silueta: verde = solo referencia (falta), rojo = solo render (sobra), gris = coinciden
    h, w = mr.shape
    sil = np.full((h, w, 3), 255, np.uint8)
    sil[mr & mn] = (170, 170, 170)
    sil[mr & ~mn] = (40, 200, 60)
    sil[~mr & mn] = (230, 50, 50)
    sheet = Image.new("RGB", (w * 3, h + 24), (255, 255, 255))
    sheet.paste(ref, (0, 24))
    sheet.paste(ren, (w, 24))
    sheet.paste(Image.fromarray(sil), (2 * w, 24))
    d = ImageDraw.Draw(sheet)
    d.text((6, 4), "REFERENCIA", fill=(0, 0, 0))
    d.text((w + 6, 4), "BLENDER", fill=(0, 0, 0))
    d.text((2 * w + 6, 4), "silueta IoU=%.3f  color dE=%.1f  (verde falta, rojo sobra)" % (iou, dcol), fill=(0, 0, 0))
    sheet.save(out_p)
    print("IoU=%.4f colorDiff=%.2f" % (iou, dcol))


if __name__ == "__main__":
    main(*sys.argv[1:4])
