using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Fuite de vapeur sous pression (étage 2) : jet qui siffle par intermittence.
    /// Coupée une fois les manomètres de l'étage stabilisés (<see cref="stopWhenSolved"/>).
    /// </summary>
    public class SteamVent : MonoBehaviour
    {
        [SerializeField] ParticleSystem jet;
        [SerializeField] AudioSource hiss;
        [SerializeField] Vector2 onRange = new Vector2(2f, 4f);
        [SerializeField] Vector2 offRange = new Vector2(4f, 9f);
        [Tooltip("Optionnel : la fuite s'arrête quand cet objectif est résolu.")]
        [SerializeField] PuzzleObjective stopWhenSolved;

        bool venting;
        bool stopped;
        float next;

        void OnEnable()
        {
            if (stopWhenSolved != null) stopWhenSolved.StateChanged += OnSolved;
        }

        void OnDisable()
        {
            if (stopWhenSolved != null) stopWhenSolved.StateChanged -= OnSolved;
        }

        void Start() => next = Time.time + Random.Range(0.5f, offRange.y);

        void OnSolved(bool solved)
        {
            if (!solved) return;
            stopped = true;
            Set(false);
        }

        void Update()
        {
            if (stopped || Time.time < next) return;
            Set(!venting);
            Vector2 range = venting ? onRange : offRange;
            next = Time.time + Random.Range(range.x, range.y);
        }

        void Set(bool on)
        {
            venting = on;
            if (jet != null)
            {
                if (on) jet.Play();
                else jet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (hiss != null)
            {
                if (on) hiss.Play();
                else hiss.Stop();
            }
        }
    }
}
