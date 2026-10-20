using System;
using System.Collections;
using System.Collections.Generic;

namespace Spotlight.Narrative
{
    /// <summary>
    /// No UI, synthetic input, global runtime or state shortcuts. The owner calls Tick on the Unity
    /// thread; roots, atoms, yielded pages and completions consume work budget. Hidden-page scans
    /// inside one graph step are bounded by the production cursor's own traversal limit.
    /// Controller alone owns selection validation, check evaluation and result transactions.
    /// </summary>
    public sealed class NarrativeAutomationPresenter : INarrativePresenter, INarrativePresentationDrain,
        INarrativePresentationCancellation, INarrativeGameplayResultSource, IDisposable
    {
        private readonly NarrativeConfiguration configuration;
        private readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
        private readonly Queue<NarrativeSetupParamsDefinition> effects = new Queue<NarrativeSetupParamsDefinition>();
        private readonly Queue<string> log = new Queue<string>();
        private NarrativeController controller;
        private NarrativeEventRequest request;
        private Action completion;
        private bool draining, disposed, ticking, started;
        private long epoch;
        private NarrativeAutomationGameplayRequest gameplay;
        private NarrativeGameplayResult gameplayResult;
        private bool gameplayReceived;

        public NarrativeAutomationPresenter(NarrativeConfiguration configuration, NarrativeAutomationPlan plan = null)
        { this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); Plan = plan ?? new NarrativeAutomationPlan(); }
        public NarrativeAutomationPlan Plan { get; }
        public Func<NarrativeAutomationChoiceContext, string> ChoiceSelector { get; set; }
        public bool IsPresenting => completion != null || draining || effects.Count > 0;
        public bool IsAwaitingGameplay => gameplay != null;
        public NarrativeAutomationPageState CurrentPage { get; private set; }
        public string LastError { get; private set; }
        public IReadOnlyList<string> Log => log.ToArray();
        public int LogCapacity { get; set; } = 2048;
        public event Action<string> LogAdded;
        public event Action<NarrativeAutomationPageState> PageChanged;
        public event Action<NarrativeEventRequest> Started;
        public event Action<NarrativeEventRequest> Finished;
        public event Action<string> Cancelled;
        public event Action<string, bool> GameplayCompleted;
        public event Action<NarrativeAutomationGameplayRequest> GameplayRequested;

