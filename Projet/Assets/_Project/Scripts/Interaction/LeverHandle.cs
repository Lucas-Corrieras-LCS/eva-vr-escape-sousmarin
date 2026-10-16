using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abysses.Interaction
{
    /// <summary>
    /// Levier physique local : on l'attrape, il suit la main en tournant autour de l'axe X du pivot.
    /// Arrivé en butée basse, il émet <see cref="Pulled"/>. L'état réseau (baissé / verrouillé) est imposé via <see cref="SetState"/>.
    /// Ne calcule rien quand il n'est ni tenu ni en mouvement.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class LeverHandle : MonoBehaviour
    {
        [Tooltip("Pivot qui tourne autour de son axe X local.")]
        [SerializeField] Transform pivot;
        [SerializeField] float upAngle = -35f;
        [SerializeField] float downAngle = 35f;
        [SerializeField, Min(1f)] float returnSpeed = 160f;
        [SerializeField, Min(0.5f)] float pullTolerance = 4f;

        public event Action Pulled;

        XRSimpleInteractable interactable;
        IXRSelectInteractor hand;
        float angle;
        float restAngle;
        bool locked;
        bool pulledThisGrab;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            angle = restAngle = upAngle;
            ApplyAngle();
        }

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

        /// <summary>État imposé par le réseau : position de repos (haut/bas) et verrouillage.</summary>
        public void SetState(bool down, bool isLocked)
        {
            restAngle = down ? downAngle : upAngle;
            locked = isLocked;
            if (locked) hand = null;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (locked) return;
            hand = args.interactorObject;
            pulledThisGrab = false;
        }

        void OnSelectExited(SelectExitEventArgs args) => hand = null;

        void Update()
        {
            if (hand != null)
            {
                Vector3 local = pivot.parent.InverseTransformPoint(hand.GetAttachTransform(interactable).position) - pivot.localPosition;
                float target = Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg;
                angle = Mathf.Clamp(target, Mathf.Min(upAngle, downAngle), Mathf.Max(upAngle, downAngle));
                if (!pulledThisGrab && Mathf.Abs(angle - downAngle) <= pullTolerance)
                {
                    pulledThisGrab = true;
                    Pulled?.Invoke();
                }
            }
            else if (!Mathf.Approximately(angle, restAngle))
            {
                angle = Mathf.MoveTowards(angle, restAngle, returnSpeed * Time.deltaTime);
            }
            else
            {
                return;
            }
            ApplyAngle();
        }

        void ApplyAngle() => pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
