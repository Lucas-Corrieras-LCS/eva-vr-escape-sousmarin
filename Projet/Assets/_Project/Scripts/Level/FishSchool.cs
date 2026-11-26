using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Banc de poissons qui longe la station derrière les hublots : chaque poisson garde sa place dans le banc
    /// (décalage fixe), le banc glisse le long de la piste puis réapparaît à l'autre bout. Ondulation de nage et
    /// petit mouvement vertical, tout calculé ici (un seul composant par banc, aucune physique).
    /// </summary>
    public class FishSchool : MonoBehaviour
    {
        [SerializeField] Transform[] fish;
        [Tooltip("Décalage de chaque poisson dans le banc (local).")]
        [SerializeField] Vector3[] offsets;
        [Tooltip("Abscisses de début et de fin du trajet (monde).")]
        [SerializeField] Vector2 range = new Vector2(-12f, 43f);
        [SerializeField] float speed = 1.2f;
        [Tooltip("+1 : nage vers +X ; -1 : vers -X.")]
        [SerializeField] float direction = 1f;
        [SerializeField] float phase;

        void Update()
        {
            float length = Mathf.Abs(range.y - range.x);
            if (length < 0.1f) return;
            float t = Time.time;
            float travel = Mathf.Repeat(phase * length + t * speed, length);
            float x = direction > 0f ? range.x + travel : range.y - travel;
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            Quaternion heading = Quaternion.LookRotation(new Vector3(direction, 0f, 0f), Vector3.up);
            for (int i = 0; i < fish.Length; i++)
            {
                Transform f = fish[i];
                if (f == null) continue;
                float k = t * 2.2f + i * 0.9f;
                Vector3 offset = i < offsets.Length ? offsets[i] : Vector3.zero;
                f.localPosition = offset + new Vector3(Mathf.Sin(k * 0.3f) * 0.15f, Mathf.Sin(k * 0.5f) * 0.12f, Mathf.Cos(k * 0.4f) * 0.15f);
                // Nage : le poisson balance la tête de gauche à droite.
                f.rotation = heading * Quaternion.Euler(0f, Mathf.Sin(k * 4f) * 12f, 0f);
            }
        }
    }
}
