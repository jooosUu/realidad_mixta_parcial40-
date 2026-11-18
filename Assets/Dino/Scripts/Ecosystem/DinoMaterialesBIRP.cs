using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DinoVehicle
{
    /// <summary>
    /// Garantiza materiales compatibles con Built-in RP (Standard).
    /// Extrae albedo/normal aunque el origen sea URP/HDRP (_BaseMap/_BaseColorMap).
    /// Regenera copias BIRP rotas (sin textura → blancas/invisibles).
    /// </summary>
    public static class DinoMaterialesBIRP
    {
        const string Carpeta = "Assets/Dino/Materials/BIRP";
        static readonly Dictionary<Material, Material> cache = new();

        static readonly string[] PalabrasHoja =
        {
            "leaf", "leaves", "hoja", "branch", "needle", "frond", "billboard",
            "grass", "fern", "bush", "foliage", "heather", "shrub", "pine"
        };
        static readonly string[] PalabrasTronco = { "bark", "trunk", "wood", "tronco", "rock", "stone" };

        static bool EsHoja(string nombreMaterial)
        {
            string n = nombreMaterial.ToLowerInvariant();
            foreach (var t in PalabrasTronco) if (n.Contains(t)) return false;
            foreach (var p in PalabrasHoja) if (n.Contains(p)) return true;
            return false;
        }

        static bool HojaOpaca(Material m)
        {
            return m.shader != null && m.shader.name == "Standard" && m.HasProperty("_Mode")
                && m.GetFloat("_Mode") < 0.5f && m.mainTexture != null && EsHoja(m.name);
        }

        /// <summary>Standard con textura solo en slots HDRP/URP → se ve blanco.</summary>
        static bool StandardSinMainTex(Material m)
        {
            if (m == null || m.shader == null || !m.shader.name.StartsWith("Standard")) return false;
            if (m.mainTexture != null) return false;
            return BuscarAlbedo(m) != null;
        }

        public static void Aplicar(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool cambio = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var c = Convertir(mats[i]);
                    if (c != mats[i]) { mats[i] = c; cambio = true; }
                    else if (c != null) RemapInlineSiHaceFalta(c);
                }
                if (cambio) r.sharedMaterials = mats;
            }
        }

        /// <summary>Recorre toda la escena y corrige materiales blancos/rotos.</summary>
        public static int RepararTodaLaEscena()
        {
            cache.Clear();
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null) continue;
                Aplicar(r.gameObject);
                n++;
            }
#if UNITY_EDITOR
            AssetDatabase.SaveAssets();
