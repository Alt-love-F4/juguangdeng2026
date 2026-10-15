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
        private static CafeWorld sceneWorld;
        private static Action<string> sceneRestore;
        private static Func<string> sceneCapture;

        public static void Bind(CafeWorld world, Func<string> capture, Action<string> restore)
        {
            Bind(world, restore);
            sceneCapture = capture;
            GameSaveParticipantRegistry.Capturers[SaveId] = capture ?? CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = restore ?? RestoreJson;
        }

        public static void Bind(CafeWorld world, Action<string> restore = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            // A session-only map world may be replaced by its scene adapter, but a
            // second live scene must never replace the first scene's world/callback.
            if (sceneWorld != null && (sceneWorld != world || sceneRestore != restore))
                throw new InvalidOperationException("Duplicate FreeCafe world in loaded scenes");
            if ((GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId, out var capture) && capture != CaptureJson && capture != sceneCapture)
                || (GameSaveParticipantRegistry.Restorers.TryGetValue(SaveId, out var restorer) && restorer != RestoreJson && restorer != sceneRestore))
                throw new InvalidOperationException("Duplicate FreeCafe world save participant");

            Current = world; RestoreError = null;
            sceneWorld = restore == null ? null : world;
            sceneRestore = restore;
            sceneCapture = null;
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
        }

        private static string CaptureJson() => UnityEngine.JsonUtility.ToJson(Current.State);
        private static void RestoreJson(string json)
        {
            try
            {
                if (sceneRestore != null) sceneRestore(json);
                else
                {
                    var result = Current.Restore(CafeSaveMigration.Prepare(UnityEngine.JsonUtility.FromJson<CafeSnapshot>(json)));
                    if (!result.success) throw new FormatException(result.message);
                }
                RestoreError = null;
            }
            catch (Exception exception)
            {
                ReportRestoreFailure(exception.Message);
                throw;
            }
        }

        public static void EndSession()
        {
            Current = null; RestoreError = null;
            if (GameSaveParticipantRegistry.Capturers.TryGetValue(SaveId, out var capture) && (capture == CaptureJson || capture == sceneCapture))
                GameSaveParticipantRegistry.Capturers.Remove(SaveId);
            if (GameSaveParticipantRegistry.Restorers.TryGetValue(SaveId, out var restorer) && (restorer == RestoreJson || restorer == sceneRestore))
                GameSaveParticipantRegistry.Restorers.Remove(SaveId);
            sceneWorld = null; sceneRestore = null; sceneCapture = null;
        }
        public static void ReportRestoreFailure(string error) => RestoreError = error;
        public static void Unbind(CafeWorld world)
        {
            if (sceneWorld == null || sceneWorld != world) return;
            sceneWorld = null; sceneRestore = null; sceneCapture = null;
            GameSaveParticipantRegistry.Capturers[SaveId] = CaptureJson;
            GameSaveParticipantRegistry.Restorers[SaveId] = RestoreJson;
            // Keep Current and its save registration alive while the scene is unloaded.
        }
    }
}
