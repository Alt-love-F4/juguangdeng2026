using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeVesselDeliveryPlayModeTests
    {
        [UnityTest] public IEnumerator RepeatedPotDeliveryKeepsHudAliveAndSameViewDraggable()
        {
            foreach(var p in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))p.Cancel();
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;app.World.EnterProduction();app.World.StartDay();
            if(FreeCafeGMPanel.Instance!=null)FreeCafeGMPanel.Instance.SetVisible(false);
            var vessel=app.World.Find("m.brewer");var view=app.View(vessel.id);var home=view.transform.position;
            for(int i=0;i<3;i++)
            {
                if(app.World.State.requests.Count==0){app.World.EndDay();app.World.StartDay();}
                app.World.TakeAndInteract("s.water",vessel.id,AreaId.Counter,0,0);
                Assert.That(app.World.Interact(vessel.id,"service").success,Is.True);
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(app.DeliverCup().success,Is.True);
                Assert.That(app.View(vessel.id),Is.SameAs(view));Assert.That(view.CanInteract,Is.True);
                float deadline=Time.realtimeSinceStartup+3f;
                while(Vector3.Distance(view.transform.position,home)>.01f && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(Vector3.Distance(view.transform.position,home),Is.LessThan(.01f));
                var e=new PointerEventData(EventSystem.current){position=app.worldCamera.WorldToScreenPoint(view.transform.position)};
                app.BeginDrag(view,e);Assert.That(app.IsDragging,Is.True);app.CancelDrag();yield return null;
            }
            Assert.That(app.GetComponent<FreeCafeProductionHud>().productionRoot.activeSelf,Is.True);
        }
    }
}
