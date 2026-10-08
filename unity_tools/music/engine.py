"""Motor de audio procedural de Curio Barn: DSP vectorizado (numpy/scipy), notacion musical, mezclador con loop
sin costura y masterizado. Misma casa que unity_tools/sound_build.py (ffmpeg por subprocess, norm/loopify), pero
todo sintetizado: no depende de archivos externos.

Loop sin costura: cada tema se renderiza con cola (reverb y notas que siguen sonando) y esa cola se PLIEGA sobre el
principio (suma modulo L). Todo lo lineal (EQ, reverb) se aplica antes del pliegue y lo no lineal (limitador) es
circular, asi que el final empalma con el principio muestra a muestra.
"""
import os, subprocess, wave
import numpy as np
from scipy import signal
from scipy.ndimage import minimum_filter1d, uniform_filter1d

SR = 44100
FFMPEG = os.environ.get('FFMPEG', '/usr/bin/ffmpeg')


# ------------------------------------------------------------------ notas y acordes
def hz(m): return 440.0 * 2 ** ((m - 69) / 12.0)


_PC = {'C': 0, 'D': 2, 'E': 4, 'F': 5, 'G': 7, 'A': 9, 'B': 11}


def _pc(s):
    """'F#' -> 6, 'Bb' -> 10. Devuelve (pitch class, resto del texto)."""
    p = _PC[s[0]]; i = 1
    while i < len(s) and s[i] in '#b':
        p += 1 if s[i] == '#' else -1; i += 1
    return p % 12, s[i:]


def midi(name):
    i, acc = 1, 0
    while name[i] in '#b':
        acc += 1 if name[i] == '#' else -1; i += 1
    return 12 * (int(name[i:]) + 1) + _PC[name[0]] + acc


QUAL = {'': [0, 4, 7], 'm': [0, 3, 7], '7': [0, 4, 7, 10], 'maj7': [0, 4, 7, 11], 'm7': [0, 3, 7, 10],
        'm6': [0, 3, 7, 9], '6': [0, 4, 7, 9], 'dim': [0, 3, 6], 'dim7': [0, 3, 6, 9], 'sus4': [0, 5, 7],
        'add9': [0, 4, 7, 2], '9': [0, 4, 7, 10, 2], 'm9': [0, 3, 7, 10, 2], '7b9': [0, 4, 7, 10, 1],
        'maj9': [0, 4, 7, 11, 2], 'm7b5': [0, 3, 6, 10], '7sus4': [0, 5, 7, 10]}


class Chord:
    def __init__(self, sym):
        self.sym = sym
        main, _, bass = sym.partition('/')
        self.root, q = _pc(main)
        self.iv = QUAL[q]
        self.pcs = [(self.root + i) % 12 for i in self.iv]
        self.bass = _pc(bass)[0] if bass else self.root
        self.fifth = (self.root + (6 if 6 in self.iv and 7 not in self.iv else 7)) % 12
        self.third = (self.root + (3 if 3 in self.iv else 5 if 5 in self.iv and 4 not in self.iv else 4)) % 12

    def tones(self, lo, hi, pcs=None):
        pcs = self.pcs if pcs is None else pcs
        return [m for m in range(lo, hi + 1) if m % 12 in pcs]


def near(pc, center):
    """Nota de clase pc mas cercana a la nota center."""
    m = center - ((center - pc) % 12)
    return m if center - m <= 6 else m + 12


def parse(text):
    """'E5:1 D5:.5 r:1' -> [(midi|None, beats)]"""
    out = []
    for tok in text.split():
        n, d = tok.split(':')
        out.append((None if n == 'r' else midi(n), float(d)))
    return out


# ------------------------------------------------------------------ filtros
def _sos(kind, f, order=2):
    return signal.butter(order, f, kind, fs=SR, output='sos')


def lp(x, f, order=2): return signal.sosfilt(_sos('low', min(f, SR * 0.45), order), x, axis=0)
def hp(x, f, order=2): return signal.sosfilt(_sos('high', f, order), x, axis=0)
def bp(x, lo, hi, order=2): return signal.sosfilt(_sos('band', [lo, min(hi, SR * 0.45)], order), x, axis=0)


