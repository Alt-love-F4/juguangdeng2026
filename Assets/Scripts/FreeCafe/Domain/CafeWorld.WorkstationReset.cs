using System;
using System.Collections.Generic;

namespace Spotlight.FreeCafe
{
    public sealed partial class CafeWorld
    {
        /// <summary>Clear the workstation without restocking ingredients or changing business progress.</summary>
        public ActionResult ResetWorkstation()
        {
            if (!ProductionEnabled || State.currentArea != AreaId.Counter)
                return ActionResult.No("请先进入工作台");

            // Include workstation equipment carried elsewhere, but leave unrelated areas alone.
            var targets = new HashSet<string>();
            foreach (var entity in State.entities)
            {
                var home = Array.Find(Catalog.layout, entry => entry.id == entity.id && entry.specId == entity.specId);
                if (entity.area == AreaId.Counter || home != null && home.area == AreaId.Counter)
                    targets.Add(entity.id);
            }
            foreach (var entity in State.entities)
                if (!string.IsNullOrEmpty(entity.ownerId) && targets.Contains(entity.ownerId)) targets.Add(entity.id);

            CancelProductionMiniGame();
            foreach (var entity in State.entities.ToArray())
            {
                if (!targets.Contains(entity.id)) continue;
                var spec = Spec(entity.specId);
                if (IsCup(entity))
                {
                    RemoveCupAndRefundStock(entity);
                    continue;
                }
                if (spec.Has(Capability.Portion) && !spec.Has(Capability.Source))
                {
                    State.entities.Remove(entity);
                    continue;
                }
                if (!IsContainer(entity) && !IsProductionMachine(entity) && !spec.Has(Capability.Source)) continue;
                entity.contents.Clear();
                entity.production = new ProductionEntityState();
                entity.task = null;
                entity.ownerId = entity.cupStackBelowId = null;
                entity.portionPayload = null;
                entity.latteArt = false;
                entity.latteArtScore = 0;
                entity.latteArtPattern = null;
                var home = Array.Find(Catalog.layout, entry => entry.id == entity.id && entry.specId == entity.specId);
                if (home != null)
                {
                    entity.area = home.area; entity.x = home.x; entity.y = home.y; entity.rotation = 0;
                }
                // Do not assign stock or invoke Refill/RestoreContainerDefaults here.
                // The infinite water source still supplies water on the next pour as usual.
            }
            State.production.activeCupId = null;
            foreach (var jug in State.entities)
            {
                if (!targets.Contains(jug.id) || !IsPrecisionExtractor(jug)) continue;
                var filter = Find(jug.id + ".filter");
                if (!IsExtractionFilter(filter) || !targets.Contains(filter.id) || filter.area != jug.area) continue;
                jug.production.extractionFilterId = filter.id;
                jug.production.extractionAssemblyInitialized = true;
                filter.production.extractionJugId = jug.id;
                PositionExtractionFilter(jug);
            }
            Notify();
            return ActionResult.Yes("工作台已重置：内容物已清空，杯子已归还，原料与道具库存不补充");
        }
    }
}
