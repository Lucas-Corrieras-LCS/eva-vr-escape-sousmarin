using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Montée de la tension (étages 2 et 3) : une brèche laisse entrer l'eau, le niveau monte lentement dans le module
    /// tant que l'étage n'est pas résolu, puis l'eau est pompée. Jamais de défaite, et jamais au-dessus des mollets
    /// (le joueur ne doit pas se sentir noyé ni perdre ses repères au sol).
    /// Étage des ballasts : un coup de bélier fait monter l'eau d'un coup (<see cref="Surge"/>), la pompe du module
    /// la vide dès que son collecteur est réglé (<see cref="Drain"/>).
    /// </summary>
    public class RisingWater : MonoBehaviour
    {
        [SerializeField] Transform surface;
        [SerializeField] float startHeight = 0.01f;
        [SerializeField] float maxHeight = 0.18f;
        [Tooltip("Temps pour atteindre la hauteur max (s).")]
        [SerializeField, Min(10f)] float riseTime = 600f;
        [SerializeField, Min(1f)] float drainTime = 6f;

        float height;
        bool draining;

        void OnEnable() => GameStateManager.PhaseChanged += OnPhaseChanged;
        void OnDisable() => GameStateManager.PhaseChanged -= OnPhaseChanged;

        void Start()
        {
            height = startHeight;
            Apply();
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.FloorSolved || phase == GamePhase.Finished) draining = true;
        }

        /// <summary>Montée brusque (coup de bélier), bornée au niveau maximal.</summary>
        public void Surge(float amount)
        {
            if (draining) return;
            height = Mathf.Min(maxHeight, height + amount);
            Apply();
        }

        /// <summary>Vidange du module (pompe amorcée), sans attendre la fin de l'étage.</summary>
        public void Drain() => draining = true;

        void Update()
        {
            if (surface == null) return;
            if (draining)
            {
                if (height <= -0.02f) return;
                height -= (maxHeight - startHeight) * Time.deltaTime / drainTime;
                if (height <= -0.02f) surface.gameObject.SetActive(false);
            }
            else
            {
                if (height >= maxHeight) return;
                height = Mathf.Min(maxHeight, height + (maxHeight - startHeight) * Time.deltaTime / riseTime);
            }
            Apply();
        }

        void Apply()
        {
            Vector3 p = surface.localPosition;
            p.y = height;
            surface.localPosition = p;
        }
    }
}