#endif
            return n;
        }

        public static bool EsIncompatible(Material m)
        {
            if (m == null) return false;
            var sh = m.shader;
            if (sh == null || !sh.isSupported) return true;
            string n = sh.name;
            return n == "Hidden/InternalErrorShader"
                || n.Contains("Universal Render Pipeline")
                || n.Contains("HDRP")
                || n.StartsWith("Shader Graphs/")
                || n.StartsWith("URP")
                || n.Contains("Lit") && !n.StartsWith("Standard");
        }

        static bool TieneMascaraURP(Material m)
        {
            if (m == null || m.shader == null || !m.shader.name.StartsWith("Standard")) return false;
            if (!m.HasProperty("_MetallicGlossMap")) return false;
            var t = m.GetTexture("_MetallicGlossMap");
            return t != null && t.name.IndexOf("mask", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static Material Convertir(Material m)
        {
            if (m == null) return null;
            if (m.name.StartsWith("BIRP_"))
            {
                // Regenerar si la copia BIRP quedó sin textura
                if (m.mainTexture == null)
                {
                    RemapInlineSiHaceFalta(m);
                    if (m.mainTexture == null) AsignarTexturaConocida(m);
                }
                return m;
            }
            if (cache.TryGetValue(m, out var c) && c != null)
            {
                if (c.mainTexture == null) RemapInlineSiHaceFalta(c);
                return c;
            }

            Material res = m;
            bool incompatible = EsIncompatible(m);
            if (incompatible || TieneMascaraURP(m) || HojaOpaca(m) || StandardSinMainTex(m))
                res = CrearCopia(m, incompatible || StandardSinMainTex(m));
            else
                RemapInlineSiHaceFalta(m);

            cache[m] = res;
            return res;
        }

        static void RemapInlineSiHaceFalta(Material m)
        {
            if (m == null) return;
            if (m.mainTexture == null)
            {
                var albedo = BuscarAlbedo(m);
                if (albedo != null)
                {
                    m.mainTexture = albedo;
                    if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                }
            }
            if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") == null)
            {
                var nrm = BuscarNormal(m);
                if (nrm != null)
                {
                    m.SetTexture("_BumpMap", nrm);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
        }

        static void AsignarTexturaConocida(Material m)
        {
#if UNITY_EDITOR
            string n = m.name.ToLowerInvariant();
            string path = null;
            if (n.Contains("grass"))
                path = "Assets/TerrainDemoScene_URP/Prefabs/Details/Textures/Grass_Dry_BaseColor.tif";
            else if (n.Contains("bark") || n.Contains("wood"))
                path = "Assets/Trees Collection Asset PBR/Materials/TexturesCom_Wood_BarkBirch2_0.9x1.8_512_albedo.tif";
            else if (n.Contains("leaf") || n.Contains("leaves"))
            {
                // buscar cualquier albedo de hojas del pack
                var guids = AssetDatabase.FindAssets("t:Texture2D Leaves", new[] { "Assets/Trees Collection Asset PBR" });
                if (guids != null && guids.Length > 0)
                    path = AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            if (path != null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) m.mainTexture = tex;
            }
#endif
        }

        static Material CrearCopia(Material m, bool desdeOtroShader)
        {
            string nombre = "BIRP_" + Limpiar(m.name);
#if UNITY_EDITOR
            string ruta = Carpeta + "/" + nombre + ".mat";
            if (!Application.isPlaying)
            {
                var existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (existente != null)
                {
                    // Si está roto (sin albedo), borrar y recrear
                    if (existente.mainTexture != null) return existente;
                    AssetDatabase.DeleteAsset(ruta);
                }
            }
#endif
            var standard = Shader.Find("Standard");
            Material n = new Material(standard) { name = nombre };

            var albedo = BuscarAlbedo(m);
            if (albedo != null) n.mainTexture = albedo;
            Color col = Color.white;
            if (m.HasProperty("_BaseColor")) col = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) col = m.GetColor("_Color");
            col.a = 1f;
            // Evitar blanco puro sin textura
            if (albedo == null && col.maxColorComponent > 0.95f)
                col = EsHoja(m.name) ? new Color(0.35f, 0.45f, 0.15f) : new Color(0.45f, 0.32f, 0.2f);
            n.color = col;

            var normal = BuscarNormal(m);
            if (normal != null)
            {
                n.SetTexture("_BumpMap", normal);
                n.EnableKeyword("_NORMALMAP");
            }

            // Copiar tiling del slot origen si existe
            CopiarTiling(m, n);

            n.SetFloat("_Metallic", 0f);
            n.SetFloat("_Glossiness", 0.12f);

            if (EsHoja(m.name) || nombre.ToLowerInvariant().Contains("grass") || nombre.ToLowerInvariant().Contains("leaf"))
            {
                n.SetFloat("_Mode", 1f);
                n.SetOverrideTag("RenderType", "TransparentCutout");
                n.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                n.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                n.SetInt("_ZWrite", 1);
                n.EnableKeyword("_ALPHATEST_ON");
                n.DisableKeyword("_ALPHABLEND_ON");
                n.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                n.SetFloat("_Cutoff", 0.35f);
                n.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            if (n.mainTexture == null) AsignarTexturaConocida(n);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Dino/Materials"))
                    AssetDatabase.CreateFolder("Assets/Dino", "Materials");
                if (!AssetDatabase.IsValidFolder(Carpeta))
                    AssetDatabase.CreateFolder("Assets/Dino/Materials", "BIRP");
                AssetDatabase.CreateAsset(n, ruta);
            }
#endif
            return n;
        }

        static Texture BuscarAlbedo(Material m)
        {
            var t = Primera(m, "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo", "_AlbedoMap",
                "_Diffuse", "_DiffuseMap", "_ColorMap", "_MainTexture", "_Texture");
            if (t != null) return t;
            return BuscarTexPorNombre(m, "base", "albedo", "diffuse", "main", "color");
        }

        static Texture BuscarNormal(Material m)
        {
            var t = Primera(m, "_BumpMap", "_NormalMap", "_BentNormalMap", "_DetailNormalMap");
            if (t != null) return t;
            return BuscarTexPorNombre(m, "normal", "bump");
        }

        static Texture Primera(Material m, params string[] props)
        {
            foreach (var p in props)
            {
                if (m.HasProperty(p))
                {
                    var t = m.GetTexture(p);
                    if (t != null) return t;
                }
            }
            return null;
        }

        static Texture BuscarTexPorNombre(Material m, params string[] claves)
        {
#if UNITY_EDITOR
            if (m == null) return null;
            var so = new SerializedObject(m);
            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null || !texEnvs.isArray) return null;
            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                var e = texEnvs.GetArrayElementAtIndex(i);
                string nom = e.FindPropertyRelative("first").stringValue;
                if (string.IsNullOrEmpty(nom)) continue;
                string low = nom.ToLowerInvariant();
                bool ok = false;
                foreach (var c in claves)
                {
                    if (low.Contains(c)) { ok = true; break; }
                }
                if (!ok) continue;
                var texProp = e.FindPropertyRelative("second.m_Texture");
                if (texProp != null && texProp.objectReferenceValue is Texture tex)
                    return tex;
            }
#endif
            return null;
        }

        static void CopiarTiling(Material src, Material dst)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(src);
            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null) return;
            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                var e = texEnvs.GetArrayElementAtIndex(i);
                string nom = e.FindPropertyRelative("first").stringValue;
                if (nom != "_BaseMap" && nom != "_BaseColorMap" && nom != "_MainTex") continue;
                var scale = e.FindPropertyRelative("second.m_Scale");
                if (scale != null && dst.HasProperty("_MainTex"))
                    dst.SetTextureScale("_MainTex", scale.vector2Value);
                break;
            }
#endif
        }

        static string Limpiar(string s)
        {
            foreach (char ch in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s.Replace(' ', '_');
        }
    }
}
