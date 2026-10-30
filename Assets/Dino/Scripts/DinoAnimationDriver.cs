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
        private int hashWalk;
        private int hashIdle;

        private bool hasSpeedParam = false;
        private bool hasRoarTrigger = false;
        private bool hasAttackTrigger = false;
        private bool lastRoaringState = false;

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
            hashWalk = Animator.StringToHash("Walk");
            hashIdle = Animator.StringToHash("Idle");
        }

        private void Start()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

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
                    else animator.CrossFade("Roar", 0.1f);
                }
                else if (!controller.IsRoaring)
                {
                    if (absSpeed > 1.2f)
                    {
                        if (absSpeed > 22f)
                        {
                            animator.CrossFade("Run", 0.15f);
                            animator.speed = Mathf.Clamp(absSpeed * animSpeedMultiplier * 0.7f, 0.8f, 2.0f);
                        }
                        else
                        {
                            animator.CrossFade("Walk", 0.15f);
                            animator.speed = Mathf.Clamp(absSpeed * animSpeedMultiplier, minAnimSpeed, maxAnimSpeed);
                        }
                    }
                    else
                    {
                        animator.CrossFade("Idle", 0.25f);
                        animator.speed = 1.0f;
                    }
                }
            }

            lastRoaringState = controller.IsRoaring;
        }
    }
}
