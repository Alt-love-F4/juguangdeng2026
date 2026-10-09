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
        public bool InputBlocked => (gameFlow != null && gameFlow.IsMapOpen) || (World != null && World.ProductionEnabled && !string.IsNullOrEmpty(World.State.production.miniGameToken)) || Time.timeScale<=0 || DialoguePanel.IsAnyPlaying || (dayFlow != null && dayFlow.BlocksGameplayInput);
        private DayFlowController dayFlow;
        private GameFlowBootstrap gameFlow;
        public event Action ViewChanged;
        private readonly Dictionary<string,FreeEntityView> views=new Dictionary<string,FreeEntityView>();
        private readonly List<string> removeIds=new List<string>();
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        private FreeEntityView draggedView,receiver;
        private string dragItemId,dragSourceId,dragMachineId,dragOutputId,confirmCancelId;
        private Vector3 origin,offset;
        private Camera dragCamera;
        private Plane dragPlane;
        private Vector2 pointerScreen;
        private bool overUI;
        private int dragFinishedFrame=-1;
        private float nextVisualRefresh;
        private bool draggingVessel, pouredThisDrag;
        private string lastPourTarget;
        private float tiltOriginY;
        private void Awake()
        {
            if(worldCamera==null) worldCamera=Camera.main;
            if(worldCamera==null || !worldCamera.isActiveAndEnabled || worldCamera.GetComponent<Physics2DRaycaster>()==null)
                Debug.LogError("[FreeCafe Input] 请为 World Camera 指定启用的游戏摄像机，并在同一对象上添加 Physics2DRaycaster。",this);
            if(areaManager==null) areaManager=GetComponent<FreeCafeAreaManager>();
            if(areaManager==null) throw new InvalidOperationException("Configure FreeCafeAreaManager on FreeCafeApp before playing");
            areaManager.Initialize(worldCamera);
            for(int i=0;i<areaRoots.Length;i++)
            {
                var entry=areaManager.Find((AreaId)i);
                if(entry==null || entry.contentRoot!=areaRoots[i]) throw new InvalidOperationException("Area manager and runtime roots disagree: "+(AreaId)i);
            }
            World=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(catalogAsset.text));
            if(productionMode) World.EnableProduction();
            if(authoringPreviews!=null) foreach(var preview in authoringPreviews) if(preview!=null) preview.SetActive(false);
            World.Changed+=SyncViews;
            if(GameSaveParticipantRegistry.Capturers.ContainsKey(SaveId)) throw new InvalidOperationException("Duplicate FreeCafe world in loaded scenes");
            GameSaveParticipantRegistry.Capturers[SaveId]=CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId]=RestoreJson;
        }
        private void Start()
        {
            dayFlow = FindFirstObjectByType<DayFlowController>();
            gameFlow = FindFirstObjectByType<GameFlowBootstrap>();
            string pending=GameSaveParticipantRegistry.TakePending(SaveId);
            if(!string.IsNullOrEmpty(pending)) RestoreJson(pending); else SyncViews();
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.Initialize(this);
            if(World.State.currentArea==AreaId.Cafe)
                ShowHint("当前是营业区；点击右侧白色箭头或右下角「一楼出餐台」进入备料区。进入出餐台后，点击或拖拽物品即可交互。");
            else if(World.ProductionEnabled)
                ShowHint("点击杯具选择出餐杯；原料拖到设备，点击启动。成品壶向下拖动倾倒，抬起停止。");
            else
                ShowHint("点击来源取出物品，拖拽到任意位置；拖到容器或设备会尝试组合。Shift 拖拽仅摆放。");
            Debug.Log("[FreeCafe] 自由操作世界已启动：无工作台插槽、无订单步骤门禁。",this);
        }
        private void OnDisable() => CancelDrag();
        private void OnDestroy()
        {
            if(World!=null) World.Changed-=SyncViews;
            if(GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId,out var capture) && capture==CaptureJson)
            { GameSaveParticipantRegistry.Capturers.Remove(SaveId); GameSaveParticipantRegistry.Restorers.Remove(SaveId); }
        }
        private void Update()
        {
            if(World==null) return;
            World.Tick(Time.deltaTime);
            if(IsDragging && draggingVessel && !InputBlocked) UpdateVesselPour();
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
        private void SyncViews()
        {
            removeIds.Clear();
            foreach(var pair in views) if(World.Find(pair.Key)==null) { if(pair.Value!=null) Destroy(pair.Value.gameObject); removeIds.Add(pair.Key); }
            foreach(var id in removeIds) views.Remove(id);
            foreach(var entity in World.State.entities)
            {
                if(!views.TryGetValue(entity.id,out var view) || view==null)
                {
                    var prefab=prefabLibrary==null ? null : prefabLibrary.Resolve(entity.specId);
                    view=Instantiate(prefab!=null ? prefab : entityPrefab,areaRoots[(int)entity.area]); view.name=entity.id; views[entity.id]=view;
                    view.Bind(this,entity);
                }
                if(view!=draggedView)
                {
                    if(view.transform.parent!=areaRoots[(int)entity.area]) view.transform.SetParent(areaRoots[(int)entity.area],false);
                    view.Refresh(entity);
                }
            }
            areaManager.Focus(World.State.currentArea);
            if(SelectedId!=null && World.Find(SelectedId)==null) SelectedId=null;
            ViewChanged?.Invoke();
        }
        public Vector3 AreaToWorld(AreaId area,float x,float y) => areaRoots[(int)area].TransformPoint(new Vector3(x,y,0));
        public Vector3 WorldToArea(AreaId area,Vector3 position) => areaRoots[(int)area].InverseTransformPoint(position);
        public bool IsCurrentAreaTransform(Transform target) => World!=null && target!=null && target.IsChildOf(areaRoots[(int)World.State.currentArea]);
        public FreeEntityView View(string id) => id!=null && views.TryGetValue(id,out var view) ? view : null;
        public void ShowHint(string message)
        {
            Hint=message; ViewChanged?.Invoke();
        }
        private void Report(ActionResult result)
        {
            ShowHint(result.message); Debug.Log("[FreeCafe] "+(result.success ? "OK " : "拒绝 ")+result.message,this);
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
                info+=" · "+liquid.volumeMl.ToString("0")+"/"+liquid.capacityMl.ToString("0")+" ml · "+World.ProductionCaption(entity);
                foreach(var portion in entity.contents) info+=" · "+World.Spec(portion.specId).label;
                ShowHint(info); return;
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
            if(InputBlocked || IsDragging || dragFinishedFrame==Time.frameCount) return;
            var entity=World.Find(view.EntityId); if(entity==null || entity.area!=World.State.currentArea) return;
            var spec=World.Spec(entity.specId);
            if(World.ProductionEnabled && entity.specId=="tool.latte-art") { Report(World.RequestProductionMiniGame()); return; }
            if(World.ProductionEnabled && spec.Has(Capability.Source) && World.Spec(spec.sourceItemId).Has(Capability.Container))
            { Report(World.SelectServingCup(entity.id)); return; }
            if(World.ProductionEnabled && spec.Has(Capability.Processor))
            { SelectedId=entity.id; Report(World.OperateProduction(entity.id)); return; }
            if(spec.Has(Capability.Source))
            {
                var result=World.Take(entity.id,entity.area,entity.x,entity.y-1.4f,out var item);
                if(result.success) Select(item.id); Report(result);
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
                foreach(var request in World.State.requests) if(request.customerId==entity.id) { World.Focus(request.id); Report(ActionResult.Yes("已选择订单："+World.Product(request.productId).label)); break; }
            }
            else Select(entity.id);
        }
        public void CancelProcessing(FreeEntityView view)
        {
            if(InputBlocked || IsDragging) return;
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
            if(InputBlocked || IsDragging || view==null || e==null) return;
            var inputCamera=e.pressEventCamera!=null ? e.pressEventCamera : worldCamera;
            if(inputCamera==null || !inputCamera.isActiveAndEnabled) { ShowHint("游戏摄像机未启用，无法拖拽实体"); return; }
            var entity=World.Find(view.EntityId); if(entity==null || entity.area!=World.State.currentArea) return; var spec=World.Spec(entity.specId);
            if(World.Locked(entity)) { ShowHint("正在加工，点击设备可中止"); return; }
            if(World.ProductionEnabled && spec.Has(Capability.Source) && World.Spec(spec.sourceItemId).Has(Capability.Container))
            { ShowHint("请点击杯具堆选择出餐杯"); return; }
            if(entity==World.ServingCup) { ShowHint("出餐杯固定在托盘；使用出品或丢弃按钮"); return; }
            if(World.CanPour(entity))
            { draggingVessel=true; dragItemId=entity.id; draggedView=view; SelectedId=entity.id; tiltOriginY=e.position.y; }
            else if(spec.Has(Capability.Source))
            {
                if(entity.stock<=0) { ShowHint("来源库存不足"); return; }
                dragSourceId=entity.id;
                draggedView=Instantiate(entityPrefab,areaRoots[(int)World.State.currentArea]); draggedView.name="UncommittedPickup";
                draggedView.Bind(this,new WorldEntity { specId=spec.sourceItemId,area=entity.area,x=entity.x,y=entity.y },true);
            }
            else if(spec.Has(Capability.Processor) && (spec.dragOutput || World.ProductionEnabled && entity.specId=="machine.mixer"))
            {
                var output=World.DragOutputPreview(entity.id);
                if(output==null) { ShowHint(entity.task==null ? "先放入咖啡豆并完成研磨，再拖出咖啡粉" : "研磨尚未完成，完成后可拖出咖啡粉"); return; }
                dragMachineId=entity.id;dragOutputId=output.id;
                var prefab=prefabLibrary==null ? null : prefabLibrary.Resolve(output.specId);
                draggedView=Instantiate(prefab!=null ? prefab : entityPrefab,areaRoots[(int)World.State.currentArea]);draggedView.name="UncommittedMachineOutput";
                draggedView.Bind(this,output,true);
            }
            else if(spec.Has(Capability.Movable)) { dragItemId=entity.id; draggedView=view; SelectedId=entity.id; }
            else return;
            dragCamera=inputCamera;origin=draggedView.transform.position;
            dragPlane=new Plane(Vector3.forward,origin);
            // BeginDrag occurs only after the mouse has crossed the drag threshold.
            // Anchor at the original press, not the moved cursor, to avoid a permanent offset.
            var pressPosition=e.pointerPressRaycast.module!=null ? e.pressPosition : e.position;
            if(!TryToWorld(pressPosition,out var point)) { CancelDrag();ShowHint("鼠标射线无法命中实体所在平面");return; }
            offset=origin-point;
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
        public void MoveDrag(PointerEventData e)
        {
            if(!IsDragging) return;
            if(InputBlocked || !TryToWorld(e.position,out var point)) { CancelDrag(); return; }
            pointerScreen=e.position; draggedView.transform.position=point+offset;
            draggedView.body.sortingOrder=2000; FindReceiver(e);
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            var item=DragData(); if(item==null) { CancelDrag(); ShowHint("设备产物已变化，拖拽已取消"); return; } var pos=WorldToArea(World.State.currentArea,draggedView.transform.position);
            var result=receiver!=null && !placeOnly ? World.Preview(item,World.Find(receiver.EntityId)) : World.CanPosition(item.specId,World.State.currentArea,pos.x,pos.y);
            if(receiver!=null && !placeOnly) receiver.SetDropTarget(true,!result.success);
            ShowHint(overUI ? "不能放在 UI 黑边；松手取消" : result.message+(placeOnly ? " · 仅摆放" : " · Shift 可仅摆放"));
        }
        public void EndDrag(PointerEventData e)
        {
            if(!IsDragging) return;
            if(InputBlocked) { CancelDrag(); return; }
            MoveDrag(e);
            if(!IsDragging) return;
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            string itemId=dragItemId,sourceId=dragSourceId,targetId=receiver==null ? null : receiver.EntityId;
            string machineId=dragMachineId,outputId=dragOutputId;
            var rejectedTarget=receiver; var ghost=draggedView; Vector3 dropPosition=ghost.transform.position; var position=WorldToArea(World.State.currentArea,ghost.transform.position);
            bool validScreen=dragCamera!=null && dragCamera.pixelRect.Contains(e.position) && !overUI;
            // Release pointer ownership before the model synchronizes/consumes any view.
            bool wasVessel=draggingVessel; ClearDrag(); ActionResult result;
            if(!validScreen) result=ActionResult.No("已取消，库存与物品位置未改变");
            else if(machineId!=null)
                result=World.TakeMachineOutput(machineId,outputId,placeOnly ? null : targetId,World.State.currentArea,position.x,position.y);
            else if(sourceId!=null)
            {
                if(targetId!=null && !placeOnly) result=World.TakeAndInteract(sourceId,targetId,World.State.currentArea,position.x,position.y);
                else result=World.Take(sourceId,World.State.currentArea,position.x,position.y,out var item);
            }
            else if(wasVessel) result=ActionResult.Yes("壶已归位；按实际倒出量消耗液体");
            else if(targetId!=null && !placeOnly) result=World.Interact(itemId,targetId);
            else result=World.Move(itemId,World.State.currentArea,position.x,position.y);
            if(sourceId!=null || machineId!=null) { if(result.success) { if(rejectedTarget!=null) ghost.AnimateReturn(dropPosition,rejectedTarget.transform.position,true); else Destroy(ghost.gameObject); } }
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
            draggingVessel=false; pouredThisDrag=false;
            if(receiver!=null) receiver.SetDropTarget(false,false);
            if(draggedView!=null && dragItemId!=null) draggedView.hitbox.enabled=World.Find(dragItemId)?.area==World.State.currentArea;
            draggedView=null; receiver=null; dragCamera=null; dragSourceId=dragItemId=dragMachineId=dragOutputId=null; dragFinishedFrame=Time.frameCount;
        }
        public void CancelDrag()
        {
            if(!IsDragging) return;
            var view=draggedView; bool provisional=dragSourceId!=null || dragMachineId!=null; ClearDrag();
            if(provisional) Destroy(view.gameObject); else view.transform.position=origin;
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

        private void UpdateVesselPour()
        {
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            tiltOriginY=Mathf.Max(tiltOriginY,pointerScreen.y); // 先抬壶到同高工位上方，再向下移动也能倾倒
            float tilt=Mathf.Clamp01((tiltOriginY-pointerScreen.y)/90f);
            if(draggedView!=null) draggedView.PresentPour(tilt,tilt>=.18f && !placeOnly);
            if(placeOnly || tilt<.18f || overUI || Time.deltaTime<=0) { StopVesselPour(); return; }
            var source=World.Find(dragItemId);
            if(!World.CanPour(source)) { StopVesselPour(); return; }
            WorldEntity target=receiver==null ? null : World.Find(receiver.EntityId);
            // 壶嘴允许位于容器上方，不要求鼠标穿过杯子碰撞体。
            if(target==null)
            {
                var point=WorldToArea(World.State.currentArea,draggedView.transform.position);
                float closest=float.MaxValue;
                foreach(var entity in World.State.entities)
                {
                    if(entity==source || entity.area!=source.area) continue;
                    bool valid=World.Spec(entity.specId).Has(Capability.Container) || entity.specId=="machine.brewer";
                    float dx=Mathf.Abs(point.x-entity.x), dy=point.y-entity.y;
                    if(valid && dx<.9f && dy>=-.4f && dy<2 && dx+Mathf.Abs(dy)<closest) { closest=dx+Mathf.Abs(dy); target=entity; }
                }
            }
            if(target!=null && World.Spec(target.specId).Has(Capability.Delivery)) target=World.ServingCup;
            string targetId=target==null ? null : target.id;
            if(pouredThisDrag && lastPourTarget!=targetId) StopVesselPour();
            var result=World.PourLiquid(dragItemId,targetId,World.Catalog.production.pourRateMl*tilt*Time.deltaTime,Time.deltaTime);
            if(result.success) { pouredThisDrag=true; lastPourTarget=targetId; }
            else ShowHint(result.message);
        }
        private void StopVesselPour()
        {
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
        }
        public void Save()
        {
            CancelDrag(); var data=new GameSaveData { SceneName="CoffeeShop",CheckpointId="freecafe",Participants=GameSaveParticipantRegistry.CaptureAll() };
            var saves=new GameSaveService();
            Report(saves.TryQuickSave(data,out var slot,out var error) ? ActionResult.Yes("已保存到空槽位 "+slot) : ActionResult.No("保存失败："+error));
        }
        public void StartBusiness() { if(!InputBlocked) Report(World.StartDay()); }
        public void EndBusiness() { if(!InputBlocked) Report(World.EndDay()); }
        public void StartDayFlowBusiness() => StartBusiness();
        public void EndDayFlowBusiness() => EndBusiness();
        public void RefillSources() { if(!InputBlocked) Report(World.Refill()); }
        public void RotateSelected() { if(!InputBlocked) World.Rotate(SelectedId); }
        public void DebugRefreshViews() { SyncViews(); ShowHint("经营视图已刷新"); }
    }
}


