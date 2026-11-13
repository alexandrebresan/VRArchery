using System;
using System.Collections.Generic;

namespace VRArchery.Scoring
{
    public enum GameMode { Training, Challenge }

    public readonly struct ShotResult
    {
        public int BaseScore { get; }
        public int Multiplier { get; }
        public int Score => BaseScore * Multiplier;

        public ShotResult(int baseScore, int multiplier)
        {
            BaseScore = baseScore;
            Multiplier = multiplier;
        }
    }

    public sealed class ArcherySession
    {
        public const int RoundCount = 4;
        public const int ArrowsPerRound = 3;

        readonly Dictionary<int, int?> pending = new Dictionary<int, int?>();
        readonly List<(int Score, int Centers)> rounds = new List<(int, int)>();
        readonly List<ShotResult> roundShots = new List<ShotResult>(ArrowsPerRound);
        readonly int[] counts = new int[6];
        readonly bool manualRoundAdvance;
        int roundCenters;
        int resolvedShots;
        int centerStreak;

        public GameMode Mode { get; }
        public int ShotsFired { get; private set; }
        public int PendingShots => ShotsFired - resolvedShots;
        public int TotalScore { get; private set; }
        public int Misses => counts[0];
        public int Hits => resolvedShots - Misses;
        public int Centers => counts[5];
        public float Accuracy => resolvedShots == 0 ? 0 : 100f * Hits / resolvedShots;
        public int RoundNumber => AwaitingNextRound ? rounds.Count : Math.Min(rounds.Count + 1, RoundCount);
        public int RoundScore { get; private set; }
        public int NextMultiplier { get; private set; } = 1;
        public ShotResult? LastShot { get; private set; }
        public IReadOnlyList<(int Score, int Centers)> Rounds => rounds;
        public IReadOnlyList<ShotResult> RoundShots => roundShots;
        public bool Finished => Mode == GameMode.Challenge && rounds.Count == RoundCount;
        public int ArrowsRemaining => Mode == GameMode.Training || Finished ? 0 : RoundNumber * ArrowsPerRound - ShotsFired;
        public bool CanFire => Mode == GameMode.Training || ArrowsRemaining > 0;
        public bool AwaitingNextRound { get; private set; }
        public int LongestCenterStreak { get; private set; }
        public event Action<ArcherySession, ShotResult> ShotApplied;

        public ArcherySession(GameMode mode, bool manualRoundAdvance = false)
        {
            Mode = mode;
            this.manualRoundAdvance = manualRoundAdvance;
        }

        public bool AdvanceRound()
        {
            if (!AwaitingNextRound) return false;
            AwaitingNextRound = false;
            ResetRound();
            return true;
        }

        public int HitsFor(int score)
        {
            if (score < 0 || score > 10 || score % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(score));
            return counts[score / 2];
        }

        public int RegisterShot()
        {
            if (!CanFire) return 0;
            int id = ++ShotsFired;
            pending.Add(id, null);
            return id;
        }

        public bool ResolveShot(int id, int baseScore)
        {
            if (!pending.TryGetValue(id, out var result) || result.HasValue) return false;
            if (baseScore < 0 || baseScore > 10 || baseScore % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(baseScore));

            if (Mode == GameMode.Training)
            {
                pending.Remove(id);
                Apply(baseScore);
                return true;
            }

            // Physical contacts can occur in a different order from releases.
            pending[id] = baseScore;
            while (pending.TryGetValue(resolvedShots + 1, out result) && result.HasValue)
            {
                pending.Remove(resolvedShots + 1);
                Apply(result.Value);
            }
            return true;
        }

        void Apply(int baseScore)
        {
            var shot = new ShotResult(baseScore, NextMultiplier);
            LastShot = shot;
            TotalScore += shot.Score;
            counts[baseScore / 2]++;
            resolvedShots++;
            if (Mode == GameMode.Challenge) ApplyChallengeShot(shot);
            ShotApplied?.Invoke(this, shot);
        }

        void ApplyChallengeShot(ShotResult shot)
        {
            roundShots.Add(shot);
            RoundScore += shot.Score;
            bool center = shot.BaseScore == 10;
            if (center) roundCenters++;
            centerStreak = center ? centerStreak + 1 : 0;
            LongestCenterStreak = Math.Max(LongestCenterStreak, centerStreak);
            // O centro dobra o fator da próxima flecha. O resultado atual já foi calculado com o fator anterior.
            NextMultiplier = center ? NextMultiplier * 2 : 1;
            if (resolvedShots % ArrowsPerRound != 0) return;

            rounds.Add((RoundScore, roundCenters));
            NextMultiplier = 1;
            if (Finished) return;
            if (manualRoundAdvance) AwaitingNextRound = true;
            else ResetRound();
        }

        void ResetRound()
        {
            RoundScore = 0;
            roundCenters = 0;
            roundShots.Clear();
        }
    }
}
