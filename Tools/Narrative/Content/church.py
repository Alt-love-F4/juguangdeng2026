"""Compile CURRENT church/common-hub DOCX into existing NCS rows; no file writes.
See INTEGRATION_CONTRACT.md. Construction-blocked graphs retain source mappings.
"""
from __future__ import annotations
from datetime import date
from hashlib import sha256
from pathlib import Path
import re
from xml.etree import ElementTree as ET
from zipfile import ZipFile

SOURCE_DIR = Path("策划文件/文案6点/文案/a-王庭教会（共通+支线）")
W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
ROUTES = ("neutral", "rose", "church", "revolution")
EXCELLENT_SCORE_PRESET = 90  # Shared host preset, not a source-author numeric grade.
CHURCH_BRIBE_AMOUNT = 3000  # Source P046 final demand; P056's pending amount resolves to this explicit demand.
SHEETS = ("Object", "Event", "EventDate", "SetupParams", "PresentationIndex", "ConditionKey", "Condition", "Result", "ConversationPage", "ConversationLine", "Choice", "AbilityCheck", "AbilityCheckCondition", "ReadingPage", "ReadingLine", "Ending", "Text")
SOURCE_HASHES = {
    "MA70.10.09-S2.docx": "15c3101b85329f082efa98d9c938791089d9ac5e5cb35c50d8788fa4a3d9c58d",
    "MA70.10.14-S2.docx": "31215dd3c7c612e1aec62f1a192c724eafffb009c42142d8dad5607541be6907",
    "MA70.10.20-S1.docx": "ef542841f02f8e52a648f963ebd04669f3c5c925ecb4964097161a74b5547042",
    "MA70.10.23-S1.docx": "34676606041bea930580ec20511d19fb78a46c83fc825d9cb3127a5c03dbaf3d",
    "MA70.10.23-S2-End if3.docx": "da8691c9b44b8b5b3455c5276acc8aebd41fffb3d34503a45e0e050f182f6a91",
    "MA70.11.04-S1.docx": "a6bd2474abbec96491fbb9ee6bfd85e03464f8df73769bc613a6073b8fb2c177",
    "MA70.11.05-S1.docx": "9adf4277422a337e24752714b1ead52192d4f364ead8a94ce380cfd14dba3a9d",
    "MA70.11.05-S2.docx": "8cb1fa3b42a79db4583419bc3851fe891ef104eacd6f73a151be12b73df42790",
    "MA70.11.11-S1（内含end if4-再见，亚拿尼亚）.docx": "bc842b005503683f760bce5b58028088b336edeb080c128e59170bfc8598970c",
    "RY70.11.11-S1.docx": "f3e1ae56f55471b7e4e2f4ba26dc5bfbe79b683082f6f34ce86ec772e8d67a3b",
    "RY70.11.12-S1.docx": "c77bfd4d4a494e19b6624ce08cf976f122bbaff7384ee83c28e253344ac363c0",
    "RY70.11.13-S1.docx": "e5b7eb86b5681eaf81559b3334a381ee0f924ba1037c391493568498a0b654fd",
    "RY70.11.15-S1.docx": "e85f90d1b6b2b4b65b0a31b2c2c325d836fda9dbef8ef28339152e5f45d2ee91",
    "RY70.11.16-S1.docx": "d4df86d2b714985110e13b7bbdcf903527fe6bbf5fb54fb68bdd092606727a91",
    "RY70.11.18-S1.docx": "0006e097bbac49b28630f38d988af4ca76dade04f5a410fc5d99387a0fd4686d",
    "RY70.11.23-S1【结局1&2】.docx": "c9af366a682b13fabd3b4f58ba6c4f643d70b290037772a37c285b9b4b4c89a2",
}  # Exact current sources; positional change in 11/4 is normalized below.

def paragraphs(path: Path) -> list[str]:
    with ZipFile(path) as z:
        body = ET.fromstring(z.read("word/document.xml")).find(W + "body")
    return [text for p in body.iter(W + "p") if (text := "".join((n.text or "") if n.tag == W + "t" else "\n" if n.tag in (W + "br", W + "cr") else "\t" if n.tag == W + "tab" else "" for n in p.iter()).strip())]

def numeric_guard(attribute, operator, threshold):
    return f"bar:{attribute}:{operator}:{threshold}"

def guard(*parts):
    return "|".join(p for p in parts if p)

def choice(cid):
    return "choice:" + cid

def any_choices(*ids):
    return "anyChoice:" + ";".join(ids)

def directive(text):
    if re.match(r"^(?:MA|RY)\s*70[.\s]|^MA10[.]", text):
        return True
    if re.match(r"^(?:选项|选择)\s*(?:MA|RY|$)", text):
        return True
    if text in {"第一幕", "第二幕", "End", "End if2", "end", "要逆转未来吗？"}:
        return True
    if re.match(r"^(?:触发条件|再次返回选项|①接受→|②拒绝→)", text):
        return True
    if text.startswith(("[", "【", "（", "(黑幕")):
        return "\n" not in text or not re.search(r"\n[^\[【（]+[：:]", text)
    return False

def visible_text(text, is_choice=False):
    text = re.sub(r"\[[^\]\n]*\]|【[^】\n]*】", "", text)
    text = re.sub(r"[（(][#＃][^）)]*[）)]", "", text)
    text = re.sub(r"[（(](?:语言|智慧|知觉|勇气|资金|触发RY)[^）)]*[）)]", "", text)
    text = re.sub(r"(?<=[：:])）[#＃](?:平静|思索|不屑|迷惑)", "", text)
    if is_choice:
        text = re.sub(r"^[①②③④⑤⑥]\s*", "", text)
        text = re.sub(r"\s*→.*$", "", text)
    return text.strip()

