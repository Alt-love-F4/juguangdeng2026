using NUnit.Framework;
using Spotlight.FreeCafe;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class CafeSaveMigrationTests
    {
        private static CafeWorld World() => new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
        private static CafeSnapshot WithScore(float score)
        {
            var world = World();
            world.EnableProduction();
            var brewer = world.Find("m.brewer");
            brewer.contents.Add(new Portion { specId = "coffee", units = 1,
                milliliters = 66.1263885f, productionScore = score });
            brewer.production.phase = ProductionPhase.Room;
            return world.State;
        }

        [Test]
        public void ObservedLegacyRoundingIsRepairedOnCloneAndStrictRestoreSucceeds()
        {
            var source = WithScore(100.0000228881836f);
            string original = JsonUtility.ToJson(source);
            var rejected = World().Restore(JsonUtility.FromJson<CafeSnapshot>(original));
            Assert.That(rejected.success, Is.False);
            Assert.That(rejected.message, Does.Contain("液体状态无效"));
            var migrated = CafeSaveMigration.Prepare(source);
            Assert.That(migrated, Is.Not.SameAs(source));
            Assert.That(migrated.entities.Find(e => e.id == "m.brewer").contents[0].productionScore, Is.EqualTo(100));
            var restored = World().Restore(migrated);
            Assert.That(restored.success, Is.True, restored.message);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
        }

        [TestCase(100.01f)]
        [TestCase(-0.00001f)]
        [TestCase(-1f)]
        public void MigrationDoesNotConcealInvalidScores(float score)
        {
            var migrated = CafeSaveMigration.Prepare(WithScore(score));
            Assert.That(migrated.entities.Find(e => e.id == "m.brewer").contents[0].productionScore, Is.EqualTo(score));
            var rejected = World().Restore(migrated);
            Assert.That(rejected.success, Is.False);
            Assert.That(rejected.message, Does.Contain("液体状态无效"));
        }

        [Test]
        public void LegalScoresAndLiquidQuantitiesAreUnchanged()
        {
            var source = WithScore(73.25f);
            var migrated = CafeSaveMigration.Prepare(source);
            Assert.That(JsonUtility.ToJson(migrated), Is.EqualTo(JsonUtility.ToJson(source)));
        }

        [Test]
        public void RepeatedFractionalPoursKeepPerfectScoreWithinStrictSaveRange()
        {
            var world = World();
            world.EnableProduction();
            world.EnterProduction();
            var liquid = new WorldEntity { id = "score.test.water", specId = "water", units = 1, area = AreaId.Counter };
            world.State.entities.Add(liquid);
            Assert.That(world.Interact(liquid.id, "m.heater").success, Is.True);
            for (int i = 0; i < 200; i++)
                Assert.That(world.PourLiquid("m.heater", "m.brewer", .331f, .01f).success, Is.True);
            var portions = world.Find("m.brewer").contents;
            Assert.That(portions.Count, Is.GreaterThan(0));
            foreach (var portion in portions) Assert.That(portion.productionScore, Is.EqualTo(100f));
            Assert.That(World().Restore(world.State).success, Is.True);
        }
    }
}
