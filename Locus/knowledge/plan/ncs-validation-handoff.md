---
id: kd_b4bfc622-6354-4e2a-a438-925f6914a7bd
injectMode: inherit
injectAgents:
- unity
aiEditMode: inherit
---

# NCS Validation Handoff

## Goal

Build a Luban-driven Narrative Control System (NCS): narrative events, triggers, results, and presentation are authored in Luban Excel and exercised in an isolated test scene, without coupling the unfinished game loop.

Relation bars remain `-2..5` as confirmed by the user. Current configuration has 11 bars, 18 events, 12 triggers, 18 setups; all six relation bars validate as `-2..5`.

## Current Implementation

- Added isolated scene `Assets/Scenes/NarrativeTest.unity`; active Scene at handoff is this scene.
- Editor menu `Spotlight/Narrative/Open Test Scene` opens it; `Spotlight/Narrative/Create Test Scene` recreates it.
- Workbench `Assets/Scripts/Narrative/NarrativeTestWorkbench.cs` reads live Luban config; displays bar values and lets the tester adjust a bar, start/end a turn, reset the narrative session, or queue a configured event.
- `NarrativeRuntime.QueueEvent(eventId)` enters the normal controller queue and applies condition, lock, once-only, and busy checks.
- `onceType=once` now persists by trigger ID (`trigger.once.<triggerId>`), separate from event `onceOnly`, so separate triggers can share one event.
- Empty optional `opts` is accepted by editor validation; `A_CHOICE` is skipped when opts is empty, so linear dialogue does not hang on an empty choice panel.
- The scene uses `InputSystemUIInputModule` and a UI workbench.

## Verification Already Done

- Unity C# compile/domain reload succeeded.
- Luban runtime load/business validation succeeded: 18 events, 11 bars, 12 triggers, 0 validation errors.
- All six relation bars read `-2..5`.
- Play Mode smoke test in NarrativeTest: workbench/runtime/EventSystem initialized; `EV_TALK_CAFE` queued and presenter became active; no Console warnings/errors.
- Test runner was invoked broadly: full run 66 passed / 28 failed. Narrative Core + Editor run: 22 passed / 25 failed. Most loader failures came from stale test fixtures that omit required fields (`ArgumentNullException` while constructing generated Luban beans); existing failures also include a reversed relation expectation and an Editor test expecting `EV_WORLD_DAY3` to be the first date event while actual data order gives `EV_TALK_CAFE`. New test methods `ManualEventUsesConditionsQueueAndEventLifetime` and `TriggerOnceStateDoesNotLockSharedEvent` did not appear among failures, but the suite did not pass.
- Do not claim all Narrative tests are green until test fixtures and existing assertions are resolved.

## Manual Validation Procedure

1. In Unity, ensure Edit Mode and open `Assets/Scenes/NarrativeTest.unity` via `Spotlight > Narrative > Open Test Scene`.
2. Enter Play Mode. Confirm the workbench loads and the bar list includes current config values.
3. Trigger by Bar:
   - Choose a bar with an enabled Trigger in Luban (inspect `Assets/ConfigTables/Data/Narrative.xlsx`, Trigger sheet).
   - Start Turn, adjust the bar across its threshold using Add/Set, then End Turn.
   - Confirm the expected event presents, and that a multi-hit set is serialized by priority, one at a time.
4. Trigger manually:
   - Choose an event that has no unmet Condition and is not locked/onceOnly already consumed, such as `EV_TALK_CAFE` in a fresh Reset Session.
   - Click Play Configured Event. Confirm it enters the presentation layer and the workspace blocks input behind it.
5. Verify presentation data:
   - `EV_TALK_CAFE` has configured choices; test a locked/unlocked condition and selection/result behavior.
   - Select an event with empty `opts` (e.g. a TwoPersonTalk setup configured with `opts=`) and confirm it completes as linear dialogue without an empty choice panel/hang.
6. Turn behavior:
   - Start and end an empty turn; confirm `world.calendar` increments once.
   - Re-trigger an already fired once Trigger and confirm it does not fire; use a distinct Trigger referencing a shared event where available to verify independence.
7. Inspect Console for `[NCS]` errors/warnings. Reset Session between cases where `onceOnly` state matters.

Current workbench has event queue selection but does not currently expose Trigger metadata, choice requirement details beyond Presenter UI, or event force bypass. It should not bypass configured conditions or event lifetime rules.

## Remaining NCS Scope (Before UI Build)

The separate `Assets/事件系统.xlsx` proposes features not yet in current Luban schema/runtime:
- Add/remove dynamic condition-pool entries.
- Action-point state and add/sub/set operations (0..2 per day).
- Map position visibility, temporary positions, persistent/temporary property mutation/reset, and click-to-restore behavior.
- Generic UI-control-to-event binding/dispatch.

Current system already includes bars, flags/switches, lock/unlock events, date triggers, bar hints, NPC spawn/despawn and presentation setups. Decide/implement missing event-system features after the user validates current NCS semantics; don't conflate this with presentation UI redesign.

## Presentation UI Follow-up (Deferred Until NCS Validation)

`Assets/叙事性界面.xlsx` asks for distinct Dialog, Reading, Notification behavior. Existing `PresentationUI.cs` already has full-screen overlay, portrait images, text reveal, paging/continue, choices, requirement locks, scroll views, background, fade, and effect atoms. Remaining gaps include explicit dialog-vs-reading mode, speaker name and reading illustration layout/config, dedicated single-page notification with optional date/art/bar changes, choice hover/click animation and centering, and narrow viewport/layout acceptance. Do not alter presentation implementation before the user confirms NCS.

## Scope / Cautions

- User-authored sheets currently exist as untracked files: `Assets/游戏叙事控制系统 (新).xlsx`, `Assets/事件系统.xlsx`, `Assets/叙事性界面.xlsx`; preserve them.
- Workspace also has a pre-existing modified `Locus/workspace-trees/default.json`; do not overwrite/revert it.
- Editor/resource activity previously reserialized two SimHei font assets; they were restored, and should remain untouched.
- Test scene currently exists and is not in Build Settings. Avoid modifying gameplay scenes or enabling narrative game-flow module.
