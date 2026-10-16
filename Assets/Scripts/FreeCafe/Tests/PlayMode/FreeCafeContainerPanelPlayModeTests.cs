using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeContainerPanelPlayModeTests
    {
        [UnityTest] public IEnumerator FinishedCupFoamUsesLatteArtVolumeAndPatternWithoutRenamingPitcherInventory()
        {
            foreach(var p in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))p.Cancel();
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;app.Enter(AreaId.Counter);
            if(FreeCafeGMPanel.Instance!=null)FreeCafeGMPanel.Instance.SetVisible(false);
            var w=app.World;
            Assert.That(w.SelectServingCup("s.cups.small").success,Is.True);
            var cup=w.ServingCup;
            cup.contents.Add(new Portion{specId="milkfoam",units=1,milliliters=10});
            cup.contents.Add(new Portion{specId="milkfoam",units=1,milliliters=15});
            cup.latteArtPattern="heart"; // Stale pattern alone must not turn unfinished foam into latte art.
            string foamLabel=w.IngredientLabel(cup.contents[0]);
            Assert.That(foamLabel,Does.Match("奶泡|奶沫"));
            app.ContainerPanels.Inspect(cup.id);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ActiveIngredients(app),Does.Contain(foamLabel).And.Contain("25 ml").And.Not.Contain("图案"));
            cup.latteArt=true;cup.latteArtScore=1;
            foreach(var pattern in new[]{"heart","rosetta","unknown"})
            {
                cup.latteArtPattern=pattern;
                string before=JsonUtility.ToJson(w.State);
                app.ContainerPanels.Inspect(cup.id);yield return new WaitForSecondsRealtime(.2f);
                Assert.That(ActiveIngredients(app),Is.EqualTo(FreeCafeProductionText.LatteArtContents(25,pattern)));
                Assert.That(cup.contents.Count,Is.EqualTo(2),"Rendering must not compact fragmented foam batches");
                Assert.That(w.MilkFoamVolume(cup),Is.EqualTo(25));
                Assert.That(JsonUtility.ToJson(w.State),Is.EqualTo(before),"Presentation must not change inventory or cup metadata");
            }
            var pitcher=w.State.entities.Find(w.IsMilkPitcher);
            Assert.That(pitcher,Is.Not.Null);
            pitcher.contents.Add(new Portion{specId="milkfoam",units=1,milliliters=40});
            pitcher.latteArt=true;pitcher.latteArtPattern="heart"; // Cup-only formatting even if non-cup metadata is stale.
            app.ContainerPanels.Inspect(pitcher.id);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ActiveIngredients(app),Does.Contain(foamLabel).And.Contain("40 ml").And.Not.Contain("图案"));
        }

        private static string ActiveIngredients(FreeCafeRuntime app)
        {
            foreach(var text in app.ContainerPanels.GetComponentsInChildren<TMP_Text>())
                if(text.name=="Ingredients") return text.text;
            Assert.Fail("Expected an active container ingredient card");return null;
        }

        [UnityTest] public IEnumerator CardsStayNonBlockingAndExtractionClosesOnlyAfterQualifiedStop()
        {
            foreach(var p in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))p.Cancel();
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;app.Enter(AreaId.Counter);
            if(FreeCafeGMPanel.Instance!=null)FreeCafeGMPanel.Instance.SetVisible(false);
            var w=app.World;var e=w.Find("v.extractor");var view=app.View(e.id);
            w.State.entities.Add(new WorldEntity{id="panel.grounds",specId="grounds",units=1,area=AreaId.Counter});
            Assert.That(w.Interact("panel.grounds",e.id).success,Is.True);
            app.ContainerPanels.Inspect(e.id);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(app.ContainerPanels.GetComponentInChildren<GraphicRaycaster>(),Is.Null);
            var group=app.ContainerPanels.GetComponentInChildren<CanvasGroup>();Assert.That(group.blocksRaycasts,Is.False);
            Assert.That(app.View(w.ExtractionFilter(e).id).body.enabled,Is.True);
            Assert.That(view.transform.Find("FillStatus").gameObject.activeSelf,Is.False);
            Assert.That(w.TakeAndInteract("s.water","m.heater",AreaId.Counter,0,0).success,Is.True);
            Assert.That(w.OperateProduction("m.heater").success,Is.True);
            app.ContainerPanels.ClearInspection(e.id);yield return new WaitForSecondsRealtime(.2f);
            bool heating=false;foreach(var text in app.ContainerPanels.GetComponentsInChildren<TMP_Text>())if(text.text.Contains("加热中"))heating=true;
            Assert.That(heating,Is.True,"Heating card persists without hover");
            w.Tick(w.Catalog.production.heatSeconds);
            var h=w.Find("m.heater");foreach(var p in h.contents)p.temperature=LiquidTemperature.Boiling;
            Time.timeScale=0; // Domain ticks below explicitly advance the timing test.
            for(int i=0;i<15;i++){Assert.That(w.PourLiquid(h.id,e.id,200f/15,.5f).success,Is.True);w.Tick(.5f);}
            Assert.That(w.FinishPour(e.id).success,Is.True);Time.timeScale=1;yield return new WaitForSecondsRealtime(.2f);
            Assert.That(w.ExtractionSnapshot(e).qualityLabel,Is.EqualTo("优秀精粹咖啡液"));
            Assert.That(w.ExtractionFilter(e),Is.Null);
            view.SetHovered(true);yield return new WaitForSecondsRealtime(.2f);
            Assert.That(view.transform.Find("ExtractionAssembly/LowerJugLid").GetComponent<SpriteRenderer>().enabled,Is.True);
            bool quality=false;foreach(var text in app.ContainerPanels.GetComponentsInChildren<TMP_Text>())if(text.text.Contains("精粹咖啡液"))quality=true;
            Assert.That(quality,Is.True);
            view.PresentPour(1,true);Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,view.body.transform.localEulerAngles.z)),Is.EqualTo(105).Within(.01));
            view.PresentPour(0,false);
        }
    }
}
