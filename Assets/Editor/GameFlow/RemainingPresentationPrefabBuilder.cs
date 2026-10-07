#if UNITY_EDITOR
using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Creates only the three missing presentation prefabs; artists can replace them later.</summary>
    [InitializeOnLoad]
    public static class RemainingPresentationPrefabBuilder
    {
        private const string Root = "Assets/Resources/Art/UIPrefab/";
        private const string FontPath = "Assets/Resources/Fonts/SimHei SDF.asset";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        private static readonly Color Gold = new Color(1f, .88f, .66f);
        private static readonly Color Surface = new Color(.08f, .06f, .055f, 1f);
        private static readonly Color ButtonColor = new Color(.22f, .14f, .09f, .98f);

        static RemainingPresentationPrefabBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) CreateMissing();
            };
        }

        [MenuItem("Tools/Game Flow/Create Missing Reading Notice CutScene Assets")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "ReadingPanel.prefab") != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(Root + "NoticePanel.prefab") != null
                && AssetDatabase.LoadAssetAtPath<GameObject>(Root + "CutScenePanel.prefab") != null) return;
            if (Font == null) throw new InvalidOperationException("Missing SimHei SDF font.");
            EnsureFolder("Assets/Resources/Art");
            EnsureFolder(Root.TrimEnd('/'));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "ReadingPanel.prefab") == null) CreateReading();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "NoticePanel.prefab") == null) CreateNotice();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "CutScenePanel.prefab") == null) CreateCutScene();
            AssetDatabase.SaveAssets();
        }

        private static void CreateReading()
        {
            GameObject root = RootObject("ReadingPanel");
            try
            {
                Image shade = Img(root.transform, "InputBlocker", Vector2.zero, Vector2.one, new Color(.02f, .015f, .012f, .56f), true);
                GameObject box = Child(root.transform, "ReadingBox");
                Rect(box.GetComponent<RectTransform>(), new Vector2(.035f, .13f), new Vector2(.715f, .89f));
                Img(box.transform, "Surface", Vector2.zero, Vector2.one, Surface, false);
                RawImage image = FittedRaw(box.transform, "PageImage", new Vector2(.54f, .21f), new Vector2(.95f, .80f));
                image.color = new Color(1f, 1f, 1f, .95f);
                TMP_Text title = Txt(box.transform, "Title", "阅读", new Vector2(.055f, .82f), new Vector2(.50f, .95f), 30f, Gold);
                TMP_Text speaker = Txt(box.transform, "Speaker", "", new Vector2(.17f, .71f), new Vector2(.50f, .82f), 22f, new Color(1f, .78f, .5f));
                Image avatar = Img(box.transform, "SpeakerPortrait", new Vector2(.055f, .70f), new Vector2(.15f, .82f), Color.white, false);
                avatar.preserveAspect = true;
                TMP_Text text = Txt(box.transform, "ReadingText", "阅读内容", new Vector2(.055f, .22f), new Vector2(.50f, .72f), 25f, Color.white);
                text.overflowMode = TextOverflowModes.Page;
                TMP_Text progress = Txt(box.transform, "Progress", "页 1 / 1", new Vector2(.055f, .14f), new Vector2(.53f, .19f), 17f, new Color(.75f, .7f, .65f));
                TMP_Text hint = Txt(box.transform, "Hint", "空格 / 回车 / 点击继续翻页", new Vector2(.055f, .06f), new Vector2(.53f, .12f), 17f, new Color(.72f, .68f, .62f));
                Button back = Btn(box.transform, "Back", "< 上一页", new Vector2(.56f, .05f), new Vector2(.73f, .15f));
                Button next = Btn(box.transform, "Continue", "继续  >", new Vector2(.75f, .05f), new Vector2(.94f, .15f));
                Button cancel = Btn(root.transform, "Cancel", "Esc · 退出", new Vector2(.57f, .91f), new Vector2(.71f, .975f));
                ReadingPanel panel = root.AddComponent<ReadingPanel>();
                SerializedObject so = new SerializedObject(panel);
                Set(so, "canvasGroup", root.GetComponent<CanvasGroup>()); Set(so, "pageImage", image);
                Set(so, "titleText", title); Set(so, "speakerText", speaker); Set(so, "speakerPortrait", avatar); Set(so, "readingText", text); Set(so, "progressText", progress); Set(so, "hintText", hint);
                Set(so, "backButton", back); Set(so, "continueButton", next); Set(so, "cancelButton", cancel); so.ApplyModifiedPropertiesWithoutUndo();
                Save(root, "ReadingPanel");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateNotice()
        {
            GameObject root = RootObject("NoticePanel");
            try
            {
                Img(root.transform, "InputBlocker", Vector2.zero, Vector2.one, new Color(.02f, .015f, .012f, .64f), true);
                GameObject card = Child(root.transform, "NoticeCard");
                Rect(card.GetComponent<RectTransform>(), new Vector2(.13f, .20f), new Vector2(.70f, .83f));
                Img(card.transform, "Surface", Vector2.zero, Vector2.one, Surface, false);
                RawImage image = FittedRaw(card.transform, "NoticeImage", new Vector2(.57f, .30f), new Vector2(.94f, .65f));
                TMP_Text title = Txt(card.transform, "Title", "事件通知", new Vector2(.07f, .76f), new Vector2(.93f, .91f), 30f, Gold);
                TMP_Text time = Txt(card.transform, "Time", "", new Vector2(.07f, .67f), new Vector2(.53f, .76f), 18f, new Color(.78f, .7f, .62f));
                TMP_Text body = Txt(card.transform, "Body", "事件信息", new Vector2(.07f, .38f), new Vector2(.53f, .67f), 24f, Color.white);
                body.enableAutoSizing = true; body.fontSizeMin = 16f; body.fontSizeMax = 24f;
                TMP_Text progressLabel = Txt(card.transform, "ProgressLabel", "进度变化", new Vector2(.07f, .27f), new Vector2(.93f, .36f), 18f, new Color(.9f, .76f, .48f));
                Slider progress = MakeSlider(card.transform, "Progress", new Vector2(.07f, .21f), new Vector2(.93f, .24f));
                TMP_Text hint = Txt(card.transform, "Hint", "空格 / 回车 / 点击确认关闭", new Vector2(.07f, .08f), new Vector2(.54f, .16f), 17f, new Color(.72f, .68f, .62f));
                Button confirm = Btn(card.transform, "Confirm", "确认  >", new Vector2(.72f, .06f), new Vector2(.94f, .18f));
                Button cancel = Btn(root.transform, "Cancel", "Esc · 退出", new Vector2(.57f, .91f), new Vector2(.71f, .975f));
                NoticePanel panel = root.AddComponent<NoticePanel>();
                SerializedObject so = new SerializedObject(panel);
                Set(so, "canvasGroup", root.GetComponent<CanvasGroup>()); Set(so, "noticeImage", image);
                Set(so, "titleText", title); Set(so, "timeText", time); Set(so, "bodyText", body); Set(so, "progressLabel", progressLabel); Set(so, "progressBar", progress); Set(so, "hintText", hint);
                Set(so, "confirmButton", confirm); Set(so, "cancelButton", cancel); so.ApplyModifiedPropertiesWithoutUndo();
                Save(root, "NoticePanel");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateCutScene()
        {
            GameObject root = RootObject("CutScenePanel");
            try
            {
                Img(root.transform, "BlackBackground", Vector2.zero, Vector2.one, Color.black, true);
                RawImage video = Raw(root.transform, "Video", Vector2.zero, Vector2.one, null);
                video.raycastTarget = false;
                var fitter = video.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 16f / 9f;
                Img(root.transform, "Overlay", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, .18f), false);
                TMP_Text title = Txt(root.transform, "Title", "过场", new Vector2(.045f, .91f), new Vector2(.72f, .975f), 26f, Gold);
                GameObject placeholderGo = Child(root.transform, "Placeholder");
                Rect(placeholderGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                TMP_Text placeholder = Txt(placeholderGo.transform, "Text", "视频占位", new Vector2(.08f, .35f), new Vector2(.70f, .70f), 28f, new Color(.82f, .82f, .82f));
                placeholder.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
                TMP_Text skip = Txt(root.transform, "SkipText", "长按 Enter 跳过", new Vector2(.45f, .07f), new Vector2(.68f, .13f), 19f, Color.white);
                Slider progress = MakeSlider(root.transform, "SkipProgress", new Vector2(.45f, .035f), new Vector2(.66f, .06f));
                TMP_Text percent = Txt(root.transform, "SkipPercent", "0%", new Vector2(.67f, .025f), new Vector2(.73f, .065f), 17f, Color.white);
                Button cancel = Btn(root.transform, "Cancel", "Esc · 取消", new Vector2(.57f, .91f), new Vector2(.71f, .975f));
                VideoPlayer player = root.AddComponent<VideoPlayer>();
                player.playOnAwake = false; player.renderMode = VideoRenderMode.RenderTexture; player.isLooping = false;
                CutScenePanel panel = root.AddComponent<CutScenePanel>();
                SerializedObject so = new SerializedObject(panel);
                Set(so, "canvasGroup", root.GetComponent<CanvasGroup>()); Set(so, "videoImage", video); Set(so, "titleText", title); Set(so, "placeholderText", placeholder);
                Set(so, "skipText", skip); Set(so, "skipPercentText", percent); Set(so, "skipProgress", progress); Set(so, "cancelButton", cancel); Set(so, "videoPlayer", player); so.ApplyModifiedPropertiesWithoutUndo();
                Save(root, "CutScenePanel");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject RootObject(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas), typeof(GraphicRaycaster));
            Rect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            Canvas canvas = root.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 800;
            root.GetComponent<CanvasGroup>().alpha = 1f;
            return root;
        }

        private static void Save(GameObject root, string name) => PrefabUtility.SaveAsPrefabAsset(root, Root + name + ".prefab");
        private static void EnsureFolder(string path) { if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(path.Substring(0, path.LastIndexOf('/')), path.Substring(path.LastIndexOf('/') + 1)); }
        private static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go;
        }
        private static Image Img(Transform parent, string name, Vector2 min, Vector2 max, Color color, bool raycast)
        {
            var go = Child(parent, name); var image = go.AddComponent<Image>(); Rect(image.rectTransform, min, max); image.color = color; image.raycastTarget = raycast; return image;
        }
        private static RawImage Raw(Transform parent, string name, Vector2 min, Vector2 max, Texture texture)
        {
            var go = Child(parent, name); var image = go.AddComponent<RawImage>(); Rect(image.rectTransform, min, max); image.texture = texture; image.raycastTarget = false; return image;
        }
        private static RawImage FittedRaw(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject slot = Child(parent, name + "Slot"); Rect(slot.GetComponent<RectTransform>(), min, max);
            RawImage image = Raw(slot.transform, name, Vector2.zero, Vector2.one, null);
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            return image;
        }
        private static TMP_Text Txt(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, Color color)
        {
            var go = Child(parent, name); var text = go.AddComponent<TextMeshProUGUI>(); Rect(text.rectTransform, min, max); text.font = Font; text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false; text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal; return text;
        }
        private static Button Btn(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            Image image = Img(parent, name, min, max, ButtonColor, true); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            TMP_Text text = Txt(image.transform, "Label", label, new Vector2(.03f, .03f), new Vector2(.97f, .97f), 18f, Gold); text.alignment = TextAlignmentOptions.Center; return button;
        }
        private static Slider MakeSlider(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject go = Child(parent, name); RectTransform rect = go.GetComponent<RectTransform>(); Rect(rect, min, max);
            Image bg = go.AddComponent<Image>(); bg.color = new Color(.22f, .18f, .15f, .9f); bg.raycastTarget = false;
            GameObject fill = Child(go.transform, "Fill"); Image fillImage = fill.AddComponent<Image>(); Rect(fillImage.rectTransform, Vector2.zero, Vector2.one); fillImage.color = new Color(1f, .66f, .3f, .95f); fillImage.raycastTarget = false;
            Slider slider = go.AddComponent<Slider>(); slider.fillRect = fill.GetComponent<RectTransform>(); slider.targetGraphic = bg; slider.direction = Slider.Direction.LeftToRight; slider.value = 0f; slider.interactable = false; return slider;
        }
        private static void Rect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
#endif
