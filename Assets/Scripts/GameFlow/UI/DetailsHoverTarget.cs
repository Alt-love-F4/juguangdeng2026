using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.GameFlow
{
    /// <summary>Hover descriptions supplied by the panel display snapshot, with no gameplay model dependencies.</summary>
    public sealed class DetailsHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private DetailsPanel panel;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string description;

        public void Bind(DetailsPanel owner, string newTitle, string newDescription)
        {
            if (panel != default) panel.HideTooltip(this);
            panel = owner;
            title = newTitle ?? string.Empty;
            description = newDescription ?? string.Empty;
        }

        public void Clear()
        {
            if (panel != default) panel.HideTooltip(this);
            title = description = string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel != default) panel.ShowTooltip(this, title, description, eventData.position);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != default) panel.HideTooltip(this);
        }
        private void OnDisable()
        {
            if (panel != default) panel.HideTooltip(this);
        }
    }
}
