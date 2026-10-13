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
            "Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset");
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
                Sprite letterBackground = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/背景.png");
                if (letterBackground != null) { blackout.sprite = letterBackground; blackout.color = Color.white; }

                RectTransform envelopeStage = Stage(root.transform, "EnvelopeStage");
                CanvasGroup envelopeGroup = envelopeStage.gameObject.AddComponent<CanvasGroup>();
                TMP_Text chapter = Text(envelopeStage, "Chapter", "一 封 未 寄 出 的 信", 27f, Gold);
                Center(chapter.rectTransform, new Vector2(0, 315), new Vector2(800, 50));
                TMP_Text place = Text(envelopeStage, "Place", "圣诺特 · 沸点咖啡馆", 19f, new Color(.5f, .49f, .46f));
                Center(place.rectTransform, new Vector2(0, 260), new Vector2(800, 40));
                RectTransform envelope = Box(envelopeStage, "Envelope", Vector2.zero, new Vector2(650, 360));
                Sprite envelopeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/信封出现.png");
                Sprite envelopeOpenSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/信封自动打开.png");
                Image envelopeArt = Image(envelope, "FormalArt", Color.white);
                Stretch(envelopeArt.rectTransform);
                envelopeArt.sprite = envelopeSprite;
                envelopeArt.preserveAspect = true;
                envelopeArt.raycastTarget = false;
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
                envelope.Find("Shadow").gameObject.SetActive(false);
                envelope.Find("Face").gameObject.SetActive(false);
                envelope.Find("LeftFold").gameObject.SetActive(false);
                envelope.Find("RightFold").gameObject.SetActive(false);
                envelope.Find("Postage").gameObject.SetActive(false);
                envelope.Find("Seal").gameObject.SetActive(false);
                envelope.Find("Address").gameObject.SetActive(false);
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
                Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/底图.png");
                if (paperSprite != null) { page.sprite = paperSprite; page.color = Color.white; page.preserveAspect = true; }
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
                Set(serialized, "envelopeArt", envelopeArt);
                Set(serialized, "envelopeClosedSprite", envelopeSprite);
                Set(serialized, "envelopeOpenSprite", envelopeOpenSprite);
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
                ApplyFormalPaperLayout(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            Debug.Log("[DayFlow] Created FirstDayLetterPanel.prefab. Existing prefabs and scenes were not modified.");
        }

        [MenuItem("Tools/Game Flow/Apply Formal First Day Letter Art")]
        public static void ApplyFormalArtToPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/背景.png");
                Sprite envelopeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/信封出现.png");
                Sprite openSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/信封自动打开.png");
                Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Func/信封/底图.png");
                Image blackout = root.transform.Find("Blackout")?.GetComponent<Image>();
                if (blackout != null && background != null) { blackout.sprite = background; blackout.color = Color.white; }
                Transform envelope = root.transform.Find("EnvelopeStage/Envelope");
                if (envelope != null)
                {
                    Image art = envelope.Find("FormalArt")?.GetComponent<Image>();
                    if (art == null)
                    {
                        art = Image(envelope, "FormalArt", Color.white);
                        Stretch(art.rectTransform);
                        art.raycastTarget = false;
                    }
                    art.sprite = envelopeSprite;
                    art.preserveAspect = true;
                    string[] oldParts = { "Shadow", "Face", "LeftFold", "RightFold", "Postage", "Seal", "Address" };
                    for (int i = 0; i < oldParts.Length; i++)
                    {
                        Transform oldPart = envelope.Find(oldParts[i]);
                        if (oldPart != null) oldPart.gameObject.SetActive(false);
                    }
                    SerializedObject panel = new SerializedObject(root.GetComponent<FirstDayLetterPanel>());
                    Set(panel, "envelopeArt", art);
                    Set(panel, "envelopeClosedSprite", envelopeSprite);
                    Set(panel, "envelopeOpenSprite", openSprite);
                    panel.ApplyModifiedPropertiesWithoutUndo();
                }
                Image page = root.transform.Find("LetterPaper/Page")?.GetComponent<Image>();
                if (page != null && paperSprite != null) { page.sprite = paperSprite; page.color = Color.white; page.preserveAspect = true; }
                ApplyFormalPaperLayout(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void ApplyFormalPaperLayout(GameObject root)
        {
            RectTransform paper = root.transform.Find("LetterPaper") as RectTransform;
            paper.sizeDelta = new Vector2(1260, 1260f * 2169f / 3203f);
            foreach (string name in new[] { "Shadow", "Title", "Rule" })
            {
                Transform old = paper.Find(name);
                if (old != default) old.gameObject.SetActive(false);
            }
            RectTransform scrollRoot = paper.Find("LetterScroll") as RectTransform;
            TopLeft(scrollRoot, new Vector2(120, -245), new Vector2(1020, 472));
            TopLeft(paper.Find("SignatureButton") as RectTransform, new Vector2(120, -730), new Vector2(1020, 48));
            TopLeft(paper.Find("ReadingHint") as RectTransform, new Vector2(120, -809), new Vector2(1020, 30));
        }

        [MenuItem("Tools/Game Flow/Apply Interactive First Day Letter")]
        public static void ApplyInteractiveLetterToPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                RectTransform paper = root.transform.Find("LetterPaper") as RectTransform;
                TMP_Text body = paper.Find("LetterScroll/Viewport/Body").GetComponent<TMP_Text>();
                body.font = Font;
                body.fontSize = 18f; // 参考稿12pt，按1.5换算为Canvas单位。
                body.enableAutoSizing = false;
                body.color = Color.black;
                body.lineSpacing = 4f;
                body.paragraphSpacing = 0f;
                body.richText = true;
                body.text = new FirstDayLetterDraft().BuildBody();
                body.raycastTarget = true;
                if (body.GetComponent<FirstDayLetterLinks>() == null) body.gameObject.AddComponent<FirstDayLetterLinks>();
                TopLeft(paper.Find("LetterScroll") as RectTransform, new Vector2(106, -207), new Vector2(1060, 470));
                TMP_Text signature = paper.Find("SignatureButton/Signature").GetComponent<TMP_Text>();
                signature.font = Font;
                signature.fontSize = 18f;
                signature.color = Color.black;
                signature.richText = true;
                signature.text = "—— 永远惦念你的<size=116.667%>叔叔</size>";
                TopLeft(paper.Find("SignatureButton") as RectTransform, new Vector2(106, -688), new Vector2(1060, 38));
                TMP_Text hint = paper.Find("ReadingHint").GetComponent<TMP_Text>();
                hint.font = Font;
                hint.fontSize = 18f;
                hint.color = new Color32(179, 0, 15, 255);
                TopLeft(hint.rectTransform, new Vector2(106, -727), new Vector2(1060, 32));

                Transform existing = root.transform.Find("NameEditor");
                CanvasGroup nameGroup;
                TMP_InputField input;
                Button confirm;
                Button cancel;
                if (existing == null)
                {
                    RectTransform overlay = Stage(root.transform, "NameEditor");
                    nameGroup = overlay.gameObject.AddComponent<CanvasGroup>();
                    Image shade = Image(overlay, "Shade", new Color(0, 0, 0, .55f));
                    Stretch(shade.rectTransform);
                    shade.raycastTarget = true;
                    RectTransform window = Box(overlay, "Window", Vector2.zero, new Vector2(580, 240));
                    Image face = window.gameObject.AddComponent<Image>();
                    face.color = new Color(.96f, .91f, .79f);
                    TMP_Text title = Text(window, "Title", "叔叔该如何称呼你？", 25, Color.black);
                    Center(title.rectTransform, new Vector2(0, 75), new Vector2(540, 45));
                    RectTransform inputRoot = Box(window, "NameInput", new Vector2(0, 12), new Vector2(500, 55));
                    Image inputFace = inputRoot.gameObject.AddComponent<Image>();
                    inputFace.color = new Color(1, 1, 1, .75f);
                    input = inputRoot.gameObject.AddComponent<TMP_InputField>();
                    RectTransform area = Child(inputRoot, "TextArea", typeof(RectMask2D)).GetComponent<RectTransform>();
                    Stretch(area); area.sizeDelta = new Vector2(-24, -8);
                    TMP_Text value = Text(area, "Text", "", 24, Color.black);
                    Stretch(value.rectTransform); value.alignment = TextAlignmentOptions.MidlineLeft;
                    TMP_Text placeholder = Text(area, "Placeholder", "输入姓名（最多16字）", 24, new Color(.5f, .45f, .4f));
                    Stretch(placeholder.rectTransform); placeholder.alignment = TextAlignmentOptions.MidlineLeft;
                    input.textViewport = area;
                    input.textComponent = (TextMeshProUGUI)value;
                    input.placeholder = placeholder;
                    input.targetGraphic = inputFace;
                    input.characterLimit = 16;
                    input.lineType = TMP_InputField.LineType.SingleLine;
                    confirm = ClickTarget(window, "Confirm");
                    Center(confirm.GetComponent<RectTransform>(), new Vector2(135, -72), new Vector2(180, 48));
                    confirm.transition = Selectable.Transition.ColorTint;
                    confirm.GetComponent<Image>().color = new Color(.83f, .72f, .52f);
                    TMP_Text confirmLabel = Text(confirm.transform, "Label", "确认", 24, Color.black);
                    Stretch(confirmLabel.rectTransform);
                    cancel = ClickTarget(window, "Cancel");
                    Center(cancel.GetComponent<RectTransform>(), new Vector2(-135, -72), new Vector2(180, 48));
                    cancel.GetComponent<Image>().color = new Color(.88f, .82f, .7f);
                    TMP_Text cancelLabel = Text(cancel.transform, "Label", "取消", 24, Color.black);
                    Stretch(cancelLabel.rectTransform);
                }
                else
                {
                    nameGroup = existing.GetComponent<CanvasGroup>();
                    input = existing.Find("Window/NameInput").GetComponent<TMP_InputField>();
                    confirm = existing.Find("Window/Confirm").GetComponent<Button>();
                    cancel = existing.Find("Window/Cancel").GetComponent<Button>();
                }
                nameGroup.gameObject.SetActive(false);
                var serialized = new SerializedObject(root.GetComponent<FirstDayLetterPanel>());
                Set(serialized, "nameEditor", nameGroup);
                Set(serialized, "nameInput", input);
                Set(serialized, "nameConfirmButton", confirm);
                Set(serialized, "nameCancelButton", cancel);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
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