class _Compiler:
    def __init__(self, project):
        self.project = project
        self.rows = {s: [] for s in SHEETS}
        self.audit, self.replacements, self.keys = [], [], set()
    def add(self, sheet, **row):
        self.rows[sheet].append({k: v for k, v in row.items() if v is not None and v != ""})
        return self.rows[sheet][-1]
    def text(self, key, text, comment=""):
        if key not in self.keys:
            self.keys.add(key)
            self.add("Text", key=key, text=text, comment=comment)
        return key
    def fact(self, key, comment="root-tail fact; initial false", only_set_true=True):
        if any(r["key"] == key for r in self.rows["ConditionKey"]): return
        name = self.text("condition." + key, key, comment)
        self.add("ConditionKey", key=key, nameText=name, initialValue=False, onlySetTrue=only_set_true)
    def condition(self, owner_type, owner, target, value="true", kind="BOOL_EQ", group=1):
        self.add("Condition", conditionId=f"{owner}.cond.{len(self.rows['Condition'])+1}", ownerType=owner_type, ownerId=owner, groupIndex=group, condType=kind, target=target, value=value)
    def source(self, filename, root, *, slot="morning", prerequisites=(), blocked=(), route=False):
        return _Root(self, filename, root, slot, prerequisites, blocked, route)
    def finish(self):
        self.rows["_audit"], self.rows["_replace_roots"] = self.audit, self.replacements
        return self.rows

