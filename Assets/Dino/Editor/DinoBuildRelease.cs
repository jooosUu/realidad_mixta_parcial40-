using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Build de Windows para el release: DINO WEST ▸ Build para release.
    /// Compila las escenas activas de Build Settings en Builds/DinoWest/DinoWest.exe y copia junto al .exe la carpeta
    /// "Manos" (script de Python, requirements y modelo de MediaPipe) que DinoManosRuntime lanza al abrir el juego.
    /// También se puede llamar en batch: -executeMethod DinoVehicle.Editor.DinoBuildRelease.Construir
    /// </summary>
    public static class DinoBuildRelease
    {
        const string Carpeta = "Builds/DinoWest";

        [MenuItem("DINO WEST/Build para release", priority = 200)]
        public static void Construir()
        {
            string raiz = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string destino = Path.Combine(raiz, Carpeta);
            if (Directory.Exists(destino)) Directory.Delete(destino, true);
            Directory.CreateDirectory(destino);

            var escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var opciones = new BuildPlayerOptions
            {
                scenes = escenas,
                locationPathName = Path.Combine(destino, "DinoWest.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            var r = reporte.summary;
            Debug.Log($"[Build] Resultado: {r.result}, errores: {r.totalErrors}, tamaño: {r.totalSize / (1024 * 1024)} MB, escenas: {string.Join(", ", escenas)}");
            if (r.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // Carpeta Manos junto al .exe
            string tools = Path.Combine(raiz, "Tools");
            string manos = Path.Combine(destino, "Manos");
            Directory.CreateDirectory(Path.Combine(manos, "models"));
            File.Copy(Path.Combine(tools, "mediapipe_hand_controller.py"), Path.Combine(manos, "mediapipe_hand_controller.py"), true);
            File.Copy(Path.Combine(tools, "requirements.txt"), Path.Combine(manos, "requirements.txt"), true);
            File.Copy(Path.Combine(tools, "models", "hand_landmarker.task"), Path.Combine(manos, "models", "hand_landmarker.task"), true);

            // Carpeta que Unity genera para depurar: no va en el release.
            foreach (var d in Directory.GetDirectories(destino, "*_BackUpThisFolder_ButDontShipItWithYourGame"))
                Directory.Delete(d, true);
            foreach (var d in Directory.GetDirectories(destino, "*_BurstDebugInformation_DoNotShip"))
                Directory.Delete(d, true);

            Debug.Log($"[Build] Listo en {destino}");
        }
    }
}
