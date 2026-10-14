using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public enum AvatarKind { Male = 0, Female = 1 }

    /// <summary>Legacy v1 migration DTO only. No registration, gameplay mutation or session singleton.</summary>
    [Serializable]
    public sealed class PlayerDetailsProfile
    {
        public const string SaveId = "game.player-details-profile.v1";

        [SerializeField] private AvatarKind avatar;
        [SerializeField] private bool hasSelectedAvatar;
        [SerializeField] private string background = "";
        [SerializeField] private List<string> specializations = new List<string>();
        [SerializeField] private int[] attributes = new int[4];
        [SerializeField] private string playerName = "";
        [SerializeField] private string origin = "";
        [SerializeField] private int[] letterBonuses = new int[4];
        public string PlayerName => playerName ?? "";
        public string Origin => origin ?? "";

        public static PlayerDetailsProfile ParseLegacyJson(string json) => JsonUtility.FromJson<PlayerDetailsProfile>(json);

        public AvatarKind Avatar => avatar;
        public bool HasSelectedAvatar => hasSelectedAvatar;
        public string Background => background ?? "";
        public int[] LegacyAttributes => attributes == null ? null : (int[])attributes.Clone();
        public IReadOnlyList<string> LegacySpecializations => specializations?.AsReadOnly();
    }
}
