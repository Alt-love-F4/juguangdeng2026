using UnityEditor;
using UnityEngine;

namespace Spotlight.GameFlow
{
    [CustomEditor(typeof(NarrativeEndingArtworkCatalog))]
    public sealed class NarrativeEndingArtworkCatalogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (NarrativeEndingArtworkCatalog)target;
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("结局图片使用独立的按 endingId 登记目录，可绑定 Sprite、需求卡ID和明确状态；它不参与NCS结局判定。完成正式美术后请关闭占位标记并更新来源备注。", MessageType.Info);
            if (GUILayout.Button("同步启用的 NCS 结局"))
            {
                var config = Spotlight.Narrative.LubanNarrativeLoader.Load();
                Undo.RecordObject(catalog, "Synchronize NCS ending art records");
                catalog.SynchronizeEnabledEndings(config.endings.FindAll(ending => ending.enabled).ConvertAll(ending => ending.endingId));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
        }
    }
}
