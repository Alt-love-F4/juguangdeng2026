using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeDailyContentTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public readonly List<string> Played = new List<string>();
            public Action Done;
            public event Action<string> Cancelled;
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); Done = completed; }
            public void Complete() { var done = Done; Done = null; done(); }
            public void Cancel() { Done = null; Cancelled?.Invoke("test"); }
        }
        private static NarrativeEventDefinition Event(string id, string kind, bool once = false)
            => new NarrativeEventDefinition { eventId = id, entry = NarrativeEntryPoint.MapAction, onceOnly = once,
                eventProperties = new List<NarrativeEventProperty> {
                    new NarrativeEventProperty { property = "dailyPool", value = "market" },
                    new NarrativeEventProperty { property = "dailyKind", value = kind } } };
        private static NarrativeActionDefinition Action(params string[] ids)
            => new NarrativeActionDefinition { actionKey = "market.daily", type = NarrativeActionType.Repeat,
                requirements = "ActionPoint(1)", resultEvents = new List<string>(ids) };

        [Test] public void FirstVisitPrecedesOneVariantAndRotationIsSavedInExistingSnapshot()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(Event("visit", "visit", true)); config.events.Add(Event("first", "ordinary")); config.events.Add(Event("second", "ordinary"));
            var presenter = new Presenter(); var core = new NarrativeController(config, presenter);
            var action = Action("visit", "first", "second"); var actions = new NarrativeActionController(core, new[] { action });
            Assert.That(actions.Execute(action.actionKey), Is.True);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "visit" }));
            presenter.Complete(); Assert.That(presenter.Played, Is.EqualTo(new[] { "visit", "first" }));
            presenter.Complete(); Assert.That(core.State.GetCounter("daily.playcount.first"), Is.EqualTo(1));
            Assert.That(actions.Execute(action.actionKey), Is.True);
            Assert.That(presenter.Played[presenter.Played.Count - 1], Is.EqualTo("second")); presenter.Complete();
            var snapshot = core.State.Capture(); core.State.SetCounter("daily.playcount.first", 0); core.State.Restore(snapshot);
            Assert.That(core.State.GetCounter("daily.playcount.first"), Is.EqualTo(1));
        }
        [Test] public void NewsReplacesRatherThanAppendsOrdinaryAndNoPreviewMutation()
        {
            var config = new NarrativeConfiguration(); config.events.Add(Event("ordinary", "ordinary")); config.events.Add(Event("news", "news", true));
            var core = new NarrativeController(config); var action = Action("ordinary", "news");
            Assert.That(core.TrySelectActionEvents(action, out var sequence), Is.True);
            Assert.That(sequence, Is.EqualTo(new[] { "news" }));
            Assert.That(core.State.GetCounter("daily.playcount.news"), Is.Zero);
            Assert.That(core.State.HasHappenedEvent("news"), Is.False);
        }
        [Test] public void EventConditionChangeNotifiesActionStateWithoutChangingDisplayedCards()
        {
            var config = new NarrativeConfiguration(); config.events.Add(Event("ordinary", "ordinary"));
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "pool.ready" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "pool.ready", ownerType = "event", ownerId = "ordinary",
                groupIndex = 1, condType = "BOOL_EQ", target = "pool.ready", value = "true" });
            var core = new NarrativeController(config); var action = Action("ordinary");
            var actions = new NarrativeActionController(core, new[] { action }); actions.Mount("market", action.actionKey);
            int stateChanges = 0, contentChanges = 0;
            actions.StateChanged += () => stateChanges++;
            actions.MapContentChanged += _ => contentChanges++;
            Assert.That(actions.GetDisplayableActions("market"), Is.EqualTo(new[] { action }));
            Assert.That(actions.IsAvailable(action.actionKey, out _), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            core.State.SetCondition("pool.ready", true);
            Assert.That(stateChanges, Is.EqualTo(1));
            Assert.That(contentChanges, Is.EqualTo(1), "Stable pool eligibility changes the map badge even when the card instance is unchanged.");
            Assert.That(actions.IsAvailable(action.actionKey, out _), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            Assert.That(core.State.HasHappenedEvent("ordinary"), Is.False);
            Assert.That(core.State.GetCounter("daily.playcount.ordinary"), Is.Zero);
            core.State.SetCondition("pool.ready", false);
            Assert.That(stateChanges, Is.EqualTo(2));
            Assert.That(actions.IsAvailable(action.actionKey, out _), Is.False);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False);
            Assert.That(contentChanges, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DailyPoolBadgeIgnoresBusyButStillHonorsCandidateConditionsAndCompletion(bool phaseEntry)
        {
            var config = new NarrativeConfiguration(); config.events.Add(Event("ordinary", "ordinary", true));
            config.events.Add(new NarrativeEventDefinition { eventId = "story",
                entry = phaseEntry ? NarrativeEntryPoint.MorningBeforeMap : NarrativeEntryPoint.Immediate });
            if (phaseEntry)
            {
                config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1,
                    advanceMode = NarrativeAdvanceMode.Monotonic });
                config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "story", day = 1,
                    timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Once });
            }
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "pool.ready", initialValue = true });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "pool.ready", ownerType = "event", ownerId = "ordinary",
                groupIndex = 1, condType = "BOOL_EQ", target = "pool.ready", value = "true" });
            var presenter = new Presenter(); var core = new NarrativeController(config, presenter);
            var action = Action("ordinary"); var actions = new NarrativeActionController(core, new[] { action });
            actions.Mount("market", action.actionKey);
            int changes = 0;
            actions.MapContentChanged += _ => changes++;
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(phaseEntry ? core.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap) : core.QueueEvent("story"), Is.True);
            Assert.That(core.IsBusy, Is.True);
            Assert.That(core.TrySelectActionEvents(action, out _), Is.False, "Execution preflight retains its busy guard.");
            actions.RefreshMapContent();
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(changes, Is.Zero);
            core.State.SetCondition("pool.ready", false);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False, "Ignoring busy must not bypass real event conditions.");
            Assert.That(changes, Is.EqualTo(1));
            core.State.SetCondition("pool.ready", true);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True);
            Assert.That(changes, Is.EqualTo(2));
            presenter.Complete(); actions.RefreshMapContent();
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(actions.Execute(action.actionKey), Is.True);
            Assert.That(actions.IsExecuting, Is.True);
            actions.RefreshMapContent();
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.True, "The active pool candidate is not completed merely because it is presenting.");
            Assert.That(changes, Is.EqualTo(2));
            presenter.Complete();
            Assert.That(actions.HasUnseenActions("market"), Is.True, "A Repeat card remains unread even when its pool has no candidates.");
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False, "Completed once-only pool events are still excluded by stable preflight.");
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test] public void UnavailableFirstVisitRejectsWithoutSpendingOrRecordingHistory()
        {
            var config = new NarrativeConfiguration();
            config.events.Add(Event("visit", "visit", true)); config.events.Add(Event("ordinary", "ordinary"));
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "visit.ready" });
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "visit.ready", ownerType = "event", ownerId = "visit",
                groupIndex = 1, condType = "BOOL_EQ", target = "visit.ready", value = "true" });
            var presenter = new Presenter(); var core = new NarrativeController(config, presenter);
            var action = Action("visit", "ordinary"); var actions = new NarrativeActionController(core, new[] { action });
            actions.Mount("market", action.actionKey);
            Assert.That(actions.HasUnseenActions("market"), Is.True);
            Assert.That(actions.HasUnseenAvailableActions("market"), Is.False, "An unavailable first visit must not be skipped for a badge.");
            Assert.That(core.ExplainMapActionEvent("ordinary").CanRequest, Is.True);
            Assert.That(core.TrySelectActionEvents(action, out _), Is.False);
            Assert.That(actions.Execute(action.actionKey), Is.False);
            Assert.That(actions.IsExecuting, Is.False);
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            Assert.That(presenter.Played, Is.Empty);
            Assert.That(core.State.HasHappenedEvent("visit"), Is.False);
            Assert.That(core.State.HasHappenedEvent("ordinary"), Is.False);
            Assert.That(core.State.GetCounter("daily.playcount.visit"), Is.Zero);
            Assert.That(core.State.GetCounter("daily.playcount.ordinary"), Is.Zero);
            Assert.That(actions.HasExecutedToday(action.actionKey), Is.False);
            core.State.SetCondition("visit.ready", true);
            Assert.That(actions.Execute(action.actionKey), Is.True);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "visit" }));
            presenter.Complete();
            Assert.That(presenter.Played, Is.EqualTo(new[] { "visit", "ordinary" }));
            presenter.Complete();
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(core.State.HasHappenedEvent("visit"), Is.True);
        }

        [Test] public void CancellationRefundsDailyActionPointAndDoesNotAdvanceHistory()
        {
            var config = new NarrativeConfiguration(); config.events.Add(Event("ordinary", "ordinary"));
            var presenter = new Presenter(); var core = new NarrativeController(config, presenter);
            var action = Action("ordinary"); var actions = new NarrativeActionController(core, new[] { action }, 5);
            Assert.That(actions.Execute(action.actionKey), Is.True); Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            presenter.Cancel(); Assert.That(actions.ActionPoints.Current, Is.EqualTo(5));
            Assert.That(core.State.GetCounter("daily.playcount.ordinary"), Is.Zero);
        }
    }
}
