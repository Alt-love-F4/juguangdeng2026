# Task List: ncs-formal-integration-overnight

## Meta
- Created: 2026-10-18 03:17
- Task Directory: `.autonomous/ncs-formal-integration-overnight`
- Total Tasks: 95
- Completed: 0/95 (0%)

## Progress Notes
- Initial baseline: `Docs/NcsIntegrationReport.md` reports 38 scanned events with 0 errors and 0 warnings, while `Docs/NcsFormalDependencies.md` says formal acceptance still requires real map/DayFlow, loading, cancellation, retry, save/restore, and exactly-once settlement evidence.
- The worktree already contains user/Locus changes. Future sessions must preserve unrelated modifications and must not reset, revert, or overwrite them.
- `EV_REVOLUTION` and its mainline content are retained; the legacy `T_CAL_D60` trigger must remain disabled unless an explicit future requirement changes it.
- The previously removed `npc_a`, `npc_b`, `npc_c`, and `npc_d` test narratives must not be restored or used as formal content.
- Presentation validation must use the shared `Assets/Scenes/PresentationDebugScene.unity`; do not create a replacement debug scene, Canvas, or input system.

## Tasks

### Phase 1: Foundation
- [x] Task 1: Record the current git status, changed paths, and untracked paths as the preservation boundary before making any project edits.
- [x] Task 2: Read and reconcile `Docs/NcsIntegrationReport.md`, `Docs/NcsFormalDependencies.md`, `Assets/ConfigTables/Data/Narrative/NcsIntegrationQueue.csv`, and the latest NCS handoff reports into one event inventory.
- [x] Task 3: Inventory the authoritative Narrative/Text Excel sources, schema, Luban configuration, generated C# files, and generated JSON timestamps or hashes.
- [x] Task 4: Verify the availability and versions of Unity 6000.3.5f2, the official Luban tool, Python/openpyxl, .NET, and the project test/build entry points; record exact blockers if unavailable.
- [x] Task 5: Verify that `Assets/Scenes/PresentationDebugScene.unity`, its builder, controller, shared prefabs, and debug input bindings are present and loadable.
- [x] Task 6: Trace the current formal NCS runtime path from trigger selection through `NarrativeController`, `NarrativePresenter`, DayFlow, map actions, and presentation panels before changing code.
- [ ] Task 7: Inventory the real presentation resources, scene background keys, character assets, reading assets, and known placeholders for every formal story root.
- [ ] Task 8: Establish and verify the invariant that Revolution source rows, content, results, and reports remain present while `EV_REVOLUTION` and `T_CAL_D60` remain disabled.
- [ ] Task 9: Establish and verify the invariant that no `npc_a` through `npc_d` test narrative is restored, copied, or used as a formal trigger; remove nothing beyond explicitly identified test junk.
- [ ] Task 10: Build a per-event verification matrix covering trigger, prerequisites, presentation, cancellation, retry, save/restore, completion result, and downstream unlock for all formal roots and continuations.
- [ ] Task 11: Define evidence locations and naming for generation logs, Unity compile/test results, shared-scene captures, map captures, build reports, and blocker notes without touching user save slots.
- [ ] Task 12: Review existing save, DayFlow, map bootstrap, loading, and presentation ownership contracts and document the smallest integration surface for formal NCS flow.

