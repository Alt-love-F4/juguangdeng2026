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
            || (ProductionEnabled && entity != null && (entity.specId == "machine.grinder" || entity.specId == "machine.mixer"));
        public bool CanPour(WorldEntity entity) => IsProductionVessel(entity) && LiquidVolume(entity) > 0
            && (entity.production.phase == ProductionPhase.Ready || entity.production.phase == ProductionPhase.Boiling
                || entity.production.phase == ProductionPhase.Warm || entity.production.phase == ProductionPhase.Room);
        private bool IsLiquid(string id) => id == "water" || id == "hotwater" || id == "milk" || id == "hotmilk"
            || id == "coffee" || id == "cocoa.drink" || id == "matcha.drink";
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
                if ((initial.id == "s.water" || initial.id == "s.sugar" || initial.id == "tool.latte-art") && Find(initial.id) == null)
                {
                    var spec = Spec(initial.specId);
                    State.entities.Add(new WorldEntity { id = initial.id, specId = initial.specId, area = initial.area,
                        x = initial.x, y = initial.y, stock = spec.initialStock });
                }
            State.version = 3;
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
            return spec.capacityMl > 0 ? spec.capacityMl : IsProductionVessel(entity) ? Prep.kettleCapacityMl : spec.capacity * Prep.portionMl;
        }
        
        public string ProductionCaption(WorldEntity entity)
        {
            if (entity == null) return "空";
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
            var p = entity == null ? null : entity.contents.Find(x => x.milliliters > 0);
            return new CafeLiquidSnapshot(LiquidVolume(entity), LiquidCapacity(entity), p == null ? LiquidTemperature.Room : p.temperature,
                entity == null ? ProductionPhase.Empty : entity.production.phase, p?.specId, entity == null ? 100 : entity.production.extractionScore);
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
            if (old != null)
            {
                // 空杯换型：归还旧杯库存，而非凭空复制杯子。
                var oldSource = State.entities.Find(e => Spec(e.specId).sourceItemId == old.specId && Spec(e.specId).Has(Capability.Source));
                if (oldSource != null) oldSource.stock++;
                State.entities.Remove(old);
            }
            var cup = new WorldEntity { id = Allocate("free_"), specId = Spec(source.specId).sourceItemId, area = AreaId.Counter, x = tray.x, y = tray.y };
            source.stock--; State.entities.Add(cup); State.production.activeCupId = cup.id;
            Notify(); return ActionResult.Yes("出餐托盘：" + Spec(cup.specId).label);
        }

        private bool PreviewProduction(WorldEntity item, WorldEntity target, out ActionResult result)
        {
            result = default;
            if (!ProductionEnabled) return false;
            var input = Spec(item.specId); var receiver = Spec(target.specId);
            if (!string.IsNullOrEmpty(State.production.miniGameToken) && (target.id == State.production.miniGameCupId || item.id == State.production.miniGameCupId))
            { result = ActionResult.No("请先结束或取消小游戏"); return true; }
            if (receiver.Has(Capability.Customer))
            { result = input.Has(Capability.Container) && RequestForTarget(target) != null ? ActionResult.Yes("按质量评分交付，允许空杯与半成品") : ActionResult.No("没有可交付的订单"); return true; }
            if (receiver.Has(Capability.Delivery))
            {
                result = ServingCup == null ? ActionResult.No("先点击杯具选择出餐杯") : Preview(item, ServingCup);
                return true;
            }
            if (IsProductionMachine(target))
            {
                bool empty = target.contents.Count == 0 && target.production.phase == ProductionPhase.Empty;
                bool allowed = input.Has(Capability.Portion) && empty &&
                    (target.specId == "machine.grinder" && item.specId == "beans"
                    || target.specId == "machine.brewer" && item.specId == "grounds"
                    || target.specId == "machine.heater" && (item.specId == "water" || item.specId == "milk" || item.specId == "cocoa.powder")
                    || target.specId == "machine.mixer" && item.specId == "matcha.powder");
                result = allowed ? ActionResult.Yes("投入 " + input.label + "；点击设备启动")
                    : CanPour(item) && target.specId == "machine.brewer" && target.production.phase == ProductionPhase.Loaded
                        ? ActionResult.Yes("热水壶向下拖动倾倒，松开停止")
                        : ActionResult.No(empty ? "此设备不接受 " + input.label : "设备已有内容；先完成加工并取出");
                return true;
            }
            if (receiver.Has(Capability.Container))
            {
                if (CanPour(item)) { result = ActionResult.Yes("左键拖动壶，按住右键移动鼠标倾倒，松开右键停止，松开停止；注意杯容量"); return true; }
                if (IsProductionVessel(item)) { result = ActionResult.No("壶未完成加工"); return true; }
                if (input.Has(Capability.Portion))
                {
                    bool allowed = item.specId != "beans" && item.specId != "grounds" && item.specId != "cocoa.powder" && item.specId != "matcha.powder";
                    float ml = IsLiquid(item.specId) ? Prep.portionMl * item.units : 0;
                    result = !allowed ? ActionResult.No("原料需要先加工") : LiquidVolume(target) + ml > LiquidCapacity(target) + .01f
                        ? ActionResult.No("液体容量不足") : ActionResult.Yes("加入 " + input.label);
                    return true;
                }
                // 制作模式中杯倒杯不走旧版原子倾倒，避免绕过液体量与顺序记录。
                result = ActionResult.No("请使用加工壶倾倒液体，或直接加入小料"); return true;
            }
            return false;
        }

        private ActionResult InteractProduction(WorldEntity item, WorldEntity target)
        {
            if (Spec(target.specId).Has(Capability.Delivery)) return InteractProduction(item, ServingCup);
            if (IsProductionMachine(target))
            {
                if (CanPour(item)) return ActionResult.No("请在拖动时倾倒，不是松手整壶转移");
                string ingredient = item.specId;
                bool heat = target.specId == "machine.heater";
                if (heat && ingredient == "cocoa.powder") ingredient = "cocoa.drink";
                target.contents.Add(new Portion { specId = ingredient, units = item.units, quality = item.quality,
                    milliliters = heat ? Prep.portionMl * item.units : 0, productionScore = 100 });
                target.production.phase = ProductionPhase.Loaded;
                State.entities.Remove(item);
                if (State.automaticMachines && target.specId != "machine.brewer") return OperateProduction(target.id);
                Notify(); return ActionResult.Yes("已装料；点击设备启动");
            }
            if (CanPour(item)) return ActionResult.No("请拖动壶身并按住右键倾倒");
            var p = new Portion { specId = item.specId, units = item.units, quality = item.quality,
                milliliters = IsLiquid(item.specId) ? Prep.portionMl * item.units : 0, productionScore = 100,
                addedSequence = ++target.production.additionSequence };
            target.contents.Add(p); target.latteArt = false; State.entities.Remove(item);
            Notify(); return ActionResult.Yes("已加入 " + Spec(p.specId).label);
        }

        public ActionResult OperateProduction(string machineId)
        {
            var machine = Find(machineId);
            if (!IsProductionMachine(machine)) return ActionResult.No("无效设备");
            var data = machine.production;
            if (machine.specId == "machine.brewer") return ActionResult.No("加入咖啡粉后，拖沸腾热水壶到上方注水；无需点击");
            if (data.phase == ProductionPhase.Empty) return ActionResult.No("先拖入一份原料");
            if (data.phase == ProductionPhase.Loaded || machine.specId == "machine.heater" && (data.phase == ProductionPhase.Room || data.phase == ProductionPhase.Warm))
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
                Notify(); return ActionResult.Yes("研磨加速 1 秒");
            }
            return ActionResult.Yes(CanPour(machine) ? "左键拖动壶，按住右键移动鼠标倾倒，松开右键停止" : "成品就绪，拖出产物");
        }
        private void FinishProductionMachine(WorldEntity machine)
        {
            var data = machine.production;
            if (machine.specId == "machine.grinder") { machine.contents[0].specId = "grounds"; data.phase = ProductionPhase.Ready; }
            else if (machine.specId == "machine.mixer")
            { machine.contents[0].specId = "matcha.drink"; machine.contents[0].milliliters = Prep.portionMl; data.phase = ProductionPhase.Ready; }
            else
            {
                data.phase = ProductionPhase.Boiling; data.remaining = Prep.boilingSeconds;
                foreach (var p in machine.contents) p.temperature = LiquidTemperature.Boiling;
            }
        }
        private bool TickProduction(float seconds)
        {
            bool changed = false;
            foreach (var entity in State.entities)
            {
                if (!IsProductionMachine(entity)) continue;
                var d = entity.production; d.clickCooldown = Math.Max(0, d.clickCooldown - seconds);
                if (entity.specId == "machine.brewer" && d.brewStarted && d.phase == ProductionPhase.Loaded) d.brewElapsed += seconds;
                float elapsed = seconds;
                // 跨阶段消费同一帧剩余时间，长 Tick 不会丢失冷却时间。
                for (int transitions = 0; transitions < 3 && elapsed > 0; transitions++)
                {
                    if (d.phase != ProductionPhase.Processing && d.phase != ProductionPhase.Boiling && d.phase != ProductionPhase.Warm) break;
                    float used = Math.Min(elapsed, d.remaining); d.remaining -= used; elapsed -= used;
                    if (d.remaining > .0001f) break;
                    if (d.phase == ProductionPhase.Processing) FinishProductionMachine(entity);
                    else if (d.phase == ProductionPhase.Boiling)
                    { d.phase = ProductionPhase.Warm; d.remaining = Prep.warmSeconds; foreach (var p in entity.contents) p.temperature = LiquidTemperature.Warm; }
                    else { d.phase = ProductionPhase.Room; d.remaining = 0; foreach (var p in entity.contents) p.temperature = LiquidTemperature.Room; }
                    changed = true;
                }
            }
            return changed;
        }

        private WorldEntity ProductionOutputPreview(WorldEntity machine)
        {
            if (machine == null || machine.production.phase != ProductionPhase.Ready || machine.contents.Count == 0
                || (machine.specId != "machine.grinder" && machine.specId != "machine.mixer")) return null;
            var p = machine.contents[0];
            return new WorldEntity { id = machine.id + ":output", specId = p.specId, units = p.units, quality = p.quality, area = machine.area, x = machine.x, y = machine.y };
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
            if (!CanPour(source) || !Finite(requestedMl) || requestedMl <= 0 || !Finite(seconds) || seconds <= 0)
                return ActionResult.No("当前不能倾倒");
            if (source == target || target != null && source.area != target.area) return ActionResult.No("目标无效");
            bool brewer = target != null && target.specId == "machine.brewer";
            if (target != null && !Spec(target.specId).Has(Capability.Container) && !brewer) return ActionResult.No("此目标不接收液体");
            if (brewer && (target.production.phase != ProductionPhase.Loaded || target.contents.Find(p => p.specId == "grounds") == null
                || source.contents.Exists(p => (p.specId != "water" && p.specId != "hotwater") || p.temperature == LiquidTemperature.Room)))
                return ActionResult.No("咖啡壶需要咖啡粉和热水（沸腾或温热）");
            if (target != null && target.id == State.production.miniGameCupId && !string.IsNullOrEmpty(State.production.miniGameToken)) return ActionResult.No("小游戏期间不能加料");
            float amount = Math.Min(requestedMl, LiquidVolume(source));
            float accepted = target == null ? 0 : Math.Min(amount, Math.Max(0, LiquidCapacity(target) - LiquidVolume(target)));
            float acceptLeft = accepted, remaining = amount;
            for (int i = source.contents.Count - 1; i >= 0 && remaining > .0001f; i--)
            {
                var p = source.contents[i]; float take = Math.Min(p.milliliters, remaining);
                if (take <= 0) continue;
                float receive = Math.Min(take, acceptLeft);
                if (receive > 0)
                {
                    int sequence = target.production.additionSequence;
                    Portion same = null;
                    for (int j = 0; j < target.contents.Count; j++)
                    {
                        var existing = target.contents[j];
                        if (existing.specId == p.specId && existing.temperature == p.temperature && Math.Abs(existing.productionScore - p.productionScore) < .01f) { same = existing; break; }
                    }
                    if (same == null)
                    {
                        same = new Portion { specId = p.specId, units = 1, quality = p.quality, temperature = p.temperature,
                            productionScore = p.productionScore, addedSequence = ++target.production.additionSequence };
                        target.contents.Add(same);
                    }
                    // 后加咖啡也记录顺序违规；不依赖合并后的列表排列。
                    if (p.specId == "coffee" && sequence > 0 && same.addedSequence < sequence) same.addedSequence = sequence + 1;
                    same.milliliters += receive; acceptLeft -= receive; target.latteArt = false;
                }
                p.milliliters -= take; remaining -= take;
                if (p.milliliters <= .001f) source.contents.RemoveAt(i);
            }
            source.production.spilledMl += amount - accepted;
            if (brewer && accepted > 0)
            {
                var d = target.production; d.brewStarted = true; d.brewPourSeconds += seconds;
                float flow = accepted / seconds; d.flowSum += flow; d.flowSquareSum += flow * flow; d.flowSamples++;
            }
            if (source.contents.Count == 0) source.production = new ProductionEntityState { spilledMl = source.production.spilledMl };
            return ActionResult.Yes(accepted > 0 ? "已注入 " + accepted.ToString("0") + " ml" : "液体洒在地上（不返还）");
        }
        public ActionResult FinishPour(string targetId)
        {
            var target = Find(targetId);
            if (target == null || target.specId != "machine.brewer" || target.production.phase != ProductionPhase.Loaded)
                return ActionResult.Yes("停止倾倒");
            float volume = LiquidVolume(target);
            if (volume > Prep.brewIdealMl + Prep.brewGoldMl + Prep.brewGreenMl) return ActionResult.No("注水过量，请清空咖啡壶重做");
            if (volume < Prep.brewIdealMl - Prep.brewGoldMl - Prep.brewGreenMl) return ActionResult.No("水量不足，可继续注水");
            var d = target.production;
            float timeScore = Clamp(100 - Math.Abs(d.brewElapsed - Prep.brewIdealSeconds) / (Prep.brewGoldSeconds + Prep.brewGreenSeconds) * 40);
            float volumeScore = Clamp(100 - Math.Abs(volume - Prep.brewIdealMl) / (Prep.brewGoldMl + Prep.brewGreenMl) * 40);
            float mean = d.flowSamples > 0 ? d.flowSum / d.flowSamples : 0;
            float variance = d.flowSamples > 0 ? Math.Max(0, d.flowSquareSum / d.flowSamples - mean * mean) : 0;
            float uniformity = mean > 0 ? Clamp(100 - (float)Math.Sqrt(variance) / mean * 100) : 0;
            uniformity *= d.brewElapsed > 0 ? Math.Min(1, d.brewPourSeconds / d.brewElapsed) : 1;
            d.extractionScore = (timeScore + volumeScore + uniformity) / 3;
            int quality = target.contents.Find(p => p.specId == "grounds").quality;
            target.contents.Clear(); target.contents.Add(new Portion { specId = "coffee", units = 1, quality = quality,
                milliliters = volume, temperature = LiquidTemperature.Boiling, productionScore = d.extractionScore });
            d.phase = ProductionPhase.Ready; Notify(); return ActionResult.Yes("咖啡原液完成 · " + d.extractionScore.ToString("0") + " 分");
        }
    }
}
