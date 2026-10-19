# Current church/common-hub compiler handoff

Updated: 2026-10-19 (Asia/Shanghai), after main's wildcard tasting / authoritative detention / host:end_business contract updates.

## Owned paths and delivery boundary

Only these three files were authored:

- Tools/Narrative/Content/church.py
- Tools/Narrative/Content/test_church.py
- Docs/NarrativeChurchContentHandoff.md

No Excel, generated JSON/C#, runtime, schema, asset, scene, or other worker file was edited. Main owns Excel merge, official Luban, Unity compile and actual host integration. Compiler uses only Python standard library. Tests additionally read the existing source workbooks through installed openpyxl.

**16 CURRENT DOCX / 1,952 nonempty source paragraphs mapped. 15 roots have compiled normal paths; only RY1123 retains a whole-root source-author blocker.** This is content/compiler readiness, not a claim that this worker has run Unity successfully.

## Final central-merge status — PARTIAL STORY, NOT ALL COMPLETE

The three owned deliverables are finished for this merge. This is **partial story integration**, not a claim that the entire narrative or every church ending is complete/playable. Source mapping coverage is not gameplay completion. RY1123 remains explicitly root-blocked for unresolved source-author choice-reference ambiguity; no confirmation is invented. Funding/fee/route-eligibility options and optional checkpoint/art/audio limitations remain documented below.

Main integrator reported actual NCS load passing at **346 / 5508** before final merge. This is central-reported evidence, not a Unity run performed by this content worker, and does not override unresolved author decisions or prove every chronological branch. The reported false Reading diagnostic is assigned to the runtime worker; central relation-target/player additions are central ownership. Neither is implemented or claimed fixed by this compiler worker.

## API / merge

    from pathlib import Path
    from Tools.Narrative.Content.church import build
    rows = build(Path(r"E:\myproject\unity_p_taptap2026"))

Return: sheetName -> list[dict], plus _audit and _replace_roots. Sheet rows contain ONLY approved sheet headers, including contract ConversationPage.gameplayKey/nextPageId. Provenance never leaks into sheet rows.

_audit is a JSON-serializable list of16 root records: source path/hash/count, root/status/blockedBy/prerequisites/dependencies/decisions, optional nodeBlockers and complete original paragraph anchors with kind/status/emitted row IDs. _replace_roots contains exactly16 owned church root IDs, NOT church.*. Preserve unrelated roots/facts and other workers' data.

Build is deterministic/read-only. Source inventory and SHA-256 are pinned; source edits/additions/removals fail closed until anchors are re-audited. Word line breaks/tabs and nested paragraphs are retained. No old snapshots are used.

All roots onceOnly=true/blocksSave=true; completion story.<root>.completed. Proper date phase entries are MorningBeforeMap/AfterBusiness/BeforeNight. church.route.1111.s1 and church.1023.s2 are Immediate QUEUE_EVENT continuations, without duplicate EventDate rows.

Conditional results use ownerType=result / ownerId=resultId / BOOL_EQ / target=<presentation guard> / value=true. Choice.resultOwnerId effects settle only at root completion; no click-side effects, no fake persistent local choice facts.

Both letters have Reading PresentationIndex entries: church.route.1113.s1.reading.princess and church.route.1115.s1.reading.princess. PassiveCheck pages now carry real success content as required by the actual loader validator; failed checks skip it.

### Current row counts

| Sheet | Rows |
|---|---:|
| Event / EventDate |16 /14|
| ConversationPage / ConversationLine |2373 /2270|
| Choice |95|
| ReadingPage / ReadingLine |2 /19|
| AbilityCheck / AbilityCheckCondition |7 /10|
| Result / Condition |105 /358|
| ConditionKey / Ending |49 /4|
| Object / Text |12 /1651|
| SetupParams / PresentationIndex |16 /18|

Repeated guarded pages implement alternatives/topic-order paths; counts are NOT a completion percentage. Source Text anchors are deduplicated.

## Source-by-source status

### 文案6点当前覆盖修正（2026-10-19）

