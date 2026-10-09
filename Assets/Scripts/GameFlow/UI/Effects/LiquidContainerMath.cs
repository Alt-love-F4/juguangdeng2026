using UnityEngine;

namespace Spotlight.GameFlow
{
    /// <summary>Area-preserving flat liquid surface in a rectangular, open-top container.</summary>
    public static class LiquidContainerMath
    {
        public static float AreaBelowPlane(Rect bounds, Vector2 normal, float offset)
        {
            float a = Mathf.Abs(normal.x) * bounds.width;
            float b = Mathf.Abs(normal.y) * bounds.height;
            float span = a + b;
            if (span < 0.000001f) return offset >= 0f ? 1f : 0f;
            float t = offset - Vector2.Dot(normal, bounds.center) + span * 0.5f;
            if (t <= 0f) return 0f;
            if (t >= span) return 1f;
            // Normalize before evaluating the uniform-sum CDF; symmetry avoids cancellation.
            t /= span;
            a /= span;
            b /= span;
            if (Mathf.Min(a, b) < 0.000001f) return Mathf.Clamp01(t);
            bool upper = t > 0.5f;
            if (upper) t = 1f - t;
            float ta = Mathf.Max(0f, t - a);
            float tb = Mathf.Max(0f, t - b);
            float area = Mathf.Clamp01((t * t - ta * ta - tb * tb) / (2f * a * b));
            return upper ? 1f - area : area;
        }

        public static float SolveSurfaceOffset(Rect bounds, Vector2 normal, float fill)
        {
            float radius = (Mathf.Abs(normal.x) * bounds.width + Mathf.Abs(normal.y) * bounds.height) * 0.5f;
            float center = Vector2.Dot(normal, bounds.center);
            float low = center - radius;
            float high = center + radius;
            if (fill <= 0f) return low;
            if (fill >= 1f) return high;
            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;
                if (AreaBelowPlane(bounds, normal, mid) < fill) low = mid;
                else high = mid;
            }
            return (low + high) * 0.5f;
        }

        public static float OpenTopCapacity(Rect bounds, Vector2 normal)
        {
            // With gravity directed toward the mouth (or exactly sideways), no static liquid is retained.
            if (normal.y <= 0f) return 0f;
            float rim = Mathf.Min(Vector2.Dot(normal, new Vector2(bounds.xMin, bounds.yMax)),
                Vector2.Dot(normal, new Vector2(bounds.xMax, bounds.yMax)));
            return AreaBelowPlane(bounds, normal, rim);
        }
    }
}
