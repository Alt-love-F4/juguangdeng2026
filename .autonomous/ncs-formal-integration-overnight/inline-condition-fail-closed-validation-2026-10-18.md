# Task 28: Inline Checks, Branches, and Unknown Dependency Fallback

Date: 2026-10-18

## Generated condition inventory

The generated `tbncsconversationpage.json` contains 42 pages with non-empty `pageCondKeys`:

- 32 `choice:` conditions
- 3 `anyChoice:` conditions
- 5 `check:` conditions
- 2 `dependency:` conditions
- 4 plain authored condition keys

All choice, any-choice, and inline-check references resolve to generated choice/check definitions. The formal condition validator and generated-data loader pass without condition-configuration errors.

The generated `tbncsabilitycheck.json` contains 11 checks: 9 `Inline` checks with no success/failure branch events, plus the existing branch-mode checks `first_day_perception` and `arrival.perception`. No inline check is configured with branch events, and the focused validator test rejects attempts to add branch events, SetupParams consumers, or both Choice and PassiveCheck consumers to one inline check.

## Runtime fail-closed behavior

The existing `NarrativeController.ReadPresentationCondition` contract was inspected and exercised:

- `choice:` reads only the active presentation's selected choice state.
- `anyChoice:` is true only when one listed choice is selected.
- `check:` throws when the inline check has not been evaluated; after evaluation it returns the cached pass/fail result.
- `not:` is applied to the evaluated condition result, so a failed check or dependency does not become success by omission.
- The known dependency `C-B06.history-specialization` maps to the existing `specialization.player.letter.politics` condition and follows that condition's value.
- The unknown dependency `C-B03.C-B04.C-B05.reciprocity` returns `false` and emits `[NCS] Presentation dependency unavailable; dependent page will be skipped: ...`.
- Unknown plain presentation condition keys throw `InvalidOperationException` rather than returning true.
- Invalid or unknown runtime condition rows throw `InvalidOperationException`; they are not treated as satisfied.

This means unknown dependencies hide/block only the dependent page and cannot invent a successful branch or formal result.

## Test evidence

Connected Unity `6000.3.5f2`, EditMode:

- Focused `InlineChoiceResultTests`: 4/4 passed, 0 failed, 0 skipped:
  - `ChoiceReferencedInlineFailureAppliesFailureOwnerOnly`
  - `HistoryPresentationDependencyUsesExistingPoliticsSpecialization`
  - `UnknownPresentationDependencyHidesOnlyTheDependentPage`
  - `ValidatorAllowsChoiceOnlyInlineAndRejectsOtherConsumers`
- Full `Spotlight.Narrative.Core.Tests`: 195/195 passed, 0 failed, 0 skipped.
- The full suite emitted expected negative-test diagnostics, including the unknown-dependency warning and a rejected external-result transaction. Those entries were inspected and the Unity console was cleared afterward.

## Boundaries

- No new condition syntax, fallback success path, branch event, result owner, or parallel story field was introduced.
- The authoritative workbook and generated JSON were not changed by this task.
- This task proves configuration and controller fail-closed behavior; it does not claim real-map acceptance or formal event completion settlement.

Status: Task 28 evidence complete; Task 29 remains open.