def peq(x, f, gain_db, q=1.0):
    """Ecualizador peaking (RBJ)."""
    A = 10 ** (gain_db / 40); w = 2 * np.pi * f / SR; al = np.sin(w) / (2 * q)
    b = [1 + al * A, -2 * np.cos(w), 1 - al * A]; a = [1 + al / A, -2 * np.cos(w), 1 - al / A]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=0)


def shelf_hi(x, f, gain_db):
    """High-shelf (RBJ, S=1): para domar agudos sin apagar."""
    A = 10 ** (gain_db / 40); w = 2 * np.pi * f / SR; al = np.sin(w) / 2 * np.sqrt(2)
    c = np.cos(w); sA = 2 * np.sqrt(A) * al
    b = [A * ((A + 1) + (A - 1) * c + sA), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - sA)]
    a = [(A + 1) - (A - 1) * c + sA, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - sA]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=0)


# ------------------------------------------------------------------ reverb (convolucion con IR sintetica)
_IR = {}


def reverb_ir(t60=1.8, damp=3500, predelay=0.015, seed=3):
    key = (t60, damp, predelay, seed)
    if key in _IR: return _IR[key]
    rng = np.random.default_rng(seed)
    n = int(SR * t60 * 1.15); t = np.arange(n) / SR
    chans = []
    for c in range(2):
        nz = rng.standard_normal(n)
        lo = lp(nz, damp, 2); hi = nz - lo
        ir = lo * np.exp(-6.91 * t / t60) + 0.35 * hi * np.exp(-6.91 * t / (t60 * 0.35))
        ir *= 1 - np.exp(-t / 0.025)                       # difusion gradual (sin "golpe" de ruido)
        for d, g in ((0.011, .5), (0.019, .38), (0.027, .3), (0.041, .22)):   # primeras reflexiones
            k = int(SR * (d + 0.004 * c)); ir[k] += g * (1 if (k + c) % 2 else -1) * 3
        ir = np.concatenate([np.zeros(int(SR * predelay)), ir])
        chans.append(ir / np.sqrt((ir ** 2).sum()))
    _IR[key] = np.stack(chans, 1)
    return _IR[key]


def reverb(x, **kw):
    """x estereo (n,2) o mono (n,). Devuelve SOLO la senal humeda (mas larga que x)."""
    ir = reverb_ir(**kw)
    if x.ndim == 1:
        return signal.fftconvolve(x, ir[:, 0])
    return np.stack([signal.fftconvolve(x[:, c], ir[:, c]) for c in range(2)], 1)


# ------------------------------------------------------------------ envolventes y ruido periodico
def env_adsr(n, a, d, s, r_start, r):
    """a,d,r en segundos; s nivel; r_start = muestra donde arranca el release."""
    t = np.arange(n) / SR
    e = np.where(t < a, 0.5 - 0.5 * np.cos(np.pi * np.clip(t / max(a, 1e-4), 0, 1)),
                 s + (1 - s) * np.exp(-(t - a) / max(d, 1e-4)))
    if r_start < n:
        k = np.arange(n - r_start) / SR
        e[r_start:] = e[r_start] * np.exp(-k / max(r, 1e-4))
    return e


def fold(x, L):
    """Suma circular: pliega todo lo que pasa de L sobre el principio (loop sin costura)."""
    out = np.zeros((L,) + x.shape[1:])
    for k in range(0, len(x), L):
        seg = x[k:k + L]; out[:len(seg)] += seg
    return out


def pnoise(n, rng, alpha=1.0, lo=20.0, hi=16000.0, soft=1.5):
    """Ruido periodico (periodo exacto n) con espectro 1/f^alpha y banda [lo,hi] de bordes suaves."""
    f = np.fft.rfftfreq(n, 1 / SR); f[0] = 1
    mag = f ** (-alpha / 2)
    mag *= 1 / (1 + (lo / f) ** (2 * soft)) / (1 + (f / hi) ** (2 * soft))
    mag[0] = 0
    X = mag * np.exp(2j * np.pi * rng.random(len(f)))
    x = np.fft.irfft(X, n)
    return x / (np.sqrt((x ** 2).mean()) + 1e-12)


