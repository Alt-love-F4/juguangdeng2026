using System;
using Spotlight.FreeCafe;
using Spotlight.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.GameFlow
{
    /// <summary>Global GM entry for the shared story atlas. Browsing is read-only; playback owns an isolated preview.</summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeAtlasGM : MonoBehaviour
    {
        private FreeCafeGMPanel shell;
        private NarrativeRelationshipGraphView view;
        private NarrativeGraphRuntimeStatus status;
        private NarrativeConfiguration config;
        private NarrativeRuntime observedRuntime;
        private NarrativeAtlasPreview preview;
        private IDisposable browsePause;
        private bool open, openRequested, playRequested, playWholeRoot, reopenAfterPlayback, reloadRequested;
        private string requestedNode, requestedPage, message = "选择故事卡片，查看当前会话状态与分支内容。";
        private float nextRefresh;
        private GUIStyle header, note, button;
        private Font font;
        private Texture2D paper, dark, hover;
        public bool IsOpen => open;
        public bool IsPreviewPlaying => preview != null && preview.IsPlaying;
        public string LastStatus => preview != null ? preview.Status : message;
        public NarrativeRelationshipGraphView View => view;

        private void Awake()
        {
            shell = GetComponent<FreeCafeGMPanel>();
            if (shell == null) { enabled = false; return; }
            shell.RegisterCategory("剧情图谱", Build);
            shell.VisibilityChanged += VisibilityChanged;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        private void Build(FreeCafeGMPanel panel)
        {
            panel.AddAction("打开剧情图谱 · 当前存档状态", Open,
                "故事连线 / 台词逐页 / 前置诊断 / 真实样式只读试播；不改变正式进度", false);
            panel.AddAction("停止图谱试播", StopPreview,
                "只释放本工具拥有的舞台和暂停，不关闭正式剧情", false);
            panel.SetExternalStatus("剧情图谱：状态来自正式NCS会话；预览仅使用独立内存副本。");
            openRequested = true;
        }
        private void VisibilityChanged(bool visible)
        {
            if (!visible) { openRequested = false; CloseAtlas(); }
            else if (IsPreviewPlaying) openRequested = true;
        }
        private void SceneUnloaded(Scene scene)
        {
            StopOwnedPreview();
            openRequested = false; playRequested = false;
            CloseAtlas();
        }
        public void Open()
        {
            try
            {
                EnsureData();
                if (!shell.IsVisible) shell.SetVisible(true);
                open = true; openRequested = false;
                shell.SetExternalOverlay(true);
                browsePause ??= PresentationPauseRegistry.Acquire("GM narrative atlas browsing");
                RefreshStatus();
            }
            catch (Exception error)
            {
                message = "剧情图谱加载失败：" + error.Message;
                shell.SetExternalStatus(message); CloseAtlas();
            }
        }
        private void EnsureData(bool force = false)
        {
            var runtime = NarrativeRuntime.Instance;
            NarrativeConfiguration active = runtime != null && runtime.IsReady ? runtime.Configuration : null;
            if (!force && view != null && active == null && observedRuntime == null) return;
            if (!force && view != null && active != null && ReferenceEquals(active, config) && observedRuntime == runtime) return;
            config = active ?? LubanNarrativeLoader.Load(); observedRuntime = active != null ? runtime : null;
            status = new NarrativeGraphRuntimeStatus(config);
            view ??= new NarrativeRelationshipGraphView();
            view.Load(config);
            view.StatusLabel = status.Label;
            view.EndingForecastText = () => status.EndingForecast;

            view.StatusDetails = node =>
            {
                string details = status.Details(node);
                if (node.Kind == StoryNodeKind.Event) details += "\n\n" + status.PageStatus(node.Key, view.SelectedPageId);
                var dialogue = DialoguePanel.Active;
                if (dialogue != null && dialogue.IsPlaying && dialogue.CurrentSource != null
                    && dialogue.CurrentSource.EventId == node.Key && dialogue.CurrentSource.PageId == view.SelectedPageId)
                {
                    // Preview reuses DialoguePanel and source IDs; the panel's owner tag is authoritative.
                    if (dialogue.Tag == NarrativeAtlasPreview.PanelTag)
                        details += "\n本工具沙盒试播正在显示此页（不是正式舞台或历史已读记录）。";
                    else if (!IsPreviewPlaying)
                        details += "\n当前真实舞台正在显示此页（仅当前游标，不是历史已读记录）。";
                }
                return details;
            };
            view.PreviewBlockReason = PreviewReason;
            view.PreviewRequested = QueuePlay;
            view.RefreshRequested = () => reloadRequested = true;
            RefreshStatus();
        }
        private void RefreshStatus()
        {
            var runtime = NarrativeRuntime.Instance;
            status?.Refresh(runtime != null && runtime.IsReady ? runtime.Controller : null);
            view?.InvalidateStatusCache();
            if (view != null) view.ContextLabel = runtime != null && runtime.IsReady
                ? "当前正式会话 · 状态只读 / 独立试播不结算 / 逐页历史未单独记录"
                : "未加载正式会话 · 仅配置浏览 / 独立试播使用默认测试数值";
            nextRefresh = Time.unscaledTime + .5f;
        }
        public void SelectNode(string nodeId) { EnsureData(); view.Select(nodeId); }
        public void PlaySelected(bool wholeRoot = false)
        {
            if (view == null || view.SelectedNode == null) { message = "请先选择剧情节点。"; return; }
            QueuePlay(view.SelectedNode, wholeRoot ? null : view.SelectedPageId);
        }
        private string PreviewReason(StoryGraphNode node)
        {
            if (IsPreviewPlaying) return "本工具有试播正在进行；可继续观看，或先停止再选择其他剧情。";
            return NarrativeAtlasPreview.BlockReason(node, NarrativeRuntime.Instance);
        }
        private bool CanAcceptPlayRequest => open && isActiveAndEnabled && shell != null && shell.IsVisible;
        private void QueuePlay(StoryGraphNode node, string pageId)
        {
            // A delayed view callback must not resurrect a closed/hidden atlas.
            if (!CanAcceptPlayRequest || node == null || view?.SelectedNodeId != node.Id) return;
            requestedNode = node.Id; requestedPage = pageId; playWholeRoot = string.IsNullOrEmpty(pageId); playRequested = true;
        }
        private void CancelPendingPlayback()
        {
            // Cancel both stages: the view's deferred callback and this host's next-Update request.
            view?.CancelQueuedActions();
            playRequested = false; requestedNode = null; requestedPage = null; playWholeRoot = false;
        }
        private void Update()
        {
            bool atlasVisible = open && shell != null && shell.IsVisible;
            if (preview == null && !atlasVisible && !openRequested && !playRequested && !reloadRequested) return;

            preview?.Tick();
            if (reloadRequested)
            {
                reloadRequested = false;
                try { EnsureData(true); message = "已刷新图谱与当前会话状态；没有重载或重置正式NCS。"; }
                catch (Exception error) { message = "刷新失败：" + error.Message; }
            }
            if (openRequested) Open();
            if (open && shell != null && shell.IsVisible)
            {
                try { EnsureData(); if (Time.unscaledTime >= nextRefresh) RefreshStatus(); }
                catch (Exception error) { message = "读取失败：" + error.Message; }
            }
            if (!playRequested) return;
            if (!CanAcceptPlayRequest) { CancelPendingPlayback(); return; }
            playRequested = false;
            try
            {
                var node = view.SelectedNode;
                if (node == null || node.Id != requestedNode) throw new InvalidOperationException("选择已改变，请重新点击试播。");
                string reason = PreviewReason(node);
                if (!string.IsNullOrEmpty(reason)) throw new InvalidOperationException(reason);
                StopOwnedPreview();
                var runtime = NarrativeRuntime.Instance;
                preview = new NarrativeAtlasPreview(config, runtime != null && runtime.IsReady ? runtime.ResolveText : null);
                preview.Finished += PlaybackFinished;
                preview.Play(node, playWholeRoot ? null : requestedPage, runtime);
                reopenAfterPlayback = true;
                CloseAtlas(); shell.SetVisible(false);
            }
            catch (Exception error)
            {
                message = "无法试播：" + error.Message;
                StopOwnedPreview();
                shell.SetExternalStatus(message);
            }
        }
        private void PlaybackFinished()
        {
            message = preview != null ? preview.Status : "试播已结束。";
            if (reopenAfterPlayback) openRequested = true;
            reopenAfterPlayback = false;
        }
        public void StopPreview()
        {
            CancelPendingPlayback();
            openRequested = false; // Do not reopen on a completion queued just before Stop.
            message = "已停止图谱试播；正式剧情、AP、成长和咖啡馆订单未被改变。";
            StopOwnedPreview();
            if (shell != null) shell.SetExternalStatus(message);
        }
        private void StopOwnedPreview()
        {
            reopenAfterPlayback = false;
            if (preview != null) { preview.Finished -= PlaybackFinished; preview.Dispose(); preview = null; }
        }
        private void CloseAtlas()
        {
            CancelPendingPlayback();
            openRequested = false;
            open = false;
            if (shell != null) shell.SetExternalOverlay(false);
            browsePause?.Dispose(); browsePause = null;
        }
        private void ContinueWatching() { CloseAtlas(); shell.SetVisible(false); }
        // Keep ordinary desktop resolutions pixel-for-pixel; only undersized screens scale down.
        private static Vector3 CalculateCanvas(int screenWidth, int screenHeight)
        {
            float width = Mathf.Max(1, screenWidth), height = Mathf.Max(1, screenHeight);
            float scale = Mathf.Min(1f, Mathf.Min(width / 1000f, height / 650f));
            return new Vector3(width / scale, height / scale, scale);
        }
        private static Rect ToolbarRect(float width)
        {
            const float toolbarWidth = 498f;
            bool separateRow = width < 1160f;
            return new Rect(separateRow ? 22f : width - 22f - toolbarWidth,
                separateRow ? 46f : 14f, toolbarWidth, 34f);
        }
        private void OnGUI()
        {
            if (!open || shell == null || !shell.IsVisible) return;
            Styles();
            Matrix4x4 oldMatrix = GUI.matrix; int depth = GUI.depth;
            Vector3 canvas = CalculateCanvas(Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(canvas.z, canvas.z, 1));
            GUI.depth = -2000;
            try
            {
                GUI.DrawTexture(new Rect(0, 0, canvas.x, canvas.y), paper);
                Rect toolbar = ToolbarRect(canvas.x);
                float titleWidth = toolbar.y > 14f ? canvas.x - 44f : toolbar.x - 38f;
                GUI.Label(new Rect(22, 8, titleWidth, 28), "沸点 · 叙事档案   /   STORY ATLAS", header);
                float x = toolbar.x, y = toolbar.y;
                if (GUI.Button(new Rect(x, y, 108, 34), "返回 GM", button)) CloseAtlas();
                x += 118;
                if (GUI.Button(new Rect(x, y, 112, 34), "停止试播", button)) StopPreview();
                x += 122;
                bool wasEnabled = GUI.enabled;
                try
                {
                    GUI.enabled = wasEnabled && IsPreviewPlaying;
                    if (GUI.Button(new Rect(x, y, 112, 34), "继续观看", button)) ContinueWatching();
                }
                finally { GUI.enabled = wasEnabled; }
                x += 122;
                if (GUI.Button(new Rect(x, y, 136, 34), "关闭 GM  [~]", button)) { CloseAtlas(); shell.SetVisible(false); }
                string statusText = IsPreviewPlaying ? preview.Status : message;
                var statusContent = new GUIContent(statusText, statusText);
                float statusHeight = Mathf.Clamp(note.CalcHeight(statusContent, canvas.x - 44f), 22f, 66f);
                var statusRect = new Rect(22, toolbar.yMax + 6f, canvas.x - 44f, statusHeight);
                GUI.Label(statusRect, statusContent, note);
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                { Event.current.Use(); CloseAtlas(); }
                float contentTop = statusRect.yMax + 8f;
                if (open) view.Draw(new Rect(10, contentTop, canvas.x - 20f, canvas.y - contentTop - 10f));
            }
            finally { GUI.matrix = oldMatrix; GUI.depth = depth; }
        }
        private static Texture2D Tile(Color color)
        {
            var image = new Texture2D(1,1) { hideFlags = HideFlags.HideAndDontSave };
            image.SetPixel(0,0,color); image.Apply(); return image;
        }
        private void Styles()
        {
            if (paper != null) return;
            paper = Tile(new Color(.94f,.89f,.78f)); dark = Tile(new Color(.29f,.20f,.16f)); hover = Tile(new Color(.43f,.30f,.21f));
            font = Font.CreateDynamicFontFromOSFont(new[] { "SimSun", "Microsoft YaHei", "Arial" }, 16);
            header = new GUIStyle { font=font,fontSize=20,fontStyle=FontStyle.Bold,normal={textColor=new Color(.27f,.18f,.14f)} };
            note = new GUIStyle { font=font,fontSize=12,wordWrap=true,clipping=TextClipping.Clip,normal={textColor=new Color(.44f,.33f,.25f)} };
            button = new GUIStyle(GUI.skin.button) { font=font,fontSize=13,normal={background=dark,textColor=new Color(.98f,.93f,.82f)},hover={background=hover,textColor=Color.white},active={background=dark,textColor=Color.white} };
        }
        private void OnDestroy()
        {
            if (shell != null) shell.VisibilityChanged -= VisibilityChanged;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            StopOwnedPreview(); CloseAtlas(); view?.Dispose(); view = null;
            if (paper != null) Destroy(paper); if (dark != null) Destroy(dark); if (hover != null) Destroy(hover); if (font != null) Destroy(font);
        }
    }
}
