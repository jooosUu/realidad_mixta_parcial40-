using UnityEngine;

namespace DinoVehicle
{
    /// <summary>
    /// Recibe los AnimationEvents que traen los clips de los paquetes de dinosaurios
    /// (Roar, Growl, ThroatEffect, GutEffect, LegTwitchEffect, Bark, Call1-3, Screech, Sniff, Yelp, Death).
    /// Sin un receptor Unity lanza "AnimationEvent '...' has no receiver" cada vez que suenan.
    /// Se pone solo en todos los Animator de la escena (fauna, menú y monturas), sin tocar escenas ni prefabs.
    /// Los métodos no hacen nada a propósito: aquí se pueden enganchar sonidos o partículas más adelante.
    /// </summary>
    [DisallowMultipleComponent]
    public class DinoEventosAnimacion : MonoBehaviour
    {
        public void Roar() { }
        public void Growl() { }
        public void ThroatEffect() { }
        public void GutEffect() { }
        public void LegTwitchEffect() { }
        public void Bark() { }
        public void Call1() { }
        public void Call2() { }
        public void Call3() { }
        public void Screech() { }
        public void Sniff() { }
        public void Yelp() { }
        public void Death() { }

        // ------------------------------------------------------------------
        // Vigilante: añade el receptor a los Animator que aparezcan (p. ej. al cambiar de montura).
        // ------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (FindAnyObjectByType<Vigilante>() != null) return;
            var go = new GameObject("DINO WEST · Receptor de eventos de animación") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<Vigilante>();
        }

        public static void Asegurar(Animator a)
        {
            if (a != null && !a.TryGetComponent<DinoEventosAnimacion>(out _)) a.gameObject.AddComponent<DinoEventosAnimacion>();
        }

        class Vigilante : MonoBehaviour
        {
            float siguiente;

            void Update()
            {
                // unscaledTime: también con el menú en pausa (timeScale 0).
                if (Time.unscaledTime < siguiente) return;
                siguiente = Time.unscaledTime + 0.25f;
                foreach (var a in FindObjectsByType<Animator>(FindObjectsInactive.Exclude)) Asegurar(a);
            }
        }
    }
}
