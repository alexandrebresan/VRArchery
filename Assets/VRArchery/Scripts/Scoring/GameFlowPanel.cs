using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRArchery.Targets;

namespace VRArchery.Scoring
{
    public sealed class GameFlowPanel : MonoBehaviour
    {
        enum Screen { Menu, Training, Challenge, RoundResults, FinalResults }

        [SerializeField] ScoringMatch m_Match;
        [SerializeField] TMP_Text m_Title;
        [SerializeField] TMP_Text m_Body;
        [SerializeField] TMP_Text m_Feedback;
        [SerializeField] Button m_Primary;
        [SerializeField] Button m_Secondary;
        [SerializeField] Button m_DifficultyButton;
        [SerializeField, Min(0.1f)] float m_FeedbackSeconds = 1.4f;

        readonly Queue<ShotResult> feedback = new Queue<ShotResult>();
        TMP_Text primaryLabel;
        TMP_Text secondaryLabel;
        TMP_Text difficultyLabel;
        float feedbackUntil;

        // Cada tela deriva do estado da partida para que os botões e os resultados acompanhem a rodada.
        Screen CurrentScreen
        {
            get
            {
                if (!m_Match.IsSessionActive) return Screen.Menu;
                var session = m_Match.Current;
                if (session.Mode == GameMode.Training) return Screen.Training;
                if (session.Finished) return Screen.FinalResults;
                return session.AwaitingNextRound ? Screen.RoundResults : Screen.Challenge;
            }
        }

        void Awake()
        {
            primaryLabel = m_Primary.GetComponentInChildren<TMP_Text>(true);
            secondaryLabel = m_Secondary.GetComponentInChildren<TMP_Text>(true);
            if (m_DifficultyButton != null) difficultyLabel = m_DifficultyButton.GetComponentInChildren<TMP_Text>(true);
        }

        void OnEnable()
        {
            m_Primary.onClick.AddListener(UsePrimary);
            m_Secondary.onClick.AddListener(UseSecondary);
            if (m_DifficultyButton != null) m_DifficultyButton.onClick.AddListener(CycleDifficulty);
            m_Match.Changed += Refresh;
            m_Match.Scored += OnScored;
            ClearFeedback();
            Refresh();
        }

        void OnDisable()
        {
            m_Primary.onClick.RemoveListener(UsePrimary);
            m_Secondary.onClick.RemoveListener(UseSecondary);
            if (m_DifficultyButton != null) m_DifficultyButton.onClick.RemoveListener(CycleDifficulty);
            m_Match.Changed -= Refresh;
            m_Match.Scored -= OnScored;
        }

        void UsePrimary()
        {
            ClearFeedback();
            switch (CurrentScreen)
            {
                case Screen.Menu: m_Match.StartSession(GameMode.Training); break;
                case Screen.Training:
                case Screen.FinalResults: m_Match.Restart(); break;
                case Screen.RoundResults: m_Match.AdvanceRound(); break;
            }
        }

        void UseSecondary()
        {
            ClearFeedback();
            if (CurrentScreen == Screen.Menu) m_Match.StartSession(GameMode.Challenge);
            else m_Match.ReturnToMenu();
        }

        void CycleDifficulty()
        {
            var next = m_Match.Difficulty.TrainingChoice switch
            {
                TargetDifficulty.Easy => TargetDifficulty.Medium,
                TargetDifficulty.Medium => TargetDifficulty.Hard,
                _ => TargetDifficulty.Easy
            };
            m_Match.SelectTrainingDifficulty(next);
        }

        void Refresh()
        {
            var session = m_Match.Current;
            var screen = CurrentScreen;
            secondaryLabel.text = "Menu Principal";
            m_Primary.gameObject.SetActive(screen != Screen.Challenge);
            switch (screen)
            {
                case Screen.Menu:
                    m_Title.text = "VRArchery";
                    m_Body.text = "<b>Treino Livre</b>  •  pratique sem limite de flechas ou tempo.\n" +
                        "<b>Desafio por Rodadas</b>  •  4 rodadas de 3 flechas. Um centro dobra o multiplicador da próxima flecha.\n\n" +
                        "Segure o arco com a mão esquerda. Agarre e puxe a corda com a direita. Mire e solte.";
                    primaryLabel.text = "Treino Livre";
                    secondaryLabel.text = "Desafio por Rodadas";
                    break;
                case Screen.Training:
                    m_Title.text = "Treino Livre";
                    m_Body.text = ScorePanel.TrainingStats(session);
                    primaryLabel.text = "Reiniciar Treino";
                    break;
                case Screen.Challenge:
                    m_Title.text = "Desafio por Rodadas";
                    m_Body.text = ChallengeStats(session);
                    break;
                case Screen.RoundResults:
                    m_Title.text = $"Rodada {session.RoundNumber} concluída";
                    m_Body.text = RoundSummary(session);
                    primaryLabel.text = "Próxima Rodada";
                    break;
                case Screen.FinalResults:
                    m_Title.text = "Desafio concluído";
                    m_Body.text = FinalSummary(session);
                    primaryLabel.text = "Jogar Novamente";
                    break;
            }
            RefreshDifficulty(screen, session.RoundNumber);
        }

