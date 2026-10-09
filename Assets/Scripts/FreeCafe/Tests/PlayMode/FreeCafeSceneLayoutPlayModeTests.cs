using System.Collections;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeSceneLayoutPlayModeTests
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
        [UnityTest] public IEnumerator EntityCaptionsBelongToTheirPrefabAndTravelWithTheirOwner()
        {
            Assert.That(app.areaRoots[1].Find("ProductionWorkspaceLabels"),Is.Null);
            var view=app.View("m.grinder");var caption=view.workspaceCaption;
            Assert.That(caption,Is.Not.Null);Assert.That(caption.transform.parent,Is.SameAs(view.transform));
            Assert.That(caption.raycastTarget,Is.False);Vector3 offset=caption.transform.position-view.transform.position;
            var entity=app.World.Find("m.grinder");entity.x+=.3f;entity.y-=.2f;app.DebugRefreshViews();yield return null;
            Assert.That(Vector3.Distance(caption.transform.position-view.transform.position,offset),Is.LessThan(.001f));
            foreach(var root in app.areaRoots)
                foreach(var other in root.GetComponentsInChildren<FreeEntityView>(true))
                    if(other.workspaceCaption!=null) Assert.That(other.workspaceCaption.transform.parent,Is.SameAs(other.transform));
            var heater=app.View("m.heater");Assert.That(heater.transform.Find("UpgradeHints/ExtraHeater2"),Is.Not.Null);
        }
        [UnityTest] public IEnumerator SaveRestoreKeepsSavedPositionsRatherThanReseedingTheEditorLayout()
        {
            var view=app.View("m.grinder");var entity=app.World.Find("m.grinder");float startX=entity.x;
            entity.x=startX+.5f;string json=app.CaptureJson();entity.x=startX-.5f;app.DebugRefreshViews();
            app.RestoreJson(json);yield return null;
            Assert.That(app.World.Find("m.grinder").x,Is.EqualTo(startX+.5f).Within(.001));
            var local=app.areaRoots[1].InverseTransformPoint(view.transform.position);
            Assert.That(local.x,Is.EqualTo(startX+.5f).Within(.001));Assert.That(app.View("m.grinder"),Is.SameAs(view));
            Assert.That(view.workspaceCaption.transform.parent,Is.SameAs(view.transform));
        }
    }
}
