using System;

namespace Spotlight.FreeCafe
{
    /// <summary>CafeWorld 的设备加工部分：投入原料、点击/QTE 操作、接取产物与拉花判定。</summary>
    public sealed partial class CafeWorld
    {
        /// <summary>按 id 查询工序规格。</summary>
        public OperationSpec Operation(string id) => id != null && operations.TryGetValue(id, out var op) ? op : null;

        /// <summary>按「机器 + 输入」匹配工序；同一组合的唯一性在目录校验时已保证。</summary>
        public OperationSpec OperationFor(string machine, string input)
        {
            foreach (var op in Catalog.operations)
            {
                if (op.machineSpecId == machine && op.inputSpecId == input)
                {
                    return op;
                }
            }

            return null;
        }

        /// <summary>
        /// 从待投入的物品里找出这台设备能处理的原料：
        /// 单份原料直接用自身；容器则从内容物里挑第一份可被该机器接收的原料。
        /// </summary>
        private Portion MachineIngredient(WorldEntity item, WorldEntity machine, out OperationSpec op)
        {
            op = OperationFor(machine.specId, item.specId);
            if (op != null && Spec(item.specId).Has(Capability.Portion))
            {
                return new Portion { specId = item.specId, units = item.units, quality = item.quality };
            }

            if (Spec(item.specId).Has(Capability.Container))
            {
                foreach (var p in item.contents)
                {
                    op = OperationFor(machine.specId, p.specId);
                    if (op != null && op.acceptsContainer)
                    {
                        return p;
                    }
                }
            }

            op = null;
            return null;
        }

        /// <summary>预演把物品拖到设备上的结果：要么接取成品，要么投入原料。</summary>
        private ActionResult PreviewMachine(WorldEntity item, WorldEntity machine)
        {
            if (machine.task != null)
            {
                // 设备里有东西：只有 Ready 状态才能接取
                if (machine.task.phase != MachinePhase.Ready)
                {
                    return ActionResult.No("设备忙碌；点击设备操作，右键取消");
                }

                if (!Spec(item.specId).Has(Capability.Container))
                {
                    return ActionResult.No("请拖入杯子或接粉杯接取产物");
                }

                var output = Find(machine.task.inputId);
                if (output == null)
                {
                    return ActionResult.No("设备产物无效");
                }

                if (TotalUnits(item) + output.units > Spec(item.specId).capacity)
                {
                    return ActionResult.No("容器容量不足，产物保留在设备内");
                }

                return ActionResult.Yes("接取：" + Spec(output.specId).label + " ×" + output.units);
            }

            var ingredient = MachineIngredient(item, machine, out var op);
            return ingredient == null
                ? ActionResult.No("没有这台设备可处理的材料；Shift 仅摆放")
                : ActionResult.Yes("投入 " + Spec(ingredient.specId).label + " → " + op.label + "（保留容器）");
        }

        /// <summary>执行设备交互：Ready 时接取产物，否则尝试投入原料并开启加工。</summary>
        private ActionResult InteractMachine(WorldEntity item, WorldEntity machine)
        {
            if (machine.task != null && machine.task.phase == MachinePhase.Ready)
            {
                var output = Find(machine.task.inputId);
                AddPortion(item, new Portion { specId = output.specId, units = output.units, quality = output.quality });
                State.entities.Remove(output);
                machine.task = null;
                Notify();
                return ActionResult.Yes("已接取产物，设备空闲");
            }

            var portion = MachineIngredient(item, machine, out var op);
            WorldEntity input;
            if (Spec(item.specId).Has(Capability.Container))
            {
                // 只转移一份剂量：杯里已有的咖啡不能被牛奶加热器整杯吞掉
                input = new WorldEntity
                {
                    id = Allocate("free_"),
                    specId = portion.specId,
                    units = 1,
                    quality = portion.quality,
                    area = machine.area
                };
                portion.units--;
                if (portion.units == 0)
                {
                    item.contents.Remove(portion);
                }

                item.latteArt = false;
                State.entities.Add(input);
            }
            else
            {
                input = item;
            }

            input.ownerId = machine.id;
            input.x = machine.x;
            input.y = machine.y;

            // 定时工序或已开启自动化：装料后直接开跑；点击/QTE 类需要玩家先点一下设备
            bool automatic = State.automaticMachines || (op.control == OperationControl.Qte && State.automaticCocoa);
            machine.task = new ProcessingTask
            {
                inputId = input.id,
                operationId = op.id,
                remaining = Duration(op),
                phase = op.control == OperationControl.Timer || automatic ? MachinePhase.Running : MachinePhase.Loaded
            };
            Notify();

            if (op.control == OperationControl.Clicks && !automatic)
            {
                return ActionResult.Yes("材料已投入；点击设备 " + RequiredClicks(machine) + " 次");
            }

            if (op.control == OperationControl.Qte && !automatic)
            {
                return ActionResult.Yes("材料已投入；点击设备打开无惩罚 QTE");
            }

            return ActionResult.Yes("已开始 " + op.label);
        }

