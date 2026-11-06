using UnityEngine;

namespace Abysses.Level
{
    /// <summary>Mouette (ou tout objet) qui tourne en rond au-dessus de la plateforme, ailes battantes.</summary>
    public class Orbit : MonoBehaviour
    {
        [SerializeField] Vector3 center;
        [SerializeField, Min(1f)] float radius = 25f;
        [Tooltip("Degrés par seconde (négatif = sens inverse).")]
        [SerializeField] float speed = 12f;
        [SerializeField] float phase;
        [SerializeField] float bob = 1.5f;
        [Tooltip("Ailes (battement par rotation autour de Z local).")]
        [SerializeField] Transform leftWing;
        [SerializeField] Transform rightWing;

        void Update()
        {
            float t = Time.time;
            float angle = (phase + t * speed) * Mathf.Deg2Rad;
            Vector3 local = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(t * 0.7f + phase) * bob, Mathf.Sin(angle) * radius);
            transform.localPosition = local;
            // Tangente au cercle : la mouette regarde où elle va.
            Vector3 heading = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Mathf.Sign(speed);
            transform.localRotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sign(speed) * -12f);
            float flap = Mathf.Sin(t * 7f + phase) * 25f;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
        }
    }
}
