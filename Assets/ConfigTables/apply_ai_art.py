#!/usr/bin/env python3
"""AI 美术后处理：把 Assets/Art/Gameplay/AI/ 里的生成图按规则处理后覆盖到 Assets/Art/Gameplay/。

用法：python Assets/ConfigTables/apply_ai_art.py [文件名，可选]

规则（与 prompts.json 的 postprocess 字段一致）：
  bg_coffee_shop            none          ：原样复制到最终路径
  customer_1/2/3            trim-or-circle：有透明通道 → 裁掉透明边；无透明 → 圆形遮罩（头像徽章）
  cup_*/machine_*           trim-or-key   ：有透明通道 → 裁掉透明边；无透明 → 边缘中值色键抠图 + 裁剪

安全保护：抠图后内容面积不足原图 5% 时判定抠图失败，跳过该文件并提示。
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

try:
    from scipy import ndimage
    _HAS_SCIPY = True
except ImportError:
    _HAS_SCIPY = False

SCRIPT_DIR = Path(__file__).resolve().parent          # Assets/ConfigTables
ART_DIR = SCRIPT_DIR.parent / "Art" / "Gameplay"      # Assets/Art/Gameplay
SRC_DIR = ART_DIR / "AI"                              # 生成图暂存
DST_DIR = ART_DIR                                     # 最终路径（覆盖同名占位图）

JOBS = [
    ("bg_coffee_shop", "none"),
    ("customer_1", "trim-or-circle"),
    ("customer_2", "trim-or-circle"),
    ("customer_3", "trim-or-circle"),
    ("cup_empty", "trim-or-key"),
    ("cup_small", "trim-or-key"),
    ("cup_latte", "trim-or-key"),
    ("machine_espresso", "trim-or-key"),
    ("machine_grinder", "trim-or-key"),
    ("machine_milk_pot", "trim-or-key"),
    ("machine_cocoa_pot", "trim-or-key"),
    ("machine_matcha_grinder", "trim-or-key"),
    ("ing_coffee_beans", "trim-or-key"),
    ("ing_milk", "trim-or-key"),
    ("ing_matcha", "trim-or-key"),
    ("ing_chocolate", "trim-or-key"),
    ("ing_honey", "trim-or-key"),
    ("ing_cream", "trim-or-key"),
    ("serve_window", "trim-or-key"),
]


def has_alpha(im: Image.Image) -> bool:
    """判断图片是否真正带透明像素（含调色板 PNG 的 tRNS 透明）。"""
    return im.convert("RGBA").getchannel("A").getextrema()[0] < 250


def trim_alpha(im: Image.Image, pad: int = 8) -> Image.Image:
    """按 alpha 通道裁掉透明边，保留少量留白。"""
    im = im.convert("RGBA")
    bbox = im.getchannel("A").getbbox()
    if not bbox:
        return im
    w, h = im.size
    box = (max(0, bbox[0] - pad), max(0, bbox[1] - pad),
           min(w, bbox[2] + pad), min(h, bbox[3] + pad))
    return im.crop(box)


def apply_circle(im: Image.Image) -> Image.Image:
    """裁成圆形头像徽章：取中心偏上正方形，圆形遮罩（4x 超采样保证边缘平滑）。"""
    im = im.convert("RGBA")
    w, h = im.size
    s = min(w, h)
    left = (w - s) // 2
    top = max(0, (h - s) // 3)  # 略偏上，对准头部
    im = im.crop((left, top, left + s, top + s))
    ss = 4
    mask = Image.new("L", (s * ss, s * ss), 0)
    ImageDraw.Draw(mask).ellipse((2, 2, s * ss - 3, s * ss - 3), fill=255)
    im.putalpha(mask.resize((s, s), Image.LANCZOS))
    return im


def border_median_color(im: Image.Image):
    """边缘像素颜色中位数，比四角取样更能抗渐变背景。"""
    px = im.load()
    w, h = im.size
    step = max(1, w // 60)
    samples = []
    for x in range(0, w, step):
        samples.append(px[x, 0])
        samples.append(px[x, h - 1])
    for y in range(0, h, step):
        samples.append(px[0, y])
        samples.append(px[w - 1, y])
    r = sorted(s[0] for s in samples)[len(samples) // 2]
    g = sorted(s[1] for s in samples)[len(samples) // 2]
    b = sorted(s[2] for s in samples)[len(samples) // 2]
    return r, g, b


def keep_largest_component(im: Image.Image) -> Image.Image:
    """只保留 alpha 最大连通分量（主体），清掉 JPEG 噪声/水印等散点，防止 bbox 被撑爆。
    无 scipy 时原样返回（退化为旧行为）。"""
    if not _HAS_SCIPY:
        return im
    arr = np.array(im)
    alpha = arr[..., 3]
    lbl, n = ndimage.label(alpha > 0)
    if n <= 1:
        return im
    sizes = ndimage.sum(np.ones_like(lbl), lbl, range(1, n + 1))
    keep = 1 + int(np.argmax(sizes))
    arr[..., 3] = np.where(lbl == keep, alpha, 0).astype(np.uint8)
    return Image.fromarray(arr)


def _key_single(im: Image.Image, br: int, bg: int, bb: int, tolerance: int = 95) -> Image.Image:
    """用指定 RGB 色作为背景色抠图一次，不裁剪。"""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    tol2 = tolerance * tolerance
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if (r - br) ** 2 + (g - bg) ** 2 + (b - bb) ** 2 < tol2:
                px[x, y] = (r, g, b, 0)
    return im


def key_out_background(im: Image.Image, tolerance: int = 95):
    """以边缘中位色为背景色抠图并裁剪；若边缘中位色与主体差异小，退化为白色/品红双方案。
    返回 (结果图, 是否成功)。"""
    w, h = im.size
    total = w * h
    br, bg, bb = border_median_color(im)

    # 方案1：边缘中位色
    out = _key_single(im, br, bg, bb, tolerance)
    out = keep_largest_component(out)
    bbox = out.getchannel("A").getbbox()
    if bbox:
        content = (bbox[2] - bbox[0]) * (bbox[3] - bbox[1])
        if content >= total * 0.05:
            pad = 8
            box = (max(0, bbox[0] - pad), max(0, bbox[1] - pad),
                   min(w, bbox[2] + pad), min(h, bbox[3] + pad))
            return out.crop(box), True

    # 方案2：纯白色背景 (255,255,255) — 针对 Seedream 白色背景图
    out2 = _key_single(im, 255, 255, 255, tolerance)
    out2 = keep_largest_component(out2)
    bbox2 = out2.getchannel("A").getbbox()
    if bbox2:
        content2 = (bbox2[2] - bbox2[0]) * (bbox2[3] - bbox2[1])
        if content2 >= total * 0.05:
            pad = 8
            box = (max(0, bbox2[0] - pad), max(0, bbox2[1] - pad),
                   min(w, bbox2[2] + pad), min(h, bbox2[3] + pad))
            return out2.crop(box), True

    # 方案3：纯品红 #FF00FF
    out3 = _key_single(im, 255, 0, 255, tolerance)
    out3 = keep_largest_component(out3)
    bbox3 = out3.getchannel("A").getbbox()
    if bbox3:
        content3 = (bbox3[2] - bbox3[0]) * (bbox3[3] - bbox3[1])
        if content3 >= total * 0.05:
            pad = 8
            box = (max(0, bbox3[0] - pad), max(0, bbox3[1] - pad),
                   min(w, bbox3[2] + pad), min(h, bbox3[3] + pad))
            return out3.crop(box), True

    return im.convert("RGBA"), False


def process(name: str, rule: str) -> bool:
    # 优先读取 .jpg（Seedream 生成），其次 .png
    src = SRC_DIR / f"{name}.jpg"
    if not src.exists():
        src = SRC_DIR / f"{name}.png"
    dst = DST_DIR / f"{name}.png"
    if not src.exists():
        print(f"[skip] {name}: 未找到生成图 {SRC_DIR / name}.[jpg|png]")
        return False

    im = Image.open(src)
    if rule == "none":
        im.convert("RGBA").save(dst, format="PNG")
        print(f"[ok]   {name}: 原样复制 -> {dst.name}")
        return True

    if has_alpha(im):
        out = trim_alpha(im)
        note = "透明图裁边"
    elif rule == "trim-or-circle":
        keyed, ok = key_out_background(im)
        out = apply_circle(keyed if ok else im)
        note = "色键抠图+圆形遮罩" if ok else "无透明通道，已做圆形遮罩"
    else:
        out, ok = key_out_background(im)
        if not ok:
            print(f"[FAIL] {name}: 抠图失败（背景色不均匀或主体被误判），已跳过，请改用纯色背景重新生成")
            return False
        note = "无透明通道，已色键抠图"

    out.save(dst, format="PNG")
    print(f"[ok]   {name}: {note} {out.size} -> {dst.name}")
    return True


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None
    if not SRC_DIR.exists():
        print(f"未找到暂存目录 {SRC_DIR}，请先让 Trae Work 生成图片到该目录。")
        return 1

    done = 0
    for name, rule in JOBS:
        if only and name != only:
            continue
        try:
            if process(name, rule):
                done += 1
        except Exception as e:  # 单张失败不影响其余
            print(f"[FAIL] {name}: {e!r}")

    total = 1 if only else len(JOBS)
    print(f"完成 {done}/{total}。回到 Unity 等待自动导入即可。")
    return 0 if done else 1


if __name__ == "__main__":
    sys.exit(main())