using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    public enum LatteArtTemplateKind { None, Heart, Rosetta, X, A, Tulip }

    public readonly struct LatteArtRecognitionResult
    {
        public readonly string patternId;
        public readonly float confidence;
        public readonly int pointCount;
        public LatteArtRecognitionResult(string id, float score, int points)
        {
            patternId = id ?? string.Empty; confidence = Mathf.Clamp01(score); pointCount = points;
        }
        public bool HasPattern => !string.IsNullOrEmpty(patternId);
        public override string ToString() => HasPattern ? patternId + " " + (confidence * 100f).ToString("0") + "%" : "未识别";
    }

    [Serializable]
    public sealed class LatteArtPatternPreset
    {
        public string id = "heart";
        public string displayName = "爱心";
        public LatteArtTemplateKind template = LatteArtTemplateKind.Heart;
        public Texture2D preview;
    }

    /// <summary>对玩家单次拉花轨迹做轻量模板匹配。模板是可扩展的，识别结果只作为玩法输入，不修改库存。</summary>
    public static class LatteArtPatternRecognizer
    {
        private const int Samples = 32;
        private static readonly Dictionary<LatteArtTemplateKind, Vector2[]> Templates = new Dictionary<LatteArtTemplateKind, Vector2[]>
        {
            { LatteArtTemplateKind.Heart, MakeHeart() },
            { LatteArtTemplateKind.Rosetta, MakeRosetta() },
            { LatteArtTemplateKind.X, MakeX() },
            { LatteArtTemplateKind.A, MakeA() },
            { LatteArtTemplateKind.Tulip, MakeTulip() }
        };

        public static LatteArtRecognitionResult Recognize(IList<Vector2> points, IList<LatteArtPatternPreset> presets = null)
        {
            if (points == null || points.Count < 5) return new LatteArtRecognitionResult(string.Empty, 0, points == null ? 0 : points.Count);
            var path = Normalize(Resample(points, Samples));
            float best = float.MaxValue; LatteArtPatternPreset bestPreset = null;
            if (presets != null && presets.Count > 0)
            {
                foreach (var preset in presets)
                {
                    if (preset == null || preset.template == LatteArtTemplateKind.None || !Templates.ContainsKey(preset.template)) continue;
                    float distance = Distance(path, Templates[preset.template]);
                    if (distance < best) { best = distance; bestPreset = preset; }
                }
            }
            else
            {
                foreach (var pair in Templates)
                {
                    float distance = Distance(path, pair.Value);
                    if (distance < best)
                    {
                        best = distance;
                        bestPreset = new LatteArtPatternPreset { id = pair.Key.ToString().ToLowerInvariant(), displayName = pair.Key.ToString(), template = pair.Key };
                    }
                }
            }
            if (bestPreset == null) return new LatteArtRecognitionResult(string.Empty, 0, points.Count);
            float confidence = Mathf.Clamp01(1f - best / 0.95f);
            return new LatteArtRecognitionResult(bestPreset.id, confidence, points.Count);
        }

        private static Vector2[] Resample(IList<Vector2> source, int count)
        {
            var result = new Vector2[count];
            if (source.Count == 1) { for (int i = 0; i < count; i++) result[i] = source[0]; return result; }
            float total = 0;
            for (int i = 1; i < source.Count; i++) total += Vector2.Distance(source[i - 1], source[i]);
            if (total < .0001f) { for (int i = 0; i < count; i++) result[i] = source[0]; return result; }
            result[0] = source[0]; float step = total / (count - 1), distance = 0; int index = 1; Vector2 previous = source[0];
            for (int sample = 1; sample < count; sample++)
            {
                float target = step * sample;
                while (index < source.Count && distance + Vector2.Distance(previous, source[index]) < target)
                {
                    distance += Vector2.Distance(previous, source[index]); previous = source[index++];
                }
                if (index >= source.Count) result[sample] = source[source.Count - 1];
                else
                {
                    float segment = Vector2.Distance(previous, source[index]);
                    float t = segment < .0001f ? 0 : (target - distance) / segment;
                    result[sample] = Vector2.Lerp(previous, source[index], Mathf.Clamp01(t));
                }
            }
            return result;
        }

        private static Vector2[] Normalize(Vector2[] points)
        {
            Vector2 min = points[0], max = points[0];
            for (int i = 1; i < points.Length; i++) { min = Vector2.Min(min, points[i]); max = Vector2.Max(max, points[i]); }
            Vector2 center = (min + max) * .5f; float scale = Mathf.Max(max.x - min.x, max.y - min.y, .0001f);
            var normalized = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) normalized[i] = (points[i] - center) / scale + Vector2.one * .5f;
            return normalized;
        }

        private static float Distance(Vector2[] a, Vector2[] b)
        {
            float forward = 0, reverse = 0;
            for (int i = 0; i < a.Length; i++) { forward += Vector2.Distance(a[i], b[i]); reverse += Vector2.Distance(a[i], b[b.Length - 1 - i]); }
            return Mathf.Min(forward, reverse) / a.Length;
        }

        private static Vector2[] MakeHeart() { return Poly(new Vector2(.5f,.16f), new Vector2(.22f,.42f), new Vector2(.2f,.65f), new Vector2(.34f,.82f), new Vector2(.5f,.68f), new Vector2(.66f,.82f), new Vector2(.8f,.65f), new Vector2(.78f,.42f), new Vector2(.5f,.16f)); }
        private static Vector2[] MakeRosetta() { return Poly(new Vector2(.5f,.18f), new Vector2(.5f,.82f), new Vector2(.25f,.48f), new Vector2(.75f,.42f), new Vector2(.28f,.35f), new Vector2(.72f,.29f), new Vector2(.4f,.22f), new Vector2(.6f,.18f)); }
        private static Vector2[] MakeX() { return Poly(new Vector2(.2f,.2f), new Vector2(.8f,.8f), new Vector2(.5f,.5f), new Vector2(.8f,.2f), new Vector2(.2f,.8f)); }
        private static Vector2[] MakeA() { return Poly(new Vector2(.2f,.18f), new Vector2(.5f,.82f), new Vector2(.8f,.18f), new Vector2(.68f,.46f), new Vector2(.32f,.46f)); }
        private static Vector2[] MakeTulip() { return Poly(new Vector2(.5f,.16f), new Vector2(.5f,.4f), new Vector2(.25f,.62f), new Vector2(.32f,.8f), new Vector2(.5f,.62f), new Vector2(.68f,.8f), new Vector2(.75f,.62f), new Vector2(.5f,.4f)); }
        private static Vector2[] Poly(params Vector2[] points) { return Normalize(Resample(points, Samples)); }
    }
}