### Phase 2: Source and Configuration Validation
- [ ] Task 13: Run the official `Assets/ConfigTables/GenerateAll.bat` against the current authoritative workbooks and capture its exit code and complete output.
- [ ] Task 14: Confirm that Player semantic validation passes for the unchanged or intentionally changed source workbooks.
- [ ] Task 15: Confirm that Luban strict validation and code/data generation complete successfully without hand-editing generated JSON.
- [ ] Task 16: Compare generated JSON and generated C# outputs against the source/schema inputs and record any stale, missing, or unexpected generated files.
- [ ] Task 17: Reconcile the generated event inventory, report count, and `NcsIntegrationQueue.csv` rows so every formal root has one authoritative status.
- [ ] Task 18: Validate the formal root set and explicitly classify demo, delivery-test, arrival-chain, and formal story events without deleting Revolution content.
- [ ] Task 19: Validate each formal event's declared entry type, source trigger, and phase handoff against the actual NCS runtime enum and queue behavior.
- [ ] Task 20: Validate all date/time/once conditions for day 2, day 3, day 4, day 6, day 8, day 9, and day 23 against the configured DayFlow phases.
- [ ] Task 21: Validate map-action names, venue/mount identifiers, map-active requirements, and action availability for `oct07.s1`, `neutral.1008.s1`, and `rose.1009.s1`.
- [ ] Task 22: Validate cross-event prerequisites use real `completed` facts and do not substitute known, seen, reserved, or merely opened state for completion.
- [ ] Task 23: Validate `onceOnly`, `blocksSave`, busy ownership, and candidate consumption semantics for every formal root and continuation.
- [ ] Task 24: Validate page/line/choice/check ordering, sequence continuity, required text keys, and absence of accidental empty or raw-key display text.
- [ ] Task 25: Validate `PresentationIndex`, visuals resources, reading resources, portrait bindings, effect presets, and fallback behavior for every configured presentation.
- [ ] Task 26: Validate the `scene.cafe` real-scene convention, explicit special cafe image keys, and restoration to `scene.cafe` after special composite shots.
- [ ] Task 27: Validate that the first-, second-, and third-floor operating base images are not used as dialogue backgrounds or `DialogueVisualsAsset.background` textures.
- [ ] Task 28: Validate inline ability checks, branch checks, and unknown dependency fallback behavior; unknown values must hide or block safely with diagnostics rather than inventing success.
- [ ] Task 29: Validate that formal results, relationship/bar changes, money changes, production facts, and completion flags are written only from authoritative configured results.
- [ ] Task 30: Validate incomplete, blocked, or partial source content remains isolated from formal completion and does not gain a fabricated ending or trigger.
- [ ] Task 31: Recheck the Revolution trigger guard after any generation or configuration change and prove the disabled `T_CAL_D60` state is preserved.
- [ ] Task 32: Recheck the NPC test-junk boundary after generation and prove no deleted `npc_a` through `npc_d` definitions or formal references reappear.

### Phase 3: Runtime Lifecycle Integration
- [ ] Task 33: Trace and, where needed, correct formal trigger dispatch so the same authoritative NCS controller owns candidate selection and event start.
- [ ] Task 34: Wire or verify map-action handoff with the active map, venue, action-point rules, and formal event candidate identity preserved.
- [ ] Task 35: Wire or verify date/phase dispatch for MorningBeforeMap, AfterBusiness, BeforeNight, and MapAction without bypassing DayFlow.
- [ ] Task 36: Verify formal cafe dialogue transitions through `GameFlowBootstrap.TransitionToGameplayScene` and prepares `CoffeeShop` before presentation when triggered from the map.
- [ ] Task 37: Verify loading UI, asynchronous scene readiness, presentation ownership, and failure reporting keep NCS state consistent during scene transitions.
- [ ] Task 38: Verify presentation resource resolution selects real NCS assets and applies the shared background/portrait/effect adaptation without introducing a parallel story system.
- [ ] Task 39: Verify `blocksSave` and busy/pause ownership prevent unsafe saves while a formal presentation or required handoff is active.
- [ ] Task 40: Verify cancellation closes the active panel, releases pause and effect handles, clears callbacks, restores the stage, and returns to the correct gameplay/map state.
- [ ] Task 41: Verify cancellation, scene exit, and interruption do not write `completed`, relationship, money, production, or other formal results.
- [ ] Task 42: Verify a cancelled formal event can be retried from its original valid trigger with no stale page, candidate, or once-only state.
- [ ] Task 43: Verify a loading or presentation failure reports a recoverable error and leaves the event retryable without partial settlement.
- [ ] Task 44: Guard against late callbacks, stale presenters, duplicate event starts, and old scene instances committing results after cancellation or restore.
- [ ] Task 45: Verify save/restore while a formal presentation is active either follows the existing supported boundary or safely aborts and requeues without corruption.
- [ ] Task 46: Verify save/restore after cancellation preserves the pre-event facts and leaves the event eligible for the same trigger.
- [ ] Task 47: Verify save/restore after a completed root preserves its completion and downstream unlocks without replaying or double-applying results.
- [ ] Task 48: Verify formal root settlement is exactly once across normal completion, repeated callbacks, reload, and duplicate input.
- [ ] Task 49: Verify queued continuation events resume from the correct page or event after gameplay, reading, checks, or choices complete.
- [ ] Task 50: Verify replaying an already completed event is rejected or safely hidden according to existing once-only semantics and never settles again.
- [ ] Task 51: Verify choices and ability checks remain transactional within the root event and are not committed early when a later page or gameplay handoff is cancelled.
- [ ] Task 52: Verify presentation effects, timeScale/F9 pause, camera, background, portrait, and overlay channels return to baseline after normal completion, cancellation, retry, and scene unload.

