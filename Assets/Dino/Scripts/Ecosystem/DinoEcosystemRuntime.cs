using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DinoVehicle
{
    /// <summary>
    /// Generador del ecosistema en Play (respaldo).
    /// Si la escena NO tiene "Ecosistema_DinoWest" horneado, lo construye al instante con la misma
    /// distribución que el menú del editor (DinoEcosystemLayout):
    /// - ~95 árboles PBR en bosques alrededor de praderas y bordes, apoyados con Terrain.SampleHeight
    /// - 4 árboles de comida, rocas y flores con materiales Standard (BIRP)
    /// - Manadas de Estegosaurios, Paquicefalosaurios y Velociraptores con sus Animator reales
    /// - Anquilosaurios guardianes estáticos, pelota gigante e interacción de pájaros
    /// Si la escena ya lo tiene guardado, no hace nada (usa DINO WEST ▸ Construir Terreno y Ecosistema para regenerarlo).
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

            // Solo en las escenas de juego (con vehículo); no en menús u otras escenas.
            if (FindAnyObjectByType<DinoVehicleController>() == null) return;

            var go = new GameObject("Ecosistema_DinoWest");
            go.AddComponent<DinoEcosystemRuntime>().Generar();
        }

        public void Generar()
        {
            Debug.Log("<color=yellow>[DinoEcosystemRuntime]</color> Generando ecosistema completo en tiempo de ejecución...");

            var arboledaRoot = Hijo("01_Arboleda_PBR_Realista");
            var comidaRoot = Hijo("02_Arboles_Comida_Interactivas");
            var rocasRoot = Hijo("03_Rocas_PBR_Fotorrealistas");
            var floraRoot = Hijo("04_Flora_Flores_Y_Arbustos");
            var faunaRoot = Hijo("05_Fauna_Dinosaurios_Vivos");
            var juguetesRoot = Hijo("06_Juguetes_Y_Pelotas");

            var terreno = Terrain.activeTerrain;

#if UNITY_EDITOR
            System.Func<string, GameObject> cargar = AssetDatabase.LoadAssetAtPath<GameObject>;
            System.Func<GameObject, Transform, GameObject> instanciar = (p, padre) => Instantiate(p, padre);

            int arboles = DinoEcosystemLayout.PoblarArboles(arboledaRoot, comidaRoot, terreno, cargar, instanciar);
            DinoEcosystemLayout.PoblarRocasYFlora(rocasRoot, floraRoot, terreno, cargar, instanciar);
            int dinos = DinoEcosystemLayout.PoblarFauna(faunaRoot, terreno, cargar, instanciar, ControllerDe, null);
            Debug.Log($"<color=green>[DinoEcosystemRuntime]</color> {arboles} árboles y {dinos} dinosaurios generados.");
#else
            GenerarArboledaFallback(arboledaRoot, terreno);
#endif
            GenerarPelotaDinosaurio(juguetesRoot);
            ConfigurarJugador();

            Debug.Log("<color=green>[DinoEcosystemRuntime]</color> ¡Ecosistema completamente listo para jugar!");
        }

        Transform Hijo(string nombre)
        {
            var t = new GameObject(nombre).transform;
            t.SetParent(transform, false);
            return t;
        }

#if UNITY_EDITOR
        static RuntimeAnimatorController ControllerDe(DinoEcosystemLayout.Especie e)
        {
            string ruta = e switch
            {
                DinoEcosystemLayout.Especie.Estegosaurio => "Assets/Dino/Animators/Ctrl_Stegosaurus.controller",
                DinoEcosystemLayout.Especie.Paquicefalo => DinoMountManager.CtrlPaquicefalo,
                DinoEcosystemLayout.Especie.Raptor => DinoMountManager.CtrlVelociraptor,
                _ => null,
            };
            return ruta != null ? DinoMountManager.CargarControllerValido(ruta) : null;
        }
#else
        /// <summary>En un build no hay AssetDatabase: troncos simples como obstáculos.</summary>
        void GenerarArboledaFallback(Transform padre, Terrain terreno)
        {
            var puntos = DinoEcosystemLayout.GenerarArboles();
            var mat = new Material(Shader.Find("Standard")) { color = new Color(0.36f, 0.25f, 0.16f) };
            for (int i = 0; i < puntos.Count; i++)
            {
                var p = puntos[i].Pos;
                var tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tronco.name = $"Arbol_Tronco_{i + 1}";
                tronco.transform.SetParent(padre, false);
                tronco.transform.localScale = new Vector3(0.9f, 5f, 0.9f);
                tronco.transform.position = new Vector3(p.x, DinoEcosystemLayout.AlturaSuelo(terreno, p.x, p.y) + 5f, p.y);
                tronco.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
#endif

        void GenerarPelotaDinosaurio(Transform parent)
        {
            var pelota = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pelota.name = "Pelota_Dinosaurio_Prehistorica";
            pelota.transform.SetParent(parent, false);
            float y = DinoEcosystemLayout.AlturaSuelo(Terrain.activeTerrain, 12f, 25f);
            pelota.transform.position = new Vector3(12f, y + 1.7f, 25f);
            pelota.transform.localScale = Vector3.one * 3.2f;

            var ren = pelota.GetComponent<Renderer>();
#if UNITY_EDITOR
            // Misma piel de foca que en las escenas horneadas.
            var piel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dino/Materials/M_DW_Pelota.mat");
            if (ren != null && piel != null) ren.sharedMaterial = piel;
            else
#endif
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
    }
}
