using System;
using System.Collections.Generic;
using Spotlight.Audio;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Spotlight.FreeCafe
{
    /// <summary>Owns domain world and input transaction; never creates the legacy gameplay controller.</summary>
    public sealed class FreeCafeRuntime : MonoBehaviour, IDayFlowGameplayEndpoint, ICafeTutorialGameplayEndpoint
    {
        public const string SaveId="freecafe.world.v1";
        public bool DayFlowBusinessActive => World != null && World.State.businessPhase == BusinessPhase.Open;
        public bool DayFlowBusinessFinished => World != null && World.State.businessPhase == BusinessPhase.Night;

        public bool productionMode = true;
        [Tooltip("显示咖啡店顶部的设备升级入口；默认隐藏，不影响设备解锁数据。")]
        public bool showEquipmentUpgradeUI;
        [Tooltip("可选身份适配器，必须实现 ICafeCustomerIdentitySource；不负责启动剧情。") ]
        public MonoBehaviour customerIdentitySource;
        [Tooltip("运行时由 Luban Object 表创建；普通客流仅借用 guest_* 外观，指定角色由 NCS 来访安排。保留旧序列化字段。")]
        public Spotlight.Narrative.CharacterCatalog characterCatalog;
        public Spotlight.Narrative.CharacterProfile Character(string id) => characterCatalog != null ? characterCatalog.Find(id) : null;
        public TextAsset catalogAsset;
        public CafeArtLibrary artLibrary;
        public FreeEntityView entityPrefab;
        public FreeCafeEntityPrefabLibrary prefabLibrary;
        public FreeCafeSeating seating;
        public Camera worldCamera;
        public FreeCafeAreaManager areaManager;
        public Transform[] areaRoots;
        public GameObject[] authoringPreviews;
        public CafeWorld World { get; private set; }
        public string SelectedId { get; private set; }
        public string Hint { get; private set; }="杯子可自由摆放；其他器皿松开左右键后归位。Shift 仅摆放，不触发组合。";
        public bool CarrySelection { get; set; }
        public bool ShowAssetIds { get; set; }
        public bool IsDragging => draggedView!=null;
        public FreeEntityView DraggedView => draggedView;
        public bool IsDraggingOutputFrom(string entityId) => draggedView != null && dragMachineId == entityId;
        public bool IsTakingFromSource(string entityId) => draggedView != null && dragSourceId == entityId;
        public FreeCafeWorkstationTutorial WorkstationTutorial { get; private set; }
        public FreeCafeMilkPitcherTutorial MilkPitcherTutorial { get; private set; }
        private FreeCafeEquipmentTutorialPrompt equipmentTutorialPrompt;
        public bool StartEquipmentTutorial(bool latte)
        {
            if (CafeTutorialActive || PracticeTutorialActive || InputBlocked || IsDragging) return false;
            if (latte)
            {
                MilkPitcherTutorial ??= gameObject.AddComponent<FreeCafeMilkPitcherTutorial>();
                return MilkPitcherTutorial.Begin(this);
            }
            return World.PrecisionEquipmentAvailable && WorkstationTutorial != null && WorkstationTutorial.Begin(true);
        }
        public bool IsSandboxMode { get; private set; }
        private FreeCafeEquipmentPanel equipmentPanel;
        private bool pendingDailyCupRefresh = true;
        public bool PracticeTutorialActive => WorkstationTutorial != null && WorkstationTutorial.IsRunning
            || MilkPitcherTutorial != null && MilkPitcherTutorial.IsRunning;
        private CafeTutorialSession cafeTutorial;
        private bool pendingTutorialCoffee;
        public event System.Action<string, string> CafeTutorialStepCompleted;
        private NarrativeConfiguration tutorialConfiguration;
        public bool CafeTutorialActive => dayFlow != null && dayFlow.IsCafeTutorialActive;
        public bool CafeBusinessOnboardingActive => CafeTutorialActive && cafeTutorial?.WaitingForBusinessOnboarding == true;
        private FreeCafeBusinessTutorial BusinessTutorial => GetComponent<FreeCafeBusinessTutorial>();
        private bool TutorialBusinessStartBlocked => CafeTutorialActive
            && (!CafeBusinessOnboardingActive || BusinessTutorial == null || !BusinessTutorial.AllowBusinessStart);
        public bool CafeTutorialWaitingForWorkstation => CafeTutorialActive && cafeTutorial != null && cafeTutorial.WaitingForWorkstation;
        private bool PresentationInputBlocked => GameplayHudPanel.BlocksWorldInput || (miniGameGateway != null && miniGameGateway.BlocksInput) || (gameFlow != null && (gameFlow.IsMapOpen || gameFlow.IsDetailsOpen)) || (World != null && World.ProductionEnabled && !string.IsNullOrEmpty(World.State.production.miniGameToken)) || Time.timeScale<=0 || DialoguePanel.IsAnyPlaying || (dayFlow != null && dayFlow.BlocksGameplayInput);
        public bool GameplayBlocked => PresentationInputBlocked || CafeTutorialWaitingForWorkstation
            || (CafeTutorialActive && cafeTutorial != null && cafeTutorial.HandoffReady);
        private CafeVisitBridge cafeVisits;
        private bool pendingBusinessStart;
        public Sprite CafeVisitSprite(string customerId) => cafeVisits?.SeatedSprite(customerId);
        public bool BusinessSimulationPaused => PresentationPauseRegistry.IsPaused || (cafeVisits != null && cafeVisits.HasStageHandoff)
            || CafeTutorialActive && TutorialInputBlocked
            || CafeBusinessOnboardingActive && BusinessTutorial != null && BusinessTutorial.PauseBusinessSimulation
            || MilkPitcherTutorial != null && MilkPitcherTutorial.IsRunning
            || equipmentTutorialPrompt != null && equipmentTutorialPrompt.IsOpen;
        private DayFlowController dayFlow;
        private GameFlowBootstrap gameFlow;
        public event Action ViewChanged;
        public event Action<CafeDeliveryReceipt> CupDelivered;
        private readonly Dictionary<string,FreeEntityView> views=new Dictionary<string,FreeEntityView>();
        private readonly HashSet<string> inactiveAuthoredIds=new HashSet<string>();
        private FreeCafeSpawnScale spawnScale;
        private FreeCafeHud playerHud;
        private FreeCafeProductionHud productionHud;
        public float maxPourAngle = 105f;
        public float pourRateMultiplier = 2f;
        public FreeCafeContainerPanels ContainerPanels { get; private set; }
        private FreeCafeMiniGameGateway miniGameGateway;
        private bool ModalInputBlocked => (equipmentTutorialPrompt != null && equipmentTutorialPrompt.IsOpen) || (equipmentPanel != null && equipmentPanel.IsOpen) || (FreeCafeGMPanel.Instance!=null && FreeCafeGMPanel.Instance.IsVisible) ||
            (playerHud!=null && playerHud.IsModalOpen) || (productionHud!=null && productionHud.IsModalOpen);
        public bool TutorialInputBlocked => PresentationInputBlocked || ModalInputBlocked;
        public bool InputBlocked => GameplayBlocked || ModalInputBlocked;
        public void OpenPlayerMenu() { if(playerHud!=null) playerHud.OpenPlayerMenu(); }
        private readonly List<string> removeIds=new List<string>();
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        private FreeEntityView draggedView,receiver,dragEventOwner,dragCompanion;
        private FreeCafeCupStackVisual reservedCupStack;
        private int dragPointerId;
        private string dragItemId,dragSourceId,dragMachineId,dragOutputId,confirmCancelId;
        private bool committingDrag, clearingDrag, dragPreview;
        private bool quitting;
        private string dragAudioSpecId;
        private Vector3 dragRustlePosition;
        private float dragRustleDistance, nextDragRustle;
        private Vector3 origin,offset;
        private Camera dragCamera;
        private Plane dragPlane;
        private Vector2 pointerScreen;
        private bool overUI;
        private int dragFinishedFrame=-1;
        private float nextVisualRefresh;
        private bool draggingVessel, draggingCupPickupPreview, pouredThisDrag;
        private bool vesselTiltMode;
        private bool vesselWasTilted;
        private Vector2 vesselTiltPointerOrigin;
        private float vesselTilt;
        private readonly TouchPourGestureRecognizer touchPourGesture = new TouchPourGestureRecognizer();
        private const float VesselTiltPixels = 90f;
        private const float VesselTiltResponseSeconds = .045f;
        private const float VesselPourThreshold = .18f;
        private PointerEventData vesselPointer;
        private string lastPourTarget;
        private string lockedPourTargetId, lastPourError;
        private FreeEntityView receivingView;
        public bool IsVesselTiltMode => vesselTiltMode;
        public string LockedPourTargetId => lockedPourTargetId;
        public string CurrentPourTargetId => !IsDragging || !draggingVessel ? null
            : vesselTiltMode ? lockedPourTargetId : ResolvePourTarget()?.id;
        private void Awake()
        {
            if (Application.isMobilePlatform)
                Hint = "按住器皿拖动；对准目标后停留，再上下滑动倾倒，抬起手指停止。";
            // A session owner may already have a save entry before this scene loads.
            // Only another initialized scene adapter is a duplicate runtime.
            foreach(var other in FindObjectsByType<FreeCafeRuntime>(FindObjectsSortMode.None))
                if(other!=this && other.World!=null)
                {
                    enabled=false;
                    throw new InvalidOperationException("Duplicate FreeCafe runtime in loaded scenes: "+other.gameObject.scene.path);
                }
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
            foreach(var view in authored)
                if(!view.gameObject.activeInHierarchy) inactiveAuthoredIds.Add(view.AuthoringEntityId);
            authored.RemoveAll(view => inactiveAuthoredIds.Contains(view.AuthoringEntityId));
            var catalog = JsonUtility.FromJson<CafeCatalog>(catalogAsset.text);
            if (seating != null) seating.Configure(catalog);
            var extractionConfig=GetComponent<FreeCafeExtractionConfig>();
            if(extractionConfig!=null) extractionConfig.Apply(catalog.production);
            catalog.layout = FreeCafeSceneLayout.Capture(this, authored);
            FreeCafeSceneLayout.FitAuthoredBounds(this, catalog, authored);
            characterCatalog=Spotlight.Narrative.CharacterCatalog.LoadFromTables(ResolveNarrativeConfiguration());
            // Resolve the entry before any scene-owned narrative fallback is created.
            IsSandboxMode = FindFirstObjectByType<GameFlowBootstrap>() == null;
            var identities = customerIdentitySource as ICafeCustomerIdentitySource;
            if(identities==null && characterCatalog!=null && characterCatalog.Characters.Count>0)
                identities=new CatalogCustomerIdentitySource(characterCatalog);
            // Named/supplied visitors must not leak into the ordinary opening batch.
            // Explicit scene identity providers remain supported; NCS visits use CafeVisitBridge.
            World=new CafeWorld(catalog,identities);
            if(prefabLibrary==null) throw new InvalidOperationException("请配置 EntityPrefabLibrary");
            prefabLibrary.Validate(World.Catalog);
            spawnScale=new FreeCafeSpawnScale(this,authored,catalog);
            if(productionMode) World.EnableProduction();
            AdoptAuthoredEntities(authored);
            World.Changed+=SyncViews;
            World.ProductionCupHandedOver+=OnProductionCupHandedOver;
            var audioPresenter = GetComponent<FreeCafeAudioPresenter>();
            if (audioPresenter == null) audioPresenter = gameObject.AddComponent<FreeCafeAudioPresenter>();
            audioPresenter.Bind(this);
            PlayerCafeOwner.Bind(World, CaptureJson, RestoreJson);
        }

        /// <summary>
        /// 复用已初始化的 NCS 配置快照。进入咖啡馆时（Awake 角色目录 + Start 教程配置）
        /// 原本会各做一次完整的 41 张 Luban 表解析，属于转场遮罩内最大的单帧开销之一。
        /// 只有沙盒/隔离场景没有 NarrativeRuntime 时才回退到独立加载。
        /// </summary>
        private static NarrativeConfiguration ResolveNarrativeConfiguration()
        {
            var runtime = NarrativeRuntime.Instance;
            return runtime != null && runtime.Configuration != null
                ? runtime.Configuration
                : LubanNarrativeLoader.Load();
        }

        private void Start()
        {
            WorkstationTutorial = gameObject.AddComponent<FreeCafeWorkstationTutorial>();
            WorkstationTutorial.Initialize(this);
            tutorialConfiguration = ResolveNarrativeConfiguration();
            dayFlow = FindFirstObjectByType<DayFlowController>();
            gameFlow = FindFirstObjectByType<GameFlowBootstrap>();
            playerHud=FindFirstObjectByType<FreeCafeHud>(); productionHud=GetComponent<FreeCafeProductionHud>();
            string pending=GameSaveParticipantRegistry.TakePending(SaveId);
            if(!string.IsNullOrEmpty(pending)) RestoreJson(pending); else SyncViews();
            World.ConfigureEquipmentProgression(IsSandboxMode);
            // Formal saves restore their calendar after scene Start. Wait for Playing
            // before choosing a date key, or a day-3 save could be refilled as day 1.
            if (IsSandboxMode) { RefreshDailyCups(); pendingDailyCupRefresh = false; }
            equipmentPanel = gameObject.AddComponent<FreeCafeEquipmentPanel>();
            equipmentPanel.Initialize(this, playerHud);
            equipmentTutorialPrompt = gameObject.AddComponent<FreeCafeEquipmentTutorialPrompt>();
            equipmentTutorialPrompt.Initialize(this);
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.Initialize(this);
            if(World.State.currentArea==AreaId.Cafe)
                ShowHint("点击大厅的「开始营业」挂牌开始接单；点击客人查看订单，或前往制作台备料。");
            else if(World.ProductionEnabled)
                ShowHint("点击杯具选择出餐杯；原料拖到设备，点击启动。左键拖动壶，按住右键并上下移动鼠标双向倾倒，松开右键停止。");
            else
                ShowHint("点击来源取出物品，拖拽到任意位置；拖到容器或设备会尝试组合。Shift 拖拽仅摆放。");
            var ambientAdele = new GameObject("AmbientAdele").AddComponent<FreeCafeAmbientAdele>();
            ambientAdele.transform.SetParent(areaRoots[(int)AreaId.Cafe], false);
            ambientAdele.Initialize(this, tutorialConfiguration);
            Debug.Log("[FreeCafe] 自由操作世界已启动：无工作台插槽、无订单步骤门禁。",this);
        }
        private void OnEnable()
        {
            if (World != null) SyncViews();
        }
        private void OnApplicationQuit() => quitting = true;
        private void OnApplicationPause(bool paused)
        {
            if (paused && Application.isMobilePlatform)
            {
                touchPourGesture.Reset();
                CancelDrag();
            }
        }
        private void OnDisable()
        {
            GameAudio.StopOwner(this);
            var audioPresenter = GetComponent<FreeCafeAudioPresenter>();
            if (audioPresenter != null) audioPresenter.ResetObservation();
            CancelDrag();
            cafeVisits?.Dispose();
            cafeVisits = null;
        }
        private void OnDestroy()
        {
            cafeVisits?.Dispose();
            cafeVisits = null;
            if(World!=null) { World.Changed-=SyncViews; World.ProductionCupHandedOver-=OnProductionCupHandedOver; }
            PlayerCafeOwner.Unbind(World);
        }
        // Story sessions only: the sandbox has no NCS faction tendencies to change.
        private void OnProductionCupHandedOver(bool latteArt, string pattern)
        {
            if (IsSandboxMode) return;
            string factionBar = LatteArtFactionStreak.Record(World.State, latteArt, pattern);
            if (factionBar != null) LatteArtFactionStreak.Award(this, factionBar, pattern);
        }
        private void Update()
        {
            if(World==null) return;
            // A destroyed drag object compares equal to null in Unity. Its source/output
            // ids still need clearing before another entity can acquire the pointer.
            if (!IsDragging && HasDragState) CancelDrag();
            if (pendingTutorialCoffee && !TutorialInputBlocked && WorkstationTutorial != null
                && WorkstationTutorial.BeginFirstCafeTutorial())
            {
                pendingTutorialCoffee = false;
                cafeTutorial.BeginCoffee();
            }
            bool sessionReady = gameFlow == null || gameFlow.SessionState == GameSessionState.Playing;
            if (sessionReady)
            {
                if (pendingDailyCupRefresh) { RefreshDailyCups(); pendingDailyCupRefresh = false; }
                if (!IsSandboxMode || NarrativeRuntime.Instance != null)
                {
                    if (cafeVisits == null) cafeVisits = new CafeVisitBridge(this);
                    cafeVisits.Pump();
                }
                if (pendingBusinessStart) TryStartBusiness();
                if (!BusinessSimulationPaused && (IsSandboxMode || Spotlight.Narrative.NarrativeRuntime.Instance == null || cafeVisits != null && cafeVisits.Ready)) World.Tick(Time.deltaTime);
                if (!IsSandboxMode || NarrativeRuntime.Instance != null) cafeVisits?.Pump();
            }
            if (IsDragging && PresentationPauseRegistry.IsPaused) CancelDrag();
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
                    if(views.TryGetValue(entity.id,out var view) && view!=null && view!=draggedView) view.Refresh(entity);
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
            if (committingDrag || clearingDrag || quitting || !Application.isPlaying || !isActiveAndEnabled || !gameObject.scene.isLoaded) return;
            removeIds.Clear();
            foreach(var pair in views) if(World.Find(pair.Key)==null) { if(pair.Value!=null) { pair.Value.RetireInteraction(); Destroy(pair.Value.gameObject); } removeIds.Add(pair.Key); }
            foreach(var id in removeIds) views.Remove(id);
            foreach(var entity in World.State.entities)
            {
                // Saved domain data remains authoritative for stock and references, but
                // cannot resurrect an instance disabled by the scene author at Awake.
                if(inactiveAuthoredIds.Contains(entity.id)) continue;
                if(!views.TryGetValue(entity.id,out var view) || view==null)
                {
                    view=CreateEntityView(entity.specId,entity.area); view.name=entity.id; views[entity.id]=view;
                    view.Bind(this,entity);
                    if (entity.area == AreaId.Cafe && World.Spec(entity.specId).Has(Capability.Customer))
                        view.PlayArrivalFade();
                }
                if(view!=draggedView && view!=dragCompanion)
                {
                    if(view.transform.parent!=areaRoots[(int)entity.area]) view.transform.SetParent(areaRoots[(int)entity.area],true);
                    view.Refresh(entity);
                }
            }
            if (draggedView != null && !IsRegisteredView(dragEventOwner)) CancelDrag();
            areaManager.Focus(World.State.currentArea);
            if (seating != null) seating.RefreshChairs();
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
        public void Report(ActionResult result) => Report(result, null);
        private void Report(ActionResult result, string deliveryCue)
        {
            ShowHint(result.message);
            if(!result.success) OperationToastPanel.ShowMessage(result.message);
            else
            {
                string cue = deliveryCue ?? FreeCafeAudioPresenter.FeedbackKey(result.feedback);
                if (result.feedback == CafeFeedbackKind.DeliverySatisfied || result.feedback == CafeFeedbackKind.DeliveryRemake || result.feedback == CafeFeedbackKind.DeliveryFailed)
                    OperationToastPanel.ShowMessage(result.message, cue);
                else GameAudio.Play(cue);
            }
            Debug.Log("[FreeCafe] "+(result.success ? "OK " : "拒绝 ")+result.message,this);
        }
        public ActionResult RingDeliveryBell()
        {
            if (InputBlocked || IsDragging) return ActionResult.No("当前不能按铃交付");
            if (MilkPitcherTutorial != null && MilkPitcherTutorial.IsRunning) return ActionResult.No("拉花练习不交付正式订单，请完成拉花后退出练习");
            if (PracticeTutorialActive && !WorkstationTutorial.IsFirstCafeTutorial)
            {
                var practice = WorkstationTutorial.RingPracticeBell();
                Report(practice);
                return practice;
            }
            return DeliverCup(true);
        }
        public ActionResult DeliverCup(bool fromBell = false)
        {
            if(InputBlocked) return ActionResult.No("当前不能交付");
            if (MilkPitcherTutorial != null && MilkPitcherTutorial.IsRunning) return ActionResult.No("拉花练习不交付正式订单，请先退出练习");
            if (PracticeTutorialActive && (!WorkstationTutorial.CanSubmitCup || !fromBell))
            {
                string message = WorkstationTutorial.CanSubmitCup
                    ? "请点击工作台右下角的按铃，完成交付"
                    : "请先按照教程完成咖啡，并把杯子放到案板上的盘子里";
                WorkstationTutorial.ReportError(message);
                return ActionResult.No(message);
            }
            bool tutorialOrder = CafeTutorialActive && cafeTutorial != null && cafeTutorial.WaitingForCoffee
                && World.State.focusedRequestId == CafeWorld.TutorialOrderId;
            var request = World.Request(World.State.focusedRequestId);
            var cup = World.ServingCup;
            var product = request == null ? null : World.Product(request.productId);
            var quality = cup != null && product != null ? World.EvaluateProduction(cup, product, World.RequiredCupSpecId(request)) : default;
            string productName = product == null ? "咖啡" : FreeCafeProductionText.Recipe(product.label);
            var customer = request == null ? null : World.Find(request.customerId);
            string customerName = World.IsNarrativeTastingOrder(request) ? "试喝饮品" : customer == null ? "顾客" : World.PersonName(customer);
            int moneyBefore = World.State.money, reputationBefore = World.State.reputation;
            var result=World.DeliverProductionCup();
            var receipt = new CafeDeliveryReceipt(productName, customerName, quality,
                World.State.money - moneyBefore, World.State.reputation - reputationBefore,
                result.feedback, result.message);
            Report(result, result.success ? FreeCafeAudioPresenter.DeliveryKey(receipt) : null);
            if (tutorialOrder && result.success && result.feedback == CafeFeedbackKind.DeliverySatisfied)
            {
                WorkstationTutorial?.Finish(true);
                NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.CoffeeSignal);
                ShowHint("首位客人的黑咖啡已交付！接下来继续学习咖啡谱与营业流程。");
                var guide = BusinessTutorial ?? gameObject.AddComponent<FreeCafeBusinessTutorial>();
                guide.Begin(this);
            }
            if (result.success) CupDelivered?.Invoke(receipt);
            return result;
        }
        public void Inspect(string id)
        {
            // 域重载或初始化前可能先收到指针进入事件。
            if(World==null) return;
            var entity=World.Find(id); if(entity==null) return;
            var spec=World.Spec(entity.specId);
            string info=spec.label;
            if(World.ProductionEnabled && (World.IsProductionVessel(entity) || spec.Has(Capability.Container) || spec.Has(Capability.Processor)))
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
            if(World==null || view==null || !view.CanInteract || IsDragging || dragFinishedFrame==Time.frameCount) return;
            if (InputBlocked) { NotifyBlockedOperation(); return; }
            var entity=World.Find(view.EntityId); if(entity==null || entity.area!=World.State.currentArea) return;
            if (cafeTutorial != null && cafeTutorial.WaitingForWorkstation) return;
            if (WorkstationTutorial != null && !WorkstationTutorial.AllowClick(entity.id)) return;
            var spec=World.Spec(entity.specId);
            if(spec.Has(Capability.BusinessControl))
            {
                var sign = view.GetComponent<FreeCafeBusinessSignView>();
                if (sign != null && sign.IsFlipping)
                {
                    if (World.State.open && CanEndBusiness) sign.CloseAfterFlip(RequestBusinessEnd);
                    return;
                }
                if (World.State.open)
                {
                    if (sign != null && CanEndBusiness) sign.CloseAfterFlip(RequestBusinessEnd);
                    else RequestBusinessEnd();
                }
                // A settled day cannot reopen; the closed sign is the way back out to the city map.
                else if (IsClosedForToday) OpenMapFromClosedSign();
                else StartBusiness();
                return;
            }
            if(World.ProductionEnabled && spec.Has(Capability.Delivery))
            { ShowHint("把饮品放到案板上的盘子里，再点击工作台右下角按铃交付"); return; }
            if(World.ProductionEnabled && entity.specId=="tool.latte-art") { Report(World.RequestProductionMiniGame()); return; }
            if(World.ProductionEnabled && World.IsMilkPitcher(entity)) { SelectedId=entity.id; Report(World.FrothMilk(entity.id)); return; }
            if(World.ProductionEnabled && spec.Has(Capability.Source) && World.Spec(spec.sourceItemId).Has(Capability.Container))
            {
                var stack = view.GetComponent<FreeCafeCupStackVisual>();
                var start = stack != null ? stack.TopPosition : view.transform.position;
                var previous = World.ServingCup;
                var result = World.SelectServingCup(entity.id);
                if (result.success && World.ServingCup != null && World.ServingCup != previous)
                    View(World.ServingCup.id)?.StartMotionFrom(start);
                Report(result); return;
            }
            if(World.ProductionEnabled && spec.Has(Capability.Processor))
            { SelectedId=entity.id; OperateWithAudio(entity); return; }
            if(spec.Has(Capability.Source))
            {
                var stack = view.GetComponent<FreeCafeCupStackVisual>();
                var pickupPosition = stack != null ? stack.TopPosition : view.transform.position;
                var result=World.Take(entity.id,entity.area,entity.x,entity.y-1.4f,out var item);
                if(result.success)
                {
                    View(item.id)?.StartMotionFrom(pickupPosition);
                    GameAudio.Play(FreeCafeSoundDynamics.PickupKey(item.specId), this, .5f);
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
                    OperateWithAudio(entity);
                    return;
                }
                if(entity.task.phase==MachinePhase.Ready)
                {
                    confirmCancelId=null;
                    OperateWithAudio(entity);
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
                string requestId = null;
                foreach (var request in World.State.requests)
                    if (request.customerId == entity.id) { requestId = request.id; break; }
                if (requestId != null) World.Focus(requestId);
                if (cafeVisits != null && cafeVisits.Click(entity.id))
                {
                    if (requestId != null) BusinessTutorial?.CustomerInspected(entity.id);
                    return;
                }
                if (requestId != null)
                {
                    var result=World.Talk(entity.id);
                    Report(result);
                    if(result.success)
                    {
                        if (NarrativeCafeAmbientContent.TryCreateOrderDialogue(World, entity, out var orderDialogue))
                            DialoguePlayback.Play(orderDialogue);
                        else CafeDialoguePlayback.PlayCustomer(entity.person, result.message);
                        BusinessTutorial?.CustomerInspected(entity.id);
                    }
                }
            }
            else Select(entity.id);
        }
        private void OperateWithAudio(WorldEntity machine)
        {
            var heated = machine.specId == "machine.stove" ? World.Find(machine.production?.heatingVesselId) : machine;
            ProductionPhase? before = heated?.production?.phase;
            bool selfHeating = heated?.production?.selfHeating == true;
            MachinePhase? taskBefore = machine.task?.phase;
            var result = World.ProductionEnabled ? World.OperateProduction(machine.id) : World.Operate(machine.id);
            if (result.success && result.feedback == CafeFeedbackKind.None &&
                (World.ProductionEnabled ? before != heated?.production?.phase || selfHeating != (heated?.production?.selfHeating == true)
                    : taskBefore != machine.task?.phase))
                GameAudio.Play(AudioKeys.MachineSwitch, this);
            Report(result);
        }
        public void CancelProcessing(FreeEntityView view)
        {
            if(World==null || view==null || !view.CanInteract || InputBlocked || IsDragging) return;
            var entity=World.Find(view.EntityId);
            if(entity==null || entity.area!=World.State.currentArea || !World.Spec(entity.specId).Has(Capability.Processor)) return;
            if (PracticeTutorialActive) { WorkstationTutorial.ReportError("练习中请勿清空设备；请按当前提示操作。"); return; }
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
        // Report on press, not release/drag threshold, so a stationary hold also explains the empty bag.
        public void Press(FreeEntityView view)
        {
            if(World==null || view==null || !view.CanInteract || InputBlocked || IsDragging) return;
            var entity=World.Find(view.EntityId);
            if(entity==null || entity.area!=World.State.currentArea) return;
            if(entity.specId=="source.beans" && entity.stock<=0)
                ShowHint("在咖啡谱的补货处补货");
        }
        public void BeginDrag(FreeEntityView view,PointerEventData e)
        {
            if(World==null || InputBlocked || IsDragging || committingDrag || clearingDrag || view==null || !view.CanInteract || e==null) return;
            if (CafeBusinessOnboardingActive)
            {
                ShowHint("请先完成咖啡谱与营业教程，再继续制作饮品。");
                return;
            }
            if (HasDragState) CancelDrag();
            var inputCamera=e.pressEventCamera!=null ? e.pressEventCamera : worldCamera;
            if(inputCamera==null || !inputCamera.isActiveAndEnabled) { ShowHint("游戏摄像机未启用，无法拖拽实体"); return; }
            var entity=World.Find(view.EntityId); if(entity==null || entity.area!=World.State.currentArea) return; var spec=World.Spec(entity.specId);
            if (WorkstationTutorial != null && !WorkstationTutorial.AllowDrag(entity.id)) return;
            if(World.ProductionEnabled && World.IsMilkPitcher(entity) && World.MilkFoamVolume(entity)<=.001f)
            {
                ShowHint("请先向拉花杯加入牛奶，点击拉花杯制成奶泡后，再拖到杯子上拉花");
                return;
            }
            if(World.CupAbove(entity)!=null) { ShowHint("请先取走最上面的杯子"); return; }
            if(World.Locked(entity)) { ShowHint("正在加工，点击设备可中止"); return; }
            if(World.ProductionEnabled && World.IsContainer(entity))
            { World.SuspendContainerHeating(entity.id); draggingVessel=true; dragItemId=entity.id; draggedView=view; SelectedId=entity.id; }
            else if(spec.Has(Capability.Source))
            {
                if(entity.stock<=0) { ShowHint(spec.id=="source.beans" ? "在咖啡谱的补货处补货" : "来源库存不足"); return; }
                dragPreview=true; dragSourceId=entity.id;
                draggedView=CreateEntityView(spec.sourceItemId,World.State.currentArea); draggedView.name="UncommittedPickup";
                draggedView.Bind(this,new WorldEntity { specId=spec.sourceItemId,area=entity.area,x=entity.x,y=entity.y },true);
                if(entity.specId=="source.beans" && view.body!=null && view.body.sprite!=null)
                {
                    var bagBounds=view.body.sprite.bounds;
                    var bagMouth=bagBounds.center+Vector3.up*(bagBounds.size.y*.22f);
                    draggedView.TeleportPresentation(view.body.transform.TransformPoint(bagMouth));
                    draggedView.PlayPickupReveal();
                }
                if(World.ProductionEnabled && World.Spec(spec.sourceItemId).Has(Capability.Container))
                {
                    // A cup-stack pickup is still provisional until release, but it
                    // should respond to the same right-button tilt gesture as a cup.
                    draggingVessel=true;
                    draggingCupPickupPreview=true;
                }
            }
            else if(spec.Has(Capability.Processor) && (spec.dragOutput || World.ProductionEnabled && entity.specId=="machine.mixer"))
            {
                var output=World.DragOutputPreview(entity.id);
                if(output==null) { ShowHint(entity.task==null ? "先放入咖啡豆并完成研磨，再拖出咖啡粉" : "研磨尚未完成，完成后可拖出咖啡粉"); return; }
                dragPreview=true; dragMachineId=entity.id;dragOutputId=output.id;
                draggedView=CreateEntityView(output.specId,World.State.currentArea);draggedView.name="UncommittedMachineOutput";
                draggedView.Bind(this,output,true);
                if (entity.specId == "machine.grinder")
                {
                    // Grounds use the same filled-drawer artwork in flight and after placement.
                    var grinderVisual = view.GetComponent<FreeCafeGrinderVisual>();
                    if (grinderVisual != null) draggedView.TeleportPresentation(grinderVisual.DrawerWorldPosition);
                }
            }
            else if(spec.Has(Capability.Movable)) { dragItemId=entity.id; draggedView=view; SelectedId=entity.id; }
            else return;
            if (dragSourceId != null)
            {
                reservedCupStack = view.GetComponent<FreeCafeCupStackVisual>();
                if (reservedCupStack != null) draggedView.transform.position = reservedCupStack.TopPosition;
            }
            else if (dragItemId != null && !string.IsNullOrEmpty(entity.cupStackBelowId))
            {
                var belowView = View(entity.cupStackBelowId);
                if (belowView != null) reservedCupStack = belowView.GetComponent<FreeCafeCupStackVisual>();
            }
            dragEventOwner=view; dragPointerId=e.pointerId;
            dragCamera=inputCamera;origin=draggedView.transform.position;
            dragPlane=new Plane(Vector3.forward,origin);
            // BeginDrag occurs only after the mouse has crossed the drag threshold.
            // Anchor at the original press, not the moved cursor, to avoid a permanent offset.
            var pressPosition=e.pointerPressRaycast.module!=null ? e.pressPosition : e.position;
            if(!TryToWorld(pressPosition,out var point)) { CancelDrag();ShowHint("鼠标射线无法命中实体所在平面");return; }
            offset=origin-point;
            if (dragItemId != null && World.ProductionEnabled && (World.IsPrecisionExtractor(entity) || World.IsExtractionFilter(entity)))
            {
                var prepared = World.CanDragExtractionVessel(entity.id);
                if (!prepared.success) { CancelDrag(); ShowHint(prepared.message); return; }
                var jug = World.IsExtractionFilter(entity) ? World.ExtractionJug(entity) : null;
                if (jug != null)
                {
                    World.SuspendContainerHeating(jug.id);
                    dragCompanion = View(jug.id);
                    if (dragCompanion != null) dragCompanion.hitbox.enabled = false;
                }
            }
            if (reservedCupStack != null) reservedCupStack.ReserveTopCup(true);
            draggedView.BeginPointerMotion();
            dragAudioSpecId = draggedView.PrefabSpecId;
            dragRustlePosition = WorldToArea(World.State.currentArea, origin);
            dragRustleDistance = 0; nextDragRustle = Time.unscaledTime + FreeCafeSoundDynamics.DragRustleCooldown;
            GameAudio.Play(FreeCafeSoundDynamics.PickupKey(dragAudioSpecId), this, .5f);
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
            return source==null ? null : new WorldEntity { specId=World.Spec(source.specId).sourceItemId,area=World.State.currentArea,quality=source.quality };
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
                if(view!=null && view!=draggedView && view!=dragCompanion)
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
            if(!IsDragging || clearingDrag || committingDrag || e==null) return;
            var movingView = draggedView;
            if(InputBlocked || !TryToWorld(e.position,out var point)) { CancelDrag(); return; }
            if(draggingVessel && Mouse.current!=null && Mouse.current.rightButton.isPressed && !vesselTiltMode)
                BeginVesselTilt();
            pointerScreen=e.position;
            // 左键落点冻结；右键移动只控制倾斜，不重新寻找接收物。
            if(!vesselTiltMode) { draggedView.MovePointer(point+offset); FindReceiver(e); }
            if (!IsDragging || draggedView != movingView) return;
            if (dragAudioSpecId == "beans" && !vesselTiltMode)
            {
                var position = WorldToArea(World.State.currentArea, point + offset);
                float distance = Vector3.Distance(position, dragRustlePosition);
                dragRustleDistance += distance;
                dragRustlePosition = position;
                if (distance > .0001f && dragRustleDistance >= FreeCafeSoundDynamics.DragDistanceForRustle && Time.unscaledTime >= nextDragRustle)
                {
                    GameAudio.Play(AudioKeys.BeansPickup, this, .35f);
                    dragRustleDistance = 0; nextDragRustle = Time.unscaledTime + FreeCafeSoundDynamics.DragRustleCooldown;
                }
            }
            draggedView.body.sortingOrder=2000;
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            var item=DragData();
            if(item==null)
            {
                bool outputChanged = dragPreview && dragMachineId != null;
                CancelDrag();
                ShowHint(outputChanged ? "设备产物已变化，拖拽已取消" : "物品状态已变化，拖拽已取消");
                return;
            }
            var pos=WorldToArea(World.State.currentArea,draggedView.DragPosition);
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
                    if (World.IsExtractionFilter(item) && World.IsPrecisionExtractor(target)
                        || World.IsPrecisionExtractor(item) && World.IsExtractionFilter(target))
                    {
                        var assembly = World.Preview(item, target);
                        receiver.SetDropTarget(true, !assembly.success);
                        ShowHint(assembly.message); return;
                    }
                    bool sourceStack = target != null && World.Spec(target.specId).Has(Capability.Source);
                    if (sourceStack)
                    {
                        var canReturn = World.Preview(item, target);
                        receiver.SetDropTarget(true, !canReturn.success);
                        ShowHint(canReturn.message); return;
                    }
                    bool valid=target!=null && (World.IsContainer(target) || target.specId=="machine.stove" || World.Spec(target.specId).Has(Capability.Delivery) || World.Spec(target.specId).Has(Capability.Discard));
                    receiver.SetDropTarget(true,!valid);
                }
                ShowHint("左键拿起；按住右键锁住目标，上下移动双向倾倒，回到按下点停水；松右键继续移动。");
                return;
            }
            var result=receiver!=null && !placeOnly ? World.Preview(item,World.Find(receiver.EntityId)) : World.CanPosition(item.specId,World.State.currentArea,pos.x,pos.y);
            if(receiver!=null && !placeOnly) receiver.SetDropTarget(true,!result.success);
            ShowHint(overUI ? "不能放在界面卡片上；松手取消" : result.message+(placeOnly ? " · 仅摆放" : " · Shift 可仅摆放"));
        }
        public void EndDrag(PointerEventData e)
        {
            if(!IsDragging || clearingDrag || committingDrag || e==null) return;
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
            if (WorkstationTutorial != null && !WorkstationTutorial.AllowDrop(targetId, placeOnly)) { CancelDrag(); return; }
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
            bool cupPickup = sourceId != null && reservedCupStack != null;
            bool returnedCup = World.IsCup(World.Find(itemId)) && receiver != null
                && World.Spec(World.Find(receiver.EntityId).specId).sourceItemId == ghost.PrefabSpecId;
            string releasedSpecId = ghost.PrefabSpecId;
            bool materialAddition = !placeOnly && targetId != null && World.Spec(releasedSpecId).Has(Capability.Portion);
            bool wasPreview=dragPreview;
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
                else if(wasVessel && World.ShouldReturnVessel(World.Find(itemId)) && (wasTilted || placeOnly || targetId==null)) result=ActionResult.Yes("器皿松手归位");
                else if(wasVessel && wasTilted) result=World.Move(itemId,World.State.currentArea,position.x,position.y);
                else if(targetId!=null && !placeOnly) result=World.Interact(itemId,targetId);
                else result=World.Move(itemId,World.State.currentArea,position.x,position.y);
                var released=World.Find(itemId);
                if(World.ShouldReturnVessel(released))
                {
                    World.ReturnVesselHome(itemId);
                }
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
                    if (views.TryGetValue(placed.id, out var oldView) && oldView != null && oldView != ghost) { oldView.RetireInteraction(); Destroy(oldView.gameObject); }
                    views[placed.id] = ghost; ghost.name = placed.id; ghost.Bind(this, placed);
                }
                else if (cupPickup) RetireDragPreview(ghost);
                else ghost.AnimateReturn(dropPosition,rejectedTarget != null ? rejectedTarget.transform.position : origin,true);
            }
            else if (result.success && itemId != null)
            {
                var item = World.Find(itemId);
                if (item == null || !string.IsNullOrEmpty(item.ownerId))
                {
                    views.Remove(itemId);
                    if (returnedCup) RetireDragPreview(ghost);
                    else ghost.AnimateReturn(dropPosition,rejectedTarget != null ? rejectedTarget.transform.position : origin,true);
                }
            }
            SyncViews();
            if(!result.success)
            {
                if(rejectedTarget!=null) rejectedTarget.RejectDrop();
                if (cupPickup) RetireDragPreview(ghost);
                else if(ghost!=null)
                {
                    var released=World.Find(itemId);
                    var home=World.ShouldReturnVessel(released) ? AreaToWorld(released.area,released.x,released.y) : origin;
                    ghost.AnimateReturn(dropPosition,home,wasPreview);
                }
            }
            if (result.success && result.feedback == CafeFeedbackKind.None && !wasTilted)
                GameAudio.Play(materialAddition ? FreeCafeSoundDynamics.AdditionKey(World, releasedSpecId) : AudioKeys.Place, this);
            if (!validScreen)
            {
                if (isActiveAndEnabled && !InputBlocked) GameAudio.Play(AudioKeys.DragCancel, this, .6f);
                ShowHint(result.message);
            }
            else Report(result);
        }
        private void RetireDragPreview(FreeEntityView preview)
        {
            preview.RetireInteraction(); preview.gameObject.SetActive(false); Destroy(preview.gameObject);
        }
        private bool HasDragState => draggedView != null || dragEventOwner != null ||
            dragItemId != null || dragSourceId != null || dragMachineId != null;

        private void ClearDrag()
        {
            if (clearingDrag) return;
            clearingDrag = true;
            var view = draggedView;
            var itemId = dragItemId;
            var companion = dragCompanion;
            var stack = reservedCupStack;
            var target = receiver;
            bool wasVessel = draggingVessel;
            // FinishPour can notify world/UI listeners synchronously. End ownership
            // before publishing anything so cancellation cannot re-enter this drag.
            draggedView=null; receiver=null; dragEventOwner=null; dragCamera=null;
            dragSourceId=dragItemId=dragMachineId=dragOutputId=null;
            dragAudioSpecId=null; dragRustleDistance=0;
            GameAudio.SetLoop(this, "pour", AudioKeys.Pour, false);
            GameAudio.SetLoop(this, "spill", AudioKeys.Spill, false);
            dragCompanion=null; reservedCupStack=null; dragPreview=false;
            draggingVessel=false; draggingCupPickupPreview=false; vesselTiltMode=false; vesselWasTilted=false; vesselTilt=0;
            touchPourGesture.Reset();
            dragFinishedFrame=Time.frameCount;
            try
            {
                if(wasVessel) StopVesselPour();
                ReleasePourTarget();
                if(stack!=null) stack.ReserveTopCup(false);
                if(companion!=null) companion.hitbox.enabled=World.Find(companion.EntityId)?.area==World.State.currentArea;
                if(target!=null) target.SetDropTarget(false,false);
                if(view!=null)
                {
                    view.EndPointerMotion(); view.PresentPour(0,false);
                    if(itemId!=null) view.hitbox.enabled=World.Find(itemId)?.area==World.State.currentArea;
                }
            }
            finally { clearingDrag=false; }
        }
        public void CancelDrag()
        {
            if(clearingDrag || committingDrag || !HasDragState) return;
            bool cupPickup=dragSourceId!=null && reservedCupStack!=null;
            if (isActiveAndEnabled && !InputBlocked && !PresentationPauseRegistry.IsPaused && !quitting) GameAudio.Play(AudioKeys.DragCancel, this, .6f);
            var view=draggedView; string id=dragItemId; bool provisional=dragPreview;
            ClearDrag();
            var released=World.Find(id);
            var returnPosition=origin;
            if(World.ShouldReturnVessel(released))
            { World.ReturnVesselHome(id); returnPosition=AreaToWorld(released.area,released.x,released.y); }
            if(view!=null)
            {
                if(cupPickup || provisional && !view.gameObject.activeInHierarchy) RetireDragPreview(view);
                else view.AnimateReturn(view.transform.position,returnPosition,provisional);
            }
            SyncViews(); ShowHint("拖拽取消，库存不变");
        }
        public void Enter(AreaId area)
        {
            if (!CanEnterArea(area)) { NotifyBlockedOperation(); return; }
            if (CafeTutorialWaitingForWorkstation)
            {
                if (area != AreaId.Counter || World.Area(area) == null || areaManager.Find(area) == null) return;
                CancelDrag();
                bool opened = areaManager.Focus(area);
                if (opened)
                {
                    World.EnterProduction();
                    NotifyTutorialStepCompleted(CafeTutorialSession.TutorialId, CafeTutorialSession.WorkstationSignal);
                }
                return;
            }
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

        public bool CanEnterArea(AreaId area)
        {
            if (World == null || World.Area(area) == null || areaManager.Find(area) == null || TutorialInputBlocked) return false;
            if (CafeTutorialWaitingForWorkstation) return area == AreaId.Counter;
            if (CafeBusinessOnboardingActive) return !InputBlocked && (area == AreaId.Cafe || area == AreaId.Counter);
            if (WorkstationTutorial != null && WorkstationTutorial.IsFirstCafeTutorial) return false;
            return !InputBlocked;
        }

        private void BeginVesselTilt()
        {
            if (World.IsMilkPitcher(World.Find(dragItemId))) return;
            // 用右键按下前的左键落点选择一次，此后仅更新倾斜量。
            if(vesselPointer==null) vesselPointer=new PointerEventData(EventSystem.current);
            vesselPointer.position=pointerScreen;
            FindReceiver(vesselPointer);
            var target=ResolvePourTarget();
            // Reject a tutorial misclick before freezing the vessel or owning a pour attempt.
            if (WorkstationTutorial != null && !WorkstationTutorial.AllowPour(dragItemId, target?.id)) return;
            vesselTiltMode=true;
            vesselWasTilted=true;
            vesselTiltPointerOrigin=pointerScreen;
            vesselTilt=0;
            draggedView.FreezePointerMotion();
            lockedPourTargetId=target?.id;
            lastPourError=null;
            receivingView=target==null ? null : View(target.id);
            if(receivingView!=null) receivingView.SetDropTarget(true,false);
        }
        private void UpdateVesselInput()
        {
            if (MobileTouchInput.TryGetPrimaryTouch(out Vector2 touchPosition, out bool touchHeld))
            {
                pointerScreen = touchPosition;
                if (touchHeld && !vesselTiltMode && !World.IsMilkPitcher(World.Find(dragItemId)))
                {
                    if (vesselPointer == null && EventSystem.current != null)
                        vesselPointer = new PointerEventData(EventSystem.current);
                    if (vesselPointer != null)
                    {
                        vesselPointer.position = touchPosition;
                        FindReceiver(vesselPointer);
                        WorldEntity target = overUI ? null : ResolvePourTarget();
                        if (touchPourGesture.Update(target != null ? target.id : null, touchPosition,
                            Time.unscaledTime, out Vector2 pourOrigin))
                        {
                            BeginVesselTilt();
                            if (vesselTiltMode) vesselTiltPointerOrigin = pourOrigin;
                            touchPourGesture.Reset();
                        }
                        else if (target != null && touchPourGesture.IsArmed)
                            ShowHint("目标已对准；上下滑动倾倒，抬起手指停止。牛奶壶请拖到杯上拉花，不要倾倒。");
                    }
                }
                else if (!touchHeld) touchPourGesture.Reset();

                UpdateVesselInputState(touchPosition, touchHeld, touchHeld);
                return;
            }

            var mouse=Mouse.current;
            if(mouse==null) return;
            bool rightHeld=mouse.rightButton.isPressed && !World.IsMilkPitcher(World.Find(dragItemId));
            if(rightHeld && !vesselTiltMode) BeginVesselTilt();
            UpdateVesselInputState(mouse.position.ReadValue(), mouse.leftButton.isPressed, rightHeld);
        }
        private void UpdateVesselInputState(Vector2 position, bool leftHeld, bool tiltHeld)
        {
            pointerScreen=position;
            if(!tiltHeld && !vesselTiltMode && !leftHeld)
            {
                if(vesselPointer==null) vesselPointer=new PointerEventData(EventSystem.current);
                vesselPointer.position=pointerScreen;
                EndDrag(vesselPointer); return;
            }
            if(vesselTiltMode && !tiltHeld)
            {
                StopVesselPour();
                ReleasePourTarget();
                vesselTiltMode=false;
                vesselTilt=0;
                draggedView.PresentPour(0,false);
                if(!leftHeld)
                {
                    if(draggingCupPickupPreview)
                    {
                        // A provisional cup has no domain id to return/move. Commit
                        // it through the normal source-drag transaction on release.
                        if(TryToWorld(pointerScreen,out var releasePoint))
                            offset=draggedView.transform.position-releasePoint;
                        vesselPointer.position=pointerScreen;
                        EndDrag(vesselPointer);
                        return;
                    }
                    var releasedId=dragItemId;
                    var releasedPosition=WorldToArea(World.State.currentArea,draggedView.DragPosition);
                    ClearDrag();
                    if(World.ShouldReturnVessel(World.Find(releasedId))) World.ReturnVesselHome(releasedId);
                    else if(World.IsCup(World.Find(releasedId))) World.Move(releasedId,World.State.currentArea,releasedPosition.x,releasedPosition.y);
                    SyncViews();
                    ShowHint(World.IsCup(World.Find(releasedId)) ? "杯子留在当前位置；按实际倒出量消耗液体" : "壶已归位；按实际倒出量消耗液体");
                    return;
                }
                // Tilt moves the cursor while the vessel is frozen. Re-anchor at the
                // visible pose so resuming left-drag cannot jump by that cursor travel.
                if(TryToWorld(pointerScreen,out var resumePoint)) offset=draggedView.transform.position-resumePoint;
                vesselPointer.position=pointerScreen;
                MoveDrag(vesselPointer);
            }
            if(vesselTiltMode)
            {
                // The active pour input owns tilt mode; the receiver remains locked.
                UpdateVesselPour();
            }
        }
        private void UpdateVesselPour()
        {
            bool placeOnly=Keyboard.current!=null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            Vector2 delta=pointerScreen-vesselTiltPointerOrigin;
            // Keep the original downward gesture and allow an equal opposite tilt.
            // Exponential response is independent of frame rate and cannot overshoot.
            float requestedTilt=Mathf.Clamp(delta.y/VesselTiltPixels,-1f,1f);
            float response=1f-Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime,.05f)/VesselTiltResponseSeconds);
            vesselTilt=Mathf.Lerp(vesselTilt,requestedTilt,response);
            float signedTilt=vesselTilt;
            float tilt=Mathf.Abs(signedTilt);
            // Visual easing must never continue transferring after the player returns
            // to neutral or reverses direction through the upright position.
            bool wantsPour=Mathf.Abs(requestedTilt)>=VesselPourThreshold && tilt>=VesselPourThreshold && requestedTilt*signedTilt>0;
            if(draggedView!=null) draggedView.PresentPour(signedTilt,wantsPour && !placeOnly);
            if(placeOnly || !wantsPour || overUI || Time.deltaTime<=0) { StopVesselPour(); return; }
            var source=World.Find(dragItemId);
            WorldEntity target=World.Find(lockedPourTargetId);
            if(target==null && receiver!=null) target=World.Find(receiver.EntityId);
            if(target!=null && !HasActiveView(target)) { StopVesselPour(); return; }
            string targetId=target==null ? null : target.id;
            if (WorkstationTutorial != null && !WorkstationTutorial.AllowPour(dragItemId, targetId)) { StopVesselPour(); return; }
            // Picking up and cancelling must not finish extraction or lose the pair.
            // Using the lower jug to pour is the point at which it actually detaches.
            if (World.IsPrecisionExtractor(source) && World.ExtractionFilter(source)!=null && World.LiquidVolume(source)>.001f)
            {
                var prepared=World.PrepareExtractionDrag(source.id);
                if(!prepared.success || !IsDragging) return;
            }
            if(!World.CanPour(source)) { StopVesselPour(); return; }
            if(pouredThisDrag && lastPourTarget!=targetId) StopVesselPour();
            if(!IsDragging) return;
            float amount = World.Catalog.production.pourRateMl*Mathf.Max(.1f,pourRateMultiplier)*tilt*Time.deltaTime;
            if (WorkstationTutorial != null) amount = WorkstationTutorial.LimitPourAmount(dragItemId, amount);
            if (amount <= 0) { StopVesselPour(); return; }
            // Snapshot the effective receiver before reactions/refills replace production states.
            var audioReceiver = FreeCafeSoundDynamics.PourReceiver(World, target);
            float receiverBefore = World.LiquidVolume(audioReceiver);
            float outflow = FreeCafeSoundDynamics.Outflow(World, source, amount);
            var result=World.PourLiquid(dragItemId,targetId,amount,Time.deltaTime);
            if(result.success)
            {
                pouredThisDrag=true; lastPourTarget=targetId;
                float received = Mathf.Max(0, World.LiquidVolume(audioReceiver) - receiverBefore);
                float spilled = Mathf.Max(0, outflow - received);
                float baseAmount = Mathf.Max(.0001f, World.Catalog.production.pourRateMl * Mathf.Max(.1f, pourRateMultiplier) * Time.deltaTime);
                FreeCafeSoundDynamics.Pour(outflow / baseAmount, FreeCafeSoundDynamics.Fill(World, audioReceiver), out float volume, out float pitch);
                GameAudio.SetLoop(this, "pour", AudioKeys.Pour, outflow > .0001f, volume, pitch);
                FreeCafeSoundDynamics.Pour(spilled / baseAmount, 0, out volume, out pitch);
                GameAudio.SetLoop(this, "spill", AudioKeys.Spill, spilled > .0001f, volume, pitch);
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
                GameAudio.SetLoop(this, "pour", AudioKeys.Pour, false);
                GameAudio.SetLoop(this, "spill", AudioKeys.Spill, false);
                if(lastPourError!=result.message) { lastPourError=result.message; OperationToastPanel.ShowMessage(result.message); }
            }
        }
        private bool HasActiveView(WorldEntity entity)
        {
            var view=entity==null ? null : View(entity.id);
            return view!=null && view.isActiveAndEnabled;
        }
        private WorldEntity ResolvePourTarget()
        {
            var target=receiver==null ? null : World.Find(receiver.EntityId);
            if(!HasActiveView(target)) target=null;
            if(target!=null && World.Spec(target.specId).Has(Capability.Delivery))
                return HasActiveView(World.ServingCup) ? World.ServingCup : target;
            if(target!=null) return target;
            // 杯子上方也允许倒入，但只在进入倾倒时判断。
            var source=World.Find(dragItemId);
            var point=WorldToArea(World.State.currentArea,draggedView.DragPosition);
            float closest=float.MaxValue;
            foreach(var entity in World.State.entities)
            {
                if(entity==source || entity.area!=World.State.currentArea || !World.IsContainer(entity) || !HasActiveView(entity)) continue;
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
            GameAudio.SetLoop(this, "pour", AudioKeys.Pour, false);
            GameAudio.SetLoop(this, "spill", AudioKeys.Spill, false);
            if(receivingView!=null) receivingView.SetReceivingPour(false);
            if(!pouredThisDrag) return;
            var targetId=lastPourTarget;
            pouredThisDrag=false; lastPourTarget=null;
            var result=World.FinishPour(targetId);
            ShowHint(result.message);
        }
        public void ResetWorkstation()
        {
            if (World == null || InputBlocked || World.State.currentArea != AreaId.Counter) return;
            if (PracticeTutorialActive || CafeTutorialActive)
            {
                ShowHint("请先完成或退出教程，再重置工作台");
                return;
            }
            CancelDrag();
            SelectedId = null;
            Report(World.ResetWorkstation());
        }

        public void ReturnFromProduction()
        {
            if(InputBlocked || WorkstationTutorial != null && WorkstationTutorial.IsFirstCafeTutorial) return;
            CancelDrag(); Report(World.LeaveProduction());
        }

        public void TogglePause() { CancelDrag(); Time.timeScale=Time.timeScale<=0 ? 1 : 0; ShowHint(Time.timeScale<=0 ? "已暂停 · 再次点击或 Esc 继续" : "继续自由操作"); }
        public string CaptureJson() => JsonUtility.ToJson(World.State);
        public void RestoreJson(string json)
        {
            try
            {
            CancelDrag();
            var gateway=GetComponent<FreeCafeMiniGameGateway>(); if(gateway!=null) gateway.Abort();
            var audioPresenter = GetComponent<FreeCafeAudioPresenter>();
            if (audioPresenter != null) audioPresenter.ResetObservation();
            var candidate=CafeSaveMigration.Prepare(JsonUtility.FromJson<CafeSnapshot>(json));
            // Supply stations and discard targets return to their scene-authored homes.
            // Restore their contents/stock without reapplying an old workstation layout.
            if(candidate?.entities!=null)
                foreach(var entity in candidate.entities)
                {
                    if(entity==null) continue;
                    var spec=World.Spec(entity.specId);
                    if(spec==null || !(spec.Has(Capability.Source) || spec.Has(Capability.Discard)
                        || World.IsInfiniteLiquidContainer(entity))) continue;
                    var initial=Array.Find(World.Catalog.layout, entry => entry.id==entity.id && entry.specId==entity.specId);
                    if(initial==null) continue;
                    entity.area=initial.area; entity.x=initial.x; entity.y=initial.y;
                }
            var result=World.Restore(candidate);
            if(!result.success) throw new FormatException(result.message); SelectedId=null; if(productionMode && !World.ProductionEnabled) World.EnableProduction();
            World.ConfigureEquipmentProgression(IsSandboxMode);
            pendingDailyCupRefresh = true;
            SyncViews();
            // 读档后镜头瞬间就位，不做区域切换的平滑平移
            areaManager.SnapNow();

                PlayerCafeOwner.ReportRestoreFailure(null);
            }
            catch(Exception exception)
            {
                PlayerCafeOwner.ReportRestoreFailure(exception.Message);
                throw;
            }
        }
        public void Save()
        {
            if (NarrativeAutoRunner.IsActive || NarrativeAutoRunner.IsWalkthroughActive)
            { ShowHint("自动测试期间禁止保存；停止测试后才能保存当前现场。"); return; }
            CancelDrag(); var data=new GameSaveData { SceneName="CoffeeShop",CheckpointId="freecafe",Participants=GameSaveParticipantRegistry.CaptureAll() };
            var saves=new GameSaveService();
            Report(saves.TryQuickSave(data,out var slot,out var error) ? ActionResult.Yes("已保存到空槽位 "+slot) : ActionResult.No("保存失败："+error));
        }
        public bool TryGetCafeTutorialStep(string tutorialId, string stepId, out CafeTutorialStepDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(tutorialId) || string.IsNullOrEmpty(stepId)) return false;
            if (tutorialConfiguration == null) tutorialConfiguration = ResolveNarrativeConfiguration();
            return tutorialConfiguration.TryGetCafeTutorialStep(tutorialId, stepId, out definition);
        }

        public void NotifyTutorialStepCompleted(string tutorialId, string stepId)
        {
            if (cafeTutorial == null || tutorialId != CafeTutorialSession.TutorialId) return;
            if (cafeTutorial.NotifyTutorialStepCompleted(tutorialId, stepId))
                CafeTutorialStepCompleted?.Invoke(tutorialId, stepId);
        }

        public bool BeginCafeTutorialCoffee()
        {
            if (!CafeTutorialActive || cafeTutorial == null || !cafeTutorial.HandoffReady) return false;
            pendingTutorialCoffee = true;
            return true;
        }

        public bool DebugStartFirstGuestTutorial(out string message)
        {
            message = "请直接运行 CoffeeShop（沙盒）后使用此操作。";
            if (!IsSandboxMode || World == null || !isActiveAndEnabled
                || gameObject.scene.name != "CoffeeShop"
                || UnityEngine.SceneManagement.SceneManager.GetActiveScene() != gameObject.scene
                || FindFirstObjectByType<GameFlowBootstrap>() != null) return false;
            if (dayFlow != null || FindFirstObjectByType<DayFlowController>() != null)
            {
                message = "本次沙盒已经存在日程/教程流程；请重新运行 CoffeeShop 后再试。";
                return false;
            }
            if (World.State.day != 1 || World.State.open || World.State.requests.Count != 0
                || World.Find(CafeWorld.TutorialCustomerId) != null)
            {
                message = "请在首日未营业、没有客人与订单的沙盒中启动首位客人教程。";
                return false;
            }
            if (GameplayBlocked || PracticeTutorialActive || IsDragging || pendingBusinessStart
                || playerHud != null && playerHud.IsModalOpen || productionHud != null && productionHud.IsModalOpen
                || equipmentPanel != null && equipmentPanel.IsOpen
                || equipmentTutorialPrompt != null && equipmentTutorialPrompt.IsOpen)
            {
                message = "请先结束当前教学、对话、小游戏或弹窗，并恢复游戏时间。";
                return false;
            }
            // Use the existing scene world and presentation owners; do not reset sandbox resources.
            var hud = GameplayHudPanel.Ensure();
            if (!hud.TryStartStandaloneCafeTutorial(gameObject, out message)) return false;
            dayFlow = GetComponent<DayFlowController>();
            World.SetArea(AreaId.Cafe);
            areaManager.SnapNow();
            GetComponent<FreeCafeAudioPresenter>()?.Bind(this);
            return true;
        }

        public void PrepareCafeTutorial()
        {
            if (dayFlow == null) dayFlow = FindFirstObjectByType<DayFlowController>();
            if (!CafeTutorialActive) return;
            World.ConfigureEquipmentProgression(false);
            RefreshDailyCups();
            cafeTutorial ??= new CafeTutorialSession();
            var result = World.StartTutorialCustomer();
            if (!result.success) { Report(result); return; }
            cafeTutorial.Begin();
            ShowHint("请点击大厅右侧吧台，为首位客人制作一杯黑咖啡；完成教程后才能正式营业。");
        }

        public void FinishCafeTutorial()
        {
            pendingTutorialCoffee = false;
            pendingBusinessStart = false;
            BusinessTutorial?.Stop();
            if (WorkstationTutorial != null && WorkstationTutorial.IsFirstCafeTutorial) WorkstationTutorial.Stop();
            World.EndTutorialCustomer();
            cafeTutorial = null;
            SyncViews();
        }

        /// <summary>Story business already closed today (Night). Sandbox keeps its reopenable sign.</summary>
        public bool IsClosedForToday => !IsSandboxMode && World != null && !World.State.open
            && World.State.businessPhase == BusinessPhase.Night;
        private void OpenMapFromClosedSign()
        {
            CancelDrag();
            if (gameFlow == null) gameFlow = FindFirstObjectByType<GameFlowBootstrap>();
            // OpenMap reports its own blocked reason (story, pause, tutorial...) as a toast.
            if (gameFlow != null) gameFlow.OpenMap();
        }
        public void StartBusiness()
        {
            if (World == null || InputBlocked || TutorialBusinessStartBlocked) return;
            if (World.State.open) RequestBusinessEnd();
            else RequestBusinessStart();
        }
        /// <summary>Explain a refused click (bed, HUD buttons) with the same message as other cafe input.</summary>
        public void ReportBlockedOperation() => NotifyBlockedOperation();
        private void NotifyBlockedOperation()
        {
            string message = CafeTutorialActive || PracticeTutorialActive
                ? "请先按照教程完成当前步骤，再进行其他操作。"
                : gameFlow != null && (gameFlow.IsMapOpen || gameFlow.IsDetailsOpen)
                    ? "请先关闭地图或详情面板，再操作咖啡馆。"
                    : IsDragging ? "请先放下手中的物品，再进行其他操作。"
                    : "当前剧情、面板或小游戏尚未结束，请完成或关闭后重试。";
            ShowHint(message);
            OperationToastPanel.ShowBlocked(message);
        }
        public void RequestBusinessStart()
        {
            if (GameplayBlocked || TutorialBusinessStartBlocked || PracticeTutorialActive) { NotifyBlockedOperation(); return; }
            string dayFlowReason = DayFlowBusinessOpeningBlockReason();
            if (dayFlowReason != null)
            {
                ShowHint(dayFlowReason);
                OperationToastPanel.ShowMessage(dayFlowReason);
                return;
            }
            pendingBusinessStart = true;
            TryStartBusiness();
        }
        private void TryStartBusiness()
        {
            if (World == null || World.State.open) { pendingBusinessStart = false; return; }
            if (GameplayBlocked || TutorialBusinessStartBlocked || PracticeTutorialActive) return;
            // A deferred start re-checks the phase: it may have changed while visits were pumped.
            if (DayFlowBusinessOpeningBlockReason() != null) { pendingBusinessStart = false; return; }
            if (!IsSandboxMode || NarrativeRuntime.Instance != null)
            {
                if (cafeVisits == null) cafeVisits = new CafeVisitBridge(this);
                var ncs = Spotlight.Narrative.NarrativeRuntime.Instance;
                if (ncs != null && (!cafeVisits.Pump() || !ncs.Controller.CanCaptureSnapshot || ncs.Controller.IsBusy || cafeVisits.HasStageHandoff)) return;
            }
            pendingBusinessStart = false;
            // Every entry route (opening page, resumed/rest visit) gets today's cup stock; idempotent per day.
            if (!IsSandboxMode && dayFlow != null) RefreshDailyCups();
            Report(World.StartDay(IsSandboxMode));
            if (!IsSandboxMode || NarrativeRuntime.Instance != null) cafeVisits?.Pump();
        }
        private string DayFlowBusinessOpeningBlockReason()
        {
            if (IsSandboxMode) return null;
            if (dayFlow == null) dayFlow = FindFirstObjectByType<DayFlowController>();
            return dayFlow != null ? dayFlow.BusinessOpeningBlockReason : null;
        }
        public string BusinessClosingBlockReason
        {
            get
            {
                if (World == null || !World.State.open) return "当前没有营业。";
                if (IsDragging) return "请先放下手中的物品，再结束营业。";
                if (CafeTutorialActive || PracticeTutorialActive) return "请先完成或退出当前操作教程，再结束营业。";
                if (gameFlow != null && gameFlow.IsMapOpen) return "请先关闭地图返回咖啡馆，再点击结束营业。";
                if (dayFlow == null && !IsSandboxMode) dayFlow = FindFirstObjectByType<DayFlowController>();
                if (dayFlow != null && !IsSandboxMode)
                {
                    string reason = dayFlow.BusinessClosingBlockReason;
                    if (!string.IsNullOrEmpty(reason)) return reason;
                }
                if (InputBlocked) return "请先关闭当前面板、结束对话或小游戏后，再结束营业。";
                if (!IsSandboxMode && dayFlow == null) return "营业日程尚未就绪，请稍后再试。";
                return null;
            }
        }
        public bool CanEndBusiness => BusinessClosingBlockReason == null;
        public void RequestBusinessEnd()
        {
            string reason = BusinessClosingBlockReason;
            if (reason != null)
            {
                ShowHint(reason);
                OperationToastPanel.ShowMessage(reason);
                return;
            }
            if (dayFlow == null) dayFlow = FindFirstObjectByType<DayFlowController>();
            if (!IsSandboxMode)
            {
                // Formal closing must go through settlement/AfterBusiness/Map, never
                // silently close the domain while its day-flow gates are still blocked.
                if (dayFlow != null) dayFlow.FinishBusiness();
                if (World.State.open)
                {
                    string blocked = dayFlow?.BusinessClosingBlockReason ?? "营业结算尚未就绪，请稍后再试。";
                    ShowHint(blocked);
                    OperationToastPanel.ShowMessage(blocked);
                }
                return;
            }
            if (dayFlow != null && dayFlow.CanFinishBusiness) dayFlow.FinishBusiness();
            else EndBusiness();
        }
        public void EndBusiness() { if(!InputBlocked) Report(World.EndDay()); }
        private void RefreshDailyCups()
        {
            int day = dayFlow != null ? dayFlow.CurrentDay : World.State.day;
            if (!IsSandboxMode && dayFlow != null) World.SynchronizeCalendarDay(day);
            World.RefreshDailyCupStock((IsSandboxMode ? "sandbox:" : "story:") + day);
        }
        public void StartDayFlowBusiness()
        {
            RefreshDailyCups();
            if (CafeTutorialActive)
            {
                PrepareCafeTutorial();
                return;
            }
            ShowHint("准备就绪，点击大厅的「开始营业」挂牌开始正式营业。");
        }
        public void EndDayFlowBusiness()
        {
            if (World != null && World.State.open)
                Report(World.EndDay());
        }
        public void RefillSources() { if(!InputBlocked) Report(World.Refill()); }
        public void RotateSelected() { if(!InputBlocked) World.Rotate(SelectedId); }
        public void DebugRefreshViews() { SyncViews(); ShowHint("经营视图已刷新"); }
        /// <summary>GM requests the authored NCS events, never creates a fake story customer.</summary>
        public ActionResult DebugArrangeCafeVisit(bool delayed = false)
        {
            if (World == null) return ActionResult.No("请先进入咖啡馆");
            if (CafeTutorialActive || PracticeTutorialActive) return ActionResult.No("请先完成或跳过咖啡馆教程");
            var ncs = NarrativeRuntime.Instance;
            if (ncs == null || !ncs.IsReady) return ActionResult.No("NCS 尚未就绪，请从启动场景进入正式游戏会话");
            if (ncs.Controller.IsBusy || !ncs.Controller.IsSafeBoundary) return ActionResult.No("剧情正在执行或结算，请稍后再试");
            foreach (var visit in ncs.GetCommittedCafeVisits().Visits)
                if (!visit.IsEnded && visit.CharacterId == "adele") return ActionResult.No("阿黛尔已有剧情来访，请查看来访状态或先取消");
            foreach (var entity in World.State.entities)
                if (entity.person?.characterId == "adele") return ActionResult.No("店内已有阿黛尔，请勿先使用普通强制召唤");
            string reserve = delayed ? "EV_DEMO_CAFE_DELAYED_RESERVE" : "EV_DEMO_CAFE_RESERVE";
            string arrive = "EV_DEMO_CAFE_ARRIVE";
            var reservation = ncs.Controller.ExplainEvent(reserve);
            if (!reservation.CanRequest) return ActionResult.No(reservation.ToString());
            var arrival = ncs.Controller.ExplainEvent(arrive);
            if (!arrival.CanRequest) return ActionResult.No(arrival.ToString());
            if (!ncs.QueueEvent(reserve)) return ActionResult.No("预约事件被拒绝：" + reserve);
            ncs.Controller.RunAtSafeBoundary(() =>
            {
                if (NarrativeRuntime.Instance != ncs) return;
                if (!ncs.QueueEvent(arrive)) { ShowHint("到场事件被拒绝：" + ncs.Controller.ExplainEvent(arrive)); return; }
                cafeVisits ??= new CafeVisitBridge(this);
                cafeVisits.Pump();
            });
            return ActionResult.Yes("已通过 NCS 安排阿黛尔" + (delayed ? "延迟来访（当次营业累计 30 秒）" : "剧情来访")
                + (World.State.open ? "；满座时等位，不挤掉客人。" : "；点击开始营业后到店。"));
        }

        public ActionResult DebugSummonCustomer(string characterId, int seatIndex = -1,
            bool allowDuplicate = true, bool replaceExisting = false)
        {
            if (World == null) return ActionResult.No("当前场景未加载经营系统");
            if (CafeTutorialActive || PracticeTutorialActive) return ActionResult.No("请先完成或跳过咖啡馆教程");
            if (characterId == "supplied_seated_guest" && seating != null && seating.suppliedCustomer != null)
                return World.DebugSummonCustomer(new CafePersonIdentity
                {
                    personId = characterId, displayName = seating.suppliedGuestName,
                    visualId = "supplied.seated.guest", role = CafePersonRole.Customer
                }, seatIndex, allowDuplicate, replaceExisting);
            var character = Character(characterId);
            if (character == null) return ActionResult.No("找不到角色：" + characterId);
            if (character.worldSprite == null && (seating == null || !seating.CanPresentCustomer(characterId)))
                return ActionResult.No(character.displayName + " 尚未配置店内素材或默认黑影");
            return World.DebugSummonCustomer(new CafePersonIdentity
            {
                personId = character.characterId, characterId = character.characterId,
                narrativeCharacterId = character.characterId, displayName = character.displayName,
                visualId = "customer", role = CafePersonRole.Customer
            }, seatIndex, allowDuplicate, replaceExisting);
        }
    }
}
