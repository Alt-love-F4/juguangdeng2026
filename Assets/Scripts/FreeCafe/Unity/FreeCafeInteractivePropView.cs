using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.FreeCafe
{
    /// <summary>Reusable world prop presentation. Gameplay remains owned by the parent interaction.</summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeInteractivePropView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public SpriteRenderer artwork;
        public SpriteRenderer highlight;
        [Range(0f, .2f)] public float idlePulse = .035f;
        [Min(1f)] public float pulsePeriod = 3.6f;
        public Color idleColor = Color.white;
        public Color hoverColor = new Color(1f, .97f, .78f, 1f);
        public bool IsHovered { get; private set; }

        public void OnPointerEnter(PointerEventData eventData) { SetHovered(true); }
        public void OnPointerExit(PointerEventData eventData) { SetHovered(false); }
        public void SetHovered(bool value) { IsHovered = value; Present(); }
        private void OnEnable() { IsHovered = false; Present(); }
        private void Update() { Present(); }
        private void Present()
        {
            float wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(1f, pulsePeriod));
            if (artwork != null) artwork.color = IsHovered ? hoverColor : idleColor;
            if (highlight != null)
            {
                Color color = new Color(1f, .78f, .28f, IsHovered ? .34f + wave * .08f : .035f + wave * idlePulse);
                highlight.color = color;
            }
        }
    }
}
