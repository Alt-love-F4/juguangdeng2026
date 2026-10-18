# Task 24: Presentation Sequence and Text Validation

Date: 2026-10-18
Scope: generated Luban presentation data under `Assets/Settings/Resources/Luban/`, with the runtime page-condition grammar in `NarrativeController.ReadPresentationCondition`.

## Generated-data audit

The audit loaded the generated JSON with UTF-8 decoding and checked all configured conversation pages/lines, reading pages/lines, choices, ability checks, ability-check conditions, and referenced text rows.

| Area | Count | Result |
|---|---:|---|
| Presentation index entries | 25 | loaded |
| Conversation pages | 112 | loaded |
| Conversation lines | 915 | loaded |
| Reading pages | 3 | loaded |
| Reading lines | 11 | loaded |
| Choices | 43 | loaded |
| Ability checks | 11 | loaded |
| Ability-check conditions | 11 | loaded |
| Text rows | 2,011 | loaded |
| Referenced text usages | 970 | no missing references |
| Unique referenced text keys | 832 | no missing references |

Text checks:

- Missing referenced keys: `0`.
- Referenced text values that are empty or whitespace: `0`.
- Referenced text values equal to their raw key: `0`.

The reference audit includes conversation line text, non-empty reading titles, reading line text, and choice text.

## Sequence audit

Each group was sorted by its authored `seq` and compared against the contiguous positive range `1..N`:

- Conversation pages grouped by `presentationKey`: `0` violations.
- Conversation lines grouped by `pageId`: `0` violations.
- Reading pages grouped by `presentationKey`: `0` violations.
- Reading lines grouped by `pageId`: `0` violations.
- Choices grouped by `groupKey`: `0` violations.
- Ability-check conditions grouped by `checkId`: `0` violations.
- Non-positive sequence values: `0`.

The reading audit uses the authoritative relationship `readingLine.pageId == readingPage.pageId`. Corrected counts are:

- `oct07.s2.letter`: 7 lines, sequences `1..7`.
- `revolution.1014.s1.letter`: 1 line, sequence `1`.
- `rose.1011.s1.card`: 3 lines, sequences `1..3`.

Corrected formal-root page/line counts are:

| Event | Presentation | Pages | Lines |
|---|---|---:|---:|
| `oct07.s1` | `oct07.s1.conversation` | 2 | 40 |
| `oct07.s2` | `oct07.s2.conversation` | 7 | 34 |
| `neutral.1008.s1` | `neutral.1008.s1` | 9 | 60 |
| `rose.1009.s1` | `rose.1009.s1.conversation` | 9 | 67 |
| `rose.1011.s1` | `rose.1011.s1.conversation` | 27 | 123 |
| `church.1009.s2` | `church.1009.s2.conversation` | 4 | 36 |
| `revolution.1013.s2` | `revolution.1013.s2.conversation` | 6 | 52 |
| `revolution.1014.s1` | `revolution.1014.s1.conversation` | 23 | 171 |
| `revolution.1028.s1` | `revolution.1028.s1.conversation` | 10 | 144 |

## Page-condition grammar audit

The audit did not treat prefixed presentation expressions as literal entries in `tbncsconditionkey.json`. It used the grammar implemented by `NarrativeController.ReadPresentationCondition`:

- `choice:<choiceId>`: 32 expressions; every referenced choice ID exists.
- `anyChoice:<choiceId>;<choiceId>`: 3 expressions; every referenced choice ID exists.
- `check:<checkId>`: 5 expressions; every referenced check ID exists.
- Recursive `not:` wrappers: parsed through the same grammar with no malformed expression.
- `dependency:C-B06.history-specialization`: 1 known mapping; the runtime maps it to `specialization.player.letter.politics`.
- Unknown dependency expression: 1; the runtime warns once and returns `false`, so only the dependent page is hidden.
- Unprefixed authored condition keys: 4; every key exists in `tbncsconditionkey.json`.

Total page-condition expressions: `46`; grammar/reference errors: `0`.

Runtime behavior is covered by the passing `InlineChoiceResultTests` cases `UnknownPresentationDependencyHidesOnlyTheDependentPage` and `HistoryPresentationDependencyUsesExistingPoliticsSpecialization`, plus the inline choice/check routing tests.

## Unity verification

The connected Unity Editor is `6000.3.5f2`, stopped, not compiling, and has zero current console errors/warnings.

The available focused Test Runner job ran:

- `Spotlight.Narrative.Editor.Tests.NarrativeConfigurationValidatorTests`
- `Spotlight.Narrative.Tests.InlineChoiceResultTests`

Result: `35/35 passed`, `0 failed`, `0 skipped`. This includes generated Luban loading/validation, formal presentation report checks, inline check routing, choice/check condition parsing, and known/unknown dependency behavior.

The dedicated `Spotlight.GameFlow.Presentation.Tests` assembly is compiled and reflection-visible, but the current Test Runner discovery returns a synthetic `0/0` result for that assembly and its class filters. Direct invocation of the S1 presentation class also exposed an existing later-scope mismatch at `October07S1PresentationTests.cs:88`: the test expects `oct07.cafe.day` to resolve as a scene-background marker while the current registry entry is configured as approved artwork. No registry, source workbook, generated data, or test contract was changed for Task 24. This remains a presentation/background follow-up for the later Tasks 25-27 and 75-80.

## Conclusion

Task 24's requested generated-data checks pass: ordering and sequence continuity are complete, all referenced display text resolves to non-empty non-raw values, and all authored page-condition expressions are valid under the existing runtime grammar with fail-closed unknown dependencies.
