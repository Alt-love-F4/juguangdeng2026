# NCS runtime integration handoff — 2026-10-19

## Ownership and status

Read `Tools/Narrative/Content/INTEGRATION_CONTRACT.md` before editing. This worker implemented extensions **inside the existing NCS**. It did not write source Excel, generated C#/JSON, GameFlow, FreeCafe, or content Python modules; it did not edit `NarrativeActionController.cs` / `NarrativeDailyContent.cs`, which belong to Main. Concurrent changes to those files are not this worker's changes.

Runtime/schema code is implemented and compile-checked. This is **not** a claim that every story root, real coffee host, or full Unity playthrough has passed. Main owns the source-column merge, official Luban generation, host binding, and Unity integration tests. During the follow-up bounded review the generated GameplayKey/NextPageId fields were present; this worker compiled those actual current generated sources without placeholders, but did not run generation or a full Unity playthrough.

## Exact host API

Both `NarrativePresenter` and `NarrativeRuntime` expose:

```csharp
public Func<NarrativeEventRequest, NarrativeGameplayPresentationData,
    Action<NarrativeGameplayResult>, IDisposable> GameplayPlayback { get; set; }
public bool IsAwaitingGameplay { get; }
```

DTOs in `NarrativePresentationDefinitions.cs`:

```csharp
public sealed class NarrativeGameplayPresentationData
{
    public string pageId, gameplayKey, eventId;
    public object continuityOwner;
}
public sealed class NarrativeGameplayResult
{
    public bool passed, cancelled;
}
```

- Bind the delegate before requesting any root with a Gameplay page. The runtime property forwards changes to the active presenter and is retained when that runtime recreates its presenter.
- The delegate must start the **existing real coffee interaction** and return a **non-null IDisposable**. Dispose means cancel/unsubscribe/restore only that request's host resources. Do not call completion merely because the host was opened.
- The callback may be synchronous or asynchronous, **on the Unity thread**. The first callback wins. `passed=false, cancelled=false` is a real completed unsuccessful attempt; resume to guarded failure content. `cancelled=true`, a null result, a null handle, a thrown exception, host interruption, or disposal must not complete the root.
- NCS treats `gameplayKey` as opaque. The host owns parsing/validating the agreed `serve:<productId>:<minimumScore>:<count>[+...]`, catalog membership, accepted served-product facts, deduplication, and all real coffee state. No second order/inventory/coffee implementation exists here.
- Before invoking the host, the presenter clears its own conversation/letter release callbacks, hides its own fallback UI, restores its camera/music ownership, and suspends **only its own** `PresentationPauseRegistry` token. It retains the root transaction and the continuity identity. Unrelated owners/tokens are untouched.
- A valid host outcome is latched, the host handle disposed once, the presenter's pause reacquired, and only then the root-local outcome is published and dialogue traversal resumes. The same stable pause wrapper is used so the resumed token is disposed when the original coroutine ends.
- Session versions plus per-request identity reject callbacks after cancel/dispose/retry. Handles returned after synchronous cancellation are immediately disposed. Cleanup during iterator disposal catches host-dispose exceptions so it cannot strand the presenter's pause lease.
- The host must prepare the required cafe scene through the existing formal path. This presenter still treats unloading its owning scene as host interruption; scene preparation is not silently fabricated here.

### Input, save, and DayFlow gates

`NarrativeRuntime.BlocksGameplayInput` is now:

```csharp
!IsAwaitingGameplay && (IsPresenting || Controller.IsBusy)
```

Null-controller checks remain in the actual implementation. `IsPresenting` and `Controller.IsBusy` **stay true** for the pending root. `NarrativeRuntime.BlocksSave` additionally remains true during a Gameplay wait; the formal roots also keep their existing `blocksSave=true` declaration. The legacy Narrative HUD explicitly requires `!Controller.IsBusy`, so its navigation/end-day controls are not enabled by the coffee-input exception.

**DayFlow/FreeCafe integrator:** yield only the coffee-world interaction gate using `runtime.IsAwaitingGameplay` / the real host request lifetime. Do not relax busy checks for map/day/story entry, other result transactions, or save/load. Release retained DialoguePanel/letter adapters through their existing release callbacks; do not globally close unrelated UI or clear the pause registry.

