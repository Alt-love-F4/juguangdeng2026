"""Append B1011 post-brew isolated source stage; never enables formal brewing.
Default validates a memory plan. --apply uses common lock/CAS/backups.
P046/P047 remain omitted until the real order name is defined. Correct Q4
requires an explicit isolated wisdom-passed fixture; failure route is unknown.
"""
import argparse
from collections import Counter
from copy import copy
from datetime import datetime
import json
import re
import shutil
import openpyxl
from openpyxl.comments import Comment
import import_rose_text as source
from rose_write_guard import source_write_guard

ROOT=source.ROOT
PATH=ROOT/'Assets/ConfigTables/Data/Narrative/Narrative.xlsx'
EVENT='rose.1011.s1'
PRESENTATION=EVENT+'.conversation'
PRE='dependency:B1011.stage.pre_order'
POST='dependency:B1011.stage.post_brew'
LONG='choice:'+EVENT+'.choice.p080|dependency:B1011.wisdom-passed'
POLITICS='dependency:B1011.politics'
ORDER=['brew_low','nature_choice','nature_ordinary','nature_not_person','early_end','reason_title','history_yes','history_no','history_lecture','portrait_memory','history_return','card_notice','card_read','history_recognition','full_end']


def build():
    entries=source.expected_rows('1011')
    rows=[]
    def add(sheet,**v): rows.append((sheet,v))
    def choice(n): return 'choice:'+EVENT+'.choice.p'+str(n).zfill(3)
    def anychoice(ns): return 'anyChoice:'+';'.join(EVENT+'.choice.p'+str(n).zfill(3) for n in ns)
    guards={'brew_low':'dependency:B1011.low-grade','nature_ordinary':anychoice([60,61]),'nature_not_person':choice(62),
            'early_end':anychoice([60,61,79,81]),'reason_title':LONG,'history_yes':LONG+'|'+POLITICS,'history_no':LONG+'|not:'+POLITICS,
            'history_recognition':LONG+'|'+POLITICS}
    groups={'nature_choice':'nature','nature_not_person':'reason'}
    for i,short in enumerate(ORDER,11):
        guard=guards.get(short,LONG if i>=16 else '')
        v=dict(pageId=EVENT+'.'+short,presentationKey=PRESENTATION,seq=i,pageCondKeys=POST+('|' +guard if guard else ''))
        if short in groups: v['optionGroupKey']=EVENT+'.choice.'+groups[short]
        if short=='history_lecture':v['bgOverride']='rose.history.portrait'
        if short in ('brew_low','history_return'):v['bgOverride']='oct07.cafe.day'
        if short=='portrait_memory':v.update(stepType='PassiveCheck',checkId=EVENT+'.check.portrait')
        if short=='card_read':v.update(stepType='Reading',readingKey=EVENT+'.card')
        add('ConversationPage',**v)
    seqs=Counter()
    for e in entries:
        n=int(e['source'].split('P')[-1]);short=e['page'].rsplit('.',1)[-1]
        if n<50 or e['kind'] in ('选项','名片正文'):continue
        assert short in ORDER and short!='card_read'
        raw=e['original'].strip()
        speaker=next((k for prefix,k in [('{playername}','player'),('阿黛尔','adele'),('麻雀','sparrow'),('西尔万','sylvain')] if raw.startswith(prefix)),None)
        # Mixed source P052 alternates speech and narration. Other mixed lines
        # are adjacent quotations by the original speaker.
        if n==52 and e['key'].endswith(('.b','.d','.f')):speaker=None
        emote=None
        m=re.search(r'[（(]#([^）)]+)[）)]',raw)
        if m and speaker:emote=m[1]
        if n==116:emote='兴奋' if e['key'].endswith('.b') else None
        seqs[e['page']]+=1
        v=dict(lineId=e['key'].replace('.text.','.'),pageId=e['page'],seq=seqs[e['page']],side='none',textKey=e['key'])
        if speaker:v['speakerKey']=speaker
        if emote:v['emote']=emote
        add('ConversationLine',**v)
    for group,ns in [('nature',[60,61,62]),('reason',[79,80,81])]:
        for seq,n in enumerate(ns,1):add('Choice',choiceId=EVENT+'.choice.p'+str(n).zfill(3),groupKey=EVENT+'.choice.'+group,textKey=EVENT+'.text.p'+str(n).zfill(3),seq=seq)
    add('AbilityCheck',checkId=EVENT+'.check.portrait',mode='Inline',comment='P111 perception >4; isolated source stage')
    add('AbilityCheckCondition',conditionId=EVENT+'.check.portrait.1',checkId=EVENT+'.check.portrait',attributeId='perception',threshold=5,groupIndex=1,seq=1)
    add('PresentationIndex',presentationKey=EVENT+'.card',presentationClass='Reading',logTag='B1011 card, not an inventory reward')
    add('ReadingPage',pageId=EVENT+'.card.page',presentationKey=EVENT+'.card',seq=1)
    for seq,n in enumerate([128,129,130],1):add('ReadingLine',lineId=EVENT+'.p'+str(n).zfill(3),pageId=EVENT+'.card.page',seq=seq,mode='narration',textKey=EVENT+'.text.p'+str(n).zfill(3))
    return entries,rows


