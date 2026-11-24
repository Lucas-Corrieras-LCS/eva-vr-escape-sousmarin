using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Registre de lest d'un module : charge cible du plateau d'un AUTRE plongeur (routage tournant), lisible du seul occupant.
    /// Voir <see cref="BallastScale"/>.
    /// </summary>
    public class BallastRegister : MonoBehaviour
    {
        static readonly string[] NamesFr = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly string[] NamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };

        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] BallastScale scale;
        [SerializeField] int shift = 2;
        [SerializeField] TMP_Text text;

        void OnEnable()
        {
            scale.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            scale.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            Loc.Changed -= Refresh;
        }

        void OnPhaseChanged(GamePhase phase) => Refresh();

        void Refresh()
        {
            if (text == null) return;
            GameStateManager state = GameStateManager.Instance;
            int active = state != null && state.IsSpawned ? state.ActiveMask.Value : 0;
            int owner = active != 0 ? Routing.OwnerReadBy(quadrant, active, shift) : -1;
            if (owner < 0 || scale.Target(owner) < 0)
            {
                text.text = Loc.T("<b>REGISTRE DE LEST</b>\n\nEn attente des ordres…", "<b>BALLAST LOG</b>\n\nAwaiting orders…");
                return;
            }
            text.text = Loc.T("<b>REGISTRE DE LEST</b>\n\nPlateau ", "<b>BALLAST LOG</b>\n\n") + Loc.T(NamesFr[owner], NamesEn[owner])
                + Loc.T(" : charger <b>", " pan: load <b>") + scale.Target(owner) + " kg</b>\n\n"
                + Loc.T("Tous les plateaux doivent être justes en même temps.\nÉchangez des lests par le sas.", "All pans must be right at the same time.\nSwap weights through the airlock.");
        }
    }
}
