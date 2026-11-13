using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VRArchery.Scoring
{
    public sealed class ScorePanel : MonoBehaviour
    {
        [SerializeField] ScoringMatch m_Match;
        [SerializeField] TMP_Text m_Title;
        [SerializeField] TMP_Text m_Stats;
        [SerializeField] Button m_Training;
        [SerializeField] Button m_Challenge;
        [SerializeField] Button m_Restart;

        void OnEnable()
        {
            m_Training.onClick.AddListener(SelectTraining);
            m_Challenge.onClick.AddListener(SelectChallenge);
            m_Restart.onClick.AddListener(m_Match.Restart);
            m_Match.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            m_Training.onClick.RemoveListener(SelectTraining);
            m_Challenge.onClick.RemoveListener(SelectChallenge);
            m_Restart.onClick.RemoveListener(m_Match.Restart);
            m_Match.Changed -= Refresh;
        }

        void SelectTraining() => m_Match.SelectMode(GameMode.Training);
        void SelectChallenge() => m_Match.SelectMode(GameMode.Challenge);

        void Refresh()
        {
            var session = m_Match.Current;
            bool training = session.Mode == GameMode.Training;
            m_Training.interactable = !training;
            m_Challenge.interactable = training;
            m_Title.text = training ? "Treino Livre" : "Desafio por Rodadas";
            m_Stats.text = training ? TrainingStats(session) : ChallengeStats(session);
        }

        internal static string TrainingStats(ArcherySession session) =>
            $"Pontuação total  <b>{session.TotalScore}</b>\n" +
            $"Flechas disparadas  {session.ShotsFired}\n" +
            $"10 pts  {session.Centers}    8 pts  {session.HitsFor(8)}    6 pts  {session.HitsFor(6)}\n" +
            $" 4 pts  {session.HitsFor(4)}    2 pts  {session.HitsFor(2)}    Erros  {session.Misses}\n" +
            $"Acertos  {session.Hits}     Precisão  {session.Accuracy:0.0}%\n" +
            $"Flechas aguardando resultado  {session.PendingShots}";

        static string ChallengeStats(ArcherySession session)
        {
            var text = new StringBuilder();
            if (session.Finished)
            {
                text.AppendLine($"<b>Fim do desafio   |   Total {session.TotalScore}</b>");
                for (int i = 0; i < session.Rounds.Count; i++)
                    text.AppendLine($"Rodada {i + 1}    {session.Rounds[i].Score} pontos");
                text.AppendLine($"Acertos no centro  {session.Centers}");
                return text.ToString();
            }

            text.AppendLine($"<b>Rodada {session.RoundNumber}/{ArcherySession.RoundCount}</b>    Flechas restantes  {session.ArrowsRemaining}");
            text.AppendLine($"Pontos da rodada  {session.RoundScore}    Total  {session.TotalScore}");
            string multiplier = session.PendingShots > 0 ? "aguardando resultado" : $"×{session.NextMultiplier}";
            text.AppendLine($"Próximo multiplicador  {multiplier}");
            if (session.LastShot is ShotResult last)
                text.AppendLine($"Última flecha  {last.BaseScore} × {last.Multiplier} = {last.Score}");
            else text.AppendLine("Última flecha  —");
            if (session.PendingShots > 0)
                text.AppendLine($"Flechas aguardando resultado  {session.PendingShots}");
            if (!session.CanFire) text.AppendLine("Aguarde o fim da rodada.");
            return text.ToString();
        }
    }
}
