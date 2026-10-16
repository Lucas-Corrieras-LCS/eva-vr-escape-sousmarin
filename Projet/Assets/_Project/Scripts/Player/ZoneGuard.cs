using UnityEngine;
using Abysses.Core;

namespace Abysses.Player
{
    /// <summary>
    /// Limite de zone (consignes C4 / C10) : si la tête s'approche à moins de 50 cm du bord du quadrant
    /// (dérive du tracking, joueur qui recule…), « alarme de pression » : voile rouge devant les yeux + alarme sonore,
    /// d'autant plus fort qu'on s'éloigne. Le réflexe naturel est de reculer.
    /// Vérification à 10 Hz, le voile n'est rendu que lorsqu'il est visible (aucun coût sinon).
    /// </summary>
    public class ZoneGuard : MonoBehaviour
    {
        [Tooltip("Quad devant la caméra, matériau URP Unlit transparent.")]
        [SerializeField] Renderer warningOverlay;
        [SerializeField] AudioSource alarm;
        [SerializeField, Range(0f, 1f)] float maxOverlayAlpha = 0.65f;
        [Tooltip("Distance (m) au-delà de la zone sûre pour atteindre l'intensité maximale.")]
        [SerializeField, Min(0.05f)] float fullWarningDistance = 0.4f;
        [SerializeField, Min(0.02f)] float checkInterval = 0.1f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        Color overlayColor = new Color(0.8f, 0.05f, 0.02f, 0f);
        float nextCheck;
        float shownIntensity = -1f;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + checkInterval;

            LocalRig rig = LocalRig.Instance;
            if (rig == null || !PlayArea.IsValid(rig.Quadrant))
            {
                Apply(0f);
                return;
            }

            Rect safe = PlayArea.GetRect(rig.Quadrant, PlayArea.SafetyMargin);
            Vector3 head = rig.Head.position;
            float dx = Mathf.Max(safe.xMin - head.x, 0f, head.x - safe.xMax);
            float dz = Mathf.Max(safe.yMin - head.z, 0f, head.z - safe.yMax);
            float outside = Mathf.Sqrt(dx * dx + dz * dz);
            Apply(Mathf.Clamp01(outside / fullWarningDistance));
        }

        void Apply(float intensity)
        {
            if (Mathf.Approximately(intensity, shownIntensity)) return;
            shownIntensity = intensity;
            bool active = intensity > 0f;

            if (warningOverlay != null)
            {
                warningOverlay.enabled = active;
                if (active)
                {
                    overlayColor.a = intensity * maxOverlayAlpha;
                    block.SetColor(BaseColorId, overlayColor);
                    warningOverlay.SetPropertyBlock(block);
                }
            }

            if (alarm != null)
            {
                alarm.volume = intensity;
                if (active && !alarm.isPlaying) alarm.Play();
                else if (!active && alarm.isPlaying) alarm.Stop();
            }
        }
    }
}
