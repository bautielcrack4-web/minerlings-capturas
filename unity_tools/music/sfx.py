"""Ambientes en loop (mono, sin costura) y pasos (one-shots) sintetizados.

Loops: las camas de ruido son ruido PERIODICO (FFT con periodo exacto = largo del loop) por envolventes tambien
periodicas, y los eventos (gotas, pajaros, crujidos) se renderizan con su cola y se pliegan sobre el principio."""
import numpy as np
from engine import SR, pnoise, pfilter, penv, fold, reverb, lp, hp, bp, master

TAU = 2 * np.pi


def _t(n): return np.arange(n) / SR


def _place(buf, sig, t, g=1.0):
    i = int(t * SR); m = min(len(sig), len(buf) - i)
    if m > 0: buf[i:i + m] += sig[:m] * g


def _sweep(f0, f1, n, curve=1.0):
    """Fase de un barrido de frecuencia f0->f1 en n muestras."""
    u = np.linspace(0, 1, n) ** curve
    return TAU * np.cumsum(f0 + (f1 - f0) * u) / SR


def _win(n, a=0.1, r=0.3):
    """Envolvente suave de evento (ataque y caida en fraccion del largo)."""
    u = np.linspace(0, 1, n)
    return np.clip(u / a, 0, 1) ** 1.5 * np.clip((1 - u) / r, 0, 1) ** 1.5


# ================================================================== MAZMORRA: gotas con eco + retumbo lejano
def _drip(f, rng):
    n = int(0.16 * SR); t = _t(n)
    fi = f * (1 + 0.7 * (1 - np.exp(-t / 0.014)))            # la burbuja sube de tono al cerrarse
    x = np.sin(TAU * np.cumsum(fi) / SR) * np.exp(-t / 0.032) * (1 - np.exp(-t / 0.0008))
    x += 0.25 * lp(rng.standard_normal(n), 2500) * np.exp(-t / 0.002)
    return x


def amb_dungeon(L=32.0, seed=5):
    rng = np.random.default_rng(seed); n = int(L * SR)
    rumble = pnoise(n, rng, alpha=2.0, lo=22, hi=110) * penv(n, rng, (1, 5), floor=0.35) * 0.55
    room = pnoise(n, rng, alpha=1.0, lo=120, hi=700) * penv(n, rng, (1, 3), floor=0.6) * 0.05
    ev = np.zeros(n + 10 * SR)
    for k in range(10): _place(ev, _drip(1380 + rng.normal(0, 30), rng), k * 3.2 + 0.4 + rng.normal(0, 0.04), 0.9)
    for k in range(8): _place(ev, _drip(930 + rng.normal(0, 20), rng), k * 4.0 + 1.7 + rng.normal(0, 0.05), 0.6)
    for k in range(7): _place(ev, _drip(rng.uniform(1100, 2000), rng), rng.uniform(0, L), rng.uniform(0.25, 0.5))
    echo = ev.copy()
    for d, g in ((0.23, 0.35), (0.47, 0.18), (0.71, 0.08)):
        k = int(d * SR); echo[k:] += lp(ev[:-k], 3000) * g
    wet = reverb(echo, t60=3.6, damp=2200)
    total = 0.9 * wet; total[:len(echo)] += echo
    events = fold(lp(total, 7000), n)
    return master(rumble + room + events, -26, -3.0)


# ================================================================== JUNGLA: pajaros, insectos, hojas
def _bird_whistle(rng):
    parts = []
    for _ in range(rng.integers(2, 4)):
        n1 = int(0.22 * SR); n2 = int(0.14 * SR)
        f0 = rng.uniform(1600, 2000)
        fr = np.concatenate([f0 + 0.45 * f0 * np.linspace(0, 1, n1) ** 0.7, np.linspace(f0 * 1.45, f0 * 1.1, n2)])
        ph = TAU * np.cumsum(fr) / SR
        x = (np.sin(ph) + 0.12 * np.sin(2 * ph)) * _win(len(ph), 0.08, 0.2)
        parts += [x, np.zeros(int(rng.uniform(0.12, 0.25) * SR))]
    return np.concatenate(parts)


