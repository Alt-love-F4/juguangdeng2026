using System;
using NUnit.Framework;
using Spotlight.FreeCafe;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PlayerCafeOwnerTests
    {
        private static CafeWorld World() => new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
        private static Func<string> Capture => GameSaveParticipantRegistry.Capturers[PlayerCafeOwner.SaveId];
        private static Action<string> Restore => GameSaveParticipantRegistry.Restorers[PlayerCafeOwner.SaveId];

        [SetUp]
        public void SetUp() => PlayerCafeOwner.EndSession();

        [TearDown]
        public void TearDown() => PlayerCafeOwner.EndSession();

        [Test]
        public void BindWorldOnlyRegistersDomainCaptureAndRestore()
        {
            var world = World();
            PlayerCafeOwner.ReportRestoreFailure("previous session");
            PlayerCafeOwner.Bind(world);
            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(PlayerCafeOwner.RestoreError, Is.Null);
            AssertDomainRoundTrip(world);
        }

        [Test]
        public void BindSceneDelegatesReplacesDomainRegistrationAndRoutesBothOperations()
        {
            var world = World();
            PlayerCafeOwner.Bind(world);
            string restored = null;
            int captures = 0;
            Func<string> capture = () => { captures++; return "scene snapshot"; };
            Action<string> restore = json => restored = json;
            PlayerCafeOwner.Bind(world, capture, restore);

            Assert.That(Capture, Is.SameAs(capture));
            Assert.That(Restore, Is.SameAs(restore));
            Assert.That(Capture(), Is.EqualTo("scene snapshot"));
            Restore("restore through scene");
            Assert.That(captures, Is.EqualTo(1));
            Assert.That(restored, Is.EqualTo("restore through scene"));
        }

        [Test]
        public void UnbindCurrentWorldReleasesSceneDelegatesButPreservesSessionAndError()
        {
            var world = World();
            PlayerCafeOwner.Bind(world,
                () => throw new InvalidOperationException("Scene capture must be released"),
                _ => throw new InvalidOperationException("Scene restore must be released"));
            PlayerCafeOwner.ReportRestoreFailure("retained error");
            PlayerCafeOwner.Unbind(world);

            Assert.That(PlayerCafeOwner.Current, Is.SameAs(world));
            Assert.That(PlayerCafeOwner.RestoreError, Is.EqualTo("retained error"));
            Assert.That(Capture.Target, Is.Null, "Fallback must not retain a scene delegate target");
            Assert.That(Restore.Target, Is.Null, "Fallback must not retain a scene delegate target");
            AssertDomainRoundTrip(world);
            PlayerCafeOwner.Unbind(world);
            AssertDomainRoundTrip(world);
        }

        [Test]
        public void OldWorldUnbindDoesNotOverwriteNewOwnersSceneDelegates()
        {
            var oldWorld = World();
            var newWorld = World();
            PlayerCafeOwner.Bind(oldWorld);
            Func<string> capture = () => "new scene";
            Action<string> restore = _ => { };
            PlayerCafeOwner.Bind(newWorld, capture, restore);
            PlayerCafeOwner.ReportRestoreFailure("new owner error");
            PlayerCafeOwner.Unbind(oldWorld);

            Assert.That(PlayerCafeOwner.Current, Is.SameAs(newWorld));
            Assert.That(Capture, Is.SameAs(capture));
            Assert.That(Restore, Is.SameAs(restore));
            Assert.That(PlayerCafeOwner.RestoreError, Is.EqualTo("new owner error"));
        }

        [Test]
        public void EndSessionClearsOwnerAndRegistrationAndLateUnbindDoesNotResurrectThem()
        {
            var world = World();
            PlayerCafeOwner.Bind(world, () => "scene", _ => { });
            PlayerCafeOwner.ReportRestoreFailure("failure");
            PlayerCafeOwner.EndSession();
            PlayerCafeOwner.Unbind(world);
            PlayerCafeOwner.Unbind(null);
            PlayerCafeOwner.EndSession();

            Assert.That(PlayerCafeOwner.Current, Is.Null);
            Assert.That(PlayerCafeOwner.RestoreError, Is.Null);
            Assert.That(GameSaveParticipantRegistry.Capturers.ContainsKey(PlayerCafeOwner.SaveId), Is.False);
            Assert.That(GameSaveParticipantRegistry.Restorers.ContainsKey(PlayerCafeOwner.SaveId), Is.False);
        }

        [Test]
        public void NewSessionWithoutSceneDelegatesDoesNotReusePreviousSceneDelegates()
        {
            PlayerCafeOwner.Bind(World(),
                () => throw new InvalidOperationException("Old session capture"),
                _ => throw new InvalidOperationException("Old session restore"));
            PlayerCafeOwner.EndSession();
            var world = World();
            PlayerCafeOwner.Bind(world);
            AssertDomainRoundTrip(world);
        }

        private static void AssertDomainRoundTrip(CafeWorld world)
        {
            world.State.money = 321;
            string json = Capture();
            Assert.That(JsonUtility.FromJson<CafeSnapshot>(json).money, Is.EqualTo(321));
            world.State.money = 7;
            Restore(json);
            Assert.That(world.State.money, Is.EqualTo(321));
        }
    }
}
