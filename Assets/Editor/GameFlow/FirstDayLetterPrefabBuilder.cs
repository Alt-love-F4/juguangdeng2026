using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Creates missing assets only. Does not overwrite edited prefabs or any scene.</summary>
    public static class FirstDayLetterPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/FirstDayLetterPanel.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/Fonts/SimHei SDF.asset");
        private static readonly Color Ink = new Color(.22f, .14f, .095f);
        private static readonly Color Gold = new Color(.82f, .69f, .46f);

        [MenuItem("Tools/Game Flow/Create Missing First Day Letter Panel")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the letter panel.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            if (Font == null) throw new InvalidOperationException("SimHei SDF font is missing.");
            GameObject root = new GameObject("FirstDayLetterPanel", typeof(RectTransform), typeof(CanvasGroup),
                typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                root.GetComponent<Canvas>().sortingOrder = 700;
                FirstDayLetterPanel panel = root.AddComponent<FirstDayLetterPanel>();
                CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
                Image blackout = Image(root.transform, "Blackout", Color.black);
                Stretch(blackout.rectTransform);
                blackout.raycastTarget = true;

                RectTransform envelopeStage = Stage(root.transform, "EnvelopeStage");
                CanvasGroup envelopeGroup = envelopeStage.gameObject.AddComponent<CanvasGroup>();
                TMP_Text chapter = Text(envelopeStage, "Chapter", "一 封 未 寄 出 的 信", 27f, Gold);
                Center(chapter.rectTransform, new Vector2(0, 315), new Vector2(800, 50));
                TMP_Text place = Text(envelopeStage, "Place", "圣诺特 · 沸点咖啡馆", 19f, new Color(.5f, .49f, .46f));
                Center(place.rectTransform, new Vector2(0, 260), new Vector2(800, 40));
                RectTransform envelope = Box(envelopeStage, "Envelope", Vector2.zero, new Vector2(650, 360));
                Image shadow = Image(envelope, "Shadow", new Color(.16f, .1f, .065f));
                Stretch(shadow.rectTransform);
                shadow.rectTransform.anchoredPosition = new Vector2(10, -14);
                Image face = Image(envelope, "Face", new Color(.82f, .72f, .53f));
                Stretch(face.rectTransform);
                // Native UI shapes: envelope folds, postage mark and wax seal. No generated raster assets.
                Fold(envelope, "LeftFold", new Vector2(-155, 65), -23f);
                Fold(envelope, "RightFold", new Vector2(155, 65), 23f);
                Image postage = Image(envelope, "Postage", new Color(.47f, .31f, .22f));
                Center(postage.rectTransform, new Vector2(260, 115), new Vector2(65, 78));
                TMP_Text stamp = Text(postage.transform, "Mark", "信", 27f, new Color(.85f, .76f, .6f));
                Stretch(stamp.rectTransform);
                Image seal = Image(envelope, "Seal", new Color(.43f, .14f, .12f));
                Center(seal.rectTransform, new Vector2(0, -8), new Vector2(74, 74));
                seal.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                TMP_Text sealLabel = Text(seal.transform, "SealMark", "沸", 23f, new Color(.88f, .64f, .43f));
                Stretch(sealLabel.rectTransform);
                TMP_Text address = Text(envelope, "Address", "致我最放心不下的孩子", 23f, Ink);
                Center(address.rectTransform, new Vector2(0, -112), new Vector2(550, 50));
                Button envelopeButton = ClickTarget(envelope, "OpenEnvelope");
                Stretch(envelopeButton.GetComponent<RectTransform>());
                TMP_Text hint = Text(envelopeStage, "ClickHint", "点击信封，拆开叔叔留给你的信", 25f, Gold);
                Center(hint.rectTransform, new Vector2(0, -270), new Vector2(1000, 55));
                CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();

                RectTransform paper = Box(root.transform, "LetterPaper", Vector2.zero, new Vector2(1260, 900));
                CanvasGroup paperGroup = paper.gameObject.AddComponent<CanvasGroup>();
                Image paperShadow = Image(paper, "Shadow", new Color(.12f, .085f, .05f));
                Stretch(paperShadow.rectTransform);
                paperShadow.rectTransform.anchoredPosition = new Vector2(12, -12);
                Image page = Image(paper, "Page", new Color(.93f, .875f, .745f));
                Stretch(page.rectTransform);
                page.raycastTarget = true;
                TMP_Text title = Text(paper, "Title", "叔叔的信", 36f, Ink);
                TopLeft(title.rectTransform, new Vector2(76, -52), new Vector2(1108, 60));
                title.alignment = TextAlignmentOptions.TopLeft;
                Image line = Image(paper, "Rule", new Color(.66f, .52f, .34f, .5f));
                TopLeft(line.rectTransform, new Vector2(76, -130), new Vector2(1108, 2));

                RectTransform scrollRoot = Child(paper, "LetterScroll", typeof(ScrollRect)).GetComponent<RectTransform>();
                TopLeft(scrollRoot, new Vector2(76, -157), new Vector2(1108, 610));
                ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 36f;
                RectTransform viewport = Child(scrollRoot, "Viewport", typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
                Stretch(viewport);
                viewport.GetComponent<Image>().color = Color.clear;
                viewport.GetComponent<Image>().raycastTarget = true;
                TMP_Text body = Text(viewport, "Body", "", 27f, Ink);
                body.alignment = TextAlignmentOptions.TopLeft;
                body.lineSpacing = 13f;
                body.paragraphSpacing = 10f;
                body.rectTransform.anchorMin = new Vector2(0, 1);
                body.rectTransform.anchorMax = new Vector2(1, 1);
                body.rectTransform.pivot = new Vector2(.5f, 1);
                body.rectTransform.anchoredPosition = Vector2.zero;
                body.rectTransform.sizeDelta = new Vector2(0, 610);
                body.raycastTarget = true;
                ContentSizeFitter fitter = body.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.viewport = viewport;
                scroll.content = body.rectTransform;

                Button signatureButton = ClickTarget(paper, "SignatureButton");
                TopLeft(signatureButton.GetComponent<RectTransform>(), new Vector2(525, -784), new Vector2(660, 54));
                TMP_Text signature = Text(signatureButton.transform, "Signature", "—— 永远惦念你的叔叔", 28f, Ink);
                Stretch(signature.rectTransform);
                signature.alignment = TextAlignmentOptions.MidlineRight;
                TMP_Text readingHint = Text(paper, "ReadingHint", "", 20f, new Color(.44f, .34f, .22f));
                TopLeft(readingHint.rectTransform, new Vector2(76, -850), new Vector2(1108, 30));
                readingHint.alignment = TextAlignmentOptions.MidlineRight;

                Button skipButton = ClickTarget(root.transform, "SkipButton");
                BottomRight(skipButton.GetComponent<RectTransform>(), new Vector2(-50, 30), new Vector2(150, 50));
                skipButton.GetComponent<Image>().color = new Color(.12f, .08f, .05f, .82f);
                TMP_Text skipLabel = Text(skipButton.transform, "Label", "跳过", 22f, Gold);
                Stretch(skipLabel.rectTransform);

                SerializedObject serialized = new SerializedObject(panel);
                Set(serialized, "rootGroup", rootGroup);
                Set(serialized, "envelopeGroup", envelopeGroup);
                Set(serialized, "envelope", envelope);
                Set(serialized, "envelopeButton", envelopeButton);
                Set(serialized, "hintGroup", hintGroup);
                Set(serialized, "paperGroup", paperGroup);
                Set(serialized, "paper", paper);
                Set(serialized, "letterBody", body);
                Set(serialized, "signatureText", signature);
                Set(serialized, "signatureButton", signatureButton);
                Set(serialized, "skipButton", skipButton);
                Set(serialized, "readingHint", readingHint);
                Set(serialized, "letterScroll", scroll);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                paper.gameObject.SetActive(false);
                signatureButton.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            Debug.Log("[DayFlow] Created FirstDayLetterPanel.prefab. Existing prefabs and scenes were not modified.");
        }

        private static void Fold(Transform parent, string name, Vector2 position, float angle)
        {
            Image fold = Image(parent, name, new Color(.54f, .4f, .23f, .8f));
            Center(fold.rectTransform, position, new Vector2(357, 3));
            fold.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }
        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (Type type in components) go.AddComponent(type);
            return go;
        }
        private static RectTransform Stage(Transform parent, string name)
        {
            RectTransform rect = Child(parent, name).GetComponent<RectTransform>();
            Stretch(rect); return rect;
        }
        private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = Child(parent, name).GetComponent<RectTransform>();
            Center(rect, position, size); return rect;
        }
        private static Image Image(Transform parent, string name, Color color)
        {
            Image image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private static Button ClickTarget(Transform parent, string name)
        {
            Button button = Child(parent, name, typeof(Image), typeof(Button)).GetComponent<Button>();
            Image image = button.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true;
            button.targetGraphic = image; button.transition = Selectable.Transition.None; return button;
        }
        private static TMP_Text Text(Transform parent, string name, string value, float fontSize, Color color)
        {
            TMP_Text text = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.font = Font; text.fontSize = fontSize; text.text = value; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.enableWordWrapping = true; return text;
        }
        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void BottomRight(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Set(SerializedObject serialized, string name, Object value)
        { serialized.FindProperty(name).objectReferenceValue = value; }
    }
}
