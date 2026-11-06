using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Build Android (Quest) : ajoute au manifeste les permissions réseau dont le jeu a besoin.
    /// - INTERNET, ACCESS_NETWORK_STATE : connexion au relais et à l'hôte local ;
    /// - ACCESS_WIFI_STATE, CHANGE_WIFI_MULTICAST_STATE : verrou multicast, sans lequel le Wi-Fi du Quest jette les
    ///   broadcasts de découverte (« aucune partie trouvée » au hasard, surtout sur un partage de connexion).
    /// </summary>
    public class AbyssesAndroidManifest : IPostGenerateGradleAndroidProject
    {
        static readonly string[] Permissions =
        {
            "android.permission.INTERNET",
            "android.permission.ACCESS_NETWORK_STATE",
            "android.permission.ACCESS_WIFI_STATE",
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
        };

        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[Abysses] Manifeste Android introuvable : " + manifestPath);
                return;
            }
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;
            int added = 0;
            foreach (string permission in Permissions)
            {
                bool present = false;
                foreach (XmlNode node in manifest.SelectNodes("uses-permission"))
                    present |= node.Attributes?["android:name"]?.Value == permission;
                if (present) continue;
                XmlElement element = doc.CreateElement("uses-permission");
                element.SetAttribute("name", AndroidNs, permission);
                manifest.PrependChild(element);
                added++;
            }
            doc.Save(manifestPath);
            Debug.Log($"[Abysses] Manifeste Android : {added} permission(s) réseau ajoutée(s).");
        }
    }
}
