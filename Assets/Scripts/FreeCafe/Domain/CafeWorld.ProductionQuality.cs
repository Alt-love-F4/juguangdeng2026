using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public ProductionQuality EvaluateProduction(WorldEntity cup, ProductSpec product)
        {
            if (cup == null || product == null || cup.contents.Count == 0) return new ProductionQuality(0, 0, 0, 0, 0);
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
                    if (!SameIngredient(p.specId, need.specId)) continue;
                    ml += p.milliliters; units += p.grams > 0 ? p.grams / (p.specId == "sugar" ? 5 : 12) : p.units;
                    temperature += p.milliliters * (100 - Math.Abs((int)p.temperature - (int)product.requiredTemperature) * 35);
                }
                if (IsLiquid(need.specId))
                {
                    float ideal = need.milliliters > 0 ? need.milliliters : need.units * Prep.portionMl;
                    float quantityScore = Clamp(100 - Math.Abs(ml - ideal) / ideal * 100);
                    liquidScore += ml > 0 ? (quantityScore + temperature / ml) / 2 : 0;
                    liquidCount++; coverage += Math.Min(1, ml / ideal);
                }
                else coverage += Math.Min(1, (float)units / need.units);
                if (need.specId == "coffee") needsCoffee = true;
            }
            coverage /= product.ingredients.Length;
            float extraction = needsCoffee ? coffeeMl > 0 ? extractionSum / coffeeMl : 0 : 100;
            float liquid = liquidCount > 0 ? liquidScore / liquidCount : 100;
            int latestLiquid = 0;
            foreach (var p in cup.contents) if (p.milliliters > 0) latestLiquid = Math.Max(latestLiquid, p.addedSequence);
            float order = 100;
            if (!string.IsNullOrEmpty(product.preferredLatteArtPattern))
                order -= string.Equals(cup.latteArtPattern, product.preferredLatteArtPattern, StringComparison.OrdinalIgnoreCase) ? 0 : 25;
            float sweet = Clamp(100 - Math.Abs(sweetness - product.idealSweetness) * 20);
            float ratio = totalMl > 0 ? coffeeMl / totalMl : 0;
            float grounds = 0;
            foreach (var portion in cup.contents) if (portion.specId == "grounds") grounds += portion.grams;
            float strength = grounds > 0 && coffeeMl > 0 ? Clamp(100 - Math.Abs(grounds / coffeeMl - .06f) / .06f * 100) : 100;
            float concentration = Clamp(100 - Math.Abs(ratio - product.idealCoffeeRatio) * 150) * strength / 100;
            // 杯型、配方完整度和多余原料是独立惩罚，空杯/半成品允许出品但不会获得虚假高分。
            float penalty = cup.specId == product.cupSpecId ? 1 : .75f;
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
                return ActionResult.No("交付区没有杯子，请先把杯子拖到方框内再按铃");
            if(Request(State.focusedRequestId)==null) return ActionResult.No("当前没有可交付的订单");
            return ServeProduction(cup, Request(State.focusedRequestId));
        }
        private ActionResult ServeProduction(WorldEntity cup, CustomerRequest request)
        {
            if (!ProductionEnabled || cup == null || request == null || !Spec(cup.specId).Has(Capability.Container))
                return ActionResult.No("需要出餐杯和当前显示的订单");
            if (!string.IsNullOrEmpty(State.production.miniGameToken)) return ActionResult.No("请先结束或取消小游戏");
            var product = Product(request.productId); var score = EvaluateProduction(cup, product);
            bool satisfied = score.total >= product.minimumScore
                && (!Prep.requireLatteArt || !product.requiresLatteArt || cup.latteArt || State.automaticLatteArt);
            request.lastProductionScore = score.total;
            string detail = score.grade + " " + score.total.ToString("0.0") + "分 · 萃取 " + score.extraction.ToString("0")
                + " / 液体 " + score.liquid.ToString("0") + " / 顺序 " + score.order.ToString("0")
                + " / 甜度 " + score.sweetness.ToString("0") + " / 浓度 " + score.concentration.ToString("0");
            State.entities.Remove(cup);
            if (State.production.activeCupId == cup.id) State.production.activeCupId = null;
            if (!satisfied && request.remakes < product.maxRemakes)
            {
                request.remakes++; State.production.lastDelivery = "顾客要求重做（" + request.remakes + "/" + product.maxRemakes + "）· " + detail;
                Notify(); return ActionResult.Yes(State.production.lastDelivery);
            }
            State.served++;
            if (satisfied)
            {
                int price = SalePrice(cup, product); State.money += price; State.todayRevenue += price;
                State.todayServed++; State.reputation += 2;
                State.production.lastDelivery = "订单完成 +" + price + " 金币 · " + detail;
            }
            else
            {
                State.todayFailed++; State.reputation--; Charge(Catalog.economy.wrongOrderPenalty);
                State.production.lastDelivery = "顾客不满意离开 · " + detail;
            }
            CompleteCustomerOrder(request, satisfied);
            UpdateFocusedRequest(); Notify();
            return ActionResult.Yes(State.production.lastDelivery);
        }
        public ActionResult DiscardProductionCup()
        {
            var cup = ServingCup; if (cup == null) return ActionResult.No("托盘为空");
            CancelProductionMiniGame(); State.entities.Remove(cup); State.production.activeCupId = null;
            Notify(); return ActionResult.Yes("出餐杯已丢弃，消耗的资源不返还");
        }
        public ActionResult EmptyProductionMachine(string id)
        {
            var machine = Find(id);
            if ((!IsProductionMachine(machine) && !IsContainer(machine)) || machine.contents.Count == 0) return ActionResult.No("没有可清空的设备内容");
            machine.contents.Clear(); machine.production = new ProductionEntityState();
            Notify(); return ActionResult.Yes("设备内容已丢弃，资源不返还");
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
            // 拉花是杯子在出餐点之后的独立玩法：不再要求杯中先有咖啡或牛奶。
            if (!ProductionEnabled || kind != "latte-art" || cup == null || Locked(cup))
                return ActionResult.No("请先在出餐点选择一只可用的杯子");
            if (!string.IsNullOrEmpty(State.production.miniGameToken)) return ActionResult.No("已有小游戏进行中");
            State.production.miniGameToken = Allocate("minigame_"); State.production.miniGameCupId = cup.id;
            State.production.miniGameKind = kind; Notify(); return ActionResult.Yes("已进入拉花：拖动鼠标绘制图案，完成后提交");
        }
        public ActionResult ResolveProductionMiniGame(string token, bool completed, float score) => ResolveProductionMiniGame(token, completed, score, null);
        public ActionResult ResolveProductionMiniGame(string token, bool completed, float score, string pattern)
        {
            if (!ProductionEnabled || string.IsNullOrEmpty(token) || token != State.production.miniGameToken
                || !Finite(score) || score < 0 || score > 100) return ActionResult.No("过期或无效的小游戏回调");
            var cup = Find(State.production.miniGameCupId);
            if (cup == null || cup != ServingCup) { CancelProductionMiniGame(); return ActionResult.No("目标杯已变化"); }
            cup.latteArt = completed && score >= 75;
            cup.latteArtScore = completed ? score : 0;
            cup.latteArtPattern = completed ? (string.IsNullOrEmpty(pattern) ? "unknown" : pattern) : null;
            CancelProductionMiniGame(); Notify(); return ActionResult.Yes(completed ? "拉花已完成 · " + (cup.latteArtPattern ?? "未识别") + " · " + score.ToString("0") + "分" : "已取消拉花，不改变杯中内容");
        }
        public void CancelProductionMiniGame()
        {
            if (!ProductionEnabled) return;
            State.production.miniGameToken = State.production.miniGameCupId = State.production.miniGameKind = null;
        }
    }
}
