using System;
using Spotlight.FreeCafe;

namespace Spotlight.GameFlow
{
    // The scene adapter supplies the domain owner without a UI -> FreeCafe.Unity dependency.
    public static class PlayerCafeOwner
    {
        public const string SaveId = "freecafe.world.v1";
        public static CafeWorld Current { get; private set; }
        public static string RestoreError { get; private set; }
        public static void Bind(CafeWorld world, Func<string> capture = null, Action<string> restore = null)
        {
            Current = world; RestoreError = null;
            GameSaveParticipantRegistry.Capturers[SaveId] = capture ?? CaptureDomain;
            GameSaveParticipantRegistry.Restorers[SaveId] = restore ?? RestoreDomain;
        }
        private static string CaptureDomain() => UnityEngine.JsonUtility.ToJson(Current.State);
        private static void RestoreDomain(string json)
        {
            var result = Current.Restore(CafeSaveMigration.Prepare(UnityEngine.JsonUtility.FromJson<CafeSnapshot>(json)));
            if (!result.success) throw new FormatException(result.message);
        }
        public static void EndSession()
        {
            Current = null; RestoreError = null;
            GameSaveParticipantRegistry.Capturers.Remove(SaveId);
            GameSaveParticipantRegistry.Restorers.Remove(SaveId);
        }
        public static void ReportRestoreFailure(string error) => RestoreError = error;
        public static void Unbind(CafeWorld world)
        {
            // Scene unloading releases scene delegates, not the session or its domain state.
            if (Current == null || !ReferenceEquals(Current, world)) return;
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureDomain;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreDomain;
        }
    }
}
