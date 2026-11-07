using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace VRArchery.Bow
{
    public enum BowStringIndicatorState { Blocked, Ready, Drawing }

    /// <summary>Read-only diagnostics: never selects, moves a hand or relaxes the bow's rules.</summary>
    [DefaultExecutionOrder(170)]
    public class BowStringDiagnostics : MonoBehaviour
    {
        [SerializeField] BowController m_Bow;
        [SerializeField] Renderer m_Marker;
        [SerializeField, Tooltip("Optional. When empty, finds the configured string hand in the same Interaction Manager.")]
        XRBaseInteractor m_ObservedInteractor;
        [SerializeField] bool m_LogStateChanges;
        [SerializeField] Color m_BlockedColor = Color.red;
        [SerializeField] Color m_ReadyColor = Color.green;
        [SerializeField] Color m_DrawingColor = Color.yellow;

        readonly List<IXRInteractor> interactors = new List<IXRInteractor>();
        readonly List<IXRInteractable> targets = new List<IXRInteractable>();
        XRBaseInteractor candidate;
        XRBaseInputInteractor bowHandInput;
        MaterialPropertyBlock block;
        string lastLog;

        public BowStringIndicatorState State { get; private set; }
        public string Message { get; private set; } = "Inicializando";
        public float HandDistance { get; private set; } = float.PositiveInfinity;
        public XRBaseInteractor ObservedInteractor => candidate;
        public bool StringGripPressed => candidate is XRBaseInputInteractor input && input.selectInput.ReadIsPerformed();
        public bool BowGripPressed => bowHandInput != null && bowHandInput.selectInput.ReadIsPerformed();
        public Color CurrentColor => State == BowStringIndicatorState.Drawing ? m_DrawingColor :
            State == BowStringIndicatorState.Ready ? m_ReadyColor : m_BlockedColor;
        public string InputSummary => candidate == null ? "Controle da corda nao encontrado" :
            $"Mao: {candidate.handedness} | grip corda: {(StringGripPressed ? 1 : 0)} | grip arco: {(BowGripPressed ? 1 : 0)} | {HandDistance:F2} m";

        void Awake()
        {
            if (m_Bow == null) m_Bow = GetComponent<BowController>();
            if (m_Marker == null && m_Bow != null && m_Bow.CenterPoint != null)
                m_Marker = m_Bow.CenterPoint.GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            ResolveInteractor();
            HandDistance = candidate != null && m_Bow != null ?
                Vector3.Distance(candidate.transform.position, m_Bow.CenterPoint.position) : float.PositiveInfinity;
            Evaluate();
            if (m_Marker != null)
            {
                m_Marker.GetPropertyBlock(block);
                block.SetColor("_BaseColor", CurrentColor);
                m_Marker.SetPropertyBlock(block);
            }
            // Message excludes distance, so enabling logs never emits one line per moving frame.
            if (m_LogStateChanges && lastLog != Message)
                Debug.Log($"[BowString] {Message}. {InputSummary}", this);
            lastLog = Message;
        }

        void ResolveInteractor()
        {
            if (m_Bow == null || m_Bow.String == null || m_Bow.String.interactionManager == null) return;
            var hand = m_Bow.StringHand;
            candidate = m_ObservedInteractor;
            bowHandInput = m_Bow.HoldingInteractor as XRBaseInputInteractor;
            m_Bow.String.interactionManager.GetRegisteredInteractors(interactors);
            foreach (var registered in interactors)
            {
                if (registered is XRBaseInputInteractor bowInput && registered.handedness == m_Bow.BowHand &&
                    (bowHandInput == null || bowInput is NearFarInteractor)) bowHandInput = bowInput;
                if (m_ObservedInteractor != null) continue;
                if (!(registered is XRBaseInteractor interactor) || interactor.handedness == InteractorHandedness.None ||
                    (hand != InteractorHandedness.None && interactor.handedness != hand)) continue;
                if (hand == InteractorHandedness.None && m_Bow.HoldingInteractor != null &&
                    interactor.handedness == m_Bow.HoldingInteractor.handedness) continue;
                if (candidate == null || !(candidate is NearFarInteractor)) candidate = interactor;
            }
        }

        void Evaluate()
        {
            State = BowStringIndicatorState.Blocked;
            if (m_Bow == null || m_Bow.String == null) { Message = "Referencias do arco ausentes"; return; }
            var cord = m_Bow.String;
            var manager = cord.interactionManager;
            if (manager == null || !manager.IsRegistered((IXRInteractable)cord)) { Message = "Corda nao registrada no XRI"; return; }
            if (m_Bow.State == BowState.Drawing && cord.isSelected)
            {
                State = BowStringIndicatorState.Drawing;
                Message = "Puxando";
                return;
            }
            if (candidate == null) { Message = "Controle da corda nao encontrado"; return; }
            if (!candidate.isActiveAndEnabled) { Message = "Controle da corda inativo"; return; }
            if (((IXRInteractor)candidate).IsBlockedByInteractionWithinGroup())
            { Message = "Bloqueada por outro interactor do grupo"; return; }
            if (!m_Bow.IsHeld) { Message = "Segure o arco com a outra mao"; return; }
            if (!m_Bow.CanPull(candidate)) { Message = "Mao incorreta ou mesma mao do arco"; return; }
            if (((int)candidate.interactionLayers & (int)cord.interactionLayers) == 0) { Message = "Interaction Layers incompativeis"; return; }
            var collider = cord.GetComponent<Collider>();
            if (collider == null || !collider.enabled) { Message = "Collider da corda ausente/inativo"; return; }
            if (!manager.TryGetInteractableForCollider(collider, out var mapped) || !ReferenceEquals(mapped, cord))
            { Message = "Collider nao registrado para a corda"; return; }
            if (HandDistance > cord.InteractionRadius || !BowDrawMath.IsFinite(candidate.transform.position))
            { Message = "Aproxime o controle do ponto"; return; }
            if (candidate.hasSelection) { Message = "Controle ocupado com outro objeto"; return; }
            if (candidate is NearFarInteractor nearFar && nearFar.nearInteractionCaster is SphereInteractionCaster sphere &&
                collider.isTrigger && sphere.physicsTriggerInteraction == QueryTriggerInteraction.Ignore)
            { Message = "Trigger ignorado pelo detector"; return; }
            targets.Clear();
            candidate.GetValidTargets(targets);
            if (!targets.Contains(cord)) { Message = "Ponto fora dos alvos do detector"; return; }
            if (!manager.IsSelectPossible(candidate, (IXRSelectInteractable)cord)) { Message = "Selecao bloqueada pelo XRI"; return; }
            State = BowStringIndicatorState.Ready;
            Message = StringGripPressed ? "Pronta: solte e pressione o grip" : "Pronta: pressione o grip";
        }
    }
}
