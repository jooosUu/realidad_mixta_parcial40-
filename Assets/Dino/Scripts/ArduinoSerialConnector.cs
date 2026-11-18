using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Mando físico (ESP32 / Arduino) con 2 potenciómetros y 2 pulsadores por puerto serie.
    /// - Busca solo el puerto: prueba el configurado y, si no responde, todos los COM hasta encontrar
    ///   las tramas "DW,pot1,pot2,b1,b2" (o el formato antiguo "steer,throttle,jump,roar").
    /// - Si se desconecta la placa, vuelve a buscarla cada pocos segundos.
    /// - Juego: pot1 = volante, pot2 = acelerador, botón 1 = salto, botón 2 = rugido,
    ///   los dos botones mantenidos 1 s = pausa.
    /// - Menús: expone el estado (Pot1, Pot2, Boton1, Boton2) para el puntero de DinoHandCursor,
    ///   y el botón 2 sirve de "atrás".
    /// La lectura va en un hilo aparte para no congelar frames.
    /// </summary>
    public class ArduinoSerialConnector : MonoBehaviour
    {
        [Header("Configuración del Puerto")]
        [Tooltip("Puerto preferido (ej. COM3). Si no responde se buscan todos automáticamente.")]
        public string portName = "COM3";
        [Tooltip("Velocidad en baudios (debe coincidir con Serial.begin en Arduino)")]
        public int baudRate = 115200;
        [Tooltip("Reintentar conexión automáticamente si no está conectado")]
        public bool autoConnect = true;

        [Header("Mapeo de Valores Arduino")]
        [Tooltip("Formato antiguo sin 'DW': valores analógicos crudos de 0 a 1023")]
        public bool expectsRawPotValues = true;
        [Tooltip("Zona muerta para evitar temblores del potenciómetro")]
        public float deadzone = 0.06f;
        [Tooltip("Invierte el sentido del volante si gira al revés")]
        public bool invertirVolante = false;
        [Tooltip("Invierte el acelerador si al subirlo frena")]
        public bool invertirAcelerador = false;
        [Tooltip("Por encima de esta posición del acelerador el dino corre (sprint)")]
        [Range(0.5f, 1f)] public float umbralCorrer = 0.85f;

        // ---------------- Estado global (hilo principal) ----------------
        /// <summary>El mando activo (hay uno solo aunque la escena traiga otro).</summary>
        public static ArduinoSerialConnector Activo { get; private set; }
        public static bool Conectado => Activo != null && Activo.IsConnected && Activo.recibiendo;
        /// <summary>Potenciómetros normalizados 0..1 (ya invertidos según la configuración).</summary>
        public static float Pot1 { get; private set; } = 0.5f;
        public static float Pot2 { get; private set; }
        public static bool Boton1 { get; private set; }
        public static bool Boton2 { get; private set; }
        /// <summary>Time.unscaledTime de la última vez que se tocó algo del mando.</summary>
        public static float UltimoUso { get; private set; } = -999f;

        static bool atrasPendiente, pausaPendiente;

        /// <summary>Botón 2 en los menús (una vez por pulsación).</summary>
        public static bool ConsumirAtras() { bool v = atrasPendiente; atrasPendiente = false; return v; }
        /// <summary>Los dos botones mantenidos 1 s en el juego (una vez por gesto).</summary>
        public static bool ConsumirPausa() { bool v = pausaPendiente; pausaPendiente = false; return v; }

        public bool IsConnected => serialPort != null && serialPort.IsOpen;

        private SerialPort serialPort;
        private Thread readThread;
        private volatile bool isRunning;
        private volatile bool buscando;
        private bool recibiendo;
        private float proximoIntento;
        private static bool avisoSinPuertos;

        // Escrito por el hilo de lectura
        private readonly object candado = new object();
        private float hiloPot1 = 0.5f, hiloPot2;
        private bool hiloB1, hiloB2;
        private double hiloUltimaTrama = -1;
        private readonly System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();

        private bool b1Antes, b2Antes;
        private float inicioDosBotones = -1f;
        private bool pausaDisparada;
        private float pot1Visto = -1f, pot2Visto = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCrear()
        {
            // Si la escena no trae su conector (p. ej. menús), se crea uno persistente.
            if (Activo != null || FindAnyObjectByType<ArduinoSerialConnector>() != null) return;
            var go = new GameObject("Mando Arduino (Serial)");
            DontDestroyOnLoad(go);
            go.AddComponent<ArduinoSerialConnector>();
        }

        private void Awake()
        {
            if (Activo != null && Activo != this)
            {
                // Ya hay un mando abierto: este se apaga para no pelear por el puerto.
                enabled = false;
                return;
            }
            Activo = this;
        }

        private void Start()
        {
            if (Activo == this && autoConnect) TryOpenPort();
        }

        /// <summary>Abre el puerto en segundo plano (sin congelar el juego).</summary>
        public void TryOpenPort()
        {
            if (IsConnected || buscando) return;
            buscando = true;
            new Thread(BuscarYAbrir) { IsBackground = true, Name = "ArduinoBuscarPuerto" }.Start();
        }

        private void BuscarYAbrir()
        {
            try
            {
                string[] puertos = SerialPort.GetPortNames();
                if (puertos.Length == 0)
                {
                    if (!avisoSinPuertos)
                    {
                        avisoSinPuertos = true;
                        Debug.Log("[ArduinoSerial] No hay puertos COM: conecta el mando por USB. Mientras tanto funcionan teclado y manos.");
                    }
                    return;
                }
                avisoSinPuertos = false;

                // Primero el configurado, luego el resto.
                string preferido = portName;
                Array.Sort(puertos, (a, b) => string.Equals(a, preferido, StringComparison.OrdinalIgnoreCase) ? -1
                                             : string.Equals(b, preferido, StringComparison.OrdinalIgnoreCase) ? 1
                                             : string.CompareOrdinal(a, b));
                foreach (var p in puertos)
                {
                    var sp = ProbarPuerto(p);
                    if (sp == null) continue;

                    serialPort = sp;
                    portName = p;
                    isRunning = true;
                    readThread = new Thread(ReadSerialLoop) { IsBackground = true, Name = "ArduinoSerialLectura" };
                    readThread.Start();
                    Debug.Log($"[ArduinoSerial] Mando conectado en {p} a {baudRate} baudios.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ArduinoSerial] Error buscando el mando: {ex.Message}");
            }
            finally
            {
                buscando = false;
            }
        }

        /// <summary>Abre el puerto y espera hasta 2.5 s una trama válida (las ESP32 se reinician al abrir).</summary>
        private SerialPort ProbarPuerto(string nombre)
        {
            SerialPort sp = null;
            try
            {
                sp = new SerialPort(nombre, baudRate) { ReadTimeout = 300, WriteTimeout = 300, NewLine = "\n" };
                sp.Open();
                var limite = DateTime.UtcNow.AddSeconds(2.5);
                while (DateTime.UtcNow < limite)
                {
                    try
                    {
                        if (ParseLine(sp.ReadLine().Trim())) return sp;
                    }
                    catch (TimeoutException) { }
                }
            }
            catch (Exception) { }
            try { sp?.Close(); } catch (Exception) { }
            return null;
        }

        private void ReadSerialLoop()
        {
            while (isRunning && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    string line = serialPort.ReadLine();
                    if (!string.IsNullOrEmpty(line)) ParseLine(line.Trim());
                }
                catch (TimeoutException) { }
                catch (Exception)
                {
                    // Placa desconectada: se cierra y Update vuelve a buscarla.
                    break;
                }
            }
            try { serialPort?.Close(); } catch (Exception) { }
        }

        /// <summary>Devuelve true si la línea es una trama válida.</summary>
        private bool ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string[] parts = line.Split(',');
            bool nuevo = parts[0] == "DW";
            int o = nuevo ? 1 : 0;
            if (parts.Length < o + 2) return false;

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var num = System.Globalization.NumberStyles.Float;
            if (!float.TryParse(parts[o], num, inv, out float a) || !float.TryParse(parts[o + 1], num, inv, out float b)) return false;
            int b1 = 0, b2 = 0;
            if (parts.Length > o + 2) int.TryParse(parts[o + 2], out b1);
            if (parts.Length > o + 3) int.TryParse(parts[o + 3], out b2);

            float p1, p2;
            if (nuevo) { p1 = a / 1000f; p2 = b / 1000f; }
            else if (expectsRawPotValues) { p1 = a / 1023f; p2 = b / 1023f; }
            else { p1 = (a + 1f) * 0.5f; p2 = b; } // formato normalizado antiguo: steer -1..1, throttle 0..1

            lock (candado)
            {
                hiloPot1 = Mathf.Clamp01(p1);
                hiloPot2 = Mathf.Clamp01(p2);
                hiloB1 = b1 == 1;
                hiloB2 = b2 == 1;
                hiloUltimaTrama = reloj.Elapsed.TotalSeconds;
            }
            return true;
        }

        private void Update()
        {
            if (Activo != this) return;

            // Reconexión automática si se desenchufa o aún no se encontró.
            if (!IsConnected && autoConnect && !buscando && Time.unscaledTime >= proximoIntento)
            {
                proximoIntento = Time.unscaledTime + 3f;
                ClosePort();
                TryOpenPort();
            }

            float p1, p2; bool b1, b2; double edad;
            lock (candado)
            {
                p1 = hiloPot1; p2 = hiloPot2; b1 = hiloB1; b2 = hiloB2;
                edad = hiloUltimaTrama < 0 ? double.MaxValue : reloj.Elapsed.TotalSeconds - hiloUltimaTrama;
            }
            recibiendo = IsConnected && edad < 0.5;

            if (!recibiendo)
            {
                Boton1 = Boton2 = false;
                b1Antes = b2Antes = false;
                inicioDosBotones = -1f;
                if (DinoInputManager.Instance != null) DinoInputManager.Instance.arduinoConnected = false;
                return;
            }

            if (invertirVolante) p1 = 1f - p1;
            if (invertirAcelerador) p2 = 1f - p2;

            // "Usado" = se movió un potenciómetro o se pulsó un botón (decide quién mueve el puntero).
            if (pot1Visto < 0 || Mathf.Abs(p1 - pot1Visto) > 0.02f || Mathf.Abs(p2 - pot2Visto) > 0.02f || b1 || b2)
            {
                if (pot1Visto >= 0) UltimoUso = Time.unscaledTime;
                pot1Visto = p1;
                pot2Visto = p2;
            }
            Pot1 = p1;
            Pot2 = p2;
            Boton1 = b1;
            Boton2 = b2;

            bool flanco1 = b1 && !b1Antes;
            bool flanco2 = b2 && !b2Antes;
            b1Antes = b1;
            b2Antes = b2;

            bool menuAbierto = DinoMenuSystem.Instancia != null && DinoMenuSystem.Instancia.Abierto;

            // Pausa: los dos botones mantenidos 1 s durante el juego.
            if (b1 && b2 && !menuAbierto)
            {
                if (inicioDosBotones < 0) inicioDosBotones = Time.unscaledTime;
                else if (!pausaDisparada && Time.unscaledTime - inicioDosBotones > 1f)
                {
                    pausaDisparada = true;
                    pausaPendiente = true;
                }
            }
            else
            {
                inicioDosBotones = -1f;
                if (!b1 && !b2) pausaDisparada = false;
            }

            if (menuAbierto)
            {
                // En los menús el botón 1 lo usa el puntero (clic) y el botón 2 es "atrás".
                if (flanco2) atrasPendiente = true;
                if (DinoInputManager.Instance != null) DinoInputManager.Instance.SetArduinoInputs(0f, 0f, false, false);
                return;
            }
            atrasPendiente = false;

            // Juego: volante centrado en la mitad del potenciómetro, acelerador de 0 a 1.
            float steer = (p1 - 0.5f) * 2f;
            if (Mathf.Abs(steer) < deadzone) steer = 0f;
            else steer = Mathf.Sign(steer) * (Mathf.Abs(steer) - deadzone) / (1f - deadzone);

            float throttle = p2 < deadzone ? 0f : (p2 - deadzone) / (1f - deadzone);
            // Igual que el teclado: medio recorrido = caminar (~0.45), al fondo = correr (1.0).
            throttle = throttle >= umbralCorrer ? 1f : throttle * 0.45f / umbralCorrer;

            if (DinoInputManager.Instance != null)
            {
                // Con los dos botones a la vez (gesto de pausa) no se salta ni se ruge.
                DinoInputManager.Instance.SetArduinoInputs(steer, throttle, flanco1 && !b2, flanco2 && !b1);
            }
        }

        private void OnDisable()
        {
            if (Activo == this) ClosePort();
        }

        private void OnDestroy()
        {
            if (Activo == this) Activo = null;
        }

        private void OnApplicationQuit()
        {
            ClosePort();
        }

        public void ClosePort()
        {
            isRunning = false;
            try { serialPort?.Close(); } catch (Exception) { }
            if (readThread != null && readThread.IsAlive && Thread.CurrentThread != readThread) readThread.Join(500);
            readThread = null;
            serialPort = null;
            recibiendo = false;
        }
    }
}
