using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Narrative
{
    /// <summary>Creates the NCS runtime without requiring a scene or prefab edit.</summary>
    public sealed class NarrativeGameFlowIntegration : MonoBehaviour
    {
        public static NarrativeGameFlowIntegration Instance { get; private set; }
        public static bool ModuleEnabled { get; private set; } = false;
        private NarrativeConfiguration configuration;
        [SerializeField] private string gameplaySceneName = "SampleScene";
        private NarrativeRuntime runtime;
        private NarrativeGameplayHUD hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnLoad()
        {
            if (!ModuleEnabled || Instance != null) return;
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
            if (Instance != this) return;
            if (hud != null) hud.gameObject.SetActive(false);
            if (runtime != null) runtime.gameObject.SetActive(false);
            Instance = null;
        }

        public static void EnableModule()
        {
            ModuleEnabled = true;
            if (Instance == null) CreateOnLoad();
            else
            {
                if (Instance.runtime != null) Instance.runtime.gameObject.SetActive(true);
                if (Instance.hud != null) Instance.hud.gameObject.SetActive(true);
                Instance.SetGameplayVisible(false);
            }
        }

        public static void DisableModule()
        {
            ModuleEnabled = false;
            if (Instance != null) Destroy(Instance.gameObject);
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
            // Formal gameplay uses its existing map/business panels, never the demo HUD.
            if (hud != null) hud.SetVisible(false);
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
