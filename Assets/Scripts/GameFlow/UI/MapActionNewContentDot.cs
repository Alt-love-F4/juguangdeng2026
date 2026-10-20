using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Persistent activity/important-task marker hosted by an existing location button.</summary>
    // Graphic/MaskableGraphic do not declare this dependency in the installed uGUI package.
    // Require it here, just as Image does, before any OnEnable/masking callback can run.
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public sealed class MapActionNewContentDot : MaskableGraphic
    {
        public static readonly Color OrdinaryColor = new Color(1f, .76f, .12f, 1f);
        public static readonly Color ImportantColor = new Color(.86f, .12f, .15f, 1f);

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            vh.AddVert(center, color, Vector2.zero);
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
