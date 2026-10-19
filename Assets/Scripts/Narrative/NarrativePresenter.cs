using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed partial class NarrativePresenter : INarrativePresenter, INarrativePresentationDrain, INarrativePresentationCancellation, INarrativePresentationContinuity, INarrativeGameplayResultSource, IDisposable
    {
        private readonly NarrativeConfiguration configuration;
        private readonly Func<string, string> resolve;
        private readonly NarrativePresentationUI ui;
        private readonly PresentationResources resources;
        private readonly Queue<NarrativeSetupParamsDefinition> effects = new Queue<NarrativeSetupParamsDefinition>();
        private Action completion;
        private Action drainCompleted;
        private Coroutine routine;
        private PresenterPauseLease pauseOwner;
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
        private readonly HashSet<string> presentedChoiceGroups = new HashSet<string>(StringComparer.Ordinal);

        public static Func<bool> ExternalInputBlocked { get; set; }
        private bool externallySuspended;
        private bool hostSuspended;
        private int resumeFrame = -1;
        private bool RefreshSuspension()
        {
            bool blocked = ExternalInputBlocked?.Invoke() == true;
            if (externallySuspended != blocked)
            {
                externallySuspended = blocked;
                if (!blocked) resumeFrame = Time.frameCount;
                ui.ResetSubmit();
            }
            bool paused = hostSuspended || blocked || Time.frameCount <= resumeFrame;
            ui.SetSuspended(paused);
            resources.SetSuspended(paused);
            suspendActiveLetter?.Invoke(paused);
            return paused;
        }

        public bool IsPresenting => completion != null || draining || effects.Count > 0;
        public event Action<NarrativeEventRequest> Started;
        public event Action<NarrativeEventRequest> Finished;
        public event Action<string> Cancelled;
        public event Func<string, bool> ChoiceSelected;
        public Func<string, NarrativeConditionContext, bool> ChoiceUnlocked { get; set; }
        public Func<string, NarrativeAbilityCheckSnapshot> AbilityCheck { get; set; }
        public Func<string, NarrativeAbilityCheckSnapshot> InlineAbilityCheck { get; set; }
        public NarrativeLetterPlayback LetterPlayback { get; set; }
        /// <summary>Preview hosts only; null plays the complete formal sequence.</summary>
        public string PreviewStartPageId { get; set; }
        private Action releaseActiveStage;
        private sealed class ContinuityStage
        {
            public readonly object Owner = new object();
            public string Background;
            public bool Released;
            public Action Release, ConversationRelease;
            public List<Action> StepReleases = new List<Action>();
        }
        private bool batchActive;
        private ContinuityStage batchStage;

        public void BeginBatch()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NarrativePresenter));
            batchActive = true;
        }

        public void EndBatch()
        {
            // Detach first: releasing an adapter may synchronously begin the next batch.
            batchActive = false;
            var stage = batchStage;
            batchStage = null;
            if (stage != null) ReleaseStage(stage);
        }

        private ContinuityStage CreateStage()
        {
            var stage = new ContinuityStage();
            stage.Release = () => ReleaseStage(stage);
            return stage;
        }

        private void ReleaseStage(ContinuityStage stage)
        {
            if (stage.Released) return;
            stage.Released = true;
            if (ReferenceEquals(releaseActiveStage, stage.Release)) releaseActiveStage = null;
            suspendActiveLetter = null;
            bool wasReleasing = stageReleaseInProgress;
            stageReleaseInProgress = true;
            try
            {
                InvokeStageRelease(stage.ConversationRelease);
                foreach (var release in stage.StepReleases.ToArray()) InvokeStageRelease(release);
            }
            finally
            {
                stage.ConversationRelease = null;
                stage.StepReleases.Clear();
                stageReleaseInProgress = wasReleasing;
            }
        }

        private void InvokeStageRelease(Action release)
        {
            if (release == null) return;
            bool wasReleasing = stageReleaseInProgress;
            stageReleaseInProgress = true;
            try { release(); } catch (Exception error) { Debug.LogException(error); }
            finally { stageReleaseInProgress = wasReleasing; }
        }

        private void RetainConversationRelease(ContinuityStage stage, Action release, bool batched)
        {
            if (release == null) return;
            if (stage.Released) InvokeStageRelease(release); // Adapter returned after synchronous cancellation.
            else if (batched) stage.ConversationRelease = release;
            else stage.StepReleases.Add(release);
        }
        private Action<bool> suspendActiveLetter;
        private bool stageCancellationRequested, stageReleaseInProgress;
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
            stageCancellationRequested = false;
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
            long version = sessionVersion;
            NarrativeEventRequest currentRequest = request;
            NarrativeSetupParamsDefinition setup = FindSetup(currentRequest.Event.setupKey);
            yield return Sequence(currentRequest.Event, setup);
            if (disposed || sessionVersion != version) yield break;
            Action callback = completion;
            completion = null;
            routine = null;
            resources.RestoreCamera();
            resources.StopAudio();
            ui.Hide();
            Finished?.Invoke(currentRequest);
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
            pauseOwner = new PresenterPauseLease();
            pauseOwner.Resume();
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
                while (stack.Count > 0 && !disposed && sessionVersion == version)
                {
                    if (sessionVersion != version) yield break;
                    if (stageCancellationRequested) { AbortPresentation("playback_cancelled"); yield break; }
                    if (RefreshSuspension()) { yield return null; continue; }
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

        /// <summary>GM only: stop visuals after the controller has invalidated pending work.</summary>
        public void DebugStopPresentation()
        {
            if (ui != null) ui.StopAllCoroutines();
            AbortPresentation("gm_skip");
        }

        private void AbortPresentation(string reason)
        {
            bool wasActive = completion != null || draining || batchActive || releaseActiveStage != null;
            sessionVersion++;
            CancelGameplay();
            pauseOwner?.Dispose();
            pauseOwner = null;
            completion = drainCompleted = null;
            draining = choosing = false;
            var release = releaseActiveStage;
            releaseActiveStage = null;
            EndBatch();
            release?.Invoke();
            suspendActiveLetter = null;
            effects.Clear();
            resources.RestoreCamera();
            resources.StopAudio();
            if (ui != null) ui.Hide();
            if (wasActive || reason == "disposed") Cancelled?.Invoke(reason);
        }

        /// <summary>Release a gameplay host without disposing the reusable presenter or committing its event.</summary>
        public void InterruptForHost()
        {
            if (disposed) return;
            if (ui != null) ui.StopAllCoroutines();
            AbortPresentation("host_unavailable");
        }

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (pauseOwner == null || scene.handle != ownerSceneHandle) return;
            InterruptForHost();
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
            long sequenceVersion = sessionVersion;
            string templateName = setup?.tplName ?? definition.setupType;
            string presentationClass = definition.presentationClass;
            NarrativePresentationIndexDefinition presentation = configuration?.presentations?.index?.Find(item => item != null && item.presentationKey == definition.presentationKey);
            if (presentation != null) presentationClass = presentation.presentationClass;
            if (!string.IsNullOrWhiteSpace(definition.presentationKey) && presentation == null)
            {
                Report("Unknown presentationKey: " + definition.presentationKey);
                yield break;
            }
            if (string.IsNullOrWhiteSpace(templateName)) templateName = TemplateForPresentationClass(presentationClass, templateName);
            NarrativeTemplateDefinition template = configuration?.templates?.Find(item => item != null && item.tplName == templateName);
            Dictionary<string, string> slots = PresentationSlots.Parse(setup);
            PresentationSlots.ApplyEventProperties(slots, definition);
            string Get(string key, string fallback = "") => slots.TryGetValue(key, out string value) ? value : fallback;
            skipRequested = false;
            allowSkip = string.Equals(template?.interruptType, "playback", StringComparison.OrdinalIgnoreCase) && (setup?.skipAble ?? template?.defaultSkip ?? false);
            // A playback skip must never manufacture completion of a required host interaction.
            if (configuration?.presentations?.conversationPages?.Exists(p => p != null && p.presentationKey == definition.presentationKey && p.stepType == NarrativeConversationStepType.Gameplay) == true)
                allowSkip = false;
            string resolvedTitle = resolve(Get("titleKey", definition.titleText));
            if (!(Get("checkDisplay") == "passive" && string.IsNullOrWhiteSpace(definition.presentationKey)
                && (definition.contentKeys == null || definition.contentKeys.Count == 0))) ui.Show(resolvedTitle, batchStage?.ConversationRelease != null);
            finishPresentation = false;
            presentedChoiceGroups.Clear();
            pageIndex = 0; choicesEverShown = false; continueEverShown = false; finishPresentation = false; pages.Clear();
            if (!string.IsNullOrWhiteSpace(setup?.checkId))
            {
                if (Get("checkDisplay") == "passive")
                {
                    if (AbilityCheck?.Invoke(setup.checkId) == null) throw new InvalidOperationException("Ability check unavailable: " + setup.checkId);
                    // A silent check has no presentation atoms to wait for, even while
                    // the host has suspended its ordinary narrative window.
                    if (string.IsNullOrWhiteSpace(definition.presentationKey)
                        && (definition.contentKeys == null || definition.contentKeys.Count == 0)) yield break;
                }
                else yield return AbilityCheckPresentation(setup.checkId);
            }
            bool isReading = string.Equals(presentationClass, "reading", StringComparison.OrdinalIgnoreCase);
            bool isConversation = string.Equals(presentationClass, "conversation", StringComparison.OrdinalIgnoreCase);
            bool isNotice = string.Equals(presentationClass, "notice", StringComparison.OrdinalIgnoreCase);
            string[] atoms = PresentationSlots.Sequence(template?.atomSeq);
            if (atoms.Length == 0)
            {
                if (setup != null || !string.IsNullOrWhiteSpace(definition.setupType))
                    Report("Missing or empty template: " + (setup?.tplName ?? definition.setupType));
                atoms = new[] { "A_NARRATE" };
            }
            foreach (string atom in atoms)
            {
                if (disposed || sessionVersion != sequenceVersion) yield break;
                // A choice is never bypassed by playback skip or auto advance.
                string name = atom.Trim().ToUpperInvariant();
                if (skipRequested && name != "A_CHOICE") continue;
                while (ui.IsSuspended) yield return null;
                if (finishPresentation) break;
                switch (name)
                {
                    case "A_DIALOG":
                    case "A_NARRATE":
                        if (isConversation && !string.IsNullOrWhiteSpace(definition.presentationKey))
                        {
                            yield return ConversationPages(definition, resolvedTitle, Get("bg"));
                            if (completion == null) yield break; // cancelled; never run trailing atoms
                            ui.Show(resolvedTitle, batchStage?.ConversationRelease != null);
                            break;
                        }
                        if (isNotice && !string.IsNullOrWhiteSpace(definition.presentationKey))
                        {
                            yield return Notice(definition, resolvedTitle);
                            finishPresentation = false;
                            ui.Show(resolvedTitle, batchStage?.ConversationRelease != null);
                            break;
                        }
                        if (isReading && configuration?.presentations?.readingPages?.Exists(page =>
                            page != null && page.presentationKey == definition.presentationKey) == true)
                        {
                            yield return ReadingPages(definition, resolvedTitle);
                            if (disposed || sessionVersion != sequenceVersion) yield break;
                            ui.Show(resolvedTitle, batchStage?.ConversationRelease != null);
                            break;
                        }
                        string lines = Get("lines", Get("textKey"));
                        IList<string> keys = string.IsNullOrWhiteSpace(lines) ? definition.contentKeys : lines.Split('|');
                        pages.Clear();
                        if (keys != null) foreach (string key in keys) pages.Add(resolve(key.Trim()));
                        if (pages.Count > 0)
                        {
                            if (isReading && readingPlayback != null)
                            {
                                bool done = false;
                                long version = sessionVersion;
                                if (readingPlayback(request, resolvedTitle, pages.ToArray(), Get("image"), false, null,
                                    Get("portrait"), () => { if (!disposed && sessionVersion == version) done = true; }))
                                {
                                    if (disposed || sessionVersion != version) yield break;
                                    ui.Hide();
                                    while (!done && !skipRequested && !disposed && sessionVersion == version) yield return null;
                                    if (disposed || sessionVersion != version) yield break;
                                    ui.Show(resolvedTitle, batchStage?.ConversationRelease != null);
                                    break;
                                }
                            }
                            if (conversationPlayback != null)
                            {
                                var stage = batchActive ? batchStage ?? (batchStage = CreateStage()) : null;
                                if (stage != null && !string.IsNullOrWhiteSpace(Get("bg"))) stage.Background = Get("bg");
                                var data = new NarrativeConversationPresentationData { title = resolvedTitle,
                                    eventId = definition.eventId, presentationKey = definition.presentationKey,
                                    continuityOwner = stage?.Owner, continues = stage != null,
                                    backgroundPath = stage?.Background ?? Get("bg") };
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
                                long dialogueVersion = sessionVersion;
                                data.cancelled = () => { if (!stageReleaseInProgress && sessionVersion == dialogueVersion) stageCancellationRequested = true; };
                                bool handled;
                                try { handled = conversationPlayback(request, data, () => { if (sessionVersion == dialogueVersion) dialogueDone = true; }); }
                                finally { if (stage != null) RetainConversationRelease(stage, data.releaseStage, true); }
                                if (handled)
                                {
                                    ui.Hide();
                                    while (!dialogueDone && !disposed && sessionVersion == dialogueVersion) yield return null;
                                    if (disposed || sessionVersion != dialogueVersion) yield break;
                                    ui.Show(resolvedTitle, batchStage?.ConversationRelease != null); // 后续选项/原子仍由 NCS 执行，不提前结算整个事件。
                                    break;
                                }
                            }
                            yield return DialoguePages(setup?.autoAdvance ?? false);
                            finishPresentation = false;
                        }
                        break;
                    case "A_BG": resources.Background(Get("bg")); break;
                    case "A_PORTRAIT":
                        yield return resources.Portraits(Get("a", Get("portrait")), Get("b"), Get("posA", "left"), Get("posB", "right"), PresentationSlots.Number(Get("fade")));
                        break;
                    case "A_CHOICE":
                        string opts = Get("opts");
                        if (!string.IsNullOrWhiteSpace(opts) && !presentedChoiceGroups.Contains(opts)) yield return Choice(opts);
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

        // Configured content is a step in the template, not an alternative to the template.
        // This keeps pre/post fades, audio, camera cues and future atoms in the formal sequence.
        private IEnumerator ConversationPages(NarrativeEventDefinition definition, string resolvedTitle, string background)
        {
                List<NarrativeConversationPageDefinition> dialoguePages = configuration?.presentations?.conversationPages?.FindAll(item => item != null && item.presentationKey == definition.presentationKey);
                dialoguePages?.Sort((a, b) => a.seq.CompareTo(b.seq));
                if (dialoguePages != null && dialoguePages.Count > 0)
                {
                    int startPage = string.IsNullOrWhiteSpace(PreviewStartPageId) ? 0 : dialoguePages.FindIndex(p => p.pageId == PreviewStartPageId);
                    if (startPage < 0) throw new InvalidOperationException("Unknown preview start page: " + PreviewStartPageId);
                    bool batched = batchActive;
                    var stage = batched ? batchStage ?? (batchStage = CreateStage()) : CreateStage();
                    if (!string.IsNullOrWhiteSpace(background)) stage.Background = background;
                    string inheritedBackground = stage.Background;
                    object stageOwner = stage.Owner;
                    var stageReleases = new List<Action>();
                    Action releaseStage = stage.Release;
                    releaseActiveStage = releaseStage;
                    try
                    {
                    var traversal = new NarrativeConversationTraversal(dialoguePages, startPage);
                    while (traversal.MoveNext(p => PageVisible(p.pageId, p.pageCondKeys, p.pageBarConds)))
                    {
                        NarrativeConversationPageDefinition page = traversal.Current;
                        if (disposed || skipRequested) yield break;
                        if (page.stepType == NarrativeConversationStepType.End) { finishPresentation = true; yield break; }
                        if (page.stepType == NarrativeConversationStepType.Gameplay)
                        {
                            if (!string.IsNullOrWhiteSpace(page.bgOverride)) stage.Background = inheritedBackground = page.bgOverride;
                            yield return GameplayStep(definition, page, stage);
                            if (completion == null) yield break;
                            continue;
                        }
                        if (page.stepType == NarrativeConversationStepType.LetterReveal || page.stepType == NarrativeConversationStepType.LetterRead)
                        {
                            if (!string.IsNullOrWhiteSpace(page.bgOverride)) stage.Background = inheritedBackground = page.bgOverride;
                            // Letter and dialogue adapters own different components. Retain both
                            // cleanup paths without accumulating callbacks across batch events.
                            if (batched) stage.StepReleases = stageReleases;
                            else stageReleases = stage.StepReleases;
                            yield return LetterStep(definition, page, stageOwner, stageReleases);
                            if (completion == null) yield break;
                            continue;
                        }
                        string stepHint = request.PassiveHint;
                        if (page.stepType == NarrativeConversationStepType.PassiveCheck)
                        {
                            var check = (InlineAbilityCheck ?? AbilityCheck)?.Invoke(page.checkId);
                            if (check == null) throw new InvalidOperationException("Inline ability check unavailable: " + page.checkId);
                            if (!check.passed) { traversal.ResumeLinearAfterCurrent(); continue; }
                            stepHint = PassiveCheckPresentationConfig.Hint(check);
                        }
                        if (!string.IsNullOrWhiteSpace(page.bgOverride)) stage.Background = inheritedBackground = page.bgOverride;
                        var conversationData = new NarrativeConversationPresentationData { title = resolvedTitle, backgroundPath = inheritedBackground, bgmOverride = page.bgmOverride,
                            eventId = definition.eventId, presentationKey = definition.presentationKey, continuityOwner = stageOwner,
                            continues = batched || dialoguePages.IndexOf(page) < dialoguePages.Count - 1 };
                        List<NarrativeConversationLineDefinition> lines = configuration.presentations.conversationLines.FindAll(item => item != null && item.pageId == page.pageId);
                        lines.Sort((a, b) => a.seq.CompareTo(b.seq));
                        foreach (NarrativeConversationLineDefinition line in lines)
                        {
                            NarrativeObjectDefinition speakerObject = configuration.objects.Find(item => item != null && item.objId == line.speakerKey);
                            NarrativeCharacterPresentationData characterData = characterResolver?.Invoke(line.speakerKey, resolve(speakerObject?.nameText), string.IsNullOrWhiteSpace(speakerObject?.portraitPath) ? speakerObject?.iconPath : speakerObject.portraitPath);
                            conversationData.lines.Add(new NarrativeConversationPresentationLine
                            {
                                lineId = line.lineId, pageId = line.pageId, seq = line.seq,
                                speakerKey = line.speakerKey, speakerName = !string.IsNullOrWhiteSpace(line.speakerNameKey) ? resolve(line.speakerNameKey) : characterData?.name ?? resolve(speakerObject?.nameText),
                                portraitPath = characterData?.portraitPath ?? speakerObject?.iconPath, portrait = characterData?.portrait,
                                side = line.side, emote = line.emote, sfxKey = line.sfxKey, voiceKey = line.voiceKey,
                                text = resolve(line.textKey), accentFxKey = line.accentFxKey, accentTargetKey = line.accentTargetKey
                            });
                        }
                        if (!string.IsNullOrWhiteSpace(page.optionGroupKey))
                        {
                            long version = sessionVersion;
                            conversationData.selectChoice = id => version == sessionVersion && completion != null
                                && configuration.choices.Exists(c => c.choiceId == id && c.groupKey == page.optionGroupKey)
                                && (ChoiceSelected?.Invoke(id) ?? false);
                            var choices = configuration.choices.FindAll(c => c.groupKey == page.optionGroupKey);
                            choices.Sort((a, b) => a.seq.CompareTo(b.seq));
                            foreach (var choice in choices)
                                conversationData.options.Add(new NarrativeConversationPresentationOption { id = choice.choiceId, text = resolve(choice.textKey),
                                    enabled = ChoiceUnlocked?.Invoke(choice.choiceId, request.Context) ?? false, requirement = ChoiceRequirement(choice.choiceId) });
                        }
                    if ((conversationData.lines.Count > 0 || conversationData.options.Count > 0) && conversationPlayback != null)
                    {
                        bool pageFinished = false;
                        bool pageCancelled = false;
                        long pageVersion = sessionVersion;
                        conversationData.cancelled = () => { if (!stageReleaseInProgress && sessionVersion == pageVersion) { pageCancelled = true; stageCancellationRequested = true; } };
                        var stepRequest = new NarrativeEventRequest(request.Event, request.Trigger, request.Depth, request.SelectedChoices, request.Context, request.RequestId, stepHint, candidate: request.Candidate);
                        bool handled;
                        try { handled = conversationPlayback(stepRequest, conversationData, () => { if (sessionVersion == pageVersion) pageFinished = true; }); }
                        finally { RetainConversationRelease(stage, conversationData.releaseStage, batched); }
                        if (!handled) throw new InvalidOperationException("Conversation playback rejected page: " + page.pageId);
                        if (handled)
                        {
                            ui.Hide();
                            while (!pageFinished && !pageCancelled && !skipRequested && !disposed && sessionVersion == pageVersion) yield return null;
                            if (disposed || sessionVersion != pageVersion) yield break;
                            if (pageCancelled) { AbortPresentation("playback_cancelled"); yield break; }
                            if (conversationData.options.Count > 0) presentedChoiceGroups.Add(page.optionGroupKey);
                        }
                    }
                    else if (conversationData.lines.Count > 0 || conversationData.options.Count > 0)
                        throw new InvalidOperationException("Conversation playback is not connected: " + page.pageId);
                    }
                    }
                    finally
                    {
                        if (!batched) releaseStage();
                        // A cancelled letter adapter can provide its release callback after
                        // Abort already released the stage. Do not retain late callbacks.
                        if (stage.Released)
                        {
                            foreach (var release in stageReleases.ToArray()) InvokeStageRelease(release);
                            stageReleases.Clear();
                        }
                    }
                    yield break;
                }
            throw new InvalidOperationException("Missing conversation pages: " + definition.presentationKey);
        }

        private IEnumerator Notice(NarrativeEventDefinition definition, string resolvedTitle)
        {
            var notice = configuration?.presentations?.notices?.Find(item => item != null && item.presentationKey == definition.presentationKey);
            if (notice == null) throw new InvalidOperationException("Missing notice presentation: " + definition.presentationKey);
            var data = new NarrativeNoticePresentationData
            {
                presentationKey = notice.presentationKey, titleKey = notice.titleKey, timeKey = notice.timeKey,
                imageKey = notice.imageKey, textKey = notice.textKey, barConds = notice.barConds,
                title = string.IsNullOrWhiteSpace(notice.titleKey) ? resolvedTitle : resolve(notice.titleKey),
                time = string.IsNullOrWhiteSpace(notice.timeKey) ? string.Empty : resolve(notice.timeKey), body = resolve(notice.textKey)
            };
            bool done = false;
            long version = sessionVersion;
            bool handled = noticePlayback?.Invoke(request, data,
                () => { if (sessionVersion == version) done = true; }) ?? false;
            if (!handled) throw new InvalidOperationException("Notice playback is unavailable: " + definition.presentationKey);
            ui.Hide();
            while (!done && !skipRequested) yield return null;
        }

        private IEnumerator ReadingPages(NarrativeEventDefinition definition, string resolvedTitle)
        {
            long version = sessionVersion;
            var configuredPages = configuration.presentations.readingPages.FindAll(page =>
                page != null && page.presentationKey == definition.presentationKey);
            configuredPages.Sort((left, right) => left.seq.CompareTo(right.seq));
            foreach (var page in configuredPages)
            {
                if (disposed || sessionVersion != version || skipRequested) yield break;
                if (!PageVisible(page.pageId, page.pageCondKeys, page.pageBarConds)) continue;
                var sourceLines = configuration.presentations.readingLines.FindAll(line => line != null && line.pageId == page.pageId);
                sourceLines.Sort((left, right) => left.seq.CompareTo(right.seq));
                var readingLines = new List<NarrativeReadingLinePresentationData>();
                foreach (var line in sourceLines)
                {
                    var speaker = configuration.objects.Find(item => item != null && item.objId == line.speakerKey);
                    var character = characterResolver?.Invoke(line.speakerKey, resolve(speaker?.nameText),
                        string.IsNullOrWhiteSpace(speaker?.portraitPath) ? speaker?.iconPath : speaker.portraitPath);
                    readingLines.Add(new NarrativeReadingLinePresentationData
                    {
                        text = resolve(line.textKey), textKey = line.textKey, speakerKey = line.speakerKey,
                        speakerName = character?.name ?? resolve(speaker?.nameText), portrait = character?.portrait,
                        portraitPath = character?.portraitPath ?? (string.IsNullOrWhiteSpace(speaker?.portraitPath) ? speaker?.iconPath : speaker.portraitPath), mode = line.mode, voiceKey = line.voiceKey
                    });
                }
                bool pageClosed = false;
                var pageRequest = new NarrativeEventRequest(request.Event, request.Trigger, request.Depth, request.SelectedChoices,
                    request.Context, request.RequestId, request.PassiveHint, readingLines, candidate: request.Candidate,
                    presentationCancelled: () =>
                    {
                        if (pageClosed || disposed || sessionVersion != version || completion == null) return;
                        pageClosed = true;
                        AbortPresentation("playback_cancelled");
                    });
                bool done = false;
                string title = string.IsNullOrWhiteSpace(page.titleKey) ? resolvedTitle : resolve(page.titleKey);
                string body = string.Join("\n", readingLines.ConvertAll(line => line.text));
                var dialogueLine = readingLines.Find(line => string.Equals(line.mode, "dialogue", StringComparison.OrdinalIgnoreCase));
                // Finish advances this page; cancellation uses the optional request callback
                // and aborts the root without settling growth/news/choice results.
                bool handled = readingPlayback?.Invoke(pageRequest, title, new[] { body }, page.imageKey,
                    dialogueLine != null, dialogueLine?.speakerName, dialogueLine?.portraitPath,
                    () =>
                    {
                        if (pageClosed || disposed || sessionVersion != version || completion == null) return;
                        pageClosed = true;
                        done = true;
                    }) ?? false;
                if (disposed || sessionVersion != version) yield break;
                if (!handled) throw new InvalidOperationException("Reading playback is unavailable: " + page.pageId);
                ui.Hide();
                while (!done && !skipRequested && !disposed && sessionVersion == version) yield return null;
                if (disposed || sessionVersion != version) yield break;
            }
        }

        private bool PageVisible(string pageId, string keys, string bars)
        {
            if (!string.IsNullOrWhiteSpace(bars))
                throw new InvalidOperationException("Unsupported presentation page bar conditions: " + pageId);
            if (!string.IsNullOrWhiteSpace(keys))
            {
                if (PageCondition == null) throw new InvalidOperationException("Page condition reader unavailable: " + pageId);
                foreach (string key in keys.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    if (!PageCondition(key.Trim())) return false;
            }
            return true;
        }
        public Func<string, bool> PageCondition { get; set; }

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
            if (!IsPresenting || choosing || RefreshSuspension()) return;
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

        public void SetSuspended(bool suspended) { hostSuspended = suspended; RefreshSuspension(); }

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
