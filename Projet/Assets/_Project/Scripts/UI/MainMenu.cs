using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Abysses.Core;
using Abysses.Interaction;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;

namespace Abysses.UI
{
    /// <summary>
    /// Menu VR (canvas World Space porté par le rig).
    /// - Toujours visible hors ligne, à la surface et à la fin ; en cours de partie, il s'ouvre et se ferme
    ///   avec le bouton Menu de la manette gauche, le bouton MENU de la montre (touche M dans l'éditeur) pour recalibrer ou quitter.
    /// - À chaque ouverture, il se place devant le regard du joueur.
    /// - Guide la calibration : le départ n'est possible que lorsque chaque plongeur est dans sa cabine.
    /// Entièrement piloté par événements : connexion, joueurs, phase, présence en cabine.
    /// </summary>
    public class MainMenu : MonoBehaviour, IPokeReceiver
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text playersText;
        [Tooltip("Consigne contextuelle (calibration, attente des autres…).")]
        [SerializeField] TMP_Text hintText;
        [Tooltip("Voyant d'état de la connexion (gris, ambre, vert, rouge).")]
        [SerializeField] Image statusDot;

        [Header("Hors ligne")]
        [SerializeField] GameObject offlineGroup;
        [SerializeField] Button joinOnlineButton;
        [SerializeField] Button hostLanButton;
        [SerializeField] Button joinLanButton;
        [SerializeField] Button soloButton;

        [Header("En partie")]
        [SerializeField] GameObject onlineGroup;
        [SerializeField] Button startButton;
        [SerializeField] Button recenterButton;
        [SerializeField] Button leaveButton;

        [Header("Réglages")]
        [SerializeField] Button languageButton;
        [Tooltip("Mode facile (à la surface seulement).")]
        [SerializeField] Button easyButton;
        [Tooltip("Classement des équipes, affiché à la fin.")]
        [SerializeField] TMP_Text leaderboardText;

        [Header("Placement")]
        [SerializeField, Min(0.3f)] float distance = 0.75f;
        [SerializeField] float heightOffset = -0.12f;

        ConnectionManager connection;
        TMP_Text startLabel;
        TMP_Text easyLabel;
        TMP_Text languageLabel;
        InputAction toggleAction;
        bool openedDuringGame;
        bool wasVisible;
        GamePhase lastPhase = GamePhase.Lobby;

