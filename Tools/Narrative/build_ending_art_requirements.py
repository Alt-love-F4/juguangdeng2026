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

def render_card(item):
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
    return image


def build_if0():
    item=dict(id='END-IF0',title='未开始就已结束',optional=False,
        brief='主角拒绝接手沸点咖啡馆后，用剩余盘缠离开王都。画面以携行囊的主角背影和通往家乡的道路为主体，王都城门渐远；氛围平静、疏离，表现故事尚未开始便已结束。不新增角色或后续剧情。构图为建议，正式插画待美术确认。',
        source='用户要求为拒绝接手结局配置占位插图；开幕文案.docx／arrival.refuse.01.04')
    path='Assets/Art/NarrativePlaceholders/Endings/END-IF0.png'
    target=ROOT/path;image=render_card(item)
    if target.exists():
        with Image.open(target) as check:
            if check.size!=image.size or check.convert('RGB').tobytes()!=image.tobytes():
                raise ValueError('Existing IF0 artwork differs; preserve it: '+path)
    else:
        target.parent.mkdir(parents=True,exist_ok=True);image.save(target)
    return dict(id=item['id'],category='结局CG',name=item['title'],status='用户新增需求·占位待制作',
        requirement=item['brief'],sourceEvidence=item['source'],configuration='arrival.ending.if0',
        notes='本轮用户要求新增IF0占位插图；不是最终画面，不改变结局文字、触发条件或选项。',
        disposition='placeholder',assetPath=path,suggestedRegistryKeys=[],proposedUnregisteredKeys=[],
        endingId='arrival.ending.if0',displayCode='序章 / End if0',reuseKey='',optional=False,
        cardSha256=hashlib.sha256(target.read_bytes()).hexdigest())


def update_if0_only():
    # Patch one record only: never regenerate metadata or cards for delivered endings.
    path=ROOT/'Docs/NarrativeEndingArtManifest.json'
    manifest=json.loads(path.read_text(encoding='utf-8'))
    matches=[i for i,r in enumerate(manifest['requirements']) if r['id']=='END-IF0']
    if len(matches)!=1:raise ValueError('Expected exactly one existing IF0 requirement')
    manifest['requirements'][matches[0]]=build_if0()
    manifest['newCgCount']=sum(bool(r.get('assetPath')) for r in manifest['requirements'])
    path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Updated IF0 placeholder only; all other artwork and requirements preserved.')


def main():
    DEST.mkdir(parents=True,exist_ok=True)
    requirements=[]
    for item in ENDINGS:
        reuse=item.get('reuseKey')
        cardpath='' if reuse else 'Assets/Art/NarrativePlaceholders/Endings/'+item['id']+'.png'
        if cardpath:
            image=render_card(item)
            target=ROOT/cardpath
            if not target.exists():image.save(target)
            # Existing files never overwritten. Metadata is safe to regenerate.
            with Image.open(target) as check:
                if check.size!=(1920,1080):raise ValueError('Card dimensions drift: '+cardpath)
                if check.convert('RGB').tobytes()!=image.tobytes():raise ValueError('Existing card differs; preserve it and version the new card: '+cardpath)
        requirements.append(dict(id=item['id'],category='结局CG',name=item['title'],status='复用现有完整构图' if reuse else ('可选·构图待确认' if item.get('optional') else '用户新需求·待制作'),requirement=item['brief'],sourceEvidence='2026-10-19 用户定稿；'+item['source'],configuration=item['endingId'],notes='文案和素材绑定不授予结局；作者闸门保持。',disposition='reuse' if reuse else 'placeholder',assetPath=cardpath,suggestedRegistryKeys=[reuse] if reuse else [],proposedUnregisteredKeys=[],endingId=item['endingId'],displayCode=item['displayCode'],tone=item['tone'],textOrigin=item['textOrigin'],reuseKey=reuse or '',optional=item.get('optional',False),cardSha256=hashlib.sha256((ROOT/cardpath).read_bytes()).hexdigest() if cardpath else ''))
    requirements.append(build_if0())
    result=dict(revision=REVISION,policy='Local labelled cards only; no image APIs. Presentation metadata does not change ending conditions.',newCgCount=15,reuseCount=1,if0Count=1,requirements=requirements)
    (ROOT/'Docs/NarrativeEndingArtManifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Ending art manifest: 15 local cards including IF0 + 1 reuse plan; no artwork overwritten.')
if __name__=='__main__':
    if sys.argv[1:]==['--only-if0']:update_if0_only()
    elif not sys.argv[1:]:main()
    else:raise SystemExit('Usage: build_ending_art_requirements.py [--only-if0]')
