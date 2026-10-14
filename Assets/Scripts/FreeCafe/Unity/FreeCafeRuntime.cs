using System;
using System.Collections.Generic;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Spotlight.FreeCafe
{
    /// <summary>Owns domain world and input transaction; never creates the legacy gameplay controller.</summary>
    public sealed class FreeCafeRuntime : MonoBehaviour, IDayFlowGameplayEndpoint
    {
        public const string SaveId="freecafe.world.v1";
        public bool DayFlowBusinessActive => World != null && World.State.businessPhase == BusinessPhase.Open;
        public bool DayFlowBusinessFinished => World != null && World.State.businessPhase == BusinessPhase.Night;

        public bool productionMode = true;
        [Tooltip("可选身份适配器，必须实现 ICafeCustomerIdentitySource；不负责启动剧情。") ]
        public MonoBehaviour customerIdentitySource;
        [Tooltip("运行时由 Luban Object 表创建；保留旧序列化字段以兼容场景。显式身份提供者优先。")]
        public Spotlight.Narrative.CharacterCatalog characterCatalog;
        public Spotlight.Narrative.CharacterProfile Character(string id) => characterCatalog != null ? characterCatalog.Find(id) : null;
        public TextAsset catalogAsset;
        public CafeArtLibrary artLibrary;
        public FreeEntityView entityPrefab;
        public FreeCafeEntityPrefabLibrary prefabLibrary;
        public Camera worldCamera;
        public FreeCafeAreaManager areaManager;
        public Transform[] areaRoots;
        public GameObject[] authoringPreviews;
        public CafeWorld World { get; private set; }
        public string SelectedId { get; private set; }
        public string Hint { get; private set; }="拖出物品，可放在画面内任意位置；Shift 仅摆放，不触发组合。";
        public bool CarrySelection { get; set; }
        public bool ShowAssetIds { get; set; }
        public bool IsDragging => draggedView!=null;
        public FreeEntityView DraggedView => draggedView;
        public bool GameplayBlocked => (miniGameGateway != null && miniGameGateway.BlocksInput) || (gameFlow != null && gameFlow.IsMapOpen) || (World != null && World.ProductionEnabled && !string.IsNullOrEmpty(World.State.production.miniGameToken)) || Time.timeScale<=0 || DialoguePanel.IsAnyPlaying || (dayFlow != null && dayFlow.BlocksGameplayInput);
        private CafeVisitBridge cafeVisits;
        private bool pendingBusinessStart;
        public Sprite CafeVisitSprite(string customerId) => cafeVisits?.SeatedSprite(customerId);
        public bool BusinessSimulationPaused => PresentationPauseRegistry.IsPaused || (cafeVisits != null && cafeVisits.HasStageHandoff);
        private DayFlowController dayFlow;
        private GameFlowBootstrap gameFlow;
        public event Action ViewChanged;
        private readonly Dictionary<string,FreeEntityView> views=new Dictionary<string,FreeEntityView>();
        private FreeCafeSpawnScale spawnScale;
        private FreeCafeHud playerHud;
        private FreeCafeProductionHud productionHud;
        public float maxPourAngle = 105f;
        public float pourRateMultiplier = 2f;
        public FreeCafeContainerPanels ContainerPanels { get; private set; }
        private FreeCafeMiniGameGateway miniGameGateway;
        public bool InputBlocked => GameplayBlocked || (FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible) ||
            (playerHud!=null && playerHud.IsModalOpen) || (productionHud!=null && productionHud.IsModalOpen);
        public void OpenPlayerMenu() { if(playerHud!=null) playerHud.OpenPlayerMenu(); }
        private readonly List<string> removeIds=new List<string>();
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        private FreeEntityView draggedView,receiver,dragEventOwner;
        private int dragPointerId;
        private string dragItemId,dragSourceId,dragMachineId,dragOutputId,confirmCancelId;
        private bool committingDrag;
        private Vector3 origin,offset;
        private Camera dragCamera;
        private Plane dragPlane;
        private Vector2 pointerScreen;
        private bool overUI;
        private int dragFinishedFrame=-1;
        private float nextVisualRefresh;
        private bool draggingVessel, pouredThisDrag;
        private bool vesselTiltMode;
        private bool vesselWasTilted;
        private Vector2 vesselTiltPointerOrigin;
        private PointerEventData vesselPointer;
        private string lastPourTarget;
        private string lockedPourTargetId, lastPourError;
        private FreeEntityView receivingView;
        public string LockedPourTargetId => lockedPourTargetId;
        private void Awake()
        {
            ContainerPanels=GetComponent<FreeCafeContainerPanels>();
            if(ContainerPanels==null) ContainerPanels=gameObject.AddComponent<FreeCafeContainerPanels>();
            ContainerPanels.Initialize(this);
            miniGameGateway=GetComponent<FreeCafeMiniGameGateway>();
            if(worldCamera==null) worldCamera=Camera.main;
            if(worldCamera==null || !worldCamera.isActiveAndEnabled || worldCamera.GetComponent<Physics2DRaycaster>()==null)
                Debug.LogError("[FreeCafe Input] 请为 World Camera 指定启用的游戏摄像机，并在同一对象上添加 Physics2DRaycaster。",this);
            if (worldCamera != null && worldCamera.TryGetComponent<Physics2DRaycaster>(out var raycaster) &&
                !(raycaster is FreeCafeEntityRaycaster))
            {
                var entityRaycaster = worldCamera.gameObject.AddComponent<FreeCafeEntityRaycaster>();
                entityRaycaster.eventMask = raycaster.eventMask;
                entityRaycaster.maxRayIntersections = raycaster.maxRayIntersections;
                raycaster.enabled = false;
                Destroy(raycaster);
            }
            if(areaManager==null) areaManager=GetComponent<FreeCafeAreaManager>();
            if(areaManager==null) throw new InvalidOperationException("Configure FreeCafeAreaManager on FreeCafeApp before playing");
            areaManager.Initialize(worldCamera);
            for(int i=0;i<areaRoots.Length;i++)
            {
                var entry=areaManager.Find((AreaId)i);
                if(entry==null || entry.contentRoot!=areaRoots[i]) throw new InvalidOperationException("Area manager and runtime roots disagree: "+(AreaId)i);
            }
            if(customerIdentitySource!=null && !(customerIdentitySource is ICafeCustomerIdentitySource))
                throw new InvalidOperationException("Customer identity source 必须实现 ICafeCustomerIdentitySource");
            var authored = FreeCafeSceneLayout.FindAuthoredEntities(this);
            var catalog = JsonUtility.FromJson<CafeCatalog>(catalogAsset.text);
            var extractionConfig=GetComponent<FreeCafeExtractionConfig>();
            if(extractionConfig!=null) extractionConfig.Apply(catalog.production);
            catalog.layout = FreeCafeSceneLayout.Capture(this, authored);
            FreeCafeSceneLayout.FitAuthoredBounds(this, catalog, authored);
            characterCatalog=Spotlight.Narrative.CharacterCatalog.LoadFromTables();
            var identities = customerIdentitySource as ICafeCustomerIdentitySource;
            if(identities==null && characterCatalog!=null && characterCatalog.Characters.Count>0)
                identities=new CatalogCustomerIdentitySource(characterCatalog);
            World=new CafeWorld(catalog,identities);
            if(prefabLibrary==null) throw new InvalidOperationException("请配置 EntityPrefabLibrary");
            prefabLibrary.Validate(World.Catalog);
            spawnScale=new FreeCafeSpawnScale(this,authored,catalog);
            if(productionMode) World.EnableProduction();
            AdoptAuthoredEntities(authored);
            World.Changed+=SyncViews;
            if(GameSaveParticipantRegistry.Capturers.ContainsKey(SaveId)) throw new InvalidOperationException("Duplicate FreeCafe world in loaded scenes");
            GameSaveParticipantRegistry.Capturers[SaveId]=CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId]=RestoreJson;
        }
        private void Start()
        {
            dayFlow = FindFirstObjectByType<DayFlowController>();
            gameFlow = FindFirstObjectByType<GameFlowBootstrap>();
            playerHud=FindFirstObjectByType<FreeCafeHud>(); productionHud=GetComponent<FreeCafeProductionHud>();
            string pending=GameSaveParticipantRegistry.TakePending(SaveId);
            if(!string.IsNullOrEmpty(pending)) RestoreJson(pending); else SyncViews();
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.Initialize(this);
            if(World.State.currentArea==AreaId.Cafe)
                ShowHint("点击大厅的「启动营业」立牌开始接单；点击客人查看订单，或前往制作台备料。");
            else if(World.ProductionEnabled)
                ShowHint("点击杯具选择出餐杯；原料拖到设备，点击启动。左键拖动壶，按住右键并移动鼠标倾倒，松开右键停止。");
            else
                ShowHint("点击来源取出物品，拖拽到任意位置；拖到容器或设备会尝试组合。Shift 拖拽仅摆放。");
            Debug.Log("[FreeCafe] 自由操作世界已启动：无工作台插槽、无订单步骤门禁。",this);
        }
        private void OnDisable() { CancelDrag(); cafeVisits?.Dispose(); cafeVisits = null; }
        private void OnDestroy()
        {
            if(World!=null) World.Changed-=SyncViews;
            if(GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId,out var capture) && capture==CaptureJson)
            { GameSaveParticipantRegistry.Capturers.Remove(SaveId); GameSaveParticipantRegistry.Restorers.Remove(SaveId); }
        }
        private void Update()
        {
            if(World==null) return;
            bool sessionReady = gameFlow == null || gameFlow.SessionState == GameSessionState.Playing;
            if (sessionReady)
            {
                if (cafeVisits == null) cafeVisits = new CafeVisitBridge(this);
                cafeVisits.Pump();
                if (pendingBusinessStart) TryStartBusiness();
                if (!BusinessSimulationPaused && (Spotlight.Narrative.NarrativeRuntime.Instance == null || cafeVisits.Ready)) World.Tick(Time.deltaTime);
                cafeVisits.Pump();
            }
            if(IsDragging && draggingVessel && !InputBlocked) UpdateVesselInput();
            // GameFlowBootstrap owns the global Escape/pause flow. FreeCafe only
            // cancels an active drag; the shared pause panel handles everything else.
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame && IsDragging)
                CancelDrag();
            if(IsDragging && InputBlocked) CancelDrag();
            if(Time.unscaledTime>=nextVisualRefresh)
            {
                nextVisualRefresh=Time.unscaledTime+.2f;
                foreach(var entity in World.State.entities)
                    if(views.TryGetValue(entity.id,out var view) && view!=draggedView) view.Refresh(entity);
                ViewChanged?.Invoke();
            }
        }
        private void AdoptAuthoredEntities(List<FreeEntityView> authored)
        {
            // 场景中真实实例与刚读取的位置一起接管；不是先按旧配表创建再覆盖。
            foreach(var view in authored)
            {
                var entity=World.Find(view.AuthoringEntityId);
                if(entity==null) throw new InvalidOperationException("场景实体未写入初始布局："+view.name);
                view.transform.SetParent(areaRoots[(int)entity.area],true);
                views.Add(entity.id,view);view.Bind(this,entity);
            }
            // 容器只是编辑器分组，保留用户额外节点；无需隐藏整个父级。
        }
        private void SyncViews()
        {
            if (committingDrag) return;
            removeIds.Clear();
            foreach(var pair in views) if(World.Find(pair.Key)==null) { if(pair.Value!=null) { pair.Value.RetireInteraction(); Destroy(pair.Value.gameObject); } removeIds.Add(pair.Key); }
            foreach(var id in removeIds) views.Remove(id);
            foreach(var entity in World.State.entities)
            {
                if(!views.TryGetValue(entity.id,out var view) || view==null)
                {
                    view=CreateEntityView(entity.specId,entity.area); view.name=entity.id; views[entity.id]=view;
                    view.Bind(this,entity);
                }
                if(view!=draggedView)
                {
                    if(view.transform.parent!=areaRoots[(int)entity.area]) view.transform.SetParent(areaRoots[(int)entity.area],true);
                    view.Refresh(entity);
                }
            }
            if (draggedView != null && !IsRegisteredView(dragEventOwner)) CancelDrag();
            areaManager.Focus(World.State.currentArea);
            if(SelectedId!=null && World.Find(SelectedId)==null) SelectedId=null;
            ViewChanged?.Invoke();
        }
        private FreeEntityView CreateEntityView(string specId,AreaId area)
        {
            var prefab=prefabLibrary.Require(specId);
            var view=Instantiate(prefab,areaRoots[(int)area]);
            view.transform.localScale=spawnScale.Resolve(area,specId,prefab.transform.localScale);
            return view;
        }
        public Vector3 AreaToWorld(AreaId area,float x,float y) => areaRoots[(int)area].TransformPoint(new Vector3(x,y,0));
        public Vector3 WorldToArea(AreaId area,Vector3 position) => areaRoots[(int)area].InverseTransformPoint(position);
        public bool IsCurrentAreaTransform(Transform target)
        {
            if(World==null || target==null) return false;
            var current=areaRoots[(int)World.State.currentArea];
            // 区域场景可相互嵌套（工作台嵌在大厅下）：以最近的区域根判定归属
            for(var t=target;t!=null;t=t.parent)
            {
                for(int i=0;i<areaRoots.Length;i++)
                    if(t==areaRoots[i]) return t==current;
            }
            return false;
        }
        public bool IsRegisteredView(FreeEntityView view) => World != null && view != null &&
            view.Runtime == this && !string.IsNullOrEmpty(view.EntityId) &&
            views.TryGetValue(view.EntityId, out var registered) && registered == view &&
            World.Find(view.EntityId) is WorldEntity entity && string.IsNullOrEmpty(entity.ownerId);
        public FreeEntityView View(string id) => id!=null && views.TryGetValue(id,out var view) ? view : null;
        public void ShowHint(string message)
        {
            Hint=message; ViewChanged?.Invoke();
        }
        public void Report(ActionResult result)
        {
            ShowHint(result.message);
            if(!result.success) OperationToastPanel.ShowMessage(result.message);
            Debug.Log("[FreeCafe] "+(result.success ? "OK " : "拒绝 ")+result.message,this);
        }
        public ActionResult DeliverCup()
        {
            if(InputBlocked) return ActionResult.No("当前不能交付");
            var result=World.DeliverProductionCup(); Report(result); return result;
        }
        public void Inspect(string id)
        {
            // 域重载或初始化前可能先收到指针进入事件。
            if(World==null) return;
            var entity=World.Find(id); if(entity==null) return;
            var spec=World.Spec(entity.specId);
            string info=spec.label;
            if(World.ProductionEnabled && (World.IsProductionVessel(entity) || spec.Has(Capability.Container)))
            {
                var liquid=World.LiquidSnapshot(entity);
                ContainerPanels?.Inspect(id);
                return;
            }
            if(spec.Has(Capability.Container))
            {
                info+=" · 内容 "+World.TotalUnits(entity)+"/"+spec.capacity;
                foreach(var p in entity.contents) info+=" · "+World.Spec(p.specId).label+"×"+p.units;
            }
            if(ShowAssetIds) info+=" · 美术ID "+spec.artId;
            ShowHint(info);
        }
        public void Select(string id) { SelectedId=id; SyncViews(); Inspect(id); }
        public void Click(FreeEntityView view)
        {
            if(World==null || view==null || !view.CanInteract || InputBlocked || IsDragging || dragFinishedFrame==Time.frameCount) return;
            var entity=World.Find(view.EntityId); if(entity==null || entity.area!=World.State.currentArea) return;
            var spec=World.Spec(entity.specId);
            if(spec.Has(Capability.BusinessControl)) { StartBusiness(); return; }
            if(World.ProductionEnabled && World.IsMilkPitcher(entity)) { SelectedId=entity.id; Report(World.FrothMilk(entity.id)); return; }
            if(World.ProductionEnabled && spec.Has(Capability.Source) && World.Spec(spec.sourceItemId).Has(Capability.Container))
            { Report(World.SelectServingCup(entity.id)); return; }
            if(World.ProductionEnabled && spec.Has(Capability.Processor))
            { SelectedId=entity.id; Report(World.OperateProduction(entity.id)); return; }
            if(spec.Has(Capability.Source))
            {
                var result=World.Take(entity.id,entity.area,entity.x,entity.y-1.4f,out var item);
                if(result.success)
                {
                    View(item.id)?.StartMotionFrom(view.transform.position);
                    Select(item.id);
                }
                Report(result);
            }
            else if(spec.Has(Capability.Processor))
            {
                if(entity.task==null)
                {
                    confirmCancelId=null;
                    Report(ActionResult.No("设备空闲：先把材料或装有材料的杯子拖到设备上。"));
                    return;
                }
                if(entity.task.phase==MachinePhase.Loaded)
                {
                    confirmCancelId=null;
                    Report(World.Operate(entity.id));
                    return;
                }
                if(entity.task.phase==MachinePhase.Ready)
                {
                    confirmCancelId=null;
                    Report(World.Operate(entity.id));
                    return;
                }
                bool cancel=entity.task.phase==MachinePhase.Running;
                if(cancel && confirmCancelId!=entity.id)
                {
                    confirmCancelId=entity.id;
                    ShowHint("设备正在加工；再次左键确认中止，或右键取消。原料会保留，已用时间不返还。");
                    return;
                }
                confirmCancelId=null; Report(World.Release(entity.id,true));
            }
            else if(spec.Has(Capability.Customer))
            {
                if (cafeVisits != null && cafeVisits.Click(entity.id)) return;
                foreach(var request in World.State.requests) if(request.customerId==entity.id)
                {
                    World.Focus(request.id);
                    var result=World.Talk(entity.id);
                    Report(result);
                    if(result.success) CafeDialoguePlayback.PlayCustomer(entity.person, result.message);
                    break;
                }
            }
            else Select(entity.id);
        }
        public void CancelProcessing(FreeEntityView view)
        {
            if(World==null || view==null || !view.CanInteract || InputBlocked || IsDragging) return;
            var entity=World.Find(view.EntityId);
            if(entity==null || entity.area!=World.State.currentArea || !World.Spec(entity.specId).Has(Capability.Processor)) return;
            if(World.ProductionEnabled) { Report(World.EmptyProductionMachine(entity.id)); return; }
            if(entity.task==null) { ShowHint("设备当前没有加工任务。"); return; }
            confirmCancelId=null;
            Report(World.Release(entity.id,true));
        }
        // Standard screen ray -> entity plane. No viewport/layout component participates in input.
        private bool TryToWorld(Vector2 screen,out Vector3 point)
        {
            point=default;
            if(dragCamera==null || !dragCamera.isActiveAndEnabled) return false;
            var ray=dragCamera.ScreenPointToRay(screen);
            if(!dragPlane.Raycast(ray,out var distance)) return false;
            point=ray.GetPoint(distance);return true;
        }
        public void BeginDrag(FreeEntityView view,PointerEventData e)
        {
            if(World==null || InputBlocked || IsDragging || view==null || !view.CanInteract || e==null) return;
            var inputCamera=e.pressEventCamera!=null ? e.pressEventCamera : worldCamera;
            if(inputCamera==null || !inputCamera.isActiveAndEnabled) { ShowHint("游戏摄像机未启用，无法拖拽实体"); return; }
            var entity=World.Find(view.EntityId); if(entity==null || entity.area!=World.State.currentArea) return; var spec=World.Spec(entity.specId);
            if(World.ProductionEnabled && World.IsMilkPitcher(entity) && World.MilkFoamVolume(entity)<=.001f)
            {
                ShowHint("请先向拉花杯加入牛奶，点击拉花杯制成奶泡后，再拖到杯子上拉花");
                return;
            }
            if(World.Locked(entity)) { ShowHint("正在加工，点击设备可中止"); return; }
            if(World.ProductionEnabled && World.IsContainer(entity))
            { World.SuspendContainerHeating(entity.id); draggingVessel=true; dragItemId=entity.id; draggedView=view; SelectedId=entity.id; }
            else if(spec.Has(Capability.Source))
            {
                if(entity.stock<=0) { ShowHint("来源库存不足"); return; }
                dragSourceId=entity.id;
                draggedView=CreateEntityView(spec.sourceItemId,World.State.currentArea); draggedView.name="UncommittedPickup";
                draggedView.Bind(this,new WorldEntity { specId=spec.sourceItemId,area=entity.area,x=entity.x,y=entity.y },true);
            }
            else if(spec.Has(Capability.Processor) && (spec.dragOutput || World.ProductionEnabled && entity.specId=="machine.mixer"))
            {
                var output=World.DragOutputPreview(entity.id);
                if(output==null) { ShowHint(entity.task==null ? "先放入咖啡豆并完成研磨，再拖出咖啡粉" : "研磨尚未完成，完成后可拖出咖啡粉"); return; }
                dragMachineId=entity.id;dragOutputId=output.id;
                draggedView=CreateEntityView(output.specId,World.State.currentArea);draggedView.name="UncommittedMachineOutput";
                draggedView.Bind(this,output,true);
                if (entity.specId == "machine.grinder")
                {
                    var drawer = artLibrary.Resolve("equipment.grinder.drawer", "default");
                    if (drawer != null)
                    {
                        draggedView.body.sprite = drawer;
                        var size = drawer.bounds.size;
                        float fit = Mathf.Min(.85f / size.x, .65f / size.y);
                        draggedView.body.transform.localScale = new Vector3(fit, fit, 1);
                    }
                }
            }
            else if(spec.Has(Capability.Movable)) { dragItemId=entity.id; draggedView=view; SelectedId=entity.id; }
            else return;
            dragEventOwner=view; dragPointerId=e.pointerId;
            dragCamera=inputCamera;origin=draggedView.transform.position;
            dragPlane=new Plane(Vector3.forward,origin);
            // BeginDrag occurs only after the mouse has crossed the drag threshold.
            // Anchor at the original press, not the moved cursor, to avoid a permanent offset.
            var pressPosition=e.pointerPressRaycast.module!=null ? e.pressPosition : e.position;
            if(!TryToWorld(pressPosition,out var point)) { CancelDrag();ShowHint("鼠标射线无法命中实体所在平面");return; }
            offset=origin-point;
            draggedView.BeginPointerMotion();
            draggedView.hitbox.enabled=false; MoveDrag(e);
        }
        private WorldEntity DragData()
        {
            if(dragItemId!=null) return World.Find(dragItemId);
            if(dragMachineId!=null)
            {
                var output=World.DragOutputPreview(dragMachineId);
                return output!=null && output.id==dragOutputId ? output : null;
            }
            var source=World.Find(dragSourceId);
            return source==null ? null : new WorldEntity { specId=World.Spec(source.specId).sourceItemId,area=World.State.currentArea };
        }
        private void FindReceiver(PointerEventData e)
        {
            if(receiver!=null) receiver.SetDropTarget(false,false);
            receiver=null; overUI=false; hits.Clear();
            if(EventSystem.current!=null) EventSystem.current.RaycastAll(e,hits);
            foreach(var hit in hits)
            {
                if(hit.gameObject.GetComponentInParent<Canvas>()!=null) { overUI=true; return; }
                var view=hit.gameObject.GetComponentInParent<FreeEntityView>();
                if(view!=null && view!=draggedView)
                {
                    var entity=World.Find(view.EntityId);
                    if(entity!=null && entity.area==World.State.currentArea && World.IsReceiver(entity)) receiver=view;
                    return;
                }
            }
        }
        public void MoveDragFrom(FreeEntityView owner, PointerEventData e)
        {
            if (owner != dragEventOwner || e == null || e.pointerId != dragPointerId ||
                e.button != PointerEventData.InputButton.Left) return;
            MoveDrag(e);
        }
        public void EndDragFrom(FreeEntityView owner, PointerEventData e)
        {
            if (owner != dragEventOwner || e == null || e.pointerId != dragPointerId ||
                e.button != PointerEventData.InputButton.Left) return;
            EndDrag(e);
        }
        public void MoveDrag(PointerEventData e)
        {
            if(!IsDragging) return;
            if(InputBlocked || !TryToWorld(e.position,out var point)) { CancelDrag(); return; }
            if(draggingVessel && Mouse.current!=null && Mouse.current.rightButton.isPressed && !vesselTiltMode)
                BeginVesselTilt();
            pointerScreen=e.position;
            // 左键落点冻结；右键移动只控制倾斜，不重新寻找接收物。
            if(!vesselTiltMode) { draggedView.MovePointer(point+offset); FindReceiver(e); }
            draggedView.body.sortingOrder=2000;
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            var item=DragData(); if(item==null) { CancelDrag(); ShowHint("设备产物已变化，拖拽已取消"); return; } var pos=WorldToArea(World.State.currentArea,draggedView.DragPosition);
            if(!IsDragging) return;
            if(draggingVessel)
            {
                if (World.IsMilkPitcher(item))
                {
                    if (receiver != null && !placeOnly)
                    {
                        var preview = World.Preview(item, World.Find(receiver.EntityId));
                        receiver.SetDropTarget(true, !preview.success); ShowHint(preview.message);
                    }
                    else ShowHint(World.MilkFoamVolume(item)>0 ? "拉花杯已有奶泡：拖到杯子上松手开始拉花；不使用右键倾倒" : "拉花杯不使用右键倾倒；先加入牛奶并点击打奶，再拖到杯子上拉花");
                    return;
                }
                if(receiver!=null)
                {
                    var target=World.Find(receiver.EntityId);
                    bool cupStack = target != null && World.Spec(target.specId).Has(Capability.Source) && World.Spec(World.Spec(target.specId).sourceItemId).Has(Capability.Container);
                    if (cupStack)
                    {
                        var canReturn = World.Preview(item, target);
                        receiver.SetDropTarget(true, !canReturn.success);
                        ShowHint(canReturn.message); return;
                    }
                    bool valid=target!=null && (World.IsContainer(target) || target.specId=="machine.stove" || World.Spec(target.specId).Has(Capability.Delivery) || World.Spec(target.specId).Has(Capability.Discard));
                    receiver.SetDropTarget(true,!valid);
                }
                ShowHint("左键拿起；右键锁住目标并倾倒，松右键继续移动；杯子拖到交付区后按铃提交。");
                return;
            }
            var result=receiver!=null && !placeOnly ? World.Preview(item,World.Find(receiver.EntityId)) : World.CanPosition(item.specId,World.State.currentArea,pos.x,pos.y);
            if(receiver!=null && !placeOnly) receiver.SetDropTarget(true,!result.success);
            ShowHint(overUI ? "不能放在界面卡片上；松手取消" : result.message+(placeOnly ? " · 仅摆放" : " · Shift 可仅摆放"));
        }
        public void EndDrag(PointerEventData e)
        {
            if(!IsDragging) return;
            if(InputBlocked) { CancelDrag(); return; }
            if(draggingVessel && !World.IsMilkPitcher(World.Find(dragItemId))
                && (vesselTiltMode || Mouse.current!=null && Mouse.current.rightButton.isPressed))
            {
                if(!vesselTiltMode) BeginVesselTilt();
                return;
            }
            MoveDrag(e);
            if(!IsDragging) return;
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            string itemId=dragItemId,sourceId=dragSourceId,targetId=receiver==null ? null : receiver.EntityId;
            string machineId=dragMachineId,outputId=dragOutputId;
            var rejectedTarget=receiver; var ghost=draggedView; Vector3 dropPosition=ghost.transform.position; var position=WorldToArea(World.State.currentArea,ghost.DragPosition);
            bool validScreen=dragCamera!=null && dragCamera.pixelRect.Contains(e.position) && !overUI;
            HashSet<string> previousIds = null;
            if (sourceId != null || machineId != null)
            {
                previousIds = new HashSet<string>();
                foreach (var entity in World.State.entities) previousIds.Add(entity.id);
            }
            // Defer synchronization until the transaction can hand off the visible drag object.
            bool wasVessel=draggingVessel; bool wasTilted=vesselWasTilted; ClearDrag(); ActionResult result;
            committingDrag = true;
            try
            {
                if(!validScreen) result=ActionResult.No("已取消，库存与物品位置未改变");
                else if(machineId!=null)
                    result=World.TakeMachineOutput(machineId,outputId,placeOnly ? null : targetId,World.State.currentArea,position.x,position.y);
                else if(sourceId!=null)
                {
                    if(targetId!=null && !placeOnly) result=World.TakeAndInteract(sourceId,targetId,World.State.currentArea,position.x,position.y);
                    else result=World.Take(sourceId,World.State.currentArea,position.x,position.y,out var item);
                }
                else if(wasVessel && !wasTilted && targetId!=null && World.Find(targetId)?.specId=="machine.stove") result=World.Interact(itemId,targetId);
                else if(wasVessel && targetId!=null && World.Spec(World.Find(targetId).specId).Has(Capability.Delivery) && World.Spec(World.Find(itemId).specId).Has(Capability.Container)) result=World.Interact(itemId,targetId);
                else if(wasVessel && wasTilted) result=ActionResult.Yes("容器已归位；按实际倒出量消耗液体");
                else if(targetId!=null && !placeOnly) result=World.Interact(itemId,targetId);
                else result=World.Move(itemId,World.State.currentArea,position.x,position.y);
            }
            finally { committingDrag = false; }
            if (result.success && (sourceId != null || machineId != null))
            {
                WorldEntity placed = null;
                foreach (var entity in World.State.entities)
                    if (string.IsNullOrEmpty(entity.ownerId) && entity.specId == ghost.PrefabSpecId &&
                        (entity.id == outputId || !previousIds.Contains(entity.id))) { placed = entity; break; }
                if (placed != null)
                {
                    if (views.TryGetValue(placed.id, out var oldView) && oldView != ghost) { oldView.RetireInteraction(); Destroy(oldView.gameObject); }
                    views[placed.id] = ghost; ghost.name = placed.id; ghost.Bind(this, placed);
                }
                else ghost.AnimateReturn(dropPosition,rejectedTarget != null ? rejectedTarget.transform.position : origin,true);
            }
            else if (result.success && itemId != null)
            {
                var item = World.Find(itemId);
                if (item == null || !string.IsNullOrEmpty(item.ownerId))
                {
                    views.Remove(itemId);
                    ghost.AnimateReturn(dropPosition,rejectedTarget != null ? rejectedTarget.transform.position : origin,true);
                }
            }
            SyncViews();
            if(!result.success)
            {
                if(rejectedTarget!=null) rejectedTarget.RejectDrop();
                if(ghost!=null) ghost.AnimateReturn(dropPosition,origin,sourceId!=null || machineId!=null);
            }
            Report(result);
        }
        private void ClearDrag()
        {
            if(draggingVessel) StopVesselPour();
            ReleasePourTarget();
            draggingVessel=false; pouredThisDrag=false; vesselTiltMode=false; vesselWasTilted=false;
            if(receiver!=null) receiver.SetDropTarget(false,false);
            if(draggedView!=null) { draggedView.EndPointerMotion(); draggedView.PresentPour(0,false); }
            if(draggedView!=null && dragItemId!=null) draggedView.hitbox.enabled=World.Find(dragItemId)?.area==World.State.currentArea;
            draggedView=null; receiver=null; dragEventOwner=null; dragCamera=null; dragSourceId=dragItemId=dragMachineId=dragOutputId=null; dragFinishedFrame=Time.frameCount;
        }
        public void CancelDrag()
        {
            if(!IsDragging) return;
            var view=draggedView; bool provisional=dragSourceId!=null || dragMachineId!=null; ClearDrag();
            view.AnimateReturn(view.transform.position,origin,provisional);
            SyncViews(); ShowHint("拖拽取消，库存不变");
        }
        public void Enter(AreaId area)
        {
            if(InputBlocked) return;
            if(World.Area(area)==null || areaManager.Find(area)==null) { ShowHint("未配置的小场景："+area);return; }
            CancelDrag();
            if(CarrySelection && SelectedId!=null)
            {
                var item=World.Find(SelectedId);
                if(item!=null) Report(World.Move(item.id,area,item.x,item.y));
            }
            if(World.ProductionEnabled && area==AreaId.Counter) { Report(World.EnterProduction()); return; }
            World.SetArea(area); ShowHint("进入 "+World.Area(area).label+"；物品坐标和加工任务保留");
        }

        private void BeginVesselTilt()
        {
            if (World.IsMilkPitcher(World.Find(dragItemId))) return;
            // 用右键按下前的左键落点选择一次，此后仅更新倾斜量。
            if(vesselPointer==null) vesselPointer=new PointerEventData(EventSystem.current);
            vesselPointer.position=pointerScreen;
            FindReceiver(vesselPointer);
            vesselTiltMode=true;
            vesselWasTilted=true;
            vesselTiltPointerOrigin=pointerScreen;
            var target=ResolvePourTarget();
            draggedView.FreezePointerMotion();
            lockedPourTargetId=target?.id;
            lastPourError=null;
            receivingView=target==null ? null : View(target.id);
            if(receivingView!=null) receivingView.SetDropTarget(true,false);
        }
        private void UpdateVesselInput()
        {
            var mouse=Mouse.current;
            if(mouse==null) return;
            bool rightHeld=mouse.rightButton.isPressed && !World.IsMilkPitcher(World.Find(dragItemId));
            if(rightHeld && !vesselTiltMode) BeginVesselTilt();
            pointerScreen=mouse.position.ReadValue();
            if(!rightHeld && !vesselTiltMode && mouse.leftButton.wasReleasedThisFrame)
            {
                if(vesselPointer==null) vesselPointer=new PointerEventData(EventSystem.current);
                vesselPointer.position=pointerScreen;
                EndDrag(vesselPointer); return;
            }
            if(vesselTiltMode && !rightHeld)
            {
                StopVesselPour();
                ReleasePourTarget();
                vesselTiltMode=false;
                draggedView.PresentPour(0,false);
                if(!mouse.leftButton.isPressed)
                {
                    ClearDrag(); SyncViews();
                    ShowHint("壶已归位；按实际倒出量消耗液体");
                    return;
                }
                vesselPointer.position=pointerScreen;
                MoveDrag(vesselPointer);
            }
            if(vesselTiltMode)
            {
                // Right owns tilt mode even when left is released; receiver remains locked.
                UpdateVesselPour();
            }
        }
        private void UpdateVesselPour()
        {
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            Vector2 delta=pointerScreen-vesselTiltPointerOrigin;
            float tilt=Mathf.Clamp01(-delta.y/90f);
            float signedTilt=-tilt;
            if(draggedView!=null) draggedView.PresentPour(signedTilt,tilt>=.18f && !placeOnly);
            if(placeOnly || tilt<.18f || overUI || Time.deltaTime<=0) { StopVesselPour(); return; }
            var source=World.Find(dragItemId);
            if(!World.CanPour(source)) { StopVesselPour(); return; }
            WorldEntity target=World.Find(lockedPourTargetId);
            if(target==null && receiver!=null) target=World.Find(receiver.EntityId);
            string targetId=target==null ? null : target.id;
            if(pouredThisDrag && lastPourTarget!=targetId) StopVesselPour();
            var result=World.PourLiquid(dragItemId,targetId,World.Catalog.production.pourRateMl*Mathf.Max(.1f,pourRateMultiplier)*tilt*Time.deltaTime,Time.deltaTime);
            if(result.success)
            {
                pouredThisDrag=true; lastPourTarget=targetId;
                if(draggedView!=null) draggedView.RefreshLiquid(signedTilt,true);
                if(target!=null && views.TryGetValue(target.id,out var targetView) && targetView!=null)
                {
                    if(receivingView!=targetView)
                    {
                        if(receivingView!=null) receivingView.SetReceivingPour(false);
                        receivingView=targetView;
                    }
                    targetView.SetReceivingPour(true);
                    targetView.RefreshLiquid();
                }
                if (World.LiquidVolume(World.Find(dragItemId)) <= .001f) StopVesselPour();
            }
            else
            {
                ShowHint(result.message);
                if(lastPourError!=result.message) { lastPourError=result.message; OperationToastPanel.ShowMessage(result.message); }
            }
        }
        private WorldEntity ResolvePourTarget()
        {
            var target=receiver==null ? null : World.Find(receiver.EntityId);
            if(target!=null && World.Spec(target.specId).Has(Capability.Delivery)) return World.ServingCup ?? target;
            if(target!=null) return target;
            // 杯子上方也允许倒入，但只在进入倾倒时判断。
            var source=World.Find(dragItemId);
            var point=WorldToArea(World.State.currentArea,draggedView.DragPosition);
            float closest=float.MaxValue;
            foreach(var entity in World.State.entities)
            {
                if(entity==source || entity.area!=World.State.currentArea || !World.IsContainer(entity)) continue;
                float dx=Mathf.Abs(point.x-entity.x), dy=point.y-entity.y;
                if(dx<.9f && dy>=-.4f && dy<2 && dx+Mathf.Abs(dy)<closest) { closest=dx+Mathf.Abs(dy); target=entity; }
            }
            return target;
        }
        private void ReleasePourTarget()
        {
            if(receivingView!=null) { receivingView.SetReceivingPour(false); receivingView.SetDropTarget(false,false); }
            receivingView=null; lockedPourTargetId=null; lastPourError=null;
        }
        private void StopVesselPour()
        {
            if(receivingView!=null) receivingView.SetReceivingPour(false);
            if(!pouredThisDrag) return;
            var result=World.FinishPour(lastPourTarget); pouredThisDrag=false; lastPourTarget=null;
            ShowHint(result.message);
        }
        public void ReturnFromProduction()
        {
            if(InputBlocked) return; CancelDrag(); Report(World.LeaveProduction());
        }

        public void TogglePause() { CancelDrag(); Time.timeScale=Time.timeScale<=0 ? 1 : 0; ShowHint(Time.timeScale<=0 ? "已暂停 · 再次点击或 Esc 继续" : "继续自由操作"); }
        public string CaptureJson() => JsonUtility.ToJson(World.State);
        public void RestoreJson(string json)
        {
            CancelDrag();
            var gateway=GetComponent<FreeCafeMiniGameGateway>(); if(gateway!=null) gateway.Abort();
            var result=World.Restore(JsonUtility.FromJson<CafeSnapshot>(json));
            if(!result.success) throw new FormatException(result.message); SelectedId=null; if(productionMode && !World.ProductionEnabled) World.EnableProduction(); SyncViews();
            // 读档后镜头瞬间就位，不做区域切换的平滑平移
            areaManager.SnapNow();
        }
        public void Save()
        {
            CancelDrag(); var data=new GameSaveData { SceneName="CoffeeShop",CheckpointId="freecafe",Participants=GameSaveParticipantRegistry.CaptureAll() };
            var saves=new GameSaveService();
            Report(saves.TryQuickSave(data,out var slot,out var error) ? ActionResult.Yes("已保存到空槽位 "+slot) : ActionResult.No("保存失败："+error));
        }
        public void StartBusiness() { if(!InputBlocked) RequestBusinessStart(); }
        public void RequestBusinessStart() { if (!GameplayBlocked) { pendingBusinessStart = true; TryStartBusiness(); } }
        private void TryStartBusiness()
        {
            if (cafeVisits == null) cafeVisits = new CafeVisitBridge(this);
            var ncs = Spotlight.Narrative.NarrativeRuntime.Instance;
            if (ncs != null && (!cafeVisits.Pump() || !ncs.Controller.CanCaptureSnapshot || ncs.Controller.IsBusy || cafeVisits.HasStageHandoff)) return;
            pendingBusinessStart = false;
            Report(World.StartDay());
            cafeVisits.Pump();
        }
        public void EndBusiness() { if(!InputBlocked) Report(World.EndDay()); }
        public void StartDayFlowBusiness() => StartBusiness();
        public void EndDayFlowBusiness() => EndBusiness();
        public void RefillSources() { if(!InputBlocked) Report(World.Refill()); }
        public void RotateSelected() { if(!InputBlocked) World.Rotate(SelectedId); }
        public void DebugRefreshViews() { SyncViews(); ShowHint("经营视图已刷新"); }
    }
}
