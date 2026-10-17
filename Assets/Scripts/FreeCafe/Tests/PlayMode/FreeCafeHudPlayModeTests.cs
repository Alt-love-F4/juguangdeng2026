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
            Assert.That(hud.playerRoot.activeSelf,Is.False);
            Assert.That(GameplayHudPanel.Instance.IsVisible,Is.True);
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

        [UnityTest] public IEnumerator RemovedPlayerHudWidgetsDoNotBlockStartupOrRemainingControls()
        {
            hud.enabled=false;
            if(hud.pauseButton==null) hud.pauseButton=hud.saveButton;
            if(hud.recipeButton==null) hud.recipeButton=hud.orderButtons[0];
            foreach(string field in new[]{"pauseButton","summary","hint","selection","areaName"}) RemoveHudWidget(hud,field);
            var counter=hud.areaButtons[(int)AreaId.Counter];
            hud.areaButtons=new Button[]{null,counter};
            hud.orderButtons=new Button[6];
            hud.orderLabels=new TMP_Text[1];
            for(int attempt=0;attempt<2;attempt++) { hud.enabled=true;hud.enabled=false; }
            hud.enabled=true;
            Assert.DoesNotThrow(()=>typeof(FreeCafeHud).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null));
            hud.recipeButton.onClick.Invoke();
            Assert.That(hud.IsModalOpen,Is.True,"Removing pause and text widgets must not disable remaining recipe bindings");
            hud.CloseRecipeTips();
            counter.onClick.Invoke();
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
            Assert.That(hud.areaButtons[(int)AreaId.TopFloor],Is.Null);
            if(hud.summary!=null)
            { Assert.That(hud.summary.text,Does.Not.Contain("FREE CAFE")); Assert.That(hud.summary.text,Does.Contain("王国币")); }
            Assert.That(hud.playerRoot.GetComponentsInChildren<CafeHudSurface>().Length,Is.GreaterThan(0));
            var lobbyArt=hud.playerRoot.transform.Find("CafeLobbyHud");
            Assert.That(lobbyArt,Is.Not.Null);
            Assert.That(System.Array.FindAll(lobbyArt.GetComponentsInChildren<Image>(true),image=>image.sprite!=null).Length,Is.GreaterThanOrEqualTo(5));
            var production=app.GetComponent<FreeCafeProductionHud>();
            app.Enter(AreaId.Counter);yield return null;
            Assert.That(hud.playerRoot.activeSelf,Is.False);
            Assert.That(production.startBusiness==null || !production.startBusiness.gameObject.activeInHierarchy,Is.True);
            Assert.That(production.saveButton==null || !production.saveButton.gameObject.activeInHierarchy,Is.True);
            Assert.That(production.productionRoot.GetComponentsInChildren<Canvas>().Length,Is.EqualTo(0));
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
            var production=app.GetComponent<FreeCafeProductionHud>();production.bookButton.onClick.Invoke();yield return null;
            Assert.That(production.bookTitle.text,Does.Contain("拉花饮品"));
            Assert.That(production.bookBody.text,Does.Contain("精粹咖啡与拉花").And.Contain("拉花  20 ml").And.Not.Contain("奶泡").And.Not.Contain("奶沫"));
            production.closeBook.onClick.Invoke();
            app.Enter(AreaId.Cafe);yield return null;
            OpenPlayerCoffeeBook();yield return null;
            Assert.That(hud.recipeTitle.text,Does.Contain("拉花饮品"));
            Assert.That(hud.recipeBody.text,Does.Contain("配方种类：拉花").And.Not.Contain("奶泡").And.Not.Contain("奶沫"));
            Assert.That(world.IngredientLabel(product.ingredients[0]),Does.Match("奶泡|奶沫"));
            Assert.That(JsonUtility.ToJson(world.Catalog),Is.EqualTo(before),"Recipe terminology must not rename authoritative ingredient data");
            hud.recipeCloseButton.onClick.Invoke();
        }

        [UnityTest] public IEnumerator CoffeeBookBlocksEntityInputAndClosingRestoresIt()
        {
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();production.bookButton.onClick.Invoke();yield return null;
            Assert.That(app.InputBlocked,Is.True);Assert.That(production.bookPanel.activeSelf,Is.True);
            var beans=app.World.Find("s.beans");int stock=beans.stock;
            app.Click(app.View(beans.id));Assert.That(beans.stock,Is.EqualTo(stock));
            production.closeBook.onClick.Invoke();yield return null;
            Assert.That(app.InputBlocked,Is.False);
            app.Enter(AreaId.Cafe);yield return null;
            OpenPlayerCoffeeBook();yield return null;Assert.That(app.InputBlocked,Is.True);
            hud.recipeCloseButton.onClick.Invoke();yield return null;Assert.That(app.InputBlocked,Is.False);
        }

        [UnityTest] public IEnumerator CoffeeBooksKeepPsdLayersAndUseOnlyUguiAndTmp()
        {
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();
            var view=production.bookPanel.GetComponent<CoffeeBookPanelView>();
            Assert.That(view,Is.Not.Null);
            Assert.That(production.bookPanel.GetComponentsInChildren<SpriteRenderer>(true),Is.Empty);
            Assert.That(production.bookPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true),Is.Empty);
            Assert.That(production.bookPanel.GetComponentsInChildren<Canvas>(true),Is.Empty);
            foreach(string name in new[]{"图层 1","图层 5","进度条蒙版","进度条","标题底图","选中标","选项底图","咖啡icon","图层 8","精粹咖啡"})
                Assert.That(view.artRoot.Find(name),Is.Not.Null,name);
            Assert.That(view.artRoot.Find("精粹咖啡").GetComponent<Image>().enabled,Is.False,"Rasterized PSD text must be replaced by TMP");
            Assert.That(view.title,Is.TypeOf<TextMeshProUGUI>());
            Assert.That(view.bodyScroll.content,Is.EqualTo(view.body.rectTransform));
            Assert.That(hud.recipePanel.GetComponent<CoffeeBookPanelView>(),Is.Not.Null);
        }

        [UnityTest] public IEnumerator CoffeeBookSidebarChangesRecipeAndEmptyListClearsSelection()
        {
            var world=app.World;
            Assert.That(world.Catalog.products.Length,Is.GreaterThan(1));
            world.State.unlockedProducts.Clear();
            world.State.unlockedProducts.Add(world.Catalog.products[0].id);
            world.State.unlockedProducts.Add(world.Catalog.products[1].id);
            app.Enter(AreaId.Counter);yield return null;
            var production=app.GetComponent<FreeCafeProductionHud>();
            production.bookButton.onClick.Invoke();yield return null;
            var view=production.bookPanel.GetComponent<CoffeeBookPanelView>();
            view.entries[1].onClick.Invoke();yield return null;
            Assert.That(view.title.text,Is.EqualTo(FreeCafeProductionText.Recipe(world.Catalog.products[1].label)));
            Assert.That(view.page.text,Is.EqualTo("02 / 02"));
            production.closeBook.onClick.Invoke();
            world.State.unlockedProducts.Clear();
            production.bookButton.onClick.Invoke();yield return null;
            Assert.That(view.body.text,Does.Contain("尚未学会咖啡"));
            Assert.That(view.marker.gameObject.activeSelf,Is.False);
            Assert.That(view.previous.interactable,Is.False);
            Assert.That(view.next.interactable,Is.False);
            foreach(var entry in view.entries) Assert.That(entry.interactable,Is.False);
            production.closeBook.onClick.Invoke();
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
            Assert.That(hud.areaButtons[(int)AreaId.Counter].interactable,Is.True);
            Assert.That(app.CanEnterArea(AreaId.Cafe),Is.False);
            app.RequestBusinessStart();
            Assert.That(app.World.State.open,Is.False);
            hud.areaButtons[(int)AreaId.Counter].onClick.Invoke();
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
            Assert.That(world.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0).success,Is.True);
            yield return null;
            Assert.That(world.OperateProduction("m.grinder").success,Is.True);
            yield return null;
            world.Tick(20); yield return null;
            var output=world.DragOutputPreview("m.grinder");
            Assert.That(world.TakeMachineOutput("m.grinder",output.id,"m.heater",AreaId.Counter,0,0).success,Is.True);
            yield return null;
            Assert.That(world.TakeAndInteract("s.water","m.heater",AreaId.Counter,0,0).success,Is.True);
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
            Assert.That(tutorialFlow.IsCafeTutorialActive,Is.False);
            Assert.That(guide.IsRunning,Is.False);
            Assert.That(world.State.open,Is.False);
            Assert.That(world.State.requests,Is.Empty);
            Assert.That(production.receiptPanel.activeSelf,Is.True);
            production.confirmReceipt.onClick.Invoke();
            Assert.That(app.InputBlocked,Is.False);
            app.Enter(AreaId.Cafe);
            app.RequestBusinessStart();
            Assert.That(world.State.open,Is.True);
        }
        private static Button FindAction(FreeCafeGMPanel gm,string label)
        {
            foreach(var button in gm.GetComponentsInChildren<Button>())
            { var text=button.GetComponentInChildren<TMP_Text>();if(text!=null && text.text.StartsWith(label+"\n")) return button; }
            Assert.Fail("GM action missing: "+label);return null;
        }
    }
}
