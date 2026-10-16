"""Prepare ONLY blocked church.1023.s1 source content.

Default: read-only dry-run. --apply: cooperative lock, backups, CAS append and
manifest, under parent coordination after 1014/1020. Never runs generation or
Unity. Existing 113 body + 3 choice Text rows are verified, never rewritten.
Only the two neutral NPC identity Text/Object rows may be appended.

The root is disabled by blockedBy. Condition has no dependency enum, so hearing
is dependency gated BEFORE exposing its choices. No unknown Condition target,
zero bribe, automatic success, settlement, eligibility or playable release is
created. Isolated unblocking requires an explicit test harness override for
every dependency; removing blockedBy alone is insufficient and must throw.
"""
from __future__ import annotations

import argparse
from collections import Counter
from copy import copy
from datetime import datetime
import json
from pathlib import Path
import re
import shutil

import openpyxl
from openpyxl.comments import Comment

from import_church_text import ROOT, MAP, TEXT, SOURCES, digest, expected_rows

NARRATIVE = ROOT / 'Assets/ConfigTables/Data/Narrative/Narrative.xlsx'
SCHEMA = ROOT / 'Assets/ConfigTables/Defines/schema.xml'
EVENT = 'church.1023.s1'
PRESENTATION = EVENT + '.conversation'
PROPERTY = 'entry=MorningBeforeMap;blocksSave=true;blockedBy=D-B01-bribe-amount,D-B02-revolution-source,D-C04-detention'
HEADERS = {
    'Event': 'eventId eventKind actorId targetId titleText contentKey setupType setupKey onceOnly logTag presentationClass presentationKey presentationLogTag eventProperty',
    'PresentationIndex': 'presentationKey presentationClass logTag',
    'SetupParams': 'setupKey tplName slotValues skipAble autoAdvance checkId',
    'ConversationPage': 'pageId presentationKey seq pageCondKeys pageBarConds optionGroupKey bgOverride bgmOverride stepType readingKey checkId',
    'ConversationLine': 'lineId pageId seq speakerKey side textKey voiceKey emote sfxKey accentFxKey accentTargetKey speakerNameKey stageLeftKey stageRightKey stageCenterKey',
    'Choice': 'choiceId groupKey textKey resultOwnerId seq checkId',
    'AbilityCheck': 'checkId successEventId failureEventId successResultOwnerId failureResultOwnerId comment mode',
    'AbilityCheckCondition': 'conditionId checkId attributeId threshold groupIndex seq',
    'Object': 'objId objType nameText iconPath portraitPath portraitIndoorPath avtarPath descText tags',
    'Text': 'key text comment',
}
NAMES = {'church.guard': '看守', 'church.inspector': '监察官'}
FORBIDDEN = ('Result', 'ConditionKey', 'Condition', 'EventDate', 'Action', 'Ending')


def require(ok, message):
    if not ok:
        raise RuntimeError(message)


def signature(cell):
    kind = 'empty' if cell.value is None and cell.data_type in ('inlineStr', 'n') else cell.data_type
    return (cell.value, kind, tuple(cell._style) if cell.has_style else None,
            (cell.comment.text, cell.comment.author) if cell.comment else None,
            cell.hyperlink.target if cell.hyperlink else None)


def source_rows():
    section = MAP.read_text(encoding='utf-8').split('## ' + EVENT + '\n', 1)[1].split('\n## ', 1)[0]
    rows, page = [], None
    for line in section.splitlines():
        if line.startswith('### page '):
            page = line.split('`')[1]
        if re.match(r'^\|P\d{3}\|', line):
            number, kind, mapping, original = line.split('|')[1:-1]
            rows.append(dict(number=int(number[1:]), kind=kind, mapping=mapping,
                             original=original, page=page))
    require([r['number'] for r in rows] == list(range(1, 154)), 'Expected all 153 source paragraphs')
    return rows


def guards(short):
    choice = 'choice:' + EVENT + '.p021'
    check = 'check:' + EVENT + '.language'
    dependency = 'dependency:'
    if short in ('hearing_dawn', 'hearing'):
        # This gate is evaluated before the release group can be presented.
        return dependency + 'D-B01-bribe-amount-and-account|' + dependency + 'D-C04-detention'
    if short == 'persuade':
        return choice
    if short == 'persuade_pass':
        return choice + '|' + check
    if short == 'persuade_fail':
        return choice + '|not:' + check + '|' + dependency + 'D-B01-fallback-funding'
    if short == 'bribe':
        return ('anyChoice:' + EVENT + '.p022;' + EVENT + '.retry.bribe|' +
                dependency + 'D-B01-bribe-amount-and-account')
    if short in ('rescue', 'record_notice', 'rescue_depart'):
        return dependency + 'D-C01-rescue-eligibility|' + dependency + 'D-B02-revolution-source'
    if short in ('home', 'home_end'):
        return dependency + 'D-C01-release-mode.released'
    if short.startswith('home_'):
        return dependency + 'D-C01-release-mode.' + {'home_persuade': 'persuasion', 'home_bribe': 'bribe', 'home_rescue': 'rescue'}[short]
    raise RuntimeError('Unknown source page: ' + short)


