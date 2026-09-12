using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LoLAR
{
    /// <summary>
    /// Toque corto: abre el pop-up del rol tocado.
    /// Arrastre horizontal: rota el mapa activo.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TapInteractor : MonoBehaviour
    {
        public float dragThresholdPixels = 18f;
        public float maxRayDistance = 10f;
        public LayerMask raycastMask = ~0;

        Camera m_Camera;
        bool m_Pressed;
        bool m_Dragging;
        Vector2 m_StartPosition;
        Vector2 m_LastPosition;

        void Awake() => m_Camera = GetComponent<Camera>();

        void Update()
        {
            if (!TryReadPointer(out bool down, out Vector2 position))
                return;

            if (down && !m_Pressed)
            {
                m_Pressed = true;
                m_Dragging = false;
                m_StartPosition = m_LastPosition = position;
            }
            else if (down)
            {
                if (!m_Dragging && (position - m_StartPosition).magnitude > dragThresholdPixels)
                    m_Dragging = true;

                if (m_Dragging && MapRotator.Active)
                    MapRotator.Active.RotateBy(position.x - m_LastPosition.x);

                m_LastPosition = position;
            }
            else if (m_Pressed)
            {
                m_Pressed = false;
                if (!m_Dragging)
                    Tap(m_LastPosition);
            }
        }

        void Tap(Vector2 screenPosition)
        {
            var ray = m_Camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, maxRayDistance, raycastMask))
                return;

            var popup = hit.collider.GetComponentInParent<LanePopup>();
            if (popup)
                popup.Toggle();
        }

        static bool TryReadPointer(out bool down, out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer == null)
            {
                down = false;
                position = default;
                return false;
            }
            down = pointer.press.isPressed;
            position = pointer.position.ReadValue();
            return true;
#else
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                down = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                position = touch.position;
                return true;
            }
            down = Input.GetMouseButton(0);
            position = Input.mousePosition;
            return true;
#endif
        }
    }
}
