using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using Unity.XR.CoreUtils;

namespace Abysses.Player
{
    /// <summary>
    /// Rotation de la vue par crans au stick droit. Debug uniquement : LocomotionGate l'active avec le joystick
    /// et la coupe en build EVA (consigne C3). Script maison plutôt que le Snap Turn des Starter Assets :
    /// celui-ci réactive la téléportation sur la même main et se désactive après un survol de menu.
    /// </summary>
    public class StickTurn : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [Tooltip("Déplacement continu : le stick droit ne déplace plus, il tourne.")]
        [SerializeField] ContinuousMoveProvider moveProvider;
        [SerializeField, Range(15f, 90f)] float stepDegrees = 45f;
        [Tooltip("Inclinaison du stick qui déclenche un cran.")]
        [SerializeField, Range(0.3f, 1f)] float threshold = 0.7f;
        [Tooltip("Il faut relâcher le stick sous ce seuil avant le cran suivant.")]
        [SerializeField, Range(0f, 0.6f)] float rearm = 0.3f;

        InputAction stick;
        bool armed = true;

        void Awake()
        {
            stick = new InputAction("StickTurn", InputActionType.Value, expectedControlType: "Vector2");
            stick.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
        }

        void OnEnable()
        {
            stick.Enable();
            if (moveProvider != null) moveProvider.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        }

        void OnDisable() => stick.Disable();

        void OnDestroy() => stick.Dispose();

        void Update()
        {
            float x = stick.ReadValue<Vector2>().x;
            if (armed && Mathf.Abs(x) >= threshold)
            {
                origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * stepDegrees);
                armed = false;
            }
            else if (!armed && Mathf.Abs(x) <= rearm)
            {
                armed = true;
            }
        }
    }
}
