using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static DinoVehicle.DinoMenuUI;

namespace DinoVehicle
{
    /// <summary>
    /// Menú principal, pausa, ajustes, bitácora, escenarios y créditos de DINO WEST.
    /// Se crea solo al iniciar el juego (no hay que tocar ninguna escena) y sobrevive a los cambios de escena.
    /// El fondo es el último cuadro del juego congelado, pasado a sepia por el shader DinoWest/PauseBackdrop.
    /// Teclas: ESC pausa/atrás · ↑↓ / W S mover · ENTER / ESPACIO elegir · ←→ / A D ajustar · Q E pestañas · R restablecer.
    /// </summary>
    public class DinoMenuSystem : MonoBehaviour
    {
        public enum Estado { Jugando, Titulo, Pausa, Ajustes, Bitacora, Creditos }

        public static DinoMenuSystem Instancia { get; private set; }
        public Estado Actual { get; private set; } = Estado.Jugando;
        public bool Abierto => Actual != Estado.Jugando;

        static bool mostrarTituloAlCargar = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (Instancia != null) return;
            var go = new GameObject("DINO WEST · Menús");
            DontDestroyOnLoad(go);
            go.AddComponent<DinoMenuSystem>();
        }

        // ---------- referencias ----------
        Canvas canvas;
        RectTransform raiz;
        RawImage fondo;
        Material fondoMat;
        Texture2D captura;
        Image sombraIzq, sombraAjustes, zarpazo;
        readonly Dictionary<Estado, RectTransform> pantallas = new();
        readonly Stack<Estado> historial = new();

        ListaMenu listaTitulo, listaPausa;
        DinoEscenarioInicio escenario;
        DinoHUDView hud;

        // ---- inicio: monturas y escenarios ----
        class Montura { public string Nombre; public bool Disponible; public Montura(string n, bool d) { Nombre = n; Disponible = d; } }
        static readonly Montura[] monturas =
        {
            new Montura("Carnotauro", true),
            new Montura("Velociraptor", true),
            new Montura("Paquicefalo", true),
            new Montura("Estegosaurio", true),
            new Montura("???", false),
        };
        int monturaSel;
        int escenarioSel;
        TextMeshProUGUI txtNombreDino, txtEscenario;
        readonly List<(Image relleno, TextMeshProUGUI valor)> statsUI = new();
        readonly List<(Image[] borde, TextMeshProUGUI nombre, Image pincel)> tarjetas = new();
        PanelFilas panelAjustes, panelBitacora, panelCreditos;

        // lo que se apaga mientras el menú está abierto
        readonly List<Behaviour> apagados = new();
        bool congelado, capturando;

        void Awake()
        {
            Instancia = this;
            SimulationSettings.Cargar();
            Construir();
            SceneManager.sceneLoaded += AlCargarEscena;
            canvas.enabled = false;
        }

        void OnDestroy() => SceneManager.sceneLoaded -= AlCargarEscena;

        void Start()
        {
            SimulationSettings.AplicarMundo();
            if (mostrarTituloAlCargar) StartCoroutine(AbrirInicio(2));
            else hud.Mostrar(true);
        }

        void AlCargarEscena(Scene s, LoadSceneMode m)
        {
            apagados.Clear();
            congelado = false;
            SimulationSettings.AplicarMundo();
            AsegurarEventSystem();
            escenario = null;
            if (mostrarTituloAlCargar) StartCoroutine(AbrirInicio(2));
            else { Cerrar(); }
        }

        // =====================================================================================
        // Construcción
        void Construir()
        {
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var esc = cgo.AddComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(W, H);
            esc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            cgo.AddComponent<GraphicRaycaster>();
            raiz = (RectTransform)cgo.transform;
            AsegurarEventSystem();

            // fondo: juego congelado
            var frt = Estirar(raiz, "Juego congelado");
            fondo = frt.gameObject.AddComponent<RawImage>();
            fondo.raycastTarget = true;   // bloquea clics al juego
            var sh = Resources.Load<Shader>("DinoUI/PauseBackdrop");
            if (sh != null) { fondoMat = new Material(sh); fondo.material = fondoMat; }
            else Debug.LogWarning("[DinoMenu] No se encontró el shader DinoWest/PauseBackdrop.");

            zarpazo = Imagen(Caja(raiz, "Zarpazo", 0, 120, 620, 800), Sprite("zarpazo_rojo"), Alfa(Color.white, 0.55f));
            var zrt = zarpazo.rectTransform;
            zrt.anchorMin = zrt.anchorMax = new Vector2(1, 1);
            zrt.pivot = new Vector2(1, 1);
            zrt.anchoredPosition = new Vector2(-150, -120);
            sombraIzq = Imagen(Estirar(raiz, "Oscuridad lateral"), Sprite("sombra_izq"), Color.white);
            sombraAjustes = Imagen(Estirar(raiz, "Oscuridad ajustes"), Sprite("sombra_ajustes"), Alfa(Color.white, 0.7f));

            ConstruirTitulo();
            ConstruirPausa();
            ConstruirAjustes();
            ConstruirBitacora();
            ConstruirCreditos();

            var grano = Imagen(Estirar(raiz, "Grano de película"), Sprite("grano"), Color.white);
            grano.transform.SetAsLastSibling();

            hud = DinoHUDView.Crear(transform);
        }

