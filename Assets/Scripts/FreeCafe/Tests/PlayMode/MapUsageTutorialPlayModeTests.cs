#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    /// <summary>Read-only tutorial coverage using the authored map and shared tutorial prefab.</summary>
    public sealed class MapUsageTutorialPlayModeTests
    {
        private GameObject host, map, runtimeObject;
        private MapSceneController controller;
        private MapUsageTutorial tutorial;
        private NarrativeRuntime runtime, previousRuntime;
        private DayFlowController days;
        private Func<GameFlowBootstrap, GameFlowPanel> previousOpenPanel;
        private Type previousPanelType;
        private bool previousScenePanel;
        private PropertyInfo instance;
        private float previousTimeScale;
        private Scene debugScene, previousScene;
        private int started, settled;
        private string selectedKey;
        private bool selectedPopup;

        private sealed class ImmediatePresenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) => completed?.Invoke();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousOpenPanel = GameplayPanelHooks.OpenPanel;
            previousPanelType = GameplayPanelHooks.PanelType;
            previousScenePanel = GameplayPanelHooks.IsScenePanel;
            previousScene = SceneManager.GetActiveScene();
            debugScene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            SceneManager.SetActiveScene(debugScene);
            previousRuntime = NarrativeRuntime.Instance;
            instance = typeof(NarrativeRuntime).GetProperty(nameof(NarrativeRuntime.Instance));

            runtimeObject = new GameObject("MapUsageTutorialTestRuntime");
            runtimeObject.SetActive(false); // Do not run Awake or replace real session services/presentation.
            runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            runtime.Initialize(LubanNarrativeLoader.Load(), new ImmediatePresenter());
            instance.GetSetMethod(true).Invoke(null, new object[] { runtime });

            YokiFrame.UIKit.ClosePanel<GameplayHudPanel>();
            YokiFrame.UIKit.UnloadPanel<GameplayHudPanel>();
            yield return null;
            host = new GameObject("MapUsageTutorialTestHost");
            host.SetActive(false);
            var prefab = Resources.Load<GameObject>("Art/UIPrefab/MapPanel");
            Assert.That(prefab, Is.Not.Null);
            map = Object.Instantiate(prefab, host.transform, false);
            map.GetComponent<Canvas>().sortingOrder = 500;
            // Match MapInitializationPlayModeTests: activate only after all nested widgets exist.
            map.SetActive(true);
            host.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            controller = map.GetComponent<MapSceneController>();
            Assert.That(controller.enabled, Is.True, "An Awake exception must not silently skip the real map bindings.");
            Assert.That(controller.UsageTutorialAvailable, Is.True);

            // A local pending record makes saveCompletion=false observable without starting a
            // game session, registering save participants, creating input, or building a Canvas.
            days = host.AddComponent<DayFlowController>();
            SetDayField("running", true);
            SetDayField("firstDayCafeTutorialCompleted", true);
            SetDayField("mapUsageTutorialPending", true);
            typeof(DayFlowController).GetProperty(nameof(DayFlowController.State)).GetSetMethod(true)
                .Invoke(days, new object[] { DayFlowState.CityMapLeisure });
            Assert.That(days.ShouldShowMapUsageTutorial, Is.True);
            tutorial = map.GetComponent<MapUsageTutorial>() ?? map.AddComponent<MapUsageTutorial>();
            started = settled = 0;
            selectedKey = null;
            selectedPopup = false;
            runtime.Actions.ActionStarted += OnActionStarted;
            runtime.Actions.ActionFinished += OnActionFinished;
            controller.LocationSelected += OnLocationSelected;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (tutorial != null) tutorial.Stop(); // Its authored panel is instantiated outside host.
            if (controller != null) controller.LocationSelected -= OnLocationSelected;
            if (runtime != null && runtime.Actions != null)
            {
                runtime.Actions.ActionStarted -= OnActionStarted;
                runtime.Actions.ActionFinished -= OnActionFinished;
            }
            if (host != null) Object.Destroy(host);
            YokiFrame.UIKit.ClosePanel<GameplayHudPanel>();
            YokiFrame.UIKit.UnloadPanel<GameplayHudPanel>();
            if (runtimeObject != null) Object.Destroy(runtimeObject);
            yield return null;
            if (instance != null) instance.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
            GameplayPanelHooks.OpenPanel = previousOpenPanel;
            GameplayPanelHooks.PanelType = previousPanelType;
            GameplayPanelHooks.IsScenePanel = previousScenePanel;
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (debugScene.IsValid() && debugScene.isLoaded) yield return SceneManager.UnloadSceneAsync(debugScene);
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator MarketSelectionExplainsUnavailableActionsAndReplayLeavesCompletionRecordUnchanged()
        {
            var before = new ResourceSnapshot(runtime, days);
            string recordBefore = days.CaptureJson();
            Assert.That(tutorial.Begin(controller, false), Is.True);
            Assert.That(tutorial.IsRunning, Is.True);
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.Overview));
            before.AssertUnchanged(runtime, days);

            tutorial.Advance();
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.SelectLocation));
            tutorial.Advance();
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.SelectLocation),
                "Next cannot bypass selecting a real authored location.");
            LocationButton("market").onClick.Invoke();
            yield return null;

            Assert.That(selectedKey, Is.EqualTo("market"));
            Assert.That(selectedPopup, Is.False, "The real selection event must report no action popup for market.");
            Assert.That(controller.IsLocationPopupVisible, Is.False);
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.ReadLocation));
            var panel = TutorialPanel();
            StringAssert.Contains("暂无开放", panel.gestureText.text);
            StringAssert.Contains("需求", panel.instruction.text);
            AssertNoActionOrResourceChanges(before);
            Assert.That(days.CaptureJson(), Is.EqualTo(recordBefore));

            tutorial.Advance();
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.Finish));
            StringAssert.Contains("T", panel.instruction.text);
            StringAssert.Contains("B", panel.instruction.text);
            StringAssert.Contains("点击床睡觉", panel.instruction.text);
            Assert.That(tutorial.IsRunning, Is.True, "The final page still needs confirmation.");
            tutorial.Advance();
            Assert.That(tutorial.IsRunning, Is.False);
            Assert.That(days.CaptureJson(), Is.EqualTo(recordBefore), "Begin(map, false) must not record formal completion.");
            Assert.That(days.ShouldShowMapUsageTutorial, Is.True);
            AssertNoActionOrResourceChanges(before);
            yield return null; // Destroy the previous authored panel before replaying.

            Assert.That(MapUsageTutorial.DebugPlay(out string message), Is.True, message);
            Assert.That(tutorial.IsRunning, Is.True, "The GM entry must reuse the strongly typed map tutorial.");
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.Overview));
            tutorial.Advance();
            LocationButton("market").onClick.Invoke();
            yield return null;
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.ReadLocation));
            tutorial.Advance();
            tutorial.Advance();
            Assert.That(tutorial.IsRunning, Is.False);
            Assert.That(days.CaptureJson(), Is.EqualTo(recordBefore), "Completing GM replay must not clear the pending record.");
            Assert.That(days.ShouldShowMapUsageTutorial, Is.True);
            AssertNoActionOrResourceChanges(before);
        }

        [UnityTest]
        public IEnumerator CafeSelectionShowsRealDetailsWithoutExecutingAndAuthoredTextIsNotTruncated()
        {
            var before = new ResourceSnapshot(runtime, days);
            string recordBefore = days.CaptureJson();
            Assert.That(tutorial.Begin(controller, false), Is.True);
            tutorial.Advance();
            LocationButton("cafe").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);
            Canvas.ForceUpdateCanvases();

            Assert.That(selectedKey, Is.EqualTo("cafe"));
            Assert.That(selectedPopup, Is.True, "The authored cafe button must report its real popup.");
            Assert.That(controller.IsLocationPopupVisible, Is.True);
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.ReadLocation));
            var popup = map.GetComponentInChildren<MapActionDetailsView>(true);
            Assert.That(popup.gameObject.activeInHierarchy, Is.True);
            Assert.That(popup.transform.Find("Viewport/ActionList/EnterCafe"), Is.Not.Null);
            var panel = TutorialPanel();
            StringAssert.Contains("需求", panel.instruction.text);
            var gestureGroup = panel.gesture.GetComponent<CanvasGroup>();
            Assert.That(gestureGroup, Is.Not.Null);
            Assert.That(gestureGroup.blocksRaycasts, Is.False, "The short mouse hint must not intercept map clicks.");
            Assert.That(gestureGroup.interactable, Is.False);
            Assert.That(panel.shade.enabled, Is.False, "The tutorial must not cover the map with a blocking shade.");
            foreach (var dot in panel.trail)
                Assert.That(dot.raycastTarget, Is.False, "Decorative trail markers must not block input.");
            AssertNoActionOrResourceChanges(before);
            Assert.That(days.CaptureJson(), Is.EqualTo(recordBefore));

            // Capture the existing prefabs while ReadLocation is still visible, even if the
            // following layout assertions fail. Never resize text or patch product assets here.
            if (!Application.isBatchMode)
            {
                yield return new WaitForEndOfFrame();
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    string path = Path.Combine(Application.dataPath, "../Library/Locus/tmp/map-tutorial.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, screenshot.EncodeToPNG());
                }
                finally { Object.Destroy(screenshot); }
            }
            Canvas.ForceUpdateCanvases();
            AssertTextNotTruncated(panel.instruction, "Tutorial ReadLocation instruction");
            AssertTextNotTruncated(panel.gestureText, "Tutorial short hint");
            foreach (var text in popup.GetComponentsInChildren<TMP_Text>())
                if (!string.IsNullOrWhiteSpace(text.text))
                    AssertTextNotTruncated(text, "Cafe detail / " + text.name);
            Assert.That(tutorial.CurrentStage, Is.EqualTo(MapUsageTutorial.Stage.ReadLocation));
            Assert.That(days.CaptureJson(), Is.EqualTo(recordBefore));
            AssertNoActionOrResourceChanges(before);
        }

        private void SetDayField(string name, object value)
        {
            var field = typeof(DayFlowController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(days, value);
        }

        private Button LocationButton(string key)
        {
            var location = map.transform.Find("MapViewport/MapContent/Entities/Location:" + key);
            Assert.That(location, Is.Not.Null, key);
            var button = location.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True, key);
            return button;
        }

        private static FreeCafeTutorialPanel TutorialPanel()
        {
            FreeCafeTutorialPanel result = null;
            foreach (var candidate in Object.FindObjectsByType<FreeCafeTutorialPanel>(FindObjectsSortMode.None))
            {
                if (candidate.name != "MapUsageTutorialPanel") continue;
                Assert.That(result, Is.Null, "Only one map tutorial panel may be active.");
                result = candidate;
            }
            Assert.That(result, Is.Not.Null, "Begin must instantiate the shared authored tutorial prefab.");
            return result;
        }

        private static void AssertTextNotTruncated(TMP_Text text, string context)
        {
            Assert.That(text, Is.Not.Null, context);
            text.ForceMeshUpdate();
            Assert.That(text.isTextTruncated, Is.False,
                context + " is truncated at " + Screen.width + "x" + Screen.height
                + ". Report the authored layout to the main agent; do not modify product layout in this test.");
            Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2f),
                context + " rendered text exceeds its authored height after auto sizing.");
        }

        private void OnActionStarted(NarrativeActionDefinition action) => started++;
        private void OnActionFinished(NarrativeActionDefinition action, bool success) => settled++;
        private void OnLocationSelected(string key, bool popupVisible)
        {
            selectedKey = key;
            selectedPopup = popupVisible;
        }

        private void AssertNoActionOrResourceChanges(ResourceSnapshot before)
        {
            Assert.That(started, Is.Zero, "Viewing the tutorial must not start any NCS action.");
            Assert.That(settled, Is.Zero, "Viewing the tutorial must not settle any NCS action.");
            Assert.That(runtime.Actions.IsExecuting, Is.False);
            before.AssertUnchanged(runtime, days);
        }

        private sealed class ResourceSnapshot
        {
            private readonly int points, maxPoints, actionDay, day;
            private readonly Dictionary<string, int> inventory, bars;

            public ResourceSnapshot(NarrativeRuntime source, DayFlowController flow)
            {
                points = source.Actions.ActionPoints.Current;
                maxPoints = source.Actions.ActionPoints.Max;
                actionDay = source.Actions.CurrentDay;
                day = flow.CurrentDay;
                inventory = new Dictionary<string, int>();
                foreach (var pair in source.MapInventory.Items) inventory.Add(pair.Key, pair.Value);
                bars = new Dictionary<string, int>();
                foreach (var pair in source.Controller.State.Bars) bars.Add(pair.Key, pair.Value);
            }

            public void AssertUnchanged(NarrativeRuntime source, DayFlowController flow)
            {
                Assert.That(source.Actions.ActionPoints.Current, Is.EqualTo(points), "Viewing must not spend AP.");
                Assert.That(source.Actions.ActionPoints.Max, Is.EqualTo(maxPoints));
                Assert.That(source.Actions.CurrentDay, Is.EqualTo(actionDay), "Viewing must not advance action day.");
                Assert.That(flow.CurrentDay, Is.EqualTo(day), "Viewing must not advance the calendar.");
                CollectionAssert.AreEquivalent(inventory, source.MapInventory.Items, "Viewing must not consume or grant items.");
                CollectionAssert.AreEquivalent(bars, source.Controller.State.Bars, "Viewing must not change narrative resources.");
            }
        }
    }
}
#endif
