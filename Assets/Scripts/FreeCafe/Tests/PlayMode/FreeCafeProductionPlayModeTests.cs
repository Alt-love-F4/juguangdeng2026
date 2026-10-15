using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NUnit.Framework;
using Spotlight.Audio;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeProductionPlayModeTests
    {
        private FreeCafeRuntime app;
        private Mouse testMouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousInputBehavior;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach (var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            previousEditorInputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousInputBehavior=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testMouse=InputSystem.AddDevice<Mouse>("FreeCafeProductionRegressionMouse");
            MouseAt(Vector2.zero,false,false);
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop",LoadSceneMode.Single);yield return null;yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.areaManager.panSmoothTime=0;app.World.EnterProduction();yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale=1;
            if(app!=null) app.CancelDrag();
            if(testMouse!=null && testMouse.added) InputSystem.RemoveDevice(testMouse);
            testMouse=null;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior=previousInputBehavior;
            yield return null;
        }
        private IEnumerator WaitForReturn(FreeEntityView view,Vector3 origin)
        {
            float timeout=Time.unscaledTime+3;
            while((Vector3.Distance(view.transform.position,origin)>=.001f || Mathf.Abs(Mathf.DeltaAngle(view.body.transform.localEulerAngles.z,0))>=.01f) && Time.unscaledTime<timeout) yield return null;
            Assert.That(Vector3.Distance(view.transform.position,origin),Is.LessThan(.001f),"Container must smoothly return to its station within three seconds");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.body.transform.localEulerAngles.z,0)),Is.LessThan(.01f),"Returned container must settle upright within three seconds");
        }
        private IEnumerator WaitForLatteTransition(FreeCafeLatteArtBench bench)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (bench.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bench.IsTransitioning, Is.False, "Latte entry/exit must finish within three unscaled seconds");
        }
        private PointerEventData Pointer(float x,float y) => new PointerEventData(EventSystem.current)
        { position=app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter,x,y)) };
        private void MouseAt(Vector2 position,bool left,bool right)
        {
            var state=new MouseState { position=position };
            if(left) state=state.WithButton(MouseButton.Left);
            if(right) state=state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(testMouse,state);
        }
        [UnityTest] public IEnumerator UsesExistingSceneCanvasAndCupButtonsTargetTheServingTray()
        {
            var hud=app.GetComponent<FreeCafeProductionHud>();Assert.That(hud,Is.Not.Null);Assert.That(hud.productionRoot.activeSelf,Is.True);
            Assert.That(hud.productionRoot.GetComponentInParent<FreeCafeHud>(),Is.Not.Null);
            hud.smallCup.onClick.Invoke();yield return null;Assert.That(app.World.ServingCup.specId,Is.EqualTo("cup.small"));
            hud.largeCup.onClick.Invoke();yield return null;Assert.That(app.World.ServingCup.specId,Is.EqualTo("cup.large"));
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("CoffeeShop"));
        }
        [UnityTest] public IEnumerator GrinderAudioStopsForPauseAndEmptyAndOnlyCompletesOncePerBatch()
        {
            var service = GameAudio.Service;
            Assert.That(service, Is.Not.Null);
            int starts = 0, ready = 0;
            System.Action<string> observe = key =>
            {
                if (key == AudioKeys.Grinder) starts++;
                if (key == AudioKeys.Ready) ready++;
            };
            service.CuePlayed += observe;
            try
            {
                Assert.That(app.World.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success, Is.True);
                Assert.That(app.World.OperateProduction("m.grinder").success, Is.True);
                yield return null;
                Assert.That(starts, Is.EqualTo(1), "State refresh must not restart a running loop");
                Time.timeScale = 0;
                yield return null;
                Time.timeScale = 1;
                yield return null;
                Assert.That(starts, Is.EqualTo(2), "A paused grinder must release and restart its loop once");
                Assert.That(app.World.EmptyProductionMachine("m.grinder").success, Is.True);
                yield return null;
                Assert.That(ready, Is.Zero, "Clearing a processing machine is not batch completion");

                Assert.That(app.World.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success, Is.True);
                Assert.That(app.World.OperateProduction("m.grinder").success, Is.True);
                yield return null;
                Assert.That(starts, Is.EqualTo(3));
                app.World.Tick(app.World.Find("m.grinder").production.remaining + 1f);
                yield return null;
                Assert.That(ready, Is.EqualTo(1));
                app.RestoreJson(app.CaptureJson());
                yield return null;
                yield return null;
                Assert.That(ready, Is.EqualTo(1), "Restoring a ready machine must not replay completion");
            }
            finally
            {
                Time.timeScale = 1;
                service.CuePlayed -= observe;
            }
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
            var start=Pointer(kettle.x,kettle.y);
            MouseAt(start.position,true,false);yield return null;yield return null;
            Physics2D.SyncTransforms();app.BeginDrag(view,start);Assert.That(app.IsDragging,Is.True);
            var pointer=Pointer(cup.x,cup.y+.7f);app.MoveDrag(pointer);
            // Runtime samples the mouse every frame; keep it aligned with the synthetic drag target before right-down locks the receiver.
            MouseAt(pointer.position,true,false);
            yield return null;yield return null;
            Assert.That(app.IsDragging,Is.True);
            Assert.That(app.World.LiquidVolume(cup),Is.Zero,"左键二维拖动不应倾倒");
            MouseAt(pointer.position,true,true);yield return null;yield return null;
            Assert.That(app.LockedPourTargetId,Is.EqualTo(cup.id),"Right-down must lock the intended cup before pouring begins");
            var lifted=view.transform.position;
            MouseAt(pointer.position+Vector2.down*90,true,true);yield return null;yield return null;yield return null;
            Assert.That(Vector3.Distance(view.transform.position,lifted),Is.LessThan(.001f),"倾倒模式固定壶位置");
            Assert.That(app.World.LiquidVolume(cup),Is.GreaterThan(0));Assert.That(app.World.LiquidVolume(kettle),Is.LessThan(200));
            var cupView=app.View(cup.id);
            Assert.That(app.LockedPourTargetId,Is.EqualTo(cup.id));
            Assert.That(cupView.IsDropTarget,Is.True);
            Assert.That(cupView.IsReceivingPour,Is.True);
            Assert.That(cupView.body.color.a,Is.EqualTo(.22f).Within(.001f));
            Assert.That(cupView.liquidPresenter.gameObject.activeSelf,Is.True);
            float before=app.World.LiquidVolume(cup);
            MouseAt(new Vector2(Screen.width*.08f,Screen.height*.08f),true,true);
            yield return null;yield return null;yield return null;
            Assert.That(app.LockedPourTargetId,Is.EqualTo(cup.id),"鼠标离开杯子仍锁住左键落点");
            Assert.That(cupView.IsDropTarget,Is.True);
            Assert.That(app.World.LiquidVolume(cup),Is.GreaterThan(before));
            MouseAt(pointer.position+Vector2.down*90,true,false);yield return null;yield return null;
            Assert.That(app.IsDragging,Is.True,"右键松开后左键仍持有壶");
            Assert.That(cupView.IsReceivingPour,Is.False);
            Assert.That(cupView.liquidPresenter.gameObject.activeSelf,Is.False,"停止接收后即使杯中有液体也隐藏液面");
            Assert.That(view.liquidPresenter.gameObject.activeSelf,Is.False,"停止倾倒后隐藏源容器液面");
            Assert.That(cupView.body.color.a,Is.EqualTo(1f));
            Assert.That(Vector3.Distance(view.transform.position,origin),Is.GreaterThan(.5f),"右键松开不得把容器送回原地");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.body.transform.localEulerAngles.z,0)),Is.LessThanOrEqualTo(view.maxDragLean+.01f),"Resumed left drag may lean, but must leave the pouring tilt");
            app.EndDrag(new PointerEventData(EventSystem.current) { position=pointer.position+Vector2.down*90 });
            MouseAt(pointer.position,false,false);yield return null;
            Assert.That(app.View(kettle.id),Is.SameAs(view));
            Assert.That(kettle.x,Is.EqualTo(app.WorldToArea(AreaId.Counter,origin).x).Within(.001f));
            Assert.That(kettle.y,Is.EqualTo(app.WorldToArea(AreaId.Counter,origin).y).Within(.001f));
            yield return WaitForReturn(view,origin);
        }
        
        [UnityTest] public IEnumerator KettleCanPourFromAboveABrewerAtTheSameStationHeight()
        {
            app.World.TakeAndInteract("s.beans","m.grinder",AreaId.Counter,0,0);app.World.Operate("m.grinder");app.World.Tick(12);
            var grounds=app.World.DragOutputPreview("m.grinder");app.World.TakeMachineOutput("m.grinder",grounds.id,"m.brewer",AreaId.Counter,-3.3f,.2f);
            app.World.TakeAndInteract("s.water","m.heater",AreaId.Counter,0,0);app.World.Operate("m.heater");app.World.Tick(4);
            var kettle=app.World.Find("m.heater");var brewer=app.World.Find("m.brewer");
            var view=app.View(kettle.id);var origin=view.transform.position;
            var start=Pointer(kettle.x,kettle.y);
            MouseAt(start.position,true,false);yield return null;yield return null;
            Physics2D.SyncTransforms();app.BeginDrag(view,start);Assert.That(app.IsDragging,Is.True);
            var pointer=Pointer(brewer.x,brewer.y+.4f);app.MoveDrag(pointer);
            MouseAt(pointer.position,true,false);yield return null;yield return null;
            MouseAt(pointer.position,true,true);yield return null;yield return null;
            Assert.That(app.LockedPourTargetId,Is.EqualTo(brewer.id),"Right-down must lock the intended brewer before pouring begins");
            MouseAt(pointer.position+Vector2.down*90,true,true);yield return null;yield return null;yield return null;
            Assert.That(Mathf.DeltaAngle(0,view.body.transform.localEulerAngles.z),Is.GreaterThan(0),"鼠标向下应倾斜注水");
            app.EndDrag(pointer);MouseAt(pointer.position+Vector2.down*90,false,true);yield return null;
            Assert.That(app.IsDragging,Is.True,"倾倒期间松开左键不应归位");
            MouseAt(pointer.position+Vector2.down*90,false,false);yield return null;yield return null;
            Assert.That(app.IsDragging,Is.False);
            Assert.That(kettle.x,Is.EqualTo(app.WorldToArea(AreaId.Counter,origin).x).Within(.001f));
            Assert.That(kettle.y,Is.EqualTo(app.WorldToArea(AreaId.Counter,origin).y).Within(.001f));
            Assert.That(app.World.LiquidVolume(brewer),Is.GreaterThan(0));Assert.That(app.World.ContainerDescription(brewer),Does.Contain("咖啡"));
            float retainedGrounds=0;foreach(var portion in brewer.contents)retainedGrounds+=app.World.CoffeeGroundsGrams(portion);
            Assert.That(retainedGrounds,Is.GreaterThan(0),"合成后咖啡粉转入液体溯源，不能丢失");
            Assert.That(brewer.contents.Exists(portion=>portion.specId=="grounds"),Is.False);
            yield return WaitForReturn(view,origin);
        }

        [UnityTest] public IEnumerator PhysicalTrayAcceptsDraggedCupAndEmptyBellShowsNonModalToast()
        {
            var service=app.View("service");
            Assert.That(service.hitbox.size,Is.EqualTo(new Vector2(3.2f,2.4f)));
            Assert.That(service.transform.Find("DeliveryFrame"),Is.Not.Null);
            var bell=service.GetComponentInChildren<FreeCafeDeliveryBell>();Assert.That(bell,Is.Not.Null);
            bell.OnPointerClick(new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left});yield return null;
            var toast=Object.FindFirstObjectByType<OperationToastPanel>();Assert.That(toast,Is.Not.Null);
            Assert.That(toast.Message,Does.Contain("没有杯子"));
            Assert.That(toast.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(app.InputBlocked,Is.False);
            app.World.Take("s.cups.small",AreaId.Counter,0,-2,out var cup);yield return null;
            Physics2D.SyncTransforms();app.BeginDrag(app.View(cup.id),Pointer(cup.x,cup.y));
            var tray=app.World.Find("service");app.EndDrag(Pointer(tray.x+1.2f,tray.y+.8f));yield return null;
            Assert.That(app.World.ServingCup,Is.SameAs(cup));
            Assert.That(cup.x,Is.EqualTo(tray.x));Assert.That(cup.y,Is.EqualTo(tray.y));
            Assert.That(app.World.Find(cup.id),Is.Not.Null,"拖入交付区不自动提交");
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.That(Object.FindFirstObjectByType<OperationToastPanel>(),Is.Null);
        }
        [UnityTest] public IEnumerator LatteSurfaceOverlayBeginsAndCancelsWithoutChangingTheCup()
        {
            app.World.SelectServingCup("s.cups.large");
            var cup = app.World.ServingCup;
            var coffee = new Portion { specId = "coffee", units = 1, milliliters = 100, productionScore = 100 };
            var milk = new Portion { specId = "milk", units = 1, milliliters = 100, productionScore = 100 };
            cup.contents.Add(coffee); cup.contents.Add(milk);
            var hud = app.GetComponent<FreeCafeProductionHud>();
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            Assert.That(bench, Is.Not.Null);
            Assert.That(hud.miniGameButton.gameObject.activeSelf, Is.False);
            Assert.That(app.World.RequestProductionMiniGame().success, Is.True);
            string token = app.World.State.production.miniGameToken;
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            Assert.That(bench.IsTransitioning, Is.True);
            yield return WaitForLatteTransition(bench);
            yield return null;
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(FindLatteSurface(bench.transform.parent).isActiveAndEnabled, Is.True);
            Assert.That(overlay.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(hud.miniGamePanel.activeSelf, Is.False, "The real presenter replaces the obsolete placeholder");
            Assert.That(hud.productionRoot.activeSelf, Is.False, "Production cards must not cover the latte surface");
            Assert.That(app.InputBlocked, Is.True);
            overlay.transform.Find("Content/PresetPanel/Cancel").GetComponent<Button>().onClick.Invoke();
            Assert.That(bench.IsTransitioning, Is.True);
            Assert.That(app.InputBlocked, Is.True, "Exit transition must keep gameplay blocked after the token clears");
            yield return WaitForLatteTransition(bench);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!hud.productionRoot.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(hud.productionRoot.activeSelf, Is.True);
            Assert.That(GameObject.Find("LatteArtPresetOverlay"), Is.Null);
            Assert.That(app.World.ServingCup, Is.SameAs(cup));
            Assert.That(cup.contents.Count, Is.EqualTo(2));
            Assert.That(cup.contents[0], Is.SameAs(coffee)); Assert.That(cup.contents[1], Is.SameAs(milk));
            Assert.That(app.World.LiquidVolume(cup), Is.EqualTo(200));
            Assert.That(cup.latteArt, Is.False);
            Assert.That(app.World.ResolveProductionMiniGame(token, true, 100).success, Is.False);
        }
        [UnityTest] public IEnumerator LatteReferenceSelectionAndResetKeepDrawingSurfaceUnobstructed()
        {
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            var caption = bench.transform.parent.Find("Caption").GetComponent<TMPro.TMP_Text>();
            bool captionEnabled = caption.enabled;
            bench.Begin("latte", "debug-cup", "ui-selection-test", (_, _, _) => { });
            yield return WaitForLatteTransition(bench);
            yield return null;
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            var panel = overlay.transform.Find("Content/PresetPanel");
            var list = panel.Find("PresetList");
            Assert.That(list.childCount, Is.EqualTo(5));
            Assert.That(list.Find("heartButton/Selected").gameObject.activeSelf, Is.True);
            Assert.That(caption.enabled, Is.False, "Old placeholder caption must not cover the cup");
            list.Find("tulipButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(list.Find("heartButton/Selected").gameObject.activeSelf, Is.False);
            Assert.That(list.Find("tulipButton/Selected").gameObject.activeSelf, Is.True);
            Assert.That(list.Find("tulipButton/Preview").GetComponent<RawImage>().raycastTarget, Is.False);
            Assert.That(overlay.transform.Find("Content/DrawingHint").GetComponent<TMPro.TMP_Text>().raycastTarget, Is.False);
            Assert.That(panel.Find("Submit").GetComponent<Button>().interactable, Is.False);
            panel.Find("Reset").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(list.Find("tulipButton/Selected").gameObject.activeSelf, Is.True, "Reset preserves the selected reference");
            Assert.That(bench.LastPatternId, Is.Null);
            Assert.That(panel.Find("Submit").GetComponent<Button>().interactable, Is.False);
            var statusText = panel.Find("StatusCard/Status").GetComponent<TMPro.TMP_Text>();
            Assert.That(statusText.color.r, Is.GreaterThan(.8f), "Status must remain readable on a dark card");
            Assert.That(statusText.text, Does.Contain("按住左键"));
            bench.Cancel("ui-selection-test");
            yield return WaitForLatteTransition(bench);
            yield return null;
            Assert.That(caption.enabled, Is.EqualTo(captionEnabled));
            Assert.That(GameObject.Find("LatteArtPresetOverlay"), Is.Null);
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
            yield return WaitForLatteTransition(bench);
            Assert.That(layout.localPosition, Is.EqualTo(bench.dock.dockedLocalPosition));
            Assert.That(app.worldCamera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(app.worldCamera.orthographicSize, Is.EqualTo(cameraSize));
            string token = app.World.State.production.miniGameToken;
            app.World.ResolveProductionMiniGame(token, false, 0);
            yield return WaitForLatteTransition(bench);
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
                yield return WaitForLatteTransition(bench);
                Bounds bounds = background.bounds;
                float halfHeight = app.worldCamera.orthographicSize;
                Assert.That(bench.layoutHeightFraction, Is.EqualTo(.9f).Within(.0001f));
                Assert.That(layout.localScale.x / scale.x, Is.LessThanOrEqualTo(1f + .00001f), "Wide views must not enlarge the authored cup");
                Assert.That(layout.localScale.y / scale.y, Is.LessThanOrEqualTo(1f + .00001f));
                Assert.That(bounds.size.y, Is.LessThanOrEqualTo(halfHeight * 2f * .9f + .001f), "Contain the composition within ninety percent of view height");
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(halfHeight * 2f * 2.5f + .001f));
                var matteTransform = layout.Find("LatteBackdrop");
                Assert.That(matteTransform, Is.Not.Null);
                var matte = matteTransform.GetComponent<SpriteRenderer>();
                Assert.That(matte.gameObject.activeInHierarchy, Is.True);
                Assert.That(matte.color.a, Is.EqualTo(1f));
                Assert.That(matte.sortingOrder, Is.LessThan(background.sortingOrder));
                Bounds coverage = matte.bounds;
                Assert.That(coverage.min.x, Is.LessThanOrEqualTo(cameraPosition.x - halfHeight * 2.5f));
                Assert.That(coverage.max.x, Is.GreaterThanOrEqualTo(cameraPosition.x + halfHeight * 2.5f));
                Assert.That(coverage.min.y, Is.LessThanOrEqualTo(cameraPosition.y - halfHeight));
                Assert.That(coverage.max.y, Is.GreaterThanOrEqualTo(cameraPosition.y + halfHeight));
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
            yield return WaitForLatteTransition(bench);
            Assert.That(layout.localScale, Is.EqualTo(scale));
            Assert.That(surface.transform.localPosition, Is.EqualTo(planeLocal));
            Assert.That(layout.Find("LatteBackdrop").gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator LattePointerAndMilkUseTheSameMeshUVOnBothAxes()
        {
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            app.World.SelectServingCup("s.cups.small");
            app.World.RequestProductionMiniGame();
            yield return WaitForLatteTransition(bench);
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
            yield return WaitForLatteTransition(bench);
        }

        [UnityTest] public IEnumerator MilkPitcherDropUsesRealDrawingAndDiscardsCurrentFoamOnResetWithoutRefundingOnCancel()
        {
            var world = app.World;
            var pitcher = world.Find("tool.milk-pitcher");
            Assert.That(pitcher, Is.Not.Null);
            Assert.That(app.View(pitcher.id), Is.Not.Null);
            Assert.That(app.View("tool.latte-art"), Is.Null);
            Assert.That(world.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0, 0).success, Is.True);
            app.Click(app.View(pitcher.id));
            Assert.That(world.MilkFoamVolume(pitcher), Is.GreaterThan(0));
            Assert.That(world.CanPour(pitcher), Is.False);
            Assert.That(world.Take("s.cups.large", AreaId.Counter, 0, -2, out var cup).success, Is.True);
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10f });
            yield return null;
            Physics2D.SyncTransforms();
            app.BeginDrag(app.View(pitcher.id), Pointer(pitcher.x, pitcher.y));
            Assert.That(app.IsDragging, Is.True);
            app.EndDrag(Pointer(cup.x, cup.y));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(world.State.production.miniGamePitcherId, Is.EqualTo(pitcher.id));
            Assert.That(world.State.production.miniGameCupId, Is.EqualTo(cup.id));
            Assert.That(world.ServingCup, Is.Null);
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            yield return WaitForLatteTransition(bench);
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay, Is.Not.Null);
            var counter = overlay.transform.Find("Content/FoamUsage").GetComponent<TMPro.TMP_Text>();
            Assert.That(counter.text, Does.Contain("已用奶泡 0.0 ml"));
            var surface = FindLatteSurface(bench.transform.parent);
            Vector2 center = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(Vector3.zero));
            float initialFoam = world.MilkFoamVolume(pitcher);
            for (int sample = 0; sample < 8; sample++)
            {
                MouseAt(center + new Vector2((sample - 4) * 8, 0), true, false);
                yield return null;
            }
            MouseAt(center, false, false); yield return null; yield return null;
            float used = world.State.production.miniGameFoamUsedMl;
            Assert.That(used, Is.GreaterThan(0));
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(used).Within(.01f));
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(10f + used).Within(.01f));
            Assert.That(world.MilkFoamVolume(pitcher) + used, Is.EqualTo(initialFoam).Within(.01f));
            Assert.That(counter.text, Does.Contain("已用奶泡 " + used.ToString("0.0") + " ml"));
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.GreaterThanOrEqualTo(5));
            string captureDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../output/milk-pitcher"));
            System.IO.Directory.CreateDirectory(captureDirectory);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory, "latte-ui.png"));
            yield return null;
            overlay.transform.Find("Content/PresetPanel/Reset").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.Zero);
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.Zero);
            Assert.That(world.MilkFoamVolume(cup), Is.Zero);
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(10f).Within(.01f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(initialFoam - used).Within(.01f));
            overlay.transform.Find("Content/PresetPanel/Cancel").GetComponent<Button>().onClick.Invoke();
            yield return WaitForLatteTransition(bench); yield return null;
            Assert.That(app.InputBlocked, Is.False);
            Assert.That(world.MilkFoamVolume(cup), Is.Zero);
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(10f).Within(.01f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(initialFoam - used).Within(.01f));
        }

        [UnityTest] public IEnumerator MilkPitcherFullCupStopsActualDrawingWithoutConsumingExtraFoam()
        {
            var world = app.World;
            var pitcher = world.Find("tool.milk-pitcher");
            Assert.That(world.Take("s.cups.large", AreaId.Counter, 0, -2, out var cup).success, Is.True);
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 399.5f });
            Assert.That(world.TakeAndInteract("s.milk", pitcher.id, AreaId.Counter, 0, 0).success, Is.True);
            Assert.That(world.FrothMilk(pitcher.id).success, Is.True);
            float initialFoam = world.MilkFoamVolume(pitcher);
            Assert.That(world.Interact(pitcher.id, cup.id).success, Is.True);
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            yield return WaitForLatteTransition(bench);
            var surface = FindLatteSurface(bench.transform.parent);
            Vector2 center = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(Vector3.zero));
            MouseAt(center, true, false);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(400).Within(.001f));
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.EqualTo(.5f).Within(.001f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.EqualTo(initialFoam - .5f).Within(.001f));
            int pointCount = ((ILatteArtSurface)surface).StrokePointCount;
            MouseAt(center + Vector2.right * 20, true, false);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.EqualTo(pointCount));
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay.transform.Find("Content/FoamUsage").GetComponent<TMPro.TMP_Text>().text, Does.Contain("杯子已满"));
            MouseAt(center, false, false); yield return null;
            overlay.transform.Find("Content/PresetPanel/Cancel").GetComponent<Button>().onClick.Invoke();
            yield return WaitForLatteTransition(bench);
        }

        [UnityTest] public IEnumerator MilkPitcherExhaustionStopsDrawingAndKeepsUsageAfterReturn()
        {
            var world = app.World;
            var pitcher = world.Find("tool.milk-pitcher");
            pitcher.contents.Add(new Portion { specId = "milkfoam", units = 1, milliliters = .5f });
            Assert.That(world.Take("s.cups.large", AreaId.Counter, 0, -2, out var cup).success, Is.True);
            cup.contents.Add(new Portion { specId = "coffee", units = 1, milliliters = 10f });
            Assert.That(world.Interact(pitcher.id, cup.id).success, Is.True);
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            yield return WaitForLatteTransition(bench);
            var surface = FindLatteSurface(bench.transform.parent);
            Vector2 center = app.worldCamera.WorldToScreenPoint(surface.transform.TransformPoint(Vector3.zero));
            MouseAt(center, true, false); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(world.State.production.miniGameFoamUsedMl, Is.EqualTo(.5f).Within(.001f));
            Assert.That(world.MilkFoamVolume(pitcher), Is.Zero);
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(.5f).Within(.001f));
            Assert.That(world.LiquidVolume(cup), Is.EqualTo(10.5f).Within(.001f));
            int pointCount = ((ILatteArtSurface)surface).StrokePointCount;
            MouseAt(center + Vector2.right * 20, true, false); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(((ILatteArtSurface)surface).StrokePointCount, Is.EqualTo(pointCount));
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay.transform.Find("Content/FoamUsage").GetComponent<TMPro.TMP_Text>().text, Does.Contain("奶泡已用完"));
            MouseAt(center, false, false); yield return null;
            overlay.transform.Find("Content/PresetPanel/Cancel").GetComponent<Button>().onClick.Invoke();
            yield return WaitForLatteTransition(bench);
            Assert.That(world.MilkFoamVolume(cup), Is.EqualTo(.5f).Within(.001f));
        }

        [UnityTest] public IEnumerator LatteShortcutExistsOnlyInGMAndLabelsItsUnlimitedDebugMode()
        {
            var hud = app.GetComponent<FreeCafeProductionHud>();
            Assert.That(hud.miniGameButton.gameObject.activeSelf, Is.False);
            Assert.That(app.View("tool.latte-art"), Is.Null);
            app.World.SelectServingCup("s.cups.large");
            var gm = FreeCafeGMPanel.Instance;
            gm.SetVisible(true);
            Assert.That(gm.SelectCategory("状态诊断"), Is.True);
            yield return null;
            Button shortcut = null;
            foreach (var button in gm.GetComponentsInChildren<Button>())
            {
                var label = button.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null && label.text.StartsWith("拉花快捷入口（GM）")) { shortcut = button; break; }
            }
            Assert.That(shortcut, Is.Not.Null);
            shortcut.onClick.Invoke();
            Assert.That(gm.IsVisible, Is.False);
            Assert.That(app.World.State.production.miniGameToken, Is.Not.Null.And.Not.Empty);
            Assert.That(app.World.State.production.miniGamePitcherId, Is.Null);
            var bench = Object.FindFirstObjectByType<FreeCafeLatteArtBench>();
            yield return WaitForLatteTransition(bench);
            var overlay = GameObject.Find("LatteArtPresetOverlay");
            Assert.That(overlay.transform.Find("Content/FoamUsage").GetComponent<TMPro.TMP_Text>().text, Does.Contain("GM 调试模式"));
            overlay.transform.Find("Content/PresetPanel/Cancel").GetComponent<Button>().onClick.Invoke();
            yield return WaitForLatteTransition(bench);
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