## Source/schema and loader contract

Main owns source workbooks and official generation. This worker updated `Assets/ConfigTables/Defines/schema.xml`:

1. ConversationPage `stepType` accepts existing types plus `End`, `Gameplay`.
2. Append source columns **`gameplayKey`, then `nextPageId`**, after existing `checkId`. `nextPageId` is an optional reference to `TbNcsConversationPage`.
3. Condition `ownerType` adds `result`.
4. Result `resultType` adds `TRAIN_ATTRIBUTE`.

The loader directly uses generated `row.GameplayKey` / `row.NextPageId`. Main has now supplied these fields in the current generated source, and the follow-up compile check used them directly. No compatibility reflection or generated-code edits were added by this worker.

### Page traversal

- Existing linear sequence sorted by `seq` remains the default.
- An eligible page's nonempty `nextPageId` selects its next page within the **same** presentation. A hidden/guard-false page does not take its edge; it falls through to the next linear page. A failed PassiveCheck also falls through linearly without taking its content edge.
- `End` is a silent successful end of this root and stops trailing presentation atoms and later branch pages. Root results still settle normally exactly once. End may be guarded; a hidden End does not stop another branch.
- `Gameplay` contains no dialogue/options, requires a key, and waits for the host as above. End and Gameplay preserve existing other step types.
- A root-local cursor permits bounded graph revisits but aborts explicit loops after `max(pageCount, min(16384, max(128, pageCount * 8)))` examined nodes. Multiplication uses 64-bit arithmetic; valid linear roots are not rejected merely for exceeding 16,384 pages. Explicit page edges use an indexed lookup, not repeated linear searches. Invalid/cross-presentation edges fail explicitly. Author a deliberately repeatable gameplay retry loop only with a reachable exit; unbounded graph execution is not supported.
- Playback skip is disabled for presentations containing Gameplay, preventing skip from manufacturing a completed gameplay request.

### Guards and conditional settlement

Existing guards remain: declared bool key, `choice:<id>`, `anyChoice:<id>;<id>`, `check:<InlineCheckId>`, `not:<guard>`, and existing construction-dependency behavior. Page `pageCondKeys` uses pipe/comma AND as before. New guards:

```text
bar:<barKey>:ge|gt|le|lt|eq|ne:<integer>
gameplay:<pageId>:passed
not:gameplay:<pageId>:passed
```

Numeric operands are invariant-culture signed integers. Missing numeric providers or an unexecuted Gameplay outcome fail closed with a once-per-key diagnostic: **even `not:` or expected-false cannot turn unavailable data into success**. An actually completed failed attempt is available and can select the failure branch. Guard nesting is bounded at 64; malformed or unknown declared expressions fail explicitly.

Gameplay outcomes live in a controller-owned **root-local dictionary**, not persistent/global story flags. Success/cancel/restore/GM cleanup clears it appropriately. Diagnostic active snapshots copy outcomes into `NarrativeSnapshot.gameplayResults` only for that active root, and restore validates that the page belongs to it. Replaying a Gameplay page still requires a new real host request; snapshots do not auto-complete coffee. Formal save restrictions remain enforced by host save gates.

A conditional result uses:

```text
Condition.ownerType = result
Condition.ownerId   = <Result.resultId>
Condition.condType  = BOOL_EQ
Condition.target    = <presentation guard>
Condition.value     = true / false
```

Existing group semantics apply (AND within group, OR across groups). All selected root/choice/check result guards are evaluated **before any result mutates a bool/bar and before selected choices/check snapshots/gameplay outcomes are cleared**. Only the filtered batch is preflighted and committed. The loader/player/editor validators accept these rows; diagnostics use the same transient guard reader. Non-BOOL_EQ condition types for result ownership are rejected by configuration validation.

Action-owned `BOOL_EQ` may also use **read-only world guards**: declared bool keys, numeric `bar:<key>:<op>:<int>` guards, and `not:` nesting. The runtime, loader validator, and diagnostics share the same semantics, including fail-closed missing providers. Action guards explicitly reject root-local `choice:`, `anyChoice:`, `check:`, `gameplay:`, and construction `dependency:` expressions; event/choice/ending condition semantics were not broadened. No DNF rewrite is needed for Main's action `not:bar:` location locks. Page bool guards read the current mutable world facts when each page is visited; this does **not** apply choice-result effects early. Conditional root effects still freeze their bool/choice/bar/check/gameplay evidence before any result mutation.

