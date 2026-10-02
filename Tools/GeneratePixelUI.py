# -*- coding: utf-8 -*-
"""
聚光灯2026 / 银星劫 —— 像素风 UI 素材程序化生成器

运行:  python Tools/GeneratePixelUI.py
输出:  Assets/Resources/Art/UI/**  (PNG, 全部为原生低分辨率像素图,
       由 Unity 导入时使用 Point(no filter) 放大, 保证像素锐利)

所有图案均在原生像素网格上逐像素绘制, 不使用抗锯齿插值,
因此放大后呈现干净的方块像素风格。
"""

import math
import os
import random

from PIL import Image, ImageDraw, ImageFont

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
OUT_ROOT = os.path.join(PROJECT_ROOT, "Assets", "Resources", "Art", "UI")

CN_FONT = r"C:\Windows\Fonts\simhei.ttf"
CN_FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"

# ---------------------------------------------------------------- 调色板
T = (0, 0, 0, 0)

OUTLINE = (8, 11, 24, 255)
OUTLINE_SOFT = (30, 39, 70, 255)

SKY_TOP = (10, 14, 34, 255)
SKY_MID = (20, 28, 60, 255)
SKY_LIT = (34, 46, 92, 255)
SKY_LOW = (10, 14, 32, 255)

PANEL_FILL = (32, 42, 82, 255)
PANEL_FILL_D = (23, 30, 60, 255)
PANEL_HI = (60, 78, 134, 255)
PANEL_LO = (14, 19, 40, 255)

GOLD = (232, 194, 90, 255)
GOLD_HI = (255, 236, 158, 255)
GOLD_MID = (196, 156, 62, 255)
GOLD_LO = (140, 102, 36, 255)
GOLD_DEEP = (92, 64, 22, 255)

CREAM = (255, 246, 214, 255)
TEXT = (206, 216, 236, 255)
TEXT_DIM = (126, 140, 172, 255)

BLUE = (112, 170, 232, 255)
BLUE_HI = (176, 216, 255, 255)
BLUE_LO = (54, 92, 152, 255)

RED = (214, 86, 86, 255)
RED_HI = (246, 142, 134, 255)
RED_LO = (118, 40, 44, 255)

GREEN = (120, 206, 140, 255)
GREEN_HI = (188, 240, 192, 255)
GREEN_LO = (50, 108, 68, 255)

BAYER = [
    [0, 8, 2, 10],
    [12, 4, 14, 6],
    [3, 11, 1, 9],
    [15, 7, 13, 5],
]


def new(w, h, fill=T):
    return Image.new("RGBA", (w, h), fill)


def save(img, *parts):
    path = os.path.join(OUT_ROOT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return path


def mix(a, b, t):
    t = 0.0 if t < 0 else (1.0 if t > 1 else t)
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))


def quant(t, levels):
    """把 0..1 量化成 levels 档, 产生像素画特有的色带而不是平滑渐变。"""
    if t <= 0:
        return 0.0
    if t >= 1:
        return 1.0
    return math.floor(t * levels) / float(levels)


def grad(stops, t):
    n = len(stops) - 1
    f = t * n
    i = min(int(f), n - 1)
    return stops[i], stops[i + 1], f - i


# ---------------------------------------------------------------- 基础图元
def bevel_box(w, h, fill, hi, lo, outline=OUTLINE, inner_hi=None):
    """带 1px 外描边 + 左上高光/右下暗面的凸起方块(适合九宫格)。"""
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=fill, outline=outline)
    d.line([(1, 1), (w - 2, 1)], fill=hi)
    d.line([(1, 1), (1, h - 2)], fill=hi)
    d.line([(1, h - 2), (w - 2, h - 2)], fill=lo)
    d.line([(w - 2, 1), (w - 2, h - 2)], fill=lo)
    if inner_hi is not None:
        d.line([(2, 2), (w - 3, 2)], fill=inner_hi)
    return img


def panel(w, h, fill=PANEL_FILL, band=GOLD_LO, stud=GOLD_HI):
    """金色描边的深色面板, 四角有铆钉。九宫格边框 = 12。"""
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=OUTLINE)
    d.rectangle([1, 1, w - 2, h - 2], fill=band)
    d.rectangle([3, 3, w - 4, h - 4], fill=OUTLINE_SOFT)
    d.rectangle([4, 4, w - 5, h - 5], fill=fill)
    d.line([(5, 4), (w - 6, 4)], fill=mix(fill, GOLD, 0.25))
    d.line([(5, h - 5), (w - 6, h - 5)], fill=PANEL_LO)
    for (cx, cy) in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)):
        d.point((cx, cy), fill=stud)
    return img


# ---------------------------------------------------------------- 背景
def paint_sky(img, stops, levels=16):
    """天空渐变: 量化成色带(而不是大面积抖动噪点), 得到干净的像素画天空。"""
    w, h = img.size
    px = img.load()
    for y in range(h):
        t = y / float(h - 1)
        band = min(int(t * levels), levels - 1)
        tt = (band + 0.5) / levels
        a, b, local = grad(stops, tt)
        c = mix(a, b, local)
        for x in range(w):
            px[x, y] = c


