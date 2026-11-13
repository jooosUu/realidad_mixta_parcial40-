using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Parámetros de la simulación que se ajustan desde el menú de Ajustes.
    /// Se guardan en PlayerPrefs. Los sistemas que aún no existen (fruta, bandadas,
    /// fragmentos, pelota, vectores) pueden leer estos valores cuando se implementen.
    /// </summary>
    public static class SimulationSettings
    {
        public enum Agresividad { Pacifica, Territorial, Hostil }

        // ---- Mundo ----
        public static float TimeScale = 1f;            // 0.25 – 2
        public static float Gravedad = 9.81f;          // 1 – 20 m/s²
        public static float MasaDino = 1200f;          // 400 – 3000 kg
        // ---- Ecosistema ----
        public static float ArbolesConFruto = 0.3f;    // 0 – 1
        public static int PoblacionPajaros = 34;       // 0 – 60
        public static Agresividad AgresividadBandadas = Agresividad.Territorial;
        // ---- Física ----
        public static bool DestruccionFragmentos = true;
        public static float RebotePelota = 0.75f;      // 0 – 1
        public static bool MostrarVectores = false;
        // ---- Video / audio ----
        public static float Volumen = 1f;
        public static bool PantallaCompleta = true;
        public static int Calidad = -1;                // -1 = la actual

        public static event System.Action Cambiaron;

        static bool cargado;

        public static void Cargar()
        {
            if (cargado) return;
            cargado = true;
            TimeScale = PlayerPrefs.GetFloat("dw.timeScale", 1f);
            Gravedad = PlayerPrefs.GetFloat("dw.gravedad", 9.81f);
            MasaDino = PlayerPrefs.GetFloat("dw.masa", 1200f);
            ArbolesConFruto = PlayerPrefs.GetFloat("dw.fruto", 0.3f);
            PoblacionPajaros = PlayerPrefs.GetInt("dw.pajaros", 34);
            AgresividadBandadas = (Agresividad)PlayerPrefs.GetInt("dw.agresividad", 1);
            DestruccionFragmentos = PlayerPrefs.GetInt("dw.fragmentos", 1) == 1;
            RebotePelota = PlayerPrefs.GetFloat("dw.rebote", 0.75f);
            MostrarVectores = PlayerPrefs.GetInt("dw.vectores", 0) == 1;
            Volumen = PlayerPrefs.GetFloat("dw.volumen", 1f);
            PantallaCompleta = PlayerPrefs.GetInt("dw.fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            Calidad = PlayerPrefs.GetInt("dw.calidad", QualitySettings.GetQualityLevel());
        }

        public static void Guardar()
        {
            PlayerPrefs.SetFloat("dw.timeScale", TimeScale);
            PlayerPrefs.SetFloat("dw.gravedad", Gravedad);
            PlayerPrefs.SetFloat("dw.masa", MasaDino);
            PlayerPrefs.SetFloat("dw.fruto", ArbolesConFruto);
            PlayerPrefs.SetInt("dw.pajaros", PoblacionPajaros);
            PlayerPrefs.SetInt("dw.agresividad", (int)AgresividadBandadas);
            PlayerPrefs.SetInt("dw.fragmentos", DestruccionFragmentos ? 1 : 0);
            PlayerPrefs.SetFloat("dw.rebote", RebotePelota);
            PlayerPrefs.SetInt("dw.vectores", MostrarVectores ? 1 : 0);
            PlayerPrefs.SetFloat("dw.volumen", Volumen);
            PlayerPrefs.SetInt("dw.fullscreen", PantallaCompleta ? 1 : 0);
            PlayerPrefs.SetInt("dw.calidad", Calidad);
            PlayerPrefs.Save();
        }

        public static void RestablecerSimulacion()
        {
            TimeScale = 1f; Gravedad = 9.81f; MasaDino = 1200f;
            ArbolesConFruto = 0.3f; PoblacionPajaros = 34; AgresividadBandadas = Agresividad.Territorial;
            DestruccionFragmentos = true; RebotePelota = 0.75f; MostrarVectores = false;
        }

        /// <summary>Aplica al mundo lo que se puede aplicar sin pausa (gravedad, masa, audio, video).</summary>
        public static void AplicarMundo()
        {
            Physics.gravity = new Vector3(0f, -Gravedad, 0f);
            foreach (var v in Object.FindObjectsByType<DinoVehicleController>(FindObjectsSortMode.None))
            {
                var rb = v.GetComponent<Rigidbody>();
                if (rb != null) rb.mass = MasaDino;
            }
            AudioListener.volume = Volumen;
            if (Screen.fullScreen != PantallaCompleta) Screen.fullScreen = PantallaCompleta;
            if (Calidad >= 0 && Calidad != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(Calidad, true);
            Cambiaron?.Invoke();
        }
    }
}
