using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DinoVehicle
{
    /// <summary>
    /// Generador dinámico del ecosistema en tiempo de ejecución.
    /// Si el usuario le da a Play (▶) y la escena no tiene el ecosistema horneado,
    /// este script lo construye automáticamente al instante con:
    /// - 20+ Árboles realistas PBR con físicas de derribo
    /// - 4 Árboles de comida interactivos con frutos y recuperación de estamina
    /// - Manadas vivas de Estegosaurios, Paquicefalosaurios y Anquilosaurio
    /// - Manada de Velociraptores acechadores
    /// - Pelota gigante interactiva ("Pelota Dinosaurio")
    /// - Flores de cactus y oasis
    /// - Interacción de huida de bandadas de pájaros
    /// </summary>
    public class DinoEcosystemRuntime : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoGenerarEnPlayMode()
        {
            var env = GameObject.Find("Environment");
            if (env != null) env.SetActive(false);
            if (GameObject.Find("Ecosistema_DinoWest") != null)
            {
                return;
            }

            var go = new GameObject("Ecosistema_DinoWest");
            go.AddComponent<DinoEcosystemRuntime>().Generar();
        }

        public void Generar()
        {
            Debug.Log("<color=yellow>[DinoEcosystemRuntime]</color> Generando ecosistema completo en tiempo de ejecución...");

            var arboledaRoot = new GameObject("01_Arboleda_PBR_Realista");
            arboledaRoot.transform.SetParent(transform, false);

            var comidaRoot = new GameObject("02_Arboles_Comida");
            comidaRoot.transform.SetParent(transform, false);

            var faunaRoot = new GameObject("03_Fauna_Dinosaurios");
            faunaRoot.transform.SetParent(transform, false);

            var juguetesRoot = new GameObject("04_Juguetes_Y_Pelotas");
            juguetesRoot.transform.SetParent(transform, false);

            var floraRoot = new GameObject("05_Flora_Flores_Cactus");
            floraRoot.transform.SetParent(transform, false);

#if UNITY_EDITOR
            GenerarArboledaEditor(arboledaRoot.transform, comidaRoot.transform);
            GenerarFaunaEditor(faunaRoot.transform);
            GenerarFloresEditor(floraRoot.transform);
#else
            GenerarArboledaFallback(arboledaRoot.transform, comidaRoot.transform);
#endif
            GenerarPelotaDinosaurio(juguetesRoot.transform);
            ConfigurarJugador();

            Debug.Log("<color=green>[DinoEcosystemRuntime]</color> ¡Ecosistema completamente listo para jugar!");
        }

#if UNITY_EDITOR
        void GenerarArboledaEditor(Transform arboledaParent, Transform comidaParent)
        {
            string[] treePaths = {
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_1.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_5.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_10.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_14.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_19.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_22.prefab",
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/BIRP/Prefabs/PF Conifer Tall BOTD.prefab",
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/BIRP/Prefabs/PF Conifer Medium BOTD.prefab"
            };

            List<GameObject> prefabs = new();
            foreach (var p in treePaths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) prefabs.Add(go);
            }

            if (prefabs.Count == 0) return;

            Vector3[] posicionesArboles = {
                // Bosque Oeste
                new Vector3(-22f, 0f, 18f),
                new Vector3(-28f, 0f, 32f),
                new Vector3(-35f, 0f, 48f),
                new Vector3(-42f, 0f, 65f),
                new Vector3(-20f, 0f, 85f),
                new Vector3(-32f, 0f, 105f),
                new Vector3(-25f, 0f, 135f),
                new Vector3(-38f, 0f, 148f),

                // Bosque Este
                new Vector3(22f, 0f, 15f),
                new Vector3(30f, 0f, 35f),
                new Vector3(38f, 0f, 55f),
                new Vector3(26f, 0f, 78f),
                new Vector3(34f, 0f, 98f),
                new Vector3(28f, 0f, 118f),
                new Vector3(36f, 0f, 140f),

                // Bosque Sur
                new Vector3(-18f, 0f, -25f),
                new Vector3(-30f, 0f, -45f),
                new Vector3(20f, 0f, -30f),
                new Vector3(32f, 0f, -50f),
                new Vector3(-12f, 0f, -75f),
                new Vector3(15f, 0f, -80f)
            };

            for (int i = 0; i < posicionesArboles.Length; i++)
            {
                var prefab = prefabs[i % prefabs.Count];
                var tree = Instantiate(prefab, posicionesArboles[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), arboledaParent);
                tree.name = $"PBR_Tree_{i + 1}_{prefab.name}";
                tree.transform.localScale = Vector3.one * Random.Range(0.9f, 1.25f);
                AsegurarColliderArbol(tree);
                CorregirShaders(tree);
            }

            // 4 Árboles de comida
            Vector3[] posicionesComida = {
                new Vector3(-16f, 0f, 45f),
                new Vector3(-22f, 0f, 58f),
                new Vector3(18f, 0f, 48f),
                new Vector3(24f, 0f, 62f)
            };

            for (int j = 0; j < posicionesComida.Length; j++)
            {
                var prefab = prefabs[j % 3];
                var foodTree = Instantiate(prefab, posicionesComida[j], Quaternion.Euler(0, Random.Range(0f, 360f), 0), comidaParent);
                foodTree.name = $"Arbol_Comida_{j + 1}";
                foodTree.transform.localScale = Vector3.one * 1.15f;
                AsegurarColliderArbol(foodTree);
                CorregirShaders(foodTree);
                foodTree.AddComponent<DinoFoodTree>();
            }
        }

        void GenerarFaunaEditor(Transform faunaParent)
        {
            // Estegosaurios
            string stegoGreenPath = "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Green.prefab";
            string stegoBluePath = "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Blue.prefab";
            InstanciarDinoEditor(stegoGreenPath, faunaParent, new Vector3(-36f, 0f, 42f), 25f, 1.15f, "Estegosaurio_Alfa");
            InstanciarDinoEditor(stegoBluePath, faunaParent, new Vector3(-44f, 0f, 54f), 60f, 1.05f, "Estegosaurio_Cria");

            // Paquicefalosaurios
            string pachyGreenPath = "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_Green.prefab";
            string pachySandPath = "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_SandFemale.prefab";
            InstanciarDinoEditor(pachyGreenPath, faunaParent, new Vector3(35f, 0f, 75f), -45f, 1.1f, "Paquicefalosaurio_Macho");
            InstanciarDinoEditor(pachySandPath, faunaParent, new Vector3(42f, 0f, 85f), -80f, 1.0f, "Paquicefalosaurio_Hembra");

            // Velociraptores
            string raptorPath = "Assets/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Sand_Female.prefab";
            InstanciarRaptorEditor(raptorPath, faunaParent, new Vector3(32f, 0f, -35f), 135f, 1.0f, "Velociraptor_Lider");
            InstanciarRaptorEditor(raptorPath, faunaParent, new Vector3(28f, 0f, -42f), 150f, 0.95f, "Velociraptor_Acechador_1");
            InstanciarRaptorEditor(raptorPath, faunaParent, new Vector3(36f, 0f, -40f), 120f, 0.92f, "Velociraptor_Acechador_2");

            // Anquilosaurio
            string ankyloPath = "Assets/Dino/Models/Fauna/Ankylosaurus/source/Dinosaur.glb";
            var ankyloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ankyloPath);
            if (ankyloPrefab != null)
            {
                var ankylo = Instantiate(ankyloPrefab, new Vector3(-25f, 0f, -45f), Quaternion.Euler(0, 40f, 0), faunaParent);
                ankylo.name = "Anquilosaurio_Acorazado";
                ankylo.transform.localScale = Vector3.one * 1.6f;
                ankylo.AddComponent<DinoAIHerbivore>();
                var bc = ankylo.AddComponent<BoxCollider>();
                bc.size = new Vector3(2.5f, 1.8f, 4.5f);
                bc.center = new Vector3(0, 0.9f, 0);
            }
        }

        void InstanciarDinoEditor(string path, Transform parent, Vector3 pos, float rotY, float escala, string nombre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var go = Instantiate(prefab, pos, Quaternion.Euler(0, rotY, 0), parent);
            go.name = nombre;
            go.transform.localScale = Vector3.one * escala;
            go.AddComponent<DinoAIHerbivore>();

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 1.1f;
                cc.height = 3.2f;
                cc.direction = 2;
                cc.center = new Vector3(0, 1.2f, 0);
            }
            CorregirShaders(go);
        }

        void InstanciarRaptorEditor(string path, Transform parent, Vector3 pos, float rotY, float escala, string nombre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var go = Instantiate(prefab, pos, Quaternion.Euler(0, rotY, 0), parent);
            go.name = nombre;
            go.transform.localScale = Vector3.one * escala;

            // El prefab del raptor no trae AnimatorController: sin uno queda en pose fija.
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController == null)
            {
                var ctrl = DinoMountManager.CargarControllerValido(
                    DinoMountManager.CtrlVelociraptor,
                    "Assets/PBRVelociraptor/Animators/DemoAnimatorWalk.controller");
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            }

            go.AddComponent<DinoAIRaptor>();

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.6f;
                cc.height = 2.0f;
                cc.direction = 2;
                cc.center = new Vector3(0, 0.8f, 0);
            }
            CorregirShaders(go);
        }

        void GenerarFloresEditor(Transform floraParent)
        {
            string cactusPath = "Assets/Dino/Models/Flora/CactusFlowers_2.fbx";
            var cactusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cactusPath);
            if (cactusPrefab == null) return;

            Vector3[] posFlores = {
                new Vector3(-12f, 0f, 38f),
                new Vector3(-14f, 0f, 52f),
                new Vector3(14f, 0f, 42f),
                new Vector3(16f, 0f, 65f),
                new Vector3(-8f, 0f, 85f),
                new Vector3(9f, 0f, 95f),
                new Vector3(-15f, 0f, 115f),
                new Vector3(15f, 0f, 125f)
            };

            for (int i = 0; i < posFlores.Length; i++)
            {
                var flor = Instantiate(cactusPrefab, posFlores[i], Quaternion.Euler(0, Random.Range(0f, 360f), 0), floraParent);
                flor.name = $"Flor_Cactus_{i + 1}";
                flor.transform.localScale = Vector3.one * Random.Range(1.2f, 2.0f);
                CorregirShaders(flor);
            }
        }
