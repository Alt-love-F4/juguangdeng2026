#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    // The endpoint reports CafeWorld facts, rather than test-owned active/finished flags.
    public sealed class DayFlowCheckpointEndpoint : MonoBehaviour, IDayFlowGameplayEndpoint
    {
        public CafeWorld World;
        public bool DeferStart;
        public int Starts;
        public int Opens;
        public int Ends;
        public bool DayFlowBusinessActive => World.State.open && World.State.businessPhase == BusinessPhase.Open;
        public bool DayFlowBusinessFinished => !World.State.open && World.State.businessPhase == BusinessPhase.Night;
        public void StartDayFlowBusiness()
        {
            Starts++;
            if (!DeferStart) OpenNow();
        }
        public void OpenNow()
        {
            Assert.That(World.StartDay().success, Is.True);
            Opens++;
        }
        public void EndDayFlowBusiness()
        {
            Ends++;
            Assert.That(World.EndDay().success, Is.True);
        }
    }

    public sealed class DayFlowCheckpointTests
    {
        private const string Chapter = "fixture.day-flow.checkpoint";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private PlayerPresentationTestSession session;
        private NarrativeConfiguration configuration;
        private CheckpointPresenter presenter;
        private GameObject root;
        private DayFlowController flow;
        private DayFlowCheckpointEndpoint endpoint;
        private Scene previousScene;
        private Type previousPanelType;
        private float previousTimeScale;
        private NarrativeController Controller => session.Narrative.Controller;

        private sealed class CheckpointPresenter : INarrativePresenter, INarrativePresentationCheckpoints,
            INarrativePresentationCancellation
        {
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public event Action<string> Cancelled;
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            private Action completed;
            public NarrativeEventRequest Current => Requests[Requests.Count - 1];
            public void Play(NarrativeEventRequest request, Action onCompleted)
            {
                Requests.Add(request);
                completed = onCompleted;
            }
            public void Wait(string page, NarrativeCheckpointWait wait)
                => CheckpointReached?.Invoke(Current, page, wait);
            public void Complete() => completed();
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
        }

        [Serializable]
        private sealed class ReceiptState
        {
            public int day;
            public DayFlowState state;
            public string narrativeBusinessToken;
            public bool narrativeBusinessObservedOpen;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run these tests in Play Mode.");
            previousScene = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            previousPanelType = GameplayPanelHooks.PanelType;
            var scene = SceneManager.GetSceneByPath(PresentationDebugSceneController.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
            }
            Assert.That(scene.isLoaded, Is.True);
            SceneManager.SetActiveScene(scene);
            Time.timeScale = 1f;
            GameplayPanelHooks.PanelType = null;
            session = new PlayerPresentationTestSession();
            configuration = new NarrativeConfiguration();
            configuration.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999,
                initValue = 2, advanceMode = NarrativeAdvanceMode.Monotonic });
            configuration.bars.Add(new NarrativeBarDefinition { barKey = "fixture.reward", min = 0, max = 10 });
            configuration.events.Add(new NarrativeEventDefinition { eventId = Chapter, onceOnly = true,
                blocksSave = true, entry = NarrativeEntryPoint.MorningBeforeMap });
            configuration.eventDates.Add(new NarrativeEventDateDefinition { eventId = Chapter, day = 2,
                timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Once });
            configuration.results.Add(new NarrativeResultDefinition { eventId = Chapter,
                resultType = NarrativeResultType.BarAdd, target = "fixture.reward", value = "1" });
            presenter = new CheckpointPresenter();
            session.Narrative.Initialize(configuration, presenter);
            root = new GameObject("DayFlow checkpoint receipt test");
            SceneManager.MoveGameObjectToScene(root, scene);
            flow = root.AddComponent<DayFlowController>();
            // Drive Update explicitly so no frame can hide which endpoint state was observed.
            flow.enabled = false;
            endpoint = root.AddComponent<DayFlowCheckpointEndpoint>();
            endpoint.World = session.World;
            flow.RestoreJson("{\"day\":2,\"state\":5}");
            SetField("running", true);
            BindEndpoint();
            Controller.SetOfficialDay(2);
            Controller.ProcessOfficialDay(2);
            Assert.That(Controller.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
            presenter.Wait("afternoon", NarrativeCheckpointWait.Business);
            Assert.That(Controller.HasPendingBusinessCheckpoint, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (flow != null) flow.HideForMainMenu();
            if (root != null) Object.Destroy(root);
            yield return null;
            session?.Dispose();
            session = null;
            GameplayPanelHooks.PanelType = previousPanelType;
            Time.timeScale = previousTimeScale;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        }

        [UnityTest]
        public IEnumerator RealOpenToNightConfirmsOnceAndEndDayWaitsForBusiness()
        {
            int completions = 0;
            Controller.EventCompleted += id => { if (id == Chapter) completions++; };
            string requestId = presenter.Current.RequestId;
            flow.EndDayFromMap();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(endpoint.Starts, Is.Zero);

            Invoke("StartFreeCafeBusiness");
            Assert.That(flow.State, Is.EqualTo(DayFlowState.FreeCafeBusiness));
            Assert.That(endpoint.Starts, Is.EqualTo(1));
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Open));
            var receipt = JsonUtility.FromJson<ReceiptState>(flow.CaptureJson());
            Assert.That(receipt.narrativeBusinessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(receipt.narrativeBusinessObservedOpen, Is.True);
            Assert.That(Controller.CaptureSnapshot().checkpoint.token, Is.EqualTo(receipt.narrativeBusinessToken));
            Assert.That(Controller.CaptureSnapshot().checkpoint.confirmed, Is.False);

            flow.FinishBusiness();
            Assert.That(endpoint.Ends, Is.EqualTo(1));
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Night));
            Assert.That(endpoint.World.State.open, Is.False);
            Assert.That(Controller.CaptureSnapshot().checkpoint.confirmed, Is.True);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.PreLeisure));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<BusinessEndPanel>());
            Assert.That(JsonUtility.FromJson<ReceiptState>(flow.CaptureJson()).narrativeBusinessToken, Is.Null.Or.Empty);
            string summary = endpoint.World.State.lastSummary;
            int reputation = endpoint.World.State.reputation;
            flow.FinishBusiness();
            Assert.That(endpoint.Ends, Is.EqualTo(1), "Duplicate Finish must not settle CafeWorld twice.");
            Assert.That(endpoint.World.State.lastSummary, Is.EqualTo(summary));
            Assert.That(endpoint.World.State.reputation, Is.EqualTo(reputation));
            Assert.That(completions, Is.Zero);

            yield return SubmitBusinessEnd();
            Assert.That(presenter.Requests.Count, Is.EqualTo(2));
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("afternoon"));
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.False);
            presenter.Wait("night", NarrativeCheckpointWait.Night);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(completions, Is.Zero);
            flow.EndDayFromMap();
            Assert.That(presenter.Requests.Count, Is.EqualTo(3));
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("night"));
            presenter.Complete();
            presenter.Complete();
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.True);
            Assert.That(Controller.State.TryGetBar("fixture.reward", out int reward) && reward == 1, Is.True);
            Assert.That(Controller.CaptureSnapshot().checkpoint, Is.Null);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.PreNight));
        }

        [UnityTest]
        public IEnumerator RestoredReceiptRejectsStaleNightUntilOpenWasObserved()
        {
            // Produce a genuine old Night, then simulate a host whose next opening is deferred.
            Assert.That(endpoint.World.StartDay().success, Is.True);
            Assert.That(endpoint.World.EndDay().success, Is.True);
            endpoint.DeferStart = true;
            Invoke("StartFreeCafeBusiness");
            string token = JsonUtility.FromJson<ReceiptState>(flow.CaptureJson()).narrativeBusinessToken;
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            Assert.That(endpoint.DayFlowBusinessFinished, Is.True);
            Invoke("Update");
            flow.FinishBusiness();
            AssertUnconfirmed(token);
            Assert.That(endpoint.Ends, Is.Zero);
            Assert.That(flow.CurrentPresentation, Is.Null);

            RoundTripSession();
            Assert.That(endpoint.Starts, Is.EqualTo(2), "Restore reissues the still-deferred start request.");
            Assert.That(endpoint.Opens, Is.Zero, "Reissuing a request must not fabricate an Open observation.");
            Invoke("Update");
            flow.FinishBusiness();
            AssertUnconfirmed(token);
            Assert.That(endpoint.Ends, Is.Zero, "A restored old Night is not this token's business receipt.");
            Assert.That(JsonUtility.FromJson<ReceiptState>(flow.CaptureJson()).narrativeBusinessObservedOpen, Is.False);

            endpoint.OpenNow();
            Invoke("Update");
            Assert.That(JsonUtility.FromJson<ReceiptState>(flow.CaptureJson()).narrativeBusinessObservedOpen, Is.True);
            string requestId = Controller.CaptureSnapshot().checkpoint.root.requestId;
            RoundTripSession();
            var restored = JsonUtility.FromJson<ReceiptState>(flow.CaptureJson());
            Assert.That(restored.day, Is.EqualTo(2));
            Assert.That(restored.state, Is.EqualTo(DayFlowState.FreeCafeBusiness));
            Assert.That(restored.narrativeBusinessToken, Is.EqualTo(token));
            Assert.That(restored.narrativeBusinessObservedOpen, Is.True);
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Open));
            Assert.That(endpoint.Starts, Is.EqualTo(2), "Restoring an observed Open must not reissue another start.");
            Assert.That(endpoint.Opens, Is.EqualTo(1), "The deferred requests must lead to exactly one real business.");
            flow.FinishBusiness();
            Assert.That(endpoint.Ends, Is.EqualTo(1));
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Night));
            Assert.That(Controller.CaptureSnapshot().checkpoint.token, Is.EqualTo(token));
            Assert.That(Controller.CaptureSnapshot().checkpoint.confirmed, Is.True);
            flow.FinishBusiness();
            Assert.That(endpoint.Ends, Is.EqualTo(1));
            yield return SubmitBusinessEnd();
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("afternoon"));
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.False);
        }

        [UnityTest]
        public IEnumerator CancelledConfirmedAfternoonRestoresAndEndDayRetriesWithoutBusinessReplay()
        {
            string requestId = presenter.Current.RequestId;
            Invoke("StartFreeCafeBusiness");
            string token = JsonUtility.FromJson<ReceiptState>(flow.CaptureJson()).narrativeBusinessToken;
            flow.FinishBusiness();
            yield return SubmitBusinessEnd();
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("afternoon"));
            presenter.Cancel();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.PreBusiness));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreBusinessPanel>());
            Assert.That(Controller.BusinessCheckpointConfirmed, Is.True);
            Assert.That(JsonUtility.FromJson<ReceiptState>(flow.CaptureJson()).state, Is.EqualTo(DayFlowState.PreBusiness));
            string summary = endpoint.World.State.lastSummary;
            int reputation = endpoint.World.State.reputation;

            // A saved retry page must resume the confirmed chapter, never reopen the cafe.
            RoundTripSession();
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("afternoon"));
            Assert.That(flow.State, Is.EqualTo(DayFlowState.NarrativeEntry));
            Assert.That(Controller.CaptureSnapshot().checkpoint.token, Is.EqualTo(token));
            Assert.That(Controller.CaptureSnapshot().checkpoint.confirmed, Is.True);
            AssertBusinessNotReplayed(summary, reputation);

            // Returning to the map after another cancellation must let EndDay retry AfterBusiness.
            presenter.Cancel();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.PreBusiness));
            Invoke("EnterCityMap");
            flow.EndDayFromMap();
            Assert.That(presenter.Requests.Count, Is.EqualTo(2));
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("afternoon"));
            Assert.That(flow.State, Is.EqualTo(DayFlowState.NarrativeEntry));
            AssertBusinessNotReplayed(summary, reputation);
            presenter.Wait("night", NarrativeCheckpointWait.Night);
            flow.EndDayFromMap();
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("night"));
            presenter.Complete();
            presenter.Complete();
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.True);
            Assert.That(Controller.State.TryGetBar("fixture.reward", out int reward) && reward == 1, Is.True);
            Assert.That(endpoint.Starts, Is.EqualTo(1));
            Assert.That(endpoint.Opens, Is.EqualTo(1));
            Assert.That(endpoint.Ends, Is.EqualTo(1));
        }

        [Test]
        public void CancelledInitialMorningRestoresItsRetryInsteadOfSkippingToMap()
        {
            // Start a fresh controller because the shared fixture has already reached its
            // business wait. Enter through DayFlow so cancellation owns a real retry callback.
            presenter = new CheckpointPresenter();
            session.Narrative.Initialize(configuration, presenter);
            Controller.SetOfficialDay(2);
            Controller.ProcessOfficialDay(2);
            typeof(DayFlowController).GetMethod("RunNarrativeEntry", Private).Invoke(flow, new object[]
            {
                NarrativeEntryPoint.MorningBeforeMap,
                (Action)(() => Invoke("EnterCityMap"))
            });
            Assert.That(flow.State, Is.EqualTo(DayFlowState.NarrativeEntry));
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo(Chapter));
            Assert.That(presenter.Current.ResumePageId, Is.Null.Or.Empty);
            presenter.Cancel();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.PreBusiness));
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreBusinessPanel>());
            Assert.That(flow.CanSave, Is.True);
            Assert.That(Controller.CaptureSnapshot().checkpoint, Is.Null);
            var failedCandidate = Controller.WaitingCandidates.Single(c => c.eventId == Chapter);
            string savedFlow = flow.CaptureJson();
            StringAssert.Contains("\"hasNarrativeRetry\":true", savedFlow);

            RoundTripSession();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.NarrativeEntry));
            Assert.That(flow.IsNarrativeEntryActive, Is.True);
            Assert.That(flow.CurrentPresentation, Is.Null);
            Assert.That(presenter.Requests.Count, Is.EqualTo(1));
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo(Chapter));
            Assert.That(presenter.Current.ResumePageId, Is.Null.Or.Empty);
            Assert.That(presenter.Current.Candidate.sequence, Is.EqualTo(failedCandidate.sequence),
                "Restore must retry the retained morning candidate rather than skip it or invent another.");
            Assert.That(endpoint.Starts, Is.Zero);
            Assert.That(endpoint.Opens, Is.Zero);
            Assert.That(endpoint.Ends, Is.Zero);
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.False);
            Assert.That(Controller.State.TryGetBar("fixture.reward", out int reward) && reward == 0, Is.True);
            presenter.Wait("afternoon", NarrativeCheckpointWait.Business);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(Controller.HasPendingBusinessCheckpoint, Is.True);
            Assert.That(Controller.CaptureSnapshot().checkpoint.confirmed, Is.False);
            Assert.That(endpoint.Starts, Is.Zero);
            StringAssert.Contains("\"hasNarrativeRetry\":false", flow.CaptureJson());
        }

        private void AssertBusinessNotReplayed(string summary, int reputation)
        {
            Assert.That(endpoint.Starts, Is.EqualTo(1));
            Assert.That(endpoint.Opens, Is.EqualTo(1));
            Assert.That(endpoint.Ends, Is.EqualTo(1));
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Night));
            Assert.That(endpoint.World.State.lastSummary, Is.EqualTo(summary));
            Assert.That(endpoint.World.State.reputation, Is.EqualTo(reputation));
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.False);
            Assert.That(Controller.State.TryGetBar("fixture.reward", out int reward) && reward == 0, Is.True);
        }

        private void AssertUnconfirmed(string token)
        {
            Assert.That(flow.State, Is.EqualTo(DayFlowState.FreeCafeBusiness));
            var checkpoint = Controller.CaptureSnapshot().checkpoint;
            Assert.That(checkpoint.token, Is.EqualTo(token));
            Assert.That(checkpoint.confirmed, Is.False);
            Assert.That(Controller.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.False);
        }

        private void RoundTripSession()
        {
            string dayFlowJson = flow.CaptureJson();
            var narrative = JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(Controller.CaptureSnapshot()));
            var cafe = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(endpoint.World.State));
            flow.RestoreJson(dayFlowJson);
            presenter = new CheckpointPresenter();
            session.Narrative.Initialize(configuration, presenter);
            Controller.RestoreSnapshot(narrative);
            Controller.SetOfficialDay(2);
            Controller.CompleteValueRestore(false);
            Assert.That(endpoint.World.Restore(cafe).success, Is.True);
            BindEndpoint();
            flow.ResumeSavedPhase();
        }

        private IEnumerator SubmitBusinessEnd()
        {
            Assert.That(flow.CurrentPresentation, Is.TypeOf<BusinessEndPanel>());
            var panel = (BusinessEndPanel)flow.CurrentPresentation;
            var button = panel.GetComponentInChildren<Button>(true);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!button.interactable)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "BusinessEnd entrance timed out.");
                yield return null;
            }
            button.onClick.Invoke();
            while (ReferenceEquals(flow.CurrentPresentation, panel))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "BusinessEnd exit timed out.");
                yield return null;
            }
        }

        private void BindEndpoint()
        {
            SetField("endpointOwner", endpoint);
            SetField("gameplayEndpoint", endpoint);
        }
        private void SetField(string name, object value)
            => typeof(DayFlowController).GetField(name, Private).SetValue(flow, value);
        private void Invoke(string name)
            => typeof(DayFlowController).GetMethod(name, Private).Invoke(flow, null);
    }
}
#endif
