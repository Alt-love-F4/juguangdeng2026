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
            resolutionDropdown?.onValueChanged.AddListener(index =>
            {
                if (Flow != null && index >= 0 && index < Flow.ResolutionOptions.Count) Flow.SetResolution(Flow.ResolutionOptions[index]);
            });
            masterVolume?.onValueChanged.AddListener(value => Flow?.SetMasterVolume(value));
            musicVolume?.onValueChanged.AddListener(value => Flow?.SetMusicVolume(value));
            sfxVolume?.onValueChanged.AddListener(value => Flow?.SetSfxVolume(value));
            windowModeButton?.onClick.AddListener(CycleWindowMode);
            resolutionButton?.onClick.AddListener(CycleResolution);
            backButton?.onClick.AddListener(() => Flow?.BackToPrevious());
        }

        protected override void OnOpen(YokiFrame.IUIData data = null)
        {
            base.OnOpen(data);
            if (Flow == null) return;
            GameSettings current = Flow.CurrentSettings;
            if (masterVolume != null) masterVolume.SetValueWithoutNotify(current.MasterVolume);
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(current.MusicVolume);
            if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(current.SfxVolume);
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var labels = new System.Collections.Generic.List<string>();
                foreach (var option in Flow.ResolutionOptions) labels.Add(option.Width + " x " + option.Height);
                resolutionDropdown.AddOptions(labels);
            }
            UpdateVolumeLabels();
            RefreshDisplaySettings();
        }

        protected override void OnClose()
        {
            Flow?.SaveSettings();
            base.OnClose();
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
            if (resolutionText != null) resolutionText.text = Flow.ResolutionLabel;
            if (resolutionDropdown != null)
                for (int i = 0; i < Flow.ResolutionOptions.Count; i++)
                    if (Flow.ResolutionOptions[i].Width == Flow.CurrentSettings.ResolutionWidth && Flow.ResolutionOptions[i].Height == Flow.CurrentSettings.ResolutionHeight)
                        { resolutionDropdown.SetValueWithoutNotify(i); break; }
        }
    }
}
