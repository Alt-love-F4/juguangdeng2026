using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Code-native neutral silhouette used only until real player avatar assets are connected.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DetailsAvatarPlaceholder : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float unit = Mathf.Min(rect.width, rect.height);
            Disc(mesh, center + Vector2.up * unit * .15f, unit * .16f);
            Disc(mesh, center + Vector2.down * unit * .3f, unit * .33f);
            // Lower edge is kept inside the portrait frame.
        }
        private void Disc(VertexHelper mesh, Vector2 center, float radius)
        {
            const int segments = 48;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                mesh.AddVert(position, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
