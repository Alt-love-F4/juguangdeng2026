"""Focused static/presentation-model regression tests; NOT Unity/host acceptance.
Run: python -B -m unittest discover -s Tools/Narrative/Content -p test_revolution.py -v
"""
from collections import defaultdict
import copy
import hashlib
import importlib.util
import itertools
from pathlib import Path
import re
import unittest
from unittest.mock import patch

SPEC=importlib.util.spec_from_file_location("revolution",Path(__file__).with_name("revolution.py"))
rev=importlib.util.module_from_spec(SPEC);SPEC.loader.exec_module(rev)
PROJECT=Path(__file__).resolve().parents[3]

class PresentationModel:
    """Test-only truth-table oracle over emitted rows, no production engine."""
    def __init__(self,data,choices=(),facts=None,bars=None):
        self.data=data;self.choices=set(choices);self.facts=facts or {};self.bars=bars or {};self.checks={}
        self.page_map={p["pageId"]:p for p in data["ConversationPage"]}
        self.texts={t["key"]:t["text"] for t in data["Text"]}

    def token(self,token):
        if token.startswith("not:"): return not self.token(token[4:])
        if token.startswith("choice:"): return token[7:] in self.choices
        if token.startswith("anyChoice:"):return any(c in self.choices for c in token[10:].split(";"))
        if token.startswith("check:"):return self.checks.get(token[6:],False)
        if token.startswith("bar:"):
            key,op,value=token[4:].rsplit(":",2)
            if key not in self.bars:return False
            x,y=self.bars[key],int(value)
            return {"ge":x>=y,"gt":x>y,"le":x<=y,"lt":x<y,"eq":x==y,"ne":x!=y}[op]
        return self.facts.get(token,False)

    def visible(self,guard):
        return all(self.token(t) for t in (guard or "").split("|") if t)

    def condition(self,row):
        typ,key,value=row["condType"],row["target"],row["value"]
        if typ=="BOOL_EQ":return self.token(key)==(value=="true")
        if key not in self.bars:return False
        n=self.bars[key]
        if typ=="BAR_GTE":return n>=int(value)
        if typ=="BAR_BETWEEN":
            a,b=map(int,value.split(","));return a<=n<=b
        raise AssertionError(typ)

    def conditions(self,owner_type,owner):
        groups=defaultdict(list)
        for r in self.data["Condition"]:
            if r["ownerType"]==owner_type and r["ownerId"]==owner:groups[r["groupIndex"]].append(self.condition(r))
        return not groups or any(all(values) for values in groups.values())

    def run(self,root):
        pages=sorted((p for p in self.data["ConversationPage"] if p["presentationKey"]==root+".conversation"),key=lambda p:p["seq"])
        visited=[];lines=[]
        for p in pages:
            if not self.visible(p.get("pageCondKeys")):continue
            visited.append(p)
            if p["stepType"]=="End":break
            if p["stepType"]=="PassiveCheck":
                groups=defaultdict(list)
                for c in self.data["AbilityCheckCondition"]:
                    if c["checkId"]==p["checkId"]:groups[c["groupIndex"]].append(self.bars.get("player."+c["attributeId"],0)>=c["threshold"])
                result=any(all(g) for g in groups.values());self.checks[p["checkId"]]=result
                if not result:continue
            lines.extend(l for l in self.data["ConversationLine"] if l["pageId"]==p["pageId"])
        return visited,lines

    def effects(self,root,completed=False):
        # Explicit contract assumption: no root/choice effect before completion.
        if not completed:return []
        owners={root}|{c["resultOwnerId"] for c in self.data["Choice"] if c["choiceId"] in self.choices}
        return [r for r in self.data["Result"] if r["eventId"] in owners and self.conditions("result",r["resultId"])]

class RevolutionCompilerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.data=rev.build(PROJECT)

    def source(self,root):return next(s for s in self.data["_audit"]["sources"] if s["root"]==root)
    def root_conditions(self,root):return [r for r in self.data["Condition"] if r["ownerType"]=="event" and r["ownerId"]==root]
    def paragraph_numbers(self,lines):return {int(re.search(r"\.line\.p(\d+)",l["lineId"])[1]) for l in lines}

    def test_check_guards_follow_the_route_that_runs_the_check(self):
        """A conditional PassiveCheck may never run; its route must be tested before check:<id>
        (10.14 language3 aborted the whole morning root and looped the retry page)."""
        split=lambda s:[a for a in (s or "").split("|") if a]
        checks=defaultdict(list)
        for p in self.data["ConversationPage"]:
            if p.get("stepType")=="PassiveCheck" and p.get("checkId"):
                checks[(p["presentationKey"],p["checkId"])].append(set(split(p.get("pageCondKeys"))))
        for p in self.data["ConversationPage"]:
            atoms=split(p.get("pageCondKeys"))
            for i,atom in enumerate(atoms):
                bare=atom
                while bare.startswith("not:"):bare=bare[4:]
                routes=checks.get((p["presentationKey"],bare[6:])) if bare.startswith("check:") else None
                if routes:
                    self.assertTrue(any(r<=set(atoms[:i]) for r in routes),p["pageId"]+": "+p["pageCondKeys"])

    def test_twelve_current_documents_all_2905_anchors(self):
        self.assertEqual(12,len(self.data["_audit"]["sources"]))
        self.assertEqual(2905,sum(s["nonempty_paragraphs"] for s in self.data["_audit"]["sources"]))
        for s in self.data["_audit"]["sources"]:
            self.assertEqual(list(range(1,s["nonempty_paragraphs"]+1)),[a["paragraph"] for a in s["anchors"]])
            self.assertTrue(all(a["role"] for a in s["anchors"]))
            self.assertEqual(64,len(s["sha256"]))

    def test_c29_is_628_fully_scripted_not_blank(self):
        s=self.source("revolution.1029.s1")
        self.assertEqual(628,s["nonempty_paragraphs"])
        self.assertGreater(sum(a["role"]=="dialogue" for a in s["anchors"]),480)
        self.assertTrue(any(a["paragraph"]==622 and a["role"]=="directive" for a in s["anchors"]))

    def test_source_drift_fails_closed(self):
        original=rev.paragraphs
        def altered(path):
            ps=original(path)
            if "10.29" in path.name:ps[4]+="changed"
            return ps
        with patch.object(rev,"paragraphs",side_effect=altered):
            with self.assertRaisesRegex(ValueError,"Current source changed"):rev.build(PROJECT)

    def test_build_is_deterministic_and_protected_files_untouched(self):
        files=[PROJECT/"Assets/ConfigTables/Data/Narrative/Narrative.xlsx",PROJECT/"Assets/ConfigTables/Data/Shared/Text.xlsx",*sorted((PROJECT/rev.SOURCE_DIR).glob("*.docx"))]
        hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
        self.assertEqual(self.data,rev.build(PROJECT))
        self.assertEqual(hashes,{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in files})

    def test_only_own_roots_are_replaced_oct07_untouched(self):
        self.assertEqual({v[0] for v in rev.ROOTS.values()},set(self.data["_replace_roots"]))
        self.assertNotIn("oct07.s2",self.data["_replace_roots"])
        for sheet,rows in self.data.items():
            if sheet.startswith("_"):continue
            for row in rows:self.assertFalse(any(str(v).startswith("oct07.s2") for k,v in row.items() if k in ("eventId","pageId","lineId","presentationKey","key")))

    def test_no_duplicate_sheet_keys(self):
        for name in rev.SHEETS:
            rows=self.data[name]
            if rows:
                primary=next(iter(rows[0]));keys=[r[primary] for r in rows]
                self.assertEqual(len(keys),len(set(keys)),name)

    def test_row_fields_are_existing_source_headers_or_agreed_extensions(self):
        # No runtime or schema dependency; these are the source header spellings.
        allowed={"Event":"eventId eventKind actorId setupKey onceOnly logTag presentationClass presentationKey presentationLogTag eventProperty","EventDate":"eventId day timeSlot repeatRule priority","SetupParams":"setupKey tplName slotValues skipAble autoAdvance","PresentationIndex":"presentationKey presentationClass logTag","ConversationPage":"pageId presentationKey seq pageCondKeys stepType bgOverride optionGroupKey checkId readingKey","ConversationLine":"lineId pageId seq speakerKey side textKey emote speakerNameKey","ReadingPage":"pageId presentationKey seq","ReadingLine":"lineId pageId seq mode textKey","Choice":"choiceId groupKey textKey resultOwnerId seq","Condition":"conditionId ownerType ownerId groupIndex condType target value","ConditionKey":"key nameText initialValue onlySetTrue","Result":"resultId eventId seq resultType target value note","AbilityCheck":"checkId mode comment","AbilityCheckCondition":"conditionId checkId attributeId threshold groupIndex seq","Ending":"endingId enabled priority titleText messageText","Object":"objId objType nameText tags","Text":"key text comment"}
        for sheet,fields in allowed.items():
            for row in self.data[sheet]:self.assertFalse(set(row)-set(fields.split()),(sheet,row))

    def test_no_source_if_labels_or_stage_directions_in_dialogue(self):
        model=PresentationModel(self.data)
        for line in self.data["ConversationLine"]:
            text=model.texts[line["textKey"]]
            self.assertFalse(re.match(r"^(?:\[|【|（(?:若|如果|检定)|选项|检定成功|检定失败|MA70\.|RE70\.)",text),text)
        for row in self.data["Choice"]:
            self.assertNotRegex(model.texts[row["textKey"]],r"检定|倾向[+-]|好感[+-]")

    def test_all_completion_known_and_local_flags_initially_false(self):
        for key in self.data["ConditionKey"]:
            self.assertIs(key["initialValue"],False)
            self.assertIs(key["onlySetTrue"],True)

    def test_c13_requires_real_neutral_completion(self):
        r="revolution.1013.s2";conds=self.root_conditions(r)
        self.assertTrue(any(c["target"]=="story.neutral.1013.s2.completed" and c["value"]=="true" for c in conds))
        self.assertIn({"from":"neutral.1013.s2","to":r,"when":"accepted root completion only"},self.data["_audit"]["externalQueueEdges"])
        self.assertFalse(any(e["eventId"]==r for e in self.data["EventDate"]))

    def test_c14_requires_real_rose1011_completion(self):
        self.assertTrue(any(c["target"]=="story.rose.1011.s1.completed" and c["value"]=="true" for c in self.root_conditions("revolution.1014.s1")))

    def test_calendar_entry_phase_and_once_save_contract(self):
        for e in self.data["Event"]:
            self.assertTrue(e["onceOnly"]);self.assertIn("blocksSave=true",e["eventProperty"])
            self.assertTrue(any(c["condType"]=="BAR_BETWEEN" and c["target"]=="world.calendar" for c in self.root_conditions(e["eventId"])))
        for row in self.data["EventDate"]:
            expected={"morning":"MorningBeforeMap","noon":"AfterBusiness","night":"BeforeNight"}[row["timeSlot"]]
            event=next(e for e in self.data["Event"] if e["eventId"]==row["eventId"])
            self.assertIn("entry="+expected,event["eventProperty"])

    def test_optional_blockers_do_not_disable_any_story_root(self):
        self.assertEqual(0,self.data["_audit"]["summary"]["blocked_roots"])
        self.assertEqual(2,self.data["_audit"]["summary"]["pending_optional_nodes"])
        for e in self.data["Event"]:
            self.assertNotIn("blockedBy=",e["eventProperty"])
            self.assertFalse(any(c["target"].startswith("integration.") for c in self.root_conditions(e["eventId"])))
        for issue in self.data["_audit"]["unresolvedDependencies"]:
            self.assertFalse(issue["blocksRoot"])
            self.assertIn(issue["scope"],("optional-dialogue","optional-effect"))
            flag=next(c for c in self.data["ConditionKey"] if c["key"]==issue["guard"])
            self.assertIs(flag["initialValue"],False)
            self.assertFalse(any(r["target"]==issue["guard"] for r in self.data["Result"]))

    def test_c14_normal_baseline_can_start_complete_and_produce_sorel(self):
        root="revolution.1014.s1"
        facts={"story.rose.1011.s1.completed":True,"story.oct07.s2.completed":True}
        model=PresentationModel(self.data,[root+".c1.1",root+".c2.1",root+".c4.1",root+".c5.1"],facts,
                                {"world.calendar":9,"faction.revolution":0,"player.language":3})
        self.assertTrue(model.conditions("event",root))
        pages,lines=model.run(root)
        self.assertIn(192,self.paragraph_numbers(lines))
        self.assertEqual("End",pages[-1]["stepType"])
        results=model.effects(root,True)
        self.assertTrue(any(r["target"]=="known.sorel" and r["value"]=="true" for r in results))
        self.assertTrue(any(r["target"]=="story."+root+".completed" for r in results))
        self.assertFalse(any("trust" in r["target"].lower() for r in results))
        self.assertEqual([],model.effects(root,False))

    def test_1122_unknown_optional_check_is_local_not_a_root_gate(self):
        root="revolution.route.1122.s1"
        model=PresentationModel(self.data,[root+".c2.1",root+".c3.3",root+".c4.2"])
        pages,lines=model.run(root);nums=self.paragraph_numbers(lines)
        self.assertFalse({233,234}&nums)
        self.assertTrue({235,258,305,309}<=nums)
        self.assertFalse(297 in nums)
        self.assertEqual("End",pages[-1]["stepType"])
        self.assertFalse(any(c["checkId"].startswith(root) for c in self.data["AbilityCheck"]))
        facts={"route.revolution":True,"story.revolution.route.1115.s1.completed":True}
        self.assertTrue(PresentationModel(self.data,facts=facts,bars={"world.calendar":48}).conditions("event",root))

    def test_c29_three_release_paths_never_flatten(self):
        root="revolution.1029.s1"
        for name in rev.RELEASE:
            for rose in (rev.ROSE_YES,rev.ROSE_NO):
                facts={key:key==rev.RELEASE[name] for key in rev.RELEASE.values()};facts.update({rev.ROSE_YES:rose==rev.ROSE_YES,rev.ROSE_NO:rose==rev.ROSE_NO})
                choices=[root+".c2a.2",root+".c3.1"]
                _,lines=PresentationModel(self.data,choices,facts).run(root);nums=self.paragraph_numbers(lines)
                for other,(lo,hi) in {"persuasion":(87,112),"bribery":(115,153),"rescue":(156,198)}.items():
                    self.assertEqual(other==name,bool(nums&set(range(lo,hi+1))),(name,other))
                self.assertEqual(rose==rev.ROSE_YES,209 in nums)
                self.assertEqual(rose==rev.ROSE_NO,247 in nums)
                self.assertEqual(rose==rev.ROSE_YES,519 in nums)
                self.assertEqual(rose==rev.ROSE_NO,535 in nums)

    def test_c29_eight_dnf_arms_keep_real_parents_date_and_exclusive_origins(self):
        rows=self.root_conditions("revolution.1029.s1");groups=defaultdict(list)
        for row in rows:groups[row["groupIndex"]].append(row)
        self.assertEqual(8,len(groups))
        for group in groups.values():
            targets={c["target"] for c in group}
            self.assertIn("world.calendar",targets)
            self.assertIn("story.revolution.1028.s1.completed",targets)
            self.assertIn("story.church.1020.s1.completed",targets)
            rose_yes=next(c["value"]=="true" for c in group if c["target"]==rev.ROSE_YES)
            self.assertEqual(rose_yes,"story.rose.1026.s2.completed" in targets)
            self.assertIn(rev.CLEARED,targets)
            self.assertTrue(set(rev.RELEASE.values())<=targets)
            cleared=next(c["value"]=="true" for c in group if c["target"]==rev.CLEARED)
            self.assertEqual(not cleared,"story.church.1023.s1.completed" in targets)
            self.assertFalse(any(t.startswith("integration.") for t in targets))
            self.assertTrue({rev.ROSE_YES,rev.ROSE_NO}<=targets)

    def test_c29_unknown_or_contradictory_origins_cannot_start(self):
        root="revolution.1029.s1"
        origins=[rev.CLEARED,*rev.RELEASE.values()]
        facts={c["target"]:True for c in self.root_conditions(root) if c["condType"]=="BOOL_EQ" and c["target"].endswith(".completed")}
        facts["story."+root+".completed"]=False
        for origin_values in itertools.product((False,True),repeat=4):
            facts.update(dict(zip(origins,origin_values)))
            for rose_values in itertools.product((False,True),repeat=2):
                facts.update(dict(zip((rev.ROSE_YES,rev.ROSE_NO),rose_values)))
                model=PresentationModel(self.data,facts=facts,bars={"world.calendar":24})
                expected=sum(origin_values)==1 and sum(rose_values)==1
                self.assertEqual(expected,model.conditions("event",root),(origin_values,rose_values))

    def test_c29_cleared_path_does_not_require_or_claim_oct23_release(self):
        root="revolution.1029.s1"
        facts={rev.CLEARED:True,rev.ROSE_YES:True,"story.church.1020.s1.completed":True,
               "story.revolution.1028.s1.completed":True,"story.rose.1026.s2.completed":True}
        model=PresentationModel(self.data,[root+".c2a.1",root+".c3.1"],facts,{"world.calendar":24})
        self.assertTrue(model.conditions("event",root))
        self.assertNotIn("story.church.1023.s1.completed",facts)
        _,lines=model.run(root);nums=self.paragraph_numbers(lines)
        self.assertTrue({51,64,70,81,199,201,579}<=nums)
        self.assertFalse(nums&set(range(83,199)))
        self.assertFalse(any(r["target"] in rev.RELEASE.values() for r in model.effects(root,True)))
        for outcome in rev.RELEASE.values():
            invalid=PresentationModel(self.data,facts={**facts,outcome:True},bars={"world.calendar":24})
            self.assertFalse(invalid.conditions("event",root))
        # Neither a cleared fact without its real parent nor a generic neutral
        # completion is sufficient to prove that the player was never detained.
        no_parent={**facts,"story.church.1020.s1.completed":False,"story.neutral.1013.s2.completed":True}
        self.assertFalse(PresentationModel(self.data,facts=no_parent,bars={"world.calendar":24}).conditions("event",root))

    def test_c29_safe_refusal_does_not_require_or_recall_cooperation_only_oct26(self):
        root="revolution.1029.s1"
        facts={rev.RELEASE["rescue"]:True,rev.ROSE_NO:True,"story.church.1020.s1.completed":True,
               "story.church.1023.s1.completed":True,"story.revolution.1028.s1.completed":True}
        model=PresentationModel(self.data,[root+".c3.1"],facts,{"world.calendar":24})
        self.assertTrue(model.conditions("event",root))
        _,lines=model.run(root);nums=self.paragraph_numbers(lines)
        self.assertFalse(nums&set(range(268,285)))
        self.assertTrue({247,285,287,519+16,579}<=nums)
        cooperated={**facts,rev.ROSE_NO:False,rev.ROSE_YES:True}
        self.assertFalse(PresentationModel(self.data,facts=cooperated,bars={"world.calendar":24}).conditions("event",root))
        cooperated["story.rose.1026.s2.completed"]=True
        model=PresentationModel(self.data,[root+".c2a.1",root+".c3.1"],cooperated,{"world.calendar":24})
        self.assertTrue(model.conditions("event",root))
        _,lines=model.run(root);self.assertTrue({268,270,284,285}<=self.paragraph_numbers(lines))

    def test_c29_accept_refuse_results_are_exclusive_and_tail_only(self):
        root="revolution.1029.s1"
        for opt in (1,2):
            model=PresentationModel(self.data,[root+f".c3.{opt}"])
            self.assertEqual([],model.effects(root,completed=False))
            targets={r["target"] for r in model.effects(root,completed=True)}
            self.assertEqual(opt==1,rev.COOP in targets);self.assertEqual(opt==2,rev.REFUSED in targets)
            _,lines=model.run(root);nums=self.paragraph_numbers(lines)
            self.assertEqual(opt==1,579 in nums);self.assertEqual(opt==2,601 in nums)

    def test_c14_reciprocity_has_language_and_projected_faction_guard(self):
        root="revolution.1014.s1"
        for a,b,old,language in itertools.product(range(1,5),range(1,4),(-2,0,2),(2,3)):
            choices=[root+f".c4.{a}",root+f".c5.{b}"]
            bars={"player.language":language,"faction.revolution":old}
            model=PresentationModel(self.data,choices,bars=bars);_,lines=model.run(root)
            should=language>=3 and old+(1 if a==3 else 0)+{1:2,2:1,3:-1}[b]>=2
            self.assertEqual(should,192 in self.paragraph_numbers(lines),(a,b,old,language))

    def test_political_specialization_or_wisdom_no_duplicate_first_success_line(self):
        root="revolution.1107.s1"
        for wisdom,politics in itertools.product((0,5),(False,True)):
            model=PresentationModel(self.data,facts={rev.POLITICS:politics},bars={"player.wisdom":wisdom})
            _,lines=model.run(root);nums=self.paragraph_numbers(lines)
            self.assertEqual(wisdom>=5 or politics,154 in nums)
            self.assertEqual(not(wisdom>=5 or politics),162 in nums)
            self.assertLessEqual(sum(".line.p154" in l["lineId"] for l in lines),1)

    def test_attribute_thresholds_are_current_source_values(self):
        rows=self.data["AbilityCheckCondition"]
        self.assertTrue(any(r["attributeId"]=="wisdom" and r["threshold"]==12 for r in rows))
        self.assertTrue(any(r["attributeId"]=="language" and r["threshold"]==10 for r in rows))
        for r in rows:self.assertIn(r["attributeId"],("language","courage","wisdom","perception"))
        self.assertTrue(any(c["ownerType"]=="choice" and c["target"]=="player.courage" and c["value"]=="18" for c in self.data["Condition"]))

    def test_1123_three_identical_shoot_options_not_invented_assassination_paths(self):
        root="revolution.route.1123.s1";texts={t["key"]:t["text"] for t in self.data["Text"]}
        opts=[c for c in self.data["Choice"] if c["groupKey"]==root+".shoot"]
        self.assertEqual(["开枪"]*3,[texts[c["textKey"]] for c in opts])
        rows=[r for r in self.data["AbilityCheckCondition"] if r["checkId"]==root+".check.mary_final"]
        self.assertEqual({1,2,3},{r["groupIndex"] for r in rows})

    def test_1124_end01_does_not_fall_through_into_end02(self):
        root="revolution.route.1124.s1"
        for opt,end,title in ((1,25,"revolution.end01"),(2,196,"revolution.end02")):
            model=PresentationModel(self.data,[root+f".c0.{opt}",root+".c1.1",root+".c2.1"])
            pages,lines=model.run(root);nums=self.paragraph_numbers(lines)
            self.assertIn(f".end.{end:03}.",pages[-1]["pageId"])
            self.assertEqual(opt==1,15 in nums);self.assertEqual(opt==2,27 in nums)
            targets={r["target"] for r in model.effects(root,True)}
            self.assertIn("story."+title+".selected",targets);self.assertIn(rev.TERMINAL,targets)
            self.assertNotIn("story.revolution.end0"+str(3-opt)+".selected",targets)

    def test_terminal_has_real_existing_ending_sheet_rows_and_no_day_money_copy(self):
        self.assertEqual({"revolution.end01","revolution.end02"},{e["endingId"] for e in self.data["Ending"]})
        for e in self.data["Ending"]:
            self.assertTrue(e["enabled"])
            self.assertTrue(any(c["ownerType"]=="ending" and c["ownerId"]==e["endingId"] and c["target"]==rev.TERMINAL for c in self.data["Condition"]))
        self.assertFalse(any(r["resultType"] in ("ADVANCE_DAY","ITEM_ADD","MONEY_ADD","MONEY_REMOVE") for r in self.data["Result"]))
        root="revolution.route.1124.s1"
        event=next(e for e in self.data["Event"] if e["eventId"]==root)
        self.assertNotIn("blockedBy",event["eventProperty"])
        pages=[p for p in self.data["ConversationPage"] if p["presentationKey"]==root+".conversation"]
        self.assertFalse(any(p["stepType"]=="Gameplay" for p in pages))
        self.assertFalse(any(r["resultType"]=="QUEUE_EVENT" and r["eventId"]==root for r in self.data["Result"]))
        bootstrap=(PROJECT/"Assets/Scripts/GameFlow/UI/GameFlowBootstrap.cs").read_text("utf-8")
        self.assertIn("EndingSelected += OnEndingSelected",bootstrap)
        self.assertIn("SetSessionState(GameSessionState.GameOver)",bootstrap)
        self.assertIn("dayFlow?.StopForGameOver()",bootstrap)
        self.assertIn("Runtime?.StopForGameOver()",bootstrap)

    def test_cancellation_contract_no_result_attached_to_page_or_reading(self):
        events={r["eventId"] for r in self.data["Event"]};owners={r["resultOwnerId"] for r in self.data["Choice"]}
        for result in self.data["Result"]:
            self.assertIn(result["eventId"],events|owners)
            self.assertIn("cancellation discards",result["note"])
        self.assertTrue(all(not c.get("successEventId") and not c.get("successResultOwnerId") and not c.get("failureResultOwnerId") for c in self.data["AbilityCheck"]))

    def test_known_producers_are_truthful_and_eugene_not_fabricated(self):
        expected={"known.sorel":"revolution.1014.s1","known.mary":"revolution.1029.s1","known.lesou":"revolution.1107.s1"}
        for flag,root in expected.items():
            rows=[r for r in self.data["Result"] if r["target"]==flag]
            self.assertEqual(1,len(rows));self.assertEqual(root,rows[0]["eventId"])
        self.assertFalse(any(r["target"]=="known.eugene" for r in self.data["Result"]))
        self.assertTrue(set(rev.KNOWN)<={k["key"] for k in self.data["ConditionKey"]})

    def test_character_catalog_and_mary_identity_caption_do_not_leak_jacques(self):
        catalog=(PROJECT/"Assets/Resources/Characters/CharacterCatalog.asset").read_text("utf-8")
        ids=set(re.findall(r"characterId: (\S+)",catalog));generics={o["objId"] for o in self.data["Object"]}
        texts={t["key"]:t["text"] for t in self.data["Text"]}
        for line in self.data["ConversationLine"]:
            speaker=line.get("speakerKey")
            if speaker:self.assertIn(speaker,ids|generics)
            if speaker=="marie":self.assertIn(texts[line["speakerNameKey"]],("玛丽","玛丽（？）","？？？"))
        self.assertTrue(any(l.get("speakerKey")=="sorel" for l in self.data["ConversationLine"]))
        self.assertFalse(any(l.get("speakerKey") in ("emmanuel","mary","lesoudup") for l in self.data["ConversationLine"]))
        epilogue = [l for l in self.data["ConversationLine"] if l.get("speakerKey")=="duval" and l["lineId"].startswith("revolution.route.1124")]
        self.assertEqual(64, len(epilogue))
        self.assertTrue(all(l["side"] == "none" for l in epilogue))
        self.assertTrue(all(texts[l["speakerNameKey"]] == "杜瓦尔" for l in epilogue))

    def test_only_verified_resource_keys_and_no_operating_floor_art(self):
        valid=set(re.findall(r"  - key: (\S+)",(PROJECT/"Assets/Resources/Dialogue/NarrativeStageVisuals.asset").read_text("utf-8")))
        for p in self.data["ConversationPage"]:
            if p.get("bgOverride"):self.assertIn(p["bgOverride"],valid)
            self.assertNotRegex(str(p.get("bgOverride","")),r"SCE_|\.png|一楼|二楼|三楼")
        self.assertTrue(self.data["_audit"]["artPending"])

    def test_pending_remote_art_is_nonblocking_and_does_not_inherit_cafe(self):
        audit=self.data["_audit"]
        self.assertTrue(audit["artPending"])
        for issue in audit["artPending"]:
            self.assertFalse(issue["blocksRoot"])
            self.assertEqual("church.execution.black",issue["fallback"])
        model=PresentationModel(self.data,["revolution.route.1124.s1.c0.2","revolution.route.1124.s1.c1.1","revolution.route.1124.s1.c2.1"])
        _,lines=model.run("revolution.route.1124.s1")
        for line in lines:
            if any(f".line.p{p:03}" in line["lineId"] for p in (4,33)):
                self.assertEqual("church.execution.black",model.page_map[line["pageId"]]["bgOverride"])

    def test_letters_and_diagram_are_reading_not_dialogue(self):
        for page in self.data["ConversationPage"]:
            if page["stepType"] in ("LetterRead","LetterReveal"):
                self.assertTrue(page.get("readingKey"));self.assertFalse(any(l["pageId"]==page["pageId"] for l in self.data["ConversationLine"]))
        for root,paragraph in (("revolution.1014.s1",17),("revolution.route.1111.s2",10),("revolution.route.1122.s1",18)):
            self.assertFalse(any(l["lineId"].startswith(root+f".line.p{paragraph:03}") for l in self.data["ConversationLine"]))

    def test_current_c13_c14_text_fixes_not_old_import(self):
        texts={t["key"]:t["text"] for t in self.data["Text"]}
        self.assertEqual("这家咖啡馆，或许承载着叔叔曾经的回忆，而阿黛尔也很珍重这里。",texts["revolution.1013.s2.text.p065"])
        self.assertEqual("⌈这话说得倒是直接——那么，你认为应该由谁来统治这个国家呢？⌋",texts["revolution.1014.s1.text.p117"])
        report=self.data["_audit"]["configuredTextDiff"]
        self.assertEqual("compared",report["status"])
        self.assertEqual({"revolution.1013.s2","revolution.1014.s1","revolution.1028.s1"},{r["root"] for r in report["roots"]})

    def test_every_playable_source_anchor_has_merged_output_reference(self):
        for source in self.data["_audit"]["sources"]:
            for anchor in source["anchors"]:
                if anchor["role"] in ("dialogue","choice","reading","reading_label","end","check","choice_label"):
                    self.assertTrue(anchor["outputs"],(source["root"],anchor))
        for root in rev.ROOTS.values():
            done="story."+root[0]+".completed"
            rows=[r for r in self.data["Result"] if r["target"]==done]
            self.assertEqual(1,len(rows),done)
            self.assertEqual(root[0],rows[0]["eventId"])

    def test_1112_hidden_clues_only_produced_on_actual_success(self):
        root="revolution.route.1112.s1"
        for wisdom,language in itertools.product((11,12),(9,10)):
            model=PresentationModel(self.data,[root+".c1.1",root+".c2.1"],bars={"player.wisdom":wisdom,"player.language":language})
            model.run(root)
            targets={r["target"] for r in model.effects(root,True)}
            self.assertEqual(wisdom>=12,"story.revolution.mary.contradiction.known" in targets)
            self.assertEqual(language>=10,"story.revolution.mary.unwilling.known" in targets)
            self.assertEqual([],model.effects(root,False))

    def test_only_route_1111_canonical_root_no_competing_route_producer(self):
        self.assertIn("revolution.route.1111.s2",{e["eventId"] for e in self.data["Event"]})
        for event in self.data["Event"]:
            if ".route." in event["eventId"]:
                self.assertTrue(any(c["target"]=="route.revolution" and c["value"]=="true" for c in self.root_conditions(event["eventId"])))
        self.assertFalse(any(r["target"].startswith("route.") for r in self.data["Result"]))

if __name__=="__main__":unittest.main()
