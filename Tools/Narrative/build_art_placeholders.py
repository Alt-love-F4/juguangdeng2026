#!/usr/bin/env python3
"""Build LOCAL labelled requirement cards, without API/network or replacing art.

Writes only Assets/Art/NarrativePlaceholders/*.png and the two Docs manifests.
Requires Pillow and Microsoft YaHei. Run --check for non-writing verification.
"""
from __future__ import annotations

import argparse
import copy
import csv
import hashlib
import io
import json
import re
from collections import Counter
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version

TSV = "策划文件/美术清单/02_美术需求可筛选表.tsv"
EMOTES = "策划文件/美术清单/03_角色表情差分清单.txt"
REGISTRY = "Assets/Resources/Dialogue/NarrativeStageVisuals.asset"
OBJECTS = "Assets/Settings/Resources/Luban/tbncsobject.json"
LINES = "Assets/Settings/Resources/Luban/tbncsconversationline.json"
OUT = "Assets/Art/NarrativePlaceholders"
MANIFEST = "Docs/NarrativeArtPlaceholderManifest.json"
DOC = "Docs/NarrativeArtPlaceholders.md"
FONT = Path("C:/Windows/Fonts/msyh.ttc")

CHARACTERS = {
    "CH01": "laurent", "CH02": "clovis", "CH03": "renaud", "CH04": "elion",
    "CH05": "alienor", "CH06": "queen", "CH07": "bertrand", "CH08": "sade",
    "CH09": "mamion", "CH10": "lucien", "CH11": "lesou", "CH12": "sylvain",
    "CH13": "antoine", "CH14": "lefebvre", "CH15": "duval", "CH16": "adele",
    "CH17": "emmanuel", "CH18": "sparrow", "CH19": "thibaut", "CH20": "octave",
    "CH21": "eugene", "CH22": "colette", "CH23": "pascal", "CH24": "marie",
    "CH25": "guest_a", "CH26": "guest_b", "CH27": "guest_c", "CH28": "guest_d",
    "CH29": "soldier_a", "CH30": "soldier_b", "CH31": "soldier_c", "CH32": "soldier_d",
}
SPEAKERS = {"CH04": "church.duke", "CH17": "sorel", "CH19": "church.thibaut"}
BG_FILES = {
    "BG01": ["SCE_一楼大厅对话场景日景.png"],
    "BG02": ["SCE_一楼大厅场景清晨.png"],
    "BG03": ["SCE_一楼大厅场景傍晚.png"],
    "BG04": ["SCE_一楼大厅场景夜晚.png"],
    "BG05": ["SCE_一楼大厅场景纸花装饰.png"],
    "BG06": ["SCE_二楼会客室对话场景日景.png", "SCE_二楼会客室场景杂乱.png"],
    "BG07": ["SCE_三楼宿舍对话场景日景.png", "SCE_三楼主角房间场景清晨.png"],
    "BG08": ["SCE_圣徒街场景1.png"], "BG09": ["SCE_圣徒街场景夜晚.png"],
    "BG10": ["SCE_沸点咖啡馆店面场景日景.png", "SCE_沸点咖啡馆店面场景萧条.png"],
    "BG11": ["SCE_布塔克俱乐部门口场景底图.png"],
    "BG12": ["SCE_布塔克俱乐部会客室场景底图.png"],
    "BG13": ["SCE_圣卷大教堂场景日景.png"],
    "BG14": ["SCE_大堂区露天市场场景日景.png"],
    "BG15": ["SCE_大堂区杂货铺内部场景日景.png"],
    "BG16": ["SCE_圣西尔万郊区场景日景.png"],
    "BG17": ["SCE_旧城区街道场景日景.png"],
    "BG18": ["SCE_吕西尼昂旧宅门前场景底图.png"],
    "BG19": ["SCE_教会看守所内部场景底图.png", "SCE_教会看守所内部场景清晨.png", "SCE_教会看守所内部场景傍晚.png"],
    "BG20": ["SCE_咖啡馆闭门场景蒂博剪影.png"],
    "BG21": ["SCE_咖啡馆吧台场景底图.png", "SCE_咖啡馆操作台场景底图.png", "SCE_咖啡馆出餐台场景底图.png"],
}
BG_KEYS = {
    "BG01": ["cafe.day"], "BG02": ["cafe.dawn"], "BG03": ["cafe.dusk"],
    "BG04": ["cafe.night"], "BG05": ["cafe.paper_flowers"],
    "BG06": ["cafe.second_floor.day", "cafe.second_floor.cluttered"],
    "BG07": ["cafe.dormitory.day", "cafe.bedroom.dawn"],
    "BG08": ["street.day", "oct07.street.day"], "BG09": ["street.night"],
    "BG10": ["cafe.exterior.day", "cafe.exterior.declining"],
    "BG11": ["club.exterior"], "BG12": ["club.reception"],
    "BG13": ["church.cathedral.day", "cathedral.day"], "BG14": ["market.day"],
    "BG15": ["market.shop.day"], "BG16": ["suburb.day"],
    "BG17": ["old.street.day"], "BG18": ["old.house.day"],
    "BG19": ["detention.interior", "detention.dawn", "detention.dusk"],
    "BG20": ["cafe.door.thibaut"], "BG21": ["cafe.bar.day", "cafe.workstation.day", "cafe.serving.day"],
}
# These are local planning labels, NOT new runtime keys or approved asset orders.
CARD_SPECS = {
    "CG02": ("protagonist_turn", "主角回头／邪恶线条；先确认主角外观，可拆人物、背景、线条。", "cg.protagonist_turn"),
    "CG03": ("knight_guidance", "老爷站起凝视玫瑰骑士画像，画像中骑士斩断荆棘；不是普通会客室。", "rose.club.knight_guidance"),
    "CG04": ("adele_sampling", "店外阿黛尔手持试饮盘；现有端杯立绘不等同本场CG。", "cg.adele_sampling"),
    "CG05": ("burning_letter", "白色信件被火焰吞噬，一张静态插画；不自动扩成视频。", "cg.burning_letter"),
    "CG06": ("duke_blessing_PENDING", "暂定：公爵露台祈福，双颊环形圣痕放光；未批准必画。", "cg.duke_blessing"),
    "P02": ("rose_knight_painting", "玫瑰骑士像／玫瑰王肖像，共用候选；与CG03的整体构图区分。", "prop.rose_knight_painting"),
    "P03": ("sylvain_card", "西尔万名片与法语手写标题；版式或字体方案，不一定新画插画。", "prop.sylvain_card"),
    "P04": ("king_coins", "十几个国王币；普通金钱UI不能直接视作满足，材质／币面需确认。", "prop.king_coins"),
    "P06": ("two_shadows", "旧宅门前一高一矮两个人影，单组叠层，不扩成两套角色差分。", "prop.old_house_shadows"),
    "P07": ("evicted_family", "被驱逐的一家人，单组群像／剪影，配旧宅门前；不扩四套立绘。", "prop.evicted_family"),
    "P08": ("church_search_group", "教会查店队伍：前排复用CH33，后排一组人群剪影。", "prop.church_search_group"),
    "P09": ("wide_brim_hat_PENDING", "暂定：风吹来的宽檐毡帽；选择贴图方案后才制透明素材。", "prop.wide_brim_hat"),
    "P10": ("cathedral_groups_PENDING", "暂定／有余力：教堂四组可点击剪影；王女戴宽檐帽，人数构图待定。", "prop.cathedral_groups"),
    "P11": ("newspaper_layout_PENDING", "展示方案待确认：推荐侧栏／商业广告的刊头、栏框、文字排版；非已批准CG。", "prop.newspaper_layout"),
    "FX03": ("golden_light_PENDING", "暂定：教堂金光／光束层或粒子贴图，关联CG06；未批准独立素材。", "fx.cathedral_golden_light"),
    "FX05": ("thibaut_drink_motion", "蒂博喝咖啡动作，手臂差分／骨骼／Tween方案待定；本静态卡不等于动作已完成。", "fx.thibaut_drink_motion"),
    "FX06": ("business_rating", "优秀评价烟花＋金钱；负面评价emoji铺屏约5秒；中等无动画。素材及实现待核。", "fx.business_rating"),
    "CH33": ("church_npc", "教会监察官／看守共用教会人员底图；10.23明确不要求专属表情差分。", "character.church_npc"),
    "CH35": ("tutorial_elderly_woman", "教程普通老太太／入场黑色剪影，需同类型老年女性模板；不能用男性或阿黛尔替代。", "character.tutorial_elderly_woman"),
}
# One card per character covers all missing emotes and any requested outfit variant.
VARIATIONS = {
    "CH05": "无圣痕平民乔装／宽檐帽；indoor室内装不自动等同合格乔装。",
    "CH17": "10.14工人装与常规中产装区别待确认，现有常规图不证明工人装已交付。",
    "CH24": "玛丽／雅克同一身份；女性与男性身份的造型差异待确认，不按两个人重复下单。",
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def asset_ref(root: Path, path: str) -> dict:
    p = root / path
    if not p.is_file():
        raise FileNotFoundError(f"Unverified asset reference: {path}")
    result = {"path": path, "sha256": sha(p)}
    if p.suffix.lower() in (".png", ".jpg", ".jpeg"):
        with Image.open(p) as image:
            result["width"], result["height"] = image.size
    return result


def registry_entries(root: Path, guid_paths: dict) -> dict:
    text = (root / REGISTRY).read_text(encoding="utf-8-sig")
    section = text.split("  entries:\n", 1)[1].split("  aliases:\n", 1)[0]
    result = {}
    for chunk in section.split("  - key: ")[1:]:
        key = chunk.splitlines()[0].strip()
        m = re.search(r"texture: \{[^\n]*guid: ([0-9a-f]{32})", chunk)
        result[key] = {
            "key": key,
            "placeholder": bool(re.search(r"\n    placeholder: 1", chunk)),
            "useSceneBackground": bool(re.search(r"\n    useSceneBackground: 1", chunk)),
            "assetPath": guid_paths.get(m.group(1), "") if m else "",
        }
    return result


def emote_inventory(root: Path) -> dict:
    result = {}
    speaker = None
    for line in (root / EMOTES).read_text(encoding="utf-8-sig").splitlines():
        header = re.match(r"^\[([^\]]+)\]", line)
        if header:
            speaker = header.group(1)
            result[speaker] = {"historical": [], "current": [], "evidence": []}
        elif speaker and "[ ]" in line:
            tag = line.split("[ ]", 1)[1].split("；", 1)[0].strip()
            result[speaker]["historical"].append(tag)
    lines = json.loads((root / LINES).read_text(encoding="utf-8-sig"))
    for line in lines:
        speaker, tag = line.get("speakerKey", ""), line.get("emote", "")
        if tag and speaker:
            item = result.setdefault(speaker, {"historical": [], "current": [], "evidence": []})
            if tag not in item["current"]:
                item["current"].append(tag)
                item["evidence"].append({"lineId": line["lineId"], "emote": tag})
    return result


def source_files(root: Path, text: str) -> list[dict]:
    """Resolve old abbreviated document names, never pretend P indices were revalidated."""
    names = re.findall(r"([^/\\；：]+?\.(?:docx|xlsx))", text)
    matches = []
    for name in names:
        name = name.strip()
        for path in sorted((root / "策划文件/文案").rglob(name)):
            matches.append(asset_ref(root, path.relative_to(root).as_posix()))
    return matches


def wrap(text: str, draw: ImageDraw.ImageDraw, font: ImageFont.FreeTypeFont, width: int) -> list[str]:
    result = []
    for paragraph in text.splitlines() or [""]:
        line = ""
        for char in paragraph:
            if line and draw.textlength(line + char, font=font) > width:
                result.append(line)
                line = char
            else:
                line += char
        result.append(line)
    return result


def render_card(item: dict, font_path: Path) -> tuple[bytes, dict]:
    portrait = item["id"].startswith("CH")
    width, height = (768, 1024) if portrait else (1920, 1080)
    image = Image.new("RGB", (width, height), (244, 244, 241))
    draw = ImageDraw.Draw(image)
    margin = 38 if portrait else 76
    title = f'{item["id"]}  {item["name"]}'
    fields = [
        "类型：" + item["category"],
        "清单状态（原记录）：" + item["status"],
        "当前处理：" + item["decisionReason"],
        "需求：" + item["requirement"],
        "本卡待办：" + item["cardRequirement"],
        "来源（历史锚点，未重新校页）：" + item["sourceEvidence"],
        "配置提示：" + item["cardConfiguration"],
        "备注：" + (item["notes"] or "无附加备注"),
    ]
    if item.get("missingEmotes"):
        fields.append("待核表情标签（可合并，不是一词一图）：" + "、".join(item["missingEmotes"]))
    title_font = ImageFont.truetype(str(font_path), 31 if portrait else 52)
    title_lines = wrap(title, draw, title_font, width - margin * 2)
    top = 112 if portrait else 142
    for line in title_lines:
        draw.text((margin, top), line, font=title_font, fill=(38, 42, 46))
        top += title_font.size + 9
    top += 22
    bottom = height - (95 if portrait else 108)
    min_size, max_size = ((16, 23) if portrait else (22, 34))
    for size in range(max_size, min_size - 1, -1):
        font = ImageFont.truetype(str(font_path), size)
        line_height = size + (8 if portrait else 11)
        wrapped = [wrap(field, draw, font, width - margin * 2) for field in fields]
        needed = sum(len(lines) * line_height + 12 for lines in wrapped)
        if top + needed <= bottom:
            break
    else:
        raise ValueError(f'Card text cannot fit legibly: {item["id"]}; split or shorten without truncating')
    draw.text((margin, 36 if portrait else 54), "美术需求占位 / 非正式画面 / 非已验收资产",
              font=ImageFont.truetype(str(font_path), 18 if portrait else 30), fill=(82, 88, 94))
    y = top
    for lines in wrapped:
        for line in lines:
            draw.text((margin, y), line, font=font, fill=(50, 54, 58))
            y += line_height
        y += 12
    footer_font = ImageFont.truetype(str(font_path), 15 if portrait else 25)
    draw.text((margin, height - 65), "仅本地文字排版 · 未调用图片 API · 暂定项须先确认", font=footer_font, fill=(82, 88, 94))
    data = io.BytesIO()
    image.save(data, format="PNG", optimize=False, compress_level=9)
    raw = data.getvalue()
    return raw, {
        "width": width, "height": height, "mode": "RGB", "bodyFontSize": size,
        "sha256": hashlib.sha256(raw).hexdigest(),
        "pixelSha256": hashlib.sha256(image.tobytes()).hexdigest(),
        "layoutBottom": y, "layoutLimit": bottom,
    }


def build(root: Path, font_path: Path) -> tuple[dict, dict[str, bytes]]:
    all_assets = sorted((root / "Assets").rglob("*"))
    pngs = [p for p in all_assets if p.suffix.lower() == ".png" and OUT not in p.as_posix()]
    by_name = {}
    for path in pngs:
        by_name.setdefault(path.name, []).append(path.relative_to(root).as_posix())
    guid_paths = {}
    for path in all_assets:
        if path.suffix == ".meta":
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            match = re.search(r"^guid: ([a-f0-9]{32})$", text, re.MULTILINE)
            if match:
                guid_paths[match.group(1)] = path.with_suffix("").relative_to(root).as_posix()
    registry = registry_entries(root, guid_paths)
    objects = {o["objId"]: o for o in json.loads((root / OBJECTS).read_text(encoding="utf-8-sig"))}
    emotes = emote_inventory(root)
    with (root / TSV).open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream, delimiter="\t"))
    if len(rows) != 93 or len({r["编号"] for r in rows}) != 93:
        raise ValueError("Expected 93 unique reviewed requirements; review code before extending source")
    requirements, cards = [], {}
    for row in rows:
        rid = row["编号"]
        item = {
            "id": rid, "category": row["类别"], "name": row["名称"], "status": row["状态"],
            "requirement": row["制作或确认内容"], "sourceEvidence": row["文案依据"],
            "configuration": row["配置对应"], "notes": row["备注"],
            "historicalExistingResources": row["现有资源"],
            "disposition": "", "assetPath": "", "suggestedRegistryKeys": [],
            "proposedUnregisteredKeys": [], "existingAssets": [], "registryEvidence": [],
            "decisionReason": "", "sourceAnchorVerified": False, "approvedArtOrder": False,
            "resolvedSourceFiles": source_files(root, row["文案依据"]),
        }
        paths = set()
        # Exact historical paths or filename relocation; do not infer identity from concept-art names.
        old_paths = re.findall(r"Assets/[^；\n]+?\.(?:png|jpg|psd)", row["现有资源"])
        for old_path in old_paths:
            if (root / old_path).is_file():
                paths.add(old_path)
            elif Path(old_path).name in by_name:
                paths.update(by_name[Path(old_path).name])
        keys = []
        if rid in BG_FILES:
            for name in BG_FILES[rid]:
                candidates = by_name.get(name, [])
                if not candidates:
                    raise FileNotFoundError(f"Background missing: {name}")
                paths.update(candidates)
            keys = BG_KEYS[rid]
            item["disposition"] = "existing"
            item["decisionReason"] = "当前已有实际图片，保留，不因旧清单待补而重复制卡；画面风格与构图仍待人工验收。"
            if rid in {"BG01", "BG02", "BG03", "BG04", "BG05", "BG06", "BG07", "BG21"}:
                item["disposition"] = "reuse"
                item["decisionReason"] = "普通店内对白使用scene.cafe实景；保留既有经营图与对话构图，不新制楼层背景卡。光照／装饰宿主接线另验收。"
                keys += ["scene.cafe"]
            if rid == "BG20":
                item["decisionReason"] = "蒂博闭门剪影的特殊合成图已存在并注册；它是店内实景规则例外，不能替换为scene.cafe。"
        elif rid == "CG01":
            paths.update(by_name.get("SCE_圣徒街场景1.png", []))
            keys = BG_KEYS["BG08"]
            item["disposition"] = "reuse"
            item["decisionReason"] = "与BG08共用现存圣徒街日景，不重复制卡；繁忙行人层是否足够仍需美术确认。"
        elif rid in CHARACTERS:
            character = CHARACTERS[rid]
            directory = root / "Assets/Resources/Characters" / character
            paths.update(p.relative_to(root).as_posix() for p in sorted(directory.glob("*.png")) if "placeholder" not in p.name and p.name != "avatar.png")
            speaker = SPEAKERS.get(rid, character)
            current_objects = [o for k, o in objects.items() if k in {speaker, character, "character." + character}]
            item["objectEvidence"] = [{k: o.get(k, "") for k in ("objId", "portraitPath", "portraitIndoorPath")} for o in current_objects]
            inventory = emotes.get(speaker, {"historical": [], "current": [], "evidence": []})
            item["emoteEvidence"] = inventory
            files = {p.stem for p in directory.glob("*.png")}
            nonvisual = {"neutral", "平静", "常态", "normal"}
            # Labels are an audit request, not automatic requests for a frame per word.
            missing = [t for t in inventory["current"] if t not in files and t not in nonvisual]
            if not missing:
                missing = [t for t in inventory["historical"] if t.split("（", 1)[0] not in files and t.split("（", 1)[0] not in nonvisual]
            item["missingEmotes"] = sorted(set(missing))
            if "不列入本期必画" in row["状态"]:
                item["disposition"] = "existing" if paths else "runtimeOnly"
                item["decisionReason"] = "原需求明确不列入本期必画；现存库存仅登记，不追加立绘或差分订单。"
                item["missingEmotes"] = []
            elif missing or rid in VARIATIONS or not paths:
                slug = character + "_variants"
                card_req = "现有底图保留；只列尚未证明具备的表情／服装差分，同角色集中一张卡。"
                if rid in VARIATIONS:
                    card_req += VARIATIONS[rid]
                if not paths:
                    card_req = "常规底图未找到，按原需求补图。" + card_req
                item.update({"disposition": "placeholder", "cardSlug": slug, "cardRequirement": card_req,
                             "cardConfiguration": "Object " + speaker + "；差分资源尚未接线，不修改既有portraitPath。",
                             "decisionReason": "现有底图不覆盖全部标记差分；本卡只跟踪缺口，不替换底图，也不按标签逐张下单。"})
            else:
                item["disposition"] = "existing"
                item["decisionReason"] = "旧清单待补已过时：当前已有对应人物底图，保留且不重复制卡；身份、构图和接线仍需人工验收。"
        elif rid == "P01":
            paths.update(p.relative_to(root).as_posix() for p in (root / "Assets/Art/Func/信封").glob("*.png"))
            item["disposition"] = "reuse"
            item["decisionReason"] = "闭合／打开信封与信纸图现存，复用，不重复生成。"
        elif rid == "P05":
            paths.add("Assets/Resources/Characters/bertrand/neutral.png")
            item["disposition"] = "reuse"
            item["decisionReason"] = "贵族剪影使用既有贝特朗底图与舞台效果，不再重复绘制。"
        elif rid in {"FX01", "FX02", "FX04", "FX07"}:
            item["disposition"] = "runtimeOnly"
            item["decisionReason"] = "程序演出／现有物件与裁切遮罩可复用，不新增静态画图量；效果实现与运行回归不由本清单认定完成。"
            if rid in {"FX01", "FX02", "FX07"}:
                paths.add("Assets/Resources/Dialogue/PresentationEffectLibrary.asset")
            if rid == "FX02":
                paths.update(["Assets/Resources/Characters/adele/neutral.png", "Assets/Resources/Characters/emmanuel/neutral.png"])
            if rid == "FX04":
                paths.add("Assets/Resources/FreeCafe/ArtLibrary.asset")
        elif rid.startswith("MAP"):
            item["disposition"] = "reuse"
            item["decisionReason"] = "保留既有地图／图标，地点语义和临时测试点需核；不自动扩画地图图标或全屏背景。"
            if rid == "MAP10":
                paths.update(p.relative_to(root).as_posix() for p in (root / "Assets/Resources/Art/Map/Locations").glob("*.png"))
            if rid == "MAP11":
                paths.update(p.relative_to(root).as_posix() for p in (root / "Assets/Art/Map/新地图系统/地图").glob("*.png"))
        elif rid == "REF01":
            paths.add("策划文件/美术清单/文案内嵌参考图预览.jpg")
            item["disposition"] = "existing"
            item["decisionReason"] = "徽记参考已存在；透明源文件与UI用途待确认，不重新设计。"
        elif rid == "REF02":
            item["disposition"] = "runtimeOnly"
            item["decisionReason"] = "没有明确画面订单，不因结局或设定段落而新造CG；旧备注对10.29／11.05缺稿的判断已过时。"
        if rid in CARD_SPECS:
            slug, detail, key = CARD_SPECS[rid]
            item.update({"disposition": "placeholder", "cardSlug": slug, "cardRequirement": detail,
                         "cardConfiguration": key + ("（既有注册key；占位，不代表已接线）" if key == "rose.club.knight_guidance" else "（建议名，未注册，未接线）"),
                         "decisionReason": "当前未找到满足此明确画面／差分要求的专用素材；仅提供需求卡，不声称制作或接线完成。"})
            if key == "rose.club.knight_guidance":
                keys = [key]
            else:
                item["proposedUnregisteredKeys"] = [key]
            if "PENDING" in slug or rid == "FX05":
                item["decisionReason"] = "方案暂定／待核，先确认再制正式素材；本卡不是已批准订单，也不是效果已实现。"
        if rid == "CH34":
            paths.add("Assets/Resources/Characters/guest_a/neutral.png")
            item["disposition"] = "reuse"
            item["decisionReason"] = "原需求允许通用市民模板，现有guest_a男客可复用；记者身份与入座一致性由主集成验收，不重复造专属角色。"
        if not item["disposition"]:
            raise ValueError(f"Unreviewed row: {rid}")
        item["suggestedRegistryKeys"] = [k for k in keys if k in registry]
        item["registryEvidence"] = [registry[k] for k in keys if k in registry]
        for evidence in item["registryEvidence"]:
            if evidence["assetPath"] and (root / evidence["assetPath"]).is_file():
                paths.add(evidence["assetPath"])
        item["existingAssets"] = [asset_ref(root, p) for p in sorted(paths) if (root / p).is_file()]
        item["assetReviewStatus"] = "file-presence-and-reference-audit; not artistic approval or Unity playback verification"
        if item["disposition"] == "placeholder":
            item["assetPath"] = f'{OUT}/{rid}_{item["cardSlug"]}.png'
            raw, image_info = render_card(item, font_path)
            item["placeholderImage"] = image_info
            cards[item["assetPath"]] = raw
        elif item["existingAssets"]:
            item["assetPath"] = item["existingAssets"][0]["path"]
        requirements.append(item)
    manifest = {
        "schemaVersion": 1,
        "source": {"path": TSV, "sha256": sha(root / TSV)},
        "additionalSources": [{"path": p, "sha256": sha(root / p)} for p in [EMOTES, REGISTRY, OBJECTS, LINES]],
        "registrySnapshot": registry,
        "generator": {"path": "Tools/Narrative/build_art_placeholders.py", "sha256": sha(Path(__file__)),
                      "pillowVersion": pillow_version, "font": str(font_path), "fontSha256": sha(font_path),
                      "networkCalls": 0, "imageApiCalls": 0, "overwriteExistingArt": False},
        "policy": {
            "history": "TSV状态／旧配置ID／P锚点原样保存，不代表当前事实；未逐段重新校页。resolvedSourceFiles只证明当前同名文件存在及其hash。",
            "binding": "本工具不写注册表／Excel／runtime；key与注册证据为生成时快照。主集成通过Unity新增requirement.<ID>和提议key后，check允许安全的需求卡绑定变化。",
            "cafe": "普通咖啡馆对白scene.cafe；不把经营原图或占位卡绑成普通店内全屏背景。BG20特殊合成图例外。",
            "orders": "任何需求卡都不是正式画稿或美术验收；pending／可选项不是已批准订单。没有新增结局CG订单。",
            "references": "仅本地文件读取、hash与文字排版；不读取密钥，不上传，不联网。",
        },
        "summary": {"requirementCount": len(requirements), "placeholderPngCount": len(cards),
                    "dispositions": dict(sorted(Counter(i["disposition"] for i in requirements).items())),
                    "placeholderTypes": dict(sorted(Counter(re.match(r"[A-Z]+", i["id"]).group() for i in requirements if i["disposition"] == "placeholder").items()))},
        "requirements": requirements,
    }
    return manifest, cards


