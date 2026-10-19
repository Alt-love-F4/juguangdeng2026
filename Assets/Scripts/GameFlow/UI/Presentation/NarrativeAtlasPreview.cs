using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>GM-only adapter over the existing NCS presenter and production panel bridges.
    /// A private configuration/state copy permits branch inspection, never formal settlement or cafe orders.</summary>
    public sealed class NarrativeAtlasPreview : IDisposable
    {
        public const string PanelTag = "gm-narrative-atlas-preview";
        private readonly NarrativeConfiguration source;
        private readonly Func<string, string> resolver;
        private readonly HashSet<object> stages = new HashSet<object>();
        private NarrativeDebugSession session;
        private NarrativePresenter presenter;
        private IDisposable pause;
        private bool disposed, pendingFinish, active;
        private bool singlePage;
        public bool IsPlaying => active && !disposed;
        public string Status { get; private set; } = "尚未开始试播";
        public string CurrentPageId { get; private set; }
        public NarrativeController PreviewController => session?.Controller;
        public event Action Finished;

        public NarrativeAtlasPreview(NarrativeConfiguration configuration, Func<string, string> resolver = null)
        {
            source = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.resolver = resolver;
        }

        public static string BlockReason(StoryGraphNode node, NarrativeRuntime runtime = null)
        {
            if (node == null) return "请先选择故事节点。";
            if (node.Kind == StoryNodeKind.Fact) return "条件节点没有可播放的演出。";
            if (node.Blocked) return "建设阻塞：" + node.Blocker;
            if (node.Disabled) return "此节点已在配置中禁用。";
            if (runtime != null && runtime.IsReady && (runtime.Controller.IsBusy || runtime.Controller.PendingEventIds.Count > 0))
                return "正式剧情正在播放或排队；请先结束正式演出。";
            var notice = UIKit.GetPanel<NoticePanel>();
            var letter = UIKit.GetPanel<FirstDayLetterPanel>();
            if (DialoguePanel.IsAnyPlaying
                || notice != null && notice.IsPlaying || letter != null && (letter.IsPlaying || letter.IsGenericLetter))
                return "其他演出正在占用舞台；请先结束或停止该演出。";
            var result = UIKit.GetPanel<ResultPanel>();
            if (result != null && result.State != PanelState.Close && result.State != PanelState.Cached && result.State != PanelState.Preloaded)
                return "请先关闭当前结算/结局面板。";
            return null;
        }

        public void Play(StoryGraphNode node, string pageId = null, NarrativeRuntime runtime = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NarrativeAtlasPreview));
            if (active) throw new InvalidOperationException("请先停止本次试播。");
            string reason = BlockReason(node, runtime);
            if (!string.IsNullOrEmpty(reason)) throw new InvalidOperationException(reason);
            singlePage = !string.IsNullOrWhiteSpace(pageId);
            active = true;
            Status = singlePage ? "单页只读预览：忽略本页可见条件；不结算、不标记已读。" : "独立分支试播：只在内存副本判定选项，不写入正式进度。";
            pause = PresentationPauseRegistry.Acquire(PanelTag);
            try
            {
                Func<string, string> resolve = resolver ?? (key => source.texts.Find(t => t.key == key)?.content ?? key ?? "");
                CurrentPageId = pageId ?? node.Key;
                if (node.Kind == StoryNodeKind.Opening)
                {
                    if (!NarrativePresentationDataAdapter.TryCreateOpeningDialogue(source, resolve, node.Key, out var script, out var error))
                        throw new InvalidOperationException(error);
                    object owner = new object(); stages.Add(owner);
                    DialoguePlayback.Play(script.WithContinuity(owner, false).WithPreviewContext(),
                        () => FinishLater("开场预览完成；正式进度未改变。"),
                        () => FinishLater("已取消开场预览。"), PanelTag, PanelCachePolicy.Transient);
                    return;
                }
                if (node.Kind == StoryNodeKind.Ending)
                {
                    var ending = source.endings.Find(e => e.endingId == node.Key);
                    if (ending == null) throw new InvalidOperationException("结局配置不存在：" + node.Key);
                    var data = NarrativeEndingPresentation.Create(null, ending, resolve,
                        () => FinishLater("结局面板预览完成；没有触发正式结局。"), "返回剧情图谱");
                    UIKit.OpenPanel<ResultPanel>(UILevel.Pop, data, tag: PanelTag, cachePolicy: PanelCachePolicy.Transient);
                    return;
                }
                PlayEvent(node.Key, pageId, runtime);
            }
            catch { Dispose(); throw; }
        }

        private void PlayEvent(string eventId, string pageId, NarrativeRuntime runtime)
        {
            // Deep copy includes dictionaries and all nested rows. Never edit Runtime.Configuration.
            var settings = new JsonSerializerSettings { ContractResolver = new ConfigurationFieldsResolver() };
            var copy = JsonConvert.DeserializeObject<NarrativeConfiguration>(JsonConvert.SerializeObject(source, settings), settings);
            var root = copy.events.Find(e => e.eventId == eventId) ?? throw new InvalidOperationException("剧情不存在：" + eventId);
            // The selected root is explicitly requested for inspection, not a claim that formal gates passed.
            root.entry = NarrativeEntryPoint.Immediate; root.onceOnly = false;
            var index = copy.presentations.index.Find(i => i.presentationKey == root.presentationKey);
            if (index != null) root.presentationClass = index.presentationClass;
            root.conditionOwnerId = PanelTag + ".ungated-root";
            copy.results.Clear(); copy.triggers.Clear(); copy.eventDates.Clear(); copy.endings.Clear();
            if (singlePage) LimitToPage(copy, root, pageId);
            session = new NarrativeDebugSession(copy);
            session.Controller.SetEndingChecksEnabled(false);
            if (runtime != null && runtime.IsReady)
            {
                // State.Capture is a pure copy, unlike pumping/restoring the formal controller.
                var snapshot = runtime.Controller.State.Capture();
                snapshot.flags.RemoveAll(f => f.key == "event.once." + eventId || f.key == "event.locked." + eventId);
                snapshot.happenedEvents.RemoveAll(id => id == eventId);
                session.Controller.RestoreSnapshot(snapshot);
                foreach (var bar in copy.bars.Where(b => b.sourceMode == NarrativeSourceMode.External))
                    if (runtime.Controller.State.TryGetBar(bar.barKey, out int value))
                        session.SetExternalValue(bar.providerId, bar.sourceKey, value);
                foreach (string attribute in new[] { "language", "courage", "wisdom", "perception" })
                    if (runtime.Controller.State.TryGetBar("player." + attribute, out int value)) session.SetAbility(attribute, value);
                string playerName = runtime.PlayerNameProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(playerName)) session.PreviewName = playerName;
            }
            session.EstablishBaseline();
            Func<string, string> resolve = resolver ?? session.ResolveText;
            presenter = new NarrativePresenter(copy, resolve)
            {
                PageCondition = session.Controller.ReadPresentationCondition,
                ChoiceUnlocked = (id, context) => session.Controller.EvaluateConditions("choice", id, context),
                AbilityCheck = session.EvaluateAbilityCheck,
                InlineAbilityCheck = session.EvaluateInlineAbilityCheck,
                CharacterResolver = NarrativeCharacterResolver.Resolve
            };
            presenter.ChoiceSelected += OnChoice;
            presenter.ConversationPlayback = (request, data, done) =>
            {
                CurrentPageId = data.lines.FirstOrDefault()?.pageId;
                stages.Add(data.continuityOwner);
                if (singlePage) foreach (var option in data.options)
                { option.enabled = false; option.requirement = "单页只读展示；选择分支请使用“从头分支试播”。"; }
                return NarrativeConversationEventPlayback.Play(request, data, done, PanelTag, PanelCachePolicy.Transient, true);
            };
            presenter.ReadingPlayback = (request, title, pages, image, dialogue, speaker, portrait, done) =>
            {
                string textKey = request.ReadingLines?.FirstOrDefault()?.textKey;
                CurrentPageId = copy.presentations.readingLines.Find(l => l.textKey == textKey)?.pageId ?? root.presentationKey;
                return ReadingEventPlayback.Play(request, title, pages, image, dialogue, speaker, portrait, done, PanelTag, PanelCachePolicy.Transient, true);
            };
            presenter.NoticePlayback = (request, data, done) =>
            {
                CurrentPageId = data.presentationKey;
                return NarrativeNoticeEventPlayback.Play(request, data, done, PanelTag, PanelCachePolicy.Transient);
            };
            presenter.LetterPlayback = (request, data, done) =>
            {
                CurrentPageId = data.pageId; stages.Add(data.continuityOwner);
                var dialogue = DialoguePanel.Active;
                if (dialogue == null || !dialogue.OwnsContinuity(data.continuityOwner))
                {
                    var step = copy.presentations.conversationPages.Find(p => p.pageId == data.pageId);
                    string background = copy.presentations.conversationPages
                        .Where(p => p.presentationKey == root.presentationKey && p.seq <= step.seq && !string.IsNullOrWhiteSpace(p.bgOverride))
                        .OrderBy(p => p.seq).LastOrDefault()?.bgOverride;
                    var stage = new DialogueScript(new[] { new DialogueLine(DialogueSpeaker.Narrator, string.Empty, autoAdvanceSeconds: .02f) },
                        new DialogueCharacter("", null), new DialogueCharacter("", null), continuityOwner: data.continuityOwner, continues: true,
                        backgroundVisual: NarrativeStageVisuals.Resolve(background, data.pageId));
                    DialoguePlayback.Play(stage.WithPreviewContext(), () =>
                    {
                        if (!disposed && active && !LetterEventPlayback.PlayPreview(request, data, done)) data.cancelled?.Invoke();
                    }, data.cancelled, PanelTag, PanelCachePolicy.Transient);
                    return true;
                }
                return LetterEventPlayback.PlayPreview(request, data, done);
            };
            presenter.GameplayPlayback = (request, data, done) =>
            {
                CurrentPageId = data.pageId;
                Status = "试播停在真实玩法节点：" + data.pageId + "\n" + data.gameplayKey + "\n只读试播不创建咖啡订单，也不伪造成功；实际交付请在正式流程验证。";
                done(new NarrativeGameplayResult { cancelled = true });
                pendingFinish = true;
                return new EmptyLease();
            };
            presenter.Cancelled += OnCancelled;
            session.Controller.EventCompleted += OnEventCompleted;
            session.Controller.EventFailed += OnEventFailed;
            session.SetPresenter(presenter);
            if (!session.RequestEvent(eventId)) throw new InvalidOperationException("独立试播入口未接受：" + session.Controller.ExplainEvent(eventId));
        }

        private static void LimitToPage(NarrativeConfiguration copy, NarrativeEventDefinition root, string pageId)
        {
            if (string.Equals(root.presentationClass, "Conversation", StringComparison.OrdinalIgnoreCase))
            {
                var pages = copy.presentations.conversationPages.Where(p => p.presentationKey == root.presentationKey).OrderBy(p => p.seq).ToList();
                var page = pages.Find(p => p.pageId == pageId) ?? throw new InvalidOperationException("此事件没有选中的对话页：" + pageId);
                // A direct jump still needs the inherited visual from earlier source pages.
                if (string.IsNullOrWhiteSpace(page.bgOverride))
                    page.bgOverride = pages.Where(p => p.seq <= page.seq && !string.IsNullOrWhiteSpace(p.bgOverride)).LastOrDefault()?.bgOverride;
                page.pageCondKeys = page.pageBarConds = page.nextPageId = null;
                copy.presentations.conversationPages.RemoveAll(p => p.presentationKey == root.presentationKey && p.pageId != pageId);
            }
            else if (string.Equals(root.presentationClass, "Reading", StringComparison.OrdinalIgnoreCase))
            {
                var page = copy.presentations.readingPages.Find(p => p.presentationKey == root.presentationKey && p.pageId == pageId)
                    ?? throw new InvalidOperationException("此事件没有选中的阅读页：" + pageId);
                page.pageCondKeys = page.pageBarConds = null;
                copy.presentations.readingPages.RemoveAll(p => p.presentationKey == root.presentationKey && p.pageId != pageId);
            }
            // Notices have one body; their graph content block is the same presentation.
        }
        private bool OnChoice(string id) => session != null && session.SelectChoice(id);
        private void OnEventCompleted(string id) => FinishLater("独立试播完成；未写入正式进度/存档，也未结算奖励。");
        private void OnEventFailed(string id, string error) => FinishLater("试播停止：" + id + "\n" + error);
        private void OnCancelled(string reason)
        {
            if (pendingFinish) return;
            FinishLater("已取消独立试播：" + reason);
        }
        private void FinishLater(string message) { Status = message; pendingFinish = true; }
        public void Tick()
        {
            if (!active || !pendingFinish) return;
            Cleanup(); active = false; pendingFinish = false;
            Finished?.Invoke();
        }
        private void Cleanup()
        {
            if (presenter != null) { presenter.ChoiceSelected -= OnChoice; presenter.Cancelled -= OnCancelled; }
            if (session != null) { session.Controller.EventCompleted -= OnEventCompleted; session.Controller.EventFailed -= OnEventFailed; }
            presenter?.Dispose(); session?.Dispose(); presenter = null; session = null;
            foreach (object owner in stages) if (owner != null) { LetterEventPlayback.ReleaseStage(owner); DialoguePlayback.ReleaseStage(owner); }
            stages.Clear();
            UIKit.ClosePanelsByTag(PanelTag);
            pause?.Dispose(); pause = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; active = false;
            Cleanup();
        }
        // Same field-only DTO boundary as the existing PresentationDebugSession.
        // Computed Unity properties (e.g. Vector2.normalized) must never be walked recursively.
        private sealed class ConfigurationFieldsResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
            {
                var properties = new List<JsonProperty>();
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    var property = base.CreateProperty(field, MemberSerialization.Fields);
                    if (property.Ignored) continue;
                    property.Readable = property.Writable = true;
                    properties.Add(property);
                }
                return properties;
            }
        }
        private sealed class EmptyLease : IDisposable { public void Dispose() { } }
    }
}