def speaker(original):
    for prefix, key in (('{playername}：', 'player'), ('阿黛尔：', 'adele'),
                        ('看守：', 'church.guard'), ('监察官：', 'church.inspector')):
        if original.startswith(prefix):
            return key
    return None


def plan():
    entries = expected_rows('1023.s1')  # DOCX hash, 153 paragraphs, exact source-map text.
    sources = source_rows()
    body = [e for e in entries if e['kind'].startswith('正文')]
    choices = [e for e in entries if e['kind'].startswith('选项')]
    require(len(body) == 113 and len(choices) == 3, 'Source display coverage changed')
    text_book = openpyxl.load_workbook(TEXT, read_only=True)
    try:
        actual = {}
        for row in text_book['Text'].iter_rows(min_row=5, values_only=True):
            if str(row[1]).startswith(EVENT + '.'):
                require(row[1] not in actual, 'Duplicate source Text: ' + str(row[1]))
                actual[row[1]] = row[2]
        require(actual == {e['key']: e['text'] for e in entries}, 'Existing Text/source mismatch')
    finally:
        text_book.close()
    rows = []

    def add(sheet, **values):
        rows.append(dict(sheet=sheet, values=values))

    add('Event', eventId=EVENT, eventKind='world', actorId='world', setupKey=EVENT,
        onceOnly=True, logTag='D1023S1', presentationClass='Conversation',
        presentationKey=PRESENTATION, presentationLogTag='D1023S1', eventProperty=PROPERTY)
    add('PresentationIndex', presentationKey=PRESENTATION, presentationClass='Conversation', logTag='D1023S1')
    add('SetupParams', setupKey=EVENT, tplName='Tpl_ArrivalConversation', slotValues='opts=', skipAble=False, autoAdvance=False)
    for key, name in NAMES.items():
        add('Text', key=key + '.name', text=name, comment='D generic source speaker; no portrait binding')
        add('Object', objId=key, objType='npc', nameText=key + '.name')
    for short, conditions in (
            ('language', [('language', 7, 1)]),
            ('anomaly', [('wisdom', 6, 1), ('perception', 6, 2)])):
        check_id = EVENT + '.' + short
        add('AbilityCheck', checkId=check_id, mode='Inline', comment='D1023 source node snapshot; no outcomes or result owners')
        for seq, (attribute, threshold, group) in enumerate(conditions, 1):
            add('AbilityCheckCondition', conditionId=check_id + '.' + str(seq), checkId=check_id,
                attributeId=attribute, threshold=threshold, groupIndex=group, seq=seq)
    pages = list(dict.fromkeys(e['page'].removeprefix(EVENT + '.') for e in body))
    pages.insert(0, 'hearing_dawn')  # Preserve P011's change of location before P013.
    source_by_number = {r['number']: r for r in sources}
    for page_seq, short in enumerate(pages, 1):
        page = EVENT + '.' + short
        content = [e for e in body if e['page'] == (EVENT + '.hearing' if short == 'hearing_dawn' else page)]
        if short in ('hearing', 'hearing_dawn'):
            content = [e for e in content if (int(e['source'][-3:]) < 13) == (short == 'hearing_dawn')]
        require(content, 'Empty content page: ' + short)
        check = 'language' if short == 'persuade_pass' else 'anomaly' if short == 'record_notice' else None
        background = ('church.cafe.dusk' if short.startswith('home') else
                      'church.detention.dawn' if short == 'hearing_dawn' else
                      'church.detention.dusk' if short in ('rescue', 'record_notice', 'rescue_depart') else
                      'church.detention.interior')
        group = 'release' if short == 'hearing' else 'fallback' if short == 'persuade_fail' else None
        add('ConversationPage', pageId=page, presentationKey=PRESENTATION, seq=page_seq,
            pageCondKeys=guards(short), optionGroupKey=EVENT + '.' + group + '.choices' if group else None,
            bgOverride=background, stepType='PassiveCheck' if check else 'Dialogue',
            checkId=EVENT + '.' + check if check else None)
        for seq, entry in enumerate(content, 1):
            number = int(entry['source'][-3:])
            raw = source_by_number[number]['original']
            who = speaker(raw)
            emote = re.search(r'（#([^）]+)）', raw)
            add('ConversationLine', lineId=entry['key'].removesuffix('.text'), pageId=page,
                seq=seq, speakerKey=who, side='none', textKey=entry['key'],
                speakerNameKey=who + '.name' if who in NAMES else None,
                emote=emote[1] if emote else None)
    for seq, entry in enumerate(choices, 1):
        add('Choice', choiceId=entry['key'].removesuffix('.text'), groupKey=EVENT + '.release.choices',
            textKey=entry['key'], seq=seq)
    for seq, (suffix, number) in enumerate((('bribe', 22), ('wait', 23)), 1):
        add('Choice', choiceId=EVENT + '.retry.' + suffix, groupKey=EVENT + '.fallback.choices',
            textKey=EVENT + f'.p{number:03}.text', seq=seq)
    counts = Counter(r['sheet'] for r in rows)
    require(counts['ConversationLine'] == 113 and counts['Choice'] == 5 and counts['ConversationPage'] == 14,
            'Prepared coverage changed')
    require(not set(counts).intersection(FORBIDDEN), 'Forbidden write scope')
    condition_enum = re.search(r'name="condType" type="([^"]+)"', SCHEMA.read_text(encoding='utf-8'))[1]
    require('DEPENDENCY' not in condition_enum.upper(), 'Condition dependency support changed; review locking')
    manifest = dict(event=EVENT, eventProperty=PROPERTY, counts=dict(counts), sources=sources,
        entries=entries, source_paragraphs=153, source_body_lines=113, source_choices=3,
        condition_enum=condition_enum, choice_lock={
            'choices': [EVENT + '.p022', EVENT + '.retry.bribe'],
            'mechanism': 'Root blockedBy plus dependency gates before hearing/fallback group; no legal dependency Condition enum exists.',
            'unblocking': 'Explicit isolated test harness overrides only. Removing blockedBy alone throws before choices; no automatic success or missing-dependency fallback.'},
        dependencies={
            'D-B01-bribe-amount-and-account': 'Unresolved B and real account; unknown throws, never 0 or insufficient funds.',
            'D-B01-fallback-funding': 'P038 requires resolved funding and conditional group exposure. Current static group cannot express insufficient-funds bypass while retaining P037. Parent harness must explicitly handle P037 then offer only funded fallback, or bypass group to rescue. This preparation does not implement that orchestration.',
            'D-C01-rescue-eligibility': 'Explicit wait/retry.wait OR failed persuasion with resolved insufficient funding; no completed bribe/persuasion. Not an automatic wait choice.',
            'D-B02-revolution-source': 'Real revolution tendency >=0; unknown throws, never default0 or failure/death.',
            'D-C01-release-mode': 'Mutually exclusive local persuasion/bribe/rescue routes; unknown throws; home/home_end only if released.',
            'D-C04-detention': 'Real completed 1020 historical detention/current custody/day18 schedule; no fake facts/date/AP/business.'},
        pending={'registry': {key: 'Neutral placeholder pending registry; no asset edits by this script' for key in
                             ('church.detention.dawn', 'church.detention.interior', 'church.detention.dusk', 'church.cafe.dusk')},
                 'cafe': 'church.cafe.dusk pending registry; oct07.cafe.night is not silently used as dusk.',
                 'presentation': 'P009/P012/P056 stage entries, P081/P153 fades and specified audio remain source anchors, not verified effects.',
                 'settlement': 'No release/completed/anomaly flags, money, failed result or 1023.s2 queue; parent must add atomic root-tail settlement after blockers resolve.'},
        omitted_sheets=list(FORBIDDEN), generation_run=False, editor_operations=False, preview_enabled=False)
    return rows, manifest


