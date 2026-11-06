using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>Reçoit l'appui d'un <see cref="PokeButton"/> (disjoncteurs, cadenas, console…).</summary>
    public interface IPokeReceiver
    {
        void Pressed(int index);
    }

    /// <summary>
    /// Bouton physique : on l'appuie du doigt (Poke Interactor) ou par sélection. Le chapeau s'enfonce puis remonte,
    /// avec un clic et une petite vibration : on sait tout de suite que l'appui a été pris en compte.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class PokeButton : MonoBehaviour
    {
        [Tooltip("Composant qui implémente IPokeReceiver.")]
        [SerializeField] MonoBehaviour receiver;
        [SerializeField] int index;
        [Tooltip("Partie mobile (par défaut ce bouton) : elle s'enfonce vers +Z local.")]
        [SerializeField] Transform cap;
        [SerializeField, Range(0f, 0.03f)] float travel = 0.012f;
        [SerializeField] AudioSource click;

        const float PressDuration = 0.18f;

        XRSimpleInteractable interactable;
        IPokeReceiver target;
        Vector3 rest;
        float pressedAt = -1f;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            target = receiver as IPokeReceiver;
            if (cap == null) cap = transform;
            rest = cap.localPosition;
        }

        void OnEnable() => interactable.selectEntered.AddListener(OnPressed);
        void OnDisable() => interactable.selectEntered.RemoveListener(OnPressed);

        void OnPressed(SelectEnterEventArgs args)
        {
            target?.Pressed(index);
            if (click != null) click.Play();
            LocalRig.Pulse(0.3f, 0.04f);
            pressedAt = Time.unscaledTime;
        }

        void Update()
        {
            if (pressedAt < 0f) return;
            float k = Mathf.Clamp01((Time.unscaledTime - pressedAt) / PressDuration);
            // Enfoncé d'un coup, remontée progressive.
            cap.localPosition = rest + Vector3.forward * (travel * (1f - k));
            if (k >= 1f) pressedAt = -1f;
        }
    }
}
