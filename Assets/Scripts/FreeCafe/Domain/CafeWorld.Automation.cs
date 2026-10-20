using System;

namespace Spotlight.FreeCafe
{
    /// <summary>A bounded batch receipt; counts come from actual delivery results, never synthetic story events.</summary>
    public sealed class CafeAutomationResult
    {
        public int Attempts { get; internal set; }
        public int Delivered { get; internal set; }
        public int OrdinaryOrdersServed { get; internal set; }
        public int NarrativeDeliveries { get; internal set; }
        public int TutorialDeliveries { get; internal set; }
        public int RemainingOrders { get; internal set; }
        public bool ReachedLimit { get; internal set; }
        public string StopReason { get; internal set; }
    }

    public sealed partial class CafeWorld
    {
        public const int AutomationDeliveryLimit = 32;
        private bool automationServing;

        /// <summary>
        /// Explicit background-assistance cheat: unlock catalog products/equipment and refill sources for free.
        /// Does not grant money, reputation, successful orders, NPCs, story authorization or equipment-purchase events.
        /// Repeated calls preserve excess stock and never duplicate product IDs.
        /// </summary>
        public ActionResult AutomationUnlockAndRefill()
        {
            bool changed = !State.sandboxEquipment || !State.precisionEquipmentActive || !State.latteEquipmentActive;
            // Sandbox is necessary because CanOfferProduct also gates mixed drinks on successful-order count.
            State.sandboxEquipment = State.precisionEquipmentActive = State.latteEquipmentActive = true;
            foreach (var product in Catalog.products)
                if (!State.unlockedProducts.Contains(product.id))
                { State.unlockedProducts.Add(product.id); changed = true; }
            foreach (var entity in State.entities)
            {
                var spec = Spec(entity.specId);
                if (spec != null && spec.Has(Capability.Source) && entity.stock < spec.initialStock)
                { entity.stock = spec.initialStock; changed = true; }
            }
            if (changed) Notify();
            return ActionResult.Yes(changed ? "后台辅助：免费解锁餐品/设备并补满来源" : "后台辅助：餐品、设备与库存已就绪");
        }

        /// <summary>
        /// Services only orders already authorized in this world, including successive narrative-service quotas.
        /// Assistance creates ideal measured portions/provenance, extraction quality and completed latte art;
        /// it skips physical preparation/minigames, not classification, evaluation, delivery or story callbacks.
        /// Does not open business, tick time, spawn customers, begin narrative services or change NCS conditions.
        /// At most 32 real delivery attempts per call. Failed quality/remakes stop the batch instead of retrying forever.
        /// Existing player cups/machines and surviving original order focus are preserved. Not a thread-safe API.
        /// </summary>
        public CafeAutomationResult AutomationServePendingOrders(int maxDeliveries = AutomationDeliveryLimit)
        {
            var report = new CafeAutomationResult { RemainingOrders = State.requests.Count };
            if (maxDeliveries < 1 || maxDeliveries > AutomationDeliveryLimit)
            { report.StopReason = "maxDeliveries must be between 1 and 32."; return report; }
            if (automationServing)
            { report.StopReason = "An automation delivery batch is already running."; return report; }
            if (!ProductionEnabled)
            { report.StopReason = "Production must be enabled before background delivery."; return report; }
            if (!string.IsNullOrEmpty(State.production.miniGameToken))
            { report.StopReason = "An active player minigame must finish first."; return report; }
            if (State.requests.Count == 0) return report;
            var tray = State.entities.Find(e => e.area == AreaId.Counter && Spec(e.specId).Has(Capability.Delivery));
            if (tray == null)
            { report.StopReason = "No counter delivery tray is available."; return report; }

            string previousCup = State.production.activeCupId, previousFocus = State.focusedRequestId;
            WorldEntity assistedCup = null;
            automationServing = true;
            try
            {
                while (report.Attempts < maxDeliveries && State.requests.Count > 0)
                {
                    var request = Request(State.focusedRequestId) ?? State.requests[0];
                    bool narrative = narrativeService != null && narrativeService.Owns(request.id);
                    bool tutorial = request.id == TutorialOrderId;
                    if (!narrative && !tutorial && !State.open)
                    { report.StopReason = "Ordinary orders require open business."; break; }
                    var product = Product(request.productId);
                    string requiredCup = RequiredCupSpecId(request);
                    var source = State.entities.Find(e => e.area == AreaId.Counter && IsCupSource(Spec(e.specId))
                        && Spec(e.specId).sourceItemId == requiredCup && e.stock > 0);
                    if (product == null || Spec(requiredCup) == null || source == null)
                    { report.StopReason = "The order product or stocked cup source is unavailable; call AutomationUnlockAndRefill first."; break; }
                    assistedCup = AutomationPrepareCup(product, requiredCup, tray, narrative);
                    var classification = ClassifyCoffee(assistedCup);
                    var score = EvaluateProduction(assistedCup, product, requiredCup);
                    if (LiquidVolume(assistedCup) > LiquidCapacity(assistedCup) + .001f
                        || product.evaluation != null && (!classification.known || classification.productId != product.id)
                        || !narrative && !tutorial && score.total < product.minimumScore)
                    { report.StopReason = "The configured ideal drink does not satisfy the real classifier/evaluator; no delivery was made."; assistedCup = null; break; }

                    assistedCup.id = Allocate("free_");
                    source.stock--;
                    State.entities.Add(assistedCup);
                    State.production.activeCupId = assistedCup.id;
                    State.focusedRequestId = request.id;
                    report.Attempts++;
                    var delivery = DeliverProductionCup();
                    // Narrative requests deliberately expose only the base product. An unsweetened cup is
                    // non-destructively rejected for a sweet variant. Probe that variant through delivery,
                    // rather than reflecting into private quota state or changing narrative conditions.
                    if (!delivery.success && narrative && product.id == "blackcoffee"
                        && ReferenceEquals(Find(assistedCup.id), assistedCup) && report.Attempts < maxDeliveries)
                    {
                        assistedCup.contents.Add(new Portion { specId = "sugar", grams = 5,
                            addedSequence = ++assistedCup.production.additionSequence });
                        report.Attempts++;
                        delivery = DeliverProductionCup();
                    }
                    if (!delivery.success)
                    { report.StopReason = delivery.message; break; }
                    report.Delivered++;
                    if (narrative) report.NarrativeDeliveries++;
                    else if (tutorial) report.TutorialDeliveries++;
                    else if (delivery.feedback == CafeFeedbackKind.DeliverySatisfied) report.OrdinaryOrdersServed++;
                    assistedCup = null; // The authoritative delivery transaction consumed/recycled it.
                    if (delivery.feedback != CafeFeedbackKind.DeliverySatisfied)
                    { report.StopReason = delivery.message; break; }
                }
            }
            finally
            {
                // Cleanup only our undelivered temporary cup; never discard the player's existing drink.
                if (assistedCup != null && ReferenceEquals(Find(assistedCup.id), assistedCup))
                    RemoveCupAndRefundStock(assistedCup);
                State.production.activeCupId = Find(previousCup) != null ? previousCup : null;
                if (Request(previousFocus) != null) State.focusedRequestId = previousFocus;
                else UpdateFocusedRequest();
                report.RemainingOrders = State.requests.Count;
                report.ReachedLimit = report.Attempts >= maxDeliveries && report.RemainingOrders > 0;
                automationServing = false;
                if (report.Attempts > 0) Notify();
            }
            return report;
        }

