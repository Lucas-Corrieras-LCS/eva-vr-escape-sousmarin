using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Montée de la tension (étage 2) : une brèche laisse entrer l'eau, le niveau monte lentement dans le module
    /// tant que l'étage n'est pas résolu, puis l'eau est pompée. Purement visuel : jamais de défaite, jamais au-dessus
    /// des chevilles (le joueur ne doit pas se sentir noyé ni perdre ses repères au sol).
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
