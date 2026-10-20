using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>A soft, breathing tutorial ring. Its transparent center leaves the cafe artwork visible.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public sealed class MapCafeGuideRing : MaskableGraphic
    {
        private float shownAt;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            shownAt = Time.unscaledTime;
            Animate();
        }

        private void Update() => Animate();

        private void Animate()
        {
            float pulse = .5f - .5f * Mathf.Cos((Time.unscaledTime - shownAt) * Mathf.PI * 2f / 1.6f);
            rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.07f, pulse);
            // Animate opacity on the renderer without rebuilding the gradient mesh each frame.
            canvasRenderer.SetAlpha(Mathf.Lerp(.65f, 1f, pulse));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            const int segments = 80;
            // Wide soft halo, narrow warm-white core, and a soft outer falloff.
            AddBand(vh, rect.center, radius, .72f, .84f, 0f, .24f, segments);
            AddBand(vh, rect.center, radius, .84f, .88f, .24f, 1f, segments);
            AddBand(vh, rect.center, radius, .88f, .90f, 1f, 1f, segments);
            AddBand(vh, rect.center, radius, .90f, .94f, 1f, .24f, segments);
            AddBand(vh, rect.center, radius, .94f, 1f, .24f, 0f, segments);
        }

        private void AddBand(VertexHelper vh, Vector2 center, float radius, float inner, float outer,
            float innerAlpha, float outerAlpha, int segments)
        {
            int start = vh.currentVertCount;
            Color innerColor = color;
            Color outerColor = color;
            innerColor.a *= innerAlpha;
            outerColor.a *= outerAlpha;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * (radius * inner), innerColor, Vector2.zero);
                vh.AddVert(center + direction * (radius * outer), outerColor, Vector2.zero);
                if (i == 0) continue;
                int vertex = start + i * 2;
                vh.AddTriangle(vertex - 2, vertex - 1, vertex);
                vh.AddTriangle(vertex, vertex - 1, vertex + 1);
            }
        }
    }
}
