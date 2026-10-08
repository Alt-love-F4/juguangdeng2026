using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeActionTests
    {
        private sealed class Presenter : INarrativePresenter
        {
            public readonly List<string> Played = new List<string>();
            private readonly List<Action> callbacks = new List<Action>();
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); callbacks.Add(completed); }
            public void Complete() { Action callback = callbacks[callbacks.Count - 1]; callbacks.RemoveAt(callbacks.Count - 1); callback(); }
        }

        private sealed class Inventory : INarrativeActionInventory
        {
            private readonly Dictionary<string, int> items = new Dictionary<string, int>();
            public int GetCount(string itemKey) => items.TryGetValue(itemKey, out int value) ? value : 0;
            public void Set(string key, int value) => items[key] = value;
            public bool TryConsume(string itemKey, int count) { if (GetCount(itemKey) < count) return false; items[itemKey] -= count; return true; }
            public bool TryAdd(string itemKey, int count) { items[itemKey] = GetCount(itemKey) + count; return true; }
        }

        private static NarrativeController Controller(Presenter presenter)
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.events.Add(new NarrativeEventDefinition { eventId = "EV_ACTION" });
            config.events.Add(new NarrativeEventDefinition { eventId = "EV_ACTION_2" });
            return new NarrativeController(config, presenter);
        }

        [Test]
        public void RequirementParserSupportsAllDeclaredForms()
        {
            List<NarrativeActionRequirement> result = NarrativeActionRequirementParser.Parse("ActionPoint(1);Item(tea,2);Bar(world.calendar,>=,2);Cond(flag.ready);NotCond(flag.done)");
            Assert.That(result, Has.Count.EqualTo(5));
            Assert.That(result[1].value, Is.EqualTo(2));
            Assert.That(result[2].op, Is.EqualTo(">="));
        }

        [Test]
        public void DailyActionConsumesPointAndCannotRepeatUntilNextDay()
        {
            var presenter = new Presenter();
            var controller = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily, requirements = "ActionPoint(1)", resultEvents = new List<string> { "EV_ACTION" } };
            var actions = new NarrativeActionController(controller, new[] { action }, 5);
            Assert.That(actions.Execute("daily"), Is.True);
            Assert.That(actions.ActionPoints.Current, Is.EqualTo(4));
            Assert.That(actions.IsAvailable("daily", out _), Is.False);
            presenter.Complete();
            actions.BeginDay(2);
            Assert.That(actions.IsAvailable("daily", out _), Is.True);
        }

        [Test]
        public void ActionEventsPlayInConfiguredOrder()
        {
            var presenter = new Presenter();
            var controller = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "once", type = NarrativeActionType.Once, resultEvents = new List<string> { "EV_ACTION", "EV_ACTION_2" } };
            var actions = new NarrativeActionController(controller, new[] { action });
            Assert.That(actions.Execute("once"), Is.True);
            Assert.That(presenter.Played, Is.EqualTo(new[] { "EV_ACTION", "EV_ACTION" }));
            presenter.Complete();
            presenter.Complete();
            Assert.That(actions.IsAvailable("once", out _), Is.False);
        }

        [Test]
        public void ActionStateRoundTrips()
        {
            var presenter = new Presenter();
            var controller = Controller(presenter);
            var action = new NarrativeActionDefinition { actionKey = "daily", type = NarrativeActionType.Daily };
            var actions = new NarrativeActionController(controller, new[] { action }, 5);
            actions.BeginDay(3);
            actions.ActionPoints.TryConsume(2);
            Assert.That(actions.Execute("daily"), Is.True);
            string json = actions.CaptureJson();
            var restored = new NarrativeActionController(controller, new[] { action }, 1);
            restored.RestoreJson(json);
            Assert.That(restored.CurrentDay, Is.EqualTo(3));
            Assert.That(restored.ActionPoints.Current, Is.EqualTo(3));
            Assert.That(restored.IsAvailable("daily", out _), Is.False);
        }
    }
}
