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
        public void SeenInstanceSurvivesRefreshDisappearanceAndSaveWithoutCompletingAction()
        {
            var presenter = new Presenter(); var narrative = Controller(presenter, true);
            var definition = new NarrativeActionDefinition { actionKey = "visit", mustExecute = true };
            var actions = new NarrativeActionController(narrative, new[] { definition });
            actions.Mount("cafe", "visit");
            Assert.That(actions.HasUnseenActions("cafe"), Is.False);
            narrative.State.SetCondition("map.active", true);
            Assert.That(actions.HasUnseenActions("cafe"), Is.True);
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
            Assert.That(actions.IsAvailable("costly", out _), Is.False);
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
            Assert.That(actions.IsAvailable("visit", out _), Is.False);
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
