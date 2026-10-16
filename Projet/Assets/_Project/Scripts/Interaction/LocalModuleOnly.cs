using UnityEngine;
using Abysses.Core;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// N'active ces composants que dans le module du joueur local.
    /// Indispensable pour les interactions automatiques (sockets) : sans ça, un objet réseau qui passe près
    /// d'un socket d'un AUTRE module serait capturé localement chez chaque client.
    /// </summary>
    public class LocalModuleOnly : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] Behaviour[] components;

        void OnEnable()
        {
            LocalRig.QuadrantAssigned += Apply;
            Apply(LocalRig.Instance != null ? LocalRig.Instance.Quadrant : -1);
        }

        void OnDisable() => LocalRig.QuadrantAssigned -= Apply;

        void Apply(int localQuadrant)
        {
            bool mine = localQuadrant == quadrant;
            for (int i = 0; i < components.Length; i++)
                if (components[i] != null) components[i].enabled = mine;
        }

        void OnValidate()
        {
            if (!PlayArea.IsValid(quadrant)) quadrant = 0;
        }
    }
}
