"""CURRENT New Rose DOCX -> Narrative/Text rows. Read INTEGRATION_CONTRACT.md.
Pure stdlib; never writes Excel, generated data, schema or runtime. Blocked roots
have explicit false construction flags, not merely cosmetic blockedBy notes.
"""
from __future__ import annotations
from collections import defaultdict
from datetime import date
import hashlib
import json
from pathlib import Path
import re
from xml.etree import ElementTree as ET
from zipfile import ZipFile

W = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
SOURCE_DIR = Path('策划文件/文案6点/文案/a-新玫瑰（共通+支线）')
ROSE1011_WISDOM = 3  # existing user-confirmed configure_rose_1011_full.py
ROSE1011_ORDER = 'blackcoffee'  # 2026-10-19 integration actual-product preset
QUALITY = {'normal': 60, 'excellent': 90}  # tunable CONTRACT presets, not author numbers
# Explicit 2026-10-19 main/domain/bridge extensions. These are selectors, NOT
# new Product IDs or recipes. Keep the token intact for real domain validation.
GAMEPLAY_SELECTORS = {
    'any.hot': {'baseProduct':None, 'predicate':'actual catalog product; actual portion temperature >=40C'},
    'blackcoffee.sweet': {'baseProduct':'blackcoffee', 'predicate':'actual sugar portion >0; distinct real delivered cup'},
    'blackcoffee.unsweetened': {'baseProduct':'blackcoffee', 'predicate':'actual sugar portion ==0; distinct real delivered cup'},
}
APPRECIATION = 'story.rose.1011.s1.sylvain_appreciation'
HAT = 'story.church.1009.s2.hat_helped'
CLEARED = 'story.church.1020.s1.cleared'
BRIBE = 'church.release.bribery'
RESCUE = 'church.release.rescue'
COOPERATED = 'story.revolution.1029.s1.cooperated'
REFUSED = 'story.revolution.1029.s1.refused'
ALLEGIANCE = 'story.rose.1024.allegiance'
SAFE_REFUSAL = 'story.rose.1024.safe_refusal'
SHORT1 = 'story.rose.ending.trust_limit.pending'
SHORT2 = 'story.rose.ending.job_requirements.pending'
POISON = 'story.rose.route.ending.unforgiven.pending'
BLOOM = 'story.rose.route.ending.bloom.pending'
SOURCES = (
 ('MA70.10.09-S1', 'rose.1009.s1', 76),
 ('MA70.10.11-S1', 'rose.1011.s1', 140),
 ('MA70.10.12-S1', 'rose.1012.s1', 86),
 ('MA70.10.16-S1', 'rose.1016.s1', 96),
 ('MA70.10.19 - S1', 'rose.1019.s1', 55),
 ('MA70.10.24-S1', 'rose.1024.s1', 128),
 ('MA70.10.24-S2', 'rose.1024.s2', 153),
 ('MA70.10.26-S2', 'rose.1026.s2', 64),
 ('MA70.10.26-Short', 'rose.ending.trust_limit', 57),
 ('MA70.11.01-S1', 'rose.1101.s1', 60),
 ('MA70.11.01—结局', 'rose.ending.job_requirements', 31),
 ('MA70.11.03', 'rose.1103.s1', 150),
 ('NR70.11.11', 'rose.route.1111.s2', 23),
 ('NR70.11.15', 'rose.route.1115.s1', 183),
 ('NR70.11.22', 'rose.route.1122.s1', 65),
 ('NR结局-结局分支', 'rose.route.ending.unforgiven', 22),
 ('NR结局-True', 'rose.route.ending.bloom', 115),
)
SPEAKERS = {'{playername}':'player', '阿黛尔':'adele', '麻雀':'sparrow',
 '西尔万':'sylvain', '埃莉诺':'alienor', '萨德':'sade', '贝特朗':'bertrand',
 '布塔克':'rose.butake', '男人':'rose.butake', '老爷':'rose.envoy', '顾客B':'guest_b',
 '埃尔玛':'rose.elma', '贾科尼':'rose.giaconi', '凯':'rose.kai'}


def paragraphs(path: Path) -> list[str]:
    with ZipFile(path) as z:
        root = ET.fromstring(z.read('word/document.xml'))
    return [s for p in root.findall('.//w:p', W)
            if (s := ''.join(t.text or '' for t in p.findall('.//w:t', W)).strip())]


def nums(a, b):
    return list(range(a, b + 1))


def AND(*guards):
    return '|'.join(g for g in guards if g)


def cg(cid):
    return 'choice:' + cid


def any_choices(*choices):
    return 'anyChoice:' + ';'.join(choices)


def is_directive(text):
    return bool(re.match(r'^(?:[\[［【（(]|(?:MA|NR)[\s\d.\-—]|(?:if|IF|If)|选项|进入条件|触发条件|→|END IF|Normal Ending|是否跳转|王国纪元)', text))


def display_segments(text):
    """Separate quotes from narration; never speak author inline annotations."""
    text = text.replace('{playername｝', '{playername}')
    text = re.sub(r'[（(]注[：:].*?[）)]', '', text)
    speaker = None
    m = re.match(r'^(\{playername\}|阿黛尔|麻雀|西尔万|埃莉诺|萨德|贝特朗|布塔克|男人|老爷|顾客B|埃尔玛|贾科尼|凯)\s*[：: ]?', text)
    if m:
        # A prose paragraph merely starting with a character name is NOT speech.
        name_end = len(m.group(1))
        rest = text[name_end:]
        spoken = rest.lstrip().startswith(('：', ':', '「')) or bool(re.match(r'^[（(]\s*[#＃].*?[）)]\s*[：:]?\s*「', rest))
        if spoken:
            speaker = SPEAKERS[m.group(1)]
            text = rest.lstrip(' ：:')
    em = re.search(r'[（(]\s*[#＃]([^）)]+)[）)]', text)
    emote = em.group(1).strip() if em else None
    text = re.sub(r'[（(]\s*[#＃][^）)]+[）)]', '', text).strip(' ：:')
    if not text:
        return []
    if not speaker or '「' not in text or '」' not in text:
        return [(speaker, text, emote)]
    out, cursor = [], 0
    for quote in re.finditer(r'「[^」]*」', text):
        if text[cursor:quote.start()].strip():
            out.append((None, text[cursor:quote.start()].strip(), None))
        out.append((speaker, quote.group(), emote))
        cursor = quote.end()
    if text[cursor:].strip():
        out.append((None, text[cursor:].strip(), None))
    return out


