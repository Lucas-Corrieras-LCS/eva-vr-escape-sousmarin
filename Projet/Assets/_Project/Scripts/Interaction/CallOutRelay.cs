using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;

namespace Abysses.Interaction
{
    /// <summary>
    /// Boucle de jeu « relais d'appels » (surtension, fuites, barres de contrôle selon l'étage), autorité serveur.
    /// À chaque appel, un symbole s'affiche chez un joueur (l'appelant) et le bon bouton est chez un AUTRE (la cible) :
    /// l'appelant décrit le symbole (« le poisson à trois traits »), la cible appuie avant la fin du temps.
    /// Manches de plus en plus longues et rapides ; une erreur ou un retard relance la manche.
    /// Les rôles changent à chaque appel : à 3 ou 4, tout le monde décrit et tout le monde agit.
    /// Seul (repli mono-joueur), le joueur est à la fois appelant et cible.
    /// </summary>
    public class CallOutRelay : NetworkBehaviour
    {
        public const int Symbols = 6;

        public enum Result : byte { None, Success, Wrong, Timeout, RoundDone }

        [SerializeField] PuzzleObjective objective;
        [SerializeField, Min(1)] int rounds = 3;
        [Tooltip("Appels réussis d'affilée pour finir la première manche (+1 à chaque manche).")]
        [SerializeField, Min(1)] int firstRoundCalls = 2;
        [SerializeField, Min(3f)] float firstRoundSeconds = 12f;
        [SerializeField, Min(3f)] float lastRoundSeconds = 7f;

        /// <summary>0 = pas commencé, 1..rounds = en cours, au-delà = terminé.</summary>
        public readonly NetworkVariable<int> Round = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Streak = new NetworkVariable<int>(0);
        /// <summary>-1 = aucun appel ; sinon cible | appelant &lt;&lt; 2 | symbole &lt;&lt; 4.</summary>
        public readonly NetworkVariable<int> Call = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<double> Deadline = new NetworkVariable<double>(0);
        public readonly NetworkVariable<float> Duration = new NetworkVariable<float>(1f);
        /// <summary>Dernier résultat (compteur * 8 + <see cref="Result"/>) : chaque changement déclenche le retour visuel et sonore.</summary>
        public readonly NetworkVariable<int> LastResult = new NetworkVariable<int>(0);

        public event Action Changed;

        public int Rounds => rounds;
        public PuzzleObjective Objective => objective;
        public bool Started => Round.Value > 0;
        public bool Finished => objective != null && objective.Solved.Value;
        public bool HasCall => Call.Value >= 0;
        public int Target => Call.Value & 3;
        public int Caller => (Call.Value >> 2) & 3;
        public int Symbol => Call.Value >> 4;
        public int CallsNeeded(int round) => firstRoundCalls + Mathf.Max(0, round - 1);
        public Result LastKind => (Result)(LastResult.Value & 7);

        int lastTarget = -1;
        bool pausing;

        public static string ColorName(int quadrant)
        {
            switch (quadrant)
            {
                case 0: return Loc.T("ORANGE", "ORANGE");
                case 1: return Loc.T("CYAN", "CYAN");
                case 2: return Loc.T("VERT", "GREEN");
                case 3: return Loc.T("MAGENTA", "MAGENTA");
                default: return "?";
            }
        }

        public override void OnNetworkSpawn()
        {
            Round.OnValueChanged += OnIntChanged;
            Streak.OnValueChanged += OnIntChanged;
            Call.OnValueChanged += OnIntChanged;
            LastResult.OnValueChanged += OnIntChanged;
            objective.StateChanged += OnSolvedChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Round.OnValueChanged -= OnIntChanged;
            Streak.OnValueChanged -= OnIntChanged;
            Call.OnValueChanged -= OnIntChanged;
            LastResult.OnValueChanged -= OnIntChanged;
            objective.StateChanged -= OnSolvedChanged;
        }

        void OnIntChanged(int previous, int current) => Changed?.Invoke();
        void OnSolvedChanged(bool solved) => Changed?.Invoke();

        // ---------- Requêtes des panneaux ----------

        [Rpc(SendTo.Server)]
        public void StartRpc()
        {
            if (Started || Finished || !Playing) return;
            Round.Value = 1;
            Streak.Value = 0;
            NextCall();
        }

        [Rpc(SendTo.Server)]
        public void SubmitRpc(int quadrant, int symbol)
        {
            if (!HasCall || pausing || Finished || quadrant != Target) return;
            if (symbol != Symbol)
            {
                Fail(Result.Wrong);
                return;
            }

            Streak.Value++;
            if (Streak.Value < CallsNeeded(Round.Value))
            {
                Report(Result.Success);
                StartCoroutine(Pause(0.9f));
                return;
            }

            Streak.Value = 0;
            if (Round.Value >= rounds)
            {
                Call.Value = -1;
                Round.Value = rounds + 1;
                Report(Result.RoundDone);
                objective.ServerSolve();
                return;
            }
            Round.Value++;
            Report(Result.RoundDone);
            StartCoroutine(Pause(2.5f));
        }

        // ---------- Logique serveur ----------

        bool Playing
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                return state != null && state.Phase.Value == GamePhase.Playing;
            }
        }

        void Update()
        {
            if (!IsServer || !HasCall || pausing || Finished) return;
            if (!Playing)
            {
                // Étage quitté ou partie relancée : on remet le relais à zéro.
                Call.Value = -1;
                return;
            }
            if (NetworkManager.ServerTime.Time > Deadline.Value) Fail(Result.Timeout);
        }

        void Fail(Result kind)
        {
            Streak.Value = 0;
            Report(kind);
            StartCoroutine(Pause(2.2f));
        }

        void Report(Result kind) => LastResult.Value = ((LastResult.Value >> 3) + 1) * 8 + (int)kind;

        IEnumerator Pause(float seconds)
        {
            pausing = true;
            Call.Value = -1;
            yield return new WaitForSeconds(seconds);
            pausing = false;
            if (!Finished && Playing) NextCall();
        }

        void NextCall()
        {
            int active = GameStateManager.Instance != null ? GameStateManager.Instance.ActiveMask.Value : 0;
            if (active == 0) active = NetworkPlayer.OccupiedQuadrantMask();
            if (active == 0) active = 1;

            int count = 0;
            Span<int> players = stackalloc int[PlayArea.QuadrantCount];
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
                if ((active & (1 << q)) != 0) players[count++] = q;

            // Cible tirée au hasard, rarement deux fois de suite ; l'appelant est toujours un autre joueur (sauf en solo).
            int target = players[UnityEngine.Random.Range(0, count)];
            if (count > 1 && target == lastTarget) target = players[UnityEngine.Random.Range(0, count)];
            int caller = target;
            if (count > 1)
                while (caller == target) caller = players[UnityEngine.Random.Range(0, count)];
            lastTarget = target;

            int symbol = UnityEngine.Random.Range(0, Symbols);
            float t = rounds <= 1 ? 0f : (Round.Value - 1f) / (rounds - 1f);
            float seconds = Mathf.Lerp(firstRoundSeconds, lastRoundSeconds, t);
            GameStateManager state = GameStateManager.Instance;
            if (state != null && state.EasyMode.Value) seconds *= 1.5f;

            Duration.Value = seconds;
            Deadline.Value = NetworkManager.ServerTime.Time + seconds;
            Call.Value = target | caller << 2 | symbol << 4;
        }
    }
}
