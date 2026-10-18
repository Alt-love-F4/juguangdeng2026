"""Procedurally draws the main-menu motion overlays in the PSD's palette and halftone style.

Run from the project root:  python Tools/MainMenuArt/generate_menu_motion_art.py
Outputs PNGs to Assets/Art/Func/MainMenuMotion/ (re-runnable; overwrites).
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join("Assets", "Art", "Func", "MainMenuMotion")
RNG = np.random.default_rng(20261018)

INK = (59, 39, 34)          # 原画深棕描线
CREAM = (234, 223, 203)     # 云/纸面米白
DOT = (176, 160, 120)       # 半调网点（橄榄米色）
PAPER = (197, 192, 173)     # 飞散纸页的灰绿纸色
PAPER_INK = (66, 85, 68)    # 纸页上的墨绿线条
WARM = (255, 214, 150)      # 暖光


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name))
    print("wrote", name, img.size)


def halftone(w, h, cell, density, color, angle=45.0, seamless_x=False):
    """Halftone dot layer: density is an (h, w) array in [0, 1] mapping to dot radius."""
    ss = 3  # supersample for smooth dots
    W, H = w * ss, h * ss
    layer = Image.new("L", (W, H), 0)
    draw = ImageDraw.Draw(layer)
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)
    c = cell * ss
    # Iterate over rotated grid covering the canvas.
    diag = int(math.hypot(W, H) / c) + 2
    for i in range(-diag, diag):
        for j in range(-diag, diag):
            x = (i * ca - j * sa) * c + W / 2
            y = (i * sa + j * ca) * c + H / 2
            if x < -c or y < -c or x > W + c or y > H + c:
                continue
            px = int(np.clip(x / ss, 0, w - 1))
            py = int(np.clip(y / ss, 0, h - 1))
            d = density[py, px]
            if d <= 0.02:
                continue
            r = c * 0.5 * math.sqrt(d) * 1.05
            draw.ellipse((x - r, y - r, x + r, y + r), fill=255)
            if seamless_x:
                draw.ellipse((x - r - W, y - r, x + r - W, y + r), fill=255)
                draw.ellipse((x - r + W, y - r, x + r + W, y + r), fill=255)
    mask = layer.resize((w, h), Image.LANCZOS)
    out = Image.new("RGBA", (w, h), color + (0,))
    out.putalpha(mask)
    return out


def smooth_noise(w, h, scale, seamless_x=False, octaves=3):
    total = np.zeros((h, w))
    amp = 1.0
    for o in range(octaves):
        gw = max(2, int(w / scale) * (2 ** o))
        gh = max(2, int(h / scale) * (2 ** o))
        grid = RNG.random((gh + 1, gw + 1))
        if seamless_x:
            grid[:, -1] = grid[:, 0]
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        total += np.asarray(img, dtype=float) / 255.0 * amp
        amp *= 0.5
    total -= total.min()
    return total / total.max()


def coffee_emblem(draw, cx, cy, s):
    """Inked coffee cup with three steam curls — echoes the cup in the PSD's foreground."""
    ink = INK + (200,)
    lw = int(5 * s)
    # Cup body and saucer.
    draw.arc((cx - 70 * s, cy - 40 * s, cx + 70 * s, cy + 90 * s), 0, 180, fill=ink, width=lw)
    draw.line((cx - 70 * s, cy + 25 * s, cx + 70 * s, cy + 25 * s), fill=ink, width=lw)
    draw.arc((cx + 50 * s, cy + 20 * s, cx + 100 * s, cy + 70 * s), 270, 110, fill=ink, width=lw)
    draw.arc((cx - 110 * s, cy + 80 * s, cx + 110 * s, cy + 110 * s), 0, 180, fill=ink, width=lw)
    # Steam curls.
    for k, dx in enumerate((-30, 0, 30)):
        pts = []
        for t in np.linspace(0, 1, 30):
            pts.append((cx + dx * s + math.sin(t * math.pi * 2 + k) * 12 * s, cy + 5 * s - t * 110 * s))
        draw.line(pts, fill=INK + (150,), width=int(4 * s), joint="curve")


