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
    /// Découverte automatique de l'hôte en réseau local, pour jouer sans Internet.
    /// - L'hôte s'annonce en broadcast UDP et répond aux sondes des clients.
    /// - Le client sonde le broadcast ET chaque adresse de son sous-réseau en unicast : les partages de connexion
    ///   des téléphones et beaucoup de box ne relaient pas le broadcast entre appareils.
    /// Non bloquant : aucune thread, lecture du socket dans Update uniquement quand des données sont disponibles.
    /// Quest (Android) : le Wi-Fi jette les paquets broadcast tant qu'aucun « verrou multicast » n'est pris
    /// (permission CHANGE_WIFI_MULTICAST_STATE, ajoutée au manifeste par AbyssesAndroidManifest).
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        const string Signature = "ABYSSES1";
        const string Probe = Signature + "?";
        const float BroadcastInterval = 1f;
        /// <summary>Taille maximale d'un sous-réseau sondé en unicast (/22).</summary>
        const int MaxScannedHosts = 1022;

        UdpClient socket;
        readonly List<IPEndPoint> broadcastEndPoints = new List<IPEndPoint>();
        readonly List<IPEndPoint> scanEndPoints = new List<IPEndPoint>();
        byte[] announcement;
        byte[] probe;
        float nextBroadcast;
        bool broadcasting;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject multicastLock;
