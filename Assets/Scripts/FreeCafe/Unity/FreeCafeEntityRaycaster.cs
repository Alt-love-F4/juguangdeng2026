using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Spotlight.FreeCafe
{
    /// <summary>Entity colliders live on the root, while their visible sprite lives on Art.
    /// Supply the sprite's render order to the EventSystem instead of arbitrary physics hit order.</summary>
    [DisallowMultipleComponent]
    public sealed class FreeCafeEntityRaycaster : Physics2DRaycaster
    {
        public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
        {
            int start = results.Count;
            // Entity motion is applied in LateUpdate, after the normal physics synchronization.
            Physics2D.SyncTransforms();
            base.Raycast(eventData, results);
            for (int i = results.Count - 1; i >= start; i--)
            {
                var hit = results[i];
                var view = hit.gameObject != null ? hit.gameObject.GetComponentInParent<FreeEntityView>() : null;
                if (view == null)
                {
                    var companion = hit.gameObject != null ? hit.gameObject.GetComponent<FreeCafeAmbientAdele>() : null;
                    if (companion != null && companion.Visual != null)
                    {
                        hit.sortingLayer = companion.Visual.sortingLayerID;
                        hit.sortingOrder = companion.Visual.sortingOrder;
                        results[i] = hit;
                    }
                    continue;
                }
                var bell = hit.gameObject.GetComponentInParent<FreeCafeDeliveryBell>();
                if (!view.CanInteract || bell != null && !bell.CanInteract)
                {
                    results.RemoveAt(i);
                    continue;
                }
                var body = bell != null ? bell.Visual : view.body;
                if (body == null) continue;
                hit.sortingLayer = body.sortingLayerID;
                hit.sortingOrder = body.sortingOrder;
                // These SpriteRenderer sorting-group indices are internal Unity APIs.
                // Resolve the public ancestor component instead, preserving the same ordering.
                var group = body.GetComponentInParent<SortingGroup>();
                if (group != null && group.isActiveAndEnabled)
                {
                    hit.sortingGroupID = group.GetInstanceID();
                    hit.sortingGroupOrder = body.sortingOrder;
                    hit.sortingLayer = group.sortingLayerID;
                    hit.sortingOrder = group.sortingOrder;
                    var ray = eventCamera.ScreenPointToRay(eventData.position);
                    hit.distance = Vector3.Dot(ray.direction, group.transform.position - ray.origin);
                }
                results[i] = hit;
            }
        }
    }
}