def _bird_trill(rng):
    k = rng.integers(8, 15); rate = rng.uniform(13, 18); f = rng.uniform(2600, 3200)
    n = int(SR / rate); out = np.zeros(int(k * n + 0.1 * SR))
    for i in range(k):
        m = int(0.045 * SR); ph = _sweep(f * 1.15, f * 0.9, m)
        out[i * n:i * n + m] += np.sin(ph) * _win(m, 0.2, 0.5) * (0.6 + 0.4 * np.sin(np.pi * i / k))
    return out


def _bird_coo(rng):
    f = rng.uniform(520, 620); parts = []
    for i, d in enumerate((0.3, 0.45, 0.35)):
        m = int(d * SR); ph = _sweep(f * (1.03 if i == 1 else 1.0), f * 0.95, m)
        parts += [(np.sin(ph) + 0.2 * np.sin(2 * ph)) * _win(m, 0.25, 0.5), np.zeros(int(0.18 * SR))]
    return lp(np.concatenate(parts), 1800)


def _bird_cuckoo(rng):
    f = rng.uniform(1050, 1300); parts = []
    for r in (1.0, 0.8):
        m = int(0.24 * SR); ph = _sweep(f * r, f * r * 0.97, m)
        parts += [np.sin(ph) * _win(m, 0.15, 0.4), np.zeros(int(0.12 * SR))]
    return np.concatenate(parts)


def _frog(rng):
    f = rng.uniform(260, 340); parts = []
    for _ in range(2):
        m = int(0.12 * SR); t = _t(m)
        x = np.sin(TAU * f * t) * (0.5 + 0.5 * np.sign(np.sin(TAU * 70 * t))) * _win(m, 0.1, 0.4)
        parts += [lp(x, 1200), np.zeros(int(0.09 * SR))]
    return np.concatenate(parts)


def amb_jungle(L=36.0, seed=8):
    rng = np.random.default_rng(seed); n = int(L * SR)
    leaves = pnoise(n, rng, alpha=1.0, lo=300, hi=4500) * penv(n, rng, (2, 12), floor=0.12, power=2) * 0.11
    low = pnoise(n, rng, alpha=1.5, lo=60, hi=500) * 0.04
    am_f = round(48 * L) / L
    t = _t(n)
    cic = pnoise(n, rng, alpha=0.0, lo=3600, hi=5200) * np.sin(TAU * am_f * t) ** 8
    cic *= penv(n, rng, (1, 4), floor=0.0, power=3) * 0.12
    crk = np.sin(TAU * 4100 * t) * (np.sin(TAU * round(30 * L) / L * t) > 0.3) * \
          (np.sin(TAU * round(1.4 * L) / L * t) > 0.6) * penv(n, rng, (1, 3), floor=0.2) * 0.025
    crk = pfilter(crk, 3000, 5500)
    ev = np.zeros(n + 6 * SR); dist = np.zeros_like(ev)
    kinds = [_bird_whistle, _bird_trill, _bird_coo, _bird_cuckoo, _frog]
    times = np.sort(rng.uniform(0, L, 26))
    for i, tt in enumerate(times):
        b = kinds[i % len(kinds)](rng)
        far = rng.random() < 0.45
        _place(dist if far else ev, b, tt, rng.uniform(0.35, 0.6) if far else rng.uniform(0.5, 0.9))
    birds = ev + lp(dist, 2500)
    wet = reverb(birds, t60=1.6, damp=3000) * 0.35
    x = fold(birds, n) + fold(wet, n)
    bed = leaves + low + cic + crk
    return master(lp(bed + x, 7500), -26, -3.0)


