using System;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.GameFlow.Tests
{
    public sealed class PlayerSessionTests
    {
        private GameObject host;
        private NarrativeRuntime narrative;
        private DayFlowController days;
        private CafeWorld world;
        private PlayerSessionCoordinator coordinator;
        private PlayerRuntime previous;
        private PlayerCreationCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            previous = PlayerRuntime.Current;
            catalog = PlayerCreationCatalog.Load();
            PlayerRuntime.Current = new PlayerRuntime(catalog);
            host = new GameObject("Player session integration test");
            host.SetActive(false); // No Awake singleton registration or scene startup.
            narrative = host.AddComponent<NarrativeRuntime>();
            narrative.Initialize(LubanNarrativeLoader.Load(), new ImmediateNarrativePresenter());
            days = host.AddComponent<DayFlowController>();
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(Resources.Load<TextAsset>("FreeCafe/catalog").text));
            coordinator = new PlayerSessionCoordinator();
        }

        [TearDown]
        public void TearDown()
        {
            coordinator?.Dispose();
            PlayerRuntime.Current = previous;
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
        }

        private int Bar(string key)
        {
            Assert.That(narrative.Controller.State.TryGetBar(key, out int value), Is.True, key);
            return value;
        }

        private FirstDayLetterDraft Draft(int origin)
        {
            var draft = new FirstDayLetterDraft(catalog);
            draft.SetName("Ada");
            for (int group = 0; group < 5; group++) Assert.That(draft.Select(group, group == 0 ? origin : 0), Is.True);
            return draft;
        }

        [TestCase(0, "waif", 250)]
        [TestCase(1, "commoner", 500)]
        [TestCase(2, "noble", 1000)]
        public void CreationUpdatesRealOwnersAndPlayerReadsSameValues(int origin, string originId, int money)
        {
            coordinator.Bind(narrative, world, days);
            Assert.That(coordinator.Submit(Draft(origin)), Is.True);
            Assert.That(PlayerRuntime.Current.CreationSubmitted, Is.True);
            Assert.That(PlayerRuntime.Current.OriginId, Is.EqualTo(originId));
            Assert.That(world.State.money, Is.EqualTo(money));
            Assert.That(PlayerRuntime.Current.TryReadMoney(out int observed), Is.EqualTo(PlayerReadStatus.Success));
            Assert.That(observed, Is.EqualTo(money));
            Assert.That(Bar("player.language"), Is.EqualTo(5));
            Assert.That(Bar("player.courage"), Is.EqualTo(2));
            Assert.That(Bar("player.wisdom"), Is.Zero);
            Assert.That(Bar("player.perception"), Is.EqualTo(2));
            Assert.That(narrative.Controller.State.GetCondition("specialization.player.letter.religion"), Is.True);
            Assert.That(narrative.Actions.ActionPoints.Current, Is.EqualTo(catalog.ActionPointMax));
            Assert.That(narrative.Controller.ValuesReady, Is.False, "Coordinator must leave initial evaluation to GameFlow.");
        }

        [Test]
        public void AbilityReaderFollowsSessionBindingResetAndDisposal()
        {
            coordinator.Bind(narrative, world, days);
            coordinator.Submit(Draft(0));
            Assert.That(narrative.Controller.AbilityReader, Is.Not.Null);
            Assert.That(narrative.Controller.AbilityReader("perception"), Is.EqualTo((true, 2)));

            var previousController = narrative.Controller;
            narrative.ResetSession();
            Assert.That(narrative.Controller, Is.Not.SameAs(previousController));
            Assert.That(narrative.Presenter, Is.Null, "Reset must keep the injected presenter instead of creating runtime UI.");
            coordinator.Bind(narrative, world, days);
            narrative.SetBar("player.perception", 7);
            Assert.That(narrative.Controller.AbilityReader("perception"), Is.EqualTo((true, 7)));

            coordinator.Dispose();
            Assert.That(narrative.AbilityReader, Is.Null);
            Assert.That(narrative.Controller.AbilityReader, Is.Null);
        }

        [Test]
        public void RepeatedSubmissionDoesNotResetMoneyAttributesOrConsumedPoints()
        {
            coordinator.Bind(narrative, world, days);
            Assert.That(coordinator.Submit(Draft(0)), Is.True);
            Assert.That(world.TryAddMoney(73, out _), Is.True);
            narrative.Actions.ActionPoints.Set(2);
            narrative.SetBar("player.language", 9);
            Assert.That(coordinator.Submit(Draft(2)), Is.True);
            Assert.That(world.State.money, Is.EqualTo(323));
            Assert.That(narrative.Actions.ActionPoints.Current, Is.EqualTo(2));
            Assert.That(Bar("player.language"), Is.EqualTo(9));
            Assert.That(PlayerRuntime.Current.OriginId, Is.EqualTo("waif"));
        }

        [Test]
        public void UnboundOwnersRejectSubmissionWithoutCommittingIdentity()
        {
            int before = world.State.money;
            Assert.Throws<InvalidOperationException>(() => coordinator.Submit(Draft(0)));
            Assert.That(PlayerRuntime.Current.CreationSubmitted, Is.False);
            Assert.That(world.State.money, Is.EqualTo(before));
            Assert.That(Bar("player.language"), Is.Zero);
        }

        [Test]
        public void ExceptionAfterMoneyWriteRollsBackOwnersAndAllowsCleanRetry()
        {
            coordinator.Bind(narrative, world, days);
            world.TryInitializeMoney(777, out _);
            narrative.Actions.ActionPoints.Set(2);
            string originalBars = JsonUtility.ToJson(narrative.Controller.State.Capture());
            bool injected = false;
            Action failOnce = () =>
            {
                if (injected) return;
                injected = true;
                throw new InvalidOperationException("Injected owner notification failure after money write");
            };
            world.Changed += failOnce;
            try { Assert.Throws<InvalidOperationException>(() => coordinator.Submit(Draft(0))); }
            finally { world.Changed -= failOnce; }
            Assert.That(injected, Is.True);
            Assert.That(PlayerRuntime.Current.CreationSubmitted, Is.False);
            Assert.That(world.State.money, Is.EqualTo(777));
            Assert.That(narrative.Actions.ActionPoints.Current, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(narrative.Controller.State.Capture()), Is.EqualTo(originalBars));
            Assert.That(coordinator.Submit(Draft(0)), Is.True);
            Assert.That(world.State.money, Is.EqualTo(250));
            Assert.That(Bar("player.language"), Is.EqualTo(5));
        }
    }
}
