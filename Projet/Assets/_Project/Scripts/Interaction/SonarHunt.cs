using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 4 — « Triangulation du Léviathan » : la créature rôde sur une carte de 6 × 6 cases autour de la station.
    /// Chaque sonar (un par module, plus celui du réacteur au centre) mesure sa DISTANCE à la créature, en cases
    /// (déplacements horizontaux + verticaux). Chaque plongeur ne lit que la distance de SON sonar ; celle du réacteur est
    /// affichée à tous. En croisant les distances, l'équipe trouve la case et y tire le leurre (lettre + chiffre, FEU).
    /// Le serveur tire une case dont la solution est UNIQUE pour les sonars présents. 3 tirs ratés : la créature se déplace.
    /// Boutons : 0..5 = lettres A-F, 6..11 = chiffres 1-6, 12 = FEU.
    /// </summary>
    public class SonarHunt : NetworkBehaviour, IPokeReceiver
    {
        public const int Size = 6;
        public const int FireIndex = 12;
        public const int MaxMisses = 3;

        /// <summary>Position des sonars sur la carte (colonne, rangée) : un coin par module, le réacteur au centre.</summary>
        public static readonly Vector2Int[] Sonars = { new Vector2Int(0, 0), new Vector2Int(5, 0), new Vector2Int(0, 5), new Vector2Int(5, 5) };
        public static readonly Vector2Int Reactor = new Vector2Int(2, 3);

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Une console par module (indexée par quadrant) : écran de distance, saisie, état.")]
        [SerializeField] TMP_Text[] distanceTexts;
        [SerializeField] TMP_Text[] entryTexts;
        [SerializeField] TMP_Text[] statusTexts;
        [SerializeField] AudioSource fireSound;
        [SerializeField] AudioSource missSound;
        [SerializeField] AudioSource hitSound;

        /// <summary>Case de la créature (rangée * 6 + colonne).</summary>
        public readonly NetworkVariable<int> Target = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Misses = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Shots = new NetworkVariable<int>(0);

        public PuzzleObjective Objective => objective;

        readonly int[] column = { -1, -1, -1, -1 };
        readonly int[] row = { -1, -1, -1, -1 };

        public static int Distance(int cell, Vector2Int sonar) => Mathf.Abs(cell % Size - sonar.x) + Mathf.Abs(cell / Size - sonar.y);
        public static string CellName(int cell) => (char)('A' + cell % Size) + (cell / Size + 1).ToString();

        public override void OnNetworkSpawn()
        {
            Target.OnValueChanged += OnIntChanged;
            Misses.OnValueChanged += OnMiss;
            objective.StateChanged += OnSolved;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Loc.Changed += Refresh;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            Target.OnValueChanged -= OnIntChanged;
            Misses.OnValueChanged -= OnMiss;
            objective.StateChanged -= OnSolved;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            Loc.Changed -= Refresh;
        }

        void OnIntChanged(int previous, int current) => Refresh();

        void OnPhaseChanged(GamePhase phase)
        {
            // Les sonars présents ne sont connus qu'au début de l'étage : la créature est tirée à ce moment-là.
            if (IsServer && phase == GamePhase.Playing && Target.Value < 0) Hide();
            Refresh();
        }

        static int ActiveMask
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                return state != null && state.IsSpawned && state.ActiveMask.Value != 0 ? state.ActiveMask.Value : 1;
            }
        }

        /// <summary>Case tirée au hasard dont les distances aux sonars présents (et au réacteur) désignent elle seule.</summary>
        void Hide()
        {
            int active = ActiveMask;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int cell = Random.Range(0, Size * Size);
                // Jamais sur un sonar (distance 0 : la réponse serait donnée).
                if (cell == Target.Value || Distance(cell, Reactor) == 0 || OnSonar(cell) || !Unique(cell, active)) continue;
                Target.Value = cell;
                return;
            }
            Target.Value = Random.Range(0, Size * Size);
        }

        static bool OnSonar(int cell)
        {
            foreach (Vector2Int sonar in Sonars)
                if (Distance(cell, sonar) == 0) return true;
            return false;
        }

        static bool Unique(int cell, int active)
        {
            for (int other = 0; other < Size * Size; other++)
            {
                if (other == cell || Distance(other, Reactor) != Distance(cell, Reactor)) continue;
                bool same = true;
                for (int q = 0; q < 4 && same; q++)
                    if ((active & (1 << q)) != 0 && Distance(other, Sonars[q]) != Distance(cell, Sonars[q])) same = false;
                if (same) return false;
            }
            return true;
        }

        public void Pressed(int index)
        {
            int q = LocalRig.Instance != null ? LocalRig.Instance.Quadrant : 0;
            if (!IsSpawned || objective.Solved.Value || !PlayArea.IsValid(q)) return;
            if (index < Size) column[q] = index;
            else if (index < 2 * Size) row[q] = index - Size;
            else if (index == FireIndex && column[q] >= 0 && row[q] >= 0)
            {
                if (fireSound != null) fireSound.Play();
                FireRpc(row[q] * Size + column[q]);
                column[q] = row[q] = -1;
            }
            Refresh();
        }

        /// <summary>Appui depuis la console d'un quadrant donné (tests automatiques, pupitre).</summary>
        public void PressedFrom(int quadrant, int index)
        {
            if (!IsSpawned || objective.Solved.Value) return;
            if (index < Size) column[quadrant] = index;
            else if (index < 2 * Size) row[quadrant] = index - Size;
            else if (index == FireIndex && column[quadrant] >= 0 && row[quadrant] >= 0)
            {
                FireRpc(row[quadrant] * Size + column[quadrant]);
                column[quadrant] = row[quadrant] = -1;
            }
            Refresh();
        }

        [Rpc(SendTo.Server)]
        void FireRpc(int cell)
        {
            if (objective.Solved.Value || Target.Value < 0) return;
            Shots.Value++;
            if (cell == Target.Value)
            {
                objective.ServerSolve();
                return;
            }
            Misses.Value++;
            // Trois tirs ratés : la créature, alertée, change de place.
            if (Misses.Value % MaxMisses == 0) Hide();
        }

        void OnMiss(int previous, int current)
        {
            if (current <= previous) return;
            if (missSound != null) missSound.Play();
            LocalRig.Pulse(0.8f, 0.4f);
            Refresh();
        }

        void OnSolved(bool solved)
        {
            if (solved && hitSound != null) hitSound.Play();
            Refresh();
        }

        void Refresh()
        {
            int target = Target.Value;
            for (int q = 0; q < 4; q++)
            {
                if (q < distanceTexts.Length && distanceTexts[q] != null)
                {
                    var text = new StringBuilder();
                    if (target < 0) text.Append(Loc.T("Sonar en attente…", "Sonar warming up…"));
                    else
                    {
                        text.Append(Loc.T("TON SONAR (", "YOUR SONAR (")).Append(CellName(Sonars[q].y * Size + Sonars[q].x)).Append(") : <b>")
                            .Append(Distance(target, Sonars[q])).Append(Loc.T(" cases</b>", " tiles</b>"));
                        text.Append(Loc.T("\nSONAR DU RÉACTEUR (", "\nREACTOR SONAR (")).Append(CellName(Reactor.y * Size + Reactor.x)).Append(") : <b>")
                            .Append(Distance(target, Reactor)).Append(Loc.T(" cases</b>", " tiles</b>"));
                    }
                    distanceTexts[q].text = text.ToString();
                }
                if (q < entryTexts.Length && entryTexts[q] != null)
                    entryTexts[q].text = (column[q] >= 0 ? ((char)('A' + column[q])).ToString() : "_") + (row[q] >= 0 ? (row[q] + 1).ToString() : "_");
                if (q < statusTexts.Length && statusTexts[q] != null)
                    statusTexts[q].text = objective.Solved.Value ? Loc.T("LEURRE TOUCHÉ : le Léviathan s'éloigne !", "DECOY HIT: the Leviathan swims away!")
                        : Misses.Value > 0 ? Loc.T("Raté : ", "Missed: ") + Misses.Value % MaxMisses + "/" + MaxMisses
                            + Loc.T(" avant qu'il ne se déplace.", " before it moves.")
                        : Loc.T("Distance = cases en ligne droite, sans diagonale.", "Distance = tiles in straight lines, no diagonals.");
            }
        }
    }
}
