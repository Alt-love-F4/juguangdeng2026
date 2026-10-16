using System;
using UnityEngine;

namespace Spotlight.Narrative
{
    public sealed class NarrativeRuntime : MonoBehaviour
    {
        private NarrativeConfiguration configuration;
        [SerializeField] private bool logBarChanges = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        private NarrativeController controller;
        private NarrativePresenter runtimePresenter;
        private INarrativePresenter injectedPresenter;
        private NarrativeActionController actionController;
        private Func<string, string> textResolver;
        private Func<string> playerNameProvider;

        public static NarrativeRuntime Instance { get; private set; }
        /// <summary>当前 NCS 会话使用的配置快照。演出适配层只读访问，不修改 NCS 数据。</summary>
        public NarrativeConfiguration Configuration => configuration;
        public NarrativeController Controller => controller;
        public NarrativePresenter Presenter => runtimePresenter;
        public NarrativeActionController Actions => actionController;
        public NarrativeMapInventory MapInventory { get; private set; }
        public bool IsReady => controller != null;
        public bool IsPresenting => runtimePresenter != null && runtimePresenter.IsPresenting;
        public bool BlocksGameplayInput => IsPresenting || (controller != null && controller.IsBusy);
        private bool restoringCafeSession;
        public event Action<NarrativeCafeVisitSnapshot> CafeVisitsCommitted;
        public NarrativeCafeVisitSnapshot GetCommittedCafeVisits() => controller?.GetCommittedCafeVisits();
        public bool SubmitCafeVisitFeedback(NarrativeCafeVisitFeedback fact, Action completed = null) => controller != null && controller.SubmitCafeVisitFeedback(fact, completed);
        public bool IsCafeVisitFeedbackComplete(NarrativeCafeVisitFeedback fact) => controller != null && controller.IsCafeVisitFeedbackComplete(fact);
        public bool ResumeServiceCheckpoint() => controller != null && controller.ResumeServiceCheckpoint();
        public event Action TurnCompleted;
        public event Action<NarrativeEventRequest> EventPresented;
        public event Action<NarrativeEventRequest> EventFinished;
        public event Action<NarrativeEventRequest> EventQueued;
        public event Action<string, int, int> BarChanged;
        private NarrativeReadingPlayback readingPlayback;
        private NarrativeLetterPlayback letterPlayback;
        private NarrativeConversationReadingPlayback conversationReadingPlayback;
        private NarrativeNoticePlayback noticePlayback;
        private NarrativeConversationPlayback conversationPlayback;
        private Func<string, string, string, NarrativeCharacterPresentationData> characterResolver;
        private Func<string, (bool ready, int value)> abilityReader;
        /// <summary>Provides the current player name for NCS text placeholders such as {playername}.</summary>
        public Func<string> PlayerNameProvider
        {
            get => playerNameProvider;
            set => playerNameProvider = value;
        }

