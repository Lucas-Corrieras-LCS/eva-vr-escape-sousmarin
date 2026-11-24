using System.Text;
using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Verrou de cabine d'un module (méta-énigme <see cref="FloorLock"/>) :
    /// - écran des fragments confiés à CE module (lisible de son seul occupant) ;
    /// - pavé numérique pour composer le code complet (n'importe quel module peut le saisir).
    /// Boutons : 0..9 = chiffres, 10 = effacer, 11 = valider.
    /// </summary>
    public class FloorLockPanel : MonoBehaviour, IPokeReceiver
    {
        public const int ClearIndex = 10;
        public const int ValidateIndex = 11;

        [SerializeField] FloorLock floorLock;
        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] TMP_Text fragmentsText;
        [SerializeField] TMP_Text entryText;
        [SerializeField] Renderer statusLamp;
        [SerializeField] Material lampIdle;
        [SerializeField] Material lampOk;
        [SerializeField] Material lampError;
        [SerializeField] AudioSource errorSound;
        [SerializeField] AudioSource successSound;

        string entry = string.Empty;
        int seenWrong;
        bool wasUnlocked;

        void OnEnable()
        {
            floorLock.Changed += Refresh;
            Loc.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            seenWrong = floorLock.Wrong.Value;
            Refresh();
        }

        void OnDisable()
        {
            floorLock.Changed -= Refresh;
            Loc.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase) => Refresh();

        public void Pressed(int index)
        {
            if (!floorLock.IsSpawned || floorLock.Unlocked) return;
            if (index == ClearIndex) entry = string.Empty;
            else if (index == ValidateIndex)
            {
                if (entry.Length > 0 && int.TryParse(entry, out int value)) floorLock.SubmitRpc(value, entry.Length);
                entry = string.Empty;
            }
            else if (index >= 0 && index <= 9 && entry.Length < floorLock.Count) entry += index.ToString();
            Refresh();
        }

        void Refresh()
        {
            if (floorLock == null) return;
            int wrong = floorLock.Wrong.Value;
            if (wrong != seenWrong)
            {
                seenWrong = wrong;
                if (LocalRig.IsLocalOrShared(quadrant))
                {
                    if (errorSound != null) errorSound.Play();
                    LocalRig.Pulse(0.6f, 0.25f);
                }
            }
            if (floorLock.Unlocked && !wasUnlocked && successSound != null && LocalRig.IsLocalOrShared(quadrant)) successSound.Play();
            wasUnlocked = floorLock.Unlocked;

            if (statusLamp != null)
                statusLamp.sharedMaterial = floorLock.Unlocked ? lampOk : wrong > 0 && !floorLock.AllFragments ? lampError : lampIdle;
            if (entryText != null)
                entryText.text = floorLock.Unlocked ? "OK" : Spaced(entry + new string('_', Mathf.Max(0, floorLock.Count - entry.Length)));
            if (fragmentsText != null) fragmentsText.text = FragmentsText();
        }

        static string Spaced(string value)
        {
            var text = new StringBuilder(value.Length * 2);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(value[i]);
            }
            return text.ToString();
        }

        string FragmentsText()
        {
            if (floorLock.Unlocked) return Loc.T("CABINE DÉVERROUILLÉE\nEmbarquez !", "CABIN UNLOCKED\nGet aboard!");
            GameStateManager state = GameStateManager.Instance;
            int active = state != null && state.IsSpawned ? state.ActiveMask.Value : 0;
            if (active == 0) active = 1 << quadrant;

            var text = new StringBuilder();
            text.Append(Loc.T("<b>CODE DE LA CABINE</b> : ", "<b>CABIN CODE</b>: ")).Append(floorLock.Count).Append(Loc.T(" chiffres\n", " digits\n"));
            bool any = false;
            for (int i = 0; i < floorLock.Count; i++)
            {
                if (Routing.FragmentHolder(i, active, floorLock.Shift) != quadrant) continue;
                any = true;
                text.Append(Loc.T("Fragment ", "Fragment ")).Append(i + 1).Append(" : ");
                text.Append(floorLock.GroupSolved(i) ? "<b>" + floorLock.Digit(i) + "</b>" : Loc.T("<i>énigme en cours…</i>", "<i>puzzle in progress…</i>"));
                text.Append('\n');
            }
            if (!any) text.Append(Loc.T("Aucun fragment ici :\nles autres plongeurs les ont.", "No fragment here:\nthe other divers have them."));
            else text.Append(Loc.T("Les autres fragments sont chez tes équipiers.", "The other fragments are with your teammates."));
            return text.ToString();
        }
    }
}
