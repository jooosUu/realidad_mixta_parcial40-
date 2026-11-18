using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Material volcánico Built-in + limpieza del pasto-alfombra naranja que arruinaba el look.
    /// </summary>
    public static class DinoVolcanMaterialsSetup
    {
        const string CarpetaTex = "Assets/Dino/Models/Environment/Volcan/Textures";
        const string CarpetaMat = "Assets/Dino/Materials/Volcan";
        const string Escena = "Assets/Dino/Scenes/Simulation Vulkan.unity";
        const string MatSuelo = CarpetaMat + "/M_Volcan_Suelo.mat";
        const string MatPasto = CarpetaMat + "/M_Volcan_Pasto.mat";

        public static void MenuReparar()
        {
            CrearMateriales();
            if (EditorSceneManager.GetActiveScene().path != Escena)
                EditorSceneManager.OpenScene(Escena);
            RepararEscena(mostrarDialogo: true);
        }

        [InitializeOnLoadMethod]
        static void Auto()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (EditorSceneManager.GetActiveScene().path != Escena) return;
                if (BuscarRoot() == null) return;
                if (AssetDatabase.LoadAssetAtPath<Material>(MatSuelo) == null)
                    CrearMateriales();
                RepararEscena(mostrarDialogo: false);
            };
        }

        public static void CrearMateriales()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Dino/Materials"))
                AssetDatabase.CreateFolder("Assets/Dino", "Materials");
            if (!AssetDatabase.IsValidFolder(CarpetaMat))
                AssetDatabase.CreateFolder("Assets/Dino/Materials", "Volcan");

            string albedo = FindTex("Volcanic_rock_BaseColor", "BaseColor");
            string normal = FindTex("Volcanic_rock_Normal", "Normal");
            if (!string.IsNullOrEmpty(normal)) MarcarNormal(normal);

            var standard = Shader.Find("Standard");
            var suelo = AssetDatabase.LoadAssetAtPath<Material>(MatSuelo);
            if (suelo == null)
            {
                suelo = new Material(standard) { name = "M_Volcan_Suelo" };
                AssetDatabase.CreateAsset(suelo, MatSuelo);
            }
            suelo.shader = standard;
            if (!string.IsNullOrEmpty(albedo))
                suelo.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
            if (!string.IsNullOrEmpty(normal))
            {
                suelo.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                suelo.EnableKeyword("_NORMALMAP");
            }
            // Tile: el mesh ~90m necesita repetición o se ve "plástico/plano"
            suelo.mainTextureScale = new Vector2(10f, 10f);
            suelo.SetTextureScale("_BumpMap", new Vector2(10f, 10f));
            suelo.SetFloat("_Glossiness", 0.2f);
            suelo.SetFloat("_Metallic", 0f);
            suelo.color = Color.white;
            EditorUtility.SetDirty(suelo);

            // Pasto ya no se usa como alfombra; dejamos el mat por si hay hebras futuras
            var pasto = AssetDatabase.LoadAssetAtPath<Material>(MatPasto);
            if (pasto == null)
            {
                pasto = new Material(standard) { name = "M_Volcan_Pasto" };
                AssetDatabase.CreateAsset(pasto, MatPasto);
            }
            pasto.shader = standard;
            pasto.color = new Color(0.35f, 0.18f, 0.08f, 1f);
            pasto.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(pasto);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[DinoVolcanMaterials]</color> M_Volcan_Suelo listo (tile 10x).");
        }

        public static void RepararEscena(bool mostrarDialogo)
        {
            var root = BuscarRoot();
            if (root == null)
            {
                if (mostrarDialogo)
                    EditorUtility.DisplayDialog("DINO WEST", "No hay Escenario_Volcan en la escena.", "OK");
                return;
            }

            CrearMateriales();
            var suelo = AssetDatabase.LoadAssetAtPath<Material>(MatSuelo);

            int borrados = 0;
            // Solo borrar el pasto-alfombra malo (triángulos naranjas), NO el scatter GrassDry
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var hijo = root.transform.GetChild(i).gameObject;
                string n = hijo.name.ToLowerInvariant();
                if (n.Contains("alfombra") || n.Contains("hebra") || n == "pasto_volcan" || n.Contains("pasto_volcan_mesh"))
                {
                    Undo.DestroyObjectImmediate(hijo);
                    borrados++;
                }
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // No tocar matas de pasto del scatter
                if (EsObjetoPastoScatter(r.transform)) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("alfombra") || n.Contains("hebra")) continue;
                Undo.RecordObject(r, "Suelo volcán");
                r.sharedMaterial = suelo;
                EditorUtility.SetDirty(r);
            }

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (EsObjetoPastoScatter(mf.transform)) continue;
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("alfombra") || n.Contains("hebra")) continue;
                foreach (var c in mf.GetComponents<Collider>())
                    Object.DestroyImmediate(c);
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            string msg = $"Visual reparado en '{root.name}'. Pasto naranja eliminado: {borrados}. Suelo con tile 10×.";
            Debug.Log($"<color=green>[DinoVolcanMaterials]</color> {msg}");
            if (mostrarDialogo)
                EditorUtility.DisplayDialog(
                    "DINO WEST",
                    msg + "\n\nSi la textura sigue rara: borra el objeto del Hierarchy, " +
                    "espera a que Unity reimporte el FBX nuevo y vuelve a Importar Escenario Volcán.",
                    "OK");
        }

        static bool EsObjetoPastoScatter(Transform t)
        {
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("pasto_volcan_scatter") || n.StartsWith("pasto_"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        static GameObject BuscarRoot()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                string n = t.name;
                if (n == "Escenario_Volcan_Mixta" || n.Contains("Escenario_Volcan") || n.Contains("TerrenoPasto"))
                    return t.gameObject;
            }
            return null;
        }

        static string FindTex(params string[] needles)
        {
            if (!Directory.Exists(CarpetaTex)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaTex }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileName(path);
                bool ok = true;
                foreach (var n in needles)
                {
                    if (name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) < 0) { ok = false; break; }
                }
                if (ok) return path;
            }
            return null;
        }

        static void MarcarNormal(string assetPath)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null || ti.textureType == TextureImporterType.NormalMap) return;
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
    }
}
