using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// One comic panel of the opening prologue. Evaluated from a beat clock (no tweens), so
    /// fast-forward, skip and replay always land on a deterministic frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProloguePanelArt : MonoBehaviour
    {
        public enum Entrance
        {
            WipeUp,
            WipeDown,
            WipeRight,
            WipeLeft,
            SplitVertical,
            SlideUp,
            SlideDown,
            SlideRight,
            SlideLeft,
            Pop,
            ZoomIn
        }

        [Header("Bindings")]
        [SerializeField] private RectTransform frame;
        [SerializeField] private RectTransform art;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectMask2D mask;
        [Header("Entrance")]
        [SerializeField] private Entrance entrance = Entrance.WipeUp;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(.01f)] private float duration = .9f;
        [SerializeField, Min(0f)] private float slideDistance = 70f;
        [SerializeField, Min(1f)] private float zoomFrom = 1.18f;
        [SerializeField, Min(0)] private int wipeSoftness = 36;
        [Header("Hold drift (slow push-in after entrance)")]
        [SerializeField, Range(0f, .1f)] private float driftScale = .03f;
        [SerializeField, Min(.1f)] private float driftSeconds = 12f;

        private bool cached;
        private Vector2 restPosition;
        private Vector3 restScale;

        public float EndTime => delay + duration;
        public Entrance Style => entrance;

        private void EnsureCached()
        {
            if (cached || frame == null) return;
            restPosition = frame.anchoredPosition;
            restScale = frame.localScale;
            cached = true;
        }

        /// <summary>Applies the panel state at <paramref name="time"/> seconds after the beat's art cue.</summary>
        public void Evaluate(float time)
        {
            if (frame == null || art == null || group == null) return;
            EnsureCached();
            float local = time - delay;
            float p = Mathf.Clamp01(local / duration);
            float e = 1f - Mathf.Pow(1f - p, 3f);
            bool wipe = entrance <= Entrance.SplitVertical;
            float alpha = local < 0f ? 0f : Smooth(Mathf.Clamp01(p / (wipe ? .3f : .6f)));

            Vector2 offset = Vector2.zero;
            float frameScale = 1f, artScale = 1f;
            Vector4 padding = Vector4.zero;
            Vector2Int softness = Vector2Int.zero;
            float w = frame.rect.width, h = frame.rect.height;
            int soft = Mathf.RoundToInt(wipeSoftness * (1f - p));
            switch (entrance)
            {
                // RectMask2D padding = (left, bottom, right, top).
                case Entrance.WipeUp: padding.w = (1f - e) * h; softness.y = soft; break;
                case Entrance.WipeDown: padding.y = (1f - e) * h; softness.y = soft; break;
                case Entrance.WipeRight: padding.z = (1f - e) * w; softness.x = soft; break;
                case Entrance.WipeLeft: padding.x = (1f - e) * w; softness.x = soft; break;
                case Entrance.SplitVertical: padding.y = padding.w = (1f - e) * h * .5f; softness.y = soft; break;
                case Entrance.SlideUp: offset.y = -(1f - e) * slideDistance; break;
                case Entrance.SlideDown: offset.y = (1f - e) * slideDistance; break;
                case Entrance.SlideRight: offset.x = -(1f - e) * slideDistance; break;
                case Entrance.SlideLeft: offset.x = (1f - e) * slideDistance; break;
                case Entrance.Pop: frameScale = Mathf.LerpUnclamped(.86f, 1f, EaseOutBack(p)); break;
                case Entrance.ZoomIn: artScale = Mathf.Lerp(zoomFrom, 1f, e); break;
            }
            float hold = Mathf.Max(0f, local - duration);
            artScale *= 1f + driftScale * Smooth(Mathf.Clamp01(hold / driftSeconds));

            group.alpha = alpha;
            frame.anchoredPosition = restPosition + offset;
            frame.localScale = restScale * frameScale;
            art.localScale = new Vector3(artScale, artScale, 1f);
            if (mask != null)
            {
                mask.padding = padding;
                mask.softness = softness;
            }
        }

        public void Hide() => Evaluate(float.NegativeInfinity);

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
