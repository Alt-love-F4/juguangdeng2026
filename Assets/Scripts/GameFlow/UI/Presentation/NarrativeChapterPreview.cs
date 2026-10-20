using System;
using System.Linq;
using Spotlight.Narrative;

namespace Spotlight.GameFlow
{
    /// <summary>Generated chapter playback in a separate memory controller. Never settles the formal session.</summary>
    public sealed class NarrativeChapterPreview : IDisposable
    {
        private NarrativeDebugSession session;
        private NarrativePresenter presenter;
        private bool disposed;
        private IDisposable pause;
        private object stageOwner;
        public bool IsPlaying => !disposed && presenter != null && presenter.IsPresenting;
        public string EventId { get; private set; }
        public string Source => DialoguePanel.Active?.CurrentSource?.SourceLineId ?? "信件 / 顺序步骤";

        public void PlayRose1009Construction(Action completed = null)
            => Play("rose.1009.s1", 2, null, completed, true);

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
                if (eventId != "rose.1009.s1" || config.results.Any(r => r.eventId == eventId)
                    || !root.eventProperties.Any(p => p.property == "blockedBy"))
                    throw new InvalidOperationException("Construction preview requires the blocked, result-free rose 1009 fragment.");
                // This configuration belongs only to the new in-memory debug session.
                // Keep the formal source blocked and stop at its declared P071 boundary.
                root.eventProperties.RemoveAll(p => p.property == "blockedBy");
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
                session.EstablishBaseline();
                presenter = new NarrativePresenter(config, session.ResolveText)
                {
                    PreviewStartPageId = startPageId,
                    PageCondition = session.Controller.ReadPageCondition,
                    ChoiceUnlocked = (id, context) => session.Controller.EvaluateConditions("choice", id, context),
                    InlineAbilityCheck = session.EvaluateInlineAbilityCheck,
                    AbilityCheck = session.EvaluateAbilityCheck,
                    CharacterResolver = NarrativeCharacterResolver.Resolve
                };
                presenter.ChoiceSelected += session.Controller.SelectChoice;
                presenter.ConversationPlayback = (request, data, done) =>
                {
                    stageOwner = data.continuityOwner;
                    return NarrativeConversationEventPlayback.PlayPreview(request, data, done);
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
                            continuityOwner: stageOwner, continues: true, backgroundVisual: NarrativeStageVisuals.Resolve(background, data.pageId));
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
                session.SetPresenter(presenter);
                session.Controller.EventCompleted += id =>
                {
                    if (id != eventId) return;
                    pause?.Dispose(); pause = null;
                    completed?.Invoke();
                };
                bool accepted = root.entry == NarrativeEntryPoint.MapAction
                    ? session.Controller.QueueMapActionEvent(eventId)
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
            if (stageOwner != null) { LetterEventPlayback.ReleaseStage(stageOwner); DialoguePlayback.ReleaseStage(stageOwner); }
            pause?.Dispose(); pause = null; session = null; presenter = null;
        }
    }
}
