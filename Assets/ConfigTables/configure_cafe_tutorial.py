"""Import the approved cafe manuscript into source workbooks; run GenerateAll.bat afterwards.
Paragraph indices are zero-based document body paragraphs, including blank paragraphs.
No prose correction or gameplay implementation is performed here.
"""
from pathlib import Path
import re
import zipfile
import xml.etree.ElementTree as ET
import openpyxl

ROOT = Path(__file__).resolve().parent
NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

def configure():
    with zipfile.ZipFile(ROOT.parent / '文案-新手教程.docx') as archive:
        tree = ET.fromstring(archive.read('word/document.xml'))
    paragraphs = [''.join(t.text or '' for t in p.findall('.//w:t', NS))
                  for p in tree.findall('.//w:body/w:p', NS)]
    narrative = openpyxl.load_workbook(ROOT / 'Data/Narrative/Narrative.xlsx')
    text = openpyxl.load_workbook(ROOT / 'Data/Shared/Text.xlsx')
    tw = text['Text']
    def put(key, value, comment='文案-新手教程.docx；原文保留'):
        for row in tw.iter_rows(min_row=5):
            if row[1].value == key:
                row[2].value = value
                row[3].value = comment
                return
        tw.append([None, key, value, comment])
    def clear_rows(sheet, predicate):
        for i in range(sheet.max_row, 4, -1):
            if predicate([c.value for c in sheet[i]]): sheet.delete_rows(i)
    presentations = narrative['OpeningPresentation']
    lines = narrative['OpeningLine']
    clear_rows(presentations, lambda r: str(r[1]).startswith('cafe_tutorial.'))
    clear_rows(lines, lambda r: r[2] == 'first_day_cafe' or str(r[2]).startswith('cafe_tutorial.'))
    clear_rows(tw, lambda r: str(r[1]).startswith('opening.line.') or str(r[1]).startswith('cafe.tutorial.'))
    for i, value in enumerate(['centerNameKey', 'string', 'c', '第三角色显示名'], 1): presentations.cell(i, 7, value)
    put('cafe.tutorial.customer.name', '顾客B')
    put('opening.title', '重新启航')
    steps = []
    opening_line_seq = 0
    tutorial_line_seq = 0
    def segment(step, indices, next_step='', focus='', optional=False, title=None):
        nonlocal opening_line_seq, tutorial_line_seq
        pid = 'first_day_cafe' if step == 'introduction' else 'cafe_tutorial.' + step
        title_key = 'opening.title' if step == 'introduction' else 'cafe.tutorial.' + step + '.title'
        if step != 'introduction': put(title_key, title or step)
        existing = any(row[1] == pid for row in presentations.iter_rows(min_row=5, values_only=True))
        if not existing:
            presentations.append([None, pid, title_key, 'opening.player.name', 'opening.adele.name',
                                  'Dialogue/FirstDayIntroductionVisuals', 'cafe.tutorial.customer.name'])
        seq = 0
        for index in indices:
            raw = paragraphs[index] if isinstance(index, int) else index
            speaker = 'narrator'
            if raw.startswith('阿黛尔·米尼耶：'): speaker, raw = 'right', raw.split('：', 1)[1]
            elif raw.startswith('{playername}：'): speaker, raw = 'left', raw.split('：', 1)[1]
            elif raw.startswith('顾客B：'): speaker, raw = 'center', raw.split('：', 1)[1]
            # Stage directions remain in the source text registry, not spoken dialogue.
            spoken = re.sub(r'^（#?[^）]+）', '', raw)
            parts = re.findall('「([^」]*)」', spoken)
            if not parts: parts = [spoken]
            for part in parts:
                seq += 1
                if step == 'introduction':
                    opening_line_seq += 1
                    line_id = f'opening_{opening_line_seq:03}'
                    key = f'opening.line.{opening_line_seq:02}'
                else:
                    tutorial_line_seq += 1
                    line_id = f'cafe_tutorial_{tutorial_line_seq:03}'
                    key = f'cafe.tutorial.{step}.{seq:02}'
                put(key, part, f'原稿段落 {index}' if isinstance(index, int) else '原稿枚举展开；未添加玩法')
                lines.append([None, line_id, pid, seq, speaker, key, 'none', 0])
        steps.append([None, step, 'first_cafe_tutorial', pid, focus,
                      'workstation_opened' if step == 'workstation_open' else '', next_step, optional])
    segment('introduction', list(range(9,14)), 'customer_arrives')
    segment('customer_arrives', list(range(15,21)), 'perception_check')
    segment('perception_success', [22,23,25,26,27,28,29], 'order_confirmed')
    segment('perception_failure', [31,32,33], 'order_confirmed')
    segment('order_confirmed', [35,36,37,38], 'workstation_open')
    segment('workstation_open', [40], 'make_coffee_base', 'cafe.workstation')
    segment('make_coffee_base', [45,46,51], 'pour_into_cup', 'cafe.coffee_base')
    segment('pour_into_cup', [51], 'adjust_strength', 'cafe.cup')
    segment('adjust_strength', [52], 'adjust_sweetness', 'cafe.strength')
    segment('adjust_sweetness', [52,53], 'serve_customer', 'cafe.sweetness')
    for key, label in [('grind','研磨'),('boil','烧水'),('brew','冲泡'),('cup','装杯'),('base','基底'),('strength','调浓度'),('sweetness','甜度'),('serve','出餐')]:
        segment('demo_' + key, [label], optional=True, title=label)
    segment('serve_customer', [67], 'result', 'cafe.delivery')
    for key,label,reward in [('excellent','独一无二','金钱+200'),('good','滴滴香浓','金钱+150'),('average','中庸之道','金钱+50'),('bad','食不知味','金钱+10'),('refund','好想退钱','金钱-200，打回重做')]:
        segment('result_' + key, ['顾客B：「这个咖啡嘛……简直就是' + label + '！」', reward], 'make_coffee_base' if key == 'refund' else 'ending', title=label)
    segment('ending', [70,71,73,74,75,76])
    # Preserve all author directions, duplicate order copy and ambiguous sentences for traceability.
    for i, paragraph in enumerate(paragraphs):
        if paragraph: put(f'cafe.tutorial.source.p{i:03}', paragraph, '原稿完整归档（含舞台说明）；不自动播放')
    if 'CafeTutorialStep' in narrative: del narrative['CafeTutorialStep']
    ws = narrative.create_sheet('CafeTutorialStep')
    ws.append(['##var','stepId','tutorialId','presentationId','focusTargetKey','completionSignalId','nextStepId','optionalDemo'])
    ws.append(['##type','string','string','string','string','string','string','bool'])
    ws.append(['##group'] + ['c'] * 7)
    ws.append(['##','步骤','教程','NCS段落','焦点','完成信号','后续步骤或分支标记','可选演示'])
    for row in steps: ws.append(row)
    # Existing NCS ability check owns selection; branch events become silent routing events.
    for row in narrative['Event'].iter_rows(min_row=5):
        if row[1].value in ('EV_FIRST_DAY_PERCEPTION_SUCCESS','EV_FIRST_DAY_PERCEPTION_FAILURE'):
            row[6].value = None
    for row in narrative['SetupParams'].iter_rows(min_row=5):
        if row[1].value in ('first_day_perception_success','first_day_perception_failure'): row[3].value = None
    narrative.save(ROOT / 'Data/Narrative/Narrative.xlsx')
    text.save(ROOT / 'Data/Shared/Text.xlsx')
    print(f'Configured {len(steps)} tutorial segments; manuscript archive preserved.')

if __name__ == '__main__': configure()