        /// <summary>点击类工序还差几下；用技能点升级过的设备可以少点一次。</summary>
        public int RequiredClicks(WorldEntity machine)
        {
            var op = machine?.task == null ? null : Operation(machine.task.operationId);
            return op == null ? 0 : Math.Max(1, op.clicks - (State.skillMachines.Contains(machine.specId) ? 1 : 0));
        }

        /// <summary>工序实际耗时：受店员数量、金钱升级、技能升级三方面影响。</summary>
        private float Duration(OperationSpec op)
        {
            float scale = Math.Max(.5f, 1 - State.staff * Catalog.economy.staffSpeedPerHire);
            if (State.fasterMachines.Contains(op.machineSpecId))
            {
                scale *= Catalog.economy.timerUpgradeMultiplier;
            }

            if (State.skillMachines.Contains(op.machineSpecId))
            {
                scale *= .8f;
            }

            return op.seconds * scale;
        }

        /// <summary>点击设备：推进点击类工序，或在各阶段给出提示。</summary>
        public ActionResult Operate(string machineId)
        {
            var machine = Find(machineId);
            var task = machine?.task;
            if (task == null)
            {
                return ActionResult.No("空闲设备：先拖入材料或装有材料的杯子");
            }

            if (task.phase == MachinePhase.Ready)
            {
                // 可拖出产物的设备（如研磨器）引导玩家直接拖设备，其余设备用容器接取
                return ActionResult.Yes(Spec(machine.specId).dragOutput
                    ? "研磨完成；拖动研磨器拖出咖啡粉，可放入摩卡炉或杯子"
                    : "产物已就绪；拖杯子到设备接取");
            }

            var op = Operation(task.operationId);
            if (task.phase == MachinePhase.Running)
            {
                return ActionResult.No("加工中；右键设备可以确认取消");
            }

            if (op.control == OperationControl.Qte)
            {
                return ActionResult.Yes("QTE：在圆圈进入绿色区间时点击；失败可重试，不扣款");
            }

            if (task.cooldown > 0)
            {
                return ActionResult.No("点击间隔约 " + op.clickInterval.ToString("0.0") + " 秒");
            }

            task.clicksDone++;
            task.cooldown = op.clickInterval;
            if (task.clicksDone >= RequiredClicks(machine))
            {
                FinishMachine(machine);
            }

            Notify();
            if (task.phase == MachinePhase.Ready)
            {
                return ActionResult.Yes(Spec(machine.specId).dragOutput ? "研磨完成，拖动研磨器取出咖啡粉" : "加工完成，拖杯接取");
            }

            return ActionResult.Yes("操作 " + task.clicksDone + "/" + RequiredClicks(machine));
        }

        /// <summary>QTE 结果：半径落在 0.28~0.42 视为命中；失败不惩罚，可以重试。</summary>
        public ActionResult ResolveQte(string machineId, float normalizedRadius)
        {
            var machine = Find(machineId);
            var task = machine?.task;
            if (task == null || task.phase != MachinePhase.Loaded || Operation(task.operationId).control != OperationControl.Qte)
            {
                return ActionResult.No("没有等待 QTE 的设备");
            }

            if (!Finite(normalizedRadius) || normalizedRadius < .28f || normalizedRadius > .42f)
            {
                return ActionResult.No("未命中，免费重试");
            }

            FinishMachine(machine);
            Notify();
            return ActionResult.Yes("可可融化完成，拖杯接取");
        }

        /// <summary>加工完成：材料原地变成产物，等待玩家接取。</summary>
        private void FinishMachine(WorldEntity machine)
        {
            var task = machine.task;
            var input = Find(task.inputId);
            input.specId = Operation(task.operationId).outputSpecId;
            task.remaining = 0;
            task.phase = MachinePhase.Ready;
        }

