#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
    public sealed class SaveSlotViewPrefabTests
    {
        [Test]
        public void EveryActionHasInvisibleRaycastableFeedbackAndContentUsesTwoColumns()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Art/UIPrefab/SaveManagementPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            SaveSlotView[] slots = prefab.GetComponentsInChildren<SaveSlotView>(true);
            Assert.That(slots.Length, Is.EqualTo(6));
            foreach (SaveSlotView slot in slots)
            {
                var fields = new SerializedObject(slot);
                foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                {
                    var button = (Button)fields.FindProperty(field).objectReferenceValue;
                    Assert.That(button, Is.Not.Null);
                    Assert.That(button.GetComponent<SaveSlotButtonFeedback>(), Is.Not.Null);
                    CanvasGroup group = button.GetComponent<CanvasGroup>();
                    Assert.That(group, Is.Not.Null);
                    Assert.That(group.alpha, Is.Zero);
                    Assert.That(group.interactable, Is.True);
                    Assert.That(group.blocksRaycasts, Is.True);
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                }
            }
            GridLayoutGroup grid = prefab.GetComponentInChildren<GridLayoutGroup>(true);
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(2));
            Assert.That(grid.GetComponent<ContentSizeFitter>().verticalFit,
                Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
        }
    }

    public sealed class SaveSlotViewPlayModeTests
    {
        private const string TestTag = "save-slot-view-tests";
        private Scene scene;
        private EventSystem eventSystem;
        private EventSystem[] previousSystems;
        private SaveManagementPanel panel;
        private SaveSlotView[] slots;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (EventSystem system in previousSystems) system.enabled = false;
            scene = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<PresentationDebugSceneController>();
                if (controller != null) controller.ToggleHelp();
                var found = root.GetComponentInChildren<EventSystem>();
                if (found != null) eventSystem = found;
            }
            Assert.That(eventSystem, Is.Not.Null);
            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>()) module.enabled = false;
            // No bootstrap or save backend: snapshots and callbacks are test-only.
            panel = UIKit.OpenPanel<SaveManagementPanel>(UILevel.Common, tag: TestTag,
                cachePolicy: PanelCachePolicy.Transient);
            yield return new WaitForSecondsRealtime(.35f);
            slots = panel.GetComponentsInChildren<SaveSlotView>(true);
            var bound = (SaveSlotView[])typeof(SaveManagementPanel)
                .GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            Assert.That(bound.Length, Is.EqualTo(slots.Length));
            Assert.That(bound, Has.None.Null);
            Array.Sort(slots, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            for (int i = 0; i < slots.Length; i++) slots[i].Refresh(Snapshot(i), true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIKit.ClosePanelsByTag(TestTag);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (previousSystems != null)
                foreach (EventSystem system in previousSystems) if (system != null) system.enabled = true;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator HoverAndPressedAlphaWorkWhileTimeIsPausedAndHiddenButtonsRemainRaycastable()
        {
            SaveSlotView slot = slots[1];
            slot.SetSelected(true);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Time.timeScale = 0f;
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
            {
                Button button = ButtonField(slot, field);
                CanvasGroup group = button.GetComponent<CanvasGroup>();
                Assert.That(group.alpha, Is.Zero);
                Assert.That(button.targetGraphic.canvasRenderer.cullTransparentMesh, Is.False);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(pointer, hits);
                Assert.That(hits.Exists(hit => hit.gameObject == button.gameObject), Is.True,
                    "Alpha zero must not remove the button's hover hit area: " + field);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                Assert.That(group.alpha, Is.EqualTo(1f));
                if (field == "loadButton") { yield return new WaitForEndOfFrame(); Capture("slot-hover"); }
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.09f);
                Assert.That(group.alpha, Is.EqualTo(.5f).Within(.01f));
                if (field == "loadButton") { yield return new WaitForEndOfFrame(); Capture("slot-pressed"); }
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                yield return new WaitForSecondsRealtime(.16f);
                Assert.That(group.alpha, Is.EqualTo(1f).Within(.01f));
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                Assert.That(group.alpha, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator PointerExitAndDeselectCancelTheFadeWithoutResurrectingOpacity()
        {
            SaveSlotView slot = slots[1];
            slot.SetSelected(true);
            Button button = ButtonField(slot, "saveButton");
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.02f);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(group.alpha, Is.Zero);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(group.alpha, Is.Zero);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            slot.SetSelected(false);
            yield return new WaitForSecondsRealtime(.2f);
            slot.SetSelected(true);
            Assert.That(group.alpha, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AutoSaveAndUnavailableOperationsStayDisabledAndDoNotHighlight()
        {
            SaveSlotView auto = slots[0];
            auto.SetSelected(true);
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            Assert.That(ButtonField(auto, "loadButton").interactable, Is.True);
            foreach (string field in new[] { "saveButton", "deleteButton" })
            {
                Button button = ButtonField(auto, field);
                Assert.That(button.interactable, Is.False);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(button.GetComponent<CanvasGroup>().alpha, Is.Zero);
            }
            SaveSlotView manual = slots[1];
            manual.SetSelected(true);
            Button load = ButtonField(manual, "loadButton");
            ExecuteEvents.Execute(load.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(load.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            manual.Refresh(Snapshot(1, GameSaveSlotState.Empty), false);
            yield return new WaitForSecondsRealtime(.2f);
            foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
            {
                Button button = ButtonField(manual, field);
                Assert.That(button.interactable, Is.False);
                Assert.That(button.GetComponent<CanvasGroup>().alpha, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator RebindingOnlyInvokesTheLatestCallbacksOnceAndUsesTheCurrentIndex()
        {
            SaveSlotView slot = slots[1];
            int previousCalls = 0, loads = 0, saves = 0, deletes = 0, selections = 0, lastIndex = -1;
            slot.Bind(_ => previousCalls++, _ => previousCalls++, _ => previousCalls++);
            slot.Bind(index => { loads++; lastIndex = index; }, index => { saves++; lastIndex = index; },
                index => { deletes++; lastIndex = index; });
            slot.BindSelection(_ => previousCalls++);
            slot.BindSelection(_ => selections++);
            slot.Refresh(Snapshot(3), true);
            slot.SetSelected(true);
            ButtonField(slot, "selectButton").onClick.Invoke();
            ButtonField(slot, "loadButton").onClick.Invoke();
            ButtonField(slot, "saveButton").onClick.Invoke();
            ButtonField(slot, "deleteButton").onClick.Invoke();
            Assert.That(previousCalls, Is.Zero);
            Assert.That(loads, Is.EqualTo(1)); Assert.That(saves, Is.EqualTo(1));
            Assert.That(deletes, Is.EqualTo(1)); Assert.That(selections, Is.EqualTo(1));
            Assert.That(lastIndex, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GridPreservesAllSixPsdCardCentersAndContainsTheActionHitAreas()
        {
            GridLayoutGroup grid = panel.GetComponentInChildren<GridLayoutGroup>(true);
            RectTransform content = (RectTransform)grid.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            Assert.That(content.rect.height, Is.EqualTo(788f).Within(.01f));
            Transform artwork = panel.transform.Find("Artwork");
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].SetSelected(true);
                var fields = new SerializedObject(slots[i]);
                var frame = (GameObject)fields.FindProperty("selectionFrame").objectReferenceValue;
                Vector3 center = artwork.InverseTransformPoint(frame.transform.position);
                Assert.That(center.x, Is.EqualTo(i % 2 == 0 ? -412f : 412f).Within(.02f));
                Assert.That(center.y, Is.EqualTo(196.5f - (i / 2) * 266.25f).Within(.02f));
                var row = (RectTransform)slots[i].transform;
                foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                {
                    var corners = new Vector3[4];
                    ((RectTransform)ButtonField(slots[i], field).transform).GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector3 point = row.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(-.02f, row.rect.width + .02f));
                        Assert.That(point.y, Is.InRange(-row.rect.height - .02f, .02f));
                    }
                }
                slots[i].SetSelected(false);
            }
            yield return new WaitForEndOfFrame();
            Capture("slot-grid");
        }

        private static Button ButtonField(SaveSlotView slot, string name) =>
            (Button)new SerializedObject(slot).FindProperty(name).objectReferenceValue;

        private static GameSaveSlotInfo Snapshot(int index, GameSaveSlotState state = GameSaveSlotState.Occupied) =>
            (GameSaveSlotInfo)Activator.CreateInstance(typeof(GameSaveSlotInfo),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { index, state, null, (DateTimeOffset?)DateTimeOffset.UtcNow, "CoffeeShop", 120d }, null);

        private static void Capture(string name)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null) throw new InvalidOperationException("Game view capture unavailable.");
            try
            {
                string directory = Path.GetFullPath("output/save-slot-view");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally { Object.Destroy(image); }
        }
    }
}
#endif