### Phase 4: Formal Event Flow Coverage
- [ ] Task 53: Exercise the day 2 `oct07.s1` cafe map action through its real trigger, complete all source lines, return to the map, and verify its root completion result.
- [ ] Task 54: Exercise `oct07.s1` cancellation, scene return, save boundary, retry, and exactly-once settlement using the real map path.
- [ ] Task 55: Exercise the day 2 `oct07.s2` BeforeNight entry, inline perception check, reading steps, and downstream meeting fact using the real DayFlow path.
- [ ] Task 56: Exercise `oct07.s2` cancellation, restore, retry, and completion settlement without falsely setting `story.oct09.meeting.known`.
- [ ] Task 57: Exercise the day 3 `neutral.1008.s1` market map action, option/check routing, completion, and return to the correct map state.
- [ ] Task 58: Exercise `neutral.1008.s1` cancellation, retry, save/restore, and completion boundary without introducing unsourced facts.
- [ ] Task 59: Exercise the day 4 `rose.1009.s1` formal map action and verify prerequisite, presentation, and completion behavior using only its authoritative content.
- [ ] Task 60: Exercise `rose.1009.s1` choices, perception/check routing, background/CG bindings, and tail-page behavior without borrowing another character identity or asset.
- [ ] Task 61: Verify `rose.1009.s1` blocked or unresolved dependencies remain explicitly blocked and cannot be mistaken for a formal completed event.
- [ ] Task 62: Exercise the day 6 `rose.1011.s1` MorningBeforeMap entry, production/reading/check handoffs, three-person presentation, and root completion.
- [ ] Task 63: Exercise `rose.1011.s1` cancellation, save/restore, retry, and stage-layout restoration without committing incomplete production or reading facts.
- [ ] Task 64: Exercise day 4 `church.1009.s2` after the real Rose prerequisite through the formal night entry and verify its root completion result and resources.
- [ ] Task 65: Exercise `church.1009.s2` cancellation, retry, save/restore, and downstream behavior without inventing money, relationship, or ending facts.
- [ ] Task 66: Exercise day 8 `revolution.1013.s2` AfterBusiness entry after the real `oct07.s2` prerequisite and verify formal completion.
- [ ] Task 67: Exercise all configured `revolution.1013.s2` choices and return paths, proving only the authoritative root result is committed at completion.
- [ ] Task 68: Exercise day 9 `revolution.1014.s1` MorningBeforeMap entry, checks, resource bindings, and Rose prerequisite using real saved state.
- [ ] Task 69: Verify unresolved or non-authoritative `revolution.1014.s1` dependency pages safely skip or block with a warning and never fabricate a relationship or check value.
- [ ] Task 70: Exercise day 23 `revolution.1028.s1` BeforeNight entry and verify the existing political-specialization mapping without inventing relationship values.
- [ ] Task 71: Exercise the `arrival.*` queued continuation chain and confirm branches, checks, retries, and final completion remain within the existing NCS event model.
- [ ] Task 72: Keep demo and delivery-test events available only through explicit debug/test entrances and prove they cannot become accidental formal map or date triggers.

