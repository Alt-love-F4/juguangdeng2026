using System;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class PresentationCheckGuardTests
    {
        private sealed class HeldPresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) { }
        }

        [Test]
        public void ChurchChecksAndChoiceGroupsRemainLocalToOneRoot()
        {
            var config = LubanNarrativeLoader.Load();
            var root = config.events.Single(e => e.eventId == "church.1104.s1");
            Assert.That(root.eventProperties.Any(p => p.property == "blockedBy"), Is.True);
            Assert.That(config.results.Any(r => r.eventId == root.eventId), Is.False);
            root.eventProperties.RemoveAll(p => p.property == "blockedBy");
            root.entry = NarrativeEntryPoint.Immediate;
            using (var session = new NarrativeDebugSession(config, new HeldPresenter()))
            {
                session.SetDate(30);
                session.EstablishBaseline();
                int reads = 0;
                int perception = 3, wisdom = 4;
                session.Controller.AbilityReader = id =>
                {
                    reads++;
                    return id == "perception" ? (true, perception)
                        : id == "wisdom" ? (true, wisdom) : (false, 0);
                };
                Assert.That(session.RequestEvent(root.eventId), Is.True);
                foreach (var group in new[] { "fee.one", "route.far", "night.ask" })
                    Assert.That(session.SelectChoice(root.eventId + "." + group), Is.True);
                Assert.That(session.SelectChoice(root.eventId + ".route.near"), Is.False);
                foreach (var check in new[] { "morning.perception", "old.perception", "street.wisdom" })
                    Assert.That(session.Controller.ReadPresentationCondition("check:" + root.eventId + "." + check), Is.True);
                perception = 0; wisdom = 0;
                foreach (var check in new[] { "morning.perception", "old.perception", "street.wisdom" })
                    Assert.That(session.Controller.ReadPresentationCondition("not:check:" + root.eventId + "." + check), Is.False);
                Assert.That(reads, Is.EqualTo(3));
                Assert.That(session.Controller.SelectedChoices.Count, Is.EqualTo(3));
                Assert.That(session.Controller.State.HasHappenedEvent(root.eventId), Is.False);
                Assert.That(session.Controller.State.TryGetCondition("story.church.1104.s1.completed", out bool completed)
                    && completed, Is.False);
                Assert.That(session.Controller.ActiveAbilityCheck, Is.Null);
            }
            var fresh = LubanNarrativeLoader.Load().events.Single(e => e.eventId == root.eventId);
            Assert.That(fresh.eventProperties.Any(p => p.property == "blockedBy"), Is.True);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void PositiveAndNegativePageGuardsShareOneInlineSnapshot(int value, bool passed)
        {
            var config = LubanNarrativeLoader.Load();
            config.bars.Clear(); config.triggers.Clear(); config.endings.Clear();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 9999,
                initValue = 2, advanceMode = NarrativeAdvanceMode.Monotonic });
            var controller = new NarrativeController(config, new HeldPresenter());
            controller.SetOfficialDay(2); controller.ProcessOfficialDay(2);
            controller.State.SetCondition("story.oct07.s1.completed", true);
            int reads = 0;
            controller.AbilityReader = _ => { reads++; return (true, value); };
            Assert.That(controller.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.True);
            Assert.That(controller.ReadPresentationCondition("check:oct07.s2.perception"), Is.EqualTo(passed));
            value = passed ? 1 : 2;
            Assert.That(controller.ReadPresentationCondition("not:check:oct07.s2.perception"), Is.EqualTo(!passed));
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(controller.ActiveAbilityCheck, Is.Null);
            Assert.That(controller.State.GetCondition("story.oct07.s2.completed"), Is.False);
            Assert.Throws<InvalidOperationException>(() => controller.ReadPresentationCondition("check:missing"));
        }
    }
}
