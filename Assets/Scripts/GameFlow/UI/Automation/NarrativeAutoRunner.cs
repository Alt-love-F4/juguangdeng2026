using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;
using YokiFrame;

namespace Spotlight.GameFlow
{
    /// <summary>In-game batch host. All simulation is isolated; only accepted endings open a real ResultPanel.</summary>
    public sealed class NarrativeAutoRunner : MonoBehaviour
    {
        public const string PanelTag = "narrative-auto-ending-review";
        public static bool IsActive { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsActive = false;
        public bool Running { get; private set; }
        public bool Paused { get; private set; }
        public bool WaitForReview { get; set; } = true;
        public bool IsAwaitingReview { get; private set; }
        public bool IsCapturing { get; private set; }
        public bool ShowOverlay { get; set; } = true;
        public int StepsPerFrame { get; set; } = 32;
        public int CompletedCount => results.Count;
        public int TotalCount { get; private set; }
        public string OutputDirectory { get; private set; }
        public string Status { get; private set; } = "未开始";
        public NarrativeAutoSession Current { get; private set; }
        public IReadOnlyList<NarrativeAutoRunResult> Results => results;
        public event Action Changed;
        private readonly Queue<NarrativeAutoPreset> pending = new Queue<NarrativeAutoPreset>();
        private readonly List<NarrativeAutoRunResult> results = new List<NarrativeAutoRunResult>();
        private NarrativeConfiguration configuration;
        private CafeCatalog catalog;
        private ResultPanel endingPanel;
        private IDisposable worldPause;
        private float previousScale;
        private string configurationHash;
        private bool handled, disposed;
        private Coroutine capture;
        private Vector2 scroll;
        private GUIStyle normal;

        [Serializable] private sealed class Manifest
        {
            public string version = "1", unityVersion, outputDirectory;
            public bool finished;
            public List<NarrativeAutoRunResult> runs;
        }

        public void StartBatch(int variants = 3, bool historicalScenarios = true, string onlyEnding = null)
        {
            if (IsActive || Running || IsCapturing) throw new InvalidOperationException("请先停止或完成当前自动测试。");
            // Never steal an active formal presentation or a ResultPanel owned by another workflow.
            var formal = NarrativeRuntime.Instance;
            if (formal?.Controller?.IsBusy == true || DialoguePanel.IsAnyPlaying || UIKit.GetPanel<ResultPanel>() != null)
                throw new InvalidOperationException("请先完成当前剧情/结局面板，再开始后台测试；推荐在主菜单启动。");
            configuration = LubanNarrativeLoader.Load();
            var source = Resources.Load<TextAsset>("FreeCafe/catalog");
            if (source == null) throw new InvalidOperationException("缺少 FreeCafe/catalog。");
            catalog = JsonUtility.FromJson<CafeCatalog>(source.text);
            var plans = NarrativeAutoPresets.Create(configuration, variants, historicalScenarios)
                .Where(p => string.IsNullOrWhiteSpace(onlyEnding) || p.targetEndingId == onlyEnding).ToArray();
            if (plans.Length == 0) throw new InvalidOperationException("没有匹配的启用结局。");
            OutputDirectory = Path.Combine(Application.persistentDataPath, "NarrativeAutoTests", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(OutputDirectory);
            configurationHash = ComputeConfigurationHash(source.text);
            pending.Clear(); results.Clear();
            foreach (var plan in plans) pending.Enqueue(plan);
            TotalCount = pending.Count; Running = IsActive = true; Paused = false; IsAwaitingReview = false; disposed = false;
            previousScale = Time.timeScale; Time.timeScale = 0f;
            worldPause = PresentationPauseRegistry.Acquire("Narrative automated testing - isolated background host");
            Status = "后台测试已启动；正式会话暂停，存档不变。";
            SaveManifest(); StartNext();
        }

        private static string ComputeConfigurationHash(string cafeJson)
        {
            var text = new StringBuilder(cafeJson);
            foreach (var asset in Resources.LoadAll<TextAsset>("Luban").OrderBy(a => a.name, StringComparer.Ordinal))
                text.Append(asset.name).Append('\n').Append(asset.text).Append('\n');
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private void StartNext()
        {
            if (!Running || IsCapturing) return;
            CloseOwnedPanel(); Current?.Dispose(); Current = null; handled = false; IsAwaitingReview = false;
            if (pending.Count == 0) { FinishBatch(); return; }
            var preset = pending.Dequeue();
            try
            {
                // New configuration per run: no mutation, counters or caching can leak between trials.
                Current = new NarrativeAutoSession(LubanNarrativeLoader.Load(), catalog, preset);
                Current.Result.configurationHash = configurationHash;
                Status = "演算 " + preset.id; Changed?.Invoke();
            }
            catch (Exception error)
            {
                var result = new NarrativeAutoRunResult { presetId = preset.id, targetEndingId = preset.targetEndingId,
                    historicalScenario = preset.historicalScenario, status = "initialization-error", reason = error.ToString(),
                    finishedUtc = DateTime.UtcNow.ToString("O"), configurationHash = configurationHash };
                results.Add(result); SaveRun(result); SaveManifest();
                Status = "初始化失败，已记录：" + error.Message; handled = true;
            }
        }

        private void Update()
        {
            if (!Running || Paused || IsAwaitingReview || IsCapturing) return;
            if (Current == null) { StartNext(); return; }
            if (!Current.Finished) Current.Tick(StepsPerFrame);
            if (!Current.Finished || handled) return;
            handled = true;
            var result = Current.Result;
            results.Add(result); SaveRun(result); SaveManifest();
            if (result.status == "ending") capture = StartCoroutine(CaptureEnding(result));
            else { Status = result.presetId + " → " + result.status + ": " + result.reason; StartNext(); }
        }

        private IEnumerator CaptureEnding(NarrativeAutoRunResult result)
        {
            IsCapturing = true;
#if UNITY_EDITOR
            // ScreenCapture needs an actual rendered Game view in the Editor, even when the
            // verifier is advanced from a background tool while Scene/Console was focused.
            UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game");
#endif
            yield return null;
            yield return null;
            bool hasPanel = false;
            try
            {
                var ending = configuration.endings.Single(e => e.endingId == result.actualEndingId);
                endingPanel = UIKit.OpenPanel<ResultPanel>(UILevel.Pop,
                    NarrativeEndingPresentation.Create(null, ending, Current.ResolveText, () => { }, "自动测试 · 等待验收"),
                    tag: PanelTag, cachePolicy: PanelCachePolicy.Transient);
                hasPanel = endingPanel != null;
            }
            catch (Exception error) { result.screenshotError = "结局面板加载失败：" + error.Message; }
            if (hasPanel)
            {
                float deadline = Time.realtimeSinceStartup + 15f;
                while (endingPanel != null && endingPanel.State != PanelState.Open && Time.realtimeSinceStartup < deadline) yield return null;
                if (endingPanel == null || endingPanel.State != PanelState.Open)
                    result.screenshotError = "真实结局面板未在15秒内就绪，未伪造截图。";
                else
                {
                    // Reflow only after the panel is visible; its first OnOpen can run before
                    // the embedded TMP text has a valid page range on a freshly created canvas.
                    if (endingPanel.EndingDialogue != null)
                        endingPanel.EndingDialogue.ShowEndingSettlement(endingPanel.DisplayedMessage, () => { });
                    yield return null;
                    if (endingPanel.EndingDialogue?.IsSettlementTyping == true) endingPanel.ConfirmResult();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForSecondsRealtime(.15f);
                    var path = Path.Combine(OutputDirectory, RunFileStem(result) + "-ending.png");
                    bool requested = false;
                    try
                    {
                        // Queue capture at the next rendered frame instead of reading the previous
                        // backbuffer from Update (which may still contain the preceding ending).
                        ScreenCapture.CaptureScreenshot(path);
                        requested = true;
                    }
                    catch (Exception error) { result.screenshotError = error.Message; }
                    float captureDeadline = Time.realtimeSinceStartup + 8f;
                    while (requested && (!File.Exists(path) || new FileInfo(path).Length == 0)
                        && Time.realtimeSinceStartup < captureDeadline) yield return null;
                    if (requested)
                    {
                        if (File.Exists(path) && new FileInfo(path).Length > 0) result.screenshot = path;
                        else
                        {
                            // A queued Editor capture may be lost during layout/focus changes.
                            // Retry against the now-rendered backbuffer before reporting failure.
                            try
                            {
                                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                                if (texture == null) throw new InvalidOperationException("Game画面不可用。");
                                try { File.WriteAllBytes(path, texture.EncodeToPNG()); } finally { Destroy(texture); }
                                result.screenshot = path;
                            }
                            catch (Exception error) { result.screenshotError = "渲染截图与重试均失败：" + error.Message; }
                        }
                    }
                }
            }
            IsCapturing = false; capture = null;
            SaveRun(result); SaveManifest();
            Status = "已记录结局 " + result.actualEndingId + (result.screenshot == null ? "（截图失败，见报告）" : "，截图已保存");
            Changed?.Invoke();
            if (WaitForReview) IsAwaitingReview = true;
            else StartNext();
        }

        private string RunFileStem(NarrativeAutoRunResult result)
        {
            int index = results.IndexOf(result) + 1;
            string name = result.presetId;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return index.ToString("000") + "-" + name.Replace('/', '_').Replace('\\', '_');
        }
        private void SaveRun(NarrativeAutoRunResult result)
        {
            if (string.IsNullOrEmpty(OutputDirectory)) return;
            File.WriteAllText(Path.Combine(OutputDirectory, RunFileStem(result) + ".json"), JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(OutputDirectory, RunFileStem(result) + ".log"), result.log, new UTF8Encoding(false));
        }
        private void SaveManifest()
        {
            if (string.IsNullOrEmpty(OutputDirectory)) return;
            File.WriteAllText(Path.Combine(OutputDirectory, "manifest.json"), JsonUtility.ToJson(new Manifest {
                unityVersion = Application.unityVersion, outputDirectory = OutputDirectory, finished = !Running, runs = results }, true), new UTF8Encoding(false));
            var lines = new List<string> { "preset,target,actual,status,evidence,day,cups,review,screenshot,reason" };
            lines.AddRange(results.Select(r => string.Join(",", new[] { r.presetId, r.targetEndingId, r.actualEndingId, r.status, r.evidence,
                r.day.ToString(), r.cupsDelivered.ToString(), r.review, r.screenshot, r.reason }.Select(Csv))));
            File.WriteAllLines(Path.Combine(OutputDirectory, "summary.csv"), lines, new UTF8Encoding(true));
        }
        private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        public void ReviewAndContinue(bool accepted)
        {
            if (!IsAwaitingReview || IsCapturing) return;
            var result = results.Last(); result.review = accepted ? "accepted" : "needs-fix";
            SaveRun(result); SaveManifest(); IsAwaitingReview = false; StartNext();
        }
        public void ContinueWithoutReview()
        { if (IsAwaitingReview && !IsCapturing) { IsAwaitingReview = false; StartNext(); } }
        public void TogglePause() { if (Running && !IsCapturing) Paused = !Paused; }
        public void Stop()
        {
            if (!Running) return;
            if (capture != null) { StopCoroutine(capture); capture = null; IsCapturing = false; }
            if (Current != null && !Current.Finished)
            { Current.Stop("cancelled", "用户停止"); results.Add(Current.Result); SaveRun(Current.Result); }
            FinishBatch();
        }
        private void FinishBatch()
        {
            Running = IsActive = false; Paused = IsAwaitingReview = IsCapturing = false; pending.Clear();
            Current?.Dispose(); Current = null; CloseOwnedPanel();
            worldPause?.Dispose(); worldPause = null; Time.timeScale = previousScale;
            Status = "已结束：" + results.Count + "轮，实际结局" + results.Where(r => r.status == "ending").Select(r => r.actualEndingId).Distinct().Count()
                + "种。报告：" + OutputDirectory;
            SaveManifest(); Changed?.Invoke();
        }
        private void CloseOwnedPanel()
        { if (endingPanel != null) { UIKit.ClosePanelsByTag(PanelTag); endingPanel = null; } }
        private void OnDestroy()
        {
            if (disposed) return; disposed = true;
            try { Stop(); } finally { Current?.Dispose(); worldPause?.Dispose(); }
        }

        private void OnGUI()
        {
            if (!ShowOverlay || IsCapturing || (!Running && results.Count == 0)) return;
            normal ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
            if (Screen.width < 600 || Screen.height < 300) return;
            GUILayout.BeginArea(new Rect(12, 12, Math.Min(480, Screen.width - 24), Math.Min(650, Screen.height - 24)), GUI.skin.box);
            GUILayout.Label("剧情后台自动测试 · " + CompletedCount + "/" + TotalCount, normal);
            GUILayout.Label(Status, normal);
            if (Current != null)
            {
                GUILayout.Label(Current.Preset.id + "\n第" + Current.Day + "天 / " + Current.Phase + " / 资金 " + Current.World.State.money
                    + " / 制作交付 " + Current.Result.cupsDelivered, normal);
                GUILayout.Label("性质：" + Current.Result.evidence + "\n不写正式存档；不认证玩家操作与数值平衡。", normal);
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(180));
                foreach (string line in Current.Result.log.Skip(Math.Max(0, Current.Result.log.Count - 12))) GUILayout.Label(line, normal);
                GUILayout.EndScrollView();
            }
            if (IsAwaitingReview)
            {
                if (GUILayout.Button("验收通过，继续下一轮")) ReviewAndContinue(true);
                if (GUILayout.Button("标记问题，继续下一轮")) ReviewAndContinue(false);
                if (GUILayout.Button("暂不验收，继续下一轮")) ContinueWithoutReview();
            }
            else if (Running && GUILayout.Button(Paused ? "继续演算" : "暂停演算")) TogglePause();
            if (Running && GUILayout.Button("停止批量测试")) Stop();
            if (!string.IsNullOrEmpty(OutputDirectory) && GUILayout.Button("打开报告目录")) Application.OpenURL(new Uri(OutputDirectory).AbsoluteUri);
            GUILayout.EndArea();
        }
    }
}
