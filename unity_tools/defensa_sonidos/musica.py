"""Fortin de Juguete: musica sintetizada (sin muestras externas).

Uso: python3 unity_tools/defensa_sonidos/musica.py [carpeta_salida]
- music_menu: cajita de musica + xilofon + pizzicato, nocturna y tierna (Fa mayor, 84 bpm).
- music_batalla_<capitulo>: la misma melodia con percusion de juguete y la variacion del capitulo
  (cocina: cucharas, banera: burbujas, cumple: matracas, picnic: grillos y palmas, atico: cajita lenta con eco,
  sotano: gotas, navidad: cascabeles). music_batalla_rapida: las ultimas oleadas (mas tempo y mas percusion).
Cada pista es un loop perfecto (el final empalma con el principio). Salida OGG estereo (ffmpeg).
"""
import numpy as np, os, sys, subprocess, tempfile, wave

SR = 32000
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "unity_defensa", "Assets", "Resources", "Audio")
rng = np.random.default_rng(11)

A4 = 440.0
def hz(n): return A4 * 2 ** ((n - 69) / 12)
# Fa mayor: F G A Bb C D E
SCALE = [65, 67, 69, 70, 72, 74, 76, 77, 79, 81, 82, 84]

# melodia (grado de la escala, duracion en corcheas); 8 compases de 4/4 = 64 corcheas
MEL = [(4, 2), (6, 1), (7, 1), (6, 2), (4, 2), (2, 2), (4, 2), (3, 4),
       (5, 2), (7, 1), (8, 1), (7, 2), (5, 2), (4, 2), (2, 2), (1, 4),
       (4, 2), (6, 1), (7, 1), (9, 2), (7, 2), (6, 2), (7, 1), (6, 1), (4, 4),
       (2, 2), (4, 2), (3, 2), (1, 2), (2, 2), (0, 6)]
CHORDS = [[53, 57, 60], [58, 62, 65], [53, 57, 60], [48, 52, 55], [50, 53, 57], [58, 62, 65], [48, 52, 55], [53, 57, 60]]


def music_box(f, d, vel=1.0):
    t = np.arange(int(SR * d)) / SR
    y = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * f * 2.0 * t) * np.exp(-t * 6) + 0.15 * np.sin(2 * np.pi * f * 4.2 * t) * np.exp(-t * 12)
    return y * np.exp(-t * 3.2) * np.minimum(1, t / 0.002) * vel


def xylo(f, d, vel=1.0):
    t = np.arange(int(SR * d)) / SR
    y = np.sin(2 * np.pi * f * t) + 0.5 * np.sin(2 * np.pi * f * 3.93 * t) * np.exp(-t * 18)
    return y * np.exp(-t * 9) * np.minimum(1, t / 0.001) * vel


def pizz(f, d, vel=1.0):
    t = np.arange(int(SR * d)) / SR
    saw = sum(np.sin(2 * np.pi * f * k * t) / k for k in range(1, 7))
    return saw * np.exp(-t * 11) * np.minimum(1, t / 0.003) * vel * 0.5


def pad(f, d, vel=1.0):
    t = np.arange(int(SR * d)) / SR
    y = sum(np.sin(2 * np.pi * f * k * t + 0.3 * np.sin(2 * np.pi * 4.5 * t)) / k ** 1.4 for k in range(1, 5))
    env = np.minimum(1, t / 0.4) * np.minimum(1, (d - t) / 0.5)
    return y * env * vel * 0.25


def noise_hit(d, decay, lp=0.5, vel=1.0):
    n = int(SR * d)
    x = rng.standard_normal(n)
    o = np.zeros(n); acc = 0.0
    for i in range(n):
        acc += lp * (x[i] - acc); o[i] = acc
    t = np.arange(n) / SR
    return o * np.exp(-t * decay) * vel


def kick(vel=1.0):
    t = np.arange(int(SR * 0.25)) / SR
    f = 90 * np.exp(-t * 18) + 45
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 14) * vel


def woodblock(f=1400, vel=1.0):
    t = np.arange(int(SR * 0.12)) / SR
    return (np.sin(2 * np.pi * f * t) + 0.4 * np.sin(2 * np.pi * f * 2.6 * t)) * np.exp(-t * 40) * vel


def bell(f, vel=1.0):
    t = np.arange(int(SR * 0.6)) / SR
    return sum(a * np.sin(2 * np.pi * f * k * t) * np.exp(-t * (4 + 3 * k)) for k, a in ((1, 1), (2.76, 0.5), (5.4, 0.25))) * vel


def bubble(vel=1.0):
    t = np.arange(int(SR * 0.09)) / SR
    f = 500 + 2500 * t / 0.09
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 30) * vel


