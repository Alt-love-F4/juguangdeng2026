using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Artwork shared by the runtime map panels.</summary>
    public sealed class MapVisualTheme : ScriptableObject
    {
        public Sprite[] pointsSegments;
        public Sprite locationHighlight;
        public Sprite pointsBase, pointsFill, pointsOutline, pointsLabel, dateFrame;
        public Sprite details, bag, tasks, shortcuts, panel, inventoryTitle, taskTitle;
        public Sprite actionCard, actionEmblem, actionDivider, requirement, execute, disabledExecute, taskCard, tracking, timer, zoomBar, zoomHandle, progress;
        public TMP_FontAsset headingFont, bodyFont;
        public static MapVisualTheme Current => Resources.Load<MapVisualTheme>("MapVisualTheme");
        public static readonly Color Ink = new Color(.38f, .21f, .08f);
        public static readonly Color Accent = new Color(.74f, .54f, .37f);
        public static void Skin(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(.95f, .90f, .71f);
            image.preserveAspect = false;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 2f;
        }
        public static Image Art(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = MapActionDetailsView.Rect(name, parent).gameObject.AddComponent<Image>();
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
            Skin(image, sprite); image.raycastTarget = false; return image;
        }
        public static void Font(TextMeshProUGUI label, bool heading = false)
        {
            var theme = Current;
            var font = theme != null ? (heading ? theme.headingFont : theme.bodyFont) : null;
            if (font != null) label.font = font;
            label.color = Ink;
        }
    }
}
