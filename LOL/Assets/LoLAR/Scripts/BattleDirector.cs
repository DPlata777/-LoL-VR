using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LoLAR
{
    /// <summary>
    /// Cinemática de combate por el Dragón. Se reinicia cada vez que el contenido se activa:
    /// 1. Aparece la fosa. 2. El dragón se yergue y ruge.
    /// 3. Los campeones aparecen alrededor. 4. Bucle de combate: fuego del dragón y ataques de los campeones.
    /// </summary>
    public class BattleDirector : MonoBehaviour
    {
        [Header("Escena")]
        public Transform pit;
        public Transform dragon;
        public Transform[] champions = Array.Empty<Transform>();

        [Header("Efectos")]
        public ParticleSystem dragonFire;
        public ParticleSystem roarBurst;
        public ParticleSystem hitSparks;
        public ParticleSystem[] spawnFlashes = Array.Empty<ParticleSystem>();
        public Color[] championHitColors = Array.Empty<Color>();
        public AudioSource roarAudio;

        [Header("Tiempos (s)")]
        public float pitDuration = 0.5f;
        public float dragonRiseDuration = 1.2f;
        public float roarDuration = 0.8f;
        public float championSpawnInterval = 0.3f;
        public Vector2 fireInterval = new Vector2(2f, 3.5f);
        public float dragonTurnDuration = 0.45f;

        [Header("Medidas (m)")]
        public float dragonHeight = 0.06f;
        public float recoilDistance = 0.008f;

        Vector3 m_PitScale;
        Vector3 m_DragonPosition;
        Vector3 m_DragonScale;
        Quaternion m_DragonRotation;
        Vector3[] m_ChampionPositions;
        Vector3[] m_ChampionScales;
        ModelAnimationLooper[] m_ChampionLoopers;
        Action[] m_AttackHandlers;
        bool m_Initialized;

        void Awake() => Initialize();

        void OnEnable()
        {
            Initialize();
            ResetState();
            SetAttackListeners(true);
            StartCoroutine(Run());
        }

        void OnDisable()
        {
            StopAllCoroutines();
            SetAttackListeners(false);
            ResetState();
        }

        void Initialize()
        {
            if (m_Initialized)
                return;
            m_Initialized = true;

            if (pit)
                m_PitScale = pit.localScale;

            if (dragon)
            {
                m_DragonPosition = dragon.localPosition;
                m_DragonScale = dragon.localScale;
                m_DragonRotation = dragon.localRotation;
            }

            int count = champions.Length;
            m_ChampionPositions = new Vector3[count];
            m_ChampionScales = new Vector3[count];
            m_ChampionLoopers = new ModelAnimationLooper[count];
            m_AttackHandlers = new Action[count];
            for (int i = 0; i < count; i++)
            {
                m_ChampionPositions[i] = champions[i].localPosition;
                m_ChampionScales[i] = champions[i].localScale;
                m_ChampionLoopers[i] = champions[i].GetComponent<ModelAnimationLooper>();
                int index = i;
                m_AttackHandlers[i] = () => OnChampionAttack(index);
            }
        }

        void SetAttackListeners(bool subscribe)
        {
            for (int i = 0; i < m_ChampionLoopers.Length; i++)
            {
                if (!m_ChampionLoopers[i])
                    continue;

                if (subscribe)
                    m_ChampionLoopers[i].CycleStarted += m_AttackHandlers[i];
                else
                    m_ChampionLoopers[i].CycleStarted -= m_AttackHandlers[i];
            }
        }

        void ResetState()
        {
            if (pit)
                pit.localScale = Vector3.zero;

            if (dragon)
            {
                dragon.localPosition = m_DragonPosition - Vector3.up * dragonHeight;
                dragon.localScale = m_DragonScale * 0.2f;
                dragon.localRotation = m_DragonRotation;
            }

            for (int i = 0; i < champions.Length; i++)
            {
                champions[i].gameObject.SetActive(false);
                champions[i].localPosition = m_ChampionPositions[i];
                champions[i].localScale = Vector3.zero;
            }

            Stop(dragonFire);
            Stop(roarBurst);
            Stop(hitSparks);
        }

        IEnumerator Run()
        {
            // Fase 1: la fosa del dragón se abre.
            yield return Tween(pitDuration, t =>
            {
                if (pit)
                    pit.localScale = m_PitScale * EaseOutBack(t);
            });

            // Fase 2: el dragón se yergue y ruge.
            yield return Tween(dragonRiseDuration, t =>
            {
                if (!dragon)
                    return;
                dragon.localPosition = Vector3.LerpUnclamped(m_DragonPosition - Vector3.up * dragonHeight, m_DragonPosition, EaseOutCubic(t));
                dragon.localScale = m_DragonScale * Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(t));
            });

            Play(roarBurst);
            if (roarAudio)
                roarAudio.Play();
            yield return Shake(dragon, m_DragonPosition, roarDuration, dragonHeight * 0.03f);

            // Fase 3: los campeones aparecen alrededor de la fosa.
            for (int i = 0; i < champions.Length; i++)
            {
                champions[i].gameObject.SetActive(true);
                if (i < spawnFlashes.Length)
                    Play(spawnFlashes[i]);
                StartCoroutine(Pop(champions[i], m_ChampionScales[i]));
                yield return new WaitForSeconds(championSpawnInterval);
            }

            yield return new WaitForSeconds(0.4f);

            // Fase 4: combate. El dragón elige un objetivo, gira y lanza fuego.
            int lastTarget = -1;
            while (true)
            {
                int target = PickTarget(lastTarget);
                lastTarget = target;

                if (target >= 0)
                {
                    yield return TurnDragonTowards(champions[target]);
                    Play(dragonFire);
                    StartCoroutine(Recoil(target, 0.25f));
                }

                yield return new WaitForSeconds(Random.Range(fireInterval.x, fireInterval.y));
            }
        }

        int PickTarget(int previous)
        {
            if (champions.Length == 0)
                return -1;
            if (champions.Length == 1)
                return 0;

            int target;
            do
                target = Random.Range(0, champions.Length);
            while (target == previous);
            return target;
        }

        IEnumerator TurnDragonTowards(Transform target)
        {
            if (!dragon)
                yield break;

            var direction = target.localPosition - m_DragonPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-8f)
                yield break;

            var from = dragon.localRotation;
            var to = Quaternion.LookRotation(direction.normalized, Vector3.up);
            yield return Tween(dragonTurnDuration, t => dragon.localRotation = Quaternion.Slerp(from, to, EaseInOut(t)));
        }

        IEnumerator Recoil(int index, float delay)
        {
            yield return new WaitForSeconds(delay);

            var champion = champions[index];
            var basePosition = m_ChampionPositions[index];
            var away = basePosition - m_DragonPosition;
            away.y = 0f;
            away = away.normalized * recoilDistance;

            yield return Tween(0.12f, t => champion.localPosition = basePosition + away * t);
            yield return Tween(0.35f, t => champion.localPosition = basePosition + away * (1f - EaseOutCubic(t)));
        }

        void OnChampionAttack(int index)
        {
            if (isActiveAndEnabled && champions[index].gameObject.activeInHierarchy)
                StartCoroutine(EmitHit(index, 0.25f));
        }

        IEnumerator EmitHit(int index, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!hitSparks || !dragon)
                yield break;

            var center = dragon.position + transform.up * (dragonHeight * 0.45f);
            var toChampion = (champions[index].position - center).normalized;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = center + toChampion * (dragonHeight * 0.35f),
                applyShapeToPosition = true,
            };
            if (index < championHitColors.Length)
                emitParams.startColor = championHitColors[index];

            hitSparks.Emit(emitParams, 14);
        }

        static IEnumerator Pop(Transform target, Vector3 finalScale)
        {
            yield return Tween(0.35f, t => target.localScale = finalScale * EaseOutBack(t));
        }

        static IEnumerator Shake(Transform target, Vector3 basePosition, float duration, float amplitude)
        {
            if (!target)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damping = 1f - elapsed / duration;
                target.localPosition = basePosition + Random.insideUnitSphere * (amplitude * damping);
                yield return null;
            }
            target.localPosition = basePosition;
        }

        static IEnumerator Tween(float duration, Action<float> step)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                step(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            step(1f);
        }

        static void Play(ParticleSystem system)
        {
            if (system)
                system.Play(true);
        }

        static void Stop(ParticleSystem system)
        {
            if (system)
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        static float EaseInOut(float t) => t * t * (3f - 2f * t);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
