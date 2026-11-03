using System.Collections;
using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Fin de partie (étage 3) : la station remonte, la lumière de la surface revient.
    /// Fondu du brouillard vers un bleu clair, faisceau de lumière venu d'en haut, bulles.
    /// </summary>
    public class FloorFinale : MonoBehaviour
    {
        [SerializeField] Light sunbeam;
        [SerializeField, Min(0f)] float sunbeamIntensity = 4f;
        [SerializeField] Color surfaceFog = new Color(0.35f, 0.62f, 0.72f);
        [SerializeField, Range(0f, 0.2f)] float surfaceFogDensity = 0.015f;
        [SerializeField] Color surfaceAmbient = new Color(0.35f, 0.5f, 0.55f);
        [SerializeField, Min(0.5f)] float duration = 6f;
        [SerializeField] ParticleSystem[] bubbles;
        [SerializeField] AudioSource finaleSound;
        [Tooltip("Rayon de lumière visible (cône additif), révélé en fondu avec le faisceau.")]
        [SerializeField] Renderer sunShaft;
        [SerializeField] Color sunShaftColor = new Color(0.75f, 0.92f, 1f, 0.35f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        bool played;
        MaterialPropertyBlock shaftBlock;

        void OnEnable() => GameStateManager.PhaseChanged += OnPhaseChanged;
        void OnDisable() => GameStateManager.PhaseChanged -= OnPhaseChanged;

        void Start()
        {
            if (sunbeam != null) sunbeam.intensity = 0f;
            if (sunShaft != null) sunShaft.enabled = false;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Finished || played) return;
            played = true;
            StartCoroutine(Ascend());
        }

        IEnumerator Ascend()
        {
            for (int i = 0; i < bubbles.Length; i++)
                if (bubbles[i] != null) bubbles[i].Play();
            if (finaleSound != null) finaleSound.Play();

            Color fogFrom = RenderSettings.fogColor;
            float densityFrom = RenderSettings.fogDensity;
            Color ambientFrom = RenderSettings.ambientLight;
            Camera main = Camera.main;
            if (sunShaft != null)
            {
                shaftBlock = new MaterialPropertyBlock();
                sunShaft.enabled = true;
            }

            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                RenderSettings.fogColor = Color.Lerp(fogFrom, surfaceFog, k);
                RenderSettings.fogDensity = Mathf.Lerp(densityFrom, surfaceFogDensity, k);
                RenderSettings.ambientLight = Color.Lerp(ambientFrom, surfaceAmbient, k);
                if (main != null) main.backgroundColor = RenderSettings.fogColor;
                if (sunbeam != null) sunbeam.intensity = k * sunbeamIntensity;
                if (sunShaft != null)
                {
                    shaftBlock.SetColor(BaseColorId, sunShaftColor * k);
                    sunShaft.SetPropertyBlock(shaftBlock);
                }
                yield return null;
            }
        }
    }
}
