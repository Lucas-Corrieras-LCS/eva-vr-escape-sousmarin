using UnityEngine;
using Abysses.Core;

namespace Abysses.Player
{
    /// <summary>
    /// Contrôle des déplacements artificiels (consigne C3).
    /// - Joystick (Move / Turn / Gravity) : actif seulement en debug, coupé en build EVA.
    /// - Téléportation, escalade, saut, grab move : toujours coupés, ils casseraient la correspondance réel/virtuel.
    /// </summary>
    public class LocomotionGate : MonoBehaviour
    {
        [Tooltip("Déplacements autorisés uniquement en debug.")]
        [SerializeField] GameObject[] debugOnly;
        [Tooltip("Déplacements toujours interdits.")]
        [SerializeField] GameObject[] alwaysDisabled;

        void Awake()
        {
            bool allow = GameConfig.Instance.AllowJoystickLocomotion;
            for (int i = 0; i < debugOnly.Length; i++)
                if (debugOnly[i] != null) debugOnly[i].SetActive(allow);
            for (int i = 0; i < alwaysDisabled.Length; i++)
                if (alwaysDisabled[i] != null) alwaysDisabled[i].SetActive(false);
        }
    }
}
