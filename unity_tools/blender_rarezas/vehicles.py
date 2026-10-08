"""Vehiculos de Rarezas (DIRECCION_CREATIVA 7): Tostada (la yegua) con el chango de madera, y la camioneta con rampa.
Por piezas con pivote para animarlas en Unity. Medidas en Unity con U() (frente hacia +Z, que es hacia donde avanza).

horse: body (0,0,0), head (0, 0.95, 0.42) cuello y cabeza, legFL/legFR/legBL/legBR (cadera a y 0.58), tail (0, 0.82, -0.44)
cart:  body (0,0,0) caja, varas y asiento; wheelL/wheelR (eje en x +-0.62, y 0.36, z 0)
van:   body (0,0,0); wheelFL/FR/BL/BR; ramp (bisagra atras)

Uso: blender -b -P vehicles.py -- [--only horse,cart,van] [--preview carpeta]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import sphere, cyl, torus, tube, blob
from shack import U, UB, UC, US

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../unity_rarezas/Assets/Resources/Models")
COAT, COATD, MANE = "b8743a", "8a5428", "f3e2c0"


def horse():
    """Tostada, chibi: cuerpo redondo, patas cortas y gorditas, cabeza grande con flequillo crema."""
    body = [US("cuerpo", 0, 0.72, -0.02, 0.3, COAT, scale=(0.95, 0.9, 1.45))]
    body.append(US("panza", 0, 0.64, 0.0, 0.25, "d59a5e", scale=(0.9, 0.7, 1.25)))
    body.append(US("pecho", 0, 0.8, 0.3, 0.24, COAT, scale=(0.9, 0.95, 0.85)))
    body.append(UB("manta", 0, 0.98, -0.02, 0.5, 0.06, 0.4, r=0.03, col="4a8fe0", plaid=("2f6bb0", "bfe0ff", 0.08)))
    body.append(torus("collera", U(0, 0.96, 0.42), 0.15, 0.04, "8a5a30", rot=(60, 0, 0)))
    for s_ in (-1, 1):
        body.append(US("hebilla", s_ * 0.15, 0.96, 0.44, 0.03, "ffd23a", metal=0.6, rough=0.3))
    # cabeza (pivote en la base del cuello)
    head = [US("cuello", 0, 1.02, 0.46, 0.15, COAT, scale=(0.85, 1.3, 0.9))]
    head.append(US("cabeza", 0, 1.22, 0.58, 0.21, COAT, scale=(0.9, 0.95, 1.15)))
    head.append(US("hocico", 0, 1.13, 0.78, 0.15, "e8b886", scale=(1.0, 0.85, 0.9)))
    for s_ in (-1, 1):
        head.append(US("narina", s_ * 0.055, 1.14, 0.9, 0.022, "5a3420"))
        head.append(US("ojo", s_ * 0.13, 1.28, 0.7, 0.05, "2b2440"))
        head.append(US("brillo", s_ * 0.14, 1.3, 0.74, 0.016, "ffffff"))
        head.append(UC("oreja", s_ * 0.1, 1.36, 0.52, 0.045, 0.14, COAT, r2=0.008, rz=s_ * 18, rx=-12, seg=10))
    for k in range(4):
        head.append(US("crin", 0, 1.34 - k * 0.1, 0.46 - k * 0.07, 0.075, MANE, scale=(0.6, 1, 1)))
    head.append(US("flequillo", 0, 1.38, 0.66, 0.08, MANE, scale=(1.3, 0.7, 0.9)))
    head.append(torus("bozal", U(0, 1.14, 0.78), 0.15, 0.02, "8a5a30", rot=(90, 0, 0), scale=(1, 1.1, 1)))
    groups = [("body", body, (0, 0, 0)), ("head", head, U(0, 0.95, 0.42))]
    for name, x, z in (("legFL", -0.15, 0.28), ("legFR", 0.15, 0.28), ("legBL", -0.15, -0.3), ("legBR", 0.15, -0.3)):
        lg = [UC("pata", x, 0.07, z, 0.085, 0.5, COAT, r2=0.075, bev=0.03, seg=12)]
        lg.append(UC("vaso", x, 0.0, z, 0.09, 0.1, "4a3426", seg=12, bev=0.02))
        lg.append(UC("pelo", x, 0.09, z, 0.095, 0.06, MANE, seg=12))
        groups.append((name, lg, U(x, 0.58, z)))
    tail = [tube("cola", [U(0, 0.82, -0.44), U(0, 0.66, -0.58), U(0, 0.42, -0.6)], 0.065, MANE)]
    tail.append(US("punta", 0, 0.4, -0.6, 0.085, MANE, scale=(0.8, 1.2, 0.8)))
    groups.append(("tail", tail, U(0, 0.82, -0.44)))
    return groups


def wheel(x):
    o = [cyl("llanta", U(x, 0.36, 0), 0.36, 0.07, "6a4026", seg=28, rot=(0, 90, 0), bev=0.015)]
    o.append(cyl("cubo", U(x, 0.36, 0), 0.08, 0.12, "3a3a44", seg=16, rot=(0, 90, 0), metal=0.5, rough=0.4))
    for k in range(8):
        a = k * math.pi / 4
        o.append(tube("rayo", [U(x, 0.36, 0), U(x, 0.36 + math.sin(a) * 0.32, math.cos(a) * 0.32)], 0.02, "a8703f"))
    o.append(torus("aro", U(x, 0.36, 0), 0.34, 0.025, "8a5a30", rot=(0, 90, 0), seg=32))
    return o


def cart():
    body = []
    body.append(UB("piso", 0, 0.5, -0.1, 1.1, 0.08, 1.3, r=0.03, col="a8703f"))
    for s_ in (-1, 1):
        body.append(UB("baranda", s_ * 0.55, 0.58, -0.1, 0.07, 0.32, 1.3, r=0.02, col="c98d55"))
        for k in range(4):
            body.append(UB("estaca", s_ * 0.56, 0.5, -0.65 + k * 0.36, 0.08, 0.46, 0.08, r=0.02, col="7a4a2a"))
        # varas hacia el caballo
        body.append(UB("vara", s_ * 0.38, 0.62, 1.05, 0.06, 0.06, 1.3, rx=-6, r=0.02, col="8a5a30"))
    body.append(UB("atras", 0, 0.58, -0.76, 1.1, 0.3, 0.07, r=0.02, col="c98d55"))
    body.append(UB("adelante", 0, 0.58, 0.56, 1.1, 0.3, 0.07, r=0.02, col="c98d55"))
    body.append(UB("asiento", 0, 0.88, 0.45, 0.9, 0.08, 0.3, r=0.03, col="7a4a2a"))
    body.append(UB("almohadon", 0, 0.94, 0.45, 0.8, 0.06, 0.26, r=0.03, col="e0594a"))
    body.append(UB("eje", 0, 0.32, 0, 1.24, 0.07, 0.07, r=0.02, col="3a3a44", metal=0.4, rough=0.5))
    body.append(UC("farol", 0.5, 0.92, 0.58, 0.05, 0.14, "ffe2a0", seg=8, emis=0.8))
    return [("body", body, (0, 0, 0)), ("wheelL", wheel(-0.62), U(-0.62, 0.36, 0)), ("wheelR", wheel(0.62), U(0.62, 0.36, 0))]


def van():
    body = []
    body.append(UB("chasis", 0, 0.36, 0, 1.5, 0.22, 2.8, r=0.08, col="3a3a44"))
    body.append(UB("caja", 0, 0.58, -0.45, 1.5, 0.12, 1.85, r=0.05, col="5cbf63"))
    for s_ in (-1, 1):
        body.append(UB("lado", s_ * 0.72, 0.7, -0.45, 0.08, 0.32, 1.85, r=0.03, col="4aa84f"))
    body.append(UB("cabina", 0, 0.58, 0.85, 1.5, 0.82, 1.0, r=0.18, col="5cbf63"))
    body.append(UB("vidrio", 0, 1.0, 1.36, 1.3, 0.36, 0.04, rx=-12, r=0.06, col="bfe4ff", rough=0.1, emis=0.1))
    for s_ in (-1, 1):
        body.append(UB("ventana", s_ * 0.76, 1.0, 0.85, 0.02, 0.32, 0.6, r=0.04, col="bfe4ff", rough=0.1))
        body.append(US("faro", s_ * 0.52, 0.7, 1.37, 0.1, "fff1b0", emis=1.0, scale=(1, 1, 0.5)))
    body.append(UB("paragolpe", 0, 0.38, 1.42, 1.56, 0.12, 0.1, r=0.04, col="c9d2da", metal=0.6, rough=0.3))
    body.append(UB("techo", 0, 1.38, 0.82, 1.4, 0.06, 0.9, r=0.05, col="fbf6ee"))
    wheels = []
    for name, x, z in (("wheelFL", -0.72, 0.85), ("wheelFR", 0.72, 0.85), ("wheelBL", -0.72, -0.85), ("wheelBR", 0.72, -0.85)):
        w = [cyl("goma", U(x, 0.3, z), 0.3, 0.22, "2b2a30", seg=24, rot=(0, 90, 0), bev=0.04)]
        w.append(cyl("llanta", U(x + (0.06 if x > 0 else -0.06), 0.3, z), 0.17, 0.12, "c9d2da", seg=16, rot=(0, 90, 0), metal=0.5, rough=0.3))
        wheels.append((name, w, U(x, 0.3, z)))
    ramp = [UB("rampa", 0, 0.55, -1.42, 1.3, 0.06, 0.1, r=0.02, col="9aa1aa", metal=0.4, rough=0.5)]
    groups = [("body", body, (0, 0, 0)), ("ramp", ramp, U(0, 0.55, -1.38))] + wheels
    return groups


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = argv[argv.index("--only") + 1].split(",") if "--only" in argv else ["horse", "cart", "van"]
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if prev:
        os.makedirs(prev, exist_ok=True)
    for n in only:
        lib.reset()
        groups = {"horse": horse, "cart": cart, "van": van}[n]()
        parts, t = lib.finish_parts(groups, "veh_" + n, OUT, ao=0.45, ao_dist=0.2, budget=9000)
        print("VEH", n, t)
        if prev:
            lib.preview(parts, os.path.join(prev, "veh_" + n + ".png"), size=420, elev=22, azim=-40)


if __name__ == "__main__":
    main()
