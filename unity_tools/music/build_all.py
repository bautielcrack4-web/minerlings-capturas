"""Genera TODA la musica, ambientes y pasos de Curio Barn (100% sintetizado, deterministico) y los verifica.

Uso:   python3 unity_tools/music/build_all.py                 # todo, a unity_rarezas/Assets/Resources/Audio
       python3 unity_tools/music/build_all.py music_pico amb_sea step_wood   # solo esos
       python3 unity_tools/music/build_all.py --out /tmp/prueba            # a otra carpeta

Ademas crea el .meta de Unity de cada archivo nuevo (si no existe) copiando los ajustes de importacion del
archivo existente equivalente: music_day (streaming), amb_birds (comprimido en memoria, mono), step_0."""
import hashlib, os, sys, time
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import songs, sfx                      # noqa: E402
from engine import write_ogg           # noqa: E402
from verify import report, DEFAULT_OUT  # noqa: E402

META_TEMPLATE = {'music_': 'music_day.ogg.meta', 'amb_': 'amb_birds.ogg.meta', 'step_': 'step_0.ogg.meta'}


def all_names():
    return list(songs.SONGS) + list(sfx.AMBS) + ['%s_%d' % (s, v) for s in sfx.STEPS for v in range(3)]


def _job(args):
    name, out = args
    t = time.time()
    if name in songs.SONGS:
        x = songs.SONGS[name](); q = 3
    elif name in sfx.AMBS:
        x = sfx.AMBS[name](); q = 2
    else:
        base, var = name.rsplit('_', 1)
        x = sfx.footstep(base.split('_', 1)[1], int(var)); q = 4
    write_ogg(os.path.join(out, name + '.ogg'), x, q)
    return name, time.time() - t


def write_meta(out, name):
    meta = os.path.join(out, name + '.ogg.meta')
    if os.path.exists(meta): return False
    tpl = next((v for k, v in META_TEMPLATE.items() if name.startswith(k)), None)
    src = os.path.join(out, tpl) if tpl else None
    if not src or not os.path.exists(src): return False
    lines = open(src).read().splitlines()
    guid = hashlib.md5(('curio-barn-audio/' + name).encode()).hexdigest()
    lines = ['guid: ' + guid if l.startswith('guid:') else l for l in lines]
    with open(meta, 'w', newline='\n') as f: f.write('\n'.join(lines) + '\n')
    return True


def main(argv):
    out = DEFAULT_OUT; names = []; meta = True
    it = iter(argv)
    for a in it:
        if a == '--out': out = next(it)
        elif a == '--no-meta': meta = False
        else: names.append(a)
    every = all_names()
    if names:   # 'step_wood' expande a sus 3 variantes
        names = [n for n in every if n in names or n.rsplit('_', 1)[0] in names]
    else:
        names = every
    os.makedirs(out, exist_ok=True)
    t0 = time.time()
    # lo mas pesado primero para repartir mejor los nucleos
    names.sort(key=lambda n: (not n.startswith('music_'), not n.startswith('amb_')))
    with Pool(min(4, os.cpu_count() or 1)) as p:
        for name, dt in p.imap_unordered(_job, [(n, out) for n in names]):
            print('  %-18s %5.1f s' % (name, dt), flush=True)
    if meta:
        made = [n for n in names if write_meta(out, n)]
        if made: print('  .meta nuevos:', len(made))
    print('generado en %.0f s -> %s\n' % (time.time() - t0, out))
    return report(out, names)


if __name__ == '__main__':
    sys.exit(1 if main(sys.argv[1:]) else 0)
