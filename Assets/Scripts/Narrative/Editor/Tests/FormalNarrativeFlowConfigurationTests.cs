using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class FormalNarrativeFlowConfigurationTests
    {
        private static readonly string[] FormalRoots =
        {
            "oct07.s1", "oct07.s2", "neutral.1008.s1", "rose.1009.s1", "rose.1011.s1",
            "church.1009.s2", "revolution.1013.s2", "revolution.1014.s1", "revolution.1028.s1"
        };

        [Test]
        public void FormalRootsHaveAnEntranceAndCompletionBoundary()
        {
            NarrativeConfiguration configuration = LubanNarrativeLoader.Load();
            foreach (string eventId in FormalRoots)
            {
                NarrativeEventDefinition definition = configuration.events.SingleOrDefault(value => value.eventId == eventId);
                Assert.That(definition, Is.Not.Null, eventId);
                Assert.That(definition.eventProperties.Any(value => value != null && value.property == "blockedBy"), Is.False, eventId);
                bool hasRoute = configuration.eventDates.Any(value => value.eventId == eventId)
                    || configuration.actions.Any(value => value.resultEvents != null && value.resultEvents.Contains(eventId))
                    || configuration.results.Any(value => value.resultType == NarrativeResultType.QueueEvent
                        && NarrativeIntegrationReport.NormalizeEventTarget(value.target) == eventId
                        && configuration.events.Any(parent => parent.eventId == value.eventId));
                Assert.That(hasRoute, Is.True, "Missing formal route: " + eventId);
                Assert.That(configuration.results.Any(value => value.eventId == eventId
                    && value.resultType == NarrativeResultType.SetFlag
                    && value.target == "story." + eventId + ".completed"
                    && value.value == "true"), Is.True, "Missing root completion result: " + eventId);
            }
        }

        [Test]
        public void FormalDateRoutesUseTheExpectedDayAndPhase()
        {
            NarrativeConfiguration configuration = LubanNarrativeLoader.Load();
            AssertDate(configuration, "oct07.s2", 2, NarrativeTimeSlot.Night);
            AssertDate(configuration, "rose.1011.s1", 6, NarrativeTimeSlot.Morning);
            // Rose1009 completes from the day-4 map visit. AfterBusiness runs BEFORE that
            // map, so the ceremony must retain the same day's later BeforeNight entrance.
            AssertDate(configuration, "church.1009.s2", 4, NarrativeTimeSlot.Night);
            Assert.That(configuration.events.Single(value => value.eventId == "church.1009.s2").entry, Is.EqualTo(NarrativeEntryPoint.BeforeNight));
            Assert.That(configuration.eventDates.Any(value => value.eventId == "church.1009.s2" && value.timeSlot == NarrativeTimeSlot.Noon), Is.False);
            // The revolutionary continuation is reached at promotion-root settlement, never by
            // an independent date that could race ahead of the cafe interaction.
            Assert.That(configuration.eventDates.Any(value => value.eventId == "revolution.1013.s2"), Is.False);
            Assert.That(configuration.events.Single(value => value.eventId == "revolution.1013.s2").entry, Is.EqualTo(NarrativeEntryPoint.Immediate));
            AssertDate(configuration, "neutral.1013.s2", 8, NarrativeTimeSlot.Morning);
            Assert.That(configuration.results.Any(value => value.eventId == "neutral.1013.s2"
                && value.resultType == NarrativeResultType.QueueEvent
                && NarrativeIntegrationReport.NormalizeEventTarget(value.target) == "revolution.1013.s2"), Is.True);
            Assert.That(configuration.conditions.Any(value => value.ownerType == "event" && value.ownerId == "revolution.1013.s2"
                && value.target == "story.neutral.1013.s2.completed" && value.value == "true"), Is.True);
            AssertDate(configuration, "revolution.1014.s1", 9, NarrativeTimeSlot.Morning);
            AssertDate(configuration, "revolution.1028.s1", 23, NarrativeTimeSlot.Night);
        }

        [Test]
        public void CrossLineRoutesUseRealCompletionFacts()
        {
            NarrativeConfiguration configuration = LubanNarrativeLoader.Load();
            Assert.That(configuration.conditions.Any(value => value.ownerId == "church.1009.s2"
                && value.target == "story.rose.1009.s1.completed" && value.value == "true"), Is.True);
            Assert.That(configuration.conditions.Any(value => value.ownerId == "revolution.1014.s1"
                && value.target == "story.rose.1011.s1.completed" && value.value == "true"), Is.True);
            Assert.That(configuration.conditions.Any(value => value.ownerId == "rose.1011.s1"
                && value.target == "story.neutral.1010.s1.completed" && value.value == "true"), Is.True);
        }

        private static void AssertDate(NarrativeConfiguration configuration, string eventId, int day, NarrativeTimeSlot timeSlot)
        {
            Assert.That(configuration.eventDates.Any(value => value.eventId == eventId && value.day == day && value.timeSlot == timeSlot), Is.True,
                eventId + " must be scheduled at day " + day + " / " + timeSlot);
        }
    }
}
