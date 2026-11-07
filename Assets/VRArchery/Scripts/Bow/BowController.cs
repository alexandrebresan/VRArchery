using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRArchery.Bow
{
    public enum BowState { Available, Held, Drawing }
    public enum BowDrawResult { None, Completed, Cancelled }

    [DefaultExecutionOrder(100)]
    public class BowController : MonoBehaviour
    {
        [SerializeField] BowGripInteractable m_Grip;
        [SerializeField] BowStringInteractable m_String;
        [SerializeField] Transform m_RestPoint;
        [SerializeField] Transform m_CenterPoint;
        [SerializeField] InteractorHandedness m_BowHand = InteractorHandedness.Left;
        [SerializeField] InteractorHandedness m_StringHand = InteractorHandedness.Right;
        [SerializeField] Vector3 m_LocalPullAxis = Vector3.back;
        [SerializeField, Min(0f)] float m_MinimumDrawDistance = 0.02f;
        [SerializeField, Min(0.01f)] float m_MaximumDrawDistance = 0.5f;

        IXRSelectInteractor holder;
        IXRSelectInteractor pullingHand;
        float startingProjection;

        public BowState State { get; private set; }
        public BowDrawResult LastDrawResult { get; private set; }
        public float DrawDistance { get; private set; }
        public float NormalizedDraw { get; private set; }
        public float LastReleaseIntensity { get; private set; }
        public float LastReleaseDistance { get; private set; }
        public Vector3 LastReleasePosition { get; private set; }
        public Quaternion LastReleaseRotation { get; private set; } = Quaternion.identity;
        public float MinimumDrawDistance => m_MinimumDrawDistance;
        public float MaximumDrawDistance => m_MaximumDrawDistance;
        public BowGripInteractable Grip => m_Grip;
        public BowStringInteractable String => m_String;
        public Transform RestPoint => m_RestPoint;
        public Transform CenterPoint => m_CenterPoint;
        public Vector3 PullAxis => transform.TransformDirection(m_LocalPullAxis).normalized;
        public InteractorHandedness BowHand { get => m_BowHand; set => m_BowHand = value; }
        public InteractorHandedness StringHand { get => m_StringHand; set => m_StringHand = value; }
        public bool IsHeld => Live(holder) && m_Grip != null && m_Grip.isSelected;
        public IXRSelectInteractor HoldingInteractor => holder;

        // Release snapshots are captured before resetting the visual string.
        public event Action<float> DrawCompleted;
        public event Action DrawCancelled;

        void OnEnable()
        {
            if (m_Grip == null || m_String == null || m_RestPoint == null || m_CenterPoint == null)
            {
                Debug.LogError("BowController: configure grip, string, rest and center references.", this);
                enabled = false;
                return;
            }
            m_Grip.selectEntered.AddListener(OnGripEntered);
            m_Grip.selectExited.AddListener(OnGripExited);
            m_String.selectEntered.AddListener(OnStringEntered);
            m_String.selectExited.AddListener(OnStringExited);
            ResetString();
        }

        void OnDisable()
        {
            if (m_Grip != null)
            {
                m_Grip.selectEntered.RemoveListener(OnGripEntered);
                m_Grip.selectExited.RemoveListener(OnGripExited);
            }
            if (m_String != null)
            {
                m_String.selectEntered.RemoveListener(OnStringEntered);
                m_String.selectExited.RemoveListener(OnStringExited);
            }
            holder = null;
            CancelDraw();
            if (m_Grip != null && m_Grip.isSelected && m_Grip.interactionManager != null)
                m_Grip.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grip);
        }

        public bool CanHold(IXRSelectInteractor interactor) =>
            isActiveAndEnabled && Live(interactor) && interactor.handedness != InteractorHandedness.None &&
            (m_BowHand == InteractorHandedness.None || interactor.handedness == m_BowHand);

        public bool CanPull(IXRSelectInteractor interactor) =>
            isActiveAndEnabled && IsHeld && Live(interactor) && !ReferenceEquals(holder, interactor) &&
            interactor.handedness != InteractorHandedness.None && interactor.handedness != holder.handedness &&
            (m_StringHand == InteractorHandedness.None || interactor.handedness == m_StringHand);

        static bool Live(IXRSelectInteractor interactor) =>
            interactor is Behaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;

        void OnGripEntered(SelectEnterEventArgs args)
        {
            holder = args.interactorObject;
            State = BowState.Held;
        }

        void OnGripExited(SelectExitEventArgs args)
        {
            holder = null;
            CancelDraw();
        }

        void OnStringEntered(SelectEnterEventArgs args)
        {
            if (!CanPull(args.interactorObject))
            {
                CancelDraw();
                return;
            }
            pullingHand = args.interactorObject;
            startingProjection = Vector3.Dot(pullingHand.transform.position - m_RestPoint.position, PullAxis);
            LastDrawResult = BowDrawResult.None;
            LastReleaseIntensity = 0f;
            State = BowState.Drawing;
            ResetString();
        }

        void OnStringExited(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(pullingHand, args.interactorObject)) return;
            if (args.isCanceled || !IsHeld || !SampleDraw())
            {
                CancelDraw();
                return;
            }
            bool completed = DrawDistance > m_MinimumDrawDistance;
            LastReleaseIntensity = NormalizedDraw;
            LastReleaseDistance = DrawDistance;
            LastReleasePosition = m_CenterPoint.position;
            LastReleaseRotation = Quaternion.LookRotation(-PullAxis, transform.up);
            pullingHand = null;
            LastDrawResult = completed ? BowDrawResult.Completed : BowDrawResult.Cancelled;
            State = BowState.Held;
            ResetString();
            if (completed) DrawCompleted?.Invoke(LastReleaseIntensity);
            else DrawCancelled?.Invoke();
        }

        void LateUpdate()
        {
            if (pullingHand == null) return;
            if (!CanPull(pullingHand) || !SampleDraw()) CancelDraw();
        }

        bool SampleDraw()
        {
            if (!Live(pullingHand) || !BowDrawMath.IsFinite(pullingHand.transform.position)) return false;
            var displacement = pullingHand.transform.position - m_RestPoint.position - PullAxis * startingProjection;
            DrawDistance = BowDrawMath.Distance(displacement, PullAxis, m_MaximumDrawDistance);
            NormalizedDraw = BowDrawMath.Intensity(DrawDistance, m_MinimumDrawDistance, m_MaximumDrawDistance);
            m_CenterPoint.position = m_RestPoint.position + PullAxis * DrawDistance;
            return true;
        }

        public void CancelDraw()
        {
            bool wasDrawing = pullingHand != null;
            // Clear ownership before asking XRI to exit, so exit callbacks cannot complete a cancelled draw.
            pullingHand = null;
            if (m_String != null && m_String.isSelected && m_String.interactionManager != null)
                m_String.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_String);
            ResetString();
            State = IsHeld ? BowState.Held : BowState.Available;
            if (!wasDrawing) return;
            LastDrawResult = BowDrawResult.Cancelled;
            LastReleaseIntensity = 0f;
            LastReleaseDistance = 0f;
            DrawCancelled?.Invoke();
        }

        void ResetString()
        {
            DrawDistance = NormalizedDraw = 0f;
            if (m_CenterPoint != null && m_RestPoint != null) m_CenterPoint.position = m_RestPoint.position;
        }

        void OnValidate()
        {
            m_MinimumDrawDistance = Mathf.Max(0, m_MinimumDrawDistance);
            m_MaximumDrawDistance = Mathf.Max(m_MinimumDrawDistance + 0.01f, m_MaximumDrawDistance);
            if (!BowDrawMath.IsFinite(m_LocalPullAxis) || m_LocalPullAxis.sqrMagnitude < 0.0001f)
                m_LocalPullAxis = Vector3.back;
        }
    }
}
