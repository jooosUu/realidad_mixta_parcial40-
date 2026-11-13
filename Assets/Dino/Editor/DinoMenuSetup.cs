using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Prepara los recursos de los menús de DINO WEST:
/// importa TMP Essentials si faltan, configura las texturas como sprites,
/// crea las fuentes TMP (Bahnschrift Condensed) y agrega las escenas a Build Settings.
/// Menú: DINO WEST ▸ Preparar menús.
/// </summary>
public static class DinoMenuSetup
{
    const string Carpeta = "Assets/Dino/UI/Resources/DinoUI";
    static readonly string[] Fuentes = { "Bahnschrift-BoldCondensed", "Bahnschrift-SemiBoldCondensed", "Bahnschrift-LightCondensed" };
    static readonly string[] Escenas = { "Assets/Dino/Scenes/Simulation.unity", "Assets/Dino/Scenes/DinoTestTrack.unity" };

    [MenuItem("DINO WEST/Preparar menús")]
    public static void Preparar()
    {
        if (!HayTmpEssentials())
        {
            ImportarTmp();
            Debug.Log("[DinoMenuSetup] TMP Essentials importados. Vuelve a ejecutar DINO WEST ▸ Preparar menús para crear las fuentes.");
            return;
        }
        Texturas();
        CrearFuentes();
        CrearPiso();
        BuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[DinoMenuSetup] Menús listos.");
    }

    static bool HayTmpEssentials() => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

    public static void ImportarTmp()
    {
        if (HayTmpEssentials()) return;
        // Equivale a Window ▸ TextMeshPro ▸ Import TMP Essential Resources.
        // (En modo batch usar mejor el argumento -importPackage con este mismo archivo.)
        string paquete = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
        AssetDatabase.ImportPackage(paquete, false);
    }

    static void Texturas()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
    }

    const string Caracteres = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]_abcdefghijklmnopqrstuvwxyz|" +
                              "ÁÉÍÓÚÜÑáéíóúüñ¿¡×²·°–—…";

    /// <summary>
    /// Fuentes TMP con atlas estático (todos los caracteres del menú ya dibujados).
    /// Con atlas dinámico las tres variantes de Bahnschrift se cruzaban en tiempo de ejecución.
    /// </summary>
    static void CrearFuentes()
    {
        foreach (var n in Fuentes)
        {
            string destino = $"{Carpeta}/{n} SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(destino) != null) continue;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{Carpeta}/{n}.ttf");
            if (font == null) { Debug.LogError($"[DinoMenuSetup] Falta {n}.ttf"); continue; }
            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fa.name = n + " SDF";
            fa.TryAddCharacters(Caracteres, out string faltan);
            if (!string.IsNullOrEmpty(faltan)) Debug.LogWarning($"[DinoMenuSetup] {n}: faltan {faltan}");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(fa, destino);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                fa.atlasTextures[i].name = n + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }
            fa.material.name = n + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>Material del piso del escenario de inicio (en Resources para que entre en la compilación).</summary>
    static void CrearPiso()
    {
        string ruta = Carpeta + "/PisoEscenario.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = m == null;
        if (nuevo) m = new Material(Shader.Find("Standard"));
        ColorUtility.TryParseHtmlString("#120C09", out var c);
        m.color = c;
        m.SetFloat("_Glossiness", 0f);
        m.SetFloat("_GlossyReflections", 0f);   // sin reflejo del cielo: el piso se funde con el fondo
        m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        if (nuevo) AssetDatabase.CreateAsset(m, ruta); else EditorUtility.SetDirty(m);
    }

    static void BuildSettings()
    {
        var actuales = EditorBuildSettings.scenes.ToList();
        foreach (var e in Escenas.Reverse())
        {
            if (!File.Exists(e)) continue;
            actuales.RemoveAll(s => s.path == e);
            actuales.Insert(0, new EditorBuildSettingsScene(e, true));
        }
        EditorBuildSettings.scenes = actuales.ToArray();
    }
}
