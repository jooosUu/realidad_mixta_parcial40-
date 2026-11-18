using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Convierte la pelota en una foca (se añade sola desde DinoBallToy; no hace falta tocar escenas):
    /// - Aletas delanteras y cola en 3D, con la misma textura de lápiz (T_Pelota_Foca).
    /// - Tentetieso: cuando va despacio se endereza sola y gira la cara hacia el Carnotauro.
    /// - Se aplasta y rebota al recibir golpes (squash &amp; stretch) y agita las aletas al rodar.
    /// La física (Rigidbody + SphereCollider) no cambia: solo cambia lo que se ve.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DinoPelotaFoca : MonoBehaviour
    {
        [Header("Tentetieso")]
        [Tooltip("Por debajo de esta velocidad (m/s) la foca se endereza")]
        public float velocidadEnderezar = 1.6f;
        [Tooltip("Fuerza con la que vuelve a ponerse derecha")]
        public float fuerzaEnderezar = 14f;
        public float amortiguacion = 4.5f;
        [Tooltip("Gira la cara hacia el jugador cuando está quieta")]
        public bool mirarAlJugador = true;

        [Header("Aplastamiento")]
        public float aplastamientoMax = 0.32f;
        public float rigidezRebote = 140f;
        public float amortiguacionRebote = 9f;

        Rigidbody rb;
        SphereCollider esfera;
        Transform pivote;      // orientado al golpe; se escala para aplastar
        Transform visual;      // copia la rotación de la pelota
        Transform aletaIzq, aletaDer, cola;
        Quaternion rotIzq, rotDer, rotCola;
        DinoVehicleController jugador;

        float aplastar, velAplastar;
        Vector3 ejeAplastar = Vector3.up;
        float fase;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            esfera = GetComponent<SphereCollider>();
        }

        void Start()
        {
            jugador = FindAnyObjectByType<DinoVehicleController>();
            ConstruirVisual();
        }

        void ConstruirVisual()
        {
            var mr = GetComponent<MeshRenderer>();
            var mf = GetComponent<MeshFilter>();
            if (mr == null || mf == null) return;

            // pelota -> pivote (aplasta en mundo) -> visual (gira con la pelota) -> malla + aletas
            pivote = new GameObject("Foca_Pivote").transform;
            pivote.SetParent(transform, false);
            visual = new GameObject("Foca_Visual").transform;
            visual.SetParent(pivote, false);

            var cuerpo = new GameObject("Foca_Cuerpo");
            cuerpo.transform.SetParent(visual, false);
            cuerpo.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var cuerpoMr = cuerpo.AddComponent<MeshRenderer>();
            cuerpoMr.sharedMaterials = mr.sharedMaterials;
            mr.enabled = false;

            var piel = mr.sharedMaterial;
            var matAleta = new Material(piel != null ? piel : new Material(Shader.Find("Standard"))) { name = "Foca_Aleta" };
            matAleta.color = new Color(0.62f, 0.62f, 0.64f);
            matAleta.mainTextureScale = new Vector2(0.15f, 0.15f);
            matAleta.mainTextureOffset = new Vector2(0.1f, 0.15f); // zona del lomo, con alguna mancha

            // Coordenadas locales de la esfera (radio 0.5). La cara mira hacia -Z.
            aletaIzq = Aleta("Aleta_Izq", matAleta, new Vector3(-0.43f, -0.24f, -0.12f), new Vector3(0f, -25f, 35f), new Vector3(0.34f, 0.08f, 0.2f));
            aletaDer = Aleta("Aleta_Der", matAleta, new Vector3(0.43f, -0.24f, -0.12f), new Vector3(0f, 25f, -35f), new Vector3(0.34f, 0.08f, 0.2f));
            cola = Aleta("Cola", matAleta, new Vector3(0f, -0.36f, 0.42f), new Vector3(-20f, 0f, 0f), new Vector3(0.36f, 0.07f, 0.26f));
            rotIzq = aletaIzq.localRotation;
            rotDer = aletaDer.localRotation;
            rotCola = cola.localRotation;
        }

        Transform Aleta(string nombre, Material mat, Vector3 pos, Vector3 rot, Vector3 escala)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = nombre;
            Destroy(go.GetComponent<Collider>()); // solo decorativa: la física es la de la esfera
            go.transform.SetParent(visual, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(rot);
            go.transform.localScale = escala;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic) return;
            float vel = rb.linearVelocity.magnitude;
            if (vel > velocidadEnderezar || !EnSuelo()) return;

            // Tentetieso: par para llevar la pelota a "derecha y mirando al jugador".
            Vector3 frente = -transform.forward; // la cara mira hacia -Z local
            Vector3 objetivoFrente = frente;
            if (mirarAlJugador && jugador != null)
            {
                Vector3 d = jugador.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.5f && d.magnitude < 40f) objetivoFrente = d.normalized;
            }
            objetivoFrente.y = 0f;
            if (objetivoFrente.sqrMagnitude < 0.01f) objetivoFrente = Vector3.ProjectOnPlane(frente, Vector3.up);
            if (objetivoFrente.sqrMagnitude < 0.01f) objetivoFrente = Vector3.forward;

            Quaternion deseada = Quaternion.LookRotation(-objetivoFrente.normalized, Vector3.up);
            Quaternion delta = deseada * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angulo, out Vector3 eje);
            if (angulo > 180f) angulo -= 360f;
            if (float.IsNaN(eje.x) || Mathf.Abs(angulo) < 0.5f) return;

            float k = Mathf.InverseLerp(velocidadEnderezar, 0f, vel); // más fuerte cuanto más quieta
            Vector3 par = eje.normalized * (angulo * Mathf.Deg2Rad * fuerzaEnderezar * k) - rb.angularVelocity * amortiguacion * k;
            rb.AddTorque(par, ForceMode.Acceleration);
        }

        bool EnSuelo()
        {
            float r = esfera != null ? esfera.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) : 1.6f;
            return Physics.Raycast(transform.position, Vector3.down, r + 0.25f, ~0, QueryTriggerInteraction.Ignore);
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 2f) return;
            if (c.contactCount > 0) ejeAplastar = c.GetContact(0).normal;
            velAplastar += Mathf.Clamp(v * 0.09f, 0f, 2.5f);
        }

        void LateUpdate()
        {
            if (pivote == null) return;
            float dt = Time.deltaTime;

            // Muelle amortiguado: se aplasta en la dirección del golpe y rebota.
            float acc = -rigidezRebote * aplastar - amortiguacionRebote * velAplastar;
            velAplastar += acc * dt;
            aplastar = Mathf.Clamp(aplastar + velAplastar * dt, -aplastamientoMax * 0.6f, aplastamientoMax);

            // El pivote apunta al eje del golpe en el mundo; el visual compensa para seguir la rotación real.
            pivote.rotation = Quaternion.FromToRotation(Vector3.up, ejeAplastar);
            float s = 1f - aplastar;
            float lado = 1f / Mathf.Sqrt(Mathf.Max(0.2f, s)); // conserva el volumen
            pivote.localScale = new Vector3(lado, s, lado);
            visual.rotation = transform.rotation;

            // Aletas: aletean al rodar y se mecen despacio en reposo.
            float vel = rb != null ? rb.linearVelocity.magnitude : 0f;
            fase += dt * Mathf.Lerp(2f, 14f, Mathf.Clamp01(vel / 10f));
            float amp = Mathf.Lerp(6f, 28f, Mathf.Clamp01(vel / 8f));
            float a = Mathf.Sin(fase) * amp;
            aletaIzq.localRotation = rotIzq * Quaternion.Euler(0f, 0f, a);
            aletaDer.localRotation = rotDer * Quaternion.Euler(0f, 0f, -a);
            cola.localRotation = rotCola * Quaternion.Euler(Mathf.Sin(fase * 0.5f) * amp * 0.6f, 0f, 0f);
        }
    }
}
