using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Output-to-source mapping shared by retainer rendering and pointer input.</summary>
    public static class UIRetainerWarp
    {
        public static Vector2 ClampPerspective(Vector2 value)
        {
            float magnitude = Mathf.Abs(value.x) + Mathf.Abs(value.y);
            return magnitude > 0.8f ? value * (0.8f / magnitude) : value;
        }

        public static Vector2 EffectivePerspective(Vector2 perspective, float fieldOfView)
        {
            Vector2 value = ClampPerspective(perspective) * (Mathf.Tan(Mathf.Clamp(fieldOfView, 1f, 150f) * Mathf.Deg2Rad * 0.5f) / Mathf.Tan(30f * Mathf.Deg2Rad));
            float magnitude = Mathf.Abs(value.x) + Mathf.Abs(value.y);
            return magnitude > 0.95f ? value * (0.95f / magnitude) : value;
        }

        /// <summary>Conservative bounds of the inverse radial warp, before perspective projection.</summary>
        public static float RadialBounds(float curvature)
        {
            float contraction = Mathf.Max(0f, -curvature);
            return Mathf.Sqrt(4f * contraction + Mathf.Sqrt(16f * contraction * contraction + 1f));
        }

        public static Vector3 FitBounds(float curvature, Vector2 effectivePerspective)
        {
            float bound = RadialBounds(curvature);
            Vector2 minimum = new Vector2(-bound / (1f + effectivePerspective.x - Mathf.Abs(effectivePerspective.y)),
                -bound / (1f + effectivePerspective.y - Mathf.Abs(effectivePerspective.x)));
            Vector2 maximum = new Vector2(bound / (1f - effectivePerspective.x - Mathf.Abs(effectivePerspective.y)),
                bound / (1f - effectivePerspective.y - Mathf.Abs(effectivePerspective.x)));
            Vector2 center = (minimum + maximum) * 0.5f;
            Vector2 extent = (maximum - minimum) * 0.5f;
            float padding = curvature == 0f && effectivePerspective == Vector2.zero ? 1f : 1.002f;
            return new Vector3(center.x, center.y, Mathf.Max(extent.x, extent.y) * padding);
        }

        public static Vector2 MapToSource(Vector2 uv, float curvature, Vector2 perspective, float fieldOfView = 60f, bool fitContent = true)
        {
            perspective = EffectivePerspective(perspective, fieldOfView);
            Vector2 point = uv * 2f - Vector2.one;
            if (fitContent)
            {
                Vector3 fit = FitBounds(curvature, perspective);
                point = point * fit.z + new Vector2(fit.x, fit.y);
                perspective /= RadialBounds(curvature);
            }
            float denominator = 1f + Vector2.Dot(perspective, point);
            if (denominator <= 0f) return new Vector2(-1f, -1f);
            point /= denominator;
            float radiusSquared = point.sqrMagnitude;
            // Globally increasing and unbounded: every source edge remains reachable.
            point *= curvature < 0f ? Mathf.Pow(1f - 4f * curvature * radiusSquared, -0.25f) : 1f + curvature * radiusSquared;
            return (point + Vector2.one) * 0.5f;
        }

        /// <summary>Project captured UI coordinates back to the visible retainer panel.</summary>
        public static Vector2 MapFromSource(Vector2 uv, float curvature, Vector2 perspective, float fieldOfView = 60f, bool fitContent = true)
        {
            Vector2 point = uv * 2f - Vector2.one;
            float radius = point.magnitude;
            if (curvature < 0f)
            {
                float value = -2f * curvature * radius * radius;
                point *= Mathf.Sqrt(value + Mathf.Sqrt(value * value + 1f));
            }
            else if (curvature > 0f && radius > 0f)
            {
                float inverseRadius = radius;
                for (int i = 0; i < 8; i++)
                    inverseRadius -= (inverseRadius + curvature * inverseRadius * inverseRadius * inverseRadius - radius) /
                        (1f + 3f * curvature * inverseRadius * inverseRadius);
                point *= inverseRadius / radius;
            }
            perspective = EffectivePerspective(perspective, fieldOfView);
            Vector2 tilt = fitContent ? perspective / RadialBounds(curvature) : perspective;
            float denominator = 1f - Vector2.Dot(tilt, point);
            if (denominator <= 0f) return new Vector2(-1f, -1f);
            point /= denominator;
            if (fitContent)
            {
                Vector3 fit = FitBounds(curvature, perspective);
                point = (point - new Vector2(fit.x, fit.y)) / fit.z;
            }
            return (point + Vector2.one) * 0.5f;
        }

        public static bool Contains(Vector2 uv) => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
    }
}
