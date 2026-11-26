using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 3 — « Contrepoids des ballasts » : chaque module a un plateau de pesée et 4 lests (1, 2, 2 et 5 kg = 10 kg).
    /// La charge à atteindre sur chaque plateau est inscrite au registre d'un AUTRE plongeur (<see cref="BallastRegister"/>).
    /// Entre partenaires présents, les cibles sont tirées pour que l'un ait besoin des lests de l'autre (total 20 kg) :
    /// il faut calculer et s'échanger des lests par le sas. Résolu quand TOUS les plateaux sont à leur charge en même temps.
    /// Pesée côté serveur, 2 fois par seconde, d'après la position des lests (aucune physique partagée).
    /// </summary>
    public class BallastScale : NetworkBehaviour
    {
        [SerializeField] PuzzleObjective objective;
        [Tooltip("Zone de pesée de chaque plateau (indexée par quadrant).")]
        [SerializeField] BoxCollider[] pans;
        [SerializeField] BallastWeight[] weights;
        [SerializeField] TMP_Text[] dials;
        [SerializeField] AudioSource successSound;

        /// <summary>Charge cible de chaque plateau (5 bits par quadrant).</summary>
        public readonly NetworkVariable<int> Targets = new NetworkVariable<int>(-1);
        /// <summary>Charge mesurée sur chaque plateau (5 bits par quadrant).</summary>
        public readonly NetworkVariable<int> Loads = new NetworkVariable<int>(0);

        public event System.Action Changed;
        public PuzzleObjective Objective => objective;
        public int Target(int quadrant) => Targets.Value < 0 ? -1 : (Targets.Value >> (quadrant * 5)) & 31;
        public int Load(int quadrant) => (Loads.Value >> (quadrant * 5)) & 31;
        public BoxCollider Pan(int quadrant) => pans[quadrant];

        float nextWeigh;

        public override void OnNetworkSpawn()
        {
            Targets.OnValueChanged += OnIntChanged;
            Loads.OnValueChanged += OnIntChanged;
            objective.StateChanged += OnSolved;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            GameStateManager.ModulesChanged += OnModulesChanged;
            Loc.Changed += Refresh;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            Targets.OnValueChanged -= OnIntChanged;
            Loads.OnValueChanged -= OnIntChanged;
            objective.StateChanged -= OnSolved;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            GameStateManager.ModulesChanged -= OnModulesChanged;
            Loc.Changed -= Refresh;
        }

        void OnIntChanged(int previous, int current) => Refresh();

        void OnSolved(bool solved)
        {
            if (solved && successSound != null) successSound.Play();
            Refresh();
        }

        void OnPhaseChanged(GamePhase phase)
        {
            // Les cibles dépendent des plongeurs présents : tirées au début de l'étage.
            if (IsServer && phase == GamePhase.Playing && Targets.Value < 0) DrawTargets();
            Refresh();
        }

        /// <summary>
        /// Un plongeur est parti (après le délai de grâce) : son partenaire ne peut plus recevoir ni envoyer de lests.
        /// Sa paire revient à 10 kg chacun, atteignable seul ; sinon l'étage serait impossible à finir.
        /// </summary>
        void OnModulesChanged()
        {
            if (IsServer && Targets.Value >= 0 && !objective.Solved.Value)
            {
                int active = Active, packed = Targets.Value;
                for (int pair = 0; pair < 4; pair += 2)
                {
                    bool both = (active & (1 << pair)) != 0 && (active & (1 << (pair + 1))) != 0;
                    if (both) continue;
                    packed &= ~(31 << (pair * 5) | 31 << ((pair + 1) * 5));
                    packed |= 10 << (pair * 5) | 10 << ((pair + 1) * 5);
                }
                if (packed != Targets.Value) Targets.Value = packed;
            }
            Refresh();
        }

        static int Active
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                return state != null && state.IsSpawned && state.ActiveMask.Value != 0 ? state.ActiveMask.Value : 1;
            }
        }

        void DrawTargets()
        {
            int active = Active, packed = 0;
            int[] uneven = { 6, 7, 8, 9, 11, 12, 13, 14 };
            for (int pair = 0; pair < 4; pair += 2)
            {
                bool a = (active & (1 << pair)) != 0, b = (active & (1 << (pair + 1))) != 0;
                int targetA = 10, targetB = 10;
                if (a && b)
                {
                    // Les deux partenaires sont là : il faudra s'échanger des lests (total commun de 20 kg).
                    targetA = uneven[Random.Range(0, uneven.Length)];
                    targetB = 20 - targetA;
                }
                packed |= targetA << (pair * 5) | targetB << ((pair + 1) * 5);
            }
            Targets.Value = packed;
        }

        void Update()
        {
            if (!IsServer || Time.unscaledTime < nextWeigh) return;
            nextWeigh = Time.unscaledTime + 0.5f;
            int loads = 0;
            for (int q = 0; q < pans.Length; q++)
            {
                if (pans[q] == null) continue;
                Bounds zone = pans[q].bounds;
                int load = 0;
                foreach (BallastWeight weight in weights)
                    if (weight != null && zone.Contains(weight.transform.position)) load += weight.Kilograms;
                loads |= Mathf.Min(load, 31) << (q * 5);
            }
            if (loads != Loads.Value) Loads.Value = loads;

            GameStateManager state = GameStateManager.Instance;
            if (objective.Solved.Value || Targets.Value < 0 || state == null || state.Phase.Value != GamePhase.Playing) return;
            int active = Active;
            for (int q = 0; q < 4; q++)
                if ((active & (1 << q)) != 0 && Load(q) != Target(q)) return;
            objective.ServerSolve();
        }

        void Refresh()
        {
            Changed?.Invoke();
            for (int q = 0; q < dials.Length; q++)
            {
                if (dials[q] == null) continue;
                dials[q].text = objective.Solved.Value ? Loc.T("ÉQUILIBRÉ", "BALANCED")
                    : Loc.T("CHARGE : ", "LOAD: ") + Load(q) + " kg";
            }
        }
    }
}