def add_stars(px, w, h, count, seed, max_y, bright=1.0):
    r = random.Random(seed)
    for _ in range(count):
        x = r.randrange(w)
        y = r.randrange(max_y)
        v = r.random()
        if v > 0.92:
            px[x, y] = CREAM
            if 0 < x < w - 1:
                px[x - 1, y] = mix(px[x - 1, y], CREAM, 0.4)
                px[x + 1, y] = mix(px[x + 1, y], CREAM, 0.4)
        elif v > 0.62:
            px[x, y] = mix(TEXT_DIM, CREAM, (v - 0.62) / 0.30 * bright)


def spotlight_beam(px, w, cx, apex_y, bottom, half_bottom, color, peak, levels=4):
    """从顶点向下张开的体积光: 由嵌套三角形色带构成, 中轴最亮、向外递减。"""
    span = float(max(1, bottom - apex_y))
    for y in range(apex_y, bottom):
        t = (y - apex_y) / span
        hw = max(1.0, half_bottom * t)
        base = px[int(cx), y]
        x0 = max(0, int(cx - hw))
        x1 = min(w - 1, int(cx + hw))
        for x in range(x0, x1 + 1):
            u = abs(x - cx) / hw
            band = min(int(u * levels), levels - 1)
            strength = peak * (1.0 - band / float(levels))
            if strength <= 0.03:
                continue
            px[x, y] = mix(base, color, strength)


def draw_ground(px, w, h, horizon, top=(20, 26, 50, 255), bottom=(11, 15, 33, 255)):
    """地平线以下的地面, 顶部带一条亮线。"""
    for y in range(horizon, h):
        t = (y - horizon) / float(max(1, h - horizon - 1))
        tone = mix(top, bottom, quant(t, 6))
        for x in range(w):
            px[x, y] = tone
    for x in range(w):
        px[x, horizon] = mix(px[x, horizon], (96, 118, 172, 255), 0.4)


def light_pool(px, w, h, cx, horizon, rx, ry, color, peak, levels=8):
    """地面光斑: 量化成柔和的椭圆色带(轻微抖动打破硬边)。"""
    for y in range(horizon, h):
        dy = (y - horizon) / float(ry)
        if dy >= 1.0:
            continue
        for x in range(w):
            dx = (x - cx) / float(rx)
            dd = math.sqrt(dx * dx + dy * dy)
            if dd >= 1.0:
                continue
            f = (1.0 - dd) ** 1.2
            t = quant(min(1.0, f * peak), levels) + (BAYER[y & 3][x & 3] / 16.0 - 0.5) * 0.03
            if t <= 0.01:
                continue
            px[x, y] = mix(px[x, y], color, min(1.0, t))


def radial_glow(img, cx, cy, radius, color, peak, levels=10):
    """放射状辉光: 量化色带 + 轻微抖动, 避免规则点阵与同心硬环。"""
    w, h = img.size
    px = img.load()
    for y in range(h):
        for x in range(w):
            d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2)
            if d >= radius:
                continue
            f = (1.0 - d / radius) ** 2.0
            t = quant(min(1.0, f * peak), levels) + (BAYER[y & 3][x & 3] / 16.0 - 0.5) * 0.03
            if t <= 0.01:
                continue
            px[x, y] = mix(px[x, y], color, min(1.0, t))


def add_ruins(px, w, horizon, seed):
    """地平线上的废墟剪影 + 零星窗火。"""
    r = random.Random(seed)
    x = -4
    while x < w:
        bw = r.randint(7, 24)
        bh = r.randint(8, 38)
        top = horizon - bh
        for yy in range(max(0, top), horizon):
            for xx in range(max(0, x), min(w, x + bw)):
                px[xx, yy] = (7, 10, 24, 255)
        # 破口
        for _ in range(r.randint(0, 2)):
            gx = r.randint(x, x + bw - 2)
            gh = r.randint(2, 6)
            for yy in range(max(0, top), min(horizon, top + gh)):
                if 0 <= gx < w:
                    px[gx, yy] = (14, 20, 42, 255)
        # 窗火
        for wy in range(top + 3, horizon - 2, 4):
            for wx in range(x + 2, x + bw - 1, 3):
                if 0 <= wx < w and r.random() > 0.55:
                    px[wx, wy] = r.choice([(232, 194, 90, 255), (196, 150, 70, 255), (150, 190, 220, 255)])
        x += bw + r.randint(1, 7)


def add_motes(px, w, h, count, seed):
    """漂浮光尘。"""
    r = random.Random(seed)
    for _ in range(count):
        x = r.randrange(w)
        y = r.randrange(int(h * 0.15), int(h * 0.85))
        c = mix(GOLD, CREAM, r.random())
        px[x, y] = c
        if r.random() > 0.6 and x + 1 < w:
            px[x + 1, y] = mix(c, GOLD_LO, 0.5)


def vignette(img, strength=0.55, color=(6, 9, 20, 255), levels=8):
    w, h = img.size
    px = img.load()
    cx, cy = w / 2.0, h / 2.0
    maxd = math.sqrt(cx * cx + cy * cy)
    for y in range(h):
        for x in range(w):
            d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2) / maxd
            t = max(0.0, (d - 0.42) / 0.58) * strength
            t = quant(t, levels) + (BAYER[y & 3][x & 3] / 16.0 - 0.5) * 0.035
            if t > 0.01:
                px[x, y] = mix(px[x, y], color, min(1.0, t))


