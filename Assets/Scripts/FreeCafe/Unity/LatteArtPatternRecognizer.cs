using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.FreeCafe
{
    // Append only: values are serialized in scenes. X/A/Crown stay for saved cups and older presets.
    public enum LatteArtTemplateKind { None, Heart, Rosetta, X, A, Tulip, Crown, Bean, SwordCrown, ConcordRing, NewRose, BrokenChain }

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
        [TextArea] public string meaning;
        [Tooltip("可选：WorldFactionCatalog 的势力 ID。填写后，玩家遇到该势力前图案保持未解锁。")]
        public string factionId;
        [Tooltip("悬停卡片中的笔顺提示。")]
        public string drawingTip;
    }

    public interface ILatteArtFeedbackSurface
    {
        void GetPatternFits(IList<LatteArtPatternPreset> presets, IList<LatteArtRecognitionResult> results);
        void SetLiquidColor(Color color);
        void SetResetHandler(Action handler);
    }

    /// <summary>Stroke-aware, bounded geometric fitting. Recognition never changes cafe inventory.</summary>
    public static class LatteArtPatternRecognizer
    {
        private const int Samples = 96;
        private const float MaximumFitError = .095f;
        private static readonly Vector2 Break = new Vector2(float.NaN, float.NaN);
        private static readonly Dictionary<LatteArtTemplateKind, Vector2[]> Templates = new Dictionary<LatteArtTemplateKind, Vector2[]>
        {
            { LatteArtTemplateKind.Crown, new[] { new Vector2(.2f,.25f), new Vector2(.15f,.72f), new Vector2(.35f,.5f), new Vector2(.5f,.85f), new Vector2(.65f,.5f), new Vector2(.85f,.72f), new Vector2(.8f,.25f), new Vector2(.2f,.25f) } },
            { LatteArtTemplateKind.Heart, new[] { new Vector2(.5f,.16f), new Vector2(.22f,.42f), new Vector2(.2f,.65f), new Vector2(.34f,.82f), new Vector2(.5f,.68f), new Vector2(.66f,.82f), new Vector2(.8f,.65f), new Vector2(.78f,.42f), new Vector2(.5f,.16f) } },
            { LatteArtTemplateKind.Rosetta, new[] { new Vector2(.5f,.18f), new Vector2(.5f,.82f), new Vector2(.25f,.48f), new Vector2(.75f,.42f), new Vector2(.28f,.35f), new Vector2(.72f,.29f), new Vector2(.4f,.22f), new Vector2(.6f,.18f) } },
            { LatteArtTemplateKind.X, new[] { new Vector2(.2f,.2f), new Vector2(.8f,.8f), Break, new Vector2(.8f,.2f), new Vector2(.2f,.8f) } },
            { LatteArtTemplateKind.A, new[] { new Vector2(.2f,.18f), new Vector2(.5f,.82f), new Vector2(.8f,.18f), Break, new Vector2(.33f,.46f), new Vector2(.67f,.46f) } },
            { LatteArtTemplateKind.Tulip, new[] { new Vector2(.5f,.16f), new Vector2(.5f,.4f), new Vector2(.25f,.62f), new Vector2(.32f,.8f), new Vector2(.5f,.62f), new Vector2(.68f,.8f), new Vector2(.75f,.62f), new Vector2(.5f,.4f) } },
            // Bean: closed oval plus the centre crease.
            { LatteArtTemplateKind.Bean, Join(Ellipse(new Vector2(.5f,.5f), .27f, .38f, 0, 360, 32), new[] { new Vector2(.5f,.86f), new Vector2(.43f,.66f), new Vector2(.57f,.34f), new Vector2(.5f,.14f) }) },
            // Court: crown outline pierced by a single sword blade (two strokes).
            { LatteArtTemplateKind.SwordCrown, Join(new[] { new Vector2(.24f,.40f), new Vector2(.18f,.70f), new Vector2(.36f,.55f), new Vector2(.5f,.78f), new Vector2(.64f,.55f), new Vector2(.82f,.70f), new Vector2(.76f,.40f), new Vector2(.24f,.40f) },
                new[] { new Vector2(.5f,.95f), new Vector2(.5f,.05f) }) },
            // Church: the concord ring with its central diamond.
            { LatteArtTemplateKind.ConcordRing, Join(Ellipse(new Vector2(.5f,.5f), .42f, .42f, 90, 450, 40), new[] { new Vector2(.5f,.72f), new Vector2(.63f,.5f), new Vector2(.5f,.28f), new Vector2(.37f,.5f), new Vector2(.5f,.72f) }) },
            // New rose: five-petal outline and a stem.
            { LatteArtTemplateKind.NewRose, Join(Petals(new Vector2(.5f,.64f), .21f, .09f, 5, 60), new[] { new Vector2(.5f,.40f), new Vector2(.5f,.05f) }) },
            // Revolution: two chain links with the break facing each other.
            { LatteArtTemplateKind.BrokenChain, Join(Rotate(Ellipse(new Vector2(.31f,.5f), .2f, .13f, 35, 325, 28), 22), Rotate(Ellipse(new Vector2(.69f,.5f), .2f, .13f, 215, 505, 28), 22)) }
        };
        private static readonly Dictionary<LatteArtTemplateKind, Shape> Shapes = BuildTemplates();

        // Compatibility: callers supplying one continuous path keep the existing API.
        public static LatteArtRecognitionResult Recognize(IList<Vector2> points, IList<LatteArtPatternPreset> presets = null)
            => Recognize(points, presets, null);

        /// <param name="strokeStarts">Ascending zero-based start indices. No length or samples are
        /// attributed to pen-up motion between strokes. NaN/NaN separators are also supported.</param>
        public static LatteArtRecognitionResult Recognize(IList<Vector2> points, IList<LatteArtPatternPreset> presets, IList<int> strokeStarts)
        {
            int count = points == null ? 0 : points.Count;
            var empty = new LatteArtRecognitionResult(string.Empty, 0, count);
            if (count < 5 || !TryBuildShape(points, strokeStarts, out Shape path)) return empty;
            float best = float.MaxValue;
            string bestId = string.Empty;
            if (presets != null && presets.Count > 0)
            {
                foreach (var preset in presets)
                {
                    if (preset == null || string.IsNullOrEmpty(preset.id) || !Shapes.TryGetValue(preset.template, out Shape template)) continue;
                    float error = Fit(path, template);
                    if (error < best) { best = error; bestId = preset.id; }
                }
            }
            else
            {
                foreach (var pair in Shapes)
                {
                    float error = Fit(path, pair.Value);
                    if (error < best) { best = error; bestId = pair.Key.ToString().ToLowerInvariant(); }
                }
            }
            // Being the nearest template is not enough: arbitrary marks must remain unrecognized.
            float confidence = Mathf.Clamp01(1f - best / .22f);
            return best <= MaximumFitError ? new LatteArtRecognitionResult(bestId, confidence, count) : empty;
        }

        /// <summary>All template fits, including below-threshold candidates, for live feedback. Does not force recognition.</summary>
        public static void GetPatternFits(IList<Vector2> points, IList<LatteArtPatternPreset> presets,
            IList<int> strokeStarts, IList<LatteArtRecognitionResult> results)
        {
            results.Clear();
            if (presets == null) return;
            int count = points == null ? 0 : points.Count;
            Shape path = null;
            bool valid = count >= 5 && TryBuildShape(points, strokeStarts, out path);
            foreach (var preset in presets)
            {
                float confidence = 0;
                if (valid && preset != null && Shapes.TryGetValue(preset.template, out Shape template))
                    confidence = Mathf.Clamp01(1f - Fit(path, template) / .22f);
                results.Add(new LatteArtRecognitionResult(preset != null ? preset.id : "", confidence, count));
            }
        }

        /// <summary>Safe copy in normalized preview coordinates. NaN/NaN denotes pen-up.</summary>
        public static Vector2[] PreviewPath(LatteArtTemplateKind kind)
        {
            if (!Templates.TryGetValue(kind, out Vector2[] source)) return Array.Empty<Vector2>();
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            foreach (var point in source) if (IsFinite(point)) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            Vector2 center = (min + max) * .5f;
            float scale = Mathf.Max(max.x - min.x, max.y - min.y);
            var copy = new Vector2[source.Length];
            for (int i = 0; i < source.Length; i++) copy[i] = IsFinite(source[i]) ? (source[i] - center) / scale + Vector2.one * .5f : Break;
            return copy;
        }

        private static Vector2[] Ellipse(Vector2 center, float rx, float ry, float fromDegrees, float toDegrees, int segments)
        {
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
            }
            return points;
        }
        private static Vector2[] Petals(Vector2 center, float radius, float depth, int petals, int segments)
        {
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * .5f + i * Mathf.PI * 2 / segments;
                // |cos| gives rounded petals that meet in shallow notches, matching the emblem silhouette.
                float r = radius - depth + depth * Mathf.Abs(Mathf.Cos((angle - Mathf.PI * .5f) * petals * .5f));
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            return points;
        }
        private static Vector2[] Rotate(Vector2[] points, float degrees)
        {
            float c = Mathf.Cos(degrees * Mathf.Deg2Rad), s = Mathf.Sin(degrees * Mathf.Deg2Rad);
            var result = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i] - Vector2.one * .5f;
                result[i] = new Vector2(c * p.x - s * p.y, s * p.x + c * p.y) + Vector2.one * .5f;
            }
            return result;
        }
        private static Vector2[] Join(params Vector2[][] strokes)
        {
            var result = new List<Vector2>();
            foreach (var stroke in strokes)
            {
                if (result.Count > 0) result.Add(Break);
                result.AddRange(stroke);
            }
            return result.ToArray();
        }

        private sealed class Shape
        {
            public Vector2[] samples;
            public float length;
        }
        private readonly struct Segment
        {
            public readonly Vector2 a, b;
            public readonly float length;
            public Segment(Vector2 start, Vector2 end) { a = start; b = end; length = Vector2.Distance(start, end); }
        }
        private static bool IsFinite(Vector2 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y);
        private static bool IsBreak(Vector2 p) => float.IsNaN(p.x) && float.IsNaN(p.y);
        private static Dictionary<LatteArtTemplateKind, Shape> BuildTemplates()
        {
            var result = new Dictionary<LatteArtTemplateKind, Shape>();
            foreach (var pair in Templates) if (TryBuildShape(pair.Value, null, out Shape shape)) result.Add(pair.Key, shape);
            return result;
        }
        private static bool TryBuildShape(IList<Vector2> points, IList<int> starts, out Shape shape)
        {
            shape = null;
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            int finiteCount = 0;
            foreach (var point in points)
            {
                if (IsBreak(point)) continue;
                if (!IsFinite(point)) return false;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point); finiteCount++;
            }
            Vector2 size = max - min;
            float scale = Mathf.Max(size.x, size.y);
            if (finiteCount < 4 || scale < .0001f || Mathf.Min(size.x, size.y) / scale < .15f) return false;
            if (starts != null)
            {
                for (int i = 0; i < starts.Count; i++)
                    if (starts[i] < 0 || starts[i] >= points.Count || (i > 0 && starts[i] <= starts[i - 1])) return false;
            }
            var segments = new List<Segment>();
            Vector2 center = (min + max) * .5f, previous = default;
            bool havePrevious = false;
            int boundary = 0;
            float length = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (starts != null && boundary < starts.Count && i == starts[boundary]) { havePrevious = false; boundary++; }
                if (IsBreak(points[i])) { havePrevious = false; continue; }
                Vector2 current = (points[i] - center) / scale;
                if (havePrevious)
                {
                    var segment = new Segment(previous, current);
                    if (segment.length > .00001f) { segments.Add(segment); length += segment.length; }
                }
                previous = current; havePrevious = true;
            }
            if (length < .4f || segments.Count == 0) return false;
            // Uniform arc-length samples only interpolate real segments, never pen-up gaps.
            var samples = new Vector2[Samples];
            int index = 0;
            float accumulated = 0;
            for (int i = 0; i < Samples; i++)
            {
                float target = length * (i + .5f) / Samples;
                while (index < segments.Count - 1 && accumulated + segments[index].length < target)
                    accumulated += segments[index++].length;
                Segment segment = segments[index];
                samples[i] = Vector2.Lerp(segment.a, segment.b, (target - accumulated) / segment.length);
            }
            shape = new Shape { samples = samples, length = length };
            return true;
        }
        private static float Fit(Shape path, Shape template)
        {
            float ratio = path.length / template.length;
            // Reject missing strokes, heavily retraced lines and high-complexity scribbles.
            if (ratio < .65f || ratio > 2.1f) return float.MaxValue;
            float best = float.MaxValue;
            var rotated = new Vector2[Samples];
            for (int degrees = -20; degrees <= 20; degrees += 5)
            {
                float angle = degrees * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                for (int i = 0; i < Samples; i++)
                {
                    Vector2 p = path.samples[i];
                    rotated[i] = new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
                }
                float error = GeometricError(rotated, template.samples);
                error += Mathf.Max(0, ratio - 1.35f) * .08f;
                best = Mathf.Min(best, error);
            }
            return best;
        }
        private static float GeometricError(Vector2[] a, Vector2[] b)
        {
            // Bidirectional coverage tolerates start point, stroke order, reversal and speed,
            // while penalizing both extra marks AND missing parts of a template.
            float total = 0, worst = 0;
            for (int direction = 0; direction < 2; direction++)
            {
                Vector2[] from = direction == 0 ? a : b, to = direction == 0 ? b : a;
                for (int i = 0; i < from.Length; i++)
                {
                    float nearest = float.MaxValue;
                    for (int j = 0; j < to.Length; j++) nearest = Mathf.Min(nearest, (from[i] - to[j]).sqrMagnitude);
                    float distance = Mathf.Sqrt(nearest);
                    total += distance; worst = Mathf.Max(worst, distance);
                }
            }
            return total / (a.Length + b.Length) * .7f + worst * .3f;
        }
    }
}
