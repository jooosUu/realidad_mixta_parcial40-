using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Arranca solo el control por manos (Tools/mediapipe_hand_controller.py) al abrir el proyecto:
    /// 1. Busca Python (python o py -3).
    /// 2. Si faltan opencv/mediapipe, ejecuta "pip install -r Tools/requirements.txt" en segundo plano.
    /// 3. Lanza el script (abre la ventana de preview de la webcam) y lo cierra al cerrar Unity.
    /// Si se cierra la ventana de preview, se vuelve a lanzar al darle a Play.
    /// La cámara se elige la primera vez (o con DINO WEST ▸ Control por manos ▸ Elegir cámara) y se recuerda
    /// en Tools/camara_elegida.json; en el preview, la tecla C cambia de cámara.
    /// Menú: DINO WEST ▸ Control por manos. La salida de Python queda en Logs/manos_python.log.
    /// </summary>
    [InitializeOnLoad]
    public static class DinoManosLauncher
    {
        const string PrefAuto = "DinoWest_ManosAutoInicio";
        const string MenuAuto = "DINO WEST/Control por manos/Iniciar automáticamente";

        static readonly string Raiz = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        static readonly string Script = Path.Combine(Raiz, "Tools", "mediapipe_hand_controller.py");
        static readonly string Requisitos = Path.Combine(Raiz, "Tools", "requirements.txt");
        static readonly string LogPython = Path.Combine(Raiz, "Logs", "manos_python.log");
        // En Temp/: Unity lo borra al cerrarse, así un PID viejo nunca bloquea el arranque.
        static readonly string ArchivoPid = Path.Combine(Raiz, "Temp", "manos.pid");

        static int LeerPid()
        {
            try { return File.Exists(ArchivoPid) && int.TryParse(File.ReadAllText(ArchivoPid), out int pid) ? pid : 0; }
            catch (Exception) { return 0; }
        }

        static void GuardarPid(int pid)
        {
            try
            {
                if (pid == 0) { if (File.Exists(ArchivoPid)) File.Delete(ArchivoPid); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(ArchivoPid));
                File.WriteAllText(ArchivoPid, pid.ToString());
            }
            catch (Exception) { }
        }

        static int arrancando; // 1 mientras un hilo está preparando/lanzando (evita dobles lanzamientos)

        static bool AutoInicio
        {
            get => EditorPrefs.GetBool(PrefAuto, true);
            set => EditorPrefs.SetBool(PrefAuto, value);
        }

        static DinoManosLauncher()
        {
            // Las verificaciones por consola (batchmode) no deben abrir la webcam.
            if (UnityEngine.Application.isBatchMode) return;

            EditorApplication.quitting += Detener;
            EditorApplication.playModeStateChanged += estado =>
            {
                if (estado == PlayModeStateChange.ExitingEditMode && AutoInicio && !EstaCorriendo()) Iniciar(false);
            };

            // El constructor se repite en cada recompilación: solo se lanza si no hay uno vivo.
            if (AutoInicio && !EstaCorriendo()) EditorApplication.delayCall += () => Iniciar(false);
        }

        [MenuItem("DINO WEST/Control por manos/Iniciar", priority = 1)]
        static void IniciarMenu() => Iniciar(true);

        [MenuItem("DINO WEST/Control por manos/Detener", priority = 2)]
        static void DetenerMenu()
        {
            Detener();
            Debug.Log("[Manos] Control por manos detenido.");
        }

        [MenuItem("DINO WEST/Control por manos/Elegir cámara...", priority = 3)]
        static void ElegirCamaraMenu()
        {
            Detener();
            Iniciar(true, argsExtra: "--elegir");
            Debug.Log("[Manos] Elige la cámara en la ventana que se abre (número o clic). Se recordará la próxima vez.");
        }

        [MenuItem("DINO WEST/Control por manos/Reinstalar dependencias de Python", priority = 20)]
        static void ReinstalarMenu()
        {
            Detener();
            Iniciar(true, forzarInstalacion: true);
        }

        [MenuItem(MenuAuto, priority = 40)]
        static void AlternarAuto()
        {
            AutoInicio = !AutoInicio;
            Debug.Log($"[Manos] Inicio automático {(AutoInicio ? "activado" : "desactivado")}.");
        }

        [MenuItem(MenuAuto, true)]
        static bool AlternarAutoValidar()
        {
            Menu.SetChecked(MenuAuto, AutoInicio);
            return true;
        }

        public static bool EstaCorriendo()
        {
            int pid = LeerPid();
            if (pid == 0) return false;
            try
            {
                var p = Process.GetProcessById(pid);
                return !p.HasExited && p.ProcessName.IndexOf("py", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Iniciar(bool avisarSiYaCorre, bool forzarInstalacion = false, string argsExtra = "")
        {
            if (EstaCorriendo())
            {
                if (avisarSiYaCorre) Debug.Log("[Manos] El control por manos ya está en marcha.");
                return;
            }
            if (!File.Exists(Script))
            {
                Debug.LogWarning($"[Manos] No se encuentra {Script}");
                return;
            }
            if (Interlocked.Exchange(ref arrancando, 1) == 1) return;

            // Todo lo lento (buscar Python, pip) va en un hilo para no congelar el editor.
            new Thread(() =>
            {
                try { Preparar(forzarInstalacion, argsExtra); }
                catch (Exception e) { Debug.LogWarning($"[Manos] No se pudo iniciar el control por manos: {e.Message}"); }
                finally { Interlocked.Exchange(ref arrancando, 0); }
            }) { IsBackground = true, Name = "DinoManosLauncher" }.Start();
        }

        static void Preparar(bool forzarInstalacion, string argsExtra)
        {
            var (exe, prefijo) = BuscarPython();
            if (exe == null)
            {
                Debug.LogWarning("[Manos] No se encontró Python en el PATH (python / py). Instálalo y reinicia Unity.");
                return;
            }

            bool listo = !forzarInstalacion && Ejecutar(exe, prefijo + "-c \"import cv2, mediapipe\"", 60000) == 0;
            if (!listo)
            {
                Debug.Log("[Manos] Instalando dependencias de Python (opencv-python, mediapipe)... puede tardar unos minutos la primera vez.");
                int r = Ejecutar(exe, $"{prefijo}-m pip install -r \"{Requisitos}\"", 900000);
                if (r != 0) r = Ejecutar(exe, $"{prefijo}-m pip install --user -r \"{Requisitos}\"", 900000);
                if (r != 0 || Ejecutar(exe, prefijo + "-c \"import cv2, mediapipe\"", 60000) != 0)
                {
                    Debug.LogWarning($"[Manos] No se pudieron instalar opencv/mediapipe (detalle en {LogPython}). El teclado y el ratón siguen funcionando.");
                    return;
                }
                Debug.Log("<color=green>[Manos]</color> Dependencias instaladas.");
            }

            var psi = new ProcessStartInfo(exe, $"{prefijo}-u \"{Script}\" {argsExtra}")
            {
                WorkingDirectory = Raiz,
                UseShellExecute = false,
                CreateNoWindow = true,          // sin consola; la ventana de la webcam sí aparece
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var proceso = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var escritor = new StreamWriter(LogPython, append: false) { AutoFlush = true };
            object candado = new object();
            void Escribir(string linea) { if (linea == null) return; lock (candado) escritor.WriteLine(linea); }
            proceso.OutputDataReceived += (_, e) => Escribir(e.Data);
            proceso.ErrorDataReceived += (_, e) => Escribir(e.Data);
            proceso.Exited += (_, __) =>
            {
                lock (candado) escritor.Dispose();
                Debug.Log($"[Manos] El control por manos se cerró (código {SafeExit(proceso)}). Se vuelve a abrir al darle a Play o desde DINO WEST ▸ Control por manos ▸ Iniciar.");
            };

            proceso.Start();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();
            GuardarPid(proceso.Id);
            Debug.Log("<color=cyan>[Manos]</color> Control por manos iniciado (webcam -> UDP 127.0.0.1:5055). Teclado y ratón siguen activos.");
        }

        static int SafeExit(Process p)
        {
            try { return p.ExitCode; } catch (Exception) { return -1; }
        }

        static (string exe, string prefijo) BuscarPython()
        {
            if (Ejecutar("python", "--version", 10000) == 0) return ("python", "");
            if (Ejecutar("py", "-3 --version", 10000) == 0) return ("py", "-3 ");
            return (null, null);
        }

        /// <summary>Ejecuta un comando oculto, añade su salida al log y devuelve el código de salida (-1 si no se pudo).</summary>
        static int Ejecutar(string exe, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = Raiz,
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
                try
                {
                    File.AppendAllText(LogPython, $"> {exe} {args}\n{salida.Result}{error.Result}\n");
                }
                catch (Exception) { }
                return p.ExitCode;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static void Detener()
        {
            int pid = LeerPid();
            GuardarPid(0);
            if (pid == 0) return;
            try
            {
                // Cierra también los procesos hijos de Python.
                using var k = Process.Start(new ProcessStartInfo("taskkill", $"/PID {pid} /T /F")
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
