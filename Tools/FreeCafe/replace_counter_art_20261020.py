"""Install the 2026-10 supplied counter layers without modifying either source export.
Run from any directory: python Tools/FreeCafe/replace_counter_art_20261020.py
Existing derived paths/GUIDs and sprite canvases are retained for authored scenes.
"""
from pathlib import Path
import hashlib
import json
import shutil
from datetime import datetime
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageChops

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Art/FreeCafe/Entities/台面道具'
ALTERNATE = ROOT / 'Assets/Art/FreeCafe/Entities/台面道具 (1)'
OUT = ROOT / 'Assets/Art/Gameplay/咖啡馆/Sliced'
REVIEW = ROOT / 'output/counter-art-20261020'
# Top-left PNG coordinates, not Unity's bottom-left Sprite Editor coordinates.
BOXES = {
    'source.beans': (0, 273, 457, 891),
    'source.cocoa': (479, 588, 934, 869),
    'source.matcha': (953, 456, 1182, 858),
    'source.milk': (1188, 454, 1545, 862),
    'machine.grinder': (1818, 280, 2291, 999),
    'machine.heater': (3033, 187, 3519, 984),
    'cup.large': (43, 1212, 473, 1569),
    'cup.small': (505, 1387, 860, 1634),
    'source.cream': (3193, 1315, 3585, 1564),
    'source.sugar': (3041, 1634, 3427, 1832),
}

def clean(im):
    a = np.array(im.convert('RGBA'))
    a[a[:, :, 3] <= 8] = 0  # PSD export haze, not the translucent glass interior.
    return Image.fromarray(a)

def largest(im):
    a = np.array(im)
    n, labels, stats, _ = cv2.connectedComponentsWithStats((a[:, :, 3] > 8).astype('uint8'))
    if n < 2:
        raise ValueError('Empty supplied crop')
    a[labels != 1 + np.argmax(stats[1:, 4])] = 0
    return Image.fromarray(a)

