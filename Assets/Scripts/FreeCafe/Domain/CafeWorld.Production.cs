using System;

namespace Spotlight.FreeCafe
{
    /// <summary>制作界面业务扩展，沿用实体身份、世界指令与快照。旧模式保持原有份数配方语义。</summary>
    public sealed partial class CafeWorld
    {
        public bool ProductionEnabled => State.production != null && State.production.enabled;
        private ProductionSettings Prep => Catalog.production;
        public WorldEntity ServingCup => ProductionEnabled ? Find(State.production.activeCupId) : null;
        public bool IsProductionVessel(WorldEntity entity) => ProductionEnabled && entity != null
            && (entity.specId == "machine.heater" || entity.specId == "machine.brewer");
        private bool IsProductionMachine(WorldEntity entity) => IsProductionVessel(entity)
            || (ProductionEnabled && entity != null && (entity.specId == "machine.grinder" || entity.specId == "machine.mixer" || entity.specId == "machine.stove"));
        public bool IsContainer(WorldEntity entity) => entity != null && (Spec(entity.specId).Has(Capability.Container) || IsProductionVessel(entity));
        public bool CanPour(WorldEntity entity) => ProductionEnabled && IsContainer(entity) && LiquidVolume(entity) > 0
            && !IsMilkPitcher(entity)
            && (!IsExtractionFilter(entity) || ExtractionJug(entity) == null)
            && (!IsPrecisionExtractor(entity) || ExtractionFilter(entity) == null || !entity.production.brewStarted);
        private bool IsLiquid(string id) => IngredientDefinition(id)?.liquid ??
            (id == "water" || id == "hotwater" || id == "milk" || id == "hotmilk"
            || id == "coffee" || id == "milkfoam" || id == "cocoa.drink" || id == "matcha.drink");
        private static float Clamp(float value, float min = 0, float max = 100) => Math.Max(min, Math.Min(max, value));

        public void EnableProduction()
        {
            if (ProductionEnabled) return;
            State.production = new ProductionSession { enabled = true, returnArea = State.currentArea };
            // 在真正进入制作模式时才迁移旧设备任务。已有原料身份与已完成产物不会丢失。
            foreach (var entity in State.entities)
            {
                entity.production = entity.production ?? new ProductionEntityState();
                if (entity.task != null && IsProductionMachine(entity))
                {
                    var input = Find(entity.task.inputId);
                    if (input != null)
                    {
                        input.ownerId = null;
                        var bounds = Area(entity.area); var inputSpec = Spec(input.specId);
                        input.x = Math.Max(-bounds.halfWidth + inputSpec.width * .5f, Math.Min(bounds.halfWidth - inputSpec.width * .5f, entity.x));
                        input.y = Math.Max(-bounds.halfHeight + inputSpec.height * .5f, Math.Min(bounds.halfHeight - inputSpec.height * .5f, entity.y - 1));
                    }
                    entity.task = null;
                }
                var spec = Spec(entity.specId);
                float mlPerUnit = spec.Has(Capability.Container) && spec.capacity > 0 ? LiquidCapacity(entity) / spec.capacity : Prep.portionMl;
                foreach (var portion in entity.contents)
                    if (IsLiquid(portion.specId) && portion.milliliters <= 0)
                    {
                        // 旧存档没有毫升：按旧杯份数容量映射，保留装填比例和内容物占比，不能把旧大杯迁移成超容量坏档。
                        portion.milliliters = portion.units * mlPerUnit;
                        portion.addedSequence = ++entity.production.additionSequence;
                    }
            }
            foreach (var initial in Catalog.layout)
                if ((initial.id == "s.water" || initial.id == "s.sugar") && Find(initial.id) == null)
                {
                    var spec = Spec(initial.specId);
                    State.entities.Add(new WorldEntity { id = initial.id, specId = initial.specId, area = initial.area,
                        x = initial.x, y = initial.y, stock = spec.initialStock });
                }
            State.version = 3;
            RestoreContainerDefaults();
            Notify();
        }

