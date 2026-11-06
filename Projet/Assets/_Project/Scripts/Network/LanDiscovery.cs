using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Abysses.Network
{
    /// <summary>
    /// Découverte automatique de l'hôte en réseau local, pour jouer sans Internet (Wi-Fi d'EVA, partage de connexion).
    /// Deux canaux, pour passer sur tous les réseaux :
    /// - l'hôte annonce sa partie chaque seconde en broadcast (255.255.255.255 ET l'adresse de broadcast de chaque
    ///   réseau, que certains points d'accès et partages de connexion exigent) ;
    /// - le casque qui cherche envoie lui-même une demande en broadcast, et l'hôte lui répond directement (unicast).
    /// Sur Quest (Android), le Wi-Fi jette les broadcasts reçus tant que l'application ne tient pas un « verrou
    /// multicast » : il est pris pendant la recherche et l'annonce (permission ajoutée au manifeste par le build).
    /// Non bloquant : aucune thread, lecture des sockets dans Update uniquement quand des données sont disponibles.
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        const string Signature = "ABYSSES1";
        const string Probe = "ABYSSES1?";
        const float Interval = 1f;

        /// <summary>Socket principal : annonces (hôte) ou écoute des annonces (client), sur le port de découverte.</summary>
        UdpClient socket;
        /// <summary>Hôte : écoute des demandes des clients (port de découverte + 1).</summary>
        UdpClient probeSocket;
        readonly List<IPEndPoint> targets = new List<IPEndPoint>();
        byte[] announcement;
        byte[] probe;
        float nextSend;
        bool broadcasting;
        ushort port;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject multicastLock;
#endif

        public bool IsActive => socket != null;

        /// <summary>Hôte trouvé : adresse IP et port de jeu.</summary>
        public event Action<string, ushort> HostFound;

        public void StartBroadcasting(ushort discoveryPort, ushort gamePort)
        {
            Stop();
            port = discoveryPort;
            try
            {
                AcquireMulticastLock();
                socket = new UdpClient { EnableBroadcast = true };
                announcement = Encoding.ASCII.GetBytes(Signature + ":" + gamePort);
                BuildTargets(discoveryPort);
                broadcasting = true;
                nextSend = 0f;
                probeSocket = new UdpClient();
                probeSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                probeSocket.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort + 1));
                Debug.Log("[Abysses] Annonce LAN sur " + string.Join(", ", targets) + " · adresses locales : " + string.Join(", ", LocalAddresses()));
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Abysses] Annonce LAN impossible : " + e.Message);
                if (socket == null) Stop();
            }
        }

        public void StartListening(ushort discoveryPort)
        {
            Stop();
            port = discoveryPort;
            try
            {
                AcquireMulticastLock();
                socket = new UdpClient();
                socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
                socket.EnableBroadcast = true;
                probe = Encoding.ASCII.GetBytes(Probe);
                BuildTargets((ushort)(discoveryPort + 1));
                nextSend = 0f;
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
            if (socket != null) socket.Close();
            if (probeSocket != null) probeSocket.Close();
            socket = null;
            probeSocket = null;
            ReleaseMulticastLock();
        }

        void Update()
        {
            if (socket == null) return;
            try
            {
                bool send = Time.unscaledTime >= nextSend;
                if (send) nextSend = Time.unscaledTime + Interval;

                if (broadcasting)
                {
                    if (send) SendAll(socket, announcement);
                    // Demandes des clients : réponse directe à l'expéditeur, sur son port d'écoute.
                    while (probeSocket != null && probeSocket.Available > 0)
                    {
                        IPEndPoint sender = null;
                        byte[] data = probeSocket.Receive(ref sender);
                        if (Encoding.ASCII.GetString(data) == Probe)
                            socket.Send(announcement, announcement.Length, new IPEndPoint(sender.Address, port));
                    }
                    return;
                }

                if (send) SendAll(socket, probe);
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

        void SendAll(UdpClient client, byte[] message)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                // Une adresse refusée (interface tombée) ne doit pas empêcher les autres.
                try { client.Send(message, message.Length, targets[i]); }
                catch (SocketException) { }
            }
        }

        /// <summary>Broadcast général + broadcast dirigé de chaque réseau IPv4 actif (ex. 192.168.43.255, 172.20.10.15).</summary>
        void BuildTargets(ushort targetPort)
        {
            targets.Clear();
            targets.Add(new IPEndPoint(IPAddress.Broadcast, targetPort));
            foreach ((IPAddress address, IPAddress mask) in Interfaces())
            {
                byte[] a = address.GetAddressBytes(), m = mask.GetAddressBytes();
                var b = new byte[4];
                for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
                var endPoint = new IPEndPoint(new IPAddress(b), targetPort);
                if (!targets.Exists(t => t.Equals(endPoint))) targets.Add(endPoint);
            }
        }

        /// <summary>Adresses IPv4 de ce casque / PC (affichées par l'hôte : on peut aussi rejoindre par l'IP).</summary>
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            foreach ((IPAddress address, IPAddress _) in Interfaces()) list.Add(address.ToString());
            return list;
        }

        static IEnumerable<(IPAddress address, IPAddress mask)> Interfaces()
        {
            var found = new List<(IPAddress, IPAddress)>();
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(info.Address)) continue;
                        IPAddress mask = info.IPv4Mask;
                        if (mask == null || mask.Equals(IPAddress.Any)) mask = IPAddress.Parse("255.255.255.0");
                        found.Add((info.Address, mask));
                    }
                }
            }
            catch (Exception e)
            {
                // Certaines plateformes refusent l'énumération : le broadcast général reste disponible.
                Debug.LogWarning("[Abysses] Interfaces réseau illisibles : " + e.Message);
            }
            return found;
        }

        void AcquireMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (multicastLock != null) return;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi");
                multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "abysses-lan");
                multicastLock.Call("setReferenceCounted", false);
                multicastLock.Call("acquire");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Abysses] Verrou multicast impossible : " + e.Message);
                multicastLock = null;
            }
#endif
        }

        void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (multicastLock == null) return;
            try { multicastLock.Call("release"); }
            catch (Exception) { }
            multicastLock.Dispose();
            multicastLock = null;
#endif
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
