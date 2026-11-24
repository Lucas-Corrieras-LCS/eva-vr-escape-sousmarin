using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 3 — « Labyrinthe électrifié » : une grille de dalles au sol de la cale (0,5 m), dont certaines sont sous tension.
    /// Le plongeur du module ne voit rien (toutes les dalles se ressemblent, sous l'eau) : un autre plongeur voit la carte et
    /// le point qui le suit sur son écran de surveillance (<see cref="MazeMonitor"/>, routage tournant), et le guide à la voix.
    /// On part de la dalle de départ ; une dalle sous tension = décharge, retour au départ ; atteindre la vanne de purge résout.
    /// On marche vraiment (aucun déplacement artificiel). Détection côté serveur, à partir de la tête synchronisée du joueur.
    /// </summary>
    public class ShockMaze : NetworkBehaviour
    {
        public const int Columns = 6;
        public const int Rows = 4;
        public const float Tile = 0.5f;

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Coin de la grille : X local = colonnes (départ en colonne 0), Z local = rangées.")]
        [SerializeField] Transform origin;
        [Tooltip("Vrai si les rangées montent vers le mur du voisin (sinon vers l'océan) : légendes de l'écran.")]
        [SerializeField] bool rowsTowardNeighbor;
        [Tooltip("Marques placées sur la dalle de départ et sur la vanne d'arrivée (selon le tirage).")]
        [SerializeField] Transform startMarker;
        [SerializeField] Transform goalMarker;
        [SerializeField] AudioSource shockSound;
        [SerializeField] AudioSource armSound;
        [SerializeField] ParticleSystem sparks;

        public readonly NetworkVariable<int> Seed = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> Armed = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<int> Shocks = new NetworkVariable<int>(0);

        public PuzzleObjective Objective => objective;
        public int Quadrant => objective.Quadrant;
        public Transform Origin => origin;
        public bool RowsTowardNeighbor => rowsTowardNeighbor;
        public event System.Action Changed;

        bool[] safe;
        int start, goal;
        int lastCell = -1;

        /// <summary>Dalles sûres (index = rangée * Columns + colonne) : un chemin de la colonne 0 à la dernière colonne.</summary>
        public bool[] Safe => safe;
        public int StartCell => start;
        public int GoalCell => goal;

        void Build(int seed)
        {
            safe = new bool[Columns * Rows];
            if (seed < 0) return;
            var random = new System.Random(seed);
            int row = random.Next(Rows);
            start = row * Columns;
            safe[start] = true;
            int column = 0;
            // Marche aléatoire vers l'avant, avec des détours verticaux (jamais deux pas en arrière).
            while (column < Columns - 1)
            {
                int move = random.Next(3);
                if (move == 0 && row > 0 && !safe[(row - 1) * Columns + column]) row--;
                else if (move == 1 && row < Rows - 1 && !safe[(row + 1) * Columns + column]) row++;
                else column++;
                safe[row * Columns + column] = true;
            }
            goal = row * Columns + column;
            if (startMarker != null) startMarker.position = CellCenter(start);
            if (goalMarker != null) goalMarker.position = CellCenter(goal);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) Seed.Value = Random.Range(1, int.MaxValue);
            Seed.OnValueChanged += OnSeedChanged;
            Armed.OnValueChanged += OnArmedChanged;
            Shocks.OnValueChanged += OnShock;
            objective.StateChanged += OnSolved;
            Build(Seed.Value);
        }

        public override void OnNetworkDespawn()
        {
            Seed.OnValueChanged -= OnSeedChanged;
            Armed.OnValueChanged -= OnArmedChanged;
            Shocks.OnValueChanged -= OnShock;
            objective.StateChanged -= OnSolved;
        }

        void OnSeedChanged(int previous, int current)
        {
            Build(current);
            Changed?.Invoke();
        }

        void OnArmedChanged(bool previous, bool current)
        {
            if (current && armSound != null && LocalRig.IsLocalOrShared(Quadrant)) armSound.Play();
            Changed?.Invoke();
        }

        void OnSolved(bool solved) => Changed?.Invoke();

        void OnShock(int previous, int current)
        {
            if (current <= previous) return;
            if (shockSound != null) shockSound.Play();
            if (sparks != null) sparks.Play();
            if (LocalRig.IsLocalOrShared(Quadrant)) LocalRig.Pulse(1f, 0.5f);
            Changed?.Invoke();
        }

        /// <summary>Dalle sous un point du monde (-1 hors de la grille).</summary>
        public int CellAt(Vector3 world)
        {
            Vector3 local = origin.InverseTransformPoint(world);
            int column = Mathf.FloorToInt(local.x / Tile), row = Mathf.FloorToInt(local.z / Tile);
            return column < 0 || column >= Columns || row < 0 || row >= Rows ? -1 : row * Columns + column;
        }

        /// <summary>Centre d'une dalle, en coordonnées monde (au sol).</summary>
        public Vector3 CellCenter(int cell) => origin.TransformPoint(new Vector3((cell % Columns + 0.5f) * Tile, 0f, (cell / Columns + 0.5f) * Tile));

        public static NetworkPlayer PlayerIn(int quadrant)
        {
            foreach (NetworkPlayer player in NetworkPlayer.All)
                if (player != null && player.Quadrant.Value == quadrant) return player;
            return null;
        }

        void Update()
        {
            if (!IsServer || objective.Solved.Value || safe == null || Seed.Value < 0) return;
            GameStateManager state = GameStateManager.Instance;
            if (state == null || state.Phase.Value != GamePhase.Playing) return;
            NetworkPlayer player = PlayerIn(Quadrant);
            if (player == null || player.Head == null) return;
            int cell = CellAt(player.Head.position);
            if (cell == lastCell) return;
            lastCell = cell;
            // Sortir de la grille désarme : impossible de contourner le labyrinthe pour atteindre la vanne.
            if (cell < 0)
            {
                if (Armed.Value) Armed.Value = false;
                return;
            }
            if (cell == start)
            {
                if (!Armed.Value) Armed.Value = true;
                return;
            }
            if (!Armed.Value) return;
            if (!safe[cell])
            {
                Armed.Value = false;
                Shocks.Value++;
                return;
            }
            if (cell == goal) objective.ServerSolve();
        }
    }
}
