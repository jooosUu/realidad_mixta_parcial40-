using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Recibe por UDP (127.0.0.1:5055) el estado de las manos que envía Tools/mediapipe_hand_controller.py
    /// y lo vuelca en DinoInputManager (handSteering, handThrottle, ...).
    /// - El socket se lee en un hilo de fondo; Unity solo copia el último paquete en Update.
    /// - Fail-safe: si pasan más de 0.45 s sin datos o sin manos, suelta volante y acelerador.
    /// - Se crea solo al cargar cualquier escena (no hace falta tocar las escenas).
    /// </summary>
    [DefaultExecutionOrder(-200)] // antes que DinoInputManager y el vehículo
    public class MediaPipeHandReceiver : MonoBehaviour
    {
        public const int PuertoPorDefecto = 5055;
        const float TiempoSinDatos = 0.45f;

        public static MediaPipeHandReceiver Instance { get; private set; }

        // ---- estado público para menús y HUD (hilo principal) ----
        /// <summary>Hay datos recientes Y al menos una mano visible.</summary>
        public static bool Conectado { get; private set; }
        /// <summary>El script de Python está enviando (aunque no vea manos).</summary>
        public static bool RecibiendoDatos { get; private set; }
        /// <summary>Modo cursor: una sola mano visible.</summary>
        public static bool HayCursor { get; private set; }
        /// <summary>Cursor normalizado 0..1 con origen arriba-izquierda (como la imagen de la webcam).</summary>
        public static Vector2 Cursor { get; private set; } = new Vector2(0.5f, 0.5f);
        public static bool Pinch { get; private set; }
        public static int ManosDetectadas { get; private set; }

        static int pausaPendiente;
        static int atrasPendiente;

        /// <summary>Devuelve true una sola vez por cada puño mantenido (gesto de "atrás" con una mano).</summary>
        public static bool ConsumirAtras()
        {
            if (atrasPendiente <= 0) return false;
            atrasPendiente = 0;
            return true;
        }

        /// <summary>Devuelve true una sola vez por cada gesto de pausa recibido.</summary>
        public static bool ConsumirPausa()
        {
            if (pausaPendiente <= 0) return false;
            pausaPendiente = 0;
            return true;
        }

        [Serializable]
        class Paquete
        {
            public int seq;
            public int hands;
            public string mode;
            public float steer;
            public float throttle;
            public int sprint;
            public int brake;
            public float cx = 0.5f;
            public float cy = 0.5f;
            public int pinch;
            public int jumpId;
            public int roarId;
            public int pauseId;
            public int backId;
        }

        [Tooltip("Puerto UDP donde escucha (debe coincidir con el script de Python)")]
        public int puerto = PuertoPorDefecto;

        UdpClient cliente;
        Thread hilo;
        volatile bool corriendo;

        readonly object candado = new();
        Paquete ultimo;
        double ultimoTiempoRecepcion = -1; // en segundos (Stopwatch), escrito por el hilo
        readonly System.Diagnostics.Stopwatch reloj = System.Diagnostics.Stopwatch.StartNew();

        int jumpIdVisto = -1, roarIdVisto = -1, pauseIdVisto = -1, backIdVisto = -1;
        bool avisoPuerto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCrear()
        {
            if (Instance != null) return;
            var go = new GameObject("MediaPipe · Receptor de manos");
            DontDestroyOnLoad(go);
            go.AddComponent<MediaPipeHandReceiver>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Iniciar();
        }

        void Iniciar()
        {
            try
            {
                cliente = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
                cliente.Client.ReceiveTimeout = 500;
                corriendo = true;
                hilo = new Thread(Escuchar) { IsBackground = true, Name = "MediaPipeHandReceiver" };
                hilo.Start();
                Debug.Log($"<color=cyan>[MediaPipeHandReceiver]</color> Escuchando manos en 127.0.0.1:{puerto}");
            }
            catch (Exception e)
            {
                if (!avisoPuerto)
                {
                    avisoPuerto = true;
                    Debug.LogWarning($"[MediaPipeHandReceiver] No se pudo abrir el puerto UDP {puerto}: {e.Message}. Control por manos desactivado.");
                }
                Cerrar();
            }
        }

        void Escuchar()
        {
            var remoto = new IPEndPoint(IPAddress.Any, 0);
            while (corriendo)
            {
                try
                {
                    byte[] datos = cliente.Receive(ref remoto);
                    var p = JsonUtility.FromJson<Paquete>(Encoding.UTF8.GetString(datos));
                    if (p == null) continue;
                    lock (candado)
                    {
                        // Si llegan desordenados se descarta el viejo (salvo reinicio del script: seq vuelve a empezar).
                        if (ultimo != null && p.seq < ultimo.seq && ultimo.seq - p.seq < 1000) continue;
                        ultimo = p;
                        ultimoTiempoRecepcion = reloj.Elapsed.TotalSeconds;
                    }
                }
                catch (SocketException)
                {
                    // Timeout de lectura (sin datos) o socket cerrado al salir: se vuelve a comprobar 'corriendo'.
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    // JSON corrupto u otro error puntual: se ignora el paquete.
                    if (corriendo) Debug.LogWarning($"[MediaPipeHandReceiver] Paquete ignorado: {e.Message}");
                }
            }
        }

        void Update()
        {
            Paquete p;
            double edad;
            lock (candado)
            {
                p = ultimo;
                edad = ultimoTiempoRecepcion < 0 ? double.MaxValue : reloj.Elapsed.TotalSeconds - ultimoTiempoRecepcion;
            }

            RecibiendoDatos = p != null && edad <= TiempoSinDatos;
            bool hayManos = RecibiendoDatos && p.hands > 0;
            bool conduciendo = hayManos && p.mode == "drive";
            Conectado = hayManos;
            ManosDetectadas = RecibiendoDatos ? p.hands : 0;
            HayCursor = hayManos && p.mode == "cursor";
            if (HayCursor) Cursor = new Vector2(Mathf.Clamp01(p.cx), Mathf.Clamp01(p.cy));
            Pinch = HayCursor && p.pinch != 0;

            var input = DinoInputManager.Instance;

            // Eventos por contador: un gesto = un disparo, aunque se pierda algún paquete.
            bool salto = false, rugido = false;
            if (p != null && RecibiendoDatos)
            {
                // Contador menor que el visto = el script de Python se reinició: solo se toma como referencia.
                if (jumpIdVisto >= 0 && p.jumpId > jumpIdVisto) salto = true;
                if (roarIdVisto >= 0 && p.roarId > roarIdVisto) rugido = true;
                if (pauseIdVisto >= 0 && p.pauseId > pauseIdVisto) pausaPendiente = 1;
                if (backIdVisto >= 0 && p.backId > backIdVisto) atrasPendiente = 1;
                jumpIdVisto = p.jumpId;
                roarIdVisto = p.roarId;
                pauseIdVisto = p.pauseId;
                backIdVisto = p.backId;
            }

            if (input == null) return;

            input.handConnected = hayManos;
            input.handCursor = Cursor;
            input.handPinch = Pinch;

            if (conduciendo)
            {
                input.handSteering = Mathf.Clamp(p.steer, -1f, 1f);
                input.handThrottle = Mathf.Clamp(p.throttle, 0f, 1f);
                input.handSprint = p.sprint != 0;
                input.handBrake = p.brake != 0;
                if (salto) input.handJump = true;
                if (rugido) input.handRoar = true;
            }
            else
            {
                // Fail-safe: sin datos, sin manos o en modo cursor el dino no se mueve solo.
                input.handSteering = 0f;
                input.handThrottle = 0f;
                input.handSprint = false;
                input.handBrake = false;
            }
        }

        void OnApplicationQuit() => Cerrar();

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Conectado = RecibiendoDatos = HayCursor = Pinch = false;
                ManosDetectadas = 0;
            }
            Cerrar();
        }

        void Cerrar()
        {
            corriendo = false;
            try { cliente?.Close(); } catch (Exception) { }
            cliente = null;
            if (hilo != null && hilo.IsAlive && Thread.CurrentThread != hilo)
            {
                hilo.Join(700);
            }
            hilo = null;
        }
    }
}