        void Awake()
        {
            // Bouton Menu de la manette gauche (selon le profil OpenXR) + touche M pour l'éditeur / le simulateur.
            toggleAction = new InputAction("ToggleMenu", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            toggleAction.AddBinding("<XRController>{LeftHand}/menu");
            toggleAction.AddBinding("<XRController>{LeftHand}/menuButton");
            toggleAction.AddBinding("<Keyboard>/m");
            toggleAction.performed += OnToggle;
            startLabel = startButton.GetComponentInChildren<TMP_Text>(true);
            if (easyButton != null) easyLabel = easyButton.GetComponentInChildren<TMP_Text>(true);
            if (languageButton != null) languageLabel = languageButton.GetComponentInChildren<TMP_Text>(true);
        }

        void OnEnable() => toggleAction.Enable();
        void OnDisable() => toggleAction.Disable();

        void Start()
        {
            connection = ConnectionManager.Instance;
            joinOnlineButton.onClick.AddListener(connection.JoinOnline);
            hostLanButton.onClick.AddListener(connection.HostLan);
            joinLanButton.onClick.AddListener(connection.JoinLan);
            soloButton.onClick.AddListener(connection.PlaySolo);
            leaveButton.onClick.AddListener(OnLeavePressed);
            startButton.onClick.AddListener(OnStartPressed);
            recenterButton.onClick.AddListener(OnRecenterPressed);
            if (languageButton != null) languageButton.onClick.AddListener(Loc.Toggle);
            if (easyButton != null) easyButton.onClick.AddListener(OnEasyPressed);
            Loc.Changed += Refresh;
            GameStateManager.SettingsChanged += Refresh;

            connection.StateChanged += Refresh;
            NetworkPlayer.Spawned += OnPlayersChanged;
            NetworkPlayer.Despawned += OnPlayersChanged;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            GameStateManager.ReadyChanged += OnReadyChanged;
            LocalRig.QuadrantAssigned += OnQuadrantAssigned;
            Refresh();
        }

        void OnDestroy()
        {
            toggleAction.performed -= OnToggle;
            toggleAction.Dispose();
            if (connection != null) connection.StateChanged -= Refresh;
            NetworkPlayer.Spawned -= OnPlayersChanged;
            NetworkPlayer.Despawned -= OnPlayersChanged;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
            GameStateManager.ReadyChanged -= OnReadyChanged;
            LocalRig.QuadrantAssigned -= OnQuadrantAssigned;
            Loc.Changed -= Refresh;
            GameStateManager.SettingsChanged -= Refresh;
        }

        void OnEasyPressed()
        {
            GameStateManager state = GameStateManager.Instance;
            if (state != null && state.Phase.Value == GamePhase.Lobby) state.SetEasyModeRpc(!state.EasyMode.Value);
        }

        void OnPlayersChanged(NetworkPlayer player) => Refresh();
        void OnReadyChanged(int readyMask) => Refresh();

        void OnPhaseChanged(GamePhase phase)
        {
            openedDuringGame = false;
            // Retour à la surface : au joystick (debug), le joueur peut être n'importe où dans l'étage qui vient d'être
            // déchargé (et tomber, gravité active) ; on le replace dans sa cabine. En build EVA, il y retourne à pied.
            if (phase == GamePhase.Lobby && lastPhase != GamePhase.Lobby && GameConfig.Instance.AllowJoystickLocomotion && LocalRig.Instance != null)
                LocalRig.Instance.Recenter();
            lastPhase = phase;
            Refresh();
        }

        void OnQuadrantAssigned(int quadrant)
        {
            // Après calibration, le menu se replace devant le joueur.
            PlaceInFrontOfPlayer();
        }

        void OnToggle(InputAction.CallbackContext context) => ToggleInGame();

        /// <summary>Bouton MENU de la montre.</summary>
        public void Pressed(int index) => ToggleInGame();

        void ToggleInGame()
        {
            if (!InGame) return;
            openedDuringGame = !openedDuringGame;
            Refresh();
        }

        void OnStartPressed()
        {
            GameStateManager state = GameStateManager.Instance;
            if (state == null) return;
            if (state.Phase.Value == GamePhase.Finished || InGame) state.RequestRestartRpc();
            else state.RequestStartRpc();
        }

        void OnRecenterPressed()
        {
            if (LocalRig.Instance == null) return;
            LocalRig.Instance.Recenter();
            PlaceInFrontOfPlayer();
        }

        void OnLeavePressed()
        {
            openedDuringGame = false;
            connection.Leave();
        }

        bool Online => connection != null && connection.State == ConnectionState.Online;

        GamePhase Phase => GameStateManager.Instance != null ? GameStateManager.Instance.Phase.Value : GamePhase.Lobby;

        /// <summary>En partie : le menu est masqué par défaut et s'ouvre au bouton.</summary>
        bool InGame => Online && Phase != GamePhase.Lobby && Phase != GamePhase.Finished;

        /// <summary>Seul l'hôte (ou le joueur solo) peut abandonner la partie en cours : ça ramène toute l'équipe à la surface.</summary>
        bool CanAbandon => InGame && Phase != GamePhase.Descending
                           && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost;

        void Refresh()
        {
            if (connection == null) return;
            GameStateManager state = GameStateManager.Instance;
            GamePhase phase = Phase;

            bool visible = !InGame || openedDuringGame;
            panel.SetActive(visible);
            if (visible && !wasVisible) PlaceInFrontOfPlayer();
            wasVisible = visible;

            offlineGroup.SetActive(!connection.IsBusy);
            onlineGroup.SetActive(Online);
            if (statusDot != null)
                statusDot.color = connection.State switch
                {
                    ConnectionState.Online => new Color(0.3f, 1f, 0.5f),
                    ConnectionState.Connecting => new Color(1f, 0.75f, 0.2f),
                    ConnectionState.Failed => new Color(1f, 0.3f, 0.25f),
                    _ => new Color(0.55f, 0.6f, 0.65f),
                };

            bool finished = phase == GamePhase.Finished;
            bool everyoneReady = state != null && state.EveryoneReady;
            bool abandon = CanAbandon;
            startButton.gameObject.SetActive(Online && (phase == GamePhase.Lobby || finished || abandon));
            startButton.interactable = finished || abandon || everyoneReady;
            if (startLabel != null)
                startLabel.text = abandon ? Loc.T("Recommencer (retour surface)", "Restart (back to surface)")
                    : finished ? Loc.T("Nouvelle plongée", "New dive") : Loc.T("Lancer la plongée", "Start the dive");
            if (languageLabel != null) languageLabel.text = Loc.English ? "Français" : "English";
            bool easy = state != null && state.EasyMode.Value;
            if (easyButton != null) easyButton.gameObject.SetActive(Online && phase == GamePhase.Lobby);
            if (easyLabel != null) easyLabel.text = easy ? Loc.T("Mode facile : OUI", "Easy mode: ON") : Loc.T("Mode facile : non", "Easy mode: off");
            if (leaderboardText != null)
            {
                leaderboardText.gameObject.SetActive(finished && state != null);
                if (finished && state != null) leaderboardText.text = Leaderboard.Format(state.Leaderboard.Value.ToString());
            }

            statusText.text = finished && state != null
                ? Loc.T("Remontée réussie en ", "Ascent completed in ") + FormatDuration(state.EndTime.Value - state.StartTime.Value)
                : connection.Status;
            playersText.text = Online ? PlayersLine(state) : string.Empty;
            if (hintText != null) hintText.text = Hint(state, phase);
        }

        static string PlayersLine(GameStateManager state)
        {
            int occupied = NetworkPlayer.OccupiedQuadrantMask();
            int ready = state != null ? CountBits(state.ReadyMask.Value & occupied) : 0;
            return Loc.T("Plongeurs : ", "Divers: ") + NetworkPlayer.All.Count + " / " + GameConfig.Instance.MaxPlayers
                + Loc.T("   ·   en cabine : ", "   ·   in cabin: ") + ready + " / " + CountBits(occupied);
        }

        string Hint(GameStateManager state, GamePhase phase)
        {
            if (!Online) return connection.IsBusy ? string.Empty : Loc.T("Choisis comment rejoindre l'équipe.", "Choose how to join the team.");
            if (InGame) return Loc.T("Bouton MENU de la montre (ou Menu, manette gauche) pour fermer.", "Watch MENU button (or Menu on the left controller) to close.");
            if (phase == GamePhase.Finished) return Loc.T("Bravo ! « Nouvelle plongée » pour recommencer avec la même équipe.", "Well done! “New dive” to play again with the same team.");

            LocalRig rig = LocalRig.Instance;
            bool meReady = state != null && rig != null && PlayArea.IsValid(rig.Quadrant)
                && (state.ReadyMask.Value & (1 << rig.Quadrant)) != 0;
            if (!meReady)
                return Loc.T("Entre dans ta cabine, fais l'entraînement, place-toi sur le repère lumineux face à la porte, puis « Recalibrer ma position ».",
                    "Step into your cabin, do the training, stand on the glowing marker facing the door, then “Recalibrate my position”.");
            return state.EveryoneReady
                ? Loc.T("Tout le monde est à bord : lancez la plongée !", "Everyone is aboard: start the dive!")
                : Loc.T("Tu es à bord. On attend les autres plongeurs…", "You are aboard. Waiting for the other divers…");
        }

        void PlaceInFrontOfPlayer()
        {
            LocalRig rig = LocalRig.Instance;
            if (rig == null) return;
            Transform head = rig.Head;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            transform.SetPositionAndRotation(head.position + forward * distance + Vector3.up * heightOffset, Quaternion.LookRotation(forward, Vector3.up));
        }

        static int CountBits(int value)
        {
            int count = 0;
            for (; value != 0; value &= value - 1) count++;
            return count;
        }

        static string FormatDuration(double seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
            return (total / 60).ToString(CultureInfo.InvariantCulture) + " min " + (total % 60).ToString("00", CultureInfo.InvariantCulture) + " s";
        }
    }
}
