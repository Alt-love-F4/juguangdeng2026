using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProductionPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop",LoadSceneMode.Single);yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.World.EnterProduction();yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;yield return null; }
        private PointerEventData Pointer(float x,float y) => new PointerEventData(EventSystem.current)
        { position=app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter,x,y)) };
        [UnityTest] public IEnumerator UsesExistingSceneCanvasAndCupButtonsTargetTheServingTray()
        {
            var hud=app.GetComponent<FreeCafeProductionHud>();Assert.That(hud,Is.Not.Null);Assert.That(hud.productionRoot.activeSelf,Is.True);
            Assert.That(hud.productionRoot.GetComponentInParent<FreeCafeHud>(),Is.Not.Null);
            hud.smallCup.onClick.Invoke();yield return null;Assert.That(app.World.ServingCup.specId,Is.EqualTo("cup.small"));
            hud.largeCup.onClick.Invoke();yield return null;Assert.That(app.World.ServingCup.specId,Is.EqualTo("cup.large"));
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CoffeeShop"));
        }
        [UnityTest] public IEnumerator InvalidActualSourceDragReturnsWithoutWithdrawingStock()
        {
            var milk=app.World.Find("s.milk");var grinder=app.World.Find("m.grinder");int stock=milk.stock,count=app.World.State.entities.Count;
            Physics2D.SyncTransforms();app.BeginDrag(app.View(milk.id),Pointer(milk.x,milk.y));Assert.That(app.IsDragging,Is.True);
            app.EndDrag(Pointer(grinder.x,grinder.y));yield return new WaitForSecondsRealtime(.3f);
            Assert.That(milk.stock,Is.EqualTo(stock));Assert.That(app.World.State.entities.Count,Is.EqualTo(count));
            Assert.That(grinder.production.phase,Is.EqualTo(ProductionPhase.Empty));Assert.That(app.IsDragging,Is.False);
        }
        [UnityTest] public IEnumerator ActualBeanDragAndMachineClickStartTheNewGrinder()
        {
            var beans=app.World.Find("s.beans");var grinder=app.World.Find("m.grinder");int stock=beans.stock;
            Physics2D.SyncTransforms();app.BeginDrag(app.View(beans.id),Pointer(beans.x,beans.y));app.EndDrag(Pointer(grinder.x,grinder.y));yield return null;
            Assert.That(beans.stock,Is.EqualTo(stock-1));Assert.That(grinder.production.phase,Is.EqualTo(ProductionPhase.Loaded));
            app.Click(app.View(grinder.id));Assert.That(grinder.production.phase,Is.EqualTo(ProductionPhase.Processing));
            app.World.Tick(12);yield return null;Assert.That(app.World.DragOutputPreview(grinder.id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator RealKettleDragPoursMillilitersAndReturnsTheSameEntityToItsStation()
        {
            app.World.SelectServingCup("s.cups.large");app.World.TakeAndInteract("s.milk","m.heater",AreaId.Counter,0,0);app.World.OperateProduction("m.heater");app.World.Tick(4);
            var kettle=app.World.Find("m.heater");var cup=app.World.ServingCup;var view=app.View(kettle.id);Vector3 origin=view.transform.position;
            Physics2D.SyncTransforms();app.BeginDrag(view,Pointer(kettle.x,kettle.y));Assert.That(app.IsDragging,Is.True);
            var pointer=Pointer(cup.x,cup.y+.7f);app.MoveDrag(pointer);
            yield return null;yield return null;yield return null;app.EndDrag(pointer);yield return null;
            Assert.That(app.World.LiquidVolume(cup),Is.GreaterThan(0));Assert.That(app.World.LiquidVolume(kettle),Is.LessThan(200));
            Assert.That(app.View(kettle.id),Is.SameAs(view));Assert.That(view.transform.position,Is.EqualTo(origin));
        }
        
        [UnityTest] public IEnumerator KettleCanPourFromAboveABrewerAtTheSameStationHeight()
        {
            app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0);app.World.Operate("m.grinder");app.World.Tick(12);
            var grounds=app.World.DragOutputPreview("m.grinder");app.World.TakeMachineOutput("m.grinder",grounds.id,"m.brewer",AreaId.Counter,-3.3f,.2f);
            app.World.TakeAndInteract("s.water","m.heater",AreaId.Counter,0,0);app.World.Operate("m.heater");app.World.Tick(4);
            var kettle=app.World.Find("m.heater");var brewer=app.World.Find("m.brewer");Physics2D.SyncTransforms();
            app.BeginDrag(app.View(kettle.id),Pointer(kettle.x,kettle.y));app.MoveDrag(Pointer(brewer.x,brewer.y+1.3f));yield return null;
            var pointer=Pointer(brewer.x,brewer.y+.4f);app.MoveDrag(pointer);yield return null;yield return null;yield return null;
            app.EndDrag(pointer);yield return null;
            Assert.That(app.World.LiquidVolume(brewer),Is.GreaterThan(0));Assert.That(brewer.production.brewStarted,Is.True);
            Assert.That(brewer.production.phase,Is.EqualTo(ProductionPhase.Loaded),"不足水量可继续，不丢掉咖啡粉");
        }

        [UnityTest] public IEnumerator MiniGamePlaceholderTriggersCancelsAndLeavesTheCupUntouched()
        {
            app.World.SelectServingCup("s.cups.large");var cup=app.World.ServingCup;
            cup.contents.Add(new Portion{specId="coffee",units=1,milliliters=100,productionScore=100});
            cup.contents.Add(new Portion{specId="milk",units=1,milliliters=100,productionScore=100});
            var hud=app.GetComponent<FreeCafeProductionHud>();hud.miniGameButton.onClick.Invoke();yield return null;
            Assert.That(hud.miniGamePanel.activeSelf,Is.True);Assert.That(app.InputBlocked,Is.True);
            string token=app.World.State.production.miniGameToken;hud.cancelMiniGame.onClick.Invoke();yield return null;
            Assert.That(app.InputBlocked,Is.False);Assert.That(hud.miniGamePanel.activeSelf,Is.False);Assert.That(app.World.LiquidVolume(cup),Is.EqualTo(200));
            Assert.That(app.World.ResolveProductionMiniGame(token,true,100).success,Is.False);
        }
        [UnityTest] public IEnumerator ReturnButtonReturnsToTheEnteringAreaAndPreservesState()
        {
            app.Enter(AreaId.TopFloor);app.Enter(AreaId.Counter);yield return null;
            app.World.SelectServingCup("s.cups.small");string cupId=app.World.ServingCup.id;
            app.GetComponent<FreeCafeProductionHud>().returnButton.onClick.Invoke();yield return null;
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.TopFloor));Assert.That(app.World.ServingCup.id,Is.EqualTo(cupId));
            Assert.That(app.areaManager.CurrentArea,Is.EqualTo(AreaId.TopFloor));
        }
    }
}
