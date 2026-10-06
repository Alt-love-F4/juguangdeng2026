using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>经营前准备页面；玩家确认后进入开店演出。</summary>
    public sealed class PreBusinessPanel : DayFlowPresentationPanel
    {
        public void ShowUnavailable()
            => SetDetail("未找到当前玩法模块。请确认当前场景已注册经营入口，然后重新尝试进入准备。");
    }
}
