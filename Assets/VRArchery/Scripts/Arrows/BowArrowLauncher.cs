using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRArchery.Bow;

namespace VRArchery.Arrows
{
    [DefaultExecutionOrder(150)]
    public class BowArrowLauncher : MonoBehaviour
    {
        [SerializeField] BowController m_Bow;
        [SerializeField] ArrowProjectile m_ArrowPrefab;
        [SerializeField, Tooltip("XR Origin root; its colliders are ignored by fired arrows.")] Transform m_PlayerRoot;
        [SerializeField, Min(0.01f)] float m_MinimumDrawDistance = 0.08f;
        [SerializeField, Min(0.1f)] float m_MinimumSpeed = 12f;
        [SerializeField, Min(0.1f)] float m_MaximumSpeed = 28f;
        [SerializeField, Min(0f)] float m_ReloadDelay = 0.15f;
        readonly List<ArrowProjectile> fired = new List<ArrowProjectile>();
        readonly List<Collider> ignored = new List<Collider>();
        bool armed;
        float reloadAt;

        public ArrowProjectile NockedArrow { get; private set; }
        public ArrowProjectile LastLaunchedArrow { get; private set; }
        public int ShotCount { get; private set; }
        public float MinimumSpeed => m_MinimumSpeed;
        public float MaximumSpeed => m_MaximumSpeed;
        public float MinimumDrawDistance => m_MinimumDrawDistance;
        public float ReloadDelay => m_ReloadDelay;
        public Transform PlayerRoot { get => m_PlayerRoot; set => m_PlayerRoot = value; }
        public event Action<ArrowProjectile> ArrowLaunched;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (m_Bow == null || m_ArrowPrefab == null)
            {
                Debug.LogError("BowArrowLauncher: assign Bow and Arrow Prefab.", this);
                enabled = false;
                return;
            }
            m_Bow.String.selectEntered.AddListener(OnDrawStarted);
            m_Bow.DrawCompleted += OnDrawCompleted;
            m_Bow.DrawCancelled += OnDrawCancelled;
            PrepareArrow();
        }

        void OnDisable()
        {
            armed = false;
            if (m_Bow != null)
            {
                m_Bow.String.selectEntered.RemoveListener(OnDrawStarted);
                m_Bow.DrawCompleted -= OnDrawCompleted;
                m_Bow.DrawCancelled -= OnDrawCancelled;
            }
            if (NockedArrow != null) Destroy(NockedArrow.gameObject);
            NockedArrow = null;
        }

        void OnDrawStarted(SelectEnterEventArgs args) =>
            armed = NockedArrow != null && m_Bow.State == BowState.Drawing && m_Bow.IsHeld;
        void OnDrawCancelled() => armed = false;

        void OnDrawCompleted(float intensity)
        {
            if (!armed) return;
            armed = false; // A draw can consume at most one arrow, even under duplicate/reentrant notifications.
            if (NockedArrow == null || !m_Bow.IsHeld || m_Bow.LastDrawResult != BowDrawResult.Completed ||
                !ArrowLaunchMath.MeetsMinimum(m_Bow.LastReleaseDistance, m_MinimumDrawDistance)) return;
            float speed = ArrowLaunchMath.Speed(intensity, m_MinimumSpeed, m_MaximumSpeed);
            if (speed <= 0) return;
            var arrow = NockedArrow;
            NockedArrow = null;
            arrow.SetNockedPose(m_Bow.LastReleasePosition, m_Bow.LastReleaseRotation);
            ignored.Clear();
            ignored.AddRange(m_Bow.GetComponentsInChildren<Collider>(true));
            if (m_PlayerRoot != null) ignored.AddRange(m_PlayerRoot.GetComponentsInChildren<Collider>(true));
            fired.RemoveAll(item => item == null);
            foreach (var previous in fired) ignored.AddRange(previous.GetComponentsInChildren<Collider>(true));
            reloadAt = Time.time + m_ReloadDelay;
            if (!arrow.Launch(m_Bow.LastReleaseRotation * Vector3.forward * speed, ignored))
            { Destroy(arrow.gameObject); return; }
            fired.Add(arrow);
            LastLaunchedArrow = arrow;
            ShotCount++;
            ArrowLaunched?.Invoke(arrow);
        }

        void LateUpdate()
        {
            if (NockedArrow == null && Time.time >= reloadAt) PrepareArrow();
            if (NockedArrow != null)
                NockedArrow.SetNockedPose(m_Bow.CenterPoint.position, Quaternion.LookRotation(-m_Bow.PullAxis, m_Bow.transform.up));
        }

        void PrepareArrow()
        {
            if (NockedArrow != null) return;
            NockedArrow = Instantiate(m_ArrowPrefab, m_Bow.CenterPoint.position,
                Quaternion.LookRotation(-m_Bow.PullAxis, m_Bow.transform.up), transform);
            // A player may start the next pull before the reload delay has elapsed.
            armed = m_Bow.State == BowState.Drawing && m_Bow.IsHeld && m_Bow.String.isSelected;
        }

        void OnValidate()
        {
            m_MinimumDrawDistance = Mathf.Max(0.01f, m_MinimumDrawDistance);
            m_MinimumSpeed = Mathf.Max(0.1f, m_MinimumSpeed);
            m_MaximumSpeed = Mathf.Max(m_MinimumSpeed, m_MaximumSpeed);
            m_ReloadDelay = Mathf.Max(0, m_ReloadDelay);
        }
    }
}
