using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Retour sonore des grandes étapes de la partie, entendu par tous : étage résolu (remise sous tension).
    /// Réagit aux changements de phase (événement), aucun polling.
    /// </summary>
    public class PhaseAudio : MonoBehaviour
    {
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip floorSolved;

        GamePhase lastPhase = GamePhase.Lobby;

        void OnEnable() => GameStateManager.PhaseChanged += OnPhaseChanged;
        void OnDisable() => GameStateManager.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.FloorSolved && lastPhase == GamePhase.Playing && source != null && floorSolved != null)
                source.PlayOneShot(floorSolved);
            lastPhase = phase;
        }
    }
}
