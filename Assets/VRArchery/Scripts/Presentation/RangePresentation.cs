using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRArchery.Arrows;
using VRArchery.Bow;
using VRArchery.Scoring;

namespace VRArchery.Presentation
{
    // The bow must accept the selection before the draw cue reads its state.
    [DefaultExecutionOrder(220)]
    public sealed class RangePresentation : MonoBehaviour
    {
        [SerializeField] BowController m_Bow;
        [SerializeField] BowArrowLauncher m_Launcher;
        [SerializeField] ScoringMatch m_Match;
        [SerializeField] AudioSource m_Sound;
        [SerializeField] AudioClip m_Cue;
        [SerializeField] Transform m_ImpactRing;

        readonly List<ArrowProjectile> watched = new List<ArrowProjectile>();
        ArcherySession observedSession;
        int observedRounds;
        float markerStarted;

        void OnEnable()
        {
            m_Bow.String.selectEntered.AddListener(OnDraw);
            m_Launcher.ArrowLaunched += OnLaunch;
            m_Match.Scored += OnScore;
            m_Match.Changed += OnMatchChanged;
            OnMatchChanged();
        }

        void OnDisable()
        {
            m_Bow.String.selectEntered.RemoveListener(OnDraw);
            m_Launcher.ArrowLaunched -= OnLaunch;
            m_Match.Scored -= OnScore;
            m_Match.Changed -= OnMatchChanged;
            foreach (var arrow in watched)
                if (arrow != null) arrow.Impacted -= OnImpact;
            watched.Clear();
            if (m_ImpactRing != null) m_ImpactRing.gameObject.SetActive(false);
        }

        void OnDraw(SelectEnterEventArgs args)
        {
            if (m_Bow.State == BowState.Drawing && !m_Launcher.LaunchBlocked) Play(0.83f, 0.20f);
        }

        void OnLaunch(ArrowProjectile arrow)
        {
            Play(1.25f, 0.48f);
            watched.RemoveAll(item => item == null);
            watched.Add(arrow);
            arrow.Impacted += OnImpact;
        }

        void OnImpact(ArrowImpact hit)
        {
            hit.Arrow.Impacted -= OnImpact;
            watched.Remove(hit.Arrow);
            if (m_ImpactRing == null || !hit.Collider.TryGetComponent<ScoringTarget>(out var target)) return;

            // Mark where the contact occurred, even if the last result changes the target profile.
            m_ImpactRing.SetPositionAndRotation(hit.Point + hit.Normal * 0.02f, target.transform.rotation);
            markerStarted = Time.time;
            m_ImpactRing.gameObject.SetActive(true);
            LateUpdate();
        }

        void LateUpdate()
        {
            if (m_ImpactRing == null || !m_ImpactRing.gameObject.activeSelf) return;
            float age = (Time.time - markerStarted) / 0.26f;
            if (age >= 1)
            {
                m_ImpactRing.gameObject.SetActive(false);
                return;
            }
            m_ImpactRing.localScale = Vector3.one * Mathf.Lerp(0.055f, 0.15f, age);
        }

        void OnScore(ShotResult result)
        {
            if (result.BaseScore == 0) Play(0.62f, 0.32f);
            else if (result.BaseScore == 10) Play(1.65f, 0.56f);
            else Play(0.95f, 0.42f);
        }

        void OnMatchChanged()
        {
            var session = m_Match.Current;
            int rounds = session.Rounds.Count;
            if (session == observedSession && m_Match.IsSessionActive && rounds > observedRounds)
                Play(session.Finished ? 1.78f : 1.42f, session.Finished ? 0.65f : 0.50f);
            observedSession = session;
            observedRounds = rounds;
        }

        void Play(float pitch, float gain)
        {
            if (m_Sound == null || !m_Sound.isActiveAndEnabled || m_Cue == null) return;
            m_Sound.pitch = pitch;
            m_Sound.PlayOneShot(m_Cue, gain);
        }
    }
}
