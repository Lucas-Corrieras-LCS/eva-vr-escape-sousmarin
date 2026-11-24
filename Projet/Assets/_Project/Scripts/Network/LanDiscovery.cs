using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Abysses.Network
{
    /// <summary>
    /// Découverte automatique de l'hôte en réseau local, pour jouer sans Internet (Wi-Fi d'EVA, partage de connexion).
    /// Deux canaux, pour passer sur tous les réseaux :
    /// - l'hôte annonce sa partie chaque seconde en broadcast (255.255.255.255 ET l'adresse de broadcast de chaque
    ///   réseau, que certains points d'accès et partages de connexion exigent) ;
    /// - le casque qui cherche envoie lui-même une demande en broadcast ET directement à chaque adresse de son réseau
    ///   (unicast : les partages de connexion des téléphones ne relaient souvent pas le broadcast entre appareils),
    ///   et l'hôte lui répond directement.
    /// Sur Quest (Android), le Wi-Fi jette les broadcasts reçus tant que l'application ne tient pas un « verrou
    /// multicast » : il est pris pendant la recherche et l'annonce (permission ajoutée au manifeste par le build).
    /// Non bloquant : aucune thread, lecture des sockets dans Update uniquement quand des données sont disponibles.
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        const string Signature = "ABYSSES1";
        const string Probe = "ABYSSES1?";
        const float Interval = 1f;
        /// <summary>Taille maximale du réseau sondé adresse par adresse (/22, 1 022 adresses).</summary>
        const int MaxScannedHosts = 1022;

        /// <summary>Socket principal : annonces (hôte) ou écoute des annonces (client), sur le port de découverte.</summary>
        UdpClient socket;
        /// <summary>Hôte : écoute des demandes des clients (port de découverte + 1).</summary>
        UdpClient probeSocket;
        readonly List<IPEndPoint> targets = new List<IPEndPoint>();
        readonly List<IPEndPoint> scanTargets = new List<IPEndPoint>();
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
                BuildScanTargets((ushort)(discoveryPort + 1));
                nextSend = 0f;
                Debug.Log($"[Abysses] Recherche LAN depuis {LocalAddress()} : broadcast + {scanTargets.Count} adresses sondées une à une.");
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
                    if (send) SendAll(socket, announcement, targets);
                    // Demandes des clients : réponse directe à l'expéditeur, sur son port d'écoute.
                    while (probeSocket != null && probeSocket.Available > 0)
                    {
                        IPEndPoint sender = null;
                        byte[] data = probeSocket.Receive(ref sender);
                        if (Encoding.ASCII.GetString(data) == Probe)
                            Send(socket, announcement, new IPEndPoint(sender.Address, port));
                    }
                    return;
                }

                if (send)
                {
                    SendAll(socket, probe, targets);
                    SendAll(socket, probe, scanTargets);
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

        static void SendAll(UdpClient client, byte[] message, List<IPEndPoint> to)
        {
            // Une adresse refusée (interface tombée, hôte absent) ne doit pas empêcher les autres.
            for (int i = 0; i < to.Count; i++) Send(client, message, to[i]);
        }

        /// <summary>Envoi d'un datagramme ; un refus du système est ignoré (renvoie false).</summary>
        static bool Send(UdpClient client, byte[] message, IPEndPoint to)
        {
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !ENABLE_IL2CPP)
            // Mono sur macOS (éditeur, builds Mac Mono) n'a pas de traduction pour certaines erreurs de sendto, par exemple
            // 45 « Operation not supported » vers un broadcast quand la route par défaut passe par un VPN ou un partage
            // de connexion : il AVORTE alors tout le processus au lieu de lever une exception. On envoie par la libc.
            var address = new byte[16];
            address[0] = 16;                       // sin_len
            address[1] = 2;                        // sin_family = AF_INET
            address[2] = (byte)(to.Port >> 8);     // sin_port, ordre réseau
            address[3] = (byte)to.Port;
            Array.Copy(to.Address.GetAddressBytes(), 0, address, 4, 4);
            return sendto(client.Client.Handle.ToInt32(), message, (UIntPtr)message.Length, 0, address, 16).ToInt64() >= 0;
#else
            try
            {
                client.Send(message, message.Length, to);
                return true;
            }
            catch (SocketException) { return false; }
#endif
        }

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !ENABLE_IL2CPP)
        [DllImport("libc")]
        static extern IntPtr sendto(int socket, byte[] buffer, UIntPtr length, int flags, byte[] address, uint addressLength);
#endif

        /// <summary>Chaque adresse du réseau local (limité à un /22 autour du casque sur les grands réseaux).</summary>
        void BuildScanTargets(ushort targetPort)
        {
            scanTargets.Clear();
            foreach ((IPAddress address, IPAddress mask) in Interfaces())
            {
                uint ip = ToUInt(address.GetAddressBytes());
                uint m = ToUInt(mask.GetAddressBytes());
                if (~m - 1 > MaxScannedHosts) m = 0xFFFFFC00;
                uint network = ip & m, count = ~m - 1;
                for (uint i = 1; i <= count; i++)
                {
                    uint candidate = network + i;
                    if (candidate != ip) scanTargets.Add(new IPEndPoint(FromUInt(candidate), targetPort));
                }
            }
        }

        static uint ToUInt(byte[] b) => (uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3];
        static IPAddress FromUInt(uint v) => new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

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

        /// <summary>Première adresse IPv4 locale (vide si aucune).</summary>
        public static string LocalAddress()
        {
            List<string> all = LocalAddresses();
            return all.Count > 0 ? all[0] : string.Empty;
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
