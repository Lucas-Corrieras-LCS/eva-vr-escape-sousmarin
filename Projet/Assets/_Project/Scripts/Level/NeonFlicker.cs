using UnityEngine;

namespace Abysses.Level
{
    /// <summary>Néon fatigué : s'éteint par rafales courtes, avec un grésillement. Un seul échange de matériau, aucune lumière dynamique.</summary>
    public class NeonFlicker : MonoBehaviour
    {
        [SerializeField] Renderer neon;
        [SerializeField] Material onMaterial;
        [SerializeField] Material offMaterial;
        [SerializeField] AudioSource buzz;
        [SerializeField] Vector2 calmRange = new Vector2(3f, 10f);

        float next;
        int burst;
        bool lit = true;

        void Start() => next = Time.time + Random.Range(calmRange.x, calmRange.y);

        void Update()
        {
            if (Time.time < next || neon == null) return;
            if (burst <= 0)
            {
                // Début d'une rafale de 2 à 5 clignotements.
                burst = Random.Range(2, 6) * 2;
                if (buzz != null) buzz.Play();
            }
            lit = !lit;
            neon.sharedMaterial = lit ? onMaterial : offMaterial;
            burst--;
            next = Time.time + (burst > 0 ? Random.Range(0.03f, 0.12f) : Random.Range(calmRange.x, calmRange.y));
            if (burst == 0 && !lit)
            {
                lit = true;
                neon.sharedMaterial = onMaterial;
            }
        }
    }
}
