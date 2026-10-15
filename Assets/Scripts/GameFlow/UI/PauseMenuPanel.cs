using UnityEngine.UI;
using YokiFrame;

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

        [UnityEngine.SerializeField] private TMPro.TMP_Text saveStatus;
        public void SetSaveStatus(string message) { if (saveStatus != null) saveStatus.text = message; }

        protected override void OnOpen(IUIData data = null)
        {
            base.OnOpen(data);
            bool canSave = !(data is GameFlowPanelData panelData) || panelData.CanSave;
            if (saveButton != null) saveButton.interactable = canSave;
            if (savesButton != null) savesButton.interactable = canSave;
            SetSaveStatus(canSave ? string.Empty : "教学期间不可存档");
        }

        protected override void Awake()
        {
            base.Awake();
            Spotlight.Audio.UIButtonAudio.Configure(resumeButton, string.Empty);
            resumeButton?.onClick.AddListener(() => Flow?.ResumeGame());
            saveButton?.onClick.AddListener(() => Flow?.QuickSave());
            savesButton?.onClick.AddListener(() => Flow?.OpenSaves(true));
            settingsButton?.onClick.AddListener(() => Flow?.OpenSettings(true));
            titleButton?.onClick.AddListener(() => Flow?.ConfirmReturnToTitle());
            exitButton?.onClick.AddListener(() => Flow?.ConfirmExit());
        }
    }
}
