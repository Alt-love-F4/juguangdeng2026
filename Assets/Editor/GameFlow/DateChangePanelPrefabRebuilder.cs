using System;
using Spotlight.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>
    /// 「小历书」DateChangePanel 布局（1920×1080 参考分辨率）。原地重建 Prefab 内容，保留 GUID 与外部引用。
    /// 素材来源：Tools/DayFlow/process_date_change_art.py。
    /// </summary>
    public static class DateChangePanelPrefabRebuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/DateChangePanel.prefab";
        private const string ArtRoot = "Assets/Resources/Art/Gameplay/DayFlow/DateChange/";
        private const string FontPath = "Assets/Resources/Fonts/CactusClassicalSerif SDF.asset";

        // Board art is 796×1462; plaque and page pad are measured in that pixel space.
        private const float BoardScale = .6156f;
        private static readonly Vector2 BoardSize = new Vector2(796f, 1462f) * BoardScale;
        private static readonly Vector2 PlaqueCenter = new Vector2(0f, (731f - 471f) * BoardScale);
        private static readonly Vector2 PageSize = new Vector2(400f, 380f);
        private static readonly Vector2 PageCenter = new Vector2(0f, -136f);
        private static readonly Vector2 CalendarPosition = new Vector2(-150f, 20f);
        private static readonly Vector2 SconcePosition = new Vector2(250f, 130f);
        private static readonly Vector2 FlamePosition = new Vector2(306f, 262f);

        private static readonly Color NightColor = new Color(.04f, .035f, .13f, .93f);
        private static readonly Color Ink = new Color(.29f, .15f, .09f);
        private static readonly Color Terracotta = new Color(.66f, .27f, .16f);
        private static readonly Color Cream = new Color(.99f, .93f, .81f);

        [MenuItem("Tools/Game Flow/Rebuild Date Change Panel (Almanac)")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the date panel.");
            ConfigureImporters();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("Missing font: " + FontPath);

            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("DateChangePanel", typeof(RectTransform));
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root, font);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[DayFlow] Rebuilt DateChangePanel (almanac layout).");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void Build(GameObject root, TMP_FontAsset font)
        {
            Stretch(root.GetComponent<RectTransform>());
            CanvasGroup group = Ensure<CanvasGroup>(root);
            Canvas canvas = Ensure<Canvas>(root);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;
            Ensure<GraphicRaycaster>(root);
            DateChangePanel panel = Ensure<DateChangePanel>(root);

            RawImage background = Child(root.transform, "Background", typeof(RawImage)).GetComponent<RawImage>();
            Stretch(background.rectTransform);
            background.texture = Load<Texture2D>("date_wall.png");
            background.raycastTarget = false;
            AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1536f / 1024f;

            Image blocker = Child(root.transform, "InputBlocker", typeof(Image)).GetComponent<Image>();
            Stretch(blocker.rectTransform);
            blocker.color = new Color(0f, 0f, 0f, 0f);

            RectTransform content = Child(root.transform, "Content").GetComponent<RectTransform>();
            Stretch(content);

            // Wall layer: morning light, calendar, sconce.
            Image windowLight = Sprite(content, "WindowLight", "date_window_light.png", new Vector2(-430f, 170f),
                new Vector2(1056f, 880f), new Color(1f, .9f, .72f, .5f));

            RectTransform calendar = Child(content, "Calendar").GetComponent<RectTransform>();
            Place(calendar, CalendarPosition, BoardSize);
            Sprite(calendar, "Board", "date_calendar_board.png", Vector2.zero, BoardSize, Color.white);
            TMP_Text header = Text(calendar, "Header", "王国纪元 670年", font, PlaqueCenter + new Vector2(0f, 2f),
                new Vector2(370f, 70f), 34f, Ink);
            header.characterSpacing = 4f;

            RectTransform newPage = Page(calendar, "NewPage", PageCenter, new Vector2(.5f, .5f));
            PageTexts(newPage, font, out TMP_Text newMonth, out TMP_Text newLatin, out TMP_Text newDay);
            Image stamp = Sprite(newPage, "Stamp", "date_stamp.png", new Vector2(132f, -122f), new Vector2(124f, 124f),
                new Color(1f, 1f, 1f, .86f));
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            // Pivot on the perforated top edge so the leaf tears from the clip bar.
            RectTransform oldPage = Page(calendar, "OldPage", PageCenter + new Vector2(0f, PageSize.y * .5f),
                new Vector2(.5f, 1f));
            CanvasGroup oldGroup = oldPage.gameObject.AddComponent<CanvasGroup>();
            oldGroup.blocksRaycasts = false;
            Image shadow = Sprite(oldPage, "Shadow", "date_page_shadow.png", new Vector2(10f, -12f),
                PageSize + new Vector2(40f, 40f), new Color(1f, 1f, 1f, .32f));
            shadow.type = UnityEngine.UI.Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 2.2f;
            shadow.transform.SetAsFirstSibling();
            PageTexts(oldPage, font, out TMP_Text oldMonth, out TMP_Text oldLatin, out TMP_Text oldDay);

            Sprite(content, "CandleSconce", "date_candle_sconce.png", SconcePosition,
                new Vector2(239f, 320f), Color.white);

            // Night tint sits over the whole wall; the candle light and motes glow above it.
            Image nightTint = Sprite(content, "NightTint", "date_tint_vignette.png", Vector2.zero, Vector2.zero,
                NightColor);
            Stretch(nightTint.rectTransform);

            Image glow = Sprite(content, "CandleGlow", "date_glow.png", FlamePosition + new Vector2(0f, 24f),
                new Vector2(560f, 560f), new Color(1f, .8f, .52f, .58f));
            Image flame = Sprite(content, "Flame", "date_flame.png", FlamePosition, new Vector2(30f, 54f), Color.white);
            flame.rectTransform.pivot = new Vector2(.5f, .1f);
            flame.rectTransform.anchoredPosition = FlamePosition;
            Image smoke = Sprite(content, "Smoke", "date_smoke.png", FlamePosition + new Vector2(0f, 26f),
                new Vector2(60f, 120f), new Color(1f, 1f, 1f, .75f));
            smoke.rectTransform.pivot = new Vector2(.5f, 0f);
            smoke.rectTransform.anchoredPosition = FlamePosition + new Vector2(0f, 26f);

            RectTransform moteRoot = Child(content, "Motes").GetComponent<RectTransform>();
            Stretch(moteRoot);
            var random = new System.Random(17);
            RectTransform[] motes = new RectTransform[12];
            for (int i = 0; i < motes.Length; i++)
            {
                float size = 8f + (float)random.NextDouble() * 10f;
                Vector2 position = new Vector2(-720f + (float)random.NextDouble() * 560f, -160f + (float)random.NextDouble() * 460f);
                motes[i] = Sprite(moteRoot, "Mote_" + i.ToString("00"), "date_mote.png", position,
                    new Vector2(size, size), new Color(1f, .95f, .82f, 0f)).rectTransform;
            }

            // Caption on the dark wainscot, over a soft shade for contrast.
            Image captionShade = Sprite(content, "CaptionShade", "date_glow.png", new Vector2(590f, -184f),
                new Vector2(820f, 300f), new Color(.14f, .06f, .03f, .62f));
            TMP_Text title = Text(content, "Title", "新的一天", font, new Vector2(590f, -150f), new Vector2(700f, 110f),
                80f, Cream);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 18f;
            TMP_Text detail = Text(content, "Detail", "Un nouveau jour", font, new Vector2(590f, -236f),
                new Vector2(700f, 44f), 30f, new Color(.96f, .8f, .6f));
            detail.fontStyle = FontStyles.Italic;
            detail.characterSpacing = 6f;

            Image continueImage = Child(root.transform, "Continue", typeof(Image)).GetComponent<Image>();
            Stretch(continueImage.rectTransform);
            continueImage.color = new Color(0f, 0f, 0f, 0f);
            Button continueButton = continueImage.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = continueImage;
            continueButton.gameObject.SetActive(false);

            var so = new SerializedObject(panel);
            Set(so, "canvasGroup", group);
            Set(so, "content", content);
            Set(so, "background", background);
            Set(so, "titleText", title);
            Set(so, "detailText", detail);
            Set(so, "continueButton", continueButton);
            Set(so, "calendar", calendar);
            Set(so, "headerText", header);
            Set(so, "newPage", newPage);
            Set(so, "newMonthText", newMonth);
            Set(so, "newMonthLatinText", newLatin);
            Set(so, "newDayText", newDay);
            Set(so, "oldPage", oldPage);
            Set(so, "oldPageGroup", oldGroup);
            Set(so, "oldMonthText", oldMonth);
            Set(so, "oldMonthLatinText", oldLatin);
            Set(so, "oldDayText", oldDay);
            Set(so, "stamp", stamp);
            Set(so, "nightTint", nightTint);
            Set(so, "windowLight", windowLight);
            Set(so, "flame", flame.rectTransform);
            Set(so, "candleGlow", glow);
            Set(so, "smoke", smoke);
            Set(so, "titleCaption", title);
            Set(so, "captionShade", captionShade);
            SerializedProperty moteList = so.FindProperty("motes");
            moteList.arraySize = motes.Length;
            for (int i = 0; i < motes.Length; i++) moteList.GetArrayElementAtIndex(i).objectReferenceValue = motes[i];
            // Linear colour space makes UI overlays read lighter, so the night is pushed deep.
            so.FindProperty("nightColor").colorValue = NightColor;
            so.FindProperty("dawnColor").colorValue = new Color(1f, .6f, .32f, .3f);
            so.FindProperty("windowLightAlpha").floatValue = .55f;
            so.FindProperty("automaticHoldSeconds").floatValue = 1.1f;
            so.FindProperty("enterSeconds").floatValue = .45f;
            so.FindProperty("exitSeconds").floatValue = .3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Page(Transform parent, string name, Vector2 position, Vector2 pivot)
        {
            Image page = Sprite(parent, name, "date_calendar_page.png", Vector2.zero, PageSize, Color.white);
            page.type = UnityEngine.UI.Image.Type.Sliced;
            page.pixelsPerUnitMultiplier = 2.4f;
            RectTransform rect = page.rectTransform;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void PageTexts(RectTransform page, TMP_FontAsset font,
            out TMP_Text month, out TMP_Text latin, out TMP_Text day)
        {
            // Children anchor to the page centre whatever the page pivot is.
            month = Text(page, "Month", "十月", font, new Vector2(0f, 120f), new Vector2(340f, 56f), 40f, Terracotta);
            month.characterSpacing = 10f;
            latin = Text(page, "MonthLatin", "OCTOBRE", font, new Vector2(0f, 80f), new Vector2(340f, 30f), 18f,
                new Color(.55f, .36f, .26f));
            latin.characterSpacing = 14f;
            day = Text(page, "Day", "6", font, new Vector2(0f, -42f), new Vector2(340f, 220f), 196f, Ink);
            day.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void ConfigureImporters()
        {
            string[] sprites =
            {
                "date_calendar_board.png", "date_calendar_page.png", "date_page_shadow.png", "date_candle_sconce.png",
                "date_stamp.png", "date_flame.png", "date_glow.png", "date_mote.png", "date_window_light.png",
                "date_smoke.png", "date_tint_vignette.png", "date_wall.png"
            };
            foreach (string file in sprites)
            {
                string path = ArtRoot + file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing art: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                // Soft gradients band badly under block compression.
                bool gradient = file is "date_glow.png" or "date_tint_vignette.png" or "date_mote.png"
                    or "date_window_light.png" or "date_smoke.png" or "date_flame.png" or "date_page_shadow.png";
                importer.textureCompression = gradient ? TextureImporterCompression.Uncompressed
                    : TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
                importer.spriteBorder = file == "date_calendar_page.png" ? new Vector4(120f, 120f, 120f, 135f)
                    : file == "date_page_shadow.png" ? new Vector4(80f, 80f, 80f, 80f) : Vector4.zero;
                importer.SaveAndReimport();
            }
        }

        private static T Load<T>(string file) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(ArtRoot + file);
            if (asset == null) throw new InvalidOperationException("Missing art: " + ArtRoot + file);
            return asset;
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

        private static GameObject Child(Transform parent, string name, params Type[] components)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            foreach (Type type in components) go.AddComponent(type);
            return go;
        }

        private static Image Sprite(Transform parent, string name, string file, Vector2 position, Vector2 size, Color color)
        {
            Image image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            image.sprite = Load<Sprite>(file);
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, position, size);
            return image;
        }

        private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font,
            Vector2 position, Vector2 size, float fontSize, Color color)
        {
            TextMeshProUGUI text = Child(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            Place(text.rectTransform, position, size);
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing serialized field: " + name);
            property.objectReferenceValue = value;
        }
    }
}
