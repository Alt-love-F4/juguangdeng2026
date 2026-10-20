using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class FirstArrivalTests
    {
        private sealed class Presenter : INarrativePresenter
        {
            public NarrativeEventRequest request;
            public Action done;
            public void Play(NarrativeEventRequest value, Action completed) { request = value; done = completed; }
        }
        [TestCase(2, false)]
        [TestCase(3, true)]
        public void ArrivalSupportsRefusalRetryAndAcceptance(int perception, bool insight)
        {
            var config = LubanNarrativeLoader.Load();
            // Isolate narrative transactions from unrelated world triggers and external providers.
            config.bars.Clear(); config.triggers.Clear(); config.endings.Clear();
            var presenter = new Presenter();
            var controller = new NarrativeController(config, presenter) { AbilityReader = _ => (true, perception), AbilityNameResolver = _ => "知觉" };
            Assert.That(controller.QueueEvent("arrival.start"), Is.True);
            Assert.That(controller.SelectChoice("MA70.10.06-S1-1.1"), Is.True);
            presenter.done(); Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.reply.1"));
            presenter.done(); Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.agent"));
            presenter.done(); Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.check"));
            var snapshot = controller.EvaluateAbilityCheckForPresentation("arrival.perception");
            Assert.That(snapshot.passed, Is.EqualTo(insight));
            presenter.done();
            if (insight) { Assert.That(presenter.request.PassiveHint, Does.StartWith("需要 知觉 到达 3 · ")); presenter.done(); }
            Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.request"));
            presenter.done();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.decision"));
                Assert.That(controller.SelectChoice("MA70.10.06-S1-2.1"), Is.True);
                presenter.done(); Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.refuse"));
                Assert.That(controller.SelectChoice("MA70.10.06-S1-3.1"), Is.True);
                presenter.done();
            }
            Assert.That(controller.SelectChoice("MA70.10.06-S1-2.2"), Is.True);
            presenter.done(); Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.accept"));
            presenter.done(); Assert.That(presenter.request.Event.eventId, Is.EqualTo("arrival.complete"));
        }
    }
}
