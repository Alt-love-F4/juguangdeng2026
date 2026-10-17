from pathlib import Path
import argparse
import json
import cv2
import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--customer-source", type=Path, required=True)
parser.add_argument("--clean-background", type=Path, required=True)
arguments = parser.parse_args()
ART = ROOT / "Assets/Art/FreeCafe/Seating"
ART.mkdir(parents=True, exist_ok=True)
source = Image.open(ROOT / "Assets/Art/Background/game/SCE_一楼大厅场景底图.png").convert("RGBA")
pixels = np.array(source)
width, height = source.size


def polygon_mask(points):
    mask = Image.new("L", source.size)
    ImageDraw.Draw(mask).polygon([(round(horizontal * 2), round(vertical * 2)) for horizontal, vertical in points], fill=255)
    return np.array(mask)


chair_regions = [
    [(245, 690), (310, 686), (331, 822), (386, 831), (402, 920), (363, 934), (257, 932), (266, 872)],
    [(535, 669), (617, 669), (620, 768), (649, 779), (644, 838), (606, 875), (542, 876), (551, 801)],
    [(675, 670), (750, 670), (741, 762), (731, 788), (738, 897), (671, 906), (598, 886), (608, 813), (664, 794)],
    [(750, 640), (820, 638), (815, 738), (800, 750), (809, 833), (731, 832), (727, 780)],
    [(894, 642), (962, 642), (972, 751), (1010, 766), (1016, 830), (957, 843), (898, 831), (906, 782)],
]
dark = (pixels[:, :, 0] < 43) & (pixels[:, :, 1] < 17) & (pixels[:, :, 2] < 6)
seat_color = (np.abs(pixels[:, :, :3].astype(int) - np.array([89, 39, 11])).max(axis=2) < 4)
union = np.zeros((height, width), dtype=np.uint8)
placements = []
for index, region in enumerate(chair_regions):
    envelope = polygon_mask(region)
    alpha = ((dark | seat_color) & (envelope > 0)).astype(np.uint8) * 255
    count, labels, stats, _ = cv2.connectedComponentsWithStats(alpha, 8)
    alpha = np.where(np.isin(labels, [label for label in range(1, count) if stats[label, cv2.CC_STAT_AREA] > 80]), 255, 0).astype(np.uint8)
    alpha = cv2.morphologyEx(alpha, cv2.MORPH_CLOSE, np.ones((3, 3), dtype=np.uint8))
    rgba = pixels.copy()
    rgba[:, :, 3] = alpha
    result = Image.fromarray(rgba)
    bounds = result.getbbox()
    result.crop(bounds).save(ART / ("chair_left.png" if index == 0 else f"chair_idle_{index}.png"))
    placements.append({"sprite": "chair_left" if index == 0 else f"chair_idle_{index}", "rect": bounds, "table": [0, 1, 1, 1, 2][index]})
    union = np.maximum(union, envelope)

generated_path = ROOT / "Assets/Art/Background/game/SCE_一楼大厅场景底图_无椅.png"
provider_path = ROOT / "output/cafe_no_chairs_provider.png"
generated = Image.open(arguments.clean_background).convert("RGBA").resize(source.size, Image.Resampling.LANCZOS)
generated.save(provider_path)
clean = generated
clean.save(generated_path)

table_specs = [(955, 1535, 465, 365), (1360, 1405, 425, 300), (2120, 1405, 410, 305), (2560, 1535, 475, 365)]
for index, (center_x, top_y, table_width, table_height) in enumerate(table_specs):
    table_mask = Image.new("L", source.size)
    draw = ImageDraw.Draw(table_mask)
    draw.ellipse((center_x-table_width/2, top_y-34, center_x+table_width/2, top_y+38), fill=255)
    leg_polygon = [(center_x-75, top_y+24), (center_x+75, top_y+24), (center_x+42, top_y+139), (center_x+47, top_y+220), (center_x+100, top_y+table_height-22), (center_x+80, top_y+table_height), (center_x-78, top_y+table_height), (center_x-100, top_y+table_height-23), (center_x-47, top_y+220), (center_x-42, top_y+139)]
    draw.polygon(leg_polygon, fill=255)
    table_pixels = np.array(clean)
    mask_pixels = np.array(table_mask)
    table_dark = (table_pixels[:, :, 0] < 85) & (table_pixels[:, :, 1] < 30) & (table_pixels[:, :, 2] < 22)
    tabletop = Image.new("L", source.size)
    ImageDraw.Draw(tabletop).ellipse((center_x-table_width/2, top_y-34, center_x+table_width/2, top_y+38), fill=255)
    table_pixels[:, :, 3] = np.where(((mask_pixels > 0) & table_dark) | (np.array(tabletop) > 0), 255, 0).astype(np.uint8)
    table = Image.fromarray(table_pixels)
    bounds = table.getbbox()
    table.crop(bounds).save(ART / f"table_front_{index}.png")
    placements.append({"sprite": f"table_front_{index}", "rect": bounds, "table": index})

customer_source = Image.open(arguments.customer_source).convert("RGBA")
customer_pixels = np.array(customer_source)
white = np.min(customer_pixels[:, :, :3], axis=2) > 238
customer_pixels[white, 3] = 0
customer = Image.fromarray(customer_pixels)
customer_bounds = customer.getbbox()
customer = customer.crop(customer_bounds)
customer.save(ART / "customer_seated.png")
(ART / "source-layout.json").write_text(json.dumps({"sourceSize": source.size, "placements": placements, "customerSourceRect": customer_bounds, "customerSize": customer.size}, indent=2), encoding="utf-8")
print(json.dumps({"assets": str(ART), "customerBounds": customer_bounds, "customerSize": customer.size, "placements": placements}, indent=2))