def main():
    atlas = Image.open(SOURCE / '桌面道具.png').convert('RGBA')
    other = Image.open(ALTERNATE / '桌面道具.png').convert('RGBA')
    assert atlas.size == other.size == (3585, 1832)
    layout = json.loads((OUT / 'slices.json').read_text(encoding='utf-8'))
    REVIEW.mkdir(parents=True, exist_ok=True)
    backup = REVIEW / ('backup-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir()
    saved = set()
    manifest = {'source': str(SOURCE.relative_to(ROOT)), 'alternate': str(ALTERNATE.relative_to(ROOT)), 'outputs': {}}

    def save(path, im, provenance):
        relative = str(path.relative_to(ROOT)).replace('\\', '/')
        if path.exists() and relative not in saved:
            dst = backup / path.relative_to(ROOT)
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, dst)
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(".png.tmp")
        im.save(temporary, format="PNG")
        temporary.replace(path)
        saved.add(relative)
        manifest['outputs'][relative] = {'size': list(im.size), **provenance}

    def canvas(art, piece, state='default', placement=None):
        x, y, r, b = layout[art]['box']
        size = (r - x, b - y)
        if placement is None:
            scale = min(size[0] / piece.width, size[1] / piece.height)
            scaled = (round(piece.width * scale), round(piece.height * scale))
            offset = ((size[0] - scaled[0]) // 2, (size[1] - scaled[1]) // 2)
        else:
            scaled, offset = placement
        result = Image.new('RGBA', size)
        result.alpha_composite(piece.resize(scaled, Image.Resampling.LANCZOS), offset)
        save(OUT / 'Entities' / art / (state + '.png'), result, {'state': state, 'scaleTo': list(scaled), 'offset': list(offset)})
        return result, (scaled, offset)

    for art, box in BOXES.items():
        canvas(art, largest(clean(atlas.crop(box))))
        manifest['outputs'][str((OUT / 'Entities' / art / 'default.png').relative_to(ROOT)).replace('\\', '/')]['crop'] = list(box)

    # Nested cups are foreground occlusion pieces, NOT half-filled drinks.
    overlap = clean(Image.open(SOURCE / '重叠.png'))
    for art, box, pad in [('cup.large', (0, 0, 430, 331), (0, 26)),
                          ('cup.small', (460, 107, 815, 336), (0, 18))]:
        full_box = BOXES[art]
        full = Image.new('RGBA', (full_box[2] - full_box[0], full_box[3] - full_box[1]))
        full.alpha_composite(overlap.crop(box), pad)
        canvas(art, full, 'nested')

    # Keep the established detachable upper/lower assembly offset and canvas sizes.
    upper_box = (2479, 40, 2760, 597)
    upper = clean(atlas.crop(upper_box))
    canvas('container.extractor.filter', upper)
    lower = clean(atlas.crop((2294, 533, 3016, 983)))
    ImageDraw.Draw(lower).rectangle((2479-2294, 0, 2760-2294-1, 597-533-1), fill=(0, 0, 0, 0))
    # Reject the low-alpha brush haze outside the actual jug; keep glass translucency inside.
    outline = [(2315,589),(2340,588),(2449,748),(2448,655),(2443,653),(2443,609),
               (2479,602),(2479,597),(2760,597),(2760,601),(2784,601),(2788,637),
               (2790,640),(2835,581),(2875,545),(2920,533),(2965,550),(3000,570),
               (3015,604),(3005,650),(2970,706),(2925,775),(2870,828),(2820,883),
               (2800,915),(2780,927),(2833,940),(2833,960),(2798,983),(2420,983),
               (2405,974),(2404,950),(2445,927),(2448,901),(2435,874),(2315,619)]
    mask = Image.new('L', (lower.width*3, lower.height*3))
    ImageDraw.Draw(mask).polygon([((x-2294)*3,(y-533)*3) for x,y in outline], fill=255)
    lower.putalpha(ImageChops.multiply(lower.getchannel('A'), mask.resize(lower.size, Image.Resampling.LANCZOS)))
    canvas('container.extractor', lower)

    # Separate lid and body from the explicitly split export. Remove adjacent cream.
    jar_box = (2909, 1185, 3267, 1550)
    jar = clean(atlas.crop(jar_box))
    body = jar.copy(); ImageDraw.Draw(body).rectangle((0, 0, 266, 119), fill=(0, 0, 0, 0))
    body = largest(body)
    lid = clean(atlas.crop((2914, 1185, 3171, 1304)))
    opened = body.copy(); opened.alpha_composite(lid, (5, 0))
    # The alternate export contains the author's actual CLOSED lid position.
    closed = largest(clean(other.crop(jar_box)))
    canvas('source.honey', closed)
    canvas('source.honey', opened, 'open')
    canvas('source.honey', closed, 'closed')

    # Original document viewport: background layer starts at PSD y=-301.
    bg = Image.open(SOURCE / '图层 12.png').convert('RGBA').crop((0, 301, 4096, 2605))
    assert bg.getchannel('A').getextrema() == (255, 255), 'Counter background must fill the viewport'
    save(OUT / 'Environment/counter.png', bg, {'crop': [0, 301, 4096, 2605], 'psdOffset': [0, -301]})

    # Re-cut moving grinder pieces from the NEW art, retaining the existing pivot canvases.
    grinder = Image.open(OUT / 'Entities/machine.grinder/default.png').convert('RGBA')
    parts = OUT / 'Entities/machine.grinder/Animated'
    arm = [(145,147),(239,147),(289,120),(462,120),(462,141),(299,141),(249,172),(145,172)]
    knob = [(368,0),(485,0),(485,120),(465,129),(398,131),(393,111),(367,94)]
    drawer = [(69,561),(302,557),(316,568),(315,689),(304,696),(72,695),(65,683),(66,575)]
    def part(poly, box):
        mask = Image.new('L', grinder.size); ImageDraw.Draw(mask).polygon(poly, fill=255)
        im = grinder.copy(); im.putalpha(ImageChops.multiply(im.getchannel('A'), mask))
        return im.crop(box)
    save(parts / 'crank-arm.png', part(arm, (145,120,463,176)), {'from': 'new grinder', 'part': 'crank'})
    save(parts / 'crank-knob.png', part(knob, (367,0,485,132)), {'from': 'new grinder', 'part': 'grip'})
    drawer_im = part(drawer, (65,557,317,697)).resize((252,131), Image.Resampling.LANCZOS)
    save(parts / 'drawer-closed.png', drawer_im, {'from': 'new grinder', 'part': 'drawer'})
    body = grinder.copy(); draw = ImageDraw.Draw(body)
    draw.polygon(arm, fill=0); draw.polygon(knob, fill=0)
    body.alpha_composite(grinder.crop((175,141,202,188)), (175,141))
    draw = ImageDraw.Draw(body); draw.polygon(drawer, fill=(43,30,24,255))
    save(parts / 'body.png', body, {'from': 'new grinder', 'part': 'stationary body'})
    # Ready drawer: same new wooden face with a crop of the supplied coffee beans above it.
    filled = Image.new('RGBA', (252,143))
    grain = atlas.crop((90,357,365,440)).resize((230,42), Image.Resampling.LANCZOS)
    mask = Image.new('L', grain.size); ImageDraw.Draw(mask).ellipse((0,0,229,41), fill=255)
    grain.putalpha(ImageChops.multiply(grain.getchannel('A'), mask))
    filled.alpha_composite(grain, (11,0)); filled.alpha_composite(drawer_im, (0,12))
    filled = filled.resize((689,392), Image.Resampling.LANCZOS)  # Retain the ready-drawer's authored canvas/scale.
    save(parts / 'drawer-filled.png', filled, {'from': 'new drawer + supplied bean bag interior', 'part': 'ready drawer'})

    # Evidence for both exports; never upload, edit or delete their PNGs/PSDs.
    manifest['sourceHashes'] = {str(p.relative_to(ROOT)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest()
                                for folder in (SOURCE, ALTERNATE) for p in folder.glob('*.png')}
    (REVIEW / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    preview = Image.new('RGB', (1600,1000), '#b5aaa0'); draw = ImageDraw.Draw(preview)
    art_ids = list(BOXES) + ['container.extractor.filter','container.extractor','source.honey']
    for i, art in enumerate(art_ids):
        im = Image.open(OUT / 'Entities' / art / 'default.png'); im.thumbnail((245,260))
        x=i%6*265; y=i//6*330
        preview.paste(im,(x,y+30),im); draw.text((x+3,y+6),art,fill='black')
    preview.save(REVIEW / 'contact-sheet.jpg')
    print(f'Updated {len(saved)} derived PNGs. Original exports untouched. Backup: {backup}')

if __name__ == '__main__':
    main()
