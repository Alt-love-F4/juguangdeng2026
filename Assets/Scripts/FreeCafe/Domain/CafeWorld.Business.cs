using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public bool Matches(WorldEntity cup,ProductSpec product)=>cup!=null && product!=null && cup.specId==product.cupSpecId && Evaluate(cup,product)>=.999f && (!product.requiresLatteArt || cup.latteArt || State.automaticLatteArt);
        public float Evaluate(WorldEntity cup,ProductSpec product)
        {
            if(cup==null || product==null || !Spec(cup.specId).Has(Capability.Container)) return 0;
            int expected=0,matched=0;
            foreach(var need in product.ingredients){expected+=need.units;int count=0;foreach(var p in cup.contents) if(p.specId==need.specId) count+=p.units;matched+=Math.Min(count,need.units);}
            float score=Math.Max(0,(matched-Math.Max(0,TotalUnits(cup)-matched)*.5f)/expected);
            if(cup.specId!=product.cupSpecId) score*=.75f;return Math.Min(1,score);
        }
        public int SalePrice(WorldEntity cup,ProductSpec product)
        {
            int quality=0;bool coffee=false;
            foreach(var p in cup.contents) if(p.specId=="coffee"){if(!coffee) quality=p.quality;else quality=Math.Min(quality,p.quality);coffee=true;}
            int price=product.price+(coffee ? quality*product.qualityPriceStep : 0);
            return (int)Math.Round(price*(State.betterProducts.Contains(product.id) ? Catalog.economy.priceUpgradeMultiplier : 1));
        }
        private ActionResult Serve(WorldEntity cup,WorldEntity target)
        {
            var request=RequestForTarget(target);var product=Product(request.productId);bool correct=Matches(cup,product);
            int price=correct ? SalePrice(cup,product) : 0;
            State.served++;
            if(correct){State.money+=price;State.todayRevenue+=price;State.todayServed++;State.reputation+=2;}
            else {Charge(Catalog.economy.wrongOrderPenalty);State.todayFailed++;State.reputation--;}
            var customer=Find(request.customerId);State.entities.Remove(customer);State.requests.Remove(request);State.entities.Remove(cup);
            State.spokenCustomers.Remove(request.customerId);UpdateFocusedRequest();Notify();
            return ActionResult.Yes(correct ? "订单完成 +"+price+" 金币" : "订单失败：无奖励，扣除 "+Catalog.economy.wrongOrderPenalty+" 金币");
        }
        private uint NextRandom(){uint value=State.randomState==0 ? 1234567u : State.randomState;value^=value<<13;value^=value>>17;value^=value<<5;State.randomState=value;return value;}
        private void UnlockContent()
        {
            foreach(var product in Catalog.products) if(State.reputation>=product.unlockReputation && !State.unlockedProducts.Contains(product.id)) State.unlockedProducts.Add(product.id);
            if(Catalog.npcs!=null) foreach(var npc in Catalog.npcs) if(State.reputation>=npc.unlockReputation && !State.unlockedNpcs.Contains(npc.id)) State.unlockedNpcs.Add(npc.id);
        }
        public ActionResult StartDay()
        {
            if(State.open) return ActionResult.No("已经营业");
            if(State.businessPhase==BusinessPhase.Night){State.day++;State.timeRemaining=Catalog.daySeconds;}
            State.open=true;State.businessPhase=BusinessPhase.Open;State.todayServed=State.todayFailed=State.todayRevenue=State.todayArrivals=0;
            State.lastSummary=null;State.arrivalRemaining=Catalog.customerInterval;State.timeRemaining=Catalog.daySeconds;
            State.todayCustomerBudget=Math.Min(Catalog.economy.maxCustomers,Catalog.economy.baseCustomers+State.served/Math.Max(1,Catalog.economy.customerGrowthEvery)+(int)(NextRandom()%3));
            UnlockContent();int arrivals=Math.Min(State.seats,State.todayCustomerBudget);for(int i=0;i<arrivals;i++) SpawnCustomer();
            Notify();return ActionResult.Yes("开始营业：今日客流 "+State.todayCustomerBudget+"，可自由备料；上餐时验证配方");
        }
        public ActionResult EndDay()
        {
            if(!State.open) return ActionResult.No("当前没有营业");
            State.todayFailed+=State.requests.Count;
            foreach(var r in State.requests) State.entities.Remove(Find(r.customerId));State.requests.Clear();State.spokenCustomers.Clear();State.focusedRequestId=null;
            State.open=false;State.businessPhase=BusinessPhase.Night;
            int change=State.todayServed>=10 ? State.todayServed : State.todayServed-10;
            State.reputation+=change;
            if(change>0){State.positiveReputationEarned+=change;int milestone=State.positiveReputationEarned/10;State.skillPoints+=milestone-State.rewardedSkillMilestones;State.rewardedSkillMilestones=milestone;}
            UnlockContent();State.lastSummary="第 "+State.day+" 天结算：成功 "+State.todayServed+" / 失败或未完成 "+State.todayFailed+" / 收入 "+State.todayRevenue+" / 声望 "+(change>=0 ? "+" : "")+change+"。进入夜间，可升级、招募、换楼层。";
            Notify();return ActionResult.Yes(State.lastSummary);
        }
        private void SpawnCustomer()
        {
            if(State.todayArrivals>=State.todayCustomerBudget) return;State.todayArrivals++;
            int slot=-1;for(int i=0;i<State.seats;i++){string id="customer_"+i;if(Find(id)==null){slot=i;break;}}
            if(slot<0){State.reputation--;State.todayFailed++;return;}
            var eligible=new List<ProductSpec>();foreach(var p in Catalog.products) if(State.unlockedProducts.Contains(p.id)) eligible.Add(p);
            var product=eligible[State.requestSequence++%eligible.Count];
            var customer=new WorldEntity{id="customer_"+slot,specId="customer",area=AreaId.Cafe,x=-6+(slot%4)*3.5f,y=.5f-(slot/4)*2.5f};
            if(Catalog.npcs!=null){var candidates=new List<NpcSpec>();foreach(var n in Catalog.npcs) if(State.unlockedNpcs.Contains(n.id) && State.beanQuality>=n.minimumBeanQuality) candidates.Add(n);if(candidates.Count>0) customer.npcId=candidates[(State.requestSequence-1)%candidates.Count].id;}
            State.entities.Add(customer);
            var request=new CustomerRequest{id=Allocate("order_"),customerId=customer.id,productId=product.id,patience=Catalog.customerPatience};
            if(State.automaticOrders && NextRandom()%100<(uint)(Catalog.economy.autoChance*100)) request.automaticRemaining=Catalog.economy.autoSeconds;
            State.requests.Add(request);UpdateFocusedRequest();
        }
        private void UpdateFocusedRequest(){if(Request(State.focusedRequestId)==null) State.focusedRequestId=State.requests.Count>0 ? State.requests[0].id : null;}
        public void Tick(float seconds)
        {
            if(seconds<=0 || !Finite(seconds)) return;bool changed=false;
            foreach(var entity in State.entities)
            {
                var task=entity.task;if(task==null) continue;task.cooldown=Math.Max(0,task.cooldown-seconds);
                if(task.phase==MachinePhase.Running){task.remaining=Math.Max(0,task.remaining-seconds);if(task.remaining==0){FinishMachine(entity);changed=true;}}
            }
            if(State.open)
            {
                State.timeRemaining=Math.Max(0,State.timeRemaining-seconds);
                for(int i=State.requests.Count-1;i>=0;i--)
                {
                    var r=State.requests[i];r.patience-=seconds;
                    if(r.automaticRemaining>=0){r.automaticRemaining-=seconds;if(r.automaticRemaining<=0){var p=Product(r.productId);State.money+=p.price;State.todayRevenue+=p.price;State.todayServed++;State.served++;State.entities.Remove(Find(r.customerId));State.requests.RemoveAt(i);State.spokenCustomers.Remove(r.customerId);changed=true;continue;}}
                    if(r.patience<=0){State.entities.Remove(Find(r.customerId));State.requests.RemoveAt(i);State.spokenCustomers.Remove(r.customerId);State.todayFailed++;changed=true;}
                }
                State.arrivalRemaining-=seconds;
                // Bounded by today's budget even for a large simulation delta.
                while(State.arrivalRemaining<=0 && State.todayArrivals<State.todayCustomerBudget){State.arrivalRemaining+=Catalog.customerInterval;SpawnCustomer();changed=true;}
                UpdateFocusedRequest();
                if(State.automaticDelivery){var service=State.entities.Find(e=>Spec(e.specId).Has(Capability.Delivery));if(service!=null){WorldEntity deliver=null;foreach(var e in State.entities) if(e.area==service.area && !Locked(e) && Spec(e.specId).Has(Capability.Container)){var r=Request(State.focusedRequestId);if(r!=null && Matches(e,Product(r.productId))){deliver=e;break;}}if(deliver!=null){Serve(deliver,service);changed=true;}}}
                if(State.timeRemaining<=0){EndDay();return;}
            }
            if(changed) Notify();
        }
        public ActionResult Refill()
        {
            int cost=0;foreach(var e in State.entities){var spec=Spec(e.specId);if(spec.Has(Capability.Source) && e.stock<spec.initialStock) cost+=spec.refillCost;}
            if(cost==0) return ActionResult.No("库存已满，先使用原料再补货");if(State.money<cost) return ActionResult.No("补货需要 "+cost+" 金币");
            State.money-=cost;foreach(var e in State.entities){var spec=Spec(e.specId);if(spec.Has(Capability.Source)) e.stock=spec.initialStock;}
            Notify();return ActionResult.Yes("补货完成 -"+cost+" 金币");
        }
        public ActionResult Refill(int beanQuality)
        {
            if(beanQuality<0 || beanQuality>2) return ActionResult.No("咖啡豆品质无效");int cost=0;
            foreach(var e in State.entities){var s=Spec(e.specId);if(s.Has(Capability.Source) && e.stock<s.initialStock) cost+=s.sourceItemId=="beans" ? Catalog.economy.beanUnitPrices[beanQuality]*(s.initialStock-e.stock) : (s.unitCost>0 ? s.unitCost*(s.initialStock-e.stock) : s.refillCost);}
            if(cost==0) return ActionResult.No("库存已满，先使用原料再补货");if(State.money<cost) return ActionResult.No("补货需要 "+cost+" 金币");
            State.money-=cost;State.beanQuality=beanQuality;
            foreach(var e in State.entities){var s=Spec(e.specId);if(s.Has(Capability.Source) && e.stock<s.initialStock){e.stock=s.initialStock;if(s.sourceItemId=="beans") e.quality=beanQuality;}}
            Notify();return ActionResult.Yes("补货完成 -"+cost+" 金币；咖啡豆品质 "+beanQuality);
        }
        public ActionResult BuySource(string sourceId,int beanQuality=-1)
        {
            var e=Find(sourceId);var s=e==null ? null : Spec(e.specId);if(s==null || !s.Has(Capability.Source)) return ActionResult.No("无效来源");
            int quality=beanQuality<0 ? State.beanQuality : beanQuality;if(quality<0 || quality>2) return ActionResult.No("无效豆子品质");
            int missing=s.initialStock-e.stock;if(missing<=0) return ActionResult.No("来源库存已满");int cost=s.sourceItemId=="beans" ? missing*Catalog.economy.beanUnitPrices[quality] : (s.unitCost>0 ? missing*s.unitCost : s.refillCost);
            if(State.money<cost) return ActionResult.No("购买需要 "+cost+" 金币");State.money-=cost;e.stock=s.initialStock;
            if(s.sourceItemId=="beans"){e.quality=quality;State.beanQuality=quality;}Notify();return ActionResult.Yes("购买 "+s.label+" -"+cost+" 金币，可继续拖出");
        }
        public ActionResult Talk(string customerId)
        {
            var customer=Find(customerId);if(customer==null || !Spec(customer.specId).Has(Capability.Customer)) return ActionResult.No("不是顾客");
            var request=State.requests.Find(r=>r.customerId==customerId);if(request!=null) State.focusedRequestId=request.id;
            NpcSpec npc=null;if(Catalog.npcs!=null) foreach(var n in Catalog.npcs) if(n.id==customer.npcId){npc=n;break;}
            string text=npc==null ? "顾客：请看看我的订单。" : npc.name+"："+npc.dialogue;
            if(npc!=null && !State.spokenCustomers.Contains(customerId)){State.spokenCustomers.Add(customerId);if(!string.IsNullOrEmpty(npc.topic) && !State.topics.Contains(npc.topic)){State.topics.Add(npc.topic);text+=" · 获得热点词条："+npc.topic;}}
            Notify();return ActionResult.Yes(text);
        }
        public ActionResult Hire(string ability)
        {
            if(State.staff>=Catalog.economy.maxStaff) return ActionResult.No("店员已达上限");
            bool unlocked=ability=="machines" ? State.automaticMachines : ability=="orders" ? State.automaticOrders : ability=="latte" ? State.automaticLatteArt : ability=="cocoa" ? State.automaticCocoa : ability=="delivery" ? State.automaticDelivery : true;
            if(unlocked) return ActionResult.No("请选择未解锁的店员能力");if(State.money<Catalog.economy.hireCost) return ActionResult.No("招募金币不足");
            State.money-=Catalog.economy.hireCost;State.staff++;
            switch(ability){case "machines":State.automaticMachines=true;break;case "orders":State.automaticOrders=true;break;case "latte":State.automaticLatteArt=true;break;case "cocoa":State.automaticCocoa=true;break;case "delivery":State.automaticDelivery=true;break;}
            Notify();return ActionResult.Yes("店员已招募，解锁 "+ability+"；设备速度与备杯能力提高");
        }
        public ActionResult Expand()
        {
            if(State.seats>=8) return ActionResult.No("初版卡座上限 8");if(State.money<Catalog.economy.expansionCost) return ActionResult.No("扩建金币不足");State.money-=Catalog.economy.expansionCost;State.seats=Math.Min(8,State.seats+Catalog.economy.seatsPerExpansion);Notify();return ActionResult.Yes("卡座容量提高到 "+State.seats);
        }
        public ActionResult UpgradeMachine(string specId,bool skill=false)
        {
            if(Spec(specId)==null || !Spec(specId).Has(Capability.Processor)) return ActionResult.No("无效设备");var list=skill ? State.skillMachines : State.fasterMachines;
            if(list.Contains(specId)) return ActionResult.No("已购买这项升级");if(skill ? State.skillPoints<1 : State.money<Catalog.economy.upgradeCost) return ActionResult.No("升级资源不足");
            if(skill) State.skillPoints--;else State.money-=Catalog.economy.upgradeCost;list.Add(specId);Notify();return ActionResult.Yes("设备效率升级（下次加工生效）");
        }
        public ActionResult UpgradeProduct(string productId)
        {
            if(Product(productId)==null || !State.unlockedProducts.Contains(productId)) return ActionResult.No("餐品尚未解锁");if(State.betterProducts.Contains(productId)) return ActionResult.No("售价已升级");
            if(State.money<Catalog.economy.upgradeCost) return ActionResult.No("升级金币不足");State.money-=Catalog.economy.upgradeCost;State.betterProducts.Add(productId);Notify();return ActionResult.Yes("餐品售价提高 50%");
        }
        public ActionResult AddMachine(string specId)
        {
            var spec=Spec(specId);if(spec==null || !spec.Has(Capability.Processor)) return ActionResult.No("无效设备");int count=State.entities.FindAll(e=>e.specId==specId).Count;
            if(count>=1+State.staff || count>=Catalog.economy.maxMachineCopies) return ActionResult.No("先招募店员增加设备数量上限");if(State.money<Catalog.economy.extraMachineCost) return ActionResult.No("设备购买金币不足");
            var origin=State.entities.Find(e=>e.specId==specId);float x=Math.Max(-8,Math.Min(8,origin.x+.4f*count)),y=origin.y-2*count;
            var valid=CanPosition(specId,origin.area,x,y);if(!valid.success) return valid;
            State.money-=Catalog.economy.extraMachineCost;State.entities.Add(new WorldEntity{id=Allocate("free_"),specId=specId,area=origin.area,x=x,y=y});Notify();return ActionResult.Yes("新增设备，可独立并行加工");
        }
    }
}
