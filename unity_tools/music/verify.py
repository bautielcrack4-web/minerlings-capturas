"""Verifica los OGG generados: los decodifica con ffmpeg y reporta duracion, canales, RMS, pico, centroide espectral
(proxy de brillo) y, en los loops, la costura (ultimos 20 ms contra primeros 20 ms).

Uso: python3 verify.py [carpeta_audio] [nombres...]"""
import os, sys
import numpy as np
from engine import SR, decode, rms_db, peak_db, centroid, active_rms

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'unity_rarezas', 'Assets', 'Resources', 'Audio'))


def seam(x, ms=20):
    """Costura del loop: diferencia de nivel entre los ultimos y los primeros `ms` ms, y salto de muestra en el
    empalme comparado con el percentil 99 de los saltos normales de la senal (>1 = posible click)."""
    k = int(ms / 1000 * SR)
    a, b = x[-k:], x[:k]
    ldiff = abs(rms_db(a) - rms_db(b))
    # referencia: saltos de nivel entre ventanas consecutivas de 20 ms DENTRO del tema (ataques normales,
    # p. ej. el tiempo fuerte de cada compas). La costura no debe saltar mas que la musica misma.
    m = x.mean(1); nw = len(m) // k
    lv = 20 * np.log10(np.sqrt((m[:nw * k].reshape(nw, k) ** 2).mean(1)) + 1e-9)
    inner = np.abs(np.diff(lv)).max()
    d = np.abs(np.diff(x, axis=0)).max(1)
    jump = np.abs(x[0] - x[-1]).max()
    ratio = jump / (np.percentile(d, 99) + 1e-12)
    # continuidad de forma: error de prediccion lineal en el empalme vs dentro de la senal
    pred = 2 * x[-1] - x[-2]
    perr = np.abs(x[0] - pred).max()
    d2 = np.abs(x[2:] - 2 * x[1:-1] + x[:-2]).max(1)
    pratio = perr / (np.percentile(d2, 99) + 1e-12)
    return ldiff, inner, ratio, pratio


def report(folder, names):
    rows = []
    for nm in names:
        p = os.path.join(folder, nm + '.ogg')
        if not os.path.exists(p):
            rows.append((nm, None)); continue
        x = decode(p)
        is_loop = nm.startswith(('music_', 'amb_'))
        r = dict(dur=len(x) / SR, ch=x.shape[1], kb=os.path.getsize(p) / 1024, rms=rms_db(x),
                 arms=20 * np.log10(active_rms(x)), peak=peak_db(x), cen=centroid(x))
        if is_loop:
            r['seam'] = seam(x)
        rows.append((nm, r))
    print('%-18s %6s %3s %7s %7s %7s %6s %6s  %s' % ('archivo', 'dur s', 'ch', 'KB', 'RMS', 'pico', 'cent', 'act', 'costura: dB nivel (max interno) | salto/p99 | pred/p99'))
    bad = 0
    for nm, r in rows:
        if r is None:
            print('%-18s FALTA' % nm); bad += 1; continue
        s = ''
        if 'seam' in r:
            l, inner, j, pj = r['seam']
            ok = l <= max(3.0, inner) and j < 1.0 and pj < 1.5
            s = '%5.2f (%5.2f) | %4.2f | %4.2f  %s' % (l, inner, j, pj, 'OK' if ok else 'REVISAR')
            bad += not ok
        if r['peak'] > -1.0: bad += 1; s += '  PICO>-1dB'
        print('%-18s %6.2f %3d %7.1f %7.1f %7.1f %6.0f %6.1f  %s' % (nm, r['dur'], r['ch'], r['kb'], r['rms'], r['peak'], r['cen'], r['arms'], s))
    return bad


if __name__ == '__main__':
    folder = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    names = sys.argv[2:]
    if not names:
        from build_all import all_names
        names = all_names()
    sys.exit(1 if report(folder, names) else 0)
