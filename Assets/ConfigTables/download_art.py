#!/usr/bin/env python3
"""Download generated art into Assets/Art/Gameplay."""
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "Art" / "Gameplay"
OUT.mkdir(parents=True, exist_ok=True)

API = "https://trae-api-cn.mchost.guru/api/ide/v1/text_to_image?prompt={}&image_size={}"

ART = [
    ("bg_coffee_shop", "landscape_16_9", "top-down view cozy 2D pixel art coffee shop interior, wooden counter, espresso machine, tables, chairs, warm lighting, game background, no text"),
    ("icon_grinder", "square", "2D pixel art coffee grinder icon, brown wooden base, metal top, game UI icon, simple background"),
    ("icon_moka_pot", "square", "2D pixel art moka pot icon, silver aluminum coffee maker, game UI icon, simple background"),
    ("icon_milk_cup", "square", "2D pixel art milk pitcher icon, stainless steel jug, game UI icon, simple background"),
    ("icon_cocoa_pot", "square", "2D pixel art cocoa pot icon, small copper pot with handle, game UI icon, simple background"),
    ("icon_matcha_bowl", "square", "2D pixel art matcha bowl icon, green ceramic bowl with whisk, game UI icon, simple background"),
    ("cup_small", "square", "2D pixel art small espresso cup icon, white ceramic cup, game UI icon, simple background"),
    ("cup_large", "square", "2D pixel art large latte cup icon, tall white ceramic cup, game UI icon, simple background"),
    ("ing_coffee_bean", "square", "2D pixel art coffee beans icon, brown roasted beans pile, game UI icon, simple background"),
    ("ing_milk", "square", "2D pixel art milk carton icon, white and blue carton, game UI icon, simple background"),
    ("ing_matcha", "square", "2D pixel art matcha powder icon, green powder in small tin, game UI icon, simple background"),
    ("ing_chocolate", "square", "2D pixel art cocoa powder icon, brown powder in tin, game UI icon, simple background"),
    ("ing_honey", "square", "2D pixel art honey jar icon, golden honey in glass jar, game UI icon, simple background"),
    ("ing_cream", "square", "2D pixel art whipped cream icon, white cream dollop, game UI icon, simple background"),
    ("customer_1", "square", "2D pixel art customer avatar, smiling person with brown hair, game icon, simple background"),
    ("customer_2", "square", "2D pixel art customer avatar, person with black hair and glasses, game icon, simple background"),
    ("customer_3", "square", "2D pixel art customer avatar, person with blonde hair, game icon, simple background"),
]

for name, size, prompt in ART:
    url = API.format(urllib.parse.quote(prompt, safe=""), size)
    out = OUT / f"{name}.png"
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=120) as resp, open(out, "wb") as f:
            f.write(resp.read())
        print(f"[OK] {name}.png")
    except Exception as e:
        print(f"[FAIL] {name}.png: {e}")
