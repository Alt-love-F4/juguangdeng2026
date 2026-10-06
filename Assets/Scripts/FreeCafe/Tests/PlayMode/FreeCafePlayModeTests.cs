using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafePlayModeTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale=1;GameSaveParticipantRegistry.PendingRestore=null;
            yield return SceneManager.LoadSceneAsync("CoffeeShop",LoadSceneMode.Single);yield return null;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1;yield return null; }
        private PointerEventData Pointer(FreeCafeRuntime app,float x,float y) => new PointerEventData(EventSystem.current) {position=app.worldCamera.WorldToScreenPoint(new Vector3(x,y,0))};
        [UnityTest] public IEnumerator FullScreenCameraSupportsQueuedMouseButtonsAndExistingEntityDrag()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();var hud=Object.FindFirstObjectByType<FreeCafeHud>();
            Assert.That(hud.recipePanel.activeSelf,Is.False,"Recipe modal must not block entities at startup");
            Assert.That(app.worldCamera.isActiveAndEnabled,Is.True);Assert.That(app.worldCamera.rect,Is.EqualTo(new Rect(0,0,1,1)));
            Assert.That(app.worldCamera.GetComponent<Physics2DRaycaster>().isActiveAndEnabled,Is.True);
            foreach(var component in app.worldCamera.GetComponents<MonoBehaviour>())
                Assert.That(component.GetType().Name,Is.Not.EqualTo("FreeCafeViewport"));
            var previousBackground=InputSystem.settings.backgroundBehavior;var mouse=InputSystem.AddDevice<Mouse>("FreeCafeStandardCameraMouse");
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                yield return ClickWithQueuedMouse(mouse,hud.startButton);
                Assert.That(app.World.State.open,Is.True,"Start button must receive actual pointer input");
                yield return ClickWithQueuedMouse(mouse,hud.areaButtons[(int)AreaId.Counter]);
                Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
                app.World.Take("s.cups.large",AreaId.Counter,1,-1,out var cup);Physics2D.SyncTransforms();
                Vector2 start=app.worldCamera.WorldToScreenPoint(app.View(cup.id).transform.position);
                Vector2 end=app.worldCamera.WorldToScreenPoint(new Vector3(3,-2,0));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=start});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=start}.WithButton(MouseButton.Left));yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=start+new Vector2(25,0)}.WithButton(MouseButton.Left));yield return null;yield return null;
                Assert.That(app.IsDragging,Is.True,"Mouse must reach Collider2D through Physics2DRaycaster without a viewport component");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=end}.WithButton(MouseButton.Left));yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=end});yield return null;yield return null;
                Assert.That(app.IsDragging,Is.False);Assert.That(cup.x,Is.EqualTo(3).Within(.01));Assert.That(cup.y,Is.EqualTo(-2).Within(.01));
                Assert.That(app.worldCamera.rect,Is.EqualTo(new Rect(0,0,1,1)),"No script should crop the camera during interaction");
            }
            finally {InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=previousBackground;}
        }
        [UnityTest] public IEnumerator DragUsesPressEventCameraAndRayPlaneInsteadOfHardcodedCameraDepth()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
            app.World.Take("s.cups.large",AreaId.Counter,0,-1,out var cup);
            var camera=app.worldCamera;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
            var wrongCameraGo=new GameObject("NotThePointerCamera",typeof(Camera));var wrongCamera=wrongCameraGo.GetComponent<Camera>();wrongCamera.enabled=false;
            try
            {
                camera.transform.position=new Vector3(0,2,-10);camera.transform.rotation=Quaternion.Euler(10,0,0);
                app.worldCamera=wrongCamera;
                var pointer=new PointerEventData(EventSystem.current){position=camera.WorldToScreenPoint(new Vector3(cup.x,cup.y,0)),pointerPressRaycast=new RaycastResult{module=camera.GetComponent<Physics2DRaycaster>()}};
                pointer.pressPosition=pointer.position;
                Assert.That(pointer.pressEventCamera,Is.SameAs(camera));app.BeginDrag(app.View(cup.id),pointer);Assert.That(app.IsDragging,Is.True);
                pointer.position=camera.WorldToScreenPoint(new Vector3(2,-2,0));Physics2D.SyncTransforms();app.EndDrag(pointer);
                Assert.That(cup.x,Is.EqualTo(2).Within(.01));Assert.That(cup.y,Is.EqualTo(-2).Within(.01));
            }
            finally {app.CancelDrag();app.worldCamera=camera;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);Object.Destroy(wrongCameraGo);}
            yield return null;
        }
        [UnityTest] public IEnumerator CupShowsEmptyPartialAndFullFillStatesWithoutHoverReset()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.Enter(AreaId.Counter); yield return null;
            app.World.Take("s.cups.large",AreaId.Counter,0,-1,out var cup);
            var view=app.View(cup.id); Assert.That(view,Is.Not.Null);
            var status=view.transform.Find("FillStatus").GetComponent<TMPro.TMP_Text>();
            Assert.That(status.text,Does.Contain("空杯"));
            view.SetHovered(true); var hoverColor=view.body.color;
            view.Refresh(cup); Assert.That(view.body.color,Is.EqualTo(hoverColor),"Periodic world refresh must not erase hover state");
            cup.contents.Add(new Portion{specId="coffee",units=1}); view.Refresh(cup);
            Assert.That(status.text,Does.Contain("有内容"));
            cup.contents[0].units=6; view.Refresh(cup);
            Assert.That(status.text,Does.Contain("容量已满"));
            view.SetHovered(false); yield return null;
        }
        [UnityTest] public IEnumerator ActualSourceDragCanDropAtArbitraryNonSlotCoordinate()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();Assert.That(app,Is.Not.Null);
            Assert.That(GameObject.Find("CoffeeGameplayController"),Is.Null,"Legacy global recipe controller must not be spawned");
            app.Enter(AreaId.Counter);yield return null;
            var source=app.View("s.beans");var record=app.World.Find("s.beans");int stock=record.stock;
            var pointer=Pointer(app,record.x,record.y);app.BeginDrag(source,pointer);Assert.That(app.IsDragging,Is.True);
            pointer.position=app.worldCamera.WorldToScreenPoint(new Vector3(2.137f,-2.613f,0));app.MoveDrag(pointer);Physics2D.SyncTransforms();app.EndDrag(pointer);yield return null;
            Assert.That(app.World.Find("s.beans").stock,Is.EqualTo(stock-1));
            WorldEntity placed=null;foreach(var entity in app.World.State.entities) if(entity.specId=="beans") placed=entity;
            Assert.That(placed,Is.Not.Null);Assert.That(placed.x,Is.EqualTo(2.137f).Within(.01));Assert.That(placed.y,Is.EqualTo(-2.613f).Within(.01));
            Assert.That(app.View(placed.id).art.prompt,Is.Not.Empty);Assert.That(app.World.State.requests.Count,Is.Zero);
        }
        [UnityTest] public IEnumerator CancelAndUiDropDoNotConsumeStock()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
            var source=app.World.Find("s.beans");int stock=source.stock;
            var pointer=Pointer(app,source.x,source.y);app.BeginDrag(app.View(source.id),pointer);app.CancelDrag();yield return null;
            Assert.That(app.World.Find(source.id).stock,Is.EqualTo(stock));
            app.BeginDrag(app.View(source.id),pointer);pointer.position=new Vector2(Screen.width*.5f,Screen.height-5);app.EndDrag(pointer);yield return null;
            Assert.That(app.World.Find(source.id).stock,Is.EqualTo(stock));Assert.That(app.IsDragging,Is.False);
        }
        [UnityTest] public IEnumerator RoomNavigationAndPortalHaveUnblockedRaycasts()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();var hud=Object.FindFirstObjectByType<FreeCafeHud>();
            var portal=Object.FindFirstObjectByType<FreeCafePortal>();Physics2D.SyncTransforms();
            var pointer=new PointerEventData(EventSystem.current) {position=app.worldCamera.WorldToScreenPoint(portal.transform.position)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject.GetComponentInParent<FreeCafePortal>(),Is.EqualTo(portal));
            ExecuteEvents.Execute(portal.gameObject,pointer,ExecuteEvents.pointerClickHandler);Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Counter));
            foreach(var area in new[]{AreaId.TopFloor,AreaId.Basement,AreaId.Cafe,AreaId.Counter})
            {
                Canvas.ForceUpdateCanvases();var button=hud.areaButtons[(int)area];pointer.position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position);hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
                Assert.That(hits.Count,Is.GreaterThan(0),button.name);Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(),Is.EqualTo(button));ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);Assert.That(app.World.State.currentArea,Is.EqualTo(area));yield return null;
            }
        }
        [UnityTest] public IEnumerator QueuedMouseInputStartsBusinessAndNavigatesAllRooms()
        {
            var app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            var hud = Object.FindFirstObjectByType<FreeCafeHud>();
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(module.point.action.enabled, Is.True);
            Assert.That(module.leftClick.action.enabled, Is.True);
            var originalDevices = new List<InputDevice>(InputSystem.devices);
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var mouse = InputSystem.AddDevice<Mouse>("FreeCafeRegressionMouse");
            try
            {
                // Automated tests may run while the editor is unfocused. Restore this setting below.
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                yield return ClickWithQueuedMouse(mouse, hud.startButton);
                Assert.That(app.World.State.open, Is.True, "Must pass through actions, raycast and pointer press/release; not onClick.Invoke");
                foreach (var area in new[] { AreaId.Counter, AreaId.TopFloor, AreaId.Basement, AreaId.Cafe })
                {
                    yield return ClickWithQueuedMouse(mouse, hud.areaButtons[(int)area]);
                    Assert.That(app.World.State.currentArea, Is.EqualTo(area));
                }
            }
            finally
            {
                // Never remove all devices: native mouse/keyboard belong to the editor, not this test.
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
            foreach (var device in originalDevices)
                Assert.That(device.added, Is.True, "Test removed an input device it does not own: " + device);
        }
        private IEnumerator ClickWithQueuedMouse(Mouse mouse, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            Assert.That(position.x, Is.InRange(0, Screen.width), button.name);
            Assert.That(position.y, Is.InRange(0, Screen.height), button.name);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null; yield return null;
        }
        [UnityTest] public IEnumerator CarrySelectionPreservesCoordinatesAcrossFloorsAndPauseCancelsDrag()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);
            app.World.Take("s.cups.large",AreaId.Counter,2.27f,-2.77f,out var cup);app.Select(cup.id);app.CarrySelection=true;
            app.Enter(AreaId.Basement);yield return null;Assert.That(cup.area,Is.EqualTo(AreaId.Basement));Assert.That(cup.x,Is.EqualTo(2.27f));Assert.That(app.View(cup.id).gameObject.activeInHierarchy,Is.True);
            var pointer=Pointer(app,cup.x,cup.y);app.BeginDrag(app.View(cup.id),pointer);Assert.That(app.IsDragging,Is.True);app.TogglePause();Assert.That(app.IsDragging,Is.False);Assert.That(cup.x,Is.EqualTo(2.27f));app.Enter(AreaId.Cafe);Assert.That(app.World.State.currentArea,Is.EqualTo(AreaId.Basement));app.TogglePause();
        }
        private WorldEntity PrepareGrounds(FreeCafeRuntime app)
        {
            Assert.That(app.World.Take("s.beans",AreaId.Counter,0,-2,out var bean).success,Is.True);
            Assert.That(app.World.Interact(bean.id,"m.grinder").success,Is.True);
            for(int i=0;i<3;i++) { Assert.That(app.World.Operate("m.grinder").success,Is.True);if(i<2) app.World.Tick(.5f); }
            return bean;
        }
        [UnityTest] public IEnumerator QueuedMouseDragsGroundsFromGrinderThroughEventSystemIntoCup()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
            var bean=PrepareGrounds(app);var grinder=app.World.Find("m.grinder");
            app.World.Take("s.cups.large",AreaId.Counter,2,-2,out var cup);Physics2D.SyncTransforms();
            var original=app.View(grinder.id).transform.position;
            var previousBackground=InputSystem.settings.backgroundBehavior;var mouse=InputSystem.AddDevice<Mouse>("FreeCafeGrinderDragMouse");
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                Vector2 start=app.worldCamera.WorldToScreenPoint(original);
                Vector2 end=app.worldCamera.WorldToScreenPoint(new Vector3(cup.x,cup.y,0));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=start});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=start}.WithButton(MouseButton.Left));yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=start+new Vector2(30,0)}.WithButton(MouseButton.Left));yield return null;yield return null;
                Assert.That(app.IsDragging,Is.True,"Must enter drag through pointer action and raycast, not a direct BeginDrag call");
                Assert.That(bean.ownerId,Is.EqualTo(grinder.id));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=end}.WithButton(MouseButton.Left));yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=end});yield return null;yield return null;
                Assert.That(app.IsDragging,Is.False);Assert.That(grinder.task,Is.Null);Assert.That(app.World.Find(bean.id),Is.Null);
                Assert.That(cup.contents.Count,Is.EqualTo(1));Assert.That(cup.contents[0].specId,Is.EqualTo("grounds"));
                Assert.That(app.View(grinder.id).transform.position,Is.EqualTo(original));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=previousBackground;
            }
        }
        [UnityTest] public IEnumerator DraggingReadyGrinderTransfersGroundsToBrewerWithoutMovingMachine()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
            var bean=PrepareGrounds(app);var grinder=app.World.Find("m.grinder");var view=app.View(grinder.id);var original=view.transform.position;
            int stock=app.World.Find("s.beans").stock;var pointer=Pointer(app,grinder.x,grinder.y);
            app.BeginDrag(view,pointer);Assert.That(app.IsDragging,Is.True);Assert.That(bean.ownerId,Is.EqualTo(grinder.id));
            Assert.That(view.transform.position,Is.EqualTo(original));
            var brewer=app.World.Find("m.brewer");pointer.position=app.worldCamera.WorldToScreenPoint(new Vector3(brewer.x,brewer.y,0));
            Physics2D.SyncTransforms();app.EndDrag(pointer);yield return null;
            Assert.That(app.IsDragging,Is.False);Assert.That(grinder.task,Is.Null);Assert.That(bean.ownerId,Is.EqualTo(brewer.id));
            Assert.That(brewer.task.inputId,Is.EqualTo(bean.id));Assert.That(view.transform.position,Is.EqualTo(original));
            Assert.That(app.World.Find("s.beans").stock,Is.EqualTo(stock));
        }
        [UnityTest] public IEnumerator OutputDragCanFillCupAndRejectsBusyFullInvalidOrCancelledDrops()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
            var grinder=app.World.Find("m.grinder");var view=app.View(grinder.id);var pointer=Pointer(app,grinder.x,grinder.y);
            app.BeginDrag(view,pointer);Assert.That(app.IsDragging,Is.False);
            app.World.Take("s.beans",AreaId.Counter,0,-2,out var bean);app.World.Interact(bean.id,grinder.id);
            Assert.That(view.label.text,Does.Contain("研磨 0/3"));Assert.That(view.label.text,Does.Not.Contain("就绪"));
            app.BeginDrag(view,pointer);Assert.That(app.IsDragging,Is.False);
            for(int i=0;i<3;i++) { app.World.Operate(grinder.id);if(i<2) app.World.Tick(.5f); }
            var job=grinder.task;int count=app.World.State.entities.Count;
            app.BeginDrag(view,pointer);app.CancelDrag();yield return null;
            Assert.That(grinder.task,Is.SameAs(job));Assert.That(bean.ownerId,Is.EqualTo(grinder.id));Assert.That(app.World.State.entities.Count,Is.EqualTo(count));
            app.BeginDrag(view,Pointer(app,grinder.x,grinder.y));pointer.position=new Vector2(Screen.width*.5f,Screen.height-2);app.EndDrag(pointer);yield return null;
            Assert.That(grinder.task,Is.SameAs(job));Assert.That(bean.ownerId,Is.EqualTo(grinder.id));
            app.World.Take("s.cups.small",AreaId.Counter,2,-2,out var cup);cup.contents.Add(new Portion{specId="milk",units=app.World.Spec(cup.specId).capacity});
            app.View(cup.id).Refresh(cup);Physics2D.SyncTransforms();app.BeginDrag(view,Pointer(app,grinder.x,grinder.y));app.EndDrag(Pointer(app,cup.x,cup.y));yield return null;
            Assert.That(grinder.task,Is.SameAs(job));Assert.That(bean.ownerId,Is.EqualTo(grinder.id));
            cup.contents.Clear();app.View(cup.id).Refresh(cup);Physics2D.SyncTransforms();app.BeginDrag(view,Pointer(app,grinder.x,grinder.y));app.EndDrag(Pointer(app,cup.x,cup.y));yield return null;
            Assert.That(grinder.task,Is.Null);Assert.That(app.World.Find(bean.id),Is.Null);Assert.That(cup.contents.Count,Is.EqualTo(1));Assert.That(cup.contents[0].specId,Is.EqualTo("grounds"));
        }
        [UnityTest] public IEnumerator MachineClickAndRightClickFollowFreeformTransferRules()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);yield return null;
            app.World.Take("s.beans",AreaId.Counter,0,-2,out var bean);app.World.Interact(bean.id,"m.grinder");
            app.Click(app.View("m.grinder"));Assert.That(app.World.Find("m.grinder").task.clicksDone,Is.EqualTo(1));
            app.World.Tick(.5f);app.Click(app.View("m.grinder"));app.World.Tick(.5f);app.Click(app.View("m.grinder"));
            Assert.That(bean.specId,Is.EqualTo("grounds"));Assert.That(app.World.Find("m.grinder").task.phase,Is.EqualTo(MachinePhase.Ready));
            app.World.Take("s.cups.large",AreaId.Counter,2,-2,out var cup);Assert.That(app.World.Interact(cup.id,"m.grinder").success,Is.True);
            Assert.That(cup.contents.Count,Is.EqualTo(1));Assert.That(cup.contents[0].specId,Is.EqualTo("grounds"));
            app.World.Take("s.milk",AreaId.Counter,4,-2,out var milk);Assert.That(app.World.Interact(milk.id,"m.heater").success,Is.True);
            app.CancelProcessing(app.View("m.heater"));Assert.That(app.World.Find("m.heater").task,Is.Null);Assert.That(milk.ownerId,Is.Null);
            yield return null;
        }
        [UnityTest] public IEnumerator IndependentJobsAndSaveRegistrySurviveFloorSwitch()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();app.Enter(AreaId.Counter);
            app.World.Take("s.beans",AreaId.Counter,0,-2,out var bean);app.World.Take("s.milk",AreaId.Counter,2,-2,out var milk);
            app.World.Interact(bean.id,"m.grinder");for(int i=0;i<3;i++){app.World.Operate("m.grinder");if(i<2)app.World.Tick(.5f);}app.World.Interact(milk.id,"m.heater");app.Enter(AreaId.TopFloor);app.World.Tick(6);yield return null;
            Assert.That(app.World.Find("m.grinder").task.phase,Is.EqualTo(MachinePhase.Ready));Assert.That(app.World.Find("m.heater").task.phase,Is.EqualTo(MachinePhase.Ready));
            string json=app.CaptureJson();app.RestoreJson(json);Assert.That(app.World.Find(bean.id).specId,Is.EqualTo("grounds"));Assert.That(app.World.Find(milk.id).specId,Is.EqualTo("hotmilk"));
            Assert.That(GameSaveParticipantRegistry.Capturers.ContainsKey(FreeCafeRuntime.SaveId),Is.True);
            Assert.That(GameSaveParticipantRegistry.Capturers.ContainsKey("world.placements"),Is.False);
        }
        [UnityTest] public IEnumerator WhiteboxesAllCarryArtContracts()
        {
            var app=Object.FindFirstObjectByType<FreeCafeRuntime>();Assert.That(app.artLibrary.entries.Length,Is.EqualTo(38));
            foreach(var entry in app.artLibrary.entries) { Assert.That(entry.whitebox,Is.Not.Null,entry.brief.id);Assert.That(entry.brief.prompt,Is.Not.Empty,entry.brief.id);Assert.That(entry.brief.width,Is.GreaterThan(0)); }
            foreach(var entity in app.World.State.entities) { var view=app.View(entity.id);Assert.That(view.art.assetId,Is.EqualTo(app.World.Spec(entity.specId).artId));Assert.That(view.art.prompt,Is.Not.Empty); }
            var tags=Object.FindObjectsByType<ArtRequirementTag>(FindObjectsInactive.Include,FindObjectsSortMode.None);Assert.That(tags.Length,Is.GreaterThan(35));yield return null;
        }
    }
}
