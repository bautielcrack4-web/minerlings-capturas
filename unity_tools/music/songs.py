"""Los 10 temas de Curio Barn. Cada funcion devuelve el loop estereo ya masterizado (np.ndarray (n,2)).

Progresion de zonas: rancho/galpon (guitarrita country) -> tienda (acordeon, vals) -> galeria (piano jazz) ->
museo (cuarteto de cuerdas) -> museo de lujo (orquesta chica con arpa). Temas de bioma: bosque (flauta), playa
(ukelele), mazmorra (organo gracioso), jungla (percusion + marimba), pico (campanitas).

Todas las progresiones terminan en dominante (o equivalente) para volver al primer compas: el loop cierra
armonicamente y no solo en la forma de onda."""
import numpy as np
from engine import Song, Chord, near, parse, midi, lp, peq
import instruments as I


# ------------------------------------------------------------------ helpers de composicion
def play(s, bus, inst, bars, start_bar, bpb, vel=0.7, pan=0.0, legato=0.95, transpose=0, gain=1.0, accent=1.08, **kw):
    beat = start_bar * bpb
    for bar in bars:
        items = parse(bar)
        tot = sum(d for _, d in items)
        assert abs(tot - bpb) < 1e-6, 'compas mal medido (%s): %s' % (tot, bar)
        for m, d in items:
            if m is not None:
                v = vel * (accent if abs(beat % bpb) < 1e-6 else 1.0)
                s.note(bus, inst, m + transpose, beat, d * legato, v, pan, gain, **kw)
            beat += d


def voice(c, lo, hi, k, prev=None):
    """k notas del acorde en [lo,hi], conduciendo voces desde prev (notas comunes / movimiento minimo)."""
    cand = c.tones(lo, hi)
    targets = sorted(prev) if prev else list(np.linspace(lo + 2, hi - 2, k))
    out = []
    for tg in targets:
        rest = [m for m in cand if m not in out]
        if not rest: break
        out.append(min(rest, key=lambda m: (abs(m - tg), m)))
    # que no falte la tercera
    if c.third not in [m % 12 for m in out] and len(out) >= 2:
        out[-2] = near(c.third, out[-2])
    return sorted(set(out))


def prog(text): return [Chord(x) for x in text.split()]


# ================================================================== RANCHO / GALPON: guitarra country fingerpicking
def rancho():
    bpb = 4
    P = prog('G G C G Em A7 D D7  C C G Em Am D G G  G G C G Em Am7 D D7')
    s = Song(96, len(P) * bpb, seed=11)
    body = lambda y: lp(peq(peq(y, 110, 3.0, 1.2), 240, 1.5, 1.0), 7000)
    s.bus('gtr', 1.0, 0.18, body)
    s.bus('lead', 2.1, 0.22, body)
    s.bus('fiddle', 1.5, 0.3)
    s.bus('perc', 0.55, 0.12)
    rng = np.random.default_rng(1)
    for i, c in enumerate(P):
        b0 = i * bpb
        r = near(c.bass, 45)
        alt = near(c.fifth, r - 5) if r - 5 >= 40 else near(c.fifth, r + 7)
        tops = c.tones(57, 71)[-3:]
        lo_, mid, hi_ = (tops + tops)[:3] if len(tops) < 3 else tops
        # pulgar alternado (apagado con la palma)
        for k, bn in enumerate([r, alt, r, alt]):
            s.note('gtr', I.guitar, bn, b0 + k, 0.7, 0.62, -0.15, var=k)
        pattern = [(0.5, mid), (1.5, hi_), (2.5, mid), (3.5, lo_ if i % 2 else hi_)]
        if i % 2 == 0: pattern.append((0, hi_))           # pinch en el 1
        if i % 4 == 3: pattern.append((2, hi_))
        for pos, m in pattern:
            s.note('gtr', I.guitar, m, b0 + pos, 1.4, 0.45 + 0.08 * rng.random(), 0.15, var=int(pos * 2))
        # percusion suave: bombo en 1 y 3, escobilla en 2 y 4, shaker en corcheas
        for k in (0, 2): s.hit('perc', I.kick(), b0 + k, 0.35)
        for k in (1, 3): s.hit('perc', I.brush_tap(k), b0 + k, 0.4, 0.1)
        for e in range(8): s.hit('perc', I.shaker(e % 3), b0 + e * 0.5, 0.12 if e % 2 == 0 else 0.2, 0.35)
    A = ['B4:1 D5:1 E5:.5 D5:.5 B4:1', 'A4:.5 G4:.5 A4:1 B4:2', 'C5:1 E5:1.5 D5:.5 C5:1', 'D5:3 r:1',
         'B4:1 D5:1 E5:.5 G5:.5 E5:1', 'C#5:1 E5:.5 C#5:.5 A4:2', 'D5:1 F#5:1 E5:1 D5:1', 'C5:1.5 B4:.5 A4:2']
    B = ['E5:2 G5:2', 'E5:1 D5:1 C5:2', 'D5:2 B4:2', 'G4:1 B4:1 E5:2',
         'C5:2 E5:1 C5:1', 'A4:1 D5:1 F#5:2', 'G5:3 D5:1', 'B4:4']
    A2 = A[:5] + ['C5:1 E5:.5 C5:.5 A4:2', 'F#4:1 A4:1 D5:1 E5:1', 'F#5:1 E5:.5 D5:.5 C5:1 A4:1']
    play(s, 'lead', I.guitar, A, 0, bpb, 0.72, 0.3, legato=1.0, transpose=0)
    play(s, 'fiddle', I.strings, B, 8, bpb, 0.6, -0.3, legato=0.97, att=0.12, vib=14, fc=2600)
    play(s, 'lead', I.guitar, A2, 16, bpb, 0.72, 0.3, legato=1.0)
    return s.render(rms=-20)