class _Root:
    def __init__(self, c, filename, root, slot, prerequisites, blocked, route):
        self.c, self.root, self.pk = c, root, root + ".conversation"
        self.path = c.project / SOURCE_DIR / filename
        raw_source = paragraphs(self.path)
        self.source = raw_source
        self.source_anchor_map = [(i,) for i in range(1, len(raw_source) + 1)]
        normalization = None
        if filename == "MA70.11.04-S1.docx":
            self.source = raw_source[:95] + [raw_source[95] + raw_source[96]] + raw_source[97:]
            self.source_anchor_map = [(i,) for i in range(1, 96)] + [(96, 97)] + [(i,) for i in range(98, len(raw_source) + 1)]
            normalization = dict(kind="paragraph-continuation", sourceParagraphs=[96, 97], canonicalParagraph=96,
                detail="The price token is split across two DOCX paragraphs; merge it to preserve the existing P096 Text key and following anchors.")
        digest = sha256(self.path.read_bytes()).hexdigest()
        if SOURCE_HASHES.get(filename) != digest:
            raise ValueError(f"Church source changed; re-audit anchors: {filename}; sha256={digest}")
        self.anchors = {i: dict(paragraph=i, sourceText=t, kind="directive" if directive(t) else "unmapped", emitted=[], status="mapped-directive" if directive(t) else "unmapped") for i,t in enumerate(raw_source, 1)}
        self.seq, self.result_seq, self.page_rows = 0, 0, []
        self.blocked = list(blocked)
        self.audit = dict(source=str(SOURCE_DIR / filename), sha256=digest, nonemptyParagraphs=len(raw_source), normalizedParagraphs=len(self.source), normalization=normalization, root=root, status="construction-blocked" if blocked else "compiled", blockedBy=self.blocked, prerequisites=list(prerequisites), dependencies=[], decisions=[], anchors=list(self.anchors.values()))
        c.audit.append(self.audit); c.replacements.append(root)
        entry = "Immediate" if root in {"church.route.1111.s1", "church.1023.s2"} else {"morning": "MorningBeforeMap", "noon": "AfterBusiness", "night": "BeforeNight"}[slot]
        prop = f"entry={entry};blocksSave=true" + (";blockedBy=" + ",".join(blocked) if blocked else "")
        c.add("Event", eventId=root, eventKind="world", actorId="world", setupKey=root, onceOnly=True, logTag=root, presentationClass="Conversation", presentationKey=self.pk, presentationLogTag=root, eventProperty=prop)
        c.add("SetupParams", setupKey=root, tplName="Tpl_ArrivalConversation", slotValues="opts=", skipAble=False, autoAdvance=False)
        c.add("PresentationIndex", presentationKey=self.pk, presentationClass="Conversation", logTag=root)
        self.complete_key = "story." + root + ".completed"
        c.fact(self.complete_key); c.condition("event", root, self.complete_key, "false")
        for pre in prerequisites: c.condition("event", root, pre)
        if blocked:
            ready = "construction." + root + ".ready"
            c.fact(ready, "Explicit integration barrier; never narrative completion", only_set_true=False)
            c.condition("event", root, ready)
        if route:
            for name in ROUTES: c.condition("event", root, "route." + name, "true" if name == "church" else "false")
        mmdd = re.search(r"(?:route\.)?(\d{4})\.s", root).group(1)
        day = (date(670,int(mmdd[:2]),int(mmdd[2:]))-date(670,10,6)).days+1
        if root not in {"church.route.1111.s1", "church.1023.s2"}:
            c.add("EventDate", eventId=root, day=day, timeSlot=slot, repeatRule="once", priority=0)
        if root != "church.1023.s2": c.condition("event", root, "world.calendar", f"{day},{day}", "BAR_BETWEEN")
        self.result("SET_FLAG", self.complete_key, "true", note="Root tail only; cancellation never settles")
    def depend(self, key, detail):
        self.audit["dependencies"].append(dict(id=key, detail=detail))
    def mark(self, n, kind, emitted=None, status="implemented"):
        source_anchors = self.source_anchor_map[n - 1]
        for index, source_n in enumerate(source_anchors):
            a = self.anchors[source_n]
            a["kind"] = kind if index == 0 else "continuation"
            a["status"] = status if index == 0 else f"merged-into-P{n:03}"
            if emitted and emitted not in a["emitted"]: a["emitted"].append(emitted)
    def page(self, suffix, when="", *, step="Dialogue", bg=None, gameplay=None, next_id=None, check=None):
        self.seq += 1
        p=self.c.add("ConversationPage", pageId=self.root+"."+suffix, presentationKey=self.pk, seq=self.seq, pageCondKeys=when, bgOverride=bg, stepType=step, gameplayKey=gameplay, nextPageId=next_id, checkId=check)
        self.page_rows.append(p)
        return p
    def line(self, n, when="", bg=None, suffix="", page=None):
        text=self.source[n-1]
        if directive(text): return None
        cleaned=visible_text(text)
        if not cleaned: return None
        p=page if page is not None else self.page(f"p{n:03}{suffix}",when,bg=bg)
        key=f"{self.root}.text.p{n:03}"
        speaker=_speaker(cleaned)
        if speaker is None and re.match(r"^(?:客人|顾客|男人|女人|摊主|看守|监察官|监察员|疑似领头的人|众监察员|平民[A-Z]?|老妇人|小孩|神父|工人|教民|？？？)[：:]",cleaned):
            label=re.split(r"[：:]",cleaned,maxsplit=1)[0].strip()
            speaker="church.generic."+sha256(label.encode()).hexdigest()[:10]
            if not any(o["objId"]==speaker for o in self.c.rows["Object"]):
                self.c.add("Object",objId=speaker,objType="npc",nameText=self.c.text(speaker+".name",label,"Generic source actor; portrait pending"))
                self.audit["decisions"].append(dict(kind="art-pending",actor=label,objId=speaker))
        self.c.text(key, speech_body(cleaned) if speaker else cleaned,
            f"{self.path.name} nonempty paragraph {n}; original in _audit" + (" [speaker-label-stripped]" if speaker else ""))
        lid=p["pageId"]+".line"
        self.c.add("ConversationLine",lineId=lid,pageId=p["pageId"],seq=1,speakerKey=speaker,side="left" if speaker=="player" else "right" if speaker else "none",textKey=key)
        self.mark(n,"line",lid)
        return p
    def span(self, start, stop, when="", bg=None, suffix=""):
        emitted=[]
        for n in range(start,stop+1):
            p=self.line(n,when,bg,suffix)
            if p: emitted.append(p); bg=None
        return emitted
    def menu(self,name,numbers,when="",bg=None):
        p=self.page("menu."+name,when,bg=bg); group=self.root+".choices."+name
        p["optionGroupKey"]=group; ids=[]
        for seq,n in enumerate(numbers,1):
            cid=f"{self.root}.choice.{name}.{seq}"; key=f"{self.root}.text.p{n:03}.option"
            self.c.text(key,visible_text(self.source[n-1],True),f"{self.path.name} nonempty paragraph {n}; option")
            self.c.add("Choice",choiceId=cid,groupKey=group,textKey=key,resultOwnerId=cid+".result",seq=seq)
            self.mark(n,"choice",cid); ids.append(cid)
        return ids
    def result(self,kind,target,value,when="",owner=None,note=""):
        self.result_seq+=1; rid=f"{self.root}.result.{self.result_seq:03}"
        self.c.add("Result",resultId=rid,eventId=owner or self.root,seq=self.result_seq,resultType=kind,target=target,value=str(value),note=note)
        for cond in when.split("|") if when else []: self.c.condition("result",rid,cond)
        return rid
    def flag(self,key,when="",note=""):
        self.c.fact(key); self.result("SET_FLAG",key,"true",when,note=note)
    def reward(self,cid,target,amount):
        kind, destination = ("MONEY_ADD", "cafe") if target == "cafe.money" else ("BAR_ADD", "bar:" + target)
        self.result(kind,destination,amount,choice(cid),owner=cid+".result",note="Source choice reward; root-tail settlement; external money uses host command")
    def end(self,suffix="end",when=""):
        return self.page(suffix,when,step="End")
    def gameplay(self,suffix,product,when="",threshold=0,anchors=(),taste=False):
        verb="taste" if taste else "serve"
        p=self.page(suffix,when,step="Gameplay",bg="scene.cafe",gameplay=f"{verb}:{product}:{threshold}:1")
        for n in anchors: self.mark(n,"gameplay",p["pageId"])
        self.depend(p["gameplayKey"],"Real FreeCafe serving bridge; confirmed product; no auto-success")
        return p
    def passive(self,suffix,check,success_anchor,when=""):
        p=self.page(suffix,when,step="PassiveCheck",check=check)
        self.line(success_anchor,page=p)
        return p
    def business_end(self,suffix,anchors):
        p=self.page(suffix,step="Gameplay",bg="scene.cafe",gameplay="host:end_business")
        for n in anchors: self.mark(n,"business-host-boundary",p["pageId"])
        self.depend("host:end_business","Central host calls actual EndDayFlowBusiness; already closed is valid; no calendar advance")
        return p
    def check(self,name,attr,threshold,groups=None):
        cid=self.root+".check."+name
        self.c.add("AbilityCheck",checkId=cid,mode="Inline",comment="CURRENT source threshold")
        for idx,(attribute,value,group) in enumerate(groups or [(attr,threshold,1)],1):
            self.c.add("AbilityCheckCondition",conditionId=cid+f".{idx}",checkId=cid,attributeId=attribute,threshold=value,groupIndex=group,seq=idx)
        return cid
    def ending(self,ending_id,title,when):
        terminal="terminal."+ending_id; self.flag(terminal,when,"Condition-gated root-tail terminal; never date-triggered")
        tk=self.c.text(ending_id+".title",title)
        self.c.add("Ending",endingId=ending_id,enabled=True,priority=20,titleText=tk,messageText=tk)
        for key in (terminal,self.complete_key): self.c.condition("ending",ending_id,key)
        if ".route." in self.root: self.c.condition("ending",ending_id,"route.church")
    def done(self):
        missing=[n for n,a in self.anchors.items() if a["kind"]=="unmapped"]
        if missing: raise AssertionError(f"Unmapped church source {self.root}: {missing}")
        if self.blocked:
            for a in self.anchors.values():
                if a["status"]=="implemented": a["status"]="compiled-in-blocked-root"
        for a in self.anchors.values():
            if a["kind"] == "directive" and a["status"] == "mapped-directive":
                t = a["sourceText"]
                if t.startswith("[音效"):
                    a["status"] = "audio-pending"
                elif t.startswith(("[美术", "[CG", "[演出")):
                    a["status"] = "presentation-directive-pending"
                else:
                    a["status"] = "control-or-source-label"
        self.audit["emittedPages"]=len(self.page_rows)
        return self

