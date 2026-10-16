using UnityEngine;
using UnityEngine.Rendering;

namespace Abysses.Level
{
    /// <summary>
    /// Ambiance d'un étage (consigne C6) : brouillard et lumière ambiante appliqués à l'arrivée de l'étage.
    /// Les étages sont des scènes additives : on applique les valeurs sans changer la scène active
    /// (sinon les objets réseau créés ensuite atterriraient dans l'étage et seraient détruits avec lui).
    /// </summary>
    public class FloorAmbience : MonoBehaviour
    {
        [SerializeField] Color fogColor = new Color(0.02f, 0.08f, 0.1f);
        [SerializeField, Range(0f, 0.3f)] float fogDensity = 0.06f;
        [SerializeField] Color ambientColor = new Color(0.05f, 0.1f, 0.12f);

        Color previousFog;
        float previousDensity;
        Color previousAmbient;

        void OnEnable()
        {
            previousFog = RenderSettings.fogColor;
            previousDensity = RenderSettings.fogDensity;
            previousAmbient = RenderSettings.ambientLight;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;

            Camera main = Camera.main;
            if (main != null) main.backgroundColor = fogColor;
        }

        void OnDisable()
        {
            RenderSettings.fogColor = previousFog;
            RenderSettings.fogDensity = previousDensity;
            RenderSettings.ambientLight = previousAmbient;
            Camera main = Camera.main;
            if (main != null) main.backgroundColor = previousFog;
        }
    }
}
