using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Keep E preview evidence separate from other windows' shared test-run summaries.</summary>
    [InitializeOnLoad]
    public static class ChurchLongPreviewVerification
    {
        private static readonly TestRunnerApi Api;
        static ChurchLongPreviewVerification()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new Results());
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                const string prefix = "Spotlight.GameFlow.Tests.ChurchLongPagePreviewTests.";
                string name = result.Test.FullName;
                if (!name.StartsWith(prefix, System.StringComparison.Ordinal)) return;
                string directory = Path.GetFullPath("Library/Locus/tmp/church-page-preview-results");
                Directory.CreateDirectory(directory);
                // One method per file: a later unrelated suite cannot overwrite this evidence.
                TestRunnerApi.SaveResultToFile(result, Path.Combine(directory, name.Substring(prefix.Length) + ".xml"));
            }
        }
    }
}
