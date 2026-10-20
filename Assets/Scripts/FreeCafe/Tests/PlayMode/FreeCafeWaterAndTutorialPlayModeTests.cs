using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class FreeCafeWaterAndTutorialPlayModeTests
    {
        private FreeCafeRuntime app;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private readonly List<BaseInputModule> disabledModules = new List<BaseInputModule>();
        [UnitySetUp] public IEnumerator Setup()
        {
            Time.timeScale = 1;
            Spotlight.GameFlow.GameSaveParticipantRegistry.PendingRestore = null;
            if (FreeCafeGMPanel.Instance != null) FreeCafeGMPanel.Instance.SetVisible(false);
            yield return SceneManager.LoadSceneAsync("CoffeeShop"); yield return null; yield return null;
            app = Object.FindFirstObjectByType<FreeCafeRuntime>();
            app.areaManager.panSmoothTime = 0; app.Enter(AreaId.Counter); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            app.WorkstationTutorial.Stop(); app.CancelDrag(); Time.timeScale = 1;
            if (mouse != null)
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                mouse = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            }
            foreach (var module in disabledModules) if (module != null) module.enabled = true;
            disabledModules.Clear();
            yield return null;
        }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/WaterAndWaste"));
            Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
        [UnityTest] public IEnumerator EmptyBeanBagReportsRefillOnPressWithoutWaitingForClickOrDrag()
        {
            var source = app.World.Find("s.beans"); source.stock = 0;
            var view = app.View(source.id);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = app.worldCamera.WorldToScreenPoint(view.transform.position) };
            string before = JsonUtility.ToJson(app.World.State);
            app.ShowHint("before press");
            ExecuteEvents.Execute(view.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(app.Hint, Is.EqualTo("在咖啡谱的补货处补货"));
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(app.Hint, Is.EqualTo("在咖啡谱的补货处补货"));
            Assert.That(app.IsDragging, Is.False);
            Assert.That(GameObject.Find("UncommittedPickup"), Is.Null);
            Assert.That(JsonUtility.ToJson(app.World.State), Is.EqualTo(before));
            view.OnBeginDrag(pointer);
            Assert.That(app.Hint, Is.EqualTo("在咖啡谱的补货处补货"));
            Assert.That(app.IsDragging, Is.False);
        }
        [UnityTest] public IEnumerator WaterUsesItsOwnContainerAndWasteAcceptsTheEnlargedLowerLeftTarget()
        {
            var world = app.World;
            var water = app.View("s.water"); var heater = app.View("m.heater"); var waste = app.View("waste");
            Assert.That(water.transform.position.x, Is.GreaterThan(heater.transform.position.x));

            Assert.That(water.body.sprite.name, Is.EqualTo("default"));
            Assert.That(waste.hitbox.size.x, Is.GreaterThanOrEqualTo(1.9f));
            Assert.That(waste.hitbox.size.y, Is.GreaterThanOrEqualTo(1.8f));
            Assert.That(world.Find("waste").x, Is.LessThan(0)); Assert.That(world.Find("waste").y, Is.LessThan(0));
            float homeX = world.Find("s.water").x;
            yield return Capture("workstation-water-and-waste");
            Physics2D.SyncTransforms();
            app.BeginDrag(water, new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(water.transform.position) });
            Assert.That(app.IsDragging, Is.True);
            Assert.That(GameObject.Find("UncommittedPickup"), Is.Null, "Water is the actual reusable container, not an ingredient ghost");
            Assert.That(world.PourLiquid("s.water", "m.heater", 200, 1).success, Is.True);
            app.CancelDrag(); yield return null;
            Assert.That(world.CanPour(world.Find("s.water")), Is.True);
            Assert.That(world.Find("s.water").x, Is.EqualTo(homeX).Within(.001f));
            Assert.That(world.Take("s.beans", AreaId.Counter, -7, -4, out var beans).success, Is.True);
            app.DebugRefreshViews(); yield return null;
            var beanView = app.View(beans.id);
            app.BeginDrag(beanView, new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(beanView.transform.position) });
            Vector3 edge = waste.transform.TransformPoint(new Vector3(.8f, .5f));
            var pointer = new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(edge) };
            Physics2D.SyncTransforms(); app.MoveDrag(pointer); app.EndDrag(pointer); yield return null;
            Assert.That(world.Find(beans.id), Is.Null, "Dropping near the expanded rim must discard the ingredient");
        }
        [UnityTest] public IEnumerator AuthoredPresentationAndSupplyHomesSurviveBindingRefreshAndOldSaveRestore()
        {
            foreach (string id in new[] { "s.water", "waste" })
            {
                var entity = app.World.Find(id);
                var prefab = app.prefabLibrary.Require(entity.specId);
                var authored = Object.Instantiate(prefab, app.areaRoots[(int)entity.area]);
                authored.ConfigureAuthoring(app.World.Spec(entity.specId), app.artLibrary, id, entity.x, entity.y);
                var artScale = new Vector3(.345f, .381f, 1);
                var artPosition = new Vector3(.46f, .69f, 0);
                var boxSize = new Vector2(2.3f, 2.1f);
                var boxOffset = new Vector2(.4f, .3f);
                authored.body.transform.localScale = artScale;
                authored.body.transform.localPosition = artPosition;
                authored.hitbox.size = boxSize;
                authored.hitbox.offset = boxOffset;
                var captionPosition = new Vector3(.2f, -1.5f, 0);
                authored.label.transform.localPosition = captionPosition;
                authored.Bind(app, entity, true);
                authored.Refresh(entity);
                Assert.That(Vector3.Distance(authored.body.transform.localScale, artScale), Is.LessThan(.0001f));
                Assert.That(authored.body.transform.localPosition, Is.EqualTo(artPosition));
                Assert.That(authored.hitbox.size, Is.EqualTo(boxSize));
                Assert.That(authored.hitbox.offset, Is.EqualTo(boxOffset));
                Assert.That(authored.label.transform.localPosition, Is.EqualTo(captionPosition));
                Object.Destroy(authored.gameObject);
            }
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(app.CaptureJson());
            foreach (string id in new[] { "s.water", "waste" })
            {
                var entry = snapshot.entities.Find(e => e.id == id);
                entry.area = AreaId.Cafe; entry.x = 0; entry.y = 0;
            }
            app.RestoreJson(JsonUtility.ToJson(snapshot));
            yield return null;
            foreach (string id in new[] { "s.water", "waste" })
            {
                var initial = System.Array.Find(app.World.Catalog.layout, e => e.id == id);
                var entity = app.World.Find(id);
                Assert.That(entity.area, Is.EqualTo(initial.area));
                Assert.That(entity.x, Is.EqualTo(initial.x).Within(.0001f));
                Assert.That(entity.y, Is.EqualTo(initial.y).Within(.0001f));
            }
        }
        [UnityTest] public IEnumerator BusinessSignKeepsScenePositionAcrossBindingRefreshAndOldSaveRestore()
        {
            var entity = app.World.Find("business.start");
            var authored = Object.Instantiate(app.prefabLibrary.Require(entity.specId), app.areaRoots[(int)entity.area]);
            authored.ConfigureAuthoring(app.World.Spec(entity.specId), app.artLibrary, entity.id, entity.x, entity.y);
            var editedPosition = new Vector3(entity.x + 1.2f, entity.y + .8f, -.15f);
            authored.transform.localPosition = editedPosition;
            try
            {
                Assert.That(authored.preserveAuthoredPosition, Is.True);
                authored.Bind(app, entity, true);
                authored.Refresh(entity);
                yield return null;
                Assert.That(authored.transform.localPosition, Is.EqualTo(editedPosition));
            }
            finally { Object.Destroy(authored.gameObject); }

            var sign = app.View(entity.id);
            var scenePosition = sign.transform.localPosition;
            var snapshot = JsonUtility.FromJson<CafeSnapshot>(app.CaptureJson());
            var savedSign = snapshot.entities.Find(e => e.id == entity.id);
            savedSign.x += 2f;
            savedSign.y += 1f;
            app.RestoreJson(JsonUtility.ToJson(snapshot));
            yield return null;
            yield return null;
            Assert.That(sign.transform.localPosition, Is.EqualTo(scenePosition), "Saved coordinates must not replace the edited sign placement");
        }
        [UnityTest] public IEnumerator RestartButtonIsAlwaysAvailableAndRestoresThePracticeWhileDragging()
        {
            var guide = app.WorkstationTutorial; var world = app.World;
            Assert.That(guide.Begin(false), Is.True); yield return null;
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(panel.retry.activeInHierarchy, Is.True);
            Assert.That(panel.retryButton.interactable, Is.True);
            int money = world.State.money, stock = world.Find("s.beans").stock;
            Assert.That(world.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success, Is.True); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Grind));
            panel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            Assert.That(world.Find("s.beans").stock, Is.EqualTo(stock));
            Assert.That(world.Find("m.grinder").contents, Is.Empty);
            Assert.That(world.State.money, Is.EqualTo(money));
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guide, new object[] { FreeCafeWorkstationTutorial.Stage.Water });
            var water = app.View("s.water"); Physics2D.SyncTransforms();
            app.BeginDrag(water, new PointerEventData(EventSystem.current) { position = app.worldCamera.WorldToScreenPoint(water.transform.position) });
            Assert.That(app.IsDragging, Is.True);
            world.PourLiquid("s.water", "m.heater", 50, 1);
            panel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(app.IsDragging, Is.False);
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
            Assert.That(world.LiquidVolume(world.Find("m.heater")), Is.Zero);
            Assert.That(panel.retry.activeInHierarchy, Is.True);
            yield return Capture("tutorial-persistent-restart");
        }
        [UnityTest] public IEnumerator ExtractionTutorialShowsBothGaugesBeforeTheFirstPour()
        {
            var guide = app.WorkstationTutorial;
            Assert.That(guide.Begin(true), Is.True);
            Assert.That(app.World.TakeAndInteract("s.beans", "m.grinder", AreaId.Counter, 0, 0).success, Is.True);
            Assert.That(app.World.OperateProduction("m.grinder").success, Is.True); app.World.Tick(20);
            var output = app.World.DragOutputPreview("m.grinder");
            Assert.That(app.World.TakeMachineOutput("m.grinder", output.id, "v.extractor.filter", AreaId.Counter, 0, 0).success, Is.True);
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.Extract);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(guide.HighlightsExtractionGauges("v.extractor"), Is.True);
            var panels = app.GetComponent<FreeCafeContainerPanels>();
            var focuses = panels.GetComponentsInChildren<UnityEngine.UI.Image>();
            int shown = 0;
            foreach (var image in focuses) if (image.name == "TutorialFocus" && image.gameObject.activeInHierarchy) shown++;
            Assert.That(shown, Is.EqualTo(2), "Both judgment bars should be highlighted before injecting water");
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            StringAssert.Contains("左侧", panel.instruction.text);
            StringAssert.Contains("右侧", panel.instruction.text);
            guide.Stop(); yield return null;
            Assert.That(guide.HighlightsExtractionGauges("v.extractor"), Is.False);
        }

        [UnityTest] public IEnumerator TutorialWaterStepMeasuresTheReceiverRatherThanEmptyingUnlimitedWater()
        {
            var guide = app.WorkstationTutorial; var world = app.World;
            Assert.That(guide.Begin(true), Is.True);
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guide, new object[] { FreeCafeWorkstationTutorial.Stage.Water });
            Assert.That(guide.AllowPour("s.water", "m.heater"), Is.True);
            Assert.That(guide.AllowPour("s.water", "waste"), Is.False);
            // The following domain commands model measured water without holding a hardware mouse button.
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guide, new object[] { FreeCafeWorkstationTutorial.Stage.Water });
            world.PourLiquid("s.water", "m.heater", 150, 1); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Water));
            yield return Capture("tutorial-measured-water");
            Assert.That(guide.LimitPourAmount("s.water", 100), Is.EqualTo(50).Within(.001f));
            world.PourLiquid("s.water", "m.heater", 50, 1); yield return null; yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Heat));
            Assert.That(world.CanPour(world.Find("s.water")), Is.True);
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(panel.retry.activeInHierarchy, Is.True);
            panel.retryButton.onClick.Invoke(); yield return null;
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Beans));
        }

        private void UseTutorialMouse()
        {
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>("TutorialPourRegressionMouse");
            // Dispatch the drag directly, while retaining the real scene and its raycasters.
            foreach (var module in EventSystem.current.GetComponents<BaseInputModule>())
                if (module.enabled) { disabledModules.Add(module); module.enabled = false; }
        }
        private void MouseAt(Vector2 position, bool left, bool right = false)
        {
            var state = new MouseState { position = position };
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }
        private void SetTutorialStage(FreeCafeWorkstationTutorial.Stage next) =>
            typeof(FreeCafeWorkstationTutorial).GetMethod("SetStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(app.WorkstationTutorial, new object[] { next });
        private PointerEventData PointerAt(string id, float above = 0)
        {
            var entity = app.World.Find(id);
            return new PointerEventData(EventSystem.current)
            {
                position = app.worldCamera.WorldToScreenPoint(app.AreaToWorld(AreaId.Counter, entity.x, entity.y + above))
            };
        }
        private IEnumerator BeginTutorialVesselDrag(string source)
        {
            var pointer = PointerAt(source);
            MouseAt(pointer.position, true); yield return null;
            Physics2D.SyncTransforms(); app.BeginDrag(app.View(source), pointer);
            Assert.That(app.IsDragging, Is.True);
            yield return null;
        }
        private IEnumerator AlignTutorialVessel(string source, string target)
        {
            var pointer = PointerAt(target, .8f);
            MouseAt(pointer.position, true);
            app.MoveDrag(pointer);
            // Let the authored vessel presentation settle before locking its visible pose.
            yield return new WaitForSecondsRealtime(.4f);
            Physics2D.SyncTransforms(); app.MoveDrag(pointer); yield return null;
            Assert.That(app.CurrentPourTargetId, Is.EqualTo(target), source + " should be over the actual receiver");
        }
        [UnityTest] public IEnumerator TutorialExplainsPurposeAndNamedControlsBeforeVisualHints()
        {
            Assert.That(app.WorkstationTutorial.Begin(false), Is.True);
            yield return null;
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            Assert.That(panel.heading.text, Does.Contain("放入咖啡豆"));
            Assert.That(panel.instruction.text, Does.Contain("磨成粉").And.Contain("按住鼠标左键").And.Contain("研磨机").And.Contain("松开"));
            Assert.That(panel.gestureText.text, Does.Contain("咖啡豆袋").And.Contain("研磨机"));
            Assert.That(app.WorkstationTutorial.AllowDrag("s.water"), Is.False);
            yield return null;
            Assert.That(panel.errorText.text, Does.Contain("咖啡豆袋"));
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.Grounds); yield return null;
            Assert.That(panel.instruction.text, Does.Contain("咖啡粉").And.Contain("煮咖啡").And.Contain("研磨机抽屉"));
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.Water); yield return null;
            Assert.That(panel.instruction.text, Does.Contain("清水").And.Contain("加热").And.Contain("第 1 步").And.Contain("先别松开左键"));
            Assert.That(panel.instruction.text, Does.Not.Contain("高亮").And.Not.Contain("光效"));
            Assert.That(panel.rotationArrow.activeSelf, Is.False);
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.Heat); yield return null;
            Assert.That(panel.instruction.text, Does.Contain("煮成黑咖啡").And.Contain("不需要连续点击"));
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.TakeCup); yield return null;
            Assert.That(panel.instruction.text, Does.Contain("接咖啡").And.Contain("空杯").And.Contain("桌面空处"));
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.DeliverCup);
            // Render this hint directly: an unprepared fixture has no cup and would auto-advance.
            typeof(FreeCafeWorkstationTutorial).GetMethod("DrawGuide", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(app.WorkstationTutorial, null);
            Assert.That(panel.instruction.text, Does.Contain("交给客人").And.Contain("盘子").And.Contain("松开左键"));
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.SubmitCup); yield return null;
            Assert.That(panel.instruction.text, Does.Contain("按铃").And.Contain("完成交付"));
        }
        [UnityTest] public IEnumerator EveryBlackCoffeeAndEssenceStageKeepsGestureGuidanceAndAppendsErrors()
        {
            var guide = app.WorkstationTutorial;
            var drawGuide = typeof(FreeCafeWorkstationTutorial).GetMethod("DrawGuide", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(drawGuide, Is.Not.Null);
            foreach (bool essence in new[] { false, true })
            {
                Assert.That(guide.Begin(essence), Is.True);
                yield return null;
                var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
                Assert.That(panel, Is.Not.Null);
                foreach (FreeCafeWorkstationTutorial.Stage stage in System.Enum.GetValues(typeof(FreeCafeWorkstationTutorial.Stage)))
                {
                    string context = (essence ? "Essence" : "Black coffee") + " / " + stage;
                    SetTutorialStage(stage);
                    yield return null;
                    // An unprepared fixture can auto-advance Heating, Drain or DeliverCup.
                    // Render the exact requested stage so those wait/completion hints are covered too.
                    SetTutorialStage(stage);
                    drawGuide.Invoke(guide, null);
                    Assert.That(panel.gesture.gameObject.activeInHierarchy, Is.True, context);
                    Assert.That(string.IsNullOrWhiteSpace(panel.gestureText.text), Is.False, context);
                    string operation = panel.gestureText.text;
                    if (stage == FreeCafeWorkstationTutorial.Stage.Extract)
                    {
                        Assert.That(operation, Does.Contain("进度条").And.Contain("流速"), context);
                    }
                    const string error = "回归测试：操作错误，请重试";
                    guide.ReportError(error);
                    drawGuide.Invoke(guide, null);
                    Assert.That(panel.gesture.gameObject.activeInHierarchy, Is.True, context + " after error");
                    Assert.That(panel.gestureText.text, Does.Contain(error).And.Contain(operation),
                        context + " must append the error without replacing the current operation");
                }
                guide.Stop();
                yield return null;
            }
        }
        [UnityTest] public IEnumerator MisplacedRightClickCanReturnWaterAndRetryThroughTheHeatStep()
        {
            UseTutorialMouse();
            var guide = app.WorkstationTutorial;
            Assert.That(guide.Begin(false), Is.True);
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.Water);
            yield return BeginTutorialVesselDrag("s.water");
            var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
            StringAssert.Contains("第 1 步", panel.instruction.text);
            Assert.That(panel.rotationArrow.activeSelf, Is.False);
            var home = PointerAt("s.water");
            MouseAt(home.position, true, true); yield return null; yield return null;
            Assert.That(app.LockedPourTargetId, Is.Null, "A misplaced right-click must not lock or freeze an invalid receiver");
            MouseAt(home.position, false); yield return null; yield return null;
            Assert.That(app.IsDragging, Is.False);
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Water));
            var restoredWater = app.World.Find("s.water");
            yield return null; yield return null; yield return null;
            Assert.That(app.World.Find("s.water"), Is.SameAs(restoredWater), "The failed attempt must restore once, not replace entities every frame");

            yield return BeginTutorialVesselDrag("s.water");
            yield return AlignTutorialVessel("s.water", "m.heater");
            StringAssert.Contains("第 2 步", panel.instruction.text);
            Assert.That(panel.rotationArrow.activeSelf, Is.True);
            var target = PointerAt("m.heater", .8f);
            MouseAt(target.position, true, true); yield return null;
            Assert.That(app.LockedPourTargetId, Is.EqualTo("m.heater"));
            MouseAt(target.position + Vector2.up * 160, false, true);
            float deadline = Time.realtimeSinceStartup + 12;
            while (guide.CurrentStage == FreeCafeWorkstationTutorial.Stage.Water && Time.realtimeSinceStartup < deadline)
                yield return null;
            MouseAt(target.position, false);
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Heat), "Retry must accept real right-button pouring and advance");
            Assert.That(app.IsDragging, Is.False);
            Assert.That(app.World.LiquidVolume(app.World.Find("m.heater")), Is.EqualTo(app.World.Catalog.production.portionMl).Within(.01f));
            yield return Capture("tutorial-water-retry-completed");
        }
        [UnityTest] public IEnumerator EarlyRightReleaseRestoresWaterOnlyOnceAndLeavesItDraggable()
        {
            UseTutorialMouse();
            var guide = app.WorkstationTutorial;
            Assert.That(guide.BeginFirstCafeTutorial(), Is.True);
            SetTutorialStage(FreeCafeWorkstationTutorial.Stage.Water);
            yield return BeginTutorialVesselDrag("s.water");
            yield return AlignTutorialVessel("s.water", "m.heater");
            var target = PointerAt("m.heater", .8f);
            MouseAt(target.position, true, true); yield return null;
            Assert.That(app.LockedPourTargetId, Is.EqualTo("m.heater"));
            Assert.That(app.World.PourLiquid("s.water", "m.heater", 50, 1).success, Is.True);
            MouseAt(target.position, false); yield return null; yield return null;
            Assert.That(app.World.LiquidVolume(app.World.Find("m.heater")), Is.Zero, "A partial pour should reset the current water checkpoint");
            var restoredWater = app.World.Find("s.water");
            Assert.That(app.IsDragging, Is.False);
            Assert.That(guide.CurrentStage, Is.EqualTo(FreeCafeWorkstationTutorial.Stage.Water));
            yield return null; yield return null; yield return null;
            Assert.That(app.World.Find("s.water"), Is.SameAs(restoredWater));
            yield return BeginTutorialVesselDrag("s.water");
            Assert.That(app.IsDragging, Is.True, "Early right release must not cancel every subsequent pickup");
            yield return Capture("tutorial-water-left-drag-prompt");
        }
        [UnityTest] public IEnumerator BlackCoffeeAndEssencePourHintsSwitchOnlyAfterAlignmentAndResetOnCancel()
        {
            UseTutorialMouse();
            var guide = app.WorkstationTutorial;
            foreach (var scenario in new[] { (false, FreeCafeWorkstationTutorial.Stage.Pour), (true, FreeCafeWorkstationTutorial.Stage.Extract), (true, FreeCafeWorkstationTutorial.Stage.Pour) })
            {
                bool essence = scenario.Item1;
                var step = scenario.Item2;
                Assert.That(guide.Begin(essence), Is.True);
                string target;
                if (step == FreeCafeWorkstationTutorial.Stage.Extract)
                    target = app.World.State.entities.Find(e => e.specId == "container.extractor.filter").id;
                else
                {
                    if (!essence) Assert.That(app.World.SelectServingCup("s.cups.large").success, Is.True);
                    target = app.World.ServingCup.id;
                    typeof(FreeCafeWorkstationTutorial).GetField("cup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(guide, target);
                }
                SetTutorialStage(step);
                var panel = Object.FindFirstObjectByType<FreeCafeTutorialPanel>();
                yield return null;
                StringAssert.Contains("第 1 步", panel.instruction.text);
                Assert.That(panel.rotationArrow.activeSelf, Is.False);
                string source = step == FreeCafeWorkstationTutorial.Stage.Pour && essence
                    ? app.World.State.entities.Find(e => app.World.IsPrecisionExtractor(e)).id : "m.heater";
                yield return BeginTutorialVesselDrag(source);
                StringAssert.Contains("第 1 步", panel.instruction.text, "Picking up alone must not show the right-button prompt");
                yield return AlignTutorialVessel(source, target);
                StringAssert.Contains("第 2 步", panel.instruction.text);
                Assert.That(panel.rightButton.color, Is.EqualTo(panel.activeButtonColor));
                Assert.That(panel.rotationArrow.activeSelf, Is.True);
                var away = PointerAt(source);
                MouseAt(away.position, true); app.MoveDrag(away); yield return null; yield return null;
                StringAssert.Contains("第 1 步", panel.instruction.text, "Moving away must return to positioning guidance");
                Assert.That(panel.rotationArrow.activeSelf, Is.False);
                yield return AlignTutorialVessel(source, target);
                yield return Capture("tutorial-" + (essence ? "essence-" : "black-") + step + "-right-prompt");
                app.CancelDrag(); MouseAt(Vector2.zero, false); yield return null; yield return null;
                StringAssert.Contains("第 1 步", panel.instruction.text);
                Assert.That(panel.rotationArrow.activeSelf, Is.False);
                guide.Stop();
            }
        }
    }
}
