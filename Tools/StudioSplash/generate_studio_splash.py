"""Slices the Organic Game Workshop badge into animated layers and synthesizes the splash sound.

Run from the project root:  python Tools/StudioSplash/generate_studio_splash.py
Source:  Tools/StudioSplash/source/organic_game_workshop_logo.jpg (640x640 team icon)
Outputs (re-runnable; overwrites):
  Assets/Resources/StudioSplash/studio_splash_atlas.png   all layers, white ink with alpha (tinted at runtime)
  Assets/Resources/StudioSplash/studio_splash_layout.json sprite rects in atlas pixels + logo-space rects/anchors
  Assets/Resources/StudioSplash/studio_splash_sfx.wav     one stereo cue synced to StudioLogoSplash's timeline

Every ink pixel belongs to exactly one layer, so the assembled badge equals the original icon.
The timeline constants below must match StudioLogoSplash.cs.
"""
import json
import math
import os
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage, signal

SRC = os.path.join("Tools", "StudioSplash", "source", "organic_game_workshop_logo.jpg")
OUT = os.path.join("Assets", "Resources", "StudioSplash")
SCALE = 3                # hi-res factor for crisp ink edges
PAD = 6                  # atlas padding (texels) so mipmaps do not bleed between sprites
STEM_X0, STEM_X1 = 294, 307   # core stem columns; bar pixels beside it stay with the halves
TOP_LEAF_Y, ROOT_Y = 142, 316

# --- timeline (seconds), mirrored in StudioLogoSplash.cs ---
T_SEED = 0.05            # seed appears and starts to fall (StudioLogoSplash.SeedStart)
T_ROOT = 0.35
T_STEM0, T_STEM1 = 0.50, 1.20
T_FLY0, T_IMPACT = 1.30, 1.85
T_BUTTONS = [2.00, 2.08, 2.16, 2.24, 2.32]
T_WORD0, T_LETTER_STEP = 2.42, 0.06
T_SUB0 = 2.90
T_SHINE = 3.35
DURATION = 5.2


# ----------------------------------------------------------------------------------- art
def load_ink():
    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
    lum = rgb.mean(axis=2)
    h, w = lum.shape
    yy, xx = np.mgrid[0:h, 0:w]
    inside = np.hypot(xx - (w - 1) / 2, yy - (h - 1) / 2) < 307
    return lum, inside


