using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    /// <summary>
    /// Dedicated acceptance of generated A01-A04 data. No scene, actual UI, singleton or authoring-table writes.
    /// Every case loads its own Luban configuration. B completed / approval / A02 completed are explicitly
    /// seeded fixture inputs in independent scenarios, NOT evidence that the unavailable B story ran naturally.
    /// RunAll is also callable by a declaration-free unity_execute body via reflection.
    /// </summary>
    public sealed class NeutralFormalAcceptanceTests
    {
        private const string A01 = "neutral.1008.s1";
        private const string A02 = "neutral.1010.s1";
        private const string A03 = "neutral.1010.s2";
        private const string A04 = "neutral.1013.s2";
        private const string BCompleted = "story.rose.1009.s1.completed";
        private const string MeetingKnown = "story.oct09.meeting.known";
        private const string Approval = "story.neutral.1010.s2.antoine_approval";
        private static readonly string[] Roots = { A01, A02, A03, A04 };
        private static readonly string[] People = { "colette", "adele", "antoine" };

        private sealed class Presenter : INarrativePresenter, INarrativePresentationCancellation, INarrativePresentationCheckpoints
        {
            public event Action<string> Cancelled;
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            public readonly List<Action> Callbacks = new List<Action>();
            public NarrativeEventRequest Current => Requests.Last();
            public Action Completion => Callbacks.Last();
            public void Play(NarrativeEventRequest request, Action completed) { Requests.Add(request); Callbacks.Add(completed); }
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
            public void Emit(NarrativeEventRequest request, string nextPage, NarrativeCheckpointWait wait)
                => CheckpointReached?.Invoke(request, nextPage, wait);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly NarrativeConfiguration Config = LubanNarrativeLoader.Load();
            public readonly Presenter P = new Presenter();
            public readonly NarrativeController C;
            public readonly NarrativeActionController Actions;
            public readonly List<string> Visible = new List<string>();
            public readonly List<string> Offered = new List<string>();
            public readonly List<string> Completed = new List<string>();
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Reads = new List<string>();
            public readonly List<NarrativeAbilityCheckSnapshot> Checks = new List<NarrativeAbilityCheckSnapshot>();
            public readonly Dictionary<string, string> Choices = new Dictionary<string, string>();
            public int Language, Wisdom;

            public Fixture(int day, params string[] allowed)
            {
                C = new NarrativeController(Config, P);
                // Isolate the case from unrelated chapters without editing any generated definition or result.
                foreach (var ev in Config.events.Where(e => !allowed.Contains(e.eventId))) C.State.SetEventLocked(ev.eventId, true);
                C.BeginValueRestore();
                foreach (var group in Config.bars.Where(b => b.sourceMode == NarrativeSourceMode.External).GroupBy(b => b.providerId))
                {
                    var values = group.ToDictionary(b => b.sourceKey,
                        b => b.providerId == "dayflow" && b.sourceKey == "day" ? day : b.initValue);
                    C.BindValueProvider(new DelegateNarrativeValueProvider(group.Key,
                        key => values.TryGetValue(key, out int value) ? (int?)value : null));
                }
                C.SetOfficialDay(day);
                C.CompleteValueRestore(false);
                C.AbilityReader = id => { Reads.Add(id); return id == "language" ? (true, Language) : id == "wisdom" ? (true, Wisdom) : (false, 0); };
                C.AbilityCheckEvaluated += Checks.Add;
                C.EventCompleted += Completed.Add;
                C.EventFailed += (id, error) => Failures.Add(id + ": " + error);
                Actions = new NarrativeActionController(C, Config.actions, 5, new NarrativeMapInventory());
                Actions.RefreshForDay(day);
                foreach (var position in Config.mapPositions.Where(p => p.enabled))
                {
                    if (!string.IsNullOrWhiteSpace(position.defaultAction)) Actions.Mount(position.positionKey, position.defaultAction);
                    foreach (var key in position.extendedActions) Actions.Mount(position.positionKey, key);
                }
                C.ProcessOfficialDay(day);
                Assert.That(P.Requests, Is.Empty, "Formal days must not silently play MapAction/Morning roots before an entry.");
            }
            public void Dispose() => C.UnbindValueProviders();
            public void Fact(string key) => C.State.SetCondition(key, true);
            public void Cancel()
            {
                int count = Failures.Count;
                P.Cancel();
                Assert.That(Failures.Count, Is.EqualTo(count + 1), "Cancellation must be reported, not mistaken for completion.");
                Assert.That(Failures.Last(), Does.Contain("presentation_cancelled: playback_cancelled"));
                Failures.RemoveAt(Failures.Count - 1); // Consume ONLY the cancellation intentionally caused by this test.
            }
            public void Choose(string root, string group, string option) => Choices[root + "." + group] = root + ".choice." + group + "." + option;
            public int Relation(string person, bool reverse = false)
            {
                Assert.That(C.State.TryGetBar(reverse ? "player>" + person : person + ">player", out int value), Is.True, "Missing formal relation: " + person);
                return value;
            }
            public string PublicState() => string.Join("|", C.State.Bars.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value))
                + ";" + string.Join("|", C.State.Conditions.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value))
                + ";" + string.Join("|", C.State.HappenedEvents.OrderBy(x => x));
            public void NoCommit(string root)
            {
                Assert.That(C.State.GetCondition("story." + root + ".completed"), Is.False);
                Assert.That(C.State.HasHappenedEvent(root), Is.False);
                Assert.That(C.State.IsFlagSet("event.once." + root), Is.False);
            }
            public void Relations(int colette = 0, int adele = 0, int antoine = 0)
            {
                Assert.That(new[] { Relation("colette"), Relation("adele"), Relation("antoine") }, Is.EqualTo(new[] { colette, adele, antoine }));
                foreach (string person in People) Assert.That(Relation(person, true), Is.Zero, "No reverse-direction reward: " + person);
            }
            public void Healthy() => Assert.That(Failures, Is.Empty, string.Join("\n", Failures));
            public NarrativeActionDefinition MarketAction => Config.actions.Single(a => a.resultEvents.Contains(A01));
            public void StartA01()
            {
                Fact("world.map.active");
                Assert.That(Actions.GetDisplayableActions("market").Any(a => a.actionKey == MarketAction.actionKey), Is.True);
                Assert.That(Actions.Execute(MarketAction.actionKey), Is.True);
                Assert.That(P.Current.Event.eventId, Is.EqualTo(A01));
            }
            public void StartA03()
            {
                // This is an isolated prerequisite fixture, not a naturally completed A02/B playthrough.
                Fact("story." + A02 + ".completed");
                Assert.That(C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                Assert.That(P.Current.Event.eventId, Is.EqualTo(A03));
            }
            public void StartA04(bool approval)
            {
                // A04 requires A02, not general A03 completion. Approval is an independent optional fixture input.
                Fact("story." + A02 + ".completed");
                if (approval) Fact(Approval);
                Assert.That(C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                Assert.That(P.Current.Event.eventId, Is.EqualTo(A04));
            }

            /// <summary>Only simulates presenter boundary protocol. Actual controller owns guards, selection, cache and settlement.
            /// Matches NarrativePresenter's ordered, short-circuit page keys and next-page checkpoint semantics.
            /// Returns Business/Night/End/Stopped without auto-completing a root unless explicitly requested.</summary>
            public string Walk(bool complete = false, string stopAfter = null)
            {
                var request = P.Current;
                var pages = Config.presentations.conversationPages.Where(p => p.presentationKey == request.Event.presentationKey).OrderBy(p => p.seq).ToArray();
                Assert.That(pages, Is.Not.Empty);
                int start = string.IsNullOrWhiteSpace(request.ResumePageId) ? 0 : Array.FindIndex(pages, p => p.pageId == request.ResumePageId);
                Assert.That(start, Is.GreaterThanOrEqualTo(0));
                for (int i = start; i < pages.Length; i++)
                {
                    var page = pages[i];
                    Assert.That(page.pageBarConds, Is.Null.Or.Empty, "Presenter rejects unsupported pageBarConds.");
                    bool visible = true;
                    foreach (string key in (page.pageCondKeys ?? "").Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                        if (!C.ReadPresentationCondition(key.Trim())) { visible = false; break; }
                    if (!visible) continue;
                    Visible.Add(page.pageId);
                    if (page.stepType == NarrativeConversationStepType.WaitBusiness || page.stepType == NarrativeConversationStepType.WaitNight)
                    {
                        Assert.That(i + 1, Is.LessThan(pages.Length));
                        var wait = page.stepType == NarrativeConversationStepType.WaitBusiness ? NarrativeCheckpointWait.Business : NarrativeCheckpointWait.Night;
                        P.Emit(request, pages[i + 1].pageId, wait);
                        return wait.ToString();
                    }
                    if (page.stepType == NarrativeConversationStepType.PassiveCheck) C.EvaluateInlineAbilityCheckForPresentation(page.checkId);
                    if (!string.IsNullOrWhiteSpace(page.optionGroupKey))
                    {
                        var options = Config.choices.Where(c => c.groupKey == page.optionGroupKey).OrderBy(c => c.seq).ToArray();
                        if (!options.Any(c => C.SelectedChoices.Contains(c.choiceId)))
                        {
                            Offered.Add(page.optionGroupKey);
                            Assert.That(Choices.TryGetValue(page.optionGroupKey, out string selected), Is.True, "Specify an explicit test choice for " + page.optionGroupKey);
                            Assert.That(options.Any(c => c.choiceId == selected), Is.True);
                            Assert.That(C.EvaluateConditions("choice", selected), Is.True, "Low-ability failure routes must still be selectable.");
                            Assert.That(C.SelectChoice(selected), Is.True);
                        }
                    }
                    if (page.pageId == stopAfter) return "Stopped";
                }
                if (complete) P.Completion();
                return "End";
            }
            public void Business()
            {
                Assert.That(C.HasPendingBusinessCheckpoint, Is.True);
                Assert.That(C.ProcessEntry(NarrativeEntryPoint.AfterBusiness), Is.False, "A phase name alone is not a business receipt.");
                Assert.That(C.TryBeginCheckpointBusiness(7, out _), Is.False);
                Assert.That(C.TryBeginCheckpointBusiness(8, out var token), Is.True);
                Assert.That(token, Is.Not.Null.And.Not.Empty);
                Assert.That(C.TryBeginCheckpointBusiness(8, out _), Is.False);
                Assert.That(C.ConfirmCheckpointBusiness(7, token), Is.False);
                Assert.That(C.ConfirmCheckpointBusiness(8, "forged"), Is.False);
                Assert.That(C.ConfirmCheckpointBusiness(8, token), Is.True);
                Assert.That(C.ConfirmCheckpointBusiness(8, token), Is.False);
                Assert.That(C.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.False);
                Assert.That(C.ProcessEntry(NarrativeEntryPoint.AfterBusiness), Is.True);
            }
        }

        public static readonly string[] CaseNames =
        {
            "GeneratedContract", "A01WrongDay2", "A01WrongDay4", "A01Help", "A01TalkFail", "A01TalkPass", "A01Wait", "A01Cancel",
            "A02MeetingKnownIsInsufficient", "A02BCompletedAutoChains", "A02WrongDay4", "A02WrongDay6", "A02CostFail", "A02CostPass", "A02PraiseFail", "A02PraisePass",
            "A03Language1", "A03Language2", "A03Language3", "A03Refuse", "A03CancelAfterFirstSuccess",
            "A04WrongDay7", "A04WrongDay9", "A04WithoutApproval", "A04OrganizeFail", "A04OrganizePass", "A04PromoteFail", "A04PromotePass", "A04Wisdom1", "A04Wisdom2", "A04Language2", "A04Language3",
            "A04CancelMorning", "A04CancelAfternoon", "A04CancelNight", "A04CachedCheckRetry"
        };

        public static string RunAll()
        {
            var report = new StringBuilder("Neutral formal acceptance: independent Luban fixtures; B completed is explicitly seeded, NOT a natural B chain.\n");
            var errors = new List<string>();
            foreach (string name in CaseNames)
            {
                try { new NeutralFormalAcceptanceTests().Acceptance(name); report.AppendLine("PASS " + name); }
                catch (Exception error) { errors.Add(name + ": " + error); report.AppendLine("FAIL " + name + ": " + error.Message); }
            }
            if (errors.Count > 0) throw new InvalidOperationException(report + "\n" + string.Join("\n\n", errors));
            return report.Append("PASS ").Append(CaseNames.Length).Append('/').Append(CaseNames.Length).ToString();
        }

        [TestCaseSource(nameof(CaseNames))]
        public void Acceptance(string name)
        {
            switch (name)
            {
                case "GeneratedContract": Contract(); break;
                case "A01WrongDay2": A01WrongDay(2); break;
                case "A01WrongDay4": A01WrongDay(4); break;
                case "A01Help": A01Route("a", 0, 2, "help"); break;
                case "A01TalkFail": A01Route("b", 1, 0, "talk_fail"); break;
                case "A01TalkPass": A01Route("b", 2, 1, "talk_pass"); break;
                case "A01Wait": A01Route("c", 0, 0, "wait"); break;
                case "A01Cancel": A01Cancel(); break;
                case "A02MeetingKnownIsInsufficient": A02KnownOnly(); break;
                case "A02BCompletedAutoChains": A02Route("a", 0, 0, 1, "agree"); break;
                case "A02WrongDay4": A02WrongDay(4); break;
                case "A02WrongDay6": A02WrongDay(6); break;
                case "A02CostFail": A02Route("b", 0, 2, 0, "cost_fail"); break;
                case "A02CostPass": A02Route("b", 0, 3, 0, "cost_pass"); break;
                case "A02PraiseFail": A02Route("c", 1, 0, 0, "praise_fail"); break;
                case "A02PraisePass": A02Route("c", 2, 0, 2, "praise_pass"); break;
                case "A03Language1": A03Route(1, 0, false); break;
                case "A03Language2": A03Route(2, 1, false); break;
                case "A03Language3": A03Route(3, 3, true); break;
                case "A03Refuse": A03Refuse(); break;
                case "A03CancelAfterFirstSuccess": A03Cancel(); break;
                case "A04WrongDay7": A04WrongDay(7); break;
                case "A04WrongDay9": A04WrongDay(9); break;
                case "A04WithoutApproval": A04Route(false, "b", 3, 3, 0, null); break;
                case "A04OrganizeFail": A04Route(false, "b", 0, 1, 0, null, "b"); break;
                case "A04OrganizePass": A04Route(false, "b", 0, 2, 0, null, "b"); break;
                case "A04PromoteFail": A04Route(false, "b", 2, 0, 0, null, "c"); break;
                case "A04PromotePass": A04Route(false, "b", 3, 0, 0, null, "c"); break;
                case "A04Wisdom1": A04Route(true, "b", 0, 1, 0, "accounts_fail"); break;
                case "A04Wisdom2": A04Route(true, "b", 0, 2, 2, "accounts_pass"); break;
                case "A04Language2": A04Route(true, "a", 2, 0, 0, "longterm_fail"); break;
                case "A04Language3": A04Route(true, "a", 3, 0, 2, "longterm_pass"); break;
                case "A04CancelMorning": A04Cancel(0); break;
                case "A04CancelAfternoon": A04Cancel(1); break;
                case "A04CancelNight": A04Cancel(2); break;
                case "A04CachedCheckRetry": A04CachedRetry(); break;
                default: Assert.Fail("Unknown case: " + name); break;
            }
        }

        private static void Contract()
        {
            using (var f = new Fixture(1))
            {
                foreach (string person in People)
                {
                    var bar = f.Config.bars.Single(b => b.barKey == person + ".trust_player");
                    Assert.That(bar.barType, Is.EqualTo(NarrativeBarType.Relation));
                    Assert.That(bar.ownerId, Is.EqualTo(person)); Assert.That(bar.targetId, Is.EqualTo("player"));
                    Assert.That(new[] { bar.min, bar.max, bar.initValue, bar.reverseInitValue }, Is.EqualTo(new[] { -2, 5, 0, 0 }));
                    Assert.That(bar.sourceMode, Is.EqualTo(NarrativeSourceMode.Ncs));
                }
                f.Relations();
                foreach (string root in Roots)
                {
                    var ev = f.Config.events.Single(e => e.eventId == root);
                    Assert.That(ev.onceOnly && ev.blocksSave, Is.True);
                    Assert.That(f.C.ConstructionBlocker(ev), Is.Null.Or.Empty, root + " is still a construction preview");
                    Assert.That(ev.entry, Is.EqualTo(root == A01 ? NarrativeEntryPoint.MapAction : NarrativeEntryPoint.MorningBeforeMap));
                    Assert.That(f.Config.results.Any(r => r.eventId == root && r.target == "story." + root + ".completed"), Is.True);
                    Assert.That(f.Config.presentations.conversationPages.Where(p => p.presentationKey == ev.presentationKey)
                        .Any(p => (p.pageCondKeys ?? "").Contains("dependency:")), Is.False);
                }
                foreach (var pair in new[] { (A02, 5), (A03, 5), (A04, 8) })
                {
                    var date = f.Config.eventDates.Single(d => d.eventId == pair.Item1);
                    Assert.That(date.day, Is.EqualTo(pair.Item2)); Assert.That(date.timeSlot, Is.EqualTo(NarrativeTimeSlot.Morning));
                }
                Assert.That(f.MarketAction.type, Is.EqualTo(NarrativeActionType.Once));
                Assert.That(f.MarketAction.mustExecute, Is.True);
                Assert.That(NarrativeActionRequirementParser.Parse(f.MarketAction.requirements), Is.Empty, "A01 costs 0AP and no items.");
                var q2 = f.Config.abilityCheckConditions.Single(c => c.checkId == A04 + ".q2_wisdom");
                Assert.That(q2.attributeId, Is.EqualTo("wisdom")); Assert.That(q2.threshold, Is.EqualTo(2));
                var waits = f.Config.presentations.conversationPages.Where(p => p.presentationKey == f.Config.events.Single(e => e.eventId == A04).presentationKey)
                    .OrderBy(p => p.seq).Where(p => p.stepType == NarrativeConversationStepType.WaitBusiness || p.stepType == NarrativeConversationStepType.WaitNight).ToArray();
                Assert.That(waits.Select(p => p.stepType), Is.EqualTo(new[] { NarrativeConversationStepType.WaitBusiness, NarrativeConversationStepType.WaitNight }));
                Assert.That(LubanNarrativeLoader.ValidateConversationSteps(f.Config), Is.Empty);
            }
        }
        private static void A01WrongDay(int day)
        {
            using (var f = new Fixture(day, A01))
            {
                Assert.That(f.Actions.IsAvailable(f.MarketAction.actionKey, out _), Is.False);
                Assert.That(f.Actions.Execute(f.MarketAction.actionKey), Is.False);
                Assert.That(f.C.QueueMapActionEvent(A01), Is.False);
                Assert.That(f.P.Requests, Is.Empty); f.NoCommit(A01); f.Relations();
            }
        }
        private static void A01Route(string choice, int language, int delta, string page)
        {
            using (var f = new Fixture(3, A01))
            {
                f.Language = language; f.Choose(A01, "q1", choice);
                Assert.That(f.C.QueueEvent(A01), Is.False, "Manual event API must not bypass the map entrance.");
                f.Fact("world.map.active");
                Assert.That(f.Actions.GetBlockingPositions(), Does.Contain("market"));
                int ap = f.Actions.ActionPoints.Current; f.StartA01();
                Assert.That(f.C.BlocksSave, Is.True); Assert.That(f.Actions.IsCompleted(f.MarketAction.actionKey), Is.False);
                Assert.That(f.Walk(), Is.EqualTo("End")); Assert.That(f.Visible, Does.Contain(A01 + "." + page));
                f.Relations(); f.NoCommit(A01); var callback = f.P.Completion; callback(); callback();
                f.Relations(colette: delta); Assert.That(f.Completed, Is.EqualTo(new[] { A01 }));
                Assert.That(f.C.State.GetCondition("story." + A01 + ".completed"), Is.True);
                Assert.That(f.Actions.IsCompleted(f.MarketAction.actionKey), Is.True);
                Assert.That(f.Actions.ActionPoints.Current, Is.EqualTo(ap));
                Assert.That(f.Actions.GetBlockingPositions(), Does.Not.Contain("market"));
                Assert.That(f.Actions.Execute(f.MarketAction.actionKey), Is.False); f.Healthy();
            }
        }
        private static void A01Cancel()
        {
            using (var f = new Fixture(3, A01))
            {
                f.Fact("world.map.active"); f.Choose(A01, "q1", "a"); string before = f.PublicState(); int ap = f.Actions.ActionPoints.Current;
                f.StartA01(); f.Walk(); var stale = f.P.Completion; f.Cancel(); stale();
                Assert.That(f.PublicState(), Is.EqualTo(before)); Assert.That(f.Actions.IsCompleted(f.MarketAction.actionKey), Is.False);
                Assert.That(f.Actions.ActionPoints.Current, Is.EqualTo(ap));
                Assert.That(f.Actions.GetBlockingPositions(), Does.Contain("market"));
                f.StartA01(); stale(); f.NoCommit(A01); f.Walk(true); f.Relations(colette: 2); f.Healthy();
            }
        }
        private static void A02KnownOnly()
        {
            using (var f = new Fixture(5, A02, A03))
            {
                f.Fact(MeetingKnown);
                Assert.That(f.C.State.GetCondition(BCompleted), Is.False);
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                Assert.That(f.P.Requests, Is.Empty); f.NoCommit(A02); f.NoCommit(A03); f.Relations();
            }
        }
        private static void A02WrongDay(int day)
        {
            using (var f = new Fixture(day, A02, A03))
            {
                f.Fact(BCompleted);
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                Assert.That(f.P.Requests, Is.Empty); f.NoCommit(A02); f.NoCommit(A03); f.Relations();
            }
        }
        private static void A02Route(string choice, int language, int wisdom, int delta, string page)
        {
            using (var f = new Fixture(5, A02, A03))
            {
                f.Fact(BCompleted); // Independent fixture: do not replace this with meeting-known or claim natural B execution.
                f.Language = language; f.Wisdom = wisdom; f.Choose(A02, "q1", choice); f.Choose(A03, "q1", "c");
                int continuations = 0;
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap, ok => { Assert.That(ok); continuations++; }), Is.True);
                Assert.That(f.P.Current.Event.eventId, Is.EqualTo(A02));
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.False);
                f.Walk(); Assert.That(f.Visible, Does.Contain(A02 + "." + page)); f.NoCommit(A02); f.Relations();
                Assert.That(f.C.State.GetCondition("story." + A02 + ".frugal_preparation"), Is.False);
                var old = f.P.Completion; old();
                Assert.That(f.P.Current.Event.eventId, Is.EqualTo(A03), "A03 must start automatically in the SAME morning batch after A02 commits.");
                Assert.That(continuations, Is.Zero); f.Relations(adele: delta); f.NoCommit(A03);
                Assert.That(f.C.State.GetCondition("story." + A02 + ".frugal_preparation"), Is.EqualTo(choice == "b" && wisdom >= 3));
                old(); Assert.That(f.P.Current.Event.eventId, Is.EqualTo(A03));
                f.Walk(true); Assert.That(continuations, Is.EqualTo(1));
                Assert.That(f.Completed, Is.EqualTo(new[] { A02, A03 })); f.Relations(adele: delta);
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                Assert.That(f.P.Requests.Count, Is.EqualTo(2)); f.Healthy();
            }
        }
        private static void A03Route(int language, int delta, bool approval)
        {
            using (var f = new Fixture(5, A03))
            {
                f.Language = language; f.Choose(A03, "q1", "b"); f.Choose(A03, "q2", "a"); f.StartA03();
                f.Walk(); f.NoCommit(A03); f.Relations(); Assert.That(f.C.State.GetCondition(Approval), Is.False);
                Assert.That(f.Visible, Does.Contain(A03 + (language >= 2 ? ".invite_pass" : ".invite_fail")));
                Assert.That(f.Visible, Does.Contain(A03 + (language >= 3 ? ".plan_pass" : ".plan_fail")));
                Assert.That(f.Checks.Select(c => c.checkId), Is.EqualTo(new[] { A03 + ".q1_language", A03 + ".q2_language" }));
                Assert.That(f.Checks.Select(c => c.passed), Is.EqualTo(new[] { language >= 2, language >= 3 }));
                Assert.That(f.Reads, Is.EqualTo(new[] { "language", "language" }));
                var done = f.P.Completion; done(); done();
                f.Relations(antoine: delta); Assert.That(f.C.State.GetCondition(Approval), Is.EqualTo(approval));
                Assert.That(f.C.State.GetCondition("story." + A03 + ".completed"), Is.True);
                Assert.That(f.Completed, Is.EqualTo(new[] { A03 })); f.Healthy();
            }
        }
        private static void A03Refuse()
        {
            using (var f = new Fixture(5, A03))
            {
                f.Choose(A03, "q1", "c"); f.StartA03(); f.Walk(true);
                Assert.That(f.Offered, Is.EqualTo(new[] { A03 + ".q1" }));
                Assert.That(f.Visible, Does.Contain(A03 + ".refuse_tail").And.Contain(A03 + ".common_end"));
                Assert.That(f.Visible, Does.Not.Contain(A03 + ".serve").And.Not.Contain(A03 + ".serve_tail"));
                Assert.That(f.Reads, Is.Empty); f.Relations(); Assert.That(f.C.State.GetCondition(Approval), Is.False); f.Healthy();
            }
        }
        private static void A03Cancel()
        {
            using (var f = new Fixture(5, A03))
            {
                f.Language = 3; f.Choose(A03, "q1", "b"); f.Choose(A03, "q2", "a"); f.StartA03(); string before = f.PublicState();
                Assert.That(f.Walk(stopAfter: A03 + ".invite_pass"), Is.EqualTo("Stopped"));
                Assert.That(f.Checks.Single().passed, Is.True); f.Relations();
                var stale = f.P.Completion; f.Cancel(); stale(); Assert.That(f.PublicState(), Is.EqualTo(before));
                f.NoCommit(A03); Assert.That(f.C.State.GetCondition(Approval), Is.False);
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                stale(); f.NoCommit(A03); f.Walk(true); f.Relations(antoine: 3); f.Healthy();
            }
        }
        private static void A04WrongDay(int day)
        {
            using (var f = new Fixture(day, A04))
            {
                f.Fact("story." + A02 + ".completed"); f.Fact("story." + A03 + ".completed"); f.Fact(Approval);
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
                Assert.That(f.P.Requests, Is.Empty); f.NoCommit(A04); f.Relations();
            }
        }
        private static void A04Route(bool approval, string q2, int language, int wisdom, int antoine, string branch, string q1 = "a")
        {
            using (var f = new Fixture(8, A04))
            {
                f.Language = language; f.Wisdom = wisdom; f.Choose(A04, "q1", q1); f.Choose(A04, "q2", q2); f.StartA04(approval);
                string id = f.P.Current.RequestId; var morning = f.P.Current; var staleMorning = f.P.Completion;
                Assert.That(f.Walk(), Is.EqualTo("Business")); f.NoCommit(A04); f.Relations();
                Assert.That(f.C.State.GetCondition("story." + A04 + ".orderly_service"), Is.False);
                Assert.That(f.C.State.GetCondition("story." + A04 + ".warm_recommendation"), Is.False);
                Assert.That(f.C.IsSafeBoundary && !f.C.BlocksSave, Is.True);
                f.Business(); Assert.That(f.P.Current.RequestId, Is.EqualTo(id));
                Assert.That(f.C.ReadPresentationCondition("checkpointStage:1"), Is.True);
                f.P.Emit(morning, "stale", NarrativeCheckpointWait.Night); Assert.That(f.C.IsBusy, Is.True);
                staleMorning(); f.NoCommit(A04);
                Assert.That(f.Walk(), Is.EqualTo("Night")); f.NoCommit(A04); f.Relations();
                Assert.That(f.Offered.Contains(A04 + ".q2"), Is.EqualTo(approval));
                Assert.That(f.Visible.Contains(A04 + ".visit"), Is.EqualTo(approval));
                if (branch != null) Assert.That(f.Visible, Does.Contain(A04 + "." + branch));
                if (!approval) Assert.That(f.Checks.Any(c => c.checkId.StartsWith(A04 + ".q2_", StringComparison.Ordinal)), Is.False);
                if (approval && q2 == "b")
                {
                    var check = f.Checks.Single(c => c.checkId == A04 + ".q2_wisdom");
                    Assert.That(check.passed, Is.EqualTo(wisdom >= 2)); Assert.That(check.values.Single().threshold, Is.EqualTo(2));
                    Assert.That(f.Reads, Is.EqualTo(new[] { "wisdom" }), "Success/failure guards must share one cached snapshot.");
                }
                Assert.That(f.C.ResumeCheckpoint(NarrativeEntryPoint.AfterBusiness), Is.False);
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.True);
                Assert.That(f.P.Current.RequestId, Is.EqualTo(id)); Assert.That(f.C.ReadPresentationCondition("checkpointStage:2"), Is.True);
                Assert.That(f.Walk(), Is.EqualTo("End")); Assert.That(f.Visible, Does.Contain(A04 + ".closing"));
                f.NoCommit(A04); f.Relations(); var done = f.P.Completion; done(); done(); staleMorning();
                f.Relations(adele: q1 == "a" ? 1 : 0, antoine: antoine); Assert.That(f.Completed, Is.EqualTo(new[] { A04 }));
                Assert.That(f.C.State.GetCondition("story." + A04 + ".orderly_service"), Is.EqualTo(q1 == "b" && wisdom >= 2));
                Assert.That(f.C.State.GetCondition("story." + A04 + ".warm_recommendation"), Is.EqualTo(q1 == "c" && language >= 3));
                Assert.That(f.C.State.GetCondition("story." + A04 + ".completed"), Is.True);
                Assert.That(f.C.CaptureSnapshot().checkpoint, Is.Null); Assert.That(f.C.BlocksSave, Is.False); f.Healthy();
            }
        }
        private static void A04Cancel(int stage)
        {
            using (var f = new Fixture(8, A04))
            {
                f.Wisdom = 2; f.Choose(A04, "q1", "a"); f.Choose(A04, "q2", "b"); f.StartA04(true); string before = f.PublicState();
                if (stage > 0) { Assert.That(f.Walk(), Is.EqualTo("Business")); f.Business(); }
                if (stage > 1) { Assert.That(f.Walk(), Is.EqualTo("Night")); Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.True); }
                if (stage == 0) f.Walk(stopAfter: A04 + ".cooperate");
                else if (stage == 1) f.Walk(stopAfter: A04 + ".accounts_pass");
                else f.Walk();
                var stale = f.P.Completion; f.Cancel(); stale();
                Assert.That(f.PublicState(), Is.EqualTo(before)); f.NoCommit(A04); f.Relations();
                var entry = stage == 0 ? NarrativeEntryPoint.MorningBeforeMap : stage == 1 ? NarrativeEntryPoint.AfterBusiness : NarrativeEntryPoint.BeforeNight;
                Assert.That(f.C.ProcessEntry(entry), Is.True); stale(); f.NoCommit(A04);
                if (stage == 0) { Assert.That(f.Walk(), Is.EqualTo("Business")); f.Business(); }
                if (stage < 2) { Assert.That(f.Walk(), Is.EqualTo("Night")); Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.True); }
                f.Walk(true); f.Relations(adele: 1, antoine: 2); Assert.That(f.Completed, Is.EqualTo(new[] { A04 })); f.Healthy();
            }
        }
        private static void A04CachedRetry()
        {
            using (var f = new Fixture(8, A04))
            {
                f.Wisdom = 2; f.Choose(A04, "q1", "a"); f.Choose(A04, "q2", "b"); f.StartA04(true);
                f.Walk(); f.Business(); Assert.That(f.Walk(stopAfter: A04 + ".accounts_pass"), Is.EqualTo("Stopped"));
                Assert.That(f.Reads, Is.EqualTo(new[] { "wisdom" }));
                var cached = f.C.EvaluateInlineAbilityCheckForPresentation(A04 + ".q2_wisdom"); cached.passed = false; cached.values[0].currentValue = -100;
                f.Cancel(); f.Wisdom = 1;
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.AfterBusiness), Is.True);
                Assert.That(f.Walk(), Is.EqualTo("Night")); Assert.That(f.Reads, Is.EqualTo(new[] { "wisdom" }));
                Assert.That(f.C.ProcessEntry(NarrativeEntryPoint.BeforeNight), Is.True); f.Walk(true);
                f.Relations(adele: 1, antoine: 2); Assert.That(f.Completed, Is.EqualTo(new[] { A04 })); f.Healthy();
            }
        }
    }
}
