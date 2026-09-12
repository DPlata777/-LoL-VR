using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LoLAR
{
    /// <summary>
    /// Reproduce en bucle la animación importada del GLB (estado "Loop" de un Animator o clip de Animation legacy),
    /// con una pausa aleatoria entre ciclos para que los campeones no ataquen sincronizados.
    /// </summary>
    public class ModelAnimationLooper : MonoBehaviour
    {
        public const string LoopStateName = "Loop";

        public float speed = 1f;
        public Vector2 pauseBetweenCycles = new Vector2(0f, 0.8f);
        public bool playOnEnable = true;

        /// <summary>Se invoca cada vez que empieza un ciclo (útil para sincronizar efectos de golpe).</summary>
        public event Action CycleStarted;

        Animator m_Animator;
        Animation m_Animation;
        bool m_Running;
        bool m_Waiting;
        float m_ResumeAt;
        int m_PlayedFrame;

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
                m_Animator.Play(LoopStateName, 0, 0f);
            }
            else if (m_Animation)
            {
                m_Animation.Rewind();
                if (m_Animation.clip)
                    m_Animation.Play(m_Animation.clip.name);
                else
                    m_Animation.Play();
            }

            CycleStarted?.Invoke();
        }

        bool IsFinished()
        {
            if (m_Animator && m_Animator.runtimeAnimatorController)
                return m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
            if (m_Animation)
                return !m_Animation.isPlaying;
            return true;
        }
    }
}
