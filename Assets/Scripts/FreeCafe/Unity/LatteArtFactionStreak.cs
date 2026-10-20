using System.Collections;
using System.Collections.Generic;
using System.Text;
using Spotlight.GameFlow;
using Spotlight.Narrative;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    /// <summary>
    /// 连续把同一阵营徽记的拉花交给客人：第 3 杯时该阵营倾向 +3，其余阵营倾向各 −1。
    /// 计数随咖啡馆存档（CafeSnapshot）；倾向只写现有 NCS faction.* bar，不另建一份数值。
    /// 王庭与教会两个徽记共用 faction.church，因此混合交付也算连续。
    /// </summary>
    public static class LatteArtFactionStreak
    {
        public const int StreakLength = 3, Reward = 3, Penalty = -1;
        public const string AutomaticPattern = "automation";
        public static readonly string[] TendencyBars = { "faction.neutral", "faction.rose", "faction.church", "faction.revolution" };

        /// <summary>Faction tendency bar for a recognised emblem pattern; null for everyday/unknown/automatic art.</summary>
        public static string BarFor(string patternId)
        {
            if (string.IsNullOrEmpty(patternId)) return null;
            foreach (var preset in FreeCafeLatteArtBench.BuiltinPresets())
                if (preset.id == patternId)
                    return string.IsNullOrEmpty(preset.factionId) ? null : WorldFactionCatalog.Find(preset.factionId)?.TendencyBarKey;
            return null;
        }

        /// <summary>
        /// Book one cup that was handed to a customer. Only latte-art cups count: an everyday or unrecognised
        /// pattern breaks the streak, a drink without latte art leaves it untouched. Returns the bar to reward
        /// when the streak completes (the streak then starts over), otherwise null.
        /// </summary>
        public static string Record(CafeSnapshot state, bool latteArt, string patternId)
        {
            // Staff automatic latte art is not the player's choice of pattern: neither counts nor breaks.
            if (state == null || !latteArt || patternId == AutomaticPattern) return null;
            string bar = BarFor(patternId);
            if (bar == null)
            {
                state.latteFactionStreakKey = null;
                state.latteFactionStreakCount = 0;
                return null;
            }
            if (state.latteFactionStreakKey == bar) state.latteFactionStreakCount++;
            else { state.latteFactionStreakKey = bar; state.latteFactionStreakCount = 1; }
            if (state.latteFactionStreakCount < StreakLength) return null;
            state.latteFactionStreakKey = null;
            state.latteFactionStreakCount = 0;
            return bar;
        }

        /// <summary>
        /// Apply the reward through NCS at its next safe boundary (a story cafe order may still own the
        /// presentation), then show the rising toast after the delivery toast has had its turn.
        /// </summary>
        public static void Award(MonoBehaviour host, string bar, string patternId)
        {
            var runtime = NarrativeRuntime.Instance;
            var controller = runtime != null && runtime.IsReady ? runtime.Controller : null;
            if (controller == null || host == null) return;
            controller.RunAtSafeBoundary(() =>
            {
                string rewarded = null;
                var others = new List<string>();
                foreach (string key in TendencyBars)
                {
                    int delta = key == bar ? Reward : Penalty;
                    if (!controller.State.TryGetBar(key, out int before)) continue;
                    controller.AddBar(key, delta);
                    controller.State.TryGetBar(key, out int after);
                    string line = TendencyName(key) + " " + (after == before ? (delta > 0 ? "已达上限" : "已达下限")
                        : (after > before ? "+" : "−") + Mathf.Abs(after - before));
                    if (key == bar) rewarded = line; else others.Add(line);
                }
                if (rewarded == null && others.Count == 0) return;
                // Three lines fit the existing toast banner: cause, reward, the other tendencies.
                var message = new StringBuilder("连续 " + StreakLength + " 杯「" + PatternName(patternId) + "」拉花");
                if (rewarded != null) message.Append('\n').Append(rewarded);
                if (others.Count > 0) message.Append('\n').Append(string.Join("　", others));
                if (host != null && host.isActiveAndEnabled) host.StartCoroutine(ShowAfterDeliveryToast(message.ToString()));
                else OperationToastPanel.ShowMessage(message.ToString(), string.Empty);
            });
        }

        private static IEnumerator ShowAfterDeliveryToast(string message)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            OperationToastPanel.ShowMessage(message, string.Empty);
        }

        private static string TendencyName(string bar)
        {
            foreach (var faction in WorldFactionCatalog.All)
                if (faction.TendencyBarKey == bar) return faction.TendencyName;
            return bar;
        }

        private static string PatternName(string patternId)
        {
            foreach (var preset in FreeCafeLatteArtBench.BuiltinPresets())
                if (preset.id == patternId) return preset.displayName;
            return patternId;
        }
    }
}
