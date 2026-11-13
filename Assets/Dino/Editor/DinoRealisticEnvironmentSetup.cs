using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoVehicle.Editor
{
    /// <summary>
    /// Reemplazo definitivo del entorno poligonal por el ecosistema y terreno PBR ultra realista:
    /// - Elimina completamente 'Environment' y todos los modelos poligonales / facetados (SimpleNaturePack).
    /// - Genera un Unity Terrain con suaves colinas orgánicas y 4 capas PBR (Grass_A, Grass_Soil_A, Pebbles_B, Cliff_Mossy_E).
    /// - Integra SkyboxMountains en el horizonte y niebla atmosférica (sin cortes al vacío negro).
    /// - Puebla arboledas PBR (Trees Collection Asset PBR + Conifers BOTD + Pines) con colisiones para TreeImpactResponse.
    /// - Instancia rocas fotorrealistas PBR, helechos, arbustos y flores de cactus (CactusFlowers_2).
    /// - Mantiene la fauna viva (Estegosaurios, Paquicefalosaurios, Velociraptores, Anquilosaurio).
    /// - Posiciona la Pelota Dinosaurio gigante, rampas de salto y cajas destructibles sobre el nuevo suelo suave.
    /// </summary>
    public static class DinoRealisticEnvironmentSetup
    {
        static readonly string[] EscenasObjetivo =
        {
            "Assets/Dino/Scenes/Simulation.unity",
            "Assets/Dino/Scenes/DinoTestTrack.unity"
        };

        // La reconstrucción completa ya NO se lanza sola al abrir el editor: borraba y regeneraba
        // las dos escenas en cada sesión, pisando cualquier cambio hecho a mano. Queda en el menú.
        // Lo único automático es reparar terrenos sin material (se ven morados), que no destruye nada.
        [InitializeOnLoadMethod]
        static void AutoRepararTerrenos()
        {
            EditorSceneManager.sceneOpened += (escena, modo) => RepararTerrenosSinMaterial();
            EditorApplication.delayCall += RepararTerrenosSinMaterial;
        }

        static Material MaterialTerrenoEstandar()
        {
            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");
        }

        [MenuItem("DINO WEST/Reparar Terreno Morado")]
        static void RepararTerrenosSinMaterial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var mat = MaterialTerrenoEstandar();
            if (mat == null) return;

            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            {
                if (t.materialTemplate != null && t.materialTemplate.shader != null && t.materialTemplate.shader.isSupported) continue;

                t.materialTemplate = mat;
                EditorUtility.SetDirty(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                Debug.Log($"<color=green>[DinoRealisticEnvironmentSetup]</color> Material estándar asignado al terreno '{t.name}'. Guarda la escena (Ctrl+S).");
            }
        }

        [MenuItem("DINO WEST/Construir Terreno y Ecosistema Realista (Cero Poligonos)")]
        public static void EjecutarReemplazoCompleto()
        {
            string escenaActivaAlIniciar = EditorSceneManager.GetActiveScene().path;

            foreach (var rutaEscena in EscenasObjetivo)
            {
                if (!File.Exists(rutaEscena)) continue;

                var escena = EditorSceneManager.OpenScene(rutaEscena);
                Debug.Log($"<color=cyan>[DinoRealisticEnvironmentSetup]</color> Procesando escena: {rutaEscena}...");

                // 1. Eliminar todo el contenido poligonal antiguo
                EliminarEntornoPoligonal();

                // 2. Crear y configurar el terreno suave y realista
                var terrainGo = CrearTerrenoRealista();

                // 3. Montañas lejanas en el horizonte y niebla atmosférica
                ConfigurarHorizonteYAtmosfera();

                // 4. Crear contenedor de ecosistema realista
                var ecoRoot = new GameObject("Ecosistema_DinoWest");
                Undo.RegisterCreatedObjectUndo(ecoRoot, "Crear Ecosistema Realista");

                var arboledaRoot = new GameObject("01_Arboleda_PBR_Realista");
                arboledaRoot.transform.SetParent(ecoRoot.transform, false);

                var comidaRoot = new GameObject("02_Arboles_Comida_Interactivas");
                comidaRoot.transform.SetParent(ecoRoot.transform, false);

                var rocasRoot = new GameObject("03_Rocas_PBR_Fotorrealistas");
                rocasRoot.transform.SetParent(ecoRoot.transform, false);

                var floraRoot = new GameObject("04_Flora_Helechos_Y_Cactus");
                floraRoot.transform.SetParent(ecoRoot.transform, false);

                var faunaRoot = new GameObject("05_Fauna_Dinosaurios_Vivos");
                faunaRoot.transform.SetParent(ecoRoot.transform, false);

                var juguetesRoot = new GameObject("06_Pista_Obstaculos_Y_Pelota");
                juguetesRoot.transform.SetParent(ecoRoot.transform, false);

                // 5. Poblar con los assets descargados
                PoblarArboleda(arboledaRoot.transform, comidaRoot.transform);
                PoblarRocas(rocasRoot.transform);
                PoblarFlora(floraRoot.transform);
                PoblarFauna(faunaRoot.transform);
                PoblarObstaculosYPelota(juguetesRoot.transform);

                // 6. Asegurar posición del jugador y componentes de impacto
                AsegurarJugador();

                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                Debug.Log($"<color=green>[DinoRealisticEnvironmentSetup]</color> ¡Escena {rutaEscena} actualizada con éxito al terreno y vegetación realista!");
            }

            if (!string.IsNullOrEmpty(escenaActivaAlIniciar))
            {
                EditorSceneManager.OpenScene(escenaActivaAlIniciar);
            }
        }

        // -------------------------------------------------------------
        // 1. LIMPIEZA TOTAL DE ACTIVOS POLIGONALES
        // -------------------------------------------------------------
        static void EliminarEntornoPoligonal()
        {
            // Eliminar Environment viejo si existe
            var env = GameObject.Find("Environment");
            if (env != null)
            {
                Undo.DestroyObjectImmediate(env);
            }

            // Eliminar ecosistema previo si existía
            var ecoViejo = GameObject.Find("Ecosistema_DinoWest");
            if (ecoViejo != null)
            {
                Undo.DestroyObjectImmediate(ecoViejo);
            }

            // Eliminar terreno plano viejo
            var pisoViejo = GameObject.Find("Terreno_Pista");
            if (pisoViejo != null)
            {
                Undo.DestroyObjectImmediate(pisoViejo);
            }

            var caminoViejo = GameObject.Find("Pista_Camino");
            if (caminoViejo != null)
            {
                Undo.DestroyObjectImmediate(caminoViejo);
            }

            // Eliminar cualquier terreno anterior para recrear limpio
            var terrenoPrevio = GameObject.Find("Terreno_Realista_PBR");
            if (terrenoPrevio != null)
            {
                Undo.DestroyObjectImmediate(terrenoPrevio);
            }

            // Buscar y eliminar cualquier residuo de SimpleNaturePack o modelos facetados sueltos
            var todos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var go in todos)
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();
                if (n.Contains("simplenature") || n.StartsWith("ground_") || n.StartsWith("hill_") ||
                    n.StartsWith("tree_0") || n.StartsWith("bush_0") || n.StartsWith("stump_") ||
                    n.StartsWith("flowers_0") || n.StartsWith("mushroom_0"))
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }
        }

        // -------------------------------------------------------------
        // 2. CREACIÓN DEL TERRENO REALISTA PBR CON UNITY TERRAIN
        // -------------------------------------------------------------
        static GameObject CrearTerrenoRealista()
        {
            string dirPath = "Assets/Dino/Terrains";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
                AssetDatabase.Refresh();
            }

            string dataPath = $"{dirPath}/DinoWest_TerrainData.asset";
            TerrainData tData = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);

            if (tData == null)
            {
                tData = new TerrainData();
                tData.heightmapResolution = 513;
                tData.size = new Vector3(1000f, 65f, 1000f);
                AssetDatabase.CreateAsset(tData, dataPath);
            }
            else
            {
                tData.heightmapResolution = 513;
                tData.size = new Vector3(1000f, 65f, 1000f);
            }

            // Asignar las capas PBR descargadas
            var layerGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_A.terrainlayer");
            var layerSoil = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_Soil_A.terrainlayer");
            var layerPebbles = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainDemoScene_URP/Terrain/Layers/Pebbles_B.terrainlayer");
            var layerCliff = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/TerrainDemoScene_URP/Terrain/Layers/Cliff_Mossy_E.terrainlayer");

            List<TerrainLayer> capas = new();
            if (layerGrass != null) capas.Add(layerGrass);
            if (layerSoil != null) capas.Add(layerSoil);
            if (layerPebbles != null) capas.Add(layerPebbles);
            if (layerCliff != null) capas.Add(layerCliff);

            if (capas.Count > 0)
            {
                tData.terrainLayers = capas.ToArray();
            }

            // Generar alturas suaves: zona central plana para correr, colinas onduladas a los lados
            int res = tData.heightmapResolution;
            float[,] heights = new float[res, res];

            for (int z = 0; z < res; z++)
            {
                float normZ = (float)z / (res - 1);
                float worldZ = (normZ - 0.5f) * 1000f;

                for (int x = 0; x < res; x++)
                {
                    float normX = (float)x / (res - 1);
                    float worldX = (normX - 0.5f) * 1000f;

                    // Corredor central de la pista (x entre -60 y +60, z entre -120 y +220)
                    float distTrackX = Mathf.Abs(worldX);
                    float distTrackZ = Mathf.Max(worldZ - 220f, -120f - worldZ, 0f);

                    float distTrack = Mathf.Max(distTrackX - 45f, distTrackZ);

                    if (distTrack <= 0f)
                    {
                        // Suelo plano natural perfecto para correr con el dinosaurio
                        heights[z, x] = 0f;
                    }
                    else
                    {
                        // Transición suave hacia colinas naturales ondulantes
                        float factor = Mathf.Clamp01(distTrack / 160f);
                        float smoothFactor = factor * factor * (3f - 2f * factor);

                        // Ruido suave armónico (cero facetas poligonales)
                        float colina1 = Mathf.Sin(worldX * 0.016f) * Mathf.Cos(worldZ * 0.013f) * 0.5f + 0.5f;
                        float colina2 = Mathf.Sin(worldX * 0.008f + worldZ * 0.007f) * 0.5f + 0.5f;
                        float colina3 = Mathf.Cos(worldX * 0.004f - worldZ * 0.005f) * 0.5f + 0.5f;

                        float blendHills = (colina1 * 0.45f + colina2 * 0.35f + colina3 * 0.20f);

                        // Altura máxima moderada (~22m en el borde)
                        heights[z, x] = blendHills * smoothFactor * 0.34f;
                    }
                }
            }

            tData.SetHeights(0, 0, heights);

            // Generar texturizado orgánico (Alphamaps / Splatmaps)
            int alphaRes = tData.alphamapResolution;
            int numCapas = capas.Count;
            float[,,] alphamaps = new float[alphaRes, alphaRes, numCapas];

            for (int z = 0; z < alphaRes; z++)
            {
                float normZ = (float)z / (alphaRes - 1);
                float worldZ = (normZ - 0.5f) * 1000f;

                for (int x = 0; x < alphaRes; x++)
                {
                    float normX = (float)x / (alphaRes - 1);
                    float worldX = (normX - 0.5f) * 1000f;

                    float distEje = Mathf.Abs(worldX);

                    // Camino central de tierra (Grass_Soil_A) donde corre el dinosaurio
                    if (distEje < 10f && worldZ >= -100f && worldZ <= 200f)
                    {
                        float blendDirt = Mathf.Clamp01(1f - (distEje / 10f));
                        alphamaps[z, x, 0] = 1f - blendDirt * 0.85f; // Pasto
                        if (numCapas > 1) alphamaps[z, x, 1] = blendDirt * 0.85f; // Tierra
                    }
                    else if (distEje < 16f && worldZ >= -100f && worldZ <= 200f && numCapas > 2)
                    {
                        // Borde de gravilla (Pebbles_B)
                        float blendPebbles = Mathf.Clamp01(1f - Mathf.Abs(distEje - 13f) / 3f);
                        alphamaps[z, x, 0] = 1f - blendPebbles * 0.6f;
                        alphamaps[z, x, 2] = blendPebbles * 0.6f;
                    }
                    else
                    {
                        // Colinas: Pasto verde y riscos en zonas altas
                        float distFuera = Mathf.Max(distEje - 50f, 0f);
                        float steepness = Mathf.Clamp01(distFuera / 200f);

                        if (steepness > 0.45f && numCapas > 3)
                        {
                            float cliffWeight = (steepness - 0.45f) / 0.55f;
                            alphamaps[z, x, 0] = 1f - cliffWeight;
                            alphamaps[z, x, 3] = cliffWeight; // Riscos de roca
                        }
                        else
                        {
                            alphamaps[z, x, 0] = 1.0f; // Pasto verde dominante
                        }
                    }
                }
            }

            tData.SetAlphamaps(0, 0, alphamaps);
            AssetDatabase.SaveAssets();

            // Instanciar GameObject del Terreno
            var terrainGo = Terrain.CreateTerrainGameObject(tData);
            terrainGo.name = "Terreno_Realista_PBR";
            terrainGo.transform.position = new Vector3(-500f, 0f, -500f);

            var terrainComp = terrainGo.GetComponent<Terrain>();
            // Sin material el terreno se pinta morado: hay que asignar el estándar del pipeline Built-in.
            terrainComp.materialTemplate = MaterialTerrenoEstandar();
            terrainComp.drawTreesAndFoliage = true;
            terrainComp.treeDistance = 1500f;
            terrainComp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            terrainComp.heightmapPixelError = 3;

            return terrainGo;
        }

        // -------------------------------------------------------------
        // 3. HORIZONTE DE MONTAÑAS Y NIEBLA ATMOSFÉRICA
        // -------------------------------------------------------------
        static void ConfigurarHorizonteYAtmosfera()
        {
            string skyboxPrefabPath = "Assets/TerrainDemoScene_URP/Prefabs/Skybox/SkyboxMountains.prefab";
            var skyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(skyboxPrefabPath);

            var existingSky = GameObject.Find("SkyboxMountains_DinoWest");
            if (existingSky != null)
            {
                Undo.DestroyObjectImmediate(existingSky);
            }

            if (skyPrefab != null)
            {
                var skyGo = (GameObject)PrefabUtility.InstantiatePrefab(skyPrefab);
                skyGo.name = "SkyboxMountains_DinoWest";
                skyGo.transform.position = new Vector3(0f, -25f, 0f);
                skyGo.transform.localScale = Vector3.one * 1.15f;
            }

            // Niebla atmosférica suave que se funde con el horizonte (sin vacío negro)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0016f;
            RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.86f);
        }

        // -------------------------------------------------------------
        // 4. ARBOLEDA PBR REALISTA Y CONÍFERAS
        // -------------------------------------------------------------
        static void PoblarArboleda(Transform arboledaParent, Transform comidaParent)
        {
            string[] broadleafTrees = {
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_1.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_5.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_10.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_14.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_19.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_22.prefab",
                "Assets/Trees Collection Asset PBR/Prefabs/Tree_25.prefab"
            };

            string[] coniferTrees = {
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/BIRP/Prefabs/PF Conifer Tall BOTD.prefab",
                "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/BIRP/Prefabs/PF Conifer Medium BOTD.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_A/Pine_A.prefab"
            };

            List<GameObject> pbrPrefabs = new();
            foreach (var p in broadleafTrees)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) pbrPrefabs.Add(go);
            }

            List<GameObject> coniferPrefabs = new();
            foreach (var p in coniferTrees)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) coniferPrefabs.Add(go);
            }

            // Arboleda Oeste (Caducifolios PBR)
            Vector3[] posOeste = {
                new Vector3(-24f, 0f, 15f), new Vector3(-32f, 0f, 30f), new Vector3(-38f, 0f, 50f),
                new Vector3(-28f, 0f, 70f), new Vector3(-42f, 0f, 90f), new Vector3(-35f, 0f, 115f),
                new Vector3(-26f, 0f, 135f), new Vector3(-45f, 0f, 155f), new Vector3(-30f, 0f, 175f),
                new Vector3(-20f, 0f, -25f), new Vector3(-34f, 0f, -48f), new Vector3(-22f, 0f, -70f)
            };

            for (int i = 0; i < posOeste.Length; i++)
            {
                if (pbrPrefabs.Count == 0) break;
                var prefab = pbrPrefabs[i % pbrPrefabs.Count];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arboledaParent);
                inst.name = $"PBR_Tree_Oeste_{i + 1}";
                inst.transform.position = posOeste[i];
                inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                inst.transform.localScale = Vector3.one * Random.Range(0.9f, 1.25f);
                AsegurarColliderTronco(inst);
            }

            // Arboleda Este (Pinares y Coníferas BOTD)
            Vector3[] posEste = {
                new Vector3(25f, 0f, 18f), new Vector3(34f, 0f, 38f), new Vector3(42f, 0f, 58f),
                new Vector3(28f, 0f, 80f), new Vector3(36f, 0f, 105f), new Vector3(45f, 0f, 128f),
                new Vector3(30f, 0f, 148f), new Vector3(40f, 0f, 170f), new Vector3(26f, 0f, 190f),
                new Vector3(24f, 0f, -30f), new Vector3(36f, 0f, -55f), new Vector3(22f, 0f, -80f)
            };

            for (int j = 0; j < posEste.Length; j++)
            {
                var pool = (coniferPrefabs.Count > 0) ? coniferPrefabs : pbrPrefabs;
                var prefab = pool[j % pool.Count];
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arboledaParent);
                inst.name = $"PBR_Conifera_Este_{j + 1}";
                inst.transform.position = posEste[j];
                inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                inst.transform.localScale = Vector3.one * Random.Range(0.85f, 1.3f);
                AsegurarColliderTronco(inst);
            }

            // 4 Árboles de comida interactivos para restaurar estamina
            Vector3[] posComida = {
                new Vector3(-18f, 0f, 45f),
                new Vector3(-24f, 0f, 60f),
                new Vector3(20f, 0f, 50f),
                new Vector3(26f, 0f, 65f)
            };

            for (int k = 0; k < posComida.Length; k++)
            {
                var prefab = pbrPrefabs.Count > 0 ? pbrPrefabs[k % pbrPrefabs.Count] : null;
                if (prefab == null) continue;

                var foodTree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, comidaParent);
                foodTree.name = $"Arbol_Comida_Oasis_{k + 1}";
                foodTree.transform.position = posComida[k];
                foodTree.transform.localScale = Vector3.one * 1.15f;
                foodTree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                AsegurarColliderTronco(foodTree);

                if (foodTree.GetComponent<DinoFoodTree>() == null)
                {
                    foodTree.AddComponent<DinoFoodTree>();
                }
            }
        }

        static void AsegurarColliderTronco(GameObject tree)
        {
            var col = tree.GetComponentInChildren<Collider>();
            if (col == null)
            {
                var cc = tree.AddComponent<CapsuleCollider>();
                cc.radius = 0.55f;
                cc.height = 9.0f;
                cc.center = new Vector3(0, 4.5f, 0);
            }
        }

        // -------------------------------------------------------------
        // 5. ROCAS PBR FOTORREALISTAS
        // -------------------------------------------------------------
        static void PoblarRocas(Transform rocasParent)
        {
            string[] rockPaths = {
                "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_01.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_01.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_A.prefab"
            };

            List<GameObject> rockPrefabs = new();
            foreach (var p in rockPaths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) rockPrefabs.Add(go);
            }

            if (rockPrefabs.Count == 0) return;

            Vector3[] posRocas = {
                new Vector3(-21f, 0f, 25f),
                new Vector3(-35f, 0f, 62f),
                new Vector3(22f, 0f, 28f),
                new Vector3(38f, 0f, 72f),
                new Vector3(-18f, 0f, 105f),
                new Vector3(25f, 0f, 122f),
                new Vector3(-15f, 0f, -40f),
                new Vector3(18f, 0f, -45f)
            };

            for (int i = 0; i < posRocas.Length; i++)
            {
                var prefab = rockPrefabs[i % rockPrefabs.Count];
                var roca = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rocasParent);
                roca.name = $"Roca_PBR_{i + 1}";
                roca.transform.position = posRocas[i];
                roca.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
                roca.transform.localScale = Vector3.one * Random.Range(1.0f, 1.8f);

                if (roca.GetComponentInChildren<Collider>() == null)
                {
                    roca.AddComponent<MeshCollider>();
                }
            }
        }

        // -------------------------------------------------------------
        // 6. FLORA: HELECHOS, ARBUSTOS Y FLORES DE CACTUS
        // -------------------------------------------------------------
        static void PoblarFlora(Transform floraParent)
        {
            // 1. Flores de Cactus
            string cactusPath = "Assets/Dino/Models/Flora/CactusFlowers_2.fbx";
            var cactusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cactusPath);

            if (cactusPrefab != null)
            {
                Vector3[] posCactus = {
                    new Vector3(-14f, 0f, 35f), new Vector3(-16f, 0f, 55f),
                    new Vector3(15f, 0f, 42f), new Vector3(17f, 0f, 68f),
                    new Vector3(-10f, 0f, 92f), new Vector3(11f, 0f, 110f),
                    new Vector3(-14f, 0f, 145f), new Vector3(14f, 0f, 160f)
                };

                for (int i = 0; i < posCactus.Length; i++)
                {
                    var flor = (GameObject)PrefabUtility.InstantiatePrefab(cactusPrefab, floraParent);
                    flor.name = $"Flor_Cactus_{i + 1}";
                    flor.transform.position = posCactus[i];
                    flor.transform.localScale = Vector3.one * Random.Range(1.3f, 2.2f);
                    flor.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
            }

            // 2. Helechos y Arbustos PBR
            string[] foliagePaths = {
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_A.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_A.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_A.prefab"
            };

            List<GameObject> folPrefabs = new();
            foreach (var p in foliagePaths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) folPrefabs.Add(go);
            }

            if (folPrefabs.Count > 0)
            {
                Vector3[] posFoliage = {
                    new Vector3(-20f, 0f, 18f), new Vector3(-27f, 0f, 35f),
                    new Vector3(21f, 0f, 22f), new Vector3(29f, 0f, 45f),
                    new Vector3(-23f, 0f, 85f), new Vector3(24f, 0f, 95f)
                };

                for (int j = 0; j < posFoliage.Length; j++)
                {
                    var prefab = folPrefabs[j % folPrefabs.Count];
                    var fol = (GameObject)PrefabUtility.InstantiatePrefab(prefab, floraParent);
                    fol.name = $"Foliage_PBR_{j + 1}";
                    fol.transform.position = posFoliage[j];
                    fol.transform.localScale = Vector3.one * Random.Range(1.1f, 1.7f);
                    fol.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
            }
        }

        // -------------------------------------------------------------
        // 7. FAUNA VIVA: ESTEGOSAURIOS, PAQUICEFALOSAURIOS, RAPTORES
        // -------------------------------------------------------------
        static void PoblarFauna(Transform faunaParent)
        {
            // 1. Estegosaurios (Oeste)
            string stegoGreen = "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Green.prefab";
            string stegoBlue = "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Blue.prefab";
            CrearHerbivoro(stegoGreen, faunaParent, new Vector3(-35f, 0f, 40f), 25f, 1.15f, "Estegosaurio_Alfa");
            CrearHerbivoro(stegoBlue, faunaParent, new Vector3(-42f, 0f, 52f), 55f, 1.0f, "Estegosaurio_Cria");

            // 2. Paquicefalosaurios (Este)
            string pachyGreen = "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_Green.prefab";
            string pachySand = "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_SandFemale.prefab";
            CrearHerbivoro(pachyGreen, faunaParent, new Vector3(36f, 0f, 72f), -45f, 1.1f, "Paquicefalosaurio_Macho");
            CrearHerbivoro(pachySand, faunaParent, new Vector3(44f, 0f, 82f), -75f, 1.0f, "Paquicefalosaurio_Hembra");

            // 3. Velociraptores acechando en el bosque norte
            string raptorPath = "Assets/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Sand_Female.prefab";
            CrearRaptor(raptorPath, faunaParent, new Vector3(30f, 0f, -32f), 135f, 1.0f, "Velociraptor_Lider");
            CrearRaptor(raptorPath, faunaParent, new Vector3(26f, 0f, -40f), 150f, 0.95f, "Velociraptor_Acechador_1");
            CrearRaptor(raptorPath, faunaParent, new Vector3(34f, 0f, -38f), 120f, 0.92f, "Velociraptor_Acechador_2");

            // 4. Anquilosaurio
            string ankyloPath = "Assets/Dino/Models/Fauna/Ankylosaurus/source/Dinosaur.glb";
            var ankyloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ankyloPath);
            if (ankyloPrefab != null)
            {
                var ankylo = (GameObject)PrefabUtility.InstantiatePrefab(ankyloPrefab, faunaParent);
                ankylo.name = "Anquilosaurio_Acorazado";
                ankylo.transform.position = new Vector3(-26f, 0f, -42f);
                ankylo.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
                ankylo.transform.localScale = Vector3.one * 1.6f;

                if (ankylo.GetComponent<DinoAIHerbivore>() == null)
                    ankylo.AddComponent<DinoAIHerbivore>();

                if (ankylo.GetComponentInChildren<Collider>() == null)
                {
                    var bc = ankylo.AddComponent<BoxCollider>();
                    bc.size = new Vector3(2.5f, 1.8f, 4.5f);
                    bc.center = new Vector3(0, 0.9f, 0);
                }
            }
        }

        static void CrearHerbivoro(string path, Transform parent, Vector3 pos, float rotY, float escala, string nombre)
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

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = 1.0f;
                cc.height = 3.5f;
                cc.direction = 2; // Eje Z
                cc.center = new Vector3(0, 1.2f, 0);
            }
        }

        static void CrearRaptor(string path, Transform parent, Vector3 pos, float rotY, float escala, string nombre)
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
                cc.radius = 0.5f;
                cc.height = 2.2f;
                cc.direction = 2;
                cc.center = new Vector3(0, 0.9f, 0);
            }
        }

        // -------------------------------------------------------------
        // 8. PISTA: PELOTA DINOSAURIO, RAMPAS Y CAJAS DESTRUCTORAS
        // -------------------------------------------------------------
        static void PoblarObstaculosYPelota(Transform juguetesParent)
        {
            // Pelota Dinosaurio Prehistórica
            var pelotaExistente = GameObject.Find("Pelota_Dinosaurio_Prehistorica");
            if (pelotaExistente != null) Undo.DestroyObjectImmediate(pelotaExistente);

            var pelota = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pelota.name = "Pelota_Dinosaurio_Prehistorica";
            pelota.transform.SetParent(juguetesParent, false);
            pelota.transform.position = new Vector3(12f, 1.6f, 25f);
            pelota.transform.localScale = Vector3.one * 3.2f;

            var rb = pelota.AddComponent<Rigidbody>();
            rb.mass = 45f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.25f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var matPelota = new Material(Shader.Find("Standard"));
            matPelota.color = new Color(0.95f, 0.55f, 0.12f);
            matPelota.SetFloat("_Glossiness", 0.65f);
            matPelota.SetFloat("_Metallic", 0.1f);
            pelota.GetComponent<Renderer>().sharedMaterial = matPelota;

            if (pelota.GetComponent<DinoBallToy>() == null)
            {
                pelota.AddComponent<DinoBallToy>();
            }

            // Rampas de salto
            CrearRampa(new Vector3(0f, 0f, 40f), new Vector3(10f, 2.2f, 12f), 18f, juguetesParent);
            CrearRampa(new Vector3(0f, 0f, 105f), new Vector3(12f, 3.6f, 15f), 24f, juguetesParent);

            // Pirámides de cajas destructibles para demolición física
            CrearPiramideCajas(new Vector3(0f, 0.5f, 72f), 4, juguetesParent);
            CrearPiramideCajas(new Vector3(8f, 0.5f, 140f), 3, juguetesParent);
        }

        static void CrearRampa(Vector3 pos, Vector3 tam, float angulo, Transform parent)
        {
            var rampa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rampa.name = "Rampa_Salto";
            rampa.transform.SetParent(parent, false);
            rampa.transform.position = pos + new Vector3(0, tam.y * 0.45f, 0);
            rampa.transform.localScale = tam;
            rampa.transform.rotation = Quaternion.Euler(-angulo, 0f, 0f);

            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.68f, 0.48f, 0.28f);
            rampa.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void CrearPiramideCajas(Vector3 posBase, int filas, Transform parent)
        {
            float tamCaja = 1.0f;
            for (int f = 0; f < filas; f++)
            {
                int cajasEnFila = filas - f;
                float inicioX = -(cajasEnFila - 1) * tamCaja * 0.5f;
                float y = posBase.y + f * tamCaja;

                for (int c = 0; c < cajasEnFila; c++)
                {
                    var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    caja.name = $"Caja_Destructible_{f}_{c}";
                    caja.transform.SetParent(parent, false);
                    caja.transform.position = new Vector3(posBase.x + inicioX + c * tamCaja, y, posBase.z);
                    caja.transform.localScale = Vector3.one * (tamCaja * 0.95f);

                    var rb = caja.AddComponent<Rigidbody>();
                    rb.mass = 25f;

                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.78f, 0.58f, 0.36f);
                    caja.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }
        }

        // -------------------------------------------------------------
        // 9. CONFIGURAR JUGADOR Y MONTURA
        // -------------------------------------------------------------
        static void AsegurarJugador()
        {
            var vehicle = Object.FindAnyObjectByType<DinoVehicleController>();
            if (vehicle != null)
            {
                // Asegurar altura sobre el terreno suave
                var p = vehicle.transform.position;
                vehicle.transform.position = new Vector3(0f, 1.0f, 0f);
                vehicle.transform.rotation = Quaternion.identity;

                if (vehicle.GetComponent<TreeImpactResponse>() == null)
                    vehicle.gameObject.AddComponent<TreeImpactResponse>();

                if (vehicle.GetComponent<DinoBirdProximity>() == null)
                    vehicle.gameObject.AddComponent<DinoBirdProximity>();
            }
        }
    }
}
