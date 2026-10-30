using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using DinoVehicle;
using DinoVehicle.Editor;

public static class BuildTestTrackScene
{
    [MenuItem("Dino Vehicle/Construir Escena de Pruebas")]
    public static void GenerateScene()
    {
        // 0. Asegurar materiales y configuración de DW Carnotauro
        DWSetup.ConfigureFBXAndMaterials();

        // 1. Crear nueva escena vacía
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Iluminación y Sol
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.88f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(48f, 38f, 0f);

        // 3. Suelo Principal (Pista de Pruebas)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Terreno_Pista";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300m x 300m
        Material groundMat = new Material(Shader.Find("Standard"));
        groundMat.color = new Color(0.24f, 0.28f, 0.20f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // Pista de arena / tierra
        GameObject trackStrip = GameObject.CreatePrimitive(PrimitiveType.Plane);
        trackStrip.name = "Pista_Camino";
        trackStrip.transform.position = new Vector3(0f, 0.01f, 0f);
        trackStrip.transform.localScale = new Vector3(3.5f, 1f, 25f);
        Material roadMat = new Material(Shader.Find("Standard"));
        roadMat.color = new Color(0.40f, 0.32f, 0.22f);
        trackStrip.GetComponent<MeshRenderer>().sharedMaterial = roadMat;

        // 4. Obstáculos: Rampas de Salto
        CreateRamp(new Vector3(0f, 0f, 40f), new Vector3(10f, 2.0f, 11f), 18f);
        CreateRamp(new Vector3(0f, 0f, 95f), new Vector3(12f, 3.5f, 14f), 24f);

        // 5. Cajas destructibles para chocar y demoler (física)
        CreateCratePyramid(new Vector3(0f, 0.5f, 68f), 4);
        CreateCratePyramid(new Vector3(-8f, 0.5f, 125f), 3);
        CreateCratePyramid(new Vector3(8f, 0.5f, 125f), 3);

        // Postes de Slalom para probar la dirección
        for (int i = 0; i < 7; i++)
        {
            float side = (i % 2 == 0) ? -4.5f : 4.5f;
            CreateSlalomPillar(new Vector3(side, 1.5f, -20f - i * 16f));
        }

        // 6. Cargar modelo del Carnotauro de DinoWest
        string fbxPath = "Assets/Dino/Model/DW_Carnotauro_UE5.fbx";
        GameObject dwCarnoAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Dino/Audio/Rugido.wav");
        RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Dino/Model/DW_Carnotaurus_Vehicle.controller");

        // Cargar Materiales PBR
        Material matPiel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dino/Materials/M_DW_Carnotauro_Piel.mat");
        Material matOjos = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dino/Materials/M_DW_Carnotauro_Ojos.mat");
        Material matSilla = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dino/Materials/M_DW_Silla_Cuero.mat");
        Material matHierro = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dino/Materials/M_DW_Hierro_Cadenas.mat");
        Material matCorreas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dino/Materials/M_DW_Correas_Negras.mat");

        // Raíz del Vehículo
        GameObject dinoRoot = new GameObject("Carnotaurus_Vehicle");
        dinoRoot.transform.position = new Vector3(0f, 0.1f, 0f);
        dinoRoot.transform.rotation = Quaternion.identity;

        // Física vehicular
        Rigidbody rb = dinoRoot.AddComponent<Rigidbody>();
        rb.mass = 1200f;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 2.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CapsuleCollider cap = dinoRoot.AddComponent<CapsuleCollider>();
        cap.radius = 1.0f;
        cap.height = 3.2f;
        cap.center = new Vector3(0f, 1.6f, 0.2f);

        // Audio
        AudioSource aSource = dinoRoot.AddComponent<AudioSource>();
        aSource.playOnAwake = false;
        aSource.clip = roarClip;

        // Scripts de Control y Telemetría
        DinoInputManager inputMgr = dinoRoot.AddComponent<DinoInputManager>();
        DinoVehicleController vehicle = dinoRoot.AddComponent<DinoVehicleController>();
        vehicle.audioSource = aSource;
        vehicle.roarClip = roarClip;
        vehicle.maxForwardSpeed = 18f;
        vehicle.turnSpeed = 70f;

        DinoAnimationDriver animDriver = dinoRoot.AddComponent<DinoAnimationDriver>();
        ArduinoSerialConnector serialConn = dinoRoot.AddComponent<ArduinoSerialConnector>();
        DinoHUD hud = dinoRoot.AddComponent<DinoHUD>();
        hud.vehicle = vehicle;
        hud.input = inputMgr;
        hud.arduino = serialConn;

        // Instanciar modelo hijo
        if (dwCarnoAsset != null)
        {
            GameObject dinoModel = (GameObject)PrefabUtility.InstantiatePrefab(dwCarnoAsset, dinoRoot.transform);
            dinoModel.name = "DW_Carnotaurus_Mesh";
            dinoModel.transform.localPosition = Vector3.zero;
            dinoModel.transform.localRotation = Quaternion.identity;
            dinoModel.transform.localScale = Vector3.one;

            vehicle.visualModel = dinoModel.transform;

            // Asignar Materiales a los Renderers
            var renderers = dinoModel.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    string mName = (mats[m] != null ? mats[m].name : r.gameObject.name).ToLower();
                    if (mName.Contains("ojo") || mName.Contains("eye"))
                        mats[m] = matOjos;
                    else if (mName.Contains("silla") || mName.Contains("cuero") || mName.Contains("saddle"))
                        mats[m] = matSilla;
                    else if (mName.Contains("cadena") || mName.Contains("hierro") || mName.Contains("metal") || mName.Contains("chain"))
                        mats[m] = matHierro;
                    else if (mName.Contains("correa") || mName.Contains("negro") || mName.Contains("bridle"))
                        mats[m] = matCorreas;
                    else
                        mats[m] = matPiel; // Por defecto piel del Carnotauro
                }
                r.sharedMaterials = mats;
            }

            // Animator
            Animator animator = dinoModel.GetComponent<Animator>();
            if (animator == null) animator = dinoModel.AddComponent<Animator>();
            if (animController != null) animator.runtimeAnimatorController = animController;
            animDriver.animator = animator;
        }

