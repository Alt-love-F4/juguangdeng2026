"""Read-only CURRENT neutral/common and route content compiler. See MD handoff.
No workbook, generated-data, runtime, schema, global-bar or attribute writes.
"""
from __future__ import annotations
from datetime import date
from hashlib import sha256
from pathlib import Path
import re
from typing import Any
from zipfile import ZipFile
import xml.etree.ElementTree as ET

SOURCE_DIR = Path("策划文件/文案6点/文案/a-中立（共通+支线）")
# Current repository snapshots, NOT old desktop/source-map paragraph numbering.
BASELINES = {
 "1007.s1": (48, "323d0c6bf2efdccbad65ccc303e2237e7f87e5e4112d521bab9e7bd289b3fbac"),
 "1008.s1": (86, "5d472f1b8fdc51487bb2171928e4ae2332a6d259024580886954a4f414519d58"),
 "1010.s1": (77, "3340b7f92006e601a784395e962f794fb2403b87e17abfff45560ea75716c99f"),
 "1010.s2": (114, "788e091ab3a06de3cc2ee78d8adc388dbb9a87cd4f9a514bdd760ccd92760120"),
 "1013.s1": (152, "b59530b617bd5eba3f84383199385ff4cf76cddd6b1a25dc4e0a5eaae3c89fb2"),
 "1018.s1": (97, "069aac8e8a9d08d485b381b7525bbf8c48404939fb12f57e71186df899705044"),
 "1027.s1": (185, "a696b215c65307bc8eec45cf02fc99437e1531ec4363a411680bf4529fce96c5"),
 "1030.s1": (107, "981cf5ed51bc4d70f86b496139c7b6558cb8f98a94acd6e292ed2d186d541853"),
 "1111.s1": (285, "6761acf77a2023acdf14d2e33d1c7b557802e307d82049b08f3392e8e769b3ce"),
 "1111.s2": (142, "5fe77793f4c54b9c62ed01a64b547d195d951b43432e80b2131b5ec14c9b47d0"),
 "1112.s1": (77, "493ebccb7348f1425f9d48661aa03b60af3a6278b6632692f52d551879386c43"),
 "1118.s1": (175, "47258b823e1ad7ecc60bbf6368b979cd25cbb56444dc08ea6b4cad69c180c7ff"),
 "1122.s1": (220, "c8ee05a0367558e537902270e364e2131348ff8f5cedf4a7867486b1bec7e221"),
 "1129.s1": (181, "3b361416ce42a512c2d40af81ae2137fa0d229498457afefbc222f8f31eba432"),
 "1130.s1": (226, "71f7039af0cc2e89acbf48c9deafb6660b91183c5b9b68da3eb39c6a493d2807"),
 "1130.s2": (133, "766121b9e432563dcf527d409c03dcacd258ceb51b43a4ccfc5e4b19c2dbefce"),
}
SHEETS = ("Event", "EventDate", "ConditionKey", "Condition", "ConversationPage", "ConversationLine",
          "Choice", "AbilityCheck", "AbilityCheckCondition", "Result", "Ending", "PresentationIndex", "Object", "Action", "Text")
# Main-authorized real generic-product protocol (2026-10-19). any is a bridge/domain
# selector, NOT a fabricated catalog product or an alias that forces blackcoffee.
# Main owns domain/bridge implementation and its actual physical-delivery tests.
GENERIC_SERVICE = "serve:any:60:1"
GENERIC_GRADE = "taste:any:60:1"
NORMAL_SCORE_PRESET, EXCELLENT_SCORE_PRESET = 60, 90
BLACK_STAGE = "church.execution.black"  # Existing registered solid black, not execution artwork.
SPEAKERS = {
 "{playername}": "player", "店长": "player", "阿黛尔·米尼耶": "adele", "阿黛尔": "adele",
 "柯莱特·布吕内": "colette", "柯莱特": "colette", "安托万·马尚": "antoine", "安托万": "antoine",
 "埃莉诺": "alienor", "阿莉耶诺尔·康多尔": "alienor", "埃利昂": "elion", "埃利昂公爵": "elion", "吕西安": "lucien",
 "店主": "neutral.1008.shopkeeper", "？？？": "neutral.voice.unknown", "记者": "neutral.reporter",
 "监察司官员": "neutral.inspector", "官方代表": "neutral.official", "送货伙计": "neutral.delivery",
 "顾客A": "guest_a", "顾客B": "guest_b", "顾客C": "guest_c", "顾客D": "guest_d",
}
GENERIC_OBJECTS = {"neutral.voice.unknown": "？？？", "neutral.reporter": "记者", "neutral.inspector": "监察司官员",
                   "neutral.official": "官方代表", "neutral.delivery": "送货伙计"}
EMOTIONS = {"开心": "happy", "微笑": "smile", "平静": "neutral", "惊讶": "surprised", "思考": "thinking",
            "得意": "proud", "歉意": "apologetic", "慌张": "surprised"}


def _paragraphs(path: Path) -> list[str]:
    ns = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
    with ZipFile(path) as archive:
        tree = ET.fromstring(archive.read("word/document.xml"))
    result = []
    for p in tree.iter(ns + "p"):
        value = "".join((n.text or "") if n.tag == ns + "t" else "\n" if n.tag in (ns + "br", ns + "cr")
                        else "\t" if n.tag == ns + "tab" else "" for n in p.iter())
        if value.strip():
            result.append(value)
    return result


