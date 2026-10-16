using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spotlight.Narrative.Tests
{
    /// <summary>
    /// Generated A05 data, real CafeWorld production and the Controller service protocol, without UI/singletons.
    /// A04 completed is an explicit independent fixture input, NOT evidence of a natural B/A04/A05 playthrough.
    /// Domain is reflection-bound only because this existing test asmdef intentionally has no CafeDomain reference.
    /// No domain verdict/receipt is fabricated: every receipt comes from DeliverProductionCup's actual outbox.
    /// </summary>
    public sealed class NeutralReporterAcceptanceTests
    {
        private const string Root = "neutral.1018.s1";
        private const string Schedule = Root + ".schedule";
        private const string Visit = Root + ".reporter";
        private const string Character = "neutral.reporter";
        private const string Previous = "story.neutral.1013.s2.completed";
        private const string Completed = "story." + Root + ".completed";
        private const string Recommend = "story." + Root + ".recommendation_requested";
        private const string Advert = "story." + Root + ".advertisement_requested";
        private const string Printed = "story." + Root + ".printed";
        private const string NoticeRecommendation = Root + ".notice.recommendation";
        private const string NoticeAdvertisement = Root + ".notice.advertisement";
        private const string Yes = Root + ".choice.q_ad.yes";
        private const string No = Root + ".choice.q_ad.no";
        private static readonly string[] OwnedEvents = { Root, Schedule, NoticeRecommendation, NoticeAdvertisement };

        // This is a narrow typed-protocol reflection adapter, not a parallel domain or a mocked service.
        private sealed class Domain
        {
            private readonly Assembly assembly = Assembly.Load("Spotlight.FreeCafe.Domain");
            public readonly object World;
            public readonly object Catalog;
            public object State => Get(World, "State");
            public object Counter => Enum.Parse(assembly.GetType("Spotlight.FreeCafe.AreaId", true), "Counter");
            public int Money => (int)Get(State, "money");
            public IList Feedback => (IList)Get(State, "cafeVisitFeedback");
            public Domain()
            {
                Catalog = JsonUtility.FromJson(Resources.Load<TextAsset>("FreeCafe/catalog").text,
                    assembly.GetType("Spotlight.FreeCafe.CafeCatalog", true));
                // Independent deterministic business inputs: reserve the only customer budget for the reporter.
                var economy = Get(Catalog, "economy"); Set(economy, "baseCustomers", 1); Set(economy, "maxCustomers", 1);
                Set(Catalog, "customerInterval", 10000f);
                World = Activator.CreateInstance(assembly.GetType("Spotlight.FreeCafe.CafeWorld", true), new[] { Catalog, null });
            }
            public static object Get(object value, string name)
            {
                Assert.That(value, Is.Not.Null, "Reading " + name + " from a missing domain object");
                var field = value.GetType().GetField(name);
                return field != null ? field.GetValue(value) : value.GetType().GetProperty(name).GetValue(value);
            }
            public static void Set(object value, string name, object data) => value.GetType().GetField(name).SetValue(value, data);
            public object Call(string name, params object[] args) => Invoke(World, name, args);
            private static object Invoke(object owner, string name, object[] args)
            {
                var candidates = owner.GetType().GetMethods().Where(m => m.Name == name && m.GetParameters().Length >= args.Length
                    && m.GetParameters().Skip(args.Length).All(p => p.IsOptional));
                foreach (var method in candidates)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Take(args.Length).Where((p, i) => args[i] != null && !p.ParameterType.IsInstanceOfType(args[i])).Any()) continue;
                    var full = parameters.Select((p, i) => i < args.Length ? args[i] : p.DefaultValue).ToArray();
                    try { return method.Invoke(owner, full); }
                    catch (TargetInvocationException error) { throw error.InnerException ?? error; }
                }
                throw new MissingMethodException(owner.GetType().FullName, name + " (" + args.Length + " matching arguments)");
            }
            public void Ok(string name, params object[] args)
            {
                var result = Call(name, args);
                Assert.That((bool)Get(result, "success"), Is.True, name + ": " + JsonUtility.ToJson(result));
            }
            public object Find(string id) => Call("Find", id);
            public object Product => Call("Product", "espresso");
            public object Cup => Get(World, "ServingCup");
            public object CafeVisit(string id) => Call("CafeVisit", id);
            public void Balance(int amount)
            {
                var args = new object[] { amount, null };
                Assert.That((bool)World.GetType().GetMethod("TryInitializeMoney").Invoke(World, args), Is.True, args[1] as string);
            }
            public bool Commit(int expected, int final, out string error)
            {
                var args = new object[] { expected, final, null };
                bool ok = (bool)World.GetType().GetMethod("TryCommitMoney").Invoke(World, args);
                error = args[2] as string; return ok;
            }
            public void Sync(NarrativeCafeVisitSnapshot snapshot)
            {
                var type = assembly.GetType("Spotlight.FreeCafe.CafeVisitIntent", true);
                var intents = Array.CreateInstance(type, snapshot.Visits.Count);
                int i = 0;
                foreach (var visit in snapshot.Visits)
                {
                    var intent = Activator.CreateInstance(type);
                    foreach (var pair in new Dictionary<string, object>
                    {
                        ["visitInstanceId"] = visit.VisitInstanceId, ["characterId"] = visit.CharacterId, ["visitKey"] = visit.VisitKey,
                        ["arrivalAuthorized"] = visit.ArrivalAuthorized, ["isEnded"] = visit.IsEnded, ["endReason"] = visit.EndReason,
                        ["orderProductId"] = visit.OrderProductId, ["requireRealProduction"] = visit.RequireRealProduction,
                        ["preferredSeatId"] = visit.PreferredSeatId, ["seatedVisualPath"] = visit.SeatedVisualPath,
                        ["arrivalDelaySeconds"] = visit.ArrivalDelaySeconds, ["hasPatienceOverride"] = visit.PatienceSeconds.HasValue,
                        ["patienceSeconds"] = visit.PatienceSeconds ?? 0, ["leaveAfterServed"] = visit.LeaveAfterServed,
                        ["usesDefaultConfiguration"] = visit.UsesDefaultConfiguration
                    }) Set(intent, pair.Key, pair.Value);
                    var identityType = assembly.GetType("Spotlight.FreeCafe.CafePersonIdentity", true);
                    var identity = Activator.CreateInstance(identityType);
                    Set(identity, "personId", visit.CharacterId); Set(identity, "characterId", visit.CharacterId);
                    Set(identity, "narrativeCharacterId", visit.CharacterId); Set(identity, "displayName", "Reporter test identity");
                    Set(identity, "role", Enum.Parse(assembly.GetType("Spotlight.FreeCafe.CafePersonRole", true), "Customer"));
                    Set(intent, "identity", identity); intents.SetValue(intent, i++);
                }
                Ok("SyncCafeVisits", intents);
            }
            public NarrativeCafeVisitFeedback ConvertFact(object actual)
            {
                // JsonUtility copies actual DTO fields by name, exactly like CafeVisitBridge's explicit field mapping.
                // It does not manufacture a cup, order, quality verdict or service receipt.
                return JsonUtility.FromJson<NarrativeCafeVisitFeedback>(JsonUtility.ToJson(actual));
            }
            public string Espresso(bool good)
            {
                Ok("EnterProduction");
                if (!good)
                {
                    Ok("SelectServingCup", "s.cups.small");
                    string failedCup = (string)Get(Cup, "id");
                    Ok("TakeAndInteract", "s.water", failedCup, Counter, 0f, 0f);
                    Ok("DeliverProductionCup"); // A real but incorrect water cup: domain alone decides remake/final failure.
                    return failedCup;
                }
                Ok("TakeAndInteract", "s.beans", "m.grinder", Counter, 0f, 0f);
                Ok("OperateProduction", "m.grinder"); Call("Tick", 12f);
                object output = Call("DragOutputPreview", "m.grinder");
                Assert.That(output, Is.Not.Null, "Real grinder must produce grounds before extraction.");
                Ok("TakeMachineOutput", "m.grinder", (string)Get(output, "id"), "v.extractor", Counter, -3.3f, .2f);
                Ok("TakeAndInteract", "s.water", "m.heater", Counter, 0f, 0f);
                Ok("OperateProduction", "m.heater"); Call("Tick", 4f);
                var extraction = Call("ExtractionSnapshot", "v.extractor");
                float ml = (float)Get(extraction, "idealMl"), seconds = (float)Get(extraction, "idealSeconds");
                Assert.That((float)Call("LiquidVolume", Find("m.heater")), Is.GreaterThanOrEqualTo(ml));
                for (int step = 0; step < 15; step++)
                {
                    Ok("PourLiquid", "m.heater", "v.extractor", ml / 15, seconds / 15); Call("Tick", seconds / 15);
                }
                Call("FinishPour", "v.extractor"); Call("Tick", .2f);
                extraction = Call("ExtractionSnapshot", "v.extractor");
                Assert.That((bool)Get(extraction, "completed"), Is.True);
                Assert.That((string)Get(extraction, "qualityLabel"), Is.EqualTo("优秀精粹咖啡液"));
                Ok("SelectServingCup", "s.cups.small"); string cupId = (string)Get(Cup, "id");
                Ok("PourLiquid", "v.extractor", cupId, ml, 1f); Call("FinishPour", cupId);
                Assert.That((string)Get(Call("ClassifyCoffee", Cup), "productId"), Is.EqualTo("espresso"));
                Ok("DeliverProductionCup"); return cupId;
            }
        }

        private sealed class Presenter : INarrativePresenter, INarrativePresentationCheckpoints, INarrativePresentationCancellation
        {
            public event Action<NarrativeEventRequest, string, NarrativeCheckpointWait> CheckpointReached;
            public event Action<string> Cancelled;
            public readonly List<NarrativeEventRequest> Requests = new List<NarrativeEventRequest>();
            public readonly List<Action> Callbacks = new List<Action>();
            public NarrativeEventRequest Current => Requests.Last();
            public Action Completion => Callbacks.Last();
            public void Play(NarrativeEventRequest request, Action completed)
            {
                // Match the real presenter's result-only fast path, rather than falsely presenting the schedule.
                if (!NarrativePresenter.RequiresPresentation(request.Event, null)) { completed(); return; }
                Requests.Add(request); Callbacks.Add(completed);
            }
            public void Wait(string page) => CheckpointReached?.Invoke(Current, page, NarrativeCheckpointWait.Service);
            public void Cancel() => Cancelled?.Invoke("playback_cancelled");
        }

        private sealed class Fixture : IDisposable, INarrativeMoneyAccount
        {
            public readonly NarrativeConfiguration Config = LubanNarrativeLoader.Load();
            public readonly Domain D = new Domain();
            public readonly Presenter P = new Presenter();
            public readonly NarrativeController C;
            public readonly List<string> CompletedEvents = new List<string>();
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Visible = new List<string>();
            public readonly List<string> Offered = new List<string>();
            public readonly List<NarrativeCafeVisitFeedback> Submitted = new List<NarrativeCafeVisitFeedback>();
            public string AdChoice = No;
            public int Day;
            public bool MutateAtCommit;
            public int MoneyCommits;
            public Fixture(int day = 13, bool previous = true)
            {
                Day = day; C = new NarrativeController(Config, P);
                foreach (var ev in Config.events.Where(e => !OwnedEvents.Contains(e.eventId))) C.State.SetEventLocked(ev.eventId, true);
                C.BeginValueRestore();
                foreach (var group in Config.bars.Where(b => b.sourceMode == NarrativeSourceMode.External).GroupBy(b => b.providerId))
                {
                    var definitions = group.ToDictionary(b => b.sourceKey);
                    C.BindValueProvider(new DelegateNarrativeValueProvider(group.Key, key =>
                    {
                        if (!definitions.TryGetValue(key, out var bar)) return null;
                        return bar.providerId == "dayflow" && key == "day" ? Day : bar.barKey == "cafe.money" ? D.Money : bar.initValue;
                    }));
                }
                C.MoneyAccount = this;
                if (previous) C.State.SetCondition(Previous, true); // Independent fixture; no claim of naturally played A04.
                C.SetOfficialDay(day); C.CompleteValueRestore(false); C.ProcessOfficialDay(day);
                C.EventCompleted += CompletedEvents.Add; C.EventFailed += (id, error) => Failures.Add(id + ": " + error);
            }
            public void Dispose() => C.UnbindValueProviders();
            public bool TryRead(out int balance) { balance = D.Money; return true; }
            public bool TryCommit(int expectedBalance, int finalBalance, out string error)
            {
                MoneyCommits++;
                if (MutateAtCommit) { MutateAtCommit = false; D.Balance(expectedBalance - 1); }
                return D.Commit(expectedBalance, finalBalance, out error);
            }
            public bool Flag(string key) => C.State.GetCondition(key);
            public void Morning() => Assert.That(C.ProcessEntry(NarrativeEntryPoint.MorningBeforeMap), Is.True);
            public void Healthy() => Assert.That(Failures, Is.Empty, string.Join("\n", Failures));
            public void Uncommitted()
            {
                Assert.That(Flag(Completed) || Flag(Recommend) || Flag(Advert), Is.False);
                Assert.That(C.State.HasHappenedEvent(Root), Is.False);
                Assert.That(C.State.IsFlagSet("event.once." + Root), Is.False);
            }
            public void Pump()
            {
                D.Sync(C.GetCommittedCafeVisits());
                foreach (object actual in D.Feedback.Cast<object>().ToArray())
                {
                    var fact = D.ConvertFact(actual); Submitted.Add(fact);
                    Assert.That(C.SubmitCafeVisitFeedback(fact), Is.True, "Actual domain feedback rejected: " + fact.stage);
                    if (C.IsCafeVisitFeedbackComplete(fact)) D.Ok("AcknowledgeCafeVisitFeedback", fact.sequence);
                }
            }
            public NarrativeCafeVisit Reporter => C.GetCommittedCafeVisits().Visits.Single(v => v.VisitKey == Visit);
            public object DomainVisit => D.CafeVisit(Reporter.VisitInstanceId);
            public object Receipt => Domain.Get(DomainVisit, "serviceReceipt");
            public void Arrive()
            {
                Morning(); Assert.That(CompletedEvents, Is.EqualTo(new[] { Schedule }));
                Assert.That(P.Requests, Is.Empty, "Result-only schedule is not a narrative presentation.");
                Assert.That(C.QueueEvent(Root), Is.False, "A service root requires the live hook context.");
                Pump(); D.Ok("StartDay"); D.Call("Tick", 149f); Pump();
                Assert.That(P.Requests, Is.Empty); Assert.That(Domain.Get(DomainVisit, "orderId"), Is.Null.Or.Empty);
                D.Call("Tick", 1f); Pump();
                Assert.That(P.Current.Event.eventId, Is.EqualTo(Root));
                var context = P.Current.CafeVisit;
                Assert.That(context, Is.Not.Null); Assert.That(context.visitInstanceId, Is.EqualTo(Reporter.VisitInstanceId));
                Assert.That(context.characterId, Is.EqualTo(Character)); Assert.That(context.productId, Is.EqualTo("espresso"));
                Assert.That(context.orderId, Is.EqualTo(Domain.Get(DomainVisit, "orderId")));
                Assert.That(context.orderId, Is.Not.Null.And.Not.Empty);
                Assert.That(Submitted.Any(f => f.stage == "arrived" && !f.isClick && f.orderId == context.orderId), Is.True);
                D.Call("Focus", context.orderId);
            }
            public string Walk(bool complete = false)
            {
                var request = P.Current;
                var pages = Config.presentations.conversationPages.Where(p => p.presentationKey == request.Event.presentationKey).OrderBy(p => p.seq).ToArray();
                int start = string.IsNullOrEmpty(request.ResumePageId) ? 0 : Array.FindIndex(pages, p => p.pageId == request.ResumePageId);
                Assert.That(start, Is.GreaterThanOrEqualTo(0));
                for (int i = start; i < pages.Length; i++)
                {
                    var page = pages[i]; Assert.That(page.pageBarConds, Is.Null.Or.Empty);
                    bool visible = true;
                    foreach (string key in (page.pageCondKeys ?? "").Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                        if (!C.ReadPresentationCondition(key.Trim())) { visible = false; break; }
                    if (!visible) continue;
                    Visible.Add(page.pageId);
                    if (page.stepType == NarrativeConversationStepType.WaitService)
                    {
                        Assert.That(i + 1, Is.LessThan(pages.Length)); P.Wait(pages[i + 1].pageId); return "Service";
                    }
                    if (!string.IsNullOrEmpty(page.optionGroupKey) && !C.SelectedChoices.Any(id => Config.choices.Any(c => c.choiceId == id && c.groupKey == page.optionGroupKey)))
                    {
                        Offered.Add(page.optionGroupKey);
                        Assert.That(C.SelectChoice(AdChoice), Is.True, "Select the permitted generated option: " + AdChoice);
                    }
                }
                if (complete) P.Completion(); return "End";
            }
            public void WaitForCup()
            {
                Assert.That(Walk(), Is.EqualTo("Service"));
                Assert.That(C.HasPendingServiceCheckpoint && C.IsSafeBoundary && !C.BlocksSave, Is.True);
                Assert.That(C.ResumeServiceCheckpoint(), Is.False, "An unserved order is not confirmed.");
                Uncommitted(); Pump();
            }
            public string Deliver(bool good)
            {
                string cup = D.Espresso(good); Pump(); return cup;
            }
            public void FinalFailure()
            {
                int maxRemakes = (int)Domain.Get(D.Product, "maxRemakes");
                for (int n = 0; n <= maxRemakes; n++) Deliver(false);
                Assert.That(Receipt, Is.Not.Null); Assert.That((bool)Domain.Get(Receipt, "satisfied"), Is.False);
                Assert.That(P.Current.ResumePageId, Is.EqualTo(Root + ".serve"));
            }
            public void Finish(bool recommendation, bool advertisement, int before, int fee)
            {
                var done = P.Completion; done(); done();
                Assert.That(Flag(Completed), Is.True); Assert.That(Flag(Recommend), Is.EqualTo(recommendation));
                Assert.That(Flag(Advert), Is.EqualTo(advertisement)); Assert.That(D.Money, Is.EqualTo(before - fee));
                Assert.That(CompletedEvents.Count(id => id == Root), Is.EqualTo(1));
                Assert.That(C.State.GetNpcLocation(Character), Is.Empty);
                Assert.That(C.GetCommittedCafeVisits().Visits.Single(v => v.VisitKey == Visit).IsEnded, Is.True);
                Healthy();
            }
        }

        public static readonly string[] CaseNames =
        {
            "GeneratedContract", "ScheduleDay12", "ScheduleDay13", "ScheduleDay14", "MissingA04",
            "EspressoRecommendation", "RemakeThenRealSuccess", "FinalFailureAdvertisementYes", "FinalFailureAdvertisementNo",
            "InsufficientBalanceChoice", "BalanceDroppedBeforeTailRetry", "CommitBalanceRaceRetry",
            "NoticeRecommendation", "NoticeAdvertisement", "NoticeConflictAndNoRequest"
        };
        private static bool runningStandalone;
        public static string RunAll()
        {
            Assert.That(runningStandalone, Is.False, "RunAll must not run recursively.");
            runningStandalone = true;
            try
            {
                var report = new StringBuilder("Reporter acceptance: isolated A04-completed fixtures, actual CafeWorld cups/outbox, NOT a natural B/A04 chain.\n");
                var errors = new List<string>();
                foreach (string name in CaseNames)
                    try { new NeutralReporterAcceptanceTests().Acceptance(name); report.AppendLine("PASS " + name); }
                    catch (Exception error) { report.AppendLine("FAIL " + name + ": " + error.Message); errors.Add(name + ": " + error); }
                if (errors.Count > 0) throw new InvalidOperationException(report + "\n" + string.Join("\n\n", errors));
                return report.Append("PASS ").Append(CaseNames.Length).Append('/').Append(CaseNames.Length).ToString();
            }
            finally { runningStandalone = false; }
        }
        private static void ExpectTransactionFailure(Action complete)
        {
            var expected = new Regex(@"\[NCS\] Event neutral\.1018\.s1 result transaction failed:.*");
            if (!runningStandalone)
            {
                LogAssert.Expect(LogType.Error, expected);
                complete();
                return;
            }
            // unity_execute has no NUnit log scope. Observe and assert the exact engine error instead of suppressing it.
            int matching = 0;
            var unexpected = new List<string>();
            Application.LogCallback listener = (message, stack, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (type == LogType.Error && expected.IsMatch(message)) matching++;
                else unexpected.Add(type + ": " + message);
            };
            Application.logMessageReceived += listener;
            try { complete(); }
            finally { Application.logMessageReceived -= listener; }
            Assert.That(matching, Is.EqualTo(1), "The rejected result batch must emit exactly one matching transaction error.");
            Assert.That(unexpected, Is.Empty, string.Join("\n", unexpected));
        }
        [TestCaseSource(nameof(CaseNames))]
        public void Acceptance(string name)
        {
            switch (name)
            {
                case "GeneratedContract": Contract(); break;
                case "ScheduleDay12": ScheduleDay(12, true, false); break;
                case "ScheduleDay13": ScheduleDay(13, true, true); break;
                case "ScheduleDay14": ScheduleDay(14, true, false); break;
                case "MissingA04": ScheduleDay(13, false, false); break;
                case "EspressoRecommendation": Recommendation(false); break;
                case "RemakeThenRealSuccess": Recommendation(true); break;
                case "FinalFailureAdvertisementYes": Advertisement(true); break;
                case "FinalFailureAdvertisementNo": Advertisement(false); break;
                case "InsufficientBalanceChoice": Insufficient(); break;
                case "BalanceDroppedBeforeTailRetry": PaymentRetry(false); break;
                case "CommitBalanceRaceRetry": PaymentRetry(true); break;
                case "NoticeRecommendation": Notice(true); break;
                case "NoticeAdvertisement": Notice(false); break;
                case "NoticeConflictAndNoRequest": NoticeConflict(); break;
                default: Assert.Fail("Unknown case " + name); break;
            }
        }
        private static void Contract()
        {
            using (var f = new Fixture())
            {
                var visit = f.Config.cafeVisits.Single(v => v.visitKey == Visit);
                Assert.That(visit.characterId, Is.EqualTo(Character)); Assert.That(visit.orderProductId, Is.EqualTo("espresso"));
                Assert.That(visit.requireRealProduction, Is.True); Assert.That(visit.leaveAfterServed, Is.False);
                Assert.That(visit.arrivalDelaySeconds, Is.EqualTo(150));
                var hook = f.Config.cafeVisitHooks.Single(h => h.visitKey == Visit);
                Assert.That(hook.eventId, Is.EqualTo(Root)); Assert.That(hook.stage, Is.EqualTo("arrived")); Assert.That(hook.trigger, Is.EqualTo("auto"));
                var date = f.Config.eventDates.Single(d => d.eventId == Schedule);
                Assert.That(date.day, Is.EqualTo(13)); Assert.That(date.timeSlot, Is.EqualTo(NarrativeTimeSlot.Morning));
                var schedule = f.Config.events.Single(e => e.eventId == Schedule);
                Assert.That(schedule.entry, Is.EqualTo(NarrativeEntryPoint.MorningBeforeMap));
                Assert.That(NarrativePresenter.RequiresPresentation(schedule, null), Is.False, "Scheduling/control properties are not presentation payload.");
                Assert.That(LubanNarrativeLoader.IsResultOnlyEvent(f.Config, schedule), Is.True);
                Assert.That(f.Config.events.Single(e => e.eventId == Root).entry, Is.EqualTo(NarrativeEntryPoint.Immediate));
                var pages = f.Config.presentations.conversationPages.Where(p => p.presentationKey == Root + ".conversation").OrderBy(p => p.seq).ToArray();
                Assert.That(pages.Select(p => p.pageId.Substring(Root.Length + 1)), Is.EqualTo(new[] { "arrival", "wait_service", "serve", "taste_pass", "taste_fail", "identity", "recommend", "advert_offer", "advert_yes", "advert_no", "closing" }));
                Assert.That(pages[1].stepType, Is.EqualTo(NarrativeConversationStepType.WaitService));
                Assert.That(pages.Any(p => (p.pageCondKeys ?? "").Contains("dependency:")), Is.False);
                Assert.That(f.Config.abilityChecks.Any(c => c.checkId.StartsWith(Root, StringComparison.Ordinal)), Is.False);
                Assert.That(f.Config.conditions.Single(c => c.conditionId == Yes + ".balance").value, Is.EqualTo("100"));
                var payment = f.Config.results.Single(r => r.resultId == Root + ".result.payment");
                Assert.That(payment.resultType, Is.EqualTo(NarrativeResultType.MoneyRemove)); Assert.That(payment.value, Is.EqualTo("100"));
                Assert.That(f.Config.eventDates.Where(d => d.eventId == NoticeRecommendation || d.eventId == NoticeAdvertisement).All(d => d.day == 14), Is.True);
                Assert.That(LubanNarrativeLoader.ValidateConversationSteps(f.Config), Is.Empty);
            }
        }
        private static void ScheduleDay(int day, bool previous, bool expected)
        {
            using (var f = new Fixture(day, previous))
            {
                f.Morning(); Assert.That(f.C.GetCommittedCafeVisits().Visits.Any(v => v.VisitKey == Visit), Is.EqualTo(expected));
                Assert.That(f.CompletedEvents.Contains(Schedule), Is.EqualTo(expected)); Assert.That(f.P.Requests, Is.Empty);
                Assert.That(f.C.QueueEvent(Root), Is.False, "No manual bypass of exact service hook context.");
                f.Uncommitted(); f.Healthy();
            }
        }
        private static void Recommendation(bool remake)
        {
            using (var f = new Fixture())
            {
                f.Arrive(); var request = f.P.Current; var stale = f.P.Completion; f.WaitForCup();
                if (remake)
                {
                    Assert.That((int)Domain.Get(f.D.Product, "maxRemakes"), Is.GreaterThan(0), "Actual espresso must support a remake.");
                    f.Deliver(false); Assert.That(f.Receipt, Is.Null); f.Uncommitted();
                    Assert.That(f.C.ResumeServiceCheckpoint(), Is.False); Assert.That(f.P.Requests.Count, Is.EqualTo(1));
                }
                string cup = f.Deliver(true);
                Assert.That(f.Receipt, Is.Not.Null); Assert.That((bool)Domain.Get(f.Receipt, "satisfied"), Is.True);
                Assert.That((bool)Domain.Get(f.Receipt, "correctType"), Is.True); Assert.That(Domain.Get(f.Receipt, "cupId"), Is.EqualTo(cup));
                Assert.That(Domain.Get(f.Receipt, "orderId"), Is.EqualTo(request.CafeVisit.orderId));
                Assert.That(f.P.Current.RequestId, Is.EqualTo(request.RequestId)); Assert.That(f.P.Current.ResumePageId, Is.EqualTo(Root + ".serve"));
                Assert.That(f.C.ReadPresentationCondition("service:passed"), Is.True); stale(); f.Uncommitted();
                int money = f.D.Money; f.Walk(); Assert.That(f.Visible, Does.Contain(Root + ".recommend").And.Not.Contain(Root + ".advert_offer"));
                Assert.That(f.Offered, Is.Empty); f.Uncommitted(); f.Finish(true, false, money, 0); Assert.That(f.MoneyCommits, Is.Zero);
                var actual = f.Submitted.Single(x => x.stage == "service_completed");
                Assert.That(f.C.SubmitCafeVisitFeedback(actual), Is.True); Assert.That(f.D.Money, Is.EqualTo(money));
            }
        }
        private static void Advertisement(bool yes)
        {
            using (var f = new Fixture())
            {
                f.Arrive(); f.WaitForCup(); f.FinalFailure(); f.AdChoice = yes ? Yes : No; int money = f.D.Money;
                Assert.That(f.C.ReadPresentationCondition("not:service:passed"), Is.True);
                f.Walk(); Assert.That(f.Visible, Does.Contain(Root + ".taste_fail").And.Contain(Root + (yes ? ".advert_yes" : ".advert_no")));
                Assert.That(f.Visible, Does.Not.Contain(Root + ".recommend")); f.Uncommitted();
                f.Finish(false, yes, money, yes ? 100 : 0); Assert.That(f.MoneyCommits, Is.EqualTo(yes ? 1 : 0));
            }
        }
        private static void Insufficient()
        {
            using (var f = new Fixture())
            {
                f.Arrive(); f.WaitForCup(); f.FinalFailure(); f.D.Balance(99);
                Assert.That(f.C.EvaluateConditions("choice", Yes), Is.False); Assert.That(f.C.SelectChoice(Yes), Is.False);
                Assert.That(f.C.SelectedChoices, Does.Not.Contain(Yes)); f.AdChoice = No; f.Walk(); f.Finish(false, false, 99, 0);
            }
        }
        private static void PaymentRetry(bool race)
        {
            using (var f = new Fixture())
            {
                f.Arrive(); f.WaitForCup(); f.FinalFailure(); f.D.Balance(250); f.AdChoice = Yes; f.Walk(); f.Uncommitted();
                if (race) f.MutateAtCommit = true; else f.D.Balance(99);
                var old = f.P.Completion;
                ExpectTransactionFailure(old);
                Assert.That(f.Failures.Count, Is.EqualTo(1)); f.Uncommitted();
                Assert.That(f.C.State.GetNpcLocation(Character), Is.EqualTo("cafe"), "Preflight/commit rejection must not partially despawn.");
                Assert.That(f.D.Money, Is.EqualTo(race ? 249 : 99));
                // Immediate-root legacy failure keeps its inspection context. Cancel/retry explicitly; don't fabricate another service.
                f.P.Cancel(); f.Failures.Clear(); f.D.Balance(250);
                Assert.That(f.C.ResumeServiceCheckpoint(), Is.True); old(); f.Uncommitted();
                f.Walk(); f.Finish(false, true, 250, 100);
            }
        }
        private static void Notice(bool recommendation)
        {
            using (var f = new Fixture(14))
            {
                f.C.State.SetCondition(recommendation ? Recommend : Advert, true); f.Morning();
                Assert.That(f.P.Current.Event.eventId, Is.EqualTo(recommendation ? NoticeRecommendation : NoticeAdvertisement));
                Assert.That(f.P.Requests.Count, Is.EqualTo(1)); Assert.That(f.Flag(Printed), Is.False);
                var done = f.P.Completion; done(); done(); Assert.That(f.Flag(Printed), Is.True);
                f.Morning(); Assert.That(f.P.Requests.Count, Is.EqualTo(1)); Assert.That(f.CompletedEvents.Count, Is.EqualTo(1)); f.Healthy();
            }
        }
        private static void NoticeConflict()
        {
            using (var f = new Fixture(14)) { f.Morning(); Assert.That(f.P.Requests, Is.Empty); Assert.That(f.Flag(Printed), Is.False); }
            using (var f = new Fixture(14))
            {
                f.C.State.SetCondition(Recommend, true); f.C.State.SetCondition(Advert, true); f.Morning();
                Assert.That(f.P.Requests, Is.Empty, "Conflicting request flags must not produce two articles."); Assert.That(f.Flag(Printed), Is.False);
            }
            using (var f = new Fixture(14))
            {
                f.C.State.SetCondition(Recommend, true); f.C.State.SetCondition(Printed, true); f.Morning();
                Assert.That(f.P.Requests, Is.Empty, "Printed is the durable dedupe marker.");
            }
        }
    }
}