        public ActionResult EnterProduction()
        {
            if (!ProductionEnabled) EnableProduction();
            if (State.currentArea != AreaId.Counter) State.production.returnArea = State.currentArea;
            SetArea(AreaId.Counter);
            return ActionResult.Yes("进入制作界面；拖原料到设备，点击设备开始加工，成品壶向下拖动倾倒");
        }
        public ActionResult LeaveProduction()
        {
            if (!ProductionEnabled) return ActionResult.No("制作系统未开启");
            SetArea(State.production.returnArea == AreaId.Counter ? AreaId.Cafe : State.production.returnArea);
            return ActionResult.Yes("返回入口；设备与制作状态保留");
        }
        public float LiquidVolume(WorldEntity entity)
        {
            float sum = 0;
            if (entity != null) foreach (var p in entity.contents) sum += p.milliliters;
            return sum;
        }
        public float LiquidCapacity(WorldEntity entity)
        {
            if (entity == null) return 0;
            var spec = Spec(entity.specId);
            return spec.capacityMl > 0 ? spec.capacityMl : IsProductionVessel(entity) || ProductionEnabled && entity.specId == "machine.mixer"
                ? Prep.kettleCapacityMl : spec.capacity * Prep.portionMl;
        }
        
        public string ProductionCaption(WorldEntity entity)
        {
            if (entity == null) return "空";
            if (IsContainer(entity)) return entity.production.phase == ProductionPhase.Processing ? (entity.production.selfHeating ? "自加热中 " : "加热中 ") + entity.production.remaining.ToString("0.0") + " s" : ContainerDescription(entity) + (entity.specId == "machine.heater" ? " · 点击自加热" : "");
            if (entity.specId == "machine.stove") return "放置容器 · 点击加热";
            var d = entity.production;
            if (entity.specId == "machine.brewer" && d.phase == ProductionPhase.Loaded)
                return "注水 " + LiquidVolume(entity).ToString("0") + " ml · " + d.brewElapsed.ToString("0.0") + " s";
            switch (d.phase)
            {
                case ProductionPhase.Loaded: return "已装料 · 点击启动";
                case ProductionPhase.Processing: return "加工中 " + d.remaining.ToString("0.0") + " s";
                case ProductionPhase.Ready: return entity.specId == "machine.grinder" ? "抽屉已出粉 · 拖出咖啡粉" : "成品就绪 · 向下拖动倾倒";
                case ProductionPhase.Boiling: return "沸腾 · " + d.remaining.ToString("0.0") + " s";
                case ProductionPhase.Warm: return "温热 · " + d.remaining.ToString("0.0") + " s";
                case ProductionPhase.Room: return "常温 · 点击重新加热";
                default: return entity.contents.Count > 0 ? "已有内容" : "空";
            }
        }

        public CafeLiquidSnapshot LiquidSnapshot(WorldEntity entity)
        {
            float coffee = 0, milk = 0, water = 0, matcha = 0, cocoa = 0, other = 0, milkFoam = 0;
            float capacity = LiquidCapacity(entity);
            Portion primary = null;
            float temperature = TemperatureCelsius(entity);
            if (entity != null)
            {
                float legacyPortionMl = capacity / Math.Max(1, Spec(entity.specId).capacity);
                foreach (var portion in entity.contents)
                {
                    float volume = Math.Max(0, portion.milliliters);
                    if (!ProductionEnabled && volume <= 0 && IsLiquid(portion.specId))
                        volume = Math.Max(0, portion.units) * legacyPortionMl;
                    if (volume <= 0) continue;
                    if (primary == null) primary = portion;
                    switch (portion.specId)
                    {
                        case "coffee": coffee += volume; break;
                        case "milkfoam": milk += volume; milkFoam += volume; break;
                        case "milk": case "hotmilk": case "cream": milk += volume; break;
                        case "water": case "hotwater": water += volume; break;
                        case "matcha.drink": matcha += volume; break;
                        case "cocoa.drink": cocoa += volume; break;
                        default: other += volume; break;
                    }
                }
            }
            var composition = new CafeLiquidComposition(coffee, milk, water, matcha, cocoa, other, milkFoam);
            return new CafeLiquidSnapshot(composition.TotalMl, capacity, TemperatureBand(temperature),
                entity == null ? ProductionPhase.Empty : entity.production.phase, primary?.specId,
                entity == null ? 100 : entity.production.extractionScore, composition, temperature);
        }

