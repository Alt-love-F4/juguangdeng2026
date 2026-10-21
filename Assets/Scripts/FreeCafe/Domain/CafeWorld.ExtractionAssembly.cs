using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public bool IsExtractionFilter(WorldEntity entity) => entity != null && entity.specId == "container.extractor.filter";
        public WorldEntity ExtractionJug(WorldEntity entity) => IsExtractionFilter(entity)
            ? Find(entity.production.extractionJugId) : IsPrecisionExtractor(entity) ? entity : null;
        public WorldEntity ExtractionFilter(WorldEntity jug) => IsPrecisionExtractor(jug)
            ? Find(jug.production.extractionFilterId) : null;

        private void InitializeExtractionAssemblies()
        {
            if (Spec("container.extractor.filter") == null) return;
            var jugs = State.entities.FindAll(IsPrecisionExtractor);
            foreach (var jug in jugs)
            {
                var filter = ExtractionFilter(jug);
                bool migrateGrounds = !jug.production.extractionAssemblyInitialized;
                if (filter == null || !IsExtractionFilter(filter)
                    || filter.production.extractionJugId != jug.id || filter.area != jug.area)
                    filter = FindNearbyUnlinkedFilter(jug);
                if (filter == null)
                {
                    // A detached original is still a real vessel, not a missing default.
                    // Never create a second entity with its ID when it is parked elsewhere.
                    if (Find(jug.id + ".filter") != null) continue;
                    filter = new WorldEntity { id = jug.id + ".filter", specId = "container.extractor.filter",
                        area = jug.area, x = jug.x + .9f, y = jug.y - .8f };
                    State.entities.Add(filter);
                }
                if (filter.production.extractionJugId != jug.id || jug.production.extractionFilterId != filter.id)
                {
                    if (!CanAutoAttachExtractionFilter(filter, jug)) continue;
                    jug.production.extractionFilterId = filter.id;
                    filter.production.extractionJugId = jug.id;
                    ResetExtraction(jug);
                }
                jug.production.extractionAssemblyInitialized = true;
                PositionExtractionFilter(jug);
                if (migrateGrounds)
                    for (int index = jug.contents.Count - 1; index >= 0; index--)
                        if (jug.contents[index].specId == "grounds")
                        { filter.contents.Add(jug.contents[index]); jug.contents.RemoveAt(index); }
            }
        }

        private WorldEntity FindNearbyUnlinkedFilter(WorldEntity jug)
        {
            WorldEntity nearest = null;
            float best = float.MaxValue;
            float maxDistance = Math.Max(0, Prep.extractionAssemblySnapDistance);
            float expectedX = jug.x + Prep.extractionFilterOffsetX;
            float expectedY = jug.y + Prep.extractionFilterOffsetY;
            foreach (var candidate in State.entities)
            {
                if (!IsExtractionFilter(candidate) || candidate.area != jug.area
                    || !string.IsNullOrEmpty(candidate.production.extractionJugId)
                    || !PreviewExtractionAssembly(candidate, jug).success) continue;
                float dx = candidate.x - expectedX, dy = candidate.y - expectedY;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared <= maxDistance * maxDistance && distanceSquared < best)
                { nearest = candidate; best = distanceSquared; }
            }
            return nearest;
        }

        private bool CanAutoAttachExtractionFilter(WorldEntity filter, WorldEntity jug)
        {
            if (!PreviewExtractionAssembly(filter, jug).success
                || !string.IsNullOrEmpty(filter.production.extractionJugId)) return false;
            float dx = filter.x - (jug.x + Prep.extractionFilterOffsetX);
            float dy = filter.y - (jug.y + Prep.extractionFilterOffsetY);
            float maxDistance = Math.Max(0, Prep.extractionAssemblySnapDistance);
            return dx * dx + dy * dy <= maxDistance * maxDistance;
        }

        private void PositionExtractionFilter(WorldEntity jug)
        {
            var filter = ExtractionFilter(jug);
            if (filter == null) return;
            filter.area = jug.area; filter.x = jug.x + Prep.extractionFilterOffsetX; filter.y = jug.y + Prep.extractionFilterOffsetY;
        }

        public ActionResult CanDragExtractionVessel(string id)
        {
            var item = Find(id);
            if (item == null || Locked(item) || MiniGameUses(item)) return ActionResult.No("器皿正在使用");
            var companion = IsExtractionFilter(item) ? ExtractionJug(item) : ExtractionFilter(item);
            return companion != null && (Locked(companion) || MiniGameUses(companion))
                ? ActionResult.No("组装器皿正在使用") : ActionResult.Yes("可拿起器皿");
        }

        /// <summary>Detach only when the lower vessel is used, not for a provisional pointer drag.</summary>
        public ActionResult PrepareExtractionDrag(string id)
        {
            var valid = CanDragExtractionVessel(id);
            if (!valid.success) return valid;
            var item = Find(id);
            var companion = IsExtractionFilter(item) ? ExtractionJug(item) : ExtractionFilter(item);
            if (IsPrecisionExtractor(item) && companion != null)
            { DetachExtractionFilter(companion); Notify(); }
            return ActionResult.Yes("已拿起器皿");
        }

        public ActionResult AttachExtractionFilter(string filterId, string jugId)
        {
            var filter = Find(filterId); var jug = Find(jugId);
            var valid = PreviewExtractionAssembly(filter, jug);
            if (!valid.success) return valid;
            // Dropping an already assembled pair must not complete/reset its current brew.
            if (ExtractionJug(filter) == jug && ExtractionFilter(jug) == filter)
            {
                PositionExtractionFilter(jug);
                Notify(); return ActionResult.Yes("滤杯已组合，保留现有内容与萃取状态");
            }
            DetachExtractionFilter(filter);
            jug.production.extractionFilterId = filter.id;
            jug.production.extractionAssemblyInitialized = true;
            filter.production.extractionJugId = jug.id;
            ResetExtraction(jug); PositionExtractionFilter(jug);
            Notify(); return ActionResult.Yes("滤杯已组合；咖啡粉放入上层，再从上方均匀注入热水");
        }

        private ActionResult PreviewExtractionAssembly(WorldEntity filter, WorldEntity jug)
        {
            if (!IsExtractionFilter(filter) || !IsPrecisionExtractor(jug) || filter.area != jug.area)
                return ActionResult.No("滤杯需要与咖啡萃取下壶组合");
            if (Locked(filter) || Locked(jug) || MiniGameUses(filter) || MiniGameUses(jug))
                return ActionResult.No("器皿正在使用");
            var previousJug = ExtractionJug(filter);
            if (previousJug != null && (Locked(previousJug) || MiniGameUses(previousJug)))
                return ActionResult.No("组装器皿正在使用");
            if (ExtractionFilter(jug) != null && ExtractionFilter(jug) != filter)
                return ActionResult.No("下壶已有滤杯，请先拆下上层");
            return ActionResult.Yes("把上层滤杯装到下壶上，保留现有内容物");
        }

        private void DetachExtractionFilter(WorldEntity filter)
        {
            if (!IsExtractionFilter(filter)) return;
            var jug = ExtractionJug(filter);
            if (jug != null)
            {
                if (jug.production.brewStarted && !jug.production.brewCompleted) CompleteExtraction(jug);
                jug.production.extractionFilterId = null;
            }
            filter.production.extractionJugId = null;
        }

        private WorldEntity ExtractionInputTarget(WorldEntity target, string ingredient)
        {
            var filter = ExtractionFilter(target);
            return filter != null && (ingredient == "grounds" || IsLiquid(ingredient)) ? filter : target;
        }

        private float ExtractionReceivingSpace(WorldEntity target)
        {
            float space = Math.Max(0, LiquidCapacity(target) - LiquidVolume(target));
            var jug = ExtractionJug(target);
            if (IsExtractionFilter(target) && jug != null)
                space = Math.Min(space, Math.Max(0, LiquidCapacity(jug) - LiquidVolume(jug) - LiquidVolume(target)));
            return space;
        }

        private Portion DrainedLiquid(Portion source, float ml)
        {
            EnsureCoffeeProvenance(source);
            var liquid = new Portion { specId = source.specId, units = source.units, milliliters = ml,
                grams = source.grams * ml / source.milliliters,
                quality = source.quality, temperature = source.temperature, extracted = source.extracted,
                coffeeKind = CoffeeKind(source), productionScore = source.productionScore };
            SetTemperature(liquid, TemperatureCelsius(source));
            CopyComposition(source, liquid, ml / source.milliliters);
            return liquid;
        }

        // Only water THROUGH the filter spends powder, never injected volume or milk flow.
        private float ConsumeFilterGrounds(WorldEntity filter, float waterMl, out float usedGrams)
        {
            usedGrams = 0;
            var d = filter.production;
            float remaining = 0;
            foreach (var p in filter.contents)
                if (p.specId == "grounds") remaining += p.grams > 0 ? p.grams : p.units * DoseGrams("grounds");
            if (d.filterSpent || remaining <= 0) return 0;
            if (d.filterReferenceGroundsGrams <= 0)
                d.filterReferenceGroundsGrams = remaining;
            double idealMl = (double)Prep.brewIdealMl * d.filterReferenceGroundsGrams / Prep.brewReferenceGroundsGrams;
            double passedBefore = Math.Min(d.filterWaterPassedMl, idealMl);
            double coffeeMl = Math.Min(waterMl, Math.Max(0, idealMl - passedBefore));
            if (coffeeMl <= 0) return 0;
            usedGrams = (float)Math.Min(remaining, d.filterReferenceGroundsGrams * coffeeMl / idealMl);
            // Use the remaining powder at the boundary to remove accumulated float residue exactly.
            if (passedBefore + coffeeMl >= idealMl - .00001) usedGrams = remaining;
            float fraction = usedGrams / remaining;
            foreach (var p in filter.contents)
                if (p.specId == "grounds")
                {
                    if (p.grams <= 0) p.grams = p.units * DoseGrams("grounds");
                    ScalePortionAmounts(p, Math.Max(0, 1 - fraction));
                }
            filter.contents.RemoveAll(p => p.specId == "grounds" && p.grams <= .000001f);
            if (!filter.contents.Exists(p => p.specId == "grounds")) d.filterSpent = true;
            d.filterWaterPassedMl = (float)Math.Min(float.MaxValue, (double)d.filterWaterPassedMl + waterMl);
            return (float)coffeeMl;
        }

        private bool TickExtractionDrain(float seconds)
        {
            bool changed = false;
            foreach (var filter in State.entities)
            {
                if (!IsExtractionFilter(filter)) continue;
                var jug = ExtractionJug(filter);
                if (jug == null) continue;
                PositionExtractionFilter(jug);
                float volume = LiquidVolume(filter);
                float hold = Math.Min(seconds, filter.production.filterDrainHold);
                filter.production.filterDrainHold -= hold;
                float elapsed = seconds - hold;
                float amount = Math.Min(volume, Math.Min(Prep.filterDrainMlPerSecond * elapsed,
                    Math.Max(0, LiquidCapacity(jug) - LiquidVolume(jug))));
                if (amount > 0 && volume > 0)
                {
                    float fraction = amount / volume;
                    float filterTemperature = TemperatureCelsius(filter);
                    SetContainerTemperature(filter, filterTemperature);
                    double priorWeight = 0, drainedWeight = 0;
                    foreach (var portion in jug.contents) priorWeight += GetThermalWeight(portion);
                    foreach (var portion in filter.contents)
                        if (portion.milliliters > 0) drainedWeight += GetThermalWeight(portion) * fraction;
                    float mixedTemperature = (float)((TemperatureCelsius(jug) * priorWeight + filterTemperature * drainedWeight) / (priorWeight + drainedWeight));
                    foreach (var portion in filter.contents.ToArray())
                    {
                        if (portion.milliliters <= 0) continue;
                        float drainedMl = portion.milliliters * fraction;
                        bool water = portion.specId == "water" || portion.specId == "hotwater";
                        float usedGrams = 0;
                        float coffeeMl = water ? ConsumeFilterGrounds(filter, drainedMl, out usedGrams) : 0;
                        if (coffeeMl > 0)
                        {
                            var coffee = DrainedLiquid(portion, coffeeMl);
                            if (coffee.composition.Count == 0) AddConstituent(coffee, "water", 0, coffeeMl);
                            AddConstituent(coffee, "grounds", usedGrams, 0);
                            coffee.specId = "coffee";
                            coffee.extracted = jug.production.brewCompleted;
                            coffee.coffeeKind = jug.production.brewCompleted ? "essence" : TemperatureBand(TemperatureCelsius(portion)) == LiquidTemperature.Room ? "cold_brew" : "black";
                            coffee.productionScore = jug.production.brewCompleted ? jug.production.extractionScore : 100;
                            ReceiveContainerPortion(jug, coffee, 1);
                        }
                        if (drainedMl > coffeeMl)
                            ReceiveContainerPortion(jug, DrainedLiquid(portion, drainedMl - coffeeMl), 1);
                        ScalePortionAmounts(portion, 1 - fraction);
                    }
                    filter.contents.RemoveAll(portion => portion.milliliters <= .00001f && portion.grams <= .00001f);
                    filter.production.filterDrainedMl += amount;
                    SetContainerTemperature(jug, mixedTemperature);
                    MixContainer(jug); RefreshContainerTemperature(jug, mixedTemperature); RefreshContainerTemperature(filter, filterTemperature);
                    changed = true;
                }
                if (jug.production.brewCompleted && filter.production.filterSpent && LiquidVolume(filter) <= .001f)
                {
                    filter.production.filterSpent = true;
                    DetachExtractionFilter(filter);
                    filter.x = jug.x + .9f; filter.y = jug.y - .75f;
                    changed = true;
                }
            }
            return changed;
        }
    }
}
