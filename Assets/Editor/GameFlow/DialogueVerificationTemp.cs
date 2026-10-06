using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal static class DialogueVerificationTemp
{
    private static readonly TestRunnerApi Api;
    private const string Request = "output/dialogue-run-tests.request";
    private const string Running = "output/dialogue-tests.running";
    static DialogueVerificationTemp()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new Results());
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        File.WriteAllText(Running, "running");
        Api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
            groupNames = new[] { "Spotlight.GameFlow.Tests.DialoguePresentationTests" } }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!File.Exists(Running)) return;
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("output/dialogue-tests.xml"));
            File.Delete(Running);
        }
    }
}
