"""Instrumentos sintetizados. Firma comun: inst(midi, dur_segundos, vel 0..1, **kw) -> np.ndarray mono.
La ganancia por velocidad la aplica el mezclador; aca la velocidad solo cambia el timbre (brillo)."""
from functools import lru_cache
import numpy as np
from scipy import signal
from engine import SR, hz, lp, hp, bp, env_adsr

TAU = 2 * np.pi


def _r(x, q=0.02): return round(float(x) / q) * q
def _t(n): return np.arange(n) / SR


def _ramp(x, a=0.0015):
    k = max(1, int(a * SR)); x[:k] *= np.linspace(0, 1, k); return x


def _release(x, dur, rel):
    """Apaga la nota desde dur con caida exponencial (apagador / dedo que silencia)."""
    n = int(dur * SR)
    if n < len(x):
        x[n:] *= np.exp(-np.arange(len(x) - n) / (rel * SR))
    k = min(len(x), int(0.006 * SR)); x[-k:] *= np.linspace(1, 0, k)
    return x


# ================================================================== cuerda pulsada (Karplus-Strong)
@lru_cache(maxsize=4096)
def _ks(m, ring, t60, bright, pos, var):
    f = hz(m)
    S = float(np.clip(0.5 * (300 / f) ** 0.5, 0.15, 0.5))          # menos perdida en agudos (no suenan a seno)
    P = max(2, int(SR / f - S)); f0 = SR / (P + S); r = f / f0
    nout = int(ring * SR); N = int(nout * r) + P + 4
    rng = np.random.default_rng(1000 * m + var)
    # excitacion con espectro controlado (ruido puro deja armonicos al azar, a veces sin fundamental)
    k = np.arange(1, P // 2 + 1)
    fc = f * (1.5 + 14 * bright)
    mag = k ** -0.7 / (1 + (k * f / fc) ** 2)                      # 12 dB/oct sobre el corte: punteo redondo
    mag *= 0.25 + np.abs(np.sin(np.pi * k * pos))                  # posicion del punteo
    mag *= 10 ** (rng.normal(0, 2.0, len(k)) / 20)                 # algo de azar entre punteos
    X = np.zeros(P // 2 + 1, complex); X[1:] = mag * np.exp(2j * np.pi * rng.random(len(k)))
    exc = np.fft.irfft(X, P)
    exc /= np.abs(exc).max() + 1e-9
    y = np.zeros(N + 1); y[1:P + 1] = exc
    rho = 10 ** (-3 / (t60 * f0))
    i = P + 1
    while i <= N:
        j = min(i + P, N + 1)
        y[i:j] = rho * ((1 - S) * y[i - P:j - P] + S * y[i - P - 1:j - P - 1])
        i = j
    out = np.interp(1 + np.arange(nout) * r, np.arange(N + 1), y)       # corrige la afinacion fraccional
    out = signal.lfilter([1, -1], [1, -0.997], out)                       # sin DC
    return out


def pluck(m, dur, vel, t60=2.5, bright=0.5, pos=0.2, var=0, release=0.08, maxring=6.0):
    ring = _r(min(dur + 6 * release, maxring, t60 * 1.2 + 0.1), 0.05)
    br = _r(min(0.95, bright * (0.7 + 0.5 * vel)), 0.05)
    x = _ks(m, ring, float(t60), br, pos, int(var) % 4).copy()
    return _ramp(_release(x, dur, release), 0.001)


def guitar(m, dur, vel, var=0):
    f = hz(m)
    return 0.8 * pluck(m, dur, vel, t60=_r(3.2 * (196 / f) ** 0.35, 0.1), bright=0.5, pos=0.17, var=var, release=0.07)


def nylon(m, dur, vel, var=0):
    f = hz(m)
    return 0.8 * pluck(m, dur, vel, t60=_r(2.6 * (196 / f) ** 0.35, 0.1), bright=0.32, pos=0.2, var=var, release=0.1)


def uke(m, dur, vel, var=0):
    f = hz(m)
    return 0.8 * pluck(m, dur, vel, t60=_r(1.5 * (262 / f) ** 0.3, 0.1), bright=0.42, pos=0.22, var=var, release=0.05)


def harp(m, dur, vel, var=0):
    f = hz(m); t60 = _r(min(5.0, 3.8 * (220 / f) ** 0.45), 0.1)
    x = 0.7 * pluck(m, dur, vel, t60=t60, bright=0.38, pos=0.42, var=var, release=0.5, maxring=5.0)
    t = _t(len(x))
    x += 0.35 * np.sin(TAU * f * t) * np.exp(-6.91 * t / t60) * (1 - np.exp(-t / 0.004))
    return x


def ubass(m, dur, vel, var=0):
    """Contrabajo pizzicato: KS oscuro + golpe de fundamental (cuerpo)."""
    f = hz(m)
    x = pluck(m, dur, vel, t60=1.8, bright=0.28, pos=0.12, var=var, release=0.06, maxring=3.0)
    t = _t(len(x))
    x = 0.8 * x + 0.6 * np.sin(TAU * f * t) * np.exp(-t / 0.35) * (1 - np.exp(-t / 0.006))
    return _release(lp(x, 1400), dur, 0.06)


# ================================================================== piano aditivo
@lru_cache(maxsize=2048)
def _piano(m, length, vb):
    f = hz(m); n = int(length * SR); t = _t(n)
    rng = np.random.default_rng(m)
    B = 0.00025 * 2 ** ((m - 60) / 18)                                     # inarmonicidad
    base = float(np.clip(6.5 * (261.6 / f) ** 0.7, 0.7, 12))
    x = np.zeros(n)
    for k in range(1, 26):
        fk = k * f * np.sqrt(1 + B * k * k)
        if fk > 8000: break
        a = k ** -1.1 * np.exp(-(k - 1) * (0.32 - 0.2 * vb)) * (abs(np.sin(np.pi * k / 7.7)) + 0.12)
        T = base / (1 + 0.45 * (k - 1))
        e = 0.6 * np.exp(-t / (T * 0.3)) + 0.4 * np.exp(-t / T)
        dt = fk * (0.0003 + 0.0003 * rng.random())                       # dos cuerdas apenas desafinadas
        ph = rng.random() * 0.5                                            # el martillo las golpea en fase
        x += a * e * (np.sin(TAU * fk * t + ph) + 0.8 * np.sin(TAU * (fk + dt) * t + ph))
    h = rng.standard_normal(min(n, int(0.03 * SR))) * np.exp(-_t(min(n, int(0.03 * SR))) / 0.005)
    x[:len(h)] += 0.25 * lp(h, 900 + 2200 * vb)                             # martillo
    x *= 1 - np.exp(-t / 0.0015)
    return x / 1.8


def piano(m, dur, vel):
    f = hz(m)
    natural = float(np.clip(6.5 * (261.6 / f) ** 0.7, 1.0, 5.0))
    length = _r(min(dur + 0.45, natural), 0.05)
    x = _piano(m, max(length, 0.1), round(vel * 3) / 3).copy()
    return _release(x, dur, 0.07 if m > 50 else 0.15)


# ================================================================== osciladores aditivos con vibrato
def _osc(f, n, amps, cents=(0,), vib=0.0, rate=5.5, vdelay=0.3, rng=None, ratios=None, trem=0.0, trate=6.0):
    t = _t(n); x = np.zeros(n)
    rng = rng or np.random.default_rng(0)
    ratios = np.arange(1, len(amps) + 1) if ratios is None else np.asarray(ratios)
    for c in cents:
        ph0 = rng.random() * TAU
        depth = vib * (1 - np.exp(-t / vdelay)) if vib else 0.0
        fi = f * 2 ** ((c + depth * np.sin(TAU * (rate + 0.4 * rng.random()) * t + ph0)) / 1200)
        if trem:
            fi = fi * (1 + 0.0012 * np.sin(TAU * trate * t))
        phase = TAU * np.cumsum(fi) / SR
        for r, a in zip(ratios, amps):
            if a == 0 or r * f > 12000: continue
            x += a * np.sin(r * phase + rng.random() * TAU)
    if trem:
        x *= 1 + trem * np.sin(TAU * trate * t)
    return x / len(cents)


def _saw_amps(f, fc, K=40, slope=4):
    k = np.arange(1, K + 1)
    return list(1 / k / np.sqrt(1 + (k * f / fc) ** slope))


@lru_cache(maxsize=1024)
def _bowed(m, length, rel_at, att, vib, fc, voices, var, rel):
    f = hz(m); n = int(length * SR)
    rng = np.random.default_rng(m * 7 + var)
    amps = _saw_amps(f, fc, K=min(30, int(8000 / f)))
    k = np.arange(1, len(amps) + 1)
    amps = list(np.array(amps) * (1 + 0.8 * np.exp(-((k * f - 480) / 260) ** 2) + 0.4 * np.exp(-((k * f - 2600) / 700) ** 2)))
    cents = (0,) if voices == 1 else tuple(np.linspace(-7, 7, voices) + rng.normal(0, 1.5, voices))
    x = _osc(f, n, amps, cents, vib=vib, rate=5.4, vdelay=0.35, rng=rng)
    nz = hp(rng.standard_normal(n), 2500) * 0.004
    x = (x + nz) * env_adsr(n, att, 0.4, 0.82, int(rel_at * SR), rel)
    return x / 6.0


def strings(m, dur, vel, att=0.25, vib=11.0, voices=1, var=0, rel=0.35, fc=2600):
    """Cuerda frotada (violin/viola/cello). voices>1 = seccion (ensamble desafinado)."""
    fcv = _r(fc * (0.75 + 0.4 * vel), 100)
    length = _r(dur + rel * 6, 0.05)
    return _bowed(m, length, _r(dur, 0.05), _r(att, 0.01), vib, fcv, voices, var % 3, rel)


@lru_cache(maxsize=1024)
def _accordion(m, length, rel_at, wet):
    f = hz(m); n = int(length * SR); rng = np.random.default_rng(m + 99)
    d = 0.32; k = np.arange(1, min(24, int(7000 / f)) + 1)
    amps = np.abs(np.sin(np.pi * k * d)) / k / np.sqrt(1 + (k * f / 2600) ** 4)
    cents = (-wet / 2, wet / 2) if wet else (0,)
    x = _osc(f, n, list(amps), cents, rng=rng, trem=0.04, trate=4.2)
    x = x * env_adsr(n, 0.025, 0.15, 0.9, int(rel_at * SR), 0.05)
    return x / 3.0


def accordion(m, dur, vel, wet=12.0):
    """Lenguetas en musette (dos voces desafinadas 'wet' cents) + temblor de fuelle."""
    return _accordion(m, _r(dur + 0.3, 0.05), _r(dur, 0.02), float(wet))


@lru_cache(maxsize=1024)
def _organ(m, length, rel_at, reg):
    f = hz(m); n = int(length * SR); rng = np.random.default_rng(m + 5)
    if reg == 'full':
        ratios, amps = [0.5, 1, 1.5, 2, 3, 4], [0.55, 1, 0.35, 0.55, 0.22, 0.18]
    else:   # 'flute': registro suave tipo tibia
        ratios, amps = [1, 2, 3, 4], [1, 0.35, 0.12, 0.08]
    x = _osc(f, n, amps, (0,), ratios=ratios, rng=rng, trem=0.13, trate=6.3)
    x *= env_adsr(n, 0.008, 0.1, 0.95, int(rel_at * SR), 0.05)
    click = hp(rng.standard_normal(int(0.004 * SR)), 1500) * 0.06
    x[:len(click)] += click
    return x / 5.5


def organ(m, dur, vel, reg='full'):
    return _organ(m, _r(dur + 0.25, 0.02), _r(dur, 0.02), reg)


@lru_cache(maxsize=1024)
def _flute(m, length, rel_at, vb):
    f = hz(m); n = int(length * SR); rng = np.random.default_rng(m + 31)
    amps = [1, 0.18 + 0.12 * vb, 0.07, 0.025]
    x = _osc(f, n, amps, (0,), vib=13, rate=5.0, vdelay=0.28, rng=rng)
    t = _t(n)
    nz = rng.standard_normal(n)
    breath = bp(nz, f * 0.9, min(f * 3.5, 9000)) * 0.11
    chiff = bp(nz, f * 1.5, min(f * 5, 9000)) * np.exp(-t / 0.03) * 0.25
    x = (x + breath + chiff) * env_adsr(n, 0.06, 0.2, 0.88, int(rel_at * SR), 0.08)
    return x / 3.6


def flute(m, dur, vel):
    return _flute(m, _r(dur + 0.4, 0.05), _r(dur, 0.02), round(vel * 2) / 2)


@lru_cache(maxsize=512)
def _wind(m, length, rel_at, kind):
    f = hz(m); n = int(length * SR); rng = np.random.default_rng(m + 77)
    k = np.arange(1, min(24, int(7000 / f)) + 1)
    if kind == 'clarinet':
        amps = np.where(k % 2 == 1, 1.0, 0.12) / k / np.sqrt(1 + (k * f / 2200) ** 4); att, vib = 0.05, 5
    elif kind == 'horn':
        amps = 1 / k / (1 + (k * f / 650) ** 3); att, vib = 0.09, 4
    else:   # 'tuba' (staccato, gracioso)
        amps = 1 / k * (1 + 1.5 * np.exp(-((k * f - 420) / 200) ** 2)) / (1 + (k * f / 900) ** 3); att, vib = 0.012, 0
    x = _osc(f, n, list(amps), (0, 3) if kind == 'horn' else (0,), vib=vib, rate=5.0, vdelay=0.4, rng=rng)
    x *= env_adsr(n, att, 0.25 if kind != 'tuba' else 0.12, 0.85 if kind != 'tuba' else 0.55, int(rel_at * SR), 0.07 if kind == 'tuba' else 0.12)
    return x / 3.6


def clarinet(m, dur, vel): return _wind(m, _r(dur + 0.4, 0.05), _r(dur, 0.02), 'clarinet')
def horn(m, dur, vel): return _wind(m, _r(dur + 0.7, 0.05), _r(dur, 0.02), 'horn')
def tuba(m, dur, vel): return 0.5 * _wind(m, _r(dur + 0.3, 0.05), _r(dur, 0.02), 'tuba')


# ================================================================== percusion afinada (modal)
@lru_cache(maxsize=2048)
def _modal(m, length, ratios, amps, decays, click, clickf):
    f = hz(m); n = int(length * SR); t = _t(n); rng = np.random.default_rng(m + 13)
    x = np.zeros(n)
    for r, a, d in zip(ratios, amps, decays):
        if r * f > 11000: continue
        x += a * np.sin(TAU * r * f * t + rng.random() * 0.3) * np.exp(-t / d)
    if click:
        k = int(0.02 * SR); c = rng.standard_normal(k) * np.exp(-_t(k) / 0.003)
        x[:k] += click * lp(c, clickf)
    x *= 1 - np.exp(-t / 0.0008)
    return 0.7 * x


def marimba(m, dur, vel):
    f = hz(m); d0 = float(np.clip(0.8 * (262 / f) ** 0.6, 0.18, 1.5))
    vb = round(vel * 2) / 2
    return _modal(m, _r(min(d0 * 5, 4), 0.05), (1.0, 3.93, 9.2), (1, 0.18 + 0.15 * vb, 0.04 + 0.04 * vb),
                  (d0, d0 * 0.18, d0 * 0.06), 0.06, 1500.0)


def xylo(m, dur, vel):
    f = hz(m); d0 = float(np.clip(0.35 * (523 / f) ** 0.5, 0.12, 0.6))
    return _modal(m, _r(d0 * 5, 0.05), (1.0, 3.0, 6.0), (1, 0.22, 0.06), (d0, d0 * 0.3, d0 * 0.12), 0.08, 2500.0)


def celesta(m, dur, vel):
    f = hz(m); d0 = float(np.clip(1.6 * (1047 / f) ** 0.5, 0.5, 2.6))
    return _modal(m, _r(min(d0 * 4.5, 6.0), 0.05), (1.0, 2.0, 2.76, 5.4), (1, 0.12, 0.1, 0.025),
                  (d0, d0 * 0.4, d0 * 0.22, d0 * 0.08), 0.04, 3000.0)


def glock(m, dur, vel):
    f = hz(m); d0 = float(np.clip(2.2 * (1047 / f) ** 0.5, 0.8, 3.0))
    return _modal(m, _r(min(d0 * 4, 6.0), 0.05), (1.0, 2.71, 5.15, 8.93), (1, 0.14, 0.045, 0.012),
                  (d0, d0 * 0.25, d0 * 0.1, d0 * 0.05), 0.03, 3500.0)


def timpani(m, dur, vel):
    return 0.9 * _modal(m, 2.5, (1.0, 1.5, 1.98, 2.44), (1, 0.45, 0.25, 0.12), (0.9, 0.6, 0.45, 0.3), 0.25, 400.0)


def mbass(m, dur, vel):
    """Bajo redondo tipo marimba grave (jungla)."""
    x = _modal(m, 1.4, (1.0, 2.0, 4.0), (1, 0.12, 0.06), (0.45, 0.15, 0.05), 0.05, 800.0).copy()
    return _release(x, dur + 0.05, 0.08)


# ================================================================== pads
@lru_cache(maxsize=512)
def _pad(m, length, rel_at, kind):
    f = hz(m); n = int(length * SR); rng = np.random.default_rng(m + 211)
    if kind == 'air':
        amps = _saw_amps(f, 1500, K=min(20, int(6000 / f)), slope=4)
        x = _osc(f, n, amps, (-9, -2, 5, 11), vib=3, rate=0.35, vdelay=0.5, rng=rng)
        nz = rng.standard_normal(n)
        air = bp(nz, min(f * 3, 6000), min(f * 6, 9000)) * 0.05
        lfo = 0.6 + 0.4 * np.sin(TAU * 0.15 * _t(n) + rng.random() * 6)
        x = x + air * lfo
        e = env_adsr(n, 1.4, 1.0, 0.9, int(rel_at * SR), 1.6)
    else:   # warm
        amps = _saw_amps(f, 1000, K=min(20, int(6000 / f)), slope=4)
        x = _osc(f, n, amps, (-7, 0, 7), vib=2, rate=0.25, vdelay=0.5, rng=rng)
        e = env_adsr(n, 0.9, 0.8, 0.9, int(rel_at * SR), 1.2)
    return x * e / 2.0


def pad(m, dur, vel, kind='warm'):
    return _pad(m, _r(dur + (8.0 if kind == 'air' else 6.0), 0.1), _r(dur, 0.05), kind)


def sub(m, dur, vel):
    n = int((dur + 0.5) * SR); t = _t(n); f = hz(m)
    x = np.sin(TAU * f * t) + 0.12 * np.sin(TAU * 2 * f * t)
    return x * env_adsr(n, 0.04, 0.5, 0.85, int(dur * SR), 0.25) * 0.4


# ================================================================== bateria y percusion
_rng = np.random.default_rng(4242)


def _nz(n, seed): return np.random.default_rng(seed).standard_normal(n)


@lru_cache(maxsize=64)
def kick(var=0):
    n = int(0.45 * SR); t = _t(n)
    f = 48 + 70 * np.exp(-t / 0.035)
    x = np.sin(TAU * np.cumsum(f) / SR) * np.exp(-t / 0.18)
    x += 0.15 * lp(_nz(n, var), 1200) * np.exp(-t / 0.004)
    return x * (1 - np.exp(-t / 0.001))


@lru_cache(maxsize=64)
def brush_tap(var=0):
    n = int(0.25 * SR); t = _t(n)
    x = bp(_nz(n, 10 + var), 1200, 7000) * (0.7 * np.exp(-t / 0.035) + 0.3 * np.exp(-t / 0.12))
    x += 0.25 * np.sin(TAU * 190 * t) * np.exp(-t / 0.03)
    return x * 0.6


@lru_cache(maxsize=64)
def brush_swish(dur, var=0):
    n = int(dur * SR); t = np.linspace(0, 1, n)
    x = bp(_nz(n, 20 + var), 1500, 6500) * np.sin(np.pi * t) ** 2
    return x * 0.25


@lru_cache(maxsize=64)
def shaker(var=0, length=0.09):
    n = int(length * SR); t = _t(n)
    x = bp(_nz(n, 30 + var), 3000, 8000) * (1 - np.exp(-t / 0.006)) * np.exp(-t / 0.03)
    return x * 0.8


@lru_cache(maxsize=64)
def conga(f, tone='open', var=0):
    n = int(0.5 * SR); t = _t(n)
    d = {'open': 0.22, 'slap': 0.07, 'mute': 0.05, 'bass': 0.3}[tone]
    fi = f * (1 + 0.25 * np.exp(-t / 0.012))
    x = np.sin(TAU * np.cumsum(fi) / SR) * np.exp(-t / d)
    x += 0.3 * np.sin(TAU * np.cumsum(fi * 1.58) / SR) * np.exp(-t / (d * 0.4))
    nz = _nz(n, 40 + var)
    if tone == 'slap':
        x += 0.8 * bp(nz, 900, 5000) * np.exp(-t / 0.018)
    else:
        x += 0.15 * bp(nz, 400, 3000) * np.exp(-t / 0.006)
    return x * (1 - np.exp(-t / 0.0007)) * 0.8


@lru_cache(maxsize=64)
def woodblock(f=900.0, var=0):
    n = int(0.2 * SR); t = _t(n)
    x = np.sin(TAU * f * t) * np.exp(-t / 0.04) + 0.4 * np.sin(TAU * f * 2.57 * t) * np.exp(-t / 0.015)
    x += 0.2 * bp(_nz(n, 50 + var), 1000, 4000) * np.exp(-t / 0.002)
    return x * (1 - np.exp(-t / 0.0005)) * 0.7


@lru_cache(maxsize=64)
def timp_roll(m, dur, var=0):
    """Redoble suave de timbal (crescendo) de dur segundos."""
    n = int((dur + 2.0) * SR); x = np.zeros(n)
    hit = timpani(m, 0, 0)
    k = 0; t = 0.0; rng = np.random.default_rng(60 + var)
    while t < dur:
        g = 0.15 + 0.85 * (t / dur) ** 1.5
        i = int(t * SR); x[i:i + len(hit)] += hit[:n - i] * g * (0.8 + 0.2 * rng.random()) * 0.35
        t += 0.075 + 0.01 * rng.random(); k += 1
    return x