#endif

        public bool IsActive => socket != null;

        /// <summary>Hôte trouvé : adresse IP et port de jeu.</summary>
        public event Action<string, ushort> HostFound;

        public void StartBroadcasting(ushort discoveryPort, ushort gamePort)
        {
            Stop();
            try
            {
                AcquireMulticastLock();
                socket = Bind(discoveryPort);
                announcement = Encoding.ASCII.GetBytes(Signature + ":" + gamePort);
                FillBroadcastEndPoints(discoveryPort);
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
                AcquireMulticastLock();
                socket = Bind(discoveryPort);
                probe = Encoding.ASCII.GetBytes(Probe);
                FillBroadcastEndPoints(discoveryPort);
                scanEndPoints.Clear();
                foreach (UnicastIPAddressInformation address in LanAddresses())
                    foreach (IPAddress host in SubnetHosts(address))
                        scanEndPoints.Add(new IPEndPoint(host, discoveryPort));
                Debug.Log($"[Abysses] Recherche LAN depuis {LocalAddress() ?? "?"} : {scanEndPoints.Count} adresses sondées.");
                nextBroadcast = 0f;
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Abysses] Écoute LAN impossible : " + e.Message);
                Stop();
            }
        }

        static UdpClient Bind(ushort port)
        {
            var client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            client.EnableBroadcast = true;
            return client;
        }

        void FillBroadcastEndPoints(ushort port)
        {
            // 255.255.255.255 n'est pas toujours relayé par les routeurs Wi-Fi : on vise aussi le broadcast du sous-réseau.
            broadcastEndPoints.Clear();
            broadcastEndPoints.Add(new IPEndPoint(IPAddress.Broadcast, port));
            foreach (UnicastIPAddressInformation address in LanAddresses())
                broadcastEndPoints.Add(new IPEndPoint(SubnetBroadcast(address), port));
        }

        public void Stop()
        {
            broadcasting = false;
            ReleaseMulticastLock();
            if (socket == null) return;
            socket.Close();
            socket = null;
        }

        void Update()
        {
            if (socket == null) return;
            try
            {
                if (Time.unscaledTime >= nextBroadcast)
                {
                    nextBroadcast = Time.unscaledTime + BroadcastInterval;
                    if (broadcasting) SendAll(announcement, broadcastEndPoints);
                    else
                    {
                        SendAll(probe, broadcastEndPoints);
                        SendAll(probe, scanEndPoints);
                    }
                }

                while (socket != null && socket.Available > 0)
                {
                    IPEndPoint sender = null;
                    byte[] data = socket.Receive(ref sender);
                    string message = Encoding.ASCII.GetString(data);
                    if (broadcasting)
                    {
                        // Sonde d'un client : réponse directe, sans dépendre du broadcast.
                        if (message == Probe) socket.Send(announcement, announcement.Length, sender);
                    }
                    else if (TryParse(message, out ushort gamePort))
                        HostFound?.Invoke(sender.Address.ToString(), gamePort);
                }
            }
            catch (SocketException e)
            {
                Debug.LogWarning("[Abysses] Erreur découverte LAN : " + e.Message);
                Stop();
            }
        }

        void SendAll(byte[] payload, List<IPEndPoint> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                // Une adresse injoignable ne doit pas couper les autres envois.
                try { socket.Send(payload, payload.Length, targets[i]); }
                catch (SocketException) { }
            }
        }

        /// <summary>Adresse IPv4 locale du casque sur le Wi-Fi (affichée par l'hôte, pour diagnostiquer), ou null.</summary>
        public static string LocalAddress()
        {
            foreach (UnicastIPAddressInformation address in LanAddresses())
                return address.Address.ToString();
            return null;
        }

        static byte[] Mask(UnicastIPAddressInformation address)
        {
            byte[] mask;
            try { mask = address.IPv4Mask != null ? address.IPv4Mask.GetAddressBytes() : null; }
            catch (NotImplementedException) { mask = null; }
            // Masque inconnu (certaines versions Android) : /24, le cas des box et des partages de connexion.
            if (mask == null || mask.Length != 4 || mask[0] == 0) mask = new byte[] { 255, 255, 255, 0 };
            return mask;
        }

        static uint ToUInt(byte[] b) => (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
        static IPAddress FromUInt(uint v) => new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

        static IPAddress SubnetBroadcast(UnicastIPAddressInformation address)
        {
            uint ip = ToUInt(address.Address.GetAddressBytes());
            uint mask = ToUInt(Mask(address));
            return FromUInt(ip | ~mask);
        }

        /// <summary>Toutes les adresses du sous-réseau (sauf la sienne), limitées à un /22 autour du casque.</summary>
        static IEnumerable<IPAddress> SubnetHosts(UnicastIPAddressInformation address)
        {
            uint ip = ToUInt(address.Address.GetAddressBytes());
            uint mask = ToUInt(Mask(address));
            uint hostCount = ~mask - 1;
            if (hostCount > MaxScannedHosts)
            {
                // Grand réseau (école, entreprise) : on ne sonde que le /22 qui contient le casque.
                mask = 0xFFFFFC00;
                hostCount = ~mask - 1;
            }
            uint network = ip & mask;
            for (uint i = 1; i <= hostCount; i++)
            {
                uint candidate = network + i;
                if (candidate != ip) yield return FromUInt(candidate);
            }
        }

        static IEnumerable<UnicastIPAddressInformation> LanAddresses()
        {
            NetworkInterface[] interfaces;
            try { interfaces = NetworkInterface.GetAllNetworkInterfaces(); }
            catch (Exception) { yield break; }
            foreach (NetworkInterface item in interfaces)
            {
                if (item.OperationalStatus != OperationalStatus.Up || item.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                UnicastIPAddressInformationCollection addresses;
                try { addresses = item.GetIPProperties().UnicastAddresses; }
                catch (Exception) { continue; }
                foreach (UnicastIPAddressInformation address in addresses)
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                        yield return address;
            }
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
                Debug.LogWarning("[Abysses] Verrou multicast Wi-Fi impossible : " + e.Message);
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

        static bool TryParse(string message, out ushort gamePort)
        {
            gamePort = 0;
            string prefix = Signature + ":";
            return message.StartsWith(prefix, StringComparison.Ordinal)
                && ushort.TryParse(message.Substring(prefix.Length), out gamePort);
        }

        void OnDestroy() => Stop();
    }
}
