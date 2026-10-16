"""Narrow B-rose stage repair; default is a read-only plan, never generation.

Only ConversationLine values on the explicit 1011/1016 whitelist may change.
P110.a/b/c remain offstage during the portrait sequence (P103-P114). No Text,
display text, guard, Result or formal root is authored. The five corrected
narrators also lose their stale speakerNameKey; 1016 P004 clears all slots. --self-test uses only memory.
Run with python -B. --apply requires the Narrative SHA from a fresh plan and
uses rose_write_guard's shared lock, CAS, backup and verified candidate readback.
"""
from __future__ import annotations

import argparse
from collections import Counter
from copy import copy, deepcopy
from datetime import datetime
from io import BytesIO
import json
import re
import shutil
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile

import openpyxl
from openpyxl.comments import Comment
from openpyxl.styles import Font, PatternFill
from openpyxl.drawing.spreadsheet_drawing import SpreadsheetDrawing
from openpyxl.worksheet._writer import WorksheetWriter
from openpyxl.writer.excel import ExcelWriter
from openpyxl.xml.functions import tostring

import import_rose_text as source
from rose_write_guard import source_write_guard

ROOT = source.ROOT
NARRATIVE = ROOT / "Assets/ConfigTables/Data/Narrative/Narrative.xlsx"
SPEC = source.MAP.with_name("spec.md")
SCHEMA = ROOT / "Assets/ConfigTables/Defines/schema.xml"
# Reviewed evidence is deliberately pinned. Changed evidence requires review,
# not an automatically broadened whitelist.
EVIDENCE = {
    source.MAP: "94c70d2e272b20fbd4f83d08a5aaa50292ea203bb22bd3b2c7d2807006360c72",
    SPEC: "12d2f98cc4869d4f6e10649d36ca72dc3463a8a52529af2a4035c711ec72925d",
}
HEADERS = ("##var lineId pageId seq speakerKey side textKey voiceKey emote sfxKey "
           "accentFxKey accentTargetKey speakerNameKey stageLeftKey stageRightKey stageCenterKey").split()
SLOTS = ("stageLeftKey", "stageRightKey", "stageCenterKey")
SIDES = {"adele": "left", "sparrow": "right", "sylvain": "center"}
NARRATORS = {"p015": "sparrow", "p042": "adele", "p057": "adele",
             "p120": "adele", "p138": "sparrow"}
