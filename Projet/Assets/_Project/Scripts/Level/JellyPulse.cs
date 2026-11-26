using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Méduses lumineuses derrière les hublots : l'ombrelle se contracte et se relâche, la méduse remonte un peu à
    /// chaque battement puis redescend lentement. Un seul composant pour toutes les méduses d'un étage.
    /// </summary>
    public class JellyPulse : MonoBehaviour
    {
        [SerializeField] Transform[] jellies;
        [SerializeField] float period = 2.6f;
        [SerializeField] float drift = 0.35f;

        Vector3[] rest;
        Vector3[] scale;

        void Awake()
        {
            rest = new Vector3[jellies.Length];
            scale = new Vector3[jellies.Length];
            for (int i = 0; i < jellies.Length; i++)
            {
                if (jellies[i] == null) continue;
                rest[i] = jellies[i].localPosition;
                scale[i] = jellies[i].localScale;
            }
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < jellies.Length; i++)
            {
                Transform j = jellies[i];
                if (j == null) continue;
                float k = (t + i * 0.77f) / period * Mathf.PI * 2f;
                float beat = Mathf.Max(0f, Mathf.Sin(k));
                j.localScale = new Vector3(scale[i].x * (1f - 0.18f * beat), scale[i].y * (1f + 0.15f * beat), scale[i].z * (1f - 0.18f * beat));
                j.localPosition = rest[i] + new Vector3(Mathf.Sin(t * 0.13f + i) * drift, Mathf.Sin(t * 0.21f + i * 1.7f) * drift, 0f);
            }
        }
    }
}
