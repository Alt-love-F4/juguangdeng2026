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
        [Tooltip("Keep a scene-authored static prop at its edited position, including after save restoration.")]
        public bool preserveAuthoredPosition;
        public string PrefabSpecId => prefabSpecId;
        public string AuthoringEntityId => authoringEntityId;
        [Header("Presentation extension anchors (local world units)")]
        public Transform effectsRoot, liquidRoot, pourOrigin;
        public MonoBehaviour liquidPresenter;
        [Header("Liquid container silhouette")]
        [SerializeField] private Texture2D liquidContainerMask;
        [SerializeField] private bool liquidMaskIsDistanceField;
        [SerializeField, Min(.001f)] private float liquidMaskDistanceRange = .125f;
        [SerializeField, Range(0f, .15f)] private float liquidWallThickness = .018f;
        public SpriteRenderer body;
        public TMP_Text label;
        public TMP_Text workspaceCaption;
        public BoxCollider2D hitbox;
        public string EntityId { get; private set; }
        public FreeCafeRuntime Runtime { get; private set; }
        private bool hasAuthoredPresentation;
        private bool hasAuthoredPosition;
        private Vector3 authoredPosition;
        private Vector3 authoredArtworkSize, authoredArtworkScale;
        private Sprite lastCharacterSprite;
        private string specId,lastState,lastArtId;
        private FreeCafePersonView personView;
        private FreeCafeCupStackVisual cupStack;
        private FreeCafeBusinessSignView businessSignView;
        private bool preview, retired;
        public bool CanInteract => !preview && !retired && !destroyOnArrival && isActiveAndEnabled &&
            Runtime != null && Runtime.IsRegisteredView(this);
        private bool hovered, selected, dropTarget, invalidDrop;
        private bool extractionFilling;
        public bool IsReceivingPour => receivingPour;
        /// <summary>The real-time GravityLiquidImage is on screen (pouring, receiving or extracting).</summary>
        public bool LiquidVisualActive => pourVisual || receivingPour || extractionFilling;
        private FreeCafeCupDrinkSurface drinkSurface;
        public bool IsHovered => hovered;
        private TMP_Text fillStatus;
        private SpriteRenderer fillBarBackground, fillBar;
        private static Sprite onePixelSprite;
        [Header("Interaction feel")]
        [Min(.01f)] public float dragSmoothTime = .055f;
        [Min(.01f)] public float releaseSmoothTime = .18f;
        [Range(0f, 30f)] public float maxDragLean = 14f;
        [Min(0f)] public float leanPerSpeed = 1.6f;
        [Min(.01f)] public float leanSmoothTime = .09f;
        private Vector3 motionTarget, motionVelocity;
        private float dragLean, leanVelocity, baseRotation, pourRotation;
        private bool motionInitialized, pointerMotion, motionFrozen, destroyOnArrival;
        private float retireElapsed;
        public Vector3 DragPosition => transform.parent != null ? transform.parent.TransformPoint(motionTarget) : motionTarget;
        private Coroutine rejectionAnimation;
        private Coroutine pickupReveal;
        private Vector3 pickupBodyScale;
        private bool arrivalFading;
        private float arrivalFadeElapsed, arrivalFadeDuration, arrivalFade = 1f;
        private SpriteRenderer[] arrivalSprites;
        private TMP_Text[] arrivalTexts;
        private float[] arrivalSpriteAlpha, arrivalTextAlpha;
        private OrthographicGlassCupPresenter glassCup;
        private Canvas glassCanvas;
        private bool pourVisual, receivingPour;
        public bool IsDropTarget => dropTarget;


        private void Awake()
        {
            EnsureFillIndicator();
            EnsureGlassCup();
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

        public void ConfigureLiquidContainerMask(Texture2D mask, bool signedDistance, float distanceRange, float inset)
        {
            liquidContainerMask = mask;
            liquidMaskIsDistanceField = signedDistance;
            liquidMaskDistanceRange = Mathf.Max(.001f, distanceRange);
            liquidWallThickness = Mathf.Clamp(inset, 0f, .15f);
            EnsureGlassCup();
        }

        private void EnsureGlassCup()
        {
            if (prefabSpecId != "cup.small" && prefabSpecId != "cup.large" && prefabSpecId != "machine.heater" && prefabSpecId != "machine.brewer" && prefabSpecId != "container.extractor" && prefabSpecId != "container.milk-pitcher" && prefabSpecId != "container.extractor.filter" && liquidContainerMask == null) return;
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
            glassCup.ConfigureContainerMask(liquidContainerMask, liquidMaskIsDistanceField, liquidMaskDistanceRange, liquidWallThickness);
            if (liquidContainerMask == null)
            {
                glassCup.ConfigureSize(prefabSpecId != "cup.small");
                if(prefabSpecId=="container.milk-pitcher") glassCup.ConfigureSize(new Vector2(.82f,1.02f));
                if (prefabSpecId == "container.extractor")
                {
                    glassCup.ConfigureSize(new Vector2(.84f, .89f));
                    glassCanvas.transform.localPosition = new Vector3(-.17f, -.04f, -.01f);
                }
            }
            glassCup.ConfigureMotionSpace(transform.parent);
            if (body != null)
            {
                body.enabled = true;
                glassCup.gameObject.SetActive(pourVisual || receivingPour || extractionFilling);
                if (glassCanvas != null)
                {
                    glassCanvas.sortingLayerID = body.sortingLayerID;
                    glassCanvas.sortingOrder = body.sortingOrder + (liquidContainerMask != null ? 1 : 0);
                }
            }
            UpdateGlassTransform();
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
            if (!spec.Has(Capability.BusinessControl) || GetComponent<FreeCafeBusinessSignView>() == null)
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
            // Capture after adoption into the area root, before world/save coordinates can move a static prop.
            if (Runtime == null && preserveAuthoredPosition && !string.IsNullOrEmpty(authoringEntityId))
            {
                authoredPosition = transform.localPosition;
                hasAuthoredPosition = true;
            }
            // Capture scene edits before the first runtime refresh can fit art to catalog dimensions.
            if (Runtime == null && !string.IsNullOrEmpty(authoringEntityId) && body != null && body.sprite != null)
            {
                hasAuthoredPresentation = true;
                authoredArtworkScale = body.transform.localScale;
                authoredArtworkSize = Vector3.Scale(body.sprite.bounds.size, authoredArtworkScale);
            }
            StopPickupReveal();
            Runtime=runtime; EntityId=entity.id; preview=isPreview;
            if((Runtime.World.IsPrecisionExtractor(entity) || Runtime.World.IsExtractionFilter(entity)) && GetComponent<FreeCafeExtractorVisual>()==null)
                gameObject.AddComponent<FreeCafeExtractorVisual>();
            personView=GetComponent<FreeCafePersonView>();businessSignView=GetComponent<FreeCafeBusinessSignView>();
            Refresh(entity);
        }
        public void Refresh(WorldEntity entity)
        {
            if (retired) return;
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
            var visitSprite=Runtime.CafeVisitSprite(entity.id);
            var characterSprite=(Runtime.seating != null ? Runtime.seating.Artwork(entity, visitSprite) : visitSprite)
                ?? (character!=null ? character.worldSprite : null);
            if(specId!=entity.specId || lastState!=state || lastArtId!=artId || lastCharacterSprite!=characterSprite)
            {
                specId=entity.specId; lastState=state; lastArtId=artId; lastCharacterSprite=characterSprite;
                body.sprite=characterSprite!=null ? characterSprite : Runtime.artLibrary.Resolve(artId,state);
                if(body.sprite!=null)
                {
                    var size=body.sprite.bounds.size;
                    var plate = GetComponent<FreeCafeDeliveryZone>();
                    var artworkSize = plate != null ? plate.ArtworkSize : new Vector2(spec.width, spec.height);
                    float fit=Mathf.Min(artworkSize.x/size.x,artworkSize.y/size.y);
                    body.transform.localScale = hasAuthoredPresentation
                        ? new Vector3(authoredArtworkSize.x / size.x, authoredArtworkSize.y / size.y, authoredArtworkScale.z)
                        : new Vector3(fit,fit,1);
                    if (!hasAuthoredPresentation && plate != null) { body.transform.localPosition = plate.ArtworkOffset; body.enabled = true; }
                }
                var deliveryZone = GetComponent<FreeCafeDeliveryZone>();
                if (!hasAuthoredPresentation)
                {
                    if (businessSignView == null) hitbox.size=deliveryZone!=null ? deliveryZone.HitboxSize : new Vector2(spec.width,spec.height);
                    if (deliveryZone != null) hitbox.offset = deliveryZone.ArtworkOffset;
                    if (businessSignView == null) label.transform.localPosition=deliveryZone!=null
                        ? new Vector3(0,deliveryZone.ArtworkOffset.y-deliveryZone.ArtworkSize.y*.5f-.2f,-.1f)
                        : spec.Has(Capability.BusinessControl) ? new Vector3(0,.3f,-.1f) : new Vector3(0,-spec.height*.5f-.18f,-.1f);
                }
            }
            Vector3 entityPosition = hasAuthoredPosition ? authoredPosition : new Vector3(entity.x, entity.y, 0);
            if (!motionInitialized)
            {
                transform.localPosition = entityPosition;
                motionTarget = transform.localPosition;
                motionInitialized = true;
            }
            else if (!pointerMotion && !destroyOnArrival) motionTarget = entityPosition;
            baseRotation = entity.rotation;
            body.sortingOrder=preview ? 2000 : 100+Mathf.RoundToInt(-entity.y*10)+(spec.Has(Capability.Movable) ? 30 : 0);
            label.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+1;
            if(workspaceCaption!=null) workspaceCaption.GetComponent<MeshRenderer>().sortingOrder=body.sortingOrder+2;
            UpdateGlassTransform();
            if(entity.specId=="cup.small" || entity.specId=="cup.large" || entity.specId=="source.cups.small" || entity.specId=="source.cups.large")
            {
                if(cupStack==null) cupStack=GetComponent<FreeCafeCupStackVisual>();
                if(cupStack==null) cupStack=gameObject.AddComponent<FreeCafeCupStackVisual>();
                cupStack.Present(spec,Runtime.World.Catalog,Runtime.artLibrary,entity.stock,Runtime.World.CupAbove(entity)!=null);
            }
            bool visible=string.IsNullOrEmpty(entity.ownerId) && Runtime.World.IsEquipmentAvailable(entity);
            gameObject.SetActive(visible);
            UpdateDrinkSurface(entity);
            hitbox.enabled=CanInteract && !pointerMotion && visible && entity.area==Runtime.World.State.currentArea;
            string caption=spec.label;
            if(spec.Has(Capability.Source)) caption+=" ["+entity.stock+"]";
            if(Runtime.World.ProductionEnabled && (spec.Has(Capability.Container) || WorldVessel(entity)))
            {
                var snapshot=Runtime.World.LiquidSnapshot(entity);
                if (glassCup != null) glassCup.gameObject.SetActive(pourVisual || receivingPour || extractionFilling);
                // Ingredients live in the secondary card, never in the entity's caption.
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
                foreach(var request in Runtime.World.State.requests) if(request.customerId==entity.id) { caption=FreeCafeProductionText.Recipe(Runtime.World.Product(request.productId).label)+" · "+request.patience.ToString("0")+"s"; break; }
            }
            if(Runtime.ShowAssetIds) caption+="\n["+spec.artId+"]";
            if(label.text!=caption) label.text=caption;
            selected=Runtime.SelectedId==EntityId;
            if(personView!=null) personView.Present(entity);
            if(Runtime.seating!=null) Runtime.seating.Present(this,entity);
            ApplyVisual();
        }


        public void StartMotionFrom(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            motionVelocity = Vector3.zero;
        }
        public void TeleportPresentation(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            motionTarget = transform.localPosition;
            motionVelocity = Vector3.zero;
            motionInitialized = true;
        }
        public void PlayArrivalFade(float duration = .45f)
        {
            arrivalSprites = GetComponentsInChildren<SpriteRenderer>(true);
            arrivalTexts = GetComponentsInChildren<TMP_Text>(true);
            arrivalSpriteAlpha = new float[arrivalSprites.Length];
            arrivalTextAlpha = new float[arrivalTexts.Length];
            for (int i = 0; i < arrivalSprites.Length; i++) arrivalSpriteAlpha[i] = arrivalSprites[i].color.a;
            for (int i = 0; i < arrivalTexts.Length; i++) arrivalTextAlpha[i] = arrivalTexts[i].color.a;
            arrivalFadeDuration = Mathf.Max(.01f, duration);
            arrivalFadeElapsed = 0f;
            arrivalFade = 0f;
            arrivalFading = true;
            ApplyArrivalFade();
            ApplyVisual();
        }

        private void ApplyArrivalFade()
        {
            if (!arrivalFading) return;
            for (int i = 0; i < arrivalSprites.Length; i++)
            {
                if (arrivalSprites[i] == null) continue;
                var color = arrivalSprites[i].color;
                color.a = arrivalSpriteAlpha[i] * arrivalFade;
                arrivalSprites[i].color = color;
            }
            for (int i = 0; i < arrivalTexts.Length; i++)
            {
                if (arrivalTexts[i] == null) continue;
                var color = arrivalTexts[i].color;
                color.a = arrivalTextAlpha[i] * arrivalFade;
                arrivalTexts[i].color = color;
            }
        }

        private void UpdateArrivalFade()
        {
            if (!arrivalFading) return;
            arrivalFadeElapsed += Time.unscaledDeltaTime;
            arrivalFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arrivalFadeElapsed / arrivalFadeDuration));
            ApplyArrivalFade();
            if (arrivalFadeElapsed < arrivalFadeDuration) return;
            arrivalFade = 1f;
            ApplyArrivalFade();
            arrivalFading = false;
            arrivalSprites = null;
            arrivalTexts = null;
            arrivalSpriteAlpha = null;
            arrivalTextAlpha = null;
        }

        public void PlayPickupReveal()
        {
            StopPickupReveal();
            if(body==null || !isActiveAndEnabled) return;
            pickupBodyScale=body.transform.localScale;
            pickupReveal=StartCoroutine(PickupRevealRoutine());
        }
        private IEnumerator PickupRevealRoutine()
        {
            float elapsed=0;
            while(elapsed<.14f && body!=null)
            {
                float t=Mathf.Clamp01(elapsed/.14f)-1;
                float eased=1+2.70158f*t*t*t+1.70158f*t*t;
                float scale=Mathf.LerpUnclamped(.78f,1,eased);
                body.transform.localScale=Vector3.Scale(pickupBodyScale,new Vector3(scale,scale,1));
                yield return null;
                elapsed+=Time.unscaledDeltaTime;
            }
            if(body!=null) body.transform.localScale=pickupBodyScale;
            pickupReveal=null;
        }
        private void StopPickupReveal()
        {
            if(pickupReveal==null) return;
            StopCoroutine(pickupReveal);
            pickupReveal=null;
            if(body!=null) body.transform.localScale=pickupBodyScale;
        }
        public void BeginPointerMotion()
        {
            pointerMotion = true;
            motionFrozen = false;
            destroyOnArrival = false;
            motionTarget = transform.localPosition;
        }
        public void MovePointer(Vector3 worldPosition)
        {
            motionFrozen = false;
            motionTarget = transform.parent != null ? transform.parent.InverseTransformPoint(worldPosition) : worldPosition;
        }
        public void EndPointerMotion() { pointerMotion = false; motionFrozen = false; }
        public void FreezePointerMotion()
        {
            motionFrozen = true;
            motionTarget = transform.localPosition;
            motionVelocity = Vector3.zero;
        }
        public void RetireInteraction()
        {
            retired = true;
            hovered = selected = dropTarget = invalidDrop = false;
            if (hitbox != null) hitbox.enabled = false;
        }
        public void AnimateReturn(Vector3 from,Vector3 to,bool destroyAfter)
        {
            motionInitialized = true;
            pointerMotion = false;
            MovePointer(to);
            // Only detached previews/consumed items may retire. A registered world
            // entity must survive even if a caller retained stale preview state.
            destroyAfter = destroyAfter && (Runtime == null || !Runtime.IsRegisteredView(this));
            destroyOnArrival = destroyAfter;
            retireElapsed = 0;
            motionVelocity = Vector3.zero;
            if (destroyAfter) RetireInteraction();
        }
        private void LateUpdate()
        {
            UpdateArrivalFade();
            if (!motionInitialized) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            float smoothTime = pointerMotion ? Mathf.Min(dragSmoothTime, .035f) : Mathf.Min(releaseSmoothTime, .07f);
            if (destroyOnArrival)
            {
                retireElapsed += Time.unscaledDeltaTime;
                var tint = body.color; tint.a = Mathf.Clamp01(1 - retireElapsed / .12f); body.color = tint;
                if (retireElapsed >= .12f) { Destroy(gameObject); return; }
            }
            if (!motionFrozen)
                transform.localPosition = Vector3.SmoothDamp(transform.localPosition, motionTarget, ref motionVelocity,
                    Mathf.Max(.01f, smoothTime), Mathf.Infinity, dt);
            float targetLean = pointerMotion && !pourVisual
                ? Mathf.Clamp(motionVelocity.x * Mathf.Min(leanPerSpeed, .7f), -Mathf.Min(maxDragLean, 6), Mathf.Min(maxDragLean, 6)) : 0f;
            dragLean = Mathf.SmoothDampAngle(dragLean, targetLean, ref leanVelocity,
                Mathf.Max(.01f, leanSmoothTime), Mathf.Infinity, dt);
            if (body != null) body.transform.localRotation = Quaternion.Euler(0, 0,
                pourVisual ? pourRotation : baseRotation + dragLean);
            UpdateGlassTransform();
            if ((transform.localPosition - motionTarget).sqrMagnitude < .000001f && motionVelocity.sqrMagnitude < .0001f)
            {
                transform.localPosition = motionTarget;
                motionVelocity = Vector3.zero;
                if (destroyOnArrival) Destroy(gameObject);
            }
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
            StopPickupReveal();
            if(rejectionAnimation!=null) StopCoroutine(rejectionAnimation);
            rejectionAnimation=null;invalidDrop=false;
        }
        private bool WorldVessel(WorldEntity entity) => Runtime.World.IsProductionVessel(entity);
        /// <summary>Recognised drinks replace the cup mouth with that drink's surface art.</summary>
        private void UpdateDrinkSurface(WorldEntity entity)
        {
            var library = CafeDrinkSurfaceLibrary.Default;
            if (drinkSurface == null && (library == null || library.Cup(entity.specId) == null)) return;
            if (drinkSurface == null)
            {
                drinkSurface = GetComponent<FreeCafeCupDrinkSurface>();
                if (drinkSurface == null) drinkSurface = gameObject.AddComponent<FreeCafeCupDrinkSurface>();
                drinkSurface.Bind(this, library);
            }
            // A cup nested under another cup shows only its rim; contents stay hidden.
            var kind = Runtime.World.CupAbove(entity) == null ? Runtime.World.ClassifyCoffee(entity) : default;
            drinkSurface.Present(entity.specId, kind.known ? kind.productId : null, preview);
        }
        public void PresentPour(float tilt,bool pouring)
        {
            pourVisual = pouring || Mathf.Abs(tilt) > .001f;
            if (glassCup != null) glassCup.gameObject.SetActive(pourVisual || receivingPour || extractionFilling);
            ApplyVisual();
            pourRotation = -tilt * (Runtime != null ? Runtime.maxPourAngle : 105f);
            body.transform.localRotation=Quaternion.Euler(0,0,pourVisual ? pourRotation : baseRotation + dragLean);
            UpdateGlassTransform();
            RefreshLiquid(tilt,pouring);
        }
        public void SetExtractionFilling(bool value)
        {
            if (extractionFilling == value) return;
            extractionFilling = value;
            if (glassCup != null) glassCup.gameObject.SetActive(pourVisual || receivingPour || extractionFilling);
            ApplyVisual();
        }
        public void SetReceivingPour(bool value)
        {
            receivingPour=value;
            if(glassCup!=null) glassCup.gameObject.SetActive(pourVisual || receivingPour || extractionFilling);
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
            if (liquidContainerMask != null && body.sprite != null)
            {
                liquidRoot.localPosition = body.transform.localPosition;
                var scale = body.transform.localScale;
                if (body.flipX) scale.x = -scale.x;
                if (body.flipY) scale.y = -scale.y;
                liquidRoot.localScale = scale;
                if (glassCanvas != null)
                {
                    glassCanvas.transform.localPosition = new Vector3(0, 0, -.01f);
                    glassCanvas.transform.localRotation = Quaternion.identity;
                    glassCanvas.transform.localScale = Vector3.one;
                }
                glassCup.FitSprite(body.sprite);
            }
            if (glassCanvas != null)
            {
                glassCanvas.sortingLayerID = body.sortingLayerID;
                glassCanvas.sortingOrder = body.sortingOrder + (liquidContainerMask != null ? 1 : 0);
            }
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
            fillStatus.gameObject.SetActive(false); fillBarBackground.gameObject.SetActive(false); fillBar.gameObject.SetActive(false);
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
            {
                var entity=Runtime.World.Find(EntityId);
                if(Runtime.World.IsContainer(entity))
                {
                    if(value) Runtime.ContainerPanels?.Inspect(EntityId);
                    else Runtime.ContainerPanels?.ClearInspection(EntityId);
                }
            }
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
            if (arrivalFading) { var tint=body.color; tint.a*=arrivalFade; body.color=tint; }
            if(pourVisual || receivingPour || extractionFilling) { var tint=body.color; tint.a=.22f; body.color=tint; }
            if(glassCup!=null) glassCup.SetTint(Color.white);
        }
        public void OnPointerEnter(PointerEventData e) { if(CanInteract && !Runtime.InputBlocked) { SetHovered(true); Runtime.Inspect(EntityId); } }
        public void OnPointerExit(PointerEventData e) { SetHovered(false); }
        public void OnPointerClick(PointerEventData e)
        {
            if(!CanInteract) return;
            if(e.button==PointerEventData.InputButton.Left) Runtime.Click(this);
            else if(e.button==PointerEventData.InputButton.Right) Runtime.CancelProcessing(this);
        }
        public void OnBeginDrag(PointerEventData e) { if(CanInteract && e.button==PointerEventData.InputButton.Left) Runtime.BeginDrag(this,e); }
        public void OnDrag(PointerEventData e) { if(Runtime!=null) Runtime.MoveDragFrom(this,e); }
        public void OnEndDrag(PointerEventData e) { if(Runtime!=null) Runtime.EndDragFrom(this,e); }
    }
}

