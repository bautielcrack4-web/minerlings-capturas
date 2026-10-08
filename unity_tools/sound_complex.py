"""Sonidos del sistema de construccion del Complejo (0.11): capas de impactos reales CC0 (Kenney Impact y Interface
Sounds) + notas de marimba sintetizadas en una escala pentatonica afinada (cada union, cada maquina de la cadena y cada
CLACK sube una nota: el oido pide la siguiente).

Uso: python3 sound_complex.py <carpeta_kenney_impact/Audio> <carpeta_kenney_interface/Audio> <salida Resources/Audio>
"""
import os, subprocess, sys
import numpy as np

SR = 44100
IMP, UI, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def load(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).copy()


def imp(name, i=0): return load(os.path.join(IMP, "%s_%03d.ogg" % (name, i)))
def ui(name, i=0): return load(os.path.join(UI, "%s_%03d.ogg" % (name, i + 1)))   # Interface Sounds numera desde 001


def pitch(a, ratio):
    """Cambia el tono por remuestreo (mas agudo = mas corto)."""
    if abs(ratio - 1) < 1e-4: return a
    n = int(len(a) / ratio)
    x = np.linspace(0, len(a) - 1, n)
    return np.interp(x, np.arange(len(a)), a).astype(np.float32)


def env(n, a=0.003, d=0.3):
    t = np.arange(n) / SR
    e = np.exp(-t / max(d, 1e-4))
    att = int(a * SR)
    if att > 0: e[:att] *= np.linspace(0, 1, att)
    return e


def mix(*parts):
    """parts: (señal, inicio_s, ganancia)."""
    end = max(int(s * SR) + len(a) for a, s, g in parts)
    out = np.zeros(end, np.float32)
    for a, s, g in parts:
        i = int(s * SR)
        out[i:i + len(a)] += a * g
    return out


def lowpass(a, cut):
    # un polo, aplicado dos veces (suave y sin anillos)
    k = np.exp(-2 * np.pi * cut / SR)
    for _ in range(2):
        y = np.empty_like(a); acc = 0.0
        for i in range(len(a)):
            acc = (1 - k) * a[i] + k * acc
            y[i] = acc
        a = y
    return a


def highpass(a, cut): return a - lowpass(a, cut)


def reverb(a, mix_=0.14, size=1.0):
    """Sala chica (Schroeder: 4 peines + 2 pasatodo): pega las capas y da cuerpo sin enturbiar."""
    combs = [int(SR * d * size) for d in (0.0297, 0.0371, 0.0411, 0.0437)]
    tail = np.zeros(len(a) + int(SR * 0.6), np.float32)
    x = np.concatenate([a, np.zeros(int(SR * 0.6), np.float32)])
    for D in combs:
        y = np.zeros_like(x)
        g = 0.72
        for i in range(len(x)):
            y[i] = x[i] + (g * y[i - D] if i >= D else 0.0)
        tail += y * 0.25
    for D, g in ((int(SR * 0.005), 0.7), (int(SR * 0.0017), 0.7)):
        y = np.zeros_like(tail)
        for i in range(len(tail)):
            xd = tail[i - D] if i >= D else 0.0
            yd = y[i - D] if i >= D else 0.0
            y[i] = -g * tail[i] + xd + g * yd
        tail = y
    out = x * (1 - mix_) + tail * mix_
    # cortar el silencio final
    thr = np.abs(out).max() * 0.002
    idx = np.where(np.abs(out) > thr)[0]
    return out[: idx[-1] + int(SR * 0.02)] if len(idx) else out


def thump(f0=110, f1=45, dur=0.22, gain=1.0):
    """Golpe grave: seno que baja de tono (el "peso" del modulo al caer)."""
    n = int(dur * SR); t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.05)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return (np.sin(ph) * env(n, 0.002, dur * 0.35) * gain).astype(np.float32)


def noise(dur, cut_lo, cut_hi, d):
    n = int(dur * SR)
    a = rng.standard_normal(n).astype(np.float32)
    a = lowpass(a, cut_hi); a = highpass(a, cut_lo)
    return a * env(n, 0.002, d)


def whoosh(dur=0.35, up=True):
    n = int(dur * SR); t = np.arange(n) / SR
    a = rng.standard_normal(n).astype(np.float32)
    # barrido de filtro: de grave a agudo (subir) o al reves
    out = np.zeros(n, np.float32); acc = 0.0
    for i in range(n):
        u = t[i] / dur
        cut = (300 + 4200 * u) if up else (4500 - 4200 * u)
        k = np.exp(-2 * np.pi * cut / SR)
        acc = (1 - k) * a[i] + k * acc
        out[i] = acc
    shape = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.5
    return out * shape


