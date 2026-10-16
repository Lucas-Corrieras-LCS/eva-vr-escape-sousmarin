using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Abysses.Network
{
    /// <summary>
    /// Découverte automatique de l'hôte en réseau local (broadcast UDP), pour jouer sans Internet.
    /// Non bloquant : aucune thread, lecture du socket dans Update uniquement quand des données sont disponibles.
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        const string Signature = "ABYSSES1";
        const float BroadcastInterval = 1f;

        UdpClient socket;
        IPEndPoint broadcastEndPoint;
        byte[] announcement;
        float nextBroadcast;
        bool broadcasting;

        public bool IsActive => socket != null;

        /// <summary>Hôte trouvé : adresse IP et port de jeu.</summary>
        public event Action<string, ushort> HostFound;

        public void StartBroadcasting(ushort discoveryPort, ushort gamePort)
        {
            Stop();
            try
            {
                socket = new UdpClient { EnableBroadcast = true };
                broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
                announcement = Encoding.ASCII.GetBytes(Signature + ":" + gamePort);
                broadcasting = true;
                nextBroadcast = 0f;
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Abysses] Annonce LAN impossible : " + e.Message);
                Stop();
            }
        }

        public void StartListening(ushort discoveryPort)
        {
            Stop();
            try
            {
                socket = new UdpClient();
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
                socket.EnableBroadcast = true;
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Abysses] Écoute LAN impossible : " + e.Message);
                Stop();
            }
        }

        public void Stop()
        {
            broadcasting = false;
            if (socket == null) return;
            socket.Close();
            socket = null;
        }

        void Update()
        {
            if (socket == null) return;
            try
            {
                if (broadcasting)
                {
                    if (Time.unscaledTime < nextBroadcast) return;
                    socket.Send(announcement, announcement.Length, broadcastEndPoint);
                    nextBroadcast = Time.unscaledTime + BroadcastInterval;
                    return;
                }

                while (socket != null && socket.Available > 0)
                {
                    IPEndPoint sender = null;
                    byte[] data = socket.Receive(ref sender);
                    if (TryParse(data, out ushort gamePort))
                        HostFound?.Invoke(sender.Address.ToString(), gamePort);
                }
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Abysses] Erreur découverte LAN : " + e.Message);
                Stop();
            }
        }

        static bool TryParse(byte[] data, out ushort gamePort)
        {
            gamePort = 0;
            string message = Encoding.ASCII.GetString(data);
            string prefix = Signature + ":";
            return message.StartsWith(prefix, StringComparison.Ordinal)
                && ushort.TryParse(message.Substring(prefix.Length), out gamePort);
        }

        void OnDestroy() => Stop();
    }
}
