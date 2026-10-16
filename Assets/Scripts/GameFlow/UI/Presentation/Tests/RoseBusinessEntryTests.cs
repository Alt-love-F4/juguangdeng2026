using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class RoseBusinessEntryTests
    {
        private const string EventId = "rose.1012.s1";
        private const string RosePrerequisite = "story.rose.1011.s1.completed";
        private const string ChurchPrerequisite = "story.church.1009.s2.completed";
        private const string Completed = "story.rose.1012.s1.completed";
        private NarrativeConfiguration configuration;
        private RoseBusinessEntry entry;
        private Presenter presenter;
        private NarrativeController controller;

        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation,
            INarrativePresentationCheckpoints
        {
            public event Action<string> Cancelled;
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            public Action Completion { get; private set; }
            public NarrativeEventRequest Current => Requests[Requests.Count - 1];
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Requests.Add(request);
                Completion = completed;
            }
            public void Cancel() => Cancelled?.Invoke("host_unavailable");
            public void Wait(NarrativeCheckpointWait wait) => CheckpointReached?.Invoke(Current, "after-wait", wait);
        }

        [SetUp]
        public void SetUp()
        {
            configuration = new NarrativeConfiguration();
            configuration.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999,
                initValue = 7, advanceMode = NarrativeAdvanceMode.Monotonic });
            foreach (string key in new[] { RosePrerequisite, ChurchPrerequisite, Completed })
                configuration.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = key, initialValue = false });
            configuration.events.Add(new NarrativeEventDefinition { eventId = EventId, onceOnly = true, blocksSave = true,
                entry = NarrativeEntryPoint.Immediate });
            configuration.results.Add(new NarrativeResultDefinition { eventId = EventId,
                resultType = NarrativeResultType.SetFlag, target = Completed, value = "true" });
            entry = new RoseBusinessEntry();
            Rebuild();
        }

        private void Rebuild()
        {
            presenter = new Presenter();
            controller = new NarrativeController(configuration, presenter);
            controller.SetOfficialDay(7);
        }

        private static void SatisfyPrerequisites(NarrativeController target)
        {
            foreach (string key in new[] { RosePrerequisite, ChurchPrerequisite })
                if (target.State.TryGetCondition(key, out _)) target.State.SetCondition(key, true);
        }

        // Compare the real controller's state, queue, active root and checkpoint, not helper internals.
        private void AssertRejectedWithoutMutation(int day = 7, bool ready = true)
        {
            string before = JsonUtility.ToJson(controller.CaptureSnapshot());
            int requests = presenter.Requests.Count;
            bool busy = controller.IsBusy;
            Assert.That(entry.TryRequest(controller, day, ready), Is.False);
            Assert.That(presenter.Requests.Count, Is.EqualTo(requests));
            Assert.That(controller.IsBusy, Is.EqualTo(busy));
            Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void NullControllerDoesNotPreventNextReadyControllerFromRequesting()
        {
            Assert.That(entry.TryRequest(null, 7, true), Is.False);
            SatisfyPrerequisites(controller);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Single().Event.eventId, Is.EqualTo(EventId));
        }

        [Test]
        public void HostNotReadyDoesNotConsumeTheAttempt()
        {
            SatisfyPrerequisites(controller);
            AssertRejectedWithoutMutation(ready: false);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(6)]
        [TestCase(8)]
        public void WrongHostDayDoesNotRequest(int day)
        {
            SatisfyPrerequisites(controller);
            AssertRejectedWithoutMutation(day);
        }

        [Test]
        public void CalendarMismatchDoesNotRequest()
        {
            SatisfyPrerequisites(controller);
            controller.SetOfficialDay(8);
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void MissingCalendarDoesNotRequest()
        {
            configuration.bars.Clear();
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.State.TryGetBar("world.calendar", out _), Is.False);
            AssertRejectedWithoutMutation();
        }

        [TestCase(RosePrerequisite)]
        [TestCase(ChurchPrerequisite)]
        [TestCase(Completed)]
        public void UnknownConditionKeyIsNotTreatedAsKnownFalse(string missing)
        {
            configuration.conditionKeys.RemoveAll(key => key.key == missing);
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.State.TryGetCondition(missing, out _), Is.False);
            AssertRejectedWithoutMutation();
        }

        [TestCase(RosePrerequisite)]
        [TestCase(ChurchPrerequisite)]
        public void KnownFalsePrerequisiteDoesNotRequestButCanBecomeReady(string prerequisite)
        {
            SatisfyPrerequisites(controller);
            controller.State.SetCondition(prerequisite, false);
            AssertRejectedWithoutMutation();
            controller.State.SetCondition(prerequisite, true);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Single().Event.eventId, Is.EqualTo(EventId));
        }

        [Test]
        public void AllConditionKeysStartFalseWithoutInventingStoryFacts()
        {
            foreach (string key in new[] { RosePrerequisite, ChurchPrerequisite, Completed })
                Assert.That(controller.State.GetCondition(key), Is.False, key);
            AssertRejectedWithoutMutation();
            Assert.That(controller.State.HappenedEvents, Is.Empty);
        }

        [Test]
        public void ConstructionBlockerRemainsAuthoritativeEvenWithAllPrerequisites()
        {
            configuration.events.Single().eventProperties.Add(new NarrativeEventProperty
                { property = "blockedBy", value = "fixture-source-blocker" });
            SatisfyPrerequisites(controller);
            Assert.That(controller.ExplainEvent(EventId).Rejection, Does.Contain("fixture-source-blocker"));
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void MissingEventDoesNotRequest()
        {
            configuration.events.Clear();
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.False);
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void CompletedFactSuppressesRequestEvenWithoutHappenedMarker()
        {
            SatisfyPrerequisites(controller);
            controller.State.SetCondition(Completed, true);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void HappenedEventSuppressesRequestEvenWithCompletedFalseAndRepeatableDefinition()
        {
            configuration.events.Single().onceOnly = false;
            SatisfyPrerequisites(controller);
            controller.State.MarkEventHappened(EventId);
            Assert.That(controller.State.GetCondition(Completed), Is.False);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True,
                "The entry must honor the happened fact independently of once-only admission.");
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void ReadyRequestPlaysOnceAndOnlyCompletionCommitsStoryFacts()
        {
            SatisfyPrerequisites(controller);
            int completions = 0;
            controller.EventCompleted += id => { if (id == EventId) completions++; };
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Single().Event.eventId, Is.EqualTo(EventId));
            Assert.That(controller.CurrentEventId, Is.EqualTo(EventId));
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(controller.State.GetCondition(Completed), Is.False);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
            for (int frame = 0; frame < 5; frame++) AssertRejectedWithoutMutation();
            presenter.Completion();
            presenter.Completion();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(controller.State.GetCondition(Completed), Is.True);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.True);
            Assert.That(controller.IsBusy, Is.False);
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void CancellationDoesNotAutomaticallyReplayAndResetAllowsExplicitRetry()
        {
            SatisfyPrerequisites(controller);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            var staleCompletion = presenter.Completion;
            presenter.Cancel();
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.State.GetCondition(Completed), Is.False);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True);
            for (int frame = 0; frame < 5; frame++)
            {
                controller.CheckEndingsAtSafeBoundary();
                controller.ResumePendingEvents();
                AssertRejectedWithoutMutation();
            }
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
            entry.Reset();
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Count, Is.EqualTo(2));
            staleCompletion();
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
            presenter.Completion();
            Assert.That(controller.State.GetCondition(Completed), Is.True);
        }

        [Test]
        public void RetryRejectedByBusinessCheckpointPreservesCancelledAttemptAfterCheckpointCompletes()
        {
            configuration.events.Add(new NarrativeEventDefinition { eventId = "fixture.retry-checkpoint", onceOnly = true });
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            presenter.Cancel();
            Assert.That(controller.QueueEvent("fixture.retry-checkpoint"), Is.True);
            presenter.Wait(NarrativeCheckpointWait.Business);
            Assert.That(controller.HasPendingBusinessCheckpoint, Is.True);
            Assert.That(controller.IsSafeBoundary, Is.True);
            Assert.That(controller.IsBusy, Is.False);
            string before = JsonUtility.ToJson(controller.CaptureSnapshot());
            Assert.That(entry.TryRetry(controller, 7, true), Is.False);
            Assert.That(presenter.Requests.Count, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(before));

            Assert.That(controller.TryBeginCheckpointBusiness(7, out string token), Is.True);
            Assert.That(controller.ConfirmCheckpointBusiness(7, token), Is.True);
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.True);
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo("fixture.retry-checkpoint"));
            presenter.Completion();
            Assert.That(controller.HasPendingBusinessCheckpoint, Is.False);
            Assert.That(controller.State.HasHappenedEvent("fixture.retry-checkpoint"), Is.True);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True);
            for (int frame = 0; frame < 5; frame++) AssertRejectedWithoutMutation();
            Assert.That(presenter.Requests.Count, Is.EqualTo(3));
            Assert.That(controller.State.GetCondition(Completed), Is.False);
            Assert.That(entry.TryRetry(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Count, Is.EqualTo(4));
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo(EventId));
        }

        [Test]
        public void RetryRejectedBySourceGatePreservesCancelledAttemptWhenSourceBecomesAvailable()
        {
            SatisfyPrerequisites(controller);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            presenter.Cancel();
            var definition = configuration.events.Single();
            var blocker = new NarrativeEventProperty { property = "blockedBy", value = "fixture-retry-source-blocker" };
            definition.eventProperties.Add(blocker);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.False);
            string before = JsonUtility.ToJson(controller.CaptureSnapshot());
            Assert.That(entry.TryRetry(controller, 7, true), Is.False);
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(before));

            // This isolated fixture changes availability; generated source gates remain untouched.
            definition.eventProperties.Remove(blocker);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True);
            for (int frame = 0; frame < 5; frame++) AssertRejectedWithoutMutation();
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
            Assert.That(controller.State.GetCondition(Completed), Is.False);
            Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
            Assert.That(entry.TryRetry(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Count, Is.EqualTo(2));
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo(EventId));
        }

        [Test]
        public void NewControllerOnSameDayHasIndependentAttemptAndStoryState()
        {
            SatisfyPrerequisites(controller);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            var previousController = controller;
            var previousPresenter = presenter;
            previousPresenter.Cancel();
            Rebuild();
            Assert.That(controller.State.GetCondition(RosePrerequisite), Is.False);
            SatisfyPrerequisites(controller);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Single().Event.eventId, Is.EqualTo(EventId));
            Assert.That(previousPresenter.Requests.Count, Is.EqualTo(1));
            Assert.That(previousController.State.HasHappenedEvent(EventId), Is.False);
        }

        [Test]
        public void UnrelatedActiveRootDoesNotQueueRoseBehindIt()
        {
            configuration.events.Add(new NarrativeEventDefinition { eventId = "fixture.other" });
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.QueueEvent("fixture.other"), Is.True);
            AssertRejectedWithoutMutation();
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo("fixture.other"));
            presenter.Completion();
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
            Assert.That(presenter.Requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void FormalEventConditionRejectionIsNotBypassedByEntryPrerequisites()
        {
            configuration.conditions.Add(new NarrativeConditionDefinition { ownerType = "event", ownerId = EventId,
                condType = "BOOL_EQ", target = RosePrerequisite, value = "false" });
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.False);
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void EndedControllerDoesNotRequest()
        {
            configuration.endings.Add(new NarrativeEndingDefinition { endingId = "fixture.ending", enabled = true });
            configuration.conditions.Add(new NarrativeConditionDefinition { ownerType = "ending", ownerId = "fixture.ending",
                condType = "BOOL_EQ", target = RosePrerequisite, value = "true" });
            Rebuild();
            SatisfyPrerequisites(controller);
            controller.SetEndingChecksEnabled(true);
            Assert.That(controller.IsEnded, Is.True);
            AssertRejectedWithoutMutation();
        }

        [Test]
        public void ValueRestoreInProgressDoesNotRequest()
        {
            SatisfyPrerequisites(controller);
            controller.BeginValueRestore();
            Assert.That(controller.CanCaptureSnapshot, Is.False);
            string before = JsonUtility.ToJson(controller.State.Capture());
            Assert.That(entry.TryRequest(controller, 7, true), Is.False);
            Assert.That(presenter.Requests, Is.Empty);
            Assert.That(JsonUtility.ToJson(controller.State.Capture()), Is.EqualTo(before));
            controller.CompleteValueRestore(false);
            Assert.That(entry.TryRequest(controller, 7, true), Is.True);
        }

        [TestCase(NarrativeCheckpointWait.Business)]
        [TestCase(NarrativeCheckpointWait.Night)]
        public void PendingCheckpointDoesNotStartAnotherRootDespiteIdleController(NarrativeCheckpointWait wait)
        {
            configuration.events.Add(new NarrativeEventDefinition { eventId = "fixture.retained", onceOnly = true });
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.QueueEvent("fixture.retained"), Is.True);
            presenter.Wait(wait);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.CanCaptureSnapshot, Is.True);
            Assert.That(controller.CaptureSnapshot().checkpoint.wait, Is.EqualTo(wait));
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True);
            AssertRejectedWithoutMutation();
            Assert.That(controller.State.HasHappenedEvent("fixture.retained"), Is.False);
        }

        [Test]
        public void PendingServiceCheckpointDoesNotStartRoseWhileRealOrderIsUnserved()
        {
            configuration.objects.Add(new NarrativeObjectDefinition { objId = "fixture.guest", objType = NarrativeObjectType.Npc });
            configuration.cafeVisits.Add(new NarrativeCafeVisitDefinition { visitKey = "fixture.visit", characterId = "fixture.guest",
                orderProductId = "espresso", requireRealProduction = true, leaveAfterServed = false });
            configuration.events.Add(new NarrativeEventDefinition { eventId = "fixture.spawn" });
            configuration.results.Add(new NarrativeResultDefinition { eventId = "fixture.spawn", resultType = NarrativeResultType.SpawnNpc,
                target = "obj:fixture.guest", value = "scene:cafe" });
            configuration.events.Add(new NarrativeEventDefinition { eventId = "fixture.service", presentationKey = "fixture.service.pages" });
            configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "wait",
                presentationKey = "fixture.service.pages", seq = 1, stepType = NarrativeConversationStepType.WaitService });
            configuration.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId = "after-wait",
                presentationKey = "fixture.service.pages", seq = 2 });
            configuration.cafeVisitHooks.Add(new NarrativeCafeVisitHookDefinition { visitKey = "fixture.visit", stage = "arrived",
                trigger = "auto", eventId = "fixture.service" });
            Rebuild();
            SatisfyPrerequisites(controller);
            Assert.That(controller.QueueEvent("fixture.spawn"), Is.True);
            presenter.Completion();
            string visitId = controller.GetCommittedCafeVisits().Visits.Single().VisitInstanceId;
            Assert.That(controller.SubmitCafeVisitFeedback(new NarrativeCafeVisitFeedback { visitInstanceId = visitId,
                characterId = "fixture.guest", orderId = "fixture.order", productId = "espresso", stage = "arrived", sequence = 1 }), Is.True);
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo("fixture.service"));
            presenter.Wait(NarrativeCheckpointWait.Service);
            Assert.That(controller.HasPendingServiceCheckpoint, Is.True);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.ExplainEvent(EventId).CanRequest, Is.True);
            AssertRejectedWithoutMutation();
            Assert.That(controller.State.HasHappenedEvent("fixture.service"), Is.False);
        }

        [Test]
        public void GeneratedB1012RemainsBlockedWithNoInventedCompletionDefinition()
        {
            var generated = LubanNarrativeLoader.Load();
            var definition = generated.events.Single(e => e.eventId == EventId);
            Assert.That(definition.eventProperties.Any(p => p.property == "blockedBy" && !string.IsNullOrWhiteSpace(p.value)), Is.True);
            Assert.That(generated.conditionKeys.Any(k => k.key == Completed), Is.False);
            var generatedPresenter = new Presenter();
            var generatedController = new NarrativeController(generated, generatedPresenter);
            // Bind external owners without changing any generated event, condition or result definition.
            generatedController.BindValueProvider(new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? 7 : (int?)null));
            generatedController.BindValueProvider(new DelegateNarrativeValueProvider("cafe", key => key == "money" ? 0 : (int?)null));
            generatedController.BindValueProvider(new DelegateNarrativeValueProvider("actions", key => key == "current" || key == "max" ? 4 : (int?)null));
            generatedController.CompleteValueRestore(false);
            Assert.That(generatedController.CanCaptureSnapshot, Is.True);
            Assert.That(generatedController.State.TryGetBar("world.calendar", out int day) && day == 7, Is.True);
            Assert.That(generatedController.State.TryGetCondition(Completed, out _), Is.False);
            Assert.That(generatedController.ExplainEvent(EventId).CanRequest, Is.False);
            string before = JsonUtility.ToJson(generatedController.CaptureSnapshot());
            Assert.That(entry.TryRequest(generatedController, 7, true), Is.False);
            Assert.That(generatedPresenter.Requests, Is.Empty);
            Assert.That(JsonUtility.ToJson(generatedController.CaptureSnapshot()), Is.EqualTo(before));
            Assert.That(definition.eventProperties.Any(p => p.property == "blockedBy"), Is.True);
        }

        [Test]
        public void DayFlowUpdateRequiresBusinessPhaseRealOpenAndReadyEndpoint()
        {
            if (!Application.isPlaying) Assert.Ignore("The synchronous DayFlow host fixture requires Play Mode.");
            var previousPanelType = GameplayPanelHooks.PanelType;
            float previousTimeScale = Time.timeScale;
            PlayerPresentationTestSession session = null;
            GameObject root = null;
            try
            {
                Time.timeScale = 1f;
                GameplayPanelHooks.PanelType = null;
                session = new PlayerPresentationTestSession();
                presenter = new Presenter();
                session.Narrative.Initialize(configuration, presenter);
                controller = session.Narrative.Controller;
                controller.SetOfficialDay(7);
                SatisfyPrerequisites(controller);
                root = new GameObject("Rose business entry host test");
                var flow = root.AddComponent<DayFlowController>();
                flow.enabled = false;
                var endpoint = root.AddComponent<RoseBusinessEntryTestEndpoint>();
                endpoint.World = session.World;
                endpoint.Ready = true;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Phase(DayFlowState state)
                {
                    flow.RestoreJson("{\"day\":7,\"state\":" + (int)state + "}");
                    typeof(DayFlowController).GetField("running", flags).SetValue(flow, true);
                    typeof(DayFlowController).GetField("endpointOwner", flags).SetValue(flow, endpoint);
                    typeof(DayFlowController).GetField("gameplayEndpoint", flags).SetValue(flow, endpoint);
                }
                void Update() => typeof(DayFlowController).GetMethod("Update", flags).Invoke(flow, null);
                void NoRequest()
                {
                    string before = JsonUtility.ToJson(controller.CaptureSnapshot());
                    Update();
                    Assert.That(presenter.Requests, Is.Empty);
                    Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(before));
                }
                Assert.That(PresentationPauseRegistry.IsPaused, Is.False);
                Assert.That(DialoguePanel.IsAnyPlaying, Is.False);
                Phase(DayFlowState.FreeCafeBusiness);
                endpoint.DeferStart = true;
                endpoint.StartDayFlowBusiness();
                Assert.That(endpoint.DayFlowBusinessActive, Is.False);
                NoRequest();
                Assert.That(endpoint.World.StartDay().success, Is.True);
                // Keep the world genuinely Open to isolate the host's phase gate.
                Phase(DayFlowState.CityMapLeisure);
                NoRequest();
                Phase(DayFlowState.BusinessTransition);
                NoRequest();
                Phase(DayFlowState.FreeCafeBusiness);
                endpoint.Ready = false;
                NoRequest();
                endpoint.Ready = true;
                endpoint.enabled = false;
                NoRequest();
                endpoint.enabled = true;
                Update();
                Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Open));
                Assert.That(presenter.Requests.Single().Event.eventId, Is.EqualTo(EventId));
                Assert.That(controller.CurrentEventId, Is.EqualTo(EventId));
                for (int frame = 0; frame < 5; frame++) Update();
                Assert.That(presenter.Requests.Count, Is.EqualTo(1));
                string activeBeforeRetry = JsonUtility.ToJson(controller.CaptureSnapshot());
                Assert.That(flow.TryRetryRoseBusinessEntry(), Is.False, "Busy retry must preserve the accepted-attempt latch.");
                Assert.That(presenter.Requests.Count, Is.EqualTo(1));
                Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(activeBeforeRetry));
                var cancelledCompletion = presenter.Completion;
                presenter.Cancel();
                for (int frame = 0; frame < 5; frame++) Update();
                Assert.That(presenter.Requests.Count, Is.EqualTo(1), "Rejected busy retry must not arm automatic replay after cancellation.");
                Assert.That(controller.State.GetCondition(Completed), Is.False);
                Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);

                endpoint.Ready = false;
                string cancelledBeforeRetry = JsonUtility.ToJson(controller.CaptureSnapshot());
                Assert.That(flow.TryRetryRoseBusinessEntry(), Is.False, "Nonready retry must preserve the accepted-attempt latch.");
                Assert.That(presenter.Requests.Count, Is.EqualTo(1));
                Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(cancelledBeforeRetry));
                endpoint.Ready = true;
                for (int frame = 0; frame < 5; frame++) Update();
                Assert.That(presenter.Requests.Count, Is.EqualTo(1), "Becoming ready after a rejected retry must not automatically replay.");
                Assert.That(JsonUtility.ToJson(controller.CaptureSnapshot()), Is.EqualTo(cancelledBeforeRetry));

                Assert.That(flow.TryRetryRoseBusinessEntry(), Is.True);
                Assert.That(presenter.Requests.Count, Is.EqualTo(2));
                Assert.That(presenter.Current.Event.eventId, Is.EqualTo(EventId));
                Assert.That(controller.CurrentEventId, Is.EqualTo(EventId));
                Assert.That(flow.TryRetryRoseBusinessEntry(), Is.False);
                for (int frame = 0; frame < 5; frame++) Update();
                Assert.That(presenter.Requests.Count, Is.EqualTo(2));
                cancelledCompletion();
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(controller.State.HasHappenedEvent(EventId), Is.False);
                presenter.Completion();
                Assert.That(controller.State.GetCondition(Completed), Is.True);
                Assert.That(controller.State.HasHappenedEvent(EventId), Is.True);
                Assert.That(controller.IsBusy, Is.False);
                for (int frame = 0; frame < 5; frame++) Update();
                Assert.That(presenter.Requests.Count, Is.EqualTo(2));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                session?.Dispose();
                GameplayPanelHooks.PanelType = previousPanelType;
                Time.timeScale = previousTimeScale;
            }
        }
    }

    public sealed class RoseBusinessEntryTestEndpoint : MonoBehaviour, IDayFlowGameplayEndpoint, IDayFlowBusinessEntryEndpoint
    {
        public CafeWorld World;
        public bool Ready;
        public bool DeferStart;
        public bool DayFlowBusinessActive => World != null && World.State.open && World.State.businessPhase == BusinessPhase.Open;
        public bool DayFlowBusinessFinished => World != null && !World.State.open && World.State.businessPhase == BusinessPhase.Night;
        public bool NarrativeBusinessEntryReady => Ready;
        public void StartDayFlowBusiness()
        {
            if (!DeferStart) Assert.That(World.StartDay().success, Is.True);
        }
        public void EndDayFlowBusiness() => Assert.That(World.EndDay().success, Is.True);
    }
}
