using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Audio
{
    [DisallowMultipleComponent]
    public sealed class GameAudioRuntime : MonoBehaviour
    {
        private AudioListener fallbackListener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var host = new GameObject("GameAudioRuntime");
            DontDestroyOnLoad(host);
            host.AddComponent<GameAudioRuntime>();
        }

        private void Awake()
        {
            GameAudio.Initialize(Resources.Load<AudioCatalog>(AudioCatalog.ResourceName));
            fallbackListener = gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = false;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        // With scene reload disabled, entering Play Mode need not raise sceneLoaded.
        // Use the same application path; SetMusic and UIButtonAudio.Install are
        // idempotent, so the normal sceneLoaded -> Start order does not restart BGM.
        private void Start() => ApplyScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => ApplyScene(scene, mode);

        private void ApplyScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (GameObject root in scene.GetRootGameObjects()) UIButtonAudio.Install(root);
                if (scene.name == "CoffeeShop") GameAudio.Music(AudioKeys.CafeMusic);
                else if (scene.name == "Map" && mode == LoadSceneMode.Single) GameAudio.Music(AudioKeys.CityMusic);
            }
            RefreshListener();
        }

        private void SceneUnloaded(Scene scene) => RefreshListener();

        private void RefreshListener()
        {
            if (fallbackListener == null) return;
            bool existing = false;
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener != fallbackListener && listener.isActiveAndEnabled) { existing = true; break; }
            fallbackListener.enabled = !existing;
        }

        private void Update() => GameAudio.Service?.Tick();

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            GameAudio.Shutdown();
        }
    }
}
