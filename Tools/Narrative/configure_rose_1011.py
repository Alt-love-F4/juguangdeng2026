"""Stage all Rose 1011 source content while preserving unresolved gameplay boundaries."""
from copy import copy
from datetime import datetime
import hashlib
import shutil
from pathlib import Path

import openpyxl

import import_rose_text as source

ROOT = source.ROOT
NARRATIVE = ROOT / 'Assets/ConfigTables/Data/Narrative/Narrative.xlsx'
TEXT = ROOT / 'Assets/ConfigTables/Data/Shared/Text.xlsx'
initial_hash = hashlib.sha256(NARRATIVE.read_bytes()).hexdigest()
text_initial_hash = hashlib.sha256(TEXT.read_bytes()).hexdigest()
book = openpyxl.load_workbook(NARRATIVE)
text_book = openpyxl.load_workbook(TEXT)
entries = source.expected_rows('1011')
originals = {row[0]: row[4] for row in source.source_rows('1011')}


def append(sheet_name, values):
    ws = book[sheet_name]
    headers = [cell.value for cell in ws[1]]
    keyfield = headers[1]
    matches = [row for row in ws.iter_rows(min_row=5) if row[1].value == values[keyfield]]
    if matches:
        row = matches[0]
        assert all(row[headers.index(key)].value == value for key, value in values.items()), \
            f'Existing different row {sheet_name}/{values[keyfield]}'
        return
    row_number = ws.max_row + 1
    for column in range(1, ws.max_column + 1):
        ws.cell(row_number, column)._style = copy(ws.cell(5, column)._style)
    for key, value in values.items():
        ws.cell(row_number, headers.index(key) + 1, value)


def upsert(sheet_name, values):
    ws = book[sheet_name]
    headers = [cell.value for cell in ws[1]]
    keyfield = headers[1]
    matches = [row for row in ws.iter_rows(min_row=5) if row[1].value == values[keyfield]]
    if len(matches) > 1:
        raise AssertionError(f'Duplicate row {sheet_name}/{values[keyfield]}')
    if matches:
        row_number = matches[0][1].row
    else:
        row_number = ws.max_row + 1
        for column in range(1, ws.max_column + 1):
            ws.cell(row_number, column)._style = copy(ws.cell(5, column)._style)
    for key, value in values.items():
        ws.cell(row_number, headers.index(key) + 1, value)


def append_text(values):
    ws = text_book['Text']
    headers = [cell.value for cell in ws[1]]
    keyfield = headers[1]
    matches = [row for row in ws.iter_rows(min_row=5) if row[1].value == values[keyfield]]
    if matches:
        row = matches[0]
        assert all(row[headers.index(key)].value == value for key, value in values.items()), \
            f'Existing different row Text/{values[keyfield]}'
        return
    row_number = ws.max_row + 1
    for column in range(1, ws.max_column + 1):
        ws.cell(row_number, column)._style = copy(ws.cell(5, column)._style)
    for key, value in values.items():
        ws.cell(row_number, headers.index(key) + 1, value)


# The producer belongs to A-neutral; these keys are read here but never written by B1011.
for key, text in [
    ('condition.story.neutral.1010.s1.completed', '10月10日中立筹备已完成'),
    ('condition.story.rose.1011.s1.completed', '10月11日玫瑰剧情已完成'),
    ('condition.story.rose.1011.s1.sylvain_appreciation', '西尔万已认可店长'),
]:
    append_text(dict(key=key, text=text, comment='B1011 condition display name; written for Luban reference integrity'))

for key, only_set_true in [
    ('story.neutral.1010.s1.completed', True),
    ('story.rose.1009.s1.completed', True),
    ('story.rose.1011.s1.completed', True),
    ('story.rose.1011.s1.sylvain_appreciation', True),
]:
    append('ConditionKey', dict(key=key, nameText='condition.' + key, initialValue=False, onlySetTrue=only_set_true))

