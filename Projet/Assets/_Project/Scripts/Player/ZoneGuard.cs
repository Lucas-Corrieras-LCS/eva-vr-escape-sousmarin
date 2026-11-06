using UnityEngine;
using Abysses.Core;

namespace Abysses.Player
{
    /// <summary>
    /// Limite de zone (consignes C4 / C10) : si la tête s'approche à moins de 50 cm du bord du quadrant
    /// (dérive du tracking, joueur qui recule…), « alarme de pression » : voile rouge devant les yeux + alarme sonore,
    /// d'autant plus fort qu'on s'éloigne. Le réflexe naturel est de reculer.
    /// Vérification à 10 Hz, le voile n'est rendu que lorsqu'il est visible (aucun coût sinon).
    /// Filet de sécurité : un joueur qui tombe sous le sol (ou, au joystick en debug, qui traverse un mur et s'éloigne
    /// de son module) est ramené au centre de son module après un bref fondu au noir.
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

        [Header("Retour au module")]
        [Tooltip("Hauteur (m) sous le sol à partir de laquelle on considère que le joueur tombe.")]
        [SerializeField, Min(0.3f)] float fallDepth = 1.2f;
        [Tooltip("Au joystick (debug) : distance (m) hors de la zone au-delà de laquelle on ramène le joueur.")]
        [SerializeField, Min(0.5f)] float lostDistance = 1.5f;
        [SerializeField, Min(0f)] float lostDelay = 1f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        Color overlayColor = new Color(0.8f, 0.05f, 0.02f, 0f);
        float nextCheck;
        float shownIntensity = -1f;
        float lostSince = -1f;
        bool recovering;

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
            float intensity = Mathf.Clamp01(outside / fullWarningDistance);
            if (recovering) return;
            if (ShouldRecover(rig, outside))
            {
                StartCoroutine(Recover(rig));
                return;
            }
            Apply(intensity);
            // Les manettes vibrent aussi : on sent la limite même sans regarder.
            if (intensity > 0f) LocalRig.Pulse(0.2f + 0.6f * intensity, checkInterval);
        }

        bool ShouldRecover(LocalRig rig, float outside)
        {
            // Chute : seul l'origine XR peut descendre sous le sol (gravité du déplacement au joystick).
            if (rig.transform.position.y < -fallDepth) return true;
            // En build EVA, le joueur marche vraiment : le déplacer casserait la correspondance avec la piste.
            if (!GameConfig.Instance.AllowJoystickLocomotion || outside < lostDistance)
            {
                lostSince = -1f;
                return false;
            }
            if (lostSince < 0f) lostSince = Time.unscaledTime;
            return Time.unscaledTime - lostSince >= lostDelay;
        }

        System.Collections.IEnumerator Recover(LocalRig rig)
        {
            recovering = true;
            lostSince = -1f;
            Color black = Color.black;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                SetOverlay(black, t);
                yield return null;
            }
            SetOverlay(black, 1f);
            rig.Recenter();
            LocalRig.Pulse(0.4f, 0.15f);
            yield return new WaitForSecondsRealtime(0.2f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f)
            {
                SetOverlay(black, t);
                yield return null;
            }
            recovering = false;
            shownIntensity = -1f;
            Apply(0f);
        }

        void SetOverlay(Color color, float alpha)
        {
            if (warningOverlay == null) return;
            warningOverlay.enabled = alpha > 0f;
            color.a = Mathf.Clamp01(alpha);
            block.SetColor(BaseColorId, color);
            warningOverlay.SetPropertyBlock(block);
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
