using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Spotlight.Narrative;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// Scene-owned preview using the production presenter and, for sequences, an isolated production
    /// controller. All branch/result state belongs to this session, never the live game or a save.
    /// </summary>
    public sealed class PresentationDebugSession : IDisposable
    {
        private readonly NarrativeConfiguration configuration;
        private readonly NarrativeController conditions;
        private readonly NarrativePresenter presenter;
        private readonly Action<UIPanel> panelChanged;
        private readonly Dictionary<string, NarrativeAbilityCheckSnapshot> checks = new Dictionary<string, NarrativeAbilityCheckSnapshot>();
        private readonly int attributeValue;
        private readonly string playerName;
        private readonly bool followEventChain;
        private readonly string tag;
        private bool disposed, playing, failed;
        public bool IsPresenting => playing || presenter.IsPresenting;
        public string CurrentEventId { get; private set; }
        public event Action<string> EventStarted;

        public PresentationDebugSession(NarrativeConfiguration configuration, string tag, bool showAllPages,
            bool unlockChoices, int attributeValue, string playerName, Action<UIPanel> panelChanged, Action<string> cancelled, bool followEventChain = false)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.followEventChain = followEventChain;
            this.tag = tag;
            // Deep-copy generated content before replacing providers/preview conditions. Never mutate
            // cached Luban data, the live runtime's configuration or its state.
            if (followEventChain)
            {
                var settings = new JsonSerializerSettings { ContractResolver = new ConfigurationFieldsResolver() };
                configuration = JsonConvert.DeserializeObject<NarrativeConfiguration>(JsonConvert.SerializeObject(configuration, settings), settings);
                foreach (var bar in configuration.bars)
                    if (bar != null && bar.sourceMode == NarrativeSourceMode.External) bar.sourceMode = NarrativeSourceMode.Ncs;
                if (unlockChoices) configuration.conditions.RemoveAll(condition => condition != null && condition.ownerType == "choice");
            }
            this.configuration = configuration;
            this.attributeValue = attributeValue;
            this.playerName = playerName;
            this.panelChanged = panelChanged;
            presenter = new NarrativePresenter(configuration, Resolve);
            conditions = new NarrativeController(configuration, followEventChain ? presenter : null);
            if (followEventChain)
            {
                conditions.AbilityReader = _ => (true, attributeValue);
                conditions.TextResolver = Resolve;
                conditions.Inventory = new NarrativeMapInventory();
                conditions.MoneyAccount = new PreviewMoneyAccount(conditions);
                conditions.EffectRequested += effect =>
                {
                    // Never forward NPC/world effects to production services or scene actors.
                    if (effect.Result?.resultType == NarrativeResultType.PlaySetup || effect.Result?.resultType == NarrativeResultType.BarHint)
                        presenter.ExecuteEffect(effect);
                };
                conditions.EventFailed += (id, reason) =>
                {
                    if (disposed || failed) return;
                    failed = true;
                    playing = false;
                    cancelled?.Invoke(id + ": " + reason);
                };
            }
            presenter.Started += request =>
            {
                CurrentEventId = request.Event.eventId;
                EventStarted?.Invoke(CurrentEventId);
            };
            // Read-only initial values replace gameplay-owned providers; no game service is created.
            foreach (var group in configuration.bars.Where(x => x.sourceMode == NarrativeSourceMode.External).GroupBy(x => x.providerId))
            {
                var values = group.ToDictionary(x => x.sourceKey, x => x.initValue, StringComparer.Ordinal);
                conditions.BindValueProvider(new DelegateNarrativeValueProvider(group.Key,
                    key => values.TryGetValue(key, out int value) ? value : (int?)null));
            }
            conditions.CompleteValueRestore(false);
            presenter.CharacterResolver = NarrativeCharacterResolver.Resolve;
            presenter.PageCondition = key => showAllPages || conditions.ReadPresentationCondition(key);
            presenter.ChoiceUnlocked = (id, context) => unlockChoices || conditions.EvaluateConditions("choice", id, context);
            // Sequence choices go through the isolated formal controller; fragment choices remain UI-only.
            presenter.ChoiceSelected += id => !followEventChain || conditions.SelectChoice(id);
            presenter.AbilityCheck = followEventChain ? conditions.EvaluateAbilityCheckForPresentation : Check;
            presenter.Cancelled += reason => { if (!disposed && !failed) { failed = true; playing = false; cancelled?.Invoke(reason); } };
            presenter.ConversationPlayback = (request, data, done) =>
            {
                bool handled = NarrativeConversationEventPlayback.Play(request, data, done, tag, PanelCachePolicy.Transient,
                    isPreview: true);
                if (handled) panelChanged?.Invoke(UIKit.GetPanel<DialoguePanel>());
                return handled;
            };
            presenter.ReadingPlayback = (request, title, pages, image, dialogue, speaker, portrait, done) =>
            {
                bool handled = ReadingEventPlayback.Play(request, title, pages, image, dialogue, speaker, portrait, done, tag, PanelCachePolicy.Transient, true);
                if (handled) panelChanged?.Invoke(UIKit.GetPanel<DialoguePanel>());
                return handled;
            };
            presenter.NoticePlayback = (request, data, done) =>
            {
                bool handled = NarrativeNoticeEventPlayback.Play(request, data, done, tag, PanelCachePolicy.Transient);
                if (handled) panelChanged?.Invoke(UIKit.GetPanel<NoticePanel>());
                return handled;
            };
        }

        public void Play(string id, Action completed)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PresentationDebugSession));
            if (playing || presenter.IsPresenting) throw new InvalidOperationException("A debug presentation is already playing.");
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A presentation id is required.", nameof(id));
            playing = true;
            failed = false;
            void Complete()
            {
                if (disposed || failed || !playing) return;
                playing = false;
                completed?.Invoke();
            }
            if (id.StartsWith("sequence:", StringComparison.Ordinal))
            {
                if (!followEventChain) throw new InvalidOperationException("A continuous preview requires an isolated narrative controller.");
                string key = id.Substring("sequence:".Length);
                if (!conditions.QueueEvent(key))
                {
                    playing = false;
                    throw new InvalidOperationException("无法开始连续演出：" + key + "。入口可能不存在或初始条件不满足。");
                }
                // This waits for all queued events AND result-driven PlaySetup effects, not just
                // the first panel's completion. Choices and checks use the real controller above.
                conditions.RunAtSafeBoundary(Complete);
                return;
            }
            if (id.StartsWith("opening:", StringComparison.Ordinal))
            {
                string key = id.Substring("opening:".Length);
                if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Missing opening id.");
                if (!NarrativePresentationDataAdapter.TryCreateOpeningDialogue(configuration, Resolve, key, out var script, out var error))
                    throw new InvalidOperationException(error);
                panelChanged?.Invoke(DialoguePlayback.Play(script.WithPreviewContext(), Complete, Complete,
                    tag, PanelCachePolicy.Transient));
                return;
            }
            var definition = PresentationDebugCatalog.Event(configuration, id);
            presenter.Play(new NarrativeEventRequest(definition, null, 0, context: new NarrativeConditionContext
            { ActorId = definition.actorId, TargetId = definition.targetId }), Complete);
        }

        private string Resolve(string key)
        {
            string text = PresentationDebugCatalog.Text(configuration, key);
            if (string.IsNullOrEmpty(playerName)) return text;
            return text.Replace("{playername}", playerName, StringComparison.OrdinalIgnoreCase)
                .Replace("｛playername}", playerName, StringComparison.OrdinalIgnoreCase)
                .Replace("｛playername｝", playerName, StringComparison.OrdinalIgnoreCase);
        }

        private NarrativeAbilityCheckSnapshot Check(string id)
        {
            if (checks.TryGetValue(id, out var value)) return value;
            var definition = configuration.abilityChecks.Find(x => x != null && x.checkId == id);
            value = NarrativeAbilityCheckEvaluator.Evaluate(definition,
                configuration.abilityCheckConditions.FindAll(x => x != null && x.checkId == id),
                _ => (true, attributeValue));
            checks.Add(id, value);
            return value;
        }

        private sealed class ConfigurationFieldsResolver : DefaultContractResolver
        {
            // Generated configuration is a field-based DTO graph. Do not serialize computed Unity
            // properties such as Vector2.normalized (which recursively returns another Vector2).
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
            {
                var properties = new List<JsonProperty>();
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                {
                    var property = base.CreateProperty(field, MemberSerialization.Fields);
                    if (property.Ignored) continue;
                    property.Readable = true;
                    property.Writable = true;
                    properties.Add(property);
                }
                return properties;
            }
        }

        private sealed class PreviewMoneyAccount : INarrativeMoneyAccount
        {
            private readonly NarrativeController owner;
            private int balance;
            public PreviewMoneyAccount(NarrativeController owner)
            {
                this.owner = owner;
                owner.State.TryGetBar("cafe.money", out balance);
            }
            public bool TryRead(out int value) { value = balance; return true; }
            public bool TryCommit(int expectedBalance, int finalBalance, out string error)
            {
                error = null;
                if (balance != expectedBalance || finalBalance < 0) { error = "Invalid preview balance."; return false; }
                balance = finalBalance;
                owner.SetBar("cafe.money", balance);
                return true;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            playing = false;
            presenter.Dispose();
            conditions.UnbindValueProviders();
            conditions.Inventory = null;
            conditions.MoneyAccount = null;
            EventStarted = null;
            checks.Clear();
        }
    }
}
