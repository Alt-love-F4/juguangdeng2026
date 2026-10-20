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
        private DialoguePortraitSizing sizing;
        private string characterId;

        public void Configure(string id)
        {
            characterId = id;
            sizing = Resources.Load<DialoguePortraitSizing>("Dialogue/DialoguePortraitSizing");
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0) return;
            var image = graphic as Image;
            if (image == null || image.type != Image.Type.Simple
                || (image.overrideSprite == null && image.sprite == null)) return;

            UIVertex vertex = default;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity, minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                bottom = Mathf.Min(bottom, vertex.position.y);
                top = Mathf.Max(top, vertex.position.y);
                minX = Mathf.Min(minX, vertex.position.x);
                maxX = Mathf.Max(maxX, vertex.position.x);
            }

            if (sizing != null && top - bottom > .001f && maxX - minX > .001f)
            {
                Sprite sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
                var entry = sizing.Find(characterId, sprite);
                Rect visible = entry != null ? entry.visibleRect : new Rect(0, 0, 1, 1);
                Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                float trimWidth = Mathf.Max(.001f, 1f - (padding.x + padding.z) / sprite.rect.width);
                float trimHeight = Mathf.Max(.001f, 1f - (padding.y + padding.w) / sprite.rect.height);
                float fullWidth = (maxX - minX) / trimWidth;
                float fullHeight = (top - bottom) / trimHeight;
                float visibleBottom = bottom + fullHeight * (visible.yMin - padding.y / sprite.rect.height);
                float visibleCenter = minX + fullWidth * (visible.center.x - padding.x / sprite.rect.width);
                float scale = graphic.rectTransform.rect.height * sizing.heightRatio
                    * (entry != null ? Mathf.Max(.01f, entry.heightScale) : 1f)
                    / (fullHeight * Mathf.Max(.001f, visible.height));
                Vector2 offset = entry != null ? entry.offset : Vector2.zero;
                Rect rect = graphic.rectTransform.rect;
                for (int i = 0; i < vertices.currentVertCount; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    vertex.position.x = (vertex.position.x - visibleCenter) * scale + rect.center.x + offset.x;
                    vertex.position.y = (vertex.position.y - visibleBottom) * scale + rect.yMin + offset.y;
                    vertices.SetUIVertex(vertex, i);
                }
                return;
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
