using UnityEngine;

namespace DinoVehicle
{
    public class DinoHUD : MonoBehaviour
    {
        public DinoVehicleController vehicle;
        public DinoInputManager input;
        public ArduinoSerialConnector arduino;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle alertStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized = false;

        private void Start()
        {
            if (vehicle == null) vehicle = FindFirstObjectByType<DinoVehicleController>();
            if (input == null) input = DinoInputManager.Instance;
            if (arduino == null) arduino = FindFirstObjectByType<ArduinoSerialConnector>();
        }

        private void Update()
        {
            // Tecla R para reiniciar posición si se cae o se traba
            if (Input.GetKeyDown(KeyCode.R) && vehicle != null)
            {
                vehicle.ResetToSpawn();
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Normal
            };
            labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

            alertStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            alertStyle.normal.textColor = new Color(1f, 0.4f, 0.1f, 1f);

            boxStyle = new GUIStyle(GUI.skin.box);

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // Panel superior izquierdo: Velocímetro y Estado
            GUILayout.BeginArea(new Rect(20, 20, 360, 260), boxStyle);
            GUILayout.Space(8);

            float speed = vehicle != null ? vehicle.CurrentSpeedKmh : 0f;
            GUILayout.Label($"?? CARNOTAURUS VEHICLE", titleStyle);
            GUILayout.Space(4);

            GUI.color = Color.cyan;
            GUILayout.Label($"Velocidad: {speed:0.0} km/h", titleStyle);
            GUI.color = Color.white;

            // Barra de progreso de velocidad
            float speedRatio = vehicle != null ? vehicle.SpeedRatio : 0f;
            Rect barRect = GUILayoutUtility.GetRect(340, 16);
            GUI.Box(barRect, "");
            GUI.color = vehicle != null && vehicle.IsBoosting ? Color.red : Color.green;
            GUI.Box(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(speedRatio), barRect.height), "");
            GUI.color = Color.white;

            GUILayout.Space(6);
            if (vehicle != null)
            {
                if (vehicle.IsBoosting)
                {
                    GUILayout.Label("? MODO BESTIA: TURBO ACTIVO !", alertStyle);
                }
                else if (vehicle.IsGrounded)
                {
                    string status = Mathf.Abs(speed) < 1f ? "Detenido / Alerta" : (speed > 25f ? "Galope Rápido" : "Marcha");
                    GUILayout.Label($"Estado: {status}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Estado: En el aire / Salto", labelStyle);
                }
            }

            GUILayout.Space(8);
            // Estado de Arduino
            bool ardConn = (arduino != null && arduino.IsConnected) || (input != null && input.arduinoConnected);
            if (ardConn)
            {
                GUI.color = Color.green;
                GUILayout.Label($"? ARDUINO: CONECTADO ({arduino?.portName})", labelStyle);
            }
            else
            {
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUILayout.Label("? ARDUINO: Desconectado (Modo Teclado Activo)", labelStyle);
            }
            GUI.color = Color.white;

            GUILayout.EndArea();

            // Panel superior derecho: Controles de Teclado
            GUILayout.BeginArea(new Rect(Screen.width - 340, 20, 320, 220), boxStyle);
            GUILayout.Label("?? CONTROLES (PRUEBAS)", titleStyle);
            GUILayout.Space(6);
            GUILayout.Label("• W / S o Flechas: Acelerar / Reversa", labelStyle);
            GUILayout.Label("• A / D o Flechas: Volante (Izquierda / Derecha)", labelStyle);
            GUILayout.Label("• ESPACIO: Frenar", labelStyle);
            GUILayout.Label("• F: Saltar / Pisotón", labelStyle);
            GUILayout.Label("• E o Click Izq: Rugir / Turbo (Adrenalina)", labelStyle);
            GUILayout.Label("• Left Shift: Sprint continuo", labelStyle);
            GUILayout.Label("• R: Reiniciar posición si vuelca", labelStyle);
            GUILayout.EndArea();
        }
    }
}
