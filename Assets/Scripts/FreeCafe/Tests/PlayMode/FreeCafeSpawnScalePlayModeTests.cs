using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeSpawnScalePlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;app.Enter(AreaId.Counter);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;yield return null; }
        private PointerEventData Pointer(FreeEntityView view) => new PointerEventData(EventSystem.current)
            {position=app.worldCamera.WorldToScreenPoint(view.transform.position)};
        private static void SameScale(Vector3 actual,Vector3 expected) => Assert.That(Vector3.Distance(actual,expected),Is.LessThan(.001f));
        [UnityTest] public IEnumerator MilkAndSugarDragGhostsUseTheScaledSourcePresentationAndCancelWithoutCost()
        {
            foreach(string sourceId in new[]{"s.milk","s.sugar"})
            {
                var source=app.View(sourceId);var record=app.World.Find(sourceId);int stock=record.stock;
                string itemSpec=app.World.Spec(record.specId).sourceItemId;
                Vector3 factor=source.transform.localScale;
                var sourceBase=app.prefabLibrary.Require(record.specId).transform.localScale;
                factor=new Vector3(factor.x/sourceBase.x,factor.y/sourceBase.y,factor.z/sourceBase.z);
                var expected=Vector3.Scale(app.prefabLibrary.Require(itemSpec).transform.localScale,factor);
                Physics2D.SyncTransforms();app.BeginDrag(source,Pointer(source));Assert.That(app.IsDragging,Is.True);
                var ghost=GameObject.Find("UncommittedPickup").GetComponent<FreeEntityView>();
                SameScale(ghost.transform.localScale,expected);Assert.That(ghost.PrefabSpecId,Is.EqualTo(itemSpec));
                app.CancelDrag();yield return null;Assert.That(record.stock,Is.EqualTo(stock));
            }
        }
        [UnityTest] public IEnumerator ReleasingMilkDoesNotShrinkTheCommittedEntityAndRestoringRecreatedViewsKeepsScale()
        {
            var source=app.View("s.milk");int stock=app.World.Find("s.milk").stock;
            Physics2D.SyncTransforms();var pointer=Pointer(source);app.BeginDrag(source,pointer);
            var ghost=GameObject.Find("UncommittedPickup").GetComponent<FreeEntityView>();Vector3 scale=ghost.transform.localScale;
            pointer.position=app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter,4,-5));
            app.EndDrag(pointer);yield return null;
            Assert.That(app.IsDragging,Is.False);Assert.That(app.World.Find("s.milk").stock,Is.EqualTo(stock-1));
            var milk=app.World.State.entities.Find(e=>e.specId=="milk" && string.IsNullOrEmpty(e.ownerId));
            Assert.That(milk,Is.Not.Null);SameScale(app.View(milk.id).transform.localScale,scale);
            string save=app.CaptureJson();app.World.State.entities.Remove(milk);app.DebugRefreshViews();yield return null;
            app.RestoreJson(save);yield return null;SameScale(app.View(milk.id).transform.localScale,scale);
        }
        [UnityTest] public IEnumerator CupAndMachineOutputUseTheSameSceneScaleAsTheirSourceOrMachine()
        {
            app.World.SelectServingCup("s.cups.large");var cup=app.View(app.World.ServingCup.id);
            SameScale(cup.transform.localScale,app.View("s.cups.large").transform.localScale);
            app.World.Take("s.beans",AreaId.Counter,0,-2,out var bean);app.World.Interact(bean.id,"m.grinder");
            for(int i=0;i<12;i++) { app.World.OperateProduction("m.grinder");app.World.Tick(.13f); }
            var machine=app.View("m.grinder");Physics2D.SyncTransforms();app.BeginDrag(machine,Pointer(machine));
            Assert.That(app.IsDragging,Is.True);var ghost=GameObject.Find("UncommittedMachineOutput").GetComponent<FreeEntityView>();
            SameScale(ghost.transform.localScale,machine.transform.localScale);app.CancelDrag();yield return null;
        }
        [UnityTest] public IEnumerator CarryingAndRecreatingMilkOnAnotherFloorDoesNotRevertItsScale()
        {
            app.World.Take("s.milk",AreaId.Counter,0,-2,out var milk);var scale=app.View(milk.id).transform.localScale;
            Assert.That(app.World.Move(milk.id,AreaId.Basement,0,-2).success,Is.True);SameScale(app.View(milk.id).transform.localScale,scale);
            string save=app.CaptureJson();app.World.State.entities.Remove(milk);app.DebugRefreshViews();yield return null;
            app.RestoreJson(save);yield return null;SameScale(app.View(milk.id).transform.localScale,scale);
        }
    }
}
