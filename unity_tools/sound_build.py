"""Arma los sonidos del juego desde las fuentes CC0 descargadas (Kenney, OpenGameArt, Freesound).
Cada entrada: nombre_salida, archivo, inicio, largo, tipo ('shot' | 'loop' | 'music'), opciones."""
import subprocess, numpy as np, os, sys
SR = 44100
S = '/tmp/claude-0/snd/'
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)

def load(path, ch=1):
    raw = subprocess.run(['ffmpeg','-v','error','-i',path,'-ac',str(ch),'-ar',str(SR),'-f','f32le','-'],capture_output=True).stdout
    a = np.frombuffer(raw, dtype=np.float32).copy()
    return a.reshape(-1, ch)

def active_rms(a):
    m = a.mean(1); w = SR//20; n = max(1, len(m)//w)
    fr = m[:n*w].reshape(n, w) if len(m) >= w else m.reshape(1, -1)
    r = np.sqrt((fr**2).mean(1)) + 1e-9
    db = 20*np.log10(r)
    act = r[db > db.max()-20]
    return np.sqrt((act**2).mean())

def norm(a, rms_db, peak_db=-1.0):
    g = 10**(rms_db/20) / active_rms(a)
    a = a * g
    pk = np.abs(a).max()
    lim = 10**(peak_db/20)
    if pk > lim:
        # limitador suave: compresion de picos por tanh sobre el excedente
        a = np.tanh(a / lim) * lim if pk > lim*1.6 else a * (lim/pk)
    return a

def fades(a, fi, fo):
    n = len(a); fi = int(fi*SR); fo = int(fo*SR)
    if fi > 0: a[:fi] *= np.linspace(0, 1, fi)[:, None]
    if fo > 0: a[n-fo:] *= (np.linspace(1, 0, fo)**2)[:, None]
    return a

def trim_silence(a, thr_db=-50):
    m = np.abs(a).max(1); thr = 10**(thr_db/20) * m.max()
    idx = np.where(m > thr)[0]
    if len(idx) == 0: return a
    return a[max(0, idx[0]-int(0.005*SR)): idx[-1]+int(0.02*SR)]

def loopify(a, xf):
    """Loop sin costura: el final se funde sobre el principio (crossfade de potencia constante)."""
    x = int(xf*SR)
    head, body, tail = a[:x], a[x:len(a)-x], a[len(a)-x:]
    t = np.linspace(0, 1, x)[:, None]
    mix = tail*np.cos(t*np.pi/2) + head*np.sin(t*np.pi/2)
    return np.concatenate([body, mix])

def write(name, a, ch, q):
    tmp = OUT + '/_tmp.wav'
    import wave
    b = (np.clip(a, -1, 1) * 32767).astype('<i2')
    with wave.open(tmp, 'wb') as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(SR); w.writeframes(b.tobytes())
    subprocess.run(['ffmpeg','-v','error','-y','-i',tmp,'-c:a','libvorbis','-q:a',str(q), OUT+'/'+name+'.ogg'], check=True)
    os.remove(tmp)

def shot(name, f, start=0.0, dur=None, rms=-18, fi=0.003, fo=0.08, trim=True, ch=1, speed=1.0, q=4):
    a = load(S+f, ch)
    s = int(start*SR); e = len(a) if dur is None else min(len(a), s+int(dur*SR))
    a = a[s:e]
    if trim: a = trim_silence(a)
    if speed != 1.0:
        idx = np.arange(0, len(a)-1, speed); a = np.stack([np.interp(idx, np.arange(len(a)), a[:, c]) for c in range(ch)], 1)
    a = fades(norm(a, rms), fi, min(fo, len(a)/SR*0.5))
    write(name, a, ch, q)

def loop(name, f, start=0.0, dur=None, rms=-22, xf=2.0, ch=1, q=3):
    a = load(S+f, ch)
    s = int(start*SR); e = len(a) if dur is None else min(len(a), s+int(dur*SR)+int(xf*SR))
    a = norm(a[s:e], rms, -2)
    write(name, loopify(a, xf), ch, q)

K = 'impact-sounds/Audio/'; I = 'interface-sounds/Audio/'; U = 'ui-audio/Audio/'; R = 'rpg-audio/Audio/'; C = 'casino-audio/Audio/'
J = 'music-jingles/Audio/'
def kfind(d, pat):
    import glob
    r = sorted(glob.glob(S + d + '**/' + pat, recursive=True))
    return [x[len(S):] for x in r]

jobs = []
def J_(*a, **k): jobs.append((a, k))