def classify(lum, inside):
    """Returns a label image (0 = none) and the ordered layer names."""
    core = (lum < 128) & inside
    lab, _ = ndimage.label(core)
    names, groups = [], np.zeros(core.shape, np.int16)

    def add(name, mask):
        names.append(name)
        groups[mask & (groups == 0)] = len(names)

    objs = ndimage.find_objects(lab)
    comps = [(i + 1, s, (lab[s] == i + 1).sum()) for i, s in enumerate(objs)]
    frame_id = max(comps, key=lambda c: c[2])[0]
    frame = lab == frame_id
    yy, xx = np.mgrid[0:core.shape[0], 0:core.shape[1]]

    # Frame = controller outline + vine. Cut the stem band out to separate halves and leaves.
    band = frame & (xx >= STEM_X0) & (xx < STEM_X1)
    rest = frame & ~band
    rl, rn = ndimage.label(rest)
    left_ctrl = right_ctrl = None
    leaves = []
    for i, s in enumerate(ndimage.find_objects(rl)):
        m = rl == i + 1
        area = m.sum()
        if area < 30:
            continue
        if area > 4000:
            if s[1].start < STEM_X0: left_ctrl = m
            else: right_ctrl = m
        else:
            leaves.append((s, m))
    top_leaf = right_ctrl & (((yy < 143) & (xx < 338)) | ((yy < 153) & (xx < 323)))
    right_ctrl = right_ctrl & ~top_leaf

    add("ctrl_left", left_ctrl)
    add("ctrl_right", right_ctrl)
    add("leaf_top", top_leaf | (band & (yy < TOP_LEAF_Y)))
    root = band & (yy >= ROOT_Y)
    for s, m in leaves:
        if s[0].start > 300: root |= m
    add("vine_root", root)
    add("vine_stem", band & (yy >= TOP_LEAF_Y) & (yy < ROOT_Y))
    for s, m in leaves:
        if s[0].start <= 300:
            add("leaf_left" if s[1].start < STEM_X0 else "leaf_right", m)

    others = []
    for cid, s, area in comps:
        if cid == frame_id or area < 30: continue
        others.append((s[0].start, s[1].start, cid, s))
    emblem = sorted([o for o in others if o[0] < 300], key=lambda o: (o[1], o[0]))
    # d-pad is the big left piece; buttons ordered top, left, right, bottom.
    dpad = emblem[0]
    add("dpad", lab == dpad[2])
    buttons = emblem[1:]
    by_name = {}
    ys = sorted(buttons, key=lambda o: o[0])
    by_name["btn_top"], by_name["btn_bottom"] = ys[0], ys[-1]
    mids = sorted(ys[1:-1], key=lambda o: o[1])
    by_name["btn_left"], by_name["btn_right"] = mids[0], mids[1]
    for n in ("btn_top", "btn_left", "btn_right", "btn_bottom"):
        add(n, lab == by_name[n][2])
    words = [o for o in others if o[0] >= 300]
    big = sorted([o for o in words if o[0] < 450], key=lambda o: o[1])
    small = sorted([o for o in words if o[0] >= 450], key=lambda o: o[1])
    for i, o in enumerate(big): add("organic_%d" % i, lab == o[2])
    for i, o in enumerate(small): add("workshop_%d" % i, lab == o[2])
    assert len(big) == 7 and len(small) == 12, (len(big), len(small))
    return groups, names


def anchors(names, groups):
    out = {}
    for i, n in enumerate(names):
        ys, xs = np.nonzero(groups == i + 1)
        x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        a = (cx, cy)
        if n == "ctrl_left": a = (x1, cy)
        elif n == "ctrl_right": a = (x0, cy)
        elif n == "leaf_top": a = ((STEM_X0 + STEM_X1) / 2, TOP_LEAF_Y)
        elif n == "leaf_left": a = (x1, y1 - 3)
        elif n == "leaf_right": a = (x0, y1 - 3)
        elif n == "vine_root": a = ((STEM_X0 + STEM_X1) / 2, ROOT_Y)
        elif n.startswith(("organic", "workshop")): a = (cx, y1)
        out[n] = [float(a[0]), float(a[1])]
    return out


def ink_layers(lum, inside, groups, names):
    h, w = lum.shape
    big = Image.fromarray(np.clip(lum, 0, 255).astype(np.uint8)).filter(ImageFilter.MedianFilter(3))
    big = np.asarray(big.resize((w * SCALE, h * SCALE), Image.LANCZOS)).astype(np.float32)
    a = np.clip((236.0 - big) / (236.0 - 30.0), 0, 1)
    a = a * a * (3 - 2 * a)                       # tighten JPEG-softened edges
    a = np.clip((a - 0.12) / 0.76, 0, 1)
    ins = np.asarray(Image.fromarray(inside.astype(np.uint8) * 255).resize((w * SCALE, h * SCALE), Image.NEAREST)) > 0
    a *= ins
    # Nearest-core ownership at hi-res, then a small overlap so bilinear sampling hides cut seams.
    _, (iy, ix) = ndimage.distance_transform_edt(groups == 0, return_indices=True)
    owner = groups[iy, ix]
    owner = np.asarray(Image.fromarray(owner.astype(np.uint8)).resize((w * SCALE, h * SCALE), Image.NEAREST))
    layers = {}
    for i, n in enumerate(names):
        m = ndimage.binary_dilation(owner == i + 1, iterations=2)
        la = a * m
        ys, xs = np.nonzero(la > 0.004)
        x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
        layers[n] = (la[y0:y1, x0:x1], [x0 / SCALE, y0 / SCALE, (x1 - x0) / SCALE, (y1 - y0) / SCALE])
    return layers, a, owner


