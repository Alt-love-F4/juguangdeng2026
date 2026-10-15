using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    public sealed class SettingsPanel : GameFlowPanel
    {
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private TMP_Text windowModeText;
        [SerializeField] private TMP_Text resolutionText;
        [SerializeField] private Button windowModeButton;
        [SerializeField] private Button resolutionButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown windowModeDropdown;
        private static readonly FullScreenMode[] WindowModes =
        {
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.Windowed,
            FullScreenMode.FullScreenWindow
        };
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sfxValue;
        private void UpdateVolumeLabels()
        {
            if (masterValue != null) masterValue.text = Mathf.RoundToInt(masterVolume.value * 100f).ToString();
            if (musicValue != null) musicValue.text = Mathf.RoundToInt(musicVolume.value * 100f).ToString();
            if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(sfxVolume.value * 100f).ToString();
        }

        protected override void Awake()
        {
            base.Awake();
            masterVolume?.onValueChanged.AddListener(_ => UpdateVolumeLabels());
            musicVolume?.onValueChanged.AddListener(_ => UpdateVolumeLabels());
            sfxVolume?.onValueChanged.AddListener(_ => UpdateVolumeLabels());
            if (windowModeDropdown != null)
                windowModeDropdown.onValueChanged.AddListener(SelectWindowMode);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(SelectResolution);
            masterVolume?.onValueChanged.AddListener(value => Flow?.SetMasterVolume(value));
            musicVolume?.onValueChanged.AddListener(value => Flow?.SetMusicVolume(value));
            sfxVolume?.onValueChanged.AddListener(value => Flow?.SetSfxVolume(value));
            windowModeButton?.onClick.AddListener(CycleWindowMode);
            resolutionButton?.onClick.AddListener(CycleResolution);
            backButton?.onClick.AddListener(() => Flow?.BackToPrevious());
            Spotlight.Audio.UIButtonAudio.Configure(backButton, string.Empty);
            foreach (Slider slider in new[] { masterVolume, musicVolume, sfxVolume })
                if (slider != null && slider.GetComponent<Spotlight.Audio.AudioSliderPreview>() == null)
                    slider.gameObject.AddComponent<Spotlight.Audio.AudioSliderPreview>();
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            if (windowModeDropdown != null)
            {
                windowModeDropdown.ClearOptions();
                windowModeDropdown.AddOptions(new System.Collections.Generic.List<string>
                    { "全屏", "窗口", "无边框" });
            }
            if (Flow == null) return;
            GameSettings current = Flow.CurrentSettings;
            if (masterVolume != null) masterVolume.SetValueWithoutNotify(current.MasterVolume);
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(current.MusicVolume);
            if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(current.SfxVolume);
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var labels = new System.Collections.Generic.List<string>();
                foreach (var option in Flow.ResolutionOptions) labels.Add(option.Width + "X" + option.Height);
                resolutionDropdown.AddOptions(labels);
            }
            UpdateVolumeLabels();
            RefreshDisplaySettings();
        }

        protected override void OnBlur()
        {
            HideDisplayChoices();
            base.OnBlur();
        }

        protected override void OnWillHide()
        {
            HideDisplayChoices();
            base.OnWillHide();
        }

        protected override void OnClose()
        {
            HideDisplayChoices();
            Flow?.SaveSettings();
            base.OnClose();
        }

        private void HideDisplayChoices()
        {
            if (windowModeDropdown != null) windowModeDropdown.Hide();
            if (resolutionDropdown != null) resolutionDropdown.Hide();
        }

        private void SelectWindowMode(int index)
        {
            if (Flow == null || index < 0 || index >= WindowModes.Length) return;
            FullScreenMode mode = WindowModes[index];
            if (Flow.CurrentSettings.WindowMode != mode) Flow.SetWindowMode(mode);
            RefreshDisplaySettings();
        }

        private void SelectResolution(int index)
        {
            if (Flow == null || index < 0 || index >= Flow.ResolutionOptions.Count) return;
            ResolutionOption option = Flow.ResolutionOptions[index];
            if (option.Width != Flow.CurrentSettings.ResolutionWidth
                || option.Height != Flow.CurrentSettings.ResolutionHeight)
                Flow.SetResolution(option);
            RefreshDisplaySettings();
        }

        private void CycleWindowMode()
        {
            if (Flow == null) return;
            FullScreenMode[] modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
            int current = System.Array.IndexOf(modes, Flow.CurrentSettings.WindowMode);
            Flow.SetWindowMode(modes[(current + 1 + modes.Length) % modes.Length]);
            RefreshDisplaySettings();
        }

        private void CycleResolution()
        {
            if (Flow == null) return;
            var options = Flow.ResolutionOptions;
            if (options.Count == 0) return;
            int current = -1;
            for (int i = 0; i < options.Count; i++)
                if (options[i].Width == Flow.CurrentSettings.ResolutionWidth && options[i].Height == Flow.CurrentSettings.ResolutionHeight)
                { current = i; break; }
            Flow.SetResolution(options[(current + 1 + options.Count) % options.Count]);
            RefreshDisplaySettings();
        }

        public void RefreshDisplaySettings()
        {
            if (Flow == null) return;
            if (windowModeText != null) windowModeText.text = Flow.WindowModeLabel;
            if (windowModeDropdown != null)
            {
                int index = System.Array.IndexOf(WindowModes, Flow.CurrentSettings.WindowMode);
                windowModeDropdown.SetValueWithoutNotify(index >= 0 ? index : 2);
            }
            if (resolutionText != null) resolutionText.text = Flow.ResolutionLabel;
            if (resolutionDropdown != null)
                for (int i = 0; i < Flow.ResolutionOptions.Count; i++)
                    if (Flow.ResolutionOptions[i].Width == Flow.CurrentSettings.ResolutionWidth && Flow.ResolutionOptions[i].Height == Flow.CurrentSettings.ResolutionHeight)
                        { resolutionDropdown.SetValueWithoutNotify(i); break; }
        }
    }
}
