using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Bottom-aligns the aspect-fitted portrait geometry (including Unity's sprite
    /// padding trim) without taking ownership of the transforms or vertex colors
    /// used by movement, focus and fade effects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class DialoguePortraitBottomAlignment : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            var image = graphic as Image;
            if (image == null || image.type != Image.Type.Simple
                || (image.overrideSprite == null && image.sprite == null)) return;

            UIVertex vertex = default;
            float bottom = float.PositiveInfinity;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                bottom = Mathf.Min(bottom, vertex.position.y);
            }

            float shift = graphic.rectTransform.rect.yMin - bottom;
            if (Mathf.Abs(shift) < .001f) return;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position.y += shift;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