# ================================================================== TIENDA: vals musette con acordeon
def tienda():
    bpb = 3
    A = prog('Dm Dm A7 A7 A7 A7 Dm Dm Dm D7 Gm Gm Dm A7 Dm Dm')
    B = prog('F F C7 C7 C7 C7 F F F F7 Bb Bbm F C7 F A7')
    A2 = A[:15] + [Chord('A7')]
    P = A + B + A2
    s = Song(152, len(P) * bpb, seed=22)
    s.bus('lead', 0.9, 0.22, lambda y: lp(y, 6000))
    s.bus('lh', 0.6, 0.18, lambda y: lp(y, 4000))
    s.bus('bass', 0.5, 0.12)
    s.bus('cel', 0.11, 0.35)
    s.bus('perc', 0.7, 0.15)
    prev = None
    for i, c in enumerate(P):
        b0 = i * bpb
        r = near(c.bass, 41) if i % 2 == 0 else near(c.fifth, 38)
        s.note('bass', I.ubass, r, b0, 0.9, 0.7, 0.0, var=i)
        s.note('lh', I.accordion, r, b0, 0.6, 0.6, -0.1, wet=3.0)
        prev = voice(c, 53, 66, 3, prev)
        for k in (1, 2):
            for m in prev:
                s.note('lh', I.accordion, m, b0 + k, 0.42, 0.5, -0.18, wet=4.0, human=0.003)
            s.hit('perc', I.brush_tap(k), b0 + k, 0.35, 0.15)
    MA = ['A4:1 D5:1 F5:1', 'A5:2 G5:1', 'F5:1 E5:1 D5:1', 'C#5:2 A4:1', 'G4:1 A4:1 C#5:1', 'E5:2 D5:1',
          'C#5:1 D5:1 E5:1', 'D5:3', 'A4:1 D5:1 F5:1', 'A5:2 F#5:1', 'G5:1 Bb5:1 G5:1', 'D5:2 Bb4:1',
          'A4:1 D5:1 F5:1', 'E5:1 G5:1 C#5:1', 'D5:3', 'r:1 A4:1 C5:1']
    MB = ['C5:.5 D5:.5 C5:1 A4:1', 'F5:2 C5:1', 'Bb4:.5 C5:.5 Bb4:1 G4:1', 'E5:2 C5:1', 'G5:1 F5:1 E5:1',
          'D5:1 C5:1 Bb4:1', 'A4:1 C5:1 F5:1', 'A5:3', 'C5:.5 D5:.5 C5:1 A4:1', 'Eb5:2 C5:1', 'D5:1 F5:1 Bb5:1',
          'Db5:2 F5:1', 'C5:1 F5:1 A5:1', 'G5:1 E5:1 C5:1', 'F5:3', 'E5:1 C#5:1 A4:1']
    MA2 = MA[:15] + ['E5:2 C#5:1']
    play(s, 'lead', I.accordion, MA, 0, bpb, 0.75, 0.08, legato=0.9, wet=14.0)
    play(s, 'lead', I.accordion, MB, 16, bpb, 0.78, 0.08, legato=0.9, wet=14.0)
    play(s, 'cel', I.celesta, MB, 16, bpb, 0.5, 0.35, legato=1.0)
    play(s, 'lead', I.accordion, MA2, 32, bpb, 0.75, 0.08, legato=0.9, wet=14.0)
    return s.render(rms=-20)


