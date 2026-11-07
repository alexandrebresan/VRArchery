using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRArchery.Bow
{
    [DefaultExecutionOrder(200)]
    public class BowFeedback : MonoBehaviour
    {
        [SerializeField] BowController m_Bow;
        [SerializeField] Transform m_UpperTip;
        [SerializeField] Transform m_LowerTip;
        [SerializeField] LineRenderer m_StringLine;
        [SerializeField] TMP_Text m_Status;
        [SerializeField] Image m_DrawBar;
        [SerializeField] BowStringDiagnostics m_StringDiagnostics;

        void LateUpdate()
        {
            if (m_Bow == null || m_StringLine == null || m_UpperTip == null || m_LowerTip == null) return;
            m_StringLine.positionCount = 3;
            SetPoint(0, m_UpperTip.position);
            SetPoint(1, m_Bow.CenterPoint.position);
            SetPoint(2, m_LowerTip.position);
            if (m_DrawBar != null) m_DrawBar.fillAmount = m_Bow.NormalizedDraw;
            if (m_Status != null)
            {
                string state = m_Bow.State == BowState.Available ? "Disponivel" : m_Bow.State == BowState.Held ? "Segurado" : "Puxando corda";
                string result = m_Bow.LastDrawResult == BowDrawResult.Completed ? "Concluida" : m_Bow.LastDrawResult == BowDrawResult.Cancelled ? "Cancelada" : "--";
                m_Status.text = $"{state}\nPuxada: {m_Bow.NormalizedDraw:P0}  ({m_Bow.DrawDistance:F2} m)\nUltima puxada: {result}";
                if (m_StringDiagnostics != null)
                    m_Status.text += $"\nCorda: {m_StringDiagnostics.Message}\n{m_StringDiagnostics.InputSummary}";
            }
        }

        void SetPoint(int index, Vector3 worldPosition) => m_StringLine.SetPosition(index,
            m_StringLine.useWorldSpace ? worldPosition : m_StringLine.transform.InverseTransformPoint(worldPosition));
    }
}
