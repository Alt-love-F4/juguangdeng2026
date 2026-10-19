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
            public string cardRequirement,cardConfiguration,decisionReason;
            public string endingId,displayCode;
            public ObjectEvidence[] objectEvidence;
        }
        [Serializable]public sealed class ObjectEvidence
        {
            public string objId;
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
            const string endings="Docs/NarrativeEndingArtManifest.json";
            if(File.Exists(endings))
            {
                var extra=JsonUtility.FromJson<Manifest>(File.ReadAllText(endings,Encoding.UTF8));
                if(extra?.requirements==null||extra.requirements.Length!=16)throw new InvalidOperationException("Ending art revision is incomplete.");
                m.requirements=m.requirements.Concat(extra.requirements).ToArray();
                if(m.requirements.Select(r=>r.id).Distinct(StringComparer.Ordinal).Count()!=m.requirements.Length)throw new InvalidOperationException("Duplicate ending art requirement IDs.");
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
    public sealed class NarrativeArtRequirementWindow : EditorWindow
    {
        private NarrativeArtPlaceholderBuilder.Manifest manifest;
        private NarrativeStageVisuals registry;
        private NarrativeCharacterArtAudit characterAudit;
        private Vector2 listScroll, detailScroll;
        private string query = "", error, characterError;
        private int selection, previewMode;
        private bool placeholdersOnly;
        private UnityEngine.Object previewAsset;

        [MenuItem("Tools/Narrative/Art Requirement Placeholders")]
        public static void Open()
        {
            var window = GetWindow<NarrativeArtRequirementWindow>("美术需求与当前资源");
            window.minSize = new Vector2(1000, 700);
            window.Reload();
            window.Show();
        }

        private void OnEnable() => Reload();
        private void OnProjectChange() { Reload(); Repaint(); }
        private void Reload()
        {
            error = characterError = null;
            try
            {
                manifest = NarrativeArtPlaceholderBuilder.ReadManifest();
                registry = AssetDatabase.LoadAssetAtPath<NarrativeStageVisuals>(NarrativeArtPlaceholderBuilder.RegistryPath);
            }
            catch (Exception ex) { error = ex.Message; manifest = null; registry = null; }
            try { characterAudit = NarrativeCharacterArtAudit.Load(); }
            catch (Exception ex) { characterError = ex.Message; characterAudit = null; }
            previewAsset = null;
        }

        private bool TryGetRegisteredArt(NarrativeArtPlaceholderBuilder.Requirement item, out string key, out NarrativeStageVisuals.Entry entry)
        {
            if(!string.IsNullOrEmpty(item.endingId))
            {
                var catalog=NarrativeEndingArtworkCatalog.Load();
                var ending=catalog!=null?catalog.Find(item.endingId):null;
                if(ending!=null&&ending.artwork!=null)
                {
                    key="ending/"+item.endingId;
                    entry=new NarrativeStageVisuals.Entry{key=key,texture=ending.artwork.texture,placeholder=ending.artworkIsPlaceholder};
                    return true;
                }
            }
            // Production keys take precedence over the separate requirement card registration.
            var keys = (item.suggestedRegistryKeys ?? Array.Empty<string>()).Concat(item.proposedUnregisteredKeys ?? Array.Empty<string>())
                .Concat(new[] { "art.requirement." + item.id });
            foreach (var candidate in keys.Distinct(StringComparer.Ordinal))
            {
                var found = registry != null ? registry.Find(candidate) : null;
                if (found == null || found.texture == null && !found.useSceneBackground && !found.solidColor) continue;
                key = candidate; entry = found; return true;
            }
            key = null; entry = null; return false;
        }

        private void OnGUI()
        {
            GUILayout.Label("美术需求与游戏当前资源", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            query = EditorGUILayout.TextField("搜索（编号／角色 ID／名称）", query);
            placeholdersOnly = GUILayout.Toggle(placeholdersOnly, "仅含需求卡", GUILayout.Width(105));
            if (GUILayout.Button("重读当前配置", GUILayout.Width(110))) Reload();
            EditorGUILayout.EndHorizontal();
            if (manifest == null) { EditorGUILayout.HelpBox(error ?? "没有清单", MessageType.Error); return; }
            if (characterError != null) EditorGUILayout.HelpBox("当前角色表读取失败：" + characterError, MessageType.Error);
            if (registry == null) EditorGUILayout.HelpBox("缺少背景／CG注册表；角色核对仍读取正式 Object 配置。", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            var items = manifest.requirements.Where(r => (!placeholdersOnly || r.disposition == "placeholder")
                && (r.id + r.name + r.requirement + string.Join(" ", (r.objectEvidence ?? Array.Empty<NarrativeArtPlaceholderBuilder.ObjectEvidence>()).Select(e => e.objId)))
                    .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            // Keep the details in the filtered list; never show a hidden, unrelated selection after searching.
            if (items.Length == 0)
            {
                GUILayout.Label("没有匹配的需求");
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal(); return;
            }
            if (!items.Contains(manifest.requirements[Mathf.Clamp(selection, 0, manifest.requirements.Length - 1)]))
            { selection = Array.IndexOf(manifest.requirements, items[0]); previewAsset = null; }
            foreach (var item in items)
            {
                var states = characterAudit?.Inspect(item) ?? Array.Empty<NarrativeCharacterArtAudit.CharacterStatus>();
                string label = item.id + "  " + item.name + (states.Length > 0 ? "\n" + NarrativeCharacterArtAudit.Summary(states) : "");
                if (GUILayout.Toggle(manifest.requirements[selection] == item, label, "Button", GUILayout.MinHeight(42)))
                {
                    int next = Array.IndexOf(manifest.requirements, item);
                    if (next != selection) { selection = next; previewAsset = null; detailScroll = Vector2.zero; }
                }
            }
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            var selected = manifest.requirements[selection];
            GUILayout.Label(selected.id + " · " + selected.name, EditorStyles.boldLabel);
            var characters = characterAudit?.Inspect(selected) ?? Array.Empty<NarrativeCharacterArtAudit.CharacterStatus>();
            if (characters.Length > 0) DrawCharacters(characters);
            else if (selected.id == "CH35")
            {
                // The opening schema owns an explicit center slot instead of an Object identity.
                var tutorialVisuals = Resources.Load<DialogueVisualsAsset>("Dialogue/CafeTutorialVisuals");
                var tutorialPortrait = tutorialVisuals != null ? tutorialVisuals.CenterPortrait : null;
                EditorGUILayout.HelpBox("教程角色使用 Opening 中心槽位：Dialogue/CafeTutorialVisuals.centerPortrait\n当前图片："
                    + (tutorialPortrait != null ? AssetDatabase.GetAssetPath(tutorialPortrait) : "未配置")
                    + "\n此入口没有独立 Object ID；彩色需求预览与游戏当前中心槽位分别核对。",
                    tutorialPortrait != null ? MessageType.Info : MessageType.Warning);
                if (tutorialPortrait != null && GUILayout.Button("预览教程当前人物")) { previewAsset = tutorialPortrait; previewMode = 0; }
            }
            else if (selected.category == "角色立绘")
                EditorGUILayout.HelpBox("此条需求没有明确 Object 映射证据，尚不能据此判断游戏角色已接线。原始配置提示：" + selected.configuration, MessageType.Warning);

            bool hasBinding = TryGetRegisteredArt(selected, out string boundKey, out var boundEntry);
            if (hasBinding)
            {
                string state = boundEntry.useSceneBackground ? "实际场景背景" : boundEntry.solidColor ? "程序纯色"
                    : boundEntry.placeholder ? "需求占位图" : "正式资源";
                EditorGUILayout.HelpBox((characters.Length > 0 ? "独立需求卡／背景注册：" : "当前背景／CG注册：") + boundKey + "\n状态：" + state,
                    boundEntry.placeholder ? MessageType.Warning : MessageType.Info);
            }
            EditorGUILayout.HelpBox("历史需求标记（不作为当前配置结论）：" + selected.status + "\n" + selected.requirement
                + "\n来源（历史锚点）：" + selected.sourceEvidence, MessageType.Info);
            if (!string.IsNullOrEmpty(selected.cardRequirement)) GUILayout.Label("待核需求：" + selected.cardRequirement, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(selected.notes)) GUILayout.Label("备注：" + selected.notes, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(selected.decisionReason)) GUILayout.Label(selected.decisionReason, EditorStyles.wordWrappedMiniLabel);

            previewMode = GUILayout.Toolbar(previewMode, new[] { "游戏当前资源", "原始需求卡／记录" });
            UnityEngine.Object current = previewAsset;
            if (current == null && characters.Length > 0) current = characters.Select(c => c.Portrait.Sprite).FirstOrDefault(s => s != null);
            if (current == null && characters.Length == 0 && hasBinding) current = boundEntry.texture;
            if (current == null && characters.Length == 0) current = AssetDatabase.LoadAssetAtPath<Texture>(selected.assetPath);
            var displayed = previewMode == 1 ? AssetDatabase.LoadAssetAtPath<Texture>(selected.assetPath) : current;
            var sprite = displayed as Sprite;
            var texture = sprite != null ? sprite.texture : displayed as Texture;
            Rect area = GUILayoutUtility.GetRect(250, 320, GUILayout.ExpandWidth(true));
            if (texture != null) EditorGUI.DrawPreviewTexture(area, texture, null, ScaleMode.ScaleToFit);
            else GUI.Label(area, hasBinding && boundEntry.useSceneBackground ? "使用真实游戏场景，无独立背景图片" : "无独立图片／复用或程序演出项目");
            EditorGUILayout.SelectableLabel(displayed != null ? AssetDatabase.GetAssetPath(displayed) : "", GUILayout.Height(22));
            if (displayed != null && GUILayout.Button("选中当前预览文件")) { Selection.activeObject = displayed; EditorGUIUtility.PingObject(displayed); }
            GUILayout.Label("角色状态来自当前 Luban Object 与实际 Sprite 加载；需求卡独立保留。配置引用不代表事件自然可达、美术验收或实机播放通过。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
        }

        private void DrawCharacters(NarrativeCharacterArtAudit.CharacterStatus[] characters)
        {
            GUILayout.Label("角色当前配置：" + NarrativeCharacterArtAudit.Summary(characters), EditorStyles.boldLabel);
            foreach (var character in characters)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(character.Name + "  [" + character.Id + "]", EditorStyles.boldLabel);
                if (!character.ObjectExists) EditorGUILayout.HelpBox("当前 Object 表不存在该 ID。", MessageType.Error);
                DrawResource("对话基础立绘", character.Portrait);
                DrawResource("头像", character.Avatar);
                DrawResource("店内形象", character.Indoor);
                GUILayout.Label("当前源表引用：Conversation " + character.ConversationLines + " 行，Reading " + character.ReadingLines + " 行。", EditorStyles.wordWrappedLabel);
                GUILayout.Label("对白立绘登场：" + character.OnstageConversationLines + " 行；side=none：" + character.OffstageConversationLines + " 行（只显示姓名和文本，不引入立绘）。", EditorStyles.wordWrappedLabel);
                if (character.OnstageConversationLines > 0 && !character.Portrait.Available)
                    EditorGUILayout.HelpBox("该 ID 被配置为对白立绘登场，但基础立绘未加载；其他 ID 的素材不会自动补给它。", MessageType.Warning);
                else if (character.LineCount == 0)
                    GUILayout.Label("当前 Conversation／Reading 没有引用此 ID；开场槽位和实际事件条件需另核。", EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(character.CafeCustomer ? "允许进入普通营业顾客池；出场仍由经营与剧情规则决定。"
                    : "未进入普通营业随机顾客池；剧情对话仍可按此 ID 使用立绘。", EditorStyles.wordWrappedMiniLabel);
                if (character.FallbackEmotes.Length > 0)
                    EditorGUILayout.HelpBox("源表使用的这些表情尚未接入独立差分；允许立绘登场时回退基础立绘：" + string.Join("、", character.FallbackEmotes), MessageType.Warning);
                if (character.UsageExamples.Length > 0) GUILayout.Label(string.Join("\n", character.UsageExamples), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawResource(string title, NarrativeCharacterArtAudit.ResourceStatus resource)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title + "：" + (resource.Available ? "已配置 · " + resource.Provenance : "未配置／无法加载"), EditorStyles.wordWrappedLabel);
            if (resource.Sprite != null && GUILayout.Button("预览", GUILayout.Width(50))) { previewAsset = resource.Sprite; previewMode = 0; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(resource.Path) ? "Object 路径为空" : resource.Path, GUILayout.Height(18));
            if (!string.IsNullOrEmpty(resource.Error)) GUILayout.Label(resource.Error, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
