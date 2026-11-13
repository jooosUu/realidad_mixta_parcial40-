using UnityEngine;

namespace DinoVehicle
{
    [RequireComponent(typeof(DinoVehicleController))]
    public class DinoAnimationDriver : MonoBehaviour
    {
        [Header("Referencias")]
        public Animator animator;
        private DinoVehicleController controller;

        [Header("Ajuste de Pasos")]
        [Tooltip("Multiplicador de velocidad de la animacion al caminar/correr")]
        public float animSpeedMultiplier = 0.22f;
        [Tooltip("Velocidad minima de animacion al moverse")]
        public float minAnimSpeed = 0.6f;
        [Tooltip("Velocidad maxima de animacion en sprint")]
        public float maxAnimSpeed = 2.4f;

        private int hashSpeed;
        private int hashIsGrounded;
        private int hashRoar;
        private int hashAttack;

        private bool hasSpeedParam = false;
        private bool hasRoarTrigger = false;
        private bool hasAttackTrigger = false;
        private bool lastRoaringState = false;
        private string estadoSolicitado;

        private void Awake()
        {
            controller = GetComponent<DinoVehicleController>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            hashSpeed = Animator.StringToHash("Speed");
            hashIsGrounded = Animator.StringToHash("IsGrounded");
            hashRoar = Animator.StringToHash("Roar");
            hashAttack = Animator.StringToHash("Attack");
        }

        private void Start()
        {
            RefrescarParametros();
        }

        public void RefrescarParametros()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            hasSpeedParam = false;
            hasRoarTrigger = false;
            hasAttackTrigger = false;
            estadoSolicitado = null;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (var param in animator.parameters)
                {
                    if (param.name == "Speed") hasSpeedParam = true;
                    if (param.name == "Roar") hasRoarTrigger = true;
                    if (param.name.ToLower().Contains("attack")) hasAttackTrigger = true;
                }
            }
        }

        private void Update()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator == null) return;
                RefrescarParametros();
            }

            float speedKmh = controller.CurrentSpeedKmh;
            float absSpeed = Mathf.Abs(speedKmh);

            // Si el Animator tiene parametro "Speed"
            if (hasSpeedParam)
            {
                animator.SetFloat(hashSpeed, absSpeed);
                animator.SetBool(hashIsGrounded, controller.IsGrounded);

                if (controller.IsRoaring && !lastRoaringState)
                {
                    if (hasRoarTrigger) animator.SetTrigger(hashRoar);
                    else if (hasAttackTrigger) animator.SetTrigger(hashAttack);
                }
            }
            else
            {
                // Conduccion directa de estados si no tiene blend tree
                if (controller.IsRoaring && !lastRoaringState)
                {
                    if (hasRoarTrigger) animator.SetTrigger(hashRoar);
                    else if (hasAttackTrigger) animator.SetTrigger(hashAttack);
                    else CrossFadeSmart("Roar", 0.1f);
                }
                else if (!controller.IsRoaring)
                {
                    if (absSpeed > 0.8f)
                    {
                        if (absSpeed > 18f)
                        {
                            CrossFadeSmart("Run", 0.18f);
                            animator.speed = Mathf.Clamp((absSpeed - 18f) * 0.045f + 1.0f, 0.9f, 2.2f);
                        }
                        else
                        {
                            CrossFadeSmart("Walk", 0.15f);
                            animator.speed = Mathf.Clamp(absSpeed * 0.085f + 0.35f, 0.65f, 1.4f);
                        }
                    }
                    else
                    {
                        CrossFadeSmart("Idle", 0.25f);
                        animator.speed = 1.0f;
                    }
                }
            }

            lastRoaringState = controller.IsRoaring;
        }

        void CrossFadeSmart(string stateName, float transitionDuration)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            // Repetir CrossFade al mismo estado cada frame reinicia la transición
            // y deja la animación congelada en su primer fotograma.
            if (stateName == estadoSolicitado) return;
            estadoSolicitado = stateName;

            if (animator.HasState(0, Animator.StringToHash(stateName)))
            {
                animator.CrossFade(stateName, transitionDuration);
                return;
            }

            // Variantes por modelo
            if (stateName == "Idle")
            {
                if (animator.HasState(0, Animator.StringToHash("IdleA"))) animator.CrossFade("IdleA", transitionDuration);
                else if (animator.HasState(0, Animator.StringToHash("RaptorArmature|Raptor_Idle1_Anim"))) animator.CrossFade("RaptorArmature|Raptor_Idle1_Anim", transitionDuration);
                else if (animator.HasState(0, Animator.StringToHash("Armature|Pachycephalasaurus_IdleA"))) animator.CrossFade("Armature|Pachycephalasaurus_IdleA", transitionDuration);
            }
            else if (stateName == "Walk")
            {
                if (animator.HasState(0, Animator.StringToHash("RaptorArmature|Raptor_Walk_Anim"))) animator.CrossFade("RaptorArmature|Raptor_Walk_Anim", transitionDuration);
                else if (animator.HasState(0, Animator.StringToHash("Armature|Pachycephalasaurus_Walk"))) animator.CrossFade("Armature|Pachycephalasaurus_Walk", transitionDuration);
            }
            else if (stateName == "Run")
            {
                if (animator.HasState(0, Animator.StringToHash("RaptorArmature|Raptor_Run1_Anim"))) animator.CrossFade("RaptorArmature|Raptor_Run1_Anim", transitionDuration);
                else if (animator.HasState(0, Animator.StringToHash("Armature|Pachycephalasaurus_Run"))) animator.CrossFade("Armature|Pachycephalasaurus_Run", transitionDuration);
                else if (animator.HasState(0, Animator.StringToHash("Armature|Pachycephalasaurus_HeadbuttCharged"))) animator.CrossFade("Armature|Pachycephalasaurus_HeadbuttCharged", transitionDuration);
            }
            else if (stateName == "Roar")
            {
                if (animator.HasState(0, Animator.StringToHash("RaptorArmature|Raptor_Roar1_Anim"))) animator.CrossFade("RaptorArmature|Raptor_Roar1_Anim", transitionDuration);
                else if (animator.HasState(0, Animator.StringToHash("Armature|Pachycephalasaurus_HeadbuttCharged"))) animator.CrossFade("Armature|Pachycephalasaurus_HeadbuttCharged", transitionDuration);
            }
        }
    }
}