        /// <summary>Bind after constructing the controller with this presenter. Also wires effect drain.</summary>
        public void Bind(NarrativeController value)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NarrativeAutomationPresenter));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(controller, value)) return;
            if (controller != null || IsPresenting) throw new InvalidOperationException("Bind once before playback.");
            controller = value;
            controller.EffectRequested += ExecuteEffect;
        }

        public void Play(NarrativeEventRequest incoming, Action completed)
        {
            RequireIdle();
            if (incoming.Event == null) throw new ArgumentException("A narrative event is required.", nameof(incoming));
            request = incoming; completion = completed ?? throw new ArgumentNullException(nameof(completed));
            epoch++; started = false; LastError = null; CurrentPage = null;
            work.Push(Sequence(incoming.Event));
        }

        public void Drain(Action completed)
        {
            RequireIdle();
            draining = true; completion = completed ?? throw new ArgumentNullException(nameof(completed));
            started = false; epoch++;
            work.Push(DrainEffects());
        }

        private void RequireIdle()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NarrativeAutomationPresenter));
            if (controller == null) throw new InvalidOperationException("Bind the production controller first.");
            if (completion != null || draining) throw new InvalidOperationException("Only one presentation or drain may be active.");
        }

        /// <returns>Consumed work units, never more than budget. Waiting for the host consumes none.</returns>
        public int Tick(int budget = 64)
        {
            if (budget < 1) throw new ArgumentOutOfRangeException(nameof(budget));
            if (disposed || ticking) return 0;
            ticking = true;
            int used = 0;
            try
            {
                while (used < budget && completion != null)
                {
                    if (gameplay != null && !gameplayReceived) break;
                    used++;
                    if (!started)
                    {
                        started = true;
                        if (!draining) { AddLog("start " + request.Event.eventId); Started?.Invoke(request); }
                        continue;
                    }
                    if (work.Count == 0)
                    {
                        var callback = completion;
                        var finished = request;
                        bool wasDrain = draining;
                        completion = null; draining = false; started = false;
                        if (!wasDrain) { AddLog("finish " + finished.Event.eventId); Finished?.Invoke(finished); }
                        callback(); // Any following Play/Drain only enqueues; it cannot recurse into Tick.
                        continue;
                    }
                    var iterator = work.Peek();
                    long version = epoch;
                    bool moved = iterator.MoveNext();
                    if (version != epoch || completion == null) continue;
                    if (!moved) { work.Pop(); (iterator as IDisposable)?.Dispose(); }
                    else if (iterator.Current is IEnumerator nested) work.Push(nested);
                }
            }
            catch (Exception error)
            {
                LastError = error.Message;
                AddLog("error " + error.Message);
                Cancel("automation_error");
            }
            finally { ticking = false; }
            return used;
        }

        private IEnumerator Sequence(NarrativeEventDefinition definition)
        {
            var setup = configuration.setups.Find(x => x != null && x.setupKey == definition.setupKey);
            var presentation = configuration.presentations.index.Find(x => x != null && x.presentationKey == definition.presentationKey);
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) && presentation == null)
                throw new InvalidOperationException("Unknown presentationKey: " + definition.presentationKey);
            string kind = presentation?.presentationClass ?? definition.presentationClass;
            string templateName = setup?.tplName ?? definition.setupType;
            if (string.IsNullOrWhiteSpace(templateName)) templateName = DefaultTemplate(kind);
            var template = configuration.templates.Find(x => x != null && x.tplName == templateName);
            var slots = PresentationSlots.Parse(setup);
            PresentationSlots.ApplyEventProperties(slots, definition);
            if (!string.IsNullOrWhiteSpace(setup?.checkId))
            {
                var check = controller.EvaluateAbilityCheckForPresentation(setup.checkId);
                AddLog("check " + setup.checkId + " passed=" + check.passed);
                yield return null;
                if (slots.TryGetValue("checkDisplay", out string display) && display == "passive"
                    && string.IsNullOrWhiteSpace(definition.presentationKey) && (definition.contentKeys == null || definition.contentKeys.Count == 0)) yield break;
            }
            var groups = new HashSet<string>(StringComparer.Ordinal);
            var atoms = PresentationSlots.Sequence(template?.atomSeq);
            if (atoms.Length == 0) atoms = new[] { "A_NARRATE" };
            foreach (string atom in atoms)
            {
                yield return null;
                string name = atom.Trim().ToUpperInvariant();
                if (name == "A_DIALOG" || name == "A_NARRATE")
                {
                    if (!string.IsNullOrWhiteSpace(definition.presentationKey) && string.Equals(kind, "conversation", StringComparison.OrdinalIgnoreCase))
                    {
                        bool reachedEnd = false;
                        yield return Conversation(definition, groups, () => reachedEnd = true);
                        if (reachedEnd) yield break; // Production End terminates trailing atoms too.
                    }
                    else if (!string.IsNullOrWhiteSpace(definition.presentationKey) && string.Equals(kind, "reading", StringComparison.OrdinalIgnoreCase)
                        && configuration.presentations.readingPages.Exists(x => x != null && x.presentationKey == definition.presentationKey))
                        yield return Reading(definition.presentationKey);
                    else if (!string.IsNullOrWhiteSpace(definition.presentationKey) && string.Equals(kind, "notice", StringComparison.OrdinalIgnoreCase))
                    {
                        var notice = configuration.presentations.notices.Find(x => x != null && x.presentationKey == definition.presentationKey);
                        if (notice == null) throw new InvalidOperationException("Missing notice: " + definition.presentationKey);
                        PublishPage(definition.presentationKey, definition.presentationKey, "Notice", true, null, new[] { notice.titleKey, notice.timeKey, notice.textKey });
                    }
                    else
                    {
                        string lines = slots.TryGetValue("lines", out string value) ? value : slots.TryGetValue("textKey", out value) ? value : null;
                        var keys = string.IsNullOrWhiteSpace(lines) ? definition.contentKeys : new List<string>(lines.Split('|'));
                        if (keys != null) foreach (string key in keys)
                        { PublishPage(definition.presentationKey, key, kind ?? "Text", true, null, new[] { key }); yield return null; }
                    }
                }
                else if (name == "A_CHOICE" && slots.TryGetValue("opts", out string group) && !string.IsNullOrWhiteSpace(group) && !groups.Contains(group))
                { Select(group); groups.Add(group); }
                // Timed, audio and other visual-only atoms complete without any UI or input synthesis.
                else if (!Enum.TryParse(name, out NarrativeAtomType _)) throw new InvalidOperationException("Unsupported atom: " + atom);
            }
        }

        private IEnumerator Conversation(NarrativeEventDefinition definition, HashSet<string> groups, Action end)
        {
            var pages = configuration.presentations.conversationPages.FindAll(x => x != null && x.presentationKey == definition.presentationKey);
            pages.Sort((a, b) => a.seq.CompareTo(b.seq));
            if (pages.Count == 0) throw new InvalidOperationException("Missing conversation pages: " + definition.presentationKey);
            var cursor = new NarrativeConversationTraversal(pages);
            // The production cursor owns hidden-page linear fallback, explicit edges and revisit limits.
            while (cursor.MoveNext(p => PageVisible(p.pageId, p.pageCondKeys, p.pageBarConds)))
            {
                var page = cursor.Current;
                var keys = configuration.presentations.conversationLines.FindAll(x => x != null && x.pageId == page.pageId);
                keys.Sort((a, b) => a.seq.CompareTo(b.seq));
                PublishPage(definition.presentationKey, page.pageId, page.stepType.ToString(), true, page.optionGroupKey, keys.ConvertAll(x => x.textKey));
                if (page.stepType == NarrativeConversationStepType.End) { end(); yield break; }
                if (page.stepType == NarrativeConversationStepType.PassiveCheck)
                {
                    var check = controller.EvaluateInlineAbilityCheckForPresentation(page.checkId);
                    AddLog("inline " + page.checkId + " passed=" + check.passed);
                    if (!check.passed) { cursor.ResumeLinearAfterCurrent(); yield return null; continue; }
                }
                if (page.stepType == NarrativeConversationStepType.Gameplay) yield return Gameplay(page);
                else if (page.stepType == NarrativeConversationStepType.LetterRead || page.stepType == NarrativeConversationStepType.LetterReveal)
                {
                    var reading = configuration.presentations.readingPages.FindAll(x => x != null && x.presentationKey == page.readingKey);
                    if (reading.Count != 1) throw new InvalidOperationException("Letter step requires exactly one reading page: " + page.readingKey);
                    if (!PageVisible(reading[0].pageId, reading[0].pageCondKeys, reading[0].pageBarConds))
                        throw new InvalidOperationException("Letter reading page is unavailable: " + reading[0].pageId);
                    yield return Reading(page.readingKey);
                }
                else if (!string.IsNullOrWhiteSpace(page.optionGroupKey))
                { Select(page.optionGroupKey); groups.Add(page.optionGroupKey); }
                yield return null;
            }
        }

        private IEnumerator Reading(string key)
        {
            var pages = configuration.presentations.readingPages.FindAll(x => x != null && x.presentationKey == key);
            pages.Sort((a, b) => a.seq.CompareTo(b.seq));
            foreach (var page in pages)
            {
                bool visible = PageVisible(page.pageId, page.pageCondKeys, page.pageBarConds);
                var lines = configuration.presentations.readingLines.FindAll(x => x != null && x.pageId == page.pageId);
                lines.Sort((a, b) => a.seq.CompareTo(b.seq));
                PublishPage(key, page.pageId, "Reading", visible, null, lines.ConvertAll(x => x.textKey));
                yield return null;
            }
        }

        private IEnumerator Gameplay(NarrativeConversationPageDefinition page)
        {
            if (string.IsNullOrWhiteSpace(page.gameplayKey)) throw new InvalidOperationException("Gameplay key is missing: " + page.pageId);
            long version = epoch;
            gameplayReceived = false; gameplayResult = null;
            gameplay = new NarrativeAutomationGameplayRequest(request, new NarrativeGameplayPresentationData
            { eventId = request.Event.eventId, pageId = page.pageId, gameplayKey = page.gameplayKey }, result =>
            {
                if (disposed || epoch != version || gameplay == null) return;
                gameplayResult = result == null ? null : new NarrativeGameplayResult { passed = result.passed, cancelled = result.cancelled };
                gameplayReceived = true;
            });
            CurrentPage.AwaitingGameplay = true;
            AddLog("gameplay request " + page.pageId + " key=" + page.gameplayKey);
            GameplayRequested?.Invoke(gameplay); // A missing host intentionally leaves the real root waiting.
            yield return null;
            if (gameplayResult == null) throw new InvalidOperationException("Gameplay returned a null result: " + page.pageId);
            var resultValue = gameplayResult;
            gameplay = null; gameplayReceived = false; gameplayResult = null;
            CurrentPage.AwaitingGameplay = false;
            if (resultValue.cancelled) { Cancel("gameplay_cancelled"); yield break; }
            GameplayCompleted?.Invoke(page.pageId, resultValue.passed);
            AddLog("gameplay completed " + page.pageId + " passed=" + resultValue.passed);
        }

        private void Select(string group)
        {
            var all = configuration.choices.FindAll(x => x != null && x.groupKey == group);
            all.Sort((a, b) => { int order = a.seq.CompareTo(b.seq); return order != 0 ? order : string.CompareOrdinal(a.choiceId, b.choiceId); });
            foreach (var choice in all)
                if (Contains(controller.SelectedChoices, choice.choiceId)) { AddLog("restored choice " + choice.choiceId); return; }
            var choices = all.FindAll(x => controller.EvaluateConditions("choice", x.choiceId, request.Context));
            if (choices.Count == 0) throw new InvalidOperationException("No unlocked choices: " + group);
            var context = new NarrativeAutomationChoiceContext { Controller = controller, Request = request,
                PageId = CurrentPage?.PageId, GroupKey = group, Choices = choices, Scores = Plan.ScoreChoices(configuration, controller, choices) };
            string id = ChoiceSelector?.Invoke(context) ?? Plan.Select(context);
            if (!choices.Exists(x => x.choiceId == id) || !controller.SelectChoice(id))
                throw new InvalidOperationException("Choice policy returned an unavailable choice: " + id + " in " + group);
            if (CurrentPage != null) CurrentPage.SelectedChoiceId = id;
            AddLog("choice " + group + " -> " + id);
        }

        private bool PageVisible(string id, string keys, string bars)
        {
            // Production rejects this legacy field; numeric conditions are bar:x:ge:n guards in pageCondKeys.
            if (!string.IsNullOrWhiteSpace(bars)) throw new InvalidOperationException("Unsupported presentation page bar conditions: " + id);
            if (!string.IsNullOrWhiteSpace(keys)) foreach (string key in keys.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (!controller.ReadPageCondition(key.Trim())) return false;
            return true;
        }
        private void PublishPage(string presentation, string id, string kind, bool visible, string group, IReadOnlyList<string> keys)
        {
            CurrentPage = new NarrativeAutomationPageState { EventId = request.Event?.eventId, PresentationKey = presentation,
                PageId = id, Kind = kind, Visible = visible, ChoiceGroup = group, TextKeys = keys };
            AddLog("page " + id + " " + kind + " visible=" + visible);
            PageChanged?.Invoke(CurrentPage);
        }
        private void ExecuteEffect(NarrativeEffectNotification notification)
        {
            if (notification?.Result == null) return;
            AddLog("effect " + notification.Result.resultType + " " + notification.Target);
            if (notification.Result.resultType != NarrativeResultType.PlaySetup) return;
            var setup = configuration.setups.Find(x => x != null && x.setupKey == notification.Target);
            if (setup == null) throw new InvalidOperationException("Missing setup: " + notification.Target);
            var template = configuration.templates.Find(x => x != null && x.tplName == setup.tplName);
            foreach (string atom in PresentationSlots.Sequence(template?.atomSeq))
                if (string.Equals(atom, "A_CHOICE", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("PLAY_SETUP cannot contain choices: " + setup.setupKey);
            effects.Enqueue(setup);
        }
        private IEnumerator DrainEffects()
        {
            while (effects.Count > 0)
            {
                var setup = effects.Dequeue();
                yield return Sequence(new NarrativeEventDefinition { eventId = "setup:" + setup.setupKey, setupKey = setup.setupKey });
            }
        }
        private static bool Contains(IReadOnlyList<string> list, string id)
        { if (list != null) for (int i = 0; i < list.Count; i++) if (list[i] == id) return true; return false; }
        private static string DefaultTemplate(string kind)
        {
            switch ((kind ?? "").Trim().ToLowerInvariant())
            { case "conversation": return "Tpl_TwoPersonTalk"; case "reading": return "Tpl_Narration";
                case "notice": return "Tpl_WorldBroadcast"; case "cutscene": return "Tpl_CutScene"; default: return null; }
        }
        private void AddLog(string value)
        {
            log.Enqueue(value);
            while (log.Count > Math.Max(1, LogCapacity)) log.Dequeue();
            LogAdded?.Invoke(value);
        }
        public void Cancel(string reason = "playback_cancelled")
        {
            epoch++;
            completion = null; draining = false; started = false;
            gameplay = null; gameplayResult = null; gameplayReceived = false;
            work.Clear(); effects.Clear();
            if (CurrentPage != null) CurrentPage.AwaitingGameplay = false;
            Cancelled?.Invoke(reason);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (controller != null) controller.EffectRequested -= ExecuteEffect;
            Cancel("disposed");
        }
    }
}
