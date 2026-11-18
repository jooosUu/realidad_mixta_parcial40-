using System.Collections.Generic;
using DinoVehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Ambiente volcánico: sol/niebla, 3 decoraciones controlables, adaptar objetos de Simulation al mesh.
    /// </summary>
    public static class DinoVolcanEnvironmentSetup
    {
        public const string Escena = "Assets/Dino/Scenes/Simulation Vulkan.unity";
        public const string ContenedorDecor = "02_Decoracion_Volcan";
        public const string ContenedorArboles = "03_Arboles_Volcan_Scatter";

        /// <summary>Árboles con colisión / TreeReaction (se pueden tumbar con el dinosaurio).</summary>
        public static readonly string[] PrefabsArbolesColision =
        {
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_3.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_4.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_5.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_8.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_10.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_13.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_17.prefab",
            "Assets/Trees Collection Asset PBR/Prefabs/Tree_18.prefab",
        };

        public static readonly string[] PrefabsRocas =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_C_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_C_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_D.prefab",
        };

        public const string ContenedorRocas = "04_Rocas_Volcan_Scatter";

        const string PrefArbCant = "DinoVolcan.Arboles.v3.Cant";
        const string PrefArbEsc = "DinoVolcan.Arboles.v3.Esc";
        const string PrefRocaCant = "DinoVolcan.Rocas.v2.Cant";

        public static int CantArboles
        {
            get => EditorPrefs.GetInt(PrefArbCant, 280);
            set => EditorPrefs.SetInt(PrefArbCant, value);
        }

        public static float EscalaArboles
        {
            get => EditorPrefs.GetFloat(PrefArbEsc, 1.15f);
            set => EditorPrefs.SetFloat(PrefArbEsc, value);
        }

        public static int CantRocas
        {
            get => EditorPrefs.GetInt(PrefRocaCant, 220);
            set => EditorPrefs.SetInt(PrefRocaCant, value);
        }

        public static readonly DecoTipo[] Tipos =
        {
            new DecoTipo(
                "Rocas",
                "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_01.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_02.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_01.prefab"),
            new DecoTipo(
                "Arbustos secos",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/BushDry_A.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/BushDry_B.prefab"),
            new DecoTipo(
                "Ramitas / helechos secos",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_Twig.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_A.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_B.prefab"),
        };

        public struct DecoTipo
        {
            public string Nombre;
            public string[] Prefabs;
            public DecoTipo(string n, params string[] p) { Nombre = n; Prefabs = p; }
        }

        // v2: escalas en metros de mundo (contenedor fuera del ×100)
        const string PrefCant = "DinoVolcan.Decor.v2.{0}.Cant";
        const string PrefEsc = "DinoVolcan.Decor.v2.{0}.Esc";
        const string PrefOn = "DinoVolcan.Decor.v2.{0}.On";

        public static int GetCant(int i) => EditorPrefs.GetInt(string.Format(PrefCant, i), i == 0 ? 60 : 45);
        public static void SetCant(int i, int v) => EditorPrefs.SetInt(string.Format(PrefCant, i), v);
        public static float GetEsc(int i) => EditorPrefs.GetFloat(string.Format(PrefEsc, i), i == 0 ? 0.55f : 0.4f);
        public static void SetEsc(int i, float v) => EditorPrefs.SetFloat(string.Format(PrefEsc, i), v);
        public static bool GetOn(int i) => EditorPrefs.GetBool(string.Format(PrefOn, i), true);
        public static void SetOn(int i, bool v) => EditorPrefs.SetBool(string.Format(PrefOn, i), v);

        [MenuItem("DINO WEST/Ambiente Volcán")]
        public static void AbrirVentana() => DinoVolcanEnvironmentWindow.Open();

        public static void MenuRapido()
        {
            AplicarTodoSilencioso();
            EditorUtility.DisplayDialog("DINO WEST",
                "Aplicado:\n• Sol + niebla\n• Pasto / rocas / árboles sobre la malla\n• Objetos adaptados al relieve\n• Materiales reparados",
                "OK");
        }

        /// <summary>Aplica todo sin diálogo (también lo llama el auto-fix al abrir Simulation 1).</summary>
        public static void AplicarTodoSilencioso()
        {
            AsegurarEscena();
            AplicarAtmosferaVolcanica();
            DinoVolcanPastoScatter.EscalaGlobal = 1.8f;
            DinoVolcanPastoScatter.Cantidad = 4500;
            DinoVolcanPastoScatter.Umbral = 0.04f;
            DinoVolcanPastoScatter.UsarTexturaGrassDryDelPack();
            CantArboles = 280;
            EscalaArboles = 1.15f;
            CantRocas = 220;
            for (int i = 0; i < Tipos.Length; i++)
            {
                SetOn(i, true);
                SetEsc(i, i == 0 ? 0.55f : 0.4f);
                SetCant(i, i == 0 ? 80 : 60);
            }
            DinoVolcanPastoScatter.Poblar(silencioso: true);
            PoblarRocasConPasto();
            PoblarDecoraciones();
            PoblarArbolesConColision();
            AdaptarObjetosSimulationAlVolcan(ocultarSiNoCabe: false);
            int reps = DinoMaterialesBIRP.RepararTodaLaEscena();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=orange>[VolcanAmbiente]</color> OK: vegetación x3, árboles altos, materiales reparados ({reps} renderers)");
        }

        public static void AsegurarEscena()
        {
            if (EditorSceneManager.GetActiveScene().path != Escena)
                EditorSceneManager.OpenScene(Escena);
        }

        public static GameObject BuscarVolcan()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                string n = t.name;
                if (n.IndexOf("Escenario_Volcan", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("TerrenoPasto", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t.gameObject;
            }
            return null;
        }

        public static MeshCollider AsegurarColliderSuelo(GameObject volcan)
        {
            // Quitar collider en la raíz (suele apuntar a un mesh incorrecto bajo ×100)
            foreach (var c in volcan.GetComponents<MeshCollider>())
                Object.DestroyImmediate(c);

            MeshCollider best = null;
            foreach (var mf in volcan.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("pasto") || n.Contains("decor") || n.Contains("scatter") || n.Contains("hebra"))
                    continue;
                var mc = mf.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    mc.sharedMesh = mf.sharedMesh;
                }
                else mc.sharedMesh = mf.sharedMesh;
                if (best == null || mf.sharedMesh.vertexCount > (best.sharedMesh != null ? best.sharedMesh.vertexCount : 0))
                    best = mc;
            }
            Physics.SyncTransforms();
            return best;
        }

        /// <summary>Snap solo contra el MeshCollider del suelo del volcán (ignora otros colliders).</summary>
        public static bool SnapAlSuelo(Vector3 approx, out Vector3 punto, out Vector3 normal, float maxDist = 200f)
        {
            punto = approx;
            normal = Vector3.up;
            var volcan = BuscarVolcan();
            if (volcan == null) return false;
            var mc = AsegurarColliderSuelo(volcan);
            if (mc == null) return false;

            var origin = approx + Vector3.up * 80f;
            var ray = new Ray(origin, Vector3.down);
            if (mc.Raycast(ray, out RaycastHit hit, maxDist))
            {
                punto = hit.point;
                normal = hit.normal.sqrMagnitude > 0.01f ? hit.normal : Vector3.up;
                return true;
            }
            // Fallback: ClosestPoint del collider del volcán
            punto = mc.ClosestPoint(approx);
            normal = Vector3.up;
            return true;
        }

        public static void AplicarAtmosferaVolcanica()
        {
            Light sol = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional) { sol = l; break; }
            }
            if (sol == null)
            {
                var go = new GameObject("Directional Light");
                sol = go.AddComponent<Light>();
                sol.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(go, "Sol volcán");
            }

            sol.gameObject.SetActive(true);
            sol.enabled = true;
            sol.color = new Color(1f, 0.48f, 0.22f);
            sol.intensity = 1.65f;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(32f, 205f, 0f);
            RenderSettings.sun = sol;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.014f; // visible a escala del volcán
            RenderSettings.fogColor = new Color(0.62f, 0.28f, 0.14f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.32f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.2f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.06f, 0.04f);
            RenderSettings.ambientIntensity = 1.15f;

            var sky = GameObject.Find("SkyboxMountains_DinoWest");
            if (sky == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/TerrainDemoScene_URP/Prefabs/Skybox/SkyboxMountains.prefab");
                if (prefab != null)
                {
                    sky = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    sky.name = "SkyboxMountains_DinoWest";
                    Undo.RegisterCreatedObjectUndo(sky, "Skybox");
                    DinoMaterialesBIRP.Aplicar(sky);
                }
            }
            else sky.SetActive(true);

            // Scene View: activar niebla (si no, parece que “no hay niebla”)
            foreach (SceneView sv in SceneView.sceneViews)
            {
                if (sv == null) continue;
                var st = sv.sceneViewState;
                var prop = st.GetType().GetProperty("fogEnabled") ?? st.GetType().GetProperty("showFog");
                if (prop != null && prop.CanWrite) prop.SetValue(st, true);
                sv.Repaint();
            }

            DynamicGI.UpdateEnvironment();
            EditorUtility.SetDirty(sol);
            Debug.Log("<color=orange>[VolcanAmbiente]</color> Sol y niebla volcánica OK (fogDensity=0.014)");
        }

        public static void PoblarDecoraciones()
        {
            var volcan = BuscarVolcan();
            if (volcan == null)
            {
                Debug.LogError("[VolcanAmbiente] No hay Escenario_Volcan");
                return;
            }

            AsegurarColliderSuelo(volcan);
            Physics.SyncTransforms();

            // Borrar decoración vieja (también si quedó bajo el ×100)
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == ContenedorDecor)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }

            // Contenedor en raíz de escena (escala 1), igual que el pasto
            var root = new GameObject(ContenedorDecor);
            Undo.RegisterCreatedObjectUndo(root, "Decoración volcán");
            root.transform.SetParent(null);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            if (!TryGetSueloMask(volcan, out MeshFilter suelo, out Mesh mesh, out Color[] cols, out bool hasCol))
                return;

            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            bool hasN = norms != null && norms.Length == verts.Length;
            var l2w = suelo.transform.localToWorldMatrix;
            int triCount = tris.Length / 3;
            var rng = new System.Random(99);

            for (int tipo = 0; tipo < Tipos.Length; tipo++)
            {
                if (!GetOn(tipo)) continue;
                var prefs = new List<GameObject>();
                foreach (var path in Tipos[tipo].Prefabs)
                {
                    var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (p != null) prefs.Add(p);
                }
                if (prefs.Count == 0) continue;

                var folder = new GameObject(Tipos[tipo].Nombre);
                folder.transform.SetParent(root.transform, false);

                int cant = Mathf.Clamp(GetCant(tipo), 0, 400);
                float esc = GetEsc(tipo);
                int hechos = 0, intentos = 0;
                while (hechos < cant && intentos < cant * 25)
                {
                    intentos++;
                    if (!SampleWeightPaint(tris, verts, norms, cols, hasCol, hasN, l2w, triCount, rng,
                            0.08f, out Vector3 pos, out Vector3 nrm, out _))
                        continue;
                    if (Vector3.Angle(nrm, Vector3.up) > 40f) continue;

                    var prefab = prefs[rng.Next(prefs.Count)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (go == null) continue;
                    Undo.RegisterCreatedObjectUndo(go, "Decor");
                    go.name = $"{Tipos[tipo].Nombre}_{hechos:000}";
                    go.transform.SetParent(folder.transform, true);
                    go.transform.position = pos;
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nrm)
                        * Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    float s = esc * Mathf.Lerp(0.75f, 1.1f, (float)rng.NextDouble());
                    go.transform.localScale = Vector3.one * s;

                    foreach (var c in go.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(c);
                    DinoMaterialesBIRP.Aplicar(go);
                    hechos++;
                }
                Debug.Log($"<color=orange>[VolcanAmbiente]</color> {Tipos[tipo].Nombre}: {hechos}");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Tree_3 + Tree_4 (colisión / TreeReaction), repartidos con la misma máscara weight-paint que el pasto.
        /// </summary>
        public static void PoblarArbolesConColision()
        {
            var volcan = BuscarVolcan();
            if (volcan == null) return;
            AsegurarColliderSuelo(volcan);

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == ContenedorArboles)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }

            var prefs = new List<GameObject>();
            foreach (var path in PrefabsArbolesColision)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) prefs.Add(p);
            }
            if (prefs.Count == 0)
            {
                Debug.LogError("[VolcanAmbiente] No encontré Tree_3 / Tree_4");
                return;
            }

            if (!TryGetSueloMask(volcan, out MeshFilter suelo, out Mesh mesh, out Color[] cols, out bool hasCol))
                return;

            var root = new GameObject(ContenedorArboles);
            Undo.RegisterCreatedObjectUndo(root, "Árboles volcán");
            root.transform.SetParent(null);
            root.transform.position = Vector3.zero;
            root.transform.localScale = Vector3.one;

            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            bool hasN = norms != null && norms.Length == verts.Length;
            var l2w = suelo.transform.localToWorldMatrix;
            int triCount = tris.Length / 3;
            var rng = new System.Random(123);

            int cant = Mathf.Clamp(CantArboles, 40, 900);
            float escMul = EscalaArboles;
            int hechos = 0, intentos = 0;
            float minSep = 2.4f;
            var colocados = new List<Vector3>();

            while (hechos < cant && intentos < cant * 80)
            {
                intentos++;
                if (!SampleWeightPaint(tris, verts, norms, cols, hasCol, hasN, l2w, triCount, rng,
                        0.05f, out Vector3 pos, out Vector3 nrm, out float dens))
                    continue;
                if (Vector3.Angle(nrm, Vector3.up) > 40f) continue;
                if (dens < 0.08f) continue;

                bool cerca = false;
                foreach (var p in colocados)
                {
                    if ((p - pos).sqrMagnitude < minSep * minSep) { cerca = true; break; }
                }
                if (cerca) continue;

                var prefab = prefs[hechos % prefs.Count];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (go == null) continue;
                Undo.RegisterCreatedObjectUndo(go, "Árbol volcán");
                go.name = $"Arbol_Volcan_{prefab.name}_{hechos:000}";
                go.transform.SetParent(root.transform, true);

                // Más altos que los dinosaurios (~18–28 m), sin gigantes de 50 m+
                go.transform.localScale = Vector3.one;
                var b = BoundsRender(go);
                float alto = Mathf.Max(0.5f, b.size.y);
                float alturaObj = Mathf.Lerp(18f, 28f, dens) * escMul;
                float escala = Mathf.Clamp(alturaObj / alto, 0.4f, 3.5f);
                go.transform.localScale = Vector3.one * escala;

                // Base del mesh sobre la superficie
                b = BoundsRender(go);
                float baseOffset = go.transform.position.y - b.min.y;
                go.transform.position = pos + Vector3.up * (baseOffset - 0.05f);
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nrm)
                    * Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

                PrepararArbolColision(go, escala);
                DinoMaterialesBIRP.Aplicar(go);
                colocados.Add(pos);
                hechos++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=orange>[VolcanAmbiente]</color> Árboles con colisión: {hechos}/{cant} (TreeReaction + weight-paint)");
        }

        /// <summary>Rocas de varios tamaños en la misma máscara weight-paint que el pasto.</summary>
        public static void PoblarRocasConPasto()
        {
            var volcan = BuscarVolcan();
            if (volcan == null) return;
            AsegurarColliderSuelo(volcan);

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == ContenedorRocas)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }

            var prefs = new List<GameObject>();
            foreach (var path in PrefabsRocas)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) prefs.Add(p);
            }
            if (prefs.Count == 0) return;
            if (!TryGetSueloMask(volcan, out MeshFilter suelo, out Mesh mesh, out Color[] cols, out bool hasCol))
                return;

            var root = new GameObject(ContenedorRocas);
            Undo.RegisterCreatedObjectUndo(root, "Rocas volcán");
            root.transform.SetParent(null);
            root.transform.localScale = Vector3.one;

            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            bool hasN = norms != null && norms.Length == verts.Length;
            var l2w = suelo.transform.localToWorldMatrix;
            int triCount = tris.Length / 3;
            var rng = new System.Random(55);

            int cant = Mathf.Clamp(CantRocas, 20, 500);
            int hechos = 0, intentos = 0;
            while (hechos < cant && intentos < cant * 40)
            {
                intentos++;
                if (!SampleWeightPaint(tris, verts, norms, cols, hasCol, hasN, l2w, triCount, rng,
                        0.06f, out Vector3 pos, out Vector3 nrm, out float dens))
                    continue;
                if (Vector3.Angle(nrm, Vector3.up) > 45f) continue;

                var prefab = prefs[rng.Next(prefs.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (go == null) continue;
                Undo.RegisterCreatedObjectUndo(go, "Roca volcán");
                go.name = $"Roca_Volcan_{prefab.name}_{hechos:000}";
                go.transform.SetParent(root.transform, true);

                // Tamaños variados: pequeñas, medias y grandes
                float roll = (float)rng.NextDouble();
                float esc = roll < 0.45f ? Mathf.Lerp(0.25f, 0.55f, (float)rng.NextDouble())
                    : roll < 0.8f ? Mathf.Lerp(0.55f, 1.1f, (float)rng.NextDouble())
                    : Mathf.Lerp(1.1f, 2.4f, (float)rng.NextDouble());
                esc *= Mathf.Lerp(0.85f, 1.15f, dens);
                go.transform.localScale = Vector3.one * esc;

                var b = BoundsRender(go);
                float baseOffset = go.transform.position.y - b.min.y;
                go.transform.position = pos + Vector3.up * (baseOffset - 0.02f);
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nrm)
                    * Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(c);
                DinoMaterialesBIRP.Aplicar(go);
                hechos++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=orange>[VolcanAmbiente]</color> Rocas: {hechos} (tamaños mixtos + weight-paint)");
        }

        static void PrepararArbolColision(GameObject arbol, float escala)
        {
            foreach (var c in arbol.GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger) Object.DestroyImmediate(c);
            }

            float s = Mathf.Max(0.01f, escala);
            // Tronco sólido en metros de mundo (~2.5 m grosor, ~12 m alto) → local / escala
            var box = arbol.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 6f / s, 0f);
            box.size = new Vector3(2.5f / s, 12f / s, 2.5f / s);
            box.isTrigger = false;
            box.providesContacts = true;

            if (arbol.GetComponent<TreeReaction>() == null)
                arbol.AddComponent<TreeReaction>();
            if (arbol.GetComponent<DinoFoodTree>() == null)
                arbol.AddComponent<DinoFoodTree>();
        }

        static Bounds BoundsRender(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static bool TryGetSueloMask(GameObject volcan, out MeshFilter suelo, out Mesh mesh, out Color[] cols, out bool hasCol)
        {
            suelo = null;
            mesh = null;
            cols = null;
            hasCol = false;
            foreach (var mf in volcan.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("pasto") || n.Contains("decor") || n.Contains("scatter") || n.Contains("hebra"))
                    continue;
                if (suelo == null || mf.sharedMesh.vertexCount > suelo.sharedMesh.vertexCount)
                    suelo = mf;
            }
            if (suelo == null || suelo.sharedMesh == null) return false;
            mesh = suelo.sharedMesh;
            cols = mesh.colors;
            hasCol = cols != null && cols.Length == mesh.vertexCount;
            if (!hasCol && mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount)
            {
                var c32 = mesh.colors32;
                cols = new Color[c32.Length];
                for (int i = 0; i < c32.Length; i++) cols[i] = c32[i];
                hasCol = true;
            }
            return true;
        }

        /// <summary>Muestreo ponderado por vertex colors (weight paint Pasto de Blender).</summary>
        static bool SampleWeightPaint(
            int[] tris, Vector3[] verts, Vector3[] norms, Color[] cols,
            bool hasCol, bool hasN, Matrix4x4 l2w, int triCount, System.Random rng,
            float umbral, out Vector3 worldPos, out Vector3 worldNrm, out float dens)
        {
            worldPos = Vector3.zero;
            worldNrm = Vector3.up;
            dens = 1f;

            int t = rng.Next(triCount);
            int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
            if (hasCol)
            {
                dens = (cols[i0].r + cols[i1].r + cols[i2].r) / 3f;
                if (dens < umbral) return false;
                if (rng.NextDouble() > dens) return false;
            }

            Vector3 bary = RandomBary(rng);
            Vector3 lp = verts[i0] * bary.x + verts[i1] * bary.y + verts[i2] * bary.z;
            Vector3 ln = hasN
                ? (norms[i0] * bary.x + norms[i1] * bary.y + norms[i2] * bary.z).normalized
                : Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).normalized;

            worldPos = l2w.MultiplyPoint3x4(lp);
            worldNrm = l2w.MultiplyVector(ln).normalized;
            if (worldNrm.sqrMagnitude < 0.0001f) worldNrm = Vector3.up;
            if (hasCol)
                dens = cols[i0].r * bary.x + cols[i1].r * bary.y + cols[i2].r * bary.z;
            return true;
        }

        public static void AdaptarObjetosSimulationAlVolcan(bool ocultarSiNoCabe)
        {
            var volcan = BuscarVolcan();
            if (volcan == null) return;
            AsegurarColliderSuelo(volcan);
            Physics.SyncTransforms();

            string[] carpetas =
            {
                "Ecosistema_DinoWest",
                "02_Arboles_Comida_Interactivas",
                "03_Rocas_PBR_Fotorrealistas",
                "04_Flora_Flores_Y_Arbustos",
                "05_Fauna_Dinosaurios_Vivos",
                "06_Pista_Obstaculos_Y_Pelota",
            };

            var roots = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || t.parent != null) continue; // raíces
                foreach (var c in carpetas)
                {
                    if (t.name == c || t.name.StartsWith("Manada_") || t.name.StartsWith("Arbol_Comida")
                        || t.name.Contains("Pelota_Dinosaurio") || t.name.Contains("Anquilo"))
                    {
                        roots.Add(t);
                        break;
                    }
                }
                // También hijos del ecosistema
                if (t.name == "Ecosistema_DinoWest")
                {
                    for (int i = 0; i < t.childCount; i++)
                        roots.Add(t.GetChild(i));
                }
            }

            // Objetos con renderer bajo ecosistema / manadas / comida
            var candidatos = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                string n = t.name;
                bool match = n.StartsWith("Arbol_Comida") || n.StartsWith("Manada_")
                    || n.StartsWith("Arbol_Bosque") || n.Contains("Tree_")
                    || n.Contains("Pelota_Dinosaurio") || n.Contains("Estegosaur")
                    || n.Contains("Paquicefal") || n.Contains("Raptor") || n.Contains("Anquilo")
                    || n.StartsWith("PBR_") || n.Contains("Stegasaurus") || n.Contains("Pachy")
                    || n.Contains("Velociraptor") || n.Contains("Dinosaur")
                    || n.Contains("Bush") || n.Contains("Fern") || n.Contains("Rock_")
                    || n.Contains("Flor") || n.Contains("Flower") || n.Contains("Heather");
                if (!match) continue;
                // solo raíces de instancia (con renderer propio o hijos)
                if (t.GetComponentInChildren<Renderer>() == null) continue;
                if (t.parent != null && (t.parent.name.StartsWith("Manada_") || t.parent.name.Contains("Fauna")))
                {
                    // usar el individuo, ok
                }
                candidatos.Add(t);
            }

            int adaptados = 0;
            foreach (var t in candidatos)
            {
                if (t == null) continue;
                // No tocar volcán / pasto / decor
                if (t.root.name.IndexOf("Escenario_Volcan", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (t.name.StartsWith("Pasto_") || t.name.Contains("Decoracion_Volcan")
                    || t.name.StartsWith("Arbol_Volcan_") || t.name.StartsWith("Roca_Volcan_")
                    || t.name == ContenedorArboles || t.name == ContenedorRocas) continue;

                Vector3 pos = t.position;
                // Anclar por la base real del mesh (bounds), no solo el pivote
                var rends = t.GetComponentsInChildren<Renderer>();
                float baseOffset = 0f;
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    baseOffset = pos.y - b.min.y;
                }

                if (SnapAlSuelo(new Vector3(pos.x, pos.y + 5f, pos.z), out Vector3 hit, out Vector3 nrm)
                    || SnapAlSuelo(pos, out hit, out nrm))
                {
                    Undo.RecordObject(t, "Adaptar al volcán");
                    t.position = new Vector3(pos.x, hit.y + baseOffset + 0.02f, pos.z);
                    adaptados++;
                }
                else if (ocultarSiNoCabe)
                {
                    t.gameObject.SetActive(false);
                }

                DinoMaterialesBIRP.Aplicar(t.gameObject);
                // Forzar renderers activos (objetos “blancos” a veces quedan sin material válido)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial == null || r.sharedMaterial.shader == null
                        || r.sharedMaterial.shader.name.Contains("InternalError"))
                        DinoMaterialesBIRP.Aplicar(r.gameObject);
                    r.enabled = true;
                    EditorUtility.SetDirty(r);
                }
                EditorUtility.SetDirty(t);
            }

            // Carpetas enteras de flora/rocas (suelen verse blancas/azules por URP)
            string[] carpetasBIRP =
            {
                "Ecosistema_DinoWest",
                "01_Arboleda_PBR_Realista",
                "03_Rocas_PBR_Fotorrealistas",
                "04_Flora_Flores_Y_Arbustos",
                "05_Fauna_Dinosaurios_Vivos",
            };
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                foreach (var n in carpetasBIRP)
                {
                    if (t.name == n)
                        DinoMaterialesBIRP.Aplicar(t.gameObject);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=orange>[VolcanAmbiente]</color> Objetos adaptados al relieve: {adaptados}");
        }

        public static void SetEcoVisible(bool visible)
        {
            string[] names =
            {
                "Ecosistema_DinoWest",
                "01_Arboleda_PBR_Realista",
                "02_Arboles_Comida_Interactivas",
                "03_Rocas_PBR_Fotorrealistas",
                "04_Flora_Flores_Y_Arbustos",
                "05_Fauna_Dinosaurios_Vivos",
                "06_Pista_Obstaculos_Y_Pelota",
                "Terreno_Realista_PBR",
            };
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                foreach (var n in names)
                {
                    if (t.name == n)
                    {
                        Undo.RecordObject(t.gameObject, "Toggle eco");
                        t.gameObject.SetActive(visible);
                    }
                }
                if (t.name.StartsWith("Manada_"))
                    t.gameObject.SetActive(visible);
                if (t.name.StartsWith("Arbol_Comida") || t.name.StartsWith("Arbol_Bosque"))
                    t.gameObject.SetActive(visible);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static Vector3 RandomBary(System.Random rng)
        {
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            return new Vector3(u, v, 1f - u - v);
        }
    }

    public class DinoVolcanEnvironmentWindow : EditorWindow
    {
        Vector2 _scroll;

        public static void Open()
        {
            var w = GetWindow<DinoVolcanEnvironmentWindow>("Ambiente Volcán");
            w.minSize = new Vector2(400, 560);
            w.Show();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("DINO WEST · Ambiente volcánico", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "El plano se escala a ×200. Pasto/árboles/rocas viven fuera de esa jerarquía (metros de mundo) y se re-adaptan al relieve.",
                MessageType.Warning);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.2f);
            if (GUILayout.Button("Escalar plano a 200 + adaptar TODO", GUILayout.Height(36)))
                DinoVolcanSceneSetup.EscalarPlano200YAdaptar();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.55f, 0.25f);
            if (GUILayout.Button("1. Aplicar SOL + niebla volcánica", GUILayout.Height(32)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                DinoVolcanEnvironmentSetup.AplicarAtmosferaVolcanica();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            GUILayout.Label("Pasto (anclado al suelo)", EditorStyles.boldLabel);
            DinoVolcanPastoScatter.Cantidad = EditorGUILayout.IntSlider("Cantidad pasto", DinoVolcanPastoScatter.Cantidad, 200, 5000);
            DinoVolcanPastoScatter.EscalaGlobal = EditorGUILayout.Slider("Escala pasto", DinoVolcanPastoScatter.EscalaGlobal, 0.8f, 4f);
            DinoVolcanPastoScatter.Umbral = EditorGUILayout.Slider("Umbral", DinoVolcanPastoScatter.Umbral, 0f, 0.8f);
            DinoVolcanPastoScatter.ColorPasto = EditorGUILayout.ColorField("Tinte pasto", DinoVolcanPastoScatter.ColorPasto);

            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button("Repoblar PASTO pegado al plano", GUILayout.Height(30)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                var v = DinoVolcanEnvironmentSetup.BuscarVolcan();
                if (v != null) DinoVolcanEnvironmentSetup.AsegurarColliderSuelo(v);
                DinoVolcanPastoScatter.UsarTexturaGrassDryDelPack();
                DinoVolcanPastoScatter.Poblar(silencioso: false);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);
            GUILayout.Label("3 decoraciones (control independiente)", EditorStyles.boldLabel);
            for (int i = 0; i < DinoVolcanEnvironmentSetup.Tipos.Length; i++)
            {
                EditorGUILayout.BeginVertical("box");
                var on = EditorGUILayout.ToggleLeft(DinoVolcanEnvironmentSetup.Tipos[i].Nombre + " visible/activo", DinoVolcanEnvironmentSetup.GetOn(i));
                DinoVolcanEnvironmentSetup.SetOn(i, on);
                DinoVolcanEnvironmentSetup.SetCant(i, EditorGUILayout.IntSlider("Cantidad", DinoVolcanEnvironmentSetup.GetCant(i), 0, 250));
                DinoVolcanEnvironmentSetup.SetEsc(i, EditorGUILayout.Slider("Escala (m)", DinoVolcanEnvironmentSetup.GetEsc(i), 0.15f, 1.5f));
                EditorGUILayout.EndVertical();
            }

            GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
            if (GUILayout.Button("Repoblar DECORACIONES en el volcán", GUILayout.Height(30)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                DinoVolcanEnvironmentSetup.PoblarDecoraciones();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);
            GUILayout.Label("Árboles con colisión (se caen con el dino)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tree_3/4/5/8/10/13/17/18 · weight-paint · BoxCollider + TreeReaction.",
                MessageType.Info);
            DinoVolcanEnvironmentSetup.CantArboles = EditorGUILayout.IntSlider("Cantidad árboles", DinoVolcanEnvironmentSetup.CantArboles, 80, 900);
            DinoVolcanEnvironmentSetup.EscalaArboles = EditorGUILayout.Slider("Escala árboles (alto)", DinoVolcanEnvironmentSetup.EscalaArboles, 1.0f, 3.5f);
            GUI.backgroundColor = new Color(0.55f, 0.85f, 0.45f);
            if (GUILayout.Button("Repoblar ÁRBOLES (weight paint)", GUILayout.Height(30)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                DinoVolcanEnvironmentSetup.PoblarArbolesConColision();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            GUILayout.Label("Rocas (varios tamaños, con el pasto)", EditorStyles.boldLabel);
            DinoVolcanEnvironmentSetup.CantRocas = EditorGUILayout.IntSlider("Cantidad rocas", DinoVolcanEnvironmentSetup.CantRocas, 40, 400);
            GUI.backgroundColor = new Color(0.7f, 0.65f, 0.5f);
            if (GUILayout.Button("Repoblar ROCAS (weight paint)", GUILayout.Height(28)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                DinoVolcanEnvironmentSetup.PoblarRocasConPasto();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);
            GUILayout.Label("Objetos traídos de Simulation (fauna, oasis…)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Se adaptan a la altura del mesh volcánico (solo collider del volcán).\n" +
                "Puedes ocultarlos/mostrarlos con los botones.",
                MessageType.Info);

            if (GUILayout.Button("Adaptar al relieve + materiales", GUILayout.Height(28)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                DinoVolcanEnvironmentSetup.AdaptarObjetosSimulationAlVolcan(false);
            }
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("REPARAR MATERIALES BLANCOS (toda la escena)", GUILayout.Height(30)))
            {
                DinoVolcanEnvironmentSetup.AsegurarEscena();
                int n = DinoMaterialesBIRP.RepararTodaLaEscena();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("DINO WEST", $"Materiales reparados en {n} renderers.", "OK");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ocultar objetos de Simulation"))
                DinoVolcanEnvironmentSetup.SetEcoVisible(false);
            if (GUILayout.Button("Mostrar objetos de Simulation"))
                DinoVolcanEnvironmentSetup.SetEcoVisible(true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button("TODO: Sol + Pasto + Rocas + Árboles + Adaptar", GUILayout.Height(40)))
            {
                DinoVolcanEnvironmentSetup.MenuRapido();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }
    }
}
