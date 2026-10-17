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
    public sealed class FreeCafeEquipmentPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
        }
        private GameObject loadingOwner;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (loadingOwner != null)
            {
                var owner = loadingOwner.GetComponent<GameFlowBootstrap>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var session = typeof(GameFlowBootstrap).GetField("session", flags).GetValue(owner);
                session?.GetType().GetMethod("Dispose").Invoke(session, null);
                Object.Destroy(loadingOwner);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RestoreWaitsForCalendarBeforeRefreshingDailyStock()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            loadingOwner = new GameObject("Inactive loading session fixture");
            loadingOwner.SetActive(false);
            var owner = loadingOwner.AddComponent<GameFlowBootstrap>();
            var days = loadingOwner.AddComponent<DayFlowController>();
            days.RestoreJson("{\"day\":1,\"state\":5,\"firstDayCafeTutorialCompleted\":true}");
            typeof(GameFlowBootstrap).GetMethod("SetSessionState", flags).Invoke(owner, new object[] { GameSessionState.Loading });
            typeof(FreeCafeRuntime).GetField("gameFlow", flags).SetValue(app, owner);
            typeof(FreeCafeRuntime).GetField("dayFlow", flags).SetValue(app, days);
            typeof(FreeCafeRuntime).GetField("<IsSandboxMode>k__BackingField", flags).SetValue(app, false);
            app.World.ConfigureEquipmentProgression(false);
            app.World.RefreshDailyCupStock("story:3");
            var stock = app.World.Find("s.cups.small");
            stock.stock = 1;
            app.RestoreJson(app.CaptureJson());
            yield return null;
            Assert.That(app.World.State.cupStockDayKey, Is.EqualTo("story:3"));
            Assert.That(app.World.Find("s.cups.small").stock, Is.EqualTo(1), "Loading cannot use the not-yet-restored day 1");
            days.RestoreJson("{\"day\":3,\"state\":5,\"firstDayCafeTutorialCompleted\":true}");
            typeof(GameFlowBootstrap).GetMethod("SetSessionState", flags).Invoke(owner, new object[] { GameSessionState.Playing });
            yield return null;
            Assert.That(app.World.State.cupStockDayKey, Is.EqualTo("story:3"));
            Assert.That(app.World.Find("s.cups.small").stock, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DirectEntryUnlocksToolsAndDoesNotWaitForNarrativeVisits()
        {
            Assert.That(app.IsSandboxMode, Is.True);
            Assert.That(app.World.PrecisionEquipmentAvailable && app.World.LatteEquipmentAvailable, Is.True);
            Assert.That(app.View("v.extractor").gameObject.activeSelf, Is.True);
            Assert.That(app.View("tool.milk-pitcher").gameObject.activeSelf, Is.True);
            Assert.That(app.World.State.unlockedProducts.Count, Is.EqualTo(app.World.Catalog.products.Length));
            app.StartBusiness(); yield return null;
            Assert.That(app.World.State.open, Is.True);
            Assert.That(app.World.State.requests.Count, Is.GreaterThan(0));
            Assert.That(app.World.State.cafeVisits, Is.Empty);
        }
        [UnityTest] public IEnumerator StoryConfigurationHidesToolsUntilPlayerActivatesFromPanel()
        {
            app.World.ConfigureEquipmentProgression(false);
            app.World.SetArea(AreaId.Counter);
            yield return null;
            Assert.That(app.View("v.extractor").gameObject.activeSelf, Is.False);
            Assert.That(app.View("tool.milk-pitcher").gameObject.activeSelf, Is.False);
            Assert.That(app.View("m.grinder").gameObject.activeSelf, Is.True);
            app.World.State.successfulOrders = CafeWorld.PrecisionOrdersRequired;
            Assert.That(app.World.ActivateEquipment(false).success, Is.True);
            yield return null;
            Assert.That(app.View("v.extractor").gameObject.activeSelf, Is.True);
            Assert.That(app.View("tool.milk-pitcher").gameObject.activeSelf, Is.False);
            var hud = Object.FindFirstObjectByType<FreeCafeHud>();
            var entry = hud.transform.Find("EquipmentActivation").GetComponent<Button>();
            Assert.That(entry.interactable, Is.True);
            entry.onClick.Invoke(); yield return null;
            var panel = app.GetComponent<FreeCafeEquipmentPanel>();
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(app.InputBlocked, Is.True);
            Assert.That(PresentationPauseRegistry.IsPaused, Is.True);
            panel.Close(); yield return null;
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(PresentationPauseRegistry.IsPaused, Is.False);
        }
    }
}
