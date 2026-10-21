using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Visible scene-owned campaign tests. Never uses historical seeds or the isolated background host.</summary>
    public sealed class NarrativeReachabilityRunner : MonoBehaviour
    {
        [Serializable] public sealed class Row
        {
            public string target, title, state = "待测", actual, reason, trace;
            public int day;
            public bool assisted;
        }
        [Serializable] private sealed class Manifest
        {
            public string protocol = "visible-new-game-v1", configurationHash;
            public bool finished;
            public List<Row> rows;
            public List<NarrativeReachabilityAudit.Finding> findings;
        }
        public bool Running { get; private set; }
        public bool WaitingForReview { get; private set; }
        public bool AssistedMode { get; private set; }
        public string ReportPath { get; private set; }
        public IReadOnlyList<Row> Results => rows;
        public string Status { get; private set; } = "未开始";
        private readonly List<Row> rows = new List<Row>();
        private List<NarrativeReachabilityAudit.Finding> findings;
        private NarrativeWalkthroughRunner walker;
        private int index;
        private bool starting;
        private float nextStart, roundStartedAt, lastReportAt;
        private string hash;
        private Vector2 scroll;
        private Font font;
        private GUIStyle label;
        private bool visible;

        public bool FastContinuous { get; private set; }
        private float roundFinishedAt;
        private int repetitionCount = 1;
        public void BeginFast(int repetitions = 2, string onlyEnding = null)
        {
            FastContinuous = true; repetitionCount = Mathf.Clamp(repetitions, 1, 10);
            Begin(true, onlyEnding);
        }
        public void Begin(bool assisted = false, string onlyEnding = null)
        {
            if (Running || NarrativeAutoRunner.IsActive || NarrativeAutoRunner.IsWalkthroughActive)
                throw new InvalidOperationException("请先停止正在运行的测试。");
            var flow = FindFirstObjectByType<GameFlowBootstrap>();
            if (flow == null || flow.SessionState != GameSessionState.Menu)
                throw new InvalidOperationException("请先返回主菜单。每轮必须从新游戏开始，不能拿当前存档当开局证据。");
            var config = LubanNarrativeLoader.Load();
            rows.Clear();
            for (int repeat = 0; repeat < (FastContinuous ? repetitionCount : 1); repeat++)
            foreach (var ending in config.endings.Where(e => e.enabled && (!FastContinuous || e.endingId != "arrival.ending.if0") && (onlyEnding == null || e.endingId == onlyEnding)))
                rows.Add(new Row { target = ending.endingId, title = ending.endingId + " · " + (config.texts.Find(t => t.key == ending.titleText)?.content ?? ending.endingId) + " · 轮" + (repeat + 1) });
            if (rows.Count == 0) throw new InvalidOperationException("没有匹配的启用结局。");
            findings = NarrativeReachabilityAudit.Inspect(config);
            using (var sha = System.Security.Cryptography.SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",
                    Resources.LoadAll<TextAsset>("Luban").OrderBy(a => a.name).Select(a => a.name + a.text))))).Replace("-", "");
            ReportPath = Path.Combine(Application.persistentDataPath, "NarrativeReachability", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), "manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            walker = GetComponent<NarrativeWalkthroughRunner>() ?? gameObject.AddComponent<NarrativeWalkthroughRunner>();
            AssistedMode = assisted; index = 0; Running = true; visible = true; WaitingForReview = false; starting = true;
            nextStart = Time.unscaledTime + .5f;
            Save();
        }
        private void Update()
        {
            if (!Running) return;
            try
            {
                if (WaitingForReview)
                {
                    if (FastContinuous && Time.unscaledTime - roundFinishedAt > 1.5f) ContinueNext();
                    return;
                }
                if (starting)
                {
                    var flow = FindFirstObjectByType<GameFlowBootstrap>();
                    if (Time.unscaledTime < nextStart || flow == null || flow.SessionState != GameSessionState.Menu) return;
                    walker.SkipOpeningAndTutorials = AssistedMode;
                    walker.AssistCoffee = AssistedMode;
                    walker.Unattended = FastContinuous;
                    walker.ReportDirectory = Path.GetDirectoryName(ReportPath);
                    walker.FastDialogue = FastContinuous; walker.AutoChoose = true; walker.ShowOverlay = true;
                    walker.StepSeconds = FastContinuous ? .04f : 1f; walker.MaximumDay = 60;
                    walker.Begin(rows[index].target);
                    roundStartedAt = lastReportAt = Time.unscaledTime;
                    rows[index].assisted = AssistedMode;
                    rows[index].trace = walker.OutputPath;
                    rows[index].state = "测试中"; starting = false;
                    Status = "第 " + (index + 1) + "/" + rows.Count + " 轮：" + rows[index].title;
                    Save(); return;
                }
                rows[index].day = walker.CurrentDay;
                if (walker.Running)
                {
                    if (FastContinuous && Time.unscaledTime - roundStartedAt > 480f)
                        walker.MarkUnproven("单轮超过8分钟安全上限：" + walker.Status);
                    if (Time.unscaledTime - lastReportAt > 2f) { lastReportAt = Time.unscaledTime; Save(); }
                    return;
                }
                var row = rows[index];
                row.actual = walker.ActualEnding; row.reason = walker.Status; row.trace = walker.OutputPath;
                row.assisted = AssistedMode || walker.UsedAssistance;
                row.state = Classify(row.target, row.actual, walker.Outcome, row.assisted);
                if (FastContinuous) ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(ReportPath), "round-" + (index + 1).ToString("00") + ".png"));
                roundFinishedAt = Time.unscaledTime;
                WaitingForReview = true; Status = row.title + "：" + row.state + (FastContinuous ? "。自动记录并开始下一轮。" : "。保留Game现场，确认后再进行下一轮。");
                Save();
            }
            catch (Exception error)
            {
                walker?.Stop(); rows[index].state = "测试错误"; rows[index].reason = error.ToString();
                roundFinishedAt = Time.unscaledTime;
                WaitingForReview = true; starting = false; Status = error.Message; Save();
            }
        }
        public static string Classify(string target, string actual, string outcome, bool assisted)
        {
            if (outcome == "ending" && actual == target) return assisted ? "辅助到达（非严格证明）" : "已见证开局可达";
            if (outcome == "ending") return "到达其他结局（目标未证明）";
            return "未证明（阻塞/停止/超限）";
        }
        public void ContinueNext()
        {
            if (!Running || !WaitingForReview) return;
            if (index + 1 >= rows.Count) { Running = false; WaitingForReview = false; Status = "测试完成；未证明不等于不可达。"; Save(); return; }
            var flow = FindFirstObjectByType<GameFlowBootstrap>();
            using (NarrativeAutoRunner.AcquireWalkthrough())
                if (flow == null || !flow.ReturnToTitleForReachabilityTest()) { Status = "等待宿主结束转场后再继续。"; return; }
            index++; WaitingForReview = false; starting = true; nextStart = Time.unscaledTime + 1;
            Save();
        }
        public void Stop()
        {
            if (!Running) return;
            walker?.Stop();
            if (!WaitingForReview) { rows[index].state = "未证明（用户停止）"; rows[index].trace = walker?.OutputPath; }
            Running = false; WaitingForReview = false; Status = "已停止，保留现场与报告。"; Save();
        }
        private void Save()
        {
            File.WriteAllText(ReportPath, JsonUtility.ToJson(new Manifest {
                configurationHash = hash, finished = !Running, rows = rows, findings = findings }, true));
            var text = new System.Text.StringBuilder("# 前台全结局自动测试报告\n\n");
            text.AppendLine("模式：" + (FastContinuous ? "高速无人值守，跳过教程与IF0；真实剧情/交付结算" : "现场测试"));
            text.AppendLine("\n配置指纹：" + hash + "\n\n状态：" + Status + "\n");
            foreach (var row in rows)
            {
                text.AppendLine("## " + row.title + "\n\n- 状态：" + row.state + "\n- 日期：第" + row.day + "天\n- 实际结局：" + (row.actual ?? "尚未到达") + "\n- 原因：" + row.reason);
                if (!string.IsNullOrEmpty(row.trace)) text.AppendLine("- [逐步操作记录](" + Path.GetFileName(Path.ChangeExtension(row.trace, ".md")) + ")");
                text.AppendLine();
            }
            text.AppendLine("## 配置风险（不替代实际运行证据）\n");
            foreach (var f in findings) text.AppendLine("- " + f.owner + " / " + f.key + "：" + f.reason);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(ReportPath), "测试报告.md"), text.ToString());
        }
        private void OnDisable() => Stop();
        private void OnDestroy() { Stop(); if (font != null) Destroy(font); }
        private void OnGUI()
        {
            if (!visible) return;
            if (label == null) { font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 14); label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true }; }
            var old = GUI.skin.font; GUI.skin.font = font;
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(370, Screen.width * .35f), Mathf.Min(620, Screen.height - 24)), GUI.skin.box);
            GUILayout.Label("全结局可达性 · Game现场", label);
            GUILayout.Label(FastContinuous ? "高速无人值守 · 跳教程/IF0 · 自动制作阅读 · 自动重开" : AssistedMode ? "辅助模式：跳教程、免费原料与理想咖啡；不算严格可达证明" : "严格模式：完整新游戏；教程/制作可人工接管；不改属性、资金或剧情标记", label);
            GUILayout.Label(Status, label);
            if (Running)
            {
                if (WaitingForReview) { if (GUILayout.Button(index + 1 == rows.Count ? "完成测试" : "确认现场，下一结局（不保存重开）")) ContinueNext(); }
                else if (walker != null && walker.Running)
                {
                    GUILayout.Label("第" + walker.CurrentDay + "天 · " + (walker.Paused ? "等待人工/已暂停" : "自动推进"), label);
                    if (GUILayout.Button(walker.Paused ? "继续" : "暂停")) walker.TogglePause();
                    if (GUILayout.Button("单步")) walker.StepOnce();
                    if (GUILayout.Button("本轮标记未证明，保留现场")) walker.MarkUnproven("用户标记阻塞：" + walker.Status);
                }
                if (GUILayout.Button("停止全部测试")) Stop();
            }
            if (GUILayout.Button("打开报告目录")) Application.OpenURL(new Uri(Path.GetDirectoryName(ReportPath)).AbsoluteUri);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var row in rows) GUILayout.Label(row.title + "：" + row.state + " · D" + row.day, label);
            GUILayout.Label("配置风险（不是穷举不可达证明）：", label);
            foreach (var f in findings) GUILayout.Label(f.owner + "：" + f.reason, label);
            GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.skin.font = old;
        }
    }
}
