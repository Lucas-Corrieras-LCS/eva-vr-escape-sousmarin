using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Manuel affiché dans un module : celui du tableau d'un AUTRE plongeur, choisi par le routage tournant
    /// (<see cref="Routing.OwnerReadBy"/>). Lisible du seul occupant du module (voir <see cref="PrivateView"/>).
    /// </summary>
    public class WiringManual : MonoBehaviour
    {
        static readonly string[] NamesFr = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly string[] NamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };

        [SerializeField, Range(0, 3)] int quadrant;
        [Tooltip("Tableaux des 4 modules, indexés par quadrant.")]
        [SerializeField] WiringPanel[] panels;
        [SerializeField] int shift = 1;
        [SerializeField] TMP_Text text;

        void OnEnable()
        {
            foreach (WiringPanel panel in panels)
                if (panel != null) panel.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            foreach (WiringPanel panel in panels)
                if (panel != null) panel.Changed -= Refresh;
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
            if (owner < 0 || owner >= panels.Length || panels[owner] == null)
            {
                text.text = Loc.T("<b>MANUEL DE DÉSAMORÇAGE</b>\n\nAucun tableau ne dépend de ce manuel.", "<b>DEFUSING MANUAL</b>\n\nNo panel depends on this manual.");
                return;
            }
            text.text = panels[owner].Objective.Solved.Value
                ? Loc.T("<b>TABLEAU " + NamesFr[owner] + "</b>\n\nDérivation rétablie. Merci !", "<b>" + NamesEn[owner] + " PANEL</b>\n\nBypass restored. Thanks!")
                : panels[owner].ManualText(Loc.T(NamesFr[owner], NamesEn[owner]));
        }
    }
}
