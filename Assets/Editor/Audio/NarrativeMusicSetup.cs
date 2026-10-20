using System;
using UnityEditor;
using UnityEngine;

namespace Spotlight.Audio.Editor
{
    /// <summary>Curated from the authored endings/conversations, not a runtime keyword classifier.</summary>
    public static class NarrativeMusicSetup
    {
        [MenuItem("Tools/Audio/Add Missing Narrative Music")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(GameAudioWindow.CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Create GameAudioCatalog first.");
            Undo.RecordObject(catalog, "Add narrative music assignments");
            int added = AddMissing(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[Audio] Added " + added + " narrative music cues; existing assignments preserved.");
        }

        public static int AddMissing(AudioCatalog catalog)
        {
            int before = catalog.entries.Count;
            Add(catalog, "music.track.avemaria弥撒", "弥撒 / 仪式与挽歌", "avemaria弥撒", false);
            Ending(catalog, "neutral.ending.a", "平凡之日：遗憾中的日常回望", "回忆");
            Ending(catalog, "neutral.ending.b", "远方的咖啡馆：重新安顿的温暖", "阳光的音乐");
            Ending(catalog, "neutral.ending.c", "沸点依旧：平凡生活继续", "休闲");
            Ending(catalog, "neutral.ending.d", "商业大亨：繁荣与旧日余韵", "巴洛克风格");
            Ending(catalog, "rose.ending.trust_limit.ending", "信任的界限：死亡后的回望", "也许悲伤");
            Ending(catalog, "rose.ending.job_requirements.ending", "工作需求要看清：棋子的悲剧", "也许悲伤");
            Ending(catalog, "rose.route.1115.s1.ending.ideal_world", "理想新世界：清洗与服从，非胜利庆典", "恐怖？");
            Ending(catalog, "rose.route.ending.unforgiven.ending", "无法原谅：告别与罪疚", "也许悲伤");
            Ending(catalog, "rose.route.ending.bloom.ending", "另一种绽放：保全生命后的新可能", "Eb 希望");
            Ending(catalog, "church.ending.if2", "茫然的历史参与者：迷失与死亡", "恐怖？");
            Ending(catalog, "church.ending.if4", "再见，亚拿尼亚：失去意识前的告别", "也许悲伤");
            Ending(catalog, "church.ending.ry1", "美丽新世界：期待不同的未来", "Eb 希望");
            Ending(catalog, "church.ending.ry2", "历史的分叉路：不甘与追问", "也许悲伤");
            Ending(catalog, "revolution.end01", "历史会判我无罪：血与信念的沉重挽歌", "avemaria弥撒");
            Ending(catalog, "revolution.end02", "历史尚未结束：继续走向明天", "Eb 希望");
            Ending(catalog, "arrival.ending.if0", "未开始就已结束：归乡与回望", "回忆");

            Conversations(catalog, "阳光的音乐", "开业、亲友和街市的暖意",
                "oct07.s1.conversation", "rose.1012.s1.conversation", "rose.1019.s1.conversation",
                "neutral.1010.s1", "neutral.1013.s2", "neutral.1030.s1", "church.1104.s1.conversation");
            Conversations(catalog, "日常推进", "经营、商谈与逐渐恢复的生活",
                "neutral.1018.s1", "neutral.route.1112.s1", "neutral.route.1129.s1", "neutral.route.1130.s1");
            Conversations(catalog, "map", "初访市场与街区",
                "neutral.1008.s1");
            Conversations(catalog, "休闲", "午后下棋与轻松交谈",
                "neutral.1027.s1");
            Conversations(catalog, "回忆", "亲情、旧账与自省",
                "revolution.1013.s2.conversation", "revolution.1028.s1.conversation",
                "rose.route.1111.s2.conversation", "neutral.route.1111.s2", "neutral.route.1118.s1");
            Conversations(catalog, "时间流逝", "等待与收拾残局",
                "neutral.route.1130.s2");
            Conversations(catalog, "巴洛克风格", "礼节、上流社会与政治试探",
                "neutral.1010.s2", "rose.1016.s1.conversation", "rose.1025.s2.conversation",
                "rose.1026.s2.conversation", "church.1014.s2.conversation", "church.1105.s1.conversation",
                "church.1105.s2.conversation", "church.route.1113.s1.conversation");
            Conversations(catalog, "恐怖？", "夜间密信、秘密与不祥",
                "oct07.s2.conversation", "rose.ending.trust_limit.conversation",
                "church.1023.s2.conversation");
            Conversations(catalog, "对峙，冲突前", "谈判、盘问与信任的试探",
                "rose.1009.s1.conversation", "rose.1011.s1.conversation", "rose.1101.s1.conversation",
                "rose.ending.job_requirements.conversation", "rose.route.1115.s1.conversation",
                "neutral.route.1111.s1", "church.1020.s1.conversation", "church.1023.s1.conversation",
                "church.1111.s1.conversation", "church.route.1112.s1.conversation",
                "church.route.1116.s1.conversation", "revolution.1014.s1.conversation",
                "revolution.1029.s1.conversation", "revolution.route.1112.s1.conversation");
            Conversations(catalog, "紧张", "封锁、失踪与风暴前夕",
                "rose.1024.s1.conversation", "rose.1024.s2.conversation", "rose.1103.s1.conversation",
                "rose.route.1122.s1.conversation", "neutral.route.1122.s1", "church.route.1115.s1.conversation",
                "revolution.1107.s1.conversation", "revolution.route.1115.s1.conversation",
                "revolution.route.1122.s1.conversation");
            Conversations(catalog, "也许悲伤", "诀别、罪疚与革命的代价",
                "rose.route.ending.unforgiven.conversation", "revolution.route.1124.s1.conversation");
            Conversations(catalog, "Eb 希望", "选择未来与保全生命",
                "rose.route.ending.bloom.conversation", "church.route.1111.s1.conversation",
                "church.route.1123.s1.conversation");
            Conversations(catalog, "战斗！", "行动之夜与革命爆发",
                "church.route.1118.s1.conversation", "revolution.route.1123.s1.conversation");
            Conversations(catalog, "avemaria弥撒", "神佑者仪式与宗教权威",
                "church.1009.s2.conversation");
            return catalog.entries.Count - before;
        }

        private static void Ending(AudioCatalog c, string id, string description, string track)
            => Add(c, NarrativeMusic.EndingPrefix + id, "结局 / " + description, track, true);

        private static void Conversations(AudioCatalog c, string track, string description, params string[] ids)
        {
            foreach (string id in ids)
                Add(c, NarrativeMusic.ConversationPrefix + id, "长剧情 / " + description + " / " + id, track, false);
        }

        private static void Add(AudioCatalog c, string key, string description, string track, bool ending)
        {
            if (c.entries.Exists(e => e != null && e.key == key)) return;
            string path = "Assets/Art/audio/BGM/" + track + ".mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing BGM: " + path);
            // Mean level varies by ~19 dB across the folder. These conservative gains
            // soften louder tracks without boosting the quietest tracks into peak clipping.
            float volume = track == "map" ? .13f : track == "战斗！" ? .18f
                : track == "也许悲伤" || track == "avemaria弥撒" || track == "休闲" ? .22f
                : track == "回忆" ? .38f : track == "Eb 希望" || track == "巴洛克风格" ? .32f : .27f;
            c.entries.Add(new AudioCue
            {
                key = key, description = description, clip = clip, category = AudioCategory.Music,
                loop = true, volume = volume * (ending ? 1f : .85f), fadeIn = ending ? 1.5f : 1.2f,
                fadeOut = 1.2f, cooldown = 0f, maxVoices = 1,
                materialNote = "按剧情文本与素材命名初配；已测量时长和平均/峰值电平，尚需实际试听确认情绪、对白下音量和循环接缝。"
            });
        }
    }
}
