using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Fill the canvas with the entire calendar composition, preserving all layer alignment.</summary>
    [ExecuteAlways]
    public sealed class DateChangeArtworkLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform artwork;
        [SerializeField] private Vector2 referenceSize = new Vector2(2181f, 1080f);

        private void OnEnable() => Resize();
        private void OnRectTransformDimensionsChange() => Resize();

        private void Resize()
        {
            if (artwork == null || !(transform is RectTransform parent)) return;
            if (referenceSize.x <= 0f || referenceSize.y <= 0f) return;
            float scale = Mathf.Max(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
            if (scale > 0f) artwork.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
