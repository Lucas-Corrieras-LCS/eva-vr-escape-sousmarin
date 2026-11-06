using UnityEngine;

namespace Abysses.Level
{
    /// <summary>Voyant d'état d'une énigme : orange tant qu'elle n'est pas résolue, vert ensuite.</summary>
    public class ObjectiveLamp : MonoBehaviour
    {
        [SerializeField] PuzzleObjective objective;
        [SerializeField] Renderer lamp;
        [SerializeField] Material idle;
        [SerializeField] Material solved;

        void Awake()
        {
            if (objective != null) objective.StateChanged += Apply;
            Apply(false);
        }

        void OnDestroy()
        {
            if (objective != null) objective.StateChanged -= Apply;
        }

        void Apply(bool isSolved)
        {
            if (lamp != null) lamp.sharedMaterial = isSolved ? solved : idle;
        }
    }
}
