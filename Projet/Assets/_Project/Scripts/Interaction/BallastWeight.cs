using UnityEngine;

namespace Abysses.Interaction
{
    /// <summary>Lest de plongée attrapable (masse affichée gravée dessus), pesé par <see cref="BallastScale"/>.</summary>
    public class BallastWeight : MonoBehaviour
    {
        [SerializeField, Range(1, 10)] int kilograms = 1;

        public int Kilograms => kilograms;
    }
}
