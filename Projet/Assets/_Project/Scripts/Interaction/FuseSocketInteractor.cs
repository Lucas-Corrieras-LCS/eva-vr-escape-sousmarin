using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Abysses.Interaction
{
    /// <summary>Socket qui n'accepte que le fusible portant le bon identifiant.</summary>
    public class FuseSocketInteractor : XRSocketInteractor
    {
        [SerializeField, Range(0, 7)] int requiredFuseId;

        public int RequiredFuseId => requiredFuseId;

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && Accepts(interactable.transform);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            return base.CanSelect(interactable) && Accepts(interactable.transform);
        }

        bool Accepts(Transform candidate)
        {
            return candidate.TryGetComponent(out FuseItem fuse) && fuse.FuseId == requiredFuseId;
        }
    }
}
