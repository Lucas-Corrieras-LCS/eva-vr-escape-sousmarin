using System.Globalization;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Core;
using Abysses.Level;

namespace Abysses.Player
{
    /// <summary>
    /// Montre de plongée au poignet gauche : profondeur, réserve d'oxygène (compte à rebours d'ambiance, jamais de défaite)
    /// et temps écoulé. Les indices de l'IA et les messages du maître du jeu s'affichent au-dessus, avec un carillon.
    /// L'information est portée par le joueur plutôt que par un menu flottant. Texte rafraîchi une fois par seconde.
    /// </summary>
    public class DiveWatch : MonoBehaviour
    {
        [SerializeField] TMP_Text face;
        [Tooltip("Bulle d'indice au-dessus de la montre (masquée hors message).")]
        [SerializeField] GameObject messagePanel;
        [SerializeField] TMP_Text messageText;
        [SerializeField] AudioSource chime;
        [Tooltip("Réserve d'oxygène affichée au départ de la plongée (minutes).")]
        [SerializeField, Min(5f)] float oxygenMinutes = 45f;
        [SerializeField, Min(3f)] float messageDuration = 25f;
        [SerializeField] Color normalColor = new Color(0.45f, 0.95f, 1f);
        [SerializeField] Color lowColor = new Color(1f, 0.35f, 0.2f);

        float nextRefresh;
        float hideMessageAt = -1f;

        void OnEnable()
        {
            HintSystem.MessageReceived += OnMessage;
            Loc.Changed += Refresh;
            GameStateManager.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            HintSystem.MessageReceived -= OnMessage;
            Loc.Changed -= Refresh;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void Start()
        {
            if (messagePanel != null) messagePanel.SetActive(false);
            // Le bouton MENU (collider) suit la main : le corps du joueur (CharacterController) ne doit pas buter dessus.
            var body = GetComponentInParent<CharacterController>();
            if (body != null)
                foreach (Collider part in GetComponentsInChildren<Collider>(true))
                    Physics.IgnoreCollision(body, part);
            Refresh();
        }

        // Arrivée à un étage, fin de partie : la profondeur change tout de suite, sans attendre la seconde suivante.
        void OnPhaseChanged(GamePhase phase) => Refresh();

        void OnMessage(string message)
        {
            if (messageText != null) messageText.text = message;
            if (messagePanel != null) messagePanel.SetActive(true);
            hideMessageAt = Time.unscaledTime + messageDuration;
            if (chime != null) chime.Play();
            LocalRig.Pulse(0.3f, 0.12f);
        }

        void Update()
        {
            if (hideMessageAt > 0f && Time.unscaledTime >= hideMessageAt)
            {
                hideMessageAt = -1f;
                if (messagePanel != null) messagePanel.SetActive(false);
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }

        void Refresh()
        {
            if (face == null) return;
            GameStateManager state = GameStateManager.Instance;
            if (state == null || !state.IsSpawned)
            {
                face.color = normalColor;
                face.text = Loc.T("SURFACE\nO2 --:--\n--:--", "SURFACE\nO2 --:--\n--:--");
                return;
            }

            int depth = state.DepthOf(state.Floor.Value);
            double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d;
            bool started = state.StartTime.Value > 0d;
            double end = state.Phase.Value == GamePhase.Finished ? state.EndTime.Value : now;
            int elapsed = started ? Mathf.Max(0, (int)(end - state.StartTime.Value)) : 0;
            int oxygen = Mathf.Max(0, Mathf.RoundToInt(oxygenMinutes * 60f) - elapsed);
            bool low = started && oxygen < 300;

            face.color = low ? lowColor : normalColor;
            string depthLine = depth == 0 ? "SURFACE" : "-" + depth.ToString(CultureInfo.InvariantCulture) + " m";
            face.text = depthLine
                + "\nO2 " + (started ? Leaderboard.Duration(oxygen) : "--:--")
                + "\n" + (started ? Leaderboard.Duration(elapsed) : "--:--");
        }
    }
}
