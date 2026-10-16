using System;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Énigme « Vannes croisées » : la vanne de chaque joueur règle le manomètre du joueur SUIVANT
    /// dans l'ordre de la piste (Q0 → Q1 → Q3 → Q2 → Q0), en ne gardant que les modules occupés.
    /// Chacun voit la jauge de l'autre par un hublot (ou l'entend la décrire) : il faut se guider à voix haute.
    /// À 1 joueur, la vanne pilote sa propre jauge.
    /// </summary>
    public class PressureNetwork : NetworkBehaviour
    {
        [Serializable]
        public struct Station
        {
            [Range(0, 3)] public int quadrant;
            public ValveWheel valve;
            public PressureGauge gauge;
        }

        const int BitsPerRoute = 3;
        const int NoRoute = 7;
        static readonly int[] RingOrder = { 0, 1, 3, 2 };

        [SerializeField] Station[] stations;

        /// <summary>Pour chaque vanne (3 bits par quadrant) : quadrant de la jauge pilotée. -1 = pas encore calculé.</summary>
        public readonly NetworkVariable<int> Routing = new NetworkVariable<int>(-1);

        Action<float>[] handlers;
        Action<bool>[] lockHandlers;

        public override void OnNetworkSpawn()
        {
            handlers = new Action<float>[stations.Length];
            for (int i = 0; i < stations.Length; i++)
            {
                int quadrant = stations[i].quadrant;
                handlers[i] = value => OnLocalValve(quadrant, value);
                stations[i].valve.ValueChanged += handlers[i];
            }

            lockHandlers = new Action<bool>[stations.Length];
            for (int i = 0; i < stations.Length; i++)
            {
                lockHandlers[i] = _ => RefreshValveLocks();
                stations[i].gauge.Objective.StateChanged += lockHandlers[i];
            }
            Routing.OnValueChanged += OnRoutingChanged;

            if (IsServer)
            {
                GameStateManager.PhaseChanged += OnPhaseChanged;
                if (GameStateManager.Instance != null && GameStateManager.Instance.Phase.Value == GamePhase.Playing)
                    ComputeRouting();
            }
            RefreshValveLocks();
        }

        public override void OnNetworkDespawn()
        {
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i].valve != null) stations[i].valve.ValueChanged -= handlers[i];
                if (stations[i].gauge != null) stations[i].gauge.Objective.StateChanged -= lockHandlers[i];
            }
            Routing.OnValueChanged -= OnRoutingChanged;
            if (IsServer) GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Playing && Routing.Value < 0) ComputeRouting();
        }

        void ComputeRouting()
        {
            int active = GameStateManager.Instance.ActiveMask.Value;
            Span<int> order = stackalloc int[PlayArea.QuadrantCount];
            int count = 0;
            for (int i = 0; i < RingOrder.Length; i++)
                if ((active & (1 << RingOrder[i])) != 0) order[count++] = RingOrder[i];

            int routing = 0;
            for (int q = 0; q < PlayArea.QuadrantCount; q++) routing |= NoRoute << (q * BitsPerRoute);
            for (int i = 0; i < count; i++)
            {
                int from = order[i];
                int to = order[(i + 1) % count];
                routing &= ~(NoRoute << (from * BitsPerRoute));
                routing |= to << (from * BitsPerRoute);
            }
            Routing.Value = routing;
        }

        int RouteOf(int valveQuadrant)
        {
            if (Routing.Value < 0) return NoRoute;
            return (Routing.Value >> (valveQuadrant * BitsPerRoute)) & NoRoute;
        }

        void OnLocalValve(int quadrant, float value)
        {
            if (LocalRig.Instance == null || LocalRig.Instance.Quadrant != quadrant) return;
            SetValveRpc(quadrant, value);
        }

        [Rpc(SendTo.Server)]
        void SetValveRpc(int valveQuadrant, float value)
        {
            int target = RouteOf(valveQuadrant);
            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i].quadrant != target) continue;
                stations[i].gauge.ServerSetPressure(Mathf.Clamp01(value));
                return;
            }
        }

        void OnRoutingChanged(int previous, int current) => RefreshValveLocks();

        /// <summary>Une vanne se bloque quand la jauge qu'elle pilote est stabilisée.</summary>
        void RefreshValveLocks()
        {
            for (int i = 0; i < stations.Length; i++)
            {
                int target = RouteOf(stations[i].quadrant);
                bool locked = false;
                for (int j = 0; j < stations.Length; j++)
                    if (stations[j].quadrant == target) locked = stations[j].gauge.Objective.Solved.Value;
                stations[i].valve.SetLocked(locked);
            }
        }
    }
}
