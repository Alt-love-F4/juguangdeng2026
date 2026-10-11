#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Spotlight.GameFlow.Editor
{
    [InitializeOnLoad]
    public static class MapLayoutValidationRunner
    {
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "outputs", "map-layout", "validation.txt"));
        private const string PreviousScene = "MapLayoutValidationPreviousScene";
        private static TestRunnerApi runner;
        private static Results callbacks;

        static MapLayoutValidationRunner() => EditorApplication.update += RestoreScene;

        [MenuItem("Tools/GameFlow/Validate Map Layout/Edit Mode")]
        public static void RunEditMode() => Run(false);

        [MenuItem("Tools/GameFlow/Validate Map Layout/Play Mode And Screenshots")]
        public static void RunPlayMode() => Run(true);

        private static void RestoreScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!SessionState.GetBool("MapLayoutValidationFinished", false)) return;
            SessionState.SetBool("MapLayoutValidationFinished", false);
            string previous = SessionState.GetString(PreviousScene, "");
            SessionState.EraseString(PreviousScene);
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        }

        private static void Run(bool playMode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validating map UI.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            if (!playMode) MapPanelPrefabBuilder.BuildDefault();
            else
            {
                var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (current.isDirty) throw new InvalidOperationException("Save the current scene before running map previews.");
                SessionState.SetString(PreviousScene, current.path);
                EditorSceneManager.OpenScene(PresentationDebugSceneController.ScenePath);
            }
            File.WriteAllText(Output, playMode ? "Play Mode regression in the shared presentation debug scene.\n" : "MapPanel rebuilt successfully.\n");
            if (callbacks != null && runner != null) runner.UnregisterCallbacks(callbacks);
            if (runner == null) runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new Results(playMode);
            runner.RegisterCallbacks(callbacks);
            runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = playMode ? TestMode.PlayMode : TestMode.EditMode,
                testNames = playMode ? new[] { "Spotlight.GameFlow.Tests.MapLayoutPlayModeTests" }
                    : new[] { "Spotlight.GameFlow.Tests.MapLayoutTests", "Spotlight.GameFlow.Tests.MapActionPointsViewTests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            private readonly bool playMode;
            private int passed, failed, skipped;
            public Results(bool playMode) => this.playMode = playMode;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite || !result.Test.FullName.StartsWith("Spotlight.GameFlow.Tests.Map", StringComparison.Ordinal)) return;
                if (result.TestStatus == TestStatus.Passed) passed++;
                else if (result.TestStatus == TestStatus.Failed) failed++;
                else skipped++;
                File.AppendAllText(Output, result.Test.FullName + ": " + result.TestStatus + " " + result.Message + "\n");
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (passed + failed + skipped == 0) return;
                File.AppendAllText(Output, $"TOTAL passed={passed} failed={failed} skipped={skipped}\n");
                runner.UnregisterCallbacks(this);
                if (playMode) SessionState.SetBool("MapLayoutValidationFinished", true);
            }
        }
    }
}
#endif
