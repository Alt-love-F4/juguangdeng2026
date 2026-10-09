#!/usr/bin/env python3
"""实体素材后处理：把「策划文件」里的多状态素材，按 entity_art_map.json 抠成透明 PNG，
输出到 Assets/Art/FreeCafe/Entities/<artId>/<state>.png，供 ArtLibrary 的 variants 接线。

用法：python Assets/ConfigTables/apply_entity_art.py [--dry]

抠图规则（针对白底/米白底 JPG）：
  1. 取四边像素中位数作为背景色；
  2. 计算每像素与背景色的最大通道差，小于 tolerance 视为「疑似背景」；
  3. 只把「与画面边缘连通的疑似背景」判为真背景（flood-fill 思路），
     物体内部的白色/米白区域不会被误删；
  4. 对 alpha 做轻微高斯羽化，消除锯齿；
  5. 按 alpha 包围盒裁边，四周留 pad 像素。

安全保护：抠图后保留内容面积不足 min_keep_ratio 时判定失败，跳过该条并提示，不写坏图。
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

try:
    from scipy import ndimage
    _HAS_SCIPY = True
except ImportError:
    _HAS_SCIPY = False

SCRIPT_DIR = Path(__file__).resolve().parent          # Assets/ConfigTables
PROJECT_ROOT = SCRIPT_DIR.parent.parent               # 项目根目录


def load_config():
    cfg_path = SCRIPT_DIR / "entity_art_map.json"
    with open(cfg_path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def border_median_color(arr: np.ndarray):
    """四边像素的中位色，比单点取样更抗噪。"""
    top = arr[0, :, :]
    bottom = arr[-1, :, :]
    left = arr[:, 0, :]
    right = arr[:, -1, :]
    samples = np.concatenate([top, bottom, left, right], axis=0)
    return np.median(samples, axis=0)


def outer_background_mask(arr: np.ndarray, tolerance: float) -> np.ndarray:
    """返回「与画面边缘连通的背景区域」布尔掩码。"""
    bg = border_median_color(arr)
    diff = np.max(np.abs(arr.astype(np.int16) - bg.astype(np.int16)), axis=2)
    bgish = diff < tolerance
    if not _HAS_SCIPY:
        # 无 scipy 时退化为「全部疑似背景都抠掉」，仍可用但可能误删内部白块。
        return bgish
    labels, count = ndimage.label(bgish)
    if count == 0:
        return bgish
    border_labels = set(labels[0, :].tolist()) | set(labels[-1, :].tolist())
    border_labels |= set(labels[:, 0].tolist()) | set(labels[:, -1].tolist())
    border_labels.discard(0)
    if not border_labels:
        return bgish
    return np.isin(labels, list(border_labels))


def cutout(image: Image.Image, tolerance: float, feather: float):
    arr = np.array(image.convert("RGB"))
    outer = outer_background_mask(arr, tolerance)
    alpha = np.where(outer, 0.0, 255.0)
    if feather > 0:
        alpha = ndimage.gaussian_filter(alpha, sigma=feather) if _HAS_SCIPY else alpha
    rgba = np.dstack([arr, np.clip(alpha, 0, 255).astype(np.uint8)])
    return Image.fromarray(rgba, "RGBA")


def trim_alpha(image: Image.Image, pad: int):
    bbox = image.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if not bbox:
        return image, 0.0
    width, height = image.size
    box = (max(0, bbox[0] - pad), max(0, bbox[1] - pad),
           min(width, bbox[2] + pad), min(height, bbox[3] + pad))
    cropped = image.crop(box)
    kept = (np.array(cropped.getchannel("A")) > 8).mean()
    return cropped, float(kept)


def main():
    dry = "--dry" in sys.argv
    cfg = load_config()
    source_dir = PROJECT_ROOT / cfg["source_dir"]
    output_dir = PROJECT_ROOT / cfg["output_dir"]
    keying = cfg["keying"]
    tolerance = float(keying.get("tolerance", 26))
    feather = float(keying.get("feather", 0.8))
    pad = int(keying.get("pad", 6))
    min_keep = float(keying.get("min_keep_ratio", 0.04))

    ok, skipped, failed = 0, 0, []
    for entry in cfg["maps"]:
        src_path = source_dir / entry["src"]
        if not src_path.exists():
            failed.append(f"{entry['src']} → 源文件不存在")
            continue
        target_dir = output_dir / entry["artId"]
        target_path = target_dir / f"{entry['state']}.png"
        if dry:
            print(f"[dry] {entry['src']} → {target_path.relative_to(PROJECT_ROOT)}")
            continue
        try:
            with Image.open(src_path) as image:
                cut = cutout(image, tolerance, feather)
            trimmed, kept = trim_alpha(cut, pad)
            if kept < min_keep:
                skipped += 1
                print(f"[skip] {entry['src']} → {entry['artId']}/{entry['state']}：抠图后内容仅 {kept:.1%}，判定失败，未写入")
                continue
            target_dir.mkdir(parents=True, exist_ok=True)
            trimmed.save(target_path, "PNG")
            ok += 1
            print(f"[ok]   {entry['src']} → {target_path.relative_to(PROJECT_ROOT)}  {trimmed.size[0]}x{trimmed.size[1]}  内容占比 {kept:.1%}")
        except Exception as exc:  # 单条失败不影响其它条目
            failed.append(f"{entry['src']} → {entry['artId']}/{entry['state']}：{exc}")

    print(f"\n完成：成功 {ok} 条，跳过 {skipped} 条，失败 {len(failed)} 条")
    for item in failed:
        print(f"  ! {item}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())