"""Plan or guardedly append only the blocked P003-P045 rose.1011.s1 fragment.

Default mode is read-only and never runs generation or Unity.  ``--apply`` is
explicit, requires Text rows staged by import_rose_text.py, and appends only
Narrative rows under the shared source-write lock with backups and CAS checks.
The root remains blocked because B-C02/B-C03/B-C04/B-C05/B-C07 and B-B03/B-B04/
B-B05 are unresolved.  No Result, EventDate, Action, order, production fact,
or completed/appreciation fact is authored here.
"""
from __future__ import annotations

import argparse
from collections import Counter
from copy import copy
from datetime import datetime
import hashlib
import json
from pathlib import Path
import shutil
import sys

import openpyxl

ROOT = Path(__file__).resolve().parents[2]
NARRATIVE = ROOT / "Assets/ConfigTables/Data/Narrative/Narrative.xlsx"
TEXT = ROOT / "Assets/ConfigTables/Data/Shared/Text.xlsx"
MAP = ROOT / "Docs/superpowers/specs/2026-10-16-content-batches/B-rose/source-map.md"
EVENT = "rose.1011.s1"
PRESENTATION = EVENT + ".conversation"
BLOCKED_BY = "B-B03,B-B04,B-B05,B-C02,B-C03,B-C04,B-C07"

HEADERS = {
    "Event": "eventId eventKind actorId targetId titleText contentKey setupType setupKey onceOnly logTag presentationClass presentationKey presentationLogTag eventProperty",
    "PresentationIndex": "presentationKey presentationClass logTag",
    "SetupParams": "setupKey tplName slotValues skipAble autoAdvance checkId",
    "ConversationPage": "pageId presentationKey seq pageCondKeys pageBarConds optionGroupKey bgOverride bgmOverride stepType readingKey checkId",
    "ConversationLine": "lineId pageId seq speakerKey side textKey voiceKey emote sfxKey accentFxKey accentTargetKey speakerNameKey stageLeftKey stageRightKey stageCenterKey",
    "Choice": "choiceId groupKey textKey resultOwnerId seq checkId",
    "AbilityCheck": "checkId successEventId failureEventId successResultOwnerId failureResultOwnerId comment mode",
    "AbilityCheckCondition": "conditionId checkId attributeId threshold groupIndex seq",
    "Object": "objId objType nameText iconPath portraitPath portraitIndoorPath avtarPath descText tags",
    "Text": "key text comment",
    "ReadingPage": "pageId presentationKey seq titleKey imageKey pageCondKeys pageBarConds",
    "ReadingLine": "lineId pageId seq mode speakerKey textKey voiceKey",
}

PAGES = [
    "arrival", "sparrow_perception", "drink_choice", "drink_black", "drink_rich", "drink_milk",
    "news_choice", "news_known", "news_unknown", "sylvain_arrives", "brew", "brew_low",
    "nature_choice", "nature_ordinary", "early_end", "nature_not_person", "reason_title",
    "history_yes", "history_no", "history_lecture", "portrait_memory", "history_return",
    "card_notice", "card_read", "history_recognition", "full_end",
]
CHOICES = {
    "drink_choice": [("black", 20), ("rich", 22), ("milk", 24)],
    "news_choice": [("known", 32), ("unknown", 33)],
    "nature_choice": [("bad", 60), ("ordinary", 61), ("not_person", 62)],
    "nature_not_person": [("insult", 79), ("alias", 80), ("nonsense", 81)],
}

# These are diagnostics, not invented executable dependencies.
BLOCKERS = {
    "B-B03": "Q4②智慧阈值/失败去向未确认；reason_title remains isolated.",
    "B-B04": "P046 {drinkname} must come from a real order bridge, not a default name.",
    "B-B05": "优秀与现有成品评价映射未确认; brew is not formally runnable.",
    "B-C02": "production pause/return/cancel/session contract is not verified.",
    "B-C03": "1011 is an early preparation hook; entry ownership is not verified.",
    "B-C04": "generic card reading and cancel-vs-complete semantics are unavailable.",
    "B-C07": "ConversationLine.side excludes center; use stageCenterKey and side=none.",
}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(ok: bool, message: str):
    if not ok:
        raise RuntimeError(message)


def source_rows():
    rows = []
    active = False
    page = None
    for line in MAP.read_text(encoding="utf-8").splitlines():
        if line.startswith("## rose.1011.s1"):
            active = True
        elif active and line.startswith("## rose."):
            break
        if not active:
            continue
        if line.startswith("| B-1011-P"):
            cells = [x.strip() for x in line.strip("|").split("|")]
            require(len(cells) == 6, "Malformed B-1011 source-map row")
            number = int(cells[0].split("P")[1])
            proposed = cells[2].strip("`")
            key = cells[4].strip("`")
            original = cells[5].replace("&#9;", "\t").replace("<br>", "\n")
            rows.append({"number": number, "kind": cells[1], "page": proposed.removeprefix(EVENT + "."),
                         "key": key, "original": original})
    require(len(rows) == 140 and [r["number"] for r in rows] == list(range(1, 141)),
            "B-1011 source-map coverage changed")
    return rows


