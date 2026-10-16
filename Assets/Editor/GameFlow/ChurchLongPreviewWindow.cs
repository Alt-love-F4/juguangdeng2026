using System;
using System.Linq;
using Spotlight.Narrative;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.GameFlow.Editor
{
    /// <summary>Editor-owned controls for the existing isolated preview, never a production event launcher.</summary>
    public sealed class ChurchLongPreviewWindow : EditorWindow
    {
        private const string Chapter = "church.1104.s1";
        private readonly string[] fees = { "未提供", "free", "one", "quoted" };
        private readonly string[] routes = { "未提供", "far", "near" };
        private readonly string[] attitudes = { "未提供", "wait", "ask", "hurry" };
        private readonly string[] factions = { "未提供", "非革命（夹具）", "革命（夹具）" };
        private string[] pageIds = Array.Empty<string>();
        private int selectedPage, fee, route, attitude, faction;
        private int perception = 2, wisdom = 3;
        private string diagnostic;
        private NarrativeChapterPreview preview;

        [MenuItem("Tools/Game Flow/Church Long Isolated Preview")]
        public static void Open() => GetWindow<ChurchLongPreviewWindow>("教会长篇 · 隔离预览");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ReloadPages();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StopPreview();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) StopPreview();
        }

        private void ReloadPages()
        {
            try
            {
                string selected = pageIds.Length > 0 ? pageIds[Mathf.Clamp(selectedPage, 0, pageIds.Length - 1)] : null;
                var config = LubanNarrativeLoader.Load();
                var root = config.events.Single(e => e.eventId == Chapter);
                pageIds = config.presentations.conversationPages
                    .Where(p => p.presentationKey == root.presentationKey
                        && (p.stepType == NarrativeConversationStepType.Dialogue || p.stepType == NarrativeConversationStepType.PassiveCheck))
                    .OrderBy(p => p.seq).Select(p => p.pageId).ToArray();
                selectedPage = Math.Max(0, Array.IndexOf(pageIds, selected));
                diagnostic = null;
            }
            catch (Exception error) { diagnostic = error.Message; }
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("仅使用正式生成内容的独立内存预览，不推进经营、日期或正式存档。金额 xx 和阵营选项都是未定内容/显式测试输入，不能作为正式发布验收。", MessageType.Info);
            if (GUILayout.Button("重新读取已生成配置")) ReloadPages();
            if (pageIds.Length > 0)
                selectedPage = EditorGUILayout.Popup("目标页", selectedPage, pageIds);
            fee = EditorGUILayout.Popup("报酬选择", fee, fees);
            route = EditorGUILayout.Popup("游览路线", route, routes);
            attitude = EditorGUILayout.Popup("夜间态度", attitude, attitudes);
            faction = EditorGUILayout.Popup("晚间阵营夹具", faction, factions);
            perception = EditorGUILayout.IntField("知觉", perception);
            wisdom = EditorGUILayout.IntField("智慧", wisdom);
            EditorGUILayout.HelpBox("已跳过的选项必须明确提供；目标页之后的选项仍由面板作答。不可达的路线/检定页会拒绝，而不是换播其他页。", MessageType.None);
            bool sharedScene = SceneManager.GetActiveScene().path == PresentationDebugSceneController.ScenePath;
            if (!sharedScene) EditorGUILayout.HelpBox("请先打开已有 PresentationDebugScene。此窗口不自动切场景或覆盖未保存内容。", MessageType.Warning);
            if (!EditorApplication.isPlaying) EditorGUILayout.HelpBox("请在共享场景进入 Play Mode 后启动。", MessageType.None);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !sharedScene || pageIds.Length == 0))
            {
                if (GUILayout.Button("从目标页隔离预览")) StartPreview();
            }
            using (new EditorGUI.DisabledScope(preview == null))
            {
                if (GUILayout.Button("停止本窗口的预览")) StopPreview();
            }
            if (!string.IsNullOrEmpty(diagnostic)) EditorGUILayout.HelpBox(diagnostic, MessageType.Info);
            if (preview != null) EditorGUILayout.LabelField("当前源行", preview.Source);
        }

        private void StartPreview()
        {
            StopPreview();
            var next = new NarrativeChapterPreview();
            try
            {
                bool? revolution = faction == 0 ? (bool?)null : faction == 2;
                next.PlayChurchLongPageConstruction(pageIds[selectedPage], fee == 0 ? null : fees[fee],
                    route == 0 ? null : routes[route], attitude == 0 ? null : attitudes[attitude],
                    perception, wisdom, revolution, () => { diagnostic = "本段隔离预览已结束；未结算正式章节。"; Repaint(); });
                preview = next;
                diagnostic = "隔离预览中。已跳过的正文与瞬时音效不会播放。";
            }
            catch (Exception error)
            {
                next.Dispose();
                diagnostic = error.Message;
            }
        }

        private void StopPreview()
        {
            preview?.Dispose();
            preview = null;
        }

        private void Update()
        {
            if (preview != null) Repaint();
        }
    }
}
