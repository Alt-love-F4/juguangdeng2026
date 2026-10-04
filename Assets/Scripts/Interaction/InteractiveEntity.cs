using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight.Interaction
{
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    public abstract class InteractiveEntity : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string definitionId;
        public EntityDefinition Definition { get; private set; }
        public InteractionWorld World { get; private set; }
        public SpriteRenderer Renderer { get; private set; }
        private Color baseColor;
        private Vector3 baseScale;
        private int draggedFrame = -1;
        public virtual bool CanDrag => false;
        public virtual bool CanReceive => false;
        public virtual ItemState Item => null;
        public void Initialize(EntityDefinition definition, InteractionWorld world)
        {
            definitionId = definition.id; Definition = definition; World = world;
            Renderer = GetComponent<SpriteRenderer>(); baseColor = Renderer.color; baseScale = transform.localScale;
        }
        public void Highlight(bool active, bool valid = true)
        {
            if (Renderer == null) return;
            Renderer.color = active ? Color.Lerp(baseColor, valid ? new Color(1f, .88f, .5f) : new Color(1f, .3f, .3f), .45f) : baseColor;
        }
        public void OnPointerEnter(PointerEventData e) { if (World == null || World.InputBlocked) return; Highlight(true); World.ShowHint(Definition?.name); }
        public void OnPointerExit(PointerEventData e) { if (World == null || !World.IsDragging) Highlight(false); }
        public void OnPointerClick(PointerEventData e) { if (draggedFrame == Time.frameCount) return; if (World != null && !World.InputBlocked) World.Activate(this); }
        public void OnBeginDrag(PointerEventData e) { draggedFrame = Time.frameCount; if (CanDrag && World != null && !World.InputBlocked) World.BeginDrag(this, e); }
        public void OnDrag(PointerEventData e) => World?.MoveDrag(e);
        public void OnEndDrag(PointerEventData e) { draggedFrame = Time.frameCount; World?.EndDrag(e); }
        protected virtual void OnDisable() { World?.CancelDragOwnedBy(this); Highlight(false); transform.localScale = baseScale == Vector3.zero ? transform.localScale : baseScale; }
    }
}