def speech_label(text):
    match = re.match(r"^([^：:；\n]{1,30})[：:；]", text)
    return match.group(1).strip() if match else None


def speech_body(text):
    return re.split(r"[：:；]", text, maxsplit=1)[1].lstrip() if speech_label(text) else text


def _speaker(text):
    # A name at the start of prose (e.g. '阿黛尔离开之后') is not a speaker.
    # Match an explicit source label followed by a dialogue delimiter instead.
    head = speech_label(text)
    if head in ("{playername}", "{playname}", "{playernanem}", "{playernmae}"):
        return "player"
    names = {"阿黛尔":"adele", "阿黛尔·米尼耶":"adele", "阿黛尔`米尼耶":"adele",
        "埃莉诺":"alienor", "阿莉耶诺尔":"alienor", "王女":"alienor",
        "埃利昂":"elion", "埃利昂公爵":"elion", "西尔万":"sylvain", "萨德":"sade",
        "萨德神父":"sade", "吕西安":"lucien", "勒费弗尔":"lefebvre",
        "军人A":"soldier_a", "军人C":"soldier_c", "客人C":"guest_c"}
    return names.get(head)


def _church1009(c):
    r=c.source("MA70.10.09-S2.docx","church.1009.s2",slot="night",prerequisites=("story.rose.1009.s1.completed",))
    r.span(4,27,bg="cathedral.day")
    a,b=r.menu("hat",[29,30])
    r.span(32,39,choice(a)); r.span(41,46,choice(b)); r.span(48,50)
    r.flag("story.church.1009.s2.hat_helped",choice(a),"Actual source pick branch")
    r.flag("known.alienor",any_choices(a,b),"P036-039 or P042-045 actually saw the girl")
    r.end()
    r.audit["decisions"].append(dict(kind="preserved",detail="Existing pick/ignore and common return; optional silhouette UI/art pending"))
    return r.done()

def _church1014(c):
    r=c.source("MA70.10.14-S2.docx","church.1014.s2",slot="noon",prerequisites=("story.neutral.1013.s2.completed","story.revolution.1014.s1.completed"))
    r.span(4,12,bg="scene.cafe"); r.gameplay("serve.thibaut","blackcoffee",anchors=(13,)); r.span(14,15)
    r.span(17,21,numeric_guard("player.perception","ge",2)); r.span(23,26,numeric_guard("player.perception","lt",2)); r.span(27,35)
    a=r.menu("open_door",[37,38,39]); r.reward(a[2],"faction.church",-1)
    r.span(41,48); b=r.menu("duke_relief",[50,51,52])
    r.span(54,55,choice(b[0])); r.span(57,62,choice(b[1])); r.span(64,66,choice(b[2]))
    r.reward(b[0],"faction.church",1); r.reward(b[1],"faction.church",-1)
    r.span(67,71); r.span(73,74,bg="cafe.door.thibaut"); r.mark(72,"stage-cg",r.root+".p073")
    r.span(75,75,bg="scene.cafe"); r.end()
    return r.done()

def _church1020(c):
    r=c.source("MA70.10.20-S1.docx","church.1020.s1",prerequisites=("story.church.1014.s2.completed","story.revolution.1014.s1.completed"))
    r.span(3,18,bg="scene.cafe"); a=r.menu("search",[20,21,22])
    r.span(24,24,any_choices(*a[:2]))
    r.span(27,28,guard(choice(a[2]),numeric_guard("player.courage","ge",4)))
    r.span(30,33,guard(choice(a[2]),numeric_guard("player.courage","lt",4)))
    r.span(35,41); ids=r.menu("explain",[44,45,46,47])
    ranges=[(49,49,51,57,59,65),(67,68,70,78,80,85),(0,0,88,94,96,102),(104,104,106,114,116,122)]
    branches=[]
    for cid,attr,(p1,p2,o1,o2,f1,f2) in zip(ids,["language","courage","wisdom","perception"],ranges):
        ck=r.check("search_"+attr,attr,8)
        next(row for row in c.rows["Choice"] if row["choiceId"]==cid)["checkId"]=ck
        if p1: r.span(p1,p2,choice(cid))
        ok=guard(choice(cid),"check:"+ck); fail=guard(choice(cid),"not:check:"+ck)
        branches.append((ok,fail)); r.span(o1,o2,ok); r.span(f1,f2,fail)
    for idx,(ok,fail) in enumerate(branches):
        suffix=f".branch{idx+1}"
        r.span(124,141,ok,suffix=suffix)
        burn=r.line(142,ok,bg="cg.burning_letter",suffix=suffix)
        r.mark(143,"user-marked-art-placeholder",burn["pageId"])
        r.span(144,145,ok,bg="scene.cafe",suffix=suffix)
        r.flag("story.church.1020.s1.cleared",guard(choice(ids[idx]),numeric_guard("player."+["language","courage","wisdom","perception"][idx],"ge",8))); r.end("cleared"+suffix,ok)
        r.span(147,153,fail,suffix=suffix)
        r.flag("church.detention.requested",guard(choice(ids[idx]),numeric_guard("player."+["language","courage","wisdom","perception"][idx],"lt",8)),"Request only; host must really detain/restrict before unblocking")
        r.result("SET_FLAG","church.detention.active","true",guard(choice(ids[idx]),numeric_guard("player."+["language","courage","wisdom","perception"][idx],"lt",8)),note="Canonical detention state; authoritative DayFlow skips map/business and completes actual days without income")
        r.end("detained"+suffix,fail)
    r.depend("church.detention.active","Host-owned authoritative state; no fake three-day advance")
    return r.done()

