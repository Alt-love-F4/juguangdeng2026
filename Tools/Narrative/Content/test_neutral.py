"""Focused compiler/graph tests. Python simulation is NOT a Unity/CafeWorld playtest."""
from collections import defaultdict
from copy import deepcopy
from hashlib import sha256
import importlib.util
from itertools import product
from pathlib import Path
import re
import unittest
from unittest.mock import patch

MODULE_PATH = Path(__file__).with_name("neutral.py")
SPEC = importlib.util.spec_from_file_location("neutral_content_under_test", MODULE_PATH)
neutral = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(neutral)
PROJECT = Path(__file__).resolve().parents[3]


class GraphModel:
    """Minimal declared-guard model to inspect branch exclusivity/root-tail plans.
    Deliberate blocked-plan inspection must be explicitly requested. Gameplay
    signals are supplied test evidence, never authored runtime auto-completions.
    """
    def __init__(self, data):
        self.data = data
        self.events = {r["eventId"]:r for r in data["Event"]}
        self.flags = {r["key"]:r["initialValue"] for r in data["ConditionKey"]}
        self.flags.update({"route."+f:False for f in ("neutral","rose","church","revolution")})
        self.flags["world.map.active"] = True
        self.groups, self.lines, self.conditions = defaultdict(list), defaultdict(list), defaultdict(list)
        self.choices = {r["choiceId"]:r for r in data["Choice"]}
        for row in data["Choice"]: self.groups[row["groupKey"]].append(row)
        for row in data["ConversationLine"]: self.lines[row["pageId"]].append(row)
        for row in data["Condition"]: self.conditions[row["ownerType"],row["ownerId"]].append(row)
        self.checks = {r["checkId"]:(r["attributeId"],r["threshold"]) for r in data["AbilityCheckCondition"]}
        self.selected, self.checked, self.played = set(), {}, {}
        self.bars = {}

    def guard(self, text):
        if not text: return True
        if "|" in text or "," in text: return all(self.guard(x) for x in re.split(r"[|,]",text))
        if text.startswith("not:"): return not self.guard(text[4:])
        if text.startswith("choice:"): return text[7:] in self.selected
        if text.startswith("anyChoice:"): return any(c in self.selected for c in text[10:].split(";"))
        if text.startswith("check:"): return self.checked.get(text[6:],False)
        if text.startswith("gameplay:"): return self.played.get(text[9:-7],False)
        if text.startswith("bar:"):
            _,key,op,threshold = text.split(":")
            if key not in self.bars: return False
            x,y = self.bars[key],int(threshold)
            return {"ge":x>=y,"gt":x>y,"le":x<=y,"lt":x<y,"eq":x==y,"ne":x!=y}[op]
        if text not in self.flags: raise AssertionError("Undeclared flag "+text)
        return self.flags[text]

    def eligible(self, typ, owner):
        groups = defaultdict(list)
        for row in self.conditions[typ,owner]:
            op,key,value = row["condType"],row["target"],row["value"]
            if op == "BOOL_EQ":
                ok = self.guard(key) == (value=="true") if typ=="result" else self.flags.get(key)==(value=="true")
            elif op == "BAR_GTE": ok = key in self.bars and self.bars[key]>=int(value)
            elif op == "BAR_BETWEEN":
                low,high = map(int,value.split(",")); ok=key in self.bars and low<=self.bars[key]<=high
            else: raise AssertionError("Unsupported test condition "+op)
            groups[row["groupIndex"]].append(ok)
        return not groups or any(all(g) for g in groups.values())

    def trace(self, root, selections=None, attributes=None, facts=None, bars=None, signals=None, *, inspect_blocked_plan=False, cancel=False):
        event = self.events[root]
        if "blockedBy=" in event["eventProperty"] and not inspect_blocked_plan:
            return dict(blocked=True,settled=False,anchors=[],results=[],deltas={},queued=[])
        self.selected,self.checked,self.played = set(),{},{}
        self.flags.update(facts or {})
        self.bars = bars or {}
        selections,attributes,signals = selections or {},attributes or {},signals or {}
        seen = []
        pages = sorted((p for p in self.data["ConversationPage"] if p["presentationKey"]==root),key=lambda p:p["seq"])
        ended = False
        for page in pages:
            if not self.guard(page.get("pageCondKeys")): continue
            step = page.get("stepType")
            if step == "End": ended=True;break
            if step == "Gameplay":
                if page["pageId"] not in signals:
                    return dict(blocked=False,waiting=page["pageId"],settled=False,anchors=seen,results=[],deltas={},queued=[])
                self.played[page["pageId"]] = signals[page["pageId"]]
                continue
            if step == "PassiveCheck":
                attr,threshold = self.checks[page["checkId"]]
                self.checked[page["checkId"]] = attributes.get(attr,0)>=threshold
                if not self.checked[page["checkId"]]: continue
            for line in sorted(self.lines[page["pageId"]],key=lambda r:r["seq"]):
                seen.append(int(re.search(r"\.p(\d+)$",line["textKey"])[1]))
            group = page.get("optionGroupKey")
            if group:
                short = group.removeprefix(root+".")
                letter = selections.get(short,"a")
                choice = next(r for r in self.groups[group] if r["choiceId"].endswith("."+letter))
                if not self.eligible("choice",choice["choiceId"]):
                    raise AssertionError("Attempt to select unavailable source branch "+choice["choiceId"])
                self.selected.add(choice["choiceId"])
                if choice.get("checkId"):
                    attr,threshold = self.checks[choice["checkId"]]
                    self.checked[choice["checkId"]] = attributes.get(attr,0)>=threshold
        if cancel:
            return dict(blocked=False,settled=False,anchors=seen,results=[],deltas={},queued=[])
        if not ended: raise AssertionError("Graph lacks terminating End "+root)
        owners = {root}|{self.choices[c]["resultOwnerId"] for c in self.selected}
        # Freeze all guard decisions before committing any bool/bar effects, as NCS does.
        results = [r for r in self.data["Result"] if r["eventId"] in owners and self.eligible("result",r["resultId"])]
        deltas,queued = defaultdict(int),[]
        for row in results:
            if row["resultType"] == "SET_FLAG": self.flags[row["target"]] = row["value"]=="true"
            elif row["resultType"] == "BAR_ADD": deltas[row["target"]]+=int(row["value"])
            elif row["resultType"] == "QUEUE_EVENT": queued.append(row["target"])
            else: raise AssertionError("Unexpected effect "+row["resultType"])
        return dict(blocked=False,settled=True,anchors=seen,results=results,deltas=dict(deltas),queued=queued)


class NeutralCompilerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = neutral.build(PROJECT)
        # Cross-worker continuation must resolve to the ACTUAL current compiler,
        # never a manufactured Event stub or date trigger in the neutral module.
        spec = importlib.util.spec_from_file_location("revolution_neutral_contract_test",MODULE_PATH.with_name("revolution.py"))
        revolution = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(revolution)
        cls.revolution_data = revolution.build(PROJECT)
    def model(self): return GraphModel(self.data)
    def audit(self,root): return next(r for r in self.data["_audit"] if r["root"]==root)

    def test_current_all16_sources_and_all2305_nonempty_anchors(self):
        self.assertEqual(16,len(self.data["_audit"]))
        self.assertEqual(2305,sum(a["source_paragraphs"] for a in self.data["_audit"]))
        self.assertEqual(set(PROJECT.joinpath(neutral.SOURCE_DIR).glob("*.docx")),{PROJECT/a["source"] for a in self.data["_audit"]})
        for audit in self.data["_audit"]:
            self.assertEqual(sha256((PROJECT/audit["source"]).read_bytes()).hexdigest(),audit["source_sha256"])
            self.assertEqual([f"P{i:03}" for i in range(1,audit["source_paragraphs"]+1)],[a["anchor"] for a in audit["anchors"]])
            self.assertFalse(any(u["kind"]=="uncompiled" for a in audit["anchors"] for u in a["uses"]))

    def test_pure_deterministic_build_no_writes(self):
        with patch.object(Path,"write_text",side_effect=AssertionError("build must not write")), patch.object(Path,"write_bytes",side_effect=AssertionError("build must not write")):
            self.assertEqual(self.data,neutral.build(PROJECT))

    def test_source_drift_fails_closed_not_old_mapping(self):
        with patch.object(neutral,"_paragraphs",return_value=["changed current source"]):
            with self.assertRaisesRegex(ValueError,"source drift"): neutral.build(PROJECT)

    def test_exported_fields_match_actual_source_headers(self):
        import openpyxl
        paths = {"Text":PROJECT/"Assets/ConfigTables/Data/Shared/Text.xlsx"}
        narrative = openpyxl.load_workbook(PROJECT/"Assets/ConfigTables/Data/Narrative/Narrative.xlsx",read_only=True,data_only=True)
        text = openpyxl.load_workbook(paths["Text"],read_only=True,data_only=True)
        try:
            for sheet,rows in self.data.items():
                if sheet.startswith("_"): continue
                headers = set(next((text if sheet=="Text" else narrative)[sheet].values))
                if sheet=="ConversationPage": headers|={"gameplayKey","nextPageId"}  # Agreed main-owned schema extension.
                for row in rows: self.assertFalse(set(row)-headers,(sheet,set(row)-headers))
        finally: narrative.close();text.close()

    def test_unique_ids_references_and_no_directions_as_dialogue(self):
        keys = dict(Event="eventId",EventDate="eventId",ConditionKey="key",Condition="conditionId",ConversationPage="pageId",
                    ConversationLine="lineId",Choice="choiceId",AbilityCheck="checkId",AbilityCheckCondition="conditionId",
                    Result="resultId",Ending="endingId",PresentationIndex="presentationKey",Object="objId",Action="actionKey",Text="key")
        for sheet,key in keys.items():
            ids=[r[key] for r in self.data[sheet]];self.assertEqual(len(ids),len(set(ids)),sheet)
        pages={p["pageId"]:p for p in self.data["ConversationPage"]}
        texts={t["key"]:t["text"] for t in self.data["Text"]}
        for flag in self.data["ConditionKey"]: self.assertIn(flag["nameText"],texts)
        for line in self.data["ConversationLine"]:
            self.assertIn(line["pageId"],pages);self.assertIn(line["textKey"],texts)
            self.assertFalse(neutral._is_direction(texts[line["textKey"]]),line)
        model=self.model()
        for page in pages.values():
            if page["stepType"] in ("End","Gameplay"):
                self.assertFalse(model.lines[page["pageId"]]);self.assertFalse(page.get("optionGroupKey"))
            if page["stepType"]=="PassiveCheck":
                self.assertTrue(model.lines[page["pageId"]]);self.assertIn(page["checkId"],model.checks)
        for row in self.data["Result"]:
            self.assertIn(row["eventId"],set(model.events)|{c["resultOwnerId"] for c in model.choices.values()})
            if row["resultType"]=="QUEUE_EVENT":
                external={r["eventId"]:r for r in self.revolution_data["Event"]}
                target=({**external,**model.events})[row["target"]]
                self.assertIn("entry=Immediate",target["eventProperty"])

    def test_preserve_oct07_and_keep_1008_real_action(self):
        for sheet in ("Event","Result","ConversationPage","ConversationLine","Text"):
            self.assertFalse(any(str(r.get(next(iter(r)),"")).startswith("oct07.s1") for r in self.data[sheet]))
        self.assertNotIn("oct07.s1",self.data["_replace_roots"])
        self.assertEqual("preserved-existing",self.audit("oct07.s1")["status"])
        self.assertEqual("neutral.1008.s1",self.data["Action"][0]["resultEvents"])
        self.assertTrue(self.data["Action"][0]["mustExecute"])

    def test_market_all_branches_are_exclusive_and_known_colette_is_root_tail(self):
        for letter,language,expected,excluded,delta in [("a",0,{40,44},{48,53,56},2),("b",2,{48,49},{40,53,56},1),
                                                      ("b",1,{53,54},{40,48,56},0),("c",0,{56,59},{40,48,53},0)]:
            with self.subTest(letter=letter,language=language):
                model=self.model();result=model.trace("neutral.1008.s1",{"q1":letter},{"language":language})
                self.assertTrue(expected.issubset(result["anchors"]));self.assertTrue(excluded.isdisjoint(result["anchors"]))
                self.assertEqual(delta,result["deltas"].get("colette.affection",0));self.assertTrue(model.flags["known.colette"])
        model=self.model();result=model.trace("neutral.1008.s1",cancel=True)
        self.assertFalse(result["settled"]);self.assertFalse(model.flags["known.colette"])

    def test_1010_preparation_success_failure_rewards_not_flattened(self):
        for letter,wisdom,language,yes,no,delta in [("a",0,0,38,44,1),("b",3,0,44,49,0),
                                                 ("b",2,0,49,44,0),("c",0,2,54,60,2),("c",0,1,60,54,0)]:
            model=self.model();r=model.trace("neutral.1010.s1",{"q1":letter},{"wisdom":wisdom,"language":language})
            self.assertIn(yes,r["anchors"]);self.assertNotIn(no,r["anchors"])
            self.assertEqual(delta,r["deltas"].get("adele.affection",0));self.assertEqual(["neutral.1010.s2"],r["queued"])
        self.assertFalse(any(r["resultType"] in ("MONEY_ADD","MONEY_REMOVE","TRAIN_ATTRIBUTE") for r in self.data["Result"]))

    def test_antoine_refusal_never_meets_unlocks_or_awards(self):
        model=self.model();r=model.trace("neutral.1010.s2",{"q1":"c"},{"language":20})
        self.assertTrue({34,37,107,109,112}.issubset(r["anchors"]))
        self.assertTrue({43,64,74,79,85,92,102}.isdisjoint(r["anchors"]))
        self.assertFalse(model.flags["known.antoine"]);self.assertFalse(model.flags["neutral.antoine.appreciation"])
        self.assertFalse(r["deltas"])
        for q1,q2,language in product(("a","b"),("a","b","c"),(1,3)):
            model=self.model();r=model.trace("neutral.1010.s2",{"q1":q1,"q2":q2},{"language":language})
            self.assertTrue(model.flags["known.antoine"])
            self.assertEqual(q2=="a" and language>=3,model.flags["neutral.antoine.appreciation"])
            self.assertNotIn(107,r["anchors"])

    def test_construction_blockers_are_real_false_conditions_not_only_notes(self):
        blocked={"neutral.route.1129.s1"}
        actual={r["eventId"] for r in self.data["Event"] if "blockedBy=" in r["eventProperty"]}
        self.assertEqual(blocked,actual)
        for root in blocked:
            model=self.model();self.assertFalse(model.trace(root)["settled"])
            guards=[r["target"] for r in self.data["Condition"] if r["ownerType"]=="event" and r["ownerId"]==root and r["target"].startswith("blocked.")]
            self.assertTrue(guards)
            self.assertTrue(all(model.flags[g] is False for g in guards));self.assertTrue(self.audit(root)["blockedBy"])

    def test_promotion_completion_c13_needs_two_real_gameplay_evidences(self):
        root="neutral.1013.s2";first=root+".first_service";activity=root+".activity"
        for signals,completed in [({},False),({first:True},False),({first:False},False),
                                  ({first:True,activity:False},False),({first:True,activity:True},True)]:
            model=self.model();r=model.trace(root,{"q1":"a"},signals=signals)
            self.assertFalse(r["blocked"])
            self.assertEqual(completed,model.flags[neutral.completed(root)])
            self.assertEqual(1 if completed else 0,r["deltas"].get("adele.affection",0))
            self.assertEqual(["revolution.1013.s2"] if completed else [],r["queued"])
        model=self.model();r=model.trace(root,{"q1":"a"},signals={first:True,activity:True},cancel=True)
        self.assertFalse(model.flags[neutral.completed(root)]);self.assertFalse(r["deltas"])
        self.assertFalse(r["queued"])
        self.assertEqual({"serve:any:60:1"},{p["gameplayKey"] for p in self.data["ConversationPage"] if p["presentationKey"]==root and p["stepType"]=="Gameplay"})

    def test_reopened_roots_have_only_choice_scoped_blockers(self):
        for root,disabled_choice in [("neutral.1013.s2","q2.b"),("neutral.1018.s1","advert.a")]:
            with self.subTest(root=root):
                model=self.model()
                event=model.events[root]
                self.assertNotIn("blockedBy=",event["eventProperty"])
                prerequisites="neutral.1010.s1" if root=="neutral.1013.s2" else "neutral.1013.s2"
                model.flags[neutral.completed(prerequisites)]=True
                self.assertTrue(model.eligible("event",root))
                self.assertFalse(any(c["target"].startswith("blocked.") for c in model.conditions["event",root]))
                audit=self.audit(root)
                self.assertEqual("partial-branches-blocked",audit["status"])
                self.assertEqual(1,len(audit["blockedBy"]))
                blocker=audit["blockedBy"][0]
                self.assertEqual("choice",blocker["scope"])
                self.assertEqual(root+".choice."+disabled_choice,blocker["choiceId"])
                self.assertFalse(model.flags[blocker["conditionKey"]])
                self.assertTrue(blocker["sourceAnchors"])

    def test_promotion_normal_roles_and_optional_observation_remain_playable(self):
        root="neutral.1013.s2"
        signals={root+".first_service":True,root+".activity":True}
        roles=[("a",{},41,{49,55,61,68}),
               ("b",{"wisdom":2},49,{41,55,61,68}),
               ("b",{"wisdom":1},55,{41,49,61,68}),
               ("c",{"language":3},61,{41,49,55,68}),
               ("c",{"language":2},68,{41,49,55,61})]
        for letter,attributes,yes,no in roles:
            with self.subTest(role=letter,attributes=attributes):
                model=self.model();r=model.trace(root,{"q1":letter},attributes,signals=signals)
                self.assertTrue(r["settled"]);self.assertIn(yes,r["anchors"])
                self.assertTrue(no.isdisjoint(r["anchors"]))
                self.assertNotIn(87,r["anchors"])
                self.assertEqual(["revolution.1013.s2"],r["queued"])
                self.assertTrue(model.flags[neutral.completed(root)])
        for choice,language,yes,no,reward in [("a",3,105,{110,122},2),("a",2,110,{105,122},0),("c",3,122,{105,110},0)]:
            with self.subTest(observation=choice,language=language):
                model=self.model();r=model.trace(root,{"q1":"a","q2":choice},{"language":language},
                    facts={"neutral.antoine.appreciation":True},signals=signals)
                self.assertIn(yes,r["anchors"]);self.assertTrue(no.isdisjoint(r["anchors"]))
                self.assertTrue({114,119}.isdisjoint(r["anchors"]))
                self.assertEqual(reward,r["deltas"].get("antoine.affection",0))
                self.assertTrue(model.flags["neutral.antoine.observation"])
                self.assertEqual(["revolution.1013.s2"],r["queued"])
        with self.assertRaisesRegex(AssertionError,"unavailable"):
            self.model().trace(root,{"q1":"a","q2":"b"},{"wisdom":20},
                facts={"neutral.antoine.appreciation":True},signals=signals)

    def test_c13_queue_is_root_owned_guarded_and_resolves_to_current_queue_only_event(self):
        root="neutral.1013.s2";target="revolution.1013.s2"
        queued=[r for r in self.data["Result"] if r["resultType"]=="QUEUE_EVENT" and r["target"]==target]
        self.assertEqual(1,len(queued));result=queued[0]
        self.assertEqual(root,result["eventId"])
        completion=next(r for r in self.data["Result"] if r["eventId"]==root and r["target"]==neutral.completed(root))
        def guards(row):
            return {c["target"] for c in self.data["Condition"] if c["ownerType"]=="result" and c["ownerId"]==row["resultId"] and c["value"]=="true"}
        expected={"gameplay:"+root+".first_service:passed","gameplay:"+root+".activity:passed"}
        self.assertEqual(expected,guards(result));self.assertEqual(expected,guards(completion))
        event=next(e for e in self.revolution_data["Event"] if e["eventId"]==target)
        self.assertIn("entry=Immediate",event["eventProperty"])
        self.assertFalse(any(d["eventId"]==target for d in self.revolution_data["EventDate"]))
        self.assertTrue(any(c["ownerType"]=="event" and c["ownerId"]==target and
            c["target"]==neutral.completed(root) and c["value"]=="true" for c in self.revolution_data["Condition"]))
        self.assertTrue(next(e for e in self.data["Event"] if e["eventId"]==root)["onceOnly"])

    def test_reporter_quality_routes_are_real_outcome_exclusive_and_paid_ad_blocked(self):
        root="neutral.1018.s1";signal=root+".reporter_grade"
        model=self.model();waiting=model.trace(root)
        self.assertFalse(waiting["blocked"]);self.assertEqual(signal,waiting["waiting"])
        self.assertFalse(waiting["settled"]);self.assertFalse(model.flags[neutral.completed(root)])
        self.assertFalse(model.flags["neutral.newspaper.recommendation"])
        self.assertEqual({"taste:any:60:1"},{p["gameplayKey"] for p in self.data["ConversationPage"] if p["presentationKey"]==root and p["stepType"]=="Gameplay"})
        model=self.model();high=model.trace(root,signals={signal:True})
        self.assertTrue({18,42,54}.issubset(high["anchors"]));self.assertTrue({21,58,70,79}.isdisjoint(high["anchors"]))
        self.assertTrue(model.flags["neutral.newspaper.recommendation"])
        self.assertTrue(model.flags[neutral.completed(root)])
        model=self.model();low=model.trace(root,{"advert":"b"},signals={signal:False})
        self.assertTrue({21,58,79,85}.issubset(low["anchors"]));self.assertTrue({18,42,70}.isdisjoint(low["anchors"]))
        self.assertFalse(model.flags["neutral.newspaper.recommendation"])
        self.assertTrue(model.flags[neutral.completed(root)])
        model=self.model();cancelled=model.trace(root,signals={signal:True},cancel=True)
        self.assertFalse(cancelled["settled"]);self.assertFalse(model.flags[neutral.completed(root)])
        self.assertFalse(model.flags["neutral.newspaper.recommendation"])
        with self.assertRaisesRegex(AssertionError,"unavailable"):
            self.model().trace(root,{"advert":"a"},signals={signal:False})

    def test_1027_and_1030_current_affection_deltas(self):
        for letter,attrs,delta in [("a",{"wisdom":4},2),("a",{"wisdom":3},0),("b",{"language":3},1),("b",{"language":2},-1),("c",{},0)]:
            r=self.model().trace("neutral.1027.s1",{"q1":letter},attrs)
            self.assertEqual(delta,r["deltas"].get("alienor.affection",0))
        for letter,attrs,delta in [("a",{},1),("b",{"perception":4},2),("b",{"perception":3},0),("c",{},-1)]:
            r=self.model().trace("neutral.1030.s1",{"q1":letter},attrs)
            self.assertEqual(delta,r["deltas"].get("adele.affection",0))
        gate=next(r for r in self.data["Condition"] if r["ownerId"]=="neutral.1030.s1" and r["condType"]=="BAR_GTE")
        self.assertEqual(("adele.affection","3"),(gate["target"],gate["value"]))

    def test_1111_split_roots_queue_only_endings_require_completed_roots(self):
        for root in ("neutral.route.1111.s1","neutral.route.1111.s2"):
            self.assertFalse(any(d["eventId"]==root for d in self.data["EventDate"]))
        for ending in self.data["Ending"]:
            model=self.model();model.flags["route.neutral"]=True
            self.assertFalse(model.eligible("ending",ending["endingId"]))
            conds=model.conditions["ending",ending["endingId"]]
            self.assertTrue(any(c["target"].endswith(".completed") for c in conds))
        model=self.model();model.trace("neutral.route.1111.s1",facts={"route.neutral":True})
        self.assertTrue(model.eligible("ending","neutral.ending.a"))

    def test_membership_only_on_accept_no_unspecified_economic_bonus(self):
        for choice,want in [("a",True),("b",False)]:
            model=self.model();r=model.trace("neutral.route.1112.s1",{"membership":choice})
            self.assertEqual(want,model.flags["neutral.association.member"]);self.assertFalse(r["deltas"])

    def test_1118_shelter_true_affection_and_no_bribe_or_missing_ending_fabricated(self):
        root="neutral.route.1118.s1"
        model=self.model();r=model.trace(root,{"investigation":"b"},bars={"alienor.affection":3})
        self.assertTrue({82,106,149,174}.issubset(r["anchors"]));self.assertTrue({69,110,127,139}.isdisjoint(r["anchors"]))
        self.assertTrue(model.flags["neutral.investigation.shelter"]);self.assertTrue(model.flags[neutral.completed(root)])
        self.assertFalse(model.flags["neutral.investigation.bribed"])
        for choice,bars in [("a",{}),("b",{"alienor.affection":2}),("c",{})]:
            with self.assertRaisesRegex(AssertionError,"unavailable"):
                self.model().trace(root,{"investigation":choice},bars=bars)

    def test_1118_event_entry_requires_a_real_available_protection_choice(self):
        root="neutral.route.1118.s1";choice=root+".choice.investigation.b"
        audit=self.audit(root)
        self.assertEqual("partial-branches-blocked",audit["status"])
        self.assertEqual(choice,audit["resolvedNormalPath"]["choiceId"])
        self.assertEqual({root+".choice.investigation.a",root+".choice.investigation.c"},
                         {b["choiceId"] for b in audit["blockedBy"]})
        self.assertTrue(all(b["scope"]=="choice" for b in audit["blockedBy"]))
        model=self.model()
        self.assertNotIn("blockedBy=",model.events[root]["eventProperty"])
        self.assertFalse(any(c["target"].startswith("blocked.") for c in model.conditions["event",root]))
        model.flags.update({"route.neutral":True,neutral.completed("neutral.route.1111.s2"):True})
        for value,expected in [(None,False),(2,False),(3,True),(20,True)]:
            with self.subTest(affection=value):
                model.bars={} if value is None else {"alienor.affection":value}
                self.assertEqual(expected,model.eligible("event",root))
                self.assertEqual(expected,model.eligible("choice",choice))
                if expected:
                    available=[c["choiceId"] for c in model.groups[root+".investigation"] if model.eligible("choice",c["choiceId"])]
                    self.assertEqual([choice],available)
        model.bars={"alienor.affection":3}
        model.flags["route.neutral"]=False;self.assertFalse(model.eligible("event",root))
        model.flags["route.neutral"]=True;model.flags["route.rose"]=True
        self.assertFalse(model.eligible("event",root))
        model.flags["route.rose"]=False;model.flags[neutral.completed("neutral.route.1111.s2")]=False
        self.assertFalse(model.eligible("event",root))

    def test_1118_shelter_and_completion_require_accepted_choice_at_root_tail(self):
        root="neutral.route.1118.s1"
        completed=next(r for r in self.data["Result"] if r["eventId"]==root and r["target"]==neutral.completed(root))
        shelter=next(r for r in self.data["Result"] if r["target"]=="neutral.investigation.shelter")
        expected={"choice:"+root+".choice.investigation.b","bar:alienor.affection:ge:3"}
        for result in (completed,shelter):
            conditions=[c for c in self.data["Condition"] if c["ownerType"]=="result" and c["ownerId"]==result["resultId"]]
            self.assertEqual(expected,{c["target"] for c in conditions})
            self.assertTrue(all(c["condType"]=="BOOL_EQ" and c["value"]=="true" for c in conditions))
        self.assertEqual(root+".choice.investigation.b.result",shelter["eventId"])
        model=self.model();r=model.trace(root,{"investigation":"b"},bars={"alienor.affection":3},cancel=True)
        self.assertFalse(r["settled"]);self.assertFalse(model.flags[neutral.completed(root)])
        self.assertFalse(model.flags["neutral.investigation.shelter"])
        self.assertFalse(model.flags["neutral.investigation.bribed"])
        # Affection alone, without an accepted branch choice, never marks success.
        model=self.model();model.bars={"alienor.affection":3}
        self.assertFalse(model.eligible("result",completed["resultId"]))
        self.assertFalse(model.eligible("result",shelter["resultId"]))

    def test_royal_protection_chain_can_reach_b_or_nonmember_c_without_revenue_guess(self):
        investigation="neutral.route.1118.s1";future="neutral.route.1122.s1";ordinary="neutral.route.1130.s2"
        for decision,ending in [("a","neutral.ending.c"),("b","neutral.ending.b")]:
            with self.subTest(decision=decision):
                model=self.model();model.flags.update({"route.neutral":True,neutral.completed("neutral.route.1111.s2"):True})
                model.bars={"alienor.affection":3}
                self.assertTrue(model.eligible("event",investigation))
                result=model.trace(investigation,{"investigation":"b"},bars=model.bars)
                self.assertFalse(result["blocked"]);self.assertTrue(result["settled"])
                self.assertTrue(model.eligible("event",future))
                self.assertFalse(model.eligible("event",investigation))  # once completed, do not reopen
                result=model.trace(future,{"future":decision})
                self.assertTrue(result["settled"])
                if decision=="a":
                    self.assertTrue(model.eligible("event",ordinary))
                    self.assertFalse(model.eligible("ending","neutral.ending.b"))
                    model.trace(ordinary)
                self.assertTrue(model.eligible("ending",ending))
                self.assertFalse(model.flags["neutral.investigation.bribed"])
                self.assertFalse(model.flags["neutral.association.committee"])
                self.assertFalse(model.trace("neutral.route.1129.s1")["settled"])

    def test_1122_staying_ends_before_retreat_epilogue_and_b_only_after_retreat(self):
        facts={"neutral.investigation.shelter":True,"route.neutral":True}
        model=self.model();stay=model.trace("neutral.route.1122.s1",{"future":"a"},facts=facts)
        self.assertIn(95,stay["anchors"]);self.assertNotIn(113,stay["anchors"])
        self.assertTrue(model.flags["neutral.stayed"]);self.assertFalse(model.eligible("ending","neutral.ending.b"))
        model=self.model();leave=model.trace("neutral.route.1122.s1",{"future":"b"},facts=facts)
        self.assertIn(113,leave["anchors"]);self.assertNotIn(95,leave["anchors"])
        self.assertFalse(model.flags["neutral.stayed"]);self.assertTrue(model.eligible("ending","neutral.ending.b"))

    def test_negotiation_failure_ends_before_d_and_queues_c_once(self):
        root="neutral.route.1130.s1"
        model=self.model();failure=model.trace(root,attributes={"language":4},facts={"route.neutral":True})
        self.assertTrue({91,105}.issubset(failure["anchors"]));self.assertTrue({61,81,116,223}.isdisjoint(failure["anchors"]))
        self.assertEqual(["neutral.route.1130.s2"],failure["queued"])
        self.assertTrue(model.flags["neutral.negotiation.failed"]);self.assertFalse(model.eligible("ending","neutral.ending.d"))
        model=self.model();success=model.trace(root,attributes={"language":5},facts={"route.neutral":True})
        self.assertTrue({61,81,116}.issubset(success["anchors"]));self.assertNotIn(91,success["anchors"])
        self.assertFalse(success["queued"]);self.assertTrue(model.eligible("ending","neutral.ending.d"))

    def test_c_entry_or_groups_never_turn_missing_revenue_content_into_failure(self):
        root="neutral.route.1130.s2"
        for member,failed,want in [(False,False,True),(True,False,False),(True,True,True)]:
            model=self.model();model.flags.update({"route.neutral":True,neutral.completed("neutral.route.1122.s1"):True,
                "neutral.stayed":True,"neutral.association.member":member,"neutral.negotiation.failed":failed})
            self.assertEqual(want,model.eligible("event",root))
            model.flags["neutral.stayed"]=False;self.assertFalse(model.eligible("event",root))
        model=self.model();trace=model.trace(root,facts={"neutral.association.member":True,"neutral.negotiation.failed":True})
        self.assertIn(58,trace["anchors"]);self.assertTrue({50,54}.isdisjoint(trace["anchors"]))

    def test_registered_backgrounds_scene_cafe_and_portrait_pending_generics(self):
        asset=(PROJECT/"Assets/Resources/Dialogue/NarrativeStageVisuals.asset").read_text(encoding="utf-8")
        registered=set(re.findall(r"- key: (\S+)",asset))
        for p in self.data["ConversationPage"]:
            if p.get("bgOverride"): self.assertIn(p["bgOverride"],registered)
        for root in ("neutral.1010.s1","neutral.1010.s2","neutral.1013.s2","neutral.1018.s1","neutral.1027.s1","neutral.1030.s1","neutral.route.1111.s2"):
            self.assertEqual({"scene.cafe"},{p["bgOverride"] for p in self.data["ConversationPage"] if p["presentationKey"]==root and p.get("bgOverride")})
        self.assertTrue(all(not o.get("portraitPath") for o in self.data["Object"]))
        self.assertTrue(all(o["objId"].startswith("neutral.") for o in self.data["Object"]))

    def test_every_transient_guard_refers_to_a_declared_source_node(self):
        choices={r["choiceId"] for r in self.data["Choice"]}
        checks={r["checkId"] for r in self.data["AbilityCheck"]}
        gameplay={r["pageId"] for r in self.data["ConversationPage"] if r["stepType"]=="Gameplay"}
        flags={r["key"] for r in self.data["ConditionKey"]}|{"route."+f for f in ("neutral","rose","church","revolution")}
        def valid(guard):
            if guard.startswith("not:"): return valid(guard[4:])
            if guard.startswith("anyChoice:"): return all(c in choices for c in guard[10:].split(";"))
            if guard.startswith("choice:"): return guard[7:] in choices
            if guard.startswith("check:"): return guard[6:] in checks
            if guard.startswith("gameplay:"): return guard.endswith(":passed") and guard[9:-7] in gameplay
            if guard.startswith("bar:"): return bool(re.fullmatch(r"bar:[^:]+:(ge|gt|le|lt|eq|ne):-?[0-9]+",guard))
            return guard in flags
        for page in self.data["ConversationPage"]:
            for guard in filter(None,re.split(r"[|,]",page.get("pageCondKeys") or "")):
                self.assertTrue(valid(guard),(page["pageId"],guard))
        for cond in self.data["Condition"]:
            if cond["ownerType"]=="result": self.assertTrue(valid(cond["target"]),cond)


if __name__=="__main__": unittest.main(verbosity=2)