# ---------------------------------------------------------------- picar y vetas
for i, f in enumerate(kfind(K, 'impactMining_00*.ogg')): J_('shot', 'pick_%d' % i, f, rms=-17)
for i, f in enumerate(kfind(K, 'impactMetal_light_00*.ogg')[:4]): J_('shot', 'clink_%d' % i, f, rms=-20)
J_('shot', 'break_0', 'fs/rock_destroy.mp3', 0, 1.6, rms=-15, fo=0.4)
J_('shot', 'break_1', 'fs/stones_fall.mp3', 0, 1.4, rms=-15, fo=0.4)
J_('shot', 'break_2', 'fs/pickaxe_group.mp3', 22.9, 0.9, rms=-15, fo=0.3)
J_('shot', 'crit_0', 'fs/anvil.mp3', 0, 1.2, rms=-15, fo=0.5)
J_('shot', 'rumble', 'fs/earth_crack.mp3', 0.2, 2.2, rms=-17, fi=0.15, fo=0.8)
# ---------------------------------------------------------------- monedas y gemas
for i, f in enumerate(kfind(R, 'handleCoins*.ogg')): J_('shot', 'coin_%d' % i, f, rms=-19)
for i, f in enumerate(kfind(C, 'chips-collide-*.ogg')[:3]): J_('shot', 'coin_%d' % (i+2), f, rms=-20)
J_('shot', 'coins_pour', 'fs/coins_pour.mp3', 1.4, 2.6, rms=-17, fo=0.8)
for i, f in enumerate(kfind(K, 'impactGlass_light_00*.ogg')[:3]): J_('shot', 'gem_%d' % i, f, rms=-19)
J_('shot', 'gleam', 'fs/gleam.mp3', 0, 3.0, rms=-19, fo=1.2)
J_('shot', 'chimes', 'fs/wind_chimes.mp3', 54.4, 4.0, rms=-21, fi=0.05, fo=1.6)
# ---------------------------------------------------------------- construir
for i, f in enumerate(kfind(K, 'impactWood_medium_00*.ogg')[:3] + kfind(K, 'impactPlank_medium_00*.ogg')[:2]): J_('shot', 'build_%d' % i, f, rms=-16)
for i, f in enumerate(kfind(K, 'impactWood_heavy_00*.ogg')[:3]): J_('shot', 'thud_%d' % i, f, rms=-14)
J_('shot', 'anvil', 'fs/anvil.mp3', 0, 1.3, rms=-17, fo=0.5)
J_('shot', 'creak', 'fs/wood_creak.mp3', rms=-19)
# ---------------------------------------------------------------- interfaz
for i, f in enumerate(kfind(I, 'click_00*.ogg')[:3]): J_('shot', 'ui_%d' % i, f, rms=-20)
for i, f in enumerate(kfind(I, 'tick_00*.ogg')[:3]): J_('shot', 'tick_%d' % i, f, rms=-22)
for i, f in enumerate(kfind(I, 'open_00*.ogg')[:2]): J_('shot', 'open_%d' % i, f, rms=-21)
for i, f in enumerate(kfind(I, 'close_00*.ogg')[:2]): J_('shot', 'close_%d' % i, f, rms=-21)
for i, f in enumerate(kfind(I, 'error_00*.ogg')[:2]): J_('shot', 'error_%d' % i, f, rms=-21)
for i, f in enumerate(kfind(I, 'pluck_00*.ogg')): J_('shot', 'pop_%d' % i, f, rms=-19)
J_('shot', 'pop_2', 'fs/confetti_pop.mp3', rms=-18)
for i, f in enumerate(kfind(I, 'confirmation_00*.ogg')[:4]): J_('shot', 'confirm_%d' % i, f, rms=-19)
for i, f in enumerate(kfind(C, 'card-slide-*.ogg')[:3]): J_('shot', 'card_%d' % i, f, rms=-19)
J_('shot', 'card_3', 'fs/card_flip.mp3', rms=-19)
for i, f in enumerate(kfind(C, 'chip-lay-*.ogg')): J_('shot', 'wheel_%d' % i, f, rms=-21)
J_('shot', 'whoosh', 'fs/whoosh.mp3', rms=-19)
J_('shot', 'drumroll', 'fs/drumroll.mp3', 0, 3.2, rms=-20, fo=0.2)
J_('shot', 'paper', 'fs/paper.mp3', 17.8, 1.0, rms=-19, fo=0.3)
# ---------------------------------------------------------------- jingles (tambor metalico: isla)
st = kfind(J, 'jingles_STEEL*.ogg'); pz = kfind(J, 'jingles_PIZZI*.ogg')
for i, f in enumerate(st): J_('shot', 'steel_%02d' % i, f, rms=-18, fo=0.3, trim=True)
for i, f in enumerate(pz): J_('shot', 'pizzi_%02d' % i, f, rms=-19, fo=0.3, trim=True)
J_('shot', 'fanfare', 'fs/fanfare.mp3', 0, 4.4, rms=-17, fo=0.8)
J_('shot', 'tadaa', 'fs/tadaa.mp3', 0, 3.0, rms=-17, fo=0.8)
J_('shot', 'powerup', 'fs/powerup.mp3', rms=-18)
J_('shot', 'magic_rise', 'fs/magic_rise.mp3', 0, 4.5, rms=-18, fi=0.3, fo=0.4)
J_('shot', 'cheer', 'fs/small_crowd.mp3', 0.5, 3.0, rms=-20, fi=0.05, fo=1.0)
J_('shot', 'wow', 'fs/child_wow.mp3', 0.55, 1.6, rms=-20, fo=0.4)
J_('shot', 'firework_0', 'fs/fireworks.mp3', 7.7, 2.2, rms=-17, fo=1.0)
J_('shot', 'firework_1', 'fs/fireworks.mp3', 16.2, 2.2, rms=-17, fo=1.0)
J_('shot', 'firework_2', 'fs/fireworks.mp3', 43.4, 2.2, rms=-17, fo=1.0)
J_('shot', 'crackle', 'fs/fireworks_crackle.mp3', 56.7, 2.4, rms=-20, fo=1.0)
# ---------------------------------------------------------------- mundo
J_('shot', 'meteor', 'fs/whistle_fall.mp3', 0, 2.0, rms=-19, fo=0.15)
J_('shot', 'horn', 'fs/ship_horn.mp3', 11.3, 3.4, rms=-17, fi=0.05, fo=1.2)
J_('shot', 'bell', 'fs/ship_bell.mp3', rms=-18, fo=0.8)
J_('shot', 'cork', 'fs/cork.mp3', rms=-17)
for i, t in enumerate([1.6, 8.1, 16.9]): J_('shot', 'dig_%d' % i, 'fs/dig.mp3', t - 0.05, 0.9, rms=-17, fo=0.3)
J_('shot', 'crab', 'fs/crab.mp3', 0, 1.6, rms=-21, fo=0.4)
J_('shot', 'rooster', 'fs/rooster.mp3', rms=-20, fo=0.3)
J_('shot', 'dolphin', 'fs/dolphin.mp3', rms=-21, fo=0.4)
J_('shot', 'splash_0', 'fs/tiny_splash.mp3', rms=-19, fo=0.4)
J_('shot', 'splash_1', 'fs/fish_splash.mp3', 4.95, 1.0, rms=-19, fo=0.4)
J_('shot', 'splash_2', 'fs/fish_splash.mp3', 10.6, 1.0, rms=-19, fo=0.4)
for i in range(5): J_('shot', 'gull_%d' % i, 'amb/Seagull_Ambient_%d.wav' % (i+1), rms=-22, fo=0.2)
for i in range(4): J_('shot', 'wave_%d' % i, 'amb/wave_0%d_cc0-18363__jasinski__alkaibeach.flac' % (i+1), rms=-21, fi=0.05, fo=0.6, ch=2)
J_('shot', 'thunder_0', 'fs/thunder_strike.mp3', 1.1, 6.0, rms=-15, fo=2.5)
J_('shot', 'thunder_1', 'fs/lightning.mp3', 0.7, 5.0, rms=-15, fo=2.0)
J_('shot', 'munch', 'fs/munch.mp3', 0.4, 1.4, rms=-21, fo=0.3)
J_('shot', 'snore', 'fs/snore.mp3', 2.2, 2.4, rms=-24, fo=0.6)
J_('shot', 'wings', 'fs/wings.mp3', 25.2, 0.9, rms=-20, fo=0.3)
J_('shot', 'shower', 'fs/shower.mp3', 2.0, 3.0, rms=-22, fi=0.3, fo=0.8)
for i, f in enumerate(kfind(K, 'footstep_grass_00*.ogg')[:4]): J_('shot', 'step_%d' % i, f, rms=-26)
# ---------------------------------------------------------------- loops de ambiente
J_('loop', 'amb_sea_far', 'fs/nox_ocean.mp3', 2.0, 40.0, rms=-20, xf=3.0, ch=2)
J_('loop', 'amb_sea_near', 'fs/calm_ocean.mp3', 30.0, 44.0, rms=-19, xf=3.0, ch=2)
J_('loop', 'amb_birds', 'amb/birds-isaiah658_0.ogg', 0.0, 26.0, rms=-26, xf=3.0)
J_('loop', 'amb_wind', 'fs/wind_loop.mp3', 1.0, 34.0, rms=-26, xf=3.0)
J_('loop', 'amb_rain', 'amb/amb_rain_loop_1.ogg', 0.0, 30.0, rms=-22, xf=2.0)
J_('loop', 'amb_crickets', 'amb/crickets_1.mp3', 0.0, 9.0, rms=-27, xf=1.5)
J_('loop', 'amb_fire', 'fs/fire.mp3', 2.0, 14.0, rms=-26, xf=2.0)
# ---------------------------------------------------------------- musica
J_('loop', 'music_day', 'music/happy-ukelele-island-surfing-theme.mp3', 0.0, 133.0, rms=-20, xf=2.5, ch=2, q=3)
J_('loop', 'music_night', 'music/a-small-fire-will-do-calming-loop.mp3', 0.0, 62.0, rms=-22, xf=2.0, ch=2, q=3)

J_('shot', 'chest', 'fs/chest_open.mp3', 0, 2.2, rms=-17, fo=0.5)
only = sys.argv[2:]
for (kind, name, f, *rest), k in [((a[0], a[1], a[2], *a[3:]), k) for a, k in jobs]:
    if only and name not in only: continue
    if len(rest) >= 1: k['start'] = rest[0]
    if len(rest) >= 2: k['dur'] = rest[1]
    try:
        (shot if kind == 'shot' else loop)(name, f, **k)
    except Exception as ex:
        print('FALLA', name, f, ex)
print('listo', len(os.listdir(OUT)))
