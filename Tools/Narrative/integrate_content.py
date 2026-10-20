"""Single writer for whole-story NCS integration.
Read XLSX with bundled Python; emit merge plan, then use artifact-tool to edit source
XLSX. Official GenerateAll.bat remains the only production JSON/C# generator.
"""
from __future__ import annotations
import argparse,importlib.util,json,hashlib,shutil,sys,re
from pathlib import Path
from collections import defaultdict
import openpyxl
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/narrative-integration'
NARRATIVE=ROOT/'Assets/ConfigTables/Data/Narrative/Narrative.xlsx'
TEXT=ROOT/'Assets/ConfigTables/Data/Shared/Text.xlsx'
PRIMARY={'Bar':lambda r:(r.get('ownerId'),r.get('barId')),'EventDate':lambda r:(r.get('eventId'),r.get('day'),r.get('timeSlot'),r.get('repeatRule')),'CafeVisitHook':lambda r:(r.get('visitKey'),r.get('stage'),r.get('trigger'),r.get('seq'))}
PLACEHOLDER_TOKEN=re.compile(r'(?i)x{2,}')

def preserve_filled_text(existing,incoming):
 old_text=existing.get('text') if existing else None
 new_text=incoming.get('text')
 if isinstance(new_text,str) and PLACEHOLDER_TOKEN.search(new_text) and isinstance(old_text,str) and old_text.strip() and not PLACEHOLDER_TOKEN.search(old_text):
  return {**incoming,'text':old_text}
 return incoming

def read_book(path):
 wb=openpyxl.load_workbook(path,read_only=True,data_only=True);sheets={}
 for sheet in wb:
  raw=[list(r)for r in sheet.iter_rows(values_only=True)]
  while raw and not any(v is not None for v in raw[-1]):raw.pop()
  h=raw[0]
  while h and h[-1]is None:h.pop()
  width=len(h);meta=[(r+[None]*width)[:width]for r in raw[:4]]
  rows=[{h[i]:r[i]if i<len(r)else None for i in range(1,width)}for r in raw[4:]if len(r)>1 and r[1]is not None and not str(r[0]or'').startswith('##')]
  sheets[sheet.title]={'header':h,'meta':meta,'rows':rows,'originalRows':len(raw)}
 wb.close();return sheets

def add_defaults(data):
 def add(sheet,**kw):data[sheet].append(kw)
 def text(key,value):add('Text',key=key,text=value,comment='Whole-story centralized authoring preset; see Docs/NarrativeIntegrationHandoff.md');return key
 def flag(key,initial=False,only=False):add('ConditionKey',key=key,nameText=text('condition.'+key,key),initialValue=initial,onlySetTrue=only)
 # Authored opening IF0: the player explicitly confirms leaving AFTER the refusal text.
 # Reuse existing source text, never select an ending merely for opening the decision page.
 flag('story.arrival.leave.completed',only=True)
 add('Result',resultId='arrival.leave.complete',eventId='arrival.leave',seq=1,resultType='SET_FLAG',target='story.arrival.leave.completed',value='true',note='Opening IF0; only explicit leave root completion, retry/accept never set this fact.')
 add('Ending',endingId='arrival.ending.if0',enabled=True,priority=100,titleText='arrival.refuse.01.08',messageText='arrival.refuse.01.04')
 add('Condition',conditionId='arrival.ending.if0.completed',ownerType='ending',ownerId='arrival.ending.if0',groupIndex=1,condType='BOOL_EQ',target='story.arrival.leave.completed',value='true')
 add('Condition',conditionId='arrival.ending.if0.happened',ownerType='ending',ownerId='arrival.ending.if0',groupIndex=1,condType='EVENT_HAPPENED',target='arrival.leave',value='')
 for key in ['route.neutral','route.rose','route.church','route.revolution','story.detained','story.adele.away','story.sylvain.unavailable','weather.rain','availability.adele','availability.sylvain']:
  flag(key)
 for oid,label in [('faction','阵营'),('training','成长进度')]:
  add('Object',objId=oid,objType='world',nameText=text('object.'+oid+'.name',label))
 for char in ['adele','antoine','colette','alienor','sylvain','lesou','sorel','eugene','pascal','sparrow','sade','marie']:
  flag('known.'+char,only=True)
  add('Bar',barId='affection',ownerId=char,targetId='player',barType='relation',min=-20,max=20,direction='highIsGood',advanceMode='event_driven',initValue=0,reverseInitValue=0,nameText=text('bar.'+char+'.affection',char+'好感'),autoStep=0,sourceMode='ncs')
 for faction in ['neutral','rose','church','revolution']:
  add('Bar',barId=faction,ownerId='faction',barType='stat',min=-20,max=20,direction='highIsGood',advanceMode='event_driven',initValue=0,reverseInitValue=0,nameText=text('bar.faction.'+faction,faction+'倾向'),autoStep=0,sourceMode='ncs')
 for attr in ['language','courage','wisdom','perception']:
  add('Bar',barId=attr,ownerId='training',barType='stat',min=0,max=9,direction='highIsGood',advanceMode='event_driven',initValue=0,reverseInitValue=0,nameText=text('bar.training.'+attr,attr+'成长进度'),autoStep=0,sourceMode='ncs')