        static void AsegurarEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        RectTransform NuevaPantalla(Estado e)
        {
            var rt = Estirar(raiz, "Pantalla · " + e);
            pantallas[e] = rt;
            rt.gameObject.SetActive(false);
            return rt;
        }

        static void Encabezado(Transform p, string s, float y)
        {
            Texto(p, "Encabezado", 130, y, 600, 34, s, Bold, 26, Sangre, 900);
            Rect(p, "Filete", 130, y + 45, 60, 2, Sangre);
        }

        void ConstruirTitulo()
        {
            var p = NuevaPantalla(Estado.Titulo);
            var logo = Imagen(Caja(p, "Logo DINO WEST", 110, 80, 330, 330 * 887f / 1490f), Sprite("logo_dw_hueso"), Color.white);
            logo.preserveAspect = true;
            listaTitulo = new ListaMenu(p, 130, 400, 72, new[]
            {
                new ItemMenu("Montar", Montar),
                new ItemMenu("Escenario", () => CambiarEscenario(1)),
                new ItemMenu("Ajustes", () => { panelAjustes.Pestana(0); Ir(Estado.Ajustes); }),
                new ItemMenu("Créditos", () => Ir(Estado.Creditos)),
                new ItemMenu("Salir", Salir),
            });
            txtEscenario = Texto(p, "Escenario elegido", 420, 478, 400, 40, "", Semi, 22, Gris, 200, TextAlignmentOptions.TopLeft, true);
            txtEscenario.gameObject.AddComponent<DinoMenuPuntero>().Clic = _ => CambiarEscenario(1);
            escenarioSel = Mathf.Max(0, SceneManager.GetActiveScene().buildIndex);

            // ficha del dino (derecha)
            Texto(p, "Elige tu montura", 1000, 70, 820, 34, "ELIGE TU MONTURA", Bold, 22, Sangre, 900, TextAlignmentOptions.TopRight);
            Rect(p, "Filete", 1760, 108, 60, 2, Sangre);
            txtNombreDino = Texto(p, "Nombre del dino", 900, 120, 920, 120, "", Bold, 104, Hueso, 120, TextAlignmentOptions.TopRight);
            string[] etiquetas = { "VELOCIDAD", "TURBO", "FUERZA", "GIRO", "SALTO" };
            for (int i = 0; i < etiquetas.Length; i++)
            {
                float yy = 290 + i * 46;
                Texto(p, "Stat " + etiquetas[i], 1540, yy - 4, 200, 26, etiquetas[i], Semi, 17, Gris, 400);
                Rect(p, "Riel", 1540, yy + 26, 180, 2, Linea);
                var rel = Rect(p, "Valor", 1540, yy + 25, 180, 4, Hueso);
                var val = Texto(p, "Valor " + etiquetas[i], 1700, yy - 4, 120, 26, "", Bold, 19, Hueso, 60, TextAlignmentOptions.TopRight);
                statsUI.Add((rel, val));
            }

            // tarjetas de monturas (abajo)
            const float cy = 880, cw = 140, ch = 84, gap = 14, x0 = 650;
            for (int i = 0; i < monturas.Length; i++)
            {
                float x = x0 + i * (cw + gap);
                var fondoT = Rect(p, "Tarjeta " + i, x, cy, cw, ch, Alfa(Hex("#100B09"), 0.88f));
                fondoT.raycastTarget = true;
                int idx = i;
                fondoT.gameObject.AddComponent<DinoMenuPuntero>().Clic = _ => ElegirMontura(idx);
                if (monturas[i].Disponible)
                    Imagen(Caja(p, "Miniatura", x, cy, cw, ch), Sprite("carno_ficha"), Color.white);
                else
                    Texto(p, "?", x, cy + 8, cw, 64, "?", Bold, 50, Linea, 0, TextAlignmentOptions.Top);
                var borde = new[]
                {
                    Rect(p, "Borde", x, cy, cw, 2, Linea), Rect(p, "Borde", x, cy + ch - 2, cw, 2, Linea),
                    Rect(p, "Borde", x, cy, 2, ch, Linea), Rect(p, "Borde", x + cw - 2, cy, 2, ch, Linea),
                };
                var pincel = Imagen(Caja(p, "Pincel", x - 25, cy + ch + 2, cw + 50, 38), Sprite("sel_0"), Color.white);
                bool ok = monturas[i].Disponible;
                var nom = Texto(p, "Nombre", x, cy + ch + 9, cw, 26, ok ? monturas[i].Nombre.ToUpperInvariant() : "BLOQUEADO",
                    ok ? Bold : Semi, ok ? 18 : 15, Hueso, ok ? 200 : 300, TextAlignmentOptions.Top);
                tarjetas.Add((borde, nom, pincel));
            }
            Tecla(p, x0 - 60, cy + 28, "Q", null);
            Tecla(p, x0 + monturas.Length * (cw + gap) - gap + 18, cy + 28, "E", null);
            BarraTeclas(p, ("Q / E", "CAMBIAR DINO"), ("ENTER", "SELECCIONAR"));
        }