def _is_direction(value: str) -> bool:
    v = value.strip()
    return bool(re.match(r"^(?:【|\[|（柯莱特好感度)", v)
                or re.match(r"^(?:MA70|时间[：:]|背景|出场人物|触发条件|选择|选项|条件[ABC])", v)
                or re.match(r"^[ABC][.．]|^[①②③]|^[一二三四五六七八九十]+(?:[、：:]|$)", v)
                or v in {"共同结尾", "共同剧情", "接待剧情", "接待剧情尾声", "事件尾声", "附加剧情尾声", "活动尾声", "语言检定成功", "语言检定失败"}
                or v.startswith(("事件尾声B", "附加剧情：")))


def _line(value: str) -> tuple[str | None, str, str | None]:
    m = re.match(r"^([^：:]+)[：:]\s*(.*)$", value, re.S)
    if m:
        label = re.sub(r"[（(]#[^）)]*[）)]", "", m[1]).strip()
        if label in SPEAKERS:
            em = re.search(r"#([^）)]+)", m[1])
            return SPEAKERS[label], m[2].strip(), EMOTIONS.get(em[1]) if em else None
    return None, value.strip(), None


def _source_id(path: Path) -> str:
    m = re.search(r"(10|11)\.(\d\d)-S(\d)", path.name)
    if not m:
        raise ValueError("Unrecognized current neutral source: " + path.name)
    return m[1] + m[2] + ".s" + m[3]


def _root(sid: str) -> str:
    if sid == "1007.s1": return "oct07.s1"
    if sid == "1013.s1": return "neutral.1013.s2"
    return "neutral." + ("route." if sid.startswith("11") else "") + sid


def completed(root: str) -> str:
    return "story." + root + ".completed"


def both(*guards: str) -> str:
    return "|".join(g for g in guards if g)


class _Compiler:
    def __init__(self, project_root: Path):
        self.project_root = project_root
        self.rows: dict[str, Any] = {s: [] for s in SHEETS}
        self.rows.update(_audit=[], _replace_roots=[])
        self.flags, self.objects, self.texts = set(), set(), {}

    def add(self, sheet: str, **row: Any) -> dict:
        self.rows[sheet].append(row)
        return row

    def text(self, key: str, value: str, comment: str = "") -> str:
        if key in self.texts:
            if self.texts[key] != value: raise ValueError("Text collision: " + key)
            return key
        self.texts[key] = value
        self.add("Text", key=key, text=value, comment=comment)
        return key

    def flag(self, key: str) -> str:
        if key not in self.flags:
            self.flags.add(key)
            self.add("ConditionKey", key=key, nameText=self.text("condition."+key,key), initialValue=False, onlySetTrue=True)
        return key

    def cond(self, owner_type: str, owner: str, target: str, value: bool = True,
             group: int = 1, kind: str = "BOOL_EQ", raw: str | None = None):
        self.add("Condition", conditionId=f"{owner}.c{len(self.rows['Condition'])+1:04}", ownerType=owner_type,
                 ownerId=owner, groupIndex=group, condType=kind, target=target,
                 value=raw if raw is not None else str(value).lower())

    def object(self, obj_id: str):
        if obj_id not in GENERIC_OBJECTS or obj_id in self.objects: return
        self.objects.add(obj_id)
        name = self.text(obj_id + ".name", GENERIC_OBJECTS[obj_id], "Generic role; portrait pending")
        self.add("Object", objId=obj_id, objType="npc", nameText=name, tags="neutral,narrative,generic,portrait_pending")


