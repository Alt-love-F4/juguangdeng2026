"""Crop user-supplied silhouettes; no AI/API calls or original-file edits.
Dialogue keeps top 2/3 of the source alpha bounds. Seated art retains the COMPLETE
source figure, including lower legs/feet, with the existing cafe scale/pivot.
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = json.loads((ROOT/'Tools/Characters/silhouette-art-manifest.json').read_text(encoding='utf-8'))
SOURCE = ROOT / MANIFEST['sourceDirectory']
DEST = ROOT / 'Assets/Resources/Characters/silhouettes'
FEMALE_IDS = set(MANIFEST['femaleIds'])


def appearance(character_id):
    return 'female' if character_id in FEMALE_IDS else 'male'


def sprite_path(character_id, kind):
    return 'Characters/silhouettes/' + appearance(character_id) + '/' + kind + '_placeholder'


def source_art(sex, pose):
    name = ('女' if sex == 'female' else '男') + ('站姿' if pose == 'standing' else '坐姿') + '.png'
    image = Image.open(SOURCE/name).convert('RGBA')
    return image.crop(image.getchannel('A').getbbox())


def standing(sex):
    art = source_art(sex, 'standing')
    art = art.crop((0, 0, art.width, round(art.height * MANIFEST['cropFraction'])))
    art = ImageOps.contain(art, (728,1496), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA',(768,1536))
    canvas.alpha_composite(art,((768-art.width)//2,1536-art.height-20))
    return canvas


def avatar(sex):
    art = source_art(sex, 'standing')
    # Generous head/shoulders square, never cutting off the supplied headwear.
    art = art.crop((0,0,art.width,min(art.height,round(art.width*1.12))))
    art = ImageOps.contain(art,(480,480),Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA',(512,512))
    canvas.alpha_composite(art,((512-art.width)//2,(512-art.height)//2))
    return canvas


def seated(sex):
    art = source_art(sex, 'seated')
    # Cafe chair mirroring assumes right-facing input. Normalize only the left-facing
    # female seated source; standing dialogue portraits are never mirrored.
    if sex == 'female':
        art = ImageOps.mirror(art)
    # Keep the complete seated figure; the 2/3 crop applies ONLY to standing portraits.
    art = ImageOps.contain(art,(262,530),Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA',(262,530))
    canvas.alpha_composite(art,((262-art.width)//2,530-art.height))
    return canvas


def save_sprite(image, path, indoor=False):
    path.parent.mkdir(parents=True,exist_ok=True)
    image.save(path)
    meta = Path(str(path)+'.meta')
    template_path = ROOT/'Assets/Art/FreeCafe/Seating/Characters/silhouette_seated.png.meta' if indoor else ROOT/'Assets/Art/NarrativeGenerated/Characters/CH35_tutorial_elderly_woman_silhouette.png.meta'
    if not meta.exists():
        text = template_path.read_text(encoding='utf-8')
        text = re.sub(r'^guid: .*$', 'guid: '+uuid.uuid4().hex,text,flags=re.M)
        text = text.replace('CH35_tutorial_elderly_woman_silhouette_0',path.stem+'_0')
    else:
        text = meta.read_text(encoding='utf-8')
    # Keep GUIDs and unrelated importer settings; use the whole updated PNG as one sprite.
    text = re.sub(r'  spriteMode: \d+', '  spriteMode: 1',text)
    if indoor:
        text = re.sub(r'  alignment: \d+', '  alignment: 9',text)
        text = re.sub(r'  spritePivot: \{[^\n]+\}', '  spritePivot: {x: 0.05503824, y: 0.31645292}',text)
    meta.write_text(text,encoding='utf-8')


def build():
    for sex in ('male','female'):
        for kind, image in [('neutral',standing(sex)),('avatar',avatar(sex)),('seated',seated(sex))]:
            save_sprite(image,DEST/sex/(kind+'_placeholder.png'),kind=='seated')
    records = json.loads((ROOT/'Tools/Characters/character-art-manifest.json').read_text(encoding='utf-8'))
    for record in records:
        folder = ROOT/'Assets/Resources/Characters'/record['id']
        sex = appearance(record['id'])
        if record['portraitPlaceholder']:
            save_sprite(standing(sex),folder/'neutral.png')
            save_sprite(avatar(sex),folder/'avatar.png')
        if record['seatedPlaceholder']:
            save_sprite(seated(sex),folder/'seated_placeholder.png',True)
    report = {'sourceHashes':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in SOURCE.glob('*.png')},
              'femaleIds':sorted(FEMALE_IDS),'standingCropFraction':MANIFEST['cropFraction'],'seatedCropFraction':1.0,
              'standingPlaceholderCount':sum(r['portraitPlaceholder'] for r in records),
              'indoorPlaceholderCount':sum(r['seatedPlaceholder'] for r in records),
              'apiCalls':0,'preservedOriginals':True}
    work=ROOT/'Library/Locus/tmp/npc-silhouette-replacement';work.mkdir(parents=True,exist_ok=True)
    (work/'image-preparation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))


if __name__ == '__main__':
    build()
