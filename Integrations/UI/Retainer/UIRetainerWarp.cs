using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Output-to-source mapping shared by retainer rendering and pointer input.</summary>
    public static class UIRetainerWarp
    {
        public static Vector2 MapToSource(Vector2 uv, float curvature, Vector2 perspective)
        {
            Vector2 point = uv * 2f - Vector2.one;
            float denominator = Mathf.Max(0.1f, 1f + Vector2.Dot(perspective, point));
            point /= denominator;
            point *= 1f + curvature * point.sqrMagnitude;
            return (point + Vector2.one) * 0.5f;
        }

        public static bool Contains(Vector2 uv) => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }
}
