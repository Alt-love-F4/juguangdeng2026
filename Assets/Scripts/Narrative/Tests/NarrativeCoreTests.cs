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

        [Test] public void ManualRepeatableEventCanRunAgainWithoutEndingOrAdvancingDay()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "repeat" });
            Result("repeat", NarrativeResultType.BarAdd, "bar:a", "1");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            Assert.That(c.QueueEvent("repeat")); p.Complete();
            Assert.That(c.QueueEvent("repeat")); p.Complete();
            Assert.That(Bar(c, "a"), Is.EqualTo(2));
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(1));
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
            c.State.SetFlag("event.locked.old", true); c.State.MarkEventHappened("old"); c.SetBar("a", 7);
            c.RestoreSnapshot(new NarrativeSnapshot { bars = new List<NarrativeBarValue> { new NarrativeBarValue { key = "b", value = 4 } }, flags = new List<NarrativeBoolValue> { null, new NarrativeBoolValue() } });
            Assert.That(Bar(c, "a"), Is.Zero); Assert.That(Bar(c, "b"), Is.EqualTo(4));
            Assert.That(c.State.IsFlagSet("event.locked.old"), Is.False); Assert.That(c.State.HasHappenedEvent("old"), Is.False);
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

        [Test] public void ConditionsUseAndWithinGroupsAndOrBetweenGroupsWithoutConsumingItems()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "x" });
            Condition("e", "BAR_GTE", "bar:a", "2", group: 1);
            Condition("e", "BOOL_EQ", "x", "true", group: 1);
            Condition("e", "ITEM_GTE", "key", "2", group: 2);
            var inventory = new NarrativeMapInventory(); inventory.TryAdd("key", 2);
            var c = new NarrativeController(config) { Inventory = inventory };
            Assert.That(c.EvaluateConditions("event", "e"));
            Assert.That(inventory.GetCount("key"), Is.EqualTo(2));
            inventory.TryConsume("key", 1);
            c.SetBar("a", 2);
            Assert.That(c.EvaluateConditions("event", "e"), Is.False);
            c.State.SetCondition("x", true);
            Assert.That(c.EvaluateConditions("event", "e"));
        }

        [TestCase("FLAG_SET")][TestCase("FLAG_CLEAR")][TestCase("SWITCH_ON")]
        [TestCase("CHANCE_PCT")][TestCase("TIMESLOT_EQ")][TestCase("ACTOR_IS")]
        [TestCase("TARGET_IS")][TestCase("LOCATION_EQ")][TestCase("REACH_COUNT_EQ")]
        public void DeprecatedConditionsAreRejected(string type)
        {
            var c = new NarrativeController(config);
            Assert.Throws<InvalidOperationException>(() => c.EvaluateCondition(new NarrativeConditionDefinition { condType = type, target = "x", value = "true" }));
        }

        [Test] public void RelationsDerivedLocksSpawnAndEffectNotificationsPersist()
        {
            // A cafe arrival now validates the NPC catalog before reserving a visit.
            config.objects.Add(new NarrativeObjectDefinition { objId = "npc", objType = NarrativeObjectType.Npc });
            config.bars.Add(new NarrativeBarDefinition { barKey = "npc.trust", ownerId = "npc", targetId = "player", barType = NarrativeBarType.Relation, min = -2, max = 5, initValue = 1, reverseInitValue = -1, advanceMode = NarrativeAdvanceMode.EventDriven });
            config.bars.Add(new NarrativeBarDefinition { barKey = "sum", min = 0, max = 20, advanceMode = NarrativeAdvanceMode.Derived, derivedSources = new List<string> { "a", "b" } });
            Event("start", priority: 10); Event("locked", "sum", threshold: 3);
            Result("start", NarrativeResultType.RelAdd, "rel:npc>player", "2");
            Result("start", NarrativeResultType.LockEvent, "event:locked", "");
            Result("start", NarrativeResultType.SpawnNpc, "obj:npc", "scene:cafe");
            Result("start", NarrativeResultType.BarAdd, "bar:b", "2");
            var p = new Presenter(); var c = new NarrativeController(config, p); int effects = 0; c.EffectRequested += _ => effects++;
            c.SetBar("a", 1); c.EndTurn(); p.Complete();
            Assert.That(Bar(c, "npc.trust"), Is.EqualTo(3));
            Assert.That(c.State.TryGetBar("player>npc", out int reverseTrust), Is.True);
            Assert.That(reverseTrust, Is.EqualTo(-1));
            Assert.That(c.AddBar("player>npc", 2), Is.True);
            Assert.That(Bar(c, "npc.trust"), Is.EqualTo(3));
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
                // Date scheduling retains its time slot without a separate context condition.
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

        [Test] public void BoolDefaultsNotificationsDeathAndRestoreHaveOneSource()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready", initialValue = true });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "dead", onlySetTrue = true });
            var c = new NarrativeController(config); int changes = 0; c.State.ConditionsChanged += () => changes++;
            Assert.That(c.State.GetCondition("ready"));
            Assert.Throws<ArgumentException>(() => c.State.SetFlag("ready", true));
            Assert.That(c.State.SetCondition("ready", true), Is.False);
            c.State.SetCondition("ready", false); c.State.SetCondition("dead", true);
            Assert.That(changes, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => c.State.SetCondition("dead", false));
            c.RestoreSnapshot(new NarrativeSnapshot { flags = new List<NarrativeBoolValue> { new NarrativeBoolValue { key = "ready", value = false }, new NarrativeBoolValue { key = "event.locked.e", value = true } } });
            Assert.That(c.State.GetCondition("dead"), Is.False);
            Assert.That(c.State.GetCondition("ready"), Is.False);
            Assert.That(c.State.IsEventLocked("e"));
            var snapshot = c.CaptureSnapshot(); snapshot.flags.Add(new NarrativeBoolValue { key = "ready", value = true });
            c.RestoreSnapshot(snapshot);
            Assert.That(c.State.GetCondition("ready"), Is.False, "new bool false must override legacy true");
        }

        [Test] public void UnknownFalseAndNegationCannotBypassConfigurationErrors()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "known" });
            var c = new NarrativeController(config);
            Assert.Throws<InvalidOperationException>(() => c.EvaluateCondition(new NarrativeConditionDefinition { condType = "BOOL_EQ", target = "typo", value = "false" }));
            Assert.Throws<InvalidOperationException>(() => c.EvaluateCondition(new NarrativeConditionDefinition { condType = "BOOL_EQ", target = "known", value = "false", notFlag = true }));
        }

        [Test] public void AbilityCheckOwnerResultsJoinActiveEventTransaction()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "check-event" });
            config.events.Add(new NarrativeEventDefinition { eventId = "pass" });
            config.events.Add(new NarrativeEventDefinition { eventId = "fail" });
            config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = "test", successEventId = "pass", failureEventId = "fail", successResultOwnerId = "check-reward" });
            config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { conditionId = "c", checkId = "test", attributeId = "perception", threshold = 3, groupIndex = 1 });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "rewarded" });
            Result("check-reward", NarrativeResultType.SetFlag, "rewarded", "true");
            var presenter = new Presenter();
            var controller = new NarrativeController(config, presenter)
            {
                AbilityReader = _ => (true, 3),
                AbilityNameResolver = _ => "感知"
            };
            controller.QueueEvent("check-event");
            controller.EvaluateAbilityCheckForPresentation("test");
            presenter.Complete();
            Assert.That(controller.State.GetCondition("rewarded"), Is.True);
            Assert.That(presenter.Played, Does.Contain("pass"));
        }

        [Test] public void EventAndChoiceItemResultsPreflightTogetherAndDoNotPartiallyCommit()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" });
            config.events.Add(new NarrativeEventDefinition { eventId = "e" });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "yes", groupKey = "g" });
            Result("e", NarrativeResultType.SetFlag, "ready", "true");
            Result("e", NarrativeResultType.ItemRemove, "key", "2", 1);
            Result("yes", NarrativeResultType.ItemRemove, "key", "2");
            var inventory = new NarrativeMapInventory(); inventory.TryAdd("key", 3);
            var p = new Presenter(); var c = new NarrativeController(config, p) { Inventory = inventory };
            int completed = 0, failed = 0;
            c.EventCompleted += _ => completed++; c.EventFailed += (_, __) => failed++;
            c.QueueEvent("e"); c.SelectChoice("yes");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("\\[NCS\\] Event e result transaction failed:.*Insufficient items"));
            p.Complete();
            Assert.That(c.State.GetCondition("ready"), Is.False);
            Assert.That(inventory.GetCount("key"), Is.EqualTo(3));
            Assert.That(c.State.HasHappenedEvent("e"), Is.False);
            Assert.That(completed, Is.Zero); Assert.That(failed, Is.EqualTo(1));
            Assert.That(c.CaptureSnapshot().active, Is.Not.Null);
        }

        [Test] public void OrderedAddRemoveCommitsFinalBoolAndInventoryBeforeNotifications()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" });
            config.events.Add(new NarrativeEventDefinition { eventId = "e" });
            Result("e", NarrativeResultType.ItemAdd, "key", "3", 0);
            Result("e", NarrativeResultType.SetFlag, "ready", "true", 1);
            Result("e", NarrativeResultType.ItemRemove, "key", "2", 2);
            var inventory = new NarrativeMapInventory();
            var p = new Presenter(); var c = new NarrativeController(config, p) { Inventory = inventory };
            int changed = 0;
            inventory.Changed += () => { changed++; Assert.That(c.State.GetCondition("ready")); Assert.That(inventory.GetCount("key"), Is.EqualTo(1)); Assert.Throws<InvalidOperationException>(() => c.CaptureSnapshot()); };
            c.QueueEvent("e"); p.Complete();
            Assert.That(changed, Is.EqualTo(1)); Assert.That(c.State.HasHappenedEvent("e"));
        }

        private void Ending(string id, string key = "ready", int priority = 0)
        {
            config.endings.Add(new NarrativeEndingDefinition { endingId = id, enabled = true, priority = priority });
            Condition(id, "BOOL_EQ", key, "true", ownerType: "ending");
        }

        [Test] public void EndingSelectionIsDisabledUntilRestoreCompleteAndStableAcrossNotifications()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready", initialValue = true });
            Ending("z", priority: 1); Ending("b", priority: 2); Ending("a", priority: 2);
            var c = new NarrativeController(config); int selected = 0; c.EndingSelected += _ => selected++;
            Assert.That(c.CheckEndingsAtSafeBoundary(), Is.Null);
            c.SetEndingChecksEnabled(true);
            Assert.That(c.SelectedEndingId, Is.EqualTo("a"));
            c.NotifyConditionsChanged(); c.CheckEndingsAtSafeBoundary();
            Assert.That(selected, Is.EqualTo(1));
            var snapshot = c.CaptureSnapshot(); c.RestoreSnapshot(snapshot); c.SetEndingChecksEnabled(true);
            Assert.That(c.IsEnded); Assert.That(selected, Is.EqualTo(1));
            Assert.That(c.SetBar("a", 5), Is.False);
        }

        [Test] public void ExternalBoolChangeChecksEndingWithoutExplicitPump()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" }); Ending("end");
            var c = new NarrativeController(config); c.SetEndingChecksEnabled(true);
            c.State.SetCondition("ready", true);
            Assert.That(c.SelectedEndingId, Is.EqualTo("end"));
        }

        [Test] public void ExternalInventoryChangeChecksEndingWithoutExplicitPump()
        {
            config.endings.Add(new NarrativeEndingDefinition { endingId = "end", enabled = true });
            Condition("end", "ITEM_GTE", "key", "2", ownerType: "ending");
            var inventory = new NarrativeMapInventory();
            var c = new NarrativeController(config) { Inventory = inventory }; c.SetEndingChecksEnabled(true);
            inventory.TryAdd("key", 2);
            Assert.That(c.SelectedEndingId, Is.EqualTo("end"));
        }

        [Test] public void ExternalBoolBatchChecksOnlyFinalValueAndRestoreWaitsForEnable()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" }); Ending("end");
            var c = new NarrativeController(config); c.SetEndingChecksEnabled(true);
            c.BeginOperation(); c.State.SetCondition("ready", true); c.State.SetCondition("ready", false); c.EndOperation();
            Assert.That(c.IsEnded, Is.False);
            var snapshot = c.CaptureSnapshot(); c.RestoreSnapshot(snapshot);
            c.State.SetCondition("ready", true);
            Assert.That(c.IsEnded, Is.False);
            c.SetEndingChecksEnabled(true); Assert.That(c.IsEnded);
        }

        [Test] public void ActionScopeChecksFinalStateAfterAllEventsAndExtraPresentation()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" });
            Ending("end");
            config.events.Add(new NarrativeEventDefinition { eventId = "first" });
            config.events.Add(new NarrativeEventDefinition { eventId = "second" });
            Result("first", NarrativeResultType.SetFlag, "ready", "true");
            Result("second", NarrativeResultType.ClearFlag, "ready", "");
            Result("second", NarrativeResultType.PlaySetup, "setup:extra", "", 1);
            var p = new DrainingPresenter(); var c = new NarrativeController(config, p);
            c.SetEndingChecksEnabled(true); c.BeginOperation();
            c.QueueEvent("first"); p.EventCallback();
            Assert.That(c.IsEnded, Is.False);
            c.QueueEvent("second"); p.EventCallback(); c.EndOperation();
            Assert.That(c.IsEnded, Is.False);
            c.State.SetCondition("ready", true); c.CheckEndingsAtSafeBoundary();
            Assert.That(c.IsEnded, Is.False, "extra presentation is still running");
            p.DrainCallback();
            Assert.That(c.IsEnded);
        }

        [Test] public void CompletionCallbackWaitsForExtraPresentationAndObservesSelectedEnding()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" }); Ending("end");
            config.events.Add(new NarrativeEventDefinition { eventId = "e" });
            Result("e", NarrativeResultType.SetFlag, "ready", "true");
            Result("e", NarrativeResultType.PlaySetup, "setup:extra", "", 1);
            var p = new DrainingPresenter(); var c = new NarrativeController(config, p);
            bool callback = false; c.SetEndingChecksEnabled(true); c.BeginOperation();
            c.QueueEvent("e"); p.EventCallback(); c.EndOperation();
            c.RunAtSafeBoundary(() => { callback = true; Assert.That(c.IsEnded); });
            Assert.That(callback, Is.False); Assert.That(c.IsEnded, Is.False);
            p.DrainCallback(); Assert.That(callback); Assert.That(c.IsEnded);
        }

        [Test] public void SetThenClearDoesNotSelectTransientEnding()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready" }); Ending("end");
            config.events.Add(new NarrativeEventDefinition { eventId = "e" });
            Result("e", NarrativeResultType.SetFlag, "ready", "true", 0);
            Result("e", NarrativeResultType.ClearFlag, "ready", "", 1);
            var p = new Presenter(); var c = new NarrativeController(config, p); c.SetEndingChecksEnabled(true);
            c.QueueEvent("e"); p.Complete();
            Assert.That(c.IsEnded, Is.False); Assert.That(c.State.GetCondition("ready"), Is.False);
        }

        [Test] public void OfficialDayAndResumeDoNotAutomaticallyAdvanceCalendar()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "e" });
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetBar("world.calendar", 50);
            c.SetOfficialDay(8); c.QueueEvent("e");
            var snapshot = c.CaptureSnapshot(); c.RestoreSnapshot(snapshot); c.ResumePendingEvents(); p.Complete();
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(8));
            c.EndTurn(); Assert.That(Bar(c, "world.calendar"), Is.EqualTo(8));
        }

        [Test] public void FormalDailyScheduleRunsOncePerDayAndRestoresPendingWithoutReplay()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "daily" });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "daily", repeatRule = NarrativeRepeatRule.Daily });
            Result("daily", NarrativeResultType.BarAdd, "a", "1");
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.SetOfficialDay(1); Assert.That(p.Played, Is.Empty);
            c.RunAtSafeBoundary(() => Assert.That(c.ProcessOfficialDay(1)));
            var snapshot = c.CaptureSnapshot();
            Assert.That(snapshot.officialDayProcessed, Is.EqualTo(1));
            c.RestoreSnapshot(snapshot); c.SetOfficialDay(1); c.ResumePendingEvents(); p.Complete();
            Assert.That(c.ProcessOfficialDay(1), Is.False);
            Assert.That(Bar(c, "a"), Is.EqualTo(1));
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(1));
            c.SetOfficialDay(2); Assert.That(c.ProcessOfficialDay(2)); p.Complete();
            Assert.That(Bar(c, "a"), Is.EqualTo(2));
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(2));
        }

        [Test] public void LegacyFormalScheduleUsesAuthoritativeRestoredDayWithoutReplayingIt()
        {
            config.events.Add(new NarrativeEventDefinition { eventId = "daily" });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "daily", repeatRule = NarrativeRepeatRule.Daily });
            var p = new Presenter(); var c = new NarrativeController(config, p);
            c.RestoreSnapshot(new NarrativeSnapshot { bars = new List<NarrativeBarValue> { new NarrativeBarValue { key = "world.calendar", value = 50 } } });
            c.SetOfficialDay(8);
            Assert.That(c.ProcessOfficialDay(8), Is.False); Assert.That(p.Played, Is.Empty);
            c.SetOfficialDay(9); Assert.That(c.ProcessOfficialDay(9)); p.Complete();
            Assert.That(p.Played, Is.EqualTo(new[] { "daily" }));
            Assert.That(Bar(c, "world.calendar"), Is.EqualTo(9));
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
