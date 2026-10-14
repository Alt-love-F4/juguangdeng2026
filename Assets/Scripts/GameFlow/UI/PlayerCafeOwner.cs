using Spotlight.FreeCafe;

namespace Spotlight.GameFlow
{
    // The scene adapter supplies the domain owner without a UI -> FreeCafe.Unity dependency.
    public static class PlayerCafeOwner
    {
        public const string SaveId = "freecafe.world.v1";
        public static CafeWorld Current { get; private set; }
        public static string RestoreError { get; private set; }
        public static void Bind(CafeWorld world)
        {
            Current = world; RestoreError = null;
            GameSaveParticipantRegistry.Capturers[SaveId] = () => UnityEngine.JsonUtility.ToJson(Current.State);
            GameSaveParticipantRegistry.Restorers[SaveId] = json =>
            {
                var result = Current.Restore(CafeSaveMigration.Prepare(UnityEngine.JsonUtility.FromJson<CafeSnapshot>(json)));
                if (!result.success) throw new System.FormatException(result.message);
            };
        }
        public static void EndSession()
        {
            Current = null; RestoreError = null;
            GameSaveParticipantRegistry.Capturers.Remove(SaveId);
            GameSaveParticipantRegistry.Restorers.Remove(SaveId);
        }
        public static void ReportRestoreFailure(string error) => RestoreError = error;
        public static void Unbind(CafeWorld world) { /* Scene unloading does not end the game session. */ }
    }
}