#endif

        void GenerarPelotaDinosaurio(Transform parent)
        {
            var pelota = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pelota.name = "Pelota_Dinosaurio_Prehistorica";
            pelota.transform.SetParent(parent, false);
            pelota.transform.position = new Vector3(10f, 1.6f, 25f);
            pelota.transform.localScale = Vector3.one * 2.8f;

            var ren = pelota.GetComponent<Renderer>();
            if (ren != null)
            {
                var mat = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.88f, 0.42f, 0.12f, 1f)
                };
                mat.SetFloat("_Glossiness", 0.35f);
                ren.sharedMaterial = mat;
            }

            pelota.AddComponent<DinoBallToy>();
        }

        void ConfigurarJugador()
        {
            var vehicle = FindAnyObjectByType<DinoVehicleController>();
            if (vehicle != null)
            {
                if (vehicle.GetComponent<TreeImpactResponse>() == null)
                    vehicle.gameObject.AddComponent<TreeImpactResponse>();

                if (vehicle.GetComponent<DinoBirdProximity>() == null)
                    vehicle.gameObject.AddComponent<DinoBirdProximity>();
            }
        }

        void AsegurarColliderArbol(GameObject tree)
        {
            var col = tree.GetComponentInChildren<Collider>();
            if (col == null)
            {
                var cc = tree.AddComponent<CapsuleCollider>();
                cc.radius = 0.55f;
                cc.height = 8.5f;
                cc.center = new Vector3(0, 4.25f, 0);
            }
        }

        void CorregirShaders(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string matName = mat.name.ToLowerInvariant();
                    bool isLeaves = matName.Contains("leave") || matName.Contains("hoja") || matName.Contains("branch") || matName.Contains("foliage");

                    if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader" || mat.shader.name.Contains("HDRP") || mat.shader.name.Contains("Universal"))
                    {
                        mat.shader = Shader.Find("Standard");
                        changed = true;
                    }

                    if (isLeaves)
                    {
                        mat.SetFloat("_Mode", 1f);
                        mat.SetOverrideTag("RenderType", "TransparentCutout");
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetInt("_ZWrite", 1);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                        mat.SetFloat("_Cutoff", 0.45f);
                        mat.DisableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", Color.black);
                        changed = true;
                    }
                    else if (matName.Contains("bark") || matName.Contains("tronco") || matName.Contains("wood"))
                    {
                        mat.DisableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", Color.black);
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
