using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeCafeVisitConfigurationTests
    {
        // All unrelated tables are empty, keeping these adapter tests independent of Resources.
        private static NarrativeConfiguration Adapt(JArray visits = null, JArray hooks = null)
        {
            return LubanNarrativeLoader.FromTables(new cfg.Tables(name =>
                name == "tbncscafevisit" ? visits ?? new JArray() :
                name == "tbncscafevisithook" ? hooks ?? new JArray() : new JArray()));
        }

        private static JObject Visit(string delay, string patience, string leave)
        {
            return new JObject
            {
                ["visitKey"] = "fixture_invalid", ["characterId"] = "npc",
                ["preferredSeatId"] = "missing_seat", ["seatedVisualPath"] = "missing/visual",
                ["arrivalDelaySeconds"] = delay, ["patienceSeconds"] = patience,
                ["leaveAfterServed"] = leave
            };
        }

        private static NarrativeController Spawn(NarrativeConfiguration config)
        {
            config.objects.Add(new NarrativeObjectDefinition { objId = "npc", objType = NarrativeObjectType.Npc });
            config.events.Add(new NarrativeEventDefinition { eventId = "spawn" });
            config.results.Add(new NarrativeResultDefinition
            {
                eventId = "spawn", resultType = NarrativeResultType.SpawnNpc,
                target = "obj:npc", value = "scene:cafe"
            });
            var controller = new NarrativeController(config);
            Assert.That(controller.QueueEvent("spawn"), Is.True);
            return controller;
        }

        [Test]
        public void EmptyTablesAllowNpcArrivalWithDefaults()
        {
            var config = Adapt();
            Assert.That(config.cafeVisits, Is.Empty);
            Assert.That(config.cafeVisitHooks, Is.Empty);
            var controller = Spawn(config);
            var visit = controller.GetCommittedCafeVisits().Visits[0];
            Assert.That(visit.UsesDefaultConfiguration, Is.True);
            Assert.That(visit.ArrivalAuthorized, Is.True);
            Assert.That(visit.ArrivalDelaySeconds, Is.Zero);
            Assert.That(visit.PatienceSeconds, Is.Null);
            Assert.That(visit.LeaveAfterServed, Is.True);
            Assert.That(visit.IsEnded, Is.False);
        }

        [TestCase("", "", "")]
        [TestCase("-4", "NaN", "invalid")]
        [TestCase("NaN", "-1", "invalid")]
        [TestCase("Infinity", "0", "invalid")]
        [TestCase("invalid", "Infinity", "invalid")]
        public void InvalidOrEmptyTimingUsesRuntimeDefaults(string delay, string patience, string leave)
        {
            var config = Adapt(new JArray(Visit(delay, patience, leave)));
            var definition = config.cafeVisits[0];
            Assert.That(definition.arrivalDelaySeconds, Is.Zero);
            Assert.That(definition.patienceSeconds, Is.Null);
            Assert.That(definition.leaveAfterServed, Is.True);
            Assert.That(definition.preferredSeatId, Is.EqualTo("missing_seat"));
            Assert.That(definition.seatedVisualPath, Is.EqualTo("missing/visual"));
            var visit = Spawn(config).GetCommittedCafeVisits().Visits[0];
            Assert.That(visit.ArrivalDelaySeconds, Is.Zero);
            Assert.That(visit.PatienceSeconds, Is.Null);
            Assert.That(visit.LeaveAfterServed, Is.True);
        }

        [Test]
        public void ValidTimingAndFalseLeaveArePreserved()
        {
            var definition = Adapt(new JArray(Visit("1.5", "30", "false"))).cafeVisits[0];
            Assert.That(definition.arrivalDelaySeconds, Is.EqualTo(1.5f));
            Assert.That(definition.patienceSeconds, Is.EqualTo(30f));
            Assert.That(definition.leaveAfterServed, Is.False);
        }

        [Test]
        public void MissingHookEventIsSkippedAndLaterHookCompletesExactlyOnce()
        {
            var hooks = JArray.Parse("[{ 'visitKey':'fixture_invalid', 'stage':'served', 'trigger':'auto', 'seq':1, 'eventId':'MISSING_EVENT' }, { 'visitKey':'fixture_invalid', 'stage':'served', 'trigger':'auto', 'seq':2, 'eventId':'valid_hook' }]");
            var config = Adapt(new JArray(Visit("-4", "NaN", "invalid")), hooks);
            Assert.That(config.cafeVisitHooks, Has.Count.EqualTo(2));
            config.events.Add(new NarrativeEventDefinition { eventId = "valid_hook" });
            var controller = Spawn(config);
            var fact = new NarrativeCafeVisitFeedback
            {
                visitInstanceId = controller.GetCommittedCafeVisits().Visits[0].VisitInstanceId,
                characterId = "npc", stage = "served", sequence = 1
            };
            int completions = 0;
            Assert.That(controller.SubmitCafeVisitFeedback(fact, () => completions++), Is.True);
            Assert.That(controller.IsCafeVisitFeedbackComplete(fact), Is.True);
            Assert.That(controller.State.HasHappenedEvent("valid_hook"), Is.True);
            Assert.That(completions, Is.EqualTo(1));
            controller.SubmitCafeVisitFeedback(fact);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(controller.CaptureSnapshot().cafeFeedback, Is.Empty);
        }
    }
}
