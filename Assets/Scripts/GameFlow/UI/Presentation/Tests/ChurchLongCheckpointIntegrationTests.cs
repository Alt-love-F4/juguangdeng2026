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
    /// <summary>
    /// Generated-content integration with explicit in-memory prerequisite fixtures.
    /// Conversation completion and stage visuals are simulated; this does not prove
    /// production prerequisites or actual fade rendering (covered by the UI suites).
    /// </summary>
    public sealed class ChurchLongCheckpointIntegrationTests
    {
        private const string Chapter = "church.1104.s1";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private PlayerPresentationTestSession session;
        private NarrativeConfiguration configuration;
        private GameObject root;
        private DayFlowController flow;
        private DayFlowCheckpointEndpoint endpoint;
        private CafeNarrativeValueProvider cafeProvider;
        private Scene previousScene;
        private Type previousPanelType;
        private float previousTimeScale;
        private int completions, simulatedFades;
        private readonly List<NarrativeEventRequest> requests = new List<NarrativeEventRequest>();
        private readonly List<string> lines = new List<string>();
        private readonly List<string> choices = new List<string>();
        private readonly List<NarrativeCheckpointWait> waits = new List<NarrativeCheckpointWait>();
        private NarrativeController Controller => session.Narrative.Controller;
        private NarrativePresenter Presenter => session.Narrative.Presenter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run in Play Mode with the shared presentation scene.");
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
            requests.Clear(); lines.Clear(); choices.Clear(); waits.Clear();
            completions = simulatedFades = 0;
            session = new PlayerPresentationTestSession();
            configuration = LubanNarrativeLoader.Load();
            var chapter = configuration.events.Single(e => e.eventId == Chapter);
            Assert.That(chapter.eventProperties.Any(p => p.property == "blockedBy"), Is.True);
            Assert.That(configuration.results.Count(r => r.eventId == Chapter), Is.Zero);
            // Only these two scheduling/prerequisite changes belong to this isolated fixture.
            chapter.eventProperties.RemoveAll(p => p.property == "blockedBy");
            if (!configuration.eventDates.Any(d => d.eventId == Chapter && d.day == 30))
                configuration.eventDates.Add(new NarrativeEventDateDefinition { eventId = Chapter, day = 30,
                    timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Once });
            root = new GameObject("Generated church checkpoint integration");
            SceneManager.MoveGameObjectToScene(root, scene);
            flow = root.AddComponent<DayFlowController>();
            flow.enabled = false;
            endpoint = root.AddComponent<DayFlowCheckpointEndpoint>();
            endpoint.World = session.World;
            flow.RestoreJson("{\"day\":30,\"state\":5}");
            SetField("running", true);
            BindEndpoint();
            // ResetSession creates the real presenter while retaining the isolated runtime singleton.
            session.Narrative.ResetSession(configuration);
            BindExternalValues();
            ConfigurePresenter();
            Controller.CompleteValueRestore(false);
            Controller.SetOfficialDay(30);
            Controller.ProcessOfficialDay(30);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (flow != null) flow.HideForMainMenu();
            if (root != null) Object.Destroy(root);
            cafeProvider?.Dispose();
            cafeProvider = null;
            yield return null;
            session?.Dispose();
            session = null;
            GameplayPanelHooks.PanelType = previousPanelType;
            Time.timeScale = previousTimeScale;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        }

        [UnityTest]
        public IEnumerator GeneratedChurchRootCrossesRealBusinessAndRestoredNightCheckpointOnce()
        {
            var chapter = configuration.events.Single(e => e.eventId == Chapter);
            var pages = configuration.presentations.conversationPages.Where(p => p.presentationKey == chapter.presentationKey).ToArray();
            var pageIds = new HashSet<string>(pages.Select(p => p.pageId));
            Assert.That(pages.Length, Is.EqualTo(42));
            Assert.That(configuration.presentations.conversationLines.Count(l => pageIds.Contains(l.pageId)), Is.EqualTo(405));
            Assert.That(configuration.choices.Count(c => c.choiceId.StartsWith(Chapter + ".", StringComparison.Ordinal)), Is.EqualTo(8));

            Invoke("RunNarrativeEntry", NarrativeEntryPoint.MorningBeforeMap, (Action)(() => Invoke("EnterCityMap")));
            yield return WaitFor(() => Controller.HasPendingBusinessCheckpoint && !Presenter.IsPresenting);
            Assert.That(requests.Count, Is.EqualTo(1));
            string requestId = requests[0].RequestId;
            Assert.That(lines, Does.Contain(Chapter + ".p005"));
            Assert.That(choices, Is.EqualTo(new[] { Chapter + ".fee.one" }));
            Assert.That(waits, Is.EqualTo(new[] { NarrativeCheckpointWait.Business }));
            Assert.That(completions, Is.Zero);
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.False);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(flow.CanSave, Is.True);
            flow.EndDayFromMap();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(endpoint.Starts, Is.Zero);
            Assert.That(requests.Count, Is.EqualTo(1));

            Invoke("StartFreeCafeBusiness");
            Assert.That(endpoint.Starts, Is.EqualTo(1));
            Assert.That(endpoint.Opens, Is.EqualTo(1));
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Open));
            var issued = Controller.CaptureSnapshot().checkpoint;
            Assert.That(issued.token, Is.Not.Null.And.Not.Empty);
            Assert.That(issued.confirmed, Is.False);
            flow.FinishBusiness();
            Assert.That(endpoint.Ends, Is.EqualTo(1));
            Assert.That(endpoint.World.State.open, Is.False);
            Assert.That(endpoint.World.State.businessPhase, Is.EqualTo(BusinessPhase.Night));
            Assert.That(Controller.CaptureSnapshot().checkpoint.confirmed, Is.True);
            flow.FinishBusiness();
            Assert.That(endpoint.Ends, Is.EqualTo(1));
            yield return SubmitBusinessEnd();
            yield return WaitFor(() => Controller.HasPendingNightCheckpoint && !Presenter.IsPresenting);
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[1].RequestId, Is.EqualTo(requestId));
            Assert.That(requests[1].ResumePageId, Is.EqualTo(Chapter + ".tour.depart"));
            Assert.That(lines, Does.Contain(Chapter + ".p071"));
            Assert.That(lines, Does.Contain(Chapter + ".p255"));
            Assert.That(choices, Is.EqualTo(new[] { Chapter + ".fee.one", Chapter + ".route.near" }));
            Assert.That(waits, Is.EqualTo(new[] { NarrativeCheckpointWait.Business, NarrativeCheckpointWait.Night }));
            Assert.That(completions, Is.Zero);
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.False);
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(flow.CanSave, Is.True);

            int beforeNight = lines.Count;
            var runtimeIdentity = NarrativeRuntime.Instance;
            var oldPresenter = Presenter;
            string dayFlowJson = flow.CaptureJson();
            string narrativeJson = JsonUtility.ToJson(session.Narrative.CaptureSnapshot());
            var saved = JsonUtility.FromJson<NarrativeSnapshot>(narrativeJson);
            // JsonUtility can expand a null nested class into an empty inline object.
            Assert.That(saved.active == null || string.IsNullOrEmpty(saved.active.eventId), Is.True,
                "The waiting-night save must not contain an active event request.");
            Assert.That(saved.checkpoint.wait, Is.EqualTo(NarrativeCheckpointWait.Night));
            Assert.That(saved.checkpoint.root.requestId, Is.EqualTo(requestId));
            Assert.That(saved.checkpoint.resumePageId, Is.EqualTo(Chapter + ".night.return"));
            flow.RestoreJson(dayFlowJson);
            cafeProvider.Dispose();
            cafeProvider = null;
            session.Narrative.RestoreSnapshot(saved);
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtimeIdentity));
            Assert.That(Presenter, Is.Not.SameAs(oldPresenter));
            BindExternalValues();
            ConfigurePresenter();
            Controller.CompleteValueRestore(false);
            Controller.SetOfficialDay(30);
            BindEndpoint();
            flow.ResumeSavedPhase();
            Assert.That(flow.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(Presenter.IsPresenting, Is.False);
            Assert.That(completions, Is.Zero);
            Assert.That(endpoint.Starts, Is.EqualTo(1));
            Assert.That(endpoint.Ends, Is.EqualTo(1));

            flow.EndDayFromMap();
            yield return WaitFor(() => flow.State == DayFlowState.PreNight && !Presenter.IsPresenting);
            Assert.That(requests.Count, Is.EqualTo(3));
            Assert.That(requests[2].RequestId, Is.EqualTo(requestId));
            Assert.That(requests[2].ResumePageId, Is.EqualTo(Chapter + ".night.return"));
            Assert.That(lines[beforeNight], Is.EqualTo(Chapter + ".p413"));
            Assert.That(lines.Skip(beforeNight).All(id => int.Parse(id.Substring(id.LastIndexOf('p') + 1)) >= 413), Is.True,
                "Restored night must not replay morning or tour lines.");
            Assert.That(choices, Is.EqualTo(new[] { Chapter + ".fee.one", Chapter + ".route.near", Chapter + ".night.wait" }));
            Assert.That(lines, Does.Contain(Chapter + ".p441"));
            Assert.That(lines, Does.Not.Contain(Chapter + ".p454"));
            Assert.That(simulatedFades, Is.EqualTo(1));
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(Controller.State.HasHappenedEvent(Chapter), Is.True);
            Assert.That(Controller.State.IsFlagSet("event.once." + Chapter), Is.True);
            Assert.That(Controller.CaptureSnapshot().checkpoint, Is.Null);
            Assert.That(flow.CurrentPresentation, Is.TypeOf<PreNightPanel>());
            flow.EndDayFromMap();
            yield return null;
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(requests.Count, Is.EqualTo(3));
            Assert.That(endpoint.Starts, Is.EqualTo(1));
            Assert.That(endpoint.Opens, Is.EqualTo(1));
            Assert.That(endpoint.Ends, Is.EqualTo(1));

            // The fixture must never unblock or invent results in the generated source data.
            var fresh = LubanNarrativeLoader.Load();
            Assert.That(fresh.events.Single(e => e.eventId == Chapter).eventProperties.Any(p => p.property == "blockedBy"), Is.True);
            Assert.That(fresh.results.Count(r => r.eventId == Chapter), Is.Zero);
            Assert.That(configuration.results.Count(r => r.eventId == Chapter), Is.Zero);
        }

        private void BindExternalValues()
        {
            Controller.BeginValueRestore();
            cafeProvider = new CafeNarrativeValueProvider(endpoint.World);
            Controller.BindValueProvider(cafeProvider);
            Controller.BindValueProvider(new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? flow.CurrentDay : (int?)null));
            Controller.BindValueProvider(new DelegateNarrativeValueProvider("actions", key => key == "current"
                ? session.Narrative.Actions.ActionPoints.Current : key == "max" ? session.Narrative.Actions.ActionPoints.Max : (int?)null));
        }

        private void ConfigurePresenter()
        {
            Assert.That(Presenter, Is.Not.Null);
            session.Narrative.AbilityReader = id => id == "perception" ? (true, 3) : id == "wisdom" ? (true, 4) : (false, 0);
            // Explicit scenario input. Recursive NOT must route through this fixture before
            // the core reader, which correctly rejects unresolved production dependencies.
            bool ReadPage(string key)
            {
                if (key.StartsWith("not:", StringComparison.Ordinal)) return !ReadPage(key.Substring(4));
                if (key == "dependency:E-B03.revolution") return false;
                return Controller.ReadPresentationCondition(key);
            }
            Presenter.PageCondition = ReadPage;
            Presenter.Started += request => { if (request.Event.eventId == Chapter) requests.Add(request); };
            Presenter.CheckpointReached += (request, _, wait) => { if (request.Event.eventId == Chapter) waits.Add(wait); };
            Controller.EventCompleted += id => { if (id == Chapter) completions++; };
            Presenter.ConversationPlayback = (request, data, completed) =>
            {
                Assert.That(request.Event.eventId, Is.EqualTo(Chapter), "Unexpected production event entered the isolated fixture.");
                lines.AddRange(data.lines.Select(line => line.lineId));
                if (data.options.Count > 0)
                {
                    var chosen = data.options.Single(option => option.id == Chapter + ".fee.one"
                        || option.id == Chapter + ".route.near" || option.id == Chapter + ".night.wait");
                    Assert.That(chosen.enabled, Is.True);
                    Assert.That(data.selectChoice(chosen.id), Is.True);
                    choices.Add(chosen.id);
                }
                // Deliberately simulated stage visuals; the real presenter owns sequencing.
                data.releaseStage = () => { };
                data.fadeStage = (_, done) => { simulatedFades++; done(true); };
                completed();
                return true;
            };
        }

        private IEnumerator SubmitBusinessEnd()
        {
            Assert.That(flow.CurrentPresentation, Is.TypeOf<BusinessEndPanel>());
            var panel = (BusinessEndPanel)flow.CurrentPresentation;
            var button = panel.GetComponentInChildren<Button>(true);
            yield return WaitFor(() => button.interactable);
            button.onClick.Invoke();
            yield return WaitFor(() => flow.CurrentPresentation != panel);
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Generated chapter checkpoint timed out.");
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
        private void Invoke(string name, params object[] args)
            => typeof(DayFlowController).GetMethod(name, Private).Invoke(flow, args);
    }
}
#endif
