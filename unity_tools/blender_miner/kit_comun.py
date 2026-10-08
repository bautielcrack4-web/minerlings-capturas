"""Techo COMUN del Complejo por variantes (PLAN_HABITACIONES §8, techo continuo): una pieza por mascara de vecinas
techadas (bits N=1, E=2, S=4, O=8; limahoyas NE=16, SE=32, SO=64, NO=128 -> Resources/RoomKit/comun_techo_<mascara>). Pizarra gris-azul, aleros y limas de
madera, esquineros de hierro; el tema solo suma su remate (<tema>_remate). Sin estirar: escala 1, origen en el centro
de la celda a la altura del borde de la pared (RoomKit.Place).
uso: blender -b -P kit_comun.py -- outdir [--sheet png]
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *
import kit_lib as K

PAL = {"pizarra_a": hexc(SLATE["pizarra_a"]), "pizarra_b": hexc(SLATE["pizarra_b"]), "madera": hexc("#7a4a26"),
       "madera_b": hexc("#6b4325"), "hierro": hexc("#2a2a2c", 1.0)}
setup("comun", PAL, rivet="hierro")

obs = {}
# 0..15: lados techados; 16..255: ademas limahoyas en las esquinas interiores de una L (kit_lib.roof_valid)
for mask in range(256):
    if not roof_valid(mask): continue
    roof_variant(mask, "hierro", "madera", "madera_b")
    ob = join("techo_%d" % mask); export(ob, "comun_techo_%d" % mask); obs[mask] = ob

if K.SHEET:
    # una L y una fila de tres: tienen que leerse como UN techo
    made = []
    def put(mask, x, y):
        o = obs[mask].copy(); o.data = obs[mask].data.copy(); K.sc.collection.objects.link(o); o.location = (x, y, 0); o.hide_render = False; made.append(o)
    for o in obs.values(): o.hide_render = True
    put(2, -4, 0); put(10, -2, 0); put(8, 0, 0)        # fila E-O
    put(3 | 16, 2.5, 0); put(8, 4.5, 0); put(4, 2.5, 2)      # L con su limahoya
    put(0, 7.5, 0)
    d = Vector((math.sin(math.radians(35)) * math.cos(math.radians(48)), -math.cos(math.radians(35)) * math.cos(math.radians(48)), math.sin(math.radians(48))))
    K.lights_and_render(K.SHEET, 13.5, Vector((1.8, 0.6, 0.4)) + d * 30, (-d).to_track_quat("-Z", "Y").to_euler(), 1800, 800)
