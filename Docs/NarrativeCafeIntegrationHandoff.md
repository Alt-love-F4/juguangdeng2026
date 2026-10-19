# Narrative ↔ real cafe / DayFlow handoff

Date: 2026-10-19 (Asia/Shanghai). Scope: DayFlow/cafe gameplay worker. This is not a claim that all narrative content has been integrated or verified.

## Owned changes

- `Assets/Scripts/GameFlow/UI/DayFlowController.cs`
- `Assets/Scripts/GameFlow/UI/NarrativeCafeGameplayBridge.cs` (+ `.meta`)
- `Assets/Scripts/GameFlow/UI/NarrativeCafeGameplayBridge.Session.cs` (+ `.meta`)
- `Assets/Scripts/GameFlow/UI/Tests/NarrativeCafeGameplayBridgeTests.cs` (+ `.meta`)
- `Assets/Scripts/GameFlow/UI/Tests/NarrativeCafeDayFlowTests.cs` (+ `.meta`)
- This document.

No Narrative runtime/schema/Excel/generated/content/catalog/CafeWorld/FreeCafe.Unity/scene/prefab writes by this worker. Main owns the new `CafeWorld.NarrativeService.cs` API and its single `ServeProduction` hook. Concurrent modifications by other workers must not be reset or overwritten.

## Final integration direction (supersedes initial observer prototype)

The first prototype conservatively observed `CafeWorld.Changed` counters and could only run in open business. **That prototype has been removed.** Main supplied a proper targeted, owner-scoped, actual-domain service API, so the final bridge:

- Calls `CafeWorld.TryBeginNarrativeService(ownerKey, orders, onDelivery, out lease, out error)`.
- Completes gameplay **only** from `CafeNarrativeDeliveryReceipt.Completed` emitted by actual `ServeProduction`/physical cup consumption.
- Never infers delivery from `Changed`, disappearing entities, money, successfulOrders, lastDelivery text, a populated tray or clicking a confirm button.
- Supports both closed/prebusiness and open cafe production worlds without toggling `State.open`, advancing dates, generating sales or replacing ordinary orders.
- Disposes the returned domain lease to cancel **only this owner's** remaining orders and restore focus. The domain creates the real guest/order; this bridge does not write world snapshots.

## Runtime/host contract

The host installs its delegate on the **existing** `NarrativeRuntime.Instance.Presenter.GameplayPlayback`, not a new presenter/system. It binds on initialization, new-day/restore entry, narrative phase entry and a cheap running-session presenter identity check in Update. Another explicit GameplayPlayback provider wins.

The runtime contract is:

```csharp
Func<NarrativeEventRequest, NarrativeGameplayPresentationData,
    Action<NarrativeGameplayResult>, IDisposable> GameplayPlayback
```

Data fields are `pageId`, `eventId`, `gameplayKey`, `continuityOwner`. Completion uses `NarrativeGameplayResult { passed, cancelled }`.

DayFlow creates a unique service owner incorporating RequestId/eventId/pageId and an operation GUID. Rebind/disable/main-menu/destruction remove only this host's delegate and cancel its active operation. A presenter disposing its returned handle is silent and idempotent. Later scene-arrival callbacks and duplicate callbacks cannot attach a second observation or complete an old owner.

### Gameplay keys

```text
serve:<catalogProductId>:<minimumScore>:<count>[+product:score:count]
taste:<catalogProductId>:<minimumScore>:<count>[+product:score:count]
serve:any:<minimumScore>:<count>
taste:any:<minimumScore>:<count>
serve:any.hot:<minimumScore>:<count>
taste:blackcoffee.sweet:<minimumScore>:<count>
serve:blackcoffee.unsweetened:<minimumScore>:<count>
```

