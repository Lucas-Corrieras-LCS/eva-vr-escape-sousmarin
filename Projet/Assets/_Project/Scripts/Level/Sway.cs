using UnityEngine;

namespace Abysses.Level
{
    /// <summary>Algues qui ondulent dans le courant (un seul composant pour toutes les tiges d'un jardin).</summary>
    public class Sway : MonoBehaviour
    {
        [SerializeField] Transform[] stems;
        [SerializeField] float angle = 9f;
        [SerializeField] float speed = 0.7f;

        Quaternion[] rest;

        void Awake()
        {
            rest = new Quaternion[stems.Length];
            for (int i = 0; i < stems.Length; i++)
                if (stems[i] != null) rest[i] = stems[i].localRotation;
        }

        void Update()
        {
            float t = Time.time * speed;
            for (int i = 0; i < stems.Length; i++)
            {
                if (stems[i] == null) continue;
                float a = Mathf.Sin(t + i * 0.83f) * angle;
                float b = Mathf.Sin(t * 0.7f + i * 1.3f) * angle * 0.5f;
                stems[i].localRotation = rest[i] * Quaternion.Euler(a, 0f, b);
            }
        }
    }
}
