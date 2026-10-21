using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeEndingDependenciesTests
    {
        private static NarrativeConfiguration Config()
        {
            var config = new NarrativeConfiguration();
            config.endings.Add(new NarrativeEndingDefinition { endingId = "ending", enabled = true });
            Gate(config, "ending", "ending", "route.rose");
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "route.rose" });
            return config;
        }

        private static void Gate(NarrativeConfiguration config, string type, string owner, string target,
            int group = 1, string value = "true")
        {
            config.conditions.Add(new NarrativeConditionDefinition
            {
                conditionId = owner + ".gate." + config.conditions.Count, ownerType = type, ownerId = owner,
                condType = "BOOL_EQ", target = target, value = value, groupIndex = group
            });
        }

        private static NarrativeResultDefinition Producer(NarrativeConfiguration config, string id, string owner,
            string target, NarrativeResultType type = NarrativeResultType.SetFlag, string value = "true")
        {
            var row = new NarrativeResultDefinition
            { resultId = id, eventId = owner, resultType = type, target = target, value = value };
            config.results.Add(row);
            return row;
        }

        private static string Explain(NarrativeConfiguration config) => string.Join("\n", NarrativeEndingDependencies.Explain(config, "ending"));

        [Test]
        public void RouteProducerTracesResultChoiceAndAliasedEventGates()
        {
            var config = Config();
            Producer(config, "write.route", "story", "route.rose");
            Gate(config, "result", "write.route", "choice:accept");
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "accept", groupKey = "decision" });
            Gate(config, "choice", "accept", "integration.ready");
            config.events.Add(new NarrativeEventDefinition { eventId = "story", conditionOwnerId = "story.alias" });
            Gate(config, "event", "story.alias", "construction.ready");
            Producer(config, "write.integration", "setup", "integration.ready");
            config.events.Add(new NarrativeEventDefinition { eventId = "setup" });
            Gate(config, "event", "setup", "blocked.author");
            var text = Explain(config);
            Assert.That(text, Does.Contain("result:write.route").And.Contain("choice:accept")
                .And.Contain("choice:accept gate").And.Contain("integration.ready")
                .And.Contain("event:story.alias gate").And.Contain("construction.ready")
                .And.Contain("result:write.integration").And.Contain("blocked.author"));
            Assert.That(text, Does.Contain("Possible dependency risk (this branch only)"));
        }

        [Test]
        public void FeasibleOrBranchIsNeverReportedAsWholeEndingFailure()
        {
            var config = Config();
            // First branch has a producer blocked by author metadata; second is true initially.
            Producer(config, "blocked.producer", "blocked.event", "route.rose");
            var blocked = new NarrativeEventDefinition { eventId = "blocked.event" };
            blocked.eventProperties.Add(new NarrativeEventProperty { property = "blockedBy", value = "integration.pending" });
            config.events.Add(blocked);
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "ready.alternative", initialValue = true });
            Gate(config, "ending", "ending", "ready.alternative", 2);
            var controller = new NarrativeController(config, new ImmediateNarrativePresenter());
            Assert.That(controller.EvaluateConditions("ending", "ending"), Is.True, "The fixture has a viable OR branch.");
            var text = Explain(config);
            Assert.That(text, Does.Contain("OR group 1").And.Contain("OR group 2")
                .And.Contain("initial=true").And.Contain("blockedBy=integration.pending")
                .And.Contain("this branch only").And.Contain("not a whole-ending verdict"));
            Assert.That(text.ToLowerInvariant(), Does.Not.Contain("unreachable").And.Not.Contain("impossible")
                .And.Not.Contain("不可达").And.Not.Contain("全结局阻塞"));
        }

        [TestCase(NarrativeResultType.SetFlag, "flag:route.rose")]
        [TestCase(NarrativeResultType.SetSwitch, "switch:route.rose")]
        public void RecognizesQualifiedTrueProducers(NarrativeResultType type, string target)
        {
            var config = Config();
            Producer(config, "writer", "story", target, type, "TRUE");
            config.events.Add(new NarrativeEventDefinition { eventId = "story" });
            Assert.That(Explain(config), Does.Contain("result:writer").And.Not.Contain("no configured SET_FLAG/SET_SWITCH true producer"));
        }

        [TestCase(NarrativeResultType.SetFlag, "false")]
        [TestCase(NarrativeResultType.SetSwitch, "1")]
        [TestCase(NarrativeResultType.ClearFlag, "true")]
        public void RejectsFalseInvalidAndUnrelatedProducers(NarrativeResultType type, string value)
        {
            var config = Config();
            Producer(config, "not.a.producer", "story", "route.rose", type, value);
            Assert.That(Explain(config), Does.Contain("no configured SET_FLAG/SET_SWITCH true producer")
                .And.Not.Contain("result:not.a.producer"));
        }

        [Test]
        public void ChoiceOwnedResultsTraceChoiceConditionsWithoutAnExplicitChoiceGuard()
        {
            var config = Config();
            Producer(config, "writer", "virtual.owner", "route.rose");
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "choose", resultOwnerId = "virtual.owner" });
            Gate(config, "choice", "choose", "construction.choice.ready");
            Assert.That(Explain(config), Does.Contain("choice:choose").And.Contain("construction.choice.ready")
                .And.Not.Contain("event:virtual.owner"));
        }

        [Test]
        public void AnyChoiceAndQueueSourcesRemainAlternatives()
        {
            var config = Config();
            Producer(config, "writer", "story", "route.rose");
            config.events.Add(new NarrativeEventDefinition { eventId = "story" });
            config.events.Add(new NarrativeEventDefinition { eventId = "source" });
            Producer(config, "enqueue", "source", "event:story", NarrativeResultType.QueueEvent, "");
            Gate(config, "result", "enqueue", "anyChoice:a;b");
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "a" });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "b" });
            Gate(config, "choice", "a", "integration.a");
            Gate(config, "choice", "b", "integration.b");
            Assert.That(Explain(config), Does.Contain("optional QUEUE_EVENT source")
                .And.Contain("alternative anyChoice member (OR)").And.Contain("integration.a").And.Contain("integration.b"));
        }

        [Test]
        public void NegativeAndUnsupportedConditionsDoNotInventTrueDependencies()
        {
            var config = Config();
            Producer(config, "writer", "story", "route.rose");
            Gate(config, "result", "writer", "not:construction.absent");
            Gate(config, "result", "writer", "not:choice:required", value: "false");
            Gate(config, "ending", "ending", "blocked.negative", value: "false");
            Gate(config, "ending", "ending", "invalid.modifier");
            config.conditions.Last().notFlag = true;
            Assert.That(Explain(config), Does.Not.Contain("fact:construction.absent")
                .And.Not.Contain("fact:blocked.negative").And.Not.Contain("fact:invalid.modifier")
                .And.Contain("choice:required").And.Contain("not interpreted"));
        }

        [Test]
        public void CyclesAndSharedDependenciesAreExpandedOnceAndOutputIsDeterministic()
        {
            var config = Config();
            Producer(config, "writer", "story", "route.rose");
            config.events.Add(new NarrativeEventDefinition { eventId = "story" });
            Gate(config, "event", "story", "route.rose");
            var first = NarrativeEndingDependencies.Explain(config, "ending");
            Assert.That(first.Count(line => line.Contains("fact:route.rose —")), Is.EqualTo(1));
            Assert.That(string.Join("\n", first), Does.Contain("shared/cycle reference; not expanded again"));
            Assert.That(NarrativeEndingDependencies.Explain(config, "ending"), Is.EqualTo(first));
            Assert.That(config.conditions.Count, Is.EqualTo(2));
            Assert.That(config.results.Count, Is.EqualTo(1));
            Assert.That(config.conditionKeys[0].initialValue, Is.False);
        }

        [Test]
        public void MoreThanThreeThousandNodesIsTruncatedWithoutRecursiveStackGrowth()
        {
            var config = Config();
            for (int i = 0; i < 3200; ++i) Gate(config, "ending", "ending", "fact." + i);
            var lines = NarrativeEndingDependencies.Explain(config, "ending");
            int[] ids = Regex.Matches(string.Join("\n", lines), @"\[(\d+)\]")
                .Cast<Match>().Select(match => int.Parse(match.Groups[1].Value)).Distinct().ToArray();
            Assert.That(ids.Length, Is.EqualTo(3000));
            Assert.That(ids.Max(), Is.EqualTo(3000));
            Assert.That(lines.Last(), Does.Contain("truncated").And.Contain("unknown"));
        }

        [Test]
        public void MissingDefinitionsAndNullOptionalRowsAreEvidenceNotCrashes()
        {
            Assert.Throws<ArgumentNullException>(() => NarrativeEndingDependencies.Explain(null, "ending"));
            Assert.Throws<ArgumentException>(() => NarrativeEndingDependencies.Explain(new NarrativeConfiguration(), " "));
            var config = Config();
            config.conditions.Add(null); config.results.Add(null); config.events.Add(null);
            config.choices.Add(null); config.conditionKeys.Add(null); config.endings.Add(null);
            Assert.That(Explain(config), Does.Contain("route.rose"));
            config.conditions = null; config.results = null; config.events = null;
            config.choices = null; config.conditionKeys = null; config.endings = null;
            Assert.That(Explain(config), Does.Contain("ending definition is missing").And.Contain("no ending conditions"));
        }
    }
}
