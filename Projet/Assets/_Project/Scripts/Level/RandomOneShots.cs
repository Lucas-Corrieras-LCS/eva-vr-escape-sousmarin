using UnityEngine;

namespace Abysses.Level
{
    /// <summary>
    /// Sons d'ambiance ponctuels (coque qui grince, gouttes, cliquetis) joués à intervalles aléatoires,
    /// à une position tirée parmi des points d'émission : la station « vit » autour du joueur.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RandomOneShots : MonoBehaviour
    {
        [SerializeField] AudioClip[] clips;
        [SerializeField] Transform[] emitters;
        [SerializeField] Vector2 intervalRange = new Vector2(6f, 15f);
        [SerializeField] Vector2 volumeRange = new Vector2(0.5f, 1f);
        [SerializeField] Vector2 pitchRange = new Vector2(0.85f, 1.15f);

        AudioSource source;
        float next;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            next = Time.time + Random.Range(intervalRange.x * 0.3f, intervalRange.y);
        }

        void Update()
        {
            if (Time.time < next || clips == null || clips.Length == 0) return;
            next = Time.time + Random.Range(intervalRange.x, intervalRange.y);
            if (emitters != null && emitters.Length > 0)
            {
                Transform emitter = emitters[Random.Range(0, emitters.Length)];
                if (emitter != null) transform.position = emitter.position;
            }
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], Random.Range(volumeRange.x, volumeRange.y));
        }
    }
}
