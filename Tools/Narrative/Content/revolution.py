"""Current revolutionary DOCX -> existing NCS sheet rows. Pure compile; never generate.
Read INTEGRATION_CONTRACT.md and Docs/NarrativeRevolutionContentHandoff.md before merge.
"""
from __future__ import annotations
from collections import defaultdict
from datetime import date
import hashlib
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile

SOURCE_DIR = Path("策划文件/文案6点/文案/a-革命联合（共通+支线）")
SHEETS = ("Event", "EventDate", "SetupParams", "PresentationIndex", "ConversationPage",
          "ConversationLine", "ReadingPage", "ReadingLine", "Choice", "Condition",
          "ConditionKey", "Result", "AbilityCheck", "AbilityCheckCondition", "Ending", "Object", "Text")
SOURCE_PINS = {'MA70.10.07-S2.docx': (47, '58d3f86a242b1f4ac06fd96ceb945aca55aec143367d7c5401bfb4a4326647ce'), 'MA70.10.13-S2.docx': (68, '1aaed066f502ab3315945494309fce7fc73bdd25944910d287cc39d92fb91c14'), 'MA70.10.14-S1.docx': (219, '4f6ddc41a7e32f5089f711ab30169f20deb5416ac1c2da8793d4f8d1b54a5f69'), 'MA70.10.28-S1.docx': (159, '587894804cdfaacc313f1ab7bd6e00b8c104df69b42cc8a721be908d9f9249f8'), 'MA70.10.29-S1.docx': (628, 'b5c392fba0c0cd764818f6abc7ee7dd858e5c5313704b0ce11b23342904df5d5'), 'MA70.11.07-S1.docx': (345, '93b1bdaff292e050981f842b62f3ecf57a9af51ad4ce80b76a29ec89d9c150f8'), 'RE70.11.11-S2.docx': (20, '2d8e7ecb1e8131bcb37daba2518a59fdbf93c1828434b332cc3baf3b0ddaa3b8'), 'RE70.11.12-S1.docx': (210, '0fa4bb54c1d8aaf948ae86edbfd92adc41b8d1c4f445792030efdef7119c5226'), 'RE70.11.15-S1.docx': (340, '6049f0f3fa4c08930da6057a996227bffd7722ba69cf3cb31fca20e6ba6d7fb7'), 'RE70.11.22-S1.docx': (372, '62281660be10fad759548a40475949de3984e313811dd8add66292ebb3e99919'), 'RE70.11.23-S1.docx': (301, '892ab2d5c813bb03df039c7eefb11de26429119121fb26550ae706ac0b84a1ac'), 'RE70.11.24-S1（结局）.docx': (196, '8c3989cb4a67148140d2a6bd03f0e0344959d8c4cdbf6d81501aff4fc85822da')}
KNOWN = ("known.sorel", "known.eugene", "known.lesou", "known.mary")
COOP = "story.revolution.1029.s1.cooperated"
REFUSED = "story.revolution.1029.s1.refused"
ROSE_YES = "story.rose.1024.allegiance"
ROSE_NO = "story.rose.1024.safe_refusal"
RELEASE = {"persuasion": "church.release.persuasion",
           "bribery": "church.release.bribery",
           "rescue": "church.release.rescue"}
TERMINAL = "story.revolution.terminal"
POLITICS = "specialization.player.letter.politics"
CLEARED = "story.church.1020.s1.cleared"
ROOTS = {
    "10.13": ("revolution.1013.s2", "night", "Immediate", ("neutral.1013.s2", "oct07.s2")),
    "10.14": ("revolution.1014.s1", "morning", "MorningBeforeMap", ("rose.1011.s1", "oct07.s2")),
    "10.28": ("revolution.1028.s1", "night", "BeforeNight", ("oct07.s2",)),
    "10.29": ("revolution.1029.s1", "night", "BeforeNight", ("revolution.1028.s1", "church.1020.s1")),
    "11.07": ("revolution.1107.s1", "night", "BeforeNight", ("revolution.1029.s1",)),
    "11.11": ("revolution.route.1111.s2", "night", "Immediate", ("church.1111.s1", "revolution.1107.s1")),
    "11.12": ("revolution.route.1112.s1", "noon", "AfterBusiness", ("revolution.route.1111.s2",)),
    "11.15": ("revolution.route.1115.s1", "noon", "AfterBusiness", ("revolution.route.1112.s1",)),
    "11.22": ("revolution.route.1122.s1", "noon", "AfterBusiness", ("revolution.route.1115.s1",)),
    "11.23": ("revolution.route.1123.s1", "night", "BeforeNight", ("revolution.route.1122.s1",)),
    "11.24": ("revolution.route.1124.s1", "morning", "MorningBeforeMap", ("revolution.route.1123.s1",)),
}

def paragraphs(path: Path) -> list[str]:
    """Every nonempty body paragraph (including tables), in source order."""
    with zipfile.ZipFile(path) as doc:
        body = ET.fromstring(doc.read("word/document.xml"))
    ns = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
    return [s for p in body.findall(".//w:body//w:p", ns)
            if (s := "".join(t.text or "" for t in p.findall(".//w:t", ns)).strip())]

def digest(ps):
    return hashlib.sha256("\n".join(ps).encode("utf-8")).hexdigest()

def _and(*parts):
    return "|".join(p for p in parts if p)

