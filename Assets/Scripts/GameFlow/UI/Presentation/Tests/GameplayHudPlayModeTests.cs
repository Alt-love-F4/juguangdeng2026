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
        public IEnumerator MapTodoNavigatesToCafeWithoutExecutingOrSpendingResources()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return WaitFor(() => GameplayHudPanel.Instance != null && PlayerRuntime.Current?.CreationSubmitted == true,
                "Standalone map todos");
            var hud = GameplayHudPanel.Instance;
            yield return WaitFor(() => hud.Tasks.CurrentGuidance?.id == "guide.map.cafe", "Cafe next-step guidance");
            Assert.That(hud.transform.Find("NextTodoGuidance"), Is.Null, "The persistent bottom-left todo hint has been removed.");
            int points = NarrativeRuntime.Instance.Actions.ActionPoints.Current;
            hud.Tasks.SetVisible(true);
            hud.Tasks.Navigate("guide.map.cafe");
            yield return null;
            var map = Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None)
                .First(candidate => candidate.isActiveAndEnabled && candidate.transform is RectTransform);
            var entry = map.GetComponentInChildren<MapActionDetailsView>(true).transform.Find("Viewport/ActionList/EnterCafe");
            Assert.That(entry, Is.Not.Null);
            Assert.That(NarrativeRuntime.Instance.Actions.ActionPoints.Current, Is.EqualTo(points));
            Assert.That(NarrativeRuntime.Instance.Actions.IsExecuting, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Map"), "Todo navigation opens details; player confirms entry.");
        }

        [UnityTest]
        public IEnumerator IdleRestTodoTravelsFromOtherSceneToThirdFloorWithoutSleeping()
        {
            yield return SceneManager.LoadSceneAsync("启动场景", LoadSceneMode.Single);
            var flow = Object.FindFirstObjectByType<GameFlowBootstrap>();
            yield return null;
            flow.StartGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Map"
                && UIKit.GetPanel<OpeningProloguePanel>() != null && !LoadingVisible(), "Formal entry for rest travel");
            yield return WaitFor(() => flow.DebugSkipTutorialToMap(out _), "Skip to map");
            var hud = GameplayHudPanel.Instance;
            Assert.That(hud.TryNavigateToCafeForRest(), Is.False, "First-day guide must not be bypassed.");
            Assert.That(hud.TryNavigateToCafe(), Is.True);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "CoffeeShop" && !LoadingVisible(), "Cafe tutorial entry");
            var days = flow.GetComponent<DayFlowController>();
            Assert.That(days.DebugSkipTutorialToCafe(out string error), Is.True, error);
            var world = PlayerCafeOwner.Current;
            // Mark business finished using the real domain entry, without advancing the calendar.
            var endpoint = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).First(item => item is IDayFlowGameplayEndpoint);
            endpoint.GetType().GetMethod("RequestBusinessStart").Invoke(endpoint, null);
            yield return WaitFor(() => world.State.open, "Business starts");
            world.EndDay();
            yield return WaitFor(() => days.CurrentPresentation is BusinessEndPanel, "Business settlement");
            var settlement = (DayFlowPresentationPanel)days.CurrentPresentation;
            var confirm = settlement.GetComponentInChildren<Button>(true);
            yield return WaitFor(() => confirm.interactable, "Settlement confirmation");
            confirm.onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Map" && !LoadingVisible(), "After-business map");
            yield return WaitFor(() => hud.Tasks.CurrentGuidance?.id == GameplayTodoProjection.RestTodoId, "Idle suggests three-floor sleep");
            int day = days.CurrentDay;
            int points = NarrativeRuntime.Instance.Actions.ActionPoints.Current;
            int money = world.State.money;
            var other = SceneManager.CreateScene("TodoTravelOtherGameplay");
            SceneManager.SetActiveScene(other);
            yield return null;
            Assert.That(hud.TryNavigateToCafeForRest(), Is.True, "Travel works outside Map and CoffeeShop.");
            Assert.That(hud.TryNavigateToCafeForRest(), Is.False, "Repeated clicks cannot start another handoff.");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "CoffeeShop" && !LoadingVisible()
                && world.State.currentArea == Spotlight.FreeCafe.AreaId.Basement, "Arrive at third floor");
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(days.CurrentDay, Is.EqualTo(day));
            Assert.That(NarrativeRuntime.Instance.Actions.ActionPoints.Current, Is.EqualTo(points));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(world.State.open, Is.False);
            Assert.That(hud.IsVisible, Is.True);
            Assert.That(flow.DebugForceOpenMap(out string overlayError), Is.True, overlayError);
            yield return WaitFor(() => flow.IsMapOpen && !LoadingVisible(), "Open map over resident cafe");
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(hud.TryNavigateToCafeForRest(), Is.True);
            yield return WaitFor(() => !flow.IsMapOpen && world.State.currentArea == Spotlight.FreeCafe.AreaId.Basement,
                "Rest navigation closes map and reuses third floor");
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(days.CurrentDay, Is.EqualTo(day));
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
        public IEnumerator BusinessMapKeyboardBrowsingAndCafeReturnKeepBusinessOpen()
        {
            GameFlowBootstrap flow = null;
            yield return OpenDayOneCafeBusiness(value => flow = value);
            var days = flow.GetComponent<DayFlowController>();
            var runtime = NarrativeRuntime.Instance;
            var actions = runtime.Actions;
            var world = PlayerCafeOwner.Current;
            var hud = GameplayHudPanel.Instance;
            var player = PlayerRuntime.Current;
            int points = actions.ActionPoints.Current;
            int day = days.CurrentDay;

            void AssertBusinessUnchanged()
            {
                Assert.That(world.State.open, Is.True, "Opening, inspecting or leaving the map must not close business.");
                Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
                Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
                Assert.That(PlayerRuntime.Current, Is.SameAs(player));
                Assert.That(days.State, Is.EqualTo(DayFlowState.FreeCafeBusiness));
                Assert.That(days.CurrentPresentation, Is.Null, "Map navigation must not start settlement.");
                Assert.That(days.CurrentDay, Is.EqualTo(day));
                Assert.That(world.State.day, Is.EqualTo(day));
                Assert.That(actions.CurrentDay, Is.EqualTo(day));
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(points));
                Assert.That(actions.IsExecuting, Is.False);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoffeeShop"));
            }

            Assert.That(flow.CanOpenMap, Is.True);
            AssertMapActiveCondition(runtime, false);
            yield return Press(Key.M);
            yield return WaitFor(() => flow.IsMapOpen && !LoadingVisible(), "M opens map over formal business");
            AssertMapActiveCondition(runtime, true);
            AssertBusinessUnchanged();
            var map = Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None)
                .First(candidate => candidate.isActiveAndEnabled && candidate.transform is RectTransform);
            var cafe = map.transform.Find("MapViewport/MapContent/Entities/Location:cafe").GetComponent<Button>();
            Assert.That(cafe.isActiveAndEnabled && cafe.interactable, Is.True);
            cafe.onClick.Invoke();
            yield return null;
            Assert.That(map.IsLocationPopupVisible, Is.True, "The real cafe location button must open its details.");
            AssertBusinessUnchanged();

            yield return Press(Key.M);
            yield return WaitFor(() => !flow.IsMapOpen, "M returns to ongoing cafe business");
            AssertMapActiveCondition(runtime, false);
            AssertBusinessUnchanged();
            yield return Press(Key.M);
            yield return WaitFor(() => flow.IsMapOpen && !LoadingVisible(), "Reopen map for cafe navigation");
            AssertMapActiveCondition(runtime, true);
            cafe.onClick.Invoke();
            yield return null;
            Assert.That(map.IsLocationPopupVisible, Is.True);
            var details = map.GetComponentInChildren<MapActionDetailsView>(true);
            Assert.That(details, Is.Not.Null);
            var entry = details.transform.Find("Viewport/ActionList/EnterCafe");
            Assert.That(entry, Is.Not.Null, "The real cafe details must include its navigation card.");
            // Navigation has no NCS action key, so SelectAction cannot select it. Page through
            // the real controls until EnterCafe, rather than a different action, is visible.
            var nextPage = details.transform.Find("PageNavigation/Next").GetComponent<Button>();
            int pageLimit = entry.parent.childCount;
            for (int page = 0; !entry.gameObject.activeInHierarchy && page < pageLimit; page++)
            {
                Assert.That(nextPage.isActiveAndEnabled && nextPage.interactable, Is.True);
                nextPage.onClick.Invoke();
                yield return null;
            }
            Assert.That(entry.gameObject.activeInHierarchy, Is.True, "Cafe navigation must be the visible card.");
            var execute = entry.Find("Execute") ?? entry.Find("ExecutePreview");
            Assert.That(execute, Is.Not.Null);
            var enterCafe = execute.GetComponent<Button>();
            Assert.That(enterCafe.isActiveAndEnabled && enterCafe.interactable, Is.True);
            enterCafe.onClick.Invoke();
            yield return WaitFor(() => !flow.IsMapOpen && !LoadingVisible(), "Cafe navigation returns without closing business");
            AssertMapActiveCondition(runtime, false);
            AssertBusinessUnchanged();
        }

        [UnityTest]
        public IEnumerator PausedMapRequestShowsCoalescedNonInteractiveBlockedToast()
        {
            GameFlowBootstrap flow = null;
            yield return OpenDayOneCafeBusiness(value => flow = value);
            UIKit.ClosePanel<OperationToastPanel>();
            UIKit.UnloadPanel<OperationToastPanel>();
            // ShowBlocked keeps its one-second cooldown across panel lifetimes.
            yield return new WaitForSecondsRealtime(1.05f);
            var paused = typeof(GameFlowBootstrap).GetField("paused",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(paused, Is.Not.Null);
            bool wasPaused = (bool)paused.GetValue(flow);
            try
            {
                paused.SetValue(flow, true);
                flow.OpenMap();
                Assert.That(flow.IsMapOpen, Is.False, "Rejected input must not open the map.");
                var toast = Object.FindFirstObjectByType<OperationToastPanel>();
                Assert.That(toast, Is.Not.Null);
                Assert.That(toast.Message, Does.Contain("恢复游戏"));
                Assert.That(toast.Message, Is.EqualTo(flow.InteractionBlockedMessage("打开地图")));
                var group = toast.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
                var animation = typeof(OperationToastPanel).GetField("animation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(animation, Is.Not.Null);
                var runningAnimation = animation.GetValue(toast);
                Assert.That(runningAnimation, Is.Not.Null);
                string message = toast.Message;
                // All calls are in the same frame, safely inside ShowBlocked's one-second window.
                OperationToastPanel.ShowBlocked(message);
                OperationToastPanel.ShowBlocked(message);
                Assert.That(Object.FindObjectsByType<OperationToastPanel>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindFirstObjectByType<OperationToastPanel>(), Is.SameAs(toast));
                Assert.That(toast.Message, Is.EqualTo(message));
                Assert.That(animation.GetValue(toast), Is.SameAs(runningAnimation), "Repeated blocked input must not restart the toast.");
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
            }
            finally
            {
                paused.SetValue(flow, wasPaused);
                UIKit.ClosePanel<OperationToastPanel>();
                UIKit.UnloadPanel<OperationToastPanel>();
            }
        }

        [UnityTest]
        public IEnumerator BusinessMapActionButtonSettlesBeforeExecutingOnceWithoutAdvancingDay()
        {
            GameFlowBootstrap flow = null;
            yield return OpenDayOneCafeBusiness(value => flow = value);
            var days = flow.GetComponent<DayFlowController>();
            var runtime = NarrativeRuntime.Instance;
            var actions = runtime.Actions;
            var world = PlayerCafeOwner.Current;
            var action = MountBusinessMapTestAction(actions);
            int points = actions.ActionPoints.Current;
            int day = days.CurrentDay;
            int started = 0;
            int finished = 0;
            bool succeeded = false;
            bool startedAfterSettlementOnMap = false;
            void OnStarted(NarrativeActionDefinition value)
            {
                if (value != action) return;
                started++;
                startedAfterSettlementOnMap = !world.State.open && days.CurrentPresentation == null
                    && days.State == DayFlowState.CityMapLeisure && SceneManager.GetActiveScene().name == "Map";
            }
            void OnFinished(NarrativeActionDefinition value, bool success)
            {
                if (value != action) return;
                finished++;
                succeeded = success;
            }
            actions.ActionStarted += OnStarted;
            actions.ActionFinished += OnFinished;
            try
            {
                yield return Press(Key.M);
                yield return WaitFor(() => flow.IsMapOpen && !LoadingVisible(), "M opens business map for action");
                AssertMapActiveCondition(runtime, true);
                var map = Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None)
                    .First(candidate => candidate.isActiveAndEnabled && candidate.transform is RectTransform);
                var details = map.GetComponentInChildren<MapActionDetailsView>(true);
                // Day one deliberately hides non-cafe popups. This is an NCS action mounted at
                // cafe, not the separate cafe-return navigation, and needs no authored story UI.
                details.Show("cafe", "咖啡馆");
                Assert.That(details.SelectAction(action.actionKey), Is.True);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(world.State.open, Is.True, "Viewing the action must leave business open.");
                Assert.That(days.CurrentPresentation, Is.Null);
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(points), "Viewing must not spend AP.");
                Assert.That(actions.GetProgress(action.actionKey), Is.Zero);
                Assert.That(started, Is.Zero);
                Assert.That(actions.IsAvailable(action.actionKey, out _), Is.True, "The test must reach the valid OnExecute handoff.");
                var card = details.transform.Find("Viewport/ActionList/Action_" + action.actionKey);
                Assert.That(card, Is.Not.Null);
                var execute = card.Find("Execute") ?? card.Find("ExecutePreview");
                Assert.That(execute, Is.Not.Null);
                var button = execute.GetComponent<Button>();
                Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
                button.onClick.Invoke(); // Real authored listener -> OnExecute -> formal settlement.

                yield return WaitFor(() => days.CurrentPresentation is BusinessEndPanel, "Action request opens business settlement");
                Assert.That(world.State.open, Is.False, "The real cafe world must close before the action executes.");
                Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
                Assert.That(days.CurrentDay, Is.EqualTo(day));
                Assert.That(world.State.day, Is.EqualTo(day));
                Assert.That(actions.CurrentDay, Is.EqualTo(day));
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(points), "A pending request must not spend AP.");
                Assert.That(actions.IsExecuting, Is.False);
                Assert.That(actions.GetProgress(action.actionKey), Is.Zero);
                Assert.That(started, Is.Zero, "Execution must wait for settlement confirmation and AfterBusiness.");
                Assert.That(finished, Is.Zero);
                var settlement = (DayFlowPresentationPanel)days.CurrentPresentation;
                var confirm = settlement.GetComponentInChildren<Button>(true);
                yield return WaitFor(() => confirm.interactable, "Requested-action settlement confirmation");
                yield return null;
                Assert.That(started, Is.Zero, "An interactable settlement is not a confirmed settlement.");
                Assert.That(actions.GetProgress(action.actionKey), Is.Zero);
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(points));
                confirm.onClick.Invoke();

                yield return WaitFor(() => finished == 1 && SceneManager.GetActiveScene().name == "Map"
                    && !LoadingVisible(), "AfterBusiness resumes requested action on active map");
                yield return null;
                yield return null;
                Assert.That(startedAfterSettlementOnMap, Is.True);
                Assert.That(started, Is.EqualTo(1));
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(succeeded, Is.True);
                Assert.That(actions.GetProgress(action.actionKey), Is.Zero, "Repeat actions do not maintain a progress counter.");
                Assert.That(actions.HasExecutedToday(action.actionKey), Is.True);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(started, Is.EqualTo(1), "The deferred request must not execute twice.");
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(actions.IsExecuting, Is.False);
                Assert.That(world.State.open, Is.False);
                Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
                Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
                Assert.That(days.State, Is.EqualTo(DayFlowState.CityMapLeisure));
                Assert.That(days.CurrentDay, Is.EqualTo(day), "Closing for an action must not advance the calendar.");
                Assert.That(world.State.day, Is.EqualTo(day));
                Assert.That(actions.CurrentDay, Is.EqualTo(day));
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(points), "The zero-cost fixture must not spend AP.");
            }
            finally
            {
                actions.ActionStarted -= OnStarted;
                actions.ActionFinished -= OnFinished;
            }
        }

        [UnityTest]
        public IEnumerator DayTwoBusinessSignClosesWithOrdinaryCustomersAndReturnsToMapWithoutAdvancingDay()
        {
            GameFlowBootstrap flow = null;
            yield return OpenDayOneCafeBusiness(value => flow = value);
            var days = flow.GetComponent<DayFlowController>();
            var world = PlayerCafeOwner.Current;
            var runtime = NarrativeRuntime.Instance;
            var player = PlayerRuntime.Current;

            // Finish the actual day-one session first. Changing its calendar while it is
            // still open would preserve Open, so day two would never exercise a fresh start.
            Assert.That(world.EndDay().success, Is.True);
            yield return WaitFor(() => days.CurrentPresentation is BusinessEndPanel, "Day-one setup settlement");
            var dayOneSettlement = (DayFlowPresentationPanel)days.CurrentPresentation;
            var dayOneConfirm = dayOneSettlement.GetComponentInChildren<Button>(true);
            Assert.That(dayOneConfirm, Is.Not.Null);
            yield return WaitFor(() => dayOneConfirm.interactable, "Day-one setup settlement confirmation");
            dayOneConfirm.onClick.Invoke();
            yield return WaitFor(() => days.State == DayFlowState.CityMapLeisure
                && SceneManager.GetActiveScene().name == "Map" && !LoadingVisible(), "Day-one setup returns to map");
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.businessPhase, Is.EqualTo(Spotlight.FreeCafe.BusinessPhase.Night));

            // FreeCafeBusiness is save enum value 3 (5 is CityMapLeisure). Restore only
            // the normal day-two phase; retain the real session, world and endpoint.
            Assert.That((int)DayFlowState.FreeCafeBusiness, Is.EqualTo(3));
            days.RestoreJson("{\"day\":2,\"state\":3,\"firstDayIntroductionCompleted\":true,"
                + "\"firstDayCafeTutorialStarted\":true,\"firstDayCafeTutorialCompleted\":true,"
                + "\"mapUsageTutorialCompleted\":true,\"lastCafeEntryDay\":2}");
            world.SynchronizeCalendarDay(2);
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(days.State, Is.EqualTo(DayFlowState.FreeCafeBusiness));
            Assert.That(world.State.day, Is.EqualTo(2));
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.businessPhase, Is.EqualTo(Spotlight.FreeCafe.BusinessPhase.Preparation));
            bool arrived = false;
            flow.TransitionToGameplayScene("CoffeeShop", () => arrived = true);
            yield return WaitFor(() => arrived && !LoadingVisible(), "Return to real cafe for day-two business");
            days.ResumeSavedPhase();
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(days.IsCafeTutorialActive, Is.False);
            Assert.That(days.CurrentPresentation, Is.Null);

            // FreeCafe.Unity is intentionally not referenced by this test assembly.
            var endpoint = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(item => item is IDayFlowGameplayEndpoint);
            var requestStart = endpoint.GetType().GetMethod("RequestBusinessStart");
            var requestEnd = endpoint.GetType().GetMethod("RequestBusinessEnd");
            Assert.That(requestStart, Is.Not.Null);
            Assert.That(requestEnd, Is.Not.Null);
            requestStart.Invoke(endpoint, null);
            yield return WaitFor(() => world.State.open && days.State == DayFlowState.FreeCafeBusiness
                && !LoadingVisible(), "Day-two sign starts real business");
            yield return WaitFor(() => days.CanFinishBusiness, "Day-two formal closing gate is ready");

            // A real unserved ordinary customer must still be seated when the sign is
            // used; do not remove orders or close the domain directly on the target day.
            var ordinaryRequest = world.State.requests.FirstOrDefault(request =>
                request.id != Spotlight.FreeCafe.CafeWorld.TutorialOrderId
                && world.Find(request.customerId) is { } customer
                && customer.specId == "customer" && customer.area == Spotlight.FreeCafe.AreaId.Cafe
                && customer.person != null && customer.person.role == Spotlight.FreeCafe.CafePersonRole.Customer
                && string.IsNullOrEmpty(customer.person.narrativeCharacterId));
            Assert.That(ordinaryRequest, Is.Not.Null, "Day-two StartDay must spawn a real ordinary customer with an unfinished order.");
            Assert.That(world.HasTutorialCustomer, Is.False);
            Assert.That(world.State.open, Is.True);
            Assert.That(days.CanFinishBusiness, Is.True);
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(world.State.day, Is.EqualTo(2));
            // Resolve the actual authored business-control entity, not an assumed ID.
            var signEntity = world.State.entities.First(entity => world.Spec(entity.specId).Has(Spotlight.FreeCafe.Capability.BusinessControl));
            var signView = endpoint.GetType().GetMethod("View").Invoke(endpoint, new object[] { signEntity.id });
            Assert.That(signView, Is.Not.Null);
            var sign = ((Component)signView).GetComponents<MonoBehaviour>()
                .First(component => component.GetType().Name == "FreeCafeBusinessSignView");
            var closingAllowed = endpoint.GetType().GetProperty("CanEndBusiness");
            Assert.That((bool)closingAllowed.GetValue(endpoint), Is.True);
            // Exercise the ignored-click window: explicitly begin the existing opening
            // animation, then click while it is still running. No fake business completion.
            sign.GetType().GetMethod("BeginFlip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(sign, new object[] { true, null });
            Assert.That((bool)sign.GetType().GetProperty("IsFlipping").GetValue(sign), Is.True);
            endpoint.GetType().GetMethod("Click").Invoke(endpoint, new[] { signView });

            yield return WaitFor(() => days.CurrentPresentation is BusinessEndPanel, "Day-two sign opens business settlement");
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.businessPhase, Is.EqualTo(Spotlight.FreeCafe.BusinessPhase.Night));
            Assert.That(world.State.requests, Is.Empty, "Closing must settle even the unfinished ordinary orders.");
            Assert.That(days.CurrentDay, Is.EqualTo(2));
            Assert.That(world.State.day, Is.EqualTo(2));
            var settlement = (DayFlowPresentationPanel)days.CurrentPresentation;
            var confirm = settlement.GetComponentInChildren<Button>(true);
            Assert.That(confirm, Is.Not.Null);
            yield return WaitFor(() => confirm.interactable, "Day-two settlement confirmation");
            Assert.That(days.CurrentPresentation, Is.SameAs(settlement), "Settlement must wait for player confirmation.");
            confirm.onClick.Invoke();

            yield return WaitFor(() => days.State == DayFlowState.CityMapLeisure
                && SceneManager.GetActiveScene().name == "Map" && !LoadingVisible(), "Day-two confirmed settlement returns to map");
            Assert.That(days.CurrentPresentation, Is.Null);
            Assert.That(days.CurrentDay, Is.EqualTo(2), "Closing is not sleeping or advancing to day three.");
            Assert.That(world.State.day, Is.EqualTo(2));
            Assert.That(world.State.open, Is.False);
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(NarrativeRuntime.Instance, Is.SameAs(runtime));
            Assert.That(PlayerRuntime.Current, Is.SameAs(player));
        }

        private IEnumerator OpenDayOneCafeBusiness(Action<GameFlowBootstrap> ready)
        {
            yield return SceneManager.LoadSceneAsync("启动场景", LoadSceneMode.Single);
            var flow = Object.FindFirstObjectByType<GameFlowBootstrap>();
            Assert.That(flow, Is.Not.Null);
            yield return null;
            flow.StartGame();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Map"
                && UIKit.GetPanel<OpeningProloguePanel>() != null && !LoadingVisible(), "Formal new-game business session");
            yield return WaitFor(() => flow.DebugSkipTutorialToMap(out _), "Skip arrival tutorial to formal map");
            bool arrived = false;
            flow.TransitionToGameplayScene("CoffeeShop", () => arrived = true);
            yield return WaitFor(() => arrived && !LoadingVisible(), "Formal day-one cafe entry");
            var days = flow.GetComponent<DayFlowController>();
            Assert.That(days.DebugSkipTutorialToCafe(out string error), Is.True, error);
            var world = PlayerCafeOwner.Current;
            var endpoint = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(item => item is IDayFlowGameplayEndpoint);
            // FreeCafe.Unity is intentionally not referenced by this test assembly.
            var requestStart = endpoint.GetType().GetMethod("RequestBusinessStart");
            Assert.That(requestStart, Is.Not.Null);
            requestStart.Invoke(endpoint, null);
            yield return WaitFor(() => world.State.open && days.State == DayFlowState.FreeCafeBusiness
                && !LoadingVisible(), "Open real day-one business through the formal endpoint");
            yield return null;
            Assert.That(days.CurrentDay, Is.EqualTo(1));
            Assert.That(world.State.day, Is.EqualTo(1));
            Assert.That(NarrativeRuntime.Instance.Actions.CurrentDay, Is.EqualTo(1));
            Assert.That(days.CurrentPresentation, Is.Null);
            ready(flow);
        }

        private static void AssertMapActiveCondition(NarrativeRuntime runtime, bool expected)
        {
            if (runtime.Controller.State.TryGetCondition("world.map.active", out bool active))
                Assert.That(active, Is.EqualTo(expected), "The declared map-active condition must follow the visible map, not just the active scene.");
        }

        private static NarrativeActionDefinition MountBusinessMapTestAction(NarrativeActionController actions)
        {
            var action = new NarrativeActionDefinition
            {
                actionKey = "test.business.map_action", nameText = "营业中地图测试行动",
                type = NarrativeActionType.Repeat, requirements = string.Empty,
                parsedRequirements = new List<NarrativeActionRequirement>(), resultEvents = new List<string>()
            };
            // Keep the real formal runtime/controller/presenter. Inject only a transient fixture
            // definition; no product configuration or FreeCafe.Unity dependency is needed.
            var field = typeof(NarrativeActionController).GetField("definitions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var definitions = (Dictionary<string, NarrativeActionDefinition>)field.GetValue(actions);
            definitions.Add(action.actionKey, action);
            actions.Mount("cafe", action.actionKey);
            return action;
        }

        [UnityTest]
        public IEnumerator ClosingKeepsDateAndResidentCafeUntilExplicitRestThenNextDayCanOpen()
            => ClosingAndMapTutorial(false);

        [UnityTest]
        public IEnumerator FirstBusinessClosingOffersMapTutorialAndSupportsReplay()
            => ClosingAndMapTutorial(true);

        [UnityTest]
        public IEnumerator InstantDomainClosingOffersMapTutorialAndSupportsReplay()
            => ClosingAndMapTutorial(true, true);

        [UnityTest]
        public IEnumerator BedClosingOffersMapTutorialWithoutAdvancingDayAndSupportsReplay()
            => ClosingAndMapTutorial(true, false, true);

        private IEnumerator ClosingAndMapTutorial(bool mapOnly, bool instantDomainClose = false, bool bedClose = false)
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
            Assert.That(days.ShouldShowMapUsageTutorial, Is.False, "The map usage tutorial must not precede day-one business.");
            Assert.That(days.DebugSkipTutorialToCafe(out string error), Is.True, error);
            Assert.That(days.ShouldShowMapUsageTutorial, Is.False, "Finishing the cafe introduction must not start the map usage tutorial.");
            var world = PlayerCafeOwner.Current;
            var endpoint = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(item => item is IDayFlowGameplayEndpoint);
            var requestStart = endpoint.GetType().GetMethod("RequestBusinessStart");
            requestStart.Invoke(endpoint, null);
            if (instantDomainClose)
            {
                // Do not yield between opening and closing: DayFlow.Update must not observe an open frame.
                // Settlement must also be driven by the binding to world.Changed.
                Assert.That(world.State.open, Is.True, "The instant-close regression requires synchronous business opening.");
                Assert.That(world.EndDay().success, Is.True);
            }
            else
            {
                yield return WaitFor(() => world.State.open, "Formal business start through narrative bridge");
                yield return null;
                if (bedClose) days.EndDayFromDormitory();
                else
                {
                    var requestEnd = endpoint.GetType().GetMethod("RequestBusinessEnd");
                    Assert.That(requestEnd, Is.Not.Null);
                    requestEnd.Invoke(endpoint, null);
                }
            }
            yield return WaitFor(() => days.CurrentPresentation is BusinessEndPanel, "Business closing settlement");
            Assert.That(days.CurrentDay, Is.EqualTo(1));
            Assert.That(world.State.day, Is.EqualTo(1));
            Assert.That(world.StartDay(advanceFromNight: false).success, Is.False);
            var settlement = (DayFlowPresentationPanel)days.CurrentPresentation;
            Assert.That(settlement, Is.TypeOf<BusinessEndPanel>());
            Assert.That(days.ShouldShowMapUsageTutorial, Is.False, "Settlement must block the pending map usage tutorial.");
            var submit = settlement.GetComponentInChildren<Button>(true);
            yield return WaitFor(() => submit.interactable, "Settlement entrance");
            Assert.That(days.ShouldShowMapUsageTutorial, Is.False, "The tutorial must remain blocked while settlement is visible.");
            submit.onClick.Invoke();
            yield return WaitFor(() => days.State == DayFlowState.CityMapLeisure
                && SceneManager.GetActiveScene().name == "Map" && !LoadingVisible(), "After-business map");
            Assert.That(days.CurrentDay, Is.EqualTo(1), "Closing must not end the calendar day.");
            var actions = NarrativeRuntime.Instance.Actions;
            int tutorialPoints = actions.ActionPoints.Current;
            int tutorialMoney = world.State.money;
            int tutorialDay = days.CurrentDay;
            int actionDay = actions.CurrentDay;
            void AssertTutorialResourcesUnchanged()
            {
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(tutorialPoints), "Viewing the map tutorial must not spend action points.");
                Assert.That(world.State.money, Is.EqualTo(tutorialMoney), "Viewing the map tutorial must not spend money.");
                Assert.That(days.CurrentDay, Is.EqualTo(tutorialDay));
                Assert.That(actions.CurrentDay, Is.EqualTo(actionDay));
                Assert.That(world.State.day, Is.EqualTo(tutorialDay), "Viewing or completing the tutorial must not advance the day.");
            }

            MapSceneController map = null;
            yield return WaitFor(() => (map = Object.FindObjectsByType<MapSceneController>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.isActiveAndEnabled && candidate.transform is RectTransform
                    && candidate.UsageTutorialAvailable)) != null, "After-business map controls ready");
            var mapHud = GameplayHudPanel.Instance;
            Assert.That(mapHud, Is.Not.Null);
            Assert.That(mapHud.EndDayButton, Is.Null, "The map HUD must not expose an end-day shortcut.");
            Assert.That(mapHud.Tasks.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "结束当天"), Is.False, "The task panel must not contain even an inactive end-day object.");
            var mapTodos = new List<MapTaskItem>();
            map.CollectTodos(mapTodos);
            Assert.That(mapTodos.Any(item => item.id == "guide.map.rest"), Is.False,
                "Map guidance must not provide the removed direct-rest action.");
            var mapState = days.State;
            var businessPhase = world.State.businessPhase;
            days.EndDayFromMap();
            Assert.That(days.CurrentDay, Is.EqualTo(tutorialDay), "The legacy map endpoint must not advance the day.");
            Assert.That(days.State, Is.EqualTo(mapState), "The legacy map endpoint must not leave the map state.");
            Assert.That(world.State.businessPhase, Is.EqualTo(businessPhase));
            Assert.That(world.State.open, Is.False);
            AssertTutorialResourcesUnchanged();
            // The tutorial lives in the cafe Unity assembly, which this test assembly cannot reference directly.
            MonoBehaviour tutorial = null;
            yield return WaitFor(() => (tutorial = map.GetComponents<MonoBehaviour>()
                .FirstOrDefault(item => item.GetType().Name == "MapUsageTutorial")) != null
                && (bool)tutorial.GetType().GetProperty("IsRunning").GetValue(tutorial), "Automatic map usage tutorial");
            var tutorialType = tutorial.GetType();
            var isRunning = tutorialType.GetProperty("IsRunning");
            var currentStage = tutorialType.GetProperty("CurrentStage");
            var advance = tutorialType.GetMethod("Advance");
            var stop = tutorialType.GetMethod("Stop");
            Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("Overview"));
            Assert.That(days.ShouldShowMapUsageTutorial, Is.True);
            StringAssert.Contains("\"mapUsageTutorialPending\":true", days.CaptureJson());
            StringAssert.Contains("\"mapUsageTutorialCompleted\":false", days.CaptureJson());
            AssertTutorialResourcesUnchanged();

            // Cancelling is not completion; leaving and re-entering the resident map must offer it again.
            string pendingRecord = days.CaptureJson();
            var tutorialPanel = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(item => item.GetType().Name == "FreeCafeTutorialPanel" && item.gameObject.name == "MapUsageTutorialPanel");
            var dismissTutorial = (Button)tutorialPanel.GetType().GetField("exitButton").GetValue(tutorialPanel);
            dismissTutorial.onClick.Invoke();
            Assert.That((bool)isRunning.GetValue(tutorial), Is.False);
            Assert.That(days.CaptureJson(), Is.EqualTo(pendingRecord));
            bool cancellationVisit = false;
            flow.TransitionToGameplayScene("CoffeeShop", () => cancellationVisit = true);
            yield return WaitFor(() => cancellationVisit && !LoadingVisible(), "Leave map with tutorial pending");
            Assert.That((bool)isRunning.GetValue(tutorial), Is.False);
            Assert.That(days.CaptureJson(), Is.EqualTo(pendingRecord));
            bool returnedToMap = false;
            flow.TransitionToGameplayScene("Map", () => returnedToMap = true);
            yield return WaitFor(() => returnedToMap && !LoadingVisible() && map.UsageTutorialAvailable
                && (bool)isRunning.GetValue(tutorial), "Pending tutorial restarts on map re-entry");
            Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("Overview"));
            Assert.That(days.CaptureJson(), Is.EqualTo(pendingRecord));
            AssertTutorialResourcesUnchanged();

            advance.Invoke(tutorial, null);
            Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("SelectLocation"));
            advance.Invoke(tutorial, null);
            Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("SelectLocation"),
                "Advance cannot bypass selecting a real map location.");
            Assert.That(days.CaptureJson(), Is.EqualTo(pendingRecord));
            var cafeLocation = map.transform.Find("MapViewport/MapContent/Entities/Location:cafe").GetComponent<Button>();
            Assert.That(cafeLocation.isActiveAndEnabled && cafeLocation.interactable, Is.True);
            bool locationSelected = false;
            string selectedLocation = null;
            Action<string, bool> onLocationSelected = (key, popupVisible) =>
            {
                locationSelected = true;
                selectedLocation = key;
            };
            map.LocationSelected += onLocationSelected;
            try
            {
                // Invoke the authored button rather than a tutorial callback; the tutorial footer can cover it.
                cafeLocation.onClick.Invoke();
                yield return WaitFor(() => locationSelected, "Cafe button raises LocationSelected");
                Assert.That(selectedLocation, Is.EqualTo("cafe"));
                Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("ReadLocation"),
                    "Reading the location must follow the real map selection event.");
            }
            finally
            {
                map.LocationSelected -= onLocationSelected;
            }
            AssertTutorialResourcesUnchanged();
            Assert.That(days.CaptureJson(), Is.EqualTo(pendingRecord), "Reading a location does not complete the tutorial.");
            advance.Invoke(tutorial, null);
            Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("Finish"));
            Assert.That((bool)isRunning.GetValue(tutorial), Is.True);
            Assert.That(days.CaptureJson(), Is.EqualTo(pendingRecord), "The final page still requires confirmation.");
            advance.Invoke(tutorial, null);
            Assert.That((bool)isRunning.GetValue(tutorial), Is.False);
            Assert.That(days.ShouldShowMapUsageTutorial, Is.False);
            string completedRecord = days.CaptureJson();
            StringAssert.Contains("\"mapUsageTutorialCompleted\":true", completedRecord);
            StringAssert.Contains("\"mapUsageTutorialPending\":false", completedRecord);
            AssertTutorialResourcesUnchanged();
            yield return null; // Let the completed panel be destroyed before GM replay.

            var debugPlay = tutorialType.GetMethod("DebugPlay");
            Assert.That(debugPlay, Is.Not.Null);
            object[] debugArguments = { null };
            Assert.That((bool)debugPlay.Invoke(null, debugArguments), Is.True, debugArguments[0] as string);
            Assert.That((bool)isRunning.GetValue(tutorial), Is.True, "GM replay must start even after formal completion.");
            Assert.That(currentStage.GetValue(tutorial).ToString(), Is.EqualTo("Overview"));
            Assert.That(days.ShouldShowMapUsageTutorial, Is.False);
            Assert.That(days.CaptureJson(), Is.EqualTo(completedRecord), "GM replay must not change the completion record.");
            AssertTutorialResourcesUnchanged();
            stop.Invoke(tutorial, null);
            Assert.That((bool)isRunning.GetValue(tutorial), Is.False);
            Assert.That(days.CaptureJson(), Is.EqualTo(completedRecord), "Stopping GM replay must leave the completion record intact.");
            AssertTutorialResourcesUnchanged();
            yield return null;
            if (mapOnly) yield break;
            days.StartCafeFromMap();
            yield return WaitFor(() => days.State == DayFlowState.FreeCafeBusiness && !LoadingVisible(), "Resident cafe return without an opening page");
            Assert.That(days.CurrentPresentation, Is.Null, "Returning after closing must not replay the cafe opening page.");
            Assert.That(world.State.businessPhase, Is.EqualTo(Spotlight.FreeCafe.BusinessPhase.Night));
            Assert.That(world.State.open, Is.False, "A same-day return must not reopen the closed business.");
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
            Assert.That(endDay.gameObject.activeSelf, Is.False, "Leaving the third-floor dormitory hides the next-day shortcut.");
            endDay.onClick.Invoke();
            Assert.That(days.CurrentDay, Is.EqualTo(1), "Even a programmatic click outside the third floor must not advance the day.");
            enterArea.Invoke(endpoint, new object[] { Spotlight.FreeCafe.AreaId.Basement });
            yield return WaitFor(() => endDay.gameObject.activeInHierarchy && endDay.interactable,
                "Explicit end-day button is available only in the third-floor dormitory");
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
            Assert.That(hud.EndDayButton, Is.Null, "Only the cafe bed may end the day; map drawers have no end-day button.");
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
