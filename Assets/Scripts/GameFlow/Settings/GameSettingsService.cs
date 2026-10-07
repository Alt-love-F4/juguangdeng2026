using System;
using System.Collections.Generic;
using YokiFrame;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public sealed class GameSettingsService
    {
        private const string PreferencesKey = "Spotlight.GameFlow.GameSettings";

        [Serializable]
        private sealed class StoredSettings
        {
            public int version = 1;
            public float masterVolume = 1f;
            public float musicVolume = 1f;
            public float sfxVolume = 1f;
            public int windowMode = (int)FullScreenMode.FullScreenWindow;
            public int resolutionWidth;
            public int resolutionHeight;
        }

        private GameSettings settings;
        private readonly List<ResolutionOption> resolutionOptions;

        public GameSettings Current => settings;
        public IReadOnlyList<ResolutionOption> ResolutionOptions => resolutionOptions;

        public GameSettingsService()
            : this(LoadResolutionOptions())
        {
        }

        public GameSettingsService(IEnumerable<ResolutionOption> options)
        {
            resolutionOptions = NormalizeResolutionOptions(options);
            settings = LoadSettings(resolutionOptions);
        }

        public void Load()
        {
            settings = LoadSettings(resolutionOptions);
        }

        public void Save()
        {
            StoredSettings stored = new StoredSettings
            {
                masterVolume = settings.MasterVolume,
                musicVolume = settings.MusicVolume,
                sfxVolume = settings.SfxVolume,
                windowMode = (int)settings.WindowMode,
                resolutionWidth = settings.ResolutionWidth,
                resolutionHeight = settings.ResolutionHeight
            };
            PlayerPrefs.SetString(PreferencesKey, JsonUtility.ToJson(stored));
            PlayerPrefs.Save();
        }

        public void Apply()
        {
            ApplyAudio();
            ApplyDisplay();
            Save();
        }

        public void SetMasterVolume(float volume)
        {
            settings.MasterVolume = NormalizeVolume(volume);
            ApplyAudio();
            Save();
        }

        public void SetMusicVolume(float volume)
        {
            settings.MusicVolume = NormalizeVolume(volume);
            ApplyAudio();
            Save();
        }

        public void SetSfxVolume(float volume)
        {
            settings.SfxVolume = NormalizeVolume(volume);
            ApplyAudio();
            Save();
        }

        public void SetWindowMode(FullScreenMode mode)
        {
            if (!IsSupportedWindowMode(mode))
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported window mode.");

            settings.WindowMode = mode;
            ApplyDisplay();
            Save();
        }

        public void SetResolution(int width, int height)
        {
            SetResolution(new ResolutionOption(width, height));
        }

        public void SetResolution(ResolutionOption option, bool persist = true)
        {
            if (!ContainsResolution(option))
                throw new ArgumentException("The resolution is not in ResolutionOptions.", nameof(option));

            settings.ResolutionWidth = option.Width;
            settings.ResolutionHeight = option.Height;
            ApplyDisplay();
            if (persist) Save();
        }

        public void RestoreResolution(int width, int height)
        {
            if (!ContainsResolution(new ResolutionOption(width, height)))
                throw new ArgumentException("The resolution is not in ResolutionOptions.");

            settings.ResolutionWidth = width;
            settings.ResolutionHeight = height;
            ApplyDisplay();
            Save();
        }

        public void CommitCurrentDisplaySettings() => Save();

        private void ApplyAudio()
        {
            AudioKit.SetGlobalVolume(settings.MasterVolume);
            AudioKit.SetBusVolume(AudioBus.Music, settings.MusicVolume);
            AudioKit.SetBusVolume(AudioBus.Sfx, settings.SfxVolume);
        }

        private void ApplyDisplay()
        {
            if (settings.ResolutionWidth <= 0 || settings.ResolutionHeight <= 0)
                return;

            Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, settings.WindowMode);
        }

        private bool ContainsResolution(ResolutionOption option)
        {
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                ResolutionOption candidate = resolutionOptions[i];
                if (candidate.Width == option.Width && candidate.Height == option.Height)
                    return true;
            }
            return false;
        }

        private static GameSettings LoadSettings(IReadOnlyList<ResolutionOption> options)
        {
            GameSettings defaults = GameSettings.Defaults;
            if (!PlayerPrefs.HasKey(PreferencesKey))
                return NormalizeSettings(defaults, options);

            try
            {
                StoredSettings stored = JsonUtility.FromJson<StoredSettings>(PlayerPrefs.GetString(PreferencesKey));
                if (stored == null)
                    return NormalizeSettings(defaults, options);

                GameSettings loaded = new GameSettings(
                    stored.masterVolume,
                    stored.musicVolume,
                    stored.sfxVolume,
                    (FullScreenMode)stored.windowMode,
                    stored.resolutionWidth,
                    stored.resolutionHeight);
                return NormalizeSettings(loaded, options);
            }
            catch (Exception)
            {
                return NormalizeSettings(defaults, options);
            }
        }

        private static GameSettings NormalizeSettings(GameSettings value, IReadOnlyList<ResolutionOption> options)
        {
            value.MasterVolume = NormalizeVolume(value.MasterVolume);
            value.MusicVolume = NormalizeVolume(value.MusicVolume);
            value.SfxVolume = NormalizeVolume(value.SfxVolume);
            if (!IsSupportedWindowMode(value.WindowMode))
                value.WindowMode = FullScreenMode.FullScreenWindow;

            if (!ContainsDimensions(options, value.ResolutionWidth, value.ResolutionHeight))
            {
                if (options.Count > 0)
                {
                    value.ResolutionWidth = options[0].Width;
                    value.ResolutionHeight = options[0].Height;
                }
            }
            return value;
        }

        private static List<ResolutionOption> NormalizeResolutionOptions(IEnumerable<ResolutionOption> options)
        {
            var result = new List<ResolutionOption>();
            if (options != null)
            {
                foreach (ResolutionOption option in options)
                {
                    if (option.Width <= 0 || option.Height <= 0 || ContainsDimensions(result, option))
                        continue;
                    result.Add(option);
                }
            }
            return result;
        }

        private static bool ContainsDimensions(List<ResolutionOption> options, ResolutionOption value)
        {
            return ContainsDimensions(options, value.Width, value.Height);
        }

        private static bool ContainsDimensions(IReadOnlyList<ResolutionOption> options, int width, int height)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Width == width && options[i].Height == height)
                    return true;
            }
            return false;
        }

        private static List<ResolutionOption> LoadResolutionOptions()
        {
            Resolution[] supported = Screen.resolutions;
            var result = new List<ResolutionOption>(supported.Length);
            for (int i = 0; i < supported.Length; i++)
            {
                Resolution resolution = supported[i];
                RefreshRate refreshRate = resolution.refreshRateRatio;
                result.Add(new ResolutionOption(
                    resolution.width,
                    resolution.height,
                    refreshRate.numerator,
                    refreshRate.denominator));
            }

            if (result.Count == 0)
            {
                Resolution current = Screen.currentResolution;
                result.Add(new ResolutionOption(
                    current.width,
                    current.height,
                    current.refreshRateRatio.numerator,
                    current.refreshRateRatio.denominator));
            }
            return result;
        }

        private static float NormalizeVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
                return 1f;
            return Mathf.Clamp01(volume);
        }

        private static bool IsSupportedWindowMode(FullScreenMode mode)
        {
            return mode == FullScreenMode.FullScreenWindow
                || mode == FullScreenMode.ExclusiveFullScreen
                || mode == FullScreenMode.Windowed;
        }
    }
}