class Bundle:
    def __init__(self, project):
        self.project, self.rows, self.keys = project, defaultdict(list), {}
        self.audit = dict(compiler='Tools/Narrative/Content/rose.py', contract='2026-10-19',
            sources=[], events=[], blockedBy=[], externalDependencies=[], knownProducers=[], artPending=[],
            presets={'normalScore':60, 'excellentScore':90,
                'rose1011Wisdom':{'value':3, 'authority':'Existing user-confirmed configure_rose_1011_full.py'},
                'rose1011Order':{'value':ROSE1011_ORDER, 'authority':'2026-10-19 actual-product integration preset; source leaves {drinkname} open'}},
            runtimeAssumptions=[
                'Gameplay waits for a real correlated CafeWorld transaction, never auto success.',
                'taste: returns an actual attempt outcome (low=false), serve: retains real must-pass/retry semantics.',
                'Result BOOL_EQ presentation guards settle at root completion before branch effects commit.',
                'End stops the current root before later mutually exclusive pages.',
                'LetterRead can display the card without inventing an envelope; cancellation is not completion.'])
        catalog = json.loads((project/'Assets/Resources/FreeCafe/catalog.json').read_text(encoding='utf-8'))
        self.products = {p['id'] for p in catalog['products']}
        asset = (project/'Assets/Resources/Characters/CharacterCatalog.asset').read_text(encoding='utf-8')
        self.characters = set(re.findall(r'characterId:\s*(\S+)', asset))
        for actor in ('adele','sparrow','sylvain','alienor','sade','bertrand','guest_b','rose.envoy','rose.butake'):
            if actor not in self.characters:
                raise ValueError('Missing CharacterCatalog ID: '+actor)
        for faction in ('neutral','rose','church','revolution'):
            self.flag('route.'+faction, exclusive=True)
        for flag in (APPRECIATION,HAT,CLEARED,BRIBE,RESCUE,COOPERATED,REFUSED,ALLEGIANCE,SAFE_REFUSAL,SHORT1,SHORT2,POISON,BLOOM):
            self.flag(flag)
        for actor in ('sparrow','sylvain','alienor','sade'):
            self.flag('known.'+actor)
        for actor, caption in (('rose.elma','埃尔玛'),('rose.giaconi','贾科尼'),('rose.kai','凯')):
            if actor not in self.characters:
                self.add('Object','objId',objId=actor,objType='npc',nameText=self.text(actor+'.name',caption),tags='rose,source-named,portrait-pending')
                self.audit['artPending'].append(dict(objectId=actor,reason='Source-named NPC, no substitute portrait.'))

    def add(self, sheet, key_name, **row):
        key = sheet, str(row[key_name])
        if key in self.keys:
            if self.keys[key] != row:
                raise ValueError(f'Conflicting compiler row {key}')
            return self.keys[key]
        self.keys[key] = row
        self.rows[sheet].append(row)
        return row

    def text(self, key, value, comment=''):
        self.add('Text','key',key=key,text=value,comment=comment)
        return key

    def flag(self, key, exclusive=False):
        exclusive = exclusive or key.startswith('route.') or key in ('story.adele.away','story.sylvain.unavailable')
        self.add('ConditionKey','key',key=key,nameText=self.text('condition.'+key,key),initialValue=False,onlySetTrue=not exclusive)

    def dependency(self, flag, producer):
        self.flag(flag)
        item = dict(flag=flag,producer=producer)
        if item not in self.audit['externalDependencies']:
            self.audit['externalDependencies'].append(item)

