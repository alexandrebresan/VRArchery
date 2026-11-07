using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRArchery.Bow
{
    /// <summary>Only starts at the physical hand, never by a distant ray attachment.</summary>
    public class BowStringInteractable : XRSimpleInteractable
    {
        [SerializeField] BowController m_Bow;
        [SerializeField, Min(0.01f)] float m_InteractionRadius = 0.12f;

        public float InteractionRadius => m_InteractionRadius;

        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!base.IsSelectableBy(interactor) || m_Bow == null || !m_Bow.CanPull(interactor))
                return false;
            // Do not apply the starting radius during an existing pull.
            return IsSelected(interactor) ||
                (BowDrawMath.IsFinite(interactor.transform.position) &&
                 Vector3.Distance(interactor.transform.position, transform.position) <= m_InteractionRadius);
        }
    }
}