下方旧表的来源目录、11/4 段数和第三报价锁定状态为早期快照；当前输入为 `策划文件/文案6点/文案/a-王庭教会（共通+支线）`，细节见 `Docs/Narrative6PointIntegration.md`。11/4 新稿原480段，其中P096/P097是拆开的同一句，编译归一化为479个稳定Text锚点。第三报价由P061收50币、P064金钱+50的明确文本接入真实 `MONEY_ADD cafe 50`；市场 `xx` 占位保留已有具体填值。10/23处死稿/11/11说明改名不更改旧存档所用Root/Ending ID。RY1123源引用歧义仍保持整根关闭。

All current DOCX are in 策划文件/文案/a-王庭教会（共通+支线）.

| DOCX stem | Paragraphs | Root | Actual content status |
|---|---:|---|---|
| MA70.10.09-S2 |50|church.1009.s2|Compiled; hat pick/ignore exclusive, common return; actual rose1009 prerequisite|
| MA70.10.14-S2 |75|church.1014.s2|Compiled; real blackcoffee wait; special Thibaut doorCG then scene.cafe|
| MA70.10.20-S1 |153|church.1020.s1|Compiled; CURRENT selected-attribute threshold8; arrest only sets canonical detention true on actual failure root-tail|
| MA70.10.23-S1 |162|church.1023.s1|Compiled normal persuasion/rescue/IF2 paths; only unresolved funding choice locked|
| MA70.10.23-S2-End if2 |39|church.1023.s2|Compiled existing Ending terminal; optional reverse-last-choice remains pending, not whole-root blocker|
| MA70.11.04-S1 |479|church.1104.s1|Compiled free/one-coin paths + actual host business end; xx fee choice alone locked; both tour orders/checks|
| MA70.11.05-S1 |114|church.1105.s1|Compiled; TRUE refusal and real preceding completions|
| MA70.11.05-S2 |109|church.1105.s2|Compiled; TRUE shelter + real1104; truth/conceal exclusive, affection only conceal|
| MA70.11.11-S1 |94|church.1111.s1|Compiled CURRENT S1-S4 hub; real revolution1107; neutral paths open, faction choices need approved eligibility|
| RY70.11.11-S1 |40|church.route.1111.s1|Compiled; queued hub + route.church + TRUE1104|
| RY70.11.12-S1 |69|church.route.1112.s1|Compiled with host:end_business|
| RY70.11.13-S1 |79|church.route.1113.s1|Compiled taste:any:90:1, true/false local quality branches|
| RY70.11.15-S1 |92|church.route.1115.s1|Compiled; real latte; independently disambiguated follow/watch|
| RY70.11.16-S1 |80|church.route.1116.s1|Compiled all-topics-once tree: six3-topic/two2-topic orders|
| RY70.11.18-S1 |85|church.route.1118.s1|Compiled early-close host:end_business at actual closure point after Adele departure dialogue|
| RY70.11.23-S1【结局1&2】 |232|church.route.1123.s1|ONE remaining whole-root blocker: source duplicate RY1115 ending reference; business boundary is now implemented|

All successors retain truthful completion prerequisites and RY roots require church=true + other three routes=false. They are NOT unconditionally date-available.

## Authoritative host contracts (normal capability blockers removed)

### Detention

Canonical mutable fact: church.detention.active (initial false, onlySetTrue=false). No story.detained parallel state is authored.

- Actual chosen search failure sets true at church1020 root-tail; unrelated high stats cannot rescue a failed chosen check.
- Main's DayFlow owns: after morning phase active => no map and no normal business; actual CompleteDay transitions without income through Oct21/22 to Oct23 release morning. There is NO compiler calendar BAR_ADD/SET, fake skipday or income.
- church1023 requires actual search completion + active=true + actual calendar Oct23. Successful persuasion/rescue root-tail clears active=false and sets its truthful release outcome. Failure keeps detention and queues IF2.
- No church.detention.release_acknowledged gate remains. Release transport/date progression are central host responsibilities, not pretend content facts.
- Persuasion language>=7 releases. Language<7 automatically falls into rescue/IF2 when the unknown funding option is unavailable; no forced dummy retry click. Revolution>=0 rescues, <0 IF2. Rescue clue wisdom>=6 OR perception>=6 (separate OR groups, not AND).
- Bribery is NOT guessed: source offers2000/demands3000 but P056 deduction TBD. Only funding choices require initial-false construction.church.1023.s1.bribery_ready and real quote/affordability. NO zero debit/guessed3000. Approval requires actual transactional MONEY_REMOVE/payment before enabling; current compiler does not emit one. Quote/payment provider facts preserve the future mapped branch without making it currently reachable.

