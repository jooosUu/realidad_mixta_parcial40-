using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Saca una foto de cada escena de las Build Settings para la vista previa del menú "Escenario".
    /// Se guardan en Assets/Dino/UI/Resources/DinoUI/Mapas/&lt;NombreEscena&gt;.png (las carga DinoMenuSystem).
    /// Hay que repetirlo si cambia mucho un mapa o se añade uno nuevo.
    /// </summary>
    public static class DinoPreviewMapas
    {
        const string Carpeta = "Assets/Dino/UI/Resources/DinoUI/Mapas";
        const int Ancho = 1280, Alto = 720;

        [MenuItem("DINO WEST/Generar vistas previas de mapas")]
        public static void Generar()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string abierta = EditorSceneManager.GetActiveScene().path;
            Directory.CreateDirectory(Carpeta);

            int n = 0;
            foreach (var e in EditorBuildSettings.scenes)
            {
                if (!e.enabled || !File.Exists(e.path)) continue;
                EditorSceneManager.OpenScene(e.path);
                string archivo = $"{Carpeta}/{Path.GetFileNameWithoutExtension(e.path)}.png";
                File.WriteAllBytes(archivo, Foto());
                n++;
                Debug.Log($"[DinoPreviewMapas] {archivo}");
            }

            AssetDatabase.Refresh();
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g));
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 1280;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }

            if (!string.IsNullOrEmpty(abierta)) EditorSceneManager.OpenScene(abierta);
            Debug.Log($"<color=green>[DinoPreviewMapas]</color> {n} vistas previas generadas en {Carpeta}");
        }

        static byte[] Foto()
        {
            var go = new GameObject("CamPreviewMapa");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.Skybox;

            // Desde atrás y arriba del dino, mirando hacia adelante: se ve la salida y el mapa.
            var veh = Object.FindAnyObjectByType<DinoVehicleController>();
            if (veh != null)
            {
                Transform t = veh.transform;
                Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                Vector3 der = Vector3.Cross(Vector3.up, fwd);
                // Alta, por encima de las copas: así ningún árbol tapa la cámara.
                go.transform.position = t.position - fwd * 42f + Vector3.up * 40f + der * 10f;
                go.transform.rotation = Quaternion.LookRotation(t.position + fwd * 70f - go.transform.position);
            }
            else
            {
                var terr = Terrain.activeTerrain;
                Vector3 c = terr != null ? terr.transform.position + terr.terrainData.size * 0.5f : Vector3.zero;
                go.transform.position = c + new Vector3(0f, 120f, -200f);
                go.transform.rotation = Quaternion.LookRotation(c - go.transform.position);
            }

            var rt = new RenderTexture(Ancho, Alto, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Ancho, Alto, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            return png;
        }
    }
}
