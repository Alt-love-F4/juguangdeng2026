using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;
using Spotlight.GameFlow;

namespace Spotlight.GameFlow.Editor
{
    public static class NoticePanelPrefabRebuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/NoticePanel.prefab";
        private const string ReferencePath = "Assets/Art/Gameplay/报纸通知（改后）.prefab";
        private const string FontPath = "Assets/Resources/Fonts/SILVER(for 报纸通知）.asset";
        private const string FontSourcePath = "Assets/Resources/Fonts/SILVER(for 报纸通知）.TTF";
        private const float WorldWidth = 40.96f;
        private const float WorldHeight = 23.04f;
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;

        [MenuItem("Tools/Game Flow/Rebuild Notice Panel (Newspaper UGUI)")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding NoticePanel.");

            GameObject reference = PrefabUtility.LoadPrefabContents(ReferencePath);
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/乐米曲奇方块体 SDF.asset") ?? EnsureFont();
                if (font == null) throw new InvalidOperationException("Could not create TMP font asset from " + FontSourcePath);

                // Load and update in place: keep the GUID and existing component/file IDs.
                bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
                var root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath)
                    : new GameObject("NoticePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
                try
                {
                    var rootRect = root.GetComponent<RectTransform>();
                    root.transform.localScale = Vector3.one;
                    root.transform.localPosition = Vector3.zero;
                    rootRect.localRotation = Quaternion.identity;
                    rootRect.pivot = new Vector2(.5f, .5f);
                    Stretch(rootRect);
                    var canvas = root.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 700;
                    var scaler = root.GetComponent<CanvasScaler>() ?? root.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = .5f;

                    Image blocker = MakeImage(root.transform, "InputBlocker", Vector2.zero, Vector2.one, new Color(.035f, .025f, .02f, .42f));
                    blocker.raycastTarget = true;

                    Transform layout = Child(root.transform, "NewspaperLayout");
                    var layoutRect = (RectTransform)layout;
                    layoutRect.anchorMin = layoutRect.anchorMax = new Vector2(.5f, .5f);
                    layoutRect.pivot = new Vector2(.5f, .5f);
                    layoutRect.anchoredPosition = Vector2.zero;
                    layoutRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
                    layoutRect.localScale = Vector3.one;

                    Transform artwork = root.transform.Find("NewspaperArtwork") ?? layout.Find("NewspaperArtwork");
                    if (artwork == null) artwork = Child(layout, "NewspaperArtwork");
                    artwork.SetParent(layout, false);
                    artwork.localScale = Vector3.one;
                    Stretch(artwork as RectTransform);
                    int order = 0;
                    foreach (var sr in reference.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (sr.sprite == null) continue;
                        Image image = MakeImage(artwork, sr.gameObject.name, Vector2.zero, Vector2.zero, Color.white);
                        image.sprite = sr.sprite;
                        image.raycastTarget = false;
                        var rect = image.rectTransform;
                        Vector3 p = sr.transform.position;
                        Vector3 s = sr.transform.localScale;
                        rect.anchorMin = rect.anchorMax = new Vector2(p.x / WorldWidth, p.y / WorldHeight);
                        rect.pivot = new Vector2(.5f, .5f);
                        rect.anchoredPosition = Vector2.zero;
                        rect.sizeDelta = new Vector2(sr.sprite.rect.width / sr.sprite.pixelsPerUnit * CanvasWidth / WorldWidth * s.x,
                            sr.sprite.rect.height / sr.sprite.pixelsPerUnit * CanvasHeight / WorldHeight * s.y);
                        image.transform.SetSiblingIndex(order++);
                        image.gameObject.SetActive(sr.gameObject.activeSelf && sr.enabled);
                        // These source layers are design overlays, not runtime content.
                        if (sr.name == "图层 2" || sr.name.StartsWith("开始一天") || sr.name.StartsWith("都来喝拿铁"))
                            image.gameObject.SetActive(false);
                        if (sr.name == "图层 1 副本 2") { image.sprite = null; image.color = Paper; }
                    }

                    Transform content = root.transform.Find("DynamicContent") ?? layout.Find("DynamicContent");
                    if (content == null) content = Child(layout, "DynamicContent");
                    content.SetParent(layout, false);
                    content.localScale = Vector3.one;
                    Stretch(content as RectTransform);

                    // Match the central page, leaving the masthead and right-hand printed column intact.
                    Image dateSurface = MakeImage(content, "DateSurface", Vector2.zero, Vector2.zero, Paper);
                    dateSurface.raycastTarget = false;
                    Position(dateSurface.rectTransform, new Vector2(1105, 857), new Vector2(650, 44));
                    dateSurface.transform.SetAsFirstSibling();
                    TMP_Text title = MakeText(content, "Title", "都来喝拿铁，沸点咖啡馆开始营业了！", new Vector2(995, 740), new Vector2(410, 92), font, 36, TextAlignmentOptions.TopLeft);
                    title.enableAutoSizing = true; title.fontSizeMin = 26; title.fontSizeMax = 36;
                    title.overflowMode = TextOverflowModes.Ellipsis;
                    TMP_Text time = MakeText(content, "Time", "本 日 快 报", new Vector2(1105, 857), new Vector2(650, 40), font, 28, TextAlignmentOptions.Center);
                    Image articleSurface = MakeImage(content, "ArticleSurface", Vector2.zero, Vector2.zero, Paper);
                    articleSurface.raycastTarget = false;
                    Position(articleSurface.rectTransform, new Vector2(995, 249), new Vector2(430, 204));
                    articleSurface.transform.SetAsFirstSibling();
                    var existingBody = content.Find("ArticleSurface/Viewport/Body");
                    if (existingBody != null) existingBody.SetParent(content, false);
                    TMP_Text body = MakeText(content, "Body", "", new Vector2(995, 249), new Vector2(410, 184), font, 30, TextAlignmentOptions.TopLeft);
                    body.overflowMode = TextOverflowModes.Overflow;
                    body.lineSpacing = 6f;
                    TMP_Text progressLabel = MakeText(content, "ProgressLabel", "", new Vector2(995, 128), new Vector2(410, 32), font, 22, TextAlignmentOptions.Left);
                    TMP_Text hint = MakeText(content, "Hint", "空格 / 回车：继续   Esc：关闭", new Vector2(995, 60), new Vector2(430, 28), font, 20, TextAlignmentOptions.Left);

                    Transform photo = Child(content, "PhotographSlot");
                    Position((RectTransform)photo, new Vector2(995, 521), new Vector2(410, 296));
                    var existingImage = content.Find("NoticeImage");
                    if (existingImage != null) existingImage.SetParent(photo, false);
                    RawImage noticeImage = MakeRawImage(photo, "NoticeImage", Vector2.zero, new Vector2(410, 296));
                    noticeImage.color = Color.white;
                    var fitter = noticeImage.GetComponent<AspectRatioFitter>() ?? noticeImage.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fitter.aspectRatio = 410f / 296f;
                    noticeImage.gameObject.SetActive(false);

                    Slider progress = MakeProgress(content, "ProgressBar", new Vector2(995, 102), new Vector2(410, 8));
                    progress.gameObject.SetActive(false);
                    Button confirm = MakeButton(content, "Confirm", "开始一天", new Vector2(1355, 70), new Vector2(190, 64), font);
                    confirm.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
                    TMP_Text confirmText = confirm.transform.Find("Label").GetComponent<TMP_Text>();
                    confirmText.color = Ink; confirmText.fontSize = 34;
                    confirm.targetGraphic = confirmText;
                    var colors = confirm.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.55f, 1.4f, 1.15f);
                    colors.pressedColor = new Color(.7f, .6f, .5f);
                    colors.disabledColor = new Color(1f, 1f, 1f, .5f);
                    confirm.colors = colors;
                    confirm.navigation = new Navigation { mode = Navigation.Mode.None };
                    Button cancel = MakeButton(root.transform, "Cancel", "", Vector2.zero, Vector2.zero, font);
                    cancel.gameObject.SetActive(false);

                    var panel = root.GetComponent<NoticePanel>() ?? root.AddComponent<NoticePanel>();
                    var serialized = new SerializedObject(panel);
                    Set(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
                    Set(serialized, "noticeImage", noticeImage);
                    Set(serialized, "titleText", title);
                    Set(serialized, "timeText", time);
                    Set(serialized, "bodyText", body);
                    Set(serialized, "progressLabel", progressLabel);
                    Set(serialized, "progressBar", progress);
                    Set(serialized, "hintText", hint);
                    Set(serialized, "confirmButton", confirm);
                    Set(serialized, "cancelButton", cancel);
                    Set(serialized, "newspaperLayout", layoutRect);
                    Set(serialized, "articleSurface", articleSurface.rectTransform);
                    Set(serialized, "confirmLabel", confirmText);
                    Set(serialized, "enterSeconds", .28f);
                    Set(serialized, "exitSeconds", .15f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    root.GetComponent<CanvasGroup>().alpha = 0f;
                    root.GetComponent<CanvasGroup>().blocksRaycasts = false;
                    root.GetComponent<CanvasGroup>().interactable = false;
                    ConfigureScrollableArticle(root);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    Debug.Log("[GameFlow] Rebuilt NoticePanel as newspaper UGUI/TMP layout.");
                }
                finally
                {
                    if (exists) PrefabUtility.UnloadPrefabContents(root);
                    else UnityEngine.Object.DestroyImmediate(root);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(reference); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Update the existing prefab without regenerating unrelated artwork or fonts.
        public static void ConfigureScrollableArticle(GameObject root)
        {
            var layout = root.transform.Find("NewspaperLayout");
            var content = layout.Find("DynamicContent");
            foreach (var image in layout.Find("NewspaperArtwork").GetComponentsInChildren<Image>(true))
                if (image.name.StartsWith("都来喝拿铁")) image.gameObject.SetActive(false);

            var surface = (RectTransform)content.Find("ArticleSurface");
            Position(surface, new Vector2(995, 417), new Vector2(430, 540));
            surface.SetAsLastSibling();
            surface.GetComponent<Image>().raycastTarget = true;
            var scroll = surface.GetComponent<ScrollRect>() ?? surface.gameObject.AddComponent<ScrollRect>();
            var viewport = (RectTransform)Child(surface, "Viewport");
            Stretch(viewport);
            viewport.offsetMin = new Vector2(10, 10);
            viewport.offsetMax = new Vector2(-10, -10);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var body = content.Find("Body") ?? viewport.Find("Body");
            body.SetParent(viewport, false);
            var rect = (RectTransform)body;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var text = body.GetComponent<TMP_Text>();
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = true;
            var fitter = body.GetComponent<ContentSizeFitter>() ?? body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            var hint = content.Find("Hint").GetComponent<TMP_Text>();
            hint.text = "滚轮 / 拖动：阅读   空格 / 回车：继续   Esc：关闭";
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 14;
            hint.fontSizeMax = 20;
            var serialized = new SerializedObject(root.GetComponent<NoticePanel>());
            Set(serialized, "bodyScroll", scroll);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_FontAsset EnsureFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null && existing.atlasTextures != null && existing.atlasTextures.Length > 0 && existing.atlasTextures[0] != null)
                return existing;
            if (existing != null)
                throw new InvalidOperationException("Existing newspaper font has no atlas; repair it without deleting its GUID: " + FontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (source == null) return null;
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (font == null) return null;
            AssetDatabase.CreateAsset(font, FontPath);
            if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
                if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        private static void Set(SerializedObject so, string name, UnityEngine.Object value) { so.FindProperty(name).objectReferenceValue = value; }
        private static void Set(SerializedObject so, string name, float value) { so.FindProperty(name).floatValue = value; }

        private static readonly Color Paper = new Color32(222, 218, 197, 255);
        private static readonly Color Ink = new Color32(99, 67, 54, 255);

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
            return child;
        }

        private static Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = Child(parent, name).gameObject;
            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>(); image.color = color;
            var rect = image.rectTransform; rect.anchorMin = min; rect.anchorMax = max;
            if (min != max) rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
        }
        private static RawImage MakeRawImage(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = Child(parent, name).gameObject;
            var image = go.GetComponent<RawImage>() ?? go.AddComponent<RawImage>();
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition = center; image.rectTransform.sizeDelta = size;
            image.raycastTarget = false; return image;
        }
        private static TMP_Text MakeText(Transform parent, string name, string value, Vector2 center, Vector2 size, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
        {
            var go = Child(parent, name).gameObject;
            var text = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>(); Position(text.rectTransform, center, size); text.font = font; text.fontSize = fontSize; text.text = value; text.color = Ink; text.alignment = alignment; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; return text;
        }
        private static Slider MakeProgress(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = Child(parent, name).gameObject; var s = go.GetComponent<Slider>() ?? go.AddComponent<Slider>(); Position(go.transform as RectTransform, center, size);
            var track = MakeImage(go.transform, "Track", Vector2.zero, Vector2.one, new Color(.39f, .26f, .21f, .2f)); track.raycastTarget = false;
            var fill = MakeImage(go.transform, "Fill", Vector2.zero, Vector2.one, Ink); fill.raycastTarget = false;
            s.fillRect = fill.rectTransform; s.interactable = false;
            return s;
        }
        private static Button MakeButton(Transform parent, string name, string label, Vector2 center, Vector2 size, TMP_FontAsset font)
        {
            var image = MakeImage(parent, name, Vector2.zero, Vector2.zero, new Color(.26f, .13f, .08f, .96f)); Position(image.rectTransform, center, size); var b = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            if (!string.IsNullOrEmpty(label)) { var text = MakeText(image.transform, "Label", label, Vector2.zero, size, font, 24, TextAlignmentOptions.Center); Stretch(text.rectTransform); text.color = new Color(.95f, .85f, .67f, 1f); }
            return b;
        }
        private static void Position(RectTransform rect, Vector2 center, Vector2 size) { rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(center.x - CanvasWidth * .5f, center.y - CanvasHeight * .5f); rect.sizeDelta = size; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