OFFSTAGE = {"p110.a", "p110.b", "p110.c"}
# Exact legacy line set, page order, speakers and emotes, reviewed against the
# two 1011 builders and source-map. '-' means None, never an empty-string alias.
# These are validation fixtures, not new text or a replacement script format.
GROUPS_1011 = (
    ("arrival", "p003/adele p004/sparrow p005 p006 p007 p008 p009/player p010/adele/开心 p011 p012/player p013/sparrow/尴尬"),
    ("sparrow_perception", "p015/sparrow p016 p017"),
    ("drink_choice", "p018"),
    ("drink_black", "p021/sparrow/尴尬"),
    ("drink_rich", "p023/sparrow/思索"),
    ("drink_milk", "p025/sparrow/开心"),
    ("news_choice", "p027/sparrow/平静 p028/player p029/adele/焦虑 p030/sparrow"),
    ("news_known", "p035/sparrow/悲伤"),
    ("news_unknown", "p037/sparrow/悲伤"),
    ("sylvain_arrives", "p038/adele/生气 p039/sparrow/生气 p040 p042/adele p043/sylvain/开心 p044 p045/player"),
    ("brew_low", "p050/sylvain/不屑"),
    ("nature_choice", "p052.a/sylvain/思索 p052.b p052.c/sylvain/思索 p052.d p052.e/sylvain/思索 p052.f p052.g/sylvain/思索 p053/sparrow/迷惑 p054/sylvain p055/adele/思索 p056/sylvain p057/adele p058/sylvain"),
    ("nature_ordinary", "p064 p065/sparrow/生气 p066/adele/生气"),
    ("early_end", "p068/sylvain/开心 p069 p070 p071 p072/player p073"),
    ("nature_not_person", "p075/sparrow/兴奋 p076/adele/生气 p077/sylvain/思索"),
    ("reason_title", "p085.a/player p085.b/player p085.c/player p086 p087.a/sylvain/开心 p087.b/sylvain/开心 p088/adele/迷惑 p089/sylvain p090 p091/sylvain/平静 p092/adele/迷惑 p093/sparrow/思索"),
    ("history_yes", "p095/player p096/sylvain p097/player p098/sylvain"),
    ("history_no", "p100/sylvain"),
    ("history_lecture", "p103 p104 p105 p106 p107 p108 p109 p110.a/sylvain/平静 p110.b/sylvain/平静 p110.c/sylvain/平静"),
    ("portrait_memory", "p112 p113 p114"),
    ("history_return", "p116.a/sparrow p116.b/sparrow/兴奋 p117.a/sylvain p117.b/sylvain p117.c/sylvain p117.d/sylvain p118/adele/迷惑 p119 p120/adele p121 p122.a/sylvain/思索 p122.b/sylvain/思索 p123/sparrow p124/player p125.a/sylvain/开心 p125.b/sylvain/开心"),
    ("card_notice", "p126"),
    ("history_recognition", "p132 p133 p134"),
    ("full_end", "p136/player p137/adele p138/sparrow p139 p140"),
)
GROUPS_1016 = (
    ("arrival", "p004/player p005 p008/player p009/alienor"),
    ("wisdom", "p011 p012"),
    ("order", "p013/player p014/alienor p015/sade"),
)


def require(ok, message):
    if not ok:
        raise RuntimeError(message)


def baseline():
    """Explicit old values for every field of the two owned line sets."""
    rows = {}
    for event, groups in (("1011", GROUPS_1011), ("1016", GROUPS_1016)):
        root = f"rose.{event}.s1"
        for page, tokens in groups:
            for seq, token in enumerate(tokens.split(), 1):
                parts = token.split("/")
                short = parts[0]
                speaker = parts[1] if len(parts) > 1 else None
                values = dict.fromkeys(HEADERS)
                values.update(lineId=root + "." + short, pageId=root + "." + page,
                              seq=seq, speakerKey=speaker, side="none",
                              textKey=root + ".text." + short,
                              emote=parts[2] if len(parts) > 2 else None)
                if event == "1011" and int(short[1:4]) <= 45 and speaker in SIDES:
                    values["speakerNameKey"] = f"character.{speaker}.name"
                if event == "1016":
                    if speaker == "alienor":
                        values.update(side="left", speakerNameKey=root + ".alias.alienor")
                    if speaker == "sade":
                        values["side"] = "right"
                require(values["lineId"] not in rows, "Duplicate fixture")
                rows[values["lineId"]] = values
    require(len(rows) == 121, "Fixture coverage changed")
    return rows


