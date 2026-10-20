using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Narrative
{
    [CreateAssetMenu(menuName = "Spotlight/Passive Check Presentation")]
    public sealed class PassiveCheckPresentationConfig : ScriptableObject
    {
        [Serializable] public sealed class Words { public string attributeId; public string[] candidates; }
        [Serializable] public sealed class FixedWord { public string checkId; public string word; }
        public Words[] words = {
            new Words { attributeId = "perception", candidates = new[] { "已察觉", "捕捉到细节", "觉出端倪" } },
            new Words { attributeId = "wisdom", candidates = new[] { "若有所悟", "理清头绪", "看出门道" } },
            new Words { attributeId = "language", candidates = new[] { "一语中的", "说到点上", "言有所中" } },
            new Words { attributeId = "courage", candidates = new[] { "鼓起勇气", "毫不退缩", "稳住心神" } }
        };
        public FixedWord[] fixedWords = Array.Empty<FixedWord>();
        private static readonly System.Random random = new System.Random();
        public string Choose(string checkId, string attributeId)
        {
            foreach (var entry in fixedWords)
                if (entry != null && entry.checkId == checkId && !string.IsNullOrWhiteSpace(entry.word)) return entry.word;
            var candidates = new List<string>();
            foreach (var entry in words)
                if (entry != null && entry.attributeId == attributeId && entry.candidates != null)
                    foreach (var word in entry.candidates) if (!string.IsNullOrWhiteSpace(word)) candidates.Add(word);
            if (candidates.Count > 0) return candidates[random.Next(candidates.Count)];
            Debug.LogWarning("[NCS] Missing passive check words: " + attributeId);
            return "有所领会";
        }
        public static string Hint(NarrativeAbilityCheckSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.passed || snapshot.values.Count != 1) return null;
            var value = snapshot.values[0];
            if (string.IsNullOrEmpty(snapshot.presentationWord))
            {
                var config = Resources.Load<PassiveCheckPresentationConfig>("Dialogue/PassiveCheckPresentation");
                if (config == null) Debug.LogWarning("[NCS] Missing passive check presentation config.");
                snapshot.presentationWord = config != null ? config.Choose(snapshot.checkId, value.attributeId) : "有所领会";
            }
            return "需要 " + value.displayName + " 到达 " + value.threshold + " · " + snapshot.presentationWord;
        }
    }
}
