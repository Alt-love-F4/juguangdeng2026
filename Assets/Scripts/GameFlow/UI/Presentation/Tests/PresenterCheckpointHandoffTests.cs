using System;
using System.Collections;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    // Fake playback keeps these shared-scene tests independent of panels and scene flow.
    public sealed class PresenterCheckpointHandoffTests
    {
        private static NarrativeConfiguration Configuration()
        {
            var config = new NarrativeConfiguration();
            config.templates.Add(new NarrativeTemplateDefinition { tplName = "Tpl_TwoPersonTalk", atomSeq = "A_DIALOG" });
            config.presentations.index.Add(new NarrativePresentationIndexDefinition
                { presentationKey = "handoff", presentationClass = "conversation" });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "wait", presentationKey = "handoff", seq = 1, stepType = NarrativeConversationStepType.WaitBusiness });
            config.presentations.conversationPages.Add(new NarrativeConversationPageDefinition
                { pageId = "resumed", presentationKey = "handoff", seq = 2 });
            config.presentations.conversationLines.Add(new NarrativeConversationLineDefinition
                { pageId = "resumed", lineId = "resumed.line", seq = 1, textKey = "resumed text" });
            return config;
        }

        private static NarrativeEventRequest Request(string resumePageId = null)
            => new NarrativeEventRequest(new NarrativeEventDefinition
                { eventId = "handoff.event", presentationKey = "handoff" }, null, 0,
                requestId: "handoff.request", resumePageId: resumePageId);

        [UnityTest]
        public IEnumerator InterruptDuringReleasedPauseHandoffCancelsExactlyOnce()
        {
            var previousInputBlocker = NarrativePresenter.ExternalInputBlocked;
            NarrativePresenter.ExternalInputBlocked = null;
            int baseline = PresentationPauseRegistry.OwnerCount;
            var presenter = new NarrativePresenter(Configuration());
            int cancelled = 0, checkpoints = 0, completed = 0, finished = 0;
            presenter.Cancelled += _ => cancelled++;
            presenter.CheckpointReached += (_, __, ___) => checkpoints++;
            presenter.Finished += _ => finished++;
            try
            {
                presenter.Play(Request(), () => completed++);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline), "Wait releases its pause before the deferred handoff.");
                Assert.That(presenter.IsPresenting, Is.True, "The pending handoff must still own the active request.");
                Assert.That(checkpoints, Is.Zero, "Checkpoint delivery must wait until StartCoroutine returns.");

                presenter.InterruptForHost();
                presenter.InterruptForHost();
                Assert.That(cancelled, Is.EqualTo(1));
                Assert.That(presenter.IsPresenting, Is.False);
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(cancelled, Is.EqualTo(1));
                Assert.That(checkpoints, Is.Zero, "An interrupted handoff must never notify the controller later.");
                Assert.That(completed, Is.Zero);
                Assert.That(finished, Is.Zero);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline));
            }
            finally
            {
                presenter.Dispose();
                NarrativePresenter.ExternalInputBlocked = previousInputBlocker;
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulHandoffIsIdleAndAllowsImmediateResumedPlay()
        {
            var previousInputBlocker = NarrativePresenter.ExternalInputBlocked;
            NarrativePresenter.ExternalInputBlocked = null;
            int baseline = PresentationPauseRegistry.OwnerCount;
            var presenter = new NarrativePresenter(Configuration());
            int cancelled = 0, checkpoints = 0, firstCompleted = 0, resumedCompleted = 0, finished = 0, pagesPlayed = 0;
            bool? presentingAtCheckpoint = null;
            int pauseOwnersAtCheckpoint = -1;
            string nextPage = null;
            NarrativeCheckpointWait? waitKind = null;
            Exception reentrantError = null;
            presenter.Cancelled += _ => cancelled++;
            presenter.Finished += _ => finished++;
            presenter.ConversationPlayback = (request, data, complete) =>
            {
                pagesPlayed++;
                complete();
                return true;
            };
            presenter.CheckpointReached += (request, pageId, wait) =>
            {
                checkpoints++;
                presentingAtCheckpoint = presenter.IsPresenting;
                pauseOwnersAtCheckpoint = PresentationPauseRegistry.OwnerCount;
                nextPage = pageId;
                waitKind = wait;
                try { presenter.Play(Request(pageId), () => resumedCompleted++); }
                catch (Exception error) { reentrantError = error; }
            };
            try
            {
                presenter.Play(Request(), () => firstCompleted++);
                Assert.That(presenter.IsPresenting, Is.True);
                Assert.That(checkpoints, Is.Zero);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline));

                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(checkpoints, Is.EqualTo(1));
                Assert.That(presentingAtCheckpoint, Is.False);
                Assert.That(pauseOwnersAtCheckpoint, Is.EqualTo(baseline));
                Assert.That(nextPage, Is.EqualTo("resumed"));
                Assert.That(waitKind, Is.EqualTo(NarrativeCheckpointWait.Business));
                Assert.That(reentrantError, Is.Null, "A checkpoint subscriber must be able to Play the resumed request immediately.");
                Assert.That(pagesPlayed, Is.EqualTo(1));
                Assert.That(firstCompleted, Is.Zero);
                Assert.That(resumedCompleted, Is.EqualTo(1));
                Assert.That(finished, Is.EqualTo(1), "Only the resumed final segment finishes the event.");
                Assert.That(cancelled, Is.Zero);
                Assert.That(presenter.IsPresenting, Is.False);
                Assert.That(PresentationPauseRegistry.OwnerCount, Is.EqualTo(baseline));
            }
            finally
            {
                presenter.Dispose();
                NarrativePresenter.ExternalInputBlocked = previousInputBlocker;
            }
        }
    }
}
