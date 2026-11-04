using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using Abysses.Core;
using Abysses.Level;
using Abysses.Player;

namespace Abysses.Network
{
    public enum ConnectionState { Offline, Connecting, Online, Failed }
    public enum ConnectionMode { None, Online, LanHost, LanClient, Solo }

    /// <summary>
    /// Point d'entrée réseau du jeu, appelé par le menu VR :
    /// - En ligne : Relay via Multiplayer Services, une seule action « Rejoindre » (crée ou rejoint la session nommée).
    /// - Réseau local : hôte annoncé en broadcast UDP, les clients le trouvent seuls (aucune saisie d'IP en VR).
    /// - Solo : repli mono-joueur (même build, quadrant 0).
    /// - Spectateur : le pupitre du maître du jeu (PC) rejoint ou héberge sans avatar ni quadrant, même en cours de partie.
    /// - Reconnexion : un casque qui décroche en pleine partie se reconnecte seul et retrouve son quadrant.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManager;
        [SerializeField] UnityTransport transport;
        [SerializeField] LanDiscovery lanDiscovery;

        public static ConnectionManager Instance { get; private set; }

        const int ReconnectAttempts = 5;
        const float ReconnectDelay = 3f;
        const byte NoQuadrant = 255;
        /// <summary>Refus volontaires de l'hôte : seuls motifs pour lesquels un casque ne retente pas sa connexion.</summary>
        const string ReasonFull = "Partie complète / Game full";
        const string ReasonStarted = "Plongée déjà commencée / Dive already started";

        /// <summary>Ce poste est le pupitre du maître du jeu : pas d'avatar, pas de quadrant.</summary>
        public bool Spectator { get; set; }

        // Serveur : clients spectateurs (autorisés à piloter la partie) et quadrant redemandé par un casque qui revient.
        static readonly HashSet<ulong> gameMasters = new HashSet<ulong>();
        static readonly Dictionary<ulong, int> preferredQuadrants = new Dictionary<ulong, int>();

        /// <summary>Serveur : l'hôte et les pupitres peuvent piloter la partie (indices, passer une énigme, relancer).</summary>
        public static bool IsGameMaster(ulong clientId) => clientId == NetworkManager.ServerClientId || gameMasters.Contains(clientId);

        /// <summary>Serveur : quadrant que ce client occupait avant de décrocher (-1 si aucun).</summary>
        public static int PreferredQuadrant(ulong clientId) => preferredQuadrants.TryGetValue(clientId, out int q) ? q : -1;

        public ConnectionState State { get; private set; } = ConnectionState.Offline;
        public ConnectionMode Mode { get; private set; } = ConnectionMode.None;
        public string Status => Loc.T(statusFrench, statusEnglish);
        string statusFrench = "Hors ligne";
        string statusEnglish = "Offline";

        /// <summary>Déclenché à chaque changement d'état (pour l'UI, pas de polling).</summary>
        public event Action StateChanged;

