# Runtime Presentation

Presentation configuration comes from standard Luban Excel, generated C# + JSON, and `LubanNarrativeLoader.Load()` returning a plain DTO. No Narrative ScriptableObject or custom XLSX import is used. Preserve Excel's first four rows (`##var`, `##type`, `##group`, `##` descriptions); author data from row 5. All narrative text is in shared `Assets/ConfigTables/Data/Shared/Text.xlsx`. Run `Assets/ConfigTables/GenerateAll.bat` for cs-json in `Assets/Scripts/Config/Generated` and `Assets/Settings/Resources/Luban`, then Validate Luban after generated code/import completes.

The nine recommended templates are `Tpl_TwoPersonTalk`, `Tpl_MonoTalk`, `Tpl_Narration`, `Tpl_WorldBroadcast`, `Tpl_LetterRead`, `Tpl_Timeskip`, `Tpl_CutScene`, `Tpl_ChoiceOnly`, and `Tpl_BarMilestone`. They use the same runtime stage and ordered atom dispatcher; they are not separate scene or prefab dependencies.

`atomSeq` is comma-separated. `SetupParams.slotValues` is comma-separated `name=value`; lists use `|`. Parsed `parameters` override the string assignment for the same slot. Every template atom reads the shared setup slots. Resource paths are relative to `Resources`, with no extension. Assigned resources that cannot be loaded are logged as errors and displayed as notices; they are never replaced with invented assets. Optional empty backgrounds/portraits require no assets. The supplied tea/date sample uses shared TbText content only, not final art.

| Atom | Slots And Behavior |
| --- | --- |
| `A_DIALOG`, `A_NARRATE` | `lines=key1|key2` or `textKey`; otherwise event `contentKeys`. Optional `titleKey` overrides the event title. Reveal one line at a time; click, Space or Enter reveals a typing line, then advances on a separate input. Long text is scrollable. |
| `A_BG` | Optional `bg` sprite; empty leaves the event stage empty. |
| `A_PORTRAIT` | Optional `a`, `b` sprite paths; `portrait` aliases `a`. `posA=left`, `posB=right` accept left/right/center. `fade` seconds defaults to 0. Empty paths hide that portrait. |
| `A_CHOICE` | `opts` is a Choice.groupKey. Ordered vertical scrollable options show text keys; locked options are disabled. Space, Enter, playback skip and autoAdvance cannot commit a choice. Restored selected choices are reused. Empty optional opts is omitted; ChoiceOnly without opts fails closed. |
| `A_WAIT` | `duration` seconds defaults to 0. `untilClick=false`; true requires a fresh submit. Uses unscaled time. |
| `A_CAMERA` | `zoom` positive multiplier defaults to 1, `shake` world-unit amplitude defaults to 0, `duration` seconds defaults to 0.3. Uses Camera.main, orthographic size or perspective FOV, restores the original camera on completion/cancel/reset. |
| `A_SFX` | Required `sfx` AudioClip path, `volume=1`, `delay=0` seconds. One-shot playback. |
| `A_BGM` | `bgm` AudioClip path, `volume=1`, `fadeIn=0`, `fadeOut=0` seconds. Loops until replaced/stopped/reset. Empty bgm fades out and stops current music. |
| `A_FX` | Required `fx` prefab path, `duration=1` seconds. Instantiates the prefab, starts particle systems, and destroys the instance after the bounded lifetime or cancellation. |
| `A_FADE` | `type=out` fades the stage to black; `in` fades from black; `black` ends black. `duration=0.3` seconds. Dialogue/options remain readable above the stage fade. |
| `A_BAR_HINT` | `barKey`, `delta`, optional `textKey` override and `duration=1` seconds. Visible hint, not a state mutation. |

`interruptType=progressive` never skips events; ESC only reveals the current typing line. `interruptType=playback` allows ESC to skip non-choice presentation atoms when setup `skipAble` is true (template default applies without a setup). Completion still calls the controller exactly once, so results settle normally. Setup `autoAdvance` only affects dialogue, not choices. All atom order, timer, input and resource logic lives in `NarrativePresenter`, `PresentationUI`, `PresentationResources`, and `PresentationSlots`.

Result `PLAY_SETUP` queues another template sequence. `INarrativePresentationDrain` keeps the core from advancing to the next event/finishing the turn until queued sequences finish. A PlaySetup with nonempty choice opts is rejected by configuration validation: choices require an active event transaction. Snapshot pendingEffects replay queued setup presentation without re-applying settled results.

## Runtime API

`NarrativeRuntime.Initialize(configuration)` initializes once; `Initialize(configuration, presenter, resolver)` deliberately replaces the session with an injected presenter. `ResetSession(configuration)` cancels previous presentation/drain callbacks and creates a fresh default presenter/controller. `BeginTurn`, `AddBar`/`SetBar`, and `EndTurn` are the gameplay action boundary. `CaptureSnapshot` keeps core active/pending state; `RestoreSnapshot` replaces the old presenter, restores state/NPC locations and pumps only a pending/ending turn. Active presentation replays from its beginning, not from a text character index.

`NarrativeGameFlowIntegration` creates persistent runtime objects without editing scenes/prefabs. Gameplay visibility is controlled by GameFlow, not by scene name. The sample HUD raises `npc_chen.trust` by one for tea and completes an empty turn for rest; the core advances the calendar once. Buttons are disabled during narration, loading, pause and menus/modals.

`NarrativeSaveBridge` stores a version-1 envelope in `GameSaveData.Participants` under `spotlight.narrative`. Missing participants are supported legacy saves and explicitly start a fresh narrative session. Empty JSON, corrupt JSON, duplicate participants, missing snapshots and unknown versions block loading. Capture failures block saving rather than dropping NCS data. Manual, quick, auto, save-and-exit and save-and-title use the same capture path.

## Acceptance

1. In Edit Mode, run `Spotlight.GameFlow.Narrative.Tests`, then the Narrative core/editor tests. The migration baseline passed 65 tests and the tea/date/snapshot Play Mode regression; rerun checks after configuration changes.
2. Start a new game in the existing SampleScene; drink tea until trust crosses 2. Check two text lines, reveal/advance separation, locked/accepted choices and the choice result. Try Space/ESC during choices; neither may bypass selection.
3. Rest to day 3 and verify the date-only world broadcast and one calendar increment per turn. Open pause/settings/save screens; HUD actions must be disabled. Return to title and start again in the same scene: day/trust/playtime reset.
4. Save during an active event through `TryCaptureSaveData`, reload, and verify active-first replay, saved choices, pending events and pending PlaySetup effects. Invalid participant data must report a load error and preserve the current session.
5. Assign each optional atom a real resource path and inspect camera zoom/shake restoration, looping/fading music, delayed SFX, stage fade, separate portraits and FX destruction. Assign one nonexistent path and verify both Console error and visible notice. The sample does not pretend to supply final art/audio.
6. Check 1920x1080 and a narrow viewport: long dialogue can scroll, long/many choices remain readable and scrollable, and no click reaches gameplay behind the full-screen blocker.
