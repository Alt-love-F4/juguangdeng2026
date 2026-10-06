using Spotlight.GameFlow;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// Adapts the scene-owned FreeCafe HUD to the shared Yoki/GameFlow lifecycle.
    /// The bridge owns registration only; gameplay remains in FreeCafeRuntime.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class FreeCafeGameFlowBridge : MonoBehaviour
    {
        [SerializeField] private FreeCafeHud hud;
        private bool registered;

        private void Awake()
        {
            if (hud == null)
                hud = GetComponentInChildren<FreeCafeHud>(true);
            Register();
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
            registered = false;
        }

        private void Register()
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