## TRAIN_ATTRIBUTE

Canonical source target is `player.<attribute>`, with `language`, `courage`, `wisdom`, `perception`. Bare attribute IDs and `player:<attribute>` are also accepted for existing programmatic callers. `value` is **positive integer XP**, never a float.

Main must provide four persistent NCS EventDriven bars:

```text
training.language / training.courage / training.wisdom / training.perception
min=0, max=9, initValue=0
```

These store only the XP remainder. The already-centralized `player.<attribute>` bars remain the sole attribute values, writable NCS bars with max 20. Example: current attribute 9, remainder 9, reward 2 => attribute 10, remainder 1. Each ten XP raises that attribute by one; at 20 excess XP and the remainder are discarded. There is no second attribute store and no independent PlayerProfile mutation.

Training results are expanded transaction-locally into ordinary writes of those two existing bars, after guard filtering and before the existing entire-batch preflight. Multiple rewards and earlier ordinary attribute writes are simulated in result order. Invalid/missing bars, invalid/nonpositive/overflowing integer reward strings, and out-of-range training state reject the batch **before any training/ordinary result commits**. The existing money/inventory/result transaction infrastructure remains authoritative; no second transaction system was created. Observers receive the normal central bar changes and expanded BarSet effects.

For Main's daily rule, use two result rows with the guards:

```text
bar:player.<attribute>:lt:10 => TRAIN_ATTRIBUTE value=2
bar:player.<attribute>:ge:10 => TRAIN_ATTRIBUTE value=1
```

Both guards observe the same pre-mutation value, so crossing 10 during settlement cannot double-award.

Editor range validation now recognizes those four training remainder bars at 0..9 and NCS EventDriven `faction.*` / `*.affection` at -20..20. Main's zero baselines and -20..20 social ranges remain **provisional integration defaults**, not asserted original-author numeric authority. Ordinary bar validation is not globally relaxed.

## Changed paths (this worker only)

Existing files:

- `Assets/ConfigTables/Defines/schema.xml`
- `Assets/Scripts/Narrative/LubanNarrativeLoader.cs`
- `Assets/Scripts/Narrative/LubanNarrativeLoader.ConversationSteps.cs`
- `Assets/Scripts/Narrative/NarrativeDefinitions.cs`
- `Assets/Scripts/Narrative/NarrativePresentationDefinitions.cs`
- `Assets/Scripts/Narrative/NarrativeController.cs`
- `Assets/Scripts/Narrative/NarrativeController.Entries.cs`
- `Assets/Scripts/Narrative/NarrativeController.Diagnostics.cs`
- `Assets/Scripts/Narrative/NarrativePresenter.cs`
- `Assets/Scripts/Narrative/NarrativeRuntime.cs`
- `Assets/Scripts/Narrative/NarrativeGameplayHUD.cs`
- `Assets/Scripts/Narrative/Editor/NarrativeConfigurationValidator.cs`

New C# files (with Unity metadata):

- `Assets/Scripts/Narrative/LubanNarrativeLoader.Training.cs`
- `Assets/Scripts/Narrative/NarrativeAssemblyInfo.cs` (friend access for bounded-cursor unit tests)
- `Assets/Scripts/Narrative/NarrativeController.Gameplay.cs`
- `Assets/Scripts/Narrative/NarrativeController.Training.cs`
- `Assets/Scripts/Narrative/NarrativeConversationTraversal.cs`
- `Assets/Scripts/Narrative/NarrativePresentationGuards.cs`
- `Assets/Scripts/Narrative/NarrativePresenter.Gameplay.cs`
- `Assets/Scripts/Narrative/Tests/NarrativeRuntimeExtensionsTests.cs`
- `Assets/Scripts/Narrative/Editor/Tests/NarrativeRuntimeIntegrationValidationTests.cs`
- `Assets/Scripts/Narrative/Tests/Gameplay/NarrativeGameplayPresenterTests.cs`
- `Assets/Scripts/Narrative/Tests/Gameplay/Spotlight.Narrative.Gameplay.Tests.asmdef`
- `Docs/NarrativeRuntimeIntegrationHandoff.md`

