using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Cached interior samples for volume-preserving surfaces in an arbitrary alpha/SDF mask.</summary>
    internal sealed class LiquidMaskGeometry
    {
        private const int Resolution = 56;
        private Texture2D texture;
        private bool sdf, valid;
        private float range, wall, aspect;
        private Vector2[] samples;
        private float[] projections;
        public Rect Bounds { get; private set; }

        public bool Configure(Texture2D mask, bool signedDistance, float distanceRange, float inset, float shapeAspect)
        {
            if (texture == mask && sdf == signedDistance && Mathf.Approximately(range, distanceRange) &&
                Mathf.Approximately(wall, inset) && Mathf.Approximately(aspect, shapeAspect)) return valid;
            texture = mask; sdf = signedDistance; range = distanceRange; wall = inset; aspect = shapeAspect;
            valid = false;
            if (mask == null) return false;
            if (!mask.isReadable)
            {
                Debug.LogWarning("Liquid container mask needs Read/Write for area-correct fill: " + mask.name);
                return false;
            }
            var points = new List<Vector2>(Resolution * Resolution);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                {
                    var uv = new Vector2((x + .5f) / Resolution, (y + .5f) / Resolution);
                    Color value = mask.GetPixelBilinear(uv.x, uv.y);
                    if (value.a < .5f) continue;
                    if (signedDistance)
                    {
                        if ((value.r - .5f) * (2f * range) < wall) continue;
                    }
                    else if (!InsideErodedAlpha(uv)) continue;
                    var point = new Vector2((uv.x - .5f) * aspect, uv.y - .5f);
                    points.Add(point); min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            if (points.Count == 0) return false;
            samples = points.ToArray(); projections = new float[samples.Length];
            var halfCell = new Vector2(aspect, 1f) * (.5f / Resolution);
            Bounds = Rect.MinMaxRect(min.x - halfCell.x, min.y - halfCell.y, max.x + halfCell.x, max.y + halfCell.y);
            valid = true;
            return true;
        }

        private bool InsideErodedAlpha(Vector2 uv)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                var delta = new Vector2(Mathf.Cos(angle) / aspect, Mathf.Sin(angle)) * wall;
                if (!InsideAlpha(uv + delta) || !InsideAlpha(uv + delta * .5f)) return false;
            }
            return true;
        }
        private bool InsideAlpha(Vector2 uv) => uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1 &&
            texture.GetPixelBilinear(uv.x, uv.y).a >= .5f;

        public float SolveSurfaceOffset(Vector2 normal, float fill)
        {
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < samples.Length; i++)
            {
                float projection = Vector2.Dot(normal, samples[i]);
                projections[i] = projection;
                low = Mathf.Min(low, projection); high = Mathf.Max(high, projection);
            }
            // Treat each sample as a small patch, rather than a point, to avoid stepping at rest.
            float halfPatch = (Mathf.Abs(normal.x) * aspect + Mathf.Abs(normal.y)) * (.5f / Resolution);
            low -= halfPatch; high += halfPatch;
            if (fill <= 0) return low;
            if (fill >= 1) return high;
            float inversePatch = 1f / Mathf.Max(halfPatch * 2f, .00001f);
            for (int iteration = 0; iteration < 14; iteration++)
            {
                float mid = (low + high) * .5f, area = 0;
                for (int i = 0; i < projections.Length; i++)
                    area += Mathf.Clamp01((mid - projections[i] + halfPatch) * inversePatch);
                if (area < fill * samples.Length) low = mid; else high = mid;
            }
            return (low + high) * .5f;
        }

        public float OpenTopCapacity(Vector2 normal)
        {
            if (normal.y <= 0) return 0;
            float rim = float.MaxValue;
            for (int i = 0; i < samples.Length; i++)
                if (samples[i].y > Bounds.yMax - 1.5f / Resolution)
                    rim = Mathf.Min(rim, Vector2.Dot(normal, samples[i]));
            int below = 0;
            for (int i = 0; i < samples.Length; i++)
                if (Vector2.Dot(normal, samples[i]) <= rim) below++;
            return (float)below / samples.Length;
        }
    }
}
