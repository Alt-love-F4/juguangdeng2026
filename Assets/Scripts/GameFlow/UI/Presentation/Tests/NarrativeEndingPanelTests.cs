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
                if(debug.HelpVisible)debug.ToggleHelp();
                var panel=debug.CurrentPanel as ResultPanel;Assert.That(panel,Is.Not.Null,e.endingId);
                Assert.That(panel.IsEndingPresentation,Is.True);Assert.That(panel.CurrentEndingId,Is.EqualTo(e.endingId));
                Assert.That(panel.DisplayedTitle,Is.EqualTo(config.texts.Find(t=>t.key==e.titleText).content));
                Assert.That(panel.DisplayedMessage,Is.Not.Empty);Assert.That(panel.GetComponent<CanvasGroup>(),Is.Not.Null);
                var art=NarrativeEndingArtworkCatalog.Load().Find(e.endingId);
                Assert.That(panel.DisplayedEndingArtwork,Is.EqualTo(art.artwork));
                Assert.That(panel.IsEndingDialogueVisible,Is.True);
                Assert.That(panel.DisplayedEndingEcho,Is.Empty);
                Assert.That(panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.text.Contains("ENDING  /  "+art.displayCode)&&t.gameObject.activeSelf),Is.True,"Prefab must bind authored display code.");
                for(int frame=0;frame<20;frame++)yield return null;
                var texts=panel.GetComponentsInChildren<TMPro.TMP_Text>(true);
                var title=texts.First(t=>t.gameObject.name=="TitleText");
                var message=texts.First(t=>t.gameObject.name=="MessageText");
                var image=panel.DisplayedEndingArtwork;
                Assert.That(panel.EndingDialogue,Is.Not.Null);
                Assert.That(panel.EndingDialogue.IsEndingSettlement,Is.True);
                Assert.That(panel.EndingDialogue.SettlementPageCount,Is.GreaterThanOrEqualTo(1));
                Assert.That(panel.EndingDialogue.SettlementText,Is.EqualTo(NarrativeTextSubstitution.PlayerName(config.texts.Find(t=>t.key==e.messageText).content,"预览店长")));
                Assert.That(DialoguePanel.IsAnyPlaying,Is.False,"Embedded settlement must not own formal dialogue playback.");
                // Each paragraph/page starts hidden. Revealing the last page must not close it;
                // only the following confirmation returns from the ending.
                int guard=0;
                while(true)
                {
                    Assert.That(++guard,Is.LessThan(100),e.endingId);
                    if(panel.EndingDialogue.IsSettlementTyping)
                    {
                        panel.EndingDialogue.AdvanceEndingSettlement();
                        Assert.That(panel.EndingDialogue.IsSettlementTyping,Is.False);
                        yield return null;
                    }
                    if(panel.EndingDialogue.IsSettlementLastPage)break;
                    panel.EndingDialogue.AdvanceEndingSettlement();
                    yield return null;
                }
                Assert.That(panel.CurrentEndingId,Is.EqualTo(e.endingId));
                Assert.That(title.rectTransform.anchorMin.y,Is.GreaterThan(.85f));
                Assert.That(message.rectTransform.anchorMax.y,Is.LessThan(.35f));
                if(e.endingId=="neutral.ending.a"||e.endingId=="church.ending.if2"||e.endingId=="rose.route.ending.bloom.ending")
                {
                    System.IO.Directory.CreateDirectory("output/narrative-endings");
                    yield return new WaitForEndOfFrame();
                    SaveScreenshot("output/narrative-endings/revised-"+e.endingId+".png");
                    yield return null;yield return null;
                }
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
            yield return ReadToLastPage(panel);
            panel.ConfirmResult();panel.ConfirmResult();for(int i=0;i<30&&closed==0;i++)yield return null;
            Assert.That(closed,Is.EqualTo(1));
            var ordinary=UIKit.OpenPanel<ResultPanel>(UILevel.Pop,new ResultPanelData(null,"操作提示","普通消息",null),tag:"ending-panel-test",cachePolicy:PanelCachePolicy.Reusable);yield return null;
            Assert.That(ordinary.IsEndingPresentation,Is.False);Assert.That(ordinary.CurrentEndingId,Is.Null);
            Assert.That(ordinary.DisplayedEndingArtwork,Is.Null);
            Assert.That(ordinary.DisplayedEndingEcho,Is.Empty);
            Assert.That(ordinary.IsEndingDialogueVisible,Is.False);
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
                yield return new WaitForEndOfFrame();
                SaveScreenshot("output/narrative-endings/accepted-ending-panel.png");
                yield return null;yield return null;
                core.UnbindValueProviders();
            }
        }
        private static IEnumerator ReadToLastPage(ResultPanel panel)
        {
            for(int guard=0;guard<100;guard++)
            {
                yield return null;
                var dialogue=panel.EndingDialogue;
                if(!dialogue.IsSettlementTyping&&dialogue.IsSettlementLastPage)yield break;
                panel.ConfirmResult();
            }
            Assert.Fail("Ending text did not reach its last readable page.");
        }

        [UnityTest]public IEnumerator EndingTypesInUnscaledTimeAndBodyClicksRevealThenAdvanceParagraphs()
        {
            Time.timeScale=0;
            int closed=0;
            var panel=UIKit.OpenPanel<ResultPanel>(UILevel.Pop,
                new ResultPanelData(null,"逐字结局","第一段的文字逐字出现，不应直接跳过。\n\n第二段也从头逐字显示。",()=>closed++,"返回主菜单","fixture.typing"),
                tag:"ending-panel-test",cachePolicy:PanelCachePolicy.Reusable);
            yield return null;
            var dialogue=panel.EndingDialogue;
            var serial=new UnityEditor.SerializedObject(dialogue);
            var text=(TMPro.TMP_Text)serial.FindProperty("dialogueText").objectReferenceValue;
            var body=(UnityEngine.UI.Button)serial.FindProperty("dialogueBodyButton").objectReferenceValue;
            var speed=serial.FindProperty("charactersPerSecond");speed.floatValue=1f;serial.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(dialogue.SettlementSegmentCount,Is.EqualTo(2));
            Assert.That(dialogue.IsSettlementTyping,Is.True);
            int visible=text.maxVisibleCharacters;
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(text.maxVisibleCharacters,Is.GreaterThan(visible),"Typing must continue at timeScale=0.");
            body.onClick.Invoke();body.onClick.Invoke();
            Assert.That(dialogue.IsSettlementTyping,Is.False);
            Assert.That(dialogue.SettlementSegmentIndex,Is.Zero,"Same-frame clicks must not skip to another paragraph.");
            Assert.That(closed,Is.Zero);
            yield return null;body.onClick.Invoke();
            Assert.That(dialogue.SettlementSegmentIndex,Is.EqualTo(1));
            Assert.That(dialogue.IsSettlementTyping,Is.True);
            yield return null;panel.ConfirmResult();
            Assert.That(dialogue.IsSettlementTyping,Is.False);
            Assert.That(closed,Is.Zero,"The last reveal must not also close the ending.");
            yield return null;body.onClick.Invoke();body.onClick.Invoke();
            for(int i=0;i<45&&closed==0;i++)yield return null;
            Assert.That(closed,Is.EqualTo(1));
            Assert.That(DialoguePanel.IsAnyPlaying,Is.False);
        }

        [UnityTest]public IEnumerator EndingKeyboardAndContinueButtonShareOneAdvanceAndHonorExternalInputGate()
        {
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var oldGate=DialoguePanel.ExternalInputBlocked;
            try
            {
                Time.timeScale=0;
                var panel=UIKit.OpenPanel<ResultPanel>(UILevel.Pop,
                    new ResultPanelData(null,"键盘结局",new string('文',1800),null,"关闭","fixture.keyboard"),
                    tag:"ending-panel-test",cachePolicy:PanelCachePolicy.Reusable);
                yield return null;
                var dialogue=panel.EndingDialogue;
                var serial=new UnityEditor.SerializedObject(dialogue);
                var arrow=(UnityEngine.UI.Button)serial.FindProperty("continueButton").objectReferenceValue;
                serial.FindProperty("charactersPerSecond").floatValue=.01f;serial.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(dialogue.SettlementPageCount,Is.GreaterThan(1));
                DialoguePanel.ExternalInputBlocked=()=>true;
                arrow.onClick.Invoke();
                Assert.That(dialogue.IsSettlementTyping,Is.True);
                DialoguePanel.ExternalInputBlocked=()=>false;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                yield return null;yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                Assert.That(dialogue.IsSettlementTyping,Is.False);
                Assert.That(dialogue.PageNumber,Is.EqualTo(1),"One Enter reveals but does not also turn the page.");
                arrow.onClick.Invoke();
                Assert.That(dialogue.PageNumber,Is.EqualTo(2));
                Assert.That(dialogue.IsSettlementTyping,Is.True);
            }
            finally
            {
                DialoguePanel.ExternalInputBlocked=oldGate;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
            }
        }

        private static void SaveScreenshot(string path)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path),texture.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(texture);}
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
