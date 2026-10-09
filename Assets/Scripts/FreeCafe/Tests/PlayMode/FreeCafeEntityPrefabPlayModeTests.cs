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
    public sealed class FreeCafeEntityPrefabPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop");yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;yield return null; }
        [UnityTest] public IEnumerator AuthoredInstancesAreAdoptedWithoutDuplicateViewsOrStateChildren()
        {
            var ids=new HashSet<string>();
            foreach(var entry in app.World.Catalog.layout)
            {
                var view=app.View(entry.id);Assert.That(view,Is.Not.Null);
                Assert.That(view.AuthoringEntityId,Is.EqualTo(entry.id),"必须复用场景中的实例");
                Assert.That(view.PrefabSpecId,Is.EqualTo(entry.specId));Assert.That(view.Runtime,Is.SameAs(app));
                Assert.That(view.transform.parent,Is.SameAs(app.areaRoots[(int)entry.area]));
                foreach(string name in new[]{"FillStatus","FillBar","FillBarBackground"})
                {
                    int count=0;foreach(Transform child in view.transform) if(child.name==name) count++;
                    Assert.That(count,Is.EqualTo(1),entry.id+" / "+name);
                }
            }
            foreach(var root in app.areaRoots)
                foreach(var view in root.GetComponentsInChildren<FreeEntityView>(true))
                    if(!string.IsNullOrEmpty(view.EntityId)) Assert.That(ids.Add(view.EntityId),Is.True,"重复实体视图 "+view.EntityId);
            yield return null;
        }
        [UnityTest] public IEnumerator SourceDragGhostUsesTheSourceItemPrefabAndCancelsWithoutStockLoss()
        {
            var source=app.World.Find("s.water");int stock=source.stock;
            Physics2D.SyncTransforms();var pointer=new PointerEventData(EventSystem.current)
            { position=app.worldCamera.WorldToScreenPoint(app.View(source.id).transform.position) };
            app.BeginDrag(app.View(source.id),pointer);Assert.That(app.IsDragging,Is.True);
            var ghost=GameObject.Find("UncommittedPickup").GetComponent<FreeEntityView>();
            Assert.That(ghost.PrefabSpecId,Is.EqualTo("water"));Assert.That(ghost.AuthoringEntityId,Is.Empty);
            app.CancelDrag();yield return null;Assert.That(source.stock,Is.EqualTo(stock));
        }
        [UnityTest] public IEnumerator SaveRestoreKeepsTheAuthoredMachineIdentityAndNewCupsUseDedicatedPrefabs()
        {
            var grinder=app.View("m.grinder");
            app.World.SelectServingCup("s.cups.large");var cup=app.View(app.World.ServingCup.id);
            Assert.That(cup.PrefabSpecId,Is.EqualTo("cup.large"));Assert.That(cup.AuthoringEntityId,Is.Empty);
            string json=app.CaptureJson();app.World.DiscardProductionCup();app.RestoreJson(json);yield return null;
            Assert.That(app.View("m.grinder"),Is.SameAs(grinder));
            Assert.That(app.View(app.World.ServingCup.id).PrefabSpecId,Is.EqualTo("cup.large"));
        }
    }
}
