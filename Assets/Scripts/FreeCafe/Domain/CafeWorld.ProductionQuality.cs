using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public ProductionQuality EvaluateProduction(WorldEntity cup, ProductSpec product, string requiredCupSpecId = null)
        {
            if (cup == null || product == null || cup.contents.Count == 0) return new ProductionQuality(0, 0, 0, 0, 0);
            if(product.evaluation!=null)
            {
                var evaluated=EvaluateProductComposition(cup,product,requiredCupSpecId);
                return new ProductionQuality(evaluated.total,evaluated.total,evaluated.total,evaluated.total,evaluated.total);
            }
            float portionScale = ProductPortionScale(product, requiredCupSpecId);
            float totalMl = LiquidVolume(cup), coffeeMl = 0, extractionSum = 0, liquidScore = 0, coverage = 0;
            int liquidCount = 0, coffeeOrder = 0, firstLiquid = int.MaxValue, firstTopping = int.MaxValue;
            float sweetness = 0;
            foreach (var p in cup.contents)
            {
                if (p.specId == "coffee") { coffeeMl += p.milliliters; extractionSum += p.productionScore * p.milliliters; coffeeOrder = Math.Max(coffeeOrder, p.addedSequence); }
                else if (p.milliliters > 0) firstLiquid = Math.Min(firstLiquid, p.addedSequence);
                if (p.specId == "honey" || p.specId == "cream") firstTopping = Math.Min(firstTopping, p.addedSequence);
                if (p.specId == "sugar") sweetness += p.grams > 0 ? p.grams / 5 : p.units;
                if (p.specId == "honey") sweetness += (p.grams > 0 ? p.grams / 12 : p.units) * 2;
            }
            bool needsCoffee = false;
            foreach (var need in product.ingredients)
            {
                float ml = 0; float units = 0; float temperature = 0;
                foreach (var p in cup.contents)
                {
                    if (!RecipeIngredientMatches(cup, p, need.specId)) continue;
                    ml += p.milliliters; units += p.grams > 0 ? p.grams / (p.specId == "sugar" ? 5 : 12) : p.units;
                    temperature += p.milliliters * (100 - Math.Abs((int)TemperatureBand(TemperatureCelsius(p)) - (int)product.requiredTemperature) * 35);
                }
                if (IsLiquid(need.specId))
                {
                    float ideal = (need.milliliters > 0 ? need.milliliters : need.units * Prep.portionMl) * portionScale;
                    float quantityScore = Clamp(100 - Math.Abs(ml - ideal) / ideal * 100);
                    liquidScore += ml > 0 ? (quantityScore + temperature / ml) / 2 : 0;
                    liquidCount++; coverage += Math.Min(1, ml / ideal);
                }
                else coverage += Math.Min(1, (float)units / (need.units * portionScale));
                if (need.specId == "coffee") needsCoffee = true;
            }
            coverage /= product.ingredients.Length;
            float extraction = needsCoffee ? coffeeMl > 0 ? extractionSum / coffeeMl : 0 : 100;
            float liquid = liquidCount > 0 ? liquidScore / liquidCount : 100;
            int latestLiquid = 0;
            foreach (var p in cup.contents) if (p.milliliters > 0) latestLiquid = Math.Max(latestLiquid, p.addedSequence);
            float order = 100; // Latte-art kind requires completion, never a particular pattern or recognizer score.
            float sweet = Clamp(100 - Math.Abs(sweetness - product.idealSweetness * portionScale) * 20);
            float ratio = totalMl > 0 ? coffeeMl / totalMl : 0;
            float grounds = 0;
            foreach (var portion in cup.contents) if (portion.specId == "grounds") grounds += portion.grams;
            float strength = grounds > 0 && coffeeMl > 0 ? Clamp(100 - Math.Abs(grounds / coffeeMl - .06f) / .06f * 100) : 100;
            float concentration = Clamp(100 - Math.Abs(ratio - product.idealCoffeeRatio) * 150) * strength / 100;
            // 杯型、配方完整度和多余原料是独立惩罚，空杯/半成品允许出品但不会获得虚假高分。
            float penalty = cup.specId == (requiredCupSpecId ?? product.cupSpecId) ? 1 : .75f;
            foreach (var p in cup.contents)
            {
                bool expected = p.specId == "sugar" || p.specId == "grounds" && needsCoffee
                    || p.specId == "cocoa.powder" && cup.contents.Exists(portion => portion.specId == "cocoa.drink")
                    || p.specId == "matcha.powder" && cup.contents.Exists(portion => portion.specId == "matcha.drink");
                foreach (var need in product.ingredients) if (SameIngredient(p.specId, need.specId)) expected = true;
                if (!expected) penalty *= .8f;
            }
            float scale = Clamp(coverage, 0, 1) * penalty;
            return new ProductionQuality(extraction * scale, liquid * scale, order * scale, sweet * scale, concentration * scale);
        }
        private bool SameIngredient(string actual, string expected) => actual == expected
            || (actual == "milk" || actual == "hotmilk") && (expected == "milk" || expected == "hotmilk")
            || (actual == "water" || actual == "hotwater") && (expected == "water" || expected == "hotwater");

        public ActionResult DeliverProductionCup()
        {
            var cup=ServingCup;
            var tray=State.entities.Find(e=>e.area==AreaId.Counter && Spec(e.specId).Has(Capability.Delivery));
            if(cup==null || tray==null || cup.area!=tray.area || Math.Abs(cup.x-tray.x)>.1f || Math.Abs(cup.y-tray.y)>.1f)
                return ActionResult.No("盘子里没有杯子，请先把咖啡拖到案板上的盘子里，再点击右下角按铃");
            if (CupAbove(cup) != null || !string.IsNullOrEmpty(cup.cupStackBelowId)) return ActionResult.No("请先拆开杯堆再交付");
            if(Request(State.focusedRequestId)==null) return ActionResult.No("当前没有可交付的订单");
            return ServeProduction(cup, Request(State.focusedRequestId));
        }
        private ActionResult ServeProduction(WorldEntity cup, CustomerRequest request)
        {
            if (!ProductionEnabled || cup == null || request == null || !Spec(cup.specId).Has(Capability.Container))
                return ActionResult.No("需要出餐杯和当前显示的订单");
            if (!string.IsNullOrEmpty(State.production.miniGameToken)) return ActionResult.No("请先结束或取消小游戏");
            if (request.id == TutorialOrderId)
            {
                var tutorialCoffee = ClassifyCoffee(cup);
                if (!tutorialCoffee.known || tutorialCoffee.productId != "blackcoffee" || cup.specId != RequiredCupSpecId(request))
                    return ActionResult.No("请使用大杯制作一杯黑咖啡再交付；当前杯子和教程订单已保留");
                ConsumeDeliveredContainer(cup);
                CompleteCustomerOrder(request, true);
                State.production.lastDelivery = "首位客人的黑咖啡已交付，接下来学习查咖啡谱和正式营业";
                Notify();
                return ActionResult.Yes(State.production.lastDelivery, CafeFeedbackKind.DeliverySatisfied);
            }
            string requiredCup = RequiredCupSpecId(request);
            var product = Product(request.productId); var score = EvaluateProduction(cup, product, requiredCup);
            var classification=ClassifyCoffee(cup);
            bool correctType=product.evaluation==null || classification.known && classification.productId==product.id;
            bool satisfied = cup.specId == requiredCup && correctType && score.total >= product.minimumScore
                && (!Prep.requireLatteArt || !product.requiresLatteArt || cup.latteArt || State.automaticLatteArt);
            request.lastProductionScore = score.total;
            string detail = "杯型：" + Spec(requiredCup).label + (cup.specId == requiredCup ? "（符合）" : "（不符）") + " · 种类："+classification.label+" · "+(product.evaluation!=null ? EvaluateProductComposition(cup,product,requiredCup).description : score.grade + " " + score.total.ToString("0.0") + "分 · 萃取 " + score.extraction.ToString("0")
                + " / 液体 " + score.liquid.ToString("0") + " / 顺序 " + score.order.ToString("0")
                + " / 甜度 " + score.sweetness.ToString("0") + " / 浓度 " + score.concentration.ToString("0"));
            int price = satisfied ? SalePrice(cup, product) : 0;
            ConsumeDeliveredContainer(cup);
            if (!satisfied && request.remakes < product.maxRemakes)
            {
                request.remakes++; State.production.lastDelivery = "顾客要求重做（" + request.remakes + "/" + product.maxRemakes + "）· " + detail;
                Notify(); return ActionResult.Yes(State.production.lastDelivery, CafeFeedbackKind.DeliveryRemake);
            }
            State.served++;
            if (satisfied)
            {
                State.money += price; State.todayRevenue += price;
                State.todayServed++; State.successfulOrders++; State.reputation += 2;
                State.production.lastDelivery = "订单完成 +" + price + " 金币 · " + detail;
            }
            else
            {
                State.todayFailed++; State.reputation--; Charge(Catalog.economy.wrongOrderPenalty);
                State.production.lastDelivery = "顾客不满意离开 · " + detail;
            }
            CompleteCustomerOrder(request, satisfied);
            UpdateFocusedRequest(); Notify();
            return ActionResult.Yes(State.production.lastDelivery, satisfied ? CafeFeedbackKind.DeliverySatisfied : CafeFeedbackKind.DeliveryFailed);
        }
        private void ConsumeDeliveredContainer(WorldEntity container)
        {
            if (State.production.activeCupId == container.id) State.production.activeCupId = null;
            foreach (var entity in State.entities)
                if (entity.production != null && entity.production.heatingVesselId == container.id)
                    entity.production.heatingVesselId = null;
            var home = Array.Find(Catalog.layout, entry => entry.id == container.id && entry.specId == container.specId);
            bool reusable = home != null && IsContainer(container) &&
                (IsMilkPitcher(container) || Spec(container.specId).Has(Capability.Processor) || Spec(container.specId).Has(Capability.HotExtraction));
            if (!reusable) { RemoveCupAndRefundStock(container); return; }
            var filter = ExtractionFilter(container);
            if (filter != null)
            {
                DetachExtractionFilter(filter); filter.x = container.x + .9f; filter.y = container.y - .75f;
            }
            container.contents.Clear(); container.production = new ProductionEntityState { extractionAssemblyInitialized = IsPrecisionExtractor(container) }; container.task = null;
            container.ownerId = null; container.latteArt = false; container.latteArtScore = 0; container.latteArtPattern = null;
            container.area = home.area; container.x = home.x; container.y = home.y; container.rotation = 0;
        }
        public ActionResult DiscardProductionCup()
        {
            var cup = ServingCup; if (cup == null) return ActionResult.No("托盘为空");
            CancelProductionMiniGame(); ConsumeDeliveredContainer(cup);
            Notify(); return ActionResult.Yes(IsCup(cup) ? "出餐杯已回收至对应杯堆，饮品原料不返还" : "容器已归位，饮品原料不返还", CafeFeedbackKind.Discard);
        }
        public ActionResult EmptyProductionMachine(string id)
        {
            var machine = Find(id);
            if (IsInfiniteLiquidContainer(machine)) return ActionResult.No("清水壶持续供水，无需清空");
            if (MiniGameUses(machine)) return ActionResult.No("请先结束拉花，再清空容器");
            var filter = ExtractionFilter(machine);
            if ((!IsProductionMachine(machine) && !IsContainer(machine)) || machine.contents.Count == 0 && (filter == null || filter.contents.Count == 0))
                return ActionResult.No("没有可清空的设备内容");
            machine.contents.Clear();
            if (filter != null)
            {
                filter.contents.Clear(); filter.production.filterSpent = false; filter.production.filterDrainHold = 0;
                filter.production.filterReferenceGroundsGrams=0;filter.production.filterWaterPassedMl=0;filter.production.filterDrainedMl=0;
            }
            machine.production = new ProductionEntityState { extractionFilterId = filter?.id,
                extractionJugId = machine.production.extractionJugId, extractionAssemblyInitialized = IsPrecisionExtractor(machine) };
            Notify(); return ActionResult.Yes("设备内容已丢弃，资源不返还", CafeFeedbackKind.Empty);
        }
        public ActionResult PageProductionOrder(int direction)
        {
            if (State.requests.Count == 0) return ActionResult.No("没有订单");
            int index = State.requests.FindIndex(r => r.id == State.focusedRequestId);
            index = (Math.Max(0, index) + (direction < 0 ? -1 : 1) + State.requests.Count) % State.requests.Count;
            Focus(State.requests[index].id); return ActionResult.Yes("已切换交付订单");
        }

        public ActionResult RequestProductionMiniGame(string kind = "latte-art")
        {
            var cup = ServingCup;
            if (!LatteEquipmentAvailable) return ActionResult.No("请先在设备升级面板激活拉花");
            // 拉花是杯子在出餐点之后的独立玩法：不再要求杯中先有咖啡或牛奶。
            if (!ProductionEnabled || kind != "latte-art" || cup == null || Locked(cup))
                return ActionResult.No("请先在出餐点选择一只可用的杯子");
            if (!string.IsNullOrEmpty(State.production.miniGameToken)) return ActionResult.No("已有小游戏进行中");
            State.production.miniGameToken = Allocate("minigame_"); State.production.miniGameCupId = cup.id;
            State.production.miniGamePitcherId = null; State.production.miniGameFoamUsedMl = 0;
            State.production.miniGameKind = kind; Notify(); return ActionResult.Yes("已进入拉花：拖动鼠标绘制图案，完成后提交");
        }
        public ActionResult ResolveProductionMiniGame(string token, bool completed, float score) => ResolveProductionMiniGame(token, completed, score, null);
        public ActionResult ResolveProductionMiniGame(string token, bool completed, float score, string pattern)
        {
            if (!ProductionEnabled || string.IsNullOrEmpty(token) || token != State.production.miniGameToken
                || !Finite(score) || score < 0 || score > 100) return ActionResult.No("过期或无效的小游戏回调");
            var cup = Find(State.production.miniGameCupId);
            bool fromPitcher = !string.IsNullOrEmpty(State.production.miniGamePitcherId);
            if (cup == null || !fromPitcher && cup != ServingCup) { CancelProductionMiniGame(); return ActionResult.No("目标杯已变化"); }
            if (completed && fromPitcher && State.production.miniGameFoamUsedMl <= 0) return ActionResult.No("尚未使用奶泡，不能提交拉花");
            float used = State.production.miniGameFoamUsedMl;
            if (!completed) { CancelProductionMiniGame(); Notify(); return ActionResult.Yes("已返回吧台 · 已用奶泡 " + used.ToString("0.0") + " ml 保留在杯中，不返还"); }
            cup.latteArt = true; // Completion is independent of recognizer score and pattern.
            cup.latteArtScore = score;
            cup.latteArtPattern = string.IsNullOrEmpty(pattern) ? "unknown" : pattern;
            CancelProductionMiniGame(); Notify(); return ActionResult.Yes("拉花已完成 · " + cup.latteArtPattern + " · " + score.ToString("0") + "分", CafeFeedbackKind.LatteComplete);
        }
        public void CancelProductionMiniGame()
        {
            if (!ProductionEnabled) return;
            var cup = Find(State.production.miniGameCupId);
            string token = State.production.miniGameToken;
            if (cup != null && !string.IsNullOrEmpty(token))
            {
                foreach (var portion in cup.contents)
                    if (portion.latteArtSessionToken == token) portion.latteArtSessionToken = null;
                MergeContainerContents(cup); // Kept foam becomes ordinary inventory after completion/cancellation.
            }
            State.production.miniGameToken = State.production.miniGameCupId = State.production.miniGameKind = null;
            State.production.miniGamePitcherId = null; State.production.miniGameFoamUsedMl = 0;
        }
    }
}
