using System.Collections.Generic;
using System.IO;
using DinoVehicle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Pasto con densidad como en Blender (vertex group → vertex colors).
    /// Donde el color es más claro = más pasto; oscuro/negro = poco o nada.
    /// </summary>
    public static class DinoVolcanPastoScatter
    {
        public const string Escena = "Assets/Dino/Scenes/Simulation Vulkan.unity";
        public const string NombreContenedor = "01_Pasto_Volcan_Scatter";
        const string CarpetaGen = "Assets/Dino/Models/Environment/Volcan/Generated";
        const string MeshAsset = CarpetaGen + "/PastoBillboard.asset";
        const string MatAsset = CarpetaGen + "/M_PastoVolcan_Billboard.mat";
        const string TexAsset = CarpetaGen + "/T_PastoVolcan.png";

        // Preferencias (clave v2: el volcán está a ×100; el pasto ya NO hereda esa escala)
        const string PrefUmbral = "DinoVolcan.Pasto.v2.Umbral";
        const string PrefCantidad = "DinoVolcan.Pasto.v2.Cantidad";
        const string PrefPotencia = "DinoVolcan.Pasto.v2.Potencia";
        const string PrefEscala = "DinoVolcan.Pasto.v2.Escala";
        const string PrefColorR = "DinoVolcan.Pasto.v2.ColorR";
        const string PrefColorG = "DinoVolcan.Pasto.v2.ColorG";
        const string PrefColorB = "DinoVolcan.Pasto.v2.ColorB";

        static readonly string[] PrefabsPastoVisible =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_D.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_B.prefab",
        };

        // Auto-poblar DESACTIVADO: competía con Importar/Reconstruir y dejaba la escena inconsistente.

        public static void MenuPoblar()
        {
            if (EditorSceneManager.GetActiveScene().path != Escena)
                EditorSceneManager.OpenScene(Escena);
            Poblar(silencioso: false);
        }

        [MenuItem("DINO WEST/Ajustar Densidad de Pasto Volcán")]
        public static void MenuVentana()
        {
            DinoVolcanPastoDensityWindow.Open();
        }

        public static float Umbral
        {
            get => EditorPrefs.GetFloat(PrefUmbral, 0.08f);
            set => EditorPrefs.SetFloat(PrefUmbral, value);
        }

        public static int Cantidad
        {
            get => EditorPrefs.GetInt(PrefCantidad, 3500);
            set => EditorPrefs.SetInt(PrefCantidad, value);
        }

        /// <summary>Exponente: &gt;1 concentra pasto en zonas muy pintadas; &lt;1 lo suaviza.</summary>
        public static float Potencia
        {
            get => EditorPrefs.GetFloat(PrefPotencia, 1.35f);
            set => EditorPrefs.SetFloat(PrefPotencia, value);
        }

        /// <summary>Escala mundo de cada mata (prefabs GrassDry ~1 m base).</summary>
        public static float EscalaGlobal
        {
            get => EditorPrefs.GetFloat(PrefEscala, 2.2f);
            set => EditorPrefs.SetFloat(PrefEscala, value);
        }

        public static Color ColorPasto
        {
            get => new Color(
                EditorPrefs.GetFloat(PrefColorR, 0.72f),
                EditorPrefs.GetFloat(PrefColorG, 0.38f),
                EditorPrefs.GetFloat(PrefColorB, 0.12f),
                1f);
            set
            {
                EditorPrefs.SetFloat(PrefColorR, value.r);
                EditorPrefs.SetFloat(PrefColorG, value.g);
                EditorPrefs.SetFloat(PrefColorB, value.b);
            }
        }

        public const string TexGrassDryPack =
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Textures/Grass_Dry_BaseColor.tif";

        public static void AplicarColorYMaterialAlScatter()
        {
            var mat = AsegurarMaterialBillboard();
            AplicarAspectoMaterial(mat);
            EditorUtility.SetDirty(mat);

            var cont = BuscarContenedor();
            if (cont == null) return;
            foreach (var r in cont.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Usa la textura real del pack (GrassDry), no el PNG procedural feo.</summary>
        public static void UsarTexturaGrassDryDelPack()
        {
            EnsureFolder();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexGrassDryPack);
            if (tex == null)
            {
                EditorUtility.DisplayDialog("DINO WEST", "No encontré Grass_Dry_BaseColor.tif en TerrainDemoScene_URP.", "OK");
                return;
            }

            var ti = AssetImporter.GetAtPath(TexGrassDryPack) as TextureImporter;
            if (ti != null && !ti.alphaIsTransparency)
            {
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }

            var mat = AsegurarMaterialBillboard();
            mat.mainTexture = tex;
            AplicarAspectoMaterial(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            AplicarColorYMaterialAlScatter();
            Debug.Log("<color=green>[DinoVolcanPasto]</color> Textura → Grass_Dry_BaseColor.tif");
        }

        static void AplicarAspectoMaterial(Material mat)
        {
            if (mat == null) return;
            var standard = Shader.Find("Standard");
            if (standard != null) mat.shader = standard;
            mat.color = ColorPasto;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", ColorPasto);
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 1);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.35f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        }

        public static void Poblar(bool silencioso)
        {
            var root = BuscarVolcan();
            if (root == null)
            {
                Fail(silencioso, "No encontré el volcán (Escenario_Volcan*).");
                return;
            }

            var suelo = BuscarMeshSuelo(root);
            if (suelo == null || suelo.sharedMesh == null)
            {
                Fail(silencioso, "Sin malla de suelo.");
                return;
            }

            AsegurarMeshLegible(suelo);
            var mesh = suelo.sharedMesh;
            var tris = mesh.triangles;
            var verts = mesh.vertices;
            var norms = mesh.normals;
            bool hasN = norms != null && norms.Length == verts.Length;
            Color[] cols = mesh.colors;
            bool hasCol = cols != null && cols.Length == verts.Length;
            // Algunas importaciones usan colors32
            if (!hasCol && mesh.colors32 != null && mesh.colors32.Length == verts.Length)
            {
                var c32 = mesh.colors32;
                cols = new Color[c32.Length];
                for (int i = 0; i < c32.Length; i++) cols[i] = c32[i];
                hasCol = true;
            }

            if (!hasCol && !silencioso)
            {
                bool cont = EditorUtility.DisplayDialog(
                    "DINO WEST — Densidad",
                    "Esta malla aún no trae la máscara de Pasto de Blender (vertex colors).\n\n" +
                    "Se repartirá el pasto de forma uniforme (como ahora).\n\n" +
                    "Para delimitar zonas como en Blender:\n" +
                    "1) En Blender weight-paint el grupo Pasto\n" +
                    "2) Corre Tools/blender_bake_pasto_vertex_colors.py\n" +
                    "3) Reimporta el FBX y vuelve a poblar\n\n" +
                    "¿Poblar igual de forma uniforme?",
                    "Poblar uniforme", "Cancelar");
                if (!cont) return;
            }

            // Material BIRP con textura GrassDry GARANTIZADA (evita matas blancas/invisibles)
            Material mat = AsegurarMaterialBillboard();
            Mesh billboard = AsegurarMeshBillboard();
            var prefsPasto = new List<GameObject>();
            foreach (var path in PrefabsPastoVisible)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) prefsPasto.Add(p);
            }
            bool usarPrefabs = prefsPasto.Count > 0;

            foreach (var c in root.GetComponents<MeshCollider>())
                Object.DestroyImmediate(c);
            var mcSuelo = suelo.GetComponent<MeshCollider>();
            if (mcSuelo == null) mcSuelo = Undo.AddComponent<MeshCollider>(suelo.gameObject);
            mcSuelo.sharedMesh = suelo.sharedMesh;
            Physics.SyncTransforms();

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == NombreContenedor)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }

            var contenedor = new GameObject(NombreContenedor);
            Undo.RegisterCreatedObjectUndo(contenedor, "Pasto volcán");
            contenedor.transform.SetParent(null);
            contenedor.transform.position = Vector3.zero;
            contenedor.transform.rotation = Quaternion.identity;
            contenedor.transform.localScale = Vector3.one;

            var l2w = suelo.transform.localToWorldMatrix;
            int triCount = tris.Length / 3;
            var areas = new float[triCount];
            var densTri = new float[triCount];
            float areaTotal = 0f;

            for (int t = 0; t < triCount; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                Vector3 a = verts[i0], b = verts[i1], c = verts[i2];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                areas[t] = Mathf.Max(area, 1e-8f);

                float d = 1f;
                if (hasCol)
                {
                    d = (cols[i0].r + cols[i1].r + cols[i2].r) / 3f;
                    d = Mathf.Pow(Mathf.Clamp01(d), Potencia);
                }
                densTri[t] = d;
                areaTotal += areas[t] * Mathf.Max(d, 0.0001f);
            }

            var rng = new System.Random(77);
            int creados = 0, intentos = 0;
            int cantidad = Mathf.Clamp(Cantidad, 100, 12000);
            int maxIntentos = cantidad * 20;
            float umbral = Umbral;

            float yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;
            for (int i = 0; i < verts.Length; i++)
            {
                float y = l2w.MultiplyPoint3x4(verts[i]).y;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }

            while (creados < cantidad && intentos < maxIntentos)
            {
                intentos++;
                int tri = ElegirTriPonderado(areas, densTri, areaTotal, rng);
                if (hasCol && densTri[tri] < umbral) continue;

                Vector3 bary = RandomBary(rng);
                int i0 = tris[tri * 3], i1 = tris[tri * 3 + 1], i2 = tris[tri * 3 + 2];
                Vector3 lp = verts[i0] * bary.x + verts[i1] * bary.y + verts[i2] * bary.z;
                Vector3 ln = hasN
                    ? (norms[i0] * bary.x + norms[i1] * bary.y + norms[i2] * bary.z).normalized
                    : Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).normalized;

                float dens = hasCol
                    ? Mathf.Pow(Mathf.Clamp01(cols[i0].r * bary.x + cols[i1].r * bary.y + cols[i2].r * bary.z), Potencia)
                    : 1f;
                if (dens < umbral) continue;
                if (hasCol && rng.NextDouble() > dens) continue;

                Vector3 wp = l2w.MultiplyPoint3x4(lp);
                Vector3 wn = l2w.MultiplyVector(ln).normalized;
                if (wn.sqrMagnitude < 0.0001f) wn = Vector3.up;
                if (Vector3.Angle(wn, Vector3.up) > 48f) continue;

                float h = Mathf.InverseLerp(yMin, yMax, wp.y);
                if (h > 0.88f && rng.NextDouble() > 0.12) continue;

                float s = EscalaGlobal * Mathf.Lerp(0.7f, 1.25f, (float)rng.NextDouble()) * Mathf.Lerp(0.9f, 1.15f, dens);
                GameObject go;
                if (usarPrefabs)
                {
                    var prefab = prefsPasto[rng.Next(prefsPasto.Count)];
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (go == null) continue;
                    Undo.RegisterCreatedObjectUndo(go, "Pasto");
                    go.name = $"Pasto_{prefab.name}_{creados:0000}";
                    go.transform.SetParent(contenedor.transform, true);
                    go.transform.localScale = Vector3.one;
                    // Anclar base del prefab a la malla
                    var rs = go.GetComponentsInChildren<Renderer>();
                    float baseOff = 0f;
                    if (rs != null && rs.Length > 0)
                    {
                        Bounds b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                        baseOff = go.transform.position.y - b.min.y;
                    }
                    go.transform.position = wp + Vector3.up * (baseOff - 0.03f);
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, wn)
                        * Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    go.transform.localScale = Vector3.one * s;
                    foreach (var c in go.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(c);
                    // Forzar material con textura (los BIRP rotos dejaban el pasto blanco/invisible)
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int mi = 0; mi < mats.Length; mi++) mats[mi] = mat;
                        r.sharedMaterials = mats;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
                else
                {
                    go = new GameObject($"Pasto_{creados:0000}");
                    Undo.RegisterCreatedObjectUndo(go, "Pasto");
                    go.transform.SetParent(contenedor.transform, true);
                    go.transform.position = wp - wn.normalized * 0.05f;
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, wn)
                        * Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    go.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble()), s);
                    go.AddComponent<MeshFilter>().sharedMesh = billboard;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                creados++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = contenedor;
            SceneView.lastActiveSceneView?.FrameSelected();

            string modo = (usarPrefabs ? "prefabs GrassDry" : "billboard")
                + (hasCol ? " + máscara Blender" : " uniforme");
            string msg = $"Pasto: {creados} matas ({modo}). Escala≈{EscalaGlobal:0.0}, pedidas={cantidad}.";
            Debug.Log($"<color=green>[DinoVolcanPasto]</color> {msg}");
            if (!silencioso) EditorUtility.DisplayDialog("DINO WEST", msg, "OK");
        }

        static void Fail(bool silencioso, string msg)
        {
            Debug.LogError("[DinoVolcanPasto] " + msg);
            if (!silencioso) EditorUtility.DisplayDialog("DINO WEST — Pasto", msg, "OK");
        }

        static GameObject BuscarContenedor()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == NombreContenedor) return t.gameObject;
            return null;
        }

        static GameObject BuscarVolcan()
        {
            GameObject best = null;
            int bestMeshes = -1;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                string n = t.name;
                if (n.IndexOf("Escenario_Volcan", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("TerrenoPasto", System.StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Volcan_Mixta", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                int m = t.GetComponentsInChildren<MeshFilter>(true).Length;
                if (m > bestMeshes) { bestMeshes = m; best = t.gameObject; }
            }
            return best;
        }

        static MeshFilter BuscarMeshSuelo(GameObject root)
        {
            MeshFilter best = null;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("pasto") || n.Contains("scatter") || n.Contains("hebra") || n.Contains("alfombra"))
                    continue;
                if (best == null || mf.sharedMesh.vertexCount > best.sharedMesh.vertexCount)
                    best = mf;
            }
            return best;
        }

        static void AsegurarMeshLegible(MeshFilter mf)
        {
            string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (string.IsNullOrEmpty(path))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(mf.gameObject);
                if (src != null) path = AssetDatabase.GetAssetPath(src);
            }
            if (string.IsNullOrEmpty(path)) return;
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            bool dirty = false;
            if (!mi.isReadable) { mi.isReadable = true; dirty = true; }
            // Importar vertex colors
            if (mi.importBlendShapeNormals != ModelImporterNormals.None) { /* keep */ }
            if (dirty) mi.SaveAndReimport();
            else
            {
                // Forzar reimport si no hay colors aún y el fbx los trae
                mi.SaveAndReimport();
            }
        }

        static Material AsegurarMaterialBillboard()
        {
            EnsureFolder();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatAsset);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = "M_PastoVolcan_Billboard" };
                AssetDatabase.CreateAsset(mat, MatAsset);
            }

            // Preferir textura real del pack; si no, el PNG procedural (solo fallback)
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexGrassDryPack);
            if (tex == null)
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexAsset);
                if (tex == null)
                {
                    tex = GenerarTexturaPasto();
                    File.WriteAllBytes(Path.GetFullPath(TexAsset), tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(TexAsset);
                    var ti = AssetImporter.GetAtPath(TexAsset) as TextureImporter;
                    if (ti != null) { ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexAsset);
                }
            }

            mat.mainTexture = tex;
            AplicarAspectoMaterial(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static Mesh AsegurarMeshBillboard()
        {
            EnsureFolder();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAsset);
            bool crear = mesh == null;
            if (crear) mesh = new Mesh { name = "PastoBillboard" };

            // Base bajo y=0 para que las matas nazcan del suelo (sin hueco visual)
            float w = 0.35f, h = 0.7f, y0 = -0.05f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
            Quad(new Vector3(-w, y0, 0), new Vector3(w, y0, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0), Vector3.forward);
            Quad(new Vector3(0, y0, -w), new Vector3(0, y0, w), new Vector3(0, h, w), new Vector3(0, h, -w), Vector3.right);
            mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            if (crear)
            {
                AssetDatabase.CreateAsset(mesh, MeshAsset);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
            return mesh;
        }

        static Texture2D GenerarTexturaPasto()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) tex.SetPixel(x, y, Color.clear);
            var rng = new System.Random(3);
            for (int i = 0; i < 18; i++)
            {
                float cx = 0.15f + (float)rng.NextDouble() * 0.7f;
                float baseW = 0.015f + (float)rng.NextDouble() * 0.025f;
                var col = Color.Lerp(new Color(0.45f, 0.22f, 0.05f, 1f), new Color(0.8f, 0.45f, 0.12f, 1f), (float)rng.NextDouble());
                for (int y = 0; y < s; y++)
                {
                    float vv = y / (float)(s - 1);
                    float sway = Mathf.Sin(vv * 6f + i) * 0.04f * vv;
                    float half = baseW * (1f - vv * 0.85f);
                    for (int x = 0; x < s; x++)
                    {
                        float u = x / (float)(s - 1);
                        if (Mathf.Abs(u - (cx + sway)) <= half)
                        {
                            var c = col; c.a = Mathf.Clamp01(1.2f - vv);
                            if (c.a > tex.GetPixel(x, y).a) tex.SetPixel(x, y, c);
                        }
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Dino/Models/Environment/Volcan"))
                AssetDatabase.CreateFolder("Assets/Dino/Models/Environment", "Volcan");
            if (!AssetDatabase.IsValidFolder(CarpetaGen))
                AssetDatabase.CreateFolder("Assets/Dino/Models/Environment/Volcan", "Generated");
        }

        static int ElegirTriPonderado(float[] areas, float[] dens, float total, System.Random rng)
        {
            float r = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < areas.Length; i++)
            {
                acc += areas[i] * Mathf.Max(dens[i], 0.0001f);
                if (r <= acc) return i;
            }
            return areas.Length - 1;
        }

        static Vector3 RandomBary(System.Random rng)
        {
            float u = (float)rng.NextDouble();
            float v = (float)rng.NextDouble();
            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
            return new Vector3(u, v, 1f - u - v);
        }
    }

    public class DinoVolcanPastoDensityWindow : EditorWindow
    {
        Vector2 _scroll;

        public static void Open()
        {
            var w = GetWindow<DinoVolcanPastoDensityWindow>("Pasto Volcán");
            w.minSize = new Vector2(380, 520);
            w.Show();
            w.Focus();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Label("DINO WEST · Pasto del volcán", EditorStyles.boldLabel);

            // --- Acciones principales SIEMPRE arriba ---
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.4f);
            if (GUILayout.Button("APLICAR / REPOBLAR con estos ajustes", GUILayout.Height(40)))
            {
                if (EditorSceneManager.GetActiveScene().path != DinoVolcanPastoScatter.Escena)
                    EditorSceneManager.OpenScene(DinoVolcanPastoScatter.Escena);
                DinoVolcanPastoScatter.AplicarColorYMaterialAlScatter();
                DinoVolcanPastoScatter.Poblar(silencioso: false);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Solo aplicar tinte/textura (sin regenerar matas)", GUILayout.Height(28)))
                DinoVolcanPastoScatter.AplicarColorYMaterialAlScatter();

            EditorGUILayout.Space(8);
            GUILayout.Label("Densidad", EditorStyles.boldLabel);
            DinoVolcanPastoScatter.Cantidad = EditorGUILayout.IntSlider(
                new GUIContent("Cantidad", "Número de matas"),
                DinoVolcanPastoScatter.Cantidad, 200, 8000);
            DinoVolcanPastoScatter.Umbral = EditorGUILayout.Slider(
                new GUIContent("Umbral", "Peso mínimo Blender"),
                DinoVolcanPastoScatter.Umbral, 0f, 0.8f);
            DinoVolcanPastoScatter.Potencia = EditorGUILayout.Slider(
                new GUIContent("Contraste", ">1 más concentrado"),
                DinoVolcanPastoScatter.Potencia, 0.5f, 3f);
            DinoVolcanPastoScatter.EscalaGlobal = EditorGUILayout.Slider(
                new GUIContent("Escala", "Tamaño de cada mata GrassDry (≈2–3 se ve bien en el volcán)"),
                DinoVolcanPastoScatter.EscalaGlobal, 0.8f, 4f);

            EditorGUILayout.Space(6);
            GUILayout.Label("Aspecto", EditorStyles.boldLabel);
            DinoVolcanPastoScatter.ColorPasto = EditorGUILayout.ColorField("Tinte", DinoVolcanPastoScatter.ColorPasto);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Dino/Models/Environment/Volcan/Generated/M_PastoVolcan_Billboard.mat");
            EditorGUILayout.ObjectField("Material", mat, typeof(Material), false);

            var texActual = mat != null ? mat.mainTexture as Texture2D : null;
            EditorGUI.BeginChangeCheck();
            var texNueva = (Texture2D)EditorGUILayout.ObjectField("Textura", texActual, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && mat != null && texNueva != null)
            {
                mat.mainTexture = texNueva;
                EditorUtility.SetDirty(mat);
                DinoVolcanPastoScatter.AplicarColorYMaterialAlScatter();
            }

            if (GUILayout.Button("Usar textura GrassDry del pack", GUILayout.Height(26)))
                DinoVolcanPastoScatter.UsarTexturaGrassDryDelPack();

            EditorGUILayout.Space(8);
            // Botón repetido abajo por si cambió sliders y quiere aplicar sin subir
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.4f);
            if (GUILayout.Button("APLICAR / REPOBLAR con estos ajustes", GUILayout.Height(36)))
            {
                if (EditorSceneManager.GetActiveScene().path != DinoVolcanPastoScatter.Escena)
                    EditorSceneManager.OpenScene(DinoVolcanPastoScatter.Escena);
                DinoVolcanPastoScatter.AplicarColorYMaterialAlScatter();
                DinoVolcanPastoScatter.Poblar(silencioso: false);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Borrar pasto del volcán"))
            {
                var cont = GameObject.Find(DinoVolcanPastoScatter.NombreContenedor);
                if (cont == null)
                {
                    foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                    {
                        if (t != null && t.name == DinoVolcanPastoScatter.NombreContenedor)
                        { cont = t.gameObject; break; }
                    }
                }
                if (cont != null)
                {
                    Undo.DestroyObjectImmediate(cont);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "No uses el material magenta Grass_Dry (URP).\n" +
                "Cambia solo la Textura o pulsa «Usar textura GrassDry del pack».\n\n" +
                "Tras mover Cantidad / Umbral / Escala / Tinte → pulsa APLICAR / REPOBLAR.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }
    }
}