class _Root:
    def __init__(self, compiler, source, ps, mmdd):
        self.c, self.source, self.ps, self.mmdd = compiler, source, ps, mmdd
        self.root, self.slot, self.entry, self.deps = ROOTS[mmdd]
        self.presentation = self.root + ".conversation"
        self.guards = defaultdict(lambda: [""])
        self.choices, self.checks, self.readings, self.ends = {}, {}, {}, {}
        self.option_paras, self.read_paras = set(), set()
        self.effects, self.blockers, self.notes = [], [], []
        self.branch_issues, self.decisions = [], []
        self.coverage = {i: {"paragraph": i, "text": s, "role": "", "outputs": []}
                         for i, s in enumerate(ps, 1)}
        self.seq = 0
        self.art_scopes = []

    def guard(self, lo, hi, *alternatives):
        for i in range(lo, hi + 1):
            self.guards[i] = [_and(a, b) for a in self.guards[i] for b in alternatives]

    def choice(self, at, key, options, branches=(), requirements=None):
        group = self.root + "." + key
        self.choices[at] = (group, options, requirements or {})
        self.option_paras.update(options)
        for option, lo, hi in branches:
            self.guard(lo, hi, "choice:" + group + "." + str(option))
        return group

    def check(self, at, key, attributes, success, failure=None, outer="", politics=False):
        cid = self.root + ".check." + key
        self.checks[at] = (cid, attributes, outer, success[0])
        passing = ["check:" + cid]
        if politics:
            passing.append(_and("not:check:" + cid, POLITICS))
        self.guard(*success, *passing)
        if failure:
            self.guard(*failure, _and("not:check:" + cid, "not:" + POLITICS if politics else ""))
        return passing

    def effect(self, typ, target, value="true", guards=None, anchor=None, owner=None):
        self.effects.append((typ, target, str(value), guards or [""], anchor, owner))

    def flag(self, target, guards=None, anchor=None):
        self.c.flag(target)
        self.effect("SET_FLAG", target, guards=guards, anchor=anchor)

    def branch_issue(self, code, paragraphs, reason, gate_pages=False, scope="optional-effect"):
        """Fail only the unresolved source node, never an otherwise valid root."""
        gate = "integration." + self.root + "." + code.lower() + ".ready"
        self.c.flag(gate)
        issue = {"blockedBy": code, "root": self.root, "scope": scope,
                 "paragraphs": list(paragraphs), "reason": reason,
                 "guard": gate, "blocksRoot": False, "status": "fail-closed"}
        self.branch_issues.append(issue)
        for i in paragraphs:
            self.coverage[i].setdefault("blockedBy", []).append(code)
            if gate_pages:
                self.guard(i, i, gate)
        return gate

    def reading(self, at, key, content, reveal=False):
        self.readings[at] = (key, tuple(content), reveal)
        self.read_paras.update(content)

    def page(self, suffix, guard="", step="Dialogue", **kw):
        self.seq += 1
        pid = self.root + ".page." + suffix
        self.c.add("ConversationPage", pageId=pid, presentationKey=self.presentation,
                   seq=self.seq, pageCondKeys=guard or None, stepType=step, **kw)
        return pid

    def mark(self, i, role, *refs):
        a = self.coverage[i]
        if a["role"] and a["role"] != role:
            raise ValueError(f"Conflicting source roles: {self.root} P{i}: {a['role']}/{role}")
        a["role"] = role
        a["outputs"].extend(r for r in refs if r)
        a["guards"] = self.guards[i]

    def emit(self):
        c, root = self.c, self.root
        done = "story." + root + ".completed"
        c.flag(done)
        day = (date(670, int(self.mmdd[:2]), int(self.mmdd[3:])) - date(670, 10, 6)).days + 1
        properties = f"entry={self.entry};blocksSave=true"
        if self.blockers:
            properties += ";blockedBy=" + ",".join(self.blockers)
            gate = "integration." + root + ".ready"
            c.flag(gate)
            c.condition("event", root, "BOOL_EQ", gate, "true", suffix="construction")
        c.add("Event", eventId=root, eventKind="world", actorId="world", setupKey=root,
              onceOnly=True, logTag=root, presentationClass="Conversation",
              presentationKey=self.presentation, presentationLogTag=root, eventProperty=properties)
        c.add("SetupParams", setupKey=root, tplName="Tpl_ArrivalConversation", slotValues="opts=", skipAble=False, autoAdvance=False)
        c.add("PresentationIndex", presentationKey=self.presentation, presentationClass="Conversation", logTag=root)
        if self.entry != "Immediate":
            c.add("EventDate", eventId=root, day=day, timeSlot=self.slot, repeatRule="once", priority=20)
        c.condition("event", root, "BAR_BETWEEN", "world.calendar", f"{day},{day}", suffix="day")
        c.condition("event", root, "BOOL_EQ", done, "false", suffix="incomplete")
        for dep in self.deps:
            c.condition("event", root, "BOOL_EQ", "story." + dep + ".completed", "true", suffix="requires." + dep)
        c.condition("event", root, "BOOL_EQ", TERMINAL, "false", suffix="not_terminal")
        if ".route." in root:
            c.condition("event", root, "BOOL_EQ", "route.revolution", "true", suffix="route")
            for route in ("neutral", "rose", "church"):
                c.condition("event", root, "BOOL_EQ", "route." + route, "false", suffix="not_route." + route)
        if self.mmdd in ("11.07", "11.11"):
            c.condition("event", root, "BOOL_EQ", COOP, "true", suffix="cooperation")
        bg = "scene.cafe"
        for i, raw in enumerate(self.ps, 1):
            page_bg = next((key for start, stop, key in self.art_scopes if start <= i <= stop), bg)
            if i in self.checks:
                cid, attrs, outer, first = self.checks[i]
                c.add("AbilityCheck", checkId=cid, mode="Inline", comment=f"{self.source} P{i:03}; root-tail effects only")
                for seq, (attr, threshold, group) in enumerate(attrs, 1):
                    c.add("AbilityCheckCondition", conditionId=cid + f".{seq}", checkId=cid,
                          attributeId=attr, threshold=threshold, groupIndex=group, seq=seq)
                # Check step uses a real success line (existing validator requires it).
                # Keep an extra not-check + specialization copy for political OR checks.
                for v, guard in enumerate(self.guards[i], 1):
                    pid = self.page(f"check.{i:03}.{v}", _and(guard, outer), "PassiveCheck", checkId=cid, bgOverride=page_bg)
                    self.emit_line(first, pid, f".check.{v}")
                    self.mark(i, "check", pid)
                c.consumed_check_lines.add((root, first))
                continue
            if i in self.choices:
                group, options, requirements = self.choices[i]
                for v, guard in enumerate(self.guards[i], 1):
                    pid = self.page(f"choice.{i:03}.{v}", guard, optionGroupKey=group, bgOverride=page_bg)
                    self.mark(i, "choice_label", pid)
                for seq, p in enumerate(options, 1):
                    cid = group + "." + str(seq)
                    tk = c.text(root + f".text.p{p:03}", _choice_text(self.ps[p - 1]), f"{self.source} P{p:03} option")
                    c.add("Choice", choiceId=cid, groupKey=group, textKey=tk, resultOwnerId=cid + ".results", seq=seq)
                    self.mark(p, "choice", cid, tk)
                    for attr, threshold in requirements.get(seq, ()):
                        c.condition("choice", cid, "BAR_GTE", "player." + attr, str(threshold), suffix=attr)
                continue
            if i in self.option_paras or i in self.read_paras:
                continue
            if i in self.readings:
                key, content, reveal = self.readings[i]
                reading = root + ".reading." + key
                c.add("PresentationIndex", presentationKey=reading, presentationClass="Reading", logTag=root)
                rp = reading + ".page"
                c.add("ReadingPage", pageId=rp, presentationKey=reading, seq=1)
                for n, p in enumerate(content, 1):
                    tk = c.text(root + f".text.p{p:03}", self.ps[p - 1], f"{self.source} P{p:03} reading")
                    lid = reading + f".line.{n:03}"
                    c.add("ReadingLine", lineId=lid, pageId=rp, seq=n, mode="narration", textKey=tk)
                    self.mark(p, "reading", lid, tk)
                for v, guard in enumerate(self.guards[i], 1):
                    if reveal:
                        pid = self.page(f"reveal.{i:03}.{v}", guard, "LetterReveal", readingKey=reading, bgOverride=page_bg)
                        self.mark(i, "reading_label", pid)
                    pid = self.page(f"read.{i:03}.{v}", guard, "LetterRead", readingKey=reading, bgOverride=page_bg)
                    self.mark(i, "reading_label", pid)
                continue
            if i in self.ends:
                for v, guard in enumerate(self.guards[i], 1):
                    pid = self.page(f"end.{i:03}.{v}", guard, "End")
                    self.mark(i, "end", pid)
                continue
            if _directive(raw):
                self.mark(i, "directive")
                if raw.startswith("[背景") or "背景为" in raw:
                    bg = self.background(raw, i)
                continue
            for v, guard in enumerate(self.guards[i], 1):
                if (root, i) in c.consumed_check_lines and not guard.startswith("not:check:"):
                    continue
                pid = self.page(f"p{i:03}.{v}", guard, bgOverride=page_bg)
                self.emit_line(i, pid, f".{v}")
        if not self.ends or len(self.ps) not in self.ends:
            self.page("end", step="End")
        self.effect("SET_FLAG", done, anchor=len(self.ps))
        for seq, (typ, target, value, guards, anchor, owner) in enumerate(self.effects, 1):
            rid = root + f".result.{seq:03}"
            c.add("Result", resultId=rid, eventId=owner or root, seq=seq, resultType=typ,
                  target=target, value=value, note=f"Root-tail only; {self.source} P{anchor or 1:03}; cancellation discards")
            for group, expression in enumerate(guards, 1):
                for n, token in enumerate(expression.split("|"), 1):
                    if token:
                        c.condition("result", rid, "BOOL_EQ", token, "true", group=group, suffix=f"guard{group}.{n}")
        c.audit["sources"].append({"source": str(SOURCE_DIR / self.source), "sha256": c.source_hash[self.source],
            "paragraph_sha256": digest(self.ps), "nonempty_paragraphs": len(self.ps), "root": root,
            "status": "construction-blocked" if self.blockers else "compiled",
            "blockedBy": self.blockers, "branchIssues": self.branch_issues,
            "decisions": self.decisions, "notes": self.notes, "anchors": list(self.coverage.values())})

    def emit_line(self, p, page, suffix):
        raw = self.ps[p - 1]
        speaker, name, emote, text = _speech(raw, self.mmdd)
        if speaker and speaker not in self.c.characters and not speaker.startswith("revolution.actor."):
            raise ValueError("CharacterCatalog missing stable ID " + speaker)
        if speaker.startswith("revolution.actor."):
            self.c.generic(speaker, name)
        tk = self.c.text(self.root + f".text.p{p:03}", text, f"{self.source} P{p:03}")
        namekey = None
        if name:
            namekey = self.c.text(self.root + ".speaker." + hashlib.sha256(name.encode()).hexdigest()[:10], name, "Source display name, not Object identity")
        lid = self.root + f".line.p{p:03}" + suffix
        side = "left" if speaker == "player" else "right"
        # Preserve Duval's identity/name while keeping the authored behind-door voice offstage.
        if self.mmdd == "11.24" and speaker == "duval":
            side = "none"
        self.c.add("ConversationLine", lineId=lid, pageId=page, seq=1, speakerKey=speaker or None,
                   side=side, textKey=tk, emote=emote or None, speakerNameKey=namekey)
        self.mark(p, "dialogue", lid, tk)

    def background(self, raw, i):
        value = raw.strip("[]").split("：", 1)[-1]
        if "咖啡" in value:
            return "scene.cafe"
        if "圣西尔万" in value:
            return self.c.bg("suburb.day")
        if "圣徒街" in value or "圣诺特街道" in value:
            return self.c.bg("street.night" if "夜" in value else "street.day")
        if "旧屋外" in value:
            return self.c.bg("old.house.day")
        if "小巷" in value:
            return self.c.bg("street.night")
        if "黑屏" in value:
            return self.c.bg("church.execution.black")
        # Non-blocking neutral fallback, never inherit a cafe behind a remote scene.
        fallback = self.c.bg("church.execution.black")
        self.c.audit["artPending"].append({"root": self.root, "paragraph": i,
            "source": raw, "fallback": fallback, "blocksRoot": False})
        return fallback


