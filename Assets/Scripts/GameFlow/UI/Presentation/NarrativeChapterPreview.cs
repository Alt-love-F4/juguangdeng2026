using System;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Generated chapter playback in a separate memory controller. Never settles the formal session.</summary>
    public sealed partial class NarrativeChapterPreview : IDisposable
    {
        private NarrativeDebugSession session;
        private NarrativePresenter presenter;
        private bool disposed;
        private IDisposable pause;
        private object stageOwner;
        private Action<NarrativeEventRequest> constructionStarted;
        private NarrativeConversationPlayback constructionPlayback;
        public bool IsPlaying => !disposed && presenter != null && presenter.IsPresenting;
        public string EventId { get; private set; }
        public string Source => DialoguePanel.Active?.CurrentSource?.SourceLineId ?? "信件 / 顺序步骤";

        public void PlayRevolutionConstruction(string eventId, int wisdom = 5, int perception = 6,
            int courage = 6, bool historyFixture = false, bool revolution13Completed = false,
            bool reciprocityFixture = false, Action completed = null)
        {
            constructionSeed = debug =>
            {
                debug.SetDate(eventId == "revolution.1013.s2" ? 8 : eventId == "revolution.1014.s1" ? 9 : 23);
                debug.SetCondition("story.oct07.s2.completed", true);
                if (revolution13Completed) debug.SetCondition("story.revolution.1013.s2.completed", true);
                debug.SetAbility("wisdom", wisdom); debug.SetAbility("perception", perception);
                debug.SetAbility("courage", courage);
            };
            constructionCondition = key => key == "dependency:C-B06.history-specialization" ? historyFixture
                : key == "dependency:C-B03.C-B04.C-B05.reciprocity" ? reciprocityFixture
                : throw new InvalidOperationException("Unknown construction fixture: " + key);
            Play(eventId, perception, null, completed, true);
        }
        private Action<NarrativeDebugSession> constructionSeed;
        private Func<string, bool> constructionCondition;

        public void PlayNeutral1008Construction(int language = 1, Action completed = null)
        {
            constructionSeed = debug => { debug.SetDate(3); debug.SetAbility("language", language); };
            Play("neutral.1008.s1", 2, null, completed, true);
        }

        public void PlayNeutralConstruction(string eventId, Action completed = null)
            => PlayNeutralConstructionSample(eventId, 3, 3, null, null, null, null, completed);

        /// <summary>Text-only isolated samples; formal results/waits stay untouched in the source configuration.</summary>
        public void PlayNeutralConstructionSample(string eventId, int language, int wisdom,
            bool? antoineApproval, bool? accountsPass, bool? qualityPass, bool? advertisementAffordable,
            Action completed = null)
        {
            if (eventId != "neutral.1010.s1" && eventId != "neutral.1010.s2" && eventId != "neutral.1013.s2" && eventId != "neutral.1018.s1")
                throw new ArgumentException("Unsupported neutral construction event: " + eventId, nameof(eventId));
            if (eventId == "neutral.1013.s2" && !antoineApproval.HasValue)
                throw new InvalidOperationException("A04 preview requires an explicit approval sample.");
            if (eventId == "neutral.1018.s1" && !qualityPass.HasValue)
                throw new InvalidOperationException("A05 preview requires an explicit quality sample.");
            constructionSeed = debug =>
            {
                debug.SetDate(eventId == "neutral.1010.s1" || eventId == "neutral.1010.s2" ? 5 : eventId == "neutral.1013.s2" ? 8 : 13);
                debug.SetAbility("language", language);
                debug.SetAbility("wisdom", wisdom);
                if (eventId == "neutral.1013.s2" && antoineApproval == true)
                    debug.SetCondition("story.neutral.1010.s2.antoine_approval", true);
                if (eventId == "neutral.1018.s1")
                    debug.SetBar("cafe.money", advertisementAffordable == true ? int.MaxValue : 0);
            };
            constructionCondition = key =>
            {
                if (eventId == "neutral.1013.s2" && key == "dependency:A04.antoine_approval") return antoineApproval.Value;
                if (eventId == "neutral.1013.s2" && key == "dependency:A04.q2B.wisdom-pass")
                    return accountsPass ?? throw new InvalidOperationException("A04 q2B requires an explicit unresolved wisdom result sample.");
                if (eventId == "neutral.1018.s1" && key == "service:passed") return qualityPass.Value;
                if (eventId == "neutral.1018.s1" && key == "dependency:A05.quality.pass") return qualityPass.Value;
                if (eventId == "neutral.1018.s1" && key == "dependency:A05.quality.fail") return !qualityPass.Value;
                if (eventId == "neutral.1018.s1" && key == "dependency:A05.advertisement.fee-and-balance")
                    return advertisementAffordable ?? throw new InvalidOperationException("A05 advertisement requires an explicit affordability sample.");
                throw new InvalidOperationException("Unknown neutral construction dependency: " + key);
            };
            Play(eventId, 2, null, completed, true);
        }

        public void PlayRose1009Construction(Action completed = null)
        {
            ConfigureRose1009Stage(false);
            Play("rose.1009.s1", 2, null, completed, true);
        }

        public void Play(string eventId, int perception = 2, string startPageId = null, Action completed = null, bool constructionPreview = false)
        {
            if (disposed || session != null) throw new InvalidOperationException("Create a fresh chapter preview for each run.");
            var runtime = NarrativeRuntime.Instance;
            if (runtime != null && runtime.IsReady && runtime.Controller.IsBusy || DialoguePanel.IsAnyPlaying)
                throw new InvalidOperationException("请先结束正式演出，或在共享演出场景预览。");
            var config = LubanNarrativeLoader.Load();
            var root = config.events.Single(e => e.eventId == eventId);
            if (constructionPreview)
            {
                bool revolution = new[] { "revolution.1013.s2", "revolution.1028.s1", "revolution.1014.s1" }.Contains(eventId) && constructionSeed != null;
                bool churchLong = eventId == "church.1104.s1" && constructionSeed != null && constructionCondition != null;
                bool churchRaid = eventId == "church.1020.s1" && constructionSeed != null;
                bool churchGuest = eventId == "church.1014.s2" && constructionSeed != null;
                bool neutral = new[] { "neutral.1008.s1", "neutral.1010.s1", "neutral.1010.s2", "neutral.1013.s2", "neutral.1018.s1" }.Contains(eventId) && constructionSeed != null;
                bool rose = eventId == "rose.1009.s1"
                    || (eventId == "rose.1016.s1" || eventId == "rose.1011.s1") && constructionSeed != null
                    || eventId == "rose.1012.s1" && constructionSeed != null && constructionCondition != null;
                if ((!revolution && !churchLong && !churchRaid && !churchGuest && !neutral && !rose) || !neutral && (config.results.Any(r => r.eventId == eventId)
                    || !root.eventProperties.Any(p => p.property == "blockedBy")))
                    throw new InvalidOperationException("Construction preview requires a supported blocked, result-free chapter and explicit fixtures.");
                // This configuration belongs only to the new in-memory debug session.
                // Blocked chapters remain blocked in the source; formal neutral roots are stripped only in this copy.
                root.eventProperties.RemoveAll(p => p.property == "blockedBy");
                // These blocked fragments deliberately have no formal EventDate registration.
                // Start only their isolated copy directly, rather than accepting an empty phase batch.
                if (churchLong || churchRaid || churchGuest) root.entry = NarrativeEntryPoint.Immediate;
                if (neutral) PrepareNeutralTextPreview(config, root);
            }
            EventId = eventId;
            pause = PresentationPauseRegistry.Acquire("Isolated chapter preview");
            try
            {
                session = new NarrativeDebugSession(config);
                session.SetDate(constructionPreview ? 4 : 2); session.SetAbility("perception", perception);
                if (constructionPreview) session.SetCondition("story.oct09.meeting.known", true);
                session.SetCondition("world.map.active", true);
                if (eventId == "oct07.s2") session.SetCondition("story.oct07.s1.completed", true);
                constructionSeed?.Invoke(session);
                session.EstablishBaseline();
                presenter = new NarrativePresenter(config, session.ResolveText)
                {
                    PreviewStartPageId = startPageId,
                    PreviewIgnoreCheckpoints = constructionPreview,
                    PageCondition = key => ReadNeutralPreviewCondition(key, constructionPreview),
                    ChoiceUnlocked = (id, context) =>
                        (eventId != "neutral.1018.s1" || !constructionPreview || id != "neutral.1018.s1.choice.q_ad.yes"
                            || constructionCondition("dependency:A05.advertisement.fee-and-balance"))
                        && session.Controller.EvaluateConditions("choice", id, context),
                    InlineAbilityCheck = session.EvaluateInlineAbilityCheck,
                    AbilityCheck = session.EvaluateAbilityCheck,
                    CharacterResolver = NarrativeCharacterResolver.Resolve
                };
                presenter.ConversationReadingPlayback = (request, data, done) =>
                {
                    stageOwner = data.continuityOwner;
                    return ConversationReadingPlayback.PlayPreview(request, data, done);
                };
                presenter.ChoiceSelected += session.Controller.SelectChoice;
                presenter.ConversationPlayback = (request, data, done) =>
                {
                    stageOwner = data.continuityOwner;
                    return constructionPlayback != null ? constructionPlayback(request, data, done)
                        : NarrativeConversationEventPlayback.PlayPreview(request, data, done);
                };
                presenter.LetterPlayback = (request, data, done) =>
                {
                    stageOwner = data.continuityOwner;
                    // Starting directly at a letter rebuilds the source background and an empty stage.
                    if (!DialoguePanel.IsAnyPlaying && DialoguePanel.Active?.OwnsContinuity(stageOwner) != true)
                    {
                        var step = config.presentations.conversationPages.Single(p => p.pageId == data.pageId);
                        var background = config.presentations.conversationPages
                            .Where(p => p.presentationKey == root.presentationKey && p.seq <= step.seq && !string.IsNullOrWhiteSpace(p.bgOverride))
                            .OrderBy(p => p.seq).Last().bgOverride;
                        var stage = new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, string.Empty, autoAdvanceSeconds: .02f) },
                            new DialogueCharacter("", null), new DialogueCharacter("", null),
                            continuityOwner: stageOwner, continues: true, backgroundVisual: NarrativeStageVisuals.Resolve(background));
                        data.releaseStage = () => { LetterEventPlayback.ReleaseStage(data.continuityOwner); DialoguePlayback.ReleaseStage(data.continuityOwner); };
                        data.setSuspended = blocked => LetterEventPlayback.SetSuspended(data.continuityOwner, blocked);
                        DialoguePlayback.Play(stage.WithPreviewContext(), () =>
                        {
                            if (!disposed && !LetterEventPlayback.PlayPreview(request, data, done)) data.cancelled?.Invoke();
                        }, data.cancelled);
                        return true;
                    }
                    return LetterEventPlayback.PlayPreview(request, data, done);
                };
                presenter.Cancelled += _ => { Dispose(); };
                if (constructionStarted != null) presenter.Started += constructionStarted;
                session.SetPresenter(presenter);
                session.Controller.EventCompleted += id =>
                {
                    if (id != eventId) return;
                    pause?.Dispose(); pause = null;
                    completed?.Invoke();
                };
                bool accepted = root.entry == NarrativeEntryPoint.MapAction
                    ? session.Controller.QueueMapActionEvent(eventId)
                    : root.entry == NarrativeEntryPoint.Immediate ? session.RequestEvent(eventId)
                    : session.Controller.ProcessEntry(root.entry);
                if (!accepted) throw new InvalidOperationException("预览入口未接受：" + session.Controller.ExplainEvent(eventId));
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session?.Dispose(); presenter?.Dispose();
            if (stageOwner != null) { ConversationReadingPlayback.ReleaseStage(stageOwner); LetterEventPlayback.ReleaseStage(stageOwner); DialoguePlayback.ReleaseStage(stageOwner); }
            pause?.Dispose(); pause = null; session = null; presenter = null;
        }
    }
}
