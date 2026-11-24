using UnityEngine;

namespace Abysses.Player
{
    /// <summary>
    /// Le corps physique du joueur (CharacterController du rig XR) prend la hauteur de la tête : un joueur grand, ou un sol
    /// du casque mal calibré, butait sur le linteau des portes et restait coincé à la sortie de la cabine.
    /// On plafonne sa hauteur après la mise à jour du toolkit XR (la tête, elle, n'est pas limitée).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class BodyHeightClamp : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxHeight = 1.6f;

        CharacterController body;

        void Awake() => body = GetComponent<CharacterController>();

        void LateUpdate()
        {
            if (body.height <= maxHeight) return;
            Vector3 center = body.center;
            center.y = maxHeight / 2f + body.skinWidth;
            body.height = maxHeight;
            body.center = center;
        }
    }
}