class Root:
    def __init__(self, b, source, day=None, slot='morning', entry=None):
        self.b, self.source, self.id = b, source, source['root']
        self.pk, self.p = self.id+'.conversation', source['paragraphs']
        self.pages, self.choices, self.blocked, self.used = [], {}, [], defaultdict(list)
        self.conditions, self.results = 0, 0
        entry = entry or {'morning':'MorningBeforeMap','noon':'AfterBusiness','night':'BeforeNight'}[slot]
        self.event = b.add('Event','eventId',eventId=self.id,eventKind='actor',actorId='player',
            setupType='Tpl_ArrivalConversation',setupKey=self.id,onceOnly=True,
            presentationClass='Conversation',presentationKey=self.pk,eventProperty=f'entry={entry};blocksSave=true')
        b.add('SetupParams','setupKey',setupKey=self.id,tplName='Tpl_ArrivalConversation',skipAble=False,autoAdvance=False)
        b.add('PresentationIndex','presentationKey',presentationKey=self.pk,presentationClass='Conversation',logTag=self.id)
        if day and entry != 'MapAction':
            n = (date(670,int(day[:2]),int(day[2:]))-date(670,10,6)).days+1
            b.add('EventDate','eventId',eventId=self.id,day=n,timeSlot=slot,repeatRule='once',priority=0)
        self.require('story.'+self.id+'.completed', False)
        self.effect('SET_FLAG','story.'+self.id+'.completed','true',label='complete')
        if '.route.' in self.id:
            self.require('route.rose')
            for faction in ('neutral','church','revolution'):
                self.require('route.'+faction,False)
        if source['paragraphCount'] != source['expectedParagraphCount']:
            self.block(1,'source_anchor_drift',f"Expected {source['expectedParagraphCount']} paragraphs, found {source['paragraphCount']}; re-review anchors.")

    def condition(self, owner_type, owner, typ, target, value, group=1):
        self.conditions += 1
        return self.b.add('Condition','conditionId',conditionId=f'{self.id}.cond.{self.conditions:03}',
            ownerType=owner_type,ownerId=owner,groupIndex=group,condType=typ,target=target,value=value)

    def require(self, flag, value=True, group=1):
        self.b.flag(flag)
        self.condition('event',self.id,'BOOL_EQ',flag,str(value).lower(),group)

    def prior(self, root):
        flag = 'story.'+root+'.completed'
        self.require(flag)
        if not root.startswith('rose.'):
            self.b.dependency(flag,root)

    def block(self, paragraph, code, reason, root=True):
        item = dict(root=self.id,node=f'P{paragraph:03}',code=code,reason=reason,scope='root' if root else 'node')
        if item in self.blocked:
            return
        self.blocked.append(item)
        self.b.audit['blockedBy'].append(item)
        if root:
            self.event['eventProperty'] += (';blockedBy=' if ';blockedBy=' not in self.event['eventProperty'] else ',')+code

    def page(self, name, guard='', step='Dialogue', **fields):
        row = dict(pageId=self.id+'.'+name,presentationKey=self.pk,seq=len(self.pages)+1,stepType=step)
        if guard:
            row['pageCondKeys'] = guard
        row.update(fields)
        row = self.b.add('ConversationPage','pageId',**row)
        self.pages.append(row)
        return row

    def dialogue(self, name, anchors, guard='', bg=None, caption=None):
        row = self.page(name,guard,**({'bgOverride':bg} if bg else {}))
        seq = 0
        for n in anchors:
            raw = self.p[n-1]
            if is_directive(raw):
                continue
            if self.id == 'rose.1011.s1' and n == 46:
                raw = raw.replace('{drinkname}','黑咖啡')
            segments = display_segments(raw)
            for part, (speaker, display, emote) in enumerate(segments,1):
                seq += 1
                suffix = '' if len(segments)==1 else f'.{part}'
                key = f'{self.id}.text.p{n:03}{suffix}'
                self.b.text(key,display,f"{self.source['path']}#P{n:03}; sha256={self.source['sha256']}")
                values = dict(lineId=f"{row['pageId']}.p{n:03}{suffix}",pageId=row['pageId'],seq=seq,
                    side='right' if speaker and speaker != 'player' else 'none',textKey=key)
                if speaker:
                    values['speakerKey'] = speaker
                if emote:
                    values['emote'] = emote
                actor_caption = caption if speaker=='bertrand' else None
                if self.id=='rose.1011.s1' and speaker=='sylvain' and n<125:
                    actor_caption = '客人'
                if actor_caption:
                    values['speakerNameKey'] = self.b.text(self.id+'.caption.'+actor_caption,actor_caption)
                self.b.add('ConversationLine','lineId',**values)
            self.used[n].append(row['pageId'])
        return row

    def end(self, name, guard=''):
        return self.page(name,guard,'End')

    def menu(self, name, anchors, guard='', caption=None):
        group = self.id+'.choices.'+name
        page = self.page('menu.'+name,guard,optionGroupKey=group)
        if caption:
            self.b.add('ConversationLine','lineId',lineId=page['pageId']+'.prompt',pageId=page['pageId'],seq=1,side='none',
                textKey=self.b.text(self.id+'.text.prompt.'+name,caption))
        out = []
        for seq,n in enumerate(anchors,1):
            cid = f'{self.id}.choice.p{n:03}'
            if cid in self.choices:
                cid += '.'+name
            display = re.split(r'\s*(?:→|->)',self.p[n-1],maxsplit=1)[0].strip()
            display = re.sub(r'^[①②③④⑤⑥]\s*','',display)
            self.b.add('Choice','choiceId',choiceId=cid,groupKey=group,
                textKey=self.b.text(f'{self.id}.text.p{n:03}.choice',display,f"{self.source['path']}#P{n:03}"),
                resultOwnerId=cid+'.result',seq=seq)
            self.choices[cid] = n
            self.used[n].append(cid)
            out.append(cid)
        return out

    def effect(self, typ, target, value, guard='', owner=None, label=None):
        self.results += 1
        rid = self.id+'.result.'+(label or f'{self.results:03}')
        if typ=='SET_FLAG':
            self.b.flag(target)
        row = self.b.add('Result','resultId',resultId=rid,eventId=owner or self.id,seq=self.results,
            resultType=typ,target=target,value=str(value),note='Source-mapped; settle at root completion, never on click')
        for g in filter(None,guard.split('|')):
            self.condition('result',rid,'BOOL_EQ',g,'true')
        return row

    def known(self, actor, guard, anchors):
        self.effect('SET_FLAG','known.'+actor,'true',guard,label='known.'+actor)
        self.b.audit['knownProducers'].append(dict(flag='known.'+actor,root=self.id,guard=guard or 'root completed',source=anchors))

    def brew(self, name, requirements, anchor, failure=(), guard='', retry=True, taste=False):
        for product,_,_ in requirements:
            selector = GAMEPLAY_SELECTORS.get(product)
            base = selector['baseProduct'] if selector else product
            # Only authorized selectors bypass exact-product lookup. Ingredients
            # such as hotmilk and arbitrary suffixes still fail closed.
            if (base is not None and base not in self.b.products) or (selector and not self.b.products):
                self.block(anchor,'missing_product_'+product,f'Actual FreeCafe base Product {base} is missing or selector has no catalog; ingredients/entities are not products.')
                return ''
            if selector:
                self.b.audit.setdefault('gameplaySelectors',[]).append(dict(root=self.id,node=f'P{anchor:03}',token=product,**selector,authority='2026-10-19 explicit main/domain/bridge contract extension; no simulated result'))
        key = ('taste:' if taste else 'serve:')+'+'.join(f'{p}:{score}:{count}' for p,score,count in requirements)
        page = self.page(name,guard,'Gameplay',gameplayKey=key,bgOverride='scene.cafe')
        self.used[anchor].append(page['pageId'])
        passed = 'gameplay:'+page['pageId']+':passed'
        if failure:
            failed = self.dialogue(name+'.failed',failure,AND(guard,'not:'+passed))
            if retry:
                failed['nextPageId'] = page['pageId']
        return passed

    def finish(self):
        if self.id!='rose.1009.s1':
            self.end('end')  # 1009 preserves its original natural nine-page completion.
        coverage, missing = [], []
        for i,raw in enumerate(self.p,1):
            kind = 'compiled' if i in self.used else 'directive' if is_directive(raw) else 'unmapped'
            if kind=='unmapped':
                missing.append(i)
            coverage.append(dict(anchor=f'P{i:03}',kind=kind,text=raw,compiledTo=list(dict.fromkeys(self.used.get(i,[])))))
        if missing:
            self.block(missing[0],'unmapped_source_prose','Unmapped prose: '+','.join(f'P{i:03}' for i in missing))
        if any(x['scope']=='root' for x in self.blocked):
            # A gate is required in EVERY OR group, including groups added later.
            groups = {c['groupIndex'] for c in self.b.rows['Condition'] if c['ownerType']=='event' and c['ownerId']==self.id}
            for group in sorted(groups):
                self.require('integration.'+self.id+'.ready',True,group)
        status = 'construction-blocked' if any(x['scope']=='root' for x in self.blocked) else 'partial' if self.blocked else 'implemented'
        self.source.update(coverage=coverage,rootStatus=status,blockedBy=self.blocked,
            implementedPages=[p['pageId'] for p in self.pages],implementedChoices=list(self.choices),unmappedAnchors=[f'P{i:03}' for i in missing])
        self.b.audit['events'].append(dict(eventId=self.id,status=status,pages=len(self.pages),choices=len(self.choices),blockedBy=self.blocked))


