using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Maneja la interacción entre el dinosaurio y las bandadas de pájaros (living birds).
    /// Cuando el Carnotauro se acerca a velocidad o ruge, las aves huyen en estampida
    /// y realizan vuelos rasantes con sentido físico y aleteo.
    /// </summary>
    public class DinoBirdProximity : MonoBehaviour
    {
        [Header("Distancias de Reacción")]
        [SerializeField] float fleeRadius = 18f;
        [SerializeField] float roarRadius = 45f;
        [SerializeField] float checkInterval = 0.35f;

        DinoVehicleController controller;
        float nextCheckTime = 0f;

        void Awake()
        {
            controller = GetComponent<DinoVehicleController>();
        }

        void Update()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + checkInterval;

            if (controller == null) return;

            bool isFast = controller.CurrentSpeedKmh > 14f;
            bool isRoaring = controller.IsRoaring;

            if (!isFast && !isRoaring) return;

            float currentRadius = isRoaring ? roarRadius : fleeRadius;

            // Buscar pájaros en el radio
            Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.CompareTag("lb_bird") || hit.name.ToLowerInvariant().Contains("bird"))
                {
                    // Forzar que el pájaro huya
                    hit.SendMessage("Flee", SendMessageOptions.DontRequireReceiver);
                    hit.SendMessage("FlyAway", SendMessageOptions.DontRequireReceiver);
                    hit.SendMessage("CrowIsClose", SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, fleeRadius);
        }
    }
}
