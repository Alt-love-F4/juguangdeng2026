"""Compile the current 270 daily groups and six newspapers into existing NCS sheets.
Read-only source extraction. Source workbook writes are centralized in integrate_content.py.
"""
from __future__ import annotations
from pathlib import Path
from collections import defaultdict
from datetime import date
from hashlib import sha256
from zipfile import ZipFile
import re
import xml.etree.ElementTree as ET
import openpyxl

LOCATION = {
 '旧城区': ('旧城区','courage','old.street.day','修旧宅楼梯'),
 '圣徒街': ('圣徒街','language','street.day','送店里的印件'),
 '圣西尔万郊区': ('圣西尔万郊区','wisdom','suburb.day','修工坊外的板车'),
 '大堂区': ('market','courage','market.day','采购并理货'),
 '圣地大教堂': ('圣地大教堂','perception','cathedral.day','帮施舍厨房'),
 '灰墙学院': ('library','wisdom','street.day','参加公开读书会'),
 '市政厅': ('市政厅','perception','street.day','办咖啡馆手续'),
 '首都法院': ('首都法院','language','street.day','帮欧仁整理申诉'),
 '花园区': ('花园区','perception','street.day','帮诊所理货'),
}
SPEAKERS={'{playername}':'player','阿黛尔':'adele','麻雀':'sparrow','柯莱特':'colette','欧仁':'eugene','西尔万':'sylvain','帕斯卡尔':'pascal','萨德':'sade','埃马纽埃尔':'sorel','索雷尔':'sorel','勒费弗尔':'lefebvre','顾客':'guest_a','教会帮工':'daily.actor.church_helper'}
NODE=re.compile(r'^[A-Z]+-(?:R)?\d{2}$')

def day(month:int, d:int): return (date(2000,month,d)-date(2000,10,6)).days+1
DATE_CONSTRAINT = re.compile(r'(?P<kind>窗口|排除|避开|不在)\s*[:：]?\s*(?P<m1>10|11)月(?P<d1>\d{1,2})日?\s*(?:[—–-]\s*(?:(?P<m2>10|11)月)?(?P<d2>\d{1,2})日?)?')
DATE_TOKEN = re.compile(r'(?P<month>10|11)月(?P<day>\d{1,2})日?')
ISSUE_DAYS = {'一':'1007','二':'1014','三':'1021','四':'1028','五':'1104','六':'1111'}
SOURCE_PATH = '策划文件/文案6点/文案/b-地图事件-日常/日常对话_报纸联动修订版_20261019.xlsx'
SOURCE_SHA256 = '026d6dd5684aee7f1be92ceabd66eaa970453a5a9e564ca4b08c8e1a5925e7dd'

def date_rules(constraint):
 windows=[]; exclusions=[]; consumed=[]
 for match in DATE_CONSTRAINT.finditer(constraint):
  kind=match.group('kind'); m1=int(match.group('m1')); d1=int(match.group('d1'))
  m2=int(match.group('m2') or m1); d2=int(match.group('d2') or d1)
  (windows if kind=='窗口' else exclusions).append((day(m1,d1),day(m2,d2)))
  consumed.append(match.span())
 earliest=[]
 for match in DATE_TOKEN.finditer(constraint):
  if any(start <= match.start() < end for start,end in consumed): continue
  earliest.append(day(int(match.group('month')),int(match.group('day'))))
 return windows,exclusions,max(earliest) if earliest else None

def available_date_ranges(start,end,excluded):
 ranges=[(start,end)]
 for lower,upper in sorted(excluded):
  next_ranges=[]
  for left,right in ranges:
   if upper < left or lower > right:
    next_ranges.append((left,right));continue
   if left < lower: next_ranges.append((left,lower-1))
   if upper < right: next_ranges.append((upper+1,right))
  ranges=next_ranges
 return ranges