def _1009(r):
    r.prior('oct07.s2')
    r.condition('event',r.id,'BAR_BETWEEN','world.calendar','4,4')
    r.b.add('Action','actionKey',actionKey=r.id+'.action',type='ONCE',mustExecute=True,
        nameText=r.b.text('action.'+r.id+'.name','赴布塔克俱乐部之约'),
        descText=r.b.text('action.'+r.id+'.desc','按信中约定前往会面。'),requirements='ActionPoint(1)',
        resultEvents=r.id,durationDays=0,progressTarget=0,deliveryMode='None',replacesLocationActions=False,focusLocationOnComplete=True)
    for typ,target,value in (('BAR_BETWEEN','world.calendar','4,4'),('BOOL_EQ','story.oct07.s2.completed','true'),('BOOL_EQ','story.rose.1009.s1.completed','false')):
        r.condition('action',r.id+'.action',typ,target,value)
    # Exact existing nine-page/67-line layout; source text is still read from
    # CURRENT DOCX, never the old D: path or source-map snapshot.
    mixed={36,38,43,51}
    def authored(name, anchors, guard='', bg=None):
        page=r.page(name,guard,**({'bgOverride':bg} if bg else {}))
        seq=0
        for n in anchors:
            raw=r.p[n-1]
            if is_directive(raw):
                continue
            if n in mixed:
                segments=display_segments(raw)
            else:
                # Keep existing authored line grouping; do not split every quote
                # into extra rows merely because it contains two utterances.
                speaker=None
                m=re.match(r'^(\{playername\}|男人|布塔克|老爷)\s*[：:]\s*',raw)
                if m:
                    speaker=SPEAKERS[m.group(1)]
                    raw=raw[m.end():]
                segments=[(speaker,raw,None)]
            for part,(speaker,text,emote) in enumerate(segments):
                seq+=1
                suffix='.'+chr(ord('a')+part) if n in mixed else ''
                key=f'{r.id}.text.p{n:03}{suffix}'
                r.b.text(key,text,f"{r.source['path']}#P{n:03}; sha256={r.source['sha256']}")
                row=dict(lineId=f'{r.id}.p{n:03}{suffix}',pageId=page['pageId'],seq=seq,
                    side='right' if speaker and speaker != 'player' else 'none',textKey=key)
                if speaker:
                    row['speakerKey']=speaker
                r.b.add('ConversationLine','lineId',**row)
            r.used[n].append(page['pageId'])
        return page
    authored('door',nums(4,23),bg='rose.club.exterior')
    authored('blindfold',nums(25,30),bg='rose.club.blindfold')
    reception=authored('reception',nums(32,43),bg='rose.club.reception')
    group=r.id+'.choice.origin'
    reception['optionGroupKey']=group
    for seq,n in enumerate((45,46),1):
        cid=f'{r.id}.choice.p{n:03}'
        text=re.sub(r'^[①②]\s*','',r.p[n-1])
        r.b.add('Choice','choiceId',choiceId=cid,groupKey=group,
            textKey=r.b.text(f'{r.id}.text.p{n:03}',text,f"{r.source['path']}#P{n:03}"),
            resultOwnerId=cid+'.result',seq=seq)
        r.choices[cid]=n
        r.used[n].append(cid)
    authored('origin_letter',nums(48,52),cg(r.id+'.choice.p045'))
    authored('origin_uncle',nums(54,63),cg(r.id+'.choice.p046'))
    authored('request',nums(65,71))
    check='rose.1009.s1.perception'
    r.b.add('AbilityCheck','checkId',checkId=check,mode='Inline',
        comment='Preserve configure_rose_1009.py baseline: perception>=5 shows P073 with temporary CG; failure shows P073 without CG; both continue P074-P076 and complete root.')
    r.b.add('AbilityCheckCondition','conditionId',conditionId=check+'.1',checkId=check,
        attributeId='perception',threshold=5,groupIndex=1,seq=1)
    # Inline failure is skipped before its background is applied by the presenter.
    portrait=r.page('portrait_cg',step='PassiveCheck',checkId=check,bgOverride='rose.club.knight_guidance')
    no_cg=r.page('portrait_no_cg','not:check:'+check)
    key=r.b.text(r.id+'.text.p073',r.p[72],f"{r.source['path']}#P073")
    for page,suffix in ((portrait,'cg'),(no_cg,'no_cg')):
        r.b.add('ConversationLine','lineId',lineId=r.id+'.p073.'+suffix,pageId=page['pageId'],seq=1,side='none',textKey=key)
        r.used[73].append(page['pageId'])
    r.used[72].append(portrait['pageId'])
    authored('portrait_tail',nums(74,76))
    # No early End and no extra terminal page: original nine-page root completes
    # naturally after its shared tail on BOTH outcomes.
    r.b.audit['presets']['rose1009Perception']={
        'value':5,'checkId':check,
        'authority':'Verified existing configure_rose_1009.py baseline; no new source numeric claim',
        'sourceNumericAuthority':False,
        'failure':'P073 without CG, then shared P074-P076; complete root',
        'layout':'9 pages, 67 lines; original page and tail line IDs preserved'}
    r.b.audit['artPending'].append(dict(root=r.id,node='P072',reason='Existing rose.club.knight_guidance placeholder=1 temporary CG, successful PassiveCheck only; failure keeps reception background and same P073 text.'))


def _1011(r):
    r.prior('rose.1009.s1')
    r.prior('neutral.1010.s1')
    r.dialogue('arrival',nums(3,13),bg='scene.cafe')
    r.dialogue('sparrow.insight',nums(15,17),'bar:player.perception:gt:5')
    r.dialogue('offer',[18])
    for c,p in zip(r.menu('sparrow_drink',[20,22,24]),(21,23,25)):
        r.dialogue(f'sparrow_drink.{p}',[p],cg(c))
    r.dialogue('news',nums(27,30))
    a,b = r.menu('heard',[32,33])
    r.dialogue('heard.yes',[35],cg(a))
    r.dialogue('heard.no',[37],cg(b))
    r.dialogue('sylvain.arrives',nums(38,47))
    r.brew('brew_gate',[(ROSE1011_ORDER,90,1)],48,[50],retry=False,taste=True)
    r.dialogue('debate',nums(52,58))
    a,b,c = r.menu('nature',[60,61,62])
    short = any_choices(a,b)
    r.dialogue('ordinary',nums(64,66),short)
    r.dialogue('short.nature',nums(68,73),short)
    r.end('end.short.nature',short)
    r.dialogue('not_human',nums(75,77),cg(c))
    a,correct,c = r.menu('reason',[79,80,81],cg(c))
    short = any_choices(a,c)
    r.dialogue('short.reason',nums(68,73),short)
    r.end('end.short.reason',short)
    failed = AND(cg(correct),f'bar:player.wisdom:lt:{ROSE1011_WISDOM}')
    r.dialogue('short.wisdom',nums(68,73),failed)
    r.end('end.short.wisdom',failed)
    long = AND(cg(correct),f'bar:player.wisdom:ge:{ROSE1011_WISDOM}')
    r.dialogue('long.answer',nums(85,93),long)
    politics = 'specialization.player.letter.politics'
    r.b.flag(politics)
    r.dialogue('long.history_known',nums(95,98),AND(long,politics))
    r.dialogue('long.history_unknown',[100],AND(long,'not:'+politics))
    r.dialogue('long.history',nums(103,110),long,bg='rose.club.knight_guidance')
    r.dialogue('long.recognition',nums(112,114),AND(long,'bar:player.perception:gt:4'))
    r.dialogue('long.farewell',nums(116,126),long,bg='scene.cafe')
    key = r.id+'.card'
    r.b.add('PresentationIndex','presentationKey',presentationKey=key,presentationClass='Reading')
    r.b.add('ReadingPage','pageId',pageId=key+'.page',presentationKey=key,seq=1)
    for seq,n in enumerate((128,129,130),1):
        text = r.b.text(f'{r.id}.text.p{n:03}',r.p[n-1],f"{r.source['path']}#P{n:03}")
        r.b.add('ReadingLine','lineId',lineId=key+f'.p{n:03}',pageId=key+'.page',seq=seq,mode='narration',textKey=text)
        r.used[n].append(key)
    r.page('long.card',long,'LetterRead',readingKey=key)
    r.dialogue('long.card_memory',nums(132,134),AND(long,politics))
    r.dialogue('long.tail',nums(136,140),long,bg='scene.cafe')
    r.effect('SET_FLAG',APPRECIATION,'true',long,label='appreciation')
    r.known('sparrow','','P003-P038; every actually completed short/long tail')
    r.known('sylvain',long,'P125 explicit introduction, P128-P130 card, P136-P140 long tail')