def page_sheet():
    """Transition page: cream newsprint with halftone shading, ruled columns and an inked edge."""
    w, h = 1920, 1080
    base = Image.new("RGBA", (w, h), CREAM + (255,))
    x = np.linspace(0, 1, w)[None, :].repeat(h, 0)
    y = np.linspace(0, 1, h)[:, None].repeat(w, 1)
    noise = smooth_noise(w, h, 240)
    density = np.clip(0.55 * (1 - x) ** 2.2 + 0.25 * noise * (1 - x) + 0.12 * y * (1 - x), 0, 0.85)
    base.alpha_composite(halftone(w, h, 12, density, DOT, angle=45))
    draw = ImageDraw.Draw(base)
    # Newspaper columns: masthead rule + faint text lines, like the newspapers in the PSD.
    for cx in (0.2, 0.62):
        x0, x1 = int(cx * w), int((cx + 0.17) * w)
        for k in range(22):
            yy = int(h * 0.3 + k * 30)
            ln = x1 - int(RNG.random() * 90) if k % 6 == 5 else x1
            draw.line((x0, yy, ln, yy), fill=PAPER_INK + (70,), width=4)
    draw.line((int(0.16 * w), int(0.17 * h), int(0.9 * w), int(0.17 * h)), fill=INK + (150,), width=7)
    draw.line((int(0.16 * w), int(0.195 * h), int(0.9 * w), int(0.195 * h)), fill=INK + (110,), width=3)
    coffee_emblem(draw, int(0.53 * w), int(0.55 * h), 1.6)
    # Slightly wobbly inked leading edge (left).
    pts = [(int(8 + 4 * math.sin(i * 0.35) + RNG.random() * 3), int(i * h / 60)) for i in range(61)]
    draw.line(pts, fill=INK + (230,), width=10)
    # Print grain.
    grain = (RNG.random((h, w)) * 28).astype(np.uint8)
    g = Image.fromarray(grain, "L")
    grain_layer = Image.new("RGBA", (w, h), INK + (0,))
    grain_layer.putalpha(g)
    base.alpha_composite(grain_layer)
    save(base, "page_sheet.png")

    # Soft drop shadow strip that rides on the page's leading edge.
    sw = 96
    shade = np.linspace(0, 1, sw)[None, :].repeat(8, 0) ** 1.8 * 150
    s = Image.new("RGBA", (sw, 8), (40, 24, 18, 0))
    s.putalpha(Image.fromarray(shade.astype(np.uint8), "L"))
    save(s, "page_shadow.png")


def mist_band():
    """Horizontally seamless cream puffs with halftone dots, echoing the PSD's cloud band."""
    w, h = 1024, 256
    puffs = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(puffs)
    x = 0
    while x < w + 80:
        r = int(36 + RNG.random() * 48)
        cy = int(h * 0.56 + (RNG.random() - 0.5) * 50)
        for dx in (-w, 0, w):
            d.ellipse((x + dx - r, cy - r, x + dx + r, cy + r), fill=255)
        x += int(r * (1.0 + RNG.random() * 0.6))
    d.rectangle((0, int(h * 0.62), w, h), fill=255)
    puffs = puffs.filter(ImageFilter.GaussianBlur(10))
    a = np.asarray(puffs, dtype=float) / 255.0
    y = np.linspace(0, 1, h)[:, None].repeat(w, 1)
    a *= np.clip((1 - y) * 4, 0, 1) * np.clip(y * 3, 0, 1)  # fade top and bottom
    a *= np.clip(1.15 - y, 0, 1)
    img = Image.new("RGBA", (w, h), CREAM + (0,))
    img.putalpha(Image.fromarray((a * 255).astype(np.uint8), "L"))
    dots = halftone(w, h, 10, np.clip(a * 0.9 * smooth_noise(w, h, 120, True), 0, 1), DOT, 30, seamless_x=True)
    dots_a = np.asarray(dots.getchannel("A"), dtype=float) * a
    dots.putalpha(Image.fromarray(dots_a.astype(np.uint8), "L"))
    img.alpha_composite(dots)
    save(img, "mist_band.png")


