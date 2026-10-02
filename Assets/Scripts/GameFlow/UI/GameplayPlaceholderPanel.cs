using TMPro;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class GameplayPlaceholderPanel : GameFlowPanel
    {
        [UnityEngine.SerializeField] private Button pauseButton;
        [UnityEngine.SerializeField] private Button quickSaveButton;
        [UnityEngine.SerializeField] private TMP_Text infoText;

        protected override void Awake()
        {
            base.Awake();
            pauseButton?.onClick.AddListener(() => Flow?.OpenPause());
            quickSaveButton?.onClick.AddListener(() => Flow?.QuickSave());
        }

        public void Refresh(string sceneName, string elapsedTime)
        {
            if (infoText != null)
                infoText.text = $"当前场景: {sceneName}\n游戏时间: {elapsedTime}";
        }
    }
}
