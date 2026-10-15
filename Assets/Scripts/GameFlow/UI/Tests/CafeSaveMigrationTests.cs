using System;
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

    public sealed class PlayerCafeOwnerMigrationTests
    {
        private static CafeWorld World() => new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));

        [SetUp]
        public void SetUp() => PlayerCafeOwner.EndSession();

        [TearDown]
        public void TearDown()
        {
            PlayerCafeOwner.EndSession();
            GameSaveParticipantRegistry.Capturers.Remove(PlayerCafeOwner.SaveId);
            GameSaveParticipantRegistry.Restorers.Remove(PlayerCafeOwner.SaveId);
        }

        [Test]
        public void MapWorldHandsOffToSceneWithoutDuplicateRegistrationAndUsesSceneRestore()
        {
            var map = World();
            map.TryInitializeMoney(777, out _);
            PlayerCafeOwner.Bind(map);
            var capture = GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId];
            var restorer = GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId];
            string saved = capture();
            var scene = World();
            int refreshes = 0;
            Action<string> restore = json =>
            {
                var result = scene.Restore(CafeSaveMigration.Prepare(JsonUtility.FromJson<CafeSnapshot>(json)));
                if (!result.success) throw new FormatException(result.message);
                ++refreshes;
            };
            Assert.DoesNotThrow(() => PlayerCafeOwner.Bind(scene, restore));
            Assert.That(GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId], Is.EqualTo(capture));
            Assert.That(GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId], Is.EqualTo(restorer));
            restorer(saved);
            Assert.That(refreshes, Is.EqualTo(1));
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(scene));
            Assert.That(scene.State.money, Is.EqualTo(777));
            Assert.That(JsonUtility.FromJson<CafeSnapshot>(capture()).money, Is.EqualTo(777));
        }

        [Test]
        public void ParallelSceneOrForeignRegistrationFailsWithoutReplacingOwner()
        {
            var first = World();
            Action<string> restore = _ => { };
            PlayerCafeOwner.Bind(first, restore);
            var capture = GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId];
            var restorer = GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId];
            Assert.Throws<InvalidOperationException>(() => PlayerCafeOwner.Bind(World(), _ => { }));
            Assert.Throws<InvalidOperationException>(() => PlayerCafeOwner.Bind(World()));
            PlayerCafeOwner.Unbind(World());
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(first));
            Assert.That(GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId], Is.EqualTo(capture));
            Assert.That(GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId], Is.EqualTo(restorer));

            PlayerCafeOwner.EndSession();
            Action<string> foreign = _ => { };
            GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId] = foreign;
            Assert.Throws<InvalidOperationException>(() => PlayerCafeOwner.Bind(World()));
            Assert.That(PlayerCafeOwner.Current, Is.Null);
            Assert.That(GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId], Is.SameAs(foreign));
            PlayerCafeOwner.EndSession();
            Assert.That(GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId], Is.SameAs(foreign));
        }

        [Test]
        public void SceneUnloadKeepsSessionRegisteredAndDoesNotCallDestroyedAdapter()
        {
            var world = World();
            int refreshes = 0;
            PlayerCafeOwner.Bind(world, _ => ++refreshes);
            var capture = GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId];
            var restorer = GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId];
            string saved = capture();
            world.TryInitializeMoney(777, out _);
            PlayerCafeOwner.Unbind(world);
            restorer(saved);
            Assert.That(refreshes, Is.Zero);
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(capture(), Is.EqualTo(saved));
            Assert.That(GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId], Is.EqualTo(capture));
            Assert.That(GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId], Is.EqualTo(restorer));
            Assert.DoesNotThrow(() => PlayerCafeOwner.Bind(World(), _ => { }));
            PlayerCafeOwner.EndSession();
            Assert.That(GameSaveParticipantRegistry.Capturers.ContainsKey(PlayerCafeOwner.SaveId), Is.False);
            Assert.That(GameSaveParticipantRegistry.Restorers.ContainsKey(PlayerCafeOwner.SaveId), Is.False);
        }

        [Test]
        public void SceneRestoreFailureIsReportedAndRethrownUntilSuccessfulRestore()
        {
            var failure = new FormatException("scene restore rejected");
            bool reject = true;
            PlayerCafeOwner.Bind(World(), _ => { if (reject) throw failure; });
            var restorer = GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId];
            Assert.That(Assert.Throws<FormatException>(() => restorer("saved")), Is.SameAs(failure));
            Assert.That(PlayerCafeOwner.RestoreError, Is.EqualTo(failure.Message));
            reject = false;
            restorer("saved");
            Assert.That(PlayerCafeOwner.RestoreError, Is.Null);
        }

        [Test]
        public void SessionRestoreMigratesOnlyObservedRoundingAndRejectsRealCorruption()
        {
            var world = World();
            world.EnableProduction();
            var brewer = world.Find("m.brewer");
            brewer.contents.Add(new Portion { specId = "coffee", units = 1,
                milliliters = 66.1263885f, productionScore = 100.0000228881836f });
            brewer.production.phase = ProductionPhase.Room;
            string legacy = JsonUtility.ToJson(world.State);
            PlayerCafeOwner.Bind(world);
            var restorer = GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId];
            restorer(legacy);
            Assert.That(world.Find("m.brewer").contents[0].productionScore, Is.EqualTo(100f));
            string valid = JsonUtility.ToJson(world.State);
            var invalid = JsonUtility.FromJson<CafeSnapshot>(valid);
            invalid.entities.Find(e => e.id == "m.brewer").contents[0].productionScore = 100.01f;
            Assert.Throws<FormatException>(() => restorer(JsonUtility.ToJson(invalid)));
            Assert.That(PlayerCafeOwner.RestoreError, Does.Contain("液体状态无效"));
            Assert.That(JsonUtility.ToJson(world.State), Is.EqualTo(valid));
            restorer(valid);
            Assert.That(PlayerCafeOwner.RestoreError, Is.Null);
        }
    }
}
