using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Convert the imported PSD layer bounds to UGUI; never regenerate or modify source artwork.</summary>
    public static class DialoguePsdUIBuilder
    {
        public const string SourcePath = "Assets/Art/Gameplay/对话框ui.prefab";
        public const string PanelPath = "Assets/Resources/Art/UIPrefab/DialoguePanel.prefab";
        private const float ArtScale = 1920f / 4096f;
        private static readonly Color Ink = new Color(.30f, .12f, .045f);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset");

        [MenuItem("Tools/Game Flow/Apply PSD Dialogue UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null || Font == null) throw new InvalidOperationException("Missing imported dialogue PSD or font.");
            SpriteRenderer[] layers = source.GetComponentsInChildren<SpriteRenderer>(true);
            Array.Sort(layers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            Bounds canvas = BoundsOf(Find(layers, "图层 9"));
            // Horizontal borders include the entire illustrated side strips. Only the blank center stretches.
            Slice(Find(layers, "对话框").sprite, new Vector4(278, 34, 278, 34));
            Slice(Find(layers, "姓名框").sprite, new Vector4(30, 22, 30, 22));
            Slice(Find(layers, "选项框").sprite, new Vector4(66, 28, 66, 28));
            Slice(Find(layers, "不可选选项框").sprite, new Vector4(66, 28, 66, 28));
            // Reload sprites after importer reimport.
            source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            layers = source.GetComponentsInChildren<SpriteRenderer>(true);
            Array.Sort(layers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

            string backup = Path.GetFullPath(".unity/recovery/dialogue-psd-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backup);
            File.Copy(PanelPath, Path.Combine(backup, "DialoguePanel.prefab"));
            File.Copy(PanelPath + ".meta", Path.Combine(backup, "DialoguePanel.prefab.meta"));
            File.Copy(PresentationDebugSceneController.ScenePath, Path.Combine(backup, "PresentationDebugScene.unity"));
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                DialoguePanel panel = root.GetComponent<DialoguePanel>();
                SerializedObject fields = new SerializedObject(panel);
                Image previousRight = fields.FindProperty("rightPortrait").objectReferenceValue as Image;
                Sprite rightSprite = previousRight != null ? previousRight.sprite : null;
                // Keep the existing prefab GUID and root/script file IDs, preserving external references.
                while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                RectTransform rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);
                root.GetComponent<Canvas>().sortingOrder = 800;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.alpha = 0; group.blocksRaycasts = group.interactable = false;
                Image blocker = Image(root.transform, "InputBlocker", null);
                Stretch(blocker.rectTransform); blocker.color = Color.clear; blocker.raycastTarget = true;

                RectTransform artwork = Child(root.transform, "Artwork");
                Stretch(artwork);
                AspectRatioFitter aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = canvas.size.x / canvas.size.y;
                GameObject cafe = Child(artwork, "PsdCafeBackground").gameObject;
                Stretch((RectTransform)cafe.transform);
                cafe.SetActive(false);
                RawImage background = Child(artwork, "Background").gameObject.AddComponent<RawImage>();
                Stretch(background.rectTransform); background.raycastTarget = false;
                background.texture = null; background.enabled = false;
                Image left = Portrait(artwork, "LeftPortrait", Find(layers, "人物-咖啡馆").sprite,
                    BoundsOf(Find(layers, "人物-咖啡馆")), canvas);
                Image right = Portrait(artwork, "RightPortrait", rightSprite,
                    BoundsOf(Find(layers, "人物-咖啡馆")), canvas);
                Image center = Portrait(artwork, "CenterPortrait", null,
                    BoundsOf(Find(layers, "人物-咖啡馆")), canvas);

                Bounds boxBounds = BoundsOf(Find(layers, "对话框"));
                Image box = Image(artwork, "DialogueBox", Find(layers, "对话框").sprite);
                Place(box.rectTransform, boxBounds, canvas);
                // Fixed width, bottom pivot: growth only moves the top, never the original bottom edge.
                float bottom = (boxBounds.min.y - canvas.min.y) / canvas.size.y;
                box.rectTransform.anchorMin = new Vector2(box.rectTransform.anchorMin.x, bottom);
                box.rectTransform.anchorMax = new Vector2(box.rectTransform.anchorMax.x, bottom);
                box.rectTransform.pivot = new Vector2(.5f, 0f);
                box.rectTransform.sizeDelta = new Vector2(0f, boxBounds.size.y * 100f * ArtScale);
                box.type = UnityEngine.UI.Image.Type.Sliced; box.pixelsPerUnitMultiplier = 1f / ArtScale;
                Image name = Image(box.transform, "NamePlate", Find(layers, "姓名框").sprite);
                PlaceRelative(name.rectTransform, BoundsOf(Find(layers, "姓名框")), boxBounds, true);
                name.type = UnityEngine.UI.Image.Type.Sliced; name.pixelsPerUnitMultiplier = 1f / ArtScale;
                TMP_Text speaker = Text(name.transform, "Speaker", "阿黛尔", 24f, Ink, TextAlignmentOptions.Center);
                Stretch(speaker.rectTransform); speaker.margin = new Vector4(14, 3, 14, 3);
                TMP_Text dialogue = Text(box.transform, "Dialogue", "你知道的。", 32f, Ink, TextAlignmentOptions.TopLeft);
                Stretch(dialogue.rectTransform);
                // Blank center only: never put live text over the illustrated side strips.
                dialogue.rectTransform.offsetMin = new Vector2(190f, 72f);
                dialogue.rectTransform.offsetMax = new Vector2(-145f, -65f);
                dialogue.overflowMode = TextOverflowModes.Page;
                Image nextImage = Image(box.transform, "Continue", Find(layers, "翻页键").sprite);
                PlaceRelative(nextImage.rectTransform, BoundsOf(Find(layers, "翻页键")), boxBounds, false);
                Button next = Button(nextImage);
                Button body = box.gameObject.AddComponent<Button>(); body.targetGraphic = box;
                body.transition = Selectable.Transition.None; body.navigation = NoNavigation(); box.raycastTarget = true;
                Image skipImage = Image(box.transform, "Skip", Find(layers, "skip底").sprite);
                PlaceRelative(skipImage.rectTransform, BoundsOf(Find(layers, "skip底")), boxBounds, true);
                Button skip = Button(skipImage);
                TMP_Text skipText = Text(skipImage.transform, "Label", "SKIP", 21f, new Color(.88f, .74f, .59f), TextAlignmentOptions.Center);
                Place(skipText.rectTransform, BoundsOf(Find(layers, "skip")), BoundsOf(Find(layers, "skip底")));

                Image dimmer = Image(artwork, "OptionsDimmer", Find(layers, "图层 7").sprite);
                Stretch(dimmer.rectTransform); dimmer.gameObject.SetActive(false);
                ScrollRect scroll = MakeOptions(artwork, layers, canvas, out RectTransform content, out DialogueOptionView template);
                Bind(fields, "canvasGroup", root.GetComponent<CanvasGroup>());
                Bind(fields, "background", background);
                Bind(fields, "psdBackground", cafe);
                Bind(fields, "artworkRoot", artwork);
                Bind(fields, "leftPortrait", left); Bind(fields, "rightPortrait", right); Bind(fields, "centerPortrait", center);
                Bind(fields, "leftPortraitFrame", left.transform.parent.GetComponent<Image>());
                Bind(fields, "rightPortraitFrame", right.transform.parent.GetComponent<Image>());
                Bind(fields, "centerPortraitFrame", center.transform.parent.GetComponent<Image>());
                Bind(fields, "dialogueBox", box.rectTransform);
                Bind(fields, "speakerText", speaker); Bind(fields, "dialogueText", dialogue);
                Bind(fields, "titleText", null); Bind(fields, "progressText", null); Bind(fields, "hintText", null);
                Bind(fields, "continueButton", next); Bind(fields, "dialogueBodyButton", body);
                Bind(fields, "cancelButton", null); Bind(fields, "skipButton", skip);
                Bind(fields, "optionRoot", scroll.gameObject);
                Bind(fields, "optionGroup", scroll.GetComponent<CanvasGroup>());
                Bind(fields, "optionContent", content); Bind(fields, "optionScroll", scroll);
                Bind(fields, "optionDimmer", dimmer); Bind(fields, "optionTemplate", template);
                Bind(fields, "optionEnabledSprite", Find(layers, "选项框").sprite);
                Bind(fields, "optionDisabledSprite", Find(layers, "不可选选项框").sprite);
                fields.FindProperty("adaptDialogueHeight").boolValue = true;
                fields.FindProperty("maximumDialogueLines").intValue = 3;
                fields.FindProperty("dialogueMinHeight").floatValue = boxBounds.size.y * 100f * ArtScale;
                fields.FindProperty("dialoguePaddingHeight").floatValue = 137f;
                fields.FindProperty("mAutoFocusOnShow").boolValue = false;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Remove only the old imported sprite preview added underneath this panel in the shared scene.
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == PresentationDebugSceneController.ScenePath)
            {
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                    foreach (DialoguePanel panel in sceneRoot.GetComponentsInChildren<DialoguePanel>(true))
                    {
                        Transform old = panel.transform.Find("对话框ui");
                        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                        PrefabUtility.RevertObjectOverride(panel.transform, InteractionMode.AutomatedAction);
                    }
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                    if (sceneRoot.name == "DebugRoomCanvas" || sceneRoot.name == "DebugHelpOverlay") sceneRoot.SetActive(true);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Dialogue PSD] Applied UGUI layout, preserved original bounds and prefab GUID. Backup: " + backup);
        }

        private static ScrollRect MakeOptions(Transform parent, SpriteRenderer[] layers, Bounds canvas,
            out RectTransform content, out DialogueOptionView template)
        {
            Bounds a = BoundsOf(Find(layers, "不可选选项框")), b = BoundsOf(Find(layers, "选项框"));
            Bounds viewportBounds = a; viewportBounds.Encapsulate(b);
            RectTransform root = Child(parent, "DialogueOptions");
            Place(root, viewportBounds, canvas);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            root.gameObject.AddComponent<CanvasGroup>();
            RectTransform viewport = Child(root, "Viewport"); Stretch(viewport);
            Image raycast = viewport.gameObject.AddComponent<Image>(); raycast.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Child(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = (a.center.y - b.center.y - b.size.y) * 100f * ArtScale;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            RectTransform row = Child(content, "OptionTemplate");
            Image image = row.gameObject.AddComponent<Image>();
            image.sprite = Find(layers, "选项框").sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 1f / ArtScale;
            Button button = Button(image);
            VerticalLayoutGroup rowLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(45, 45, 14, 14);
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true; rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement minimum = row.gameObject.AddComponent<LayoutElement>();
            minimum.minHeight = b.size.y * 100f * ArtScale;
            TMP_Text label = Text(row, "Label", "我不知道", 26f, Ink, TextAlignmentOptions.Center);
            TMP_Text requirement = Text(row, "Requirement", "需要更多情报", 21f, new Color(.31f, .20f, .15f), TextAlignmentOptions.Center);
            template = row.gameObject.AddComponent<DialogueOptionView>();
            var fields = new SerializedObject(template);
            Bind(fields, "button", button); Bind(fields, "surface", image);
            Bind(fields, "label", label); Bind(fields, "requirement", requirement);
            fields.ApplyModifiedPropertiesWithoutUndo();
            row.gameObject.SetActive(false); root.gameObject.SetActive(false);
            return scroll;
        }

        private static Image Portrait(Transform parent, string name, Sprite sprite, Bounds bounds, Bounds canvas)
        {
            Image frame = Image(parent, name + "Frame", null); frame.color = Color.clear;
            Place(frame.rectTransform, bounds, canvas);
            Image image = Image(frame.transform, name, sprite); Stretch(image.rectTransform);
            image.preserveAspect = true; image.enabled = sprite != null; frame.enabled = image.enabled;
            return image;
        }
        private static Navigation NoNavigation() => new Navigation { mode = Navigation.Mode.None };
        private static Button Button(Image image)
        {
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = NoNavigation();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1, .94f, .84f); colors.pressedColor = new Color(.80f, .74f, .67f);
            colors.selectedColor = colors.normalColor; colors.disabledColor = Color.white; button.colors = colors;
            return button;
        }
        private static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        private static Image Image(Transform parent, string name, Sprite sprite)
        {
            Image image = Child(parent, name).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false; image.useSpriteMesh = false; return image;
        }
        private static TMP_Text Text(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = Child(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = value; text.fontSize = size; text.color = color;
            text.richText = false; text.raycastTarget = false; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
        private static void Bind(SerializedObject fields, string name, Object value) => fields.FindProperty(name).objectReferenceValue = value;
        private static SpriteRenderer Find(SpriteRenderer[] layers, string name)
        {
            foreach (var layer in layers) if (layer.name == name) return layer;
            throw new InvalidOperationException("Missing PSD layer: " + name);
        }
        private static Bounds BoundsOf(SpriteRenderer layer) => layer.bounds;
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
        private static void Place(RectTransform rect, Bounds bounds, Bounds parent)
        {
            rect.anchorMin = new Vector2((bounds.min.x - parent.min.x) / parent.size.x, (bounds.min.y - parent.min.y) / parent.size.y);
            rect.anchorMax = new Vector2((bounds.max.x - parent.min.x) / parent.size.x, (bounds.max.y - parent.min.y) / parent.size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void PlaceRelative(RectTransform rect, Bounds bounds, Bounds box, bool top)
        {
            Vector2 anchor = new Vector2((bounds.center.x - box.min.x) / box.size.x, top ? 1f : 0f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y) * 100f * ArtScale;
            rect.anchoredPosition = new Vector2(0f, (bounds.center.y - (top ? box.max.y : box.min.y)) * 100f * ArtScale);
        }
        private static void Slice(Sprite sprite, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing PSD texture importer.");
            importer.spriteBorder = border;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