class _Root:
    def __init__(self, compiler: _Compiler, path: Path, sid: str):
        self.c, self.id, self.source_id = compiler, _root(sid), sid
        self.source = path.relative_to(compiler.project_root).as_posix()
        self.paras = _paragraphs(path)
        count, baseline = BASELINES[sid]
        digest = sha256(path.read_bytes()).hexdigest()
        if len(self.paras) != count or digest != baseline:
            raise ValueError(f"Current neutral source drift: {self.source}; re-diff graph spans before updating baseline")
        self.audit = dict(source=self.source, source_sha256=digest, source_paragraphs=count, root=self.id,
                          supported_events=[self.id], status="compiled", blockedBy=[], pending=[], anchors=[])
        self.c.rows["_audit"].append(self.audit)
        self.claimed, self.pages, self.choice_rows = {}, [], {}
        self.background, self.event, self.completion_guards = "scene.cafe", None, ""
        # Existing oct07.s1 Text must also remain untouched.
        if sid != "1007.s1":
            for i, p in enumerate(self.paras, 1):
                if not _is_direction(p):
                    speaker, value, _ = _line(p)
                    compiler.text(self.key(i), value, f"{self.source}#P{i:03}; current source")
                    compiler.object(speaker)

    def key(self, anchor: int) -> str:
        return f"{self.id}.text.p{anchor:03}"

    def mark(self, anchor: int, kind: str, **data):
        self.claimed.setdefault(anchor, []).append(dict(kind=kind, **data))

    def issue(self, message: str, anchors: tuple[int, ...] = (), blocking: bool = False):
        self.audit["blockedBy" if blocking else "pending"].append(dict(reason=message, sourceAnchors=[f"P{x:03}" for x in anchors]))

    def block(self, code: str, message: str, anchors: tuple[int, ...] = ()):
        self.issue(message, anchors, True)
        self.audit["status"] = "construction-blocked"
        self.c.cond("event", self.id, self.c.flag("blocked."+self.id+"."+code))
        prop = self.event["eventProperty"]
        self.event["eventProperty"] = prop + ("," if ";blockedBy=" in prop else ";blockedBy=") + code

    def start(self, *, slot: str | None = "morning", prerequisites: tuple[str, ...] = (), entry: str = "Immediate"):
        self.c.flag(completed(self.id))
        self.c.rows["_replace_roots"].append(self.id)
        title = self.c.text(self.id + ".title", self.paras[0].removeprefix("MA70."), self.source)
        self.event = self.c.add("Event", eventId=self.id, eventKind="world", actorId="world", onceOnly=True,
                               titleText=title, presentationClass="Conversation", presentationKey=self.id,
                               logTag=self.id, presentationLogTag=self.id, eventProperty=f"entry={entry};blocksSave=true")
        self.c.add("PresentationIndex", presentationKey=self.id, presentationClass="Conversation", logTag=self.id)
        self.c.cond("event", self.id, completed(self.id), False)
        for pre in prerequisites:
            self.c.flag(pre)
            self.c.cond("event", self.id, pre)
        if ".route." in self.id:
            self.c.cond("event", self.id, "route.neutral")
            for faction in ("rose","church","revolution"):
                self.c.cond("event", self.id, "route."+faction, False)
        if slot:
            mmdd = self.source_id[:4]
            day = (date(670, int(mmdd[:2]), int(mmdd[2:])) - date(670, 10, 6)).days + 1
            self.c.add("EventDate", eventId=self.id, day=day, timeSlot=slot, repeatRule="once", priority=70)
            self.audit["scheduleDecision"] = dict(day=day, timeSlot=slot, reason="Current source date; common/route date scheduling is an integration decision, not a second calendar")

    def _bg(self, raw: str, anchor: int) -> str | None:
        if not re.match(r"^(?:【)?背景", raw.strip()): return None
        if any(x in raw for x in ("原沸点咖啡馆", "新咖啡馆", "小镇")): value = None
        elif "沸点咖啡馆" in raw: value = "scene.cafe"
        elif "黑幕" in raw: value = BLACK_STAGE
        elif "圣徒街" in raw or "圣诺特街道" in raw: value = "street.night" if "夜" in raw else "street.day"
        elif "大堂区杂货铺" in raw: value = "market.shop.day"
        elif "大堂区" in raw: value = "market.day"
        else: value = None
        if value is not None and value.startswith("street."):
            self.issue("Registered generic Saint Street staging for source street lighting/time; not a new room/background resource.",(anchor,))
        if value is None:
            self.issue("Art pending; explicit registered neutral black rather than unrelated inherited scene: " + raw, (anchor,))
            value = BLACK_STAGE
        return value

    def page(self, suffix: str, guard: str = "", **fields) -> dict:
        row = self.c.add("ConversationPage", pageId=self.id+"."+suffix, presentationKey=self.id,
                         seq=len(self.pages)+1, pageCondKeys=guard or None, **fields)
        self.pages.append(row)
        return row

    def span(self, first: int, last: int, guard: str = "", bg: str | None = None):
        if bg: self.background = bg
        current, seq = None, 0
        for anchor in range(first, last+1):
            raw = self.paras[anchor-1]
            new_bg = self._bg(raw, anchor)
            if new_bg:
                self.background, current = new_bg, None
            if _is_direction(raw):
                self.mark(anchor, "direction", guard=guard)
                continue
            if current is None:
                current = self.page(f"p{anchor:03}", guard, bgOverride=self.background, stepType="Dialogue")
                seq = 0
            seq += 1
            speaker, value, emotion = _line(raw)
            if not value: raise ValueError(f"Empty source line {self.source} P{anchor}")
            line_id = f"{self.id}.line.p{anchor:03}"
            self.c.add("ConversationLine", lineId=line_id, pageId=current["pageId"], seq=seq,
                       speakerKey=speaker, side="none" if speaker in (None,"player") else "right",
                       textKey=self.key(anchor), emote=emotion)
            self.mark(anchor, "line", lineId=line_id, guard=guard)

    def check(self, name: str, attribute: str, threshold: int) -> str:
        cid = self.id+".check."+name
        self.c.add("AbilityCheck", checkId=cid, mode="Inline", comment=self.source)
        self.c.add("AbilityCheckCondition", conditionId=cid+".threshold", checkId=cid, attributeId=attribute, threshold=threshold, groupIndex=1, seq=1)
        return cid

    def choices(self, group: str, anchors: tuple[int,...], checks: dict | None = None, guard: str = "") -> dict[str,str]:
        gid = self.id+"."+group
        self.page(group, guard, bgOverride=self.background, stepType="Dialogue", optionGroupKey=gid)
        result = {}
        for index, anchor in enumerate(anchors):
            letter = chr(ord("a")+index)
            cid = self.id+".choice."+group+"."+letter
            raw = self.paras[anchor-1].strip()
            match = re.search(r"「(.*?)」", raw, re.S)
            label = match[1] if match else re.sub(r"^选择", "", raw)
            self.c.text(self.key(anchor), label, f"{self.source}#P{anchor:03}; choice")
            check = self.check(group+letter, *checks[letter]) if checks and letter in checks else None
            row = self.c.add("Choice", choiceId=cid, groupKey=gid, textKey=self.key(anchor), resultOwnerId=cid+".result", seq=index+1, checkId=check)
            self.choice_rows[cid] = row
            result[letter] = cid
            self.mark(anchor, "choice", choiceId=cid)
        return result

    def unavailable(self, cid: str, code: str, message: str, anchors: tuple[int,...] = ()):
        flag = self.c.flag("blocked.neutral."+code)
        self.c.cond("choice", cid, flag)
        self.issue(message, anchors, True)
        self.audit["blockedBy"][-1].update(scope="choice", choiceId=cid, conditionKey=flag)
        if self.audit["status"] != "construction-blocked": self.audit["status"] = "partial-branches-blocked"

    def effect(self, suffix: str, kind: str, target: str, value: str = "true", *, choice: str | None = None, guard: str = "", anchor: int | None = None):
        owner = self.choice_rows[choice]["resultOwnerId"] if choice else self.id
        rid = self.id+".result."+suffix
        if kind == "SET_FLAG": self.c.flag(target)
        self.c.add("Result", resultId=rid, eventId=owner, seq=len(self.c.rows["Result"])+1, resultType=kind, target=target, value=value,
                   note="Root-tail settlement; "+self.source+(f"#P{anchor:03}" if anchor else ""))
        for term in filter(None, guard.split("|")): self.c.cond("result", rid, term)
        if anchor: self.mark(anchor, "result", resultId=rid, guard=guard, choiceId=choice)

    def affection(self, suffix: str, target: str, value: int, choice: str, anchor: int, guard: str = ""):
        self.effect(suffix, "BAR_ADD", target+".affection", str(value), choice=choice, guard=guard, anchor=anchor)

    def gameplay(self, suffix: str, key: str, anchor: int, guard: str = "") -> str:
        row = self.page(suffix, guard, bgOverride="scene.cafe", stepType="Gameplay", gameplayKey=key)
        self.mark(anchor, "gameplay", pageId=row["pageId"], gameplayKey=key, guard=guard)
        return "gameplay:"+row["pageId"]+":passed"

    def end(self, suffix: str = "end", guard: str = ""):
        self.page(suffix, guard, stepType="End")

    def ending(self, label: str, title: str, *, guard: str = "", anchor: int | None = None):
        flag = self.c.flag("story."+self.id+".ending."+label)
        self.effect("ending."+label, "SET_FLAG", flag, guard=guard, anchor=anchor)
        eid = "neutral.ending."+label
        self.c.add("Ending", endingId=eid, enabled=True, priority=100,
                   titleText=self.c.text(eid+".title", title, self.source), messageText=self.c.text(eid+".message", title, self.source))
        for f in (completed(self.id), flag, "route.neutral"): self.c.cond("ending", eid, f)

    def finish(self, guard: str = ""):
        self.end()
        self.effect("complete", "SET_FLAG", completed(self.id), guard=guard)
        self.completion_guards = guard

    def audit_finish(self):
        unhandled = []
        for anchor, raw in enumerate(self.paras, 1):
            uses = self.claimed.get(anchor, [])
            if not uses:
                kind = "preserved-existing" if self.audit["status"] == "preserved-existing" else "source-directive" if _is_direction(raw) else "uncompiled"
                uses = [dict(kind=kind)]
                if kind == "uncompiled": unhandled.append(anchor)
            self.audit["anchors"].append(dict(anchor=f"P{anchor:03}", raw=raw, uses=uses))
        if unhandled: raise ValueError(f"Unaccounted source prose {self.source}: {unhandled}")
        self.audit["completionGuards"] = self.completion_guards
        self.audit["runtimeAssumptions"] = ["Inline ability checks", "guarded root-tail result-owner settlement", "End terminates root", "global bars/routes MAIN-owned"]
        self.audit["stageDirections"] = "Audio/camera/fade/subtitle directions retained as anchors, not player dialogue or claimed implemented."


