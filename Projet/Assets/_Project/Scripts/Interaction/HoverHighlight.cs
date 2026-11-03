using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Abysses.Interaction
{
    /// <summary>
    /// Surbrillance d'un objet attrapable / actionnable quand une main s'en approche (survol XRI, main ou doigt) :
    /// un halo additif légèrement plus grand que l'objet s'allume. On comprend d'un coup d'œil ce qui se manipule.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public class HoverHighlight : MonoBehaviour
    {
        [Tooltip("Halo (renderer additif désactivé par défaut).")]
        [SerializeField] Renderer halo;

        XRBaseInteractable interactable;

        void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            if (halo != null) halo.enabled = false;
        }

        void OnEnable()
        {
            interactable.hoverEntered.AddListener(OnHoverChanged);
            interactable.hoverExited.AddListener(OnHoverChanged);
            interactable.selectEntered.AddListener(OnSelectChanged);
            interactable.selectExited.AddListener(OnSelectChanged);
        }

        void OnDisable()
        {
            interactable.hoverEntered.RemoveListener(OnHoverChanged);
            interactable.hoverExited.RemoveListener(OnHoverChanged);
            interactable.selectEntered.RemoveListener(OnSelectChanged);
            interactable.selectExited.RemoveListener(OnSelectChanged);
            if (halo != null) halo.enabled = false;
        }

        void OnHoverChanged(BaseInteractionEventArgs args) => Refresh();
        void OnSelectChanged(BaseInteractionEventArgs args) => Refresh();

        // Allumé au survol, éteint pendant la prise (l'objet est déjà en main).
        void Refresh()
        {
            if (halo != null) halo.enabled = interactable.isHovered && !interactable.isSelected;
        }
    }
}
