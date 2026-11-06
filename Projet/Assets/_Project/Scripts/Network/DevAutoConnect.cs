using System;
using UnityEngine;
using Abysses.Level;
#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif

namespace Abysses.Network
{
    /// <summary>
    /// Connexion automatique pour les tests multijoueur :
    /// - dans l'éditeur (Multiplayer Play Mode) : une instance portant le tag « Host » héberge une partie locale,
    ///   une instance portant le tag « Client » rejoint l'hôte sur cette machine (avec relances) ;
    /// - dans un build de test (ligne de commande) : <c>-abysses-join 127.0.0.1</c> rejoint une adresse,
    ///   <c>-abysses-join-lan</c> cherche l'hôte par la découverte réseau local (comme « Rejoindre (réseau local) » au casque),
    ///   <c>-abysses-player</c> force un plongeur sur un PC sans casque (sinon le pupitre s'active),
    ///   <c>-abysses-spectator</c> rejoint comme pupitre (sans avatar), <c>-abysses-gm-hint</c> fait demander
    ///   un indice par le pupitre toutes les 10 s pendant les énigmes (test des droits du maître du jeu).
    /// Sans tag ni argument, ce composant ne fait rien.
    /// </summary>
    public class DevAutoConnect : MonoBehaviour
    {
        public const string HostTag = "Host";
        public const string ClientTag = "Client";

        [SerializeField] ConnectionManager connection;
        [SerializeField, Min(0.5f)] float retryDelay = 2f;
        [SerializeField, Min(1)] int maxAttempts = 15;

        bool asHost;
        string joinAddress;
        bool requestHints;
        bool joinLan;
        int attempts;
        float nextAttempt;
        float nextHint;

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-abysses-join" && i + 1 < args.Length) joinAddress = args[i + 1];
                else if (args[i] == "-abysses-join-lan") joinLan = true;
                else if (args[i] == "-abysses-spectator") connection.Spectator = true;
                else if (args[i] == "-abysses-gm-hint") requestHints = true;
            }
#if UNITY_EDITOR
            var tags = CurrentPlayer.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                asHost |= tags[i] == HostTag;
                if (tags[i] == ClientTag) joinAddress = "127.0.0.1";
            }
#endif
            enabled = asHost || joinAddress != null || joinLan;
            nextAttempt = Time.unscaledTime + (asHost ? 0.5f : 3f);
        }

        void Update()
        {
            if (connection.State == ConnectionState.Online)
            {
                if (requestHints) RequestHint();
                else enabled = false;
                return;
            }
            if (Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + retryDelay;
            if (connection.IsBusy) return;
            if (attempts++ >= maxAttempts)
            {
                Debug.LogWarning("[Abysses] Connexion automatique abandonnée après " + maxAttempts + " essais.");
                enabled = false;
                return;
            }

            if (asHost) connection.HostLan();
            else if (joinLan) connection.JoinLan();
            else connection.JoinDirect(joinAddress);
        }

        void RequestHint()
        {
            GameStateManager state = GameStateManager.Instance;
            if (Time.unscaledTime < nextHint || state == null || state.Phase.Value != GamePhase.Playing || HintSystem.Instance == null) return;
            nextHint = Time.unscaledTime + 10f;
            HintSystem.Instance.RequestHintRpc();
        }
    }
}
