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
    /// - Ambas cartas a menos de <see cref="battleEnterDistance"/> -> batalla sobre la carta del dragón.
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

        ARTrackedImageManager m_Manager;
        readonly Dictionary<TrackableId, ARTrackedImage> m_Images = new Dictionary<TrackableId, ARTrackedImage>();

        GameObject m_Map, m_Dragon, m_Battle;
        Pose m_MapPose, m_DragonPose;
        float m_MapLastSeen = float.NegativeInfinity;
        float m_DragonLastSeen = float.NegativeInfinity;

        public bool BattleActive { get; private set; }

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

            foreach (var image in m_Images.Values)
            {
                if (image == null || image.trackingState != TrackingState.Tracking)
                    continue;

                var pose = new Pose(image.transform.position, image.transform.rotation);
                var imageName = image.referenceImage.name;

                if (imageName == mapCardName)
                {
                    m_MapPose = pose;
                    m_MapLastSeen = now;
                }
                else if (imageName == dragonCardName)
                {
                    m_DragonPose = pose;
                    m_DragonLastSeen = now;
                }
            }

            bool mapVisible = now - m_MapLastSeen <= lostGraceTime;
            bool dragonVisible = now - m_DragonLastSeen <= lostGraceTime;

            if (mapVisible && dragonVisible)
            {
                float distance = Vector3.Distance(m_MapPose.position, m_DragonPose.position);
                if (!BattleActive && distance < battleEnterDistance)
                    BattleActive = true;
                else if (BattleActive && distance > battleExitDistance)
                    BattleActive = false;
            }
            else
            {
                BattleActive = false;
            }

            Show(m_Map, mapVisible, m_MapPose);
            Show(m_Dragon, dragonVisible && !BattleActive, m_DragonPose);
            Show(m_Battle, dragonVisible && BattleActive, m_DragonPose);
        }

        static void Show(GameObject content, bool visible, Pose pose)
        {
            if (!content)
                return;

            // La pose se aplica antes de activar para que OnEnable ya vea la posición correcta.
            if (visible)
                content.transform.SetPositionAndRotation(pose.position, pose.rotation);

            if (content.activeSelf != visible)
                content.SetActive(visible);
        }
    }
}
