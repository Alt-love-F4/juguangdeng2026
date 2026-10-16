using System;

namespace Spotlight.GameFlow
{
    public interface IGameplayScenePanelRegistration
    {
        void RegisterAsGameplayPanel();
    }

    /// <summary>
    /// 玩法面板打开钩子：玩法程序集注册后，GameFlowBootstrap 用其替代占位面板。
    /// </summary>
    public static class GameplayPanelHooks
    {
        public static Func<GameFlowBootstrap, GameFlowPanel> OpenPanel;
        public static Type PanelType;
        /// <summary>场景内面板返回 true：此时面板不经 UIKit 管理，需要手动处理 Esc/关闭。</summary>
        public static bool IsScenePanel;
    }
}
