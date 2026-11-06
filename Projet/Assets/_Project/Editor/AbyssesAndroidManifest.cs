using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Build Android (Quest) : ajoute au manifeste les permissions réseau dont le jeu a besoin
    /// (voir <see cref="Abysses.Network.LanDiscovery"/>).
    /// - INTERNET, ACCESS_NETWORK_STATE : connexion au relais et à l'hôte local ;
    /// - ACCESS_WIFI_STATE, CHANGE_WIFI_MULTICAST_STATE : verrou multicast, sans lequel le Wi-Fi du Quest jette les
    ///   broadcasts de découverte (« aucune partie trouvée » au hasard, surtout sur un partage de connexion).
    /// </summary>
    class AbyssesAndroidManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        static readonly string[] Permissions =
        {
            "android.permission.INTERNET",
            "android.permission.ACCESS_NETWORK_STATE",
            "android.permission.ACCESS_WIFI_STATE",
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
        };

        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[Abysses] Manifeste Android introuvable : " + manifestPath);
                return;
            }

            var document = new XmlDocument();
            document.Load(manifestPath);
            XmlElement manifest = document.DocumentElement;
            int added = 0;
            foreach (string permission in Permissions)
            {
                if (manifest.SelectSingleNode($"uses-permission[@*[local-name()='name']='{permission}']") != null) continue;
                XmlElement element = document.CreateElement("uses-permission");
                element.SetAttribute("name", AndroidNamespace, permission);
                manifest.PrependChild(element);
                added++;
            }
            document.Save(manifestPath);
            Debug.Log($"[Abysses] Manifeste Android : {added} permission(s) réseau ajoutée(s).");
        }
    }
}