def whitelist(old_rows):
    """(lineId, field) -> (exact old value, target, category)."""
    edits = {}

    def add(short, field, target, category, event="1011"):
        key = f"rose.{event}.s1.{short}"
        require(key in old_rows and field in HEADERS, "Unknown whitelist row/field")
        require((key, field) not in edits, "Overlapping whitelist")
        edits[key, field] = (old_rows[key][field], target, category)

    for key, row in old_rows.items():
        short = key.removeprefix("rose.1011.s1.")
        if key.startswith("rose.1011.s1.") and row["speakerKey"] in SIDES:
            if short not in NARRATORS and short not in OFFSTAGE:
                add(short, "side", SIDES[row["speakerKey"]], "speaker_side")
    for short, speaker in NARRATORS.items():
        require(old_rows["rose.1011.s1." + short]["speakerKey"] == speaker, "Narrator fixture drift")
        add(short, "speakerKey", None, "narrator_speaker")
        add(short, "emote", None, "narrator_emote")
        add(short, "speakerNameKey", None, "narrator_name")
    for short, slots, category, event in (
        ("p003", ("adele", "sparrow", "-"), "opening_slots", "1011"),
        ("p043", (None, None, "sylvain"), "sylvain_arrival", "1011"),
        ("p069", (None, None, "-"), "early_exit", "1011"),
        ("p126", (None, None, "-"), "long_exit", "1011"),
        ("p050", ("adele", "sparrow", "sylvain"), "postbrew_entry_slots", "1011"),
        ("p052.a", ("adele", "sparrow", "sylvain"), "postbrew_entry_slots", "1011"),
        ("p103", ("-", "-", "-"), "portrait_clear", "1011"),
        ("p116.a", ("adele", "sparrow", "sylvain"), "portrait_return", "1011"),
        ("p004", ("-", "-", "-"), "visitors_opening_clear", "1016"),
        ("p008", ("alienor", "sade", "-"), "visitors_slots", "1016"),
    ):
        for field, target in zip(SLOTS, slots):
            if target is not None:
                add(short, field, target, category, event)
    require(len(edits) == 93, "Whitelist coverage changed")
    return edits


def evidence_texts(old_rows):
    """Read pinned evidence; no DOCX imports, writes or inferred new targets."""
    for path, expected in EVIDENCE.items():
        require(source.digest(path) == expected, f"Reviewed evidence changed: {path}")
    texts = {}
    for event in ("1011", "1016"):
        mapped = {}
        for number, kind, page, key, original in source.source_rows(event):
            if kind != "正文" or (event == "1011" and 46 <= number < 50):
                continue
            if key == "—":
                continue
            raw = source.clean(original)
            parts = source.split_text(event, number, raw) if number in source.MIXED[event] else [raw]
            for i, text in enumerate(parts):
                suffix = "." + chr(97 + i) if number in source.MIXED[event] else ""
                text_key = key + suffix
                line_key = text_key.replace(".text.", ".")
                require(line_key in old_rows, f"Unknown source row: {line_key}")
                require(old_rows[line_key]["pageId"] == page, f"Source page changed: {line_key}")
                mapped[line_key] = text_key
                texts[text_key] = text
        expected = {k for k in old_rows if k.startswith(f"rose.{event}.s1.")}
        require(set(mapped) == expected, f"Source coverage changed: {event}")
    return texts


def prepare(wb):
    """Validate everything before the first assignment; only set whitelist values."""
    old_rows = baseline()
    allowed = whitelist(old_rows)
    ws = wb["ConversationLine"]
    require([c.value for c in ws[1]] == HEADERS, "ConversationLine schema drift")
    locations = {}
    for row in ws.iter_rows(min_row=5):
        key = row[1].value
        if not isinstance(key, str) or not key.startswith(("rose.1011.s1.", "rose.1016.s1.")):
            continue
        require(key in old_rows, f"Unknown owned row: {key}")
        require(key not in locations, f"Duplicate owned row: {key}")
        locations[key] = row[0].row
    require(locations.keys() == old_rows.keys(), "Missing owned row(s)")
    for key, row in old_rows.items():
        for field, old in row.items():
            value = ws.cell(locations[key], HEADERS.index(field) + 1).value
            permitted = (old, allowed[key, field][1]) if (key, field) in allowed else (old,)
            require(value in permitted and type(value) in tuple(type(v) for v in permitted),
                    f"Unexpected value: {key}/{field}: {value!r}; expected {permitted!r}")
    changes, targets = [], []
    for (key, field), (_, target, category) in allowed.items():
        cell = ws.cell(locations[key], HEADERS.index(field) + 1)
        item = dict(sheet=ws.title, cell=cell.coordinate, lineId=key, field=field,
                    before=cell.value, after=target, category=category)
        targets.append(item)
        if cell.value != target:
            changes.append(item)
    for item in changes:
        # ws.cell(..., value=None) is a getter, NOT a clearing operation.
        ws[item["cell"]].value = item["after"]
    return targets, changes


