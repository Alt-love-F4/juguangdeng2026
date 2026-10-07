using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>经营前准备页面；玩家确认后进入开店演出。</summary>
    public sealed class PreBusinessPanel : DayFlowPresentationPanel
    {
        public void ShowIntroductionUnavailable()
            => SetDetail("阿黛尔的引入剧本暂时无法读取。请检查 Luban 配表、生成数据和演出资源，修正后点击继续重试。");

        public void ShowUnavailable()
            => SetDetail("未找到当前玩法模块。请确认当前场景已注册经营入口，然后重新尝试进入准备。");
    }
}
