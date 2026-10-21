using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace Spotlight.FreeCafe
{
    /// <summary>Drives the current game through its public UI commands. Never seeds facts or completes gameplay.</summary>
    public sealed class NarrativeWalkthroughRunner : MonoBehaviour
    {
        [Serializable] public sealed class Entry
        {
            public int day;
            public string scene, location, kind, text;
        }
        [Serializable] private sealed class Report
        {
            public string target, outcome, reason;
            public bool assisted;
            public string startup;
            public List<Entry> entries;
        }
        public bool Running { get; private set; }
        public bool Paused { get; private set; }
        public bool ShowOverlay { get; set; } = true;
        public bool AutoChoose { get; set; } = true;
        public bool FastDialogue { get; set; } = true;
        public bool SkipOpeningAndTutorials { get; set; } = true;
        private bool pendingTutorialSkip, usedStartupBypass;
        private string startupMode;
        public float StepSeconds { get; set; } = 1.5f;
        public string Status { get; private set; } = "未开始";
        public string TargetEnding { get; private set; }
        public string OutputPath { get; private set; }
        public string Outcome { get; private set; }
        public string ActualEnding { get; private set; }
        public bool AssistCoffee { get; set; }
        public bool UsedAssistance { get; private set; }
        public bool Unattended { get; set; }
        private StreamWriter readableLog;
        private float lastSnapshot;
        public string ReadablePath => string.IsNullOrEmpty(OutputPath) ? null : Path.ChangeExtension(OutputPath, ".md");
        public string ReportDirectory { get; set; }
        public int MaximumDay { get; set; } = 60;
        public int CurrentDay => lastDay;
        public IReadOnlyList<Entry> History => entries.AsReadOnly();
        private readonly List<Entry> entries = new List<Entry>();
        private readonly HashSet<string> observedChoices = new HashSet<string>();
        private GameFlowBootstrap flow;
        private NarrativeRuntime runtime;
        private NarrativeController observedController;
        private NarrativePresenter observedPresenter;
        private bool previousSkipText;
        private NarrativeAutomationPlan plan;
        private IDisposable saveLease;
        private float nextStep, lastProgress;
        private bool singleStep, collapsed;
        private string lastScene, lastArea, lastLine, lastTodo, manualKey, lastProduction;
        private int lastServed = -1;
        private int lastDay;
        private Vector2 scroll;
        private GUIStyle textStyle;
        private Font overlayFont;

        public void Begin(string targetEnding = null)
        {
            if (Running) throw new InvalidOperationException("请先停止当前现场测试。");
            flow = FindFirstObjectByType<GameFlowBootstrap>();
            if (flow == null) throw new InvalidOperationException("请从游戏主菜单或正式游戏启动现场测试。");
            if (flow.SessionState != GameSessionState.Menu && flow.SessionState != GameSessionState.Playing)
                throw new InvalidOperationException("请等待转场完成，或返回主菜单后开始。");
            var config = NarrativeRuntime.Instance?.Configuration ?? LubanNarrativeLoader.Load();
            plan = string.IsNullOrEmpty(targetEnding) ? NarrativeAutomationPlan.ForRoute(config, null) : NarrativeAutomationPlan.ForEnding(config, targetEnding);
            saveLease = NarrativeAutoRunner.AcquireWalkthrough();
            Outcome = null; ActualEnding = null; UsedAssistance = false;
            TargetEnding = targetEnding;
            bool fromMenu = flow.SessionState == GameSessionState.Menu;
            bool skip = SkipOpeningAndTutorials && targetEnding != "arrival.ending.if0";
            pendingTutorialSkip = skip && !fromMenu && flow.GetComponent<DayFlowController>()?.IsFirstSessionGuideActive == true;
            usedStartupBypass = skip && fromMenu || pendingTutorialSkip;
            startupMode = fromMenu ? skip ? "skip-opening-and-tutorials" : "full-opening"
                : pendingTutorialSkip ? "current-session-skip-tutorials" : "current-session";
            entries.Clear(); observedChoices.Clear();
            lastScene = lastArea = lastLine = lastTodo = manualKey = lastProduction = null; lastDay = 0; lastServed = -1;
            Running = true; Paused = false; singleStep = false;
            lastProgress = nextStep = Time.unscaledTime;
            OutputPath = Path.Combine(ReportDirectory ?? Path.Combine(Application.persistentDataPath, "NarrativeWalkthroughs"), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                readableLog = new StreamWriter(ReadablePath, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                readableLog.WriteLine("# 前台结局测试轨迹\n\n目标：" + targetEnding + "\n\n模式：" + startupMode + "；自动操作=" + Unattended + "\n\n咖啡辅助跳过操作与补料，真实结算评分/收入/声望/剧情回执；不修改历史标记。\n");
                if (!fromMenu) BindRuntime();
                Record("开始", "跟随真实待办；保留原始属性、资金、制作、日期与剧情门禁。测试期间禁止保存。停止后保留现场供检查。");
                if (usedStartupBypass) Record("快速测试", fromMenu
                    ? "跳过开场/建角信件/新手教程；默认角色直接进入游戏。"
                    : "沿用当前会话，跳过尚未完成的新手引导。此轮记录为辅助测试。");
                if (flow.SessionState == GameSessionState.Menu)
                { if (skip) flow.StartFastTestGame(); else flow.StartGame(); }
                Save("running", "");
            }
            catch { Stop(); throw; }
        }

        private void BindRuntime()
        {
            var current = NarrativeRuntime.Instance;
            var presenter = current?.Presenter;
            if (!ReferenceEquals(presenter, observedPresenter))
            {
                if (observedPresenter != null) observedPresenter.SkipDialogueText = previousSkipText;
                observedPresenter = presenter;
                previousSkipText = presenter?.SkipDialogueText ?? false;
            }
            if (observedPresenter != null) observedPresenter.SkipDialogueText = FastDialogue && (!Paused || singleStep) || previousSkipText;
            if (current == runtime && current?.Controller == observedController) return;
            if (observedController != null)
            {
                observedController.EventCompleted -= OnEventCompleted;
                observedController.EventFailed -= OnEventFailed;
                observedController.BarChanged -= OnBarChanged;
            }
            runtime = current;
            observedController = current?.Controller;
            if (observedController != null)
            {
                observedController.EventCompleted += OnEventCompleted;
                observedController.EventFailed += OnEventFailed;
                observedController.BarChanged += OnBarChanged;
                foreach (string id in observedController.SelectedChoices) observedChoices.Add(id);
            }
        }
        private void OnBarChanged(string key, int before, int after)
            => Record("数值变化", key + "：" + before + " → " + after);
        private void OnEventCompleted(string id) => Record("完成剧情", id);
        private void OnEventFailed(string id, string reason)
        {
            // GameOver disposes the formal presenter after a successfully committed root.
            if (observedController?.IsEnded == true || flow?.SessionState != GameSessionState.Playing) return;
            RequireManual("event:" + id, "剧情未完成：" + id + "；" + reason);
        }
        private void Record(string kind, string text)
        {
            if (entries.Count >= 10000) { Paused = true; Status = "操作记录达到上限，请停止后查看报告。"; return; }
            var cafe = FindFirstObjectByType<FreeCafeRuntime>();
            entries.Add(new Entry { day = FindFirstObjectByType<DayFlowController>()?.CurrentDay ?? 0,
                scene = SceneManager.GetActiveScene().name,
                location = cafe != null && cafe.isActiveAndEnabled && cafe.World != null && !flow.IsMapOpen
                    ? cafe.World.Area(cafe.World.State.currentArea)?.label : runtime?.Controller?.Location,
                kind = kind, text = text });
            Status = text; lastProgress = Time.unscaledTime; scroll.y = float.MaxValue;
            readableLog?.WriteLine("- D" + entries[entries.Count - 1].day + " | " + entries[entries.Count - 1].scene + " / " + entries[entries.Count - 1].location + " | **" + kind + "**：" + text.Replace("\r", "").Replace("\n", " / "));
            try { if (Time.unscaledTime - lastSnapshot > 2f) { Save("running", ""); lastSnapshot = Time.unscaledTime; } }
            catch (IOException error) { Status = "报告写入失败：" + error.Message; Paused = true; }
        }
        private void Observe()
        {
            if (flow.SessionState == GameSessionState.Playing) BindRuntime();
            string scene = SceneManager.GetActiveScene().name + (flow.IsMapOpen ? " / 城区地图" : "");
            if (scene != lastScene) { lastScene = scene; Record("切换场景", scene); }
            int day = FindFirstObjectByType<DayFlowController>()?.CurrentDay ?? 0;
            if (day != lastDay) { lastDay = day; Record("日期", "第 " + day + " 天"); }
            var cafe = FindFirstObjectByType<FreeCafeRuntime>();
            string area = cafe != null && cafe.isActiveAndEnabled && cafe.World != null && !flow.IsMapOpen
                ? cafe.World.Area(cafe.World.State.currentArea)?.label : runtime?.Controller?.Location;
            if (area != lastArea) { lastArea = area; Record("到达地点", area ?? "等待场景就绪"); }
            if (cafe != null && cafe.World != null)
            {
                var world = cafe.World;
                if (lastServed >= 0 && world.State.todayServed > lastServed)
                    Record("真实交付", "今日成功交付 " + world.State.todayServed + " 杯。");
                lastServed = world.State.todayServed;
                var cup = world.ServingCup;
                string production = world.State.production?.miniGameKind + "/" + world.State.production?.miniGameToken
                    + "/" + cup?.id + "/" + (cup == null ? "" : world.ContainerInspection(cup));
                if (production != lastProduction)
                {
                    lastProduction = production;
                    if (!string.IsNullOrEmpty(world.State.production?.miniGameToken))
                        Record("制作小游戏", world.State.production.miniGameKind);
                    else if (cup != null) Record("制作状态", world.ContainerInspection(cup));
                }
            }
            var dialogue = DialoguePanel.Active;
            if (dialogue != null && dialogue.IsPlaying)
            {
                string line = dialogue.CurrentSource?.SourceLineId + "/" + dialogue.LineIndex + "/" + dialogue.CurrentLineText;
                if (line != lastLine) { lastLine = line; Record("对话", dialogue.CurrentLineText); }
            }
            if (runtime?.Controller != null)
                foreach (string id in runtime.Controller.SelectedChoices)
                    if (observedChoices.Add(id)) Record("选项已提交", runtime.ResolveText(runtime.Configuration.choices.Find(c => c.choiceId == id)?.textKey) + " [" + id + "]");
        }
        private void Update()
        {
            if (!Running) return;
            try
            {
                if (flow == null) { Finish("host-lost", "游戏宿主已关闭"); return; }
                Observe();
                if (runtime?.Controller?.IsEnded == true)
                {
                    string actual = runtime.Controller.SelectedEndingId;
                    Record("到达结局", actual + (string.IsNullOrEmpty(TargetEnding) || actual == TargetEnding ? "" : "（与目标不同）"));
                    ActualEnding = actual;
                    Finish("ending", actual); return;
                }
                if (flow.SessionState == GameSessionState.Menu && Time.unscaledTime - lastProgress > 5)
                { Finish("cancelled", "已返回主菜单"); return; }
                if (lastDay > MaximumDay) { Finish("unproven", "超过本轮日期上限，未证明目标可达；不据此认定不可达。"); return; }
                bool fastPresentation = FastDialogue && DialoguePanel.Active?.IsPlaying == true;
                if ((Paused && !singleStep) || !fastPresentation && Time.unscaledTime < nextStep) return;
                nextStep = Time.unscaledTime + Mathf.Clamp(StepSeconds, Unattended ? .02f : .3f, 10f);
                singleStep = false;
                if (Time.timeScale <= 0f || FreeCafeGMPanel.Instance?.IsVisible == true) return;
                // New-game loading awaits the real prologue's completion callback.
                // Do not deadlock that panel by treating Loading as an unconditional input lock.
                if (flow.SessionState == GameSessionState.Loading)
                {
                    var prologue = UIKit.GetPanel<OpeningProloguePanel>();
                    if (prologue?.IsPlaying == true) prologue.Continue();
                    else if (Time.unscaledTime - lastProgress > 60f)
                        RequireManual("loading-stalled", "加载超过60秒没有进展，请检查转场或场景加载错误。");
                    return;
                }
                if (pendingTutorialSkip)
                {
                    var days = flow.GetComponent<DayFlowController>();
                    if (days != null && !days.IsFirstSessionGuideActive) pendingTutorialSkip = false;
                    else if (days != null && days.DebugSkipTutorialToCafe(out string reason))
                    { pendingTutorialSkip = false; Record("跳过教程", reason); return; }
                }
                if (AssistCoffee && TryAssistedCoffee()) return;
                if (AdvancePresentation())
                {
                    if (!Paused && Time.unscaledTime - lastProgress > (Unattended ? 15f : 60f))
                        RequireManual("presentation-stalled", "演出超过60秒没有进展，请检查当前阅读、输入或演出条件，再继续。");
                    return;
                }
                FollowTodo();
                if (!Paused && Time.unscaledTime - lastProgress > (Unattended ? 15f : 45f))
                    RequireManual("stalled", "45秒没有实际进展；请检查当前待办或手动完成当前操作，再继续。");
            }
            catch (Exception error) { Record("异常", error.Message); Finish("error", error.ToString()); Debug.LogException(error, this); }
        }

        private bool AdvancePresentation()
        {
            var confirmation = UIKit.GetPanel<ConfirmationPanel>();
            if (Unattended && confirmation != null && confirmation.gameObject.activeInHierarchy)
            { if (confirmation.AutomationAcceptDayFlow()) Record("确认", "确认日程弹窗，走原正式回调。"); return true; }
            var dialogue = DialoguePanel.Active;
            if (dialogue != null && dialogue.IsPlaying)
            {
                if (dialogue.InputSuspended || dialogue.ReadingPaused || dialogue.IsHistoryOpen) return true;
                if (FastDialogue && !dialogue.IsChoosing)
                {
                    if (dialogue.FastForwardToChoiceOrComplete()) Record("跳过文本段", "直接推进到选项或段尾，仍等待原剧情提交。");
                    if (!dialogue.IsPlaying || !dialogue.IsChoosing) return true;
                }
                if (!dialogue.IsReady) return true;
                if (!dialogue.IsChoosing) { dialogue.Advance(); return true; }
                if (!AutoChoose) { RequireManual("choice", "请选择对话选项；选择后点击继续跟随。"); return true; }
                var options = dialogue.DisplayedOptions.Where(o => o.Enabled).ToArray();
                var definitions = runtime?.Configuration.choices.Where(c => options.Any(o => o.OptionId == c.choiceId)).ToArray();
                if (definitions != null && definitions.Length > 0
                    && plan.ChoiceOverrides.TryGetValue(definitions[0].groupKey, out string expected)
                    && !options.Any(o => o.OptionId == expected))
                {
                    RequireManual("planned-choice-locked:" + expected, "目标策略选项被锁定或缺失：" + expected + "。未自动改选其他路线；可人工检查后继续。");
                    return true;
                }
                var scores = definitions == null ? null : plan.ScoreChoices(runtime.Configuration, runtime.Controller, definitions);
                string id = scores?.OrderByDescending(s => s.Score).ThenBy(s => s.ChoiceId, StringComparer.Ordinal).FirstOrDefault()?.ChoiceId;
                var selected = options.FirstOrDefault(o => o.OptionId == id) ?? options.FirstOrDefault();
                if (selected == null) { RequireManual("choice-empty", "没有可用选项，请检查选项条件。"); return true; }
                // The readable mode pauses on the options; fast mode submits in this same step.
                string key = "choose:" + string.Join("|", options.Select(o => o.OptionId));
                if (manualKey != key) { manualKey = key; Record("准备选择", selected.Text + "；可选：" + string.Join(" / ", options.Select(o => o.Text))); if (!FastDialogue) return true; }
                if (dialogue.SelectOption(selected.OptionId)) { manualKey = null; Record("选择", selected.Text + " [" + selected.OptionId + "]"); }
                return true;
            }
            var prologue = UIKit.GetPanel<OpeningProloguePanel>();
            if (prologue?.IsPlaying == true) { prologue.Continue(); return true; }
            var letter = UIKit.GetPanel<FirstDayLetterPanel>();
            if (letter?.IsPlaying == true)
            {
                if (letter.Stage == FirstDayLetterPanel.LetterStage.Envelope) letter.OpenEnvelope();
                else if (letter.IsGenericLetter)
                {
                    if (Unattended) letter.AutomationReadToEnd();
                    else if (letter.CanFinishLetter) letter.FinishReadingLetter();
                    else if (letter.Stage == FirstDayLetterPanel.LetterStage.Reading)
                        RequireManual("read-letter", "请向下阅读信件，读完后继续跟随。");
                }
                else if (letter.Stage == FirstDayLetterPanel.LetterStage.AwaitingSignature)
                    RequireManual("letter", "请在来信中选择身世、属性和姓名并签名；现场测试保留你的真实建角选择。");
                return true;
            }
            var reading = UIKit.GetPanel<ReadingPanel>();
            if (reading?.IsPlaying == true) { reading.Advance(); return true; }
            var notice = UIKit.GetPanel<NoticePanel>();
            if (notice?.IsPlaying == true) { notice.Confirm(); return true; }
            var days = flow.GetComponent<DayFlowController>();
            if (days?.CurrentPresentation is DayFlowPresentationPanel panel && panel.IsPlaying)
            { panel.Continue(); return true; }
            var mapGuide = FindFirstObjectByType<MapUsageTutorial>();
            if (mapGuide?.IsRunning == true)
            { if (Unattended) { mapGuide.AutomationSkipTutorial(); Record("跳过教学", "跳过地图说明，不改剧情标记与行动点。"); }
                else RequireManual("map-guide", "请跟随地图教程选择地点并完成教学，然后继续跟随待办。"); return true; }
            return false;
        }

        private void FollowTodo()
        {
            var hud = GameplayHudPanel.Instance;
            var todo = hud?.Tasks?.CurrentGuidance;
            if (Unattended && todo?.optional == true)
                todo = hud.Tasks.Items.FirstOrDefault(i => !i.completed && i.available && !i.optional);
            if (todo == null) return;
            if (runtime?.Controller?.IsBusy == true || runtime?.Actions?.IsExecuting == true
                || GameplayHudPanel.Instance.IsDetailsOpen) return;
            if (todo.id != lastTodo)
            { lastTodo = todo.id; manualKey = null; hud.Tasks.SetTracked(todo.id); Record("当前待办", todo.name + "：" + todo.currentTask); }
            if (!todo.available) { RequireManual(todo.id, todo.blockedReason ?? todo.status); return; }
            if (todo.optional) { RequireManual(todo.id, "当前只有自选事项。请检查休息入口或选择希望体验的内容。"); return; }
            if (!string.IsNullOrEmpty(todo.actionKey))
            {
                var details = FindFirstObjectByType<MapActionDetailsView>();
                if (manualKey != "navigate:" + todo.id)
                {
                    if (hud.TryNavigateToTodo(todo.id)) { manualKey = "navigate:" + todo.id; Record("前往行动", todo.location + " · " + todo.name); }
                    return;
                }
                if (details != null && details.isActiveAndEnabled && details.SelectAction(todo.actionKey)
                    && details.ResumeAction(todo.actionKey))
                { Record("执行行动", todo.name); manualKey = null; }
                return;
            }
            var cafe = FindFirstObjectByType<FreeCafeRuntime>();
            if (todo.id == "guide.cafe.tutorial" || todo.id.StartsWith("guide.cafe.order.", StringComparison.Ordinal))
            {
                if (todo.id.StartsWith("guide.cafe.order.", StringComparison.Ordinal) && cafe != null && cafe.World.State.currentArea != AreaId.Counter)
                { if (hud.TryNavigateToTodo(todo.id)) Record("进入制作台", todo.name); return; }
                RequireManual(todo.id, todo.currentTask + " 完成真实交付后，点击继续跟随。"); return;
            }
            if (todo.id == "guide.map.cafe" || todo.id == "guide.travel.cafe")
            { if (hud.TryNavigateToCafe()) Record("前往咖啡馆", "通过真实地图入口进入咖啡馆。"); return; }
            if (todo.id == "guide.cafe.wait")
            {
                if (Unattended && cafe != null)
                {
                    if (cafe.World.State.todayArrivals < cafe.World.State.todayCustomerBudget)
                    { cafe.World.Tick(cafe.World.Catalog.customerInterval); Record("加速候客", "推进经营时钟，保留原始耐心与客流结算。"); }
                    else
                    {
                        if (cafe.World.State.currentArea != AreaId.Cafe) cafe.Enter(AreaId.Cafe);
                        cafe.RequestBusinessEnd();
                        if (!cafe.World.State.open) Record("打烊", "完成当日客流，通过正式结算入口打烊。");
                    }
                }
                return;
            }
            if (todo.id == "guide.cafe.open" || todo.id == "guide.cafe.close")
            {
                if (cafe == null) { hud.TryNavigateToCafe(); return; }
                if (cafe.World.State.currentArea != AreaId.Cafe) { hud.TryNavigateToTodo(todo.id); return; }
                if (todo.id == "guide.cafe.open") cafe.RequestBusinessStart(); else cafe.RequestBusinessEnd();
                // Do not count a rejected command as progress.
                if ((todo.id == "guide.cafe.open") == cafe.World.State.open) Record("营业操作", todo.name);
                return;
            }
            if (todo.id == GameplayTodoProjection.RestTodoId || todo.id == "guide.cafe.rest")
            {
                if (cafe == null || flow.IsMapOpen || SceneManager.GetActiveScene().name != "CoffeeShop")
                { hud.TryNavigateToCafeForRest(); return; }
                if (cafe.World.State.currentArea != AreaId.Basement)
                { if (cafe.CanEnterArea(AreaId.Basement)) { cafe.Enter(AreaId.Basement); Record("前往宿舍", "进入三楼，准备点击床休息。"); } return; }
                var days = flow.GetComponent<DayFlowController>();
                days?.EndDayFromDormitory(); return; // Official gates and real day flow own date advancement.
            }
            if (!string.IsNullOrEmpty(todo.navigationLabel)) hud.TryNavigateToTodo(todo.id);
            else RequireManual(todo.id, todo.currentTask);
        }
        private bool TryAssistedCoffee()
        {
            var cafe = FindFirstObjectByType<FreeCafeRuntime>();
            if (cafe?.World == null || cafe.World.HasTutorialCustomer || !cafe.World.ProductionEnabled) return false;
            var world = cafe.World;
            if (world.State.requests.Count == 0 || !world.HasNarrativeService && runtime?.Controller?.IsBusy == true) return false;
            if (flow.IsMapOpen) return false;
            if (world.State.currentArea != AreaId.Counter)
            {
                if (cafe.CanEnterArea(AreaId.Counter)) { cafe.Enter(AreaId.Counter); Record("辅助导航", "进入真实制作台；下一步辅助交付。"); }
                return true;
            }
            UsedAssistance = true;
            world.AutomationUnlockAndRefill();
            int beforeMoney = world.State.money, beforeReputation = world.State.reputation, beforeServed = world.State.served;
            var result = world.AutomationServePendingOrders(2);
            Record("辅助制作", "交付 " + result.Delivered + " 杯；资金 " + beforeMoney + "→" + world.State.money
                + "；声望 " + beforeReputation + "→" + world.State.reputation + "；服务数 " + beforeServed + "→" + world.State.served
                + "；完成经过真实交付回执（制作与补料由工具代办）。");
            if (!string.IsNullOrEmpty(result.StopReason)) RequireManual("coffee", result.StopReason);
            return true;
        }

        public void MarkUnproven(string reason) { if (Running) Finish("unproven", reason); }

        private void RequireManual(string key, string reason)
        {
            if (Unattended) { Record("自动测试阻塞", key + "：" + reason); Finish("blocked", key + "：" + reason); return; }
            if (!Paused || manualKey != key) { manualKey = key; Record("等待操作", reason); }
            Paused = true;
        }
        public void TogglePause() { if (!Running) return; Paused = !Paused; manualKey = null; lastProgress = Time.unscaledTime; }
        public void StepOnce() { if (!Running) return; Paused = true; singleStep = true; nextStep = 0; lastProgress = Time.unscaledTime; }
        public void Stop() { if (Running) Finish("stopped", "用户停止，保留当前游戏现场。"); }
        private void Finish(string outcome, string reason)
        {
            Outcome = outcome;
            Running = false; Paused = false;
            saveLease?.Dispose(); saveLease = null;
            if (observedController != null)
            {
                observedController.EventCompleted -= OnEventCompleted;
                observedController.EventFailed -= OnEventFailed;
                observedController.BarChanged -= OnBarChanged;
            }
            if (observedPresenter != null) observedPresenter.SkipDialogueText = previousSkipText;
            observedPresenter = null;
            observedController = null; runtime = null; Status = reason;
            try { Save(outcome, reason); readableLog?.WriteLine("\n## 本轮结果\n\n" + outcome + "：" + reason); }
            catch (IOException error) { Debug.LogWarning("现场报告保存失败：" + error.Message, this); }
            finally { readableLog?.Dispose(); readableLog = null; }
        }
        private void Save(string outcome, string reason)
        {
            if (string.IsNullOrEmpty(OutputPath)) return;
            File.WriteAllText(OutputPath, JsonUtility.ToJson(new Report { target = TargetEnding, outcome = outcome, reason = reason, entries = entries,
                assisted = usedStartupBypass || UsedAssistance, startup = startupMode }, true));
        }
        private void OnDestroy()
        {
            try { Stop(); } finally { if (overlayFont != null) Destroy(overlayFont); }
        }
        private void OnDisable() => Stop();
        private void OnGUI()
        {
            if (!ShowOverlay || entries.Count == 0) return;
            float width = Mathf.Min(340, Screen.width * .34f);
            if (width < 180 || Screen.height < 240) return;
            if (textStyle == null)
            {
                overlayFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 14);
                textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14, richText = false, font = overlayFont };
            }
            Font previousFont = GUI.skin.font;
            if (overlayFont != null) GUI.skin.font = overlayFont;
            var area = new Rect(Screen.width - width - 12, 12, width, Mathf.Min(Screen.height - 24, collapsed ? 80 : Screen.height * .7f));
            Color previousColor = GUI.color;
            GUI.color = new Color(.075f, .06f, .045f, .94f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.BeginArea(area, GUI.skin.box);
            if (GUILayout.Button(collapsed ? "展开现场操作记录" : "现场跟随待办 · 收起")) collapsed = !collapsed;
            if (!collapsed)
            {
                GUILayout.Label((Running ? Paused ? "已暂停 / 可手动接管" : "正在跟随" : "已结束") + " · " + lastScene + " · " + lastArea, textStyle);
                GUILayout.Label("目标：" + (TargetEnding ?? "自由跟随") + " · 第" + lastDay + "天", textStyle);
                GUILayout.Label(UsedAssistance || usedStartupBypass ? "辅助轨迹：不能证明严格开局可达" : "严格轨迹：不注入属性/资金/剧情标记", textStyle);
                GUILayout.Label(Status, textStyle);
                if (Running)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(Paused ? "继续跟随" : "暂停")) TogglePause();
                    if (GUILayout.Button("单步")) StepOnce();
                    if (GUILayout.Button("停止")) Stop();
                    GUILayout.EndHorizontal();
                    AutoChoose = GUILayout.Toggle(AutoChoose, "自动选择对话选项");
                    FastDialogue = GUILayout.Toggle(FastDialogue, "直接跳到选项并立即选择");
                    GUILayout.Label("每步间隔 " + StepSeconds.ToString("0.0") + " 秒", textStyle);
                    StepSeconds = GUILayout.HorizontalSlider(StepSeconds, .5f, 5f);
                }
                scroll = GUILayout.BeginScrollView(scroll);
                foreach (var entry in entries.Skip(Math.Max(0, entries.Count - 150)))
                    GUILayout.Label("第" + entry.day + "天 · " + entry.scene + " / " + entry.location + "\n" + entry.kind + "：" + entry.text, textStyle);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            GUI.skin.font = previousFont;
        }
    }
}
