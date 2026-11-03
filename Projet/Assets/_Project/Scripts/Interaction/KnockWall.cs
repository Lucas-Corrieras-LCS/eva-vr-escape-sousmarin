using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Plaque de coque partagée par deux modules voisins : quand l'un tape dessus, l'autre entend le coup
    /// de son côté de la cloison (son spatialisé) et le sent dans la manette. De quoi se parler en morse,
    /// comme de vrais sous-mariniers, ou simplement attirer l'attention du coéquipier.
    /// </summary>
    public class KnockWall : NetworkBehaviour
    {
        [System.Serializable]
        public class Side
        {
            [Range(0, 3)] public int quadrant;
            public XRSimpleInteractable plate;
            public AudioSource knock;
        }

        [SerializeField] Side sideA = new Side();
        [SerializeField] Side sideB = new Side();
        [SerializeField, Min(0.05f)] float cooldown = 0.12f;

        float nextKnock;

        public override void OnNetworkSpawn()
        {
            sideA.plate.selectEntered.AddListener(OnKnockA);
            sideB.plate.selectEntered.AddListener(OnKnockB);
        }

        public override void OnNetworkDespawn()
        {
            sideA.plate.selectEntered.RemoveListener(OnKnockA);
            sideB.plate.selectEntered.RemoveListener(OnKnockB);
        }

        void OnKnockA(SelectEnterEventArgs args) => Knock(sideA, 0);
        void OnKnockB(SelectEnterEventArgs args) => Knock(sideB, 1);

        void Knock(Side from, int side)
        {
            if (LocalRig.Instance == null || LocalRig.Instance.Quadrant != from.quadrant || Time.unscaledTime < nextKnock) return;
            nextKnock = Time.unscaledTime + cooldown;
            // Le frappeur entend son propre coup tout de suite (pas d'aller-retour réseau).
            if (from.knock != null) from.knock.PlayOneShot(from.knock.clip, 0.6f);
            LocalRig.Pulse(0.5f, 0.04f);
            KnockRpc(side);
        }

        [Rpc(SendTo.NotMe)]
        void KnockRpc(int fromSide)
        {
            Side to = fromSide == 0 ? sideB : sideA;
            if (to.knock != null) to.knock.PlayOneShot(to.knock.clip);
            if (LocalRig.Instance != null && LocalRig.Instance.Quadrant == to.quadrant) LocalRig.Pulse(0.35f, 0.05f);
        }
    }
}