        void RefreshDifficulty(Screen screen, int round)
        {
            var difficulty = m_Match.Difficulty;
            if (difficulty == null || m_DifficultyButton == null) return;
            bool choosing = screen == Screen.Menu || screen == Screen.Training;
            m_DifficultyButton.gameObject.SetActive(choosing);
            difficultyLabel.text = $"Dificuldade do treino   {DifficultyName(difficulty.TrainingChoice)}   ›";

            var profile = difficulty.CurrentProfile;
            bool horizontal = profile.HorizontalAmplitude > 0 && profile.HorizontalFrequency > 0;
            bool vertical = profile.VerticalAmplitude > 0 && profile.VerticalFrequency > 0;
            string behavior = horizontal ? (vertical ? "horizontal e vertical" : "horizontal") :
                (vertical ? "vertical" : "estático");
            m_Body.text = $"<b>{DifficultyName(difficulty.Current)}</b>   ·   {profile.Distance:0.#} m   ·   {behavior}\n" + m_Body.text;
            if (difficulty.IsWaitingForShots)
                m_Body.text += "\nAguardando as flechas antes de mudar o alvo.";
            if (screen == Screen.RoundResults)
                m_Body.text += $"Próxima rodada   <b>{DifficultyName(TargetDifficultyController.ForRound(round + 1))}</b>";
        }

        static string DifficultyName(TargetDifficulty difficulty) => difficulty switch
        {
            TargetDifficulty.Medium => "Médio",
            TargetDifficulty.AdvancedMedium => "Médio avançado",
            TargetDifficulty.Hard => "Difícil",
            _ => "Fácil"
        };

        void OnScored(ShotResult result)
        {
            if (m_Match.Mode == GameMode.Training)
            {
                ShowFeedback(result);
                return;
            }
            feedback.Enqueue(result);
            if (Time.unscaledTime >= feedbackUntil) ShowNextFeedback();
        }

        void Update()
        {
            if (feedbackUntil > 0 && Time.unscaledTime >= feedbackUntil) ShowNextFeedback();
        }

        void ShowNextFeedback()
        {
            if (feedback.Count == 0)
            {
                ClearFeedback();
                return;
            }
            ShowFeedback(feedback.Dequeue());
        }

        void ShowFeedback(ShotResult shot)
        {
            m_Feedback.text = shot.BaseScore == 0 ? "Errou! +0 pontos" : shot.Multiplier == 1
                ? $"+{shot.Score} pontos" : $"+{shot.Score} pontos ({shot.BaseScore} × {shot.Multiplier})";
            feedbackUntil = Time.unscaledTime + m_FeedbackSeconds;
        }

        void ClearFeedback()
        {
            feedback.Clear();
            feedbackUntil = 0;
            m_Feedback.text = string.Empty;
        }

        static string ChallengeStats(ArcherySession session)
        {
            int used = ArcherySession.ArrowsPerRound - session.ArrowsRemaining;
            string multiplier = session.PendingShots == 0 ? $"×{session.NextMultiplier}" : $"×{session.NextMultiplier} (aguardando resultado)";
            string last = session.LastShot is ShotResult shot ? Breakdown(shot) : "—";
            string waiting = session.CanFire ? string.Empty : "\nAguarde a resolução das três flechas.";
            return $"<b>Rodada {session.RoundNumber}/{ArcherySession.RoundCount}</b>\n" +
                $"Flechas usadas {used}   |   Restantes {session.ArrowsRemaining}\n" +
                $"Pontos da rodada {session.RoundScore}   |   Total {session.TotalScore}\n" +
                $"Multiplicador ativo {multiplier}\nÚltimo disparo  {last}{waiting}";
        }

        static string RoundSummary(ArcherySession session)
        {
            var text = new StringBuilder($"<b>{session.RoundScore} pontos na rodada</b>   |   Total {session.TotalScore}\n");
            for (int i = 0; i < session.RoundShots.Count; i++)
                text.AppendLine($"Flecha {i + 1}   {Breakdown(session.RoundShots[i])}");
            return text.ToString();
        }

        static string FinalSummary(ArcherySession session)
        {
            var text = new StringBuilder($"<b>Pontuação final  {session.TotalScore}</b>\n");
            for (int i = 0; i < session.Rounds.Count; i++)
            {
                text.Append($"Rodada {i + 1}   {session.Rounds[i].Score} pontos");
                text.Append(i % 2 == 0 ? "    |    " : "\n");
            }
            text.Append("Flechas da rodada 4  ");
            for (int i = 0; i < session.RoundShots.Count; i++)
            {
                if (i > 0) text.Append("  |  ");
                text.Append(session.RoundShots[i].Score);
            }
            text.AppendLine(" pontos");
            text.AppendLine($"Acertos no centro  {session.Centers}");
            text.Append($"Maior sequência de centros  {session.LongestCenterStreak}");
            return text.ToString();
        }

        static string Breakdown(ShotResult result) => $"{result.BaseScore} × {result.Multiplier} = {result.Score} pontos";
    }
}
