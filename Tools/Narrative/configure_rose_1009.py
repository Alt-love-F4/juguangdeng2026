"""Configure blocked 1009 P003-P071 using existing NCS sources; no formal mount/results."""
from copy import copy
from datetime import datetime
import hashlib
import import_rose_text as source
import openpyxl
from openpyxl.comments import Comment

root = source.ROOT
paths = [root/'Assets/ConfigTables/Data/Narrative/Narrative.xlsx', source.TEXT]
initial = {p: source.digest(p) for p in paths}
books = {p: openpyxl.load_workbook(p) for p in paths}
n, t = (books[p] for p in paths)
# The concurrent schema now requires this optional named-speaker column; add only
# the missing workbook column with the project header convention.
line_sheet = n['ConversationLine']
line_headers = [c.value for c in line_sheet[1]]
if 'speakerNameKey' not in line_headers:
    col = line_sheet.max_column + 1
    line_sheet.cell(1, col, 'speakerNameKey')
    line_sheet.cell(2, col, 'string')
    line_sheet.cell(3, col, 'c')
    line_sheet.cell(4, col, '可选显示名Text key')
    for row in range(1, 5): line_sheet.cell(row, col)._style = copy(line_sheet.cell(row, 12)._style)
entries = source.expected_rows('1009')
originals = {r[0]: r[4] for r in source.source_rows('1009')}

def append(book, sheet, values):
    ws = book[sheet]
    headers = [c.value for c in ws[1]]
    keyfield = headers[1]
    matches = [row for row in ws.iter_rows(min_row=5) if row[1].value == values[keyfield]]
    if matches:
        row = matches[0]
        # This row was created by the previous run of this same staging tool;
        # repair only its invalid enum value after the official validator reported it.
        if sheet == 'Event' and values[keyfield] == 'rose.1009.s1' and row[headers.index('eventKind')].value == 'narrative':
            row[headers.index('eventKind')].value = 'actor'
        assert all(row[headers.index(k)].value == v for k,v in values.items()), f'Existing different row {sheet}/{values[keyfield]}'
        return
    number = ws.max_row + 1
    for c in range(1, ws.max_column+1):
        ws.cell(number,c)._style = copy(ws.cell(5,c)._style)
    for k,v in values.items(): ws.cell(number,headers.index(k)+1,v)

for key,text in [('rose.1009.s1.title','赴约'),('rose.envoy.name','老爷'),('rose.butake.name','男人')]:
    append(t,'Text',dict(key=key,text=text,comment='B1009 source-mapped blocked construction; not formally enabled'))
for key in ['rose.envoy','rose.butake']:
    append(n,'Object',dict(objId=key,objType='npc',nameText=key+'.name'))
append(n,'Event',dict(eventId='rose.1009.s1',eventKind='actor',actorId='player',titleText='rose.1009.s1.title',setupType='Tpl_ArrivalConversation',setupKey='rose.1009.s1',onceOnly=True,presentationClass='Conversation',presentationKey='rose.1009.s1.conversation',eventProperty='entry=MapAction;blocksSave=true;blockedBy=B-B01+B-B02;contentBoundary=P071'))
append(n,'SetupParams',dict(setupKey='rose.1009.s1',tplName='Tpl_ArrivalConversation',skipAble=False,autoAdvance=False))
append(n,'PresentationIndex',dict(presentationKey='rose.1009.s1.conversation',presentationClass='Conversation',logTag='B1009 blocked P003-P071'))
pages=['door','blindfold','reception','origin_letter','origin_uncle','request']
for seq,page in enumerate(pages,1):
    values=dict(pageId='rose.1009.s1.'+page,presentationKey='rose.1009.s1.conversation',seq=seq)
    if page=='door': values['bgOverride']='rose.club.exterior'
    if page=='blindfold': values['bgOverride']='rose.club.blindfold'
    if page=='reception': values['bgOverride']='rose.club.reception'
    if page=='reception': values['optionGroupKey']='rose.1009.s1.choice.origin'
    if page.startswith('origin_'): values['pageCondKeys']='choice:rose.1009.s1.choice.p'+('045' if page=='origin_letter' else '046')
    append(n,'ConversationPage',values)
seqs={}
for entry in entries:
    number=int(entry['source'].split('P')[-1])
    if number>=72 or entry['kind']=='选项': continue
    page=entry['page']; seqs[page]=seqs.get(page,0)+1
    raw=originals[number].strip()
    speaker=''
    if raw.startswith('{playername}'): speaker='player'
    elif raw.startswith(('男人','布塔克')) and '：' in raw[:8]: speaker='rose.butake'
    elif raw.startswith('老爷：'): speaker='rose.envoy'
    if number in [38,43,51] and entry['key'].endswith('.b'): speaker=''
    values=dict(lineId=entry['key'].replace('.text.','.'),pageId=page,seq=seqs[page],side='none',textKey=entry['key'],speakerNameKey=None)
    if speaker: values['speakerKey']=speaker
    append(n,'ConversationLine',values)
for seq,number in enumerate([45,46],1):
    append(n,'Choice',dict(choiceId=f'rose.1009.s1.choice.p{number:03}',groupKey='rose.1009.s1.choice.origin',textKey=f'rose.1009.s1.text.p{number:03}',seq=seq))
# Existing authoritative upstream condition, without inventing a completed flag.
append(n,'Condition',dict(conditionId='rose.1009.s1.day',ownerType='event',ownerId='rose.1009.s1',groupIndex=1,condType='BAR_BETWEEN',target='world.calendar',value='4,4'))
append(n,'Condition',dict(conditionId='rose.1009.s1.meeting',ownerType='event',ownerId='rose.1009.s1',groupIndex=1,condType='BOOL_EQ',target='story.oct09.meeting.known',value='true'))
stamp=datetime.now().strftime('%Y%m%d-%H%M%S')
out=root/'Library/Locus/tmp/rose-content'/f'{stamp}-1009-wire';out.mkdir(parents=True)
for p in paths:
    import shutil
    shutil.copy2(p,out/(p.stem+'.before.xlsx'))
    books[p].save(out/p.name)
assert all(source.digest(p)==initial[p] for p in paths),'Concurrent workbook modification; candidates preserved'
for p in paths: (out/p.name).replace(p)
print('Configured 6 pages, 62 lines, 2 choices, blocked root with no Action/Date/Result. Backup:',out)