- `serve:` sends `CafeNarrativeOrderSpec.requirePass=true`. Below-threshold actual deliveries consume their actual cup and require a retry; the runtime remains pending until the domain emits a completed passing quota.
- `taste:` sends `requirePass=false`. Each real taste attempt counts; a below-threshold attempt can complete the request with **passed=false, cancelled=false**. Multi-attempt results are aggregated. Do not turn a low taste result into cancellation or fake success.
- A newly created guest/order is **not** completion.
- Each receipt is checked for this OwnerKey, nonempty RequestId/CupId, a permitted actual catalog ProductId and finite Score in 0–100. Each physical CupId is accepted at most once; only `Completed=true` releases the runtime. The authoritative domain owns quota bookkeeping, request creation and cup-size-aware product evaluation.
- Explicit special tokens are `any`, `any.hot`, `blackcoffee.sweet`, `blackcoffee.unsweetened`. They are passed **unchanged** as `CafeNarrativeOrderSpec.productId`; only fixed catalog/receipt validation resolves the two blackcoffee variants to base `blackcoffee`. Unknown IDs and unagreed suffixes (`missing`, `any.cold`, `espresso.sweet`, `blackcoffee.unknown`, capitalized `Any`) still fail closed.
- `any`/`any.hot` accept a receipt for any **real catalog product** classified and physically delivered by the domain; a receipt product ID of `any`, unknown/empty beverage or a UI placeholder is not accepted. A mixed wildcard/fixed request keeps domain-owned quota sequence; no receipt is reassigned by host guessing.
- `any.hot` requires actual beverage temperature; sugar variants require actual sugar presence/absence. These are verified by main's domain delivery path, not inferred from hints/recipe names. The hot threshold **40°C** is a provisional implementation preset, not claimed author truth. Domain uses actual `TemperatureCelsius(cup)` and nonzero actual sugar portions.
- `GameplayHint` formats “需要一杯任意饮品 / 热饮 / 加糖黑咖啡 / 无糖黑咖啡”, thresholds and counts. The session exposes this hint, and DayFlow shows it via the existing nonmodal `OperationToastPanel` only after service is ready (no new UI, input capture or success button). The initial wildcard UI order's default product is **not** an extra story constraint.
- Parse/catalog validation has no side effects. Actual order availability, seat capacity and concurrent service validation belong to the domain API. For example, the current domain caps the aggregate requested count at 32 and can reject when no seat is free; host propagates the real diagnostic/cancellation instead of inventing capacity.

### Product IDs and actual float evaluation

Use catalog IDs, not English spelling guesses: `blackcoffee`, `milkcoffee`, `espresso`, `latte`, `cocoa` etc; `black_coffee` is invalid. Main owns content authoring/merging of actual keys.

Minimum scores are invariant-culture **float** 0–100, never integer/grade rounding. The real implementation is `EvaluateProduction(actualCup, actualProduct, RequiredCupSpecId(actualOrder)).total`, including the configured `EvaluateProductComposition` amount/standard/concentration, recipe class and cup scaling. Main's `NarrativeService.Deliver` calls that evaluator and emits the Score. `ProductionQuality.total` is float. Current catalog amount/standard weights are 0.65/0.35; the bridge does not duplicate that evaluator or hardcode those weights.

`ProvisionalNormalScore=60` and `ProvisionalExcellentScore=90` are **tunable implementation presets, not source-author truth**. No localized display-grade interpretation is used. Explicit authored numeric thresholds should be passed through by content authoring.

## Scene/world/input lifetime

The host uses `GameFlowBootstrap.TransitionToGameplayScene("CoffeeShop", ...)`, `PlayerCafeOwner` and the existing gameplay endpoint. It never creates another cafe, test Scene, Canvas, input system, coffee inventory or success button.

A pending scene transition is **not ready**. `IsNarrativeCafeGameplayActive` becomes true only after the destination callback obtains the live scene-owned world and endpoint and successfully attaches its domain service. Until then, the DayFlow input gate remains blocked.

Once ready, only the cafe input gate opens; DayFlow preserves its narrative phase/continuation. Save/end-day gates remain closed, including while a scene transition is pending. The runtime worker owns `IsAwaitingGameplay`, `NarrativeRuntime.BlocksGameplayInput` exception and release/reacquisition of the presenter's **own** pause token. The bridge calls no global Clear/Resume, changes no timeScale, and cannot release pause-menu/details/another owner's pause.

`CafeWorld.Changed` is subscribed **only for cancellation/lifecycle**: snapshot replacement, calendar change and an observed active business becoming closed cancel this pending owner. DayFlow also cancels if `PlayerCafeOwner.Current` changes. An initially closed Night/Preparation world is supported; it is not treated as an ended ordinary business. Owner disposal unsubscribes before releasing the domain lease or invoking runtime callbacks to prevent reentrancy/stale results.

## Existing feedback investigated

`FreeCafeRuntime.CupDelivered` and `CafeDeliveryReceipt` are UI-layer events with display labels and no immutable cup/request/product IDs. Also FreeCafe.Unity already references GameFlow.UI, so the opposite reference would create a cycle. The bridge does **not** reference FreeCafe.Unity, parse message text or use its receipt's display quality. It now consumes the main-owned **domain** `CafeNarrativeDeliveryReceipt` instead.

