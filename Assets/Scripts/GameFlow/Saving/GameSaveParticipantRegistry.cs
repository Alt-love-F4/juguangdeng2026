using System;
using System.Collections.Generic;

namespace Spotlight.GameFlow
{
    /// <summary>
    /// 玩法系统向 GameFlow 存档注册自身状态的静态入口。
    /// 玩法程序集注册 Capturers/Restorers，GameFlowBootstrap 负责采集；
    /// 读档时通过 PendingRestore 暂存，待玩法场景加载后由玩法侧领取。
    /// </summary>
    public static class GameSaveParticipantRegistry
    {
        public static readonly Dictionary<string, Func<string>> Capturers = new Dictionary<string, Func<string>>();
        public static readonly Dictionary<string, Action<string>> Restorers = new Dictionary<string, Action<string>>();

        /// <summary>读档后暂存的参与者数据，玩法场景加载后领取并清空。</summary>
        public static List<GameSaveParticipantData> PendingRestore;

        public static List<GameSaveParticipantData> CaptureAll()
        {
            var list = new List<GameSaveParticipantData>();
            foreach (var pair in Capturers)
            {
                string json = null;
                try { json = pair.Value?.Invoke(); }
                catch (Exception e) { UnityEngine.Debug.LogError("[GameSave] Participant capture failed: " + pair.Key + " " + e.Message); }
                if (!string.IsNullOrEmpty(json))
                    list.Add(new GameSaveParticipantData { Id = pair.Key, Json = json });
            }
            return list;
        }

        public static string TakePending(string id)
        {
            if (PendingRestore == null) return null;
            var entry = PendingRestore.Find(p => p.Id == id);
            if (entry != null) PendingRestore.Remove(entry);
            return entry?.Json;
        }
    }
}
