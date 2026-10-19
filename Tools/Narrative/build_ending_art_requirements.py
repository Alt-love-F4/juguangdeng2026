"""Build local labelled END cards, never network images or replacements of delivered art."""
from pathlib import Path
import json,hashlib,textwrap,sys
from PIL import Image,ImageDraw,ImageFont
from ending_revision import ENDINGS, REVISION
ROOT=Path(__file__).resolve().parents[2]
FONT=Path('C:/Windows/Fonts/msyh.ttc')
DEST=ROOT/'Assets/Art/NarrativePlaceholders/Endings'

def wrapped(draw,text,font,width):
    lines=[]
    for paragraph in text.splitlines():
        line=''
        for ch in paragraph:
            if draw.textlength(line+ch,font=font)>width:
                lines.append(line);line=''
            line+=ch
        lines.append(line)
    return lines

def main():
    DEST.mkdir(parents=True,exist_ok=True)
    requirements=[]
    for item in ENDINGS:
        reuse=item.get('reuseKey')
        cardpath='' if reuse else 'Assets/Art/NarrativePlaceholders/Endings/'+item['id']+'.png'
        if cardpath:
            # Deliberately a neutral instruction card, not AI-generated artwork.
            image=Image.new('RGB',(1920,1080),'#edece7');d=ImageDraw.Draw(image)
            head=ImageFont.truetype(str(FONT),54);sub=ImageFont.truetype(str(FONT),32);font=ImageFont.truetype(str(FONT),36)
            d.text((90,65),item['id']+'  '+item['title'],font=head,fill='#24292f')
            d.text((90,155),'美术需求占位 · 非最终插画 · '+('可选 / 构图待确认' if item.get('optional') else '待制作'),font=sub,fill='#805c2f')
            y=255
            for line in wrapped(d,item['brief'],font,1740):
                d.text((90,y),line,font=font,fill='#32373d');y+=57
            y=max(y+50,700)
            for line in wrapped(d,'来源：'+item['source'],sub,1740):
                d.text((90,y),line,font=sub,fill='#555c64');y+=43
            d.text((90,970),'1920×1080 / AVG 半身或场景CG / 保留标题与下方文本框可读性',font=sub,fill='#555c64')
            target=ROOT/cardpath
            if not target.exists():image.save(target)
            # Existing files never overwritten. Metadata is safe to regenerate.
            with Image.open(target) as check:
                if check.size!=(1920,1080):raise ValueError('Card dimensions drift: '+cardpath)
                if check.convert('RGB').tobytes()!=image.tobytes():raise ValueError('Existing card differs; preserve it and version the new card: '+cardpath)
        requirements.append(dict(id=item['id'],category='结局CG',name=item['title'],status='复用现有完整构图' if reuse else ('可选·构图待确认' if item.get('optional') else '用户新需求·待制作'),requirement=item['brief'],sourceEvidence='2026-10-19 用户定稿；'+item['source'],configuration=item['endingId'],notes='文案和素材绑定不授予结局；作者闸门保持。',disposition='reuse' if reuse else 'placeholder',assetPath=cardpath,suggestedRegistryKeys=[reuse] if reuse else [],proposedUnregisteredKeys=[],endingId=item['endingId'],displayCode=item['displayCode'],tone=item['tone'],textOrigin=item['textOrigin'],reuseKey=reuse or '',optional=item.get('optional',False),cardSha256=hashlib.sha256((ROOT/cardpath).read_bytes()).hexdigest() if cardpath else ''))
    requirements.append(dict(id='END-IF0',category='结局登记',name='未开始就已结束',status='序章已存在·未提出新CG',requirement='保留原序章结局标题与正文；本次没有授权新构图，不计入14张新CG。',sourceEvidence='开幕文案.docx；2026-10-19 用户补充',configuration='arrival.ending.if0',notes='不因纳入结局表擅增一张图。',disposition='runtimeOnly',assetPath='',suggestedRegistryKeys=[],proposedUnregisteredKeys=[],endingId='arrival.ending.if0',displayCode='序章 / End if0',reuseKey='',optional=True))
    result=dict(revision=REVISION,policy='Local labelled cards only; no image APIs. Presentation metadata does not change ending conditions.',newCgCount=14,reuseCount=1,if0Count=1,requirements=requirements)
    (ROOT/'Docs/NarrativeEndingArtManifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Ending art manifest: 14 local cards + 1 reuse plan + IF0 registration; no artwork overwritten.')
if __name__=='__main__':main()
