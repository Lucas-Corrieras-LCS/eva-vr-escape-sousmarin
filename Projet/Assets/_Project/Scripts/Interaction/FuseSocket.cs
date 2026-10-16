using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Emplacement de fusible synchronisé. Quand le bon fusible y est posé, le serveur le « consomme »
    /// (despawn) et l'emplacement affiche un fusible fixe : pas de physique partagée fragile à maintenir.
    /// Le socket n'est actif que dans le module du joueur local (voir <see cref="LocalModuleOnly"/>).
    /// </summary>
    public class FuseSocket : NetworkBehaviour
    {
        [SerializeField] FuseSocketInteractor socket;
        [SerializeField] PuzzleObjective objective;
        [Tooltip("Fusible fixe affiché une fois l'objectif résolu.")]
        [SerializeField] GameObject installedVisual;
        [SerializeField] AudioSource insertSound;

        public override void OnNetworkSpawn()
        {
            socket.selectEntered.AddListener(OnSocketed);
            objective.StateChanged += Apply;
            Apply(objective.Solved.Value);
        }

        public override void OnNetworkDespawn()
        {
            socket.selectEntered.RemoveListener(OnSocketed);
            objective.StateChanged -= Apply;
        }

        void OnSocketed(SelectEnterEventArgs args)
        {
            NetworkObject fuse = args.interactableObject.transform.GetComponent<NetworkObject>();
            if (fuse != null && fuse.IsSpawned) InsertRpc(fuse);
        }

        [Rpc(SendTo.Server)]
        void InsertRpc(NetworkObjectReference fuseReference)
        {
            if (objective.Solved.Value || !fuseReference.TryGet(out NetworkObject fuse)) return;
            if (!fuse.TryGetComponent(out FuseItem item) || item.FuseId != socket.RequiredFuseId) return;
            fuse.Despawn(true);
            objective.ServerSolve();
        }

        void Apply(bool solved)
        {
            if (installedVisual != null) installedVisual.SetActive(solved);
            if (solved)
            {
                socket.socketActive = false;
                if (insertSound != null && IsSpawned) insertSound.Play();
            }
        }
    }
}