Produced canonical outcomes church.release.persuasion / bribery / rescue, plus same-source compatibility aliases story.church.1023.s1.release.persuasion / bribery / bribe / rescue. They are branch-conditional root-tail facts, never “missing outcome means rescue”. church.detention.requested is only a diagnostic request alias, NOT an alternative authority.

### Business boundaries

Gameplay key host:end_business calls the main-owned endpoint.EndDayFlowBusiness, then continues the same presentation without advancing calendar. Already-closed legitimate success is allowed by main's contract. It is NOT a one-cup substitute for the whole business system.

Placed at:

- church1104 P069-070, before outing.
- RY1112 P006-007, before princess arrival.
- RY1118 after P033 actual early-close signage, before soldiers' private briefing.
- RY1123 P026, before afternoon conversations (root still blocked only by author reference).

No remaining whole-root business-boundary blocker. Main still must exercise actual reentrancy/session ownership and ensure the endpoint does not duplicate income/calendar transitions.

### Coffee bridge

Confirmed source orders only: church1014 blackcoffee; church1105 both blackcoffee; church1111 language>=6 latte; RY1115 latte. All use real serve:<product>:0:1; minimum0 means no authored grade, NOT automatic service completion.

RY1113 P054 now uses taste:any:90:1; guard gameplay:church.route.1113.s1.taste.lefebvre:passed and its negation choose excellent vs ordinary/below. Low tasting legitimately completes with false rather than a must-pass retry. Removed placeholder global serving.excellent/ordinary facts and arbitrary-product root blocker. 90 is the approved shared excellent PRESET, not an original source number.

### Economic result fix from central loader

church.1104.s1.result.002 now uses MONEY_ADD target=cafe value=1, deferred under the one-coin choice result owner. It does NOT write external cafe.money through BAR_ADD. All emitted economic results were searched; this is the only emitted money mutation. Remaining BAR_ADD rewards are writable NCS faction/affection bars. Tests check external/derived write rejection against actual central Bar rows.

## Cross-worker facts / current routes

### Shelter aliases confirmed fixed

Current rose.py produces BOTH church.alienor.sheltered and church.alienor.refused_shelter from actual chosen rose1103 root-tail (alongside its story.rose.1103.s1.sheltered compatibility fact). These match this compiler's consumers. No remaining shelter-name mismatch.

11/5-S1: refusal=true AND shelter=false. 11/5-S2: shelter=true AND refusal=false AND actual church1104 completion. Both require actual rose1103 and one actual rose1024-S1/S2 completion; default-false shelter is NOT refusal.

### Known people

known.sade / known.alienor declared false/onlySetTrue=true; actual-encounter root results only, never mention/letter/click. Cancellation never settles.

Sade: RY1115 actual conversation; RY1123 actual common church meeting. Alienor:1009 actually seen in both branches;1104 actual day together;1105-S2 actual departures;RY1112/1116 actual meetings;RY1123 SUCCESS only actual meeting/epilogue. No grants from RY1111 mention, RY1113 letter,1105-S1, or RY1123 death branch.

### Hub CURRENT S1-S4

True prerequisite: story.revolution.1107.s1.completed (CURRENT Lesou root), NOT nonexistent1108. Hub requires all four route flags false; root-tail selects ONE route flag/queue.

| Choice path | Exact queue target |
|---|---|
| S1 confess OR S2 confess |neutral.route.1111.s1|
| S2 speech succeeds language>=4; S3 stay out |neutral.route.1111.s2|
| S3 faction; S4 princess |church.route.1111.s1|
| S3 faction; S4 New Rose |rose.route.1111.s2|
| S3 faction; S4 revolution |revolution.route.1111.s2|
| S2 speech language<4 |existing IF4 terminal, no queue/route|

Main owns route/faction/affection definitions. All queue destinations must use Immediate. Three faction options retain fail-closed route.eligible.church/rose/revolution because source says inclination check with NO numeric cutoff. Neutral paths do not require these. Approve central eligibility; do not silently guess>=0.

## Remaining blockers / limitations: precise scope

