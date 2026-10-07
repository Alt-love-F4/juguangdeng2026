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
        private Func<string, string> textResolver;

        public static NarrativeRuntime Instance { get; private set; }
        public NarrativeController Controller => controller;
        public NarrativePresenter Presenter => runtimePresenter;
        public bool IsReady => controller != null;
        public bool IsPresenting => runtimePresenter != null && runtimePresenter.IsPresenting;
        public bool BlocksGameplayInput => IsPresenting || (controller != null && controller.IsBusy);
        public event Action TurnCompleted;
        public event Action<NarrativeEventRequest> EventPresented;
        public event Action<NarrativeEventRequest> EventFinished;
        public event Action<NarrativeEventRequest> EventQueued;
        public event Action<string, int, int> BarChanged;

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
                runtimePresenter = new NarrativePresenter(configuration, ResolveText);
            controller = new NarrativeController(configuration, injectedPresenter ?? runtimePresenter);
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
            }
            controller.BarChanged += (key, oldValue, newValue) =>
            {
                if (logBarChanges) Debug.Log($"[NCS][State] {key}: {oldValue} -> {newValue}", this);
                BarChanged?.Invoke(key, oldValue, newValue);
            };
        }

        public void ResetSession(NarrativeConfiguration overrideConfiguration = null)
        {
            runtimePresenter?.Dispose();
            runtimePresenter = null;
            injectedPresenter = null;
            controller = null;
            if (overrideConfiguration != null) configuration = overrideConfiguration;
            Initialize(configuration, textResolver);
        }

        public void Initialize(NarrativeConfiguration source, INarrativePresenter presenter, Func<string, string> resolver = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            runtimePresenter?.Dispose();
            runtimePresenter = null;
            controller = null;
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
                if (!string.IsNullOrEmpty(resolved) && resolved != key) return resolved;
            }
            if (configuration != null && configuration.texts != null)
                for (int i = 0; i < configuration.texts.Count; i++)
                    if (configuration.texts[i] != null && configuration.texts[i].key == key)
                        return configuration.texts[i].content ?? key;
            return key;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            runtimePresenter?.Dispose();
        }

        public void BeginTurn() => controller?.BeginTurn();
        public void EndTurn() => controller?.EndTurn();
        public bool QueueEvent(string eventId) => controller != null && controller.QueueEvent(eventId);
        public bool AddBar(string barKey, int delta) => controller != null && controller.AddBar(barKey, delta);
        public bool SetBar(string barKey, int value) => controller != null && controller.SetBar(barKey, value);
        public bool SelectChoice(string choiceId) => controller != null && controller.SelectChoice(choiceId);
        public bool EvaluateConditions(string ownerType, string ownerId, NarrativeConditionContext context = null) => controller != null && controller.EvaluateConditions(ownerType, ownerId, context);
        public NarrativeSnapshot CaptureSnapshot() => controller?.CaptureSnapshot();
        public void RestoreSnapshot(NarrativeSnapshot snapshot)
        {
            if (snapshot == null) return;
            ResetSession(configuration);
            if (controller == null) throw new InvalidOperationException("Narrative runtime is not initialized.");
            controller.RestoreSnapshot(snapshot);
            runtimePresenter?.SyncNpcLocations(snapshot.npcLocations);
            if (snapshot.turnEnding || snapshot.active != null || (snapshot.pending != null && snapshot.pending.Count > 0) || (snapshot.pendingEffects != null && snapshot.pendingEffects.Count > 0))
                controller.EndTurn();
        }
    }
}
