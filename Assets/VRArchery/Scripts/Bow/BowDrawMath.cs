using UnityEngine;

namespace VRArchery.Bow
{
    public static class BowDrawMath
    {
        public static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>Axial distance in metres; lateral motion never stretches the string.</summary>
        public static float Distance(Vector3 displacement, Vector3 axis, float maximum)
        {
            if (!IsFinite(displacement) || !IsFinite(axis) || !IsFinite(maximum) || maximum <= 0f || axis.sqrMagnitude < 0.0001f)
                return 0f;
            return Mathf.Clamp(Vector3.Dot(displacement, axis.normalized), 0f, maximum);
        }

        /// <summary>The minimum is a dead zone, not a forced initial jump.</summary>
        public static float Intensity(float distance, float minimum, float maximum)
        {
            if (!IsFinite(distance) || !IsFinite(minimum) || !IsFinite(maximum) || minimum < 0 || maximum <= minimum)
                return 0f;
            return Mathf.Clamp01((distance - minimum) / (maximum - minimum));
        }
    }
}