def _church1023(c):
    r=c.source("MA70.10.23-S1.docx","church.1023.s1",prerequisites=("story.church.1020.s1.completed","church.detention.active"))
    r.span(6,19,bg="detention.dawn")
    a=r.menu("release",[21,22,23]); r.span(25,29,choice(a[0]))
    persuasion=guard(choice(a[0]),numeric_guard("player.language","ge",7))
    pfail=guard(choice(a[0]),numeric_guard("player.language","lt",7))
    r.span(32,34,persuasion); r.span(37,37,pfail)
    money_guard=numeric_guard("cafe.money","ge",CHURCH_BRIBE_AMOUNT)
    money_short=numeric_guard("cafe.money","lt",CHURCH_BRIBE_AMOUNT)
    retry_when=guard(pfail,money_guard)
    retry=r.menu("bribe_retry",[22,23],retry_when)
    # The source's funding condition is a live cafe balance, not a narrative
    # readiness flag. Keep the initial option gated as well as the retry menu.
    for cid in (a[1],retry[0]):
        c.condition("choice",cid,"cafe.money",f"{CHURCH_BRIBE_AMOUNT},2147483647",kind="BAR_BETWEEN")
    bribe=any_choices(a[1],retry[0]); r.span(40,48,bribe)
    price=r.menu("bribe_price",[50,51],bribe)
    r.span(53,57,choice(price[0])); r.mark(56,"money-settlement",status="implemented")
    r.span(59,61,choice(price[1]))
    bribery=guard(choice(price[0]),money_guard)
    r.result("MONEY_REMOVE","cafe",CHURCH_BRIBE_AMOUNT,bribery,owner=price[0]+".result",note="Source P046 final demand; actual cafe balance debit settles the bribe")
    ck=r.check("altered_release",None,None,[("wisdom",6,1),("perception",6,2)])
    routes=[("direct",choice(a[2])),("decline",any_choices(retry[1],price[1])),
            ("insufficient",guard(bribe,money_short))]
    rescues=[]
    for name,attempt in routes:
        rescue=guard(attempt,numeric_guard("faction.revolution","ge",0)); rescues.append((name,rescue))
        failure=guard(attempt,numeric_guard("faction.revolution","lt",0))
        r.span(64,73,rescue,bg="detention.dusk",suffix="."+name)
        r.passive("notice_release_check."+name,ck,75,rescue)
        r.span(76,82,guard(rescue,"check:"+ck),suffix="."+name); r.span(83,93,rescue,suffix="."+name)
        r.flag("church.release.records_anomalous",guard(rescue,numeric_guard("player.wisdom","ge",6)))
        r.flag("church.release.records_anomalous",guard(rescue,numeric_guard("player.wisdom","lt",6),numeric_guard("player.perception","ge",6)))
        r.flag("church.revolution.intervened",rescue)
        r.flag("church.detention.release_failed",failure)
        r.result("QUEUE_EVENT","church.1023.s2","",failure,note="Only actual failed escape and revolution inclination<0; no date changes")
        r.end("failure."+name,failure)
    returns=[("persuasion",persuasion,111,113),("bribery",bribery,115,122)]
    returns += [("rescue."+name,when,124,139) for name,when in rescues]
    for name,when,start,stop in returns:
        outcome=name.split(".")[0]
        r.span(98,108,when,bg="scene.cafe",suffix="."+name); r.span(start,stop,when,suffix="."+name)
        r.span(141,162,when,suffix="."+name)
        r.flag("church.release."+outcome,when); r.flag("story.church.1023.s1.release."+outcome,when)
        if outcome=="bribery": r.flag("story.church.1023.s1.release.bribe",when,"Legacy Rose alias")
        r.result("SET_FLAG","church.detention.active","false",when,note="Canonical state cleared only by actual successful release root-tail; host owns real daily transitions/map/business")
        r.end("returned."+name,when)
    r.audit["decisions"].append(dict(kind="source-derived-amount",paragraphs=[41,46,56],offer=2000,demand=CHURCH_BRIBE_AMOUNT,settledAmount=CHURCH_BRIBE_AMOUNT,scope="funding-option",detail="P046 states the final demanded amount is 3000; P056's pending deduction is settled as a real MONEY_REMOVE after the live cafe balance guard."))
    r.depend("church.detention.active","Main DayFlow blocks normal map/business while active; actual CompleteDay transitions without income through Oct21/22; release runs Oct23 morning")
    return r.done()

def _church_if2(c):
    r=c.source("MA70.10.23-S2-End if3.docx","church.1023.s2",prerequisites=("story.church.1023.s1.completed","church.detention.release_failed","church.detention.active"))
    r.span(5,34,bg="detention.interior"); r.ending("church.ending.if2","牢狱终点","church.detention.release_failed")
    for n in range(35,40): r.mark(n,"host-checkpoint-ui",status="optional-checkpoint-unresolved")
    r.depend("Existing Ending terminal/GameFlow menu","Normal terminal uses existing Ending lifecycle; optional return-to-last-choice remains separate pending UX, not root blocker")
    r.end(); return r.done()

