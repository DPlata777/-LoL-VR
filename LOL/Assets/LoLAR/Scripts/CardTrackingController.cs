using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LoLAR
{
    /// <summary>
    /// Escucha las cartas detectadas por ARTrackedImageManager y decide qué contenido mostrar:
    /// - Carta Mapa   -> mapa de la Grieta con los pop-ups de cada rol.
    /// - Carta Dragón -> dragón solo.
    /// - Ambas cartas a menos de <see cref="battleEnterDistance"/> -> batalla sobre la carta del dragón (el mapa se oculta).
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class CardTrackingController : MonoBehaviour
    {
        [Header("Nombres en la Reference Image Library")]
        public string mapCardName = "MapCard";
        public string dragonCardName = "DragonCard";

        [Header("Contenido")]
        public GameObject mapContentPrefab;
        public GameObject dragonContentPrefab;
        public GameObject battleContentPrefab;

        [Header("Proximidad (metros)")]
        [Tooltip("Distancia entre centros de las cartas para iniciar la batalla.")]
        public float battleEnterDistance = 0.15f;
        [Tooltip("Distancia para terminarla. Un poco mayor que la de entrada evita parpadeos.")]
        public float battleExitDistance = 0.18f;

        [Header("Tracking")]
        [Tooltip("Segundos que el contenido sigue visible después de perder la carta.")]
        public float lostGraceTime = 0.35f;
        [Tooltip("Segundos que una carta sigue contando para la batalla después de verla bien. Con las dos cartas en cuadro, " +
                 "ARCore suele rastrear bien solo una y reporta la otra con su última posición conocida; como las cartas " +
                 "están quietas sobre la mesa, esa posición sirve para medir la distancia.")]
        public float battleMemoryTime = 8f;
        [Tooltip("Suavizado de la posición al seguir la carta. Más alto = sigue más rápido pero tiembla más; más bajo = más suave pero con más retraso.")]
        public float positionSmoothing = 15f;
        [Tooltip("Igual que Position Smoothing pero para la rotación.")]
        public float rotationSmoothing = 15f;

        ARTrackedImageManager m_Manager;
        readonly Dictionary<TrackableId, ARTrackedImage> m_Images = new Dictionary<TrackableId, ARTrackedImage>();

        GameObject m_Map, m_Dragon, m_Battle;
        Pose m_MapPose, m_DragonPose;
        float m_MapLastSeen = float.NegativeInfinity;
        float m_DragonLastSeen = float.NegativeInfinity;

        public bool BattleActive { get; private set; }
        // Para depurar por qué no arranca la batalla (ver DebugHud) sin necesitar el log de Android.
        public bool MapVisible { get; private set; }
        public bool DragonVisible { get; private set; }
        public float CurrentDistance { get; private set; } = -1f;

        /// <summary>AudioSource del contenido activo ahora mismo (mapa, dragón o batalla), si hay alguno.</summary>
        public AudioSource ActiveAudioSource
        {
            get
            {
                if (m_Map && m_Map.activeSelf) return m_Map.GetComponent<AudioSource>();
                if (m_Dragon && m_Dragon.activeSelf) return m_Dragon.GetComponent<AudioSource>();
                if (m_Battle && m_Battle.activeSelf) return m_Battle.GetComponent<AudioSource>();
                return null;
            }
        }

        void Awake()
        {
            m_Manager = GetComponent<ARTrackedImageManager>();
            m_Map = Spawn(mapContentPrefab);
            m_Dragon = Spawn(dragonContentPrefab);
            m_Battle = Spawn(battleContentPrefab);
        }

        void OnEnable() => m_Manager.trackablesChanged.AddListener(OnTrackablesChanged);

        void OnDisable() => m_Manager.trackablesChanged.RemoveListener(OnTrackablesChanged);

        static GameObject Spawn(GameObject prefab)
        {
            if (!prefab)
                return null;

            var instance = Instantiate(prefab);
            instance.SetActive(false);
            return instance;
        }

        void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (var image in args.added)
                m_Images[image.trackableId] = image;

            foreach (var image in args.updated)
                m_Images[image.trackableId] = image;

            foreach (var pair in args.removed)
                m_Images.Remove(pair.Key);
        }

        void Update()
        {
            float now = Time.time;

            // Se lee la colección del manager en cada frame además de los eventos, para no depender de que llegue
            // cada "added/updated" (si alguno se pierde, la carta seguiría invisible para siempre).
            foreach (var image in m_Manager.trackables)
                m_Images[image.trackableId] = image;

            foreach (var image in m_Images.Values)
            {
                if (image == null)
                    continue;

                // Tracking = la cámara ve la carta ahora. Limited = ARCore conserva su última posición conocida.
                bool tracking = image.trackingState == TrackingState.Tracking;
                if (!tracking && image.trackingState != TrackingState.Limited)
                    continue;

                var pose = new Pose(image.transform.position, image.transform.rotation);
                var imageName = image.referenceImage.name;

                if (imageName == mapCardName)
                {
                    m_MapPose = pose;
                    if (tracking)
                        m_MapLastSeen = now;
                }
                else if (imageName == dragonCardName)
                {
                    m_DragonPose = pose;
                    if (tracking)
                        m_DragonLastSeen = now;
                }
            }

            bool mapVisible = now - m_MapLastSeen <= lostGraceTime;
            bool dragonVisible = now - m_DragonLastSeen <= lostGraceTime;
            MapVisible = mapVisible;
            DragonVisible = dragonVisible;

            // Para la batalla basta con haber visto cada carta hace poco: no hace falta que ARCore rastree las dos en el mismo frame.
            bool mapRemembered = now - m_MapLastSeen <= battleMemoryTime;
            bool dragonRemembered = now - m_DragonLastSeen <= battleMemoryTime;

            // Una vez iniciada, la batalla sigue mientras se recuerde la carta del dragón (la cámara suele enfocar solo esa);
            // termina si las cartas se separan (al volver a ver una carta en otro lugar, la distancia crece).
            bool hasBothPoses = dragonRemembered && (mapRemembered || BattleActive);
            if (hasBothPoses)
            {
                float distance = Vector3.Distance(m_MapPose.position, m_DragonPose.position);
                CurrentDistance = distance;
                if (!BattleActive && distance < battleEnterDistance)
                    BattleActive = true;
                else if (BattleActive && distance > battleExitDistance)
                    BattleActive = false;
            }
            else
            {
                BattleActive = false;
                CurrentDistance = -1f;
            }

            // Durante la batalla se oculta el mapa: mide ~20 cm y taparía la batalla en la carta de al lado.
            Show(m_Map, mapVisible && !BattleActive, m_MapPose);
            Show(m_Dragon, dragonVisible && !BattleActive, m_DragonPose);
            Show(m_Battle, BattleActive, m_DragonPose);
        }

        void Show(GameObject content, bool visible, Pose pose)
        {
            if (!content)
                return;

            if (!visible)
            {
                if (content.activeSelf)
                    content.SetActive(false);
                return;
            }

            if (!content.activeSelf)
            {
                // Recién aparece: coloca directo, sin suavizar desde donde estaba la última vez.
                content.transform.SetPositionAndRotation(pose.position, pose.rotation);
                content.SetActive(true);
                return;
            }

            // El tracking de ARCore tiembla un poco cuadro a cuadro; suaviza en vez de seguirlo en crudo.
            float posT = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            float rotT = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            content.transform.SetPositionAndRotation(
                Vector3.Lerp(content.transform.position, pose.position, posT),
                Quaternion.Slerp(content.transform.rotation, pose.rotation, rotT));
        }
    }
}
