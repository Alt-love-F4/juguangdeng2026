using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Spotlight.GameFlow
{
    public sealed class MainMenuPanel : GameFlowPanel
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button savesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text noSavesText;

        protected override void OnShow()
        {
            if (savesButton != null) savesButton.interactable = true;
            if (noSavesText != null) noSavesText.gameObject.SetActive(false);
            SetDefaultSelectable(startButton);
            base.OnShow();
        }

        protected override void Awake()
        {
            base.Awake();
            startButton?.onClick.AddListener(() => Flow?.StartGame());
            savesButton?.onClick.AddListener(() => Flow?.OpenSaves(false));
            settingsButton?.onClick.AddListener(() => Flow?.OpenSettings(false));
            quitButton?.onClick.AddListener(() => Flow?.ConfirmQuitFromMenu());
        }
    }
}
