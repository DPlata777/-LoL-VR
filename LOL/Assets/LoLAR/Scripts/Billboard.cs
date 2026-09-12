using UnityEngine;

namespace LoLAR
{
    /// <summary>
    /// Mantiene un Canvas en World Space siempre mirando a la cámara.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        Camera m_Camera;

        void LateUpdate()
        {
            if (!m_Camera)
                m_Camera = Camera.main;
            if (!m_Camera)
                return;

            var direction = transform.position - m_Camera.transform.position;
            if (direction.sqrMagnitude < 1e-6f)
                return;

            transform.rotation = Quaternion.LookRotation(direction, m_Camera.transform.up);
        }
    }
}