def merge(modules):
 books={str(NARRATIVE):read_book(NARRATIVE),str(TEXT):read_book(TEXT)};sheets=dict(books[str(NARRATIVE)]);sheets.update(books[str(TEXT)])
 data=defaultdict(list);add_defaults(data);audit={};replace=[];placeholder_preservations=[]
 for module in modules:
  path=ROOT/'Tools/Narrative/Content'/f'{module}.py';spec=importlib.util.spec_from_file_location('content_'+module,path);m=importlib.util.module_from_spec(spec);sys.modules[spec.name]=m;spec.loader.exec_module(m)
  rows=m.build(ROOT)
  if not isinstance(rows,dict):raise TypeError((module,type(rows)))
  audit[module]=rows.pop('_audit',{})
  replace.extend(rows.pop('_replace_roots',[]))
  for table,rs in rows.items():
   if table.startswith('_'):continue
   if table not in sheets:raise ValueError(f'{module}: unknown NCS source sheet {table}')
   data[table].extend(rs)
 # Preserve untouched auxiliary Reading indexes while replacing a faction's root pages.
 # Re-add genuine existing index rows that the worker intentionally retains as references.
 incoming_keys={r.get('presentationKey') for r in data['PresentationIndex']}
 for row in sheets['PresentationIndex']['rows']:
  if row.get('presentationKey') not in incoming_keys and any(r.get('presentationKey')==row.get('presentationKey') for r in data['ReadingPage']):
   data['PresentationIndex'].append(row)
 # Extend the EXISTING NCS page sheet for graph edges/real gameplay; schema is owned by runtime worker.
 page=sheets['ConversationPage']
 for col in ['gameplayKey','nextPageId']:
  if col not in page['header']:
   page['header'].append(col)
   for r,value in zip(page['meta'],[col,'string','c',col]):r.append(value)
 def matches(v):
  if not isinstance(v,str):return False
  return any(v.startswith(p)if p.endswith('.')else(v==p or v.startswith(p+'.'))for p in replace)
 purge={'Event':['eventId'],'EventDate':['eventId'],'Result':['eventId','resultId'],'Condition':['ownerId','conditionId'],'SetupParams':['setupKey'],'Choice':['choiceId','groupKey'],'PresentationIndex':['presentationKey'],'ConversationPage':['pageId','presentationKey'],'ConversationLine':['lineId','pageId'],'ReadingPage':['pageId','presentationKey'],'ReadingLine':['lineId','pageId'],'AbilityCheck':['checkId'],'AbilityCheckCondition':['checkId'],'Notice':['presentationKey'],'OpeningPresentation':['presentationId'],'OpeningLine':['presentationId']}
 changes={}
 for name,sheet in sheets.items():
  existing=sheet['rows']
  if name in purge:existing=[r for r in existing if not any(matches(r.get(k))for k in purge[name])]
  key=PRIMARY.get(name,lambda r,h=sheet['header'][1]:r.get(h))
  index={key(r):r for r in existing}
  for row in data.get(name,[]):
   unknown=set(row)-set(sheet['header'])
   if unknown:raise ValueError(f'{name} unsupported fields {unknown} in {row}')
   k=key(row)
   if k is None or k=='':raise ValueError(f'{name} missing primary key: {row}')
   if name=='Text' and k in index:
    merged=preserve_filled_text(index[k],row)
    if merged.get('text')!=row.get('text'):
     placeholder_preservations.append({'key':k,'sourcePlaceholder':row.get('text'),'preservedText':merged.get('text')})
    row=merged
   # New declared facts never erase current creation/specialization flags. Route policy
   # is intentionally mutable so session restore/branch transactions can clear them.
   if name=='ConditionKey'and k in index:
    old=index[k]
    if not str(k).startswith('route.'):
     row={**row,'initialValue':old.get('initialValue',False),'onlySetTrue':bool(old.get('onlySetTrue'))or bool(row.get('onlySetTrue'))}
   index[k]=row
  sheet['rows']=list(index.values());changes[name]={'before':len(sheet['meta']) and len(sheet.get('rows',[])),'after':len(index)}
 # Alias-free public names, keeping authored map objects. Actual map binding aliases are documented.
 positions={r['positionKey']:r for r in sheets['MapPosition']['rows']}
 text_index={r['key']:r for r in sheets['Text']['rows']}
 def mount(pos,label,x,y,alias=None):
  key='map.position.integrated.'+pos+'.name';text_index[key]={'key':key,'text':label,'comment':'Whole-story location; legacy artwork anchor alias '+str(alias or pos)}
  old=positions.get(pos,{})
  ext=[v for v in str(old.get('extendedActions')or'').replace('|',',').split(',')if v]
  aid='daily.action.'+pos
  if aid in {r['actionKey']for r in sheets['Action']['rows']}and aid not in ext:ext.append(aid)
  positions[pos]={**old,'positionKey':pos,'nameText':key,'posX':old.get('posX',x),'posY':old.get('posY',y),'isImportant':True,'enabled':True,'defaultAction':old.get('defaultAction'),'extendedActions':','.join(ext),'imgPath':old.get('imgPath')or'Art/UI/Avatars/Unknown'}
 if 'daily'in modules:
  for pos,label,x,y,alias in [('旧城区','旧城区',-958,367,'大堂区'),('圣地大教堂','圣地大教堂',300,484,'temporary'),('library','灰墙学院',187,-159,None),('market','大堂区',-57,192,None),('圣徒街','圣徒街',774,399,None),('圣西尔万郊区','圣西尔万郊区',-379,406,None),('市政厅','市政厅',-411,45,None),('首都法院','首都法院',567,86,None),('花园区','花园区',450,-310,None)]:mount(pos,label,x,y,alias)
  if 'temporary'in positions:positions['temporary']={**positions['temporary'],'enabled':False,'isImportant':False}
  sheets['MapPosition']['rows']=list(positions.values());sheets['Text']['rows']=list(text_index.values())
 # Remove the obsolete integration-attempt spelling; the catalog identity is marie.
 sheets['Bar']['rows']=[r for r in sheets['Bar']['rows'] if r.get('ownerId')!='mary']
 # Declare referenced persistent flags, but never infer success or manufacture a producer.
 flags={r['key']:r for r in sheets['ConditionKey']['rows']}
 def declare(key):
  if key not in flags:
   tk='condition.'+key;flags[key]={'key':key,'nameText':tk,'initialValue':False,'onlySetTrue':key.startswith(('story.','known.','ending.'))}
   text_index[tk]={'key':tk,'text':key,'comment':'Referenced cross-content fact; false until its actual producer completes.'}
 for r in sheets['Condition']['rows']:
  if r['condType']=='BOOL_EQ'and not str(r['target']).startswith(('choice:','check:','bar:','anyChoice:','not:','gameplay:','dependency:')):declare(r['target'])
 for r in sheets['Result']['rows']:
  if r['resultType']in['SET_FLAG','CLEAR_FLAG']and r.get('target'):declare(r['target'])
 sheets['ConditionKey']['rows']=list(flags.values());sheets['Text']['rows']=list(text_index.values())
 # Mutable host availability/state must never inherit an earlier false-only construction declaration.
 mutable={'church.detention.active','story.adele.away','story.sylvain.unavailable','weather.rain','availability.adele','availability.sylvain','world.map.active','route.neutral','route.rose','route.church','route.revolution'}
 for row in sheets['ConditionKey']['rows']:
  if row['key'] in mutable:row['onlySetTrue']=False
 # A missing event referenced by another faction is a hard integration error, never a fake placeholder.
 events={r['eventId']for r in sheets['Event']['rows']};errors=[]
 for r in sheets['Result']['rows']:
  if r['resultType']=='QUEUE_EVENT'and str(r.get('target','')).removeprefix('event:')not in events:errors.append('Unknown continuation '+str(r))
 # Apply approved presentation on the existing NCS source sheets.
 from ending_revision import apply as apply_ending_revision
 apply_ending_revision(sheets)
 from character_visual_revision import apply as apply_character_visual_revision
 apply_character_visual_revision(sheets)
 # Emit a completely rectangular value matrix including untouched metadata.
 workbooks=[]
 for path,own in books.items():
  exported=[]
  for name in own:
   s=sheets[name];h=s['header'];matrix=[(r+[None]*len(h))[:len(h)]for r in s['meta']]
   matrix += [[None]+[r.get(k)for k in h[1:]]for r in s['rows']]
   exported.append({'name':name,'matrix':matrix,'originalRows':s['originalRows'],'width':len(h)})
  workbooks.append({'path':path,'beforeSha256':hashlib.sha256(Path(path).read_bytes()).hexdigest(),'sheets':exported})
 OUT.mkdir(parents=True,exist_ok=True)
 plan={'workbooks':workbooks,'audit':audit,'placeholderPreservations':placeholder_preservations,'errors':errors,'counts':{name:len(s['rows'])for name,s in sheets.items()},'modules':modules}
 (OUT/'merge-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
 (OUT/'content-audit.json').write_text(json.dumps({'modules':audit,'errors':errors,'counts':plan['counts']},ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'counts':plan['counts'],'errors':errors},ensure_ascii=False))
 return plan

