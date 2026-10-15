using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeMilkPitcherTests
    {
        private CafeWorld world;
        private WorldEntity pitcher, cup;

        [SetUp] public void Setup()
        {
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            world.EnableProduction(); world.EnterProduction();
            pitcher = world.Find("tool.milk-pitcher");
            Assert.That(pitcher, Is.Not.Null);
            Assert.That(world.Take("s.cups.large", AreaId.Counter, 0, 0, out cup).success, Is.True);
        }

        private string Start(float milkMl = 100)
        {
            if (world.LiquidVolume(cup) <= .001f)
                cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10, temperatureCelsius = 60 });
            pitcher.contents.Add(new Portion { specId = "milk", units = 1, milliliters = milkMl, temperatureCelsius = 60 });
            Assert.That(world.FrothMilk(pitcher.id).success, Is.True);
            Assert.That(world.Interact(pitcher.id, cup.id).success, Is.True);
            return world.State.production.miniGameToken;
        }

        [Test] public void LiquidSnapshotTracksFoamAsMilkSubsetWithoutChangingVolume()
        {
            cup.contents.Add(new Portion { specId = "coffee", milliliters = 100, units = 1 });
            cup.contents.Add(new Portion { specId = "milk", milliliters = 40, units = 1 });
            cup.contents.Add(new Portion { specId = "milkfoam", milliliters = 20, units = 1 });
            cup.contents.Add(new Portion { specId = "milkfoam", milliliters = 30, units = 1 });
            var snapshot = world.LiquidSnapshot(cup);
            Assert.That(snapshot.composition.milkMl, Is.EqualTo(90));
            Assert.That(snapshot.composition.milkFoamMl, Is.EqualTo(50));
            Assert.That(snapshot.composition.TotalMl, Is.EqualTo(190));
            Assert.That(snapshot.volumeMl, Is.EqualTo(world.LiquidVolume(cup)));
            Assert.That(new CafeLiquidComposition(100, 90, 0, 0, 0, 0).milkFoamMl, Is.Zero);
            string token = Start();
            Assert.That(world.ConsumeLatteArtFoam(token, 10), Is.EqualTo(10));
            snapshot = world.LiquidSnapshot(cup);
            Assert.That(snapshot.composition.milkFoamMl, Is.EqualTo(60));
            Assert.That(snapshot.volumeMl, Is.EqualTo(200));
            Assert.That(world.ResetLatteArtFoam(token), Is.True);
            snapshot = world.LiquidSnapshot(cup);
            Assert.That(snapshot.composition.milkFoamMl, Is.EqualTo(50));
            Assert.That(snapshot.volumeMl, Is.EqualTo(190));
        }

        [Test] public void FrothingPreservesMilkAmountTemperatureAndProvenance()
        {
            Assert.That(world.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0, 0).success, Is.True);
            float before = world.LiquidVolume(pitcher);
            Assert.That(world.FrothMilk(pitcher.id).success, Is.True);
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(before).Within(.001f));
            Assert.That(pitcher.contents[0].composition[0].ingredientId, Is.EqualTo("milk"));
            Assert.That(world.TemperatureCelsius(pitcher), Is.EqualTo(20));
            Assert.That(world.FrothMilk(pitcher.id).success, Is.False);
        }

        [Test] public void EmptyPitcherCannotFrothAndOtherIngredientsRemainUntouched()
        {
            Assert.That(world.FrothMilk(pitcher.id).success, Is.False);
            pitcher.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 100 });
            pitcher.contents.Add(new Portion { specId = "water", units = 1, milliliters = 10 });
            Assert.That(world.FrothMilk(pitcher.id).success, Is.True);
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(100));
            Assert.That(pitcher.contents.Find(portion => portion.specId == "water").milliliters, Is.EqualTo(10));
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10 });
            Assert.That(world.Interact(pitcher.id, cup.id).success, Is.True);
            world.ConsumeLatteArtFoam(world.State.production.miniGameToken, 100);
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(110));
            Assert.That(world.LiquidVolume(pitcher), Is.EqualTo(10));
            Assert.That(pitcher.contents[0].specId, Is.EqualTo("water"));
        }

        [TestCase("milk")]
        [TestCase("water")]
        [TestCase("milkfoam")]
        public void PitcherNeverAllowsOrdinaryPouringAndRejectedPourPreservesBothContainers(string specId)
        {
            pitcher.contents.Add(new Portion { specId = specId, units = 1, milliliters = 100 });
            var pitcherBefore = JsonUtility.ToJson(pitcher);
            var cupBefore = JsonUtility.ToJson(cup);
            Assert.That(world.CanPour(pitcher), Is.False);
            Assert.That(world.PourLiquid(pitcher.id, cup.id, 20, 1).success, Is.False);
            Assert.That(world.PourLiquid(pitcher.id, null, 20, 1).success, Is.False,
                "A pitcher must not consume liquid by pouring into empty space either");
            Assert.That(world.LiquidVolume(pitcher), Is.EqualTo(100));
            Assert.That(world.LiquidVolume(cup), Is.Zero);
            Assert.That(JsonUtility.ToJson(pitcher), Is.EqualTo(pitcherBefore));
            Assert.That(JsonUtility.ToJson(cup), Is.EqualTo(cupBefore));
            Assert.That(world.State.production.miniGameToken, Is.Null);
        }

        [Test] public void PitcherCannotPourBeforeOrAfterFrothingAndEmptyCupRejectsLatteArtWithoutMovingOrConsuming()
        {
            pitcher.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 100 });
            Assert.That(world.CanPour(pitcher), Is.False);
            Assert.That(world.FrothMilk(pitcher.id).success, Is.True);
            Assert.That(world.CanPour(pitcher), Is.False);
            Assert.That(world.PourLiquid(pitcher.id, cup.id, 20, 1).success, Is.False);
            var before = JsonUtility.ToJson(pitcher);
            var cupBefore = JsonUtility.ToJson(cup);
            var result = world.Interact(pitcher.id, cup.id);
            Assert.That(result.success, Is.False);
            Assert.That(result.message, Is.EqualTo("空杯不能使用奶泡，请先加入饮品液体"));
            Assert.That(world.ServingCup, Is.Null);
            Assert.That(world.State.production.miniGameToken, Is.Null);
            Assert.That(world.State.production.miniGameCupId, Is.Null);
            Assert.That(world.State.production.miniGamePitcherId, Is.Null);
            Assert.That(JsonUtility.ToJson(pitcher), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(cup), Is.EqualTo(cupBefore));
            Assert.That(world.LiquidVolume(cup), Is.Zero);
        }

        [Test] public void DrawingTransfersOnlyFoamAndAccumulatesActualUsage()
        {
            string token = Start();
            Assert.That(world.ConsumeLatteArtFoam(token, 15), Is.EqualTo(15));
            Assert.That(world.ConsumeLatteArtFoam(token, 10), Is.EqualTo(10));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(75).Within(.001f));
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(25).Within(.001f));
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.EqualTo(25));
            Assert.That(cup.contents.Count, Is.EqualTo(2));
            Assert.That(world.TemperatureCelsius(cup), Is.EqualTo(60).Within(.001f));
        }

        [Test] public void CupCapacityClipsConsumptionWithoutSpilling()
        {
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 390, temperatureCelsius = 20 });
            string token = Start();
            Assert.That(world.ConsumeLatteArtFoam(token, 30), Is.EqualTo(10).Within(.001f));
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(400).Within(.001f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(90).Within(.001f));
            Assert.That(world.ConsumeLatteArtFoam(token, 30), Is.Zero);
            Assert.That(pitcher.production.spilledMl, Is.Zero);
            Assert.That(world.TemperatureCelsius(cup), Is.EqualTo(21).Within(.001f));
        }

        [Test] public void ExhaustionStopsDrawingAndMakesEmptyPitcherRefillable()
        {
            string token = Start(3);
            Assert.That(world.ConsumeLatteArtFoam(token, 10), Is.EqualTo(3));
            Assert.That(world.ConsumeLatteArtFoam(token, 1), Is.Zero);
            Assert.That(pitcher.contents, Is.Empty);
            Assert.That(world.ResolveProductionMiniGame(token, true, 90, "heart").success, Is.True);
            Assert.That(cup.latteArt, Is.True);
            Assert.That(world.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0, 0).success, Is.True);
        }

        [Test] public void CancellationKeepsTransferredFoamAndInvalidatesCallbacks()
        {
            string token = Start();
            world.ConsumeLatteArtFoam(token, 20);
            Assert.That(world.ResolveProductionMiniGame(token, false, 0).success, Is.True);
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(80).Within(.001f));
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(20).Within(.001f));
            Assert.That(world.ConsumeLatteArtFoam(token, 10), Is.Zero);
            Assert.That(world.ResolveProductionMiniGame(token, true, 100).success, Is.False);
        }

        [Test] public void ActiveSessionProtectsBothContainersAndRejectsNonFiniteAmounts()
        {
            string token = Start();
            Assert.That(world.Move(pitcher.id, AreaId.Counter, 1, 1).success, Is.False);
            Assert.That(world.Move(cup.id, AreaId.Counter, 1, 1).success, Is.False);
            Assert.That(world.EmptyProductionMachine(pitcher.id).success, Is.False);
            Assert.That(world.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0, 0).success, Is.False);
            Assert.That(world.ConsumeLatteArtFoam(token, float.NaN), Is.Zero);
            Assert.That(world.ConsumeLatteArtFoam(token, float.PositiveInfinity), Is.Zero);
            Assert.That(world.ConsumeLatteArtFoam(token, -1), Is.Zero);
            Assert.That(world.ResolveProductionMiniGame(token, true, 100).success, Is.False);
        }

        [Test] public void FullCupAndNonCupTargetsDoNotStartLatteArt()
        {
            pitcher.contents.Add(new Portion { specId = "milkfoam", units = 1, milliliters = 100 });
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 400 });
            Assert.That(world.Interact(pitcher.id, cup.id).success, Is.False);
            Assert.That(world.Interact(pitcher.id, "m.heater").success, Is.False);
            Assert.That(world.State.production.miniGameToken, Is.Null);
        }

        [Test] public void SavingRestoresPitcherCupAndCumulativeConsumption()
        {
            string token = Start(); world.ConsumeLatteArtFoam(token, 25);
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var restored = new CafeWorld(world.Catalog);
            Assert.That(restored.Restore(copy).success, Is.True);
            Assert.That(restored.State.production.miniGamePitcherId, Is.EqualTo(pitcher.id));
            Assert.That(restored.State.production.miniGameFoamUsedMl, Is.EqualTo(25));
            Assert.That(restored.ConsumeLatteArtFoam(token, 10), Is.EqualTo(10));
            Assert.That(restored.MilkFoamVolume(restored.Find(cup.id)), Is.EqualTo(35).Within(.001f));
        }

        [Test] public void InvalidSavedPitcherReferenceOrUsageIsRejectedAtomically()
        {
            Start();
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            copy.production.miniGamePitcherId = "m.heater";
            var before = world.State;
            Assert.That(world.Restore(copy).success, Is.False);
            Assert.That(world.State, Is.SameAs(before));
            copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            copy.production.miniGameFoamUsedMl = float.NaN;
            Assert.That(world.Restore(copy).success, Is.False);
        }

        [Test] public void RedrawDiscardsOnlyCurrentBatchAndDoesNotRefundPitcher()
        {
            var coffee = new Portion { specId = "coffee", units = 1, milliliters = 100, coffeeKind = "essence" };
            var milk = new Portion { specId = "milk", units = 1, milliliters = 80 };
            var originalFoam = new Portion { specId = "milkfoam", units = 1, milliliters = 15 };
            world.AddConstituent(originalFoam, "milk", 2, 15);
            cup.contents.Add(coffee); cup.contents.Add(milk); cup.contents.Add(originalFoam);
            string token = Start();
            world.ConsumeLatteArtFoam(token, 25);
            Assert.That(cup.contents.FindAll(p => p.specId == "milkfoam").Count, Is.EqualTo(2));
            Assert.That(world.ResetLatteArtFoam(token), Is.True);
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(15).Within(.001f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(75).Within(.001f));
            Assert.That(coffee.milliliters, Is.EqualTo(100)); Assert.That(milk.milliliters, Is.EqualTo(80));
            Assert.That(cup.contents, Does.Contain(originalFoam));
            Assert.That(originalFoam.composition[0].grams, Is.EqualTo(2));
            Assert.That(originalFoam.composition[0].milliliters, Is.EqualTo(15));
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(cup.production.spilledMl, Is.EqualTo(25).Within(.001f));
            Assert.That(world.LiquidVolume(cup) + world.LiquidVolume(pitcher) + cup.production.spilledMl,
                Is.EqualTo(295).Within(.001f), "Real inventory plus explicit waste is conserved.");
            Assert.That(world.ResolveProductionMiniGame(token, true, 0, "unknown").success, Is.False);
            Assert.That(world.ResetLatteArtFoam(token), Is.True, "Repeated redraw is idempotent.");
            Assert.That(cup.production.spilledMl, Is.EqualTo(25).Within(.001f));
            Assert.That(world.ConsumeLatteArtFoam(token, 10), Is.EqualTo(10));
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.EqualTo(10));
            Assert.That(world.ResolveProductionMiniGame(token, true, 0, "unknown").success, Is.True);
            Assert.That(world.LatteArtFoamVolume(cup), Is.EqualTo(25).Within(.001f));
            Assert.That(world.ResetLatteArtFoam(token), Is.False);
        }

        [TestCase(0, null)] [TestCase(5, "unknown")] [TestCase(70, "arbitrary")]
        public void CompletionDoesNotRequireScoreOrSpecificPattern(float score, string pattern)
        {
            string token = Start(); world.ConsumeLatteArtFoam(token, 20);
            Assert.That(world.LatteArtFoamVolume(cup), Is.Zero);
            Assert.That(world.ResolveProductionMiniGame(token, true, score, pattern).success, Is.True);
            Assert.That(cup.latteArt, Is.True);
            Assert.That(cup.latteArtScore, Is.EqualTo(score));
            Assert.That(world.LatteArtFoamVolume(cup), Is.EqualTo(20).Within(.001f));
            Assert.That(cup.contents.TrueForAll(p => string.IsNullOrEmpty(p.latteArtSessionToken)), Is.True);
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(world.ResetLatteArtFoam(token), Is.False);
        }

        [Test] public void CancelledAndCompletedEarlierBatchesCannotBeErasedByNewRedraw()
        {
            string first = Start(); world.ConsumeLatteArtFoam(first, 20);
            Assert.That(world.ResolveProductionMiniGame(first, false, 0).success, Is.True);
            Assert.That(world.LatteArtFoamVolume(cup), Is.Zero);
            Assert.That(cup.contents.Find(p => p.specId == "milkfoam").latteArtSessionToken, Is.Null);
            Assert.That(world.ResetLatteArtFoam(first), Is.False);
            Assert.That(world.RequestMilkPitcherLatteArt(pitcher.id, cup.id).success, Is.True);
            string second = world.State.production.miniGameToken;
            world.ConsumeLatteArtFoam(second, 10);
            Assert.That(world.ResolveProductionMiniGame(second, true, 0).success, Is.True);
            Assert.That(world.LatteArtFoamVolume(cup), Is.EqualTo(30).Within(.001f));
            Assert.That(world.RequestMilkPitcherLatteArt(pitcher.id, cup.id).success, Is.True);
            string third = world.State.production.miniGameToken;
            world.ConsumeLatteArtFoam(third, 15);
            Assert.That(world.ResetLatteArtFoam(first), Is.False);
            Assert.That(world.ResetLatteArtFoam(third), Is.True);
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(30).Within(.001f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(55).Within(.001f));
            Assert.That(world.LatteArtFoamVolume(cup), Is.Zero);
            world.CancelProductionMiniGame();
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.Zero);
        }

        [Test] public void RedrawAfterRestoringPreservesOriginalFoamAndAllProvenance()
        {
            var oldFoam = new Portion { specId = "milkfoam", units = 1, milliliters = 12 };
            world.AddConstituent(oldFoam, "milk", 3, 12); cup.contents.Add(oldFoam);
            pitcher.contents.Add(new Portion { specId = "milk", units = 1, milliliters = 100, grams = 4 });
            string token = Start(100); world.ConsumeLatteArtFoam(token, 20);
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(copy).success, Is.True);
            Assert.That(world.ResetLatteArtFoam(token), Is.True);
            var remaining = world.Find(cup.id).contents.Find(p => p.specId == "milkfoam");
            Assert.That(remaining.milliliters, Is.EqualTo(12).Within(.001f));
            Assert.That(remaining.composition[0].grams, Is.EqualTo(3));
            Assert.That(remaining.composition[0].milliliters, Is.EqualTo(12));
            Assert.That(world.MilkFoamVolume(world.Find(pitcher.id)), Is.EqualTo(180).Within(.001f));
        }

        [Test] public void LegacyUnmarkedActiveBatchRestoresLiquidButSafelyEndsSession()
        {
            string token = Start(); world.ConsumeLatteArtFoam(token, 25);
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            foreach (var portion in copy.entities.Find(e => e.id == cup.id).contents) portion.latteArtSessionToken = null;
            Assert.That(world.Restore(copy).success, Is.True);
            Assert.That(world.State.production.miniGameToken, Is.Null);
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(world.MilkFoamVolume(world.Find(cup.id)), Is.EqualTo(25).Within(.001f));
            Assert.That(world.MilkFoamVolume(world.Find(pitcher.id)), Is.EqualTo(75).Within(.001f));
            Assert.That(world.ResetLatteArtFoam(token), Is.False);
        }

        [TestCase("usage")] [TestCase("token")] [TestCase("cup")]
        public void InconsistentSavedBatchSourcesAreRejectedAtomically(string corruption)
        {
            Start(); world.ConsumeLatteArtFoam(world.State.production.miniGameToken, 20);
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            var foam = copy.entities.Find(e => e.id == cup.id).contents.Find(p => p.specId == "milkfoam");
            if (corruption == "usage") copy.production.miniGameFoamUsedMl = 21;
            else if (corruption == "token") foam.latteArtSessionToken = "stale";
            else
            {
                copy.entities.Find(e => e.id == cup.id).contents.Remove(foam);
                copy.entities.Find(e => e.id == pitcher.id).contents.Add(foam);
            }
            var before = world.State;
            Assert.That(world.Restore(copy).success, Is.False);
            Assert.That(world.State, Is.SameAs(before));
        }

        [Test] public void InvalidRedrawTokenDoesNotChangeInventory()
        {
            string token = Start(); world.ConsumeLatteArtFoam(token, 20);
            string before = JsonUtility.ToJson(world.State);
            Assert.That(world.ResetLatteArtFoam(null), Is.False);
            Assert.That(world.ResetLatteArtFoam("stale"), Is.False);
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(before));
        }

        [TestCase(true)] [TestCase(false)]
        public void LegacyCompletionFlagAndFoamRemainCompatibleWithoutPatternGuessing(bool completed)
        {
            cup.contents.Add(new Portion { specId = "milkfoam", units = 1, milliliters = 25 });
            cup.latteArt = completed; cup.latteArtScore = 10; cup.latteArtPattern = "unknown";
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            Assert.That(world.Restore(copy).success, Is.True);
            var restoredCup = world.Find(cup.id);
            Assert.That(restoredCup.contents[0].specId, Is.EqualTo("milkfoam"));
            Assert.That(world.MilkFoamVolume(restoredCup), Is.EqualTo(25));
            Assert.That(world.LatteArtFoamVolume(restoredCup), Is.EqualTo(completed ? 25 : 0));
            Assert.That(restoredCup.latteArtPattern, Is.EqualTo("unknown"));
        }

        [Test] public void LegacyProductionSnapshotGetsPitcherAndRemovesObsoleteEntry()
        {
            var copy = JsonUtility.FromJson<CafeSnapshot>(JsonUtility.ToJson(world.State));
            copy.entities.RemoveAll(entity => entity.id == pitcher.id);
            copy.entities.Add(new WorldEntity { id = "tool.latte-art", specId = "tool.latte-art", area = AreaId.Counter });
            Assert.That(world.Restore(copy).success, Is.True);
            Assert.That(world.Find("tool.latte-art"), Is.Null);
            Assert.That(world.Find("tool.milk-pitcher"), Is.Not.Null);
        }
    }
}
