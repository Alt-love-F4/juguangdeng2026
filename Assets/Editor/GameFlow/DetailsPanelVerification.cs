using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Spotlight.GameFlow.Editor
{
    public static class DetailsPanelVerification
    {
        [MenuItem("Tools/Game Flow/Run Details Panel Regression")]
        public static void Run()
        {
            DetailsPanelPrefabBuilder.CreateMissing();
            DetailsPanelPrefabBuilder.BindExisting();
            PresentationDebugSceneBuilder.UpdateContentEntries();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                groupNames = new[] { "Spotlight.GameFlow.Tests.DetailsPanelTests", "Spotlight.GameFlow.Tests.PresentationDebugSceneTests" }
            }));
        }
        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi api;
            public Results(TestRunnerApi api) => this.api = api;
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Path.GetFullPath("output/details-panel"));
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("output/details-panel/test-results.xml"));
                Object.DestroyImmediate(api);
            }
        }
    }
}
