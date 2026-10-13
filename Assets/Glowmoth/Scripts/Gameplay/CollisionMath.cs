using UnityEngine;

namespace Glowmoth
{
    /// <summary>
    /// Tiny allocation-free geometry used instead of the physics engine. Everything in the game is either a
    /// circle (moth, spiders, fireflies, portals) or an axis-aligned rectangle (walls, thorns, drafts).
    /// </summary>
    public static class CollisionMath
    {
        public static bool PointInRect(Vector2 p, Vector2 center, Vector2 half)
        {
            return Mathf.Abs(p.x - center.x) <= half.x && Mathf.Abs(p.y - center.y) <= half.y;
        }

        public static bool CircleOverlapsRect(Vector2 p, float r, Vector2 center, Vector2 half)
        {
            float dx = Mathf.Max(Mathf.Abs(p.x - center.x) - half.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y - center.y) - half.y, 0f);
            return dx * dx + dy * dy < r * r;
        }

        /// <summary>Circle vs rectangle with the push-out normal and depth needed to separate them.</summary>
        public static bool CircleRect(Vector2 p, float r, Vector2 center, Vector2 half, out Vector2 normal, out float penetration)
        {
            float lx = p.x - center.x, ly = p.y - center.y;
            float qx = Mathf.Clamp(lx, -half.x, half.x), qy = Mathf.Clamp(ly, -half.y, half.y);
            float dx = lx - qx, dy = ly - qy;
            float d2 = dx * dx + dy * dy;

            if (d2 >= r * r)
            {
                normal = Vector2.zero;
                penetration = 0f;
                return false;
            }

            if (d2 > 1e-8f)
            {
                float d = Mathf.Sqrt(d2);
                normal = new Vector2(dx / d, dy / d);
                penetration = r - d;
                return true;
            }

            // Centre is inside the rectangle: push out along the shallowest axis.
            float ox = half.x - Mathf.Abs(lx), oy = half.y - Mathf.Abs(ly);
            if (ox < oy)
            {
                normal = new Vector2(lx < 0f ? -1f : 1f, 0f);
                penetration = ox + r;
            }
            else
            {
                normal = new Vector2(0f, ly < 0f ? -1f : 1f);
                penetration = oy + r;
            }
            return true;
        }

        /// <summary>Does the segment a-b pass through the rectangle? (Slab test; used for line of sight.)</summary>
        public static bool SegmentHitsRect(Vector2 a, Vector2 b, Vector2 center, Vector2 half)
        {
            float tMin = 0f, tMax = 1f;
            Vector2 d = b - a;
            return Slab(a.x - center.x, d.x, half.x, ref tMin, ref tMax)
                && Slab(a.y - center.y, d.y, half.y, ref tMin, ref tMax);
        }

        static bool Slab(float start, float dir, float half, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(dir) < 1e-6f) return start >= -half && start <= half;
            float inv = 1f / dir;
            float t1 = (-half - start) * inv;
            float t2 = (half - start) * inv;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            return tMin <= tMax;
        }
    }
}