def cell_signature(cell):
    # source.signature covers value/type/style/comment text. Also keep authors,
    # comment geometry and hyperlinks, including those on changed cells.
    comment = cell.comment
    value, data_type, style, comment_text = source.signature(cell)
    # Absent cells load with _style=None; populated s=0 cells load with the
    # all-zero StyleArray. Both mean exactly the same unauthored default style.
    # Clearing/setting a default-styled value can swap those representations;
    # compare effective metadata, never assign or erase any authored style.
    if not cell.has_style:
        style = None
    return (value, data_type, style, comment_text,
            (comment.author, comment.width, comment.height) if comment else None,
            copy(cell.hyperlink))


def snapshot(wb):
    return {(ws.title, c.coordinate): cell_signature(c) for ws in wb for row in ws for c in row}


def metadata(wb):
    def dimensions(items):
        return [(k, tuple(sorted(dict(v).items())), tuple(v._style) if v.has_style else None)
                for k, v in items.items()]
    return deepcopy((wb.properties, tostring(wb.custom_doc_props.to_tree()), wb.defined_names,
                     wb.calculation, wb.security, wb.views,
                     [(ws.title, ws.max_row, ws.max_column, ws.sheet_state, ws.sheet_properties,
                       ws.sheet_format, ws.views, ws.freeze_panes, str(ws.merged_cells),
                       dimensions(ws.row_dimensions), dimensions(ws.column_dimensions),
                       ws.auto_filter, ws.data_validations, ws.print_options, ws.page_margins,
                       ws.page_setup, ws.protection) for ws in wb]))


class MemorySheetExcelWriter(ExcelWriter):
    """Avoid openpyxl's hidden per-sheet NamedTemporaryFile, even for BytesIO."""

    def write_worksheet(self, ws):
        require(not self.workbook.write_only, "Write-only workbooks unsupported")
        ws._drawing = SpreadsheetDrawing()
        ws._drawing.charts = ws._charts
        ws._drawing.images = ws._images
        writer = WorksheetWriter(ws, out=BytesIO())
        writer.write()
        ws._rels = writer._rels
        self._archive.writestr(ws.path[1:], writer.read())
        self.manifest.append(ws)


def save_preserving_properties(wb, destination):
    # Workbook.save() changes properties.modified in openpyxl 3.1.5.
    with ZipFile(destination, "w", ZIP_DEFLATED, allowZip64=True) as archive:
        MemorySheetExcelWriter(wb, archive).save()


def verify_candidate(saved, before, old_metadata, expected, changes):
    require(metadata(saved) == old_metadata, "Workbook/sheet metadata changed")
    actual = snapshot(saved)
    require(actual.keys() == before.keys(), "Cell coordinate set changed")
    allowed = {(x["sheet"], x["cell"]) for x in changes}
    for key, old in before.items():
        require(actual[key] == expected[key], f"Candidate readback mismatch: {key}")
        if key in allowed:
            require(actual[key][2:] == old[2:], f"Target metadata changed: {key}")
        else:
            require(actual[key] == old, f"Non-whitelist cell changed: {key}")


def validate_schema(xml):
    bean = ET.fromstring(xml).find(".//bean[@name='NcsConversationLineEntry']")
    require(bean is not None, "ConversationLine schema missing")
    fields = {v.get("name"): v.get("type") for v in bean.findall("var")}
    require(fields.get("side") == "string#set=left,right,center,none",
            "ConversationLine.side must support left,right,center,none before migration")
    require(all(fields.get(slot) == "string" for slot in SLOTS), "Stage slot schema missing")


