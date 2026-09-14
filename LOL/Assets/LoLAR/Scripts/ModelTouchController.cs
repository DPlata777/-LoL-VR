using UnityEngine;

namespace LoLAR
{
    /// <summary>
    /// Deja rotar y hacer zoom al contenido activo (mapa o dragón solo) sin moverlo de la carta:
    /// arrastrar con 1 dedo gira (horizontal = giro sobre el eje vertical, vertical = inclinación,
    /// juntos cubren los 360°), y pellizcar con 2 dedos acerca o aleja. Antes había que mover el
    /// celular alrededor de la carta para ver otros ángulos, y eso hacía que ARCore la perdiera.
    /// </summary>
    public class ModelTouchController : MonoBehaviour
    {
        public float degreesPerPixel = 0.35f;
        [Tooltip("Inclinación máxima hacia arriba/abajo, en grados, para no voltear el modelo boca abajo.")]
        public float maxTilt = 85f;
        [Tooltip("Límites de zoom, como múltiplo del tamaño original.")]
        public Vector2 zoomRange = new Vector2(0.6f, 2.2f);

        public static ModelTouchController Active { get; private set; }

        Quaternion m_BaseRotation;
        Vector3 m_BaseScale;
        float m_Yaw, m_Pitch, m_Zoom = 1f;

        void Awake()
        {
            m_BaseRotation = transform.localRotation;
            m_BaseScale = transform.localScale;
        }

        void OnEnable()
        {
            Active = this;
            m_Yaw = m_Pitch = 0f;
            m_Zoom = 1f;
            transform.localRotation = m_BaseRotation;
            transform.localScale = m_BaseScale;
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        /// <summary>Arrastre de 1 dedo: x gira, y inclina.</summary>
        public void RotateBy(Vector2 pixelDelta)
        {
            m_Yaw += pixelDelta.x * degreesPerPixel;
            m_Pitch = Mathf.Clamp(m_Pitch - pixelDelta.y * degreesPerPixel, -maxTilt, maxTilt);
            transform.localRotation = m_BaseRotation * Quaternion.Euler(m_Pitch, m_Yaw, 0f);
        }

        /// <summary>Pellizco de 2 dedos: factor > 1 aleja los dedos (acerca la cámara al modelo).</summary>
        public void ZoomBy(float factor)
        {
            m_Zoom = Mathf.Clamp(m_Zoom * factor, zoomRange.x, zoomRange.y);
            transform.localScale = m_BaseScale * m_Zoom;
        }
    }
}
