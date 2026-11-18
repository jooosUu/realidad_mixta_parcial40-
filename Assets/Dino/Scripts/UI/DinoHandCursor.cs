using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DinoVehicle
{
    /// <summary>
    /// Puntero para los menús de DinoMenuSystem controlado con una mano (MediaPipe) o con el mando Arduino.
    /// - Mano: aparece con una mano visible; el pellizco pulgar+índice hace clic.
    /// - Mando: potenciómetro 1 = horizontal, potenciómetro 2 = vertical, botón 1 = clic.
    ///   Si se usan los dos, manda el que se tocó en los últimos segundos.
    /// - Pasar por encima de una opción la selecciona (OnPointerEnter).
    /// - Mantener el pellizco arrastra (deslizadores). El ratón y el teclado siguen funcionando igual.
    /// </summary>
    public class DinoHandCursor : MonoBehaviour
    {
        DinoMenuSystem menu;
        Canvas canvas;
        RectTransform puntero;
        Image anillo, centro;

        GameObject sobre;          // objeto bajo el puntero
        GameObject presionado;     // objeto al empezar el pellizco
        bool pinchAntes;
        Vector2 cursorMando = new Vector2(0.5f, 0.5f);
        PointerEventData evento;
        readonly List<RaycastResult> resultados = new();

        public static DinoHandCursor Crear(DinoMenuSystem menu)
        {
            var c = menu.gameObject.AddComponent<DinoHandCursor>();
            c.menu = menu;
            return c;
        }

        void Awake()
        {
            var go = new GameObject("Cursor de mano", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100; // por encima del menú (1000)

            var sprite = CrearCirculo(64);
            puntero = new GameObject("Puntero", typeof(RectTransform)).GetComponent<RectTransform>();
            puntero.SetParent(go.transform, false);
            puntero.anchorMin = puntero.anchorMax = Vector2.zero;
            puntero.sizeDelta = new Vector2(46, 46);

            anillo = puntero.gameObject.AddComponent<Image>();
            anillo.sprite = sprite;
            anillo.raycastTarget = false;

            var c = new GameObject("Centro", typeof(RectTransform)).GetComponent<RectTransform>();
            c.SetParent(puntero, false);
            c.sizeDelta = new Vector2(14, 14);
            centro = c.gameObject.AddComponent<Image>();
            centro.sprite = sprite;
            centro.raycastTarget = false;

            canvas.enabled = false;
        }

        void Update()
        {
            if (menu == null) menu = DinoMenuSystem.Instancia;
            bool manos = MediaPipeHandReceiver.HayCursor;
            bool mando = ArduinoSerialConnector.Conectado;
            // El mando manda si se tocó hace poco o si no hay mano visible.
            bool usarMando = mando && (!manos || Time.unscaledTime - ArduinoSerialConnector.UltimoUso < 4f);
            bool activo = menu != null && menu.Abierto && (manos || mando);
            if (!activo)
            {
                if (canvas.enabled) Soltar();
                canvas.enabled = false;
                pinchAntes = false;
                return;
            }

            canvas.enabled = true;
            // El script envía y de arriba hacia abajo; en pantalla Unity y crece hacia arriba.
            Vector2 c;
            if (usarMando)
            {
                // Potenciómetro 2 arriba = puntero arriba. Suavizado para que no tiemble.
                var destinoMando = new Vector2(ArduinoSerialConnector.Pot1, 1f - ArduinoSerialConnector.Pot2);
                cursorMando = Vector2.Lerp(cursorMando, destinoMando, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
                c = cursorMando;
            }
            else
            {
                c = MediaPipeHandReceiver.Cursor;
            }
            Vector2 pos = new Vector2(c.x * Screen.width, (1f - c.y) * Screen.height);
            puntero.position = pos;

            bool pinch = usarMando ? ArduinoSerialConnector.Boton1 : MediaPipeHandReceiver.Pinch;
            anillo.color = pinch ? new Color(0.85f, 0.15f, 0.1f, 0.95f) : new Color(1f, 0.95f, 0.85f, 0.85f);
            centro.color = pinch ? Color.white : new Color(0.85f, 0.15f, 0.1f, 0.9f);
            puntero.localScale = Vector3.one * (pinch ? 0.8f : 1f);

            var es = EventSystem.current;
            if (es == null) return;
            evento ??= new PointerEventData(es);
            evento.Reset();
            Vector2 delta = pos - evento.position;
            evento.position = pos;
            evento.delta = delta;
            evento.button = PointerEventData.InputButton.Left;

            resultados.Clear();
            es.RaycastAll(evento, resultados);
            GameObject objetivo = resultados.Count > 0 ? resultados[0].gameObject : null;
            if (resultados.Count > 0) evento.pointerCurrentRaycast = resultados[0];

            // Hover: entrar/salir como hace el ratón.
            if (objetivo != sobre)
            {
                if (sobre != null) ExecuteEvents.ExecuteHierarchy(sobre, evento, ExecuteEvents.pointerExitHandler);
                sobre = objetivo;
                if (sobre != null) ExecuteEvents.ExecuteHierarchy(sobre, evento, ExecuteEvents.pointerEnterHandler);
            }

            if (pinch && !pinchAntes)
            {
                // Pellizco = clic (al cerrar, para que se sienta inmediato).
                presionado = objetivo;
                if (presionado != null)
                {
                    evento.pointerPress = ExecuteEvents.ExecuteHierarchy(presionado, evento, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(presionado, evento, ExecuteEvents.pointerClickHandler);
                    evento.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(presionado);
                    if (evento.pointerDrag != null) ExecuteEvents.Execute(evento.pointerDrag, evento, ExecuteEvents.beginDragHandler);
                }
            }
            else if (pinch && evento.pointerDrag != null)
            {
                ExecuteEvents.Execute(evento.pointerDrag, evento, ExecuteEvents.dragHandler);
            }
            else if (!pinch && pinchAntes)
            {
                Soltar();
            }
            pinchAntes = pinch;
        }

        void Soltar()
        {
            if (evento == null) return;
            if (evento.pointerDrag != null) ExecuteEvents.Execute(evento.pointerDrag, evento, ExecuteEvents.endDragHandler);
            if (presionado != null) ExecuteEvents.ExecuteHierarchy(presionado, evento, ExecuteEvents.pointerUpHandler);
            evento.pointerDrag = null;
            evento.pointerPress = null;
            presionado = null;
        }

        static Sprite CrearCirculo(int tam)
        {
            var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = tam * 0.5f;
            var px = new Color32[tam * tam];
            for (int y = 0; y < tam; y++)
            {
                for (int x = 0; x < tam; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    // Disco con borde suave.
                    byte a = (byte)(Mathf.Clamp01(r - 1f - d + 1f) * 255f);
                    px[y * tam + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
