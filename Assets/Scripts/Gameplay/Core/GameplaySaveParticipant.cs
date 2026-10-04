using System;
using UnityEngine;

namespace Spotlight.Gameplay
{
    /// <summary>
    /// 玩法存档参与者，通过静态注册表接入 GameFlow 存档系统。
    /// </summary>
    public static class GameplaySaveParticipant
    {
        public const string ParticipantId = "gameplay";

        public static Func<string> Capture;
        public static Action<string> Restore;
        public static Action Reset;

        public static void Register(Func<string> capture, Action<string> restore, Action reset)
        {
            Capture = capture;
            Restore = restore;
            Reset = reset;
        }

        public static void Unregister()
        {
            Capture = null;
            Restore = null;
            Reset = null;
        }

        public static string CaptureJson()
        {
            if (Capture == null) return null;
            try { return Capture(); }
            catch (Exception e) { Debug.LogError("[GameplaySave] Capture failed: " + e.Message); return null; }
        }

        public static void RestoreJson(string json)
        {
            if (string.IsNullOrEmpty(json)) { Reset?.Invoke(); return; }
            try { Restore?.Invoke(json); }
            catch (Exception e) { Debug.LogError("[GameplaySave] Restore failed: " + e.Message); }
        }
    }
}
