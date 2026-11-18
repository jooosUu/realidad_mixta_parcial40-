using System;
using UnityEngine;

namespace DinoVehicle
{
    public enum InputSource
    {
        KeyboardMouse,
        ArduinoSerial,
        /// <summary>Teclado + Arduino + manos (MediaPipe), combinados sin pisarse.</summary>
        BothCombined,
        /// <summary>Solo manos con MediaPipe (Tools/mediapipe_hand_controller.py por UDP 5055).</summary>
        MediaPipeHands
    }

    /// <summary>
    /// Administra las entradas del dinosaurio: teclado (pruebas), Arduino por puerto serie
    /// y manos con MediaPipe (MediaPipeHandReceiver por UDP).
    /// En BothCombined funcionan todas a la vez: el teclado/ratón tiene prioridad en el eje que esté
    /// usando y las manos controlan lo demás; entre teclado y Arduino manda el de más intensidad.
    /// Freno, salto y rugido se suman (OR). Salto y rugido duran un solo frame por gesto.
    /// </summary>
    public class DinoInputManager : MonoBehaviour
    {
        public static DinoInputManager Instance { get; private set; }

        [Header("Modo de Entrada")]
        [Tooltip("Selecciona si recibes datos del teclado, de Arduino o de ambos")]
        public InputSource activeSource = InputSource.BothCombined;

        [Header("Valores de Control (Lectura)")]
        [Range(-1f, 1f)] public float steering = 0f;    // -1: Izquierda máxima, +1: Derecha máxima
        [Range(-1f, 1f)] public float throttle = 0f;    // -1: Reversa, 0: Parado, +1: Aceleración máxima
        public bool isBraking = false;
        public bool isSprinting = false;                // Correr / Sprint (Shift)                  // Freno
        public bool jumpTriggered = false;              // Salto / Pisotón
        public bool roarTriggered = false;              // Rugido / Turbo

        [Header("Configuración Teclado")]
        public KeyCode keyBrake = KeyCode.Space;
        public KeyCode keyJump = KeyCode.F;
        public KeyCode keyRoar = KeyCode.E;
        public KeyCode keyTurbo = KeyCode.LeftShift;

        [Header("Suavizado de Entrada")]
        public float steeringSensitivity = 4f;
        public float throttleSensitivity = 3f;

        // Entradas crudas desde Arduino (actualizadas por ArduinoSerialConnector)
        [NonSerialized] public float arduinoSteering = 0f;
        [NonSerialized] public float arduinoThrottle = 0f;
        [NonSerialized] public bool arduinoJump = false;
        [NonSerialized] public bool arduinoRoar = false;
        [NonSerialized] public bool arduinoConnected = false;

        // Entradas desde las manos (actualizadas por MediaPipeHandReceiver)
        [NonSerialized] public float handSteering = 0f;
        [NonSerialized] public float handThrottle = 0f;
        [NonSerialized] public bool handSprint = false;
        [NonSerialized] public bool handBrake = false;
        [NonSerialized] public bool handJump = false;
        [NonSerialized] public bool handRoar = false;
        [NonSerialized] public bool handConnected = false;
        [NonSerialized] public Vector2 handCursor = new Vector2(0.5f, 0.5f);
        [NonSerialized] public bool handPinch = false;

        // Entradas del mando de Switch / gamepad (actualizadas por DinoGamepadConnector)
        [NonSerialized] public bool padConnected = false;
        [NonSerialized] public float padSteering = 0f;
        [NonSerialized] public float padThrottle = 0f;
        [NonSerialized] public bool padSprint = false;
        [NonSerialized] public bool padBrake = false;
        [NonSerialized] public bool padJump = false;
        [NonSerialized] public bool padRoar = false;

