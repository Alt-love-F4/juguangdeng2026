"""Crop the four Packy customer sheets into deterministic upper-body portraits and avatars."""
from __future__ import annotations

from collections import deque
from hashlib import sha256
from pathlib import Path
import json

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "output/art-fill-2026-10-19/raw/customer-variants"
OUTPUT = ROOT / "Assets/Resources/Characters"
REPORT = ROOT / "output/art-fill-2026-10-19/customer-variant-crops.json"


def remove_connected_white(image: Image.Image) -> Image.Image:
    rgba = np.array(image.convert("RGBA"))
    rgb = rgba[:, :, :3]
    white = (rgb.min(axis=2) >= 243) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 10)
    height, width = white.shape
    seen = np.zeros_like(white, dtype=bool)
    queue = deque()
    for x in range(width):
        if white[0, x]: queue.append((x, 0))
        if white[height - 1, x]: queue.append((x, height - 1))
    for y in range(height):
        if white[y, 0]: queue.append((0, y))
        if white[y, width - 1]: queue.append((width - 1, y))
    while queue:
        x, y = queue.popleft()
        if seen[y, x] or not white[y, x]: continue
        seen[y, x] = True
        if x: queue.append((x - 1, y))
        if x + 1 < width: queue.append((x + 1, y))
        if y: queue.append((x, y - 1))
        if y + 1 < height: queue.append((x, y + 1))
    rgba[seen, 3] = 0
    return Image.fromarray(rgba)


def prepare_portrait(cell: Image.Image) -> tuple[Image.Image, Image.Image, int]:
    result = remove_connected_white(cell)
    alpha = np.array(result.getchannel("A"))
    ys, xs = np.where(alpha > 8)
    if len(xs) == 0: raise ValueError("Customer crop has no opaque figure")
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    figure = result.crop(bbox)
    if figure.width < 280 or figure.height < 320:
        raise ValueError("Unexpectedly small customer figure: " + repr(figure.size))

    # The source panels are square bust portraits. Keep all artwork and place it on a portrait canvas;
    # do not synthesize legs or turn the required upper-body art into full-body art.
    canvas = Image.new("RGBA", (768, 1024), (0, 0, 0, 0))
    figure.thumbnail((700, 760), Image.Resampling.LANCZOS)
    canvas.alpha_composite(figure, ((768 - figure.width) // 2, 36))

    figure_alpha = np.array(figure.getchannel("A"))
    fy, fx = np.where(figure_alpha > 8)
    left, top, right, bottom = int(fx.min()), int(fy.min()), int(fx.max()) + 1, int(fy.max()) + 1
    face_crop = figure.crop((left, top, right, min(bottom, top + int(figure.height * .69))))
    side = max(face_crop.width, face_crop.height)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(face_crop, ((side - face_crop.width) // 2, (side - face_crop.height) // 2))
    avatar = square.resize((512, 512), Image.Resampling.LANCZOS)
    transparent = int((np.array(result.getchannel("A")) == 0).sum())
    return canvas, avatar, transparent


def save_new(image: Image.Image, path: Path) -> dict:
    path.parent.mkdir(parents=True, exist_ok=True)
    buffer = __import__("io").BytesIO()
    image.save(buffer, format="PNG", optimize=False, compress_level=9)
    data = buffer.getvalue()
    if path.exists():
        if path.read_bytes() != data:
            raise FileExistsError("Refusing to overwrite different art: " + str(path))
    else:
        path.write_bytes(data)
    return {"path": path.relative_to(ROOT).as_posix(), "width": image.width, "height": image.height,
            "sha256": sha256(data).hexdigest()}


def main() -> None:
    report = {"source": "PackyAPI 2x2 upper-body sheets", "variantsPerRole": 4, "roles": {}}
    for character_id in ("guest_a", "guest_b", "guest_c", "guest_d"):
        sheet = Image.open(RAW / f"{character_id}_sheet.png").convert("RGBA")
        if sheet.size != (1024, 1024): raise ValueError(f"Unexpected contact sheet dimensions: {character_id} {sheet.size}")
        role = []
        for row in range(2):
            for col in range(2):
                index = row * 2 + col + 1
                cell = sheet.crop((col * 512, row * 512, (col + 1) * 512, (row + 1) * 512))
                portrait, avatar, transparent = prepare_portrait(cell)
                folder = OUTPUT / character_id / "variants"
                ppath = save_new(portrait, folder / f"portrait_{index:02d}.png")
                apath = save_new(avatar, folder / f"avatar_{index:02d}.png")
                role.append({"index": index, "portrait": ppath, "avatar": apath,
                             "sourceSheet": (RAW / f"{character_id}_sheet.png").relative_to(ROOT).as_posix(),
                             "exteriorTransparentPixels": transparent})
        report["roles"][character_id] = role

    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"roleCount": len(report["roles"]), "portraitVariants": 16,
                      "avatarVariants": 16, "report": REPORT.as_posix()}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