Existing `CafeVisitIntent` / `SyncCafeVisits` do not accept a fixed product. They were not abused to rewrite normal guests/orders. Targeted reservations now come through main's dedicated domain service API, still using existing actual cafe entities/delivery/scoring and interactions.

## End-day fix

Map and bed share `CanRequestDayEnd`:

- playing/running session;
- first-session cafe guide/tutorial genuinely complete; bed no longer marks tutorial complete;
- no active narrative entry/controller work/action execution;
- no cafe gameplay operation, including pending scene arrival;
- no mounted mandatory actions (`HasBlockingActions`), using existing map focus/hint or a nonmodal existing toast in the cafe.

Bed sequence:

1. Active/observed business → existing endpoint closes it once → existing `BusinessEndPanel` confirmation → `AfterBusiness` → recheck mandatory actions → `BeforeNight` → existing `CompleteDay`.
2. Preparation/rest visits or a same-day return after already-settled business do not invent another business settlement; they use the map's BeforeNight/day-end gate.
3. A repeated bed click cannot replace an unconfirmed settlement page or queue a second night entry.
4. If AfterBusiness unlocks a mandatory action, do not enter BeforeNight or advance day; return to the existing map.
5. Existing narrative failure/cancellation retry behavior remains. No recursive map↔bed calls, no direct shortcut and no fake AddCalendar skipping.

Cafe opening UI, business sign interaction, teaching steps and real coffee drag/pour/tray/bell code are untouched by this worker.

## Tests and actual evidence

### Authored fixtures

- `Spotlight.GameFlow.Tests.NarrativeCafeGameplayBridgeTests`: **58 authored NUnit cases after wildcard/variant extension**. Actual shipped catalog and CafeWorld+main's NarrativeService transactions; serve/taste parsing; invalid catalog IDs/float/counts; multiple actual cups/products; failed serve retry; failed taste outcome; multi-taste aggregation; discard; ordinary-counter edits cannot complete; silent dispose; ordinary-business close cancellation; restore to a valid pre-service snapshot; closed/Night real service without reopening/date/economy changes; preserve ordinary orders and restore focus; explicit wildcard/variant grammar and hints; actual wildcard beverage different from the default UI order; empty/unknown drink rejection; mixed fixed/wildcard quotas; actual sugar and temperature constraints.
- `Spotlight.GameFlow.Tests.NarrativeCafeDayFlowTests`: **9 authored NUnit cases**. Mandatory gates map/bed/preleisure; tutorial never force-completed; BeforeNight before date increment; AfterBusiness→BeforeNight continuation; new mandatory actions after business; pending vs ready cafe gate and no save/day-end; duplicate scene-arrival attachment rejected. Headless controller tests, not another scene/UI layout.

### Executed by this worker

- Standalone .NET harness compiles **actual current CafeWorld domain sources including main's newly supplied NarrativeService**, actual bridge/session sources and actual bridge test source: **34 passed / 0 failed**. Minimal runtime DTO and Resources/JSON shims live outside the repo. This validates real domain transactions but is **not** Unity serialization/compile/scene/pause/UI verification. The earlier 28-case state-observer prototype run is superseded by this 34-case API run.
- `git diff --check` on owned DayFlow diff passed.
- Offline project Roslyn compile attempted before final domain API landed: stopped in concurrently edited Narrative loader because compiled/generated config lacked `NcsConversationPageEntry.GameplayKey`/`NextPageId`. No generated files modified to bypass it. Full-project compile is still main's responsibility after official schema generation.
- Read-only `.unity/mcp.py get_compilation_state` at 6402 returned `Unknown or unsupported command type: get_compilation_state`. Legacy 6400 `status` also unsupported. No Unity refresh/PlayMode/tests were started; main owns centralized integration tests.

## Main/runtime follow-up and remaining limits

