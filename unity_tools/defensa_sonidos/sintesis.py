"""Fortin de Juguete: sonidos propios sintetizados (se suman a los CC0 de Kenney que ya trae el proyecto).

Uso: python3 unity_tools/defensa_sonidos/sintesis.py [carpeta_salida]
Todo es sintesis aditiva/sustractiva con numpy (sin muestras externas): campanitas de cajita de musica, "pling" de
fusion, rayo, goma que chilla, madera "toc", "ding" triste, fush de fuego, clank de hojalata, pop con cascabel.
"""
import numpy as np, os, sys, wave

SR = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "unity_defensa", "Assets", "Resources", "Audio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def t(d): return np.arange(int(SR * d)) / SR


def env(n, a=0.005, d=0.3, curve=4.0):
    x = np.arange(n) / SR
    e = np.minimum(1.0, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) * curve / max(d, 1e-3))
    return e


def bell(f, d=1.2, partials=((1, 1), (2.76, 0.45), (5.4, 0.22), (8.93, 0.1)), decay=3.0):
    x = t(d); y = np.zeros_like(x)
    for k, a in partials:
        y += a * np.sin(2 * np.pi * f * k * x) * np.exp(-x * decay * (0.6 + 0.4 * k))
    return y * env(len(x), 0.002, d, 0.5)


def noise(d): return rng.standard_normal(int(SR * d))


def lowpass(y, k=0.2):
    o = np.zeros_like(y); acc = 0.0
    for i, v in enumerate(y):
        acc += k * (v - acc); o[i] = acc
    return o


def save(name, y, gain_db=-3.0):
    y = np.asarray(y, dtype=np.float64)
    y = y / (np.abs(y).max() + 1e-9) * 10 ** (gain_db / 20)
    fade = int(SR * 0.01)
    y[-fade:] *= np.linspace(1, 0, fade)
    data = (np.clip(y, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    print("SONIDO", name, round(len(y) / SR, 2), "s")


def mix(*parts):
    n = max(len(p) for p in parts); o = np.zeros(n)
    for p in parts: o[:len(p)] += p
    return o


def delay(y, sec): return np.concatenate([np.zeros(int(SR * sec)), y])


# ¡PLING! de fusion: dos campanitas en quinta + brillo; el juego le sube el tono con el nivel
save("pling", mix(bell(1046.5, 1.3), 0.6 * delay(bell(1568, 1.1), 0.045), 0.25 * delay(bell(2093, 0.8), 0.09)), -2)
# pop con cascabel (invocar)
x = t(0.18)
pop = np.sin(2 * np.pi * (380 + 900 * np.exp(-x * 40)) * x) * env(len(x), 0.001, 0.08, 6)
jing = mix(*[0.25 * delay(bell(3200 + 400 * k, 0.35, decay=6), 0.03 + 0.04 * k) for k in range(3)])
save("pop_cascabel", mix(pop, jing), -3)
# madera "toc" (levantar un juguete)
for i, f in enumerate((780, 900)):
    x = t(0.12)
    y = (np.sin(2 * np.pi * f * x) + 0.4 * np.sin(2 * np.pi * f * 2.4 * x)) * env(len(x), 0.0008, 0.04, 8)
    y += lowpass(noise(0.12), 0.3) * env(len(x), 0.0005, 0.01, 10) * 0.6
    save("toc_%d" % i, y, -4)
# rayo "bzzt"
for i in range(2):
    x = t(0.28)
    saw = ((x * (95 + 20 * i)) % 1.0) * 2 - 1
    buzz = saw * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 31 * x))) + 0.5 * noise(0.28)
    save("zap_%d" % i, buzz * env(len(x), 0.003, 0.18, 3), -8)
# goma que chilla (dino)
for i in range(2):
    x = t(0.32)
    f = 900 + 500 * np.sin(np.pi * x / 0.32) + 60 * np.sin(2 * np.pi * 30 * x)
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = (np.sin(ph) + 0.3 * np.sin(2 * ph)) * env(len(x), 0.01, 0.25, 2)
    save("squeak_%d" % i, y * (1 + 0.1 * i), -6)
# "ding" triste (el velador pierde vida): campana que baja
x = t(0.9)
f = 880 * np.exp(-x * 0.6)
ph = 2 * np.pi * np.cumsum(f) / SR
save("ding_triste", (np.sin(ph) + 0.3 * np.sin(2.76 * ph)) * env(len(x), 0.002, 0.8, 2.5), -4)
# fush (bola de fuego)
x = t(0.45)
y = lowpass(noise(0.45), 0.08) * np.sin(np.pi * np.minimum(1, x / 0.45)) ** 0.6
save("fush", y, -6)
# clank de hojalata
for i in range(2):
    x = t(0.3)
    y = sum(a * np.sin(2 * np.pi * f * x) * np.exp(-x * dcy) for f, a, dcy in ((1320 + 60 * i, 1, 14), (2210, 0.6, 18), (3170, 0.4, 22), (4460, 0.25, 26)))
    y += noise(0.3) * env(len(x), 0.0005, 0.01, 12) * 0.5
    save("clank_%d" % i, y, -6)
# fiu (flecha)
x = t(0.22)
f = 2400 * np.exp(-x * 6) + 500
ph = 2 * np.pi * np.cumsum(f) / SR
save("fiu", (np.sin(ph) * 0.3 + lowpass(noise(0.22), 0.5) * 0.7) * env(len(x), 0.01, 0.15, 3), -10)
# coro + campana de nivel 7
choir = sum(np.sin(2 * np.pi * fr * t(1.6) + 3 * np.sin(2 * np.pi * 5 * t(1.6))) * 0.3 for fr in (523, 659, 784, 1046))
choir *= env(len(choir), 0.2, 1.4, 1.5)
save("coro", mix(choir, bell(1046.5, 1.6, decay=1.6) * 0.8), -3)
# fanfarria corta de juguete (victoria)
notes = [(523, 0.0), (659, 0.12), (784, 0.24), (1046, 0.4)]
fan = mix(*[delay(bell(f, 0.9, partials=((1, 1), (2, 0.5), (3, 0.3), (4, 0.15)), decay=2.2), d) for f, d in notes])
save("fanfarria", fan, -2)
# cajita de musica que se queda sin cuerda (derrota)
seq = [784, 659, 587, 523, 494, 440]
box = mix(*[delay(bell(f * (1 - 0.02 * i), 1.0), 0.18 * i + 0.03 * i * i) for i, f in enumerate(seq)])
save("cuerda_fin", box, -3)
# "uuuh" tierno del jefe
x = t(1.0)
f = 180 + 60 * np.sin(np.pi * x)
ph = 2 * np.pi * np.cumsum(f) / SR
save("uuuh", (np.sin(ph) + 0.5 * np.sin(2 * ph) + 0.2 * np.sin(3 * ph)) * env(len(x), 0.08, 0.9, 1.2), -4)
# carta que se da vuelta
x = t(0.16)
save("carta", lowpass(noise(0.16), 0.35) * np.sin(np.pi * x / 0.16) ** 2, -8)
