using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DinoVehicle.Editor
{
    public static class DWSetup
    {
        [MenuItem("Dino Vehicle/Configurar Importacion FBX y Materiales DW")]
        public static void ConfigureFBXAndMaterials()
        {
            // 1. Configurar Textura de Normales
            string normalTexPath = "Assets/Dino/Textures/DW_Carnotauro_Normales.png";
            TextureImporter normalImporter = AssetImporter.GetAtPath(normalTexPath) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
                Debug.Log("[DWSetup] Textura de normales configurada correctamente.");
            }

            // 2. Configurar Importador del FBX
            string fbxPath = "Assets/Dino/Model/DW_Carnotauro_UE5.fbx";
            ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (mi != null)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.importAnimation = true;
                mi.preserveHierarchy = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                // Configurar bucles en animaciones
                ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    List<ModelImporterClipAnimation> customClips = new List<ModelImporterClipAnimation>();
                    foreach (var c in clips)
                    {
                        string nameLower = c.name.ToLower();
                        if (nameLower.Contains("idle") || nameLower.Contains("walk") || nameLower.Contains("run"))
                        {
                            c.loopTime = true;
                            c.loopPose = true;
                        }
                        customClips.Add(c);
                        Debug.Log($"[DWSetup] Clip encontrado: {c.name} (Loop: {c.loopTime})");
                    }
                    mi.clipAnimations = customClips.ToArray();
                }

                mi.SaveAndReimport();
                Debug.Log("[DWSetup] ModelImporter de DW_Carnotauro_UE5 configurado.");
            }

            // 3. Crear Materiales PBR
            CreatePBRMaterials();

            // 4. Crear Animator Controller
            CreateDinoAnimatorController();
        }

        public static RuntimeAnimatorController CreateDinoAnimatorController()
        {
            string fbxPath = "Assets/Dino/Model/DW_Carnotauro_UE5.fbx";
            string controllerPath = "Assets/Dino/Model/DW_Carnotaurus_Vehicle.controller";

            UnityEditor.Animations.AnimatorController ac = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Parámetros
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Roar", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            // Cargar Clips del FBX
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip idleClip = null;
            AnimationClip walkClip = null;
            AnimationClip runClip = null;
            AnimationClip roarClip = null;
            AnimationClip attackClip = null;

            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    string n = clip.name.ToLower();
                    Debug.Log($"[DWSetup] Encontrado clip de animación: {clip.name}");
                    if (n.Contains("idle")) idleClip = clip;
                    else if (n.Contains("walk")) walkClip = clip;
                    else if (n.Contains("run")) runClip = clip;
                    else if (n.Contains("roar")) roarClip = clip;
                    else if (n.Contains("attack") || n.Contains("horn")) attackClip = clip;
                }
            }

            var rootStateMachine = ac.layers[0].stateMachine;

            // Estados
            var stateIdle = rootStateMachine.AddState("Idle");
            if (idleClip != null) stateIdle.motion = idleClip;

            var stateWalk = rootStateMachine.AddState("Walk");
            if (walkClip != null) stateWalk.motion = walkClip;

            var stateRun = rootStateMachine.AddState("Run");
            if (runClip != null) stateRun.motion = runClip;

            var stateRoar = rootStateMachine.AddState("Roar");
            if (roarClip != null) stateRoar.motion = roarClip;

            var stateAttack = rootStateMachine.AddState("Attack");
            if (attackClip != null) stateAttack.motion = attackClip;

            rootStateMachine.defaultState = stateIdle;

            // Transición Idle <-> Walk
            var tIdleToWalk = stateIdle.AddTransition(stateWalk);
            tIdleToWalk.hasExitTime = false;
            tIdleToWalk.duration = 0.2f;
            tIdleToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 1.2f, "Speed");

            var tWalkToIdle = stateWalk.AddTransition(stateIdle);
            tWalkToIdle.hasExitTime = false;
            tWalkToIdle.duration = 0.25f;
            tWalkToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.8f, "Speed");

            // Transición Walk <-> Run
            var tWalkToRun = stateWalk.AddTransition(stateRun);
            tWalkToRun.hasExitTime = false;
            tWalkToRun.duration = 0.2f;
            tWalkToRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 22.0f, "Speed");

            var tRunToWalk = stateRun.AddTransition(stateWalk);
            tRunToWalk.hasExitTime = false;
            tRunToWalk.duration = 0.25f;
            tRunToWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 19.0f, "Speed");

            // Transición Any State -> Roar
            var tAnyToRoar = rootStateMachine.AddAnyStateTransition(stateRoar);
            tAnyToRoar.hasExitTime = false;
            tAnyToRoar.duration = 0.15f;
            tAnyToRoar.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Roar");

            var tRoarToIdle = stateRoar.AddTransition(stateIdle);
            tRoarToIdle.hasExitTime = true;
            tRoarToIdle.duration = 0.3f;

            // Transición Any State -> Attack
            var tAnyToAttack = rootStateMachine.AddAnyStateTransition(stateAttack);
            tAnyToAttack.hasExitTime = false;
            tAnyToAttack.duration = 0.15f;
            tAnyToAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Attack");

            var tAttackToIdle = stateAttack.AddTransition(stateIdle);
            tAttackToIdle.hasExitTime = true;
            tAttackToIdle.duration = 0.3f;

            AssetDatabase.SaveAssets();
            Debug.Log($"[DWSetup] AnimatorController creado exitosamente en {controllerPath}");
            return ac;
        }

        public static void CreatePBRMaterials()
        {
            Shader standardShader = Shader.Find("Standard");

            Texture2D texPiel = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dino/Textures/DW_Carnotauro_Piel_NegroRojo.png");
            Texture2D texNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dino/Textures/DW_Carnotauro_Normales.png");
            Texture2D texOjos = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Dino/Textures/DW_Carnotauro_Ojos.png");

            // Material Piel Dino
            Material matPiel = new Material(standardShader);
            matPiel.name = "M_DW_Carnotauro_Piel";
            if (texPiel != null) matPiel.mainTexture = texPiel;
            if (texNormal != null)
            {
                matPiel.SetTexture("_BumpMap", texNormal);
                matPiel.EnableKeyword("_NORMALMAP");
            }
            matPiel.SetFloat("_Glossiness", 0.35f);
            matPiel.SetFloat("_Metallic", 0.05f);
            AssetDatabase.CreateAsset(matPiel, "Assets/Dino/Materials/M_DW_Carnotauro_Piel.mat");

            // Material Ojos
            Material matOjos = new Material(standardShader);
            matOjos.name = "M_DW_Carnotauro_Ojos";
            if (texOjos != null) matOjos.mainTexture = texOjos;
            matOjos.SetFloat("_Glossiness", 0.85f);
            matOjos.SetFloat("_Metallic", 0.1f);
            AssetDatabase.CreateAsset(matOjos, "Assets/Dino/Materials/M_DW_Carnotauro_Ojos.mat");

            // Material Cuero Silla de Montar
            Material matSilla = new Material(standardShader);
            matSilla.name = "M_DW_Silla_Cuero";
            matSilla.color = new Color(0.22f, 0.12f, 0.07f); // Cuero marrón oscuro desgastado
            matSilla.SetFloat("_Glossiness", 0.45f);
            matSilla.SetFloat("_Metallic", 0.0f);
            AssetDatabase.CreateAsset(matSilla, "Assets/Dino/Materials/M_DW_Silla_Cuero.mat");

            // Material Hierro / Cadenas
            Material matHierro = new Material(standardShader);
            matHierro.name = "M_DW_Hierro_Cadenas";
            matHierro.color = new Color(0.35f, 0.35f, 0.38f);
            matHierro.SetFloat("_Glossiness", 0.65f);
            matHierro.SetFloat("_Metallic", 0.9f);
            AssetDatabase.CreateAsset(matHierro, "Assets/Dino/Materials/M_DW_Hierro_Cadenas.mat");

            // Material Correas / Cabezada
            Material matCorreas = new Material(standardShader);
            matCorreas.name = "M_DW_Correas_Negras";
            matCorreas.color = new Color(0.08f, 0.08f, 0.08f);
            matCorreas.SetFloat("_Glossiness", 0.3f);
            AssetDatabase.CreateAsset(matCorreas, "Assets/Dino/Materials/M_DW_Correas_Negras.mat");

            AssetDatabase.SaveAssets();
            Debug.Log("[DWSetup] Materiales PBR creados en Assets/Dino/Materials/");
        }
    }
}
