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
    /// Découverte automatique de l'hôte en réseau local (broadcast UDP), pour jouer sans Internet.
    /// Non bloquant : aucune thread, lecture du socket dans Update uniquement quand des données sont disponibles.
    /// Quest (Android) : le Wi-Fi jette les paquets broadcast tant qu'aucun « verrou multicast » n'est pris
    /// (permission CHANGE_WIFI_MULTICAST_STATE, ajoutée au manifeste par AbyssesAndroidManifest).
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        const string Signature = "ABYSSES1";
        const float BroadcastInterval = 1f;

        UdpClient socket;
        readonly List<IPEndPoint> broadcastEndPoints = new List<IPEndPoint>();
        byte[] announcement;
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
                socket = new UdpClient { EnableBroadcast = true };
                announcement = Encoding.ASCII.GetBytes(Signature + ":" + gamePort);
                // 255.255.255.255 n'est pas toujours relayé par les routeurs Wi-Fi : on vise aussi le broadcast du sous-réseau.
                broadcastEndPoints.Clear();
                broadcastEndPoints.Add(new IPEndPoint(IPAddress.Broadcast, discoveryPort));
                foreach (IPAddress subnet in SubnetBroadcasts())
                    broadcastEndPoints.Add(new IPEndPoint(subnet, discoveryPort));
                AcquireMulticastLock();
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
                if (broadcasting)
                {
                    if (Time.unscaledTime < nextBroadcast) return;
                    for (int i = 0; i < broadcastEndPoints.Count; i++)
                    {
                        // Un sous-réseau injoignable ne doit pas couper les autres annonces.
                        try { socket.Send(announcement, announcement.Length, broadcastEndPoints[i]); }
                        catch (SocketException) { }
                    }
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

        /// <summary>Adresse IPv4 locale du casque sur le Wi-Fi (affichée par l'hôte, pour diagnostiquer), ou null.</summary>
        public static string LocalAddress()
        {
            foreach (UnicastIPAddressInformation address in LanAddresses())
                return address.Address.ToString();
            return null;
        }

        static IEnumerable<IPAddress> SubnetBroadcasts()
        {
            foreach (UnicastIPAddressInformation address in LanAddresses())
            {
                byte[] ip = address.Address.GetAddressBytes();
                byte[] mask;
                try { mask = address.IPv4Mask != null ? address.IPv4Mask.GetAddressBytes() : null; }
                catch (NotImplementedException) { mask = null; }
                // Masque inconnu (certaines versions Android) : /24, le cas des box et des partages de connexion.
                if (mask == null || mask.Length != 4 || mask[0] == 0) mask = new byte[] { 255, 255, 255, 0 };
                var broadcast = new byte[4];
                for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mask[i]);
                yield return new IPAddress(broadcast);
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
