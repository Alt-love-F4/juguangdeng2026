#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Real authored scene navigation; the presenter and value owners remain fixture-owned memory.</summary>
    public sealed class ActionNavigationSceneAcceptanceTests
    {
        private const string MapPath = "Assets/Scenes/Map.unity";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene previousScene, mapScene, debugScene;
        private GameObject runtimeObject, driverObject, hostObject;
        private NarrativeRuntime runtime, previousRuntime;
        private PlayerRuntime previousPlayer;
        private MapSceneController map;
        private MapActionDetailsView popup;
        private DayFlowController days;
        private HeldPresenter presenter;
        private CafeWorld walletWorld;
        private CafeNarrativeValueProvider wallet;
        private float previousTimeScale;
        private Func<GameFlowBootstrap, GameFlowPanel> previousOpenPanel;
        private Type previousPanelType;
        private bool previousScenePanel;
        private Dictionary<string, Func<string>> capturers;
        private Dictionary<string, Action<string>> restorers;
        private List<GameSaveParticipantData> pendingRestore;
        private Dictionary<FieldInfo, object> cafeOwnerFields;
        private Dictionary<EventSystem, bool> eventSystems;
        private Dictionary<EventSystem, GameObject> selectedObjects;
        private HashSet<ShortLoadingPanel> previousLoadingPanels;
        private HashSet<Type> previousPanelSlots;
        private bool ownsScenes;
        private int starts, settlements, completions;

        private sealed class HeldPresenter : INarrativePresenter
        {
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            private Action completed;
            public void Play(NarrativeEventRequest request, Action onCompleted)
            {
                Requests.Add(request);
                completed = onCompleted;
            }
            public void Complete()
            {
                Assert.That(completed, Is.Not.Null, "No story presentation is waiting for completion.");
                var callback = completed;
                completed = null;
                callback();
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True, "Run this fixture in Play Mode.");
            starts = settlements = completions = 0;
            ownsScenes = false;
            mapScene = debugScene = default;
            previousScene = SceneManager.GetActiveScene();
            previousTimeScale = Time.timeScale;
            previousRuntime = NarrativeRuntime.Instance;
            previousPlayer = PlayerRuntime.Current;
            previousOpenPanel = GameplayPanelHooks.OpenPanel;
            previousPanelType = GameplayPanelHooks.PanelType;
            previousScenePanel = GameplayPanelHooks.IsScenePanel;
            capturers = new Dictionary<string, Func<string>>(GameSaveParticipantRegistry.Capturers);
            restorers = new Dictionary<string, Action<string>>(GameSaveParticipantRegistry.Restorers);
            pendingRestore = GameSaveParticipantRegistry.PendingRestore;
            cafeOwnerFields = typeof(PlayerCafeOwner).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(field => !field.IsLiteral && !field.IsInitOnly).ToDictionary(field => field, field => field.GetValue(null));
            eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToDictionary(system => system, system => system.enabled);
            selectedObjects = eventSystems.Keys.ToDictionary(system => system, system => system.currentSelectedGameObject);
            previousLoadingPanels = new HashSet<ShortLoadingPanel>(Object.FindObjectsByType<ShortLoadingPanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
            previousPanelSlots = new HashSet<Type>(UIKit.GetLoadedPanelTypes());

            // A second live cafe or Bootstrap would alter an unrelated session on sceneLoaded/Update.
            Assert.That(previousLoadingPanels, Is.Empty, "Do not reuse or close another test's ShortLoadingPanel slot.");
            Assert.That(Object.FindFirstObjectByType<GameFlowBootstrap>(FindObjectsInactive.Include), Is.Null,
                "Use an isolated PlayMode test session, with no live gameplay Bootstrap.");
            Assert.That(SceneManager.GetSceneByName("Map").isLoaded, Is.False, "Do not reuse another test's Map.");
            Assert.That(SceneManager.GetSceneByName("CoffeeShop").isLoaded, Is.False, "Do not replace a live cafe.");
            Assert.That(SceneManager.GetSceneByPath(PresentationDebugSceneController.ScenePath).isLoaded, Is.False,
                "The fixture must own the shared debug scene it loads.");
            ownsScenes = true;
            Time.timeScale = 1f;
            foreach (var system in eventSystems.Keys) system.enabled = false;
            GameSaveParticipantRegistry.Capturers.Clear();
            GameSaveParticipantRegistry.Restorers.Clear();
            GameSaveParticipantRegistry.PendingRestore = null;
            PlayerCafeOwner.EndSession();
            GameplayPanelHooks.OpenPanel = null;
            GameplayPanelHooks.PanelType = null;
            GameplayPanelHooks.IsScenePanel = false;
            // Restored, transient identity only; no creation rewards or player save writes.
            PlayerRuntime.Current = new PlayerRuntime(PlayerCreationCatalog.Load(), new PlayerState { creationSubmitted = true });

            driverObject = new GameObject("Action scene acceptance coroutine owner");
            days = driverObject.AddComponent<DayFlowController>();
            days.enabled = false;
            days.RestoreJson("{\"day\":3,\"state\":5}");
            runtimeObject = new GameObject("Action scene acceptance narrative");
            runtimeObject.SetActive(false); // Skip singleton/session Awake; use the existing injection API.
            runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            presenter = new HeldPresenter();
            var configuration = LubanNarrativeLoader.Load();
            runtime.Initialize(configuration, presenter);
            SetRuntime(runtime);
            walletWorld = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            Assert.That(walletWorld.TryInitializeMoney(0, out string moneyError), Is.True, moneyError);
            wallet = new CafeNarrativeValueProvider(walletWorld);
            runtime.Controller.BeginValueRestore();
            foreach (var group in configuration.bars.Where(bar => bar.sourceMode == NarrativeSourceMode.External).GroupBy(bar => bar.providerId))
            {
                var definitions = group.ToDictionary(bar => bar.sourceKey, bar => bar.initValue);
                string providerId = group.Key;
                runtime.Controller.BindValueProvider(new DelegateNarrativeValueProvider(providerId, key =>
                {
                    if (providerId == "dayflow" && key == "day") return days.CurrentDay;
                    if (providerId == "actions" && key == "current") return runtime.Actions.ActionPoints.Current;
                    if (providerId == "actions" && key == "max") return runtime.Actions.ActionPoints.Max;
                    if (providerId == "cafe" && key == "money") return walletWorld.State.money;
                    return definitions.TryGetValue(key, out int value) ? value : (int?)null;
                }));
            }
            runtime.Controller.MoneyAccount = wallet;
            runtime.Controller.CompleteValueRestore(false);
            runtime.Controller.SetOfficialDay(3);
            runtime.Actions.RefreshForDay(3);
            runtime.Actions.ActionPoints.Set(0);
            Assert.That(runtime.Controller.ValuesReady, Is.True, "All configured external owners must be explicit.");
            runtime.Actions.ActionStarted += _ => starts++;
            runtime.Actions.ActionFinished += (_, __) => settlements++;
            runtime.Controller.EventCompleted += _ => completions++;

            // UIKit comes from the shared authored scene, never a test-created canvas/input root.
            debugScene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (var root in debugScene.GetRootGameObjects())
                foreach (var system in root.GetComponentsInChildren<EventSystem>(true)) system.enabled = false;
            mapScene = EditorSceneManager.LoadSceneInPlayMode(MapPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Assert.That(mapScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(mapScene);
            map = mapScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MapSceneController>(true))
                .Single(controller => controller.transform is RectTransform);
            Assert.That(map.enabled && map.gameObject.activeInHierarchy, Is.True, "The loaded Map must finish its authored UI binding.");
            popup = map.GetComponentInChildren<MapActionDetailsView>(true);
            Assert.That(popup, Is.Not.Null);
            // Explicit registration is needed because an additive load runs Awake before SetActiveScene.
            map.RegisterAsGameplayPanel();
            days.ResumeSavedPhase();
            yield return null;
            Assert.That(days.State, Is.EqualTo(DayFlowState.CityMapLeisure));
        }

        [UnityTest]
        public IEnumerator AuthoredMapNavigatesToRealCafeWithZeroPointsAndMoneyWithoutNcsSettlement()
        {
            OpenCafe();
            var enter = ExecuteButton("cafe.enter");
            Assert.That(enter.interactable, Is.True);
            Assert.That(runtime.Actions.ActionPoints.Current, Is.Zero);
            Assert.That(wallet.TryRead(out int balance) && balance == 0, Is.True);
            enter.onClick.Invoke();
            enter.onClick.Invoke(); // Accepted navigation must debounce the authored action button.
            yield return Until(() => SceneManager.GetActiveScene().name == "CoffeeShop", "Real CoffeeShop activation");
            yield return Until(() => !popup.IsBusy || !map.gameObject.activeInHierarchy, "Source Map dismissal");
            Assert.That(SceneManager.GetSceneByName("CoffeeShop").isLoaded, Is.True);
            Assert.That(mapScene.isLoaded, Is.True, "Map stays resident during additive navigation.");
            Assert.That(map.gameObject.activeSelf, Is.False);
            Assert.That(PlayerCafeOwner.Current, Is.Not.Null, "The actual CoffeeShop adapter must bind its world.");
            Assert.That(PlayerCafeOwner.Current.State.open, Is.False, "Navigation must not automatically open business.");
            AssertNoNavigationSettlement();
            yield return LoadingFinished();
        }

        [UnityTest]
        public IEnumerator HostedInvalidDestinationRestoresLoadedSourceAndAllowsAnotherAttempt()
        {
            // Use the existing hosted loader without Bootstrap.Awake (which starts real services/session work).
            // Its iterator is driven by our active DayFlow owner, since the source Map is hidden mid-load.
            hostObject = new GameObject("Inactive action navigation host");
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<GameFlowBootstrap>();
            typeof(GameFlowBootstrap).GetMethod("SetSessionState", Private).Invoke(host, new object[] { GameSessionState.Playing });
            GameplayPanelHooks.OpenPanel(host); // Bind the actual authored popup's controls to the host.
            const string invalidDestination = "fixture.missing.action-navigation.destination";
            bool finished = false;
            int attempts = 0;
            // Substitute only the runtime destination callback. Keep the real card validation,
            // busy/debounce logic, hosted loader, source hide and failure notification intact.
            popup.Initialize(map.GetComponentInChildren<MapActionInventoryView>(true),
                () => host.SessionState == GameSessionState.Playing, null, () =>
                {
                    attempts++;
                    var routine = (IEnumerator)typeof(GameFlowBootstrap).GetMethod("TransitionGameplaySceneRoutine", Private)
                        .Invoke(host, new object[] { invalidDestination, (Action)(() => Assert.Fail("A failed load cannot report completion.")) });
                    days.StartCoroutine(Drive(routine, () => finished = true));
                });
            for (int attempt = 0; attempt < 2; attempt++)
            {
                OpenCafe();
                var enter = ExecuteButton("cafe.enter");
                Assert.That(enter.interactable, Is.True);
                finished = false;
                LogAssert.Expect(LogType.Error, new Regex("Scene '" + Regex.Escape(invalidDestination) + "'.*couldn't be loaded.*", RegexOptions.Singleline));
                LogAssert.Expect(LogType.Error, "无法加载游戏场景：" + invalidDestination);
                enter.onClick.Invoke();
                Assert.That(popup.IsBusy, Is.True, "The actual navigation card must lock during the accepted load.");
                enter.onClick.Invoke();
                Assert.That(attempts, Is.EqualTo(attempt + 1), "Repeated clicks must not launch another loader.");
                yield return Until(() => finished, "Hosted invalid-destination recovery");
                yield return LoadingFinished();
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(mapScene));
                Assert.That(mapScene.isLoaded && map.gameObject.activeInHierarchy, Is.True);
                Assert.That(host.SessionState, Is.EqualTo(GameSessionState.Playing));
                Assert.That(popup.IsBusy, Is.False);
                OpenCafe();
                Assert.That(ExecuteButton("cafe.enter").interactable, Is.True, "Recovery must make the real navigation card retryable.");
                Assert.That(SceneManager.GetSceneByName(invalidDestination).isLoaded, Is.False);
                AssertNoNavigationSettlement();
            }
        }

        [UnityTest]
        public IEnumerator October07ActualS1StaysOnMapThenHandsPendingFocusToCafeNavigation()
        {
            days.RestoreJson("{\"day\":2,\"state\":5}");
            runtime.Controller.SetOfficialDay(2);
            runtime.Actions.RefreshForDay(2);
            days.ResumeSavedPhase();
            runtime.Controller.ProcessOfficialDay(2);
            OpenCafe();
            var story = runtime.Actions.Get("oct07.cafe.s1");
            Assert.That(story.resultEvents, Is.EqualTo(new[] { "oct07.s1" }));
            Assert.That(story.replacesLocationActions && story.focusLocationOnComplete, Is.True);
            Assert.That(story.executionKind, Is.EqualTo(NarrativeActionExecutionKind.Event));
            Assert.That(popup.transform.Find("Viewport/ActionList/Action_cafe.enter"), Is.Null,
                "The actual S1 replacement configuration must offer story only.");
            ExecuteButton(story.actionKey).onClick.Invoke();
            yield return Until(() => presenter.Requests.Count == 1, "Actual S1 story start");
            Assert.That(presenter.Requests.Single().Event.eventId, Is.EqualTo("oct07.s1"));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(mapScene));
            Assert.That(SceneManager.GetSceneByName("CoffeeShop").isLoaded, Is.False);
            Assert.That(runtime.Actions.PendingFocusPosition, Is.Null);
            Assert.That(completions, Is.Zero);
            Assert.That(settlements, Is.Zero);
            Assert.That(days.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            presenter.Complete();
            Assert.That(runtime.Actions.PendingFocusPosition, Is.EqualTo("cafe"), "Completion must create the real pending-focus handoff.");
            Assert.That(runtime.Controller.State.HasHappenedEvent("oct07.s1"), Is.True);
            Assert.That(runtime.Controller.State.TryGetCondition("story.oct07.s1.completed", out bool completed) && completed, Is.True);
            Assert.That(settlements, Is.EqualTo(1));
            yield return Until(() => runtime.Actions.PendingFocusPosition == null && popup.gameObject.activeInHierarchy
                && popup.transform.Find("Viewport/ActionList/Action_cafe.enter") != null, "Visible pending-focus consumption");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(mapScene));
            Assert.That(SceneManager.GetSceneByName("CoffeeShop").isLoaded, Is.False, "Story completion must not navigate automatically.");
            Assert.That(days.CurrentPresentation, Is.Null, "S1 completion must not start the cafe-opening presentation.");
            int settledBefore = settlements, completedBefore = completions;
            ExecuteButton("cafe.enter").onClick.Invoke();
            yield return Until(() => SceneManager.GetActiveScene().name == "CoffeeShop", "Explicit post-S1 cafe navigation");
            Assert.That(settlements, Is.EqualTo(settledBefore));
            Assert.That(completions, Is.EqualTo(completedBefore));
            Assert.That(PlayerCafeOwner.Current.State.open, Is.False);
            Assert.That(days.State, Is.EqualTo(DayFlowState.CityMapLeisure));
            Assert.That(runtime.Actions.ActionPoints.Current, Is.Zero);
            Assert.That(wallet.TryRead(out int money) && money == 0, Is.True);
            yield return LoadingFinished();
        }

        private void OpenCafe()
        {
            var button = map.transform.Find("MapViewport/MapContent/Entities/Location:cafe").GetComponent<Button>();
            Assert.That(button.IsActive() && button.interactable, Is.True);
            button.onClick.Invoke();
            Assert.That(popup.gameObject.activeInHierarchy, Is.True);
        }

        private Button ExecuteButton(string key)
        {
            // Rebuild defers Destroy until end of frame; never select a retired card with the same name.
            var list = popup.transform.Find("Viewport/ActionList");
            var card = list.Cast<Transform>().SingleOrDefault(child => child.name == "Action_" + key
                && child.gameObject.activeInHierarchy);
            Assert.That(card, Is.Not.Null, "Missing authored action card: " + key);
            var button = card.Find("Execute")?.GetComponent<Button>() ?? card.Find("ExecutePreview")?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            return button;
        }

        private void AssertNoNavigationSettlement()
        {
            Assert.That(starts, Is.Zero);
            Assert.That(settlements, Is.Zero);
            Assert.That(completions, Is.Zero);
            Assert.That(presenter.Requests, Is.Empty);
            Assert.That(runtime.Actions.Receipts, Is.Empty);
            Assert.That(runtime.Actions.ActionPoints.Current, Is.Zero);
            Assert.That(wallet.TryRead(out int balance) && balance == 0, Is.True);
        }

        private static IEnumerator Until(Func<bool> condition, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), operation + " timed out.");
                yield return null;
            }
        }

        private static IEnumerator Drive(IEnumerator routine, Action finished)
        {
            yield return routine;
            finished();
        }

        private IEnumerator LoadingFinished() => Until(() => Object.FindObjectsByType<ShortLoadingPanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Where(panel => !previousLoadingPanels.Contains(panel))
            .All(panel => panel.State == PanelState.Cached || panel.State == PanelState.Close), "Loading transition close");

        private static void SetRuntime(NarrativeRuntime value) => typeof(NarrativeRuntime).GetProperty(nameof(NarrativeRuntime.Instance))
            .GetSetMethod(true).Invoke(null, new object[] { value });

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (days != null) { days.StopAllCoroutines(); days.HideForMainMenu(); }
            if (hostObject != null) Object.Destroy(hostObject);
            if (driverObject != null) Object.Destroy(driverObject);
            // Close only transition panels created by this fixture; preserve other tests' UIKit slots.
            if (previousLoadingPanels != null)
                foreach (var panel in Object.FindObjectsByType<ShortLoadingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!previousLoadingPanels.Contains(panel)) UIKit.ClosePanel(panel);
            if (previousPanelSlots != null)
                foreach (var type in UIKit.GetLoadedPanelTypes().Where(type => !previousPanelSlots.Contains(type)).ToArray())
                {
                    foreach (var panel in UIKit.GetLoadedPanels().Where(panel => panel.GetType() == type).ToArray())
                        UIKit.ClosePanel(panel);
                    UIKit.UnloadPanel(type);
                }
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (ownsScenes)
            {
                var cafe = SceneManager.GetSceneByName("CoffeeShop");
                if (cafe.IsValid() && cafe.isLoaded) yield return SceneManager.UnloadSceneAsync(cafe);
                if (mapScene.IsValid() && mapScene.isLoaded) yield return SceneManager.UnloadSceneAsync(mapScene);
                if (debugScene.IsValid() && debugScene.isLoaded) yield return SceneManager.UnloadSceneAsync(debugScene);
            }
            runtime?.Controller?.UnbindValueProviders();
            wallet?.Dispose();
            if (runtimeObject != null) Object.Destroy(runtimeObject);
            yield return null;
            SetRuntime(previousRuntime);
            PlayerRuntime.Current = previousPlayer;
            if (cafeOwnerFields != null) foreach (var pair in cafeOwnerFields) pair.Key.SetValue(null, pair.Value);
            if (capturers != null)
            {
                GameSaveParticipantRegistry.Capturers.Clear();
                foreach (var pair in capturers) GameSaveParticipantRegistry.Capturers.Add(pair.Key, pair.Value);
                GameSaveParticipantRegistry.Restorers.Clear();
                foreach (var pair in restorers) GameSaveParticipantRegistry.Restorers.Add(pair.Key, pair.Value);
                GameSaveParticipantRegistry.PendingRestore = pendingRestore;
            }
            GameplayPanelHooks.OpenPanel = previousOpenPanel;
            GameplayPanelHooks.PanelType = previousPanelType;
            GameplayPanelHooks.IsScenePanel = previousScenePanel;
            if (eventSystems != null)
                foreach (var pair in eventSystems)
                    if (pair.Key != null)
                    {
                        pair.Key.enabled = pair.Value;
                        pair.Key.SetSelectedGameObject(selectedObjects[pair.Key]);
                    }
            Time.timeScale = previousTimeScale;
        }
    }
}
#endif