## Tests actually run

### Executed: portable core regression (120 passed, 0 failed; follow-up review)

A temporary .NET 8 harness outside the repository compiled the actual controller/state/loader/guard/training/cursor/editor-validator sources and the existing NUnit tests. Only Unity engine-only primitives/logging/resources were shimmed (no real host was simulated as successful production gameplay). The runner invoked actual NUnit assertions and setup/teardown methods; expected error logs were checked. The source implementations under test were not reimplemented.

- `NarrativeRuntimeExtensionsTests`: **46** cases: numeric operators, unavailable/not/false safety, choice/numeric conditional result snapshots, gameplay outcome guards, root-local snapshot/cancel behavior, training tenths/persistence/cap/overflow/preflight, the daily crossing-10 rule, graph branches/End isolation/hidden edges/loop bound, loader validation, diagnostics, action read-only/negated guards and transient rejection, mutable page bools vs frozen result bools, external/derived training-bar rejection before any flag/attribute write, and 5,001/20,001-page linear and explicit-edge traversal.
- `NarrativeRuntimeIntegrationValidationTests`: **1** case: training/social ranges accepted, unrelated 0..9 bar rejected.
- Existing `NarrativeCoreTests`: **41** cases.
- Existing `NarrativeEntryTests`: **14** cases.
- Existing `NarrativeBatchContinuityTests`: **7** cases.
- Existing `NarrativeExternalValuesTests`: **11** cases.

Latest local evidence: `C:/Users/AltF4/AppData/Local/Temp/ncs-runtime-worker-2390iu3l/portable-review-test.log`. Original 106-test evidence remains in `portable-test.log`.

Central-reuse harness (outside the repository):

```powershell
dotnet run --project "C:/Users/AltF4/AppData/Local/Temp/ncs-runtime-worker-2390iu3l/portable/Portable.csproj" --no-restore |
    Tee-Object -FilePath "C:/Users/AltF4/AppData/Local/Temp/ncs-runtime-worker-2390iu3l/central-portable-results.log"
```

The harness references actual repository source files, so it recompiles the current owned runtime on each run. It now references the temporary Config DLL compiled from Main's current generated source in `official/Spotlight.Config.dll`; it no longer uses signature placeholders. If generated schema changes again, rebuild that temporary Config assembly first using `official/Spotlight.Config.rsp`. This is a temporary local aid with Unity shims, not a committed replacement Unity test runner.

### Executed: Unity compiler checks

Used the installed Unity Roslyn `D:/Unity/6000.3.5.f2sEditor/Data/DotNetSdkRoslyn/csc.dll`, the project's actual Unity references/defines/C#9 response files, and temporary output DLLs, compiling:

- `Spotlight.Config` (actual current generated source)
- `Spotlight.Narrative`
- `Spotlight.Narrative.Core.Tests`
- `Spotlight.Narrative.Editor`
- `Spotlight.Narrative.Editor.Tests`
- `Spotlight.Narrative.Gameplay.Tests`

All **six** compile successfully in the follow-up review against actual current generated fields. Temporary response files and DLLs are in `C:/Users/AltF4/AppData/Local/Temp/ncs-runtime-worker-2390iu3l/official/`. No `PendingSchema.cs` signature placeholder is included in these six checks. The earlier five-assembly check used a temporary signature-only partial while regeneration was pending; that historical check is superseded for compilation by this actual-generated-source check. This is still not a full generated-data semantic load or a Unity PlayMode run.

`git diff --check` passed for this worker's modified tracked files. It was not used to overwrite/fix concurrent workers' unrelated whitespace.

### Written and compiled, NOT executed in Unity

`Spotlight.Narrative.Gameplay.Tests` has four PlayMode tests, all loading the **existing** `Assets/Scenes/PresentationDebugScene.unity`:

1. Presenter releases only its pause/visual owners, preserves another owner, then reacquires before the next page; End excludes later content.
2. Cancel/retry/dispose reject late callbacks and dispose host handles once.
3. Synchronous callbacks accept only the first outcome.
4. Runtime input gate yields while root busy/presentation/save gates remain blocked; a genuine reported failed attempt can resume.

These test a controllable host contract. They do **not** certify the real cafe bridge, real order scoring, or real mouse interaction. The currently open Unity editor was not interrupted to force a competing batch test run.