def prepare(w,entries,rows):
    changes=[];counts=Counter()
    for sheet in ['Result','EventDate','CafeVisitHook']:
        ws=w[sheet]
        assert not any(any(EVENT in str(c.value) for c in row if c.value) for row in ws.iter_rows(min_row=5)), 'Root must remain result/entry-free'
    ws=w['Event'];h={c.value:c.column for c in ws[1]};r=next(r[0].row for r in ws.iter_rows(min_row=5) if r[1].value==EVENT)
    cell=ws.cell(r,h['eventProperty']);old='entry=Immediate;blocksSave=true;blockedBy=B-B03+B-B04+B-B05+B-C02+B-C04;contentBoundary=P045'
    new=old.replace('contentBoundary=P045','contentBoundary=isolated-stages;missingOrder=P046-P048')
    assert cell.value in (old,new),'Unexpected B root properties'
    if cell.value!=new:changes.append((ws.title,cell.coordinate,cell.value,new));cell.value=new
    ws=w['ConversationPage'];h={c.value:c.column for c in ws[1]}
    for row in ws.iter_rows(min_row=5):
        if row[1].value and str(row[1].value).startswith(EVENT+'.') and ws.cell(row[0].row,h['seq']).value<=10:
            cell=ws.cell(row[0].row,h['pageCondKeys']);v=cell.value or ''
            if not v.startswith(PRE):
                new=PRE+('|' +v if v else '');changes.append((ws.title,cell.coordinate,cell.value,new));cell.value=new
    for sheet,values in rows:
        ws=w[sheet];h={c.value:c.column for c in ws[1]};assert set(values)<=set(h),f'Header missing {sheet}'
        key=values[ws.cell(1,2).value];matches=[r[0].row for r in ws.iter_rows(min_row=5) if r[1].value==key]
        assert len(matches)<=1,f'Duplicate {sheet}/{key}'
        if matches:
            for k,v in values.items():assert ws.cell(matches[0],h[k]).value==v,f'Existing different {sheet}/{key}/{k}'
            continue
        r=ws.max_row+1
        for col in range(1,ws.max_column+1):ws.cell(r,col)._style=copy(ws.cell(5,col)._style)
        for k,v in values.items():ws.cell(r,h[k],v)
        ws.cell(r,2).comment=Comment('B1011 isolated post-brew source mapping; no real brewing or completed fact','B-rose')
        counts[sheet]+=1
    return counts,changes


def run(apply=False):
    entries,rows=build();initial=source.digest(PATH);text_hash=source.digest(source.TEXT)
    t=openpyxl.load_workbook(source.TEXT,read_only=True)
    try:texts={r[1].value:r[2].value for r in t['Text'].iter_rows(min_row=5) if r[1].value}
    finally:t.close()
    assert all(texts.get(e['key'])==e['text'] for e in entries),'Source Text changed'
    w=openpyxl.load_workbook(PATH)
    try:
        before={(s.title,c.coordinate):source.signature(c) for s in w for row in s for c in row}
        added,changes=prepare(w,entries,rows)
        manifest=dict(event=EVENT,added=dict(added),migrations=changes,writes=apply,formal_enabled=False,missing='P046 real order; brewing/grade map; wisdom threshold and failure route; formal outcomes')
        if not apply or not (added or changes):print(json.dumps(manifest,ensure_ascii=False,indent=2));return
        out=ROOT/'Library/Locus/tmp/rose-content'/(datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'-1011-postbrew');out.mkdir(parents=True)
        assert source.digest(PATH)==initial
        shutil.copy2(PATH,out/'Narrative.before.xlsx');assert source.digest(out/'Narrative.before.xlsx')==initial
        candidate=out/'Narrative.candidate.xlsx';w.save(candidate);saved=openpyxl.load_workbook(candidate)
        allowed={(s,c) for s,c,old,new in changes}
        try:
            for (s,c),v in before.items():
                if (s,c) not in allowed:assert source.signature(saved[s][c])==v,f'Existing cell altered {s}/{c}'
        finally:saved.close()
        manifest.update(before_sha256=initial,text_sha256=text_hash)
        (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        assert source.digest(PATH)==initial and source.digest(source.TEXT)==text_hash,'Concurrent source changed; candidate preserved'
        candidate.replace(PATH);print(json.dumps({**manifest,'evidence':str(out)},ensure_ascii=False,indent=2))
    finally:w.close()


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--apply',action='store_true');a=p.parse_args()
    if a.apply:
        with source_write_guard('B1011 isolated postbrew source'):run(True)
    else:run()