def expected_text_rows(rows):
    # Match import_rose_text.py cleaning/splitting rules without importing or writing it.
    import re
    prefix = re.compile(r"^(?:\{playername\}|男人|布塔克|老爷|阿黛尔|麻雀|西尔万|埃莉诺|萨德)\s*[：:]\s*")
    emote = re.compile(r"[（(]\s*#.*?[）)]")
    mixed = {52, 85, 87, 110, 116, 117, 122, 125}
    entries = []
    for row in rows:
        if row["key"] == "—" or row["kind"] not in {"正文", "选项", "名片正文"}:
            continue
        raw = row["original"].strip()
        if row["kind"] == "选项":
            raw = re.sub(r"^[①②③④]\s*", "", raw)
        raw = prefix.sub("", raw)
        raw = emote.sub("", raw).strip()
        parts = [x for x in re.split(r"(「[^」]*」)", raw) if x.strip()] if row["number"] in mixed else [raw]
        for i, part in enumerate(parts):
            value = prefix.sub("", emote.sub("", part.strip())).strip()
            suffix = "." + chr(ord("a") + i) if row["number"] in mixed else ""
            entries.append({"key": row["key"] + suffix, "text": value, "number": row["number"],
                            "page": row["page"], "kind": row["kind"]})
    require(len({x["key"] for x in entries}) == len(entries), "Duplicate staged Text key in source-map")
    return entries


def add(rows, sheet, **values):
    rows.append({"sheet": sheet, "values": values})


