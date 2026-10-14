using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>CafeWorld 的经营循环部分：订单匹配、营业结算、顾客生成与各类经济操作。</summary>
    public sealed partial class CafeWorld
    {
        /// <summary>判断杯子是否符合餐品配方：杯型、配比、拉花要求都满足才算匹配。</summary>
        public bool Matches(WorldEntity cup, ProductSpec product) => ProductionEnabled
            ? cup != null && product != null && cup == ServingCup && (product.evaluation==null || ClassifyCoffee(cup).productId==product.id) && EvaluateProduction(cup, product).total >= product.minimumScore
                && (!Prep.requireLatteArt || !product.requiresLatteArt || cup.latteArt || State.automaticLatteArt)
            : cup != null && product != null
            && cup.specId == product.cupSpecId && Evaluate(cup, product) >= .999f
            && (!product.requiresLatteArt || cup.latteArt || State.automaticLatteArt);

        /// <summary>
        /// 给一杯饮品打分：先算配方原料的匹配比例，多放的部分按一半扣分，
        /// 杯子用错再乘 0.75；结果夹在 0~1。
        /// </summary>
        public float Evaluate(WorldEntity cup, ProductSpec product)
        {
            if (cup == null || product == null || !Spec(cup.specId).Has(Capability.Container))
            {
                return 0;
            }

            int expected = 0, matched = 0;
            foreach (var need in product.ingredients)
            {
                expected += need.units;
                int count = 0;
                foreach (var p in cup.contents)
                {
                    if (p.specId == need.specId)
                    {
                        count += p.units;
                    }
                }

                matched += Math.Min(count, need.units);
            }

            float score = Math.Max(0, (matched - Math.Max(0, TotalUnits(cup) - matched) * .5f) / expected);
            if (cup.specId != product.cupSpecId)
            {
                score *= .75f;
            }

            return Math.Min(1, score);
        }

        /// <summary>卖出价格：基础价 + 咖啡品质加成；购买了售价升级的餐品再乘倍率。</summary>
        public int SalePrice(WorldEntity cup, ProductSpec product)
        {
            // 多份咖啡取最低品质，避免高品豆混低品豆时按高品计价
            int quality = 0;
            bool coffee = false;
            foreach (var p in cup.contents)
            {
                if (p.specId == "coffee")
                {
                    if (!coffee)
                    {
                        quality = p.quality;
                    }
                    else
                    {
                        quality = Math.Min(quality, p.quality);
                    }

                    coffee = true;
                }
            }

            int price = product.price + (coffee ? quality * product.qualityPriceStep : 0);
            return (int)Math.Round(price * (State.betterProducts.Contains(product.id) ? Catalog.economy.priceUpgradeMultiplier : 1));
        }

        /// <summary>上餐结算：正确则加钱加声望；错误则扣款扣声望，顾客与杯子都消失。</summary>
        private ActionResult Serve(WorldEntity cup, WorldEntity target)
        {
            var request = RequestForTarget(target);
            if (ProductionEnabled) return ServeProduction(cup, request);
            var product = Product(request.productId);
            bool correct = Matches(cup, product);
            int price = correct ? SalePrice(cup, product) : 0;
            State.served++;
            if (correct)
            {
                State.money += price;
                State.todayRevenue += price;
                State.todayServed++;
                State.reputation += 2;
            }
            else
            {
                Charge(Catalog.economy.wrongOrderPenalty);
                State.todayFailed++;
                State.reputation--;
            }

            var customer = Find(request.customerId);
            State.entities.Remove(customer);
            State.requests.Remove(request);
            State.entities.Remove(cup);
            State.spokenCustomers.Remove(request.customerId);
            UpdateFocusedRequest();
            Notify();
            return ActionResult.Yes(correct
                ? "订单完成 +" + price + " 金币"
                : "订单失败：无奖励，扣除 " + Catalog.economy.wrongOrderPenalty + " 金币");
        }

        /// <summary>xorshift 随机数；状态存进快照，读档后随机序列可以复现。</summary>
        private uint NextRandom()
        {
            uint value = State.randomState == 0 ? 1234567u : State.randomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            State.randomState = value;
            return value;
        }

        /// <summary>按当前声望解锁达到门槛的餐品和 NPC。</summary>
        private void UnlockContent()
        {
            foreach (var product in Catalog.products)
            {
                if (State.reputation >= product.unlockReputation && !State.unlockedProducts.Contains(product.id))
                {
                    State.unlockedProducts.Add(product.id);
                }
            }

            if (Catalog.npcs != null)
            {
                foreach (var npc in Catalog.npcs)
                {
                    if (State.reputation >= npc.unlockReputation && !State.unlockedNpcs.Contains(npc.id))
                    {
                        State.unlockedNpcs.Add(npc.id);
                    }
                }
            }
        }

        /// <summary>开始营业：从夜间开业会进入下一天；重置当日统计并按客流生成首批顾客。</summary>
        public ActionResult StartDay()
        {
            if (State.open)
            {
                return ActionResult.No("已经营业");
            }

            // 从夜间开始新的一天：天数和倒计时都要重置
            if (State.businessPhase == BusinessPhase.Night)
            {
                State.day++;
                State.timeRemaining = Catalog.daySeconds;
            }

            State.open = true;
            State.businessPhase = BusinessPhase.Open;
            State.todayServed = 0;
            State.todayFailed = 0;
            State.todayRevenue = 0;
            State.todayArrivals = 0;
            State.lastSummary = null;
            State.arrivalRemaining = Catalog.customerInterval;
            State.timeRemaining = Catalog.daySeconds;

            // 今日客流 = 基础客流 + 历史服务量的成长奖励 + 0~2 的随机浮动，且不超过上限
            State.todayCustomerBudget = Math.Min(Catalog.economy.maxCustomers,
                Catalog.economy.baseCustomers
                + State.served / Math.Max(1, Catalog.economy.customerGrowthEvery)
                + (int)(NextRandom() % 3));

            UnlockContent();

            // 开场先把能坐的座位直接坐满
            int arrivals = Math.Min(State.seats, State.todayCustomerBudget);
            for (int i = 0; i < arrivals; i++)
            {
                SpawnCustomer();
            }

            Notify();
            return ActionResult.Yes("开始营业：今日客流 " + State.todayCustomerBudget + "，可自由备料；上餐时验证配方");
        }

        /// <summary>打烊结算：未完成的订单算失败，按当日成绩增减声望，并结算技能点里程碑。</summary>
        public ActionResult EndDay()
        {
            if (!State.open)
            {
                return ActionResult.No("当前没有营业");
            }

            // 还在店里的顾客全部算未完成
            State.todayFailed += State.requests.Count;
            foreach (var r in State.requests)
            {
                State.entities.Remove(Find(r.customerId));
            }

            State.requests.Clear();
            State.spokenCustomers.Clear();
            State.focusedRequestId = null;
            State.open = false;
            State.businessPhase = BusinessPhase.Night;

            // 当日不足 10 单会倒扣声望，差额就是惩罚
            int change = State.todayServed >= 10 ? State.todayServed : State.todayServed - 10;
            State.reputation += change;

            if (change > 0)
            {
                // 每累计 10 点正向声望发 1 个技能点，只补发尚未发放的部分
                State.positiveReputationEarned += change;
                int milestone = State.positiveReputationEarned / 10;
                State.skillPoints += milestone - State.rewardedSkillMilestones;
                State.rewardedSkillMilestones = milestone;
            }

            UnlockContent();
            State.lastSummary = "第 " + State.day + " 天结算：成功 " + State.todayServed
                + " / 失败或未完成 " + State.todayFailed
                + " / 收入 " + State.todayRevenue
                + " / 声望 " + (change >= 0 ? "+" : "") + change
                + "。进入夜间，可升级、招募、换楼层。";
            Notify();
            return ActionResult.Yes(State.lastSummary);
        }

        /// <summary>生成一位顾客：占用一个空卡座、挑一道已解锁餐品下单，并决定 NPC 是否出场。</summary>
        private void SpawnCustomer()
        {
            if (State.todayArrivals >= State.todayCustomerBudget)
            {
                return;
            }

            State.todayArrivals++;

            // 找第一个空座位；没有空位就扣声望并记一次失败
            int slot = -1;
            for (int i = 0; i < State.seats; i++)
            {
                string id = "customer_" + i;
                if (Find(id) == null)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                State.reputation--;
                State.todayFailed++;
                return;
            }

            // 从已解锁餐品里按订单序号轮换，保证品类分布均匀
            var eligible = new List<ProductSpec>();
            foreach (var p in Catalog.products)
            {
                if (State.unlockedProducts.Contains(p.id))
                {
                    eligible.Add(p);
                }
            }

            var product = eligible[State.requestSequence++ % eligible.Count];

            // 卡座按 4 列排布，x/y 由座位号换算而来
            var customer = new WorldEntity
            {
                id = "customer_" + slot,
                specId = "customer",
                area = AreaId.Cafe,
                x = -6 + (slot % 4) * 3.5f,
                y = .5f - (slot / 4) * 2.5f
            };

            var request = new CustomerRequest
            {
                id = Allocate("order_"),
                customerId = customer.id,
                productId = product.id,
                patience = Catalog.customerPatience
            };
            // 普通客人只下单。未来角色身份由显式注入的适配器提供，不自动触发 NCS。
            customer.person = CreateCustomerIdentity(request.id, slot);
            State.entities.Add(customer);

            // 招募「自动点单」店员后，有一定概率顾客自己完成点单
            if (State.automaticOrders && NextRandom() % 100 < (uint)(Catalog.economy.autoChance * 100))
            {
                request.automaticRemaining = Catalog.economy.autoSeconds;
            }

            State.requests.Add(request);
            UpdateFocusedRequest();
        }

        /// <summary>聚焦订单失效时，自动切到第一条订单。</summary>
        private void UpdateFocusedRequest()
        {
            if (Request(State.focusedRequestId) == null)
            {
                State.focusedRequestId = State.requests.Count > 0 ? State.requests[0].id : null;
            }
        }

        /// <summary>推进世界时间：设备加工、顾客耐心、到店节奏、自动交付与打烊判定。</summary>
        public void Tick(float seconds)
        {
            if (seconds <= 0 || !Finite(seconds))
            {
                return;
            }

            bool changed = ProductionEnabled && TickProduction(seconds);

            // 设备加工：冷却和剩余时间一起走
            foreach (var entity in State.entities)
            {
                var task = entity.task;
                if (task == null)
                {
                    continue;
                }

                task.cooldown = Math.Max(0, task.cooldown - seconds);
                if (task.phase == MachinePhase.Running)
                {
                    task.remaining = Math.Max(0, task.remaining - seconds);
                    if (task.remaining == 0)
                    {
                        FinishMachine(entity);
                        changed = true;
                    }
                }
            }

            if (State.open)
            {
                State.timeRemaining = Math.Max(0, State.timeRemaining - seconds);

                // 倒序遍历订单，方便边遍历边删除
                for (int i = State.requests.Count - 1; i >= 0; i--)
                {
                    var r = State.requests[i];
                    r.patience -= seconds;

                    // 自动点单到期：顾客自行完成，直接结算
                    if (r.automaticRemaining >= 0)
                    {
                        r.automaticRemaining -= seconds;
                        if (r.automaticRemaining <= 0)
                        {
                            var p = Product(r.productId);
                            State.money += p.price;
                            State.todayRevenue += p.price;
                            State.todayServed++;
                            State.served++;
                            State.entities.Remove(Find(r.customerId));
                            State.requests.RemoveAt(i);
                            State.spokenCustomers.Remove(r.customerId);
                            changed = true;
                            continue;
                        }
                    }

                    // 耐心耗尽：顾客离开，记一次失败
                    if (r.patience <= 0)
                    {
                        State.entities.Remove(Find(r.customerId));
                        State.requests.RemoveAt(i);
                        State.spokenCustomers.Remove(r.customerId);
                        State.todayFailed++;
                        changed = true;
                    }
                }

                // 到店节奏：一帧内可能补多位顾客，但始终受当日客流上限约束
                State.arrivalRemaining -= seconds;
                while (State.arrivalRemaining <= 0 && State.todayArrivals < State.todayCustomerBudget)
                {
                    State.arrivalRemaining += Catalog.customerInterval;
                    SpawnCustomer();
                    changed = true;
                }

                UpdateFocusedRequest();

                // 自动交付：店员从出餐口所在区域找一杯符合当前订单的饮品送出去
                if (State.automaticDelivery)
                {
                    var service = State.entities.Find(e => Spec(e.specId).Has(Capability.Delivery));
                    if (service != null)
                    {
                        WorldEntity deliver = null;
                        foreach (var e in State.entities)
                        {
                            if (e.area == service.area && !Locked(e) && Spec(e.specId).Has(Capability.Container))
                            {
                                var r = Request(State.focusedRequestId);
                                if (r != null && Matches(e, Product(r.productId)))
                                {
                                    deliver = e;
                                    break;
                                }
                            }
                        }

                        if (deliver != null)
                        {
                            Serve(deliver, service);
                            changed = true;
                        }
                    }
                }

                if (State.timeRemaining <= 0)
                {
                    EndDay();
                    return;
                }
            }

            if (changed)
            {
                Notify();
            }
        }

        /// <summary>一键补货（沿用当前豆子品质）：按固定补货费收费，补满所有来源。</summary>
        public ActionResult Refill()
        {
            int cost = 0;
            foreach (var e in State.entities)
            {
                var spec = Spec(e.specId);
                if (spec.Has(Capability.Source) && e.stock < spec.initialStock)
                {
                    cost += spec.refillCost;
                }
            }

            if (cost == 0)
            {
                return ActionResult.No("库存已满，先使用原料再补货");
            }

            if (State.money < cost)
            {
                return ActionResult.No("补货需要 " + cost + " 金币");
            }

            State.money -= cost;
            foreach (var e in State.entities)
            {
                var spec = Spec(e.specId);
                if (spec.Has(Capability.Source))
                {
                    e.stock = spec.initialStock;
                }
            }

            Notify();
            return ActionResult.Yes("补货完成 -" + cost + " 金币");
        }

        /// <summary>按指定咖啡豆品质补货：咖啡豆按品质单价计费，其他原料用单位成本或固定补货费。</summary>
        public ActionResult Refill(int beanQuality)
        {
            if (beanQuality < 0 || beanQuality > 2)
            {
                return ActionResult.No("咖啡豆品质无效");
            }

            int cost = 0;
            foreach (var e in State.entities)
            {
                var s = Spec(e.specId);
                if (s.Has(Capability.Source) && e.stock < s.initialStock)
                {
                    cost += s.sourceItemId == "beans"
                        ? Catalog.economy.beanUnitPrices[beanQuality] * (s.initialStock - e.stock)
                        : (s.unitCost > 0 ? s.unitCost * (s.initialStock - e.stock) : s.refillCost);
                }
            }

            if (cost == 0)
            {
                return ActionResult.No("库存已满，先使用原料再补货");
            }

            if (State.money < cost)
            {
                return ActionResult.No("补货需要 " + cost + " 金币");
            }

            State.money -= cost;
            State.beanQuality = beanQuality;
            foreach (var e in State.entities)
            {
                var s = Spec(e.specId);
                if (s.Has(Capability.Source) && e.stock < s.initialStock)
                {
                    e.stock = s.initialStock;

                    // 换了豆子品质，来源里的库存品质同步更新
                    if (s.sourceItemId == "beans")
                    {
                        e.quality = beanQuality;
                    }
                }
            }

            Notify();
            return ActionResult.Yes("补货完成 -" + cost + " 金币；咖啡豆品质 " + beanQuality);
        }

        /// <summary>单独购买某个来源的库存，可指定咖啡豆品质。</summary>
        public ActionResult BuySource(string sourceId, int beanQuality = -1)
        {
            var e = Find(sourceId);
            var s = e == null ? null : Spec(e.specId);
            if (s == null || !s.Has(Capability.Source))
            {
                return ActionResult.No("无效来源");
            }

            int quality = beanQuality < 0 ? State.beanQuality : beanQuality;
            if (quality < 0 || quality > 2)
            {
                return ActionResult.No("无效豆子品质");
            }

            // 只补齐缺的那部分
            int missing = s.initialStock - e.stock;
            if (missing <= 0)
            {
                return ActionResult.No("来源库存已满");
            }

            int cost = s.sourceItemId == "beans"
                ? missing * Catalog.economy.beanUnitPrices[quality]
                : (s.unitCost > 0 ? missing * s.unitCost : s.refillCost);
            if (State.money < cost)
            {
                return ActionResult.No("购买需要 " + cost + " 金币");
            }

            State.money -= cost;
            e.stock = s.initialStock;
            if (s.sourceItemId == "beans")
            {
                e.quality = quality;
                State.beanQuality = quality;
            }

            Notify();
            return ActionResult.Yes("购买 " + s.label + " -" + cost + " 金币，可继续拖出");
        }

        /// <summary>与顾客对话：聚焦其订单；首次对话可以获得 NPC 的热点词条。</summary>
        public ActionResult Talk(string customerId)
        {
            var customer = Find(customerId);
            if (customer == null || !Spec(customer.specId).Has(Capability.Customer))
            {
                return ActionResult.No("不是顾客");
            }

            var request = State.requests.Find(r => r.customerId == customerId);
            if (request != null)
            {
                State.focusedRequestId = request.id;
            }

            NpcSpec npc = null;
            if (Catalog.npcs != null)
            {
                foreach (var n in Catalog.npcs)
                {
                    if (n.id == customer.npcId)
                    {
                        npc = n;
                        break;
                    }
                }
            }

            string text = npc == null ? "顾客：请看看我的订单。" : npc.name + "：" + npc.dialogue;
            if (npc != null && !State.spokenCustomers.Contains(customerId))
            {
                State.spokenCustomers.Add(customerId);
                if (!string.IsNullOrEmpty(npc.topic) && !State.topics.Contains(npc.topic))
                {
                    State.topics.Add(npc.topic);
                    text += " · 获得热点词条：" + npc.topic;
                }
            }

            Notify();
            return ActionResult.Yes(text);
        }

        /// <summary>招募店员：解锁一种自动化能力，同时提高设备速度与备杯能力。</summary>
        public ActionResult Hire(string ability)
        {
            if (State.staff >= Catalog.economy.maxStaff)
            {
                return ActionResult.No("店员已达上限");
            }

            bool unlocked = ability == "machines" ? State.automaticMachines
                : ability == "orders" ? State.automaticOrders
                : ability == "latte" ? State.automaticLatteArt
                : ability == "cocoa" ? State.automaticCocoa
                : ability == "delivery" ? State.automaticDelivery
                : true;

            if (unlocked)
            {
                return ActionResult.No("请选择未解锁的店员能力");
            }

            if (State.money < Catalog.economy.hireCost)
            {
                return ActionResult.No("招募金币不足");
            }

            State.money -= Catalog.economy.hireCost;
            State.staff++;
            switch (ability)
            {
                case "machines":
                    State.automaticMachines = true;
                    break;
                case "orders":
                    State.automaticOrders = true;
                    break;
                case "latte":
                    State.automaticLatteArt = true;
                    break;
                case "cocoa":
                    State.automaticCocoa = true;
                    break;
                case "delivery":
                    State.automaticDelivery = true;
                    break;
            }

            Notify();
            return ActionResult.Yes("店员已招募，解锁 " + ability + "；设备速度与备杯能力提高");
        }

        /// <summary>扩建卡座，初版上限 8 个。</summary>
        public ActionResult Expand()
        {
            if (State.seats >= 8)
            {
                return ActionResult.No("初版卡座上限 8");
            }

            if (State.money < Catalog.economy.expansionCost)
            {
                return ActionResult.No("扩建金币不足");
            }

            State.money -= Catalog.economy.expansionCost;
            State.seats = Math.Min(8, State.seats + Catalog.economy.seatsPerExpansion);
            Notify();
            return ActionResult.Yes("卡座容量提高到 " + State.seats);
        }

        /// <summary>升级设备效率：skill=true 时消耗技能点，否则花金币。</summary>
        public ActionResult UpgradeMachine(string specId, bool skill = false)
        {
            if (Spec(specId) == null || !Spec(specId).Has(Capability.Processor))
            {
                return ActionResult.No("无效设备");
            }

            var list = skill ? State.skillMachines : State.fasterMachines;
            if (list.Contains(specId))
            {
                return ActionResult.No("已购买这项升级");
            }

            if (skill ? State.skillPoints < 1 : State.money < Catalog.economy.upgradeCost)
            {
                return ActionResult.No("升级资源不足");
            }

            if (skill)
            {
                State.skillPoints--;
            }
            else
            {
                State.money -= Catalog.economy.upgradeCost;
            }

            list.Add(specId);
            Notify();
            return ActionResult.Yes("设备效率升级（下次加工生效）");
        }

        /// <summary>升级餐品售价，每项只能升一次，提升 50%。</summary>
        public ActionResult UpgradeProduct(string productId)
        {
            if (Product(productId) == null || !State.unlockedProducts.Contains(productId))
            {
                return ActionResult.No("餐品尚未解锁");
            }

            if (State.betterProducts.Contains(productId))
            {
                return ActionResult.No("售价已升级");
            }

            if (State.money < Catalog.economy.upgradeCost)
            {
                return ActionResult.No("升级金币不足");
            }

            State.money -= Catalog.economy.upgradeCost;
            State.betterProducts.Add(productId);
            Notify();
            return ActionResult.Yes("餐品售价提高 50%");
        }

        /// <summary>购买一台新设备：数量上限为 1 + 店员数，且不能超过配置上限。</summary>
        public ActionResult AddMachine(string specId)
        {
            var spec = Spec(specId);
            if (spec == null || !spec.Has(Capability.Processor))
            {
                return ActionResult.No("无效设备");
            }

            int count = State.entities.FindAll(e => e.specId == specId).Count;
            if (count >= 1 + State.staff || count >= Catalog.economy.maxMachineCopies)
            {
                return ActionResult.No("先招募店员增加设备数量上限");
            }

            if (State.money < Catalog.economy.extraMachineCost)
            {
                return ActionResult.No("设备购买金币不足");
            }

            // 新设备在原设备右下方向错开摆放，避免完全重叠
            var origin = State.entities.Find(e => e.specId == specId);
            float x = Math.Max(-8, Math.Min(8, origin.x + .4f * count));
            float y = origin.y - 2 * count;
            var valid = CanPosition(specId, origin.area, x, y);
            if (!valid.success)
            {
                return valid;
            }

            State.money -= Catalog.economy.extraMachineCost;
            State.entities.Add(new WorldEntity { id = Allocate("free_"), specId = specId, area = origin.area, x = x, y = y });
            Notify();
            return ActionResult.Yes("新增设备，可独立并行加工");
        }
    }
}