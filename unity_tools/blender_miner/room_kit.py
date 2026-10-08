"""Kit de habitaciones listo (trellis/listo) -> Resources/RoomKit del juego.

- habitaciones/<tema>_<pieza>.glb (pieza = pared, ventana, techo): una malla y textura cada una.
- puertas/<tema>.glb (PartCrafter, en pedazos): se separa en <tema>_marco y <tema>_hoja.
El juego estira cada pieza para calzar en su lugar del modulo (IslandView.RoomKit): aca solo se normaliza.
La pieza "puerta" entera (sin separar) no se usa: el juego necesita la hoja aparte para abrirla.

Uso: python3 room_kit.py [--listo trellis/listo] [--out ../../unity_miner/Assets/Resources/RoomKit] [--tex 512]
"""
import argparse, os, subprocess, sys, tempfile

here = os.path.dirname(os.path.abspath(__file__))
ap = argparse.ArgumentParser()
ap.add_argument("--listo", default=os.path.join(here, "trellis", "listo"))
ap.add_argument("--out", default=os.path.join(here, "..", "..", "unity_miner", "Assets", "Resources", "RoomKit"))
ap.add_argument("--tex", type=int, default=512)
ap.add_argument("--blender", default="blender")
a = ap.parse_args()
PIECES = ("pared", "ventana", "techo")


def convert(glb, name):
    r = subprocess.run([a.blender, "-b", "-P", os.path.join(here, "tripo_building.py"), "--", glb, "--name", name,
                        "--size", "1", "--sink", "0", "--tex", str(a.tex), "--outdir", os.path.abspath(a.out)],
                       capture_output=True, text=True)
    ok = "EXPORT" in r.stdout
    print(("ok   " if ok else "FALLO"), name, "" if ok else (r.stdout + r.stderr)[-400:])
    return ok


done = fail = 0
hab = os.path.join(a.listo, "habitaciones")
for f in sorted(os.listdir(hab)) if os.path.isdir(hab) else []:
    base, ext = os.path.splitext(f)
    if ext.lower() != ".glb" or base.rsplit("_", 1)[-1] not in PIECES: continue
    if convert(os.path.join(hab, f), base): done += 1
    else: fail += 1
pts = os.path.join(a.listo, "puertas")
for f in sorted(os.listdir(pts)) if os.path.isdir(pts) else []:
    base, ext = os.path.splitext(f)
    if ext.lower() != ".glb": continue
    with tempfile.TemporaryDirectory() as tmp:
        r = subprocess.run([a.blender, "-b", "-P", os.path.join(here, "door_split.py"), "--", os.path.join(pts, f), tmp], capture_output=True, text=True)
        if "ERROR" in r.stdout or r.returncode != 0:
            print("FALLO", base, (r.stdout + r.stderr)[-300:]); fail += 1; continue
        for part in ("marco", "hoja"):
            if convert(os.path.join(tmp, "%s_%s.glb" % (base, part)), "%s_%s" % (base, part)): done += 1
            else: fail += 1
print("listo:", done, "piezas;", fail, "fallaron ->", os.path.abspath(a.out))
sys.exit(1 if fail else 0)