def _directive(s):
    return (bool(re.match(r"^(?:MA|RE)70\.", s)) or s.startswith(("[", "【", "（", "触发条件", "选项", "分支收束", "检定成功", "检定失败", "密道示意图标注", "特别强调"))
            or bool(re.match(r"^[①②③④]", s)))


def _choice_text(raw):
    text = re.sub(r"^[①②③④]\s*", "", raw)
    return re.sub(r"[（(][^（）()]*?(?:检定|倾向|好感)[^（）()]*[）)]", "", text).strip()


NAMED = {"阿黛尔": "adele", "索雷尔": "sorel", "玛丽": "marie", "勒苏": "lesou", "麻雀": "sparrow", "杜瓦尔": "duval"}
GENERIC = {name: "revolution.actor." + key for name, key in {
    "青年工人": "young_worker", "工人": "worker", "另一名工人": "worker_other", "市民": "citizen",
    "街区代表": "district_delegate", "工坊代表": "workshop_delegate", "行动负责人": "action_leader",
    "演讲者": "rally_speaker", "工作人员": "rally_staff", "革命联合队员": "squad_member",
    "革命联合成员": "member"}.items()}


def _speech(raw, mmdd):
    m = re.match(r"^=?([^：:]{1,24})[：:]\s*(.*)$", raw)
    if not m:
        return "", "", "", raw
    name, text = m.groups()
    if name == "{playername}":
        speaker = "player"
    elif name in ("？？", "？？？"):
        speaker = {"10.14": "sorel", "10.29": "marie", "11.07": "lesou", "11.23": "revolution.actor.squad_member"}.get(mmdd, "")
    elif name.startswith("玛丽（"):
        speaker = "marie"
    else:
        speaker = NAMED.get(name, GENERIC.get(name, ""))
    if not speaker:
        return "", "", "", raw
    emotes = re.findall(r"（(?:[^（）]*：)?#([^（）]+)）", text)
    text = re.sub(r"（(?:[^（）]*：)?#[^（）]+）", "", text).strip()
    return speaker, name, emotes[0] if emotes else "", text


