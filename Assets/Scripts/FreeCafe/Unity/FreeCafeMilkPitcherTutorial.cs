using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// Repeatable milk-pitcher / latte-art practice. This component is intentionally
    /// self-contained: it observes the production world and restores the counter on exit.
    /// Runtime input remains the normal, unrestricted production input.
    /// </summary>
    public sealed class FreeCafeMilkPitcherTutorial : MonoBehaviour
    {
        public enum Stage { PrepareCup, PrepareDrink, FillPitcher, Froth, Transfer, DrawLatte, Complete }

        [Serializable] private sealed class CounterState
        {
            public List<WorldEntity> entities = new List<WorldEntity>();
            public string activeCup;
            public bool activeCupWasNull;
            public bool automaticMachines;
        }

        private FreeCafeRuntime app;
        private FreeCafeTutorialPanel panel;
        private Canvas canvas;
        private Image shade;
        private TMP_Text instruction, heading, errorText, gestureText;
        private RectTransform gesture;
        private readonly List<Image> trail = new List<Image>();
        private string original;
        private string cupId;
        private string pitcherId;
        private string milkSourceId;
        private string cupSourceId;
        private string latteToolId;
        private string error;
        private float errorUntil;
        private Stage stage;
        private bool restoring;

        public bool IsRunning { get; private set; }
        public Stage CurrentStage => stage;
        public string TodoInstruction => Description();

        private CafeWorld W => app == null ? null : app.World;

        /// <summary>Starts only on the counter with acquired latte equipment and no modal/drag.</summary>
        public bool Begin(FreeCafeRuntime owner)
        {
            if (owner == null || owner.World == null || !owner.World.ProductionEnabled
                || owner.World.State.currentArea != AreaId.Counter || owner.InputBlocked
                || owner.IsDragging || owner.World.State.production != null
                    && !string.IsNullOrEmpty(owner.World.State.production.miniGameToken))
                return false;

            if (IsRunning) Stop();
            app = owner;
            var world = owner.World;
            if (!world.LatteEquipmentAvailable) return false;
            if (FindFirstObjectByType<FreeCafeLatteArtBench>(FindObjectsInactive.Include) == null) return false;

            pitcherId = FindEntity("container.milk-pitcher");
            milkSourceId = FindSource("milk");
            cupSourceId = FindCupSource();
            latteToolId = FindEntity("tool.latte-art");
            // The authored latte-art entry was intentionally removed from the counter;
            // the real Bench is the MiniGameGateway presenter, not a world entity.
            if (pitcherId == null || milkSourceId == null || cupSourceId == null) return false;

            original = Capture();
            var pitcher = W.Find(pitcherId);
            pitcher.contents.Clear();
            pitcher.production = new ProductionEntityState();
            if (!W.Take(cupSourceId, AreaId.Counter, 0f, -2f, out var practiceCup).success)
            {
                Restore(original);
                return false;
            }
            W.State.automaticMachines = false;
            practiceCup.contents.Add(new Portion
            {
                specId = "coffee", units = 1, milliliters = 100f,
                coffeeKind = "black", temperatureCelsius = 60f,
                temperature = LiquidTemperature.Warm
            });
            practiceCup.production.phase = ProductionPhase.Room;
            W.State.production.activeCupId = practiceCup.id;
            cupId = practiceCup.id;
            IsRunning = true;
            BuildUI();
            panel.exitButton.gameObject.SetActive(true);
            panel.retry.SetActive(true);
            canvas.gameObject.SetActive(true);
            SetStage(Stage.FillPitcher);
            app.ShowHint("拉花练习已开始：已准备独立的 100 ml 练习饮品杯，不消耗正式原料。");
            app.DebugRefreshViews();
            return true;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                if (canvas != null) canvas.gameObject.SetActive(false);
                return;
            }

            IsRunning = false;
            var gateway = FindFirstObjectByType<FreeCafeMiniGameGateway>(FindObjectsInactive.Include);
            if (gateway != null && W != null && W.State.production != null
                && !string.IsNullOrEmpty(W.State.production.miniGameToken)) gateway.Abort();
            if (!string.IsNullOrEmpty(original) && W != null) Restore(original);
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Stop();
            if (panel != null) Destroy(panel.gameObject);
        }

        private string FindEntity(string specId)
        {
            if (W == null) return null;
            foreach (var entity in W.State.entities)
                if (entity.area == AreaId.Counter && entity.specId == specId) return entity.id;
            return null;
        }

        private string FindSource(string itemSpec)
        {
            if (W == null) return null;
            foreach (var entity in W.State.entities)
                if (entity.area == AreaId.Counter && W.Spec(entity.specId).Has(Capability.Source)
                    && W.Spec(entity.specId).sourceItemId == itemSpec) return entity.id;
            return null;
        }

        private string FindCupSource()
        {
            if (W == null) return null;
            foreach (var entity in W.State.entities)
                if (entity.area == AreaId.Counter && W.Spec(entity.specId).Has(Capability.Source)
                    && W.Spec(entity.specId).sourceItemId.StartsWith("cup.", StringComparison.Ordinal)) return entity.id;
            return null;
        }

        private bool IsPracticeDrink(WorldEntity cup)
        {
            return cup != null && W.IsLatteArtCup(cup) && W.LiquidVolume(cup) > .001f;
        }

        private string Capture()
        {
            var state = new CounterState
            {
                activeCup = W.State.production.activeCupId,
                activeCupWasNull = W.State.production.activeCupId == null,
                automaticMachines = W.State.automaticMachines
            };
            foreach (var entity in W.State.entities)
                if (entity.area == AreaId.Counter) state.entities.Add(entity);
            return JsonUtility.ToJson(state);
        }

        private void Restore(string json)
        {
            restoring = true;
            app.CancelDrag();
            var state = JsonUtility.FromJson<CounterState>(json);
            W.State.entities.RemoveAll(entity => entity.area == AreaId.Counter);
            W.State.entities.AddRange(state.entities);
            W.State.production.activeCupId = state.activeCupWasNull ? null : state.activeCup;
            W.State.automaticMachines = state.automaticMachines;
            restoring = false;
            app.DebugRefreshViews();
        }

        private void SetStage(Stage next)
        {
            stage = next;
            error = null;
            errorUntil = 0;
        }

        private void BuildUI()
        {
            if (panel == null)
            {
                panel = Instantiate(Resources.Load<FreeCafeTutorialPanel>("UI/FreeCafeTutorialPanel"));
                canvas = panel.canvas;
                shade = panel.shade;
                heading = panel.heading;
                instruction = panel.instruction;
                errorText = panel.errorText;
                gestureText = panel.gestureText;
                gesture = panel.gesture;
                trail.AddRange(panel.trail);
                panel.exitButton.onClick.AddListener(Stop);
                panel.retryButton.onClick.AddListener(Restart);
            }
        }

        public void Restart()
        {
            if (!IsRunning || app == null || app.TutorialInputBlocked) return;
            Stop();
            Begin(app);
        }

        private void LateUpdate()
        {
            if (!IsRunning || app == null || W == null || panel == null) return;
            if (W.State.currentArea != AreaId.Counter) { Stop(); return; }

            // Advance before hiding: the gateway owns the real latte-art modal, so
            // Transfer must observe its token even while this panel is invisible.
            AdvanceFromWorld();
            bool hidden = app.TutorialInputBlocked || W.State.production != null
                && !string.IsNullOrEmpty(W.State.production.miniGameToken);
            canvas.gameObject.SetActive(!hidden);
            if (hidden) return;

            DrawGuide();
        }

        private void AdvanceFromWorld()
        {
            var pitcher = W.Find(pitcherId);
            var cup = W.Find(cupId);
            if (stage == Stage.PrepareCup)
            {
                if (W.ServingCup != null) { cupId = W.ServingCup.id; cup = W.ServingCup; }
                if (cup != null) SetStage(IsPracticeDrink(cup) ? Stage.PrepareDrink : Stage.PrepareDrink);
            }
            else if (stage == Stage.PrepareDrink && IsPracticeDrink(cup)) SetStage(Stage.FillPitcher);
            else if (stage == Stage.FillPitcher && W.LiquidVolume(pitcher) > .001f) SetStage(Stage.Froth);
            else if (stage == Stage.Froth && W.MilkFoamVolume(pitcher) > .001f) SetStage(Stage.Transfer);
            else if (stage == Stage.Transfer)
            {
                var session = W.State.production;
                if (session != null && !string.IsNullOrEmpty(session.miniGameToken)
                    && session.miniGameCupId == cupId) SetStage(Stage.DrawLatte);
            }
            else if (stage == Stage.DrawLatte)
            {
                var session = W.State.production;
                if ((session == null || string.IsNullOrEmpty(session.miniGameToken))
                    && W.Find(cupId) != null && W.Find(cupId).latteArt) SetStage(Stage.Complete);
                else if (session == null || string.IsNullOrEmpty(session.miniGameToken)) SetStage(Stage.Transfer);
            }
        }

        private void DrawGuide()
        {
            shade.enabled = false;
            panel.retry.SetActive(true);
            panel.exitButton.gameObject.SetActive(true);
            heading.text = "拉花杯 · 重复操作练习" + (stage == Stage.DrawLatte ? "  | 真实拉花界面" : string.Empty);
            instruction.text = Description();
            errorText.text = Time.unscaledTime < errorUntil ? error : "本练习不结算订单；重来或退出会恢复练习前工作台。";
            gesture.gameObject.SetActive(true);
            panel.SetMouseMode(false, false);
            gestureText.text = GestureDescription();

            Vector2 target = ScreenPoint(TargetId());
            if (Mouse.current != null && app.IsDragging) target = Mouse.current.position.ReadValue();
            gesture.position = ClampBadge(target + Vector2.up * 90f);
            for (int i = 0; i < trail.Count; i++)
            {
                var dot = trail[i];
                dot.enabled = stage != Stage.Complete;
                float t = Mathf.Repeat(Time.unscaledTime * .65f - i * .035f, 1);
                dot.rectTransform.position = target + new Vector2(Mathf.Cos(i * .6f), Mathf.Sin(i * .6f)) * (24 + t * 30);
                dot.color = panel.trailColor;
            }
        }

        private Vector2 ClampBadge(Vector2 value)
        {
            float scale = canvas.scaleFactor;
            Vector2 half = gesture.rect.size * .5f + Vector2.one * 12;
            value.x = Mathf.Clamp(value.x, half.x * scale, Mathf.Max(half.x * scale, Screen.width - half.x * scale));
            value.y = Mathf.Clamp(value.y, Screen.height * .25f + half.y * scale, Mathf.Max(Screen.height * .25f + half.y * scale, Screen.height - half.y * scale));
            return value;
        }

        private string TargetId()
        {
            switch (stage)
            {
                case Stage.PrepareCup: return cupSourceId;
                case Stage.PrepareDrink: return cupId;
                case Stage.FillPitcher: return milkSourceId;
                case Stage.Froth: return pitcherId;
                case Stage.Transfer: return cupId;
                case Stage.DrawLatte: return null;
                default: return cupId;
            }
        }

        private Vector2 ScreenPoint(string id)
        {
            var view = app.View(id);
            return view == null || app.worldCamera == null
                ? new Vector2(Screen.width * .5f, Screen.height * .52f)
                : app.worldCamera.WorldToScreenPoint(view.transform.position);
        }

        private string Description()
        {
            switch (stage)
            {
                case Stage.PrepareCup: return "先准备练习饮品杯。点击杯堆取杯，再按真实制作流程加入饮品液体；本次练习结束会恢复杯堆和内容物。";
                case Stage.PrepareDrink: return "练习饮品杯已准备好。确认杯中有饮品液体，再处理牛奶。";
                case Stage.FillPitcher: return "准备牛奶：按住鼠标左键从牛奶来源取牛奶，拖到拉花杯上方后松开左键注入。拉花杯不是普通倾倒容器，不要用右键倒。";
                case Stage.Froth: return "牛奶已进入拉花杯。点击拉花杯打奶，直到变成奶泡。";
                case Stage.Transfer: return "奶泡已准备好。按住鼠标左键拖动拉花杯到饮品杯上，松开后进入真实拉花界面。这里不会使用普通右键倾倒。";
                case Stage.DrawLatte: return "现在是实际拉花界面。按住左键画出图案，完成后点击界面内的“提交拉花”；返回后继续显示完成结果。";
                default: return "拉花练习完成！点击“重来”再次练习，或“退出”恢复练习前所有工作台实体和状态。";
            }
        }

        private string GestureDescription()
        {
            switch (stage)
            {
                case Stage.PrepareCup: return "准备练习杯\n点击杯堆取杯";
                case Stage.PrepareDrink: return "杯中加入饮品\n按真实制作流程操作";
                case Stage.FillPitcher: return "左键拖牛奶\n放到拉花杯松开";
                case Stage.Froth: return "点击拉花杯\n打成奶泡";
                case Stage.Transfer: return "左键拖奶泡杯\n放到饮品杯";
                case Stage.DrawLatte: return "真实界面中左键画图\n完成后提交";
                default: return "完成\n可重来或退出";
            }
        }
    }
}
