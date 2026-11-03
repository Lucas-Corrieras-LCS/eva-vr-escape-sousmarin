using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Abysses.Level
{
    /// <summary>
    /// Caustiques sous-marines (consigne C6) : cookie animé sur une lumière directionnelle sans ombres.
    /// Le cookie défile image par image (boucle) et dérive lentement : aucun post-process, aucune allocation.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class CausticsAnimator : MonoBehaviour
    {
        [Tooltip("Images de la boucle de caustiques (pavables).")]
        [SerializeField] Texture2D[] frames;
        [SerializeField, Min(1f)] float framesPerSecond = 10f;
        [Tooltip("Taille d'un motif au sol (m).")]
        [SerializeField, Min(0.5f)] float patternSize = 3.5f;
        [Tooltip("Dérive du motif (m/s), comme un courant.")]
        [SerializeField] Vector2 drift = new Vector2(0.12f, 0.05f);

        Light caustics;
        UniversalAdditionalLightData lightData;
        int shownFrame = -1;

        void Awake()
        {
            caustics = GetComponent<Light>();
            lightData = GetComponent<UniversalAdditionalLightData>();
            if (lightData != null) lightData.lightCookieSize = new Vector2(patternSize, patternSize);
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            int frame = (int)(Time.time * framesPerSecond) % frames.Length;
            if (frame != shownFrame)
            {
                shownFrame = frame;
                caustics.cookie = frames[frame];
            }
            if (lightData != null) lightData.lightCookieOffset = drift * Time.time;
        }
    }
}