        // 7. Cámara de Seguimiento (Third-Person Dino Cam)
        GameObject camObj = new GameObject("Dino_Follow_Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();
        camObj.transform.position = new Vector3(0f, 4.0f, -7.5f);

        DinoFollowCamera followCam = camObj.AddComponent<DinoFollowCamera>();
        followCam.target = dinoRoot.transform;
        followCam.vehicle = vehicle;
        followCam.distance = 7.0f;
        followCam.height = 3.2f;
        followCam.lookAtHeight = 1.6f;

        // 8. Guardar la escena
        string scenePath = "Assets/Dino/Scenes/DinoTestTrack.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("? ¡Escena DinoTestTrack con DW Carnotauro construida y guardada exitosamente en " + scenePath + "!");

        // Añadir a Build Settings
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = scenes;
    }

    [MenuItem("Dino Vehicle/Compilar Ejecutable de Prueba (.exe)")]
    public static void BuildStandaloneExe()
    {
        GenerateScene();

        string buildFolder = "Builds";
        if (!System.IO.Directory.Exists(buildFolder))
        {
            System.IO.Directory.CreateDirectory(buildFolder);
        }

        string exePath = System.IO.Path.Combine(buildFolder, "DinoRider.exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Dino/Scenes/DinoTestTrack.unity" },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"? ¡Build exitoso! Ejecutable listo en: {exePath}");
        }
        else
        {
            Debug.LogError($"Error en build: {report.summary.result}");
        }
    }

    private static void CreateRamp(Vector3 position, Vector3 size, float angle)
    {
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Rampa_Salto";
        ramp.transform.position = position + new Vector3(0f, size.y * 0.35f, 0f);
        ramp.transform.localScale = size;
        ramp.transform.rotation = Quaternion.Euler(angle, 0f, 0f);

        Material rampMat = new Material(Shader.Find("Standard"));
        rampMat.color = new Color(0.55f, 0.32f, 0.15f);
        ramp.GetComponent<MeshRenderer>().sharedMaterial = rampMat;
    }

    private static void CreateCratePyramid(Vector3 basePos, int levels)
    {
        Material crateMat = new Material(Shader.Find("Standard"));
        crateMat.color = new Color(0.70f, 0.50f, 0.22f);

        float crateSize = 1.0f;
        for (int y = 0; y < levels; y++)
        {
            int countInRow = levels - y;
            float startX = -(countInRow - 1) * (crateSize * 1.05f) * 0.5f;

            for (int x = 0; x < countInRow; x++)
            {
                Vector3 pos = basePos + new Vector3(startX + x * (crateSize * 1.05f), y * (crateSize * 1.02f) + crateSize * 0.5f, 0f);
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = $"Caja_Destructible_{y}_{x}";
                crate.transform.position = pos;
                crate.transform.localScale = Vector3.one * crateSize;
                crate.GetComponent<MeshRenderer>().sharedMaterial = crateMat;

                Rigidbody rb = crate.AddComponent<Rigidbody>();
                rb.mass = 25f;
            }
        }
    }

    private static void CreateSlalomPillar(Vector3 pos)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pilar_Slalom";
        pillar.transform.position = pos;
        pillar.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.9f, 0.2f, 0.1f);
        pillar.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
