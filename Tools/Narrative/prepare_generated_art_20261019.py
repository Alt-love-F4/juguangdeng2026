"""Local deterministic preparation of the four explicitly requested 2026-10-19 API outputs.
No API calls. Preserve original outputs, requirement cards and existing art.
"""
from pathlib import Path
import hashlib
import io
import json
from PIL import Image, ImageDraw
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'output/art-fill-2026-10-19'
DEST = ROOT / 'Assets/Art/NarrativeGenerated'


def remove_border_white(image):
    array = np.array(image.convert('RGBA'))
    rgb = array[:, :, :3]
    near = (rgb.min(axis=2) > 238) & ((rgb.max(axis=2) - rgb.min(axis=2)) < 12)
    mask = Image.fromarray(np.where(near, 255, 0).astype('uint8'), 'L').copy()
    w, h = mask.size
    for xy in ((0, 0), (w-1, 0), (0, h-1), (w-1, h-1)):
        if mask.getpixel(xy) == 255:
            ImageDraw.floodfill(mask, xy, 128, thresh=0)
    array[np.array(mask) == 128, 3] = 0
    return Image.fromarray(array)


def save(image, relative):
    path = DEST / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = io.BytesIO()
    image.save(buffer, format='PNG')
    data = buffer.getvalue()
    if path.exists() and path.read_bytes() != data:
        raise FileExistsError('Refusing to replace different existing artwork: ' + str(path))
    if not path.exists():
        path.write_bytes(data)
    return {'path': path.relative_to(ROOT).as_posix(), 'width': image.width, 'height': image.height,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    image = Image.open(WORK/'raw/CG07_marie_moonlit.png').convert('RGB')
    height = image.width * 9 // 16
    top = (image.height-height)//2
    moon = save(image.crop((0, top, image.width, top+height)), 'CG/CG07_marie_moonlit.png')
    elderly_image = remove_border_white(Image.open(WORK/'raw/CH35_tutorial_elderly_woman.png'))
    elderly = save(elderly_image, 'Characters/CH35_tutorial_elderly_woman.png')
    shadow_array = np.array(elderly_image)
    shadow_array[:, :, :3] = 18
    shadow = save(Image.fromarray(shadow_array), 'Characters/CH35_tutorial_elderly_woman_silhouette.png')
    coin_image = remove_border_white(Image.open(WORK/'raw/P04_king_coin.png'))
    coin = save(coin_image, 'Props/P04_king_coin.png')
    crop = coin_image.crop(coin_image.getbbox())
    group = Image.new('RGBA', (1536, 864))
    # Exactly fourteen decorative copies; visual count does not determine gameplay reward values.
    layout = [(280, 80, 220, 0), (505, 60, 228, -12), (738, 90, 220, 7), (970, 70, 228, 18),
              (145, 275, 235, -18), (390, 268, 232, 14), (635, 285, 235, -8), (875, 270, 232, 8), (1110, 278, 235, -14),
              (190, 490, 235, 6), (435, 510, 230, -13), (675, 500, 235, 17), (920, 508, 230, -6), (1160, 495, 235, 12)]
    for x, y, size, angle in layout:
        item = crop.resize((size, size), Image.Resampling.LANCZOS).rotate(angle, Image.Resampling.BICUBIC, expand=True)
        group.alpha_composite(item, (x, y))
    coins = save(group, 'Props/P04_king_coins.png')
    report = {'date': '2026-10-19', 'provider': 'PackyAPI', 'model': 'gpt-image-2.5-sunburst',
              'apiRequestCount': 4, 'endingsExcluded': True, 'humanArtApproval': False,
              'items': [
                  {'id': 'CG07', 'key': 'cg.marie_moonlit', 'asset': moon,
                   'status': 'integrated-ai-generated-art-pending-human-review',
                   'sourceAnchor': 'MA70.10.29-S1.docx P012-P015', 'existingStoryHook': True,
                   'rawPath': 'output/art-fill-2026-10-19/raw/CG07_marie_moonlit.png'},
                  {'id': 'CG09', 'key': 'cg.rainy_triad', 'status': 'candidate-rejected-for-runtime-binding',
                   'reason': 'Protagonist firearm points down instead of aiming at Marie as the source requires. No automatic API retry.',
                   'existingStoryHook': True, 'bindingChanged': False,
                   'rawPath': 'output/art-fill-2026-10-19/raw/CG09_rainy_triad.png'},
                  {'id': 'CH35', 'key': 'character.tutorial_elderly_woman', 'asset': elderly,
                   'derivedSilhouette': shadow, 'status': 'tutorial-center-silhouette-integrated-ai-art-pending-human-review',
                   'sourceAnchor': '文案-新手教程.docx P013-P015',
                   'rawPath': 'output/art-fill-2026-10-19/raw/CH35_tutorial_elderly_woman.png',
                   'note': 'Uses existing CafeTutorialVisuals centerPortrait; retains existing silhouette presentation. No new reveal timing.'},
                  {'id': 'P04', 'key': 'prop.king_coins', 'asset': coins, 'singleCoin': coin,
                   'status': 'registered-art-no-active-story-hook', 'existingStoryHook': False,
                   'sourceAnchor': 'MA70.11.04-S1.docx P061-P063',
                   'rawPath': 'output/art-fill-2026-10-19/raw/P04_king_coin.png',
                   'note': '14 visual copies are not a reward amount. Coin relief is a generated art proposal, not approved currency lore.'}
              ]}
    for item in report['items']:
        raw = ROOT/item['rawPath']
        item['rawSha256'] = hashlib.sha256(raw.read_bytes()).hexdigest()
        reference_file = WORK/'prompts'/('P04-references.json' if item['id'] == 'P04' else item['id']+'-references.json')
        item['references'] = []
        for value in json.loads(reference_file.read_text(encoding='utf-8-sig')):
            reference = Path(value)
            item['references'].append({'path': reference.relative_to(ROOT).as_posix(),
                                       'sha256': hashlib.sha256(reference.read_bytes()).hexdigest()})
    (ROOT/'Docs/NarrativeGeneratedArtDelivery-2026-10-19.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