upsert('Event', dict(
    eventId='rose.1011.s1', eventKind='actor', actorId='player', titleText=None,
    contentKey=None, setupType='Tpl_ArrivalConversation', setupKey='rose.1011.s1',
    onceOnly=True, logTag='B1011', presentationClass='Conversation',
    presentationKey='rose.1011.s1.conversation', presentationLogTag='B1011',
    eventProperty='entry=MorningBeforeMap;blocksSave=true;blockedBy=B-B05,B-C02,B-C04,B-C07;contentBoundary=P047;previewTail=P052'))
append('EventDate', dict(eventId='rose.1011.s1', day=6, timeSlot='morning', repeatRule='once', priority=0))
append('SetupParams', dict(setupKey='rose.1011.s1', tplName='Tpl_ArrivalConversation', slotValues=None,
                          skipAble=False, autoAdvance=False, checkId=None))
append('PresentationIndex', dict(presentationKey='rose.1011.s1.conversation', presentationClass='Conversation',
                                logTag='B1011 full text through P140; production bridge remains blocked'))

for condition_id, group, cond_type, target, value in [
    ('rose.1011.s1.day', 1, 'BAR_BETWEEN', 'world.calendar', '6,6'),
    ('rose.1011.s1.neutral_ready', 1, 'BOOL_EQ', 'story.neutral.1010.s1.completed', 'true'),
    ('rose.1011.s1.rose1009_ready', 1, 'BOOL_EQ', 'story.rose.1009.s1.completed', 'true'),
    ('rose.1011.s1.incomplete', 1, 'BOOL_EQ', 'story.rose.1011.s1.completed', 'false'),
]:
    append('Condition', dict(conditionId=condition_id, ownerType='event', ownerId='rose.1011.s1',
                             groupIndex=group, condType=cond_type, target=target, value=value))

check_ids = {
    'sparrow': 'rose.1011.s1.check.sparrow',
    'portrait': 'rose.1011.s1.check.portrait',
    'wisdom': 'rose.1011.s1.check.wisdom',
}
for check_id, comment, attribute, threshold in [
    (check_ids['sparrow'], 'P014-P017; perception >5 means int >=6; failure skips the insight page.', 'perception', 6),
    (check_ids['portrait'], 'P111-P114; perception >4 means int >=5; failure skips the memory page.', 'perception', 5),
    (check_ids['wisdom'], 'P080 correct answer; wisdom >=3 enters the long route, failure goes to the existing early_end.', 'wisdom', 3),
]:
    append('AbilityCheck', dict(checkId=check_id, successEventId=None, failureEventId=None,
                                successResultOwnerId=None, failureResultOwnerId=None,
                                comment=comment, mode='Inline'))
    append('AbilityCheckCondition', dict(conditionId=check_id + '.1', checkId=check_id,
                                         attributeId=attribute, threshold=threshold, groupIndex=1, seq=1))