def _1012(r):
    r.prior('rose.1011.s1')
    r.prior('church.1009.s2')
    r.dialogue('trial_offer',nums(4,11),bg='scene.cafe')
    r.brew('brew_gate',[('blackcoffee',60,1),('milkcoffee',60,1)],12,[16])
    r.dialogue('trial_success',[14])
    # CG04 is a user-marked white labelled requirement card, not generated/final artwork.
    # Source P018 introduces the sampling composition; P025 explicitly returns to exterior.
    sampling = r.dialogue('outside.sampling',nums(19,24),bg='cg.adele_sampling')
    r.used[18].append(sampling['pageId'])
    r.dialogue('outside',nums(25,34),bg='cafe.exterior.day')
    r.dialogue('hat.memory',[36],HAT)
    r.dialogue('no_appreciation',nums(39,54),'not:'+APPRECIATION)
    r.end('end.short','not:'+APPRECIATION)
    r.dialogue('appreciation.outside',nums(56,62),APPRECIATION)
    r.dialogue('appreciation',nums(64,70),APPRECIATION,bg='scene.cafe')
    r.dialogue('hat.helped',nums(72,75),AND(APPRECIATION,HAT))
    r.dialogue('hat.not_helped',nums(77,83),AND(APPRECIATION,'not:'+HAT))
    r.dialogue('tail',nums(85,86),APPRECIATION)
    r.known('alienor','','P032-P034 actual encounter; P039 or P072/P077 branch-specific introduction')


def _topic_tree(r, chosen=(), guard=''):
    """Acyclic 3! histories: every topic exactly once, in player-chosen order.
    Distinct option groups avoid the presenter's already-shown-group suppression.
    """
    remaining = [n for n in (64,65,66) if n not in chosen]
    if not remaining:
        return
    name = 'topics.'+('_'.join(map(str,chosen)) if chosen else 'start')
    ranges = {64:nums(69,71),65:nums(73,75),66:nums(77,81)}
    for cid,topic in zip(r.menu(name,remaining,guard),remaining):
        path = AND(guard,cg(cid))
        r.dialogue(name+f'.answer.{topic}',ranges[topic],path)
        _topic_tree(r,chosen+(topic,),path)


def _1016(r):
    r.prior('rose.1011.s1')
    r.require(APPRECIATION)
    r.dialogue('arrival',nums(4,10),bg='scene.cafe')
    r.dialogue('sade.insight',nums(12,13),'bar:player.wisdom:gt:6')
    r.dialogue('order',nums(14,16))
    r.brew('brew_gate',[('blackcoffee.sweet',60,1),('blackcoffee.unsweetened',60,1)],17,[19,20])
    r.block(15,'light_bitter_threshold_unspecified','Source 微苦加糖 / 无糖 is represented by actual positive/zero sugar on two distinct blackcoffee cups. Source gives no quantitative 微苦 threshold; none is invented. Exact bitterness tuning remains a content node, not a root gate.',root=False)
    r.b.audit['artPending'].append(dict(root=r.id,node='P021',reason='Actual cafe scene retained; floor-specific composition awaits genuine floor-unlocked provider, no simulated unlock.'))
    r.dialogue('seated',nums(22,26),bg='scene.cafe')
    r.dialogue('sade.caution',nums(28,34),'bar:player.perception:gt:7')
    r.dialogue('trust.offer',nums(36,44))
    refuse,trust = r.menu('trust',[46,47])
    r.dialogue('trust.refused',nums(49,53),cg(refuse))
    r.end('end.refused',cg(refuse))
    r.dialogue('trust.accepted',nums(56,62),cg(trust))
    _topic_tree(r,guard=cg(trust))
    r.dialogue('topics.tail',nums(83,89),cg(trust))
    a,b,c = r.menu('stance',[91,92,93],cg(trust))
    for cid,target in ((a,'faction.rose'),(b,'faction.revolution')):
        r.effect('BAR_ADD',target,1,owner=cid+'.result')
    flag = 'integration.rose.1016.stance3.resolved'
    r.b.flag(flag)
    r.condition('choice',c,'BOOL_EQ',flag,'true')
    r.block(93,'ambiguous_faction_recipient','Source 中立/王庭教会倾向+1 does not identify one or both recipients. Third stance remains locked until content decision.',root=False)
    r.dialogue('tail',[94,95],cg(trust))
    r.known('sade','','P010 actual introduction; P049-P053 refusal or P083-P096 long tail')
    r.known('alienor','','P009-P010 actual encounter, not filename inference')


def _1019(r):
    r.prior('rose.1016.s1')
    r.dialogue('arrival',nums(5,17),bg='scene.cafe')
    r.dialogue('insight',nums(19,21),'bar:player.perception:gt:7')
    r.dialogue('sylvain',nums(24,27))
    r.brew('brew_gate',[('creamcoffee',60,1),('milkcoffee',60,1)],28)
    r.dialogue('request',nums(29,41))
    help_,refuse = r.menu('guide',[43,44])
    r.dialogue('guide.yes',[46],cg(help_))
    for target in ('sylvain.affection','adele.affection'):
        r.effect('BAR_ADD',target,1,owner=help_+'.result')
    r.dialogue('guide.no',[49],cg(refuse))
    r.dialogue('tail',nums(51,55))


