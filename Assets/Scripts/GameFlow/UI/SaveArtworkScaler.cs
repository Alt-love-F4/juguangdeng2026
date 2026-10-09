using UnityEngine;

namespace Spotlight.GameFlow
{
    [ExecuteAlways]
    public sealed class SaveArtworkScaler : MonoBehaviour
    {
        [SerializeField] private RectTransform artwork;

        private void OnEnable() => Resize();
        private void OnRectTransformDimensionsChange() => Resize();

        private void Resize()
        {
            if (artwork == null || !(transform is RectTransform parent)) return;
            float scale = Mathf.Min(parent.rect.width / 2048f, parent.rect.height / 1152f);
            artwork.localScale = Vector3.one * scale;
        }
    }
}
