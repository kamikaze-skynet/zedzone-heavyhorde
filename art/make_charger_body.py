# The Charger's own body (replaces the zombie's skin parts). Canonical orientation: limbs vertical,
# joint at the top; the mod rotates/flips each one at runtime to match the game's part sprite.
import random, os, struct, math
from PIL import Image, ImageDraw, ImageFilter
random.seed(77)
OUT = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(OUT, '..', 'src', 'Art')
SKIN = [(26,30,34),(40,46,50),(56,64,66),(74,84,84),(96,106,102),(122,132,124),(150,158,146)]
VEIN = (58,30,44); SCAR = (120,70,72); STITCH = (20,18,16); BLOOD = [(70,10,10),(104,18,16)]
OUTL = (12,12,14)

def body_shade(mask, muscles=()):
    """Shade an 'L' mask as heavy grey flesh: round volume, light from upper left, muscle bulges."""
    w, h = mask.size; m = mask.load()
    inner = mask.filter(ImageFilter.MinFilter(3)).load()
    im = Image.new('RGBA', (w, h), (0,0,0,0)); p = im.load()
    for y in range(h):
        for x in range(w):
            if not m[x, y]: continue
            # distance-from-edge proxy across the row for roundness
            l = x; 
            while l > 0 and m[l-1, y]: l -= 1
            r = x
            while r < w-1 and m[r+1, y]: r += 1
            t = (x - l) / max(1, r - l)                     # 0..1 across the limb
            lvl = 1.4 + 3.2 * math.sin(t * math.pi) ** 0.8 - 0.9 * (t > 0.6) * (t - 0.6) * 4
            lvl -= 0.8 * y / h
            for (mx, my, mr, s) in muscles:                  # bulges
                dd = math.hypot((x - mx) / mr[0], (y - my) / mr[1])
                if dd < 1: lvl += s * (1 - dd) * (1.0 if (x - mx) < 0 or (y - my) < 0 else -0.6)
            lvl += random.uniform(-0.3, 0.3)
            if not inner[x, y]: lvl = 0.6
            p[x, y] = SKIN[max(0, min(6, int(round(lvl))))] + (255,)
    o = mask.filter(ImageFilter.MaxFilter(3)).load()
    for y in range(h):
        for x in range(w):
            if o[x, y] and not m[x, y]: p[x, y] = OUTL + (255,)
    return im

def veins(p, w, h, x, y, n):
    for _ in range(n):
        if 0 <= x < w and 0 <= y < h and p[x, y][3] and p[x, y][:3] != OUTL: p[x, y] = VEIN + (255,)
        x += random.choice((-1, 0, 1)); y += random.choice((1, 1, 0))

def stitches(p, w, h, x0, y0, x1, y1):
    n = max(abs(x1-x0), abs(y1-y0), 1)
    for k in range(n+1):
        x = round(x0+(x1-x0)*k/n); y = round(y0+(y1-y0)*k/n)
        if 0 <= x < w and 0 <= y < h and p[x, y][3]: p[x, y] = SCAR + (255,)
        if k % 3 == 0:
            for dx in (-1, 1):
                if 0 <= x+dx < w and 0 <= y-1 < h and p[x+dx, y-1][3]: p[x+dx, y-1] = STITCH + (255,)

def gore(p, w, h, cx, cy, r):
    for y in range(cy-r, cy+r+1):
        for x in range(cx-r, cx+r+1):
            if 0 <= x < w and 0 <= y < h and p[x, y][3] and p[x, y][:3] != OUTL and math.hypot(x-cx, y-cy) < r * random.uniform(0.6, 1.1):
                p[x, y] = random.choice(BLOOD) + (255,)

def strap(d, w, x0, y0, x1, y1, th=3):
    d.line((x0, y0, x1, y1), fill=(30,24,20), width=th+2)
    d.line((x0, y0, x1, y1), fill=(70,52,36), width=th)
    d.line((x0, y0-1, x1, y1-1), fill=(96,72,48), width=1)

_last = {}
def M(w, h, f):
    m = Image.new('L', (w, h), 0); f(ImageDraw.Draw(m)); _last['m'] = m; return m

def clip(im):
    keep = _last['m'].filter(ImageFilter.MaxFilter(3))
    a = Image.composite(im.getchannel('A'), Image.new('L', im.size, 0), keep)
    im.putalpha(a); return im