def _1024(r, detained):
    if detained:
        # Full conjunction in each OR group; persuasion is not falsely bribery.
        for group,flag in enumerate((BRIBE,RESCUE),1):
            if group==2:
                r.require('story.'+r.id+'.completed',False,group)
            r.require('story.church.1023.s1.completed',True,group)
            r.require(flag,True,group)
            r.require(RESCUE if flag==BRIBE else BRIBE,False,group)
        r.b.dependency('story.church.1023.s1.completed','church.1023.s1')
        r.block(6,'street_tip_unpriced','Source hands over a purse to street guards but gives no amount; no invented MONEY_REMOVE. Economic effect remains pending even though dialogue is mapped.',root=False)
        r.dialogue('homecoming.street',nums(4,12),bg='street.day')
        r.dialogue('homecoming',nums(15,48),bg='scene.cafe',caption='男孩')
        r.brew('brew_gate',[('blackcoffee',90,1)],49,[51,52])
        r.dialogue('talk',nums(54,72),caption='男孩')
        r.dialogue('mind.bribed',nums(74,78),BRIBE)
        r.dialogue('mind.rescued',nums(80,84),RESCUE)
        r.dialogue('threat',nums(86,99),caption='男孩')
        r.dialogue('bribed',nums(101,111),BRIBE,caption='男孩')
        r.dialogue('rescued',nums(113,115),RESCUE,caption='男孩')
        a,b,c,purpose,d = r.menu('question',nums(118,122))
        bad = any_choices(a,b,c,d)
        r.dialogue('question.other',[124],bad,caption='男孩')
        r.dialogue('question.purpose',[126],cg(purpose),caption='男孩')
        r.dialogue('rose.offer',nums(127,133),caption='男孩')
        r.dialogue('signal',[135,136],'bar:player.perception:gt:9')
        early,defer = r.menu('signal',[138,139],'bar:player.perception:gt:9')
        normal = 'not:'+cg(early)
        r.dialogue('rose.normal_offer',nums(143,147),normal,caption='男孩')
        loyal,refuse = r.menu('allegiance',[150,151],normal)
        r.effect('BAR_ADD','adele.affection',1,guard=normal)
        r.dialogue('tail',[152],caption='男孩')
        r.effect('SET_FLAG',ALLEGIANCE,'true',any_choices(early,loyal))
        r.effect('SET_FLAG',SAFE_REFUSAL,'true',AND(cg(refuse),cg(purpose)))
        r.effect('SET_FLAG',SHORT1,'true',AND(cg(refuse),bad))
    else:
        r.prior('church.1020.s1')
        r.require(CLEARED)
        r.dialogue('arrival',nums(3,33),bg='scene.cafe',caption='男孩')
        r.brew('brew_gate',[('blackcoffee',90,1)],34,[36,37])
        r.dialogue('talk',nums(39,97),caption='男孩')
        r.menu('identity_guess',nums(99,102))
        r.dialogue('flag.offer',nums(103,125),caption='男孩')
        loyal,refuse = r.menu('flag',[127,128])
        r.effect('SET_FLAG',ALLEGIANCE,'true',cg(loyal))
        r.effect('SET_FLAG',SHORT1,'true',cg(refuse))


def _1026(r):
    r.require(ALLEGIANCE)
    r.require(SHORT1,False)
    # One source spans two calendar phases; use existing scheduled roots, no date writes.
    source = dict(r.source, root='rose.1025.s2')
    invite = Root(r.b, source, '1025', 'night')
    invite.require(ALLEGIANCE)
    invite.require(SHORT1,False)
    invite.dialogue('invitation',nums(4,14),bg='scene.cafe')
    invite.end('end')
    for n,pages in invite.used.items():
        r.used[n].extend(pages)
    r.b.audit['events'].append(dict(eventId=invite.id,status='implemented',pages=len(invite.pages),choices=0,blockedBy=[],fragmentOf=r.id))
    r.b.audit.setdefault('sourceFragments',[]).append(dict(eventId=invite.id,source=r.source['path'],anchors='P004-P014',scheduledDay=20,timeSlot='night'))
    r.prior(invite.id)
    r.dialogue('meeting',nums(18,25),bg='club.reception')
    a,b,c = r.menu('role',[27,28,29])
    r.condition('choice',c,'BAR_GTE','player.wisdom','7')
    r.dialogue('role.wait',nums(32,33),cg(a))
    r.dialogue('role.rooms',nums(35,36),cg(b))
    r.dialogue('role.bridge',[38],cg(c))
    r.dialogue('alliance',nums(40,42))
    r.dialogue('doubt',[44],'bar:player.perception:gt:8|bar:player.wisdom:gt:9')
    r.dialogue('mission',nums(46,62))
    r.effect('SET_FLAG','story.rose.1026.s2.prisoner_list_received','true')
    r.block(63,'club_map_action_pending','Source unlocks garden-district club access. Main map integration must register reusable club action, not a parallel location.',root=False)


def _ending(r, pending, anchors, title):
    r.require(pending)
    if r.p[1] == title:
        r.used[2].append(r.id+'.ending.title')
    if r.id.endswith('trust_limit'):
        r.dialogue('recovery.street',nums(2,12),bg='street.day')
        r.dialogue('story',nums(15,34),bg='scene.cafe')
        r.dialogue('betrayal',nums(36,56),bg='market.day')
    elif r.id.endswith('bloom'):
        r.dialogue('story',anchors,bg='scene.cafe')
        r.b.audit['artPending'].append(dict(root=r.id,node='P029/P037/P046',reason='Terminal epilogue next-day montage/credits narrated without mutating game calendar; CG/scroll treatment remains presentation polish.'))
    else:
        r.dialogue('story',anchors,bg='scene.cafe')
    terminal = 'story.'+r.id+'.terminal'
    r.effect('SET_FLAG',terminal,'true',label='terminal')
    eid = r.id+'.ending'
    r.b.add('Ending','endingId',endingId=eid,enabled=True,priority=20,
        titleText=r.b.text(eid+'.title',title),messageText=r.b.text(eid+'.message',title))
    r.condition('ending',eid,'BOOL_EQ',terminal,'true')
    r.condition('ending',eid,'BOOL_EQ','story.'+r.id+'.completed','true')


def _1101(r):
    # Exact source alternatives: cooperation (either origin), or cleared-origin
    # refusal. Detained-origin refusal is not silently assigned a made-up fate.
    for group,flag in ((1,REFUSED),(2,COOPERATED)):
        if group==2:
            r.require('story.'+r.id+'.completed',False,group)
        r.require('story.revolution.1029.s1.completed',True,group)
        r.require(ALLEGIANCE,True,group)
        r.require(flag,True,group)
        r.require(COOPERATED if flag==REFUSED else REFUSED,False,group)
        if group==1:
            r.require('story.rose.1024.s1.completed',True,group)
    r.b.dependency('story.revolution.1029.s1.completed','revolution.1029.s1')
    r.dialogue('arrival',nums(3,38),bg='scene.cafe')
    r.dialogue('success',nums(43,58),COOPERATED)
    r.end('end.success',COOPERATED)
    fail = AND(REFUSED,'story.rose.1024.s1.completed')
    r.effect('SET_FLAG',SHORT2,'true',fail)
    r.effect('QUEUE_EVENT','rose.ending.job_requirements','',fail)
    r.block(41,'detained_refusal_unspecified','Only non-detained 10/24-S1 plus refusal is authored as failure; other refusal origin excluded by event groups.',root=False)


