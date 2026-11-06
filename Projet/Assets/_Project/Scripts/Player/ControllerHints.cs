using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Player
{
    /// <summary>
    /// Aide-mémoire collé aux manettes : quel bouton fait quoi. Visible à la surface et pendant les premières minutes
    /// de l'étage 1, puis masqué (le joueur a compris) ; il revient quand on ouvre le menu.
    /// </summary>
    public class ControllerHints : MonoBehaviour
    {
        [SerializeField] TMP_Text left;
        [SerializeField] TMP_Text right;
        [Tooltip("Durée d'affichage au début de l'étage 1 (s).")]
        [SerializeField, Min(0f)] float firstFloorDuration = 120f;

        float playingSince = -1f;
        float nextCheck;
        bool shown = true;

        void OnEnable()
        {
            Loc.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        void OnDisable()
        {
            Loc.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            GameStateManager state = GameStateManager.Instance;
            playingSince = phase == GamePhase.Playing && state != null && state.Floor.Value == 1 ? Time.unscaledTime : -1f;
            nextCheck = 0f;
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            GameStateManager state = GameStateManager.Instance;
            bool show;
            if (state == null || !state.IsSpawned) show = true;
            else
            {
                GamePhase phase = state.Phase.Value;
                show = phase == GamePhase.Lobby
                       || (phase == GamePhase.Playing && playingSince >= 0f && Time.unscaledTime - playingSince < firstFloorDuration);
            }
            if (show == shown) return;
            shown = show;
            if (left != null) left.gameObject.SetActive(show);
            if (right != null) right.gameObject.SetActive(show);
        }

        void Refresh()
        {
            bool joystick = GameConfig.Instance != null && GameConfig.Instance.AllowJoystickLocomotion;
            if (left != null)
                left.text = Loc.T("<b>Bouton Menu</b> : menu\n<b>Gâchette arrière</b> : attraper", "<b>Menu button</b>: menu\n<b>Grip</b>: grab")
                            + (joystick ? Loc.T("\n<b>Stick</b> : avancer", "\n<b>Stick</b>: move") : string.Empty);
            if (right != null)
                right.text = Loc.T("<b>Bout de la manette</b> : appuyer\nsur un bouton\n<b>Gâchette arrière</b> : attraper", "<b>Controller tip</b>: press\na button\n<b>Grip</b>: grab")
                             + (joystick ? Loc.T("\n<b>Stick</b> : tourner", "\n<b>Stick</b>: turn") : string.Empty);
        }
    }
}