# ================================================================== GALERIA: trio de jazz suave (piano, contrabajo, escobillas)
def galeria():
    bpb = 4
    P = prog('Fmaj7 Dm7 Gm7 C7 Am7 D7 Gm7 C7  Fmaj7 F7 Bbmaj7 Bbm6 Am7 D7 Gm7 C7  Cm7 F7 Bbmaj7 Bbm7 Am7 D7 Gm7 C7')
    s = Song(96, len(P) * bpb, swing=0.62, seed=33)
    s.bus('rh', 0.95, 0.25)
    s.bus('lh', 0.6, 0.25)
    s.bus('bass', 0.65, 0.12)
    s.bus('drums', 0.8, 0.15)
    rng = np.random.default_rng(3)
    comp = [[(0, 1.4)], [(1.5, 0.6), (3, 0.8)], [(0, 0.7), (2.5, 1.2)], [(-0.5, 1.6), (2.5, 0.5)], [(0.5, 1.0), (2, 1.0)]]
    for i, c in enumerate(P):
        b0 = i * bpb
        nxt = P[(i + 1) % len(P)]
        # voicing sin fundamental: 3a, 7a (o 6a) y 9a
        sev = [x for x in c.iv if x in (9, 10, 11)]
        pcs = [c.third, (c.root + (sev[-1] if sev else 9)) % 12, (c.root + 2) % 12]
        v = sorted(near(p, 58) for p in pcs)
        for pos, d in comp[rng.integers(len(comp))] if i % 4 else comp[0]:
            for m in v:
                s.note('lh', I.piano, m, b0 + pos, d, 0.38, -0.2)
        # contrabajo caminando
        r = near(c.root, 40)
        tgt = near(nxt.root, 40)
        walk = [r, near(c.third, r + 4), near(c.fifth, r + 7), tgt + (1 if rng.random() < 0.5 else -1)]
        for k, m in enumerate(walk):
            s.note('bass', I.ubass, m, b0 + k, 0.9, 0.75 if k == 0 else 0.62, 0.05, var=k)
        # escobillas
        for k in (0, 2):
            s.hit('drums', I.brush_swish(round(2 * s.spb, 2), k), b0 + k, 0.55, -0.3 if k == 0 else 0.3)
        for k in (1, 3): s.hit('drums', I.brush_tap(k), b0 + k, 0.45, 0.2)
        for k in (0, 2): s.hit('drums', I.kick(), b0 + k, 0.18)
        s.hit('drums', I.brush_tap(5), b0 + 3.5, 0.18, 0.25)
    M = ['A4:1.5 C5:.5 E5:1 D5:1', 'C5:2 r:1 A4:.5 C5:.5', 'Bb4:.5 A4:.5 Bb4:.5 D5:.5 F5:1 E5:.5 D5:.5', 'E5:2 r:2',
         'r:.5 E5:.5 G5:.5 E5:.5 C5:1 A4:1', 'F#5:1.5 E5:.5 D5:1 C5:1', 'Bb4:.5 D5:.5 F5:1 A5:1 G5:1', 'E5:3 r:1',
         'C5:.5 D5:.5 E5:.5 G5:.5 A5:1 F5:1', 'Eb5:2 r:.5 C5:.5 D5:.5 Eb5:.5', 'D5:1.5 C5:.5 A4:2', 'Db5:2 r:1 Bb4:.5 C5:.5',
         'C5:1 E5:1 G5:.5 E5:.5 C5:1', 'A5:1.5 F#5:.5 E5:1 C5:1', 'Bb4:.5 C5:.5 D5:.5 F5:.5 A5:1 G5:1', 'E5:2 r:2',
         'G5:1.5 Eb5:.5 C5:1 Bb4:1', 'A4:2 r:1 C5:.5 Eb5:.5', 'D5:1.5 F5:.5 A5:2', 'Ab5:1 F5:1 Db5:2',
         'C5:1 E5:1 G5:1 E5:1', 'F#5:1.5 A5:.5 C6:1 A5:1', 'Bb5:1 G5:.5 F5:.5 D5:1 Bb4:1', 'Bb4:1 C5:.5 E5:.5 G5:1 r:1']
    play(s, 'rh', I.piano, M, 0, bpb, 0.62, 0.12, legato=0.92)
    return s.render(rms=-20)


