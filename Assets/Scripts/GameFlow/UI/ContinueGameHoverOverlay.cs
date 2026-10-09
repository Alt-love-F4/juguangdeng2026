using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ContinueGameHoverOverlay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Graphic overlay;
        private Button button;

        private void Awake() => button = GetComponent<Button>();
        private void OnEnable() => SetVisible(false);
        private void OnDisable() => SetVisible(false);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null) button = GetComponent<Button>();
            SetVisible(button.IsActive() && button.IsInteractable());
        }

        public void OnPointerExit(PointerEventData eventData) => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (overlay == null) return;
            Color color = overlay.color;
            color.a = visible ? 1f : 0f;
            overlay.color = color;
        }
    }
}
