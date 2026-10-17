using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>只配置立绘和背景，不保存台词。当前所有对话文本在 C# 中定义。</summary>
    [CreateAssetMenu(menuName = "Spotlight/Dialogue Visuals", fileName = "DialogueVisuals")]
    public sealed class DialogueVisualsAsset : ScriptableObject
    {
        [SerializeField] private Sprite leftPortrait;
        [SerializeField] private Sprite rightPortrait;
        [SerializeField] private Sprite centerPortrait;
        [SerializeField] private Texture background;
        [SerializeField] private string backgroundKey;
        public Sprite LeftPortrait => leftPortrait;
        public Sprite RightPortrait => rightPortrait;
        public Sprite CenterPortrait => centerPortrait;
        public Texture Background => background;
        public string BackgroundKey => backgroundKey;
    }
}
