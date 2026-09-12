using UnityEngine;

namespace LoLAR
{
    /// <summary>
    /// Gira el mapa sobre su eje vertical al deslizar el dedo, sin moverlo de la carta.
    /// </summary>
    public class MapRotator : MonoBehaviour
    {
        public float degreesPerPixel = 0.35f;

        public static MapRotator Active { get; private set; }

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public void RotateBy(float pixels)
        {
            transform.Rotate(0f, -pixels * degreesPerPixel, 0f, Space.Self);
        }
    }
}