# ================================================================== PICO: viento helado + crepitar de lava lejana
def amb_peak(L=34.0, seed=13):
    rng = np.random.default_rng(seed); n = int(L * SR)
    base = pnoise(n, rng, alpha=1.3, lo=60, hi=3500)
    wind = np.zeros(n)
    for (lo, hi), w in (((80, 380), 1.0), ((300, 800), 0.75), ((650, 1500), 0.45), ((1300, 3000), 0.2)):
        wind += pfilter(base, lo, hi) * penv(n, rng, (1, 7), floor=0.15, power=1.6) * w
    whistle = np.zeros(n)
    for c in (620, 780, 960, 1180):
        whistle += pfilter(pnoise(n, rng, 0.0, 20, 16000), c * 0.96, c * 1.04, soft=4) * penv(n, rng, (2, 9), 0.0, 4) * 0.12
    gust = penv(n, rng, (1, 4), floor=0.35)
    air = (wind + whistle) * gust
    ev = np.zeros(n + 5 * SR)
    t = 1.0
    while t < L:
        cl = rng.uniform(0.3, 1.4); k = rng.integers(10, 40)
        for tt in np.sort(rng.uniform(0, cl, k)):
            m = int(rng.uniform(0.002, 0.007) * SR)
            pop = rng.standard_normal(m) * np.exp(-_t(m) / (m / SR / 3))
            _place(ev, pop, t + tt, rng.uniform(0.2, 1.0) * (1 - 0.5 * tt / cl))
        if rng.random() < 0.6:
            m = int(0.35 * SR); f = rng.uniform(50, 80)
            _place(ev, np.sin(TAU * f * _t(m) * (1 + 0.3 * _t(m))) * np.exp(-_t(m) / 0.08) * (1 - np.exp(-_t(m) / 0.01)), t + cl * 0.3, 0.6)
        t += rng.uniform(4.0, 8.0)
    ev = lp(hp(ev, 60), 2200)
    lava = fold(ev * 0.5 + reverb(ev, t60=2.0, damp=1800)[:len(ev)] * 0.5, n)
    return master(air + lava * 0.35, -26, -3.0)


# ================================================================== MAR: olas suaves
def amb_sea(L=36.0, seed=21):
    rng = np.random.default_rng(seed); n = int(L * SR)
    t = _t(n)
    gaps = np.array([6.5, 5.5, 6.8, 5.2, 6.2, 5.8]); gaps *= L / gaps.sum()
    crest = np.cumsum(gaps) - gaps[0] + 1.5
    sw = np.zeros(n); cr = np.zeros(n); wa = np.zeros(n)
    for c, s in zip(crest, rng.uniform(0.6, 1.0, len(crest))):
        for off in (-L, 0.0, L):               # envolventes circulares
            d = t - (c + off)
            sw += s * np.where(d < 0, np.exp(-(d / 1.6) ** 2), np.exp(-d / 2.6))
            cr += s * np.where(d < 0, np.exp(-(d / 0.3) ** 2), np.exp(-d / 1.1))
            dw = d - 0.35
            wa += s * np.where(dw < 0, np.exp(-(dw / 0.5) ** 2), np.exp(-dw / 2.3))
    swell = pnoise(n, rng, alpha=1.2, lo=50, hi=600) * sw * 0.5
    crash = pnoise(n, rng, alpha=0.8, lo=250, hi=4000) * cr * 0.3
    fizz = pnoise(n, rng, alpha=0.4, lo=1500, hi=6000)
    fizz *= 1 + 1.5 * np.clip(pnoise(n, rng, 0.0, 15, 80), 0, None)       # espuma granulada
    wash = fizz * wa * 0.07
    bed = pnoise(n, rng, alpha=1.3, lo=50, hi=1500) * 0.07
    return master(swell + crash + wash + bed, -26, -3.0)


AMBS = {'amb_dungeon': amb_dungeon, 'amb_jungle': amb_jungle, 'amb_peak': amb_peak, 'amb_sea': amb_sea}


# ================================================================== PASOS
def _grains(n, rng, count, t0, t1, lo, hi, amp, dmin=0.001, dmax=0.003, shape=1.0):
    x = np.zeros(n)
    ts = t0 + (t1 - t0) * rng.random(count) ** shape
    for tt in ts:
        m = int(rng.uniform(dmin, dmax) * SR); i = int(tt * SR)
        if i + m >= n: continue
        x[i:i + m] += rng.standard_normal(m) * np.exp(-_t(m) / (m / SR / 3)) * amp * rng.exponential(1.0)
    return bp(x, lo, hi)


