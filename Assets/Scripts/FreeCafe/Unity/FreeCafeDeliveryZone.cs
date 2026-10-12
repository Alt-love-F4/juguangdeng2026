using UnityEngine;

namespace Spotlight.FreeCafe
{
    [DisallowMultipleComponent]
    public sealed class FreeCafeDeliveryZone : MonoBehaviour
    {
        [SerializeField] private Vector2 hitboxSize = new Vector2(3.2f, 2.4f);
        [SerializeField] private Color gizmoColor = new Color(1f, .78f, .2f, .7f);
        public Vector2 HitboxSize => hitboxSize;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var old = Gizmos.color;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(transform.position, new Vector3(hitboxSize.x, hitboxSize.y, .05f));
            Gizmos.color = old;
        }
#endif
    }
}
