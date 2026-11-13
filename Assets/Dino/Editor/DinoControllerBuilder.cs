using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DinoVehicle
{
    public static class DinoControllerBuilder
    {
        const string OutDir = "Assets/Dino/Animators";

        // Crear assets dentro de InitializeOnLoad deja el controller sin su StateMachine
        // (la AssetDatabase aún no está lista), así que se difiere al primer tick del editor.
        [InitializeOnLoadMethod]
        static void AutoBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.playModeStateChanged += AlSalirDePlay;
                    return;
                }
                BuildControllers();
            };
        }

        static void AlSalirDePlay(PlayModeStateChange estado)
        {
            if (estado != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= AlSalirDePlay;
            BuildControllers();
        }

        [MenuItem("DINO WEST/Construir Animators de Monturas")]
        public static void BuildControllers()
        {
            if (!AssetDatabase.IsValidFolder(OutDir))
            {
                AssetDatabase.CreateFolder("Assets/Dino", "Animators");
            }

            BuildController(
                OutDir + "/Ctrl_Velociraptor.controller",
                "Assets/PBRVelociraptor/Models/15K/Raptor_Animated_FBX.fbx",
                "RaptorArmature|Raptor_Idle1_Anim",
                "RaptorArmature|Raptor_Walk_Anim",
                "RaptorArmature|Raptor_Run1_Anim",
                "RaptorArmature|Raptor_Roar1_Anim");

            BuildController(
                OutDir + "/Ctrl_Pachycephalosaurus.controller",
                "Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Models/LODG/Pachycephalasaurus.fbx",
                "Armature|Pachycephalasaurus_IdleA",
                "Armature|Pachycephalasaurus_Walk",
                "Armature|Pachycephalasaurus_Run",
                "Armature|Pachycephalasaurus_Roar");
        }

        /// <summary>Un controller sirve si su capa base tiene StateMachine con al menos un estado.</summary>
        public static bool EsValido(AnimatorController ctrl)
        {
            if (ctrl == null) return false;
            var layers = ctrl.layers;
            return layers.Length > 0
                && layers[0].stateMachine != null
                && layers[0].stateMachine.states.Length > 0;
        }

        static void BuildController(string path, string fbxPath, string idle, string walk, string run, string roar)
        {
            var existente = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (EsValido(existente)) return;

            AnimationClip idleClip = null, walkClip = null, runClip = null, roarClip = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                // Nombre exacto: evita los clips "__preview__" y las variantes _RM (root motion).
                if (a is not AnimationClip c) continue;
                if (c.name == idle) idleClip = c;
                else if (c.name == walk) walkClip = c;
                else if (c.name == run) runClip = c;
                else if (c.name == roar) roarClip = c;
            }

            if (idleClip == null && walkClip == null && runClip == null)
            {
                Debug.LogWarning($"[DinoControllerBuilder] No se encontraron clips en {fbxPath}; no se crea {path}");
                return;
            }

            // Un controller roto (sin StateMachine) se borra y se regenera.
            if (System.IO.File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var root = ctrl.layers[0].stateMachine;

            var idleState = root.AddState("Idle");
            idleState.motion = idleClip;
            root.AddState("Walk").motion = walkClip;
            root.AddState("Run").motion = runClip;
            root.AddState("Roar").motion = roarClip;
            root.defaultState = idleState;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[DinoControllerBuilder]</color> Creado controller: {path}");
        }
    }
}