class _Compiler:
    def __init__(self, project):
        self.project = project
        self.rows = {s: [] for s in SHEETS}
        self.flags, self.texts, self.generics = set(), {}, set()
        self.source_hash, self.consumed_check_lines = {}, set()
        self.characters = set(re.findall(r"characterId: (\S+)", (project / "Assets/Resources/Characters/CharacterCatalog.asset").read_text("utf-8")))
        self.backgrounds = set(re.findall(r"  - key: (\S+)", (project / "Assets/Resources/Dialogue/NarrativeStageVisuals.asset").read_text("utf-8")))
        self.audit = {"compiler": "revolution.current.v2.branch-scoped", "sources": [], "artPending": [],
                      "unresolvedDependencies": [], "preservedRoots": ["oct07.s2"],
                      "runtimeAssumptions": ["End completes only after accepted root-tail", "Abort/skip cancellation discards all staged root/choice results", "Result BOOL_EQ reads root-local snapshot until settlement ends", "No current whole-root construction gates; unresolved optional nodes carry scoped blockedBy metadata and never auto-succeed", "GameFlowBootstrap.OnEndingSelected -> GameOver -> EnterGameOver stops DayFlow/runtime at accepted ending root-tail; no extra host gate", "Active presenter blocks gameplay until completion; cancellation is transactional, not an irreversible terminal entry"]}

    def add(self, sheet, **row):
        self.rows[sheet].append(row)

    def text(self, key, text, comment):
        if key in self.texts and self.texts[key] != text:
            raise ValueError("Text collision: " + key)
        if key not in self.texts:
            self.texts[key] = text
            self.add("Text", key=key, text=text, comment=comment)
        return key

    def flag(self, key):
        if key not in self.flags:
            self.flags.add(key)
            tk = self.text("condition." + key, key, "Declaration: false until truthful root-tail producer")
            self.add("ConditionKey", key=key, nameText=tk, initialValue=False, onlySetTrue=True)

    def condition(self, owner_type, owner, typ, target, value, group=1, suffix=""):
        self.add("Condition", conditionId=owner + ".condition." + suffix,
                 ownerType=owner_type, ownerId=owner, groupIndex=group, condType=typ, target=target, value=value)

    def generic(self, key, name):
        if key in self.generics:
            return
        self.generics.add(key)
        self.add("Object", objId=key, objType="npc", nameText=self.text("character." + key + ".name", name, "Source generic; no named-person substitution"), tags="revolution,generic,no_portrait")

    def bg(self, key):
        if key not in self.backgrounds:
            raise ValueError("Unregistered stage resource " + key)
        return key