def _church1104(c):
    r=c.source("MA70.11.04-S1.docx","church.1104.s1",prerequisites=("story.rose.1103.s1.completed","church.alienor.sheltered"),blocked=())
    r.span(5,11,bg="cafe.dormitory.day"); r.span(13,20,bg="scene.cafe")
    r.span(22,28,numeric_guard("player.perception","ge",3)); r.span(30,32,numeric_guard("player.perception","lt",3)); r.span(34,39)
    pay=r.menu("guide_fee",[41,42,43]); r.span(45,47,choice(pay[0])); r.span(49,58,choice(pay[1]))
    r.reward(pay[1],"cafe.money",1)
    fee_text_key=f"{r.root}.text.p043.option"
    fee_text=next(row for row in r.c.rows["Text"] if row["key"]==fee_text_key)
    fee_text["text"]=fee_text["text"].replace("xx个国王币","50个国王币")
    fee_text["comment"] += "; amount resolved by the selected branch's P061/P064 50-coin receipt"
    r.span(60,68,choice(pay[2])); r.mark(43,"choice",pay[2])
    r.reward(pay[2],"cafe.money",50)
    r.audit["decisions"].append(dict(kind="source-derived-amount",paragraphs=[43,61,64],amount=50,detail="P061 explicitly receives 50 coins and P064 specifies Money+50; the linked P043 placeholder is compiled as 50."))
    r.business_end("business.end",(69,70))
    r.span(71,73); order=r.menu("tour_order",[75,76]); g1,g2=choice(order[0]),choice(order[1])
    r.span(80,119,g1,bg="market.day"); r.span(121,127,g1,bg="suburb.day")
    r.span(129,129,guard(g1,numeric_guard("player.perception","ge",5))); r.span(131,164,g1)
    r.span(166,171,g1,bg="old.street.day"); r.span(173,174,guard(g1,numeric_guard("player.perception","ge",3))); r.span(176,223,g1); r.span(225,228,g1,bg="street.day")
    r.span(230,236,guard(g1,numeric_guard("player.wisdom","ge",4)))
    r.span(238,244,guard(g1,numeric_guard("player.wisdom","lt",4))); r.span(246,254,g1)
    r.span(257,263,g2,bg="street.day"); r.span(265,270,g2,bg="old.street.day"); r.span(272,273,guard(g2,numeric_guard("player.perception","ge",3))); r.span(275,322,g2)
    r.span(324,364,g2,bg="suburb.day"); r.span(366,406,g2,bg="market.day"); r.span(408,415,g2,bg="street.day")
    r.span(417,431,bg="scene.cafe"); r.menu("patient",[433,434,435]); r.span(436,443)
    r.span(445,457,numeric_guard("faction.revolution","le",0)); r.span(459,479,numeric_guard("faction.revolution","gt",0))
    r.flag("known.alienor",note="Actual day together"); r.end()
    r.audit["decisions"].append(dict(kind="tunable",detail="Source leaning/neutral not numeric; >0/<=0 are explicit provisional cutoff, not author fact"))
    for a in r.anchors.values():
        if "xx" in a["sourceText"]: a["status"]="blocked-decision"
    return r.done()

def _church1105(c,sheltered):
    root="church.1105.s2" if sheltered else "church.1105.s1"
    pre="church.alienor.sheltered" if sheltered else "church.alienor.refused_shelter"
    prerequisites=["story.rose.1103.s1.completed",pre]
    if sheltered: prerequisites.append("story.church.1104.s1.completed")
    r=c.source("MA70.11.05-S2.docx" if sheltered else "MA70.11.05-S1.docx",root,prerequisites=prerequisites)
    opposite="church.alienor.refused_shelter" if sheltered else "church.alienor.sheltered"
    c.condition("event",root,opposite,"false")
    base=[row.copy() for row in c.rows["Condition"] if row["ownerType"]=="event" and row["ownerId"]==root]
    c.condition("event",root,"story.rose.1024.s1.completed",group=1)
    for cond in base: c.condition("event",root,cond["target"],cond["value"],cond["condType"],group=2)
    c.condition("event",root,"story.rose.1024.s2.completed",group=2)
    r.depend("story.rose.1024.s1.completed|story.rose.1024.s2.completed","Current source says10/24; OR of real completions")
    r.span(4,9,bg="scene.cafe"); ck=r.check("intuition",None,None,[("wisdom",4,1),("perception",4,2)])
    r.passive("intuition",ck,11); r.span(12,17 if sheltered else 16,"check:"+ck)
    shift=1 if sheltered else 0
    r.span(18+shift,19+shift); a=r.menu("greet",[21+shift,22+shift,23+shift])
    for cid,start in zip(a,[25+shift,28+shift,31+shift]): r.span(start,start+1,choice(cid))
    r.span(34+shift,35+shift); r.gameplay("serve.duke","blackcoffee",anchors=(36+shift,)); r.span(37+shift,54)
    b=r.menu("answer",[56,57,58])
    if sheltered:
        r.span(60,60,any_choices(b[0],b[2])); r.span(62,62,choice(b[1])); d=r.menu("outing_report",[65,66])
        r.span(68,71,choice(d[0])); r.span(73,73,guard(choice(d[0]),numeric_guard("player.perception","ge",4))); r.span(75,85,choice(d[0]))
        r.end("truth_end",choice(d[0])); r.span(87,109,choice(d[1])); r.reward(d[1],"alienor.affection",1)
        r.flag("known.alienor",any_choices(*d),"Actual meeting in BOTH branches; root-tail including early End")
        r.end("conceal_end",choice(d[1]))
    else:
        r.span(60,61,choice(b[0])); r.span(63,66,guard(choice(b[0]),numeric_guard("player.language","ge",4)))
        r.span(68,69,guard(choice(b[0]),numeric_guard("player.language","lt",4))); r.span(71,72,choice(b[1])); r.span(74,75,choice(b[2]))
        r.audit["decisions"].append(dict(kind="source-label-correction",paragraph=73,detail="Third response erroneously labelled①; actual third option preserved"))
        r.span(77,83); d=r.menu("question_duke",[85,86]); r.span(88,93,choice(d[0])); r.end("quiet_end",choice(d[0]))
        r.span(96,102,guard(choice(d[1]),numeric_guard("player.courage","ge",4)))
        r.span(104,108,guard(choice(d[1]),numeric_guard("player.courage","lt",4))); r.span(110,len(r.source),choice(d[1])); r.end("challenge_end",choice(d[1]))
    return r.done()