        private float targetSteering = 0f;
        private float targetThrottle = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // El receptor de manos se crea solo; esto cubre escenas cargadas sin él.
                if (MediaPipeHandReceiver.Instance == null)
                {
                    var go = new GameObject("MediaPipe · Receptor de manos");
                    DontDestroyOnLoad(go);
                    go.AddComponent<MediaPipeHandReceiver>();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            jumpTriggered = false;
            roarTriggered = false;

            float kbSteer = 0f;
            float kbThrottle = 0f;
            bool kbBrake = false;
            bool sprint = false;

            // 1. Procesar Teclado
            if (activeSource == InputSource.KeyboardMouse || activeSource == InputSource.BothCombined)
            {
                kbSteer = Input.GetAxis("Horizontal"); // A/D o Flechas
                float rawVertical = Input.GetAxis("Vertical"); // W/S o Flechas

                sprint = Input.GetKey(keyTurbo);

                if (rawVertical > 0.05f)
                {
                    // W solo: paso firme y caminata (~14-18 km/h)
                    // W + Shift: carrera a fondo (~58-65 km/h)
                    kbThrottle = sprint ? rawVertical : (rawVertical * 0.45f);
                }
                else
                {
                    kbThrottle = rawVertical;
                }

                kbBrake = Input.GetKey(keyBrake);

                if (Input.GetKeyDown(keyJump))
                {
                    jumpTriggered = true;
                }

                if (Input.GetKeyDown(keyRoar) || Input.GetMouseButtonDown(0))
                {
                    roarTriggered = true;
                }
            }

            // 2. Procesar Arduino
            if ((activeSource == InputSource.ArduinoSerial || activeSource == InputSource.BothCombined) && arduinoConnected)
            {
                kbSteer = MayorMagnitud(kbSteer, arduinoSteering);
                kbThrottle = MayorMagnitud(kbThrottle, arduinoThrottle);
                if (arduinoJump) { jumpTriggered = true; arduinoJump = false; }
                if (arduinoRoar) { roarTriggered = true; arduinoRoar = false; }
            }

            // 3. Procesar manos (MediaPipe)
            if (activeSource == InputSource.MediaPipeHands || activeSource == InputSource.BothCombined)
            {
                if (handConnected)
                {
                    // Teclado/ratón primero; las manos mandan en cada eje que el teclado no está usando.
                    if (Mathf.Abs(kbSteer) < 0.05f) kbSteer = handSteering;
                    if (Mathf.Abs(kbThrottle) < 0.05f)
                    {
                        kbThrottle = handThrottle;
                        sprint |= handSprint;
                    }
                    kbBrake |= handBrake;
                }
                // Los gestos se consumen siempre (por flanco) para que no se disparen tarde.
                if (handJump) { if (handConnected) jumpTriggered = true; handJump = false; }
                if (handRoar) { if (handConnected) roarTriggered = true; handRoar = false; }
            }
            else
            {
                handJump = handRoar = false;
            }

            // 4. Mando de Switch / gamepad: va con el teclado (KeyboardMouse) y en el modo combinado.
            if ((activeSource == InputSource.KeyboardMouse || activeSource == InputSource.BothCombined) && padConnected)
            {
                if (Mathf.Abs(padSteering) > Mathf.Abs(kbSteer)) kbSteer = padSteering;
                if (Mathf.Abs(padThrottle) > Mathf.Abs(kbThrottle)) { kbThrottle = padThrottle; sprint |= padSprint; }
                kbBrake |= padBrake;
                if (padJump) jumpTriggered = true;
                if (padRoar) roarTriggered = true;
            }
            padJump = padRoar = false;

            targetSteering = Mathf.Clamp(kbSteer, -1f, 1f);
            targetThrottle = Mathf.Clamp(kbThrottle, -1f, 1.5f);
            isBraking = kbBrake;
            isSprinting = sprint;

            // Suavizado para simular la inercia del volante y pedal
            steering = Mathf.MoveTowards(steering, targetSteering, Time.deltaTime * steeringSensitivity);
            throttle = Mathf.MoveTowards(throttle, targetThrottle, Time.deltaTime * throttleSensitivity);
        }

        /// <summary>La fuente con más intensidad manda; así ninguna pisa a otra que está quieta.</summary>
        static float MayorMagnitud(float a, float b) => Mathf.Abs(b) > Mathf.Abs(a) ? b : a;

        public void SetGamepadInputs(bool connected, float steerVal, float throttleVal, bool sprintVal, bool brakeVal,
            bool jumpBtn, bool roarBtn)
        {
            padConnected = connected;
            padSteering = steerVal;
            padThrottle = throttleVal;
            padSprint = sprintVal;
            padBrake = brakeVal;
            if (jumpBtn) padJump = true;
            if (roarBtn) padRoar = true;
        }

        public void SetArduinoInputs(float steerVal, float throttleVal, bool jumpBtn, bool roarBtn)
        {
            arduinoSteering = steerVal;
            arduinoThrottle = throttleVal;
            if (jumpBtn) arduinoJump = true;
            if (roarBtn) arduinoRoar = true;
            arduinoConnected = true;
        }
    }
}
