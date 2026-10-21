"""Source compiler tests only. Does not claim Unity/cafe-host validation."""
from __future__ import annotations
from collections import defaultdict
from copy import deepcopy
from hashlib import sha256
import json
from pathlib import Path
import re
import sys
import unittest
from unittest.mock import patch

try:
    from . import church
except ImportError:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import church

PROJECT = Path(__file__).resolve().parents[3]

class Graph:
    """Independent bounded interpreter for emitted existing NCS page guards.
    Never mocks gameplay success as integration evidence. Source graph tests
    explicitly inject a serving result and are not host/cafe play-mode tests.
    """
    def __init__(self, rows, root, attrs=None, facts=None):
        self.rows, self.root = rows, root
        self.attrs = {"player.language": 8, "player.courage": 8, "player.wisdom": 8,
                      "player.perception": 8, "alienor.affection": 4, "faction.revolution": 0,
                      "cafe.money": 3000}
        self.attrs.update(attrs or {})
        self.facts = {r["key"]: r["initialValue"] for r in rows["ConditionKey"]}
        self.facts.update(facts or {})
        self.selected, self.checks, self.visited, self.lines, self.menus = set(), {}, [], [], []
        self.gameplay_results = {}
        self.pages = sorted([p for p in rows["ConversationPage"] if p["presentationKey"] == root + ".conversation"], key=lambda p:p["seq"])

    def atom(self, key):
        if key.startswith("not:"): return not self.atom(key[4:])
        if key.startswith("choice:"): return key[7:] in self.selected
        if key.startswith("anyChoice:"): return any(x in self.selected for x in key[10:].split(";"))
        if key.startswith("check:"):
            if key[6:] not in self.checks: raise AssertionError("unevaluated check: " + key)
            return self.checks[key[6:]]
        if key.startswith("gameplay:"):
            return self.gameplay_results.get(key[9:-7], False)
        if key.startswith("bar:"):
            bar, op, bound = key[4:].rsplit(":", 2)
            if bar not in self.attrs: return False
            x, y = self.attrs[bar], int(bound)
            return {"ge":x>=y,"gt":x>y,"le":x<=y,"lt":x<y,"eq":x==y,"ne":x!=y}[op]
        return self.facts.get(key, False)

    def visible(self, value): return all(self.atom(x) for x in value.split("|") if x)

    def check(self, key):
        groups=defaultdict(list)
        for r in self.rows["AbilityCheckCondition"]:
            if r["checkId"]==key: groups[r["groupIndex"]].append(self.attrs.get("player."+r["attributeId"],-999)>=r["threshold"])
        self.checks[key]=any(all(v) for v in groups.values())

    def run(self, picks=None, picker=None, tasting_passed=True):
        picks=picks or {}
        for p in self.pages:
            if not self.visible(p.get("pageCondKeys","")): continue
            self.visited.append(p)
            if p["stepType"]=="End": break
            if p["stepType"]=="PassiveCheck":
                self.check(p["checkId"])
                if not self.checks[p["checkId"]]: continue
            if p["stepType"]=="Gameplay": self.gameplay_results[p["pageId"]] = tasting_passed
            self.lines.extend(x["textKey"] for x in self.rows["ConversationLine"] if x["pageId"]==p["pageId"])
            if "optionGroupKey" in p:
                opts=sorted([x for x in self.rows["Choice"] if x["groupKey"]==p["optionGroupKey"]],key=lambda x:x["seq"])
                self.menus.append(opts)
                index=picker(p,opts) if picker else picks.get(p["optionGroupKey"].split(".choices.",1)[1],0)
                selected=opts[index]; self.selected.add(selected["choiceId"])
                if selected.get("checkId"): self.check(selected["checkId"])
        return self

    def results(self):
        applied=[]
        for r in self.rows["Result"]:
            if r["eventId"]!=self.root and not any(r["eventId"]==cid+".result" for cid in self.selected): continue
            conditions=[c for c in self.rows["Condition"] if c["ownerType"]=="result" and c["ownerId"]==r["resultId"]]
            # Evaluate every row as the real core currently does (no assumed
            # result-condition AND short-circuit for unevaluated checks).
            values=[self.atom(c["target"])==(c["value"]=="true") for c in conditions]
            if all(values): applied.append(r)
        return applied

    def seen(self, paragraph):
        key=f"{self.root}.text.p{paragraph:03}"
        return key in self.lines

class ChurchContentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.rows=church.build(PROJECT)

    def test_all_current_16_sources_and_1953_anchors(self):
        a=self.rows["_audit"]
        self.assertEqual(16,len(a)); self.assertEqual(1953,sum(x["nonemptyParagraphs"] for x in a))
        self.assertEqual(set(church.SOURCE_HASHES),{Path(x["source"]).name for x in a})
        for source in a:
            self.assertNotIn("unmapped",{x["kind"] for x in source["anchors"]})
            self.assertEqual(list(range(1,source["nonemptyParagraphs"]+1)),[x["paragraph"] for x in source["anchors"]])
            self.assertEqual(source["sha256"],sha256((PROJECT/source["source"]).read_bytes()).hexdigest())

    def test_deterministic_json_serializable_pure_build(self):
        protected=[PROJECT/"Assets/ConfigTables/Data/Narrative/Narrative.xlsx",PROJECT/"Assets/ConfigTables/Data/Shared/Text.xlsx"]
        before={p:sha256(p.read_bytes()).hexdigest() for p in protected}
        again=church.build(PROJECT)
        self.assertEqual(json.dumps(self.rows,sort_keys=True,ensure_ascii=False),json.dumps(again,sort_keys=True,ensure_ascii=False))
        self.assertEqual(before,{p:sha256(p.read_bytes()).hexdigest() for p in protected})

    def test_source_drift_fails_closed(self):
        with patch.dict(church.SOURCE_HASHES,{"MA70.10.09-S2.docx":"wrong"}):
            with self.assertRaisesRegex(ValueError,"source changed"): church.build(PROJECT)

    def test_sheet_rows_only_approved_fields(self):
        allowed={
          "Object":"objId objType nameText iconPath portraitPath portraitIndoorPath avtarPath descText tags",
          "Event":"eventId eventKind actorId targetId titleText contentKey setupType setupKey onceOnly logTag presentationClass presentationKey presentationLogTag eventProperty",
          "EventDate":"eventId day timeSlot repeatRule priority", "SetupParams":"setupKey tplName slotValues skipAble autoAdvance checkId",
          "PresentationIndex":"presentationKey presentationClass logTag", "ConditionKey":"key nameText initialValue onlySetTrue",
          "Condition":"conditionId ownerType ownerId groupIndex condType target value", "Result":"resultId eventId seq resultType target value note",
          "ConversationPage":"pageId presentationKey seq pageCondKeys pageBarConds optionGroupKey bgOverride bgmOverride stepType readingKey checkId gameplayKey nextPageId",
          "ConversationLine":"lineId pageId seq speakerKey side textKey voiceKey emote sfxKey accentFxKey accentTargetKey speakerNameKey",
          "Choice":"choiceId groupKey textKey resultOwnerId seq checkId", "AbilityCheck":"checkId successEventId failureEventId successResultOwnerId failureResultOwnerId comment mode",
          "AbilityCheckCondition":"conditionId checkId attributeId threshold groupIndex seq", "ReadingPage":"pageId presentationKey seq titleKey imageKey pageCondKeys pageBarConds",
          "ReadingLine":"lineId pageId seq mode speakerKey textKey voiceKey", "Ending":"endingId enabled priority titleText messageText", "Text":"key text comment"}
        for sheet,fields in allowed.items():
            for row in self.rows[sheet]: self.assertFalse(set(row)-set(fields.split()),(sheet,row))

    def test_unique_primary_keys_and_only_own_replacements(self):
        pk={"Object":"objId","Event":"eventId","EventDate":"eventId","SetupParams":"setupKey","PresentationIndex":"presentationKey","ConditionKey":"key","Condition":"conditionId","Result":"resultId","ConversationPage":"pageId","ConversationLine":"lineId","Choice":"choiceId","AbilityCheck":"checkId","AbilityCheckCondition":"conditionId","ReadingPage":"pageId","ReadingLine":"lineId","Ending":"endingId","Text":"key"}
        for sheet,key in pk.items():
            values=[r[key] for r in self.rows[sheet]]
            self.assertEqual(len(values),len(set(values)),sheet)
        self.assertTrue(all(x.startswith("church.") for x in self.rows["_replace_roots"]))
        self.assertEqual(16,len(self.rows["_replace_roots"]))
        indices={r["presentationKey"]:r for r in self.rows["PresentationIndex"]}
        for page in self.rows["ReadingPage"]:
            self.assertIn(page["presentationKey"],indices)
            self.assertEqual("Reading",indices[page["presentationKey"]]["presentationClass"])

    def test_1009_pick_ignore_exclusive_and_common_return(self):
        for index in (0,1):
            g=Graph(self.rows,"church.1009.s2").run({"hat":index})
            self.assertEqual(index==0,g.seen(32)); self.assertEqual(index==1,g.seen(41)); self.assertTrue(g.seen(50))
            got=[r["target"] for r in g.results() if r["resultType"]=="SET_FLAG"]
            self.assertIn("known.alienor",got)
            self.assertEqual(index==0,"story.church.1009.s2.hat_helped" in got)

    def test_1014_real_order_and_special_door_restores_scene(self):
        p=[p for p in self.rows["ConversationPage"] if p["presentationKey"]=="church.1014.s2.conversation"]
        self.assertEqual("serve:blackcoffee:0:1",next(p for p in p if p["stepType"]=="Gameplay")["gameplayKey"])
        cg=next(i for i,x in enumerate(p) if x.get("bgOverride")=="cafe.door.thibaut")
        self.assertTrue(any(x.get("bgOverride")=="scene.cafe" for x in p[cg+1:]))

    def test_search_selected_attribute_threshold_is_current_8(self):
        for selected,attr in enumerate(("language","courage","wisdom","perception")):
            for value in (7,8):
                attrs={"player."+a:20 for a in ("language","courage","wisdom","perception")}; attrs["player."+attr]=value
                g=Graph(self.rows,"church.1020.s1",attrs).run({"search":0,"explain":selected})
                self.assertEqual(value>=8,g.seen(124)); self.assertEqual(value<8,g.seen(147))
                targets=[r["target"] for r in g.results()]
                self.assertEqual(value>=8,"story.church.1020.s1.cleared" in targets)
                self.assertEqual(value<8,"church.detention.requested" in targets)
        checks=[x for x in self.rows["AbilityCheckCondition"] if "search_" in x["checkId"]]
        self.assertEqual([8,8,8,8],[x["threshold"] for x in checks])

    def test_detention_is_authoritative_state_and_bribe_uses_live_money(self):
        events={x["eventId"]:x for x in self.rows["Event"]}
        for root in ("church.1020.s1","church.1023.s1","church.1023.s2"):
            self.assertNotIn("blockedBy=",events[root]["eventProperty"])
        self.assertFalse(any(r["target"] in ("world.calendar","bar:world.calendar") for r in self.rows["Result"]))
        self.assertFalse(any(r["resultType"]=="BAR_ADD" and r["eventId"].startswith("church.1023") for r in self.rows["Result"]))
        active=next(x for x in self.rows["ConditionKey"] if x["key"]=="church.detention.active")
        self.assertFalse(active["initialValue"]); self.assertFalse(active["onlySetTrue"])
        self.assertFalse(any(x["key"].startswith("construction.church.1023") for x in self.rows["ConditionKey"]))
        money=next(x for x in next(a for a in self.rows["_audit"] if a["root"]=="church.1023.s1")["decisions"] if x["kind"]=="source-derived-amount")
        self.assertEqual((2000,3000,3000),(money["offer"],money["demand"],money["settledAmount"]))
        self.assertEqual("funding-option",money["scope"])
        for value in (7,8):
            g=Graph(self.rows,"church.1020.s1",{"player.language":value}).run({"search":0,"explain":0})
            writes=[r for r in g.results() if r["target"]=="church.detention.active"]
            self.assertEqual([] if value>=8 else ["true"],[r["value"] for r in writes])

    def test_bribe_acceptance_debits_3000_and_shortfall_uses_rescue(self):
        for balance, expected in ((3000,"bribery"),(2999,"rescue")):
            pick = 1 if expected == "bribery" else 2
            g=Graph(self.rows,"church.1023.s1",{"player.language":0,"faction.revolution":0,"cafe.money":balance}).run({"release":pick,"bribe_price":0})
            targets=[r["target"] for r in g.results()]
            actual={x.rsplit(".",1)[1] for x in targets if x.startswith("church.release.") and x.rsplit(".",1)[1] in ("persuasion","bribery","rescue")}
            self.assertEqual({expected},actual)
            payments=[r for r in g.results() if r["resultType"]=="MONEY_REMOVE"]
            self.assertEqual(["3000"] if expected=="bribery" else [],[r["value"] for r in payments])
        cond=[c for c in self.rows["Condition"] if c["ownerType"]=="choice" and c["target"]=="cafe.money"]
        self.assertEqual({"3000,2147483647"},{c["value"] for c in cond})

    def test_release_outcomes_and_failure_are_exclusive(self):
        for language,inclination,release,expected in ((7,0,0,"persuasion"),(6,0,2,"rescue"),(6,-1,2,None)):
            facts={"church.detention.release_acknowledged":True}
            g=Graph(self.rows,"church.1023.s1",{"player.language":language,"faction.revolution":inclination,"cafe.money":0},facts).run({"release":release,"bribe_retry":1})
            targets=[r["target"] for r in g.results()]
            actual={x.rsplit(".",1)[1] for x in targets if x.startswith("church.release.") and x.rsplit(".",1)[1] in ("persuasion","bribery","rescue")}
            self.assertEqual(set() if expected is None else {expected},actual)
            self.assertEqual(expected is None,"church.1023.s2" in targets)
            clears=[r for r in g.results() if r["target"]=="church.detention.active"]
            self.assertEqual([] if expected is None else ["false"],[r["value"] for r in clears])
        # OR (wisdom6 OR perception6), not AND, for altered-release clue.
        g=Graph(self.rows,"church.1023.s1",{"player.language":0,"player.wisdom":0,"player.perception":6},{"church.detention.release_acknowledged":True}).run({"release":2})
        self.assertTrue(g.seen(75)); self.assertIn("church.release.records_anomalous",[r["target"] for r in g.results()])

    def test_outing_orders_and_oldtown_hidden_checks(self):
        for order in (0,1):
            for perception in (2,3):
                g=Graph(self.rows,"church.1104.s1",{"player.perception":perception}).run({"guide_fee":0,"tour_order":order,"patient":0})
                self.assertEqual(order==0,g.seen(80)); self.assertEqual(order==1,g.seen(258))
                self.assertEqual(order==0 and perception>=3,g.seen(173))
                self.assertEqual(order==1 and perception>=3,g.seen(272))
                self.assertTrue(g.seen(436))
        choices=[x for x in self.rows["Choice"] if ".choice.guide_fee." in x["choiceId"]]
        self.assertEqual(3,len(choices)); self.assertIn("50个国王币",next(x["text"] for x in self.rows["Text"] if x["key"]==choices[2]["textKey"]))

    def test_latest_outing_source_settles_explicit_fifty_coin_option(self):
        graph=Graph(self.rows,"church.1104.s1").run({"guide_fee":2,"tour_order":0,"patient":0})
        self.assertTrue(graph.seen(61)); self.assertTrue(graph.seen(64))
        payments=[row for row in graph.results() if row["resultType"]=="MONEY_ADD" and row["target"]=="cafe"]
        self.assertEqual(["50"],[row["value"] for row in payments])
        audit=next(row for row in self.rows["_audit"] if row["root"]=="church.1104.s1")
        self.assertEqual(480,audit["nonemptyParagraphs"])
        self.assertEqual({96,97},set(audit["normalization"]["sourceParagraphs"]))
        self.assertEqual("continuation",audit["anchors"][96]["kind"])
        self.assertEqual("merged-into-P096",audit["anchors"][96]["status"])

    def test_1105_shelter_variants_have_real_opposite_gates(self):
        for root,fact in (("church.1105.s1","church.alienor.refused_shelter"),("church.1105.s2","church.alienor.sheltered")):
            conditions=[x for x in self.rows["Condition"] if x["ownerType"]=="event" and x["ownerId"]==root]
            groups={x["groupIndex"] for x in conditions}
            self.assertEqual({1,2},groups)
            for group in groups: self.assertTrue(any(x["target"]==fact and x["value"]=="true" and x["groupIndex"]==group for x in conditions))
        g=Graph(self.rows,"church.1105.s1").run({"greet":0,"answer":0,"question_duke":0})
        self.assertFalse(g.seen(96)); self.assertTrue(g.seen(93))
        self.assertNotIn("known.alienor",[r["target"] for r in g.results()])

    def test_current_hub_routes_are_exclusive_exact_contract_ids(self):
        cases=[({"s1":0},"neutral.route.1111.s1","neutral"),({"s1":1,"s2":0},"neutral.route.1111.s1","neutral"),({"s1":1,"s2":1,"s3":1},"neutral.route.1111.s2","neutral")]
        for index,faction,target in ((0,"church","church.route.1111.s1"),(1,"rose","rose.route.1111.s2"),(2,"revolution","revolution.route.1111.s2")):
            cases.append(({"s1":1,"s2":1,"s3":0,"s4":index},target,faction))
        for picks,target,faction in cases:
            g=Graph(self.rows,"church.1111.s1").run(picks)
            results=g.results()
            self.assertEqual([target],[r["target"] for r in results if r["resultType"]=="QUEUE_EVENT"])
            self.assertEqual(["route."+faction],[r["target"] for r in results if r["target"].startswith("route.")])
        g=Graph(self.rows,"church.1111.s1",{"player.language":3}).run({"s1":1,"s2":1})
        self.assertTrue(g.seen(73)); self.assertFalse(any(r["resultType"]=="QUEUE_EVENT" for r in g.results()))
        self.assertIn("terminal.church.ending.if4",[r["target"] for r in g.results()])

    def test_all_ry_roots_require_exclusive_route_and_predecessor(self):
        for root in [r["eventId"] for r in self.rows["Event"] if ".route." in r["eventId"]]:
            cond=[r for r in self.rows["Condition"] if r["ownerType"]=="event" and r["ownerId"]==root]
            self.assertTrue(any(c["target"]=="route.church" and c["value"]=="true" for c in cond),root)
            self.assertEqual(3,len([c for c in cond if c["target"].startswith("route.") and c["value"]=="false"]))
            self.assertTrue(any(c["target"].startswith("story.") and c["target"]!="story."+root+".completed" and c["value"]=="true" for c in cond),root)

    def test_any_product_1113_real_taste_outcome_not_guessed_order(self):
        pages=[x for x in self.rows["ConversationPage"] if x["presentationKey"]=="church.route.1113.s1.conversation"]
        brew=next(p for p in pages if p["stepType"]=="Gameplay")
        self.assertEqual("taste:any:90:1",brew["gameplayKey"])
        audit=next(x for x in self.rows["_audit"] if x["root"]=="church.route.1113.s1")
        self.assertEqual("compiled",audit["status"])
        self.assertEqual("implemented",audit["anchors"][53]["status"])
        for passed in (False,True):
            g=Graph(self.rows,"church.route.1113.s1").run(tasting_passed=passed)
            self.assertEqual(passed,g.seen(56)); self.assertEqual(not passed,g.seen(58)); self.assertTrue(g.seen(79))
        self.assertFalse(any(x["key"].startswith("church.route.1113.serving.") for x in self.rows["ConditionKey"]))

    def test_actual_business_end_keys_only_remaining_root_blocker_is_source(self):
        for root in ("church.1104.s1","church.route.1112.s1","church.route.1118.s1","church.route.1123.s1"):
            pages=[p for p in self.rows["ConversationPage"] if p["presentationKey"]==root+".conversation"]
            self.assertEqual(1,len([p for p in pages if p.get("gameplayKey")=="host:end_business"]))
        self.assertEqual([], [a["root"] for a in self.rows["_audit"] if a["blockedBy"]])
        self.assertFalse(any("release_acknowledged" in c["target"] for c in self.rows["Condition"]))
        route=next(a for a in self.rows["_audit"] if a["root"]=="church.route.1123.s1")
        self.assertEqual("compiled",route["status"])
        decision=next(x for x in route["decisions"] if x["kind"]=="source-label-disambiguation")
        self.assertEqual("church.thibaut.followed",decision["fact"])

    def test_all_authored_fee_options_settle_their_actual_money(self):
        root="church.1104.s1"
        options=sorted([c for c in self.rows["Choice"] if c["groupKey"]==root+".choices.guide_fee"],key=lambda c:c["seq"])
        for option in options:
            self.assertFalse(any(c["ownerType"]=="choice" and c["ownerId"]==option["choiceId"] for c in self.rows["Condition"]))
        for fee,expected in ((0,0),(1,1),(2,50)):
            g=Graph(self.rows,root).run({"guide_fee":fee})
            money=[r for r in g.results() if r["resultType"]=="MONEY_ADD"]
            self.assertEqual(0 if fee==0 else 1,len(money))
            if money: self.assertEqual(("cafe",str(expected)),(money[0]["target"],money[0]["value"]))
        self.assertFalse(any(r["resultType"].startswith("BAR_") and r["target"] in ("cafe.money","bar:cafe.money") for r in self.rows["Result"]))

    def test_1115_follow_and_watch_are_different_choices(self):
        for follow,watch in ((0,1),(1,0)):
            g=Graph(self.rows,"church.route.1115.s1").run({"follow_thibaut":follow,"watch_revolution":watch})
            flags=[r["target"] for r in g.results()]
            self.assertEqual(follow==0,"church.thibaut.followed" in flags)
            self.assertEqual(watch==0,"church.revolution.watch_requested" in flags)
            self.assertIn("known.sade",flags)

    def test_1116_each_topic_once_in_multiple_orders_and_no_hidden_topic(self):
        text={r["key"]:r["text"] for r in self.rows["Text"]}
        for followed in (False,True):
            for reverse in (False,True):
                def picker(page,opts): return len(opts)-1 if reverse else 0
                g=Graph(self.rows,"church.route.1116.s1",facts={"church.thibaut.followed":followed}).run(picker=picker)
                self.assertEqual(1,g.lines.count("church.route.1116.s1.text.p012"))
                self.assertEqual(1,g.lines.count("church.route.1116.s1.text.p038"))
                self.assertEqual(1 if followed else 0,g.lines.count("church.route.1116.s1.text.p018"))
                self.assertEqual(1,g.lines.count("church.route.1116.s1.text.p055"))
                self.assertEqual("End",g.visited[-1]["stepType"])

    def test_1116_all_six_three_topic_orders_and_two_two_topic_orders(self):
        from itertools import permutations
        topic_anchor={"thibaut":"p008.option","stigma":"p009.option","duke":"p010.option"}
        for followed in (False,True):
            topics=["thibaut","stigma","duke"] if followed else ["thibaut","duke"]
            for order in permutations(topics):
                wanted=iter(order)
                def picker(page,opts):
                    if ".choices.topics." in opts[0]["groupKey"]:
                        topic=next(wanted)
                        return next(i for i,o in enumerate(opts) if o["textKey"].endswith(topic_anchor[topic]))
                    return 0
                g=Graph(self.rows,"church.route.1116.s1",facts={"church.thibaut.followed":followed}).run(picker=picker)
                seen=[{"p012":"thibaut","p018":"stigma","p038":"duke"}[key.rsplit(".",1)[1]] for key in g.lines if key.rsplit(".",1)[1] in {"p012","p018","p038"}]
                self.assertEqual(list(order),seen)
                self.assertEqual(1,g.lines.count("church.route.1116.s1.text.p055"))

    def test_duke_courage_failure_reply_does_not_leak_into_success(self):
        for courage in (3,4):
            g=Graph(self.rows,"church.1105.s1",{"player.courage":courage}).run({"greet":0,"answer":2,"question_duke":1})
            self.assertEqual(courage<4,g.seen(108))
            self.assertEqual(courage>=4,g.seen(102))
            self.assertTrue(g.seen(114))

    def test_endings_are_terminal_gated_not_date_only(self):
        for ending in self.rows["Ending"]:
            self.assertTrue(ending["enabled"])
            conditions=[c for c in self.rows["Condition"] if c["ownerType"]=="ending" and c["ownerId"]==ending["endingId"]]
            self.assertTrue(any(c["target"].startswith("terminal.") for c in conditions))
            self.assertTrue(any(c["target"].startswith("story.") and c["target"].endswith(".completed") for c in conditions))
            self.assertFalse(any(c["target"]=="world.calendar" for c in conditions))
        for follow in (False,True):
            g=Graph(self.rows,"church.route.1123.s1",facts={"church.thibaut.followed":follow}).run({"last_reply":1})
            targets=[r["target"] for r in g.results()]
            self.assertEqual(follow,"terminal.church.ending.ry1" in targets)
            self.assertEqual(not follow,"terminal.church.ending.ry2" in targets)
            self.assertEqual(follow,g.seen(231)); self.assertEqual(not follow,g.seen(199))

    def test_known_people_only_actual_encounters_root_tail(self):
        allowed={"known.sade":{"church.route.1115.s1","church.route.1123.s1"},"known.alienor":{"church.1009.s2","church.1104.s1","church.1105.s2","church.route.1112.s1","church.route.1116.s1","church.route.1123.s1"}}
        keys={r["key"]:r for r in self.rows["ConditionKey"]}
        for key,roots in allowed.items():
            self.assertFalse(keys[key]["initialValue"])
            producers={r["eventId"] for r in self.rows["Result"] if r["target"]==key}
            self.assertEqual(roots,producers)
            self.assertTrue(all(r["resultType"]=="SET_FLAG" and r["value"]=="true" for r in self.rows["Result"] if r["target"]==key))

    def test_no_author_directives_speak_and_mixed_paragraph_is_kept(self):
        playerkeys={x["textKey"] for x in self.rows["ConversationLine"]+self.rows["ReadingLine"]}
        for row in self.rows["Text"]:
            if row["key"] in playerkeys:
                self.assertFalse(church.directive(row["text"]),(row["key"],row["text"]))
                self.assertNotRegex(row["text"],r"【(?:解锁|资金|释放方式|隐藏标记)|\[(?:背景|场景|演出|音效|美术)")
        self.assertIn("church.route.1123.s1.text.p029",playerkeys)

    def test_all_reference_links_and_external_write_rules_against_central_source(self):
        """Read real central source tables, overlay this compiler, validate every
        emitted reference. Not a substitute for LubanNarrativeLoader.Load in Unity.
        """
        import openpyxl
        books=[openpyxl.load_workbook(PROJECT/"Assets/ConfigTables/Data/Narrative/Narrative.xlsx",read_only=True,data_only=True),
               openpyxl.load_workbook(PROJECT/"Assets/ConfigTables/Data/Shared/Text.xlsx",read_only=True,data_only=True)]
        central={}
        try:
            for book in books:
                for sheet in book:
                    values=list(sheet.values); headers=values[0]
                    central[sheet.title]=[{k:v for k,v in zip(headers,row) if k and k!="##var" and v is not None} for row in values[4:] if len(row)>1 and row[1]]
        finally:
            for book in books: book.close()
        primary={"Object":"objId","Event":"eventId","SetupParams":"setupKey","Template":"tplName","PresentationIndex":"presentationKey","ConditionKey":"key","Condition":"conditionId","Result":"resultId","ConversationPage":"pageId","ConversationLine":"lineId","Choice":"choiceId","AbilityCheck":"checkId","AbilityCheckCondition":"conditionId","ReadingPage":"pageId","ReadingLine":"lineId","Ending":"endingId","Text":"key"}
        refs={}
        for sheet,key in primary.items():
            refs[sheet]={row[key]:row for row in central.get(sheet,[]) if key in row}
            refs[sheet].update({row[key]:row for row in self.rows.get(sheet,[])})
        bars={str(r["ownerId"])+"."+str(r["barId"]):r for r in central["Bar"]}
        groups={r["groupKey"] for r in refs["Choice"].values()}
        choices=refs["Choice"]
        owners=set(refs["Event"])|{r.get("resultOwnerId",r["choiceId"]) for r in choices.values()}
        backgrounds=set(re.findall(r"^  - key: (.+)$",(PROJECT/"Assets/Resources/Dialogue/NarrativeStageVisuals.asset").read_text(encoding="utf-8-sig"),re.M))
        def exists(sheet,key,context): self.assertIn(key,refs[sheet],(context,sheet,key))
        def check_guard(value,context):
            if value.startswith("not:"): return check_guard(value[4:],context)
            if value.startswith("bar:"):
                bar,op,bound=value[4:].rsplit(":",2); self.assertIn(bar,bars,(context,value)); self.assertIn(op,{"ge","gt","le","lt","eq","ne"}); int(bound); return
            if value.startswith("choice:"): return exists("Choice",value[7:],context)
            if value.startswith("anyChoice:"):
                for cid in value[10:].split(";"): exists("Choice",cid,context)
                return
            if value.startswith("check:"):
                exists("AbilityCheck",value[6:],context); self.assertEqual("Inline",refs["AbilityCheck"][value[6:]].get("mode")); return
            if value.startswith("gameplay:"):
                self.assertTrue(value.endswith(":passed")); pid=value[9:-7]; exists("ConversationPage",pid,context)
                self.assertEqual("Gameplay",refs["ConversationPage"][pid]["stepType"]); return
            exists("ConditionKey",value,context)
        for row in self.rows["Event"]:
            exists("Object",row["actorId"],row); exists("SetupParams",row["setupKey"],row); exists("PresentationIndex",row["presentationKey"],row)
        for row in self.rows["SetupParams"]: exists("Template",row["tplName"],row)
        for row in self.rows["Object"]: exists("Text",row["nameText"],row)
        for row in self.rows["ConditionKey"]: exists("Text",row["nameText"],row)
        for row in self.rows["EventDate"]: exists("Event",row["eventId"],row)
        for row in self.rows["ConversationPage"]:
            exists("PresentationIndex",row["presentationKey"],row)
            for atom in filter(None,row.get("pageCondKeys","").split("|")): check_guard(atom,row["pageId"])
            if "optionGroupKey" in row: self.assertIn(row["optionGroupKey"],groups,row)
            if "bgOverride" in row: self.assertIn(row["bgOverride"],backgrounds,row)
            if "readingKey" in row:
                exists("PresentationIndex",row["readingKey"],row); self.assertEqual("Reading",refs["PresentationIndex"][row["readingKey"]]["presentationClass"])
                self.assertEqual(1,len([p for p in self.rows["ReadingPage"] if p["presentationKey"]==row["readingKey"]]))
            if row["stepType"]=="PassiveCheck":
                exists("AbilityCheck",row["checkId"],row)
                self.assertEqual("Inline",refs["AbilityCheck"][row["checkId"]]["mode"])
                self.assertTrue(any(line["pageId"]==row["pageId"] for line in self.rows["ConversationLine"]),row)
            if row["stepType"] in {"Gameplay","End"}:
                self.assertFalse(any(line["pageId"]==row["pageId"] for line in self.rows["ConversationLine"]),row)
                self.assertNotIn("optionGroupKey",row)
            if row["stepType"]=="Gameplay":
                if row["gameplayKey"]!="host:end_business":
                    verb,product,score,count=row["gameplayKey"].split(":")
                    self.assertIn(verb,{"taste","serve"}); self.assertIn(product,{"any","blackcoffee","milkcoffee","cocoa","espresso","latte"}); self.assertLessEqual(int(score),100); self.assertGreater(int(count),0)
        for sheet,page_sheet in (("ConversationLine","ConversationPage"),("ReadingLine","ReadingPage")):
            for row in self.rows[sheet]:
                exists(page_sheet,row["pageId"],row); exists("Text",row["textKey"],row)
                if "speakerKey" in row: exists("Object",row["speakerKey"],row)
                if "speakerNameKey" in row: exists("Text",row["speakerNameKey"],row)
        for row in self.rows["ReadingPage"]: exists("PresentationIndex",row["presentationKey"],row)
        for row in self.rows["Choice"]:
            exists("Text",row["textKey"],row)
            if "checkId" in row: exists("AbilityCheck",row["checkId"],row)
        for row in self.rows["AbilityCheckCondition"]:
            exists("AbilityCheck",row["checkId"],row); self.assertIn("player."+row["attributeId"],bars,row)
        for row in self.rows["Condition"]:
            typ=row["ownerType"]
            exists({"event":"Event","result":"Result","choice":"Choice","ending":"Ending"}[typ],row["ownerId"],row)
            if row["condType"]=="BOOL_EQ":
                if typ=="result": check_guard(row["target"],row)
                else: exists("ConditionKey",row["target"],row)
            else:
                self.assertEqual("BAR_BETWEEN",row["condType"]); self.assertIn(row["target"],bars,row)
        for row in self.rows["Result"]:
            self.assertIn(row["eventId"],owners,row)
            if row["resultType"]=="SET_FLAG": exists("ConditionKey",row["target"],row)
            elif row["resultType"]=="QUEUE_EVENT":
                exists("Event",row["target"],row); self.assertIn("entry=Immediate",refs["Event"][row["target"]]["eventProperty"],row)
            elif row["resultType"] in {"BAR_ADD","BAR_SET"}:
                bar=row["target"].removeprefix("bar:"); self.assertIn(bar,bars,row)
                self.assertNotEqual("external",bars[bar].get("sourceMode"),row)
                self.assertNotEqual("derived",bars[bar].get("advanceMode"),row)
            elif row["resultType"] in {"MONEY_ADD","MONEY_REMOVE"}: exists("Object",row["target"],row); self.assertEqual("cafe",row["target"]); int(row["value"])
            else: self.fail(("unknown result kind",row))
        for row in self.rows["Ending"]:
            exists("Text",row["titleText"],row); exists("Text",row["messageText"],row)

    def test_exact_numeric_operators_including_strict_gt(self):
        self.assertEqual("bar:faction.revolution:gt:0",church.numeric_guard("faction.revolution","gt",0))
        g=Graph(self.rows,"church.1104.s1",{"faction.revolution":0}).run()
        self.assertTrue(g.seen(445)); self.assertFalse(g.seen(459))
        g=Graph(self.rows,"church.1104.s1",{"faction.revolution":1}).run()
        self.assertFalse(g.seen(445)); self.assertTrue(g.seen(459))

if __name__=="__main__": unittest.main()
