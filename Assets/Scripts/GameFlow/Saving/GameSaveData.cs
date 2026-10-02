using System;
using System.Collections.Generic;

namespace Spotlight.GameFlow
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int DataVersion = 1;
        public string SceneName;
        public double ElapsedPlaytimeSeconds;
        public string CheckpointId;
        public string State;
        public List<GameSaveParticipantData> Participants = new List<GameSaveParticipantData>();
    }

    /// <summary>Plain JSON state captured by gameplay code, addressed by a stable ID.</summary>
    [Serializable]
    public sealed class GameSaveParticipantData
    {
        public string Id;
        public string Json;
    }
}
