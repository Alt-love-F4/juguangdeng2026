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
            beans = FindSource("beans"); water = FindSpec("source.water"); grinder = FindSpec("machine.grinder"); heater = FindSpec("machine.heater");
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
                foreach (string id in new[] { beans, cupSource }) W.Find(id).stock = Math.Max(10, W.Find(id).stock);
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
            wasDragging = false; rightStarted = false; restoring = false;
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
            checkpoint = Capture(); retry.SetActive(true);
        }
        private bool PourStep => stage == Stage.Water || stage == Stage.Extract || stage == Stage.Pour;
        private bool DragStep => stage == Stage.Beans || stage == Stage.Grounds || stage == Stage.Water || PourStep || stage == Stage.TakeCup || stage == Stage.DeliverCup;
        private string Source => stage == Stage.SubmitCup ? delivery : stage == Stage.TakeCup ? cupSource : stage == Stage.DeliverCup ? cup : stage == Stage.Beans ? beans : stage == Stage.Grounds || stage == Stage.Grind || stage == Stage.Grinding ? grinder : stage == Stage.Water ? water : stage == Stage.Pour && essence ? extractor : heater;
        private string Target => stage == Stage.DeliverCup || stage == Stage.TakeCup ? delivery : stage == Stage.Beans ? grinder : stage == Stage.Grounds && essence || stage == Stage.Extract ? filter : stage == Stage.Pour ? cup : heater;
        public bool AllowClick(string id)
        {
            if (!IsRunning || restoring) return true;
            if (stage == Stage.SubmitCup) { ReportError("请点击工作台右下角的按铃交付，不是点击盘子。"); return false; }
            if ((stage == Stage.Grind || stage == Stage.Grinding) && id == grinder || stage == Stage.Heat && id == heater) return true;
            ReportError(DragStep ? "取放原料需要按住左键拖动，再松开放下，不是单击。" : "请点击" + (stage == Stage.Grind || stage == Stage.Grinding ? "研磨机" : "通用加热炉") + "；正在加工时等待完成即可。"); return false;
        }
        public bool AllowDrag(string id)
        {
            if (!IsRunning || restoring) return true;
            if (DragStep && id == Source) return true;
            ReportError(DragStep ? "请按住左键从" + SourceLabel + "取用物品。" : "请先完成点击或等待加工结束，再拖动。"); return false;
        }
        public bool AllowDrop(string id, bool placeOnly)
        {
            if (!IsRunning || restoring || !DragStep) return true;
            if (PourStep)
            {
                ReportError("容器已放回。请按第 1 步，重新按住左键拖到目标上方。");
                return false;
            }
            if (stage == Stage.TakeCup)
            {
                if (id == null) return true;
                ReportError("先把空杯拖到桌面空处，不要叠回杯堆或放进设备。"); return false;
            }
            if (!placeOnly && id == Target) return true;
            ReportError(placeOnly ? "请松开 Shift；Shift 只摆放，不会投入原料。" : "还没放入" + TargetLabel + "。请按住左键移到" + TargetLabel + "，再松开左键。"); return false;
        }
        public bool AllowPour(string source, string target)
        {
            if (!IsRunning || restoring) return true;
            bool valid = PourStep && source == Source && IsPourTarget(target);
            if (valid) rightStarted = true;
            if (!valid) ReportError("还没有对准" + TargetLabel + "。请先松开右键，再按住左键把" + SourceLabel + "移到" + TargetLabel + "上方。");
            return valid;
        }
        private bool IsPourTarget(string target) => target != null && (target == Target || stage == Stage.Extract && target == extractor);
        private bool ReadyToPour => PourStep && app.IsDragging && IsPourTarget(app.CurrentPourTargetId);
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
                else if (stage == Stage.Water && W.LiquidVolume(W.Find(heater)) >= WaterTargetMl - .01f)
                {
                    app.CancelDrag();
                    SetStage(Stage.Heat);
                }
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
            // Consume the attempt before cancellation/restoration can notify other systems.
            // A failed water pour must not restore the same checkpoint every subsequent frame.
            rightStarted = false;
            if (stage == Stage.Water)
            {
                bool enough = W.LiquidVolume(W.Find(heater)) >= WaterTargetMl - .01f;
                app.CancelDrag();
                if (enough) SetStage(Stage.Heat);
                else { Restore(checkpoint); ReportError("注水未完成，本步已恢复。请跟随第 1 步，按住左键重新拖动清水壶。"); }
                return;
            }
            bool empty = W.LiquidVolume(W.Find(Source)) <= .01f;
            app.CancelDrag(); // Finalizes extraction and returns the vessel before checkpointing.
            bool success = empty && (stage == Stage.Extract ? W.ExtractionSnapshot(extractor).completed : Has(cup, "coffee"));
            if (success) SetStage(stage == Stage.Extract ? Stage.Drain : Stage.DeliverCup);
            else
            {
                Restore(checkpoint);
                ReportError(empty ? "本次萃取未完成，已重置本步。请对准滤杯，保持稳定流量。" : "倾倒未完成，本步液体已复原。请跟随第 1 步，按住左键重新拖到目标上方。");
            }
        }
        private string SourceLabel => stage == Stage.Beans ? "咖啡豆袋" : stage == Stage.Grounds ? "研磨机抽屉" : stage == Stage.Water ? "清水壶" : stage == Stage.TakeCup ? "杯堆" : stage == Stage.DeliverCup ? "装好咖啡的杯子" : stage == Stage.Pour && essence ? "精粹炉下层咖啡壶" : "通用加热炉";
        private string TargetLabel => stage == Stage.Beans ? "研磨机" : stage == Stage.Grounds && essence || stage == Stage.Extract ? "精粹炉上层滤杯" : stage == Stage.Pour ? "空杯" : stage == Stage.TakeCup ? "桌面空处" : stage == Stage.DeliverCup ? "案板上的盘子" : "通用加热炉";
        private string Description()
        {
            switch (stage)
            {
                case Stage.Beans: return "先把咖啡豆磨成粉，才能制作咖啡。\n按住鼠标左键从咖啡豆袋取豆，拖到研磨机后松开左键，放入豆子。";
                case Stage.Grind: return "豆子已放入研磨机。点击鼠标左键启动研磨，把豆子磨成咖啡粉。";
                case Stage.Grinding: return "研磨机正在把豆子磨成粉，等待完成即可。\n想快一点，可以连续点击研磨机加速；不点击也会自动磨完。";
                case Stage.Grounds: return "接下来" + (essence ? "把咖啡粉放入滤杯，准备热水萃取。" : "把咖啡粉放入加热炉，准备加水煮咖啡。")
                    + "\n按住左键从研磨机抽屉取粉，拖到" + TargetLabel + "后松开左键。";
                case Stage.Water: return ReadyToPour
                    ? $"{(essence ? "准备一份萃取用水。" : "给咖啡粉加入一份水。")}第 2 步：按住右键，上下移动鼠标使清水壶倾斜。\n注水 {W.LiquidVolume(W.Find(heater)):0}/{WaterTargetMl:0} ml；加够后水壶自动归位。"
                    : $"先加入 {WaterTargetMl:0} ml 清水，供下一步加热。\n第 1 步：按住左键拿起右上角清水壶，移到通用加热炉上方；先别松开左键。";
                case Stage.Heat: return essence ? "点击通用加热炉，把清水加热成萃取用的热水。\n单击左键启动后自动加热，不需要连续点击。" : "点击通用加热炉，把咖啡粉和水煮成黑咖啡。\n单击左键启动后自动加热，不需要连续点击。";
                case Stage.Heating: return "正在自动加热，等待完成即可。\n不用再次点击，也不用重做加水。";
                case Stage.Extract:
                    if (!ReadyToPour) return "用热水冲过咖啡粉，萃取出的咖啡会流入下层壶。\n第 1 步：按住左键拿起热水壶，移到精粹炉上层滤杯上方，先别松开左键。";
                    var s = W.ExtractionSnapshot(extractor);
                    return $"第 2 步：按住右键，上下移动鼠标控制热水流量，稳定冲泡咖啡粉。\n注水 {s.injectedHotWaterMl:0}/{s.idealMl:0} ml · 时间 {s.elapsedSeconds:0.0}/{s.idealSeconds:0.0} s\n保持小角度直到热水倒空，再松开右键；中途松开会重来。";
                case Stage.Drain: return "等待咖啡从滤杯滴入下层壶。\n滴滤结束后拿下层咖啡壶，不是上层滤杯。";
                case Stage.TakeCup: return "咖啡做好了，先准备一个杯子来接咖啡。\n按住左键从杯堆取出空杯，拖到桌面空处，松开左键放下。";
                case Stage.DeliverCup: return "把装好的咖啡放到出餐盘，准备交给客人。\n按住左键拖动咖啡杯，移到案板上的盘子后松开左键；下一步再按铃。";
                case Stage.SubmitCup: return "咖啡已放到出餐盘。点击工作台右下角的按铃，通知出餐并完成交付。\n" + (firstCafeTutorial ? "交给首位客人后，新手教程才会结束。" : "这是操作练习，本次不结算正式订单。");
                case Stage.Pour: return ReadyToPour
                    ? "把咖啡倒进杯里。第 2 步：按住右键，上下移动鼠标使壶倾斜。\n保持倾倒，等壶中咖啡倒空后再松开右键。"
                    : "接下来装杯，把做好的咖啡倒进空杯。\n第 1 步：按住左键拿起" + (essence ? "精粹炉下层咖啡壶" : "通用加热炉") + "，移到空杯上方，先别松开左键。";
                default: return "制作完成！是否要再来一次？退出后恢复练习前的工作台。";
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
            panel.retryButton.onClick.AddListener(Restart);
        }
        private float WaterTargetMl => W.Catalog.production.portionMl;
        public float LimitPourAmount(string sourceId, float amount) => IsRunning && stage == Stage.Water && sourceId == water
            ? Mathf.Min(amount, Mathf.Max(0, WaterTargetMl - W.LiquidVolume(W.Find(heater)))) : amount;

        public void Restart()
        {
            if (app.TutorialInputBlocked) return;
            if (!IsRunning)
            {
                if (showingWorkstationEntry) { app.CancelDrag(); DrawWorkstationEntry(); }
                return;
            }
            bool mode = essence, firstCafe = firstCafeTutorial;
            Stop();
            if (firstCafe) BeginFirstCafeTutorial();
            else Begin(mode);
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
            retry.SetActive(true);
            panel.exitButton.gameObject.SetActive(false);
            heading.text = "咖啡馆 · 新手教程";
            instruction.text = "工作台是取用原料、加工和出餐的地方。\n点击场景中的箭头标志，进入工作台为首位客人制作无糖黑咖啡。";
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
        private string StageTitle()
        {
            switch (stage)
            {
                case Stage.Beans: return "放入咖啡豆";
                case Stage.Grind: case Stage.Grinding: return "把豆磨成粉";
                case Stage.Grounds: return "取出咖啡粉";
                case Stage.Heat: case Stage.Heating: return essence ? "准备热水" : "煮成黑咖啡";
                case Stage.TakeCup: return "准备空杯";
                case Stage.DeliverCup: return "放上出餐盘";
                case Stage.SubmitCup: return "按铃交付";
                case Stage.Drain: return "等待滴滤";
                default: return "练习完成";
            }
        }
        private void DrawGuide()
        {
            shade.enabled = DragStep && !app.IsDragging;
            bool readyToPour = ReadyToPour;
            heading.text = (firstCafeTutorial ? "无糖黑咖啡 · 新手教程" : essence ? "精粹咖啡 · 操作练习" : "黑咖啡 · 操作练习") + (PourStep ? readyToPour ? "    |    第 2 步 · 右键倾倒" : "    |    第 1 步 · 左键拿起" : "    |    " + StageTitle());
            instruction.text = Description();
            errorText.text = Time.unscaledTime < errorUntil ? error : firstCafeTutorial
                ? "完成黑咖啡并交付首位客人后，解锁正常营业 · 随时点上方重来" : "练习不结算订单 · 废弃桶在左下角 · 随时点上方重来，恢复初始练习";
            bool showGesture = DragStep || stage == Stage.Grind || stage == Stage.Grinding || stage == Stage.Heat || stage == Stage.SubmitCup;
            gesture.gameObject.SetActive(showGesture);
            panel.SetMouseMode(readyToPour, readyToPour);
            gestureText.text = PourStep ? readyToPour ? "按住右键\n上下移动倾倒" : "按住左键拿起\n移到" + TargetLabel + "上方" : DragStep ? "左键取用" + SourceLabel + "\n放到" + TargetLabel : stage == Stage.Grinding ? "左键连续点击\n加速研磨（可选）" : stage == Stage.SubmitCup ? "点击右下角按铃\n完成交付" : "单击左键";
            Vector2 start = ScreenPoint(Source), end = ScreenPoint(Target);
            if (PourStep) end += Vector2.up * (55 * canvas.scaleFactor);
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
                Vector2 position = DragStep && !readyToPour ? Vector2.Lerp(start, end, t) : (readyToPour ? end : start) + new Vector2(Mathf.Cos(i * Mathf.PI * .2f), Mathf.Sin(i * Mathf.PI * .2f)) * (24 + t * 40);
                dot.rectTransform.position = position;
                dot.rectTransform.sizeDelta = Vector2.one * (i == 0 ? panel.trailHeadSize : panel.trailSize);
                dot.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                var tint = panel.trailColor; tint.a *= DragStep ? 1 - i * .08f : 1 - t; dot.color = tint;
            }
        }
    }
}
