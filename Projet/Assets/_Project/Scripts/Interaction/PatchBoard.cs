using System.Text;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 4 — « Câblage du standard », énigme à trois rôles (trois « langues ») :
    /// - l'OPÉRATEUR a le tableau : 4 fiches et 8 prises, repérées par des NUMÉROS de circuit ;
    /// - la LISTE (chez un autre plongeur) donne les 4 liaisons en COULEURS (« ROUGE → TURQUOISE ») ;
    /// - la LÉGENDE (chez un troisième, ou chez l'opérateur à 2 joueurs) traduit chaque couleur en numéro de circuit.
    /// L'info doit passer par plusieurs bouches. 4 liaisons faites : vérification ; une erreur débranche tout.
    /// Rôles répartis par <see cref="Routing.FragmentHolder"/>. Voir <see cref="PatchStation"/> pour l'affichage par module.
    /// </summary>
    public class PatchBoard : NetworkBehaviour
    {
        public const int Sources = 4;
        public const int Sockets = 8;

        static readonly string[] ColorsFr = { "ROUGE", "BLEU", "VERT", "JAUNE", "BLANC", "NOIR", "ORANGE", "VIOLET", "ROSE", "GRIS", "MARRON", "TURQUOISE" };
        static readonly string[] ColorsEn = { "RED", "BLUE", "GREEN", "YELLOW", "WHITE", "BLACK", "ORANGE", "PURPLE", "PINK", "GREY", "BROWN", "TURQUOISE" };

        [SerializeField] PuzzleObjective objective;
        [SerializeField] int shift = 1;
        [SerializeField] AudioSource plugSound;
        [SerializeField] AudioSource errorSound;
        [SerializeField] AudioSource successSound;

        public readonly NetworkVariable<int> Seed = new NetworkVariable<int>(-1);
        /// <summary>Prise branchée sur chaque fiche : 4 bits par fiche (0 = libre, sinon prise + 1).</summary>
        public readonly NetworkVariable<int> Links = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Errors = new NetworkVariable<int>(0);

        public event System.Action Changed;
        public PuzzleObjective Objective => objective;

        int[] colors;   // 4 couleurs de fiches puis 8 couleurs de prises (indices de couleur distincts)
        int[] numbers;  // numéro de circuit de chaque couleur utilisée (même ordre)
        int[] target;   // prise attendue pour chaque fiche

        public enum Role { None, Operator, List, Legend, OperatorAndLegend }

        public int Operator(int active) => Routing.FragmentHolder(0, active, shift);
        public int ListHolder(int active) => Routing.FragmentHolder(1, active, shift);
        public int LegendHolder(int active) => Count(active) >= 3 ? Routing.FragmentHolder(2, active, shift) : Operator(active);

        public Role RoleOf(int quadrant, int active)
        {
            bool op = Operator(active) == quadrant, legend = LegendHolder(active) == quadrant;
            if (op && legend) return Role.OperatorAndLegend;
            if (op) return Role.Operator;
            if (ListHolder(active) == quadrant) return Role.List;
            return legend ? Role.Legend : Role.None;
        }

        static int Count(int mask)
        {
            int n = 0;
            for (; mask != 0; mask &= mask - 1) n++;
            return n;
        }

        void Build(int seed)
        {
            if (seed < 0) return;
            var random = new System.Random(seed);
            colors = Shuffle(random, ColorsFr.Length, Sources + Sockets);
            numbers = new int[Sources + Sockets];
            var used = new bool[100];
            for (int i = 0; i < numbers.Length; i++)
            {
                int n;
                do n = random.Next(10, 100);
                while (used[n]);
                used[n] = true;
                numbers[i] = n;
            }
            target = Shuffle(random, Sockets, Sources);
        }

        static int[] Shuffle(System.Random random, int range, int count)
        {
            var all = new int[range];
            for (int i = 0; i < range; i++) all[i] = i;
            for (int i = range - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            var result = new int[count];
            System.Array.Copy(all, result, count);
            return result;
        }

        public bool Ready => colors != null;
        public int SourceNumber(int source) => numbers[source];
        public int SocketNumber(int socket) => numbers[Sources + socket];
        public int Expected(int source) => target[source];
        public int LinkOf(int source) => ((Links.Value >> (source * 4)) & 15) - 1;

        static string C(int color) => Loc.T(ColorsFr[color], ColorsEn[color]);

        public string ListText()
        {
            if (!Ready) return string.Empty;
            var text = new StringBuilder(Loc.T("<b>STANDARD — LIAISONS À ÉTABLIR</b>\n\n", "<b>SWITCHBOARD — LINKS TO MAKE</b>\n\n"));
            for (int i = 0; i < Sources; i++)
                text.Append(i + 1).Append(". ").Append(C(colors[i])).Append("  →  ").Append(C(colors[Sources + target[i]])).Append('\n');
            text.Append(Loc.T("\nL'opérateur ne voit que des numéros :\nil faut la légende des couleurs.", "\nThe operator only sees numbers:\nthe colour legend is needed."));
            return text.ToString();
        }

        public string LegendText()
        {
            if (!Ready) return string.Empty;
            var text = new StringBuilder(Loc.T("<b>LÉGENDE DES CIRCUITS</b>\n", "<b>CIRCUIT LEGEND</b>\n"));
            // Ordre alphabétique des couleurs : la légende ne trahit pas quelles couleurs sont des fiches.
            var order = new int[colors.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => string.CompareOrdinal(C(colors[a]), C(colors[b])));
            for (int k = 0; k < order.Length; k++)
            {
                text.Append(C(colors[order[k]])).Append(" = ").Append(numbers[order[k]]);
                text.Append(k % 2 == 0 ? "      " : "\n");
            }
            return text.ToString();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) Seed.Value = Random.Range(1, int.MaxValue);
            Build(Seed.Value);
            Seed.OnValueChanged += OnSeedChanged;
            Links.OnValueChanged += OnLinksChanged;
            Errors.OnValueChanged += OnErrors;
            objective.StateChanged += OnSolved;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            Seed.OnValueChanged -= OnSeedChanged;
            Links.OnValueChanged -= OnLinksChanged;
            Errors.OnValueChanged -= OnErrors;
            objective.StateChanged -= OnSolved;
        }

        void OnSeedChanged(int previous, int current)
        {
            Build(current);
            Changed?.Invoke();
        }

        void OnLinksChanged(int previous, int current)
        {
            if (current != 0 && plugSound != null) plugSound.Play();
            Changed?.Invoke();
        }

        void OnErrors(int previous, int current)
        {
            if (current > previous && errorSound != null) errorSound.Play();
            Changed?.Invoke();
        }

        void OnSolved(bool solved)
        {
            if (solved && successSound != null) successSound.Play();
            Changed?.Invoke();
        }

        [Rpc(SendTo.Server)]
        public void LinkRpc(int source, int socket)
        {
            if (objective.Solved.Value || !Ready || source < 0 || source >= Sources || socket < 0 || socket >= Sockets) return;
            int links = Links.Value;
            // Une prise ne reçoit qu'une fiche : on débranche l'éventuelle fiche déjà dessus.
            for (int i = 0; i < Sources; i++)
                if (((links >> (i * 4)) & 15) == socket + 1) links &= ~(15 << (i * 4));
            links = (links & ~(15 << (source * 4))) | (socket + 1) << (source * 4);
            Links.Value = links;

            for (int i = 0; i < Sources; i++)
                if (((links >> (i * 4)) & 15) == 0) return;
            bool correct = true;
            for (int i = 0; i < Sources; i++)
                if (((links >> (i * 4)) & 15) - 1 != target[i]) correct = false;
            if (correct) objective.ServerSolve();
            else
            {
                Errors.Value++;
                Links.Value = 0;
            }
        }
    }
}