def _choice(cid: str) -> str: return "choice:"+cid

def _check(r: _Root, name: str, passed: bool = True) -> str:
    guard = "check:"+r.id+".check."+name
    return guard if passed else "not:"+guard


def _compile_common(r: _Root):
    sid = r.source_id
    if sid == "1007.s1":
        r.audit["status"] = "preserved-existing"
        r.issue("Existing oct07.s1 Event/Conversation/Result/Text retained unchanged; current source audited, not rebuilt.")
        return
    if sid == "1008.s1":
        r.start(slot=None, entry="MapAction")
        r.c.add("Action", actionKey=r.id+".action", type="ONCE", mustExecute=True,
                nameText=r.c.text(r.id+".action.name","购买蜡烛与亚麻擦布",r.source+"#P014"),
                descText=r.c.text(r.id+".action.desc","前往大堂区杂货铺采购。",r.source),
                resultEvents=r.id, durationDays=0, progressTarget=0, deliveryMode="None",
                replacesLocationActions=False, focusLocationOnComplete=False)
        # Existing Action/MapPosition remain owned by main and retain their current IDs.
        for typ, owner in [("event",r.id), ("action",r.id+".action")]:
            r.c.cond(typ, owner, "world.calendar", kind="BAR_BETWEEN", raw="3,3")
            r.c.cond(typ, owner, "world.map.active")
            if typ == "action": r.c.cond(typ, owner, completed(r.id), False)
        r.span(4,34,bg="market.day")
        q = r.choices("q1",(36,37,38),{"b":("language",2)})
        r.span(40,44,_choice(q["a"]))
        r.span(48,49,both(_choice(q["b"]),_check(r,"q1b")))
        r.span(53,54,both(_choice(q["b"]),_check(r,"q1b",False)))
        r.span(56,59,_choice(q["c"]))
        r.span(60,85,bg="market.shop.day")
        r.affection("help","colette",2,q["a"],45)
        r.affection("talk","colette",1,q["b"],50,_check(r,"q1b"))
        r.effect("known.colette","SET_FLAG","known.colette")
        r.issue("Daily importer owns unresolved 大堂区采购 activity; not fabricated here.",(86,))
        r.finish()
    elif sid == "1010.s1":
        r.start(prerequisites=(completed("rose.1009.s1"),))
        r.span(5,32)
        q = r.choices("q1",(34,35,36),{"b":("wisdom",3),"c":("language",2)})
        r.span(38,40,_choice(q["a"]))
        r.span(44,46,both(_choice(q["b"]),_check(r,"q1b")))
        r.span(49,51,both(_choice(q["b"]),_check(r,"q1b",False)))
        r.span(54,57,both(_choice(q["c"]),_check(r,"q1c")))
        r.span(60,62,both(_choice(q["c"]),_check(r,"q1c",False)))
        r.span(64,76)
        r.affection("agreement","adele",1,q["a"],41)
        r.affection("recognition","adele",2,q["c"],58,_check(r,"q1c"))
        r.effect("cost_saving","SET_FLAG","neutral.promotion.cost_saving",choice=q["b"],guard=_check(r,"q1b"),anchor=47)
        r.issue("Promotion cost reduction has no amount/formula. Record fact only; no invented money/growth delta.",(47,))
        r.issue("Current 10/10 says two days later but current activity file is 10/13-S1; preserve both, editorial correction required.",(66,77))
        r.effect("continue","QUEUE_EVENT","neutral.1010.s2")
        r.finish()
    elif sid == "1010.s2":
        r.start(slot=None,prerequisites=(completed("neutral.1010.s1"),))
        r.issue("Current S2 explicitly requires promotion preparation but describes pre-opening morning; S1 already has customers. Queue follows the explicit prerequisite without pretending to rewind the cafe clock; chronology prose requires editorial decision.",(2,3,4))
        r.span(3,14)
        q = r.choices("q1",(16,17,18),{"b":("language",2)})
        met = "anyChoice:"+q["a"]+";"+q["b"]
        r.span(20,21,_choice(q["a"]))
        r.span(25,26,both(_choice(q["b"]),_check(r,"q1b")))
        r.span(30,31,both(_choice(q["b"]),_check(r,"q1b",False)))
        r.span(34,38,_choice(q["c"]))
        r.span(43,57,met)
        q2 = r.choices("q2",(59,60,61),{"a":("language",3)},guard=met)
        r.span(64,69,both(met,_choice(q2["a"]),_check(r,"q2a")))
        r.span(74,76,both(met,_choice(q2["a"]),_check(r,"q2a",False)))
        r.span(79,82,both(met,_choice(q2["b"])))
        r.span(85,89,both(met,_choice(q2["c"])))
        r.span(92,104,met)
        r.span(107,109,_choice(q["c"]))
        r.span(112,113)
        r.affection("welcome","antoine",1,q["b"],27,_check(r,"q1b"))
        r.affection("business","antoine",2,q2["a"],70,both(met,_check(r,"q2a")))
        r.effect("appreciation","SET_FLAG","neutral.antoine.appreciation",choice=q2["a"],guard=both(met,_check(r,"q2a")),anchor=71)
        r.effect("known.antoine","SET_FLAG","known.antoine",guard=met,anchor=114)
        r.finish()
    elif sid == "1013.s1":
        r.start(prerequisites=(completed("neutral.1010.s1"),))
        r.issue("Main-authorized serve:any follows the source unspecified coffee request; actual domain/bridge service must supply and score physical orders. No forced blackcoffee or automatic completion.",(18,19,75))
        r.span(3,18)
        first = r.gameplay("first_service",GENERIC_SERVICE,19)
        r.span(20,35,first)
        q = r.choices("q1",(37,38,39),{"b":("wisdom",2),"c":("language",3)},guard=first)
        r.span(41,44,both(first,_choice(q["a"])))
        r.span(49,52,both(first,_choice(q["b"]),_check(r,"q1b")))
        r.span(55,57,both(first,_choice(q["b"]),_check(r,"q1b",False)))
        r.span(61,65,both(first,_choice(q["c"]),_check(r,"q1c")))
        r.span(68,71,both(first,_choice(q["c"]),_check(r,"q1c",False)))
        activity = r.gameplay("activity",GENERIC_SERVICE,74,first)
        done = both(first,activity)
        r.span(75,81,done)
        extra = both(done,r.c.flag("neutral.antoine.appreciation"))
        r.span(87,98,extra)
        q2 = r.choices("q2",(100,101,102),{"a":("language",3)},guard=extra)
        r.unavailable(q2["b"],"promotion_wisdom_conflict","P101 wisdom>=3 contradicts P112 wisdom>=2. No guessed check threshold.",(101,112))
        r.span(105,107,both(extra,_choice(q2["a"]),_check(r,"q2a")))
        r.span(110,111,both(extra,_choice(q2["a"]),_check(r,"q2a",False)))
        conflict = "blocked.neutral.promotion_wisdom_conflict"
        r.span(114,116,both(extra,_choice(q2["b"]),conflict))
        r.span(119,120,both(extra,_choice(q2["b"]),"not:"+conflict))
        r.span(122,124,both(extra,_choice(q2["c"])))
        r.span(126,136,extra)
        r.span(139,152,done)
        r.affection("cooperation","adele",1,q["a"],45,done)
        r.affection("long_term","antoine",2,q2["a"],108,both(extra,_check(r,"q2a")))
        r.mark(117,"blocked-directive",reason="Conflicting threshold P101/P112, no fake reward")
        r.effect("observed","SET_FLAG","neutral.antoine.observation",guard=extra)
        for name,cid,check,anchor in [("organized",q["b"],"q1b",53),("recommended",q["c"],"q1c",66)]:
            r.effect(name,"SET_FLAG","neutral.promotion."+name,choice=cid,guard=both(done,_check(r,check)),anchor=anchor)
        r.issue("Bounded activity segment: one additional actual served order after role choice, not simulated full shift. Both deliveries gate completion/C13. Count=1 and normal60 are tunable implementation presets.",(19,74,75))
        r.issue("Small activity revenue increase lacks formula/amount; facts retained, no unknown money/growth gain.",(53,66))
        r.finish(done)
        r.effect("c13_continuation","QUEUE_EVENT","revolution.1013.s2",guard=done)
        r.audit["externalQueueEdges"] = [dict(target="revolution.1013.s2", guard=done, when="accepted root-tail settlement only; no date autofire")]
    elif sid == "1018.s1":
        r.start(slot="noon",prerequisites=(completed("neutral.1013.s2"),))
        r.issue("Main-authorized taste:any completes only from a real physical delivery and actual quality result; low score returns false, not cancellation. Generic selector is source-appropriate, not a guessed blackcoffee order.",(9,13,40))
        r.span(3,11)
        grade = r.gameplay("reporter_grade",GENERIC_GRADE,13)
        r.span(14,16)
        r.span(18,19,grade)
        r.span(21,22,"not:"+grade)
        r.span(23,39)
        r.span(42,54,grade)
        r.span(58,67,"not:"+grade)
        q = r.choices("advert",(69,78),guard="not:"+grade)
        r.unavailable(q["a"],"reporter_advert_price","No ad price/real-money debit specified; paid option disabled, never free advertising.",(65,66,68,77))
        r.span(70,76,both("not:"+grade,_choice(q["a"])))
        r.span(79,85,both("not:"+grade,_choice(q["b"])))
        r.span(87,97)
        r.effect("newspaper_recommendation","SET_FLAG","neutral.newspaper.recommendation",guard=grade,anchor=55)
        r.mark(77,"blocked-directive",reason="No posted ad without authorized cost/debit")
        r.issue("News importer consumes recommendation/ad state for next issue; no invented circulation bonus.",(55,77))
        r.finish()
    elif sid == "1027.s1":
        r.start(slot="noon",prerequisites=(completed("rose.1016.s1"),))
        r.span(4,42)
        q = r.choices("q1",(44,45,46),{"a":("wisdom",4),"b":("language",3)})
        r.span(49,58,both(_choice(q["a"]),_check(r,"q1a")))
        r.span(62,68,both(_choice(q["a"]),_check(r,"q1a",False)))
        r.span(73,82,both(_choice(q["b"]),_check(r,"q1b")))
        r.span(86,93,both(_choice(q["b"]),_check(r,"q1b",False)))
        r.span(97,102,_choice(q["c"]))
        r.span(108,184)
        r.affection("chess_success","alienor",2,q["a"],59,_check(r,"q1a"))
        r.affection("chess_learning","alienor",1,q["b"],83,_check(r,"q1b"))
        r.affection("chess_resignation","alienor",-1,q["b"],94,_check(r,"q1b",False))
        r.effect("alienor_dialogue","SET_FLAG","neutral.alienor.dialogue",anchor=185)
        r.issue("Daily importer owns unlocked Alienor exclusive topic/pool.",(185,))
        r.finish()
    elif sid == "1030.s1":
        r.start(slot="noon")
        r.c.cond("event",r.id,"adele.affection",kind="BAR_GTE",raw="3")
        r.span(3,23)
        q = r.choices("q1",(25,26,27),{"b":("perception",4)})
        r.span(29,32,_choice(q["a"]))
        r.span(37,43,both(_choice(q["b"]),_check(r,"q1b")))
        r.span(46,49,both(_choice(q["b"]),_check(r,"q1b",False)))
        r.span(53,57,_choice(q["c"]))
        r.span(61,105)
        r.affection("admire","adele",1,q["a"],33)
        r.affection("arrange","adele",2,q["b"],44,_check(r,"q1b"))
        r.affection("rest","adele",-1,q["c"],58)
        r.effect("adele_dreams","SET_FLAG","neutral.adele.dreams_unlocked",anchor=107)
        r.effect("adele_city_talk_closed","SET_FLAG","neutral.adele.city_talk_closed",anchor=106)
        r.issue("Daily importer enforces source exclusive-topic lock/unlock.",(106,107))
        r.finish()
    else:
        raise ValueError("Unrecognized common source: "+sid)


