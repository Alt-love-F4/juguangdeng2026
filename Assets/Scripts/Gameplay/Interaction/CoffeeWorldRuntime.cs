using System;
using System.Linq;
using Spotlight.GameFlow;
using Spotlight.Interaction;
using TMPro;
using UnityEngine;

namespace Spotlight.Gameplay
{
    /// <summary>Cafe rules adapter. Generic entities and pointer code never depend on recipes or NPC story logic.</summary>
    public sealed class CoffeeWorldRuntime : InteractionWorld, IInteractionRules
    {
        public CarryableEntity[] itemPrefabs;
        public Sprite[] npcSprites;
        public Sprite finishedCupSprite;
        [Tooltip("动态生成的杯子与原料统一放在出餐台的可移动物体根节点。未绑定时回退到本对象。")]
        public Transform carryableRoot;
        public string Hint { get; private set; } = "开始营业后点击顾客接单；从杯子堆拖出杯子到工作台。";
        protected override IInteractionRules Rules => this;
        private CoffeeGameplayController ctrl;
        public CoffeeLocationController locationController;
        private bool CanUseCounter()
        {
            if (locationController == null || locationController.CurrentLocation == CoffeeLocationId.FirstFloorServingCounter) return true;
            ShowHint("请先进入一楼右侧的出餐台"); return false;
        }
        private const string SaveId = "world.placements";
        public override bool InputBlocked => base.InputBlocked || CoffeeGameplayPanel.IsMinigameOpen;
        public ItemState ActiveCup => ctrl?.CurrentOrder == null ? null : Ledger.Items.FirstOrDefault(i => i.bindingId == ctrl.CurrentOrder.OrderId.ToString() && Ledger.Definition(i.definitionId).HasTag("cup"));
        private void Awake()
        {
            ctrl = CoffeeGameplayController.EnsureExists(); Ledger = new PlacementLedger(GameplayConfigService.AllEntities);
            foreach (var entity in GetComponentsInChildren<InteractiveEntity>(true))
            {
                var definition = Ledger.Definition(entity.definitionId);
                if (definition == null) throw new InvalidOperationException("实体配置不存在：" + entity.definitionId);
                entity.Initialize(definition, this);
            }
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson; GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
        }
        private void Start()
        {
            var saved = GameSaveParticipantRegistry.TakePending(SaveId);
            if (!string.IsNullOrEmpty(saved)) RestoreJson(saved);
            Reconcile();
        }
        private void OnDestroy()
        {
            GameSaveParticipantRegistry.Capturers.Remove(SaveId); GameSaveParticipantRegistry.Restorers.Remove(SaveId);
        }
        public override void ShowHint(string message) { if (!string.IsNullOrEmpty(message)) Hint = message; }
        public string CaptureJson() => JsonUtility.ToJson(Ledger.Capture());
        public void RestoreJson(string json)
        {
            CancelDrag(); var result = Ledger.Restore(JsonUtility.FromJson<PlacementSnapshot>(json));
            if (!result.Success) throw new FormatException(result.Message);
            Reconcile();
        }
        protected override CarryableEntity CreateView(ItemState item)
        {
            var prefab = itemPrefabs.FirstOrDefault(p => p != null && p.definitionId == item.definitionId);
            if (prefab == null) { ShowHint("缺少物品预制体：" + item.definitionId); return null; }
            var view = Instantiate(prefab, carryableRoot != null ? carryableRoot : transform); view.name = string.IsNullOrEmpty(item.instanceId) ? "DragPreview" : item.instanceId;
            view.Bind(item); view.Initialize(Ledger.Definition(item.definitionId), this); view.Renderer.sortingOrder = 8; return view;
        }
        public InteractionResult CanTake(InteractiveEntity source) => CanTake(source.Definition, source.Item);
        public InteractionResult CanTake(EntityDefinition source, ItemState carried)
        {
            if (InputBlocked) return InteractionResult.Reject("当前交互已暂停");
            if (carried != null && carried == ActiveCup && ctrl.IsProcessing) return InteractionResult.Reject("杯子正在加工，完成后才能拿走");
            if (source.role == "source")
            {
                var item = Ledger.Definition(source.item_id);
                if (item == null) return InteractionResult.Reject("来源没有配置物品");
                if (item.HasTag("ingredient"))
                {
                    var ingredient = GameplayConfigService.GetIngredient(item.behavior_id);
                    if (ingredient == null || ctrl.Profile.Reputation < ingredient.unlock_rep) return InteractionResult.Reject("原料尚未解锁");
                    if (ctrl.Profile.GetStock(ingredient.id) <= 0) return InteractionResult.Reject("库存不足，请在库存面板补货");
                }
            }
            return InteractionResult.Accept();
        }
        public InteractionResult Preview(ItemState item, InteractiveEntity target, bool provisional) => target == null ? InteractionResult.Reject("没有目标") : Preview(item, new InteractionTarget(target.Definition, target.Item), provisional);
        public InteractionResult Preview(ItemState item, InteractionTarget target, bool provisional)
        {
            if (InputBlocked || item == null || target.Definition == null) return InteractionResult.Reject("当前无法放置");
            var definition = Ledger.Definition(item.definitionId);
            if (definition == null) return InteractionResult.Reject("未知物品");
            if (item == ActiveCup && ctrl.IsProcessing) return InteractionResult.Reject("杯子正在加工");
            if (target.Item != null)
            {
                var cup = target;
                if (!definition.HasTag("ingredient") || !cup.Definition.HasTag("cup")) return InteractionResult.Reject("这里只接受原料");
                if (cup.Item != ActiveCup) return InteractionResult.Reject("请向当前订单绑定的杯子加料");
                if (cup.Item.contents.Count + item.amount > cup.Definition.capacity) return InteractionResult.Reject("杯子容量不足");
                return ctrl.Session?.CanAddIngredient(definition.behavior_id, !provisional) ?? InteractionResult.Reject("请先接单");
            }
            var placement = Ledger.CanPlace(item, target.Definition.id);
            if (!placement.Success) return placement;
            if (target.Definition.behavior_id == "serve")
            {
                if (item != ActiveCup || ctrl.Session?.CurrentStep?.Kind != RecipeStepKind.Serve) return InteractionResult.Reject("饮品未完成或不是当前订单");
            }
            else if (target.Definition.role == "machine")
            {
                if (item != ActiveCup || ctrl.Session?.CurrentStep?.DeviceId != target.Definition.behavior_id) return InteractionResult.Reject("当前步骤不使用这台设备");
            }
            else if (definition.HasTag("cup") && ctrl.Session?.CurrentStep?.Kind == RecipeStepKind.SelectCup)
            {
                if (item.contents.Count > 0) return InteractionResult.Reject("请选择一个空杯");
                return ctrl.Session.CanSelectCup(definition.behavior_id);
            }
            if (provisional && definition.HasTag("ingredient"))
            {
                var entry = GameplayConfigService.GetIngredient(definition.behavior_id);
                if (entry == null || ctrl.Profile.Reputation < entry.unlock_rep || ctrl.Profile.GetStock(entry.id) < item.amount) return InteractionResult.Reject("原料库存不足或未解锁");
            }
            return InteractionResult.Accept();
        }
        public InteractionResult Commit(ItemState item, InteractiveEntity target, bool provisional) => target == null ? InteractionResult.Reject("没有目标") : Commit(item, new InteractionTarget(target.Definition, target.Item), provisional);
        public InteractionResult Commit(ItemState item, InteractionTarget target, bool provisional)
        {
            var permission = Preview(item, target, provisional); if (!permission.Success) return permission;
            var definition = Ledger.Definition(item.definitionId);
            if (target.Item != null)
            {
                var cup = target;
                var result = ctrl.TryAddIngredient(definition.behavior_id, !provisional);
                if (!result.Success) return result;
                cup.Item.contents.Add(definition.behavior_id);
                if (!provisional) Ledger.Remove(item.instanceId);
                return result;
            }
            if (target.Definition.behavior_id == "serve")
            {
                ctrl.CompleteServe(); Ledger.Remove(item.instanceId); return InteractionResult.Accept("饮品已交给顾客");
            }
            bool selectCup = definition.HasTag("cup") && ctrl.Session?.CurrentStep?.Kind == RecipeStepKind.SelectCup;
            var placed = Ledger.Place(item, target.Definition.id); if (!placed.Success) return placed;
            if (provisional && definition.HasTag("ingredient")) ctrl.Profile.Consume(definition.behavior_id, item.amount);
            if (selectCup) { item.bindingId = ctrl.CurrentOrder.OrderId.ToString(); ctrl.TrySelectCup(definition.behavior_id); }
            if (target.Definition.role == "machine") return ctrl.TryUseDevice(target.Definition.behavior_id);
            ctrl.NotifyState(); return InteractionResult.Accept("已放置到：" + target.Definition.name);
        }
        void IInteractionRules.Activate(InteractionTarget entity)
        {
            if (entity.Definition.role == "npc") { ctrl.TalkCustomer(); return; }
            if (entity.Definition.role == "source")
            {
                var definition = Ledger.Definition(entity.Definition.item_id);
                if (definition.HasTag("cup")) SelectCupFromSource(GetComponentsInChildren<ItemSourceEntity>().First(e => e.definitionId == entity.Definition.id)); else AddFromButton(definition.behavior_id);
            }
            else if (entity.Definition.role == "slot" || entity.Definition.role == "machine")
            {
                if (entity.Definition.behavior_id == "serve") ServeFromButton();
                else if (entity.Definition.role == "machine") UseDeviceFromButton(entity.Definition.behavior_id);
                else if (ActiveCup != null) Apply(ActiveCup, entity, false);
            }
            else if (entity.Definition.role == "item")
            {
                if (entity.Item == ActiveCup) PerformCurrentStep();
                else if (Ledger.Definition(entity.Item.definitionId).HasTag("ingredient") && ActiveCup != null && views.TryGetValue(ActiveCup.instanceId, out var cup)) Apply(entity.Item, cup, false);
                else if (ctrl.Session?.CurrentStep?.Kind == RecipeStepKind.SelectCup) Apply(entity.Item, entityForLocation(entity.Item.locationId), false);
            }
        }
        private InteractiveEntity entityForLocation(string id) => GetComponentsInChildren<PlacementEntity>().FirstOrDefault(e => e.definitionId == id);
        private void Apply(ItemState item, InteractionTarget target, bool provisional)
        { var result = Commit(item, target, provisional); Reconcile(); ShowHint(result.Message); }
        private void Apply(ItemState item, InteractiveEntity target, bool provisional)
        {
            var result = Commit(item, target, provisional); Reconcile(); ShowHint(result.Message);
        }
        private void SelectCupFromSource(InteractiveEntity source)
        {
            var permission = CanTake(source); if (!permission.Success) { ShowHint(permission.Message); return; }
            var slot = GetComponentsInChildren<PlacementEntity>().FirstOrDefault(e => e.Definition.role == "slot" && e.Definition.behavior_id == "workbench" && Ledger.At(e.definitionId) == null);
            if (slot == null) { ShowHint("工作台已满，请移动或丢弃物品"); return; }
            Apply(new ItemState { definitionId = source.Definition.item_id }, slot, true);
        }
        public void SelectCupFromButton()
        {
            if (!CanUseCounter() || InputBlocked) return;
            var size = ctrl.CurrentOrder?.Recipe?.cup_size; if (size == null) { ShowHint("请先接单"); return; }
            var existing = Ledger.Items.FirstOrDefault(i => Ledger.Definition(i.definitionId).HasTag("cup") && Ledger.Definition(i.definitionId).behavior_id == size && i.contents.Count == 0 && string.IsNullOrEmpty(i.bindingId));
            if (existing != null) { Apply(existing, entityForLocation(existing.locationId), false); return; }
            var source = GetComponentsInChildren<ItemSourceEntity>().FirstOrDefault(e => Ledger.Definition(e.Definition.item_id).HasTag("cup") && Ledger.Definition(e.Definition.item_id).behavior_id == size);
            if (source != null) SelectCupFromSource(source);
        }
        public void AddFromButton(string id)
        {
            if (!CanUseCounter() || InputBlocked) return;
            if (ActiveCup == null || !views.TryGetValue(ActiveCup.instanceId, out var cup)) { ShowHint("请先取出并放置当前订单的杯子"); return; }
            var source = GetComponentsInChildren<ItemSourceEntity>().FirstOrDefault(e => Ledger.Definition(e.Definition.item_id).behavior_id == id && Ledger.Definition(e.Definition.item_id).HasTag("ingredient"));
            if (source == null) return;
            var permission = CanTake(source); if (!permission.Success) { ShowHint(permission.Message); return; }
            Apply(new ItemState { definitionId = source.Definition.item_id }, cup, true);
        }
        public void UseCurrentDeviceFromButton() { var id = ctrl.Session?.CurrentStep?.DeviceId; if (id != null) UseDeviceFromButton(id); }
        public void UseDeviceFromButton(string id)
        {
            if (!CanUseCounter() || InputBlocked) return;
            if (ActiveCup == null) { ShowHint("没有当前订单杯子"); return; }
            var target = GetComponentsInChildren<PlacementEntity>().FirstOrDefault(e => e.Definition.role == "machine" && e.Definition.behavior_id == id);
            if (target != null) Apply(ActiveCup, target, false);
        }
        public void ServeFromButton()
        {
            if (!CanUseCounter() || InputBlocked) return;
            if (ActiveCup == null) { ShowHint("没有可上餐的饮品"); return; }
            var target = GetComponentsInChildren<PlacementEntity>().FirstOrDefault(e => e.Definition.behavior_id == "serve");
            if (target != null) Apply(ActiveCup, target, false);
        }
        public void PerformCurrentStep()
        {
            if (!CanUseCounter() || InputBlocked) return;
            if (ctrl.Session?.CurrentStep?.Kind is RecipeStepKind.BeanSelection or RecipeStepKind.LatteArt) FindFirstObjectByType<CoffeeGameplayPanel>()?.OpenStepMinigame();
            else if (ctrl.Session?.CurrentStep?.Kind == RecipeStepKind.Serve) ServeFromButton();
            else UseCurrentDeviceFromButton();
        }
        public void ClearLooseItems()
        {
            if (!CanUseCounter() || InputBlocked) return;
            CancelDrag();
            foreach (var item in Ledger.Items.ToArray())
            {
                if (item == ActiveCup) continue;
                var definition = Ledger.Definition(item.definitionId);
                if (definition.HasTag("ingredient")) ctrl.Profile.Refill(definition.behavior_id, item.amount);
                Ledger.Remove(item.instanceId);
            }
            Reconcile(); ctrl.NotifyState(); ShowHint("空杯与未使用原料已收回");
        }
        private void LateUpdate()
        {
            if (Ledger == null || ctrl == null) return;
            foreach (var item in Ledger.Items.ToArray())
                if (!string.IsNullOrEmpty(item.bindingId) && !ctrl.Orders.Orders.Any(o => o.OrderId.ToString() == item.bindingId && o.State == OrderState.InProgress)) Ledger.Remove(item.instanceId);
            Reconcile();
            foreach (var item in Ledger.Items)
                if (views.TryGetValue(item.instanceId, out var view) && Ledger.Definition(item.definitionId).HasTag("cup"))
                {
                    var prefab = itemPrefabs.FirstOrDefault(p => p.definitionId == item.definitionId);
                    view.Renderer.sprite = item.contents.Count > 0 && finishedCupSprite != null ? finishedCupSprite : prefab.Renderer != null ? prefab.Renderer.sprite : prefab.GetComponent<SpriteRenderer>().sprite;
                }
            var visitor = ctrl.CurrentOrder ?? ctrl.Orders.NextPending;
            foreach (var npc in GetComponentsInChildren<NpcEntity>(true))
            {
                bool visible = visitor != null && ctrl.Day.Phase == DayPhase.Running;
                npc.gameObject.SetActive(visible);
                if (!visible) continue;
                var data = GameplayConfigService.GetNpc(visitor.NpcId);
                var sprite = npcSprites.FirstOrDefault(s => s != null && data != null && s.name == System.IO.Path.GetFileNameWithoutExtension(data.sprite_path));
                if (sprite != null) npc.Renderer.sprite = sprite;
                var text = npc.GetComponentInChildren<TMP_Text>(); if (text != null) text.text = visitor.CustomerName + (ctrl.CurrentOrder == null ? " · 点击交流 / 接单" : " · " + visitor.Recipe.comment);
            }
        }
    }
}