        public ActionResult SelectServingCup(string sourceId)
        {
            var source = Find(sourceId);
            if (!ProductionEnabled || source == null || source.area != AreaId.Counter || source.stock <= 0
                || !Spec(source.specId).Has(Capability.Source) || !Spec(Spec(source.specId).sourceItemId).Has(Capability.Container))
                return ActionResult.No("杯具来源无效或库存不足");
            var old = ServingCup;
            if (old != null && old.contents.Count > 0) return ActionResult.No("出餐杯已有内容，请先出品或丢弃");
            if (old != null && old.specId == Spec(source.specId).sourceItemId) return ActionResult.Yes("已选择此杯型");
            var tray = State.entities.Find(e => e.area == AreaId.Counter && Spec(e.specId).Has(Capability.Delivery));
            if (tray == null) return ActionResult.No("没有出餐托盘");
            var valid = CanPosition(Spec(source.specId).sourceItemId, AreaId.Counter, tray.x, tray.y);
            if (!valid.success) return valid;
            WorldEntity oldSource = null;
            if (old != null)
            {
                oldSource = State.entities.Find(e => e.area == old.area && IsCupSource(Spec(e.specId))
                    && Spec(e.specId).sourceItemId == old.specId);
                if (oldSource == null) return ActionResult.No("找不到原杯型杯堆，无法归还旧杯");
                valid = Preview(old, oldSource);
                if (!valid.success) return valid;
            }
            // Empty-cup swaps obey the same safe return policy, before any stock or identity is changed.
            if (old != null) ReturnCup(old, oldSource);
            var cup = new WorldEntity { id = Allocate("free_"), specId = Spec(source.specId).sourceItemId, area = AreaId.Counter, x = tray.x, y = tray.y };
            source.stock--; State.entities.Add(cup); State.production.activeCupId = cup.id;
            Notify(); return ActionResult.Yes("出餐托盘：" + Spec(cup.specId).label, CafeFeedbackKind.CupPickup);
        }

        private bool PreviewProduction(WorldEntity item, WorldEntity target, out ActionResult result)
        {
            result = default;
            if (!ProductionEnabled) return false;
            var input = Spec(item.specId); var receiver = Spec(target.specId);
            if (MiniGameUses(target) || MiniGameUses(item))
            { result = ActionResult.No("请先结束或取消小游戏"); return true; }
            if (IsExtractionFilter(item) && IsPrecisionExtractor(target))
            { result = PreviewExtractionAssembly(item, target); return true; }
            if (target.specId == "machine.stove")
            { result = IsContainer(item) ? ActionResult.Yes("放到炉台，点击炉台加热") : ActionResult.No("炉台需要容器"); return true; }
            if (IsContainer(target)) { result = PreviewContainer(item, target); return true; }
            if (receiver.Has(Capability.Customer))
            { result = input.Has(Capability.Container) && RequestForTarget(target) != null ? ActionResult.Yes("按质量评分交付，允许空杯与半成品") : ActionResult.No("没有可交付的订单"); return true; }
            if (receiver.Has(Capability.Delivery))
            {
                if(input.Has(Capability.Container))
                    result = ServingCup!=null && ServingCup!=item ? ActionResult.No("交付区已有杯子，请先交付、移走或丢弃") : ActionResult.Yes("放置出餐杯；按铃或点击 UI 交付按钮提交");
                else result = ServingCup == null ? ActionResult.No("交付区没有杯子，请先拖入杯子") : Preview(item, ServingCup);
                return true;
            }
            if (IsProductionMachine(target))
            {
                bool empty = target.contents.Count == 0 && target.production.phase == ProductionPhase.Empty;
                bool allowed = input.Has(Capability.Portion) && empty &&
                    (target.specId == "machine.grinder" && item.specId == "beans" || target.specId == "machine.mixer"
                    && (item.specId == "matcha.powder" || item.specId == "milk" || item.specId == "hotmilk"));
                result = allowed ? ActionResult.Yes("投入 " + input.label + "；点击加工") : ActionResult.No("此加工设备需要对应原料；通用配料请放入杯或壶");
                return true;
            }
            return false;
        }

