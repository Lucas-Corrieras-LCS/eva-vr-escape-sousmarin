using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Core;

namespace Abysses.Interaction
{
    /// <summary>
    /// Objet perdu qui revient : un objet tombé derrière un mur, sous le sol ou hors de toute zone jouable
    /// réapparaît à son dernier emplacement sûr (sinon à sa place d'origine). Personne ne doit sortir de sa zone
    /// pour le ramasser, et une partie ne peut plus se bloquer.
    /// Vérifié par le propriétaire de l'objet (celui qui simule sa physique), 2 fois par seconde.
    /// </summary>
    [RequireComponent(typeof(NetworkGrabbable))]
    public class LostItemReturn : NetworkBehaviour
    {
        [Tooltip("Temps passé « perdu » avant le retour (s).")]
        [SerializeField, Min(0.2f)] float delay = 1.5f;
        [Tooltip("Marge par rapport au bord du quadrant : au-delà, l'objet est derrière un mur.")]
        [SerializeField, Min(0f)] float edgeMargin = PlayArea.WallInset;
        [SerializeField] AudioSource returnSound;

        const float CheckInterval = 0.5f;
        const float MinHeight = -0.25f;
        const float MaxHeight = 3.4f;

        XRGrabInteractable grab;
        Rigidbody body;
        NetworkTransform networkTransform;
        Vector3 safePosition;
        Quaternion safeRotation;
        float lostSince = -1f;
        float nextCheck;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            networkTransform = GetComponent<NetworkTransform>();
            safePosition = transform.position;
            safeRotation = transform.rotation;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckInterval;
            if (grab.isSelected)
            {
                lostSince = -1f;
                return;
            }

            Vector3 position = transform.position;
            if (IsSafe(position))
            {
                lostSince = -1f;
                // Dernier emplacement sûr = là où l'objet s'est posé (immobile).
                if (body.isKinematic || body.linearVelocity.sqrMagnitude < 0.0025f)
                {
                    safePosition = position;
                    safeRotation = transform.rotation;
                }
                return;
            }

            if (lostSince < 0f) lostSince = Time.unscaledTime;
            else if (Time.unscaledTime - lostSince >= delay) ReturnToSafety();
        }

        bool IsSafe(Vector3 position)
        {
            if (position.y < MinHeight || position.y > MaxHeight) return false;
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
                if (PlayArea.IsInside(q, position, edgeMargin)) return true;
            return false;
        }

        void ReturnToSafety()
        {
            lostSince = -1f;
            Vector3 position = safePosition + Vector3.up * 0.05f;
            // Même méthode que le sas : le Rigidbody est déplacé avec l'objet, sinon la physique le ramènerait.
            body.position = position;
            body.rotation = safeRotation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (networkTransform != null) networkTransform.Teleport(position, safeRotation, transform.localScale);
            else transform.SetPositionAndRotation(position, safeRotation);
            ReturnedRpc();
        }

        [Rpc(SendTo.Everyone)]
        void ReturnedRpc()
        {
            if (returnSound != null) returnSound.Play();
        }
    }
}