def _plan(r):
    """Reviewed CURRENT nonempty paragraph spans; labels are never player text."""
    ch = r.mmdd
    C = lambda key, opt: "choice:" + r.root + "." + key + "." + str(opt)
    if ch == "10.13":
        r.choice(38, "c1", (39,40,41), ((1,43,45),(2,47,49),(3,51,53)))
    elif ch == "10.14":
        r.choice(9,"c1",(10,11))
        r.reading(16,"cipher",(17,),reveal=True)
        r.choice(54,"c2",(55,56,57),((3,66,75),),{3:(("courage",6),)})
        r.guard(59,64,"anyChoice:"+r.root+".c2.1;"+r.root+".c2.2")
        r.check(77,"wisdom4",(("wisdom",4,1),),(78,80))
        r.choice(91,"c4",(92,93,94,95),((1,97,101),(2,103,111),(3,113,123),(4,125,131)))
        r.check(115,"perception6",(("perception",6,1),),(116,116),outer=C("c4",3))
        r.choice(145,"c5",(146,147,148),((1,150,157),(2,159,164),(3,166,172)))
        r.check(174,"wisdom5",(("wisdom",5,1),),(175,190))
        r.check(191,"language3",(("language",3,1),),(192,201))
        # Same-root deltas are staged; test projected value, not the stale live bar.
        projected=[_and(C("c4",a),C("c5",b),f"bar:faction.revolution:ge:{2-(1 if a==3 else 0)-delta}")
                   for a in range(1,5) for b,delta in ((1,2),(2,1),(3,-1))]
        r.guard(191,201,*projected)
        for opt,target,anchor in ((1,"neutral",101),(2,"rose",111),(3,"revolution",123),(4,"church",131)):
            r.effect("BAR_ADD","faction."+target,1,[C("c4",opt)],anchor, r.root+f".c4.{opt}.results")
        for opt,delta,anchor in ((1,2,157),(2,1,164),(3,-1,172)):
            r.effect("BAR_ADD","faction.revolution",delta,[C("c5",opt)],anchor,r.root+f".c5.{opt}.results")
        r.flag("story.revolution.sorel.organization.known", r.guards[199], 199)
        r.flag("story.revolution.cafe.organization.known", r.guards[200], 200)
        r.flag("known.sorel",anchor=219)
        r.branch_issue("C14_REVOLUTION_TRUST_BAR_UNRESOLVED", (201,),
            "Only optional trust+1 effect lacks a canonical Bar ID. Language/faction dialogue and whole C14 remain live; no invented trust target.")
        r.notes += ["P191 is language>=3 AND projected faction.revolution>=2 in CURRENT 219-paragraph source; P217 is tea-cup narration, not a check. P201 trust-only effect pending, not a root gate."]
    elif ch == "10.28":
        r.guard(36,43,POLITICS)
        r.guard(96,101,"story.revolution.1013.s2.completed")
        r.choice(102,"c1",(103,104,105),((1,107,110),(2,112,120),(3,122,128)))
        for opt,anchor in ((1,110),(2,120),(3,128)):
            r.effect("BAR_ADD","adele.affection",1,[C("c1",opt)],anchor,r.root+f".c1.{opt}.results")
    elif ch == "10.29":
        r.art_scopes.append((13,20,"cg.marie_moonlit"))
        r.notes.append("User-marked CG07 at current P012: labelled local card only on P013-P020; P021 returns to the previous real stage. Not final artwork.")
        p,b,s=(RELEASE[x] for x in ("persuasion","bribery","rescue"))
        no_arrest=_and(CLEARED, "not:"+p, "not:"+b, "not:"+s)
        persuasion=_and(p, "not:"+CLEARED, "not:"+b, "not:"+s)
        bribery=_and(b, "not:"+CLEARED, "not:"+p, "not:"+s)
        rescue=_and(s, "not:"+CLEARED, "not:"+p, "not:"+b)
        # P064 truthfully says "not taken away"; the P050 source label conflicts.
        # Reuse its current intact dialogue for actual cleared players, never a fake release.
        r.guard(51,81,no_arrest,persuasion,bribery)
        r.guard(64,64,no_arrest)
        r.guard(67,68,ROSE_YES)
        r.guard(83,154,persuasion,bribery)
        r.guard(87,112,p)
        r.guard(115,153,b)
        r.guard(156,198,rescue)
        # One existing sentence becomes the common transition into the Rose topic.
        r.guard(199,199,no_arrest,persuasion,bribery,rescue)
        r.decisions.append({"kind":"source-label-scope-resolution", "paragraphs":[50,51,64,69,82,84,155,199,200],
            "detail":"P050/P069 say persuasion/bribery only, but P064 explicitly says not arrested. P051-081 are reused for actual 1020 cleared plus non-rescue releases; P064 cleared only; P083-154 detained only. Existing P199 transitions every origin to common Rose discussion. No new prose, no fabricated release result."})
        for lo,hi in ((209,245),(519,533)):
            r.guard(lo,hi,ROSE_YES)
        for lo,hi in ((247,266),(535,554)):
            r.guard(lo,hi,ROSE_NO)
        r.choice(223,"c2a",(224,225,226),((1,228,229),(2,231,236),(3,238,242)))
        r.guard(223,226,ROSE_YES)
        r.guard(268,284, _and(ROSE_YES,"story.rose.1026.s2.completed"))
        r.decisions.append({"kind":"factual-recap-scope", "paragraphs":list(range(268,285)),
            "detail":"10/26 Rose invitation addresses accepted cooperators only. Require actual rose.1026.s2 completion only in the allegiance DNF arm; safe-refusal players skip only this historical recap, not the existing common mind-reading explanation P285 onward."})
        r.reading(385,"passage_diagram",range(386,390))
        r.choice(575,"c3",(576,577),((1,579,599),(2,601,625)))
        r.flag(COOP,[C("c3",1)],592);r.flag(REFUSED,[C("c3",2)],622)
        r.flag("known.mary",anchor=628)
        r.effect("BAR_ADD","faction.rose",1,[_and(ROSE_YES,C("c2a",2))],225,r.root+".c2a.2.results")
        for opt,delta,anchor in ((1,2,593),(2,-2,624)):
            r.effect("BAR_ADD","faction.revolution",delta,[C("c3",opt)],anchor,r.root+f".c3.{opt}.results")
        r.notes += ["Four exclusive origins: actual 1020 cleared, or actual 1023 persuasion/bribery/rescue. Source-label conflict is resolved by bounded reuse, not whole-root blockage. Common parent completion does not stand in for a release fact."]
    elif ch == "11.07":
        r.notes += ["Pending secret-room/tunnel art uses registered neutral-black fallback; art is not a trigger gate."]
        for at,key,attr,succ,fail,pol in ((103,"letters","perception",(105,111),(113,114),False),(152,"timing","wisdom",(154,160),(162,163),True),(190,"route","perception",(192,196),(198,200),False),(217,"alliance","wisdom",(219,226),(228,229),True)):
            passed=r.check(at,key,((attr,5,1),),succ,fail,politics=pol)
            r.effect("BAR_ADD","lesou.affection",1,passed,succ[1])
        r.choice(116,"c1",(117,118,119),((1,121,123),(2,125,126),(3,128,130)))
        r.guard(232,235,ROSE_YES);r.guard(238,243,ROSE_NO)
        r.choice(248,"c2",(249,250,251,252),((1,254,258),(2,260,263),(3,265,280),(4,282,286)))
        passed=r.check(265,"challenge",(("courage",5,1),),(267,276),(278,279),outer=C("c2",3))
        r.effect("BAR_ADD","lesou.affection",1,[_and(C("c2",3),p) for p in passed],276)
        for group,anchor in (("c1",123),("c2",258)):
            r.effect("BAR_ADD","faction.revolution",1,[C(group,1)],anchor,r.root+f".{group}.1.results")
        r.flag("known.lesou",anchor=345)
        r.flag("story.revolution.revolution_date.known",anchor=341)
        r.flag("story.revolution.rose.plan.known",anchor=342)
        r.flag("story.revolution.cafe.transfer_plan.known",anchor=343)
    elif ch == "11.11":
        r.reading(9,"protection_request",range(10,14))
        r.flag("story.revolution.protection_request.sent",anchor=19)
        r.notes += ["Queued only by central church.1111.s1 selecting revolution; no route flag set here."]
    elif ch == "11.12":
        r.choice(30,"c1",(31,32),((1,34,35),(2,37,38)))
        r.choice(75,"c2",(76,77,78),((1,80,84),(2,86,90),(3,92,99)))
        for at,key,attr,n,succ,fail,fact in ((127,"wisdom12","wisdom",12,(129,146),(148,150),"story.revolution.mary.contradiction.known"),(152,"language10","language",10,(154,171),(173,175),"story.revolution.mary.unwilling.known")):
            passed=r.check(at,key,((attr,n,1),),succ,fail)
            r.flag(fact,passed,succ[1])
        r.flag("story.revolution.mary.pistol.received",anchor=208)
        r.flag("story.revolution.private_invitation.received",anchor=209)
        r.notes += ["Pistol receipt is source narrative history, not an inventory/gameplay reward. Existing story presents handover and later scripted gunshot; no unknown ITEM_ADD or weapon subsystem is required to trigger this root."]
    elif ch == "11.15":
        r.choice(21,"c1",(22,23,24),((1,26,27),(2,29,32),(3,34,36)))
        passed=r.check(133,"structure9",(("wisdom",9,1),),(135,147),politics=True)
        r.flag("story.revolution.alliance_structure.known",passed,140)
        r.flag("story.revolution.lesou.coordinator.known",passed,147)
        r.choice(182,"c2",(183,184,185),((1,187,190),(2,192,195),(3,197,200)))
        r.choice(226,"c3",(227,228,229),((1,231,233),(2,235,237),(3,239,241)))
        f1,f2="story.revolution.mary.contradiction.known","story.revolution.mary.unwilling.known"
        r.guard(283,286,f1,_and("not:"+f1,f2))
        r.choice(302,"c4",(303,304),((1,306,308),(2,310,314)))
        r.flag("story.revolution.badge.received",anchor=339)
        r.notes += ["Badge is source narrative receipt/identity fact, not a second inventory. Meeting/landing art pending is non-blocking; no physical-item action is fabricated."]
    elif ch == "11.22":
        r.reading(17,"adele_note",(18,),reveal=True)
        r.choice(102,"c2",(103,104),((1,106,109),(2,111,114)))
        r.choice(208,"c3",(209,210,211),((1,213,222),(2,224,230)))
        r.choice(293,"c4",(294,295),((1,297,303),(2,305,309)))
        r.branch_issue("RE1122_LANGUAGE_THRESHOLD_UNSPECIFIED", (232,233,234),
            "P232 has no numeric threshold. Only the optional inference P233-234 is fail-closed; the new question P235 starts the unaffected common conversation.", scope="optional-dialogue")
        gate=r.branch_issues[-1]["guard"]
        r.guard(233,234,gate)
        r.decisions += [
            {"kind":"empty-source-response", "paragraphs":[208,211,231],
             "detail":"Third option has no distinct response in source; it joins the existing shared continuation without invented reply or reward."},
            {"kind":"source-label-semantic-mapping", "paragraphs":[293,295,304,305,309],
             "detail":"There are only two offered options. P304 says option3 but P305-309 answers P295 (stay in cafe tomorrow), not P294 (political thought). Map this existing reply to c4.2; preserve the raw label in audit, no source edit."}]
        r.notes += ["Only optional language inference is disabled; no made-up threshold and no successful language check/clue. The P235-260 family/previous-manager conversation remains shared source dialogue."]
    elif ch == "11.23":
        r.art_scopes.append((229,231,"cg.rainy_triad"))
        r.notes.append("User-marked CG09 at P228: labelled local card P229-P231; subsequent crowd returns to previous stage. CG10/P293 fallback still awaits art choice, no invented corpse effect.")
        r.check(271,"mary_final",(("perception",10,1),("wisdom",10,2),("language",10,3)),(272,278))
        r.choice(282,"shoot",(283,284,285))
        r.flag("story.revolution.mary.assassination.stopped",anchor=301)
        r.decisions.append({"kind":"source-continuity-note", "paragraphs":[193,288],
            "detail":"Source gives Adele the pistol then has player fire it. Preserve exact text without invented transfer gameplay/inventory action; no mutually exclusive outcome depends on an unowned item state."})
        r.notes += ["All THREE source options are literally 开枪; do not invent a save-Mary or assassination-success path.","P193 gives Adele the pistol, yet P288 player fires the same pistol: resolve source/host ownership."]
    elif ch == "11.24":
        r.choice(11,"c0",(12,13),((1,15,25),(2,27,196)),{1:(("courage",18),)})
        r.ends[25]=True;r.ends[196]=True
        r.guard(26,26,C("c0",2))
        r.choice(55,"c1",(56,57,58),((1,60,60),(2,62,62),(3,64,64)))
        r.choice(159,"c2",(160,161,162),((1,164,164),(2,166,166),(3,168,168)))
        r.flag(TERMINAL,anchor=2)
        for opt,title,anchor in ((1,"历史会判我无罪",25),(2,"历史尚未结束",196)):
            eid="revolution.end0"+str(opt);fact="story."+eid+".selected"
            r.flag(fact,[C("c0",opt)],anchor)
            r.c.add("Ending",endingId=eid,enabled=True,priority=100+opt,
                    titleText=r.c.text(eid+".title",title,f"{r.source} P{anchor:03}"),
                    messageText=r.c.text(eid+".message",title,f"{r.source} P{anchor:03}"))
            r.c.condition("ending",eid,"BOOL_EQ",fact,"true",suffix="selected")
            r.c.condition("ending",eid,"BOOL_EQ",TERMINAL,"true",suffix="terminal")
        r.notes += ["Existing GameFlowBootstrap.OnEndingSelected -> GameOver/EnterGameOver stops DayFlow, stops NCS and shows ResultPanel after accepted ending root-tail. No invented terminal host dependency.","One multi-day terminal presentation has no Gameplay/ADVANCE_DAY/QUEUE_EVENT escape back into business. Active narrative owns interaction; cancellation discards results (not an irreversible ending-entry claim). Pending art is non-blocking."]


