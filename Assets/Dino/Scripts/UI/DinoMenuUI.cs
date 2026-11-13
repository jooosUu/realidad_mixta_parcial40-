using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DinoVehicle
{
    /// <summary>Paleta, fuentes, texturas y fábrica de elementos UGUI para los menús de DINO WEST.</summary>
    public static class DinoMenuUI
    {
        public static readonly Color Hueso = Hex("#F1E7D3");
        public static readonly Color Arena = Hex("#D8C3A0");
        public static readonly Color Gris = Hex("#8A7F73");
        public static readonly Color Sangre = Hex("#B3201A");
        public static readonly Color Apagado = Hex("#4A3E36");
        public static readonly Color Riel = Hex("#5A4A40");
        public static readonly Color Linea = Hex("#3A2E28");

        public const float W = 1920f, H = 1080f;

        static TMP_FontAsset bold, semi, light;
        public static TMP_FontAsset Bold => bold ??= Fuente("Bahnschrift-BoldCondensed SDF");
        public static TMP_FontAsset Semi => semi ??= Fuente("Bahnschrift-SemiBoldCondensed SDF");
        public static TMP_FontAsset Fina => light ??= Fuente("Bahnschrift-LightCondensed SDF");

        static TMP_FontAsset Fuente(string n)
        {
            var f = Resources.Load<TMP_FontAsset>("DinoUI/" + n);
            if (f == null)
            {
                Debug.LogWarning($"[DinoMenu] Falta la fuente {n}. Ejecuta DINO WEST ▸ Preparar menús.");
                f = TMP_Settings.defaultFontAsset;
            }
            return f;
        }

        public static Sprite Sprite(string n) => Resources.Load<Sprite>("DinoUI/" + n);

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        public static Color Alfa(Color c, float a) { c.a = a; return c; }

        /// <summary>Ancho aproximado de un texto en Bahnschrift Condensed (mayúsculas) con su tracking.</summary>
        public static float AnchoTexto(string s, float size, float tracking) => s.Length * size * (0.40f + tracking / 1000f);

        // ---------- fábrica ----------
        /// <summary>Rect anclado arriba-izquierda con coordenadas de Illustrator (y hacia abajo).</summary>
        public static RectTransform Caja(Transform padre, string nombre, float x, float y, float w, float h)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Rect anclado abajo-derecha (para la barra de teclas).</summary>
        public static RectTransform CajaAbajoDerecha(Transform padre, string nombre, float derecha, float abajo, float w, float h)
        {
            var rt = Caja(padre, nombre, 0, 0, w, h);
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-derecha, abajo);
            return rt;
        }

        public static RectTransform Estirar(Transform padre, string nombre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Imagen(RectTransform rt, Sprite s, Color c, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Rect(Transform padre, string nombre, float x, float y, float w, float h, Color c)
            => Imagen(Caja(padre, nombre, x, y, w, h), null, c);

        public static TextMeshProUGUI Texto(Transform padre, string nombre, float x, float y, float w, float h,
            string s, TMP_FontAsset f, float size, Color c, float tracking = 0,
            TextAlignmentOptions al = TextAlignmentOptions.TopLeft, bool raycast = false)
        {
            var rt = Caja(padre, nombre, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = f;
            t.fontSize = size;
            t.color = c;
            t.characterSpacing = tracking / 10f;   // tracking de Illustrator (1/1000 em) → TMP (1/100 em)
            t.alignment = al;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = raycast;
            t.text = s;
            return t;
        }

        /// <summary>Tecla con borde + etiqueta. Devuelve el ancho total ocupado.</summary>
        public static float Tecla(Transform padre, float x, float y, string tecla, string etiqueta, float size = 19)
        {
            float w = Mathf.Max(40, 16 + tecla.Length * size * 0.55f);
            float h = size + 12;
            var c = Hex("#D8C3A0");
            Rect(padre, "Tecla " + tecla + " ·arriba", x, y, w, 1.5f, c);
            Rect(padre, "Tecla " + tecla + " ·abajo", x, y + h - 1.5f, w, 1.5f, c);
            Rect(padre, "Tecla " + tecla + " ·izq", x, y, 1.5f, h, c);
            Rect(padre, "Tecla " + tecla + " ·der", x + w - 1.5f, y, 1.5f, h, c);
            Texto(padre, "Tecla " + tecla, x, y + 6, w, size + 6, tecla, Bold, size * 0.85f, Hueso, 0, TextAlignmentOptions.Top);
            float total = w;
            if (!string.IsNullOrEmpty(etiqueta))
            {
                Texto(padre, "Etiqueta " + etiqueta, x + w + 12, y + 6, 320, size + 6, etiqueta, Semi, size * 0.9f, Gris, 250);
                total += 12 + AnchoTexto(etiqueta, size * 0.9f, 250);
            }
            return total;
        }

        /// <summary>Barra de teclas abajo a la derecha.</summary>
        public static RectTransform BarraTeclas(Transform padre, params (string tecla, string etiqueta)[] items)
        {
            var barra = CajaAbajoDerecha(padre, "Barra de teclas", 80, 43, 1100, 32);
            // se arma de izquierda a derecha y luego se alinea a la derecha
            float x = 0;
            foreach (var (t, e) in items)
                x += Tecla(barra, x, 0, t, e) + 50;
            barra.sizeDelta = new Vector2(x - 50, 32);
            return barra;
        }
    }

    /// <summary>Recibe hover / clic / rueda del ratón y los pasa al menú.</summary>
    public class DinoMenuPuntero : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IScrollHandler
    {
        public Action Entra;
        public Action<PointerEventData> Clic;
        public Action<float> Rueda;

        public void OnPointerEnter(PointerEventData e) => Entra?.Invoke();
        public void OnPointerClick(PointerEventData e) => Clic?.Invoke(e);
        public void OnScroll(PointerEventData e) => Rueda?.Invoke(e.scrollDelta.y);
    }
}
