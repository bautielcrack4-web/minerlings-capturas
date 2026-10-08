"""Objetos de Curio Barn modelados en Blender por codigo (base en z=0, frente hacia -Y, ~0.3-0.5 m de alto).

Uso: blender -b -P items.py -- --only bottle,teddy --out ../../unity_rarezas/Assets/Resources/Models --preview /tmp/prev
Cada funcion arma las piezas y devuelve la lista de objetos; lib.finish los une, hornea la oclusion y exporta.
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import rbox, sphere, cyl, lathe, torus, tube, blob

ITEMS = {}


def item(fn):
    ITEMS[fn.__name__] = fn
    return fn


# ================================================================ NAUTICO
@item
def bottle():
    o = []
    # botella tumbada sobre un soportecito, con el mensaje enrollado adentro
    prof = [(0, 0), (0.105, 0), (0.12, 0.012), (0.122, 0.2), (0.11, 0.24), (0.06, 0.29), (0.042, 0.31), (0.04, 0.37), (0.05, 0.375), (0.05, 0.39), (0, 0.39)]
    g = lathe("vidrio", (0, 0.2, 0.135), prof, "4fbf8f", rot=(90, 0, 0), rough=0.15)
    o.append(g)
    o.append(cyl("corcho", (0, -0.17, 0.135), 0.036, 0.07, "c99a6a", r2=0.042, rot=(90, 0, 0), bev=0.008))
    o.append(sphere("lacre", (0, -0.245, 0.135), 0.042, "e0594a", scale=(1, 0.7, 1)))
    o.append(torus("soga", (0, -0.13, 0.135), 0.046, 0.009, "d9b07a", rot=(90, 0, 0)))
    o.append(cyl("papel", (0.0, 0.14, 0.11), 0.045, 0.2, "fff3d6", rot=(90, 0, 0), bev=0.01))
    o.append(torus("cinta", (0, 0.04, 0.11), 0.047, 0.007, "e0594a", rot=(90, 0, 0)))
    for x in (-0.13, 0.13):
        o.append(rbox("soporte", (0, x, 0.03), (0.2, 0.05, 0.06), r=0.015, col="wood"))
        o.append(rbox("cuna", (0, x, 0.065), (0.12, 0.052, 0.03), r=0.012, col="woodd"))
    return o


@item
def compass():
    o = []
    o.append(cyl("caja", (0, 0, 0), 0.14, 0.06, "brass", seg=40, bev=0.02, rough=0.3, metal=0.6))
    o.append(torus("bisel", (0, 0, 0.06), 0.125, 0.014, "gold", seg=40, metal=0.6, rough=0.3))
    o.append(cyl("esfera", (0, 0, 0.05), 0.118, 0.014, "fff3d6", seg=40))
    o.append(cyl("rosa", (0, 0, 0.062), 0.07, 0.004, "e9dcc0", seg=8))
    o.append(rbox("aguja_n", (0, -0.045, 0.072), (0.032, 0.1, 0.01), r=0.004, col="red"))
    o.append(rbox("aguja_s", (0, 0.045, 0.072), (0.032, 0.1, 0.01), r=0.004, col="ink"))
    o.append(sphere("eje", (0, 0, 0.08), 0.014, "gold"))
    o.append(torus("aro", (0, 0.155, 0.04), 0.03, 0.009, "brass", rot=(90, 0, 0), metal=0.6, rough=0.3))
    o.append(cyl("cuello", (0, 0.13, 0.04), 0.016, 0.03, "brass", rot=(-90, 0, 0)))
    for k in range(8):
        a = k * math.pi / 4
        o.append(sphere("marca", (math.sin(a) * 0.098, -math.cos(a) * 0.098, 0.066), 0.009 if k % 2 else 0.013, "ink"))
    o.append(cyl("tapa", (0, 0.12, 0.045), 0.14, 0.03, "brass", seg=40, bev=0.01, rot=(-110, 0, 0), metal=0.6, rough=0.3))
    return o

@item
def ship_wheel():
    o = []
    # timon de pie, sobre una base chica
    z = 0.26
    o.append(torus("aro", (0, 0, z), 0.17, 0.022, "wood", rot=(90, 0, 0), seg=40))
    o.append(torus("aro2", (0, 0, z), 0.12, 0.012, "woodd", rot=(90, 0, 0), seg=40))
    o.append(cyl("cubo", (0, 0.03, z), 0.045, 0.06, "brass", rot=(90, 0, 0), bev=0.01, metal=0.6, rough=0.35))
    for k in range(8):
        a = k * math.pi / 4
        c, s = math.cos(a), math.sin(a)
        o.append(tube("rayo", [(c * 0.04, 0, z + s * 0.04), (c * 0.24, 0, z + s * 0.24)], 0.012, "woodl"))
        o.append(sphere("manija", (c * 0.255, 0, z + s * 0.255), 0.022, "woodl", scale=(1, 1, 1)))
    o.append(rbox("poste", (0, 0.04, 0.05), (0.06, 0.06, 0.1), r=0.02, col="woodd"))
    o.append(rbox("base", (0, 0.04, 0.012), (0.2, 0.14, 0.03), r=0.012, col="wood"))
    return o


@item
def diving_helmet():
    o = []
    m = dict(metal=0.7, rough=0.35)
    o.append(sphere("casco", (0, 0, 0.21), 0.17, "copper", **m))
    o.append(cyl("cuello", (0, 0, 0), 0.15, 0.08, "brass", seg=32, bev=0.015, **m))
    o.append(torus("collar", (0, 0, 0.08), 0.15, 0.018, "brass", **m))
    o.append(torus("marco", (0, -0.155, 0.22), 0.07, 0.018, "brass", rot=(90, 0, 0), **m))
    o.append(sphere("cristal", (0, -0.13, 0.22), 0.07, "9fd8e8", scale=(1, 0.55, 1), rough=0.05))
    for s_ in (-1, 1):
        o.append(torus("mlat", (s_ * 0.155, 0, 0.21), 0.042, 0.012, "brass", rot=(0, 90, 0), **m))
        o.append(sphere("vlat", (s_ * 0.14, 0, 0.21), 0.042, "9fd8e8", scale=(0.55, 1, 1), rough=0.05))
    for x in (-0.025, 0.025):
        o.append(rbox("barra", (x, -0.17, 0.22), (0.01, 0.012, 0.13), r=0.004, col="brass"))
    o.append(rbox("barrah", (0, -0.17, 0.22), (0.13, 0.012, 0.01), r=0.004, col="brass"))
    for k in range(10):
        a = k * 2 * math.pi / 10
        o.append(sphere("tornillo", (math.cos(a) * 0.15, math.sin(a) * 0.15, 0.04), 0.013, "gold"))
    o.append(cyl("valvula", (0, 0.12, 0.33), 0.025, 0.05, "brass", rot=(-40, 0, 0), bev=0.006))
    return o

@item
def ship_in_bottle():
    o = []
    # maqueta de galeon sobre su soporte (legendario nautico)
    o.append(rbox("tabla", (0, 0, 0.015), (0.16, 0.42, 0.03), r=0.012, col="woodd"))
    for y in (-0.12, 0.12):
        o.append(rbox("horquilla", (0, y, 0.05), (0.03, 0.03, 0.06), r=0.01, col="gold", metal=0.5, rough=0.3))
    prof = [(0.0, 0.0), (0.06, 0.0), (0.085, 0.04), (0.09, 0.08), (0.0, 0.08)]
    o.append(lathe("casco", (0, 0, 0.07), prof, "8a5a3a", seg=24))
    o[-1].scale = (0.9, 2.6, 1.0)
    o.append(rbox("cubierta", (0, 0, 0.15), (0.15, 0.4, 0.015), r=0.006, col="c98d55"))
    o.append(rbox("franja", (0, 0, 0.12), (0.165, 0.42, 0.018), r=0.008, col="ffd23a"))
    o.append(rbox("popa", (0, 0.17, 0.18), (0.14, 0.09, 0.07), r=0.015, col="8a5a3a"))
    for y, h in ((-0.12, 0.2), (0.0, 0.27), (0.12, 0.2)):
        o.append(cyl("mastil", (0, y, 0.15), 0.007, h, "woodd"))
        for k, z in enumerate((0.45, 0.75)):
            w = 0.16 - k * 0.04
            sail = sphere("vela", (0, y - 0.01, 0.15 + h * z), 0.05, "fbf6ee", scale=(w / 0.1, 0.25, 0.8))
            o.append(sail)
        o.append(rbox("bandera", (0.015, y, 0.16 + h), (0.03, 0.006, 0.02), r=0.003, col="red"))
    o.append(tube("bauprés", [(0, -0.18, 0.16), (0, -0.28, 0.22)], 0.006, "woodd"))
    return o

# ================================================================ JUGUETES
@item
def teddy():
    o = []
    fur, furl, dark = "b07a48", "e2b98a", "3a2620"
    o.append(sphere("panza", (0, 0, 0.14), 0.13, fur, scale=(1, 0.9, 1.05)))
    o.append(sphere("barriga", (0, -0.075, 0.13), 0.085, furl, scale=(1, 0.5, 1.1)))
    o.append(sphere("cabeza", (0, -0.01, 0.33), 0.115, fur))
    o.append(sphere("hocico", (0, -0.1, 0.31), 0.052, furl, scale=(1, 0.8, 0.8)))
    o.append(sphere("nariz", (0, -0.14, 0.325), 0.02, dark, scale=(1.3, 1, 0.9), rough=0.2))
    for s in (-1, 1):
        o.append(sphere("ojo", (s * 0.045, -0.1, 0.36), 0.016, dark, rough=0.15))
        o.append(sphere("oreja", (s * 0.085, 0.0, 0.43), 0.045, fur))
        o.append(sphere("orejai", (s * 0.085, -0.025, 0.43), 0.026, furl, scale=(1, 0.5, 1)))
        o.append(sphere("brazo", (s * 0.14, -0.03, 0.17), 0.05, fur, scale=(0.85, 0.85, 1.25)))
        o.append(sphere("pata", (s * 0.075, -0.09, 0.045), 0.055, fur, scale=(0.9, 1.25, 0.75)))
        o.append(sphere("planta", (s * 0.075, -0.155, 0.045), 0.035, furl, scale=(1, 0.4, 1)))
        o.append(sphere("moño", (s * 0.045, -0.085, 0.235), 0.035, "red", scale=(1.25, 0.6, 0.8), rough=0.3))
    o.append(sphere("nudo", (0, -0.1, 0.235), 0.02, "red"))
    o.append(sphere("remiendo", (0.07, -0.095, 0.17), 0.03, "6fa0d8", scale=(1, 0.3, 1)))
    return o


@item
def tin_robot():
    o = []
    o.append(rbox("torso", (0, 0, 0.17), (0.15, 0.11, 0.15), r=0.03, col="9aa8b8", metal=0.6, rough=0.3))
    o.append(rbox("cabeza", (0, 0, 0.3), (0.12, 0.1, 0.1), r=0.03, col="b8c4d0", metal=0.6, rough=0.3))
    for s in (-1, 1):
        o.append(cyl("ojo", (s * 0.03, -0.05, 0.31), 0.018, 0.012, "ffd23a", rot=(90, 0, 0), emis=0.6))
        o.append(cyl("brazo", (s * 0.09, 0, 0.2), 0.022, 0.1, "e0594a", rot=(0, 180, 0)))
        o.append(sphere("mano", (s * 0.09, 0, 0.09), 0.025, "6d747e"))
        o.append(rbox("pierna", (s * 0.04, 0, 0.05), (0.05, 0.06, 0.1), r=0.015, col="6d747e"))
        o.append(rbox("pie", (s * 0.04, -0.015, 0.012), (0.06, 0.09, 0.025), r=0.01, col="e0594a"))
    o.append(cyl("antena", (0, 0, 0.35), 0.006, 0.06, "6d747e"))
    o.append(sphere("bolita", (0, 0, 0.415), 0.018, "red", emis=0.4))
    o.append(rbox("boca", (0, -0.05, 0.275), (0.06, 0.01, 0.015), r=0.005, col="ink"))
    o.append(rbox("panel", (0, -0.056, 0.18), (0.08, 0.01, 0.07), r=0.01, col="ffd23a"))
    for k in range(3):
        o.append(sphere("boton", (-0.025 + k * 0.025, -0.064, 0.18), 0.009, ["e0594a", "5cbf63", "4a8fe0"][k]))
    o.append(cyl("llave", (0, 0.07, 0.17), 0.01, 0.04, "gold", rot=(-90, 0, 0)))
    o.append(rbox("alas_llave", (0, 0.11, 0.17), (0.06, 0.008, 0.02), r=0.006, col="gold"))
    return o


@item
def rocking_horse():
    o = []
    # balancines curvos
    for s in (-1, 1):
        pts = [(s * 0.075, -0.2 + i * 0.04, 0.03 + 0.06 * ((i - 5) / 5.0) ** 2) for i in range(11)]
        o.append(tube("balancin", pts, 0.016, "woodd"))
    for y in (-0.12, 0.12):
        for s in (-1, 1):
            o.append(tube("pata", [(s * 0.075, y, 0.04), (s * 0.04, y * 0.8, 0.17)], 0.014, "wood"))
    o.append(sphere("cuerpo", (0, 0, 0.2), 0.09, "fff3d6", scale=(0.75, 1.5, 0.75)))
    o.append(sphere("cuello", (0, -0.11, 0.27), 0.05, "fff3d6", scale=(0.7, 0.9, 1.3)))
    o.append(sphere("cabeza", (0, -0.15, 0.33), 0.048, "fff3d6", scale=(0.75, 1.35, 0.85)))
    o.append(sphere("hocico", (0, -0.205, 0.315), 0.03, "f2d6b0", scale=(0.9, 1, 0.8)))
    for s in (-1, 1):
        o.append(sphere("ojo", (s * 0.03, -0.17, 0.35), 0.01, "ink"))
        o.append(cyl("oreja", (s * 0.02, -0.13, 0.37), 0.014, 0.04, "fff3d6", r2=0.002))
    for k in range(6):
        o.append(sphere("crin", (0, -0.15 + k * 0.035, 0.36 - k * 0.02), 0.025, "e0594a"))
    o.append(rbox("montura", (0, 0.0, 0.265), (0.13, 0.12, 0.03), r=0.012, col="4a8fe0"))
    o.append(tube("cola", [(0, 0.13, 0.22), (0, 0.18, 0.2), (0, 0.2, 0.15)], 0.02, "e0594a"))
    for s in (-1, 1):
        o.append(sphere("manija", (s * 0.03, -0.12, 0.31), 0.012, "gold"))
    return o


@item
def music_box():
    o = []
    o.append(rbox("caja", (0, 0, 0.07), (0.26, 0.18, 0.14), r=0.02, col="8a3a5a", rough=0.4))
    o.append(rbox("tapa", (0, 0.1, 0.22), (0.26, 0.02, 0.17), r=0.012, col="8a3a5a", rot=(-15, 0, 0)))
    o.append(rbox("espejo", (0, 0.088, 0.22), (0.2, 0.006, 0.12), r=0.01, col="cfe8f2", rot=(-15, 0, 0), rough=0.1))
    o.append(rbox("ribete", (0, 0, 0.142), (0.27, 0.19, 0.012), r=0.006, col="gold", metal=0.6, rough=0.3))
    o.append(rbox("ribete2", (0, 0, 0.006), (0.27, 0.19, 0.012), r=0.006, col="gold", metal=0.6, rough=0.3))
    o.append(cyl("pedestal", (0, -0.01, 0.148), 0.03, 0.012, "gold"))
    # bailarina
    o.append(cyl("pierna", (0, -0.01, 0.16), 0.006, 0.05, "f5d3b8"))
    o.append(lathe("tutu", (0, -0.01, 0.2), [(0, 0), (0.05, 0.0), (0.055, 0.01), (0.02, 0.03), (0, 0.03)], "ff9ec0"))
    o.append(sphere("torso", (0, -0.01, 0.245), 0.018, "ff9ec0", scale=(1, 0.8, 1.4)))
    o.append(sphere("cabeza", (0, -0.01, 0.285), 0.016, "f5d3b8"))
    o.append(sphere("rodete", (0, 0.0, 0.302), 0.01, "5a3a28"))
    o.append(tube("brazos", [(-0.035, -0.01, 0.29), (0, -0.01, 0.31), (0.035, -0.01, 0.29)], 0.004, "f5d3b8"))
    o.append(cyl("manija", (0.13, 0, 0.07), 0.008, 0.04, "gold", rot=(0, 90, 0)))
    o.append(rbox("manija2", (0.175, 0, 0.085), (0.01, 0.01, 0.045), r=0.004, col="gold"))
    for x in (-0.07, 0.07):
        o.append(sphere("flor", (x, -0.092, 0.07), 0.018, "ffd23a", scale=(1, 0.4, 1)))
    return o


@item
def toy_train():
    o = []
    o.append(rbox("base", (0, 0, 0.06), (0.14, 0.34, 0.04), r=0.012, col="2b2440"))
    o.append(cyl("caldera", (0, -0.05, 0.14), 0.065, 0.2, "e0594a", rot=(90, 0, 0), bev=0.015, rough=0.3))
    o.append(rbox("cabina", (0, 0.1, 0.16), (0.14, 0.12, 0.17), r=0.02, col="4a8fe0", rough=0.3))
    o.append(rbox("techo", (0, 0.1, 0.255), (0.17, 0.15, 0.025), r=0.01, col="2b2440"))
    o.append(rbox("ventana", (0, 0.035, 0.18), (0.09, 0.012, 0.06), r=0.01, col="ffe7a0"))
    o.append(cyl("chimenea", (0, -0.15, 0.19), 0.025, 0.08, "2b2440", r2=0.035, bev=0.005))
    o.append(sphere("domo", (0, -0.04, 0.2), 0.03, "gold", metal=0.6, rough=0.3))
    o.append(cyl("frente", (0, -0.25, 0.14), 0.05, 0.012, "gold", rot=(90, 0, 0)))
    o.append(rbox("quitapiedras", (0, -0.24, 0.05), (0.13, 0.04, 0.05), r=0.01, col="e0594a", rot=(25, 0, 0)))
    for y in (-0.12, 0.0, 0.12):
        for s in (-1, 1):
            o.append(cyl("rueda", (s * 0.075, y, 0.05), 0.045, 0.02, "gold", rot=(0, 90, 0), bev=0.006, metal=0.5))
            o.append(cyl("eje", (s * 0.085, y, 0.05), 0.015, 0.012, "2b2440", rot=(0, 90, 0)))
    o.append(blob("humo", (0, -0.15, 0.32), 0.04, "f4f1e8", amp=0.25))
    o.append(blob("humo2", (0, -0.12, 0.38), 0.03, "f4f1e8", amp=0.25))
    return o


# ================================================================ ARTE
@item
def picture_frame():
    o = []
    o.append(rbox("marco", (0, 0, 0.17), (0.28, 0.035, 0.32), r=0.02, col="gold", metal=0.5, rough=0.35))
    o.append(rbox("lienzo", (0, -0.016, 0.17), (0.22, 0.012, 0.26), r=0.004, col="bfe4ff"))
    # paisaje: colina, sol y arbol
    o.append(sphere("colina", (0.0, -0.022, 0.07), 0.13, "8cc247", scale=(1, 0.08, 0.5)))
    o.append(sphere("sol", (0.06, -0.022, 0.25), 0.03, "ffd23a", scale=(1, 0.2, 1)))
    o.append(sphere("copa", (-0.05, -0.024, 0.16), 0.04, "5cae47", scale=(1, 0.2, 1)))
    o.append(rbox("tronco", (-0.05, -0.022, 0.11), (0.012, 0.006, 0.05), r=0.003, col="8a5a3a"))
    o.append(tube("atril", [(0, 0.02, 0.2), (0, 0.13, 0.0)], 0.01, "woodd"))
    for s in (-1, 1):
        o.append(sphere("adorno", (s * 0.13, -0.02, 0.32), 0.022, "gold", metal=0.5, rough=0.3))
    return o


@item
def vase():
    o = []
    prof = [(0, 0), (0.09, 0), (0.1, 0.02), (0.08, 0.05), (0.15, 0.14), (0.165, 0.21), (0.14, 0.29), (0.075, 0.36), (0.065, 0.4), (0.1, 0.44), (0.085, 0.45), (0.06, 0.43), (0, 0.43)]
    o.append(lathe("jarron", (0, 0, 0), prof, "3f7fd0", seg=40, rough=0.25))
    o.append(torus("banda1", (0, 0, 0.14), 0.15, 0.01, "gold", seg=40, metal=0.6, rough=0.3))
    o.append(torus("banda2", (0, 0, 0.29), 0.14, 0.009, "gold", seg=40, metal=0.6, rough=0.3))
    o.append(torus("banda3", (0, 0, 0.215), 0.166, 0.016, "fbf6ee", seg=40, rough=0.25))
    for i in range(8):
        a = i * math.pi / 4
        o.append(sphere("flor", (math.sin(a) * 0.162, -math.cos(a) * 0.162, 0.25), 0.02, "fbf6ee", scale=(1, 1, 1)))
    for s in (-1, 1):
        o.append(torus("asa", (s * 0.1, 0, 0.36), 0.045, 0.011, "gold", rot=(90, 0, 0), metal=0.6, rough=0.3))
    return o


@item
def plaster_bust():
    o = []
    st = "ece6da"
    o.append(rbox("pedestal", (0, 0, 0.04), (0.16, 0.16, 0.08), r=0.015, col="9aa1aa"))
    o.append(lathe("hombros", (0, 0, 0.08), [(0, 0), (0.07, 0), (0.12, 0.05), (0.11, 0.1), (0.04, 0.13), (0, 0.13)], st))
    o.append(cyl("cuello", (0, 0, 0.19), 0.035, 0.05, st))
    c = (0, -0.005, 0.28)
    o.append(sphere("cabeza", c, 0.075, st, scale=(0.92, 1, 1.12)))
    o.append(sphere("nariz", (0, -0.078, 0.28), 0.016, st, scale=(0.8, 1, 1.4)))
    for sx in (-1, 1):
        o.append(sphere("oreja", (sx * 0.068, 0, 0.28), 0.018, st, scale=(0.5, 1, 1.3)))
        o.append(sphere("ojo", (sx * 0.025, -0.068, 0.3), 0.012, "d9d1c0", scale=(1, 0.5, 0.7)))
    # rulos sobre el craneo (hemisferio de arriba y de atras)
    for ring, (phi, n) in enumerate(((0.35, 5), (0.8, 9), (1.2, 11))):
        for k in range(n):
            th = 2 * math.pi * k / n + ring * 0.3
            x, y, z = math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)
            if y < -0.55 and ring > 0:
                continue   # despejar la frente
            o.append(sphere("rulo", (c[0] + x * 0.07, c[1] + y * 0.075, c[2] + z * 0.08), 0.022, "e2dccf"))
    return o

@item
def golden_statuette():
    o = []
    o.append(rbox("base", (0, 0, 0.03), (0.15, 0.15, 0.06), r=0.012, col="2b2440"))
    o.append(rbox("placa", (0, -0.076, 0.03), (0.08, 0.004, 0.03), r=0.003, col="gold"))
    g = dict(col="gold", metal=0.8, rough=0.25)
    o.append(cyl("pie", (0, 0, 0.06), 0.035, 0.03, **g))
    o.append(lathe("tunica", (0, 0, 0.09), [(0, 0), (0.045, 0), (0.035, 0.08), (0.03, 0.14), (0, 0.15)], **g))
    o.append(sphere("cabeza", (0, 0, 0.27), 0.028, **g))
    o.append(tube("brazo", [(0.02, 0, 0.22), (0.05, 0, 0.27), (0.055, 0, 0.32)], 0.009, **g))
    o.append(sphere("estrella", (0.055, 0, 0.345), 0.022, "ffe680", emis=0.8, scale=(1, 0.5, 1)))
    o.append(tube("brazo2", [(-0.02, 0, 0.22), (-0.04, -0.01, 0.17)], 0.009, **g))
    o.append(blob("alas", (0, 0.04, 0.22), 0.05, "gold", amp=0.15, scale=(1.6, 0.3, 1.1), metal=0.8, rough=0.25))
    return o


@item
def crown():
    o = []
    o.append(rbox("cojin", (0, 0, 0.04), (0.26, 0.26, 0.08), r=0.035, col="c0392b", rough=0.7))
    for s in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        o.append(sphere("borla", (s[0] * 0.13, s[1] * 0.13, 0.04), 0.018, "gold"))
    g = dict(col="gold", metal=0.8, rough=0.22)
    o.append(cyl("aro", (0, 0, 0.08), 0.1, 0.06, seg=40, bev=0.01, **g))
    o.append(torus("borde", (0, 0, 0.085), 0.1, 0.01, seg=40, **g))
    for k in range(6):
        a = k * math.pi / 3
        x, y = math.sin(a) * 0.095, -math.cos(a) * 0.095
        o.append(cyl("punta", (x, y, 0.14), 0.026, 0.07, r2=0.004, **g))
        o.append(sphere("perla", (x * 1.03, y * 1.03, 0.215), 0.014, "fbf6ee", rough=0.2))
        o.append(sphere("gema", (x * 1.06, y * 1.06, 0.11), 0.016, ["e0594a", "4a8fe0", "5cbf63"][k % 3], rough=0.1, scale=(1, 0.6, 1.2)))
    o.append(sphere("cima", (0, 0, 0.2), 0.03, "b06ef0", rough=0.1))
    return o


# ================================================================ NATURALEZA
@item
def mushroom_jar():
    o = []
    # tronco cortado con hongos (naturaleza, comun)
    o.append(cyl("tronco", (0, 0, 0), 0.12, 0.11, "8a5a3a", seg=28, bev=0.015))
    o.append(cyl("corte", (0, 0, 0.105), 0.105, 0.012, "e2b98a", seg=28))
    for r in (0.07, 0.04):
        o.append(torus("anillo", (0, 0, 0.118), r, 0.004, "c98d55", seg=28))
    o.append(blob("musgo", (-0.07, -0.05, 0.1), 0.05, "5cae47", amp=0.3, scale=(1, 1, 0.4)))
    for (x, y, h, c) in ((-0.03, -0.02, 0.09, "e0594a"), (0.04, 0.02, 0.13, "f59a32"), (0.03, -0.07, 0.06, "e0594a"), (-0.05, 0.05, 0.07, "f59a32")):
        o.append(cyl("pie", (x, y, 0.11), 0.014, h, "fbf6ee", r2=0.011))
        o.append(sphere("sombrero", (x, y, 0.11 + h), 0.04 if h > 0.08 else 0.03, c, scale=(1, 1, 0.62)))
        for k in range(4):
            a = k * 1.6 + x * 20
            o.append(sphere("punto", (x + math.cos(a) * 0.024, y + math.sin(a) * 0.024, 0.122 + h), 0.007, "fbf6ee"))
    o.append(sphere("hoja", (0.09, -0.06, 0.02), 0.04, "c9a032", scale=(1, 0.6, 0.15), rot=(0, 0, 30)))
    return o

@item
def bird_nest():
    o = []
    o.append(torus("nido", (0, 0, 0.06), 0.11, 0.05, "b98a55", scale=(1, 1, 0.75)))
    o.append(cyl("fondo", (0, 0, 0.02), 0.1, 0.04, "8a5a3a"))
    for k in range(14):
        a = k * 2 * math.pi / 14
        o.append(tube("rama", [(math.cos(a) * 0.15, math.sin(a) * 0.15, 0.05 + 0.03 * math.sin(k)),
                               (math.cos(a + 0.6) * 0.1, math.sin(a + 0.6) * 0.1, 0.1)], 0.006, "d9b07a"))
    for (x, y) in ((-0.03, 0.0), (0.035, -0.02), (0.01, 0.04)):
        o.append(sphere("huevo", (x, y, 0.08), 0.035, "9fd8e8", scale=(1, 1, 1.25), rough=0.3))
    o.append(sphere("pluma", (0.09, -0.08, 0.1), 0.025, "4a8fe0", scale=(0.4, 1.6, 0.3)))
    return o


@item
def geode():
    o = []
    o.append(blob("roca", (0, 0, 0.11), 0.13, "8a8378", amp=0.18, scale=(1, 0.95, 0.85)))
    o.append(cyl("corte", (0, -0.07, 0.11), 0.115, 0.03, "e8e2f2", rot=(90, 0, 0), seg=28))
    o.append(cyl("hueco", (0, -0.082, 0.11), 0.085, 0.02, "7a3fc8", rot=(90, 0, 0), seg=28, emis=0.15))
    for k in range(10):
        a = k * 2 * math.pi / 10
        r = 0.055
        o.append(cyl("cristal", (math.cos(a) * r, -0.085, 0.11 + math.sin(a) * r), 0.016, 0.035, "c49cf5", r2=0.0,
                     rot=(90, 0, 0), seg=6, rough=0.1, emis=0.2))
    o.append(sphere("centro", (0, -0.088, 0.11), 0.03, "e2c8ff", emis=0.4, rough=0.1))
    return o


@item
def amber():
    o = []
    o.append(rbox("soporte", (0, 0, 0.03), (0.14, 0.12, 0.06), r=0.02, col="woodd"))
    o.append(blob("ambar", (0, 0, 0.16), 0.1, "f2a33a", amp=0.12, scale=(1, 0.7, 1.15), rough=0.1, emis=0.12))
    # mosquito adentro (asoma por delante)
    o.append(sphere("bicho", (0, -0.072, 0.17), 0.016, "3a2620", scale=(0.8, 0.6, 1.3)))
    for s in (-1, 1):
        o.append(sphere("ala", (s * 0.02, -0.073, 0.185), 0.016, "fff3d6", scale=(1.3, 0.3, 0.6)))
        o.append(tube("pata", [(s * 0.005, -0.073, 0.16), (s * 0.03, -0.074, 0.14)], 0.002, "3a2620"))
    return o


@item
def fossil_skull():
    o = []
    o.append(rbox("base", (0, 0, 0.03), (0.3, 0.2, 0.06), r=0.02, col="9aa1aa"))
    b = "e9dcc0"
    o.append(sphere("craneo", (0, 0.02, 0.14), 0.09, b, scale=(0.9, 1.5, 0.8)))
    o.append(sphere("hocico", (0, -0.12, 0.11), 0.05, b, scale=(0.8, 1.4, 0.7)))
    o.append(rbox("mandibula", (0, -0.08, 0.075), (0.09, 0.2, 0.03), r=0.012, col=b))
    for s in (-1, 1):
        o.append(sphere("ojo", (s * 0.05, -0.03, 0.17), 0.028, "6d5a48", scale=(0.6, 1, 1)))
        o.append(cyl("cuerno", (s * 0.06, 0.06, 0.2), 0.022, 0.12, b, r2=0.002, rot=(-35, s * 30, 0)))
    for k in range(6):
        o.append(cyl("diente", (-0.03 + k * 0.012, -0.165, 0.1), 0.006, 0.025, "fbf6ee", r2=0.001, rot=(180, 0, 0)))
    for k in range(4):
        o.append(tube("grieta", [(0.02 + k * 0.01, 0.06 - k * 0.03, 0.2), (0.03 + k * 0.01, 0.02 - k * 0.03, 0.19)], 0.003, "b9a888"))
    return o


# ================================================================ MISTERIO
@item
def old_key():
    o = []
    o.append(rbox("cojin", (0, 0, 0.025), (0.24, 0.16, 0.05), r=0.022, col="4a8fe0", rough=0.7))
    g = dict(col="brass", metal=0.7, rough=0.3)
    o.append(torus("ojo", (-0.07, 0, 0.07), 0.04, 0.012, rot=(90, 0, 0), **g))
    o.append(torus("ojo2", (-0.07, 0, 0.07), 0.018, 0.008, rot=(90, 0, 0), **g))
    o.append(cyl("cano", (-0.03, 0, 0.07), 0.011, 0.15, rot=(0, 90, 0), **g))
    o.append(rbox("diente", (0.1, 0, 0.05), (0.02, 0.012, 0.04), r=0.004, **g))
    o.append(rbox("diente2", (0.075, 0, 0.055), (0.015, 0.012, 0.03), r=0.004, **g))
    o.append(sphere("gema", (-0.07, -0.004, 0.07), 0.012, "e0594a", rough=0.1, emis=0.3))
    return o


@item
def crystal_ball():
    o = []
    o.append(lathe("pie", (0, 0, 0), [(0, 0), (0.1, 0), (0.1, 0.02), (0.06, 0.05), (0.07, 0.08), (0.05, 0.1), (0, 0.1)], "woodd"))
    for k in range(3):
        a = k * 2 * math.pi / 3
        o.append(tube("garra", [(math.cos(a) * 0.05, math.sin(a) * 0.05, 0.09), (math.cos(a) * 0.08, math.sin(a) * 0.08, 0.14)], 0.01, "gold", metal=0.6, rough=0.3))
    o.append(sphere("bola", (0, 0, 0.19), 0.1, "b9a8f0", rough=0.05, emis=0.25))
    o.append(blob("niebla", (0, -0.02, 0.18), 0.06, "e8dcff", amp=0.3, emis=0.5))
    o.append(sphere("brillo", (-0.04, -0.07, 0.24), 0.018, "ffffff", emis=1.0, scale=(1, 0.5, 1)))
    return o


@item
def lantern():
    o = []
    m = dict(col="2b3040", metal=0.6, rough=0.35)
    o.append(cyl("base", (0, 0, 0), 0.08, 0.03, bev=0.01, **m))
    o.append(cyl("luz", (0, 0, 0.03), 0.06, 0.16, "ffd88a", emis=1.2))
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        o.append(rbox("varilla", (math.cos(a) * 0.065, math.sin(a) * 0.065, 0.11), (0.014, 0.014, 0.17), r=0.005, **m))
    o.append(cyl("techo", (0, 0, 0.19), 0.09, 0.07, r2=0.02, bev=0.01, **m))
    o.append(torus("argolla", (0, 0, 0.29), 0.035, 0.008, rot=(90, 0, 0), col="brass", metal=0.6))
    o.append(sphere("llama", (0, 0, 0.1), 0.02, "fff1d6", emis=2.0, scale=(1, 1, 1.6)))
    return o


@item
def treasure_chest():
    o = []
    o.append(rbox("caja", (0, 0, 0.08), (0.3, 0.2, 0.16), r=0.02, col="8a5a3a"))
    o.append(cyl("tapa", (-0.158, 0, 0.165), 0.098, 0.316, "a8703f", rot=(0, 90, 0), seg=24, bev=0.012))
    for x in (-0.11, 0.11):
        o.append(rbox("fleje", (x, 0, 0.08), (0.03, 0.21, 0.165), r=0.008, col="gold", metal=0.6, rough=0.3))
        o.append(torus("flejet", (x, 0, 0.16), 0.1, 0.014, rot=(0, 90, 0), col="gold", metal=0.6, rough=0.3, scale=(1, 1.05, 1.05)))
    o.append(rbox("cerradura", (0, -0.105, 0.14), (0.05, 0.02, 0.06), r=0.01, col="gold", metal=0.6, rough=0.3))
    o.append(rbox("ojo", (0, -0.117, 0.135), (0.012, 0.006, 0.02), r=0.003, col="2b2440"))
    for k in range(5):
        o.append(cyl("moneda", (-0.1 + k * 0.05, -0.13, 0.008 + (k % 2) * 0.005), 0.025, 0.008, "coin", rot=(0, 0, 0)))
    o.append(sphere("gema", (0.12, -0.13, 0.02), 0.02, "4a8fe0", rough=0.1))
    return o


@item
def dragon_egg():
    o = []
    o.append(blob("nido", (0, 0, 0.04), 0.13, "8a8378", amp=0.25, scale=(1, 1, 0.35)))
    prof = [(0, 0), (0.06, 0.005), (0.1, 0.05), (0.11, 0.12), (0.095, 0.2), (0.06, 0.26), (0, 0.285)]
    o.append(lathe("huevo", (0, 0, 0.05), prof, "5cbf63", seg=36, rough=0.25))
    # escamas en anillos
    for ring in range(4):
        z = 0.1 + ring * 0.05
        rr = [0.1, 0.11, 0.1, 0.075][ring]
        for k in range(9):
            a = k * 2 * math.pi / 9 + ring * 0.35
            o.append(sphere("escama", (math.cos(a) * rr, math.sin(a) * rr, z), 0.024, ["3f9a52", "ffd23a"][(k + ring) % 4 == 0],
                            scale=(1, 1, 0.7), rough=0.2))
    o.append(sphere("brillo", (-0.035, -0.06, 0.25), 0.02, "ffffff", emis=0.8, scale=(1, 0.5, 1.3)))
    for k in range(5):
        a = k * 2 * math.pi / 5
        o.append(cyl("cristal", (math.cos(a) * 0.12, math.sin(a) * 0.12, 0.03), 0.018, 0.06, "e0594a", r2=0.0, seg=6, rot=(15 * math.cos(a), 15 * math.sin(a), 0), emis=0.4))
    return o


# ================================================================ BOSQUE (catalogo v2; base en z=0, tamaño real junto a Pipo de ~1 m)
@item
def tin_cup():
    o = []
    m = dict(metal=0.55, rough=0.35)
    o.append(cyl("taza", (0, 0, 0), 0.07, 0.11, "a9b4bf", r2=0.075, seg=28, bev=0.008, **m))
    o.append(torus("borde", (0, 0, 0.11), 0.073, 0.007, "c9d2da", seg=28, **m))
    o.append(cyl("fondo", (0, 0, 0.095), 0.066, 0.01, "6d747e", seg=28))
    o.append(tube("asa", [(0.07, 0, 0.09), (0.11, 0, 0.085), (0.115, 0, 0.04), (0.07, 0, 0.03)], 0.01, "a9b4bf", **m))
    for k in range(4):
        a = k * 1.7
        o.append(sphere("abollon", (math.cos(a) * 0.07, math.sin(a) * 0.07, 0.03 + k * 0.018), 0.012, "8a96a4"))
    o.append(cyl("esmalte", (0, 0, 0.04), 0.0745, 0.03, "4a8fe0", r2=0.0745, seg=28))
    return o


@item
def wood_pipe():
    o = []
    o.append(cyl("hornillo", (0.06, 0, 0), 0.045, 0.09, "8a5530", r2=0.05, seg=24, bev=0.012))
    o.append(cyl("ceniza", (0.06, 0, 0.083), 0.035, 0.01, "3a2a22", seg=20))
    o.append(torus("aro", (0.06, 0, 0.085), 0.045, 0.006, "d9a441", seg=24, metal=0.6, rough=0.3))
    o.append(tube("boquilla", [(0.02, 0, 0.03), (-0.05, 0, 0.045), (-0.12, 0, 0.07), (-0.16, 0, 0.075)], 0.014, "5a3a28"))
    o.append(cyl("punta", (-0.16, 0, 0.075), 0.012, 0.03, "2b2440", rot=(0, -80, 0)))
    o.append(rbox("soporte", (0, 0, 0.01), (0.2, 0.08, 0.02), r=0.008, col="c98d55"))
    return o


@item
def stone_nest():
    o = []
    o.append(blob("nido", (0, 0, 0.05), 0.14, "c49a5a", amp=0.28, scale=(1, 1, 0.42)))
    for k in range(10):
        a = k * 0.63
        o.append(tube("ramita", [(math.cos(a) * 0.12, math.sin(a) * 0.12, 0.07), (math.cos(a + 0.6) * 0.15, math.sin(a + 0.6) * 0.15, 0.085)], 0.007, "8a6a3a"))
    o.append(blob("hueco", (0, 0, 0.09), 0.09, "7a5a30", amp=0.2, scale=(1, 1, 0.3)))
    prof = [(0, 0), (0.035, 0.004), (0.055, 0.035), (0.057, 0.07), (0.045, 0.11), (0, 0.13)]
    o.append(lathe("huevo", (0, 0, 0.075), prof, "b8c0c8", seg=28, rough=0.7))
    for k in range(5):
        a = k * 1.25
        o.append(sphere("moteado", (math.cos(a) * 0.05, math.sin(a) * 0.05, 0.12 + (k % 2) * 0.03), 0.01, "8a96a4"))
    o.append(sphere("pluma", (0.11, -0.05, 0.1), 0.025, "e0594a", scale=(2.2, 0.5, 0.4), rot=(0, 30, 25)))
    return o


@item
def pocket_watch():
    o = []
    m = dict(metal=0.65, rough=0.28)
    o.append(cyl("caja", (0, 0, 0.0), 0.085, 0.03, "ffd23a", seg=36, bev=0.012, **m))
    o.append(cyl("esfera", (0, 0, 0.026), 0.072, 0.008, "fff6e4", seg=36))
    for k in range(12):
        a = k * math.pi / 6
        o.append(sphere("hora", (math.sin(a) * 0.058, -math.cos(a) * 0.058, 0.034), 0.006 if k % 3 else 0.009, "2b2440"))
    o.append(rbox("aguja1", (0.0, -0.022, 0.036), (0.008, 0.045, 0.004), r=0.002, col="2b2440"))
    o.append(rbox("aguja2", (0.016, 0.0, 0.037), (0.034, 0.007, 0.004), r=0.002, col="2b2440"))
    o.append(sphere("eje", (0, 0, 0.04), 0.008, "e0594a"))
    o.append(cyl("tapa", (0, 0.085, 0.012), 0.085, 0.02, "e0b030", seg=36, bev=0.01, rot=(-60, 0, 0), **m))
    o.append(cyl("corona", (0, -0.1, 0.015), 0.014, 0.025, "ffd23a", rot=(90, 0, 0), **m))
    o.append(torus("argolla", (0, -0.13, 0.015), 0.018, 0.005, "ffd23a", rot=(90, 0, 0), **m))
    pts = [(0.0, -0.15, 0.01), (0.05, -0.19, 0.006), (0.12, -0.17, 0.006), (0.15, -0.1, 0.006)]
    o.append(tube("cadena", pts, 0.005, "e0b030", **m))
    return o


@item
def dry_mushroom():
    o = []
    o.append(cyl("tallo", (0, 0, 0), 0.09, 0.32, "f0e2c0", r2=0.07, seg=24, bev=0.03))
    o.append(torus("anillo", (0, 0, 0.24), 0.08, 0.018, "e8d6ae", seg=24))
    prof = [(0, 0), (0.26, 0.0), (0.27, 0.03), (0.24, 0.09), (0.16, 0.15), (0.06, 0.18), (0, 0.185)]
    o.append(lathe("sombrero", (0, 0, 0.3), prof, "c46a3a", seg=36))
    o.append(cyl("laminas", (0, 0, 0.295), 0.25, 0.012, "e8c89a", seg=36))
    surf = [(0.0, 0.185), (0.06, 0.18), (0.16, 0.15), (0.24, 0.09)]

    def top(r):
        for (r0, z0), (r1, z1) in zip(surf, surf[1:]):
            if r <= r1:
                return z0 + (z1 - z0) * (r - r0) / (r1 - r0)
        return 0.09
    for k in range(10):
        a = k * 0.7
        r = 0.04 + (k % 3) * 0.07
        o.append(sphere("lunar", (math.cos(a) * r, math.sin(a) * r, 0.3 + top(r) - 0.008), 0.03 + (k % 2) * 0.01, "fbf1d8", scale=(1, 1, 0.45)))
    o.append(blob("tierra", (0, 0, 0.0), 0.12, "8a6a3a", amp=0.3, scale=(1, 1, 0.3)))
    return o


@item
def rusty_axe():
    o = []
    # hacha apoyada en un tocón chiquito
    o.append(cyl("tocon", (0, 0, 0), 0.13, 0.12, "a8703f", seg=20, bev=0.02))
    o.append(cyl("anillos", (0, 0, 0.12), 0.115, 0.004, "d9b07a", seg=20))
    o.append(tube("mango", [(0.0, 0.0, 0.12), (-0.02, 0.0, 0.3), (-0.01, 0.0, 0.5)], 0.02, "c98d55"))
    o.append(rbox("hoja", (0.04, 0.0, 0.12), (0.17, 0.035, 0.12), r=0.01, col="9a6a4a", metal=0.4, rough=0.6, rot=(0, -8, 0)))
    o.append(rbox("filo", (0.12, 0.0, 0.12), (0.03, 0.037, 0.15), r=0.008, col="b8c0c8", metal=0.6, rough=0.4))
    for k in range(4):
        o.append(sphere("oxido", (0.02 + k * 0.03, -0.02, 0.1 + (k % 2) * 0.04), 0.014, "c4683a", scale=(1, 0.3, 1)))
    o.append(torus("cuero", (-0.015, 0, 0.42), 0.024, 0.008, "7a4a2a", seg=16))
    return o


@item
def old_lantern():
    o = []
    m = dict(col="3a4048", metal=0.55, rough=0.45)
    o.append(cyl("base", (0, 0, 0), 0.12, 0.05, bev=0.015, seg=24, **m))
    o.append(cyl("vidrio", (0, 0, 0.05), 0.095, 0.22, "ffe2a0", seg=24, emis=0.9, rough=0.1))
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        o.append(rbox("varilla", (math.cos(a) * 0.1, math.sin(a) * 0.1, 0.16), (0.022, 0.022, 0.23), r=0.008, **m))
    o.append(torus("aro", (0, 0, 0.16), 0.1, 0.01, seg=24, **m))
    o.append(cyl("techo", (0, 0, 0.27), 0.13, 0.1, r2=0.03, bev=0.015, seg=24, **m))
    o.append(cyl("chimenea", (0, 0, 0.36), 0.03, 0.04, seg=16, **m))
    o.append(torus("asa", (0, 0, 0.44), 0.05, 0.011, rot=(90, 0, 0), col="c98d55", seg=20))
    o.append(sphere("llama", (0, 0, 0.13), 0.03, "fff1d6", emis=2.0, scale=(1, 1, 1.7)))
    for k in range(3):
        o.append(sphere("oxido", (0.11 * math.cos(k * 2.1), 0.11 * math.sin(k * 2.1), 0.03), 0.015, "a8603a"))
    return o


@item
def banjo():
    o = []
    # banjo apoyado de canto sobre un pie chico
    o.append(cyl("parche", (0, 0, 0.17), 0.15, 0.04, "f3ead8", seg=36, rot=(80, 0, 0)))
    o.append(torus("aro", (0, 0.004, 0.17), 0.155, 0.02, "a8703f", seg=36, rot=(80, 0, 0)))
    o.append(cyl("caja", (0, 0.03, 0.17), 0.152, 0.05, "8a5530", seg=36, rot=(80, 0, 0)))
    for k in range(12):
        a = k * math.pi / 6
        o.append(sphere("tornillo", (math.cos(a) * 0.158, -0.02, 0.17 + math.sin(a) * 0.156), 0.008, "d9a441", metal=0.6, rough=0.3))
    o.append(rbox("mastil", (0, -0.03, 0.46), (0.05, 0.03, 0.42), r=0.01, col="5a3a28", rot=(-10, 0, 0)))
    o.append(rbox("clavijero", (0, -0.07, 0.69), (0.07, 0.035, 0.1), r=0.015, col="5a3a28", rot=(-10, 0, 0)))
    for s_ in (-1, 1):
        for k in range(2):
            o.append(cyl("clavija", (s_ * 0.035, -0.07, 0.67 + k * 0.04), 0.01, 0.03, "fbf6ee", rot=(0, s_ * 90, 0)))
    for k in range(4):
        x = -0.018 + k * 0.012
        o.append(tube("cuerda", [(x, -0.025, 0.12), (x, -0.06, 0.68)], 0.0025, "e8e8f0"))
    o.append(rbox("puente", (0, -0.02, 0.2), (0.06, 0.012, 0.012), r=0.003, col="f0cf6a"))
    o.append(rbox("pie", (0, 0.04, 0.012), (0.18, 0.12, 0.025), r=0.01, col="a8703f"))
    return o


@item
def carved_log():
    o = []
    # tronco tallado con forma de buho
    o.append(cyl("tronco", (0, 0, 0), 0.16, 0.48, "a8703f", r2=0.14, seg=28, bev=0.03))
    o.append(cyl("corte", (0, 0, 0.48), 0.135, 0.01, "e8c89a", seg=28))
    o.append(torus("anillo", (0, 0, 0.485), 0.08, 0.005, "c49a5a", seg=24))
    for s_ in (-1, 1):
        o.append(cyl("ojo", (s_ * 0.06, -0.135, 0.33), 0.05, 0.03, "f0cf6a", rot=(90, 0, 0), seg=20))
        o.append(cyl("pupila", (s_ * 0.06, -0.162, 0.33), 0.025, 0.01, "2b2440", rot=(90, 0, 0), seg=16))
        o.append(sphere("ceja", (s_ * 0.075, -0.13, 0.39), 0.045, "7a4a2a", scale=(1.3, 0.5, 0.4), rot=(0, s_ * 20, 0)))
        o.append(sphere("ala", (s_ * 0.15, -0.03, 0.18), 0.07, "7a4a2a", scale=(0.4, 0.8, 1.4)))
    o.append(cyl("pico", (0, -0.15, 0.28), 0.025, 0.05, "e09a1c", r2=0.0, rot=(100, 0, 0), seg=12))
    for k in range(6):
        o.append(sphere("pluma", ((k % 3 - 1) * 0.05, -0.14, 0.12 + (k // 3) * 0.06), 0.03, "8a5530", scale=(1, 0.4, 0.8)))
    o.append(blob("musgo", (0.05, 0.08, 0.46), 0.07, "76ab3a", amp=0.3, scale=(1.2, 1, 0.45)))
    return o


@item
def rocking_chair():
    o = []
    w = "a8703f"
    for s_ in (-1, 1):
        # balancines curvos
        pts = [(s_ * 0.22, -0.32 + i * 0.08, 0.02 + 0.06 * ((i - 4) / 4.0) ** 2) for i in range(9)]
        o.append(tube("balancin", pts, 0.022, "7a4a2a"))
        for y in (-0.18, 0.15):
            o.append(rbox("pata", (s_ * 0.22, y, 0.17), (0.04, 0.04, 0.3), r=0.012, col=w))
        o.append(rbox("poste", (s_ * 0.22, 0.17, 0.55), (0.045, 0.045, 0.52), r=0.015, col=w, rot=(-8, 0, 0)))
        o.append(rbox("brazo", (s_ * 0.24, -0.05, 0.42), (0.06, 0.36, 0.035), r=0.014, col=w))
        o.append(sphere("pomo", (s_ * 0.22, 0.2, 0.82), 0.032, "7a4a2a"))
    o.append(rbox("asiento", (0, -0.02, 0.32), (0.46, 0.42, 0.045), r=0.015, col="c98d55"))
    o.append(rbox("almohadon", (0, -0.02, 0.36), (0.38, 0.34, 0.05), r=0.025, col="e0594a", plaid=("a83a30", "ffd2b0", 0.06)))
    for k in range(4):
        o.append(rbox("varilla", (-0.15 + k * 0.1, 0.19, 0.58), (0.03, 0.025, 0.4), r=0.01, col=w, rot=(-8, 0, 0)))
    o.append(rbox("copete", (0, 0.21, 0.79), (0.46, 0.05, 0.08), r=0.02, col="7a4a2a", rot=(-8, 0, 0)))
    o.append(rbox("travesano", (0, 0.18, 0.4), (0.44, 0.03, 0.04), r=0.01, col=w))
    return o


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
    prev = None
    for i, a in enumerate(argv):
        if a == "--only":
            only = argv[i + 1].split(",")
        if a == "--out":
            out = argv[i + 1]
        if a == "--preview":
            prev = argv[i + 1]
    names = only or list(ITEMS)
    report = []
    for n in names:
        lib.reset()
        objs = ITEMS[n]()
        objs = [x for x in objs if x is not None]
        o, tris = lib.finish(objs, "item_" + n, out)
        report.append((n, tris))
        if prev:
            os.makedirs(prev, exist_ok=True)
            lib.preview([o], os.path.join(prev, n + ".png"), size=420)
    for n, t in report:
        print("ITEM", n, t)


if __name__ == "__main__":
    main()