def _fix_event_dnf(c):
    """Unknown/contradictory origins fail closed; every DNF arm keeps parent guards."""
    for root in ("revolution.1029.s1", "revolution.1107.s1"):
        shared=[r for r in c.rows["Condition"] if r["ownerType"]=="event" and r["ownerId"]==root]
        c.rows["Condition"]=[r for r in c.rows["Condition"] if r not in shared]
        origins=[[]]
        if root=="revolution.1029.s1":
            keys=[CLEARED, *RELEASE.values()]
            origins=[]
            for selected in keys:
                origin=[(k,"true" if k==selected else "false") for k in keys]
                if selected!=CLEARED:
                    origin.append(("story.church.1023.s1.completed","true"))
                origins.append(origin)
        group=0
        for origin in origins:
            for selected in (ROSE_YES,ROSE_NO):
                group+=1
                for row in shared:
                    c.add("Condition",**{**row,"conditionId":row["conditionId"]+f".group{group}","groupIndex":group})
                outcomes=origin+[(k,"true" if k==selected else "false") for k in (ROSE_YES,ROSE_NO)]
                if root=="revolution.1029.s1" and selected==ROSE_YES:
                    outcomes.append(("story.rose.1026.s2.completed","true"))
                for n,(key,value) in enumerate(outcomes,1):
                    c.condition("event",root,"BOOL_EQ",key,value,group=group,suffix=f"outcome.{group}.{n}")


