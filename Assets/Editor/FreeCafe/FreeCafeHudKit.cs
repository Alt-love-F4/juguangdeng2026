using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Editor
{
    /// <summary>
    /// Shared look for the cafe HUDs: the supplied counter-PSD paper frame, plaque buttons and
    /// ornaments, with Source Han Serif headings and Source Han Sans body text.
    /// Sliced sprites use pixelsPerUnitMultiplier so their hand-drawn ornaments shrink with the
    /// element instead of being cut or squashed.
    /// </summary>
    internal static class FreeCafeHudKit
    {
        public const string Art = "Assets/Art/Gameplay/咖啡馆/台面ui/";
        public static readonly Color Ink = new Color(.27f, .15f, .09f);
        public static readonly Color Muted = new Color(.47f, .33f, .23f);
        public static readonly Color Accent = new Color(.55f, .21f, .10f);
        public static readonly Color Cream = new Color(.97f, .91f, .80f);
        public static readonly Color CreamMuted = new Color(.88f, .78f, .64f);
        public static TMP_FontAsset Title => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Map/SourceHanSerifCN-Heavy-4 SDF.asset");
        public static TMP_FontAsset Body => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Map/SourceHanSansSC-Regular-2 SDF.asset");

        // Native source heights for the sliced sprites (used to scale their borders).
        private const float PlaqueHeight = 148, PaperHeight = 339, BandHeight = 104;

        public static Sprite Sprite(string name)
        {
            string path = Art + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            // Unity border order: left, bottom, right, top.
            Vector4 border = name == "选中" || name == "未选中" ? new Vector4(72, 0, 72, 0)
                : name == "物品详情框" ? new Vector4(100, 145, 150, 100)
                : name == "提示框" ? new Vector4(40, 0, 40, 0) : Vector4.zero;
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static RectTransform Root(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>Places a rect by its centre relative to an anchor point (1920×1080 reference).</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        }

        public static Image Picture(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false; image.preserveAspect = true;
            Place(image.rectTransform, anchor, position, size);
            return image;
        }

        /// <summary>The counter PSD's paper card; ornaments are drawn at 1/scale of their source size.</summary>
        public static RectTransform Paper(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float scale = 2.3f)
        {
            var image = Picture(parent, name, Sprite("物品详情框"), anchor, position, size);
            image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = scale;
            return image.rectTransform;
        }

        /// <summary>Dark ribbon strip used for the hint line.</summary>
        public static RectTransform Band(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = Picture(parent, name, Sprite("提示框"), anchor, position, size);
            image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = BandHeight / size.y;
            return image.rectTransform;
        }

        /// <summary>A procedurally drawn rounded plate for small chips and frames.</summary>
        public static CafeHudSurface Plate(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color fill, Color border, float radius = 10)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CafeHudSurface));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var surface = go.GetComponent<CafeHudSurface>();
            surface.color = fill; surface.border = border; surface.radius = radius; surface.borderWidth = 2;
            surface.shadow = false; surface.raycastTarget = false;
            Place(surface.rectTransform, anchor, position, size);
            return surface;
        }

        public static TMP_Text Label(Transform parent, string name, string value, Vector2 anchor, Vector2 position, Vector2 size,
            float fontSize, bool title = false, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = title ? Title : Body; text.text = value; text.fontSize = fontSize; text.color = color ?? Ink;
            text.alignment = align; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            Place(text.rectTransform, anchor, position, size);
            return text;
        }

        /// <summary>Plaque button from the PSD; primary uses the darker "选中" plaque.</summary>
        public static Button Plaque(Transform parent, string name, string caption, Vector2 anchor, Vector2 position, Vector2 size,
            bool primary = false, float fontSize = 0)
        {
            var image = Picture(parent, name, Sprite(primary ? "选中" : "未选中"), anchor, position, size);
            image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = PlaqueHeight / size.y;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            Style(button, image);
            var label = Label(image.transform, "Label", caption, new Vector2(.5f, .5f), new Vector2(0, 1), size - new Vector2(size.y * .9f, 6),
                fontSize > 0 ? fontSize : Mathf.Round(size.y * .38f), true, Ink);
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = label.fontSize;
            return button;
        }

        /// <summary>Invisible hit area around an ornament (arrows, bookmark tabs).</summary>
        public static Button IconButton(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = Picture(parent, name, sprite, anchor, position, size);
            image.raycastTarget = true; image.alphaHitTestMinimumThreshold = 0;
            var button = image.gameObject.AddComponent<Button>();
            Style(button, image);
            return button;
        }

        public static void Style(Button button, Graphic target)
        {
            button.targetGraphic = target;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .93f, .80f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.80f, .68f, .55f);
            colors.disabledColor = new Color(.72f, .66f, .60f, .55f);
            colors.fadeDuration = .08f;
            button.colors = colors;
        }

        /// <summary>A short tapered rule with the PSD's diamond: a quiet section divider.</summary>
        public static void Rule(Transform parent, string name, Vector2 anchor, Vector2 position, float width, Color color)
        {
            var line = Picture(parent, name, null, anchor, position, new Vector2(width, 1.5f));
            line.color = new Color(color.r, color.g, color.b, color.a * .55f);
        }
    }
}
