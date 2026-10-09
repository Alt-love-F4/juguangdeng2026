using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Small runtime UI factory; all geometry is local to the existing page roots.</summary>
    internal static class DetailsViewUI
    {
        internal static readonly Color Card = new Color(.19f, .15f, .21f);
        internal static readonly Color Accent = new Color(.86f, .57f, .64f);
        internal static RectTransform Root(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        internal static Image Box(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = Root(parent, name, min, max).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
        internal static TMP_Text Text(Transform parent, TMP_FontAsset font, string name, string text,
            Vector2 min, Vector2 max, float size = 23f, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Root(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        internal static Button Button(Transform parent, TMP_FontAsset font, string name, string label, Vector2 min, Vector2 max)
        {
            var image = Box(parent, name, min, max, Card);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text(image.transform, font, "Label", label, Vector2.zero, Vector2.one, 24, TextAlignmentOptions.Center);
            return button;
        }
        internal static void Clear(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }
        internal static RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var viewport = Box(parent, name, min, max, new Color(.08f, .06f, .11f, .4f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = Root(viewport.transform, "Content", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            return content;
        }
        internal static void Avatar(Transform parent, TMP_FontAsset font, Sprite avatar, Sprite fallback,
            Vector2 min, Vector2 max)
        {
            var image = Box(parent, "Avatar", min, max, Card);
            image.raycastTarget = false;
            image.sprite = avatar != null ? avatar : fallback;
            image.preserveAspect = true;
            if (image.sprite != null) image.color = Color.white;
            else Text(image.transform, font, "UnknownAvatar", "?", Vector2.zero, Vector2.one, 58, TextAlignmentOptions.Center);
        }
    }
}