page_specs = [
    ('arrival', 1, None, None, None, 'cafe.day'),
    ('sparrow_perception', 2, None, None, 'PassiveCheck', None),
    ('drink_choice', 3, None, 'rose.1011.s1.choice.drink', None, None),
    ('drink_black', 4, 'choice:rose.1011.s1.choice.p020', None, None, None),
    ('drink_rich', 5, 'choice:rose.1011.s1.choice.p022', None, None, None),
    ('drink_milk', 6, 'choice:rose.1011.s1.choice.p024', None, None, None),
    ('news_choice', 7, None, 'rose.1011.s1.choice.news', None, None),
    ('news_known', 8, 'choice:rose.1011.s1.choice.p032', None, None, None),
    ('news_unknown', 9, 'choice:rose.1011.s1.choice.p033', None, None, None),
    ('sylvain_arrives', 10, None, None, None, None),
    ('brew_blocked', 11, None, None, None, None),
    ('brew_low', 12, 'dependency:rose.1011.brew_quality', None, None, None),
    ('nature_choice', 13, None, 'rose.1011.s1.choice.nature', None, None),
    ('nature_ordinary', 14, 'anyChoice:rose.1011.s1.choice.p060;rose.1011.s1.choice.p061', None, None, None),
    ('early_end_nature', 15, 'anyChoice:rose.1011.s1.choice.p060;rose.1011.s1.choice.p061', None, None, None),
    ('nature_not_person', 16, 'choice:rose.1011.s1.choice.p062', 'rose.1011.s1.choice.reason', None, None),
    ('reason_early_end_wrong', 17, 'anyChoice:rose.1011.s1.choice.p079;rose.1011.s1.choice.p081', None, None, None),
    ('reason_title', 18, 'choice:rose.1011.s1.choice.p080', None, 'PassiveCheck', None),
    ('reason_early_end_wisdom', 19, 'choice:rose.1011.s1.choice.p080|not:check:rose.1011.s1.check.wisdom', None, None, None),
    ('history_yes', 20, 'specialization.player.letter.politics', None, None, None),
    ('history_no', 21, 'not:specialization.player.letter.politics', None, None, None),
    ('history_lecture', 22, None, None, None, None),
    ('portrait_memory', 23, None, None, 'PassiveCheck', None),
    ('history_return', 24, 'choice:rose.1011.s1.choice.p080|check:rose.1011.s1.check.wisdom', None, None, None),
    ('card_notice', 25, 'choice:rose.1011.s1.choice.p080|check:rose.1011.s1.check.wisdom', None, None, None),
    ('card_read', 26, 'choice:rose.1011.s1.choice.p080|check:rose.1011.s1.check.wisdom', None, 'LetterRead', None),
    ('history_recognition', 27, 'choice:rose.1011.s1.choice.p080|check:rose.1011.s1.check.wisdom|specialization.player.letter.politics', None, None, None),
    ('full_end', 28, 'choice:rose.1011.s1.choice.p080|check:rose.1011.s1.check.wisdom', None, None, None),
]
for page, seq, conditions, option_group, step_type, background in page_specs:
    values = dict(pageId='rose.1011.s1.' + page, presentationKey='rose.1011.s1.conversation', seq=seq,
                  pageCondKeys=conditions, pageBarConds=None, optionGroupKey=option_group,
                  bgOverride=background, bgmOverride=None, stepType=step_type,
                  readingKey='rose.1011.s1.card' if page == 'card_read' else None,
                  checkId=(check_ids['sparrow'] if page == 'sparrow_perception' else
                           check_ids['wisdom'] if page == 'wisdom_check' else
                           check_ids['portrait'] if page == 'portrait_memory' else None))
    append('ConversationPage', values)

# Rebuild the common early-end text once for each route that can reach it.
for page in ['early_end_nature', 'reason_early_end_wrong', 'reason_early_end_wisdom']:
    for entry in entries:
        if entry['source'].endswith(('P068', 'P069', 'P070', 'P071', 'P072', 'P073')):
            suffix = entry['key'].rsplit('.', 1)[-1]
            append('ConversationLine', dict(
                lineId=f'rose.1011.s1.{page}.{suffix}', pageId=f'rose.1011.s1.{page}',
                seq=int(suffix[-3:]), speakerKey=base_speaker(originals[int(entry['source'].split('P')[-1])].strip()),
                side='none', textKey=entry['key'], voiceKey=None, emote=None,
                sfxKey=None, accentFxKey=None, accentTargetKey=None, speakerNameKey=None))

for page_number, suffix, group in [
    (60, 'nature', 'rose.1011.s1.choice.nature'),
    (61, 'nature', 'rose.1011.s1.choice.nature'),
    (62, 'nature', 'rose.1011.s1.choice.nature'),
    (79, 'reason', 'rose.1011.s1.choice.reason'),
    (80, 'reason', 'rose.1011.s1.choice.reason'),
    (81, 'reason', 'rose.1011.s1.choice.reason'),
]:
    append('Choice', dict(choiceId=f'rose.1011.s1.choice.p{page_number:03}', groupKey=group,
                          textKey=f'rose.1011.s1.text.p{page_number:03}', resultOwnerId=None,
                          seq={60: 1, 61: 2, 62: 3, 79: 1, 80: 2, 81: 3}[page_number],
                          checkId=check_ids['wisdom'] if page_number == 80 else None))

append('PresentationIndex', dict(presentationKey='rose.1011.s1.card', presentationClass='Reading',
                                logTag='B1011 card text preview; read/cancel lifecycle remains blocked'))
