using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Switch;

namespace DinoVehicle
{
    /// <summary>
    /// Mando de Nintendo Switch (Pro Controller por USB o Bluetooth) con el Input System.
    /// También sirve cualquier gamepad que Unity reconozca (Xbox, PlayStation, Joy-Con a través de Steam/BetterJoy).
    /// Se crea solo al cargar la escena; no hace falta tocar escenas.
    ///
    /// Juego (nombres de botón de Switch):
    ///   Stick izquierdo = girar y avanzar / reversa.  ZR = avanzar.  ZL = reversa.
    ///   R o clic del stick izquierdo = correr.  B = frenar.  A = saltar.  X = rugir.  + = pausa.
    /// Menús:
    ///   Stick o cruceta = moverse.  A = aceptar.  B = atrás.  L / R = cambiar montura o pestaña.
    ///   Y = restablecer (ajustes).  + = cerrar la pausa.
    /// En mandos que no son de Nintendo se usa la posición: el botón de abajo acepta/salta y el de la derecha es atrás/freno.
    /// </summary>
    public class DinoGamepadConnector : MonoBehaviour
    {
        public static DinoGamepadConnector Activo { get; private set; }

        [Tooltip("Zona muerta de los sticks")]
        [Range(0f, 0.5f)] public float zonaMuerta = 0.18f;
        [Tooltip("Caminar con el stick o ZR sin correr (igual que W sin Shift)")]
        [Range(0.2f, 1f)] public float factorCaminar = 0.45f;
        [Tooltip("Tiempo antes de repetir al mantener el stick en los menús")]
        public float retardoRepeticion = 0.4f;
        public float intervaloRepeticion = 0.12f;

        /// <summary>Hay un gamepad conectado.</summary>
        public static bool Conectado => Gamepad.current != null;
        /// <summary>El gamepad actual es un mando de Switch.</summary>
        public static bool EsSwitch => Gamepad.current is SwitchProControllerHID;
        public static string NombreMando => Gamepad.current != null ? Gamepad.current.displayName : "";

        // ---- lectura para menús (se calcula una vez por frame, sin depender del orden de scripts) ----
        static int frameLeido = -1;
        static bool arriba, abajo, izq, der, ok, atras, anterior, siguiente, restablecer, pausa;
        static int dirMantenida;
        static float tSiguienteRepeticion;

