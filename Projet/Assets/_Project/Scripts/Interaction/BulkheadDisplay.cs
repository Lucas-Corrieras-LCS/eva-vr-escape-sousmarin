using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Interaction
{
    /// <summary>
    /// Pupitre des cloisons étanches d'un module (<see cref="BulkheadNetwork"/>) :
    /// - le PLAN de la coque (compartiments, eau, pompe, portes), seulement dans le module désigné, lisible de lui seul ;
    /// - les COMMANDES des portes confiées à ce module (les autres boutons sont masqués) ;
    /// - l'état : fermetures restantes, prochaine montée de l'eau.
    /// Boutons : index = numéro de porte (0..6), <see cref="StartIndex"/> = déclencher l'alerte (plan seulement).
    /// </summary>
    public class BulkheadDisplay : MonoBehaviour, IPokeReceiver
    {
        public const int StartIndex = 7;

        static readonly string[] NamesFr = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly string[] NamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };

        [SerializeField] BulkheadNetwork network;
        [SerializeField, Range(0, 3)] int quadrant;
        [Tooltip("Plan : 6 compartiments puis 7 portes, et leurs étiquettes.")]
        [SerializeField] GameObject map;
        [SerializeField] Renderer[] compartments;
        [SerializeField] Renderer[] doorMarks;
        [SerializeField] Material dry;
        [SerializeField] Material water;
        [SerializeField] Material pump;
        [SerializeField] Material doorOpen;
        [SerializeField] Material doorClosed;
        [Tooltip("Commandes des portes (une par porte, masquée si la porte est commandée ailleurs).")]
        [SerializeField] GameObject[] doorButtons;
        [SerializeField] TMP_Text status;
        [SerializeField] GameObject startButton;

        void OnEnable()
        {
            network.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            network.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            Loc.Changed -= Refresh;
        }

        void OnPhaseChanged(GamePhase phase) => Refresh();

        public void Pressed(int index)
        {
            if (!network.IsSpawned || network.Objective.Solved.Value) return;
            if (index == StartIndex) network.StartRpc();
            else network.CloseRpc(index);
        }

        int Active
        {
            get
            {
                GameStateManager state = GameStateManager.Instance;
                int active = state != null && state.IsSpawned ? state.ActiveMask.Value : 0;
                return active != 0 ? active : 1 << quadrant;
            }
        }

        void Refresh()
        {
            if (network == null) return;
            int active = Active;
            bool hasMap = network.MapHolder(active) == quadrant;
            if (map != null) map.SetActive(hasMap);
            if (hasMap && network.Layout.Value >= 0)
            {
                for (int c = 0; c < compartments.Length; c++)
                    if (compartments[c] != null)
                        compartments[c].sharedMaterial = network.IsFlooded(c) ? water : c == network.Pump ? pump : dry;
                for (int d = 0; d < doorMarks.Length; d++)
                    if (doorMarks[d] != null) doorMarks[d].sharedMaterial = network.IsClosed(d) ? doorClosed : doorOpen;
            }
            for (int d = 0; d < doorButtons.Length; d++)
                if (doorButtons[d] != null)
                    doorButtons[d].SetActive(network.DoorHolder(d, active) == quadrant && !network.IsClosed(d) && !network.Objective.Solved.Value);
            if (startButton != null) startButton.SetActive(hasMap && !network.Running.Value && !network.Objective.Solved.Value);
            if (status != null) status.text = StatusText(active, hasMap);
        }

        void Update()
        {
            // Compte à rebours de la prochaine montée de l'eau (texte seulement, 4 fois par seconde).
            if (status == null || network == null || !network.IsSpawned || network.Objective.Solved.Value) return;
            if (Time.frameCount % 18 == 0) status.text = StatusText(Active, network.MapHolder(Active) == quadrant);
        }

        string StatusText(int active, bool hasMap)
        {
            if (network.Objective.Solved.Value) return Loc.T("FUITE ISOLÉE — coque stabilisée.", "LEAK ISOLATED — hull stabilised.");
            int holder = network.MapHolder(active);
            if (!network.Running.Value)
            {
                string again = network.Failures.Value > 0 ? Loc.T("L'eau a atteint la pompe : nouvelle fuite.\n", "Water reached the pump: new leak.\n") : string.Empty;
                return again + (hasMap
                    ? Loc.T("Tu as le PLAN. Quand l'équipe est prête, DÉCLENCHE L'ALERTE : l'eau avancera toutes les 15 s.",
                        "You have the MAP. When the team is ready, TRIGGER THE ALARM: water spreads every 15 s.")
                    : Loc.T("Le plan est chez " + NamesFr[holder] + " : il déclenche l'alerte, tu fermes tes portes quand il te le dit.",
                        "The map is at " + NamesEn[holder] + "'s: they trigger the alarm, you close your doors when told."));
            }
            double left = network.IsSpawned ? network.NextSpread.Value - network.NetworkManager.ServerTime.Time : 0;
            string header = Loc.T("Fermetures restantes : ", "Closures left: ") + network.ClosuresLeft + " / " + BulkheadNetwork.MaxClosures
                + Loc.T("   ·   l'eau avance dans ", "   ·   water spreads in ") + Mathf.Max(0, Mathf.CeilToInt((float)left)) + " s";
            string role = hasMap
                ? Loc.T("Tu as le PLAN : dis quelles portes fermer avant que l'eau n'atteigne la POMPE (jaune).",
                    "You have the MAP: say which doors to close before the water reaches the PUMP (yellow).")
                : Loc.T("Le plan est chez " + NamesFr[holder] + " : ferme tes portes quand il te le dit.",
                    "The map is at " + NamesEn[holder] + "'s: close your doors when told.");
            return header + "\n" + role;
        }
    }
}
