using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>The PSD exports bake different page interiors into their paper sprites.
    /// Keep one persistent paper Image and swap only this artwork; common chrome never fades.</summary>
    [Serializable]
    public sealed class DetailsPageSurfaceBinding
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private Vector2 position;
        [SerializeField] private Vector2 size;
        [SerializeField] private Vector2 pivot;
        [SerializeField] private Vector3 scale;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool visible = true;

        public DetailsPageSurfaceBinding(Image source)
        {
            sprite = source.sprite;
            position = source.rectTransform.anchoredPosition;
            size = source.rectTransform.sizeDelta;
            pivot = source.rectTransform.pivot;
            scale = source.rectTransform.localScale;
            color = source.color;
            visible = source.enabled && source.gameObject.activeSelf;
        }
        public void Render(Image target)
        {
            target.sprite = sprite;
            target.color = color;
            target.enabled = visible;
            RectTransform rect = target.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = scale;
        }
    }

    /// <summary>Serialized component references only; gameplay/display values live in DetailsPanelData.</summary>
    [Serializable]
    public sealed class DetailsAttributeBinding
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private RectTransform fill;
        [SerializeField] private DetailsHoverTarget hover;

        public bool IsValid => nameText != default && valueText != default && hover != default;

        public void Render(DetailsPanel panel, DetailsAttributeData data)
        {
            nameText.text = data.Name;
            valueText.text = data.Value + " / 20";
            if (fill != default)
            {
                Vector2 max = fill.anchorMax;
                max.x = data.Value / 20f;
                fill.anchorMax = max;
            }
            hover.Bind(panel, data.Name, data.Description);
        }
    }

    [Serializable]
    public sealed class DetailsSectionBinding
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text heading;
        [SerializeField] private TMP_Text description;
        public bool IsValid => title != default && heading != default && description != default;

        public void Render(DetailsSectionData data)
        {
            title.text = data.Title;
            heading.text = data.Heading;
            description.text = data.Description;
        }
    }
}
