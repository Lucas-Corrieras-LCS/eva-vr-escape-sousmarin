using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abysses.Player
{
    /// <summary>
    /// Debug uniquement (coupé en build EVA par <see cref="LocomotionGate"/>, comme le joystick des manettes) :
    /// jouer sans casque, à la manette de jeu branchée sur le PC / Mac.
    /// Stick gauche : avancer / se décaler (dans la direction du regard) ; stick droit : tourner.
    /// </summary>
    public class GamepadLocomotion : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [SerializeField, Range(0.5f, 4f)] float speed = 1.6f;
        [SerializeField, Range(30f, 180f)] float turnSpeed = 90f;
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.15f;

        CharacterController body;

        void Awake()
        {
            if (origin == null) origin = GetComponentInParent<XROrigin>();
            if (origin != null) body = origin.GetComponent<CharacterController>();
        }

        void Update()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || origin == null) return;

            Vector2 move = pad.leftStick.ReadValue();
            if (move.magnitude > deadZone)
            {
                Transform head = origin.Camera.transform;
                Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                Vector3 rightDir = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
                Vector3 delta = (forward * move.y + rightDir * move.x) * (speed * Time.deltaTime);
                if (body != null && body.enabled) body.Move(delta);
                else origin.transform.position += delta;
            }

            float turn = pad.rightStick.ReadValue().x;
            if (Mathf.Abs(turn) > deadZone)
                origin.RotateAroundCameraUsingOriginUp(turn * turnSpeed * Time.deltaTime);
        }
    }
}
