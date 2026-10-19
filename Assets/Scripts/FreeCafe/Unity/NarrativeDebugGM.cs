using System;
using System.Collections.Generic;
using System.Linq;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spotlight.GameFlow
{
    /// <summary>Global GM extension. Never creates or resets NarrativeRuntime.</summary>
    public sealed class NarrativeDebugGM : MonoBehaviour
    {
        private FreeCafeGMPanel shell;
        private NarrativeDebugSession test;
        private NarrativeDebugCatalog catalog;
        private NarrativeDebugCatalog.Entry selected;
        private string selectedIdentity, query = "", error = "", tab = "内容查找";
        private int searchPage, seekIndex, previewStart;
        private float speed = 35;
        private bool typewriter = true;
        private DialoguePanel preview;
        private DialogueScript script;
        private NarrativePresenter visualPresenter;
        private NarrativeChapterPreview chapterPreview;
        private IDisposable simulationPause;
        private bool branchVisual;
        private bool optionsRequested;
        private Transform content;
        private TMP_InputField report;
        private float nextRefresh;
        private const int PageSize = 8;

        private void Awake()
        {
            shell = GetComponent<FreeCafeGMPanel>();
            shell.RegisterCategory("叙事调试", Build);
            if (GetComponent<NarrativeAtlasGM>() == null) gameObject.AddComponent<NarrativeAtlasGM>();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        private void OnDestroy() { SceneManager.sceneUnloaded -= OnSceneUnloaded; ExitTest(); }
        private void OnSceneUnloaded(Scene scene) { ExitTest(); }
        private void Update()
        {
            if (optionsRequested && preview != null && preview.IsPlaying && preview.IsReady)
            {
                if (preview.IsTyping) preview.Advance();
                else { preview.Advance(); optionsRequested = false; }
            }
            if (report != null && shell.IsVisible && Time.unscaledTime >= nextRefresh)
            { nextRefresh = Time.unscaledTime + .25f; report.SetTextWithoutNotify(LocationReport()); }
        }
        private void Run(Action action, bool rebuild = true)
        {
            try { action(); error = ""; }
            catch (Exception ex) { error = ex.Message; Debug.LogWarning("[NarrativeDebug] " + error); }
            if (rebuild) Build(shell);
            shell.SetExternalStatus(string.IsNullOrEmpty(error) ? "叙事调试操作已提交；请查看状态与诊断。" : error);
        }
        private void EnsureData()
        {
            if (test != null) return;
            test = new NarrativeDebugSession();
            catalog = new NarrativeDebugCatalog(test.Configuration, test.ResolveText);
            selected = catalog.Entries.FirstOrDefault(e => e.Identity == selectedIdentity);
        }
        private void Reload()
        {
            if (preview != null && preview.IsPlaying || test != null && test.Controller.IsBusy) throw new InvalidOperationException("活动预览/测试期间不能重载配置，请先退出测试会话。");
            test?.Dispose(); test = null;
            EnsureData();
            if (selectedIdentity != null && selected == null) throw new InvalidOperationException("原选择已失效：" + selectedIdentity);
        }
        private void Build(FreeCafeGMPanel panel)
        {
            if (panel.ContentRoot == null) return;
            // A page refresh replaces only our own view, preserving the shell heading.
            var old = panel.ContentRoot.Find("NarrativeDebugContent");
            if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
            content = MakeScroll(panel.ContentRoot);
            Row("内容查找", () => Switch("内容查找"), "播放控制", () => Switch("播放控制"), "测试条件", () => Switch("测试条件"), "状态与诊断", () => Switch("状态与诊断"));
            Label("当前：" + (branchVisual ? "独立分支测试 · 真实演出与核心" : preview != null && preview.IsPlaying ? "演出预览 · 只看演出，不结算" : test != null && test.HasBaseline ? "独立分支测试" : "正式只读 / 配置浏览"));
            if (!string.IsNullOrEmpty(error)) Label("错误：" + error);
            try { EnsureData(); }
            catch (Exception ex) { error = ex.Message; Label("配置加载失败：" + error); Button("重试加载", () => Run(Reload)); return; }
            Label("选择：" + (selected != null ? selected.ToString() + "\n关联事件：" + string.Join(", ", selected.EventIds) : selectedIdentity == null ? "无" : "已失效 " + selectedIdentity));
            if (tab == "内容查找") BuildSearch();
            else if (tab == "播放控制") BuildPlayback();
            else if (tab == "测试条件") BuildConditions();
            else BuildDiagnostics();
        }
        private void Switch(string value) { tab = value; Build(shell); }
        private void BuildSearch()
        {
            Input("中文标题 / ID / 演出key / 正文", query, value => query = value);
            Row("搜索", () => { searchPage = 0; Build(shell); }, "重载生成配置", () => Run(Reload));
            var matches = catalog.Search(query);
            searchPage = Mathf.Clamp(searchPage, 0, Math.Max(0, (matches.Count - 1) / PageSize));
            Label($"{matches.Count} 条；第 {searchPage + 1}/{Math.Max(1, (matches.Count + PageSize - 1) / PageSize)} 页");
            foreach (var entry in matches.Skip(searchPage * PageSize).Take(PageSize))
            {
                var captured = entry;
                Button(entry.ToString(), () => { selected = captured; selectedIdentity = captured.Identity; seekIndex = 0; tab = "播放控制"; Build(shell); });
            }
            Row("上一页", () => { searchPage--; Build(shell); }, "下一页", () => { searchPage++; Build(shell); });
        }
        private void BuildPlayback()
        {
            Label("只看演出，不结算。Conversation按源段落展示，条件不作判定；选项禁用并指向真实分支测试。定位输入为本段0起始快照行索引，源行ID/seq见诊断。");
            Row("10/7 S1 完整预览", () => Run(() => PlayChapter("oct07.s1", 2)), "S2 知觉1", () => Run(() => PlayChapter("oct07.s2", 1)), "S2 知觉2", () => Run(() => PlayChapter("oct07.s2", 2)));
            Row("S2 知觉节点", () => Run(() => PlayChapter("oct07.s2", 2, "oct07.s2.perception")), "S2 信纸节点", () => Run(() => PlayChapter("oct07.s2", 2, "oct07.s2.read_letter")));
            if (selected != null && selected.Kind == "Event")
            {
                foreach (var item in catalog.Entries.Where(e => e.Kind != "Event" && e.EventIds.Contains(selected.Key)))
                { var captured = item; Button("关联演出：" + item, () => { selected = captured; selectedIdentity = captured.Identity; Build(shell); }); }
            }
            Input("定位快照行索引 (0起始)", seekIndex.ToString(), value => { if (int.TryParse(value, out int n)) seekIndex = n; });
            Input("预览文字速度 (字/秒)", speed.ToString("0.##"), value => { if (float.TryParse(value, out float n) && n >= 0 && !float.IsNaN(n) && !float.IsInfinity(n)) speed = n; });
            Button("逐字：" + (typewriter ? "开启" : "关闭"), () => { typewriter = !typewriter; Build(shell); });
            Row("从头播放 / 重播本段", () => Run(() => PlayAt(0)), "定位指定句", () => Run(() => PlayAt(seekIndex)));
            Row("上一句", () => Run(() => PlayAt(CurrentIndex - 1)), "下一句", () => Run(() => PlayAt(CurrentIndex + 1)));
            Row("到下一组选项",  () => Run(() => { PrepareScript(); if (script.Options.Count == 0) throw new InvalidOperationException("本段没有选项；请在查找中选择带选项的Conversation段落。"); PlayAt(script.Count - 1); optionsRequested = true; }), "停止预览", () => Run(StopPreview));
            Button("关闭GM继续观看 [~可再打开]", () => shell.SetVisible(false));
            Button("退出测试会话", () => Run(ExitTest));
        }
        private int CurrentIndex => preview != null && preview.IsPlaying ? previewStart + Math.Max(0, preview.LineIndex) : seekIndex;
        private void RequireSafeBoundary()
        {
            var runtime = NarrativeRuntime.Instance;
            if (runtime != null && runtime.IsReady && (runtime.Controller.IsBusy || runtime.Controller.PendingEventIds.Count > 0))
                throw new InvalidOperationException("请先结束当前正式演出；或在共享调试场景测试。目标已保留。");
            if (DialoguePanel.IsAnyPlaying && DialoguePanel.Active != preview)
                throw new InvalidOperationException("其他演出正在占用舞台，请先结束该演出；共享场景可使用原停止按钮。");
            simulationPause ??= PresentationPauseRegistry.Acquire("NarrativeDebug independent context");
        }
        private void PrepareScript()
        {
            if (selected == null) throw new InvalidOperationException("尚未选择有效演出。");
            script = catalog.CreateScript(selected);
        }
        private void PlayAt(int index)
        {
            RequireSafeBoundary();
            if (test.Controller.IsBusy) throw new InvalidOperationException("分支测试尚未结束，请恢复起点或退出测试后预览。");
            branchVisual = false;
            PrepareScript();
            if (index < 0 || index >= script.Count) throw new InvalidOperationException("定位越界：" + index + "；有效范围0.." + (script.Count - 1));
            StopPreview();
            previewStart = seekIndex = index;
            preview = DialoguePlayback.PlayPreview(script.RebuildAt(index));
            preview.SetPreviewReading(speed, typewriter);
            if (selected.Kind == "Conversation")
            {
                string music = test.Configuration.presentations.conversationPages.First(p => p.pageId == selected.PageId).bgmOverride;
                if (!string.IsNullOrEmpty(music)) Spotlight.Audio.GameAudio.OverrideMusic(preview, music);
            }
            shell.SetVisible(false);
        }
        private void PlayChapter(string eventId, int perception, string pageId = null)
        {
            StopPreview(); RequireSafeBoundary();
            chapterPreview = new NarrativeChapterPreview();
            chapterPreview.Play(eventId, perception, pageId);
            shell.SetVisible(false);
        }
        private void StopPreview()
        {
            chapterPreview?.Dispose(); chapterPreview = null;
            if (preview != null && DialoguePanel.Active == preview) DialoguePlayback.Stop();
            preview = null; optionsRequested = false;
        }
        private void ExitTest()
        {
            test?.Dispose(); visualPresenter?.Dispose(); visualPresenter = null;
            StopPreview(); test = null; catalog = null; branchVisual = false;
            simulationPause?.Dispose(); simulationPause = null;
        }
        private void RestoreTest()
        {
            visualPresenter?.Dispose(); visualPresenter = null; StopPreview();
            test.RestoreBaseline(); branchVisual = false;
        }
        private void StartVisualTest()
        {
            if (visualPresenter != null) return;
            visualPresenter = new NarrativePresenter(test.Configuration, test.ResolveText);
            visualPresenter.ChoiceSelected += test.SelectChoice;
            visualPresenter.ChoiceUnlocked = (id, context) => test.Controller.EvaluateConditions("choice", id, context);
            visualPresenter.AbilityCheck = test.EvaluateAbilityCheck;
            visualPresenter.InlineAbilityCheck = test.Controller.EvaluateInlineAbilityCheckForPresentation;
            visualPresenter.LetterPlayback = LetterEventPlayback.PlayPreview;
            visualPresenter.PageCondition = key => test.Controller.State.GetCondition(key);
            visualPresenter.ConversationPlayback = (request, data, completed) =>
            {
                previewStart = 0;
                script = NarrativeConversationEventPlayback.CreateScript(data, request.PassiveHint);
                preview = DialoguePlayback.Play(script.WithPreviewContext(), completed, data.cancelled);
                data.releaseStage = () => DialoguePlayback.ReleaseStage(data.continuityOwner);
                preview.AllowCancel = true;
                preview.OptionSelectionRequested = data.selectChoice;
                preview.SetPreviewReading(speed, typewriter);
                if (!string.IsNullOrEmpty(data.bgmOverride)) Spotlight.Audio.GameAudio.OverrideMusic(preview, data.bgmOverride);
                return true;
            };
            visualPresenter.Cancelled += reason => { error = "测试演出取消：" + reason + "；请恢复起点后重测。"; };
            test.SetPresenter(visualPresenter);
            branchVisual = true;
        }
        private void BuildConditions()
        {
            Label("仅独立内存会话。每轮修改前先恢复起点；修改完成后明确建立新起点。直接请求和自然调度分别操作。演出宿主请求与未验证能力见诊断。");
            Row("恢复测试起点", () => Run(() => { RequireSafeBoundary(); RestoreTest(); }), "建立测试起点", () => Run(() => { RequireSafeBoundary(); test.EstablishBaseline(); }));
            foreach (string ability in new[] { "language", "courage", "wisdom", "perception" })
            { string key = ability; ValueEditor("属性 " + key, value => test.SetAbility(key, ParseInt(value))); }
            ValueEditor("日期 day", value => test.SetDate(ParseInt(value)));
            ValueEditor("地点 ID", value => test.SetLocation(value));
            foreach (var bar in test.Configuration.bars) { string key = bar.barKey; ValueEditor("进度 " + key, value => test.SetBar(key, ParseInt(value))); }
            foreach (var condition in test.Configuration.conditionKeys) { string key = condition.key; ValueEditor("布尔 " + key + " (true/false)", value => test.SetCondition(key, bool.Parse(value))); }
            ValueEditor("前置已发生事件 ID（只准备事实）", value => test.PrepareEventFact(value));
            ValueEditor("自然日期提交 day", value => { StartVisualTest(); test.SubmitDate(ParseInt(value)); shell.SetVisible(false); }, false);
            foreach (var bar in test.Configuration.bars) { string key = bar.barKey; ValueEditor("自然阈值提交 " + key, value => { StartVisualTest(); test.SubmitBar(key, ParseInt(value)); shell.SetVisible(false); }, false); }
            Button("直接请求选中事件", () => Run(RequestSelected));
            Button("恢复起点并重测事件", () => Run(() => { RequireSafeBoundary(); RestoreTest(); RequestSelected(); }));
            Button("结束测试回合（非正式结束当天）", () => Run(() => { RequireSafeBoundary(); StartVisualTest(); test.EndTurn(); shell.SetVisible(false); }));
        }
        private static int ParseInt(string value) => int.Parse(value);
        private void ValueEditor(string label, Action<string> setter, bool restore = true)
        {
            string draft = "";
            Input(label, "", value => draft = value);
            Button("应用 " + label, () => Run(() => { RequireSafeBoundary(); if (restore && visualPresenter != null) RestoreTest(); setter(draft); }));
        }
        private void RequestSelected()
        {
            RequireSafeBoundary();
            if (!test.HasBaseline) throw new InvalidOperationException("请先建立测试起点。");
            if (selected == null || selected.Kind != "Event") throw new InvalidOperationException("请从内容查找选择 Event，避免把复用演出的多个事件混为一个。");
            StartVisualTest();
            shell.SetVisible(false);
            var definition = test.Configuration.events.Single(e => e.eventId == selected.Key);
            bool accepted = definition.entry == NarrativeEntryPoint.Immediate ? test.RequestEvent(selected.Key)
                : definition.entry == NarrativeEntryPoint.MapAction ? test.Controller.QueueMapActionEvent(selected.Key)
                : test.Controller.ProcessEntry(definition.entry);
            if (!accepted) throw new InvalidOperationException("事件请求被拒绝：" + test.Controller.ExplainEvent(selected.Key));
        }
        private void BuildDiagnostics()
        {
            report = Input("可选择/复制的定位与诊断", LocationReport(), null, 420);
            report.readOnly = true;
            Row("复制定位信息", () => GUIUtility.systemCopyBuffer = LocationReport(), "刷新诊断", () => Build(shell));
            BuildCurrentTestActions();
            ValueEditor("提交实际 choiceId", value => { if (!test.SelectChoice(value)) throw new InvalidOperationException("选择被核心拒绝：" + value); }, false);
            if (!branchVisual) Button("手动确认当前演出完成（仅核心测试）", () => Run(() => { RequireSafeBoundary(); test.CompletePresentation(); }));
            Button("退出测试会话", () => Run(ExitTest));
        }
        private void BuildCurrentTestActions()
        {
            var current = test.CurrentRequest.Event;
            if (current == null || branchVisual) return;
            var setup = test.Configuration.setups.FirstOrDefault(s => s.setupKey == current.setupKey);
            if (!string.IsNullOrEmpty(setup?.checkId))
                Button("执行当前真实检定 " + setup.checkId, () => Run(() => { RequireSafeBoundary(); test.EvaluateAbilityCheck(setup.checkId); }));
            var slots = PresentationSlots.Parse(setup);
            PresentationSlots.ApplyEventProperties(slots, current);
            var groups = new HashSet<string>();
            if (slots.TryGetValue("opts", out string group)) groups.Add(group);
            foreach (var page in test.Configuration.presentations.conversationPages.Where(p => p.presentationKey == current.presentationKey))
                if (!string.IsNullOrEmpty(page.optionGroupKey)) groups.Add(page.optionGroupKey);
            foreach (var choice in test.Configuration.choices.Where(c => groups.Contains(c.groupKey)).OrderBy(c => c.seq))
            {
                var captured = choice;
                Label("选择条件 " + captured.choiceId + "\n" + string.Join("\n", test.Controller.ExplainConditions("choice", captured.choiceId)));
                Button("选择：" + test.ResolveText(captured.textKey) + " [" + captured.choiceId + "]", () => Run(() =>
                {
                    RequireSafeBoundary();
                    if (!test.SelectChoice(captured.choiceId)) throw new InvalidOperationException("选择被核心拒绝：" + captured.choiceId);
                    if (!string.IsNullOrEmpty(captured.checkId)) test.EvaluateAbilityCheck(captured.checkId);
                }));
            }
        }
        private string LocationReport()
        {
            string mode = branchVisual ? "独立分支测试（真实演出与核心）" : preview != null && preview.IsPlaying ? "演出预览：只看演出，不结算" : test != null && test.HasBaseline ? "独立分支测试" : "正式只读";
            string result = "mode=" + mode + "\nselection=" + (selectedIdentity ?? "none") + "\neventIds=" + (selected == null ? "" : string.Join(",", selected.EventIds)) +
                "\npresentation=" + selected?.Key + "\n" + (catalog?.SourceAt(selected, CurrentIndex) ?? "无源行") + "\nsnapshotIndex=" + CurrentIndex + " page=" + (preview != null && preview.IsPlaying ? preview.PageNumber.ToString() : "无活动页") + "\nlastError=" + error;
            var activePanel = DialoguePanel.Active;
            if (activePanel != null && activePanel.CurrentSource != null)
            {
                var source = activePanel.CurrentSource;
                result += "\n实际舞台 source=" + source.SourceType + " eventId=" + source.EventId + " presentation=" + source.PresentationKey + " sourceLineId=" + source.SourceLineId + " seq=" + source.Sequence + " pageId=" + source.PageId + " page=" + activePanel.PageNumber;
            }
            var runtime = NarrativeRuntime.Instance;
            if (runtime != null && runtime.IsReady && selected != null && selected.Kind == "Event") result += "\n正式只读事件诊断：\n" + runtime.Controller.ExplainEvent(selected.Key);
            if (branchVisual && script != null && preview != null && preview.LineIndex >= 0 && preview.LineIndex < script.Count)
            {
                var source = script[preview.LineIndex].Source;
                if (source != null) result += "\n活动演出 source=" + source.SourceType + " eventId=" + source.EventId + " presentation=" + source.PresentationKey + " sourceLineId=" + source.SourceLineId + " seq=" + source.Sequence + " pageId=" + source.PageId;
            }
            if (test != null)
            {
                result += "\n独立测试：\n" + test.GetReport();
                if (selected != null && selected.Kind == "Event")
                {
                    var diagnosis = test.Controller.ExplainEvent(selected.Key);
                    result += "\n选中事件诊断：" + diagnosis + "\n" + string.Join("\n", diagnosis.Conditions);
                }
            }
            return result;
        }
        private Transform MakeScroll(Transform parent)
        {
            var root = new GameObject("NarrativeDebugContent", typeof(RectTransform), typeof(Image), typeof(ScrollRect)); root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform; rt.anchorMin = new Vector2(.025f, .02f); rt.anchorMax = new Vector2(.975f, .86f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(.08f, .09f, .11f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(root.transform, false); Stretch((RectTransform)viewport.transform);
            var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); list.transform.SetParent(viewport.transform, false);
            var rect = (RectTransform)list.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
            var layout = list.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(10, 20, 10, 10); layout.spacing = 8; layout.childForceExpandHeight = false;
            list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.GetComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform; scroll.content = rect; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            return list.transform;
        }
        private TMP_Text Label(string value, Transform parent = null)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent ?? content, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = shell.UIFont; text.fontSize = 19; text.color = Color.white; text.text = value; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            return text;
        }
        private void Button(string caption, Action click, Transform parent = null)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)); go.transform.SetParent(parent ?? content, false);
            go.GetComponent<Image>().color = new Color(.2f, .25f, .3f); go.GetComponent<LayoutElement>().preferredHeight = 46;
            var text = Label(caption, go.transform); Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center; text.fontSize = 18;
            go.GetComponent<Button>().onClick.AddListener(() => click());
        }
        private void Row(string a, Action aa, string b, Action bb, string c = null, Action cc = null, string d = null, Action dd = null)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); go.transform.SetParent(content, false);
            go.GetComponent<LayoutElement>().preferredHeight = 46; var layout = go.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 6; layout.childForceExpandWidth = true;
            Button(a, aa, go.transform); Button(b, bb, go.transform); if (c != null) Button(c, cc, go.transform); if (d != null) Button(d, dd, go.transform);
        }
        private TMP_InputField Input(string caption, string value, Action<string> changed, float height = 44)
        {
            Label(caption);
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement)); go.transform.SetParent(content, false);
            go.GetComponent<LayoutElement>().preferredHeight = height; go.GetComponent<Image>().color = new Color(.14f, .17f, .2f);
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)); area.transform.SetParent(go.transform, false); Stretch((RectTransform)area.transform);
            var text = Label("", area.transform); Stretch(text.rectTransform); text.margin = new Vector4(8, 4, 8, 4); text.richText = false;
            var input = go.GetComponent<TMP_InputField>(); input.textViewport = (RectTransform)area.transform; input.textComponent = (TextMeshProUGUI)text; input.fontAsset = shell.UIFont;
            input.lineType = height > 50 ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine; input.SetTextWithoutNotify(value);
            if (changed != null) input.onValueChanged.AddListener(v => changed(v));
            return input;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