def _hub1111(c):
    r=c.source("MA70.11.11-S1（内含end if4-再见，亚拿尼亚）.docx","church.1111.s1",prerequisites=("story.revolution.1107.s1.completed",))
    r.depend("story.revolution.1107.s1.completed","Real Lesou common prerequisite; integrator must verify current source ID")
    for name in ROUTES: c.condition("event",r.root,"route."+name,"false")
    r.span(3,26,bg="scene.cafe"); r.span(28,30,numeric_guard("player.language","ge",6))
    r.gameplay("serve.lucien","latte",numeric_guard("player.language","ge",6),anchors=(31,))
    r.span(32,35,numeric_guard("player.language","ge",6)); r.span(38,44)
    a=r.menu("s1",[46,47]); r.span(49,50,choice(a[0])); r.end("inform_duke_first",choice(a[0]))
    r.span(53,55,choice(a[1])); b=r.menu("s2",[57,58],choice(a[1]))
    r.span(61,62,choice(b[0])); r.end("inform_duke_second",choice(b[0]))
    failure=guard(choice(b[1]),numeric_guard("player.language","lt",4))
    success=guard(choice(b[1]),numeric_guard("player.language","ge",4))
    r.span(66,77,failure); r.ending("church.ending.if4","无言的终点",failure); r.end("if4_end",failure)
    r.span(79,83,success); d=r.menu("s3",[85,86],success); r.span(89,89,choice(d[0])); e=r.menu("s4",[90,91,92],choice(d[0]))
    routes=[("neutral","neutral.route.1111.s1",any_choices(a[0],b[0])),("neutral","neutral.route.1111.s2",choice(d[1])),("church","church.route.1111.s1",choice(e[0])),("rose","rose.route.1111.s2",choice(e[1])),("revolution","revolution.route.1111.s2",choice(e[2]))]
    for faction,target,when in routes:
        r.result("SET_FLAG","route."+faction,"true",when,note="Exclusive actual choice; central route flags")
        r.result("QUEUE_EVENT",target,"",when,note="CURRENT source S1-4 routing; root-tail only"); r.depend(target,"Queue target owned by destination compiler; must exist")
    for faction,cid in zip(("church","rose","revolution"),e):
        key="route.eligible."+faction; c.fact(key,only_set_true=False); c.condition("choice",cid,key)
        r.depend(key,"Source only says inclination check, no cutoff; central explicit tunable/provider required")
        r.mark({"church":90,"rose":91,"revolution":92}[faction],"choice",cid,"blocked-decision")
    r.audit["decisions"].append(dict(kind="unresolved-eligibility",paragraphs=[90,91,92],detail="No numeric cutoff in source; all options retained, locked until approved eligibility facts"))
    r.end(); return r.done()

def _route(c,mmdd,pre,blocked=()):
    return c.source(f"RY70.{mmdd[:2]}.{mmdd[2:]}-S1.docx",f"church.route.{mmdd}.s1",prerequisites=tuple(pre),blocked=blocked,route=True)

def _route1111(c):
    r=_route(c,"1111",["story.church.1111.s1.completed","story.church.1104.s1.completed"])
    r.span(3,12,bg="scene.cafe"); r.span(14,32,bg="street.day"); r.span(33,40,bg="scene.cafe")
    r.audit["decisions"].append(dict(kind="art-pending",paragraph=13,detail="No academy asset; explicit registered street.day transitional fallback"))
    r.end(); return r.done()

def _route1112(c):
    r=_route(c,"1112",["story.church.route.1111.s1.completed"])
    r.span(3,5,bg="scene.cafe"); r.business_end("business.end",(6,7)); r.span(8,len(r.source),bg="scene.cafe")
    r.flag("known.alienor",note="P008 onward actual face-to-face meeting"); r.end(); return r.done()

def _letter(r,name,start,stop,when=""):
    pk=r.root+".reading."+name; pid=pk+".page"
    r.c.add("PresentationIndex",presentationKey=pk,presentationClass="Reading",logTag=r.root)
    r.c.add("ReadingPage",pageId=pid,presentationKey=pk,seq=1)
    p=r.page("letter."+name,when,step="LetterRead"); p["readingKey"]=pk
    for seq,n in enumerate(range(start,stop+1),1):
        if directive(r.source[n-1]): continue
        key=r.root+f".text.p{n:03}"; r.c.text(key,visible_text(r.source[n-1]),f"{r.path.name} paragraph{n}; letter, not actual meeting")
        lid=pid+f".line.{seq:03}"; r.c.add("ReadingLine",lineId=lid,pageId=pid,seq=seq,mode="narration",textKey=key); r.mark(n,"letter",lid)

def _route1113(c):
    r=_route(c,"1113",["story.church.route.1112.s1.completed"])
    r.span(3,9,bg="scene.cafe"); _letter(r,"princess",11,23); r.span(24,53,bg="scene.cafe")
    tasting=r.gameplay("taste.lefebvre","any",threshold=EXCELLENT_SCORE_PRESET,anchors=(54,),taste=True)
    passed="gameplay:"+tasting["pageId"]+":passed"
    r.span(56,56,passed); r.span(58,58,"not:"+passed)
    r.audit["decisions"].append(dict(kind="quality-preset",paragraph=55,excellentScore=EXCELLENT_SCORE_PRESET,detail="Shared excellent90 preset, NOT source numeric authority; real any-product tasting returns low=false or excellent=true"))
    r.span(60,65); r.span(67,72,bg="old.house.day"); r.span(73,79,bg="scene.cafe")
    r.audit["decisions"].append(dict(kind="art-pending",paragraph=66,detail="No tunnel asset; explicit old.house.day transitional fallback"))
    r.end(); return r.done()

def _route1115(c):
    r=_route(c,"1115",["story.church.route.1113.s1.completed"])
    r.span(3,16,bg="scene.cafe"); r.gameplay("serve.messenger","latte",anchors=(17,)); r.span(18,20)
    _letter(r,"princess",22,27); r.span(28,31); r.span(33,36,bg="cathedral.day")
    a=r.menu("follow_thibaut",[38,39]); r.span(41,47,choice(a[0]))
    r.span(49,51,guard(choice(a[0]),numeric_guard("player.wisdom","ge",10)))
    r.span(53,54,guard(choice(a[0]),numeric_guard("player.wisdom","lt",10))); r.span(56,56,choice(a[1]))
    r.span(58,73); r.span(75,75,choice(a[0])); r.span(77,77,choice(a[1])); r.span(79,82)
    r.span(84,88,bg="cafe.dormitory.day"); b=r.menu("watch_revolution",[90,91]); r.span(92,92)
    r.flag("church.thibaut.followed",choice(a[0]),"First duplicated source label: P038")
    r.flag("church.revolution.watch_requested",choice(b[0]),"Second duplicated label: P090, NOT follow")
    r.flag("known.sade",note="P058-080 actual face-to-face encounter")
    r.audit["decisions"].append(dict(kind="source-label-disambiguation",paragraphs=[37,89],detail="Repeated RY1115-S1-1 split into follow_thibaut/watch_revolution; downstream ambiguous references audited"))
    r.end(); return r.done()