        /// <summary>
        /// 生成一份「脱离运行时」的产物预览：只用于拖拽过程中的显示与预演，
        /// 不会释放真实产物；只有落点校验通过后才会真正提交。
        /// </summary>
        public WorldEntity DragOutputPreview(string machineId)
        {
            var machine = Find(machineId);
            if (machine == null || !Spec(machine.specId).dragOutput || machine.task == null || machine.task.phase != MachinePhase.Ready)
            {
                return null;
            }

            var output = Find(machine.task.inputId);
            if (output == null || output.ownerId != machine.id || !Spec(output.specId).Has(Capability.Movable))
            {
                return null;
            }

            return new WorldEntity
            {
                id = output.id,
                specId = output.specId,
                area = machine.area,
                x = machine.x,
                y = machine.y,
                units = output.units,
                quality = output.quality
            };
        }

        /// <summary>
        /// 把设备产物拖出到指定位置：先整体校验（含组合预演），
        /// 任何一步失败都保持原任务与产物不变，全部通过后才解除归属并落点。
        /// </summary>
        public ActionResult TakeMachineOutput(string machineId, string expectedOutputId, string targetId, AreaId area, float x, float y)
        {
            var candidate = DragOutputPreview(machineId);
            if (candidate == null || candidate.id != expectedOutputId)
            {
                return ActionResult.No("设备产物已变化或尚未完成，请重新拖取");
            }

            if (candidate.area != area)
            {
                return ActionResult.No("请到设备所在区域接取产物");
            }

            var valid = CanPosition(candidate.specId, area, x, y);
            if (!valid.success)
            {
                return valid;
            }

            if (targetId != null)
            {
                var preview = Preview(candidate, Find(targetId));
                if (!preview.success)
                {
                    return preview;
                }
            }

            // 校验全部通过后才修改世界
            var machine = Find(machineId);
            var output = Find(expectedOutputId);
            output.ownerId = null;
            output.area = area;
            output.x = x;
            output.y = y;
            machine.task = null;
            if (targetId != null)
            {
                return Interact(output.id, targetId);
            }

            Notify();
            return ActionResult.Yes("已拖出 " + Spec(output.specId).label + "，设备空闲");
        }

        /// <summary>从设备里取出材料；cancel=true 时允许中断未完成的加工。</summary>
        public ActionResult Release(string machineId, bool cancel = false)
        {
            var machine = Find(machineId);
            var task = machine?.task;
            if (task == null)
            {
                return ActionResult.No("设备空闲");
            }

            if (task.phase != MachinePhase.Ready && !cancel)
            {
                return ActionResult.No("材料未完成；点击操作或右键取消");
            }

            var input = Find(task.inputId);
            if (input == null)
            {
                return ActionResult.No("设备记录无效");
            }

            input.ownerId = null;
            machine.task = null;

            // 放到机器旁边，并夹在区域边界内
            var bounds = Area(input.area);
            var spec = Spec(input.specId);
            input.x = Math.Max(-bounds.halfWidth + spec.width * .5f, Math.Min(bounds.halfWidth - spec.width * .5f, machine.x + .9f));
            input.y = Math.Max(-bounds.halfHeight + spec.height * .5f, Math.Min(bounds.halfHeight - spec.height * .5f, machine.y - 1));
            Notify();
            return ActionResult.Yes(cancel ? "已取消，材料保留" : "已取出；推荐用容器直接接取");
        }

        /// <summary>完成拉花：杯子里必须同时有咖啡和热牛奶，且拖画覆盖率达到 75%。</summary>
        public ActionResult CompleteLatteArt(string cupId, float coverage)
        {
            var cup = Find(cupId);
            if (cup == null || !Spec(cup.specId).Has(Capability.Container) || Locked(cup)
                || cup.contents.Find(p => p.specId == "coffee") == null
                || cup.contents.Find(p => p.specId == "hotmilk") == null)
            {
                return ActionResult.No("先准备含咖啡和热牛奶的杯子");
            }

            if (!Finite(coverage) || coverage < .75f)
            {
                return ActionResult.No("拉花覆盖不足，可继续拖画");
            }

            cup.latteArt = true;
            Notify();
            return ActionResult.Yes("拉花完成");
        }
    }
}