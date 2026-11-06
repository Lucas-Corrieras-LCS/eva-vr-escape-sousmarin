using UnityEngine;
using Abysses.Level;

namespace Abysses.Player
{
    /// <summary>
    /// Pendant la descente, les oreilles « se bouchent » : tous les sons sont étouffés (filtre passe-bas sur l'auditeur),
    /// puis se débouchent à l'arrivée. Aucun mouvement de caméra (nausée assurée en VR) : la sensation passe par le son.
    /// Le filtre n'est actif que pendant la descente (aucun coût CPU le reste du temps).
    /// </summary>
    [RequireComponent(typeof(AudioLowPassFilter))]
    public class DescentEars : MonoBehaviour
    {
        [SerializeField, Min(200f)] float muffledCutoff = 900f;
        [SerializeField, Min(0.1f)] float closeTime = 2.5f;
        [SerializeField, Min(0.1f)] float openTime = 0.6f;

        const float OpenCutoff = 22000f;

        AudioLowPassFilter filter;
        float target = OpenCutoff;
        float speed;

        void Awake()
        {
            filter = GetComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = OpenCutoff;
            filter.enabled = false;
        }

        void OnEnable() => GameStateManager.PhaseChanged += OnPhaseChanged;
        void OnDisable() => GameStateManager.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(GamePhase phase)
        {
            bool descending = phase == GamePhase.Descending;
            target = descending ? muffledCutoff : OpenCutoff;
            speed = (OpenCutoff - muffledCutoff) / (descending ? closeTime : openTime);
            if (descending) filter.enabled = true;
        }

        void Update()
        {
            if (!filter.enabled) return;
            filter.cutoffFrequency = Mathf.MoveTowards(filter.cutoffFrequency, target, speed * Time.deltaTime);
            if (target >= OpenCutoff && filter.cutoffFrequency >= OpenCutoff) filter.enabled = false;
        }
    }
}
