using System;
using System.Collections.Generic;
using UnityEngine;
using VRArchery.Arrows;
using VRArchery.Targets;

namespace VRArchery.Scoring
{
    public sealed class ScoringMatch : MonoBehaviour
    {
        [SerializeField] BowArrowLauncher m_Launcher;
        [SerializeField] bool m_GameFlow;
        [SerializeField] TargetDifficultyController m_TargetDifficulty;
        [SerializeField, Min(0f), Tooltip("Seconds to resolve a missing shot. Zero uses the projectile lifetime.")]
        float m_ShotResolutionSeconds;

        readonly List<PendingShot> pending = new List<PendingShot>();
        ArcherySession training = new ArcherySession(GameMode.Training);
        ArcherySession challenge = new ArcherySession(GameMode.Challenge);
        bool sessionStarted;

        public GameMode Mode { get; private set; }
        public bool IsSessionActive => !m_GameFlow || sessionStarted;
        public TargetDifficultyController Difficulty => m_TargetDifficulty;
        public ArcherySession Current => Mode == GameMode.Training ? training : challenge;
        public event Action Changed;
        public event Action<ShotResult> Scored;

        void Awake()
        {
            training.ShotApplied += OnShotApplied;
            challenge = CreateSession(GameMode.Challenge);
        }

        void OnEnable()
        {
            m_Launcher.ArrowLaunched += OnLaunched;
            Publish();
        }

        void OnDisable()
        {
            if (m_Launcher != null)
            {
                m_Launcher.ArrowLaunched -= OnLaunched;
                m_Launcher.LaunchBlocked = false;
            }
            foreach (var shot in pending)
            {
                if (shot.Arrow != null) shot.Arrow.Impacted -= OnImpacted;
                shot.Session.ResolveShot(shot.Id, 0);
            }
            pending.Clear();
        }

        public void SelectMode(GameMode mode)
        {
            Mode = mode;
            Publish();
        }

        public void StartSession(GameMode mode)
        {
            Mode = mode;
            sessionStarted = true;
            Restart();
        }

        public void SelectTrainingDifficulty(TargetDifficulty difficulty)
        {
            if (m_TargetDifficulty == null || (IsSessionActive && Mode == GameMode.Challenge)) return;
            m_TargetDifficulty.ChooseForTraining(difficulty);
            Publish();
        }

        public void ReturnToMenu()
        {
            sessionStarted = false;
            Restart();
        }

        public void AdvanceRound()
        {
            if (IsSessionActive && Current.AdvanceRound()) Publish();
        }

        public void Restart()
        {
            var previous = Current;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var shot = pending[i];
                if (shot.Session != previous) continue;
                if (shot.Arrow != null) shot.Arrow.Impacted -= OnImpacted;
                pending.RemoveAt(i);
            }
            previous.ShotApplied -= OnShotApplied;
            if (Mode == GameMode.Training) training = CreateSession(Mode);
            else challenge = CreateSession(Mode);
            Publish();
        }

        ArcherySession CreateSession(GameMode mode)
        {
            var session = new ArcherySession(mode, m_GameFlow);
            session.ShotApplied += OnShotApplied;
            return session;
        }

        void OnShotApplied(ArcherySession session, ShotResult result)
        {
            if (IsSessionActive && session == Current) Scored?.Invoke(result);
        }

        void OnLaunched(ArrowProjectile arrow)
        {
            if (!IsSessionActive) return;
            var session = Current;
            int id = session.RegisterShot();
            if (id == 0) return;
            // O prazo resolve como erro os tiros que não atingem o alvo a tempo e permite encerrar a rodada.
            float deadline = m_ShotResolutionSeconds > 0 ? Time.time + m_ShotResolutionSeconds : float.PositiveInfinity;
            pending.Add(new PendingShot(arrow, session, id, deadline));
            arrow.Impacted += OnImpacted;
            Publish();
        }

        void OnImpacted(ArrowImpact impact)
        {
            int index = pending.FindIndex(shot => shot.Arrow == impact.Arrow);
            if (index < 0) return;
            int points = Time.time < pending[index].Deadline && impact.Collider.TryGetComponent<ScoringTarget>(out var target)
                ? target.ScoreImpactAt(impact.Point) : 0;
            Resolve(index, points);
            Publish();
        }

        void Update()
        {
            bool expired = false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                // Stage 3 destroys arrows on expiry, including ones that never hit anything.
                if (pending[i].Arrow != null && Time.time < pending[i].Deadline) continue;
                Resolve(i, 0);
                expired = true;
            }
            if (expired) Publish();
        }

        void Resolve(int index, int points)
        {
            var shot = pending[index];
            pending.RemoveAt(index);
            if (shot.Arrow != null) shot.Arrow.Impacted -= OnImpacted;
            shot.Session.ResolveShot(shot.Id, points);
        }

        // A partida controla a liberação do disparo e sincroniza o alvo antes de atualizar a interface.
        void Publish()
        {
            if (isActiveAndEnabled && m_Launcher != null)
            {
                if (m_TargetDifficulty != null) m_TargetDifficulty.Sync(Current, IsSessionActive);
                m_Launcher.LaunchBlocked = !IsSessionActive || !Current.CanFire ||
                    (m_TargetDifficulty != null && m_TargetDifficulty.IsWaitingForShots);
            }
            Changed?.Invoke();
        }

        readonly struct PendingShot
        {
            public readonly ArrowProjectile Arrow;
            public readonly ArcherySession Session;
            public readonly int Id;
            public readonly float Deadline;

            public PendingShot(ArrowProjectile arrow, ArcherySession session, int id, float deadline)
            {
                Arrow = arrow;
                Session = session;
                Id = id;
                Deadline = deadline;
            }
        }
    }
}
