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
            SceneManager.SetActiveScene(scene);
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
            YokiFrame.UIKit.ClosePanel<GameplayHudPanel>();
            YokiFrame.UIKit.UnloadPanel<GameplayHudPanel>();
            yield return null;
            map = Object.Instantiate(Resources.Load<GameObject>("Art/UIPrefab/MapPanel"));
            SceneManager.MoveGameObjectToScene(map, scene);
            map.GetComponent<Canvas>().sortingOrder = 500;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTime;
            if (map != null) Object.Destroy(map);
            YokiFrame.UIKit.ClosePanel<GameplayHudPanel>();
            YokiFrame.UIKit.UnloadPanel<GameplayHudPanel>();
            if (runtimeObject != null) Object.Destroy(runtimeObject);
            yield return null;
            instance?.GetSetMethod(true).Invoke(null, new object[] { previousRuntime });
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var system in previousSystems) if (system != null) system.enabled = true;
        }

        [UnityTest]
        public IEnumerator ReferenceLayoutsAndMutuallyExclusiveDrawers()
        {
            var inventory = GameplayHudPanel.Instance.Inventory;
            var tasks = GameplayHudPanel.Instance.Tasks;
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
            var requirement = (RectTransform)(card.Find("Requirements/Requirement") ?? card.Find("Requirement"));
            Assert.That(requirement, Is.Not.Null);
            var descriptionCorners = new Vector3[4];
            var requirementCorners = new Vector3[4];
            description.rectTransform.GetWorldCorners(descriptionCorners);
            requirement.GetWorldCorners(requirementCorners);
            Assert.That(descriptionCorners[2].x <= requirementCorners[0].x
                || descriptionCorners[0].x >= requirementCorners[2].x
                || descriptionCorners[0].y >= requirementCorners[2].y
                || descriptionCorners[2].y <= requirementCorners[0].y, Is.True,
                "Authored description and requirement areas must not overlap.");
            yield return Capture("map-action");
            details.Show("progress", "圣西尔万郊区");
            yield return null;
            var progressCard = details.transform.Find("Viewport/ActionList/Action_layout_progress");
            Assert.That(progressCard, Is.Not.Null);
            Assert.That(progressCard.Find("Name").GetComponent<TMP_Text>().text, Is.EqualTo("layout_progress"));
            var progressRequirement = progressCard.Find("Requirements/Requirement") ?? progressCard.Find("Requirement");
            Assert.That(progressRequirement.GetComponentInChildren<TMP_Text>().text, Does.Contain("2"));
            yield return Capture("map-progress");
            details.gameObject.SetActive(false);
            Time.timeScale = 0;
            GameplayHudPanel.Instance.transform.Find("ArtworkHud/InventoryToggle").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(inventory.IsVisible, Is.True);
            Assert.That(((RectTransform)inventory.transform).anchoredPosition.x, Is.EqualTo(-28).Within(.1f));
            Assert.That(inventory.transform.Find("Viewport/Items/Item_letter"), Is.Not.Null);
            Assert.That(inventory.transform.Find("Viewport/Items/EmptySlot_5"), Is.Not.Null);
            yield return Capture("map-inventory");
            GameplayHudPanel.Instance.transform.Find("ArtworkHud/TasksToggle").GetComponent<Button>().onClick.Invoke();
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
            var inventory = GameplayHudPanel.Instance.Inventory;
            Assert.That(inventory.PointsSegments[2].fillAmount, Is.EqualTo(0).Within(.00001f));
            Assert.That(inventory.PointsSegments[inventory.PointsSegments.Length - 1].fillAmount, Is.EqualTo(1).Within(.00001f));
            var slider = map.GetComponentInChildren<Slider>(true);
            Assert.That(slider.handleRect.rect.height, Is.LessThanOrEqualTo(((RectTransform)slider.transform).rect.height));
            slider.value = slider.maxValue;
            yield return new WaitForSecondsRealtime(1);
            Assert.That(map.transform.Find("MapViewport/MapContent").localScale.x, Is.EqualTo(slider.maxValue).Within(.01f));
            Assert.That(GameplayHudPanel.Instance.Artwork.localScale.x, Is.EqualTo(1).Within(.01f));
        }

        [UnityTest]
        public IEnumerator SelectingLocationUsesConfiguredFocusZoom()
        {
            var content = map.transform.Find("MapViewport/MapContent");
            var feel = Resources.Load<MapFeelConfig>("MapFeelConfig");
            content.Find("Entities/Location:圣西尔万郊区").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(content.localScale.x, Is.EqualTo(feel.focusZoom).Within(.02f));
            Assert.That(content.localScale.y, Is.EqualTo(content.localScale.x));
        }

        [UnityTest]
        public IEnumerator TaskVisibilityPreservesBuiltRowsAndScrollPosition()
        {
            var tasks = GameplayHudPanel.Instance.Tasks;
            var content = tasks.transform.Find("TaskBody/TaskViewport/TaskContent");
            var scroll = tasks.transform.Find("TaskBody/TaskViewport").GetComponent<ScrollRect>();
            var rows = new Transform[content.childCount];
            for (int i = 0; i < rows.Length; i++) rows[i] = content.GetChild(i);
            Assert.That(rows.Length, Is.GreaterThan(0));
            scroll.StopMovement();
            ((RectTransform)content).anchoredPosition = new Vector2(0, 37);
            var position = ((RectTransform)content).anchoredPosition;

            // Include redundant calls, completed slides, Toggle, and reversing an in-flight slide.
            tasks.SetVisible(false);
            tasks.SetVisible(true);
            tasks.SetVisible(true);
            AssertTaskRowsUnchanged(content, rows);
            Assert.That(((RectTransform)content).anchoredPosition, Is.EqualTo(position));
            yield return new WaitForSecondsRealtime(.3f);
            AssertTaskRowsUnchanged(content, rows);
            tasks.SetVisible(false);
            AssertTaskRowsUnchanged(content, rows);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(tasks.gameObject.activeSelf, Is.False);
            AssertTaskRowsUnchanged(content, rows);
            tasks.SetVisible(false);
            tasks.Toggle();
            tasks.Toggle();
            tasks.Toggle();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(tasks.IsVisible, Is.True);
            AssertTaskRowsUnchanged(content, rows);
        }

        [UnityTest]
        public IEnumerator HiddenTaskChangesRefreshBeforeReopening()
        {
            var tasks = GameplayHudPanel.Instance.Tasks;
            var content = tasks.transform.Find("TaskBody/TaskViewport/TaskContent");
            Assert.That(tasks.IsVisible, Is.False);
            var item = new MapTaskItem { id = "task_update", name = "Original", description = "Before" };
            tasks.SetItems(new[] { item });
            yield return null;
            var original = content.Find("Task_task_update");
            Assert.That(original, Is.Not.Null);
            item.name = "Updated";
            item.description = "After";
            tasks.SetItems(new[] { item });
            yield return null;
            var updated = content.Find("Task_task_update");
            Assert.That(original == null, Is.True, "SetItems must still rebuild changed content.");
            Assert.That(updated.Find("Description").GetComponent<TMP_Text>().text, Is.EqualTo("After"));
            string changedId = null;
            tasks.TrackingChanged += id => changedId = id;
            tasks.SetTracked(item.id);
            yield return null;
            var tracked = content.Find("Task_task_update");
            Assert.That(tasks.TrackedId, Is.EqualTo(item.id));
            Assert.That(changedId, Is.EqualTo(item.id));
            Assert.That(tracked.Find("Label").GetComponent<TMP_Text>().text, Does.Contain("Updated").And.Contain("追踪中"));
            tasks.SetVisible(true);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(content.Find("Task_task_update"), Is.SameAs(tracked));
        }

        [UnityTest]
        public IEnumerator TaskTabsAndExpansionStillRefreshContent()
        {
            var tasks = GameplayHudPanel.Instance.Tasks;
            tasks.SetItems(new[]
            {
                new MapTaskItem { id = "active", name = "Active", currentTask = "Current", previousTasks = new List<string> { "Previous" }, navigationLabel = "查看地点与行动" },
                new MapTaskItem { id = "done", name = "Done", completed = true }
            });
            yield return null;
            tasks.SetVisible(true);
            var body = tasks.transform.Find("TaskBody");
            var content = body.Find("TaskViewport/TaskContent");
            content.Find("Task_active").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(content.Find("PreviousTask").GetComponent<TMP_Text>().text, Does.Contain("Previous"));
            Assert.That(content.Find("CurrentTask").GetComponent<TMP_Text>().text, Does.Contain("Current"));
            Assert.That(content.Find("Track_active"), Is.Not.Null);
            foreach (string node in new[] { "Navigate_active", "Track_active" })
            {
                var button = content.Find(node).GetComponent<Button>();
                var image = button.GetComponent<Image>();
                var label = button.GetComponentInChildren<TMP_Text>(true);
                Assert.That(image.sprite, Is.EqualTo(MapVisualTheme.Current.taskCard), "Expanded actions reuse paper artwork.");
                Assert.That(image.color, Is.EqualTo((Color)new Color32(224, 176, 107, 255)));
                Assert.That(label.color, Is.EqualTo(MapVisualTheme.Ink), "Never inherit white-on-white template text.");
                Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
                Assert.That(button.colors.pressedColor, Is.Not.EqualTo(button.colors.normalColor));
            }
            tasks.SetVisible(false);
            tasks.SetVisible(true);
            Assert.That(content.Find("CurrentTask"), Is.Not.Null, "Visibility must preserve expansion.");
            content.Find("Task_active").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(content.Find("CurrentTask"), Is.Null);
            body.Find("CompletedTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(content.Find("Task_active"), Is.Null);
            Assert.That(content.Find("Task_done"), Is.Not.Null);
            tasks.SetVisible(false);
            tasks.SetVisible(true);
            Assert.That(content.Find("Task_done"), Is.Not.Null, "Visibility must preserve the selected tab.");
            content.Find("Task_done").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(content.Find("CompletedStatus"), Is.Not.Null);
            body.Find("InProgressTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(content.Find("Task_active"), Is.Not.Null);
            Assert.That(content.Find("Task_done"), Is.Null);
            Assert.That(content.Find("CurrentTask"), Is.Null, "Switching tabs must reset expansion.");
            tasks.SetItems(null);
            yield return null;
            Assert.That(content.Find("Empty"), Is.Not.Null);
        }

        private static void AssertTaskRowsUnchanged(Transform content, Transform[] rows)
        {
            Assert.That(content.childCount, Is.EqualTo(rows.Length));
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.That(rows[i] != null, Is.True, "Visibility must not destroy a built row.");
                Assert.That(content.GetChild(i), Is.SameAs(rows[i]));
                if (rows[i].name != "Task_map_example_active_0" && !rows[i].name.StartsWith("Preview"))
                    Assert.That(rows[i].gameObject.activeSelf, Is.True);
            }
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
