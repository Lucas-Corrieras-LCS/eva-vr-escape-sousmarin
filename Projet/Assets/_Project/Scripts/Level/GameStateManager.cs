using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abysses.Core;
using Abysses.Network;

namespace Abysses.Level
{
    public enum GamePhase : byte
    {
        /// <summary>Dans les cabines, avant la plongée.</summary>
        Lobby,
        /// <summary>Étage en cours, énigmes à résoudre.</summary>
        Playing,
        /// <summary>Étage résolu : retour dans les cabines pour descendre.</summary>
        FloorSolved,
        /// <summary>Descente en ascenseur, chargement de l'étage suivant.</summary>
        Descending,
        /// <summary>Remontée d'urgence lancée : fin de partie.</summary>
        Finished,
    }

    /// <summary>
    /// Machine d'états de la partie, autorité serveur. Les clients réagissent aux NetworkVariables (aucun polling).
    /// Gère la descente : déchargement de l'étage courant, chargement additif du suivant, synchronisé par NetworkSceneManager.
    /// </summary>
    public class GameStateManager : NetworkBehaviour
    {
        [Tooltip("Scènes des étages, dans l'ordre de la descente. Elles doivent être dans les Build Settings.")]
        [SerializeField] string[] floorScenes = { "Etage1_Habitat", "Etage2_Labo", "Etage3_Coeur" };
        [Tooltip("Profondeur affichée pour chaque palier (surface puis étages), en mètres.")]
        [SerializeField] int[] floorDepths = { 0, 200, 1500, 4000 };
        [Tooltip("Durée minimale d'une descente en ascenseur (s).")]
        [SerializeField, Min(2f)] float descentDuration = 8f;

        public static GameStateManager Instance { get; private set; }

        /// <summary>Changement de phase, sur tous les clients.</summary>
        public static event Action<GamePhase> PhaseChanged;

        public readonly NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>(GamePhase.Lobby);
        /// <summary>0 = surface, puis 1..N.</summary>
        public readonly NetworkVariable<int> Floor = new NetworkVariable<int>(0);
        /// <summary>Quadrants occupés au début de l'étage (bit n = Qn).</summary>
        public readonly NetworkVariable<int> ActiveMask = new NetworkVariable<int>(0);
        /// <summary>Quadrants dont le joueur est dans sa cabine.</summary>
        public readonly NetworkVariable<int> ReadyMask = new NetworkVariable<int>(0);
        public readonly NetworkVariable<double> StartTime = new NetworkVariable<double>(0);
        public readonly NetworkVariable<double> EndTime = new NetworkVariable<double>(0);

        public int FloorCount => floorScenes.Length;
        public float DescentDuration => descentDuration;

        Scene loadedFloor;
        bool sceneEventDone;

        public int DepthOf(int floor) => floorDepths[Mathf.Clamp(floor, 0, floorDepths.Length - 1)];

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Phase.OnValueChanged += OnPhaseChanged;

            if (IsServer)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneEventCompleted;
                NetworkManager.SceneManager.OnUnloadEventCompleted += OnSceneEventCompleted;
                PuzzleObjective.ServerSolved += OnObjectiveSolved;
                NetworkPlayer.Despawned += OnPlayerLeft;
            }

            PhaseChanged?.Invoke(Phase.Value);
        }

        public override void OnNetworkDespawn()
        {
            Phase.OnValueChanged -= OnPhaseChanged;
            if (IsServer)
            {
                if (NetworkManager != null && NetworkManager.SceneManager != null)
                {
                    NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneEventCompleted;
                    NetworkManager.SceneManager.OnUnloadEventCompleted -= OnSceneEventCompleted;
                }
                PuzzleObjective.ServerSolved -= OnObjectiveSolved;
                NetworkPlayer.Despawned -= OnPlayerLeft;
            }
            if (Instance == this) Instance = null;
        }

        void OnPhaseChanged(GamePhase previous, GamePhase current) => PhaseChanged?.Invoke(current);

        // ---------- Requêtes des clients ----------

        /// <summary>Lance la plongée depuis la surface (bouton du menu, n'importe quel joueur).</summary>
        [Rpc(SendTo.Server)]
        public void RequestStartRpc()
        {
            if (Phase.Value != GamePhase.Lobby) return;
            StartTime.Value = NetworkManager.ServerTime.Time;
            StartCoroutine(DescendTo(1));
        }

        /// <summary>Le joueur local signale s'il est dans sa cabine.</summary>
        [Rpc(SendTo.Server)]
        public void SetReadyRpc(int quadrant, bool ready)
        {
            if (!PlayArea.IsValid(quadrant)) return;
            int bit = 1 << quadrant;
            ReadyMask.Value = ready ? ReadyMask.Value | bit : ReadyMask.Value & ~bit;
            TryStartDescent();
        }

        // ---------- Logique serveur ----------

        void OnObjectiveSolved(PuzzleObjective objective)
        {
            if (Phase.Value != GamePhase.Playing || !PuzzleObjective.AllSolved(Floor.Value)) return;

            if (Floor.Value >= FloorCount)
            {
                EndTime.Value = NetworkManager.ServerTime.Time;
                Phase.Value = GamePhase.Finished;
            }
            else
            {
                Phase.Value = GamePhase.FloorSolved;
                TryStartDescent();
            }
        }

        void TryStartDescent()
        {
            if (Phase.Value != GamePhase.FloorSolved) return;
            int occupied = NetworkPlayer.OccupiedQuadrantMask();
            if (occupied == 0 || (ReadyMask.Value & occupied) != occupied) return;
            StartCoroutine(DescendTo(Floor.Value + 1));
        }

        void OnPlayerLeft(NetworkPlayer player)
        {
            // Un joueur part en cours de partie : son module ne doit pas bloquer l'équipe.
            int occupied = NetworkPlayer.OccupiedQuadrantMask();
            ActiveMask.Value = occupied;
            ReadyMask.Value &= occupied;
            if (Phase.Value == GamePhase.Playing)
            {
                PuzzleObjective.ServerAutoSolveInactive(Floor.Value, occupied);
                OnObjectiveSolved(null);
            }
            TryStartDescent();
        }

        IEnumerator DescendTo(int floor)
        {
            Phase.Value = GamePhase.Descending;
            ReadyMask.Value = 0;
            float earliestArrival = Time.unscaledTime + descentDuration;

            if (loadedFloor.IsValid() && loadedFloor.isLoaded)
            {
                sceneEventDone = false;
                if (NetworkManager.SceneManager.UnloadScene(loadedFloor) == SceneEventProgressStatus.Started)
                    yield return new WaitUntil(() => sceneEventDone);
            }

            string sceneName = floorScenes[floor - 1];
            sceneEventDone = false;
            SceneEventProgressStatus status = NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[Abysses] Chargement de {sceneName} impossible : {status}. Scène absente des Build Settings ?");
                yield break;
            }
            yield return new WaitUntil(() => sceneEventDone);
            loadedFloor = SceneManager.GetSceneByName(sceneName);

            while (Time.unscaledTime < earliestArrival) yield return null;

            Floor.Value = floor;
            ActiveMask.Value = NetworkPlayer.OccupiedQuadrantMask();
            PuzzleObjective.ServerAutoSolveInactive(floor, ActiveMask.Value);
            Phase.Value = GamePhase.Playing;
            OnObjectiveSolved(null);
        }

        void OnSceneEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            sceneEventDone = true;
        }
    }
}