        void ElegirMontura(int i)
        {
            monturaSel = ((i % monturas.Length) + monturas.Length) % monturas.Length;
            DinoMountManager.CurrentMountIndex = monturaSel;
            var m = monturas[monturaSel];
            var def = DinoMountManager.Mounts[monturaSel];

            txtNombreDino.text = m.Disponible ? m.Nombre.ToUpperInvariant() : "BLOQUEADO";
            txtNombreDino.color = m.Disponible ? Hueso : Apagado;

            float vel = def.MaxSpeedKmh;
            float turbo = def.TurboMultiplier;
            float masa = def.MassKg;
            float giro = def.TurnSpeed;
            float salto = def.JumpImpulse;

            (float f, string t)[] datos =
            {
                (vel / 100f, $"{vel:0} km/h"),
                (turbo / 2f, $"×{turbo:0.1}"),
                (masa / 3000f, $"{masa:0} kg"),
                (giro / 150f, $"{giro:0}°/s"),
                (salto / 20f, $"{salto:0} m/s"),
            };
            for (int k = 0; k < statsUI.Count; k++)
            {
                statsUI[k].relleno.rectTransform.sizeDelta = new Vector2(180 * Mathf.Clamp01(m.Disponible ? datos[k].f : 0f), 4);
                statsUI[k].valor.text = m.Disponible ? datos[k].t : "—";
            }
            for (int k = 0; k < tarjetas.Count; k++)
            {
                bool on = k == monturaSel;
                foreach (var b in tarjetas[k].borde) b.color = on ? Hueso : Linea;
                tarjetas[k].pincel.enabled = on;
                tarjetas[k].nombre.color = on ? Hueso : monturas[k].Disponible ? Arena : Apagado;
            }
            if (escenario != null) escenario.CambiarModelo(monturaSel);
            listaTitulo.FijarActivo(0, m.Disponible);
        }

        void CambiarEscenario(int d)
        {
            int n = Mathf.Max(1, SceneManager.sceneCountInBuildSettings);
            escenarioSel = ((escenarioSel + d) % n + n) % n;
            txtEscenario.text = "<  " + NombreEscena(SceneUtility.GetScenePathByBuildIndex(escenarioSel)).ToUpperInvariant() + "  >";
        }

        void Montar()
        {
            if (!monturas[monturaSel].Disponible) return;
            DinoMountManager.CurrentMountIndex = monturaSel;
            var vehiculo = FindAnyObjectByType<DinoVehicleController>();
            if (vehiculo != null)
            {
                DinoMountManager.AplicarMontura(vehiculo, monturaSel);
            }

            if (escenarioSel != SceneManager.GetActiveScene().buildIndex && escenarioSel < SceneManager.sceneCountInBuildSettings)
            {
                mostrarTituloAlCargar = false;
                CargarEscena(escenarioSel, null);
            }
            else Cerrar();
        }

        IEnumerator AbrirInicio(int esperarCuadros)
        {
            capturando = true;
            canvas.enabled = false;
            for (int i = 0; i < esperarCuadros; i++) yield return null;
            foreach (var h in FindObjectsByType<DinoHUD>(FindObjectsSortMode.None)) Apagar(h);
            Congelar();
            if (escenario == null) escenario = DinoEscenarioInicio.Crear();
            escenario.Encender();
            historial.Clear();
            mostrarTituloAlCargar = false;
            CambiarEscenario(0);
            ElegirMontura(monturaSel);
            Mostrar(Estado.Titulo);
            capturando = false;
        }

        void ConstruirPausa()
        {
            var p = NuevaPantalla(Estado.Pausa);
            Encabezado(p, "PAUSA", 300);
            listaPausa = new ListaMenu(p, 130, 400, 72, new[]
            {
                new ItemMenu("Continuar", () => Cerrar()),
                new ItemMenu("Mapa del valle", null, false),
                new ItemMenu("Bitácora", () => Ir(Estado.Bitacora)),
                new ItemMenu("Configuración", () => { panelAjustes.Pestana(0); Ir(Estado.Ajustes); }),
                new ItemMenu("Reiniciar escena", () => { mostrarTituloAlCargar = false; CargarEscena(SceneManager.GetActiveScene().buildIndex, SceneManager.GetActiveScene().path); }),
                new ItemMenu("Controles", () => { panelAjustes.Pestana(1); Ir(Estado.Ajustes); }),
                new ItemMenu("Salir al menú", () => StartCoroutine(AbrirInicio(0))),
            });
            BarraTeclas(p, ("ENTER", "SELECCIONAR"), ("ESC", "VOLVER"));
        }

