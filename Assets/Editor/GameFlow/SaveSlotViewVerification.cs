using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Spotlight.GameFlow.Editor
{
    internal static class SaveSlotViewVerification
    {
        [MenuItem("Tools/Game Flow/Run Save Slot View Regression")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetFullPath("output/save-slot-view"));
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before running save slot regression.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before running save slot regression.");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            File.WriteAllText("output/save-slot-view/run.started", DateTime.UtcNow.ToString("O"));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                groupNames = new[] { "Spotlight.GameFlow.Tests.SaveSlotViewPrefabTests", "Spotlight.GameFlow.Tests.SaveSlotViewPlayModeTests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi owner;
            public Results(TestRunnerApi owner) => this.owner = owner;
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("output/save-slot-view/test-results.xml"));
                    File.WriteAllText("output/save-slot-view/run.finished", "Passed=" + result.PassCount + "; Failed=" + result.FailCount);
                }
                finally
                {
                    TestRunnerApi.UnregisterTestCallback(this);
                    if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                }
            }
        }
    }
}
