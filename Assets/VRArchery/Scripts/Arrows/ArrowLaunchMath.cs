using UnityEngine;

namespace VRArchery.Arrows
{
    public static class ArrowLaunchMath
    {
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Speed(float intensity, float minimum, float maximum) =>
            Finite(intensity) && Finite(minimum) && Finite(maximum) && minimum > 0 && maximum >= minimum ?
                Mathf.Lerp(minimum, maximum, Mathf.Clamp01(intensity)) : 0;
        public static bool MeetsMinimum(float distance, float minimum) =>
            Finite(distance) && Finite(minimum) && minimum > 0 && distance >= minimum;
    }
}
