using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using YokiFrame;

namespace Spotlight.GameFlow
{
    public sealed class MainMenuPanel : GameFlowPanel
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button savesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text noSavesText;
        [SerializeField] private MainMenuMotion motion;

        private static bool entrancePlayed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEntranceAudio() => entrancePlayed = false;

        // Title buttons are pointer-only; UIKit must not restore navigation focus.
        public override bool AutoFocusOnShow => false;

        protected override void OnOpen(IUIData data = null)
        {
            base.OnOpen(data);
            // Open runs while the panel is still inactive only for a fresh entry; returning
            // from Settings/Saves is a Show without Open, so the intro never replays there.
            motion?.RequestIntro();
        }

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
            // 继续游戏/设置先翻页盖住菜单再打开目标面板；开始游戏已有独立的加载转场。
            savesButton?.onClick.AddListener(() => CoverThen(() => Flow?.OpenSaves(false)));
            settingsButton?.onClick.AddListener(() => CoverThen(() => Flow?.OpenSettings(false)));
            quitButton?.onClick.AddListener(() => Flow?.ConfirmQuitFromMenu());
        }

        private void CoverThen(System.Action open)
        {
            if (motion == null) open();
            else motion.CoverThen(open);
        }

        protected override void OnBlur()
        {
            base.OnBlur();
            motion?.SetBlurred(true);
        }

        protected override void OnFocus()
        {
            base.OnFocus();
            motion?.SetBlurred(false);
        }
    }
}
