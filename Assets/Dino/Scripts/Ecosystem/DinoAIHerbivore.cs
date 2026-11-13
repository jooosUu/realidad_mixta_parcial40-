using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// IA para dinosaurios herbívoros del ecosistema (Estegosaurios, Paquicefalosaurios, Anquilosaurios).
    /// Pastan pacíficamente en su territorio, caminan lentamente entre áreas de hierba
    /// y huyen despavoridos si el Carnotauro se acerca a gran velocidad o ruge.
    /// </summary>
    public class DinoAIHerbivore : MonoBehaviour
    {
        public enum AIState { Grazing, Walking, Fleeing }

        [Header("Comportamiento")]
        [SerializeField] AIState currentState = AIState.Grazing;
        [SerializeField] float walkSpeed = 2.4f;
        [SerializeField] float fleeSpeed = 8.5f;
        [SerializeField] float territoryRadius = 25f;
        [SerializeField] float fearDistance = 14f;
        [SerializeField] float roarFearDistance = 35f;

        Vector3 spawnPosition;
        Vector3 targetPoint;
        float stateTimer = 0f;
        float changeStateCooldown = 6f;

        Animator animator;
        Transform playerDino;
        DinoVehicleController playerController;

        void Start()
        {
            spawnPosition = transform.position;
            animator = GetComponentInChildren<Animator>();

            var player = FindAnyObjectByType<DinoVehicleController>();
            if (player != null)
            {
                playerController = player;
                playerDino = player.transform;
            }

            ElegirNuevoPuntoPasto();
        }

        void Update()
        {
            CheckPlayerProximity();

            switch (currentState)
            {
                case AIState.Grazing:
                    stateTimer += Time.deltaTime;
                    if (stateTimer >= changeStateCooldown)
                    {
                        ElegirNuevoPuntoPasto();
                        currentState = AIState.Walking;
                        stateTimer = 0f;
                    }
                    break;

                case AIState.Walking:
                    MoverHacia(targetPoint, walkSpeed);
                    if (Vector3.Distance(transform.position, targetPoint) < 1.5f)
                    {
                        currentState = AIState.Grazing;
                        stateTimer = 0f;
                        changeStateCooldown = Random.Range(4f, 9f);
                    }
                    break;

                case AIState.Fleeing:
                    if (playerDino != null)
                    {
                        Vector3 fleeDir = (transform.position - playerDino.position).normalized;
                        fleeDir.y = 0f;
                        Vector3 fleeTarget = transform.position + fleeDir * 15f;
                        MoverHacia(fleeTarget, fleeSpeed);

                        // Si ya se alejó lo suficiente, calmarse
                        if (Vector3.Distance(transform.position, playerDino.position) > fearDistance * 1.8f)
                        {
                            currentState = AIState.Grazing;
                            stateTimer = 0f;
                        }
                    }
                    else
                    {
                        currentState = AIState.Grazing;
                    }
                    break;
            }

            ActualizarAnimacion();
        }

        void CheckPlayerProximity()
        {
            if (playerDino == null || playerController == null) return;

            float dist = Vector3.Distance(transform.position, playerDino.position);

            // Reacción a rugido a mayor distancia
            if (playerController.IsRoaring && dist < roarFearDistance)
            {
                currentState = AIState.Fleeing;
                return;
            }

            // Reacción a velocidad alta si se acerca
            bool playerIsFast = playerController.CurrentSpeedKmh > 18f;
            if (dist < (playerIsFast ? fearDistance * 1.3f : fearDistance * 0.7f))
            {
                currentState = AIState.Fleeing;
            }
        }

        void ElegirNuevoPuntoPasto()
        {
            Vector2 randomCircle = Random.insideUnitCircle * territoryRadius;
            targetPoint = spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
            targetPoint.y = transform.position.y;
        }

        void MoverHacia(Vector3 target, float speed)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.05f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 3.5f);
                transform.position += transform.forward * speed * Time.deltaTime;
            }
        }

        bool animPreparada;
        bool tieneSpeed;
        string estadoAnim;

        void ActualizarAnimacion()
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            if (!animPreparada)
            {
                // Se consulta una sola vez: animator.parameters crea un array nuevo en cada llamada.
                animPreparada = true;
                foreach (var p in animator.parameters)
                {
                    if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) tieneSpeed = true;
                }
            }

            if (tieneSpeed)
            {
                float speedValue = 0f;
                if (currentState == AIState.Walking) speedValue = 0.4f;
                else if (currentState == AIState.Fleeing) speedValue = 1.0f;
                animator.SetFloat("Speed", speedValue);
                return;
            }

            // Si no tiene parámetro Speed, alternar estados
            string deseado = null;
            if (currentState == AIState.Fleeing)
            {
                if (TieneEstado("Run")) deseado = "Run";
                else if (TieneEstado("Walk")) deseado = "Walk";
            }
            else if (currentState == AIState.Walking)
            {
                if (TieneEstado("Walk")) deseado = "Walk";
            }
            else
            {
                if (TieneEstado("Idle")) deseado = "Idle";
            }

            // Solo al cambiar: repetir CrossFade cada frame congela la animación.
            if (deseado == null || deseado == estadoAnim) return;
            estadoAnim = deseado;
            animator.CrossFade(deseado, 0.2f);
        }

        bool TieneEstado(string stateName)
        {
            return animator.HasState(0, Animator.StringToHash(stateName));
        }
    }
}
