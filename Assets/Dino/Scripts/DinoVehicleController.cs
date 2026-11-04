using System.Collections;
using UnityEngine;

namespace DinoVehicle
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class DinoVehicleController : MonoBehaviour
    {
        [Header("Motor y Velocidad")]
        [Tooltip("Velocidad máxima normal de carrera en m/s")]
        public float maxForwardSpeed = 16f;
        [Tooltip("Velocidad de retroceso")]
        public float maxReverseSpeed = 5f;
        [Tooltip("Fuerza de aceleración")]
        public float acceleration = 25f;
        [Tooltip("Fuerza de frenado")]
        public float brakeStrength = 35f;

        [Header("Dirección y Giro")]
        [Tooltip("Velocidad angular de giro en grados por segundo")]
        public float turnSpeed = 75f;
        [Tooltip("Inclinación visual en grados al girar fuertemente")]
        public float maxBankAngle = 12f;
        [Tooltip("Velocidad de respuesta de inclinación")]
        public float bankSmoothSpeed = 5f;

        [Header("Física y Suelo")]
        public float gravityMultiplier = 2.5f;
        public float groundCheckDistance = 0.5f;
        public LayerMask groundLayer = ~0;
        public float jumpImpulse = 9f;

        [Header("Poderes y Rugido (Turbo)")]
        public float boostMultiplier = 1.6f;
        public float boostDuration = 3.5f;
        public float boostCooldown = 6f;
        public AudioSource audioSource;
        public AudioClip roarClip;

        [Header("Referencias Visuales")]
        [Tooltip("Transform del modelo hijo para aplicar inclinación (bank/lean) sin afectar el collider")]
        public Transform visualModel;

        // Estados y telemetría
        public float CurrentSpeedKmh => currentForwardSpeed * 3.6f;
        public float SpeedRatio => Mathf.Abs(currentForwardSpeed) / (maxForwardSpeed * boostMultiplier);
        public bool IsGrounded { get; private set; } = true;
        public bool IsBoosting { get; private set; } = false;
        public bool IsRoaring { get; private set; } = false;

        private Rigidbody rb;
        private CapsuleCollider col;
        private DinoInputManager input;
        private TreeImpactResponse treeImpact;

        private float currentForwardSpeed = 0f;
        private float currentBank = 0f;
        private float boostTimer = 0f;
        private float boostCooldownTimer = 0f;
        private Vector3 groundNormal = Vector3.up;
        private bool placedOnGround = false;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();
            treeImpact = GetComponent<TreeImpactResponse>();
            if (treeImpact == null)
            {
                treeImpact = gameObject.AddComponent<TreeImpactResponse>();
            }

            // Configurar física del Rigidbody para evitar vuelcos descontrolados
            rb.mass = 1200f; // 1.2 toneladas de dinosaurio
            rb.linearDamping = 0.5f;
            rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void Start()
        {
            input = DinoInputManager.Instance;
            if (visualModel == null && transform.childCount > 0)
            {
                visualModel = transform.GetChild(0);
            }
        }

        private void Update()
        {
            if (input == null) input = DinoInputManager.Instance;
            if (input == null) return;

            // Enfriamiento del Turbo
            if (boostCooldownTimer > 0f) boostCooldownTimer -= Time.deltaTime;

            if (IsBoosting)
            {
                boostTimer -= Time.deltaTime;
                if (boostTimer <= 0f)
                {
                    IsBoosting = false;
                }
            }

            // Activar Rugido / Turbo
            if (input.roarTriggered && boostCooldownTimer <= 0f && !IsBoosting)
            {
                ActivateRoarBoost();
            }

            // Salto / Pisotón
            if (input.jumpTriggered && IsGrounded)
            {
                ExecuteJump();
            }

            // Inclinación visual del modelo al tomar curvas
            UpdateVisualBanking();
        }

        private void FixedUpdate()
        {
            if (!placedOnGround)
            {
                PlaceOnGround();
                placedOnGround = true;
            }

            CheckGround();
            ApplyMovementPhysics();
            ApplyExtraGravity();
        }

        // El suelo del entorno es una malla que solo choca por arriba.
        // Si el pivote queda debajo, el dinosaurio atraviesa y cae al vacío.
        private void PlaceOnGround()
        {
            Vector3 origin = transform.position + Vector3.up * 80f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, groundLayer, QueryTriggerInteraction.Ignore);
            float feet = transform.position.y;
            bool hasBelow = false;
            bool hasAbove = false;
            float belowDist = float.MaxValue;
            float aboveDist = float.MaxValue;
            float belowY = feet;
            float aboveY = feet;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.attachedRigidbody == rb) continue;
                if (hit.normal.y < 0.25f) continue;

                float dy = feet - hit.point.y;
                if (dy >= -0.05f)
                {
                    if (dy < belowDist)
                    {
                        belowDist = dy;
                        belowY = hit.point.y;
                        hasBelow = true;
                    }
                }
                else
                {
                    float up = -dy;
                    if (up < aboveDist)
                    {
                        aboveDist = up;
                        aboveY = hit.point.y;
                        hasAbove = true;
                    }
                }
            }

            float targetY;
            if (hasBelow && belowDist <= 2.5f) targetY = belowY + 0.15f;
            else if (hasAbove) targetY = aboveY + 0.15f;
            else if (hasBelow) targetY = belowY + 0.15f;
            else return;

            Vector3 p = transform.position;
            p.y = targetY;
            rb.position = p;
            transform.position = p;
            rb.linearVelocity = Vector3.zero;
        }

        public void ResetToSpawn()
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            rb.position = spawnPosition;
            rb.rotation = spawnRotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            PlaceOnGround();
            placedOnGround = true;
        }

        private void CheckGround()
        {
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            RaycastHit hit;
            if (Physics.SphereCast(origin, 0.35f, Vector3.down, out hit, groundCheckDistance + 0.4f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                IsGrounded = true;
                groundNormal = hit.normal;
            }
            else
            {
                IsGrounded = false;
                groundNormal = Vector3.up;
            }
        }

        private void ApplyMovementPhysics()
        {
            if (input == null) return;

            // Calcular velocidad local hacia adelante
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            currentForwardSpeed = localVel.z;

            float throttle = input.throttle;
            float steer = input.steering;
            bool brake = input.isBraking;

            // Velocidad objetivo
            float topSpeed = (throttle >= 0f) ? maxForwardSpeed : maxReverseSpeed;
            if (IsBoosting && throttle > 0f) topSpeed *= boostMultiplier;

            float targetSpeed = throttle * topSpeed;

            // Aceleración / Frenado
            if (IsGrounded)
            {
                if (brake || (Mathf.Abs(throttle) < 0.05f && Mathf.Abs(currentForwardSpeed) > 0.1f))
                {
                    // Frenado por inercia o pedal
                    float brakeForce = brake ? brakeStrength : (acceleration * 0.5f);
                    currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeForce * Time.fixedDeltaTime);
                }
                else
                {
                    currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
                }

                // Aplicar velocidad orientada a la pendiente del terreno
                Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
                Vector3 newVelocity = moveDir * currentForwardSpeed;
                newVelocity.y = rb.linearVelocity.y; // Conservar gravedad vertical
                rb.linearVelocity = newVelocity;

                // Giro de dirección (efecto de volante vehicular)
                // Se permite girar si hay movimiento o si el usuario quiere pivotar suavemente en reposo
                float speedFactor = Mathf.Clamp01(Mathf.Abs(currentForwardSpeed) / 4f);
                float steerAmount = steer * turnSpeed * (0.35f + 0.65f * speedFactor) * Time.fixedDeltaTime;
                
                // Si va en reversa, invertir el giro como en un carro real
                if (currentForwardSpeed < -0.5f) steerAmount = -steerAmount;

                Quaternion turnRotation = Quaternion.Euler(0f, steerAmount, 0f);
                rb.MoveRotation(rb.rotation * turnRotation);
            }
        }

        private void ApplyExtraGravity()
        {
            if (!IsGrounded)
            {
                rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        private void ExecuteJump()
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = jumpImpulse;
            rb.linearVelocity = vel;
            IsGrounded = false;

            if (DinoFollowCamera.Instance != null)
            {
                DinoFollowCamera.Instance.TriggerShake(0.35f, 0.2f);
            }
        }

        public void ActivateRoarBoost()
        {
            IsBoosting = true;
            IsRoaring = true;
            boostTimer = boostDuration;
            boostCooldownTimer = boostCooldown;

            if (audioSource != null && roarClip != null)
            {
                audioSource.PlayOneShot(roarClip);
            }

            if (DinoFollowCamera.Instance != null)
            {
                DinoFollowCamera.Instance.TriggerShake(0.6f, 0.4f);
            }

            StartCoroutine(ResetRoarFlagAfter(1.2f));
        }

        private IEnumerator ResetRoarFlagAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            IsRoaring = false;
        }

        private void UpdateVisualBanking()
        {
            if (visualModel == null || input == null) return;

            // Inclinarse hacia el lado del giro
            float targetBank = -input.steering * maxBankAngle * Mathf.Clamp01(Mathf.Abs(currentForwardSpeed) / 5f);
            currentBank = Mathf.Lerp(currentBank, targetBank, Time.deltaTime * bankSmoothSpeed);

            visualModel.localRotation = Quaternion.Euler(0f, 0f, currentBank);
        }

        /// <summary>
        /// Sincroniza la velocidad de tracción después de un impulso externo,
        /// para que el siguiente FixedUpdate no borre el rebote.
        /// </summary>
        public void NotifyExternalVelocityChange()
        {
            if (rb == null) return;
            currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        }

        // Empujar o demoler obstáculos con masa al chocar a gran velocidad
        private void OnCollisionEnter(Collision collision)
        {
            if (treeImpact != null)
            {
                treeImpact.HandleCollision(collision);
            }

            Rigidbody otherRb = collision.rigidbody;
            if (otherRb != null && !otherRb.isKinematic)
            {
                float smashPower = Mathf.Abs(currentForwardSpeed);
                if (smashPower > 4f)
                {
                    Vector3 forceDir = (collision.transform.position - transform.position).normalized + Vector3.up * 0.3f;
                    otherRb.AddForce(forceDir * smashPower * 250f, ForceMode.Impulse);

                    if (DinoFollowCamera.Instance != null && smashPower > 8f)
                    {
                        DinoFollowCamera.Instance.TriggerShake(0.25f, 0.15f);
                    }
                }
            }
        }
    }
}
