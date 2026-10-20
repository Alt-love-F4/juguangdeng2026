using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Spotlight.FreeCafe.Tests
{
    public sealed class LatteArtPatternRecognizerTests
    {
        [TestCase(LatteArtTemplateKind.Heart, "heart")]
        [TestCase(LatteArtTemplateKind.Rosetta, "rosetta")]
        [TestCase(LatteArtTemplateKind.X, "x")]
        [TestCase(LatteArtTemplateKind.A, "a")]
        [TestCase(LatteArtTemplateKind.Tulip, "tulip")]
        [TestCase(LatteArtTemplateKind.Bean, "bean")]
        [TestCase(LatteArtTemplateKind.SwordCrown, "swordcrown")]
        [TestCase(LatteArtTemplateKind.ConcordRing, "concordring")]
        [TestCase(LatteArtTemplateKind.NewRose, "newrose")]
        [TestCase(LatteArtTemplateKind.BrokenChain, "brokenchain")]
        public void TemplatesRecognizeWithUnevenSamplingAndReverseDirection(LatteArtTemplateKind kind, string id)
        {
            var points = Densify(LatteArtPatternRecognizer.PreviewPath(kind));
            points.Reverse();
            var result = LatteArtPatternRecognizer.Recognize(points);
            Assert.That(result.patternId, Is.EqualTo(id));
            Assert.That(result.confidence, Is.GreaterThan(.85f));
            Assert.That(result.pointCount, Is.EqualTo(points.Count));
        }

        [Test]
        public void ClosedHeartAcceptsDifferentStartTranslationScaleAndModestRotation()
        {
            var source = LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.Heart);
            var shifted = new List<Vector2>();
            for (int i = 0; i < source.Length; i++) shifted.Add(source[(i + 3) % (source.Length - 1)]);
            var points = Densify(shifted);
            float c = Mathf.Cos(12f * Mathf.Deg2Rad), s = Mathf.Sin(12f * Mathf.Deg2Rad);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i] - Vector2.one * .5f;
                // Small deterministic hand-drawn error, not just a perfect template.
                p += new Vector2(Mathf.Sin(i * 1.7f), Mathf.Cos(i * .9f)) * .008f;
                points[i] = new Vector2(c * p.x - s * p.y, s * p.x + c * p.y) * 3.2f + new Vector2(12, -5);
            }
            var result = LatteArtPatternRecognizer.Recognize(points);
            Assert.That(result.patternId, Is.EqualTo("heart"));
            Assert.That(result.confidence, Is.GreaterThan(.65f));
        }

        [Test]
        public void SeparateAStrokesDoNotCreateBridgeAndMayBeDrawnInEitherOrder()
        {
            var points = new List<Vector2>();
            points.AddRange(Densify(new[] { new Vector2(.67f,.46f), new Vector2(.33f,.46f) }));
            int nextStart = points.Count;
            points.AddRange(Densify(new[] { new Vector2(.8f,.18f), new Vector2(.5f,.82f), new Vector2(.2f,.18f) }));
            var result = LatteArtPatternRecognizer.Recognize(points, null, new[] { 0, nextStart });
            Assert.That(result.patternId, Is.EqualTo("a"));
            Assert.That(result.confidence, Is.GreaterThan(.85f));
            // Adding the pen-up line geometrically changes the mark and must not score as well.
            var bridged = LatteArtPatternRecognizer.Recognize(points);
            Assert.That(bridged.HasPattern && bridged.confidence >= result.confidence, Is.False);
        }

        [Test]
        public void MissingXDiagonalIsRejectedInsteadOfInferringPenUpMotion()
        {
            var points = new[] { new Vector2(.2f,.2f), new Vector2(.5f,.5f), new Vector2(.8f,.8f), new Vector2(.8f,.2f), new Vector2(.2f,.8f) };
            // Last two points are separate taps, not a second diagonal.
            var result = LatteArtPatternRecognizer.Recognize(points, null, new[] { 0, 3, 4 });
            Assert.That(result.HasPattern, Is.False);
        }

        [Test]
        public void StraightLineStationaryInputAndInvalidCoordinatesAreRejected()
        {
            Assert.That(LatteArtPatternRecognizer.Recognize(null).HasPattern, Is.False);
            Assert.That(LatteArtPatternRecognizer.Recognize(new Vector2[20]).HasPattern, Is.False);
            Assert.That(LatteArtPatternRecognizer.Recognize(Densify(new[] { Vector2.zero, Vector2.one })).HasPattern, Is.False);
            Assert.That(LatteArtPatternRecognizer.Recognize(new[] { Vector2.zero, Vector2.one, Vector2.right, Vector2.up, new Vector2(float.PositiveInfinity, 0) }).HasPattern, Is.False);
        }

        [Test]
        public void CircleSquareAndDenseScribbleAreNotAutomaticallyRecognized()
        {
            var circle = new List<Vector2>();
            for (int i = 0; i <= 96; i++) circle.Add(new Vector2(Mathf.Cos(i * Mathf.PI * 2 / 96), Mathf.Sin(i * Mathf.PI * 2 / 96)));
            Assert.That(LatteArtPatternRecognizer.Recognize(circle).HasPattern, Is.False);
            Assert.That(LatteArtPatternRecognizer.Recognize(Densify(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector2.zero })).HasPattern, Is.False);
            var scribble = new List<Vector2>();
            var random = new System.Random(738);
            for (int i = 0; i < 120; i++) scribble.Add(new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
            Assert.That(LatteArtPatternRecognizer.Recognize(scribble).HasPattern, Is.False);
        }

        [Test]
        public void RepeatedRetracingIsRejectedEvenWhenGeometryLooksLikeTemplate()
        {
            var heart = Densify(LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.Heart));
            var repeated = new List<Vector2>();
            for (int i = 0; i < 4; i++) repeated.AddRange(heart);
            Assert.That(LatteArtPatternRecognizer.Recognize(repeated).HasPattern, Is.False);
        }

        [Test]
        public void CustomPresetIdIsPreservedAndWrongPresetDoesNotForceMatch()
        {
            var heart = Densify(LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.Heart));
            var preset = new LatteArtPatternPreset { id = "order.heart", template = LatteArtTemplateKind.Heart };
            Assert.That(LatteArtPatternRecognizer.Recognize(heart, new[] { preset }).patternId, Is.EqualTo("order.heart"));
            preset.template = LatteArtTemplateKind.X;
            Assert.That(LatteArtPatternRecognizer.Recognize(heart, new[] { preset }).HasPattern, Is.False);
        }

        [Test]
        public void PreviewReturnsIndependentNormalizedCopyWithStrokeSeparators()
        {
            var first = LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.X);
            Assert.That(float.IsNaN(first[2].x) && float.IsNaN(first[2].y), Is.True);
            first[0] = new Vector2(100, 100);
            var second = LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.X);
            foreach (var point in second)
            {
                if (float.IsNaN(point.x)) continue;
                Assert.That(point.x, Is.InRange(0f, 1f));
                Assert.That(point.y, Is.InRange(0f, 1f));
            }
            Assert.That(LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.None), Is.Empty);
        }

        [Test]
        public void InvalidStrokeBoundariesFailSafely()
        {
            var heart = Densify(LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.Heart));
            Assert.That(LatteArtPatternRecognizer.Recognize(heart, null, new[] { 0, -1 }).HasPattern, Is.False);
            Assert.That(LatteArtPatternRecognizer.Recognize(heart, null, new[] { 0, 0 }).HasPattern, Is.False);
            Assert.That(LatteArtPatternRecognizer.Recognize(heart, null, new[] { heart.Count }).HasPattern, Is.False);
        }

        [Test]
        public void LiveFitsReportEveryPresetAndKeepRecognitionThresholdIndependent()
        {
            var presets = new[]
            {
                new LatteArtPatternPreset { id = "heart", template = LatteArtTemplateKind.Heart },
                new LatteArtPatternPreset { id = "x", template = LatteArtTemplateKind.X },
                new LatteArtPatternPreset { id = "a", template = LatteArtTemplateKind.A },
                new LatteArtPatternPreset { id = "tulip", template = LatteArtTemplateKind.Tulip },
                new LatteArtPatternPreset { id = "rosetta", template = LatteArtTemplateKind.Rosetta }
            };
            var results = new List<LatteArtRecognitionResult>();
            LatteArtPatternRecognizer.GetPatternFits(null, presets, null, results);
            Assert.That(results.Count, Is.EqualTo(presets.Length));
            foreach (var fit in results) Assert.That(fit.confidence, Is.Zero);
            LatteArtPatternRecognizer.GetPatternFits(Densify(LatteArtPatternRecognizer.PreviewPath(LatteArtTemplateKind.Heart)), presets, null, results);
            Assert.That(results.Count, Is.EqualTo(presets.Length));
            Assert.That(results[0].confidence, Is.GreaterThan(.85f));
            for (int i = 1; i < results.Count; i++)
            {
                Assert.That(results[i].patternId, Is.EqualTo(presets[i].id));
                Assert.That(results[i].confidence, Is.InRange(0f, results[0].confidence));
            }
        }

        [TestCase(LatteArtTemplateKind.Bean)]
        [TestCase(LatteArtTemplateKind.SwordCrown)]
        [TestCase(LatteArtTemplateKind.ConcordRing)]
        [TestCase(LatteArtTemplateKind.NewRose)]
        [TestCase(LatteArtTemplateKind.BrokenChain)]
        public void BenchPatternsSurviveSmoothHandTremorAndStayDistinct(LatteArtTemplateKind kind)
        {
            var presets = new List<LatteArtPatternPreset>(FreeCafeLatteArtBench.BuiltinPresets());
            string expected = presets.Find(item => item.template == kind).id;
            var points = Densify(LatteArtPatternRecognizer.PreviewPath(kind));
            float c = Mathf.Cos(8f * Mathf.Deg2Rad), s = Mathf.Sin(8f * Mathf.Deg2Rad);
            for (int i = 0; i < points.Count; i++)
            {
                if (float.IsNaN(points[i].x)) continue;
                Vector2 p = points[i] - Vector2.one * .5f;
                points[i] = new Vector2(c * p.x - s * p.y + .014f * Mathf.Sin(i * .09f), s * p.x + c * p.y + .014f * Mathf.Cos(i * .06f));
            }
            var result = LatteArtPatternRecognizer.Recognize(points, presets);
            Assert.That(result.patternId, Is.EqualTo(expected));
            Assert.That(result.confidence, Is.GreaterThan(.7f));
            var fits = new List<LatteArtRecognitionResult>();
            LatteArtPatternRecognizer.GetPatternFits(points, presets, null, fits);
            foreach (var fit in fits)
                if (fit.patternId != expected) Assert.That(fit.confidence, Is.LessThan(result.confidence - .15f), fit.patternId);
        }

        [Test]
        public void EveryFactionPatternNamesAKnownFaction()
        {
            int factions = 0;
            foreach (var preset in FreeCafeLatteArtBench.BuiltinPresets())
            {
                Assert.That(FreeCafeProductionText.Pattern(preset.id), Is.EqualTo(preset.displayName), preset.id);
                Assert.That(preset.meaning, Is.Not.Empty.And.Not.Contain("占位"), preset.id);
                if (string.IsNullOrEmpty(preset.factionId)) continue;
                factions++;
                Assert.That(Spotlight.GameFlow.WorldFactionCatalog.Find(preset.factionId), Is.Not.Null, preset.id);
            }
            Assert.That(factions, Is.EqualTo(4));
        }

        private static List<Vector2> Densify(IList<Vector2> vertices)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                if (i == 0 || float.IsNaN(current.x) || float.IsNaN(vertices[i - 1].x)) { result.Add(current); continue; }
                int steps = i % 2 == 0 ? 19 : 7;
                for (int j = 1; j <= steps; j++) result.Add(Vector2.Lerp(vertices[i - 1], current, j / (float)steps));
            }
            return result;
        }
    }
}
