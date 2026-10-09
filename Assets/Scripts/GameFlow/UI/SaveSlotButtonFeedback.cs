using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(CanvasGroup))]
    public sealed class SaveSlotButtonFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(0f)] private float pressDuration = .06f;
        [SerializeField, Min(0f)] private float releaseDuration = .12f;
        private Button button;
        private CanvasGroup group;
        private Tween fade;
        private bool hovered;
        private bool pressed;

        private void Awake() => EnsureComponents();
        private void OnEnable() => ResetVisual();
        private void OnDisable() => ResetVisual();
        private void OnDestroy() => StopFade();

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureComponents();
            if (!button.IsActive() || !button.IsInteractable()) return;
            hovered = true;
            StopFade();
            group.alpha = 1f;
        }

        public void OnPointerExit(PointerEventData eventData) => ResetVisual();

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureComponents();
            if (eventData.button != PointerEventData.InputButton.Left || !hovered
                || !button.IsActive() || !button.IsInteractable()) return;
            pressed = true;
            FadeTo(.5f, pressDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !pressed) return;
            pressed = false;
            if (!button.IsActive() || !button.IsInteractable()) ResetVisual();
            else FadeTo(hovered ? 1f : 0f, releaseDuration);
        }

        public void RefreshAvailability()
        {
            EnsureComponents();
            if (!button.IsActive() || !button.IsInteractable()) ResetVisual();
        }

        public void ResetVisual()
        {
            EnsureComponents();
            hovered = pressed = false;
            StopFade();
            // Alpha zero must still allow raycasts so the invisible button can
            // receive PointerEnter. Availability is owned by Button.interactable.
            group.alpha = 0f;
        }

        private void EnsureComponents()
        {
            if (button == null) button = GetComponent<Button>();
            // Reapply after activation; a culled alpha-zero Graphic cannot receive
            // the PointerEnter needed to reveal its CanvasGroup.
            if (button.targetGraphic != null)
            {
                button.targetGraphic.canvasRenderer.cullTransparentMesh = false;
                button.targetGraphic.canvasRenderer.cull = false;
            }
            if (group == null) group = GetComponent<CanvasGroup>();
        }

        private void FadeTo(float alpha, float duration)
        {
            StopFade();
            if (duration <= 0f) group.alpha = alpha;
            else fade = DOTween.To(() => group.alpha, value => group.alpha = value, alpha, duration)
                .SetEase(Ease.OutQuad).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void StopFade()
        {
            fade?.Kill(false);
            fade = null;
        }
    }
}