def documentation(manifest: dict) -> bytes:
    summary = manifest["summary"]
    lines = ["# 叙事美术需求占位卡（纯本地，不调用图片 API）", "",
             "## 范围与交接", "",
             f'- 审阅原表 **{summary["requirementCount"]} 条**，输出 **{summary["placeholderPngCount"]} 张**素色中文需求卡。所有需求都在 JSON 中有处置记录，不是所有旧“待补”都再画一张。',
             f'- 需求卡分组：`{json.dumps(summary["placeholderTypes"], ensure_ascii=False)}`；处置：`{json.dumps(summary["dispositions"], ensure_ascii=False)}`。',
             "- 当前资源中已有圣徒街、俱乐部、监狱、店面、楼层对话图及大多数角色底图；旧清单的待补状态已过时。仅核文件、引用及hash，不将存在等同美术验收。",
             "- 单角色所有待核表情及服装集中一张卡；小声、点头等可能是演技，不机械一词一张。玛丽／雅克、sorel／emmanuel、蒂博、埃利昂均按同一身份处理。",
             "- 制卡脚本只修改本脚本、`Assets/Art/NarrativePlaceholders/*.png`、本 MD 和 `Docs/NarrativeArtPlaceholderManifest.json`。不写 Excel、生成表、StageVisuals、Prefab、Runtime，不执行 Unity 作业。",
             "- 未调用 PackyAPI、OpenAI 或任何网络／图片 API；不需要也不检查密钥。Pillow 字体排版不是 AI 生图。", "",
             "## 素材及绑定契约", "",
             "1. `requirements[]` 每项含 `id/category/name/status/requirement/sourceEvidence/configuration/notes/disposition/assetPath/suggestedRegistryKeys`。",
             "2. `disposition=placeholder` 时 `assetPath` 为新卡；否则它是现有参考（或空），**不能把每个assetPath都拿去覆盖注册贴图**。",
             "3. `existingAssets` 保留真实路径、尺寸、SHA256；`registryEvidence` 与 `registrySnapshot` 是生成时的引用快照。`suggestedRegistryKeys` 只列当时已核实存在的 key；`proposedUnregisteredKeys` 是当时的未注册提议，后续是否可用须查当前注册表。",
             "4. 主集成仅通过Unity为31张卡新增 `requirement.<ID>` 与提议key；CG03可把原先无texture的 `rose.club.knight_guidance` 绑定到对应需求卡。绑定仍保留 `placeholder=true` 并使用白色tint；绝不覆盖已解析真实美术。本工具不编辑注册表。",
             "5. 普通店内对白始终 `scene.cafe`，不生成新的楼层背景卡、不覆盖经营原图。蒂博闭门特殊合成图已实际存在并注册，保留例外。",
             "6. 暂定CG06、P09、P10、P11、FX03及动作方案待核的FX05均明确标注未批准；不能把卡片数量当正式画图订单数量。没有根据结局表擅增15张结局CG。",
             "7. 1920×1080用于CG／道具组／特效组，768×1024用于角色差分。卡片本身不透明、非实际剪影，角色卡不应直接替换现有角色立绘。",
             "8. 卡面文字固定为制卡时记录；主集成后续注册提议key不会触发重绘，不能把卡面旧“未注册”标签当成实时注册诊断。",
             "9. CG04正式片段拟用现稿P018–P024，P025恢复店外背景；CG05焚信必须先核当前分支与锚点，再显式恢复。此处仅记录主集成计划，不声称已接线；旧P编号不当成当前事实。", "",
             "## 历史来源与当前事实", "",
             "- TSV及其文案P编号、旧配置ID、旧资源路径原样保留为历史证据；`sourceAnchorVerified=false`，不声称它们与当前段号一致。",
             "- `resolvedSourceFiles` 仅记录当前同名源文档hash；不把短文件名映射或文件存在当作剧情接线证据。",
             "- 旧REF02对10.29／11.05缺稿的判断已过时；全文入库与关系图、自然触发、Unity验收仍分别判断。",
             "- 参考图、地图和现有图标先复用；未知头像或临时地点用途待确认，不自动扩成全屏背景或新的正式地点。", "",
             "## 本地重现和只读检查", "", "```powershell",
             "$python = 'C:/Users/AltF4/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'",
             "& $python Tools/Narrative/build_art_placeholders.py",
             "& $python Tools/Narrative/build_art_placeholders.py --check", "```", "",
             "- 首次运行仅创建缺失PNG；重跑若字节不同即报错，**绝不覆盖**任何已有图。替换正式素材应另存新文件，由主集成变更引用。",
             "- 同一Pillow与字体hash环境下PNG字节、像素hash可复现。`--check`只读：重新排版逐一校验PNG／像素／尺寸／排版不越界，同时核93项及JSON和MD未漂移。允许且报告主集成的安全需求卡绑定漂移，不因注册表hash单独变化失败。",
             "- 注册表绑定后优先用 `--check`，无需刷新原审计快照；它只允许新增映射到对应卡的requirement／提议key，或空占位绑定卡。删除旧key、替换真实图、错误卡ID、取消placeholder或破坏scene.cafe会失败。脚本不重写主集成绑定。",
             "- 默认构建可主动刷新JSON／MD的证据快照，已有PNG仍受不可覆盖保护；字体或需求文本改变导致旧PNG不同会停下，不自动重制。",
             "- 不生成 `.meta`，由Unity正常导入生成；引用请由主集成获得实际GUID，不猜GUID。", "", "## 逐项处置", "",
             "|编号|名称|原状态|处置|实际资源或需求卡|", "|---|---|---|---|---|"]
    for item in manifest["requirements"]:
        values = [item["id"], item["name"], item["status"], item["disposition"], item["assetPath"] or "仅程序／未下单"]
        lines.append("|" + "|".join(v.replace("|", "／").replace("\n", " ") for v in values) + "|")
    lines += ["", "## 验证范围", "", "此工具的成功只证明本地占位卡、完整台账、现存引用与确定性检查通过。未启动Unity、未测试演出、不证明结局已接线。结束面板与资源绑定由主集成负责。", ""]
    return "\n".join(lines).encode("utf-8")



