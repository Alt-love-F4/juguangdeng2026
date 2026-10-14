using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.Narrative.Tests
{
    public sealed class CafeTutorialContentTests
    {
        [Test]
        public void GeneratedTutorialContentIncludesOpeningBranchesAndFiveResults()
        {
            NarrativeConfiguration config = LubanNarrativeLoader.Load();
            Assert.That(config.openingLines.Count(line => line.presentationId == "first_day_cafe"), Is.GreaterThan(0));
            Assert.That(config.openingLines.Where(line => line.presentationId == "first_day_cafe")
                .Any(line => config.texts.Any(text => text.key == line.textKey && text.content.Contains("大堂"))), Is.True);
            Assert.That(config.abilityChecks.Any(check => check.checkId == "first_day_perception"), Is.True);
            Assert.That(config.TryGetCafeTutorialStep("first_cafe_tutorial", "perception_success", out _), Is.True);
            Assert.That(config.TryGetCafeTutorialStep("first_cafe_tutorial", "perception_failure", out _), Is.True);
            foreach (string id in new[] { "excellent", "good", "average", "bad", "refund" })
                Assert.That(config.TryGetCafeTutorialStep("first_cafe_tutorial", "result_" + id, out _), Is.True);
        }

        [Test]
        public void TutorialSessionAcceptsOnlyCurrentWorkstationSignalOnce()
        {
            var session = new CafeTutorialSession();
            int notifications = 0;
            session.StepCompleted += (_, _) => notifications++;
            session.NotifyTutorialStepCompleted("first_cafe_tutorial", "workstation_opened");
            Assert.That(session.HandoffReady, Is.False);
            session.Begin();
            session.NotifyTutorialStepCompleted("other", "workstation_opened");
            session.NotifyTutorialStepCompleted("first_cafe_tutorial", "make_coffee_base");
            Assert.That(session.WaitingForWorkstation, Is.True);
            session.NotifyTutorialStepCompleted("first_cafe_tutorial", "workstation_opened");
            session.NotifyTutorialStepCompleted("first_cafe_tutorial", "workstation_opened");
            Assert.That(session.WaitingForWorkstation, Is.False);
            Assert.That(session.HandoffReady, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }
    }
}
