using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Gameplay
{
    /// <summary>Sprite 世界中的地点入口，使用主摄像机的 Physics2DRaycaster。</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class CoffeeLocationPortal : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public CoffeeLocationController controller;
        public CoffeeLocationId targetLocation;
        public SpriteRenderer highlightRenderer;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (controller == null) controller = FindFirstObjectByType<CoffeeLocationController>();
            if (controller != null) controller.Enter(targetLocation);
        }
        public void OnPointerEnter(PointerEventData eventData) => Highlight(true);
        public void OnPointerExit(PointerEventData eventData) => Highlight(false);
        private void OnDisable() => Highlight(false);
        private void Highlight(bool on)
        {
            if (highlightRenderer != null)
                highlightRenderer.color = new Color(1f, .78f, .4f, on ? .5f : .14f);
        }
    }
}
