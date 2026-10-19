#!/usr/bin/env python3
"""Six supplemental, LOCAL Pillow requirement cards. No network/API or Unity work.

Does not edit the original 93-item manifest, 31 images, registry or source tables.
"""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import io
import json
from pathlib import Path
from zipfile import ZipFile
import xml.etree.ElementTree as ET
from PIL import Image

BASE = Path(__file__).resolve().parents[2]
OUT = "Assets/Art/NarrativePlaceholders/Additional"
MANIFEST = "Docs/NarrativeAdditionalArtPlaceholderManifest.json"
DOC = "Docs/NarrativeAdditionalArtPlaceholders.md"
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
SPECS = [
    ("CG07", "marie_moonlit", "月光下墙角的玛丽", "MA70.10.29-S1.docx", [12],
     "当前原稿明确CG需求／尚未制作", "cg.marie_moonlit", "腰间别枪，靠窗桌旁站在墙角；月光只映出半身与半张脸。", "不是普通店内背景；特殊CG结束后由剧情明确恢复scene.cafe。"),
    ("CG08", "handshake_optional", "合作握手特写", "MA70.10.29-S1.docx", [591],
     "可复用或新增／非强制新增订单", "cg.handshake", "合作达成时的握手特写；先查是否可复用，不要求另画全景。", "原文允许复用或新增；需求卡不等于批准新CG。"),
    ("CG09", "rainy_triad", "雨中的玛丽、主角与勒苏", "RE70.11.23-S1.docx", [228],
     "当前原稿明确CG需求／尚未制作", "cg.rainy_triad", "雨中三人，从主角脚后方视角看向玛丽与勒苏。", "构图按原稿；不把三人CG拆成三个新角色订单。"),
    ("CG10", "marie_fallen_pending", "雨水中倒地的玛丽", "RE70.11.23-S1.docx", [292,293],
     "CG或立绘下移替代／方案待确认", "cg.marie_fallen", "玛丽倒地，血与雨水混合；周围人群奔向勒苏。原文允许立绘向下移出屏幕替代。", "P293明确fallback：不能做CG时用立绘下移动作；不得把新增CG写成唯一必需方案。"),
    ("CG11", "alienor_corner_optional", "暖光角落中的王女", "MA70.11.04-S1.docx", [453,476],
     "可省略／原稿明确复杂可不做", "cg.alienor_corner", "王女坐在咖啡馆角落，手撑下巴沉思；周围昏暗，一束暖光打在脸上忽明忽暗。", "两处相同演出共用一张需求卡；原文允许复杂时省略。不是普通店内对白的新背景。"),
    ("P12", "cafe_tunnel_diagram", "咖啡馆建筑与双密道复合示意图", "MA70.10.29-S1.docx", [384],
     "当前原稿明确示意图需求／尚未制作", "cg.cafe_tunnel_diagram", "建筑平面＋地下剖面：两条密道分别通往灰墙学院与圣西尔万郊区。红色阴影标大型排水主管和承重地基，表明无法直接横向连通；高亮经咖啡馆内部中转路线。", "这是单张叙事复合示意图，不创建新的玩法地图、可走通道或经营系统。"),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def load_paragraphs(path):
    with ZipFile(path) as archive:
        tree = ET.fromstring(archive.read("word/document.xml"))
    paragraphs = ["".join(node.text or "" for node in paragraph.iter(W + "t")).strip()
                  for paragraph in tree.iter(W + "p")]
    return [text for text in paragraphs if text]


def build(root):
    spec = importlib.util.spec_from_file_location("local_requirement_renderer", root / "Tools/Narrative/build_art_placeholders.py")
    renderer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(renderer)
    documents = {}
    requirements, cards = [], {}
    for rid, slug, name, filename, anchors, status, key, detail, notes in SPECS:
        if filename not in documents:
            matches = sorted((root / "策划文件/文案").rglob(filename))
            if len(matches) != 1:
                raise ValueError(f"Expected one current source: {filename}, found {len(matches)}")
            source = matches[0]
            documents[filename] = (source, load_paragraphs(source))
        source, paragraphs = documents[filename]
        evidence = [{"paragraph": n, "anchor": f"P{n:03}", "text": paragraphs[n-1]} for n in anchors]
        if not all("CG" in e["text"].upper() or "美术" in e["text"] or "画面" in e["text"] for e in evidence):
            raise ValueError(f"Source marker drift: {rid}")
        relative = source.relative_to(root).as_posix()
        source_evidence = relative + " " + "／".join(e["anchor"] for e in evidence)
        marker_quotes = "\n".join(e["anchor"] + " " + e["text"] for e in evidence)
        item = {
            "id": rid, "category": "道具／叙事复合示意图" if rid == "P12" else "CG／插画",
            "name": name, "status": status, "requirement": detail,
            "sourceEvidence": source_evidence, "configuration": key + "（建议注册名；本工具不接线）",
            "notes": notes, "disposition": "placeholder", "assetPath": f"{OUT}/{rid}_{slug}.png",
            "suggestedRegistryKeys": [key], "proposedUnregisteredKeys": [key],
            "suggestedRegistryKeysVerified": False,
            "sourceFile": {"path": relative, "sha256": sha(source), "nonemptyParagraphCount": len(paragraphs)},
            "sourceParagraphs": evidence, "sourceAnchorVerified": True,
            "approvedArtOrder": False, "existingAssets": [], "registryEvidence": [],
            "cardRequirement": "原稿标记原文：\n" + marker_quotes,
            "cardConfiguration": key + "（建议key，是否已注册以Unity当前表为准）",
            "decisionReason": "仅本地素色需求卡；未制作画稿、未绑定、未验收。" + ("原稿允许复用／省略／演出替代，须先确认方案。" if rid in {"CG08","CG10","CG11"} else ""),
        }
        raw, metadata = renderer.render_card(item, renderer.FONT)
        item["placeholderImage"] = metadata
        requirements.append(item)
        cards[item["assetPath"]] = raw
    manifest = {
        "schemaVersion": 1,
        "source": {"path": "策划文件/文案", "kind": "current-docx-nonempty-paragraphs", "files": [r["sourceFile"] for r in requirements if r["id"] in {"CG07","CG09","CG11"}]},
        "primaryManifest": "Docs/NarrativeArtPlaceholderManifest.json",
        "generator": {"path": "Tools/Narrative/build_additional_art_placeholders.py", "sha256": sha(Path(__file__)),
                      "rendererPath": "Tools/Narrative/build_art_placeholders.py", "rendererSha256": sha(root / "Tools/Narrative/build_art_placeholders.py"),
                      "fontSha256": sha(renderer.FONT), "pillowVersion": renderer.pillow_version,
                      "networkCalls": 0, "imageApiCalls": 0, "overwriteExistingArt": False},
        "summary": {"requirementCount": 6, "placeholderPngCount": 6, "placeholderTypes": {"CG": 5, "P": 1}, "isSupplement": True},
        "policy": {"scope": "Only these six assigned current-source requirements; supplement does not alter the original 93-item manifest or 31 cards.",
                   "keys": "suggestedRegistryKeys are owner-requested proposed binding names, not verified existing registrations; this script never reads/writes the registry.",
                   "optional": "CG08 may reuse; CG10 may use actor-out fallback; CG11 may be omitted. None of the cards is approved finished artwork.",
                   "cafe": "Special illustrations/diagram only; ordinary cafe dialogue stays scene.cafe. P12 is not a gameplay map."},
        "requirements": requirements,
    }
    return manifest, cards


def documentation(manifest):
    lines = ["# 当前原稿补充美术需求卡（6项，非原93项替代）", "",
             "直接读取当前DOCX的word/document.xml，按非空段P编号提取原文；每项保留当前源文件SHA256、段号及精确标记原文。没有使用旧抽取稿作为生产来源。",
             "", "## 数量与范围", "", "- 补充6张1920×1080素色中文卡：5项CG与1项双密道复合示意图。",
             "- 原93项／31卡完全不改；补充清单独立，不回写其数量。两份清单合计99项、37张需求卡，不代表37张正式画稿订单。",
             "- CG08允许复用；CG10原文P293允许立绘下移出屏幕替代CG；CG11复杂时可省略，两处重复标记共用一项。",
             "- P12明确建筑平面＋地下剖面、两条密道、红色阻隔及店内中转，不是新玩法地图。",
             "- 本补充仅处理明确委派的6项，不声称重新验收全部61份路线原稿或37处标记。", "", "## 给主集成的JSON契约", "",
             "`requirements`包含与主清单相同的id/category/name/status/requirement/sourceEvidence/configuration/notes/disposition/assetPath/suggestedRegistryKeys。",
             "`sourceFile`、`sourceParagraphs`提供当前文件hash、非空段编号及精确原文；sourceAnchorVerified=true仅表示本次确已读取对应段。",
             "**suggestedRegistryKeys在这份补充中是主集成指定的建议名称，不代表已注册**；suggestedRegistryKeysVerified=false，同时保留proposedUnregisteredKeys。本工具不读取／修改注册表，后续Unity绑定不会影响--check。",
             "只允许主集成将明确挂点接到演出，绑定仍为placeholder，不能覆盖已解析真实美术；特殊CG后按来源显式恢复背景，普通咖啡馆保持scene.cafe。", "", "|ID|需求|建议key|当前锚点|", "|---|---|---|---|"]
    for item in manifest["requirements"]:
        lines.append("|" + "|".join([item["id"],item["name"],item["suggestedRegistryKeys"][0],"、".join(e["anchor"] for e in item["sourceParagraphs"])]) + "|")
    lines += ["", "## 本地生成与只读检查", "", "```powershell",
              "$python = 'C:/Users/AltF4/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'",
              "& $python Tools/Narrative/build_additional_art_placeholders.py",
              "& $python Tools/Narrative/build_additional_art_placeholders.py --check", "```", "",
              "首次仅创建缺失PNG；存在且不同则立即报错，绝不覆盖原图。--check重新读取当前DOCX、重排本地文字，校验6张PNG的字节、像素hash、尺寸、布局及清单。",
              "仅写本补充脚本、Additional/*.png、本JSON和MD；不编辑主93项清单、原31卡、Excel、生成表、Runtime、Prefab、注册表。不运行Unity、无任何网络／图片API。", ""]
    return "\n".join(lines).encode("utf-8")


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check",action="store_true")
    parser.add_argument("--project-root",type=Path,default=BASE)
    args=parser.parse_args()
    root=args.project_root.resolve()
    manifest,cards=build(root)
    for relative,raw in cards.items():
        path=root/relative
        if path.exists() and path.read_bytes()!=raw:
            raise FileExistsError(f"Refusing to overwrite existing art: {relative}")
        if args.check and not path.is_file():
            raise FileNotFoundError(relative)
        with Image.open(io.BytesIO(raw)) as image:
            image.verify()
        info=next(i["placeholderImage"] for i in manifest["requirements"] if i["assetPath"]==relative)
        with Image.open(io.BytesIO(raw)) as image:
            assert image.size==(1920,1080)
            assert hashlib.sha256(image.tobytes()).hexdigest()==info["pixelSha256"]
        assert info["layoutBottom"]<=info["layoutLimit"]
    assert len(cards)==6
    actual={p.relative_to(root).as_posix() for p in (root/OUT).glob("*.png")}
    if actual-cards.keys():
        raise ValueError("Unexpected additional image files; preserve for manual review")
    outputs={MANIFEST:encoded(manifest),DOC:documentation(manifest)}
    if args.check:
        for relative,raw in outputs.items():
            if (root/relative).read_bytes()!=raw:
                raise ValueError(f"Source/manifest drift: {relative}")
    else:
        for relative,raw in cards.items():
            path=root/relative
            path.parent.mkdir(parents=True,exist_ok=True)
            if not path.exists():
                with path.open("xb") as stream:stream.write(raw)
        for relative,raw in outputs.items():(root/relative).write_bytes(raw)
    print(json.dumps({"mode":"check" if args.check else "build","passed":True,**manifest["summary"],"apiCalls":0,"unityJobs":0},ensure_ascii=False))


if __name__=="__main__":main()