        static string NombreEscena(string ruta)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(ruta);
            return n switch
            {
                "Simulation" => "Valle de simulación",
                "DinoTestTrack" => "Pista de pruebas",
                _ => n,
            };
        }

        void ConstruirAjustes()
        {
            var p = NuevaPantalla(Estado.Ajustes);
            Texto(p, "Encabezado", 130, 90, 600, 34, "AJUSTES", Bold, 26, Sangre, 900);
            Rect(p, "Filete", 130, 135, 60, 2, Sangre);
            panelAjustes = new PanelFilas(p, true, new[] { "SIMULACIÓN", "CONTROLES", "VIDEO", "AUDIO" }, ArmarPestanaAjustes);
            BarraTeclas(p, ("A / D", "AJUSTAR"), ("R", "RESTABLECER"), ("ESC", "ATRÁS"));
        }

        void ArmarPestanaAjustes(PanelFilas panel, int tab)
        {
            switch (tab)
            {
                case 0:
                    panel.Seccion("MUNDO");
                    panel.Deslizador("Escala de tiempo", () => SimulationSettings.TimeScale, v => SimulationSettings.TimeScale = v, 0.25f, 2f, 0.05f,
                        v => $"×{v:0.00}", "Qué tan rápido corre el tiempo del mundo. Bájalo para ver los choques en cámara lenta.");
                    panel.Deslizador("Gravedad", () => SimulationSettings.Gravedad, v => SimulationSettings.Gravedad = v, 1f, 20f, 0.25f,
                        v => $"{v:0.00} m/s²", "Aceleración hacia abajo del mundo. Afecta el salto, las caídas y los escombros.");
                    panel.Deslizador("Masa del carnotauro", () => SimulationSettings.MasaDino, v => SimulationSettings.MasaDino = v, 400f, 3000f, 50f,
                        v => $"{v:0} kg", "Más masa: más fuerza al chocar los árboles y más inercia al girar.");
                    panel.Seccion("ECOSISTEMA");
                    panel.Deslizador("Árboles con fruto", () => SimulationSettings.ArbolesConFruto, v => SimulationSettings.ArbolesConFruto = v, 0f, 1f, 0.05f,
                        v => $"{v * 100:0} %", "Parte de los árboles que sueltan fruta al ser golpeados.");
                    panel.Deslizador("Población de pájaros", () => SimulationSettings.PoblacionPajaros, v => SimulationSettings.PoblacionPajaros = Mathf.RoundToInt(v), 0f, 60f, 1f,
                        v => $"{v:0}", "Cantidad de pájaros que genera el controlador de bandadas.");
                    panel.Selector("Agresividad de las bandadas", () => (int)SimulationSettings.AgresividadBandadas, i => SimulationSettings.AgresividadBandadas = (SimulationSettings.Agresividad)i,
                        new[] { "Pacífica", "Territorial", "Hostil" }, "Si una bandada huye o ataca cuando el carnotauro se acerca o ruge.");
                    panel.Seccion("FÍSICA");
                    panel.Selector("Destrucción por fragmentos", () => SimulationSettings.DestruccionFragmentos ? 0 : 1, i => SimulationSettings.DestruccionFragmentos = i == 0,
                        new[] { "Activada", "Desactivada" }, "Al superar la fuerza umbral, cajas y troncos se parten en fragmentos.");
                    panel.Deslizador("Rebote de la pelota", () => SimulationSettings.RebotePelota, v => SimulationSettings.RebotePelota = v, 0f, 1f, 0.05f,
                        v => $"{v:0.00}", "Coeficiente de restitución de la pelota.");
                    panel.Selector("Mostrar vectores de fuerza", () => SimulationSettings.MostrarVectores ? 1 : 0, i => SimulationSettings.MostrarVectores = i == 1,
                        new[] { "No", "Sí" }, "Dibuja la velocidad y la fuerza de impacto sobre el carnotauro.");
                    break;
                case 1:
                    panel.Seccion("TECLADO");
                    panel.Info("Acelerar / reversa", "W / S");
                    panel.Info("Girar", "A / D");
                    panel.Info("Frenar", "ESPACIO");
                    panel.Info("Saltar / pisotón", "F");
                    panel.Info("Rugido (turbo)", "E · CLIC");
                    panel.Info("Sprint", "SHIFT");
                    panel.Info("Reiniciar posición", "R");
                    panel.Info("Pausa", "ESC");
                    panel.Seccion("ARDUINO");
                    var ard = FindAnyObjectByType<ArduinoSerialConnector>();
                    panel.Info("Estado", ard != null && ard.IsConnected ? "CONECTADO" : "DESCONECTADO");
                    panel.Info("Puerto", ard != null ? ard.portName + " · " + ard.baudRate : "—");
                    break;
                case 2:
                    panel.Seccion("PANTALLA");
                    panel.Selector("Pantalla completa", () => SimulationSettings.PantallaCompleta ? 0 : 1, i => SimulationSettings.PantallaCompleta = i == 0,
                        new[] { "Sí", "No" }, "Alterna entre pantalla completa y ventana.");
                    panel.Selector("Calidad", () => Mathf.Max(0, SimulationSettings.Calidad), i => SimulationSettings.Calidad = i,
                        QualitySettings.names, "Nivel de calidad gráfica de Unity.");
                    panel.Selector("Sincronización vertical", () => QualitySettings.vSyncCount > 0 ? 0 : 1, i => QualitySettings.vSyncCount = i == 0 ? 1 : 0,
                        new[] { "Sí", "No" }, "Evita el desgarro de imagen limitando los FPS al monitor.");
                    break;
                case 3:
                    panel.Seccion("SONIDO");
                    panel.Deslizador("Volumen general", () => SimulationSettings.Volumen, v => SimulationSettings.Volumen = v, 0f, 1f, 0.05f,
                        v => $"{v * 100:0} %", "Volumen de todo el juego.");
                    break;
            }
            panel.AlCambiar = () => { SimulationSettings.AplicarMundo(); SimulationSettings.Guardar(); };
            panel.AlRestablecer = tab == 0 ? () => SimulationSettings.RestablecerSimulacion() : null;
        }

        void ConstruirBitacora()
        {
            var p = NuevaPantalla(Estado.Bitacora);
            Encabezado(p, "BITÁCORA", 90);
            panelBitacora = new PanelFilas(p, false, null, (panel, _) =>
            {
                panel.Seccion("SIMULACIÓN");
                panel.Info("Tiempo simulado", () => TimeSpan.FromSeconds(Time.timeSinceLevelLoad).ToString(@"hh\:mm\:ss"));
                panel.Info("Escala de tiempo", () => $"×{SimulationSettings.TimeScale:0.00}");
                panel.Info("Gravedad", () => $"{SimulationSettings.Gravedad:0.00} m/s²");
                panel.Seccion("CARNOTAURO");
                panel.Info("Velocidad", () =>
                {
                    var v = FindAnyObjectByType<DinoVehicleController>();
                    return v != null ? $"{Mathf.Abs(v.CurrentSpeedKmh):0} km/h" : "—";
                });
                panel.Info("Masa", () => $"{SimulationSettings.MasaDino:0} kg");
                panel.Info("Último impacto", () =>
                {
                    var t = FindAnyObjectByType<TreeImpactResponse>();
                    return t != null && t.LastImpactForce > 0 ? $"{t.LastImpactForce / 1000f:0.0} kN" : "—";
                });
                panel.Seccion("ECOSISTEMA");
                panel.Info("Árboles derribados", () =>
                {
                    var arboles = FindObjectsByType<TreeReaction>(FindObjectsSortMode.None);
                    int caidos = 0;
                    foreach (var a in arboles) if (a.HasFallen) caidos++;
                    return arboles.Length > 0 ? $"{caidos} / {arboles.Length}" : "—";
                });
            });
            BarraTeclas(p, ("ESC", "ATRÁS"));
        }

        void ConstruirCreditos()
        {
            var p = NuevaPantalla(Estado.Creditos);
            Encabezado(p, "CRÉDITOS", 90);
            panelCreditos = new PanelFilas(p, false, null, (panel, _) =>
            {
                panel.Seccion("EQUIPO · UNIVERSIDAD DE BOYACÁ");
                panel.Info("Juan J. Plazas", "JUGABILIDAD Y FÍSICA");
                panel.Info("K. J. Barreto", "ARDUINO Y SIMULACIÓN");
                panel.Info("David Rosas", "ARTE 3D Y NIVELES");
                panel.Seccion("UNIVERSO");
                panel.Info("DINO WEST", "NIDRA ESTUDIO");
            });
            BarraTeclas(p, ("ESC", "ATRÁS"));
        }

        // =====================================================================================
        // API pública (para el Arduino u otros scripts)
        public void AbrirPausa()
        {
            if (!Abierto && !capturando) StartCoroutine(AbrirDesdeJuego(Estado.Pausa, 0));
        }

        public void IrA(Estado e, int pestana = 0)
        {
            if (!Abierto) return;
            if (e == Estado.Ajustes) panelAjustes.Pestana(pestana);
            Ir(e);
        }

        // =====================================================================================
        // Flujo
        void Update()
        {
            if (capturando) return;
            if (!Abierto)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                    StartCoroutine(AbrirDesdeJuego(Estado.Pausa, 0));
                return;
            }

            bool arriba = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            bool abajo = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            bool izq = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool der = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            bool ok = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
            bool atras = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);

            switch (Actual)
            {
                case Estado.Titulo:
                    Navegar(listaTitulo, arriba, abajo, ok);
                    if (Input.GetKeyDown(KeyCode.Q)) ElegirMontura(monturaSel - 1);
                    if (Input.GetKeyDown(KeyCode.E)) ElegirMontura(monturaSel + 1);
                    if (listaTitulo.Seleccion == 1 && (izq || der)) CambiarEscenario(izq ? -1 : 1);
                    break;
                case Estado.Pausa: Navegar(listaPausa, arriba, abajo, ok); if (atras) { Cerrar(); return; } break;
                case Estado.Ajustes:
                    if (arriba) panelAjustes.Mover(-1);
                    if (abajo) panelAjustes.Mover(1);
                    if (izq) panelAjustes.Ajustar(-1);
                    if (der) panelAjustes.Ajustar(1);
                    if (Input.GetKeyDown(KeyCode.Q)) panelAjustes.Pestana(panelAjustes.PestanaActual - 1);
                    if (Input.GetKeyDown(KeyCode.E)) panelAjustes.Pestana(panelAjustes.PestanaActual + 1);
                    if (Input.GetKeyDown(KeyCode.R)) panelAjustes.Restablecer();
                    break;
                case Estado.Bitacora: panelBitacora.Refrescar(); break;
            }
            if (atras && Actual != Estado.Titulo) Volver();
        }

        static void Navegar(ListaMenu l, bool arriba, bool abajo, bool ok)
        {
            if (arriba) l.Mover(-1);
            if (abajo) l.Mover(1);
            if (ok) l.Activar();
        }

        void Ir(Estado e)
        {
            historial.Push(Actual);
            Mostrar(e);
        }

        void Volver()
        {
            if (historial.Count > 0) Mostrar(historial.Pop());
            else if (Actual != Estado.Titulo) Cerrar();
        }

        void Mostrar(Estado e)
        {
            Actual = e;
            foreach (var kv in pantallas) kv.Value.gameObject.SetActive(kv.Key == e);
            bool esAjustes = e == Estado.Ajustes || e == Estado.Bitacora || e == Estado.Creditos;
            sombraIzq.gameObject.SetActive(!esAjustes);
            sombraAjustes.gameObject.SetActive(esAjustes);
            zarpazo.gameObject.SetActive(e == Estado.Pausa);
            bool enEscenario = escenario != null && escenario.Activo;
            fondo.enabled = !enEscenario;
            sombraIzq.color = Alfa(Color.white, e == Estado.Titulo ? 0.85f : 1f);
            sombraAjustes.color = Alfa(Color.white, enEscenario ? 0.93f : 0.7f);
            hud.Mostrar(false);
            if (fondoMat != null)
            {
                fondoMat.SetFloat("_Bright", e == Estado.Titulo ? 0.60f : esAjustes ? 0.58f : 0.62f);
                fondoMat.SetFloat("_Blur", e == Estado.Titulo ? 1.5f : esAjustes ? 6f : 2f);
            }
            if (e == Estado.Ajustes) panelAjustes.Pestana(panelAjustes.PestanaActual);
            if (e == Estado.Bitacora) panelBitacora.Pestana(0);
            if (e == Estado.Creditos) panelCreditos.Pestana(0);
            canvas.enabled = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        IEnumerator AbrirDesdeJuego(Estado e, int esperarCuadros)
        {
            capturando = true;
            canvas.enabled = false;
            for (int i = 0; i < esperarCuadros; i++) yield return null;
            // el HUD de OnGUI no debe quedar en la foto
            foreach (var hud in FindObjectsByType<DinoHUD>(FindObjectsSortMode.None)) Apagar(hud);
            yield return null;
            yield return new WaitForEndOfFrame();
            if (captura != null) Destroy(captura);
            captura = ScreenCapture.CaptureScreenshotAsTexture();
            captura.wrapMode = TextureWrapMode.Clamp;
            fondo.texture = captura;
            Congelar();
            historial.Clear();
            mostrarTituloAlCargar = false;
            Mostrar(e);
            capturando = false;
        }

        void Apagar(Behaviour b)
        {
            if (b == null || !b.enabled) return;
            b.enabled = false;
            apagados.Add(b);
        }

        void Congelar()
        {
            if (congelado) return;
            congelado = true;
            foreach (var b in FindObjectsByType<DinoInputManager>(FindObjectsSortMode.None)) Apagar(b);
            foreach (var b in FindObjectsByType<DinoVehicleController>(FindObjectsSortMode.None)) Apagar(b);
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public void Cerrar()
        {
            canvas.enabled = false;
            Actual = Estado.Jugando;
            historial.Clear();
            if (escenario != null) escenario.Apagar();
            hud.Mostrar(true);
            StartCoroutine(Reanudar());
            congelado = false;
            Time.timeScale = SimulationSettings.TimeScale;
            AudioListener.pause = false;
            SimulationSettings.AplicarMundo();
        }

        // Se reactivan un cuadro después para que el clic o la tecla que cerró el menú
        // no le llegue al carnotauro (el clic izquierdo es el rugido).
        IEnumerator Reanudar()
        {
            var lista = new List<Behaviour>(apagados);
            apagados.Clear();
            yield return null;
            if (Abierto) { apagados.AddRange(lista); yield break; }
            foreach (var b in lista) if (b != null) b.enabled = true;
        }

        void CargarEscena(int indice, string ruta)
        {
            if (escenario != null) escenario.Apagar();
            escenario = null;
            canvas.enabled = false;
            Actual = Estado.Jugando;
            Time.timeScale = SimulationSettings.TimeScale;
            AudioListener.pause = false;
            if (indice >= 0) SceneManager.LoadScene(indice);
            else SceneManager.LoadScene(ruta);
        }

        static void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =====================================================================================
        // Lista vertical de opciones (título, pausa, escenarios)
        public class ItemMenu
        {
            public string Nombre;
            public Action Accion;
            public bool Activo;
            public ItemMenu(string n, Action a, bool activo = true) { Nombre = n; Accion = a; Activo = activo; }

            internal TextMeshProUGUI texto;
            internal Image pincel, guion;
        }

        public class ListaMenu
        {
            readonly ItemMenu[] items;
            int sel;
            static readonly string[] pinceles = { "sel_0", "sel_1", "sel_2" };

            public ListaMenu(Transform p, float x, float y, float paso, ItemMenu[] items, float size = 44)
            {
                this.items = items;
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    float yy = y + i * paso;
                    string s = it.Nombre.ToUpperInvariant();
                    it.pincel = Imagen(Caja(p, "Selección · pincel", x - 70, yy - 26, 600, size + 56), Sprite(pinceles[i % 3]), Color.white);
                    it.guion = Rect(p, "Guion", x - 46, yy + size * 0.42f, 22, 3, Hueso);
                    it.texto = Texto(p, it.Nombre, x, yy, 900, size + 14, s, Semi, size,
                        it.Activo ? Arena : Apagado, 220, TextAlignmentOptions.TopLeft, true);
                    it.pincel.rectTransform.sizeDelta = new Vector2(AnchoTexto(s, size, 220) + 260, size + 56);
                    int idx = i;
                    var ptr = it.texto.gameObject.AddComponent<DinoMenuPuntero>();
                    ptr.Entra = () => { if (items[idx].Activo) Seleccionar(idx); };
                    ptr.Clic = _ => { if (items[idx].Activo) { Seleccionar(idx); Activar(); } };
                }
                sel = Array.FindIndex(items, it => it.Activo);
                Seleccionar(Mathf.Max(0, sel));
            }

            public int Seleccion => sel;

            public void FijarActivo(int i, bool activo)
            {
                if (i < 0 || i >= items.Length) return;
                items[i].Activo = activo;
                Seleccionar(sel);
            }

            public void Seleccionar(int i)
            {
                sel = i;
                for (int k = 0; k < items.Length; k++)
                {
                    var it = items[k];
                    bool on = k == sel && it.Activo;
                    it.pincel.enabled = on;
                    it.guion.enabled = on;
                    it.texto.font = on ? Bold : Semi;
                    it.texto.color = on ? Hueso : it.Activo ? Arena : Apagado;
                }
            }

            public void Mover(int d)
            {
                int n = items.Length, i = sel;
                for (int k = 0; k < n; k++)
                {
                    i = (i + d + n) % n;
                    if (items[i].Activo) { Seleccionar(i); return; }
                }
            }

            public void Activar()
            {
                if (sel >= 0 && sel < items.Length && items[sel].Activo) items[sel].Accion?.Invoke();
            }
        }

        // =====================================================================================
        // Panel de filas (ajustes con pestañas, bitácora, créditos)
        public class PanelFilas
        {
            class Fila
            {
                public RectTransform raiz;
                public TextMeshProUGUI nombre, valor, flechaIzq, flechaDer;
                public Image pincel, relleno, tirador;
                public bool seleccionable;
                public Action<int> paso;
                public Action<float> fijar;     // deslizador: fija por fracción 0-1
                public Func<string> leer;
                public Func<float> fraccion;
                public string ayuda;
            }

            const float X = 140, ANCHO = 1640, ALTO_FILA = 58, CONTROL_X = ANCHO - 620, RIEL = 400;

            readonly Transform panel;
            readonly RectTransform filas;
            readonly TextMeshProUGUI ayuda;
            readonly List<Fila> lista = new();
            readonly List<(TextMeshProUGUI t, Image barra)> pestanas = new();
            readonly Action<PanelFilas, int> armar;
            readonly string[] nombresPestanas;
            float y;
            int sel = -1;

            public int PestanaActual { get; private set; }
            public Action AlCambiar, AlRestablecer;

            public PanelFilas(Transform p, bool conPestanas, string[] tabs, Action<PanelFilas, int> armar)
            {
                panel = p;
                this.armar = armar;
                nombresPestanas = tabs;
                float top = 210;
                if (conPestanas)
                {
                    Tecla(p, 130, 186, "Q", null);
                    float tx = 200;
                    for (int i = 0; i < tabs.Length; i++)
                    {
                        var t = Texto(p, "Pestaña " + tabs[i], tx, 180, 300, 50, tabs[i], Semi, 36, Gris, 220, TextAlignmentOptions.TopLeft, true);
                        float w = AnchoTexto(tabs[i], 36, 220);
                        var barra = Rect(p, "Pestaña activa", tx, 232, w, 3, Sangre);
                        pestanas.Add((t, barra));
                        int idx = i;
                        t.gameObject.AddComponent<DinoMenuPuntero>().Clic = _ => Pestana(idx);
                        tx += w + 80;
                    }
                    Tecla(p, tx, 186, "E", null);
                    Rect(p, "Línea de pestañas", 130, 250, ANCHO + 20, 1, Linea);
                    top = 275;
                }
                filas = Caja(p, "Filas", 0, top, W, H - top);
                ayuda = Texto(p, "Ayuda", X, H - 120, 1300, 34, "", Fina, 24, Arena, 40);
                ayuda.rectTransform.anchorMin = ayuda.rectTransform.anchorMax = new Vector2(0, 0);
                ayuda.rectTransform.pivot = new Vector2(0, 0);
                ayuda.rectTransform.anchoredPosition = new Vector2(X, 90);
            }

            public void Pestana(int i)
            {
                int n = nombresPestanas?.Length ?? 1;
                PestanaActual = ((i % n) + n) % n;
                for (int k = 0; k < pestanas.Count; k++)
                {
                    bool on = k == PestanaActual;
                    pestanas[k].t.font = on ? Bold : Semi;
                    pestanas[k].t.color = on ? Hueso : Gris;
                    pestanas[k].barra.enabled = on;
                }
                for (int k = filas.childCount - 1; k >= 0; k--) { var hijo = filas.GetChild(k).gameObject; hijo.SetActive(false); UnityEngine.Object.Destroy(hijo); }
                lista.Clear();
                y = 0;
                sel = -1;
                AlCambiar = AlRestablecer = null;
                armar(this, PestanaActual);
                Mover(1);
                Refrescar();
            }

            // ---- constructores de filas ----
            public void Seccion(string s)
            {
                if (lista.Count > 0) y += 12;
                Texto(filas, "Sección " + s, X, y, 600, 24, s, Semi, 17, Sangre, 700);
                y += 34;
            }

            Fila NuevaFila(string nombre, bool seleccionable)
            {
                var f = new Fila { seleccionable = seleccionable };
                f.raiz = Caja(filas, "Fila · " + nombre, X, y, ANCHO, ALTO_FILA);
                var hit = Imagen(f.raiz, null, new Color(0, 0, 0, 0), true);   // zona de ratón
                f.pincel = Imagen(Caja(f.raiz, "Fila seleccionada · pincel", -30, -4, ANCHO * 0.62f, 60), Sprite("sel_fila"), Alfa(Color.white, 0.85f));
                f.pincel.enabled = false;
                f.nombre = Texto(f.raiz, "Nombre", 10, 12, 700, 32, nombre.ToUpperInvariant(), Semi, 25, Arena, 180);
                lista.Add(f);
                int idx = lista.Count - 1;
                var ptr = f.raiz.gameObject.AddComponent<DinoMenuPuntero>();
                ptr.Entra = () => { if (seleccionable) Seleccionar(idx); };
                ptr.Rueda = d => { if (seleccionable) { Seleccionar(idx); Ajustar(d > 0 ? 1 : -1); } };
                ptr.Clic = e =>
                {
                    if (!seleccionable) return;
                    Seleccionar(idx);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(f.raiz, e.position, e.pressEventCamera, out var local);
                    if (f.fijar != null && local.x >= CONTROL_X - 10 && local.x <= CONTROL_X + RIEL + 10)
                    {
                        f.fijar(Mathf.Clamp01((local.x - CONTROL_X) / RIEL));
                        AlCambiar?.Invoke();
                        Refrescar();
                    }
                    else if (f.paso != null) Ajustar(local.x < CONTROL_X + 230 + 180 ? -1 : 1);
                };
                y += ALTO_FILA;
                return f;
            }

            public void Deslizador(string nombre, Func<float> get, Action<float> set, float min, float max, float paso,
                Func<float, string> formato, string ayudaTxt)
            {
                var f = NuevaFila(nombre, true);
                Rect(f.raiz, "Riel", CONTROL_X, 26, RIEL, 2, Riel);
                f.relleno = Rect(f.raiz, "Relleno", CONTROL_X, 25, RIEL, 4, Arena);
                f.tirador = Rect(f.raiz, "Tirador", CONTROL_X, 15, 4, 24, Hueso);
                f.valor = Texto(f.raiz, "Valor", CONTROL_X + RIEL + 24, 14, 170, 30, "", Bold, 24, Hueso, 60, TextAlignmentOptions.TopRight);
                f.fraccion = () => Mathf.InverseLerp(min, max, get());
                f.leer = () => formato(get());
                f.paso = d => set(Mathf.Clamp(Mathf.Round((get() + d * paso) / paso) * paso, min, max));
                f.fijar = t => set(Mathf.Clamp(Mathf.Round(Mathf.Lerp(min, max, t) / paso) * paso, min, max));
                f.ayuda = ayudaTxt;
            }

            public void Selector(string nombre, Func<int> get, Action<int> set, string[] opciones, string ayudaTxt)
            {
                var f = NuevaFila(nombre, true);
                float cx = CONTROL_X + 230;
                f.flechaIzq = Texto(f.raiz, "<", cx, 14, 30, 30, "<", Bold, 24, Gris, 0, TextAlignmentOptions.Top);
                f.valor = Texto(f.raiz, "Valor", cx + 30, 14, 300, 30, "", Bold, 24, Hueso, 200, TextAlignmentOptions.Top);
                f.flechaDer = Texto(f.raiz, ">", cx + 330, 14, 30, 30, ">", Bold, 24, Gris, 0, TextAlignmentOptions.Top);
                f.leer = () => opciones.Length > 0 ? opciones[Mathf.Clamp(get(), 0, opciones.Length - 1)].ToUpperInvariant() : "—";
                f.paso = d => { if (opciones.Length > 0) set(((get() + d) % opciones.Length + opciones.Length) % opciones.Length); };
                f.ayuda = ayudaTxt;
            }

            public void Info(string nombre, string valor) => Info(nombre, () => valor);

            public void Info(string nombre, Func<string> valor)
            {
                var f = NuevaFila(nombre, false);
                f.valor = Texto(f.raiz, "Valor", CONTROL_X, 14, 570, 30, "", Bold, 24, Hueso, 120, TextAlignmentOptions.TopRight);
                f.leer = valor;
            }

            // ---- interacción ----
            void Seleccionar(int i)
            {
                sel = i;
                for (int k = 0; k < lista.Count; k++)
                {
                    var f = lista[k];
                    bool on = k == sel;
                    f.pincel.enabled = on;
                    f.nombre.font = on ? Bold : Semi;
                    f.nombre.color = on ? Hueso : Arena;
                    if (f.flechaIzq != null) f.flechaIzq.color = f.flechaDer.color = on ? Sangre : Gris;
                }
                ayuda.text = sel >= 0 && sel < lista.Count ? lista[sel].ayuda ?? "" : "";
                Refrescar();
            }

            public void Mover(int d)
            {
                int n = lista.Count, i = sel;
                if (n == 0) { ayuda.text = ""; return; }
                for (int k = 0; k < n; k++)
                {
                    i = ((i + d) % n + n) % n;
                    if (lista[i].seleccionable) { Seleccionar(i); return; }
                }
                ayuda.text = "";
            }

            public void Ajustar(int d)
            {
                if (sel < 0 || sel >= lista.Count || lista[sel].paso == null) return;
                lista[sel].paso(d);
                AlCambiar?.Invoke();
                Refrescar();
            }

            public void Restablecer()
            {
                if (AlRestablecer == null) return;
                AlRestablecer();
                AlCambiar?.Invoke();
                Refrescar();
            }

            public void Refrescar()
            {
                for (int k = 0; k < lista.Count; k++)
                {
                    var f = lista[k];
                    if (f.valor != null && f.leer != null) f.valor.text = f.leer();
                    if (f.fraccion != null)
                    {
                        float t = Mathf.Clamp01(f.fraccion());
                        f.relleno.rectTransform.sizeDelta = new Vector2(RIEL * t, 4);
                        f.relleno.color = k == sel ? Hueso : Arena;
                        f.tirador.rectTransform.anchoredPosition = new Vector2(CONTROL_X + RIEL * t - 2, -15);
                    }
                }
            }
        }
    }
}
