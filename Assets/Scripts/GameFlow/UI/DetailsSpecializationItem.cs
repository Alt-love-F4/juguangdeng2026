using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>One owned badge view; preserves the existing icon, label and hover component references.</summary>
    public sealed class DetailsSpecializationItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private DetailsHoverTarget hover;
        [SerializeField] private TMP_Text fallbackIcon;

        public bool IsValid => icon != default && nameText != default && hover != default && fallbackIcon != default;

        public void Render(DetailsPanel panel, DetailsSpecializationData data)
        {
            nameText.text = data.Name;
            icon.sprite = data.Icon;
            icon.enabled = data.Icon != default;
            if (fallbackIcon != default) fallbackIcon.gameObject.SetActive(data.Icon == default);
            hover.Bind(panel, data.Name, data.Description);
        }

        public void Clear()
        {
            icon.sprite = null;
            nameText.text = string.Empty;
            hover.Clear();
        }
    }
}
