using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>Guided workstation practice shared by GM tools and the first-cafe tutorial.</summary>
    public sealed class FreeCafeWorkstationTutorial : MonoBehaviour
    {
        public enum Stage { Beans, Grind, Grinding, Grounds, Water, Heat, Heating, Extract, Drain, TakeCup, Pour, DeliverCup, Complete, SubmitCup }
        [Serializable] private sealed class CounterState { public List<WorldEntity> entities = new List<WorldEntity>(); public string cup; public bool automatic; }
        private FreeCafeRuntime app;
        private string original, checkpoint, beans, grinder, heater, extractor, filter, water, cup, cupSource, delivery;
        private readonly HashSet<string> existingCups = new HashSet<string>();
        private bool rightStarted;
        private bool essence, wasDragging, restoring;
        private bool restoreOnStop = true;
        private bool firstCafeTutorial;
        private bool originalAutomatic;
        private bool refreshOnEnable;
        private Stage stage;
        private string error;
        private float errorUntil;
        private Canvas canvas;
        private Image shade;
        private TMP_Text instruction, heading, errorText, gestureText;
        private RectTransform gesture;
        private FreeCafeTutorialPanel panel;
        private readonly List<Image> trail = new List<Image>();
        private GameObject retry;
        private FreeCafePortal entryPortal;
        private FreeCafeDeliveryBell deliveryBell;
        private Collider2D entryHitbox;
        private bool showingWorkstationEntry;
        public bool IsRunning { get; private set; }
        public Stage CurrentStage => stage;
        public bool IsFirstCafeTutorial => IsRunning && firstCafeTutorial;
        public bool CanSubmitCup => !IsRunning || stage == Stage.SubmitCup;
        public ActionResult RingPracticeBell()
        {
            if (!IsRunning || firstCafeTutorial || stage != Stage.SubmitCup)
                return ActionResult.No("先完成咖啡并放到案板上的盘子里，再按铃");
            if (W.ServingCup == null || W.ServingCup.id != cup)
                return ActionResult.No("盘子里没有本次练习的咖啡，请重新放好杯子");
            SetStage(Stage.Complete);
            return ActionResult.Yes("按铃完成！操作练习结束，本次不结算正式订单");
        }
        private CafeWorld W => app.World;
        public void Initialize(FreeCafeRuntime owner)
        {
            app = owner;
            foreach (var portal in app.areaRoots[(int)AreaId.Cafe].GetComponentsInChildren<FreeCafePortal>(true))
            {
                if (portal.runtime != app || portal.destination != AreaId.Counter) continue;
                entryPortal = portal;
                entryHitbox = portal.GetComponent<Collider2D>();
                break;
            }
        }

        private string FindSpec(string spec) => W.State.entities.Find(e => e.area == AreaId.Counter && e.specId == spec)?.id;
        private string FindSource(string spec) => W.State.entities.Find(e => e.area == AreaId.Counter && W.Spec(e.specId).Has(Capability.Source) && W.Spec(e.specId).sourceItemId == spec)?.id;
        public bool Begin(bool extraction)
        {
            if (app == null || app.CafeTutorialActive) return false;
            return BeginGuidance(extraction, false);
        }

        public bool BeginFirstCafeTutorial() => BeginGuidance(false, true);

        private bool BeginGuidance(bool extraction, bool firstCafe)
        {
            if (app == null || !W.ProductionEnabled || W.State.currentArea != AreaId.Counter
                || (firstCafe ? app.TutorialInputBlocked : app.InputBlocked) || app.IsDragging) return false;
            if (IsRunning) Stop();
            beans = FindSource("beans"); water = FindSource("water"); grinder = FindSpec("machine.grinder"); heater = FindSpec("machine.heater");
            extractor = W.State.entities.Find(e => e.area == AreaId.Counter && W.IsPrecisionExtractor(e))?.id;
            filter = FindSpec("container.extractor.filter");
            cupSource = FindSource("cup.large") ?? FindSource("cup.small");
            delivery = W.State.entities.Find(e => e.area == AreaId.Counter && W.Spec(e.specId).Has(Capability.Delivery))?.id;
            if (beans == null || water == null || grinder == null || heater == null || cupSource == null || delivery == null || extraction && (extractor == null || filter == null)) return false;
            var deliveryView = app.View(delivery);
            deliveryBell = deliveryView != null ? deliveryView.GetComponentInChildren<FreeCafeDeliveryBell>(true) : null;
            if (deliveryBell == null) return false;
            original = Capture(); essence = extraction; restoreOnStop = true;
            firstCafeTutorial = firstCafe; originalAutomatic = W.State.automaticMachines;
            W.State.automaticMachines = false;
            // Only practice equipment is reset; the original counter is restored on exit.
            foreach (string id in new[] { grinder, heater, extractor, filter })
            {
                var e = W.Find(id); if (e == null) continue;
                e.contents.Clear(); e.task = null; e.production = new ProductionEntityState();
            }
            W.State.production.activeCupId = null;
            if (!firstCafe)
                foreach (string id in new[] { beans, water, cupSource }) W.Find(id).stock = Math.Max(10, W.Find(id).stock);
            cup = null;
            if (essence)
            {
                W.SelectServingCup(cupSource); cup = W.ServingCup?.id;
                if (cup == null) { Restore(original); return false; }
            }
            if (essence) W.AttachExtractionFilter(filter, extractor);
            showingWorkstationEntry = false;
            IsRunning = true;
            if (canvas == null) BuildUI();
            panel.exitButton.gameObject.SetActive(!firstCafeTutorial);
            canvas.gameObject.SetActive(true);
            SetStage(Stage.Beans);
            app.DebugRefreshViews();
            return true;
        }
        private string Capture()
        {
            var state = new CounterState { cup = W.State.production.activeCupId, automatic = W.State.automaticMachines };
            foreach (var e in W.State.entities) if (e.area == AreaId.Counter) state.entities.Add(e);
            return JsonUtility.ToJson(state);
        }
        private void Restore(string json, bool refreshViews = true)
        {
            restoring = true;
            if (refreshViews) app.CancelDrag();
            var state = JsonUtility.FromJson<CounterState>(json);
            W.State.entities.RemoveAll(e => e.area == AreaId.Counter);
            W.State.entities.AddRange(state.entities);
            W.State.production.activeCupId = state.cup;
            W.State.automaticMachines = state.automatic;
            if (refreshViews) app.DebugRefreshViews();
            else refreshOnEnable = true;
            wasDragging = false; restoring = false;
        }
        public void Stop() => Stop(true);

        private void Stop(bool refreshViews)
        {
            if (!IsRunning) { HideWorkstationEntry(); return; }
            IsRunning = false;
            if (restoreOnStop) Restore(original, refreshViews);
            else
            {
                W.State.automaticMachines = originalAutomatic;
                if (refreshViews) app.DebugRefreshViews();
                else refreshOnEnable = true;
            }
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        public void Finish(bool keepResult)
        {
            restoreOnStop = !keepResult;
            Stop();
        }
        private void OnEnable()
        {
            if (!refreshOnEnable || app == null) return;
            refreshOnEnable = false;
            app.DebugRefreshViews();
        }
        private void OnDisable() { if (app != null && app.World != null) Stop(false); }
        private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
        public void ReportError(string message) { error = message; errorUntil = Time.unscaledTime + 5; }
        private void SetStage(Stage next)
        {
            stage = next; wasDragging = false; rightStarted = false; error = null;
            if (next == Stage.TakeCup)
            {
                existingCups.Clear();
                foreach (var e in W.State.entities) if (W.IsCup(e)) existingCups.Add(e.id);
            }
            checkpoint = Capture(); retry.SetActive(next == Stage.SubmitCup || next == Stage.Complete && !firstCafeTutorial);
        }
        private bool PourStep => stage == Stage.Extract || stage == Stage.Pour;
        private bool DragStep => stage == Stage.Beans || stage == Stage.Grounds || stage == Stage.Water || PourStep || stage == Stage.TakeCup || stage == Stage.DeliverCup;
        private string Source => stage == Stage.SubmitCup ? delivery : stage == Stage.TakeCup ? cupSource : stage == Stage.DeliverCup ? cup : stage == Stage.Beans ? beans : stage == Stage.Grounds || stage == Stage.Grind || stage == Stage.Grinding ? grinder : stage == Stage.Water ? water : stage == Stage.Pour && essence ? extractor : heater;
        private string Target => stage == Stage.DeliverCup || stage == Stage.TakeCup ? delivery : stage == Stage.Beans ? grinder : stage == Stage.Grounds && essence || stage == Stage.Extract ? filter : stage == Stage.Pour ? cup : heater;
        public bool AllowClick(string id)
        {
            if (!IsRunning || restoring) return true;
            if (stage == Stage.SubmitCup) { ReportError("请点击工作台右下角的按铃交付，不是点击盘子。"); return false; }
            if ((stage == Stage.Grind || stage == Stage.Grinding) && id == grinder || stage == Stage.Heat && id == heater) return true;
            ReportError(DragStep ? "此步骤需要按住左键拖动，不是单击。" : "请点击提示标记的设备；加工会自动继续。"); return false;
        }
        public bool AllowDrag(string id)
        {
            if (!IsRunning || restoring) return true;
            if (DragStep && id == Source) return true;
            ReportError(DragStep ? "拿错物品了，请从光效的起点拖动。" : "请先完成点击或等待加工结束，再拖动。"); return false;
        }
        public bool AllowDrop(string id, bool placeOnly)
        {
            if (!IsRunning || restoring || !DragStep || stage == Stage.Extract || stage == Stage.Pour) return true;
            if (stage == Stage.TakeCup)
            {
                if (id == null) return true;
                ReportError("先把空杯拖到桌面空处，不要叠回杯堆或放进设备。"); return false;
            }
            if (!placeOnly && id == Target) return true;
            ReportError(placeOnly ? "请松开 Shift；Shift 只摆放，不会投入原料。" : "尚未拖到目标就松手了，请重新拖到光效终点。"); return false;
        }
        public bool AllowPour(string source, string target)
        {
            if (!IsRunning || restoring) return true;
            bool valid = (stage == Stage.Extract || stage == Stage.Pour) && source == Source && (target == Target || stage == Stage.Extract && target == extractor);
            if (valid) rightStarted = true;
            if (!valid) ReportError("倾倒目标不对：先松开右键，移到标记容器上方，再按住右键上下移动。");
            return valid;
        }
        private bool Has(string id, string spec) => W.Find(id)?.contents.Exists(p => p.specId == spec) == true;
        private void LateUpdate()
        {
            if (!IsRunning) { DrawWorkstationEntry(); return; }
            if (W.State.currentArea != AreaId.Counter) { Stop(); return; }
            canvas.gameObject.SetActive(!app.TutorialInputBlocked);
            if (!app.InputBlocked)
            {
                if (stage == Stage.Beans && Has(grinder, "beans")) SetStage(Stage.Grind);
                else if (stage == Stage.Grind && W.Find(grinder).production.phase != ProductionPhase.Loaded) SetStage(Stage.Grinding);
                else if (stage == Stage.Grinding && W.Find(grinder).production.phase == ProductionPhase.Ready) SetStage(Stage.Grounds);
                else if (stage == Stage.Grounds && Has(Target, "grounds")) SetStage(Stage.Water);
                else if (stage == Stage.Water && W.LiquidVolume(W.Find(heater)) > 0) SetStage(Stage.Heat);
                else if (stage == Stage.Heat && W.Find(heater).production.phase == ProductionPhase.Processing) SetStage(Stage.Heating);
                else if (stage == Stage.Heating && W.Find(heater).production.phase != ProductionPhase.Processing) SetStage(essence ? Stage.Extract : Stage.TakeCup);
                else if (stage == Stage.Drain && W.LiquidVolume(W.Find(filter)) < .01f) SetStage(Stage.Pour);
                if (stage == Stage.TakeCup && !app.IsDragging)
                {
                    var taken = W.State.entities.Find(e => W.IsCup(e) && e.area == AreaId.Counter && !existingCups.Contains(e.id));
                    if (taken != null) { cup = taken.id; SetStage(Stage.Pour); }
                }
                if (stage == Stage.DeliverCup && W.ServingCup?.id == cup && !app.IsDragging)
                    SetStage(Stage.SubmitCup);
                var mouse = Mouse.current;
                if (PourStep && app.IsDragging && mouse != null && mouse.rightButton.isPressed) rightStarted = true;
                if (PourStep && rightStarted && mouse != null && !mouse.rightButton.isPressed)
                {
                    FinishPourAttempt();
                }
                if (DragStep && wasDragging && !app.IsDragging)
                {
                    if (PourStep) FinishPourAttempt();
                    else
                    {
                        string reason = Time.unscaledTime < errorUntil ? error : "未投入目标，已恢复本步骤，请按住左键完整拖到终点。";
                        Restore(checkpoint); ReportError(reason);
                    }
                }
                wasDragging = app.IsDragging;
            }
            DrawGuide();
        }
        private void FinishPourAttempt()
        {
            bool empty = W.LiquidVolume(W.Find(Source)) <= .01f;
            app.CancelDrag(); // Finalizes extraction and returns the vessel before checkpointing.
            bool success = empty && (stage == Stage.Extract ? W.ExtractionSnapshot(extractor).completed : Has(cup, "coffee"));
            if (success) SetStage(stage == Stage.Extract ? Stage.Drain : Stage.DeliverCup);
            else
            {
                Restore(checkpoint); rightStarted = false;
                ReportError(empty ? "本次萃取未完成，已重置本步。请对准滤杯，保持稳定流量。" : "还没倒完就松开右键了！本步液体已复原，请重新拿起，持续按住右键直到倒空。");
            }
        }
        private string Description()
        {
            switch (stage)
            {
                case Stage.Beans: return "按住左键，从咖啡豆袋拖出咖啡豆 → 研磨机，在终点松手";
                case Stage.Grind: return "点击研磨机，开始研磨";
                case Stage.Grinding: return "连续点击研磨机加速！圆圈会反复提示点击；不点击也会自动磨完";
                case Stage.Grounds: return essence ? "拖出研磨机抽屉里的咖啡粉 → 精粹炉上层滤杯" : "拖出研磨机抽屉里的咖啡粉 → 通用加热炉";
                case Stage.Water: return "拖一份水 → 通用加热炉。粉量决定浓度，水量决定稀释比例";
                case Stage.Heat: return "点击通用加热炉加热（加热不需要连续点击）";
                case Stage.Heating: return "正在自动加热，请等待；不用重做上一步";
                case Stage.Extract:
                    var s = W.ExtractionSnapshot(extractor);
                    return $"拖动热水壶 → 上层滤杯，按住右键并上下移动控制流量\n粉 {s.groundsGrams:0} g · 注水 {s.injectedHotWaterMl:0}/{s.idealMl:0} ml · 时间 {s.elapsedSeconds:0.0}/{s.idealSeconds:0.0} s\n稳定小角度倾倒；保持右键直到热水倒空，中途松键会重来";
                case Stage.Drain: return "等待滤杯滴滤完成。下一步拿下层壶，不是上层滤杯";
                case Stage.TakeCup: return "按住左键，从杯堆拖出一个空杯，放到桌面空处\n先准备接咖啡的杯子，装好咖啡后再放到案板上的盘子里";
                case Stage.DeliverCup: return "按住左键，把装好咖啡的杯子拖到案板上的盘子里\n看到盘子高亮后松开左键，然后点击右下角按铃";
                case Stage.SubmitCup: return "咖啡已放到盘子里！点击工作台右下角的按铃完成交付\n" + (firstCafeTutorial ? "按铃交给首位客人后，新手教程才会结束" : "练习也要按铃才算完成；本次不结算正式订单");
                case Stage.Pour: return (essence ? "拖动精粹炉下层咖啡壶（自动分离滤杯）" : "拖动通用加热炉") + " → 标记杯子\n按住右键，上下移动使壶旋转倾斜；倒空后才能松开！";
                default: return "制作完成！是否要再来一次？退出后恢复练习前的工作台";
            }
        }
        private void BuildUI()
        {
            panel = Instantiate(Resources.Load<FreeCafeTutorialPanel>("UI/FreeCafeTutorialPanel"));
            canvas = panel.canvas; shade = panel.shade; heading = panel.heading;
            instruction = panel.instruction; errorText = panel.errorText; gestureText = panel.gestureText;
            gesture = panel.gesture; retry = panel.retry;
            trail.Clear(); trail.AddRange(panel.trail);
            panel.exitButton.onClick.AddListener(Stop);
            panel.retryButton.onClick.AddListener(() =>
            {
                bool mode = essence, firstCafe = firstCafeTutorial;
                Stop();
                if (firstCafe) BeginFirstCafeTutorial();
                else Begin(mode);
            });
        }
        private void HideWorkstationEntry()
        {
            if (!showingWorkstationEntry) return;
            showingWorkstationEntry = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void DrawWorkstationEntry()
        {
            if (app == null || W == null || !app.CafeTutorialWaitingForWorkstation
                || W.State.currentArea != AreaId.Cafe || app.TutorialInputBlocked)
            {
                HideWorkstationEntry();
                return;
            }
            if (entryPortal == null || !entryPortal.isActiveAndEnabled
                || entryPortal.visual == null || !entryPortal.visual.enabled || !entryPortal.visual.gameObject.activeInHierarchy
                || entryHitbox == null || !entryHitbox.isActiveAndEnabled
                || app.worldCamera == null || !app.worldCamera.isActiveAndEnabled
                || !app.IsCurrentAreaTransform(entryPortal.transform) || !app.CanEnterArea(AreaId.Counter))
            {
                HideWorkstationEntry();
                return;
            }
            if (canvas == null) BuildUI();
            showingWorkstationEntry = true;
            canvas.gameObject.SetActive(true);
            shade.enabled = false;
            retry.SetActive(false);
            panel.exitButton.gameObject.SetActive(false);
            heading.text = "咖啡馆 · 新手教程";
            instruction.text = "点击场景中的箭头标志，进入工作台为首位客人制作黑咖啡";
            errorText.text = "跟随鼠标光标提示进入工作台 · 完成教程后才能正式营业";
            gesture.gameObject.SetActive(true);
            gestureText.text = "点击箭头\n进入工作台";
            panel.SetMouseMode(false, false);

            var bounds = entryPortal.visual.bounds;
            Vector3 screenCenter = app.worldCamera.WorldToScreenPoint(bounds.center);
            if (screenCenter.z <= 0 || screenCenter.x < 0 || screenCenter.x > Screen.width
                || screenCenter.y < 0 || screenCenter.y > Screen.height)
            {
                HideWorkstationEntry();
                return;
            }
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, bounds.center.z);
                Vector2 screen = app.worldCamera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            Vector2 center = screenCenter;
            float scale = canvas.scaleFactor;
            float badgeX = min.x - 145 * scale;
            if (badgeX < 165 * scale) badgeX = max.x + 145 * scale;
            gesture.position = new Vector2(
                Mathf.Clamp(badgeX, 165 * scale, Mathf.Max(165 * scale, Screen.width - 165 * scale)),
                Mathf.Clamp(center.y, 80 * scale, Mathf.Max(80 * scale, Screen.height - 80 * scale)));
            float pulse = Mathf.Repeat(Time.unscaledTime * .8f, 1);
            Vector2 radius = (max - min) * .5f + Vector2.one * ((12 + pulse * 14) * scale);
            for (int i = 0; i < trail.Count; i++)
            {
                var dot = trail[i];
                dot.enabled = true;
                float angle = i * Mathf.PI * 2 / trail.Count;
                dot.rectTransform.position = center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                dot.rectTransform.sizeDelta = Vector2.one * (i == 0 ? panel.trailHeadSize : panel.trailSize);
                dot.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                var tint = panel.trailColor;
                tint.a *= 1 - pulse * .7f;
                dot.color = tint;
            }
        }

        private Vector2 ScreenPoint(string id)
        {
            if (stage == Stage.SubmitCup && deliveryBell != null)
                return app.worldCamera.WorldToScreenPoint(deliveryBell.TutorialAnchor.position);
            var view = app.View(id);
            if (view == null) return new Vector2(Screen.width * .5f, Screen.height * .5f);
            var plate = view.GetComponent<FreeCafeDeliveryZone>();
            var position = plate != null ? view.transform.TransformPoint(plate.ArtworkOffset) : view.transform.position;
            return app.worldCamera.WorldToScreenPoint(position);
        }
        private void DrawGuide()
        {
            shade.enabled = DragStep && !app.IsDragging;
            heading.text = (firstCafeTutorial ? "黑咖啡 · 新手教程" : essence ? "精粹咖啡 · 操作练习" : "黑咖啡 · 操作练习") + (PourStep ? "    |    必须倒空 · 松键重来" : "    |    跟随光效操作");
            instruction.text = Description();
            errorText.text = Time.unscaledTime < errorUntil ? error : firstCafeTutorial
                ? "完成黑咖啡并交付首位客人后，解锁正常营业" : "把咖啡放到盘子里并按铃才算完成 · 练习不结算订单，退出后恢复原工作台";
            bool showGesture = DragStep || stage == Stage.Grind || stage == Stage.Grinding || stage == Stage.Heat || stage == Stage.SubmitCup;
            gesture.gameObject.SetActive(showGesture);
            panel.SetMouseMode(PourStep && app.IsDragging, PourStep && app.IsDragging);
            gestureText.text = PourStep && app.IsDragging ? "按住右键\n上下移动旋转" : DragStep ? "按住左键\n拖到目标松开" : stage == Stage.Grinding ? "左键连续点击\n加速研磨" : stage == Stage.SubmitCup ? "点击右下角按铃\n完成交付" : "单击左键";
            Vector2 start = ScreenPoint(Source), end = ScreenPoint(Target);
            if (app.IsDragging && Mouse.current != null) start = Mouse.current.position.ReadValue();
            if (stage == Stage.TakeCup) end = new Vector2(Screen.width * .5f, Screen.height * .48f);
            float scale = canvas.scaleFactor;
            Vector2 badge = start + new Vector2(0, -panel.gestureOffset * scale);
            badge.x = Mathf.Clamp(badge.x, 165 * scale, Mathf.Max(165 * scale, Screen.width - 165 * scale));
            badge.y = Mathf.Clamp(badge.y, Screen.height * .25f + 65 * scale, Screen.height - 65 * scale);
            gesture.position = badge;
            for (int i = 0; i < trail.Count; i++)
            {
                var dot = trail[i]; dot.enabled = stage != Stage.Complete;
                float t = Mathf.Repeat(Time.unscaledTime * .65f - i * .035f, 1);
                Vector2 position = DragStep ? Vector2.Lerp(start, end, t) : start + new Vector2(Mathf.Cos(i * Mathf.PI * .2f), Mathf.Sin(i * Mathf.PI * .2f)) * (24 + t * 40);
                dot.rectTransform.position = position;
                dot.rectTransform.sizeDelta = Vector2.one * (i == 0 ? panel.trailHeadSize : panel.trailSize);
                dot.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                var tint = panel.trailColor; tint.a *= DragStep ? 1 - i * .08f : 1 - t; dot.color = tint;
            }
        }
    }
}
