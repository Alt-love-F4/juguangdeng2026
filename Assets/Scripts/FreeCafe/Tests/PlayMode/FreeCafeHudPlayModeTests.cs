using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeHudPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeHud hud;
        private DayFlowController tutorialFlow;
        private GameObject overlayOwner;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;hud=Object.FindFirstObjectByType<FreeCafeHud>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(tutorialFlow!=null)
            {
                app.FinishCafeTutorial();
                tutorialFlow.HideForMainMenu();
                Object.Destroy(tutorialFlow);
            }
            if(overlayOwner!=null)
            {
                overlayOwner.GetComponent<GameFlowBootstrap>().CloseMap();
                var session=typeof(GameFlowBootstrap).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(overlayOwner.GetComponent<GameFlowBootstrap>());
                session?.GetType().GetMethod("Dispose").Invoke(session,null);
                Object.Destroy(overlayOwner);
            }
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            Time.timeScale=1;yield return null;
        }
        [UnityTest] public IEnumerator LobbyArtworkHudShowsLiveResourcesAndPagesRealOrders()
        {
            var art=hud.playerRoot.transform.Find("CafeLobbyHud");
            Assert.That(art,Is.Not.Null);
            Assert.That(art.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
            Assert.That(art.GetComponentsInChildren<Canvas>(true),Is.Empty);
            Assert.That(hud.playerRoot.transform.Find("CafeLedger"),Is.Null);
            Assert.That(hud.playerRoot.transform.Find("GuestTickets"),Is.Null);
            foreach(var image in art.GetComponentsInChildren<Image>(true))
            {
                Assert.That(image.name,Does.Not.Contain("行动点"));
                Assert.That(image.name,Is.Not.EqualTo("图层 1"));
            }
            Assert.That(hud.orderPage.text,Is.EqualTo("暂无订单"));
            Assert.That(hud.previousOrder.interactable,Is.False);
            Assert.That(hud.nextOrder.interactable,Is.False);
            Assert.That(hud.moneyLabel.text,Is.EqualTo(app.World.State.money.ToString()));
            Assert.That(hud.reputationLabel.text,Is.EqualTo(app.World.State.reputation.ToString()));
            Assert.That(app.World.StartDay().success,Is.True);
            if(app.World.State.requests.Count<2) app.World.DebugSpawnCustomers(2-app.World.State.requests.Count);
            Assert.That(app.World.State.requests.Count,Is.GreaterThanOrEqualTo(2));
            app.World.DebugAddMoney(17);
            app.World.DebugAddReputation(3);
            app.DebugRefreshViews();
            yield return null;
            Assert.That(hud.moneyLabel.text,Is.EqualTo(app.World.State.money.ToString()));
            Assert.That(hud.reputationLabel.text,Is.EqualTo(app.World.State.reputation.ToString()));
            Assert.That(hud.orderPage.text,Does.StartWith("01 /"));
            ClickHudButton(hud.nextOrder);
            Assert.That(hud.orderPage.text,Does.StartWith("02 /"));
            ClickHudButton(hud.orderButtons[0]);
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(app.World.State.requests[1].id));
            ClickHudButton(hud.previousOrder);
            ClickHudButton(hud.orderButtons[0]);
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(app.World.State.requests[0].id));
            yield return new WaitForEndOfFrame();
            var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
            var directory=System.IO.Path.Combine(Application.dataPath,"../Logs/GameplayHud");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"cafe-lobby-artwork-orders.png"),screenshot.EncodeToPNG());
            Object.Destroy(screenshot);
            app.World.State.requests.Clear();
            app.DebugRefreshViews();
            Assert.That(hud.orderPage.text,Is.EqualTo("暂无订单"));
            Assert.That(hud.orderButtons[0].interactable,Is.False);
            Assert.That(hud.nextOrder.interactable,Is.False);
            app.Enter(AreaId.Counter);
            yield return null;
            Assert.That(hud.playerRoot.activeSelf,Is.True);
            Assert.That(GameplayHudPanel.Instance.IsVisible,Is.False);
            app.Enter(AreaId.Cafe);
            yield return null;
            Assert.That(hud.playerRoot.activeSelf,Is.True);
        }

        private static void ClickHudButton(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var pointer=new PointerEventData(EventSystem.current) {position=point,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits,Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),Is.EqualTo(button.gameObject),button.name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }

        [UnityTest] public IEnumerator WorkstationReusesOrderPanelAndOnlyShowsProductionMenus()
        {
            Assert.That(app.World.StartDay().success,Is.True);
            if(app.World.State.requests.Count<2) app.World.DebugSpawnCustomers(2-app.World.State.requests.Count);
            app.DebugRefreshViews();
            var panel=hud.playerRoot.transform.Find("CafeLobbyHud");
            var order=hud.orderButtons[0];
            ClickHudButton(hud.nextOrder);
            ClickHudButton(order);
            string focused=app.World.State.focusedRequestId;
            ClickWorkstationPortal();
            yield return null;yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();
            Assert.That(hud.playerRoot.activeSelf,Is.True);
            Assert.That(hud.playerRoot.transform.Find("CafeLobbyHud"),Is.SameAs(panel));
            Assert.That(hud.orderButtons[0],Is.SameAs(order));
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(focused));
            Assert.That(GameplayHudPanel.Instance.IsVisible,Is.False);
            Assert.That(panel.Find("HeaderDock/Resources").gameObject.activeSelf,Is.False);
            Assert.That(panel.Find("FloorSwitcher").gameObject.activeSelf,Is.False);
            Assert.That(panel.Find("HeaderDock/SecondaryMenus").gameObject.activeSelf,Is.True);
            Assert.That(production.productionRoot.transform.Find("ProductionHeader/OrderTicket"),Is.Null);
            Assert.That(production.previousOrder,Is.Null);
            Assert.That(production.nextOrder,Is.Null);
            int next=(app.World.State.requests.FindIndex(request=>request.id==focused)+1)%app.World.State.requests.Count;
            ClickHudButton(hud.nextOrder);
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(app.World.State.requests[next].id),"The shared arrows must select the actual delivery order in production.");
            Assert.That(hud.orderPage.text,Does.StartWith((next+1).ToString("00")+" /"));
            ClickHudButton(production.orderRecipe);
            yield return null;
            Assert.That(production.IsModalOpen,Is.True);
            yield return CloseBook(Compendium(production.bookPanel));
            yield return new WaitForEndOfFrame();
            var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
            var directory=System.IO.Path.Combine(Application.dataPath,"../Logs/GameplayHud");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"cafe-workstation-shared-orders.png"),screenshot.EncodeToPNG());
            Object.Destroy(screenshot);
            ClickHudButton(production.returnButton);
            yield return null;yield return null;
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Cafe));
            Assert.That(GameplayHudPanel.Instance.IsVisible,Is.True);
            Assert.That(panel.Find("HeaderDock/Resources").gameObject.activeSelf,Is.True);
            Assert.That(panel.Find("FloorSwitcher").gameObject.activeSelf,Is.True);
            Assert.That(panel.Find("HeaderDock/SecondaryMenus").gameObject.activeSelf,Is.False);
        }

        private void ClickWorkstationPortal()
        {
            foreach(var portal in Object.FindObjectsByType<FreeCafePortal>(FindObjectsSortMode.None))
                if(portal.runtime==app && portal.destination==AreaId.Counter && app.IsCurrentAreaTransform(portal.transform))
                {
                    portal.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
                    return;
                }
            Assert.Fail("The scene must retain its workstation entry after removing the old HUD.");
        }

        [UnityTest] public IEnumerator LobbyHeaderFitsTopSpaceAndFloorButtonsNavigateTheExistingWorld()
        {
            yield return null;
            var shared=GameplayHudPanel.Instance;
            Assert.That(shared,Is.Not.Null);
            Assert.That(shared.Artwork.localScale.x,Is.EqualTo(.75f).Within(.01f));
            var art=hud.playerRoot.transform.Find("CafeLobbyHud");
            foreach(string old in new[]{"PlayerNavigation","PlayerTools","CafeTools","CafeNote","OpenCityMap"})
                Assert.That(hud.playerRoot.transform.Find(old),Is.Null,old);
            Assert.That(hud.mapButton,Is.Null);
            var orders=(RectTransform)art.Find("HeaderDock/Orders");
            var resources=(RectTransform)art.Find("HeaderDock/Resources");
            Canvas.ForceUpdateCanvases();
            var orderCorners=new Vector3[4];var headerCorners=new Vector3[4];var resourceCorners=new Vector3[4];
            orders.GetWorldCorners(orderCorners);shared.Artwork.GetWorldCorners(headerCorners);resources.GetWorldCorners(resourceCorners);
            Assert.That(orderCorners[2].x,Is.LessThan(headerCorners[0].x),"Order paper must not overlap the shared header");
            Assert.That(headerCorners[2].x,Is.LessThan(resourceCorners[0].x),"Currency must not overlap the shared header");
            Assert.That(orderCorners[0].y,Is.GreaterThan(Screen.height*.8f));
            Assert.That(resourceCorners[0].y,Is.GreaterThan(Screen.height*.8f));
            var world=app.World;int sceneHandle=app.gameObject.scene.handle;
            Assert.That(hud.endDayButton,Is.Not.Null);
            Assert.That(hud.endDayButton.gameObject.activeSelf,Is.False,"The first-floor lobby must not show the next-day shortcut");
            var first=hud.areaButtons[(int)AreaId.Cafe];var second=hud.areaButtons[(int)AreaId.TopFloor];var third=hud.areaButtons[(int)AreaId.Basement];
            Assert.That(first.interactable,Is.False);
            Assert.That(((Image)first.targetGraphic).sprite,Is.SameAs(hud.floorCurrentSprite));
            ClickHudButton(second);
            yield return null;
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.TopFloor));
            Assert.That(app.areaManager.CurrentArea,Is.EqualTo(AreaId.TopFloor));
            Assert.That(hud.endDayButton.gameObject.activeSelf,Is.False,"The second floor must not show the next-day shortcut");
            Assert.That(app.worldCamera.transform.position.y,Is.EqualTo(app.areaManager.Find(AreaId.TopFloor).cameraAnchor.position.y+app.areaManager.cameraOffset.y).Within(.01f));
            Assert.That(second.interactable,Is.False);
            Assert.That(((Image)second.targetGraphic).sprite,Is.SameAs(hud.floorCurrentSprite));
            Time.timeScale=0;
            ClickHudButton(third);
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.TopFloor),"Floor navigation must respect the existing pause gate");
            Time.timeScale=1;app.DebugRefreshViews();
            ClickHudButton(third);
            yield return null;
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.Basement));
            Assert.That(app.areaManager.CurrentArea,Is.EqualTo(AreaId.Basement));
            Assert.That(hud.endDayButton.gameObject.activeInHierarchy,Is.True,"Only the third-floor dormitory shows the next-day shortcut");
            ClickHudButton(first);
            yield return null;
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.Cafe));
            Assert.That(hud.endDayButton.gameObject.activeSelf,Is.False,"Leaving the third floor must hide the next-day shortcut");
            Assert.That(app.World,Is.SameAs(world));
            Assert.That((int)app.gameObject.scene.handle,Is.EqualTo(sceneHandle));
            ClickWorkstationPortal();
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.Counter));
            Assert.That(hud.endDayButton.gameObject.activeSelf,Is.False,"The workstation must not show the next-day shortcut");
        }

        [UnityTest] public IEnumerator RemovedPlayerHudWidgetsDoNotBlockStartupOrRemainingControls()
        {
            hud.enabled=false;
            if(hud.pauseButton==null) hud.pauseButton=hud.saveButton;
            if(hud.recipeButton==null) hud.recipeButton=hud.orderButtons[0];
            foreach(string field in new[]{"pauseButton","summary","hint","selection","areaName"}) RemoveHudWidget(hud,field);
            var secondFloor=hud.areaButtons[(int)AreaId.TopFloor];
            hud.areaButtons=new Button[]{null,null,secondFloor};
            hud.orderButtons=new Button[6];
            hud.orderLabels=new TMP_Text[1];
            for(int attempt=0;attempt<2;attempt++) { hud.enabled=true;hud.enabled=false; }
            hud.enabled=true;
            Assert.DoesNotThrow(()=>typeof(FreeCafeHud).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null));
            hud.recipeButton.onClick.Invoke();
            Assert.That(hud.IsModalOpen,Is.True,"Removing pause and text widgets must not disable remaining recipe bindings");
            hud.CloseRecipeTips();
            secondFloor.onClick.Invoke();
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.TopFloor));
            app.Enter(AreaId.Cafe);
            ClickWorkstationPortal();
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            Assert.That(app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            app.DebugRefreshViews();
            yield return null;
            Assert.That(hud.pauseButton==null,Is.True,"Missing UI must not be recreated");
        }
        [UnityTest] public IEnumerator OrderRowsCanBeSparseAndDifferInLengthWithoutLosingTheirClickBindings()
        {
            hud.enabled=false;
            var orderButton=hud.orderButtons[0];
            hud.orderButtons=new Button[]{null,orderButton};
            hud.orderLabels=System.Array.Empty<TMP_Text>();
            hud.enabled=true;
            Assert.That(app.World.StartDay().success,Is.True);
            if(app.World.State.requests.Count<2)
            {
                var spawned=app.World.DebugSpawnCustomers(2-app.World.State.requests.Count);
                Assert.That(spawned.success,Is.True,spawned.message);
            }
            Assert.That(app.World.State.requests.Count,Is.GreaterThanOrEqualTo(2));
            string expected=app.World.State.requests[1].id;
            app.DebugRefreshViews();
            orderButton.onClick.Invoke();
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(expected));
            hud.enabled=false;
            hud.areaButtons=null;hud.orderButtons=null;hud.orderLabels=null;
            hud.enabled=true;
            app.DebugRefreshViews();
            yield return null;
        }
        [UnityTest] public IEnumerator RemovedProductionHudWidgetsKeepReturnAndWorldSimulationWorking()
        {
            var production=app.GetComponent<FreeCafeProductionHud>();
            production.enabled=false;
            foreach(string field in new[]{"pauseButton","orderTitle","customerName","customerAvatar","orderDescription","cupStatus","instruction","result","bookPanel","miniGamePanel","previousOrder","nextOrder","clearMachine","smallCup","largeCup","discard"})
                RemoveHudWidget(production,field);
            production.enabled=true;
            Assert.DoesNotThrow(()=>typeof(FreeCafeProductionHud).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(production,null));
            app.Enter(AreaId.Counter);
            production.bookButton.onClick.Invoke();
            Assert.That(production.IsModalOpen,Is.False);
            Assert.That(app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            Assert.That(app.World.OperateProduction("m.grinder").success,Is.True);
            app.World.Tick(20);
            Assert.That(app.World.Find("m.grinder").production.phase,Is.EqualTo(ProductionPhase.Ready));
            production.returnButton.onClick.Invoke();
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Cafe));
            yield return null;
        }
        [UnityTest] public IEnumerator AllHudDisplayReferencesCanBeEmptyWithoutLockingGameplay()
        {
            var production=app.GetComponent<FreeCafeProductionHud>();
            hud.enabled=false;production.enabled=false;
            ClearHudDisplayReferences(hud);ClearHudDisplayReferences(production);
            hud.enabled=true;production.enabled=true;
            Assert.DoesNotThrow(()=>typeof(FreeCafeHud).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null));
            Assert.DoesNotThrow(()=>typeof(FreeCafeProductionHud).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(production,null));
            Assert.That(app.World.StartDay().success,Is.True);
            app.Enter(AreaId.Counter);
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            app.World.Tick(1);
            app.ReturnFromProduction();
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Cafe));
            Assert.That(app.World.State.open,Is.True);
            Assert.That(app.InputBlocked,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator HidingHudClosesOwnedBooksAndReleasesGameplayInput()
        {
            typeof(FreeCafeHud).GetMethod("ToggleRecipeTips",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
            Assert.That(hud.IsModalOpen,Is.True);
            Assert.That(app.InputBlocked,Is.True);
            hud.enabled=false;
            Assert.That(hud.IsModalOpen,Is.False);
            Assert.That(app.InputBlocked,Is.False);
            hud.enabled=true;
            app.Enter(AreaId.Counter);
            var production=app.GetComponent<FreeCafeProductionHud>();
            production.bookButton.onClick.Invoke();
            Assert.That(production.IsModalOpen,Is.True);
            Assert.That(app.InputBlocked,Is.True);
            production.enabled=false;
            Assert.That(production.IsModalOpen,Is.False);
            Assert.That(app.InputBlocked,Is.False);
            production.enabled=true;
            yield return null;
        }
        private void OpenPlayerCoffeeBook()
        {
            if(hud.recipeButton!=null) hud.recipeButton.onClick.Invoke();
            else typeof(FreeCafeHud).GetMethod("ToggleRecipeTips",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
        }
        private static void RemoveHudWidget(object owner,string fieldName)
        {
            var widget=owner.GetType().GetField(fieldName).GetValue(owner) as Object;
            if(widget==null) return;
            if(widget is Component component) Object.DestroyImmediate(component.gameObject);
            else Object.DestroyImmediate(widget);
        }
        private static void ClearHudDisplayReferences(object owner)
        {
            foreach(var field in owner.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="runtime") continue;
                if(typeof(Object).IsAssignableFrom(field.FieldType) || field.FieldType.IsArray) field.SetValue(owner,null);
            }
        }
        [UnityTest] public IEnumerator PlayerHudHasNoFullWidthBlackBarsOrDebugButtons()
        {
            var topBar=hud.transform.Find("TopBlackBar");
            Assert.That(topBar==null || !topBar.gameObject.activeSelf,Is.True);
            var bottomBar=hud.transform.Find("BottomBlackBar");
            Assert.That(bottomBar==null || !bottomBar.gameObject.activeSelf,Is.True);
            Assert.That(hud.startButton==null || !hud.startButton.gameObject.activeInHierarchy,Is.True);
            Assert.That(hud.saveButton==null || !hud.saveButton.gameObject.activeInHierarchy,Is.True);
            Assert.That(hud.areaButtons[(int)AreaId.TopFloor],Is.Not.Null);
            if(hud.summary!=null)
            { Assert.That(hud.summary.text,Does.Not.Contain("FREE CAFE")); Assert.That(hud.summary.text,Does.Contain("王国币")); }
            Assert.That(hud.playerRoot.GetComponentsInChildren<CafeHudSurface>().Length,Is.EqualTo(0));
            var lobbyArt=hud.playerRoot.transform.Find("CafeLobbyHud");
            Assert.That(lobbyArt,Is.Not.Null);
            Assert.That(System.Array.FindAll(lobbyArt.GetComponentsInChildren<Image>(true),image=>image.sprite!=null).Length,Is.GreaterThanOrEqualTo(5));
            var production=app.GetComponent<FreeCafeProductionHud>();
            app.Enter(AreaId.Counter);yield return null;
            Assert.That(hud.playerRoot.activeSelf,Is.True);
            Assert.That(production.startBusiness==null || !production.startBusiness.gameObject.activeInHierarchy,Is.True);
            Assert.That(production.saveButton==null || !production.saveButton.gameObject.activeInHierarchy,Is.True);
            Assert.That(production.productionRoot.GetComponentsInChildren<Canvas>().Length,Is.EqualTo(0));
        }
        private static CoffeeCompendiumPanel Compendium(GameObject panel) => panel == null ? null : panel.GetComponent<CoffeeCompendiumPanel>();
        private static IEnumerator WaitForBook(CoffeeCompendiumPanel book)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (book.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }
        private static string BookText(CoffeeCompendiumPanel book)
        {
            var text = new System.Text.StringBuilder();
            foreach (var label in book.GetComponentsInChildren<TMP_Text>()) text.AppendLine(label.text);
            return text.ToString();
        }
        private static IEnumerator CloseBook(CoffeeCompendiumPanel book)
        {
            book.closeButton.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (book.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(book.IsOpen, Is.False);
        }
        [UnityTest] public IEnumerator BothCoffeeBooksUseLatteArtRecipeTermsWhilePreservingRawIngredientLabels()
        {
            var world=app.World;
            var product=world.Catalog.products[0];
            product.label="奶泡饮品";product.description="精粹咖啡与奶沫";product.tutorial="配方种类：奶泡";
            product.ingredients=new[]{new Portion{specId="milkfoam",units=1,milliliters=20}};
            world.State.unlockedProducts.Clear();world.State.unlockedProducts.Add(product.id);
            string before=JsonUtility.ToJson(world.Catalog);
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();production.bookButton.onClick.Invoke();
            var book=Compendium(production.bookPanel);yield return WaitForBook(book);
            book.ShowRecipe(System.Linq.Enumerable.ToList(book.ProductOrder).IndexOf(product.id));yield return WaitForBook(book);
            string text=BookText(book);
            Assert.That(text,Does.Contain("拉花饮品"));
            Assert.That(text,Does.Contain("拉花").And.Contain("20 ml").And.Not.Contain("奶泡").And.Not.Contain("奶沫"));
            yield return CloseBook(book);
            app.Enter(AreaId.Cafe);yield return null;
            OpenPlayerCoffeeBook();
            var lobbyBook=Compendium(hud.recipePanel);yield return WaitForBook(lobbyBook);
            Assert.That(BookText(lobbyBook),Does.Contain("拉花饮品").And.Not.Contain("奶泡").And.Not.Contain("奶沫"));
            Assert.That(world.IngredientLabel(product.ingredients[0]),Does.Match("奶泡|奶沫"));
            Assert.That(JsonUtility.ToJson(world.Catalog),Is.EqualTo(before),"Recipe terminology must not rename authoritative ingredient data");
            yield return CloseBook(lobbyBook);
        }

        [UnityTest] public IEnumerator CoffeeBookBlocksEntityInputAndClosingRestoresIt()
        {
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();production.bookButton.onClick.Invoke();yield return null;
            Assert.That(app.InputBlocked,Is.True);Assert.That(production.bookPanel.activeSelf,Is.True);
            var beans=app.World.Find("s.beans");int stock=beans.stock;
            app.Click(app.View(beans.id));Assert.That(beans.stock,Is.EqualTo(stock));
            yield return CloseBook(Compendium(production.bookPanel));
            Assert.That(app.InputBlocked,Is.False);
            app.Enter(AreaId.Cafe);yield return null;
            OpenPlayerCoffeeBook();yield return null;Assert.That(app.InputBlocked,Is.True);
            yield return CloseBook(Compendium(hud.recipePanel));Assert.That(app.InputBlocked,Is.False);
        }

        [UnityTest] public IEnumerator CoffeeCompendiumReplacesTheOldBookAndUsesOnlyUguiAndTmp()
        {
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();
            foreach(var panel in new[]{production.bookPanel,hud.recipePanel})
            {
                Assert.That(Compendium(panel),Is.Not.Null,panel.name);
                Assert.That(panel.GetComponent<CoffeeBookPanelView>(),Is.Null,"The PSD coffee book is no longer used in CoffeeShop.");
                Assert.That(panel.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
                Assert.That(panel.GetComponentsInChildren<UnityEngine.UI.Text>(true),Is.Empty);
                Assert.That(panel.GetComponentsInChildren<Canvas>(true),Is.Empty);
            }
            Assert.That(Object.FindObjectsByType<CoffeeBookPanelView>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty);
        }

        [UnityTest] public IEnumerator CoffeeBookListsEveryRecipeAndMarksLockedOnes()
        {
            var world=app.World;
            Assert.That(world.Catalog.products.Length,Is.GreaterThan(1));
            world.State.unlockedProducts.Clear();
            world.State.unlockedProducts.Add(world.Catalog.products[0].id);
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();
            production.bookButton.onClick.Invoke();
            var book=Compendium(production.bookPanel);yield return WaitForBook(book);
            Assert.That(book.CurrentTab,Is.EqualTo(CoffeeCompendiumPanel.Tab.Recipes));
            Assert.That(book.RecipeIndex,Is.EqualTo(-1),"Opening without an order shows the table of contents.");
            Assert.That(book.ProductOrder.Count,Is.EqualTo(world.Catalog.products.Length));
            string contents=BookText(book);
            foreach(var product in world.Catalog.products) Assert.That(contents,Does.Contain(FreeCafeProductionText.Recipe(product.label)),product.id);
            Assert.That(contents,Does.Contain("已掌握").And.Contain("未解锁"));
            var locked=System.Array.Find(world.Catalog.products,product=>product.id!=world.Catalog.products[0].id);
            book.ShowRecipe(System.Linq.Enumerable.ToList(book.ProductOrder).IndexOf(locked.id));yield return WaitForBook(book);
            Assert.That(BookText(book),Does.Contain("尚未解锁").And.Contain("制作方式"));
            book.ShowContents();yield return WaitForBook(book);
            Assert.That(book.RecipeIndex,Is.EqualTo(-1));
            yield return CloseBook(book);
        }

        [UnityTest] public IEnumerator GmActionsWorkWhileUnderlyingWorldInputIsBlocked()
        {
            var gm=FreeCafeGMPanel.Instance;gm.SetVisible(true);gm.SelectCategory("经营流程");yield return null;
            Assert.That(app.InputBlocked,Is.True);FindAction(gm,"开始营业").onClick.Invoke();yield return null;
            Assert.That(app.World.State.open,Is.True);
            gm.SelectCategory("界面与场景");yield return null;FindAction(gm,"直达制作界面").onClick.Invoke();yield return null;
            Assert.That(gm.IsVisible,Is.False);Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            gm.SetVisible(true);gm.SelectCategory("状态诊断");yield return null;
            FindAction(gm,"显示 / 隐藏实体 ID").onClick.Invoke();Assert.That(app.ShowAssetIds,Is.True);
        }
        [UnityTest] public IEnumerator BackquoteStillTogglesExistingGlobalGmPanel()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>("CafeHudGmKeyboard");
            var previous=InputSystem.settings.backgroundBehavior;
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Backquote));yield return null;yield return null;
                Assert.That(FreeCafeGMPanel.Instance.IsVisible,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Backquote));yield return null;yield return null;
                Assert.That(FreeCafeGMPanel.Instance.IsVisible,Is.False);
            }
            finally { InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=previous; }
        }
        [UnityTest] public IEnumerator CardGapsDoNotInterceptWorldRaycasts()
        {
            app.Enter(AreaId.Counter);yield return null;Canvas.ForceUpdateCanvases();
            var pointer=new PointerEventData(EventSystem.current) { position=new Vector2(Screen.width*.72f,Screen.height*.9f) };
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            foreach(var hit in hits) Assert.That(hit.gameObject.GetComponentInParent<CafeHudSurface>(),Is.Null,"卡片间的空间必须可以穿透");
        }
        [UnityTest] public IEnumerator MapOverlayReusesTheSceneAndKeepsCafeObjectsAndScriptsAlive()
        {
            overlayOwner=new GameObject("Inactive map navigation owner");
            overlayOwner.SetActive(false);
            var owner=overlayOwner.AddComponent<GameFlowBootstrap>();
            var schedule=overlayOwner.AddComponent<DayFlowController>();
            schedule.RestoreJson("{\"day\":2,\"state\":5,\"firstDayCafeTutorialCompleted\":true}");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(GameFlowBootstrap).GetMethod("SetSessionState",flags).Invoke(owner,new object[]{GameSessionState.Playing});
            typeof(GameFlowBootstrap).GetField("dayFlow",flags).SetValue(owner,schedule);
            typeof(FreeCafeRuntime).GetField("gameFlow",flags).SetValue(app,owner);
            var originalWorld=app.World;
            var originalHeater=originalWorld.Find("m.heater");
            int runtimeId=app.GetInstanceID(), mapId=0, mapHandle=0;
            MapSceneController residentMap=null;
            for(int attempt=0;attempt<2;attempt++)
            {
                typeof(GameFlowBootstrap).GetField("openingMap",flags).SetValue(owner,true);
                yield return (IEnumerator)typeof(GameFlowBootstrap).GetMethod("OpenMapRoutine",flags).Invoke(owner,new object[]{false});
                var map=(MapSceneController)typeof(GameFlowBootstrap).GetField("mapOverlay",flags).GetValue(owner);
                residentMap=map;
                Assert.That(map,Is.Not.Null);
                Assert.That(map.GetComponent<Canvas>().sortingOrder,Is.EqualTo(GameplayHudPanel.SortingOrder-10));
                Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CoffeeShop"));
                Assert.That(SceneManager.GetSceneByName("Map").isLoaded,Is.True);
                Assert.That(app.enabled,Is.True);
                Assert.That(app.gameObject.activeInHierarchy,Is.True);
                Assert.That(hud.gameObject.activeInHierarchy,Is.True);
                if(attempt==0) { mapId=map.GetInstanceID();mapHandle=map.gameObject.scene.handle; }
                else
                {
                    Assert.That((int)map.GetInstanceID(),Is.EqualTo(mapId),"Map panel instance must be reused");
                    Assert.That((int)map.gameObject.scene.handle,Is.EqualTo(mapHandle),"Map scene must stay loaded");
                }
                typeof(MapSceneController).GetMethod("StartCafe",flags).Invoke(map,null);
                Assert.That(owner.IsMapOpen,Is.False);
                Assert.That(map.gameObject.activeSelf,Is.False);
                Assert.That((int)app.GetInstanceID(),Is.EqualTo(runtimeId),"Cafe runtime instance must survive the overlay");
                Assert.That(app.World,Is.SameAs(originalWorld));
                Assert.That(app.World.Find("m.heater"),Is.SameAs(originalHeater));
                Assert.That(SceneManager.GetSceneByName("Map").isLoaded,Is.True);
            }
            typeof(MapSceneController).GetMethod("OpenPanel",flags).Invoke(residentMap,new object[]{owner});
            Assert.That(residentMap.gameObject.activeSelf,Is.True);
            Assert.That(residentMap.transform.Find("CloseMapOverlay").gameObject.activeSelf,Is.False);
            typeof(GameFlowBootstrap).GetMethod("HideResidentMap",flags).Invoke(owner,null);
            Assert.That(residentMap.gameObject.activeSelf,Is.False);
            Assert.That(SceneManager.GetSceneByName("Map").isLoaded,Is.True);
            residentMap.RegisterAsGameplayPanel();
            Assert.That(GameplayPanelHooks.PanelType,Is.EqualTo(typeof(MapSceneController)));
            typeof(GameFlowBootstrap).GetMethod("RegisterGameplayPanel",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{SceneManager.GetActiveScene()});
            Assert.That(GameplayPanelHooks.PanelType,Is.EqualTo(typeof(FreeCafeHud)));
        }
        [UnityTest] public IEnumerator DisablingTutorialRestoresOnlyDataUntilReenabledWithoutRecreatingDestroyedViews()
        {
            app.Enter(AreaId.Counter);
            var guide=app.WorkstationTutorial;
            int beans=app.World.Find("s.beans").stock;
            Assert.That(guide.Begin(false),Is.True);
            Assert.That(app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            Object.DestroyImmediate(app.View("m.grinder").gameObject);
            int viewCount=Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            guide.enabled=false;
            Assert.That(guide.IsRunning,Is.False);
            Assert.That(app.World.Find("s.beans").stock,Is.EqualTo(beans));
            Assert.That(app.View("m.grinder")==null,Is.True,"OnDisable must never reconstruct views during scene destruction");
            Assert.That(Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(viewCount));
            guide.enabled=true;
            Assert.That(app.View("m.grinder"),Is.Not.Null);
            Assert.That(app.World.Find("m.grinder").contents,Is.Empty);
            yield return null;
        }
        [UnityTest] public IEnumerator DisabledRuntimeCannotSpawnViewsAndResynchronizesWhenReenabled()
        {
            app.enabled=false;
            Object.DestroyImmediate(app.View("m.grinder").gameObject);
            app.DebugRefreshViews();
            Assert.That(app.View("m.grinder")==null,Is.True,"A disabled runtime must not respawn scene objects");
            app.enabled=true;
            Assert.That(app.View("m.grinder"),Is.Not.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator QuittingDuringTutorialCannotRecreateDestroyedViews()
        {
            app.Enter(AreaId.Counter);
            Assert.That(app.WorkstationTutorial.Begin(false),Is.True);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(FreeCafeRuntime).GetMethod("OnApplicationQuit",flags).Invoke(app,null);
            try
            {
                Object.DestroyImmediate(app.View("m.grinder").gameObject);
                app.WorkstationTutorial.Stop();
                app.DebugRefreshViews();
                Assert.That(app.View("m.grinder")==null,Is.True,"Quitting must suppress even explicit tutorial restore and refresh calls");
            }
            finally
            {
                typeof(FreeCafeRuntime).GetField("quitting",flags).SetValue(app,false);
                app.DebugRefreshViews();
            }
            yield return null;
        }
        [UnityTest] public IEnumerator UnloadingSceneDuringTutorialLeavesNoRuntimeEntitiesOrGuidance()
        {
            app.Enter(AreaId.Counter);
            Assert.That(app.WorkstationTutorial.Begin(false),Is.True);
            Assert.That(app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            yield return SceneManager.LoadSceneAsync("Map");
            yield return null;
            Assert.That(Object.FindObjectsByType<FreeEntityView>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty);
            Assert.That(Object.FindObjectsByType<FreeCafeTutorialPanel>(FindObjectsInactive.Include,FindObjectsSortMode.None),Is.Empty);
            Assert.That(Object.FindFirstObjectByType<FreeCafeRuntime>(),Is.Null);
        }
        [UnityTest] public IEnumerator DirectSandboxStaysFreeUntilFirstGuestGmActionIsRequested()
        {
            Assert.That(app.IsSandboxMode,Is.True);
            Assert.That(Object.FindFirstObjectByType<DayFlowController>(),Is.Null);
            Assert.That(app.CafeTutorialActive,Is.False);
            Assert.That(app.World.State.sandboxEquipment,Is.True);
            Assert.That(app.World.HasTutorialCustomer,Is.False);
            Assert.That(app.World.StartDay().success,Is.True);
            int requests=app.World.State.requests.Count;
            Assert.That(app.DebugStartFirstGuestTutorial(out _),Is.False);
            Assert.That(app.World.State.open,Is.True);
            Assert.That(app.World.State.requests.Count,Is.EqualTo(requests));
            Assert.That(Object.FindFirstObjectByType<DayFlowController>(),Is.Null);
            yield return null;
        }

        [UnityTest] public IEnumerator SandboxGmFirstGuestReusesPlayerThenReachesOfficialWorkstationTutorial()
        {
            var player=PlayerRuntime.Current;
            var narrative=Spotlight.Narrative.NarrativeRuntime.Instance;
            int money=app.World.State.money;
            var gm=FreeCafeGMPanel.Instance;
            gm.SetVisible(true);
            Assert.That(gm.SelectCategory("咖啡馆教程"),Is.True);
            yield return null;
            FindAction(gm,"首位客人剧情与完整教学（沙盒）").onClick.Invoke();
            tutorialFlow=app.GetComponent<DayFlowController>();
            Assert.That(tutorialFlow,Is.Not.Null);
            Assert.That(gm.IsVisible,Is.False);
            Assert.That(PlayerRuntime.Current,Is.SameAs(player));
            Assert.That(Spotlight.Narrative.NarrativeRuntime.Instance,Is.SameAs(narrative));
            Assert.That(player.CreationSubmitted,Is.True);
            Assert.That(player.HasSelectedAvatar,Is.True);
            Assert.That(app.World.State.money,Is.EqualTo(money));
            Assert.That(tutorialFlow.State,Is.EqualTo(DayFlowState.FirstDayIntroduction));
            Assert.That(tutorialFlow.CurrentPresentation,Is.Null,"No prologue, letter, date or opening page");
            Assert.That(tutorialFlow.CurrentDialogue.AllowCancel,Is.False);
            Assert.That(tutorialFlow.CanSave,Is.False);
            Assert.That(app.CafeTutorialActive,Is.True);
            Assert.That(app.DebugStartFirstGuestTutorial(out _),Is.False,"Repeated clicks cannot restart the story");

            float deadline=Time.realtimeSinceStartup+30f;
            bool checkedPerception=false;
            narrative.Controller.AbilityCheckEvaluated+=OnCheck;
            void OnCheck(Spotlight.Narrative.NarrativeAbilityCheckSnapshot _) => checkedPerception=true;
            try
            {
                while(tutorialFlow.State!=DayFlowState.FreeCafeBusiness || DialoguePanel.IsAnyPlaying)
                {
                    Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"First guest story did not reach workstation handoff");
                    var dialogue=DialoguePanel.Active;
                    if(dialogue!=null && dialogue.IsPlaying) dialogue.Advance();
                    var presentation=Object.FindFirstObjectByType<Spotlight.Narrative.NarrativePresentationUI>();
                    if(presentation!=null)
                        presentation.transform.Find("Blocker").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                }
            }
            finally { narrative.Controller.AbilityCheckEvaluated-=OnCheck; }
            Assert.That(checkedPerception,Is.True,"Use the real bound player ability check");
            Assert.That(app.World.HasTutorialCustomer,Is.True);
            Assert.That(app.World.State.open,Is.False);
            Assert.That(app.World.State.requests.Count,Is.EqualTo(1));
            Assert.That(app.World.State.sandboxEquipment,Is.False,"Official tutorial owns equipment progression after opt-in");
            Assert.That(app.CafeTutorialWaitingForWorkstation,Is.True);
            Assert.That(app.CanEnterArea(AreaId.Counter),Is.True);
            Assert.That(app.CanEnterArea(AreaId.Cafe),Is.False);
            app.RequestBusinessStart();
            Assert.That(app.World.State.open,Is.False);
            app.Enter(AreaId.Counter);
            deadline=Time.realtimeSinceStartup+15f;
            while(!app.WorkstationTutorial.IsFirstCafeTutorial)
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Workstation lesson did not start");
                if(DialoguePanel.Active!=null && DialoguePanel.Active.IsPlaying) DialoguePanel.Active.Advance();
                yield return null;
            }
            Assert.That(app.WorkstationTutorial.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            Assert.That(tutorialFlow.DebugSkipTutorialToCafe(out _),Is.True,"Existing GM escape also works without Bootstrap");
            Assert.That(app.WorkstationTutorial.IsRunning,Is.False);
            Assert.That(app.CafeTutorialActive,Is.False);
            Assert.That(app.World.HasTutorialCustomer,Is.False);
            Assert.That(app.World.State.open,Is.False);
            Assert.That(app.World.State.money,Is.EqualTo(money));
        }

        [UnityTest] public IEnumerator GmSkipCancelsOfficialGuidanceAndRestoresPreparationWithoutTheFirstCustomer()
        {
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            overlayOwner=new GameObject("Inactive tutorial navigation owner");
            overlayOwner.SetActive(false);
            var owner=overlayOwner.AddComponent<GameFlowBootstrap>();
            typeof(GameFlowBootstrap).GetMethod("SetSessionState",flags).Invoke(owner,new object[]{GameSessionState.Playing});
            tutorialFlow=app.gameObject.AddComponent<DayFlowController>();
            tutorialFlow.RestoreJson("{\"day\":1,\"state\":3,\"firstDayIntroductionCompleted\":true,\"firstDayCafeTutorialStarted\":true}");
            typeof(DayFlowController).GetField("flow",flags).SetValue(tutorialFlow,owner);
            typeof(DayFlowController).GetField("running",flags).SetValue(tutorialFlow,true);
            typeof(GameFlowBootstrap).GetField("dayFlow",flags).SetValue(owner,tutorialFlow);
            typeof(FreeCafeRuntime).GetField("dayFlow",flags).SetValue(app,tutorialFlow);
            typeof(FreeCafeRuntime).GetField("gameFlow",flags).SetValue(app,owner);
            app.PrepareCafeTutorial();
            int beans=app.World.Find("s.beans").stock;
            app.Enter(AreaId.Counter);
            Assert.That(app.BeginCafeTutorialCoffee(),Is.True);
            yield return null;yield return null;
            Assert.That(app.WorkstationTutorial.IsFirstCafeTutorial,Is.True);
            Assert.That(app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            yield return null;
            int day=tutorialFlow.CurrentDay;
            typeof(DayFlowController).GetField("awaitingNarrativeEntry",flags).SetValue(tutorialFlow,true);
            var gm=FreeCafeGMPanel.Instance;
            gm.SetVisible(true);
            Assert.That(gm.SelectCategory("全局调试"),Is.True);
            yield return null;
            FindAction(gm,"跳过所有教程（首杯完成后）").onClick.Invoke();
            yield return null;
            Assert.That(gm.IsVisible,Is.False);
            Assert.That(tutorialFlow.CurrentDay,Is.EqualTo(day));
            Assert.That(tutorialFlow.IsNarrativeEntryActive,Is.False);
            Assert.That(tutorialFlow.State,Is.EqualTo(DayFlowState.FreeCafeBusiness));
            Assert.That(tutorialFlow.IsFirstDayCafeMapGuideActive,Is.False);
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.False);
            Assert.That(app.WorkstationTutorial.IsRunning,Is.False);
            Assert.That(app.CafeTutorialWaitingForWorkstation,Is.False);
            Assert.That(app.BeginCafeTutorialCoffee(),Is.False);
            Assert.That(app.World.State.open,Is.False);
            Assert.That(app.World.State.requests,Is.Empty);
            Assert.That(app.World.Find(CafeWorld.TutorialCustomerId),Is.Null);
            Assert.That(app.World.Find("s.beans").stock,Is.EqualTo(beans));
            Assert.That(tutorialFlow.CurrentDialogue,Is.Null);
            app.Enter(AreaId.Cafe);
            app.RequestBusinessStart();
            Assert.That(app.World.State.open,Is.True);
        }
        [UnityTest] public IEnumerator CafeArrivalGuideFollowsSceneArrowAndHidesForOverlaysAndEntry()
        {
            tutorialFlow=app.gameObject.AddComponent<DayFlowController>();
            tutorialFlow.RestoreJson("{\"day\":1,\"state\":3,\"firstDayIntroductionCompleted\":true,\"firstDayCafeTutorialStarted\":true}");
            typeof(DayFlowController).GetField("running",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(tutorialFlow,true);
            typeof(FreeCafeRuntime).GetField("dayFlow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app,tutorialFlow);
            FreeCafePortal portal=null;
            foreach(var candidate in Object.FindObjectsByType<FreeCafePortal>(FindObjectsSortMode.None))
                if(candidate.runtime==app && candidate.destination==AreaId.Counter && app.IsCurrentAreaTransform(candidate.transform))
                { portal=candidate;break; }
            Assert.That(portal,Is.Not.Null);
            var hudEntry=hud.areaButtons[(int)AreaId.Counter];
            if(hudEntry!=null) hudEntry.gameObject.SetActive(false);
            app.PrepareCafeTutorial();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(app.CafeTutorialWaitingForWorkstation,Is.True);
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Cafe));
            Assert.That(app.TutorialInputBlocked,Is.False);
            var panel=Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(panel,Is.Not.Null);
            Assert.That(panel.gesture.gameObject.activeInHierarchy,Is.True);
            Assert.That(panel.gestureText.text,Does.Contain("点击箭头"));
            Assert.That(panel.instruction.text,Does.Contain("场景中的箭头"));
            Assert.That(panel.shade.enabled,Is.False);
            Assert.That(panel.exitButton.gameObject.activeSelf,Is.False);
            Assert.That(app.WorkstationTutorial.IsRunning,Is.False,"Entry guidance must not reset equipment or start practice");
            Vector3 before=panel.trail[0].rectTransform.position;
            portal.transform.position+=Vector3.left*1.5f;
            yield return null; yield return null;
            Assert.That(panel.trail[0].rectTransform.position.x,Is.LessThan(before.x-20),"Guide must follow the scene arrow");
            var pointer=new PointerEventData(EventSystem.current)
            { position=app.worldCamera.WorldToScreenPoint(portal.visual.bounds.center),button=PointerEventData.InputButton.Left };
            Physics2D.SyncTransforms();
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits,Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<FreeCafePortal>(),Is.SameAs(portal),"Guidance must leave the scene arrow clickable");
            portal.gameObject.SetActive(false);
            yield return null; yield return null;
            Assert.That(panel.canvas.gameObject.activeSelf,Is.False);
            portal.gameObject.SetActive(true);
            yield return null; yield return null;
            Assert.That(panel.canvas.gameObject.activeSelf,Is.True);
            Time.timeScale=0;
            yield return null; yield return null;
            Assert.That(panel.canvas.gameObject.activeSelf,Is.False);
            Time.timeScale=1;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(panel.canvas.gameObject.activeSelf,Is.True);
            var gm=FreeCafeGMPanel.Instance;
            gm.SetVisible(true);
            yield return null; yield return null;
            Assert.That(panel.canvas.gameObject.activeSelf,Is.False);
            gm.SetVisible(false);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(panel.canvas.gameObject.activeSelf,Is.True);
            ExecuteEvents.Execute(portal.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            yield return null; yield return null;
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            Assert.That(panel.canvas.gameObject.activeSelf,Is.False);
            Assert.That(app.BeginCafeTutorialCoffee(),Is.True);
            yield return null; yield return null;
            Assert.That(app.WorkstationTutorial.IsFirstCafeTutorial,Is.True);
            Assert.That(Object.FindFirstObjectByType<FreeCafeTutorialPanel>(),Is.SameAs(panel),"Entry and production guidance must share one panel");
        }

        [UnityTest] public IEnumerator FirstCafeTutorialRequiresWorkstationAndRealBlackCoffeeDeliveryBeforeManualOpening()
        {
            return RunFirstCafeTutorial(false);
        }

        [UnityTest] public IEnumerator FirstCafeTutorialCanFinishWithoutTodoTracking()
        {
            return RunFirstCafeTutorial(true);
        }

        private IEnumerator RunFirstCafeTutorial(bool skipOptionalTracking)
        {
            tutorialFlow=app.gameObject.AddComponent<DayFlowController>();
            tutorialFlow.RestoreJson("{\"day\":1,\"state\":3,\"firstDayIntroductionCompleted\":true,\"firstDayCafeTutorialStarted\":true}");
            typeof(DayFlowController).GetField("running",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(tutorialFlow,true);
            typeof(FreeCafeRuntime).GetField("dayFlow",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(app,tutorialFlow);
            app.PrepareCafeTutorial();
            app.PrepareCafeTutorial();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(app.World.State.open,Is.False);
            Assert.That(app.World.State.requests.Count,Is.EqualTo(1));
            Assert.That(app.CafeTutorialWaitingForWorkstation,Is.True);
            Assert.That(app.CanEnterArea(AreaId.Counter),Is.True);
            Assert.That(app.CanEnterArea(AreaId.Cafe),Is.False);
            app.RequestBusinessStart();
            Assert.That(app.World.State.open,Is.False);
            ClickWorkstationPortal();
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            Assert.That(app.BeginCafeTutorialCoffee(),Is.True);
            yield return null; yield return null;
            var guide=app.WorkstationTutorial;
            var production=app.GetComponent<FreeCafeProductionHud>();
            Assert.That(guide.IsFirstCafeTutorial,Is.True);
            Assert.That(production.smallCup.interactable,Is.False);
            Assert.That(production.returnButton.interactable,Is.False);
            Assert.That(app.DeliverCup().success,Is.False);
            production.bookButton.onClick.Invoke();
            yield return null;
            Assert.That(production.IsModalOpen,Is.True);
            Assert.That(guide.IsRunning,Is.True);
            Assert.That(app.BusinessSimulationPaused,Is.True);
            production.CloseOverlays();
            yield return null;
            Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            var world=app.World;
            var tutorialPanel=Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(tutorialPanel.retry.activeInHierarchy,Is.True);
            int beforeRestart=world.State.money;
            tutorialPanel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            Assert.That(guide.IsFirstCafeTutorial,Is.True);
            Assert.That(world.HasTutorialCustomer,Is.True);
            Assert.That(world.State.money,Is.EqualTo(beforeRestart));
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True);
            Assert.That(world.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            yield return null;
            Assert.That(world.OperateProduction("m.grinder").success,Is.True);
            yield return null;
            world.Tick(20); yield return null;
            var output=world.DragOutputPreview("m.grinder");
            Assert.That(world.TakeMachineOutput("m.grinder",output.id,"m.heater",AreaId.Counter,0,0).success,Is.True);
            yield return null;
            Assert.That(world.PourLiquid("s.water", "m.heater", 200, 1).success,Is.True);
            yield return null;
            Assert.That(world.OperateProduction("m.heater").success,Is.True);
            yield return null;
            world.Tick(4.1f); yield return null;
            Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.TakeCup));
            Assert.That(world.Take("s.cups.large",AreaId.Counter,0,0,out var cup).success,Is.True);
            yield return null;
            Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Pour));
            Assert.That(world.PourLiquid("m.heater",cup.id,400,1).success,Is.True);
            typeof(FreeCafeWorkstationTutorial).GetMethod("FinishPourAttempt",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,null);
            Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.DeliverCup));
            var tray=world.State.entities.Find(entity=>entity.area==AreaId.Counter && world.Spec(entity.specId).Has(Capability.Delivery));
            Assert.That(world.Interact(cup.id,tray.id).success,Is.True);
            yield return null;
            Assert.That(guide.CurrentStage,Is.EqualTo(FreeCafeWorkstationTutorial.Stage.SubmitCup));
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True);
            Assert.That(world.HasTutorialCustomer,Is.True);
            typeof(DayFlowController).GetMethod("FindGameplayEndpoint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tutorialFlow,null);
            Assert.That(production.deliver.interactable,Is.False,"Tutorial completion requires the physical bell");
            Assert.That(app.DeliverCup().success,Is.False);
            app.Click(app.View(tray.id));
            Assert.That(guide.IsRunning,Is.True,"Clicking the plate must not complete the tutorial");
            var bell=app.View(tray.id).GetComponentInChildren<FreeCafeDeliveryBell>();
            Assert.That(bell,Is.Not.Null);
            bell.OnPointerClick(new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left});
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True,"The first delivery must hand off to business onboarding, not finish the tutorial");
            Assert.That(guide.IsRunning,Is.False);
            Assert.That(world.State.open,Is.False);
            Assert.That(world.State.requests,Is.Empty);
            Assert.That(world.Find(CafeWorld.TutorialCustomerId),Is.Null);
            var business=app.GetComponent<FreeCafeBusinessTutorial>();
            Assert.That(business,Is.Not.Null);

            void AssertBusinessStage(FreeCafeBusinessTutorial.Stage stage,bool paused)
            {
                Assert.That(business.IsRunning,Is.True);
                Assert.That(business.CurrentStage,Is.EqualTo(stage));
                Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True,"Business onboarding must remain active until order and todo guidance are acknowledged");
                Assert.That(app.CafeBusinessOnboardingActive,Is.True);
                Assert.That(business.PauseBusinessSimulation,Is.EqualTo(paused));
                Assert.That(app.BusinessSimulationPaused,Is.EqualTo(paused));
            }

            Assert.That(production.receiptPanel.activeInHierarchy,Is.True);
            Assert.That(app.TutorialInputBlocked,Is.True);
            business.ConfirmOrderRead();
            app.RequestBusinessStart();
            production.bookButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(production.bookPanel.activeInHierarchy,Is.False,"The receipt must be dismissed before opening the recipe book");
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.OpenRecipe,true);
            Assert.That(world.State.open,Is.False);
            var businessPanel=Object.FindFirstObjectByType<FreeCafeTutorialPanel>(FindObjectsInactive.Include);
            Assert.That(businessPanel,Is.Not.Null);
            Assert.That(businessPanel.canvas.gameObject.activeSelf,Is.False,"Business guidance must not cover the delivery receipt");

            production.confirmReceipt.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(production.receiptPanel.activeInHierarchy,Is.False);
            Assert.That(app.InputBlocked,Is.False);
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.OpenRecipe,true);
            Assert.That(businessPanel.canvas.gameObject.activeInHierarchy,Is.True);
            Assert.That(businessPanel.retry.activeSelf,Is.False,"Acknowledgement is available only after inspecting a real order");
            Assert.That(production.bookPanel.activeInHierarchy,Is.False,"Reading the book during coffee preparation does not satisfy business onboarding");
            business.ConfirmOrderRead();
            app.RequestBusinessStart();
            yield return null; yield return null;
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.OpenRecipe,true);
            Assert.That(world.State.open,Is.False,"Delivering coffee and dismissing the receipt must not unlock business start before reading the book");

            Assert.That(production.bookButton.interactable,Is.True);
            production.bookButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(production.bookPanel.activeInHierarchy,Is.True);
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.CloseRecipe,true);
            Assert.That(business.AllowBusinessStart,Is.False);
            business.ConfirmOrderRead();
            app.RequestBusinessStart();
            yield return null; yield return null;
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.CloseRecipe,true);
            Assert.That(world.State.open,Is.False,"Opening the book alone must not unlock business start");

            var compendium=production.bookPanel.GetComponent<CoffeeCompendiumPanel>();
            Assert.That(compendium.closeButton,Is.Not.Null);
            compendium.closeButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(production.bookPanel.activeInHierarchy,Is.False);
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReturnToCafe,true);
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.Counter));
            Assert.That(business.AllowBusinessStart,Is.False);
            business.ConfirmOrderRead();
            app.RequestBusinessStart();
            Assert.That(world.State.open,Is.False,"Business start must remain blocked until returning to the cafe");
            Assert.That(production.returnButton.interactable,Is.True);
            production.returnButton.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(world.State.currentArea,Is.EqualTo(AreaId.Cafe));
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.StartBusiness,false);
            Assert.That(business.AllowBusinessStart,Is.True);
            Assert.That(world.State.open,Is.False,"Returning to the cafe must not automatically open for business");
            business.ConfirmOrderRead();
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True);

            app.RequestBusinessStart();
            Assert.That(world.State.open,Is.True);
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True,"Opening for business is not the end of onboarding");
            // StartDay can seat customers immediately; otherwise advance the real simulation.
            yield return null; yield return null;
            if(business.CurrentStage==FreeCafeBusinessTutorial.Stage.WaitForCustomer)
            {
                AssertBusinessStage(FreeCafeBusinessTutorial.Stage.WaitForCustomer,false);
                Assert.That(business.AllowBusinessStart,Is.False);
                business.ConfirmOrderRead();
                Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True);
                for(int i=0;i<10 && business.CurrentStage==FreeCafeBusinessTutorial.Stage.WaitForCustomer;i++)
                {
                    world.Tick(world.Catalog.customerInterval);
                    yield return null; yield return null;
                }
            }
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.InspectCustomer,true);
            Assert.That(world.State.requests,Is.Not.Empty);
            var request=world.State.requests.Find(order=>order.id!=CafeWorld.TutorialOrderId && world.Find(order.customerId)!=null);
            Assert.That(request,Is.Not.Null,"Inspection must use a real seated customer with a real order");
            Assert.That(request.id,Is.Not.EqualTo(CafeWorld.TutorialOrderId),"Entity IDs can be reused after the tutorial customer leaves; the order must be a real business order");
            business.ConfirmOrderRead();
            business.CustomerInspected("missing-customer");
            yield return null; yield return null;
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.InspectCustomer,true);
            Assert.That(businessPanel.retry.activeSelf,Is.False);
            float patience=request.patience;
            float businessElapsed=world.State.businessElapsed;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(request.patience,Is.EqualTo(patience),"Customer patience must be paused while teaching inspection");
            Assert.That(world.State.businessElapsed,Is.EqualTo(businessElapsed));
            app.DebugRefreshViews();
            yield return null;
            var customerView=app.View(request.customerId);
            Assert.That(customerView,Is.Not.Null);
            Assert.That(customerView.CanInteract,Is.True);
            Assert.That(app.InputBlocked,Is.False);
            app.Click(customerView);
            Assert.That(world.State.focusedRequestId,Is.EqualTo(request.id));
            Assert.That(business.CurrentStage,Is.EqualTo(FreeCafeBusinessTutorial.Stage.ReadOrder));
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True,"Inspecting the customer must not bypass explicit order acknowledgement");
            // Customer inspection can play a real dialogue; it must block acknowledgement too.
            if(app.TutorialInputBlocked)
            {
                business.ConfirmOrderRead();
                Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True);
                Assert.That(business.IsRunning,Is.True);
            }
            float dialogueDeadline=Time.realtimeSinceStartup+8f;
            while(DialoguePanel.IsAnyPlaying && Time.realtimeSinceStartup<dialogueDeadline)
            {
                foreach(var dialogue in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) dialogue.Skip();
                yield return null;
            }
            Assert.That(DialoguePanel.IsAnyPlaying,Is.False,"Customer dialogue must finish before acknowledging the order");
            yield return null; yield return null;
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReadOrder,true);
            Assert.That(businessPanel.retry.activeInHierarchy,Is.True);
            Assert.That(business.AllowBusinessStart,Is.False);
            Assert.That(app.TutorialInputBlocked,Is.False);
            patience=request.patience;
            businessElapsed=world.State.businessElapsed;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(request.patience,Is.EqualTo(patience),"Order reading must not consume customer patience");
            Assert.That(world.State.businessElapsed,Is.EqualTo(businessElapsed));
            Time.timeScale=0;
            business.ConfirmOrderRead();
            Assert.That(business.IsRunning,Is.True,"A paused presentation must not accept order acknowledgement");
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True);
            Time.timeScale=1;
            businessPanel.retryButton.onClick.Invoke();
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.OpenDetails,true);
            Assert.That(businessPanel.retry.activeSelf,Is.False,"Opening details must require the real HUD button, not acknowledgement");
            Assert.That(businessPanel.canvas.gameObject.activeInHierarchy,Is.True);
            Assert.That(businessPanel.gesture.gameObject.activeInHierarchy,Is.True);
            Assert.That(businessPanel.instruction.text,Does.Contain("Tab"));
            business.ConfirmOrderRead();
            business.ConfirmTodoGuidance();
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.OpenDetails,true);
            var gameplayHud=GameplayHudPanel.Instance;
            Assert.That(gameplayHud,Is.Not.Null);
            Assert.That(gameplayHud.DetailsButton,Is.Not.Null);
            ClickHudButton(gameplayHud.DetailsButton);
            yield return null; yield return null;
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.CloseDetails,true);
            Assert.That(gameplayHud.IsDetailsOpen,Is.True);
            var details=gameplayHud.OpenDetailsPanel;
            Assert.That(details,Is.Not.Null,"The real opened details panel must be publicly accessible");
            var detailsCanvas=details.GetComponent<Canvas>();
            Assert.That(detailsCanvas,Is.Not.Null);
            foreach(var page in new[] {DetailsPanel.Page.MyInfo,DetailsPanel.Page.Relationships,DetailsPanel.Page.World})
            {
                details.SelectPage(page);
                yield return null; yield return null;
                Assert.That(details.CurrentPage,Is.EqualTo(page));
                AssertBusinessStage(FreeCafeBusinessTutorial.Stage.CloseDetails,true);
                Assert.That(businessPanel.canvas.gameObject.activeInHierarchy,Is.True,"Details must not hide the tutorial guidance");
                Assert.That(businessPanel.canvas.sortingOrder,Is.GreaterThan(detailsCanvas.sortingOrder),"Tutorial guidance must remain above details");
                Assert.That(businessPanel.gesture.gameObject.activeInHierarchy,Is.True,"The short hint must remain visible on every details page");
                Assert.That(businessPanel.instruction.text,Does.Contain("Tab"));
                Assert.That(businessPanel.instruction.text,Does.Contain("Esc"));
                string pageHint=page==DetailsPanel.Page.MyInfo ? "属性" : page==DetailsPanel.Page.Relationships ? "人物" : "世界";
                Assert.That(businessPanel.gestureText.text,Does.Contain(pageHint));
                Assert.That(businessPanel.retry.activeSelf,Is.False,"Details guidance must not offer an acknowledgement shortcut");
                Assert.That(request.patience,Is.EqualTo(patience),"Browsing details must not consume customer patience");
                Assert.That(world.State.businessElapsed,Is.EqualTo(businessElapsed));
            }
            business.ConfirmTodoGuidance();
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.CloseDetails,true);
            details.RequestClose();
            float detailsDeadline=Time.realtimeSinceStartup+3f;
            while(gameplayHud.IsDetailsOpen && Time.realtimeSinceStartup<detailsDeadline) yield return null;
            yield return null; yield return null;
            Assert.That(gameplayHud.IsDetailsOpen,Is.False,"The real details panel must finish closing before todo guidance");
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.TodoGuidance,true);
            Assert.That(businessPanel.canvas.gameObject.activeInHierarchy,Is.True);
            Assert.That(businessPanel.instruction.text,Does.Contain("不知道接下来做什么"));
            Assert.That(businessPanel.instruction.text,Does.Contain("按下 T"));
            Assert.That(businessPanel.instruction.text,Does.Contain("上方 HUD"));
            Assert.That(businessPanel.instruction.text,Does.Contain("下一步目标"));
            Assert.That(businessPanel.gesture.gameObject.activeInHierarchy,Is.True,"The todo reminder must visibly point to the tasks entry");
            Assert.That(businessPanel.gestureText.text,Does.Contain("T"));
            Assert.That(businessPanel.gestureText.text,Does.Contain("不知道接下来做什么"));
            Assert.That(businessPanel.retry.activeSelf,Is.False,"Todo guidance cannot be skipped before opening tasks");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(request.patience,Is.EqualTo(patience),"Reading the todo reminder must not consume customer patience");
            Assert.That(world.State.businessElapsed,Is.EqualTo(businessElapsed));
            business.ConfirmOrderRead();
            business.ConfirmTodoGuidance();
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.TodoGuidance,true);
            Assert.That(gameplayHud.TasksButton,Is.Not.Null);
            ClickHudButton(gameplayHud.TasksButton);
            yield return null; yield return null;
            var tasks=gameplayHud.Tasks;
            Assert.That(tasks.IsVisible,Is.True);
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReadTodo,true);
            Assert.That(businessPanel.retry.activeInHierarchy,Is.True);
            Assert.That(businessPanel.retryButton.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("完成教程"));
            Assert.That(businessPanel.gesture.gameObject.activeInHierarchy,Is.True);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(request.patience,Is.EqualTo(patience),"Reading the real tasks panel must not consume customer patience");
            Assert.That(world.State.businessElapsed,Is.EqualTo(businessElapsed));
            const string tutorialTodoId="guide.cafe.tutorial";
            if(skipOptionalTracking)
            {
                Assert.That(tasks.ExpandedId,Is.Not.EqualTo(tutorialTodoId));
                Assert.That(tasks.TrackedId,Is.Not.EqualTo(tutorialTodoId));
                tasks.SetItems(new MapTaskItem[0]);
                yield return null; yield return null;
                Assert.That(tasks.ItemCount,Is.Zero);
                Assert.That(tasks.GetItemButton(tutorialTodoId),Is.Null);
                Assert.That(tasks.GetTrackingButton(tutorialTodoId),Is.Null);
                AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReadTodo,true);
                Assert.That(businessPanel.retry.activeInHierarchy,Is.True);
                Assert.That(businessPanel.retryButton.interactable,Is.True);
                tasks.SetVisible(false);
                yield return null; yield return null;
                Assert.That(tasks.IsVisible,Is.False);
                Assert.That(tasks.TrackedId,Is.Not.EqualTo(tutorialTodoId));
            }
            else
            {
                float todoDeadline=Time.realtimeSinceStartup+3f;
                while(tasks.GetItemButton(tutorialTodoId)==null && Time.realtimeSinceStartup<todoDeadline) yield return null;
                var todoButton=tasks.GetItemButton(tutorialTodoId);
                Assert.That(todoButton,Is.Not.Null,"The real tutorial task row must be available for optional tracking");
                todoButton.onClick.Invoke();
                yield return null; yield return null;
                Assert.That(tasks.ExpandedId,Is.EqualTo(tutorialTodoId));
                var trackingButton=tasks.GetTrackingButton(tutorialTodoId);
                Assert.That(trackingButton,Is.Not.Null);
                Assert.That(businessPanel.gestureText.text,Does.Contain("追踪此待办"));
                trackingButton.onClick.Invoke();
                yield return null; yield return null;
                Assert.That(tasks.TrackedId,Is.EqualTo(tutorialTodoId));
            }
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReadTodo,true);
            Assert.That(businessPanel.retry.activeInHierarchy,Is.True,"Optional task tracking and closing tasks must not hide tutorial completion");
            Assert.That(businessPanel.retryButton.interactable,Is.True);
            Assert.That(businessPanel.retryButton.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("完成教程"));
            Assert.That(request.patience,Is.EqualTo(patience),"Optional task tracking must not consume customer patience");
            Assert.That(world.State.businessElapsed,Is.EqualTo(businessElapsed));
            business.ConfirmOrderRead();
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReadTodo,true);
            Time.timeScale=0;
            business.ConfirmTodoGuidance();
            AssertBusinessStage(FreeCafeBusinessTutorial.Stage.ReadTodo,true);
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.True,"A paused presentation must not accept todo acknowledgement");
            Time.timeScale=1;
            ClickHudButton(businessPanel.retryButton);
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.False);
            Assert.That(app.CafeBusinessOnboardingActive,Is.False);
            Assert.That(business.IsRunning,Is.False);
            Assert.That(world.State.open,Is.True,"Completing onboarding must preserve the manually started business");
            Assert.That(world.Request(request.id),Is.SameAs(request),"Acknowledging the order must not serve or remove it");
            yield return null; yield return null;
            Assert.That(businessPanel==null,Is.True,"The completed business guide must release its panel");
            Assert.That(app.BusinessSimulationPaused,Is.False);
            business.ConfirmOrderRead();
            business.ConfirmTodoGuidance();
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.False,"Repeated acknowledgement must not restart onboarding");
            if(skipOptionalTracking)
            {
                // Do not leak the test-only SetItems override through the reusable HUD.
                YokiFrame.UIKit.ClosePanel<GameplayHudPanel>();
                YokiFrame.UIKit.UnloadPanel<GameplayHudPanel>();
                yield return null;
            }
        }
        private static Button FindAction(FreeCafeGMPanel gm,string label)
        {
            foreach(var button in gm.GetComponentsInChildren<Button>())
            { var text=button.GetComponentInChildren<TMP_Text>();if(text!=null && text.text.StartsWith(label+"\n")) return button; }
            Assert.Fail("GM action missing: "+label);return null;
        }
    }
}
