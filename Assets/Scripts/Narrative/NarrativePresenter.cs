using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed class NarrativePresenter : INarrativePresenter, INarrativePresentationDrain, INarrativePresentationCancellation, IDisposable
    {
        private readonly NarrativeConfiguration configuration;
        private readonly Func<string, string> resolve;
        private readonly NarrativePresentationUI ui;
        private readonly PresentationResources resources;
        private readonly Queue<NarrativeSetupParamsDefinition> effects = new Queue<NarrativeSetupParamsDefinition>();
        private Action completion;
        private Action drainCompleted;
        private Coroutine routine;
        private IDisposable pauseOwner;
        private int ownerSceneHandle;
        private long sessionVersion;
        private NarrativeEventRequest request;
        private bool draining, disposed, skipRequested, allowSkip, choosing;
        private NarrativeReadingPlayback readingPlayback;
        private NarrativeNoticePlayback noticePlayback;
        private NarrativeConversationPlayback conversationPlayback;
        private Func<string, string, string, NarrativeCharacterPresentationData> characterResolver;
        private int pageIndex;
        private bool choicesEverShown, continueEverShown, finishPresentation;
        private readonly List<string> pages = new List<string>();

        public bool IsPresenting => completion != null || draining || effects.Count > 0;
        public event Action<NarrativeEventRequest> Started;
        public event Action<NarrativeEventRequest> Finished;
        public event Action<string> Cancelled;
        public event Func<string, bool> ChoiceSelected;
        public Func<string, NarrativeConditionContext, bool> ChoiceUnlocked { get; set; }
        public Func<string, NarrativeAbilityCheckSnapshot> AbilityCheck { get; set; }
        public NarrativeReadingPlayback ReadingPlayback { get => readingPlayback; set => readingPlayback = value; }
        public NarrativeNoticePlayback NoticePlayback { get => noticePlayback; set => noticePlayback = value; }
        public NarrativeConversationPlayback ConversationPlayback { get => conversationPlayback; set => conversationPlayback = value; }
        public Func<string, string, string, NarrativeCharacterPresentationData> CharacterResolver { get => characterResolver; set => characterResolver = value; }

        public NarrativePresenter(NarrativeConfiguration configuration = null, Func<string, string> textResolver = null)
        {
            this.configuration = configuration;
            resolve = textResolver ?? (key => key ?? string.Empty);
            ui = NarrativePresentationUI.Create();
            resources = new PresentationResources(ui);
            ui.Escape += OnEscape;
            ui.gameObject.AddComponent<Spotlight.GameFlow.PresentationPauseLifetime>().Interrupted = () =>
            {
                ui.StopAllCoroutines();
                AbortPresentation("host_unavailable");
            };
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Play(NarrativeEventRequest incoming, Action completed)
        {
            if (disposed || IsPresenting) throw new InvalidOperationException("NCS presenter requires a single live session.");
            request = incoming;
            completion = completed ?? throw new ArgumentNullException(nameof(completed));
            long version = ++sessionVersion;
            try
            {
                Started?.Invoke(incoming);
                if (!RequiresPresentation(incoming.Event, FindSetup(incoming.Event.setupKey)))
                {
                    Action callback = completion;
                    completion = null;
                    Finished?.Invoke(incoming);
                    callback();
                    return;
                }
                BeginPause();
                routine = ui.StartCoroutine(Guarded(PlayEvent()));
            }
            catch (Exception error) { if (sessionVersion == version) AbortPresentation(error.Message); throw; }
        }

        private IEnumerator PlayEvent()
        {
            NarrativeSetupParamsDefinition setup = FindSetup(request.Event.setupKey);
            yield return Sequence(request.Event, setup);
            Action callback = completion;
            completion = null;
            routine = null;
            resources.RestoreCamera();
            ui.Hide();
            Finished?.Invoke(request);
            callback?.Invoke();
        }

        /// <summary>Empty result transactions settle synchronously without opening a window.</summary>
        public static bool RequiresPresentation(NarrativeEventDefinition definition, NarrativeSetupParamsDefinition setup)
        {
            if (definition == null) return false;
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) || setup != null ||
                !string.IsNullOrWhiteSpace(definition.setupKey) || !string.IsNullOrWhiteSpace(definition.setupType)) return true;
            if (definition.contentKeys != null)
                foreach (string key in definition.contentKeys) if (!string.IsNullOrWhiteSpace(key)) return true;
            if (definition.eventProperties != null)
                foreach (NarrativeEventProperty property in definition.eventProperties)
                    if (property != null && !string.IsNullOrWhiteSpace(property.value)) return true;
            return false;
        }

        private void BeginPause()
        {
            ownerSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            pauseOwner = Spotlight.GameFlow.PresentationPauseRegistry.Acquire("NarrativePresenter");
        }

        // Drive nested enumerators ourselves: Unity does not propagate a child coroutine's
        // exception into the parent iterator's finally block.
        private IEnumerator Guarded(IEnumerator root)
        {
            IDisposable lease = pauseOwner;
            long version = sessionVersion;
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    bool moved;
                    object yielded = null;
                    try { moved = current.MoveNext(); if (moved) yielded = current.Current; }
                    catch (Exception error) { if (sessionVersion == version) AbortPresentation(error.Message); throw; }
                    if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (yielded is IEnumerator nested) stack.Push(nested);
                    else yield return yielded;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                lease?.Dispose();
                if (ReferenceEquals(pauseOwner, lease)) pauseOwner = null;
            }
        }

        private void AbortPresentation(string reason)
        {
            bool wasActive = completion != null || draining;
            sessionVersion++;
            pauseOwner?.Dispose();
            pauseOwner = null;
            completion = drainCompleted = null;
            draining = choosing = false;
            effects.Clear();
            resources.RestoreCamera();
            if (ui != null) ui.Hide();
            if (wasActive) Cancelled?.Invoke(reason);
        }

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (pauseOwner == null || scene.handle != ownerSceneHandle) return;
            if (ui != null) ui.StopAllCoroutines();
            AbortPresentation("host_unavailable");
        }

        private NarrativeSetupParamsDefinition FindSetup(string key)
            => configuration?.setups?.Find(item => item != null && item.setupKey == key);

        private static string TemplateForPresentationClass(string presentationClass, string fallback)
        {
            switch ((presentationClass ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "conversation": return "Tpl_TwoPersonTalk";
                case "reading": return "Tpl_Narration";
                case "notice": return "Tpl_WorldBroadcast";
                case "cutscene": return "Tpl_CutScene";
                default: return fallback;
            }
        }

        private IEnumerator Sequence(NarrativeEventDefinition definition, NarrativeSetupParamsDefinition setup)
        {
            string templateName = setup?.tplName ?? definition.setupType;
            string presentationClass = definition.presentationClass;
            NarrativePresentationIndexDefinition presentation = configuration?.presentations?.index?.Find(item => item != null && item.presentationKey == definition.presentationKey);
            if (presentation != null) presentationClass = presentation.presentationClass;
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) && presentation == null)
            {
                Report("Unknown presentationKey: " + definition.presentationKey);
                yield break;
            }
            if (!string.IsNullOrWhiteSpace(presentationClass)) templateName = TemplateForPresentationClass(presentationClass, templateName);
            NarrativeTemplateDefinition template = configuration?.templates?.Find(item => item != null && item.tplName == templateName);
            Dictionary<string, string> slots = PresentationSlots.Parse(setup);
            PresentationSlots.ApplyEventProperties(slots, definition);
            string Get(string key, string fallback = "") => slots.TryGetValue(key, out string value) ? value : fallback;
            skipRequested = false;
            allowSkip = string.Equals(template?.interruptType, "playback", StringComparison.OrdinalIgnoreCase) && (setup?.skipAble ?? template?.defaultSkip ?? false);
            string resolvedTitle = resolve(Get("titleKey", definition.titleText));
            ui.Show(resolvedTitle);
            finishPresentation = false;
            pageIndex = 0; choicesEverShown = false; continueEverShown = false; finishPresentation = false; pages.Clear();
            if (!string.IsNullOrWhiteSpace(setup?.checkId)) yield return AbilityCheckPresentation(setup.checkId);
            bool isReading = string.Equals(presentationClass, "reading", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(presentationClass, "conversation", StringComparison.OrdinalIgnoreCase))
            {
                List<NarrativeConversationPageDefinition> dialoguePages = configuration?.presentations?.conversationPages?.FindAll(item => item != null && item.presentationKey == definition.presentationKey);
                dialoguePages?.Sort((a, b) => a.seq.CompareTo(b.seq));
                if (dialoguePages != null && dialoguePages.Count > 0)
                {
                    var conversationData = new NarrativeConversationPresentationData { title = resolvedTitle };
                    foreach (NarrativeConversationPageDefinition page in dialoguePages)
                    {
                        List<NarrativeConversationLineDefinition> lines = configuration.presentations.conversationLines.FindAll(item => item != null && item.pageId == page.pageId);
                        lines.Sort((a, b) => a.seq.CompareTo(b.seq));
                        foreach (NarrativeConversationLineDefinition line in lines)
                        {
                            NarrativeObjectDefinition speakerObject = configuration.objects.Find(item => item != null && item.objId == line.speakerKey);
                            NarrativeCharacterPresentationData characterData = characterResolver?.Invoke(line.speakerKey, resolve(speakerObject?.nameText), string.IsNullOrWhiteSpace(speakerObject?.portraitPath) ? speakerObject?.iconPath : speakerObject.portraitPath);
                            conversationData.lines.Add(new NarrativeConversationPresentationLine
                            {
                                speakerKey = line.speakerKey, speakerName = characterData?.name ?? resolve(speakerObject?.nameText),
                                portraitPath = characterData?.portraitPath ?? speakerObject?.iconPath, portrait = characterData?.portrait,
                                side = line.side, emote = line.emote,
                                text = resolve(line.textKey), accentFxKey = line.accentFxKey, accentTargetKey = line.accentTargetKey
                            });
                        }
                    }
                    if (conversationData.lines.Count > 0 && conversationPlayback != null)
                    {
                        bool handled = conversationPlayback(request, conversationData, () => finishPresentation = true);
                        if (handled)
                        {
                            ui.Hide();
                            while (!finishPresentation && !skipRequested) yield return null;
                            yield break;
                        }
                    }
                }
            }
            if (string.Equals(presentationClass, "notice", StringComparison.OrdinalIgnoreCase))
            {
                NarrativeNoticeDefinition notice = configuration?.presentations?.notices?.Find(item => item != null && item.presentationKey == definition.presentationKey);
                if (notice == null) { Report("Missing notice presentation: " + definition.presentationKey); yield break; }
                if (noticePlayback == null) { Report("Notice playback is not connected to the formal UI."); yield break; }
                string title = string.IsNullOrWhiteSpace(notice.titleKey) ? string.Empty : resolve(notice.titleKey);
                string time = string.IsNullOrWhiteSpace(notice.timeKey) ? string.Empty : resolve(notice.timeKey);
                string body = resolve(notice.textKey);
                var data = new NarrativeNoticePresentationData
                {
                    presentationKey = notice.presentationKey, titleKey = notice.titleKey, timeKey = notice.timeKey,
                    imageKey = notice.imageKey, textKey = notice.textKey, barConds = notice.barConds,
                    title = title, time = time, body = body
                };
                bool handled = noticePlayback(request, data, () => finishPresentation = true);
                if (handled)
                {
                    ui.Hide();
                    while (!finishPresentation && !skipRequested) yield return null;
                    yield break;
                }
            }
            string[] atoms = PresentationSlots.Sequence(template?.atomSeq);
            if (atoms.Length == 0)
            {
                if (setup != null || !string.IsNullOrWhiteSpace(definition.setupType))
                    Report("Missing or empty template: " + (setup?.tplName ?? definition.setupType));
                atoms = new[] { "A_NARRATE" };
            }
            foreach (string atom in atoms)
            {
                // A choice is never bypassed by playback skip or auto advance.
                string name = atom.Trim().ToUpperInvariant();
                if (skipRequested && name != "A_CHOICE") continue;
                while (ui.IsSuspended) yield return null;
                if (finishPresentation) break;
                switch (name)
                {
                    case "A_DIALOG":
                    case "A_NARRATE":
                        string lines = Get("lines", Get("textKey"));
                        IList<string> keys = string.IsNullOrWhiteSpace(lines) ? definition.contentKeys : lines.Split('|');
                        pages.Clear();
                        List<NarrativeReadingPageDefinition> configuredReadingPages = null;
                        if (isReading)
                        {
                            configuredReadingPages = configuration?.presentations?.readingPages?.FindAll(item => item != null && item.presentationKey == definition.presentationKey);
                            configuredReadingPages?.Sort((a, b) => a.seq.CompareTo(b.seq));
                            List<NarrativeReadingPageDefinition> configuredPages = configuredReadingPages;
                            configuredPages?.Sort((a, b) => a.seq.CompareTo(b.seq));
                            if (configuredPages != null && configuredPages.Count > 0)
                            {
                                foreach (NarrativeReadingPageDefinition page in configuredPages)
                                {
                                    List<NarrativeReadingLineDefinition> configuredLines = configuration.presentations.readingLines.FindAll(item => item != null && item.pageId == page.pageId);
                                    configuredLines.Sort((a, b) => a.seq.CompareTo(b.seq));
                                    pages.Add(configuredLines.Count == 0 ? string.Empty : string.Join("\n", configuredLines.ConvertAll(line => resolve(line.textKey))));
                                }
                            }
                            else if (keys != null) foreach (string key in keys) pages.Add(resolve(key.Trim()));
                        }
                        else if (keys != null) foreach (string key in keys) pages.Add(resolve(key.Trim()));
                        if (pages.Count > 0)
                        {
                            if (isReading && readingPlayback != null)
                            {
                                List<NarrativeReadingPageDefinition> configuredPages = configuration?.presentations?.readingPages?.FindAll(item => item != null && item.presentationKey == definition.presentationKey);
                                configuredPages?.Sort((a, b) => a.seq.CompareTo(b.seq));
                                NarrativeReadingPageDefinition configuredPage = configuredPages != null && pageIndex < configuredPages.Count ? configuredPages[pageIndex] : null;
                                string imagePath = configuredPage?.imageKey;
                                string configuredTitle = string.IsNullOrWhiteSpace(configuredPage?.titleKey) ? resolvedTitle : resolve(configuredPage.titleKey);
                                NarrativeReadingLineDefinition dialogueLine = configuredPage == null ? null : configuration.presentations.readingLines.Find(line => line != null && line.pageId == configuredPage.pageId && line.mode == "dialogue");
                                NarrativeObjectDefinition readingSpeaker = dialogueLine == null ? null : configuration.objects.Find(item => item != null && item.objId == dialogueLine.speakerKey);
                                NarrativeCharacterPresentationData speakerPresentation = dialogueLine == null ? null : characterResolver?.Invoke(dialogueLine.speakerKey, resolve(readingSpeaker?.nameText), string.IsNullOrWhiteSpace(readingSpeaker?.portraitPath) ? readingSpeaker?.iconPath : readingSpeaker.portraitPath);
                                bool handled = readingPlayback(request, configuredTitle, new[] { pages[pageIndex] }, imagePath,
                                    dialogueLine != null,
                                    speakerPresentation?.name ?? resolve(readingSpeaker?.nameText), speakerPresentation?.portraitPath ?? Get("portrait"),
                                    () => finishPresentation = true);
                                if (handled)
                                {
                                    // The reusable reading panel owns the visible presentation layer.
                                    ui.Hide();
                                    while (!finishPresentation && !skipRequested) yield return null;
                                    break;
                                }
                            }
                            if (!isReading && conversationPlayback != null)
                            {
                                var data = new NarrativeConversationPresentationData { title = resolvedTitle };
                                var speaker = configuration?.objects?.Find(o => o.objId == definition.actorId && o.objType != NarrativeObjectType.World);
                                var character = speaker == null ? null : characterResolver?.Invoke(speaker.objId, resolve(speaker.nameText), speaker.portraitPath);
                                foreach (var text in pages)
                                    data.lines.Add(new NarrativeConversationPresentationLine
                                    {
                                        speakerKey = speaker?.objId, speakerName = character?.name ?? resolve(speaker?.nameText),
                                        portraitPath = character?.portraitPath ?? speaker?.portraitPath,
                                        portrait = character?.portrait, side = "right", text = text
                                    });
                                bool dialogueDone = false;
                                if (conversationPlayback(request, data, () => dialogueDone = true))
                                {
                                    ui.Hide();
                                    while (!dialogueDone) yield return null;
                                    ui.Show(resolvedTitle); // 后续选项/原子仍由 NCS 执行，不提前结算整个事件。
                                    break;
                                }
                            }
                            yield return DialoguePages(setup?.autoAdvance ?? false);
                        }
                        break;
                    case "A_BG": resources.Background(Get("bg")); break;
                    case "A_PORTRAIT":
                        yield return resources.Portraits(Get("a", Get("portrait")), Get("b"), Get("posA", "left"), Get("posB", "right"), PresentationSlots.Number(Get("fade")));
                        break;
                    case "A_CHOICE":
                        string opts = Get("opts");
                        if (!string.IsNullOrWhiteSpace(opts)) yield return Choice(opts);
                        break;
                    case "A_WAIT":
                        ui.ResetSubmit();
                        if (bool.TryParse(Get("untilClick", "false"), out bool click) && click)
                            while (!skipRequested && !ui.ConsumeSubmit()) yield return null;
                        else yield return Wait(PresentationSlots.Number(Get("duration")));
                        break;
                    case "A_CAMERA":
                        yield return resources.Camera(PresentationSlots.Number(Get("zoom"), 1f), PresentationSlots.Number(Get("shake")), PresentationSlots.Number(Get("duration"), .3f), () => skipRequested);
                        break;
                    case "A_SFX":
                        yield return Wait(PresentationSlots.Number(Get("delay")));
                        if (!skipRequested) resources.Sfx(Get("sfx"), PresentationSlots.Number(Get("volume"), 1f));
                        break;
                    case "A_BGM":
                        yield return resources.Bgm(Get("bgm"), PresentationSlots.Number(Get("volume"), 1f), PresentationSlots.Number(Get("fadeIn")), PresentationSlots.Number(Get("fadeOut")), () => skipRequested);
                        break;
                    case "A_FX": yield return resources.Fx(Get("fx"), PresentationSlots.Number(Get("duration"), 1f), () => skipRequested); break;
                    case "A_FADE": yield return ui.Fade(Get("type", "out"), PresentationSlots.Number(Get("duration"), .3f), () => skipRequested); break;
                    case "A_BAR_HINT":
                        ui.ShowHint(resolve(Get("textKey", Get("barKey"))) + "  " + Get("delta"));
                        yield return Wait(PresentationSlots.Number(Get("duration"), 1f));
                        break;
                    default: Report("Unsupported atom: " + atom); break;
                }
            }
        }

        private IEnumerator DialoguePages(bool autoAdvance)
        {
            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
            while (!skipRequested && !finishPresentation)
            {
                if (pageIndex >= pages.Count) yield break;
                ui.ShowLine(pages[pageIndex]);
                ui.ResetSubmit();
                yield return ui.Reveal(() => skipRequested);
                bool lastPage = pageIndex == pages.Count - 1;
                bool showContinue = lastPage && (continueEverShown || !HasChoicesAfterCurrent());
                if (showContinue) continueEverShown = true;
                bool choicesFollow = lastPage && HasChoicesAfterCurrent();
                ui.SetNavigation(pageIndex > 0, !lastPage || choicesFollow, showContinue,
                    () => { if (pageIndex > 0) pageIndex--; },
                    () => { if (pageIndex < pages.Count - 1) pageIndex++; else if (choicesFollow) pageIndex = pages.Count; },
                    () => finishPresentation = true);
                if (autoAdvance && !choicesEverShown && !continueEverShown) yield return Wait(.7f);
                else while (!skipRequested && !finishPresentation && !ui.ConsumeSubmit()) yield return null;
                if (finishPresentation || skipRequested) yield break;
                pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count);
                if (pageIndex >= pages.Count) yield break;
            }
        }

        private string ChoiceRequirement(string choiceId)
        {
            List<NarrativeConditionDefinition> requirements = configuration?.conditions?.FindAll(c => c != null && c.ownerType == "choice" && c.ownerId == choiceId) ?? new List<NarrativeConditionDefinition>();
            if (requirements.Count == 0) return "条件未满足";
            List<string> descriptions = new List<string>();
            foreach (NarrativeConditionDefinition condition in requirements)
            {
                NarrativeBarDefinition bar = configuration.bars.Find(b => b != null && b.barKey == condition.target);
                string label = bar != null ? resolve(bar.nameText) : condition.target;
                descriptions.Add(label + " " + condition.op + " " + condition.value);
            }
            return string.Join(" / ", descriptions);
        }

        private bool HasChoicesAfterCurrent()
        {
            NarrativeSetupParamsDefinition setup = FindSetup(request.Event.setupKey);
            if (setup == null) return false;
            foreach (string assignment in (setup.slotValues ?? string.Empty).Split(','))
            {
                int equals = assignment.IndexOf('=');
                if (equals > 0 && assignment.Substring(0, equals).Trim() == "opts" && !string.IsNullOrWhiteSpace(assignment.Substring(equals + 1))) return true;
            }
            return false;
        }

        private IEnumerator Choice(string group)
        {
            choosing = true;
            ui.ResetSubmit();
            List<NarrativeChoiceDefinition> choices = configuration?.choices?.FindAll(item => item != null && item.groupKey == group) ?? new List<NarrativeChoiceDefinition>();
            choices.Sort((a, b) => a.seq.CompareTo(b.seq));
            if (choices.Count == 0)
            {
                Report("Choice group has no choices: " + group);
                // Fail closed: the request stays active/saveable instead of applying unchosen results.
                while (choosing) yield return null;
            }
            bool selected = false;
            foreach (NarrativeChoiceDefinition choice in choices)
                if (request.SelectedChoices != null && Contains(request.SelectedChoices, choice.choiceId)) selected = true;
            if (selected && AbilityCheck != null)
            {
                NarrativeChoiceDefinition restoredChoice = choices.Find(choice => request.SelectedChoices != null && Contains(request.SelectedChoices, choice.choiceId));
                if (!string.IsNullOrWhiteSpace(restoredChoice?.checkId))
                {
                    NarrativeAbilityCheckSnapshot restoredResult = AbilityCheck(restoredChoice.checkId);
                    ui.ShowLine(FormatAbilityCheck(restoredResult));
                    ui.ResetSubmit();
                    yield return ui.Reveal(() => skipRequested);
                    while (!skipRequested && !ui.ConsumeSubmit()) yield return null;
                }
            }
            pageIndex = pages.Count;
            choicesEverShown = true;
            string selectedId = null;
            ui.ShowChoices(choices, resolve, id => ChoiceUnlocked?.Invoke(id, request.Context) ?? false, ChoiceRequirement,
                request.SelectedChoices, id => { if (!selected) selectedId = id; });
            if (!selected) while (selectedId == null) yield return null;
            if (selectedId != null && ChoiceSelected != null && ChoiceSelected.Invoke(selectedId))
            {
                NarrativeChoiceDefinition selectedChoice = choices.Find(x => x.choiceId == selectedId);
                if (!string.IsNullOrWhiteSpace(selectedChoice?.checkId))
                {
                    NarrativeAbilityCheckSnapshot result = AbilityCheck?.Invoke(selectedChoice.checkId);
                    if (result == null) { Report("Ability check service is unavailable: " + selectedChoice.checkId); while (choosing) yield return null; }
                    ui.ShowLine(FormatAbilityCheck(result));
                    ui.ResetSubmit();
                    yield return ui.Reveal(() => skipRequested);
                    while (!skipRequested && !ui.ConsumeSubmit()) yield return null;
                }
                selected = true;
            }
            choicesEverShown = true;
            continueEverShown = true;
            choosing = false;
            ui.ResetSubmit();
            ui.SetNavigation(false, false, true, null, null,
                () => finishPresentation = true);
        }

        private IEnumerator AbilityCheckPresentation(string checkId)
        {
            NarrativeAbilityCheckSnapshot result = AbilityCheck?.Invoke(checkId);
            if (result == null) { Report("Ability check service is unavailable: " + checkId); while (true) yield return null; }
            ui.ShowLine(FormatAbilityCheck(result));
            ui.ResetSubmit();
            yield return ui.Reveal(() => skipRequested);
            while (!skipRequested && !ui.ConsumeSubmit()) yield return null;
        }

        private IEnumerator WaitForCheckAcknowledgement()
        {
            ui.ResetSubmit();
            while (!skipRequested && !ui.ConsumeSubmit()) yield return null;
        }

        private static string FormatAbilityCheck(NarrativeAbilityCheckSnapshot result)
        {
            var lines = new List<string>();
            int lastGroup = -1;
            foreach (var value in result.values)
            {
                if (lastGroup > 0 && value.groupIndex != lastGroup) lines.Add("或");
                lines.Add("组 " + value.groupIndex + "：" + value.displayName + " " + value.currentValue + (value.passed ? " >= " : " < ") + value.threshold);
                lastGroup = value.groupIndex;
            }
            lines.Add(result.passed ? "检定成功" : "检定失败");
            return string.Join("\n", lines);
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++) if (values[i] == value) return true;
            return false;
        }

        private IEnumerator Wait(float duration)
        {
            float elapsed = 0;
            while (elapsed < Mathf.Max(0, duration) && !skipRequested)
            {
                if (!ui.IsSuspended) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void OnEscape()
        {
            if (!IsPresenting || choosing) return;
            if (allowSkip) skipRequested = true;
            // Progressive presentation never skips the event; it only reveals the current line.
            else ui.RevealImmediately();
        }

        public void ExecuteEffect(NarrativeEffectNotification notification)
        {
            if (notification?.Result == null) return;
            if (notification.Result.resultType == NarrativeResultType.PlaySetup)
            {
                NarrativeSetupParamsDefinition setup = FindSetup(notification.Target);
                if (setup == null) Report("Missing setup: " + notification.Target);
                else if (ContainsChoice(setup)) Report("PLAY_SETUP cannot contain choices; use a narrative event for a choice transaction: " + setup.setupKey);
                else effects.Enqueue(setup);
            }
            else if (notification.Result.resultType == NarrativeResultType.BarHint)
                ui.ShowHint(resolve(notification.Target) + "  " + notification.Value);
            else if (notification.Result.resultType == NarrativeResultType.SpawnNpc || notification.Result.resultType == NarrativeResultType.DespawnNpc)
                resources.Npc(notification.Target, notification.Value, notification.Result.resultType == NarrativeResultType.SpawnNpc);
        }

        private bool ContainsChoice(NarrativeSetupParamsDefinition setup)
        {
            NarrativeTemplateDefinition template = configuration?.templates?.Find(item => item != null && item.tplName == setup.tplName);
            foreach (string atom in PresentationSlots.Sequence(template?.atomSeq))
                if (string.Equals(atom, "A_CHOICE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(PresentationSlots.Parse(setup).TryGetValue("opts", out string group) ? group : null)) return true;
            return false;
        }

        public void SetSuspended(bool suspended) => ui.SetSuspended(suspended);

        public void SyncNpcLocations(IReadOnlyList<NarrativeStringValue> locations)
        {
            if (locations == null) return;
            foreach (NarrativeStringValue value in locations) if (value != null) resources.Npc(value.key, value.value, true);
        }

        public void Drain(Action completed)
        {
            if (disposed || completion != null || draining) throw new InvalidOperationException("NCS drain cannot overlap a presentation.");
            if (effects.Count == 0) { completed?.Invoke(); return; }
            draining = true;
            drainCompleted = completed;
            long version = ++sessionVersion;
            try { BeginPause(); routine = ui.StartCoroutine(Guarded(DrainEffects())); }
            catch (Exception error) { if (sessionVersion == version) AbortPresentation(error.Message); throw; }
        }

        private IEnumerator DrainEffects()
        {
            while (effects.Count > 0)
            {
                NarrativeSetupParamsDefinition setup = effects.Dequeue();
                yield return Sequence(new NarrativeEventDefinition { eventId = "setup:" + setup.setupKey, setupKey = setup.setupKey }, setup);
            }
            resources.RestoreCamera();
            ui.Hide();
            draining = false;
            routine = null;
            Action callback = drainCompleted;
            drainCompleted = null;
            callback?.Invoke();
        }

        private void Report(string message)
        {
            Debug.LogError("[NCS][Presentation] " + message);
            ui.ShowHint(message);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            if (ui != null) ui.StopAllCoroutines();
            AbortPresentation("disposed");
            completion = drainCompleted = null;
            effects.Clear();
            resources.Dispose();
            if (ui != null) ui.Dispose();
        }
    }
}
