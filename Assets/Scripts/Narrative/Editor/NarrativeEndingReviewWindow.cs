using System;
using System.Linq;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    /// <summary>Read-only ending catalogue. Panel previews always use the shared presentation scene.</summary>
    public sealed class NarrativeEndingReviewWindow : EditorWindow
    {
        private NarrativeConfiguration config;
        private Vector2 scroll;
        private string query="",error;
        [MenuItem("Tools/Narrative/Ending Panels and Trigger Review")]
        public static void Open(){var w=GetWindow<NarrativeEndingReviewWindow>("结局与面板核对");w.minSize=new Vector2(760,520);w.Reload();w.Show();}
        private void Reload(){try{config=LubanNarrativeLoader.Load();error=null;}catch(Exception ex){error=ex.Message;config=null;}}
        private NarrativeEndingArtworkCatalog ArtworkCatalog => NarrativeEndingArtworkCatalog.Load();
        private void OnGUI()
        {
            GUILayout.Label("明确结局 / 已配置触发 / 面板预览",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("只读目录不授予结局。正式结束由NCS根尾结果和Ending条件判定。预览仅在已有PresentationDebugScene内播放ResultPanel，不改剧情或存档。未定源稿不能靠预览冒充已接通。",MessageType.Info);
            EditorGUILayout.BeginHorizontal();query=EditorGUILayout.TextField("查找",query);if(GUILayout.Button("重读配置",GUILayout.Width(95)))Reload();EditorGUILayout.EndHorizontal();
            if(config==null){EditorGUILayout.HelpBox(error??"没有配置",MessageType.Error);return;}
            var debug=FindDebug();
            if(debug==null){EditorGUILayout.HelpBox("面板预览：打开共享演出调试场景并进入Play Mode，再点击对应结局的预览按钮。",MessageType.None);
                if(!EditorApplication.isPlaying&&GUILayout.Button("打开已有 PresentationDebugScene"))EditorApplication.ExecuteMenuItem("Tools/Game Flow/Open Presentation Debug Scene");}
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var ending in config.endings.Where(e=>e.enabled).OrderByDescending(e=>e.priority).ThenBy(e=>e.endingId,StringComparer.Ordinal))
            {
                string title=Text(ending.titleText);if(!string.IsNullOrWhiteSpace(query)&&(ending.endingId+title).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                var artwork = NarrativeEndingEcho.ResolveArtwork(ending.endingId);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(title,EditorStyles.boldLabel);EditorGUILayout.SelectableLabel(ending.endingId,GUILayout.Height(18));
                GUILayout.Label(Text(ending.messageText),EditorStyles.wordWrappedLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (artwork.Sprite != null)
                    {
                        Rect previewRect = GUILayoutUtility.GetRect(150, 90, GUILayout.Width(150), GUILayout.Height(90));
                        EditorGUI.DrawPreviewTexture(previewRect, artwork.Sprite.texture, null, ScaleMode.ScaleToFit);
                    }
                    EditorGUILayout.HelpBox(artwork.StatusLabel, artwork.Sprite == null ? MessageType.Warning : MessageType.Info);
                }
                foreach(var group in config.conditions.Where(c=>c.ownerType=="ending"&&c.ownerId==ending.endingId).GroupBy(c=>c.groupIndex))
                {
                    GUILayout.Label("条件组 "+group.Key+"（组内AND／组间OR）",EditorStyles.miniBoldLabel);
                    foreach(var condition in group){
                        string producers=string.Join("、",config.results.Where(r=>r.resultType==NarrativeResultType.SetFlag&&r.target==condition.target&&r.value=="true").Select(r=>r.eventId).Distinct());
                        GUILayout.Label("• "+NarrativeRelationshipGraph.ConditionText(condition)+(producers.Length==0?"":"\n  生产者："+producers),EditorStyles.wordWrappedMiniLabel);
                    }
                }
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("复制ID"))GUIUtility.systemCopyBuffer=ending.endingId;
                using(new EditorGUI.DisabledScope(debug==null))if(GUILayout.Button("预览真实 ResultPanel"))debug.PlayEnding(ending.endingId,config);
                using (new EditorGUI.DisabledScope(ArtworkCatalog == null))
                {
                    if (GUILayout.Button("标记需补插画", GUILayout.Width(130))) MarkArtStatus(ending.endingId, NarrativeEndingArtworkCatalog.ArtStatus.RequiredMissing);
                    if (GUILayout.Button("标记可选/无需", GUILayout.Width(130))) MarkArtStatus(ending.endingId, NarrativeEndingArtworkCatalog.ArtStatus.OptionalMissing);
                }
                EditorGUILayout.EndHorizontal();EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        private void MarkArtStatus(string endingId, NarrativeEndingArtworkCatalog.ArtStatus status)
        {
            var catalog = ArtworkCatalog;
            if (catalog == null) { error = "资源不存在：Assets/Resources/Dialogue/NarrativeEndingArtworkCatalog.asset"; return; }
            var entry = catalog.Find(endingId);
            if (entry == null) { error = "未登记该结局ID。请先在Ending图片目录Inspector里手动同步结局记录。"; return; }
            entry.status = status;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private string Text(string key)=>config.texts.Find(t=>t.key==key)?.content??key??"";
        private static PresentationDebugSceneController FindDebug()
        {
            if(!EditorApplication.isPlaying)return null;
            return UnityEngine.Object.FindObjectsByType<PresentationDebugSceneController>(FindObjectsSortMode.None)
                .FirstOrDefault(c=>c.gameObject.scene.path==PresentationDebugSceneController.ScenePath);
        }
    }
}