def pfilter(x, lo, hi, soft=2.0):
    """Filtra circularmente (por FFT) una senal periodica: mantiene el loop perfecto."""
    n = len(x); f = np.fft.rfftfreq(n, 1 / SR); f[0] = 1
    m = 1 / (1 + (lo / f) ** (2 * soft)) / (1 + (f / hi) ** (2 * soft))
    return np.fft.irfft(np.fft.rfft(x) * m, n)


def penv(n, rng, cycles=(1, 8), floor=0.0, power=1.0):
    """Envolvente aleatoria suave y periodica (serie de Fourier con ciclos enteros) en [floor,1]."""
    t = np.arange(n) / n; e = np.zeros(n)
    for k in range(cycles[0], cycles[1] + 1):
        e += rng.normal() / k * np.cos(2 * np.pi * k * t + rng.random() * 6.283)
    e = (e - e.min()) / (e.max() - e.min() + 1e-12)
    return floor + (1 - floor) * e ** power


# ------------------------------------------------------------------ nivel, limitador, master
def rms_db(x): return 20 * np.log10(np.sqrt((np.asarray(x, float) ** 2).mean()) + 1e-12)
def peak_db(x): return 20 * np.log10(np.abs(x).max() + 1e-12)


def active_rms(a):
    """Igual que sound_build.py: RMS de las ventanas activas (ignora silencios)."""
    m = a.mean(1) if a.ndim == 2 else a
    w = SR // 20; n = max(1, len(m) // w)
    fr = m[:n * w].reshape(n, w) if len(m) >= w else m.reshape(1, -1)
    r = np.sqrt((fr ** 2).mean(1)) + 1e-9
    db = 20 * np.log10(r); act = r[db > db.max() - 20]
    return np.sqrt((act ** 2).mean())


def limit(x, ceiling_db=-1.5, win=0.03, circular=True):
    """Limitador look-ahead sin sobrepaso: min-filter + media movil (la media nunca supera la ganancia requerida).
    circular=True: el loop sigue sin costura."""
    thr = 10 ** (ceiling_db / 20)
    a = np.abs(x).max(1) if x.ndim == 2 else np.abs(x)
    g = np.minimum(1.0, thr / (a + 1e-12))
    w = int(win * SR) | 1; mode = 'wrap' if circular else 'nearest'
    g = minimum_filter1d(g, w, mode=mode)
    g = uniform_filter1d(g, w, mode=mode)
    g = uniform_filter1d(g, w // 2 | 1, mode=mode)
    g = np.minimum(g, thr / (a + 1e-12)).clip(0, 1)
    return x * (g[:, None] if x.ndim == 2 else g)


def master(x, rms=-20.0, ceiling=-1.5, circular=True, active=False):
    """Lleva el RMS al objetivo y limita picos (iterativo: el limitador baja un poco el RMS)."""
    meas = (lambda y: 20 * np.log10(active_rms(y))) if active else rms_db
    for _ in range(5):
        x = x * 10 ** ((rms - meas(x)) / 20)
        x = limit(x, ceiling, circular=circular)
    return x


# ------------------------------------------------------------------ entrada/salida
def write_ogg(path, x, q=3):
    x = np.asarray(x, float)
    ch = 1 if x.ndim == 1 else x.shape[1]
    rng = np.random.default_rng(0)
    d = (rng.random(x.shape) - rng.random(x.shape)) / 32768.0     # dither TPDF
    b = (np.clip(x + d, -1, 1) * 32767).astype('<i2')
    tmp = path + '.tmp.wav'
    with wave.open(tmp, 'wb') as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(SR); w.writeframes(b.tobytes())
    subprocess.run([FFMPEG, '-v', 'error', '-y', '-i', tmp, '-c:a', 'libvorbis', '-q:a', str(q), path], check=True)
    os.remove(tmp)


def decode(path, ch=None):
    if ch is None:
        p = subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'stream=channels', '-of', 'csv=p=0', path],
                           capture_output=True, text=True)
        ch = int(p.stdout.strip() or 1)
    raw = subprocess.run([FFMPEG, '-v', 'error', '-i', path, '-ac', str(ch), '-ar', str(SR), '-f', 'f32le', '-'],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, ch).astype(float)


