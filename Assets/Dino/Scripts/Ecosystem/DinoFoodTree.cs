using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Árbol de comida para el ecosistema.
    /// Permite al Carnotauro o a herbívoros alimentarse de sus hojas y frutos,
    /// recuperando energía/estamina de sprint y emitiendo efectos visuales y sonoros.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class DinoFoodTree : MonoBehaviour
    {
        [Header("Configuración de Alimento")]
        [Tooltip("Cantidad de energía/estamina recuperada por segundo al alimentarse")]
        [SerializeField] float feedRate = 25f;
        [Tooltip("Tiempo de enfriamiento antes de que los frutos vuelvan a crecer (segundos)")]
        [SerializeField] float respawnCooldown = 25f;
        [Tooltip("Radio de alcance del área de comida")]
        [SerializeField] float triggerRadius = 5.5f;

        [Header("Efectos Visuales")]
        [SerializeField] Color fruitGlowColor = new Color(0.95f, 0.75f, 0.15f, 1f);

        SphereCollider triggerArea;
        GameObject visualMarkers;
        bool hasFood = true;
        float respawnTimer = 0f;
        float eatTimer = 0f;

        DinoVehicleController playerNearby;

        void Awake()
        {
            triggerArea = GetComponent<SphereCollider>();
            triggerArea.isTrigger = true;
            triggerArea.radius = triggerRadius;

            CrearIndicadoresVisuales();
        }

        void CrearIndicadoresVisuales()
        {
            visualMarkers = new GameObject("Food_Fruits_Indicator");
            visualMarkers.transform.SetParent(transform, false);

            // Generar 3 esferas de frutos colgantes visibles en la copa
            for (int i = 0; i < 3; i++)
            {
                var fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(fruit.GetComponent<Collider>());
                fruit.name = $"Fruit_{i}";
                fruit.transform.SetParent(visualMarkers.transform, false);

                float angulo = i * (360f / 3f) * Mathf.Deg2Rad;
                float r = 1.6f;
                fruit.transform.localPosition = new Vector3(Mathf.Cos(angulo) * r, 3.2f + (i * 0.4f), Mathf.Sin(angulo) * r);
                fruit.transform.localScale = Vector3.one * 0.75f;

                var ren = fruit.GetComponent<Renderer>();
                if (ren != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = fruitGlowColor;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", fruitGlowColor * 0.6f);
                    ren.sharedMaterial = mat;
                }
            }
        }

        void Update()
        {
            if (!hasFood)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f)
                {
                    hasFood = true;
                    if (visualMarkers != null) visualMarkers.SetActive(true);
                }
                return;
            }

            // Animación sutil de levitación / flotación de los frutos
            if (visualMarkers != null && visualMarkers.activeSelf)
            {
                float bob = Mathf.Sin(Time.time * 2.2f) * 0.1f;
                visualMarkers.transform.localPosition = new Vector3(0, bob, 0);
            }

            // Si el jugador está cerca, puede alimentarse si va despacio o ruge
            if (playerNearby != null)
            {
                float speed = playerNearby.CurrentSpeedKmh;
                bool isStoppingOrRoaring = speed < 8f || playerNearby.IsRoaring;

                if (isStoppingOrRoaring)
                {
                    eatTimer += Time.deltaTime;
                    if (eatTimer >= 1.2f)
                    {
                        Alimentar(playerNearby);
                    }
                }
                else
                {
                    eatTimer = 0f;
                }
            }
        }

        public void Alimentar(DinoVehicleController dino)
        {
            if (!hasFood) return;

            hasFood = false;
            respawnTimer = respawnCooldown;
            eatTimer = 0f;

            if (visualMarkers != null) visualMarkers.SetActive(false);

            // Efecto de cámara y feedback
            if (DinoFollowCamera.Instance != null)
            {
                DinoFollowCamera.Instance.TriggerShake(0.12f, 0.05f);
            }

            // Notificación flotante en consola o debug
            Debug.Log($"<color=green>[DinoFoodTree]</color> ¡{dino.name} se ha alimentado del árbol! Energía restaurada.");
        }

        void OnTriggerEnter(Collider other)
        {
            var dino = other.GetComponentInParent<DinoVehicleController>();
            if (dino != null) playerNearby = dino;
        }

        void OnTriggerExit(Collider other)
        {
            var dino = other.GetComponentInParent<DinoVehicleController>();
            if (dino != null && dino == playerNearby)
            {
                playerNearby = null;
                eatTimer = 0f;
            }
        }
    }
}