def _1103(r):
    r.prior('rose.1101.s1')
    r.require(ALLEGIANCE)
    r.require(SHORT2,False)
    r.dialogue('rain',nums(3,28),bg='scene.cafe')
    r.brew('brew_gate',[('any.hot',60,1)],30)
    r.block(31,'special_drink_identity_pending','Common path waits for one actual hot product >=40C and score>=60. Special cocoa+hotmilk identity/combination data is unavailable in the root-local passed-only receipt; P032-P037 remains disabled behind a false no-producer marker, never inferred from any.hot success.',root=False)
    special = 'integration.rose.1103.cocoa_milk.served'
    r.b.flag(special)
    r.dialogue('drink.special',nums(32,37),special)
    r.dialogue('shelter.request',nums(39,48))
    refuse,shelter = r.menu('shelter',[50,51])
    transfer_ready = 'integration.rose.1103.revenue_transfer.ready'
    r.b.flag(transfer_ready)
    r.condition('choice',refuse,'BOOL_EQ',transfer_ready,'true')
    r.dialogue('shelter.refused',nums(53,66),cg(refuse))
    r.block(58,'today_revenue_transfer_missing','Refusal hands over ALL today revenue, not current money or guessed number. Choice P050 is locked behind integration.rose.1103.revenue_transfer.ready=false until actual transactional transfer is connected; common acceptance paths remain open.',root=False)
    r.end('end.refused',cg(refuse))
    yes = cg(shelter)
    r.dialogue('shelter.accepted',nums(69,85),yes)
    r.dialogue('insight',nums(87,91),AND(yes,'bar:player.wisdom:gt:8'))
    r.dialogue('identity',nums(93,106),yes)
    comfort,question = r.menu('response',[108,109],yes)
    r.dialogue('comfort',nums(111,114),cg(comfort))
    r.end('end.comfort',cg(comfort))
    r.dialogue('question',nums(117,148),cg(question))
    r.effect('BAR_ADD','alienor.affection',1,guard=cg(question))
    r.effect('SET_FLAG','story.rose.1103.s1.sheltered','true',yes)
    r.effect('SET_FLAG','church.alienor.sheltered','true',yes)
    r.effect('SET_FLAG','church.alienor.refused_shelter','true',cg(refuse))
    r.effect('SET_FLAG','story.rose.1103.s1.identity_known','true',yes)
    r.known('alienor','','P020 actual arrival and both completed tails')


def _1111(r):
    r.prior('church.1111.s1')
    r.dialogue('choice_of_order',nums(3,23),bg='scene.cafe')


def _1115(r):
    r.prior('rose.route.1111.s2')
    r.dialogue('letter',nums(3,23),bg='scene.cafe')
    r.dialogue('letter_and_meeting',nums(24,83),bg='club.reception')
    r.b.audit['artPending'].append(dict(root=r.id,node='P030',reason='No registered underground conference-room key. Inherit same-club reception, not invented artwork.'))
    r.dialogue('insight',[85],'bar:player.perception:gt:9')
    r.dialogue('politics',nums(87,91))
    cause,family = r.menu('motive',[93,94])
    r.dialogue('cause',[96],cg(cause))
    r.dialogue('family',[98],cg(family))
    r.dialogue('question_of_cost',nums(101,103))
    low,high = 'integration.rose.route.1115.candor.low','integration.rose.route.1115.candor.high'
    r.b.flag(low)
    r.b.flag(high)
    high = AND(high,'not:'+low)
    r.block(99,'candor_baseline_and_gap','P099 +2 candor, P104 <1, P115 >2. No initial value, prior producer, contract numeric target or 1..2 outcome. Never map second choice automatically to >2. Draft low/high flags have no producer.')
    r.dialogue('normal_ending',nums(105,113),low)
    terminal = 'story.rose.route.1115.s1.terminal.ideal_world'
    r.effect('SET_FLAG',terminal,'true',low)
    eid = 'rose.route.1115.s1.ending.ideal_world'
    r.b.add('Ending','endingId',endingId=eid,enabled=True,priority=20,
        titleText=r.b.text(eid+'.title','理想新世界'),messageText=r.b.text(eid+'.message','理想新世界'))
    r.condition('ending',eid,'BOOL_EQ',terminal,'true')
    r.condition('ending',eid,'BOOL_EQ','story.'+r.id+'.completed','true')
    r.end('end.normal_ending',low)
    r.dialogue('candor.high',nums(116,137),high)
    intervention = AND(high,'bar:player.perception:gt:9')
    r.dialogue('interjection',nums(139,144),intervention)
    r.dialogue('plan',nums(145,160),intervention)
    r.block(138,'perception_failure_tail_unspecified','P138 opens the dialogue through P182 with no alternate tail for perception<=9. Never play the answer when the initiating question was skipped; content must specify that return path.')
    identity = 'story.rose.1103.s1.identity_known'
    r.b.flag(identity)
    r.dialogue('known_identity',[162],AND(intervention,identity))
    r.dialogue('long.tail',nums(164,182),intervention)
    r.effect('SET_FLAG','story.rose.route.1115.s1.plan_known','true',intervention)


def _1122(r):
    r.prior('rose.route.1115.s1')
    r.require('story.rose.route.1115.s1.plan_known')
    r.dialogue('missing_adele',nums(3,27),bg='scene.cafe')
    r.dialogue('letter',nums(29,34))
    r.dialogue('night',nums(36,60),bg='scene.cafe')
    poison,spare = r.menu('coffee',[63,64],caption=r.p[61])
    r.used[62].append(r.id+'.menu.coffee')
    for cid,flag,target in ((poison,POISON,'rose.route.ending.unforgiven'),(spare,BLOOM,'rose.route.ending.bloom')):
        r.effect('SET_FLAG',flag,'true',cg(cid))
        r.effect('QUEUE_EVENT',target,'',cg(cid))


