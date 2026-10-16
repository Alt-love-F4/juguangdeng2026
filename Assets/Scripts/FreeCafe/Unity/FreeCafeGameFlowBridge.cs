using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// Adapts the scene-owned FreeCafe HUD to the shared Yoki/GameFlow lifecycle.
    /// The bridge owns registration only; gameplay remains in FreeCafeRuntime.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class FreeCafeGameFlowBridge : MonoBehaviour, IGameplayScenePanelRegistration
    {
        [SerializeField] private FreeCafeHud hud;
        private bool registered;

        private void Awake()
        {
            if (hud == null)
                hud = GetComponentInChildren<FreeCafeHud>(true);
            RegisterAsGameplayPanel();
        }

        private void OnDestroy()
        {
            if (!registered)
                return;
            if (GameplayPanelHooks.OpenPanel == OpenPanel)
            {
                GameplayPanelHooks.OpenPanel = null;
                GameplayPanelHooks.PanelType = null;
                GameplayPanelHooks.IsScenePanel = false;
            }
            if (SceneManager.GetActiveScene().name == "Map")
            {
                MapSceneController map = FindFirstObjectByType<MapSceneController>(FindObjectsInactive.Include);
                map?.RegisterAsGameplayPanel();
            }
            registered = false;
        }

        public void RegisterAsGameplayPanel()
        {
            if (hud == null)
            {
                Debug.LogError("FreeCafeGameFlowBridge requires a FreeCafeHud in the scene.", this);
                return;
            }

            GameplayPanelHooks.OpenPanel = OpenPanel;
            GameplayPanelHooks.PanelType = typeof(FreeCafeHud);
            GameplayPanelHooks.IsScenePanel = true;
            registered = true;
        }

        private GameFlowPanel OpenPanel(GameFlowBootstrap flow)
        {
            hud.gameObject.SetActive(true);
            hud.OpenFromFlow(flow);
            return hud;
        }
    }
}
