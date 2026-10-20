"""Prepare a two-thirds-body neutral black placeholder by editing the existing CH35 alpha.
Local image edits only. Both API edits failed to return an image (2026-10-20).
Preserve the colored original and old tutorial silhouette; do not claim new colored art.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
from PIL import Image, ImageChops, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Art/NarrativeGenerated/Characters/CH35_tutorial_elderly_woman.png'
DEST = ROOT / 'Assets/Resources/Characters/generic'
WORK = ROOT / 'output/npc-art-2026-10-20'


def write_sprite(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        template = (ROOT / 'Assets/Art/NarrativeGenerated/Characters/CH35_tutorial_elderly_woman_silhouette.png.meta').read_text(encoding='utf-8')
        template = re.sub(r'^guid: .*$', 'guid: ' + uuid.uuid4().hex, template, flags=re.M)
        template = template.replace('CH35_tutorial_elderly_woman_silhouette_0', path.stem + '_0')
        meta.write_text(template, encoding='utf-8')


def prepare():
    source = Image.open(SOURCE).convert('RGBA')
    # Edit the source contour: retain the face/torso/arms, trim the bonnet/bun,
    # straighten the outer garment and crop before the lower skirt and feet.
    # Four-times masks retain antialiasing around the edited head and coat.
    scale = 4
    head = Image.new('L', (source.width * scale, source.height * scale))
    ImageDraw.Draw(head).ellipse(tuple(int(v * scale) for v in (446, 63, 627, 253)), fill=255)
    body = Image.new('L', head.size)
    contour = [(472,245),(553,245),(553,278),(588,298),(620,334),(638,405),
               (651,451),(677,501),(701,577),(723,620),(710,665),(700,700),
               (700,1060),(390,1060),(390,703),(381,674),(378,590),(365,548),
               (377,505),(371,470),(379,398),(385,348),(410,315),(438,293),(468,277)]
    ImageDraw.Draw(body).polygon([(int(x*scale), int(y*scale)) for x,y in contour], fill=255)
    mask = ImageChops.lighter(head, body).resize(source.size, Image.Resampling.LANCZOS)
    alpha = ImageChops.multiply(source.getchannel('A'), mask)
    edited = Image.new('RGBA', source.size)
    edited.putalpha(alpha)
    edited = edited.crop((330, 45, 755, 1060))
    edited = ImageOps.contain(edited, (728,1496), Image.Resampling.LANCZOS)
    portrait = Image.new('RGBA',(768,1536))
    portrait.alpha_composite(edited, ((768-edited.width)//2,1536-edited.height-20))
    write_sprite(portrait, DEST/'neutral_placeholder.png')
    # The avatar is a head/shoulder crop of the same edited portrait.
    bounds = portrait.getchannel('A').getbbox()
    shape = portrait.crop(bounds)
    head_crop = ImageOps.contain(shape.crop((0,0,shape.width,int(shape.height*.39))), (480,480), Image.Resampling.LANCZOS)
    avatar = Image.new('RGBA',(512,512)); avatar.alpha_composite(head_crop,((512-head_crop.width)//2,(512-head_crop.height)//2))
    write_sprite(avatar, DEST/'avatar_placeholder.png')
    # Cafe fallback is the actual existing seated shadow, independent of dialogue art.
    seated = Image.open(ROOT/'Assets/Art/FreeCafe/Seating/Characters/silhouette_seated.png').convert('RGBA')
    seated = seated.crop(seated.getchannel('A').getbbox())
    seated = ImageOps.contain(seated,(728,984),Image.Resampling.LANCZOS)
    black = Image.new('RGBA',seated.size); black.putalpha(seated.getchannel('A'))
    indoor = Image.new('RGBA',(768,1024)); indoor.alpha_composite(black,((768-black.width)//2,1024-black.height-20))
    write_sprite(indoor,DEST/'seated_placeholder.png')
    report = {'source': SOURCE.relative_to(ROOT).as_posix(), 'sourceSha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
              'method':'local-alpha-contour-edit-and-two-thirds-crop', 'coloredNeutralImageGenerated':False,
              'apiAttempts':[{'model':'gpt-image-2.5-sunburst','result':'HTTP 503, no available channel'},
                             {'model':'gpt-image-2','result':'request failed, no saved image; acceptance unknown; no retry'}],
              'outputs':[p.relative_to(ROOT).as_posix() for p in sorted(DEST.glob('*.png'))]}
    WORK.mkdir(parents=True,exist_ok=True)
    (WORK/'image-preparation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__ == '__main__':
    prepare()
