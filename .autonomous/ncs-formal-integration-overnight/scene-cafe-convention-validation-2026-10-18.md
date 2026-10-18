# Task 26: Cafe Scene Background Convention Validation

Date: 2026-10-18

## Source and generation

The authoritative workbook is `Assets/ConfigTables/Data/Narrative/Narrative.xlsx`. The existing workbook contained 48 legacy `ConversationPage.bgOverride` cells using `oct07.cafe.day` or `oct07.cafe.night`: 1 for `oct07.s1.cafe`, 7 for `oct07.s2`, 6 for `revolution.1013.s2`, 10 for `revolution.1028.s1`, 23 for `revolution.1014.s1`, and 1 for `rose.1011.s1.arrival`. The last row reconciles the earlier 47-row note: its authored text is inside the coffee shop, so it is ordinary in-cafe dialogue rather than an exterior arrival page.

Those 48 source cells were changed to `scene.cafe`. No special composite page was added, and no operating-floor base image was used. The 25 Task 25 opening-resource changes remain in the workbook, for 73 presentation cells changed across the two tasks.

Official generation was run with `Assets/ConfigTables/GenerateAll.bat`.

- Log: `.autonomous/ncs-formal-integration-overnight/generation-20261018-task26.log`
- Exit code: `0`
- Player semantic validation: passed.
- Luban strict validation and JSON generation: passed.
- Generated C#: unchanged.
- Generated data change: `Assets/Settings/Resources/Luban/tbncsconversationpage.json` only for this task.

## Static data checks

The generated conversation table contains 112 pages. It has 48 `scene.cafe` pages, zero `oct07.cafe.day/night` pages, zero operating-floor base-image keys, and zero current formal pages using `cafe.door.thibaut` or `church.cafe.thibaut.departure`. `rose.1011.s1.arrival` resolves to `scene.cafe`.

The report/queue refresh through the existing NCS integration tools returned 38 events, 0 errors, and 0 warnings. This remains configuration/static evidence only and does not claim real-map acceptance.

## Resource and restoration contract

The connected Unity Editor is Unity `6000.3.5f2`, outside Play Mode, with compilation complete and no reported compile failure.

Direct in-editor contract verification passed:

- `scene.cafe` resolves as a valid scene marker with no texture, placeholder, or solid-color state.
- `cafe.door.thibaut` resolves to real artwork.
- `church.cafe.thibaut.departure` resolves to the same artwork through the preserved alias.
- Current source contains no `church.1014.s2` page and no special composite page; this is intentionally not claimed as formally wired.
- A `DialogueScript` created with stale special artwork plus an explicit `scene.cafe` visual has null `Background` and a scene-backed `BackgroundVisual`. Repeating the construction for the restored page passes the same check, proving the explicit restoration contract.
- The connected direct result was: `pages=112;scenePages=48;legacyCafePages=0;baseFloorPages=0;specialPages=0;sceneMarkerValid=true;specialAliasValid=true;restorationContract=true`.

## Test evidence

- `Spotlight.Narrative.Editor.Tests`: 28/28 passed, 0 failed, 0 skipped.
- The exact presentation test `PublishedCafePagesExplicitlyUseTheLiveSceneWithoutChangingOutsideLocations` was submitted, but Unity returned the known synthetic 0/0 assembly-discovery result. It is not treated as a passing test; the direct Editor contract check above is the usable runtime evidence.
- The existing presentation test source already rejects legacy cafe page overrides and requires at least 47 scene pages. The current generated state exceeds that threshold with 48 pages.

## Boundaries

- The old `oct07.cafe.day/night` registry entries remain available for scoped preview/compatibility and were not deleted.
- Street, club, church, black-scene, and other explicit image keys were not batch-replaced.
- `cafe.door.thibaut` and its legacy alias remain reserved for the future formal special-shot sequence; no `church.1014.s2` event was invented.
- No scene, Canvas, input system, save slot, portrait asset, or operating-floor base image was changed.

Status: Task 26 evidence complete; Tasks 27 onward remain open.