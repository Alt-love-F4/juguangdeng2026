using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>用户手动触发的制作系统回归入口；不注册启动轮询、不读取命令文件。</summary>
public static class ProductionLocalVerification
{
    private static TestRunnerApi runner;
    private static Results callback;
    [MenuItem("Tools/Free Cafe/Verification/Run EditMode Tests")]
    public static void RunEditMode() => Run(TestMode.EditMode,"Spotlight.FreeCafe.Tests.EditMode","freecafe-editmode");
    [MenuItem("Tools/Free Cafe/Verification/Run PlayMode Tests")]
    public static void RunPlayMode() => Run(TestMode.PlayMode,"Spotlight.FreeCafe.Tests.PlayMode","freecafe-playmode");
    private static void Run(TestMode mode,string assembly,string report)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("请先保存场景、退出 Play Mode 并等待编译完成，再运行回归。");
        if(callback!=null) TestRunnerApi.UnregisterTestCallback(callback);
        if(runner!=null) UnityEngine.Object.DestroyImmediate(runner);
        runner=ScriptableObject.CreateInstance<TestRunnerApi>();callback=new Results(runner,report,assembly);runner.RegisterCallbacks(callback);
        runner.Execute(new ExecutionSettings(new Filter { testMode=mode,assemblyNames=new[]{assembly} }));
    }
    private sealed class Results : ICallbacks
    {
        private readonly TestRunnerApi owner;
        private readonly string report;
        private readonly string assembly;
        private bool finished;
        public Results(TestRunnerApi owner,string report,string assembly) { this.owner=owner;this.report=report;this.assembly=assembly; }
        public void RunStarted(ITestAdaptor tests) => Debug.Log("[FreeCafe Verification] 开始 "+report);
        private static bool ContainsAssembly(ITestAdaptor test,string name)
        {
            if(test.IsTestAssembly && (test.Name==name || test.Name==name+".dll")) return true;
            foreach(var child in test.Children)
                if(ContainsAssembly(child,name)) return true;
            return false;
        }
        public void TestStarted(ITestAdaptor test) {}
        public void TestFinished(ITestResultAdaptor result) {}
        public void RunFinished(ITestResultAdaptor result)
        {
            if(finished || !ContainsAssembly(result.Test,assembly)) return;
            finished=true;
            try
            {
                Directory.CreateDirectory("output/production-validation");
                TestRunnerApi.SaveResultToFile(result,"output/production-validation/"+report+".xml");
                Debug.Log("[FreeCafe Verification] "+report+" 通过 "+result.PassCount+" / 失败 "+result.FailCount);
            }
            finally
            {
                // 回调是全局注册，不能因 Unity owner 已销毁而跳过注销。
                TestRunnerApi.UnregisterTestCallback(this);
                if(owner!=null) UnityEngine.Object.DestroyImmediate(owner);
                if(ReferenceEquals(callback,this)) { runner=null;callback=null; }
            }
        }
    }
}
