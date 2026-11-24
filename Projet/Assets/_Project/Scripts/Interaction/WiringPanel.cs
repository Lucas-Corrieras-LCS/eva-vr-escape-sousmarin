using System.Collections;
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
    /// Étage 1 — « Manuel de désamorçage » : tableau de dérivation en panne, 4 à 6 fils de couleur, un voyant et un numéro
    /// de série tirés au hasard. Le MANUEL (règles conditionnelles propres à CE tableau) s'affiche chez un autre plongeur
    /// (<see cref="WiringManual"/>, routage tournant) : on décrit le tableau, l'autre trouve la règle, on coupe le fil.
    /// 3 configurations d'affilée à réussir ; un mauvais fil = court-circuit, retour à la première.
    /// Le serveur tire les graines et valide ; la configuration et les règles se recalculent à l'identique chez chacun.
    /// </summary>
    public class WiringPanel : NetworkBehaviour, IPokeReceiver
    {
        public const int Slots = 6;
        public const int Rounds = 3;
        const int RuleCount = 4;

        static readonly string[] ColorsFr = { "ROUGE", "BLEU", "JAUNE", "BLANC", "NOIR" };
        static readonly string[] ColorsEn = { "RED", "BLUE", "YELLOW", "WHITE", "BLACK" };

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Un fil par emplacement (désactivé s'il n'est pas tiré).")]
        [SerializeField] Renderer[] wires;
        [Tooltip("Bouts de fil coupé, affichés à la place du fil coupé.")]
        [SerializeField] GameObject[] cutStubs;
        [SerializeField] Renderer[] cutStubRenderers;
        [SerializeField] Material[] wireMaterials;
        [SerializeField] Renderer indicator;
        [SerializeField] Material indicatorOn;
        [SerializeField] Material indicatorOff;
        [SerializeField] TMP_Text serialText;
        [SerializeField] Renderer[] progressLamps;
        [SerializeField] Material progressOff;
        [SerializeField] Material progressOn;
        [SerializeField] AudioSource cutSound;
        [SerializeField] AudioSource shortSound;
        [SerializeField] AudioSource successSound;
        [SerializeField] ParticleSystem sparks;

        public readonly NetworkVariable<int> RuleSeed = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Seed = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Round = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Cut = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Shorts = new NetworkVariable<int>(0);

        public PuzzleObjective Objective => objective;
        public event System.Action Changed;

        // ---------- Configuration et règles (déterministes à partir des graines) ----------

        public struct Board
        {
            public int Count;
            public int[] Colors;
            public bool Lamp;
            public string Serial;
            public bool SerialOdd;
        }

        struct Rule
        {
            public int Kind, Color, N;
        }

        public Board Current => MakeBoard(Seed.Value);

        static Board MakeBoard(int seed)
        {
            var random = new System.Random(seed);
            var board = new Board { Count = 4 + random.Next(3), Lamp = random.Next(2) == 0 };
            board.Colors = new int[board.Count];
            for (int i = 0; i < board.Count; i++) board.Colors[i] = random.Next(ColorsFr.Length);
            int digit = random.Next(10);
            board.Serial = (char)('A' + random.Next(26)) + "" + (char)('A' + random.Next(26)) + "-" + random.Next(100, 1000) + digit;
            board.SerialOdd = digit % 2 == 1;
            return board;
        }

        static Rule[] MakeRules(int seed)
        {
            var random = new System.Random(seed);
            var rules = new Rule[RuleCount + 1];
            var used = new bool[7];
            for (int i = 0; i < RuleCount; i++)
            {
                int kind;
                do kind = random.Next(7);
                while (used[kind]);
                used[kind] = true;
                rules[i] = new Rule { Kind = kind, Color = random.Next(ColorsFr.Length), N = 1 + random.Next(3) };
            }
            rules[RuleCount] = new Rule { Kind = -1, N = 1 + random.Next(4) };
            return rules;
        }

        static int CountColor(Board b, int color)
        {
            int n = 0;
            for (int i = 0; i < b.Count; i++) if (b.Colors[i] == color) n++;
            return n;
        }

        static int First(Board b, int color)
        {
            for (int i = 0; i < b.Count; i++) if (b.Colors[i] == color) return i;
            return -1;
        }

        static int Last(Board b, int color)
        {
            for (int i = b.Count - 1; i >= 0; i--) if (b.Colors[i] == color) return i;
            return -1;
        }

        /// <summary>Fil à couper (index 0..Count-1) selon la première règle applicable.</summary>
        static int Evaluate(Rule[] rules, Board b)
        {
            foreach (Rule r in rules)
            {
                int target = -1;
                switch (r.Kind)
                {
                    case 0: if (CountColor(b, r.Color) == 0) target = r.N - 1; break;
                    case 1: if (CountColor(b, r.Color) > 1) target = Last(b, r.Color); break;
                    case 2: if (b.Lamp) target = First(b, r.Color); break;
                    case 3: if (b.Colors[b.Count - 1] == r.Color) target = b.Count - 1; break;
                    case 4: if (b.SerialOdd) target = r.N - 1; break;
                    case 5: if (b.Count == 3 + r.N) target = First(b, r.Color); break;
                    case 6: if (b.Colors[0] == r.Color) target = r.N; break;
                    default: target = r.N - 1; break;
                }
                if (target >= 0 && target < b.Count) return target;
            }
            return 0;
        }

        public int Solution => Seed.Value < 0 || RuleSeed.Value < 0 ? -1 : Evaluate(MakeRules(RuleSeed.Value), Current);

        static string Ordinal(int n) => n == 1 ? Loc.T("1er", "1st") : n == 2 ? Loc.T("2e", "2nd") : n == 3 ? Loc.T("3e", "3rd") : n + Loc.T("e", "th");
        static string C(int color) => Loc.T(ColorsFr[color], ColorsEn[color]);

        /// <summary>Texte du manuel de CE tableau (affiché chez un autre plongeur).</summary>
        public string ManualText(string moduleName)
        {
            if (RuleSeed.Value < 0) return string.Empty;
            Rule[] rules = MakeRules(RuleSeed.Value);
            var text = new StringBuilder();
            text.Append(Loc.T("<b>MANUEL — TABLEAU ", "<b>MANUAL — ")).Append(moduleName).Append(Loc.T("</b>\n", " PANEL</b>\n"));
            text.Append(Loc.T("Appliquez la PREMIÈRE règle vraie. Fils comptés de haut en bas.\n\n", "Apply the FIRST rule that is true. Wires counted top to bottom.\n\n"));
            for (int i = 0; i < rules.Length; i++)
            {
                Rule r = rules[i];
                text.Append("<b>").Append(i + 1).Append(".</b> ");
                switch (r.Kind)
                {
                    case 0: text.Append(Loc.T($"S'il n'y a aucun fil {C(r.Color)} : coupez le {Ordinal(r.N)} fil.", $"If there is no {C(r.Color)} wire: cut the {Ordinal(r.N)} wire.")); break;
                    case 1: text.Append(Loc.T($"S'il y a plus d'un fil {C(r.Color)} : coupez le dernier fil {C(r.Color)}.", $"If there is more than one {C(r.Color)} wire: cut the last {C(r.Color)} wire.")); break;
                    case 2: text.Append(Loc.T($"Si le voyant est allumé et qu'il y a un fil {C(r.Color)} : coupez le premier fil {C(r.Color)}.", $"If the light is on and there is a {C(r.Color)} wire: cut the first {C(r.Color)} wire.")); break;
                    case 3: text.Append(Loc.T($"Si le dernier fil est {C(r.Color)} : coupez le dernier fil.", $"If the last wire is {C(r.Color)}: cut the last wire.")); break;
                    case 4: text.Append(Loc.T($"Si le n° de série finit par un chiffre impair : coupez le {Ordinal(r.N)} fil.", $"If the serial number ends with an odd digit: cut the {Ordinal(r.N)} wire.")); break;
                    case 5: text.Append(Loc.T($"S'il y a exactement {3 + r.N} fils et un fil {C(r.Color)} : coupez le premier fil {C(r.Color)}.", $"If there are exactly {3 + r.N} wires and a {C(r.Color)} wire: cut the first {C(r.Color)} wire.")); break;
                    case 6: text.Append(Loc.T($"Si le premier fil est {C(r.Color)} et qu'il y a au moins {r.N + 1} fils : coupez le {Ordinal(r.N + 1)} fil.", $"If the first wire is {C(r.Color)} and there are at least {r.N + 1} wires: cut the {Ordinal(r.N + 1)} wire.")); break;
                    default: text.Append(Loc.T($"Sinon : coupez le {Ordinal(r.N)} fil.", $"Otherwise: cut the {Ordinal(r.N)} wire.")); break;
                }
                text.Append('\n');
            }
            text.Append(Loc.T("\nTrois tableaux réussis d'affilée rétablissent la dérivation.", "\nThree panels in a row restore the bypass."));
            return text.ToString();
        }

        // ---------- Réseau ----------

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                RuleSeed.Value = Random.Range(1, int.MaxValue);
                Seed.Value = Random.Range(1, int.MaxValue);
            }
            Seed.OnValueChanged += OnIntChanged;
            RuleSeed.OnValueChanged += OnIntChanged;
            Round.OnValueChanged += OnIntChanged;
            Cut.OnValueChanged += OnIntChanged;
            Shorts.OnValueChanged += OnShort;
            objective.StateChanged += OnSolved;
            Loc.Changed += Refresh;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            Seed.OnValueChanged -= OnIntChanged;
            RuleSeed.OnValueChanged -= OnIntChanged;
            Round.OnValueChanged -= OnIntChanged;
            Cut.OnValueChanged -= OnIntChanged;
            Shorts.OnValueChanged -= OnShort;
            objective.StateChanged -= OnSolved;
            Loc.Changed -= Refresh;
        }

        void OnIntChanged(int previous, int current) => Refresh();

        public void Pressed(int index)
        {
            if (!IsSpawned || objective.Solved.Value || index < 0 || index >= Current.Count || (Cut.Value & (1 << index)) != 0) return;
            if (cutSound != null) cutSound.Play();
            CutRpc(index, Seed.Value);
        }

        [Rpc(SendTo.Server)]
        void CutRpc(int index, int seed)
        {
            // Proposition sur un tableau déjà remplacé (double appui, latence) : ignorée.
            if (objective.Solved.Value || seed != Seed.Value || Cut.Value != 0) return;
            Cut.Value = 1 << index;
            if (index == Solution)
            {
                Round.Value++;
                if (Round.Value >= Rounds) objective.ServerSolve();
                else StartCoroutine(NextBoard(0.8f));
            }
            else
            {
                Shorts.Value++;
                Round.Value = 0;
                StartCoroutine(NextBoard(1.6f));
            }
        }

        IEnumerator NextBoard(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (objective.Solved.Value) yield break;
            Seed.Value = Random.Range(1, int.MaxValue);
            Cut.Value = 0;
        }

        void OnShort(int previous, int current)
        {
            if (current <= previous) return;
            if (shortSound != null) shortSound.Play();
            if (sparks != null) sparks.Play();
            if (LocalRig.IsLocalOrShared(objective.Quadrant)) LocalRig.Pulse(0.9f, 0.35f);
        }

        void OnSolved(bool solved)
        {
            if (solved && successSound != null) successSound.Play();
            Refresh();
        }

        void Refresh()
        {
            Changed?.Invoke();
            if (Seed.Value < 0) return;
            Board b = Current;
            for (int i = 0; i < Slots; i++)
            {
                bool present = i < b.Count;
                bool cut = (Cut.Value & (1 << i)) != 0 || objective.Solved.Value && present && i == Solution;
                if (i < wires.Length && wires[i] != null)
                {
                    wires[i].gameObject.SetActive(present && !cut);
                    if (present) wires[i].sharedMaterial = wireMaterials[b.Colors[i]];
                }
                if (i < cutStubs.Length && cutStubs[i] != null)
                {
                    cutStubs[i].SetActive(present && cut);
                    if (present && i < cutStubRenderers.Length && cutStubRenderers[i] != null) cutStubRenderers[i].sharedMaterial = wireMaterials[b.Colors[i]];
                }
            }
            if (indicator != null) indicator.sharedMaterial = b.Lamp ? indicatorOn : indicatorOff;
            if (serialText != null) serialText.text = Loc.T("N° DE SÉRIE  ", "SERIAL NO.  ") + b.Serial;
            int done = objective.Solved.Value ? Rounds : Round.Value;
            for (int i = 0; i < progressLamps.Length; i++)
                if (progressLamps[i] != null) progressLamps[i].sharedMaterial = i < done ? progressOn : progressOff;
        }
    }
}
