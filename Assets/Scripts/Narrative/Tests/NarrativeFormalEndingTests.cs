using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Tests
{
    /// <summary>
    /// Actual generated configuration + actual Controller/result transactions. The host below is
    /// deliberately headless: it uses the production bounded conversation cursor, controller guards,
    /// choices and checks. It does NOT certify UI, CafeWorld interaction or new-game reachability.
    /// Author-blocked roots remain blocked; fixtures never grant construction/integration readiness.
    /// </summary>
    public sealed class NarrativeFormalEndingTests
    {
        private sealed class EndingCase
        {
            public readonly string Id, Root;
            public readonly int Day;
            public readonly string[] Ready, Choices;
            public readonly bool Blocked;
            public EndingCase(string id, string root, int day, string[] ready = null,
                string[] choices = null, bool blocked = false)
            { Id = id; Root = root; Day = day; Ready = ready ?? Array.Empty<string>(); Choices = choices ?? Array.Empty<string>(); Blocked = blocked; }
        }

        // Explicit historical fixtures, NOT a claim these histories are naturally obtainable.
        // In particular D seeds 1129.completed (the revenue authoring gate remains unresolved),
        // and bloom/unforgiven seed their upstream pending facts (1115 remains author-blocked).
        private static readonly EndingCase[] Cases =
        {
            new EndingCase("neutral.ending.a", "neutral.route.1111.s1", 37, new[] { "route.neutral" }),
            new EndingCase("neutral.ending.b", "neutral.route.1122.s1", 48,
                new[] { "route.neutral", "story.neutral.route.1118.s1.completed", "neutral.investigation.shelter" },
                new[] { "neutral.route.1122.s1.choice.future.b" }),
            new EndingCase("neutral.ending.c", "neutral.route.1130.s2", 56,
                new[] { "route.neutral", "story.neutral.route.1122.s1.completed", "neutral.stayed" }),
            new EndingCase("neutral.ending.d", "neutral.route.1130.s1", 56,
                new[] { "route.neutral", "story.neutral.route.1129.s1.completed", "neutral.association.committee", "neutral.stayed" }),
            new EndingCase("rose.ending.trust_limit.ending", "rose.ending.trust_limit", 21,
                new[] { "story.rose.ending.trust_limit.pending" }),
            new EndingCase("rose.ending.job_requirements.ending", "rose.ending.job_requirements", 27,
                new[] { "story.rose.ending.job_requirements.pending" }),
            new EndingCase("rose.route.1115.s1.ending.ideal_world", "rose.route.1115.s1", 41,
                new[] { "route.rose", "story.rose.route.1111.s2.completed" }, blocked: true),
            new EndingCase("rose.route.ending.unforgiven.ending", "rose.route.ending.unforgiven", 48,
                new[] { "route.rose", "story.rose.route.ending.unforgiven.pending" }),
            new EndingCase("rose.route.ending.bloom.ending", "rose.route.ending.bloom", 48,
                new[] { "route.rose", "story.rose.route.ending.bloom.pending" }),
            new EndingCase("church.ending.if2", "church.1023.s2", 18,
                new[] { "story.church.1023.s1.completed", "church.detention.release_failed", "church.detention.active" }),
            new EndingCase("church.ending.if4", "church.1111.s1", 37,
                new[] { "story.revolution.1107.s1.completed" },
                new[] { "church.1111.s1.choice.s1.2", "church.1111.s1.choice.s2.2" }),
            new EndingCase("church.ending.ry1", "church.route.1123.s1", 49,
                new[] { "route.church", "story.church.route.1118.s1.completed", "church.thibaut.followed" }, blocked: true),
            new EndingCase("church.ending.ry2", "church.route.1123.s1", 49,
                new[] { "route.church", "story.church.route.1118.s1.completed" }, blocked: true),
            new EndingCase("revolution.end01", "revolution.route.1124.s1", 50,
                new[] { "route.revolution", "story.revolution.route.1123.s1.completed" },
                new[] { "revolution.route.1124.s1.c0.1" }),
            new EndingCase("revolution.end02", "revolution.route.1124.s1", 50,
                new[] { "route.revolution", "story.revolution.route.1123.s1.completed" },
                new[] { "revolution.route.1124.s1.c0.2", "revolution.route.1124.s1.c1.1", "revolution.route.1124.s1.c2.1" })
        };
        // Authored opening refusal is maintained centrally, not by the four route compilers.
        // Its actual arrival/refuse/retry lifecycle is covered by Main's opening-specific tests.
        private const string OpeningRefusalEndingId = "arrival.ending.if0";
        private static EndingCase Find(string id) => Cases.Single(c => c.Id == id);

        /// <summary>No automatic completion. Even after reaching End the owner explicitly releases
        /// the completion callback, which lets tests cancel or hold a transaction at the last frame.</summary>
        private sealed class ScriptedOwner : INarrativePresenter, INarrativePresentationCancellation
        {
            public NarrativeController Controller;
            public NarrativeConfiguration Config;
            public NarrativeEventRequest Request;
            public Action Completion;
            public string ReachedEnd;
            public readonly List<string> Pages = new List<string>();
            public readonly List<string> Played = new List<string>();
            public readonly HashSet<string> Plan = new HashSet<string>(StringComparer.Ordinal);
            public event Action<string> Cancelled;
            public void Play(NarrativeEventRequest request, Action completed)
            {
                Request = request; Completion = completed; ReachedEnd = null;
                Pages.Clear(); Played.Add(request.Event.eventId);
            }
            public void WalkToEnd()
            {
                Assert.That(Completion, Is.Not.Null);
                var sorted = Config.presentations.conversationPages
                    .Where(p => p.presentationKey == Request.Event.presentationKey).OrderBy(p => p.seq).ToList();
                Assert.That(sorted, Is.Not.Empty, Request.Event.eventId);
                var cursor = new NarrativeConversationTraversal(sorted);
                while (cursor.MoveNext(Visible))
                {
                    var page = cursor.Current;
                    Pages.Add(page.pageId);
                    if (page.stepType == NarrativeConversationStepType.End) { ReachedEnd = page.pageId; break; }
                    if (page.stepType == NarrativeConversationStepType.Gameplay)
                    {
                        // Deliberately fail instead of manufacturing a coffee-host success.
                        Assert.Fail("This ending fixture unexpectedly requires a real host: " + page.pageId);
                    }
                    if (page.stepType == NarrativeConversationStepType.PassiveCheck)
                    {
                        if (!Controller.EvaluateInlineAbilityCheckForPresentation(page.checkId).passed)
                        { cursor.ResumeLinearAfterCurrent(); continue; }
                    }
                    if (string.IsNullOrWhiteSpace(page.optionGroupKey)) continue;
                    var choices = Config.choices.Where(c => c.groupKey == page.optionGroupKey && Plan.Contains(c.choiceId)).ToArray();
                    Assert.That(choices.Length, Is.EqualTo(1), "Explicit selection required for visible group " + page.optionGroupKey);
                    Assert.That(Controller.EvaluateConditions("choice", choices[0].choiceId, Request.Context), Is.True, choices[0].choiceId);
                    Assert.That(Controller.SelectChoice(choices[0].choiceId), Is.True, choices[0].choiceId);
                }
                Assert.That(ReachedEnd, Is.Not.Null, "Real source End must be reached before completing " + Request.Event.eventId);
            }
            public void FinishLeadingNewspaper()
            {
                Assert.That(Request.Event.eventId, Is.EqualTo("newspaper.1111"), "Never silently complete an unexpected preceding story");
                Assert.That(Request.Event.presentationClass, Is.EqualTo("Reading"));
                var pages = Config.presentations.readingPages
                    .Where(p => p.presentationKey == Request.Event.presentationKey).OrderBy(p => p.seq).ToArray();
                Assert.That(pages, Is.Not.Empty);
                foreach (var page in pages)
                {
                    Assert.That(string.IsNullOrWhiteSpace(page.pageCondKeys), Is.True, page.pageId);
                    Assert.That(string.IsNullOrWhiteSpace(page.pageBarConds), Is.True, page.pageId);
                    var lines = Config.presentations.readingLines.Where(l => l.pageId == page.pageId).ToArray();
                    Assert.That(lines, Is.Not.Empty, page.pageId);
                    foreach (var line in lines)
                        Assert.That(Config.texts.Any(t => t.key == line.textKey && !string.IsNullOrWhiteSpace(t.content)), Is.True, line.textKey);
                }
                // Actual reading event completion/results; do not mark its history directly.
                Completion();
            }
            private bool Visible(NarrativeConversationPageDefinition page)
            {
                Assert.That(string.IsNullOrWhiteSpace(page.pageBarConds), Is.True, page.pageId);
                return string.IsNullOrWhiteSpace(page.pageCondKeys) || page.pageCondKeys
                    .Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .All(g => Controller.ReadPresentationCondition(g.Trim()));
            }
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
        }

        private sealed class Fixture : IDisposable
        {
            public readonly NarrativeConfiguration Config;
            public readonly NarrativeController Controller;
            public readonly ScriptedOwner Owner;
            public readonly EndingCase Case;
            public readonly List<string> Selected = new List<string>();
            public int Day;
            public Fixture(EndingCase item, int? language = null)
            {
                Case = item; Day = item.Day; Config = LubanNarrativeLoader.Load();
                Owner = new ScriptedOwner { Config = Config };
                Owner.Plan.UnionWith(item.Choices);
                Controller = new NarrativeController(Config, Owner);
                Owner.Controller = Controller;
                Controller.BindValueProvider(new DelegateNarrativeValueProvider("dayflow", key => key == "day" ? Day : (int?)null));
                Controller.BindValueProvider(new DelegateNarrativeValueProvider("cafe", key => key == "money" ? 10000 : (int?)null));
                Controller.BindValueProvider(new DelegateNarrativeValueProvider("actions", key => key == "current" || key == "max" ? 6 : (int?)null));
                Controller.BeginValueRestore();
                // Known numerical branch inputs; no ready/terminal/choice/check facts are invented.
                Assert.That(Controller.SetBar("player.language", language ?? (item.Id == "church.ending.if4" ? 3 : 6)), Is.True);
                Assert.That(Controller.SetBar("player.courage", 18), Is.True);
                Controller.AbilityReader = attribute =>
                {
                    bool ready = Controller.State.TryGetBar("player." + attribute, out int value);
                    return (ready, value);
                };
                foreach (string ready in item.Ready)
                {
                    Assert.That(IsConstruction(ready), Is.False, "Fixtures cannot grant author readiness");
                    Controller.State.SetCondition(ready, true);
                }
                Controller.SetOfficialDay(Day);
                Controller.CompleteValueRestore(false);
                Controller.EndingSelected += e => Selected.Add(e.endingId);
                Controller.SetEndingChecksEnabled(true);
                Assert.That(Controller.IsEnded, Is.False, "History alone must not end the session");
            }
            public void Start()
            {
                var root = Config.events.Single(e => e.eventId == Case.Root);
                var date = Config.eventDates.FirstOrDefault(d => d.eventId == Case.Root);
                if (date != null)
                {
                    var entry = date.timeSlot == NarrativeTimeSlot.Morning ? NarrativeEntryPoint.MorningBeforeMap
                        : date.timeSlot == NarrativeTimeSlot.Noon ? NarrativeEntryPoint.AfterBusiness : NarrativeEntryPoint.BeforeNight;
                    Assert.That(Controller.ProcessEntry(entry), Is.True, Case.Root);
                }
                else if (root.entry == NarrativeEntryPoint.Immediate) Assert.That(Controller.QueueEvent(Case.Root), Is.True, Controller.ExplainEvent(Case.Root).ToString());
                else Assert.That(Controller.ProcessEntry(root.entry), Is.True, Case.Root);
                if (Controller.CurrentEventId == "newspaper.1111" && Case.Root == "church.1111.s1")
                    Owner.FinishLeadingNewspaper();
                Assert.That(Controller.CurrentEventId, Is.EqualTo(Case.Root), "Must actually enter the requested generated story root");
            }
            public void Dispose() => Controller.UnbindValueProviders();
        }
        private static bool IsConstruction(string key) => key.StartsWith("construction.", StringComparison.Ordinal)
            || key.StartsWith("integration.", StringComparison.Ordinal) || key.StartsWith("blocked.", StringComparison.Ordinal);
        private static IEnumerable<NarrativeConditionDefinition> Gates(NarrativeConfiguration c, string id)
            => c.conditions.Where(x => x.ownerType == "ending" && x.ownerId == id);
        private static IEnumerable<NarrativeResultDefinition> TerminalResults(NarrativeConfiguration c, EndingCase item)
            => c.results.Where(r => r.eventId == item.Root && r.resultType == NarrativeResultType.SetFlag && r.value == "true"
                && Gates(c, item.Id).Any(g => g.target == r.target));

        [Test]
        public void EveryEnabledEndingHasExplicitCoverageAndAConditionedRootTailProducer()
        {
            var c = LubanNarrativeLoader.Load();
            Assert.That(c.endings.Where(e => e.enabled).Select(e => e.endingId),
                Is.EquivalentTo(Cases.Select(x => x.Id).Concat(new[] { OpeningRefusalEndingId })));
            foreach (var item in Cases)
            {
                var ending = c.endings.Single(e => e.endingId == item.Id);
                Assert.That(c.texts.Any(t => t.key == ending.titleText && !string.IsNullOrWhiteSpace(t.content)), Is.True, item.Id + " title");
                Assert.That(c.texts.Any(t => t.key == ending.messageText && !string.IsNullOrWhiteSpace(t.content)), Is.True, item.Id + " message");
                var gates = Gates(c, item.Id).ToArray();
                Assert.That(gates, Is.Not.Empty, item.Id);
                foreach (var group in gates.GroupBy(g => g.groupIndex))
                {
                    Assert.That(group.All(g => g.condType == "BOOL_EQ" && g.value == "true"), Is.True, item.Id);
                    var produced = group.Where(g => !g.target.StartsWith("route.", StringComparison.Ordinal)).ToArray();
                    Assert.That(produced.Select(g => g.target).Distinct().Count(), Is.GreaterThanOrEqualTo(2), "Both terminal/selection AND root finish evidence required: " + item.Id);
                    foreach (var gate in produced)
                    {
                        var producers = c.results.Where(r => r.target == gate.target && r.resultType == NarrativeResultType.SetFlag && r.value == "true").ToArray();
                        Assert.That(producers, Is.Not.Empty, gate.target);
                        Assert.That(producers.All(p => p.eventId == item.Root), Is.True, "Terminal must not be produced by an unrelated event/choice owner: " + gate.target);
                    }
                }
                Assert.That(c.conditionKeys.Where(k => gates.Any(g => g.target == k.key)).All(k => !k.initialValue), Is.True, item.Id);
                var root = c.events.Single(e => e.eventId == item.Root);
                Assert.That(c.presentations.conversationPages.Any(p => p.presentationKey == root.presentationKey && p.stepType == NarrativeConversationStepType.End), Is.True, item.Id);
            }
            AssertOpeningRefusalContract(c);
        }

        private static void AssertOpeningRefusalContract(NarrativeConfiguration c)
        {
            var ending = c.endings.Single(e => e.endingId == OpeningRefusalEndingId);
            Assert.That(ending.titleText, Is.EqualTo("arrival.refuse.01.08"), "Reuse authored opening title");
            Assert.That(ending.messageText, Is.EqualTo("arrival.refuse.01.04"), "Reuse authored opening message");
            Assert.That(c.texts.Any(t => t.key == ending.titleText && !string.IsNullOrWhiteSpace(t.content)), Is.True);
            Assert.That(c.texts.Any(t => t.key == ending.messageText && !string.IsNullOrWhiteSpace(t.content)), Is.True);
            var gates = Gates(c, OpeningRefusalEndingId).ToArray();
            Assert.That(gates, Is.Not.Empty);
            foreach (var group in gates.GroupBy(g => g.groupIndex))
            {
                Assert.That(group.Any(g => g.condType == "BOOL_EQ" && g.target == "story.arrival.leave.completed" && g.value == "true"), Is.True,
                    "Every IF0 alternative needs the real leave root-tail fact");
                Assert.That(group.Any(g => g.condType == "EVENT_HAPPENED" && (g.target == "arrival.leave" || g.target == "event:arrival.leave")), Is.True,
                    "Every IF0 alternative needs actual leave-root occurrence, not initial refusal/retry");
            }
            var key = c.conditionKeys.Single(k => k.key == "story.arrival.leave.completed");
            Assert.That(key.initialValue, Is.False);
            var producers = c.results.Where(r => r.resultType == NarrativeResultType.SetFlag && r.target == key.key && r.value == "true").ToArray();
            Assert.That(producers, Is.Not.Empty);
            Assert.That(producers.All(r => r.eventId == "arrival.leave"), Is.True,
                "Try-again/refusal dialogue must never produce the irreversible IF0 completion fact");
        }

        // One explicit case per enabled route Ending; IF0's distinct opening flow is tested separately by Main.
        // Do not silently skip disabled-by-author route content.
        [TestCase("neutral.ending.a")]
        [TestCase("neutral.ending.b")]
        [TestCase("neutral.ending.c")]
        [TestCase("neutral.ending.d")]
        [TestCase("rose.ending.trust_limit.ending")]
        [TestCase("rose.ending.job_requirements.ending")]
        [TestCase("rose.route.1115.s1.ending.ideal_world")]
        [TestCase("rose.route.ending.unforgiven.ending")]
        [TestCase("rose.route.ending.bloom.ending")]
        [TestCase("church.ending.if2")]
        [TestCase("church.ending.if4")]
        [TestCase("church.ending.ry1")]
        [TestCase("church.ending.ry2")]
        [TestCase("revolution.end01")]
        [TestCase("revolution.end02")]
        public void FormalRootFinishesOnlyAtASafeBoundaryOrRemainsAuthorBlocked(string id)
        {
            var item = Find(id);
            using (var f = new Fixture(item))
            {
                if (item.Blocked)
                {
                    AssertAuthorBlocked(f);
                    return;
                }
                f.Start();
                Assert.That(f.Controller.State.HasHappenedEvent(item.Root), Is.False);
                f.Owner.WalkToEnd();
                Assert.That(f.Controller.IsEnded, Is.False, "Seeing an End page is not completion");
                foreach (var result in TerminalResults(f.Config, item)) Assert.That(f.Controller.State.GetCondition(result.target), Is.False, result.target);
                var completion = f.Owner.Completion;
                f.Controller.BeginOperation();
                completion();
                Assert.That(f.Controller.State.HasHappenedEvent(item.Root), Is.True);
                Assert.That(f.Controller.IsEnded, Is.False, "Must wait for host operation's safe boundary");
                f.Controller.EndOperation();
                Assert.That(f.Controller.IsSafeBoundary, Is.True);
                Assert.That(f.Controller.SelectedEndingId, Is.EqualTo(id));
                Assert.That(f.Selected, Is.EqualTo(new[] { id }));
                completion(); // Repeated/late callback cannot select again or alter the ending.
                f.Controller.NotifyConditionsChanged(); f.Controller.CheckEndingsAtSafeBoundary();
                Assert.That(f.Selected, Has.Count.EqualTo(1));
                Assert.That(f.Controller.SelectedEndingId, Is.EqualTo(id));
                Assert.That(f.Controller.QueueEvent(item.Root), Is.False);
                foreach (var other in Cases.Where(x => x.Id != id))
                    Assert.That(f.Controller.EvaluateConditions("ending", other.Id), Is.False, "Wrong ending gates: " + other.Id);
                AssertEndedRestore(f, id);
            }
        }

        [TestCase("neutral.ending.a")]
        [TestCase("neutral.ending.b")]
        [TestCase("neutral.ending.c")]
        [TestCase("neutral.ending.d")]
        [TestCase("rose.ending.trust_limit.ending")]
        [TestCase("rose.ending.job_requirements.ending")]
        [TestCase("rose.route.ending.unforgiven.ending")]
        [TestCase("rose.route.ending.bloom.ending")]
        [TestCase("church.ending.if2")]
        [TestCase("church.ending.if4")]
        [TestCase("revolution.end01")]
        [TestCase("revolution.end02")]
        public void CancellingAfterTheActualLastPageDoesNotCommitEndingOrChoices(string id)
        {
            var item = Find(id);
            using (var f = new Fixture(item))
            {
                f.Start(); f.Owner.WalkToEnd();
                var stale = f.Owner.Completion;
                f.Owner.Cancel(); stale();
                Assert.That(f.Controller.IsEnded, Is.False);
                Assert.That(f.Controller.State.HasHappenedEvent(item.Root), Is.False);
                Assert.That(f.Selected, Is.Empty);
                foreach (var result in TerminalResults(f.Config, item)) Assert.That(f.Controller.State.GetCondition(result.target), Is.False, result.target);
                Assert.That(f.Controller.SelectedChoices, Is.Empty);
                Assert.That(f.Controller.IsBusy, Is.False);
            }
        }

        [TestCase("neutral.ending.a")]
        [TestCase("neutral.ending.b")]
        [TestCase("neutral.ending.c")]
        [TestCase("neutral.ending.d")]
        [TestCase("rose.ending.trust_limit.ending")]
        [TestCase("rose.ending.job_requirements.ending")]
        [TestCase("rose.route.1115.s1.ending.ideal_world")]
        [TestCase("rose.route.ending.unforgiven.ending")]
        [TestCase("rose.route.ending.bloom.ending")]
        [TestCase("church.ending.if2")]
        [TestCase("church.ending.if4")]
        [TestCase("church.ending.ry1")]
        [TestCase("church.ending.ry2")]
        [TestCase("revolution.end01")]
        [TestCase("revolution.end02")]
        public void EveryGateIsNecessary_NoDateRouteOrSingleTerminalFlagFalsePositive(string id)
        {
            var c = LubanNarrativeLoader.Load();
            var gates = Gates(c, id).Select(g => g.target).Distinct().ToArray();
            foreach (var missing in gates)
            {
                // Isolated condition-matrix test, in ADDITION to actual root completion tests.
                // This intentionally stages gate facts, not an assertion of gameplay reachability.
                using (var f = new Fixture(Find(id)))
                {
                    f.Controller.BeginOperation();
                    foreach (string key in gates.Where(k => k != missing)) f.Controller.State.SetCondition(key, true);
                    if (f.Controller.State.GetCondition(missing)) f.Controller.State.SetCondition(missing, false);
                    f.Controller.EndOperation();
                    Assert.That(f.Controller.EvaluateConditions("ending", id), Is.False, id + " missing " + missing);
                    Assert.That(f.Controller.IsEnded, Is.False, id + " missing " + missing);
                }
            }
        }

        private static void AssertAuthorBlocked(Fixture f)
        {
            var root = f.Config.events.Single(e => e.eventId == f.Case.Root);
            Assert.That(f.Controller.ConstructionBlocker(root), Is.Not.Empty, f.Case.Id);
            var readiness = f.Config.conditions.Where(c => c.ownerType == "event" && c.ownerId == root.eventId && IsConstruction(c.target)).ToArray();
            Assert.That(readiness, Is.Not.Empty);
            foreach (var condition in readiness)
            {
                Assert.That(f.Controller.State.GetCondition(condition.target), Is.False, condition.target);
                Assert.That(f.Config.results.Any(r => r.target == condition.target && r.value == "true"), Is.False, "Unresolved readiness gained a producer");
            }
            Assert.That(f.Controller.QueueEvent(root.eventId), Is.False);
            var date = f.Config.eventDates.Single(d => d.eventId == root.eventId);
            var entry = date.timeSlot == NarrativeTimeSlot.Night ? NarrativeEntryPoint.BeforeNight : NarrativeEntryPoint.MorningBeforeMap;
            Assert.That(f.Controller.ProcessEntry(entry), Is.True);
            Assert.That(f.Owner.Played, Does.Not.Contain(root.eventId));
            Assert.That(f.Controller.IsEnded, Is.False);
            Assert.That(f.Controller.State.HasHappenedEvent(root.eventId), Is.False);
            foreach (var result in TerminalResults(f.Config, f.Case)) Assert.That(f.Controller.State.GetCondition(result.target), Is.False);
        }

        private static void AssertEndedRestore(Fixture source, string id)
        {
            var saved = source.Controller.CaptureSnapshot();
            using (var restored = new Fixture(source.Case))
            {
                restored.Controller.RestoreSnapshot(saved);
                restored.Controller.CompleteValueRestore(false);
                restored.Controller.SetEndingChecksEnabled(true);
                restored.Controller.ResumePendingEvents();
                Assert.That(restored.Controller.SelectedEndingId, Is.EqualTo(id));
                Assert.That(restored.Controller.IsEnded, Is.True);
                Assert.That(restored.Owner.Played, Is.Empty, "Ended save must not replay a story");
                Assert.That(restored.Selected, Is.Empty, "Restore must not re-emit an earned-ending notification");
                Assert.That(restored.Controller.QueueEvent(source.Case.Root), Is.False);
                Assert.That(restored.Controller.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.False);
                Assert.That(restored.Controller.SetBar("player.language", 1), Is.False);
            }
        }

        [Test]
        public void StayingInNeutral1122IsNotTheLeavingEnding()
        {
            using (var f = new Fixture(Find("neutral.ending.b")))
            {
                f.Owner.Plan.Clear(); f.Owner.Plan.Add("neutral.route.1122.s1.choice.future.a");
                f.Start(); f.Owner.WalkToEnd(); f.Owner.Completion();
                Assert.That(f.Owner.ReachedEnd, Is.EqualTo("neutral.route.1122.s1.stay_end"));
                Assert.That(f.Controller.State.GetCondition("neutral.stayed"), Is.True, "Choice resultOwner must actually settle");
                Assert.That(f.Controller.State.GetCondition("story.neutral.route.1122.s1.ending.b"), Is.False);
                Assert.That(f.Controller.IsEnded, Is.False);
            }
        }

        [Test]
        public void NegotiationFailureQueuesTheAuthoredCEpilogue_NotD()
        {
            using (var f = new Fixture(Find("neutral.ending.d"), language: 4))
            {
                // Real C continuation additionally requires 1122 stay history; it is not fabricated
                // by making 1129 incomplete, and the original D fixture's history remains explicit.
                f.Controller.State.SetCondition("story.neutral.route.1122.s1.completed", true);
                f.Start(); f.Owner.WalkToEnd();
                Assert.That(f.Owner.ReachedEnd, Is.EqualTo("neutral.route.1130.s1.negotiation_failure_end"));
                f.Owner.Completion();
                Assert.That(f.Controller.IsEnded, Is.False);
                Assert.That(f.Controller.State.GetCondition("neutral.negotiation.failed"), Is.True);
                Assert.That(f.Controller.State.GetCondition("story.neutral.route.1130.s1.ending.d"), Is.False);
                Assert.That(f.Controller.CurrentEventId, Is.EqualTo("neutral.route.1130.s2"));
                f.Owner.WalkToEnd(); f.Owner.Completion();
                Assert.That(f.Controller.SelectedEndingId, Is.EqualTo("neutral.ending.c"));
                Assert.That(f.Selected, Is.EqualTo(new[] { "neutral.ending.c" }));
            }
        }

        [Test]
        public void If4RequiresItsActualLowLanguageBranch_NotOnlyTheSameChoices()
        {
            using (var f = new Fixture(Find("church.ending.if4"), language: 4))
            {
                f.Owner.Plan.Add("church.1111.s1.choice.s3.2");
                f.Start(); f.Owner.WalkToEnd();
                Assert.That(f.Owner.Pages, Does.Not.Contain("church.1111.s1.if4_end"));
                var terminal = f.Config.results.Single(r => r.target == "terminal.church.ending.if4");
                Assert.That(f.Controller.EvaluateConditions("result", terminal.resultId), Is.False);
                f.Owner.Cancel();
                Assert.That(f.Controller.IsEnded, Is.False);
            }
        }
    }
}
