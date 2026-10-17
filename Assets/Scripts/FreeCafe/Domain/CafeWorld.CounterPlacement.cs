using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    /// <summary>Counter placement rules. Cup identities and heat remain in the existing world snapshot.</summary>
    public sealed partial class CafeWorld
    {
        public bool IsCup(WorldEntity entity) => entity != null && (entity.specId == "cup.small" || entity.specId == "cup.large");
        public WorldEntity CupAbove(WorldEntity cup)
        {
            if (!IsCup(cup) || string.IsNullOrEmpty(cup.id)) return null;
            foreach (var other in State.entities) if (other.cupStackBelowId == cup.id) return other;
            return null;
        }
        public float CupStackStep(string specId) => Spec(specId).height * (specId == "cup.large" ? .46f : .43f);
        public bool ShouldReturnVessel(WorldEntity entity) => ProductionEnabled && IsContainer(entity) && !IsCup(entity);
        public ActionResult StartSelfHeating(string id)
        {
            var vessel = Find(id);
            if (!ProductionEnabled || vessel == null || vessel.specId != "machine.heater") return ActionResult.No("不是自加热壶");
            if (MiniGameUses(vessel)) return ActionResult.No("请先结束小游戏");
            if (vessel.contents.Count == 0) return ActionResult.No("空壶没有内容可加热");
            if (vessel.production.phase == ProductionPhase.Processing)
            { vessel.production.selfHeating = true; Notify(); return ActionResult.Yes("正在自加热"); }
            vessel.production.selfHeating = true;
            vessel.production.phase = ProductionPhase.Processing;
            vessel.production.remaining = Prep.heatSeconds;
            Notify(); return ActionResult.Yes("开始自加热，不需要炉台");
        }
        /// <summary>Called on release, never while either mouse button owns the vessel.</summary>
        public ActionResult ReturnVesselHome(string id)
        {
            var vessel = Find(id);
            if (!ShouldReturnVessel(vessel)) return ActionResult.No("此物品可自由摆放");
            var home = Array.Find(Catalog.layout, e => e.id == id && e.specId == vessel.specId);
            if (home == null) return ActionResult.No("没有记录器皿原位");
            foreach (var stove in State.entities)
                if (stove.production.heatingVesselId == id) stove.production.heatingVesselId = null;
            if (ServingCup == vessel) State.production.activeCupId = null;
            vessel.area = home.area; vessel.x = home.x; vessel.y = home.y; vessel.rotation = 0;
            if (IsExtractionFilter(vessel))
            {
                var jug = ExtractionJug(vessel);
                if (jug != null) ReturnVesselHome(jug.id);
                else // Park the removed tower beside its jug, never silently re-assemble it.
                {
                    foreach (var entry in Catalog.layout)
                        if (entry.area == home.area && Spec(entry.specId).Has(Capability.HotExtraction))
                        { vessel.x = entry.x + Spec(entry.specId).width * .6f; vessel.y = entry.y; break; }
                }
            }
            if (IsPrecisionExtractor(vessel))
            {
                PositionExtractionFilter(vessel);
                InitializeExtractionAssemblies();
            }
            Notify(); return ActionResult.Yes("器皿已回到原位，保留内容物");
        }
        private ActionResult PreviewCupStack(WorldEntity cup, WorldEntity target)
        {
            if (cup == target || cup.specId != target.specId) return ActionResult.No("只能堆叠同型空杯");
            if (cup.contents.Count != 0 || target.contents.Count != 0 || Locked(cup) || Locked(target)
                || MiniGameUses(cup) || MiniGameUses(target)) return ActionResult.No("请先倒空杯子并结束加工");
            if (CupAbove(cup) != null) return ActionResult.No("请先取走最上面的杯子");
            var top = target;
            for (int n = 0; n < State.entities.Count; n++)
            {
                if (top == cup) return ActionResult.No("杯子已经在这个杯堆里");
                var next = CupAbove(top); if (next == null) break; top = next;
            }
            if (top.contents.Count != 0 || Locked(top) || MiniGameUses(top)) return ActionResult.No("请先倒空顶杯并结束加工");
            return CanPosition(cup.specId, target.area, top.x, top.y + CupStackStep(cup.specId));
        }
        private ActionResult StackCups(WorldEntity cup, WorldEntity target)
        {
            var valid = PreviewCupStack(cup, target); if (!valid.success) return valid;
            for (int n = 0; n < State.entities.Count; n++)
            { var next = CupAbove(target); if (next == null) break; target = next; }
            cup.cupStackBelowId = target.id;
            if (ServingCup == cup) State.production.activeCupId = null;
            Notify(); return ActionResult.Yes("空杯已叠放；从最上方依次取杯");
        }
        private void RefreshCupStacks()
        {
            foreach (var cup in State.entities)
            {
                var below = Find(cup.cupStackBelowId);
                if (below == null || !IsCup(cup) || below.specId != cup.specId || cup.contents.Count != 0 || below.contents.Count != 0)
                    cup.cupStackBelowId = null;
            }
            // Resolve the short chains bottom-up, without an allocation on Tick/Notify.
            for (int n = 0; n < State.entities.Count; n++)
            {
                bool changed = false;
                foreach (var cup in State.entities)
                {
                    var below = Find(cup.cupStackBelowId); if (below == null) continue;
                    float y = below.y + CupStackStep(cup.specId);
                    if (cup.x == below.x && cup.y == y && cup.area == below.area) continue;
                    cup.x = below.x; cup.y = y; cup.area = below.area; changed = true;
                }
                if (!changed) break;
            }
        }
        private bool ValidCupStacks(Dictionary<string, WorldEntity> entities)
        {
            var occupied = new HashSet<string>();
            foreach (var cup in entities.Values)
            {
                if (string.IsNullOrEmpty(cup.cupStackBelowId)) continue;
                if (!IsCup(cup) || cup.contents.Count != 0 || !occupied.Add(cup.cupStackBelowId)
                    || !entities.TryGetValue(cup.cupStackBelowId, out var below) || below.specId != cup.specId
                    || below.contents.Count != 0 || below.area != cup.area) return false;
                var current = cup;
                for (int n = 0; !string.IsNullOrEmpty(current.cupStackBelowId); n++)
                { if (n >= entities.Count || !entities.TryGetValue(current.cupStackBelowId, out current)) return false; }
            }
            return true;
        }
    }
}
