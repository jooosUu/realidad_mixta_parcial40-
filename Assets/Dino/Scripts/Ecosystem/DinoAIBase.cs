using System.Collections.Generic;
using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Locomoción común de la fauna sin patinaje:
    /// - La velocidad se interpola (aceleración finita) y el giro tiene una velocidad angular máxima.
    /// - Solo se avanza en proporción a la alineación frontal (Vector3.Dot), así no se desliza de lado al girar.
    /// - animator.speed = velocidad real / velocidad natural del clip (Walk o Run) a la escala del animal.
    /// - La altura Y sale de Terrain.activeTerrain.SampleHeight y el cuerpo se inclina con la normal del suelo.
    /// </summary>
    public abstract class DinoAIBase : MonoBehaviour
    {
        [Header("Locomoción (anti-patinaje)")]
        [Tooltip("m/s que recorre el clip Walk a escala 1 (medido de la variante _RM)")]
        public float velocidadClipCaminar = 1.5f;
        [Tooltip("m/s que recorre el clip Run a escala 1 (medido de la variante _RM)")]
        public float velocidadClipCorrer = 6f;
        [Tooltip("Aceleración y frenado en m/s²")]
        public float aceleracion = 3f;
        [Tooltip("Giro máximo en grados por segundo a velocidad de paso")]
        public float giroGradosSeg = 70f;
        [Tooltip("El modelo mira hacia -Z (Velociraptor PBR)")]
        public bool frenteInvertido = false;
        [Tooltip("Inclinación máxima del cuerpo siguiendo la pendiente")]
        public float inclinacionMax = 10f;
        [Tooltip("No se desplaza (modelos sin clips, p. ej. el Anquilosaurio)")]
        public bool estatico = false;

        protected Animator animator;
        protected DinoVehicleController jugador;
        protected float velActual;
        protected float yaw;

        /// <summary>Estado de animación cuando está quieto (Idle, Eat, Drink, Sniff, Sleep...).</summary>
        protected string animQuieta = "Idle";

        string accion;
        float accionTiempo;
        float accionDuracionMax;

        string estadoAnim;
        bool corriendoAnim;
        Terrain terreno;
        Vector3 normalSuave = Vector3.up;
        readonly Dictionary<string, string> resueltos = new();

        protected float Escala => Mathf.Max(0.05f, Mathf.Abs(transform.lossyScale.y));
        protected float VelCaminar => velocidadClipCaminar * Escala;
        protected float VelCorrer => velocidadClipCorrer * Escala;
        protected Vector3 Frente => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        protected bool AccionEnCurso => accion != null;
        public float VelocidadActual => velActual;

        protected virtual void Start()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                DinoEventosAnimacion.Asegurar(animator); // eventos Roar/Growl/... de los clips
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            jugador = FindAnyObjectByType<DinoVehicleController>();
            terreno = Terrain.activeTerrain;

            yaw = transform.eulerAngles.y + (frenteInvertido ? 180f : 0f);
            PegarAlSuelo(true);
        }

        // ------------------------------------------------------------------
        // Movimiento
        // ------------------------------------------------------------------

        /// <summary>Avanza hacia destino. Llamar una sola vez por frame (o Frenar).</summary>
        protected void Locomover(Vector3 destino, float velDeseada, float distParada = 1.2f)
        {
            float dt = Time.deltaTime;
            Vector3 d = destino - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            float objetivo = 0f;

            if (!estatico && dist > distParada && velDeseada > 0f)
            {
                float yawObjetivo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

                // Quieto gira despacio (como pivotando con pasos cortos); andando gira más.
                float factorGiro = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(velActual / Mathf.Max(0.1f, VelCaminar)));
                if (velActual > VelCaminar * 1.3f) factorGiro *= 1.3f;
                yaw = Mathf.MoveTowardsAngle(yaw, yawObjetivo, giroGradosSeg * factorGiro * dt);

                // Solo avanza lo que está alineado: de lado no se desliza.
                float alineacion = Vector3.Dot(Frente, d / dist);
                objetivo = velDeseada * Mathf.Clamp(alineacion, 0.12f, 1f);

                // Frenada suave al llegar: v <= sqrt(2·a·d)
                float frenada = Mathf.Sqrt(2f * aceleracion * Mathf.Max(0f, dist - distParada)) + 0.2f;
                objetivo = Mathf.Min(objetivo, frenada);
            }

            AplicarVelocidad(objetivo, dt);
        }

        /// <summary>Frena hasta parar sin cambiar de rumbo.</summary>
        protected void Frenar()
        {
            AplicarVelocidad(0f, Time.deltaTime);
        }

        void AplicarVelocidad(float objetivo, float dt)
        {
            velActual = Mathf.MoveTowards(velActual, objetivo, aceleracion * dt);
            if (estatico) velActual = 0f;
            if (velActual > 0.001f)
            {
                transform.position += Frente * (velActual * dt);
            }
        }

        /// <summary>Gira en el sitio solo si está andando; sirve para encarar algo antes de una acción.</summary>
        protected float AlineacionCon(Vector3 punto)
        {
            Vector3 d = punto - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return 1f;
            return Vector3.Dot(Frente, d.normalized);
        }

        protected void PegarAlSuelo(bool inmediato = false)
        {
            if (terreno == null) terreno = Terrain.activeTerrain;
            Vector3 p = transform.position;
            Vector3 normal = Vector3.up;

            if (terreno != null && terreno.terrainData != null)
            {
                Vector3 tp = terreno.transform.position;
                var td = terreno.terrainData;
                p.y = terreno.SampleHeight(p) + tp.y;
                float u = Mathf.Clamp01((p.x - tp.x) / td.size.x);
                float v = Mathf.Clamp01((p.z - tp.z) / td.size.z);
                normal = td.GetInterpolatedNormal(u, v);
            }
            else if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)
                     && !hit.transform.IsChildOf(transform))
            {
                p.y = hit.point.y;
                normal = hit.normal;
            }

            normalSuave = inmediato ? normal : Vector3.Slerp(normalSuave, normal, Time.deltaTime * 4f);
            Vector3 n = Vector3.RotateTowards(Vector3.up, normalSuave, inclinacionMax * Mathf.Deg2Rad, 0f);

            transform.position = p;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, n)
                               * Quaternion.Euler(0f, yaw + (frenteInvertido ? 180f : 0f), 0f);
        }

        protected Vector3 PosJugador => jugador != null ? jugador.transform.position : transform.position + Vector3.forward * 9999f;

        protected float DistanciaJugador()
        {
            if (jugador == null) return float.MaxValue;
            Vector3 d = jugador.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        // ------------------------------------------------------------------
        // Animación
        // ------------------------------------------------------------------

        /// <summary>Reproduce un estado de una vez (Roar, Attack, Call...). Detiene el desplazamiento.</summary>
        protected void IniciarAccion(string estado, float duracionMax)
        {
            accion = estado;
            accionTiempo = 0f;
            accionDuracionMax = duracionMax;
            estadoAnim = null; // fuerza el CrossFade aunque ya estuviera en ese estado
        }

        protected void CancelarAccion() => accion = null;

        void ActualizarAccion()
        {
            if (accion == null) return;
            accionTiempo += Time.deltaTime;
            bool terminada = accionTiempo >= accionDuracionMax;

            if (!terminada && accionTiempo > 0.4f && animator != null && animator.isActiveAndEnabled)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && !info.loop && info.normalizedTime >= 0.96f) terminada = true;
            }
            if (terminada) accion = null;
        }

        /// <summary>Llamar al final de Update: decide el clip y sincroniza animator.speed con velActual.</summary>
        protected void ActualizarAnimacion()
        {
            ActualizarAccion();
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            if (accion != null)
            {
                Reproducir(accion, 0.2f);
                animator.speed = 1f;
                return;
            }

            float umbralParado = VelCaminar * 0.18f;
            if (velActual > umbralParado)
            {
                // Histéresis entre andar y correr para que no parpadee.
                float umbralCorrer = Mathf.Lerp(VelCaminar, VelCorrer, corriendoAnim ? 0.35f : 0.5f);
                corriendoAnim = velActual > umbralCorrer;

                if (corriendoAnim)
                {
                    Reproducir("Run", 0.25f);
                    animator.speed = Mathf.Clamp(velActual / VelCorrer, 0.5f, 1.8f);
                }
                else
                {
                    Reproducir("Walk", 0.25f);
                    animator.speed = Mathf.Clamp(velActual / VelCaminar, 0.2f, 1.8f);
                }
            }
            else
            {
                corriendoAnim = false;
                Reproducir(animQuieta, 0.35f);
                animator.speed = 1f;
            }
        }

        void Reproducir(string estado, float fundido)
        {
            string real = Resolver(estado);
            if (real == null || real == estadoAnim) return;
            estadoAnim = real;
            animator.CrossFadeInFixedTime(real, fundido);
        }

        static readonly Dictionary<string, string[]> Alternativas = new()
        {
            { "Idle", new[] { "Idle", "IdleA", "RaptorArmature|Raptor_Idle1_Anim", "Armature|Pachycephalasaurus_IdleA" } },
            { "Idle2", new[] { "Idle2", "Idle" } },
            { "Walk", new[] { "Walk", "RaptorArmature|Raptor_Walk_Anim", "Armature|Pachycephalasaurus_Walk", "Idle" } },
            { "Run", new[] { "Run", "RaptorArmature|Raptor_Run1_Anim", "Armature|Pachycephalasaurus_Run", "Walk" } },
            { "Roar", new[] { "Roar", "Call", "Idle2", "Idle" } },
            { "Call", new[] { "Call", "Roar", "Idle" } },
            { "Attack", new[] { "Attack", "Roar", "Idle" } },
            { "Eat", new[] { "Eat", "Idle2", "Idle" } },
            { "Drink", new[] { "Drink", "Eat", "Idle" } },
            { "Sniff", new[] { "Sniff", "Eat", "Idle2", "Idle" } },
            { "Sleep", new[] { "Sleep", "Idle" } },
            { "GoToSleep", new[] { "GoToSleep", "Sleep", "Idle" } },
            { "WakeUp", new[] { "WakeUp", "Idle" } },
        };

        string Resolver(string estado)
        {
            if (resueltos.TryGetValue(estado, out var r)) return r;
            r = null;
            if (!Alternativas.TryGetValue(estado, out var opciones)) opciones = new[] { estado, "Idle" };
            foreach (var o in opciones)
            {
                if (animator.HasState(0, Animator.StringToHash(o))) { r = o; break; }
            }
            resueltos[estado] = r;
            return r;
        }

        /// <summary>Hay que llamarlo si se cambia el controller en tiempo de ejecución.</summary>
        public void RefrescarAnimator()
        {
            resueltos.Clear();
            estadoAnim = null;
        }
    }
}
