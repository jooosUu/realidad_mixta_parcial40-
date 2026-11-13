using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static DinoVehicle.DinoMenuUI;

namespace DinoVehicle
{
    /// <summary>
    /// HUD en juego con el estilo de los menús: velocímetro, rugido (turbo), energía y estado del Arduino.
    /// Reemplaza las cajas de OnGUI de DinoHUD (que sigue manejando la tecla R).
    /// </summary>
    public class DinoHUDView : MonoBehaviour
    {
        /// <summary>Energía del dino 0-1. La puede escribir el sistema de comida cuando exista.</summary>
        public static float Energia = 1f;

        const float RIEL = 420f;

        Canvas canvas;
        TextMeshProUGUI velocidad, estadoRugido, energiaTxt, arduinoTxt;
        Image relleno, zonaTurbo, marcaMax, rugido, energia, puntoArduino;
        float inicioTurbo = -999f;
        bool turboAntes;

        public static DinoHUDView Crear(Transform padre)
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var h = go.AddComponent<DinoHUDView>();
            h.Construir();
            return h;
        }

        void Construir()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var esc = gameObject.AddComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(W, H);
            esc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var raiz = transform;

            // sombra para leer abajo a la izquierda
            var sombra = Imagen(Caja(raiz, "Sombra", 0, 0, 1300, 620), Sprite("sombra_hud"), Color.white);
            Abajo(sombra.rectTransform, 0, 0);

            var bloque = Caja(raiz, "Velocímetro", 0, 0, 700, 300);
            Abajo(bloque, 0, 0);
            // coordenadas dentro del bloque (y hacia abajo desde arriba del bloque; bloque = últimos 300 px)
            velocidad = Texto(bloque, "Velocidad", 80, 20, 300, 150, "0", Bold, 132, Hueso, 20);
            Texto(bloque, "KM/H", 206, 108, 200, 30, "KM/H", Bold, 24, Arena, 400);
            Rect(bloque, "Riel", 84, 172, RIEL, 2, Linea);
            zonaTurbo = Rect(bloque, "Zona turbo", 84, 172, RIEL, 2, Sangre);
            relleno = Rect(bloque, "Velocidad actual", 84, 171, 0, 4, Hueso);
            marcaMax = Rect(bloque, "Velocidad máxima", 84, 164, 2, 18, Gris);

            Texto(bloque, "Rugido", 84, 198, 120, 24, "RUGIDO", Semi, 17, Gris, 400);
            rugido = Imagen(Caja(bloque, "Rugido · pincel", 190, 194, 320, 30), Sprite("sel_fila"), Color.white);
            rugido.type = Image.Type.Filled;
            rugido.fillMethod = Image.FillMethod.Horizontal;
            estadoRugido = Texto(bloque, "Estado rugido", 520, 198, 160, 24, "LISTO", Bold, 18, Hueso, 300);

            Texto(bloque, "Energía", 84, 238, 120, 24, "ENERGÍA", Semi, 17, Gris, 400);
            Rect(bloque, "Riel energía", 200, 249, 300, 2, Linea);
            energia = Rect(bloque, "Energía", 200, 248, 300, 4, Arena);
            energiaTxt = Texto(bloque, "Energía %", 520, 238, 120, 24, "100 %", Bold, 18, Hueso, 100);

            puntoArduino = Rect(raiz, "Punto Arduino", 80, 60, 10, 10, Gris);
            arduinoTxt = Texto(raiz, "Arduino", 100, 52, 700, 26, "", Semi, 17, Gris, 300);

            BarraTeclas(raiz, ("ESC", "PAUSA"));
            Imagen(Estirar(raiz, "Grano"), Sprite("grano"), Alfa(Color.white, 0.35f));
            canvas.enabled = false;
        }

        static void Abajo(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
        }

        public void Mostrar(bool si)
        {
            if (canvas == null) return;
            canvas.enabled = si;
            DinoHUD.UsarHUDClasico = false;
        }

        void Update()
        {
            if (!canvas.enabled) return;
            var v = FindAnyObjectByType<DinoVehicleController>();
            if (v == null) return;

            float vmax = v.maxForwardSpeed * 3.6f;
            float vtope = vmax * v.boostMultiplier;
            float kmh = Mathf.Abs(v.CurrentSpeedKmh);
            velocidad.text = Mathf.RoundToInt(kmh).ToString();
            relleno.rectTransform.sizeDelta = new Vector2(RIEL * Mathf.Clamp01(kmh / vtope), 4);
            relleno.color = kmh > vmax + 0.5f ? Sangre : Hueso;
            float xMax = 84 + RIEL * vmax / vtope;
            zonaTurbo.rectTransform.anchoredPosition = new Vector2(xMax, -172);
            zonaTurbo.rectTransform.sizeDelta = new Vector2(RIEL * (1 - vmax / vtope), 2);
            marcaMax.rectTransform.anchoredPosition = new Vector2(xMax, -164);

            // rugido / turbo: activo → recarga → listo
            if (v.IsBoosting && !turboAntes) inicioTurbo = Time.time;
            turboAntes = v.IsBoosting;
            float desde = Time.time - inicioTurbo;
            if (v.IsBoosting)
            {
                rugido.fillAmount = 1f - Mathf.Clamp01(desde / v.boostDuration);
                estadoRugido.text = "TURBO";
                estadoRugido.color = Sangre;
            }
            else
            {
                float recarga = Mathf.Clamp01((desde - v.boostDuration) / Mathf.Max(0.01f, v.boostCooldown));
                bool listo = inicioTurbo < 0 || recarga >= 1f;
                rugido.fillAmount = listo ? 1f : recarga;
                rugido.color = Alfa(Color.white, listo ? 1f : 0.45f);
                estadoRugido.text = listo ? "LISTO" : "RECARGA";
                estadoRugido.color = listo ? Hueso : Gris;
            }

            energia.rectTransform.sizeDelta = new Vector2(300 * Mathf.Clamp01(Energia), 4);
            energiaTxt.text = $"{Energia * 100:0} %";

            var ard = FindAnyObjectByType<ArduinoSerialConnector>();
            bool con = ard != null && ard.IsConnected;
            arduinoTxt.text = con ? $"ARDUINO CONECTADO · {ard.portName}" : "ARDUINO DESCONECTADO · TECLADO";
            puntoArduino.color = con ? Sangre : Gris;
        }
    }
}
