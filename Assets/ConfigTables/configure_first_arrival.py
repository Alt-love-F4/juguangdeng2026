"""Import FirstArrivalAndPassiveChecksSpec appendix into the existing NCS workbook."""
from pathlib import Path
import re
import openpyxl
ROOT = Path(__file__).resolve().parent

def configure():
    source = (ROOT.parents[1] / 'Docs/FirstArrivalAndPassiveChecksSpec.md').read_text(encoding='utf-8').splitlines()
    book = openpyxl.load_workbook(ROOT / 'Data/Narrative/Narrative.xlsx')
    texts = openpyxl.load_workbook(ROOT / 'Data/Shared/Text.xlsx')
    def put(sheet, key, **values):
        headers = [c.value for c in sheet[1]]
        row = next((r for r in range(5, sheet.max_row+1) if sheet.cell(r,2).value == key), sheet.max_row+1)
        sheet.cell(row,2,key)
        for field,value in values.items(): sheet.cell(row,headers.index(field)+1,value)
    def text(key, value):
        s=texts['Text']; row=next((r for r in range(5,s.max_row+1) if s.cell(r,2).value==key),s.max_row+1)
        s.cell(row,2,key); s.cell(row,3,value)
    def route(owner, target):
        put(book['Result'],owner+'.next',eventId=owner,seq=1,resultType='QUEUE_EVENT',target=target,value='')
    def event(key, indices, group='', next_event=''):
        put(book['Event'],key,eventKind='world',actorId='world',titleText='arrival.title',setupKey=key,onceOnly=False,presentationClass='Conversation',presentationKey=key)
        put(book['SetupParams'],key,tplName='Tpl_TwoPersonTalk',slotValues=('opts='+group) if group else '',skipAble=False,autoAdvance=False)
        put(book['PresentationIndex'],key,presentationClass='Conversation',logTag='FIRST_ARRIVAL')
        put(book['ConversationPage'],key+'.page',presentationKey=key,seq=1,optionGroupKey=group)
        for seq,index in enumerate(indices,1):
            raw=source[index-1].strip() if isinstance(index,int) else index
            speaker=''; emote=''
            if raw.startswith('{playername}：'): speaker='player'; raw=raw.split('：',1)[1]
            elif raw.startswith('阿黛尔·米尼耶：'): speaker='adele'; raw=raw.split('：',1)[1]
            match=re.match(r'（#([^）]+)）',raw)
            if match: emote=match[1]; raw=raw[match.end():]
            raw=raw.replace('⌈','').replace('⌋','')
            tid=f'{key}.{seq:02}'
            text(tid,raw)
            put(book['ConversationLine'],tid,pageId=key+'.page',seq=seq,speakerKey=speaker,side='left' if speaker=='player' else 'right',textKey=tid,emote=emote)
        if next_event: route(key,next_event)
    def choice(group, seq, label, target):
        key=group+'.'+str(seq); text(key,label)
        put(book['Choice'],key,groupKey=group,textKey=key,resultOwnerId=key,seq=seq)
        route(key,target)
    text('arrival.title','初来乍到')
    event('arrival.start',list(range(180,188,2))+list(range(190,202,2))+[204,206,208], 'MA70.10.06-S1-1')
    event('arrival.reply.1',['你们这咖啡馆真是......颇具特色......'],next_event='arrival.agent')
    event('arrival.reply.2',['额......这咖啡馆真在营业吗？'],next_event='arrival.agent')
    # Selected replies are player speech.
    for key in ['arrival.reply.1.01','arrival.reply.2.01']:
        put(book['ConversationLine'],key,speakerKey='player',side='left')
    choice('MA70.10.06-S1-1',1,'你们这咖啡馆真是......颇具特色......','arrival.reply.1')
    choice('MA70.10.06-S1-1',2,'额......这咖啡馆真在营业吗？','arrival.reply.2')
    event('arrival.agent',[216,218,220,222],next_event='arrival.check')
    put(book['Event'],'arrival.check',eventKind='world',actorId='world',setupKey='arrival.check',setupType='Tpl_Narration',onceOnly=False)
    put(book['SetupParams'],'arrival.check',tplName='Tpl_Narration',slotValues='checkDisplay=passive',checkId='arrival.perception')
    put(book['AbilityCheck'],'arrival.perception',successEventId='arrival.insight',failureEventId='arrival.request')
    put(book['AbilityCheckCondition'],'arrival.perception.1',checkId='arrival.perception',attributeId='perception',threshold=3,groupIndex=1,seq=1)
    event('arrival.insight',[226],next_event='arrival.request')
    event('arrival.request',[230,232,234,236],next_event='arrival.decision')
    event('arrival.decision',['要接手沸点咖啡馆吗？'],'MA70.10.06-S1-2')
    choice('MA70.10.06-S1-2',1,'强硬拒绝','arrival.refuse')
    choice('MA70.10.06-S1-2',2,'接受委托','arrival.accept')
    event('arrival.refuse',list(range(246,264,2)),'MA70.10.06-S1-3')
    choice('MA70.10.06-S1-3',1,'重新抉择','arrival.decision')
    choice('MA70.10.06-S1-3',2,'就此别离','arrival.leave')
    put(book['Event'],'arrival.leave',eventKind='world',actorId='world',setupType='Tpl_Narration',onceOnly=False)
    event('arrival.accept',[272,274,276,278,280,282,284,288,290,292,294,296],next_event='arrival.complete')
    put(book['Event'],'arrival.complete',eventKind='world',actorId='world',setupType='Tpl_Narration',onceOnly=False)
    book.save(ROOT / 'Data/Narrative/Narrative.xlsx'); texts.save(ROOT / 'Data/Shared/Text.xlsx')

if __name__ == '__main__': configure()
