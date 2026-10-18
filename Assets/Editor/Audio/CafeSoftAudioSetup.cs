using UnityEditor;
using UnityEngine;

namespace Spotlight.Audio.Editor
{
    /// <summary>Explicit installer: never replaces a catalog or touches non-cafe cues.</summary>
    public static class CafeSoftAudioSetup
    {
        private const string Root = "Assets/Art/audio/SFX/CafeSoft/";

        [MenuItem("Tools/Audio/Apply Soft Cafe Sounds")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(GameAudioWindow.CatalogPath);
            if (catalog == null) throw new System.InvalidOperationException("Create the Game Audio catalog first.");
            Undo.RecordObject(catalog, "Configure soft cafe sounds");
            Set(catalog, AudioKeys.Pickup, "拿起道具 / 材料", "ClothPickup", .36f);
            Set(catalog, AudioKeys.BeansPickup, "咖啡豆拿起 / 拖动轻响", "BeansRustle", .32f, cooldown: .20f);
            Set(catalog, AudioKeys.BeansAdd, "咖啡豆投入研磨机", "BeansAdd", .43f);
            Set(catalog, AudioKeys.PowderAdd, "咖啡粉 / 可可 / 抹茶 / 糖投入", "PowderAdd", .4f);
            Set(catalog, AudioKeys.LiquidAdd, "液体材料放入", "LiquidSplash", .3f);
            Set(catalog, AudioKeys.MachineSwitch, "设备实际启动 / 操作开关", "MachineSwitch", .3f, cooldown: .15f);
            Set(catalog, AudioKeys.Stove, "炉子加热（随温度渐强）", "HeatingLoop", .32f, true);
            Set(catalog, AudioKeys.Spill, "有效溢出 / 倒空地面", "LiquidPourLoop", .18f, true);
            Set(catalog, AudioKeys.DragCancel, "取消拿取 / 道具归位", "SoftTouch", .24f);
            Set(catalog, AudioKeys.CupPickup, "取杯 / 拿起器皿", "CupLift", .4f);
            Set(catalog, AudioKeys.Place, "放下器皿 / 叠杯 / 摆放", "CupSetDown", .42f);
            Set(catalog, AudioKeys.Ingredient, "普通材料投入", "PowderAdd", .38f);
            Set(catalog, AudioKeys.Pour, "液体倾倒（随实际流量与接收液位变化）", "LiquidPourLoop", .43f, true);
            Set(catalog, AudioKeys.Grinder, "柔和研磨运行", "GrinderLoop", .30f, true);
            Set(catalog, AudioKeys.GrinderTurn, "手动研磨轻转", "GrinderTurn", .32f, cooldown: .12f);
            Set(catalog, AudioKeys.Mixer, "搅拌运行", "GrinderLoop", .20f, true, pitch: .82f);
            Set(catalog, AudioKeys.Heating, "热水壶独立加热 / 沸腾", "HeatingLoop", .32f, true);
            Set(catalog, AudioKeys.Extraction, "滤杯有效滴滤", "LiquidDripLoop", .23f, true);
            Set(catalog, AudioKeys.Ready, "设备完成轻提示", "CupLift", .25f, pitch: 1.2f);
            Set(catalog, AudioKeys.Froth, "打奶 / 奶泡完成", "MilkFoam", .32f);
            Set(catalog, AudioKeys.Bell, "柔和出餐铃", "ServiceBell", .32f);
            Set(catalog, AudioKeys.Discard, "丢弃物品", "SoftTouch", .32f);
            Set(catalog, AudioKeys.Empty, "清空容器", "LiquidSplash", .28f);
            Set(catalog, AudioKeys.LattePour, "拉花有效注奶", "LiquidPourLoop", .23f, true, pitch: .92f);
            Set(catalog, AudioKeys.LatteComplete, "拉花提交轻提示", "CupLift", .26f, pitch: 1.12f);
            Set(catalog, AudioKeys.Arrive, "顾客入座轻脚步", "ClothPickup", .18f);
            Set(catalog, AudioKeys.Leave, "顾客离席轻脚步", "ClothPickup", .15f, pitch: .94f);
            Set(catalog, AudioKeys.Area, "区域切换轻响", "ClothPickup", .18f);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[CafeAudio] Configured 28 soft cafe cues. Existing music, UI and result cues preserved.");
        }

        private static void Set(AudioCatalog catalog, string key, string description, string file, float volume,
            bool loop = false, float pitch = 1f, float cooldown = .08f)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + file + ".wav");
            if (clip == null) throw new System.InvalidOperationException("Missing cafe clip: " + file);
            var cue = catalog.entries.Find(item => item != null && item.key == key);
            if (cue == null) { cue = new AudioCue { key = key }; catalog.entries.Add(cue); }
            cue.description = description; cue.clip = clip; cue.resourcePath = string.Empty;
            cue.enabled = true; cue.category = AudioCategory.Sfx; cue.loop = loop;
            cue.volume = volume; cue.pitch = pitch; cue.cooldown = cooldown;
            cue.fadeIn = loop ? .16f : .008f; cue.fadeOut = loop ? .18f : .05f;
            cue.maxVoices = loop ? 4 : 3; cue.provisional = false;
            cue.materialNote = "柔和滤波、峰值控制与淡入淡出；循环已交叉处理。来源和署名见 Assets/Art/audio/SFX/CafeSoft/CREDITS.md。搅拌/脚步/完成提示为拟音设计，可统一替换。";
        }
    }
}
