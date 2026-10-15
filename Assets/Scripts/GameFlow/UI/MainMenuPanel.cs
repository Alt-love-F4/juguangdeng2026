using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

        private static bool entrancePlayed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEntranceAudio() => entrancePlayed = false;

        // Title buttons are pointer-only; UIKit must not restore navigation focus.
        public override bool AutoFocusOnShow => false;

        protected override void OnShow()
        {
            if (savesButton != null) savesButton.interactable = true;
            if (noSavesText != null) noSavesText.gameObject.SetActive(false);
            base.OnShow();
            // OnFocus also calls OnShow: play only on the first title display this run.
            if (!entrancePlayed && Spotlight.Audio.GameAudio.Service != null)
            {
                entrancePlayed = true;
                Spotlight.Audio.GameAudio.Play(Spotlight.Audio.AudioKeys.Open);
            }
        }

        protected override void SelectDefaultControl()
        {
            // Clear any previous title selection when reopening/refocusing the menu,
            // without changing the selection owned by a different panel.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
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
