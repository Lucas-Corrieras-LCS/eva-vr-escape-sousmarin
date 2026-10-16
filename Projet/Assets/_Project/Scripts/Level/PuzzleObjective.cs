using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;

namespace Abysses.Level
{
    /// <summary>
    /// Un objectif d'énigme (code saisi, fusible posé, leviers synchronisés…). Son état « résolu » est décidé par le serveur.
    /// Un étage est terminé quand tous ses objectifs sont résolus.
    /// Les objectifs rattachés à un module inoccupé sont résolus automatiquement (adaptation 2–4 joueurs / solo).
    /// </summary>
    public class PuzzleObjective : NetworkBehaviour
    {
        [SerializeField, Range(1, 3)] int floor = 1;
        [Tooltip("Module où se trouve l'objectif (0..3). -1 = objectif commun, jamais résolu automatiquement.")]
        [SerializeField, Range(-1, 3)] int quadrant;
        [SerializeField] string label = "Objectif";

        public readonly NetworkVariable<bool> Solved = new NetworkVariable<bool>(false);

        public int Floor => floor;
        public int Quadrant => quadrant;
        public string Label => label;

        /// <summary>Changement d'état de CET objectif (visuels locaux).</summary>
        public event Action<bool> StateChanged;

        /// <summary>Côté serveur : un objectif vient d'être résolu (agrégé par le GameStateManager).</summary>
        public static event Action<PuzzleObjective> ServerSolved;

        static readonly List<PuzzleObjective> All = new List<PuzzleObjective>(32);

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Solved.OnValueChanged += OnSolvedChanged;
            StateChanged?.Invoke(Solved.Value);
        }

        public override void OnNetworkDespawn()
        {
            Solved.OnValueChanged -= OnSolvedChanged;
            All.Remove(this);
        }

        void OnSolvedChanged(bool previous, bool current)
        {
            StateChanged?.Invoke(current);
            if (IsServer && current) ServerSolved?.Invoke(this);
        }

        public void ServerSolve()
        {
            if (!IsServer || Solved.Value) return;
            Solved.Value = true;
        }

        /// <summary>Vrai si tous les objectifs de l'étage sont résolus (vrai aussi s'il n'y en a aucun).</summary>
        public static bool AllSolved(int floor)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].floor == floor && !All[i].Solved.Value) return false;
            return true;
        }

        /// <summary>Serveur : résout les objectifs des modules sans joueur.</summary>
        public static void ServerAutoSolveInactive(int floor, int activeQuadrantMask)
        {
            for (int i = 0; i < All.Count; i++)
            {
                PuzzleObjective objective = All[i];
                if (objective.floor != floor || !PlayArea.IsValid(objective.quadrant)) continue;
                if ((activeQuadrantMask & (1 << objective.quadrant)) == 0) objective.ServerSolve();
            }
        }
    }
}
