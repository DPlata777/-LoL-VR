using UnityEngine;

namespace LoLAR
{
    /// <summary>
    /// Reproduce en bucle la música de fondo del contenido al que está enganchado.
    /// Como el mapa, el dragón y la batalla se activan/desactivan según la carta detectada
    /// (ver <see cref="CardTrackingController"/>), esto basta para que cada uno suene solo
    /// mientras está a la vista.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AutoLoopMusic : MonoBehaviour
    {
        AudioSource m_Source;

        void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_Source.loop = true;
            m_Source.playOnAwake = false;
        }

        void OnEnable()
        {
            if (m_Source.clip)
                m_Source.Play();
        }

        void OnDisable() => m_Source.Stop();
    }
}
