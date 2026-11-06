using UnityEngine;

namespace Abysses.Network
{
    /// <summary>
    /// Corps de l'avatar d'un coéquipier, déduit localement de la tête et des mains déjà synchronisées
    /// (aucune donnée réseau en plus) : buste sous le casque, orienté comme le regard (lissé),
    /// bras tendus de l'épaule à la main. Le coéquipier vu par le hublot ressemble à un plongeur, plus à une sphère.
    /// </summary>
    public class AvatarBody : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [Tooltip("Buste (enfant de cet objet, non synchronisé).")]
        [SerializeField] Transform torso;
        [SerializeField] Transform leftArm;
        [SerializeField] Transform rightArm;
        [SerializeField] float neckDrop = 0.42f;
        [SerializeField] float shoulderWidth = 0.19f;
        [SerializeField] float shoulderHeight = 0.2f;
        [SerializeField, Min(0.1f)] float turnSpeed = 6f;
        [SerializeField] float armThickness = 0.07f;

        void LateUpdate()
        {
            if (head == null || torso == null) return;

            // Le buste suit la tête en position, et son cap avec un léger retard (on tourne la tête sans tourner le corps).
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = torso.forward;
            Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            torso.SetPositionAndRotation(head.position - Vector3.up * neckDrop - forward.normalized * 0.06f,
                Quaternion.Slerp(torso.rotation, yaw, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime)));

            Stretch(leftArm, torso.TransformPoint(new Vector3(-shoulderWidth, shoulderHeight, 0f)), leftHand);
            Stretch(rightArm, torso.TransformPoint(new Vector3(shoulderWidth, shoulderHeight, 0f)), rightHand);
        }

        /// <summary>Cylindre (axe Y, hauteur 2) étiré de l'épaule jusqu'à la main.</summary>
        void Stretch(Transform arm, Vector3 shoulder, Transform hand)
        {
            if (arm == null || hand == null) return;
            Vector3 delta = hand.position - shoulder;
            float length = Mathf.Clamp(delta.magnitude, 0.05f, 0.8f);
            if (delta.sqrMagnitude < 0.0001f) return;
            arm.SetPositionAndRotation(shoulder + delta.normalized * (length * 0.5f), Quaternion.FromToRotation(Vector3.up, delta));
            arm.localScale = new Vector3(armThickness, length * 0.5f, armThickness);
        }
    }
}
