import sys, numpy as np
from PIL import Image
sys.path.insert(0, '.')
from compare import mask
def load(p, comp):
    im = Image.open(p).convert('RGBA')
    if comp:
        w = Image.new('RGBA', im.size, (250, 250, 250, 255)); im = Image.alpha_composite(w, im)
    return mask(im.convert('RGB').resize((710, 635)))
r, b = load('ref/front.png', False), load('out/front.png', True)
for name, m in (('ref', r), ('blend', b)):
    rows = np.where(m.any(axis=1))[0]
    print(name, 'top', rows.min(), 'bottom', rows.max())
print('row  ref[x0-x1 w]   blend[x0-x1 w]')
for y in (20, 60, 100, 140, 180, 200, 250, 300, 360, 400, 430, 470, 510, 540, 580, 610):
    def seg(m):
        xs = np.where(m[y])[0]
        if len(xs) == 0: return '-'
        # tramos
        cuts = np.where(np.diff(xs) > 3)[0]
        parts = np.split(xs, cuts + 1)
        return ' '.join('%d-%d' % (p[0], p[-1]) for p in parts)
    print(y, '|', seg(r), '|', seg(b))