def bg_main_menu():
    w, h = 320, 180
    horizon = 142
    img = new(w, h)
    px = img.load()
    paint_sky(img, [SKY_TOP, SKY_MID, SKY_LIT, SKY_MID, SKY_LOW])
    add_stars(px, w, h, 130, 7, 118)
    draw_ground(px, w, h, horizon)
    add_ruins(px, w, horizon, 11)
    spotlight_beam(px, w, 160, 0, horizon, 126, (255, 232, 158, 255), 0.58, levels=5)
    light_pool(px, w, h, 160, horizon, 62, 28, (255, 226, 150, 255), 0.46)
    add_motes(px, w, h, 24, 23)
    vignette(img, 0.5)
    return img


def bg_loading():
    w, h = 320, 180
    img = new(w, h)
    px = img.load()
    paint_sky(img, [(8, 11, 28, 255), (14, 20, 46, 255), (22, 31, 64, 255), (12, 17, 38, 255)])
    add_stars(px, w, h, 110, 31, 150, bright=0.8)
    radial_glow(img, 160, 78, 80, (255, 206, 96, 255), 0.40)
    for y in range(0, h, 4):
        for x in range(w):
            px[x, y] = mix(px[x, y], (150, 180, 230, 255), 0.05)
    vignette(img, 0.55)
    return img


def bg_menu_generic():
    """暂停/存档/设置/确认等菜单共用底图: 更暗更安静, 便于面板阅读。"""
    w, h = 320, 180
    img = new(w, h)
    px = img.load()
    paint_sky(img, [(9, 12, 30, 255), (16, 22, 50, 255), (24, 33, 68, 255), (10, 14, 32, 255)])
    add_stars(px, w, h, 80, 47, 120, bright=0.6)
    spotlight_beam(px, w, 160, 0, h, 172, (140, 168, 230, 255), 0.22, levels=5)
    add_motes(px, w, h, 14, 53)
    vignette(img, 0.68)
    return img


def bg_dim_overlay():
    """半透明压暗+暗角遮罩, 叠在游戏画面上方。"""
    w, h = 320, 180
    img = new(w, h, (5, 8, 18, 0))
    px = img.load()
    cx, cy = w / 2.0, h / 2.0
    maxd = math.sqrt(cx * cx + cy * cy)
    for y in range(h):
        for x in range(w):
            d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2) / maxd
            a = 120 + int(quant(max(0.0, (d - 0.3) / 0.7), 6) * 105)
            px[x, y] = (5, 8, 18, max(0, min(255, a)))
    return img


