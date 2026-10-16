using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Abysses.Interaction
{
    /// <summary>
    /// Objet attrapable partagé (ex. fusible envoyé par le sas).
    /// Celui qui l'attrape en devient propriétaire : son NetworkTransform (autorité propriétaire) diffuse la position,
    /// et lui seul simule la physique ; chez les autres le Rigidbody reste cinématique.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public class NetworkGrabbable : NetworkBehaviour
    {
        XRGrabInteractable grab;
        Rigidbody body;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
            UpdatePhysicsAuthority();
        }

        public override void OnNetworkDespawn()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        public override void OnGainedOwnership() => UpdatePhysicsAuthority();
        public override void OnLostOwnership() => UpdatePhysicsAuthority();

        void UpdatePhysicsAuthority()
        {
            // Pendant une prise, c'est le XRGrabInteractable qui pilote le Rigidbody.
            if (grab.isSelected) return;
            body.isKinematic = !IsOwner;
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            if (!IsOwner) RequestOwnershipRpc();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            // Le XRGrabInteractable restaure l'état cinématique d'avant la prise : on le corrige selon l'ownership actuel.
            body.isKinematic = !IsOwner;
        }

        [Rpc(SendTo.Server)]
        void RequestOwnershipRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (OwnerClientId != sender) NetworkObject.ChangeOwnership(sender);
        }
    }
}
