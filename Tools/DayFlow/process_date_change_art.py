"""Builds the DateChangePanel ("Le Petit Almanach") sprites.

Inputs: PackyAPI generations on flat magenta, in output/datechange/raw2 (gitignored).
Outputs: Assets/Resources/Art/Gameplay/DayFlow/DateChange/*.png

    python -I Tools/DayFlow/process_date_change_art.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "output", "datechange", "raw2")
OUT = os.path.join(ROOT, "Assets", "Resources", "Art", "Gameplay", "DayFlow", "DateChange")
MAGENTA = np.array([255.0, 0.0, 255.0])


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name)
    img.save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT), img.size)


def key_magenta(name, pad=12, low=40.0, high=150.0):
    """Remove the flat magenta matte and un-premultiply edge colours to kill pink fringes."""
    rgb = np.asarray(Image.open(os.path.join(RAW, name)).convert("RGB")).astype(np.float64)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    magentaness = np.minimum(r, b) - g
    alpha = 1.0 - np.clip((magentaness - low) / (high - low), 0.0, 1.0)
    # Shrink the matte by one pixel; generators soften the edge with magenta.
    a_img = Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3))
    a_img = a_img.filter(ImageFilter.GaussianBlur(0.6))
    alpha = np.asarray(a_img).astype(np.float64) / 255.0
    safe = np.maximum(alpha, 0.05)[..., None]
    colour = (rgb - (1.0 - safe) * MAGENTA) / safe
    colour = np.clip(colour, 0, 255)
    # Residual spill: never let an edge pixel be more magenta than its green allows.
    spill = np.minimum(colour[..., 0], colour[..., 2]) - colour[..., 1]
    edge = (alpha < 0.98) & (spill > 30)
    colour[edge, 0] -= (spill[edge] - 30) * 0.5
    colour[edge, 2] -= (spill[edge] - 30)
    colour = np.clip(colour, 0, 255)
    rgba = np.dstack([colour, alpha * 255]).astype(np.uint8)
    img = Image.fromarray(rgba, "RGBA")
    bbox = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    img = img.crop((max(bbox[0] - pad, 0), max(bbox[1] - pad, 0),
                    min(bbox[2] + pad, img.width), min(bbox[3] + pad, img.height)))
    return img


def radial(size, stops, power=1.0):
    """Soft round sprite: stops are (t, (r,g,b,a)) from centre (0) to edge (1)."""
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xx + .5) / w * 2 - 1) ** 2 + ((yy + .5) / h * 2 - 1) ** 2) ** power
    out = np.zeros((h, w, 4))
    ts = [s[0] for s in stops]
    for c in range(4):
        out[..., c] = np.interp(d, ts, [s[1][c] for s in stops])
    return Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")


def grain(img, amount=10, seed=7):
    rnd = np.random.default_rng(seed)
    a = np.asarray(img).astype(np.int16)
    n = rnd.integers(-amount, amount + 1, a.shape[:2])
    a[..., :3] = np.clip(a[..., :3] + n[..., None], 0, 255)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def flame():
    w, h = 96, 160
    yy, xx = np.mgrid[0:h, 0:w]
    x = (xx + .5) / w * 2 - 1
    y = (yy + .5) / h  # 0 top, 1 bottom
    # Teardrop: narrow tip at top, round belly near the bottom.
    width = np.where(y < .72, np.sin(np.clip(y / .72, 0, 1) * math.pi / 2) ** 1.6 * .62,
                     np.sqrt(np.clip(1 - ((y - .72) / .26) ** 2, 0, 1)) * .62)
    inside = np.abs(x) / np.maximum(width, 1e-3)
    mask = np.clip((1.0 - inside) * 6, 0, 1) * (y < .98)
    core = np.clip(1.0 - np.sqrt((x / .3) ** 2 + ((y - .74) / .2) ** 2), 0, 1)
    col = np.zeros((h, w, 4))
    outer = np.array([236, 128, 48.0])
    mid = np.array([252, 196, 92.0])
    inner = np.array([255, 246, 214.0])
    t = np.clip(core * 1.6, 0, 1)[..., None]
    rgb = outer * (1 - t) + mid * t
    rgb = rgb * (1 - np.clip(core * 2 - 1, 0, 1)[..., None]) + inner * np.clip(core * 2 - 1, 0, 1)[..., None]
    col[..., :3] = rgb
    col[..., 3] = mask * 255
    img = Image.fromarray(col.astype(np.uint8), "RGBA").filter(ImageFilter.GaussianBlur(1.2))
    return img


def window_light():
    """Warm morning light thrown on the wall by a four-pane window (soft edges, slanted)."""
    w, h = 900, 1100
    base = Image.new("L", (w, h), 0)
    px = base.load()
    frame = 26
    pane_w, pane_h = (w - frame * 3) // 2, (h - frame * 3) // 2
    arr = np.zeros((h, w))
    for ix in range(2):
        for iy in range(2):
            x0 = frame + ix * (pane_w + frame)
            y0 = frame + iy * (pane_h + frame)
            arr[y0:y0 + pane_h, x0:x0 + pane_w] = 1.0
    img = Image.fromarray((arr * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(14))
    # Shear so the light falls diagonally, like low dawn sun.
    sheared = img.transform((w + 420, h), Image.AFFINE, (1, -0.38, 0, 0, 1, 0), resample=Image.BICUBIC)
    sheared = sheared.transform(sheared.size, Image.AFFINE, (1, 0, -0, 0, 1, 0))
    a = np.asarray(sheared).astype(np.float64) / 255.0
    # Fade the light toward the bottom of the beam.
    fade = np.linspace(1.0, .55, a.shape[0])[:, None]
    a = a * fade
    rgba = np.zeros(a.shape + (4,))
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 255, 226, 168
    rgba[..., 3] = a * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def smoke():
    """A single hand-drawn-ish smoke curl puff (soft blobs along an S-curve)."""
    w, h = 160, 320
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    rnd = random.Random(3)
    for i in range(26):
        t = i / 25
        cx = w / 2 + math.sin(t * math.pi * 2.2) * 34 * t
        cy = h - 30 - t * (h - 70)
        r = 14 + t * 22
        blob = radial((int(r * 2), int(r * 2)), [(0, (236, 226, 214, 150 * (1 - t * .6))), (1, (236, 226, 214, 0))])
        img.alpha_composite(blob, (int(cx - r), int(cy - r)))
    return img.filter(ImageFilter.GaussianBlur(2))


def paper_shadow(size):
    w, h = size
    img = Image.new("L", (w + 80, h + 80), 0)
    img.paste(255, (40, 40, 40 + w, 40 + h))
    img = img.filter(ImageFilter.GaussianBlur(18))
    a = np.asarray(img).astype(np.float64) / 255.0
    rgba = np.zeros(a.shape + (4,))
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 48, 22, 12
    rgba[..., 3] = a * 255
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def main():
    wall = Image.open(os.path.join(RAW, "wall.png")).convert("RGB")
    save(wall, "date_wall.png")

    board = key_magenta("board.png")
    save(board, "date_calendar_board.png")
    page = key_magenta("page.png")
    save(page, "date_calendar_page.png")
    save(paper_shadow((page.width - 40, page.height - 40)), "date_page_shadow.png")
    save(key_magenta("sconce.png"), "date_candle_sconce.png")
    save(key_magenta("seal.png", low=60, high=170), "date_stamp.png")

    save(flame(), "date_flame.png")
    save(radial((256, 256), [(0, (255, 214, 140, 210)), (.35, (255, 176, 96, 110)), (1, (255, 150, 80, 0))], 1.2),
         "date_glow.png")
    save(radial((32, 32), [(0, (255, 244, 214, 255)), (.45, (255, 230, 180, 170)), (1, (255, 220, 160, 0))]),
         "date_mote.png")
    save(window_light(), "date_window_light.png")
    save(smoke(), "date_smoke.png")
    # Full-screen tint plate: flat white with a soft vignette so the code can colour it night/dawn.
    vig = radial((480, 270), [(0, (255, 255, 255, 205)), (.7, (255, 255, 255, 240)), (1.45, (255, 255, 255, 255))])
    save(vig, "date_tint_vignette.png")


if __name__ == "__main__":
    main()
