"""Import the approved arrival chapter into Narrative.xlsx and Shared/Text.xlsx."""
from pathlib import Path
import re
import openpyxl
ROOT = Path(__file__).resolve().parent
SPEC = ROOT.parents[1] / 'Docs/FirstArrivalAndPassiveChecksSpec.md'
NARRATIVE_FILE = ROOT / 'Data/Narrative/Narrative.xlsx'
TEXT_FILE = ROOT / 'Data/Shared/Text.xlsx'
CHOICE_GROUPS = ('MA70.10.06-S1-1', 'MA70.10.06-S1-2', 'MA70.10.06-S1-3')

def configure():
    source = SPEC.read_text(encoding='utf-8').splitlines()
    book = openpyxl.load_workbook(NARRATIVE_FILE)
    texts = openpyxl.load_workbook(TEXT_FILE)
    def clear(sheet, predicate):
        for row in range(sheet.max_row, 4, -1):
            if predicate([cell.value for cell in sheet[row]]): sheet.delete_rows(row)
    clear(book['Event'], lambda r: str(r[1] or '').startswith('arrival.'))
    clear(book['SetupParams'], lambda r: str(r[1] or '').startswith('arrival.'))
    clear(book['PresentationIndex'], lambda r: str(r[1] or '').startswith('arrival.'))
    clear(book['ConversationPage'], lambda r: str(r[2] or '').startswith('arrival.'))
    clear(book['ConversationLine'], lambda r: str(r[2] or '').startswith('arrival.'))
    clear(book['AbilityCheck'], lambda r: str(r[1] or '').startswith('arrival.'))
    clear(book['AbilityCheckCondition'], lambda r: str(r[2] or '').startswith('arrival.'))
    clear(book['Choice'], lambda r: r[2] in CHOICE_GROUPS)
    clear(book['Result'], lambda r: str(r[2] or '').startswith('arrival.') or any(str(r[2] or '').startswith(g + '.') for g in CHOICE_GROUPS))
    clear(texts['Text'], lambda r: str(r[1] or '').startswith('arrival.') or any(str(r[1] or '').startswith(g + '.') for g in CHOICE_GROUPS))
    def put(sheet, key, **values):
        headers = [c.value for c in sheet[1]]
        row = next((r for r in range(5, sheet.max_row+1) if sheet.cell(r,2).value == key), sheet.max_row+1)
        sheet.cell(row,2,key)
        for field,value in values.items(): sheet.cell(row,headers.index(field)+1,value)
    put(book['Template'], 'Tpl_ArrivalConversation', descText='抵达剧情分页', interruptType='progressive', atomSeq='A_DIALOG,A_CHOICE', slotDef='opts:optionList', defaultSkip=False)
    put(book['Template'], 'Tpl_PassiveCheck', descText='静默属性检定', interruptType='progressive', atomSeq='A_NARRATE', slotDef='checkDisplay:text', defaultSkip=False)
    put(book['Template'], 'Tpl_NarrativeSignal', descText='无正文流程通知', interruptType='progressive', atomSeq='A_NARRATE', slotDef='', defaultSkip=False)
    put(book['SetupParams'], 'first_day_perception_check', tplName='Tpl_PassiveCheck', slotValues='checkDisplay=passive')
    for branch in ('success', 'failure'):
        put(book['SetupParams'], 'first_day_perception_'+branch, tplName='Tpl_NarrativeSignal', slotValues='')
    def text(key, value):
        s=texts['Text']
        row=next((n for n in range(5,s.max_row+1) if s.cell(n,2).value==key),s.max_row+1)
        s.cell(row,2,key); s.cell(row,3,value); s.cell(row,4,'FirstArrivalAndPassiveChecksSpec.md')
    def route(owner, target):
        put(book['Result'],owner+'.next',eventId=owner,seq=1,resultType='QUEUE_EVENT',target='event:'+target,value='')
    def pieces(raw, speaker):
        raw=raw.strip(); emote=''
        mood=re.match(r'（#([^）]+)）',raw)
        if mood: emote=mood.group(1); raw=raw[mood.end():]
        if not re.search(r'⌈|「', raw):
            return [(speaker, raw, emote)] if raw else []
        out=[]; cursor=0
        for match in re.finditer(r'⌈(.*?)⌋|「(.*?)」',raw):
            aside=raw[cursor:match.start()].strip()
            if aside: out.append(('',aside,''))
            spoken=match.group(1) if match.group(1) is not None else match.group(2)
            if spoken.strip(): out.append((speaker,spoken.strip(),emote))
            cursor=match.end()
        aside=raw[cursor:].strip()
        if aside: out.append(('',aside,''))
        if not out and raw: out.append((speaker,raw,emote))
        return out
    def event(key, paragraphs, group='', next_event='', default_bg='Art/Gameplay/DayFlow/phase_pre_business'):
        put(book['Event'],key,eventKind='world',actorId='world',titleText='arrival.title',setupKey=key,onceOnly=False,presentationClass='Conversation',presentationKey=key)
        put(book['SetupParams'],key,tplName='Tpl_ArrivalConversation',slotValues='opts='+group,skipAble=False,autoAdvance=False)
        put(book['PresentationIndex'],key,presentationClass='Conversation',logTag='FIRST_ARRIVAL')
        pages={}
        for paragraph in paragraphs:
            raw=source[paragraph-1].strip() if isinstance(paragraph,int) else paragraph
            speaker=''
            if raw.startswith('{playername}：'): speaker='player'; raw=raw.split('：',1)[1]
            elif raw.startswith('阿黛尔·米尼耶：'): speaker='adele'; raw=raw.split('：',1)[1]
            # Stage directions are presented as narration, never merged into spoken text.
            for item in pieces(raw,speaker):
                page=key+'.page'
                if key=='arrival.start': page='arrival.start.street' if paragraph<=186 else 'arrival.start.cafe'
                elif key=='arrival.accept': page='arrival.accept.welcome' if paragraph<=282 else 'arrival.accept.malice'
                pages.setdefault(page,[]).append(item)
        for page_seq,(page,items) in enumerate(pages.items(),1):
            background='Art/Gameplay/DayFlow/city_map_placeholder' if page.endswith('.street') else 'Art/Gameplay/DayFlow/phase_night_choice' if page.endswith('.malice') else default_bg
            put(book['ConversationPage'],page,presentationKey=key,seq=page_seq,optionGroupKey=group if page_seq==len(pages) else '',bgOverride=background)
            for seq,(speaker,spoken,emote) in enumerate(items,1):
                line_id=f'{key}.{page_seq:02}.{seq:02}'
                text(line_id,spoken)
                put(book['ConversationLine'],line_id,pageId=page,seq=seq,speakerKey=speaker,side='left' if speaker=='player' else 'right',textKey=line_id,emote=emote)
        if next_event: route(key,next_event)
    def choice(group, seq, label, target):
        key=group+'.'+str(seq); text(key,label)
        put(book['Choice'],key,groupKey=group,textKey=key,resultOwnerId=key,seq=seq)
        route(key,target)
    text('arrival.title','初来乍到')
    event('arrival.start',[180,182,184,186,190,192,194,196,198,200,204,206,208],'MA70.10.06-S1-1',default_bg='Art/Gameplay/DayFlow/city_map_placeholder')
    event('arrival.reply.1',['{playername}：「你们这咖啡馆真是......颇具特色......」'],next_event='arrival.agent')
    event('arrival.reply.2',['{playername}：「额......这咖啡馆真在营业吗？」'],next_event='arrival.agent')
    choice('MA70.10.06-S1-1',1,'你们这咖啡馆真是......颇具特色......','arrival.reply.1')
    choice('MA70.10.06-S1-1',2,'额......这咖啡馆真在营业吗？','arrival.reply.2')
    event('arrival.agent',[216,218,220,222],next_event='arrival.check')
    put(book['Event'],'arrival.check',eventKind='world',actorId='world',setupType='Tpl_Narration',setupKey='arrival.check',onceOnly=False)
    put(book['SetupParams'],'arrival.check',tplName='Tpl_PassiveCheck',slotValues='checkDisplay=passive',checkId='arrival.perception')
    put(book['AbilityCheck'],'arrival.perception',successEventId='arrival.insight',failureEventId='arrival.request')
    put(book['AbilityCheckCondition'],'arrival.perception.1',checkId='arrival.perception',attributeId='perception',threshold=3,groupIndex=1,seq=1)
    event('arrival.insight',[226],next_event='arrival.request')
    event('arrival.request',[230,232,234,236],next_event='arrival.decision')
    event('arrival.decision',['要接手沸点咖啡馆吗？'],'MA70.10.06-S1-2')
    choice('MA70.10.06-S1-2',1,'强硬拒绝','arrival.refuse')
    choice('MA70.10.06-S1-2',2,'接受委托','arrival.accept')
    event('arrival.refuse',[246,248,250,252,254,256,258,'未开始就已结束',262],'MA70.10.06-S1-3')
    choice('MA70.10.06-S1-3',1,'重新抉择','arrival.decision')
    choice('MA70.10.06-S1-3',2,'就此别离','arrival.leave')
    put(book['Event'],'arrival.leave',eventKind='world',actorId='world',setupType='Tpl_Narration',onceOnly=False)
    event('arrival.accept',[272,274,276,278,280,282,284,288,290,292,294,296],next_event='arrival.complete')
    put(book['Event'],'arrival.complete',eventKind='world',actorId='world',setupType='Tpl_Narration',onceOnly=False)
    book.save(NARRATIVE_FILE); texts.save(TEXT_FILE)
    print('Configured unique NCS entry arrival.start and 13 ordered event nodes.')

if __name__ == '__main__': configure()
