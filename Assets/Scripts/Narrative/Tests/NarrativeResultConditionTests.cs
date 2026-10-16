using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    public sealed class NarrativeResultConditionTests
    {
        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation, INarrativePresentationCheckpoints
        {
            public event Action<string> Cancelled;
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public readonly List<Action> Callbacks = new List<Action>();
            public NarrativeEventRequest Current;
            public void Play(NarrativeEventRequest request, Action completed) { Current = request; Callbacks.Add(completed); }
            public void Complete() => Callbacks.Last()();
            public void Cancel() => Cancelled?.Invoke("cancelled");
            public void Wait() => CheckpointReached?.Invoke(Current, "night", NarrativeCheckpointWait.Night);
        }

        private NarrativeConfiguration config;
        private Presenter presenter;
        [SetUp] public void SetUp()
        {
            config = new NarrativeConfiguration(); presenter = new Presenter();
            config.bars.Add(new NarrativeBarDefinition { barKey = "world.calendar", min = 1, max = 999, initValue = 1, advanceMode = NarrativeAdvanceMode.Monotonic });
            config.bars.Add(new NarrativeBarDefinition { barKey = "reward", min = 0, max = 100 });
            config.events.Add(new NarrativeEventDefinition { eventId = "root", onceOnly = true, entry = NarrativeEntryPoint.MorningBeforeMap });
            config.eventDates.Add(new NarrativeEventDateDefinition { eventId = "root", day = 2, timeSlot = NarrativeTimeSlot.Morning });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "chosen", groupKey = "route" });
            config.choices.Add(new NarrativeChoiceDefinition { choiceId = "other", groupKey = "route" });
            config.abilityNameKeys.Add("wisdom", "wisdom");
            foreach (string id in new[] { "first", "second", "unvisited" })
            {
                config.abilityChecks.Add(new NarrativeAbilityCheckDefinition { checkId = id, mode = NarrativeAbilityCheckMode.Inline });
                config.abilityCheckConditions.Add(new NarrativeAbilityCheckConditionDefinition { checkId = id, attributeId = "wisdom", groupIndex = 1, threshold = 3 });
            }
            Result("base", 1);
        }
        private void Result(string id, int reward, string owner = "root") => config.results.Add(new NarrativeResultDefinition
            { resultId = id, eventId = owner, resultType = NarrativeResultType.BarAdd, target = "reward", value = reward.ToString() });
        private void Guard(string result, string target, string value = "true", int group = 1, string type = "PRESENTATION") => config.conditions.Add(new NarrativeConditionDefinition
            { conditionId = result + "." + config.conditions.Count, ownerType = "result", ownerId = result, groupIndex = group, condType = type, target = target, value = value });
        private NarrativeController Start()
        {
            var c = new NarrativeController(config, presenter);
            c.SetOfficialDay(2); c.ProcessOfficialDay(2);
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap));
            return c;
        }
        private static int Reward(NarrativeController c) { Assert.That(c.State.TryGetBar("reward", out int value)); return value; }

        [TestCase(true, 12)]
        [TestCase(false, 9)]
        public void MultipleInlineResultsCommitTogetherOnlyAtRootAndOnlyOnce(bool pass, int expected)
        {
            Result("first.pass", 2); Guard("first.pass", "choice:chosen"); Guard("first.pass", "check:first");
            Result("first.fail", 4); Guard("first.fail", "choice:chosen"); Guard("first.fail", "not:check:first");
            Result("second.pass", 4); Guard("second.pass", "check:second");
            Result("choice.pass", 2, "chosen"); Guard("choice.pass", "check:first");
            Result("first.extra", 3); Guard("first.extra", "check:first");
            var c = Start(); int reads = 0, completed = 0;
            c.EventCompleted += _ => completed++;
            c.AbilityReader = _ => { reads++; return (true, pass ? 3 : 0); };
            Assert.That(c.SelectChoice("chosen"));
            Assert.That(c.ReadPresentationCondition("check:first"), Is.EqualTo(pass));
            c.AbilityReader = _ => { reads++; return (true, 3); };
            Assert.That(c.ReadPresentationCondition("check:second"));
            Assert.That(Reward(c), Is.Zero);
            c.AbilityReader = _ => throw new Exception("Settlement must never evaluate an ability");
            presenter.Complete(); presenter.Complete();
            Assert.That(Reward(c), Is.EqualTo(expected)); Assert.That(reads, Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(1)); Assert.That(c.State.HasHappenedEvent("root"));
        }

        [Test]
        public void UnknownOrUnvisitedCheckCannotPassNegationOrFalseComparison()
        {
            foreach (string target in new[] { "check:unvisited", "not:check:unvisited", "not:not:check:unvisited", "not:check:missing" })
                foreach (string value in new[] { "true", "false" })
                {
                    string id = "blocked" + config.results.Count; Result(id, 5); Guard(id, target, value);
                }
            var c = Start(); c.AbilityReader = _ => throw new Exception("Unvisited check evaluated");
            presenter.Complete(); Assert.That(Reward(c), Is.EqualTo(1));
        }

        [Test]
        public void UnselectedChoiceShortCircuitsEvenWhenAuthoredAfterOtherCondition()
        {
            Result("unchosen", 20);
            Guard("unchosen", "wisdom", "3", type: "ATTRIBUTE_GTE");
            Guard("unchosen", "choice:other");
            var c = Start(); Assert.That(c.SelectChoice("chosen"));
            c.AbilityReader = _ => throw new Exception("Unchosen branch must short circuit");
            presenter.Complete(); Assert.That(Reward(c), Is.EqualTo(1));
        }

        [Test]
        public void ResultGroupsShortCircuitAndFormalBoolUsesStateBeforeEntireBatch()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "formal", nameText = "formal" });
            config.results.Add(new NarrativeResultDefinition { resultId = "set", eventId = "root", resultType = NarrativeResultType.SetFlag, target = "formal", value = "true" });
            Result("prestate", 2); Guard("prestate", "formal", "false", type: "BOOL_EQ");
            Result("newstate", 10); Guard("newstate", "formal", type: "BOOL_EQ");
            Result("or", 4); Guard("or", "choice:chosen"); Guard("or", "wisdom", "3", group: 2, type: "ATTRIBUTE_GTE");
            var c = Start(); Assert.That(c.SelectChoice("chosen"));
            c.AbilityReader = _ => throw new Exception("Successful OR group must short circuit");
            presenter.Complete(); Assert.That(Reward(c), Is.EqualTo(7)); Assert.That(c.State.GetCondition("formal"));
        }

        [Test]
        public void PresentationBoolGuardsReadFormalStateAndUnknownNegationFailsClosed()
        {
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "formal" });
            Result("positive", 2); Guard("positive", "formal");
            Result("negative", 4); Guard("negative", "not:formal");
            Result("false", 8); Guard("false", "formal", "false");
            Result("unknown", 16); Guard("unknown", "not:unknown");
            Result("unknown.false", 32); Guard("unknown.false", "unknown", "false");
            var c = Start(); c.State.SetCondition("formal", true);
            presenter.Complete(); Assert.That(Reward(c), Is.EqualTo(3));
        }

        [Test]
        public void CancelCommitsNothingAndStaleCompletionCannotCommitRetry()
        {
            Result("guarded", 3); Guard("guarded", "check:first");
            var c = Start(); c.AbilityReader = _ => (true, 3); Assert.That(c.ReadPresentationCondition("check:first"));
            var stale = presenter.Callbacks.Last(); presenter.Cancel(); stale();
            Assert.That(Reward(c), Is.Zero); Assert.That(c.State.HasHappenedEvent("root"), Is.False);
            Assert.That(c.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap)); stale();
            c.AbilityReader = _ => throw new Exception("Retry has no visited check");
            presenter.Complete(); Assert.That(Reward(c), Is.EqualTo(1));
        }

        [Test]
        public void CheckpointRestoreUsesCachedInlineSnapshotAndRetainedChoiceForResult()
        {
            Result("guarded", 3); Guard("guarded", "choice:chosen"); Guard("guarded", "check:first");
            var c = Start(); c.AbilityReader = _ => (true, 3);
            Assert.That(c.SelectChoice("chosen")); Assert.That(c.ReadPresentationCondition("check:first"));
            presenter.Wait(); var stale = presenter.Callbacks.Last(); Assert.That(Reward(c), Is.Zero);
            var saved = c.CaptureSnapshot();
            presenter = new Presenter(); var restored = new NarrativeController(config, presenter);
            restored.RestoreSnapshot(saved); restored.SetOfficialDay(2);
            restored.AbilityReader = _ => throw new Exception("Restored settlement must use cache");
            Assert.That(restored.ResumeCheckpoint(NarrativeEntryPoint.BeforeNight));
            stale(); Assert.That(Reward(restored), Is.Zero);
            presenter.Complete(); presenter.Complete(); Assert.That(Reward(restored), Is.EqualTo(4));
        }

        [Test]
        public void LoaderPreservesResultConditionOwnerAndRejectsUnknownGuard()
        {
            var data = new Dictionary<string, Newtonsoft.Json.Linq.JArray>
            {
                ["tbtext"] = Newtonsoft.Json.Linq.JArray.Parse("[{ 'key':'title', 'text':'Title' }]"),
                ["tbncsevent"] = Newtonsoft.Json.Linq.JArray.Parse("[{ 'eventId':'root', 'eventKind':'world', 'presentationClass':'Conversation', 'presentationKey':'', 'actorId':'', 'targetId':'', 'titleText':'title', 'setupType':'', 'setupKey':'', 'onceOnly':true }]"),
                ["tbncschoice"] = Newtonsoft.Json.Linq.JArray.Parse("[{ 'choiceId':'chosen', 'groupKey':'route', 'textKey':'title', 'checkId':'', 'seq':1 }]"),
                ["tbncsconditionkey"] = Newtonsoft.Json.Linq.JArray.Parse("[{ 'key':'formal', 'nameText':'title', 'initialValue':false, 'onlySetTrue':false }]"),
                ["tbncsresult"] = Newtonsoft.Json.Linq.JArray.Parse("[{ 'resultId':'result', 'eventId':'root', 'seq':1, 'resultType':'SET_FLAG', 'target':'formal', 'value':'true' }]"),
                ["tbncscondition"] = Newtonsoft.Json.Linq.JArray.Parse("[{ 'conditionId':'guard', 'ownerType':'result', 'ownerId':'result', 'groupIndex':1, 'condType':'PRESENTATION', 'target':'choice:chosen', 'value':'true' }]")
            };
            NarrativeConfiguration Load() => LubanNarrativeLoader.FromTables(new cfg.Tables(name => data.TryGetValue(name, out var rows) ? rows : new Newtonsoft.Json.Linq.JArray()));
            var loaded = Load();
            Assert.That(loaded.conditions.Single().ownerType, Is.EqualTo("result"));
            Assert.That(loaded.conditions.Single().ownerId, Is.EqualTo("result"));
            Assert.That(loaded.conditions.Single().condType, Is.EqualTo("PRESENTATION"));
            data["tbncscondition"][0]["target"] = "not:check:missing";
            Assert.Throws<FormatException>(() => Load());
        }

        [Test]
        public void ValidatorAcceptsResultGuardsAndRejectsMissingOwnerGuardAndNonInlineCheck()
        {
            config.texts.Add(new NarrativeTextDefinition { key = "formal", content = "Formal" });
            config.conditionKeys.Add(new NarrativeConditionKeyDefinition { key = "formal", nameText = "formal" });
            foreach (string target in new[] { "choice:chosen", "anyChoice:chosen;other", "not:check:first", "formal", "not:formal", "checkpointStage:1" }) Guard("base", target);
            Guard("base", "formal", type: "BOOL_EQ");
            Assert.That(LubanNarrativeLoader.ValidateConditionConfiguration(config), Is.Empty);
            Guard("missing", "choice:chosen"); Guard("base", "check:missing");
            Guard("base", "not:unknown"); Guard("base", "anyChoice:chosen;missing"); Guard("base", "check:first", "invalid");
            config.abilityChecks.Find(x => x.checkId == "first").mode = NarrativeAbilityCheckMode.Branch;
            config.conditions.Add(new NarrativeConditionDefinition { conditionId = "wrongOwner", ownerType = "event", ownerId = "root", groupIndex = 1, condType = "PRESENTATION", target = "choice:chosen", value = "true" });
            var errors = LubanNarrativeLoader.ValidateConditionConfiguration(config);
            Assert.That(errors.Any(x => x.Contains("unknown result owner missing")));
            Assert.That(errors.Count(x => x.Contains("unknown presentation guard")), Is.EqualTo(5));
            Assert.That(errors.Any(x => x.Contains("expected true or false")));
            Assert.That(errors.Any(x => x.Contains("requires a result owner")));
        }
    }
}
