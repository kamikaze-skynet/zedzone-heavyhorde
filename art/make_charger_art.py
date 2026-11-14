# Charger armour plates (pixel art) -> art/*.png and src/Art/*.rgba (raw RGBA, bottom row first, for Unity).
# Each plate is a filled shape, shaded like bent steel: light from the upper left, a bright bevel on
# lit edges, a dark bevel on shadowed ones, a soft highlight band, dents, weld seams, rivets and rust
# that runs down from rivets and seams.
import random, os, struct, math
from PIL import Image, ImageDraw, ImageFilter
random.seed(20261114)
OUT = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(OUT, '..', 'src', 'Art'); os.makedirs(RAW, exist_ok=True)

STEEL = [(24,26,30),(38,41,46),(54,58,64),(72,77,84),(94,100,108),(122,129,138),(158,165,174),(204,210,216)]
RUST  = [(64,30,18),(98,46,24),(132,66,30),(168,92,40),(196,124,58)]
HAZ_Y, HAZ_K = (214,170,40), (30,28,26)
RED = [(120,10,8),(200,30,16),(255,80,40),(255,170,120)]

def shade(mask, curve=1.0, band=0.35):
    """Shade a filled 'L' mask as a bent steel plate."""
    w, h = mask.size
    m = mask.load()
    dist = mask.filter(ImageFilter.MinFilter(3))
    d2 = dist.filter(ImageFilter.MinFilter(3))
    im = Image.new('RGBA', (w, h), (0,0,0,0)); p = im.load()
    a, b = dist.load(), d2.load()
    for y in range(h):
        for x in range(w):
            if not m[x, y]: continue
            u, v = x / max(1, w-1), y / max(1, h-1)
            # cylinder-ish curvature across x, light from the upper left
            n = math.cos((u - 0.38) * math.pi * curve)
            lvl = 1.5 + 2.9 * n - 1.4 * v
            if abs(u - band) < 0.06: lvl += 1.2           # specular band
            lvl += random.uniform(-0.35, 0.35)            # hammered texture
            if not a[x, y]:                               # outer edge
                lit = (x < w * 0.5) or (y < h * 0.3)
                lvl = 6.2 if (lit and y < h*0.7) else 0.4
            elif not b[x, y]:
                lvl -= 0.8
            p[x, y] = STEEL[max(0, min(7, int(round(lvl))))] + (255,)
    # 1-px dark outline
    out = mask.filter(ImageFilter.MaxFilter(3))
    o = out.load()
    for y in range(h):
        for x in range(w):
            if o[x, y] and not m[x, y]: p[x, y] = (14,14,16,255)
    return im

