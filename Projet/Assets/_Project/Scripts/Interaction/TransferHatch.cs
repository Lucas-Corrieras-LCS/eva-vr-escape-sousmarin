using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Sas à objets entre deux modules voisins (tube pneumatique dans le mur commun).
    /// On pose un objet dans le sas, on appuie sur le bouton : il est aspiré et ressort de l'autre côté.
    /// Le serveur reprend l'objet et le téléporte : aucune physique partagée entre les deux joueurs.
    /// </summary>
    public class TransferHatch : NetworkBehaviour
    {
        [Serializable]
        public class Endpoint
        {
            [Range(0, 3)] public int quadrant;
            [Tooltip("Volume du sas (collider trigger) où l'on dépose l'objet.")]
            public BoxCollider zone;
            public Transform arrivalPoint;
            public XRSimpleInteractable sendButton;
            public ParticleSystem bubbles;
            public AudioSource sound;
        }

        [SerializeField] Endpoint sideA = new Endpoint();
        [SerializeField] Endpoint sideB = new Endpoint();
        [SerializeField] LayerMask itemLayers = ~0;

        readonly Collider[] hits = new Collider[8];

        public override void OnNetworkSpawn()
        {
            sideA.sendButton.selectEntered.AddListener(OnSendA);
            sideB.sendButton.selectEntered.AddListener(OnSendB);
        }

        public override void OnNetworkDespawn()
        {
            sideA.sendButton.selectEntered.RemoveListener(OnSendA);
            sideB.sendButton.selectEntered.RemoveListener(OnSendB);
        }

        void OnSendA(SelectEnterEventArgs args) => TrySend(sideA, 0);
        void OnSendB(SelectEnterEventArgs args) => TrySend(sideB, 1);

        void TrySend(Endpoint from, int side)
        {
            if (LocalRig.Instance == null || LocalRig.Instance.Quadrant != from.quadrant) return;

            Bounds bounds = from.zone.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, hits, Quaternion.identity, itemLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = hits[i].attachedRigidbody;
                if (body == null || !body.TryGetComponent(out NetworkGrabbable item)) continue;
                if (item.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected) continue;
                SendRpc(item.NetworkObject, side);
                return;
            }
        }

        [Rpc(SendTo.Server)]
        void SendRpc(NetworkObjectReference itemReference, int fromSide)
        {
            if (!itemReference.TryGet(out NetworkObject item)) return;
            Endpoint destination = fromSide == 0 ? sideB : sideA;

            // Le serveur reprend la main sur l'objet avant de le déplacer.
            item.RemoveOwnership();
            if (item.TryGetComponent(out Rigidbody body) && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (item.TryGetComponent(out NetworkTransform networkTransform))
                networkTransform.Teleport(destination.arrivalPoint.position, destination.arrivalPoint.rotation, item.transform.localScale);

            PlayEffectsRpc();
        }

        [Rpc(SendTo.Everyone)]
        void PlayEffectsRpc()
        {
            Play(sideA);
            Play(sideB);
        }

        static void Play(Endpoint endpoint)
        {
            if (endpoint.bubbles != null) endpoint.bubbles.Play();
            if (endpoint.sound != null) endpoint.sound.Play();
        }
    }
}