# ---------------------------------------------------------------- 图标引擎
class Mask(object):
    def __init__(self, w=32, h=32):
        self.w = w
        self.h = h
        self.m = [[False] * w for _ in range(h)]

    def set(self, x, y, v=True):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.m[y][x] = v

    def get(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h and self.m[y][x]

    def rect(self, x0, y0, x1, y1, v=True):
        for y in range(min(int(y0), int(y1)), max(int(y0), int(y1)) + 1):
            for x in range(min(int(x0), int(x1)), max(int(x0), int(x1)) + 1):
                self.set(x, y, v)

    def poly(self, pts, v=True):
        ys = [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xs = []
            n = len(pts)
            for i in range(n):
                x1, y1 = pts[i]
                x2, y2 = pts[(i + 1) % n]
                if (y1 <= y < y2) or (y2 <= y < y1):
                    t = (y - y1) / float(y2 - y1)
                    xs.append(x1 + (x2 - x1) * t)
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for x in range(int(round(xs[i])), int(round(xs[i + 1])) + 1):
                    self.set(x, y, v)

    def disc(self, cx, cy, r, v=True):
        for y in range(int(cy - r), int(cy + r) + 1):
            for x in range(int(cx - r), int(cx + r) + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                    self.set(x, y, v)

    def ring(self, cx, cy, r, thick=1.0, v=True):
        for y in range(int(cy - r - thick), int(cy + r + thick) + 1):
            for x in range(int(cx - r - thick), int(cx + r + thick) + 1):
                d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2)
                if r - thick < d <= r:
                    self.set(x, y, v)

    def sub(self, other):
        for y in range(self.h):
            for x in range(self.w):
                if other.get(x, y):
                    self.m[y][x] = False
        return self

    def add(self, other):
        for y in range(self.h):
            for x in range(self.w):
                if other.get(x, y):
                    self.m[y][x] = True
        return self

    def star(self, cx, cy, r_out, r_in, points=5, rot=-math.pi / 2):
        pts = []
        for i in range(points * 2):
            a = rot + i * math.pi / points
            r = r_out if i % 2 == 0 else r_in
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        self.poly(pts)


def render(mask, fill, outline=OUTLINE, hi=None, lo=None):
    img = new(mask.w, mask.h)
    px = img.load()
    for y in range(mask.h):
        for x in range(mask.w):
            if mask.get(x, y):
                continue
            near = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if mask.get(x + dx, y + dy):
                        near = True
                        break
                if near:
                    break
            if near:
                px[x, y] = outline
    for y in range(mask.h):
        for x in range(mask.w):
            if not mask.get(x, y):
                continue
            if hi is not None and not mask.get(x, y - 1):
                px[x, y] = hi
            elif lo is not None and not mask.get(x, y + 1):
                px[x, y] = lo
            else:
                px[x, y] = fill
    return img


def paint(img, mask, color):
    px = img.load()
    for y in range(mask.h):
        for x in range(mask.w):
            if mask.get(x, y):
                px[x, y] = color


def icon_continue():
    m = Mask()
    m.poly([(10, 6), (10, 25), (26, 15.5)])
    return render(m, GOLD, hi=GOLD_HI, lo=GOLD_LO)


def icon_new_game():
    m = Mask()
    c = Mask()
    c.ring(16, 16, 13, 2.0)
    body = Mask()
    body.poly([(13, 9), (13, 23), (24, 16)])
    m.add(c).add(body)
    return render(m, PANEL_FILL, hi=GOLD, lo=GOLD_LO)


# 用于金色高亮按钮上的深色变体(浅色描边, 保证在亮底上依然清晰)
def icon_continue_dark():
    m = Mask()
    m.poly([(10, 6), (10, 25), (26, 15.5)])
    return render(m, (26, 33, 62, 255), outline=CREAM, hi=(52, 64, 104, 255), lo=(12, 16, 34, 255))


def icon_new_game_dark():
    m = Mask()
    c = Mask()
    c.ring(16, 16, 13, 2.0)
    body = Mask()
    body.poly([(13, 9), (13, 23), (24, 16)])
    m.add(c).add(body)
    return render(m, (26, 33, 62, 255), outline=CREAM, hi=(52, 64, 104, 255), lo=(12, 16, 34, 255))


def icon_save():
    body = Mask()
    body.rect(6, 5, 25, 26)
    img = render(body, BLUE, hi=BLUE_HI, lo=BLUE_LO)
    shutter = Mask()
    shutter.rect(11, 5, 20, 13)
    shutter.rect(15, 6, 16, 12, False)
    paint(img, shutter, TEXT_DIM)
    label = Mask()
    label.rect(9, 17, 22, 25)
    paint(img, label, CREAM)
    lines = Mask()
    for ly in (19, 21, 23):
        lines.rect(11, ly, 20, ly)
    paint(img, lines, BLUE_LO)
    return img


def icon_load():
    m = Mask()
    m.poly([(4, 12), (12, 12), (15, 9), (20, 9), (20, 13), (4, 13)])
    m.rect(4, 12, 27, 24)
    img = render(m, GOLD_MID, hi=GOLD_HI, lo=GOLD_DEEP)
    inner = Mask()
    inner.rect(6, 14, 25, 22)
    paint(img, inner, mix(GOLD_HI, GOLD, 0.35))
    return img


def icon_delete():
    lid = Mask()
    lid.rect(7, 8, 24, 10)
    lid.rect(13, 5, 18, 8)
    body = Mask()
    body.poly([(9, 11), (22, 11), (20, 27), (11, 27)])
    m = Mask()
    m.add(lid).add(body)
    img = render(m, RED, hi=RED_HI, lo=RED_LO)
    bars = Mask()
    for bx in (12, 15, 18):
        bars.rect(bx, 14, bx, 24)
    paint(img, bars, RED_LO)
    return img


def icon_settings():
    core = Mask()
    core.disc(16, 16, 10)
    teeth = Mask()
    for k in range(8):
        a = k * math.pi / 4
        cx = 16 + math.cos(a) * 11
        cy = 16 + math.sin(a) * 11
        teeth.disc(cx, cy, 3.2)
    hole = Mask()
    hole.disc(16, 16, 4.2)
    m = Mask()
    m.add(core).add(teeth).sub(hole)
    return render(m, TEXT, hi=CREAM, lo=TEXT_DIM)


def icon_exit():
    door = Mask()
    door.rect(5, 6, 17, 27)
    img = render(door, PANEL_FILL_D, hi=PANEL_HI, lo=PANEL_LO)
    open_door = Mask()
    open_door.rect(8, 9, 15, 24)
    paint(img, open_door, mix(GOLD_LO, OUTLINE, 0.4))
    arrow = Mask()
    arrow.rect(17, 15, 25, 17)
    arrow.poly([(21, 10), (21, 22), (30, 16)])
    img2 = render(arrow, GOLD, hi=GOLD_HI, lo=GOLD_LO)
    img.alpha_composite(img2)
    return img


def icon_back():
    m = Mask()
    m.rect(8, 14, 25, 17)
    m.poly([(3, 15.5), (14, 6), (14, 25)])
    return render(m, TEXT, hi=CREAM, lo=TEXT_DIM)


def icon_arrow_right():
    m = Mask()
    m.rect(6, 14, 23, 17)
    m.poly([(28, 15.5), (17, 6), (17, 25)])
    return render(m, TEXT, hi=CREAM, lo=TEXT_DIM)


def icon_arrow_left():
    m = Mask()
    m.rect(8, 14, 25, 17)
    m.poly([(3, 15.5), (14, 6), (14, 25)])
    return render(m, TEXT, hi=CREAM, lo=TEXT_DIM)


def icon_archive():
    """存档箱: 三层抽屉。"""
    body = Mask()
    body.rect(4, 5, 27, 26)
    img = render(body, PANEL_FILL, hi=PANEL_HI, lo=PANEL_LO)
    for ry in (7, 13, 19):
        slot = Mask()
        slot.rect(7, ry, 24, ry + 5)
        paint(img, slot, OUTLINE_SOFT)
        inner = Mask()
        inner.rect(8, ry + 1, 23, ry + 4)
        paint(img, inner, GOLD_LO if ry == 13 else mix(PANEL_FILL, OUTLINE, 0.5))
    handle = Mask()
    for ry in (9, 15, 21):
        handle.rect(15, ry, 17, ry + 1)
    paint(img, handle, GOLD_HI)
    return img


def icon_autosave():
    img = icon_save()
    badge = Mask()
    badge.disc(24, 24, 7)
    bimg = render(badge, PANEL_FILL, hi=PANEL_HI, lo=PANEL_LO)
    img.alpha_composite(bimg)
    hand = Mask()
    hand.rect(23, 19, 24, 24)
    hand.rect(24, 23, 28, 24)
    himg = render(hand, GOLD_HI, outline=OUTLINE)
    img.alpha_composite(himg)
    return img


def icon_clock():
    m = Mask()
    m.ring(16, 16, 12, 2.0)
    hand = Mask()
    hand.rect(15, 9, 16, 17)
    hand.rect(15, 15, 22, 16)
    m.add(hand)
    return render(m, TEXT, hi=CREAM, lo=TEXT_DIM)


def icon_check():
    m = Mask()
    m.poly([(5, 17), (9, 13), (14, 18), (23, 8), (27, 12), (14, 26)])
    return render(m, GREEN, hi=GREEN_HI, lo=GREEN_LO)


def icon_cross():
    m = Mask()
    m.poly([(7, 5), (16, 13), (25, 5), (28, 9), (19, 16), (28, 23), (25, 27), (16, 19), (7, 27), (4, 23), (12, 16), (4, 9)])
    return render(m, RED, hi=RED_HI, lo=RED_LO)


def icon_volume():
    body = Mask()
    body.poly([(6, 12), (11, 12), (17, 6), (17, 26), (11, 20), (6, 20)])
    img = render(body, TEXT, hi=CREAM, lo=TEXT_DIM)
    wave = Mask()
    wave.ring(15, 16, 9, 1.6)
    wimg = render(wave, GOLD)
    px = wimg.load()
    for y in range(32):
        for x in range(0, 22):
            wimg.putpixel((x, y), T)
    img.alpha_composite(wimg)
    return img


def icon_music():
    m = Mask()
    m.disc(11, 24, 4.6)
    m.disc(21, 21, 4.6)
    m.rect(14, 6, 16, 24)
    m.rect(23, 4, 25, 21)
    m.poly([(14, 6), (25, 3), (25, 8), (14, 11)])
    return render(m, GOLD, hi=GOLD_HI, lo=GOLD_LO)


def icon_sfx():
    m = Mask()
    m.poly([(6, 12), (11, 12), (17, 6), (17, 26), (11, 20), (6, 20)])
    img = render(m, BLUE, hi=BLUE_HI, lo=BLUE_LO)
    arcs = Mask()
    arcs.ring(16, 16, 8, 1.6)
    arcs.ring(16, 16, 12, 1.6)
    aimg = render(arcs, GOLD_HI)
    px = aimg.load()
    for y in range(32):
        for x in range(0, 18):
            aimg.putpixel((x, y), T)
    img.alpha_composite(aimg)
    return img


def icon_display():
    body = Mask()
    body.rect(3, 6, 28, 22)
    img = render(body, PANEL_FILL_D, hi=PANEL_HI, lo=PANEL_LO)
    screen = Mask()
    screen.rect(5, 8, 26, 20)
    paint(img, screen, mix(BLUE_LO, OUTLINE, 0.35))
    glint = Mask()
    glint.poly([(6, 19), (13, 9), (18, 9), (9, 19)])
    paint(img, glint, mix(BLUE_LO, BLUE_HI, 0.35))
    stand = Mask()
    stand.rect(13, 23, 18, 26)
    stand.rect(9, 26, 22, 28)
    simg = render(stand, PANEL_FILL, hi=PANEL_HI, lo=PANEL_LO)
    img.alpha_composite(simg)
    return img


def icon_resolution():
    img = icon_display()
    arrows = Mask()
    arrows.poly([(4, 4), (4, 10), (10, 4)])
    arrows.poly([(28, 4), (28, 10), (22, 4)])
    a = render(arrows, GOLD_HI)
    img.alpha_composite(a)
    return img


def icon_fullscreen():
    m = Mask()
    for (ox, oy, sx, sy) in ((3, 3, 1, 1), (28, 3, -1, 1), (3, 28, 1, -1), (28, 28, -1, -1)):
        m.rect(ox, oy, ox + sx * 8, oy + sy * 2)
        m.rect(ox, oy, ox + sx * 2, oy + sy * 8)
    return render(m, GOLD, hi=GOLD_HI, lo=GOLD_LO)


def icon_windowed():
    m = Mask()
    m.rect(3, 6, 28, 26)
    img = render(m, PANEL_FILL, hi=PANEL_HI, lo=PANEL_LO)
    bar = Mask()
    bar.rect(4, 7, 27, 12)
    paint(img, bar, BLUE_LO)
    dots = Mask()
    dots.rect(6, 9, 7, 10)
    dots.rect(9, 9, 10, 10)
    dots.rect(12, 9, 13, 10)
    paint(img, dots, CREAM)
    return img


def icon_borderless():
    m = Mask()
    for x in range(4, 28):
        if ((x - 4) // 2) % 2 == 0:
            for yy in (5, 6, 25, 26):
                m.set(x, yy)
    for y in range(4, 28):
        if ((y - 4) // 2) % 2 == 0:
            for xx in (4, 5, 26, 27):
                m.set(xx, y)
    return render(m, TEXT_DIM, hi=None, lo=None)


def icon_star():
    m = Mask()
    m.star(16, 16, 13, 5.4)
    return render(m, GOLD, hi=GOLD_HI, lo=GOLD_LO)


ICONS = {
    "ic_continue": icon_continue,
    "ic_continue_dark": icon_continue_dark,
    "ic_new_game": icon_new_game,
    "ic_new_game_dark": icon_new_game_dark,
    "ic_save": icon_save,
    "ic_load": icon_load,
    "ic_delete": icon_delete,
    "ic_settings": icon_settings,
    "ic_exit": icon_exit,
    "ic_back": icon_back,
    "ic_arrow_right": icon_arrow_right,
    "ic_arrow_left": icon_arrow_left,
    "ic_archive": icon_archive,
    "ic_autosave": icon_autosave,
    "ic_clock": icon_clock,
    "ic_check": icon_check,
    "ic_cross": icon_cross,
    "ic_volume": icon_volume,
    "ic_music": icon_music,
    "ic_sfx": icon_sfx,
    "ic_display": icon_display,
    "ic_resolution": icon_resolution,
    "ic_fullscreen": icon_fullscreen,
    "ic_windowed": icon_windowed,
    "ic_borderless": icon_borderless,
    "ic_star": icon_star,
}


# ---------------------------------------------------------------- 控件
def slider_track(w=64, h=10):
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 2, w - 1, h - 3], fill=OUTLINE)
    d.rectangle([1, 3, w - 2, h - 4], fill=PANEL_FILL_D)
    d.line([(1, 3), (w - 2, 3)], fill=PANEL_LO)
    return img


def slider_fill(w=8, h=10):
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 2, w - 1, h - 3], fill=GOLD_LO)
    d.rectangle([0, 2, w - 1, (h - 1) // 2], fill=GOLD)
    i = 0
    for x in range(0, w, 3):
        d.line([(x, 3), (x, h - 4)], fill=GOLD_HI if i % 2 == 0 else GOLD_MID)
        i += 1
    d.line([(0, 3), (w - 1, 3)], fill=GOLD_HI)
    return img


def slider_knob(s=18):
    m = Mask(s, s)
    m.disc(s / 2.0 - 0.5, s / 2.0 - 0.5, s / 2.0 - 1.0)
    img = render(m, GOLD, hi=GOLD_HI, lo=GOLD_LO)
    d = ImageDraw.Draw(img)
    d.rectangle([s // 2 - 3, s // 2 - 1, s // 2 + 2, s // 2], fill=CREAM)
    d.rectangle([s // 2 - 1, s // 2 - 3, s // 2, s // 2 + 2], fill=CREAM)
    return img


def dropdown(w=64, h=26, hover=False):
    fill = PANEL_FILL if not hover else mix(PANEL_FILL, GOLD, 0.18)
    hi = PANEL_HI if not hover else GOLD_MID
    return bevel_box(w, h, fill, hi, PANEL_LO, inner_hi=mix(fill, hi, 0.45))


def dropdown_arrow(s=16):
    m = Mask(s, s)
    m.poly([(3, 6), (s - 4, 6), (s / 2.0 - 0.5, s - 5)])
    return render(m, GOLD, hi=GOLD_HI, lo=GOLD_LO)


def checkbox(on, s=22):
    img = bevel_box(s, s, PANEL_FILL_D, PANEL_HI, PANEL_LO)
    if on:
        m = Mask(s, s)
        m.poly([(4, 11), (8, 7), (12, 12), (18, 5), (19, 8), (12, 17)])
        tick = render(m, GREEN, hi=GREEN_HI, lo=GREEN_LO)
        img.alpha_composite(tick)
    return img


def progress_track(w=96, h=14):
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=OUTLINE)
    d.rectangle([1, 1, w - 2, h - 2], fill=PANEL_FILL_D)
    d.line([(2, 2), (w - 3, 2)], fill=PANEL_LO)
    return img


def progress_fill(w=16, h=14):
    img = new(w, h)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], fill=GOLD_LO)
    d.rectangle([0, 0, w - 1, h // 2], fill=GOLD)
    for x in range(0, w, 4):
        d.line([(x, 1), (x, h - 2)], fill=GOLD_HI)
    return img


# ---------------------------------------------------------------- 文字 Logo
def text_mask(text, size, font_path=CN_FONT, threshold=110):
    try:
        font = ImageFont.truetype(font_path, size)
    except Exception:
        font = ImageFont.load_default()
    tmp = Image.new("L", (8, 8))
    d = ImageDraw.Draw(tmp)
    bbox = d.textbbox((0, 0), text, font=font)
    pad = 3
    w = max(1, bbox[2] - bbox[0]) + pad * 2
    h = max(1, bbox[3] - bbox[1]) + pad * 2
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    d.text((pad - bbox[0], pad - bbox[1]), text, font=font, fill=255)
    m = m.point(lambda v: 255 if v > threshold else 0)
    mask = Mask(w, h)
    mp = m.load()
    for y in range(h):
        for x in range(w):
            if mp[x, y] > 0:
                mask.set(x, y)
    return mask


def logo_text(text, size, fill=GOLD, hi=CREAM, lo=GOLD_LO, outline=OUTLINE,
              font_path=CN_FONT_BOLD, threshold=128):
    """把中文渲染成像素化金属字: 纯色填充 + 1px 描边 + 上高光/下暗面 + 投影。

    注意: 不要在图元内部做抖动, 否则笔画会被噪点糊掉导致无法辨识。
    """
    mask = text_mask(text, size, font_path, threshold)
    w, h = mask.w, mask.h
    img = new(w + 3, h + 4)
    # 投影(偏移 1,2)
    shadow = new(w + 3, h + 4)
    for y in range(h):
        for x in range(w):
            if mask.get(x, y):
                shadow.putpixel((x + 2, y + 3), (4, 6, 14, 190))
    img.alpha_composite(shadow)
    # 1px 外描边
    for y in range(h):
        for x in range(w):
            if mask.get(x, y):
                continue
            near = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if mask.get(x + dx, y + dy):
                        near = True
                        break
                if near:
                    break
            if near:
                img.putpixel((x + 1, y + 1), outline)
    # 字面: 上缘高光 / 下缘暗面 / 主体纯色
    for y in range(h):
        for x in range(w):
            if not mask.get(x, y):
                continue
            if not mask.get(x, y - 1):
                img.putpixel((x + 1, y + 1), hi)
            elif not mask.get(x, y + 1):
                img.putpixel((x + 1, y + 1), lo)
            else:
                img.putpixel((x + 1, y + 1), fill)
    return img


# ---------------------------------------------------------------- 加载动画
def loading_frame(index, total=8, size=64):
    img = new(size, size)
    px = img.load()
    cx = cy = (size - 1) / 2.0
    phase = index / float(total) * 2 * math.pi

    # 呼吸辉光: 保留柔和的半透明过渡, 叠加轻微抖动以打破同心圆环
    pulse = 0.5 + 0.5 * math.sin(phase)
    glow_r = 21 + 3 * pulse
    for y in range(size):
        for x in range(size):
            d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2)
            if d >= glow_r:
                continue
            f = (1.0 - d / glow_r) ** 2.0
            t = min(1.0, f * 0.58) + (BAYER[y & 3][x & 3] / 16.0 - 0.5) * 0.07
            t = max(0.0, min(1.0, t))
            if t <= 0.01:
                continue
            px[x, y] = mix(px[x, y], (255, 206, 96, 255), t)

    # 旋转光束
    for k in range(8):
        ang = phase + k * math.pi / 4
        length = 21 + 7 * math.sin(phase * 2 + k * 0.9)
        for r in range(12, int(length) + 1):
            x = int(round(cx + math.cos(ang) * r))
            y = int(round(cy + math.sin(ang) * r))
            if 0 <= x < size and 0 <= y < size:
                lvl = 1.0 - (r - 12) / float(max(1, length - 12))
                px[x, y] = mix(GOLD_MID, GOLD_HI, lvl)

    # 旋转点环
    ring_r = 19
    for k in range(12):
        a = phase * 1.0 + k * math.pi / 6
        x = int(round(cx + math.cos(a) * ring_r))
        y = int(round(cy + math.sin(a) * ring_r))
        v = 0.35 + 0.65 * (0.5 + 0.5 * math.sin(a * 3 + phase * 2))
        c = mix(GOLD_DEEP, CREAM, v)
        if 0 <= x < size and 0 <= y < size:
            px[x, y] = c

    # 中央星徽
    star = Mask(size, size)
    star.star(cx, cy, 13, 5.2)
    rot = math.sin(phase) * 0.12
    star2 = Mask(size, size)
    star2.star(cx, cy, 13, 5.2, rot=-math.pi / 2 + rot)
    simg = render(star2, GOLD, hi=GOLD_HI, lo=GOLD_LO)
    img.alpha_composite(simg)

    core = Mask(size, size)
    core.disc(cx, cy, 3.4)
    cimg = render(core, CREAM if pulse > 0.5 else GOLD_HI)
    img.alpha_composite(cimg)
    return img


def loading_sheet(frames):
    w, h = frames[0].size
    sheet = new(w * len(frames), h)
    for i, f in enumerate(frames):
        sheet.paste(f, (i * w, 0))
    return sheet


# ---------------------------------------------------------------- 主流程
def main():
    # ---- 背景
    save(bg_main_menu(), "Backgrounds", "bg_main_menu.png")
    save(bg_loading(), "Backgrounds", "bg_loading.png")
    save(bg_menu_generic(), "Backgrounds", "bg_menu_generic.png")
    save(bg_dim_overlay(), "Backgrounds", "bg_dim_overlay.png")

    # ---- 面板 (九宫格 border=12)
    save(panel(72, 72), "Panels", "panel_large.png")
    save(panel(48, 48), "Panels", "panel_medium.png")
    save(panel(48, 48, fill=PANEL_FILL_D, band=GOLD_MID, stud=CREAM), "Panels", "panel_dialog.png")

    # ---- 按钮 (九宫格 border=8)
    bw, bh = 64, 28
    save(bevel_box(bw, bh, PANEL_FILL, PANEL_HI, PANEL_LO, inner_hi=mix(PANEL_FILL, PANEL_HI, 0.45)),
         "Buttons", "btn_normal.png")
    save(bevel_box(bw, bh, (48, 62, 110, 255), GOLD_HI, PANEL_LO, inner_hi=GOLD_MID),
         "Buttons", "btn_hover.png")
    save(bevel_box(bw, bh, mix(PANEL_FILL, OUTLINE, 0.35), PANEL_LO, OUTLINE, inner_hi=PANEL_LO),
         "Buttons", "btn_pressed.png")
    save(bevel_box(bw, bh, (20, 26, 48, 255), (48, 58, 92, 255), (14, 18, 36, 255)),
         "Buttons", "btn_disabled.png")

    save(bevel_box(bw, bh, GOLD, GOLD_HI, GOLD_DEEP, inner_hi=CREAM), "Buttons", "btn_accent_normal.png")
    save(bevel_box(bw, bh, GOLD_HI, CREAM, GOLD_LO, inner_hi=CREAM), "Buttons", "btn_accent_hover.png")
    save(bevel_box(bw, bh, GOLD_LO, GOLD_MID, GOLD_DEEP, inner_hi=GOLD), "Buttons", "btn_accent_pressed.png")
    save(bevel_box(bw, bh, (86, 70, 40, 255), (120, 100, 60, 255), (52, 42, 24, 255)),
         "Buttons", "btn_accent_disabled.png")

    save(bevel_box(bw, bh, mix(PANEL_FILL, RED, 0.3), RED_HI, RED_LO, inner_hi=RED), "Buttons", "btn_danger_normal.png")
    save(bevel_box(bw, bh, mix(PANEL_FILL, RED, 0.5), RED_HI, RED_LO, inner_hi=RED_HI), "Buttons", "btn_danger_hover.png")
    save(bevel_box(bw, bh, RED_LO, RED, RED_LO, inner_hi=RED_LO), "Buttons", "btn_danger_pressed.png")

    sw, sh = 52, 24
    save(bevel_box(sw, sh, PANEL_FILL, PANEL_HI, PANEL_LO), "Buttons", "btn_small_normal.png")
    save(bevel_box(sw, sh, mix(PANEL_FILL, GOLD, 0.22), GOLD_HI, PANEL_LO), "Buttons", "btn_small_hover.png")
    save(bevel_box(sw, sh, mix(PANEL_FILL, OUTLINE, 0.35), PANEL_LO, OUTLINE), "Buttons", "btn_small_pressed.png")

    # ---- 存档槽 (九宫格 border=10)
    tw, th = 72, 44
    save(bevel_box(tw, th, PANEL_FILL_D, PANEL_HI, PANEL_LO), "Slots", "slot_normal.png")
    save(bevel_box(tw, th, mix(PANEL_FILL_D, GOLD, 0.18), GOLD_HI, PANEL_LO), "Slots", "slot_hover.png")
    save(bevel_box(tw, th, mix(PANEL_FILL_D, GOLD, 0.3), GOLD_HI, GOLD_DEEP, inner_hi=GOLD_MID), "Slots", "slot_selected.png")
    save(bevel_box(tw, th, mix(PANEL_FILL_D, GOLD, 0.24), GOLD_HI, GOLD_LO, inner_hi=GOLD_MID), "Slots", "slot_autosave.png")

    empty = new(tw, th)
    d = ImageDraw.Draw(empty)
    for x in range(3, tw - 3, 6):
        d.line([(x, 3), (x, th - 4)], fill=(42, 52, 84, 255))
    for y in range(3, th - 3, 6):
        d.line([(3, y), (tw - 4, y)], fill=(42, 52, 84, 255))
    d.rectangle([0, 0, tw - 1, th - 1], outline=OUTLINE)
    save(empty, "Slots", "slot_empty.png")

    # ---- 控件
    save(slider_track(), "Widgets", "slider_track.png")
    save(slider_fill(), "Widgets", "slider_fill.png")
    save(slider_knob(), "Widgets", "slider_knob.png")
    save(dropdown(72, 28, False), "Widgets", "dropdown_normal.png")
    save(dropdown(72, 28, True), "Widgets", "dropdown_hover.png")
    save(dropdown_arrow(), "Widgets", "dropdown_arrow.png")
    save(checkbox(False), "Widgets", "checkbox_off.png")
    save(checkbox(True), "Widgets", "checkbox_on.png")
    save(progress_track(), "Widgets", "progress_track.png")
    save(progress_fill(), "Widgets", "progress_fill.png")

    # ---- 图标
    for name, fn in ICONS.items():
        save(fn(), "Icons", name + ".png")

    # ---- Logo
    save(logo_text("银星劫", 48), "Logo", "logo_title.png")
    save(logo_text("聚光灯 2026", 20, fill=TEXT, hi=CREAM, lo=TEXT_DIM,
                   outline=(6, 9, 20, 255), font_path=CN_FONT, threshold=120),
         "Logo", "logo_subtitle.png")

    frames = [loading_frame(i) for i in range(8)]
    for i, f in enumerate(frames):
        save(f, "Logo", "loading_logo_%02d.png" % i)
    save(loading_sheet(frames), "Logo", "loading_logo_sheet.png")

    print("generated into: " + OUT_ROOT)


if __name__ == "__main__":
    main()
