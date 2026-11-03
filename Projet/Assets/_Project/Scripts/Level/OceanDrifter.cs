using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Silhouette qui passe derrière les hublots (banc, raie, créature géante…) : traverse lentement d'un point à l'autre,
    /// en ondulant, puis disparaît un moment avant de repasser dans l'autre sens. Une simple forme sombre dans le
    /// brouillard suffit à faire sentir l'océan autour de la station.
    /// </summary>
    public class OceanDrifter : MonoBehaviour
    {
        [SerializeField] Vector3 from;
        [SerializeField] Vector3 to;
        [SerializeField, Min(1f)] float crossingTime = 40f;
        [SerializeField] Vector2 pauseRange = new Vector2(20f, 50f);
        [SerializeField] float bobAmplitude = 0.4f;
        [SerializeField, Min(0.01f)] float bobFrequency = 0.08f;
        [Tooltip("Visuel retourné selon le sens de la traversée (échelle X).")]
        [SerializeField] Transform visual;

        float progress;
        float resumeAt;
        bool forward = true;
        Vector3 baseScale;
        Renderer[] renderers;

        void Awake()
        {
            if (visual == null) visual = transform;
            baseScale = visual.localScale;
            renderers = GetComponentsInChildren<Renderer>(true);
            // Première apparition décalée : tous les étages ne montrent pas la créature au même moment.
            resumeAt = Time.time + Random.Range(pauseRange.x * 0.25f, pauseRange.x);
            SetVisible(false);
        }

        void Update()
        {
            if (Time.time < resumeAt) return;
            if (progress == 0f) SetVisible(true);

            progress += Time.deltaTime / crossingTime;
            Vector3 a = forward ? from : to;
            Vector3 b = forward ? to : from;
            Vector3 position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, progress));
            position.y += Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            transform.position = position;

            if (progress < 1f) return;
            progress = 0f;
            forward = !forward;
            visual.localScale = new Vector3(forward ? baseScale.x : -baseScale.x, baseScale.y, baseScale.z);
            resumeAt = Time.time + Random.Range(pauseRange.x, pauseRange.y);
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }
    }
}
