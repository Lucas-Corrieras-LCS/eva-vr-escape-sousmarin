using UnityEngine;

namespace Abysses.Level
{
    /// <summary>Bateau au mouillage : il monte, descend et roule doucement sur la houle.</summary>
    public class Bobbing : MonoBehaviour
    {
        [SerializeField] float height = 0.25f;
        [SerializeField] float roll = 4f;
        [SerializeField] float period = 4.5f;

        Vector3 rest;
        Quaternion restRotation;

        void Awake()
        {
            rest = transform.localPosition;
            restRotation = transform.localRotation;
        }

        void Update()
        {
            float w = Mathf.PI * 2f / period;
            float t = Time.time;
            transform.localPosition = rest + Vector3.up * (Mathf.Sin(t * w) * height);
            transform.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(t * w * 0.7f) * roll * 0.5f, 0f, Mathf.Sin(t * w + 1f) * roll);
        }
    }
}