# ================================================================== MUSEO: cuarteto de cuerdas, calmo
def museo():
    bpb = 4
    P = prog('D A/C# Bm F#m/A G D/F# Em7 A7  D F#m G D Bm Em A A7')
    s = Song(64, len(P) * bpb, seed=44)
    s.bus('vln1', 0.85, 0.3)
    s.bus('vln2', 0.6, 0.32)
    s.bus('vla', 0.62, 0.32)
    s.bus('vc', 0.7, 0.28)
    p2 = p3 = None
    for i, c in enumerate(P):
        b0 = i * bpb
        cb = near(c.bass, 45) if c.bass != c.root else near(c.root, 43)
        s.note('vc', I.strings, cb, b0, 2.0, 0.6, 0.35, att=0.2, vib=9, fc=1800, var=i)
        s.note('vc', I.strings, cb if i % 2 else near(c.fifth, cb + 5) - (12 if near(c.fifth, cb + 5) > 52 else 0),
               b0 + 2, 2.0, 0.55, 0.35, att=0.2, vib=9, fc=1800, var=i + 1)
        p2 = voice(c, 62, 73, 1, p2); p3 = voice(c, 55, 66, 1, p3)
        if i < 8:
            s.note('vln2', I.strings, p2[0], b0, 4.0, 0.45, -0.12, att=0.45, vib=9, var=i)
            s.note('vla', I.strings, p3[0], b0, 4.0, 0.45, 0.15, att=0.45, vib=8, fc=2000, var=i)
        else:
            for k in range(4):
                s.note('vln2', I.strings, p2[0], b0 + k, 0.85, 0.42 if k % 2 else 0.5, -0.12, att=0.07, vib=7, var=k)
                s.note('vla', I.strings, p3[0], b0 + k, 0.85, 0.4 if k % 2 else 0.48, 0.15, att=0.07, vib=7, fc=2000, var=k)
    M = ['F#5:2 E5:1 D5:1', 'C#5:2 E5:2', 'D5:1.5 C#5:.5 B4:2', 'A4:2 C#5:2', 'B4:1 D5:1 G5:1.5 F#5:.5',
         'F#5:2 D5:2', 'G5:1 F#5:1 E5:1 D5:1', 'C#5:1 D5:1 E5:2',
         'F#5:3 A5:1', 'A5:1.5 G5:.5 F#5:1 E5:1', 'D5:2 B4:1 D5:1', 'F#5:1 E5:1 D5:2', 'D5:1 F#5:1 B5:2',
         'G5:1.5 F#5:.5 E5:2', 'C#5:1 E5:1 A5:1 G5:1', 'E5:2 C#5:1 A4:1']
    play(s, 'vln1', I.strings, M, 0, bpb, 0.62, -0.35, legato=0.98, att=0.16, vib=13, fc=2800)
    return s.render(rms=-20, revkw=dict(t60=2.3, damp=3000))


# ================================================================== LUJO: orquesta chica con arpa
def lujo():
    bpb = 4
    P = prog('Eb Gm/D Cm Ab Eb/G Ab Fm7 Bb7  Eb Ab Fm Bb Cm Ab F7 Bb  Ab Eb/G Fm7 Bb7')
    s = Song(76, len(P) * bpb, seed=55)
    s.bus('harp', 0.7, 0.35)
    s.bus('str', 0.55, 0.4, lambda y: lp(y, 5000))
    s.bus('low', 0.6, 0.3)
    s.bus('mel', 1.05, 0.35)
    s.bus('horn', 0.8, 0.4)
    s.bus('perc', 0.6, 0.35)
    prev = None
    for i, c in enumerate(P):
        b0 = i * bpb
        tones = c.tones(51, 82)
        root_i = min(range(len(tones)), key=lambda j: abs(tones[j] - near(c.root, 52)))
        arp = tones[root_i:root_i + 6]
        while len(arp) < 6: arp.append(arp[-1] + 12)
        seq = arp[:6] + [arp[4], arp[2]] if i % 2 == 0 else arp[:5] + [arp[5], arp[3], arp[1]]
        for k, m in enumerate(seq):
            s.note('harp', I.harp, m, b0 + 0.5 * k, 2.5, 0.45 + 0.05 * (k == 0), -0.25 + 0.08 * k, var=k)
        prev = voice(c, 55, 72, 4, prev)
        for j, m in enumerate(prev):
            s.note('str', I.strings, m, b0, 4.05, 0.42, (-0.5, -0.2, 0.2, 0.5)[j % 4], att=0.6, vib=8, voices=3, rel=0.6, fc=2200, var=j)
        lb = near(c.bass, 40)
        s.note('low', I.strings, lb, b0, 4.0, 0.55, 0.25, att=0.3, vib=6, voices=2, fc=1200)
        s.note('low', I.ubass, lb - 12 if lb - 12 >= 28 else lb, b0, 1.5, 0.5, 0.1)
        if 8 <= i < 20 and i % 2 == 0:
            s.note('horn', I.horn, near(c.third, 62), b0, 7.6, 0.55, 0.3)
            s.note('horn', I.horn, near(c.fifth, 57), b0, 7.6, 0.5, 0.35)
    # redobles suaves de timbal hacia los compases 9 y 17 y golpe en el 1
    for bar, root in ((8, midi('Eb2')), (16, midi('Ab2'))):
        s.add('perc', I.timp_roll(midi('Bb2'), round(2 * s.spb, 2)), s.sec(bar * bpb - 2), 0.45, 0.1)
        s.hit('perc', I.timpani(root, 1, 0.7), bar * bpb, 0.6, 0.1)
        s.note('perc', I.celesta, midi('G6') if bar == 8 else midi('Eb6'), bar * bpb, 1, 0.5, -0.3, gain=0.3)
    M = ['Bb4:1 Eb5:1 G5:2', 'F5:1.5 Eb5:.5 D5:2', 'Eb5:1 G5:1 C6:2', 'Ab5:2 Eb5:2', 'G5:1 F5:1 Eb5:1 Bb4:1',
         'C5:2 Eb5:1 Ab5:1', 'G5:1 F5:1 Eb5:1 C5:1', 'D5:3 r:1',
         'Bb4:1 Eb5:1 G5:1.5 Bb5:.5', 'C6:2 Ab5:2', 'Ab5:1 G5:1 F5:1 C5:1', 'D5:2 F5:2', 'Eb5:1.5 D5:.5 C5:1 G5:1',
         'Ab5:2 Eb5:2', 'A4:1 C5:1 F5:1 A5:1', 'Bb5:3 r:1',
         'C6:1.5 Bb5:.5 Ab5:1 Eb5:1', 'G5:2 Bb5:2', 'Ab5:1 G5:1 F5:1 Eb5:1', 'D5:2 Bb4:2']
    play(s, 'mel', I.flute, M[:8], 0, bpb, 0.6, -0.15, legato=0.97)
    play(s, 'mel', I.clarinet, M[8:16], 8, bpb, 0.6, 0.15, legato=0.97, transpose=-12)
    play(s, 'mel', I.flute, M[16:], 16, bpb, 0.62, -0.15, legato=0.97)
    play(s, 'mel', I.clarinet, M[16:], 16, bpb, 0.5, 0.2, legato=0.97, transpose=-12, gain=0.7)
    return s.render(rms=-20, revkw=dict(t60=2.5, damp=3000))


