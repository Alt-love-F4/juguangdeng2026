using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        
        private void ValidateProductionCatalog()
        {
            Catalog.production = Catalog.production ?? new ProductionSettings();
            var p = Catalog.production;
            foreach (float value in new[] { p.grindSeconds, p.grindClickAdvance, p.grindClickCooldown, p.heatSeconds, p.boilingSeconds,
                p.warmSeconds, p.portionMl, p.pourRateMl, p.brewIdealSeconds, p.brewGoldSeconds, p.brewGreenSeconds,
                p.brewIdealMl, p.brewGoldMl, p.brewGreenMl, p.kettleCapacityMl, p.brewReferenceGroundsGrams,
                p.filterDrainMlPerSecond, p.filterResidenceSeconds })
                if (!Finite(value) || value <= 0) throw new ArgumentException("制作参数必须为有限正数");
            foreach (float value in new[] { p.ambientTemperatureCelsius, p.warmTemperatureCelsius, p.boilingTemperatureCelsius,
                p.hotThresholdCelsius, p.boilingThresholdCelsius, p.heatingTargetCelsius })
                if (!Finite(value) || value < 0 || value > 100) throw new ArgumentException("温度参数必须在 0 到 100 摄氏度之间");
            if (p.hotThresholdCelsius >= p.boilingThresholdCelsius)
                throw new ArgumentException("沸腾温度阈值必须高于温热阈值");
            foreach (float value in new[] { p.brewGreenLeftSeconds, p.brewGreenRightSeconds, p.brewGreenLeftMl, p.brewGreenRightMl })
                if (!Finite(value) || value < 0) throw new ArgumentException("独立建议区长度必须为有限非负数");
            if (p.brewIdealMl <= p.brewGoldMl + p.brewGreenLeftMl || p.brewIdealSeconds <= p.brewGoldSeconds + p.brewGreenLeftSeconds)
                throw new ArgumentException("建议区下界不能小于零");
            if (Catalog.entities != null) foreach (var spec in Catalog.entities)
                if (spec != null && (!Finite(spec.capacityMl) || spec.capacityMl < 0)) throw new ArgumentException("液体容量无效");
            if (Catalog.products != null) foreach (var product in Catalog.products)
            {
                if (product == null) continue;
                if (!Finite(product.idealCoffeeRatio) || product.idealCoffeeRatio < 0 || product.idealCoffeeRatio > 1
                    || !Finite(product.idealSweetness) || product.idealSweetness < 0 || !Finite(product.minimumScore)
                    || product.minimumScore < 0 || product.minimumScore > 100 || product.maxRemakes < 0
                    || !Enum.IsDefined(typeof(LiquidTemperature), product.requiredTemperature)) throw new ArgumentException("餐品评价参数无效");
                if (product.ingredients != null) foreach (var ingredient in product.ingredients)
                    if (ingredient != null && (!Finite(ingredient.milliliters) || ingredient.milliliters < 0)) throw new ArgumentException("参考用量无效");
            }
        }

        private bool ValidPortionAmounts(Portion portion)
        {
            if (portion == null || !Finite(portion.grams) || portion.grams < 0
                || !Finite(portion.milliliters) || portion.milliliters < 0
                || !Finite(portion.productionScore) || portion.productionScore < 0 || portion.productionScore > 100
                || !Enum.IsDefined(typeof(LiquidTemperature), portion.temperature) || portion.addedSequence < 0
                || !Finite(portion.temperatureCelsius) || portion.temperatureCelsius != -1 && (portion.temperatureCelsius < 0 || portion.temperatureCelsius > 100)
                || portion.milliliters > 0 && !IsLiquid(portion.specId)) return false;
            if (!string.IsNullOrEmpty(portion.coffeeKind) && (portion.specId != "coffee"
                || portion.coffeeKind != "black" && portion.coffeeKind != "cold_brew" && portion.coffeeKind != "essence" && portion.coffeeKind != "hot")) return false;
            if (portion.composition == null) return true; // Legacy snapshots have no ledger.
            var identities = new HashSet<string>();
            double ml = 0;
            foreach (var constituent in portion.composition)
            {
                if (constituent == null || string.IsNullOrEmpty(constituent.ingredientId) || Spec(constituent.ingredientId) == null
                    || !Spec(constituent.ingredientId).Has(Capability.Portion)
                    || !identities.Add(CanonicalIngredient(constituent.ingredientId))
                    || !Finite(constituent.grams) || constituent.grams < 0
                    || !Finite(constituent.milliliters) || constituent.milliliters < 0) return false;
                ml += constituent.milliliters;
            }
            return ml <= portion.milliliters + .01;
        }

        private ActionResult ValidateProductionSnapshot(CafeSnapshot candidate)
        {
            candidate.production = candidate.production ?? new ProductionSession();
            if (!Enum.IsDefined(typeof(AreaId), candidate.production.returnArea)) return ActionResult.No("制作返回区域无效");
            foreach (var entity in candidate.entities)
            {
                if (entity.portionPayload != null && string.IsNullOrEmpty(entity.portionPayload.specId)
                    && entity.portionPayload.milliliters == 0 && entity.portionPayload.grams == 0
                    && (entity.portionPayload.composition == null || entity.portionPayload.composition.Count == 0))
                    entity.portionPayload = null; // Unity inline serialization expands null payloads.
                if (entity.portionPayload != null && (entity.portionPayload.specId != entity.specId
                    || !Spec(entity.specId).Has(Capability.Portion) || !ValidPortionAmounts(entity.portionPayload)
                    || entity.portionPayload.units <= 0 || entity.portionPayload.quality < 0 || entity.portionPayload.quality > 2))
                    return ActionResult.No("原料数量状态无效：" + entity.id);
                if(entity.contents!=null)foreach(var portion in entity.contents)
                {
                    if(portion.coffeeKind=="hot")portion.coffeeKind="black";
                    if(!string.IsNullOrEmpty(portion.coffeeKind) && (portion.specId!="coffee" || (portion.coffeeKind!="black" && portion.coffeeKind!="cold_brew" && portion.coffeeKind!="essence")))return ActionResult.No("咖啡类型无效");
                    if(portion.specId=="coffee" && string.IsNullOrEmpty(portion.coffeeKind))
                        portion.coffeeKind=entity.production!=null && entity.production.brewCompleted ? "essence" : CoffeeKind(portion);
                }
                entity.production = entity.production ?? new ProductionEntityState();
                var d = entity.production;
                if (!Enum.IsDefined(typeof(ProductionPhase), d.phase) || !Finite(d.remaining) || d.remaining < 0
                    || !Finite(d.clickCooldown) || d.clickCooldown < 0 || !Finite(d.brewElapsed) || d.brewElapsed < 0
                    || !Finite(d.brewPourSeconds) || d.brewPourSeconds < 0
                    || !Finite(d.flowSum) || d.flowSum < 0 || !Finite(d.flowSquareSum) || d.flowSquareSum < 0
                    || d.flowSamples < 0 || !Finite(d.extractionScore) || d.extractionScore < 0 || d.extractionScore > 100
                    || !Finite(d.brewGroundsGrams) || d.brewGroundsGrams < 0
                    || !Finite(d.brewInjectedHotWaterMl) || d.brewInjectedHotWaterMl < 0
                    || !Finite(d.brewRateSquareIntegral) || d.brewRateSquareIntegral < 0
                    || !Finite(d.filterDrainHold) || d.filterDrainHold < 0
                    || !Finite(d.filterDrainedMl) || d.filterDrainedMl < 0
                    || !Finite(d.filterReferenceGroundsGrams) || d.filterReferenceGroundsGrams < 0
                    || !Finite(d.filterWaterPassedMl) || d.filterWaterPassedMl < 0
                    || d.filterWaterPassedMl > 0 && d.filterReferenceGroundsGrams <= 0
                    || d.brewCompleted && !d.brewStarted
                    || d.brewStarted && (d.brewGroundsGrams <= 0 || d.brewInjectedHotWaterMl <= 0 || d.brewPourSeconds <= 0)
                    || !Finite(d.spilledMl) || d.spilledMl < 0 || d.additionSequence < 0)
                    return ActionResult.No("道具状态无效：" + entity.id);
                foreach (var p in entity.contents)
                    if (!ValidPortionAmounts(p)) return ActionResult.No("液体状态无效：" + entity.id);
                if (candidate.production.enabled)
                {
                    var spec = Spec(entity.specId);
                    bool vessel = entity.specId == "machine.heater" || entity.specId == "machine.brewer";
                    float capacity = spec.capacityMl > 0 ? spec.capacityMl : vessel || entity.specId == "machine.mixer"
                        ? Prep.kettleCapacityMl : spec.capacity * Prep.portionMl;
                    if (LiquidVolume(entity) > capacity + .01f) return ActionResult.No("液体容量超限：" + entity.id);
                    if (spec.Has(Capability.Processor) && entity.task != null) return ActionResult.No("制作设备不能同时持有旧加工任务");
                    bool machine = vessel || entity.specId == "machine.grinder" || entity.specId == "machine.mixer";
                    if (machine && (d.phase == ProductionPhase.Empty) != (entity.contents.Count == 0)) return ActionResult.No("设备状态与内容物不匹配");
                    if ((entity.specId == "machine.grinder" || entity.specId == "machine.mixer")
                        && d.phase != ProductionPhase.Empty && d.phase != ProductionPhase.Loaded && d.phase != ProductionPhase.Processing && d.phase != ProductionPhase.Ready)
                        return ActionResult.No("固体加工设备阶段无效");
                    if (entity.specId == "machine.mixer" && entity.contents.Count > 0 && (entity.contents.Count != 1
                        || (d.phase == ProductionPhase.Ready
                            ? entity.contents[0].specId != "matcha.drink" && entity.contents[0].specId != "milkfoam"
                            : entity.contents[0].specId != "matcha.powder" && entity.contents[0].specId != "milk" && entity.contents[0].specId != "hotmilk")))
                        return ActionResult.No("搅拌材料无效");
                    if (entity.specId == "machine.grinder" && entity.contents.Count > 0
                        && (entity.contents.Count != 1 || entity.contents[0].specId != (d.phase == ProductionPhase.Ready ? "grounds" : "beans")))
                        return ActionResult.No("研磨器材料无效");

                }
            }
            foreach (var entity in candidate.entities)
            {
                var data = entity.production;
                if (!string.IsNullOrEmpty(data.extractionFilterId))
                {
                    var filter = candidate.entities.Find(item => item.id == data.extractionFilterId);
                    if (!Spec(entity.specId).Has(Capability.HotExtraction) || filter == null || !IsExtractionFilter(filter)
                        || filter.production.extractionJugId != entity.id || filter.area != entity.area
                        || LiquidVolume(entity) + LiquidVolume(filter) > LiquidCapacity(entity) + .01f)
                        return ActionResult.No("滤杯组合引用无效：" + entity.id);
                }
                if (!string.IsNullOrEmpty(data.extractionJugId))
                {
                    var jug = candidate.entities.Find(item => item.id == data.extractionJugId);
                    if (!IsExtractionFilter(entity) || jug == null || jug.production.extractionFilterId != entity.id)
                        return ActionResult.No("下壶组合引用无效：" + entity.id);
                }
            }
            foreach (var request in candidate.requests)
                if (request.remakes < 0 || !Finite(request.lastProductionScore) || request.lastProductionScore < 0 || request.lastProductionScore > 100)
                    return ActionResult.No("订单评分无效");
            if (!string.IsNullOrEmpty(candidate.production.miniGameToken) && candidate.production.miniGameToken.StartsWith("minigame_")
                && int.TryParse(candidate.production.miniGameToken.Substring(9), out var sequence)) candidate.nextId = Math.Max(candidate.nextId, sequence + 1);
            var session = candidate.production;
            if (!string.IsNullOrEmpty(session.activeCupId))
            {
                var cup = candidate.entities.Find(e => e.id == session.activeCupId);
                if (cup == null || cup.area != AreaId.Counter || !Spec(cup.specId).Has(Capability.Container)) return ActionResult.No("出餐杯引用无效");
            }
            if (!string.IsNullOrEmpty(session.miniGameToken)
                && (session.miniGameCupId != session.activeCupId || string.IsNullOrEmpty(session.activeCupId) || session.miniGameKind != "latte-art"))
                return ActionResult.No("小游戏引用无效");
            // Numeric Celsius is authoritative; legacy sentinel and enum mirrors are normalized only
            // after the complete candidate has validated, before it can replace the live State.
            foreach (var entity in candidate.entities)
            {
                foreach (var portion in entity.contents) SetTemperature(portion, TemperatureCelsius(portion));
                if (entity.portionPayload != null) SetTemperature(entity.portionPayload, TemperatureCelsius(entity.portionPayload));
            }
            return ActionResult.Yes("制作快照有效");
        }
    }
}