def build():
    source = source_rows()
    text_entries = expected_text_rows(source)
    rows = []
    add(rows, "Event", eventId=EVENT, eventKind="actor", actorId="player", setupKey=EVENT,
        onceOnly=True, logTag="B1011", presentationClass="Conversation", presentationKey=PRESENTATION,
        presentationLogTag="B1011", eventProperty=f"entry=MorningBeforeMap;blocksSave=true;blockedBy={BLOCKED_BY};contentBoundary=P003-P140")
    add(rows, "PresentationIndex", presentationKey=PRESENTATION, presentationClass="Conversation", logTag="B1011")
    add(rows, "SetupParams", setupKey=EVENT, tplName="Tpl_ArrivalConversation", slotValues="opts=", skipAble=False, autoAdvance=False)

    names = {"adele": "character.adele.name", "sparrow": "character.sparrow.name", "sylvain": "character.sylvain.name"}
    for obj, name in names.items():
        add(rows, "Object", objId=obj, objType="npc", nameText=name)

    checks = [("sparrow", "perception", 6), ("portrait", "perception", 5)]
    for suffix, attr, threshold in checks:
        check = f"{EVENT}.check.{suffix}"
        add(rows, "AbilityCheck", checkId=check, comment="B1011 cached Inline snapshot; blocked root; no result owner", mode="Inline")
        add(rows, "AbilityCheckCondition", conditionId=check + ".1", checkId=check, attributeId=attr,
            threshold=threshold, groupIndex=1, seq=1)

    def guard(short):
        if short == "sparrow_perception": return ""
        if short == "portrait_memory": return f"check:{EVENT}.check.portrait"
        if short == "drink_black": return f"choice:{EVENT}.choice.p020"
        if short == "drink_rich": return f"choice:{EVENT}.choice.p022"
        if short == "drink_milk": return f"choice:{EVENT}.choice.p024"
        if short == "news_known": return f"choice:{EVENT}.choice.p032"
        if short == "news_unknown": return f"choice:{EVENT}.choice.p033"
        if short == "nature_ordinary": return f"anyChoice:{EVENT}.choice.p060;{EVENT}.choice.p061"
        if short == "early_end": return f"anyChoice:{EVENT}.choice.p060;{EVENT}.choice.p061;{EVENT}.choice.p079;{EVENT}.choice.p081"
        if short == "nature_not_person": return f"choice:{EVENT}.choice.p062"
        if short == "reason_title": return f"choice:{EVENT}.choice.p080|dependency:B-B03.wisdom"
        if short == "history_yes": return "dependency:B-C01.specialization.politics.true"
        if short == "history_no": return "dependency:B-C01.specialization.politics.false"
        if short == "history_recognition": return "dependency:B-C01.specialization.politics.true"
        if short == "brew": return "dependency:B-C02.production.1011"
        if short == "brew_low": return "dependency:B-C02.production.1011.low"
        return ""

    for seq, short in enumerate(PAGES, 1):
        step = "PassiveCheck" if short in {"sparrow_perception", "portrait_memory"} else "Dialogue"
        if short == "card_read":
            continue
        option = {"drink_choice": "drink", "news_choice": "news", "nature_choice": "nature", "nature_not_person": "reason"}.get(short)
        bg = "oct07.cafe.day" if short not in {"history_lecture", "card_notice", "card_read"} else "rose.1011.history.portrait" if short == "history_lecture" else "oct07.cafe.day"
        add(rows, "ConversationPage", pageId=f"{EVENT}.{short}", presentationKey=PRESENTATION, seq=seq,
            pageCondKeys=guard(short), optionGroupKey=f"{EVENT}.choice.{option}" if option else None,
            bgOverride=bg, stepType=step, checkId=f"{EVENT}.check.{('sparrow' if short == 'sparrow_perception' else 'portrait' if short == 'portrait_memory' else '')}" if step == "PassiveCheck" else None)

    page_lines = Counter()
    for entry in text_entries:
        short = entry["page"]
        if short not in PAGES or short == "card_read":
            continue
        page_lines[short] += 1
        raw = next(x["original"] for x in source if x["number"] == entry["number"])
        speaker = "player" if raw.startswith("{playername}") else "adele" if raw.startswith("阿黛尔") else "sparrow" if raw.startswith("麻雀") else "sylvain" if raw.startswith("西尔万") else None
        emote = None
        import re
        match = re.search(r"[（(]#([^）)]+)[）)]", raw)
        if match: emote = match.group(1)
        # Current schema permits only left/right/none. Three-slot staging uses explicit snapshots.
        stage_left = "adele" if short in {"arrival", "news_choice", "news_known", "news_unknown", "sylvain_arrives", "nature_choice", "nature_ordinary", "nature_not_person", "reason_title", "history_lecture", "history_return", "full_end"} else None
        stage_right = "sparrow" if stage_left else None
        stage_center = "sylvain" if short in {"sylvain_arrives", "nature_choice", "nature_ordinary", "nature_not_person", "reason_title", "history_lecture", "history_return", "card_notice", "full_end"} else None
        add(rows, "ConversationLine", lineId=entry["key"].replace(".text.", "."), pageId=f"{EVENT}.{short}",
            seq=page_lines[short], speakerKey=speaker, side="none", textKey=entry["key"], emote=emote,
            stageLeftKey=stage_left, stageRightKey=stage_right, stageCenterKey=stage_center,
            speakerNameKey={"adele": "character.adele.name", "sparrow": "character.sparrow.name", "sylvain": "character.sylvain.name"}.get(speaker))

    choice_groups = [("drink", "drink_choice"), ("news", "news_choice"), ("nature", "nature_choice"), ("reason", "nature_not_person")]
    source_by_num = {x["number"]: x for x in source}
    for group, page in choice_groups:
        for label, number in CHOICES[page]:
            add(rows, "Choice", choiceId=f"{EVENT}.choice.p{number:03}", groupKey=f"{EVENT}.choice.{group}",
                textKey=source_by_num[number]["key"], seq=1 + [x[0] for x in CHOICES[page]].index(label))

    # B-C04 is unresolved: do not emit a fake generic Reading step or presentation ref.
    # Deliver the independent pre-order fragment first. P046 needs an unknown
    # real order name and P048 needs a real brewing handoff. Neither may leak
    # into this preview. Remaining draft rows are intentionally not emitted.
    allowed = {EVENT + '.' + name for name in PAGES[:10]}
    kept = []
    for item in rows:
        sheet, values = item['sheet'], item['values']
        if sheet == 'ConversationPage' and values['pageId'] not in allowed: continue
        if sheet == 'ConversationLine':
            if any(x['number'] == int(values['lineId'].split('.p')[-1].split('.')[0]) and x['kind'] == '选项' for x in text_entries): continue
            if values['pageId'] not in allowed: continue
            number = int(values['lineId'].split('.p')[-1].split('.')[0])
            if number > 45: continue
            # Stage resources are unverified: keep text with explicit speaker names.
            for field in ('stageLeftKey', 'stageRightKey', 'stageCenterKey'): values[field] = None
        if sheet == 'Choice' and values['groupKey'] not in (EVENT+'.choice.drink', EVENT+'.choice.news'): continue
        if sheet in ('AbilityCheck','AbilityCheckCondition') and '.portrait' in str(values): continue
        if sheet == 'Event':
            values['eventProperty'] = 'entry=Immediate;blocksSave=true;blockedBy=B-B03+B-B04+B-B05+B-C02+B-C04;contentBoundary=P045'
        kept.append(item)
    return kept, {"source_rows": len(source), "text_entries": len(text_entries),
                   "boundary": "P045 before unresolved drinkname P046 and real brewing P048",
                   "card_reading": "not emitted; generic Reading remains blocked"}


