using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LoLAR
{
    /// <summary>
    /// Reproduce en bucle la animación importada del GLB (estado "Loop" de un Animator o clip de Animation legacy),
    /// con una pausa aleatoria entre ciclos para que los campeones no ataquen sincronizados.
    /// Opcionalmente repite solo un tramo del clip (por ejemplo, la parte de vuelo de "Landing").
    /// </summary>
    public class ModelAnimationLooper : MonoBehaviour
    {
        public const string LoopStateName = "Loop";
        /// <summary>Copia del estado "Loop"; permite mezclar el final del tramo con su inicio.</summary>
        public const string LoopStateNameB = "LoopB";

        static readonly int s_LoopHash = Animator.StringToHash(LoopStateName);
        static readonly int s_LoopBHash = Animator.StringToHash(LoopStateNameB);

        public float speed = 1f;
        public Vector2 pauseBetweenCycles = new Vector2(0f, 0.8f);
        public bool playOnEnable = true;

        [Header("Tramo en bucle (tiempo normalizado del clip)")]
        [Tooltip("Inicio del tramo que se repite. 0 = inicio del clip.")]
        [Range(0f, 1f)] public float segmentStart;
        [Tooltip("Fin del tramo que se repite. 1 = final del clip.")]
        [Range(0f, 1f)] public float segmentEnd = 1f;
        [Tooltip("Segundos de mezcla al volver al inicio del tramo, para que no se note el salto.")]
        public float segmentBlend = 0.3f;

        /// <summary>Se invoca cada vez que empieza un ciclo (útil para sincronizar efectos de golpe).</summary>
        public event Action CycleStarted;

        Animator m_Animator;
        Animation m_Animation;
        bool m_Running;
        bool m_Waiting;
        bool m_HasPlayed;
        float m_ResumeAt;
        int m_PlayedFrame;
        int m_CurrentState = s_LoopHash;

        bool UsesSegment => segmentStart > 0f || segmentEnd < 1f;

        void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>(true);
            m_Animation = GetComponentInChildren<Animation>(true);

            if (m_Animator)
            {
                m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                m_Animator.applyRootMotion = false;
                m_Animator.speed = speed;
            }

            if (m_Animation)
            {
                m_Animation.cullingType = AnimationCullingType.AlwaysAnimate;
                m_Animation.playAutomatically = false;
                foreach (AnimationState state in m_Animation)
                {
                    state.wrapMode = WrapMode.Once;
                    state.speed = speed;
                }
            }

            // Los modelos son diminutos en AR; evita que el culling por bounds los haga desaparecer.
            foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.updateWhenOffscreen = true;
        }

        void OnEnable()
        {
            m_HasPlayed = false;
            if (playOnEnable)
                Begin(Random.Range(0f, pauseBetweenCycles.y));
        }

        void OnDisable() => m_Running = false;

        public void Begin(float delay = 0f)
        {
            m_Running = true;
            m_Waiting = true;
            m_ResumeAt = Time.time + delay;
        }

        public void Stop() => m_Running = false;

        void Update()
        {
            if (!m_Running)
                return;

            if (m_Waiting)
            {
                if (Time.time < m_ResumeAt)
                    return;

                m_Waiting = false;
                PlayFromStart();
                return;
            }

            // El estado del Animator no se actualiza hasta el frame siguiente a Play().
            if (Time.frameCount > m_PlayedFrame + 1 && IsFinished())
            {
                m_Waiting = true;
                m_ResumeAt = Time.time + Random.Range(pauseBetweenCycles.x, pauseBetweenCycles.y);
            }
        }

        void PlayFromStart()
        {
            m_PlayedFrame = Time.frameCount;

            if (m_Animator && m_Animator.runtimeAnimatorController)
            {
                float length = m_Animator.GetCurrentAnimatorStateInfo(0).length;
                bool canBlend = UsesSegment && m_HasPlayed && segmentBlend > 0f && length > 0f &&
                                m_Animator.HasState(0, s_LoopBHash);
                if (canBlend)
                {
                    // Alterna entre dos estados con el mismo clip para poder mezclar el final del tramo con su inicio.
                    // El estado entrante arranca un poco antes de segmentStart y llega a él justo cuando el saliente
                    // llega a segmentEnd (la mezcla empieza antes; ver IsFinished).
                    float blendNormalized = segmentBlend / length;
                    m_CurrentState = m_CurrentState == s_LoopHash ? s_LoopBHash : s_LoopHash;
                    m_Animator.CrossFade(m_CurrentState, blendNormalized, 0, Mathf.Max(0f, segmentStart - blendNormalized));
                }
                else
                {
                    m_CurrentState = s_LoopHash;
                    m_Animator.Play(s_LoopHash, 0, segmentStart);
                }
            }
            else if (m_Animation && m_Animation.clip)
            {
                var state = m_Animation[m_Animation.clip.name];
                m_Animation.Play(m_Animation.clip.name);
                state.normalizedTime = segmentStart;
            }
            else if (m_Animation)
            {
                m_Animation.Rewind();
                m_Animation.Play();
            }

            m_HasPlayed = true;
            CycleStarted?.Invoke();
        }

        bool IsFinished()
        {
            if (m_Animator && m_Animator.runtimeAnimatorController)
            {
                if (m_Animator.IsInTransition(0))
                    return false;

                var info = m_Animator.GetCurrentAnimatorStateInfo(0);
                float end = segmentEnd;
                // Con tramo, la mezcla empieza antes del final para terminar justo en segmentEnd.
                if (UsesSegment && segmentBlend > 0f && info.length > 0f)
                    end -= segmentBlend / info.length;
                return info.normalizedTime >= end;
            }

            if (m_Animation && m_Animation.clip)
            {
                var state = m_Animation[m_Animation.clip.name];
                return !m_Animation.isPlaying || state.normalizedTime >= segmentEnd;
            }

            return !m_Animation || !m_Animation.isPlaying;
        }
    }
}
