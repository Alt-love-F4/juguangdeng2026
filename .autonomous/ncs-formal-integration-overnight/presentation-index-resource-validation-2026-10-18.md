# Presentation Index and Resource Validation

Date: 2026-10-18
Scope: Task 25 of `ncs-formal-integration-overnight`
Status: Completed with documented presentation-asset boundaries. No generated JSON was hand-edited.

## Source correction

The authoritative `OpeningPresentation` sheet in `Assets/ConfigTables/Data/Narrative/Narrative.xlsx` had the old `Dialogue/FirstDayIntroductionVisuals` resource on `first_day_cafe` and all 24 `cafe_tutorial.*` rows. The project presentation convention requires these openings to use the scene-backed `Dialogue/CafeSceneVisuals` asset, whose `backgroundKey` is `scene.cafe`.

Exactly 25 source rows were changed from `Dialogue/FirstDayIntroductionVisuals` to `Dialogue/CafeSceneVisuals`. The old asset was preserved. The official generator was then run:

- Log: `generation-20261018-task25.log`
- Exit code: `0`
- Player semantic validation: passed
- Luban schema/data load, validation, C# generation, and JSON generation: passed
- Generated artifact changed: `Assets/Settings/Resources/Luban/tbopeningpresentation.json` only
- Generated C# output: unchanged

## Runtime resource checks

The connected Unity Editor is `6000.3.5f2`, outside Play Mode, and loaded the refreshed generated data. Direct runtime validation through the existing loader and presentation adapters reported:

- `PresentationIndex`: 25 unique entries; 22 Conversation and 3 Reading entries.
- Conversation data: 112 pages and 915 lines loaded from generated tables.
- Reading data: 3 pages and 11 lines loaded; all configured reading `imageKey` values are empty, so no missing reading image resource is hidden by a fallback.
- Conversation background resolution: 109 non-empty inherited/page keys resolved with 0 missing-resource errors: 105 artwork entries, 1 explicit solid-color entry, and 3 explicit placeholder entries.
- `scene.cafe`: resolved as a scene marker with no texture, no solid color, and no placeholder flag.
- Opening visuals: all 25 opening `visualsResource` values load; the corrected cafe/tutorial rows resolve to `Dialogue/CafeSceneVisuals`; the scene marker is valid.
- Effect library: `Dialogue/PresentationEffectLibrary` loads. Both authored line effect references resolve, and the required `fx_portrait_nod` and `fx_background_pan` presets are present and valid.
- Prefabs: `DialoguePanel`, `ReadingPanel`, and `FirstDayLetterPanel` load from Resources with their expected component types; no prefab resource errors were reported.
- Conversation adapter: 106 ordinary conversation pages built through `NarrativeConversationEventPlayback.CreateScript` without resource exceptions.

The six pages not passed to the ordinary conversation adapter are intentional handoff pages: four LetterReveal/LetterRead pages, the Rose production gate, and the Rose card-read step. They are consumed by the existing letter/production flow and are not missing dialogue content.

## Portrait binding and fallback behavior

The runtime catalog was loaded from the generated NCS object table. Across all indexed presentations there were 221 on-stage portrait references:

- 141 resolved to a configured base portrait.
- 147 authored expression usages use the existing base-portrait fallback when a specific expression sprite is absent; this is the resolver's explicit fallback behavior, not a borrowed character asset.
- 80 references have no configured base portrait. They are limited to the known `player` arrival-chain entries and `sorel` lines in `revolution.1014.s1`. No substitute portrait was invented or copied. The formal C14 Sorel art gap remains an acceptance boundary.

## Test and diagnostic evidence

- `Spotlight.Narrative.Editor.Tests`: 28/28 passed, 0 failed, 0 skipped after the source/generation refresh.
- `Spotlight.GameFlow.Presentation.Tests`: Unity's connected Test Runner still returns a synthetic `0/0` result for the assembly and exact class filters, even though reflection sees the compiled assembly and 269 NUnit/Unity test methods. This is a runner discovery limitation, not evidence of a passing presentation suite.
- Current Unity console read after validation: 0 error entries and 0 warning entries.
- The existing `October07S1PresentationTests` expectation that `oct07.cafe.day` should simultaneously be approved artwork and `useSceneBackground=true` remains unresolved. The background convention decision belongs to Task 26; this task records it rather than changing the registry or test contract.

## Preservation boundary

Project paths intentionally changed by this task:

- `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`
- `Assets/Settings/Resources/Luban/tbopeningpresentation.json`

Task evidence added:

- `.autonomous/ncs-formal-integration-overnight/generation-20261018-task25.log`
- `.autonomous/ncs-formal-integration-overnight/presentation-index-resource-validation-2026-10-18.md`

No scene, Canvas, input system, save slot, portrait asset, background asset, generated C# file, or unrelated user/Locus path was changed by this task.

