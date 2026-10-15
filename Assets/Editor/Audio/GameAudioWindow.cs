using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Audio.Editor
{
    public sealed class GameAudioWindow : EditorWindow
    {
        public const string CatalogPath = "Assets/Resources/GameAudioCatalog.asset";
        private AudioCatalog catalog;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        private bool issuesExpanded;
        private bool activeExpanded;
        private readonly List<string> activeVoices = new List<string>();
        private void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        [MenuItem("Tools/Audio/Game Audio")]
        public static void Open() => GetWindow<GameAudioWindow>("游戏音频配置");

        private void OnEnable() => catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("统一配置按钮、违规提示、出品结果、经营设备、场景音乐和演出。缺失项保持留空；临时替代项已注明。", MessageType.Info);
            if (catalog == null)
            {
                if (GUILayout.Button("创建初始配置并关联现有素材")) CreateCatalog();
                return;
            }
            EditorGUILayout.ObjectField("配置资产", catalog, typeof(AudioCatalog), false);
            if (GUILayout.Button("定位配置资产")) Selection.activeObject = catalog;
            if (Application.isPlaying && GameAudio.Service != null)
            {
                GameAudio.Service.DescribeActiveVoices(activeVoices);
                activeExpanded = EditorGUILayout.Foldout(activeExpanded, "项目活动声音 / 所有者（" + activeVoices.Count + "）", true);
                if (activeExpanded)
                    foreach (string voice in activeVoices) EditorGUILayout.LabelField(voice, EditorStyles.wordWrappedMiniLabel);
            }
            List<string> issues = catalog.Validate().ToList();
            issuesExpanded = EditorGUILayout.Foldout(issuesExpanded, "素材缺口 / 临时替代（" + issues.Count + "）", true);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (issuesExpanded) foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
            if (inspector == null || inspector.target != catalog) UnityEditor.Editor.CreateCachedEditor(catalog, null, ref inspector);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Tools/Audio/Create Initial Audio Catalog")]
        public static void CreateCatalog()
        {
            AudioCatalog existing = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (existing != null) { Selection.activeObject = existing; Open(); return; }
            AudioCatalog asset = CreateInstance<AudioCatalog>();
            Add(asset, AudioKeys.Select, "按钮悬停 / 主动导航选中", "H-pitch wooooooodblock 高音调西洋木鱼", AudioCategory.UI, .2f, provisional: true);
            Add(asset, AudioKeys.Confirm, "按钮点击确认", "确认选择", AudioCategory.UI, .45f);
            Add(asset, AudioKeys.Cancel, "返回 / 取消", "M-pitch wooooooodblock 中音调西洋木鱼", AudioCategory.UI, .3f, provisional: true);
            Add(asset, AudioKeys.Error, "违规操作 UI / 行动拒绝", "L-pitch wooooooodblock 低音调西洋木鱼", AudioCategory.UI, .5f, provisional: true);
            Add(asset, AudioKeys.Save, "手动保存成功", "确认选择", AudioCategory.UI, .4f, provisional: true);
            Add(asset, AudioKeys.Tab, "切换页签", "M-pitch wooooooodblock 中音调西洋木鱼", AudioCategory.UI, .2f, provisional: true);
            Add(asset, AudioKeys.Page, "订单 / 配方翻页", "纸张翻动声", AudioCategory.UI, .35f);
            Add(asset, AudioKeys.VolumePreview, "调节结束试听", "确认选择", AudioCategory.UI, .4f);
            Add(asset, AudioKeys.PanelOpen, "打开弹窗 / 侧栏", null, AudioCategory.UI);
            Add(asset, AudioKeys.PanelClose, "关闭弹窗 / 侧栏", null, AudioCategory.UI);
            Add(asset, AudioKeys.MapComplete, "地图行动完成", "确认选择", AudioCategory.UI, .4f, provisional: true);
            Add(asset, AudioKeys.CupPickup, "取杯", null);
            Add(asset, AudioKeys.Place, "放下器皿", null);
            Add(asset, AudioKeys.Ingredient, "材料投入", null);
            Add(asset, AudioKeys.Pour, "液体倾倒", null, loop: true);
            Add(asset, AudioKeys.Grinder, "研磨运行", "研磨旋转", volume: .45f, loop: true);
            Add(asset, AudioKeys.GrinderTurn, "手动研磨加速", null);
            Add(asset, AudioKeys.Mixer, "搅拌机运行", null, loop: true);
            Add(asset, AudioKeys.Heating, "加热 / 沸腾", "水沸腾", volume: .3f, loop: true);
            Add(asset, AudioKeys.Extraction, "过滤滴水", null, loop: true);
            Add(asset, AudioKeys.Ready, "设备完成", "H-pitch wooooooodblock 高音调西洋木鱼", volume: .3f, provisional: true);
            Add(asset, AudioKeys.Froth, "即时打奶完成", null);
            Add(asset, AudioKeys.Bell, "出餐铃", "H-pitch wooooooodblock 高音调西洋木鱼", volume: .4f, provisional: true);
            Add(asset, AudioKeys.OrderSuccess, "出品 UI — 顾客满意", "确认选择", AudioCategory.UI, .55f, provisional: true);
            Add(asset, AudioKeys.OrderRemake, "出品 UI — 要求重做", "M-pitch wooooooodblock 中音调西洋木鱼", AudioCategory.UI, .5f, provisional: true);
            Add(asset, AudioKeys.OrderFailed, "出品 UI — 不满意离开", "L-pitch wooooooodblock 低音调西洋木鱼", AudioCategory.UI, .55f, provisional: true);
            Add(asset, AudioKeys.Discard, "丢弃杯子", null);
            Add(asset, AudioKeys.Empty, "清空容器", null);
            Add(asset, AudioKeys.LattePour, "拉花有效注奶", null, loop: true);
            Add(asset, AudioKeys.LatteComplete, "拉花提交完成", "确认选择", volume: .35f, provisional: true);
            Add(asset, AudioKeys.Arrive, "顾客实际到店", null);
            Add(asset, AudioKeys.Leave, "顾客实际离店", null);
            Add(asset, AudioKeys.Area, "切换大厅 / 制作台", null);
            Add(asset, AudioKeys.MenuMusic, "主菜单音乐", "Dorian-C#-一号主菜单(1)", AudioCategory.Music, .35f, true);
            Add(asset, AudioKeys.CafeMusic, "经营音乐", "休闲", AudioCategory.Music, .25f, true);
            Add(asset, AudioKeys.CityMusic, "城区音乐", "日常推进", AudioCategory.Music, .25f, true);
            Add(asset, AudioKeys.NightMusic, "夜晚音乐", "回忆", AudioCategory.Music, .25f, true);
            Add(asset, AudioKeys.CafeAmbience, "咖啡店环境", "洽谈", AudioCategory.Ambience, .08f, true, true);
            Add(asset, "ambience.city", "城区街道环境", "中世纪街道", AudioCategory.Ambience, .12f, true);
            Add(asset, AudioKeys.Date, "日期切换", "纸张翻动声", volume: .4f, provisional: true);
            Add(asset, AudioKeys.Open, "开店演出", "游戏进入音效", volume: .35f);
            Add(asset, AudioKeys.BusinessEnd, "营业结束", null);
            Add(asset, AudioKeys.DayEnd, "日结", null);
            Add(asset, AudioKeys.PageTurn, "阅读翻页", "纸张翻动声", AudioCategory.UI, .35f);
            Add(asset, AudioKeys.Envelope, "拆开信封", "纸张翻动声", volume: .45f, provisional: true);
            Add(asset, AudioKeys.Signature, "签名确认", "确认选择", volume: .4f, provisional: true);
            Add(asset, AudioKeys.Notice, "新通知展示", "M-pitch wooooooodblock 中音调西洋木鱼", AudioCategory.UI, .3f, provisional: true);
            foreach (string name in new[] { "巴洛克风格", "对峙，冲突前", "回忆", "紧张", "恐怖？", "日常推进", "时间流逝", "休闲", "阳光的音乐", "也许悲伤", "战斗！", "Eb 希望", "win！" })
                Add(asset, "music.track." + name, "剧情可引用音乐：" + name, name, AudioCategory.Music, .3f, true);
            AssetDatabase.CreateAsset(asset, CatalogPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            GetWindow<GameAudioWindow>("游戏音频配置").catalog = asset;
        }

        private static void Add(AudioCatalog asset, string key, string description, string filename,
            AudioCategory category = AudioCategory.Sfx, float volume = .5f, bool loop = false, bool provisional = false)
        {
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(filename))
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Art/audio" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), filename, StringComparison.Ordinal))
                    { clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); break; }
                }
            asset.entries.Add(new AudioCue { key = key, description = description, clip = clip, category = category,
                volume = volume, loop = loop, fadeIn = loop ? .15f : 0f, maxVoices = loop ? 8 : 3,
                cooldown = key == AudioKeys.Select ? 0f : .08f,
                provisional = provisional, materialNote = clip == null ? "尚缺对应素材，暂时静音。" : provisional ? "现有素材按文件名初配，临时替代；请试听确认或替换。" : "按文件名初配，请试听确认音量及循环衔接。" });
        }
    }
}
