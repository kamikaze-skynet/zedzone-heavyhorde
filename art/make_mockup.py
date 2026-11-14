# Rough full-body mockup of the Charger: a stand-in zombie with the armour plates on it (two walk frames).
import random, math, sys
from PIL import Image, ImageDraw
random.seed(5)
A = __file__.rsplit('/', 1)[0] + '/'
L = lambda n: Image.open(A + 'charger_' + n + '.png').convert('RGBA')
helm, chest, paul, brac, grea = [L(n) for n in ('helmet', 'chest', 'pauldron', 'bracer', 'greave')]
SK = [(46,58,44),(64,80,58),(84,102,74),(104,124,90)]; CL = [(34,28,24),(48,40,34),(62,52,42)]
W, H = 120, 160
def seg(d, a, b, w, col, dark):
    d.line((a, b), fill=dark, width=w+2); d.line((a, b), fill=col, width=w)
    for q in (a, b): d.ellipse((q[0]-w//2, q[1]-w//2, q[0]+w//2, q[1]+w//2), fill=col)
def put(im, sp, cx, cy, wd, ht, rot=0):
    s = sp.rotate(rot, expand=True, resample=Image.NEAREST).resize((wd, ht), Image.NEAREST); im.alpha_composite(s, (int(cx-wd/2), int(cy-ht/2)))
def frame(t):
    im = Image.new('RGBA', (W, H), (0,0,0,0)); d = ImageDraw.Draw(im)
    cx = 56; sw = math.sin(t) * 6
    hip = (cx, 104); sh = (cx+6, 50)
    k1 = (cx-6-sw, 126); f1 = (cx-8-sw*1.4, 150); seg(d, hip, k1, 14, CL[0], (10,8,8)); seg(d, k1, f1, 9, CL[0], (10,8,8))
    e1 = (cx-4, 76+sw*0.3); hnd1 = (cx+6, 96); seg(d, (sh[0]-10, sh[1]+2), e1, 13, SK[0], (10,12,10)); seg(d, e1, hnd1, 12, SK[0], (10,12,10))
    d.ellipse((cx-22, 98, cx+30, 112), fill=(18,16,14))
    d.polygon([(cx-24,46),(cx+28,42),(cx+22,106),(cx-18,108)], fill=CL[1], outline=(10,8,8))
    for yy in range(40, 50, 3):
        for xx in range(cx+4, cx+24, 3): d.ellipse((xx, yy, xx+3, yy+3), outline=(70,72,76))  # chain gorget
    d.ellipse((cx+6,20,cx+36,52), fill=SK[2], outline=(10,12,10))
    k2 = (cx+6+sw, 126); f2 = (cx+8+sw*1.4, 150); seg(d, hip, k2, 14, CL[2], (10,8,8)); seg(d, k2, f2, 9, CL[2], (10,8,8))
    e2 = (cx+30, 66-sw*0.3); hnd2 = (cx+54, 68); seg(d, (sh[0]+12, sh[1]), e2, 13, SK[2], (10,12,10)); seg(d, e2, hnd2, 12, SK[2], (10,12,10))
    d.ellipse((hnd2[0]-3, hnd2[1]-6, hnd2[0]+9, hnd2[1]+6), fill=SK[1], outline=(10,12,10))
    for i in range(3): d.line((hnd2[0]+6, hnd2[1]-4+i*4, hnd2[0]+12, hnd2[1]-3+i*4), fill=SK[0], width=2)
    put(im, grea, (k1[0]+f1[0])/2, (k1[1]+f1[1])/2, 13, 26)
    put(im, brac, (e1[0]+hnd1[0])/2, (e1[1]+hnd1[1])/2, 11, 22, -25)
    put(im, chest, cx+3, 76, 50, 62, -6)
    put(im, paul, cx-12, 48, 28, 26, 10)
    put(im, helm, cx+21, 30, 44, 30, -6)
    put(im, grea, (k2[0]+f2[0])/2, (k2[1]+f2[1])/2, 14, 28)
    put(im, paul, (hip[0]+k2[0])/2+2, (hip[1]+k2[1])/2, 16, 16)
    put(im, paul, cx+20, 50, 30, 28, -12)
    put(im, brac, (e2[0]+hnd2[0])/2, (e2[1]+hnd2[1])/2, 24, 12, 90)
    return im
bg = Image.new('RGBA', (W*2+40, H+16), (26,24,22,255)); d = ImageDraw.Draw(bg)
d.rectangle((0, H+2, bg.width, bg.height), fill=(38,34,30))
for x in range(0, bg.width, 3): d.point((x, H+2+random.randint(0, 12)), fill=(50,46,40))
bg.alpha_composite(frame(math.pi/2), (10, 6)); bg.alpha_composite(frame(-math.pi/2), (W+30, 6))
bg.resize((bg.width*4, bg.height*4), Image.NEAREST).save(sys.argv[1] if len(sys.argv) > 1 else A + 'charger_mockup.png')