append('ReadingPage', dict(pageId='rose.1011.s1.card.page', presentationKey='rose.1011.s1.card', seq=1,
                          titleKey=None, imageKey=None, pageCondKeys=None, pageBarConds=None))
for seq, number in enumerate([128, 129, 130], 1):
    append('ReadingLine', dict(lineId=f'rose.1011.s1.card.p{number}', pageId='rose.1011.s1.card.page',
                              seq=seq, mode='narration', speakerKey=None,
                              textKey=f'rose.1011.s1.text.p{number}', voiceKey=None))

mixed_speakers = {
    52: {'a': 'sylvain', 'b': None, 'c': 'sylvain', 'd': 'adele', 'e': 'sylvain', 'f': None, 'g': 'sylvain'},
    85: {'a': 'player', 'b': 'player', 'c': 'player'},
    87: {'a': 'sylvain', 'b': 'sylvain'},
    110: {'a': 'sylvain', 'b': 'sylvain', 'c': 'sylvain'},
    116: {'a': 'sparrow', 'b': 'sparrow'},
    117: {'a': 'sylvain', 'b': 'sylvain', 'c': 'sylvain', 'd': 'sylvain'},
    122: {'a': 'sylvain', 'b': 'sylvain'},
    125: {'a': 'sylvain', 'b': 'sylvain'},
}

def base_speaker(raw):
    if raw.startswith('{playername}'):
        return 'player'
    for prefix, speaker in [('阿黛尔', 'adele'), ('麻雀', 'sparrow'), ('西尔万', 'sylvain')]:
        if raw.startswith(prefix):
            return speaker
    return None

line_sequences = {}
for entry in entries:
    number = int(entry['source'].split('P')[-1])
    if entry['kind'] in ('选项', '名片正文'):
        continue
    page = entry['page']
    line_sequences[page] = line_sequences.get(page, 0) + 1
    suffix = entry['key'].rsplit('.', 1)[-1]
    speaker = mixed_speakers.get(number, {}).get(suffix, base_speaker(originals[number].strip()))
    append('ConversationLine', dict(
        lineId=entry['key'].replace('.text.', '.'), pageId=page, seq=line_sequences[page],
        speakerKey=speaker, side='none', textKey=entry['key'], voiceKey=None, emote=None,
        sfxKey=None, accentFxKey=None, accentTargetKey=None, speakerNameKey=None))

for group, choices in [
    ('rose.1011.s1.choice.drink', [20, 22, 24]),
    ('rose.1011.s1.choice.news', [32, 33]),
]:
    for seq, number in enumerate(choices, 1):
        append('Choice', dict(choiceId=f'rose.1011.s1.choice.p{number:03}', groupKey=group,
                              textKey=f'rose.1011.s1.text.p{number:03}', resultOwnerId=None,
                              seq=seq, checkId=None))

stamp = datetime.now().strftime('%Y%m%d-%H%M%S')
out = ROOT / 'Library/Locus/tmp/rose-content' / f'{stamp}-1011-wire'
out.mkdir(parents=True)
shutil.copy2(NARRATIVE, out / 'Narrative.before.xlsx')
shutil.copy2(TEXT, out / 'Text.before.xlsx')
book.save(out / 'Narrative.candidate.xlsx')
text_book.save(out / 'Text.candidate.xlsx')
if hashlib.sha256(NARRATIVE.read_bytes()).hexdigest() != initial_hash or hashlib.sha256(TEXT.read_bytes()).hexdigest() != text_initial_hash:
    raise RuntimeError('Concurrent source workbook change; candidates preserved and sources not replaced')
(out / 'Narrative.candidate.xlsx').replace(NARRATIVE)
(out / 'Text.candidate.xlsx').replace(TEXT)
print('Configured Rose 1011 source content through P140: all 128 text/choice entries, branch pages, card reading rows, checks 6/5/3.')
print('Formal root remains blocked by B-B05/B-C02/B-C04/B-C07; no completion result was added before production and reading lifecycles.')
print('Backup:', out)