def add_date_window(data,condition,root,segments):
 if not segments: return
 if len(segments)==1:
  condition(root,'BAR_BETWEEN','world.calendar',f'{segments[0][0]},{segments[0][1]}')
  return
 existing=[row for row in data['Condition'] if row['ownerType']=='event' and row['ownerId']==root]
 existing_ids={id(row) for row in existing}
 data['Condition']=[row for row in data['Condition'] if id(row) not in existing_ids]
 for group,(start,end) in enumerate(segments,1):
  for index,row in enumerate(existing,1):
   copied=dict(row);copied['groupIndex']=group
   copied['conditionId']=f'{root}.event.window.{group}.cond.{index}'
   data['Condition'].append(copied)
  condition(root,'BAR_BETWEEN','world.calendar',f'{start},{end}',group=group)

def root_id(code:str):return 'daily.'+code.lower().replace('-','.')
def read_doc(p):
 with ZipFile(p) as z: xml=ET.fromstring(z.read('word/document.xml'))
 ns='{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
 return [''.join((n.text or '') if n.tag==ns+'t' else '\n' if n.tag in [ns+'br',ns+'cr'] else '' for n in e.iter()).strip() for e in xml.iter(ns+'p') if ''.join(e.itertext()).strip()]

def build(project_root:Path):
 data=defaultdict(list); seen=set(); audit={'source':SOURCE_PATH,'groups':[],'presets':['normal growth <10:2 tenths, otherwise1 tenth; authoring draft rule','character/route/weather gates fail closed until truthful producers exist'],'art_pending':['city hall/court/garden/academy use registered general street artwork pending dedicated backgrounds']}
 def add(sheet,**row):data[sheet].append(row);return row
 def text(key,value,comment=''):
  if key not in seen:add('Text',key=key,text=value,comment=comment);seen.add(key)
  return key
 def flag(key,only=True):
  if key in {r['key'] for r in data['ConditionKey']}:return
  add('ConditionKey',key=key,nameText=text('condition.'+key,key),initialValue=False,onlySetTrue=only)
 def condition(owner,typ,target,value,group=1,owner_type='event'):
  if typ=='BOOL_EQ' and not target.startswith(('choice:','anyChoice:','check:','bar:','not:','gameplay:')):flag(target,not target.startswith(('world.','weather.','availability.')) and target!='church.detention.active')
  return add('Condition',conditionId=f'{owner}.{owner_type}.cond.{len(data["Condition"])+1}',ownerType=owner_type,ownerId=owner,groupIndex=group,condType=typ,target=target,value=str(value).lower() if isinstance(value,bool)else str(value))
 def result(root,typ,target,value,suffix,owner=None):
  return add('Result',resultId=root+'.'+suffix,eventId=owner or root,seq=len(data['Result'])+1,resultType=typ,target=target,value=str(value).lower() if isinstance(value,bool)else str(value))
 def root(root,title,once,pclass='Conversation',props=''):
  flag('story.'+root+'.completed');add('Event',eventId=root,eventKind='world',actorId='world',onceOnly=once,titleText=text(root+'.title',title),presentationClass=pclass,presentationKey=root+'.presentation',eventProperty='entry=MapAction;blocksSave=true;'+props)
  add('PresentationIndex',presentationKey=root+'.presentation',presentationClass=pclass)
  result(root,'SET_FLAG','story.'+root+'.completed',True,'complete')
 def line(root,pid,i,speaker,body,guard=None,text_key=None):
  if speaker and speaker not in SPEAKERS:raise ValueError(f'Unmapped daily speaker {speaker!r} in {root}')
  sid=SPEAKERS.get(speaker);key=text(text_key or root+f'.text.{i:03}',body)
  if sid=='daily.actor.church_helper' and not any(row['objId']==sid for row in data['Object']):
   add('Object',objId=sid,objType='npc',nameText=text(sid+'.name','教会帮工'))
  add('ConversationLine',lineId=root+f'.line.{i:03}',pageId=pid,seq=i,speakerKey=sid,side='left' if sid=='player'else'right' if sid else'none',textKey=key,speakerNameKey=text(root+'.speaker.'+str(i),speaker)if speaker and speaker not in ['玩家选择','入场','动作']else None)
 def training(root,attr):
  for amount,op in [(2,'lt'),(1,'ge')]:
   r=result(root,'TRAIN_ATTRIBUTE','player.'+attr,amount,'training.'+op)
   condition(r['resultId'],'BOOL_EQ',f'bar:player.{attr}:{op}:10',True,owner_type='result')
 source=project_root/audit['source'];digest=sha256(source.read_bytes()).hexdigest()
 if digest!=SOURCE_SHA256:raise ValueError(f'Daily source changed; re-audit IDs and triggers: {audit["source"]}; sha256={digest}')
 wb=openpyxl.load_workbook(source,read_only=True,data_only=True)
 groups=[]
 for sheet in wb:
  cur=None
  for row in sheet.iter_rows(values_only=True):
   row=list(row[:3]);row+=['']*(3-len(row));row=[str(v).strip()if v is not None else ''for v in row]
   if NODE.fullmatch(row[0]):
    cur={'code':row[0],'kind':row[1],'title':row[2],'sheet':sheet.title,'rows':[]};groups.append(cur)
   elif cur:cur['rows'].append(row)
 wb.close();audit['sha256']=digest
 assert len(groups)==270,len(groups)
 public_notices={}
 for g in groups:
  code=g['code']; rid=root_id(code); category=g['sheet']; kind=g['kind']; rows=g['rows'];constraint=';'.join(r[1]for r in rows if r[0]=='触发条件')
  if category.startswith('咖啡馆'):
   # Snapshots for the existing cafe hint adapter; no modal event/pause owner.
   p='cafe.ambient.'+('order' if category=='咖啡馆点单'else'chat')+'.'+code.lower()
   add('OpeningPresentation',presentationId=p,titleKey=text(p+'.title',g['title']),leftNameKey='player.name',rightNameKey='character.guest_a.name',visualsResource='Dialogue/CafeSceneVisuals')
   for i,r in enumerate(rows,1):
    if r[0]not in ['触发条件','结果/配置'] and r[2]:add('OpeningLine',lineId=p+'.'+str(i),presentationId=p,seq=i,speaker='right',gesture='none',textKey=text(p+'.text.'+str(i),(r[1]+'：' if r[1] else '')+r[2],constraint))
   audit['groups'].append({'code':code,'state':'nonmodal_cafe_snapshot','constraints':constraint});continue
  loc=category
  if category=='首次到访':loc=g['title']
  if category=='新闻反应':loc=next((l for l in LOCATION if l in g['title']),None)
  if loc=='圣卷大教堂':loc='圣地大教堂'  # Source rename; keep the existing map location/provider.
  if loc not in LOCATION:raise ValueError((code,loc))
  pos,attr,bg,actiontitle=LOCATION[loc];pool='daily.action.'+pos
  is_visit=category=='首次到访';is_news=category=='新闻反应';has_choices=kind=='选择事件'
  pkind='visit'if is_visit else'news'if is_news else'ordinary'
  date_windows,excluded_dates,earliest_date=date_rules(constraint)
  root(rid,g['title'],is_visit or is_news or has_choices,'Conversation',f'dailyPool={pool};dailyKind={pkind}')
  condition(rid,'BOOL_EQ','world.map.active',True)
  condition(rid,'BOOL_EQ','church.detention.active',False)
  condition(rid,'BAR_BETWEEN','world.calendar',f'2,{day(11,22)}')
  for prior in re.findall(r'([A-Z]+-(?:R)?\d{2})后',constraint):
   if prior.startswith('VISIT-'):continue # Pool guarantees intro precedes the ordinary root in one action.
   condition(rid,'BOOL_EQ','story.'+root_id(prior)+'.completed',True)
  if is_news:
   if len(date_windows)!=1:raise ValueError(f'News root must have one explicit source window: {code}: {constraint}')
   m=re.search(r'(10|11)月(\d{1,2})日',g['title']);assert m,g
   when=day(int(m[1]),int(m[2]))
   if '仪式' in g['title']:
    condition(rid,'BOOL_EQ','story.church.1009.s2.completed',False)
   else:
    headline=g['title'].split('｜')[1];public='news.public.'+str(when)+'.'+sha256(headline.encode()).hexdigest()[:8]
    public_notices[(when,headline)]=public
    condition(rid,'BOOL_EQ',public,True)
   issue=re.search(r'第([一二三四五六])期报纸已展示',constraint)
   if issue:condition(rid,'BOOL_EQ','story.newspaper.'+ISSUE_DAYS[issue.group(1)]+'.completed',True)
   if code=='NEWS-11':condition(rid,'BOOL_EQ','story.daily.old.02.completed',True)
  # First-visit source dependencies apply to all variants, even if omitted in shorthand rows.
  base_known={'旧城区':['sparrow'],'圣徒街':['pascal'],'圣西尔万郊区':['sparrow','eugene'],'大堂区':['colette'],'圣地大教堂':['sade'],'灰墙学院':['sylvain'],'市政厅':['adele'],'首都法院':['eugene'],'花园区':['sorel']}[loc]
  for known in base_known:condition(rid,'BOOL_EQ','known.'+known,True)
  for display,sid in SPEAKERS.items():
   if display in constraint and sid not in ['player','guest_a'] and ('相识' in constraint or '已登场'in constraint):condition(rid,'BOOL_EQ','known.'+sid,True)
  if loc=='花园区':condition(rid,'BAR_GTE','world.calendar',day(10,15));condition(rid,'BOOL_EQ','story.revolution.1014.s1.completed',True)
  if loc=='旧城区':condition(rid,'BAR_GTE','world.calendar',day(10,11))
  if loc=='大堂区':condition(rid,'BOOL_EQ','story.neutral.1008.s1.completed',True)
  if loc=='圣地大教堂':condition(rid,'BOOL_EQ','story.church.1009.s2.completed',True)
  if loc=='灰墙学院':condition(rid,'BOOL_EQ','story.rose.1011.s1.completed',True)
  if '雨'in constraint:condition(rid,'BOOL_EQ','weather.rain',True)
  # Explicit character availability is separate from having met the character.
  if any(r[1]=='西尔万'for r in rows)or'西尔万可出场'in constraint:condition(rid,'BOOL_EQ','availability.sylvain',True)
  if any(r[1]=='阿黛尔'for r in rows):condition(rid,'BOOL_EQ','availability.adele',True)
  if is_news:
   segments=available_date_ranges(*date_windows[0],excluded_dates)
   if not segments:raise ValueError(f'News root has no dates after exclusions: {code}: {constraint}')
   add_date_window(data,condition,rid,segments)
  elif excluded_dates:
   segments=available_date_ranges(max(2,earliest_date or 2),day(11,22),excluded_dates)
   if not segments:raise ValueError(f'Daily root has no dates after exclusions: {code}: {constraint}')
   add_date_window(data,condition,rid,segments)
  elif earliest_date:
   condition(rid,'BAR_GTE','world.calendar',earliest_date)
  if not is_visit:training(rid,attr)
  if has_choices:
   pg=rid+'.opening';group=rid+'.choices';add('ConversationPage',pageId=pg,presentationKey=rid+'.presentation',seq=1,bgOverride=bg,optionGroupKey=group)
   ln=0; responses={};page_seq=2
   for r in rows:
    if r[0].startswith('选项 '):
     choice=r[0].split()[-1];cid=rid+'.choice.'+choice.lower();owner=cid+'.result';add('Choice',choiceId=cid,groupKey=group,textKey=text(cid+'.text',r[2]),resultOwnerId=owner,seq=ord(choice)-64)
     flag(rid+'.selected.'+choice.lower());result(rid,'SET_FLAG',rid+'.selected.'+choice.lower(),True,'selected.'+choice.lower(),owner)
    elif r[0].startswith('回应 '):responses.setdefault(r[0].split()[-1],[]).append(r)
    elif r[0]not in ['触发条件','结果/配置']:
     body=r[2]or(r[1]if r[0]in ['入场','动作']else'')
     if body:ln+=1;line(rid,pg,ln,''if r[0]in['入场','动作']else r[1],body)
   for choice,rs in responses.items():
    pid=rid+'.response.'+choice.lower();add('ConversationPage',pageId=pid,presentationKey=rid+'.presentation',seq=page_seq,pageCondKeys='choice:'+rid+'.choice.'+choice.lower(),bgOverride=bg);page_seq+=1
    for r in rs:ln+=1;line(rid,pid,ln,r[1],r[2])
  else:
   # Map encounters are acted dialogue, including first visits and news reactions.
   # Keep the source's entry/action beats as narrator lines, not flattened "name: text" reading copy.
   # Actual newspapers below still use Reading; public bulletins still use Notice.
   pid=rid+'.opening';add('ConversationPage',pageId=pid,presentationKey=rid+'.presentation',seq=1,bgOverride=bg)
   for i,r in enumerate(rows,1):
    if r[0]in ['触发条件','结果/配置']:continue
    body=r[2]or(r[1]if r[0]in ['入场','动作']else'')
    if not body:continue
    speaker=''if r[0]in ['入场','动作']else r[1]
    line(rid,pid,i,speaker,body,text_key=rid+'.text.'+str(i))
  audit['groups'].append({'code':code,'root':rid,'location':loc,'kind':pkind,'constraints':constraint,'availabilityWindows':segments if is_news or excluded_dates else []})
 # Mount one existing Repeat action per location, not 24 competing map buttons.
 for loc,(pos,attr,bg,title)in LOCATION.items():
  aid='daily.action.'+pos;targets=[r['eventId']for r in data['Event']if f'dailyPool={aid};'in r['eventProperty']]
  add('Action',actionKey=aid,type='REPEAT',mustExecute=False,nameText=text(aid+'.name',title),descText=text(aid+'.desc','前往'+loc+'，完成日常行动。'),requirements='ActionPoint(1)',resultEvents='|'.join(targets),durationDays=0,progressTarget=0,deliveryMode='None',replacesLocationActions=False,focusLocationOnComplete=False)
  condition(aid,'BOOL_EQ','world.map.active',True,owner_type='action')
  condition(aid,'BOOL_EQ','church.detention.active',False,owner_type='action')
  condition(aid,'BAR_BETWEEN','world.calendar',f'2,{day(11,22)}',owner_type='action')
  # Day10/21 public lockdown: only the saint street daily action is available.
  if loc!='圣徒街':
   condition(aid,'BOOL_EQ',f'not:bar:world.calendar:eq:{day(10,21)}',True,owner_type='action')
 # Public notices are generated from source news headings, never secret political facts.
 for (when,headline),public in public_notices.items():
  rid='news.bulletin.'+public.rsplit('.',1)[-1];root(rid,headline,True,'Notice')
  data['Event'][-1]['eventProperty']='entry=MorningBeforeMap;blocksSave=true'
  add('EventDate',eventId=rid,day=when,timeSlot='morning',repeatRule='once',priority=100)
  add('Notice',presentationKey=rid+'.presentation',titleKey=rid+'.title',timeKey=text(rid+'.date','670年'+str((date(2000,10,6).fromordinal(date(2000,10,6).toordinal()+when-1)).month)+'月'+str(date.fromordinal(date(2000,10,6).toordinal()+when-1).day)+'日'),textKey=text(rid+'.body',headline))
  result(rid,'SET_FLAG',public,True,'public');flag(public)
 # Six complete newspaper texts, faithfully sourced and date-triggered in the existing reader.
 papers=[]
 for source in sorted((project_root/'策划文件/文案6点/文案/b-报纸事件-换皮').glob('*.docx')):
  pars=[p for p in read_doc(source)if p];joined='\n'.join(pars);m=re.search(r'670年\s*(10|11)月\s*(\d{1,2})日',joined);assert m,source
  when=day(int(m[1]),int(m[2]));rid=f'newspaper.{m[1]}{int(m[2]):02d}';root(rid,pars[0],True,'Reading')
  data['Event'][-1]['eventProperty']='entry=MorningBeforeMap;blocksSave=true'
  add('EventDate',eventId=rid,day=when,timeSlot='morning',repeatRule='once',priority=200)
  pid=rid+'.reading';add('ReadingPage',pageId=pid,presentationKey=rid+'.presentation',seq=1,titleKey=rid+'.title')
  for i,p in enumerate(pars,1):add('ReadingLine',lineId=rid+f'.line.{i:03d}',pageId=pid,seq=i,mode='narration',textKey=text(rid+f'.text.{i:03d}',p,source.relative_to(project_root).as_posix()))
  papers.append({'root':rid,'source':source.relative_to(project_root).as_posix(),'sha256':sha256(source.read_bytes()).hexdigest(),'day':when})
 data['_audit']={'daily':audit,'newspapers':papers};data['_replace_roots']=['daily.','news.bulletin.','newspaper.','cafe.ambient.']
 return dict(data)

