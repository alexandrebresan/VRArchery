using UnityEngine;

namespace VRArchery.Targets
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class MovingTarget : MonoBehaviour
    {
        [SerializeField] TargetMotionProfile m_InitialProfile = new TargetMotionProfile(6);
        [SerializeField, Min(0f), Tooltip("Range distance of the authored starting pose.")]
        float m_ReferenceDistance = 6f;
        [SerializeField] Vector2 m_DistanceRange = new Vector2(5f, 10.5f);
        [SerializeField, Min(0f)] float m_MaxHorizontalOffset = 1.95f;
        [SerializeField, Min(0f)] float m_MaxVerticalOffset = 0.50f;

        Rigidbody body;
        Vector3 anchor;
        Quaternion orientation;
        double elapsed;
        bool hasMotion;

        public TargetMotionProfile Profile { get; private set; }
        public bool IsPaused { get; private set; }
        public Vector3 PhysicsPosition => body != null ? body.position : transform.position;

        void Awake()
        {
            if (body == null) Configure(m_InitialProfile);
        }

        public void Configure(TargetMotionProfile profile)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
                anchor = body.position;
                orientation = body.rotation;
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            Profile = profile.Constrain(m_DistanceRange, m_MaxHorizontalOffset, m_MaxVerticalOffset);
            hasMotion = (Profile.HorizontalAmplitude > 0 && Profile.HorizontalFrequency > 0) ||
                        (Profile.VerticalAmplitude > 0 && Profile.VerticalFrequency > 0);
            RestartMotion();
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        public void RestartMotion()
        {
            if (body == null)
            {
                Configure(m_InitialProfile);
                return;
            }
            elapsed = 0;
            var position = PositionAt(elapsed);
            // Profile changes occur between shots. Reset the interpolated pose along with the collider.
            body.interpolation = RigidbodyInterpolation.None;
            body.position = position;
            body.rotation = orientation;
            transform.SetPositionAndRotation(position, orientation);
            Physics.SyncTransforms();
            body.interpolation = hasMotion ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        }

        void FixedUpdate()
        {
            if (IsPaused || !hasMotion) return;
            elapsed += Time.fixedDeltaTime;
            body.MovePosition(PositionAt(elapsed));
        }

        // Calcula os deslocamentos no referencial da pose original do alvo, incluindo a profundidade da pista.
        Vector3 PositionAt(double seconds)
        {
            Vector2 offset = Profile.Offset(seconds);
            return anchor + orientation * new Vector3(offset.x, offset.y, Profile.Distance - m_ReferenceDistance);
        }

        void OnValidate()
        {
            m_DistanceRange.x = Mathf.Max(0, m_DistanceRange.x);
            m_DistanceRange.y = Mathf.Max(m_DistanceRange.x, m_DistanceRange.y);
        }
    }
}