1. **Runtime owner-scene unload boundary:** at inspection `NarrativePresenter.OnSceneUnloaded` still interrupted the owner without an awaiting-gameplay exception. Map↔CoffeeShop normally keeps Map resident; a root originating in a genuinely unloaded scene still needs the runtime worker's intentional handoff handling. The cafe worker did not edit runtime.
2. **Domain capacity/guest presentation:** the domain may reject a full cafe or another narrative service. Do not silently evict existing customers. Main should verify the new domain guest's seat placement, identity/visual, recipe availability and UI focus as part of real-game testing. No role was impersonated by this worker.
3. **Runtime-only reservations are not save payload:** host/runtime blocks saving during gameplay. Restore tests restore a valid pre-service snapshot rather than claiming runtime reservations can be serialized/replayed.
4. **Detention:** the follow-up below now implements canonical-fact, natural one-day transitions. No fake calendar skip, income, payment or shadow host-ready flag is used.
5. This bridge does not settle NCS choice/results or select daily pools. Existing runtime root completion and main's `NarrativeActionController` daily selector remain the only owners.

## Main's central regression checklist

After official generation and Unity compile/import:

- Run `Spotlight.GameFlow.Narrative.Tests` including both new fixtures, plus existing `DayFlowPresentationTests`, cafe tutorial, FreeCafe production/order/visit tests and main's domain NarrativeService tests.
- Presentation/UI regression uses **existing** `Assets/Scenes/PresentationDebugScene.unity`: image→scene.cafe→image, cancel/replay, unrelated pause owner; no new scene/Canvas/input system.
- Formal **Rose1011 MorningBeforeMap**: closed/prebusiness real cafe loads, its real targeted order is visible, actual drag/pour/tray/bell completes exactly once, low taste branches false, serve requires actual successful retry, original narrative resumes and root result settles once. No fake reopen, money, calendar or ordinary-order replacement.
- Pending transition failure/cancellation, world restore/replacement, menu/disable/destroy, presenter replacement/injected delegate and duplicate callbacks.
- Bed/map mandatory parity; active business confirmation→AfterBusiness→BeforeNight; new mandatory action stops night; bed cannot skip tutorial.

Do not call the complete story playable solely because the new gameplay page can be displayed; main must validate content prerequisites, runtime graphs and formal date/phase entry as well.

## Wildcard / variant extension verification (follow-up)

Bridge patch now accepts the four agreed special tokens intact, validates actual catalog receipt products, and exposes ready-only nonmodal instructions. No CafeWorld/content/runtime writes were made by this worker. First actual-domain run while main's variant patch was not yet saved: **54 passed / 4 failed**; all four failures were `TryBeginNarrativeService` rejecting not-yet-supported `blackcoffee.sweet`, `blackcoffee.unsweetened`, `any.hot` at domain validation. Plain wildcard `serve:any`/`taste:any`, real alternate-product delivery and empty/unknown rejection passed. Final variant verification will be recorded after main's domain implementation is available; this is not represented as a completed variant end-to-end pass.

## Follow-up: host:end_business and canonical detention natural days

### Implemented host command

`Gameplay.gameplayKey = host:end_business` is handled by DayFlow **before** any serve/taste parsing or service reservation. It shares the existing cancellable scene-arrival lease and requires the live bound CoffeeShop world. For an actually open world, it calls **only** the existing `FindGameplayEndpoint().EndDayFlowBusiness()` and then verifies that the same `PlayerCafeOwner.Current` world is closed. If already closed, it returns a legitimate idempotent success without starting/settling another business. Missing/replaced world, missing open-world endpoint or endpoint refusal cancels/fails closed. Only verified closure emits `passed=true`; no direct calendar, payment, service order, recipe unlock or invented revenue is emitted. A repeated/disposed callback cannot complete twice.

Normal closure may perform the existing real day's standard EndDay bookkeeping (such as the existing reputation summary); this is not replaced by fabricated results. The command itself never calls DayFlow.CompleteDay or starts business.

### Canonical-fact detention flow

The **only** detention authority read is the declared NCS condition `church.detention.active`. This host does not write it or add `church.detention.host_ready`, `requested`, release-ack or payment flags. Church content owns setting the fact on actual capture and clearing it in the scheduled release root.

