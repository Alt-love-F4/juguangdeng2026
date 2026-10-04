"""程序化绘制咖啡店 2D 美术占位图（扁平卡通风格）。
直接生成带透明通道的道具图与不透明背景图，AI 美术后续可覆盖替换。
"""
import os
from PIL import Image, ImageDraw

OUT = r"e:\myproject\unity_p_taptap2026\Assets\Art\Gameplay"
SS = 2  # 2x 超采样抗锯齿


def canvas(w, h, alpha=True):
    mode = "RGBA"
    bg = (0, 0, 0, 0) if alpha else (255, 255, 255, 255)
    return Image.new(mode, (w * SS, h * SS), bg)


def finish(im, path, w, h):
    im = im.resize((w, h), Image.LANCZOS)
    im.save(path)
    print("drawn", os.path.basename(path))


def E(d, box, fill, outline=None, width=0):
    d.ellipse([v * SS for v in box], fill=fill, outline=outline, width=width * SS)


def R(d, box, fill, radius=0, outline=None, width=0):
    d.rounded_rectangle([v * SS for v in box], radius=radius * SS, fill=fill, outline=outline, width=width * SS)


def P(d, pts, fill):
    d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)


def L(d, pts, fill, width):
    d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=width * SS)


# ---------------- 背景 1920x1080 ----------------

