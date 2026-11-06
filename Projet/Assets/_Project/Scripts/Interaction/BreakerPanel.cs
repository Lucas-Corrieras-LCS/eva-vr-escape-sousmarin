using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Étage 1 — tableau des disjoncteurs (énigme coopérative : la note est chez le partenaire).
    /// Après la panne, il faut réarmer tous les disjoncteurs SAUF deux qui sont en court-circuit.
    /// Les deux à laisser coupés sont tirés au hasard à chaque partie et écrits sur la note du chef mécanicien,
    /// posée sur le bureau du module PARTENAIRE (visible de lui seul) : il doit la lire à voix haute.
    /// Réarmer un disjoncteur interdit fait tout disjoncter (étincelles) : on ne peut pas trouver la solution au hasard.
    /// </summary>
    public class BreakerPanel : NetworkBehaviour, IPokeReceiver
    {
        public const int Count = 5;
        const int Full = (1 << Count) - 1;

        static readonly string[] NamesFr = { "POMPES", "SONAR", "RADIO", "CHAUFFAGE", "PROJECTEURS" };
        static readonly string[] NamesEn = { "PUMPS", "SONAR", "RADIO", "HEATING", "FLOODLIGHTS" };

        [SerializeField] PuzzleObjective objective;
        [Tooltip("Manettes des disjoncteurs (pivot : rotation autour de X local, la manette pointe vers -Z).")]
        [SerializeField] Transform[] levers;
        [Tooltip("Voyant de chaque disjoncteur.")]
        [SerializeField] Renderer[] lamps;
        [SerializeField] Material lampOff;
        [SerializeField] Material lampOn;
        [Tooltip("Notes du chef mécanicien : sur le bureau du module PARTENAIRE (il faut se la faire lire), "
            + "et une copie de repli dans ce module, visible seulement si le partenaire est absent.")]
        [SerializeField] TMP_Text[] notes;
        [Tooltip("Nom du module de ce tableau, écrit sur la note (elle est lue dans un autre module).")]
        [SerializeField] string moduleFrench;
        [SerializeField] string moduleEnglish;
        [SerializeField] AudioSource clickSound;
        [SerializeField] AudioSource tripSound;
        [SerializeField] AudioSource successSound;
        [SerializeField] ParticleSystem sparks;

        /// <summary>Disjoncteurs à laisser coupés (masque de 2 bits), tiré par le serveur.</summary>
        public readonly NetworkVariable<int> Forbidden = new NetworkVariable<int>(-1);
        /// <summary>Disjoncteurs armés (masque).</summary>
        public readonly NetworkVariable<int> Armed = new NetworkVariable<int>(0);

        public static string NameOf(int index) => Loc.T(NamesFr[index], NamesEn[index]);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                int a = Random.Range(0, Count);
                int b = (a + Random.Range(1, Count)) % Count;
                Forbidden.Value = (1 << a) | (1 << b);
                Armed.Value = 0;
            }
            Armed.OnValueChanged += OnArmedChanged;
            Forbidden.OnValueChanged += OnForbiddenChanged;
            objective.StateChanged += OnObjectiveChanged;
            Loc.Changed += RefreshNote;
            RefreshSwitches();
            RefreshNote();
        }

        public override void OnNetworkDespawn()
        {
            Armed.OnValueChanged -= OnArmedChanged;
            Forbidden.OnValueChanged -= OnForbiddenChanged;
            objective.StateChanged -= OnObjectiveChanged;
            Loc.Changed -= RefreshNote;
        }

        public void Pressed(int index)
        {
            if (!IsSpawned || objective.Solved.Value || index < 0 || index >= Count) return;
            if (clickSound != null) clickSound.Play();
            ToggleRpc(index);
        }

        [Rpc(SendTo.Server)]
        void ToggleRpc(int index)
        {
            if (objective.Solved.Value || Forbidden.Value < 0) return;
            int armed = Armed.Value ^ (1 << index);
            if ((armed & Forbidden.Value) != 0)
            {
                // Court-circuit : tout le tableau disjoncte.
                Armed.Value = 0;
                TripRpc();
                return;
            }
            Armed.Value = armed;
            if (armed == (Full & ~Forbidden.Value)) objective.ServerSolve();
        }

        [Rpc(SendTo.Everyone)]
        void TripRpc()
        {
            if (sparks != null) sparks.Play();
            if (tripSound != null) tripSound.Play();
            if (LocalRig.IsLocalOrShared(objective.Quadrant)) LocalRig.Pulse(0.8f, 0.35f);
        }

        void OnArmedChanged(int previous, int current) => RefreshSwitches();
        void OnForbiddenChanged(int previous, int current) => RefreshNote();

        void OnObjectiveChanged(bool solved)
        {
            if (!solved) return;
            // Résolu par le maître du jeu : le tableau se met dans la bonne position.
            if (IsServer && Forbidden.Value >= 0) Armed.Value = Full & ~Forbidden.Value;
            if (successSound != null) successSound.Play();
        }

        void RefreshSwitches()
        {
            for (int i = 0; i < Count; i++)
            {
                bool on = (Armed.Value & (1 << i)) != 0;
                if (i < levers.Length && levers[i] != null) levers[i].localRotation = Quaternion.Euler(on ? 35f : -35f, 0f, 0f);
                if (i < lamps.Length && lamps[i] != null) lamps[i].sharedMaterial = on ? lampOn : lampOff;
            }
        }

        void RefreshNote()
        {
            string text = NoteText();
            for (int i = 0; i < notes.Length; i++)
                if (notes[i] != null) notes[i].text = text;
        }

        string NoteText()
        {
            if (Forbidden.Value < 0) return string.Empty;
            int a = -1, b = -1;
            for (int i = 0; i < Count; i++)
            {
                if ((Forbidden.Value & (1 << i)) == 0) continue;
                if (a < 0) a = i;
                else b = i;
            }
            return Loc.T(
                "<b>NOTE DU CHEF MÉCANICIEN</b>\n<b>Module " + moduleFrench + "</b>\n\nAprès la panne, réarmez tout\nle tableau des disjoncteurs,\n<b>SAUF " + NamesFr[a] + "</b> et <b>" + NamesFr[b] + "</b> :\nils sont en court-circuit !\n\n— M. Ferrand",
                "<b>CHIEF ENGINEER'S NOTE</b>\n<b>" + moduleEnglish + " module</b>\n\nAfter the blackout, reset the\nwhole breaker panel,\n<b>EXCEPT " + NamesEn[a] + "</b> and <b>" + NamesEn[b] + "</b>:\nthey are short-circuited!\n\n— M. Ferrand");
        }
    }
}
