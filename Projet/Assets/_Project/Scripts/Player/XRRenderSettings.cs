using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Abysses.Player
{
    /// <summary>
    /// Réglages de rendu du casque, appliqués au lancement (sans effet dans l'éditeur sans casque).
    /// Mesure Quest 2 avant ces réglages : 57 FPS de moyenne, GPU à 98 % (l'application est limitée par le GPU).
    /// </summary>
    public class XRRenderSettings : MonoBehaviour
    {
        [Tooltip("Rendu fovéal fixe (0 = aucun, 1 = maximal) : la périphérie, floue dans les lentilles du Quest 2, est rendue en basse résolution.")]
        [SerializeField, Range(0f, 1f)] float foveationLevel = 1f;
        [Tooltip("Résolution des images par œil (1 = recommandée par le casque). 0,9 = 19 % de pixels en moins.")]
        [SerializeField, Range(0.7f, 1.2f)] float eyeResolutionScale = 0.9f;

        void Start()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
            {
                display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
                display.foveatedRenderingLevel = foveationLevel;
            }
            if (displays.Count > 0) XRSettings.eyeTextureResolutionScale = eyeResolutionScale;
        }
    }
}
