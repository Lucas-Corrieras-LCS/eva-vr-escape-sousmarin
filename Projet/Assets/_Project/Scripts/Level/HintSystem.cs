using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Network;
using Abysses.Player;

namespace Abysses.Level
{
    /// <summary>
    /// IA de la station : quand l'équipe n'avance plus depuis quelques minutes, chaque module reçoit un indice
    /// sur sa prochaine énigme, de plus en plus précis. Le maître du jeu peut aussi en déclencher un, ou envoyer un message.
    /// Autorité serveur ; l'indice s'affiche sur la montre de plongée du joueur concerné (texte + carillon).
    /// </summary>
    public class HintSystem : NetworkBehaviour
    {
        [Tooltip("Minutes sans progrès avant un indice (mode normal / mode facile).")]
        [SerializeField, Min(0.5f)] float minutesNormal = 5f;
        [SerializeField, Min(0.5f)] float minutesEasy = 2f;
        [SerializeField, Min(1f)] float checkInterval = 5f;

        /// <summary>Texte à afficher au joueur local (indice ou message du maître du jeu).</summary>
        public static event Action<string> MessageReceived;

        public static HintSystem Instance { get; private set; }

        readonly Dictionary<PuzzleObjective, int> levels = new Dictionary<PuzzleObjective, int>();
        readonly HashSet<PuzzleObjective> chosen = new HashSet<PuzzleObjective>();
        float lastProgress;
        float nextCheck;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer) return;
            PuzzleObjective.ServerSolved += OnProgress;
            GameStateManager.PhaseChanged += OnPhaseChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (!IsServer) return;
            PuzzleObjective.ServerSolved -= OnProgress;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void OnProgress(PuzzleObjective objective) => lastProgress = Time.unscaledTime;

        void OnPhaseChanged(GamePhase phase)
        {
            lastProgress = Time.unscaledTime;
            if (phase == GamePhase.Lobby) levels.Clear();
        }

        void Update()
        {
            if (!IsServer || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;
            GameStateManager state = GameStateManager.Instance;
            if (state == null || state.Phase.Value != GamePhase.Playing) return;
            float minutes = state.EasyMode.Value ? minutesEasy : minutesNormal;
            if (Time.unscaledTime - lastProgress < minutes * 60f) return;
            lastProgress = Time.unscaledTime;
            GiveHints();
        }

        /// <summary>Pupitre : indice immédiat pour chaque module.</summary>
        [Rpc(SendTo.Server)]
        public void RequestHintRpc(RpcParams rpcParams = default)
        {
            if (!ConnectionManager.IsGameMaster(rpcParams.Receive.SenderClientId)) return;
            lastProgress = Time.unscaledTime;
            GiveHints();
        }

        /// <summary>Pupitre : message libre affiché à tous les plongeurs.</summary>
        [Rpc(SendTo.Server)]
        public void BroadcastMessageRpc(string message, RpcParams rpcParams = default)
        {
            if (!ConnectionManager.IsGameMaster(rpcParams.Receive.SenderClientId) || string.IsNullOrWhiteSpace(message)) return;
            ShowMessageRpc(message.Length > 160 ? message.Substring(0, 160) : message);
        }

        void GiveHints()
        {
            GameStateManager state = GameStateManager.Instance;
            if (state == null) return;
            chosen.Clear();
            int active = state.ActiveMask.Value;
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
            {
                if ((active & (1 << q)) == 0) continue;
                PuzzleObjective objective = NextObjective(state.Floor.Value, q);
                if (objective == null || !chosen.Add(objective)) continue;
                levels.TryGetValue(objective, out int level);
                levels[objective] = level + 1;
                ShowHintRpc(objective, level);
            }
        }

        /// <summary>Prochaine énigme d'un module : d'abord celles du module, puis les communes.</summary>
        static PuzzleObjective NextObjective(int floor, int quadrant)
        {
            IReadOnlyList<PuzzleObjective> all = PuzzleObjective.Registered;
            PuzzleObjective shared = null;
            for (int i = 0; i < all.Count; i++)
            {
                PuzzleObjective o = all[i];
                if (o.Floor != floor || o.Solved.Value || o.HintCount == 0) continue;
                if (o.Quadrant == quadrant) return o;
                if (o.Quadrant < 0 && shared == null) shared = o;
            }
            return shared;
        }

        [Rpc(SendTo.Everyone)]
        void ShowHintRpc(NetworkBehaviourReference objectiveReference, int level)
        {
            if (!objectiveReference.TryGet(out PuzzleObjective objective)) return;
            // Chaque joueur ne reçoit que les indices de son module (ou communs) ; le pupitre voit tout.
            bool forMe = NetworkPlayer.Local == null || LocalRig.IsLocalOrShared(objective.Quadrant);
            if (!forMe) return;
            MessageReceived?.Invoke(Loc.T("IA de la station — ", "Station AI — ") + objective.Hint(level));
        }

        [Rpc(SendTo.Everyone)]
        void ShowMessageRpc(string message)
        {
            MessageReceived?.Invoke(Loc.T("Surface — ", "Surface — ") + message);
        }
    }
}
