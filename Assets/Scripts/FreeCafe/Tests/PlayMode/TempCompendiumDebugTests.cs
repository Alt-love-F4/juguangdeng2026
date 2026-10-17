using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class TempCompendiumDebugTests
    {
        [UnityTest] public IEnumerator Diagnose()
        {
            GameSaveParticipantRegistry.PendingRestore = null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");
            yield return null; yield return null;
            var app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            var hud = app.GetComponent<FreeCafeProductionHud>();
            app.areaManager.panSmoothTime = 0;
            app.World.StartDay();
            app.World.DebugSpawnCustomers(2);
            app.Enter(AreaId.Counter);
            yield return null;
            var request = app.World.Request(app.World.State.focusedRequestId);
            Debug.Log("DIAG focused=" + app.World.State.focusedRequestId + " product=" + request?.productId);
            var book = hud.bookPanel.GetComponent<CoffeeCompendiumPanel>();
            try { book.Open(app, request?.productId); }
            catch (System.Exception e) { Debug.Log("DIAG EXC " + e); }
            Debug.Log("DIAG index=" + book.RecipeIndex + " tab=" + book.CurrentTab);
            book.CloseImmediate();
        }
    }
}
