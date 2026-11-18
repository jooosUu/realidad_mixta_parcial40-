using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Corrige el "todo blanco" (luz/skybox/materiales) y deja el pasto usable.
    /// </summary>
    public static class DinoVolcanLightingRepair
    {
        const string Escena = "Assets/Dino/Scenes/Simulation Vulkan.unity";
        const string SkyboxPrefab = "Assets/TerrainDemoScene_URP/Prefabs/Skybox/SkyboxMountains.prefab";

        public static void Reparar()
        {
            if (EditorSceneManager.GetActiveScene().path != Escena)
                EditorSceneManager.OpenScene(Escena);

            // Pipeline completo: sol volcánico + pasto fuera del ×100 + decor + materiales
            DinoVolcanEnvironmentSetup.AplicarTodoSilencioso();
            AsegurarSol();
            AsegurarSkyboxYNiebla();
            RepararMaterialesFaunaYProps();
            DinoVolcanMaterialsSetup.CrearMateriales();
            DinoVolcanMaterialsSetup.RepararEscena(mostrarDialogo: false);
            RepararMaterialPasto();

            var pasto = GameObject.Find(DinoVolcanPastoScatter.NombreContenedor);
            int matas = pasto != null ? pasto.transform.childCount : 0;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "DINO WEST — Iluminación",
                "Se aplicó el ambiente volcánico completo:\n" +
                "• Sol ámbar + niebla\n" +
                "• Pasto anclado (sin heredar ×100 del volcán)\n" +
                "• 3 decoraciones + materiales BIRP\n\n" +
                "EN SCENE VIEW: activa el icono de iluminación (bombilla).\n" +
                $"Pasto: {matas} matas.",
                "OK");
        }

        static void AsegurarSol()
        {
            Light sol = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional)
                {
                    sol = l;
                    break;
                }
            }

            if (sol == null)
            {
                var go = new GameObject("Directional Light");
                sol = go.AddComponent<Light>();
                sol.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(go, "Sol");
            }

            sol.gameObject.SetActive(true);
            sol.enabled = true;
            sol.color = new Color(1f, 0.95f, 0.88f);
            sol.intensity = 1.35f;
            sol.shadows = LightShadows.Soft;
            sol.transform.rotation = Quaternion.Euler(48f, 38f, 0f);
            RenderSettings.sun = sol;
            EditorUtility.SetDirty(sol);
            Debug.Log("<color=green>[DinoVolcanLight]</color> Directional Light OK");
        }

        static void AsegurarSkyboxYNiebla()
        {
            // Niebla suave (antes se veía “vacío blanco”)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0028f;
            RenderSettings.fogColor = new Color(0.65f, 0.72f, 0.78f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.40f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.18f, 0.14f);
            RenderSettings.ambientIntensity = 1.05f;

            // Horizonte montañoso del pack (si existe)
            var existing = GameObject.Find("SkyboxMountains_DinoWest");
            if (existing == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkyboxPrefab);
                if (prefab != null)
                {
                    var sky = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    sky.name = "SkyboxMountains_DinoWest";
                    sky.transform.position = Vector3.zero;
                    Undo.RegisterCreatedObjectUndo(sky, "Skybox");
                    DinoMaterialesBIRP.Aplicar(sky);
                }
            }
            else
            {
                existing.SetActive(true);
            }

            // Skybox procedural de respaldo
            var skyMat = RenderSettings.skybox;
            if (skyMat == null)
            {
                // Default-Skybox builtin
                skyMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                RenderSettings.skybox = skyMat;
            }

            DynamicGI.UpdateEnvironment();
            Debug.Log("<color=green>[DinoVolcanLight]</color> Skybox / ambient / fog OK");
        }

        static void RepararMaterialesFaunaYProps()
        {
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null) continue;
                // No tocar el scatter de pasto billboard
                if (r.transform.root.name.Contains("Pasto_Volcan_Scatter") ||
                    (r.transform.parent != null && r.transform.parent.name.Contains("Pasto_Volcan_Scatter")))
                    continue;
                if (r.gameObject.name.StartsWith("Pasto_")) continue;

                DinoMaterialesBIRP.Aplicar(r.gameObject);
                n++;
            }
            Debug.Log($"<color=green>[DinoVolcanLight]</color> Materiales BIRP revisados en {n} renderers");
        }

        static void RepararMaterialPasto()
        {
            const string path = "Assets/Dino/Models/Environment/Volcan/Generated/M_PastoVolcan_Billboard.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) return;

            var standard = Shader.Find("Standard");
            if (standard == null) return;

            mat.shader = standard;
            mat.SetFloat("_Mode", 1); // Cutout
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetFloat("_Cutoff", 0.4f);
            mat.SetFloat("_Glossiness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            mat.color = DinoVolcanPastoScatter.ColorPasto;
            // Doble cara aproximada: no hay, pero evita rosa
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            var cont = GameObject.Find(DinoVolcanPastoScatter.NombreContenedor);
            if (cont != null)
            {
                foreach (var r in cont.GetComponentsInChildren<MeshRenderer>(true))
                    r.sharedMaterial = mat;
            }
        }
    }
}
