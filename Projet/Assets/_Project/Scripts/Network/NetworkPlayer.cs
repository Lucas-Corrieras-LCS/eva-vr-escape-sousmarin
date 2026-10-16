using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Player;

namespace Abysses.Network
{
    /// <summary>
    /// Avatar réseau d'un joueur : tête + 2 mains, une couleur par quadrant.
    /// Le propriétaire recopie les poses de son rig XR local ; les NetworkTransform (autorité propriétaire)
    /// des enfants Head / LeftHand / RightHand les diffusent aux autres.
    /// Le serveur attribue à chaque joueur le premier quadrant libre (Q0, Q1, Q2, Q3).
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Points synchronisés")]
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;

        [Header("Apparence")]
        [Tooltip("Renderers recolorés selon le quadrant du joueur.")]
        [SerializeField] Renderer[] tintedRenderers;
        [Tooltip("Un matériau par quadrant (Q0..Q3), partagés pour rester compatibles SRP Batcher.")]
        [SerializeField] Material[] quadrantMaterials;
        [Tooltip("Visuels masqués pour le joueur local (il voit déjà ses manettes).")]
        [SerializeField] GameObject[] hiddenForOwner;

        /// <summary>Tous les joueurs connectés (registre, évite les FindObjectsByType).</summary>
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>(PlayArea.QuadrantCount);
        public static NetworkPlayer Local { get; private set; }
        public static event Action<NetworkPlayer> Spawned;
        public static event Action<NetworkPlayer> Despawned;

        public readonly NetworkVariable<int> Quadrant = new NetworkVariable<int>(-1);

        public Transform Head => head;

        Transform sourceHead;
        Transform sourceLeft;
        Transform sourceRight;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Quadrant.OnValueChanged += OnQuadrantChanged;

            if (IsServer) Quadrant.Value = AllocateQuadrant();

            if (IsOwner)
            {
                Local = this;
                LocalRig rig = LocalRig.Instance;
                if (rig != null)
                {
                    sourceHead = rig.Head;
                    sourceLeft = rig.LeftHand;
                    sourceRight = rig.RightHand;
                }
                for (int i = 0; i < hiddenForOwner.Length; i++)
                    if (hiddenForOwner[i] != null) hiddenForOwner[i].SetActive(false);
            }

            ApplyQuadrant(Quadrant.Value);
            Spawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            Quadrant.OnValueChanged -= OnQuadrantChanged;
            All.Remove(this);
            if (Local == this) Local = null;
            Despawned?.Invoke(this);
        }

        /// <summary>Masque de bits des quadrants occupés (bit n = Qn).</summary>
        public static int OccupiedQuadrantMask()
        {
            int mask = 0;
            for (int i = 0; i < All.Count; i++)
                if (PlayArea.IsValid(All[i].Quadrant.Value)) mask |= 1 << All[i].Quadrant.Value;
            return mask;
        }

        int AllocateQuadrant()
        {
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
            {
                bool taken = false;
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i] != this && All[i].Quadrant.Value == q) { taken = true; break; }
                }
                if (!taken) return q;
            }
            return -1;
        }

        void OnQuadrantChanged(int previous, int current) => ApplyQuadrant(current);

        void ApplyQuadrant(int quadrant)
        {
            if (!PlayArea.IsValid(quadrant)) return;

            if (quadrantMaterials != null && quadrantMaterials.Length > 0)
            {
                Material material = quadrantMaterials[quadrant % quadrantMaterials.Length];
                for (int i = 0; i < tintedRenderers.Length; i++)
                    if (tintedRenderers[i] != null) tintedRenderers[i].sharedMaterial = material;
            }

            if (IsOwner && LocalRig.Instance != null)
                LocalRig.Instance.AssignQuadrant(quadrant);
        }

        void LateUpdate()
        {
            if (!IsOwner || sourceHead == null) return;
            head.SetPositionAndRotation(sourceHead.position, sourceHead.rotation);
            if (sourceLeft != null) leftHand.SetPositionAndRotation(sourceLeft.position, sourceLeft.rotation);
            if (sourceRight != null) rightHand.SetPositionAndRotation(sourceRight.position, sourceRight.rotation);
        }
    }
}
