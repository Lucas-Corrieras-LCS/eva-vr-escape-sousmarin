using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Abysses.Core;
using Abysses.Level;
using Abysses.Network;
using Abysses.Player;

namespace Abysses.UI
{
    /// <summary>
    /// Menu VR (canvas World Space attaché au rig). Visible avant la plongée et à la fin.
    /// Entièrement piloté par événements : connexion, arrivée/départ de joueurs, phase de jeu.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text playersText;

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

        ConnectionManager connection;

        void Start()
        {
            connection = ConnectionManager.Instance;
            joinOnlineButton.onClick.AddListener(connection.JoinOnline);
            hostLanButton.onClick.AddListener(connection.HostLan);
            joinLanButton.onClick.AddListener(connection.JoinLan);
            soloButton.onClick.AddListener(connection.PlaySolo);
            leaveButton.onClick.AddListener(connection.Leave);
            startButton.onClick.AddListener(OnStartPressed);
            recenterButton.onClick.AddListener(OnRecenterPressed);

            connection.StateChanged += Refresh;
            NetworkPlayer.Spawned += OnPlayersChanged;
            NetworkPlayer.Despawned += OnPlayersChanged;
            GameStateManager.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (connection != null) connection.StateChanged -= Refresh;
            NetworkPlayer.Spawned -= OnPlayersChanged;
            NetworkPlayer.Despawned -= OnPlayersChanged;
            GameStateManager.PhaseChanged -= OnPhaseChanged;
        }

        void OnPlayersChanged(NetworkPlayer player) => Refresh();
        void OnPhaseChanged(GamePhase phase) => Refresh();

        void OnStartPressed()
        {
            if (GameStateManager.Instance != null) GameStateManager.Instance.RequestStartRpc();
        }

        void OnRecenterPressed()
        {
            if (LocalRig.Instance != null) LocalRig.Instance.Recenter();
        }

        void Refresh()
        {
            bool online = connection.State == ConnectionState.Online;
            GameStateManager state = GameStateManager.Instance;
            GamePhase phase = state != null ? state.Phase.Value : GamePhase.Lobby;

            panel.SetActive(!online || phase == GamePhase.Lobby || phase == GamePhase.Finished);
            offlineGroup.SetActive(!connection.IsBusy);
            onlineGroup.SetActive(online);
            startButton.interactable = online && phase == GamePhase.Lobby;

            statusText.text = phase == GamePhase.Finished && state != null
                ? "Remontée réussie en " + FormatDuration(state.EndTime.Value - state.StartTime.Value)
                : connection.Status;

            playersText.text = online
                ? "Plongeurs : " + NetworkPlayer.All.Count + " / " + GameConfig.Instance.MaxPlayers
                : string.Empty;
        }

        static string FormatDuration(double seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
            return (total / 60).ToString(CultureInfo.InvariantCulture) + " min " + (total % 60).ToString("00", CultureInfo.InvariantCulture) + " s";
        }
    }
}
