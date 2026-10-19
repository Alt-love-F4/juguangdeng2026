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
            public string endingId,displayCode,reuseKey;
            public ObjectEvidence[] objectEvidence;
        }
        [Serializable]public sealed class ObjectEvidence
        {
            public string objId;
        }
        public sealed class CurrentArtStatus
        {
            public string Key, Source, AssetPath, State;
            public UnityEngine.Object Asset;
            public bool HasBinding, Connected, RequirementCard, NoNewArtwork, NeedsManualAcceptance;
        }
        public sealed class CoverageSummary
        {
            public int Total, HistoricalCards, Connected, Unconnected, NoNewArtwork;
            public int EndingNewImages, EndingReuses, EndingNoNewArtwork;
        }
        public const int ExpectedRequirementCount = 115;
        public const string TutorialVisualsPath = "Dialogue/CafeTutorialVisuals";
        private static readonly Dictionary<string, string> CurrentProductionKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "CG04", "cg.adele_sampling" },
            { "CG05", "cg.burning_letter" },
            { "CG07", "cg.marie_moonlit" },
            { "P04", "prop.king_coins" }
        };

        public static CurrentArtStatus InspectCurrentArt(Requirement item, NarrativeStageVisuals registry,
            NarrativeCharacterArtAudit characterAudit, NarrativeConfiguration configuration,
            NarrativeEndingArtworkCatalog endingCatalog)
            => InspectCurrentArt(item, registry, characterAudit, configuration, endingCatalog, null);

        public static CurrentArtStatus InspectCurrentArt(Requirement item, NarrativeStageVisuals registry,
            NarrativeCharacterArtAudit characterAudit, NarrativeConfiguration configuration,
            NarrativeEndingArtworkCatalog endingCatalog, HashSet<string> usedKeys)
        {
            var status = new CurrentArtStatus();
            if (item == null) return status;

            if (!string.IsNullOrWhiteSpace(item.endingId))
            {
                var ending = endingCatalog != null ? endingCatalog.Find(item.endingId) : null;
                status.Key = "ending/" + item.endingId;
                status.Source = "NarrativeEndingArtworkCatalog";
                status.Asset = ending != null ? ending.artwork : null;
                status.HasBinding = status.Asset != null;
                status.RequirementCard = ending != null && ending.artworkIsPlaceholder;
                status.NoNewArtwork = item.endingId == "arrival.ending.if0"
                    && ending != null && ending.status == NarrativeEndingArtworkCatalog.ArtStatus.NotRequired;
                status.AssetPath = status.Asset != null ? AssetDatabase.GetAssetPath(status.Asset) : string.Empty;
                status.Connected = status.HasBinding && !status.RequirementCard;
                status.NeedsManualAcceptance = status.Connected;
                status.State = status.NoNewArtwork ? "无需新增图片（IF0）"
                    : status.Connected ? "已接入 · 待人工验收"
                    : status.RequirementCard ? "当前目录绑定的是需求卡"
                    : "当前未接入";
                return status;
            }

            if (item.id == "CH35")
            {
                var visuals = Resources.Load<DialogueVisualsAsset>(TutorialVisualsPath);
                status.Key = TutorialVisualsPath + ".centerPortrait";
                status.Source = "OpeningPresentation / " + status.Key;
                status.Asset = visuals != null ? visuals.CenterPortrait : null;
                status.HasBinding = status.Asset != null;
                status.AssetPath = status.Asset != null ? AssetDatabase.GetAssetPath(status.Asset) : string.Empty;
                status.Connected = status.HasBinding;
                status.NeedsManualAcceptance = status.Connected;
                status.State = status.Connected ? "已接入 · 待人工验收" : "当前未接入";
                return status;
            }

            if (item.category == "角色立绘")
            {
                var characters = characterAudit != null
                    ? characterAudit.Inspect(item)
                    : Array.Empty<NarrativeCharacterArtAudit.CharacterStatus>();
                var current = characters.FirstOrDefault(c => c.ObjectExists && c.Portrait.Available && !c.NoPortrait);
                if (current != null)
                {
                    status.Key = current.Id;
                    status.Source = "NCS Object / portraitPath";
                    status.Asset = current.Portrait.Sprite;
                    status.AssetPath = current.Portrait.AssetPath;
                    status.HasBinding = true;
                    status.RequirementCard = current.Portrait.Placeholder;
                    status.Connected = !status.RequirementCard;
                    status.NeedsManualAcceptance = status.Connected;
                }
                status.State = status.Connected ? "已接入 · 待人工验收"
                    : status.HasBinding ? "当前立绘为占位素材" : "当前未接入";
                return status;
            }

            if (registry == null) { status.State = "当前未接入"; return status; }
            string productionKey = null;
            if (CurrentProductionKeys.TryGetValue(item.id, out var explicitKey)) productionKey = explicitKey;
            if (!string.IsNullOrEmpty(productionKey))
            {
                var entry = registry.Find(productionKey);
                return FromStageEntry(productionKey, entry, "NarrativeStageVisuals（当前登记键）");
            }

            if (usedKeys == null) usedKeys = CurrentStageKeys(configuration);
            foreach (string candidate in item.suggestedRegistryKeys ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith("art.requirement.", StringComparison.Ordinal)
                    || !usedKeys.Contains(candidate)) continue;
                if (candidate == "scene.cafe" && item.id != "BG01") continue;
                var entry = registry.Find(candidate);
                if (entry == null || entry.texture == null && !entry.useSceneBackground && !entry.solidColor) continue;
                return FromStageEntry(candidate, entry, "NCS 当前演出引用 / NarrativeStageVisuals");
            }
            status.State = "当前未接入";
            return status;
        }

        private static CurrentArtStatus FromStageEntry(string key, NarrativeStageVisuals.Entry entry, string source)
        {
            var status = new CurrentArtStatus { Key = key, Source = source };
            if (entry == null || entry.texture == null && !entry.useSceneBackground && !entry.solidColor)
            { status.State = "当前未接入"; return status; }
            status.HasBinding = true;
            status.Asset = entry.texture;
            status.AssetPath = entry.texture != null ? AssetDatabase.GetAssetPath(entry.texture) : string.Empty;
            status.RequirementCard = entry.placeholder || key.StartsWith("art.requirement.", StringComparison.Ordinal)
                || status.AssetPath.StartsWith("Assets/Art/NarrativePlaceholders/", StringComparison.Ordinal);
            status.Connected = !status.RequirementCard;
            status.NeedsManualAcceptance = status.Connected && entry.texture != null;
            status.State = entry.useSceneBackground ? "已接入真实场景背景"
                : status.Connected ? "已接入 · 待人工验收" : "当前键绑定的是需求卡";
            return status;
        }

        public static HashSet<string> CurrentStageKeys(NarrativeConfiguration configuration)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (configuration == null) return keys;
            foreach (var page in configuration.presentations.conversationPages)
                if (!string.IsNullOrWhiteSpace(page.bgOverride)) keys.Add(page.bgOverride);
            foreach (var page in configuration.presentations.readingPages)
                if (!string.IsNullOrWhiteSpace(page.imageKey)) keys.Add(page.imageKey);
            foreach (var notice in configuration.presentations.notices)
                if (!string.IsNullOrWhiteSpace(notice.imageKey)) keys.Add(notice.imageKey);
            foreach (var opening in configuration.openingPresentations)
            {
                if (string.IsNullOrWhiteSpace(opening.visualsResource)) continue;
                var visuals = Resources.Load<DialogueVisualsAsset>(opening.visualsResource);
                if (visuals != null && !string.IsNullOrWhiteSpace(visuals.BackgroundKey)) keys.Add(visuals.BackgroundKey);
            }
            return keys;
        }

        public static CoverageSummary Summarize(Requirement[] requirements,
            NarrativeStageVisuals registry, NarrativeCharacterArtAudit characterAudit,
            NarrativeConfiguration configuration, NarrativeEndingArtworkCatalog endingCatalog)
        {
            var usedKeys = CurrentStageKeys(configuration);
            var statuses = (requirements ?? Array.Empty<Requirement>())
                .Select(item => InspectCurrentArt(item, registry, characterAudit, configuration, endingCatalog, usedKeys)).ToArray();
            return Summarize(requirements, statuses);
        }

        public static CoverageSummary Summarize(Requirement[] requirements, CurrentArtStatus[] statuses)
        {
            var summary = new CoverageSummary { Total = requirements?.Length ?? 0 };
            if (requirements == null || statuses == null) return summary;
            for (int i = 0; i < requirements.Length; i++)
            {
                var item = requirements[i];
                var current = i < statuses.Length ? statuses[i] : new CurrentArtStatus();
                if (HasHistoricalCard(item)) summary.HistoricalCards++;
                if (current.NoNewArtwork) summary.NoNewArtwork++;
                else if (current.Connected) summary.Connected++;
                else summary.Unconnected++;
                if (!string.IsNullOrWhiteSpace(item.endingId))
                {
                    if (!string.IsNullOrWhiteSpace(item.reuseKey)) summary.EndingReuses++;
                    else if (!string.IsNullOrWhiteSpace(item.assetPath)) summary.EndingNewImages++;
                    else summary.EndingNoNewArtwork++;
                }
            }
            return summary;
        }

        public static bool HasHistoricalCard(Requirement item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.assetPath)
                && item.assetPath.StartsWith("Assets/Art/NarrativePlaceholders/", StringComparison.Ordinal)
                && File.Exists(item.assetPath);
        }

        private static bool IsUniqueManifest(Requirement[] requirements)
        {
            return requirements != null && requirements.Length == ExpectedRequirementCount
                && requirements.All(r => r != null && !string.IsNullOrWhiteSpace(r.id))
                && requirements.Select(r => r.id).Distinct(StringComparer.Ordinal).Count() == ExpectedRequirementCount;
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
            else throw new FileNotFoundException("The additional art requirement manifest is missing.", supplement);
            const string endings="Docs/NarrativeEndingArtManifest.json";
            if(File.Exists(endings))
            {
                var extra=JsonUtility.FromJson<Manifest>(File.ReadAllText(endings,Encoding.UTF8));
                if(extra?.requirements==null||extra.requirements.Length!=16)throw new InvalidOperationException("Ending art revision is incomplete.");
                m.requirements=m.requirements.Concat(extra.requirements).ToArray();
                if(m.requirements.Select(r=>r.id).Distinct(StringComparer.Ordinal).Count()!=m.requirements.Length)throw new InvalidOperationException("Duplicate ending art requirement IDs.");
            }
            else throw new FileNotFoundException("The ending art requirement manifest is missing.", endings);
            if(!IsUniqueManifest(m.requirements))throw new InvalidOperationException("Expected 115 unique art requirement IDs.");
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
        private NarrativeConfiguration configuration;
        private NarrativeEndingArtworkCatalog endingCatalog;
        private HashSet<string> usedStageKeys = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, NarrativeArtPlaceholderBuilder.CurrentArtStatus> currentArtById
            = new Dictionary<string, NarrativeArtPlaceholderBuilder.CurrentArtStatus>(StringComparer.Ordinal);
        private NarrativeArtPlaceholderBuilder.CoverageSummary coverageSummary;
        private Vector2 listScroll, detailScroll;
        private string query = "", error, characterError;
        private int selection, previewMode, coverageFilter;
        private bool objectsExpanded;
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
            try
            {
                configuration = LubanNarrativeLoader.Load();
                endingCatalog = NarrativeEndingArtworkCatalog.Load();
                characterAudit = NarrativeCharacterArtAudit.Load();
            }
            catch (Exception ex) { characterError = ex.Message; characterAudit = null; }
            usedStageKeys = NarrativeArtPlaceholderBuilder.CurrentStageKeys(configuration);
            currentArtById.Clear();
            if (manifest != null)
            {
                var statuses = new List<NarrativeArtPlaceholderBuilder.CurrentArtStatus>();
                foreach (var item in manifest.requirements)
                {
                    var status = NarrativeArtPlaceholderBuilder.InspectCurrentArt(
                        item, registry, characterAudit, configuration, endingCatalog, usedStageKeys);
                    currentArtById[item.id] = status;
                    statuses.Add(status);
                }
                coverageSummary = NarrativeArtPlaceholderBuilder.Summarize(manifest.requirements, statuses.ToArray());
            }
            previewAsset = null;
        }

        private NarrativeArtPlaceholderBuilder.CurrentArtStatus CurrentArt(NarrativeArtPlaceholderBuilder.Requirement item)
            => currentArtById.TryGetValue(item.id, out var status) ? status
                : NarrativeArtPlaceholderBuilder.InspectCurrentArt(item, registry, characterAudit, configuration, endingCatalog, usedStageKeys);

        private void OnGUI()
        {
            GUILayout.Label("美术需求与游戏当前资源", EditorStyles.boldLabel);
            if (manifest == null) { EditorGUILayout.HelpBox(error ?? "没有清单", MessageType.Error); return; }
            if (characterError != null) EditorGUILayout.HelpBox("当前角色表读取失败：" + characterError, MessageType.Error);
            var summary = coverageSummary ?? new NarrativeArtPlaceholderBuilder.CoverageSummary { Total = manifest.requirements.Length };
            EditorGUILayout.HelpBox("唯一美术需求：" + summary.Total
                + "　历史需求卡文件：" + summary.HistoricalCards
                + "　当前未接入素材：" + summary.Unconnected
                + "　当前已接入素材：" + summary.Connected
                + "　无需新增图片：" + summary.NoNewArtwork, MessageType.Info);
            GUILayout.Label("结局画面需求：新图 " + summary.EndingNewImages + "　复用 " + summary.EndingReuses
                + "　IF0 不新增图 " + summary.EndingNoNewArtwork, EditorStyles.boldLabel);
            DrawGuestVariantPools(NarrativeCharacterArtAudit.CountGuestVariants());
            EditorGUILayout.BeginHorizontal();
            query = EditorGUILayout.TextField("搜索（编号／角色 ID／名称）", query);
            if (GUILayout.Button("重读当前配置", GUILayout.Width(110))) Reload();
            EditorGUILayout.EndHorizontal();
            coverageFilter = GUILayout.Toolbar(coverageFilter,
                new[] { "全部", "当前未接入", "已接入 · 待人工验收" });
            if (registry == null) EditorGUILayout.HelpBox("缺少背景／CG注册表；角色核对仍读取正式 Object 配置。", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            var items = manifest.requirements.Where(r =>
            {
                var current = CurrentArt(r);
                bool matchesFilter = coverageFilter == 0
                    || coverageFilter == 1 && !current.Connected && !current.NoNewArtwork
                    || coverageFilter == 2 && current.NeedsManualAcceptance;
                string objectIds = string.Join(" ", (r.objectEvidence ?? Array.Empty<NarrativeArtPlaceholderBuilder.ObjectEvidence>())
                    .Where(e => e != null).Select(e => e.objId));
                string searchable = string.Join(" ", r.id, r.category, r.name, r.requirement, r.configuration,
                    objectIds, current.Key, current.Source);
                return matchesFilter && searchable.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToArray();
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
                string label = item.id + "  " + item.name + "\n" + CurrentArt(item).State
                    + (states.Length > 0 ? " · " + NarrativeCharacterArtAudit.Summary(states) : "");
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
            var currentArt = CurrentArt(selected);
            var characters = characterAudit?.Inspect(selected) ?? Array.Empty<NarrativeCharacterArtAudit.CharacterStatus>();
            if (characters.Length > 0) DrawCharacters(characters);
            else if (selected.id == "CH35")
            {
                // The opening schema owns an explicit center slot instead of an Object identity.
                var tutorialVisuals = Resources.Load<DialogueVisualsAsset>(NarrativeArtPlaceholderBuilder.TutorialVisualsPath);
                var tutorialPortrait = tutorialVisuals != null ? tutorialVisuals.CenterPortrait : null;
                EditorGUILayout.HelpBox("教程角色使用 Opening 中心槽位：Dialogue/CafeTutorialVisuals.centerPortrait\n当前图片："
                    + (tutorialPortrait != null ? AssetDatabase.GetAssetPath(tutorialPortrait) : "未配置")
                    + "\n此入口没有独立 Object ID；彩色需求预览与游戏当前中心槽位分别核对。",
                    tutorialPortrait != null ? MessageType.Info : MessageType.Warning);
                if (tutorialPortrait != null && GUILayout.Button("预览教程当前人物")) { previewAsset = tutorialPortrait; previewMode = 0; }
            }
            else if (selected.category == "角色立绘")
                EditorGUILayout.HelpBox("此条需求没有明确 Object 映射证据，尚不能据此判断游戏角色已接线。原始配置提示：" + selected.configuration, MessageType.Warning);

            EditorGUILayout.HelpBox("当前素材状态：" + currentArt.State
                + (string.IsNullOrEmpty(currentArt.Source) ? "" : "\n来源：" + currentArt.Source)
                + (string.IsNullOrEmpty(currentArt.Key) ? "" : "\n生产键／身份：" + currentArt.Key)
                + (string.IsNullOrEmpty(currentArt.AssetPath) ? "" : "\n资源：" + currentArt.AssetPath),
                currentArt.Connected ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.HelpBox("历史需求标记（不作为当前配置结论）：" + selected.status + "\n" + selected.requirement
                + "\n来源（历史锚点）：" + selected.sourceEvidence, MessageType.Info);
            if (!string.IsNullOrEmpty(selected.cardRequirement)) GUILayout.Label("待核需求：" + selected.cardRequirement, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(selected.notes)) GUILayout.Label("备注：" + selected.notes, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(selected.decisionReason)) GUILayout.Label(selected.decisionReason, EditorStyles.wordWrappedMiniLabel);

            previewMode = GUILayout.Toolbar(previewMode, new[] { "当前生产资源", "历史需求卡" });
            UnityEngine.Object displayed = previewMode == 1
                ? (NarrativeArtPlaceholderBuilder.HasHistoricalCard(selected)
                    ? AssetDatabase.LoadAssetAtPath<Texture>(selected.assetPath) : null)
                : previewAsset ?? currentArt.Asset;
            var sprite = displayed as Sprite;
            var texture = sprite != null ? sprite.texture : displayed as Texture;
            Rect area = GUILayoutUtility.GetRect(250, 320, GUILayout.ExpandWidth(true));
            if (texture != null) EditorGUI.DrawPreviewTexture(area, texture, null, ScaleMode.ScaleToFit);
            else GUI.Label(area, currentArt.State == "已接入真实场景背景" && previewMode == 0
                ? "使用真实游戏场景，无独立背景图片"
                : previewMode == 1 ? "没有历史需求卡文件" : "当前来源未提供可预览的图片资源");
            EditorGUILayout.SelectableLabel(displayed != null ? AssetDatabase.GetAssetPath(displayed) : "", GUILayout.Height(22));
            if (displayed != null && GUILayout.Button("选中当前预览文件")) { Selection.activeObject = displayed; EditorGUIUtility.PingObject(displayed); }
            GUILayout.Label("当前资源、历史需求卡与人工验收状态分开显示；配置引用不代表自然可达或实机播放通过。", EditorStyles.wordWrappedMiniLabel);
            DrawObjectCoverage();
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal();
        }

        private void DrawGuestVariantPools(NarrativeCharacterArtAudit.GuestVariantCounts counts)
        {
            GUILayout.Label("Guest 变体资源池（独立于 115 条需求）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var pool in counts.Pools)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(150));
                GUILayout.Label(pool.GuestId, EditorStyles.boldLabel);
                GUILayout.Label("portrait_NN：" + pool.Portraits + "　avatar_NN：" + pool.Avatars, EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(pool.Total == 0 ? "待生成" : "已有变体 · 待人工验收", EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label("Characters/" + pool.GuestId + "/variants", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectCoverage()
        {
            if (characterAudit == null) return;
            var objects = characterAudit.InspectAllObjects();
            var missing = characterAudit.MissingOnstageNpcPortraitIds();
            var noPortrait = objects.Where(c => c.NoPortrait).Select(c => c.Id).ToArray();
            int loaded = objects.Count(c => c.Portrait.Available);
            objectsExpanded = EditorGUILayout.Foldout(objectsExpanded,
                "NCS Object：" + objects.Length + " 项，立绘已加载 " + loaded
                + "，可登场缺图 NPC " + missing.Length + "，no_portrait " + noPortrait.Length, true);
            if (missing.Length > 0)
                EditorGUILayout.HelpBox("可登场但缺基础立绘：" + string.Join("、", missing), MessageType.Warning);
            if (noPortrait.Length > 0)
                EditorGUILayout.HelpBox("明确标记 no_portrait（不计入缺图）：" + string.Join("、", noPortrait), MessageType.Info);
            if (!objectsExpanded) return;
            foreach (var character in objects)
            {
                string state = character.NoPortrait ? "no_portrait（明确不需要立绘）"
                    : character.Portrait.Available ? "立绘已加载 · 待人工验收"
                    : character.CanAppearOnstage ? "可登场但缺图" : "当前无立绘／未见对白登场";
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(character.Id + "  [" + character.ObjectType + "]", GUILayout.Width(270));
                GUILayout.Label(state + "　对白登场 " + character.OnstageConversationLines
                    + "　side=none " + character.OffstageConversationLines, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
                if (!character.NoPortrait && !character.Portrait.Available && !string.IsNullOrEmpty(character.Portrait.Error))
                    GUILayout.Label("    " + character.Portrait.Error, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawCharacters(NarrativeCharacterArtAudit.CharacterStatus[] characters)
        {
            GUILayout.Label("角色当前配置：" + NarrativeCharacterArtAudit.Summary(characters), EditorStyles.boldLabel);
            foreach (var character in characters)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(character.Name + "  [" + character.Id + "]", EditorStyles.boldLabel);
                if (!character.ObjectExists) EditorGUILayout.HelpBox("当前 Object 表不存在该 ID。", MessageType.Error);
                if (character.NoPortrait) EditorGUILayout.HelpBox("NCS Object 标记 no_portrait；与可登场缺图分开统计。", MessageType.Info);
                DrawResource("对话基础立绘", character.Portrait);
                DrawResource("头像", character.Avatar);
                DrawResource("店内形象", character.Indoor);
                GUILayout.Label("当前源表引用：Conversation " + character.ConversationLines + " 行，Reading " + character.ReadingLines + " 行。", EditorStyles.wordWrappedLabel);
                GUILayout.Label("对白立绘登场：" + character.OnstageConversationLines + " 行；side=none：" + character.OffstageConversationLines + " 行（只显示姓名和文本，不引入立绘）。", EditorStyles.wordWrappedLabel);
                if (!character.NoPortrait && character.CanAppearOnstage && !character.Portrait.Available)
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
