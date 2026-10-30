using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle.Editor
{
    public static class TakeScreenshot
    {
        [MenuItem("Dino Vehicle/Tomar Capturas")]
        public static void CaptureScreenshots()
        {
            EditorSceneManager.OpenScene("Assets/Dino/Scenes/DinoTestTrack.unity");

            GameObject dino = GameObject.Find("Carnotaurus_Vehicle");
            if (dino == null)
            {
                Debug.LogError("No se encontro Carnotaurus_Vehicle en la escena.");
                return;
            }

            Vector3 dinoPos = dino.transform.position + Vector3.up * 1.5f;

            // 1. Captura Vista Tercera Persona (Conducción / Pista)
            Vector3 camPos1 = dinoPos - dino.transform.forward * 7.0f + Vector3.up * 2.8f + dino.transform.right * 1.8f;
            Quaternion camRot1 = Quaternion.LookRotation((dinoPos + dino.transform.forward * 4.0f) - camPos1);
            RenderShot(camPos1, camRot1, "Captura_Conduccion.png");

            // 2. Captura Primer Plano (Detalle 3/4 del Carnotauro con Montura)
            Vector3 camPos2 = dinoPos + dino.transform.forward * 4.2f + Vector3.up * 1.2f + dino.transform.right * 4.0f;
            Quaternion camRot2 = Quaternion.LookRotation(dinoPos - camPos2);
            RenderShot(camPos2, camRot2, "Captura_Detalle_Montura.png");

            Debug.Log("? ¡Capturas de pantalla generadas exitosamente!");
        }

        private static void RenderShot(Vector3 pos, Quaternion rot, string filename)
        {
            GameObject tempCamObj = new GameObject("TempCaptureCam");
            Camera cam = tempCamObj.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.position = pos;
            cam.transform.rotation = rot;

            RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            cam.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tempCamObj);

            byte[] bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(filename, bytes);

            // Copiar también al directorio de la conversación para visualización
            string brainDir = @"C:\Users\juanj\.gemini\antigravity\brain\0eb8d341-c5a3-45a2-92c5-5d68dbf449c8";
            if (Directory.Exists(brainDir))
            {
                File.WriteAllBytes(Path.Combine(brainDir, filename), bytes);
            }
        }
    }
}