def compare_hashes(expected, observed):
    require(expected == observed, "Concurrent source/evidence change; CAS refused")


def repair(apply=False, expected_sha256=None):
    require(not apply or expected_sha256 is not None, "--apply requires --expected-sha256 from a fresh plan")
    paths = (NARRATIVE, source.TEXT, SCHEMA, *EVIDENCE)
    hashes = {p: source.digest(p) for p in paths}
    validate_schema(SCHEMA.read_text(encoding="utf-8"))
    if expected_sha256 is not None:
        require(hashes[NARRATIVE] == expected_sha256.lower(), "Narrative SHA changed; re-plan")
    texts = evidence_texts(baseline())
    tw = openpyxl.load_workbook(source.TEXT, read_only=True)
    try:
        staged = {}
        for row in tw["Text"].iter_rows(min_row=5, values_only=True):
            if row[1] in texts:
                require(row[1] not in staged, f"Duplicate Text key: {row[1]}")
                staged[row[1]] = row[2]
        require(staged == texts, "Source Text mismatch; refusing stage migration")
    finally:
        tw.close()
    wb = openpyxl.load_workbook(NARRATIVE)
    try:
        before, old_metadata = snapshot(wb), metadata(wb)
        targets, changes = prepare(wb)
        expected = snapshot(wb)
        report = dict(tool="repair_rose_stage", writes=bool(apply and changes), changed=False,
                      formal_enabled=False, before_sha256=hashes[NARRATIVE],
                      idempotent=not changes, changed_cells=len(changes),
                      changed_rows=len({x["lineId"] for x in changes}),
                      changes_by_category=dict(Counter(x["category"] for x in changes)),
                      whitelist_by_category=dict(Counter(x["category"] for x in targets)),
                      cells_checked=len(before), targets=targets, changes=changes,
                      portrait_offstage=sorted(OFFSTAGE),
                      evidence_sha256={str(p): h for p, h in hashes.items() if p != NARRATIVE})

        def cas():
            compare_hashes(hashes, {p: source.digest(p) for p in paths})

        cas()
        if not apply or not changes:
            return report
        out = ROOT / "Library/Locus/tmp/rose-content" / (
            datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-stage-repair")
        out.mkdir(parents=True, exist_ok=False)
        backup = out / "Narrative.before.xlsx"
        candidate = out / "Narrative.candidate.xlsx"
        shutil.copy2(NARRATIVE, backup)
        require(source.digest(backup) == hashes[NARRATIVE], "Backup SHA mismatch; not applied")
        save_preserving_properties(wb, candidate)
        saved = openpyxl.load_workbook(candidate)
        try:
            verify_candidate(saved, before, old_metadata, expected, changes)
            require(not prepare(saved)[1], "Candidate is not idempotent")
        finally:
            saved.close()
        candidate_sha = source.digest(candidate)
        report.update(status="verified-candidate", evidence=str(out), metadata_preserved=True,
                      backup_sha256=source.digest(backup), candidate_sha256=candidate_sha)
        journal = out / "repair-evidence.json"
        journal.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        replacement = out / "Narrative.replace.xlsx"
        shutil.copy2(candidate, replacement)
        require(source.digest(replacement) == candidate_sha, "Replacement SHA mismatch")
        require(source.digest(candidate) == candidate_sha and source.digest(backup) == hashes[NARRATIVE],
                "Evidence workbook changed")
        cas()
        replacement.replace(NARRATIVE)
        require(source.digest(NARRATIVE) == candidate_sha, "Post-replacement SHA mismatch")
        compare_hashes({p: h for p, h in hashes.items() if p != NARRATIVE},
                       {p: source.digest(p) for p in paths if p != NARRATIVE})
        report.update(changed=True, status="committed", after_sha256=candidate_sha)
        journal.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        return report
    finally:
        wb.close()


def fixture():
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "ConversationLine"
    ws.append(HEADERS)
    for _ in range(3):
        ws.append([None] * len(HEADERS))
    for values in baseline().values():
        ws.append([values[h] for h in HEADERS])
    ws.freeze_panes = "C5"
    ws.column_dimensions["B"].width = 32
    # Blank and populated target cells both carry metadata that must survive.
    for row in ws.iter_rows(min_row=5):
        for cell in row:
            cell.font = Font(name="Arial", size=10)
            cell.fill = PatternFill("solid", fgColor="F0F0F0")
            cell.number_format = "@"
            cell.comment = Comment("retain old metadata", "fixture owner")
    # Include absent/default-style cells as well as richly styled targets.
    ws.cell(5, HEADERS.index("stageLeftKey") + 1)._style = None
    ws.cell(5, HEADERS.index("stageLeftKey") + 1).comment = None
    ws.cell(16, HEADERS.index("speakerKey") + 1)._style = None
    other = wb.create_sheet("Untouched")
    other["A1"] = "guard/result/root sentinel"
    other["A1"].hyperlink = "https://example.invalid/keep"
    other["B2"] = "=1+2"
    # Tests model a loaded workbook, whose authored metadata has already been
    # serialized (timestamps, column min/max and frozen-pane selection defaults).
    buffer = BytesIO()
    save_preserving_properties(wb, buffer)
    wb.close()
    buffer.seek(0)
    return openpyxl.load_workbook(buffer)


def self_test():
    """No path reads, writes, temporary files, locks or source workbook changes."""
    passed = []
    old = baseline()
    allowed = whitelist(old)
    wb = fixture()
    before, meta = snapshot(wb), metadata(wb)
    targets, changes = prepare(wb)
    require(len(changes) == 85, "Unexpected old-state change count")
    expected = snapshot(wb)
    require(not prepare(wb)[1] and snapshot(wb) == expected, "Second prepare was not a no-op")
    passed.extend(("exact-whitelist-85-changes", "idempotent-memory-no-op"))
    buffer = BytesIO()
    save_preserving_properties(wb, buffer)
    buffer.seek(0)
    saved = openpyxl.load_workbook(buffer)
    verify_candidate(saved, before, meta, expected, changes)
    require(not prepare(saved)[1], "Roundtrip not idempotent")
    passed.append("in-memory-candidate-roundtrip-all-cell-metadata")
    ws = wb["ConversationLine"]
    loc = {row[1].value: row[0].row for row in ws.iter_rows(min_row=5)}

    def cell(key, field):
        return ws.cell(loc[key], HEADERS.index(field) + 1)

    def rejects(label, operation):
        try:
            operation()
        except RuntimeError:
            return
        raise RuntimeError("Self-test did not reject: " + label)

    # Every single whitelist cell rejects a third, non-approved value. Validation
    # is all-or-nothing, including when an invalid row follows valid pending edits.
    for key, field in allowed:
        c = cell(key, field)
        original = c.value
        c.value = "UNREVIEWED"
        unchanged = snapshot(wb)
        rejects(key + "/" + field, lambda: prepare(wb))
        require(snapshot(wb) == unchanged, "Rejected prepare mutated cells")
        c.value = original
    passed.append("reject-third-value-on-all-93-whitelist-cells")
    for short, field, bad in (("p003", "pageId", "wrong.page"), ("p003", "textKey", "wrong.text"),
                              ("p003", "seq", True), ("p003", "speakerNameKey", "wrong.name"),
                              ("p110.a", "side", "center"), ("p038", "stageCenterKey", "sylvain"),
                              ("p126", "speakerKey", "sylvain"), ("p009", "side", "left")):
        c = cell("rose.1011.s1." + short, field)
        value = c.value
        c.value = bad
        rejects(short + "/" + field, lambda: prepare(wb))
        c.value = value
    passed.append("reject-unlisted-fields-early-arrival-and-portrait-restaging")
    c = cell("rose.1011.s1.p003", "lineId")
    for bad in ("rose.1011.s1.p999", "rose.1011.s1.p004", None):
        c.value = bad
        rejects("unknown-duplicate-missing-row", lambda: prepare(wb))
    c.value = "rose.1011.s1.p003"
    ws.cell(1, len(HEADERS) + 1).value = "unknownField"
    rejects("unknown-field", lambda: prepare(wb))
    del ws._cells[1, len(HEADERS) + 1]
    passed.append("reject-unknown-duplicate-missing-row-and-schema-drift")
    # Clearing explicitly assigns None, even when an approved old speaker exists.
    for short in NARRATORS:
        require(cell("rose.1011.s1." + short, "speakerKey").value is None, "Speaker not cleared")
        require(cell("rose.1011.s1." + short, "emote").value is None, "Emote not cleared")
        require(cell("rose.1011.s1." + short, "speakerNameKey").value is None, "Narrator name not cleared")
        require(cell("rose.1011.s1." + short, "side").value == "none", "Narrator side changed")
    for short in OFFSTAGE:
        require(cell("rose.1011.s1." + short, "side").value == "none", "Portrait restaged")
    require(cell("rose.1016.s1.p008", "speakerKey").value == "player", "1016 speaker changed")
    passed.append("explicit-None-clear-narrators-and-1016-speaker-preserved")
    saved["Untouched"]["A1"].value = "tampered"
    rejects("non-whitelist-candidate", lambda: verify_candidate(saved, before, meta, expected, changes))
    saved["Untouched"]["A1"].value = "guard/result/root sentinel"
    target = changes[0]
    saved[target["sheet"]][target["cell"]].comment = Comment("retain old metadata", "wrong author")
    rejects("target-comment-author", lambda: verify_candidate(saved, before, meta, expected, changes))
    passed.append("reject-nontarget-value-and-target-metadata-tampering")
    compare_hashes({"source": "a"}, {"source": "a"})
    rejects("CAS", lambda: compare_hashes({"source": "a"}, {"source": "b"}))
    passed.append("CAS-reject-concurrent-source")
    xml = '<schema><bean name="NcsConversationLineEntry"><var name="side" type="string#set=left,right,center,none"/>'
    xml += ''.join(f'<var name="{slot}" type="string"/>' for slot in SLOTS) + '</bean></schema>'
    validate_schema(xml)
    rejects("missing-center-schema", lambda: validate_schema(xml.replace(",center", "")))
    rejects("missing-stage-schema", lambda: validate_schema(xml.replace("stageCenterKey", "missing")))
    passed.append("reject-schema-without-center-or-stage-slots")
    # A partially repaired whitelist is safe: only remaining approved values change.
    c = cell("rose.1011.s1.p003", "side")
    c.value = "none"
    require(len(prepare(wb)[1]) == 1 and not prepare(wb)[1], "Partial approved state not idempotent")
    passed.append("partial-approved-state-idempotent")
    wb.close()
    saved.close()
    return dict(self_test="PASS", files_written=0, tests=passed, tests_passed=len(passed),
                old_state_changed_cells=len(changes),
                changes_by_category=dict(Counter(x["category"] for x in changes)),
                whitelist_by_category=dict(Counter(x["category"] for x in targets)))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--self-test", action="store_true")
    parser.add_argument("--expected-sha256", help="Narrative SHA from the read-only plan")
    args = parser.parse_args()
    if args.self_test:
        require(args.expected_sha256 is None, "--self-test does not use source files/SHA")
        report = self_test()
    elif args.apply:
        require(args.expected_sha256 is not None and re.fullmatch(r"[0-9a-fA-F]{64}", args.expected_sha256),
                "--apply requires a 64-digit --expected-sha256")
        with source_write_guard("repair rose stage whitelist"):
            report = repair(True, args.expected_sha256)
    else:
        report = repair(False, args.expected_sha256)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
