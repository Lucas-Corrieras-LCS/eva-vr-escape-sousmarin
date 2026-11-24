using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Poste du standard dans un module (<see cref="PatchBoard"/>) : selon le rôle de ce module, il affiche le tableau de
    /// fiches et de prises (opérateur), la liste des liaisons, la légende, ou qui a quoi.
    /// Texte lisible du seul occupant. Boutons : 0..3 = fiches, 4..11 = prises (on touche une fiche, puis une prise).
    /// </summary>
    public class PatchStation : MonoBehaviour, IPokeReceiver
    {
        static readonly string[] NamesFr = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly string[] NamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };

        [SerializeField] PatchBoard board;
        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] TMP_Text text;
        [Tooltip("Tableau de l'opérateur (fiches, prises, câbles), masqué pour les autres rôles.")]
        [SerializeField] GameObject boardGroup;
        [SerializeField] TMP_Text[] sourceLabels;
        [SerializeField] TMP_Text[] socketLabels;
        [SerializeField] Transform[] sourcePoints;
        [SerializeField] Transform[] socketPoints;
        [SerializeField] Transform[] cables;
        [SerializeField] Renderer[] sourceCaps;
        [SerializeField] Material capIdle;
        [SerializeField] Material capSelected;

        int selected = -1;

        void OnEnable()
        {
            board.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            board.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            Loc.Changed -= Refresh;
        }

        void OnPhaseChanged(GamePhase phase) => Refresh();

        int Active
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                int active = state != null && state.IsSpawned ? state.ActiveMask.Value : 0;
                return active != 0 ? active : 1 << quadrant;
            }
        }

        bool IsOperator(PatchBoard.Role role) => role == PatchBoard.Role.Operator || role == PatchBoard.Role.OperatorAndLegend;

        public void Pressed(int index)
        {
            if (!board.IsSpawned || board.Objective.Solved.Value || !IsOperator(board.RoleOf(quadrant, Active))) return;
            if (index < PatchBoard.Sources) selected = index;
            else if (selected >= 0)
            {
                board.LinkRpc(selected, index - PatchBoard.Sources);
                selected = -1;
            }
            Refresh();
        }

        void Refresh()
        {
            if (board == null) return;
            int active = Active;
            PatchBoard.Role role = board.RoleOf(quadrant, active);
            bool op = IsOperator(role);
            if (boardGroup != null) boardGroup.SetActive(op && board.Ready);
            if (op && board.Ready)
            {
                for (int i = 0; i < sourceLabels.Length; i++) if (sourceLabels[i] != null) sourceLabels[i].text = board.SourceNumber(i).ToString();
                for (int i = 0; i < socketLabels.Length; i++) if (socketLabels[i] != null) socketLabels[i].text = board.SocketNumber(i).ToString();
                for (int i = 0; i < cables.Length; i++)
                {
                    if (cables[i] == null) continue;
                    int link = board.Objective.Solved.Value ? board.Expected(i) : board.LinkOf(i);
                    cables[i].gameObject.SetActive(link >= 0);
                    if (link < 0) continue;
                    Vector3 from = sourcePoints[i].localPosition, to = socketPoints[link].localPosition;
                    Vector3 delta = to - from;
                    cables[i].localPosition = (from + to) / 2f;
                    cables[i].localRotation = Quaternion.FromToRotation(Vector3.up, delta);
                    cables[i].localScale = new Vector3(0.012f, delta.magnitude / 2f, 0.012f);
                }
                for (int i = 0; i < sourceCaps.Length; i++)
                    if (sourceCaps[i] != null) sourceCaps[i].sharedMaterial = i == selected ? capSelected : capIdle;
            }
            if (text != null) text.text = Text(role, active);
        }

        string Text(PatchBoard.Role role, int active)
        {
            if (board.Objective.Solved.Value) return Loc.T("<b>STANDARD RÉTABLI</b>\nLes circuits de remontée répondent.", "<b>SWITCHBOARD RESTORED</b>\nThe ascent circuits respond.");
            if (!board.Ready) return string.Empty;
            string Name(int q) => Loc.T(NamesFr[q], NamesEn[q]);
            switch (role)
            {
                case PatchBoard.Role.Operator:
                    return Loc.T("<b>TU ES L'OPÉRATEUR</b>\nTouche une fiche (à gauche), puis la prise où la brancher.\nLa liste des liaisons est chez " + Name(board.ListHolder(active))
                        + ", la légende des couleurs chez " + Name(board.LegendHolder(active)) + ".",
                        "<b>YOU ARE THE OPERATOR</b>\nTouch a plug (left), then the socket to plug it into.\nThe link list is at " + Name(board.ListHolder(active))
                        + "'s, the colour legend at " + Name(board.LegendHolder(active)) + "'s.");
                case PatchBoard.Role.OperatorAndLegend:
                    return Loc.T("<b>TU ES L'OPÉRATEUR</b> — la liste est chez " + Name(board.ListHolder(active)) + ".\n", "<b>YOU ARE THE OPERATOR</b> — the list is at " + Name(board.ListHolder(active)) + "'s.\n")
                        + board.LegendText();
                case PatchBoard.Role.List: return board.ListText();
                case PatchBoard.Role.Legend: return board.LegendText();
                default:
                    return Loc.T("<b>STANDARD</b>\nOpérateur : " + Name(board.Operator(active)) + "\nListe : " + Name(board.ListHolder(active)) + "\nLégende : " + Name(board.LegendHolder(active)),
                        "<b>SWITCHBOARD</b>\nOperator: " + Name(board.Operator(active)) + "\nList: " + Name(board.ListHolder(active)) + "\nLegend: " + Name(board.LegendHolder(active)));
            }
        }
    }
}
