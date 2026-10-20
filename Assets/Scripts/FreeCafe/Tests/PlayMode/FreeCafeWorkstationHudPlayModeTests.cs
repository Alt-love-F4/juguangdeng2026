using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeWorkstationHudPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeProductionHud hud;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;
            GameSaveParticipantRegistry.PendingRestore=null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null; yield return null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();
            hud=app.GetComponent<FreeCafeProductionHud>();
            app.areaManager.panSmoothTime=0;
            Assert.That(app.World.StartDay().success,Is.True);
            app.Enter(AreaId.Counter);
            app.enabled=false; // Real domain operations with deterministic, explicit ticks.
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(app!=null) { hud.CloseOverlays();app.enabled=true;app.CancelDrag(); }
            Time.timeScale=1;yield return null;
        }
        [UnityTest] public IEnumerator FormalDeliveryRequiresTrayThenShowsActualReceiptAndReleasesInput()
        { yield return DeliverAndConfirm(false); }
        [UnityTest] public IEnumerator ExistingDeliveryBellSharesTheReceiptAndDoesNotDeliverTwice()
        { yield return DeliverAndConfirm(true); }
        [UnityTest] public IEnumerator BothPracticeRecipesWaitForPhysicalBellWithoutSettlingOrders()
        {
            foreach (bool extraction in new[] {false,true})
            {
                var guide=app.WorkstationTutorial;
                Assert.That(guide.Begin(extraction),Is.True);
                var world=app.World;
                if(world.ServingCup==null) Assert.That(world.SelectServingCup("s.cups.large").success,Is.True);
                var cup=world.ServingCup;
                cup.contents.Add(new Portion {specId="coffee",units=1,milliliters=240});
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(FreeCafeWorkstationTutorial).GetField("cup",flags).SetValue(guide,cup.id);
                typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage",flags).Invoke(guide,new object[] {FreeCafeWorkstationTutorial.Stage.DeliverCup});
                Assert.That(world.Interact(cup.id,"service").success,Is.True);
                app.DebugRefreshViews();yield return null;
                Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.SubmitCup));
                Assert.That(hud.deliver.interactable,Is.False);
                Assert.That(app.DeliverCup().success,Is.False);
                int money=world.State.money,served=world.State.todayServed,orders=world.State.requests.Count;
                app.View("service").GetComponentInChildren<FreeCafeDeliveryBell>().OnPointerClick(
                    new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left});
                Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Complete));
                Assert.That(world.State.money,Is.EqualTo(money));
                Assert.That(world.State.todayServed,Is.EqualTo(served));
                Assert.That(world.State.requests.Count,Is.EqualTo(orders));
                Assert.That(world.Find(cup.id),Is.Not.Null);
                Assert.That(hud.receiptPanel.activeSelf,Is.False);
                guide.Stop();yield return null;
            }
        }
        private IEnumerator DeliverAndConfirm(bool bell)
        {
            var w=app.World;
            var request=w.Request(w.State.focusedRequestId);
            Assert.That(request,Is.Not.Null);
            request.productId="blackcoffee";
            Assert.That(w.SelectServingCup("s.cups.small").success,Is.True);
            var cup=w.ServingCup;
            cup.contents.Add(new Portion {specId="coffee",units=1,milliliters=240,
                coffeeKind="black",temperature=LiquidTemperature.Warm,productionScore=100});
            w.AddConstituent(cup.contents[0],"grounds",14.4f,0);
            w.AddConstituent(cup.contents[0],"water",0,240);
            Assert.That(w.Move(cup.id,AreaId.Counter,0,0).success,Is.True);
            app.DebugRefreshViews();yield return null;
            Assert.That(hud.deliver.interactable,Is.False,"A prepared cup outside the tray cannot be submitted");
            var tray=w.State.entities.Find(e=>w.Spec(e.specId).Has(Capability.Delivery));
            Assert.That(w.Interact(cup.id,tray.id).success,Is.True);
            app.DebugRefreshViews();yield return null;
            Assert.That(hud.deliver.interactable,Is.True);
            int money=w.State.money,reputation=w.State.reputation;
            CafeDeliveryReceipt receipt=default;int receipts=0;
            System.Action<CafeDeliveryReceipt> listener=r=>{receipt=r;receipts++;};
            app.CupDelivered+=listener;
            try
            {
                if(bell)
                {
                    app.Click(app.View(tray.id));
                    Assert.That(receipts,Is.Zero,"Placing or clicking a cup on the plate must not submit it");
                    app.View(tray.id).GetComponentInChildren<FreeCafeDeliveryBell>().OnPointerClick(
                        new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left});
                }
                else hud.deliver.onClick.Invoke();
                yield return null;
                Assert.That(receipts,Is.EqualTo(1));
                Assert.That(hud.receiptPanel.activeInHierarchy,Is.True);
                Assert.That(hud.receiptDetails.text,Is.EqualTo(w.State.production.lastDelivery));
                Assert.That(receipt.money,Is.EqualTo(w.State.money-money));
                Assert.That(receipt.reputation,Is.EqualTo(w.State.reputation-reputation));
                Assert.That(receipt.feedback,Is.EqualTo(CafeFeedbackKind.DeliverySatisfied));
                Assert.That(hud.receiptGrade.text,Does.Contain(receipt.GradeLabel));
                Assert.That(app.InputBlocked,Is.True);
                Assert.That(app.DeliverCup().success,Is.False);
                Assert.That(receipts,Is.EqualTo(1));
                hud.confirmReceipt.onClick.Invoke();yield return null;
                Assert.That(app.InputBlocked,Is.False);
                Assert.That(hud.receiptPanel.activeSelf,Is.False);
            }
            finally {app.CupDelivered-=listener;}
        }
        [UnityTest] public IEnumerator OrderRecipeTargetsFocusedOrderAndStackFollowsRealOrderCount()
        {
            var w=app.World;
            w.DebugSpawnCustomers(3);
            app.DebugRefreshViews();yield return null;
            Assert.That(hud.stackedOrderSheets[0].activeSelf,Is.True);
            string first=w.State.focusedRequestId;
            hud.nextOrder.onClick.Invoke();yield return null;
            Assert.That(w.State.focusedRequestId,Is.Not.EqualTo(first));
            var product=w.Product(w.Request(w.State.focusedRequestId).productId);
            hud.orderRecipe.onClick.Invoke();yield return null;
            Assert.That(hud.bookPanel.activeInHierarchy,Is.True);
            var book=hud.bookPanel.GetComponent<CoffeeCompendiumPanel>();
            Assert.That(book.CurrentTab,Is.EqualTo(CoffeeCompendiumPanel.Tab.Recipes));
            Assert.That(book.ProductOrder[book.RecipeIndex],Is.EqualTo(product.id),"The order button opens that order's recipe page.");
            book.CloseImmediate();yield return null;
            Assert.That(app.InputBlocked,Is.False);
        }
        [UnityTest] public IEnumerator PatternMeaningAppearsOnHoverAndDisappearsWithoutBlockingDrawing()
        {
            Assert.That(app.World.SelectServingCup("s.cups.small").success,Is.True);
            Assert.That(app.World.RequestProductionMiniGame().success,Is.True);
            app.DebugRefreshViews();
            yield return null;
            var bench=Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            float deadline=Time.realtimeSinceStartup+3;
            while(bench.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(bench.IsTransitioning,Is.False);
            var overlay=GameObject.Find("LatteArtPresetOverlay").transform;
            var crown=overlay.Find("Content/PresetPanel/PresetList/sword_crownButton");
            var data=new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.2f,Screen.height*.4f)};
            ExecuteEvents.Execute(crown.gameObject,data,ExecuteEvents.pointerEnterHandler);
            var hint=overlay.Find("Content/PatternMeaning");
            Assert.That(hint.gameObject.activeInHierarchy,Is.True);
            Assert.That(hint.Find("Meaning").GetComponent<TMP_Text>().text,Does.Contain("王庭").And.Not.Contain("占位"));
            foreach(var graphic in hint.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget,Is.False);
            ExecuteEvents.Execute(crown.gameObject,data,ExecuteEvents.pointerExitHandler);
            Assert.That(hint.gameObject.activeSelf,Is.False);
            app.World.ResolveProductionMiniGame(app.World.State.production.miniGameToken,false,0);
        }
        [UnityTest] public IEnumerator GrinderHoverUsesRealProgressAndNeverCapturesDrops()
        {
            var w=app.World;
            Assert.That(w.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            Assert.That(w.OperateProduction("m.grinder").success,Is.True);
            var grinder=w.Find("m.grinder");
            float duration=w.MachineProgress(grinder).duration;
            w.Tick(duration*.35f);
            app.Inspect(grinder.id);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(w.MachineProgress(grinder).Fraction,Is.EqualTo(.35f).Within(.001f));
            Image fill=null;
            foreach(var image in app.ContainerPanels.GetComponentsInChildren<Image>())
                if(image.name=="ProgressFill") fill=image;
            Assert.That(fill,Is.Not.Null);
            Assert.That(fill.rectTransform.anchorMax.x,Is.EqualTo(.35f).Within(.001f));
            Assert.That(app.ContainerPanels.GetComponentInChildren<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(app.ContainerPanels.GetComponentInChildren<GraphicRaycaster>(),Is.Null);
        }
    }
}
