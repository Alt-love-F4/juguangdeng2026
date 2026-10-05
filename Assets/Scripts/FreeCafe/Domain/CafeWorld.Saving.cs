using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        /// <summary>Validate a detached snapshot before changing the live world. v1 saves migrate explicitly.</summary>
        public ActionResult Restore(CafeSnapshot candidate)
        {
            if(candidate==null || (candidate.version!=1 && candidate.version!=2) || candidate.entities==null || candidate.requests==null || Area(candidate.currentArea)==null || candidate.nextId<1 || candidate.money<0 || candidate.day<1 || !Finite(candidate.timeRemaining) || !Finite(candidate.arrivalRemaining) || candidate.timeRemaining<0) return ActionResult.No("存档版本或结构无效");
            bool legacy=candidate.version==1;
            if(legacy){candidate.version=2;candidate.seats=Catalog.economy.initialSeats;candidate.businessPhase=candidate.open ? BusinessPhase.Open : BusinessPhase.Preparation;candidate.todayCustomerBudget=Catalog.economy.baseCustomers;}
            if(candidate.seats<1 || candidate.seats>8 || candidate.staff<0 || candidate.staff>Catalog.economy.maxStaff || candidate.beanQuality<0 || candidate.beanQuality>2 || candidate.skillPoints<0 || candidate.todayCustomerBudget<0 || candidate.todayCustomerBudget>Catalog.economy.maxCustomers || candidate.todayArrivals<0 || candidate.todayArrivals>candidate.todayCustomerBudget || !Enum.IsDefined(typeof(BusinessPhase),candidate.businessPhase) || candidate.open!=(candidate.businessPhase==BusinessPhase.Open)) return ActionResult.No("存档经营状态无效");
            var ids=new Dictionary<string,WorldEntity>();
            foreach(var e in candidate.entities)
            {
                // Unity's inline serializer expands null nested objects into empty objects.
                if(e!=null && e.task!=null && e.task.phase==MachinePhase.Idle && string.IsNullOrEmpty(e.task.inputId) && string.IsNullOrEmpty(e.task.operationId) && e.task.remaining==0) e.task=null;
                var spec=e==null ? null : Spec(e.specId);
                if(spec==null || string.IsNullOrEmpty(e.id) || ids.ContainsKey(e.id) || e.contents==null || e.units<=0 || e.stock<0 || e.quality<0 || e.quality>2 || !Finite(e.rotation) || !CanPosition(e.specId,e.area,e.x,e.y).success) return ActionResult.No("存档实体无效："+e?.id);
                if(!spec.Has(Capability.Container) && e.contents.Count!=0) return ActionResult.No("非容器存在内容物");
                foreach(var p in e.contents) if(p==null || Spec(p.specId)==null || !Spec(p.specId).Has(Capability.Portion) || p.units<=0 || p.quality<0 || p.quality>2) return ActionResult.No("存档内容物无效");
                if(spec.Has(Capability.Container) && TotalUnits(e)>spec.capacity) return ActionResult.No("存档容器容量超限");
                ids.Add(e.id,e);
            }
            foreach(var e in candidate.entities)
            {
                if(e.task!=null)
                {
                    var t=e.task;var op=Operation(t.operationId);
                    if(!Spec(e.specId).Has(Capability.Processor) || op==null || op.machineSpecId!=e.specId || string.IsNullOrEmpty(t.inputId) || !ids.TryGetValue(t.inputId,out var input) || input.ownerId!=e.id || input.area!=e.area || !Finite(t.remaining) || t.remaining<0 || !Finite(t.cooldown) || t.cooldown<0 || t.clicksDone<0 || (t.phase!=MachinePhase.Loaded && t.phase!=MachinePhase.Running && t.phase!=MachinePhase.Ready) || input.specId!=(t.phase==MachinePhase.Ready ? op.outputSpecId : op.inputSpecId)) return ActionResult.No("存档设备任务无效："+e.id);
                }
                if(!string.IsNullOrEmpty(e.ownerId) && (!ids.TryGetValue(e.ownerId,out var owner) || owner.task==null || owner.task.inputId!=e.id)) return ActionResult.No("存档归属无效");
            }
            var requestIds=new HashSet<string>();var customers=new HashSet<string>();
            foreach(var r in candidate.requests)
                if(r==null || string.IsNullOrEmpty(r.id) || !requestIds.Add(r.id) || !customers.Add(r.customerId) || Product(r.productId)==null || !ids.TryGetValue(r.customerId,out var customer) || !Spec(customer.specId).Has(Capability.Customer) || !Finite(r.patience) || r.patience<=0 || !Finite(r.automaticRemaining)) return ActionResult.No("存档订单无效");
            if(candidate.requests.Count>candidate.seats || (!candidate.open && candidate.requests.Count>0)) return ActionResult.No("订单数量与营业状态不匹配");
            // Normalize optional collections for older snapshots. They never hold Unity references.
            candidate.unlockedProducts=candidate.unlockedProducts ?? new List<string>();candidate.unlockedNpcs=candidate.unlockedNpcs ?? new List<string>();candidate.topics=candidate.topics ?? new List<string>();candidate.spokenCustomers=candidate.spokenCustomers ?? new List<string>();
            candidate.fasterMachines=candidate.fasterMachines ?? new List<string>();candidate.betterProducts=candidate.betterProducts ?? new List<string>();candidate.skillMachines=candidate.skillMachines ?? new List<string>();
            foreach(var id in candidate.unlockedProducts) if(Product(id)==null) return ActionResult.No("未知餐品解锁");
            foreach(var id in candidate.betterProducts) if(Product(id)==null) return ActionResult.No("未知售价升级");
            foreach(var list in new[]{candidate.fasterMachines,candidate.skillMachines}) foreach(var id in list) if(Spec(id)==null || !Spec(id).Has(Capability.Processor)) return ActionResult.No("未知设备升级");
            int maxId=0;foreach(var id in ids.Keys) if(id.StartsWith("free_") && int.TryParse(id.Substring(5),out var value)) maxId=Math.Max(maxId,value);
            foreach(var id in requestIds) if(id.StartsWith("order_") && int.TryParse(id.Substring(6),out var value)) maxId=Math.Max(maxId,value);
            candidate.nextId=Math.Max(candidate.nextId,maxId+1);State=candidate;UnlockContent();Notify();return ActionResult.Yes("世界状态已恢复");
        }
    }
}
