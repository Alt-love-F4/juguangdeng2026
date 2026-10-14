using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// 咖啡店世界的权威模拟层：所有空间指令只依赖数据快照，
    /// 不依赖当前聚焦的订单，也不依赖场景中的任何 GameObject。
    /// </summary>
    public sealed partial class CafeWorld
    {
        // 目录静态数据索引：构造时一次性校验并缓存，避免运行期反复遍历数组
        private readonly Dictionary<string, EntitySpec> specs = new Dictionary<string, EntitySpec>();
        private readonly Dictionary<string, OperationSpec> operations = new Dictionary<string, OperationSpec>();
        private readonly Dictionary<string, ProductSpec> products = new Dictionary<string, ProductSpec>();
        private readonly Dictionary<string, RecipeSpec> recipes = new Dictionary<string, RecipeSpec>();
        private readonly ICafeCustomerIdentitySource customerIdentities;

        /// <summary>完整配置目录：实体、工序、餐品、配方、布局、区域与经济参数。</summary>
        public CafeCatalog Catalog { get; }

        /// <summary>当前世界快照；存档与读档直接序列化这个对象。</summary>
        public CafeSnapshot State { get; private set; }

        /// <summary>世界状态每次变化后触发，视图层据此刷新。</summary>
        public event Action Changed;

        public CafeWorld(CafeCatalog catalog, ICafeCustomerIdentitySource customerIdentities = null)
        {
            this.customerIdentities = customerIdentities;
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ValidateCatalog();

            // 初始化快照：启动资金、当天剩余时间、顾客到店倒计时、初始卡座数
            State = new CafeSnapshot
            {
                money = catalog.startingMoney,
                timeRemaining = catalog.daySeconds,
                arrivalRemaining = catalog.customerInterval,
                seats = catalog.economy.initialSeats
            };

            // 按配置摆放初始布局；id 重复或位置越界都视为配置错误
            foreach (var initial in catalog.layout)
            {
                var spec = Spec(initial.specId);
                if (Find(initial.id) != null || !CanPosition(initial.specId, initial.area, initial.x, initial.y).success)
                {
                    throw new ArgumentException("Invalid layout " + initial.id);
                }

                State.entities.Add(new WorldEntity
                {
                    id = initial.id,
                    specId = initial.specId,
                    area = initial.area,
                    x = initial.x,
                    y = initial.y,
                    stock = spec.initialStock
                });
            }

            UnlockContent();
        }

        /// <summary>构造时校验目录数据，让配置错误在启动时就暴露，而不是运行到一半才炸。</summary>
        private void ValidateCatalog()
        {
            ValidateProductionCatalog();
            if (Catalog.entities == null || Catalog.operations == null || Catalog.products == null
                || Catalog.layout == null || Catalog.areas == null || Catalog.economy == null)
            {
                throw new ArgumentException("Catalog structure missing");
            }

            if (!Finite(Catalog.daySeconds) || Catalog.daySeconds <= 0 || Catalog.customerPatience <= 0
                || Catalog.customerInterval <= 0 || Catalog.startingMoney < 0)
            {
                throw new ArgumentException("Invalid business timing");
            }

            // 实体规格：尺寸、库存、成本都必须有效，id 不能重复
            foreach (var spec in Catalog.entities)
            {
                if (spec == null || string.IsNullOrEmpty(spec.id) || spec.width <= 0 || spec.height <= 0
                    || !Finite(spec.width) || !Finite(spec.height) || spec.capacity < 0
                    || spec.initialStock < 0 || spec.refillCost < 0 || spec.unitCost < 0)
                {
                    throw new ArgumentException("Invalid entity specification");
                }

                // 只有加工设备才允许配置「拖动设备取出产物」
                if (spec.dragOutput && !spec.Has(Capability.Processor))
                {
                    throw new ArgumentException("Only processors can expose drag output");
                }

                specs.Add(spec.id, spec);
            }

            ValidateCompositionCatalog();
            // 来源类实体必须能对应到一个可取出的物品规格
            foreach (var spec in Catalog.entities)
            {
                if (spec.Has(Capability.Source) && Spec(spec.sourceItemId) == null)
                {
                    throw new ArgumentException("Invalid source " + spec.id);
                }
            }

            // 工序：机器必须是加工设备，输入/输出规格必须存在，且同一机器+输入只能有一条工序
            foreach (var op in Catalog.operations)
            {
                if (op == null || Spec(op.machineSpecId) == null || !Spec(op.machineSpecId).Has(Capability.Processor)
                    || Spec(op.inputSpecId) == null || Spec(op.outputSpecId) == null
                    || !Finite(op.seconds) || op.seconds <= 0 || op.clicks <= 0 || op.clickInterval < 0)
                {
                    throw new ArgumentException("Invalid operation");
                }

                foreach (var existing in operations.Values)
                {
                    if (existing.machineSpecId == op.machineSpecId && existing.inputSpecId == op.inputSpecId)
                    {
                        throw new ArgumentException("Ambiguous operation " + op.id);
                    }
                }

                operations.Add(op.id, op);
            }

            // 餐品：杯型必须存在，配方原料不能重复，总量不能超过杯子容量
            foreach (var product in Catalog.products)
            {
                if (product == null || Spec(product.cupSpecId) == null || product.price < 0
                    || product.ingredients == null || product.ingredients.Length == 0)
                {
                    throw new ArgumentException("Invalid product");
                }

                int units = 0;
                var ingredients = new HashSet<string>();
                foreach (var p in product.ingredients)
                {
                    if (p == null || Spec(p.specId) == null || p.units <= 0 || !ingredients.Add(p.specId))
                    {
                        throw new ArgumentException("Invalid recipe " + product.id);
                    }

                    units += p.units;
                }

                if (units > Spec(product.cupSpecId).capacity)
                {
                    throw new ArgumentException("Recipe exceeds cup capacity " + product.id);
                }

                products.Add(product.id, product);
            }

            // 制作流程（可选）：id 唯一，步骤必须带类型和文案
            if (Catalog.recipes != null)
            {
                foreach (var recipe in Catalog.recipes)
                {
                    if (recipe == null || string.IsNullOrEmpty(recipe.id) || Product(recipe.productId) == null
                        || recipe.steps == null || recipe.steps.Length == 0)
                    {
                        throw new ArgumentException("Invalid recipe steps");
                    }

                    if (recipes.ContainsKey(recipe.id))
                    {
                        throw new ArgumentException("Duplicate recipe " + recipe.id);
                    }

                    foreach (var step in recipe.steps)
                    {
                        if (step == null || string.IsNullOrEmpty(step.kind) || string.IsNullOrEmpty(step.label))
                        {
                            throw new ArgumentException("Invalid recipe step " + recipe.id);
                        }
                    }

                    recipes.Add(recipe.id, recipe);
                }
            }

            // 区域 id 不能重复，尺寸必须为正
            var areas = new HashSet<AreaId>();
            foreach (var area in Catalog.areas)
            {
                if (area == null || !areas.Add(area.id) || area.halfWidth <= 0 || area.halfHeight <= 0)
                {
                    throw new ArgumentException("Invalid area");
                }
            }

            // 经济参数目前固定三档咖啡豆单价
            if (Catalog.economy.beanUnitPrices == null || Catalog.economy.beanUnitPrices.Length != 3)
            {
                throw new ArgumentException("Three bean price tiers required");
            }
        }

        /// <summary>按 id 查询实体规格，找不到返回 null。</summary>
        public EntitySpec Spec(string id) => id != null && specs.TryGetValue(id, out var spec) ? spec : null;

        /// <summary>按 id 查询餐品规格，找不到返回 null。</summary>
        public ProductSpec Product(string id) => id != null && products.TryGetValue(id, out var product) ? product : null;

        /// <summary>按 id 查询制作流程，找不到返回 null。</summary>
        public RecipeSpec Recipe(string id) => id != null && recipes.TryGetValue(id, out var recipe) ? recipe : null;

        /// <summary>取餐品对应的制作流程；未显式指定 recipeId 时退回用餐品 id 查找。</summary>
        public RecipeSpec RecipeFor(ProductSpec product)
        {
            if (product == null)
            {
                return null;
            }

            return Recipe(string.IsNullOrEmpty(product.recipeId) ? product.id : product.recipeId);
        }

        /// <summary>按 id 查找世界中的实体（设备、杯子、原料、顾客等）。</summary>
        public WorldEntity Find(string id)
        {
            if (id == null) return null;
            foreach (var entity in State.entities) if (entity.id == id) return entity;
            return null;
        }

        /// <summary>按 id 查找订单。</summary>
        public CustomerRequest Request(string id) => id == null ? null : State.requests.Find(r => r.id == id);

        /// <summary>按 id 查找区域配置，找不到返回 null。</summary>
        public AreaSpec Area(AreaId id)
        {
            foreach (var area in Catalog.areas)
            {
                if (area.id == id)
                {
                    return area;
                }
            }

            return null;
        }

        /// <summary>统计容器/物品内容物的总份数。</summary>
        public int TotalUnits(WorldEntity entity)
        {
            int sum = 0;
            if (entity?.contents != null)
            {
                foreach (var p in entity.contents)
                {
                    sum += p.units;
                }
            }

            return sum;
        }

        /// <summary>判断某个规格能否放在指定区域的 (x,y)：要扣除自身尺寸的一半，避免越出场景边界。</summary>
        public ActionResult CanPosition(string specId, AreaId area, float x, float y)
        {
            var bounds = Area(area);
            var spec = Spec(specId);
            if (bounds == null || spec == null || !Finite(x) || !Finite(y))
            {
                return ActionResult.No("无效位置");
            }

            if (Math.Abs(x) > bounds.halfWidth - spec.width * .5f || Math.Abs(y) > bounds.halfHeight - spec.height * .5f)
            {
                return ActionResult.No("请放在当前场景的可用范围内");
            }

            return ActionResult.Yes("放在这里");
        }

        /// <summary>是否被设备占用（ownerId 非空表示材料正锁在设备里，不能搬动）。</summary>
        public bool Locked(WorldEntity item) => item != null && (!string.IsNullOrEmpty(item.ownerId) || MiniGameUses(item));

        /// <summary>从来源（豆仓、牛奶柜等）取出一件物品放到指定位置。</summary>
        public ActionResult Take(string sourceId, AreaId area, float x, float y, out WorldEntity item)
        {
            item = null;
            var source = Find(sourceId);
            var spec = source == null ? null : Spec(source.specId);
            if (spec == null || !spec.Has(Capability.Source))
            {
                return ActionResult.No("不是物品来源");
            }

            if (source.area != area)
            {
                return ActionResult.No("请到物品所在区域取出");
            }

            if (source.stock <= 0)
            {
                return ActionResult.No("库存不足；点击补货或从经营菜单购买原料");
            }

            // 先验证落点，避免取出后再回滚
            var valid = CanPosition(spec.sourceItemId, area, x, y);
            if (!valid.success)
            {
                return valid;
            }

            // 取出的物品继承来源的品质（咖啡豆按当前品质计价）
            item = new WorldEntity
            {
                id = Allocate("free_"),
                specId = spec.sourceItemId,
                area = area,
                x = x,
                y = y,
                quality = source.quality
            };
            source.stock--;
            State.entities.Add(item);
            Notify();
            return ActionResult.Yes("取出：" + Spec(item.specId).label);
        }

        /// <summary>自由移动物品；固定设备和来源不可搬动，锁在设备里的材料也不行。</summary>
        public ActionResult Move(string itemId, AreaId area, float x, float y)
        {
            var item = Find(itemId);
            if (item == null || !Spec(item.specId).Has(Capability.Movable))
            {
                return ActionResult.No("固定设备或来源不能搬动");
            }

            if (CupAbove(item) != null) return ActionResult.No("请先取走最上面的杯子");
            bool removeFromTray = ProductionEnabled && item==ServingCup;
            if (Locked(item))
            {
                return ActionResult.No("材料留在设备内；请接取产物或取消加工");
            }

            var valid = CanPosition(item.specId, area, x, y);
            if (!valid.success)
            {
                return valid;
            }

            var assemblyJug = ProductionEnabled && IsExtractionFilter(item) ? ExtractionJug(item) : null;
            if (assemblyJug != null)
            {
                if (Locked(assemblyJug) || MiniGameUses(assemblyJug)) return ActionResult.No("下壶正在使用");
                var companionPosition = CanPosition(assemblyJug.specId, area, x - Prep.extractionFilterOffsetX, y - Prep.extractionFilterOffsetY);
                if (!companionPosition.success) return companionPosition;
            }
            var upper = ProductionEnabled ? ExtractionFilter(item) : null;
            if (upper != null && (Locked(upper) || MiniGameUses(upper))) return ActionResult.No("上层过滤器正在使用");
            item.cupStackBelowId = null;
            if (ProductionEnabled)
            {
                SuspendContainerHeating(item.id);
                if (upper != null) DetachExtractionFilter(upper);
            }
            item.area = area;
            item.x = x;
            item.y = y;
            if (assemblyJug != null)
            {
                SuspendContainerHeating(assemblyJug.id);
                assemblyJug.area = area; assemblyJug.x = x - Prep.extractionFilterOffsetX; assemblyJug.y = y - Prep.extractionFilterOffsetY;
                PositionExtractionFilter(assemblyJug);
            }
            if(removeFromTray) State.production.activeCupId=null;
            Notify();
            return ActionResult.Yes("已自由放置");
        }

        /// <summary>让可移动物品旋转 15 度，仅影响表现。</summary>
        public void Rotate(string id)
        {
            var item = Find(id);
            if (item != null && Spec(item.specId).Has(Capability.Movable) && !Locked(item))
            {
                item.rotation = (item.rotation + 15) % 360;
                Notify();
            }
        }

        /// <summary>聚焦某个订单，用于 UI 展示与自动交付判定。</summary>
        public void Focus(string id)
        {
            if (Request(id) != null)
            {
                State.focusedRequestId = id;
                Notify();
            }
        }

        /// <summary>切换当前所在区域。</summary>
        public void SetArea(AreaId area)
        {
            if (Area(area) != null)
            {
                State.currentArea = area;
                Notify();
            }
        }

        /// <summary>目标能否接收物品：杯具来源、容器、设备、顾客、出餐口或丢弃区。</summary>
        public bool IsReceiver(WorldEntity target) => target != null
            && (IsCupSource(Spec(target.specId)) || (Spec(target.specId).capabilities
                & (Capability.Container | Capability.Processor | Capability.Customer | Capability.Delivery | Capability.Discard)) != 0);

        /// <summary>预演一次「把 item 放到 target 上」的结果；只用于提示，不产生任何副作用。</summary>
        public ActionResult Preview(string itemId, string targetId) => Preview(Find(itemId), Find(targetId));

        public ActionResult Preview(WorldEntity item, WorldEntity target)
        {
            if (item == null || target == null || item == target || item.area != target.area)
            {
                return ActionResult.No("目标不在同一区域");
            }

            if (Locked(item) || Locked(target))
            {
                return ActionResult.No("材料正在设备内");
            }

            var input = Spec(item.specId);
            var receiver = Spec(target.specId);
            if (input == null || receiver == null)
            {
                return ActionResult.No("无效物品");
            }

            if (CupAbove(item) != null) return ActionResult.No("请先取走最上面的杯子");
            if (IsCupSource(receiver)) return PreviewCupReturn(item, target);
            if (IsCup(item) && IsCup(target) && item.contents.Count == 0 && target.contents.Count == 0)
                return PreviewCupStack(item, target);
            if (PreviewProduction(item, target, out var productionPreview)) return productionPreview;

            if (receiver.Has(Capability.Discard))
            {
                return ActionResult.Yes("丢弃 · 扣除 " + Catalog.economy.wastePenalty + " 金币");
            }

            if (receiver.Has(Capability.Processor))
            {
                return PreviewMachine(item, target);
            }

            if (receiver.Has(Capability.Container))
            {
                // 单份原料：直接看目标容器容量够不够
                if (input.Has(Capability.Portion))
                {
                    return TotalUnits(target) + item.units > receiver.capacity
                        ? ActionResult.No("容器容量不足")
                        : ActionResult.Yes("加入 " + input.label);
                }

                // 杯倒杯：把内容物转移过去，两个杯子都保留
                if (input.Has(Capability.Container) && TotalUnits(item) > 0)
                {
                    return TotalUnits(target) + TotalUnits(item) > receiver.capacity
                        ? ActionResult.No("目标杯子容量不足")
                        : ActionResult.Yes("把内容物倒入 " + receiver.label + "（保留两个容器）");
                }
            }

            if ((receiver.Has(Capability.Customer) || receiver.Has(Capability.Delivery)) && input.Has(Capability.Container))
            {
                if (!State.open)
                {
                    return ActionResult.No("先开始营业再交付");
                }

                var request = RequestForTarget(target);
                if (request == null)
                {
                    return ActionResult.No("没有待交付的订单");
                }

                if (TotalUnits(item) == 0)
                {
                    return ActionResult.No("空杯不能交付");
                }

                // 交付前先告诉玩家配方是否正确，失败会有扣款
                var product = Product(request.productId);
                return ActionResult.Yes("确认上餐：" + product.label
                    + (Matches(item, product) ? " · 配方正确" : " · 配方不符，交付会失败并扣款"));
            }

            return ActionResult.No("不支持这种操作；Shift 只摆放");
        }

        /// <summary>执行一次交互：先做无副作用的预演，通过后再按目标类型分发。</summary>
        public ActionResult Interact(string itemId, string targetId)
        {
            var item = Find(itemId);
            var target = Find(targetId);
            var preview = Preview(item, target);
            if (!preview.success)
            {
                return preview;
            }

            var receiver = Spec(target.specId);
            if (IsCup(item) && IsCup(target) && item.contents.Count == 0 && target.contents.Count == 0)
                return StackCups(item, target);
            item.cupStackBelowId = null;
            if (IsCupSource(receiver))
            {
                ReturnCup(item, target);
                Notify();
                return preview;
            }
            if (ProductionEnabled && (IsProductionMachine(target) || receiver.Has(Capability.Container) || receiver.Has(Capability.Delivery)))
                return InteractProduction(item, target);
            if (receiver.Has(Capability.Processor))
            {
                return InteractMachine(item, target);
            }

            if (receiver.Has(Capability.Discard))
            {
                if (ProductionEnabled && item==ServingCup) return DiscardProductionCup();
                if (ProductionEnabled && IsExtractionFilter(item))
                {
                    Charge(Catalog.economy.wastePenalty);
                    item.contents.Clear(); item.production.filterSpent = false; item.production.filterDrainHold = 0;
                    Notify(); return ActionResult.Yes("滤杯已清空，可继续组合使用");
                }
                if (ProductionEnabled && (IsProductionVessel(item) || IsPrecisionExtractor(item) || IsMilkPitcher(item)))
                {
                    Charge(Catalog.economy.wastePenalty); ConsumeDeliveredContainer(item); Notify();
                    return ActionResult.Yes("壶中内容已丢弃，空壶已归位");
                }
                Charge(Catalog.economy.wastePenalty);
                State.entities.Remove(item);
                Notify();
                return ActionResult.Yes("已丢弃 " + Spec(item.specId).label + "，扣除 " + Catalog.economy.wastePenalty + " 金币");
            }

            if (receiver.Has(Capability.Container))
            {
                if (Spec(item.specId).Has(Capability.Portion))
                {
                    // 单份原料：直接并入目标容器，原物品消失
                    AddPortion(target, item.portionPayload != null ? CopyPortion(item.portionPayload)
                        : new Portion { specId = item.specId, units = item.units, quality = item.quality,
                            temperatureCelsius = TemperatureCelsius(item), temperature = TemperatureBand(TemperatureCelsius(item)) });
                    State.entities.Remove(item);
                }
                else
                {
                    // 杯倒杯：内容物全部转移，杯子本身保留；加了新东西后拉花作废
                    foreach (var p in item.contents)
                    {
                        AddPortion(target, p);
                    }

                    target.latteArt = false;
                    item.contents.Clear();
                    item.latteArt = false;
                }

                Notify();
                return preview;
            }

            return Serve(item, target);
        }

        /// <summary>取出并立即交互（拖拽落点用）：任何一步失败都不改变世界。</summary>
        public ActionResult TakeAndInteract(string sourceId, string targetId, AreaId area, float x, float y)
        {
            var source = Find(sourceId);
            if (source == null || !Spec(source.specId).Has(Capability.Source) || source.stock <= 0 || source.area != area)
            {
                return ActionResult.No("来源无货或不在当前区域");
            }

            // 先用一个临时对象预演组合，确认可行后再真正取出
            var candidate = new WorldEntity { specId = Spec(source.specId).sourceItemId, area = area, quality = source.quality };
            var preview = Preview(candidate, Find(targetId));
            if (!preview.success)
            {
                return preview;
            }

            var valid = CanPosition(candidate.specId, area, x, y);
            if (!valid.success)
            {
                return valid;
            }

            Take(sourceId, area, x, y, out var item);
            return Interact(item.id, targetId);
        }

        /// <summary>把一份原料并入容器；同规格同品质的份数直接累加。</summary>
        private void AddPortion(WorldEntity container, Portion portion)
        {
            var existing = container.contents.Find(p => p.specId == portion.specId && p.quality == portion.quality);
            if (existing == null)
            {
                container.contents.Add(CopyPortion(portion));
            }
            else
            {
                float priorWeight = GetThermalWeight(existing), addedWeight = GetThermalWeight(portion);
                if (priorWeight + addedWeight <= 0) { priorWeight = existing.units; addedWeight = portion.units; }
                float temperature = (TemperatureCelsius(existing) * priorWeight + TemperatureCelsius(portion) * addedWeight) / (priorWeight + addedWeight);
                existing.units += portion.units;
                existing.milliliters += portion.milliliters; existing.grams += portion.grams;
                MergeComposition(existing, portion);
                SetTemperature(existing, temperature);
            }

            // 加了新原料后，之前的拉花图案作废
            container.latteArt = false;
        }

        /// <summary>顾客目标就找该顾客的订单；其他目标（出餐口）用聚焦订单或第一条订单。</summary>
        private CustomerRequest RequestForTarget(WorldEntity target) => Spec(target.specId).Has(Capability.Customer)
            ? State.requests.Find(r => r.customerId == target.id)
            : Request(State.focusedRequestId) ?? (State.requests.Count > 0 ? State.requests[0] : null);

        /// <summary>生成一个不与现有实体/订单冲突的自增 id。</summary>
        private string Allocate(string prefix)
        {
            string id;
            do
            {
                id = prefix + State.nextId++;
            }
            while (Find(id) != null || Request(id) != null);

            return id;
        }

        /// <summary>扣钱，最低扣到 0，避免出现负数金币。</summary>
        private void Charge(int amount) => State.money = Math.Max(0, State.money - Math.Max(0, amount));

        /// <summary>排除 NaN / Infinity，避免坏数据污染世界状态。</summary>
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>通知视图刷新。</summary>
        private void Notify() { RefreshCupStacks(); Changed?.Invoke(); }
    }
}