1. **Whole-root ONLY: church.route.1123.s1** blockedBy=source.RY1115_duplicate_choice_reference_needs_author_confirmation; initial-false construction.church.route.1123.s1.ready. RY1115 repeats same source choice label for P037 follow-Thibaut and P089 watch-action. RY1123 ending references are ambiguous. Candidate graph uses follow context but is NOT exposed until author confirms. No arbitrary branch guess is enabled.
2. **Option-only: 1023 funding** construction.church.1023.s1.bribery_ready; offer2000/demand3000/actual deduction TBD. Normal persuasion/rescue/IF2 work without it.
3. **Option-only: 1104 third fee** construction.church.1104.s1.fee_amount_ready; free and exact1-coin choices have NO dependency on this key. xx market remarks are preserved literal author placeholders, never converted into charges. Approve exact fee and corresponding MONEY_ADD before enabling only this option.
4. **Option eligibility: hub three factions** route.eligible.* requires central author-approved cutoff; no whole-root block and both neutral branches remain selectable.
5. **Optional terminal UX:** reverse-last-choice/checkpoint source directives remain pending, but existing Ending/main-menu behavior is the normal terminal. They do NOT block IF2 or normal paths. RY ending blocker is author-reference only.
6. **Art/audio:** generic unnamed actors have neutral Objects/empty portraits; no wrong named impersonation. Only registered backgrounds emitted. Missing academy/tunnel final art uses explicitly audited street.day/old.house.day transitional fallbacks. Audio/unspecified CG/staging notes marked pending, never spoken as text.
7. **Source details preserved:** third-response labels repeated① in1105 and RY1123 are explicitly position/meaning mapped; RY1123's16th-vs18th prose chronology is not silently rewritten. 1104 leaning >0/<=0 remains an explicitly documented provisional cutoff, not author numeric authority.

The former list of8 capability-blocked roots is obsolete: wildcard tasting, authoritative detention, business endpoints, funding-node scoping and existing terminal handling remove those capability blockers. Only item1 above is still a whole-root construction blocker.

## Scene rules

Ordinary café dialogue scene.cafe. 1014 special scene uses cafe.door.thibaut then explicit scene.cafe restore. No operating floor base art used. Thibaut portrait uses anonymous 客人 caption where source hides his identity. Regression must use Assets/Scenes/PresentationDebugScene.unity, never a temporary scene.

## Validation actually performed

    python -m py_compile Tools/Narrative/Content/church.py Tools/Narrative/Content/test_church.py
    python -m unittest Tools.Narrative.Content.test_church -v
    python -c "from Tools.Narrative.Content.church import build; from pathlib import Path; import json; r=build(Path('.')); json.dumps(r,ensure_ascii=False); print({k:len(v) for k,v in r.items()})"

Final local validation: py_compile passed; 26 tests passed; full16-source build/JSON serialization passed; source-anchor coverage1952; all-row reference/economic-rule test passed against CURRENT central Narrative.xlsx/Text.xlsx overlaid with this compiler.

Reference validation checks all emitted event/object/setup/template/index/text/page/choice/check/reading/condition/result/ending refs; guards resolve to real bars/facts/choices/Inline checks/Gameplay pages; queue targets exist and are Immediate; PassiveCheck has success content; Gameplay/End have no dialogue; embedded letters have real Reading indices; background keys registered; economic Bar writes are NOT external/derived. This caught/now protects the actual MONEY_ADD issue and earlier missing letter indices. It is NOT a replacement for Unity LubanNarrativeLoader.Load.

Other tests cover all current hashes, pure deterministic output/no workbook writes, allowed fields/unique keys, hat branches, CG restoration, selected search7/8, canonical detention true/false and zero calendar writes, automatic honest release paths/OR clue/IF2, fee-only lock/real1-coin MONEY_ADD, both outing orders/checks, true shelter prerequisites, exact exclusive hub queues/IF4, real wildcard taste success+failure, four business endpoints, known-person production, independent follow/watch, all six3-topic/two2-topic permutations, no duplicate topic/branch leakage, and condition-gated endings/epilogue.

Tests interpret emitted graphs with injected outcomes; they do NOT certify actual CafeWorld or Unity behavior. Main must regenerate via official source-Excel/Luban pipeline and run actual loader, Unity compilation, DayFlow detention/business endpoint tests, cancellation/save/restore and chronological playable branches. No hand-edited generated JSON.
