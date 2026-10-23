using System;
using UnityEngine;

namespace DinoVehicle
{
    public enum InputSource
    {
        KeyboardMouse,
        ArduinoSerial,
        BothCombined
    }

    /// <summary>
    /// Administra las entradas del dinosaurio: soporta tanto teclado (para pruebas)
    /// como puerto Serial desde Arduino cuando esté conectado físicamente.
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
        public bool isBraking = false;                  // Freno
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

        private float targetSteering = 0f;
        private float targetThrottle = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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

            // 1. Procesar Teclado
            if (activeSource == InputSource.KeyboardMouse || activeSource == InputSource.BothCombined)
            {
                kbSteer = Input.GetAxis("Horizontal"); // A/D o Flechas
                kbThrottle = Input.GetAxis("Vertical");   // W/S o Flechas

                if (Input.GetKey(keyTurbo) && kbThrottle > 0.1f)
                {
                    kbThrottle = 1.35f; // Turbo boost con Shift
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
                if (Mathf.Abs(arduinoSteering) > 0.05f) kbSteer = arduinoSteering;
                if (Mathf.Abs(arduinoThrottle) > 0.05f) kbThrottle = arduinoThrottle;
                if (arduinoJump) { jumpTriggered = true; arduinoJump = false; }
                if (arduinoRoar) { roarTriggered = true; arduinoRoar = false; }
            }

            targetSteering = Mathf.Clamp(kbSteer, -1f, 1f);
            targetThrottle = Mathf.Clamp(kbThrottle, -1f, 1.5f);
            isBraking = kbBrake;

            // Suavizado para simular la inercia del volante y pedal
            steering = Mathf.MoveTowards(steering, targetSteering, Time.deltaTime * steeringSensitivity);
            throttle = Mathf.MoveTowards(throttle, targetThrottle, Time.deltaTime * throttleSensitivity);
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
