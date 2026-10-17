using UnityEngine;

namespace Spotlight.FreeCafe
{
    [DisallowMultipleComponent]
    public sealed class FreeCafeDeliveryZone : MonoBehaviour
    {
        [SerializeField] private Vector2 hitboxSize = new Vector2(4.4f, 2.1f);
        [SerializeField] private Vector2 artworkSize = new Vector2(4.4f, 2.1f);
        [SerializeField] private Vector2 artworkOffset = new Vector2(0, -.8f);
        public Vector2 ArtworkSize => artworkSize;
        public Vector2 ArtworkOffset => artworkOffset;
        [SerializeField] private Color gizmoColor = new Color(1f, .78f, .2f, .7f);
        public Vector2 HitboxSize => hitboxSize;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var old = Gizmos.color;
            var oldMatrix = Gizmos.matrix;
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(artworkOffset, new Vector3(hitboxSize.x, hitboxSize.y, .05f));
            Gizmos.matrix = oldMatrix;
            Gizmos.color = old;
        }
#endif
    }
}
