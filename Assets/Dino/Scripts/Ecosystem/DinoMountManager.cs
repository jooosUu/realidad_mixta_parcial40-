using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DinoVehicle
{
    /// <summary>
    /// Gestiona las monturas jugables de DinoWest.
    /// Permite al jugador elegir entre Carnotauro, Velociraptor y Paquicefalosaurio en el menú,
    /// cambiando el modelo 3D visual, las animaciones y las físicas (velocidad, turbo, masa, giro y salto).
    /// </summary>
    public static class DinoMountManager
    {
        public struct MountDef
        {
            public string Nombre;
            public bool Disponible;
            public string PrefabPath;
            public float MaxSpeedKmh;
            public float TurboMultiplier;
            public float MassKg;
            public float TurnSpeed;
            public float JumpImpulse;
            public float VisualScale;
            /// <summary>Giro en Y (grados) para modelos cuyo frente no es +Z.</summary>
            public float VisualYaw;
            public string Descripcion;

            public MountDef(string nom, bool disp, string path, float speed, float turbo, float mass, float turn, float jump, float scale, string desc, float yaw = 0f)
            {
                VisualYaw = yaw;
                Nombre = nom;
                Disponible = disp;
                PrefabPath = path;
                MaxSpeedKmh = speed;
                TurboMultiplier = turbo;
                MassKg = mass;
                TurnSpeed = turn;
                JumpImpulse = jump;
                VisualScale = scale;
                Descripcion = desc;
            }
        }

        public static readonly MountDef[] Mounts =
        {
            new MountDef(
                "Carnotauro",
                true,
                "Carnotauro_Default",
                58f,
                1.6f,
                1500f,
                75f,
                9f,
                1.0f,
                "Depredador ápice con montura vaquera. Gran inercia y derribo imparable de árboles en sprint."
            ),
            new MountDef(
                "Velociraptor",
                true,
                "Assets/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Sand_Female.prefab",
                75f,
                1.9f,
                120f,
                135f,
                15f,
                1.55f,
                "Montura ligera ultra veloz. Aceleración explosiva, virajes cerrados y saltos acrobáticos.",
                180f // El modelo del Velociraptor viene mirando hacia -Z
            ),
            new MountDef(
                "Paquicefalo",
                true,
                "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Prefabs/PBR/Pachycephalasaurus_Green.prefab",
                52f,
                1.65f,
                750f,
                95f,
                10f,
                1.35f,
                "Cráneo de ariete acorazado. Embestida demoledora capaz de pulverizar obstáculos y cajas."
            ),
            new MountDef(
                "Estegosaurio",
                true,
                "Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Prefabs/LODG/Stegasaurus_Green.prefab",
                42f,
                1.3f,
                2800f,
                55f,
                6f,
                1.2f,
                "Tanque pesado de cola espinada. Máxima resistencia a impactos y peso colosal."
            ),
            new MountDef(
                "???",
                false,
                null,
                0f,
                0f,
                0f,
                0f,
                0f,
                1.0f,
                "Montura bloqueada."
            )
        };

        const string PREF_KEY = "DinoWest_SelectedMount";

        public static int CurrentMountIndex
        {
            get => PlayerPrefs.GetInt(PREF_KEY, 0);
            set
            {
                int idx = Mathf.Clamp(value, 0, Mounts.Length - 1);
                PlayerPrefs.SetInt(PREF_KEY, idx);
                PlayerPrefs.Save();
            }
        }

        public static MountDef CurrentMount => Mounts[Mathf.Clamp(CurrentMountIndex, 0, Mounts.Length - 1)];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AlCargarEscena()
        {
            var vehicle = UnityEngine.Object.FindAnyObjectByType<DinoVehicleController>();
            if (vehicle != null)
            {
                AplicarMontura(vehicle, CurrentMountIndex);
            }
        }

        public static void AplicarMontura(DinoVehicleController vehicle, int mountIdx)
        {
            if (vehicle == null) return;
            if (mountIdx < 0 || mountIdx >= Mounts.Length) mountIdx = 0;

            var def = Mounts[mountIdx];
            if (!def.Disponible) return;

            // 1. Ajustar físicas del vehículo
            vehicle.maxForwardSpeed = def.MaxSpeedKmh / 3.6f;
            vehicle.boostMultiplier = def.TurboMultiplier;
            vehicle.turnSpeed = def.TurnSpeed;
            vehicle.jumpImpulse = def.JumpImpulse;

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = def.MassKg;
            }

            // 2. Gestionar modelo visual
            Transform visualParent = vehicle.visualModel != null ? vehicle.visualModel.parent : vehicle.transform;

            // Buscar contenedor de montura dinámica
            Transform dynamicMount = vehicle.transform.Find("DynamicMount_Visual");
            if (dynamicMount == null)
            {
                var go = new GameObject("DynamicMount_Visual");
                go.transform.SetParent(vehicle.transform, false);
                dynamicMount = go.transform;
            }

            // Si es Carnotauro por defecto
            if (mountIdx == 0 || string.IsNullOrEmpty(def.PrefabPath) || def.PrefabPath == "Carnotauro_Default")
            {
                if (vehicle.visualModel != null)
                {
                    vehicle.visualModel.gameObject.SetActive(true);
                }
                dynamicMount.gameObject.SetActive(false);

                var animDriver = vehicle.GetComponent<DinoAnimationDriver>();
                if (animDriver != null)
                {
                    animDriver.animator = vehicle.visualModel != null
                        ? vehicle.visualModel.GetComponentInChildren<Animator>()
                        : vehicle.GetComponentInChildren<Animator>();
                    animDriver.RefrescarParametros();
                }
                return;
            }

            // Para otras monturas (Velociraptor, Paquicefalo, etc.)
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath);
#endif
            if (prefab == null)
            {
                // Sin prefab (p. ej. en un build, donde AssetDatabase no existe) se conserva
                // el modelo base: ocultarlo dejaría al jugador sin dinosaurio visible.
                if (vehicle.visualModel != null) vehicle.visualModel.gameObject.SetActive(true);
                dynamicMount.gameObject.SetActive(false);
                Debug.LogWarning($"[DinoMountManager] No se pudo cargar el modelo de '{def.Nombre}'; se mantiene el modelo base con sus físicas.");
                return;
            }

            if (vehicle.visualModel != null)
            {
                vehicle.visualModel.gameObject.SetActive(false);
            }
            dynamicMount.gameObject.SetActive(true);

            // Limpiar hijos previos de la montura dinámica
            for (int i = dynamicMount.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(dynamicMount.GetChild(i).gameObject);
            }

            {
                var nuevoVisual = UnityEngine.Object.Instantiate(prefab, dynamicMount);
                nuevoVisual.transform.localPosition = Vector3.zero;
                nuevoVisual.transform.localRotation = Quaternion.Euler(0f, def.VisualYaw, 0f);
                nuevoVisual.transform.localScale = Vector3.one * def.VisualScale;
                nuevoVisual.name = $"Visual_{def.Nombre}";

                // Quitar scripts de IA si el prefab los traía
                var aiH = nuevoVisual.GetComponent<DinoAIHerbivore>();
                if (aiH != null) UnityEngine.Object.Destroy(aiH);
                var aiR = nuevoVisual.GetComponent<DinoAIRaptor>();
                if (aiR != null) UnityEngine.Object.Destroy(aiR);

                // Configurar animador
                var anim = nuevoVisual.GetComponentInChildren<Animator>();
                if (anim != null)
                {
#if UNITY_EDITOR
                    ConfigurarAnimatorParaMontura(anim, mountIdx);
#endif
                    var animDriver = vehicle.GetComponent<DinoAnimationDriver>();
                    if (animDriver != null)
                    {
                        animDriver.animator = anim;
                        animDriver.RefrescarParametros();
                    }
                }
            }

            Debug.Log($"<color=cyan>[DinoMountManager]</color> ¡Montura equipada: <b>{def.Nombre}</b>! (Vel: {def.MaxSpeedKmh:0} km/h, Masa: {def.MassKg:0} kg, Turbo: ×{def.TurboMultiplier:0.0})");
        }

