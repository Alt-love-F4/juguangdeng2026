import unittest
from collections import Counter, defaultdict
from datetime import date
import re
from unittest.mock import patch
from zipfile import ZipFile
import xml.etree.ElementTree as ET

import openpyxl
from pathlib import Path
import daily

class DailyContentTests(unittest.TestCase):
 # Independent expectations: do not derive speaker/background assertions from
 # the compiler constants whose regressions these tests need to catch.
 BACKGROUNDS = {
  '旧城区': 'old.street.day', '圣徒街': 'street.day',
  '圣西尔万郊区': 'suburb.day', '大堂区': 'market.day',
  '圣地大教堂': 'cathedral.day', '圣卷大教堂': 'cathedral.day', '灰墙学院': 'street.day',
  '市政厅': 'street.day', '首都法院': 'street.day', '花园区': 'street.day',
 }
 SPEAKER_KEYS = {
  '{playername}': 'player', '阿黛尔': 'adele', '麻雀': 'sparrow',
  '柯莱特': 'colette', '欧仁': 'eugene', '西尔万': 'sylvain',
  '帕斯卡尔': 'pascal', '萨德': 'sade', '埃马纽埃尔': 'sorel',
  '索雷尔': 'sorel', '勒费弗尔': 'lefebvre', '顾客': 'guest_a',
  '教会帮工': 'daily.actor.church_helper',
 }

 @classmethod
 def setUpClass(cls):
  cls.project_root = Path(__file__).resolve().parents[3]
  cls.source_groups = []
  source = cls.project_root / daily.SOURCE_PATH
  workbook = openpyxl.load_workbook(source, read_only=True, data_only=True)
  try:
   for sheet in workbook:
    group = None
    for values in sheet.iter_rows(values_only=True):
     row = [str(v).strip() if v is not None else '' for v in values[:3]]
     row += [''] * (3 - len(row))
     if re.fullmatch(r'[A-Z]+-(?:R)?\d{2}', row[0]):
      group = {'code': row[0], 'kind': row[1], 'title': row[2],
               'sheet': sheet.title, 'rows': []}
      cls.source_groups.append(group)
     elif group is not None:
      group['rows'].append(row)
  finally:
   workbook.close()
  cls.map_groups = [g for g in cls.source_groups if not g['sheet'].startswith('咖啡馆')]
  cls.data = daily.build(cls.project_root)
  cls.source_sha256 = daily.sha256(source.read_bytes()).hexdigest()
  cls.texts = {row['key']: row['text'] for row in cls.data['Text']}
  cls.pages_by_presentation = defaultdict(list)
  for page in cls.data['ConversationPage']:
   cls.pages_by_presentation[page['presentationKey']].append(page)
  cls.lines_by_page = defaultdict(list)
  for line in cls.data['ConversationLine']:
   cls.lines_by_page[line['pageId']].append(line)

 @staticmethod
 def _root(group):
  return 'daily.' + group['code'].lower().replace('-', '.')

 @staticmethod
 def _prose(row):
  label, speaker, body = row
  if label in ('触发条件', '结果/配置'):
   return None
  if label in ('入场', '动作'):
   speaker, body = '', body or speaker
  return (speaker, body) if body else None

 def _assert_source_lines(self, page, expected):
  lines = sorted(self.lines_by_page[page['pageId']], key=lambda line: line['seq'])
  self.assertEqual(len(expected), len(lines), page['pageId'])
  self.assertEqual(len(lines), len({line['seq'] for line in lines}), page['pageId'])
  for line, (speaker, body) in zip(lines, expected):
   with self.subTest(page=page['pageId'], line=line['lineId'], speaker=speaker):
    self.assertEqual(body, self.texts[line['textKey']])
    if speaker:
     self.assertIn(speaker, self.SPEAKER_KEYS, 'New source speakers require an explicit identity expectation')
    self.assertEqual(self.SPEAKER_KEYS.get(speaker), line.get('speakerKey'))
    side = 'left' if speaker == '{playername}' else 'right' if speaker in self.SPEAKER_KEYS else 'none'
    self.assertEqual(side, line.get('side'))
    self.assertEqual(speaker, self.texts.get(line.get('speakerNameKey'), ''))

 def test_all_270_groups_and_six_papers_are_mapped(self):
  self.assertEqual(daily.SOURCE_SHA256,self.source_sha256)
  self.assertEqual(daily.SOURCE_SHA256,self.data['_audit']['daily']['sha256'])
  self.assertEqual(270,len(self.data['_audit']['daily']['groups']))
  self.assertEqual(6,len(self.data['_audit']['newspapers']))
  self.assertEqual(9,len(self.data['Action']))

 def test_latest_news_windows_exclusions_and_issue_read_gates(self):
  conditions=self.data['Condition']
  cases={
   'daily.news.01': ({(2,8)},'story.newspaper.1007.completed'),
   'daily.news.07': ({(3,6)},'story.newspaper.1007.completed'),
   'daily.news.02': ({(9,10),(14,15)},'story.newspaper.1014.completed'),
   'daily.news.13': ({(17,22)},'story.newspaper.1021.completed'),
   'daily.news.05': ({(23,29)},'story.newspaper.1028.completed'),
  }
  for root,(windows,issue_flag) in cases.items():
   rows=[row for row in conditions if row['ownerType']=='event' and row['ownerId']==root]
   date_rows=[row for row in rows if row['condType']=='BAR_BETWEEN' and row['target']=='world.calendar'
      and row['value'] not in ('2,48',)]
   self.assertEqual(windows,{tuple(map(int,row['value'].split(','))) for row in date_rows},root)
   groups={row['groupIndex'] for row in date_rows}
   self.assertEqual(groups,{row['groupIndex'] for row in rows if row['target']==issue_flag},root)
   newspaper=issue_flag[len('story.'):].removesuffix('.completed')
   self.assertIn(newspaper,{row['eventId'] for row in self.data['Event']})

 def test_date_window_parser_keeps_exclusion_out_of_minimum(self):
  windows,excluded,earliest=daily.date_rules('窗口：10月14—20日；排除10月16—18日')
  self.assertEqual([(daily.day(10,14),daily.day(10,20))],windows)
  self.assertEqual([(daily.day(10,16),daily.day(10,18))],excluded)
  self.assertIsNone(earliest)

 def test_latest_news_negative_and_prior_event_guards_are_truthful(self):
  conditions=self.data['Condition']
  ritual=[row for row in conditions if row['ownerType']=='event' and row['ownerId']=='daily.news.08'
      and row['target']=='story.church.1009.s2.completed']
  self.assertEqual(['false'],[row['value'] for row in ritual])
  relief=[row for row in conditions if row['ownerType']=='event' and row['ownerId']=='daily.news.11'
      and row['target']=='story.daily.old.02.completed']
  self.assertEqual(['true'],[row['value'] for row in relief])
 def test_choice_events_are_not_flattened(self):
  self.assertEqual(40,len(self.data['Choice']))
  responses=[p for p in self.data['ConversationPage']if '.response.'in p['pageId']]
  self.assertEqual(40,len(responses))
  self.assertTrue(all(p.get('pageCondKeys','').startswith('choice:')for p in responses))
 def test_no_directives_displayed_as_dialogue(self):
  displayed={x['key']:x['text']for x in self.data['Text']}
  for row in self.data['ConversationLine']:
   self.assertFalse(displayed[row['textKey']].startswith(('【结果','【最早','【触发条件')))
 def test_order_templates_are_nonmodal_snapshots(self):
  self.assertEqual(30,len(self.data['OpeningPresentation']))
  self.assertFalse(any('ambient' in e['eventId'] for e in self.data['Event']))
 def test_growth_is_conditional_integer_tenths(self):
  rs=[r for r in self.data['Result']if r['resultType']=='TRAIN_ATTRIBUTE']
  self.assertEqual(462,len(rs))
  self.assertTrue(all(r['value']in['1','2']for r in rs))
  ids={r['resultId']for r in rs};guards=[c for c in self.data['Condition']if c['ownerType']=='result'and c['ownerId']in ids]
  self.assertEqual(len(rs),len(guards))

 def test_all_240_map_roots_and_indexes_use_conversation(self):
  self.assertEqual(240, len(self.map_groups))
  self.assertEqual(Counter({'ordinary': 216, 'news': 15, 'visit': 9}), Counter(
   'news' if g['sheet'] == '新闻反应' else 'visit' if g['sheet'] == '首次到访' else 'ordinary'
   for g in self.map_groups))
  roots = {self._root(g) for g in self.map_groups}
  events = [e for e in self.data['Event'] if e['eventId'].startswith('daily.')]
  self.assertEqual(240, len(events))
  self.assertEqual(roots, {e['eventId'] for e in events})
  indexes = [p for p in self.data['PresentationIndex'] if p['presentationKey'].startswith('daily.')]
  self.assertEqual(240, len(indexes))
  self.assertEqual({root + '.presentation' for root in roots}, {p['presentationKey'] for p in indexes})
  for row in events + indexes:
   with self.subTest(root=row.get('eventId', row['presentationKey'])):
    self.assertEqual('Conversation', row['presentationClass'])
  for event in events:
   self.assertEqual(event['eventId'] + '.presentation', event['presentationKey'])
  for table in ('ReadingPage', 'Notice'):
   self.assertFalse(any(row['presentationKey'].startswith('daily.') for row in self.data[table]))

 def test_every_map_page_has_its_location_background_and_dialogue(self):
  for group in self.map_groups:
   root = self._root(group)
   location = group['sheet']
   if location == '首次到访':
    location = group['title']
   elif location == '新闻反应':
    locations = [loc for loc in self.BACKGROUNDS if loc in group['title']]
    self.assertEqual(1, len(locations), group['code'])
    location = locations[0]
   with self.subTest(root=root):
    pages = self.pages_by_presentation[root + '.presentation']
    self.assertTrue(pages, 'Every map daily root must have a ConversationPage')
    for page in pages:
     self.assertEqual(self.BACKGROUNDS[location], page.get('bgOverride'), page['pageId'])
     self.assertTrue(self.lines_by_page[page['pageId']], page['pageId'])

 def test_map_prose_and_speakers_match_every_source_row_in_order(self):
  # The workbook's former Reading convention is intentionally superseded by
  # the requested Conversation presentation; its prose and identities are not.
  for group in self.map_groups:
   with self.subTest(code=group['code']):
    pages = sorted(self.pages_by_presentation[self._root(group) + '.presentation'], key=lambda p: p['seq'])
    expected_opening, responses = [], defaultdict(list)
    for row in group['rows']:
     if row[0].startswith('选项 '):
      continue
     prose = self._prose(row)
     if prose is None:
      continue
     if row[0].startswith('回应 '):
      responses[row[0].split()[-1]].append(prose)
     else:
      expected_opening.append(prose)
    self.assertEqual(1 + len(responses), len(pages))
    self._assert_source_lines(pages[0], expected_opening)
    expected_guards = {'choice:' + self._root(group) + '.choice.' + label.lower(): rows
                       for label, rows in responses.items()}
    self.assertEqual(set(expected_guards), {page.get('pageCondKeys') for page in pages[1:]})
    for page in pages[1:]:
     self._assert_source_lines(page, expected_guards[page['pageCondKeys']])

 def test_choice_texts_groups_and_selected_results_stay_intact(self):
  groups = [g for g in self.map_groups if g['kind'] == '选择事件']
  self.assertEqual(20, len(groups))
  by_group = defaultdict(list)
  for choice in self.data['Choice']:
   by_group[choice['groupKey']].append(choice)
  for group in groups:
   root = self._root(group)
   with self.subTest(root=root):
    pages = sorted(self.pages_by_presentation[root + '.presentation'], key=lambda p: p['seq'])
    self.assertTrue(pages, 'Choice root must retain its opening page')
    self.assertEqual(root + '.choices', pages[0].get('optionGroupKey'))
    expected = [(r[0].split()[-1].lower(), r[2]) for r in group['rows'] if r[0].startswith('选项 ')]
    choices = sorted(by_group[root + '.choices'], key=lambda c: c['seq'])
    self.assertEqual([(root + '.choice.' + label, body) for label, body in expected],
                     [(c['choiceId'], self.texts[c['textKey']]) for c in choices])
    for choice, (label, _) in zip(choices, expected):
     selected = [r for r in self.data['Result'] if r['eventId'] == choice['resultOwnerId']]
     self.assertEqual([('SET_FLAG', root + '.selected.' + label, 'true')],
                      [(r['resultType'], r['target'], r['value']) for r in selected])
  for group in self.map_groups:
   if group['kind'] != '选择事件':
    pages = self.pages_by_presentation[self._root(group) + '.presentation']
    self.assertFalse(any(p.get('optionGroupKey') for p in pages), group['code'])

 def test_real_newspapers_remain_reading_not_conversation(self):
  # Derive dates and complete prose from the actual source DOCX files, not an
  # assumed weekly schedule or the compiler's own audit output.
  expected = {}
  namespace = '{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
  for source in sorted((self.project_root / '策划文件/文案6点/文案/b-报纸事件-换皮').glob('*.docx')):
   with ZipFile(source) as archive:
    document = ET.fromstring(archive.read('word/document.xml'))
   paragraphs = []
   for paragraph in document.iter(namespace + 'p'):
    body = ''.join(node.text or '' if node.tag == namespace + 't' else
                   '\n' if node.tag in (namespace + 'br', namespace + 'cr') else ''
                   for node in paragraph.iter()).strip()
    if body:
     paragraphs.append(body)
   match = re.search(r'670年\s*(10|11)月\s*(\d{1,2})日', '\n'.join(paragraphs))
   self.assertIsNotNone(match, str(source))
   month, day = map(int, match.groups())
   root = f'newspaper.{month}{day:02d}'
   self.assertNotIn(root, expected, str(source))
   expected[root] = (paragraphs, (date(2000, month, day) - date(2000, 10, 6)).days + 1)
  self.assertEqual(6, len(expected))
  papers = [e for e in self.data['Event'] if e['eventId'].startswith('newspaper.')]
  self.assertEqual(6, len(papers))
  self.assertEqual(set(expected), {e['eventId'] for e in papers})
  for event in papers:
   with self.subTest(root=event['eventId']):
    key = event['presentationKey']
    paragraphs, expected_day = expected[event['eventId']]
    self.assertEqual('Reading', event['presentationClass'])
    self.assertEqual('entry=MorningBeforeMap;blocksSave=true', event['eventProperty'])
    self.assertEqual(paragraphs[0], self.texts[event['titleText']])
    self.assertEqual(['Reading'], [p['presentationClass'] for p in self.data['PresentationIndex'] if p['presentationKey'] == key])
    pages = [p for p in self.data['ReadingPage'] if p['presentationKey'] == key]
    self.assertEqual(1, len(pages))
    lines = sorted((line for line in self.data['ReadingLine'] if line['pageId'] == pages[0]['pageId']), key=lambda line: line['seq'])
    self.assertEqual(paragraphs, [self.texts[line['textKey']] for line in lines])
    self.assertTrue(all(line['mode'] == 'narration' for line in lines))
    dates = [d for d in self.data['EventDate'] if d['eventId'] == event['eventId']]
    self.assertEqual([(expected_day, 'morning', 'once')], [(d['day'], d['timeSlot'], d['repeatRule']) for d in dates])
    self.assertFalse(self.pages_by_presentation[key])
    self.assertFalse(any(n['presentationKey'] == key for n in self.data['Notice']))

 def test_generic_church_helper_is_registered_only_when_source_uses_it(self):
  speakers = {speaker for g in self.map_groups for row in g['rows']
              if (prose := self._prose(row)) is not None
              and not row[0].startswith('选项 ') for speaker in [prose[0]]}
  used = '教会帮工' in speakers
  objects = [obj for obj in self.data.get('Object', []) if obj['objId'] == 'daily.actor.church_helper']
  self.assertEqual(1 if used else 0, len(objects))
  if used:
   self.assertEqual('npc', objects[0]['objType'])
   self.assertEqual('教会帮工', self.texts[objects[0]['nameText']])
   self.assertFalse(objects[0].get('portraitPath'))
   self.assertFalse(objects[0].get('iconPath'))

 def test_unmapped_source_speaker_fails_closed_instead_of_becoming_narration(self):
  # Simulate losing a mapping for a name really present in the source, without
  # editing the workbook, compiler, generated tables, or persistent state.
  speaker = next(speaker for g in self.map_groups for row in g['rows']
                 if (prose := self._prose(row)) is not None
                 and not row[0].startswith('选项 ') for speaker in [prose[0]] if speaker)
  with patch.dict(daily.SPEAKERS):
   daily.SPEAKERS.pop(speaker)
   with self.assertRaisesRegex(ValueError, 'Unmapped daily speaker.*' + re.escape(speaker)):
    daily.build(self.project_root)

 def test_public_bulletins_remain_notices_separate_from_news_reactions(self):
  headlines = {g['title'].split('｜')[1] for g in self.map_groups
               if g['sheet'] == '新闻反应' and '仪式' not in g['title']}
  bulletins = [e for e in self.data['Event'] if e['eventId'].startswith('news.bulletin.')]
  self.assertTrue(headlines)
  self.assertEqual(len(headlines), len(bulletins))
  self.assertEqual(headlines, {self.texts[e['titleText']] for e in bulletins})
  for event in bulletins:
   with self.subTest(root=event['eventId']):
    key = event['presentationKey']
    self.assertEqual('Notice', event['presentationClass'])
    self.assertEqual('entry=MorningBeforeMap;blocksSave=true', event['eventProperty'])
    self.assertEqual(['Notice'], [p['presentationClass'] for p in self.data['PresentationIndex'] if p['presentationKey'] == key])
    notices = [n for n in self.data['Notice'] if n['presentationKey'] == key]
    self.assertEqual(1, len(notices))
    self.assertEqual(self.texts[event['titleText']], self.texts[notices[0]['textKey']])
    self.assertTrue(self.texts[notices[0]['timeKey']])
    self.assertFalse(self.pages_by_presentation[key])
    self.assertFalse(any(p['presentationKey'] == key for p in self.data['ReadingPage']))

 def test_all_cafe_snapshots_keep_source_lines_and_nonmodal_visuals(self):
  groups = [g for g in self.source_groups if g['sheet'].startswith('咖啡馆')]
  self.assertEqual(30, len(groups))
  snapshots = {p['presentationId']: p for p in self.data['OpeningPresentation']}
  self.assertEqual(30, len(snapshots))
  for group in groups:
   kind = 'order' if group['sheet'] == '咖啡馆点单' else 'chat'
   key = 'cafe.ambient.' + kind + '.' + group['code'].lower()
   with self.subTest(snapshot=key):
    self.assertIn(key, snapshots)
    self.assertEqual('Dialogue/CafeSceneVisuals', snapshots[key]['visualsResource'])
    self.assertEqual('player.name', snapshots[key]['leftNameKey'])
    self.assertEqual('character.guest_a.name', snapshots[key]['rightNameKey'])
    expected = [(r[1] + '：' if r[1] else '') + r[2] for r in group['rows']
                if r[0] not in ('触发条件', '结果/配置') and r[2]]
    lines = sorted((r for r in self.data['OpeningLine'] if r['presentationId'] == key), key=lambda r: r['seq'])
    self.assertEqual(expected, [self.texts[r['textKey']] for r in lines])
    self.assertTrue(all(r['speaker'] == 'right' and r['gesture'] == 'none' for r in lines))
  self.assertFalse(any(e['eventId'].startswith('cafe.ambient.') for e in self.data['Event']))

if __name__=='__main__':unittest.main()