# ------------------------------------------------------------------ mezclador
class Bus:
    def __init__(self, n, gain=1.0, send=0.2, fx=None):
        self.buf = np.zeros((n, 2)); self.gain = gain; self.send = send; self.fx = fx


class Song:
    """Tema en loop: tiempo en pulsos (beats). swing: posicion del corchea 'y' (0.5 recto, 0.62 swing suave)."""

    def __init__(self, bpm, beats, tail=7.0, swing=0.5, seed=1, human=0.006):
        self.bpm, self.beats, self.swing = bpm, beats, swing
        self.spb = 60.0 / bpm
        self.L = int(round(beats * self.spb * SR))
        self.n = self.L + int(tail * SR)
        self.buses = {}
        self.rng = np.random.default_rng(seed)
        self.human = human

    def bus(self, name, gain=1.0, send=0.2, fx=None):
        self.buses[name] = Bus(self.n, gain, send, fx)
        return name

    def sec(self, beat):
        b = np.floor(beat); f = beat - b; s = self.swing
        f = f / 0.5 * s if f <= 0.5 else s + (f - 0.5) / 0.5 * (1 - s)
        return (b + f) * self.spb

    def add(self, bus, sig, t, gain=1.0, pan=0.0):
        """sig mono o estereo; t en segundos (se envuelve modulo loop)."""
        i = int(round(t * SR)) % self.L
        sig = np.array(sig, float)
        k = min(len(sig) // 4, int(0.008 * SR))
        if k > 1: sig[-k:] *= np.linspace(1, 0, k)[:, None] if sig.ndim == 2 else np.linspace(1, 0, k)   # sin clicks
        if sig.ndim == 1:
            th = (pan + 1) * np.pi / 4
            sig = np.stack([sig * np.cos(th), sig * np.sin(th)], 1) * np.sqrt(2)
        m = min(len(sig), self.n - i)
        self.buses[bus].buf[i:i + m] += sig[:m] * gain

    def note(self, bus, inst, m, beat, dur, vel=0.7, pan=0.0, gain=1.0, human=None, **kw):
        h = self.human if human is None else human
        t0 = self.sec(beat); t1 = self.sec(beat + dur)
        t0 += self.rng.normal() * h
        v = float(np.clip(vel * (1 + 0.06 * self.rng.normal()), 0.05, 1.0))
        sig = inst(m, max(t1 - t0, 0.03), v, **kw)
        self.add(bus, sig, t0, gain * v, pan)

    def hit(self, bus, sig, beat, gain=1.0, pan=0.0, human=None):
        h = self.human if human is None else human
        self.add(bus, sig, self.sec(beat) + self.rng.normal() * h, gain, pan)

    def render(self, rms=-20.0, ceiling=-1.5, lowpass=10500, shelf=(5000, -2.0), revkw=None):
        dry = np.zeros((self.n, 2)); snd = np.zeros((self.n, 2))
        self.stats = {}
        for name, b in self.buses.items():
            y = b.buf
            if b.fx is not None: y = b.fx(y)
            dry += y * b.gain; snd += y * b.gain * b.send
            self.stats[name] = rms_db(y[:self.L] * b.gain)
        wet = reverb(snd, **(revkw or {}))
        total = np.zeros((len(wet), 2)); total[:self.n] += dry; total += wet
        total = hp(total, 30, 2)
        if shelf: total = shelf_hi(total, *shelf)
        if lowpass: total = lp(total, lowpass, 2)
        loop = fold(total, self.L)
        out = master(loop, rms, ceiling, circular=True)
        g = rms_db(out) - rms_db(loop)
        self.stats = {k: round(v + g, 1) for k, v in self.stats.items()}
        return out


def centroid(x, nfft=4096):
    """Centroide espectral medio (Hz) ponderado por energia: proxy de 'brillo'."""
    m = x.mean(1) if x.ndim == 2 else x
    f, t, Z = signal.stft(m, SR, nperseg=nfft)
    P = np.abs(Z) ** 2
    return float((f[:, None] * P).sum() / (P.sum() + 1e-20))
