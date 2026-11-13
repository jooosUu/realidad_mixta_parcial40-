using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Construye y puebla el ecosistema completo de DinoWest en las escenas de simulación:
    /// - Arboleda PBR realista (Trees Collection Asset PBR + Conifers BOTD) con colisiones físicas (TreeReaction).
    /// - Árboles de comida interactivos (DinoFoodTree) para recarga de estamina/energía.
    /// - Manadas vivas de dinosaurios herbívoros (Estegosaurios, Paquicefalosaurios, Anquilosaurio) con IA de pastoreo y huida (DinoAIHerbivore).
    /// - Manada de Velociraptores acechadores (DinoAIRaptor).
    /// - Pelota interactiva con físicas prehistóricas ("Pelota Dinosaurio" / DinoBallToy).
    /// - Flores de cactus y vegetación en el terreno (CactusFlowers_2).
    /// - Sensor de huida y aleteo de pájaros en el Carnotauro (DinoBirdProximity).
    /// </summary>
    public static class DinoEcosystemSetup
    {
        static readonly string[] Escenas =
        {
            "Assets/Dino/Scenes/Simulation.unity",
            "Assets/Dino/Scenes/DinoTestTrack.unity"
        };

        [InitializeOnLoadMethod]
        static void AutoRunOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("DinoEcosystemSetup_Done_v1", false))
                {
                    SessionState.SetBool("DinoEcosystemSetup_Done_v1", true);
                    ConstruirEcosistema();
                }
            };
        }

        [MenuItem("DINO WEST/Construir Ecosistema Completo (Árboles, Dinos, Flores, Pelota)")]
        public static void ConstruirEcosistema()
        {
            string actual = EditorSceneManager.GetActiveScene().path;

            foreach (var rutaEscena in Escenas)
            {
                var escena = EditorSceneManager.OpenScene(rutaEscena);
                Debug.Log($"<color=cyan>[DinoEcosystemSetup]</color> Configurando ecosistema en: {rutaEscena}...");

                // 1. Limpiar contenedor previo si existía
                var existente = GameObject.Find("Ecosistema_DinoWest");
                if (existente != null)
                {
                    Undo.DestroyObjectImmediate(existente);
                }

                var rootEco = new GameObject("Ecosistema_DinoWest");
                Undo.RegisterCreatedObjectUndo(rootEco, "Crear Ecosistema");

                // Contenedores organizados
                var arboledaRoot = new GameObject("01_Arboleda_PBR_Realista");
                arboledaRoot.transform.SetParent(rootEco.transform, false);

                var comidaRoot = new GameObject("02_Arboles_Comida_Interactivas");
                comidaRoot.transform.SetParent(rootEco.transform, false);

                var faunaRoot = new GameObject("03_Fauna_Dinosaurios_Vivos");
                faunaRoot.transform.SetParent(rootEco.transform, false);

                var juguetesRoot = new GameObject("04_Juguetes_Y_Fisicas");
                juguetesRoot.transform.SetParent(rootEco.transform, false);

                var floraRoot = new GameObject("05_Flora_Flores_Cactus");
                floraRoot.transform.SetParent(rootEco.transform, false);

                // --- 2. ÁRBOLES REALISTAS PBR CON FÍSICAS DE CHOQUE ---
                ConstruirArboleda(arboledaRoot.transform, comidaRoot.transform);

                // --- 3. FAUNA VIVA: ESTEGOSAURIOS, PAQUICEFALOSAURIOS, RAPTORES, ANQUILOSAURIO ---
                PoblarDinosaurios(faunaRoot.transform);

                // --- 4. PELOTA DINOSAURIO INTERACTIVA ---
                CrearPelotaDinosaurio(juguetesRoot.transform);

                // --- 5. FLORES DE CACTUS Y VEGETACIÓN ---
                PoblarFlores(floraRoot.transform);

                // --- 6. JUGADOR: TreeImpactResponse & DinoBirdProximity ---
                ConfigurarJugador();

                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                Debug.Log($"<color=green>[DinoEcosystemSetup]</color> ¡Ecosistema completado con éxito en {rutaEscena}!");
            }

            if (!string.IsNullOrEmpty(actual))
            {
                EditorSceneManager.OpenScene(actual);
            }
        }

        static void ConstruirArboleda(Transform arboledaParent, Transform comidaParent)
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

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("[DinoEcosystemSetup] No se encontraron prefabs de árboles PBR.");
                return;
            }

            // Puntos distribuidos en el mapa formando bosques a los lados de la pista
            Vector3[] posicionesArboles = {
                // Costado Oeste (Bosque Caducifolio / PBR)
                new Vector3(-22f, 0f, 18f),
                new Vector3(-28f, 0f, 32f),
                new Vector3(-35f, 0f, 48f),
                new Vector3(-42f, 0f, 65f),
                new Vector3(-20f, 0f, 85f),
                new Vector3(-32f, 0f, 105f),
                new Vector3(-25f, 0f, 135f),
                new Vector3(-38f, 0f, 148f),

                // Costado Este (Pinar / Coníferas y Árboles PBR)
                new Vector3(22f, 0f, 15f),
                new Vector3(30f, 0f, 35f),
                new Vector3(38f, 0f, 55f),
                new Vector3(26f, 0f, 78f),
                new Vector3(34f, 0f, 98f),
                new Vector3(28f, 0f, 118f),
                new Vector3(36f, 0f, 140f),

                // Zona Sur / Entrada
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
                var treeInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arboledaParent);
                treeInstance.transform.position = posicionesArboles[i];

                float escala = Random.Range(0.85f, 1.25f);
                treeInstance.transform.localScale = Vector3.one * escala;
                treeInstance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                treeInstance.name = $"PBR_Tree_{i + 1}_{prefab.name}";

                // Asegurar collider de tronco
                AsegurarColliderArbol(treeInstance);

                // Corregir shader si es magenta/incompatible
                CorregirShadersEnObjeto(treeInstance);
            }

            // 4 Árboles de comida en la zona del oasis
            Vector3[] posicionesComida = {
                new Vector3(-16f, 0f, 45f),
                new Vector3(-22f, 0f, 58f),
                new Vector3(18f, 0f, 48f),
                new Vector3(24f, 0f, 62f)
            };

            for (int j = 0; j < posicionesComida.Length; j++)
            {
                var prefab = prefabs[j % 3]; // árboles frondosos
                var foodTree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, comidaParent);
                foodTree.transform.position = posicionesComida[j];
                foodTree.transform.localScale = Vector3.one * 1.15f;
                foodTree.name = $"Arbol_Comida_Oasis_{j + 1}";

                AsegurarColliderArbol(foodTree);
                CorregirShadersEnObjeto(foodTree);

                // Agregar componente de comida
                if (foodTree.GetComponent<DinoFoodTree>() == null)
                {
                    foodTree.AddComponent<DinoFoodTree>();
                }
            }
        }

        static void AsegurarColliderArbol(GameObject tree)
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

        static void PoblarDinosaurios(Transform faunaParent)
        {
            // 1. Estegosaurios (Manada en el oasis oeste)
            string stegoGreenPath = "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Green.prefab";
            string stegoBluePath = "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Blue.prefab";
            InstanciarDino(stegoGreenPath, faunaParent, new Vector3(-36f, 0f, 42f), 25f, 1.15f, "Estegosaurio_Alfa");
            InstanciarDino(stegoBluePath, faunaParent, new Vector3(-44f, 0f, 54f), 60f, 1.05f, "Estegosaurio_Cria");

            // 2. Paquicefalosaurios (Colinas este)
            string pachyGreenPath = "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_Green.prefab";
            string pachySandPath = "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_SandFemale.prefab";
            InstanciarDino(pachyGreenPath, faunaParent, new Vector3(35f, 0f, 75f), -45f, 1.1f, "Paquicefalosaurio_Macho");
            InstanciarDino(pachySandPath, faunaParent, new Vector3(42f, 0f, 85f), -80f, 1.0f, "Paquicefalosaurio_Hembra");

            // 3. Velociraptores (Manada acechando en el cañón)
            string raptorPath = "Assets/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Sand_Female.prefab";
            InstanciarRaptor(raptorPath, faunaParent, new Vector3(32f, 0f, -35f), 135f, 1.0f, "Velociraptor_Lider");
            InstanciarRaptor(raptorPath, faunaParent, new Vector3(28f, 0f, -42f), 150f, 0.95f, "Velociraptor_Acechador_1");
            InstanciarRaptor(raptorPath, faunaParent, new Vector3(36f, 0f, -40f), 120f, 0.92f, "Velociraptor_Acechador_2");

            // 4. Anquilosaurio
            string ankyloPath = "Assets/Dino/Models/Fauna/Ankylosaurus/source/Dinosaur.glb";
            var ankyloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ankyloPath);
            if (ankyloPrefab != null)
            {
                var ankylo = (GameObject)PrefabUtility.InstantiatePrefab(ankyloPrefab, faunaParent);
                ankylo.name = "Anquilosaurio_Acorazado";
                ankylo.transform.position = new Vector3(-25f, 0f, -45f);
                ankylo.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
                ankylo.transform.localScale = Vector3.one * 1.6f;

                if (ankylo.GetComponent<DinoAIHerbivore>() == null)
                    ankylo.AddComponent<DinoAIHerbivore>();

                var col = ankylo.GetComponentInChildren<Collider>();
                if (col == null)
                {
                    var bc = ankylo.AddComponent<BoxCollider>();
                    bc.size = new Vector3(2.5f, 1.8f, 4.5f);
                    bc.center = new Vector3(0, 0.9f, 0);
                }
            }
        }

        static void InstanciarDino(string path, Transform parent, Vector3 pos, float rotY, float escala, string nombre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = nombre;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * escala;

            if (go.GetComponent<DinoAIHerbivore>() == null)
                go.AddComponent<DinoAIHerbivore>();

            // Colisionador para empujar o chocar
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 1.1f;
                cc.height = 3.2f;
                cc.direction = 2; // eje Z
                cc.center = new Vector3(0, 1.2f, 0);
            }

            CorregirShadersEnObjeto(go);
        }

        static void InstanciarRaptor(string path, Transform parent, Vector3 pos, float rotY, float escala, string nombre)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = nombre;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * escala;

            if (go.GetComponent<DinoAIRaptor>() == null)
                go.AddComponent<DinoAIRaptor>();

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 0.6f;
                cc.height = 2.0f;
                cc.direction = 2;
                cc.center = new Vector3(0, 0.8f, 0);
            }

            CorregirShadersEnObjeto(go);
        }

        static void CrearPelotaDinosaurio(Transform parent)
        {
            var pelota = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pelota.name = "Pelota_Dinosaurio_Prehistorica";
            pelota.transform.SetParent(parent, false);
            pelota.transform.position = new Vector3(10f, 1.6f, 25f);
            pelota.transform.localScale = Vector3.one * 2.8f; // Esfera de 2.8 metros de diámetro

            // Material prehistórico llamativo
            var ren = pelota.GetComponent<Renderer>();
            if (ren != null)
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.name = "M_DinoPelota_Cuero";
                mat.color = new Color(0.85f, 0.42f, 0.12f, 1f); // Naranja rojizo arcilla
                mat.SetFloat("_Glossiness", 0.35f);
                ren.sharedMaterial = mat;
            }

            // Componente de juguete
            if (pelota.GetComponent<DinoBallToy>() == null)
            {
                pelota.AddComponent<DinoBallToy>();
            }
        }

        static void PoblarFlores(Transform floraParent)
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
                var flor = (GameObject)PrefabUtility.InstantiatePrefab(cactusPrefab, floraParent);
                flor.name = $"Flor_Cactus_{i + 1}";
                flor.transform.position = posFlores[i];
                flor.transform.localScale = Vector3.one * Random.Range(1.2f, 2.0f);
                flor.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                CorregirShadersEnObjeto(flor);
            }
        }

        static void ConfigurarJugador()
        {
            var vehicle = Object.FindAnyObjectByType<DinoVehicleController>();
            if (vehicle != null)
            {
                if (vehicle.GetComponent<TreeImpactResponse>() == null)
                {
                    vehicle.gameObject.AddComponent<TreeImpactResponse>();
                }

                if (vehicle.GetComponent<DinoBirdProximity>() == null)
                {
                    vehicle.gameObject.AddComponent<DinoBirdProximity>();
                }
            }
        }

        static void CorregirShadersEnObjeto(GameObject go)
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
