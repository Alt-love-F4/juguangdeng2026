using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow.Editor
{
    public static class DetailsStoryArchivePrefabInstaller
    {
        private const string OriginalPath = "Assets/Resources/Art/UIPrefab/DetailsPanel.prefab";
        private const string BackupPath = "Assets/Resources/Art/UIPrefab/DetailsPanel_OriginalBackup.prefab";
        private const string VariantPath = "Assets/Resources/Art/UIPrefab/DetailsPanel_StoryArchive.prefab";

        [MenuItem("Tools/Game Flow/Install Story Archive Details Panel")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing the details panel.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPath) == null)
                throw new InvalidOperationException("The original details panel prefab is missing.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BackupPath) == null
                && !AssetDatabase.CopyAsset(OriginalPath, BackupPath))
                throw new InvalidOperationException("Could not create the original details panel backup.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath) == null
                && !AssetDatabase.CopyAsset(OriginalPath, VariantPath))
                throw new InvalidOperationException("Could not create the story archive prefab copy.");

            GameObject staged = PrefabUtility.LoadPrefabContents(VariantPath);
            try
            {
                if (staged.transform.Find("Window/Story") == null) AddStoryPage(staged);
                BindStoryReferences(staged);
                staged.name = "DetailsPanel";
                EditorSceneManager.MarkSceneDirty(staged.scene);
                PrefabUtility.SaveAsPrefabAsset(staged, VariantPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(staged); }

            staged = PrefabUtility.LoadPrefabContents(VariantPath);
            try
            {
                Validate(staged);
                staged.name = "DetailsPanel";
                EditorSceneManager.MarkSceneDirty(staged.scene);
                PrefabUtility.SaveAsPrefabAsset(staged, OriginalPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(staged); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Story archive details panel installed. Original prefab backup: " + BackupPath);
        }

        [MenuItem("Tools/Game Flow/Restore Original Details Panel Backup")]
        public static void RestoreOriginal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before restoring the details panel.");
            GameObject backup = AssetDatabase.LoadAssetAtPath<GameObject>(BackupPath);
            if (backup == null) throw new InvalidOperationException("No original details panel backup exists.");
            GameObject root = PrefabUtility.LoadPrefabContents(BackupPath);
            try
            {
                root.name = "DetailsPanel";
                PrefabUtility.SaveAsPrefabAsset(root, OriginalPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Original details panel restored from " + BackupPath);
        }

        private static void AddStoryPage(GameObject root)
        {
            var panel = root.GetComponent<DetailsPanel>();
            var view = root.GetComponent<DetailsPagesView>();
            var window = root.transform.Find("Window") as RectTransform;
            var chrome = root.transform.Find("Window/SharedChrome") as RectTransform;
            if (panel == null || view == null || window == null || chrome == null)
                throw new InvalidOperationException("The copied details panel is missing its expected shared-frame hierarchy.");

            RectTransform world = root.transform.Find("Window/World") as RectTransform;
            if (world == null) throw new InvalidOperationException("The world details page is missing.");
            var page = Rect(window, "Story", 0, 0, 2048, 1152);
            page.SetSiblingIndex(world.GetSiblingIndex() + 1);
            var titleBinding = HiddenText(page, "StorySectionTitle");
            var headingBinding = HiddenText(page, "StorySectionHeading");
            var descriptionBinding = HiddenText(page, "StorySectionDescription");

            var archive = Rect(page, "StoryView", 345, 275, 1358, 685);
            var leftRule = Image(archive, "ListRule", 477, 48, 2, 578, new Color(.49f, .33f, .18f, .45f));
            leftRule.raycastTarget = false;
            var listScroll = CreateScroll(archive, "StoryList", 34, 35, 415, 600, out RectTransform listContent);
            var listLayout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(0, 8, 0, 0);
            listLayout.spacing = 7;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var reader = Rect(archive, "Reader", 505, 35, 812, 600);
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Map/SourceHanSansSC-Regular-2 SDF.asset");
            var intro = root.transform.Find("Window/MyInfo/Introduction")?.GetComponent<TMP_Text>();
            TMP_FontAsset font = bodyFont != null ? bodyFont : intro != null ? intro.font : null;
            var storyTitle = Text(reader, "StoryTitle", "选择一段已解锁的剧情", 0, 0, 790, 50, 31, font, true);
            storyTitle.color = new Color(.28f, .15f, .07f);
            var storyMeta = Text(reader, "StoryMeta", string.Empty, 0, 57, 790, 32, 19, font, false);
            storyMeta.color = new Color(.43f, .31f, .19f);
            var bodyScroll = CreateScroll(reader, "BodyScroll", 0, 106, 790, 490, out RectTransform bodyContent);
            var storyBody = Text(bodyContent, "StoryBody", "暂无可查看的剧情记录。", 0, 0, 770, 40, 23, font, false);
            storyBody.alignment = TextAlignmentOptions.TopLeft;
            storyBody.color = new Color(.22f, .18f, .13f);
            storyBody.overflowMode = TextOverflowModes.Overflow;
            storyBody.rectTransform.anchorMin = new Vector2(0, 1);
            storyBody.rectTransform.anchorMax = new Vector2(1, 1);
            storyBody.rectTransform.pivot = new Vector2(.5f, 1);
            storyBody.rectTransform.sizeDelta = new Vector2(0, 40);
            bodyContent.gameObject.AddComponent<VerticalLayoutGroup>().childControlHeight = true;
            bodyContent.GetComponent<VerticalLayoutGroup>().childControlWidth = true;
            bodyContent.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            bodyContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var tabHit = Image(window, "Tab_3", 902, 135, 119, 105, Color.clear);
            tabHit.raycastTarget = true;
            var tab = tabHit.gameObject.AddComponent<Button>();
            tab.targetGraphic = tabHit;
            tab.transition = Selectable.Transition.None;
            tab.navigation = new Navigation { mode = Navigation.Mode.None };
            var idleSource = chrome.Find("IdleTab_2").GetComponent<Image>();
            var idle = Image(chrome, "IdleTab_3", 902, 135, 119.5f, 96.5f, Color.white);
            idle.sprite = idleSource.sprite;
            idle.color = idleSource.color;
            var label = Text(chrome, "StoryTabLabel", "剧情", 902, 135, 119, 96, 24, font, true);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.34f, .21f, .10f);
            var hint = Text(chrome, "StoryPageHint", "回顾已解锁的剧情", 950, 183, 420, 36, 23, font, true);

            var panelObject = new SerializedObject(panel);
            SetArrayObject(panelObject, "pages", 3, page.gameObject);
            SetArrayObject(panelObject, "tabButtons", 3, tab);
            SetArrayObject(panelObject, "idleTabArtwork", 3, idle);
            SetArrayString(panelObject, "pageHints", 3, hint.text);
            var positions = panelObject.FindProperty("selectedTabPositions");
            positions.arraySize = 4;
            positions.GetArrayElementAtIndex(3).vector2Value = positions.GetArrayElementAtIndex(2).vector2Value + new Vector2(125, 0);
            CopySurfaceStyle(panelObject.FindProperty("paperStyles"), 2, 3);
            var binding = panelObject.FindProperty("storyBinding");
            binding.FindPropertyRelative("title").objectReferenceValue = titleBinding;
            binding.FindPropertyRelative("heading").objectReferenceValue = headingBinding;
            binding.FindPropertyRelative("description").objectReferenceValue = descriptionBinding;
            panelObject.ApplyModifiedPropertiesWithoutUndo();

            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("storyList").objectReferenceValue = listContent;
            viewObject.FindProperty("storyScroll").objectReferenceValue = listScroll.GetComponent<ScrollRect>();
            viewObject.FindProperty("storyTitle").objectReferenceValue = storyTitle;
            viewObject.FindProperty("storyMeta").objectReferenceValue = storyMeta;
            viewObject.FindProperty("storyBody").objectReferenceValue = storyBody;
            viewObject.FindProperty("storyReader").objectReferenceValue = reader.gameObject;
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            var extraHint = root.transform.Find("Window/SharedChrome/StoryPageHint");
            if (extraHint != null)
            {
                extraHint.gameObject.SetActive(false);
                var text = extraHint.GetComponent<TMP_Text>();
                if (text != null) text.text = string.Empty;
                EditorUtility.SetDirty(extraHint.gameObject);
            }
            SetArrayString(panelObject, "pageHints", 3, "回顾已解锁的剧情");
            panelObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindStoryReferences(GameObject root)
        {
            var page = root.transform.Find("Window/Story");
            var panelObject = new SerializedObject(root.GetComponent<DetailsPanel>());
            var binding = panelObject.FindProperty("storyBinding");
            binding.FindPropertyRelative("title").objectReferenceValue = page.Find("StorySectionTitle").GetComponent<TMP_Text>();
            binding.FindPropertyRelative("heading").objectReferenceValue = page.Find("StorySectionHeading").GetComponent<TMP_Text>();
            binding.FindPropertyRelative("description").objectReferenceValue = page.Find("StorySectionDescription").GetComponent<TMP_Text>();
            panelObject.ApplyModifiedPropertiesWithoutUndo();

            var viewObject = new SerializedObject(root.GetComponent<DetailsPagesView>());
            var listViewport = page.Find("StoryView/StoryList");
            var reader = page.Find("StoryView/Reader");
            viewObject.FindProperty("storyList").objectReferenceValue = listViewport.Find("Content");
            viewObject.FindProperty("storyScroll").objectReferenceValue = listViewport.GetComponent<ScrollRect>();
            viewObject.FindProperty("storyTitle").objectReferenceValue = reader.Find("StoryTitle").GetComponent<TMP_Text>();
            viewObject.FindProperty("storyMeta").objectReferenceValue = reader.Find("StoryMeta").GetComponent<TMP_Text>();
            viewObject.FindProperty("storyBody").objectReferenceValue = reader.Find("BodyScroll/Content/StoryBody").GetComponent<TMP_Text>();
            viewObject.FindProperty("storyReader").objectReferenceValue = reader.gameObject;
            viewObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Validate(GameObject root)
        {
            var panel = new SerializedObject(root.GetComponent<DetailsPanel>());
            foreach (string name in new[] { "pages", "tabButtons", "paperStyles", "selectedTabPositions", "idleTabArtwork", "pageHints" })
                if (panel.FindProperty(name).arraySize != 4)
                    throw new InvalidOperationException("Story archive binding array has the wrong size: " + name);
            foreach (string name in new[] { "pages", "tabButtons", "idleTabArtwork" })
                for (int i = 0; i < 4; i++)
                    if (panel.FindProperty(name).GetArrayElementAtIndex(i).objectReferenceValue == null)
                        throw new InvalidOperationException("Story archive binding is empty: " + name + "[" + i + "]");
            var view = new SerializedObject(root.GetComponent<DetailsPagesView>());
            foreach (string name in new[] { "storyList", "storyScroll", "storyTitle", "storyMeta", "storyBody", "storyReader" })
                if (view.FindProperty(name).objectReferenceValue == null)
                    throw new InvalidOperationException("Story archive view binding is missing: " + name);
        }

        private static void CopySurfaceStyle(SerializedProperty array, int sourceIndex, int targetIndex)
        {
            array.arraySize = targetIndex + 1;
            var source = array.GetArrayElementAtIndex(sourceIndex);
            var target = array.GetArrayElementAtIndex(targetIndex);
            foreach (string name in new[] { "sprite", "position", "size", "pivot", "scale", "color", "visible" })
            {
                var from = source.FindPropertyRelative(name);
                var to = target.FindPropertyRelative(name);
                switch (from.propertyType)
                {
                    case SerializedPropertyType.ObjectReference: to.objectReferenceValue = from.objectReferenceValue; break;
                    case SerializedPropertyType.Vector2: to.vector2Value = from.vector2Value; break;
                    case SerializedPropertyType.Vector3: to.vector3Value = from.vector3Value; break;
                    case SerializedPropertyType.Color: to.colorValue = from.colorValue; break;
                    case SerializedPropertyType.Boolean: to.boolValue = from.boolValue; break;
                }
            }
        }

        private static void SetArrayObject(SerializedObject data, string propertyName, int index, UnityEngine.Object value)
        {
            var property = data.FindProperty(propertyName);
            property.arraySize = index + 1;
            property.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        private static void SetArrayString(SerializedObject data, string propertyName, int index, string value)
        {
            var property = data.FindProperty(propertyName);
            property.arraySize = index + 1;
            property.GetArrayElementAtIndex(index).stringValue = value;
        }

        private static RectTransform CreateScroll(Transform parent, string name, float x, float y, float width, float height,
            out RectTransform content)
        {
            var viewport = Rect(parent, name, x, y, width, height);
            var mask = viewport.gameObject.AddComponent<Image>();
            mask.color = Color.white;
            mask.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport, "Content", 0, 0, width, height);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            return viewport;
        }

        private static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width,
            float height, float size, TMP_FontAsset font, bool bold)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.color = new Color(.28f, .15f, .07f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return text;
        }

        private static TMP_Text HiddenText(Transform parent, string name)
        {
            TMP_Text text = Text(parent, name, string.Empty, 0, 0, 1, 1, 18, null, false);
            text.gameObject.SetActive(false);
            return text;
        }

        private static Image Image(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
            return rect;
        }
    }
}
