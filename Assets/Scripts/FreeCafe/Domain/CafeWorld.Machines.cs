using System;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        public OperationSpec Operation(string id)=>id!=null && operations.TryGetValue(id,out var op) ? op : null;
        public OperationSpec OperationFor(string machine,string input)
        { foreach(var op in Catalog.operations) if(op.machineSpecId==machine && op.inputSpecId==input) return op;return null; }
        private Portion MachineIngredient(WorldEntity item,WorldEntity machine,out OperationSpec op)
        {
            op=OperationFor(machine.specId,item.specId);
            if(op!=null && Spec(item.specId).Has(Capability.Portion)) return new Portion{specId=item.specId,units=item.units,quality=item.quality};
            if(Spec(item.specId).Has(Capability.Container))
                foreach(var p in item.contents)
                {
                    op=OperationFor(machine.specId,p.specId);
                    if(op!=null && op.acceptsContainer) return p;
                }
            op=null;return null;
        }
        private ActionResult PreviewMachine(WorldEntity item,WorldEntity machine)
        {
            if(machine.task!=null)
            {
                if(machine.task.phase!=MachinePhase.Ready) return ActionResult.No("设备忙碌；点击设备操作，右键取消");
                if(!Spec(item.specId).Has(Capability.Container)) return ActionResult.No("请拖入杯子或接粉杯接取产物");
                var output=Find(machine.task.inputId);
                if(output==null) return ActionResult.No("设备产物无效");
                if(TotalUnits(item)+output.units>Spec(item.specId).capacity) return ActionResult.No("容器容量不足，产物保留在设备内");
                return ActionResult.Yes("接取："+Spec(output.specId).label+" ×"+output.units);
            }
            var ingredient=MachineIngredient(item,machine,out var op);
            return ingredient==null ? ActionResult.No("没有这台设备可处理的材料；Shift 仅摆放") : ActionResult.Yes("投入 "+Spec(ingredient.specId).label+" → "+op.label+"（保留容器）");
        }
        private ActionResult InteractMachine(WorldEntity item,WorldEntity machine)
        {
            if(machine.task!=null && machine.task.phase==MachinePhase.Ready)
            {
                var output=Find(machine.task.inputId);
                AddPortion(item,new Portion{specId=output.specId,units=output.units,quality=output.quality});
                State.entities.Remove(output);machine.task=null;Notify();return ActionResult.Yes("已接取产物，设备空闲");
            }
            var portion=MachineIngredient(item,machine,out var op);
            WorldEntity input;
            if(Spec(item.specId).Has(Capability.Container))
            {
                // Transfer one dose only; coffee already in the cup must not be swallowed by the milk heater.
                input=new WorldEntity{id=Allocate("free_"),specId=portion.specId,units=1,quality=portion.quality,area=machine.area};
                portion.units--;if(portion.units==0) item.contents.Remove(portion);item.latteArt=false;State.entities.Add(input);
            }
            else input=item;
            input.ownerId=machine.id;input.x=machine.x;input.y=machine.y;
            bool automatic=State.automaticMachines || (op.control==OperationControl.Qte && State.automaticCocoa);
            machine.task=new ProcessingTask{inputId=input.id,operationId=op.id,remaining=Duration(op),phase=op.control==OperationControl.Timer || automatic ? MachinePhase.Running : MachinePhase.Loaded};
            Notify();return ActionResult.Yes(op.control==OperationControl.Clicks && !automatic ? "材料已投入；点击设备 "+RequiredClicks(machine)+" 次" : op.control==OperationControl.Qte && !automatic ? "材料已投入；点击设备打开无惩罚 QTE" : "已开始 "+op.label);
        }
        public int RequiredClicks(WorldEntity machine)
        {var op=machine?.task==null ? null : Operation(machine.task.operationId);return op==null ? 0 : Math.Max(1,op.clicks-(State.skillMachines.Contains(machine.specId) ? 1 : 0));}
        private float Duration(OperationSpec op)
        {
            float scale=Math.Max(.5f,1-State.staff*Catalog.economy.staffSpeedPerHire);
            if(State.fasterMachines.Contains(op.machineSpecId)) scale*=Catalog.economy.timerUpgradeMultiplier;
            if(State.skillMachines.Contains(op.machineSpecId)) scale*=.8f;
            return op.seconds*scale;
        }
        public ActionResult Operate(string machineId)
        {
            var machine=Find(machineId);var task=machine?.task;
            if(task==null) return ActionResult.No("空闲设备：先拖入材料或装有材料的杯子");
            if(task.phase==MachinePhase.Ready) return ActionResult.Yes("产物已就绪；拖杯子到设备接取，或把设备拖到杯子");
            var op=Operation(task.operationId);
            if(task.phase==MachinePhase.Running) return ActionResult.No("加工中；右键设备可以确认取消");
            if(op.control==OperationControl.Qte) return ActionResult.Yes("QTE：在圆圈进入绿色区间时点击；失败可重试，不扣款");
            if(task.cooldown>0) return ActionResult.No("点击间隔约 "+op.clickInterval.ToString("0.0")+" 秒");
            task.clicksDone++;task.cooldown=op.clickInterval;
            if(task.clicksDone>=RequiredClicks(machine)) FinishMachine(machine);
            Notify();return ActionResult.Yes(task.phase==MachinePhase.Ready ? "加工完成，拖杯接取" : "操作 "+task.clicksDone+"/"+RequiredClicks(machine));
        }
        public ActionResult ResolveQte(string machineId,float normalizedRadius)
        {
            var machine=Find(machineId);var task=machine?.task;
            if(task==null || task.phase!=MachinePhase.Loaded || Operation(task.operationId).control!=OperationControl.Qte) return ActionResult.No("没有等待 QTE 的设备");
            if(!Finite(normalizedRadius) || normalizedRadius<.28f || normalizedRadius>.42f) return ActionResult.No("未命中，免费重试");
            FinishMachine(machine);Notify();return ActionResult.Yes("可可融化完成，拖杯接取");
        }
        private void FinishMachine(WorldEntity machine)
        {
            var task=machine.task;var input=Find(task.inputId);
            input.specId=Operation(task.operationId).outputSpecId;task.remaining=0;task.phase=MachinePhase.Ready;
        }
        public ActionResult Release(string machineId,bool cancel=false)
        {
            var machine=Find(machineId);var task=machine?.task;if(task==null) return ActionResult.No("设备空闲");
            if(task.phase!=MachinePhase.Ready && !cancel) return ActionResult.No("材料未完成；点击操作或右键取消");
            var input=Find(task.inputId);if(input==null) return ActionResult.No("设备记录无效");
            input.ownerId=null;machine.task=null;
            var bounds=Area(input.area);var spec=Spec(input.specId);
            input.x=Math.Max(-bounds.halfWidth+spec.width*.5f,Math.Min(bounds.halfWidth-spec.width*.5f,machine.x+.9f));
            input.y=Math.Max(-bounds.halfHeight+spec.height*.5f,Math.Min(bounds.halfHeight-spec.height*.5f,machine.y-1));
            Notify();return ActionResult.Yes(cancel ? "已取消，材料保留" : "已取出；推荐用容器直接接取");
        }
        public ActionResult CompleteLatteArt(string cupId,float coverage)
        {
            var cup=Find(cupId);
            if(cup==null || !Spec(cup.specId).Has(Capability.Container) || Locked(cup) || cup.contents.Find(p=>p.specId=="coffee")==null || cup.contents.Find(p=>p.specId=="hotmilk")==null) return ActionResult.No("先准备含咖啡和热牛奶的杯子");
            if(!Finite(coverage) || coverage<.75f) return ActionResult.No("拉花覆盖不足，可继续拖画");
            cup.latteArt=true;Notify();return ActionResult.Yes("拉花完成");
        }
    }
}
