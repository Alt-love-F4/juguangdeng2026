using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Persist Unity's full diagnostics, including failures hidden by external bridge job summaries.</summary>
    [InitializeOnLoad]
    public static class PresentationEffectsVerification
    {
        private static readonly TestRunnerApi Api;
        static PresentationEffectsVerification()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Results());
        }
        [MenuItem("Tools/Game Flow/Run Presentation Effects Regression")]
        public static void Run()
        {
            Api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                groupNames = new[] { "Spotlight.GameFlow.Tests.DialoguePresentationTests", "Spotlight.GameFlow.Tests.PresentationDebugSceneTests",
                    "Spotlight.GameFlow.Tests.PresentationEffectsTests", "Spotlight.GameFlow.Tests.FirstDayIntroductionTests" }
            }));
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Path.GetFullPath("output"));
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("output/presentation-effects-tests.xml"));
            }
        }
    }
}
