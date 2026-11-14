# Full-body mockup of the Charger with its own body parts and armour on a simple skeleton (two walk frames).
import math, random, sys
from PIL import Image, ImageDraw
random.seed(5)
A = __file__.rsplit('/', 1)[0] + '/'
L = lambda n: Image.open(A + 'charger_' + n + '.png').convert('RGBA')
B = {n: L('body_' + n) for n in ('torso', 'upperarm', 'forearm', 'thigh', 'shin', 'foot')}
P = {n: L(n) for n in ('helmet', 'chest', 'pauldron', 'bracer', 'greave')}
W, H = 130, 170

def along(im, sp, a, b, width_scale=1.0, cover=1.0):
    """Draw sprite sp (upright, joint at top) from point a to point b."""
    ax, ay = a; bx, by = b
    length = math.hypot(bx-ax, by-ay)
    s = length / sp.height * cover
    w = max(1, int(sp.width * s * width_scale)); h = max(1, int(sp.height * s))
    sc = sp.resize((w, h), Image.NEAREST)
    ang = math.degrees(math.atan2(bx-ax, by-ay))       # 0 = pointing down
    rot = sc.rotate(ang, expand=True, resample=Image.NEAREST)
    mx, my = (ax+bx)/2, (ay+by)/2
    im.alpha_composite(rot, (int(mx - rot.width/2), int(my - rot.height/2)))

def at(im, sp, cx, cy, w, h, rot=0):
    s = sp.resize((w, h), Image.NEAREST).rotate(rot, expand=True, resample=Image.NEAREST)
    im.alpha_composite(s, (int(cx - s.width/2), int(cy - s.height/2)))

def frame(t):
    im = Image.new('RGBA', (W, H), (0,0,0,0)); d = ImageDraw.Draw(im)
    sw = math.sin(t)
    hip = (60, 108); shL = (46, 54); shR = (78, 50)
    # ground shadow
    d.ellipse((24, 156, 104, 166), fill=(16,14,12,255))
    # back leg
    k1 = (54 - 8*sw, 132); f1 = (52 - 12*sw, 154)
    along(im, B['thigh'], (hip[0]-6, hip[1]), k1, 1.15); along(im, B['shin'], k1, f1, 1.1)
    at(im, B['foot'], f1[0]+5, f1[1]+3, 20, 10)
    along(im, P['greave'], k1, f1, 0.95, 0.95)
    # back arm (hanging, swinging)
    e1 = (40 + 6*sw, 80); h1 = (44 + 10*sw, 104)
    along(im, B['upperarm'], shL, e1, 1.2); along(im, B['forearm'], e1, h1, 1.2)
    along(im, P['bracer'], e1, (e1[0]+(h1[0]-e1[0])*0.7, e1[1]+(h1[1]-e1[1])*0.7), 1.0)
    # torso (hunched forward a little)
    at(im, B['torso'], 62, 80, 50, 64, -8)
    at(im, P['chest'], 63, 82, 46, 56, -8)
    # head/helmet
    at(im, P['helmet'], 74, 32, 52, 38, -10)
    # front leg
    k2 = (66 + 8*sw, 132); f2 = (70 + 12*sw, 154)
    along(im, B['thigh'], (hip[0]+6, hip[1]), k2, 1.15); along(im, B['shin'], k2, f2, 1.1)
    at(im, B['foot'], f2[0]+5, f2[1]+3, 22, 11)
    along(im, P['greave'], k2, f2, 0.95, 0.95)
    at(im, P['pauldron'], (hip[0]+6+k2[0])/2, (hip[1]+k2[1])/2 - 2, 18, 16)
    # back shoulder plate, front arm (reaching), front shoulder plate
    at(im, P['pauldron'], shL[0]-2, shL[1]-2, 28, 26, 12)
    e2 = (98, 62 - 2*sw); h2 = (118, 66 - 2*sw)
    along(im, B['upperarm'], shR, e2, 1.25); along(im, B['forearm'], e2, h2, 1.25)
    along(im, P['bracer'], e2, (e2[0]+(h2[0]-e2[0])*0.7, e2[1]+(h2[1]-e2[1])*0.7), 1.05)
    at(im, P['pauldron'], shR[0]+8, shR[1]+6, 28, 26, -14)
    return im

bg = Image.new('RGBA', (W*2+30, H+10), (26,24,22,255)); d = ImageDraw.Draw(bg)
d.rectangle((0, H-8, bg.width, bg.height), fill=(38,34,30))
for x in range(0, bg.width, 3): d.point((x, H-8+random.randint(0, 16)), fill=(50,46,40))
bg.alpha_composite(frame(math.pi/2), (8, 2)); bg.alpha_composite(frame(-math.pi/2), (W+22, 2))
bg.resize((bg.width*4, bg.height*4), Image.NEAREST).save(sys.argv[1] if len(sys.argv) > 1 else A + 'charger_mockup.png')