- While true, DayFlow denies cafe travel/business start/bed/map interaction and never exposes `world.map.active=true`. Existing pending real gameplay does not become a back door to normal business during detention.
- Successful `MorningBeforeMap` records that **this** day's morning completed. If the fact is still true, DayFlow skips business/map and calls the existing CompleteDay **once**, only at a safe idle NCS boundary, with no executing map action/gameplay lease, no terminal ending and no unrelated presentation pause/timeScale pause.
- A capture that settles from a MapAction or later phase is noticed only after the complete root/action is safe. Remaining actually-open business is closed through the same real endpoint; an unavailable/refusing endpoint holds the date rather than faking closure. No auto-completion occurs inside the active capture or release root.
- Each increment uses the existing CompleteDay → BeginNewDayFlow → DateChange presentation → ProcessOfficialDay → MorningBeforeMap. There is no AddCalendar, loop jumping several dates, auto-serving or income generation. The per-day latch prevents duplicate callbacks from advancing the same day twice.
- With the source schedule requested by main, day15 is 670-10-20 (capture), day16/17 are 10-21/22 and day18 is 10-23 (release). DayFlow does **not** clear detention just because the clock reaches day18; the real scheduled release event must run at MorningBeforeMap and set `church.detention.active=false`. Once it does, the normal continuation runs on day18 without a fake payment.
- The detention restriction intentionally supersedes irrelevant normal-day mounted actions while detained; the regular mandatory-action gate remains unchanged outside detention. It is not a general map/bed bypass.
- Save restoration of an ordinary interactive phase can recognize its completed morning; an interrupted NarrativeEntry is not treated as safe completion. Map availability remains suppressed while detained.

### Source/content responsibilities still not invented by host

Main/church worker must author the capture result, scheduled release root, lawful rescue/payment/ending decisions and fact clear on actual release. No 2000/3000 payment, ending menu, checkpoint reload, false release acknowledgement or unsupported source outcome was added. A still-active fact on later days remains active; this host cannot claim release or resolve missing content merely by reaching a calendar date.

### Additional authored tests / actual compilation evidence

`Spotlight.GameFlow.Tests.NarrativeCafeHostDetentionTests` adds **11 focused cases**: actual endpoint closure and idempotence, already-closed success, refusal/missing world, capture root cannot auto-advance, days16/17 require their own morning and per-day latch, scheduled day18 morning release restores the ordinary continuation with no invented money, canonical fact only, unrelated pause preservation, idempotent host-result disposal. The tests are headless; no new scene/UI layout is constructed. Main must execute them centrally in Unity.

Offline Roslyn compilation using the real Unity project references and current sources now succeeds for **Spotlight.Config (85 sources), Spotlight.FreeCafe.Domain (31), Spotlight.Narrative (42), Spotlight.GameFlow.UI (122), Spotlight.GameFlow.Narrative.Tests (11)**. These compilations include the new host/detention fixture and bridge files and use C#9. This supersedes the earlier generated-GameplayKey/NextPageId compile blocker. It is compile evidence, not Unity runtime test execution.

After main's variant domain implementation landed, standalone bridge regression progressed to **57 passed / 1 failed**. The remaining actual-domain failure is `SweetTokenKeepsActualSugarConstraintAndShowsInstruction`: the domain's generic `ClassifyCoffee` rejects a blackcoffee cup containing an actual sugar portion before `NarrativeService` reaches its `.sweet` check. Unsweetened/hot and plain wildcard cases pass. This is reported, not hidden by stripping tokens or changing/faking the delivered drink. Main owns any approved domain fix; this worker did not alter domain/classification/content.

Main integration regression must additionally drive real date transitions 15→16→17→18, inspect map/business absence on days16/17, and verify the day18 release root clears the canonical fact before normal play resumes. These complete scene/date-transition regressions were not run by this worker.

## Central EditMode follow-up: business-start ordering and domain cancellation

Main reported the centralized 73-test run's sole failure in `ClosingBusinessCancelsOnceWithoutCalendarAdvance`. The test now asserts actual ordinary StartDay **before** beginning narrative service and supplies a spare test-fixture seat without evicting a customer. It no longer attempts the intentionally prohibited StartDay during `HasNarrativeService`; no input or business-start gate was relaxed.

The bridge's lifecycle-only Changed observer now also cancels a still-pending request when the authoritative `world.HasNarrativeService` lease disappears. This covers domain EndDay disposal even when service began in an already-closed/prebusiness world (no open→closed transition). The main-owned successful path remains Release(false) → immutable Completed receipt callback → Notify, so real completion reaches the bridge before any missing-lease notification and is not misreported as cancellation. A new regression `DomainServiceCancellationWhileAlreadyClosedCancelsBridgeExactlyOnce` verifies this, missing owned request, cancellation once and unchanged calendar.

After main's sugar-classification fix, the updated **actual-domain bridge harness passes all 59 cases / 0 failures**, including sweet/unsweetened/hot, normal-business closure and already-closed service cancellation. This supersedes the earlier 57/1 variant result. Central Unity rerun remains main-owned; no Unity integration tests or input gates were changed by this fix.
