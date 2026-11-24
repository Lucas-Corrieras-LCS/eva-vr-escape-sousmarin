using UnityEngine;

namespace Abysses.Network
{
    /// <summary>
    /// Corps de l'avatar d'un coéquipier (scaphandrier), déduit localement de la tête et des mains déjà synchronisées
    /// (aucune donnée réseau en plus) :
    /// - buste sous le casque, orienté comme le regard (lissé : on tourne la tête sans tourner le corps) ;
    /// - bras en deux segments, coude calculé (IK à deux os, coude vers le bas et l'extérieur) ;
    /// - jambes de la hanche jusqu'au sol (y = 0, sol de tous les étages), bottes lestées posées à plat.
    /// Les segments sont des capsules (axe Y, hauteur 2) mises à l'échelle chaque frame.
    /// </summary>
    public class AvatarBody : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [Tooltip("Buste (enfant de cet objet, non synchronisé).")]
        [SerializeField] Transform torso;
        [SerializeField] Transform leftUpperArm;
        [SerializeField] Transform leftForearm;
        [SerializeField] Transform rightUpperArm;
        [SerializeField] Transform rightForearm;
        [SerializeField] Transform leftElbow;
        [SerializeField] Transform rightElbow;
        [SerializeField] Transform leftThigh;
        [SerializeField] Transform leftShin;
        [SerializeField] Transform rightThigh;
        [SerializeField] Transform rightShin;
        [SerializeField] Transform leftBoot;
        [SerializeField] Transform rightBoot;
        [SerializeField] float neckDrop = 0.42f;
        [SerializeField] float shoulderWidth = 0.2f;
        [SerializeField] float shoulderHeight = 0.2f;
        [SerializeField] float hipDrop = 0.32f;
        [SerializeField] float hipWidth = 0.1f;
        [SerializeField] float upperArmLength = 0.3f;
        [SerializeField] float forearmLength = 0.3f;
        [SerializeField] float armThickness = 0.085f;
        [SerializeField] float legThickness = 0.12f;
        [SerializeField, Min(0.1f)] float turnSpeed = 6f;

        bool placed;

        void LateUpdate()
        {
            if (head == null || torso == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = torso.forward;
            forward.Normalize();
            Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up);
            // Première image (apparition du coéquipier) : le buste se cale d'un coup, ensuite il suit le regard en douceur.
            Quaternion rotation = placed ? Quaternion.Slerp(torso.rotation, yaw, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime)) : yaw;
            placed = true;
            torso.SetPositionAndRotation(head.position - Vector3.up * neckDrop - forward * 0.06f, rotation);

            Arm(torso.TransformPoint(new Vector3(-shoulderWidth, shoulderHeight, 0f)), leftHand, -1f, leftUpperArm, leftForearm, leftElbow);
            Arm(torso.TransformPoint(new Vector3(shoulderWidth, shoulderHeight, 0f)), rightHand, 1f, rightUpperArm, rightForearm, rightElbow);
            Leg(torso.TransformPoint(new Vector3(-hipWidth, -hipDrop, 0f)), leftThigh, leftShin, leftBoot);
            Leg(torso.TransformPoint(new Vector3(hipWidth, -hipDrop, 0f)), rightThigh, rightShin, rightBoot);
        }

        /// <summary>IK à deux os : coude placé dans le plan épaule-main, poussé vers le bas et l'extérieur.</summary>
        void Arm(Vector3 shoulder, Transform hand, float side, Transform upper, Transform fore, Transform elbowJoint)
        {
            if (hand == null || upper == null || fore == null) return;
            Vector3 target = hand.position;
            Vector3 toHand = target - shoulder;
            float reach = upperArmLength + forearmLength;
            float distance = Mathf.Clamp(toHand.magnitude, 0.05f, reach * 0.999f);
            Vector3 direction = toHand.sqrMagnitude > 1e-6f ? toHand.normalized : -torso.up;
            // Loi des cosinus : distance de l'épaule au pied de la hauteur du coude, et hauteur du coude.
            float a = (upperArmLength * upperArmLength - forearmLength * forearmLength + distance * distance) / (2f * distance);
            float h = Mathf.Sqrt(Mathf.Max(0f, upperArmLength * upperArmLength - a * a));
            Vector3 hint = Vector3.ProjectOnPlane(-torso.up * 0.8f + torso.right * side * 0.5f - torso.forward * 0.2f, direction);
            if (hint.sqrMagnitude < 1e-6f) hint = Vector3.ProjectOnPlane(-Vector3.up, direction);
            Vector3 elbow = shoulder + direction * a + hint.normalized * h;
            Segment(upper, shoulder, elbow, armThickness);
            Segment(fore, elbow, shoulder + direction * distance, armThickness * 0.9f);
            if (elbowJoint != null) elbowJoint.position = elbow;
        }

        /// <summary>Jambe droite de la hanche au sol, genou légèrement en avant ; botte posée à plat sous le pied.</summary>
        void Leg(Vector3 hip, Transform thigh, Transform shin, Transform boot)
        {
            if (thigh == null || shin == null) return;
            Vector3 foot = new Vector3(hip.x, 0.09f, hip.z);
            if (foot.y > hip.y - 0.2f) foot.y = hip.y - 0.2f;
            Vector3 knee = Vector3.Lerp(hip, foot, 0.5f) + torso.forward * 0.04f;
            Segment(thigh, hip, knee, legThickness);
            Segment(shin, knee, foot, legThickness * 0.9f);
            if (boot != null) boot.SetPositionAndRotation(new Vector3(foot.x, Mathf.Max(0.06f, foot.y - 0.03f), foot.z) + torso.forward * 0.04f, Quaternion.LookRotation(torso.forward, Vector3.up));
        }

        /// <summary>Capsule (axe Y, hauteur 2) tendue entre deux points.</summary>
        static void Segment(Transform part, Vector3 from, Vector3 to, float thickness)
        {
            if (part == null) return;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f) return;
            part.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, delta));
            // Une capsule de hauteur 2 garde ses bouts ronds : la hauteur utile est la longueur + l'épaisseur.
            part.localScale = new Vector3(thickness, (length + thickness) * 0.5f, thickness);
        }
    }
}
