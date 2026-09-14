using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LoLAR
{
    /// <summary>
    /// Toque corto: abre el pop-up del rol tocado.
    /// Arrastre con 1 dedo: rota el contenido activo (horizontal gira, vertical inclina).
    /// Pellizco con 2 dedos: hace zoom al contenido activo.
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
        float m_PinchDistance = -1f;

        static readonly Vector2[] s_Pointers = new Vector2[2];

        void Awake() => m_Camera = GetComponent<Camera>();

        void Update()
        {
            int count = ReadPointers(s_Pointers);

            if (count >= 2)
            {
                m_Pressed = false;
                HandlePinch(s_Pointers[0], s_Pointers[1]);
                return;
            }

            m_PinchDistance = -1f;

            if (count == 1)
            {
                HandleSingleTouch(true, s_Pointers[0]);
            }
            else if (m_Pressed)
            {
                HandleSingleTouch(false, m_LastPosition);
            }
        }

        void HandleSingleTouch(bool down, Vector2 position)
        {
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

                if (m_Dragging && ModelTouchController.Active)
                    ModelTouchController.Active.RotateBy(position - m_LastPosition);

                m_LastPosition = position;
            }
            else if (m_Pressed)
            {
                m_Pressed = false;
                if (!m_Dragging)
                    Tap(m_LastPosition);
            }
        }

        void HandlePinch(Vector2 a, Vector2 b)
        {
            float distance = Vector2.Distance(a, b);
            if (m_PinchDistance > 0f && ModelTouchController.Active)
                ModelTouchController.Active.ZoomBy(distance / m_PinchDistance);
            m_PinchDistance = distance;
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

        /// <summary>Llena <paramref name="buffer"/> (tamaño 2) con los punteros presionados y devuelve cuántos hay.</summary>
        static int ReadPointers(Vector2[] buffer)
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                int count = 0;
                foreach (var touch in touchscreen.touches)
                {
                    if (count >= buffer.Length)
                        break;
                    if (!touch.press.isPressed)
                        continue;
                    buffer[count++] = touch.position.ReadValue();
                }
                if (count > 0)
                    return count;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                buffer[0] = mouse.position.ReadValue();
                return 1;
            }
            return 0;
#else
            int touchCount = Mathf.Min(Input.touchCount, buffer.Length);
            int valid = 0;
            for (int i = 0; i < touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    continue;
                buffer[valid++] = touch.position;
            }
            if (valid > 0)
                return valid;

            if (Input.GetMouseButton(0))
            {
                buffer[0] = Input.mousePosition;
                return 1;
            }
            return 0;
#endif
        }
    }
}
