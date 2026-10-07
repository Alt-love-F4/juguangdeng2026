using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeCoreTests
    {
        private NarrativeConfiguration config;
        private sealed class Presenter : INarrativePresenter
        {
            public readonly List<string> Played = new List<string>();
            public readonly List<Action> Callbacks = new List<Action>();
            public void Play(NarrativeEventRequest request, Action completed) { Played.Add(request.Event.eventId); Callbacks.Add(completed); }
            public void Complete() => Callbacks[Callbacks.Count - 1]();
        }
        private sealed class DrainingPresenter : INarrativePresenter, INarrativePresentationDrain
        {
            public Action EventCallback, DrainCallback;
            public int Plays, Drains;
            public void Play(NarrativeEventRequest request, Action completed) { Plays++; EventCallback = completed; }
            public void Drain(Action completed) { Drains++; DrainCallback = completed; }
        }
        [SetUp] public void Setup()
        {
            config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "a", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.EventDriven });
            config.bars.Add(new NarrativeBarDefinition { barKey = "b", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.EventDriven });
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
        }
        [TearDown] public void Cleanup() => config = null;
        private void Event(string id, string bar = "a", int threshold = 1, int priority = 0, string reach = "all", NarrativeCrossType cross = NarrativeCrossType.Cross, NarrativeOnceType once = NarrativeOnceType.Repeat, bool reset = false)
        {
            config.events.Add(new NarrativeEventDefinition { eventId = id });
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = id + "_trigger", eventId = id, barKey = bar, threshold = threshold, priority = priority, direction = NarrativeTriggerDirection.Up, reachCount = reach, crossType = cross, onceType = once, resetOnExit = reset });
        }
        private void Result(string owner, NarrativeResultType type, string target, string value, int seq = 0) => config.results.Add(new NarrativeResultDefinition { eventId = owner, resultType = type, target = target, value = value, seq = seq });
        private void Condition(string owner, string type, string target, string value, int group = 0, string ownerType = "event", bool not = false) => config.conditions.Add(new NarrativeConditionDefinition { ownerId = owner, ownerType = ownerType, condType = type, target = target, value = value, groupIndex = group, notFlag = not });
        private static int Bar(NarrativeController c, string key) { Assert.That(c.State.TryGetBar(key, out int v)); return v; }

        [Test] public void ManualEventUsesConditionsQueueAndEventLifetime()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "manual", onceOnly = true });
            Condition("manual", "BAR_GTE", "bar:a", "2");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            Assert.That(c.QueueEvent("manual"), Is.False);
            c.SetBar("a", 2);
            Assert.That(c.QueueEvent("manual"), Is.True);
            Assert.That(p.Played, Is.EqualTo(new[] { "manual" }));
            p.Complete();
            Assert.That(c.QueueEvent("manual"), Is.False);
        }

        [Test] public void TriggerOnceStateDoesNotLockSharedEvent()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "shared" });
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = "first", eventId = "shared", barKey = "a", threshold = 1, direction = NarrativeTriggerDirection.Up, onceType = NarrativeOnceType.Once });
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = "second", eventId = "shared", barKey = "b", threshold = 1, direction = NarrativeTriggerDirection.Up, onceType = NarrativeOnceType.Once });
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.BeginTurn(); c.SetBar("a", 1); c.EndTurn(); p.Complete();
            c.BeginTurn(); c.SetBar("b", 1); c.EndTurn(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "shared", "shared" }));
        }

        [Test] public void BoundarySortsAcrossBarsAndUsesTableOrderForTies()
        {
            Event("z", "a", priority: 5); Event("a", "b", priority: 5); Event("high", "b", priority: 10);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.BeginTurn(); c.AddBar("a", 1); c.AddBar("b", 1);
            Assert.That(p.Played, Is.Empty);
            c.EndTurn(); p.Complete(); p.Complete(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "high", "z", "a" }));
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(2));
        }

        [Test] public void SharedCountSpecificWinsThenAllFallbackAndResetIsZero()
        {
            Event("first", reach: "1"); Event("fallback");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.AddBar("a", 1); c.EndTurn(); p.Complete();
            Assert.That(c.State.GetCounter("a@1@Up"), Is.EqualTo(1));
            c.BeginTurn(); c.SetBar("a", 0); c.SetBar("a", 1); c.EndTurn(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "first", "fallback" }));
            config.triggers[0].resetOnExit = true; config.triggers[1].resetOnExit = true;
            c.BeginTurn(); c.SetBar("a", 0); c.EndTurn();
            Assert.That(c.State.GetCounter("a@1@Up"), Is.Zero);
        }

        [Test] public void HoldChecksUnchangedTurnsAndOnceNeverResets()
        {
            Event("hold", threshold: 0, cross: NarrativeCrossType.Hold);
            Event("once", threshold: 0, cross: NarrativeCrossType.Hold, once: NarrativeOnceType.Once, reset: true);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.BeginTurn(); c.EndTurn(); p.Complete(); p.Complete();
            c.BeginTurn(); c.EndTurn(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "hold", "once", "hold" }));
        }

        [Test] public void ConditionsFreezeAtEnqueueAndDuplicateCallbacksSettleOnce()
        {
            Event("first", priority: 10); Event("second");
            Condition("second", "BAR_GTE", "bar:b", "1");
            Result("first", NarrativeResultType.BarSet, "bar:b", "0");
            Result("second", NarrativeResultType.BarAdd, "bar:b", "2");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetBar("b", 1); c.SetBar("a", 1); c.EndTurn();
            Action first = p.Callbacks[0]; first(); first();
            Assert.That(p.Played, Is.EqualTo(new[] { "first", "second" }));
            p.Complete(); p.Complete();
            Assert.That(Bar(c, "b"), Is.EqualTo(2)); Assert.That(Bar(c, "world.calendar"), Is.EqualTo(2));
        }

        [Test] public void SameEventIsDeduplicatedAcrossSameTurnAndChain()
        {
            Event("event");
            config.triggers.Add(new NarrativeTriggerDefinition { triggerId = "other", eventId = "event", barKey = "b", threshold = 1, direction = NarrativeTriggerDirection.Up });
            Result("event", NarrativeResultType.BarAdd, "bar:b", "1");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetBar("a", 1); c.EndTurn(); p.Complete();
            Assert.That(p.Played, Has.Count.EqualTo(1));
        }

        [Test] public void ActiveAndPendingRestoreReplayChoicesWithoutDoubleSettlement()
        {
            Event("active", priority: 10); Event("pending");
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "yes", groupKey = "g" });
            Result("yes", NarrativeResultType.BarAdd, "bar:b", "3");
            Result("active", NarrativeResultType.BarAdd, "bar:b", "1");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetBar("a", 1); c.EndTurn(); Assert.That(c.SelectChoice("yes"));
            NarrativeSnapshot saved = c.CaptureSnapshot();
            Assert.That(saved.active.selectedChoices, Is.EqualTo(new[] { "yes" })); Assert.That(saved.pending, Has.Count.EqualTo(1));
            Action oldCallback = p.Callbacks[0];
            c.RestoreSnapshot(saved); c.EndTurn(); oldCallback();
            Assert.That(Bar(c, "b"), Is.Zero);
            Assert.That(c.SelectChoice("yes")); p.Complete(); p.Complete();
            Assert.That(Bar(c, "b"), Is.EqualTo(4)); Assert.That(Bar(c, "world.calendar"), Is.EqualTo(2));
        }

        [Test] public void OldSnapshotReplacesStateAndDefaultsNewContent()
        {
            var c = new NarrativeController(config);
            c.State.SetFlag("old", true); c.State.MarkEventHappened("old"); c.SetBar("a", 7);
            c.RestoreSnapshot(new NarrativeSnapshot { bars = new List<NarrativeBarValue> { new NarrativeBarValue { key = "b", value = 4 } }, flags = new List<NarrativeBoolValue> { null, new NarrativeBoolValue() } });
            Assert.That(Bar(c, "a"), Is.Zero); Assert.That(Bar(c, "b"), Is.EqualTo(4));
            Assert.That(c.State.IsFlagSet("old"), Is.False); Assert.That(c.State.HasHappenedEvent("old"), Is.False);
        }

        [Test] public void DateRulesUseTimeSlotDayOneWeekAnd365DayYear()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "date" });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "date", day = 0, timeSlot = NarrativeTimeSlot.Night, repeatRule = NarrativeRepeatRule.Weekly });
            var p = new Presenter(); var c = new NarrativeController(config, p) { TimeSlot = NarrativeTimeSlot.Night };
            c.EndTurn(); p.Complete(); Assert.That(p.Played, Has.Count.EqualTo(1));
            c.SetBar("world.calendar", 8); c.EndTurn(); p.Complete(); Assert.That(p.Played, Has.Count.EqualTo(2));
            config.eventDates[0].repeatRule = NarrativeRepeatRule.Yearly; config.eventDates[0].day = 1;
            c.SetBar("world.calendar", 366); c.EndTurn(); p.Complete(); Assert.That(p.Played, Has.Count.EqualTo(3));
            Assert.That(c.SetBar("world.calendar", 0), Is.False);
            Assert.That(c.SetBar("world.calendar", 10000)); Assert.That(Bar(c, "world.calendar"), Is.EqualTo(9999));
        }

        [Test] public void ConditionsSupportPrefixesGroupsContextNotAndSavedRandom()
        {
            Condition("e", "BAR_GTE", "bar:a", "2", group: 1);
            Condition("e", "FLAG_SET", "flag:x", "true", group: 1);
            Condition("e", "LOCATION_EQ", "scene:cafe", "cafe", group: 2);
            var c = new NarrativeController(config) { Location = "cafe", TimeSlot = NarrativeTimeSlot.Night };
            Assert.That(c.EvaluateConditions("event", "e"));
            Assert.That(c.EvaluateCondition(new NarrativeConditionDefinition { condType = "TIMESLOT_EQ", value = "night" }));
            Assert.That(c.EvaluateCondition(new NarrativeConditionDefinition { condType = "ACTOR_IS", value = "npc" }, new NarrativeConditionContext { ActorId = "npc" }));
            Assert.That(c.EvaluateCondition(new NarrativeConditionDefinition { condType = "FLAG_SET", target = "flag:x", notFlag = true }));
            var chance = new NarrativeConditionDefinition { condType = "CHANCE_PCT", value = "0.4" };
            NarrativeSnapshot saved = c.CaptureSnapshot(); bool result = c.EvaluateCondition(chance);
            c.RestoreSnapshot(saved); Assert.That(c.EvaluateCondition(chance), Is.EqualTo(result));
            LogAssert.Expect(LogType.Warning, "[NCS] Unknown condition: TYPO");
            Assert.That(c.EvaluateCondition(new NarrativeConditionDefinition { condType = "TYPO", notFlag = true }), Is.False);
        }

        [Test] public void RelationsDerivedLocksSpawnAndEffectNotificationsPersist()
        {
            config.bars.Add(new NarrativeBarDefinition { barKey = "npc.trust", ownerId = "npc", targetId = "player", barType = NarrativeBarType.Relation, min = -2, max = 5, initValue = 1, reverseInitValue = -1, advanceMode = NarrativeAdvanceMode.EventDriven });
            config.bars.Add(new NarrativeBarDefinition { barKey = "sum", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.Derived, derivedSources = new List<string> { "a", "b" } });
            Event("start", priority: 10); Event("locked", "sum", threshold: 3);
            Result("start", NarrativeResultType.RelAdd, "rel:npc>player", "2");
            Result("start", NarrativeResultType.LockEvent, "event:locked", "");
            Result("start", NarrativeResultType.SpawnNpc, "obj:npc", "scene:cafe");
            Result("start", NarrativeResultType.BarAdd, "bar:b", "2");
            var p = new Presenter(); var c = new NarrativeController(config, p); int effects = 0; c.EffectRequested += _ => effects++;
            c.SetBar("a", 1); c.EndTurn(); p.Complete();
            Assert.That(Bar(c, "npc.trust"), Is.EqualTo(2));
            Assert.That(c.State.TryGetBar("player>npc", out int reverseTrust), Is.True);
            Assert.That(reverseTrust, Is.EqualTo(-1));
            Assert.That(c.AddBar("player>npc", 2), Is.True);
            Assert.That(Bar(c, "npc.trust"), Is.EqualTo(2));
            Assert.That(c.State.TryGetBar("player>npc", out reverseTrust), Is.True);
            Assert.That(reverseTrust, Is.EqualTo(1));
            Assert.That(Bar(c, "sum"), Is.EqualTo(3));
            Assert.That(c.State.Bars["sum"], Is.EqualTo(3)); Assert.That(c.SetBar("sum", 9), Is.False);
            Assert.That(c.State.IsEventLocked("locked")); Assert.That(p.Played, Has.Count.EqualTo(1));
            var saved = c.CaptureSnapshot(); c.RestoreSnapshot(saved);
            Assert.That(c.State.GetNpcLocation("npc"), Is.EqualTo("cafe")); Assert.That(effects, Is.EqualTo(4));
        }

        [Test] public void AllDateSlotsAreCollectedWithTheirOwnFrozenContext()
        {
            foreach (NarrativeTimeSlot slot in new[] { NarrativeTimeSlot.Morning, NarrativeTimeSlot.Noon, NarrativeTimeSlot.Night })
            {
                string id = slot.ToString(); config.events.Add(new NarrativeEventDefinition { eventId = id });
                config.eventDates.Add(new NarrativeEventDateDefinition { eventId = id, day = 1, timeSlot = slot, repeatRule = NarrativeRepeatRule.Once });
                Condition(id, "TIMESLOT_EQ", "slot", id.ToLowerInvariant());
            }
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.EndTurn(); p.Complete(); p.Complete(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "Morning", "Noon", "Night" }));
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(2));
        }

        [Test] public void DerivedThresholdChangesAndMalformedStateReadsFailSafely()
        {
            config.bars.Add(new NarrativeBarDefinition { barKey = "sum", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.Derived, derivedSources = new List<string> { "a", "b" } });
            Event("derived", "sum", threshold: 3);
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetBar("a", 2); c.SetBar("b", 1); c.EndTurn(); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "derived" }));
            Assert.That(c.State.ResolveRelation(null), Is.Null);
            Assert.That(c.AddBar(null, 1), Is.False);
            Assert.That(c.State.GetDefinition(null), Is.Null);
        }

        [Test] public void PlaySetupDrainBlocksNextEventAndDateAndRestoresPendingEffects()
        {
            Event("first", priority: 10); Event("next");
            Result("first", NarrativeResultType.PlaySetup, "setup:extra", "");
            var p = new DrainingPresenter(); var c = new NarrativeController(config, p);
            int effects = 0, turns = 0; c.EffectRequested += _ => effects++; c.TurnCompleted += () => turns++;
            c.SetBar("a", 1); c.EndTurn(); p.EventCallback();
            Assert.That(p.Plays, Is.EqualTo(1)); Assert.That(p.Drains, Is.EqualTo(1));
            Assert.That(turns, Is.Zero); Assert.That(Bar(c, "world.calendar"), Is.EqualTo(1));
            NarrativeSnapshot saved = c.CaptureSnapshot(); Assert.That(saved.pendingEffects, Has.Count.EqualTo(1));
            Action staleDrain = p.DrainCallback;
            c.RestoreSnapshot(saved); c.EndTurn(); staleDrain();
            Assert.That(p.Plays, Is.EqualTo(1)); Assert.That(effects, Is.EqualTo(2));
            Action drain = p.DrainCallback; drain(); drain();
            Assert.That(p.Plays, Is.EqualTo(2)); p.EventCallback();
            Assert.That(turns, Is.EqualTo(1)); Assert.That(Bar(c, "world.calendar"), Is.EqualTo(2));
            Assert.That(c.CaptureSnapshot().pendingEffects, Is.Empty);
        }

        [Test] public void ResultChainStopsBeforeDepthFour()
        {
            for (int i = 0; i < 5; i++)
            {
                string key = "chain" + i; config.bars.Add(new NarrativeBarDefinition { barKey = key, min = 0, max = 20, advanceMode = NarrativeAdvanceMode.EventDriven });
                Event("e" + i, key);
                if (i < 4) Result("e" + i, NarrativeResultType.BarAdd, "bar:chain" + (i + 1), "1");
            }
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetBar("chain0", 1); c.EndTurn(); p.Complete(); p.Complete(); p.Complete();
            LogAssert.Expect(LogType.Warning, "[NCS] Trigger depth exceeds 3: e4");
            p.Complete(); Assert.That(p.Played, Is.EqualTo(new[] { "e0", "e1", "e2", "e3" }));
        }
    }
}
