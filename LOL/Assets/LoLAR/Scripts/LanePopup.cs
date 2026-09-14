using System.Collections.Generic;
using UnityEngine;

namespace LoLAR
{
    /// <summary>
    /// Ícono flotante de un rol sobre el mapa. Al tocarlo abre/cierra su panel de información.
    /// Solo un panel puede estar abierto a la vez.
    /// </summary>
    public class LanePopup : MonoBehaviour
    {
        public Transform icon;
        public GameObject infoPanel;

        [Header("Animación del ícono")]
        public float bobAmplitude = 0.003f;
        public float bobSpeed = 2.2f;
        public float spinSpeed = 60f;

        static readonly List<LanePopup> s_Enabled = new List<LanePopup>();

        Vector3 m_IconBase;
        float m_Phase;

        public bool IsOpen => infoPanel && infoPanel.activeSelf;

        void Awake()
        {
            if (icon)
                m_IconBase = icon.localPosition;

            m_Phase = Random.value * Mathf.PI * 2f;
            SetOpen(false);
        }

        void OnEnable() => s_Enabled.Add(this);

        void OnDisable() => s_Enabled.Remove(this);

        void Update()
        {
            if (!icon)
                return;

            icon.localPosition = m_IconBase + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + m_Phase) * bobAmplitude);
            icon.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public void Toggle()
        {
            bool open = !IsOpen;
            foreach (var popup in s_Enabled)
            {
                if (popup != this)
                    popup.SetOpen(false);
            }
            SetOpen(open);
        }

        public void SetOpen(bool open)
        {
            if (infoPanel)
                infoPanel.SetActive(open);
        }

        /// <summary>Cierra cualquier panel abierto. Se usa al tocar fuera de todos los pop-ups.</summary>
        public static void CloseAll()
        {
            foreach (var popup in s_Enabled)
                popup.SetOpen(false);
        }
    }
}
