# Whole narrative integration contract (2026-10-19)

Source of truth: current repository 策划文件/文案, not old D:/桌面 files. Source workbooks are owned by the main integrator only. Never edit Narrative.xlsx/Text.xlsx/generated files in a worker.

Content workers own one Python module under Tools/Narrative/Content. Export build(project_root: pathlib.Path) -> dict[str,list[dict]]. Keys are existing Narrative sheet names plus Text. Row dicts use the existing source header spelling. Include `_audit` metadata separately with source path/hash, supported events, unresolved dependencies. Main integrator will merge rows by each sheet's primary key then run official Luban. Do not delete existing entries; explicit replacement prefixes can be listed in `_replace_roots` for your faction's content only. Do not run generation.

Use standard IDs: common roots neutral.MMDD.sN / rose.MMDD.sN / church.MMDD.sN / revolution.MMDD.sN. Keep oct07.s1/oct07.s2 unchanged, and keep neutral.1013.s2 as the compatibility root for the current neutral 10/13-S1 source. Route roots use neutral.route.MMDD.sN, rose.route.MMDD.sN, church.route.MMDD.sN, revolution.route.MMDD.sN. Text keys root.text.pNNN or disambiguated split suffix. Every current nonempty paragraph is a source anchor; directives/labels are never player dialogue.

ConditionKey story.<root>.completed: false/onlySetTrue=true. Four exclusive flags route.neutral,route.rose,route.church,route.revolution. Existing player specializations remain specialization.player.letter.politics/literature/religion/science. Attribute IDs language/courage/wisdom/perception. Numeric bar targets: adele.affection, antoine.affection, colette.affection, alienor.affection, sylvain.affection, lesou.affection; faction.neutral, faction.rose, faction.church, faction.revolution. These are centralized implementation defaults, not claimed source numeric authority. No separate copies of money/calendar/AP.

day1 = game 670-10-06. EventDate morning maps MorningBeforeMap, noon maps AfterBusiness, night maps BeforeNight. Map actions use entry=MapAction plus date/action conditions. All narrative roots blocksSave=true, onceOnly=true. Existing root completion is SET_FLAG story.<root>.completed=true. Result choice effects use Choice.resultOwnerId and settle at root completion; do not commit on click. Queue continuations through existing QUEUE_EVENT only.

Conversation step contract being implemented in the existing NCS: End (completes current root; no dialogue), Gameplay (suspends this presenter owner and waits for real cafe interaction), plus existing Dialogue/PassiveCheck/LetterReveal/LetterRead. Page fields `nextPageId` (optional explicit graph edge), `gameplayKey` (only for Gameplay). Existing next linear order remains default. For immediate implementation prefer guarded linear pages with End branch nodes; do not assume an arbitrary JS scripting language. Branch guard pageCondKeys uses existing pipe/comma AND, choice:<id>, anyChoice:<id>;<id>, check:<checkId>, not:<guard>, declared global booleans. New read-only numeric guard: bar:<barKey>:<operator>:<integer>, operators ge/gt/le/lt/eq/ne. Missing numeric providers fail closed with diagnostics. Result ownerType=result uses BOOL_EQ target=<presentation guard> value=true/false for root-local conditional settlement. Do not author fake persistent choice facts merely to route local pages.

Gameplay keys agreed for cafe bridge: `serve:<productId>:<minimumScore>:<count>` and multi-requirement `serve:<productId>:<minimumScore>:<count>+<productId>:<minimumScore>:<count>`. Use only catalog-confirmed IDs. Threshold mapping is an implementation preset (normal60/excellent90), documented as tunable, not original author fact; if source explicitly says number use that. Runtime exposes gameplay:<pageId>:passed as root-local guard. No auto success, no fake order completion, no inventory/coffee duplicate systems.

Cafe normal backgrounds scene.cafe. Special shop CG keep actual key (cafe.door.thibaut). External scenes must use registered resources; if unknown, leave inherited only when semantically appropriate and audit as art pending. Never register operating floor base art as dialogue backgrounds. Character IDs reuse CharacterCatalog/Object; do not impersonate unknown actors using a named wrong person. Add neutral generic Object rows only when required, empty portraits allowed and audited.

If a source decision cannot be accurately compiled, DO NOT flatten all mutually exclusive dialogue into an unguarded sequence. Mark the root construction-blocked with a concrete blockedBy and record the unresolved node. Prefer a correct bounded root to a fake complete event. Preserve existing working coffee interaction and tutorial.

Every worker must add focused tests and a handoff MD listing changed paths, API assumptions, source coverage, real tests run and remaining limitations. Workers have disjoint write scopes. Main owns Excel merge, final generation, integration tests and top-level handoff.

## Final central amendments

The real host additionally supports `taste:` (one real grading attempt; low score continues with passed=false), `any`, `any.hot` (actual >=40°C, tunable implementation preset), `blackcoffee.sweet` and `blackcoffee.unsweetened` (actual sugar positive/zero, distinct physical cup deliveries). `host:end_business` calls the existing bound cafe endpoint and does not advance the calendar. The typed immutable domain receipt comes ONLY from ServeProduction; do not regress to Changed/state-diff inferred success.

Canonical detention fact is **church.detention.active**, mutable. Daily action availability uses this same fact, not story.detained. Root-local result guards remain available until whole-root preflight. Direct Reading cancellation has an optional request-owned cancellation callback, so cancelling a daily reader cannot grant training.

Natural phase order is MorningBeforeMap -> business -> AfterBusiness -> MapAction -> BeforeNight. Church1009 must remain day4/Night/BeforeNight because its Rose1009 prerequisite completes from the later map visit. Do not move it to Noon simply because the source says return journey; static configuration validity alone does not establish reachability.

Preserve the verified existing Rose1009 baseline: perception>=5, P073 with CG on pass / same text without CG on failure, both share P074-P076. Do not treat an agent's speculative threshold question as author approval for a new value.

Relation Bar targets are player; canonical catalog identity marie, not a new mary Object. Authoring fact known.mary is a semantic fact and does not imply another person identity.

Post-integration current handoff/verification are Docs/NarrativeIntegrationHandoff.md and Docs/NarrativeIntegrationVerification.md. Source audit coverage, compile/static validity and actual whole-game natural play are separate milestones.

## Map dialogue presentation correction (2026-10-19)

All 240 map daily/first-visit/news-reaction roots use existing Conversation pages and actor-aware lines, not flattened Reading narration. This supersedes the source overview B22 ordinary-reading presentation policy in response to the user's in-game panel feedback. Keep the source workbook immutable. Actual newspapers remain Reading; public bulletins remain Notice. Cafe ambient/order snapshots remain nonmodal. Do not revert this routing during regeneration, and do not change root IDs, conditions, training, AP, or selection branch semantics for a panel change. Unknown speaking actors fail closed; the source generic church helper is its own portrait-free Object daily.actor.church_helper, never a named cast substitute.
