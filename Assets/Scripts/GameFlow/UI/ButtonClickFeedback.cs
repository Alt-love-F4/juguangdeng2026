using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonClickFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.85f, 1f)] private float pressedScale = 0.96f;
        [SerializeField, Min(0f)] private float duration = 0.07f;
        [SerializeField] private Ease ease = Ease.OutQuad;
        private Selectable selectable;
        private Tween tween;
        private Vector3 baseScale;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && selectable.interactable)
                Animate(pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData) => Animate(1f);
        public void OnPointerExit(PointerEventData eventData) => Animate(1f);

        private void Animate(float multiplier)
        {
            tween?.Kill(false);
            tween = transform.DOScale(baseScale * multiplier, duration)
                .SetEase(ease).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void OnDisable()
        {
            tween?.Kill(false);
            tween = null;
            if (transform != null) transform.localScale = baseScale;
        }

        private void OnDestroy()
        {
            tween?.Kill(false);
            tween = null;
        }
    }
}
