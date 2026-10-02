using TMPro;
using UnityEngine;

namespace Spotlight.GameFlow
{
    [CreateAssetMenu(menuName = "Spotlight/Game Flow Configuration")]
    public sealed class GameFlowConfig : ScriptableObject
    {
        public string MainMenuScene = "SampleScene";
        public string GameplayScene = "SampleScene";
        public TMP_FontAsset Font;
        public Texture2D MenuArtwork;
        [Min(0f)] public float MinimumLoadingSeconds = 1f;
        [Min(1f)] public float AutoSaveIntervalSeconds = 60f;
        public string[] LoadingTips =
        {
            "按 Esc 可以暂停游戏。",
            "自动存档位只可读取，不可手动覆盖或删除。",
            "保存游戏时会优先选择最靠前的空存档位。",
            "加载时可使用左右方向键查看上一条或下一条提示。",
            "可以在设置中分别调节音乐和音效音量。"
        };
    }
}
