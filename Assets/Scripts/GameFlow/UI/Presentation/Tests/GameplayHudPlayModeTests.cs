#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YokiFrame;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class GameplayHudPlayModeTests
    {
        private Keyboard keyboard;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return ClearOwners();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            yield return ClearOwners();
        }

        private IEnumerator ClearOwners()
        {
            UIKit.ClosePanel<GameplayHudPanel>();
            UIKit.UnloadPanel<GameplayHudPanel>();
            UIKit.ClosePanel<DetailsPanel>();
            UIKit.UnloadPanel<DetailsPanel>();
            foreach (var flow in Object.FindObjectsByType<GameFlowBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(flow.gameObject);
            foreach (var integration in Object.FindObjectsByType<NarrativeGameFlowIntegration>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(integration.gameObject);
            yield return null;
            foreach (var runtime in Object.FindObjectsByType<NarrativeRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(runtime.gameObject);
            PlayerRuntime.Current = null;
            PlayerCafeOwner.EndSession();
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectCafeEntryHasLiveHudButtonsAndKeyboardWithoutMainFlow()
        {
            yield return SceneManager.LoadSceneAsync("CoffeeShop", LoadSceneMode.Single);
            yield return WaitFor(() => GameplayHudPanel.Instance != null && GameplayHudPanel.Instance.IsVisible
                && PlayerRuntime.Current?.CreationSubmitted == true, "Standalone cafe HUD initialization");
            Assert.That(Object.FindFirstObjectByType<GameFlowBootstrap>(), Is.Null);
            Assert.That(SceneManager.GetSceneByName("Map").isLoaded, Is.False,
                "The shared HUD must never load a hidden map to get its UI.");
            var runtime = NarrativeRuntime.Instance;
            var player = PlayerRuntime.Current;
            var hud = GameplayHudPanel.Instance;
            Assert.That(hud.Artwork.localScale.x, Is.EqualTo(.75f).Within(.01f));
            int day = runtime.Actions.CurrentDay;
            runtime.Actions.ActionPoints.Set(2);
            yield return new WaitForSecondsRealtime(.15f);
            var segments = hud.Inventory.PointsSegments;
            Assert.That(segments, Is.Not.Empty);
            Assert.That(segments.Sum(segment => segment.fillAmount),
                Is.EqualTo(2f / runtime.Actions.ActionPoints.Max * segments.Length).Within(.001f));
            Assert.That(hud.Artwork.Find("Date/Month").GetComponent<TMP_Text>().text, Is.EqualTo("10"));
            Assert.That(hud.Artwork.Find("Date/Day").GetComponent<TMP_Text>().text, Is.EqualTo("06"));
            yield return ExerciseWindows(hud);
            Assert.That(PlayerRuntime.Current, Is.SameAs(player));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
            Assert.That(runtime.Actions.CurrentDay, Is.EqualTo(day), "Opening UI must not advance time.");
            Assert.That(runtime.Actions.ActionPoints.Current, Is.EqualTo(2), "Opening UI must not spend points.");
            int activeInputs = 0;
            foreach (var input in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (input.isActiveAndEnabled) activeInputs++;
            Assert.That(activeInputs, Is.EqualTo(1));
            yield return Capture("cafe-shared-hud");
        }

        [UnityTest]
        public IEnumerator MapToCafeKeepsOneHudAndSamePlayerActionAndTaskOwners()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return WaitFor(() => GameplayHudPanel.Instance != null && PlayerRuntime.Current?.CreationSubmitted == true,
                "Standalone map session");
            var hud = GameplayHudPanel.Instance;
            var player = PlayerRuntime.Current;
            var runtime = NarrativeRuntime.Instance;
            runtime.Actions.ActionPoints.Set(2);
            var previousWorld = PlayerCafeOwner.Current;
            previousWorld.TryAddMoney(73, out _);
            int money = previousWorld.State.money;
            Click(hud.Artwork.Find("TasksToggle").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(hud.Tasks.IsVisible, Is.True);
            // Real authored map card and standalone transition.
            MapSceneController map = null;
            foreach (var candidate in Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled && candidate.transform is RectTransform) map = candidate;
            Assert.That(map, Is.Not.Null);
            map.transform.Find("MapViewport/MapContent/Entities/Location:cafe").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var entry = map.GetComponentInChildren<MapActionDetailsView>(true).transform.Find("Viewport/ActionList/EnterCafe");
            var execute = entry.Find("Execute");
            if (execute == null) execute = entry.Find("ExecutePreview");
            execute.GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "CoffeeShop" && !LoadingVisible(), "Map to cafe transition");
            Assert.That(GameplayHudPanel.Instance, Is.SameAs(hud));
            Assert.That(PlayerRuntime.Current, Is.SameAs(player));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
            Assert.That(Object.FindObjectsByType<GameplayHudPanel>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(hud.Tasks.IsVisible, Is.False, "Scene switches close drawers without discarding their owner.");
            Assert.That(runtime.Actions.ActionPoints.Current, Is.EqualTo(2));
            Assert.That(PlayerCafeOwner.Current, Is.Not.SameAs(previousWorld));
            Assert.That(PlayerCafeOwner.Current.State.money, Is.EqualTo(money));
            Assert.That(player.TryReadMoney(out int observed), Is.EqualTo(PlayerReadStatus.Success));
            Assert.That(observed, Is.EqualTo(money));
            yield return ExerciseWindows(hud);
        }

        [UnityTest]
        public IEnumerator MissingGameplaySceneRestoresMapAndReportsFailure()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return WaitFor(() => GameplayHudPanel.Instance != null && PlayerRuntime.Current?.CreationSubmitted == true,
                "Standalone map session for transition failure");
            var flow = Object.FindFirstObjectByType<GameFlowBootstrap>();
            Assert.That(flow, Is.Not.Null);

            bool completed = false;
            bool failed = false;
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                flow.TransitionToGameplayScene("NCS_Missing_Test_Scene", () => completed = true, () => failed = true);
                yield return WaitFor(() => failed && !LoadingVisible(), "Failed scene transition cleanup");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
            }

            Assert.That(completed, Is.False, "A failed transition must not invoke its success callback.");
            Assert.That(flow.SessionState, Is.EqualTo(GameSessionState.Playing));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Map"));
            Assert.That(Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None)
                .Any(panel => panel.isActiveAndEnabled && panel.transform is RectTransform), Is.True,
                "The previous map host must be restored after a load failure.");
            Assert.That(NarrativeRuntime.Instance?.IsPresenting ?? false, Is.False,
                "A failed scene handoff must release the formal presentation owner.");
        }

        [UnityTest]
        public IEnumerator MainFlowCafeAndMapOverlayUseSharedHudWithoutReplacingFormalSession()
        {
            yield return SceneManager.LoadSceneAsync("启动场景", LoadSceneMode.Single);
            var flow = Object.FindFirstObjectByType<GameFlowBootstrap>();
            Assert.That(flow, Is.Not.Null);
            yield return null;
            flow.StartGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Map"
                && UIKit.GetPanel<OpeningProloguePanel>() != null, "Formal new-game map loading");
            yield return WaitFor(() => !LoadingVisible() && flow.SessionState != GameSessionState.Menu, "Formal loading completion");
            bool skipped = false;
            float deadline = Time.realtimeSinceStartup + 15;
            while (!skipped && Time.realtimeSinceStartup < deadline)
            {
                skipped = flow.DebugSkipTutorialToMap(out _);
                yield return null;
            }
            Assert.That(skipped, Is.True);
            var runtime = NarrativeRuntime.Instance;
            var player = PlayerRuntime.Current;
            var hud = GameplayHudPanel.Instance;
            PlayerCafeOwner.Current.TryAddMoney(47, out _);
            int money = PlayerCafeOwner.Current.State.money;
            bool arrived = false;
            flow.TransitionToGameplayScene("CoffeeShop", () => arrived = true);
            yield return WaitFor(() => arrived && !LoadingVisible(), "Formal cafe transition");
            Assert.That(PlayerRuntime.Current, Is.SameAs(player));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
            Assert.That(GameplayHudPanel.Instance, Is.SameAs(hud));
            Assert.That(PlayerCafeOwner.Current.State.money, Is.EqualTo(money));
            PlayerCafeOwner.Current.TryAddMoney(11, out _);
            Assert.That(runtime.Controller.State.TryGetBar("cafe.money", out int observedMoney), Is.True);
            Assert.That(observedMoney, Is.EqualTo(money + 11), "Formal value providers must follow the scene world.");
            yield return ExerciseWindows(hud);
            Assert.That(flow.DebugForceOpenMap(out string error), Is.True, error);
            yield return WaitFor(() => flow.IsMapOpen && !LoadingVisible(), "Resident map overlay");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(hud.Artwork.localScale.x, Is.EqualTo(1f).Within(.01f), "Opening the resident map restores its original header size.");
            Assert.That(hud.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(GameplayHudPanel.SortingOrder - 10));
            Click(hud.Artwork.Find("详情面板").GetComponent<Button>());
            yield return null;
            Assert.That(flow.IsDetailsOpen, Is.True);
            Assert.That(flow.IsMapOpen, Is.True, "Details must not discard the resident map underneath it.");
            UIKit.GetPanel<DetailsPanel>().RequestClose();
            yield return WaitFor(() => !flow.IsDetailsOpen, "Close map details");
            flow.CloseMap();
            yield return null;
            Assert.That(hud.IsVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator ClosingKeepsDateAndResidentCafeUntilExplicitRestThenNextDayCanOpen()
        {
            yield return SceneManager.LoadSceneAsync("启动场景", LoadSceneMode.Single);
            var flow = Object.FindFirstObjectByType<GameFlowBootstrap>();
            yield return null;
            flow.StartGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Map"
                && UIKit.GetPanel<OpeningProloguePanel>() != null && !LoadingVisible(), "New game entry");
            bool skipped = false;
            yield return WaitFor(() => skipped = flow.DebugSkipTutorialToMap(out _), "Skip arrival to map");
            bool arrived = false;
            flow.TransitionToGameplayScene("CoffeeShop", () => arrived = true);
            yield return WaitFor(() => arrived && !LoadingVisible(), "First cafe entry");
            var days = flow.GetComponent<DayFlowController>();
            Assert.That(days.DebugSkipTutorialToCafe(out string error), Is.True, error);
            var world = PlayerCafeOwner.Current;
            var endpoint = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(item => item is IDayFlowGameplayEndpoint);
            var requestStart = endpoint.GetType().GetMethod("RequestBusinessStart");
            requestStart.Invoke(endpoint, null);
            yield return WaitFor(() => world.State.open, "Formal business start through narrative bridge");
            yield return null;
            days.FinishBusiness();
            Assert.That(days.CurrentDay, Is.EqualTo(1));
            Assert.That(world.State.day, Is.EqualTo(1));
            Assert.That(world.StartDay(advanceFromNight: false).success, Is.False);
            var settlement = (DayFlowPresentationPanel)days.CurrentPresentation;
            var submit = settlement.GetComponentInChildren<Button>(true);
            yield return WaitFor(() => submit.interactable, "Settlement entrance");
            submit.onClick.Invoke();
            yield return WaitFor(() => days.State == DayFlowState.CityMapLeisure
                && SceneManager.GetActiveScene().name == "Map" && !LoadingVisible(), "After-business map");
            Assert.That(days.CurrentDay, Is.EqualTo(1), "Closing must not end the calendar day.");
            days.StartCafeFromMap();
            yield return WaitFor(() => days.CurrentPresentation is CafeOpeningPanel && !LoadingVisible(), "Resident cafe return");
            submit = ((DayFlowPresentationPanel)days.CurrentPresentation).GetComponentInChildren<Button>(true);
            yield return WaitFor(() => submit.interactable, "Cafe entrance");
            submit.onClick.Invoke();
            yield return WaitFor(() => days.State == DayFlowState.FreeCafeBusiness, "Cafe preparation");
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world), "Return must reuse the scene world.");
            Assert.That(GameSaveParticipantRegistry.PendingRestore == null
                || !GameSaveParticipantRegistry.PendingRestore.Any(p => p.Id == PlayerCafeOwner.SaveId), Is.True,
                "A resident adapter must not wait for a Start-only restore.");
            yield return null;
            Assert.That(days.CurrentPresentation, Is.Null, "Old Night state cannot repeat settlement.");
            var cafeHud = Object.FindObjectsByType<GameFlowPanel>(FindObjectsSortMode.None)
                .First(item => item.GetType().Name == "FreeCafeHud");
            var endDay = (Button)cafeHud.GetType().GetField("endDayButton").GetValue(cafeHud);
            var enterArea = endpoint.GetType().GetMethod("Enter");
            enterArea.Invoke(endpoint, new object[] { Spotlight.FreeCafe.AreaId.Counter });
            yield return null;
            Assert.That(endDay.gameObject.activeSelf, Is.False, "The workstation is not a rest entry.");
            enterArea.Invoke(endpoint, new object[] { Spotlight.FreeCafe.AreaId.Basement });
            yield return null;
            Assert.That(endDay.gameObject.activeSelf, Is.True, "Third-floor dormitory exposes explicit rest.");
            enterArea.Invoke(endpoint, new object[] { Spotlight.FreeCafe.AreaId.Cafe });
            yield return WaitFor(() => endDay.gameObject.activeInHierarchy && endDay.interactable,
                "Explicit end-day button is available in the first-floor lobby");
            endDay.onClick.Invoke();
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(world.State.day, Is.EqualTo(2));
            Assert.That(world.State.businessPhase, Is.EqualTo(Spotlight.FreeCafe.BusinessPhase.Preparation));
            Assert.That(world.State.open, Is.False);
            yield return WaitFor(() => days.State == DayFlowState.CityMapLeisure && !LoadingVisible(), "Next morning");
            days.StartCafeFromMap();
            yield return WaitFor(() => days.CurrentPresentation is CafeOpeningPanel && !LoadingVisible(), "Day-two cafe");
            submit = ((DayFlowPresentationPanel)days.CurrentPresentation).GetComponentInChildren<Button>(true);
            yield return WaitFor(() => submit.interactable, "Day-two entrance");
            submit.onClick.Invoke();
            yield return WaitFor(() => days.State == DayFlowState.FreeCafeBusiness, "Day-two preparation");
            requestStart.Invoke(endpoint, null);
            yield return WaitFor(() => world.State.open, "Day-two business through narrative bridge");
            Assert.That(world.State.day, Is.EqualTo(2));
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            // The timer closes the domain before DayFlow observes it; this path must settle once too.
            yield return null;
            Assert.That(world.EndDay().success, Is.True);
            yield return WaitFor(() => days.CurrentPresentation is BusinessEndPanel, "Domain-driven closing settlement");
            Assert.That(days.CurrentDay, Is.EqualTo(2));
        }

        private IEnumerator ExerciseWindows(GameplayHudPanel hud)
        {
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Click(hud.Artwork.Find("TasksToggle").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(hud.Tasks.IsVisible, Is.True);
            Assert.That(hud.EndDayButton, Is.Not.Null);
            Assert.That(hud.EndDayButton.gameObject.activeSelf, Is.False, "Cafe drawers cannot advance map days.");
            yield return Capture("cafe-shared-tasks");
            yield return Press(Key.T);
            Assert.That(hud.Tasks.IsVisible, Is.False);
            yield return Press(Key.B);
            Assert.That(hud.Inventory.IsVisible, Is.True);
            Click(hud.Artwork.Find("TasksToggle").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(hud.Tasks.IsVisible, Is.True);
            Assert.That(hud.Inventory.IsVisible, Is.False);
            yield return Press(Key.Escape);
            Assert.That(hud.Tasks.IsVisible, Is.False);
            Click(hud.Artwork.Find("详情面板").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(hud.IsDetailsOpen, Is.True);
            Assert.That(GameplayHudPanel.BlocksWorldInput, Is.True);
            Assert.That(PresentationPauseRegistry.IsPaused, Is.True);
            var details = UIKit.GetPanel<DetailsPanel>();
            Assert.That(details.CurrentData.Profile.Name, Is.EqualTo(PlayerRuntime.Current.PlayerName));
            details.ShowRelationships();
            details.ShowWorld();
            yield return Capture("cafe-shared-details");
            yield return Press(Key.Tab);
            yield return WaitFor(() => !hud.IsDetailsOpen, "Tab closes details");
            yield return Press(Key.Tab);
            Assert.That(hud.IsDetailsOpen, Is.True);
            yield return Press(Key.Escape);
            yield return WaitFor(() => !hud.IsDetailsOpen, "Escape closes details");
            Assert.That(GameplayHudPanel.BlocksWorldInput, Is.False);
            Time.timeScale = 1;
        }

        private static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Assert.That(point.x, Is.InRange(0, Screen.width));
            Assert.That(point.y, Is.InRange(0, Screen.height));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " must receive pointer input.");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject),
                button.name + " must be the topmost clickable control at its screen position. Hit: " + hits[0].gameObject.name + " at " + point + "; target rect " + rect.rect + "; target scale " + rect.lossyScale);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static bool LoadingVisible()
        {
            var shortPanel = UIKit.GetPanel<ShortLoadingPanel>();
            var fullPanel = UIKit.GetPanel<LoadingPanel>();
            return shortPanel != null && shortPanel.gameObject.activeInHierarchy
                || fullPanel != null && fullPanel.gameObject.activeInHierarchy;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 25f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, operation);
        }

        private static IEnumerator Capture(string name)
        {
            string directory = Path.Combine(Application.dataPath, "../Logs/GameplayHud");
            Directory.CreateDirectory(directory);
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(texture, Is.Not.Null);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
#endif