def rivet(p, x, y, w, h, drip=True):
    for dx, dy, c in ((0,0,STEEL[6]),(1,0,STEEL[4]),(0,1,STEEL[3]),(1,1,STEEL[1]),(-1,0,STEEL[2]),(0,-1,STEEL[5])):
        if 0 <= x+dx < w and 0 <= y+dy < h and p[x+dx, y+dy][3]: p[x+dx, y+dy] = c + (255,)
    if drip:
        L = random.randint(2, 7)
        for k in range(2, 2+L):
            if y+k < h and p[x, y+k][3]: p[x, y+k] = RUST[max(0, 3 - k*3//L)] + (255,)

def rust_patch(p, w, h, cx, cy, r):
    for y in range(cy-r, cy+r+1):
        for x in range(cx-r, cx+r+1):
            if 0 <= x < w and 0 <= y < h and p[x, y][3] and p[x, y][:3] != (14,14,16):
                dd = math.hypot(x-cx, y-cy) / max(1, r)
                if dd < 1 and random.random() < 1.1 - dd:
                    p[x, y] = RUST[min(4, int((1-dd)*4 + random.random()))] + (255,)

def dent(p, w, h, cx, cy):
    for (dx, dy, c) in ((0,0,STEEL[1]),(1,0,STEEL[2]),(-1,-1,STEEL[6]),(0,-1,STEEL[5]),(1,1,STEEL[2])):
        x, y = cx+dx, cy+dy
        if 0 <= x < w and 0 <= y < h and p[x, y][3]: p[x, y] = c + (255,)

def weld(p, w, h, pts):
    for i in range(len(pts)-1):
        (x0, y0), (x1, y1) = pts[i], pts[i+1]
        n = max(abs(x1-x0), abs(y1-y0), 1)
        for k in range(n+1):
            x = round(x0 + (x1-x0)*k/n); y = round(y0 + (y1-y0)*k/n)
            if 0 <= x < w and 0 <= y < h and p[x, y][3]:
                p[x, y] = (STEEL[6] if k % 2 else STEEL[1]) + (255,)
                if y+1 < h and p[x, y+1][3]: p[x, y+1] = STEEL[1] + (255,)

def mask_of(w, h, draw):
    m = Image.new('L', (w, h), 0); draw(ImageDraw.Draw(m)); return m

def helmet():
    w, h = 44, 46
    m = mask_of(w, h, lambda d: (d.ellipse((3, 2, w-4, 40), fill=255), d.rectangle((5, 22, w-6, h-3), fill=255),
                                  d.polygon([(w//2-5, 0), (w//2+5, 0), (w//2+3, 14), (w//2-3, 14)], fill=255)))  # crest ridge
    im = shade(m, curve=1.2); p = im.load(); d = ImageDraw.Draw(im)
    weld(p, w, h, [(w//2, 2), (w//2, 18)])
    # visor: deep slit with glowing eyes
    d.polygon([(7, 19), (w-8, 19), (w-10, 25), (9, 25)], fill=(8, 6, 6))
    for x in range(10, w-10):
        g = RED[1] if x % 2 else RED[0]
        if 12 <= x <= 16 or w-17 <= x <= w-13: g = RED[3] if x in (14, w-15) else RED[2]
        p[x, 21] = g + (255,); p[x, 22] = (RED[1] if g != RED[0] else RED[0]) + (255,)
    # brow ridge over the slit
    d.line((7, 18, w-8, 18), fill=STEEL[6]); d.line((8, 17, w-9, 17), fill=STEEL[4])
    # jaw cage
    d.rectangle((6, 28, w-7, h-4), fill=STEEL[1], outline=(14,14,16))
    for x in range(9, w-8, 4):
        d.line((x, 30, x, h-6), fill=STEEL[4]); d.line((x+1, 30, x+1, h-6), fill=STEEL[0])
    d.line((7, 29, w-8, 29), fill=STEEL[5])
    for x in (8, w-9):
        for y in (8, 31, h-7): rivet(p, x, y, w, h)
    rust_patch(p, w, h, 34, 10, 4); rust_patch(p, w, h, 10, 38, 3)
    dent(p, w, h, 15, 9); dent(p, w, h, 30, 13)
    return im

def chest():
    w, h = 52, 62
    # a heavy ram plate: wide shoulders, tapered waist, thick prow down the middle
    m = mask_of(w, h, lambda d: d.polygon([(2, 6), (10, 1), (w-11, 1), (w-3, 6), (w-6, h-14), (w//2+8, h-2),
                                            (w//2-8, h-2), (5, h-14)], fill=255))
    im = shade(m, curve=1.0, band=0.3); p = im.load(); d = ImageDraw.Draw(im)
    # central prow (raised ridge)
    for y in range(4, h-3):
        x = w // 2
        p[x-1, y] = STEEL[6] + (255,); p[x, y] = STEEL[5] + (255,); p[x+1, y] = STEEL[1] + (255,)
    # overlapping lower plates (lames)
    for y0 in (h-24, h-15):
        d.line((6, y0, w-7, y0), fill=(14,14,16)); d.line((6, y0+1, w-7, y0+1), fill=STEEL[6])
    # hazard stripe band across the upper chest
    top, bot = 14, 21
    for y in range(top, bot+1):
        for x in range(3, w-3):
            if p[x, y][3] and p[x, y][:3] != (14,14,16) and abs(x - w//2) > 2:
                p[x, y] = (HAZ_Y if ((x + y) // 4) % 2 == 0 else HAZ_K) + (255,)
    d.line((3, top-1, w-4, top-1), fill=(14,14,16)); d.line((3, bot+1, w-4, bot+1), fill=(14,14,16))
    # chipped paint
    for _ in range(14):
        x, y = random.randint(4, w-5), random.randint(top, bot)
        if p[x, y][:3] == HAZ_Y: p[x, y] = STEEL[3] + (255,)
    weld(p, w, h, [(8, 30), (18, 33), (w//2-2, 31)])
    weld(p, w, h, [(w//2+2, 40), (w-12, 37)])
    for (x, y) in ((6, 6), (w-7, 6), (6, 27), (w-8, 27), (10, h-18), (w-11, h-18), (w//2-6, h-6), (w//2+5, h-6)):
        rivet(p, x, y, w, h)
    rust_patch(p, w, h, 14, 44, 5); rust_patch(p, w, h, 38, 28, 4); rust_patch(p, w, h, 30, 52, 3)
    for c in ((12, 36), (40, 46), (20, 52), (33, 9)): dent(p, w, h, *c)
    # blood smear
    for _ in range(18):
        x, y = random.randint(28, 44), random.randint(40, 50)
        if p[x, y][3] and p[x, y][:3] != (14,14,16): p[x, y] = random.choice([(92,14,12), (120,20,16), (70,10,10)]) + (255,)
    return im

def pauldron():
    w, h = 34, 34
    m = mask_of(w, h, lambda d: (d.ellipse((1, 7, w-2, 40), fill=255), d.rectangle((0, h-1, w, h), fill=0)))
    base = shade(m, curve=1.3)
    im = Image.new('RGBA', (w, h+0), (0,0,0,0)); im.alpha_composite(base)
    p = im.load(); d = ImageDraw.Draw(im)
    # three spikes along the top
    for sx in (8, 17, 26):
        d.polygon([(sx-4, 12), (sx+4, 12), (sx, 0)], fill=STEEL[2], outline=(14,14,16))
        d.line((sx-2, 11, sx, 2), fill=STEEL[5]); d.line((sx+1, 11, sx+1, 5), fill=STEEL[0])
    # banded lames
    for y0 in (20, 26):
        d.line((3, y0, w-4, y0), fill=(14,14,16)); d.line((3, y0+1, w-4, y0+1), fill=STEEL[5])
    for x in (5, w-6): rivet(p, x, 23, w, h)
    rust_patch(p, w, h, 22, 15, 3); dent(p, w, h, 11, 16)
    return im

def bracer():
    w, h = 22, 40
    m = mask_of(w, h, lambda d: d.rounded_rectangle((1, 1, w-2, h-2), 5, fill=255))
    im = shade(m, curve=1.4); p = im.load(); d = ImageDraw.Draw(im)
    for y0 in (10, 20, 30):
        d.line((2, y0, w-3, y0), fill=(14,14,16)); d.line((2, y0+1, w-3, y0+1), fill=STEEL[5])
    # knuckle studs on the end
    for x in (6, 11, 16): rivet(p, x, h-5, w, h, drip=False)
    rivet(p, 4, 5, w, h); rivet(p, w-5, 15, w, h)
    rust_patch(p, w, h, 14, 25, 3)
    return im

def greave():
    w, h = 24, 46
    m = mask_of(w, h, lambda d: d.polygon([(2, 4), (w//2, 0), (w-3, 4), (w-3, h-8), (w//2, h-2), (2, h-8)], fill=255))
    im = shade(m, curve=1.3); p = im.load(); d = ImageDraw.Draw(im)
    # knee cop
    d.ellipse((4, 3, w-5, 15), fill=STEEL[4], outline=(14,14,16)); d.arc((5, 4, w-6, 14), 200, 320, fill=STEEL[7])
    d.polygon([(w//2-2, 6), (w//2+2, 6), (w//2, -1)], fill=STEEL[3])
    # shin ridge
    for y in range(16, h-6):
        p[w//2, y] = STEEL[6] + (255,); p[w//2+1, y] = STEEL[1] + (255,)
    for y in (20, 32): rivet(p, 4, y, w, h); rivet(p, w-5, y, w, h)
    rust_patch(p, w, h, 8, 36, 3); dent(p, w, h, 16, 26)
    return im

parts = {'charger_helmet': helmet(), 'charger_chest': chest(), 'charger_pauldron': pauldron(),
         'charger_bracer': bracer(), 'charger_greave': greave()}
for name, im in parts.items():
    im.save(os.path.join(OUT, name + '.png'))
    with open(os.path.join(RAW, name + '.rgba'), 'wb') as f:
        f.write(struct.pack('<ii', im.width, im.height)); f.write(im.transpose(Image.FLIP_TOP_BOTTOM).tobytes())
pad, S = 10, 6
W = sum(i.width for i in parts.values())*S + pad*(len(parts)+1); H = max(i.height for i in parts.values())*S + pad*2
sheet = Image.new('RGBA', (W, H), (24,22,20,255)); x = pad
for i in parts.values():
    big = i.resize((i.width*S, i.height*S), Image.NEAREST); sheet.alpha_composite(big, (x, pad)); x += big.width + pad
sheet.save(os.path.join(OUT, 'charger_sheet.png'))
