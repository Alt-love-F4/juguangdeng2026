using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>Thin input/render adapter. Identity and processing never live in the GameObject.</summary>
    public sealed class FreeEntityView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public MonoBehaviour liquidPresenter;
        public SpriteRenderer body;
        public TMP_Text label;
        public BoxCollider2D hitbox;
        public ArtRequirementTag art;
        public string EntityId { get; private set; }
        public FreeCafeRuntime Runtime { get; private set; }
        private string specId,lastState;
        private bool preview;
        private bool hovered, selected, dropTarget, invalidDrop;
        private TMP_Text fillStatus;
        private SpriteRenderer fillBarBackground, fillBar;
        private static Sprite onePixelSprite;
        private Coroutine returnAnimation, rejectionAnimation;
        private Vector3 bodyOrigin;

        private void Awake()
        {
            EnsureFillIndicator();
            if(body!=null) bodyOrigin=body.transform.localPosition;
        }
        private void EnsureFillIndicator()
        {
            if(fillStatus!=null) return;
            var statusGo=new GameObject("FillStatus"); statusGo.transform.SetParent(transform,false);
            fillStatus=statusGo.AddComponent<TextMeshPro>();
            if(label!=null) fillStatus.font=label.font;
            fillStatus.alignment=TextAlignmentOptions.Center; fillStatus.fontSize=0.62f;
            fillStatus.color=new Color(.18f,.12f,.08f,.95f); fillStatus.raycastTarget=false;
            var backGo=new GameObject("FillBarBackground"); backGo.transform.SetParent(transform,false);
            fillBarBackground=backGo.AddComponent<SpriteRenderer>(); fillBarBackground.sprite=OnePixelSprite();
            fillBarBackground.color=new Color(.06f,.04f,.03f,.72f); fillBarBackground.sortingOrder=4;
            var barGo=new GameObject("FillBar"); barGo.transform.SetParent(transform,false);
            fillBar=barGo.AddComponent<SpriteRenderer>(); fillBar.sprite=OnePixelSprite();
            fillBar.sortingOrder=5;
        }
        private static Sprite OnePixelSprite()
        {
            if(onePixelSprite==null) onePixelSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            return onePixelSprite;
        }
        public void Bind(FreeCafeRuntime runtime,WorldEntity entity,bool isPreview=false)
        {
            Runtime=runtime; EntityId=entity.id; preview=isPreview;
            Refresh(entity);
        }
        public void Refresh(WorldEntity entity)
        {
            EnsureFillIndicator();
            var spec=Runtime.World.Spec(entity.specId);
            int total=Runtime.World.TotalUnits(entity);
            string state=entity.task!=null ? entity.task.phase.ToString().ToLowerInvariant() : spec.Has(Capability.Container) ? (total==0 ? "empty" : total>=Mathf.Max(1,spec.capacity) ? "full" : entity.contents.Count>1 ? "mixed" : entity.contents.Count>0 && entity.contents[0].specId=="coffee" ? "coffee" : "milk") : "default";
            if(Runtime.World.ProductionEnabled && spec.Has(Capability.Processor)) state=entity.production.phase.ToString().ToLowerInvariant();
            if(specId!=entity.specId || lastState!=state)
            {
                specId=entity.specId; lastState=state;
                body.sprite=Runtime.artLibrary.Resolve(spec.artId,state);
                if(body.sprite!=null)
                {
                    var size=body.sprite.bounds.size;
                    float fit=Mathf.Min(spec.width/size.x,spec.height/size.y);
                    body.transform.localScale=new Vector3(fit,fit,1);
                }
                art.Bind(Runtime.artLibrary,spec.artId);
                hitbox.size=new Vector2(spec.width,spec.height);
                label.transform.localPosition=new Vector3(0,-spec.height*.5f-.18f,-.1f);
            }
            if(returnAnimation==null) transform.localPosition=new Vector3(entity.x,entity.y,0);
            body.transform.localRotation=Quaternion.Euler(0,0,entity.rotation);
            body.sortingOrder=preview ? 2000 : 100+Mathf.RoundToInt(-entity.y*10)+(spec.Has(Capability.Movable) ? 30 : 0);
            label.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+1;
            bool visible=string.IsNullOrEmpty(entity.ownerId);
            gameObject.SetActive(visible);
            hitbox.enabled=!preview && entity.area==Runtime.World.State.currentArea;
            string caption=spec.label;
            if(spec.Has(Capability.Source)) caption+=" ["+entity.stock+"]";
            if(Runtime.World.ProductionEnabled && (spec.Has(Capability.Container) || WorldVessel(entity)))
            {
                var snapshot=Runtime.World.LiquidSnapshot(entity);
                caption+=" · "+Runtime.World.ProductionCaption(entity)+" · "+snapshot.volumeMl.ToString("0")+"/"+snapshot.capacityMl.ToString("0")+"ml";
                UpdateProductionFill(entity,snapshot);
                if(liquidPresenter is ICafeLiquidPresenter presenter) presenter.Present(snapshot,0,false);
            }
            else if(spec.Has(Capability.Container))
            {
                bool full=total>=Mathf.Max(1,spec.capacity);
                caption+=" · 容量 "+total+"/"+spec.capacity+(full ? " · 容量已满" : total>0 ? " · 有内容" : " · 空杯");
                if(total>0 && entity.contents!=null)
                {
                    foreach(var portion in entity.contents)
                    {
                        var portionSpec=Runtime.World.Spec(portion.specId);
                        if(portionSpec!=null) caption+=" · "+portionSpec.label+"×"+portion.units;
                    }
                }
                UpdateFillIndicator(entity,spec,total);
            }
            else
            {
                fillStatus.gameObject.SetActive(false); fillBarBackground.gameObject.SetActive(false); fillBar.gameObject.SetActive(false);
            }
            if(Runtime.World.ProductionEnabled && spec.Has(Capability.Processor) && !WorldVessel(entity)) caption+=" · "+Runtime.World.ProductionCaption(entity);
            if(entity.task!=null)
            {
                if(entity.task.phase==MachinePhase.Running) caption+=" · "+entity.task.remaining.ToString("0.0")+"s";
                else if(entity.task.phase==MachinePhase.Loaded)
                {
                    var operation=Runtime.World.Operation(entity.task.operationId);
                    caption+=operation.control==OperationControl.Clicks ? " · 研磨 "+entity.task.clicksDone+"/"+Runtime.World.RequiredClicks(entity) : " · 已装料 · 点击操作";
                }
                else if(entity.task.phase==MachinePhase.Ready)
                {
                    var operation=Runtime.World.Operation(entity.task.operationId);
                    string output=Runtime.World.Spec(operation.outputSpecId).label;
                    caption+=" · 就绪 · "+(spec.dragOutput ? "拖出" : "拖杯接取")+output;
                }
            }
            if(spec.Has(Capability.Customer))
            {
                foreach(var request in Runtime.World.State.requests) if(request.customerId==entity.id) { caption=Runtime.World.Product(request.productId).label+" · "+request.patience.ToString("0")+"s"; break; }
            }
            if(Runtime.ShowAssetIds) caption+="\n["+spec.artId+"]";
            if(label.text!=caption) label.text=caption;
            selected=Runtime.SelectedId==EntityId;
            ApplyVisual();
        }


        public void AnimateReturn(Vector3 from,Vector3 to,bool destroyAfter)
        {
            if(returnAnimation!=null) StopCoroutine(returnAnimation);
            returnAnimation=StartCoroutine(ReturnRoutine(from,to,destroyAfter));
        }
        private IEnumerator ReturnRoutine(Vector3 from,Vector3 to,bool destroyAfter)
        {
            float elapsed=0;
            while(elapsed<.24f)
            {
                elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/.24f);t=t*t*(3-2*t);
                transform.position=Vector3.LerpUnclamped(from,to,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.25f);
                yield return null;
            }
            transform.position=to;returnAnimation=null;if(destroyAfter) Destroy(gameObject);
        }
        public void RejectDrop()
        {
            if(rejectionAnimation!=null) StopCoroutine(rejectionAnimation);
            rejectionAnimation=StartCoroutine(RejectRoutine());
        }
        private IEnumerator RejectRoutine()
        {
            invalidDrop=true;ApplyVisual();yield return new WaitForSecondsRealtime(.2f);invalidDrop=false;ApplyVisual();rejectionAnimation=null;
        }
        private void OnDisable()
        {
            if(returnAnimation!=null) StopCoroutine(returnAnimation);if(rejectionAnimation!=null) StopCoroutine(rejectionAnimation);
            returnAnimation=rejectionAnimation=null;invalidDrop=false;
        }
        private void Update()
        {
            if(Runtime==null || Runtime.World==null || !Runtime.World.ProductionEnabled || preview || body==null) return;
            var entity=Runtime.World.Find(EntityId);if(entity==null) return;
            if(entity.specId=="machine.grinder" && entity.production.phase==ProductionPhase.Processing)
            {
                float speed=entity.production.clickCooldown>0 ? 38 : 14;
                body.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.time*speed)*4);
            }
            if(entity.specId=="machine.heater")
                body.transform.localPosition=bodyOrigin+Vector3.up*(entity.production.phase==ProductionPhase.Boiling ? Mathf.Sin(Time.time*20)*.025f : 0);
        }

        private bool WorldVessel(WorldEntity entity) => Runtime.World.IsProductionVessel(entity);
        public void PresentPour(float tilt,bool pouring)
        {
            body.transform.localRotation=Quaternion.Euler(0,0,-tilt*75);
            if(liquidPresenter is ICafeLiquidPresenter presenter)
                presenter.Present(Runtime.World.LiquidSnapshot(Runtime.World.Find(EntityId)),tilt,pouring);
        }
        private void UpdateProductionFill(WorldEntity entity,CafeLiquidSnapshot snapshot)
        {
            UpdateFillIndicator(entity,Runtime.World.Spec(entity.specId),0);
            float fraction=snapshot.capacityMl>0 ? Mathf.Clamp01(snapshot.volumeMl/snapshot.capacityMl) : 0;
            const float width=2.25f;
            var pos=fillBar.transform.localPosition;
            fillBar.transform.localPosition=new Vector3(-width*.5f+width*fraction*.5f,pos.y,pos.z);
            fillBar.transform.localScale=new Vector3(Mathf.Max(.01f,width*fraction),.10f,1);
            fillBar.color=snapshot.temperature==LiquidTemperature.Boiling ? new Color(.95f,.45f,.18f) : snapshot.temperature==LiquidTemperature.Warm ? new Color(.95f,.74f,.3f) : new Color(.35f,.72f,.82f);
            fillStatus.text=snapshot.volumeMl.ToString("0")+" ml";
        }

        private void UpdateFillIndicator(WorldEntity entity,EntitySpec spec,int total)
        {
            float capacity=Mathf.Max(1,spec.capacity), fraction=Mathf.Clamp01(total/capacity);
            bool full=total>=capacity;
            fillStatus.gameObject.SetActive(true); fillBarBackground.gameObject.SetActive(true); fillBar.gameObject.SetActive(true);
            float y=-spec.height*.5f-.55f;
            fillStatus.transform.localPosition=new Vector3(0,y-.16f,-.12f);
            fillStatus.text=full ? "容量已满" : total>0 ? "有内容" : "空杯";
            fillStatus.color=full ? new Color(.12f,.52f,.18f) : total>0 ? new Color(.78f,.43f,.08f) : new Color(.30f,.30f,.30f);
            const float width=2.25f;
            fillBarBackground.transform.localPosition=new Vector3(0,y,.02f); fillBarBackground.transform.localScale=new Vector3(width,.10f,1);
            fillBar.transform.localPosition=new Vector3(-width*.5f+width*fraction*.5f,y,.01f); fillBar.transform.localScale=new Vector3(Mathf.Max(.02f,width*fraction),.10f,1);
            fillBar.color=full ? new Color(.20f,.75f,.28f) : total>0 ? new Color(.95f,.64f,.15f) : new Color(.48f,.48f,.48f);
            fillBarBackground.sortingOrder=body.sortingOrder+2; fillBar.sortingOrder=body.sortingOrder+3; fillStatus.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+4;
        }
        public void Highlight(bool isSelected,bool isInvalid)
        {
            selected=isSelected; invalidDrop=isInvalid; ApplyVisual();
        }
        public void SetHovered(bool value)
        {
            hovered=value; ApplyVisual();
        }
        public void SetDropTarget(bool active,bool invalid)
        {
            dropTarget=active; invalidDrop=active && invalid; ApplyVisual();
        }
        private void ApplyVisual()
        {
            if(body==null) return;
            body.color=invalidDrop ? new Color(1,.35f,.28f,1) : dropTarget ? new Color(.35f,1,.82f,1) : hovered ? new Color(1,.94f,.55f,1) : selected ? new Color(1,.84f,.4f,1) : preview ? new Color(1,1,1,.65f) : Color.white;
        }
        public void OnPointerEnter(PointerEventData e) { if(Runtime!=null && !Runtime.InputBlocked) { SetHovered(true); Runtime.Inspect(EntityId); } }
        public void OnPointerExit(PointerEventData e) { SetHovered(false); }
        public void OnPointerClick(PointerEventData e)
        {
            if(Runtime==null) return;
            if(e.button==PointerEventData.InputButton.Left) Runtime.Click(this);
            else if(e.button==PointerEventData.InputButton.Right) Runtime.CancelProcessing(this);
        }
        public void OnBeginDrag(PointerEventData e) { if(Runtime!=null && e.button==PointerEventData.InputButton.Left) Runtime.BeginDrag(this,e); }
        public void OnDrag(PointerEventData e) { if(Runtime!=null) Runtime.MoveDrag(e); }
        public void OnEndDrag(PointerEventData e) { if(Runtime!=null) Runtime.EndDrag(e); }
    }
}

