using System;
using UnityEngine;

namespace VRArchery.Targets
{
    [Serializable]
    public struct TargetMotionProfile
    {
        [SerializeField, Min(0f)] float m_Distance;
        [SerializeField, Min(0f)] float m_HorizontalAmplitude;
        [SerializeField, Min(0f), Tooltip("Horizontal cycles per second.")] float m_HorizontalFrequency;
        [SerializeField, Min(0f)] float m_VerticalAmplitude;
        [SerializeField, Min(0f), Tooltip("Vertical cycles per second.")] float m_VerticalFrequency;
        [SerializeField, Range(-180f, 180f)] float m_VerticalPhase;

        public float Distance => m_Distance;
        public float HorizontalAmplitude => m_HorizontalAmplitude;
        public float HorizontalFrequency => m_HorizontalFrequency;
        public float VerticalAmplitude => m_VerticalAmplitude;
        public float VerticalFrequency => m_VerticalFrequency;

        public TargetMotionProfile(float distance, float horizontalAmplitude = 0, float horizontalFrequency = 0,
            float verticalAmplitude = 0, float verticalFrequency = 0, float verticalPhase = 0)
        {
            m_Distance = distance;
            m_HorizontalAmplitude = horizontalAmplitude;
            m_HorizontalFrequency = horizontalFrequency;
            m_VerticalAmplitude = verticalAmplitude;
            m_VerticalFrequency = verticalFrequency;
            m_VerticalPhase = verticalPhase;
        }

        public Vector2 Offset(double seconds) => new Vector2(
            Axis(seconds, m_HorizontalAmplitude, m_HorizontalFrequency, 0),
            Axis(seconds, m_VerticalAmplitude, m_VerticalFrequency, m_VerticalPhase));

        internal TargetMotionProfile Constrain(Vector2 distanceRange, float maxHorizontal, float maxVertical) =>
            new TargetMotionProfile(Mathf.Clamp(m_Distance, distanceRange.x, distanceRange.y),
                Mathf.Clamp(m_HorizontalAmplitude, 0, maxHorizontal), Mathf.Max(0, m_HorizontalFrequency),
                Mathf.Clamp(m_VerticalAmplitude, 0, maxVertical), Mathf.Max(0, m_VerticalFrequency), m_VerticalPhase);

        static float Axis(double seconds, float amplitude, float frequency, float phaseDegrees)
        {
            if (amplitude <= 0 || frequency <= 0) return 0;
            // A frequência em ciclos por segundo vira fase angular dentro de um único período.
            float radians = (float)((seconds * frequency) % 1) * (2 * Mathf.PI) + phaseDegrees * Mathf.Deg2Rad;
            return amplitude * Mathf.Sin(radians);
        }
    }
}
