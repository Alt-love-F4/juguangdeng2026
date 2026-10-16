"""Focused, concurrency-checked C construction follow-up. Run before official generation."""
from pathlib import Path
from datetime import datetime
import hashlib, shutil
import openpyxl
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'Assets/ConfigTables/Data/Narrative/Narrative.xlsx'
before=hashlib.sha256(p.read_bytes()).hexdigest()
w=openpyxl.load_workbook(p)
s=w['Condition']; headers=[c.value for c in s[1]]
id='revolution.1014.s1.c2.3.courage6'
if not any(r[1]==id for r in s.values):
 values=dict(conditionId=id,ownerType='choice',ownerId='revolution.1014.s1.c2.3',groupIndex=1,condType='ATTRIBUTE_GTE',target='courage',value='6')
 s.append([values.get(h) for h in headers])
# Reuse authored exit boundaries. Effects remain subject to runtime verification.
s=w['ConversationLine']; h={c.value:c.column for c in s[1]}
for row in range(5,s.max_row+1):
 lid=s.cell(row,h['lineId']).value
 target={'revolution.1013.s2.p063':'adele','revolution.1028.s1.p150':'adele'}.get(lid)
 if target:
  s.cell(row,h['accentFxKey'],'fx_portrait_fade_out');s.cell(row,h['accentTargetKey'],target)
 if lid=='revolution.1014.s1.p136':
  s.cell(row,h['accentFxKey'],'fx_background_pan_left_to_right');s.cell(row,h['accentTargetKey'],'background')
# P007 reveal occurs after P006 and before P008/c1, not after the choice.
s=w['ConversationPage']; ph={c.value:c.column for c in s[1]}
root='revolution.1014.s1'; pid=root+'.opening_choice'
if not any(r[1]==pid for r in s.values):
 for r in range(5,s.max_row+1):
  if s.cell(r,ph['presentationKey']).value!=root+'.conversation':continue
  seq=s.cell(r,ph['seq']).value
  if seq>=3:s.cell(r,ph['seq'],seq+1)
  if s.cell(r,ph['pageId']).value==root+'.opening':s.cell(r,ph['optionGroupKey']).value=None
 values=dict(pageId=pid,presentationKey=root+'.conversation',seq=3,optionGroupKey=root+'.c1',bgOverride='oct07.cafe.day',stepType='Dialogue')
 s.append([values.get(c.value) for c in s[1]])
 lines=w['ConversationLine']; lh={c.value:c.column for c in lines[1]}
 for r in range(5,lines.max_row+1):
  if lines.cell(r,lh['lineId']).value==root+'.p008':
   lines.cell(r,lh['pageId'],pid);lines.cell(r,lh['seq'],1)
# A missing Sorel portrait is diagnosed rather than replaced with another NPC.
# Fade cue is attached to the first narrator line after P215.
s=w['ConversationLine']; h={c.value:c.column for c in s[1]}
for r in range(5,s.max_row+1):
 if s.cell(r,h['lineId']).value=='revolution.1014.s1.p188':
  s.cell(r,h['accentFxKey'],'fx_dialogue_shake');s.cell(r,h['accentTargetKey'],'dialogue')
 if s.cell(r,h['lineId']).value=='revolution.1014.s1.p216':
  s.cell(r,h['accentFxKey'],'fx_portrait_fade_out');s.cell(r,h['accentTargetKey'],'sorel')
# Root final black is an explicit no-text step; never duplicates the last source line.
if 'FadeOut' in (ROOT/'Assets/ConfigTables/Defines/schema.xml').read_text(encoding='utf-8'):
 s=w['ConversationPage'];h={c.value:c.column for c in s[1]}
 for root in ['revolution.1013.s2','revolution.1028.s1']:
  pid=root+'.fade_out'
  if any(r[1]==pid for r in s.values):continue
  seq=max(s.cell(r,h['seq']).value for r in range(5,s.max_row+1) if s.cell(r,h['presentationKey']).value==root+'.conversation')+1
  v=dict(pageId=pid,presentationKey=root+'.conversation',seq=seq,stepType='FadeOut')
  s.append([v.get(c.value) for c in s[1]])
out=ROOT/'Library/Locus/tmp/C-revolution'/datetime.now().strftime('patch-%Y%m%d-%H%M%S-%f');out.mkdir(parents=True)
shutil.copy2(p,out/'Narrative.before.xlsx');tmp=out/'Narrative.candidate.xlsx';w.save(tmp)
assert hashlib.sha256(p.read_bytes()).hexdigest()==before,'Concurrent workbook edit; candidate retained'
tmp.replace(p);print('Applied C-only follow-up:',out)
