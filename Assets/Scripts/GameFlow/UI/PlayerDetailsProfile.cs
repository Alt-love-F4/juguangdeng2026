using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    public enum AvatarKind { Male = 0, Female = 1 }

    /// <summary>Session-owned player details. Avatar choice has no gameplay/stat effects.</summary>
    [Serializable]
    public sealed class PlayerDetailsProfile
    {
        public const string SaveId = "game.player-details-profile.v1";
        private static readonly PlayerDetailsProfile session = new PlayerDetailsProfile();
        [SerializeField] private AvatarKind avatar;
        [SerializeField] private bool hasSelectedAvatar;
        [SerializeField] private string background = "";
        [SerializeField] private List<string> specializations = new List<string>();
        [SerializeField] private int[] attributes = new int[4];

        public static PlayerDetailsProfile Current
        {
            get { EnsureRegistered(); return session; }
        }

        public AvatarKind Avatar { get => avatar; set => SelectAvatar(value); }
        public bool HasSelectedAvatar => hasSelectedAvatar;
        public string Background { get => background ?? ""; set => background = value ?? ""; }
        public List<string> Specializations => specializations ?? (specializations = new List<string>());
        public int[] Attributes
        {
            get { Normalize(); return attributes; }
            set
            {
                attributes = new int[4];
                if (value != null) Array.Copy(value, attributes, Math.Min(4, value.Length));
                Normalize();
            }
        }

        public void SelectAvatar(AvatarKind kind)
        {
            if (!Enum.IsDefined(typeof(AvatarKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            avatar = kind;
            hasSelectedAvatar = true;
        }

        public void SetAttribute(int index, int value)
        {
            if (index < 0 || index >= 4) throw new ArgumentOutOfRangeException(nameof(index));
            Attributes[index] = Mathf.Clamp(value, 0, 20);
        }

        public static void EnsureRegistered()
        {
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
        }

        public static void ResetForNewSession()
        {
            EnsureRegistered();
            session.CopyFrom(new PlayerDetailsProfile());
            // A previously loaded save must not overwrite a newly started session later.
            GameSaveParticipantRegistry.TakePending(SaveId);
        }

        /// <summary>Called once by the day flow on load. Legacy saves start with empty details.</summary>
        public static void RestorePending()
        {
            EnsureRegistered();
            string json = GameSaveParticipantRegistry.TakePending(SaveId);
            session.CopyFrom(new PlayerDetailsProfile());
            if (!string.IsNullOrEmpty(json)) RestoreJson(json);
        }

        public static string CaptureJson()
        {
            session.Normalize();
            return JsonUtility.ToJson(session);
        }

        public static void RestoreJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            PlayerDetailsProfile saved = JsonUtility.FromJson<PlayerDetailsProfile>(json);
            if (saved != null) session.CopyFrom(saved);
        }

        private void CopyFrom(PlayerDetailsProfile source)
        {
            avatar = source.avatar;
            hasSelectedAvatar = source.hasSelectedAvatar;
            background = source.background;
            specializations = source.specializations == null ? new List<string>() : new List<string>(source.specializations);
            attributes = source.attributes == null ? new int[4] : (int[])source.attributes.Clone();
            Normalize();
        }

        private void Normalize()
        {
            if (!Enum.IsDefined(typeof(AvatarKind), avatar))
            {
                avatar = AvatarKind.Male;
                hasSelectedAvatar = false;
            }
            background = background ?? "";
            if (specializations == null) specializations = new List<string>();
            if (attributes == null || attributes.Length != 4)
            {
                int[] normalized = new int[4];
                if (attributes != null) Array.Copy(attributes, normalized, Math.Min(4, attributes.Length));
                attributes = normalized;
            }
            for (int i = 0; i < 4; i++) attributes[i] = Mathf.Clamp(attributes[i], 0, 20);
        }
    }
}
