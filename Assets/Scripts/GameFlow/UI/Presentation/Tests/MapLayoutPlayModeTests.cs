#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Spotlight.Narrative;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Tests
{
    public sealed class MapLayoutPlayModeTests
    {
        private Scene scene;
        private GameObject map, runtimeObject;
        private NarrativeRuntime previousRuntime;
        private NarrativeActionController actions;
        private EventSystem[] previousSystems;
        private float previousTime;
        private PropertyInfo instance;

        private sealed class Presenter : INarrativePresenter
        {
            public void Play(NarrativeEventRequest request, Action completed) => completed?.Invoke();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTime = Time.timeScale;
            previousSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var system in previousSystems) system.enabled = false;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var debug = root.GetComponentInChildren<PresentationDebugSceneController>();
                if (debug != null && debug.HelpVisible) debug.ToggleHelp();
            }
            previousRuntime = NarrativeRuntime.Instance;
            instance = typeof(NarrativeRuntime).GetProperty("Instance");
            runtimeObject = new GameObject("MapLayoutTestRuntime");
            runtimeObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(runtimeObject, scene);
            var runtime = runtimeObject.AddComponent<NarrativeRuntime>();
            var narrative = new NarrativeController(new NarrativeConfiguration(), new Presenter());
            var inventory = new NarrativeMapInventory();
            inventory.TryAdd("letter", 2);
            var repeat = new NarrativeActionDefinition
            {
                actionKey = "layout_repeat", nameText = "圣西尔万郊区", type = NarrativeActionType.Repeat,
                descText = "城东城门外的工人区。工匠和从外省迁来的居民在此居住。街道连接着繁忙的城区，也通往田野与远处的村庄。",
                requirements = "ActionPoint(1)"
            };
            var progress = new NarrativeActionDefinition
            {
                actionKey = "layout_progress", nameText = "帮助修缮街区", type = NarrativeActionType.Progress,
                descText = "街区的居民正在修缮旧屋。你可以带上信件前去联络工匠，并协助整理需要修复的房屋。\n这是较长的说明，用于确认正文不会挤入右侧需求栏。",
                requirements = "ActionPoint(2);Item(letter,1)", progressTarget = 3
            };
            actions = new NarrativeActionController(narrative, new[] { repeat, progress }, 5, inventory);
            actions.Mount("layout", repeat.actionKey);
            actions.Mount("progress", progress.actionKey);
            typeof(NarrativeRuntime).GetField("controller", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, narrative);
            typeof(NarrativeRuntime).GetField("actionController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, actions);
            typeof(NarrativeRuntime).GetProperty("MapInventory").GetSetMethod(true).Invoke(runtime, new object[] { inventory });
            instance.GetSetMethod(true).Invoke(null, new object[] { runtime });
            map = Object.Instantiate(Resources.Load<GameObject>("Art/UIPrefab/MapPanel"));
            SceneManager.MoveGameObjectToScene(map, scene);
            map.GetComponent<Canvas>().sortingOrder = 700;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTime;
            if (map != null) Object.Destroy(map);
            if (runtimeObject != null) Object.Destroy(runtimeObject);
            yield return null;
            instance?.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var system in previousSystems) if (system != null) system.enabled = true;
        }

        [UnityTest]
        public IEnumerator ReferenceLayoutsAndMutuallyExclusiveDrawers()
        {
            var inventory = map.GetComponentInChildren<MapActionInventoryView>(true);
            var tasks = map.GetComponentInChildren<MapTaskPanel>(true);
            var details = map.GetComponentInChildren<MapActionDetailsView>(true);
            Assert.That(inventory.IsVisible, Is.False);
            Assert.That(tasks.IsVisible, Is.False);
            yield return Capture("map-default");
            details.Show("layout", "圣西尔万郊区");
            yield return null;
            var card = details.transform.Find("Viewport/ActionList/Action_layout_repeat");
            var title = card.Find("Name").GetComponent<TMP_Text>();
            var description = card.Find("Description").GetComponent<TMP_Text>();
            Assert.That(title.font, Is.Not.EqualTo(description.font));
            Assert.That(description.rectTransform.rect.width + description.rectTransform.anchoredPosition.x,
                Is.LessThan(((RectTransform)card.Find("Requirement_0")).anchoredPosition.x));
            yield return Capture("map-action");
            details.Show("progress", "圣西尔万郊区");
            yield return null;
            Assert.That(details.transform.Find("Viewport/ActionList/Action_layout_progress/ProgressTrack"), Is.Not.Null);
            yield return Capture("map-progress");
            details.gameObject.SetActive(false);
            Time.timeScale = 0;
            map.transform.Find("HUD/ArtworkHud/InventoryToggle").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(inventory.IsVisible, Is.True);
            Assert.That(((RectTransform)inventory.transform).anchoredPosition.x, Is.EqualTo(-28).Within(.1f));
            Assert.That(inventory.transform.Find("Viewport/Items/Item_letter"), Is.Not.Null);
            Assert.That(inventory.transform.Find("Viewport/Items/EmptySlot_5"), Is.Not.Null);
            yield return Capture("map-inventory");
            map.transform.Find("HUD/ArtworkHud/TasksToggle").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(inventory.IsVisible, Is.False);
            Assert.That(inventory.gameObject.activeSelf, Is.False);
            Assert.That(tasks.IsVisible, Is.True);
            Assert.That(tasks.transform.Find("TaskTitle").gameObject.activeInHierarchy, Is.True);
            yield return Capture("map-tasks");
        }

        [UnityTest]
        public IEnumerator HiddenInventoryDoesNotFreezeHudAndSliderChangesMapScale()
        {
            actions.ActionPoints.Set(2);
            yield return new WaitForSecondsRealtime(.2f);
            var inventory = map.GetComponentInChildren<MapActionInventoryView>(true);
            Assert.That(inventory.PointsSegments[2].fillAmount, Is.EqualTo(0).Within(.00001f));
            Assert.That(inventory.PointsSegments[1].fillAmount, Is.EqualTo(1).Within(.00001f));
            var slider = map.GetComponentInChildren<Slider>(true);
            Assert.That(slider.handleRect.rect.height, Is.LessThanOrEqualTo(((RectTransform)slider.transform).rect.height));
            slider.value = slider.maxValue;
            yield return new WaitForSecondsRealtime(1);
            Assert.That(map.transform.Find("MapViewport/MapContent").localScale.x, Is.EqualTo(slider.maxValue).Within(.01f));
            Assert.That(map.transform.Find("HUD/ArtworkHud").localScale.x, Is.EqualTo(1).Within(.01f));
        }

        [UnityTest]
        public IEnumerator SelectingLocationPreservesReferenceMapFraming()
        {
            var content = map.transform.Find("MapViewport/MapContent");
            var scale = content.localScale;
            var position = ((RectTransform)content).anchoredPosition;
            content.Find("Entities/Location:圣西尔万郊区").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(content.localScale, Is.EqualTo(scale));
            Assert.That(((RectTransform)content).anchoredPosition, Is.EqualTo(position));
        }

        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "outputs", "map-layout"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
#endif
