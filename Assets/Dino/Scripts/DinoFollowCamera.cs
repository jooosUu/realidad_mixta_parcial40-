using UnityEngine;

namespace DinoVehicle
{
    public class DinoFollowCamera : MonoBehaviour
    {
        public static DinoFollowCamera Instance { get; private set; }

        [Header("Objetivo")]
        public Transform target;
        public DinoVehicleController vehicle;

        [Header("Posición de Seguimiento")]
        public float distance = 5.5f;
        public float height = 2.4f;
        public float lookAtHeight = 1.3f;
        public float followSharpness = 6f;
        public float rotationSharpness = 8f;

        [Header("Cámara Dinámica de Velocidad")]
        public float baseFOV = 60f;
        public float maxSpeedFOV = 78f;
        public float boostFOV = 88f;
        public float fovSmoothSpeed = 4f;

        private Camera cam;
        private Vector3 currentVelocity;
        private float shakeIntensity = 0f;
        private float shakeDuration = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null && vehicle != null)
            {
                target = vehicle.transform;
            }
            else if (target != null && vehicle == null)
            {
                vehicle = target.GetComponent<DinoVehicleController>();
            }

            if (cam != null) cam.fieldOfView = baseFOV;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. Calcular posición ideal detrás del objetivo
            Vector3 forward = target.forward;
            Vector3 targetPos = target.position - forward * distance + Vector3.up * height;

            // 2. Interpolar suavemente
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSharpness);

            // 3. Mirar hacia el objetivo + altura de vista
            Vector3 lookTarget = target.position + Vector3.up * lookAtHeight + target.forward * 1.5f;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSharpness);

            // 4. Efecto Shake (sacudida)
            if (shakeDuration > 0f)
            {
                shakeDuration -= Time.deltaTime;
                Vector3 shakeOffset = Random.insideUnitSphere * shakeIntensity;
                transform.position += shakeOffset;
            }

            // 5. Ajuste dinámico de campo de visión (FOV) con la velocidad
            if (cam != null && vehicle != null)
            {
                float targetFOV = baseFOV;
                if (vehicle.IsBoosting)
                {
                    targetFOV = boostFOV;
                }
                else
                {
                    targetFOV = Mathf.Lerp(baseFOV, maxSpeedFOV, vehicle.SpeedRatio);
                }

                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovSmoothSpeed);
            }
        }

        public void TriggerShake(float intensity, float duration)
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
        }
    }
}
