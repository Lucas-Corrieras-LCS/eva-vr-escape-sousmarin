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
    /// Étage 2 — « Antidote du labo », trois sources à croiser :
    /// - le MÉLANGEUR (ce module) : 4 flacons de COULEUR, une dose par appui, puis MÉLANGER ;
    /// - la RECETTE de cet antidote, en noms de MOLÉCULES (« 3 × ARGONITE »), chez un autre plongeur (<see cref="AntidoteRecipe"/>) ;
    /// - la correspondance molécules → couleurs, écrite à l'ENCRE UV au labo ORANGE : il faut la lampe UV, qui circule par les sas.
    /// Mauvais mélange : il mousse et se vide. Recette tirée par le serveur ; la correspondance est commune à tous les mélangeurs.
    /// Boutons : 0..3 = une dose du flacon, 4 = mélanger, 5 = vider.
    /// </summary>
    public class AntidoteMixer : NetworkBehaviour, IPokeReceiver
    {
        public const int Flasks = 4;
        public const int MixIndex = 4;
        public const int EmptyIndex = 5;
        const int MaxDoses = 5;

        static readonly string[] Molecules = { "ARGONITE", "BROMAZINE", "CHLORELLE", "DIOXANE", "ÉTHYLOSE", "FLUORINE", "GALLIUM-7", "HÉLIOTROPE" };
        static readonly string[] ColorsFr = { "ROUGE", "BLEU", "VERT", "JAUNE" };
        static readonly string[] ColorsEn = { "RED", "BLUE", "GREEN", "YELLOW" };

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Graine commune de la correspondance molécules → couleurs (même valeur pour les 4 mélangeurs).")]
        [SerializeField] AntidoteMixer legendSource;
        [SerializeField] TMP_Text[] doseTexts;
        [SerializeField] Transform[] liquids;
        [SerializeField] Renderer statusLamp;
        [SerializeField] Material lampIdle;
        [SerializeField] Material lampOk;
        [SerializeField] Material lampError;
        [SerializeField] AudioSource doseSound;
        [SerializeField] AudioSource failSound;
        [SerializeField] AudioSource successSound;
        [SerializeField] ParticleSystem foam;

        /// <summary>Doses voulues par molécule (2 bits chacune, 4 molécules choisies).</summary>
        public readonly NetworkVariable<int> Recipe = new NetworkVariable<int>(-1);
        /// <summary>Graine de la correspondance (utilisée seulement sur le mélangeur de référence).</summary>
        public readonly NetworkVariable<int> LegendSeed = new NetworkVariable<int>(-1);
        /// <summary>Doses versées par flacon de couleur (3 bits chacune).</summary>
        public readonly NetworkVariable<int> Doses = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Failures = new NetworkVariable<int>(0);

        public event System.Action Changed;
        public PuzzleObjective Objective => objective;
        float[] fullLevels;
        AntidoteMixer Reference => legendSource != null ? legendSource : this;

        /// <summary>Les 4 molécules en jeu et la couleur de flacon de chacune (index de couleur), d'après la graine commune.</summary>
        public static void Legend(int seed, out int[] molecules, out int[] colorOf)
        {
            var random = new System.Random(seed);
            molecules = new int[Flasks];
            colorOf = new int[Flasks];
            var picked = new bool[Molecules.Length];
            for (int i = 0; i < Flasks; i++)
            {
                int m;
                do m = random.Next(Molecules.Length);
                while (picked[m]);
                picked[m] = true;
                molecules[i] = m;
                colorOf[i] = i;
            }
            for (int i = Flasks - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (colorOf[i], colorOf[j]) = (colorOf[j], colorOf[i]);
            }
        }

        public int RecipeDoses(int molecule) => (Recipe.Value >> (molecule * 2)) & 3;
        public int DosesIn(int flask) => (Doses.Value >> (flask * 3)) & 7;
        public bool Ready => Recipe.Value >= 0 && Reference.LegendSeed.Value >= 0;

        /// <summary>Doses attendues pour le flacon de couleur <paramref name="flask"/>.</summary>
        public int Expected(int flask)
        {
            Legend(Reference.LegendSeed.Value, out _, out int[] colorOf);
            for (int k = 0; k < Flasks; k++)
                if (colorOf[k] == flask) return RecipeDoses(k);
            return 0;
        }

        public string RecipeText(string moduleName)
        {
            if (!Ready) return string.Empty;
            Legend(Reference.LegendSeed.Value, out int[] molecules, out _);
            var text = new StringBuilder(Loc.T("<b>ANTIDOTE — LABO ", "<b>ANTIDOTE — ")).Append(moduleName).Append(Loc.T("</b>\n\n", " LAB</b>\n\n"));
            for (int k = 0; k < Flasks; k++)
                text.Append(RecipeDoses(k)).Append(" × ").Append(Molecules[molecules[k]]).Append('\n');
            text.Append(Loc.T("\nQuel flacon contient quoi ? Voir le tableau à l'encre UV (labo ORANGE).", "\nWhich flask holds what? See the UV-ink chart (ORANGE lab)."));
            return text.ToString();
        }

        public static string LegendText(int seed)
        {
            if (seed < 0) return string.Empty;
            Legend(seed, out int[] molecules, out int[] colorOf);
            var text = new StringBuilder(Loc.T("<b>RÉACTIFS</b>\n", "<b>REAGENTS</b>\n"));
            for (int k = 0; k < Flasks; k++)
                text.Append(Molecules[molecules[k]]).Append(" = ").Append(Loc.T("flacon ", "flask ")).Append(Loc.T(ColorsFr[colorOf[k]], ColorsEn[colorOf[k]])).Append('\n');
            return text.ToString();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                int recipe, total;
                do
                {
                    recipe = Random.Range(0, 1 << (Flasks * 2));
                    total = 0;
                    for (int k = 0; k < Flasks; k++) total += (recipe >> (k * 2)) & 3;
                }
                while (total < 4 || total > 8);
                Recipe.Value = recipe;
                if (legendSource == null) LegendSeed.Value = Random.Range(1, int.MaxValue);
            }
            Recipe.OnValueChanged += OnIntChanged;
            Doses.OnValueChanged += OnDosesChanged;
            Failures.OnValueChanged += OnFailure;
            objective.StateChanged += OnSolved;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            Recipe.OnValueChanged -= OnIntChanged;
            Doses.OnValueChanged -= OnDosesChanged;
            Failures.OnValueChanged -= OnFailure;
            objective.StateChanged -= OnSolved;
        }

        void OnIntChanged(int previous, int current) => Refresh();

        void OnDosesChanged(int previous, int current)
        {
            if (current > previous && doseSound != null) doseSound.Play();
            Refresh();
        }

        void OnFailure(int previous, int current)
        {
            if (current <= previous) return;
            if (failSound != null) failSound.Play();
            if (foam != null) foam.Play();
            if (LocalRig.IsLocalOrShared(objective.Quadrant)) LocalRig.Pulse(0.6f, 0.3f);
            Refresh();
        }

        void OnSolved(bool solved)
        {
            if (solved && successSound != null) successSound.Play();
            Refresh();
        }

        public void Pressed(int index)
        {
            if (!IsSpawned || objective.Solved.Value) return;
            if (index < Flasks) DoseRpc(index);
            else if (index == MixIndex) MixRpc();
            else if (index == EmptyIndex) EmptyRpc();
        }

        [Rpc(SendTo.Server)]
        void DoseRpc(int flask)
        {
            if (objective.Solved.Value || DosesIn(flask) >= MaxDoses) return;
            Doses.Value += 1 << (flask * 3);
        }

        [Rpc(SendTo.Server)]
        void EmptyRpc()
        {
            if (!objective.Solved.Value) Doses.Value = 0;
        }

        [Rpc(SendTo.Server)]
        void MixRpc()
        {
            if (objective.Solved.Value || !Ready || Doses.Value == 0) return;
            for (int flask = 0; flask < Flasks; flask++)
            {
                if (DosesIn(flask) == Expected(flask)) continue;
                Failures.Value++;
                Doses.Value = 0;
                return;
            }
            objective.ServerSolve();
        }

        void Refresh()
        {
            Changed?.Invoke();
            for (int i = 0; i < Flasks; i++)
            {
                int doses = DosesIn(i);
                if (i < doseTexts.Length && doseTexts[i] != null) doseTexts[i].text = doses.ToString();
                if (i < liquids.Length && liquids[i] != null)
                {
                    // Le niveau du flacon (pivot au fond) baisse à chaque dose versée.
                    if (fullLevels == null)
                    {
                        fullLevels = new float[liquids.Length];
                        for (int k = 0; k < liquids.Length; k++) fullLevels[k] = liquids[k] != null ? liquids[k].localScale.y : 1f;
                    }
                    Vector3 scale = liquids[i].localScale;
                    scale.y = fullLevels[i] * Mathf.Lerp(1f, 0.25f, doses / (float)MaxDoses);
                    liquids[i].localScale = scale;
                }
            }
            if (statusLamp != null) statusLamp.sharedMaterial = objective.Solved.Value ? lampOk : Failures.Value > 0 && Doses.Value == 0 ? lampError : lampIdle;
        }
    }
}
