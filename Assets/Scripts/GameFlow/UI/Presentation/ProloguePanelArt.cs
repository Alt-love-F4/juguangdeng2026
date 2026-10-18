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
            ZoomIn,
            // Motion-comic glides: the panel starts part-way towards the screen edge and fades in while gliding home.
            FlyFromLeft,
            FlyFromRight,
            FlyFromTop,
            FlyFromBottom
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
        [Tooltip("Fly entrances: start offset as a fraction of the distance to just beyond the screen edge.")]
        [SerializeField, Range(0f, 1f)] private float flyDistance = .5f;
        [Tooltip("Fly entrances: portion of the glide over which the panel fades in.")]
        [SerializeField, Range(.05f, 1f)] private float flyFadePortion = .7f;
        [Tooltip("Fly entrances: starting tilt in degrees, settles to 0 with a light overshoot.")]
        [SerializeField] private float flyTilt = 1.5f;
        [Tooltip("Fly entrances: the art inside counter-moves by this fraction of the frame for a parallax push.")]
        [SerializeField, Range(0f, .2f)] private float flyParallax = .06f;
        [Header("Hold drift (slow push-in after entrance)")]
        [SerializeField, Range(0f, .1f)] private float driftScale = .03f;
        [SerializeField, Min(.1f)] private float driftSeconds = 12f;

        private bool cached;
        private Vector2 restPosition;
        private Vector3 restScale;
        private Quaternion restRotation;

        public float EndTime => delay + duration;
        public Entrance Style => entrance;

        private void EnsureCached()
        {
            if (cached || frame == null) return;
            restPosition = frame.anchoredPosition;
            restScale = frame.localScale;
            restRotation = frame.localRotation;
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
            bool fly = entrance >= Entrance.FlyFromLeft;
            float alpha = local < 0f ? 0f : Smooth(Mathf.Clamp01(p / (fly ? flyFadePortion : wipe ? .3f : .6f)));

            Vector2 offset = Vector2.zero, artOffset = Vector2.zero;
            float frameScale = 1f, artScale = 1f, tilt = 0f;
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
                case Entrance.FlyFromLeft:
                case Entrance.FlyFromRight:
                case Entrance.FlyFromTop:
                case Entrance.FlyFromBottom:
                {
                    Vector2 dir = FlyDirection();
                    float travel = 1f - EaseOutQuart(p);
                    offset = -dir * OffscreenDistance(dir, w, h) * flyDistance * travel;
                    tilt = flyTilt * (dir.x != 0f ? -dir.x : dir.y) * (1f - EaseOutBack(p));
                    // Art lags behind the frame, then catches up; slight overscale hides the exposed edge.
                    artOffset = dir * new Vector2(w, h) * flyParallax * travel;
                    artScale = 1f + flyParallax * 2f * travel;
                    break;
                }
            }
            float hold = Mathf.Max(0f, local - duration);
            artScale *= 1f + driftScale * Smooth(Mathf.Clamp01(hold / driftSeconds));

            group.alpha = alpha;
            frame.anchoredPosition = restPosition + offset;
            frame.localScale = restScale * frameScale;
            frame.localRotation = restRotation * Quaternion.Euler(0f, 0f, tilt);
            art.anchoredPosition = artOffset;
            art.localScale = new Vector3(artScale, artScale, 1f);
            if (mask != null)
            {
                mask.padding = padding;
                mask.softness = softness;
            }
        }

        public void Hide() => Evaluate(float.NegativeInfinity);

        /// <summary>Direction of travel (towards the rest position).</summary>
        private Vector2 FlyDirection() => entrance switch
        {
            Entrance.FlyFromLeft => Vector2.right,
            Entrance.FlyFromRight => Vector2.left,
            Entrance.FlyFromTop => Vector2.down,
            _ => Vector2.up
        };

        /// <summary>How far back along <paramref name="dir"/> the frame must start to be fully off-screen.</summary>
        private float OffscreenDistance(Vector2 dir, float w, float h)
        {
            Rect screen = frame.parent is RectTransform parent ? parent.rect : new Rect(-960f, -540f, 1920f, 1080f);
            const float margin = 40f;
            if (dir.x > 0f) return restPosition.x - screen.xMin + w * .5f + margin;
            if (dir.x < 0f) return screen.xMax - restPosition.x + w * .5f + margin;
            if (dir.y > 0f) return restPosition.y - screen.yMin + h * .5f + margin;
            return screen.yMax - restPosition.y + h * .5f + margin;
        }

        private static float EaseOutQuart(float t) { float u = 1f - t; return 1f - u * u * u * u; }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
