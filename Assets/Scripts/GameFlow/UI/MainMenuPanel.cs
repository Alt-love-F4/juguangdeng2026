using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class MainMenuPanel : GameFlowPanel
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button savesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        protected override void Awake()
        {
            base.Awake();
            startButton?.onClick.AddListener(() => Flow?.StartGame());
            savesButton?.onClick.AddListener(() => Flow?.OpenSaves(false));
            settingsButton?.onClick.AddListener(() => Flow?.OpenSettings(false));
            quitButton?.onClick.AddListener(() => Flow?.QuitGame());
        }
    }
}