def _compile_route(r: _Root):
    sid = r.source_id
    if sid == "1111.s1":
        # Queue-only from CENTRAL church split: no independent date trigger.
        r.start(slot=None)
        r.span(3,282,bg=BLACK_STAGE)
        r.mark(283,"ending-title")
        r.ending("a","平凡之日",anchor=284)
        r.finish()
        r.issue("Central church.1111.s1 owns informer decision and QueueEvent to this root; no duplicate split date entry.")
    elif sid == "1111.s2":
        r.start(slot=None)
        r.span(3,142)
        r.finish()
        r.issue("Central church.1111.s1 owns successful language>=4/non-disclosure/neutral choice and exclusive route flags BEFORE QueueEvent; no duplicate check.")
    elif sid == "1112.s1":
        r.start(prerequisites=(completed("neutral.route.1111.s2"),"neutral.antoine.observation"))
        r.span(3,31)
        q = r.choices("membership",(33,34))
        r.span(36,42,_choice(q["a"]))
        r.span(46,51,_choice(q["b"]))
        r.span(54,77)
        r.effect("association_member","SET_FLAG","neutral.association.member",choice=q["a"],anchor=43)
        r.issue("Membership money/revenue/customer bonus values unset. Set real membership only, no unknown gains.",(43,))
        r.finish()
    elif sid == "1118.s1":
        r.start(prerequisites=(completed("neutral.route.1111.s2"),))
        # Only the source-supported royal-protection choice is currently executable.
        # Gate the EVENT as well as the choice: never open an all-disabled menu for
        # low/missing affection, and never pretend missing terminal/payment rules work.
        r.c.cond("event",r.id,"alienor.affection",kind="BAR_GTE",raw="3")
        r.audit["resolvedNormalPath"] = dict(
            choiceId=r.id+".choice.investigation.b", fact="neutral.investigation.shelter",
            sourceAnchors=["P066","P081","P107"],
            eventRequirement=dict(condType="BAR_GTE",target="alienor.affection",value="3"),
            explanation="Normal royal-protection branch is open. Low or unavailable Alienor affection cannot enter because refusal end_if4 and paid bribery remain separately disabled.")
        r.issue("Main-authorized bounded entry requires alienor.affection>=3, matching the only available source choice P066/P081. This event-level restriction prevents an all-disabled menu; it is an integration safety decision, not an invented whole-event source prerequisite.",(66,81))
        r.span(3,63)
        q = r.choices("investigation",(65,66,67))
        r.c.cond("choice",q["b"],"alienor.affection",kind="BAR_GTE",raw="3")
        r.unavailable(q["a"],"missing_end_if4","Refusal targets missing end if4; not silently replaced by A/C or ordinary continuation.",(79,))
        r.unavailable(q["c"],"investigation_bribe","Large-money requirement/debit and failure terminal outcome unset.",(67,109,125,136,144))
        r.span(69,78,_choice(q["a"]))
        r.end("refusal_end",_choice(q["a"]))
        sheltered = both(_choice(q["b"]),"bar:alienor.affection:ge:3")
        r.span(82,106,sheltered)
        bribe = both(_choice(q["c"]),"blocked.neutral.investigation_bribe")
        r.span(110,124,_choice(q["c"]))
        r.span(127,135,bribe)
        r.span(139,143,both(_choice(q["c"]),"not:blocked.neutral.investigation_bribe"))
        r.end("bribe_failure_end",both(_choice(q["c"]),"not:blocked.neutral.investigation_bribe"))
        r.span(147,175,sheltered)
        r.effect("shelter","SET_FLAG","neutral.investigation.shelter",choice=q["b"],guard=sheltered,anchor=107)
        r.c.flag("neutral.investigation.bribed")  # Never produced without real payment.
        r.finish(sheltered)
    elif sid == "1122.s1":
        r.start(prerequisites=(completed("neutral.route.1118.s1"),))
        r.c.cond("event",r.id,"neutral.investigation.shelter")
        r.span(3,20)
        r.span(24,36,"neutral.investigation.shelter")
        r.issue("P29-P31 letter content currently remains authored narration, not LetterReveal/LetterRead animation; source anchors preserved.",(28,29,30,31))
        r.span(40,53,both("neutral.investigation.bribed","neutral.association.member"))
        r.span(56,60,both("neutral.investigation.bribed","not:neutral.association.member"))
        r.span(64,90)
        q = r.choices("future",(92,93))
        r.span(95,109,_choice(q["a"]))
        r.end("stay_end",_choice(q["a"]))
        r.span(113,217,_choice(q["b"]))
        r.mark(218,"ending-title")
        r.effect("stayed","SET_FLAG","neutral.stayed",choice=q["a"],anchor=110)
        r.ending("b","远方的咖啡馆",guard=_choice(q["b"]),anchor=219)
        r.finish()
        r.issue("Bribery variant staged/guarded but unavailable until 1118 payment rules exist; event readiness currently requires actual shelter.",(38,))
    elif sid == "1129.s1":
        r.start(prerequisites=(completed("neutral.route.1122.s1"),"neutral.stayed","neutral.association.member"))
        r.block("neutral_revenue_threshold","Source revenue standard has no amount/period/provider. Never guess threshold or use wallet money as cafe revenue.",(2,))
        r.span(3,181)
        r.effect("committee","SET_FLAG","neutral.association.committee",anchor=101)
        r.finish()
    elif sid == "1130.s1":
        r.start(prerequisites=(completed("neutral.route.1129.s1"),"neutral.association.committee","neutral.stayed"))
        r.span(3,58,bg=BLACK_STAGE)
        cid = r.check("negotiation","language",5)
        r.mark(59,"check",checkId=cid)
        passed,failed = "check:"+cid,"not:check:"+cid
        first_success_page = len(r.pages)
        r.span(61,87,passed,bg=BLACK_STAGE)
        # PassiveCheck must contain actual success prose and evaluate before its guard.
        r.pages[first_success_page].update(stepType="PassiveCheck", checkId=cid, pageCondKeys=None)
        r.span(91,105,failed,bg=BLACK_STAGE)
        r.end("negotiation_failure_end",failed)
        r.span(110,223,passed,bg=BLACK_STAGE)
        r.mark(224,"ending-title")
        r.effect("negotiation_success","SET_FLAG","neutral.negotiation.success",guard=passed,anchor=88)
        r.effect("negotiation_failure","SET_FLAG","neutral.negotiation.failed",guard=failed,anchor=106)
        r.effect("failure_continuation","QUEUE_EVENT","neutral.route.1130.s2",guard=failed,anchor=107)
        r.ending("d","商业大亨",guard=passed,anchor=225)
        r.finish()
        r.issue("Complete graph dormant behind 1129 revenue blocker. Failure Ends root and queues C; never reaches D epilogue.")
    elif sid == "1130.s2":
        r.start(prerequisites=(completed("neutral.route.1122.s1"),"neutral.stayed"))
        # Group1 non-member, group2 actual negotiation failure; duplicate ALL common gates.
        r.c.cond("event",r.id,"neutral.association.member",False)
        for flag,value in [(completed(r.id),False),("route.neutral",True),("route.rose",False),("route.church",False),("route.revolution",False),(completed("neutral.route.1122.s1"),True),
                           ("neutral.stayed",True),("neutral.negotiation.failed",True)]:
            r.c.cond("event",r.id,flag,value,group=2)
        r.span(3,48,bg=BLACK_STAGE)
        r.span(50,52,"not:neutral.association.member")
        r.c.flag("neutral.revenue.below_standard")
        r.span(54,56,both("neutral.association.member","neutral.revenue.below_standard","not:neutral.negotiation.failed"))
        r.span(58,61,"neutral.negotiation.failed")
        r.span(63,130)
        r.ending("c","沸点依旧",anchor=131)
        r.finish()
        r.issue("Member-but-revenue-low alternative blocked until metric/threshold is authorized. Missing 1129 content is NOT failed revenue. Nonmembers/actual failed negotiation remain correct.",(53,))
    else:
        raise ValueError("Unrecognized route source: "+sid)


def build(project_root: Path) -> dict[str, Any]:
    """Existing Narrative sheet lists + Text/_audit/_replace_roots; deterministic, no writes.
    Fail closed on source drift, missing/duplicate files, or unclaimed source prose.
    """
    project_root = Path(project_root).resolve()
    paths = sorted((project_root/SOURCE_DIR).glob("*.docx"))
    sources = {_source_id(p):p for p in paths}
    if len(paths)!=16 or len(sources)!=16 or set(sources)!=set(BASELINES):
        raise ValueError("Expected exactly the 16 CURRENT neutral DOCX files")
    c = _Compiler(project_root)
    for flag in ("known.colette","known.antoine","neutral.antoine.appreciation","neutral.antoine.observation",
                 "neutral.association.member","neutral.investigation.shelter","neutral.investigation.bribed",
                 "neutral.stayed","neutral.negotiation.failed","neutral.negotiation.success"):
        c.flag(flag)
    for sid in BASELINES:
        r = _Root(c,sources[sid],sid)
        if sid.startswith("10"): _compile_common(r)
        else: _compile_route(r)
        r.audit_finish()
    return c.rows