def headers(ws):
    return [c.value for c in ws[1]][1:]


def apply_rows(rows, books):
    locations, added = [], Counter()
    indexes = {}
    for item in rows:
        sheet, values = item["sheet"], item["values"]
        path = TEXT if sheet == "Text" else NARRATIVE
        require(sheet in HEADERS, f"Unsupported sheet in plan: {sheet}")
        ws = books[path][sheet]
        actual = headers(ws)
        expected = HEADERS[sheet].split()
        require(actual == expected, f"Schema drift: {sheet}: {actual} != {expected}")
        key_field = expected[0]
        require(key_field in values, f"Missing key field: {sheet}")
        index = indexes.setdefault(sheet, {r[1].value: r[0].row for r in ws.iter_rows(min_row=5) if r[1].value})
        key = values[key_field]
        col = {name: i + 2 for i, name in enumerate(expected)}
        if key in index:
            row = index[key]
            for field, value in values.items():
                if value is not None:
                    require(ws.cell(row, col[field]).value == value, f"Existing row differs: {sheet}/{key}/{field}")
        else:
            row = ws.max_row + 1
            for c in range(1, ws.max_column + 1):
                ws.cell(row, c)._style = copy(ws.cell(max(5, row - 1), c)._style)
            for field, value in values.items():
                if value is not None:
                    ws.cell(row, col[field], value)
            index[key] = row
            added[sheet] += 1
        locations.append({"sheet": sheet, "key": key, "row": row})
    return added, locations


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="CAS append blocked Narrative rows; never generate or run Unity")
    args = parser.parse_args()
    rows, coverage = build()
    text_wb = openpyxl.load_workbook(TEXT, read_only=True)
    text_keys = {r[1].value for r in text_wb["Text"].iter_rows(min_row=5) if r[1].value}
    text_wb.close()
    needed = {item["values"].get("textKey") for item in rows if item["sheet"] in {"ConversationLine", "Choice", "ReadingLine"}} - {None}
    missing = sorted(needed - text_keys)
    counts = Counter(item["sheet"] for item in rows)
    manifest = {"event": EVENT, "presentation": PRESENTATION, "coverage": coverage, "source_map_sha256": digest(MAP),
                "would_append": dict(counts), "writes": False, "blocked": BLOCKERS,
                "missing_text": missing,                 "center_side": "text-only fragment; stage resource binding remains unverified",
                "results": [], "event_dates": [], "actions": [],
                "route_contract": "Pre-order fragment only: two option groups, five original choices; stops at P045",
                "route_blocker": "Post-brew Q3/Q4, shared early_end, wisdom and generic card are not emitted by this increment.",
                "thresholds": {"sparrow": 6}, "specialization_snapshot": "not consumed by the pre-order fragment"}
    require(not missing, f"Missing staged Text keys ({len(missing)}); run import_rose_text.py --event 1011 first")
    if not args.apply:
        print(json.dumps(manifest, ensure_ascii=False, indent=2))
        return
    from rose_write_guard import source_write_guard
    paths = (NARRATIVE, TEXT)
    initial = {p: digest(p) for p in paths}
    with source_write_guard("configure rose.1011"):
        require(all(digest(p) == initial[p] for p in paths), "Concurrent workbook change before apply")
        books = {p: openpyxl.load_workbook(p) for p in paths}
        try:
            # No Text items are emitted by this tool; Text is an authoritative staged dependency.
            added, locations = apply_rows(rows, books)
            if not added:
                print(json.dumps({"idempotent": True, "changed": False, **manifest}, ensure_ascii=False, indent=2))
                return
            out = ROOT / "Library/Locus/tmp/rose-content" / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-1011-config")
            out.mkdir(parents=True, exist_ok=False)
            candidates = {}
            for path, wb in books.items():
                require(digest(path) == initial[path], f"Concurrent change before backup: {path}")
                shutil.copy2(path, out / (path.stem + ".before.xlsx"))
                require(digest(out / (path.stem + ".before.xlsx")) == initial[path], f"Backup race: {path}")
                candidate = out / (path.stem + ".candidate.xlsx")
                wb.save(candidate)
                candidates[path] = candidate
            for path in paths:
                require(digest(path) == initial[path], f"Concurrent change before replace: {path}")
            candidates[NARRATIVE].replace(NARRATIVE)
            manifest.update(writes=True, added=dict(added), locations=locations, evidence=str(out),
                            before_sha256={str(p): h for p, h in initial.items()}, replaced=[str(NARRATIVE)])
            (out / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
            print(json.dumps(manifest, ensure_ascii=False, indent=2))
        finally:
            for wb in books.values():
                wb.close()


if __name__ == "__main__":
    main()
