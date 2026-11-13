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
        public float distance = 9f;
        public float height = 3.4f;
        public float lookAtHeight = 1.8f;
        [Tooltip("Punto del cuerpo que queda en el centro de la pantalla")]
        public Vector3 focusLocalOffset = new Vector3(0f, 1.8f, 1.1f);
        public float followSharpness = 10f;
        public float rotationSharpness = 14f;

        [Header("Cámara Dinámica de Velocidad")]
        public float baseFOV = 60f;
        public float maxSpeedFOV = 78f;
        public float boostFOV = 88f;
        public float fovSmoothSpeed = 4f;

        private Camera cam;
        private Vector3 currentVelocity;
        private float shakeIntensity = 0f;
        private float shakeDuration = 0f;
        private bool framed;

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

            Vector3 forward = target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 focus = target.TransformPoint(focusLocalOffset);
            Vector3 targetPos = focus - forward * distance + Vector3.up * height;
            if (!framed)
            {
                transform.position = targetPos;
                framed = true;
            }
            else
            {
                float posT = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPos, posT);
            }

            Vector3 lookDir = focus - transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }

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
