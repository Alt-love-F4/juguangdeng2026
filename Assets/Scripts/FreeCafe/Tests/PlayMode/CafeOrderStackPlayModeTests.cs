using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class CafeOrderStackPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeHud hud;
        private CafeOrderStackView stack;
        private CoffeeCompendiumPanel book;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null;
            yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            hud = Object.FindFirstObjectByType<FreeCafeHud>();
            Assert.That(app, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            app.enabled = false; // Keep the scene-owned domain; only explicit operations advance orders.
            app.areaManager.panSmoothTime = 0;
            Assert.That(hud.nextOrder, Is.Not.Null);
            stack = hud.nextOrder.GetComponentInParent<CafeOrderStackView>();
            Assert.That(stack, Is.Not.Null, "The authored paging button belongs to the order stack.");
            Assert.That(stack.transform, Is.SameAs(hud.playerRoot.transform.Find("CafeLobbyHud/HeaderDock/Orders")));
            Assert.That(hud.recipePanel, Is.Not.Null);
            book = hud.recipePanel.GetComponent<CoffeeCompendiumPanel>();
            Assert.That(book, Is.Not.Null);
            Assert.That(stack.front, Is.Not.Null);
            Assert.That(stack.outgoing, Is.Not.Null);
            Assert.That(stack.waiting, Is.Not.Null.And.Not.Empty);
            Assert.That(hud.orderButtons.Length, Is.EqualTo(1), "The stack retains one paged public order row.");
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (app != null) app.enabled = true;
            yield return null;
        }

        [UnityTest] public IEnumerator NamedStoryCustomerUsesConfiguredSeatedArtAndOrderIdentity()
        {
            StartOrders();
            var identity = new CafePersonIdentity { personId = "story:elion", characterId = "elion", narrativeCharacterId = "elion", displayName = "埃利昂", role = CafePersonRole.Customer };
            Assert.That(app.World.TryBeginNarrativeService("named-ui", new[] { new CafeNarrativeOrderSpec { productId = "blackcoffee", customer = identity } },
                _ => { }, out var lease, out string error, orderOnly: true), Is.True, error);
            try
            {
                var order = app.World.Request(app.World.State.focusedRequestId);
                var entity = app.World.Find(order.customerId);
                var art = System.Array.Find(app.seating.characterArt, x => x.characterId == "elion");
                Assert.That(art, Is.Not.Null);
                Assert.That(app.seating.Artwork(entity), Is.SameAs(art.sprite));
                stack.Show(app.World.State.requests.IndexOf(order));
                Assert.That(stack.front.heading.text, Is.EqualTo("剧情订单"));
                Assert.That(stack.front.label.text, Does.Contain("埃利昂"));
                Assert.That(stack.front.avatar.gameObject.activeSelf, Is.True);
                Assert.That(stack.front.patience.text, Does.Contain("不限时"));
                yield return Settle();
                app.enabled = true;
                app.DebugRefreshViews();
                Assert.That(app.View(entity.id), Is.Not.Null);
                Assert.That(app.View(entity.id).body.sprite, Is.SameAs(art.sprite));
                app.enabled = false;
            }
            finally { lease.Dispose(); }
        }

        [UnityTest] public IEnumerator NarrativeTastingShowsNoCustomerAvatarAndRestoresOrdinaryTicket()
        {
            StartOrders();
            var original = app.World.State.requests[0];
            int customers = app.World.State.entities.FindAll(e => e.specId == "customer").Count;
            System.IDisposable lease;
            Assert.That(app.World.TryBeginNarrativeService("ui-tasting", new[] { new CafeNarrativeOrderSpec { productId = "blackcoffee" } },
                _ => { }, out lease, out string error, orderOnly: true), Is.True, error);
            try
            {
                int index = app.World.State.requests.FindIndex(r => app.World.IsNarrativeTastingOrder(r));
                stack.Show(index);
                Assert.That(stack.front.heading.text, Is.EqualTo("试喝订单"));
                Assert.That(stack.front.label.text, Does.Contain("试喝饮品"));
                Assert.That(stack.front.patience.text, Does.Contain("不限时"));
                Assert.That(stack.front.details.text, Does.Contain("不计营收"));
                Assert.That(stack.front.avatar.gameObject.activeSelf, Is.False);
                Assert.That(app.World.State.entities.FindAll(e => e.specId == "customer").Count, Is.EqualTo(customers));
                yield return Settle();
            }
            finally { lease.Dispose(); }
            stack.Show(app.World.State.requests.IndexOf(original));
            Assert.That(stack.front.heading.text, Is.EqualTo("来客订单"));
            Assert.That(stack.front.label.text, Does.Not.Contain("试喝饮品"));
            Assert.That(stack.front.avatar.gameObject.activeSelf, Is.True);
        }

        [UnityTest] public IEnumerator RealOrdersShowWaitingSheetsAndUseCharacterAndCompendiumSprites()
        {
            StartOrders();
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Cafe));
            Assert.That(stack.iconSource, Is.SameAs(book), "Drink art must come from the HUD's recipe panel.");
            AssertWaitingSheets();
            AssertCardMatches(stack.front, 0);
            Assert.That(stack.front.avatar.transform, Is.SameAs(stack.front.root.Find("CharacterAvatar")),
                "The stack reuses the HUD's real character avatar child.");
            yield return Settle();
            AssertResting();

            string focused = app.World.State.focusedRequestId;
            for (int index = 1; index < app.World.State.requests.Count; index++)
            {
                hud.nextOrder.onClick.Invoke();
                AssertPage(index);
                Assert.That(app.World.State.focusedRequestId, Is.EqualTo(focused), "Lobby paging must not auto-focus an order.");
                AssertWaitingSheets();
                yield return Settle();
            }
        }

        [UnityTest] public IEnumerator PagingIsImmediateAnimationsUseUnscaledTimeAndRapidClicksKeepLatestOrder()
        {
            StartOrders();
            yield return Settle();
            string focused = app.World.State.focusedRequestId;
            int count = app.World.State.requests.Count;
            Assert.That(hud.nextOrder.interactable, Is.True);
            hud.nextOrder.onClick.Invoke();
            AssertPage(1); // No frame or animation wait is allowed before the data changes.
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(focused));
            Assert.That(stack.IsAnimating, Is.True);
            Assert.That(stack.outgoing.root.gameObject.activeInHierarchy, Is.True);
            AssertCardMatches(stack.outgoing, 0);
            Assert.That(stack.front.root.anchoredPosition.sqrMagnitude, Is.GreaterThan(.01f),
                "The newly selected sheet starts in the deck, not already at its resting pose.");

            Time.timeScale = 0;
            try
            {
                // Pause after clicking: paused gameplay intentionally blocks new input, not an in-flight UI animation.
                yield return Settle();
                Assert.That(Time.timeScale, Is.Zero);
                AssertResting();
                AssertPage(1);
            }
            finally { Time.timeScale = 1; }

            hud.previousOrder.onClick.Invoke();
            AssertPage(0);
            Assert.That(stack.IsAnimating, Is.True);
            Assert.That(stack.outgoing.root.gameObject.activeInHierarchy, Is.True);
            int latest = 0;
            for (int click = 0; click < count * 2 + 1; click++)
            {
                hud.nextOrder.onClick.Invoke();
                latest = (latest + 1) % count;
                AssertPage(latest);
                Assert.That(app.World.State.focusedRequestId, Is.EqualTo(focused));
                Assert.That(stack.IsAnimating, Is.True);
                if (click % 2 == 0) yield return null;
            }
            yield return Settle();
            AssertResting();
            AssertPage(latest);

            hud.orderButtons[0].onClick.Invoke();
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(app.World.State.requests[latest].id),
                "Clicking the front order, unlike lobby paging, explicitly focuses it.");
            AssertOrderRecipe(book, latest);
            book.CloseImmediate();
            app.Enter(AreaId.Counter);
            yield return null;
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Counter));
            Assert.That(hud.playerRoot.activeInHierarchy, Is.True);
            AssertPage(latest);
            hud.nextOrder.onClick.Invoke();
            latest = (latest + 1) % count;
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(app.World.State.requests[latest].id),
                "Counter paging must synchronously change the delivery focus.");
            AssertPage(latest);
            Assert.That(stack.IsAnimating, Is.True);
            hud.previousOrder.onClick.Invoke();
            latest = (latest - 1 + count) % count;
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(app.World.State.requests[latest].id));
            AssertPage(latest);
            yield return Settle();
            AssertResting();
            AssertPage(latest);
        }

        [UnityTest] public IEnumerator OrderClicksOpenTheSelectedProductAcrossPagesAndAreas()
        {
            StartOrders();
            Assert.That(app.World.Catalog.products.Length, Is.GreaterThanOrEqualTo(2));
            // Distinct products ensure reopening cannot accidentally pass with a stale recipe page.
            app.World.State.requests[0].productId = app.World.Catalog.products[0].id;
            app.World.State.requests[1].productId = app.World.Catalog.products[1].id;
            app.DebugRefreshViews();
            var productionBook = app.GetComponent<FreeCafeProductionHud>().bookPanel.GetComponent<CoffeeCompendiumPanel>();
            Assert.That(productionBook, Is.Not.Null);

            hud.orderButtons[0].onClick.Invoke();
            AssertOrderRecipe(book, 0);
            if (productionBook != book) Assert.That(productionBook.IsOpen, Is.False);
            book.CloseImmediate();
            Assert.That(app.InputBlocked, Is.False);

            hud.nextOrder.onClick.Invoke();
            AssertPage(1);
            hud.orderButtons[0].onClick.Invoke();
            AssertOrderRecipe(book, 1);
            book.CloseImmediate();
            Assert.That(app.InputBlocked, Is.False);

            app.Enter(AreaId.Counter);
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Counter));
            hud.orderButtons[0].onClick.Invoke();
            AssertOrderRecipe(productionBook, 1);
            if (productionBook != book) Assert.That(book.IsOpen, Is.False);
            productionBook.CloseImmediate();
            Assert.That(app.InputBlocked, Is.False);

            hud.previousOrder.onClick.Invoke();
            AssertPage(0);
            hud.orderButtons[0].onClick.Invoke();
            AssertOrderRecipe(productionBook, 0);
            productionBook.CloseImmediate();
            Assert.That(app.InputBlocked, Is.False);
            yield return null;
        }

        private void AssertOrderRecipe(CoffeeCompendiumPanel panel, int requestIndex)
        {
            var request = app.World.State.requests[requestIndex];
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(request.id));
            Assert.That(panel.IsOpen, Is.True, "Order clicks must open the recipe immediately, without a frame wait.");
            Assert.That(panel.CurrentTab, Is.EqualTo(CoffeeCompendiumPanel.Tab.Recipes));
            Assert.That(panel.RecipeIndex, Is.InRange(0, panel.ProductOrder.Count - 1));
            Assert.That(panel.ProductOrder[panel.RecipeIndex], Is.EqualTo(request.productId));
            Assert.That(app.InputBlocked, Is.True);
        }

        [UnityTest] public IEnumerator ClearingOrdersHidesEverySheetAndHudRebindingDoesNotDuplicateNodes()
        {
            StartOrders();
            yield return Settle();
            var avatar = stack.front.avatar;
            var drink = stack.front.drink;
            var populatedNodes = stack.GetComponentsInChildren<Transform>(true);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                hud.enabled = false;
                hud.enabled = true;
                app.DebugRefreshViews();
                yield return null;
                Assert.That(hud.nextOrder.GetComponentInParent<CafeOrderStackView>(), Is.SameAs(stack));
                Assert.That(stack.front.avatar, Is.SameAs(avatar));
                Assert.That(stack.front.drink, Is.SameAs(drink));
                Assert.That(stack.GetComponentsInChildren<Transform>(true), Is.EquivalentTo(populatedNodes),
                    "Rebinding a populated HUD must reuse its existing stack and avatar nodes.");
                AssertPage(0);
                AssertWaitingSheets();
            }

            hud.nextOrder.onClick.Invoke();
            AssertPage(1); // Rebinding must not accumulate paging listeners either.
            Assert.That(stack.IsAnimating, Is.True);
            Assert.That(stack.outgoing.root.gameObject.activeInHierarchy, Is.True);
            app.World.State.requests.Clear();
            app.DebugRefreshViews();
            AssertEmpty();
            yield return Settle();
            AssertEmpty(); // A cancelled outgoing animation must not bring old contents back.
            var emptyNodes = stack.GetComponentsInChildren<Transform>(true);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                hud.enabled = false;
                hud.enabled = true;
                app.DebugRefreshViews();
                yield return null;
                AssertEmpty();
                Assert.That(stack.front.avatar, Is.SameAs(avatar));
                Assert.That(stack.front.drink, Is.SameAs(drink));
                Assert.That(stack.GetComponentsInChildren<Transform>(true), Is.EquivalentTo(emptyNodes),
                    "Rebinding an empty HUD must not create another stack, sheet, avatar or drink node.");
            }
        }

        private void StartOrders()
        {
            Assert.That(app.World.StartDay().success, Is.True);
            if (app.World.State.requests.Count < 2)
                Assert.That(app.World.DebugSpawnCustomers(2 - app.World.State.requests.Count).success, Is.True);
            Assert.That(app.World.State.requests.Count, Is.GreaterThanOrEqualTo(2), "Use real spawned customer orders, not fabricated UI data.");
            app.DebugRefreshViews();
            stack.OnPointerExit(null); // Normalize pointer hover before testing the resting transform.
        }

        private void AssertWaitingSheets()
        {
            int count = app.World.State.requests.Count;
            for (int index = 0; index < stack.waiting.Length; index++)
            {
                Assert.That(stack.waiting[index], Is.Not.Null);
                Assert.That(stack.waiting[index].root, Is.Not.Null);
                Assert.That(stack.waiting[index].root.gameObject.activeSelf, Is.EqualTo(count > index + 1),
                    "Waiting sheet " + index + " must correspond to an actual extra order.");
                Assert.That(stack.waiting[index].root.gameObject.activeInHierarchy, Is.EqualTo(count > index + 1));
            }
        }

        private void AssertCardMatches(CafeOrderStackView.Card card, int requestIndex)
        {
            var request = app.World.State.requests[requestIndex];
            var customer = app.World.Find(request.customerId);
            Assert.That(customer?.person, Is.Not.Null);
            var character = app.Character(customer.person.characterId);
            Assert.That(character, Is.Not.Null);
            Assert.That(character.avatar, Is.Not.Null, "The real customer must have a character portrait.");
            Assert.That(card.root.gameObject.activeInHierarchy, Is.True);
            Assert.That(card.avatar, Is.Not.Null);
            Assert.That(card.avatar.gameObject.activeInHierarchy, Is.True);
            Assert.That(card.avatar.sprite, Is.SameAs(character.avatar));
            Assert.That(card.avatar.raycastTarget, Is.False);
            Assert.That(card.avatar.preserveAspect, Is.True);
            var recipe = System.Array.Find(book.recipes, copy => copy != null && copy.productId == request.productId);
            Assert.That(recipe, Is.Not.Null, "The HUD compendium must contain productId " + request.productId);
            Assert.That(recipe.icon, Is.Not.Null, request.productId + " must have its own compendium icon.");
            Assert.That(card.drink, Is.Not.Null);
            Assert.That(card.drink.gameObject.activeInHierarchy, Is.True);
            Assert.That(card.drink.sprite, Is.SameAs(recipe.icon), "Match by productId, not recipe or request array position.");
            Assert.That(card.number.text, Is.EqualTo((requestIndex + 1).ToString("00")));
            Assert.That(card.label.text, Does.Contain(app.World.PersonName(customer)));
            Assert.That(card.label.text, Does.Contain(FreeCafeProductionText.Recipe(app.World.Product(request.productId).label)));
        }

        private void AssertPage(int index)
        {
            AssertCardMatches(stack.front, index);
            Assert.That(hud.orderPage.text, Is.EqualTo((index + 1).ToString("00") + " / " + app.World.State.requests.Count.ToString("00")));
        }

        private IEnumerator Settle()
        {
            stack.OnPointerExit(null);
            yield return new WaitForSecondsRealtime(Mathf.Max(stack.switchSeconds, stack.arrivalSeconds) + .15f);
            yield return null;
            Assert.That(stack.IsAnimating, Is.False, "The stack must finish using real, unscaled time.");
        }

        private void AssertResting()
        {
            Assert.That(stack.front.root.anchoredPosition.x, Is.EqualTo(0).Within(.01f));
            Assert.That(stack.front.root.anchoredPosition.y, Is.EqualTo(0).Within(.01f));
            Assert.That(Quaternion.Angle(stack.front.root.localRotation, Quaternion.identity), Is.EqualTo(0).Within(.01f));
            Assert.That(Vector3.Distance(stack.front.root.localScale, Vector3.one), Is.EqualTo(0).Within(.001f));
            Assert.That(stack.outgoing.root.gameObject.activeSelf, Is.False);
        }

        private void AssertEmpty()
        {
            AssertWaitingSheets();
            Assert.That(stack.front.avatar.gameObject.activeSelf, Is.False);
            Assert.That(stack.front.avatar.sprite, Is.Null);
            Assert.That(stack.front.drink.gameObject.activeSelf, Is.False);
            Assert.That(stack.front.drink.sprite, Is.Null);
            Assert.That(stack.outgoing.root.gameObject.activeSelf, Is.False);
            Assert.That(stack.IsAnimating, Is.False);
            Assert.That(hud.nextOrder.interactable, Is.False);
            Assert.That(hud.previousOrder.interactable, Is.False);
            foreach (var button in hud.orderButtons) Assert.That(button.interactable, Is.False);
            Assert.That(hud.orderPage.text, Is.EqualTo("暂无订单"));
            // Invoke bypasses Button.interactable, so the handler must also reject stale/empty rows.
            string focused = app.World.State.focusedRequestId;
            foreach (var button in hud.orderButtons) button.onClick.Invoke();
            Assert.That(app.World.State.focusedRequestId, Is.EqualTo(focused));
            Assert.That(book.IsOpen, Is.False);
            Assert.That(app.GetComponent<FreeCafeProductionHud>().IsModalOpen, Is.False);
            Assert.That(app.InputBlocked, Is.False);
        }
    }
}