## Main's remaining integration checklist

1. Main owns confirming source fields and official Luban output consistency; the two generated fields are now present and compile-checked. No JSON-only patching.
2. Bind the real coffee host delegate, preserve its cancellation ownership and existing cafe interaction, and apply only the narrow DayFlow input exception.
3. Finish the central Unity compilation using generated `GameplayKey`/`NextPageId`, then run Narrative core/editor tests and `Spotlight.Narrative.Gameplay.Tests` in PlayMode. Resolve actual loader/generated-data validation failures rather than suppressing them.
4. In the shared presentation scene verify short End vs long branch, image→scene.cafe→image, choices, cancel/replay, and request lifecycle. Real-play verify an actual served product resumes the right quality branch and cancellation does not settle results.
5. Verify normal save gates stay closed during a Gameplay wait, and map/day/story actions remain unavailable while coffee interaction is allowed. No skip or stale callback may mark the root complete.
6. Verify daily threshold rewards, persistence of 0..9 remainder bars, profile/check reads from the existing central player bars, and no double settlement after load.
7. If source graphs need richer semantics than guarded linear/explicit edges, construction-block and audit them rather than flattening exclusive paragraphs or inventing success.


## Bounded follow-up review summary

Only this worker's prior runtime/loader/validation/tests and this MD were edited. No edits to Main's bridge, daily action/content files, Excel, generated outputs, GameFlow, or FreeCafe.

Fixed: action-owned BOOL_EQ `not:bar:` support in runtime/loader/diagnostics, transient-action guard rejection, valid-large-root traversal budget, and O(1) explicit edge lookup. Verified: mutable world-bool page guards, frozen bool-result settlement, external/derived training destinations rejected before mutation, large graph traversal. Latest executed result: **120/120 portable tests**, plus six actual-generated-source Unity-Roslyn assembly compiles. Unity lifecycle/real coffee tests remain unexecuted by this worker.


## Final integration follow-up — editor reports, cancellation, and injection lifecycle

### Direct Reading reporting (fixed)

`Editor/NarrativeIntegrationReport.cs` now resolves the effective class from PresentationIndex just like the runtime, includes an event's direct Reading `presentationKey` as well as nested letter readingKeys, counts shared reading pages/lines once, and checks reading title/body text references and speaker IDs. Missing reading pages/lines, blank body text keys, and genuine `blockedBy` source-construction errors remain errors. No construction gate was suppressed.

Added `Editor/Tests/NarrativeIntegrationReadingReportTests.cs` (+ metadata): 8 regression cases covering direct Reading counts, index-class precedence, missing title/body keys, nested letter deduplication, missing pages, retained construction blockers, missing lines/blank text/unknown speakers, and exclusion of unrelated readings.

### Reading cancellation (fixed, reported Unity verified by Main)

`NarrativeEventRequest` now has `public readonly Action PresentationCancelled`, supplied by the final optional constructor argument `Action presentationCancelled = null`. `NarrativePresenter.ReadingPages` passes a version-guarded page-local cancellation callback that aborts with `playback_cancelled` instead of completing the page/root. Per-page closure rejects stale/duplicate finish or cancel calls. The existing reading delegate API was not replaced; Main owns the GameFlow adapter routing cancel to this callback when present, preserving legacy completion fallback when null.

Added a shared-scene PlayMode lifecycle case proving cancellation does not award training or mark a newspaper read, stale callbacks cannot settle a retry, and a fresh completion awards once. Main reported the runtime PlayMode suite **5/5 passed**, including this cancellation regression. This worker did not independently drive Unity PlayMode.

### Existing content assertions updated to the new contract

- `Tests/LubanNarrativeLoaderTests.cs`: verifies 17 endings comprise 2 disabled delivery-test endings and 15 enabled authored endings; every OR group has an initially-false outcome fact produced by a real Result, and no formal ending qualifies in a fresh controller.
- `Editor/Tests/FormalNarrativeFlowConfigurationTests.cs`: recognizes QueueEvent entrances; C13 must be queued from the completed promotion root rather than independently dated; church1009 was temporarily asserted as day4/noon AfterBusiness to match emitted data, but **natural-flow review proved that source routing incorrect** (see pending correction below); Rose1011 has the actual neutral1010 completion prerequisite.
- `Editor/Tests/NarrativeConfigurationValidatorTests.cs`: C14's Sorel name must be anonymous through the source introduction and named afterward, without a total-line-count shortcut; Rose1011 asserts its real taste Gameplay key, failed outcome guard, source numeric thresholds, exclusive short End branches, guarded long path, terminal End, and choice groups.
- Rose1009's original threshold-5/perception-tail preservation assertions were **not weakened or deleted**; Main/Rose worker owns source restoration, now reported preserved.

