using System;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeOpeningEndingTests
    {
        private sealed class Presenter:INarrativePresenter,INarrativePresentationCancellation
        {
            public NarrativeEventRequest Current;public Action Complete;
            public event Action<string> Cancelled;
            public void Cancel()=>Cancelled?.Invoke("playback_cancelled");
            public void Play(NarrativeEventRequest request,Action completed){Current=request;Complete=completed;}
            public void Next(){var done=Complete;Complete=null;done();}
        }
        private static NarrativeController Create(Presenter p)
        {
            var c=LubanNarrativeLoader.Load();c.bars.Clear();c.triggers.Clear();
            var controller=new NarrativeController(c,p);controller.SetEndingChecksEnabled(true);return controller;
        }
        [Test]public void ConfirmedDepartureOnlyTriggersIf0AfterItsRealRootCompletes()
        {
            var p=new Presenter();var core=Create(p);int endings=0;core.EndingSelected+=_=>endings++;
            Assert.That(core.QueueEvent("arrival.decision"),Is.True);
            Assert.That(core.SelectChoice("MA70.10.06-S1-2.1"),Is.True);p.Next();Assert.That(p.Current.Event.eventId,Is.EqualTo("arrival.refuse"));
            Assert.That(core.SelectChoice("MA70.10.06-S1-3.2"),Is.True);
            Assert.That(core.IsEnded,Is.False);p.Next();Assert.That(p.Current.Event.eventId,Is.EqualTo("arrival.leave"));
            Assert.That(core.IsEnded,Is.False);p.Next();
            Assert.That(core.SelectedEndingId,Is.EqualTo("arrival.ending.if0"));Assert.That(endings,Is.EqualTo(1));Assert.That(core.State.HasHappenedEvent("arrival.leave"),Is.True);
            Assert.That(core.State.TryGetCondition("story.arrival.leave.completed",out bool done)&&done,Is.True);
        }
        [Test]public void DepartureListenerEnablesEndingChecksOnlyAfterItsNotificationBoundary()
        {
            var p=new Presenter();var core=Create(p);core.SetEndingChecksEnabled(false);bool deferred=false;
            core.EventCompleted+=id=>{if(id!="arrival.leave")return;Assert.That(core.IsSafeBoundary,Is.False);core.RunAtSafeBoundary(()=>{deferred=true;core.SetEndingChecksEnabled(true);});};
            Assert.That(core.QueueEvent("arrival.decision"),Is.True);core.SelectChoice("MA70.10.06-S1-2.1");p.Next();core.SelectChoice("MA70.10.06-S1-3.2");p.Next();p.Next();
            Assert.That(deferred,Is.True);Assert.That(core.SelectedEndingId,Is.EqualTo("arrival.ending.if0"));
        }
        [Test]public void CancellingTheConfirmedDepartureBeforeItsRootTailNeverGrantsTheEnding()
        {
            var p=new Presenter();var core=Create(p);
            core.QueueEvent("arrival.decision");core.SelectChoice("MA70.10.06-S1-2.1");p.Next();core.SelectChoice("MA70.10.06-S1-3.2");p.Next();
            Assert.That(p.Current.Event.eventId,Is.EqualTo("arrival.leave"));var late=p.Complete;p.Cancel();late();
            Assert.That(core.IsEnded,Is.False);Assert.That(core.State.HasHappenedEvent("arrival.leave"),Is.False);
            Assert.That(core.State.TryGetCondition("story.arrival.leave.completed",out bool completed)&&completed,Is.False);
        }
        [Test]public void RetryAndAcceptanceNeverGrantDepartureEnding()
        {
            var p=new Presenter();var core=Create(p);
            Assert.That(core.QueueEvent("arrival.decision"),Is.True);core.SelectChoice("MA70.10.06-S1-2.1");p.Next();
            core.SelectChoice("MA70.10.06-S1-3.1");p.Next();Assert.That(p.Current.Event.eventId,Is.EqualTo("arrival.decision"));
            core.SelectChoice("MA70.10.06-S1-2.2");p.Next();Assert.That(p.Current.Event.eventId,Is.EqualTo("arrival.accept"));p.Next();p.Next();
            Assert.That(core.IsEnded,Is.False);Assert.That(core.State.HasHappenedEvent("arrival.leave"),Is.False);
        }
    }
}
