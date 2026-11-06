using UnityEngine;
using Abysses.Core;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Indice privé : n'est rendu que pour le joueur du module (personne ne peut le lire par un hublot).
    /// Coopération forcée : l'info est chez l'un, l'action chez l'autre, il faut la décrire de vive voix.
    /// Sans quadrant local (pupitre du maître du jeu, captures dans l'éditeur), tout reste visible.
    /// </summary>
    public class PrivateView : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] Renderer[] renderers;

        void OnEnable()
        {
            LocalRig.QuadrantAssigned += Apply;
            Apply(LocalRig.Instance != null ? LocalRig.Instance.Quadrant : -1);
        }

        void OnDisable() => LocalRig.QuadrantAssigned -= Apply;

        void Apply(int localQuadrant)
        {
            bool visible = !PlayArea.IsValid(localQuadrant) || localQuadrant == quadrant;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }

        void OnValidate()
        {
            if (!PlayArea.IsValid(quadrant)) quadrant = 0;
        }
    }
}