def place(buf, y, start, pan=0.0):
    i = int(start * SR)
    if i >= buf.shape[0]: return
    n = min(len(y), buf.shape[0] - i)
    l = np.sqrt(0.5 * (1 - pan)); r = np.sqrt(0.5 * (1 + pan))
    buf[i:i + n, 0] += y[:n] * l; buf[i:i + n, 1] += y[:n] * r


def track(bpm, perc, variant, bars_rep=2, fast=False):
    e = 60.0 / bpm / 2                       # corchea
    loop = 64 * e
    total = loop * bars_rep
    buf = np.zeros((int(total * SR) + SR, 2))
    for rep in range(bars_rep):
        t0 = rep * loop
        # melodia: cajita de musica (+ xilofon en la segunda vuelta)
        t = t0
        for deg, d in MEL:
            f = hz(SCALE[deg] + (12 if variant == "navidad" and rep else 0))
            place(buf, music_box(f, min(1.6, d * e + 0.8), 0.55), t, -0.15)
            if rep % 2 == 1 or perc: place(buf, xylo(f * (2 if fast else 1), 0.5, 0.25), t + 0.005, 0.25)
            t += d * e
        # acordes en pizzicato (bajo y arpegio) + colchon suave
        for bar in range(8):
            ch = CHORDS[bar]; tb = t0 + bar * 8 * e
            place(buf, pizz(hz(ch[0] - 12), 0.5, 0.7), tb, 0.0)
            place(buf, pizz(hz(ch[0] - 12), 0.4, 0.45), tb + 4 * e, 0.0)
            for k, n in enumerate(ch * 2):
                place(buf, pizz(hz(n + (12 if k >= 3 else 0)), 0.35, 0.22), tb + (2 + k) * e, (-0.4 + 0.16 * k))
            if not perc or variant == "atico":
                for n in ch: place(buf, pad(hz(n), 8 * e, 0.35), tb, 0.0)
            # percusion de juguete
            if perc:
                for b in range(8):
                    tt = tb + b * e
                    if b in (0, 4): place(buf, kick(0.6), tt)
                    if b in (2, 6): place(buf, noise_hit(0.12, 30, 0.6, 0.35), tt, 0.2)
                    place(buf, woodblock(1500 if b % 2 else 1100, 0.18 if not fast else 0.26), tt, -0.3)
                    if fast and b % 2: place(buf, noise_hit(0.05, 60, 0.9, 0.12), tt + e / 2, 0.4)
                    if variant == "cocina" and b % 2 == 1: place(buf, bell(2600, 0.12), tt, 0.5)         # cucharas
                    if variant == "banera" and b in (1, 5, 7): place(buf, bubble(0.35), tt + e * 0.3, rng.uniform(-0.6, 0.6))
                    if variant == "cumple" and b in (3, 7):
                        for k in range(4): place(buf, noise_hit(0.03, 80, 0.95, 0.25), tt + k * e / 4, 0.3)   # matraca
                    if variant == "picnic" and b % 2 == 0: place(buf, xylo(hz(96), 0.05, 0.05), tt + e * 0.5, 0.6)  # grillos
                    if variant == "sotano" and b == 5: place(buf, bubble(0.25), tt, -0.5)
                    if variant == "navidad": place(buf, noise_hit(0.08, 25, 0.95, 0.08), tt, 0.6)              # cascabeles
    # empalme del loop: lo que sobra al final se suma al principio
    tail = buf[int(total * SR):]
    out = buf[:int(total * SR)].copy()
    out[:len(tail)] += tail
    # eco suave nocturno
    d = int(0.28 * SR)
    eco = np.zeros_like(out); eco[d:] = out[:-d] * 0.18
    out += eco
    out /= np.abs(out).max() + 1e-9
    return out * 0.85


def save_ogg(name, y):
    os.makedirs(OUT, exist_ok=True)
    tmp = tempfile.mktemp(suffix=".wav")
    data = (np.clip(y, -1, 1) * 32767).astype(np.int16)
    with wave.open(tmp, "w") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    out = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-c:a", "libvorbis", "-q:a", "3", out], check=True)
    os.remove(tmp)
    print("MUSICA", name, round(len(y) / SR, 1), "s", os.path.getsize(out) // 1024, "KB")


if __name__ == "__main__":
    save_ogg("music_menu", track(84, False, "menu", 2))
    for cap in ("escritorio", "cocina", "banera", "picnic", "cumple", "atico", "sotano", "navidad"):
        save_ogg("music_batalla_" + cap, track(104 if cap != "atico" else 92, True, cap, 2))
    save_ogg("music_batalla_rapida", track(128, True, "rapida", 1, fast=True))
