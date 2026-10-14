using System.Collections;
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
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;hud=Object.FindFirstObjectByType<FreeCafeHud>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            Time.timeScale=1;yield return null;
        }
        [UnityTest] public IEnumerator PlayerHudHasNoFullWidthBlackBarsOrDebugButtons()
        {
            Assert.That(hud.transform.Find("TopBlackBar").gameObject.activeSelf,Is.False);
            Assert.That(hud.transform.Find("BottomBlackBar").gameObject.activeSelf,Is.False);
            Assert.That(hud.startButton.gameObject.activeInHierarchy,Is.False);
            Assert.That(hud.saveButton.gameObject.activeInHierarchy,Is.False);
            Assert.That(hud.areaButtons[(int)AreaId.TopFloor],Is.Null);
            Assert.That(hud.summary.text,Does.Not.Contain("FREE CAFE"));
            Assert.That(hud.summary.text,Does.Contain("王国币"));
            Assert.That(hud.playerRoot.GetComponentsInChildren<CafeHudSurface>().Length,Is.GreaterThan(5));
            var production=app.GetComponent<FreeCafeProductionHud>();
            app.Enter(AreaId.Counter);yield return null;
            Assert.That(hud.playerRoot.activeSelf,Is.False);
            Assert.That(production.startBusiness.gameObject.activeInHierarchy,Is.False);
            Assert.That(production.saveButton.gameObject.activeInHierarchy,Is.False);
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
            hud.recipeButton.onClick.Invoke();yield return null;
            Assert.That(hud.recipeBody.text,Does.Contain("拉花饮品").And.Contain("配方种类：拉花").And.Not.Contain("奶泡").And.Not.Contain("奶沫"));
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
            hud.recipeButton.onClick.Invoke();yield return null;Assert.That(app.InputBlocked,Is.True);
            hud.recipeCloseButton.onClick.Invoke();yield return null;Assert.That(app.InputBlocked,Is.False);
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
        private static Button FindAction(FreeCafeGMPanel gm,string label)
        {
            foreach(var button in gm.GetComponentsInChildren<Button>())
            { var text=button.GetComponentInChildren<TMP_Text>();if(text!=null && text.text.StartsWith(label+"\n")) return button; }
            Assert.Fail("GM action missing: "+label);return null;
        }
    }
}
