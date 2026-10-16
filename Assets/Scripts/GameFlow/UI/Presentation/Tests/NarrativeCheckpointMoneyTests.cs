using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeCheckpointMoneyTests
    {
        // Fixture-owned protocol only: these IDs are not formal chapter content or permanent facts.
        private const string Prefix = "fixture.checkpoint-money.";
        private const string Chapter = Prefix + "chapter";
        private const string Complete = Prefix + "complete";
        private const string Reported = Prefix + "reported";
        private const string FeeOne = Prefix + "fee.one";
        private const string FeeFree = Prefix + "fee.free";
        private const string FeeOneOwner = Prefix + "fee.one.result";
        private const string MoneyResult = Prefix + "money-add-1";
        private const int Day = 2;

        private sealed class CheckpointPresenter : INarrativePresenter, INarrativePresentationCheckpoints
        {
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            public readonly List<Action> Completions = new List<Action>();
            public NarrativeEventRequest Current => Requests[Requests.Count - 1];
            public Action Completion => Completions[Completions.Count - 1];
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Requests.Add(request);
                Completions.Add(completed);
            }
            public void Wait(string page, NarrativeCheckpointWait wait) => Emit(Current, page, wait);
            public void Emit(NarrativeEventRequest request, string page, NarrativeCheckpointWait wait)
                => CheckpointReached?.Invoke(request, page, wait);
        }

        // Rejections exercise the public money boundary while successful reads/writes still use CafeWorld.
        private sealed class RejectingMoneyAccount : INarrativeMoneyAccount
        {
            private readonly CafeNarrativeValueProvider provider;
            public bool RejectReads;
            public bool RejectCommits;
            public int CommitAttempts;
            public int SuccessfulCommits;
            public RejectingMoneyAccount(CafeNarrativeValueProvider provider) => this.provider = provider;
            public bool TryRead(out int balance)
            {
                if (!RejectReads) return provider.TryRead(out balance);
                balance = 0;
                return false;
            }
            public bool TryCommit(int expectedBalance, int finalBalance, out string error)
            {
                CommitAttempts++;
                if (RejectCommits)
                {
                    error = "fixture rejected commit";
                    return false;
                }
                bool committed = provider.TryCommit(expectedBalance, finalBalance, out error);
                if (committed) SuccessfulCommits++;
                return committed;
            }
        }

        private CafeWorld world;
        private CafeNarrativeValueProvider provider;
        private NarrativeController controller;
        private CheckpointPresenter presenter;
        private readonly List<string> effects = new List<string>();
        private readonly List<bool> entryCompletions = new List<bool>();
        private readonly List<int> walletChanges = new List<int>();
        private int lastMoney;
        private int eventCompletions;
        private int eventFailures;

        [SetUp]
        public void SetUp()
        {
            var catalog = Resources.Load<TextAsset>("FreeCafe/catalog");
            Assert.That(catalog, Is.Not.Null);
            world = new CafeWorld(JsonUtility.FromJson<CafeCatalog>(catalog.text));
            provider = new CafeNarrativeValueProvider(world);
            presenter = new CheckpointPresenter();
            controller = new NarrativeController(Configuration(), presenter);
            controller.BeginValueRestore();
            controller.BindValueProvider(provider);
            controller.MoneyAccount = provider;
            controller.CompleteValueRestore(false);
            controller.SetOfficialDay(Day);
            controller.ProcessOfficialDay(Day);
            effects.Clear(); entryCompletions.Clear(); walletChanges.Clear();
            eventCompletions = eventFailures = 0;
            lastMoney = world.State.money;
            world.Changed += ObserveWallet;
            controller.EffectRequested += effect => effects.Add(effect.Result.resultId);
            controller.EventCompleted += id =>
            {
                Assert.That(id, Is.EqualTo(Chapter));
                eventCompletions++;
            };
            controller.EventFailed += (id, reason) =>
            {
                Assert.That(id, Is.EqualTo(Chapter));
                eventFailures++;
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (world != null) world.Changed -= ObserveWallet;
            controller?.UnbindValueProviders();
            provider?.Dispose();
        }

        private static NarrativeConfiguration Configuration()
        {
            var config = new NarrativeConfiguration();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999,
                initValue = Day, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.bars.Add(new NarrativeBarDefinition { barKey = "cafe.money", sourceMode = NarrativeSourceMode.External,
                providerId = "cafe", sourceKey = "money", max = int.MaxValue });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = Complete, onlySetTrue = true });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = Reported, onlySetTrue = true });
            config.events.Add(new NarrativeEventDefinition { eventId = Chapter, onceOnly = true,
                blocksSave = true, entry = NarrativeEntryPoint.MorningBeforeMap });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = Chapter, day = Day,
                timeSlot = NarrativeTimeSlot.Morning, repeatRule = NarrativeRepeatRule.Once });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = FeeOne, groupKey = Prefix + "fee",
                resultOwnerId = FeeOneOwner });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = FeeFree, groupKey = Prefix + "fee" });
            config.results.Add(new NarrativeResultDefinition { resultId = Complete, eventId = Chapter,
                resultType = NarrativeResultType.SetFlag, target = Complete, value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = Reported, eventId = Chapter, seq = 1,
                resultType = NarrativeResultType.SetFlag, target = Reported, value = "true" });
            config.results.Add(new NarrativeResultDefinition { resultId = MoneyResult, eventId = FeeOneOwner,
                resultType = NarrativeResultType.MoneyAdd, target = "cafe", value = "1" });
            return config;
        }

        private void ObserveWallet()
        {
            if (lastMoney == world.State.money) return;
            lastMoney = world.State.money;
            walletChanges.Add(lastMoney);
        }

        private void AssertUnsettled(int expectedMoney)
        {
            Assert.That(world.State.money, Is.EqualTo(expectedMoney));
            Assert.That(controller.State.TryGetBar("cafe.money", out int observed), Is.True);
            Assert.That(observed, Is.EqualTo(expectedMoney), "The narrative must read the real cafe wallet.");
            Assert.That(controller.State.GetCondition(Complete), Is.False);
            Assert.That(controller.State.GetCondition(Reported), Is.False);
            Assert.That(controller.State.HasHappenedEvent(Chapter), Is.False);
            Assert.That(controller.State.IsFlagSet("event.once." + Chapter), Is.False);
            Assert.That(eventCompletions, Is.Zero);
            Assert.That(effects, Is.Empty);
        }

        private void StartAndWaitForBusiness(string choice)
        {
            int initial = world.State.money;
            Assert.That(controller.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap, entryCompletions.Add), Is.True);
            Assert.That(controller.SelectChoice(choice), Is.True);
            Assert.That(controller.SelectChoice(choice), Is.True, "Repeated selection is idempotent.");
            Assert.That(controller.SelectChoice(choice == FeeOne ? FeeFree : FeeOne), Is.False);
            AssertUnsettled(initial);
            presenter.Wait("fixture.afternoon", NarrativeCheckpointWait.Business);
            Assert.That(controller.HasPendingBusinessCheckpoint, Is.True);
            Assert.That(controller.IsSafeBoundary, Is.True);
            Assert.That(controller.BlocksSave, Is.False);
            Assert.That(entryCompletions, Is.EqualTo(new[] { true }));
            AssertUnsettled(initial);
            Assert.That(walletChanges, Is.Empty, "Neither selection nor Business Wait may write the wallet.");
        }

        private void RunRealBusinessOnce()
        {
            Assert.That(controller.TryBeginCheckpointBusiness(Day, out string token), Is.True);
            Assert.That(controller.TryBeginCheckpointBusiness(Day, out _), Is.False);
            int money = world.State.money;
            int reputation = world.State.reputation;
            int cafeDay = world.State.day;
            Assert.That(world.State.open, Is.False);
            Assert.That(world.StartDay().success, Is.True);
            Assert.That(world.State.open, Is.True);
            Assert.That(world.State.businessPhase, Is.EqualTo(BusinessPhase.Open));
            AssertUnsettled(money);
            Assert.That(world.EndDay().success, Is.True);
            Assert.That(world.State.open, Is.False);
            Assert.That(world.State.businessPhase, Is.EqualTo(BusinessPhase.Night));
            Assert.That(world.State.lastSummary, Is.Not.Null.And.Not.Empty);
            Assert.That(world.State.reputation, Is.EqualTo(reputation - 10));
            Assert.That(world.State.day, Is.EqualTo(cafeDay));
            AssertUnsettled(money);
            Assert.That(walletChanges, Is.Empty, "Opening/closing changes cafe state independently of the deferred fee.");
            Assert.That(controller.ConfirmCheckpointBusiness(Day, token), Is.True);
            Assert.That(controller.ConfirmCheckpointBusiness(Day, token), Is.False);
        }

        private void ResumeAndWaitForNight(string requestId, string choice)
        {
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness, entryCompletions.Add), Is.True);
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.Event.eventId, Is.EqualTo(Chapter));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("fixture.afternoon"));
            Assert.That(controller.SelectedChoices, Is.EqualTo(new[] { choice }));
            Assert.That(controller.CurrentCheckpointStage, Is.EqualTo(1));
            presenter.Wait("fixture.night", NarrativeCheckpointWait.Night);
            Assert.That(controller.HasPendingNightCheckpoint, Is.True);
            AssertUnsettled(world.State.money);
        }

        private void AdjustRealMoney(int delta)
        {
            int current = world.State.money;
            Assert.That(world.TryCommitMoney(current, current + delta, out string error), Is.True, error);
            controller.FlushValueChanges();
            AssertUnsettled(current + delta);
        }

        private void Restore(NarrativeSnapshot snapshot)
        {
            controller.BeginValueRestore();
            controller.RestoreSnapshot(JsonUtility.FromJson<NarrativeSnapshot>(JsonUtility.ToJson(snapshot)));
            controller.SetOfficialDay(Day);
            controller.CompleteValueRestore(false);
        }

        private void AssertSettled(int expectedMoney, bool paid)
        {
            Assert.That(world.State.money, Is.EqualTo(expectedMoney));
            Assert.That(controller.State.GetCondition(Complete), Is.True);
            Assert.That(controller.State.GetCondition(Reported), Is.True);
            Assert.That(controller.State.HasHappenedEvent(Chapter), Is.True);
            Assert.That(controller.State.IsFlagSet("event.once." + Chapter), Is.True);
            Assert.That(eventCompletions, Is.EqualTo(1));
            Assert.That(effects.Count(id => id == Complete), Is.EqualTo(1));
            Assert.That(effects.Count(id => id == Reported), Is.EqualTo(1));
            Assert.That(effects.Count(id => id == MoneyResult), Is.EqualTo(paid ? 1 : 0));
            Assert.That(effects.Count, Is.EqualTo(paid ? 3 : 2));
            var snapshot = controller.CaptureSnapshot();
            Assert.That(snapshot.checkpoint, Is.Null);
            Assert.That(snapshot.happenedEvents.Count(id => id == Chapter), Is.EqualTo(1));
            Assert.That(snapshot.flags.Count(flag => flag.key == "event.once." + Chapter && flag.value), Is.EqualTo(1));
            Assert.That(snapshot.bars.Any(bar => bar.key == "cafe.money"), Is.False);
            Assert.That(controller.BlocksSave, Is.False);
        }

        [Test]
        public void FeeOneBusinessAndNightRestoreCommitOnceAgainstLatestRealWallet()
        {
            int initial = world.State.money;
            StartAndWaitForBusiness(FeeOne);
            string requestId = presenter.Current.RequestId;
            var morning = presenter.Current;
            Action oldMorning = presenter.Completion;
            RunRealBusinessOnce();
            Restore(controller.CaptureSnapshot());
            Assert.That(controller.BusinessCheckpointConfirmed, Is.True);
            Assert.That(controller.TryBeginCheckpointBusiness(Day, out _), Is.False);
            ResumeAndWaitForNight(requestId, FeeOne);
            var afternoon = presenter.Current;
            Action oldAfternoon = presenter.Completion;
            Assert.That(walletChanges, Is.Empty, "Night Wait must not pay the fee either.");
            AdjustRealMoney(-17);
            int latest = world.State.money;
            Assert.That(latest, Is.EqualTo(initial - 17));
            Restore(controller.CaptureSnapshot());
            oldMorning(); oldAfternoon();
            presenter.Emit(morning, "fixture.stale", NarrativeCheckpointWait.Night);
            AssertUnsettled(latest);
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight, entryCompletions.Add), Is.True);
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(presenter.Current.ResumePageId, Is.EqualTo("fixture.night"));
            Assert.That(controller.CurrentCheckpointStage, Is.EqualTo(2));
            Assert.That(controller.SelectedChoices, Is.EqualTo(new[] { FeeOne }));
            presenter.Emit(afternoon, "fixture.stale", NarrativeCheckpointWait.Business);
            oldAfternoon(); oldMorning();
            AssertUnsettled(latest);
            Action final = presenter.Completion;
            final(); final(); oldMorning(); oldAfternoon();
            AssertSettled(latest + 1, true);
            Assert.That(walletChanges, Is.EqualTo(new[] { latest, latest + 1 }));
            Assert.That(entryCompletions, Is.EqualTo(new[] { true, true, true }));
            Restore(controller.CaptureSnapshot());
            final(); oldMorning(); oldAfternoon();
            presenter.Wait("fixture.stale", NarrativeCheckpointWait.Night);
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight), Is.False);
            Assert.That(controller.QueueEvent(Chapter), Is.False);
            AssertSettled(latest + 1, true);
            Assert.That(walletChanges, Is.EqualTo(new[] { latest, latest + 1 }));
            Assert.That(eventFailures, Is.Zero);
        }

        [Test]
        public void FeeFreeCompletesSameRootWithoutMoneyResultOrWalletWrite()
        {
            StartAndWaitForBusiness(FeeFree);
            string requestId = presenter.Current.RequestId;
            Action oldMorning = presenter.Completion;
            RunRealBusinessOnce();
            ResumeAndWaitForNight(requestId, FeeFree);
            Assert.That(walletChanges, Is.Empty);
            AdjustRealMoney(23);
            int latest = world.State.money;
            Restore(controller.CaptureSnapshot());
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight, entryCompletions.Add), Is.True);
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(controller.SelectedChoices, Is.EqualTo(new[] { FeeFree }));
            Action final = presenter.Completion;
            final(); final(); oldMorning();
            AssertSettled(latest, false);
            Assert.That(walletChanges, Is.EqualTo(new[] { latest }));
            Assert.That(entryCompletions, Is.EqualTo(new[] { true, true, true }));
            Assert.That(eventFailures, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RejectedMoneySettlementRetainsNightCheckpointAndRetryCommitsOnce(bool rejectPreflightRead)
        {
            var account = new RejectingMoneyAccount(provider);
            controller.MoneyAccount = account;
            StartAndWaitForBusiness(FeeOne);
            string requestId = presenter.Current.RequestId;
            Action oldMorning = presenter.Completion;
            RunRealBusinessOnce();
            ResumeAndWaitForNight(requestId, FeeOne);
            Action oldAfternoon = presenter.Completion;
            int beforeFailure = world.State.money;
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight, entryCompletions.Add), Is.True);
            account.RejectReads = rejectPreflightRead;
            account.RejectCommits = !rejectPreflightRead;
            Action failed = presenter.Completion;
            string reason = rejectPreflightRead ? "Money account is not ready" : "Money batch rejected: fixture rejected commit";
            LogAssert.Expect(LogType.Error, new Regex("Event " + Regex.Escape(Chapter) + " result transaction failed:.*" + Regex.Escape(reason)));
            failed(); failed(); oldMorning(); oldAfternoon();
            AssertUnsettled(beforeFailure);
            Assert.That(walletChanges, Is.Empty);
            Assert.That(eventFailures, Is.EqualTo(1));
            Assert.That(account.SuccessfulCommits, Is.Zero);
            Assert.That(account.CommitAttempts, Is.EqualTo(rejectPreflightRead ? 0 : 1));
            Assert.That(controller.HasPendingNightCheckpoint, Is.True);
            Assert.That(controller.IsSafeBoundary, Is.True);
            var retained = controller.CaptureSnapshot();
            Assert.That(retained.checkpoint.root.requestId, Is.EqualTo(requestId));
            Assert.That(retained.checkpoint.root.selectedChoices, Is.EqualTo(new[] { FeeOne }));
            Assert.That(retained.checkpoint.resumePageId, Is.EqualTo("fixture.night"));
            Assert.That(entryCompletions, Is.EqualTo(new[] { true, true, false }));
            account.RejectReads = account.RejectCommits = false;
            Restore(retained);
            AdjustRealMoney(9);
            int latest = world.State.money;
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight, entryCompletions.Add), Is.True);
            Assert.That(presenter.Current.RequestId, Is.EqualTo(requestId));
            Assert.That(controller.SelectedChoices, Is.EqualTo(new[] { FeeOne }));
            failed(); oldMorning(); oldAfternoon();
            AssertUnsettled(latest);
            Action retry = presenter.Completion;
            retry(); retry(); failed();
            AssertSettled(latest + 1, true);
            Assert.That(account.SuccessfulCommits, Is.EqualTo(1));
            Assert.That(account.CommitAttempts, Is.EqualTo(rejectPreflightRead ? 1 : 2));
            Assert.That(walletChanges, Is.EqualTo(new[] { latest, latest + 1 }));
            Assert.That(eventFailures, Is.EqualTo(1));
            Assert.That(entryCompletions, Is.EqualTo(new[] { true, true, false, true }));
            Restore(controller.CaptureSnapshot());
            retry(); failed();
            Assert.That(controller.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight), Is.False);
            AssertSettled(latest + 1, true);
            Assert.That(account.SuccessfulCommits, Is.EqualTo(1));
        }
    }
}
