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
                    bounciness = 0.65f,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    dynamicFriction = 0.28f,
                    staticFriction = 0.35f,
                    frictionCombine = PhysicsMaterialCombine.Average
                };
                col.sharedMaterial = pm;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            var vehicle = collision.collider.GetComponentInParent<DinoVehicleController>();
            if (vehicle != null)
            {
                Vector3 dinoVel = vehicle.PlanarVelocity;
                float impactSpeed = dinoVel.magnitude;

                if (impactSpeed > 1.5f)
                {
                    Vector3 kickDir = (transform.position - vehicle.transform.position).normalized;
                    kickDir.y = Mathf.Clamp(kickDir.y + 0.3f, 0.15f, 0.65f); // Eleva levemente la pelota al patearla

                    float kickPower = impactSpeed * bounceForceMultiplier * rb.mass;
                    rb.AddForce(kickDir * kickPower, ForceMode.Impulse);

                    // Sacudida de cámara si el impacto fue en carrera
                    if (impactSpeed > 6.0f && DinoFollowCamera.Instance != null)
                    {
                        DinoFollowCamera.Instance.TriggerShake(0.18f, 0.08f);
                    }
                }
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
