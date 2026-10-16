using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abysses.Core;

namespace Abysses.Network
{
    public enum ConnectionState { Offline, Connecting, Online, Failed }
    public enum ConnectionMode { None, Online, LanHost, LanClient, Solo }

    /// <summary>
    /// Point d'entrée réseau du jeu, appelé par le menu VR :
    /// - En ligne : Relay via Multiplayer Services, une seule action « Rejoindre » (crée ou rejoint la session nommée).
    /// - Réseau local : hôte annoncé en broadcast UDP, les clients le trouvent seuls (aucune saisie d'IP en VR).
    /// - Solo : repli mono-joueur (même build, quadrant 0).
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;
        [SerializeField] UnityTransport transport;
        [SerializeField] LanDiscovery lanDiscovery;

        public static ConnectionManager Instance { get; private set; }

        public ConnectionState State { get; private set; } = ConnectionState.Offline;
        public ConnectionMode Mode { get; private set; } = ConnectionMode.None;
        public string Status { get; private set; } = "Hors ligne";

        /// <summary>Déclenché à chaque changement d'état (pour l'UI, pas de polling).</summary>
        public event Action StateChanged;

        GameConfig config;
        ISession session;
        float lanJoinDeadline;

        void Awake()
        {
            Instance = this;
            config = GameConfig.Instance;

            networkManager.NetworkConfig.TickRate = (uint)config.NetworkTickRate;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;

            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientStopped += OnClientStopped;
            lanDiscovery.HostFound += OnLanHostFound;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= OnServerStarted;
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientStopped -= OnClientStopped;
            }
            if (lanDiscovery != null) lanDiscovery.HostFound -= OnLanHostFound;
        }

        // ---------- Actions du menu ----------

        /// <summary>Partie en ligne via Relay : le premier casque crée la session, les suivants la rejoignent.</summary>
        public async void JoinOnline()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.Online;
            SetState(ConnectionState.Connecting, "Connexion aux services Unity…");
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                SetState(ConnectionState.Connecting, "Recherche de l'équipe…");
                var options = new SessionOptions { MaxPlayers = config.MaxPlayers, IsPrivate = false }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(config.SessionName, options);
                // L'état passe à Online dans OnClientConnected, une fois Netcode réellement connecté.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                session = null;
                Mode = ConnectionMode.None;
                SetState(ConnectionState.Failed, "Connexion en ligne impossible.\nEssayez le réseau local.");
            }
        }

        /// <summary>Héberge une partie sur le réseau local et l'annonce aux autres casques.</summary>
        public void HostLan()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.LanHost;
            SetState(ConnectionState.Connecting, "Création de la partie locale…");
            transport.SetConnectionData("127.0.0.1", config.LanPort, "0.0.0.0");
            if (!networkManager.StartHost()) Fail("Impossible d'héberger la partie locale.");
        }

        /// <summary>Cherche un hôte sur le réseau local puis s'y connecte automatiquement.</summary>
        public void JoinLan()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.LanClient;
            SetState(ConnectionState.Connecting, "Recherche d'une partie locale…");
            lanJoinDeadline = Time.unscaledTime + config.LanJoinTimeout;
            lanDiscovery.StartListening(config.LanDiscoveryPort);
            if (!lanDiscovery.IsActive) Fail("Réseau local indisponible.");
        }

        /// <summary>Repli mono-joueur : hôte local sans annonce.</summary>
        public void PlaySolo()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.Solo;
            SetState(ConnectionState.Connecting, "Lancement en solo…");
            transport.SetConnectionData("127.0.0.1", config.LanPort, "127.0.0.1");
            if (!networkManager.StartHost()) Fail("Impossible de lancer la partie solo.");
        }

        /// <summary>Quitte la partie, quel que soit le mode.</summary>
        public async void Leave()
        {
            Mode = ConnectionMode.None;
            SetState(ConnectionState.Offline, "Hors ligne");
            lanDiscovery.Stop();

            if (session != null)
            {
                ISession leaving = session;
                session = null;
                try { await leaving.LeaveAsync(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (networkManager.IsListening) networkManager.Shutdown();
        }

        public bool IsBusy => State == ConnectionState.Connecting || State == ConnectionState.Online;

        // ---------- Netcode ----------

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasRoom = networkManager.ConnectedClientsIds.Count < config.MaxPlayers;
            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;
            response.Reason = hasRoom ? string.Empty : "Partie complète";
            response.Pending = false;
        }

        void OnServerStarted()
        {
            // Les clients gardent la scène Station déjà chargée et ne chargent que les étages en additif.
            networkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            if (Mode == ConnectionMode.LanHost)
                lanDiscovery.StartBroadcasting(config.LanDiscoveryPort, config.LanPort);
        }

        void OnClientConnected(ulong clientId)
        {
            if (clientId != networkManager.LocalClientId) return;
            string message = Mode switch
            {
                ConnectionMode.Solo => "Partie solo",
                ConnectionMode.LanHost => "Partie locale créée — en attente de l'équipe",
                _ => networkManager.IsHost ? "Partie créée — en attente de l'équipe" : "Équipe rejointe",
            };
            SetState(ConnectionState.Online, message);
        }

        void OnClientStopped(bool wasHost)
        {
            lanDiscovery.Stop();
            // Arrêt volontaire (Leave) : l'état est déjà Offline.
            if (State == ConnectionState.Offline) return;
            session = null;
            Mode = ConnectionMode.None;
            SetState(ConnectionState.Failed, "Connexion perdue.");
        }

        void OnLanHostFound(string address, ushort port)
        {
            if (Mode != ConnectionMode.LanClient || State != ConnectionState.Connecting) return;
            lanDiscovery.Stop();
            SetState(ConnectionState.Connecting, "Partie trouvée, connexion…");
            transport.SetConnectionData(address, port);
            if (!networkManager.StartClient()) Fail("Connexion à la partie locale impossible.");
        }

        void Update()
        {
            // Seul cas temporisé : l'attente d'un hôte LAN.
            if (Mode != ConnectionMode.LanClient || State != ConnectionState.Connecting || networkManager.IsClient) return;
            if (Time.unscaledTime > lanJoinDeadline)
            {
                lanDiscovery.Stop();
                Fail("Aucune partie trouvée sur le réseau local.");
            }
        }

        void Fail(string message)
        {
            Mode = ConnectionMode.None;
            SetState(ConnectionState.Failed, message);
        }

        void SetState(ConnectionState state, string status)
        {
            State = state;
            Status = status;
            StateChanged?.Invoke();
        }
    }
}
