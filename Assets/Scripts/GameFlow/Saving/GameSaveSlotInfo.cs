using System;

namespace Spotlight.GameFlow
{
    public enum GameSaveSlotState
    {
        Empty,
        Occupied,
        Corrupt,
        Incompatible,
        Unavailable
    }

    /// <summary>Immutable slot snapshot. Unreadable slots are never treated as empty.</summary>
    public sealed class GameSaveSlotInfo
    {
        public int Index { get; }
        public bool IsAutoSave => Index == GameSaveService.AutoSaveSlot;
        public GameSaveSlotState State { get; }
        public bool IsOccupied => State != GameSaveSlotState.Empty;
        public bool CanLoad => State == GameSaveSlotState.Occupied;
        public DateTimeOffset? SavedAtUtc { get; }
        public string SceneName { get; }
        public double ElapsedPlaytimeSeconds { get; }
        public string Error { get; }

        internal GameSaveSlotInfo(int index, GameSaveSlotState state, string error = null,
            DateTimeOffset? savedAtUtc = null, string sceneName = null, double elapsedPlaytimeSeconds = 0)
        {
            Index = index;
            State = state;
            Error = error;
            SavedAtUtc = savedAtUtc;
            SceneName = sceneName;
            ElapsedPlaytimeSeconds = elapsedPlaytimeSeconds;
        }
    }
}
