using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>GM-only guided practice. No narrative completion signals or save progression.</summary>
    public sealed class FreeCafeWorkstationTutorial : MonoBehaviour
    {
        public enum Stage { Beans, Grind, Grinding, Grounds, Water, Heat, Heating, Extract, Drain, TakeCup, Pour, DeliverCup, Complete }
        [Serializable] private sealed class CounterState { public List<WorldEntity> entities = new List<WorldEntity>(); public string cup; public bool automatic; }
        private FreeCafeRuntime app;
        private string original, checkpoint, beans, grinder, heater, extractor, filter, water, cup, cupSource, delivery;
        private readonly HashSet<string> existingCups = new HashSet<string>();
        private bool rightStarted;
        private bool essence, wasDragging, restoring;
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
        public bool IsRunning { get; private set; }
        public Stage CurrentStage => stage;
        private CafeWorld W => app.World;
        public void Initialize(FreeCafeRuntime owner) { app = owner; }

        private string FindSpec(string spec) => W.State.entities.Find(e => e.area == AreaId.Counter && e.specId == spec)?.id;
        private string FindSource(string spec) => W.State.entities.Find(e => e.area == AreaId.Counter && W.Spec(e.specId).Has(Capability.Source) && W.Spec(e.specId).sourceItemId == spec)?.id;
        public bool Begin(bool extraction)
        {
            if (IsRunning) Stop();
            if (app == null || !W.ProductionEnabled || W.State.currentArea != AreaId.Counter || app.GameplayBlocked || app.IsDragging) return false;
            beans = FindSource("beans"); water = FindSource("water"); grinder = FindSpec("machine.grinder"); heater = FindSpec("machine.heater");
            extractor = W.State.entities.Find(e => e.area == AreaId.Counter && W.IsPrecisionExtractor(e))?.id;
            filter = FindSpec("container.extractor.filter");
            cupSource = FindSource("cup.large") ?? FindSource("cup.small");
            delivery = W.State.entities.Find(e => e.area == AreaId.Counter && W.Spec(e.specId).Has(Capability.Delivery))?.id;
            if (beans == null || water == null || grinder == null || heater == null || cupSource == null || delivery == null || extraction && (extractor == null || filter == null)) return false;
            original = Capture(); essence = extraction;
            W.State.automaticMachines = false;
            // Only practice equipment is reset; the original counter is restored on exit.
            foreach (string id in new[] { grinder, heater, extractor, filter })
            {
                var e = W.Find(id); if (e == null) continue;
                e.contents.Clear(); e.task = null; e.production = new ProductionEntityState();
            }
            W.State.production.activeCupId = null;
            foreach (string id in new[] { beans, water, cupSource }) W.Find(id).stock = Math.Max(10, W.Find(id).stock);
            cup = null;
            if (essence)
            {
                W.SelectServingCup(cupSource); cup = W.ServingCup?.id;
                if (cup == null) { Restore(original); return false; }
            }
            if (essence) W.AttachExtractionFilter(filter, extractor);
            IsRunning = true;
            if (canvas == null) BuildUI();
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
        private void Restore(string json)
        {
            restoring = true;
            app.CancelDrag();
            var state = JsonUtility.FromJson<CounterState>(json);
            W.State.entities.RemoveAll(e => e.area == AreaId.Counter);
            W.State.entities.AddRange(state.entities);
            W.State.production.activeCupId = state.cup;
            W.State.automaticMachines = state.automatic;
            app.DebugRefreshViews(); wasDragging = false; restoring = false;
        }
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            Restore(original);
            if (canvas != null) canvas.gameObject.SetActive(false);
        }
        private void OnDisable() { if (app != null && app.World != null) Stop(); }
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
            checkpoint = Capture(); retry.SetActive(next == Stage.Complete);
        }
        private bool PourStep => stage == Stage.Extract || stage == Stage.Pour;
        private bool DragStep => stage == Stage.Beans || stage == Stage.Grounds || stage == Stage.Water || PourStep || stage == Stage.TakeCup || stage == Stage.DeliverCup;
        private string Source => stage == Stage.TakeCup ? cupSource : stage == Stage.DeliverCup ? cup : stage == Stage.Beans ? beans : stage == Stage.Grounds || stage == Stage.Grind || stage == Stage.Grinding ? grinder : stage == Stage.Water ? water : stage == Stage.Pour && essence ? extractor : heater;
        private string Target => stage == Stage.DeliverCup || stage == Stage.TakeCup ? delivery : stage == Stage.Beans ? grinder : stage == Stage.Grounds && essence || stage == Stage.Extract ? filter : stage == Stage.Pour ? cup : heater;
        public bool AllowClick(string id)
        {
            if (!IsRunning || restoring) return true;
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
            if (!IsRunning) return;
            if (W.State.currentArea != AreaId.Counter) { Stop(); return; }
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
                if (stage == Stage.DeliverCup && W.ServingCup?.id == cup && !app.IsDragging) SetStage(Stage.Complete);
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
            if (success) SetStage(stage == Stage.Extract ? Stage.Drain : essence ? Stage.Complete : Stage.DeliverCup);
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
                case Stage.TakeCup: return "按住左键，从杯堆拖出一个空杯，放到桌面空处\n先准备接咖啡的杯子，装好咖啡后再放入交付区";
                case Stage.DeliverCup: return "按住左键，把装满黑咖啡的杯子拖入交付区\n看到目标高亮后松开左键（本教程不提交订单）";
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
            panel.retryButton.onClick.AddListener(() => { bool mode = essence; Stop(); Begin(mode); });
        }
        private Vector2 ScreenPoint(string id)
        {
            var view = app.View(id); return view != null ? (Vector2)app.worldCamera.WorldToScreenPoint(view.transform.position) : new Vector2(Screen.width * .5f, Screen.height * .5f);
        }
        private void DrawGuide()
        {
            shade.enabled = DragStep && !app.IsDragging;
            heading.text = (essence ? "精粹咖啡 · 操作练习" : "黑咖啡 · 操作练习") + (PourStep ? "    |    必须倒空 · 松键重来" : "    |    跟随光效操作");
            instruction.text = Description();
            errorText.text = Time.unscaledTime < errorUntil ? error : "练习不会提交订单 · 退出后恢复原工作台";
            bool showGesture = DragStep || stage == Stage.Grind || stage == Stage.Grinding || stage == Stage.Heat;
            gesture.gameObject.SetActive(showGesture);
            panel.SetMouseMode(PourStep && app.IsDragging, PourStep && app.IsDragging);
            gestureText.text = PourStep && app.IsDragging ? "按住右键\n上下移动旋转" : DragStep ? "按住左键\n拖到目标松开" : stage == Stage.Grinding ? "左键连续点击\n加速研磨" : "单击左键";
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