def normalize_safe_registry_drift(baseline: dict, fresh: dict) -> tuple[dict, bool]:
    """Registry evidence is a snapshot; safe Unity card binding is not image drift.

    Preserve all pre-existing real art/scene entries. Only mapped requirement cards
    may occupy the added keys or an old empty placeholder. Never writes registry.
    """
    original_registry = baseline.get("registrySnapshot", {})
    current_registry = fresh.get("registrySnapshot", {})
    if original_registry == current_registry:
        # Hash may change for harmless serialization/ordering/tint formatting.
        registry_drift = any(a["sha256"] != b["sha256"] for a, b in zip(
            baseline["additionalSources"], fresh["additionalSources"]
        ) if a["path"] == REGISTRY and b["path"] == REGISTRY)
    else:
        registry_drift = True
    if not registry_drift:
        return fresh, False
    if not original_registry:
        raise ValueError("Registry drift without baseline snapshot: review and rebuild manifest first")
    allowed = {}
    for item in baseline["requirements"]:
        if item["disposition"] != "placeholder":
            continue
        path = item["assetPath"]
        for key in ["requirement." + item["id"], *item["suggestedRegistryKeys"], *item["proposedUnregisteredKeys"]]:
            if key in allowed and allowed[key] != path:
                raise ValueError(f"Ambiguous allowed binding: {key}")
            allowed[key] = path
    for key, original in original_registry.items():
        current = current_registry.get(key)
        if current is None:
            raise ValueError(f"Original registry key removed: {key}")
        if current == original:
            continue
        # Resolved real art may not be replaced, even by a mapped requirement.
        original_is_our_card = original.get("assetPath", "").startswith(OUT + "/")
        originally_empty_placeholder = original["placeholder"] and not original["assetPath"] and not original["useSceneBackground"]
        if key not in allowed or not (original_is_our_card or originally_empty_placeholder):
            raise ValueError(f"Original real art/scene/registry entry changed: {key}")
        if current["assetPath"] != allowed[key] or not current["placeholder"] or current["useSceneBackground"]:
            raise ValueError(f"Unsafe requirement-card replacement: {key}")
    for key, current in current_registry.items():
        if key in original_registry:
            continue
        if key not in allowed:
            raise ValueError(f"Unreviewed additional registry entry: {key}")
        if current["assetPath"] != allowed[key] or not current["placeholder"] or current["useSceneBackground"]:
            raise ValueError(f"New requirement key must reference its labelled placeholder: {key}")
    normalized = copy.deepcopy(fresh)
    normalized["registrySnapshot"] = copy.deepcopy(original_registry)
    previous_sources = {i["path"]: i for i in baseline["additionalSources"]}
    for item in normalized["additionalSources"]:
        if item["path"] == REGISTRY:
            item["sha256"] = previous_sources[REGISTRY]["sha256"]
    previous_requirements = {i["id"]: i for i in baseline["requirements"]}
    for item in normalized["requirements"]:
        previous = previous_requirements[item["id"]]
        item["registryEvidence"] = copy.deepcopy(previous["registryEvidence"])
        previous_paths = {i["path"] for i in previous["existingAssets"]}
        # CG03 acquires its own card reference after binding; do not relabel it
        # "existing finished art" in the original audit snapshot.
        item["existingAssets"] = [i for i in item["existingAssets"]
                                  if i["path"] in previous_paths or not i["path"].startswith(OUT + "/")]
    return normalized, True


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--font", type=Path, default=FONT)
    parser.add_argument("--check", action="store_true", help="Read-only deterministic validation; permits safe Unity card-binding registry drift")
    args = parser.parse_args()
    root = args.project_root.resolve()
    manifest, cards = build(root, args.font)
    # Validate all prior files BEFORE making any writes; do not partially replace a set.
    for relative, raw in cards.items():
        target = root / relative
        if target.exists() and target.read_bytes() != raw:
            raise FileExistsError(f"Refusing to overwrite existing art: {relative}")
        if args.check and not target.is_file():
            raise FileNotFoundError(relative)
        info = next(i["placeholderImage"] for i in manifest["requirements"] if i["assetPath"] == relative)
        with Image.open(io.BytesIO(raw)) as image:
            image.verify()
        with Image.open(io.BytesIO(raw)) as image:
            assert image.size == (info["width"], info["height"])
            assert hashlib.sha256(image.tobytes()).hexdigest() == info["pixelSha256"]
            assert info["layoutBottom"] <= info["layoutLimit"]
    actual_pngs = {p.relative_to(root).as_posix() for p in (root / OUT).glob("*.png")}
    if actual_pngs - cards.keys():
        raise ValueError(f"Unexpected PNGs retained for manual review: {sorted(actual_pngs - cards.keys())}")
    registry_binding_drift = False
    if args.check:
        baseline = json.loads((root / MANIFEST).read_text(encoding="utf-8-sig"))
        manifest, registry_binding_drift = normalize_safe_registry_drift(baseline, manifest)
    outputs = {MANIFEST: json_bytes(manifest), DOC: documentation(manifest)}
    if args.check:
        for relative, raw in outputs.items():
            if (root / relative).read_bytes() != raw:
                raise ValueError(f"Manifest/document inventory drift: {relative}; rerun builder to refresh evidence")
    else:
        for relative, raw in cards.items():
            target = root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if not target.exists():
                # Exclusive creation: never truncate an image another agent just created.
                with target.open("xb") as stream:
                    stream.write(raw)
        for relative, raw in outputs.items():
            (root / relative).write_bytes(raw)
    print(json.dumps({"mode": "check" if args.check else "build", "passed": True,
                      **manifest["summary"], "safeRegistryBindingDrift": registry_binding_drift,
                      "apiCalls": 0, "networkCalls": 0}, ensure_ascii=False))


if __name__ == "__main__":
    main()
