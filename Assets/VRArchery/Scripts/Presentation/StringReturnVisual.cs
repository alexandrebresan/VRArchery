using UnityEngine;
using VRArchery.Bow;

namespace VRArchery.Presentation
{
    [DefaultExecutionOrder(210)]
    public sealed class StringReturnVisual : MonoBehaviour
    {
        [SerializeField] BowController m_Bow;
        [SerializeField] LineRenderer m_Line;
        float startedAt;
        float releaseDistance;

        void OnEnable()
        {
            m_Bow.DrawCompleted += OnReleased;
            m_Bow.DrawCancelled += Cancel;
        }

        void OnDisable()
        {
            m_Bow.DrawCompleted -= OnReleased;
            m_Bow.DrawCancelled -= Cancel;
            Cancel();
        }

        void OnReleased(float intensity)
        {
            startedAt = Time.time;
            releaseDistance = m_Bow.LastReleaseDistance;
        }

        void Cancel() => releaseDistance = 0;

        void LateUpdate()
        {
            if (m_Bow.State == BowState.Drawing) Cancel();
            if (releaseDistance <= 0 || m_Line.positionCount < 3) return;
            float elapsed = Time.time - startedAt;
            if (elapsed >= 0.24f)
            {
                Cancel();
                return;
            }
            Vector3 position = m_Bow.RestPoint.position + m_Bow.PullAxis * Offset(releaseDistance, elapsed);
            m_Line.SetPosition(1, m_Line.useWorldSpace ? position : m_Line.transform.InverseTransformPoint(position));
        }

        public static float Offset(float distance, float seconds)
        {
            const float returnTime = 0.08f;
            if (seconds <= 0) return distance;
            if (seconds >= 0.24f) return 0;
            if (seconds < returnTime)
            {
                float remaining = 1 - seconds / returnTime;
                return distance * remaining * remaining;
            }
            float ring = seconds - returnTime;
            return -Mathf.Min(distance * 0.08f, 0.012f) * Mathf.Exp(-18 * ring) * Mathf.Sin(20 * Mathf.PI * ring);
        }
    }
}
