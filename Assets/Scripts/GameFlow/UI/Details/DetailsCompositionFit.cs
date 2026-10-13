using UnityEngine;
namespace Spotlight.GameFlow
{
    /// <summary>Fit the entire PSD composition uniformly within the UIKit canvas.</summary>
    [ExecuteAlways]
    public sealed class DetailsCompositionFit : MonoBehaviour
    {
        private void LateUpdate()
        {
            var rect = transform as RectTransform;
            var parent = transform.parent as RectTransform;
            if (rect == default || parent == default) return;
            float scale = Mathf.Min(parent.rect.width / 2048f, parent.rect.height / 1152f);
            if (scale > 0) rect.localScale = new Vector3(scale, scale, 1);
        }
    }
}