def glow_layer(alpha_hi, mask_hi, sigma):
    a = alpha_hi * mask_hi
    small = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).resize(
        (a.shape[1] // SCALE, a.shape[0] // SCALE), Image.BOX)).astype(np.float32) / 255
    pad = int(sigma * 3)
    small = np.pad(small, pad)
    g = ndimage.gaussian_filter(small, sigma)
    g = np.clip(g / max(g.max(), 1e-6) * 1.15, 0, 1)
    ys, xs = np.nonzero(g > 0.01)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    return g[y0:y1, x0:x1], [x0 - pad, y0 - pad, x1 - x0, y1 - y0]


def procedural():
    out = {}
    n = 512
    yy, xx = (np.mgrid[0:n, 0:n] + 0.5 - n / 2) / (n / 2)
    r = np.hypot(xx, yy)
    out["disc"] = np.clip((1 - r) * n / 2, 0, 1)
    out["ring"] = np.clip(1 - np.abs(r - 0.93) * n / 2 / 6, 0, 1) * (r < 1)
    m = 128
    yy, xx = (np.mgrid[0:m, 0:m] + 0.5 - m / 2) / (m / 2)
    r = np.hypot(xx, yy)
    out["soft"] = np.clip(1 - r, 0, 1) ** 2.2
    star = np.clip(1 - (np.abs(xx) * 9 + np.abs(yy)) , 0, 1) + np.clip(1 - (np.abs(yy) * 9 + np.abs(xx)), 0, 1)
    out["spark"] = np.clip(star * 0.9 + np.clip(1 - r * 2.2, 0, 1) ** 2, 0, 1)
    # filled leaf: vesica rotated 45 degrees
    u, v = (xx + yy) / math.sqrt(2), (yy - xx) / math.sqrt(2)
    leaf = (np.hypot(u, v - 0.62) < 1.05) & (np.hypot(u, v + 0.62) < 1.05) & (np.abs(u) < 0.95)
    leaf_img = Image.fromarray((leaf * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    out["leaf_particle"] = np.asarray(leaf_img).astype(np.float32) / 255
    sx = (np.arange(96) + 0.5 - 48) / 48
    band = np.exp(-(sx / 0.42) ** 2)
    out["shine"] = np.tile(band, (8, 1))
    return out


def pack(items):
    """Shelf packer. items: name -> 2D float alpha. Returns atlas RGBA and pixel rects (bottom-left origin)."""
    order = sorted(items, key=lambda k: -items[k].shape[0])
    width = 2048
    x = y = shelf = 0
    pos = {}
    for k in order:
        h, w = items[k].shape
        if x + w + PAD * 2 > width:
            x, y, shelf = 0, y + shelf, 0
        pos[k] = (x + PAD, y + PAD)
        x += w + PAD * 2
        shelf = max(shelf, h + PAD * 2)
    height = 1 << int(math.ceil(math.log2(y + shelf)))
    atlas = np.zeros((height, width), np.float32)
    rects = {}
    for k, (px, py) in pos.items():
        h, w = items[k].shape
        atlas[py:py + h, px:px + w] = items[k]
        rects[k] = [px, height - (py + h), w, h]
    rgba = np.zeros((height, width, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (np.clip(atlas, 0, 1) * 255 + 0.5).astype(np.uint8)
    return rgba, rects


def build_art():
    lum, inside = load_ink()
    groups, names = classify(lum, inside)
    layers, alpha_hi, owner = ink_layers(lum, inside, groups, names)
    anchor = anchors(names, groups)
    items, logo = {}, {}
    for n, (img, rect) in layers.items():
        items[n] = img
        logo[n] = rect
    def owned(prefixes):
        ids = [i + 1 for i, n in enumerate(names) if n.startswith(prefixes)]
        return np.isin(owner, ids)
    for gname, prefixes, sigma in [("glow_ctrl_left", ("ctrl_left",), 5), ("glow_ctrl_right", ("ctrl_right",), 5),
                                   ("glow_vine", ("leaf", "vine"), 4), ("glow_emblem", ("ctrl", "leaf", "vine", "dpad", "btn"), 9)]:
        img, rect = glow_layer(alpha_hi, owned(prefixes), sigma)
        items[gname] = img
        logo[gname] = [float(v) for v in rect]
    for n, img in procedural().items():
        items[n] = img
    rgba, rects = pack(items)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, "studio_splash_atlas.png"), optimize=True)
    sprites = []
    for n in items:
        entry = {"name": n, "atlas": rects[n]}
        if n in logo:
            entry["logo"] = [round(float(v), 3) for v in logo[n]]
            ax, ay = anchor.get(n, (logo[n][0] + logo[n][2] / 2, logo[n][1] + logo[n][3] / 2))
            entry["anchor"] = [round(float(ax), 3), round(float(ay), 3)]
        sprites.append(entry)
    with open(os.path.join(OUT, "studio_splash_layout.json"), "w", encoding="utf-8") as f:
        json.dump({"logoSize": 640, "sprites": sprites}, f, indent=1)
    print("atlas", rgba.shape[1], "x", rgba.shape[0], "sprites", len(sprites))
    # Debug composite proves the partition reproduces the source.
    comp = np.zeros(alpha_hi.shape, np.float32)
    for n, (img, rect) in layers.items():
        x0, y0 = int(round(rect[0] * SCALE)), int(round(rect[1] * SCALE))
        sub = comp[y0:y0 + img.shape[0], x0:x0 + img.shape[1]]
        sub[:] = sub + img * (1 - sub)
    print("composite max error vs hi-res ink", float(np.abs(comp - alpha_hi).max()))


# --------------------------------------------------------------------------------- audio
SR = 44100


def env_exp(n, decay):
    return np.exp(-np.arange(n) / SR / decay)


def place(buf, start, mono, pan=0.0, gain=1.0):
    i = int(start * SR)
    if i >= buf.shape[0]: return
    mono = mono[: buf.shape[0] - i] * gain
    l = math.cos((pan + 1) * math.pi / 4)
    r = math.sin((pan + 1) * math.pi / 4)
    buf[i:i + len(mono), 0] += mono * l
    buf[i:i + len(mono), 1] += mono * r


def sine_sweep(f0, f1, dur, curve=2.0):
    t = np.arange(int(dur * SR)) / SR
    f = f1 + (f0 - f1) * (1 - t / dur) ** curve
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def bandnoise(dur, lo, hi, rng):
    n = rng.standard_normal(int(dur * SR))
    b, a = signal.butter(2, [lo / (SR / 2), min(hi / (SR / 2), 0.99)], btype="band")
    return signal.lfilter(b, a, n)


def sweep_noise(dur, f0, f1, rng, q=1.4):
    """Band noise whose centre glides f0 -> f1 (processed in short blocks)."""
    n = int(dur * SR)
    noise = rng.standard_normal(n + 2048)
    out = np.zeros(n)
    block = 512
    zi = None
    for s in range(0, n, block):
        frac = s / n
        fc = f0 * (f1 / f0) ** frac
        lo, hi = fc / q, min(fc * q, SR / 2 * 0.95)
        b, a = signal.butter(2, [lo / (SR / 2), hi / (SR / 2)], btype="band")
        if zi is None or len(zi) != max(len(a), len(b)) - 1:
            zi = signal.lfilter_zi(b, a) * 0
        seg, zi = signal.lfilter(b, a, noise[s:s + block], zi=zi)
        out[s:s + len(seg)] = seg[: n - s]
    return out / (np.abs(out).max() + 1e-9)


def blip(freq, dur, rng, square=0.35):
    t = np.arange(int(dur * SR)) / SR
    ph = 2 * np.pi * freq * t
    tone = np.sin(ph) * (1 - square) + np.sign(np.sin(ph)) * square * 0.5 + 0.25 * np.sin(2 * ph)
    e = np.minimum(t / 0.004, 1) * np.exp(-t / (dur * 0.35))
    return tone * e


def pluck(freq, dur):
    t = np.arange(int(dur * SR)) / SR
    tone = sum(np.sin(2 * np.pi * freq * k * t) * (0.6 ** (k - 1)) * np.exp(-t * (3 + 4 * k)) for k in range(1, 5))
    return tone * np.minimum(t / 0.002, 1)


def pop(freq, rng):
    t = np.arange(int(0.18 * SR)) / SR
    f = freq * (1 + 1.6 * np.exp(-t / 0.012))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.05)
    click = bandnoise(0.18, 2500, 9000, rng) * np.exp(-t / 0.004) * 0.25
    return body + click


def reverb(x, seconds, rng, mix):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal((n, 2)) * np.exp(-t / (seconds / 6.9))[:, None]
    b, a = signal.butter(1, 6000 / (SR / 2))
    ir = signal.lfilter(b, a, ir, axis=0)
    ir[: int(0.012 * SR)] = 0
    ir /= np.sqrt((ir ** 2).sum(axis=0))
    wet = np.stack([signal.fftconvolve(x[:, c], ir[:, c])[: len(x)] for c in range(2)], axis=1)
    return x + wet * mix


def build_audio():
    rng = np.random.default_rng(20261021)
    buf = np.zeros((int(DURATION * SR), 2))
    dry = np.zeros_like(buf)

    # Rising air + sub drone into the impact.
    rise = sweep_noise(T_IMPACT - 0.05, 260, 5200, rng)
    t = np.arange(len(rise)) / SR
    place(buf, 0.05, rise * (t / t[-1]) ** 2.6, 0, 0.32)
    drone_t = np.arange(int(T_IMPACT * SR)) / SR
    drone = (np.sin(2 * np.pi * 55 * drone_t) + 0.5 * np.sin(2 * np.pi * 82.4 * drone_t)) * (drone_t / T_IMPACT) ** 2
    place(dry, 0.0, drone, 0, 0.22)

    # Seed: a soft glassy plink.
    place(buf, T_SEED, pluck(1318.5, 0.9), 0, 0.30)
    place(buf, T_SEED + 0.05, pluck(1975.5, 0.7), 0.1, 0.12)

    # Growth: woody pops climbing a pentatonic line as each leaf unfurls.
    place(buf, T_ROOT, pop(330, rng), 0.05, 0.42)
    # Same rule as StudioLogoSplash: a leaf unfurls when the stem fill reaches its anchor.
    with open(os.path.join(OUT, "studio_splash_layout.json"), encoding="utf-8") as f:
        layout = {e["name"]: e for e in json.load(f)["sprites"]}
    sx, sy, sw, sh = layout["vine_stem"]["logo"]
    leaf_times = [T_STEM0 + (T_STEM1 - T_STEM0) * min(max((sy + sh - layout[n]["anchor"][1]) / sh, 0), 1)
                  for n in ("leaf_right", "leaf_left", "leaf_top")]
    for k, (lt, f) in enumerate(zip(leaf_times, (392, 494, 587))):
        place(buf, lt, pop(f, rng), (-0.35, 0.35, 0.0)[k], 0.4)
        place(buf, lt + 0.01, pluck(f * 4, 0.5), (-0.35, 0.35, 0.0)[k], 0.08)
    grow = sweep_noise(T_STEM1 - T_STEM0, 900, 3200, rng, q=1.15)
    gt = np.arange(len(grow)) / SR
    place(buf, T_STEM0, grow * np.sin(np.pi * gt / gt[-1]) ** 2, 0, 0.10)

    # Twin whooshes: controller halves converge from both sides.
    wd = T_IMPACT - T_FLY0
    for pan in (-1, 1):
        w = sweep_noise(wd, 380, 2600, rng, q=1.6)
        wt = np.arange(len(w)) / SR
        e = (wt / wd) ** 3
        n = len(w)
        seg_pan = np.linspace(pan * 0.9, pan * 0.15, n)
        i = int(T_FLY0 * SR)
        l = np.cos((seg_pan + 1) * np.pi / 4)
        r = np.sin((seg_pan + 1) * np.pi / 4)
        buf[i:i + n, 0] += w * e * l * 0.5
        buf[i:i + n, 1] += w * e * r * 0.5

    # Impact: sub kick, crack, bright chord stab.
    kick_t = np.arange(int(1.2 * SR)) / SR
    kick = sine_sweep(150, 38, 1.2, 6) * np.exp(-kick_t / 0.38)
    place(dry, T_IMPACT, np.tanh(kick * 1.6), 0, 0.85)
    crack = bandnoise(0.4, 1800, 12000, rng) * env_exp(int(0.4 * SR), 0.05)
    place(buf, T_IMPACT, crack, 0, 0.55)
    chord_t = np.arange(int(2.6 * SR)) / SR
    chord = np.zeros_like(chord_t)
    for f in (261.63, 329.63, 392.0, 523.25, 783.99):
        for det in (-0.004, 0.004):
            chord += signal.sawtooth(2 * np.pi * f * (1 + det) * chord_t) * 0.12
    b, a = signal.butter(2, 3200 / (SR / 2))
    chord = signal.lfilter(b, a, chord) * np.exp(-chord_t / 0.75) * np.minimum(chord_t / 0.006, 1)
    place(buf, T_IMPACT, chord, 0, 0.55)

    # Buttons and d-pad: chiptune arpeggio C6 E6 G6 C7 E7.
    for k, (bt, f) in enumerate(zip(T_BUTTONS, (1046.5, 1318.5, 1568.0, 2093.0, 2637.0))):
        place(buf, bt, blip(f, 0.16, rng), (-0.5, 0.4, -0.2, 0.5, 0.0)[k], 0.17)

    # Letters: light ticks; subtitle: a quick shimmer.
    for k in range(7):
        tick = pluck(880 * 2 ** (k * 2 / 12), 0.18) * 0.4
        tick[: int(0.05 * SR)] += bandnoise(0.05, 3000, 11000, rng) * env_exp(int(0.05 * SR), 0.008)
        place(buf, T_WORD0 + k * T_LETTER_STEP, tick, -0.6 + k * 0.2, 0.16)
    place(buf, T_SUB0, sweep_noise(0.45, 3000, 9000, rng) * np.sin(np.linspace(0, np.pi, int(0.45 * SR))) ** 2, 0, 0.07)

    # Shine: glittering chime cluster.
    for k in range(9):
        f = 2093.0 * 2 ** (rng.integers(0, 14) / 12)
        place(buf, T_SHINE + k * 0.035, pluck(f, 1.2), float(rng.uniform(-0.7, 0.7)), 0.07)

    mix = reverb(buf, 2.2, rng, 0.42) + dry
    fade = np.ones(len(mix))
    tail = int(0.6 * SR)
    fade[-tail:] = np.linspace(1, 0, tail) ** 2
    mix *= fade[:, None]
    mix = np.tanh(mix * 1.1) / np.tanh(1.1)
    mix *= 0.89 / np.abs(mix).max()
    pcm = (mix * 32767).astype("<i2")
    path = os.path.join(OUT, "studio_splash_sfx.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("wrote", path, "%.2fs" % DURATION)


if __name__ == "__main__":
    build_art()
    build_audio()
