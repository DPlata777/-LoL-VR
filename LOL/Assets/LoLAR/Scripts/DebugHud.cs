using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

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
        ARTrackedImageManager m_ImageManager;
        readonly StringBuilder m_Builder = new StringBuilder();

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
            rect.sizeDelta = new Vector2(1000f, 700f);

            m_Text = textGo.GetComponent<Text>();
            m_Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_Text.fontSize = 28;
            m_Text.color = Color.white;
            m_Text.alignment = TextAnchor.UpperLeft;

            // Sombra para que se lea sobre fondos claros (como una mesa blanca).
            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        void Update()
        {
            if (!controller)
                return;

            if (!m_ImageManager)
                m_ImageManager = controller.GetComponent<ARTrackedImageManager>();

            string distance = controller.CurrentDistance >= 0f ? $"{controller.CurrentDistance:0.000} m" : "-";
            var audio = controller.ActiveAudioSource;
            string audioInfo = audio
                ? $"{audio.gameObject.name}: clip={(audio.clip ? audio.clip.name : "SIN CLIP")} sonando={audio.isPlaying} vol={audio.volume:0.0}"
                : "sin contenido activo";

            var sb = m_Builder;
            sb.Clear();
            sb.Append("Mapa visible: ").Append(controller.MapVisible).Append('\n');
            sb.Append("Dragón visible: ").Append(controller.DragonVisible).Append('\n');
            sb.Append("Distancia entre cartas: ").Append(distance).Append('\n');
            sb.Append("Batalla activa: ").Append(controller.BattleActive).Append('\n');
            sb.Append("Toque: ").Append(TapInteractor.DebugInfo).Append('\n');
            sb.Append("Audio: ").Append(audioInfo).Append('\n');

            // Diagnóstico de ARCore: dónde se corta la detección de las cartas.
            sb.Append("\nSesión AR: ").Append(ARSession.state)
              .Append(" (motivo: ").Append(ARSession.notTrackingReason).Append(")\n");

            if (!m_ImageManager)
            {
                sb.Append("Image Manager: NO ENCONTRADO\n");
            }
            else
            {
                var subsystem = m_ImageManager.subsystem;
                sb.Append("Image Manager: activo=").Append(m_ImageManager.enabled)
                  .Append(" subsistema=").Append(subsystem == null ? "NULO" : subsystem.running ? "corriendo" : "detenido")
                  .Append('\n');

                var library = m_ImageManager.referenceLibrary;
                if (library == null)
                {
                    sb.Append("Librería: NULA\n");
                }
                else
                {
                    sb.Append("Librería: ").Append(library.count).Append(" imágenes (");
                    for (int i = 0; i < library.count; i++)
                    {
                        var reference = library[i];
                        if (i > 0) sb.Append(", ");
                        sb.Append(reference.name).Append(' ')
                          .Append($"{reference.size.x * 100f:0.0}cm");
                    }
                    sb.Append(")\n");
                }

                int detected = 0;
                foreach (var image in m_ImageManager.trackables)
                {
                    detected++;
                    var reference = image.referenceImage;
                    sb.Append("  Detectada: ").Append(string.IsNullOrEmpty(reference.name) ? "(sin nombre)" : reference.name)
                      .Append(" estado=").Append(image.trackingState)
                      .Append(" tamaño=").Append($"{image.size.x * 100f:0.0}x{image.size.y * 100f:0.0}cm")
                      .Append('\n');
                }
                sb.Append("Imágenes detectadas: ").Append(detected).Append('\n');
            }

            m_Text.text = sb.ToString();
        }
    }
}
