using TMPro;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;

namespace Abysses.Interaction
{
    /// <summary>
    /// Écran de surveillance d'une cale (<see cref="ShockMaze"/>) : carte des dalles (sûres / sous tension) et point qui suit
    /// le plongeur de cette cale en direct. Affiché dans un AUTRE module (routage tournant), lisible de son seul occupant.
    /// </summary>
    public class MazeMonitor : MonoBehaviour
    {
        static readonly string[] NamesFr = { "ORANGE", "CYAN", "VERT", "MAGENTA" };
        static readonly string[] NamesEn = { "ORANGE", "CYAN", "GREEN", "MAGENTA" };

        [SerializeField, Range(0, 3)] int quadrant;
        [SerializeField] ShockMaze[] mazes;
        [SerializeField] int shift = 2;
        [Tooltip("Cases de la carte (rangée * colonnes + colonne).")]
        [SerializeField] Renderer[] cells;
        [SerializeField] Material safeMaterial;
        [SerializeField] Material dangerMaterial;
        [SerializeField] Material startMaterial;
        [SerializeField] Material goalMaterial;
        [Tooltip("Point du plongeur ; la carte va de (0,0) à mapSize en local.")]
        [SerializeField] Transform dot;
        [SerializeField] Vector2 mapSize = new Vector2(0.6f, 0.4f);
        [SerializeField] TMP_Text title;
        [Tooltip("Légendes haut / bas de la carte (mur du voisin ou de l'océan, selon la cale surveillée).")]
        [SerializeField] TMP_Text topLabel;
        [SerializeField] TMP_Text bottomLabel;
        [SerializeField] TMP_Text status;

        ShockMaze watched;

        void OnEnable()
        {
            foreach (ShockMaze maze in mazes)
                if (maze != null) maze.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            GameStateManager.ModulesChanged += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            foreach (ShockMaze maze in mazes)
                if (maze != null) maze.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            GameStateManager.ModulesChanged -= Refresh;
            Loc.Changed -= Refresh;
        }

        void OnPhaseChanged(GamePhase phase) => Refresh();

        void Refresh()
        {
            GameStateManager state = GameStateManager.Instance;
            int active = state != null && state.IsSpawned ? state.ActiveMask.Value : 0;
            int owner = active != 0 ? Routing.OwnerReadBy(quadrant, active, shift) : -1;
            watched = owner >= 0 && owner < mazes.Length ? mazes[owner] : null;
            bool on = watched != null && watched.Safe != null && watched.Seed.Value >= 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                cells[i].enabled = on;
                if (!on) continue;
                cells[i].sharedMaterial = i == watched.StartCell ? startMaterial : i == watched.GoalCell ? goalMaterial
                    : watched.Safe[i] ? safeMaterial : dangerMaterial;
            }
            if (dot != null) dot.gameObject.SetActive(on && !watched.Objective.Solved.Value);
            string neighbor = Loc.T("mur du voisin", "neighbour wall"), ocean = Loc.T("mur de l'océan", "ocean wall");
            if (topLabel != null) topLabel.text = !on ? string.Empty : watched.RowsTowardNeighbor ? neighbor : ocean;
            if (bottomLabel != null) bottomLabel.text = !on ? string.Empty : watched.RowsTowardNeighbor ? ocean : neighbor;
            if (title != null)
                title.text = on ? Loc.T("SURVEILLANCE CALE " + NamesFr[owner], NamesEn[owner] + " HOLD MONITOR") : Loc.T("SURVEILLANCE CALE", "HOLD MONITOR");
            if (status != null)
                status.text = !on ? Loc.T("Aucune cale à surveiller.", "No hold to watch.")
                    : watched.Objective.Solved.Value ? Loc.T("VANNE DE PURGE ATTEINTE. Bravo !", "PURGE VALVE REACHED. Well done!")
                    : watched.Armed.Value ? Loc.T("Guide-le case par case jusqu'à la vanne !", "Guide them tile by tile to the valve!")
                    : Loc.T("Il doit repartir de la case DÉPART.", "They must go back to the START tile.");
        }

        void Update()
        {
            if (watched == null || dot == null || !dot.gameObject.activeSelf) return;
            NetworkPlayer walker = ShockMaze.PlayerIn(watched.Quadrant);
            if (walker == null || walker.Head == null) return;
            Vector3 local = watched.Origin.InverseTransformPoint(walker.Head.position);
            float u = Mathf.Clamp(local.x / (ShockMaze.Columns * ShockMaze.Tile), -0.05f, 1.05f);
            float v = Mathf.Clamp(local.z / (ShockMaze.Rows * ShockMaze.Tile), -0.05f, 1.05f);
            dot.localPosition = new Vector3(u * mapSize.x, v * mapSize.y, dot.localPosition.z);
        }
    }
}