def _modes(n, freqs, decays, amps, sc=1.0):
    t = _t(n); x = np.zeros(n)
    for f, d, a in zip(freqs, decays, amps):
        x += a * np.sin(TAU * f * sc * t) * np.exp(-t / d)
    return x * (1 - np.exp(-t / 0.0006))


def _impact(kind, rng, n, g=1.0):
    t = _t(n); sc = rng.uniform(0.93, 1.07); nz = rng.standard_normal(n)
    if kind == 'dirt':
        x = lp(nz, 500) * np.exp(-t / 0.022) * 0.9 + np.sin(TAU * 85 * sc * t) * np.exp(-t / 0.03) * 0.8
    elif kind == 'wood':
        x = _modes(n, (110, 205, 330, 540, 900), (0.06, 0.045, 0.03, 0.02, 0.012), (1, 0.7, 0.5, 0.3, 0.12), sc)
        x += hp(nz, 2000) * np.exp(-t / 0.0015) * 0.3
    elif kind == 'marble':
        x = bp(nz, 1500, 7000) * np.exp(-t / 0.002) * 0.9
        x += _modes(n, (1900, 3100, 4300), (0.03, 0.02, 0.012), (0.18, 0.1, 0.05), sc)
        x += np.sin(TAU * 140 * sc * t) * np.exp(-t / 0.015) * 0.6
    elif kind == 'sand':
        e = (1 - np.exp(-t / 0.015)) * np.exp(-t / 0.06)
        x = bp(nz, 300, 2800) * e * 0.8 + np.sin(TAU * 70 * sc * t) * np.exp(-t / 0.04) * 0.5
    elif kind == 'stone':
        x = bp(nz, 1000, 5000) * np.exp(-t / 0.0025) * 0.6
        x += _modes(n, (160, 290, 610), (0.02, 0.012, 0.006), (1, 0.5, 0.2), sc)
    else:   # snow
        x = lp(nz, 400) * np.exp(-t / 0.03) * 0.6 + np.sin(TAU * 90 * sc * t) * np.exp(-t / 0.05) * 0.4
    return x * g


def footstep(kind, var):
    rng = np.random.default_rng(sum(map(ord, kind)) * 31 + var)
    length = {'marble': 0.9, 'stone': 0.6, 'wood': 0.45}.get(kind, 0.38)
    n = int(length * SR); x = np.zeros(n)
    t_toe = rng.uniform(0.035, 0.065)
    _place(x, _impact(kind, rng, n), 0.0, 1.0)
    _place(x, _impact(kind, rng, n), t_toe, rng.uniform(0.4, 0.6))
    if kind == 'dirt':
        x += _grains(n, rng, 30, 0.0, 0.16, 1200, 4500, 0.35, shape=1.8)
    elif kind == 'sand':
        x += _grains(n, rng, 70, 0.005, 0.18, 1200, 4500, 0.18, shape=1.4)
    elif kind == 'stone':
        x += _grains(n, rng, 10, 0.0, 0.08, 2000, 6000, 0.25, shape=2.0)
    elif kind == 'snow':
        x += _grains(n, rng, 140, 0.0, 0.2, 700, 3500, 0.35, 0.0005, 0.002, shape=1.6)
        x += _grains(n, rng, 60, t_toe, t_toe + 0.12, 900, 3000, 0.25, 0.0005, 0.002, shape=1.5)
    if kind == 'marble':
        x = x + 0.45 * reverb(x, t60=1.6, damp=4000, seed=7)[:n]
    elif kind == 'stone':
        x = x + 0.3 * reverb(x, t60=0.8, damp=3000, seed=7)[:n]
    elif kind == 'wood':
        x = x + 0.15 * reverb(x, t60=0.5, damp=3000, seed=7)[:n]
    x = lp(hp(x, 45), 8000)
    k = int(0.04 * SR); x[-k:] *= np.linspace(1, 0, k) ** 2
    return master(x, -31, -3.0, circular=False, active=True)   # = nivel de los step_N existentes


STEPS = ['step_dirt', 'step_wood', 'step_marble', 'step_sand', 'step_stone', 'step_snow']
