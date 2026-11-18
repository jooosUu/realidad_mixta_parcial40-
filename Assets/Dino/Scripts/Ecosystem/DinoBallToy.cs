using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Pelota interactiva con físicas para el dinosaurio ("Pelota Dinosaurio").
    /// El jugador puede patearla, empujarla a toda velocidad, jugar con ella
    /// o usarla como proyectil demoledor contra torres de cajas y obstáculos.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class DinoBallToy : MonoBehaviour
    {
        [Header("Físicas")]
        [SerializeField] float ballMass = 120f;
        [SerializeField] float bounceForceMultiplier = 1.6f;
        [SerializeField] float maxSpeed = 45f;

        [Header("Empuje según la velocidad del dino")]
        [Tooltip("Por debajo de esta velocidad (km/h) el dino solo empuja la pelota, sin patada")]
        [SerializeField] float kmhSoloEmpuje = 30f;
        [Tooltip("A partir de esta velocidad (km/h) la patada es completa")]
        [SerializeField] float kmhPatadaCompleta = 55f;
        [Tooltip("Al empujar despacio, la pelota no supera la velocidad del dino por este factor")]
        [SerializeField] float factorEmpujeLento = 1.15f;

        Vector3 spawnPosition;
        Rigidbody rb;

        void Awake()
        {
            spawnPosition = transform.position;
            rb = GetComponent<Rigidbody>();
            rb.mass = ballMass;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Asignar PhysicMaterial con rebote si no tiene
            var col = GetComponent<SphereCollider>();
            if (col.sharedMaterial == null)
            {
                var pm = new PhysicsMaterial("BallBounceMat")
                {
                    bounciness = 0.45f,
                    bounceCombine = PhysicsMaterialCombine.Average,
                    dynamicFriction = 0.28f,
                    staticFriction = 0.35f,
                    frictionCombine = PhysicsMaterialCombine.Average
                };
                col.sharedMaterial = pm;
            }

            // Aspecto de foca (aletas, tentetieso, aplastamiento) sin tocar las escenas.
            if (!TryGetComponent<DinoPelotaFoca>(out _)) gameObject.AddComponent<DinoPelotaFoca>();
        }

        void OnCollisionEnter(Collision collision)
        {
            var vehicle = collision.collider.GetComponentInParent<DinoVehicleController>();
            if (vehicle != null)
            {
                LimitarEmpujeLento(collision);
                Vector3 dinoVel = vehicle.PlanarVelocity;
                float impactSpeed = dinoVel.magnitude;

                // 0 caminando (solo empuje), 1 a toda carrera (patada completa), progresivo entre medias.
                float kmh = impactSpeed * 3.6f;
                float patada = Mathf.InverseLerp(kmhSoloEmpuje, kmhPatadaCompleta, kmh);
                patada *= patada;

                if (patada > 0.001f)
                {
                    Vector3 kickDir = (transform.position - vehicle.transform.position).normalized;
                    // Solo se eleva cuando la patada es fuerte.
                    kickDir.y = Mathf.Clamp(kickDir.y + 0.3f * patada, 0.02f, 0.65f);

                    float kickPower = impactSpeed * bounceForceMultiplier * rb.mass * patada;
                    rb.AddForce(kickDir.normalized * kickPower, ForceMode.Impulse);

                    // Sacudida de cámara si el impacto fue en carrera
                    if (impactSpeed > 6.0f && DinoFollowCamera.Instance != null)
                    {
                        DinoFollowCamera.Instance.TriggerShake(0.18f, 0.08f);
                    }
                }
            }
        }

        void OnCollisionStay(Collision collision) => LimitarEmpujeLento(collision);

        /// <summary>
        /// El choque con un dino de 1500 kg lanza la pelota a casi el doble de su velocidad (caminando
        /// salía a ~77 km/h). Aquí se limita su velocidad horizontal según lo rápido que va el dino:
        /// caminando apenas lo supera; la patada completa (sin límite) solo llega a toda carrera.
        /// </summary>
        void LimitarEmpujeLento(Collision collision)
        {
            var vehicle = collision.collider.GetComponentInParent<DinoVehicleController>();
            if (vehicle == null) return;
            float dino = vehicle.PlanarVelocity.magnitude;
            float patada = Mathf.InverseLerp(kmhSoloEmpuje, kmhPatadaCompleta, dino * 3.6f);
            if (patada >= 0.999f) return;

            Vector3 v = rb.linearVelocity;
            Vector3 plano = new Vector3(v.x, 0f, v.z);
            float limite = dino * Mathf.Lerp(factorEmpujeLento, 2.6f, patada * patada) + 0.4f;
            if (plano.magnitude > limite)
            {
                plano = plano.normalized * limite;
                rb.linearVelocity = new Vector3(plano.x, Mathf.Min(v.y, 1f), plano.z);
            }
        }

        void Update()
        {
            // Limitar velocidad máxima para estabilidad
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }

            // Si se cae del mapa o rueda demasiado lejos (> 250m), reaparecer en origen
            if (transform.position.y < -15f || Vector3.Distance(transform.position, spawnPosition) > 280f)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                transform.position = spawnPosition + Vector3.up * 1.5f;
            }
        }
    }
}