def character_visual_plan():
 # Narrow art-only revision through the same single-writer/merge-workbooks pipeline.
 # Content, triggers, dialogue text and every other source sheet remain untouched.
 from character_visual_revision import apply
 sheets=read_book(NARRATIVE)
 original=[dict(row) for row in sheets['Object']['rows']]
 changes=apply(sheets)
 sheet=sheets['Object'];h=sheet['header']
 matrix=[(row+[None]*len(h))[:len(h)] for row in sheet['meta']]
 matrix += [[None]+[row.get(key) for key in h[1:]] for row in sheet['rows']]
 edits=[]
 for i,(before,after) in enumerate(zip(original,sheet['rows']),start=4):
  for col,key in enumerate(h[1:],start=1):
   if before.get(key)!=after.get(key):edits.append({'row':i,'column':col,'before':before.get(key),'after':after.get(key)})
 plan={'workbooks':[{'path':str(NARRATIVE),'beforeSha256':hashlib.sha256(NARRATIVE.read_bytes()).hexdigest(),
        'sheets':[{'name':'Object','matrix':matrix,'originalRows':sheet['originalRows'],'width':len(h)}]}],
       'errors':[],'characterVisualChanges':changes,'characterVisualCellEdits':edits}
 OUT.mkdir(parents=True,exist_ok=True)
 (OUT/'merge-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
 (ROOT/'output/npc-art-2026-10-20/character-visual-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'changedObjects':len(changes),'changedCells':len(edits),'sheets':['Object']},ensure_ascii=False))
 return plan

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--modules',nargs='+',default=['neutral','rose','church','revolution','daily']);p.add_argument('--allow-unresolved',action='store_true');p.add_argument('--character-visuals-only',action='store_true');args=p.parse_args()
 plan=character_visual_plan() if args.character_visuals_only else merge(args.modules)
 if plan['errors']and not args.allow_unresolved:raise SystemExit('Unresolved cross-content references: see output/narrative-integration/merge-plan.json')