        public Func<string, (bool ready, int value)> AbilityReader
        {
            get => abilityReader;
            set
            {
                abilityReader = value;
                if (controller != null) controller.AbilityReader = value;
            }
        }
        public NarrativeReadingPlayback ReadingPlayback
        {
            get => readingPlayback;
            set { readingPlayback = value; if (runtimePresenter != null) runtimePresenter.ReadingPlayback = value; }
        }
        public NarrativeConversationReadingPlayback ConversationReadingPlayback
        {
            get => conversationReadingPlayback;
            set { conversationReadingPlayback = value; if (runtimePresenter != null) runtimePresenter.ConversationReadingPlayback = value; }
        }
        public NarrativeLetterPlayback LetterPlayback
        {
            get => letterPlayback;
            set { letterPlayback = value; if (runtimePresenter != null) runtimePresenter.LetterPlayback = value; }
        }
        public NarrativeConversationPlayback ConversationPlayback
        {
            get => conversationPlayback;
            set { conversationPlayback = value; if (runtimePresenter != null) runtimePresenter.ConversationPlayback = value; }
        }
        public NarrativeNoticePlayback NoticePlayback
        {
            get => noticePlayback;
            set { noticePlayback = value; if (runtimePresenter != null) runtimePresenter.NoticePlayback = value; }
        }
        public Func<string, string, string, NarrativeCharacterPresentationData> CharacterResolver
        {
            get => characterResolver;
            set { characterResolver = value; if (runtimePresenter != null) runtimePresenter.CharacterResolver = ResolveCharacterForPresentation; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            Initialize();
        }

        public void Initialize(NarrativeConfiguration overrideConfiguration = null, Func<string, string> resolver = null)
        {
            if (controller != null) return;
            if (overrideConfiguration != null) configuration = overrideConfiguration;
            textResolver = resolver;
            if (configuration == null)
                configuration = LubanNarrativeLoader.Load();
            if (configuration == null)
            {
                Debug.LogError("[NCS] Narrative configuration could not be loaded from Luban tables.", this);
                return;
            }
            if (injectedPresenter == null)
            {
                runtimePresenter = new NarrativePresenter(configuration, ResolveText);
                runtimePresenter.ReadingPlayback = ReadingPlayback;
                runtimePresenter.LetterPlayback = LetterPlayback;
                runtimePresenter.ConversationReadingPlayback = ConversationReadingPlayback;
                runtimePresenter.NoticePlayback = NoticePlayback;
                runtimePresenter.ConversationPlayback = ConversationPlayback;
                runtimePresenter.CharacterResolver = ResolveCharacterForPresentation;
            }
            controller = new NarrativeController(configuration, injectedPresenter ?? runtimePresenter);
            if (runtimePresenter != null) runtimePresenter.PageCondition = controller.ReadPresentationCondition;
            controller.CafeVisitsCommitted += snapshot => CafeVisitsCommitted?.Invoke(snapshot);
            controller.AbilityReader = AbilityReader;
            controller.TextResolver = ResolveText;
            MapInventory = new NarrativeMapInventory();
            actionController = new NarrativeActionController(controller, configuration.actions, 5, MapInventory);
            foreach (var position in configuration.mapPositions)
            {
                if (position == null || !position.enabled) continue;
                if (!string.IsNullOrWhiteSpace(position.defaultAction)) actionController.Mount(position.positionKey, position.defaultAction);
                foreach (string key in position.extendedActions) actionController.Mount(position.positionKey, key);
            }
            if (runtimePresenter != null)
            {
                runtimePresenter.Started += request => EventPresented?.Invoke(request);
                runtimePresenter.Finished += request => EventFinished?.Invoke(request);
            }
            controller.EventQueued += request => EventQueued?.Invoke(request);
            controller.EffectRequested += notification => runtimePresenter?.ExecuteEffect(notification);
            controller.TurnCompleted += () => TurnCompleted?.Invoke();
            if (runtimePresenter != null)
            {
                runtimePresenter.ChoiceSelected += choiceId => controller.SelectChoice(choiceId);
                runtimePresenter.ChoiceUnlocked = (id, context) => controller.EvaluateConditions("choice", id, context);
                runtimePresenter.AbilityCheck = controller.EvaluateAbilityCheckForPresentation;
                runtimePresenter.InlineAbilityCheck = controller.EvaluateInlineAbilityCheckForPresentation;
            }
            controller.BarChanged += (key, oldValue, newValue) =>
            {
                if (logBarChanges) Debug.Log($"[NCS][State] {key}: {oldValue} -> {newValue}", this);
                BarChanged?.Invoke(key, oldValue, newValue);
            };
            if (!restoringCafeSession) CafeVisitsCommitted?.Invoke(controller.GetCommittedCafeVisits());
        }

        public void ResetSession(NarrativeConfiguration overrideConfiguration = null)
        {
            runtimePresenter?.Dispose();
            runtimePresenter = null;
            // Keep the caller-owned presentation dependency when rebuilding session state.
            controller?.UnbindValueProviders();
            controller = null;
            actionController = null;
            if (overrideConfiguration != null) configuration = overrideConfiguration;
            Initialize(configuration, textResolver);
        }

        public void Initialize(NarrativeConfiguration source, INarrativePresenter presenter, Func<string, string> resolver = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            runtimePresenter?.Dispose();
            runtimePresenter = null;
            controller?.UnbindValueProviders();
            controller = null;
            actionController = null;
            configuration = source;
            injectedPresenter = presenter;
            Initialize(source, resolver);
        }

        public void InjectPresenter(INarrativePresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (configuration == null) configuration = LubanNarrativeLoader.Load();
            Initialize(configuration, presenter, textResolver);
        }

        public string ResolveText(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (textResolver != null)
            {
                string resolved = textResolver(key);
                if (!string.IsNullOrEmpty(resolved) && resolved != key) return SubstitutePlayerName(resolved);
            }
            if (configuration != null && configuration.texts != null)
                for (int i = 0; i < configuration.texts.Count; i++)
                    if (configuration.texts[i] != null && configuration.texts[i].key == key)
                        return SubstitutePlayerName(configuration.texts[i].content ?? key);
            return SubstitutePlayerName(key);
        }

        private NarrativeCharacterPresentationData ResolveCharacterForPresentation(string key, string displayName, string portraitPath)
        {
            var resolved = characterResolver?.Invoke(key, displayName, portraitPath)
                ?? new NarrativeCharacterPresentationData { key = key, name = displayName, portraitPath = portraitPath };
            string playerName = key == "player" ? playerNameProvider?.Invoke() : null;
            return new NarrativeCharacterPresentationData { key = resolved.key, name = string.IsNullOrWhiteSpace(playerName) ? resolved.name : playerName,
                portraitPath = resolved.portraitPath, portrait = resolved.portrait, avatar = resolved.avatar };
        }

        private string SubstitutePlayerName(string text)
        {
            if (string.IsNullOrEmpty(text) || playerNameProvider == null) return text;
            string playerName = playerNameProvider();
            if (string.IsNullOrEmpty(playerName)) return text;
            return NarrativeTextSubstitution.PlayerName(text, playerName);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            controller?.UnbindValueProviders();
            runtimePresenter?.Dispose();
        }

        public void BeginTurn() => controller?.BeginTurn();
        public void EndTurn() => controller?.EndTurn();
        public bool QueueEvent(string eventId) => controller != null && controller.QueueEvent(eventId);
        public bool QueueMapActionEvent(string eventId) => controller != null && controller.QueueMapActionEvent(eventId);
        public bool ProcessEntry(NarrativeEntryPoint entry, Action<bool> completed = null) => controller != null && controller.ProcessEntry(entry, completed);
        public bool BlocksSave => controller != null && controller.BlocksSave;
        public bool AddBar(string barKey, int delta) => controller != null && controller.AddBar(barKey, delta);
        public bool SetBar(string barKey, int value) => controller != null && controller.SetBar(barKey, value);
        public bool SelectChoice(string choiceId) => controller != null && controller.SelectChoice(choiceId);
        public NarrativeAbilityCheckSnapshot EvaluateAbilityCheck(string checkId) => controller == null ? throw new InvalidOperationException("Narrative runtime is not ready.") : controller.EvaluateAbilityCheckForPresentation(checkId);
        public bool EvaluateConditions(string ownerType, string ownerId, NarrativeConditionContext context = null) => controller != null && controller.EvaluateConditions(ownerType, ownerId, context);
        public NarrativeSnapshot CaptureSnapshot(bool allowInitialization = false)
        {
            if (actionController != null && !actionController.CanCaptureSnapshot) throw new InvalidOperationException("Cannot save while narrative results are settling.");
            NarrativeSnapshot snapshot = controller?.CaptureSnapshot(allowInitialization);
            if (snapshot != null && actionController != null)
            {
                snapshot.actionState = new NarrativeActionStateSnapshot();
                actionController.CaptureInto(snapshot.actionState);
            }
            return snapshot;
        }
        public void ResumeRestoredSession()
        {
            if (controller == null || controller.IsEnded) return;
            actionController?.ResumePendingExecution();
            controller.ResumePendingEvents();
            controller.CheckEndingsAtSafeBoundary();
        }

        public void StopForGameOver()
        {
            runtimePresenter?.Dispose();
            runtimePresenter = null;
        }

        public void RestoreSnapshot(NarrativeSnapshot snapshot)
        {
            if (snapshot == null) return;
            restoringCafeSession = true;
            try { ResetSession(configuration); }
            finally { restoringCafeSession = false; }
            if (controller == null) throw new InvalidOperationException("Narrative runtime is not initialized.");
            controller.BeginValueRestore();
            controller.RestoreSnapshot(snapshot);
            actionController?.RestoreFrom(snapshot.actionState);
            runtimePresenter?.SyncNpcLocations(snapshot.npcLocations);
            // Formal GameFlow resumes only after day, profile, inventory and actions are restored.
        }
    }
}