# ================================================================== BOSQUE: flauta sobre punteos y pad
def bosque():
    bpb = 4
    P = prog('Em Cmaj7 G D Em Cmaj7 Am7 D  C G D Em C G Am7 B7')
    s = Song(64, len(P) * bpb, seed=66)
    s.bus('flute', 1.1, 0.35)
    s.bus('pluck', 0.75, 0.3, lambda y: peq(y, 150, 2.0, 1.0))
    s.bus('pad', 0.32, 0.45)
    s.bus('glint', 0.6, 0.5)
    prev = None
    for i, c in enumerate(P):
        b0 = i * bpb
        bass = near(c.root, 45)
        t = c.tones(52, 72)
        up = t[:4] if len(t) >= 4 else (t + [x + 12 for x in t])[:4]
        seq = [bass, up[0], up[1], up[2], up[3], up[2], up[1], up[0]] if i % 2 == 0 else \
              [bass, up[1], up[2], up[3], up[2], up[1], up[0], up[1]]
        for k, m in enumerate(seq):
            s.note('pluck', I.nylon, m, b0 + 0.5 * k, 2.2 if k == 0 else 1.4, 0.48 if k == 0 else 0.38, 0.25, var=k)
        prev = voice(c, 52, 67, 3, prev)
        for j, m in enumerate(prev):
            s.note('pad', I.pad, m, b0, 4.0, 0.4, (-0.6, 0.0, 0.6)[j % 3])
        if i % 4 == 3:
            hi = c.tones(76, 88)[:3]
            for k, m in enumerate(hi):
                s.note('glint', I.harp, m, b0 + 3 + 0.25 * k, 1.5, 0.35, 0.4 - 0.3 * k)
    M = ['B4:1 E5:1 F#5:1 G5:1', 'G5:1.5 F#5:.5 E5:2', 'D5:1 B4:1 D5:1 G5:1', 'F#5:3 r:1',
         'B4:1 E5:1 F#5:1 G5:1', 'A5:1.5 G5:.5 E5:2', 'E5:1 D5:1 C5:1 A4:1', 'D5:3 r:1',
         'C5:1 E5:1 G5:1.5 E5:.5', 'D5:2 B4:2', 'A4:1 D5:1 F#5:1 A5:1', 'G5:3 r:1',
         'E5:1.5 F#5:.5 G5:1 E5:1', 'D5:1 B4:1 G4:2', 'C5:1 E5:1 G5:1 E5:1', 'D#5:2 F#5:1 r:1']
    play(s, 'flute', I.flute, M, 0, bpb, 0.6, -0.1, legato=0.96)
    return s.render(rms=-20, revkw=dict(t60=2.2, damp=3200))


# ================================================================== PLAYA: rasgueo de ukelele + shaker
UKE = {'C': 'G4 C4 E4 C5', 'Am': 'A4 C4 E4 A4', 'F': 'A4 C4 F4 A4', 'G': 'G4 D4 G4 B4', 'Em': 'G4 E4 G4 B4',
       'Dm7': 'A4 D4 F4 C5', 'G7': 'G4 D4 F4 B4'}