def _availability(r):
    # Root-tail state only. Presence is explicit source evidence, not filename.
    records = {
        'rose.1011.s1': [('story.adele.away',False,'','P003 actual cafe presence')],
        'rose.1012.s1': [('story.adele.away',False,'','P004-P031 actual presence')],
        'rose.1016.s1': [('story.adele.away',True,'','P005 leave of absence'),('story.sylvain.unavailable',True,'','P036 out teaching/avoiding danger')],
        'rose.1019.s1': [('story.adele.away',False,'','P007 actual presence'),('story.sylvain.unavailable',False,'','P024-P025 actual return')],
        'rose.1024.s1': [('story.adele.away',False,'','P022-P124 actual presence')],
        'rose.1024.s2': [('story.adele.away',False,'','P015-P019 actual reunion')],
        'rose.1101.s1': [('story.adele.away',False,COOPERATED,'P058 actually returns'),('story.adele.away',True,REFUSED,'P015 leaves; terminal refusal has no return')],
        'rose.1103.s1': [('story.adele.away',False,'','P003 actual presence')],
        'rose.route.1115.s1': [('story.adele.away',False,'','P011-P020 actual presence')],
        'rose.route.1122.s1': [('story.adele.away',True,'','P003-P027 missing/held by New Rose')],
        'rose.route.ending.bloom': [('story.adele.away',False,'','P032-P044 actual reunion')],
    }
    for i,(flag,value,guard,source) in enumerate(records.get(r.id,[])):
        r.effect('SET_FLAG',flag,str(value).lower(),guard,label=f'availability.{i}')
        r.b.audit.setdefault('availabilityProducers',[]).append(dict(root=r.id,flag=flag,value=value,guard=guard,source=source))


def build(project_root: Path) -> dict:
    """Pure row compilation; exact source anchors + blockers, no external snapshots."""
    project = Path(project_root).resolve()
    b = Bundle(project)
    files = sorted((project/SOURCE_DIR).glob('*.docx'))
    if len(files)!=17:
        raise ValueError(f'Expected all17 CURRENT New Rose DOCX files, found {len(files)}')
    found,roots = set(),{}
    dates = {'1009':('1009','morning','MapAction'),'1011':('1011','morning',None),
        '1012':('1012','morning',None),'1016':('1016','morning',None),'1019':('1019','noon',None),
        '1024':('1024','noon',None),'1026':('1026','night',None),'1101':('1101','noon',None),
        '1103':('1103','night',None),'1111':(None,'night','Immediate'),'1115':('1115','night',None),'1122':('1122','night',None)}
    for discriminator,rid,expected in SOURCES:
        candidates = [f for f in files if f.name.startswith(discriminator)]
        if len(candidates)!=1:
            raise ValueError(f'Ambiguous/missing CURRENT source: {discriminator}: {candidates}')
        path = candidates[0]
        if path in found:
            raise ValueError(f'Source mapped twice: {path}')
        found.add(path)
        paras = paragraphs(path)
        source = dict(path=path.relative_to(project).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            root=rid,paragraphCount=len(paras),expectedParagraphCount=expected,paragraphs=paras)
        b.audit['sources'].append(source)
        if '.ending.' in rid:
            schedule = ('1026','night',None) if rid.endswith('trust_limit') else (None,'night','Immediate')
        else:
            schedule = dates[re.search(r'\.(\d{4})\.',rid).group(1)]
        roots[rid] = Root(b,source,*schedule)
    dispatch = {
        'rose.1009.s1':_1009,'rose.1011.s1':_1011,'rose.1012.s1':_1012,'rose.1016.s1':_1016,'rose.1019.s1':_1019,
        'rose.1024.s1':lambda r:_1024(r,False),'rose.1024.s2':lambda r:_1024(r,True),'rose.1026.s2':_1026,
        'rose.ending.trust_limit':lambda r:_ending(r,SHORT1,nums(2,56),'信任的界限'),
        'rose.1101.s1':_1101,'rose.ending.job_requirements':lambda r:_ending(r,SHORT2,nums(4,30),'工作需求要看清'),
        'rose.1103.s1':_1103,'rose.route.1111.s2':_1111,'rose.route.1115.s1':_1115,'rose.route.1122.s1':_1122,
        'rose.route.ending.unforgiven':lambda r:_ending(r,POISON,nums(2,21),'无法原谅'),
        'rose.route.ending.bloom':lambda r:_ending(r,BLOOM,nums(3,114),'另一种绽放'),
    }
    for rid,r in roots.items():
        dispatch[rid](r)
        _availability(r)
        r.finish()
    b.audit.update(sourceDirectory=SOURCE_DIR.as_posix(),sourceCount=len(found),
        supportedEvents=[e['eventId'] for e in b.audit['events'] if e['status']!='construction-blocked'],
        constructionBlockedEvents=[e['eventId'] for e in b.audit['events'] if e['status']=='construction-blocked'],
        foreignFlags=[HAT,CLEARED,BRIBE,RESCUE,COOPERATED,REFUSED],
        notes=[
            'Shelter fact is story.rose.1103.s1.sheltered, not route.rose; central church1111 owns four-way route selection.',
            'File marked BAD Ending is titled Normal Ending inside. Semantic ending ID unforgiven avoids guessing its rating.',
            'Only authorized {drinkname} -> 黑咖啡 preset substitution changes source display text.',
            'All scene/directive paragraphs are audit anchors, not spoken dialogue. No operating-floor art is registered.',
            'False integration.<root>.ready flags hard-gate all OR groups of blocked roots. Do not strip these gates.',
        ])
    b.audit['buildMeaning'] = 'Rows are source-mapped compiler output, NOT proof of merged source workbooks, generated data or Unity/cafe runtime verification.'
    b.audit['runtimeDependencies'] = ['targeted CafeWorld narrative-order API for closed/morning service', 'taste attempt outcome vs serve retry semantics', 'any.hot actual-temperature>=40 selector, not any product alone', 'blackcoffee.sweet/unsweetened actual sugar positive/zero, distinct real cup identities', 'root-local presentation guard settlement', 'card LetterRead no-envelope cancellation semantics']
    b.audit['dependenciesByEvent'] = {rid: sorted({c['target'] for c in b.rows['Condition'] if c['ownerType']=='event' and c['ownerId']==rid and c['condType']=='BOOL_EQ' and c['value']=='true'}) for rid in [e['eventId'] for e in b.rows['Event']]}
    blocked_local = set(b.audit['constructionBlockedEvents'])
    inherited = set()
    while True:
        new = {rid for rid,deps in b.audit['dependenciesByEvent'].items() if any('story.'+parent+'.completed' in deps for parent in blocked_local|inherited)}-blocked_local-inherited
        if not new:
            break
        inherited.update(new)
    b.audit['transitivelyBlockedByRosePrerequisites'] = sorted(inherited)
    result = dict(b.rows)
    result['_replace_roots'] = ['rose.1009.s1','rose.1011.s1','rose.1016.s1','rose.1103.s1']
    result['_audit'] = b.audit
    return result
