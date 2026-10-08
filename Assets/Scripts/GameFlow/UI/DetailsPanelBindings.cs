using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Serialized component references only; gameplay/display values live in DetailsPanelData.</summary>
    [Serializable]
    public sealed class DetailsAttributeBinding
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private RectTransform fill;
        [SerializeField] private DetailsHoverTarget hover;

        public bool IsValid => nameText != default && valueText != default && fill != default && hover != default;

        public void Render(DetailsPanel panel, DetailsAttributeData data)
        {
            nameText.text = data.Name;
            valueText.text = data.Value + " / 20";
            Vector2 max = fill.anchorMax;
            max.x = data.Value / 20f;
            fill.anchorMax = max;
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
