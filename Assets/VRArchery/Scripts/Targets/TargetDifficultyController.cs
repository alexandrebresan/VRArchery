using UnityEngine;
using VRArchery.Scoring;

namespace VRArchery.Targets
{
    public enum TargetDifficulty { Easy, Medium, AdvancedMedium, Hard }

    public sealed class TargetDifficultyController : MonoBehaviour
    {
        [SerializeField] MovingTarget m_Target;
        [SerializeField] TargetMotionProfile m_Easy = new TargetMotionProfile(6);
        [SerializeField] TargetMotionProfile m_Medium = new TargetMotionProfile(8, 1.2f, 0.22f);
        [SerializeField] TargetMotionProfile m_AdvancedMedium = new TargetMotionProfile(8, 1.2f, 0.42f);
        [SerializeField] TargetMotionProfile m_Hard = new TargetMotionProfile(10, 1.55f, 0.31f, 0.42f, 0.37f);

        ArcherySession appliedSession;
        bool appliedActive;

        public TargetDifficulty Current { get; private set; }
        public TargetMotionProfile CurrentProfile => m_Target.Profile;
        public TargetDifficulty TrainingChoice { get; private set; }
        public bool IsWaitingForShots { get; private set; }

        internal void ChooseForTraining(TargetDifficulty difficulty)
        {
            if (difficulty == TargetDifficulty.Easy || difficulty == TargetDifficulty.Medium || difficulty == TargetDifficulty.Hard)
                TrainingChoice = difficulty;
        }

        internal void Sync(ArcherySession session, bool active)
        {
            bool challenge = active && session.Mode == GameMode.Challenge;
            var requested = challenge ? ForRound(session.RoundNumber) : TrainingChoice;
            // Adia a troca de perfil até resolver os disparos em voo na dificuldade atual.
            IsWaitingForShots = active && !challenge && requested != Current && session.PendingShots > 0;
            if (IsWaitingForShots) return;
            if (appliedSession == session && appliedActive == active && Current == requested) return;

            m_Target.Configure(ProfileFor(requested));
            Current = requested;
            appliedSession = session;
            appliedActive = active;
        }

        /// <summary>Aplica uma progressão fixa no desafio, com uma variação mais rápida do nível médio na terceira rodada.</summary>
        public static TargetDifficulty ForRound(int round) => round switch
        {
            2 => TargetDifficulty.Medium,
            3 => TargetDifficulty.AdvancedMedium,
            4 => TargetDifficulty.Hard,
            _ => TargetDifficulty.Easy
        };

        TargetMotionProfile ProfileFor(TargetDifficulty difficulty) => difficulty switch
        {
            TargetDifficulty.Medium => m_Medium,
            TargetDifficulty.AdvancedMedium => m_AdvancedMedium,
            TargetDifficulty.Hard => m_Hard,
            _ => m_Easy
        };
    }
}
