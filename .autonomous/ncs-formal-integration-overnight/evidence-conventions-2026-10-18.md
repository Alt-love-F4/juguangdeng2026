# NCS Acceptance Evidence Conventions

Date: 2026-10-18
Scope: Task 11 of `ncs-formal-integration-overnight`
Status: Convention defined. This task created documentation only and did not create save data, screenshots, build outputs, scenes, or project assets.

## Evidence roots

| Evidence | Location | Naming rule | Required contents |
|---|---|---|---|
| Task audit notes and static inventories | `.autonomous/ncs-formal-integration-overnight/` | `<topic>-YYYY-MM-DD.md` | Scope, exact inputs, hashes or counts, tool result, status boundary, and next gap |
| Official generation output | `.autonomous/ncs-formal-integration-overnight/generation-YYYY-MM-DD-HHmmss.log` | One log per `GenerateAll.bat` invocation | Command, working directory, selected Luban/.NET paths, complete stdout/stderr, exit code, and output timestamps |
| Unity compile/reload result | `.autonomous/ncs-formal-integration-overnight/unity-compile-YYYY-MM-DD-HHmmss.json` | One record per forced recompile/domain reload | Unity version, recompile status, compile-failure flag, console ground truth, and any diagnostics |
| Focused Unity test result | `.autonomous/ncs-formal-integration-overnight/unity-test-<filter>-YYYY-MM-DD-HHmmss.json` | Use the exact test filter in the filename, sanitized to ASCII | Mode, assembly/filter, total/pass/fail/skip, duration, job id, and console status |
| Shared presentation captures | `Library/Locus/Screenshots/` | `ncs-formal-YYYYMMDD-HHmmss-shared-<entrance>-<stage>.png` | Shared scene path `Assets/Scenes/PresentationDebugScene.unity`, entrance key, stage, resolution, and capture source in the paired audit note |
| Real map captures | `Library/Locus/Screenshots/` | `ncs-formal-YYYYMMDD-HHmmss-map-<event>-<stage>.png` | Active formal scene, event id, day/phase, map/venue, and whether the capture is before, during, return, cancel, retry, or restore |
| Runtime logs and diagnostics | `Library/Locus/tmp/ncs-formal-integration-overnight/<run-id>/` | `<run-id>` is `YYYYMMDD-HHmmss-<event>` | Console export, loading diagnostics, state snapshots, and failure details for one isolated run |
| Temporary save slots | `Library/Locus/tmp/ncs-formal-integration-overnight/<run-id>/save-slots/` | Never use the user's configured save directory | Baseline service path, temporary slot names, capture/restore result, final cleanup result, and proof that the production service was restored |
| Development build report | `Library/Locus/tmp/ncs-formal-integration-overnight/<run-id>/build/` | `build-report-<target>-YYYYMMDD-HHmmss.json` | Target, scene list, profile/settings, output path, files, errors, warnings, and build exit/status |
| Human-only blocker note | `.autonomous/ncs-formal-integration-overnight/` | `blocker-<topic>-YYYY-MM-DD.md` | Exact missing authority or unavailable environment, attempted checks, safe boundary, and owner/next action |

## Run identity and pairing

Every runtime acceptance run uses one `run-id` for its save directory, console export, screenshots, and audit note. The audit note must list:

- Unity version and active build target.
- Scene path and whether the shared debug scene or a formal build scene was used.
- Event id, declared entry, official day/time phase, map venue/action when applicable.
- Save service replacement path and temporary slot names, or `no save`.
- Input sequence, cancellation/retry point, and expected result boundary.
- Result flags/bars before and after, `CanSave`, busy state, pause owner count, and active scene on return.
- Exact test/build job id, exit status, and whether the evidence is static, code-path, historical, or current runtime acceptance.

## Save-slot protection

Acceptance runs must redirect the save service to the isolated `Library/Locus/tmp/ncs-formal-integration-overnight/<run-id>/save-slots/` directory before any save or restore operation and restore the original service in a `finally`-equivalent cleanup path. The run must not write to the user's normal save directory, automatic slot, or existing manual slot. A run that cannot prove this isolation is a blocker, not acceptance evidence.

No user save slot was touched while defining this convention. Existing October 7 evidence used `Library/Locus/tmp/oct07-acceptance/`; that historical location remains evidence only and is not reused for new runs.

## Shared-scene rule

Presentation checks use the existing `Assets/Scenes/PresentationDebugScene.unity` and its documented keyboard entrances. No temporary scene, Canvas, input system, or replacement debug layout is permitted. Formal map checks use the existing playable scenes and the normal `GameFlowBootstrap`/DayFlow path; preview captures must be labeled as preview and cannot substitute for map acceptance.

## Retention and cleanup

- Keep the paired audit note and machine-readable test/build result for every accepted or blocked run.
- Keep screenshots only when they prove a visual state or regression boundary; name them with the same run id.
- Do not delete pre-existing screenshots, temporary scenes, logs, or user/Locus artifacts as part of this task.
- Do not commit `Library/` runtime evidence or generated Player output unless a later handoff explicitly requires an exported artifact.
- Record cleanup status rather than silently removing evidence.

## Current baseline

Existing static evidence already follows the task-directory `<topic>-2026-10-18.md` pattern: source/generation manifest, toolchain verification, presentation scene verification, presentation resources, runtime path trace, Revolution invariant, NPC boundary, and the formal event matrix. The formal build scene list remains the three configured playable scenes; `PresentationDebugScene.unity` remains excluded from Build Settings.
