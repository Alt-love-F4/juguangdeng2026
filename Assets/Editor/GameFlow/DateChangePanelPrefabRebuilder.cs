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
    /// 将美术日历 Prefab 的 SpriteRenderer 构图转换为 Canvas UI；原地重建，保留面板 GUID。
    /// 年份、月份和日期继续由 DateChangePanel 动态填充。
    /// </summary>
    public static class DateChangePanelPrefabRebuilder
    {
        private const string PrefabPath = "Assets/Resources/Art/UIPrefab/DateChangePanel.prefab";
        private const string SourcePath = "Assets/Art/Func/日历.prefab";
        private const string FontPath = "Assets/Resources/Fonts/CactusClassicalSerif SDF.asset";
        private static readonly Color Ink = new Color(67f / 255f, 29f / 255f, 15f / 255f);
        private static readonly Color NightColor = new Color(.04f, .035f, .13f, .78f);

        [MenuItem("Tools/Game Flow/Rebuild Date Change Panel (Calendar Artwork)")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before rebuilding the date panel.");
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new InvalidOperationException("Missing calendar artwork: " + SourcePath);
            // Validate before touching the target; source assets and import settings stay unchanged.
            foreach (string name in new[] { "背景", "底板", "底页", "王国纪元     670年", "切页", "烛台" })
                Source(source, name);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("Missing font: " + FontPath);

            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("DateChangePanel", typeof(RectTransform));
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root, source, font);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[DayFlow] Rebuilt DateChangePanel from " + SourcePath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void Build(GameObject root, GameObject source, TMP_FontAsset font)
        {
            Stretch(root.GetComponent<RectTransform>());
            CanvasGroup group = Ensure<CanvasGroup>(root);
            Canvas canvas = Ensure<Canvas>(root);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 700;
            Ensure<GraphicRaycaster>(root);
            DateChangePanel panel = Ensure<DateChangePanel>(root);

            Image blocker = Child(root.transform, "InputBlocker", typeof(Image)).GetComponent<Image>();
            Stretch(blocker.rectTransform);
            blocker.color = Color.clear;

            RectTransform content = Child(root.transform, "Content").GetComponent<RectTransform>();
            Stretch(content);
            // One shared artwork coordinate system: fill the screen without distorting or separating layers.
            SpriteRenderer wall = Source(source, "背景");
            Vector2 wallSize = (Vector2)wall.sprite.bounds.size;
            float scale = 1080f / wallSize.y;
            RectTransform artwork = Child(content, "Artwork").GetComponent<RectTransform>();
            Place(artwork, Vector2.zero, wallSize * scale);
            DateChangeArtworkLayout layout = content.gameObject.AddComponent<DateChangeArtworkLayout>();
            var layoutSettings = new SerializedObject(layout);
            layoutSettings.FindProperty("artwork").objectReferenceValue = artwork;
            layoutSettings.FindProperty("referenceSize").vector2Value = wallSize * scale;
            layoutSettings.ApplyModifiedPropertiesWithoutUndo();

            RawImage background = Child(artwork, "Background", typeof(RawImage)).GetComponent<RawImage>();
            Stretch(background.rectTransform);
            background.texture = wall.sprite.texture;
            background.color = wall.color;
            Rect uv = wall.sprite.textureRect;
            background.uvRect = new Rect(uv.x / wall.sprite.texture.width, uv.y / wall.sprite.texture.height,
                uv.width / wall.sprite.texture.width, uv.height / wall.sprite.texture.height);
            background.raycastTarget = false;

            // The anchor uses normalized source coordinates; children retain the source's relative dimensions.
            SpriteRenderer board = Source(source, "底板");
            Vector2 boardPosition = (Vector2)(board.transform.localPosition - wall.transform.localPosition);
            RectTransform calendar = Child(artwork, "Calendar").GetComponent<RectTransform>();
            Place(calendar, Vector2.zero, (Vector2)board.sprite.bounds.size * scale);
            calendar.anchorMin = calendar.anchorMax = new Vector2(.5f + boardPosition.x / wallSize.x,
                .5f + boardPosition.y / wallSize.y);
            ArtworkImage(calendar, "Board", board, board, scale);
            ArtworkImage(calendar, "BasePage", Source(source, "底页"), board, scale);

            SpriteRenderer headerArt = Source(source, "王国纪元     670年");
            TMP_Text header = Text(calendar, "Header", "王国纪元 670年", font,
                (Vector2)(headerArt.transform.localPosition - board.transform.localPosition) * scale,
                new Vector2(620f, 90f) * scale / 100f, 53f * scale / 100f, Ink);

            SpriteRenderer pageArt = Source(source, "切页");
            Image newPageImage = ArtworkImage(calendar, "NewPage", pageArt, board, scale);
            RectTransform newPage = newPageImage.rectTransform;
            PageTexts(newPage, font, scale / 100f, "7", out TMP_Text newMonth, out TMP_Text newLatin, out TMP_Text newDay);
            Image oldPageImage = ArtworkImage(calendar, "OldPage", pageArt, board, scale);
            RectTransform oldPage = oldPageImage.rectTransform;
            oldPage.pivot = new Vector2(.5f, 1f);
            oldPage.anchoredPosition += Vector2.up * oldPage.sizeDelta.y * .5f;
            CanvasGroup oldGroup = oldPage.gameObject.AddComponent<CanvasGroup>();
            oldGroup.blocksRaycasts = false;
            PageTexts(oldPage, font, scale / 100f, "6", out TMP_Text oldMonth, out TMP_Text oldLatin, out TMP_Text oldDay);

            SpriteRenderer sconce = Source(source, "烛台");
            Image candle = ArtworkImage(artwork, "CandleSconce", sconce, wall, scale);
            // The entire artwork scales uniformly; the candle anchor follows the wall.
            Vector2 candlePosition = (Vector2)(sconce.transform.localPosition - wall.transform.localPosition);
            candle.rectTransform.anchorMin = candle.rectTransform.anchorMax = new Vector2(
                .5f + candlePosition.x / wallSize.x, .5f + candlePosition.y / wallSize.y);
            candle.rectTransform.anchoredPosition = Vector2.zero;

            // A subtle night-to-day wash keeps the existing pacing without bringing back the old art layers.
            Image nightTint = Child(artwork, "NightTint", typeof(Image)).GetComponent<Image>();
            Stretch(nightTint.rectTransform);
            nightTint.color = NightColor;
            nightTint.raycastTarget = false;

            Image continueImage = Child(root.transform, "Continue", typeof(Image)).GetComponent<Image>();
            Stretch(continueImage.rectTransform);
            continueImage.color = Color.clear;
            Button continueButton = continueImage.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = continueImage;
            continueButton.gameObject.SetActive(false);

            // Clear bindings to replaced visuals; runtime treats these optional effects as absent.
            var so = new SerializedObject(panel);
            foreach (string field in new[] { "titleText", "detailText", "stamp", "windowLight", "flame",
                "candleGlow", "smoke", "titleCaption", "captionShade" }) Set(so, field, null);
            Set(so, "canvasGroup", group);
            Set(so, "content", content);
            Set(so, "background", background);
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
            Set(so, "nightTint", nightTint);
            so.FindProperty("motes").arraySize = 0;
            so.FindProperty("nightColor").colorValue = NightColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SpriteRenderer Source(GameObject source, string name)
        {
            Transform child = source.transform.Find(name);
            if (child == null || !child.TryGetComponent(out SpriteRenderer renderer) || renderer.sprite == null)
                throw new InvalidOperationException("Missing calendar artwork layer: " + name);
            return renderer;
        }

        private static Image ArtworkImage(Transform parent, string name, SpriteRenderer source,
            SpriteRenderer origin, float scale)
        {
            Image image = Child(parent, name, typeof(Image)).GetComponent<Image>();
            image.sprite = source.sprite;
            image.color = source.color;
            image.raycastTarget = false;
            Place(image.rectTransform, (Vector2)(source.transform.localPosition - origin.transform.localPosition) * scale,
                (Vector2)source.sprite.bounds.size * scale);
            return image;
        }

        private static void PageTexts(RectTransform page, TMP_FontAsset font, float scale, string initialDay,
            out TMP_Text month, out TMP_Text latin, out TMP_Text day)
        {
            // Dynamic equivalent of the source's baked "7 / October 十月" artwork.
            day = Text(page, "Day", initialDay, font, new Vector2(10f, 65f) * scale,
                new Vector2(760f, 470f) * scale, 470f * scale, Ink);
            day.enableAutoSizing = true;
            day.fontSizeMin = 280f * scale;
            day.fontSizeMax = 470f * scale;
            latin = Text(page, "MonthLatin", "OCTOBRE", font, new Vector2(-125f, -235f) * scale,
                new Vector2(440f, 70f) * scale, 50f * scale, Ink);
            month = Text(page, "Month", "十月", font, new Vector2(235f, -235f) * scale,
                new Vector2(240f, 70f) * scale, 50f * scale, Ink);
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
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
