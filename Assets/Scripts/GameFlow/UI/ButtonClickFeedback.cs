using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonClickFeedback : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.85f, 1f)] private float pressedScale = 0.96f;
        [SerializeField, Min(0f)] private float duration = 0.07f;
        [SerializeField] private Ease ease = Ease.OutQuad;
        // 悬停参数默认无效果；只有主菜单纸片按钮显式配置，其他面板保持原有按压反馈。
        [Header("Hover (optional)")]
        [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1f;
        [SerializeField] private Vector2 hoverOffset;
        [SerializeField] private Shadow hoverShadow;
        [SerializeField] private Color hoverShadowColor = new Color(0.16f, 0.08f, 0.05f, 0.45f);
        [SerializeField, Min(0f)] private float hoverDuration = 0.14f;
        [SerializeField, Min(0f)] private float releaseOvershoot;
        private Selectable selectable;
        private RectTransform rect;
        private Graphic hitArea;
        private Vector4 basePadding;
        private Tween tween;
        private Tween moveTween;
        private Tween shadowTween;
        private Vector3 baseScale;
        private Vector2 basePosition;
        private Color shadowRestColor;
        private bool hovered;
        private bool pressed;

        private bool HasHover => hoverScale > 1f || hoverOffset != Vector2.zero || hoverShadow != null;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            rect = transform as RectTransform;
            hitArea = GetComponent<Graphic>();
            if (hitArea != null) basePadding = hitArea.raycastPadding;
            baseScale = transform.localScale;
            if (rect != null) basePosition = rect.anchoredPosition;
            if (hoverShadow != null)
            {
                shadowRestColor = hoverShadowColor;
                shadowRestColor.a = 0f;
                hoverShadow.effectColor = shadowRestColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!HasHover || selectable == null || !selectable.IsInteractable()) return;
            hovered = true;
            SetHitPadding(true);
            Apply(hoverDuration, ease);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null || !selectable.interactable) return;
            pressed = true;
            Apply(duration, ease);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;
            // 轻微回弹只用于松开；离开按钮时直接平滑复位。
            if (releaseOvershoot > 0f) Apply(Mathf.Max(duration, .16f), Ease.OutBack, releaseOvershoot);
            else Apply(duration, ease);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!hovered && !pressed) return;
            hovered = pressed = false;
            SetHitPadding(false);
            Apply(HasHover ? hoverDuration : duration, ease);
        }

        // 悬停位移会带走点击区域；把位移反方向的边补回来，指针停在边缘时不会进出抖动。
        private void SetHitPadding(bool extend)
        {
            if (hoverOffset == Vector2.zero || hitArea == null) return;
            hitArea.raycastPadding = extend
                ? basePadding - new Vector4(Mathf.Max(hoverOffset.x, 0f), Mathf.Max(hoverOffset.y, 0f),
                    Mathf.Max(-hoverOffset.x, 0f), Mathf.Max(-hoverOffset.y, 0f))
                : basePadding;
        }

        private void Apply(float time, Ease curve, float overshoot = 0f)
        {
            float multiplier = (hovered ? hoverScale : 1f) * (pressed ? pressedScale : 1f);
            tween?.Kill(false);
            tween = transform.DOScale(baseScale * multiplier, time).SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            if (overshoot > 0f) tween.SetEase(curve, overshoot);
            else tween.SetEase(curve);
            if (rect != null && hoverOffset != Vector2.zero)
            {
                moveTween?.Kill(false);
                moveTween = DOTween.To(() => rect.anchoredPosition, p => rect.anchoredPosition = p,
                        basePosition + (hovered ? hoverOffset : Vector2.zero), hoverDuration).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
            if (hoverShadow != null)
            {
                shadowTween?.Kill(false);
                Color target = hovered ? hoverShadowColor : shadowRestColor;
                shadowTween = DOTween.To(() => hoverShadow.effectColor, c => hoverShadow.effectColor = c, target, hoverDuration)
                    .SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            }
        }

        private void KillTweens()
        {
            tween?.Kill(false);
            moveTween?.Kill(false);
            shadowTween?.Kill(false);
            tween = moveTween = shadowTween = null;
        }

        private void OnDisable()
        {
            KillTweens();
            SetHitPadding(false);
            hovered = pressed = false;
            if (transform != null) transform.localScale = baseScale;
            if (rect != null && hoverOffset != Vector2.zero) rect.anchoredPosition = basePosition;
            if (hoverShadow != null) hoverShadow.effectColor = shadowRestColor;
        }

        private void OnDestroy() => KillTweens();
    }
}
