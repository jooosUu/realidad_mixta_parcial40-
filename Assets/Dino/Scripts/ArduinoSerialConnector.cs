using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Manejador de comunicación Serial con Arduino.
    /// Corre en un subproceso (Thread) independiente para evitar cualquier tirón o congelamiento de frames en Unity.
    /// </summary>
    public class ArduinoSerialConnector : MonoBehaviour
    {
        [Header("Configuración del Puerto")]
        [Tooltip("Nombre del puerto COM donde esté conectado el Arduino (ej. COM3, COM4, COM5)")]
        public string portName = "COM3";
        [Tooltip("Velocidad en baudios (debe coincidir con Serial.begin en Arduino)")]
        public int baudRate = 115200;
        [Tooltip("Reintentar conexión automáticamente si no está conectado")]
        public bool autoConnect = true;

        [Header("Mapeo de Valores Arduino")]
        [Tooltip("Marcar si el Arduino envía valores analógicos crudos de 0 a 1023 (potenciómetros normales)")]
        public bool expectsRawPotValues = true;
        [Tooltip("Zona muerta para evitar temblores del potenciómetro")]
        public float deadzone = 0.05f;

        public bool IsConnected => serialPort != null && serialPort.IsOpen;

        private SerialPort serialPort;
        private Thread readThread;
        private bool isRunning = false;

        // Variables recibidas en el hilo
        private float receivedSteer = 0f;
        private float receivedThrottle = 0f;
        private bool receivedJump = false;
        private bool receivedRoar = false;

        private void Start()
        {
            if (autoConnect)
            {
                TryOpenPort();
            }
        }

        public void TryOpenPort()
        {
            if (IsConnected) return;

            try
            {
                // Verificar si el puerto existe en el sistema
                string[] availablePorts = SerialPort.GetPortNames();
                bool portFound = false;
                foreach (string p in availablePorts)
                {
                    if (p.Equals(portName, StringComparison.OrdinalIgnoreCase))
                    {
                        portFound = true;
                        break;
                    }
                }

                if (!portFound)
                {
                    Debug.Log($"[ArduinoSerial] Puerto {portName} no detectado actualmente. Usando modo de simulación por teclado.");
                    return;
                }

                serialPort = new SerialPort(portName, baudRate);
                serialPort.ReadTimeout = 1000;
                serialPort.WriteTimeout = 1000;
                serialPort.Open();

                isRunning = true;
                readThread = new Thread(ReadSerialLoop);
                readThread.IsBackground = true;
                readThread.Start();

                Debug.Log($"[ArduinoSerial] ? Conectado exitosamente al puerto {portName} a {baudRate} baudios.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ArduinoSerial] No se pudo abrir {portName}: {ex.Message}. Manteniendo modo teclado.");
                serialPort = null;
            }
        }

        private void ReadSerialLoop()
        {
            while (isRunning && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    string line = serialPort.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        ParseLine(line.Trim());
                    }
                }
                catch (TimeoutException)
                {
                    // Normal si no hay datos en ese milisegundo
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        private void ParseLine(string line)
        {
            // Formato esperado de Arduino: "Steer,Throttle,JumpBtn,RoarBtn"
            // Ej: "512,1023,0,0" (analógicos 0-1023) o "0.0,1.0,0,0" (normalizados)
            string[] parts = line.Split(',');
            if (parts.Length >= 2)
            {
                float rawSteer = 0f;
                float rawThrottle = 0f;
                int rawJump = 0;
                int rawRoar = 0;

                float.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rawSteer);
                float.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rawThrottle);

                if (parts.Length > 2) int.TryParse(parts[2], out rawJump);
                if (parts.Length > 3) int.TryParse(parts[3], out rawRoar);

                float finalSteer;
                float finalThrottle;

                if (expectsRawPotValues)
                {
                    // Potenciómetro dirección: centro en 512 (0 a 1023 -> -1.0 a 1.0)
                    finalSteer = Mathf.Clamp((rawSteer - 512f) / 512f, -1f, 1f);
                    // Potenciómetro acelerador: 0 a 1023 -> 0.0 a 1.0 (o pedal)
                    finalThrottle = Mathf.Clamp(rawThrottle / 1023f, 0f, 1f);
                }
                else
                {
                    finalSteer = Mathf.Clamp(rawSteer, -1f, 1f);
                    finalThrottle = Mathf.Clamp(rawThrottle, -1f, 1f);
                }

                // Zona muerta
                if (Mathf.Abs(finalSteer) < deadzone) finalSteer = 0f;
                if (Mathf.Abs(finalThrottle) < deadzone) finalThrottle = 0f;

                receivedSteer = finalSteer;
                receivedThrottle = finalThrottle;
                if (rawJump == 1) receivedJump = true;
                if (rawRoar == 1) receivedRoar = true;
            }
        }

        private void Update()
        {
            if (IsConnected && DinoInputManager.Instance != null)
            {
                DinoInputManager.Instance.SetArduinoInputs(receivedSteer, receivedThrottle, receivedJump, receivedRoar);
                receivedJump = false;
                receivedRoar = false;
            }
        }

        private void OnDisable()
        {
            ClosePort();
        }

        private void OnApplicationQuit()
        {
            ClosePort();
        }

        public void ClosePort()
        {
            isRunning = false;
            if (readThread != null && readThread.IsAlive)
            {
                readThread.Abort();
            }

            if (serialPort != null && serialPort.IsOpen)
            {
                try { serialPort.Close(); } catch { }
            }
            serialPort = null;
        }
    }
}