def marimba(freq, dur=0.9, bright=1.0):
    """Nota de marimba: parciales 1, 3.99 y 9.9 con caidas distintas + golpe de maza."""
    n = int(dur * SR); t = np.arange(n) / SR
    a = (np.sin(2 * np.pi * freq * t) * np.exp(-t / 0.42)
         + 0.33 * bright * np.sin(2 * np.pi * freq * 3.99 * t) * np.exp(-t / 0.09)
         + 0.08 * bright * np.sin(2 * np.pi * freq * 9.9 * t) * np.exp(-t / 0.03))
    att = int(0.002 * SR); a[:att] *= np.linspace(0, 1, att)
    click = noise(0.012, 1500, 7000, 0.003) * 0.25
    a[: len(click)] += click
    return a.astype(np.float32)


def bell(freq, dur=1.4):
    n = int(dur * SR); t = np.arange(n) / SR
    parts = [(1, 1, 0.9), (2.76, 0.45, 0.45), (5.4, 0.25, 0.22), (8.93, 0.12, 0.1)]
    a = sum(g * np.sin(2 * np.pi * freq * r * t) * np.exp(-t / d) for r, g, d in parts)
    att = int(0.001 * SR); a[:att] *= np.linspace(0, 1, att)
    return a.astype(np.float32)


def sparkle(dur=0.6, base=2400):
    """Brillito: granos agudos que suben y se apagan (el "premio")."""
    out = np.zeros(int(dur * SR), np.float32)
    for k in range(9):
        f = base * (2 ** (k / 7.0)) * (1 + 0.01 * rng.standard_normal())
        g = marimba(f, 0.25, 0.4) * (0.5 - 0.035 * k)
        i = int(k * 0.045 * SR)
        out[i:i + len(g)] += g[: len(out) - i]
    return out


def norm(a, peak_db=-1.0, rms_db=None):
    a = a - a.mean()
    if rms_db is not None:
        r = np.sqrt((a ** 2).mean()) + 1e-9
        a = a * (10 ** (rms_db / 20) / r)
    pk = np.abs(a).max() + 1e-9
    lim = 10 ** (peak_db / 20)
    a = a * (lim / pk)                     # pico exacto: todos los sonidos del banco quedan parejos
    fade = min(int(0.008 * SR), len(a) // 4)
    if fade > 0: a[-fade:] *= np.linspace(1, 0, fade)
    return a.astype(np.float32)


def save(name, a, peak_db=-1.0, rms_db=None):
    a = norm(a, peak_db, rms_db)
    wav = os.path.join(OUT, name + ".wav")
    import wave
    with wave.open(wav, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(a, -1, 1) * 32767).astype(np.int16).tobytes())
    ogg = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "6", ogg], check=True)
    os.remove(wav)
    print(name, "%.2fs" % (len(a) / SR))


# escala pentatonica mayor (Do) en dos octavas: cada paso suena "bien" con el anterior y con el siguiente
SCALE = [523.25, 587.33, 659.25, 783.99, 880.00, 1046.50, 1174.66, 1318.51]

# ---------------------------------------------------------------- notas de la cascada y de las uniones
for i, f in enumerate(SCALE):
    save("cx_n%d" % i, reverb(marimba(f), 0.12), -3.0)

# ---------------------------------------------------------------- agarrar (levantar el modulo)
for v in range(2):
    a = mix((whoosh(0.28, True), 0, 0.5), (pitch(imp("impactSoft_medium", v), 1.25), 0, 0.6), (pitch(ui("select", v), 1.0), 0.02, 0.35))
    save("cx_grab_%d" % v, reverb(a, 0.08), -4.0)

# ---------------------------------------------------------------- pasar de celda (tic suave)
for v in range(3):
    a = pitch(ui("click", v), 1.15)
    save("cx_tick_%d" % v, a, -9.0)

# ---------------------------------------------------------------- CLACK del iman (madera + pestillo metalico + golpe grave)
for v in range(3):
    a = mix((imp("impactWood_light", v), 0, 0.9),
            (pitch(imp("impactMetal_light", v), 1.35), 0.004, 0.55),
            (thump(150, 70, 0.12, 0.7), 0, 1.0),
            (pitch(ui("switch", v), 1.1), 0.01, 0.35))
    save("cx_snap_%d" % v, reverb(a, 0.1), -1.5)

# ---------------------------------------------------------------- girar (trinquete)
for v in range(3):
    a = mix((pitch(ui("switch", v + 2), 0.95), 0, 0.8), (pitch(imp("impactWood_light", v + 1), 1.6), 0.03, 0.45))
    save("cx_rot_%d" % v, a, -4.0)

