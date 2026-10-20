using System;
using Spotlight.Narrative;
using Spotlight.GameFlow;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>Registration-only GM integration; runtime work lives in GameFlow's isolated batch host.</summary>
    public sealed class NarrativeAutoGM : MonoBehaviour
    {
        private FreeCafeGMPanel shell;
        private NarrativeAutoRunner runner;
        private bool waitForReview = true, scenarios = true;
        private void Awake()
        {
            shell = GetComponent<FreeCafeGMPanel>();
            runner = GetComponent<NarrativeAutoRunner>();
            if (runner == null) runner = gameObject.AddComponent<NarrativeAutoRunner>();
            shell.RegisterCategory("剧情自动测试", Build);
        }
        private void Build(FreeCafeGMPanel panel)
        {
            panel.AddAction("开始全结局后台测试（3种属性策略）", () => BeginBatch(3),
                "逐个结局倾向预设；后台经营、制作交付、阶段和选项结算。每轮到实际结局记录JSON/CSV/日志与真实结局面板截图。不改正式存档。", false);
            panel.AddAction("快速回归（每结局1种策略）", () => BeginBatch(1), "适合先检查管线与阻塞。", false);
            panel.AddAction("结局后行为：" + (waitForReview ? "停下人工验收" : "截图后自动下一轮"), () =>
            { waitForReview = !waitForReview; runner.WaitForReview = waitForReview; panel.SetExternalStatus(waitForReview ? "结局后停止，等待人工验收。" : "结局截图记录后自动继续下一轮。"); },
                "无论哪种模式，都先结束当前演算、写记录和截图，再决定是否继续。", false);
            panel.AddAction("补充历史起点场景：" + (scenarios ? "开启" : "关闭"), () =>
            { scenarios = !scenarios; panel.SetExternalStatus(scenarios ? "已启用明示历史起点补充测试。" : "仅执行从第1天开始的辅助策略。"); },
                "在从第1天的策略测试之后追加结局尾段历史预设，扩大演出/结算覆盖；单独标为历史注入，不冒充开局可达。不会解除作者blockedBy。", false);
            panel.AddAction("暂停 / 继续", () => { runner.TogglePause(); panel.SetExternalStatus(runner.Status); }, "不丢失当前独立测试会话。", false);
            panel.AddAction("验收通过并继续", () => { runner.ReviewAndContinue(true); panel.SetVisible(false); }, "只影响测试报告的人工验收标记。", false);
            panel.AddAction("标记问题并继续", () => { runner.ReviewAndContinue(false); panel.SetVisible(false); }, "保留当前截图与日志。", false);
            panel.AddAction("停止批量测试", () => { runner.Stop(); panel.SetExternalStatus(runner.Status); }, "释放工具自己的暂停，不恢复/覆盖正式存档。", false);
            panel.AddAction("打开结果目录", () =>
            { if (!string.IsNullOrEmpty(runner.OutputDirectory)) Application.OpenURL(new Uri(runner.OutputDirectory).AbsoluteUri); }, runner.OutputDirectory ?? "尚无报告", false);
            panel.AddAction("显示 / 隐藏实时演算窗", () => runner.ShowOverlay = !runner.ShowOverlay, "运行与日志不依赖面板是否可见。", false);
            try
            {
                var config = LubanNarrativeLoader.Load();
                foreach (var ending in config.endings)
                {
                    if (!ending.enabled) continue;
                    string id = ending.endingId;
                    string title = config.texts.Find(t => t.key == ending.titleText)?.content ?? id;
                    panel.AddAction("只测：" + title, () => BeginBatch(3, id), id, false);
                }
            }
            catch (Exception error) { panel.SetExternalStatus("配置加载失败：" + error.Message); }
            panel.SetExternalStatus(runner.Status);
        }
        private void BeginBatch(int variants, string ending = null)
        {
            try
            {
                runner.WaitForReview = waitForReview;
                runner.StartBatch(variants, scenarios, ending);
                shell.SetVisible(false);
            }
            catch (Exception error) { shell.SetExternalStatus(error.Message); Debug.LogWarning("[NarrativeAuto] " + error.Message); }
        }
    }
}
