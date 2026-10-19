#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spotlight.Narrative;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YokiFrame;

namespace Spotlight.GameFlow.Tests
{
    public sealed class NarrativeEndingPanelTests
    {
        private Scene previous,shared;
        private PresentationDebugSceneController debug;
        private float oldTime;
        [UnitySetUp]public IEnumerator Setup()
        {
            oldTime=Time.timeScale;previous=SceneManager.GetActiveScene();
            shared=EditorSceneManager.LoadSceneInPlayMode(PresentationDebugSceneController.ScenePath,new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;SceneManager.SetActiveScene(shared);
            debug=UnityEngine.Object.FindObjectsByType<PresentationDebugSceneController>(FindObjectsSortMode.None).First(c=>c.gameObject.scene==shared);
        }
        [UnityTearDown]public IEnumerator Teardown()
        {
            debug?.StopCurrent();UIKit.ClosePanelsByTag("ending-panel-test");Time.timeScale=oldTime;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(shared.IsValid()&&shared.isLoaded)yield return SceneManager.UnloadSceneAsync(shared);
        }
        [UnityTest]public IEnumerator EveryEnabledEndingHasARealReadablePanelEvenAtTimeScaleZero()
        {
            var config=LubanNarrativeLoader.Load();var runtime=NarrativeRuntime.Instance;
            string before=runtime?.Controller==null?null:JsonUtility.ToJson(runtime.Controller.State.Capture());
            var endings=config.endings.Where(e=>e.enabled).ToArray();Assert.That(endings.Length,Is.EqualTo(16));
            Time.timeScale=0;
            foreach(var e in endings)
            {
                debug.PlayEnding(e.endingId,config);yield return null;
                var panel=debug.CurrentPanel as ResultPanel;Assert.That(panel,Is.Not.Null,e.endingId);
                Assert.That(panel.IsEndingPresentation,Is.True);Assert.That(panel.CurrentEndingId,Is.EqualTo(e.endingId));
                Assert.That(panel.DisplayedTitle,Is.EqualTo(config.texts.Find(t=>t.key==e.titleText).content));
                Assert.That(panel.DisplayedMessage,Is.Not.Empty);Assert.That(panel.GetComponent<CanvasGroup>(),Is.Not.Null);
                Assert.That(panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.text.Contains("ENDING  /  "+e.endingId)&&t.gameObject.activeSelf),Is.True,"Prefab must bind ending badge.");
                debug.StopCurrent();yield return null;
            }
            Assert.That(NarrativeRuntime.Instance,Is.SameAs(runtime));
            if(before!=null)Assert.That(JsonUtility.ToJson(runtime.Controller.State.Capture()),Is.EqualTo(before));
        }
        [UnityTest]public IEnumerator ConfirmingTwiceClosesOnlyOnceAndReusableOrdinaryResultClearsEndingBadge()
        {
            Time.timeScale=0;
            var c=LubanNarrativeLoader.Load();var ending=c.endings.First(e=>e.enabled);int closed=0;
            var data=NarrativeEndingPresentation.Create(null,ending,key=>c.texts.Find(t=>t.key==key)?.content??key,()=>closed++,"关闭测试");
            var panel=UIKit.OpenPanel<ResultPanel>(UILevel.Pop,data,tag:"ending-panel-test",cachePolicy:PanelCachePolicy.Reusable);
            for(int i=0;i<25&&panel.State==PanelState.Opening;i++)yield return null;
            panel.ConfirmResult();panel.ConfirmResult();for(int i=0;i<30&&closed==0;i++)yield return null;
            Assert.That(closed,Is.EqualTo(1));
            var ordinary=UIKit.OpenPanel<ResultPanel>(UILevel.Pop,new ResultPanelData(null,"操作提示","普通消息",null),tag:"ending-panel-test",cachePolicy:PanelCachePolicy.Reusable);yield return null;
            Assert.That(ordinary.IsEndingPresentation,Is.False);Assert.That(ordinary.CurrentEndingId,Is.Null);
            Assert.That(ordinary.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.gameObject.activeSelf&&t.text.StartsWith("ENDING  /")),Is.False);
        }
        [UnityTest]public IEnumerator ActualAuthoredRootCompletesThroughPresenterThenOpensTheRealEndingPanel()
        {
            var c=LubanNarrativeLoader.Load();
            using(var presenter=new NarrativePresenter(c,key=>c.texts.Find(t=>t.key==key)?.content??key))
            {
                var core=new NarrativeController(c,presenter);core.BeginValueRestore();
                core.BindValueProvider(new DelegateNarrativeValueProvider("dayflow",key=>key=="day"?37:(int?)null));
                core.BindValueProvider(new DelegateNarrativeValueProvider("cafe",key=>key=="money"?500:(int?)null));
                core.BindValueProvider(new DelegateNarrativeValueProvider("actions",key=>key=="current"||key=="max"?5:(int?)null));
                core.State.SetCondition("route.neutral",true);core.SetOfficialDay(37);core.CompleteValueRestore(false);
                presenter.PageCondition=core.ReadPresentationCondition;
                int shown=0;ResultPanel result=null;
                core.EndingSelected+=ending=>
                {
                    shown++;Time.timeScale=0;
                    result=UIKit.OpenPanel<ResultPanel>(UILevel.Pop,NarrativeEndingPresentation.Create(null,ending,key=>c.texts.Find(t=>t.key==key)?.content??key,null),tag:"ending-panel-test",cachePolicy:PanelCachePolicy.Reusable);
                };
                int pages=0;
                presenter.ConversationPlayback=(request,data,done)=>{pages++;Assert.That(shown,Is.Zero,"Ending cannot precede final root callback.");done();return true;};
                core.SetEndingChecksEnabled(true);
                Assert.That(shown,Is.Zero);Assert.That(core.QueueEvent("neutral.route.1111.s1"),Is.True);
                for(int i=0;i<90&&shown==0;i++)yield return null;
                Assert.That(pages,Is.GreaterThan(1));Assert.That(shown,Is.EqualTo(1));Assert.That(core.SelectedEndingId,Is.EqualTo("neutral.ending.a"));
                Assert.That(core.State.TryGetCondition("story.neutral.route.1111.s1.completed",out bool completed)&&completed,Is.True);
                Assert.That(result,Is.Not.Null);Assert.That(result.IsEndingPresentation,Is.True);Assert.That(result.CurrentEndingId,Is.EqualTo(core.SelectedEndingId));
                for(int i=0;i<20;i++)yield return null;
                Assert.That(result.State,Is.EqualTo(PanelState.Open));
                if(!System.IO.Directory.Exists("output/narrative-endings"))System.IO.Directory.CreateDirectory("output/narrative-endings");
                UnityEngine.ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../output/narrative-endings/accepted-ending-panel.png")));
                yield return null;yield return null;
                core.UnbindValueProviders();
            }
        }
        [Test]public void UnresolvedTextKeysNeverLeakIntoEndingPanel()
        {
            var data=NarrativeEndingPresentation.Create(null,new NarrativeEndingDefinition{endingId="fixture.ending",titleText="unknown.title",messageText="unknown.message"},key=>key,null);
            Assert.That(data.Title,Is.Not.EqualTo("unknown.title"));Assert.That(data.Message,Is.Not.EqualTo("unknown.message"));
            Assert.That(data.EndingId,Is.EqualTo("fixture.ending"));
        }
    }
}
#endif