# ---------------------------------------------------------------- no entra (suave, sin castigar)
for v in range(2):
    a = mix((pitch(imp("impactSoft_heavy", v), 0.85), 0, 0.9), (thump(90, 60, 0.12, 0.4), 0, 1))
    save("cx_deny_%d" % v, a, -6.0)

# ---------------------------------------------------------------- caida del modulo (pesada, satisfactoria)
for v in range(3):
    a = mix((imp("impactWood_heavy", v), 0, 1.0),
            (pitch(imp("impactPlate_heavy", v), 0.8), 0.006, 0.45),
            (thump(120, 42, 0.38, 1.1), 0, 1.0),
            (noise(0.5, 200, 1800, 0.18), 0.03, 0.18))   # polvo
    save("cx_drop_%d" % v, reverb(a, 0.16, 1.2), -0.8)

# ---------------------------------------------------------------- obra terminada: arpegio + brillito
arp = mix((marimba(SCALE[0]), 0, 0.8), (marimba(SCALE[2]), 0.07, 0.8), (marimba(SCALE[3]), 0.14, 0.85), (marimba(SCALE[5]), 0.21, 1.0), (sparkle(0.7), 0.24, 0.5))
save("cx_done", reverb(arp, 0.18), -1.5)

# ---------------------------------------------------------------- evolucionar: barrido que sube + acorde
evo = mix((whoosh(0.55, True), 0, 0.6), (marimba(SCALE[1], 1.2), 0.42, 0.7), (marimba(SCALE[3], 1.2), 0.42, 0.6), (marimba(SCALE[6], 1.2), 0.42, 0.7), (sparkle(0.8, 2000), 0.45, 0.6))
save("cx_evolve", reverb(evo, 0.2, 1.3), -1.5)

# ---------------------------------------------------------------- venta al final de la linea: campana + monedas
for v in range(2):
    a = mix((bell(1046.5 if v == 0 else 1174.66), 0, 0.55), (pitch(imp("impactBell_heavy", v), 1.8), 0, 0.25),
            (marimba(SCALE[5]), 0.02, 0.5), (sparkle(0.45, 3000), 0.05, 0.35))
    save("cx_sell_%d" % v, reverb(a, 0.16), -2.0)

# ---------------------------------------------------------------- maquinas al recibir la vagoneta (grave y corto: la nota la pone la escala)
for v in range(3):
    save("cx_m_smelt_%d" % v, mix((pitch(imp("impactMetal_heavy", v), 0.9), 0, 0.8), (noise(0.35, 300, 2500, 0.12), 0.01, 0.25)), -6.0)
    save("cx_m_crush_%d" % v, mix((imp("impactPlate_medium", v), 0, 0.8), (imp("impactMining", v), 0.03, 0.6)), -6.0)
    save("cx_m_wood_%d" % v, imp("impactWood_medium", v), -7.0)

# ---------------------------------------------------------------- abrir / cerrar el Modo Cuartel
save("cx_open", reverb(mix((whoosh(0.32, True), 0, 0.5), (ui("maximize", 1), 0.05, 0.6), (marimba(SCALE[3], 0.6), 0.12, 0.35), (marimba(SCALE[5], 0.6), 0.18, 0.3)), 0.12), -3.0)
save("cx_close", mix((whoosh(0.28, False), 0, 0.5), (ui("minimize", 1), 0.03, 0.6)), -4.0)

# ---------------------------------------------------------------- tocar una tarjeta de la bandeja
for v in range(3):
    save("cx_card_%d" % v, mix((ui("select", v + 3), 0, 0.8), (pitch(imp("impactSoft_medium", v), 1.5), 0, 0.3)), -5.0)

# ---------------------------------------------------------------- descubrimiento (combinacion, sala secreta): acorde magico
magic = mix(*[(bell(f * 0.5, 2.0), k * 0.09, 0.45) for k, f in enumerate([SCALE[0], SCALE[2], SCALE[3], SCALE[5], SCALE[7]])] + [(sparkle(1.0, 2200), 0.3, 0.6)])
save("cx_magic", reverb(magic, 0.28, 1.5), -1.5)

# ---------------------------------------------------------------- la energia corre por el cable
zap = mix((whoosh(0.25, True), 0, 0.35), (marimba(SCALE[4] * 2, 0.4, 0.3), 0.15, 0.4), (noise(0.2, 2500, 8000, 0.05), 0.0, 0.2))
save("cx_power", reverb(zap, 0.1), -4.0)
