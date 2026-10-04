using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Narrative
{
    /// <summary>Creates the NCS runtime without requiring a scene or prefab edit.</summary>
    public sealed class NarrativeGameFlowIntegration : MonoBehaviour
    {
        public static NarrativeGameFlowIntegration Instance { get; private set; }
        private NarrativeConfiguration configuration;
        [SerializeField] private string gameplaySceneName = "SampleScene";
        private NarrativeRuntime runtime;
        private NarrativeGameplayHUD hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnLoad()
        {
            if (Instance != null) return;
            GameObject root = new GameObject("NarrativeGameFlowRuntime");
            Instance = root.AddComponent<NarrativeGameFlowIntegration>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureRuntime();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        public NarrativeRuntime Runtime => runtime;
        public NarrativeGameplayHUD GameplayHUD => hud;

        public NarrativeRuntime EnsureRuntime()
        {
            if (runtime != null && runtime.IsReady) return runtime;
            runtime = NarrativeRuntime.Instance;
            if (runtime == null)
            {
                GameObject root = new GameObject("NarrativeRuntime");
                runtime = root.AddComponent<NarrativeRuntime>();
            }
            if (configuration != null) runtime.ResetSession(configuration);
            else runtime.Initialize();
            return runtime;
        }

        public void ResetSession(NarrativeConfiguration newConfiguration = null)
        {
            EnsureRuntime().ResetSession(newConfiguration ?? configuration);
            if (hud != null) hud.Refresh();
        }

        public void SetGameplayVisible(bool visible)
        {
            EnsureRuntime();
            if (hud == null) hud = NarrativeGameplayHUD.Create(runtime);
            hud.SetVisible(visible);
        }

        public void SetGameplayScene(string sceneName) => gameplaySceneName = sceneName;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetGameplayVisible(false);
            if (runtime?.Controller != null)
            {
                var locations = new System.Collections.Generic.List<NarrativeStringValue>();
                foreach (var pair in runtime.Controller.State.NpcLocations) locations.Add(new NarrativeStringValue { key = pair.Key, value = pair.Value });
                runtime.Presenter?.SyncNpcLocations(locations);
            }
        }
    }
}