def _route1116(c):
    r=_route(c,"1116",["story.church.route.1115.s1.completed"])
    r.span(3,6,bg="scene.cafe")
    # NCS allows only one choice per group. Unique finite-tree groups encode
    # choose ALL topics once in arbitrary order, without fake persistent facts.
    def tail(prefix,when):
        r.span(55,80,when,suffix="."+prefix); r.end("end."+prefix,when)
    def topics(prefix,remaining,when):
        if not remaining: tail(prefix,when); return
        ids=r.menu("topics."+prefix,[n for _,n in remaining],when)
        for cid,(topic,_) in zip(ids,remaining):
            branch=guard(when,choice(cid)); tag=prefix+"."+topic
            if topic=="thibaut": r.span(12,15,branch,suffix="."+tag)
            elif topic=="stigma": r.span(18,35,branch,suffix="."+tag)
            else:
                r.span(38,43,branch,suffix="."+tag); ask,deal=r.menu("compensation."+tag,[45,46],branch)
                r.span(49,50,guard(branch,choice(ask),numeric_guard("alienor.affection","ge",4)),suffix="."+tag)
                r.span(52,53,guard(branch,choice(ask),numeric_guard("alienor.affection","lt",4)),suffix="."+tag)
            topics(tag,[(t,n) for t,n in remaining if t!=topic],branch)
    topics("followed",[("thibaut",8),("stigma",9),("duke",10)],"church.thibaut.followed")
    topics("not_followed",[("thibaut",8),("duke",10)],"not:church.thibaut.followed")
    r.flag("known.alienor",note="Actual meeting at start")
    r.audit["decisions"].append(dict(kind="contextual-reference",paragraph=9,detail="Stigma topic requires actual follow P038, not unrelated watch choice P090"))
    return r.done()

def _route1118(c):
    r=_route(c,"1118",["story.church.route.1116.s1.completed"])
    r.span(3,16,bg="cafe.dormitory.day"); r.mark(18,"business-preparation",status="control-or-source-label"); r.mark(19,"business-preparation",status="control-or-source-label"); r.span(20,33,bg="scene.cafe"); r.business_end("business.early_close",(33,)); r.span(34,45)
    r.span(47,75,bg="cafe.dormitory.day"); r.span(77,len(r.source),bg="scene.cafe")
    r.end(); return r.done()

def _route1123(c):
    r=c.source("RY70.11.23-S1【结局1&2】.docx","church.route.1123.s1",prerequisites=("story.church.route.1118.s1.completed",),route=True)
    yes,no="church.thibaut.followed","not:church.thibaut.followed"
    r.span(3,14,bg="scene.cafe"); r.span(16,18,yes); r.span(21,24); r.business_end("business.end",(26,)); r.span(27,36)
    r.span(38,40,yes); r.span(42,45,no); r.span(47,49); r.span(51,56,bg="cathedral.day")
    r.span(58,58,yes); r.span(60,60,no); r.span(62,65); r.span(67,77,bg="scene.cafe")
    r.span(80,143,yes); r.span(210,215,yes,bg="cathedral.day"); r.span(217,232,yes,bg="scene.cafe")
    r.ending("church.ending.ry1","美丽新世界",yes)
    r.flag("known.sade",note="Common P051-063 actual encounter")
    r.flag("known.alienor",yes,"Actual success meeting, never death branch")
    r.end("ry1_end",yes)
    r.span(146,152,no,bg="scene.cafe"); a=r.menu("last_reply",[154,155,156],no)
    for cid,n in zip(a,[158,160,162]): r.span(n,n,guard(no,choice(cid)))
    r.span(164,201,no)
    for n in range(202,209): r.mark(n,"host-checkpoint-ui" if n<=206 else "directive",status="optional-checkpoint-unresolved" if n<=206 else "mapped-directive")
    r.ending("church.ending.ry2","历史的分叉路，左还是右？",no); r.end("ry2_end",no)
    r.audit["decisions"].append(dict(kind="source-label-disambiguation",paragraphs=[15,37,57,78,144],fact=yes,detail="Repeated RY1115 source labels are resolved by the existing follow_thibaut choice fact; this route is production-ready and retains the church route predecessor."))
    r.audit["decisions"].append(dict(kind="source-label-correction",paragraphs=[157,159,161],detail="Three repeated① labels mapped to actual ①/②/③ response semantics"))
    return r.done()

def build(project_root: Path) -> dict[str,list[dict]]:
    """Compile all 16 CURRENT source documents, never write external state."""
    c=_Compiler(Path(project_root))
    c.fact("known.sade","Actual Sade encounter, root-tail only")
    c.fact("known.alienor","Actual Alienor encounter, root-tail only")
    for key in ("church.alienor.sheltered","church.alienor.refused_shelter","church.detention.active"):
        c.fact(key,"External owner; false until real source/host produces it",only_set_true=key!="church.detention.active")
    _church1009(c); _church1014(c); _church1020(c); _church1023(c); _church_if2(c)
    _church1104(c); _church1105(c,False); _church1105(c,True); _hub1111(c)
    _route1111(c); _route1112(c); _route1113(c); _route1115(c); _route1116(c); _route1118(c); _route1123(c)
    actual={p.name for p in (Path(project_root)/SOURCE_DIR).glob("*.docx")}
    if actual!=set(SOURCE_HASHES): raise ValueError(f"Church source inventory changed: added={actual-set(SOURCE_HASHES)}, missing={set(SOURCE_HASHES)-actual}")
    return c.finish()