def _configured_text_diff(c):
    """Read-only XLSX audit. Never a compiler source or an openpyxl dependency."""
    path=c.project/"Assets/ConfigTables/Data/Shared/Text.xlsx"
    if not path.exists():
        return {"status":"unavailable", "reason":"Text.xlsx absent", "roots":[]}
    ns={"s":"http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
    relns="{http://schemas.openxmlformats.org/package/2006/relationships}"
    with zipfile.ZipFile(path) as z:
        shared=[]
        if "xl/sharedStrings.xml" in z.namelist():
            shared=["".join(t.text or "" for t in si.findall(".//s:t",ns))
                    for si in ET.fromstring(z.read("xl/sharedStrings.xml")).findall("s:si",ns)]
        book=ET.fromstring(z.read("xl/workbook.xml"))
        sheet=next(s for s in book.findall("s:sheets/s:sheet",ns) if s.attrib["name"]=="Text")
        rid=sheet.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
        rels=ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
        target=next(r.attrib["Target"] for r in rels.findall(relns+"Relationship") if r.attrib["Id"]==rid)
        member=target.lstrip("/") if target.startswith("/") else "xl/"+target
        rows=ET.fromstring(z.read(member)).findall("s:sheetData/s:row",ns)
        def values(row):
            out={}
            for cell in row.findall("s:c",ns):
                column=re.match(r"[A-Z]+",cell.attrib["r"])[0]
                if cell.attrib.get("t")=="inlineStr":
                    value="".join(t.text or "" for t in cell.findall(".//s:t",ns))
                else:
                    value=cell.findtext("s:v",default="",namespaces=ns)
                    if cell.attrib.get("t")=="s":value=shared[int(value)]
                out[column]=value
            return out
        header=values(rows[0]);keycol=next(k for k,v in header.items() if v=="key");textcol=next(k for k,v in header.items() if v=="text")
        old={}
        for row in rows[4:]:
            v=values(row)
            if v.get(keycol):old[v[keycol]]=v.get(textcol,"")
    report=[]
    for root in ("revolution.1013.s2","revolution.1014.s1","revolution.1028.s1"):
        source_groups=defaultdict(list)
        pattern=re.compile(r"^ncs\."+re.escape(root)+r"\.p(\d+)([a-z]?)$")
        for key,text in old.items():
            if m:=pattern.match(key):source_groups[int(m[1])].append((m[2],text))
        previous={p:"".join(text for suffix,text in sorted(parts)) for p,parts in source_groups.items()}
        current={int(m[1]):text for key,text in c.texts.items()
                 if (m:=re.match(r"^"+re.escape(root)+r"\.text\.p(\d+)$",key))}
        changed=[{"paragraph":p,"previous":previous[p],"current":current[p]}
                 for p in sorted(previous.keys()&current.keys()) if previous[p]!=current[p]]
        report.append({"root":root,"configured_anchors":len(previous),"current_text_anchors":len(current),
                       "changed":changed,"added":[p for p in sorted(current.keys()-previous.keys())],
                       "removed":[p for p in sorted(previous.keys()-current.keys())]})
    return {"status":"compared", "workbook_sha256":hashlib.sha256(path.read_bytes()).hexdigest(),
            "note":"Ordinal text comparison, split old p190a/b recombined. DOCX remains sole source. Controls remain audit anchors, not player text.", "roots":report}


def build(project_root: Path) -> dict:
    """Caller alone merges source workbooks and runs official Luban."""
    project=Path(project_root).resolve();c=_Compiler(project)
    for key in (*KNOWN,TERMINAL):
        c.flag(key)
    files=sorted((project/SOURCE_DIR).glob("*.docx"))
    if {p.name for p in files} != set(SOURCE_PINS):
        raise ValueError("Current revolution DOCX set differs from reviewed 12-source manifest")
    for path in files:
        ps=paragraphs(path);count,sha=SOURCE_PINS[path.name]
        if len(ps)!=count or digest(ps)!=sha:
            raise ValueError(f"Current source changed: {path.name}; re-review ordinal plan before merge")
        c.source_hash[path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
        if "10.07" in path.name:
            c.audit["sources"].append({"source":str(SOURCE_DIR/path.name),"sha256":c.source_hash[path.name],
                 "paragraph_sha256":digest(ps),"nonempty_paragraphs":len(ps),"root":"oct07.s2","status":"preserved-existing",
                 "blockedBy":[],"anchors":[{"paragraph":i,"text":s,"role":"preserved-existing","outputs":["oct07.s2"]} for i,s in enumerate(ps,1)]})
            continue
        mmdd=re.search(r"70\.(\d{2}\.\d{2})",path.name)[1]
        r=_Root(c,path.name,ps,mmdd);_plan(r);r.emit()
        c.audit["unresolvedDependencies"].extend({"root":r.root,"blockedBy":b,"scope":"root","blocksRoot":True} for b in r.blockers)
        c.audit["unresolvedDependencies"].extend(r.branch_issues)
    _fix_event_dnf(c)
    c.audit["configuredTextDiff"]=_configured_text_diff(c)
    c.audit["supportedEvents"]=[s["root"] for s in c.audit["sources"] if s["status"] in ("compiled","preserved-existing")]
    c.audit["genericActors"]=[{**o,"portraitStatus":"intentionally empty; no named-person impersonation"} for o in c.rows["Object"]]
    c.audit["externalFacts"]=[CLEARED,*RELEASE.values(),ROSE_YES,ROSE_NO,"route.revolution",POLITICS]
    c.audit["externalQueueEdges"]=[{"from":"neutral.1013.s2","to":"revolution.1013.s2","when":"accepted root completion only"},{"from":"church.1111.s1","to":"revolution.route.1111.s2","when":"actual central revolution choice only"}]
    c.audit["knownProducers"]={"known.sorel":"revolution.1014.s1","known.mary":"revolution.1029.s1","known.lesou":"revolution.1107.s1","known.eugene":None}
    c.audit["summary"]={"source_files":len(files),"nonempty_paragraphs":sum(s["nonempty_paragraphs"] for s in c.audit["sources"]),
                          "roots_compiled":len(c.rows["Event"]),"roots_preserved":1,
                          "blocked_roots":sum(bool(s["blockedBy"]) for s in c.audit["sources"]),
                          "pending_optional_nodes":sum(len(s.get("branchIssues",[])) for s in c.audit["sources"])}
    return {**c.rows,"_audit":c.audit,"_replace_roots":[v[0] for v in ROOTS.values()]}
