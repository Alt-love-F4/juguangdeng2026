#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>Exercise the real authored MapPanel, including nested widget prefabs.</summary>
    public sealed class MapInitializationPlayModeTests
    {
        private GameObject host, map, runtimeObject;
        private MapSceneController controller;
        private NarrativeRuntime previousRuntime;
        private Func<GameFlowBootstrap, GameFlowPanel> previousOpenPanel;
        private Type previousPanelType;
        private bool previousScenePanel;
        private PropertyInfo instance;
        private float previousTimeScale;

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
            previousRuntime = NarrativeRuntime.Instance;
            instance = typeof(NarrativeRuntime).GetProperty(nameof(NarrativeRuntime.Instance));

            runtimeObject = new GameObject("MapInitializationTestRuntime");
            runtimeObject.SetActive(false); // Keep real session services and presentation untouched.
            var runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            runtime.Initialize(LubanNarrativeLoader.Load(), new ImmediatePresenter());
            instance.GetSetMethod(true).Invoke(null, new object[] { runtime });

            host = new GameObject("MapInitializationTestHost");
            host.SetActive(false);
            var prefab = Resources.Load<GameObject>("Art/UIPrefab/MapPanel");
            Assert.That(prefab, Is.Not.Null);
            map = Object.Instantiate(prefab, host.transform, false);
            map.GetComponent<Canvas>().sortingOrder = 700;
            // Activate only after creating all nested widgets, exactly as a scene load does.
            map.SetActive(true);
            host.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            controller = map.GetComponent<MapSceneController>();
            Assert.That(controller.enabled, Is.True, "An Awake exception disables the map and skips every input binding.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (runtimeObject != null) Object.Destroy(runtimeObject);
            yield return null;
            if (instance != null) instance.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
            GameplayPanelHooks.OpenPanel = previousOpenPanel;
            GameplayPanelHooks.PanelType = previousPanelType;
            GameplayPanelHooks.IsScenePanel = previousScenePanel;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator AuthoredPopupWithoutCloseDoesNotInterruptZoomOrSidebarBindings()
        {
            var inventory = map.GetComponentInChildren<MapActionInventoryView>(true);
            var tasks = map.GetComponentInChildren<MapTaskPanel>(true);
            var popup = map.GetComponentInChildren<MapActionDetailsView>(true);
            Assert.That(popup.gameObject.activeSelf, Is.False);
            Assert.That(inventory.IsVisible, Is.False);
            Assert.That(tasks.IsVisible, Is.False);

            map.transform.Find("HUD/ArtworkHud/InventoryToggle").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(inventory.IsVisible, Is.True);
            Assert.That(inventory.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);

            map.transform.Find("HUD/ArtworkHud/TasksToggle").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(tasks.IsVisible, Is.True);
            Assert.That(inventory.IsVisible, Is.False);
            Assert.That(inventory.gameObject.activeSelf, Is.False);

            var slider = map.transform.Find("HUD/ZoomControl").GetComponent<Slider>();
            var content = map.transform.Find("MapViewport/MapContent");
            slider.value = slider.maxValue;
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(content.localScale.x, Is.EqualTo(slider.maxValue).Within(.01f));
            slider.value = slider.minValue;
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(content.localScale.x, Is.EqualTo(slider.minValue).Within(.01f));
        }

        [UnityTest]
        public IEnumerator LocationSelectionAndDismissalUseTheDiscoveredNestedPopup()
        {
            var popup = map.GetComponentInChildren<MapActionDetailsView>(true);
            // The existing prefab leaves the legacy focusPanel field empty; Awake must resolve it.
            var focusPanel = typeof(MapSceneController).GetField("focusPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(focusPanel.GetValue(controller), Is.SameAs(popup.gameObject));
            map.transform.Find("MapViewport/MapContent/Entities/Location:market").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(popup.gameObject.activeInHierarchy, Is.True);
            Assert.That(popup.transform.Find("Viewport/ActionList/Action_act_meet_b"), Is.Not.Null,
                "Configured locations must be bound after popup initialization.");

            // This is the common dismissal path used by right-click, Escape and map overlays.
            typeof(MapSceneController).GetMethod("HideFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(popup.gameObject.activeSelf, Is.False);
            map.transform.Find("MapViewport/MapContent/Entities/Location:market").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(popup.gameObject.activeInHierarchy, Is.True);
            Assert.That(popup.transform.Find("Viewport/ActionList/Action_act_meet_b"), Is.Not.Null);
        }
    }
}
#endif
