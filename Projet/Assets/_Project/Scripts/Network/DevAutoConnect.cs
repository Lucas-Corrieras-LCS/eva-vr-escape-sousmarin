using UnityEngine;
#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif

namespace Abysses.Network
{
    /// <summary>
    /// Tests multijoueur dans l'éditeur (Multiplayer Play Mode) : une instance portant le tag « Host » héberge
    /// une partie locale, une instance portant le tag « Client » rejoint l'hôte sur cette machine (avec relances).
    /// Sans tag, ou hors de l'éditeur, ce composant ne fait rien.
    /// </summary>
    public class DevAutoConnect : MonoBehaviour
    {
        public const string HostTag = "Host";
        public const string ClientTag = "Client";

        [SerializeField] ConnectionManager connection;
        [SerializeField, Min(0.5f)] float retryDelay = 2f;
        [SerializeField, Min(1)] int maxAttempts = 15;

#if UNITY_EDITOR
        bool asHost;
        int attempts;
        float nextAttempt;

        void Start()
        {
            bool asClient = false;
            var tags = CurrentPlayer.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                asHost |= tags[i] == HostTag;
                asClient |= tags[i] == ClientTag;
            }
            enabled = asHost || asClient;
            nextAttempt = Time.unscaledTime + (asHost ? 0.5f : 3f);
        }

        void Update()
        {
            if (Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + retryDelay;

            if (connection.State == ConnectionState.Online)
            {
                enabled = false;
                return;
            }
            if (connection.IsBusy) return;
            if (attempts++ >= maxAttempts)
            {
                Debug.LogWarning("[Abysses] Connexion automatique abandonnée après " + maxAttempts + " essais.");
                enabled = false;
                return;
            }

            if (asHost) connection.HostLan();
            else connection.JoinDirect("127.0.0.1");
        }
#endif
    }
}
