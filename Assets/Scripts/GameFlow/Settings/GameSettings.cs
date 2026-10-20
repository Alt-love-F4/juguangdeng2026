using System;
using UnityEngine;

namespace Spotlight.GameFlow
{
    [Serializable]
    public struct GameSettings
    {
        [Range(0f, 1f)]
        public float MasterVolume;

        [Range(0f, 1f)]
        public float MusicVolume;

        [Range(0f, 1f)]
        public float SfxVolume;

        public FullScreenMode WindowMode;
        public int ResolutionWidth;
        public int ResolutionHeight;

        public GameSettings(float masterVolume, float musicVolume, float sfxVolume,
            FullScreenMode windowMode, int resolutionWidth, int resolutionHeight)
        {
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            WindowMode = windowMode;
            ResolutionWidth = resolutionWidth;
            ResolutionHeight = resolutionHeight;
        }

        private const int DefaultResolutionWidth = 1920;
        private const int DefaultResolutionHeight = 1080;

        public static GameSettings Defaults
        {
            get
            {
                return new GameSettings(
                    1f,
                    1f,
                    1f,
                    FullScreenMode.Windowed,
                    DefaultResolutionWidth,
                    DefaultResolutionHeight);
            }
        }
    }

    [Serializable]
    public readonly struct ResolutionOption : IEquatable<ResolutionOption>
    {
        public readonly int Width;
        public readonly int Height;
        public readonly uint RefreshRateNumerator;
        public readonly uint RefreshRateDenominator;

        public int RefreshRate => RefreshRateDenominator == 0
            ? 0
            : Mathf.RoundToInt((float)RefreshRateNumerator / RefreshRateDenominator);

        public ResolutionOption(int width, int height, int refreshRate = 0)
            : this(width, height, refreshRate > 0 ? (uint)refreshRate : 0u, refreshRate > 0 ? 1u : 0u)
        {
        }

        public ResolutionOption(int width, int height, uint refreshRateNumerator, uint refreshRateDenominator)
        {
            Width = width;
            Height = height;
            RefreshRateNumerator = refreshRateNumerator;
            RefreshRateDenominator = refreshRateDenominator;
        }

        public bool Equals(ResolutionOption other)
        {
            return Width == other.Width && Height == other.Height
                && RefreshRateNumerator == other.RefreshRateNumerator
                && RefreshRateDenominator == other.RefreshRateDenominator;
        }

        public override bool Equals(object obj)
        {
            return obj is ResolutionOption other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width;
                hash = (hash * 397) ^ Height;
                hash = (hash * 397) ^ (int)RefreshRateNumerator;
                return (hash * 397) ^ (int)RefreshRateDenominator;
            }
        }

        public override string ToString()
        {
            return RefreshRate > 0
                ? string.Format("{0}x{1} ({2} Hz)", Width, Height, RefreshRate)
                : string.Format("{0}x{1}", Width, Height);
        }
    }
}
