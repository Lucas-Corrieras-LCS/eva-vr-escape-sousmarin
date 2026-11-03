using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;

namespace Abysses.UI
{
    /// <summary>
    /// Menu VR (canvas World Space porté par le rig).
    /// - Toujours visible hors ligne, à la surface et à la fin ; en cours de partie, il s'ouvre et se ferme
    ///   avec le bouton Menu de la manette gauche (touche M dans l'éditeur) pour recalibrer ou quitter.
    /// - À chaque ouverture, il se place devant le regard du joueur.
    /// - Guide la calibration : le départ n'est possible que lorsque chaque plongeur est dans sa cabine.
    /// Entièrement piloté par événements : connexion, joueurs, phase, présence en cabine.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text playersText;
        [Tooltip("Consigne contextuelle (calibration, attente des autres…).")]
        [SerializeField] TMP_Text hintText;

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

        [Header("Placement")]
        [SerializeField, Min(0.3f)] float distance = 0.75f;
        [SerializeField] float heightOffset = -0.12f;

        ConnectionManager connection;
        TMP_Text startLabel;
        InputAction toggleAction;
        bool openedDuringGame;
        bool wasVisible;

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
        }

        void OnPlayersChanged(NetworkPlayer player) => Refresh();
        void OnReadyChanged(int readyMask) => Refresh();

        void OnPhaseChanged(GamePhase phase)
        {
            openedDuringGame = false;
            Refresh();
        }

        void OnQuadrantAssigned(int quadrant)
        {
            // Après calibration, le menu se replace devant le joueur.
            PlaceInFrontOfPlayer();
        }

        void OnToggle(InputAction.CallbackContext context)
        {
            if (!InGame) return;
            openedDuringGame = !openedDuringGame;
            Refresh();
        }

        void OnStartPressed()
        {
            GameStateManager state = GameStateManager.Instance;
            if (state == null) return;
            if (state.Phase.Value == GamePhase.Finished) state.RequestRestartRpc();
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

            bool finished = phase == GamePhase.Finished;
            bool everyoneReady = state != null && state.EveryoneReady;
            startButton.gameObject.SetActive(Online && (phase == GamePhase.Lobby || finished));
            startButton.interactable = finished || everyoneReady;
            if (startLabel != null) startLabel.text = finished ? "Nouvelle plongée" : "Lancer la plongée";

            statusText.text = finished && state != null
                ? "Remontée réussie en " + FormatDuration(state.EndTime.Value - state.StartTime.Value)
                : connection.Status;
            playersText.text = Online ? PlayersLine(state) : string.Empty;
            if (hintText != null) hintText.text = Hint(state, phase);
        }

        static string PlayersLine(GameStateManager state)
        {
            int occupied = NetworkPlayer.OccupiedQuadrantMask();
            int ready = state != null ? CountBits(state.ReadyMask.Value & occupied) : 0;
            return "Plongeurs : " + NetworkPlayer.All.Count + " / " + GameConfig.Instance.MaxPlayers
                + "   ·   en cabine : " + ready + " / " + CountBits(occupied);
        }

        string Hint(GameStateManager state, GamePhase phase)
        {
            if (!Online) return connection.IsBusy ? string.Empty : "Choisis comment rejoindre l'équipe.";
            if (InGame) return "Bouton Menu (manette gauche) pour fermer.";
            if (phase == GamePhase.Finished) return "Bravo ! « Nouvelle plongée » pour recommencer avec la même équipe.";

            LocalRig rig = LocalRig.Instance;
            bool meReady = state != null && rig != null && PlayArea.IsValid(rig.Quadrant)
                && (state.ReadyMask.Value & (1 << rig.Quadrant)) != 0;
            if (!meReady)
                return "Entre dans ta cabine, place-toi sur le repère lumineux face à la porte, puis « Recalibrer ma position ».";
            return state.EveryoneReady
                ? "Tout le monde est à bord : lancez la plongée !"
                : "Tu es à bord. On attend les autres plongeurs…";
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
