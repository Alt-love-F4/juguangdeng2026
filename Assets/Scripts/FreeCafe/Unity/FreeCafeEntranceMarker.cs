using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Reusable entrance affordance. The owning portal/interaction keeps all gameplay logic.</summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeEntranceMarker : MonoBehaviour
    {
        public SpriteRenderer frame;
        public SpriteRenderer halo;
        [Range(0f, .15f)] public float pulseAmount = .06f;
        [Min(1f)] public float pulsePeriod = 3.2f;
        public Color idleColor = new Color(1f, .87f, .62f, .9f);
        public Color hoverColor = new Color(1f, .96f, .8f, 1f);
        public bool IsHovered { get; private set; }
        public Bounds WorldBounds => frame != null ? frame.bounds : new Bounds(transform.position, Vector3.zero);

        public void SetHovered(bool hovered)
        {
            IsHovered = hovered;
            Present();
        }
        private void OnEnable() { IsHovered = false; Present(); }
        private void OnDisable() { IsHovered = false; Present(); }
        private void Update() => Present();
        private void Present()
        {
            // Opacity-only breathing: no moving target, zoom, flashing or changing hitbox.
            float wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(1f, pulsePeriod));
            Color tint = IsHovered ? hoverColor : idleColor;
            tint.a *= 1f - pulseAmount * wave;
            if (frame != null) frame.color = tint;
            if (halo != null)
            {
                tint.a = IsHovered ? .17f : .08f + wave * .025f;
                halo.color = tint;
            }
        }
    }
}