        GameConfig config;
        ISession session;
        float lanJoinDeadline;
        string lastAddress;
        string lastSessionId;
        ConnectionMode lastMode;
        int lastQuadrant = -1;
        int reconnectsLeft;
        bool reconnecting;

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
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            lanDiscovery.HostFound += OnLanHostFound;
            LocalRig.QuadrantAssigned += OnQuadrantAssigned;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= OnServerStarted;
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientStopped -= OnClientStopped;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (lanDiscovery != null) lanDiscovery.HostFound -= OnLanHostFound;
            LocalRig.QuadrantAssigned -= OnQuadrantAssigned;
        }

        void OnQuadrantAssigned(int quadrant) => lastQuadrant = quadrant;

        /// <summary>Données envoyées à l'hôte à la connexion : spectateur ? quadrant à retrouver ?</summary>
        void PreparePayload()
        {
            byte quadrant = reconnecting && PlayArea.IsValid(lastQuadrant) ? (byte)lastQuadrant : NoQuadrant;
            networkManager.NetworkConfig.ConnectionData = new[] { Spectator ? (byte)1 : (byte)0, quadrant };
        }

        // ---------- Actions du menu ----------

        /// <summary>Partie en ligne via Relay : le premier casque crée la session, les suivants la rejoignent.</summary>
        public async void JoinOnline()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.Online;
            SetState(ConnectionState.Connecting, "Connexion aux services Unity…", "Connecting to Unity services…");
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                SetState(ConnectionState.Connecting, "Recherche de l'équipe…", "Looking for the team…");
                PreparePayload();
                if (reconnecting && !string.IsNullOrEmpty(lastSessionId))
                {
                    session = await MultiplayerService.Instance.JoinSessionByIdAsync(lastSessionId);
                }
                else
                {
                    // Les pupitres spectateurs ne comptent pas comme plongeurs : une place de plus dans la session.
                    var options = new SessionOptions { MaxPlayers = config.MaxPlayers + 1, IsPrivate = false }.WithRelayNetwork();
                    session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(config.SessionName, options);
                }
                lastSessionId = session.Id;
                // L'état passe à Online dans OnClientConnected, une fois Netcode réellement connecté.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                session = null;
                Fail("Connexion en ligne impossible.\nEssayez le réseau local.", "Online connection failed.\nTry the local network.");
            }
        }

        /// <summary>Héberge une partie sur le réseau local et l'annonce aux autres casques.</summary>
        public void HostLan()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.LanHost;
            SetState(ConnectionState.Connecting, "Création de la partie locale…", "Creating the local game…");
            transport.SetConnectionData("127.0.0.1", config.LanPort, "0.0.0.0");
            PreparePayload();
            if (!networkManager.StartHost()) Fail("Impossible d'héberger la partie locale.", "Could not host the local game.");
        }

        /// <summary>Cherche un hôte sur le réseau local puis s'y connecte automatiquement.</summary>
        public void JoinLan()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.LanClient;
            SetState(ConnectionState.Connecting, "Recherche d'une partie locale…", "Looking for a local game…");
            lanJoinDeadline = Time.unscaledTime + config.LanJoinTimeout;
            lanDiscovery.StartListening(config.LanDiscoveryPort);
            if (!lanDiscovery.IsActive) Fail("Réseau local indisponible.", "Local network unavailable.");
        }

        /// <summary>Rejoint directement une adresse connue (tests multijoueur dans l'éditeur, secours si la découverte LAN est bloquée).</summary>
        public void JoinDirect(string address)
        {
            if (IsBusy) return;
            Mode = ConnectionMode.LanClient;
            SetState(ConnectionState.Connecting, "Connexion à " + address + "…", "Connecting to " + address + "…");
            lanJoinDeadline = Time.unscaledTime + config.LanJoinTimeout;
            lastAddress = address;
            transport.SetConnectionData(address, config.LanPort);
            PreparePayload();
            if (!networkManager.StartClient()) Fail("Connexion à " + address + " impossible.", "Could not connect to " + address + ".");
        }

        /// <summary>Repli mono-joueur : hôte local sans annonce.</summary>
        public void PlaySolo()
        {
            if (IsBusy) return;
            Mode = ConnectionMode.Solo;
            SetState(ConnectionState.Connecting, "Lancement en solo…", "Starting solo…");
            transport.SetConnectionData("127.0.0.1", config.LanPort, "127.0.0.1");
            PreparePayload();
            if (!networkManager.StartHost()) Fail("Impossible de lancer la partie solo.", "Could not start the solo game.");
        }

        /// <summary>Quitte la partie, quel que soit le mode.</summary>
        public async void Leave()
        {
            StopReconnecting();
            Mode = ConnectionMode.None;
            SetState(ConnectionState.Offline, "Hors ligne", "Offline");
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
            // L'hôte passe aussi par ici (avant que l'état de partie n'existe) : toujours accepté.
            bool isHost = request.ClientNetworkId == NetworkManager.ServerClientId;
            byte[] payload = request.Payload;
            bool spectator = payload != null && payload.Length > 0 && payload[0] == 1;
            int wanted = payload != null && payload.Length > 1 && payload[1] != NoQuadrant ? payload[1] : -1;

            // Spectateur (pupitre) : toujours accepté, sans avatar, et autorisé à piloter la partie.
            if (spectator)
            {
                if (!isHost) gameMasters.Add(request.ClientNetworkId);
                response.Approved = true;
                response.CreatePlayerObject = false;
                response.Pending = false;
                return;
            }

            GameStateManager state = GameStateManager.Instance;
            bool hasRoom = NetworkPlayer.All.Count < config.MaxPlayers;
            bool inLobby = state == null || state.Phase.Value == GamePhase.Lobby;
            // Retour d'un casque qui a décroché : son quadrant est libre (même si son module a été résolu entre-temps).
            bool rejoin = !inLobby && PlayArea.IsValid(wanted) && (NetworkPlayer.OccupiedQuadrantMask() & (1 << wanted)) == 0;

            response.Approved = isHost || (hasRoom && (inLobby || rejoin));
            response.CreatePlayerObject = response.Approved;
            response.Reason = response.Approved ? string.Empty : !hasRoom ? ReasonFull : ReasonStarted;
            response.Pending = false;
            if (response.Approved && PlayArea.IsValid(wanted)) preferredQuadrants[request.ClientNetworkId] = wanted;
        }

        void OnServerStarted()
        {
            // Les clients gardent la scène Station déjà chargée et ne chargent que les étages en additif.
            networkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            if (Mode == ConnectionMode.LanHost)
                lanDiscovery.StartBroadcasting(config.LanDiscoveryPort, config.LanPort);
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (!networkManager.IsServer) return;
            gameMasters.Remove(clientId);
            preferredQuadrants.Remove(clientId);
        }

        void OnClientConnected(ulong clientId)
        {
            if (clientId != networkManager.LocalClientId) return;
            StopReconnecting();
            lastMode = Mode;
            if (Spectator) SetState(ConnectionState.Online, "Pupitre connecté", "Console connected");
            else if (Mode == ConnectionMode.Solo) SetState(ConnectionState.Online, "Partie solo", "Solo game");
            else if (networkManager.IsHost) SetState(ConnectionState.Online, "Partie créée — en attente de l'équipe", "Game created — waiting for the team");
            else SetState(ConnectionState.Online, "Équipe rejointe", "Team joined");
        }

        void OnClientStopped(bool wasHost)
        {
            lanDiscovery.Stop();
            UnloadFloors();
            if (networkManager.IsServer || wasHost)
            {
                gameMasters.Clear();
                preferredQuadrants.Clear();
            }
            // Arrêt volontaire (Leave) : l'état est déjà Offline.
            if (State == ConnectionState.Offline) return;
            bool wasOnline = State == ConnectionState.Online;
            session = null;
            Mode = ConnectionMode.None;
            // Motif envoyé par l'hôte en cas de refus (partie complète, plongée commencée…).
            string reason = networkManager.DisconnectReason;

            // Casque client qui décroche (Wi-Fi, délai dépassé, coupure côté hôte) : on retente seul, il retrouvera son quadrant.
            // Netcode remplit aussi le motif pour une simple coupure : seuls nos refus d'admission arrêtent les essais.
            bool refused = reason == ReasonFull || reason == ReasonStarted;
            bool canRetry = !wasHost && !refused
                && (lastMode == ConnectionMode.LanClient || lastMode == ConnectionMode.Online);
            if (canRetry && (wasOnline || reconnecting))
            {
                if (!reconnecting)
                {
                    reconnecting = true;
                    reconnectsLeft = ReconnectAttempts;
                }
                if (reconnectsLeft-- > 0)
                {
                    SetState(ConnectionState.Failed, "Connexion perdue — reconnexion…", "Connection lost — reconnecting…");
                    StartCoroutine(Reconnect());
                    return;
                }
                StopReconnecting();
            }
            SetState(ConnectionState.Failed, refused ? "Refusé : " + reason : "Connexion perdue.",
                refused ? "Refused: " + reason : "Connection lost.");
        }

        /// <summary>
        /// Netcode ne décharge pas les étages chargés en additif quand la session s'arrête : sans ça, un étage resterait
        /// affiché (brouillard compris) et la partie suivante en chargerait un second par-dessus.
        /// </summary>
        void UnloadFloors()
        {
            Scene station = gameObject.scene;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != station && scene.isLoaded && scene.name.StartsWith("Etage", StringComparison.Ordinal))
                    SceneManager.UnloadSceneAsync(scene);
            }
        }

        IEnumerator Reconnect()
        {
            yield return new WaitForSecondsRealtime(ReconnectDelay);
            if (!reconnecting || State != ConnectionState.Failed) yield break;
            if (lastMode == ConnectionMode.Online) JoinOnline();
            else if (!string.IsNullOrEmpty(lastAddress)) JoinDirect(lastAddress);
            else StopReconnecting();
        }

        void StopReconnecting()
        {
            reconnecting = false;
            reconnectsLeft = 0;
        }

        void OnLanHostFound(string address, ushort port)
        {
            if (Mode != ConnectionMode.LanClient || State != ConnectionState.Connecting) return;
            lanDiscovery.Stop();
            SetState(ConnectionState.Connecting, "Partie trouvée, connexion…", "Game found, connecting…");
            lastAddress = address;
            transport.SetConnectionData(address, port);
            PreparePayload();
            if (!networkManager.StartClient()) Fail("Connexion à la partie locale impossible.", "Could not join the local game.");
        }

        void Update()
        {
            // Seul cas temporisé : l'attente d'un hôte LAN.
            if (Mode != ConnectionMode.LanClient || State != ConnectionState.Connecting || networkManager.IsClient) return;
            if (Time.unscaledTime > lanJoinDeadline)
            {
                lanDiscovery.Stop();
                Fail("Aucune partie trouvée sur le réseau local.", "No game found on the local network.");
            }
        }

        void Fail(string message, string english)
        {
            Mode = ConnectionMode.None;
            if (reconnecting && reconnectsLeft-- > 0)
            {
                SetState(ConnectionState.Failed, "Connexion perdue — reconnexion…", "Connection lost — reconnecting…");
                StartCoroutine(Reconnect());
                return;
            }
            StopReconnecting();
            SetState(ConnectionState.Failed, message, english);
        }

        void SetState(ConnectionState state, string status, string english)
        {
            State = state;
            statusFrench = status;
            statusEnglish = english;
            // Trace dans le journal (logcat sur le Quest) : indispensable pour diagnostiquer une connexion chez EVA.
            Debug.Log($"[Abysses] Connexion : {state} — {status}");
            StateChanged?.Invoke();
        }
    }
}
