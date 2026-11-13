using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reasigna los materiales del carnotauro por nombre de pieza.
/// En las escenas el cuerpo tenía el material de los ojos y la piel estaba en la manta, estribos, lazo y rifle.
/// Menú: DINO WEST ▸ Corregir materiales del carnotauro (aplica a Simulation y DinoTestTrack).
/// </summary>
public static class DinoCorregirMateriales
{
    const string M = "Assets/Dino/Materials/";
    static readonly string[] Escenas = { "Assets/Dino/Scenes/Simulation.unity", "Assets/Dino/Scenes/DinoTestTrack.unity" };

    [MenuItem("DINO WEST/Corregir materiales del carnotauro")]
    public static void Corregir()
    {
        var piel = AssetDatabase.LoadAssetAtPath<Material>(M + "M_DW_Carnotauro_Piel.mat");
        var ojos = AssetDatabase.LoadAssetAtPath<Material>(M + "M_DW_Carnotauro_Ojos.mat");
        var cuero = AssetDatabase.LoadAssetAtPath<Material>(M + "M_DW_Silla_Cuero.mat");
        var hierro = AssetDatabase.LoadAssetAtPath<Material>(M + "M_DW_Hierro_Cadenas.mat");
        var manta = Crear("M_DW_Manta_Navajo", "Assets/Dino/Textures/DW_Tex_Manta_Navajo.png", 0.15f);
        var madera = Crear("M_DW_Madera", "Assets/Dino/Textures/DW_Tex_Madera.png", 0.25f);

        string actual = EditorSceneManager.GetActiveScene().path;
        foreach (var ruta in Escenas)
        {
            var escena = EditorSceneManager.OpenScene(ruta);
            int cambios = 0;
            foreach (var v in Object.FindObjectsByType<DinoVehicle.DinoVehicleController>(FindObjectsSortMode.None))
            {
                foreach (var r in v.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = Elegir(r.name, piel, ojos, cuero, hierro, manta, madera);
                    if (m == null || r.sharedMaterial == m) continue;
                    Undo.RecordObject(r, "Corregir material");
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = m;
                    r.sharedMaterials = mats;
                    cambios++;
                }
            }
            if (cambios > 0) { EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
            Debug.Log($"[DinoCorregirMateriales] {ruta}: {cambios} piezas corregidas");
        }
        if (!string.IsNullOrEmpty(actual)) EditorSceneManager.OpenScene(actual);
    }

    static Material Elegir(string n, Material piel, Material ojos, Material cuero, Material hierro, Material manta, Material madera)
    {
        if (n.Contains("Cuerpo") || n.Contains("Body")) return piel;
        if (n.Contains("Eyes") || n.Contains("Ojos")) return ojos;
        if (n.Contains("Manta")) return manta;
        if (n.Contains("Culata") || n.Contains("Estribo_")) return madera;
        if (n.Contains("Argollas") || n.Contains("Conchos") || n.Contains("Hebillas") || n.Contains("Cadena") || n.Contains("Concho")) return hierro;
        if (n.StartsWith("DW_Silla") || n.StartsWith("DW_Cabezada")) return cuero;
        return null;
    }

    static Material Crear(string nombre, string textura, float brillo)
    {
        string ruta = M + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard")) { name = nombre };
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textura);
        m.SetFloat("_Glossiness", brillo);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }
}
