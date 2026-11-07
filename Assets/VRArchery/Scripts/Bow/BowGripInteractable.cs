using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRArchery.Bow
{
    /// <summary>Standard XRI grab with an Inspector-configured hand role.</summary>
    public class BowGripInteractable : XRGrabInteractable
    {
        [SerializeField] BowController m_Bow;

        public override bool IsSelectableBy(IXRSelectInteractor interactor) =>
            base.IsSelectableBy(interactor) && m_Bow != null && m_Bow.CanHold(interactor);
    }
}
