# Draws the Charger's armour plates (pixel art) -> art/*.png and src/Art/*.rgba (raw RGBA, bottom row first, for Unity).
import random, os, struct
from PIL import Image, ImageDraw
random.seed(1337)
OUT = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(OUT, '..', 'src', 'Art'); os.makedirs(RAW, exist_ok=True)
IRON = [(52,54,58),(66,68,72),(82,84,88),(100,101,104)]
RUST = [(96,52,30),(122,66,34),(148,84,40)]
EDGE = (28,28,30)
def plate(w, h, rounded=4, hazard=False):
    im = Image.new('RGBA', (w, h), (0,0,0,0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle((0,0,w-1,h-1), rounded, fill=IRON[1], outline=EDGE)
    # hammered noise + rust blotches
    for _ in range(w*h//6):
        x,y = random.randrange(1,w-1), random.randrange(1,h-1)
        if im.getpixel((x,y))[3]: im.putpixel((x,y), random.choice(IRON))
    for _ in range(max(2,w*h//300)):
        cx,cy,r = random.randrange(w), random.randrange(h), random.randint(2,5)
        for y in range(cy-r,cy+r+1):
            for x in range(cx-r,cx+r+1):
                if 0<x<w-1 and 0<y<h-1 and (x-cx)**2+(y-cy)**2<=r*r and random.random()<0.7 and im.getpixel((x,y))[3]:
                    im.putpixel((x,y), random.choice(RUST))
    # rust streaks running down from rivets
    if hazard:
        for i in range(-h, w, 8):
            d.polygon([(i, h*0.62), (i+4, h*0.62), (i+4+6, h*0.78), (i+6, h*0.78)], fill=(186,150,40))
        d.rectangle((0,int(h*0.62),w-1,int(h*0.62)), fill=EDGE); d.rectangle((0,int(h*0.78),w-1,int(h*0.78)), fill=EDGE)
    # highlight top edge, shadow bottom edge
    d.line((2,1,w-3,1), fill=(128,130,134)); d.line((2,h-2,w-3,h-2), fill=(38,38,40))
    # rivets
    for x in (3, w-4):
        for y in range(3, h-2, max(6, h//4)):
            d.point((x,y), fill=(150,150,150)); d.point((x+1,y+1), fill=EDGE)
            for k in range(1, random.randint(2,6)):
                if y+1+k < h-2 and random.random()<0.8: d.point((x, y+1+k), fill=RUST[0])
    # mask back to rounded shape
    mask = Image.new('L', (w,h), 0); ImageDraw.Draw(mask).rounded_rectangle((0,0,w-1,h-1), rounded, fill=255)
    im.putalpha(Image.composite(im.getchannel('A'), mask, mask))
    return im
def helmet():
    w,h = 40,40
    im = Image.new('RGBA',(w,h),(0,0,0,0)); d = ImageDraw.Draw(im)
    d.ellipse((2,2,w-3,h+12), fill=IRON[1], outline=EDGE)            # dome
    base = plate(w, h, 10)
    im = Image.composite(base, im, im.getchannel('A')); d = ImageDraw.Draw(im)
    d.rectangle((6,17,w-7,22), fill=(14,12,12))                       # visor slit
    for x in range(8, w-8, 2): d.point((x,19), fill=(255,60,30)); d.point((x,20), fill=(200,30,20))
    d.line((w//2,3,w//2,15), fill=(40,40,44)); d.line((w//2+1,3,w//2+1,15), fill=(110,110,114))  # weld seam
    for x in range(6, w-6, 6): d.point((x,27), fill=(150,150,150))   # jaw rivets
    d.rectangle((4,30,w-5,h-1), fill=IRON[0], outline=EDGE)            # jaw guard
    for x in range(6, w-6, 3): d.line((x,32,x,h-3), fill=EDGE)        # grille
    return im
parts = {
    'charger_helmet': helmet(),
    'charger_chest': plate(44, 56, 6, hazard=True),
    'charger_pauldron': plate(26, 34, 8),
    'charger_bracer': plate(20, 36, 4),
    'charger_greave': plate(22, 40, 4),
}
for name, im in parts.items():
    im.save(os.path.join(OUT, name + '.png'))
    flipped = im.transpose(Image.FLIP_TOP_BOTTOM)   # Unity textures start at the bottom row
    with open(os.path.join(RAW, name + '.rgba'), 'wb') as f:
        f.write(struct.pack('<ii', im.width, im.height)); f.write(flipped.tobytes())
# preview sheet at 6x on a dark background
pad = 10; S = 6
W = sum(im.width for im in parts.values())*S + pad*(len(parts)+1)
H = max(im.height for im in parts.values())*S + pad*2
sheet = Image.new('RGBA', (W, H), (24,22,20,255)); x = pad
for im in parts.values():
    big = im.resize((im.width*S, im.height*S), Image.NEAREST); sheet.alpha_composite(big, (x, pad)); x += big.width + pad
sheet.save(os.path.join(OUT, 'charger_sheet.png'))
