using Spotlight.GameFlow;
using UnityEngine;
using YokiFrame;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// 把玩法面板注册到 GameFlowBootstrap，替代占位面板。
    /// </summary>
    public static class CoffeeGameplayPanelRegister
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            // The legacy gameplay panel is intentionally not registered. CoffeeShop and
            // CoffeeUIEntitySandbox are owned exclusively by the FreeCafe runtime now.
            GameplayPanelHooks.OpenPanel = null;
            GameplayPanelHooks.PanelType = null;
            GameplayPanelHooks.IsScenePanel = false;
        }
    }
}
