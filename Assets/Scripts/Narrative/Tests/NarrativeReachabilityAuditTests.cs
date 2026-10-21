using System;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeReachabilityAuditTests
    {
        private static NarrativeConfiguration Config(string key = "route.eligible.rose")
        {
            var c = new NarrativeConfiguration();
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = key, initialValue = false });
            c.conditions.Add(new NarrativeConditionDefinition
            {
                conditionId = "gate.id", ownerType = "event", ownerId = "story.root",
                groupIndex = 1, condType = "BOOL_EQ", target = key, value = "true"
            });
            return c;
        }

        [TestCase("route.eligible.rose")]
        [TestCase("construction.pending")]
        [TestCase("integration.ready")]
        [TestCase("blocked.source")]
        public void ReportsMissingProducerWithOwnerFactAndConditionEvidence(string key)
        {
            var c = Config(key);
            var finding = NarrativeReachabilityAudit.Inspect(c).Single();
            Assert.That(finding.owner, Is.EqualTo("story.root"));
            Assert.That(finding.key, Is.EqualTo(key));
            Assert.That(finding.reason, Does.Contain("gate.id").And.Contain("initially false")
                .And.Contain("SET_FLAG/SET_SWITCH").And.Contain("Evidence only"));
            Assert.That(c.conditionKeys.Single().initialValue, Is.False);
            Assert.That(c.results, Is.Empty);
        }

        [TestCase(NarrativeResultType.SetFlag, "route.eligible.rose")]
        [TestCase(NarrativeResultType.SetFlag, "flag:route.eligible.rose")]
        [TestCase(NarrativeResultType.SetSwitch, "route.eligible.rose")]
        [TestCase(NarrativeResultType.SetSwitch, "switch:route.eligible.rose")]
        public void AnyTrueProducerSuppressesMissingProducerHint(NarrativeResultType type, string target)
        {
            var c = Config();
            c.results.Add(new NarrativeResultDefinition
            { resultId = "write", eventId = "unproven.owner", resultType = type, target = target, value = "TRUE" });
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
        }

        [TestCase(NarrativeResultType.SetFlag, "false")]
        [TestCase(NarrativeResultType.SetSwitch, "false")]
        [TestCase(NarrativeResultType.SetFlag, "1")]
        [TestCase(NarrativeResultType.ClearFlag, "true")]
        [TestCase(NarrativeResultType.BarSet, "true")]
        public void FalseInvalidAndUnrelatedEffectsAreNotTrueProducers(NarrativeResultType type, string value)
        {
            var c = Config();
            c.results.Add(new NarrativeResultDefinition
            { resultType = type, target = c.conditions[0].target, value = value });
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Has.Count.EqualTo(1));
        }

        [TestCase("route.rose")]
        [TestCase("route.eligibility.rose")]
        [TestCase("constructional.pending")]
        [TestCase("not:construction.pending")]
        [TestCase("story.finished")]
        public void DoesNotAuditUnrelatedOrNegatedKeys(string key)
            => Assert.That(NarrativeReachabilityAudit.Inspect(Config(key)), Is.Empty);

        [Test]
        public void RequiresExplicitFalseDeclarationAndValidPositiveBoolCondition()
        {
            var c = Config();
            c.conditionKeys.Clear();
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = c.conditions[0].target, initialValue = true });
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
            c.conditionKeys[0].initialValue = false;
            c.conditions[0].value = "false";
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
            c.conditions[0].value = "true";
            c.conditions[0].notFlag = true;
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
            c.conditions[0].notFlag = false;
            c.conditions[0].op = "!=";
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
            c.conditions[0].op = null;
            c.conditions[0].condType = "EVENT_HAPPENED";
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
        }

        [Test]
        public void AlternativeGroupsAndEndingsDoNotTurnEvidenceIntoGlobalVerdict()
        {
            var c = Config();
            c.conditions[0].ownerType = "ending";
            c.conditions.Add(new NarrativeConditionDefinition
            { conditionId = "alternative", ownerType = "ending", ownerId = "story.root", groupIndex = 2,
                condType = "BOOL_EQ", target = "known.ready", value = "true" });
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "known.ready", initialValue = true });
            c.endings.Add(new NarrativeEndingDefinition { endingId = "unrelated", enabled = true });
            var findings = NarrativeReachabilityAudit.Inspect(c);
            Assert.That(findings, Has.Count.EqualTo(1));
            Assert.That(findings[0].reason, Does.Contain("alternative groups are not evaluated"));
            Assert.That(findings[0].owner, Is.EqualTo("story.root"));
        }

        [Test]
        public void ReportsNonEmptyBlockedByMetadataWithoutRequiringConditionKey()
        {
            var c = new NarrativeConfiguration();
            var e = new NarrativeEventDefinition { eventId = "blocked.event" };
            e.eventProperties.Add(new NarrativeEventProperty { property = "blockedBy", value = "source.pending; integration.owner" });
            e.eventProperties.Add(new NarrativeEventProperty { property = "blockedBy", value = "  " });
            e.eventProperties.Add(new NarrativeEventProperty { property = "other", value = "ignored" });
            e.eventProperties.Add(null);
            c.events.Add(e);
            var finding = NarrativeReachabilityAudit.Inspect(c).Single();
            Assert.That(finding.owner, Is.EqualTo("blocked.event"));
            Assert.That(finding.key, Is.EqualTo("blockedBy"));
            Assert.That(finding.reason, Does.Contain("source.pending; integration.owner"));
        }

        [Test]
        public void NullRowsAndAbsentOptionalListsAreSafeButNullConfigurationIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => NarrativeReachabilityAudit.Inspect(null));
            var c = new NarrativeConfiguration();
            c.conditionKeys.Add(null);
            c.conditions.Add(null);
            c.results.Add(null);
            c.events.Add(null);
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
            c.conditionKeys = null; c.conditions = null; c.results = null; c.events = null;
            Assert.That(NarrativeReachabilityAudit.Inspect(c), Is.Empty);
        }

        [Test]
        public void FindingUsesSerializablePublicFields()
        {
            var type = typeof(NarrativeReachabilityAudit.Finding);
            Assert.That(Attribute.IsDefined(type, typeof(SerializableAttribute)), Is.True);
            Assert.That(type.GetFields().Select(field => field.Name), Is.EquivalentTo(new[] { "owner", "key", "reason" }));
        }
    }
}
