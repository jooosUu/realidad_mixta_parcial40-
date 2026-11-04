using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Choque del dinosaurio contra un tronco fijo (masa del árbol M → ∞).
    /// El motor de física ya anula la componente normal de entrada.
    /// Aquí se completa la restitución para que el cambio total de velocidad sea
    /// Δv_n = -(1 + e) · v_n, con impulso J = m · Δv y fuerza media F = |J| / Δt.
    /// </summary>
    public class TreeImpactResponse : MonoBehaviour
    {
        [Header("Tronco")]
        [Tooltip("Texto que identifica el collider de un árbol en la jerarquía")]
        [SerializeField] string treeNameToken = "Tree";

        [Header("Impacto")]
        [Tooltip("Coeficiente de restitución e. 0 = choque plástico, 1 = elástico")]
        [SerializeField] [Range(0f, 1f)] float restitution = 0.28f;
        [Tooltip("Duración del contacto Δt, en segundos, para F = Δp / Δt")]
        [SerializeField] float impactDuration = 0.08f;
        [Tooltip("Velocidad mínima de aproximación, en m/s, para aplicar el impulso")]
        [SerializeField] float minimumApproachSpeed = 1.4f;
        [Tooltip("Fuerza media a partir de la cual tiembla la cámara")]
        [SerializeField] float shakeForce = 12000f;

        Rigidbody body;
        DinoVehicleController vehicle;

        public float LastImpulse { get; private set; }
        public float LastImpactForce { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<DinoVehicleController>();
        }

        public void HandleCollision(Collision collision)
        {
            if (body == null || !IsTree(collision.collider) || collision.contactCount == 0)
            {
                return;
            }

            Vector3 normal = collision.GetContact(0).normal;
            if (normal.sqrMagnitude < 0.0001f)
            {
                return;
            }
            normal.Normalize();

            // v_n < 0 cuando el dinosaurio se acerca al tronco.
            float approachSpeed = Vector3.Dot(collision.relativeVelocity, normal);
            if (approachSpeed > -minimumApproachSpeed)
            {
                return;
            }

            float mass = Mathf.Max(body.mass, 0.01f);
            float bounce = Mathf.Clamp01(restitution);

            // Velocidad normal que debe quedar tras el choque: v' = -e · v_n
            float desiredNormalSpeed = -bounce * approachSpeed;
            float currentNormalSpeed = Vector3.Dot(body.linearVelocity, normal);
            float deltaSpeed = desiredNormalSpeed - currentNormalSpeed;
            if (deltaSpeed <= 0f)
            {
                return;
            }

            // J = m · Δv. El Δv total respecto a la entrada sigue siendo -(1+e)·v_n
            // porque PhysX ya canceló la componente de aproximación.
            float impulseMagnitude = mass * deltaSpeed;
            Vector3 impulse = normal * impulseMagnitude;
            body.linearVelocity += impulse / mass;

            if (vehicle != null)
            {
                vehicle.NotifyExternalVelocityChange();
            }

            LastImpulse = impulseMagnitude;
            LastImpactForce = impulseMagnitude / Mathf.Max(0.01f, impactDuration);

            if (DinoFollowCamera.Instance != null && LastImpactForce >= shakeForce)
            {
                DinoFollowCamera.Instance.TriggerShake(0.28f, 0.12f);
            }
        }

        bool IsTree(Collider collider)
        {
            Transform current = collider.transform;
            while (current != null)
            {
                if (current.name.IndexOf(treeNameToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }
    }
}
