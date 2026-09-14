using UnityEngine;
using UnityEngine.UI;

namespace LoLAR
{
    /// <summary>
    /// Texto en pantalla con el estado del tracking (mapa/dragón visibles, distancia entre las cartas,
    /// batalla activa), para ver en el momento por qué no arranca la batalla sin depender del log de
    /// Android. Se puede desactivar poniendo LoLARBuilder.ShowDebugHud en false y regenerando.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        public CardTrackingController controller;
        Text m_Text;

        void Awake()
        {
            var canvasGo = new GameObject("DebugCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(700f, 200f);

            m_Text = textGo.GetComponent<Text>();
            m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_Text.fontSize = 28;
            m_Text.color = Color.white;
            m_Text.alignment = TextAnchor.UpperLeft;
        }

        void Update()
        {
            if (!controller)
                return;

            string distance = controller.CurrentDistance >= 0f ? $"{controller.CurrentDistance:0.000} m" : "-";
            m_Text.text = $"Mapa visible: {controller.MapVisible}\n" +
                          $"Dragón visible: {controller.DragonVisible}\n" +
                          $"Distancia entre cartas: {distance}\n" +
                          $"Batalla activa: {controller.BattleActive}";
        }
    }
}
