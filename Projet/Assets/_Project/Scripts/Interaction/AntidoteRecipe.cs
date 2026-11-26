using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Fiche de recette affichée dans un module : celle du mélangeur d'un AUTRE plongeur (routage tournant),
    /// lisible du seul occupant. Voir <see cref="AntidoteMixer"/>.
    /// </summary>
    public class AntidoteRecipe : MonoBehaviour
    {
        static readonly string[] NamesFr = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly string[] NamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };

        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] AntidoteMixer[] mixers;
        [SerializeField] int shift = 1;
        [SerializeField] TMP_Text text;

        void OnEnable()
        {
            foreach (AntidoteMixer mixer in mixers)
                if (mixer != null) mixer.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            GameStateManager.ModulesChanged += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            foreach (AntidoteMixer mixer in mixers)
                if (mixer != null) mixer.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            GameStateManager.ModulesChanged -= Refresh;
            Loc.Changed -= Refresh;
        }

        void OnPhaseChanged(GamePhase phase) => Refresh();

        void Refresh()
        {
            if (text == null) return;
            GameStateManager state = GameStateManager.Instance;
            int active = state != null && state.IsSpawned ? state.ActiveMask.Value : 0;
            int owner = active != 0 ? Routing.OwnerReadBy(quadrant, active, shift) : -1;
            if (owner < 0 || owner >= mixers.Length || mixers[owner] == null)
            {
                text.text = Loc.T("<b>RECETTES</b>\n\nAucune recette pour ce labo.", "<b>RECIPES</b>\n\nNo recipe for this lab.");
                return;
            }
            text.text = mixers[owner].Objective.Solved.Value
                ? Loc.T("<b>ANTIDOTE " + NamesFr[owner] + "</b>\n\nMélange réussi. Merci !", "<b>" + NamesEn[owner] + " ANTIDOTE</b>\n\nMix successful. Thanks!")
                : mixers[owner].RecipeText(Loc.T(NamesFr[owner], NamesEn[owner]));
        }
    }
}