        private ActionResult InteractProduction(WorldEntity item, WorldEntity target)
        {
            if (Spec(target.specId).Has(Capability.Delivery))
            {
                if(!Spec(item.specId).Has(Capability.Container)) return InteractProduction(item, ServingCup);
                SuspendContainerHeating(item.id);
                item.x=target.x; item.y=target.y; item.area=target.area;
                State.production.activeCupId=item.id;
                Notify(); return ActionResult.Yes("杯子已放到交付区；按铃交付当前订单");
            }
            if (IsExtractionFilter(item) && IsPrecisionExtractor(target)) return AttachExtractionFilter(item.id, target.id);
            if (target.specId == "machine.stove") return DockOnStove(item, target);
            if (IsContainer(target)) return AddToContainer(item, target);
            target.contents.Add(IngredientPortion(item));
            target.production.phase = ProductionPhase.Loaded;
            State.entities.Remove(item);
            if (State.automaticMachines) return OperateProduction(target.id);
            Notify(); return ActionResult.Yes("已装料；点击设备启动");
        }

        public ActionResult OperateProduction(string machineId)
        {
            var machine = Find(machineId);
            if (!IsProductionMachine(machine)) return ActionResult.No("无效设备");
            var data = machine.production;
            if (machine.specId == "machine.stove") return StartStove(machine);
            if (machine.specId == "machine.heater") return StartSelfHeating(machine.id);
            if (IsProductionVessel(machine)) return ActionResult.Yes("把壶放到炉台上，点击炉台加热");
            if (data.phase == ProductionPhase.Empty) return ActionResult.No("先拖入一份原料");
            if (data.phase == ProductionPhase.Loaded)
            {
                data.phase = ProductionPhase.Processing;
                data.remaining = MachineDuration(machine.specId, machine.specId == "machine.grinder" ? Prep.grindSeconds : Prep.heatSeconds);
                Notify(); return ActionResult.Yes("开始加工");
            }
            if (machine.specId == "machine.grinder" && data.phase == ProductionPhase.Processing)
            {
                if (data.clickCooldown > 0) return ActionResult.No("手柄正在转动");
                data.clickCooldown = Prep.grindClickCooldown; data.remaining = Math.Max(0, data.remaining - Prep.grindClickAdvance);
                if (data.remaining == 0) FinishProductionMachine(machine);
                Notify(); return ActionResult.Yes("研磨加速 1 秒", CafeFeedbackKind.GrinderTurn);
            }
            return ActionResult.Yes(CanPour(machine) ? "左键拖动壶，按住右键移动鼠标倾倒，松开右键停止" : "成品就绪，拖出产物");
        }
        private void FinishProductionMachine(WorldEntity machine)
        {
            var data = machine.production;
            if (machine.specId == "machine.grinder") { machine.contents[0].specId = "grounds"; data.phase = ProductionPhase.Ready; }
            else if (machine.specId == "machine.mixer")
            {
                var input = machine.contents[0];
                bool milk = input.specId == "milk" || input.specId == "hotmilk";
                if (input.composition == null || input.composition.Count == 0)
                    AddConstituent(input, input.specId, input.grams, input.milliliters);
                input.specId = milk ? "milkfoam" : "matcha.drink";
                if (!milk && input.milliliters <= 0)
                {
                    input.milliliters = Prep.portionMl;
                    AddConstituent(input, "water", 0, input.milliliters);
                }
                input.grams = 0;
                data.phase = ProductionPhase.Ready;
            }
            else
            {
                data.phase = ProductionPhase.Room;
                data.selfHeating = false;
                SetContainerTemperature(machine, Math.Max(TemperatureCelsius(machine), Prep.heatingTargetCelsius));
                MixContainer(machine);
                RefreshContainerTemperature(machine);
            }
        }
        private bool TickProduction(float seconds)
        {
            bool changed = false;
            // Account for this interval before drain can exhaust the filter. The next
            // tick sees the exhausted state and cannot leave a clock running forever.
            foreach(var jug in State.entities)
            {
                if(!IsPrecisionExtractor(jug))continue;
                var d=jug.production;var filter=ExtractionFilter(jug);
                if(d.brewStarted && !d.brewCompleted && !d.brewPourPaused && filter!=null &&
                    (!filter.production.filterSpent || LiquidVolume(filter)>.001f))
                {
                    d.brewElapsed=(float)Math.Min(float.MaxValue,(double)d.brewElapsed+seconds);
                    changed=true;
                }
            }
            changed |= TickExtractionDrain(seconds);
            foreach (var entity in State.entities)
            {
                if (!IsProductionMachine(entity) && !IsContainer(entity)) continue;
                if (entity.specId == "machine.stove") continue;
                if (IsContainer(entity) && !(entity.specId == "machine.heater" && entity.production.selfHeating) && entity.production.phase == ProductionPhase.Processing)
                {
                    var stove = State.entities.Find(candidate => candidate.specId == "machine.stove" && candidate.production.heatingVesselId == entity.id);
                    if (stove == null || stove.area != entity.area || Math.Abs(entity.x - stove.x) > .8f || Math.Abs(entity.y - StoveDockHeight(entity, stove)) > .7f)
                    { entity.production.phase = ProductionPhase.Room; entity.production.selfHeating = false; RefreshContainerTemperature(entity); changed = true; continue; }
                }
                var d = entity.production; d.clickCooldown = Math.Max(0, d.clickCooldown - seconds);
                // Only active processing consumes time. Warm/Boiling never cool automatically.
                if (d.phase != ProductionPhase.Processing) continue;
                float previousRemaining = d.remaining;
                float used = Math.Min(seconds, previousRemaining);
                if (IsContainer(entity))
                {
                    float current = TemperatureCelsius(entity);
                    float target = Math.Max(current, Prep.heatingTargetCelsius);
                    float fraction = previousRemaining > 0 ? used / previousRemaining : 1;
                    SetContainerTemperature(entity, current + (target - current) * fraction);
                    ApplyCompositionRules(entity);
                    MergeContainerContents(entity);
                }
                d.remaining = Math.Max(0, previousRemaining - used);
                if (d.remaining <= .0001f) FinishProductionMachine(entity);
                changed = true;
            }
            return changed;
        }

