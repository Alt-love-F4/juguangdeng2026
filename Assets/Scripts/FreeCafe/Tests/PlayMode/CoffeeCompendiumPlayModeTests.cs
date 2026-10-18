using System.Collections;
using System.Linq;
using NUnit.Framework;
using Spotlight.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    /// <summary>咖啡宝典：订购与补货只走 CafeWorld 现有接口，条件不足时不扣钱。</summary>
    public sealed class CoffeeCompendiumPlayModeTests
    {
        private FreeCafeRuntime app;
        private FreeCafeHud hud;
        private CoffeeCompendiumPanel book;

        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0;
            hud = Object.FindFirstObjectByType<FreeCafeHud>();
            book = hud.recipePanel.GetComponent<CoffeeCompendiumPanel>();
            // Real progression rules instead of the free sandbox the bare scene starts with.
            app.World.ConfigureEquipmentProgression(false);
            app.World.State.open = false;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (book != null) book.CloseImmediate();
            Time.timeScale = 1;
            yield return null;
        }

        private IEnumerator OpenTab(CoffeeCompendiumPanel.Tab tab)
        {
            // The lobby HUD has no own 咖啡谱 button (it lives on the workstation menu); use the HUD toggle like the other HUD tests.
            if (hud.recipeButton != null) hud.recipeButton.onClick.Invoke();
            else typeof(FreeCafeHud).GetMethod("ToggleRecipeTips", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hud, null);
            yield return Settle();
            Assert.That(book.IsOpen, Is.True);
            book.SelectTab(tab);
            yield return Settle();
            Assert.That(book.CurrentTab, Is.EqualTo(tab));
        }

        private IEnumerator Settle()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (book.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        private Button ButtonLabelled(string label)
            => book.GetComponentsInChildren<Button>().FirstOrDefault(button => !book.tabButtons.Contains(button)
                && button.GetComponentsInChildren<Button>().Length == 1
                && button.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Replace(" ", "").Contains(label)));

        private Button RestockButton(EntitySpec spec)
        {
            var row = book.GetComponentsInChildren<TMP_Text>().First(text => text.text == FreeCafeProductionText.Recipe(spec.label)).transform.parent;
            return row.GetComponentsInChildren<Button>().First(button => button.transform != row);
        }

        [UnityTest] public IEnumerator OrderingEquipmentIsRefusedUntilRequirementsAreMetThenActivates()
        {
            var w = app.World;
            w.State.successfulOrders = 0;
            w.State.money = 1000;
            yield return OpenTab(CoffeeCompendiumPanel.Tab.Shop);
            var order = ButtonLabelled("订购");
            Assert.That(order, Is.Not.Null, "Precision set offers an order button.");
            order.onClick.Invoke();
            yield return null;
            Assert.That(w.PrecisionEquipmentAvailable, Is.False, "Not enough successful orders yet.");
            Assert.That(w.State.money, Is.EqualTo(1000), "A refused order must not spend money.");
            Assert.That(book.toastLabel.text, Does.Contain(CafeWorld.PrecisionOrdersRequired.ToString()));

            w.State.successfulOrders = CafeWorld.PrecisionOrdersRequired;
            book.SelectTab(CoffeeCompendiumPanel.Tab.Restock); yield return Settle();
            book.SelectTab(CoffeeCompendiumPanel.Tab.Shop); yield return Settle();
            ButtonLabelled("订购").onClick.Invoke();
            yield return Settle();
            Assert.That(w.PrecisionEquipmentAvailable, Is.True);
            Assert.That(w.State.money, Is.EqualTo(1000 - CafeWorld.PrecisionActivationCost));
            Assert.That(w.State.unlockedProducts, Does.Contain("espresso"), "Owning the extractor teaches 精粹咖啡.");
            Assert.That(book.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "已到货"), Is.True);
        }

        [UnityTest] public IEnumerator LatteSetRequiresPrecisionFirstAndShopClosesWhileOpen()
        {
            var w = app.World;
            w.State.successfulOrders = CafeWorld.LatteOrdersRequired;
            w.State.money = 1000;
            Assert.That(w.EquipmentOrderCheck(true).success, Is.False, "Latte art needs the precision set first.");
            w.State.open = true;
            Assert.That(w.EquipmentOrderCheck(false).success, Is.False, "No orders during business hours.");
            w.State.open = false;
            Assert.That(w.EquipmentOrderCheck(false).success, Is.True);
            yield return OpenTab(CoffeeCompendiumPanel.Tab.Shop);
            Assert.That(w.State.money, Is.EqualTo(1000), "Browsing the brochure costs nothing.");
        }

        [UnityTest] public IEnumerator RestockFillsShelfForTheQuotedPrice()
        {
            var w = app.World;
            var milk = w.State.entities.First(e => w.Spec(e.specId).Has(Capability.Source) && w.Spec(e.specId).sourceItemId == "milk");
            var spec = w.Spec(milk.specId);
            milk.stock = 0;
            w.State.money = 500;
            int quote = w.RestockQuote(milk, -1, out int missing);
            Assert.That(missing, Is.EqualTo(spec.initialStock));
            yield return OpenTab(CoffeeCompendiumPanel.Tab.Restock);
            RestockButton(spec).onClick.Invoke();
            yield return null;
            Assert.That(milk.stock, Is.EqualTo(spec.initialStock));
            Assert.That(w.State.money, Is.EqualTo(500 - quote));

            w.State.money = 0;
            milk.stock = 0;
            book.SelectTab(CoffeeCompendiumPanel.Tab.Shop); yield return Settle();
            book.SelectTab(CoffeeCompendiumPanel.Tab.Restock); yield return Settle();
            RestockButton(spec).onClick.Invoke();
            yield return null;
            Assert.That(milk.stock, Is.Zero, "Without money the shelf stays empty.");
            Assert.That(w.State.money, Is.Zero);
        }

        [UnityTest] public IEnumerator PracticeNeedsTheCounterAndClosesTheBookWhenStarted()
        {
            Assert.That(app.World.State.currentArea, Is.EqualTo(AreaId.Cafe));
            yield return OpenTab(CoffeeCompendiumPanel.Tab.Tutorials);
            Assert.That(book.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("请先走到吧台")), Is.True);
            ButtonLabelled("暂不可用").onClick.Invoke();
            yield return null;
            Assert.That(app.PracticeTutorialActive, Is.False);
            Assert.That(book.IsOpen, Is.True);
            book.CloseImmediate();

            app.Enter(AreaId.Counter); yield return null;
            var production = app.GetComponent<FreeCafeProductionHud>();
            book = production.bookPanel.GetComponent<CoffeeCompendiumPanel>();
            production.bookButton.onClick.Invoke();
            yield return Settle();
            book.SelectTab(CoffeeCompendiumPanel.Tab.Tutorials); yield return Settle();
            ButtonLabelled("开始练习").onClick.Invoke();
            yield return null;
            Assert.That(book.IsOpen, Is.False, "The book steps aside for the workstation tutorial.");
            Assert.That(app.PracticeTutorialActive, Is.True);
            app.WorkstationTutorial.Stop();
        }
    }
}
