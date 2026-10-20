using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Read-only lid presentation of the existing provisional source pickup. Never owns stock.</summary>
    [DefaultExecutionOrder(120)]
    [RequireComponent(typeof(FreeEntityView))]
    public sealed class FreeCafeSourceJarVisual : MonoBehaviour
    {
        public Sprite closedSprite, openSprite;
        private FreeEntityView view;
        public bool IsOpen { get; private set; }

        private void Awake() { view = GetComponent<FreeEntityView>(); }
        private void LateUpdate()
        {
            if (view == null) view = GetComponent<FreeEntityView>();
            Present(view.Runtime != null && view.Runtime.IsTakingFromSource(view.EntityId));
        }
        public void Present(bool opened)
        {
            if (view == null) view = GetComponent<FreeEntityView>();
            IsOpen = opened;
            var sprite = opened ? openSprite : closedSprite;
            // Both variants have an identical canvas: keep authored scale, hitbox and position.
            if (view.body != null && sprite != null) view.body.sprite = sprite;
        }
        private void OnDisable() { Present(false); }
    }
}
