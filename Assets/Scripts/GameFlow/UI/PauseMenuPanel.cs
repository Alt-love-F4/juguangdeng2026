using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class PauseMenuPanel : GameFlowPanel
    {
        [UnityEngine.SerializeField] private Button resumeButton;
        [UnityEngine.SerializeField] private Button saveButton;
        [UnityEngine.SerializeField] private Button savesButton;
        [UnityEngine.SerializeField] private Button settingsButton;
        [UnityEngine.SerializeField] private Button titleButton;
        [UnityEngine.SerializeField] private Button exitButton;

        protected override void Awake()
        {
            base.Awake();
            resumeButton?.onClick.AddListener(() => Flow?.ResumeGame());
            saveButton?.onClick.AddListener(() => Flow?.QuickSave());
            savesButton?.onClick.AddListener(() => Flow?.OpenSaves(true));
            settingsButton?.onClick.AddListener(() => Flow?.OpenSettings(true));
            titleButton?.onClick.AddListener(() => Flow?.ConfirmReturnToTitle());
            exitButton?.onClick.AddListener(() => Flow?.ConfirmExit());
        }
    }
}