        public static bool Arriba { get { Leer(); return arriba; } }
        public static bool Abajo { get { Leer(); return abajo; } }
        public static bool Izquierda { get { Leer(); return izq; } }
        public static bool Derecha { get { Leer(); return der; } }
        public static bool Aceptar { get { Leer(); return ok; } }
        public static bool Atras { get { Leer(); return atras; } }
        public static bool Anterior { get { Leer(); return anterior; } }
        public static bool Siguiente { get { Leer(); return siguiente; } }
        public static bool Restablecer { get { Leer(); return restablecer; } }
        /// <summary>Botón + pulsado en este frame.</summary>
        public static bool Pausa { get { Leer(); return pausa; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCrear()
        {
            if (Activo != null || FindAnyObjectByType<DinoGamepadConnector>() != null) return;
            var go = new GameObject("Mando Switch / Gamepad");
            DontDestroyOnLoad(go);
            go.AddComponent<DinoGamepadConnector>();
        }

        void Awake()
        {
            if (Activo != null && Activo != this) { Destroy(gameObject); return; }
            Activo = this;
            InputSystem.onDeviceChange += AlCambiarDispositivo;
        }

        void OnDestroy()
        {
            if (Activo == this) Activo = null;
            InputSystem.onDeviceChange -= AlCambiarDispositivo;
        }

        static void AlCambiarDispositivo(InputDevice d, InputDeviceChange c)
        {
            if (d is Gamepad && (c == InputDeviceChange.Added || c == InputDeviceChange.Removed))
                Debug.Log($"[DinoWest] Mando {(c == InputDeviceChange.Added ? "conectado" : "desconectado")}: {d.displayName}");
        }

        // Botones según el tipo de mando: en Switch "A" está a la derecha y "B" abajo.
        static ButtonControl BotonAceptar(Gamepad g) => g is SwitchProControllerHID ? g.buttonEast : g.buttonSouth;
        static ButtonControl BotonAtras(Gamepad g) => g is SwitchProControllerHID ? g.buttonSouth : g.buttonEast;
        static ButtonControl BotonRugir(Gamepad g) => g.buttonNorth; // X en Switch, Y/Triángulo en otros
        static ButtonControl BotonRestablecer(Gamepad g) => g.buttonWest;

        static void Leer()
        {
            if (frameLeido == Time.frameCount) return;
            frameLeido = Time.frameCount;
            arriba = abajo = izq = der = ok = atras = anterior = siguiente = restablecer = pausa = false;
            var g = Gamepad.current;
            if (g == null) { dirMantenida = 0; return; }

            ok = BotonAceptar(g).wasPressedThisFrame;
            atras = BotonAtras(g).wasPressedThisFrame;
            anterior = g.leftShoulder.wasPressedThisFrame;
            siguiente = g.rightShoulder.wasPressedThisFrame;
            restablecer = BotonRestablecer(g).wasPressedThisFrame;
            pausa = g.startButton.wasPressedThisFrame;

            // Dirección: cruceta o stick izquierdo, con repetición al mantener.
            Vector2 v = g.dpad.ReadValue() + g.leftStick.ReadValue();
            int dir = 0; // 1 arriba, 2 abajo, 3 izq, 4 der
            if (Mathf.Abs(v.y) >= 0.6f && Mathf.Abs(v.y) >= Mathf.Abs(v.x)) dir = v.y > 0 ? 1 : 2;
            else if (Mathf.Abs(v.x) >= 0.6f) dir = v.x < 0 ? 3 : 4;
            float r = Activo != null ? Activo.retardoRepeticion : 0.4f;
            float i = Activo != null ? Activo.intervaloRepeticion : 0.12f;
            bool dispara = false;
            if (dir != dirMantenida) { dirMantenida = dir; dispara = dir != 0; tSiguienteRepeticion = Time.unscaledTime + r; }
            else if (dir != 0 && Time.unscaledTime >= tSiguienteRepeticion) { dispara = true; tSiguienteRepeticion = Time.unscaledTime + i; }
            if (dispara)
            {
                arriba = dir == 1; abajo = dir == 2; izq = dir == 3; der = dir == 4;
            }
        }

        void Update()
        {
            var im = DinoInputManager.Instance;
            if (im == null) return;
            var g = Gamepad.current;
            if (g == null) { im.SetGamepadInputs(false, 0f, 0f, false, false, false, false); return; }

            bool menuAbierto = DinoMenuSystem.Instancia != null && DinoMenuSystem.Instancia.Abierto;
            if (menuAbierto) { im.SetGamepadInputs(true, 0f, 0f, false, false, false, false); return; }

            Vector2 stick = g.leftStick.ReadValue();
            float steer = ConZonaMuerta(stick.x);
            float avance = ConZonaMuerta(stick.y);
            if (g.rightTrigger.ReadValue() > 0.3f) avance = Mathf.Max(avance, 1f);
            if (g.leftTrigger.ReadValue() > 0.3f) avance = Mathf.Min(avance, -1f);

            bool correr = g.rightShoulder.isPressed || g.leftStickButton.isPressed;
            float throttle = avance > 0f ? (correr ? avance : avance * factorCaminar) : avance;

            bool frenar = BotonAtras(g).isPressed;
            bool saltar = BotonAceptar(g).wasPressedThisFrame;
            bool rugir = BotonRugir(g).wasPressedThisFrame;
            im.SetGamepadInputs(true, steer, throttle, correr && avance > 0f, frenar, saltar, rugir);
        }

        float ConZonaMuerta(float v)
        {
            float a = Mathf.Abs(v);
            if (a < zonaMuerta) return 0f;
            return Mathf.Sign(v) * Mathf.Clamp01((a - zonaMuerta) / (1f - zonaMuerta));
        }
    }
}
