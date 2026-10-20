using System;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeAutomationPlanTests
    {
        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999, initValue = 1 });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "route.neutral" });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "route.rose" });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "terminal" });
            c.endings.Add(new NarrativeEndingDefinition { endingId = "target", enabled = true });
            c.conditions.Add(new NarrativeConditionDefinition { conditionId = "end.c", ownerType = "ending", ownerId = "target",
                condType = "BOOL_EQ", target = "terminal", value = "true", groupIndex = 1 });
            c.choices.Add(new NarrativeChoiceDefinition { choiceId = "a", groupKey = "q", resultOwnerId = "a.r" });
            c.choices.Add(new NarrativeChoiceDefinition { choiceId = "b", groupKey = "q", resultOwnerId = "b.r" });
            return c;
        }
        private static void Result(NarrativeConfiguration c, string id, string owner, string target)
            => c.results.Add(new NarrativeResultDefinition { resultId = id, eventId = owner, resultType = NarrativeResultType.SetFlag, target = target, value = "true" });
        private static void Guard(NarrativeConfiguration c, string owner, string guard, string value = "true")
            => c.conditions.Add(new NarrativeConditionDefinition { conditionId = owner + "." + guard, ownerType = "result", ownerId = owner,
                condType = "BOOL_EQ", target = guard, value = value, groupIndex = 1 });

        [Test] public void ActualResultTargetAndRouteScoreBeatAuthoredOrderWithoutMutatingFacts()
        {
            var c = Config(); Result(c, "ra", "a.r", "route.rose"); Result(c, "rb", "b.r", "terminal");
            var controller = new NarrativeController(c);
            var plan = new NarrativeAutomationPlan { TargetEndingId = "target", TargetRoute = "neutral" };
            var scores = plan.ScoreChoices(c, controller, c.choices);
            Assert.That(scores.Single(x => x.ChoiceId == "b").Score, Is.GreaterThan(scores.Single(x => x.ChoiceId == "a").Score));
            Assert.That(controller.State.GetCondition("terminal"), Is.False);
            Assert.That(controller.SelectedChoices, Is.Empty);
        }

        [Test] public void RootOwnedResultChoiceGuardsAndCurrentWorldConditionsContributeNotNames()
        {
            var c = Config(); Result(c, "root.tail", "root", "terminal");
            Guard(c, "root.tail", "choice:b"); Guard(c, "root.tail", "not:route.rose");
            var controller = new NarrativeController(c);
            var scores = new NarrativeAutomationPlan { TargetEndingId = "target" }.ScoreChoices(c, controller, c.choices);
            Assert.That(scores.Single(x => x.ChoiceId == "b").Score, Is.GreaterThan(0));
            Assert.That(scores.Single(x => x.ChoiceId == "a").Score, Is.Zero);
            Assert.That(controller.State.GetCondition("terminal"), Is.False);
        }

        [Test] public void UnsatisfiedResultGuardsDoNotPromiseTargetAndStatusUsesActualEnding()
        {
            var c = Config(); Result(c, "rb", "b.r", "terminal"); Guard(c, "rb", "route.rose");
            var controller = new NarrativeController(c);
            var plan = new NarrativeAutomationPlan { TargetEndingId = "target" };
            Assert.That(plan.ScoreChoices(c, controller, c.choices).All(x => x.Score == 0), Is.True);
            var status = plan.Evaluate(controller);
            Assert.That(status.TargetReached, Is.False); Assert.That(status.ConditionProgress, Is.Zero);
            Assert.That(status.Conditions.Single().Passed, Is.False);
        }

        [Test] public void UnknownCheckUnderNegationDoesNotManufactureFailureEvidenceInScores()
        {
            var c = Config(); Result(c, "rb", "b.r", "terminal"); Guard(c, "rb", "not:check:not-yet-evaluated");
            var controller = new NarrativeController(c);
            var scores = new NarrativeAutomationPlan { TargetEndingId = "target" }.ScoreChoices(c, controller, c.choices);
            Assert.That(scores.Single(x => x.ChoiceId == "b").Score, Is.Zero);
            Assert.That(controller.State.GetCondition("terminal"), Is.False);
        }

        [Test] public void ExplicitPlanCannotSelectLockedOrAbsentChoice()
        {
            var c = Config(); var plan = new NarrativeAutomationPlan(); plan.ChoiceOverrides["q"] = "absent";
            Assert.Throws<InvalidOperationException>(() => plan.Select(new NarrativeAutomationChoiceContext
            { GroupKey = "q", Choices = c.choices, Scores = Array.Empty<NarrativeAutomationChoiceScore>() }));
        }

        [Test] public void AllEnabledProductionEndingsHavePlansWhoseOverridesExistAndNeverInjectHistory()
        {
            var c = LubanNarrativeLoader.Load();
            foreach (var ending in c.endings.Where(x => x.enabled))
            {
                var plan = NarrativeAutomationPlan.ForEnding(c, ending.endingId);
                Assert.That(plan.TargetEndingId, Is.EqualTo(ending.endingId));
                foreach (var pair in plan.ChoiceOverrides)
                    Assert.That(c.choices.Any(x => x.groupKey == pair.Key && x.choiceId == pair.Value), Is.True, pair.Value);
            }
            var neutralB = NarrativeAutomationPlan.ForEnding(c, "neutral.ending.b");
            Assert.That(neutralB.ChoiceOverrides.Values, Does.Contain("neutral.route.1122.s1.choice.future.b"));
            var neutralC = NarrativeAutomationPlan.ForEnding(c, "neutral.ending.c");
            Assert.That(neutralC.ChoiceOverrides.Values, Does.Contain("neutral.route.1112.s1.choice.membership.b"));
            Assert.That(neutralC.ChoiceOverrides.Values, Does.Contain("neutral.route.1122.s1.choice.future.a"));
            var neutralD = NarrativeAutomationPlan.ForEnding(c, "neutral.ending.d");
            Assert.That(neutralD.ChoiceOverrides.Values, Does.Contain("neutral.route.1112.s1.choice.membership.a"));
            Assert.That(c.events.Single(e => e.eventId == "neutral.route.1129.s1").eventProperties
                .Any(p => p.property == "blockedBy" && !string.IsNullOrWhiteSpace(p.value)), Is.False, "Wallet rule is user-approved, not an author blocker.");
            Assert.That(c.conditions.Any(g => g.ownerType == "event" && g.ownerId == "neutral.route.1129.s1"
                && g.target == "cafe.money" && g.condType == "BAR_BETWEEN" && g.value == "0,2147483647"), Is.True,
                "Unavailable wallet provider must fail closed.");
        }
    }
}
