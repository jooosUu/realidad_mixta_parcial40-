using System.IO;
using DinoVehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Importa el terreno+pasto volcánico (Blender) en Simulation 1.
    /// Si la escena está abierta y aún no tiene el volcán, lo importa solo al recompilar / abrir.
    /// </summary>
    public static class DinoVolcanSceneSetup
    {
        public const string EscenaSimulation1 = "Assets/Dino/Scenes/Simulation Vulkan.unity";
        public const string RutaFbx = "Assets/Dino/Models/Environment/Volcan/Escenario_Volcan_TerrenoPasto.fbx";
        public const string RutaGlb = "Assets/Dino/Models/Environment/Volcan/Escenario_Volcan_TerrenoPasto.glb";
        public const string NombreRoot = "Escenario_Volcan_Mixta";
        const string FlagSesion = "DinoVolcan_AutoImportHecho_v2";
        public const string EscenaSimulationBase = "Assets/Dino/Scenes/Simulation.unity";

        /// <summary>Ancho objetivo del plano en metros (también se usa al importar).</summary>
        public const float AnchoObjetivoMetros = 200f;
        /// <summary>Escala uniforme del Transform del plano (pedido: 200).</summary>
        public const float EscalaPlanoUniforme = 200f;

        // Auto-import DESACTIVADO: vaciaba Simulation 1 al guardar solo el volcán.
        // Usa el menú "Reconstruir Simulation 1…" en su lugar.

        public static void EscalarPlano200YAdaptar()
        {
            if (EditorSceneManager.GetActiveScene().path != EscenaSimulation1)
                EditorSceneManager.OpenScene(EscenaSimulation1);

            var volcan = BuscarVolcan();
            if (volcan == null)
            {
                EditorUtility.DisplayDialog("DINO WEST", "No hay Escenario_Volcan_Mixta en la escena.", "OK");
                return;
            }

            Undo.RecordObject(volcan.transform, "Escala plano 200");
            volcan.transform.localScale = Vector3.one * EscalaPlanoUniforme;

            // Centrar: XZ en 0, base (min Y) en 0
            Bounds b = CalcularBounds(volcan);
            Vector3 offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            volcan.transform.position += offset;
            b = CalcularBounds(volcan);

            // Collider en la malla visual
            foreach (var c in volcan.GetComponents<MeshCollider>())
                Object.DestroyImmediate(c);
            MeshFilter suelo = null;
            foreach (var mf in volcan.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("pasto") || n.Contains("hebra") || n.Contains("scatter")) continue;
                if (suelo == null || mf.sharedMesh.vertexCount > suelo.sharedMesh.vertexCount)
                    suelo = mf;
            }
            if (suelo != null)
            {
                var mc = suelo.GetComponent<MeshCollider>();
                if (mc == null) mc = Undo.AddComponent<MeshCollider>(suelo.gameObject);
                mc.sharedMesh = suelo.sharedMesh;
            }
            Physics.SyncTransforms();

            DinoVolcanMaterialsSetup.CrearMateriales();
            DinoVolcanMaterialsSetup.RepararEscena(mostrarDialogo: false);

            // Re-poblar y adaptar TODO a la nueva malla
            DinoVolcanEnvironmentSetup.AplicarTodoSilencioso();
            AsegurarJugadorSobreVolcan(volcan);
            AsegurarTreeImpactEnJugador();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = volcan;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorUtility.DisplayDialog(
                "DINO WEST",
                $"Plano escalado a {EscalaPlanoUniforme:0}.\n" +
                $"Tamaño mundo ≈ {b.size.x:0} × {b.size.y:0} × {b.size.z:0} m\n\n" +
                "Pasto, rocas, árboles y objetos de Simulation re-adaptados al relieve.",
                "OK");
        }

        public static void ImportarEnSimulation1()
        {
            ImportarCore(mostrarDialogo: true);
        }

        /// <summary>
        /// Recupera Simulation 1: copia Simulation.unity (fauna/luz/jugador) + volcán + ambiente.
        /// </summary>
        public static void ReconstruirDesdeSimulation()
        {
            if (!File.Exists(EscenaSimulationBase))
            {
                EditorUtility.DisplayDialog("DINO WEST", $"No encuentro:\n{EscenaSimulationBase}", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "DINO WEST — Reconstruir",
                    "Se va a SOBRESCRIBIR Simulation 1 con una copia de Simulation,\n" +
                    "luego importar el volcán y poblar pasto/árboles/rocas.\n\n" +
                    "¿Continuar?",
                    "Reconstruir", "Cancelar"))
                return;

            // Cerrar escena actual sin guardar basura vacía
            if (EditorSceneManager.GetActiveScene().path == EscenaSimulation1)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            File.Copy(EscenaSimulationBase, EscenaSimulation1, overwrite: true);
            AssetDatabase.Refresh();

            var escena = EditorSceneManager.OpenScene(EscenaSimulation1, OpenSceneMode.Single);
            ImportarCore(mostrarDialogo: false);

            // Ambiente completo (pasto texturizado, árboles, rocas, niebla)
            DinoVolcanEnvironmentSetup.AplicarTodoSilencioso();

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);

            EditorUtility.DisplayDialog(
                "DINO WEST",
                "Simulation 1 reconstruida:\n" +
                "• Base = Simulation (jugador, fauna, luz)\n" +
                "• Volcán + materiales\n" +
                "• Pasto / rocas / árboles + niebla\n\n" +
                "Activa Fog y Lighting (bombilla) en Scene View.",
                "OK");
        }

        public static void ImportarCore(bool mostrarDialogo)
        {
            if (!File.Exists(EscenaSimulation1))
            {
                if (mostrarDialogo)
                    EditorUtility.DisplayDialog("DINO WEST", $"No se encontró:\n{EscenaSimulation1}", "OK");
                Debug.LogError($"[DinoVolcanSceneSetup] Falta la escena {EscenaSimulation1}");
                return;
            }

            AsegurarFbxLegible();

            var escenaActual = EditorSceneManager.GetActiveScene();
            if (escenaActual.path != EscenaSimulation1)
            {
                escenaActual = EditorSceneManager.OpenScene(EscenaSimulation1, OpenSceneMode.Single);
            }

            if (!escenaActual.IsValid())
            {
                Debug.LogError("[DinoVolcanSceneSetup] No se pudo abrir Simulation 1.");
                return;
            }

            var previo = BuscarVolcan();
            if (previo != null)
                Undo.DestroyObjectImmediate(previo);

            DesactivarEntornoViejo();

            GameObject prefab = CargarPrefabVolcan();
            if (prefab == null)
            {
                string msg = "No está el FBX/GLB en Assets/Dino/Models/Environment/Volcan/.";
                Debug.LogError("[DinoVolcanSceneSetup] " + msg);
                if (mostrarDialogo) EditorUtility.DisplayDialog("DINO WEST", msg, "OK");
                return;
            }

            GameObject root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (root == null)
                root = Object.Instantiate(prefab);

            Undo.RegisterCreatedObjectUndo(root, "Importar Escenario Volcán");
            root.name = NombreRoot;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Desemparentar si el FBX trae jerarquía rara
            root.SetActive(true);

            AjustarEscalaYCentrar(root);
            ConfigurarCollidersYMateriales(root);
            DinoVolcanMaterialsSetup.CrearMateriales();
            DinoVolcanMaterialsSetup.RepararEscena(mostrarDialogo: false);
            AsegurarJugadorSobreVolcan(root);
            AsegurarTreeImpactEnJugador();
            IncluirEscenaEnBuild();

            EditorSceneManager.MarkSceneDirty(escenaActual);
            bool guardado = EditorSceneManager.SaveScene(escenaActual);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            if (SceneView.lastActiveSceneView != null)
            {
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView.FrameSelected();
                SceneView.lastActiveSceneView.Repaint();
            }

            Bounds b = CalcularBounds(root);
            int meshes = root.GetComponentsInChildren<MeshFilter>(true).Length;
            Debug.Log(
                $"<color=green>[DinoVolcanSceneSetup]</color> Volcán importado. " +
                $"meshes={meshes}, bounds={b.size}, pos={root.transform.position}, save={guardado}. " +
                $"Hierarchy → '{NombreRoot}'.");

            if (mostrarDialogo)
            {
                EditorUtility.DisplayDialog(
                    "DINO WEST",
                    $"Escenario volcánico en Simulation 1.\n\n" +
                    $"Objeto: {NombreRoot}\n" +
                    $"Meshes: {meshes}\n" +
                    $"Tamaño: {b.size.x:0} x {b.size.y:0} x {b.size.z:0} m\n\n" +
                    "Míralo en Hierarchy y pulsa F en Scene View.",
                    "OK");
            }
        }

        static GameObject BuscarVolcan()
        {
            var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < todos.Length; i++)
            {
                if (todos[i] != null && todos[i].name == NombreRoot)
                    return todos[i].gameObject;
            }
            return null;
        }

        static void AsegurarFbxLegible()
        {
            var mi = AssetImporter.GetAtPath(RutaFbx) as ModelImporter;
            if (mi == null) return;
            if (mi.isReadable) return;
            mi.isReadable = true;
            mi.SaveAndReimport();
        }

        static GameObject CargarPrefabVolcan()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RutaFbx);
            if (fbx != null) return fbx;
            return AssetDatabase.LoadAssetAtPath<GameObject>(RutaGlb);
        }

        static void DesactivarEntornoViejo()
        {
            string[] nombres =
            {
                "Terreno_Realista_PBR",
                "01_Arboleda_PBR_Realista",
                "Environment",
                "Terreno_Pista",
                "Pista_Camino"
            };

            var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in todos)
            {
                if (t == null || t.parent != null) continue; // solo raíces o buscar por nombre en cualquier nivel
            }

            foreach (var t in todos)
            {
                if (t == null) continue;
                string n = t.name;
                bool match = false;
                for (int i = 0; i < nombres.Length; i++)
                {
                    if (n == nombres[i]) { match = true; break; }
                }
                if (!match && n.IndexOf("Arboleda", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    match = true;
                if (!match) continue;

                Undo.RecordObject(t.gameObject, "Desactivar entorno previo");
                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
                Debug.Log($"<color=cyan>[DinoVolcanSceneSetup]</color> Desactivado: {n}");
            }
        }

        static void AjustarEscalaYCentrar(GameObject root)
        {
            Bounds b = CalcularBounds(root);
            if (b.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[DinoVolcanSceneSetup] Bounds vacíos: el FBX podría no tener mallas visibles.");
                return;
            }

            // Escala fija 200 (pedido) — el plano Blender es ~2 m → ~400 m de ancho
            root.transform.localScale = Vector3.one * EscalaPlanoUniforme;
            b = CalcularBounds(root);
            Debug.Log($"<color=cyan>[DinoVolcanSceneSetup]</color> Escala x{EscalaPlanoUniforme:0} → tamaño {b.size}.");

            Vector3 offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            root.transform.position += offset;
            b = CalcularBounds(root);
            Debug.Log($"<color=cyan>[DinoVolcanSceneSetup]</color> Bounds: min={b.min} max={b.max} size={b.size}");
        }

        static Bounds CalcularBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void ConfigurarCollidersYMateriales(GameObject root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.gameObject.name;
                bool esPasto = n.IndexOf("Pasto", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Hair", System.StringComparison.OrdinalIgnoreCase) >= 0;

                foreach (var c in mf.GetComponents<Collider>())
                    Object.DestroyImmediate(c);

                if (esPasto)
                {
                    var r = mf.GetComponent<MeshRenderer>();
                    if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    continue;
                }

                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
            }

            DinoMaterialesBIRP.Aplicar(root);
        }

        static void AsegurarJugadorSobreVolcan(GameObject root)
        {
            var vehicle = Object.FindAnyObjectByType<DinoVehicleController>();
            if (vehicle == null) return;

            Bounds b = CalcularBounds(root);
            Vector3 pos = vehicle.transform.position;
            Vector3 xz = new Vector3(pos.x, 0f, pos.z);
            Vector3 centro = new Vector3(b.center.x, 0f, b.center.z);
            if (Mathf.Abs(xz.x - centro.x) > b.extents.x * 0.9f ||
                Mathf.Abs(xz.z - centro.z) > b.extents.z * 0.9f)
            {
                xz = centro;
            }

            float y = b.max.y + 2f;
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(xz.x, b.max.y + 80f, xz.z), Vector3.down, out RaycastHit hit, 300f))
                y = hit.point.y + 0.2f;

            Undo.RecordObject(vehicle.transform, "Reubicar Carnotauro sobre volcán");
            vehicle.transform.position = new Vector3(xz.x, y, xz.z);
            EditorUtility.SetDirty(vehicle);
        }

        static void AsegurarTreeImpactEnJugador()
        {
            var vehicle = Object.FindAnyObjectByType<DinoVehicleController>();
            if (vehicle == null) return;
            if (vehicle.GetComponent<TreeImpactResponse>() == null)
                Undo.AddComponent<TreeImpactResponse>(vehicle.gameObject);
        }

        static void IncluirEscenaEnBuild()
        {
            var actuales = EditorBuildSettings.scenes;
            foreach (var s in actuales)
            {
                if (s.path == EscenaSimulation1) return;
            }

            var lista = new EditorBuildSettingsScene[actuales.Length + 1];
            actuales.CopyTo(lista, 0);
            lista[lista.Length - 1] = new EditorBuildSettingsScene(EscenaSimulation1, true);
            EditorBuildSettings.scenes = lista;
        }
    }
}
