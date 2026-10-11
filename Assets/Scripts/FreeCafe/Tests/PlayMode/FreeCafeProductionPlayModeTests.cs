using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProductionPlayModeTests
    {
        private FreeCafeRuntime app;
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop",LoadSceneMode.Single);yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;app.World.EnterProduction();yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;yield return null; }
        private PointerEventData Pointer(float x,float y) => new PointerEventData(EventSystem.current)
        { position=app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter,x,y)) };
        [UnityTest] public IEnumerator UsesExistingSceneCanvasAndCupButtonsTargetTheServingTray()
        {
            var hud=app.GetComponent<FreeCafeProductionHud>();Assert.That(hud,Is.Not.Null);Assert.That(hud.productionRoot.activeSelf,Is.True);
            Assert.That(hud.productionRoot.GetComponentInParent<FreeCafeHud>(),Is.Not.Null);
            hud.smallCup.onClick.Invoke();yield return null;Assert.That(app.World.ServingCup.specId,Is.EqualTo("cup.small"));
            hud.largeCup.onClick.Invoke();yield return null;Assert.That(app.World.ServingCup.specId,Is.EqualTo("cup.large"));
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CoffeeShop"));
        }
        [UnityTest] public IEnumerator InvalidActualSourceDragReturnsWithoutWithdrawingStock()
        {
            var milk=app.World.Find("s.milk");var grinder=app.World.Find("m.grinder");int stock=milk.stock,count=app.World.State.entities.Count;
            Physics2D.SyncTransforms();app.BeginDrag(app.View(milk.id),Pointer(milk.x,milk.y));Assert.That(app.IsDragging,Is.True);
            app.EndDrag(Pointer(grinder.x,grinder.y));yield return new WaitForSecondsRealtime(.3f);
            Assert.That(milk.stock,Is.EqualTo(stock));Assert.That(app.World.State.entities.Count,Is.EqualTo(count));
            Assert.That(grinder.production.phase,Is.EqualTo(ProductionPhase.Empty));Assert.That(app.IsDragging,Is.False);
        }
        [UnityTest] public IEnumerator ActualBeanDragAndMachineClickStartTheNewGrinder()
        {
            var beans=app.World.Find("s.beans");var grinder=app.World.Find("m.grinder");int stock=beans.stock;
            Physics2D.SyncTransforms();app.BeginDrag(app.View(beans.id),Pointer(beans.x,beans.y));app.EndDrag(Pointer(grinder.x,grinder.y));yield return null;
            Assert.That(beans.stock,Is.EqualTo(stock-1));Assert.That(grinder.production.phase,Is.EqualTo(ProductionPhase.Loaded));
            app.Click(app.View(grinder.id));Assert.That(grinder.production.phase,Is.EqualTo(ProductionPhase.Processing));
            app.World.Tick(12);yield return null;Assert.That(app.World.DragOutputPreview(grinder.id),Is.Not.Null);
        }
        [UnityTest] public IEnumerator RealKettleDragPoursMillilitersAndReturnsTheSameEntityToItsStation()
        {
            app.World.SelectServingCup("s.cups.large");app.World.TakeAndInteract("s.milk","m.heater",AreaId.Counter,0,0);app.World.OperateProduction("m.heater");app.World.Tick(4);
            var kettle=app.World.Find("m.heater");var cup=app.World.ServingCup;var view=app.View(kettle.id);Vector3 origin=view.transform.position;
            Physics2D.SyncTransforms();app.BeginDrag(view,Pointer(kettle.x,kettle.y));Assert.That(app.IsDragging,Is.True);
            var pointer=Pointer(cup.x,cup.y+.7f);app.MoveDrag(pointer);
            yield return null;yield return null;yield return null;app.EndDrag(pointer);yield return null;
            Assert.That(app.World.LiquidVolume(cup),Is.GreaterThan(0));Assert.That(app.World.LiquidVolume(kettle),Is.LessThan(200));
            Assert.That(app.View(kettle.id),Is.SameAs(view));Assert.That(view.transform.position,Is.EqualTo(origin));
        }
        
        [UnityTest] public IEnumerator KettleCanPourFromAboveABrewerAtTheSameStationHeight()
        {
            app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0);app.World.Operate("m.grinder");app.World.Tick(12);
            var grounds=app.World.DragOutputPreview("m.grinder");app.World.TakeMachineOutput("m.grinder",grounds.id,"m.brewer",AreaId.Counter,-3.3f,.2f);
            app.World.TakeAndInteract("s.water","m.heater",AreaId.Counter,0,0);app.World.Operate("m.heater");app.World.Tick(4);
            var kettle=app.World.Find("m.heater");var brewer=app.World.Find("m.brewer");Physics2D.SyncTransforms();
            app.BeginDrag(app.View(kettle.id),Pointer(kettle.x,kettle.y));app.MoveDrag(Pointer(brewer.x,brewer.y+1.3f));yield return null;
            var pointer=Pointer(brewer.x,brewer.y+.4f);app.MoveDrag(pointer);yield return null;yield return null;yield return null;
            app.EndDrag(pointer);yield return null;
            Assert.That(app.World.LiquidVolume(brewer),Is.GreaterThan(0));Assert.That(brewer.production.brewStarted,Is.True);
            Assert.That(brewer.production.phase,Is.EqualTo(ProductionPhase.Loaded),"不足水量可继续，不丢掉咖啡粉");
        }

        [UnityTest] public IEnumerator MiniGamePlaceholderTriggersCancelsAndLeavesTheCupUntouched()
        {
            app.World.SelectServingCup("s.cups.large");var cup=app.World.ServingCup;
            cup.contents.Add(new Portion{specId="coffee",units=1,milliliters=100,productionScore=100});
            cup.contents.Add(new Portion{specId="milk",units=1,milliliters=100,productionScore=100});
            var hud=app.GetComponent<FreeCafeProductionHud>();hud.miniGameButton.onClick.Invoke();yield return null;
            Assert.That(hud.miniGamePanel.activeSelf,Is.True);Assert.That(app.InputBlocked,Is.True);
            string token=app.World.State.production.miniGameToken;hud.cancelMiniGame.onClick.Invoke();yield return null;
            Assert.That(app.InputBlocked,Is.False);Assert.That(hud.miniGamePanel.activeSelf,Is.False);Assert.That(app.World.LiquidVolume(cup),Is.EqualTo(200));
            Assert.That(app.World.ResolveProductionMiniGame(token,true,100).success,Is.False);
        }
        [UnityTest] public IEnumerator LatteBenchDocksIndependentlyAndRestoresTheProductionLayout()
        {
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            Assert.That(bench, Is.Not.Null);
            var layout = bench.transform.parent;
            Vector3 home = layout.localPosition, scale = layout.localScale;
            Vector3 cameraPosition = app.worldCamera.transform.position;
            float cameraSize = app.worldCamera.orthographicSize;
            Assert.That(home.x, Is.GreaterThan(20), "Idle latte bench must not cover the production area");
            app.World.SelectServingCup("s.cups.small");
            Assert.That(app.World.LiquidVolume(app.World.ServingCup), Is.Zero);
            Assert.That(app.World.RequestProductionMiniGame().success, Is.True);
            yield return null;
            Assert.That(layout.localPosition, Is.EqualTo(bench.dock.dockedLocalPosition));
            Assert.That(app.worldCamera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(app.worldCamera.orthographicSize, Is.EqualTo(cameraSize));
            string token = app.World.State.production.miniGameToken;
            app.World.ResolveProductionMiniGame(token, false, 0);
            yield return null;
            Assert.That(layout.localPosition, Is.EqualTo(home));
            Assert.That(layout.localScale, Is.EqualTo(scale));
            Assert.That(app.areaRoots[(int)AreaId.Counter].GetComponent<FreeCafeSubSceneDock>().IsDocked, Is.True);
        }

        [UnityTest] public IEnumerator LatteBackgroundCoversWideViewWithoutMovingTheCameraOrLosingTheCupAlignment()
        {
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            var layout = bench.transform.parent;
            var surface = FindLatteSurface(layout);
            var background = bench.GetComponent<SpriteRenderer>();
            var originalSprite = background.sprite;
            Vector3 planeLocal = surface.transform.localPosition;
            Vector3 scale = layout.localScale;
            Vector3 cameraPosition = app.worldCamera.transform.position;
            float cameraSize = app.worldCamera.orthographicSize;
            float oldAspect = app.worldCamera.aspect;
            try
            {
                app.worldCamera.aspect = 2.5f;
                bench.Begin("latte", "debug-cup", "cover-test", (_, _, _) => { });
                yield return null;
                Bounds bounds = background.bounds;
                float halfHeight = app.worldCamera.orthographicSize;
                Assert.That(bounds.min.x, Is.LessThanOrEqualTo(cameraPosition.x - halfHeight * 2.5f));
                Assert.That(bounds.max.x, Is.GreaterThanOrEqualTo(cameraPosition.x + halfHeight * 2.5f));
                Assert.That(bounds.min.y, Is.LessThanOrEqualTo(cameraPosition.y - halfHeight));
                Assert.That(bounds.max.y, Is.GreaterThanOrEqualTo(cameraPosition.y + halfHeight));
                Assert.That(background.sprite, Is.SameAs(originalSprite), "Keep the authored background image");
                Assert.That(surface.transform.localPosition, Is.EqualTo(planeLocal));
                Assert.That(layout.localScale.x / scale.x, Is.EqualTo(layout.localScale.y / scale.y).Within(.00001f));
                Assert.That(app.worldCamera.transform.position, Is.EqualTo(cameraPosition));
                Assert.That(app.worldCamera.orthographicSize, Is.EqualTo(cameraSize));
            }
            finally
            {
                bench.Cancel("cover-test");
                app.worldCamera.aspect = oldAspect;
            }
        }

        [UnityTest] public IEnumerator LattePointerAndMilkUseTheSameMeshUVOnBothAxes()
        {
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            app.World.SelectServingCup("s.cups.small");
            app.World.RequestProductionMiniGame();
            yield return null;
            var surface = FindLatteSurface(bench.transform.parent);
            var map = surface.GetType().GetMethod("TryGetSurfaceUV");
            Assert.That(map, Is.Not.Null);
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            int checkedPoints = 0;
            Vector2 pointer = default, expectedUV = default;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!Mathf.Approximately(Mathf.Abs(vertices[i].x), 2f) || !Mathf.Approximately(Mathf.Abs(vertices[i].z), 2f)) continue;
                Vector2 screen = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(vertices[i]));
                object[] args = { screen, Vector2.zero };
                Assert.That((bool)map.Invoke(surface, args), Is.True);
                Assert.That(Vector2.Distance((Vector2)args[1], uvs[i]), Is.LessThan(.0001f), "Both axes must agree with rendered mesh UVs");
                checkedPoints++;
                pointer = screen; expectedUV = uvs[i];
            }
            Assert.That(checkedPoints, Is.EqualTo(4));
            var mouse = Mouse.current;
            bool addedMouse = mouse == null;
            if (addedMouse) mouse = InputSystem.AddDevice<Mouse>();
            var previousPosition = mouse.position.ReadValue();
            try
            {
                // Exercise real pointer input, brush preview, GPU deposition, and retained milk.
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
                yield return null; yield return null; yield return null;
                var material = (Material)surface.GetType().GetProperty("SurfaceMaterial").GetValue(surface);
                Vector4 preview = material.GetVector("_BrushPreviewUV");
                Assert.That(Vector2.Distance(new Vector2(preview.x, preview.y), expectedUV), Is.LessThan(.0001f));
                Assert.That(material.GetFloat("_BrushPreviewActive"), Is.EqualTo(1));
                var milk = (RenderTexture)surface.GetType().GetProperty("MilkTexture").GetValue(surface);
                var oldActive = RenderTexture.active;
                var readback = new Texture2D(milk.width, milk.height, TextureFormat.RGBAFloat, false, true);
                try
                {
                    RenderTexture.active = milk;
                    readback.ReadPixels(new Rect(0, 0, milk.width, milk.height), 0, 0);
                    readback.Apply();
                    Assert.That(readback.GetPixelBilinear(expectedUV.x, expectedUV.y).r, Is.GreaterThan(.001f), "Milk must appear under the same pointer UV, not mirrored");
                    Assert.That(readback.GetPixelBilinear(1 - expectedUV.x, 1 - expectedUV.y).r, Is.LessThan(.001f));
                }
                finally { RenderTexture.active = oldActive; Object.Destroy(readback); }
            }
            finally
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = previousPosition });
                if (addedMouse) InputSystem.RemoveDevice(mouse);
                app.World.ResolveProductionMiniGame(app.World.State.production.miniGameToken, false, 0);
            }
        }

        private static MonoBehaviour FindLatteSurface(Transform layout)
        {
            foreach (var component in layout.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is ILatteArtSurface) return component;
            Assert.Fail("Existing latte bench must contain its Plane surface");
            return null;
        }

        [UnityTest] public IEnumerator ReturnButtonReturnsToTheEnteringAreaAndPreservesState()
        {
            app.Enter(AreaId.TopFloor);app.Enter(AreaId.Counter);yield return null;
            app.World.SelectServingCup("s.cups.small");string cupId=app.World.ServingCup.id;
            app.GetComponent<FreeCafeProductionHud>().returnButton.onClick.Invoke();yield return null;
            Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.TopFloor));Assert.That(app.World.ServingCup.id,Is.EqualTo(cupId));
            Assert.That(app.areaManager.CurrentArea,Is.EqualTo(AreaId.TopFloor));
        }
    }
}
