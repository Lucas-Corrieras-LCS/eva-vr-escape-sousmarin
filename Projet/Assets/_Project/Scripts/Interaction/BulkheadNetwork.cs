using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 3 — « Cloisons étanches », énigme d'équipe sous pression :
    /// la coque des ballasts compte 6 compartiments (2 rangées × 3) reliés par 7 portes étanches. Une fuite remplit son
    /// compartiment, puis l'eau passe aux voisins par les portes ouvertes toutes les <see cref="spreadSeconds"/> s.
    /// La pression hydraulique ne permet que <see cref="MaxClosures"/> fermetures. Il faut isoler la fuite (toutes les portes
    /// entre l'eau et le sec fermées) avant que l'eau n'atteigne la salle des pompes ; sinon, nouvelle fuite.
    /// Le PLAN n'est affiché que chez un plongeur (<see cref="BulkheadDisplay"/>) ; les COMMANDES des portes sont réparties
    /// entre tous les modules : celui qui voit le plan dirige, les autres ferment.
    /// Le chrono ne part que quand celui qui a le plan déclenche l'alerte (l'équipe choisit son moment) ; après un échec,
    /// une nouvelle fuite attend une nouvelle alerte.
    /// </summary>
    public class BulkheadNetwork : NetworkBehaviour
    {
        public const int Columns = 3;
        public const int Rows = 2;
        public const int Compartments = Columns * Rows;
        public const int MaxClosures = 3;

        /// <summary>Portes : paires de compartiments (rangée * 3 + colonne). 4 horizontales puis 3 verticales.</summary>
        public static readonly Vector2Int[] Doors =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 2), new Vector2Int(3, 4), new Vector2Int(4, 5),
            new Vector2Int(0, 3), new Vector2Int(1, 4), new Vector2Int(2, 5),
        };

        [SerializeField] PuzzleObjective objective;
        [SerializeField, Min(5f)] float spreadSeconds = 15f;
        [Tooltip("Décalage du routage : module qui voit le plan, répartition des commandes.")]
        [SerializeField] int shift = 3;
        [SerializeField] AudioSource spreadSound;
        [SerializeField] AudioSource failSound;
        [SerializeField] AudioSource doorSound;

        /// <summary>Fuite (bits 0-2) et salle des pompes (bits 3-5).</summary>
        public readonly NetworkVariable<int> Layout = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Flooded = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Closed = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Failures = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> Running = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<double> NextSpread = new NetworkVariable<double>(0);

        public event System.Action Changed;

        public PuzzleObjective Objective => objective;
        public int Shift => shift;
        public int Leak => Layout.Value & 7;
        public int Pump => (Layout.Value >> 3) & 7;
        public int ClosuresLeft => MaxClosures - Bits(Closed.Value);
        public bool IsClosed(int door) => (Closed.Value & (1 << door)) != 0;
        public bool IsFlooded(int compartment) => (Flooded.Value & (1 << compartment)) != 0;

        /// <summary>Module qui affiche le plan (lisible de lui seul).</summary>
        public int MapHolder(int activeMask) => Routing.FragmentHolder(0, activeMask, shift);

        /// <summary>Module qui commande la porte <paramref name="door"/>.</summary>
        public int DoorHolder(int door, int activeMask) => Routing.FragmentHolder(door + 1, activeMask, shift);

        public override void OnNetworkSpawn()
        {
            if (IsServer) NewLeak();
            Layout.OnValueChanged += OnIntChanged;
            Flooded.OnValueChanged += OnFloodedChanged;
            Closed.OnValueChanged += OnClosedChanged;
            Failures.OnValueChanged += OnFailure;
            Running.OnValueChanged += OnRunningChanged;
            objective.StateChanged += OnSolved;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Layout.OnValueChanged -= OnIntChanged;
            Flooded.OnValueChanged -= OnFloodedChanged;
            Closed.OnValueChanged -= OnClosedChanged;
            Failures.OnValueChanged -= OnFailure;
            Running.OnValueChanged -= OnRunningChanged;
            objective.StateChanged -= OnSolved;
        }

        void OnRunningChanged(bool previous, bool current) => Changed?.Invoke();

        [Rpc(SendTo.Server)]
        public void StartRpc()
        {
            if (objective.Solved.Value || Running.Value || Layout.Value < 0) return;
            Running.Value = true;
            NextSpread.Value = NetworkManager.ServerTime.Time + Duration();
        }

        void OnIntChanged(int previous, int current) => Changed?.Invoke();
        void OnSolved(bool solved) => Changed?.Invoke();

        void OnFloodedChanged(int previous, int current)
        {
            if (Bits(current) > Bits(previous) && Bits(previous) > 0 && spreadSound != null) spreadSound.Play();
            Changed?.Invoke();
        }

        void OnClosedChanged(int previous, int current)
        {
            if (Bits(current) > Bits(previous) && doorSound != null) doorSound.Play();
            Changed?.Invoke();
        }

        void OnFailure(int previous, int current)
        {
            if (current > previous && failSound != null) failSound.Play();
            Changed?.Invoke();
        }

        [Rpc(SendTo.Server)]
        public void CloseRpc(int door)
        {
            if (objective.Solved.Value || !Running.Value || door < 0 || door >= Doors.Length || IsClosed(door) || ClosuresLeft <= 0) return;
            Closed.Value |= 1 << door;
            CheckContained();
        }

        void NewLeak()
        {
            // Fuite et pompe jamais voisines : il y a toujours le temps d'agir si l'équipe se parle.
            int leak, pump;
            do
            {
                leak = Random.Range(0, Compartments);
                pump = Random.Range(0, Compartments);
            }
            while (pump == leak || Adjacent(leak, pump));
            Layout.Value = leak | pump << 3;
            Running.Value = false;
            Closed.Value = 0;
            Flooded.Value = 1 << leak;
            NextSpread.Value = NetworkManager.ServerTime.Time + Duration();
        }

        float Duration()
        {
            GameStateManager state = GameStateManager.Instance;
            return state != null && state.EasyMode.Value ? spreadSeconds * 1.6f : spreadSeconds;
        }

        static bool Adjacent(int a, int b)
        {
            foreach (Vector2Int door in Doors)
                if (door.x == a && door.y == b || door.x == b && door.y == a) return true;
            return false;
        }

        void Update()
        {
            if (!IsServer || objective.Solved.Value || Layout.Value < 0) return;
            GameStateManager state = GameStateManager.Instance;
            if (state == null || state.Phase.Value != GamePhase.Playing || !Running.Value)
            {
                // Le chrono ne tourne que pendant l'étage.
                NextSpread.Value = NetworkManager.ServerTime.Time + Duration();
                return;
            }
            if (NetworkManager.ServerTime.Time < NextSpread.Value) return;

            int flooded = Flooded.Value;
            for (int i = 0; i < Doors.Length; i++)
            {
                if (IsClosed(i)) continue;
                Vector2Int door = Doors[i];
                if ((Flooded.Value & (1 << door.x)) != 0) flooded |= 1 << door.y;
                if ((Flooded.Value & (1 << door.y)) != 0) flooded |= 1 << door.x;
            }
            Flooded.Value = flooded;
            NextSpread.Value = NetworkManager.ServerTime.Time + Duration();
            if ((flooded & (1 << Pump)) != 0)
            {
                Failures.Value++;
                NewLeak();
                return;
            }
            CheckContained();
        }

        /// <summary>Résolu quand toutes les portes entre l'eau et le sec sont fermées et que la pompe est au sec.</summary>
        void CheckContained()
        {
            if (IsFlooded(Pump)) return;
            for (int i = 0; i < Doors.Length; i++)
            {
                Vector2Int door = Doors[i];
                if (IsFlooded(door.x) != IsFlooded(door.y) && !IsClosed(i)) return;
            }
            objective.ServerSolve();
        }

        static int Bits(int value)
        {
            int count = 0;
            for (; value != 0; value &= value - 1) count++;
            return count;
        }
    }
}