        private WorldEntity ProductionOutputPreview(WorldEntity machine)
        {
            if (machine == null || machine.production.phase != ProductionPhase.Ready || machine.contents.Count == 0
                || (machine.specId != "machine.grinder" && machine.specId != "machine.mixer")) return null;
            var p = machine.contents[0];
            return new WorldEntity { id = machine.id + ":output", specId = p.specId, units = p.units, quality = p.quality,
                portionPayload = CopyPortion(p), area = machine.area, x = machine.x, y = machine.y };
        }
        private ActionResult TakeProductionOutput(string machineId, string expectedId, string targetId, AreaId area, float x, float y)
        {
            var machine = Find(machineId); var candidate = ProductionOutputPreview(machine);
            if (candidate == null || candidate.id != expectedId || area != machine.area) return ActionResult.No("产物已变化");
            var valid = CanPosition(candidate.specId, area, x, y); if (!valid.success) return valid;
            if (targetId != null) { valid = Preview(candidate, Find(targetId)); if (!valid.success) return valid; }
            machine.contents.Clear(); machine.production = new ProductionEntityState();
            candidate.id = Allocate("free_"); candidate.x = x; candidate.y = y; State.entities.Add(candidate);
            if (targetId != null) return Interact(candidate.id, targetId);
            Notify(); return ActionResult.Yes("已拖出产物");
        }

