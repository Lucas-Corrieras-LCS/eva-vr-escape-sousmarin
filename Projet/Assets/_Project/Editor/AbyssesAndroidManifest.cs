using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Ajoute au manifeste Android les permissions Wi-Fi de la découverte LAN (voir <see cref="Abysses.Network.LanDiscovery"/>) :
    /// sans verrou multicast, le Quest jette les annonces broadcast des autres casques.
    /// </summary>
    class AbyssesAndroidManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        static readonly string[] Permissions =
        {
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
            "android.permission.ACCESS_WIFI_STATE",
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
            foreach (string permission in Permissions)
            {
                if (manifest.SelectSingleNode($"uses-permission[@*[local-name()='name']='{permission}']") != null) continue;
                XmlElement element = document.CreateElement("uses-permission");
                element.SetAttribute("name", AndroidNamespace, permission);
                manifest.PrependChild(element);
            }
            document.Save(manifestPath);
        }
    }
}