### Phase 5: Integration Testing and Build Verification
- [ ] Task 73: Add or update focused configuration tests for event roots, disabled Revolution trigger, NPC test-junk boundary, background rules, and authoritative result ownership.
- [ ] Task 74: Add or update focused lifecycle tests for trigger, loading, busy/save gating, cancellation, retry, stale callbacks, restore, and exactly-once settlement.
- [ ] Task 75: Run focused presentation tests for backgrounds, portraits, choices, reading, effects, pause, cancellation, and stage reset.
- [ ] Task 76: Run the existing October 7 acceptance and map handoff tests, preserving their temporary-save isolation and recording exact results.
- [ ] Task 77: Run existing save/restore and player-runtime tests that cover NCS participants, DayFlow, map state, and formal history.
- [ ] Task 78: Force a Unity recompile/domain reload, confirm no compile errors, and record the actual compiler status rather than relying on `up_to_date` alone.
- [ ] Task 79: Open and run `Assets/Scenes/PresentationDebugScene.unity` for the shared baseline entrances and confirm no temporary test scene is involved.
- [ ] Task 80: Verify the shared debug image-to-real-scene-to-image background sequence, continuous paging, and explicit cafe background restoration.
- [ ] Task 81: Verify shared debug cancellation, replay, reading completion, option selection, and close/reopen behavior with no stale UI or callbacks.
- [ ] Task 82: Verify shared debug timeScale pause/resume and effect cleanup through F9, normal stop, cancellation, and replay.
- [ ] Task 83: Run the real day 2/day 3 map acceptance path, including cafe/market actions, AP/save boundaries, completion facts, and return state.
- [ ] Task 84: Run the real day 4 path for Rose and Church with prerequisite gating, night flow, resource loading, cancellation, and restore coverage.
- [ ] Task 85: Run the real day 6 path for Rose with production/reading/check handoffs, three-person layout, cancellation, retry, and save/restore coverage.
- [ ] Task 86: Run the real day 8/day 9 path for Revolution 1013/1014, including choices, checks, prerequisite facts, and no fabricated values.
- [ ] Task 87: Run or explicitly document the day 23 Revolution 1028 path; if blocked by missing authoritative data, record the exact human-only acceptance boundary.
- [ ] Task 88: Inspect Unity console, loading diagnostics, resource warnings, and presentation screenshots after focused and real-map runs; resolve regressions caused by this task only.
- [ ] Task 89: Prove all acceptance saves use isolated temporary slots and that user save slots, unrelated scenes, and unrelated generated assets were not changed.
- [ ] Task 90: Validate the complete playable sequence from a clean supported save through formal triggers, gameplay return, settlement, next-day unlock, and restore after each accepted root.

### Phase 6: Build Path, Evidence, and Handoff
- [ ] Task 91: Verify Build Settings include the required playable scenes and exclude `Assets/Scenes/PresentationDebugScene.unity` from the formal build.
- [ ] Task 92: Produce a development Player build for the installed target using the authoritative scene list and capture the complete build report.
- [ ] Task 93: Smoke-test the Player build's loading, formal trigger, presentation, cancellation, retry, save/restore, and completion path where the available runtime permits.
- [ ] Task 94: Regenerate the NCS report and queue after the final source/config state, update dependency and acceptance documentation with evidence, and list remaining human-only steps or blockers.
- [ ] Task 95: Update `.autonomous/ncs-formal-integration-overnight/progress.md`, mark only evidenced tasks complete, verify the task files and worktree boundary, and leave the next session with one highest-value unfinished task.
