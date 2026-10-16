using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Énigme coopérative « remise sous tension » : chaque module occupé a un levier,
    /// tous doivent être abaissés dans une fenêtre de quelques secondes (« 3, 2, 1… »).
    /// Sinon les leviers remontent et il faut recommencer.
    /// </summary>
    public class SyncLeverGroup : NetworkBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Range(0, 3)] public int quadrant;
            public LeverHandle lever;
        }

        [SerializeField] PuzzleObjective objective;
        [SerializeField] Entry[] levers;
        [SerializeField, Min(0.5f)] float syncWindow = 2f;
        [SerializeField] AudioSource failSound;

        /// <summary>Leviers actuellement abaissés (bit n = Qn).</summary>
        public readonly NetworkVariable<int> PulledMask = new NetworkVariable<int>(0);

        Action[] pullHandlers;
        Coroutine resetRoutine;

        public override void OnNetworkSpawn()
        {
            pullHandlers = new Action[levers.Length];
            for (int i = 0; i < levers.Length; i++)
            {
                int quadrant = levers[i].quadrant;
                pullHandlers[i] = () => OnLocalPull(quadrant);
                levers[i].lever.Pulled += pullHandlers[i];
            }
            PulledMask.OnValueChanged += OnMaskChanged;
            objective.StateChanged += OnObjectiveChanged;
            ApplyLevers();
        }

        public override void OnNetworkDespawn()
        {
            for (int i = 0; i < levers.Length; i++)
                if (levers[i].lever != null) levers[i].lever.Pulled -= pullHandlers[i];
            PulledMask.OnValueChanged -= OnMaskChanged;
            objective.StateChanged -= OnObjectiveChanged;
        }

        void OnLocalPull(int quadrant)
        {
            // Seul le levier de son propre module compte.
            if (LocalRig.Instance == null || LocalRig.Instance.Quadrant != quadrant) return;
            PullRpc(quadrant);
        }

        [Rpc(SendTo.Server)]
        void PullRpc(int quadrant)
        {
            if (objective.Solved.Value) return;

            bool first = PulledMask.Value == 0;
            PulledMask.Value |= 1 << quadrant;

            int required = RequiredMask();
            if ((PulledMask.Value & required) == required)
            {
                if (resetRoutine != null) StopCoroutine(resetRoutine);
                objective.ServerSolve();
                return;
            }
            if (first) resetRoutine = StartCoroutine(ResetAfterWindow());
        }

        int RequiredMask()
        {
            int active = GameStateManager.Instance != null ? GameStateManager.Instance.ActiveMask.Value : ~0;
            int required = 0;
            for (int i = 0; i < levers.Length; i++)
            {
                int bit = 1 << levers[i].quadrant;
                if ((active & bit) != 0) required |= bit;
            }
            return required;
        }

        IEnumerator ResetAfterWindow()
        {
            yield return new WaitForSeconds(syncWindow);
            resetRoutine = null;
            if (objective.Solved.Value) yield break;
            PulledMask.Value = 0;
            FailRpc();
        }

        [Rpc(SendTo.Everyone)]
        void FailRpc()
        {
            if (failSound != null) failSound.Play();
        }

        void OnMaskChanged(int previous, int current) => ApplyLevers();
        void OnObjectiveChanged(bool solved) => ApplyLevers();

        void ApplyLevers()
        {
            bool solved = objective.Solved.Value;
            int mask = PulledMask.Value;
            for (int i = 0; i < levers.Length; i++)
            {
                bool down = solved || (mask & (1 << levers[i].quadrant)) != 0;
                levers[i].lever.SetState(down, solved);
            }
        }
    }
}