def prepare(books, rows):
    added, locations, indexes = Counter(), [], {}
    for item in rows:
        sheet, values = item['sheet'], item['values']
        path = TEXT if sheet == 'Text' else NARRATIVE
        ws = books[path][sheet]
        expected = ['##var'] + HEADERS[sheet].split()
        require([c.value for c in ws[1]] == expected, 'Schema drift: ' + sheet)
        headers = {v: i for i, v in enumerate(expected, 1)}
        if sheet not in indexes:
            index = {}
            for row in ws.iter_rows(min_row=5):
                if row[1].value is not None:
                    require(row[1].value not in index, 'Duplicate key: ' + sheet + '/' + str(row[1].value))
                    index[row[1].value] = row[1].row
            indexes[sheet] = index
        index = indexes[sheet]
        key = next(iter(values.values()))
        if key in index:
            number = index[key]
            # Shared generic identity may be registered by 1020 first.
            fields = ('key', 'text') if sheet == 'Text' else ('objId', 'objType', 'nameText') if sheet == 'Object' else HEADERS[sheet].split()
            for field in fields:
                require(ws.cell(number, headers[field]).value == values.get(field), f'Existing row conflict: {sheet}/{key}/{field}')
        else:
            number = ws.max_row + 1
            for column in range(1, ws.max_column + 1):
                ws.cell(number, column)._style = copy(ws.cell(number - 1, column)._style)
            for field, value in values.items():
                if value is not None:
                    ws.cell(number, headers[field], value)
            ws.cell(number, 2).comment = Comment('D1023 blocked source preparation; source numbering preserved; no outcomes.', 'D-church')
            index[key] = number
            added[sheet] += 1
        locations.append(dict(sheet=sheet, key=key, row=number))
    for sheet in FORBIDDEN:
        require(not any(EVENT in str(c.value) for row in books[NARRATIVE][sheet].iter_rows(min_row=5) for c in row),
                'Unexpected existing formal row: ' + sheet)
    return dict(added), locations


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='Parent-authorized CAS append with backups and manifest')
    args = parser.parse_args()
    base = ROOT / 'Library/Locus/tmp'
    lock = base / 'narrative-source-write.lock'
    owned_lock = False
    books = {}
    try:
        if args.apply:
            base.mkdir(parents=True, exist_ok=True)
            with lock.open('x', encoding='utf-8') as handle:
                handle.write('D1023 blocked release content append\n')
            owned_lock = True
        watched = [NARRATIVE, TEXT, MAP, SCHEMA, Path(__file__),
                   ROOT / 'Tools/Narrative/import_church_text.py',
                   ROOT / 'Docs/superpowers/specs/2026-10-16-content-batches/D-church/spec.md',
                   ROOT / 'Docs/superpowers/specs/2026-10-16-content-batches/D-church/handoff.md',
                   Path('D:/桌面/文案/王庭教会/MA70.10.23-S1.docx')]
        hashes = {p: digest(p) for p in watched}
        rows, manifest = plan()
        books = {p: openpyxl.load_workbook(p) for p in (NARRATIVE, TEXT)}
        before = {p: {(s.title, c.coordinate): signature(c) for s in wb for row in s for c in row}
                  for p, wb in books.items()}
        added, locations = prepare(books, rows)
        for p, sha in hashes.items():
            require(digest(p) == sha, 'Concurrent input change: ' + str(p))
        summary = dict(writes=False, would_append=added, eventProperty=PROPERTY,
                       counts=manifest['counts'], source_paragraphs=153, source_body_lines=113, source_choices=3,
                       condition_enum=manifest['condition_enum'], choice_lock=manifest['choice_lock'],
                       dependencies=manifest['dependencies'], pending=manifest['pending'],
                       before_sha256={str(p): sha for p, sha in hashes.items()})
        if not args.apply or not added:
            print(json.dumps(summary, ensure_ascii=False, indent=2))
            return
        out = base / 'church-content' / (datetime.now().strftime('%Y%m%d-%H%M%S-%f') + '-1023-release-config')
        out.mkdir(parents=True)
        candidates = {}
        for p, wb in books.items():
            require(digest(p) == hashes[p], 'Concurrent workbook change before backup: ' + str(p))
            backup = out / (p.stem + '.before.xlsx')
            shutil.copy2(p, backup)
            require(digest(backup) == hashes[p], 'Backup race: ' + str(p))
            candidate = out / (p.stem + '.candidate.xlsx')
            wb.save(candidate)
            candidates[p] = candidate
        saved = {p: openpyxl.load_workbook(c) for p, c in candidates.items()}
        try:
            for p, cells in before.items():
                for (sheet, coordinate), value in cells.items():
                    require(signature(saved[p][sheet][coordinate]) == value, f'Existing cell changed: {p}/{sheet}/{coordinate}')
            again, check_locations = prepare(saved, rows)
            require(not again and locations == check_locations, 'Candidate/idempotency verification failed')
        finally:
            for wb in saved.values():
                wb.close()
        manifest.update(time=datetime.now().astimezone().isoformat(), added=added, locations=locations,
                        before_sha256={str(p): sha for p, sha in hashes.items()},
                        preserved_existing_cells={str(p): len(cells) for p, cells in before.items()}, replaced=[])
        journal = out / 'manifest.json'
        journal.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
        for p, sha in hashes.items():
            require(digest(p) == sha, 'Concurrent input change; candidate retained: ' + str(p))
        # Cooperative lock protects this two-workbook commit. Journal records each
        # replacement; a process crash between replacements requires owner recovery.
        changed_paths = {TEXT if sheet == 'Text' else NARRATIVE for sheet in added}
        for p, candidate in candidates.items():
            if p not in changed_paths:
                continue
            require(digest(p) == hashes[p], 'CAS conflict: ' + str(p))
            candidate.replace(p)
            manifest['replaced'].append(str(p))
            journal.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
        manifest['after_sha256'] = {str(p): digest(p) for p in books}
        journal.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps(dict(writes=True, added=added, manifest=str(journal)), ensure_ascii=False))
    finally:
        for wb in books.values():
            wb.close()
        if owned_lock:
            lock.unlink()


if __name__ == '__main__':
    main()