def bird_frames():
    """Three wing poses, hand-inked with tapered strokes. Drawn white so Image.color can tint
    them: warm cream reads against the dark sky, ink brown against the lighter haze."""
    w, h = 96, 48
    poses = [(-0.75, -0.25), (-0.15, 0.1), (0.45, 0.55)]  # (shoulder lift, tip lift) up→down
    for idx, (lift, tip) in enumerate(poses):
        ss = 4
        img = Image.new("L", (w * ss, h * ss), 0)
        d = ImageDraw.Draw(img)
        cx, cy = w * ss / 2, h * ss * 0.55
        for side in (-1, 1):
            pts = []
            for t in np.linspace(0, 1, 24):
                px = cx + side * t * w * ss * 0.44
                py = cy + (lift * math.sin(t * math.pi * 0.9) + tip * t * t) * h * ss * 0.55
                pts.append((px, py))
            for k in range(len(pts) - 1):
                width = int((1 - k / len(pts)) * 5.5 * ss) + ss
                d.line((pts[k], pts[k + 1]), fill=255, width=width)
        d.ellipse((cx - 5 * ss, cy - 4 * ss, cx + 5 * ss, cy + 4 * ss), fill=255)
        mask = img.resize((w, h), Image.LANCZOS)
        out = Image.new("RGBA", (w, h), (255, 255, 255, 0))
        out.putalpha(mask)
        save(out, f"bird_{idx}.png")


def light_rays():
    """Warm diagonal beams from the top-left, broken up with sparse halftone like print."""
    w, h = 512, 512
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    ox, oy = -0.15 * w, -0.2 * h
    ang = np.arctan2(yy - oy, xx - ox)
    dist = np.hypot(xx - ox, yy - oy) / (w * 1.25)
    beams = np.zeros_like(ang)
    for c, wdt, s in ((0.55, 0.05, 1.0), (0.72, 0.035, 0.8), (0.88, 0.06, 0.9), (1.02, 0.03, 0.6)):
        beams += s * np.exp(-((ang - c) / wdt) ** 2)
    a = np.clip(beams, 0, 1) * np.clip(1.15 - dist, 0, 1) ** 1.5
    a = Image.fromarray((a * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(6))
    img = Image.new("RGBA", (w, h), WARM + (0,))
    img.putalpha(a)
    av = np.asarray(a, dtype=float) / 255.0
    dots = halftone(w, h, 7, av * 0.5, (255, 236, 200), 15)
    img.alpha_composite(dots)
    save(img, "light_rays.png")


def paper_slip():
    """Small flying page matching the grey-green papers piled in the PSD."""
    w, h = 112, 84
    ss = 3
    img = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = 6 * ss
    corners = [(m, m + 4 * ss), (w * ss - m, m), (w * ss - m - 3 * ss, h * ss - m), (m + 2 * ss, h * ss - m + ss)]
    d.polygon(corners, fill=PAPER + (255,))
    for k in range(5):
        yy = m + 14 * ss + k * 11 * ss
        d.line((m + 10 * ss, yy, w * ss - m - (18 if k == 4 else 12) * ss, yy - 2 * ss), fill=PAPER_INK + (170,), width=2 * ss)
    d.line(corners + [corners[0]], fill=PAPER_INK + (255,), width=3 * ss, joint="curve")
    # Folded corner hint.
    d.line((w * ss - m - 18 * ss, m + 2 * ss, w * ss - m - 2 * ss, m + 16 * ss), fill=PAPER_INK + (200,), width=2 * ss)
    img = img.resize((w, h), Image.LANCZOS)
    save(img, "paper_slip.png")


def dust_mote():
    s = 32
    yy, xx = np.mgrid[0:s, 0:s].astype(float)
    r = np.hypot(xx - s / 2 + 0.5, yy - s / 2 + 0.5) / (s / 2)
    a = np.clip(1 - r, 0, 1) ** 2
    img = Image.new("RGBA", (s, s), (255, 240, 214, 0))
    img.putalpha(Image.fromarray((a * 255).astype(np.uint8), "L"))
    save(img, "dust_mote.png")


def title_gleam():
    w, h = 128, 256
    x = np.linspace(-1, 1, w)[None, :].repeat(h, 0)
    a = np.exp(-(x / 0.28) ** 2) * 0.9 + np.exp(-((x - 0.55) / 0.08) ** 2) * 0.5
    img = Image.new("RGBA", (w, h), (255, 248, 236, 0))
    img.putalpha(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "L"))
    save(img, "title_gleam.png")


if __name__ == "__main__":
    page_sheet()
    mist_band()
    bird_frames()
    light_rays()
    paper_slip()
    dust_mote()
    title_gleam()