def playa():
    bpb = 4
    P = prog('C Am F G C Am F G  F G Em Am F G C C  C Am F G C Am Dm7 G7')
    s = Song(104, len(P) * bpb, seed=77)
    s.bus('strum', 0.75, 0.2, lambda y: lp(peq(y, 300, 2.0, 1.0), 6500))
    s.bus('lead', 1.6, 0.25)
    s.bus('whistle', 1.5, 0.3)
    s.bus('bass', 0.45, 0.1)
    s.bus('perc', 0.5, 0.12)
    rng = np.random.default_rng(7)
    strums = [(0, 'D', 0.55), (1, 'D', 0.5), (1.5, 'U', 0.38), (2.5, 'U', 0.36), (3, 'D', 0.48), (3.5, 'U', 0.36)]
    for i, c in enumerate(P):
        b0 = i * bpb
        notes = [midi(x) for x in UKE[c.sym].split()]
        for j, (pos, d, v) in enumerate(strums):
            nxt = strums[j + 1][0] if j + 1 < len(strums) else 4
            order = notes if d == 'D' else notes[::-1][:3]
            t0 = s.sec(b0 + pos) + rng.normal() * 0.004
            dur = (nxt - pos) * s.spb + 0.05
            for k, m in enumerate(order):
                sig = I.uke(m, dur - k * 0.012, v, var=k + j)
                s.add('strum', sig, t0 + k * (0.013 if d == 'D' else 0.010), v * (0.9 + 0.2 * rng.random()), -0.25 + 0.15 * k)
        r = near(c.root, 40); f5 = near(c.fifth, r + 7)
        for pos, m, d in ((0, r, 1.4), (2, f5, 1.0), (3.5, r, 0.45)):
            s.note('bass', I.ubass, m, b0 + pos, d, 0.65, 0.0)
        for k in (0, 2): s.hit('perc', I.kick(), b0 + k, 0.3)
        for e in range(8): s.hit('perc', I.shaker(e % 3), b0 + 0.5 * e, 0.16 if e % 2 == 0 else 0.3, 0.35)
        s.hit('perc', I.conga(330.0, 'open', i % 2), b0 + 3.5, 0.18, -0.4)
        s.hit('perc', I.conga(240.0, 'open', i % 2), b0 + 3.75 if i % 4 == 3 else b0 + 2.5, 0.15, -0.4)
    A = ['E5:1 G5:.5 E5:.5 C5:1 D5:1', 'E5:1.5 C5:.5 A4:2', 'A4:.5 C5:.5 F5:1 E5:.5 D5:.5 C5:1', 'D5:3 r:1',
         'E5:1 G5:.5 E5:.5 C5:1 D5:1', 'E5:1.5 G5:.5 A5:2', 'A5:.5 G5:.5 F5:1 E5:1 D5:1', 'G5:2 r:2']
    B = ['A5:2 F5:2', 'G5:1.5 D5:.5 B4:2', 'G5:2 E5:1 B4:1', 'C5:2 E5:2', 'F5:1 A5:1 C6:1 A5:1',
         'G5:1 F5:1 E5:1 D5:1', 'E5:3 r:1', 'r:4']
    A2 = A[:6] + ['F5:1 E5:.5 D5:.5 C5:1 A4:1', 'B4:1 D5:1 F5:1 D5:1']
    play(s, 'lead', I.uke, A, 0, bpb, 0.75, 0.2, legato=1.0)
    play(s, 'whistle', I.flute, B, 8, bpb, 0.5, -0.15, legato=0.92)
    play(s, 'lead', I.uke, A2, 16, bpb, 0.75, 0.2, legato=1.0)
    return s.render(rms=-20)


