using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DinoVehicle
{
    /// <summary>
    /// Escenario del menú de inicio: una copia del dino de la escena, lejos del mundo,
    /// sobre un piso oscuro con un haz de luz cálida y contraluces rojos.
    /// Solo se ve con su propia cámara (capa 31); las cámaras del juego se apagan mientras está activo.
    /// </summary>
    public class DinoEscenarioInicio : MonoBehaviour
    {
        const int Capa = 31, CapaPiso = 30;
        static readonly Vector3 Origen = new Vector3(0f, -2000f, 0f);
        const float Mirada = -150f, Vaiven = 10f;   // hacia dónde mira el dino: adelante-izquierda de la cámara
        float yawBase = Mirada;

        Camera cam;
        Transform modelo;
        Animator anim;
        readonly List<Behaviour> camarasApagadas = new();
        readonly List<(Light luz, int mascara)> lucesEscena = new();
        AmbientMode ambModo; Color ambColor; float ambIntensidad; bool niebla; FogMode nieblaModo; Color nieblaColor; float nieblaIni, nieblaFin;
        float distCam = 12f;
        Quaternion rotBase;
        Transform cabeza;
        public bool Activo { get; private set; }

        public static DinoEscenarioInicio Crear()
        {
            var go = new GameObject("DINO WEST · Escenario de inicio");
            var e = go.AddComponent<DinoEscenarioInicio>();
            e.Construir();
            return e;
        }

        void Construir()
        {
            transform.position = Origen;

            // --- piso ---
            var piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(piso.GetComponent<Collider>());
            piso.name = "Piso";
            piso.transform.SetParent(transform, false);
            piso.transform.localScale = Vector3.one * 8f;
            var mat = Resources.Load<Material>("DinoUI/PisoEscenario");
            if (mat != null) piso.GetComponent<Renderer>().sharedMaterial = mat;
            piso.layer = CapaPiso;

            // --- dino: copia del modelo visual del vehículo ---
            var vehiculo = FindAnyObjectByType<DinoVehicleController>();
            Transform fuente = vehiculo != null ? (vehiculo.visualModel != null ? vehiculo.visualModel : vehiculo.transform) : null;
            if (fuente != null)
            {
                var contenedor = new GameObject("Dino");
                contenedor.SetActive(false);   // así los scripts copiados no despiertan
                contenedor.transform.SetParent(transform, false);
                var copia = Instantiate(fuente.gameObject, contenedor.transform);
                copia.transform.localPosition = Vector3.zero;
                copia.transform.localRotation = Quaternion.Inverse(vehiculo.transform.rotation) * fuente.rotation;
                copia.transform.localScale = fuente.lossyScale;
                Limpiar(copia);
                foreach (var t in copia.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Capa;
                foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                }
                anim = copia.GetComponentInChildren<Animator>();
                if (anim != null) { anim.updateMode = AnimatorUpdateMode.UnscaledTime; anim.applyRootMotion = false; }
                modelo = contenedor.transform;
                contenedor.SetActive(true);

                // apoyar en el piso
                var b = Limites(copia);
                copia.transform.position += Vector3.up * (Origen.y - b.min.y);
                rotBase = Quaternion.identity;

                // orientar por la cabeza: así queda en tres cuartos aunque el modelo venga girado
                cabeza = null;
                foreach (var t in copia.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("head") || n.Contains("cabeza")) { cabeza = t; break; }
                }
                if (cabeza != null)
                {
                    var c = Limites(copia).center;
                    Vector3 d = cabeza.position - c; d.y = 0;
                    if (d.sqrMagnitude > 0.01f)
                        yawBase = Mirada - Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
            }

            // --- cámara ---
            var cgo = new GameObject("Cámara de escenario");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.cullingMask = (1 << Capa) | (1 << CapaPiso);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(10, 7, 6, 255);
            cam.fieldOfView = 30f;
            cam.depth = 50;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // --- luces ---
            int todo = (1 << Capa) | (1 << CapaPiso), dino = 1 << Capa;
            Luz("Haz cálido", LightType.Spot, new Color(1f, 0.8f, 0.6f), 5.5f, new Vector3(-3f, 9f, -3.5f), 62f, LightShadows.Soft, todo);
            Luz("Contra roja", LightType.Spot, new Color(1f, 0.25f, 0.12f), 6f, new Vector3(3.5f, 4.5f, 6f), 55f, LightShadows.None, dino);
            Luz("Contra roja baja", LightType.Spot, new Color(1f, 0.32f, 0.16f), 3.5f, new Vector3(-5f, 2.5f, 5f), 55f, LightShadows.None, dino);
            Luz("Relleno frío", LightType.Point, new Color(0.55f, 0.6f, 0.75f), 0.6f, new Vector3(0f, 2f, -8f), 0f, LightShadows.None, dino);

            Encuadrar();
            gameObject.SetActive(false);
        }

        static void Limpiar(GameObject copia)
        {
            // primero lo que depende de otros componentes
            foreach (var c in copia.GetComponentsInChildren<Joint>(true)) DestroyImmediate(c);
            foreach (var c in copia.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);
            foreach (var c in copia.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var c in copia.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(c);
            foreach (var c in copia.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(c);
            foreach (var c in copia.GetComponentsInChildren<Camera>(true)) DestroyImmediate(c);
            foreach (var c in copia.GetComponentsInChildren<Light>(true)) DestroyImmediate(c);
        }

        static Bounds Limites(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        void Luz(string nombre, LightType tipo, Color c, float intensidad, Vector3 pos, float angulo, LightShadows sombras, int mascara)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = tipo;
            l.color = c;
            l.intensity = intensidad;
            l.range = 30f;
            if (tipo == LightType.Spot) l.spotAngle = angulo;
            l.shadows = sombras;
            l.cullingMask = mascara;
            go.transform.LookAt(transform.position + Vector3.up * 1.5f);
        }

        void Encuadrar()
        {
            if (modelo == null) { cam.transform.localPosition = new Vector3(0, 2, -10); return; }
            modelo.localRotation = Quaternion.Euler(0f, yawBase, 0f) * rotBase;
            var b = Limites(modelo.gameObject);
            float radio = b.extents.magnitude;
            float dist = radio / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.82f;
            // el dino queda a la derecha del centro: la cámara mira a un punto a su izquierda
            Vector3 mira = b.center + Vector3.left * radio * 0.28f - Vector3.up * radio * 0.08f;
            // cámara un poco alta mirando hacia abajo: el horizonte del piso queda fuera del cuadro
            cam.transform.position = mira + Quaternion.Euler(23f, 0f, 0f) * new Vector3(0f, 0f, -dist);
            distCam = dist;
            cam.transform.LookAt(mira);
        }

        void Update()
        {
            if (modelo == null) return;
            float t = Time.unscaledTime;
            modelo.localRotation = Quaternion.Euler(0f, yawBase + Mathf.Sin(t * 0.35f) * Vaiven, 0f) * rotBase;
        }

        public void Rugir()
        {
            if (anim == null) return;
            foreach (var p in anim.parameters)
                if (p.name == "Roar" && p.type == AnimatorControllerParameterType.Trigger) { anim.SetTrigger("Roar"); return; }
        }

        public void CambiarModelo(int mountIdx)
        {
            if (mountIdx < 0 || mountIdx >= DinoMountManager.Mounts.Length) mountIdx = 0;
            var def = DinoMountManager.Mounts[mountIdx];
            if (!def.Disponible)
            {
                MostrarDino(false);
                return;
            }
            MostrarDino(true);

            if (modelo != null)
            {
                Destroy(modelo.gameObject);
                modelo = null;
            }

            var vehiculo = FindAnyObjectByType<DinoVehicleController>();
            GameObject prefab = null;

            if (mountIdx == 0 || string.IsNullOrEmpty(def.PrefabPath) || def.PrefabPath == "Carnotauro_Default")
            {
                Transform fuente = vehiculo != null ? (vehiculo.visualModel != null ? vehiculo.visualModel : vehiculo.transform) : null;
                if (fuente != null) prefab = fuente.gameObject;
            }
            else
            {
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath);
#endif
            }

            if (prefab != null)
            {
                var contenedor = new GameObject("Dino");
                contenedor.SetActive(false);
                contenedor.transform.SetParent(transform, false);

                var copia = Instantiate(prefab, contenedor.transform);
                copia.transform.localPosition = Vector3.zero;
                copia.transform.localRotation = Quaternion.identity;
                copia.transform.localScale = Vector3.one * def.VisualScale;
                Limpiar(copia);

                foreach (var t in copia.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Capa;
                foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                    var mats = r.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != null && (mats[m].shader == null || mats[m].shader.name == "Hidden/InternalErrorShader"))
                            mats[m].shader = Shader.Find("Standard");
                    }
                    r.sharedMaterials = mats;
                }

                anim = copia.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                    anim.applyRootMotion = false;
#if UNITY_EDITOR
                    if (mountIdx == 1) anim.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Dino/Animators/Ctrl_Velociraptor.controller") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PBRVelociraptor/Animators/DemoAnimatorA.controller");
                    else if (mountIdx == 2) anim.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Dino/Animators/Ctrl_Pachycephalosaurus.controller") ?? UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/FerociousIndustries/PBRDinosaurs/PBRPachycephalasaurus/Animators/DemoAnimatorA.controller");
                    else if (mountIdx == 3) anim.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/FerociousIndustries/PBRDinosaurs/PBRStegasaurus/Animators/DemoAnimator_Idle.controller");
#endif
                }

                modelo = contenedor.transform;
                contenedor.SetActive(true);

                var b = Limites(copia);
                copia.transform.position += Vector3.up * (Origen.y - b.min.y);
                rotBase = Quaternion.identity;

                cabeza = null;
                foreach (var t in copia.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("head") || n.Contains("cabeza")) { cabeza = t; break; }
                }
                if (cabeza != null)
                {
                    var c = Limites(copia).center;
                    Vector3 d = cabeza.position - c; d.y = 0;
                    if (d.sqrMagnitude > 0.01f)
                        yawBase = Mirada - Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
                else
                {
                    yawBase = Mirada;
                }

                Encuadrar();
                Rugir();
            }
        }

        public void MostrarDino(bool visible)
        {
            if (modelo != null) modelo.gameObject.SetActive(visible);
        }

        public void Encender()
        {
            if (Activo) return;
            Activo = true;
            gameObject.SetActive(true);
            camarasApagadas.Clear();
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c != cam && c.enabled) { c.enabled = false; camarasApagadas.Add(c); }
            lucesEscena.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.transform.parent != transform) { lucesEscena.Add((l, l.cullingMask)); l.cullingMask &= ~((1 << Capa) | (1 << CapaPiso)); }
            ambModo = RenderSettings.ambientMode; ambColor = RenderSettings.ambientLight;
            ambIntensidad = RenderSettings.ambientIntensity; niebla = RenderSettings.fog;
            nieblaModo = RenderSettings.fogMode; nieblaColor = RenderSettings.fogColor;
            nieblaIni = RenderSettings.fogStartDistance; nieblaFin = RenderSettings.fogEndDistance;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(14, 10, 9, 255);
            // niebla del color del fondo: el piso se pierde en la oscuridad, sin horizonte
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = distCam + 1f;
            RenderSettings.fogEndDistance = distCam + 14f;
            Rugir();
        }

        public void Apagar()
        {
            if (!Activo) return;
            Activo = false;
            foreach (var c in camarasApagadas) if (c != null) c.enabled = true;
            camarasApagadas.Clear();
            foreach (var (l, m) in lucesEscena) if (l != null) l.cullingMask = m;
            lucesEscena.Clear();
            RenderSettings.ambientMode = ambModo; RenderSettings.ambientLight = ambColor;
            RenderSettings.ambientIntensity = ambIntensidad; RenderSettings.fog = niebla;
            RenderSettings.fogMode = nieblaModo; RenderSettings.fogColor = nieblaColor;
            RenderSettings.fogStartDistance = nieblaIni; RenderSettings.fogEndDistance = nieblaFin;
            gameObject.SetActive(false);
        }
    }
}
