using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// IA para los Velociraptores en manada del ecosistema.
    /// Acechan en las inmediaciones, se mueven con agilidad felina y se dispersan
    /// velozmente si el gran Carnotauro embiste a toda carrera o ruge.
    /// </summary>
    public class DinoAIRaptor : MonoBehaviour
    {
        public enum RaptorState { Stalking, Circling, Scattering }

        [Header("Comportamiento Raptor")]
        [SerializeField] RaptorState currentState = RaptorState.Stalking;
        [SerializeField] float stalkSpeed = 3.2f;
        [SerializeField] float sprintSpeed = 12.0f;
        [SerializeField] float alertDistance = 18f;

        Vector3 originPos;
        Vector3 targetPos;
        float stateTimer = 0f;

        Animator animator;
        DinoVehicleController player;

        void Start()
        {
            originPos = transform.position;
            animator = GetComponentInChildren<Animator>();
            player = FindAnyObjectByType<DinoVehicleController>();

            ElegirPuntoAcecho();
        }

        void Update()
        {
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);

                // Si el Carnotauro ruge o viene embalado hacia ellos
                if (player.IsRoaring || (player.CurrentSpeedKmh > 22f && dist < alertDistance))
                {
                    currentState = RaptorState.Scattering;
                }
            }

            switch (currentState)
            {
                case RaptorState.Stalking:
                    stateTimer += Time.deltaTime;
                    MoverHacia(targetPos, stalkSpeed);
                    if (Vector3.Distance(transform.position, targetPos) < 1.2f || stateTimer > 8f)
                    {
                        ElegirPuntoAcecho();
                        stateTimer = 0f;
                    }
                    break;

                case RaptorState.Scattering:
                    if (player != null)
                    {
                        Vector3 huida = (transform.position - player.transform.position).normalized;
                        huida.y = 0;
                        MoverHacia(transform.position + huida * 20f, sprintSpeed);

                        if (Vector3.Distance(transform.position, player.transform.position) > alertDistance * 1.5f)
                        {
                            currentState = RaptorState.Stalking;
                            originPos = transform.position;
                            ElegirPuntoAcecho();
                        }
                    }
                    else
                    {
                        currentState = RaptorState.Stalking;
                    }
                    break;
            }

            ActualizarAnimacion();
        }

        void ElegirPuntoAcecho()
        {
            Vector2 circ = Random.insideUnitCircle * 16f;
            targetPos = originPos + new Vector3(circ.x, 0, circ.y);
            targetPos.y = transform.position.y;
        }

        void MoverHacia(Vector3 target, float speed)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.05f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 6f);
                transform.position += transform.forward * speed * Time.deltaTime;
            }
        }

        string estadoAnim;

        void ActualizarAnimacion()
        {
            // Sin controller, HasState/CrossFade registran un aviso en cada frame.
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            string deseado = null;
            if (currentState == RaptorState.Scattering)
            {
                if (TieneEstado("Run")) deseado = "Run";
                else if (TieneEstado("RaptorArmature|Raptor_Run1_Anim")) deseado = "RaptorArmature|Raptor_Run1_Anim";
            }
            else
            {
                if (TieneEstado("Walk")) deseado = "Walk";
                else if (TieneEstado("RaptorArmature|Raptor_Walk_Anim")) deseado = "RaptorArmature|Raptor_Walk_Anim";
                else if (TieneEstado("Idle")) deseado = "Idle";
            }

            // Solo al cambiar: repetir CrossFade cada frame congela la animación.
            if (deseado == null || deseado == estadoAnim) return;
            estadoAnim = deseado;
            animator.CrossFade(deseado, 0.15f);
        }

        bool TieneEstado(string n)
        {
            return animator.HasState(0, Animator.StringToHash(n));
        }
    }
}
