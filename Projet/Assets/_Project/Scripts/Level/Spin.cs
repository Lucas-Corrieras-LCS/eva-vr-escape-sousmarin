using UnityEngine;

namespace Abysses.Level
{
    /// <summary>Rotation continue autour de l'axe Y local (gyrophares de secours, éléments de décor).</summary>
    public class Spin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 120f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
