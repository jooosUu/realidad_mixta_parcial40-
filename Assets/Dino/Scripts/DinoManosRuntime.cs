using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DinoVehicle
{
    /// <summary>
    /// En el juego compilado (.exe) arranca solo el control por manos, igual que DinoManosLauncher en el editor.
    /// Busca la carpeta "Manos" junto al .exe (la copia DinoBuildRelease): mediapipe_hand_controller.py,
    /// requirements.txt y models/hand_landmarker.task. Necesita Python en el PC; si faltan opencv/mediapipe
    /// los instala con pip la primera vez. Si no hay Python, el juego sigue con teclado, ratón y mando.
    /// Al cerrar el juego se cierra también el Python. La salida queda en Manos/manos_python.log.
    /// </summary>
    public static class DinoManosRuntime
    {
        static Process proceso;
        static int arrancando;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AlArrancar()
        {
            if (Application.isEditor) return; // en el editor lo lanza DinoManosLauncher
            Application.quitting += Detener;
            Iniciar();
        }

        static string Carpeta => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Manos"));

        public static void Iniciar()
        {
            string script = Path.Combine(Carpeta, "mediapipe_hand_controller.py");
            if (!File.Exists(script))
            {
                Debug.LogWarning($"[Manos] No está {script}: el control por manos queda apagado.");
                return;
            }
            if (Interlocked.Exchange(ref arrancando, 1) == 1) return;
            new Thread(() =>
            {
                try { Preparar(script); }
                catch (Exception e) { Debug.LogWarning($"[Manos] No se pudo iniciar: {e.Message}"); }
                finally { Interlocked.Exchange(ref arrancando, 0); }
            }) { IsBackground = true, Name = "DinoManosRuntime" }.Start();
        }

        static void Preparar(string script)
        {
            string log = Path.Combine(Carpeta, "manos_python.log");
            var (exe, prefijo) = BuscarPython(log);
            if (exe == null)
            {
                Debug.LogWarning("[Manos] No se encontró Python (python / py). El juego sigue con teclado, ratón y mando.");
                return;
            }
            if (Ejecutar(exe, prefijo + "-c \"import cv2, mediapipe\"", 60000, log) != 0)
            {
                string req = Path.Combine(Carpeta, "requirements.txt");
                int r = Ejecutar(exe, $"{prefijo}-m pip install -r \"{req}\"", 900000, log);
                if (r != 0) Ejecutar(exe, $"{prefijo}-m pip install --user -r \"{req}\"", 900000, log);
            }

            var psi = new ProcessStartInfo(exe, $"{prefijo}-u \"{script}\"")
            {
                WorkingDirectory = Carpeta,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var escritor = new StreamWriter(log, append: true) { AutoFlush = true };
            object candado = new object();
            void Escribir(string linea) { if (linea == null) return; lock (candado) escritor.WriteLine(linea); }
            p.OutputDataReceived += (_, e) => Escribir(e.Data);
            p.ErrorDataReceived += (_, e) => Escribir(e.Data);
            p.Exited += (_, __) => { lock (candado) escritor.Dispose(); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            proceso = p;
            Debug.Log("[Manos] Control por manos iniciado.");
        }

        static (string, string) BuscarPython(string log)
        {
            if (Ejecutar("python", "--version", 10000, log) == 0) return ("python", "");
            if (Ejecutar("py", "-3 --version", 10000, log) == 0) return ("py", "-3 ");
            return (null, null);
        }

        static int Ejecutar(string exe, string args, int timeoutMs, string log)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = Carpeta,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var p = Process.Start(psi);
                var salida = p.StandardOutput.ReadToEndAsync();
                var error = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch (Exception) { }
                    return -1;
                }
                try { File.AppendAllText(log, $"> {exe} {args}\n{salida.Result}{error.Result}\n"); } catch (Exception) { }
                return p.ExitCode;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        static void Detener()
        {
            var p = proceso;
            proceso = null;
            if (p == null) return;
            try
            {
                if (p.HasExited) return;
                using var k = Process.Start(new ProcessStartInfo("taskkill", $"/PID {p.Id} /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                k?.WaitForExit(3000);
            }
            catch (Exception) { }
        }
    }
}
