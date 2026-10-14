using System;
using NUnit.Framework;
using Spotlight.Narrative;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeConfigurationValidatorTests
    {
        [Test] public void RejectsDuplicateIdsAndNonStandardRange()
        {
            var c = new NarrativeConfiguration();
            c.objects.Add(new NarrativeObjectDefinition { objId = "world" });
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.a", ownerId = "world", min = 0, max = 15, initValue = 1 });
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.a", ownerId = "world", min = 0, max = 10, initValue = 1 });
            var errors = NarrativeConfigurationValidator.Validate(c);
            Assert.That(errors.Exists(x => x.Contains("duplicate ID")), Is.True);
            Assert.That(errors.Exists(x => x.Contains("range must")), Is.True);
        }

        [Test] public void GateConditionPreventsAnchor()
        {
            var c = new NarrativeConfiguration();
            c.objects.Add(new NarrativeObjectDefinition { objId = "world" });
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", ownerId = "world", min = 1, max = 9999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            c.events.Add(new NarrativeEventDefinition { eventId = "E" });
            c.triggers.Add(new NarrativeTriggerDefinition { triggerId = "T", barKey = "world.calendar", eventId = "E", threshold = 2 });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "C", ownerType = "event", ownerId = "E" });
            Assert.That(NarrativeEventByBarWindow.IsAnchor(c, c.events[0]), Is.False);
        }

        [Test] public void ActualGeneratedLubanDataLoadsAndValidates()
        {
            var c = LubanNarrativeLoader.Load();
            Assert.That(c.objects.Exists(x => x.objId == "world"), Is.True);
            Assert.That(c.eventDates.Count, Is.GreaterThan(0));
            Assert.That(c.eventDates[0].rowIndex, Is.EqualTo(0));
            Assert.That(NarrativeConfigurationValidator.Validate(c), Is.Empty);
            Assert.That(c.texts.Exists(x => x.key == "ev.chen.tea.p1"), Is.True);
            Assert.That(c.setups[0].parameters.Count, Is.GreaterThan(0));
            Assert.That(c.templates.Count, Is.EqualTo(9));
            Assert.That(c.eventDates[0].eventId, Is.EqualTo("EV_TALK_CAFE"));
            Assert.That(c.eventDates[0].day, Is.EqualTo(3));
            Assert.That(c.triggers.Exists(x => x.eventId == "EV_TALK_CAFE"), Is.False);
            Assert.That(NarrativeEventByBarWindow.IsAnchor(c, c.events.Find(x => x.eventId == "EV_TALK_CAFE")), Is.True);
        }
        [Test] public void SharedCounterRequiresConsistentResetOnExit()
        {
            var c = new NarrativeConfiguration();
            c.objects.Add(new NarrativeObjectDefinition { objId = "world" });
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", ownerId = "world", min = 1, max = 9999, initValue = 1 });
            c.events.Add(new NarrativeEventDefinition { eventId = "E", eventKind = NarrativeEventKind.World });
            c.triggers.Add(new NarrativeTriggerDefinition { triggerId = "T1", barKey = "world.calendar", threshold = 3, eventId = "E", resetOnExit = true });
            c.triggers.Add(new NarrativeTriggerDefinition { triggerId = "T2", barKey = "world.calendar", threshold = 3, eventId = "E", resetOnExit = false });
            Assert.That(NarrativeConfigurationValidator.Validate(c).Exists(x => x.Contains("resetOnExit must be identical")), Is.True);
            c.triggers[1].resetOnExit = true;
            Assert.That(NarrativeConfigurationValidator.Validate(c).Exists(x => x.Contains("resetOnExit must be identical")), Is.False);
        }
        [Test] public void PlaySetupRejectsChoiceTransaction()
        {
            var c = new NarrativeConfiguration();
            c.templates.Add(new NarrativeTemplateDefinition { tplName = "ChoiceTpl", atomSeq = "A_CHOICE", slotDef = "opts:optionList" });
            c.setups.Add(new NarrativeSetupParamsDefinition { setupKey = "choiceSetup", tplName = "ChoiceTpl", slotValues = "opts=group" });
            c.events.Add(new NarrativeEventDefinition { eventId = "E", eventKind = NarrativeEventKind.World });
            c.results.Add(new NarrativeResultDefinition { resultId = "R", eventId = "E", resultType = NarrativeResultType.PlaySetup, target = "choiceSetup" });
            Assert.That(NarrativeConfigurationValidator.Validate(c).Exists(x => x.Contains("PLAY_SETUP cannot invoke choice setup")), Is.True);
        }
        [Test] public void InvalidEffectsRejectMissingBarAndNonNumericValue()
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId = "E", eventKind = NarrativeEventKind.World });
            c.results.Add(new NarrativeResultDefinition { resultId = "R", eventId = "E", resultType = NarrativeResultType.BarAdd, target = "bar:missing", value = "bad" });
            var errors = NarrativeConfigurationValidator.Validate(c);
            Assert.That(errors.Exists(x => x.Contains("unknown bar target")), Is.True);
            Assert.That(errors.Exists(x => x.Contains("numeric value required")), Is.True);
        }
        [Test] public void RejectsUnknownBoolKeyDeathClearAndEmptyDisabledEnding()
        {
            var c = new NarrativeConfiguration();
            c.texts.Add(new NarrativeTextDefinition { key = "title", content = "Title" });
            c.events.Add(new NarrativeEventDefinition { eventId = "E", presentationClass = "Notice", eventKind = NarrativeEventKind.World });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "npc.dead", nameText = "title", onlySetTrue = true });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "C", ownerType = "event", ownerId = "E", groupIndex = 1, condType = "BOOL_EQ", target = "typo", value = "false" });
            c.results.Add(new NarrativeResultDefinition { resultId = "R", eventId = "E", resultType = NarrativeResultType.ClearFlag, target = "npc.dead", value = "false" });
            c.endings.Add(new NarrativeEndingDefinition { endingId = "end", enabled = false, titleText = "title", messageText = "title" });
            var errors = NarrativeConfigurationValidator.Validate(c);
            Assert.That(errors.Exists(x => x.Contains("Condition C:") && x.Contains("unknown bool key typo")), Is.True);
            Assert.That(errors.Exists(x => x.Contains("Result R:") && x.Contains("cannot clear")), Is.True);
            Assert.That(errors.Exists(x => x.Contains("Ending end:") && x.Contains("requires condition rows")), Is.True);
        }

        [TestCase("FLAG_SET", "true")]
        [TestCase("CHANCE_PCT", "0.5")]
        [TestCase("ITEM_GTE", "-1")]
        [TestCase("ITEM_GTE", "1.5")]
        [TestCase("ACTION_COMPLETED", "true")]
        public void RejectsUnsupportedOrMalformedCondition(string type, string value)
        {
            var c = new NarrativeConfiguration();
            c.actions.Add(new NarrativeActionDefinition { actionKey = "a" });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "C", ownerType = "action", ownerId = "a", groupIndex = 1, condType = type, target = "a", value = value });
            Assert.That(NarrativeConfigurationValidator.Validate(c).Exists(x => x.StartsWith("Condition C:")), Is.True);
        }

        [TestCase("0")]
        [TestCase("-2")]
        [TestCase("1.5")]
        [TestCase("2147483648")]
        public void RejectsInvalidItemResultQuantity(string quantity)
        {
            var c = new NarrativeConfiguration();
            c.results.Add(new NarrativeResultDefinition { resultId = "R", resultType = NarrativeResultType.ItemAdd, target = "medicine", value = quantity });
            Assert.That(NarrativeConfigurationValidator.Validate(c).Exists(x => x.Contains("Result R:") && x.Contains("positive integer")), Is.True);
        }

        [Test] public void RejectsLegacyModifiersAndWarnsAboutDuplicateDeliveryCost()
        {
            var c = new NarrativeConfiguration();
            c.actions.Add(new NarrativeActionDefinition { actionKey = "a", deliveryMode = NarrativeDeliveryMode.Cumulative, progressTarget = 5, requirements = "Item(medicine,2)", resultEvents = new System.Collections.Generic.List<string> { "E" } });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "C", ownerType = "action", ownerId = "a", groupIndex = 1, condType = "ITEM_GTE", target = "medicine", value = "1", op = ">=", notFlag = true });
            c.results.Add(new NarrativeResultDefinition { resultId = "R", eventId = "E", resultType = NarrativeResultType.ItemRemove, target = "medicine", value = "2" });
            Assert.That(NarrativeConfigurationValidator.Validate(c).Exists(x => x.Contains("op/notFlag are obsolete")), Is.True);
            Assert.That(NarrativeConfigurationValidator.Warnings(c), Has.Count.EqualTo(1));
        }

        [Test] public void ReverseSuggestionsAreLubanRowsAndDoNotMutateConfiguration()
        {
            var c = new NarrativeConfiguration();
            c.bars.Add(new NarrativeBarDefinition { barKey = "a.trust", ownerId = "a", targetId = "b", barType = NarrativeBarType.Relation, min = -2, max = 5, initValue = 1 });
            c.triggers.Add(new NarrativeTriggerDefinition { triggerId = "T", barKey = "a.trust", eventId = "E", threshold = 2 });
            string bars = NarrativeEventByBarWindow.BuildReverseRows(c, false);
            string triggers = NarrativeEventByBarWindow.BuildReverseRows(c, true);
            Assert.That(bars.StartsWith("\ttrust_reverse\tb\ta\trelation\t-2\t5\thighIsGood\tevent_driven\t1\t", StringComparison.Ordinal), Is.True);
            Assert.That(bars.Split('\t').Length, Is.EqualTo(15));
            Assert.That(triggers.StartsWith("\tT_reverse\tb.trust_reverse\t2\tup\tcross\tonce\tall\t", StringComparison.Ordinal), Is.True);
            Assert.That(triggers.Split('\t').Length, Is.EqualTo(12));
            Assert.That(c.bars.Count, Is.EqualTo(1));
            Assert.That(c.triggers.Count, Is.EqualTo(1));
        }
    }
}