def draw_background():
    W, H = 1920, 1080
    im = canvas(W, H, alpha=False)
    d = ImageDraw.Draw(im)

    wall_top = (246, 230, 205, 255)
    wall_bottom = (238, 214, 178, 255)
    for y in range(H * 2 // 3):
        t = y / (H * 2 / 3)
        c = tuple(int(wall_top[i] + (wall_bottom[i] - wall_top[i]) * t) for i in range(4))
        d.line([(0, y * SS), (W * SS, y * SS)], fill=c, width=SS)

    # 地板
    R(d, [0, H * 2 // 3, W, H], (139, 94, 60, 255))
    for i in range(12):
        y = H * 2 // 3 + i * 32
        L(d, [(0, y), (W, y)], (120, 78, 48, 255), 3)

    # 左窗
    R(d, [120, 130, 520, 560], (190, 220, 235, 255), radius=18, outline=(120, 90, 60, 255), width=10)
    L(d, [(320, 130), (320, 560)], (120, 90, 60, 255), 10)
    L(d, [(120, 345), (520, 345)], (120, 90, 60, 255), 10)
    E(d, [170, 170, 260, 240], (245, 240, 200, 255))  # 阳光

    # 后墙置物架 x2
    for sy in (210, 360):
        R(d, [760, sy + 66, 1420, sy + 82], (150, 105, 70, 255), radius=4)
        for i, x in enumerate(range(800, 1380, 90)):
            if (i + sy) % 3 == 0:  # 罐子
                R(d, [x, sy + 10, x + 44, sy + 66], (214, 170, 120, 255), radius=8, outline=(120, 85, 55, 255), width=4)
            elif (i + sy) % 3 == 1:  # 杯子
                R(d, [x, sy + 26, x + 40, sy + 66], (235, 240, 240, 255), radius=6, outline=(140, 120, 100, 255), width=4)
            else:  # 绿植
                R(d, [x + 6, sy + 34, x + 38, sy + 66], (170, 90, 60, 255), radius=4)
                E(d, [x - 2, sy, x + 46, sy + 40], (110, 150, 90, 255))

    # 黑板菜单
    R(d, [1500, 170, 1810, 430], (60, 80, 70, 255), radius=10, outline=(130, 95, 60, 255), width=10)
    for i in range(4):
        L(d, [(1540, 230 + i * 48), (1770, 230 + i * 48)], (220, 225, 215, 255), 4)

    # 吊灯 x3
    for x in (700, 1100, 1500):
        L(d, [(x, 0), (x, 96)], (90, 70, 50, 255), 6)
        P(d, [(x - 56, 150), (x + 56, 150), (x + 30, 96), (x - 30, 96)], (200, 130, 70, 255))
        E(d, [x - 16, 150, x + 16, 182], (255, 235, 180, 255))

    # 吧台（前景下三分之一）
    R(d, [0, 760, W, H], (160, 105, 65, 255))
    R(d, [0, 730, W, 790], (190, 135, 85, 255), radius=8)  # 台面
    for i in range(8):  # 吧台前板竖纹
        x = 120 + i * 240
        L(d, [(x, 800), (x, H - 20)], (140, 90, 55, 255), 5)

    finish(im, os.path.join(OUT, "bg_coffee_shop.png"), W, H)


# ---------------- 顾客 512x512 ----------------

def draw_customer(path, skin, hair, cloth, kind):
    S = 512
    im = canvas(S, S)
    d = ImageDraw.Draw(im)
    # 身体
    P(d, [(156, 512), (176, 340), (336, 340), (356, 512)], cloth)
    R(d, [176, 330, 336, 380], cloth, radius=20)
    # 头
    E(d, [166, 120, 346, 300], skin)
    if kind == "bun":  # 发髻 + 眼镜
        E(d, [166, 96, 346, 236], hair)
        E(d, [216, 60, 296, 140], hair)
        R(d, [166, 210, 346, 250], skin)  # 露出脸
        E(d, [196, 215, 244, 263], (0, 0, 0, 0), outline=(60, 50, 45, 255), width=6)
        E(d, [268, 215, 316, 263], (0, 0, 0, 0), outline=(60, 50, 45, 255), width=6)
        L(d, [(244, 239), (268, 239)], (60, 50, 45, 255), 6)
    elif kind == "beard":  # 短发 + 胡子
        E(d, [166, 100, 346, 210], hair)
        R(d, [166, 170, 346, 220], skin)
        E(d, [190, 240, 322, 330], hair)  # 胡子
        E(d, [226, 250, 286, 290], skin)  # 嘴部露出
    else:  # 灰发 + 围巾
        E(d, [160, 96, 352, 220], hair)
        R(d, [166, 180, 346, 230], skin)
        R(d, [186, 330, 326, 380], (150, 110, 170, 255), radius=16)
    # 眼睛 + 微笑
    E(d, [214, 205, 234, 225], (50, 40, 35, 255))
    E(d, [278, 205, 298, 225], (50, 40, 35, 255))
    d.arc([226 * SS, 230 * SS, 286 * SS, 290 * SS], 20, 160, fill=(120, 60, 50, 255), width=5 * SS)
    finish(im, path, S, S)


# ---------------- 杯子 512x512 ----------------

def cup_body(d, fill):
    R(d, [136, 170, 356, 400], fill, radius=26, outline=(110, 90, 75, 255), width=6)  # 杯身
    E(d, [340, 220, 436, 330], (0, 0, 0, 0), outline=(110, 90, 75, 255), width=14)  # 把手
    E(d, [116, 380, 396, 452], (228, 222, 214, 255), outline=(150, 135, 120, 255), width=6)  # 碟子


def draw_cup_empty():
    S = 512
    im = canvas(S, S)
    d = ImageDraw.Draw(im)
    cup_body(d, (245, 242, 236, 255))
    finish(im, os.path.join(OUT, "cup_empty.png"), S, S)


def draw_cup_latte():
    S = 512
    im = canvas(S, S)
    d = ImageDraw.Draw(im)
    cup_body(d, (245, 242, 236, 255))
    E(d, [146, 176, 346, 240], (120, 72, 40, 255))  # 咖啡液面
    # 心形拉花
    E(d, [216, 190, 252, 226], (245, 238, 225, 255))
    E(d, [244, 190, 280, 226], (245, 238, 225, 255))
    P(d, [(218, 208), (278, 208), (248, 240)], (245, 238, 225, 255))
    finish(im, os.path.join(OUT, "cup_latte.png"), S, S)


# ---------------- 设备 512x512 ----------------

def draw_espresso():
    S = 512
    im = canvas(S, S)
    d = ImageDraw.Draw(im)
    steel = (168, 172, 178, 255)
    dark = (70, 60, 55, 255)
    P(d, [(176, 400), (336, 400), (316, 300), (196, 300)], steel)          # 下壶
    P(d, [(196, 300), (316, 300), (296, 160), (216, 160)], (198, 202, 208, 255))  # 上壶
    R(d, [206, 140, 306, 170], dark, radius=8)                             # 盖
    E(d, [236, 104, 276, 144], dark)                                       # 盖钮
    L(d, [(336, 380), (408, 300)], dark, 22)                               # 把手
    L(d, [(216, 176), (168, 150)], steel, 18)                              # 壶嘴
    finish(im, os.path.join(OUT, "machine_espresso.png"), S, S)


def draw_grinder():
    S = 512
    im = canvas(S, S)
    d = ImageDraw.Draw(im)
    wood = (150, 100, 62, 255)
    wood_dark = (110, 72, 44, 255)
    R(d, [156, 230, 356, 430], wood, radius=10, outline=wood_dark, width=8)  # 机身
    R(d, [176, 300, 336, 360], wood_dark, radius=6)                          # 抽屉
    E(d, [236, 316, 276, 344], (70, 50, 35, 255))                            # 抽屉钮
    P(d, [(186, 230), (326, 230), (296, 150), (216, 150)], (90, 85, 80, 255))  # 料斗
    E(d, [206, 120, 306, 160], (90, 85, 80, 255))                            # 斗盖
    L(d, [(256, 130), (256, 70)], (60, 55, 50, 255), 12)                     # 摇杆
    L(d, [(256, 70), (356, 70)], (60, 55, 50, 255), 12)
    E(d, [346, 50, 396, 100], (150, 100, 62, 255))                           # 摇柄球
    finish(im, os.path.join(OUT, "machine_grinder.png"), S, S)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    draw_background()
    draw_customer(os.path.join(OUT, "customer_1.png"), (242, 201, 164, 255), (80, 55, 40, 255), (90, 140, 150, 255), "bun")
    draw_customer(os.path.join(OUT, "customer_2.png"), (230, 180, 140, 255), (60, 45, 35, 255), (170, 70, 60, 255), "beard")
    draw_customer(os.path.join(OUT, "customer_3.png"), (240, 205, 175, 255), (200, 200, 205, 255), (130, 110, 160, 255), "scarf")
    draw_cup_empty()
    draw_cup_latte()
    draw_espresso()
    draw_grinder()
    print("all done")
