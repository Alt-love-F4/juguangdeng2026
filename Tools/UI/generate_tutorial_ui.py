"""Procedurally draws the shared tutorial prompt sprites (no AI image service).

Style follows the café UI: cream parchment with grain, rough coffee-brown crayon
outlines, coffee-bean / branch ornaments only in 9-slice corners.

Usage: python -I Tools/UI/generate_tutorial_ui.py [output_dir]
Default output: Assets/Art/UI/Tutorial
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SS = 2  # supersampling factor
RNG = np.random.default_rng(20261020)

INK = (90, 48, 24)
INK_DARK = (62, 32, 16)
PAPER = (243, 230, 196)
PAPER_EDGE = (226, 204, 158)
CHOCOLATE = (74, 38, 20)
TAN = (226, 201, 154)
CREAM_LINE = (240, 222, 182)
ORANGE = (208, 96, 32)


# ---------------------------------------------------------------- helpers
def smooth_noise(n, step, amp):
    """1D smooth noise of length n (control point every `step` samples)."""
    k = max(2, int(n / step) + 2)
    ctrl = RNG.uniform(-amp, amp, k)
    return np.interp(np.arange(n), np.linspace(0, n - 1, k), ctrl)


def field_noise(w, h, cell, amp):
    """2D low-frequency noise (bilinear upsampled random grid)."""
    gw, gh = max(2, w // cell + 2), max(2, h // cell + 2)
    grid = Image.fromarray(RNG.uniform(0, 255, (gh, gw)).astype(np.uint8))
    up = np.asarray(grid.resize((w, h), Image.BICUBIC), np.float32) / 255.0
    return (up - 0.5) * 2 * amp


def rounded_rect_path(x0, y0, x1, y1, r, spacing=2.0):
    """Dense closed perimeter of a rounded rectangle, clockwise from top-left."""
    pts = []

    def line(a, b):
        n = max(2, int(math.dist(a, b) / spacing))
        for i in range(n):
            t = i / n
            pts.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))

    def arc(cx, cy, a0, a1):
        n = max(2, int(abs(a1 - a0) * r / spacing))
        for i in range(n):
            a = a0 + (a1 - a0) * i / n
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))

    line((x0 + r, y0), (x1 - r, y0)); arc(x1 - r, y0 + r, -math.pi / 2, 0)
    line((x1, y0 + r), (x1, y1 - r)); arc(x1 - r, y1 - r, 0, math.pi / 2)
    line((x1 - r, y1), (x0 + r, y1)); arc(x0 + r, y1 - r, math.pi / 2, math.pi)
    line((x0, y1 - r), (x0, y0 + r)); arc(x0 + r, y0 + r, math.pi, 1.5 * math.pi)
    return np.array(pts, np.float32)


def jitter_path(pts, amp, step=40, closed=True):
    """Offset a path along its normals by smooth noise (hand-drawn wobble)."""
    pts = np.asarray(pts, np.float32)
    d = np.roll(pts, -1, 0) - np.roll(pts, 1, 0)
    nrm = np.stack([-d[:, 1], d[:, 0]], 1)
    nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-6
    off = smooth_noise(len(pts), step, amp)
    if closed:  # make the wobble meet itself
        fade = np.minimum(1, np.minimum(np.arange(len(pts)), len(pts) - np.arange(len(pts))) / 30)
        off *= fade
    return pts + nrm * off[:, None]


class Canvas:
    """Supersampled RGBA canvas assembled from premultiplied layers."""

    def __init__(self, w, h):
        self.w, self.h = w * SS, h * SS
        self.rgb = np.zeros((self.h, self.w, 3), np.float32)
        self.a = np.zeros((self.h, self.w), np.float32)

    def blend(self, color, alpha):
        alpha = np.clip(alpha, 0, 1)
        col = np.asarray(color, np.float32)[None, None, :] if np.ndim(color) == 1 else color
        self.rgb = self.rgb * (1 - alpha[..., None]) + col * alpha[..., None]
        self.a = self.a + alpha * (1 - self.a)

    def mask(self):
        return Image.new("L", (self.w, self.h), 0)

    def save(self, path):
        a = np.clip(self.a, 0, 1)
        img = np.dstack([np.clip(self.rgb, 0, 255), a * 255]).astype(np.uint8)
        im = Image.fromarray(img, "RGBA").resize((self.w // SS, self.h // SS), Image.LANCZOS)
        im.save(path)
        print("wrote", path, im.size)


def to_alpha(mask_img):
    return np.asarray(mask_img, np.float32) / 255.0


def chalk(alpha, density=0.62, blur=0.5):
    """Break a stroke mask into crayon-like grain."""
    grain = RNG.uniform(0, 1, alpha.shape).astype(np.float32)
    grain = np.asarray(Image.fromarray((grain * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(blur * SS)), np.float32) / 255.0
    keep = np.clip((grain - (1 - density)) * 4 + 0.5, 0.25, 1)
    return alpha * keep


def stroke(cv, pts, width, color, passes=3, wobble=1.6, opacity=0.95, closed=True):
    """Draw a rough crayon outline along `pts` (already in supersampled space)."""
    acc = np.zeros((cv.h, cv.w), np.float32)
    for p in range(passes):
        m = cv.mask()
        dr = ImageDraw.Draw(m)
        path = jitter_path(pts, wobble * SS, step=30 + 20 * p, closed=closed)
        seq = [tuple(map(float, q)) for q in path]
        if closed:
            seq.append(seq[0])
        w = max(1, int(round(width * SS * (1.0 if p == 0 else 0.55))))
        dr.line(seq, fill=255, width=w, joint="curve")
        acc = np.maximum(acc, to_alpha(m) * (1.0 if p == 0 else 0.7))
    cv.blend(color, chalk(acc) * opacity)


def paper_fill(cv, poly_pts, base, edge, grain=6.0, mottling=5.0, edge_width=26):
    m = cv.mask()
    ImageDraw.Draw(m).polygon([tuple(map(float, q)) for q in poly_pts], fill=255)
    shape = to_alpha(m.filter(ImageFilter.GaussianBlur(0.6 * SS)))
    # Inner darkening toward the edge (aged paper).
    dist = np.asarray(m.filter(ImageFilter.GaussianBlur(edge_width * SS)), np.float32) / 255.0
    t = np.clip((dist - 0.5) * 2, 0, 1)[..., None]
    col = np.asarray(edge, np.float32) * (1 - t) + np.asarray(base, np.float32) * t
    col = col + field_noise(cv.w, cv.h, 60 * SS, mottling)[..., None]
    col = col + RNG.normal(0, grain, (cv.h, cv.w, 1)).astype(np.float32)
    cv.blend(np.clip(col, 0, 255), shape)


def solid_fill(cv, poly_pts, color, grain=5.0):
    m = cv.mask()
    ImageDraw.Draw(m).polygon([tuple(map(float, q)) for q in poly_pts], fill=255)
    col = np.asarray(color, np.float32)[None, None, :] + RNG.normal(0, grain, (cv.h, cv.w, 1)).astype(np.float32)
    col = col + field_noise(cv.w, cv.h, 50 * SS, grain * 1.5)[..., None]
    cv.blend(np.clip(col, 0, 255), to_alpha(m.filter(ImageFilter.GaussianBlur(0.6 * SS))))


def bezier(p0, p1, p2, n=40):
    t = np.linspace(0, 1, n)[:, None]
    return (1 - t) ** 2 * np.array(p0) + 2 * (1 - t) * t * np.array(p1) + t ** 2 * np.array(p2)


def coffee_bean(cv, cx, cy, rx, ry, angle, fill, ink):
    """Coffee bean: filled ellipse with an S-shaped centre crease."""
    s = SS
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)

    def tr(x, y):
        return (cx * s + (x * ca - y * sa) * s, cy * s + (x * sa + y * ca) * s)

    ell = np.array([tr(rx * math.cos(t), ry * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 90, endpoint=False)])
    solid_fill(cv, ell, fill, grain=4)
    stroke(cv, ell, 1.6, ink, passes=2, wobble=0.3)
    crease = [tr(x, ry * 0.28 * math.sin(x / rx * math.pi)) for x in np.linspace(-rx * 0.82, rx * 0.82, 30)]
    stroke(cv, np.array(crease), 1.5, ink, passes=1, wobble=0.2, closed=False)


def leaf(cv, x0, y0, x1, y1, bulge, fill, ink):
    """Pointed leaf between two points, curving by `bulge` (px)."""
    s = SS
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy)
    nx, ny = -dy / L, dx / L
    a = bezier((x0 * s, y0 * s), ((mx + nx * bulge) * s, (my + ny * bulge) * s), (x1 * s, y1 * s))
    b = bezier((x1 * s, y1 * s), ((mx - nx * bulge) * s, (my - ny * bulge) * s), (x0 * s, y0 * s))
    outline = np.vstack([a, b])
    solid_fill(cv, outline, fill, grain=4)
    stroke(cv, outline, 1.4, ink, passes=2, wobble=0.2)
    stroke(cv, np.array([(x0 * s, y0 * s), (x1 * s, y1 * s)]), 1.0, ink, passes=1, wobble=0, closed=False, opacity=0.7)


def corner_sprig(cv, x, y, fx, fy, k=1.55):
    """Coffee branch ornament anchored at corner (x,y); fx/fy flip direction (+1/-1)."""
    def P(u, v):
        return (x + u * k * fx, y + v * k * fy)

    stem = bezier(P(0, 0), P(26, 4), P(46, 18), 30) * SS
    stroke(cv, stem, 1.8, INK, passes=2, wobble=0.2, closed=False)
    leaf(cv, *P(14, 4), *P(30, -8), 6 * k * fx * fy, (170, 140, 92), INK)
    leaf(cv, *P(24, 9), *P(34, 26), -6 * k * fx * fy, (170, 140, 92), INK)
    coffee_bean(cv, *P(48, 21), 7.5 * k, 5.2 * k, 35 * fx * fy, (150, 82, 40), INK_DARK)
    coffee_bean(cv, *P(40, 31), 6.5 * k, 4.6 * k, -20 * fx * fy, (120, 62, 30), INK_DARK)


# ---------------------------------------------------------------- sprites
def tutorial_card(path, w=1500, h=300):
    cv = Canvas(w, h)
    m = 6
    outer = rounded_rect_path(m * SS, m * SS, (w - m) * SS, (h - m) * SS, 14 * SS)
    paper_fill(cv, jitter_path(outer, 1.0 * SS), PAPER, PAPER_EDGE)
    stroke(cv, outer, 4.2, INK, passes=3, wobble=1.3)
    i = 17
    inner = rounded_rect_path(i * SS, i * SS, (w - i) * SS, (h - i) * SS, 8 * SS)
    stroke(cv, inner, 1.6, INK, passes=2, wobble=0.9, opacity=0.8)
    c = 24
    corner_sprig(cv, c, c, 1, 1)
    corner_sprig(cv, w - c, c, -1, 1)
    corner_sprig(cv, c, h - c, 1, -1)
    corner_sprig(cv, w - c, h - c, -1, -1)
    cv.save(path)


def ribbon(path, w=520, h=64):
    """Dark plaque with swallow-tail notches, used behind the tutorial heading."""
    cv = Canvas(w, h)
    s = SS
    n = 22  # notch depth
    pts = [(0, 4), (w, 4), (w - n, h / 2), (w, h - 4), (0, h - 4), (n, h / 2)]
    body = []
    for a, b in zip(pts, pts[1:] + pts[:1]):
        k = max(2, int(math.dist(a, b) / 2))
        body += [(a[0] + (b[0] - a[0]) * t / k, a[1] + (b[1] - a[1]) * t / k) for t in range(k)]
    body = np.array(body, np.float32) * s
    solid_fill(cv, jitter_path(body, 0.6 * s), CHOCOLATE, grain=6)
    stroke(cv, body, 2.4, INK_DARK, passes=2, wobble=0.6)
    inset = rounded_rect_path((n + 12) * s, 11 * s, (w - n - 12) * s, (h - 11) * s, 4 * s)
    stroke(cv, inset, 1.4, CREAM_LINE, passes=2, wobble=0.4, opacity=0.85)
    cv.save(path)


def hint_note(path, w=480, h=200):
    cv = Canvas(w, h)
    m = 6
    outer = rounded_rect_path(m * SS, (m + 4) * SS, (w - m) * SS, (h - m) * SS, 16 * SS)
    paper_fill(cv, jitter_path(outer, 1.0 * SS), (247, 237, 210), PAPER_EDGE, edge_width=18)
    stroke(cv, outer, 3.2, INK, passes=3, wobble=1.1)
    # Burnt-orange pin at the top-left corner.
    pin = np.array([(30 * SS + 11 * SS * math.cos(t), 22 * SS + 11 * SS * math.sin(t))
                    for t in np.linspace(0, 2 * math.pi, 60, endpoint=False)])
    solid_fill(cv, pin, ORANGE, grain=5)
    stroke(cv, pin, 1.8, INK_DARK, passes=2, wobble=0.3)
    hl = np.array([(27 * SS + 3.5 * SS * math.cos(t), 19 * SS + 3.5 * SS * math.sin(t))
                   for t in np.linspace(0, 2 * math.pi, 30, endpoint=False)])
    solid_fill(cv, hl, (250, 200, 150), grain=0)
    cv.save(path)


def button(path, fill, line, inner, w=400, h=96):
    cv = Canvas(w, h)
    m = 5
    outer = rounded_rect_path(m * SS, m * SS, (w - m) * SS, (h - m) * SS, 18 * SS)
    solid_fill(cv, jitter_path(outer, 0.8 * SS), fill, grain=5)
    stroke(cv, outer, 3.0, line, passes=3, wobble=0.9)
    i = 13
    stroke(cv, rounded_rect_path(i * SS, i * SS, (w - i) * SS, (h - i) * SS, 11 * SS),
           1.4, inner, passes=2, wobble=0.5, opacity=0.85)
    cv.save(path)


def soft_round(path, size=64, r=20):
    """White rounded rectangle for tinted mouse-icon parts (9-slice)."""
    big = Image.new("L", (size * 4, size * 4), 0)
    ImageDraw.Draw(big).rounded_rectangle((2, 2, size * 4 - 3, size * 4 - 3), r * 4, fill=255)
    a = big.resize((size, size), Image.LANCZOS)
    im = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    im.putalpha(a)
    im.save(path)
    print("wrote", path, im.size)


def glow_dot(path, size=64):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    d = np.hypot(x - (size - 1) / 2, y - (size - 1) / 2) / (size / 2)
    core = np.clip(1 - d / 0.42, 0, 1) ** 0.6
    halo = np.clip(1 - d, 0, 1) ** 2.2 * 0.55
    a = np.clip(np.maximum(core, halo), 0, 1)
    img = np.dstack([np.full((size, size, 3), 255, np.float32), a * 255]).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(path)
    print("wrote", path, (size, size))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join("Assets", "Art", "UI", "Tutorial")
    os.makedirs(out, exist_ok=True)
    tutorial_card(os.path.join(out, "tutorial_card.png"))
    ribbon(os.path.join(out, "tutorial_ribbon.png"))
    hint_note(os.path.join(out, "tutorial_hint_note.png"))
    button(os.path.join(out, "tutorial_button_primary.png"), CHOCOLATE, INK_DARK, CREAM_LINE)
    button(os.path.join(out, "tutorial_button_secondary.png"), TAN, INK, INK)
    soft_round(os.path.join(out, "tutorial_round.png"))
    glow_dot(os.path.join(out, "tutorial_glow_dot.png"))


if __name__ == "__main__":
    main()
