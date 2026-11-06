using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Abysses.Player;

namespace Abysses.Interaction
{
    /// <summary>
    /// Volant de vanne local : on l'attrape et on tourne la main autour de son axe (Z local).
    /// Sens anti-horaire = ouverture. La valeur (0..1) est émise au plus toutes les <see cref="sendInterval"/> secondes,
    /// pour ne pas inonder le réseau ; rien n'est calculé quand le volant n'est pas tenu.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ValveWheel : MonoBehaviour
    {
        [Tooltip("Partie visuelle qui tourne autour de son axe Z local.")]
        [SerializeField] Transform wheel;
        [Tooltip("Nombre de tours pour passer de fermé (0) à grand ouvert (1).")]
        [SerializeField, Min(0.25f)] float turnsForFullRange = 1.5f;
        [SerializeField, Min(0.02f)] float sendInterval = 0.1f;
        [Tooltip("Un cran de vibration tous les N degrés de rotation du volant.")]
        [SerializeField, Min(5f)] float detentDegrees = 20f;

        /// <summary>Nouvelle position de la vanne (0..1), limitée en fréquence.</summary>
        public event Action<float> ValueChanged;

        public float Value { get; private set; }

        XRSimpleInteractable interactable;
        IXRSelectInteractor hand;
        float previousAngle;
        float lastSent = -1f;
        float nextSend;
        bool locked;
        int lastDetent;
        bool atStop;

        void Awake() => interactable = GetComponent<XRSimpleInteractable>();

        void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            hand = null;
        }

        /// <summary>Bloque le volant (la jauge qu'il pilote est stabilisée).</summary>
        public void SetLocked(bool value)
        {
            locked = value;
            if (locked) hand = null;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (locked) return;
            hand = args.interactorObject;
            previousAngle = HandAngle();
            lastDetent = Detent();
            atStop = false;
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (hand == null) return;
            hand = null;
            Send();
        }

        float HandAngle()
        {
            Vector3 local = wheel.parent.InverseTransformPoint(hand.GetAttachTransform(interactable).position) - wheel.localPosition;
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }

        void Update()
        {
            if (hand == null) return;

            float angle = HandAngle();
            float delta = Mathf.DeltaAngle(previousAngle, angle);
            previousAngle = angle;
            Value = Mathf.Clamp01(Value + delta / (360f * turnsForFullRange));
            wheel.localRotation = Quaternion.Euler(0f, 0f, Value * 360f * turnsForFullRange);

            // Retour haptique : un petit cran régulier, un choc franc en butée (grand ouvert / fermé).
            int detent = Detent();
            if (detent != lastDetent)
            {
                lastDetent = detent;
                LocalRig.Pulse(0.12f, 0.02f);
            }
            bool stop = Value <= 0f || Value >= 1f;
            if (stop && !atStop && Mathf.Abs(delta) > 0.01f) LocalRig.Pulse(0.55f, 0.06f);
            atStop = stop;

            if (Time.unscaledTime >= nextSend && Mathf.Abs(Value - lastSent) > 0.005f) Send();
        }

        int Detent() => Mathf.FloorToInt(Value * 360f * turnsForFullRange / detentDegrees);

        void Send()
        {
            lastSent = Value;
            nextSend = Time.unscaledTime + sendInterval;
            ValueChanged?.Invoke(Value);
        }
    }
}
