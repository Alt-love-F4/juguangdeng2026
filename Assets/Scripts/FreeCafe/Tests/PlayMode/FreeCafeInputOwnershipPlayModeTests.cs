using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spotlight.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeInputOwnershipPlayModeTests
    {
        private FreeCafeRuntime app;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
        private InputSettings.BackgroundBehavior oldBehavior;
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach(var panel in Object.FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None)) panel.Cancel();
            Time.timeScale=1; GameSaveParticipantRegistry.PendingRestore=null;
            oldEditorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            oldBehavior=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse=InputSystem.AddDevice<Mouse>("OwnershipMouse");
            keyboard=InputSystem.AddDevice<Keyboard>("OwnershipKeyboard");
            InputSystem.QueueStateEvent(mouse,new MouseState());
            yield return SceneManager.LoadSceneAsync("CoffeeShop",LoadSceneMode.Single);
            yield return null; yield return null;
            app=Object.FindFirstObjectByType<FreeCafeRuntime>();
            if(FreeCafeGMPanel.Instance!=null) FreeCafeGMPanel.Instance.SetVisible(false);
            app.areaManager.panSmoothTime=0; app.World.EnterProduction(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(app!=null) app.CancelDrag();
            if(mouse!=null && mouse.added) InputSystem.RemoveDevice(mouse);
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorBehavior;
            InputSystem.settings.backgroundBehavior=oldBehavior; Time.timeScale=1; yield return null;
        }
        private WorldEntity Cup()
        {
            Assert.That(app.World.Take("s.cups.large",AreaId.Counter,0,-2,out var cup).success,Is.True);
            return cup;
        }
        private PointerEventData Pointer(Vector3 world) => new PointerEventData(EventSystem.current)
        { position=app.worldCamera.WorldToScreenPoint(world),button=PointerEventData.InputButton.Left,pointerId=23 };
        private void MouseAt(Vector2 screen,bool held)
        {
            var state=new MouseState {position=screen}; if(held) state=state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse,state);
        }
        [UnityTest] public IEnumerator OverlappingSpritesPickAndDragTheVisibleFrontCupAfterReturn()
        {
            var returned=Cup();var returnView=app.View(returned.id);
            app.BeginDrag(returnView,Pointer(returnView.transform.position));
            app.EndDrag(Pointer(app.View("s.cups.large").transform.position));
            Assert.That(app.World.Find(returned.id),Is.Null);
            var back=Cup();var front=Cup();var backView=app.View(back.id);var frontView=app.View(front.id);
            backView.body.sortingOrder=500;frontView.body.sortingOrder=700;
            var pointer=Pointer(frontView.transform.position);var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits.Count,Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<FreeEntityView>(),Is.SameAs(frontView),
                "Root collider hit order must follow its child sprite, not creation order");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return null;
            var start=(Vector2)app.worldCamera.WorldToScreenPoint(frontView.transform.position);
            MouseAt(start,false);yield return null;
            MouseAt(start,true);yield return null;
            MouseAt(start+new Vector2(45,0),true);yield return null;yield return null;
            Assert.That(app.IsDragging,Is.True);
            Assert.That(app.SelectedId,Is.EqualTo(front.id));
            var end=(Vector2)app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter,4,-3));
            MouseAt(end,true);yield return null;yield return null;
            MouseAt(end,false);yield return null;yield return null;
            Assert.That(app.IsDragging,Is.False);
            Assert.That(front.x,Is.EqualTo(4).Within(.01));Assert.That(front.y,Is.EqualTo(-3).Within(.01));
            Assert.That(back.x,Is.EqualTo(0));Assert.That(back.y,Is.EqualTo(-2));
        }
        [UnityTest] public IEnumerator OtherViewAndOtherPointerCannotMoveOrEndAnOwnedDrag()
        {
            var a=Cup();var b=Cup();var av=app.View(a.id);var bv=app.View(b.id);
            var start=Pointer(av.transform.position);app.BeginDrag(av,start);
            Assert.That(app.IsDragging,Is.True);var target=av.DragPosition;
            var stray=Pointer(app.AreaToWorld(AreaId.Counter,4,-3));
            bv.OnDrag(stray);bv.OnEndDrag(stray);
            Assert.That(app.IsDragging,Is.True);Assert.That(av.DragPosition,Is.EqualTo(target));
            stray.pointerId=24;av.OnDrag(stray);av.OnEndDrag(stray);
            Assert.That(app.IsDragging,Is.True);Assert.That(av.DragPosition,Is.EqualTo(target));
            stray.pointerId=23;av.OnDrag(stray);
            Assert.That(Vector3.Distance(av.DragPosition,target),Is.GreaterThan(1));
            app.CancelDrag();yield return null;
        }
        [UnityTest] public IEnumerator DuplicateEntityIdViewCannotStartAnInteraction()
        {
            var cup=Cup();var view=app.View(cup.id);
            var duplicate=Object.Instantiate(app.prefabLibrary.Require(cup.specId),view.transform.parent);
            duplicate.Bind(app,cup);
            Assert.That(duplicate.CanInteract,Is.False);
            app.BeginDrag(duplicate,Pointer(duplicate.transform.position));
            Assert.That(app.IsDragging,Is.False);
            app.Click(duplicate);Assert.That(app.SelectedId,Is.Not.EqualTo(cup.id));
            Object.Destroy(duplicate.gameObject);yield return null;
        }
        [UnityTest] public IEnumerator RetiredReturnGhostCannotReactivateOrControlANewCup()
        {
            var cup=Cup();var ghost=app.View(cup.id);var stack=app.View("s.cups.large");
            app.BeginDrag(ghost,Pointer(ghost.transform.position));app.EndDrag(Pointer(stack.transform.position));
            Assert.That(app.World.Find(cup.id),Is.Null);Assert.That(ghost.hitbox.enabled,Is.False);
            ghost.Refresh(cup);Assert.That(ghost.hitbox.enabled,Is.False);Assert.That(ghost.CanInteract,Is.False);
            var next=Cup();var current=app.View(next.id);app.BeginDrag(current,Pointer(current.transform.position));
            Assert.That(app.IsDragging,Is.True);var target=current.DragPosition;
            var e=Pointer(stack.transform.position);ghost.OnDrag(e);ghost.OnEndDrag(e);
            Assert.That(app.IsDragging,Is.True);Assert.That(current.DragPosition,Is.EqualTo(target));
            app.CancelDrag();yield return null;
        }
        [UnityTest] public IEnumerator RemovingTheDragOwnerClearsOwnershipBeforeAnotherPickup()
        {
            var cup=Cup();var view=app.View(cup.id);app.BeginDrag(view,Pointer(view.transform.position));
            Assert.That(app.IsDragging,Is.True);
            Assert.That(app.World.Interact(cup.id,"s.cups.large").success,Is.True);
            Assert.That(app.IsDragging,Is.False);
            var next=Cup();app.BeginDrag(app.View(next.id),Pointer(app.View(next.id).transform.position));
            Assert.That(app.IsDragging,Is.True);Assert.That(app.SelectedId,Is.EqualTo(next.id));
            view.OnEndDrag(Pointer(app.View("s.cups.large").transform.position));
            Assert.That(app.IsDragging,Is.True);app.CancelDrag();yield return null;
        }
        [UnityTest] public IEnumerator PickupSourceOwnsItsPreviewCallbacksRatherThanThePreview()
        {
            var source=app.View("s.beans");var e=Pointer(source.transform.position);
            app.BeginDrag(source,e);Assert.That(app.IsDragging,Is.True);
            var preview=GameObject.Find("UncommittedPickup").GetComponent<FreeEntityView>();
            var before=preview.DragPosition;var moved=Pointer(app.AreaToWorld(AreaId.Counter,4,-3));
            preview.OnDrag(moved);Assert.That(preview.DragPosition,Is.EqualTo(before));
            source.OnDrag(moved);Assert.That(Vector3.Distance(preview.DragPosition,before),Is.GreaterThan(1));
            source.OnEndDrag(Pointer(source.transform.position));Assert.That(app.IsDragging,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator DomainRemovalDisablesColliderBeforeDeferredDestroy()
        {
            var cup=Cup();var view=app.View(cup.id);
            Assert.That(app.World.Interact(cup.id,"s.cups.large").success,Is.True);
            Assert.That(view,Is.Not.Null);Assert.That(view.hitbox.enabled,Is.False);
            Assert.That(view.CanInteract,Is.False);Assert.That(app.View(cup.id),Is.Null);
            yield return null;
        }
    }
}