        /// <summary>单位毫升，允许倾倒到地面/溢出。库存仅由此权威函数扣减。</summary>
        public ActionResult PourLiquid(string sourceId, string targetId, float requestedMl, float seconds)
        {
            var source = Find(sourceId); var target = Find(targetId);
            if (target != null && ExtractionFilter(target) != null) target = ExtractionFilter(target);
            if (!CanPour(source) || !Finite(requestedMl) || requestedMl <= 0 || !Finite(seconds) || seconds <= 0)
                return ActionResult.No("当前不能倾倒");
            if (IsCup(target) && (CupAbove(target) != null || !string.IsNullOrEmpty(target.cupStackBelowId))) return ActionResult.No("请先从杯堆取出杯子再倾倒");
            if (source == target || target != null && (source.area != target.area || !IsContainer(target))) return ActionResult.No("目标无效");
            if (MiniGameUses(source) || MiniGameUses(target))
                return ActionResult.No("小游戏期间不能改变内容");
            float total = LiquidVolume(source);
            float amount = Math.Min(requestedMl, total);
            if (target != null && ExtractionJug(target) == source) return ActionResult.No("不能把下壶倒回自己的滤杯");
            float accepted = target == null ? 0 : Math.Min(amount, ExtractionReceivingSpace(target));
            float takeFraction = amount / total, receiveFraction = accepted / total;
            // Compute accepted actual ml/free grams BEFORE reactions or ingredient compaction.
            var mixedTemperature = MixedTemperature(target, source, receiveFraction);
            SetContainerTemperature(source, TemperatureCelsius(source));
            float acceptedHotWater = 0;
            foreach (var portion in source.contents)
                if ((portion.specId == "water" || portion.specId == "hotwater") && TemperatureBand(TemperatureCelsius(portion)) != LiquidTemperature.Room)
                    acceptedHotWater += portion.milliliters * receiveFraction;
            // A clipped/depleted final frame contributes only the duration of its accepted stream.
            float acceptedSeconds = seconds * (accepted / requestedMl);
            if (target != null && acceptedHotWater > 0) AcceptExtractionWater(target, acceptedHotWater, acceptedSeconds);
            foreach (var portion in source.contents)
            {
                EnsureCoffeeProvenance(portion);
                if (target != null && accepted > 0) ReceiveContainerPortion(target, portion, receiveFraction);
                ScalePortionAmounts(portion, 1 - takeFraction);
            }
            source.contents.RemoveAll(portion => portion.milliliters <= 0 && portion.grams <= 0);
            source.production.spilledMl += amount - accepted;
            if (target != null && accepted > 0)
            {
                if (IsExtractionFilter(target) && LiquidVolume(target) <= accepted + .001f)
                    target.production.filterDrainHold = Prep.filterResidenceSeconds;
                SetContainerTemperature(target, mixedTemperature);
                MixContainer(target); RefreshContainerTemperature(target, mixedTemperature);
                var jug = ExtractionJug(target);
                if (jug != null && jug.production.brewStarted && !jug.production.brewCompleted
                    && jug.production.brewInjectedHotWaterMl > ExtractionSnapshot(jug).greenMlMax) CompleteExtraction(jug);
            }
            if (source.contents.Count == 0 && !IsPrecisionExtractor(source) && !IsExtractionFilter(source))
                source.production = new ProductionEntityState { spilledMl = source.production.spilledMl };
            else if(source.contents.Count==0 && IsContainer(source))source.production.phase=ProductionPhase.Empty;
            return ActionResult.Yes(accepted > 0 ? "已注入 " + accepted.ToString("0") + " ml" : "液体洒出（不返还）");
        }
        public ActionResult FinishPour(string targetId)
        {
            var target = ExtractionJug(Find(targetId));
            if(IsPrecisionExtractor(target) && target.production.brewStarted)target.production.brewPourPaused=true;
            if (IsPrecisionExtractor(target) && target.production.brewStarted && !target.production.brewCompleted
                && target.production.brewInjectedHotWaterMl >= ExtractionSnapshot(target).greenMlMin - .0001f)
                CompleteExtraction(target);
            TickExtractionDrain(0);
            Notify();
            return ActionResult.Yes(IsPrecisionExtractor(target) ? ExtractionSnapshot(target).qualityLabel : "停止倾倒 · 按实际成分与温度出品");
        }
    }
}
