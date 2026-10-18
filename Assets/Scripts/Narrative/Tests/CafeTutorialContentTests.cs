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
        public void TutorialSpokenLinesArePlayerDialogueNotProductionDirections()
        {
            var config = LubanNarrativeLoader.Load();
            var texts = config.texts.ToDictionary(text => text.key, text => text.content ?? "");
            var lines = config.openingLines.Where(line => line.presentationId.StartsWith("cafe_tutorial.")).ToArray();
            foreach (var line in lines)
            {
                string text = texts[line.textKey];
                Assert.That(text, Does.Not.Contain("演出说明"), line.textKey);
                Assert.That(text, Does.Not.Contain("配料表"), line.textKey);
                Assert.That(text, Does.Not.Contain("老太太"), line.textKey);
                Assert.That(text, Does.Not.Contain("太太"), line.textKey);
                Assert.That(text, Does.Not.Contain("→"), line.textKey);
                if (line.speaker == "right")
                {
                    Assert.That(text, Does.Not.Match(@"[\[\]#]|鼠标|左键|右键|高亮"), line.textKey);
                    Assert.That(text.Length, Is.LessThan(110), "Adele introduces the goal; workstation hints own detailed controls: " + line.textKey);
                }
            }
            Assert.That(texts["cafe.tutorial.customer_arrives.01"], Does.Contain("男人"));
            Assert.That(texts["cafe.tutorial.order_confirmed.02"], Does.Contain("大杯").And.Contain("不加糖"));
            Assert.That(texts["cafe.tutorial.make_coffee_base.02"], Does.Contain("研磨").And.Contain("装入杯中"));
            Assert.That(config.texts.Any(text => text.key.StartsWith("cafe.tutorial.source.")
                && text.content != null && text.content.Contains("演出说明")), Is.True, "Keep the manuscript archive, but do not play it.");
            Assert.That(lines.Any(line => line.textKey.StartsWith("cafe.tutorial.source.")), Is.False);
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

        [Test]
        public void TutorialCoffeeRequiresWorkstationHandoffThenWaitsForBusinessOnboarding()
        {
            var session = new CafeTutorialSession();
            int notifications = 0;
            session.StepCompleted += (_, _) => notifications++;
            Assert.That(session.BeginCoffee(), Is.False);
            session.Begin();
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal), Is.False);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.WorkstationSignal), Is.True);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal), Is.False);
            Assert.That(session.BeginCoffee(), Is.True);
            Assert.That(session.BeginCoffee(), Is.False);
            Assert.That(session.WaitingForCoffee, Is.True);
            Assert.That(session.NotifyTutorialStepCompleted("other", CafeTutorialSession.CoffeeSignal), Is.False);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal), Is.True);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal), Is.False);
            session.Begin();
            Assert.That(session.Completed, Is.False);
            Assert.That(session.WaitingForBusinessOnboarding, Is.True);
            Assert.That(session.WaitingForWorkstation, Is.False);
            Assert.That(session.WaitingForCoffee, Is.False);
            Assert.That(session.BeginCoffee(), Is.False);
            Assert.That(session.CurrentStepId, Is.Empty);
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void BusinessOnboardingCompletesOnlyAfterCoffeeAndOnlyOnce()
        {
            var session = new CafeTutorialSession();
            int notifications = 0;
            session.StepCompleted += (_, _) => notifications++;
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal), Is.False);
            session.Begin();
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal), Is.False);
            session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.WorkstationSignal);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal), Is.False);
            session.BeginCoffee();
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal), Is.False);
            session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal);
            Assert.That(session.NotifyTutorialStepCompleted("other", CafeTutorialSession.PostCoffeeSignal), Is.False);
            Assert.That(session.WaitingForBusinessOnboarding, Is.True);
            Assert.That(session.Completed, Is.False);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal), Is.True);
            Assert.That(session.WaitingForBusinessOnboarding, Is.False);
            Assert.That(session.Completed, Is.True);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.PostCoffeeSignal), Is.False);
            Assert.That(session.NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal), Is.False);
            session.Begin();
            Assert.That(session.WaitingForWorkstation, Is.False);
            Assert.That(session.CurrentStepId, Is.Empty);
            Assert.That(notifications, Is.EqualTo(3));
        }
    }
}
