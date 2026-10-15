using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class FirstArrivalRecoveryTests
    {
        private PlayerPresentationTestSession session;
        private GameObject root;
        private DayFlowController flow;
        private CancellablePresenter presenter;

        private sealed class CancellablePresenter : INarrativePresenter, INarrativePresentationCancellation
        {
            public event Action<string> Cancelled;
            public readonly List<string> Played = new List<string>();
            public void Play(NarrativeEventRequest request, Action completed) => Played.Add(request.Event.eventId);
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
        }

        [SetUp]
        public void SetUp()
        {
            session = new PlayerPresentationTestSession();
            presenter = new CancellablePresenter();
            var config = new NarrativeConfiguration();
            config.events.Add(new NarrativeEventDefinition { eventId = "arrival.start" });
            config.events.Add(new NarrativeEventDefinition { eventId = "arrival.decision" });
            session.Narrative.Initialize(config, presenter);
            root = new GameObject("First arrival recovery test");
            flow = root.AddComponent<DayFlowController>();
            typeof(DayFlowController).GetField("running", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            flow.HideForMainMenu();
            DialoguePlayback.Stop();
            UnityEngine.Object.Destroy(root);
            yield return null;
            session.Dispose();
        }

        [UnityTest]
        public IEnumerator CancelledArrivalShowsRetryAndReplaysOnlyFailedEvent()
        {
            Invoke("PlayFirstArrivalEvent", "arrival.decision");
            Assert.That(presenter.Played, Is.EqualTo(new[] { "arrival.decision" }));
            presenter.Cancel();
            Assert.That(session.Narrative.Controller.IsBusy, Is.False);
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreBusinessPanel>());
            Assert.That(flow.State, Is.EqualTo(DayFlowState.FirstDayIntroduction));
            Assert.That(flow.BlocksGameplayInput, Is.True);
            var button = ((UIPanel)flow.CurrentPresentation).GetComponentInChildren<Button>(true);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!button.interactable)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            button.onClick.Invoke();
            while (presenter.Played.Count < 2)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            Assert.That(presenter.Played, Is.EqualTo(new[] { "arrival.decision", "arrival.decision" }));
            Assert.That(flow.CurrentPresentation, Is.Null);
        }

        [Test]
        public void RejectedArrivalShowsRetryInsteadOfLeavingEmptyStage()
        {
            Invoke("PlayFirstArrivalEvent", "arrival.missing");
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreBusinessPanel>());
            Assert.That(presenter.Played, Is.Empty);
        }

        [Test]
        public void LeavingSessionUnsubscribesFailureRecovery()
        {
            Invoke("BeginFirstArrival");
            flow.HideForMainMenu();
            presenter.Cancel();
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(flow.IsFirstSessionGuideActive, Is.False);
        }

        [Test]
        public void FormalGuideCannotCancelButPlaybackOutsideGuideCan()
        {
            var data = new NarrativeConversationPresentationData();
            data.lines.Add(new NarrativeConversationPresentationLine { text = "Arrival" });
            NarrativeConversationEventPlayback.Play(default, data, null);
            Assert.That(DialoguePanel.Active.AllowCancel, Is.False);
            DialoguePanel.Active.Cancel();
            Assert.That(DialoguePanel.Active.IsPlaying, Is.True);
            DialoguePlayback.Stop();
            flow.HideForMainMenu();
            NarrativeConversationEventPlayback.Play(default, data, null);
            Assert.That(DialoguePanel.Active.AllowCancel, Is.True);
        }

        private void Invoke(string method, params object[] arguments)
            => typeof(DayFlowController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, arguments);
    }
}