# ================================================================== MAZMORRA: organo de castillo embrujado (gracioso)
def mazmorra():
    bpb = 4
    A = prog('Dm Dm Bb7 A7 Dm Dm E7 A7')
    A2 = prog('Dm Dm Bb7 A7 Gm Dm/A A7 Dm')
    B = prog('Gm Gm Dm Dm Bb7 A7 Dm A7')
    P = A + A2 + B
    s = Song(108, len(P) * bpb, seed=88)
    s.bus('org', 0.8, 0.3, lambda y: lp(y, 5500))
    s.bus('chords', 0.5, 0.3, lambda y: lp(y, 4000))
    s.bus('tuba', 0.85, 0.15)
    s.bus('xylo', 0.16, 0.3)
    s.bus('tick', 0.3, 0.2)
    for i, c in enumerate(P):
        b0 = i * bpb
        r = near(c.bass, 38); f5 = near(c.fifth, r - 5) if r - 5 >= 31 else near(c.fifth, r + 7)
        v = voice(c, 57, 69, 3)
        if i < 16:
            for k, m in enumerate([r, f5, r, f5]):
                s.note('tuba', I.tuba, m, b0 + k, 0.42, 0.7 if k % 2 == 0 else 0.6, 0.1)
            for k in (1, 3):
                for m in v: s.note('chords', I.organ, m, b0 + k, 0.35, 0.5, -0.2, reg='flute', human=0.002)
            for k in range(4):
                s.hit('tick', I.woodblock(820.0 if k % 2 == 0 else 640.0, k), b0 + k + 0.5, 0.5, -0.45)
        else:
            s.note('tuba', I.tuba, r, b0, 1.6, 0.65, 0.1); s.note('tuba', I.tuba, f5, b0 + 2, 1.6, 0.6, 0.1)
            for m in v: s.note('chords', I.organ, m, b0, 3.9, 0.45, -0.2, reg='flute')
    MA = ['D5:.5 F5:.5 A5:.5 r:.5 G#5:.5 A5:.5 r:1', 'F5:.5 E5:.5 F5:.5 D5:.5 A4:2',
          'D5:.5 F5:.5 Ab5:.5 r:.5 G5:.5 Ab5:.5 r:1', 'G5:.5 F5:.5 E5:.5 C#5:.5 A4:2',
          'D5:.5 F5:.5 A5:.5 r:.5 G#5:.5 A5:.5 r:1', 'D6:1 C6:.5 A5:.5 F5:1 D5:1',
          'E5:.5 G#5:.5 B5:.5 D6:.5 C6:.5 B5:.5 G#5:1', 'A5:1.5 r:.5 E5:.5 C#5:.5 A4:1']
    MA2 = MA[:4] + ['G5:.5 Bb5:.5 D6:.5 r:.5 C#6:.5 D6:.5 r:1', 'A5:.5 F5:.5 D5:.5 F5:.5 A5:2',
                    'G5:.5 E5:.5 C#5:.5 E5:.5 G5:.5 F5:.5 E5:1', 'D5:2 r:2']
    MB = ['Bb4:2 D5:2', 'Eb5:2 D5:2', 'F5:2 A5:2', 'G#5:.5 A5:.5 G#5:.5 A5:.5 F5:2',
          'Ab5:2 F5:1 D5:1', 'C#5:2 E5:1 G5:1', 'F5:1 E5:.5 D5:.5 A4:2', 'C#5:.5 E5:.5 G5:.5 Bb5:.5 A5:1 r:1']
    play(s, 'org', I.organ, MA, 0, bpb, 0.7, 0.05, legato=0.55)
    play(s, 'org', I.organ, MA2, 8, bpb, 0.7, 0.05, legato=0.55)
    play(s, 'xylo', I.xylo, MA2[:4], 8, bpb, 0.6, 0.4, legato=1.0)
    play(s, 'org', I.organ, MB, 16, bpb, 0.65, 0.05, legato=0.95)
    return s.render(rms=-20)


# ================================================================== JUNGLA: percusion + gancho de marimba
def jungla():
    bpb = 4
    A = prog('Am F C G Am F C G')
    B = prog('Dm Am F G Dm Am E7 E7')
    A2 = prog('Am F C G Am F C E7')
    P = A + B + A2
    s = Song(104, len(P) * bpb, seed=99)
    s.bus('mar', 0.85, 0.25)
    s.bus('comp', 0.45, 0.25)
    s.bus('bass', 0.8, 0.08)
    s.bus('drums', 0.75, 0.15)
    s.bus('pad', 0.36, 0.4)
    tumbao = [(0, 'mute', 220, .45), (.5, 'mute', 220, .25), (1, 'slap', 330, .6), (1.5, 'mute', 330, .25),
              (2, 'mute', 220, .45), (2.5, 'mute', 220, .25), (3, 'open', 330, .7), (3.5, 'open', 240, .65)]
    clave = [[0, 1.5, 3], [1, 2]]
    for i, c in enumerate(P):
        b0 = i * bpb
        for pos, tone, f, g in tumbao:
            s.hit('drums', I.conga(float(f), tone, int(pos * 2) % 3), b0 + pos, g, -0.3)
        for k in range(16):
            s.hit('drums', I.shaker(k % 3, 0.07), b0 + 0.25 * k, (0.12, 0.06, 0.2, 0.08)[k % 4], 0.3)
        for pos in clave[i % 2]:
            s.hit('drums', I.woodblock(1000.0, int(pos)), b0 + pos, 0.32, -0.12)
        for k in (0, 2): s.hit('drums', I.conga(85.0, 'bass', k), b0 + k, 0.55, 0.0)
        if i % 4 == 3:
            for k in range(4):
                s.hit('drums', I.conga(560.0 if k % 2 == 0 else 420.0, 'open', k), b0 + 3 + 0.25 * k, 0.32, 0.4)
        r = near(c.root, 40)
        for pos, m, d in ((0, r, 0.9), (1.5, r, 0.5), (2.5, near(c.fifth, r + 7), 0.5), (3.5, r + 12, 0.4)):
            s.note('bass', I.mbass, m, b0 + pos, d, 0.7, 0.0)
        v = voice(c, 60, 72, 2)
        for pos in (1.5, 3.5):
            for m in v: s.note('comp', I.marimba, m, b0 + pos, 0.4, 0.4, -0.25)
        if 8 <= i < 16:
            for j, m in enumerate(voice(c, 55, 67, 3)): s.note('pad', I.pad, m, b0, 4.0, 0.4, (-0.5, 0, 0.5)[j])
    H = ['A4:.5 C5:.5 E5:.5 A5:.5 r:.5 G5:.5 E5:1', 'F5:.5 A5:.5 r:.5 G5:.5 F5:.5 E5:.5 C5:1',
         'E5:.5 G5:.5 C6:.5 r:.5 B5:.5 G5:.5 E5:1', 'D5:.5 G5:.5 B5:.5 r:.5 A5:.5 G5:.5 D5:1']
    HA = H + H[:3] + ['D5:.5 E5:.5 G5:.5 A5:.5 B5:1 r:1']
    HB = ['F5:1 D5:.5 A4:.5 r:2', 'E5:1 C5:.5 A4:.5 r:2', 'A5:.5 G5:.5 F5:.5 E5:.5 D5:.5 C5:.5 A4:1', 'B4:1 D5:1 G5:2',
          'F5:1 D5:.5 A4:.5 r:2', 'E5:1 C5:.5 A4:.5 r:2', 'G#5:.5 B5:.5 D6:.5 B5:.5 G#5:.5 E5:.5 D5:1', 'B4:2 r:2']
    HA2 = H + H[:3] + ['E5:.5 G#5:.5 B5:.5 r:.5 D6:.5 B5:.5 G#5:1']
    play(s, 'mar', I.marimba, HA, 0, bpb, 0.75, 0.12, legato=1.0)
    play(s, 'mar', I.marimba, HB, 8, bpb, 0.72, 0.12, legato=1.0)
    play(s, 'mar', I.marimba, HA2, 16, bpb, 0.75, 0.12, legato=1.0)
    return s.render(rms=-20)


