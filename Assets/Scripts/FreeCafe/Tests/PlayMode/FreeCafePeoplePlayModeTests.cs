using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafePeoplePlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Cafe);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;yield return null; }
        [UnityTest] public IEnumerator ActualLobbyPrefabIsRaycastableAndPointerClickOpensTheCafe()
        {
            var view=app.View("business.start");Assert.That(view.AuthoringEntityId,Is.EqualTo("business.start"));
            var sign=view.GetComponent<FreeCafeBusinessSignView>();Assert.That(sign,Is.Not.Null);
            Assert.That(view.label.text,Is.EqualTo("启动营业"));Assert.That(app.World.State.requests,Is.Empty);
            System.IO.Directory.CreateDirectory("output/entity-prefab-validation");
            ScreenCapture.CaptureScreenshot("output/entity-prefab-validation/lobby-before-business.png");yield return null;
            Physics2D.SyncTransforms();var pointer=new PointerEventData(EventSystem.current)
                {position=app.worldCamera.WorldToScreenPoint(view.transform.position),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits,Is.Not.Empty);Assert.That(hits[0].gameObject.GetComponent<FreeEntityView>(),Is.SameAs(view),"立牌不能被 HUD 遮挡");
            ExecuteEvents.Execute(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return null;
            Assert.That(app.World.State.open,Is.True);Assert.That(app.World.State.requests.Count,Is.GreaterThan(0));
            Assert.That(view.label.text,Is.EqualTo("营业中"));int count=app.World.State.requests.Count;
            app.Click(view);Assert.That(app.World.State.requests.Count,Is.EqualTo(count));
            ScreenCapture.CaptureScreenshot("output/entity-prefab-validation/lobby-open-customers.png");yield return null;yield return null;
        }
        [UnityTest] public IEnumerator CustomerPrefabHasPersonAnchorsAndClickOnlyFocusesTheOrder()
        {
            app.Click(app.View("business.start"));yield return null;
            var request=app.World.State.requests[1];var view=app.View(request.customerId);var person=view.GetComponent<FreeCafePersonView>();
            Assert.That(person,Is.Not.Null);Assert.That(person.visualRoot,Is.Not.Null);Assert.That(person.portraitAnchor,Is.Not.Null);
            Assert.That(person.speechAnchor,Is.Not.Null);Assert.That(person.orderAnchor,Is.Not.Null);
            Assert.That(person.Identity,Is.SameAs(app.World.Find(request.customerId).person));
            Assert.That(person.orderCaption.text,Does.Contain(app.World.Product(request.productId).label));
            Assert.That(view.label.text,Is.EqualTo(person.Identity.displayName));
            app.Click(view);Assert.That(app.World.State.focusedRequestId,Is.EqualTo(request.id));
            Assert.That(app.World.State.topics,Is.Empty);Assert.That(person.Identity.narrativeCharacterId,Is.Null.Or.Empty);
            string identity=person.Identity.personId;app.RestoreJson(app.CaptureJson());yield return null;
            Assert.That(app.View(request.customerId).GetComponent<FreeCafePersonView>().Identity.personId,Is.EqualTo(identity));
        }
        [UnityTest] public IEnumerator OtherAreasAndPlayerModalCannotActivateTheLobbySign()
        {
            var view=app.View("business.start");app.Enter(AreaId.Counter);app.Click(view);
            Assert.That(app.World.State.open,Is.False);app.Enter(AreaId.Cafe);
            var hud=Object.FindFirstObjectByType<FreeCafeHud>();hud.recipePanel.SetActive(true);app.Click(view);
            Assert.That(app.World.State.open,Is.False);hud.CloseRecipeTips();
            app.Click(view);Assert.That(app.World.State.open,Is.True);yield return null;
        }
    }
}
