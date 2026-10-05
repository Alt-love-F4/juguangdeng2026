using UnityEngine;

namespace Spotlight.Gameplay
{
    /// <summary>世界只绘制在上下 UI 黑边之间。背景等比适配，保留原图比例。</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class CoffeeWorldViewport : MonoBehaviour
    {
        [Range(0f, .4f)] public float topMargin = 160f / 1080f;
        [Range(0f, .4f)] public float bottomMargin = 160f / 1080f;
        public SpriteRenderer[] backgrounds;
        private Camera worldCamera;
        private void OnEnable() { worldCamera = GetComponent<Camera>(); Apply(); }
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if (worldCamera == null) worldCamera = GetComponent<Camera>();
            var viewport = new Rect(0, bottomMargin, 1, Mathf.Max(.1f, 1 - topMargin - bottomMargin));
            if (worldCamera.rect != viewport) worldCamera.rect = viewport;
            if (!worldCamera.orthographic || backgrounds == null) return;
            float height = worldCamera.orthographicSize * 2;
            float width = height * worldCamera.aspect;
            foreach (var background in backgrounds)
            {
                if (background == null || background.sprite == null) continue;
                var size = background.sprite.bounds.size;
                // 草图可能比例不同，Contain 防止裁掉草图上的摆放区域。
                float scale = Mathf.Min(width / size.x, height / size.y);
                var desiredScale = new Vector3(scale, scale, 1);
                if ((background.transform.localScale - desiredScale).sqrMagnitude > .000001f) background.transform.localScale = desiredScale;
            }
        }
    }
}