# ================================================================== PICO: celesta y glockenspiel con pad aireado
def pico():
    bpb = 4
    P = prog('Emaj7 F#/E C#m7 Aadd9 Emaj7 F#/E Bsus4 B  Amaj7 G#m7 F#m7 Bsus4 C#m7 Aadd9 F#m7 B7')
    s = Song(64, len(P) * bpb, seed=111)
    s.bus('glock', 0.72, 0.45, lambda y: lp(y, 6000))
    s.bus('cel', 0.33, 0.45, lambda y: lp(y, 6000))
    s.bus('pad', 0.45, 0.5)
    s.bus('sub', 0.5, 0.1)
    rng = np.random.default_rng(11)
    prev = None
    for i, c in enumerate(P):
        b0 = i * bpb
        t = c.tones(64, 81)
        shape = [0, 1, 2, 3, 4, 3, 2, 1] if i % 2 == 0 else [1, 2, 3, 4, 5, 4, 3, 2]
        for k, idx in enumerate(shape):
            if k > 0 and rng.random() < 0.22: continue
            m = t[min(idx, len(t) - 1)]
            s.note('cel', I.celesta, m, b0 + 0.5 * k, 1.0, 0.42, 0.3 - 0.1 * (k % 3))
        prev = voice(c, 52, 71, 4, prev)
        for j, m in enumerate(prev):
            s.note('pad', I.pad, m, b0, 4.0, 0.4, (-0.6, -0.2, 0.2, 0.6)[j % 4], kind='air')
        s.note('sub', I.sub, near(c.bass, 40), b0, 3.9, 0.6, 0.0)
        if rng.random() < 0.4:
            s.note('cel', I.celesta, rng.choice(c.tones(83, 90) or [88]), b0 + 2.75, 0.5, 0.3, -0.5)
    M = ['G#5:1 B5:1 D#6:2', 'C#6:2 A#5:2', 'B5:1.5 G#5:.5 E5:2', 'A5:1 B5:1 C#6:2', 'G#5:1 B5:1 D#6:2',
         'E6:2 C#6:2', 'E6:1 C#6:1 B5:2', 'F#5:3 r:1',
         'C#6:1.5 B5:.5 G#5:2', 'F#5:1 G#5:1 B5:2', 'A5:2 C#6:2', 'B5:3 r:1', 'E6:1 D#6:1 C#6:1 B5:1',
         'A5:2 B5:2', 'C#6:1.5 B5:.5 A5:2', 'D#5:1 F#5:1 A5:1 B5:1']
    play(s, 'glock', I.glock, M, 0, bpb, 0.55, -0.1, legato=1.0, transpose=0)
    return s.render(rms=-20, revkw=dict(t60=3.0, damp=3500))


SONGS = {'music_rancho': rancho, 'music_tienda': tienda, 'music_galeria': galeria, 'music_museo': museo,
         'music_lujo': lujo, 'music_bosque': bosque, 'music_playa': playa, 'music_mazmorra': mazmorra,
         'music_jungla': jungla, 'music_pico': pico}
