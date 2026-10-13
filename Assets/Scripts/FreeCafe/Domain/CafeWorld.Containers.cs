using System;
using System.Collections.Generic;
using System.Text;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        private void RestoreContainerDefaults()
        {
            if (!ProductionEnabled) return;
            foreach (var entity in State.entities)
            {
                foreach (var portion in entity.contents)
                    if (!IsLiquid(portion.specId) && portion.grams <= 0) portion.grams = portion.units * (portion.specId == "sugar" ? 5 : 12);
                if (IsContainer(entity)) MergeContainerContents(entity);
            }
            foreach (var entry in Catalog.layout)
                if ((entry.specId == "machine.stove" || Spec(entry.specId).Has(Capability.HotExtraction)) && Find(entry.id) == null)
                    State.entities.Add(new WorldEntity { id = entry.id, specId = entry.specId, area = entry.area, x = entry.x, y = entry.y });
        }

        private bool IsCupSource(EntitySpec spec) => spec != null && spec.Has(Capability.Source)
            && Spec(spec.sourceItemId) != null && Spec(spec.sourceItemId).Has(Capability.Container);

        private ActionResult PreviewCupReturn(WorldEntity cup, WorldEntity source)
        {
            var sourceSpec = Spec(source.specId);
            if (!Spec(cup.specId).Has(Capability.Movable) || cup.specId != sourceSpec.sourceItemId)
                return ActionResult.No("杯型不匹配，请归还到对应杯堆");
            if (cup.task != null || cup.production != null && cup.production.phase == ProductionPhase.Processing)
                return ActionResult.No("杯子正在加工，请先停止加工再归还");
            if (ProductionEnabled && !string.IsNullOrEmpty(State.production.miniGameToken)
                && cup.id == State.production.miniGameCupId)
                return ActionResult.No("请先结束或取消小游戏，再归还杯子");
            // A solid-only or legacy portion is still content; never discard a drink or residue on return.
            if (cup.contents.Count != 0)
                return ActionResult.No("杯子还有内容，请先倒空或丢弃内容后再归还");
            if (source.stock >= sourceSpec.initialStock)
                return ActionResult.No("杯堆已满，请先取出杯子再归还");
            return ActionResult.Yes("归还空杯：" + Spec(cup.specId).label);
        }

        // Called only after Preview succeeds; update identity, stock and references before one notification.
        private void ReturnCup(WorldEntity cup, WorldEntity source)
        {
            State.entities.Remove(cup);
            source.stock++;
            if (State.production != null && State.production.activeCupId == cup.id)
                State.production.activeCupId = null;
            foreach (var entity in State.entities)
                if (entity.production != null && entity.production.heatingVesselId == cup.id)
                    entity.production.heatingVesselId = null;
        }

        public float SolidMass(WorldEntity entity)
        {
            float total = 0;
            if (entity != null) foreach (var portion in entity.contents) total += portion.grams;
            return total;
        }

        public string ContainerDescription(WorldEntity entity)
        {
            if (!IsContainer(entity)) return ProductionCaption(entity);
            float volume = LiquidVolume(entity);
            float grounds = 0;
            bool coffee = false, extracted = false, milk = false, cocoa = false;
            foreach (var portion in entity.contents)
            {
                if (portion.specId == "grounds") grounds += portion.grams > 0 ? portion.grams : portion.units * 12;
                if (portion.specId == "coffee") { coffee = true; extracted |= portion.extracted; }
                milk |= portion.specId == "milk" || portion.specId == "hotmilk";
                cocoa |= portion.specId == "cocoa.powder" || portion.specId == "cocoa.drink";
            }
            string description = coffee ? (grounds > 0 && grounds / Math.Max(1, volume) < .04f ? "淡淡" : "浓")
                + (extracted ? "精粹咖啡液" : "冷萃咖啡液") : volume > 0 ? "混合液" : "空容器";
            if (milk) description += "+奶";
            if (cocoa) description += "+可可";
            return description;
        }

        public string ContainerInspection(WorldEntity entity)
        {
            var text = new StringBuilder(ContainerDescription(entity));
            text.Append(" · ").Append(LiquidVolume(entity).ToString("0")).Append("/").Append(LiquidCapacity(entity).ToString("0")).Append(" ml");
            text.Append(" · ").Append(SolidMass(entity).ToString("0.0")).Append(" g · ").Append(LiquidSnapshot(entity).temperature == LiquidTemperature.Boiling ? "热" : LiquidSnapshot(entity).temperature == LiquidTemperature.Warm ? "温" : "常温");
            // Inspection must also handle fragmented snapshots without changing their contents.
            var ingredients = new List<string>();
            var volumes = new List<float>();
            var masses = new List<float>();
            foreach (var portion in entity.contents)
            {
                int index = ingredients.FindIndex(id => SameIngredient(id, portion.specId));
                if (index < 0)
                {
                    ingredients.Add(portion.specId); volumes.Add(portion.milliliters); masses.Add(portion.grams);
                }
                else { volumes[index] += portion.milliliters; masses[index] += portion.grams; }
            }
            for (int i = 0; i < ingredients.Count; i++)
                text.Append(" · ").Append(Spec(ingredients[i]).label).Append(" ").Append(volumes[i] > 0 ? volumes[i].ToString("0.0") + "ml" : masses[i].ToString("0.0") + "g");
            return text.ToString();
        }

        // Identity is the ingredient, not mutable temperature/extraction/quality metadata.
        private void MergePortionAmounts(Portion target, Portion incoming, float fraction)
        {
            float volume = incoming.milliliters * fraction, mass = incoming.grams * fraction;
            float priorWeight = IsLiquid(target.specId) ? target.milliliters : target.grams;
            float addedWeight = IsLiquid(incoming.specId) ? volume : mass;
            float weight = priorWeight + addedWeight;
            if (weight > 0)
                target.productionScore = (target.productionScore * priorWeight + incoming.productionScore * addedWeight) / weight;
            if (target.milliliters + volume > 0)
                target.temperature = (LiquidTemperature)(int)Math.Round(((int)target.temperature * target.milliliters + (int)incoming.temperature * volume) / (target.milliliters + volume));
            target.quality = Math.Min(target.quality, incoming.quality); // SalePrice uses the lowest ingredient quality.
            target.extracted |= incoming.extracted;
            target.units = Math.Max(target.units, incoming.units); // Production quantities are ml/g, not pour-frame counts.
            target.milliliters += volume;
            target.grams += mass;
        }

        private void ReceiveContainerPortion(WorldEntity entity, Portion incoming, float fraction)
        {
            if (incoming.milliliters * fraction <= 0 && incoming.grams * fraction <= 0) return;
            var same = entity.contents.Find(portion => SameIngredient(portion.specId, incoming.specId));
            if (same != null) { MergePortionAmounts(same, incoming, fraction); return; }
            entity.contents.Add(new Portion { specId = incoming.specId, units = Math.Max(1, incoming.units), quality = incoming.quality,
                milliliters = incoming.milliliters * fraction, grams = incoming.grams * fraction,
                temperature = incoming.temperature, extracted = incoming.extracted, productionScore = incoming.productionScore,
                addedSequence = ++entity.production.additionSequence });
        }

        private void MergeContainerContents(WorldEntity entity)
        {
            // Capture the existing volume-weighted container temperature before ingredient compaction.
            var temperature = LiquidSnapshot(entity).temperature;
            bool merged = false;
            for (int i = 0; i < entity.contents.Count; i++)
                for (int j = i + 1; j < entity.contents.Count; j++)
                {
                    var first = entity.contents[i]; var next = entity.contents[j];
                    if (!SameIngredient(first.specId, next.specId)) continue;
                    MergePortionAmounts(first, next, 1);
                    first.addedSequence = Math.Min(first.addedSequence, next.addedSequence);
                    entity.contents.RemoveAt(j--);
                    merged = true;
                }
            if (merged) foreach (var portion in entity.contents) portion.temperature = temperature;
        }

        private void MixContainer(WorldEntity entity)
        {
            bool grounds = entity.contents.Exists(portion => portion.specId == "grounds" && (portion.grams > 0 || portion.units > 0));
            bool cocoa = entity.contents.Exists(portion => portion.specId == "cocoa.powder");
            bool matcha = entity.contents.Exists(portion => portion.specId == "matcha.powder");
            foreach (var portion in entity.contents)
            {
                if (portion.specId != "water" && portion.specId != "hotwater") continue;
                if (grounds)
                {
                    // 专用萃取容器无需炉台或点击；只有实际热水参与时才标记萃取。
                    if (Spec(entity.specId).Has(Capability.HotExtraction)
                        && portion.milliliters > 0 && portion.temperature != LiquidTemperature.Room)
                        portion.extracted = true;
                    portion.specId = "coffee";
                }
                else if (cocoa) portion.specId = "cocoa.drink";
                else if (matcha) portion.specId = "matcha.drink";
            }
            MergeContainerContents(entity);
            entity.latteArt = false;
            if (entity.production.phase != ProductionPhase.Processing && entity.production.phase != ProductionPhase.Boiling && entity.production.phase != ProductionPhase.Warm)
                entity.production.phase = entity.contents.Count == 0 ? ProductionPhase.Empty : ProductionPhase.Room;
        }

        private void RefreshContainerTemperature(WorldEntity entity, LiquidTemperature? mixedTemperature = null)
        {
            if (entity.production.phase == ProductionPhase.Processing) return;
            var temperature = mixedTemperature ?? LiquidSnapshot(entity).temperature;
            foreach (var portion in entity.contents) portion.temperature = temperature;
            entity.production.phase = entity.contents.Count == 0 ? ProductionPhase.Empty : temperature == LiquidTemperature.Boiling ? ProductionPhase.Boiling : temperature == LiquidTemperature.Warm ? ProductionPhase.Warm : ProductionPhase.Room;
            entity.production.remaining = temperature == LiquidTemperature.Boiling ? Prep.boilingSeconds : temperature == LiquidTemperature.Warm ? Prep.warmSeconds : 0;
        }

        private ActionResult PreviewContainer(WorldEntity item, WorldEntity target)
        {
            if (IsContainer(item)) return ActionResult.Yes("左键拿起，按住右键倾倒；松开右键继续拿着");
            if (!Spec(item.specId).Has(Capability.Portion)) return ActionResult.No("请加入原料或从其他容器倾倒");
            float volume = IsLiquid(item.specId) ? Prep.portionMl * item.units : 0;
            return LiquidVolume(target) + volume <= LiquidCapacity(target) + .01f
                ? ActionResult.Yes("自由加入 " + Spec(item.specId).label) : ActionResult.No("容器液体容量不足");
        }

        private ActionResult AddToContainer(WorldEntity item, WorldEntity target)
        {
            if (IsContainer(item)) return ActionResult.No("请按住右键倾倒，而不是松手整杯转移");
            target.contents.Add(new Portion { specId = item.specId, units = item.units, quality = item.quality,
                milliliters = IsLiquid(item.specId) ? Prep.portionMl * item.units : 0,
                grams = IsLiquid(item.specId) ? 0 : item.units * (item.specId == "sugar" ? 5 : 12),
                addedSequence = ++target.production.additionSequence,
                temperature = item.specId == "hotwater" || item.specId == "hotmilk" ? LiquidTemperature.Boiling : LiquidTemperature.Room });
            State.entities.Remove(item);
            MixContainer(target);
            RefreshContainerTemperature(target);
            Notify();
            return ActionResult.Yes(ContainerInspection(target));
        }

        public void SuspendContainerHeating(string entityId)
        {
            var entity = Find(entityId);
            if (!IsContainer(entity) || entity.production.phase != ProductionPhase.Processing) return;
            entity.production.phase = ProductionPhase.Room;
            RefreshContainerTemperature(entity);
            Notify();
        }

        private float StoveDockHeight(WorldEntity vessel, WorldEntity stove) => stove.y + Spec(stove.specId).height * .45f + Spec(vessel.specId).height * .5f;

        private ActionResult DockOnStove(WorldEntity vessel, WorldEntity stove)
        {
            if (!IsContainer(vessel)) return ActionResult.No("把容器放在炉台上，再点击炉台加热");
            var prior = Find(stove.production.heatingVesselId);
            if (prior != null && prior != vessel && Math.Abs(prior.x - stove.x) < .8f && Math.Abs(prior.y - stove.y) < 1.6f)
                return ActionResult.No("炉台上已有容器");
            vessel.x = stove.x; vessel.y = StoveDockHeight(vessel, stove);
            stove.production.heatingVesselId = vessel.id;
            Notify();
            return ActionResult.Yes("容器已放到炉台 · 点击炉台开始加热");
        }

        private ActionResult StartStove(WorldEntity stove)
        {
            var vessel = Find(stove.production.heatingVesselId);
            if (!IsContainer(vessel) || vessel.area != stove.area || Math.Abs(vessel.x - stove.x) > .8f || Math.Abs(vessel.y - StoveDockHeight(vessel, stove)) > .7f)
                return ActionResult.No("请先把杯子或壶放到炉台上");
            if (!string.IsNullOrEmpty(State.production.miniGameToken) && vessel.id == State.production.miniGameCupId) return ActionResult.No("请先结束小游戏");
            if (vessel.contents.Count == 0) return ActionResult.No("空容器没有内容可加热");
            if (vessel.production.phase == ProductionPhase.Processing) return ActionResult.Yes("正在加热");
            vessel.production.phase = ProductionPhase.Processing;
            vessel.production.remaining = Prep.heatSeconds;
            Notify();
            return ActionResult.Yes("开始加热 · 粉与水加热后萃取，不限制其他配料");
        }
    }
}
