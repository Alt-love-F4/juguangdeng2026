using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spotlight.GameFlow;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Narrative.Editor
{
    /// <summary>Local labelled cards only. No provider/API calls; no existing finished artwork is replaced.</summary>
    public static class NarrativeArtPlaceholderBuilder
    {
        public const string ManifestPath="Docs/NarrativeArtPlaceholderManifest.json";
        public const string RegistryPath="Assets/Resources/Dialogue/NarrativeStageVisuals.asset";
        [Serializable]public sealed class Manifest { public Requirement[] requirements; }
        [Serializable]public sealed class Requirement
        {
            public string id,category,name,status,requirement,sourceEvidence,configuration,notes,disposition,assetPath;
            public string[] suggestedRegistryKeys,proposedUnregisteredKeys;
        }
        [Serializable]private sealed class Binding { public string key,id,assetPath,status; }
        [Serializable]private sealed class Report { public string policy="Local labelled placeholders. Not final art. No image API.";public Binding[] bindings; }
        public static Manifest ReadManifest()
        {
            var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath,Encoding.UTF8));
            if(m?.requirements==null||m.requirements.Length!=93)throw new InvalidOperationException("The marked source requirements manifest is missing/incomplete.");
            const string supplement="Docs/NarrativeAdditionalArtPlaceholderManifest.json";
            if(File.Exists(supplement))
            {
                var extra=JsonUtility.FromJson<Manifest>(File.ReadAllText(supplement,Encoding.UTF8));
                if(extra?.requirements==null||extra.requirements.Length!=6)throw new InvalidOperationException("Current-DOCX additional marked requirements are incomplete.");
                m.requirements=m.requirements.Concat(extra.requirements).ToArray();
                if(m.requirements.Select(r=>r.id).Distinct(StringComparer.Ordinal).Count()!=m.requirements.Length)throw new InvalidOperationException("Duplicate art requirement IDs.");
            }
            return m;
        }
        [MenuItem("Tools/Narrative/Bind Local Art Requirement Placeholders")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Bind persistent art in Edit Mode.");
            var manifest=ReadManifest();var registry=AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>(RegistryPath);
            if(registry==null)throw new InvalidOperationException("Existing NarrativeStageVisuals registry is required.");
            var pending=manifest.requirements.Where(r=>r.disposition=="placeholder").ToArray();
            // Preflight all files before writing the registry. The generator's directory is the only permitted source.
            foreach(var item in pending)if(!item.assetPath.StartsWith("Assets/Art/NarrativePlaceholders/",StringComparison.Ordinal)||!File.Exists(item.assetPath))throw new InvalidOperationException("Missing local requirement card: "+item.id);
            var report=new List<Binding>();Undo.RecordObject(registry,"Bind local labelled narrative art placeholders");
            foreach(var item in pending)
            {
                var importer=AssetImporter.GetAtPath(item.assetPath)as TextureImporter;
                if(importer!=null&&(importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.maxTextureSize<2048))
                {importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
                var texture=AssetDatabase.LoadAssetAtPath<Texture>(item.assetPath);if(texture==null)throw new InvalidOperationException("Cannot import "+item.assetPath);
                var keys=new List<string>{"art.requirement."+item.id};
                keys.AddRange(item.suggestedRegistryKeys??Array.Empty<string>());
                keys.AddRange(item.proposedUnregisteredKeys??Array.Empty<string>());
                foreach(string key in keys.Distinct(StringComparer.Ordinal))
                {
                    var old=registry.Find(key);
                    // Explicit exact entries can replace a prior placeholder, but never scene.cafe,
                    // intentional black, existing completed art or an alias resolving to real art.
                    if(old!=null&&(!old.placeholder||old.useSceneBackground||old.solidColor))
                    {report.Add(new Binding{key=key,id=item.id,assetPath=AssetDatabase.GetAssetPath(old.texture),status="preserved-existing"});continue;}
                    registry.SetEntry(new NarrativeStageVisuals.Entry{key=key,texture=texture,tint=Color.white,placeholderColor=Color.white,placeholder=true,fadeSeconds=.3f});
                    report.Add(new Binding{key=key,id=item.id,assetPath=item.assetPath,status="labelled-placeholder-not-final-art"});
                }
            }
            EditorUtility.SetDirty(registry);AssetDatabase.SaveAssetIfDirty(registry);
            File.WriteAllText("Docs/NarrativeArtPlaceholderBindings.json",JsonUtility.ToJson(new Report{bindings=report.ToArray()},true),new UTF8Encoding(false));
            Debug.Log("[NarrativeArt] Bound "+pending.Length+" local requirement cards; existing real artwork and scene.cafe preserved. No image API.");
        }
    }
    public sealed class NarrativeArtRequirementWindow:EditorWindow
    {
        private NarrativeArtPlaceholderBuilder.Manifest manifest;
        private NarrativeStageVisuals registry;
        private Vector2 scroll;private string query="",error;private int selection;private bool placeholdersOnly=true;
        [MenuItem("Tools/Narrative/Art Requirement Placeholders")]
        public static void Open(){var w=GetWindow<NarrativeArtRequirementWindow>("美术需求占位");w.minSize=new Vector2(900,600);w.Reload();w.Show();}
        private void Reload(){try{manifest=NarrativeArtPlaceholderBuilder.ReadManifest();registry=AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>(NarrativeArtPlaceholderBuilder.RegistryPath);error=registry==null?"缺少叙事美术注册表":null;}catch(Exception ex){error=ex.Message;manifest=null;registry=null;}}
        private bool TryGetRegisteredArt(NarrativeArtPlaceholderBuilder.Requirement item,out string key,out NarrativeStageVisuals.Entry entry)
        {
            var keys=(item.suggestedRegistryKeys??Array.Empty<string>()).Concat(item.proposedUnregisteredKeys??Array.Empty<string>())
                .Concat(new[]{"art.requirement."+item.id});
            foreach(var candidate in keys.Distinct(StringComparer.Ordinal))
            {
                var found=registry.Find(candidate);
                if(found?.texture==null)continue;
                key=candidate;entry=found;return true;
            }
            key=null;entry=null;return false;
        }
        private void OnGUI()
        {
            GUILayout.Label("美术需求与当前资源 / 不调用API",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();query=EditorGUILayout.TextField("搜索",query);placeholdersOnly=GUILayout.Toggle(placeholdersOnly,"仅需求卡",GUILayout.Width(100));if(GUILayout.Button("重读",GUILayout.Width(60)))Reload();EditorGUILayout.EndHorizontal();
            if(manifest==null){EditorGUILayout.HelpBox(error??"没有清单",MessageType.Error);return;}
            if(registry==null){EditorGUILayout.HelpBox(error??"没有叙事美术注册表",MessageType.Error);return;}
            EditorGUILayout.BeginHorizontal();EditorGUILayout.BeginVertical(GUILayout.Width(265));scroll=EditorGUILayout.BeginScrollView(scroll);
            var items=manifest.requirements.Where(r=>(!placeholdersOnly||r.disposition=="placeholder")&&(r.id+r.name+r.requirement).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            foreach(var item in items)if(GUILayout.Button(item.id+"  "+item.name,GUILayout.Height(30)))selection=Array.IndexOf(manifest.requirements,item);
            EditorGUILayout.EndScrollView();EditorGUILayout.EndVertical();EditorGUILayout.BeginVertical();
            var selected=manifest.requirements[Mathf.Clamp(selection,0,manifest.requirements.Length-1)];
            GUILayout.Label(selected.id+" · "+selected.name,EditorStyles.boldLabel);EditorGUILayout.HelpBox("原标记："+selected.status+"\n处理："+selected.disposition+"\n"+selected.requirement+"\n来源（历史锚点，非当前运行顺序）："+selected.sourceEvidence,MessageType.Info);
            string boundKey;NarrativeStageVisuals.Entry boundEntry;
            bool hasBinding=TryGetRegisteredArt(selected,out boundKey,out boundEntry);
            var texture=hasBinding?boundEntry.texture:AssetDatabase.LoadAssetAtPath<Texture>(selected.assetPath);
            string assetPath=hasBinding?AssetDatabase.GetAssetPath(boundEntry.texture):selected.assetPath;
            if(hasBinding)EditorGUILayout.HelpBox("当前注册："+boundKey+"\n状态："+(boundEntry.placeholder?"需求占位图":"正式资源"),boundEntry.placeholder?MessageType.Warning:MessageType.Info);
            Rect area=GUILayoutUtility.GetRect(300,300,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));if(texture!=null)EditorGUI.DrawPreviewTexture(area,texture,null,ScaleMode.ScaleToFit);else GUI.Label(area,"无独立图片／复用或程序演出项目");
            EditorGUILayout.SelectableLabel(assetPath??"",GUILayout.Height(22));
            if(texture!=null&&GUILayout.Button("选中图片文件")){Selection.activeObject=texture;EditorGUIUtility.PingObject(texture);}
            GUILayout.Label("需求卡仅作需求记录；正式资源状态读取自 NarrativeStageVisuals。剧情挂点是否启用仍以当前源表为准。",EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();
        }
    }
}
