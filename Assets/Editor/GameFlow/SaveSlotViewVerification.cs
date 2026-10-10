using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Spotlight.GameFlow.Editor
{
    internal static class SaveSlotViewVerification
    {
        [MenuItem("Tools/Game Flow/Run Save Slot View Regression")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetFullPath("output/save-slot-view"));
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before running save slot regression.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before running save slot regression.");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            File.WriteAllText("output/save-slot-view/run.started", DateTime.UtcNow.ToString("O"));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                groupNames = new[] { "Spotlight.GameFlow.Tests.SaveSlotViewPrefabTests", "Spotlight.GameFlow.Tests.SaveSlotViewPlayModeTests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi owner;
            public Results(TestRunnerApi owner) => this.owner = owner;
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("output/save-slot-view/test-results.xml"));
                    File.WriteAllText("output/save-slot-view/run.finished", "Passed=" + result.PassCount + "; Failed=" + result.FailCount);
                }
                finally
                {
                    TestRunnerApi.UnregisterTestCallback(this);
                    if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                }
            }
        }
    }
}

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Rebuilds only action presentation on the existing save prefab; preserves card layout and artwork.</summary>
    internal static class SaveSlotActionLayout
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/SaveManagementPanel.prefab";
        private const string BeanPath = "Assets/Art/UI/SaveActions/SaveActionBean.png";

        [MenuItem("Tools/Game Flow/Rebuild Save Slot Action Layout")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the save action layout.");
            Sprite bean = AssetDatabase.LoadAssetAtPath<Sprite>(BeanPath);
            if (bean == null) throw new InvalidOperationException("Missing extracted single-action bean: " + BeanPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                SaveSlotView[] rows = root.GetComponentsInChildren<SaveSlotView>(true);
                Array.Sort(rows, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                for (int i = 0; i < rows.Length; i++)
                {
                    SaveSlotView row = rows[i];
                    // Remove the obsolete composite, including copies nested under SelectedFrame.
                    foreach (Transform transform in row.GetComponentsInChildren<Transform>(true))
                        if (transform != null && transform.name == "Actions底图")
                            UnityEngine.Object.DestroyImmediate(transform.gameObject);
                    var fields = new SerializedObject(row);
                    GameObject actions = (GameObject)fields.FindProperty("actionsRoot").objectReferenceValue;
                    RectTransform rect = (RectTransform)actions.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(-92f, 0f);
                    rect.sizeDelta = new Vector2(164f, 200f);
                    VerticalLayoutGroup layout = actions.GetComponent<VerticalLayoutGroup>();
                    if (layout == null) layout = actions.AddComponent<VerticalLayoutGroup>();
                    layout.padding = new RectOffset(0, 0, 5, 0);
                    layout.spacing = 4f;
                    layout.childAlignment = TextAnchor.UpperLeft;
                    layout.childControlWidth = layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                    foreach (string field in new[] { "loadButton", "saveButton", "deleteButton" })
                    {
                        Button button = (Button)fields.FindProperty(field).objectReferenceValue;
                        ConfigureAction(button, bean);
                        button.gameObject.SetActive(false);
                    }
                    actions.SetActive(false);
                    GameObject selection = (GameObject)fields.FindProperty("selectionFrame").objectReferenceValue;
                    if (selection != null) selection.SetActive(false);
                    Button select = (Button)fields.FindProperty("selectButton").objectReferenceValue;
                    if (select != null) select.navigation = new Navigation { mode = Navigation.Mode.None };
                }
                SaveManagementPanel panel = root.GetComponent<SaveManagementPanel>();
                var panelFields = new SerializedObject(panel);
                SerializedProperty slots = panelFields.FindProperty("slots");
                slots.arraySize = rows.Length;
                for (int i = 0; i < rows.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                panelFields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Save slot action layout rebuilt: " + rows.Length + " rows.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureAction(Button button, Sprite bean)
        {
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            // Visibility belongs to the whole action, not an alpha-zero CanvasGroup.
            foreach (CanvasGroup group in button.GetComponents<CanvasGroup>())
                UnityEngine.Object.DestroyImmediate(group);
            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 58f;
            element.flexibleHeight = 0f;
            Image hit = button.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
            Transform iconTransform = button.transform.Find("Bean");
            if (iconTransform == null) iconTransform = button.transform.Find("Background");
            Image icon = iconTransform.GetComponent<Image>();
            iconTransform.name = "Background";
            icon.sprite = bean;
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, .5f);
            iconRect.pivot = new Vector2(0f, .5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(45f, 42.5f);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.color = new Color(.60f, .36f, .15f);
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSize = label.fontSizeMax = 23f;
            label.fontSizeMin = 20f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(54f, 0f);
            textRect.offsetMax = Vector2.zero;
            button.targetGraphic = hit;
        }
    }
}
