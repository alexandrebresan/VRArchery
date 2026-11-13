using System;
using System.Collections.Generic;
using UnityEngine;
using VRArchery.Bow;
using VRArchery.Targets;

namespace VRArchery.Arrows
{
    public enum ArrowState { Nocked, Flying, Stuck }

    public readonly struct ArrowImpact
    {
        public readonly ArrowProjectile Arrow;
        public readonly Collider Collider;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public ArrowImpact(ArrowProjectile arrow, Collider collider, Vector3 point, Vector3 normal)
        { Arrow = arrow; Collider = collider; Point = point; Normal = normal; }
    }

    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class ArrowProjectile : MonoBehaviour
    {
        [SerializeField] Transform m_Tip;
        [SerializeField, Min(0.02f)] float m_LifetimeSeconds = 25f;
        [SerializeField, Min(0.001f)] float m_SweepRadius = 0.012f;
        [SerializeField, Min(0f)] float m_EmbedDepth = 0.015f;
        [SerializeField] LayerMask m_ImpactMask = ~0;

        Rigidbody body;
        Collider[] colliders;
        readonly HashSet<Collider> ignored = new HashSet<Collider>();
        Vector3 tipOffset;
        Vector3 previousTip;
        Vector3 flightDirection;
        MovingTarget attachment;
        Vector3 attachmentOffset;
        Quaternion attachmentRotation;

        public ArrowState State { get; private set; }
        public Collider HitCollider { get; private set; }
        public Vector3 ImpactPoint { get; private set; }
        public Vector3 TipPosition => body.position + body.rotation * Vector3.Scale(tipOffset, transform.lossyScale);
        public float LifetimeSeconds { get => m_LifetimeSeconds; set => m_LifetimeSeconds = Mathf.Max(0.02f, value); }
        public event Action<ArrowImpact> Impacted;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            tipOffset = m_Tip != null ? transform.InverseTransformPoint(m_Tip.position) : Vector3.forward * 0.67f;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            SetColliders(false);
        }

        public void SetNockedPose(Vector3 position, Quaternion rotation)
        {
            if (State != ArrowState.Nocked) return;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
        }

        public bool Launch(Vector3 velocity, IEnumerable<Collider> ownerColliders)
        {
            if (State != ArrowState.Nocked || !BowDrawMath.IsFinite(velocity) || velocity.sqrMagnitude < 0.0001f) return false;
            transform.SetParent(null, true);
            ignored.Clear();
            if (ownerColliders != null)
                foreach (var collider in ownerColliders) if (collider != null) ignored.Add(collider);
            State = ArrowState.Flying;
            flightDirection = velocity.normalized;
            body.rotation = Facing(flightDirection);
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            SetColliders(true);
            foreach (var own in colliders)
                foreach (var other in ignored)
                    if (own != other && other != null && other.enabled && other.gameObject.activeInHierarchy)
                        Physics.IgnoreCollision(own, other, true);
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.zero;
            previousTip = TipPosition;
            Destroy(gameObject, m_LifetimeSeconds);
            return true;
        }

        void FixedUpdate()
        {
            if (State != ArrowState.Flying) return;
            var tip = TipPosition;
            var displacement = tip - previousTip;
            if (displacement.sqrMagnitude > 0.000001f)
            {
                // Varre a trajetória da ponta entre passos físicos para registrar contatos com superfícies finas.
                var hits = Physics.SphereCastAll(previousTip, m_SweepRadius, displacement.normalized,
                    displacement.magnitude, m_ImpactMask, QueryTriggerInteraction.Ignore);
                float closest = float.PositiveInfinity;
                RaycastHit best = default;
                foreach (var hit in hits)
                    if (CanHit(hit.collider) && hit.distance < closest) { best = hit; closest = hit.distance; }
                if (!float.IsPositiveInfinity(closest))
                {
                    Stick(best.collider, best.point, best.normal);
                    return;
                }
            }
            var velocity = body.linearVelocity;
            if (BowDrawMath.IsFinite(velocity) && velocity.sqrMagnitude > 0.0001f)
            {
                flightDirection = velocity.normalized;
                body.MoveRotation(Facing(flightDirection));
            }
            previousTip = tip;
        }

        bool CanHit(Collider other) => other != null && other.enabled &&
            !ignored.Contains(other) && !other.transform.IsChildOf(transform) &&
            other.GetComponentInParent<ArrowProjectile>() == null &&
            (m_ImpactMask.value & (1 << other.gameObject.layer)) != 0;

        void OnCollisionEnter(Collision collision)
        {
            if (State != ArrowState.Flying || !CanHit(collision.collider) || collision.contactCount == 0) return;
            var contact = collision.GetContact(0);
            Stick(collision.collider, contact.point, contact.normal);
        }

        void Stick(Collider target, Vector3 point, Vector3 normal)
        {
            if (State != ArrowState.Flying) return;
            State = ArrowState.Stuck; // Consume impact before notifying listeners.
            HitCollider = target;
            ImpactPoint = point;
            var rotation = Facing(flightDirection);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.rotation = rotation;
            body.position = point - rotation * Vector3.Scale(tipOffset, transform.lossyScale) + flightDirection * m_EmbedDepth;
            transform.SetPositionAndRotation(body.position, rotation);
            SetColliders(false);
            if (target.TryGetComponent(out attachment))
            {
                var inverse = Quaternion.Inverse(attachment.transform.rotation);
                attachmentOffset = inverse * (body.position - attachment.PhysicsPosition);
                attachmentRotation = inverse * rotation;
            }
            Impacted?.Invoke(new ArrowImpact(this, target, point, normal));
        }

        void LateUpdate()
        {
            if (attachment == null) return;
            // Follow the rendered target pose without inheriting its scale or re-enabling collisions.
            var frame = attachment.transform;
            var position = frame.position + frame.rotation * attachmentOffset;
            var rotation = frame.rotation * attachmentRotation;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        static Quaternion Facing(Vector3 direction) => Quaternion.LookRotation(direction,
            Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);

        void SetColliders(bool enabled)
        { foreach (var collider in colliders) collider.enabled = enabled; }
    }
}
