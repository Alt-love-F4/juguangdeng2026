#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    /// <summary>
    /// Formal Luban data -> daily.action -> presenter -> existing panel bridges.
    /// Day 26, encounters, route prerequisites and visit history are explicitly seeded below.
    /// This is a panel/action regression fixture, NOT a natural new-game playthrough.
    /// </summary>
    public sealed class NarrativeMapDailyPanelTests
    {
        private const string Tag = "map-daily-panel-test";
        private const int Day = 26;
        private static readonly string[] Locations =
        {
            "旧城区", "圣徒街", "圣西尔万郊区", "market", "圣地大教堂",
            "library", "市政厅", "首都法院", "花园区"
        };
        private static readonly string[] Attributes = { "language", "courage", "wisdom", "perception" };
        private Scene previous, shared;
        private float oldTimeScale;
        private NarrativeConfiguration config;
        private NarrativeController controller;
        private NarrativeActionController actions;
        private NarrativePresenter presenter;
        private NarrativeConversationPresentationData current;
        private Action lateComplete;
        private int readingCalls, noticeCalls;
        private readonly List<string> completedEvents = new List<string>();
        private readonly List<string> failures = new List<string>();

        [UnitySetUp]
        public IEnumerator Setup()
        {
            current = null; lateComplete = null; readingCalls = noticeCalls = 0;
            completedEvents.Clear(); failures.Clear();
            oldTimeScale = Time.timeScale;
            previous = SceneManager.GetActiveScene();
            shared = EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            SceneManager.SetActiveScene(shared);

            config = LubanNarrativeLoader.Load();
            presenter = new NarrativePresenter(config, Resolve);
            controller = new NarrativeController(config, presenter);
            controller.SetEndingChecksEnabled(false);
            controller.BeginValueRestore();
            actions = new NarrativeActionController(controller, config.actions);
            controller.BindValueProvider(new DelegateNarrativeValueProvider("dayflow",
                key => key == "day" ? Day : (int?)null));
            controller.BindValueProvider(new DelegateNarrativeValueProvider("cafe",
                key => key == "money" ? 500 : (int?)null));
            // Read the real action pool, not a constant AP stand-in.
            controller.BindValueProvider(new DelegateNarrativeValueProvider("actions",
                key => key == "current" ? actions.ActionPoints.Current
                    : key == "max" ? actions.ActionPoints.Max : (int?)null));
            foreach (string attribute in Attributes)
            {
                controller.SetBar("player." + attribute, 5);
                controller.SetBar("training." + attribute, 9);
            }
            // These are actual authored prerequisites, not blanket construction/integration unlocks.
            foreach (string fact in new[]
            {
                "world.map.active", "known.adele", "known.sparrow", "known.colette", "known.eugene",
                "known.sylvain", "known.pascal", "known.sade", "known.sorel", "known.lefebvre",
                "story.neutral.1008.s1.completed", "story.church.1009.s2.completed",
                "story.rose.1011.s1.completed", "story.revolution.1014.s1.completed"
            }) controller.State.SetCondition(fact, true);
            controller.State.SetCondition("church.detention.active", false);
            // Deliberately skip introductions so every location tests its ORDINARY daily root.
            // Marking visit history here is fixture seeding, not evidence of playing those visits.
            foreach (var visit in config.events.Where(e => Property(e, "dailyKind") == "visit"
                && (Property(e, "dailyPool") ?? "").StartsWith("daily.action.", StringComparison.Ordinal)))
                controller.State.MarkEventHappened(visit.eventId);
            controller.SetOfficialDay(Day);
            actions.RefreshForDay(Day); // Restored day, not scheduling/playing 26 days of story.
            controller.CompleteValueRestore(false);
            controller.AbilityReader = id => controller.State.TryGetBar("player." + id, out int value)
                ? (true, value) : (false, 0);
            presenter.PageCondition = controller.ReadPresentationCondition;
            presenter.ChoiceUnlocked = (id, context) => controller.EvaluateConditions("choice", id, context);
            presenter.InlineAbilityCheck = controller.EvaluateInlineAbilityCheckForPresentation;
            presenter.ChoiceSelected += controller.SelectChoice;
            presenter.ConversationPlayback = (request, data, done) =>
            {
                current = data; lateComplete = done;
                Assert.That(request.Event.blocksSave, Is.True, data.eventId);
                return NarrativeConversationEventPlayback.Play(request, data, done, Tag, PanelCachePolicy.Transient);
            };
            presenter.ReadingPlayback = (request, title, pages, image, dialogue, speaker, portrait, done) =>
            {
                readingCalls++;
                return ReadingEventPlayback.Play(request, title, pages, image, dialogue, speaker, portrait, done,
                    Tag, PanelCachePolicy.Transient);
            };
            presenter.NoticePlayback = (request, data, done) =>
            {
                noticeCalls++;
                return NarrativeNoticeEventPlayback.Play(request, data, done, Tag, PanelCachePolicy.Transient);
            };
            controller.EventCompleted += completedEvents.Add;
            controller.EventFailed += (id, reason) => failures.Add(id + ":" + reason);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            presenter?.InterruptForHost();
            if (presenter != null && controller != null) presenter.ChoiceSelected -= controller.SelectChoice;
            presenter?.Dispose();
            UIKit.ClosePanelsByTag(Tag);
            controller?.UnbindValueProviders();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (shared.IsValid() && shared.isLoaded) yield return SceneManager.UnloadSceneAsync(shared);
            Time.timeScale = oldTimeScale;
        }

        [UnityTest]
        public IEnumerator NineLocationsShowOrdinaryDialogueAndHostInterruptionRefundsWithoutGrowth()
        {
            // Also guard the complete generated migration, including preserved reader/notice classes.
            var roots = config.events.Where(e => (Property(e, "dailyPool") ?? "")
                .StartsWith("daily.action.", StringComparison.Ordinal)).ToArray();
            Assert.That(roots.Length, Is.EqualTo(240));
            foreach (var root in roots) Assert.That(root.presentationClass, Is.EqualTo("Conversation").IgnoreCase, root.eventId);
            foreach (string prefix in new[] { "news.bulletin.", "newspaper." })
            {
                var preserved = config.events.Where(e => e.eventId.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                Assert.That(preserved, Is.Not.Empty, prefix);
                foreach (var root in preserved) Assert.That(root.presentationClass,
                    Is.EqualTo(prefix == "newspaper." ? "Reading" : "Notice").IgnoreCase, root.eventId);
            }

            foreach (string location in Locations)
            {
                string key = "daily.action." + location;
                var action = actions.Get(key);
                Assert.That(action, Is.Not.Null, key);
                Assert.That(controller.TrySelectActionEvents(action, out var selected), Is.True, key);
                Assert.That(selected.Count, Is.EqualTo(1), "Visits are seeded; exercise ordinary dialogue: " + key);
                string eventId = selected[0];
                Assert.That(Property(config.events.Single(e => e.eventId == eventId), "dailyKind"), Is.EqualTo("ordinary"), key);
                Assert.That(controller.State.HasHappenedEvent(eventId), Is.False, key);
                int ap = actions.ActionPoints.Current;
                int playcount = controller.State.GetCounter("daily.playcount." + eventId);
                var growth = Attributes.SelectMany(a => new[] { "player." + a, "training." + a })
                    .ToDictionary(bar => bar, ReadBar);
                int callbacks = 0;
                bool? success = null;
                current = null; lateComplete = null;
                Assert.That(actions.Execute(key, result => { callbacks++; success = result; }), Is.True, key);
                yield return WaitFor(() => current != null && DialoguePanel.Active != null
                    && DialoguePanel.Active.IsPlaying && DialoguePanel.Active.IsReady, "DialoguePanel: " + key);
                var data = current;
                var panel = DialoguePanel.Active;
                Assert.That(data.eventId, Is.EqualTo(eventId));
                Assert.That(panel.Tag, Is.EqualTo(Tag));
                Assert.That(panel.AllowCancel, Is.False, "blocksSave daily must be interrupted by its host, not ESC.");
                Assert.That(readingCalls, Is.Zero, key);
                Assert.That(noticeCalls, Is.Zero, key);
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(ap - 1), "AP is reserved until success/cancellation.");
                Assert.That(data.lines, Is.Not.Empty, key);
                foreach (var line in data.lines)
                {
                    var source = config.presentations.conversationLines.Single(l => l.lineId == line.lineId);
                    Assert.That(line.speakerKey ?? "", Is.EqualTo(source.speakerKey ?? ""), line.lineId);
                    Assert.That(line.text, Is.EqualTo(Resolve(source.textKey)).And.Not.Empty, line.lineId);
                    if (!string.IsNullOrEmpty(source.speakerNameKey))
                        Assert.That(line.speakerName, Is.EqualTo(Resolve(source.speakerNameKey)), line.lineId);
                }
                int spoken = data.lines.FindIndex(line => !string.IsNullOrWhiteSpace(line.speakerKey));
                Assert.That(spoken, Is.GreaterThanOrEqualTo(0), "Authored encounter must retain a speaker: " + key);
                float deadline = Time.realtimeSinceStartup + 10f;
                while (panel.LineIndex < spoken && Time.realtimeSinceStartup < deadline)
                {
                    panel.Advance();
                    yield return null;
                }
                Assert.That(panel.LineIndex, Is.EqualTo(spoken), key);
                yield return WaitFor(() => panel.IsReady, "Spoken line: " + key);
                var texts = panel.GetComponentsInChildren<TMP_Text>(true).Where(t => t.gameObject.activeInHierarchy).ToArray();
                Assert.That(texts.Any(t => t.text == data.lines[spoken].text), Is.True, "Real panel body: " + key);
                Assert.That(data.lines[spoken].speakerName, Is.Not.Null.And.Not.Empty, key);
                Assert.That(texts.Any(t => t.text == data.lines[spoken].speakerName), Is.True, "Real panel speaker: " + key);

                // No fake done()/Cancel()/ESC: exercise the presenter's actual host failure contract.
                Action stale = lateComplete;
                presenter.InterruptForHost();
                yield return WaitFor(() => !actions.IsExecuting && !presenter.IsPresenting, "Host interruption: " + key);
                Assert.That(callbacks, Is.EqualTo(1), key);
                Assert.That(success, Is.False, key);
                Assert.That(failures.Last(), Is.EqualTo(eventId + ":presentation_cancelled: host_unavailable"));
                Assert.That(panel == null || !panel.IsPlaying, Is.True, key);
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(ap), key);
                foreach (var bar in growth) Assert.That(ReadBar(bar.Key), Is.EqualTo(bar.Value), key + ":" + bar.Key);
                Assert.That(actions.HasExecutedToday(key), Is.False, key);
                Assert.That(controller.State.HasHappenedEvent(eventId), Is.False, key);
                Assert.That(controller.State.GetCounter("daily.playcount." + eventId), Is.EqualTo(playcount), key);
                Assert.That(completedEvents, Is.Empty, key);
                stale?.Invoke(); // Late UI acknowledgement must not resurrect a cancelled action.
                yield return null;
                Assert.That(callbacks, Is.EqualTo(1), key);
                Assert.That(actions.ActionPoints.Current, Is.EqualTo(ap), key);
                Assert.That(completedEvents, Is.Empty, key);
            }
            Assert.That(failures.Count, Is.EqualTo(Locations.Length));
        }

        [UnityTest]
        public IEnumerator NineFirstVisitsAutomaticallyContinueToTheirDailyBodyForOnePoint()
        {
            // Undo only this fixture's seeded visit history. No formal save/runtime is touched.
            var history = controller.State.Capture();
            var visits = config.events.Where(e => Property(e, "dailyKind") == "visit").Select(e => e.eventId).ToArray();
            history.happenedEvents.RemoveAll(id => visits.Contains(id));
            controller.State.Restore(history);
            foreach (string location in Locations)
            {
                actions.ActionPoints.Set(1);
                string key = "daily.action." + location;
                Assert.That(controller.TrySelectActionEvents(actions.Get(key), out var expected), Is.True, key);
                Assert.That(expected.Count, Is.EqualTo(2), key);
                int callbacks = 0; bool? success = null;
                int completedBefore = completedEvents.Count;
                var seen = new HashSet<string>();
                Assert.That(actions.Execute(key, value => { callbacks++; success = value; }), Is.True, key);
                Assert.That(actions.Execute(key), Is.False, "Repeated click cannot buy a second action while presenting.");
                float deadline = Time.realtimeSinceStartup + 15f;
                while (actions.IsExecuting && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(actions.ActionPoints.Current, Is.Zero, "No extra charge/refund between first visit and body: " + key);
                    Assert.That(callbacks, Is.Zero, "The map callback must wait for BOTH roots: " + key);
                    var panel = DialoguePanel.Active;
                    if (panel != null && panel.IsPlaying && panel.Tag == Tag && panel.IsReady)
                    {
                        if (panel.CurrentSource != null) seen.Add(panel.CurrentSource.EventId);
                        Assert.That(panel.IsChoosing, Is.False, "First eligible ordinary fixture is not a choice: " + key);
                        panel.Advance();
                    }
                    yield return null;
                }
                Assert.That(actions.IsExecuting, Is.False, "Full visit/body sequence timed out: " + key);
                Assert.That(success, Is.True, key); Assert.That(callbacks, Is.EqualTo(1), key);
                Assert.That(actions.ActionPoints.Current, Is.Zero, key);
                CollectionAssert.AreEquivalent(expected, seen, key);
                CollectionAssert.AreEqual(expected, completedEvents.Skip(completedBefore).ToArray(), key);
                Assert.That(actions.HasExecutedToday(key), Is.True);
                Assert.That(failures, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator CityHallChoiceAndItsResponseFinishInOnePurchase()
        {
            controller.State.SetCondition("story.daily.civ.05.completed", true);
            var city = actions.Get("daily.action.市政厅");
            // All ordinary variants except CIV-08 have been seen, so the real least-played
            // selector chooses the authored choice root. Explicit test history, not natural progress.
            foreach (string id in city.resultEvents.Where(id => id != "daily.civ.08"))
                controller.State.SetCounter("daily.playcount." + id, 1);
            Assert.That(controller.TrySelectActionEvents(city, out var selected), Is.True);
            Assert.That(selected, Is.EqualTo(new[] { "daily.civ.08" }));
            actions.ActionPoints.Set(1);
            int callbacks = 0; bool? success = null;
            Assert.That(actions.Execute(city.actionKey, value => { callbacks++; success = value; }), Is.True);
            var seen = new HashSet<string>(); bool chosen = false;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (actions.IsExecuting && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(actions.ActionPoints.Current, Is.Zero);
                var panel = DialoguePanel.Active;
                if (panel != null && panel.IsPlaying && panel.Tag == Tag && panel.IsReady)
                {
                    if (panel.CurrentSource != null) seen.Add(panel.CurrentSource.PageId);
                    if (panel.IsChoosing)
                    {
                        Assert.That(chosen, Is.False);
                        Assert.That(panel.SelectOption("daily.civ.08.choice.a"), Is.True);
                        chosen = true;
                    }
                    else panel.Advance();
                }
                yield return null;
            }
            Assert.That(chosen, Is.True);
            Assert.That(success, Is.True); Assert.That(callbacks, Is.EqualTo(1));
            CollectionAssert.AreEquivalent(new[] { "daily.civ.08.opening", "daily.civ.08.response.a" }, seen);
            Assert.That(controller.State.GetCounter("daily.playcount.daily.civ.08"), Is.EqualTo(1));
            Assert.That(controller.State.HasHappenedEvent("daily.civ.08"), Is.True);
            Assert.That(actions.ActionPoints.Current, Is.Zero);
            Assert.That(failures, Is.Empty);
        }

        private string Resolve(string key) => config.texts.Find(t => t.key == key)?.content ?? key;
        private int ReadBar(string key)
        {
            Assert.That(controller.State.TryGetBar(key, out int value), Is.True, key);
            return value;
        }
        private static string Property(NarrativeEventDefinition definition, string name)
            => definition.eventProperties?.FirstOrDefault(p => p != null && p.property == name)?.value;
        private static IEnumerator WaitFor(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, message);
        }
    }
}
#endif
