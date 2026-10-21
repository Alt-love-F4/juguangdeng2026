using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeWorkstationResetTests
    {
        private CafeWorld world;
        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnterProduction();
        }

        [Test] public void ResetClearsContentsReturnsCupsAndPreservesStocksAndProgress()
        {
            var stocks = new Dictionary<string, int>();
            foreach (var entity in world.State.entities)
                if (world.Spec(entity.specId).Has(Capability.Source)) stocks.Add(entity.id, entity.stock);
            Assert.That(world.Take("s.cups.small", AreaId.Counter, 0, -1, out var cup).success, Is.True);
            Assert.That(world.Take("s.cups.large", AreaId.Counter, 1, -1, out var large).success, Is.True);
            Assert.That(world.Take("s.beans", AreaId.Counter, 0, -1, out var beans).success, Is.True);
            stocks["s.beans"]--;
            cup.contents.Add(new Portion { specId = "milk", milliliters = 30, units = 1 });
            world.State.production.activeCupId = cup.id;
            var heater = world.Find("m.heater");
            heater.contents.Add(new Portion { specId = "water", milliliters = 100, units = 1 });
            Assert.That(world.StartSelfHeating(heater.id).success, Is.True);
            world.Move(heater.id, AreaId.Counter, 0, -1);
            var jug = world.Find("v.extractor"); var filter = world.Find("v.extractor.filter");
            world.PrepareExtractionDrag(jug.id);
            filter.contents.Add(new Portion { specId = "sugar", grams = 5, units = 1 });
            filter.production.filterSpent = true;
            int money = world.State.money, reputation = world.State.reputation, orders = world.State.requests.Count;
            Assert.That(world.ResetWorkstation().success, Is.True);
            Assert.That(world.Find(cup.id), Is.Null); Assert.That(world.Find(large.id), Is.Null);
            Assert.That(world.Find(beans.id), Is.Null); Assert.That(world.ServingCup, Is.Null);
            foreach (var pair in stocks) Assert.That(world.Find(pair.Key).stock, Is.EqualTo(pair.Value), pair.Key);
            foreach (var entity in world.State.entities)
                if (entity.area == AreaId.Counter)
                {
                    Assert.That(entity.contents, Is.Empty, entity.id);
                    Assert.That(entity.task, Is.Null, entity.id);
                    Assert.That(entity.production.phase, Is.Not.EqualTo(ProductionPhase.Processing), entity.id);
                }
            var home = System.Array.Find(world.Catalog.layout, entry => entry.id == heater.id);
            Assert.That(heater.x, Is.EqualTo(home.x)); Assert.That(heater.y, Is.EqualTo(home.y));
            Assert.That(world.ExtractionFilter(jug), Is.SameAs(filter));
            Assert.That(filter.production.filterSpent, Is.False);
            Assert.That(world.State.money, Is.EqualTo(money)); Assert.That(world.State.reputation, Is.EqualTo(reputation));
            Assert.That(world.State.requests.Count, Is.EqualTo(orders));
            string once = JsonUtility.ToJson(world.State);
            Assert.That(world.ResetWorkstation().success, Is.True);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(once), "Repeated reset must not mint cups or ingredients");
            Assert.That(world.Restore(JsonUtility.FromJson<CafeSnapshot>(once)).success, Is.True);
        }

        [Test] public void ResetLeavesOtherAreasUntouchedAndInfiniteWaterUsable()
        {
            Assert.That(world.Take("s.cups.small", AreaId.Counter, 0, -1, out var cup).success, Is.True);
            cup.area = AreaId.Cafe;
            cup.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 30 });
            string before = JsonUtility.ToJson(cup);
            Assert.That(world.ResetWorkstation().success, Is.True);
            Assert.That(JsonUtility.ToJson(world.Find(cup.id)), Is.EqualTo(before));
            Assert.That(world.PourLiquid("s.water", "m.heater", 50, 1).success, Is.True);
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.EqualTo(50));
        }

        [Test] public void ResetOutsideWorkstationIsRejectedWithoutChanges()
        {
            world.LeaveProduction();
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.ResetWorkstation().success, Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }
    }
}
