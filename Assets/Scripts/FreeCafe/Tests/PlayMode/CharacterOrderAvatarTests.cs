using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class CharacterOrderAvatarTests
    {
        private FreeCafeRuntime app;
        private FreeCafeHud hud;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;
            GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null; yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();
            hud=Object.FindFirstObjectByType<FreeCafeHud>();
            Assert.That(app,Is.Not.Null);
            Assert.That(hud,Is.Not.Null);
            app.areaManager.panSmoothTime=0;
            app.enabled=false; // Only explicit domain operations advance this test's orders.
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(app!=null) app.enabled=true;
            Time.timeScale=1;
            yield return null;
        }

        [UnityTest] public IEnumerator SharedOrderAvatarTracksPagesCounterAndEmptyOrders()
        {
            Assert.That(hud.orderButtons.Length,Is.EqualTo(1),"CoffeeShop uses one paged public order row.");
            var label=hud.orderLabels[0].rectTransform;
            var originalMin=label.offsetMin;
            var originalMax=label.offsetMax;
            var avatar=Avatar(0);
            Assert.That(avatar.gameObject.activeSelf,Is.False);
            Assert.That(app.World.StartDay().success,Is.True);
            if(app.World.State.requests.Count<2)
                Assert.That(app.World.DebugSpawnCustomers(2-app.World.State.requests.Count).success,Is.True);
            app.DebugRefreshViews();
            AssertAvatar(0,0);
            Assert.That(avatar.rectTransform.sizeDelta,Is.EqualTo(new Vector2(64,64)));
            Assert.That(label.offsetMin.x,Is.EqualTo(originalMin.x+72).Within(.01f));
            Assert.That(label.offsetMin.y,Is.EqualTo(originalMin.y));
            Assert.That(label.offsetMax,Is.EqualTo(originalMax));
            string focused=app.World.State.focusedRequestId;
            Assert.That(hud.nextOrder.interactable,Is.True);
            hud.nextOrder.onClick.Invoke();
            AssertAvatar(0,1);
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(focused),"Cafe paging must not change selection.");
            hud.orderButtons[0].onClick.Invoke();
            Assert.That(app.World.State.focusedRequestId,Is.EqualTo(app.World.State.requests[1].id));
            var book=hud.recipePanel.GetComponent<CoffeeCompendiumPanel>();
            Assert.That(book,Is.Not.Null);
            Assert.That(book.IsOpen,Is.True);
            Assert.That(book.CurrentTab,Is.EqualTo(CoffeeCompendiumPanel.Tab.Recipes));
            Assert.That(book.RecipeIndex,Is.InRange(0,book.ProductOrder.Count-1));
            Assert.That(book.ProductOrder[book.RecipeIndex],Is.EqualTo(app.World.State.requests[1].productId));
            book.CloseImmediate();
            Assert.That(app.InputBlocked,Is.False);
            app.Enter(AreaId.Counter);
            yield return null;
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            Assert.That(hud.playerRoot.activeSelf,Is.True);
            Assert.That(Avatar(0),Is.SameAs(avatar));
            AssertAvatar(0,1);
            hud.nextOrder.onClick.Invoke();
            int selected=app.World.State.requests.FindIndex(request=>request.id==app.World.State.focusedRequestId);
            Assert.That(selected,Is.Not.EqualTo(1));
            AssertAvatar(0,selected);
            app.World.State.requests.Clear();
            app.DebugRefreshViews();
            Assert.That(avatar.gameObject.activeSelf,Is.False);
            Assert.That(avatar.sprite,Is.Null);
            Assert.That(label.offsetMin,Is.EqualTo(originalMin));
            Assert.That(label.offsetMax,Is.EqualTo(originalMax));
            Assert.That(hud.orderPage.text,Is.EqualTo("暂无订单"));
            Assert.That(hud.orderButtons[0].interactable,Is.False);
            Assert.That(book.IsOpen,Is.False);
            app.Enter(AreaId.Cafe);
            app.DebugRefreshViews();
            Assert.That(avatar.gameObject.activeSelf,Is.False);
        }

        [UnityTest] public IEnumerator MissingAvatarRestoresTextAndRebindingReusesTheChild()
        {
            var label=hud.orderLabels[0].rectTransform;
            var originalMin=label.offsetMin;
            var originalMax=label.offsetMax;
            Assert.That(app.World.StartDay().success,Is.True);
            app.DebugRefreshViews();
            var avatar=Avatar(0);
            AssertAvatar(0,0);
            var customer=app.World.Find(app.World.State.requests[0].customerId);
            var character=app.Character(customer.person.characterId);
            var saved=character.avatar;
            try
            {
                character.avatar=null;
                app.DebugRefreshViews();
                Assert.That(avatar.sprite,Is.Null,"No other portrait or role may substitute for a missing avatar.");
                Assert.That(avatar.gameObject.activeSelf,Is.False);
                Assert.That(label.offsetMin,Is.EqualTo(originalMin));
                Assert.That(label.offsetMax,Is.EqualTo(originalMax));
            }
            finally { character.avatar=saved; }
            for(int attempt=0;attempt<3;attempt++)
            {
                hud.enabled=false;
                Assert.That(label.offsetMin,Is.EqualTo(originalMin));
                Assert.That(label.offsetMax,Is.EqualTo(originalMax));
                hud.enabled=true;
                app.DebugRefreshViews();
                Assert.That(Avatar(0),Is.SameAs(avatar));
                AssertAvatar(0,0);
                Assert.That(label.offsetMin.x,Is.EqualTo(originalMin.x+72).Within(.01f));
                int count=0;
                foreach(Transform child in label.parent)
                    if(child.name=="CharacterAvatar") count++;
                Assert.That(count,Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator MultipleSlotsHaveIndependentAvatarsAndRestoreTheirOwnLayout()
        {
            hud.enabled=false;
            var first=hud.orderButtons[0];
            var second=Object.Instantiate(first,first.transform.parent);
            second.name="AvatarTestOrder1";
            var firstLabel=hud.orderLabels[0];
            var secondLabel=second.GetComponentInChildren<TMP_Text>(true);
            var originalMin=secondLabel.rectTransform.offsetMin;
            var originalMax=secondLabel.rectTransform.offsetMax;
            try
            {
                secondLabel.rectTransform.anchoredPosition+=new Vector2(3,7);
                originalMin=secondLabel.rectTransform.offsetMin;
                originalMax=secondLabel.rectTransform.offsetMax;
                hud.orderButtons=new[]{first,second};
                hud.orderLabels=new[]{firstLabel,secondLabel};
                hud.enabled=true;
                Assert.That(app.World.StartDay().success,Is.True);
                if(app.World.State.requests.Count<2)
                    Assert.That(app.World.DebugSpawnCustomers(2-app.World.State.requests.Count).success,Is.True);
                app.DebugRefreshViews();
                AssertAvatar(0,0);
                AssertAvatar(1,1);
                Assert.That(Avatar(1),Is.Not.SameAs(Avatar(0)));
                Assert.That(Avatar(1).rectTransform.anchoredPosition.y,Is.EqualTo(7).Within(.01f));
                app.World.State.requests.RemoveRange(1,app.World.State.requests.Count-1);
                app.DebugRefreshViews();
                AssertAvatar(0,0);
                Assert.That(Avatar(1).gameObject.activeSelf,Is.False);
                Assert.That(secondLabel.rectTransform.offsetMin,Is.EqualTo(originalMin));
                Assert.That(secondLabel.rectTransform.offsetMax,Is.EqualTo(originalMax));
            }
            finally
            {
                hud.enabled=false;
                hud.orderButtons=new[]{first};
                hud.orderLabels=new[]{firstLabel};
                Object.Destroy(second.gameObject);
                hud.enabled=true;
            }
            yield return null;
        }

        private Image Avatar(int row)
        {
            var child=hud.orderLabels[row].transform.parent.Find("CharacterAvatar");
            Assert.That(child,Is.Not.Null,"Every bound order row needs one cached avatar child.");
            var avatar=child.GetComponent<Image>();
            Assert.That(avatar,Is.Not.Null);
            return avatar;
        }

        private void AssertAvatar(int row,int requestIndex)
        {
            var customer=app.World.Find(app.World.State.requests[requestIndex].customerId);
            Assert.That(customer?.person,Is.Not.Null);
            var character=app.Character(customer.person.characterId);
            Assert.That(character,Is.Not.Null);
            Assert.That(character.avatar,Is.Not.Null,customer.person.characterId+" must have its own avatar resource.");
            var avatar=Avatar(row);
            Assert.That(avatar.gameObject.activeInHierarchy,Is.True);
            Assert.That(avatar.sprite,Is.SameAs(character.avatar));
            Assert.That(avatar.raycastTarget,Is.False);
            Assert.That(avatar.preserveAspect,Is.True);
        }
    }
}
