"""Focused source/compiler tests, not substitutes for Unity/real cafe tests.
Run with the bundled Python: -B -m unittest Tools.Narrative.Content.test_rose -v
"""
from collections import defaultdict
import hashlib
import itertools
import re
from pathlib import Path
import unittest
from unittest.mock import patch

from Tools.Narrative.Content import rose

PROJECT = Path(__file__).resolve().parents[3]


class Walk:
    """Small contract-only graph interpreter for branch/settlement assertions."""
    def __init__(self, bundle, root, flags=None, bars=None, picks=(), attempts=None):
        self.b, self.root = bundle, root
        self.flags = {r['key']:r['initialValue'] for r in bundle['ConditionKey']}
        self.flags.update(flags or {})
        self.bars = {('player.'+k if k in ('wisdom','perception','language','courage') else k):v for k,v in (bars or {}).items()}
        self.picks = iter(picks)
        self.selected = set()
        self.attempts = {k:iter(v) for k,v in (attempts or {}).items()}
        self.gameplay = {}
        self.checks = {}
        self.seen, self.lines, self.menus, self.operations = [], [], [], []
        self.complete = False

    def guard(self, text):
        if not text:
            return True
        if '|' in text:
            return all(self.guard(g) for g in text.split('|'))
        if text.startswith('not:'):
            return not self.guard(text[4:])
        if text.startswith('choice:'):
            return text[7:] in self.selected
        if text.startswith('anyChoice:'):
            return any(c in self.selected for c in text[10:].split(';'))
        if text.startswith('bar:'):
            _,key,op,bound = text.split(':')
            if key not in self.bars:
                return False
            a,b = self.bars[key],int(bound)
            return {'gt':a>b,'ge':a>=b,'lt':a<b,'le':a<=b,'eq':a==b,'ne':a!=b}[op]
        if text.startswith('check:'):
            return self.checks.get(text[6:],False)
        if text.startswith('gameplay:'):
            return self.gameplay.get(text[9:-7],False)
        return self.flags.get(text,False)

    def condition(self, c):
        typ,target,value = c['condType'],c['target'],c['value']
        if typ=='BOOL_EQ':
            actual = self.guard(target) if c['ownerType']=='result' else self.flags.get(target,False)
            return actual == (value=='true')
        if typ=='BAR_GTE':
            return target in self.bars and self.bars[target]>=int(value)
        if typ=='BAR_BETWEEN':
            lo,hi = map(int,value.split(','))
            return target in self.bars and lo<=self.bars[target]<=hi
        raise AssertionError('Unimplemented test condition '+typ)

    def allowed(self, owner_type, owner):
        groups = defaultdict(list)
        for c in self.b['Condition']:
            if c['ownerType']==owner_type and c['ownerId']==owner:
                groups[c['groupIndex']].append(c)
        return not groups or any(all(self.condition(c) for c in group) for group in groups.values())

    def run(self, stop_on_gameplay=False):
        event = next(e for e in self.b['Event'] if e['eventId']==self.root)
        pages = sorted((p for p in self.b['ConversationPage'] if p['presentationKey']==event['presentationKey']),key=lambda p:p['seq'])
        indices = {p['pageId']:i for i,p in enumerate(pages)}
        i,steps = 0,0
        while i<len(pages):
            steps += 1
            if steps>1200:
                raise AssertionError('Unbounded source graph')
            p = pages[i]
            if not self.guard(p.get('pageCondKeys','')):
                i += 1
                continue
            self.seen.append(p['pageId'])
            if p['stepType']=='End':
                self.complete = True
                break
            if p['stepType']=='Gameplay':
                self.operations.append(p['gameplayKey'])
                if stop_on_gameplay:
                    return self
                self.gameplay[p['pageId']] = next(self.attempts[p['pageId']],True) if p['pageId'] in self.attempts else True
            if p['stepType']=='PassiveCheck':
                conditions=[c for c in self.b['AbilityCheckCondition'] if c['checkId']==p['checkId']]
                groups=defaultdict(list)
                for c in conditions:
                    key='player.'+c['attributeId']
                    groups[c['groupIndex']].append(key in self.bars and self.bars[key]>=c['threshold'])
                passed=bool(groups) and any(all(g) for g in groups.values())
                self.checks[p['checkId']]=passed
                if not passed:
                    i+=1
                    continue
            self.lines.extend(l for l in self.b['ConversationLine'] if l['pageId']==p['pageId'])
            if p.get('optionGroupKey'):
                choices = sorted((c for c in self.b['Choice'] if c['groupKey']==p['optionGroupKey']),key=lambda c:c['seq'])
                wanted = next(self.picks,1)
                if isinstance(wanted,int) and wanted>=20:
                    chosen = next(c for c in choices if f'.choice.p{wanted:03}' in c['choiceId'])
                else:
                    chosen = choices[int(wanted)-1]
                if not self.allowed('choice',chosen['choiceId']):
                    raise AssertionError('Tried locked source choice '+chosen['choiceId'])
                self.selected.add(chosen['choiceId'])
                self.menus.append((p['optionGroupKey'],chosen['choiceId']))
            i = indices[p['nextPageId']] if p.get('nextPageId') else i+1
        if not self.complete and self.root=='rose.1009.s1' and i==len(pages):
            self.complete=True  # Original nine-page root completes naturally.
        if not self.complete:
            raise AssertionError('Graph fell off without End')
        owners = {self.root} | {c['resultOwnerId'] for c in self.b['Choice'] if c['choiceId'] in self.selected}
        # Evaluate all guards before committing any mutation (runtime transaction contract).
        self.effects = [r for r in self.b['Result'] if r['eventId'] in owners and self.allowed('result',r['resultId'])]
        for r in self.effects:
            if r['resultType']=='SET_FLAG':
                self.flags[r['target']] = r['value']=='true'
            elif r['resultType']=='BAR_ADD':
                self.bars[r['target']] = self.bars.get(r['target'],0)+int(r['value'])
        return self


class RoseContentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.b = rose.build(PROJECT)

    def walk(self, root, **kwargs):
        return Walk(self.b,root,**kwargs).run()

    def test_all17_current_sources_and_every_paragraph(self):
        audit = self.b['_audit']
        self.assertEqual(audit['sourceCount'],17)
        self.assertEqual({s['path'] for s in audit['sources']},
            {p.relative_to(PROJECT).as_posix() for p in (PROJECT/rose.SOURCE_DIR).glob('*.docx')})
        for src in audit['sources']:
            p = PROJECT/src['path']
            self.assertEqual(src['sha256'],hashlib.sha256(p.read_bytes()).hexdigest())
            self.assertEqual(len(src['coverage']),len(rose.paragraphs(p)))
            self.assertEqual(src['unmappedAnchors'],[],src['path'])
            self.assertNotIn('D:/',src['path'])
        self.assertEqual(next(s['paragraphCount'] for s in audit['sources'] if s['root']=='rose.1016.s1'),96)

    def test_oct19_rose_revisions_compile_with_resolved_author_gaps(self):
        texts = {row['key']: row['text'] for row in self.b['Text']}
        sources = {row['root']: row for row in self.b['_audit']['sources']}
        for root, anchors in {
            'rose.route.1115.s1': (113,),
            'rose.route.ending.bloom': (53, 58, 59),
        }.items():
            source = sources[root]
            for anchor in anchors:
                key = f'{root}.text.p{anchor:03}'
                self.assertEqual(source['paragraphs'][anchor - 1], texts[key], key)
        ideal = texts['rose.route.1115.s1.text.p113']
        self.assertIn('十五年都未发生过叛乱', ideal)
        self.assertNotIn('十五年都发生过叛乱', ideal)
        event = next(row for row in self.b['Event'] if row['eventId'] == 'rose.route.1115.s1')
        self.assertNotIn('blockedBy=', event['eventProperty'])
        self.assertEqual(sources['rose.route.1115.s1']['blockedBy'],[])

    def test_build_is_deterministic_and_no_writes(self):
        with patch.object(Path,'write_text',side_effect=AssertionError('compiler wrote text')), \
             patch.object(Path,'write_bytes',side_effect=AssertionError('compiler wrote bytes')):
            self.assertEqual(self.b,rose.build(PROJECT))
        self.assertEqual(self.b['_replace_roots'],[e['eventId'] for e in self.b['Event']])
        self.assertTrue(all(root.startswith('rose.') for root in self.b['_replace_roots']))
        self.assertIn('rose.1025.s2',self.b['_replace_roots'])

    def test_spoken_npcs_get_portraits_without_staging_player_or_narration(self):
        lines = self.b['ConversationLine']
        for line in lines:
            speaker = line.get('speakerKey')
            expected = 'right' if speaker and speaker != 'player' else 'none'
            self.assertEqual(expected, line['side'], line['lineId'])
        for root, speakers in {
            'rose.1009.s1': {'rose.butake', 'rose.envoy'},
            'rose.1011.s1': {'adele', 'sparrow', 'sylvain'},
        }.items():
            staged = {l.get('speakerKey') for l in lines if l['pageId'].startswith(root+'.') and l['side']=='right'}
            self.assertEqual(speakers, staged, root)

    def test_row_keys_unique_and_references_real(self):
        pk = {'Text':'key','ConditionKey':'key','Object':'objId','Event':'eventId','SetupParams':'setupKey',
            'PresentationIndex':'presentationKey','Condition':'conditionId','Result':'resultId','EventDate':'eventId',
            'Action':'actionKey','ConversationPage':'pageId','ConversationLine':'lineId','Choice':'choiceId',
            'ReadingPage':'pageId','ReadingLine':'lineId','Ending':'endingId','AbilityCheck':'checkId','AbilityCheckCondition':'conditionId'}
        for sheet,key in pk.items():
            ids = [r[key] for r in self.b.get(sheet,[])]
            self.assertEqual(len(ids),len(set(ids)),sheet)
        text = {r['key'] for r in self.b['Text']}
        pages = {r['pageId']:r for r in self.b['ConversationPage']}
        for l in self.b['ConversationLine']:
            self.assertIn(l['textKey'],text)
            self.assertIn(l['pageId'],pages)
        for page in pages.values():
            if 'nextPageId' in page:
                self.assertEqual(pages[page['nextPageId']]['presentationKey'],page['presentationKey'])
            if page['stepType']=='Gameplay':
                self.assertRegex(page['gameplayKey'],r'^(serve|taste):')
                for req in page['gameplayKey'].split(':',1)[1].split('+'):
                    token=req.split(':')[0]
                    if token in rose.GAMEPLAY_SELECTORS:
                        base=rose.GAMEPLAY_SELECTORS[token]['baseProduct']
                        if base is not None:
                            self.assertIn(base,rose.Bundle(PROJECT).products)
                    else:
                        self.assertIn(token,rose.Bundle(PROJECT).products)
            if page['stepType']=='End':
                self.assertFalse(any(l['pageId']==page['pageId'] for l in self.b['ConversationLine']))

    def test_no_directive_or_alternate_label_is_spoken(self):
        text = {r['key']:r['text'] for r in self.b['Text']}
        for line in self.b['ConversationLine']:
            s = text[line['textKey']]
            self.assertFalse(rose.is_directive(s),(line['lineId'],s))
            self.assertNotIn('→',s,line['lineId'])
            self.assertNotIn('{drinkname}',s)
        self.assertEqual(rose.display_segments('阿黛尔走到了门外。'),[(None,'阿黛尔走到了门外。',None)])
        self.assertEqual(rose.display_segments('贝特朗·康多尔'),[(None,'贝特朗·康多尔',None)])

    def test_1011_all_short_tails_stop_before_appreciation(self):
        for nature in (1,2):
            for news in (1,2):
                x = self.walk('rose.1011.s1',bars={'perception':6,'wisdom':9},picks=[1,news,nature])
                self.assertIn('rose.1011.s1.end.short.nature',x.seen)
                self.assertFalse(x.flags[rose.APPRECIATION])
                self.assertTrue(x.flags['known.sparrow'])
                self.assertFalse(x.flags['known.sylvain'])
                self.assertFalse(any('.long.' in p for p in x.seen))
        for reason in (1,3):
            x = self.walk('rose.1011.s1',bars={'wisdom':9},picks=[1,1,3,reason])
            self.assertIn('rose.1011.s1.end.short.reason',x.seen)
            self.assertFalse(x.flags[rose.APPRECIATION])
        x = self.walk('rose.1011.s1',bars={'wisdom':2},picks=[1,1,3,2])
        self.assertIn('rose.1011.s1.end.short.wisdom',x.seen)
        self.assertFalse(x.flags[rose.APPRECIATION])

    def test_1011_real_taste_low_continues_and_long_commits(self):
        for quality in (False,True):
            x = self.walk('rose.1011.s1',bars={'wisdom':3,'perception':6},picks=[3,2,3,2],
                attempts={'rose.1011.s1.brew_gate':[quality]})
            self.assertEqual(x.operations,['taste:blackcoffee:90:1'])
            self.assertEqual('rose.1011.s1.brew_gate.failed' in x.seen,not quality)
            self.assertTrue(x.flags[rose.APPRECIATION])
            self.assertTrue(x.flags['known.sylvain'])
            self.assertIn('rose.1011.s1.long.card',x.seen)
        pending = Walk(self.b,'rose.1011.s1',bars={'wisdom':9},picks=[1,1,3,2]).run(stop_on_gameplay=True)
        self.assertFalse(pending.complete)
        self.assertFalse(pending.flags['story.rose.1011.s1.completed'])
        self.assertFalse(pending.flags['known.sylvain'])

    def test_1012_hat_and_appreciation_paths_do_not_leak(self):
        for app,hat in itertools.product((False,True),repeat=2):
            x = self.walk('rose.1012.s1',flags={rose.APPRECIATION:app,rose.HAT:hat})
            self.assertEqual('rose.1012.s1.no_appreciation' in x.seen,not app)
            self.assertEqual('rose.1012.s1.appreciation' in x.seen,app)
            self.assertEqual('rose.1012.s1.hat.helped' in x.seen,app and hat)
            self.assertEqual('rose.1012.s1.hat.not_helped' in x.seen,app and not hat)
            self.assertTrue(x.flags['known.alienor'])
        x = self.walk('rose.1012.s1',attempts={'rose.1012.s1.brew_gate':[False,True]})
        self.assertEqual(len(x.operations),2)
        self.assertIn('rose.1012.s1.brew_gate.failed',x.seen)

    def test_1016_all_six_topic_orders_exactly_once(self):
        for topics in itertools.permutations((64,65,66)):
            x = self.walk('rose.1016.s1',picks=[2,*topics,1])
            chosen_topics = [cid for group,cid in x.menus if '.choices.topics.' in group]
            self.assertEqual(len(chosen_topics),3)
            self.assertEqual([int(re.search(r'\.p(\d+)',cid).group(1)) for cid in chosen_topics],list(topics))
            self.assertEqual(x.bars['faction.rose'],1)
            self.assertTrue(x.flags['known.sade'])
        x = self.walk('rose.1016.s1',picks=[1])
        self.assertIn('rose.1016.s1.end.refused',x.seen)
        self.assertEqual(len(x.menus),1)
        self.assertTrue(x.flags['known.sade'])

    def test_1024_all_release_histories_and_question_effects(self):
        for release in rose.RELEASES:
            for question in range(1,6):
                x = self.walk('rose.1024.s2',flags={release:True},bars={'perception':9},picks=[question,2])
                self.assertEqual('rose.1024.s2.mind.persuaded' in x.seen,release==rose.PERSUASION)
                self.assertEqual('rose.1024.s2.mind.bribed' in x.seen,release==rose.BRIBE)
                self.assertEqual('rose.1024.s2.mind.rescued' in x.seen,release==rose.RESCUE)
                self.assertEqual(x.flags[rose.SAFE_REFUSAL],question==4)
                self.assertEqual(x.flags[rose.SHORT1],question!=4)
                self.assertFalse(x.flags[rose.ALLEGIANCE])
        x = self.walk('rose.1024.s2',flags={rose.RESCUE:True},bars={'perception':10},picks=[4,1])
        self.assertTrue(x.flags[rose.ALLEGIANCE])
        self.assertEqual(len(x.menus),2)  # early loyalty skips redundant final menu
        self.assertTrue(all(l.get('speakerNameKey') for l in x.lines if l.get('speakerKey')=='bertrand'))

    def test_1103_comfort_is_early_end_not_question_history(self):
        x = self.walk('rose.1103.s1',picks=[2,1])
        self.assertIn('rose.1103.s1.end.comfort',x.seen)
        self.assertNotIn('rose.1103.s1.question',x.seen)
        self.assertTrue(x.flags['church.alienor.sheltered'])
        y = self.walk('rose.1103.s1',picks=[2,2])
        self.assertIn('rose.1103.s1.question',y.seen)
        self.assertEqual(y.bars['alienor.affection'],1)

    def test_ending_conditions_are_terminal_not_calendar(self):
        for ending in self.b['Ending']:
            conds = [c for c in self.b['Condition'] if c['ownerType']=='ending' and c['ownerId']==ending['endingId']]
            self.assertGreaterEqual(len(conds),2)
            self.assertTrue(all(c['condType']=='BOOL_EQ' and c['value']=='true' for c in conds))
            self.assertTrue(any('terminal' in c['target'] for c in conds))
        for selection,pending,other in ((1,rose.POISON,rose.BLOOM),(2,rose.BLOOM,rose.POISON)):
            x = self.walk('rose.route.1122.s1',picks=[selection])
            self.assertTrue(x.flags[pending])
            self.assertFalse(x.flags[other])
            queues = [r['target'] for r in x.effects if r['resultType']=='QUEUE_EVENT']
            self.assertEqual(len(queues),1)

    def test_late_roots_keep_real_predecessor_and_exclusive_route_gates(self):
        self.assertEqual(self.b['_audit']['constructionBlockedEvents'],[])
        root='rose.route.1115.s1'
        for predecessor,rose_route,other_route in itertools.product((False,True),repeat=3):
            flags={'story.rose.route.1111.s2.completed':predecessor,
                'route.rose':rose_route,'route.church':other_route}
            self.assertEqual(Walk(self.b,root,flags=flags).allowed('event',root),
                predecessor and rose_route and not other_route)
        flags={'story.rose.route.1111.s2.completed':True,'route.rose':True,
            'story.rose.route.1115.s1.completed':True}
        self.assertFalse(Walk(self.b,root,flags=flags).allowed('event',root))

    def test_availability_flags_are_mutable_and_source_produced(self):
        for flag in ('story.adele.away','story.sylvain.unavailable','church.detention.active'):
            definition = next(r for r in self.b['ConditionKey'] if r['key']==flag)
            self.assertFalse(definition['onlySetTrue'])
            if flag=='church.detention.active':
                continue  # Church owns release/detention mutations.
            values = {r['value'] for r in self.b['Result'] if r['target']==flag}
            self.assertEqual(values,{'true','false'})
        x = self.walk('rose.route.1122.s1',picks=[2])
        self.assertTrue(x.flags['story.adele.away'])
        y = self.walk('rose.route.ending.bloom',flags=x.flags)
        self.assertFalse(y.flags['story.adele.away'])



    def test_1009_existing_threshold5_nine_pages_67_lines_and_both_tails(self):
        check='rose.1009.s1.perception'
        self.assertEqual(next(c for c in self.b['AbilityCheck'] if c['checkId']==check)['mode'],'Inline')
        condition=next(c for c in self.b['AbilityCheckCondition'] if c['checkId']==check)
        self.assertEqual((condition['attributeId'],condition['threshold']),('perception',5))
        pages=[p for p in self.b['ConversationPage'] if p['presentationKey']=='rose.1009.s1.conversation']
        lines=[l for l in self.b['ConversationLine'] if l['pageId'] in {p['pageId'] for p in pages}]
        self.assertEqual(len(pages),9)
        self.assertEqual(len(lines),67)
        self.assertEqual([p['pageId'].removeprefix('rose.1009.s1.') for p in pages],
            ['door','blindfold','reception','origin_letter','origin_uncle','request','portrait_cg','portrait_no_cg','portrait_tail'])
        self.assertEqual((pages[6]['stepType'],pages[6]['checkId'],pages[6]['bgOverride']),
            ('PassiveCheck',check,'rose.club.knight_guidance'))
        self.assertEqual(pages[7]['pageCondKeys'],'not:check:'+check)
        self.assertNotIn('bgOverride',pages[7])
        self.assertNotIn('pageCondKeys',pages[8])
        self.assertFalse(any(p['stepType']=='End' for p in pages))
        for origin,perception in itertools.product((1,2),(0,2,4,5,6)):
            x=self.walk('rose.1009.s1',picks=[origin],bars={'perception':perception})
            self.assertEqual('rose.1009.s1.origin_letter' in x.seen,origin==1)
            self.assertEqual('rose.1009.s1.origin_uncle' in x.seen,origin==2)
            passed=perception>=5
            self.assertEqual(x.checks[check],passed)
            self.assertEqual(any(l['lineId']=='rose.1009.s1.p073.cg' for l in x.lines),passed)
            self.assertEqual(any(l['lineId']=='rose.1009.s1.p073.no_cg' for l in x.lines),not passed)
            for number in range(73,77):
                self.assertEqual(sum(l['textKey']==f'rose.1009.s1.text.p{number:03}' for l in x.lines),1)
            self.assertTrue(x.flags['story.rose.1009.s1.completed'])
        self.assertEqual(sum(l['textKey']=='rose.1009.s1.text.p073' for l in lines),2)
        for number in range(74,77):
            self.assertEqual(sum(l['lineId']==f'rose.1009.s1.p{number:03}' for l in lines),1)
        registry=(PROJECT/'Assets/Resources/Dialogue/NarrativeStageVisuals.asset').read_text(encoding='utf-8')
        block=registry.split('  - key: rose.club.knight_guidance',1)[1].split('  - key:',1)[0]
        self.assertIn('placeholder: 0',block)
        meta=(PROJECT/'Assets/Art/NarrativeCG/CG03_knight_guidance.jpg.meta').read_text(encoding='utf-8')
        guid=meta.split('guid: ',1)[1].splitlines()[0].strip()
        self.assertIn('guid: '+guid,block)
        self.assertEqual(self.b['_audit']['presets']['rose1009Perception']['value'],5)
        self.assertNotIn('legacyUnityAssertionConflict',self.b['_audit']['presets']['rose1009Perception'])

    def test_1019_source_reward_only_on_help(self):
        for option in (1,2):
            x = self.walk('rose.1019.s1',picks=[option],flags={'story.adele.away':True,'story.sylvain.unavailable':True})
            for target in ('adele.affection','sylvain.affection'):
                self.assertEqual(x.bars.get(target,0),1 if option==1 else 0)
            self.assertFalse(x.flags['story.adele.away'])
            self.assertFalse(x.flags['story.sylvain.unavailable'])
            self.assertEqual(x.operations,['serve:creamcoffee:60:1+milkcoffee:60:1'])

    def test_1101_failure_does_not_inherit_success_or_double_endings(self):
        for cooperation in (True,False):
            facts={rose.COOPERATED:cooperation,rose.REFUSED:not cooperation,'story.rose.1024.s1.completed':True}
            x = self.walk('rose.1101.s1',flags=facts)
            self.assertEqual(x.flags[rose.SHORT2],not cooperation)
            self.assertEqual('rose.1101.s1.success' in x.seen,cooperation)
            self.assertEqual(x.flags['story.adele.away'],not cooperation)
            queues=[r for r in x.effects if r['resultType']=='QUEUE_EVENT']
            self.assertEqual(len(queues),0 if cooperation else 1)

    def test_1115_motive_partition_preserves_perception_revelation(self):
        x=self.walk('rose.route.1115.s1',bars={'perception':10},picks=[1])
        self.assertTrue(x.flags['story.rose.route.1115.s1.terminal.ideal_world'])
        self.assertFalse(x.flags['story.rose.route.1115.s1.plan_known'])
        self.assertFalse(x.flags[rose.FOLLOWUP])
        self.assertNotIn('rose.route.1115.s1.candor.high',x.seen)
        for perception in (0,9,10,12):
            y=self.walk('rose.route.1115.s1',bars={'perception':perception},picks=[2])
            self.assertEqual('rose.route.1115.s1.plan' in y.seen,perception>9)
            self.assertEqual(y.flags['story.rose.route.1115.s1.plan_known'],perception>9)
            self.assertEqual('rose.route.1115.s1.quiet.farewell' in y.seen,perception<=9)
            self.assertTrue(y.flags[rose.FOLLOWUP])
            self.assertFalse(y.flags['story.rose.route.1115.s1.terminal.ideal_world'])
            for anchor in range(139,179):
                self.assertEqual(any(l['textKey'].startswith(f'rose.route.1115.s1.text.p{anchor:03}') for l in y.lines),
                    perception>9 and anchor not in (161,162,163))

    def test_split_1025_invitation_and_1026_meeting_have_real_dates(self):
        dates={r['eventId']:r for r in self.b['EventDate']}
        self.assertEqual(dates['rose.1025.s2']['day'],20)
        self.assertEqual(dates['rose.1026.s2']['day'],21)
        self.assertEqual(dates['rose.1025.s2']['timeSlot'],'night')
        invite=self.walk('rose.1025.s2')
        self.assertTrue(invite.flags['story.rose.1025.s2.completed'])
        for option in (1,2,3):
            x=self.walk('rose.1026.s2',bars={'wisdom':10,'perception':9},picks=[option])
            self.assertTrue(x.flags['story.rose.1026.s2.prisoner_list_received'])
        for r in self.b['Result']:
            self.assertFalse(r['resultType'] in ('BAR_SET','BAR_ADD') and r['target']=='world.calendar')

    def test_source_anchors_drift_is_construction_blocked(self):
        original=rose.paragraphs
        def drift(path):
            rows=original(path)
            if path.name.startswith('MA70.10.09'):
                rows.append('新增未审核原稿内容')
            return rows
        with patch.object(rose,'paragraphs',side_effect=drift):
            result=rose.build(PROJECT)
        audit=next(s for s in result['_audit']['sources'] if s['root']=='rose.1009.s1')
        self.assertEqual(audit['rootStatus'],'construction-blocked')
        self.assertIn('source_anchor_drift',{b['code'] for b in audit['blockedBy']})
        for root in result['_audit']['constructionBlockedEvents']:
            conditions=[c for c in result['Condition'] if c['ownerType']=='event' and c['ownerId']==root]
            for group in {c['groupIndex'] for c in conditions}:
                self.assertTrue(any(c['groupIndex']==group and c['target']=='integration.'+root+'.ready'
                    and c['value']=='true' for c in conditions))
            self.assertFalse(any(r['resultType']=='SET_FLAG' and r['target']=='integration.'+root+'.ready' for r in result['Result']))

    def test_numeric_guards_use_real_registered_player_bar_ids(self):
        for p in self.b['ConversationPage']:
            for guard in p.get('pageCondKeys','').split('|'):
                while guard.startswith('not:'):
                    guard=guard[4:]
                if guard.startswith('bar:'):
                    key=guard.split(':')[1]
                    self.assertTrue(key.startswith(('player.','faction.')) or key.endswith('.affection'),key)
        for c in self.b['Condition']:
            if c['condType']=='BAR_GTE':
                self.assertEqual(c['target'],'player.wisdom')


    def test_1016_real_sugar_selectors_and_no_whole_root_gate(self):
        flags={rose.APPRECIATION:True,'story.rose.1011.s1.completed':True}
        pending=Walk(self.b,'rose.1016.s1',flags=flags,picks=[1])
        self.assertTrue(pending.allowed('event','rose.1016.s1'))
        pending.run(stop_on_gameplay=True)
        self.assertEqual(pending.operations,['serve:blackcoffee.sweet:60:1+blackcoffee.unsweetened:60:1'])
        self.assertFalse(pending.flags['story.rose.1016.s1.completed'])
        self.assertFalse(pending.flags['known.sade'])
        x=self.walk('rose.1016.s1',flags=flags,picks=[1],attempts={'rose.1016.s1.brew_gate':[False,True]})
        self.assertEqual(len(x.operations),2)
        self.assertIn('rose.1016.s1.brew_gate.failed',x.seen)
        self.assertTrue(x.flags['known.sade'])
        blockers=[b for b in self.b['_audit']['blockedBy'] if b['root']=='rose.1016.s1']
        self.assertTrue(any(b['code']=='light_bitter_threshold_unspecified' and b['scope']=='node' for b in blockers))
        self.assertFalse(any(b['scope']=='root' for b in blockers))

    def test_1103_real_hot_wait_and_response_require_delivery(self):
        facts={'story.rose.1101.s1.completed':True,rose.ALLEGIANCE:True}
        pending=Walk(self.b,'rose.1103.s1',flags=facts,picks=[2,1])
        self.assertTrue(pending.allowed('event','rose.1103.s1'))
        pending.run(stop_on_gameplay=True)
        self.assertEqual(pending.operations,['serve:any.hot:60:1'])
        self.assertFalse(pending.complete)
        self.assertFalse(pending.flags['church.alienor.sheltered'])
        self.assertFalse(pending.flags['known.alienor'])
        for response in (1,2):
            x=self.walk('rose.1103.s1',flags=facts,picks=[2,response])
            self.assertTrue(x.flags['story.rose.1103.s1.completed'])
            self.assertTrue(x.flags['church.alienor.sheltered'])
            self.assertTrue(x.flags['known.alienor'])
            self.assertIn('rose.1103.s1.drink.special',x.seen)
        self.assertNotIn('rose.1103.s1.drink.special',pending.seen)
        self.assertFalse(any(r['key'].startswith('integration.') for r in self.b['ConditionKey']))
        self.assertTrue(any(r['code']=='hot_drink_response' for r in self.b['_audit']['designResolutions']))

    def test_1103_refusal_is_playable_without_fake_revenue_transfer(self):
        x=self.walk('rose.1103.s1',picks=[1],bars={'cafe.money':0})
        self.assertTrue(x.flags['church.alienor.refused_shelter'])
        self.assertFalse(x.flags['church.alienor.sheltered'])
        self.assertFalse(x.flags['story.rose.1103.s1.identity_known'])
        self.assertEqual(x.bars['cafe.money'],0)
        self.assertIn('rose.1103.s1.end.refused',x.seen)
        self.assertNotIn('rose.1103.s1.shelter.accepted',x.seen)
        self.assertFalse(any(r['resultType'] in ('MONEY_ADD','MONEY_REMOVE') for r in x.effects))
        texts={r['key']:r['text'] for r in self.b['Text']}
        shown=''.join(texts[l['textKey']] for l in x.lines)
        self.assertNotIn('营业额',shown)
        self.assertIn('伞和衣服您先带着',shown)
        revisions={r['anchor']:r for r in self.b['_audit']['textRevisions'] if r['root']=='rose.1103.s1'}
        self.assertIn('营业额',revisions['P058']['original'])
        self.assertEqual(revisions['P058']['compiled'],texts['rose.1103.s1.text.p058'])

    def test_gameplay_selectors_document_real_predicates_not_fake_catalog_ids(self):
        selectors=self.b['_audit']['gameplaySelectors']
        self.assertEqual({s['token'] for s in selectors},{'any.hot','blackcoffee.sweet','blackcoffee.unsweetened'})
        hot=next(s for s in selectors if s['token']=='any.hot')
        self.assertIn('>=40',hot['predicate'])
        for s in selectors:
            if s['token'].startswith('blackcoffee.'):
                self.assertEqual(s['baseProduct'],'blackcoffee')
                self.assertIn('distinct real delivered cup',s['predicate'])
        self.assertNotIn('hotmilk',rose.Bundle(PROJECT).products)


    def test_1016_third_stance_is_neutral_not_church_or_locked(self):
        for stance,target in ((91,'faction.rose'),(92,'faction.revolution'),(93,'faction.neutral')):
            x=self.walk('rose.1016.s1',picks=[47,64,65,66,stance])
            self.assertEqual(x.bars,{target:1})
            self.assertTrue(x.flags['story.rose.1016.s1.completed'])

    def test_1024_exclusive_origins_require_real_release_and_no_detention(self):
        completed={'story.church.1020.s1.completed':True,'story.church.1023.s1.completed':True}
        for cleared,detained,*released in itertools.product((False,True),repeat=5):
            facts={**completed,rose.CLEARED:cleared,'church.detention.active':detained,
                **dict(zip(rose.RELEASES,released))}
            s1=Walk(self.b,'rose.1024.s1',flags=facts)
            s2=Walk(self.b,'rose.1024.s2',flags=facts)
            self.assertEqual(s1.allowed('event',s1.root),cleared and not detained and not any(released))
            self.assertEqual(s2.allowed('event',s2.root),not cleared and not detained and sum(released)==1)
        for release in rose.RELEASES:
            facts={release:True}
            self.assertFalse(Walk(self.b,'rose.1024.s2',flags=facts).allowed('event','rose.1024.s2'))
            facts['story.church.1023.s1.completed']=True
            x=Walk(self.b,'rose.1024.s2',flags=facts,bars={'perception':9},picks=[121,150])
            self.assertTrue(x.allowed('event',x.root))
            x.run()
            self.assertTrue(x.flags[rose.ALLEGIANCE])
            self.assertEqual(x.flags[rose.PERSUASION],release==rose.PERSUASION)
            self.assertEqual(x.flags[rose.BRIBE],release==rose.BRIBE)
            self.assertEqual(x.flags[rose.RESCUE],release==rose.RESCUE)

    def test_rose_chain_reaches_three_late_endings_without_construction_facts(self):
        # External boundary facts are supplied only by their named owning roots.
        # This is a compiler/contract walk, not a claim of Unity natural-play proof.
        for origin,perception,motive in itertools.product((rose.CLEARED,*rose.RELEASES),(9,10),(93,94)):
            with self.subTest(origin=origin,perception=perception,motive=motive):
                facts={origin:True,'story.church.1020.s1.completed':True,
                    'story.church.1023.s1.completed':origin!=rose.CLEARED}
                bars={'player.perception':perception,'player.wisdom':7}
                def run(root,picks=()):
                    nonlocal facts,bars
                    x=Walk(self.b,root,flags=facts,bars=bars,picks=picks)
                    self.assertTrue(x.allowed('event',root),(root,origin))
                    x.run()
                    facts,bars=x.flags,x.bars
                    return x
                if origin==rose.CLEARED:
                    run('rose.1024.s1',[99,127])
                else:
                    run('rose.1024.s2',[121,138] if perception>9 else [121,150])
                run('rose.1025.s2')
                run('rose.1026.s2',[29])
                facts.update({rose.COOPERATED:True,'story.revolution.1029.s1.completed':True})
                run('rose.1101.s1')
                run('rose.1103.s1',[51,108])
                facts.update({'story.church.1111.s1.completed':True,'route.rose':True})
                run('rose.route.1111.s2')
                meeting=run('rose.route.1115.s1',[motive])
                ideal='rose.route.1115.s1.ending.ideal_world'
                self.assertEqual(meeting.allowed('ending',ideal),motive==93)
                if motive==93:
                    self.assertFalse(Walk(self.b,'rose.route.1122.s1',flags=facts).allowed('event','rose.route.1122.s1'))
                    continue
                saved_facts,saved_bars=dict(facts),dict(bars)
                for pick,ending in ((63,'unforgiven'),(64,'bloom')):
                    facts,bars=dict(saved_facts),dict(saved_bars)
                    branch=run('rose.route.1122.s1',[pick])
                    root='rose.route.ending.'+ending
                    self.assertEqual([r['target'] for r in branch.effects if r['resultType']=='QUEUE_EVENT'],[root])
                    epilogue=run(root)
                    reached=[e['endingId'] for e in self.b['Ending'] if epilogue.allowed('ending',e['endingId'])]
                    self.assertEqual(reached,[root+'.ending'])
                    self.assertFalse(any(key.startswith('integration.') and value for key,value in facts.items()))

    def test_1101_refusal_all_origins_requires_real_mission(self):
        for origin in (rose.CLEARED,*rose.RELEASES):
            facts={origin:True,rose.ALLEGIANCE:True,rose.REFUSED:True,
                'story.revolution.1029.s1.completed':True}
            root='rose.1101.s1'
            self.assertFalse(Walk(self.b,root,flags=facts).allowed('event',root))
            facts['story.rose.1026.s2.completed']=True
            x=Walk(self.b,root,flags=facts)
            self.assertTrue(x.allowed('event',root))
            x.run()
            self.assertTrue(x.flags[rose.SHORT2])
            self.assertFalse(Walk(self.b,'rose.1103.s1',flags=x.flags).allowed('event','rose.1103.s1'))
            end=Walk(self.b,'rose.ending.job_requirements',flags=x.flags)
            self.assertTrue(end.allowed('event',end.root))
            end.run()
            self.assertTrue(end.allowed('ending',end.root+'.ending'))

    def test_no_production_construction_facts_or_uncovered_rose_roots(self):
        self.assertFalse(any(r['key'].startswith(('integration.','construction.')) for r in self.b['ConditionKey']))
        self.assertEqual(self.b['_audit']['transitivelyBlockedByRosePrerequisites'],[])
        replaced=set(self.b['_replace_roots'])
        for root in ('rose.1024.s1','rose.1024.s2','rose.1101.s1','rose.route.1115.s1','rose.route.1122.s1'):
            self.assertIn(root,replaced)
        self.assertEqual({(r['root'],int(r['anchor'][1:])) for r in self.b['_audit']['textRevisions']},set(rose.TEXT_REVISIONS))

    def test_unknown_selector_and_entity_are_not_authorized_products(self):
        source=next(s for s in self.b['_audit']['sources'] if s['root']=='rose.1016.s1')
        for token in ('hotmilk','blackcoffee.fictional','any.cold'):
            bundle=rose.Bundle(PROJECT)
            root=rose.Root(bundle,source,'1016')
            self.assertEqual(root.brew('probe',[(token,60,1)],17),'')
            self.assertTrue(any(b['scope']=='root' and b['code']=='missing_product_'+token for b in root.blocked))
            self.assertFalse(any(p['stepType']=='Gameplay' for p in root.pages))


if __name__=='__main__':
    unittest.main()
