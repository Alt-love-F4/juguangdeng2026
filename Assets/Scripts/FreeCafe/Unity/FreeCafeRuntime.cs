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

        public TextAsset catalogAsset;
        public CafeArtLibrary artLibrary;
        public FreeEntityView entityPrefab;
        public FreeCafeEntityPrefabLibrary prefabLibrary;
        public Camera worldCamera;
        public Transform[] areaRoots;
        public GameObject[] authoringPreviews;
        public CafeWorld World { get; private set; }
        public string SelectedId { get; private set; }
        public string Hint { get; private set; }="拖出物品，可放在画面内任意位置；Shift 仅摆放，不触发组合。";
        public bool CarrySelection { get; set; }
        public bool ShowAssetIds { get; set; }
        public bool IsDragging => draggedView!=null;
        public bool InputBlocked => Time.timeScale<=0;
        public event Action ViewChanged;
        private readonly Dictionary<string,FreeEntityView> views=new Dictionary<string,FreeEntityView>();
        private readonly List<string> removeIds=new List<string>();
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        private FreeEntityView draggedView,receiver;
        private string dragItemId,dragSourceId,confirmCancelId;
        private Vector3 origin,offset;
        private Vector2 pointerScreen;
        private bool overUI;
        private int dragFinishedFrame=-1;
        private float nextVisualRefresh;
        private void Awake()
        {
            World=new CafeWorld(JsonUtility.FromJson<CafeCatalog>(catalogAsset.text));
            if(authoringPreviews!=null) foreach(var preview in authoringPreviews) if(preview!=null) preview.SetActive(false);
            World.Changed+=SyncViews;
            if(GameSaveParticipantRegistry.Capturers.ContainsKey(SaveId)) throw new InvalidOperationException("Duplicate FreeCafe world in loaded scenes");
            GameSaveParticipantRegistry.Capturers[SaveId]=CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId]=RestoreJson;
        }
        private void Start()
        {
            string pending=GameSaveParticipantRegistry.TakePending(SaveId);
            if(!string.IsNullOrEmpty(pending)) RestoreJson(pending); else SyncViews();
            if(World.State.currentArea==AreaId.Cafe)
                ShowHint("当前是营业区；点击右侧白色箭头或右下角「一楼出餐台」进入备料区。进入出餐台后，点击或拖拽物品即可交互。");
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
            for(int i=0;i<areaRoots.Length;i++) areaRoots[i].gameObject.SetActive(i==(int)World.State.currentArea);
            if(SelectedId!=null && World.Find(SelectedId)==null) SelectedId=null;
            ViewChanged?.Invoke();
        }
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
            var entity=World.Find(id); if(entity==null) return;
            var spec=World.Spec(entity.specId);
            string info=spec.label;
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
            var entity=World.Find(view.EntityId); if(entity==null) return;
            var spec=World.Spec(entity.specId);
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
            if(entity==null || !World.Spec(entity.specId).Has(Capability.Processor)) return;
            if(entity.task==null) { ShowHint("设备当前没有加工任务。"); return; }
            confirmCancelId=null;
            Report(World.Release(entity.id,true));
        }
        private Vector3 ToWorld(Vector2 screen) { var point=worldCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-worldCamera.transform.position.z)); point.z=0; return point; }
        public void BeginDrag(FreeEntityView view,PointerEventData e)
        {
            if(InputBlocked || IsDragging) return;
            var entity=World.Find(view.EntityId); if(entity==null) return; var spec=World.Spec(entity.specId);
            if(World.Locked(entity)) { ShowHint("正在加工，点击设备可中止"); return; }
            if(spec.Has(Capability.Source))
            {
                if(entity.stock<=0) { ShowHint("来源库存不足"); return; }
                dragSourceId=entity.id;
                draggedView=Instantiate(entityPrefab,areaRoots[(int)World.State.currentArea]); draggedView.name="UncommittedPickup";
                draggedView.Bind(this,new WorldEntity { specId=spec.sourceItemId,area=entity.area,x=entity.x,y=entity.y },true);
            }
            else if(spec.Has(Capability.Movable)) { dragItemId=entity.id; draggedView=view; SelectedId=entity.id; }
            else return;
            origin=draggedView.transform.position; offset=origin-ToWorld(e.position);
            draggedView.hitbox.enabled=false; MoveDrag(e);
        }
        private WorldEntity DragData()
        {
            if(dragItemId!=null) return World.Find(dragItemId);
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
                    if(World.IsReceiver(World.Find(view.EntityId))) receiver=view;
                    return;
                }
            }
        }
        public void MoveDrag(PointerEventData e)
        {
            if(!IsDragging) return;
            pointerScreen=e.position; draggedView.transform.position=ToWorld(e.position)+offset;
            draggedView.body.sortingOrder=2000; FindReceiver(e);
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            var item=DragData(); var pos=draggedView.transform.position;
            var result=receiver!=null && !placeOnly ? World.Preview(item,World.Find(receiver.EntityId)) : World.CanPosition(item.specId,World.State.currentArea,pos.x,pos.y);
            if(receiver!=null && !placeOnly) receiver.SetDropTarget(true,!result.success);
            ShowHint(overUI ? "不能放在 UI 黑边；松手取消" : result.message+(placeOnly ? " · 仅摆放" : " · Shift 可仅摆放"));
        }
        public void EndDrag(PointerEventData e)
        {
            if(!IsDragging) return;
            if(InputBlocked) { CancelDrag(); return; }
            MoveDrag(e);
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            string itemId=dragItemId,sourceId=dragSourceId,targetId=receiver==null ? null : receiver.EntityId;
            var ghost=draggedView; var position=ghost.transform.position;
            bool validScreen=worldCamera.pixelRect.Contains(e.position) && !overUI;
            // Release pointer ownership before the model synchronizes/consumes any view.
            ClearDrag(); ActionResult result;
            if(!validScreen) result=ActionResult.No("已取消，库存与物品位置未改变");
            else if(sourceId!=null)
            {
                if(targetId!=null && !placeOnly) result=World.TakeAndInteract(sourceId,targetId,World.State.currentArea,position.x,position.y);
                else result=World.Take(sourceId,World.State.currentArea,position.x,position.y,out var item);
            }
            else if(targetId!=null && !placeOnly) result=World.Interact(itemId,targetId);
            else result=World.Move(itemId,World.State.currentArea,position.x,position.y);
            if(sourceId!=null) Destroy(ghost.gameObject);
            else if(!result.success && ghost!=null) ghost.transform.position=origin;
            SyncViews(); Report(result);
        }
        private void ClearDrag()
        {
            if(receiver!=null) receiver.SetDropTarget(false,false);
            if(draggedView!=null && dragItemId!=null) draggedView.hitbox.enabled=true;
            draggedView=null; receiver=null; dragSourceId=dragItemId=null; dragFinishedFrame=Time.frameCount;
        }
        public void CancelDrag()
        {
            if(!IsDragging) return;
            var view=draggedView; bool provisional=dragSourceId!=null; ClearDrag();
            if(provisional) Destroy(view.gameObject); else view.transform.position=origin;
            SyncViews(); ShowHint("拖拽取消，库存不变");
        }
        public void Enter(AreaId area)
        {
            if(InputBlocked) return; CancelDrag();
            if(CarrySelection && SelectedId!=null)
            {
                var item=World.Find(SelectedId);
                if(item!=null) Report(World.Move(item.id,area,item.x,item.y));
            }
            World.SetArea(area); ShowHint("进入 "+World.Area(area).label+"；物品坐标和加工任务保留");
        }
        public void TogglePause() { CancelDrag(); Time.timeScale=Time.timeScale<=0 ? 1 : 0; ShowHint(Time.timeScale<=0 ? "已暂停 · 再次点击或 Esc 继续" : "继续自由操作"); }
        public string CaptureJson() => JsonUtility.ToJson(World.State);
        public void RestoreJson(string json)
        {
            CancelDrag(); var result=World.Restore(JsonUtility.FromJson<CafeSnapshot>(json));
            if(!result.success) throw new FormatException(result.message); SelectedId=null; SyncViews();
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
    }
}


