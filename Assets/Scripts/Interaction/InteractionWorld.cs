using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Interaction
{
    public interface IInteractionRules
    {
        InteractionResult CanTake(EntityDefinition source, ItemState item);
        InteractionResult Preview(ItemState item, InteractionTarget target, bool provisional);
        InteractionResult Commit(ItemState item, InteractionTarget target, bool provisional);
        void Activate(InteractionTarget entity);
    }

    /// <summary>Reusable pointer adapter. A cafe, printing desk or other scene supplies its own rules.</summary>
    public abstract class InteractionWorld : MonoBehaviour
    {
        public PlacementLedger Ledger { get; protected set; }
        protected abstract IInteractionRules Rules { get; }
        public virtual bool InputBlocked => Time.timeScale <= 0f;
        public bool IsDragging => dragging != null;
        protected readonly Dictionary<string, CarryableEntity> views = new();
        private readonly List<RaycastResult> hits = new();
        private InteractiveEntity dragOwner, target;
        private CarryableEntity dragging;
        private bool provisional;
        private Vector3 origin, offset;
        private int sortingOrder;
        public abstract void ShowHint(string message);
        protected abstract CarryableEntity CreateView(ItemState item);
        public virtual void Activate(InteractiveEntity entity) => Rules.Activate(new InteractionTarget(entity.Definition, entity.Item));
        public void BeginDrag(InteractiveEntity entity, PointerEventData e)
        {
            if (IsDragging || InputBlocked) return;
            var permission = Rules.CanTake(entity.Definition, entity.Item);
            if (!permission.Success) { ShowHint(permission.Message); return; }
            dragOwner = entity; provisional = entity is ItemSourceEntity;
            if (provisional)
            {
                var item = new ItemState { definitionId = entity.Definition.item_id };
                dragging = CreateView(item);
                if (dragging == null) { dragOwner = null; return; }
                dragging.transform.position = entity.transform.position;
            }
            else dragging = entity as CarryableEntity;
            if (dragging == null) { dragOwner = null; return; }
            origin = dragging.transform.position; offset = origin - ScreenToWorld(e);
            sortingOrder = dragging.Renderer.sortingOrder; dragging.Renderer.sortingOrder = 100;
            MoveDrag(e);
        }
        private Vector3 ScreenToWorld(PointerEventData e)
        {
            var camera = e.pressEventCamera != null ? e.pressEventCamera : Camera.main;
            if (camera == null) return origin;
            var point = camera.ScreenToWorldPoint(new Vector3(e.position.x, e.position.y, -camera.transform.position.z));
            point.z = 0f; return point;
        }
        public void MoveDrag(PointerEventData e)
        {
            if (dragging == null) return;
            if (InputBlocked) { CancelDrag(); return; }
            dragging.transform.position = ScreenToWorld(e) + offset;
            if (target != null) target.Highlight(false);
            target = FindTarget(e);
            if (target != null)
            {
                var result = Rules.Preview(dragging.Item, new InteractionTarget(target.Definition, target.Item), provisional);
                target.Highlight(true, result.Success);
                ShowHint(result.Success ? "松开放置到：" + target.Definition.name : result.Message);
            }
        }
        private InteractiveEntity FindTarget(PointerEventData e)
        {
            hits.Clear(); EventSystem.current?.RaycastAll(e, hits);
            foreach (var hit in hits)
            {
                if (hit.gameObject.GetComponentInParent<UnityEngine.Canvas>() != null) return null;
                var entity = hit.gameObject.GetComponentInParent<InteractiveEntity>();
                if (entity != null && entity != dragging && entity.CanReceive) return entity;
            }
            return null;
        }
        public void EndDrag(PointerEventData e)
        {
            if (dragging == null) return;
            if (InputBlocked) { CancelDrag(); return; }
            target = FindTarget(e);
            var old = dragging; bool wasProvisional = provisional; var destination = target;
            ClearDrag(); // release input ownership before rules may open a modal or destroy a consumed item
            var result = destination != null ? Rules.Commit(old.Item, new InteractionTarget(destination.Definition, destination.Item), wasProvisional) : InteractionResult.Reject("没有可放置的位置，已取消");
            if (!result.Success)
            {
                if (wasProvisional) Destroy(old.gameObject); else old.transform.position = origin;
                ShowHint(result.Message); return;
            }
            if (string.IsNullOrEmpty(old.Item.instanceId) || Ledger.Find(old.Item.instanceId) == null) Destroy(old.gameObject);
            else { views[old.Item.instanceId] = old; Snap(old); }
            Reconcile(); ShowHint(result.Message);
        }
        public void CancelDragOwnedBy(InteractiveEntity owner) { if (dragOwner == owner) CancelDrag(); }
        public void CancelDrag()
        {
            if (dragging == null) return;
            var old = dragging; bool wasProvisional = provisional;
            ClearDrag();
            if (wasProvisional) Destroy(old.gameObject); else old.transform.position = origin;
        }
        private void ClearDrag()
        {
            if (target != null) target.Highlight(false);
            if (dragging != null) { dragging.Renderer.sortingOrder = sortingOrder; dragging.Highlight(false); }
            dragging = null; dragOwner = null; target = null;
        }
        protected void Snap(CarryableEntity view)
        {
            foreach (var entity in GetComponentsInChildren<InteractiveEntity>(true))
                if (entity.definitionId == view.Item.locationId && entity is PlacementEntity) { view.transform.position = entity.transform.position + Vector3.up * .18f; break; }
        }
        protected void Reconcile()
        {
            if (Ledger == null) return;
            var remove = new List<string>();
            foreach (var pair in views) if (Ledger.Find(pair.Key) == null) { if (pair.Value != null) Destroy(pair.Value.gameObject); remove.Add(pair.Key); }
            foreach (var id in remove) views.Remove(id);
            foreach (var item in Ledger.Items)
            {
                if (!views.TryGetValue(item.instanceId, out var view) || view == null) { view = CreateView(item); if (view == null) continue; views[item.instanceId] = view; }
                view.Bind(item);
                if (view != dragging) Snap(view);
            }
        }
        protected virtual void Update() { if (InputBlocked && IsDragging) CancelDrag(); }
        protected virtual void OnDisable() => CancelDrag();
    }
}
