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
        public void EveryActionOwnsItsArtworkAndContentUsesTwoColumns()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Art/UIPrefab/SaveManagementPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            SaveSlotView[] slots = prefab.GetComponentsInChildren<SaveSlotView>(true);
            Assert.That(slots.Length, Is.EqualTo(6));
            foreach (SaveSlotView slot in slots)
            {
                var fields = new SerializedObject(slot);
                foreach (Transform child in slot.GetComponentsInChildren<Transform>(true))
                    Assert.That(child.name, Is.Not.EqualTo("Actions底图"), "Composite artwork must be removed.");
                GameObject actions = (GameObject)fields.FindProperty("actionsRoot").objectReferenceValue;
                Assert.That(actions.activeSelf, Is.False);
                Assert.That(actions.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                {
                    var button = (Button)fields.FindProperty(field).objectReferenceValue;
                    Assert.That(button, Is.Not.Null);
                    Assert.That(button.GetComponent<SaveSlotButtonFeedback>(), Is.Not.Null);
                    CanvasGroup group = button.GetComponent<CanvasGroup>();
                    Assert.That(group, Is.Null, "Action visibility must not depend on an invisible CanvasGroup.");
                    Assert.That(button.transform.Find("Background"), Is.Not.Null);
                    Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                    Assert.That(button.GetComponent<Image>().canvasRenderer.cullTransparentMesh, Is.False);
                    foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
                        if (graphic.transform != button.transform) Assert.That(graphic.raycastTarget, Is.False);
                    Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
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
        private GameObject testFlowRoot;

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
            if (testFlowRoot != null) Object.Destroy(testFlowRoot);
            if (testFlowRoot != null) SaveKit.Reset();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (previousSystems != null)
                foreach (EventSystem system in previousSystems) if (system != null) system.enabled = true;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator FirstOpenFromRealSnapshotsThenCardClickRevealsLightActions()
        {
            // Do not refresh rows after opening: production supplies snapshots in
            // OnOpen, before UIKit activates the panel and Unity invokes Awake.
            UIKit.ClosePanelsByTag(TestTag);
            yield return null;
            testFlowRoot = new GameObject("SaveSlotTestFlow");
            testFlowRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(testFlowRoot, scene);
            GameFlowBootstrap flow = testFlowRoot.AddComponent<GameFlowBootstrap>();
            var service = new GameSaveService(new MemorySaveStorage());
            Assert.That(service.TrySaveManual(1, new GameSaveData { SceneName = "CoffeeShop", State = "playing" }, out string error), Is.True, error);
            typeof(GameFlowBootstrap).GetField("saves", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, service);
            SaveManagementPanel cachedPanel = null;
            bool[] contexts = { true, false, true };
            for (int round = 0; round < contexts.Length; round++)
            {
                bool gameplay = contexts[round];
                // First open plus reopening the same reusable instance in both
                // entry contexts; final close releases it for subsequent tests.
                panel = UIKit.OpenPanel<SaveManagementPanel>(UILevel.Pop, new GameFlowPanelData(flow, gameplay),
                    tag: TestTag, cachePolicy: round == contexts.Length - 1 ? PanelCachePolicy.Transient : PanelCachePolicy.Reusable);
                if (cachedPanel != null) Assert.That(panel, Is.SameAs(cachedPanel));
                cachedPanel = panel;
                yield return new WaitForSecondsRealtime(.35f);
                slots = panel.GetComponentsInChildren<SaveSlotView>(true);
                Array.Sort(slots, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                ClickCard(slots[1]);
                yield return null;
                AssertAction(slots[1], "loadButton", true);
                AssertAction(slots[1], "saveButton", gameplay);
                AssertAction(slots[1], "deleteButton", true);
                Image icon = ButtonField(slots[1], "loadButton").transform.Find("Background").GetComponent<Image>();
                Assert.That(icon.color, Is.EqualTo(Color.white), "Newly revealed actions must start light, not focused/pressed.");
                yield return new WaitForEndOfFrame();
                Capture(gameplay ? "card-click-gameplay" : "card-click-title");
                ClickCard(slots[2]);
                yield return null;
                AssertAction(slots[2], "saveButton", gameplay);
                Assert.That(ButtonField(slots[1], "loadButton").IsActive(), Is.False);
                UIKit.ClosePanelsByTag(TestTag);
                yield return null;
            }
        }

        private void ClickCard(SaveSlotView slot)
        {
            Button button = ButtonField(slot, "selectButton");
            RectTransform rect = (RectTransform)button.transform;
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "The save card must have a raycast hit.");
            GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(target, Is.EqualTo(button.gameObject), "Actual topmost hit must select this card.");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator SelectedActionsStayVisibleAndPointerDarkensAllChildrenWhilePaused()
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
                Image background = button.transform.Find("Background").GetComponent<Image>();
                TMPro.TMP_Text label = button.GetComponentInChildren<TMPro.TMP_Text>();
                Color normal = background.color, textNormal = label.color;
                Assert.That(button.IsActive() && button.IsInteractable(), Is.True);
                Assert.That(normal.a, Is.EqualTo(1f));
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                var results = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(pointer, results);
                Assert.That(results.Exists(result => result.gameObject == button.gameObject), Is.True);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.15f);
                AssertCoffee(background.color, false, normal.a);
                AssertCoffee(label.color, false, textNormal.a);
                if (field == "loadButton") { yield return new WaitForEndOfFrame(); Capture("slot-hover"); }
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(.15f);
                AssertCoffee(background.color, true, normal.a);
                AssertCoffee(label.color, true, textNormal.a);
                Assert.That(background.transform.localScale.x, Is.EqualTo(.94f).Within(.001f));
                Assert.That(button.transform.localScale.x, Is.EqualTo(1f));
                if (field == "loadButton") { yield return new WaitForEndOfFrame(); Capture("slot-pressed"); }
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                yield return new WaitForSecondsRealtime(.15f);
                AssertCoffee(background.color, false, normal.a);
                Assert.That(background.transform.localScale.x, Is.EqualTo(1f).Within(.001f));
                eventSystem.SetSelectedGameObject(null);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(background.color, Is.EqualTo(normal));
            }
        }

        private static void AssertCoffee(Color actual, bool pressed, float alpha = 1f)
        {
            Color expected = pressed ? new Color(.28f, .14f, .065f, alpha) : new Color(.42f, .24f, .12f, alpha);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01f));
            Assert.That(actual.a, Is.EqualTo(alpha).Within(.001f));
        }

        [UnityTest]
        public IEnumerator FocusStaysCoffeeOnPointerExitAndSubmitWorksWhilePaused()
        {
            SaveSlotView slot = slots[1];
            slot.SetSelected(true);
            Button button = ButtonField(slot, "loadButton");
            Image icon = button.transform.Find("Background").GetComponent<Image>();
            Color normal = icon.color;
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            Time.timeScale = 0f;
            eventSystem.SetSelectedGameObject(button.gameObject);
            yield return new WaitForSecondsRealtime(.15f);
            AssertCoffee(icon.color, false);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.15f);
            AssertCoffee(icon.color, false);
            yield return new WaitForEndOfFrame();
            Capture("slot-focused");
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            yield return new WaitForSecondsRealtime(.1f);
            AssertCoffee(icon.color, true);
            yield return new WaitForSecondsRealtime(.2f);
            AssertCoffee(icon.color, false);
            eventSystem.SetSelectedGameObject(null);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(icon.color, Is.EqualTo(normal));
        }

        [UnityTest]
        public IEnumerator IconLabelAndTransparentPaddingAllClickTheSameActionAndClippingStillWorks()
        {
            SaveSlotView slot = slots[1];
            slot.SetSelected(true);
            Button button = ButtonField(slot, "loadButton");
            RectTransform rect = (RectTransform)button.transform;
            Image icon = button.transform.Find("Background").GetComponent<Image>();
            TMPro.TMP_Text label = button.GetComponentInChildren<TMPro.TMP_Text>();
            int calls = 0, lastIndex = -1;
            slot.Bind(index => { calls++; lastIndex = index; }, null, null);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Vector3[] positions = { icon.transform.position, label.transform.position,
                rect.TransformPoint(new Vector3(rect.rect.xMax - 2f, rect.rect.yMax - 2f)) };
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            foreach (Vector3 position in positions)
            {
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, position);
                hits.Clear();
                eventSystem.RaycastAll(pointer, hits);
                Assert.That(hits.Count, Is.GreaterThan(0));
                GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(target, Is.EqualTo(button.gameObject), "Topmost raycast must resolve to the action, not the card.");
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
            }
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(lastIndex, Is.EqualTo(1));
            RectTransform actions = (RectTransform)button.transform.parent;
            Vector2 previous = actions.anchoredPosition;
            actions.anchoredPosition = previous + new Vector2(0f, 4000f);
            Canvas.ForceUpdateCanvases();
            yield return null;
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            hits.Clear();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == button.gameObject), Is.False);
            actions.anchoredPosition = previous;
        }

        [UnityTest]
        public IEnumerator DragOutsideRestoresScaleButKeepsFocusAndDisablingCancelsPress()
        {
            SaveSlotView slot = slots[1];
            slot.SetSelected(true);
            Button button = ButtonField(slot, "saveButton");
            Image icon = button.transform.Find("Background").GetComponent<Image>();
            Color normal = icon.color;
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            eventSystem.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.15f);
            AssertCoffee(icon.color, true);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.15f);
            AssertCoffee(icon.color, false);
            Assert.That(icon.transform.localScale.x, Is.EqualTo(1f).Within(.001f));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.15f);
            AssertCoffee(icon.color, true);
            slot.Refresh(Snapshot(1), false);
            Assert.That(button.IsActive(), Is.False);
            Assert.That(icon.color, Is.EqualTo(normal));
            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.EqualTo(button.gameObject));
            slot.Refresh(Snapshot(1), true);
            Assert.That(icon.color, Is.EqualTo(normal));
            Assert.That(icon.transform.localScale.x, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator FocusNavigationSkipsUnavailableActionsAndRightClickDoesNotPress()
        {
            SaveSlotView slot = slots[1];
            slot.Refresh(Snapshot(1), false);
            slot.SetSelected(true);
            Button load = ButtonField(slot, "loadButton"), delete = ButtonField(slot, "deleteButton");
            Assert.That(load.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(load.FindSelectableOnDown(), Is.EqualTo(delete));
            Assert.That(delete.FindSelectableOnUp(), Is.EqualTo(load));
            Image icon = load.transform.Find("Background").GetComponent<Image>();
            Color normal = icon.color;
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(load.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(load.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(icon.color, Is.EqualTo(normal));
            Assert.That(icon.transform.localScale.x, Is.EqualTo(1f));
            slot.Refresh(Snapshot(1, GameSaveSlotState.Empty), true);
            Button save = ButtonField(slot, "saveButton");
            Assert.That(save.FindSelectableOnUp(), Is.Null);
            Assert.That(save.FindSelectableOnDown(), Is.Null);
        }

        [UnityTest]
        public IEnumerator RefreshWithoutSaveBackendClearsPreviouslyAvailableActions()
        {
            slots[1].SetSelected(true);
            Assert.That(ButtonField(slots[1], "loadButton").IsActive(), Is.True);
            // The test deliberately opens the panel without a DayFlow/save backend.
            panel.RefreshSlots();
            foreach (SaveSlotView slot in slots)
            foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                Assert.That(ButtonField(slot, field).IsActive(), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeselectCancelsFeedbackAndReselectStartsAtNormalColor()
        {
            SaveSlotView slot = slots[1];
            slot.SetSelected(true);
            Button button = ButtonField(slot, "saveButton");
            Image background = button.transform.Find("Background").GetComponent<Image>();
            Color normal = background.color;
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.02f);
            slot.SetSelected(false);
            Assert.That(button.IsActive(), Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            slot.SetSelected(true);
            Assert.That(background.color, Is.EqualTo(normal));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(background.color, Is.EqualTo(normal));
        }

        [UnityTest]
        public IEnumerator AvailabilityMatchesEverySlotStateAndEntryContext()
        {
            SaveSlotView slot = slots[1];
            foreach (bool gameplay in new[] { false, true })
            foreach (int index in new[] { 0, 1 })
            foreach (GameSaveSlotState state in Enum.GetValues(typeof(GameSaveSlotState)))
            {
                slot.Refresh(Snapshot(index, state), gameplay);
                slot.SetSelected(true);
                bool load = state == GameSaveSlotState.Occupied;
                bool save = gameplay && index != 0 && state != GameSaveSlotState.Unavailable;
                bool delete = index != 0 && state != GameSaveSlotState.Empty && state != GameSaveSlotState.Unavailable;
                AssertAction(slot, "loadButton", load);
                AssertAction(slot, "saveButton", save);
                AssertAction(slot, "deleteButton", delete);
                GameObject actions = (GameObject)new SerializedObject(slot).FindProperty("actionsRoot").objectReferenceValue;
                Assert.That(actions.activeSelf, Is.EqualTo(load || save || delete));
                Assert.That(ButtonField(slot, "saveButton").GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo(state == GameSaveSlotState.Empty ? "保存" : "覆盖保存"));
                slot.SetSelected(false);
                Assert.That(actions.activeSelf, Is.False);
            }
            slot.Refresh(null, true);
            slot.SetSelected(true);
            AssertAction(slot, "loadButton", false);
            AssertAction(slot, "saveButton", false);
            AssertAction(slot, "deleteButton", false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionIsExclusiveAndHiddenActionsCannotInvokeCallbacks()
        {
            int calls = 0;
            foreach (SaveSlotView slot in slots)
            {
                slot.Bind(_ => calls++, _ => calls++, _ => calls++);
                foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                    ButtonField(slot, field).onClick.Invoke();
            }
            Assert.That(calls, Is.Zero);
            ButtonField(slots[1], "selectButton").onClick.Invoke();
            ButtonField(slots[2], "selectButton").onClick.Invoke();
            for (int i = 0; i < slots.Length; i++)
            {
                GameObject actions = (GameObject)new SerializedObject(slots[i]).FindProperty("actionsRoot").objectReferenceValue;
                Assert.That(actions.activeInHierarchy, Is.EqualTo(i == 2));
            }
            slots[2].Refresh(Snapshot(2, GameSaveSlotState.Empty), true);
            ButtonField(slots[2], "loadButton").onClick.Invoke();
            ButtonField(slots[2], "deleteButton").onClick.Invoke();
            Assert.That(calls, Is.Zero);
            ButtonField(slots[2], "saveButton").onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AvailableActionsReflowWithoutBlankGapsAndCanRefreshWhileSelected()
        {
            SaveSlotView slot = slots[1];
            slot.Refresh(Snapshot(1, GameSaveSlotState.Empty), true);
            slot.SetSelected(true);
            RectTransform actions = (RectTransform)ButtonField(slot, "saveButton").transform.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
            Canvas.ForceUpdateCanvases();
            float firstY = ((RectTransform)ButtonField(slot, "saveButton").transform).anchoredPosition.y;
            slot.Refresh(Snapshot(1), true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
            Canvas.ForceUpdateCanvases();
            Assert.That(((RectTransform)ButtonField(slot, "loadButton").transform).anchoredPosition.y, Is.EqualTo(firstY).Within(.01f));
            Assert.That(((RectTransform)ButtonField(slot, "saveButton").transform).anchoredPosition.y, Is.EqualTo(firstY - 62f).Within(.01f));
            Assert.That(((RectTransform)ButtonField(slot, "deleteButton").transform).anchoredPosition.y, Is.EqualTo(firstY - 124f).Within(.01f));
            slot.Refresh(Snapshot(1, GameSaveSlotState.Empty), true);
            yield return new WaitForEndOfFrame();
            Capture("slot-empty-save");
            slot.Refresh(Snapshot(1, GameSaveSlotState.Unavailable), true);
            Assert.That(actions.gameObject.activeSelf, Is.False);
            slot.Refresh(Snapshot(1), true);
            Assert.That(actions.gameObject.activeSelf, Is.True);
        }

        private static void AssertAction(SaveSlotView slot, string field, bool available)
        {
            Button button = ButtonField(slot, field);
            Assert.That(button.interactable, Is.EqualTo(available), field);
            Assert.That(button.gameObject.activeSelf, Is.EqualTo(available), field);
            Assert.That(button.IsActive(), Is.EqualTo(available), field);
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
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)ButtonField(slots[i], "loadButton").transform.parent);
                Canvas.ForceUpdateCanvases();
                var fields = new SerializedObject(slots[i]);
                var frame = (GameObject)fields.FindProperty("selectionFrame").objectReferenceValue;
                Vector3 center = artwork.InverseTransformPoint(frame.transform.position);
                Assert.That(center.x, Is.EqualTo(i % 2 == 0 ? -412f : 412f).Within(.02f));
                Assert.That(center.y, Is.EqualTo(196.5f - (i / 2) * 266.25f).Within(.02f));
                var row = (RectTransform)slots[i].transform;
                foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                {
                    if (!ButtonField(slots[i], field).gameObject.activeSelf) continue;
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