def torso():
    w, h = 46, 60
    m = M(w, h, lambda d: d.polygon([(0, 10), (10, 2), (w//2-6, 0), (w//2+6, 0), (w-11, 2), (w-1, 10), (w-4, 28), (w-11, h-4), (w//2+6, h-1), (w//2-6, h-1), (10, h-4), (3, 28)], fill=255))
    im = body_shade(m, muscles=[(13, 15, (11, 9), 1.8), (w-14, 15, (11, 9), 1.5), (w//2-6, 40, (7, 12), 0.9), (w//2+6, 40, (7, 12), 0.7)])
    p = im.load(); d = ImageDraw.Draw(im)
    d.arc((4, 6, w//2, 26), 20, 160, fill=SKIN[1]); d.arc((w//2, 6, w-5, 26), 20, 160, fill=SKIN[1])   # pec lines
    d.line((w//2, 8, w//2, h-6), fill=SKIN[1])                      # sternum line
    for y in (32, 39, 46): d.line((w//2-8, y, w//2+8, y), fill=SKIN[2])  # abs
    stitches(p, w, h, 8, 20, 22, 34); stitches(p, w, h, w-10, 40, w-24, 50)
    veins(p, w, h, 30, 6, 14); veins(p, w, h, 10, 30, 10)
    strap(d, w, 3, 6, w-4, h-12, 4)                                  # harness straps (hold the armour on)
    strap(d, w, w-4, 6, 3, h-12, 4)
    d.ellipse((w//2-3, 22, w//2+3, 28), fill=(140,140,144), outline=OUTL)  # buckle
    gore(p, w, h, 34, 52, 3)
    return clip(im)

def upper_arm():
    w, h = 22, 38
    m = M(w, h, lambda d: d.ellipse((1, 0, w-2, h-1), fill=255))
    im = body_shade(m, muscles=[(8, 14, (7, 10), 1.6)])
    p = im.load(); d = ImageDraw.Draw(im)
    veins(p, w, h, 14, 8, 18); stitches(p, w, h, 4, 26, 16, 30)
    strap(d, w, 1, 22, w-2, 24, 3)
    return clip(im)

def forearm():
    w, h = 22, 40
    m = M(w, h, lambda d: (d.rounded_rectangle((3, 0, w-4, h-12), 6, fill=255), d.ellipse((0, h-17, w-1, h-1), fill=255)))  # big fist
    im = body_shade(m, muscles=[(8, 10, (6, 9), 1.2)])
    p = im.load(); d = ImageDraw.Draw(im)
    for k in range(4): d.line((3 + k*4, h-14, 3 + k*4, h-10), fill=SKIN[1])  # knuckles
    for k in range(4): d.point((4 + k*4, h-15), fill=SKIN[5])
    veins(p, w, h, 12, 4, 16); gore(p, w, h, 9, h-6, 3)
    return clip(im)

def thigh():
    w, h = 24, 38
    m = M(w, h, lambda d: d.ellipse((1, 0, w-2, h-1), fill=255))
    im = body_shade(m, muscles=[(9, 14, (8, 11), 1.3)])
    p = im.load(); d = ImageDraw.Draw(im)
    d.rectangle((1, 0, w-2, 13), fill=(44,38,32)); d.line((1, 13, w-2, 13), fill=OUTL)  # torn shorts
    for x in range(2, w-2, 3): d.line((x, 13, x + random.choice((-1, 0, 1)), 13 + random.randint(1, 4)), fill=(44,38,32))
    stitches(p, w, h, 6, 22, 17, 30)
    return clip(im)

def shin():
    w, h = 20, 38
    m = M(w, h, lambda d: d.rounded_rectangle((2, 0, w-3, h-1), 7, fill=255))
    im = body_shade(m, muscles=[(8, 12, (6, 9), 1.2)])
    p = im.load(); veins(p, w, h, 11, 6, 18)
    return clip(im)

def foot():
    w, h = 28, 14
    m = M(w, h, lambda d: d.rounded_rectangle((0, 1, w-1, h-1), 5, fill=255))
    im = body_shade(m)
    d = ImageDraw.Draw(im)
    d.rectangle((0, h-4, w-1, h-1), fill=(30,26,22))                  # wrapped rag sole
    for x in range(3, w-3, 5): d.line((x, 3, x+2, h-4), fill=(80,62,44))
    return clip(im)

parts = {'charger_body_torso': torso(), 'charger_body_upperarm': upper_arm(), 'charger_body_forearm': forearm(),
         'charger_body_thigh': thigh(), 'charger_body_shin': shin(), 'charger_body_foot': foot()}
for name, im in parts.items():
    im.save(os.path.join(OUT, name + '.png'))
    with open(os.path.join(RAW, name + '.rgba'), 'wb') as f:
        f.write(struct.pack('<ii', im.width, im.height)); f.write(im.transpose(Image.FLIP_TOP_BOTTOM).tobytes())
pad, S = 10, 6
W = sum(i.width for i in parts.values())*S + pad*(len(parts)+1); H = max(i.height for i in parts.values())*S + pad*2
sheet = Image.new('RGBA', (W, H), (24,22,20,255)); x = pad
for i in parts.values():
    big = i.resize((i.width*S, i.height*S), Image.NEAREST); sheet.alpha_composite(big, (x, pad)); x += big.width + pad
sheet.save(os.path.join(OUT, 'charger_body_sheet.png'))
