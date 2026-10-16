"""Split the sprite actually referenced by CoffeeShop's grinder prefab; preserve its silhouette."""
from pathlib import Path
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Art/Gameplay/\u5496\u5561\u9986/Sliced/Entities/machine.grinder/default.png"
OUT = SOURCE.parent / "Animated"

def split():
    OUT.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGBA")
    if source.size != (485, 730):
        raise ValueError("Grinder source changed; re-author the split masks before running")
    arm_polygon = [(145,149),(238,150),(287,122),(405,120),(459,121),(461,142),(299,142),(249,172),(145,172)]
    knob_polygon = [(368,0),(485,0),(485,120),(465,129),(398,131),(393,111),(367,94)]
    def mask(points):
        result = Image.new("L", source.size); ImageDraw.Draw(result).polygon(points, fill=255); return result
    def piece(points, box, name):
        result = source.copy()
        from PIL import ImageChops
        result.putalpha(ImageChops.multiply(source.getchannel("A"), mask(points)))
        result.crop(box).save(OUT / name)
    piece(arm_polygon, (145,120,463,176), "crank-arm.png")
    piece(knob_polygon, (367,0,485,132), "crank-knob.png")
    drawer = [(69,561),(302,557),(316,568),(315,677),(304,687),(72,685),(65,673),(66,575)]
    piece(drawer,(65,557,317,688),"drawer-closed.png")
    body = source.copy()
    erase = ImageDraw.Draw(body)
    erase.polygon(arm_polygon, fill=(0,0,0,0)); erase.polygon(knob_polygon, fill=(0,0,0,0))
    # Restore the stationary axle, which passes in front of the horizontal crank.
    body.alpha_composite(source.crop((175,141,202,188)),(175,141))
    erase = ImageDraw.Draw(body)
    erase.polygon(drawer, fill=(31,24,22,255))
    erase.polygon([(77,574),(300,571),(302,671),(78,673)],fill=(43,30,24,255))
    erase.line([(70,568),(302,564),(309,574)],fill=(14,13,13,255),width=4)
    body.save(OUT / "body.png")
    metadata = {"source":str(SOURCE.relative_to(ROOT)), "sourceSize":[485,730], "crankPivotPixels":[188,162], "knobFootPixels":[427,130], "closedDrawerCenterPixels":[191,622.5]}
    (OUT / "split-layout.json").write_text(json.dumps(metadata,indent=2),encoding="utf-8")
    print(OUT)

def process_pickups(sheet_path):
    import numpy as np
    sheet = Image.open(sheet_path).convert("RGB")
    if sheet.size != (1536, 1024):
        raise ValueError("Pickup sheet dimensions changed; inspect its layout before cropping")
    entities = SOURCE.parent.parent
    for box, path in [((0,200,800,850),OUT / "drawer-filled.png"),
                      ((850,250,1536,820),entities / "beans/handful.png")]:
        rgb = np.array(sheet.crop(box), dtype=np.float32)
        alpha = np.clip((250-rgb.min(axis=2))/55, 0, 1)
        # Remove the generated white matte and its antialias fringe; keep the warm painted fills.
        foreground = np.clip((rgb-255*(1-alpha[...,None]))/np.maximum(alpha[...,None],.001),0,255)
        result = Image.fromarray(np.dstack((foreground,alpha*255)).astype("uint8"), "RGBA")
        result = result.crop(result.getchannel("A").getbbox())
        padded = Image.new("RGBA", (result.width+16,result.height+16))
        padded.alpha_composite(result,(8,8)); path.parent.mkdir(parents=True,exist_ok=True); padded.save(path)

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pickup-sheet", type=Path)
    args = parser.parse_args()
    split()
    if args.pickup_sheet: process_pickups(args.pickup_sheet)