### Injected presenter reset ownership (fixed)

`NarrativeRuntime.ResetSession` captures the explicitly injected presenter and reinitializes the new session with that same externally-owned presenter and text resolver. It does not dispose that host object and does not silently replace it with a graphical presenter. Uninjected production reset still disposes/recreates its owned runtime presenter normally. This is an injection-priority/lifecycle fix, not a data-autoplay workaround.

Added `Tests/NarrativeRuntimeInjectionLifecycleTests.cs` (+ metadata), two EditMode cases for normal/replacement-configuration reset: injection retention, resolver retention, no graphical UI creation, no autoplay, repeated resets, no external disposal, and old completion callbacks unable to complete the new session. These two tests compile and are included in Main's final broad EditMode run; they are not included in the portable shim runner because they inspect real Unity GameObject/UI lifecycle.

### Final evidence and scope boundary

Latest owned portable suite: **166/166 passed**; includes updated real-generated snapshot assertions as well as core/daily/action/training/report regressions. Six assemblies passed isolated Unity-Roslyn compilation against actual official Config source. The standalone test runner command remains `Verify-Ncs.ps1 -Mode Tests`; actual full-loader/all-validator command remains `Verify-Ncs.ps1 -Mode Tables`; `-Mode All` runs both. All sidecar source/output remains outside the repository.

Main reported a final official strict generation/load with 346 events, 5,506 pages, 8,156 lines, 36 checks, 17 endings and started a final broad EditMode run. **No further C# writes during that job.** Main's cafe PlayMode run completed 40 tests with 6 known prefab/documentation-assumption failures; those are not certified fixed by this Narrative handoff. The unrelated strict float-score assertion also remains outside this worker's changes.


### Natural-flow correction queued during final test freeze

Main confirmed `AfterBusiness` executes BEFORE MAP, while the Rose1009 prerequisite completes from a subsequent MapAction. Therefore Church1009 scheduled at day4/noon is unreachable in normal play even though static validators pass. The church worker is restoring the original day4/Night/BeforeNight source schedule. Restore `FormalNarrativeFlowConfigurationTests`' Church1009 date and entry assertions accordingly **only after Main releases the current C#-write freeze**. Do not weaken its prerequisite or mark it completed to bypass the problem.

The latest read-only sidecar result against the pre-correction 5,506-page snapshot is **166/166 portable tests, strict FromTables PASS, all validator categories 0 errors / 0 warnings**. Evidence: `C:/Users/AltF4/AppData/Local/Temp/ncs-runtime-worker-2390iu3l/final-central-generated-verification/`. This confirms structural consistency only; it does **not** certify the incorrect noon natural-flow route. That pending chronology correction still requires source regeneration and the date-assertion change.


### Final write-freeze release and Night assertion restored

Main released the freeze after final EditMode job `189a8…` timed out during initialization with **0 tests executed**; do not count that job as a pass. Restored `FormalNarrativeFlowConfigurationTests.FormalDateRoutesUseTheExpectedDayAndPhase` to assert Church1009 day4/Night/BeforeNight and reject a competing noon date. Its real Rose1009 completion prerequisite remains asserted. The comment explains the required map-before-night order. Rose1009's existing threshold5 and shared perception/failure/tail assertions remain unchanged.

All requested owned C# edits are now finished: runtime extensions, direct Reading report/text checks, Reading cancellation callback and regression, externally injected presenter retention across ResetSession and two lifecycle regressions, and meaningful expanded-content baseline assertions. Main will regenerate the Night source correction then rerun broad EditMode only when Unity compilation is idle. Source audit currently reports **3 explicitly blocked roots / 4 construction-error strings**; these remain visible, not suppressed. No further C# writes from this worker during the restarted jobs.
