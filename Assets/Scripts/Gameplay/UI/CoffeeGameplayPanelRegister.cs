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
            // 场景内已放置未激活的 CoffeeGameplayPanel，运行时直接激活，不走 UIKit 动态加载
            GameplayPanelHooks.OpenPanel = flow =>
            {
                CoffeeGameplayController.EnsureExists();
                var panel = Object.FindFirstObjectByType<CoffeeGameplayPanel>(FindObjectsInactive.Include);
                if (panel != null)
                {
                    panel.gameObject.SetActive(true);
                    panel.OpenFromFlow(flow);
                }
                return panel;
            };
            GameplayPanelHooks.PanelType = typeof(CoffeeGameplayPanel);
            GameplayPanelHooks.IsScenePanel = true;
        }
    }
}
