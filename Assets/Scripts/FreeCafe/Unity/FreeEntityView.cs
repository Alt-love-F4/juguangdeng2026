using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>Thin input/render adapter. Identity and processing never live in the GameObject.</summary>
    public sealed class FreeEntityView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        [Header("Prefab / scene authoring")]
        [SerializeField] private string prefabSpecId;
        [SerializeField] private string authoringEntityId;
        public string PrefabSpecId => prefabSpecId;
        public string AuthoringEntityId => authoringEntityId;
        [Header("Presentation extension anchors (local world units)")]
        public Transform effectsRoot, liquidRoot, pourOrigin;
        public MonoBehaviour liquidPresenter;
        public SpriteRenderer body;
        public TMP_Text label;
        public TMP_Text workspaceCaption;
        public BoxCollider2D hitbox;
        public string EntityId { get; private set; }
        public FreeCafeRuntime Runtime { get; private set; }
        private Sprite lastCharacterSprite;
        private string specId,lastState,lastArtId;
        private FreeCafePersonView personView;
        private FreeCafeBusinessSignView businessSignView;
        private bool preview;
        private bool hovered, selected, dropTarget, invalidDrop;
        private TMP_Text fillStatus;
        private SpriteRenderer fillBarBackground, fillBar;
        private static Sprite onePixelSprite;
        private Coroutine returnAnimation, rejectionAnimation;
        private Vector3 bodyOrigin;
        private OrthographicGlassCupPresenter glassCup;
        private Canvas glassCanvas;
        private bool pourVisual, receivingPour;
        public bool IsDropTarget => dropTarget;
        public bool IsReceivingPour => receivingPour;

        private void Awake()
        {
            EnsureFillIndicator();
            EnsureGlassCup();
            if(body!=null) bodyOrigin=body.transform.localPosition;
        }
        private void EnsureFillIndicator()
        {
            // Prefab 自带状态节点；兼容旧版缺节点的资产，但不重复生成。
            if(fillStatus==null && transform.Find("FillStatus")!=null) fillStatus=transform.Find("FillStatus").GetComponent<TMP_Text>();
            if(fillBarBackground==null && transform.Find("FillBarBackground")!=null) fillBarBackground=transform.Find("FillBarBackground").GetComponent<SpriteRenderer>();
            if(fillBar==null && transform.Find("FillBar")!=null) fillBar=transform.Find("FillBar").GetComponent<SpriteRenderer>();
            if(fillStatus!=null && fillBarBackground!=null && fillBar!=null)
            {
                if(fillBarBackground.sprite==null) fillBarBackground.sprite=OnePixelSprite();
                if(fillBar.sprite==null) fillBar.sprite=OnePixelSprite();
                return;
            }
            if(fillStatus==null)
            {
                var statusGo=new GameObject("FillStatus"); statusGo.transform.SetParent(transform,false);
                fillStatus=statusGo.AddComponent<TextMeshPro>();
                if(label!=null) fillStatus.font=label.font;
                fillStatus.alignment=TextAlignmentOptions.Center; fillStatus.fontSize=0.62f;
                fillStatus.color=new Color(.18f,.12f,.08f,.95f); fillStatus.raycastTarget=false;
            }
            if(fillBarBackground==null)
            {
                var backGo=new GameObject("FillBarBackground"); backGo.transform.SetParent(transform,false);
                fillBarBackground=backGo.AddComponent<SpriteRenderer>(); fillBarBackground.sprite=OnePixelSprite();
                fillBarBackground.color=new Color(.06f,.04f,.03f,.72f); fillBarBackground.sortingOrder=4;
            }
            if(fillBar==null)
            {
                var barGo=new GameObject("FillBar"); barGo.transform.SetParent(transform,false);
                fillBar=barGo.AddComponent<SpriteRenderer>(); fillBar.sprite=OnePixelSprite();
                fillBar.sortingOrder=5;
            }
        }
        private static Sprite OnePixelSprite()
        {
            if(onePixelSprite==null) onePixelSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            return onePixelSprite;
        }

        public void ConfigureGlassCup()
        {
            EnsureGlassCup();
        }

        private void EnsureGlassCup()
        {
            if (prefabSpecId != "cup.small" && prefabSpecId != "cup.large" && prefabSpecId != "machine.heater" && prefabSpecId != "machine.brewer") return;
            if (liquidPresenter != null && !(liquidPresenter is OrthographicGlassCupPresenter)) return;
            if (glassCup == null)
            {
                if (liquidRoot == null)
                {
                    var anchor = new GameObject("Liquid");
                    anchor.transform.SetParent(transform, false);
                    liquidRoot = anchor.transform;
                }
                glassCanvas = liquidRoot.GetComponentInChildren<Canvas>(true);
                glassCup = liquidPresenter as OrthographicGlassCupPresenter;
                if (glassCup == null) glassCup = liquidRoot.GetComponentInChildren<OrthographicGlassCupPresenter>(true);
                if (glassCup == null)
                {
                    var canvasObject = new GameObject("GlassCupCanvas", typeof(RectTransform), typeof(Canvas));
                    canvasObject.transform.SetParent(liquidRoot, false);
                    canvasObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                    glassCanvas = canvasObject.GetComponent<Canvas>();
                    glassCanvas.renderMode = RenderMode.WorldSpace;
                    glassCanvas.overrideSorting = true;
                    glassCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
                    var surface = new GameObject("GlassSurface", typeof(RectTransform));
                    surface.transform.SetParent(canvasObject.transform, false);
                    glassCup = surface.AddComponent<OrthographicGlassCupPresenter>();
                }
                liquidPresenter = glassCup;
            }
            glassCup.ConfigureSize(prefabSpecId != "cup.small");
            glassCup.ConfigureMotionSpace(transform.parent);
            if (body != null)
            {
                body.enabled = true;
                glassCup.gameObject.SetActive(pourVisual || receivingPour);
                if (glassCanvas != null)
                {
                    glassCanvas.sortingLayerID = body.sortingLayerID;
                    glassCanvas.sortingOrder = body.sortingOrder;
                }
            }
        }

        /// <summary>编辑器和运行时共用同一个 Prefab；这里只设置初始外观，不创建世界或扣库存。</summary>
        public void ConfigureAuthoring(EntitySpec spec,CafeArtLibrary library,string entityId,float x,float y)
        {
            if(spec==null || library==null || body==null || label==null || hitbox==null)
                throw new System.InvalidOperationException("实体 Prefab 缺少规格或必需组件");
            prefabSpecId=spec.id;authoringEntityId=entityId;
            EnsureGlassCup();
            transform.localPosition=new Vector3(x,y,0);
            body.sprite=library.Resolve(spec.artId,"empty");
            if(body.sprite!=null)
            {
                var size=body.sprite.bounds.size;
                float fit=Mathf.Min(spec.width/size.x,spec.height/size.y);
                body.transform.localScale=new Vector3(fit,fit,1);
            }
            hitbox.size=new Vector2(spec.width,spec.height);hitbox.isTrigger=true;
            label.transform.localPosition=spec.Has(Capability.BusinessControl) ? new Vector3(0,.3f,-.1f) : new Vector3(0,-spec.height*.5f-.18f,-.1f);
            label.text=spec.label;label.raycastTarget=false;
            body.sortingOrder=100+Mathf.RoundToInt(-y*10)+(spec.Has(Capability.Movable) ? 30 : 0);
            var labelRenderer=label.GetComponent<MeshRenderer>();if(labelRenderer!=null) labelRenderer.sortingOrder=body.sortingOrder+1;
            if(workspaceCaption!=null) workspaceCaption.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+2;
            var sign=GetComponent<FreeCafeBusinessSignView>();if(sign!=null) sign.UpdateSorting(body.sortingOrder);
            UpdateGlassTransform();
        }
        public void Bind(FreeCafeRuntime runtime,WorldEntity entity,bool isPreview=false)
        {
            if(runtime==null || entity==null) throw new System.ArgumentNullException("实体绑定缺少 runtime 或实体");
            if(!string.IsNullOrEmpty(prefabSpecId) && prefabSpecId!=entity.specId)
                throw new System.InvalidOperationException("Prefab 规格不匹配："+prefabSpecId+" / "+entity.specId);
            Runtime=runtime; EntityId=entity.id; preview=isPreview;
            personView=GetComponent<FreeCafePersonView>();businessSignView=GetComponent<FreeCafeBusinessSignView>();
            Refresh(entity);
        }
        public void Refresh(WorldEntity entity)
        {
            EnsureFillIndicator();
            EnsureGlassCup();
            var spec=Runtime.World.Spec(entity.specId);
            if (Runtime.World.ProductionEnabled && entity.area == AreaId.Counter && !spec.Has(Capability.Customer)) label.fontSize=2.2f;
            int total=Runtime.World.TotalUnits(entity);
            string state=entity.task!=null ? entity.task.phase.ToString().ToLowerInvariant() : spec.Has(Capability.Container) ? (total==0 ? "empty" : total>=Mathf.Max(1,spec.capacity) ? "full" : entity.contents.Count>1 ? "mixed" : entity.contents.Count>0 && entity.contents[0].specId=="coffee" ? "coffee" : "milk") : "default";
            if(Runtime.World.ProductionEnabled && spec.Has(Capability.Processor)) state=entity.production.phase.ToString().ToLowerInvariant();
            string artId=spec.Has(Capability.Customer) && entity.person!=null && !string.IsNullOrEmpty(entity.person.visualId) && Runtime.artLibrary.Find(entity.person.visualId)!=null
                ? entity.person.visualId : spec.artId;
            var character=entity.person!=null ? Runtime.Character(entity.person.characterId) : null;
            var characterSprite=character!=null ? character.worldSprite : null;
            if(specId!=entity.specId || lastState!=state || lastArtId!=artId || lastCharacterSprite!=characterSprite)
            {
                specId=entity.specId; lastState=state; lastArtId=artId; lastCharacterSprite=characterSprite;
                body.sprite=characterSprite!=null ? characterSprite : Runtime.artLibrary.Resolve(artId,state);
                if(body.sprite!=null)
                {
                    var size=body.sprite.bounds.size;
                    float fit=Mathf.Min(spec.width/size.x,spec.height/size.y);
                    body.transform.localScale=new Vector3(fit,fit,1);
                }
                var deliveryZone = GetComponent<FreeCafeDeliveryZone>();
                hitbox.size=deliveryZone!=null ? deliveryZone.HitboxSize : new Vector2(spec.width,spec.height);
                label.transform.localPosition=spec.Has(Capability.BusinessControl) ? new Vector3(0,.3f,-.1f) : new Vector3(0,-spec.height*.5f-.18f,-.1f);
            }
            if(returnAnimation==null) transform.localPosition=new Vector3(entity.x,entity.y,0);
            body.transform.localRotation=Quaternion.Euler(0,0,entity.rotation);
            body.sortingOrder=preview ? 2000 : 100+Mathf.RoundToInt(-entity.y*10)+(spec.Has(Capability.Movable) ? 30 : 0);
            label.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+1;
            if(workspaceCaption!=null) workspaceCaption.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+2;
            UpdateGlassTransform();
            bool visible=string.IsNullOrEmpty(entity.ownerId);
            gameObject.SetActive(visible);
            hitbox.enabled=!preview && entity.area==Runtime.World.State.currentArea;
            string caption=spec.label;
            if(spec.Has(Capability.Source)) caption+=" ["+entity.stock+"]";
            if(Runtime.World.ProductionEnabled && (spec.Has(Capability.Container) || WorldVessel(entity)))
            {
                var snapshot=Runtime.World.LiquidSnapshot(entity);
                if (glassCup != null) glassCup.gameObject.SetActive(pourVisual || receivingPour);
                if (hovered) caption+=" · "+Runtime.World.ContainerDescription(entity);
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
                if(liquidPresenter is ICafeLiquidPresenter presenter) presenter.Present(Runtime.World.LiquidSnapshot(entity),0,false);
            }
            else
            {
                fillStatus.gameObject.SetActive(false); fillBarBackground.gameObject.SetActive(false); fillBar.gameObject.SetActive(false);
            }
            if(Runtime.World.ProductionEnabled && hovered && spec.Has(Capability.Processor) && !WorldVessel(entity)) caption+=" · "+Runtime.World.ProductionCaption(entity);
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
            if(personView!=null) personView.Present(entity);
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
            pourVisual = pouring || Mathf.Abs(tilt) > .001f;
            if (glassCup != null) glassCup.gameObject.SetActive(pourVisual || receivingPour);
            ApplyVisual();
            body.transform.localRotation=Quaternion.Euler(0,0,-tilt*75);
            UpdateGlassTransform();
            RefreshLiquid(tilt,pouring);
        }
        public void SetReceivingPour(bool value)
        {
            receivingPour=value;
            if(glassCup!=null) glassCup.gameObject.SetActive(pourVisual || receivingPour);
            RefreshLiquid();
            ApplyVisual();
        }
        public void RefreshLiquid(float tilt=0f,bool pouring=false)
        {
            if(Runtime==null || Runtime.World==null) return;
            if(liquidPresenter is ICafeLiquidPresenter presenter)
                presenter.Present(Runtime.World.LiquidSnapshot(Runtime.World.Find(EntityId)),tilt,pouring);
        }

        private void UpdateGlassTransform()
        {
            if (glassCup == null || body == null) return;
            liquidRoot.localRotation = body.transform.localRotation;
            if (glassCanvas != null) glassCanvas.sortingOrder = body.sortingOrder;
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
            fillStatus.fontSize=1.8f;
            fillStatus.text=snapshot.volumeMl.ToString("0")+"/"+snapshot.capacityMl.ToString("0")+" ml · "+Runtime.World.SolidMass(entity).ToString("0.0")+" g";
            fillStatus.gameObject.SetActive(hovered); fillBarBackground.gameObject.SetActive(hovered); fillBar.gameObject.SetActive(hovered);
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
            if(Runtime!=null && Runtime.World!=null)
            { var entity=Runtime.World.Find(EntityId); if(Runtime.World.IsContainer(entity)) UpdateProductionFill(entity,Runtime.World.LiquidSnapshot(entity)); }
        }
        public void SetDropTarget(bool active,bool invalid)
        {
            dropTarget=active; invalidDrop=active && invalid; ApplyVisual();
        }
        private void ApplyVisual()
        {
            if(body==null) return;
            if(businessSignView!=null && Runtime!=null) businessSignView.Present(hovered);
            body.color=invalidDrop ? new Color(1,.35f,.28f,1) : dropTarget ? new Color(.35f,1,.82f,1) : hovered ? new Color(1,.94f,.55f,1) : selected ? new Color(1,.84f,.4f,1) : preview ? new Color(1,1,1,.65f) : Color.white;
            if(pourVisual || receivingPour) { var tint=body.color; tint.a=.22f; body.color=tint; }
            if(glassCup!=null) glassCup.SetTint(Color.white);
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

