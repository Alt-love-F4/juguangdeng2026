using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeMapContentTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public Action complete;
            public event Action<string> Cancelled;
            public void Play(NarrativeEventRequest request, Action completed) => complete = completed;
            public void Cancel() => Cancelled?.Invoke("user_cancelled");
        }

        private static NarrativeController Controller(Presenter presenter, bool condition = false)
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.events.Add(new NarrativeEventDefinition { eventId = "story" });
            if (condition)
            {
                config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "map.active" });
                config.conditions.Add(new NarrativeConditionDefinition { conditionId = "active", ownerType = "action", ownerId = "visit", condType = "BOOL_EQ", target = "map.active", value = "true" });
            }
            return new NarrativeController(config, presenter);
        }

        [Test]
        public void PageGatedOnSkippedCheckIsHiddenInsteadOfAbortingTheRoot()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            var narrative = new NarrativeController(config, new Presenter());
            // revolution.1014.s1: the language check page is itself conditional, so it may never run.
            Assert.That(narrative.ReadPageCondition("check:skipped"), Is.False);
            Assert.That(narrative.ReadPageCondition("not:check:skipped"), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => narrative.ReadPresentationCondition("check:skipped"),
                "Diagnostics keep reporting the missing evaluation.");
        }

        [Test]
        public void MapGuardedMandatoryActionStillBlocksNightWhileMapIsClosed()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "world.map.active" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "map", ownerType = "action", ownerId = "visit",
                condType = "BOOL_EQ", target = "world.map.active", value = "true" });
            var narrative = new NarrativeController(config, new Presenter());
            var actions = new NarrativeActionController(narrative, new[] { new NarrativeActionDefinition { actionKey = "visit", mustExecute = true } });
            actions.Mount("cafe", "visit");
            narrative.State.SetCondition("world.map.active", false);
            Assert.That(actions.GetDisplayableActions("cafe"), Is.Empty, "Closed map still hides the card.");
            Assert.That(actions.HasBlockingActions(), Is.True, "The cafe bed must not skip an outstanding map task.");
            Assert.That(narrative.State.TryGetCondition("world.map.active", out bool map) && !map, Is.True, "Evaluation must not write state.");
        }

        [Test]
        public void SeenInstanceSurvivesRefreshDisappearanceAndSaveWithoutCompletingAction()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter, true);
            var definition = new NarrativeActionDefinition { actionKey = "visit", mustExecute = true };
            var actions = new NarrativeActionController(narrative, new[] { definition });
            actions.Mount("cafe", "visit");
            Assert.That(actions.HasUnseenActions("cafe"), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.False);
            Assert.That(actions.HasUnseenImportantActions("cafe"), Is.False, "No action events means no important map dot.");
            narrative.State.SetCondition("map.active", true);
            Assert.That(actions.HasUnseenActions("cafe"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.True);
            Assert.That(actions.HasUnseenImportantActions("cafe"), Is.False, "The configured event list is empty.");
            string token = actions.GetActionInstanceKey("cafe", "visit");
            Assert.That(actions.ConfirmActionDisplayed("cafe", token), Is.True);
            Assert.That(actions.HasBlockingActions(), Is.True, "Seen is not completed.");
            narrative.State.SetCondition("map.active", false);
            narrative.State.SetCondition("map.active", true);
            actions.RefreshMapContent(); actions.Mount("cafe", "visit");
            Assert.That(actions.HasUnseenActions("cafe"), Is.False);
            string json = actions.CaptureJson();
            var restoredNarrative = Controller(new Presenter(), true); restoredNarrative.State.SetCondition("map.active", true);
            var restored = new NarrativeActionController(restoredNarrative, new[] { definition }); restored.RestoreJson(json);
            Assert.That(restored.HasUnseenActions("cafe"), Is.False);
            Assert.That(restored.GetActionInstanceKey("cafe", "visit"), Is.EqualTo(token));
            Assert.That(restored.HasBlockingActions(), Is.True);
        }

        [Test]
        public void DailyAndExplicitStageVersionsAreStableAndCanBecomeNew()
        {
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] {
                new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily },
                new NarrativeActionDefinition { actionKey = "stage" } });
            actions.Mount("market", "daily"); actions.Mount("cafe", "stage", "phase-a");
            string daily = actions.GetActionInstanceKey("market", "daily");
            string stage = actions.GetActionInstanceKey("cafe", "stage");
            actions.ConfirmActionDisplayed("market", daily); actions.ConfirmActionDisplayed("cafe", stage);
            actions.RefreshMapContent(); actions.Mount("cafe", "stage", "phase-a");
            Assert.That(actions.HasUnseenActions("cafe"), Is.False);
            actions.BeginDay(2);
            Assert.That(actions.GetActionInstanceKey("market", "daily"), Is.Not.EqualTo(daily));
            Assert.That(actions.HasUnseenActions("market"), Is.True);
            Assert.That(actions.HasUnseenActions("cafe"), Is.False);
            actions.Mount("cafe", "stage", "phase-b");
            Assert.That(actions.ConfirmActionDisplayed("cafe", stage), Is.False, "Stale detail must not acknowledge a new instance.");
            Assert.That(actions.HasUnseenActions("cafe"), Is.True);
        }

        [Test]
        public void LegacySnapshotDefaultsVisibleActionsToUnseenAndAPCostsDoNotHideCards()
        {
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] {
                new NarrativeActionDefinition { actionKey = "costly", requirements = "ActionPoint(2)" } });
            actions.RestoreFrom(new NarrativeActionStateSnapshot { actionPoints = 0, mounts = new List<NarrativeActionMountValue> {
                new NarrativeActionMountValue { position = "market", action = "costly", day = 1 } } });
            Assert.That(actions.GetDisplayableActions("market").Count(), Is.EqualTo(1));
            Assert.That(actions.HasUnseenActions("market"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            Assert.That(actions.IsAvailable("costly", out _), Is.False);
            var changedPositions = new List<string>();
            actions.MapContentChanged += changedPositions.Add;
            actions.ActionPoints.Add(2);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(changedPositions, Is.EqualTo(new[] { "market" }), "AP changes must refresh the badge without changing the card instance.");
            actions.ActionPoints.Add(1);
            actions.RefreshMapContent();
            Assert.That(changedPositions.Count, Is.EqualTo(1), "Still-affordable actions do not produce duplicate content notifications.");
            actions.ActionPoints.Set(0);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            Assert.That(actions.GetDisplayableActions("market").Count(), Is.EqualTo(1));
            Assert.That(changedPositions.Count, Is.EqualTo(2));
        }

        [Test]
        public void MapDotOnlyShowsUnreadImportantNonDailyActionsWithEvents()
        {
            var presenter = new Presenter();
            var narrative = Controller(presenter);
            var important = new NarrativeActionDefinition { actionKey = "important", type = NarrativeActionType.Once,
                mustExecute = true, requirements = "ActionPoint(2)", resultEvents = new List<string> { "story" } };
            var optional = new NarrativeActionDefinition { actionKey = "optional", resultEvents = new List<string> { "story" } };
            var noEvents = new NarrativeActionDefinition { actionKey = "no-events", mustExecute = true };
            var daily = new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily,
                mustExecute = true, resultEvents = new List<string> { "story" } };
            var actions = new NarrativeActionController(narrative, new[] { important, optional, noEvents, daily });
            actions.ActionPoints.Set(0);
            actions.Mount("market", "important");
            actions.Mount("cafe", "optional");
            actions.Mount("home", "no-events");
            actions.Mount("shop", "daily");

            Assert.That(actions.HasUnseenImportantActions("market"), Is.True, "Important action dots should not depend on affordability.");
            Assert.That(actions.HasUnseenImportantActions("cafe"), Is.False, "Ordinary actions should not have map dots.");
            Assert.That(actions.HasUnseenImportantActions("home"), Is.False, "Actions without event lists should not have map dots.");
            Assert.That(actions.HasUnseenImportantActions("shop"), Is.False, "Daily actions should not have map dots.");

            string token = actions.GetActionInstanceKey("market", "important");
            Assert.That(actions.ConfirmActionDisplayed("market", token), Is.True);
            Assert.That(actions.HasUnseenImportantActions("market"), Is.False, "Opening the important action clears its dot.");
        }

        [Test]
        public void ImportantMapDotStillHonorsConditionsAndFutureState()
        {
            var narrative = Controller(new Presenter(), true);
            var gated = new NarrativeActionDefinition { actionKey = "visit", type = NarrativeActionType.Once,
                mustExecute = true, resultEvents = new List<string> { "story" } };
            var future = new NarrativeActionDefinition { actionKey = "future", type = NarrativeActionType.Future,
                mustExecute = true, durationDays = 1, nextActionKey = "next", resultEvents = new List<string> { "story" } };
            var next = new NarrativeActionDefinition { actionKey = "next", type = NarrativeActionType.Once,
                mustExecute = true, resultEvents = new List<string> { "story" } };
            var actions = new NarrativeActionController(narrative, new[] { gated, future, next });
            actions.Mount("cafe", "visit");
            actions.Mount("market", "future");

            Assert.That(actions.HasUnseenImportantActions("cafe"), Is.False, "Unmet action conditions still hide the dot.");
            Assert.That(actions.HasUnseenImportantActions("market"), Is.False, "Future actions do not have map dots.");
            narrative.State.SetCondition("map.active", true);
            Assert.That(actions.HasUnseenImportantActions("cafe"), Is.True);
            actions.BeginDay(2);
            Assert.That(actions.HasUnseenImportantActions("market"), Is.True, "The successor action becomes eligible after mounting.");
        }

        [Test]
        public void ZeroCostMandatoryOnceReplacesLocationAndCompletesOnlyAfterSuccess()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter);
            var visit = new NarrativeActionDefinition { actionKey = "visit", type = NarrativeActionType.Once, mustExecute = true,
                replacesLocationActions = true, focusLocationOnComplete = true, resultEvents = new List<string> { "story" } };
            var actions = new NarrativeActionController(narrative, new[] { visit, new NarrativeActionDefinition { actionKey = "ordinary" } });
            actions.Mount("cafe", "ordinary"); actions.Mount("cafe", "visit"); actions.ActionPoints.Set(0);
            Assert.That(actions.GetDisplayableActions("cafe").Select(a => a.actionKey), Is.EqualTo(new[] { "visit" }));
            Assert.That(actions.GetBlockingPositions(), Is.EqualTo(new[] { "cafe" }));
            Assert.That(actions.Execute("visit"), Is.True);
            Assert.That(actions.IsCompleted("visit"), Is.False);
            Assert.That(actions.CanCaptureSnapshot, Is.False);
            presenter.complete();
            Assert.That(actions.IsCompleted("visit"), Is.True);
            Assert.That(actions.HasBlockingActions(), Is.False);
            Assert.That(actions.ReplacesLocationActions("cafe"), Is.False);
            Assert.That(actions.PendingFocusPosition, Is.EqualTo("cafe"));
            string json = actions.CaptureJson();
            var restored = new NarrativeActionController(Controller(new Presenter()), new[] { visit, new NarrativeActionDefinition { actionKey = "ordinary" } });
            restored.RestoreJson(json);
            Assert.That(restored.ConsumePendingFocus("cafe"), Is.True);
            Assert.That(restored.PendingFocusPosition, Is.Null);
            Assert.That(restored.Execute("visit"), Is.False);
        }

        [Test]
        public void CancelledOnceKeepsMandatoryActionRetryableAndRejectsOldCompletion()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "visit", type = NarrativeActionType.Once, mustExecute = true,
                focusLocationOnComplete = true, resultEvents = new List<string> { "story" } };
            var actions = new NarrativeActionController(narrative, new[] { action }); actions.Mount("cafe", "visit");
            bool? result = null; actions.Execute("visit", value => result = value); Action stale = presenter.complete;
            presenter.Cancel();
            Assert.That(result, Is.False); Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.IsCompleted("visit"), Is.False); Assert.That(actions.HasBlockingActions(), Is.True);
            Assert.That(actions.PendingFocusPosition, Is.Null);
            Assert.That(actions.Execute("visit"), Is.True);
            stale();
            Assert.That(actions.IsCompleted("visit"), Is.False, "NCS epoch invalidates the old callback.");
            presenter.complete();
            Assert.That(actions.IsCompleted("visit"), Is.True);
        }

        [Test]
        public void UnreadyValueBaselineDoesNotEvaluateExternalConditionsOrShowActions()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", sourceMode = NarrativeSourceMode.External, providerId = "calendar", sourceKey = "day" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "day", ownerType = "action", ownerId = "visit",
                condType = "BAR_BETWEEN", target = "world.calendar", value = "2,2" });
            var actions = new NarrativeActionController(new NarrativeController(config), new[] { new NarrativeActionDefinition { actionKey = "visit" } });
            Assert.DoesNotThrow(() => actions.Mount("cafe", "visit"));
            Assert.DoesNotThrow(() => actions.RefreshMapContent());
            Assert.That(actions.GetDisplayableActions("cafe"), Is.Empty);
            Assert.That(actions.HasUnseenActions("cafe"), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.False);
            Assert.That(actions.IsAvailable("visit", out _), Is.False);
        }

        [Test]
        public void FutureCardIsUnreadButDoesNotBadgeUntilItsSuccessorIsMounted()
        {
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] {
                new NarrativeActionDefinition { actionKey = "future", type = NarrativeActionType.Future, durationDays = 1, nextActionKey = "ready" },
                new NarrativeActionDefinition { actionKey = "ready" } });
            actions.Mount("market", "future");
            Assert.That(actions.GetDisplayableActions("market").Select(a => a.actionKey), Is.EqualTo(new[] { "future" }));
            Assert.That(actions.HasUnseenActions("market"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            actions.BeginDay(2);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(actions.GetDisplayableActions("market").Select(a => a.actionKey), Is.EqualTo(new[] { "ready" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DailyOrDeliveryPerformedTodayDoesNotBadgeAndNextDayCanBecomeNew(bool delivery)
        {
            var action = new NarrativeActionDefinition { actionKey = "daily", type = delivery ? NarrativeActionType.Repeat : NarrativeActionType.Daily,
                deliveryMode = delivery ? NarrativeDeliveryMode.Cumulative : NarrativeDeliveryMode.None, progressTarget = delivery ? 2 : 0 };
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { action });
            actions.Mount("market", "daily");
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(actions.Execute("daily"), Is.True);
            Assert.That(actions.GetDisplayableActions("market"), Is.EqualTo(new[] { action }));
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            actions.BeginDay(2);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(actions.Execute("daily"), Is.True);
            actions.BeginDay(3);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.EqualTo(!delivery), "Completed deliveries must not badge on later days.");
        }

        [Test]
        public void ItemCostsRefreshBadgesAndInventoryReplacementUnsubscribesPreviousStore()
        {
            var inventory = new NarrativeMapInventory();
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] {
                new NarrativeActionDefinition { actionKey = "deliver", requirements = "Item(bean,1);Item(bean,1)" } }, inventory: inventory);
            actions.Mount("market", "deliver");
            int changes = 0;
            actions.MapContentChanged += _ => changes++;
            Assert.That(actions.HasUnseenActions("market"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            inventory.TryAdd("bean", 1);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False, "Repeated costs share the same stock.");
            Assert.That(changes, Is.Zero);
            inventory.TryAdd("bean", 1);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            var replacement = new NarrativeMapInventory();
            actions.SetInventory(replacement);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            Assert.That(changes, Is.EqualTo(2));
            int stateChanges = 0;
            actions.StateChanged += () => stateChanges++;
            inventory.TryAdd("bean", 1);
            Assert.That(stateChanges, Is.Zero, "An old store must not keep notifying the action controller.");
            replacement.TryAdd("bean", 2);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(stateChanges, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void EndDayDoesNotBadgeWhileMandatoryActionBlocksIt()
        {
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] {
                new NarrativeActionDefinition { actionKey = "visit", type = NarrativeActionType.Once, mustExecute = true },
                new NarrativeActionDefinition { actionKey = "end", type = NarrativeActionType.Daily, endDay = true } });
            actions.Mount("cafe", "visit"); actions.Mount("home", "end");
            Assert.That(actions.HasUnseenActions("home"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("home"), Is.False);
            var changedPositions = new List<string>();
            actions.MapContentChanged += changedPositions.Add;
            Assert.That(actions.Execute("visit"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("home"), Is.True);
            Assert.That(changedPositions, Does.Contain("home"));
        }

        [Test]
        public void TemporaryStoryBusyAndExecutingLocksDoNotFlickerBadges()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter);
            var actions = new NarrativeActionController(narrative, new[] {
                new NarrativeActionDefinition { actionKey = "visit", type = NarrativeActionType.Once, resultEvents = new List<string> { "story" } },
                new NarrativeActionDefinition { actionKey = "ordinary" } });
            actions.Mount("cafe", "visit"); actions.Mount("market", "ordinary");
            int changes = 0;
            actions.MapContentChanged += _ => changes++;
            Assert.That(narrative.QueueEvent("story"), Is.True);
            Assert.That(narrative.IsBusy, Is.True);
            actions.RefreshMapContent();
            Assert.That(actions.IsAvailable("visit", out _), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.True);
            Assert.That(changes, Is.Zero);
            presenter.complete();
            actions.RefreshMapContent();
            Assert.That(changes, Is.Zero);
            Assert.That(actions.Execute("visit"), Is.True);
            Assert.That(actions.IsExecuting, Is.True);
            Assert.That(narrative.IsBusy, Is.True);
            actions.RefreshMapContent();
            Assert.That(actions.IsAvailable("ordinary", out _), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(changes, Is.Zero, "Playback locks alone must not change the stable map signature.");
            presenter.complete();
            Assert.That(actions.HasUnseenAvailableActions("cafe"), Is.False, "Actual completion still clears the badge.");
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ContentNotificationsAreDeduplicatedAndNeverMarkSeen()
        {
            var actions = new NarrativeActionController(Controller(new Presenter()), new[] { new NarrativeActionDefinition { actionKey = "visit" } });
            int notifications = 0; actions.MapContentChanged += _ => notifications++;
            actions.Mount("cafe", "visit"); int initial = notifications;
            actions.RefreshMapContent(); actions.RefreshMapContent();
            Assert.That(notifications, Is.EqualTo(initial)); Assert.That(actions.HasUnseenActions("cafe"), Is.True);
            actions.ConfirmActionDisplayed("cafe", actions.GetActionInstanceKey("cafe", "visit"));
            Assert.That(notifications, Is.EqualTo(initial + 1));
        }
    }
}