#if UNITY_EDITOR
        public const string CtrlVelociraptor = "Assets/Dino/Animators/Ctrl_Velociraptor.controller";
        public const string CtrlPaquicefalo = "Assets/Dino/Animators/Ctrl_Pachycephalosaurus.controller";

        static void ConfigurarAnimatorParaMontura(Animator anim, int mountIdx)
        {
            RuntimeAnimatorController ctrl = null;
            if (mountIdx == 1) // Velociraptor
            {
                ctrl = CargarControllerValido(CtrlVelociraptor, "Assets/PBRVelociraptor/Animators/DemoAnimatorRun.controller");
            }
            else if (mountIdx == 2) // Paquicefalosaurio
            {
                ctrl = CargarControllerValido(CtrlPaquicefalo, "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Animators/DemoAnimatorRun2.controller");
            }
            else if (mountIdx == 3) // Estegosaurio
            {
                ctrl = CargarControllerValido("Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Animators/DemoAnimator_Run.controller");
            }

            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
        }

        /// <summary>
        /// Devuelve el primer controller de la lista que exista y tenga StateMachine con estados.
        /// Asignar uno vacío hace que Unity registre un error en cada frame.
        /// </summary>
        public static RuntimeAnimatorController CargarControllerValido(params string[] paths)
        {
            foreach (var path in paths)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
                if (ctrl == null) continue;

                var layers = ctrl.layers;
                if (layers.Length > 0 && layers[0].stateMachine != null && layers[0].stateMachine.states.Length > 0)
                {
                    return ctrl;
                }
            }
            return null;
        }
#endif
    }
}
