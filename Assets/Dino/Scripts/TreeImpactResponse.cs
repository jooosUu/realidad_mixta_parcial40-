using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Choque plástico contra un árbol enraizado. No hay restitución.
    /// Acción del dinosaurio: entrega el impulso J = m · Δv.
    /// Reacción del árbol: devuelve −J, así que la velocidad normal del dinosaurio
    /// queda en cero (Δv_n = −v_n) y el tronco se flexiona con el mismo impulso.
    /// </summary>
    public class TreeImpactResponse : MonoBehaviour
    {
        [Header("Tronco")]
        [Tooltip("Texto que identifica el collider de un árbol en la jerarquía")]
        [SerializeField] string treeNameToken = "Tree";

        [Header("Acción y reacción")]
        [Tooltip("Duración del contacto Δt, en segundos, para F = |J| / Δt")]
        [SerializeField] float impactDuration = 0.08f;
        [Tooltip("Segundos en los que la tracción no vuelve a empujar el tronco")]
        [SerializeField] float reactionLock = 0.28f;
        [Tooltip("Fuerza media a partir de la cual tiembla la cámara")]
        [SerializeField] float shakeForce = 4000f;
        [Tooltip("Multiplicador del grosor del tronco sólido (0.72 = grosor estimado normal)")]
        [SerializeField] [Range(0.2f, 1f)] float solidWidth = 0.72f;

        // Nombres que identifican un árbol además de treeNameToken
        // (las coníferas de la escena se llaman "PBR_Conifera_Este_N").
        static readonly string[] TreeTokens = { "Tree", "Conifer", "Pine", "Arbol" };

        Rigidbody body;
        DinoVehicleController vehicle;
        float nextLogTime;

        public float LastImpulse { get; private set; }
        public float LastImpactForce { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<DinoVehicleController>();
        }

        void Start()
        {
            SolidifyTrees();
        }

        public void HandleCollision(Collision collision, bool firstContact)
        {
            if (body == null || collision.collider == null || !IsTree(collision.collider))
            {
                return;
            }

            Vector3 normal = ContactNormal(collision);
            if (normal.sqrMagnitude < 0.0001f)
            {
                return;
            }
            normal.Normalize();

            Vector3 velocity = vehicle != null
                ? vehicle.PlanarVelocity + Vector3.up * body.linearVelocity.y
                : body.linearVelocity;

            // v_n < 0 cuando el dinosaurio empuja hacia el tronco.
            float normalSpeed = Vector3.Dot(velocity, normal);
            if (normalSpeed >= -0.05f)
            {
                return;
            }

            float mass = Mathf.Max(body.mass, 0.01f);

            // Reacción sobre el dinosaurio: se anula la componente que entra, no se invierte.
            Vector3 reactionImpulse = -mass * normalSpeed * normal;

            float speedKmh = vehicle != null ? vehicle.CurrentSpeedKmh : (velocity.magnitude * 3.6f);
            bool isHighSpeed = speedKmh >= 32f || (vehicle != null && vehicle.IsBoosting);
            bool isMediumSpeed = speedKmh >= 15f;

            TreeReaction tree = collision.collider.GetComponentInParent<TreeReaction>();

            if (firstContact && Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + 0.3f;
                string efecto = tree == null ? "SIN TreeReaction (no puede caer)"
                    : tree.HasFallen ? "ya estaba caído"
                    : isHighSpeed ? "cae de inmediato"
                    : isMediumSpeed ? $"golpe {tree.HitCount + 1} de {tree.HitsToFall}"
                    : "solo se mece (menos de 15 km/h)";
                Debug.Log($"<color=orange>[TreeImpact]</color> Choque con '{collision.collider.transform.root.name}/{collision.collider.name}' a {speedKmh:0} km/h → {efecto}");
            }

            Vector3 travel = Vector3.ProjectOnPlane(velocity, Vector3.up);
            Vector3 push = travel.sqrMagnitude > 0.25f
                ? travel
                : Vector3.ProjectOnPlane(-normal, Vector3.up);

            // 1. Velocidad muy baja (< 15 km/h): solo mece suavemente sin derribar
            if (!isMediumSpeed && !isHighSpeed)
            {
                Vector3 slowVelocity = velocity - normalSpeed * normal;
                if (vehicle != null) vehicle.ApplyImpactVelocity(slowVelocity, 0.08f);
                else body.linearVelocity = slowVelocity;

                if (tree != null && push.sqrMagnitude > 0.0001f)
                {
                    push.Normalize();
                    tree.ReceivePush(push, -normalSpeed);
                }
                return;
            }

            // 2. Velocidad muy alta (>= 32 km/h o turbo):
            // Cae de UNA y el dinosaurio conserva el 72% de inercia (arrolla el árbol)
            if (isHighSpeed)
            {
                if (tree != null && push.sqrMagnitude > 0.0001f)
                {
                    push.Normalize();
                    tree.ReceiveImpulse(push, reactionImpulse.magnitude, true);
                }

                Vector3 plowVelocity = velocity * 0.72f;
                if (vehicle != null)
                {
                    vehicle.ApplyImpactVelocity(plowVelocity, 0.05f);
                }
                else
                {
                    body.linearVelocity = plowVelocity;
                }

                LastImpulse = reactionImpulse.magnitude;
                LastImpactForce = LastImpulse / Mathf.Max(0.01f, impactDuration);

                if (DinoFollowCamera.Instance != null)
                {
                    DinoFollowCamera.Instance.TriggerShake(0.35f, 0.12f);
                }
                return;
            }

            // 3. Velocidad media (15 a 32 km/h):
            // Se flexiona y requiere 2 o 3 impactos para caer
            Vector3 newVelocity = velocity - normalSpeed * normal;
            if (vehicle != null)
            {
                vehicle.ApplyImpactVelocity(newVelocity, firstContact ? reactionLock : 0.08f);
            }
            else
            {
                body.linearVelocity = newVelocity;
            }

            if (tree != null && push.sqrMagnitude > 0.0001f)
            {
                push.Normalize();
                if (firstContact)
                {
                    tree.ReceiveImpulse(push, reactionImpulse.magnitude, false);
                }
                else
                {
                    tree.ReceivePush(push, -normalSpeed);
                }
            }

            if (!firstContact)
            {
                return;
            }

            LastImpulse = reactionImpulse.magnitude;
            LastImpactForce = LastImpulse / Mathf.Max(0.01f, impactDuration);

            if (DinoFollowCamera.Instance != null && LastImpactForce >= shakeForce)
            {
                DinoFollowCamera.Instance.TriggerShake(0.22f, 0.08f);
            }
        }


        void SolidifyTrees()
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform tree = transforms[i];
                if (!IsTreeRoot(tree))
                {
                    continue;
                }

                FitVisibleCollider(tree);
            }
        }

        static bool IsTreeRoot(Transform tree)
        {
            if (!IsTreeName(tree.name))
            {
                return false;
            }

            // Solo la raíz: si algún ancestro ya es un árbol, esta es una pieza suya.
            for (Transform parent = tree.parent; parent != null; parent = parent.parent)
            {
                if (IsTreeName(parent.name))
                {
                    return false;
                }
            }

            // Un contenedor vacío ("01_Arboleda_...") no es un árbol.
            return tree.GetComponentInChildren<Renderer>() != null;
        }

        static bool IsTreeName(string name)
        {
            if (name.IndexOf("LOD", System.StringComparison.Ordinal) >= 0 ||
                name.IndexOf("Arboleda", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Arboles", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            for (int i = 0; i < TreeTokens.Length; i++)
            {
                if (name.IndexOf(TreeTokens[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        Vector3 ContactNormal(Collision collision)
        {
            if (collision.contactCount > 0)
            {
                return collision.GetContact(0).normal;
            }

            Vector3 away = body.position - collision.collider.bounds.center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = -transform.forward;
            }
            return away.normalized;
        }

        void FitVisibleCollider(Transform tree)
        {
            if (tree.GetComponent<TreeReaction>() != null)
            {
                return;
            }

            if (!TryMeshBounds(tree, out Vector3 center, out Vector3 size))
            {
                return;
            }

            // La caja se apoya en la base del árbol. Si solo se recorta la altura alrededor
            // del centro, en un árbol alto queda flotando en la copa y el dinosaurio pasa por debajo.
            // Los límites están en metros de mundo, así que se convierten a la escala del árbol.
            Vector3 escala = tree.lossyScale;
            float ex = Mathf.Max(0.0001f, Mathf.Abs(escala.x));
            float ey = Mathf.Max(0.0001f, Mathf.Abs(escala.y));
            float ez = Mathf.Max(0.0001f, Mathf.Abs(escala.z));

            float baseY = Mathf.Min(center.y - size.y * 0.5f, 0f);

            // El sólido va en el tronco, que nace en el pivote del árbol. Centrarlo en la malla
            // lo ponía bajo la copa, que en árboles grandes está desplazada varios metros del tronco
            // (se atravesaban). El grosor se estima por la altura: de 1.2 m a 3.5 m.
            float alturaMundo = size.y * ey;
            float grosor = Mathf.Clamp(alturaMundo * 0.11f * solidWidth, 1.2f, 3.5f);
            size.x = grosor / ex;
            size.z = grosor / ez;
            size.y = Mathf.Clamp(alturaMundo, 3f, 8f) / ey;
            center = new Vector3(0f, baseY + size.y * 0.5f, 0f);

            Collider[] oldColliders = tree.GetComponentsInChildren<Collider>();
            for (int i = 0; i < oldColliders.Length; i++)
            {
                // Los triggers (p. ej. la zona de comida de DinoFoodTree) se conservan.
                if (!oldColliders[i].isTrigger) oldColliders[i].enabled = false;
            }

            BoxCollider solid = tree.gameObject.AddComponent<BoxCollider>();
            solid.center = center;
            solid.size = size;
            solid.isTrigger = false;
            solid.providesContacts = true;

            tree.gameObject.AddComponent<TreeReaction>();
        }

        static bool TryMeshBounds(Transform tree, out Vector3 center, out Vector3 size)
        {
            center = new Vector3(0f, 2.6f, 0f);
            size = new Vector3(2.8f, 5.2f, 2.8f);

            MeshFilter[] filters = tree.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter source = null;
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                {
                    continue;
                }
                if (filters[i].name.IndexOf("LOD0", System.StringComparison.Ordinal) >= 0)
                {
                    source = filters[i];
                    break;
                }
                if (source == null)
                {
                    source = filters[i];
                }
            }

            if (source == null)
            {
                return tree.GetComponentInChildren<Renderer>() != null;
            }

            Bounds mesh = source.sharedMesh.bounds;
            if (mesh.size.sqrMagnitude < 0.01f)
            {
                return true;
            }

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            Vector3 c = mesh.center;
            Vector3 e = mesh.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 corner = c + Vector3.Scale(e, new Vector3(xi, yi, zi));
                        Vector3 local = tree.InverseTransformPoint(source.transform.TransformPoint(corner));
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
            }

            center = (min + max) * 0.5f;
            size = max - min;
            return true;
        }

        bool IsTree(Collider collider)
        {
            if (collider.GetComponentInParent<TreeReaction>() != null)
            {
                return true;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                if (IsTreeName(current.name) ||
                    (!string.IsNullOrEmpty(treeNameToken) &&
                     current.name.IndexOf(treeNameToken, System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }
    }

    /// <summary>
    /// El árbol está enraizado: el impulso lo flexiona y la rigidez lo devuelve.
    /// Al tercer golpe se suelta y cae como una vara con pivote en la base:
    /// α = (3g / 2L) · sin(θ).
    /// </summary>
    public class TreeReaction : MonoBehaviour
    {
        [Tooltip("Grados por segundo que aporta cada N·s de impulso")]
        [SerializeField] float impulseGain = 0.006f;
        [Tooltip("Grados de flexión por cada m/s que el dinosaurio sigue empujando")]
        [SerializeField] float pushGain = 1.8f;
        [Tooltip("Rigidez que devuelve el tronco, en 1/s²")]
        [SerializeField] float stiffness = 18f;
        [Tooltip("Amortiguación del vaivén, en 1/s")]
        [SerializeField] float damping = 5.5f;
        [SerializeField] float maxLeanDegrees = 24f;
        [Tooltip("Golpes separados que aguantan las raíces")]
        [SerializeField] int hitsToFall = 3;
        [Tooltip("Segundos mínimos entre un golpe y el siguiente")]
        [SerializeField] float hitCooldown = 0.45f;

        Vector3 pivot;
        Vector3 bendAxis = Vector3.right;
        float bendAngle;
        float bendSpeed;
        float pushTarget;
        bool pushing;

        int hitCount;
        float nextHitTime;
        bool falling;
        bool fallen;
        float fallAngle;
        float fallSpeed;
        float trunkLength = 5f;

        Transform[] parts;
        Vector3[] restOffsets;
        Quaternion[] restRotations;
        Vector3[] restLocalPositions;
        Quaternion[] restLocalRotations;
        Quaternion restRootRotation;

        public bool HasFallen => fallen;
        public int HitCount => hitCount;
        public int HitsToFall => hitsToFall;

        void Start()
        {
            pivot = transform.position;
            restRootRotation = transform.rotation;
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                trunkLength = Mathf.Max(1.5f, box.size.y * Mathf.Abs(transform.lossyScale.y));
            }

            // Árbol de una sola pieza (sin hijos): la pieza que se flexiona es la propia raíz.
            bool sinHijos = transform.childCount == 0;
            int count = sinHijos ? 1 : transform.childCount;
            parts = new Transform[count];
            restOffsets = new Vector3[count];
            restRotations = new Quaternion[count];
            restLocalPositions = new Vector3[count];
            restLocalRotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = sinHijos ? transform : transform.GetChild(i);
                restOffsets[i] = parts[i].position - pivot;
                restRotations[i] = parts[i].rotation;
                restLocalPositions[i] = parts[i].localPosition;
                restLocalRotations[i] = parts[i].localRotation;
            }
        }

        /// <summary>
        /// Impulso de la acción, en la dirección en que empuja el dinosaurio.
        /// </summary>
        public void ReceiveImpulse(Vector3 pushDirection, float impulse, bool isHighSpeed = false)
        {
            if (falling || fallen || parts == null || pushDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (!SetBendAxis(pushDirection))
            {
                return;
            }
            bendSpeed += impulse * impulseGain;
            if (isHighSpeed)
            {
                BeginFall();
            }
            else
            {
                RegisterHit();
            }
        }

        /// <summary>
        /// Empuje continuo mientras el dinosaurio sigue apoyado en el tronco.
        /// </summary>
        public void ReceivePush(Vector3 pushDirection, float speed)
        {
            if (falling || fallen || parts == null || pushDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (!SetBendAxis(pushDirection))
            {
                return;
            }
            pushTarget = Mathf.Clamp(speed * pushGain, 0f, maxLeanDegrees);
            pushing = true;
        }

        bool SetBendAxis(Vector3 pushDirection)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, pushDirection.normalized);
            if (axis.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            axis.Normalize();
            if (Vector3.Dot(axis, bendAxis) < 0f)
            {
                bendAngle = -bendAngle;
                bendSpeed = -bendSpeed;
                axis = -axis;
            }
            bendAxis = axis;
            return true;
        }

        void RegisterHit()
        {
            if (Time.time < nextHitTime)
            {
                return;
            }

            nextHitTime = Time.time + hitCooldown;
            hitCount++;
            if (hitCount >= hitsToFall)
            {
                BeginFall();
            }
        }

        void BeginFall()
        {
            falling = true;
            fallAngle = Mathf.Clamp(Mathf.Abs(bendAngle), 8f, 50f);
            fallSpeed = Mathf.Max(Mathf.Abs(bendSpeed), 45f);

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }
                parts[i].localPosition = restLocalPositions[i];
                parts[i].localRotation = restLocalRotations[i];
            }

            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger) colliders[i].enabled = false;
            }

            ApplyFallRotation();
        }

        void ApplyFallRotation()
        {
            transform.rotation = Quaternion.AngleAxis(fallAngle, bendAxis) * restRootRotation;
        }

        void LateUpdate()
        {
            if (parts == null || fallen)
            {
                return;
            }

            if (falling)
            {
                // Vara con pivote en la raíz: τ = mg·(L/2)·sin(θ), I = m L² / 3
                float alpha = (3f * 9.81f) / (2f * trunkLength) * Mathf.Sin(fallAngle * Mathf.Deg2Rad);
                fallSpeed += alpha * Mathf.Rad2Deg * Time.deltaTime;
                fallAngle += fallSpeed * Time.deltaTime;
                if (fallAngle >= 82f)
                {
                    fallAngle = 82f;
                    fallSpeed = 0f;
                    falling = false;
                    fallen = true;
                    ApplyFallRotation();
                    Collider[] colliders = GetComponents<Collider>();
                    for (int i = 0; i < colliders.Length; i++)
                    {
                        // Solo el sólido creado al inicio; el collider original del prefab sigue apagado.
                        if (colliders[i] is BoxCollider && !colliders[i].isTrigger) colliders[i].enabled = true;
                    }
                    return;
                }

                ApplyFallRotation();
                return;
            }

            bool wasPushing = pushing;
            float target = wasPushing ? pushTarget : 0f;
            pushing = false;
            pushTarget = 0f;

            float accel = stiffness * (target - bendAngle) - damping * bendSpeed;
            bendSpeed += accel * Time.deltaTime;
            bendAngle += bendSpeed * Time.deltaTime;
            bendAngle = Mathf.Clamp(bendAngle, -maxLeanDegrees, maxLeanDegrees);

            if (!wasPushing && Mathf.Abs(bendAngle) < 0.05f && Mathf.Abs(bendSpeed) < 0.05f)
            {
                bendAngle = 0f;
                bendSpeed = 0f;
            }

            Quaternion bend = Quaternion.AngleAxis(bendAngle, bendAxis);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }
                parts[i].position = pivot + bend * restOffsets[i];
                parts[i].rotation = bend * restRotations[i];
            }
        }
    }
}