        private WorldEntity AutomationPrepareCup(ProductSpec product, string cupSpecId, WorldEntity tray, bool narrative)
        {
            var cup = new WorldEntity { specId = cupSpecId, area = tray.area, x = tray.x, y = tray.y,
                latteArt = product.requiresLatteArt, latteArtScore = product.requiresLatteArt ? 100 : 0,
                latteArtPattern = product.requiresLatteArt ? "automation" : null };
            float scale = ProductPortionScale(product, cupSpecId);
            if (ValidProductEvaluation(product.evaluation))
            {
                foreach (var need in product.evaluation.amounts)
                {
                    cup.latteArt |= need.ingredientId == "milkfoam";
                    AutomationAddPortion(cup, product, need.ingredientId, need.coffeeKind,
                        need.idealMl * scale, need.idealGrams * scale, 0, narrative, need.minimumTemperature);
                }
            }
            else if (product.evaluation == null)
            {
                foreach (var need in product.ingredients)
                    AutomationAddPortion(cup, product, need.specId, need.coffeeKind,
                        IsLiquid(need.specId) ? (need.milliliters > 0 ? need.milliliters : need.units * Prep.portionMl) * scale : 0,
                        IsLiquid(need.specId) ? 0 : (need.grams > 0 ? need.grams : need.units * (need.specId == "sugar" ? 5 : 12)) * scale,
                        need.units, narrative, -1);
            }
            return cup;
        }

        private void AutomationAddPortion(WorldEntity cup, ProductSpec product, string id, string coffeeKind,
            float ml, float grams, int units, bool narrative, int minimumTemperature)
        {
            var portion = new Portion { specId = id, coffeeKind = coffeeKind, milliliters = ml, grams = grams,
                units = units, productionScore = 100, addedSequence = ++cup.production.additionSequence };
            float temperature = product.requiredTemperature == LiquidTemperature.Room ? Prep.ambientTemperatureCelsius
                : product.requiredTemperature == LiquidTemperature.Warm ? Prep.warmTemperatureCelsius : Prep.boilingTemperatureCelsius;
            if (minimumTemperature >= 1) temperature = Math.Max(temperature,
                minimumTemperature == 2 ? Prep.boilingTemperatureCelsius : Prep.warmTemperatureCelsius);
            // A narrative wildcard can request any.hot; explicit coffeeKind retains cold-brew identity if heated.
            if (narrative) temperature = Math.Max(40, temperature);
            SetTemperature(portion, temperature);
            if (id == "coffee")
            {
                float strength = product.evaluation?.idealCoffeeStrength ?? Catalog.compositionSettings.defaultCoffeeStrength;
                AddConstituent(portion, "water", 0, ml);
                AddConstituent(portion, "grounds", ml * strength, 0);
            }
            cup.contents.Add(portion);
        }
    }
}
